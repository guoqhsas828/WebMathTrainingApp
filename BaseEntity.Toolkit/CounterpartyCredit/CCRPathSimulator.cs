using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Pathwise CCR simulator
  /// </summary>
  internal class CCRPathSimulator : IRunSimulationPath
  {
    #region Data

    private readonly double[][][] allocatedEEWeights_;
    private readonly double[][][] allocatedNEEWeights_;
    private readonly double[][][] allocatedEPVWeights_;
    private readonly double[] bookingEntityWeightTotal_;
    private readonly double[] counterpartyWeightTotal_;
    private readonly double[] epvWeightTotal_;
    private readonly int[][] reversePortfolioMap_;
    private CCRMeasureAccumulator _calculator;
    private bool reduced_;
    private SimulatedPathValues[] paths_;
    private readonly bool _isUnilateral;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="exposureDates">Exposure dates (can be different from simulation dates)</param>
    /// <param name="simulator">Simulator</param>
    /// <param name="marketEnvironment">Environment to simulate</param>
    /// <param name="volatilities">Volatilities of underlying libor rates</param>
    /// <param name="factorLoadings">Factor loadings of underlying libor rates</param>
    /// <param name="portfolioData">Portfolio</param>
    /// <param name="netting">Netting data</param>
    /// <param name="rng">Multistream random number generator</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    public CCRPathSimulator(
      Dt[] exposureDates,
      Simulator simulator,
      CCRMarketEnvironment marketEnvironment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      PortfolioData portfolioData,
      Netting netting,
      MultiStreamRng rng, 
      bool unilateral = false
      )
    {
      ExposureDates = exposureDates;
      ExposureDatesIndex = Array.ConvertAll(exposureDates, dt => Array.IndexOf(simulator.SimulationDates, dt));
      OriginalMarketData = marketEnvironment;
      OriginalPortfolioData = portfolioData;
      Volatilities = volatilities;
      FactorLoadings = factorLoadings;
      var env = CloneUtil.CloneObjectGraph(marketEnvironment, portfolioData);
      Simulator = simulator;
      MarketData = env.Item1;
      PortfolioData = env.Item2;
      _isUnilateral = unilateral;
      Rng = rng;
      IntegrationKernels = new List<Tuple<Dt[], double[]>>();
      if (OriginalMarketData.CptyIndex.Length >= 1)
      {
        var krn = new double[Simulator.SimulationDates.Length];
        Simulator.DefaultKernel(unilateral ? 3 : 0, krn);
        IntegrationKernels.Add(new Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
      }
      if (OriginalMarketData.CptyIndex.Length >= 2)
      {
        var krn = new double[Simulator.SimulationDates.Length];
        Simulator.DefaultKernel(unilateral ? 4 : 1, krn);
        IntegrationKernels.Add(new Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
      }
      if (OriginalMarketData.CptyIndex.Length >= 1)
      {
        var skrn = new double[Simulator.SimulationDates.Length];
        if (OriginalMarketData.CptyIndex.Length >= 2)
          Simulator.SurvivalKernel(unilateral ? 1 : 2, skrn);
        else
          Simulator.SurvivalKernel(unilateral ? 3 : 4, skrn);
        IntegrationKernels.Add(new Tuple<Dt[], double[]>(Simulator.SimulationDates, skrn));
      }
      var pretime = MarketData.AsOf;
      var krn0 = new double[Simulator.SimulationDates.Length];
      int t = 0;
      for (; t < Simulator.SimulationDates.Length; ++t)
      {
        krn0[t] = (Simulator.SimulationDates[t] - pretime) / 365.0;
        pretime = Simulator.SimulationDates[t];
      }
      var noDefaultKernel = new Tuple<Dt[], double[]>(Simulator.SimulationDates, krn0);
      IntegrationKernels.Add(noDefaultKernel);

      MarketData.Conform();
      CounterpartyExposure = new PathWiseExposure(ExposureDates, PortfolioData.Map, netting,
                                                  PathWiseExposure.RiskyParty.Counterparty);
      BookingEntityExposure = new PathWiseExposure(ExposureDates, PortfolioData.Map, netting,
                                                   PathWiseExposure.RiskyParty.BookingEntity);
      // count trades in each netting group
      var tradeCounts = new int[NettingMap.Count];
      foreach (var tuple in PortfolioData.Portfolio)
        tradeCounts[tuple.Item3]++;
      foreach (var tuple in PortfolioData.Exotics)
        tradeCounts[tuple.Item3]++;
      // initialize jagged arrays
      reversePortfolioMap_ = new int[NettingMap.Count][];
      allocatedEEWeights_ = new double[DateCount][][];
      allocatedNEEWeights_ = new double[DateCount][][];
      allocatedEPVWeights_ = new double[DateCount][][];
      for (int dtIdx = 0; dtIdx < DateCount; ++dtIdx)
      {
        allocatedEEWeights_[dtIdx] = new double[NettingMap.Count][];
        allocatedNEEWeights_[dtIdx] = new double[NettingMap.Count][];
        allocatedEPVWeights_[dtIdx] = new double[NettingMap.Count][];
      }
      for (int nettingIdx = 0; nettingIdx < NettingMap.Count; ++nettingIdx)
      {
        reversePortfolioMap_[nettingIdx] = new int[tradeCounts[nettingIdx]];
        for (int dtIdx = 0; dtIdx < DateCount; dtIdx++)
        {
          allocatedEEWeights_[dtIdx][nettingIdx] = new double[tradeCounts[nettingIdx]];
          allocatedNEEWeights_[dtIdx][nettingIdx] = new double[tradeCounts[nettingIdx]];
          allocatedEPVWeights_[dtIdx][nettingIdx] = new double[tradeCounts[nettingIdx]];
        }
        tradeCounts[nettingIdx] = 0;
      }
      t = 0;
      for (; t < PortfolioData.Portfolio.Length; ++t)
      {
        var tuple = PortfolioData.Portfolio[t];
        reversePortfolioMap_[tuple.Item3][tradeCounts[tuple.Item3]++] = t;
      }
      for (int i = 0; i < PortfolioData.Exotics.Length; ++i, ++t)
      {
        var tuple = PortfolioData.Exotics[i];
        reversePortfolioMap_[tuple.Item3][tradeCounts[tuple.Item3]++] = t;
      }
      counterpartyWeightTotal_ = new double[DateCount];
      bookingEntityWeightTotal_ = new double[DateCount];
      epvWeightTotal_ = new double[DateCount];
      PrecalculatedPvs = new List<double[,]>();
      paths_ = new SimulatedPathValues[simulator.PathCount];
    }

    #endregion

    #region Properties

    /// <summary>
    /// Vols
    /// </summary>
    public VolatilityCollection Volatilities { get; private set; }

    /// <summary>
    /// Factor loadings
    /// </summary>
    public FactorLoadingCollection FactorLoadings { get; private set; }

    /// <summary>
    /// Delegate to compute pathwise counterparty exposure
    /// </summary>
    public PathWiseExposure CounterpartyExposure { get; private set; }

    /// <summary>
    /// Delegate to compute pathwise own exposure
    /// </summary>
    public PathWiseExposure BookingEntityExposure { get; private set; }

    /// <summary>
    /// Original market environment
    /// </summary>
    public CCRMarketEnvironment OriginalMarketData { get; private set; }

    /// <summary>
    /// EffectiveSurvival[0] = Probability that counterparty survives to T and that default time of counterparty follows default time of the booking entity  
    /// EffectiveSurvival[1] = Probability that booking entity survives to T and that default time of counterparty precedes default time of the booking entity
    /// </summary>
    public IList<Tuple<Dt[], double[]>> IntegrationKernels { get; private set; }

    /// <summary>
    /// Market environment
    /// </summary>
    public CCRMarketEnvironment MarketData { get; private set; }

    /// <summary>
    /// Portfolio
    /// </summary>
    public PortfolioData PortfolioData { get; private set; }

    /// <summary>
    /// Original portfolio
    /// </summary>
    public PortfolioData OriginalPortfolioData { get; private set; }

    /// <summary>
    /// Simulator
    /// </summary>
    public Simulator Simulator { get; private set; }

    /// <summary>
    /// Multi Stream random number generator
    /// </summary>
    public MultiStreamRng Rng { get; private set; }

    /// <summary>
    /// Precalculated exotic pvs
    /// </summary>
    public IList<double[,]> PrecalculatedPvs { get; set; }

    /// <summary>
    /// Index of exposure dates in simulation dates
    /// </summary>
    public int[] ExposureDatesIndex { get; private set; }

    /// <summary>
    /// Get unilateral flag
    /// </summary>
    public bool IsUnilateral
    {
      get { return _isUnilateral; }
    }

    #endregion

    #region Implementation of ISimulatedValues

    /// <summary>
    /// Number of exposure dates
    /// </summary>
    public int DateCount
    {
      get { return ExposureDates.Length; }
    }

    /// <summary>
    /// Exposure dates
    /// </summary>
    public Dt[] ExposureDates { private set; get; }

    /// <summary>
    ///   Number of paths.
    /// </summary>
    public int PathCount
    {
      get { return Simulator.PathCount; }
    }

    /// <summary>
    /// Netting group information
    /// </summary>
    public Dictionary<string, int> NettingMap
    {
      get { return PortfolioData.Map; }
    }

    /// <summary>
    ///   Simulated realizations.
    /// </summary>
    public IEnumerable<ISimulatedPathValues> Paths
    {
      get
      {
        return paths_;
      }
    }

    ///<summary>
    /// Type of Random Number Generator used.
    ///</summary>
    public MultiStreamRng.Type RngType
    {
      get { return Rng.RngType; }
    }

    /// <summary>
    /// Max simulation step size
    /// </summary>
    public Tenor GridSize
    {
      get { return OriginalMarketData.GridSize; }
    }

    #endregion

    #region Implementation of IRunSimulationPath

    public IList<double[,]> PrecalculateExotics()
    {
      PrecalculatedPvs =
        OriginalPortfolioData.Exotics.Select(p => Simulations.GenerateLeastSquaresGrid(OriginalMarketData, Simulator.SimulationDates, PathCount,
                                                                                       RngType, Volatilities, FactorLoadings, p, ExposureDates)).ToList();
      return PrecalculatedPvs;
    }

    
    public IList<ISimulatedPathValues> RunSimulationPaths(int from, int to)
    {
      var results = new ISimulatedPathValues[to - from];
      Parallel.For(from, to,
                   () =>
                     {
                       var clonedData = CloneUtil.ClonePreserveReferences(MarketData, PortfolioData,
                                                                          _calculator,
                                                                          allocatedEEWeights_, allocatedEPVWeights_, allocatedNEEWeights_,
                                                                          counterpartyWeightTotal_, new Tuple<double[], double[]>(bookingEntityWeightTotal_, epvWeightTotal_));
                       var threadData =
                         new Tuple
                           <CCRMarketEnvironment, PortfolioData, CCRMeasureAccumulator, double[][][], double[][][], double[][][], double[], Tuple<double[], double[], MultiStreamRng>>(clonedData.Item1, clonedData.Item2,
                                                                        clonedData.Item3,
                                                                        clonedData.Item4, clonedData.Item5,
                                                                        clonedData.Item6,
                                                                        clonedData.Item7,
                                                                        new Tuple<double[], double[], MultiStreamRng>(clonedData.Rest.Item1, clonedData.Rest.Item2,Rng.Clone()));
                       return threadData;
                     },
                   (i, threadData) =>
                     {
                       results[i - from] = paths_[i] = RunSimulationPath(i, threadData.Item1, threadData.Item2, IsUnilateral, threadData.Item3,
                                                             threadData.Item4, threadData.Item5, threadData.Item6, threadData.Item7,
                                                             threadData.Rest.Item1, threadData.Rest.Item2, threadData.Rest.Item3);
                     },
                   (threadData) =>
                     {
                       for (int t = 0; t < DateCount; ++t)
                       {
                         counterpartyWeightTotal_[t] += threadData.Item7[t];
                         bookingEntityWeightTotal_[t] += threadData.Rest.Item1[t];
                         epvWeightTotal_[t] += threadData.Rest.Item2[t];
                         for (int i = 0; i < NettingMap.Count; i++)
                         {
                           for (int j = 0; j < allocatedEEWeights_[t][i].Length; j++)
                           {
                             allocatedEEWeights_[t][i][j] += threadData.Item4[t][i][j];
                             allocatedNEEWeights_[t][i][j] += threadData.Item5[t][i][j];
                             allocatedEPVWeights_[t][i][j] += threadData.Item6[t][i][j];
                           }
                         }
                       }
                       _calculator.MergeCumulativeValues(threadData.Item3);
                     }
        );
      return results;
    }

    public void 
      AddMeasureAccumulator(CCRMeasure measure, double ci)
    {
      if (_calculator == null)
      {
        _calculator = new CCRMeasureAccumulator(OriginalMarketData.AsOf, ExposureDates,
                                                                IntegrationKernels.ToArray(),
                                                                OriginalMarketData.CptyRecoveries,
                                                                OriginalMarketData.CptyCurve(0), Simulator.PathCount);
      }
      _calculator.AddMeasureAccumulator(measure, ci);
    }

    public void Merge(IRunSimulationPath other)
    {
      var otherPathSimulator = other as CCRPathSimulator;
      if (otherPathSimulator == null)
        throw new NotSupportedException(
          "CCRPathSimulator cannot merge IRunSimulationPath of type other than CCRPathSimulator");
      for (int t = 0; t < DateCount; ++t)
      {
        counterpartyWeightTotal_[t] += otherPathSimulator.counterpartyWeightTotal_[t];
        bookingEntityWeightTotal_[t] += otherPathSimulator.bookingEntityWeightTotal_[t];
        epvWeightTotal_[t] += otherPathSimulator.epvWeightTotal_[t];
        for (int i = 0; i < NettingMap.Count; i++)
        {
          for (int j = 0; j < allocatedEEWeights_[t][i].Length; j++)
          {
            allocatedEEWeights_[t][i][j] += otherPathSimulator.allocatedEEWeights_[t][i][j];
            allocatedNEEWeights_[t][i][j] += otherPathSimulator.allocatedNEEWeights_[t][i][j];
            allocatedEPVWeights_[t][i][j] += otherPathSimulator.allocatedEPVWeights_[t][i][j];
          }
        }
      }
      _calculator.MergeCumulativeValues(otherPathSimulator._calculator);
    }

    public double[] GetMeasureAllocatedByTrade(CCRMeasure measure, int t, double ci)
    {
      if (!reduced_)
        Reduce();
      var allocatedValues = new double[PortfolioData.Portfolio.Length + PortfolioData.Exotics.Length];
      int nettingCount = NettingMap.Count;
      double[][][] allocatedWeightsByNettingSet;
      bool isIntegral = false;
      bool useCVAWeights = false;
      switch (measure)
      {
        case CCRMeasure.DVA:
        case CCRMeasure.DVATheta:
        case CCRMeasure.DVA0:
        case CCRMeasure.FBA:
        case CCRMeasure.FBATheta:
        case CCRMeasure.FBA0:
        case CCRMeasure.FBANoDefault:
          isIntegral = true;
          allocatedWeightsByNettingSet = allocatedNEEWeights_;
          break;
        case CCRMeasure.NEE:
        case CCRMeasure.NEE0:
        case CCRMeasure.ENE:
        case CCRMeasure.DiscountedNEE:
        case CCRMeasure.DiscountedNEE0:
        case CCRMeasure.PFNE:
        case CCRMeasure.MPFNE:
        case CCRMeasure.PFNCSA:
        case CCRMeasure.DiscountedPFNE:
          allocatedWeightsByNettingSet = allocatedNEEWeights_;
          break;
        case CCRMeasure.CVA:
        case CCRMeasure.CVATheta:
        case CCRMeasure.CVA0:
        case CCRMeasure.EC:
        case CCRMeasure.EC0:
        case CCRMeasure.FCA:
        case CCRMeasure.FCATheta:
        case CCRMeasure.FCA0:
        case CCRMeasure.FCANoDefault:
          isIntegral = true;
          allocatedWeightsByNettingSet = allocatedEEWeights_;
          break;
        case CCRMeasure.RWA:
        case CCRMeasure.RWA0:
          isIntegral = true;
          allocatedWeightsByNettingSet = allocatedEEWeights_;
          useCVAWeights = true;
          break;
        case CCRMeasure.EPV:
        case CCRMeasure.DiscountedEPV:
          allocatedWeightsByNettingSet = allocatedEPVWeights_;
          break;
        case CCRMeasure.FVA:
          var fca = GetMeasureAllocatedByTrade(CCRMeasure.FCA, t, ci);
          var fba = GetMeasureAllocatedByTrade(CCRMeasure.FBA, t, ci);
          var fva = new double[fca.Length];
          for (int i = 0; i < fva.Length; i++)
          {
            fva[i] = fca[i] + fba[i];
          }
          return fva;
        case CCRMeasure.FVATheta:
          var fcaTheta = GetMeasureAllocatedByTrade(CCRMeasure.FCATheta, t, ci);
          var fbaTheta = GetMeasureAllocatedByTrade(CCRMeasure.FBATheta, t, ci);
          var fvaTheta = new double[fcaTheta.Length];
          for (int i = 0; i < fvaTheta.Length; i++)
          {
            fvaTheta[i] = fcaTheta[i] + fbaTheta[i];
          }
          return fvaTheta;
        case CCRMeasure.FVA0:
          var fca0 = GetMeasureAllocatedByTrade(CCRMeasure.FCA0, t, ci);
          var fba0 = GetMeasureAllocatedByTrade(CCRMeasure.FBA0, t, ci);
          var fva0 = new double[fca0.Length];
          for (int i = 0; i < fva0.Length; i++)
          {
            fva0[i] = fca0[i] + fba0[i];
          }
          return fva0;
        case CCRMeasure.FVANoDefault:
          var fcaNd = GetMeasureAllocatedByTrade(CCRMeasure.FCANoDefault, t, ci);
          var fbaNd = GetMeasureAllocatedByTrade(CCRMeasure.FBANoDefault, t, ci);
          var fvaNd = new double[fcaNd.Length];
          for (int i = 0; i < fvaNd.Length; i++)
          {
            fvaNd[i] = fcaNd[i] + fbaNd[i];
          }
          return fbaNd;
        default:
          allocatedWeightsByNettingSet = allocatedEEWeights_;
          break;
      }

      var incrementalCalc = _calculator.HasMeasureAccumulator(measure, ci) ? _calculator : AccumulateMeasureFromPaths(measure, ci);
      if (isIntegral)
      {
        var tradeWeights = new double[ExposureDates.Length];
        for (int i = 0; i < nettingCount; i++)
        {
          for (int j = 0; j < allocatedWeightsByNettingSet[t][i].Length; j++)
          {
            for (int ti = 0; ti < tradeWeights.Length; ti++)
            {
              tradeWeights[ti] = allocatedWeightsByNettingSet[ti][i][j];
            }


            allocatedValues[reversePortfolioMap_[i][j]] =
              incrementalCalc.GetMeasure(useCVAWeights ? CCRMeasure.CVA : measure,
                                                ExposureDates[t],
                                                ci, tradeWeights);
          }
        }
        if (useCVAWeights)
        {
          var totalCVA = allocatedValues.Sum();
          var rwa = incrementalCalc.GetMeasure(measure, Dt.Empty, 0.0);
          for (int i = 0; i < allocatedValues.Length; i++)
          {
            allocatedValues[i] = (allocatedValues[i]/totalCVA)*rwa;
          }
        }
      }
      else
      {
        double total = incrementalCalc.GetMeasure(measure, ExposureDates[t], ci);

        for (int i = 0; i < nettingCount; i++)
        {
          for (int j = 0; j < allocatedWeightsByNettingSet[t][i].Length; j++)
          {
            double weight = allocatedWeightsByNettingSet[t][i][j];
            allocatedValues[reversePortfolioMap_[i][j]] = total*weight;
          }
        }
      }
      return allocatedValues;
    }

    public double GetMeasure(CCRMeasure measure, Dt dt, double ci)
    {
      if (!reduced_)
        Reduce();
      if(_calculator.HasMeasureAccumulator(measure, ci))
      {
        return _calculator.GetMeasure(measure, dt, ci);
      }
      else
      {
        var incrementalCalc = AccumulateMeasureFromPaths(measure, ci); 
        return incrementalCalc.GetMeasure(measure, dt, ci); 
      }
    }

    private SimulatedPathValues RunSimulationPath(int idx, CCRMarketEnvironment marketData, PortfolioData portfolioData, bool unilateral,
                                                   CCRMeasureAccumulator calculator,
                                                   double[][][] allocatedEEWeights, double[][][] allocatedNEEWeights,
                                                   double[][][] allocatedEPVWeights,
                                                   double[] counterpartyWeightTotal, double[] bookingEntityWeightTotal,
                                                   double[] epvWeightTotal,
                                                   MultiStreamRng rng)
    {
      SimulatedPath path = Simulator.GetSimulatedPath(idx, rng);
      if (path == null)
        return null;
      int nettingCount = NettingMap.Count;
      var resultData = new SimulatedPathValues(DateCount, nettingCount);
      for (int t = 0; t < DateCount; ++t)
      {
        double df, numeraire, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread;
        var time = ExposureDatesIndex[t];
        var dt = ExposureDates[t];

        path.Evolve(time, dt, marketData, unilateral, out numeraire, out df, out cptyRn, out ownRn, out survivalRn, out cptySpread,
                    out ownSpread, out lendSpread, out borrowSpread);
        var v = Simulations.CalculateFwdValuesByTrade(nettingCount, dt, marketData, portfolioData.Portfolio);
        v = AddExoticPvs(v, idx, t);
        resultData.Add(v.Item1, df, numeraire, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread, idx, path.Weight);
        var tradeEEByNettingSet = new double[nettingCount][];
        var tradeNEEByNettingSet = new double[nettingCount][];
        var tradeEPVByNettingSet = new double[nettingCount][];
        for (int i = 0; i < nettingCount; i++)
        {
          tradeEEByNettingSet[i] = new double[allocatedEEWeights_[t][i].Length];
          tradeNEEByNettingSet[i] = new double[allocatedEEWeights_[t][i].Length];
          tradeEPVByNettingSet[i] = new double[allocatedEEWeights_[t][i].Length];
          for (int j = 0; j < tradeEEByNettingSet[i].Length; j++)
          {
            tradeEEByNettingSet[i][j] = v.Item2[reversePortfolioMap_[i][j]];
            tradeNEEByNettingSet[i][j] = v.Item2[reversePortfolioMap_[i][j]];
            tradeEPVByNettingSet[i][j] = v.Item2[reversePortfolioMap_[i][j]];
          }
        }
        var pathEE = CounterpartyExposure.Compute(resultData, t, tradeEEByNettingSet);
        var pathNEE = BookingEntityExposure.Compute(resultData, t, tradeNEEByNettingSet);
        calculator.AccumulateExposures(resultData, t, pathEE.Item1, pathEE.Item2, pathNEE.Item1, pathNEE.Item2);
        double cptyWeight = cptyRn*resultData.GetRadonNikodym(t)*resultData.Weight;
        double ownWeight = ownRn*resultData.GetRadonNikodym(t)*resultData.Weight;
        double epvWeight = resultData.GetRadonNikodym(t) * resultData.Weight;
        counterpartyWeightTotal[t] += cptyWeight;
        bookingEntityWeightTotal[t] += ownWeight;
        epvWeightTotal[t] += epvWeight;
        for (int i = 0; i < nettingCount; i++)
        {
          for (int j = 0; j < tradeEEByNettingSet[i].Length; j++)
          {
            allocatedEEWeights[t][i][j] += tradeEEByNettingSet[i][j]*df*cptyWeight;
            allocatedNEEWeights[t][i][j] += tradeNEEByNettingSet[i][j]*df*ownWeight;
            allocatedEPVWeights[t][i][j] += tradeEPVByNettingSet[i][j] * df * epvWeight;
          }
        }
      }
      path.Dispose();
      return resultData;
    }


    private CCRMeasureAccumulator AccumulateMeasureFromPaths(CCRMeasure measure, double ci)
    {

      var incrementalCalc = new CCRMeasureAccumulator(OriginalMarketData.AsOf, ExposureDates,
                                                             IntegrationKernels.ToArray(),
                                                             OriginalMarketData.CptyRecoveries,
                                                             OriginalMarketData.CptyCurve(0), Simulator.PathCount);
      incrementalCalc.AddMeasureAccumulator(measure, ci);
      // always add CVA as it is sometimes needed even if another measure is requested
      incrementalCalc.AddMeasureAccumulator(CCRMeasure.CVA, 1.0); 

      for (int p = 0; p < paths_.Length; ++p) 
      {
        var path = paths_[p];
        if (path == null)
          return null;
        for (int t = 0; t < DateCount; ++t)
        {
          var pathEE = CounterpartyExposure.Compute(path, t, null);
          var pathNEE = BookingEntityExposure.Compute(path, t, null);
          incrementalCalc.AccumulateExposures(path, t, pathEE.Item1, pathEE.Item2, pathNEE.Item1, pathNEE.Item2);
        }
      }

      return incrementalCalc;
    }

    #endregion

    #region Private Methods

    private void 
      Reduce()
    {
      if (reduced_)
        return;
      int nettingCount = NettingMap.Count;

      for (int t = 0; t < DateCount; ++t)
      {
        double totalEE = 0.0;
        double totalNEE = 0.0;
        double totalEPV = 0.0;
        // reduce each weighted sum to expectation
        for (int i = 0; i < nettingCount; i++)
        {
          for (int j = 0; j < allocatedEEWeights_[t][i].Length; j++)
          {
            allocatedEEWeights_[t][i][j] /= counterpartyWeightTotal_[t];
            allocatedNEEWeights_[t][i][j] /= bookingEntityWeightTotal_[t];
            allocatedEPVWeights_[t][i][j] /= epvWeightTotal_[t];
            totalEE += allocatedEEWeights_[t][i][j];
            totalNEE += allocatedNEEWeights_[t][i][j];
            totalEPV += allocatedEPVWeights_[t][i][j];
          }
        }
        // now change expectation for each trade to proportion of total expected exposure
        for (int i = 0; i < nettingCount; i++)
        {
          for (int j = 0; j < allocatedEEWeights_[t][i].Length; j++)
          {
            if (totalEE != 0.0)
              allocatedEEWeights_[t][i][j] /= totalEE;
            if (totalNEE != 0.0)
              allocatedNEEWeights_[t][i][j] /= totalNEE;
            if (totalEPV != 0.0)
              allocatedEPVWeights_[t][i][j] /= totalEPV;
          }
        }
      }
      reduced_ = true;
    }

    private Tuple<double[], double[]> AddExoticPvs(Tuple<double[], double[]> vanillaPvs, int pathIdx, int dtIdx)
    {
      if (!PrecalculatedPvs.Any())
        return vanillaPvs;
      var nettedPvs = vanillaPvs.Item1;
      var tradePvs = new double[PortfolioData.Portfolio.Length + PrecalculatedPvs.Count];
      int tradeIdx = 0;
      while (tradeIdx < vanillaPvs.Item2.Length)
      {
        tradePvs[tradeIdx] = vanillaPvs.Item2[tradeIdx];
        ++tradeIdx;
      }

      for (int i = 0; i < PrecalculatedPvs.Count; i++)
      {
        var precalculatedPv = PrecalculatedPvs[i];
        tradePvs[tradeIdx] = precalculatedPv[pathIdx, dtIdx];
        nettedPvs[OriginalPortfolioData.Exotics[i].Item3] += precalculatedPv[pathIdx, dtIdx];
        ++tradeIdx;
      }
      return new Tuple<double[], double[]>(nettedPvs, tradePvs);
    }

    #endregion
  }

}