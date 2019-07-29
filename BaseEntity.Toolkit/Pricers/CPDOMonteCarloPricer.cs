/*
 * CPDOMonteCarloPricer.cs
 *
 *
 */
using System;
using System.Data;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Rng;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   Price a Constant Proportion Debt Obligation 
  /// </summary>
  [Serializable]
  public class CPDOMonteCarloPricer : PricerBase , IPricer
  {
    // Logger
    private static readonly log4net.ILog
    logger = log4net.LogManager.GetLogger(typeof(CPDOMonteCarloPricer));

    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">Product to price</param>
    ///
    protected
    CPDOMonteCarloPricer(Product product)
      : base(product)
    { }

    /// <summary>
    ///   Constructor.
    /// </summary>
    ///
    /// <param name="cpdo">cpdo product to price</param>
    /// <param name="asOf">as-of date</param>
    /// <param name="settle">settlement date</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="referenceCurve">reference curve</param>
    /// <param name="lastResetFloatingBond">last reset floating bond</param>
    /// <param name="rollCost">roll cost</param>
    /// <param name="initialSpreadsOfIndices">initial level spreads(prices) of reference indices</param>
    /// <param name="corr">correlation coefficient for spreads</param>
    /// <param name="kappaP">mean reversion of spreads</param>
    /// <param name="thetaP">long run mean for spreads</param>
    /// <param name="sigmaP">volatility (curve)of spreads</param>
    /// <param name="weights">spread weights</param>
    /// <param name="recovery">average Recovery of underlying portfolio</param>
    /// <param name="cushion">cushion</param>
    /// <param name="lossFactor">loss factor</param>
    /// <param name="compChange">spread change due to compostition change at roll dates</param>
    /// <param name="rollDown">roll down</param>
    /// <param name="evalGridFreq">units for step size</param>
    /// <param name="notional">notional</param>
    /// <param name="simulations">number of simulated paths</param>
    ///
    public
    CPDOMonteCarloPricer(CPDO cpdo,
                         Dt asOf, Dt settle,
                         DiscountCurve discountCurve,
                         DiscountCurve referenceCurve,
                         double lastResetFloatingBond,
                         double rollCost,
                         double[] initialSpreadsOfIndices,
                         double corr,
                         double[] kappaP, double[] thetaP, double[] sigmaP,
                         double[] weights,
                         double recovery,
                         double cushion,
                         double lossFactor,
                         double compChange, //adjust spread at roll dates
                         double rollDown, // adjust spread at roll dates
                         Frequency evalGridFreq,
                         double notional,
                         int simulations)
      : base(cpdo, asOf, settle)
    {
      // TBD: errors check

      this.Product = cpdo;
      discountCurve_ = discountCurve;
      referenceCurve_ = referenceCurve;
      lastResetFloatingBond_ = lastResetFloatingBond;
      this.Notional = notional;
      initialSpreadsOfIndices_ = initialSpreadsOfIndices;
      corr_ = corr;
      kappaP_ = kappaP;
      thetaP_ = thetaP;
      sigmaP_ = sigmaP;
      weights_ = weights;
      recovery_ = recovery;
      cushion_ = cushion;
      lossFactor_ = lossFactor;
      rollCost_ = rollCost;
      rollDown_ = rollDown;
      evalGridFreq_ = evalGridFreq;
      simulations_ = simulations;
      compChange_ = compChange;

      cpdo.Notional = notional;
      cpdoBondPath_ = new CPDOBondPath(cpdo, asOf, settle,
                       discountCurve, referenceCurve,
                       lastResetFloatingBond, evalGridFreq);

      //construct roll cost array upfront
      SurvivalFitCalibrator fit = new SurvivalFitCalibrator(asOf, asOf, Recovery, DiscountCurve);
      int nSteps = 500; int maxSteps = 5000;
      double[] spreads = new double[nSteps];
      upDeltas_ = new double[maxSteps];

      for (int i = 0; i < nSteps; ++i)
      {
        if (i == 0)
          spreads[i] = 1;
        else
          spreads[i] = spreads[i - 1] + 1;
        SurvivalCurve flatSurvivalCurve = new SurvivalCurve(fit);
        flatSurvivalCurve.AddCDS("EvalDtToMaturity", Dt.CDSMaturity(asOf, "5 Years"), spreads[i] / 10000.0,
                        Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);
        flatSurvivalCurve.Fit();

        // construct CDS product and Pricer
        CDS globoxxCds = new CDS(asOf, Dt.CDSMaturity(asOf, "5 Year"), cpdo.Ccy, spreads[i] / 10000.0,
                     Cpdo.DayCount, Cpdo.Freq, Cpdo.BDConvention, Cpdo.Calendar);

        CDSCashflowPricer globoxxCdsPricer = new CDSCashflowPricer(globoxxCds, asOf, asOf, DiscountCurve, flatSurvivalCurve,
                                           null, 0, 0, TimeUnit.None);
        upDeltas_[i] = Sensitivities2.Spread01(globoxxCdsPricer, null, 0.5 * rollCost, 0, BumpFlags.BumpInPlace) * 0.5 * rollCost;
        //downDeltas[i] = Sensitivities.Spread01(globoxxCdsPricer, 0, 0.5 * rollCost) * 0.5 * rollCost;
      }

      for (int i = nSteps; i < maxSteps; ++i)
      {
        upDeltas_[i] = upDeltas_[nSteps - 1];
        //downDeltas[i] = downDeltas[nSteps - 1];
      }

    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///  Pv(Full Price) of the CPDO
    /// </summary>
    public override double ProductPv()
    {
      Dt[] evalDatesArr = (Dt[])CpdoBondPath.EvalDates.ToArray();
      Dt start = evalDatesArr[0]; Dt stop = evalDatesArr[evalDatesArr.Length - 1];//maturity
      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
      SimPvs = new double[Simulations];

      SimCpdoSpreads = new double[Simulations, evalDatesArr.Length];

      for (int i = 0; i < Simulations; ++i)
      {
        // simulate weighted path spreads
        double[] simWeightedSpreads = SimulateStochasticSpreads(
                                        InitialSpreadsOfIndices,
                                        Corr,
                                        start,
                                        stop,
                                        stepSize,
                                        stepUnit,
                                        evalDatesArr,
                                        Cpdo.DayCount,
                                        Weights,
                                        KappaP,
                                        ThetaP,
                                        SigmaP
                                        );

        for (int j = 0; j < evalDatesArr.Length; ++j)
          SimCpdoSpreads[i, j] = simWeightedSpreads[j];

        // construct spread curve
        if (simWeightedSpreads.Length != evalDatesArr.Length)
          throw new ArgumentException("Number of values must match number of maturities");

        Curve spreadCurve = new Curve(AsOf);
        spreadCurve.Interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const);

        for (int l = 0; l < evalDatesArr.Length; ++l)
        {
          if (simWeightedSpreads[l] > 0.0)
            spreadCurve.Add(evalDatesArr[l], simWeightedSpreads[l]);
          else
          {
            simWeightedSpreads[i] = 10e-10;
            spreadCurve.Add(evalDatesArr[l], simWeightedSpreads[l]);
          }
        }

        // return path Nav (Pv)
        CPDOMonteCarloModel mcPath = new CPDOMonteCarloModel(Cpdo,
                                            CpdoBondPath,
                                            AsOf, Settle,
                                            DiscountCurve,
                                            ReferenceCurve,
                                            spreadCurve,
                                            Recovery,
                                            LastResetFloatingBond,
                                            Cushion,
                                            LossFactor,
                                            RollCost,
                                            CompChange, //adjust spread at roll dates
                                            RollDown, // adjust spread at roll dates
                                            EvalGridFreq,
                                            UpDeltas);

        double pathPv = mcPath.GetPathPv();
        SimPvs[i] = pathPv;
      }

      return Statistics.Mean(SimPvs);
    }


    /// <summary>
    ///  Pv(Full Price) of the CPDO Scenario
    /// </summary>
    /// 
    /// <returns>Scenario Pv</returns>
    /// 
    public double ScenarioPv(Curve spreadCurve)
    {
      Dt[] evalDatesArr = (Dt[])CpdoBondPath.EvalDates.ToArray(); ;
      SimPvs = new double[1];

      SimCpdoSpreads = new double[1, evalDatesArr.Length];
      double[] simWeightedSpreads = new double[CpdoBondPath.EvalDates.Count];

      for (int i = 0; i < evalDatesArr.Length; ++i)
        simWeightedSpreads[i] = spreadCurve.Interpolate(CpdoBondPath.EvalDates[i]) * (1 + RollDown) * (1 + CompChange);

      for (int j = 0; j < evalDatesArr.Length; ++j)
        SimCpdoSpreads[0, j] = simWeightedSpreads[j];

      if (simWeightedSpreads.Length != evalDatesArr.Length)
        throw new ArgumentException("Number of values must match number of maturities");

      // return path Nav (Pv)
      CPDOMonteCarloModel mcPath = new CPDOMonteCarloModel(Cpdo,
                                            CpdoBondPath,
                                            AsOf, Settle,
                                            DiscountCurve,
                                            ReferenceCurve,
                                            spreadCurve,
                                            Recovery,
                                            LastResetFloatingBond,
                                            Cushion,
                                            LossFactor,
                                            RollCost,
                                            CompChange, //adjust spread at roll dates
                                            RollDown, // adjust spread at roll dates
                                            EvalGridFreq,
                                            UpDeltas);

      double pathPv = mcPath.GetPathPv();
      SimPvs[0] = pathPv;

      return pathPv;
    }


    /// <summary>
    ///  DataTable of the CPDO Scenario
    /// </summary>
    /// 
    /// <param name="spreadCurve">forward spread curve</param>
    /// 
    /// <returns>Scenario DataTable</returns>
    /// 
    public DataTable ScenarioTable(Curve spreadCurve)
    {
      Dt[] evalDatesArr = (Dt[])CpdoBondPath.EvalDates.ToArray(); ;
      SimPvs = new double[1];

      SimCpdoSpreads = new double[1, evalDatesArr.Length];
      double[] simWeightedSpreads = new double[CpdoBondPath.EvalDates.Count];

      for (int i = 0; i < evalDatesArr.Length; ++i)
        simWeightedSpreads[i] = spreadCurve.Interpolate(CpdoBondPath.EvalDates[i]) * (1 + RollDown) * (1 + CompChange);

      for (int j = 0; j < evalDatesArr.Length; ++j)
        SimCpdoSpreads[0, j] = simWeightedSpreads[j];

      if (simWeightedSpreads.Length != evalDatesArr.Length)
        throw new ArgumentException("Number of values must match number of maturities");

      // return path Nav (Pv)
      CPDOMonteCarloModel mcPath = new CPDOMonteCarloModel(Cpdo,
                                            CpdoBondPath,
                                            AsOf, Settle,
                                            DiscountCurve,
                                            ReferenceCurve,
                                            spreadCurve,
                                            Recovery,
                                            LastResetFloatingBond,
                                            Cushion,
                                            LossFactor,
                                            RollCost,
                                            CompChange, //adjust spread at roll dates
                                            RollDown, // adjust spread at roll dates
                                            EvalGridFreq,
                                            UpDeltas);

      DataTable pathDataTable = mcPath.FillPathDataTable();

      return pathDataTable;
    }


    /// <summary>
    ///   Get Simulated Nav's (Pv's)
    /// </summary>
    /// <returns>Output data for 1 simulation path</returns>
    public double GetCPDOSimPvSinglePath(int mcRun)
    {

      // validate mcRun input
      if (mcRun < 0)
        throw new System.SystemException("The Monte Carlo simulation no. has to be >=0");
      if (mcRun > 0 && mcRun > Simulations)
        throw new System.SystemException("The path no. is > than the no. of simlation paths");

      if (SimPvs != null)
        return SimPvs[mcRun];
      else
      {
        ProductPv();
        return SimPvs[mcRun];
      }

    }


    /// <summary>
    ///   Get Floating Bond Pv
    /// </summary>
    /// <returns>Return Pv of floating bond</returns>
    public double GetCPDOFloatingBondPv()
    {
      return CpdoBondPath.FloatingBondForwardValues[0];
    }

    /// <summary>
    ///   Get Floating Bond Accrued
    /// </summary>
    /// <returns>Return the accrual of floating bond</returns>
    public double GetCPDOFloatingBondAccrued()
    {
      return CpdoBondPath.FloatingBondAccruals[1] * Notional;
    }

    /// <summary>
    ///   Get Simulated Nav's (Pv's)
    /// </summary>
    /// <returns>Nav's for all simulation paths</returns>
    public double[] GetCPDOSimPvs()
    {
      if (SimPvs != null)
        return SimPvs;
      else
      {
        ProductPv();
        return SimPvs;
      }

    }

    /// <summary>
    ///   Get Simulated Spreads for a path
    /// </summary>
    /// <returns>Simulated Spreads for a path</returns>
    public object[] GetCPDOSimSpreads(int mcRun)
    {
      object[] simSpreadsPathArray = new object[SimCpdoSpreads.GetLength(1)]; ;
      if (SimPvs != null)
      {
        for (int j = 0; j < SimCpdoSpreads.GetLength(1); ++j)
          simSpreadsPathArray[j] = (double)SimCpdoSpreads[mcRun, j];
        return simSpreadsPathArray;
      }
      else
      {
        ProductPv();
        for (int j = 0; j < SimCpdoSpreads.GetLength(1); ++j)
          simSpreadsPathArray[j] = (double)SimCpdoSpreads[mcRun, j];
        return simSpreadsPathArray;
      }

    }

    /// <summary>
    ///   Get Eval Dates
    /// </summary>
    /// <returns>Evaluation Dates(time-grid)</returns>
    public Dt[] GetCPDOEvalDates()
    {
      return CpdoBondPath.EvalDates.ToArray();
    }

    /// <summary>
    ///   Generate correlated weighted Spreads path
    /// </summary>
    public double[] SimulateStochasticSpreads(
                        double[] x0,
                        double corr,
                        Dt start,
                        Dt stop,
                        int stepSize,
                        TimeUnit stepUnit,
                        Dt[] includeDates,
                        DayCount dc,
                        double[] weights,
                        double[] kappaP,
                        double[] thetaP,
                        double[] sigmaP
                        )
    {
      int dim = x0.Length;

      if ((kappaP.Length != thetaP.Length) || (kappaP.Length != sigmaP.Length))
        throw new ArgumentException("parameter arrays must have same size");

      if (kappaP.Length != x0.Length)
        throw new ArgumentException("Number of dimensions (x0) must match size of params array");

      if (weights.Length != x0.Length)
        throw new ArgumentException("Number of dimensions (x0) must match size of params array");

      // fill in correlation matrix
      double[,] corrMatrix = new double[dim, dim];
      for (int i = 0; i < dim; ++i)
        for (int j = 0; j < dim; ++j)
          corrMatrix[i, j] = corr;
      for (int i = 0; i < dim; ++i)
        corrMatrix[i, i] = 1;

      StochasticRng vasicekRng = new StochasticRng(x0,
                                                    corrMatrix,
                                                    start, stop,
                                                    stepSize, stepUnit,
                                                    includeDates,
                                                    dc);
      vasicekRng.Rng.Seed = RandomNumberGenerator.RandomSeed;

      MultiDimVasicekFn vasicekNDimFn = new MultiDimVasicekFn(kappaP, thetaP, sigmaP);
      vasicekRng.Simulate(vasicekNDimFn);

      double[][] states = vasicekRng.GetStates();

      int N = states.GetLength(0);
      int M = states[0].GetLength(0); // all states should have same no. of elements
      double[] simWeightedSpreads = new double[N];

      for (int i = 0; i < N; i++)
      {
        simWeightedSpreads[i] = 0;
        for (int j = 0; j < states[i].Length; ++j)
        {
          simWeightedSpreads[i] += states[i][j] * weights[j];

        }
      }

      return simWeightedSpreads;

    }


    #endregion // Methods

    #region Properties

    /// <summary>
    ///  CPDO
    /// </summary>
    public CPDO Cpdo
    {
      get
      {
        return (CPDO)this.Product;
      }
    }

    /// <summary>
    ///  CPDO Bond Path
    /// </summary>
    public CPDOBondPath CpdoBondPath
    {
      get
      {
        return (CPDOBondPath)cpdoBondPath_;
      }
    }

    /// <summary>
    ///  Last Reset of Floating Bond
    /// </summary>
    public double LastResetFloatingBond
    {
      get
      {
        return lastResetFloatingBond_;
      }
    }

    /// <summary>
    ///  CPDO Simulated Spreads
    /// </summary>
    public double[,] SimCpdoSpreads
    {
      get
      {
        return simCpdoSpreads_;
      }
      set
      {
        if (value == null)
          throw new ArgumentException("Empty Array");
        simCpdoSpreads_ = value;
      }
    }

    /// <summary>
    ///  Saved Delta's (upbump)
    /// </summary>
    public double[] UpDeltas
    {
      get
      {
        return upDeltas_;
      }
    }

    /// <summary>
    ///  Kappa cofficients
    /// </summary>
    public double[] KappaP
    {
      get
      {
        return kappaP_;
      }
    }

    /// <summary>
    ///  Theta cofficients
    /// </summary>
    public double[] ThetaP
    {
      get
      {
        return thetaP_;
      }
    }

    /// <summary>
    ///  Sigma cofficients
    /// </summary>
    public double[] SigmaP
    {
      get
      {
        return sigmaP_;
      }
    }

    /// <summary>
    ///  Cpdo Spreads
    /// </summary>
    public double[] InitialSpreadsOfIndices
    {
      get
      {
        return initialSpreadsOfIndices_;
      }
    }

    /// <summary>
    ///  Spread weights
    /// </summary>
    public double[] Weights
    {
      get
      {
        return weights_;
      }
    }

    /// <summary>
    ///  Simulated Pvs (Navs)
    /// </summary>
    public double[] SimPvs
    {
      get
      {
        return simPvs_;
      }
      set
      {
        if (value == null)
          throw new ArgumentException("Empty Array");
        simPvs_ = value;
      }
    }

    /// <summary>
    ///  Recoveries
    /// </summary>
    public double Recovery
    {
      get
      {
        return recovery_;
      }
    }

    /// <summary>
    ///  Correlation
    /// </summary>
    public double Corr
    {
      get
      {
        return corr_;
      }
    }

    /// <summary>
    ///  Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get
      {
        return discountCurve_;
      }
    }
    /// <summary>
    ///  Discount curve
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get
      {
        return referenceCurve_;
      }
    }

    /// <summary>
    ///  Shortfall cushion
    /// </summary>
    public double Cushion
    {
      get
      {
        return cushion_;
      }
    }


    /// <summary>
    ///  Loss Factor
    /// </summary>
    public double LossFactor
    {
      get
      {
        return lossFactor_;
      }
    }

    /// <summary>
    ///  Roll Cost
    /// </summary>
    public double RollCost
    {
      get
      {
        return rollCost_;
      }
    }

    /// <summary>
    ///  Roll Down Cost
    /// </summary>
    public double RollDown
    {
      get
      {
        return rollDown_;
      }
    }

    /// <summary>
    ///  Composition Change
    /// </summary>
    public double CompChange
    {
      get
      {
        return compChange_;
      }
    }

    /// <summary>
    ///  Simulations
    /// </summary>
    public int Simulations
    {
      get
      {
        return simulations_;
      }
    }

    /// <summary>
    ///  Evaluation-grid frequency
    /// </summary>
    public Frequency EvalGridFreq
    {
      get
      {
        return evalGridFreq_;
      }
    }

    #endregion Properties

    #region Data

    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private double lastResetFloatingBond_;
    private double[] initialSpreadsOfIndices_;
    private double corr_;
    private double[] kappaP_;
    private double[] thetaP_;
    private double[] sigmaP_;
    private double[] weights_;
    private double recovery_;
    private Frequency evalGridFreq_;
    private int simulations_;
    private double cushion_;
    private double lossFactor_;
    private double rollCost_;
    private double compChange_; //adjust spread at roll dates
    private double rollDown_; // adjust spread at roll dates
    private double[] upDeltas_;

    private double[,] simCpdoSpreads_;    // stores simulated spreads for all simulations
    private double[] simPvs_;    // stores simulated nav's
    private CPDOBondPath cpdoBondPath_;    // stores all bond related data

    #endregion Data

  } // class CPDOMonteCarloPricer

}
