using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// CCR calculations by semi-analytic Markov projection methods
  /// </summary>
  [Serializable]
  internal class ProjectiveCCRCalculations : CCRCalculations
  {
    #region Data
    private const int MaxIter = 100;
    private SimulatorFlags Flags { get; set; }
    #endregion

    #region Constructor

    /// <summary>
    ///Constructor 
    /// </summary>
    /// <param name="quadraturePoints">Number of quadrature points</param>
    /// <param name="exposureDates">Exposure dates</param>
    /// <param name="flags">Simulator flags</param>
    /// <param name="environment">Market environment</param>
    /// <param name="volatilities">Libor rate valatilities</param>
    /// <param name="factorLoadings">Libor rate factor loadings</param>
    /// <param name="cptyDefaultTimeCorrelation">Default time correlation between default time of the counterparty and booking entity</param>
    /// <param name="portfolio">Portfolio data</param>
    ///<param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    internal ProjectiveCCRCalculations(
      int quadraturePoints,
      Dt[] exposureDates,
      SimulatorFlags flags,
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      PortfolioData portfolio,
      double cptyDefaultTimeCorrelation, 
      bool unilateral
       )
      : base(null,
        quadraturePoints, exposureDates, environment, volatilities, factorLoadings, MultiStreamRng.Type.Projective, portfolio, cptyDefaultTimeCorrelation, unilateral)
    {
      Flags = flags;
    }

    #endregion

    #region PfeObjectiveFunction

    private class PfeObjectiveFunction : SolverFn
    {
      internal double Alpha;
      internal ISimulatedValues Calcs;
      internal bool Discount;
      internal PathWiseExposure Exposure;
      internal RadonNikodymDerivative Rn;
      internal int Time;

      public override double evaluate(double x)
      {
        double retVal = 0.0;
        double norm = 0.0;
        foreach (SimulatedPathValues p in Calcs.Paths)
        {
          double wt = p.Weight;
          double w = wt*Rn(p, Time);
          double e = Exposure.Compute(p, Time);
          double df = p.GetDiscountFactor(Time);
          if (Discount)
            e *= df;
          else
            w *= df;
          norm += w;
          retVal += w*(1.0 - Normal.cumulative(e, x, Alpha));
        }
        return retVal/norm;
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Create projective simulator
    /// </summary>
    /// <returns>Projective simulator</returns>
    internal override Simulator CreateSimulator()
    {
      var simulDates = Simulations.GenerateSimulationDates(Environment.AsOf, ExposureDates, Environment.Tenors,
                                                           Environment.GridSize);
      return Simulations.CreateProjectiveSimulator(SampleSize, simulDates, Flags, Environment, Volatilities, FactorLoadings, CptyDefaultTimeCorrelation);
    }

    /// <summary>
    /// Create multi-stream random number generator
    /// </summary>
    /// <param name="engine">simulator</param>
    /// <returns></returns>
    internal override MultiStreamRng CreateRng(Simulator engine)
    {
      return MultiStreamRng.Create(engine.QuadRule, engine.Dimension, engine.SimulationTimeGrid);
    }

    /// <summary>
    /// Perform calculations
    /// </summary>
    protected override void Simulate()
    {
      using (var engine = CreateSimulator())
      {
        var rng = CreateRng(engine);
        SimulatedValues = Simulations.CalculateExposures(ExposureDates, engine, rng, Environment, Portfolio, IsUnilateral);

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

      }
    }


    protected override double Pfe(int d, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym,
                                  double pVal)
    {
      double sigma = Sigma(d, discount, exposure, radonNikodym, pVal);
      double mean = Pv(d, discount, exposure, radonNikodym, pVal);
      if (sigma < 1e-3)
        return mean;
      var calculations = this as ICounterpartyCreditRiskCalculations;
      var objectiveFunction = new PfeObjectiveFunction
                                {
                                  Time = d,
                                  Discount = discount,
                                  Alpha = sigma*1e-5,
                                  Calcs = calculations.SimulatedValues,
                                  Exposure = exposure,
                                  Rn = radonNikodym
                                };
      bool bracket = false;
      for (int i = 0; i < MaxIter; ++i)
      {
        if (objectiveFunction.evaluate(0.0) <= pVal)
        {
          bracket = true;
          break;
        }
        objectiveFunction.Alpha *= 0.5;
      }
      if (!bracket)
        return 0.0;
      var solver = new Brent2();
      solver.setLowerBounds(0.0);
      solver.setLowerBracket(0.0);
      solver.setUpperBracket(mean + 12*sigma);
      return solver.solve(objectiveFunction, pVal);
    }

    #endregion
  }
}