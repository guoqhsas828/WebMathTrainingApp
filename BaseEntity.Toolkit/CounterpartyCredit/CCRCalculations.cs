using System;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Ccr
{

  #region Delegates

  /// <summary>
  /// CCR Function delegate
  /// </summary>
  /// <param name="date">Date index</param>
  /// <param name="discount">true if discounted.</param>
  /// <param name="exposure">Risky party exposure</param>
  /// <param name="radonNikodym">Radon Nikodym Derivative, can be CptyRn, OwnRn, ZeroRn, or FundingRn.</param>
  /// <param name="pVal">Quantile for distribution measure</param>
  /// <remarks>
  /// This delegate CcrFunction can assign the
  /// following functions: 
  /// <list type="bullet">
  /// <item><description>
  /// Pv: Calculate the discounted/undiscounted expected exposure or negative exposure at some given time.
  /// </description></item>
  /// <item><description>
  /// FundingCost: Calculate average value of the counterparty exposure multiplying borrow spread at some given time. 
  /// This is the integrand of the numerical integration for FCA calculation.
  /// </description></item>
  /// <item><description>
  /// FundingBenefit: Calculate average value of the booking entity exposure multiplying lend spread at some given time. 
  /// This is the integrand of the numerical integration for FBA calculation.
  /// </description></item>
  /// <item><description>
  /// Sigma: Calculate the standard deviation of EE, NEE, DiscountedEE, DiscountedNEE.
  /// </description></item>
  /// <item><description>
  /// StdError: Calculate the standard error of EE, NEE, DiscountedEE, DiscountedNEE, expressed as a percentage of the expected value.
  /// </description></item>
  /// <item><description>
  /// Pfe: Calculate the maximum amount of discounted/undiscounted and positive/negative exposure expected to occur on a future date with a high degree of statistical confidence.
  /// </description></item>
  /// </list>
  /// </remarks>
  public delegate double CcrFunction(
    int date, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym, double pVal);

  /// <summary>
  /// Radon Nikodym Derivative delegate.
  /// </summary>
  /// <param name="path">Simulation path.</param>
  /// <param name="date">Date index.</param>
  /// <remarks>
  /// <para>
  /// Denote the counterparty and booking entity default time by <m>\tau_c</m> and <m>\tau_o</m> respectively.
  /// Let <m>P</m> be the simulation measure (spot libor measure). </para>
  /// <para>
  /// This delegate <see cref="RadonNikodymDerivative">RadonNikodymDerivative</see> can assign the
  ///  following RadonNikodym derivatives: 
  /// </para>
  /// <list type="bullet">
  /// <item><description><c>CptyRn</c> represents the Radon Nikodym derivatives condition
  ///  on event of the counter party defaulting at date index.
  ///  <math env="align*">
  ///  \text{Unilateral  CptyRn} = \frac{dP(\cdot|\tau_c = t)}{dP(\cdot)}.
  ///  </math>
  /// <math env="align*">
  ///  \text{Bilateral  CptyRn} = \frac{dP(\cdot|\tau_o > t, \tau_c = t)}{dP(\cdot)}.
  ///  </math>
  /// </description></item>
  /// <item><description><c>OwnRn</c> represents the Radon Nikodym derivatives condition 
  /// on event of the booking entity defaulting at date index.
  /// <math env="align*">
  ///  \text{Unilateral OwnRn} = \frac{dP(\cdot|\tau_o = t)}{dP(\cdot)}.
  ///  </math>
  /// <math env="align*">
  ///  \text{Bilateral OwnRn} = \frac{dP(\cdot|\tau_c > t, \tau_o = t)}{dP(\cdot)}.
  ///  </math>
  /// </description></item>
  /// <item><description><c>ZeroRn</c> represents the Radon Nikodym derivative to change the measure 
  /// from simulation measure to risk neutral measure.
  /// <math env="align*">
  ///  \text{ZeroRn} = 1.
  ///  </math>
  /// </description></item>
  /// <item><description><c>FundingRn</c> represents the Radon Nikodym derivative to 
  /// condition on event of booking entity surviving at date index.
  /// <math env="align*">
  ///  \text{Unilateral FundingRn} = \frac{dP(\cdot|\tau_o > t)}{dP(\cdot)}.
  ///  </math>
  /// <math env="align*">
  ///  \text{Bilateral FundingRn} = \frac{dP(\cdot|\tau_c > t, \tau_o > t)}{dP(\cdot)}.
  ///  </math></description></item>
  /// </list>
  /// </remarks>
  public delegate double RadonNikodymDerivative(SimulatedPathValues path, int date);

  #endregion

  /// <summary>
  /// Store raw path results and compute relevant CCR measures
  /// </summary>
  [Serializable]
  internal class CCRCalculations : ICounterpartyCreditRiskCalculations
  {
    #region Data

    internal double CptyDefaultTimeCorrelation { get; set; }
    internal readonly FactorLoadingCollection FactorLoadings;
    internal readonly VolatilityCollection Volatilities;
    internal readonly CCRMarketEnvironment Environment;
    internal readonly Dt[] ExposureDates;
    internal readonly PortfolioData Portfolio;
    internal readonly MultiStreamRng.Type RngType;
    internal readonly int SampleSize;
    protected internal Tuple<Dt[], double[]>[] DefaultKernel;
    protected internal ISimulatedValues SimulatedValues;
    protected internal Tuple<Dt[], double[]> SurvivalKernel;
    protected internal Tuple<Dt[], double[]> NoDefaultKernel;
    private readonly bool _isUnilateral;
    protected internal ISimulationModel SimulationModel;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor 
    /// </summary>
    /// <param name="simulationModel">Simulation model (i.e., LIBOR market model, Hull-White model)</param>
    /// <param name="sampleSize">Sample size</param>
    /// <param name="exposureDates">Exposure dates</param>
    /// <param name="environment">Market environment</param>
    /// <param name="volatilities">Volatilities of each underlying process</param>
    /// <param name="factorLoadings">Factor loadings of each underlying process</param>
    /// <param name="rngType">Random number generator</param>
    /// <param name="portfolio">Portfolio data</param>
    /// <param name="cptyDefaultTimeCorrelation">Default time correlation between default time of the counterparty and booking entity</param>
    /// <param name="unilateral"> treat default unilaterally or jointly (first-to-default)</param>
    /// <returns>CCRCalculations objects</returns>
    internal CCRCalculations(
      ISimulationModel simulationModel,
      int sampleSize,
      Dt[] exposureDates,
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      MultiStreamRng.Type rngType,
      PortfolioData portfolio,
      double cptyDefaultTimeCorrelation, 
      bool unilateral 
      )
    {
      SimulationModel = simulationModel
        ?? SimulationModels.LiborMarketModel;
      SampleSize = sampleSize;
      ExposureDates = exposureDates;
      Environment = environment;
      Volatilities = volatilities;
      FactorLoadings = factorLoadings;
      RngType = rngType;
      Portfolio = portfolio;
      CptyDefaultTimeCorrelation = cptyDefaultTimeCorrelation;
      _isUnilateral = unilateral; 
    }

    #endregion

    #region Methods

    /// <summary>
    /// Create simulation engine
    /// </summary>
    /// <returns>Simulation engine</returns>
    internal virtual Simulator CreateSimulator()
    {
      var simulDates = Simulations.GenerateSimulationDates(Environment.AsOf, ExposureDates, Environment.Tenors,
                                                           Environment.GridSize);
      return Simulations.CreateSimulator(SimulationModel, SampleSize, simulDates, Environment, Volatilities, FactorLoadings, CptyDefaultTimeCorrelation);
    }

    /// <summary>
    /// Create multi-stream random number generator
    /// </summary>
    /// <param name="engine">simulator</param>
    /// <returns></returns>
    internal virtual MultiStreamRng CreateRng(Simulator engine)
    {
      return MultiStreamRng.Create(RngType, engine.Dimension, engine.SimulationTimeGrid);
    }

    /// <summary>
    /// Perform calculations and simulate Default Kernel, Survival Kernel, and NoDefault Kernel.
    /// </summary>
    /// <remarks>
    /// <para>This function simulate the Discount Factors, Radon Nikodym Derivatives and Credit Spreads for each path at each simulation date.</para>
    /// <para>Besides, this function also simulate the Default kernel, Survival Kernel, and NoDefault Kernel for XVA calculations.  </para>
    /// <list type="bullet">
    /// <item><description>
    /// DefaultKernel[0] is the kernel used for CVA calculation.
    /// <math env="align*">
    /// \text{Bilateral DefaultKernel[0]} &amp; = \mathbb{P} (\tau_c = t, \tau_o > t) dt \\\\
    /// \text{Unilateral DefaultKernel[0]} &amp; = \mathbb{P} (\tau_c = t) dt 
    /// </math>
    /// DefaultKernel[1] is the kernel used for DVA calculation.
    /// <math env="align*">
    /// \text{Bilateral DefaultKernel[1]} &amp; = \mathbb{P} (\tau_o = t, \tau_c > t) dt \\\\
    /// \text{Unilateral DefaultKernel[1]} &amp; = \mathbb{P} (\tau_o = t) dt 
    /// </math>
    /// </description></item>
    /// <item><description>
    /// SurvivalKernel is the kernel used for FBA/FCA calculation.
    /// <math env="align*">
    /// \text{Bilateral SurvivalKernel} &amp; = \mathbb{P} (\tau_c > t, \tau_o > t) dt \\\\
    /// \text{Unilateral SurvivalKernel} &amp; = \mathbb{P} (\tau_o > t) dt 
    /// </math>
    /// </description></item>
    /// <item><description>
    /// NoDefaultKernel is the kernel used for FBA/FCA calculation, assuming that there is no default for both parties..
    /// <math env="align*">
    /// \text{NoDefaultKernel} = dt \\\\
    /// </math>
    /// </description></item>
    /// </list>                                                                                                                 
    /// </remarks>
    protected virtual void Simulate()
    {
      using (var engine = CreateSimulator())
      {
        var rng = CreateRng(engine);
        SimulatedValues = Simulations.CalculateExposures(ExposureDates, engine, rng, Environment, Portfolio, IsUnilateral);
        foreach (var p in Portfolio.Exotics)
        {
          int nettingSet = p.Item3;
          var grid = Simulations.GenerateLeastSquaresGrid(Environment, engine.SimulationDates, engine.PathCount, RngType,
                                                          Volatilities, FactorLoadings, p, ExposureDates);
          foreach (var v in SimulatedValues.Paths)
          {
            int idx = v.Id;
            for (int t = 0; t < v.DateCount; ++t)
              v.SetPortfolioValue(t, nettingSet, v.GetPortfolioValue(t, nettingSet) + grid[idx, t]);
          }
        }

        //Simulate the Default Kernel.
        if (Environment.CptyCcy.Length >= 2 && IsUnilateral)
          //we simulate unilateral default kernel with at least two survival curves 
          DefaultKernel = Environment.CptyIndex.Select((index, i) =>
          {
            if (i >= 2) return null;
            var krn =
                  new double[engine.SimulationDates.Length];
            engine.DefaultKernel(i + 3, krn);
            return
                new Tuple<Dt[], double[]>(
                    engine.SimulationDates, krn);
          }).ToArray();
        else
          // case1: simulate unilateral default kernel with at most one survival curve 
          // case2: simulate bilateral default kernel. 
          // If we simulate bilateral default kernel with only one survival curve,
          // we actually simulate unilateral default kernel.
          DefaultKernel = Environment.CptyIndex.Select((index, i) =>
          {
            if (i >= 2) return null;
            var krn =
              new double[engine.SimulationDates.Length];
            engine.DefaultKernel(i, krn);
            return
              new Tuple<Dt[], double[]>(
                engine.SimulationDates, krn);
          }).ToArray();

        //Simulate the Survival Kernel. 
        if (Environment.CptyIndex.Length >= 2)
        // we have both cpty curve and own curve
        {
          var krn = new double[engine.SimulationDates.Length];
          engine.SurvivalKernel((IsUnilateral) ? 1 : 2, krn);
          SurvivalKernel = new Tuple<Dt[], double[]>(engine.SimulationDates, krn);
        }
        else if (Environment.CptyIndex.Length == 1)
          // we don't have own curve, by default, own survival probability = 1.
        {
          var krn = new double[engine.SimulationDates.Length];
          engine.SurvivalKernel((IsUnilateral) ? 3 : 4, krn);
          SurvivalKernel = new Tuple<Dt[], double[]>(engine.SimulationDates, krn);
        }

        //Simulate the NoDefault Kernel.
        var pretime = Environment.AsOf;
        var krn0 = new double[engine.SimulationDates.Length];
        for (int t = 0; t < engine.SimulationDates.Length; ++t)
        {
          krn0[t] = (engine.SimulationDates[t] - pretime) / 365.0;
          pretime = engine.SimulationDates[t];
        }
        NoDefaultKernel = new Tuple<Dt[], double[]>(engine.SimulationDates, krn0);
  
      }
    }


    /// <summary>
    /// Compute the discount or undiscounted expected exposure with applying Randon Nikodym derivatives.  
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="discount">true if discounted.</param>
    /// <param name="exposure">Risky party Exposure</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative, can be CptyRn, OwnRn, or ZeroRn.</param>
    /// <param name="pVal">Quantile for distribution measure, which is not used in this function.</param>
    /// <returns>The expected value of discounted/undiscounted, and positive/negative exposure</returns>
    /// <remarks>
    /// We have the following notations:
    /// <math env="align*">
    /// d &amp;-- \text{ The simulation date index.} \\\\
    /// j &amp;-- \text{ The simulation path index.} \\\\
    /// N &amp;-- \text{ The simulation path numbers.} \\\\
    /// {E_d^j} &amp;-- \text{ The netted and collateralised risky party exposure at date <m>d</m> and the <m>j</m>-th path. Depending on the risky party, } 
    /// E_d^j \text{ can be counterparty exposure } {X_d^j}^+, \text{ booking entity exposure } {X_d^j}^-, \text{ or exposure } X_d^j.  \\\\
    /// D_d^j &amp;-- \text{ The stochastic discount factor at date <m>d</m> and the <m>j</m>-th path.} \\\\
    /// R_d^j &amp;-- \text{ The Radon Nikodym Derivative at date <m>d</m> and the <m>j</m>-th path. It can be CptyRn, OwnRn, or ZeroRn.} 
    /// </math>
    /// Depending on the flag discount, we have:
    /// <math env="align*">
    /// \text{Discounted PV(d)} &amp;= \frac{\sum_{j=1}^N {E_d^j} \cdot D_d^j \cdot R_d^j }{\sum_{j=1}^N R_d^j} \\\\
    /// \text{Undiscounted PV(d)} &amp;= \frac{\sum_{j=1}^N {E_d^j} \cdot D_d^j \cdot R_d^j }{\sum_{j=1}^N D_d^j \cdot R_d^j} \\\\
    /// </math>
    /// </remarks>
    protected virtual double Pv(int d, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym,
                                double pVal)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight; // For Semi-Analytic engine, wt is the weight for Gauss Lengendre numerical integration. For Monte Carlo engine, wt = 1.
        double rn = radonNikodym(p, d); // R_d^j
        double w = wt*rn;
        double df = p.GetDiscountFactor(d); // D_d^j
        double e = exposure.Compute(p, d)*df;
        retVal += w*e;
        norm += discount ? w : w*df;
      }
      return (norm <= 0.0) ? 0.0 : retVal/norm;
    }

    /// <summary>
    /// Compute the expected value of Randon Nikodym derivatives at a given date.
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="discount">true if discounted, will not be used in this function.</param>
    /// <param name="exposure">Risky party exposure, will not be used in this function.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative, can be CptyRn, OwnRn, FundingRn, or ZeroRn.</param>
    /// <param name="pVal">Quantile for distribution measure, which is not used in this function.</param>
    /// <returns>The expected value of Randon Nikodym derivatives at a given date.</returns>
    /// <remarks>
    /// Denote the simulation date index by <m>d</m>, path index by <m>j</m>, and total path number by <m>N</m>. 
    /// Let <m>CptyRn_d^j</m> be the counterparty default Radon Nikodym derivative at date <m>d</m> and the <m>j-th</m> path, 
    /// <m>OwnRn_d^j</m> be the booking entity default Radon Nikodym derivative at date <m>d</m> and the <m>j-th</m> path,
    /// and <m>FundingRn_d^j</m> be the booking entity survival Radon Nikodym derivative at date <m>d</m> and the <m>j-th</m> path.
    /// Then we have:
    /// <math env="align*">
    /// \text{CptyRn(d)} &amp;= \frac{\sum_{j=1}^N CptyRn_d^j }{N} \\\\
    /// \text{OwnRn(d)} &amp;= \frac{\sum_{j=1}^N OwnRn_d^j }{N} \\\\
    /// \text{FundingRn(d)} &amp;= \frac{\sum_{j=1}^N FundingRn_d^j }{N} \\\\
    /// \text{ZeroRn(d)} &amp; \equiv 1
    /// </math>
    /// </remarks>
    protected virtual double RnDensity(int d, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym,
                                double pVal)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double rn = radonNikodym(p, d); 
        retVal += rn;
      }
      return retVal / calc.SimulatedValues.PathCount;
    }


    /// <summary>
    /// The CCR function for FCA calculation. 
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="discount">true if discounted</param>
    /// <param name="exposure">Counterparty exposure</param>
    /// <param name="radonNikodym">FundingRn</param>
    /// <param name="pVal">Quantile for distribution measure, which is not used in this function.</param>
    /// <returns>The weighted average value of counterparty exposure multiplying borrow spread.</returns>
    /// <remarks>
    /// We have the following notations:
    /// <math env="align*">
    /// d &amp;-- \text{ The simulation date index.} \\\\
    /// j &amp;-- \text{ The simulation path index.} \\\\
    /// N &amp;-- \text{ The simulation path numbers.} \\\\
    /// {X_d^j}^+ &amp;-- \text{ The netted and collateralised counterparty exposure at date <m>d</m> and the <m>j</m>-th path.} \\\\
    /// D_d^j &amp;-- \text{ The stochastic discount factor at date <m>d</m> and the <m>j</m>-th path.} \\\\
    /// SB_d^j &amp;-- \text{ The borrow spread at date <m>d</m> and the <m>j</m>-th path.} \\\\
    /// FR_d^j &amp;-- \text{ FundingRn at date <m>d</m> and the <m>j</m>-th path, which is the Radon Nikodym Derivative for FCA calculation.} 
    /// </math>
    /// Depending on the flag discount, we have:
    /// <math env="align*">
    /// \text{Discounted FundingCost(d)} &amp;= \frac{\sum_{j=1}^N {X_d^j}^+ \cdot SB_d^j \cdot D_d^j  \cdot FR_d^j }{\sum_{j=1}^N FR_d^j} \\\\
    /// \text{Undiscounted FundingCost(d)} &amp;= \frac{\sum_{j=1}^N {X_d^j}^+ \cdot SB_d^j \cdot D_d^j  \cdot FR_d^j }{\sum_{j=1}^N D_d^j \cdot FR_d^j} \\\\
    /// </math>
    /// </remarks>
    protected virtual double FundingCost(int d, bool discount, PathWiseExposure exposure,
                                         RadonNikodymDerivative radonNikodym, double pVal)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt*rn;
        double df = p.GetDiscountFactor(d);
        double e = exposure.Compute(p, d) * df * p.GetBorrowSpread(d);
        retVal += w * e;
        norm += discount ? w : w * df;
      }
      return (norm <= 0.0) ? 0.0 : retVal / norm;
    }


    /// <summary>
    /// The CCR function for FBA calculation. 
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="discount">true if discounted</param>
    /// <param name="exposure">Booking entity exposure</param>
    /// <param name="radonNikodym">FundingRn</param>
    /// <param name="pVal">Quantile for distribution measure, which is not used in this function.</param>
    /// <returns>The weighted average value of booking entity exposure multiplying lend spread.</returns>
    /// <remarks>
    /// We have the following notations:
    /// <math env="align*">
    /// d &amp;-- \text{ The simulation date index.} \\\\
    /// j &amp;-- \text{ The simulation path index.} \\\\
    /// N &amp;-- \text{ The simulation path numbers.} \\\\
    /// {X_d^j}^- &amp;-- \text{ The netted and collateralised booking entity exposure at date <m>d</m> and the <m>j</m>-th path.} \\\\
    /// D_d^j &amp;-- \text{ The stochastic discount factor at date <m>d</m> and the <m>j</m>-th path.} \\\\
    /// SL_d^j &amp;-- \text{ The lend spread at date <m>d</m> and the <m>j</m>-th path.} \\\\
    /// FR_d^j &amp;-- \text{ FundingRn at date <m>d</m> and the <m>j</m>-th path, which is the Radon Nikodym Derivative for FBA calculation.} 
    /// </math>
    /// Depending on the flag discount, we have:
    /// <math env="align*">
    /// \text{Discounted FundingCost(d)} &amp;= \frac{\sum_{j=1}^N {X_d^j}^- \cdot SL_d^j \cdot D_d^j  \cdot FR_d^j }{\sum_{j=1}^N FR_d^j} \\\\
    /// \text{Undiscounted FundingCost(d)} &amp;= \frac{\sum_{j=1}^N {X_d^j}^- \cdot SL_d^j \cdot D_d^j  \cdot FR_d^j }{\sum_{j=1}^N D_d^j \cdot FR_d^j} \\\\
    /// </math>
    /// </remarks>
    protected virtual double FundingBenefit(int d, bool discount, PathWiseExposure exposure,
                                         RadonNikodymDerivative radonNikodym, double pVal)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt * rn;
        double df = p.GetDiscountFactor(d);
        double e = exposure.Compute(p, d) * df * p.GetLendSpread(d);
        retVal += w * e;
        norm += discount ? w : w * df;
      }
      return (norm <= 0.0) ? 0.0 : retVal / norm;
    }


    /// <summary>
    /// Calculate the own spread at a given date.
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative</param>
    /// <returns>The average value of the own spread.</returns>
    protected virtual double OwnSpread(int d, RadonNikodymDerivative radonNikodym)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt*rn;
        double s = p.GetOwnSpread(d);
        retVal += w*s;
        norm += w;
      }
      return (norm <= 0.0) ? 0.0 : retVal/norm;
    }


    /// <summary>
    /// Calculate the borrow spread at a given date.
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="radonNikodym">ZeroRn</param>
    /// <returns>The average value of the borrow spread at a given date.</returns>
    protected virtual double BorrowSpread(int d, RadonNikodymDerivative radonNikodym)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt * rn;
        double s = p.GetBorrowSpread(d);
        retVal += w * s;
        norm += w;
      }
      return (norm <= 0.0) ? 0.0 : retVal / norm;
    }


    /// <summary>
    /// Calculate the lend spread at a given date.
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="radonNikodym">ZeroRn</param>
    /// <returns>The average value of the lend spread at a given date.</returns>
    protected virtual double LendSpread(int d, RadonNikodymDerivative radonNikodym)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt * rn;
        double s = p.GetLendSpread(d);
        retVal += w * s;
        norm += w;
      }
      return (norm <= 0.0) ? 0.0 : retVal / norm;
    }


    /// <summary>
    /// Calculate the standard deviation of EE, NEE, DiscountedEE, DiscountedNEE.
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="discount">True if discounted.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivatives. It can be CptyRn, OwnRn, or ZeroRn.</param>
    /// <param name="pVal">Quantile for distribution measure, which is not used in this function.</param>
    /// <returns>The standard deviation of EE, NEE, DiscountedEE, or DiscountedNEE.</returns>
    /// <remarks>
    /// Depending on the discount flag and the risky party, one have:
    /// <list type="bullet">
    /// <item><description>
    /// If Risky Party = Counterparty, discount = true, this function returns the standard deviation of discounted counterpart exposure. 
    /// </description></item>
    /// <item><description>
    /// If Risky Party = Counterparty, discount = false, this function returns the standard deviation of undiscounted counterpart exposure. 
    /// </description></item>
    /// <item><description>
    /// If Risky Party = Booking entity, discount = true, this function returns the standard deviation of discounted booking entity exposure. 
    /// </description></item>
    /// <item><description>
    /// If Risky Party = Booking entity, discount = false, this function returns the standard deviation of undiscounted booking entity exposure. 
    /// </description></item>
    /// </list>
    /// </remarks>
    protected virtual double Sigma(int d, bool discount, PathWiseExposure exposure,
                                   RadonNikodymDerivative radonNikodym, double pVal)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double retVal2 = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt*rn;
        double df = p.GetDiscountFactor(d);
        double e = exposure.Compute(p, d);
        retVal += w*df*e;
        retVal2 += discount ? w*df*df*e*e : w*df*e*e;
        norm += discount ? w : df*w;
      }
      return (norm <= 0.0) ? 0.0 : Math.Sqrt(Math.Max(retVal2/norm - (retVal/norm*retVal/norm), 0.0));
    }


    /// <summary>
    /// Calculate the standard error of EE, NEE, DiscountedEE, or DiscountedNEE. The value will be expressed as a percentage of the expected value.
    /// </summary>
    /// <param name="d">Date index.</param>
    /// <param name="discount">True if discounted.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivatives. It can be CptyRn, OwnRn, or ZeroRn.</param>
    /// <param name="pVal">Quantile for distribution measure, which is not used in this function.</param>
    /// <returns>Standard error of the discounted/undiscounted, and positive/negative exposure, expressed as a percentage of the expected value.</returns>
    /// <remarks>
    /// Depending on the discount flag and the risky party, one have:
    /// <list type="bullet">
    /// <item><description>
    /// If Risky Party = Counterparty, discount = true, this function returns the standard error of discounted counterpart exposure, expressed as a percentage of DiscountedEE.
    /// <math env="align*">
    /// \text{Standard Error of DiscountedEE(t)} = \frac{\text{Standard Deviation of DiscountedEE(t)}}{DiscountedEE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </description></item>
    /// <item><description>
    /// If Risky Party = Counterparty, discount = false, this function returns the standard error of undiscounted counterpart exposure, expressed as a percentage of EE. 
    /// <math env="align*">
    /// \text{Standard Error of EE(t)} = \frac{\text{Standard Deviation of EE(t)}}{EE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </description></item>
    /// <item><description>
    /// If Risky Party = Booking entity, discount = true, this function returns the standard error of discounted booking entity exposure, expressed as a percentage of DiscountedNEE.
    /// <math env="align*">
    /// \text{Standard Error of DiscountedNEE(t)} = \frac{\text{Standard Deviation of DiscountedNEE(t)}}{DiscountedNEE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </description></item>
    /// <item><description>
    /// If Risky Party = Booking entity, discount = false, this function returns the standard error of undiscounted booking entity exposure, expressed as a percentage of NEE. 
    /// <math env="align*">
    /// \text{Standard Error of NEE(t)} = \frac{\text{Standard Deviation of NEE(t)}}{NEE(t) \cdot \sqrt{N}} 
    /// </math>
    /// </description></item>
    /// </list>
    /// </remarks>
    protected virtual double StdError(int d, bool discount, PathWiseExposure exposure,
                                   RadonNikodymDerivative radonNikodym, double pVal)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      double retVal = 0.0;
      double retVal2 = 0.0;
      double norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt * rn;
        double df = p.GetDiscountFactor(d);
        double e = exposure.Compute(p, d);
        retVal += w * df * e;
        retVal2 += discount ? w * df * df * e * e : w * df * e * e;
        norm += discount ? w : df * w;
      }
      double StdError= (norm <= 0.0) ? 0.0 : Math.Sqrt(Math.Max(retVal2 / norm - (retVal / norm * retVal / norm), 0.0));
      double total = (norm <= 0.0) ? 0.0 : (retVal / norm) * Math.Sqrt(calc.SimulatedValues.PathCount);
      StdError = (total <= 0.0) ? 0.0 : (StdError / total);
      return StdError;
    }


    /// <summary>
    /// Calculate the maximum amount of discounted/undiscounted and positive/negative exposure expected to occur on a future date with a high degree of statistical confidence.
    /// </summary>
    /// <param name="d">Date index</param>
    /// <param name="discount">True if discounted</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivatives. It can be CptyRn, OwnRn, or ZeroRn. </param>
    /// <param name="pVal">Quantile for distribution measure.</param>
    /// <returns></returns>
    protected virtual double Pfe(int d, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym,
                                 double pVal)
    {
      var calc = this as ICounterpartyCreditRiskCalculations;
      var pdf = new List<Tuple<double, double>>();
      double mass = 0.0, norm = 0.0;
      foreach (SimulatedPathValues p in calc.SimulatedValues.Paths)
      {
        double wt = p.Weight;
        double rn = radonNikodym(p, d);
        double w = wt*rn;
        if (w <= 0.0)
          continue;
        double df = p.GetDiscountFactor(d);
        double e = exposure.Compute(p, d);
        if (discount)
          e *= df;
        else
          w *= df;
        norm += w;
        if (e <= 0)
          continue;
        pdf.Add(new Tuple<double, double>(e, w));
        mass += w;
      }
      if (mass <= 0.0)
        return 0.0;
      if (mass < norm)
        pdf.Add(new Tuple<double, double>(0.0, norm - mass));
      pdf.Sort((x, y) => (x.Item1 < y.Item1) ? -1 : (x.Item1 > y.Item1) ? 1 : 0);
      var mtm = new List<double>();
      var cdf = new List<double>();
      double pv = pdf[0].Item1;
      double pm = pdf[0].Item2/norm;
      mtm.Add(pv);
      cdf.Add(pm);
      for (int i = 1; i < pdf.Count; ++i)
      {
        double pvNew = pdf[i].Item1;
        pm += pdf[i].Item2/norm;
        if (pvNew > pv)
        {
          mtm.Add(pvNew);
          cdf.Add(pm);
        }
        else
          cdf[cdf.Count - 1] = pm;
        pv = pvNew;
      }
      var distribution = new EmpiricalDistribution(mtm.ToArray(), cdf.ToArray());
      return distribution.Quantile(pVal);
    }


    /// <summary>
    /// Interpolate the value of a function at a given date.
    /// </summary>
    /// <param name="func">Function f that need to be interpolated.</param>
    /// <param name="exposureDates">An array of ordered exposure dates <m>T_0, T_1, \cdots, T_n</m></param>
    /// <param name="date">Date <m>t</m>.</param>
    /// <param name="discount">Discount flag.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivatives</param>
    /// <param name="pVal">Quantile for distribution measure.</param>
    /// <returns>The interpolated value for <m>f(t)</m>.</returns>
    /// <remarks>
    /// <para>
    /// Given an array of ordered exposure dates, <m>T_0 \lt T_1 \lt \cdots \lt T_n</m>. We want to interpolate the
    /// value of function <m>f</m> at time <m>t</m> based on its value on the exposure dates.</para>
    /// <list type="bullet">
    /// <item><description>
    /// If <m>t \leq T_0</m>, it returns <m>f(T_0)</m>. 
    /// </description></item>
    /// <item><description>
    /// If <m>t \geq T_n</m>, it returns <m>f(T_n)</m>.
    /// </description></item>
    /// <item><description>
    /// If <m>T_{i-1} \lt t \leq T_i</m>, it returns <m>\frac{t - T_{i-1}}{T_i - T_{i-1}}f(T_i) + \frac{T_{i} - t}{T_i - T_{i-1}}f(T_{i-1})</m>.
    /// </description></item>
    /// </list>
    /// </remarks>
    protected static double Interpolate(CcrFunction func, Dt[] exposureDates, Dt date, bool discount,
                                        PathWiseExposure exposure, RadonNikodymDerivative radonNikodym, double pVal)
    {
      int days;
      if (Dt.Cmp(date, exposureDates[0]) <= 0)
        return func(0, discount, exposure, radonNikodym, pVal);
      if ((days = Dt.Cmp(date, exposureDates[exposureDates.Length - 1])) >= 0)
        return (days == 0) ? func(exposureDates.Length - 1, discount, exposure, radonNikodym, pVal) : 0.0;
      {
        int offset = Array.BinarySearch(exposureDates, date);
        if (offset > 0)
          return func(offset, discount, exposure, radonNikodym, pVal);
        {
          offset = ~offset;
          double t = Dt.Diff(exposureDates[offset - 1], date);
          double dt = Dt.Diff(exposureDates[offset - 1], exposureDates[offset]);
          double p0 = func(offset - 1, discount, exposure, radonNikodym, pVal);
          double p1 = func(offset, discount, exposure, radonNikodym, pVal);
          double p = p0 + (p1 - p0) * t / dt;
          return p;
        }
      }
    }

    protected double Interpolate(CcrFunction func, Dt date, bool discount, PathWiseExposure exposure,
                                 RadonNikodymDerivative radonNikodym, double pVal)
    {
      return Interpolate(func, ExposureDates, date, discount, exposure, radonNikodym, pVal);
    }


    /// <summary>
    /// Numerical integration by trapezoid rule.
    /// </summary>
    /// <param name="func">Integrand function <m>f(t)</m>.</param>
    /// <param name="fromDt">Beginning date <m>a</m>.</param>
    /// <param name="toDt">Ending date <m>b</m>. </param>
    /// <param name="exposureDates">An array of ordered exposure dates <m>T_0, T_1, \cdots, T_n</m>.</param>
    /// <param name="discount">True if discounted.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative.</param>
    /// <param name="pVal">Quantile for distribution measure.</param>
    /// <param name="kernel">Integration Kernel <m>k(t)</m>.</param>
    /// <param name="recovery">Recovery Rate <m>R</m>.</param>
    /// <returns>Numerical Integration for <m>(1-R) \cdot \int_a^b f(t) d k(t)</m>.</returns>
    /// <remarks>
    /// <para>Denote the array of ordered exposure dates by <m>T_0 \lt T_1 \lt \cdots \lt T_n.</m></para>
    /// <para> Given two dates <m>a</m> and <m>b</m>, if <m>a \geq b</m>, it returns <m>0</m>. </para>
    /// <para>If <m>a \lt b</m>, we want to estimate the value of <m>(1-R) \cdot \int_a^b f(t) d k(t)</m> by the following steps.</para>
    /// <list type="number">
    /// <item><description>
    /// If <m>a \leq T_0</m>, reset <m>a = T_0</m>.
    /// </description></item>
    /// <item><description>
    /// If <m>b \geq T_n</m>, reset <m>b = T_n</m>.
    /// </description></item>
    /// <item><description>
    /// Now suppose <m>T_{i-1} \leq a \lt T_i \lt \cdots \lt T_{j} \lt b \leq T_{j+1}</m>, then the formula for the numerical integration is given by:
    /// <math env="align*">
    /// \int_a^b f(t) d k(t) \approx \frac{f(a) + f(T_{i+1})}{2} \cdot (k(T_{i+1}) - k(T_i)) + 
    /// \sum_{s = i+1}^{j-1} \frac{f(T_s) + f(T_{s+1})}{2} \cdot (k(T_{s+1}) - k(T_s)) + \frac{f(T_j) + f(b)}{2} \cdot (k(T_{j+1}) - k(T_j)),
    /// </math>
    /// where <m>f(a)</m> and <m>f(b)</m> are the linear interpolated value:
    /// <math env="align*">
    /// f(a) &amp; := \frac{a - T_{i-1}}{T_i - T_{i-1}} f(T_i) + \frac{T_i - a}{T_i - T_{i-1}} f(T_{i-1}) \\\\
    /// f(b) &amp; := \frac{b - T_{j}}{T_{j+1} - T_{j}} f(T_{j+1}) + \frac{T_{j+1} - b}{T_{j+1} - T_{j}} f(T_{j}) 
    /// </math>
    /// </description></item>
    /// </list>
    /// </remarks>
    protected static double Integrate(CcrFunction func, Dt fromDt, Dt toDt, Dt[] exposureDates, bool discount,
                                      PathWiseExposure exposure, RadonNikodymDerivative radonNikodym, double pVal, Tuple<Dt[], double[]> kernel,
                                      double recovery)
    {

      if (toDt <= fromDt)
        return 0.0;
      int from = Array.BinarySearch(kernel.Item1, fromDt);
      int to = (toDt >= kernel.Item1.Last()) ? kernel.Item1.Length - 1 : Array.BinarySearch(kernel.Item1, toDt);
      from = (from < 0) ? ~from : from + 1;
      to = (to < 0) ? ~to : to;
      double valNext = Interpolate(func, exposureDates, fromDt, discount, exposure, radonNikodym, pVal);
      double retVal = 0;
      for (int i = from; i < to; ++i)
      {
        var dt = kernel.Item1[i];
        var valPrev = valNext;
        valNext = Interpolate(func, exposureDates, dt, discount, exposure, radonNikodym, pVal);
        retVal += 0.5 * (1 - recovery) * (valPrev + valNext) * kernel.Item2[i];
      }
      double lastKernelValue;
      if (to >= 1)
      {
        double w1 = Dt.Diff(kernel.Item1[to - 1], toDt);
        double w2 = Dt.Diff(toDt, kernel.Item1[to]);
        lastKernelValue = w2 / (w1 + w2) * kernel.Item2[to - 1] + w1 / (w1 + w2) * kernel.Item2[to]; // Interpolate the kernel value at time toDt.
      }
      else // for the case that to = 0.
      {
        lastKernelValue = kernel.Item2[to];
      }      
      retVal += 0.5 * (1 - recovery) * (valNext + Interpolate(func, exposureDates, toDt, discount, exposure, radonNikodym, pVal)) * lastKernelValue;
      return retVal;
    }


    /// <summary>
    /// Calculate the bucketed average value.
    /// </summary>
    /// <param name="func">Integrand function <m>f(u)</m>.</param>
    /// <param name="asOf">As of date <m>T_0</m>.</param>
    /// <param name="date">Date <m>t</m>.</param>
    /// <param name="exposureDates">An array of ordered exposure dates <m>T_0, T_1, \cdots, T_n</m>.</param>
    /// <param name="discount">True if discounted.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative.</param>
    /// <param name="pVal">Quantile for distribution measure.</param>
    /// <param name="kernel">Integration Kernel <m>k(u)</m>.</param>
    /// <param name="recovery">Recovery rate <m>R</m>.</param>
    /// <returns>The bucketed average value</returns>
    /// <remarks>
    /// <para>
    /// Suppose <m>T_0 \lt T_1 \lt \cdots \lt T_n</m>, where <m>T_0</m> is the as of date, <m>T_1, \cdots, T_n</m> are 
    /// all the exposure dates. </para>
    /// <para>
    /// For any given date <m>t</m>, consider the following cases:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// If <m>t \lt T_0</m> or <m>t \gt T_n</m>, return <m>0</m>.
    /// </description></item>
    /// <item><description>
    /// If <m>T_i \leq t \lt T_{i+1}</m>, return the bucketed numerical integration for <m>(1-R) \cdot \int_{T_i}^{T_{i+1}} f(u) d k(u)</m>, i.e.
    /// <math env="align*">
    /// (1-R) \cdot \frac{f(T_i) + f(T_{i+1})}{2} \cdot (k(T_{i+1}) - k(T_i))
    /// </math>
    /// </description></item>
    /// </list>
    /// </remarks>
    protected static double BucketAverageExposure(CcrFunction func, Dt asOf, Dt date, Dt[] exposureDates, bool discount, PathWiseExposure exposure,
                                      RadonNikodymDerivative radonNikodym, double pVal, Tuple<Dt[], double[]> kernel, double recovery)
    {
      if ((date < asOf) || (date >= exposureDates.Last()))
        return 0.0;
      Dt start, end;
      if (date < exposureDates[0])
      {
        start = asOf;
        end = exposureDates[0];
      }
      else
      {
        var idx = Array.BinarySearch(exposureDates, date);
        start = (idx >= 0) ? exposureDates[idx] : exposureDates[~idx - 1];
        end = (idx >= 0) ? exposureDates[idx + 1] : exposureDates[~idx];
      }
      return Integrate(func, start, end, exposureDates, discount, exposure, radonNikodym, pVal, kernel, recovery);
    }

    protected double Integrate(CcrFunction func, bool discount, PathWiseExposure exposure,
                               RadonNikodymDerivative radonNikodym, double pVal, Tuple<Dt[], double[]> kernel,
                               double recovery)
    {
      return Integrate(func, Environment.AsOf, kernel.Item1.Last(), ExposureDates, discount, exposure, radonNikodym, pVal, kernel, recovery);
    }


    /// <summary>
    /// Numerical integration for XVA theta calculation.
    /// </summary>
    /// <param name="func">Integrand function <m>f(u)</m>.</param>
    /// <param name="discount">True if discounted.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative.</param>
    /// <param name="pVal">Quantile for distribution measure.</param>
    /// <param name="kernel">Integration kernel <m>k(u)</m></param>
    /// <param name="date">Specified future pricing date <m>T_0 + t</m></param>
    /// <param name="recovery">Recovery rate <m>R</m>.</param>
    /// <returns><para>XVA theta, which is defined as the difference between the current XVA at time <m>T_0</m>, and the XVA at the specified future pricing date <m>T_0 + t</m>, 
    /// i.e. <m>\int_{T_0}^{T_0 + t} f(u) d k(u)</m>.</para>
    /// <para>
    /// If <m>t</m> is not provided, the default value for it is <m>1</m> day.
    /// </para></returns>
    protected double IntegrateTheta(CcrFunction func, bool discount, PathWiseExposure exposure,
                               RadonNikodymDerivative radonNikodym, double pVal, Tuple<Dt[], double[]> kernel, Dt date,
                               double recovery)
    {
      if (date.IsEmpty())
        date = Dt.Add(Environment.AsOf, 1);
      return Integrate(func, Environment.AsOf, date, ExposureDates, discount, exposure, radonNikodym, pVal, kernel, recovery);
    }
    /// <summary>
    /// Integrate a grid function func over a given survival density 
    /// </summary>
    /// <param name="func">Grid function</param>
    /// <param name="from">from date</param>
    /// <param name="exposureDates">Exposure dates</param>
    /// <param name="kernel">Integration kernel</param>
    /// <param name="recovery">Recovery at default <m>R</m></param>
    /// <returns><m>(1.0 - R)\int_0^T f(t)dP(\tau\leq t)</m></returns>
    internal static double Integrate(Func<int, double> func, Dt[] exposureDates, Dt from, Tuple<Dt[], double[]> kernel, double recovery)
    {
      return Integrate((d, df, ex, rn, pVal) => func(d), from, kernel.Item1.Last(), exposureDates, false, null, null, 0.0, kernel, recovery);
    }


    /// <summary>
    /// Compute the maximum of a function seen so far.
    /// </summary>
    /// <param name="func">CCR function f(u).</param>
    /// <param name="date">Date <m>t.</m></param>
    /// <param name="discount">True if discounted.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative.</param>
    /// <param name="pVal">Quantile for distribution measure.</param>
    /// <returns>The maximum of the function <m>f</m> that 
    /// occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.</returns>
    /// <remarks>
    /// <para>
    /// Suppose <m>T_0, \cdots, T_{k-1}</m> are all the exposure dates prior to <m>t</m>, and denote <m>T_k = t</m>.</para> 
    /// This function RunningMax will return the maximum of the function <m>f</m> that 
    /// occurs at date <m>t</m> and any exposure dates prior to <m>t</m>, i.e. 
    /// <math env="align*">
    /// \text{M}_f(t) = \max_{0 \leq i \leq k} f(T_i)
    /// </math>
    /// 
    /// Alternatively, it may be defined by induction as the greater of the function <m>f</m> at that date <m>t = T_k</m>, or the <m>\text{M}_f</m> at the previous date <m>T_{k-1}</m>.
    /// <math env="align*">
    /// \text{M}_f(t) = \max \{f(T_k), \text{M}_f(T_{k-1}))\} \ \ \ \ \text{and}\ \ \ \ \ \text{M}_f(T_0) = 0.
    /// </math>
    /// </remarks>
    protected double RunningMax(CcrFunction func, Dt date, bool discount, PathWiseExposure exposure,
                                RadonNikodymDerivative radonNikodym, double pVal)
    {
      var dts = ExposureDates;
      double ee = 0;
      for (int i = 0; i < dts.Length; ++i)
      {
        if (Dt.Cmp(dts[i], date) < 0)
        {
          double e = func(i, discount, exposure, radonNikodym, pVal);
          ee = Math.Max(ee, e);
        }
        else
        {
          double e = Interpolate(func, date, discount, exposure, radonNikodym, pVal);
          ee = Math.Max(ee, e);
          break;
        }
      }
      return ee;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="func">CCR function <m>f(u)</m>.</param>
    /// <param name="date">Date <m>t.</m></param>
    /// <param name="discount">True if discounted.</param>
    /// <param name="exposure">Risky party exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative.</param>
    /// <param name="pVal">Quantile for distribution measure.</param>
    /// <returns>The time weighted average of function <m>f</m> at date <m>t</m> and any exposure dates prior to <m>t</m>.</returns>
    /// <remarks>
    /// <para>
    /// Suppose <m>T_0</m> is the as of date, and <m>T_1, \cdots, T_{k-1}</m> are all the exposure dates prior to <m>t</m>, and denote <m>T_k = t</m>.</para> 
    /// This function TimeAverage will return the time weighted average of function <m>f</m> at date <m>t</m> and any exposure dates prior to <m>t</m>, i.e.
    /// <para>
    /// <math env="align*">
    /// A_f(t) = \frac{\sum_{i=1}^k f(T_i) \Delta T_i}{T},
    /// </math>
    /// where <m>\Delta t_i = T_i - T_{i-1}</m> and <m>T = t_n - t_0</m>.
    /// </para>
    /// </remarks>
    protected double TimeAverage(CcrFunction func, Dt date, bool discount, PathWiseExposure exposure,
                                 RadonNikodymDerivative radonNikodym, double pVal)
    {
      Dt asOf = Environment.AsOf;
      Dt[] dts = ExposureDates;
      double f = func(0, discount, exposure, radonNikodym, pVal);
      double T = Dt.FractDiff(asOf, dts[0]);
      double retVal = f*T;
      for (int i = 1; i < dts.Length; ++i)
      {
        if (Dt.Cmp(dts[i], date) < 0)
        {
          double dt = Dt.FractDiff(dts[i - 1], dts[i]);
          f = func(i, discount, exposure, radonNikodym, pVal);
          retVal += f*dt;
          T += dt;
        }
        else
        {
          double dt = Dt.FractDiff(dts[i - 1], date);
          f = Interpolate(func, date, discount, exposure, radonNikodym, pVal);
          retVal += f*dt;
          T += dt;
          break;
        }
      }
      return (T <= 1e-12) ? 0.0 : retVal/T;
    }


    /// <summary>
    /// Compute the pathwise counterparty Radon Nikodym derivative at some given time.
    /// </summary>
    /// <param name="p">Simulation path.</param>
    /// <param name="d">Date index.</param>
    /// <returns>Bilateral or unilateral counterparty Radon Nikodym derivative CptyRn.</returns>
    /// <remarks>
    /// CptyRn represents the Radon Nikodym derivatives condition
    ///  on event of the counter party defaulting at date index.
    ///  <math env="align*">
    ///  \text{Unilateral  CptyRn} &amp; = \frac{dP(\cdot|\tau_c = t)}{dP(\cdot)}. \\\\
    ///  \text{Bilateral  CptyRn} &amp; = \frac{dP(\cdot|\tau_o > t, \tau_c = t)}{dP(\cdot)}.
    ///  </math>
    /// </remarks>
    protected double CptyRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d)*p.GetRadonNikodymCpty(d);
    }


    /// <summary>
    /// Compute the pathwise booking entity Radon Nikodym derivative at some given time <m>t</m>.
    /// </summary>
    /// <param name="p">Simulation path.</param>
    /// <param name="d">Date index.</param>
    /// <returns>Bilateral or unilateral booking entity Radon Nikodym derivative OwnRn.</returns>
    /// <remarks>
    /// OwnRn represents the Radon Nikodym derivatives condition
    ///  on event of the booking entity defaulting at date <m>t</m>.
    ///  <math env="align*">
    ///  \text{Unilateral  OwnRn} &amp; = \frac{dP(\cdot|\tau_o = t)}{dP(\cdot)}. \\\\
    ///  \text{Bilateral  OwnRn} &amp; = \frac{dP(\cdot|\tau_c > t, \tau_o = t)}{dP(\cdot)}.
    ///  </math>
    /// </remarks>
    protected double OwnRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d)*p.GetRadonNikodymOwn(d);
    }


    /// <summary>
    /// The Radon Nikodym derivative without considering WWR.
    /// </summary>
    /// <param name="p">Simulation path</param>
    /// <param name="d">Date index</param>
    /// <returns>ZeroRn = 1.</returns>
    protected double ZeroRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d);
    }


    /// <summary>
    /// Compute the pathwise funding Radon Nikodym derivative at some given time <m>t</m>.
    /// </summary>
    /// <param name="p">Simulation path.</param>
    /// <param name="d">Date index.</param>
    /// <returns>Bilateral or unilateral funding Radon Nikodym derivative FundingRn.</returns>
    /// <remarks>
    /// FundingRn represents the Radon Nikodym derivatives condition
    ///  on event of the booking entity survival at date <m>t</m>.
    ///  <math env="align*">
    ///  \text{Unilateral  FundingRn} &amp; = \frac{dP(\cdot|\tau_o > t)}{dP(\cdot)}. \\\\
    ///  \text{Bilateral  FundingRn} &amp; = \frac{dP(\cdot|\tau_c > t, \tau_o > t)}{dP(\cdot)}.
    ///  </math>
    /// </remarks>
    protected double FundingRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d)*p.GetRadonNikodymSurvival(d);
    }


    /// <summary>
    /// Compute the effective maturity.
    /// </summary>
    /// <param name="pv">The CCR function pv.</param>
    /// <param name="exposure">Counterparty exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym derivative.</param>
    /// <returns>Effective Maturity under IMM Approach</returns>
    /// <remarks>
    /// <para>
    /// The Effective Maturity under IMM Approach is defined as the time-weighted Discounted Expected Exposure 
    /// (calculations start at one year point and go out until Maturity) divided by the time-weighted Effective 
    /// DiscountedEE (calculations start at the reporting date and go to the shorter of residual residual maturity
    /// and one year.)
    /// </para>
    /// <para>
    /// Suppose <m>T_0 \lt T_1 \lt \cdots \lt T_n</m>, where <m>T_0</m> is the as of date, <m>T_1, \cdots, T_{n-1}</m> are 
    /// all the exposure dates prior to <m>t</m>, and <m>T_n</m> is the maturity date. Then Effective Maturity is 
    /// obtained by the following formula:
    /// <math env="align*">
    /// Effective Maturity = 1 + \frac{\sum_{T_k \geq 1 \text{year}}^{maturity} pv(T_k) \Delta T_k}{\sum_{k=1}^{T_k \leq 1 \text{year}} M_{pv}(T_k) \Delta T_k},
    /// </math>
    /// where <m>\Delta T_k = T_k - T_{k-1}</m>, and the <m>M_{pv}(t)</m> is the maximum value of <m>pv</m> that 
    /// occurs at date <m>t</m> and any exposure dates prior to <m>t</m>.
    /// </para>
    /// </remarks>
    protected double EffectiveMaturity(CcrFunction pv, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym)
    {
      var asOf = Environment.AsOf;
      var lastExposureDate = ExposureDates[ExposureDates.Length - 1];
      var oneYearOut = Dt.Add(asOf, new Tenor(1, TimeUnit.Years));
      double numerator = TimeAverage(pv, lastExposureDate, true, exposure, radonNikodym, 0.0)*
                         Dt.Years(asOf, lastExposureDate, DayCount.Actual365Fixed) -
                         TimeAverage(pv, oneYearOut, true, exposure, radonNikodym, 0.0);
      double denominator = TimeAverage((d, df, ex, rn, pVal) => RunningMax(pv, ExposureDates[d], df, ex, rn, pVal),
                                       oneYearOut, true, exposure, radonNikodym, 0.0);
      if (Math.Abs(numerator) < 1e-12 && Math.Abs(denominator) < 1e-12)
        return 1.0;
      if (denominator <= 0.0)
        return 5.0;
      return 1.0 + numerator/denominator;
    }


    /// <summary>
    /// Compute the Risk Weighted Assets (RWA) under the Advanced Internal Ratings Based Approach
    /// </summary>
    /// <param name="pv">The CCr function pv.</param>
    /// <param name="exposure">Counterparty exposure.</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative.</param>
    /// <returns>The Risk Weighted Assets (RWA)</returns>
    /// <remarks>
    /// <para>
    /// Under the Advanced Internal Ratings Based Approach, the Risk Weighted Assets (RWA) is calculated for each
    /// transaction or netting set using the following formula:
    /// <math env="align*">
    /// RWA = \text{OutstandingEAD} \cdot 12.5 \cdot K,
    /// </math>
    /// where <m>OutstandingEAD = EAD - CVA</m>, therefore computation of the CVA should be precede the computation
    /// of the default risk charge. 
    /// </para>
    /// <para>
    /// According to Internal Model Method (IMM), the Exposure at Default (EAD) is EEPE multiplied by factor <m>\alpha</m>
    /// to compensate for inaccuracies in the model and adjust for a "bad state" of the economy. This factor <m>\alpha</m>
    /// is equal to <m>1.4</m> now and is subject to change from regulator, it also floors at <m>1.2</m>.
    /// <math env="align*">
    /// EAD_{IMM} = \max (EEPE, EEPE_{stressed}) \cdot \alpha
    /// </math>
    /// </para>
    /// <para>
    /// The capital requirement K is obtained by the following formula:
    /// <math env="align*">
    /// K = (1-R_c) \cdot \left ( N\left [\frac{N^{-1}(PD)}{\sqrt{1-R}} + \sqrt{\frac{R}{1-R}} \cdot N^{-1}(0.999) \right ]- PD \right ) \cdot \frac{1 + (M - 2.5) \cdot b}{1 - 1.5 \cdot b},
    /// </math>
    /// where <m>R</m> is the correlation factor based on default probability <m>PD</m>:
    /// <math env="align*">
    /// R = 0.12 \times (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) + 0.24 \times \left [1 - (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) \right ]
    /// </math>
    /// and <m>b</m> is a maturity adjustment factor based on PD:
    /// <math env="align*">
    /// b = (0.11852 - 0.05478 \times \ln (PD))^2
    /// </math>
    /// and <m>M</m> is effective maturity.
    /// </para>
    /// </remarks>
    protected double RiskWeightedAssets(CcrFunction pv, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym)
    {
      var oneYearOut = Dt.Add(Environment.AsOf, new Tenor(1, TimeUnit.Years));
      var k = CapitalRequirement(pv, exposure, radonNikodym);
      var eepe = TimeAverage((d, df, ex, rn, pVal) => RunningMax(pv, ExposureDates[d], df, ex, rn, pVal),
                             oneYearOut, false, exposure, radonNikodym, 0.0);
      const double alpha = 1.4;
      var ead = alpha * eepe;
      return ead * 12.5 * k;
    }

    /// <summary>
    /// Compute the Capital Requirement K
    /// </summary>
    /// <param name="pv">The CCR function pv</param>
    /// <param name="exposure">Counterparty exposure</param>
    /// <param name="radonNikodym">Radon Nikodym Derivative</param>
    /// <returns>Capital Requirement</returns>
    /// <remarks>
    /// <para>
    /// The capital requirement K is obtained by the following formula:
    /// <math env="align*">
    /// K = (1-R_c) \cdot \left ( N\left [\frac{N^{-1}(PD)}{\sqrt{1-R}} + \sqrt{\frac{R}{1-R}} \cdot N^{-1}(0.999) \right ]- PD \right ) \cdot \frac{1 + (M - 2.5) \cdot b}{1 - 1.5 \cdot b},
    /// </math>
    /// where <m>R</m> is the correlation factor based on default probability <m>PD</m>:
    /// <math env="align*">
    /// R = 0.12 \times (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) + 0.24 \times \left [1 - (\frac{1 - e^{-50 \cdot PD}}{1 - e^{-50}}) \right ]
    /// </math>
    /// and <m>b</m> is a maturity adjustment factor based on PD:
    /// <math env="align*">
    /// b = (0.11852 - 0.05478 \times \ln (PD))^2
    /// </math>
    /// and <m>M</m> is effective maturity.
    /// </para>
    /// </remarks>
    protected double CapitalRequirement(CcrFunction pv, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym)
    {
      var cptyCurve = Environment.CptyCurve(0);
      var oneYearOut = Dt.Add(Environment.AsOf, new Tenor(1, TimeUnit.Years));
      var defaultProb = (cptyCurve != null) ? 1.0 - cptyCurve.Interpolate(oneYearOut) : 0.0;

      var expRatio = (1 - Math.Exp(-50.0 * defaultProb)) / (1 - Math.Exp(-50.0));
      var r = 0.12 * expRatio + 0.24 * (1 - expRatio);

      var b = (0.11852 - 0.05478 * Math.Log(defaultProb));
      b *= b;
      var lgd = 1.0 - Environment.CptyRecovery(0);
      var effectiveMaturity = EffectiveMaturity(pv, exposure, radonNikodym);
      effectiveMaturity = Math.Min(effectiveMaturity, 5.0);
      var k = lgd * (
                Normal.cumulative(
                (Normal.inverseCumulative(defaultProb, 0.0, 1.0) +
                 Math.Sqrt(r) * Normal.inverseCumulative(0.999, 0.0, 1.0)) / Math.Sqrt(1 - r), 0.0, 1.0) - defaultProb) *
              (1 + (effectiveMaturity - 2.5) * b) / (1.0 - 1.5 * b);
      return k;
    }
    #endregion

    #region ICounterpartyCreditRiskCalculations

        /// <summary>
        /// Reset stored paths. Next time the SimulatedValues property is accessed the calculations are performed
        /// </summary>
        void ICounterpartyCreditRiskCalculations.Reset()
    {
      SimulatedValues = null;
    }

    /// <summary>
    /// Perform calculations
    /// </summary>
    void ICounterpartyCreditRiskCalculations.Execute()
    {
      Simulate();
    }

    /// <summary>
    /// Get stored simulation paths
    /// </summary>
    ISimulatedValues ICounterpartyCreditRiskCalculations.SimulatedValues
    {
      get { return SimulatedValues; }
    }

    /// <summary>
    /// Get unilateral flag
    /// </summary>
    public bool IsUnilateral
    {
        get { return _isUnilateral; }
    }

    /// <summary>
    /// Display CCR measure
    /// </summary>
    /// <param name="measure">Measure enum constant</param>
    /// <param name="netting">Netting rule</param>
    /// <param name="date">Future date (required only for time-bucketed measures)</param>
    /// <param name="alpha">Confidence level (required only for tail measures)</param>
    /// <returns>CCRMeasure</returns>
    double ICounterpartyCreditRiskCalculations.GetMeasure(CCRMeasure measure, Netting netting, Dt date, double alpha)
    {
      return GetMeasureImpl(measure, netting, date, alpha);
    }


    /// <summary>
    /// Compute CCR measure
    /// </summary>
    /// <param name="measure">Measure enum constant</param>
    /// <param name="netting">Netting rule</param>
    /// <param name="date">Future date (required only for time-bucketed measures)</param>
    /// <param name="alpha">Confidence level (required only for tail measures)</param>
    /// <returns>CCRMeasure</returns>
    protected double GetMeasureImpl(CCRMeasure measure, Netting netting, Dt date, double alpha)
    {
      Dictionary<string, int> map = Portfolio.Map;
      PathWiseExposure exposure = null;
      if (SimulatedValues == null)
        throw new ArgumentException("Must Execute before calculating risk measures");
      double retVal = 0.0;
      switch (measure)
      {
        case CCRMeasure.CVA:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal =
              -Integrate(Pv, true, exposure, CptyRn, alpha, DefaultKernel[0], Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.DVA:
          if (DefaultKernel.Length >= 2)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal = Integrate(Pv, true, exposure, OwnRn, alpha, DefaultKernel[1], Environment.CptyRecovery(1));
          }
          break;
        case CCRMeasure.CVA0:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal =
              -Integrate(Pv, true, exposure, ZeroRn, alpha, DefaultKernel[0], Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.DVA0:
          if (DefaultKernel.Length >= 2)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal = Integrate(Pv, true, exposure, ZeroRn, alpha, DefaultKernel[1], Environment.CptyRecovery(1));
          }
          break;
        case CCRMeasure.FCA:
          {
            if (SurvivalKernel != null)
            {
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
              retVal = -Integrate(FundingCost, true, exposure, FundingRn, alpha, SurvivalKernel, 0.0);
            }
          }
          break;
        case CCRMeasure.FCA0:
          {
            if (SurvivalKernel != null)
            {
              //if exposure is independent of counterparties E(S_t X^+_t | \tau_B > t, \tau_C > t) = E(S_t | tau_B > t, \tau_C > t) * E(X^+_t)
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
              retVal =
                -Integrate((d, df, ex, rn, pVal) => BorrowSpread(d, rn) * Pv(d, df, ex, rn, pVal), true, exposure,
                           ZeroRn, alpha, SurvivalKernel, 0.0);
            }
          }
          break;
        case CCRMeasure.FCANoDefault:    
          {
            if (NoDefaultKernel != null)
            {
              //if no default for both parties 
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
              retVal =
                -Integrate((d, df, ex, rn, pVal) => BorrowSpread(d, rn) * Pv(d, df, ex, rn, pVal), true, exposure,
                           ZeroRn, alpha, NoDefaultKernel, 0.0);
            }
          }
          break;
        case CCRMeasure.FBA:
          {
            if (SurvivalKernel != null)
            {
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
              retVal = Integrate(FundingBenefit, true, exposure, FundingRn, alpha, SurvivalKernel, 0.0);
            }
          }
          break;
        case CCRMeasure.FBA0:
          {
            if (SurvivalKernel != null)
            {
              //if exposure is independent of counterparties E(S_t X^+_t | \tau_B > t, \tau_C > t) = E(S_t | tau_B > t, \tau_C > t) * E(X^+_t)
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
              retVal =
                Integrate((d, df, ex, rn, pVal) => LendSpread(d, rn) * Pv(d, df, ex, rn, pVal), true, exposure,
                           ZeroRn, alpha, SurvivalKernel, 0.0);
            }
          }
          break;
        case CCRMeasure.FBANoDefault:    
          {
            if (NoDefaultKernel != null)
            {
              //if no default for both parties 
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
              retVal =
                Integrate((d, df, ex, rn, pVal) => LendSpread(d, rn) * Pv(d, df, ex, rn, pVal), true, exposure,
                           ZeroRn, alpha, NoDefaultKernel, 0.0);
            }
          }
          break;
        case CCRMeasure.FVA:
          {
            retVal = GetMeasureImpl(CCRMeasure.FCA, netting, Dt.Empty, 0.0) + GetMeasureImpl(CCRMeasure.FBA, netting, Dt.Empty, 0.0);
          }
          break;
        case CCRMeasure.FVA0:
          {
            retVal = GetMeasureImpl(CCRMeasure.FCA0, netting, Dt.Empty, 0.0) + GetMeasureImpl(CCRMeasure.FBA0, netting, Dt.Empty, 0.0);
          }
          break;
        case CCRMeasure.FVANoDefault:
          {
            retVal = GetMeasureImpl(CCRMeasure.FCANoDefault, netting, Dt.Empty, 0.0) + GetMeasureImpl(CCRMeasure.FBANoDefault, netting, Dt.Empty, 0.0);
          }
          break;
        case CCRMeasure.CVATheta:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal =
              IntegrateTheta(Pv, true, exposure, CptyRn, alpha, DefaultKernel[0], date, Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.DVATheta:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal =
              -IntegrateTheta(Pv, true, exposure, OwnRn, alpha, DefaultKernel[1], date, Environment.CptyRecovery(1));  
          }
          break;
        case CCRMeasure.FCATheta:
          {
            if (SurvivalKernel != null)
            {
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
              retVal = IntegrateTheta(FundingCost, true, exposure, FundingRn, alpha, SurvivalKernel,date, 0.0);
            }
          }
          break;
        case CCRMeasure.FBATheta:
          {
            if (SurvivalKernel != null)
            {
              exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
              retVal = -IntegrateTheta(FundingBenefit, true, exposure, FundingRn, alpha, SurvivalKernel, date, 0.0);
            }
          }
          break;
        case CCRMeasure.FVATheta:
        {
          retVal = GetMeasureImpl(CCRMeasure.FCATheta, netting, Dt.Empty, 0.0) + GetMeasureImpl(CCRMeasure.FBATheta, netting, Dt.Empty, 0.0); 
        }
          break;
        case CCRMeasure.CptyRn:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(RnDensity, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.OwnRn:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(RnDensity, date, false, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.FundingRn:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(RnDensity, date, false, exposure, FundingRn, alpha);
          break;
        case CCRMeasure.ZeroRn:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(RnDensity, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.EC:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal = -Integrate(
              (d, df, ex, rn, pVal) => Pfe(d, df, ex, rn, pVal) - Pv(d, df, ex, rn, pVal), true, exposure,
              CptyRn, alpha,
              DefaultKernel[0], Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.EC0:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal = -Integrate(
              (d, df, ex, rn, pVal) => Pfe(d, df, ex, rn, pVal) - Pv(d, df, ex, rn, pVal), true, exposure,
              ZeroRn, alpha,
              DefaultKernel[0], Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.DiscountedEPV:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(Pv, date, true, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.EPV:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(Pv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.EE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pv, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.CE:
        case CCRMeasure.EE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.DiscountedEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pv, date, true, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.DiscountedCE:
        case CCRMeasure.DiscountedEE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pv, date, true, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.NEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Pv, date, false, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.DiscountedNEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Pv, date, true, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.NEE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Pv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.DiscountedNEE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Pv, date, true, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.PFE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pfe, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.DiscountedPFE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pfe, date, true, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.PFE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pfe, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.DiscountedPFE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Pfe, date, true, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.PFCSA:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          var collateralizedPFE = Interpolate(Pfe, date, false, exposure, CptyRn, alpha);
          exposure = new PathWiseExposure(ExposureDates, map, new Netting(netting.NettingGroups, netting.NettingSuperGroups, null), PathWiseExposure.RiskyParty.Counterparty);
          var uncollateralizedPFE = Interpolate(Pfe, date, false, exposure, CptyRn, alpha);
          retVal = uncollateralizedPFE - collateralizedPFE;
          break;
        case CCRMeasure.PFNE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Pfe, date, false, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.DiscountedPFNE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Pfe, date, true, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.PFNCSA:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          var collateralizedPFNE = Interpolate(Pfe, date, false, exposure, OwnRn, alpha);
          exposure = new PathWiseExposure(ExposureDates, map, new Netting(netting.NettingGroups, netting.NettingSuperGroups, null), PathWiseExposure.RiskyParty.BookingEntity);
          var uncollateralizedPFNE = Interpolate(Pfe, date, false, exposure, OwnRn, alpha);
          retVal = uncollateralizedPFNE - collateralizedPFNE;
          break;
        case CCRMeasure.Sigma:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Sigma, date, true, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.SigmaDiscountedEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Sigma, date, true, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.SigmaEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(Sigma, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.SigmaDiscountedNEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Sigma, date, true, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.SigmaNEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(Sigma, date, false, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.StdErrDiscountedEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(StdError, date, true, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.StdErrDiscountedNEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(StdError, date, true, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.StdErrEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(StdError, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.StdErrNEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(StdError, date, false, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.EEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = RunningMax(Pv, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.EEE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = RunningMax(Pv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.EPE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = TimeAverage(Pv, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.EPE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = TimeAverage(Pv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.ENE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = TimeAverage(Pv, date, false, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.ENE0:
         exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
         retVal = TimeAverage(Pv, date, false, exposure, ZeroRn, alpha);
         break;
        case CCRMeasure.EEPE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = TimeAverage((d, df, ex, rn, pVal) => RunningMax(Pv, ExposureDates[d], df, ex, rn, pVal),
                               date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.EEPE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = TimeAverage((d, df, ex, rn, pVal) => RunningMax(Pv, ExposureDates[d], df, ex, rn, pVal),
                                         date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.MPFE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = RunningMax(Pfe, ExposureDates.Last(), false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.EffectiveMaturity:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = EffectiveMaturity(Pv, exposure, CptyRn);
          break;
        case CCRMeasure.EffectiveMaturity0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = EffectiveMaturity(Pv, exposure, ZeroRn);
          break;
        case CCRMeasure.EAD:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = TimeAverage((d, df, ex, rn, pVal) => RunningMax(Pv, ExposureDates[d], df, ex, rn, pVal),
                                Dt.Add(Environment.AsOf, new Tenor(1, TimeUnit.Years)), false, exposure, CptyRn, alpha);
          retVal *= 1.4;
          break;
          case CCRMeasure.EAD0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);          
          retVal = TimeAverage((d, df, ex, rn, pVal) => RunningMax(Pv, ExposureDates[d], df, ex, rn, pVal),
                               Dt.Add(Environment.AsOf, new Tenor(1, TimeUnit.Years)), false, exposure, ZeroRn, alpha);
          retVal *= 1.4;
          break;
        case CCRMeasure.RWA:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = RiskWeightedAssets(Pv, exposure, CptyRn);
          break;
        case CCRMeasure.RWA0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = RiskWeightedAssets(Pv, exposure, ZeroRn);
          break;
        case CCRMeasure.CapitalRequirement:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = CapitalRequirement(Pv, exposure, CptyRn);
          break;
        case CCRMeasure.CapitalRequirement0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = CapitalRequirement(Pv, exposure, ZeroRn);
          break;
        case CCRMeasure.BucketedCVA:
          if (DefaultKernel.Length >= 0)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal = -BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, CptyRn, alpha, DefaultKernel[0],
                                           Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.BucketedCVA0:
          if (DefaultKernel.Length >= 0)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal = -BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, ZeroRn, alpha, DefaultKernel[0],
                                           Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.BucketedDVA:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal = BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, OwnRn, alpha, DefaultKernel[1],
                                           Environment.CptyRecovery(1));
          }
          break;
        case CCRMeasure.BucketedDVA0:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal = BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, ZeroRn, alpha, DefaultKernel[1],
                                           Environment.CptyRecovery(1));
          }
          break;
        default:
          throw new NotSupportedException(String.Format("Measure {0} not implemented", measure));
      }
      return retVal;
    }

    #endregion
  }
}