//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics.Rng;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRMonteCarlo : TestCCRBase
  {
    #region Data

    private int factorCount_ = 5;
    private int sampleSize_ = 20000;
    private Tenor tenor_ = Tenor.Empty;

    #endregion

    #region Initialization

    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateInput(factorCount_, sampleSize_, tenor_, currencies_.Length, lambda_.Length, true, false, false, false);
      var groups = new UniqueSequence<string>(input_.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      netting_ = new Netting(groupArray, subGroupArray, null);
    }

    protected override ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      return Simulations.SimulateCounterpartyCreditRisks(MultiStreamRng.Type.MersenneTwister, input.AsOf, input.Pricers,
                                                         input.Names, input.Cpty,
                                                         input.CptyRec, input.TenorDates, input.DiscountCurves,
                                                         input.FxRates, input.FwdCurves, input.CreditCurves, 
                                                         input.Volatilities, input.FactorLoadings, -100.0, input.Sample, input.SimulDates,
                                                         input.GridSize, false);
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      var pricers = new IPricer[3];
      pricers[0] = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), input_.CreditCurves[0],
                                      input_.DiscountCurves[0]);
      pricers[1] = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), input_.CreditCurves[1],
                                      input_.DiscountCurves[1]);
      pricers[2] = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), input_.CreditCurves[2],
                                      input_.DiscountCurves[2]);
      id = new[] {"A", "B", "C"};
      return pricers;
    }

    private IRunSimulationPath CreatePathwiseEngine(Input input)
    {
      return CreatePathwiseEngine(input, null);
    }

    #endregion

    #region ParallelMC

    private class Thread
    {
      internal Thread(MultiStreamRng rng, int dim)
      {
        Generator = rng.Clone();
        Parallel = new double[dim];
      }

      internal double[] Parallel { get; private set; }
      internal MultiStreamRng Generator { get; private set; }
    }



    [Test]
    public void ParallelRandomNumbersFor()
    {
      const int nfactors = 10;
      const int ndates = 50;
      const int size = nfactors * ndates;
      const int paths = 5000;
      MultiStreamRng rng = MultiStreamRng.Create(MultiStreamRng.Type.MersenneTwister, nfactors,
                                                 ArrayUtil.Generate(ndates, i => Dt.Add(asOf_, i * 180) - asOf_));
      var serialRng = new RandomNumberGenerator(985456376);
      var allSerial = new double[size * paths];
      var allParallel = new double[size * paths];
      for (int i = 0, k = 0; i < paths; ++i)
      {
        for (int j = 0; j < size; ++j, ++k)
          allSerial[k] = serialRng.Uniform(0, 1);
      }
      Parallel.For(0, paths, () => new Thread(rng, size), (idx, thread) =>
                                                          {
                                                            thread.Generator.Uniform(idx, thread.Parallel);
                                                            int rngidx = idx * size;
                                                            for (int j = 0; j < size; ++j)
                                                              allParallel[rngidx + j] = thread.Parallel[j];
                                                          });
      bool success = true;
      for (int i = 0; i < allParallel.Length; ++i)
      {
        success &= allParallel[i].ApproximatelyEqualsTo(allSerial[i]);
      }
      Assert.IsTrue(success);
    }

    [Test]
    public void ParallelRandomNumbersWithJump()
    {
      const int nfactors = 10;
      const int ndates = 50;
      const int size = nfactors * ndates;
      const int paths = 5000;
      MultiStreamRng rng = MultiStreamRng.Create(MultiStreamRng.Type.MersenneTwister, nfactors,
                                                 ArrayUtil.Generate(ndates, i => Dt.Add(asOf_, i * 180) - asOf_));
      var serialRng = new RandomNumberGenerator(985456376);
      var allSerial = new double[size * paths];
      var serial = new double[size];
      var parallel = new double[size];
      for (int i = 0, k = 0; i < paths; ++i)
      {
        for (int j = 0; j < size; ++j, ++k)
          allSerial[k] = serialRng.Uniform(0, 1);
      }
      var rand = new Random();
      bool success = true;
      for (int i = 0; i < 1000; ++i)
      {
        int idx = rand.Next(5000);
        rng.Uniform(idx, parallel);
        for (int j = 0; j < size; ++j)
          serial[j] = allSerial[idx * size + j];
        for (int j = 0; j < size; ++j)
          success &= parallel[j].ApproximatelyEqualsTo(serial[j]);
      }
      Assert.IsTrue(success);
    }

    #endregion

    #region Tests

    [Test]
    public void CVACalculations()
    {
      BaseCVACalculations();

    }

    [Test]
    public void RnTest()
    {
      BaseRnTest();
    }

    [Test]
    public void CvaHedges()
    {
      BaseTestHedgeNotionals(CreateInput(factorCount_, sampleSize_, tenor_, currencies_.Length, lambda_.Length, true, false, false, false));
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void CreditSpreadSensitivities()
    {
      BaseCreditSpreadSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void RateSensitivities()
    {
      BaseRateSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void FxSensitivities()
    {
      BaseFxSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void CreditVolSensitivities()
    {
      BaseCreditVolSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void RateVolSensitivities()
    {
      BaseRateVolSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void FxVolSensitivities()
    {
      BaseFxVolSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void CreditFactorSensitivities()
    {
      BaseCreditFactorSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void RateFactorSensitivities()
    {
      BaseRateFactorSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void FxFactorSensitivities()
    {
      BaseFxFactorSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void FwdSensitivities()
    {
      BaseFwdSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void FwdVolSensitivities()
    {
      BaseFwdVolSensitivities();
    }

    [Ignore("Unknown.  TODO")]
    [Test]
    public void FwdFactorSensitivities()
    {
      BaseFwdFactorSensitivities();
    }

    [Test]
    public void TestTowerPropertyNormalRates()
    {
      BaseTestTowerPropertyNormalRates(null);
    }

    [Test]
    public void TestTowerProperty()
    {
      BaseTestTowerProperty(null);
    }

    [Test]
    public void TestStockOptionTowerProperty()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, tenor_, 1, 0, false, false, true, false);
      var stockForwardCurve = (StockCurve)input.FwdCurves.First(c => c is StockCurve);
      var stockOptionPricer = CreateStockOptionPricer(asOf_, stockForwardCurve, stockForwardCurve.Interpolate(Dt.Add(asOf_, "5Y")), "5Y", OptionType.Call,
                                                      new VolatilityCurve(asOf_, stockVol_), 1.0);
      input.Pricers = new IPricer[] { stockOptionPricer };
      input.Names = new[] { "OptionPricer" };
      BaseTestTowerProperty(input);
    }

    [Test]
    public void TestForwardStockWithDividendsTowerProperty()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, tenor_, 1, 0, false, false, true, false);
      var stockForwardCurve = (StockCurve)input.FwdCurves.First(c => c is StockCurve);
      foreach (var dt in input.SimulDates)
        stockForwardCurve.Dividends.Add(dt, 0.01, DividendSchedule.DividendType.Proportional);
      var forwardPricer = new TestStockPricer(asOf_, asOf_, stockForwardCurve);
      input.Pricers = new IPricer[] {forwardPricer};
      input.Names = new[] {"OptionPricer"};
      BaseTestTowerProperty(input);
    }


    [Test]
    public void TestForeignForwardTowerProperty()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, tenor_, 2, 0, false, true, false, false);
      var inflCurve = CreateInflationCurve(asOf_, spotInflation_, tenors_, inflationZeroRates_, input.DiscountCurves[1],
                                           dayCounts_[1], calendar_[1], roll_[1]);
      var inflZerPricer = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), inflCurve, input.DiscountCurves[1]);
      input.Pricers = new IPricer[]{inflZerPricer};
      input.Names = new[] {"InflationZeroPricer"};
      BaseTestTowerProperty(input);
    }

    [Test]
    public void TestForeignDualCurveTowerProperty()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, tenor_, 2, 0, false, false, false, true);
      var projection = input.FwdCurves.First(c => c is DiscountCurve);
      projection.Ccy = currencies_[1];
      var fwdPricer = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), projection, input.DiscountCurves[1]);
      input.Pricers = new IPricer[] {fwdPricer};
      input.Names = new[] {"fwdPricer"};
      BaseTestTowerProperty(input);
    }


    [Test]
    public void CVACalculationsWithInflation()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, tenor_, 1, 2, false, true, false, false);
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input);
      engine.Execute();
      DataTable dt = FormatResults(engine, netting_);
      timer.Stop();
      var retVal = ToResultData(dt, timer.Elapsed);
      MatchExpects(retVal);
    }

    [Test]
    public void CVACalculationsWithEquity()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, tenor_, 1, 2, false, false, true, false);
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input);
      engine.Execute();
      DataTable dt = FormatResults(engine, netting_);
      timer.Stop();
      var retVal = ToResultData(dt, timer.Elapsed);
      MatchExpects(retVal);
    }

    [Test]
    public void CVACalculationsWithDualCurve()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, tenor_, 1, 2, false, false, false, true);
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input);
      engine.Execute();
      DataTable dt = FormatResults(engine, netting_);
      timer.Stop();
      var retVal = ToResultData(dt, timer.Elapsed);
      MatchExpects(retVal);
    }


    /// <summary>
    /// Compare pathwise (used by Risk) and regular cva results. 
    /// </summary>
    /// <returns></returns>
    [Test]
    public void PathwiseCVACalculations()
    {
      Timer timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      engine.Execute();
      DataTable expectsTable = FormatResults(engine, netting_);
      IRunSimulationPath pathWiseEngine = CreatePathwiseEngine(input_);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.CVA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.DVA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.EE, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.PFE, 0.99);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.NEE, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FCA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FBA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FCA0, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FBA0, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FCANoDefault, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FBANoDefault, 1.0);
      
      pathWiseEngine.RunSimulationPaths(0, input_.Sample);
      DataTable resultsTable = FormatResults(pathWiseEngine);
      timer.Stop();
      ResultData retVal = ToResultData(expectsTable, resultsTable, timer.Elapsed);
      MatchExpects(retVal);
    }



    /// <summary>
    /// Compare pathwise (used by Risk) and regular fva results. 
    /// </summary>
    /// <returns></returns>
    [Test]
    public void PathwiseFVACalculations()
    {
      Timer timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      engine.Execute();
      DataTable expectsTable = FormatFundingResults(engine, netting_);
      IRunSimulationPath pathWiseEngine = CreatePathwiseEngine(input_);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FCA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FBA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FCA0, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FBA0, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FCANoDefault, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.FBANoDefault, 1.0);

      pathWiseEngine.RunSimulationPaths(0, input_.Sample);
      DataTable resultsTable = FormatFundingResults(pathWiseEngine);
      timer.Stop();
      ResultData retVal = ToResultData(expectsTable, resultsTable, timer.Elapsed);
      MatchExpects(retVal);
    }
    /// <summary>
    /// Test that Marginal gives same values as baseline simulation when 
    /// new netting sets are used in marginal
    /// </summary>
    [Test]
    public void MarginalCVAComparison()
    {
      Timer timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      engine.Execute();
      DataTable dt1 = FormatResults(engine, netting_);
      // rerun same pricers for marginal but put in new netting sets
      string[] margNames = ArrayUtil.Generate(input_.Names.Length, (i) => input_.Names[i] + " Marg");
      ICounterpartyCreditRiskCalculations marginalEngine =
        Simulations.SimulateCounterpartyCreditRisks(
          input_.AsOf, engine.SimulatedValues, engine.SimulatedValues.PathCount, input_.Pricers, margNames, input_.Cpty,
          input_.CptyRec, input_.TenorDates, input_.DiscountCurves, input_.FxRates, input_.FwdCurves, input_.CreditCurves,
          input_.Volatilities, input_.FactorLoadings, -100.0, false);
      marginalEngine.Execute();
      var netting = new Netting(new[] {"A Marg", "B Marg"}, new[] {"2", "2"},
                                null);

      DataTable dt2 = FormatResults(marginalEngine, netting);
      timer.Stop();
      ResultData retVal = ToResultData(dt1, dt2, timer.Elapsed);
      MatchExpects(retVal);
    }

    [Test]
    public void MarginalCVACalculations()
    {
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      engine.Execute();
      Input input = input_;
      IPricer[] margPricers = {input.Pricers[0], input.Pricers[1]};
      string[] margNames = {"A", "C"};
      Timer timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations marginalEngine =
        Simulations.SimulateCounterpartyCreditRisks(
          input.AsOf, engine.SimulatedValues, engine.SimulatedValues.PathCount, margPricers, margNames, input.Cpty,
          input.CptyRec, input.TenorDates, input.DiscountCurves, input.FxRates, input.FwdCurves, input.CreditCurves,
          input.Volatilities, input.FactorLoadings, -100.0, false);
      timer.Stop();
      var netting = new Netting(new[] {"A"}, new[] {"1"}, null);
      marginalEngine.Execute();
      DataTable dt = FormatResults(marginalEngine, netting);
      ResultData retVal = ToResultData(dt, timer.Elapsed);
      MatchExpects(retVal);
    }


    [Test]
    public void CVAVaR()
    {
      var survivalProbShifts = new double[10][];
      var labels = new string[10];
      var bumpedTenors = new string[input_.Cpty[0].Tenors.Count];
      for (int i = 0; i < bumpedTenors.Length; i++)
      {
        bumpedTenors[i] = input_.Cpty[0].Tenors[i].Name;
      }
      for (int i = 0; i < survivalProbShifts.Length; i++)
      {
        survivalProbShifts[i] = new double[bumpedTenors.Length];
        for (int j = 0; j < survivalProbShifts[i].Length; j++)
        {
          survivalProbShifts[i][j] = i / 10000.0;
        }
        labels[i] = String.Format("CVA Delta {0}", i);
      }

      Timer timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      engine.Execute();
      var varEngine = new CVAVaREngine(input_.AsOf, input_.SimulDates, input_.Names, netting_);
      var ee = new double[input_.SimulDates.Length];
      for (int i = 0; i < ee.Length; ++i)
      {
        ee[i] = engine.GetMeasure(CCRMeasure.EE, netting_, input_.SimulDates[i], 1.0);
      }
      var cvaVaRDeltas = varEngine.CalculateShiftedRegulatoryCVADeltas(ee, input_.Cpty[0], bumpedTenors, survivalProbShifts, false, null);

      timer.Stop();

      ResultData rd = LoadExpects();
      rd.Accuracy = 1e-1;
      if (rd.Results.Length == 0 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }

      rd.Results[0].Name = "CVAVaR";
      rd.Results[0].Labels = labels;
      rd.Results[0].Actuals = cvaVaRDeltas;

      rd.TimeUsed = timer.Elapsed;
      MatchExpects(rd);
    }

    [Test]
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping(CreateInput(factorCount_, 1, tenor_, currencies_.Length, lambda_.Length, true, false, false, false));
    }

    #endregion

    #region Calibration Test
    
    [Test]
    public void TestCapletVolConversionFromModelParameters()
    {

      var index = new InterestRateIndex("libor3M", Frequency.SemiAnnual, Currency.USD,
                                        DayCount.Actual360, Calendar.NYB, 2);
      var dc = new DiscountCurve(asOf_, 0.03);
      var alpha = new Curve(asOf_, 0.5);
      var beta = new Curve(asOf_, 0.75);
      var nu = new Curve(asOf_, 0.1);
      var rho = new Curve(asOf_, 0.5);
      var modelParameters = new RateModelParameters(RateModelParameters.Model.SABR,
                                                    new[]
                                                    {
                                                      RateModelParameters.Param.Alpha, RateModelParameters.Param.Beta, RateModelParameters.Param.Nu,
                                                      RateModelParameters.Param.Rho
                                                    },
                                                    new IModelParameter[] { alpha, beta, nu, rho }, index);

      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y" };
      var ten = Array.ConvertAll(tenors, Tenor.Parse);
      var correlation = new FactorLoadingCollection(new[] { "" }, ten);
      var factorLoadings = new double[ten.Length,1];
      for(int i = 0; i < factorLoadings.GetLength(0); ++i)
        factorLoadings[i, 0] = 1.0;
      correlation.AddFactors(dc, factorLoadings);
      var retVal = new VolatilityCollection(ten);
      CCRCalibrationUtils.FromVolatilityObject(dc, modelParameters, false, correlation, retVal);
      var vols = retVal.GetVolsAt(dc).Select(v => v.Interpolate(0.0)).ToArray();
      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      rd.Results[0].Actuals = vols;
      rd.Results[0].Labels = tenors;
      MatchExpects(rd);
    }

    [Test]
    public void TestCapletVolConversionFromModelParametersNormal()
    {

      var index = new InterestRateIndex("libor3M", Frequency.SemiAnnual, Currency.USD,
                                        DayCount.Actual360, Calendar.NYB, 2);
      var dc = new DiscountCurve(asOf_, 0.03);
      var alpha = new Curve(asOf_, 0.5);
      var beta = new Curve(asOf_, 0.75);
      var nu = new Curve(asOf_, 0.1);
      var rho = new Curve(asOf_, 0.5);
      var modelParameters = new RateModelParameters(RateModelParameters.Model.SABR,
                                                    new[]
                                                    {
                                                      RateModelParameters.Param.Alpha, RateModelParameters.Param.Beta, RateModelParameters.Param.Nu,
                                                      RateModelParameters.Param.Rho
                                                    },
                                                    new IModelParameter[] { alpha, beta, nu, rho }, index);

      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y" };
      var ten = Array.ConvertAll(tenors, Tenor.Parse);
      var correlation = new FactorLoadingCollection(new[] { "" }, ten);
      var factorLoadings = new double[ten.Length, 1];
      for (int i = 0; i < factorLoadings.GetLength(0); ++i)
        factorLoadings[i, 0] = 1.0;
      correlation.AddFactors(dc, factorLoadings);
      var retVal = new VolatilityCollection(ten);
      CCRCalibrationUtils.FromVolatilityObject(dc, modelParameters, false, correlation, retVal);
      var vols = retVal.GetVolsAt(dc).Select(v => v.Interpolate(0.0)).ToArray();
      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      rd.Results[0].Actuals = vols;
      rd.Results[0].Labels = tenors;
      MatchExpects(rd);
    }

    [Test]
    public void TestFactorInterpolation()
    {
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y" };
      var factors = new double[tenors.Length, 5];
      for (int i = 0; i < factors.GetLength(0); ++i)
        factors[i, i % 5] = 1.0;
      var ten = Array.ConvertAll(tenors, t => Dt.Add(asOf_, t));
      var retVal = CalibrationUtils.InterpolateFactorLoadings(asOf_, factors, ten, ten);
      for (int i = 0; i < factors.GetLength(0); ++i)
        for (int j = 0; j < factors.GetLength(1); ++j)
          Assert.AreEqual(factors[i, j], retVal[i, j], 1e-6);
      var random = new Random(5);
      for (int i = 0; i < factors.GetLength(0); ++i)
      {
        double r = 0.0;
        for (int j = 0; j < factors.GetLength(1); ++j)
        {
          double rj = -1.0 + 2.0 * random.NextDouble();
          factors[i, j] = rj;
          r += rj * rj;
        }
        for (int j = 0; j < factors.GetLength(1); ++j)
          factors[i, j] /= Math.Sqrt(r);
      }
      retVal = CalibrationUtils.InterpolateFactorLoadings(asOf_, factors, ten, ten);
      for (int i = 0; i < factors.GetLength(0); ++i)
        for (int j = 0; j < factors.GetLength(1); ++j)
          Assert.AreEqual(factors[i, j], retVal[i, j], 1e-6);
    }

    [Test]
    public void TestCapletVolConversionFromCube()
    {
      var index = new InterestRateIndex("", Tenor.Parse(indexTenor_[0]).ToFrequency(), currencies_[0],
                                       dayCounts_[0], calendar_[0], 2);
      var dc = new DiscountCurve(asOf_, 0.03);
      var capVols = tenors_.Select(t => 0.5).ToArray();
      var volCube = GetCapletVolCube(asOf_, index, dc, tenors_, capVols, VolatilityType.LogNormal);
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y" };
      var ten = Array.ConvertAll(tenors, Tenor.Parse);
      var correlation = new FactorLoadingCollection(new[] { "" }, ten);
      var factorLoadings = new double[ten.Length, 1];
      for (int i = 0; i < factorLoadings.GetLength(0); ++i)
        factorLoadings[i, 0] = 1.0;
      correlation.AddFactors(dc, factorLoadings);
      var retVal = new VolatilityCollection(ten);
      CCRCalibrationUtils.FromVolatilityObject(dc, volCube, false, correlation, retVal);
      double[] vols = Array.ConvertAll(retVal.GetVolsAt(dc), d => d.Interpolate(0.0));
      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      rd.Results[0].Actuals = vols;
      rd.Results[0].Labels = tenors;
      MatchExpects(rd);
    }

    [Test]
    public void TestCapletVolConversionFromSurface()
    {
      var dc = new DiscountCurve(asOf_, 0.03);
      var volatilitySurface = GetBgmForwardVolatilitySurface(asOf_, dc);
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y" };
      var ten = Array.ConvertAll(tenors, Tenor.Parse);
      var correlation = new FactorLoadingCollection(new[] { "USD" }, ten);
      var factorLoadings = new double[ten.Length, 1];
      for (int i = 0; i < factorLoadings.GetLength(0); ++i)
        factorLoadings[i, 0] = 1.0;
      correlation.AddFactors(dc, factorLoadings);
      var retVal = new VolatilityCollection(ten);
      CCRCalibrationUtils.FromVolatilityObject(dc, volatilitySurface, false, correlation, retVal);
      double[] vols = Array.ConvertAll(retVal.GetVolsAt(dc), d => d.Interpolate(0.0));
      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      rd.Results[0].Actuals = vols;
      rd.Results[0].Labels = tenors;
      MatchExpects(rd);
    }

    [Test]
    public void TestCapletVolConversionFromSwaptionSurface()
    {
      var timer = new Timer();
      timer.Start();
      var dc = new DiscountCurve(asOf_, 0.03);
      var volatilitySurface = GetMarketSwaptionVolatility(asOf_, dc);
      var tenors = new[] {"1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y"};
      var ten = Array.ConvertAll(tenors, Tenor.Parse);
      var correlation = new FactorLoadingCollection(new[] {"factor0", "factor1", "factor2", "factor3"}, ten);
      var vols = new VolatilityCollection(ten);
      var swapFactors = GenerateFactors(new Random(3), 2, 4, 1.0);
      var swapEffective = new[] {asOf_, asOf_};
      var swapMaturities = new[] {Dt.Add(asOf_, "5Y"), Dt.Add(asOf_, "10Y")};
      var fl = new double[swapFactors.GetLength(0)][];
      for (int i = 0; i < fl.Length; ++i)
      {
        fl[i] = new double[swapFactors.GetLength(1)];
        for (int j = 0; j < fl[i].Length; ++j)
          fl[i][j] = swapFactors[i, j];
      }
      VolatilityCurve[] bespokeVols;
      var calibratedFactors = CCRCalibrationUtils.CalibrateForwardRateFactorLoadings(asOf_, tenors.Select(t => Dt.Add(asOf_, t)).ToArray(), dc,
                                                                                     volatilitySurface, swapEffective, swapMaturities, fl, null, false,
                                                                                     out bespokeVols);
      correlation.AddFactors(dc, calibratedFactors);
      vols.Add(dc, bespokeVols);
      timer.Stop();
      var results = ToCalibrationResultData(correlation, vols, timer.Elapsed);
      MatchExpects(results);
    }


    [Test]
    public void TestModelCalibration()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(factorCount_, sampleSize_, Tenor.Empty, currencies_.Length, lambda_.Length, true, true, true, true);
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y"};
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var primaryVols = new List<object>();
      var primaryVar = new List<object>();
      var primaryTen = new List<Tenor>();
      var primaryType = new List<CCRCalibrationUtils.MarketVariableType>();
      var secondaryVar = new List<object>();
      var secondaryVol = new List<object>();
      var secondaryTen = new List<Tenor>();
      var secondaryType = new List<CCRCalibrationUtils.MarketVariableType>();
      foreach(var dc in input.DiscountCurves)
      {
        var index = new InterestRateIndex(String.Concat(dc.Ccy, "_LIBOR"), Frequency.Quarterly, dc.Ccy, dc.DayCount, dc.SpotCalendar, 2);
        var volCube = GetCapletVolCube(asOf_, index, dc, capTenors_, (dc.Ccy == Currency.EUR) ? eurCapVols_ : (dc.Ccy == Currency.JPY) ? jpyCapVols_ : usdCapVols_, VolatilityType.LogNormal);
        primaryVols.Add(volCube);
        primaryVols.Add(null);
        primaryVar.Add(dc);
        primaryVar.Add(dc);
        primaryTen.Add(Tenor.Parse("2Y"));
        primaryTen.Add(Tenor.Parse("10Y"));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SwapRate);
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SwapRate);
      }
      foreach (var fx in input.FxRates)
      {
        primaryVar.Add(CreateFxCurve(fx, input.DiscountCurves.First(dc => dc.Ccy == fx.FromCcy), input.DiscountCurves.First(dc => dc.Ccy == fx.ToCcy)));
        primaryVols.Add(GetVolCurve(fx.Name, tenors_, 0.2, -0.005));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SpotFx);
        primaryTen.Add(Tenor.Empty);
      }
      foreach (var fwd in input.FwdCurves.Where(c => !(c is StockCurve)))
      {
        if (fwd is DiscountCurve)
        {
          var index = new InterestRateIndex(String.Concat(fwd.Ccy, "_LIBOR"), Frequency.Quarterly, fwd.Ccy, fwd.DayCount, fwd.SpotCalendar, 2);
          var volCube = GetCapletVolCube(asOf_, index, fwd as DiscountCurve, tenors_,
                                         (fwd.Ccy == Currency.EUR) ? eurCapVols_ : (fwd.Ccy == Currency.JPY) ? jpyCapVols_ : usdCapVols_, VolatilityType.LogNormal);
          primaryVols.Add(volCube);
          primaryVar.Add(fwd);
          primaryTen.Add(Tenor.Parse("10Y"));
          primaryType.Add(CCRCalibrationUtils.MarketVariableType.SwapRate);
          continue;
        }
        primaryVols.Add(GetVolCurve(fwd.Name, tenors_, 0.03, 0.005));
        primaryVols.Add(null);
        primaryVar.Add(fwd);
        primaryTen.Add(Tenor.Parse("2Y"));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.ForwardPrice);
        primaryVar.Add(fwd);
        primaryTen.Add(Tenor.Parse("10Y"));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.ForwardPrice);
      }
      foreach (var fwd in input.FwdCurves.Where(c => c is StockCurve))
      {
        var spot = ((StockCurve)fwd).Spot;
        primaryVols.Add(GetVolCurve(spot.Name, tenors_, 0.4, 0.005));
        primaryVar.Add(spot);
        primaryTen.Add(Tenor.Empty);
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SpotPrice);
      }
      foreach (var credit in input.CreditCurves)
      {
        secondaryVar.Add(credit);
        secondaryType.Add(CCRCalibrationUtils.MarketVariableType.CreditSpread);
        secondaryTen.Add(Tenor.Parse("5Y"));
        secondaryVol.Add(GetVolCurve(credit.Name, new[] {"3M", "6M", "1Y"}, 0.7, 0.005));
      }
      

      var fl = GenerateFactors(new Random(3), primaryVar.Count, factorCount_, 1.0);
      var correlationMatrix = GenerateCorrelationMatrix(fl);
      var betas = GenerateBetas(new Random(5), secondaryVar.Count, fl, correlationMatrix, 0.95);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      CCRCalibrationUtils.CalibrateMonteCarloModel(asOf_, factorCount_, tenors.Select(Tenor.Parse).ToArray(), primaryType.ToArray(),
                                                     primaryVar.ToArray(), primaryTen.ToArray(), primaryVols.ToArray(), correlationMatrix,
                                                     secondaryType.ToArray(), secondaryVar.ToArray(), secondaryTen.ToArray(), secondaryVol.ToArray(), betas,
                                                     null, false, out factors, out vols);
      timer.Stop();
      var results = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(results);
    }

    [Test]
    public void TestIncrementalModelCalibration()
    {
      VolatilityCollection ccrModelCalibrateVolatilityCollection;
      VolatilityCollection ccrIncrementalModelCalibrateVolatilityCollection;
      FactorLoadingCollection ccrModelCalibrateFactorLoadingCollection;
      FactorLoadingCollection ccrIncrementalModelCalibrateFactorLoadingCollection;

      var input = CreateInput(factorCount_, sampleSize_, Tenor.Empty, currencies_.Length, lambda_.Length, true, true, true, true);
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y" };
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var primaryVols = new List<object>();
      var primaryVar = new List<object>();
      var primaryTen = new List<Tenor>();
      var primaryType = new List<CCRCalibrationUtils.MarketVariableType>();
      var secondaryVar = new List<object>();
      var secondaryVol = new List<object>();
      var secondaryTen = new List<Tenor>();
      var secondaryType = new List<CCRCalibrationUtils.MarketVariableType>();
      foreach (var dc in input.DiscountCurves)
      {
        var index = new InterestRateIndex(String.Concat(dc.Ccy, "_LIBOR"), Frequency.Quarterly, dc.Ccy, dc.DayCount, dc.SpotCalendar, 2);
        var volCube = GetCapletVolCube(asOf_, index, dc, capTenors_,
          (dc.Ccy == Currency.EUR) ? eurCapVols_ : (dc.Ccy == Currency.JPY) ? jpyCapVols_ : usdCapVols_, VolatilityType.LogNormal);
        primaryVols.Add(volCube);
        primaryVols.Add(null);
        primaryVar.Add(dc);
        primaryVar.Add(dc);
        primaryTen.Add(Tenor.Parse("2Y"));
        primaryTen.Add(Tenor.Parse("10Y"));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SwapRate);
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SwapRate);
      }
      foreach (var fx in input.FxRates)
      {
        primaryVar.Add(CreateFxCurve(fx, input.DiscountCurves.First(dc => dc.Ccy == fx.FromCcy), input.DiscountCurves.First(dc => dc.Ccy == fx.ToCcy)));
        primaryVols.Add(GetVolCurve(fx.Name, tenors_, 0.2, -0.005));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SpotFx);
        primaryTen.Add(Tenor.Empty);
      }
      foreach (var fwd in input.FwdCurves.Where(c => !(c is StockCurve)))
      {
        if (fwd is DiscountCurve)
        {
          var index = new InterestRateIndex(String.Concat(fwd.Ccy, "_LIBOR"), Frequency.Quarterly, fwd.Ccy, fwd.DayCount, fwd.SpotCalendar, 2);
          var volCube = GetCapletVolCube(asOf_, index, fwd as DiscountCurve, tenors_,
            (fwd.Ccy == Currency.EUR) ? eurCapVols_ : (fwd.Ccy == Currency.JPY) ? jpyCapVols_ : usdCapVols_, VolatilityType.LogNormal);
          primaryVols.Add(volCube);
          primaryVar.Add(fwd);
          primaryTen.Add(Tenor.Parse("10Y"));
          primaryType.Add(CCRCalibrationUtils.MarketVariableType.SwapRate);
          continue;
        }
        primaryVols.Add(GetVolCurve(fwd.Name, tenors_, 0.03, 0.005));
        primaryVols.Add(null);
        primaryVar.Add(fwd);
        primaryTen.Add(Tenor.Parse("2Y"));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.ForwardPrice);
        primaryVar.Add(fwd);
        primaryTen.Add(Tenor.Parse("10Y"));
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.ForwardPrice);
      }
      foreach (var fwd in input.FwdCurves.Where(c => c is StockCurve))
      {
        var spot = ((StockCurve)fwd).Spot;
        primaryVols.Add(GetVolCurve(spot.Name, tenors_, 0.4, 0.005));
        primaryVar.Add(spot);
        primaryTen.Add(Tenor.Empty);
        primaryType.Add(CCRCalibrationUtils.MarketVariableType.SpotPrice);
      }
      foreach (var credit in input.CreditCurves)
      {
        secondaryVar.Add(credit);
        secondaryType.Add(CCRCalibrationUtils.MarketVariableType.CreditSpread);
        secondaryTen.Add(Tenor.Parse("5Y"));
        secondaryVol.Add(GetVolCurve(credit.Name, new[] { "3M", "6M", "1Y" }, 0.7, 0.005));
      }

      var fl = GenerateFactors(new Random(3), primaryVar.Count, factorCount_, 1.0);
      var correlationMatrix = GenerateCorrelationMatrix(fl);
      var betas = GenerateBetas(new Random(5), secondaryVar.Count, fl, correlationMatrix, 0.95);
      
      CCRCalibrationUtils.CalibrateMonteCarloModel(asOf_, factorCount_, tenors.Select(Tenor.Parse).ToArray(), primaryType.ToArray(),
        primaryVar.ToArray(), primaryTen.ToArray(), primaryVols.ToArray(), correlationMatrix,
        secondaryType.ToArray(), secondaryVar.ToArray(), secondaryTen.ToArray(), secondaryVol.ToArray(), betas,
        null, false, out ccrModelCalibrateFactorLoadingCollection, out ccrModelCalibrateVolatilityCollection);

      IList<CCRCalibrationUtils.FactorData> factorData = CCRCalibrationUtils.CalibrateMonteCarloModelIncremental(asOf_, CCRCalibrationUtils.GetFactorNames(factorCount_), tenors.Select(Tenor.Parse).ToArray(), primaryType.ToArray(),
        primaryVar.ToArray(), primaryTen.ToArray(), primaryVols.ToArray(), correlationMatrix, secondaryType.ToArray(), secondaryVar.ToArray(),
        secondaryTen.ToArray(), secondaryVol.ToArray(), betas, out ccrIncrementalModelCalibrateFactorLoadingCollection, out ccrIncrementalModelCalibrateVolatilityCollection);

      var marketFactorNames = ccrIncrementalModelCalibrateFactorLoadingCollection.MarketFactorNames;
      var log = new CalibrationUtils.CalibrationLogCollection(1);

      foreach (var fwd in input.FwdCurves)
      {
        if (fwd is DiscountCurve)
        {
          CCRCalibrationUtils.TryCalibrateDiscountCurve(asOf_, null, false, ccrIncrementalModelCalibrateFactorLoadingCollection,
            ccrIncrementalModelCalibrateVolatilityCollection, factorData, marketFactorNames, (DiscountCurve)fwd);
          ValidatePartialFactorLoadings(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
          ValidateParitialVolatilities(ccrModelCalibrateVolatilityCollection, ccrIncrementalModelCalibrateVolatilityCollection);
        }
        else if (fwd is SurvivalCurve)
        {
          CCRCalibrationUtils.TryCalibrateSurvivalCurve(asOf_, ccrIncrementalModelCalibrateFactorLoadingCollection,
            ccrIncrementalModelCalibrateVolatilityCollection, factorData, marketFactorNames, (SurvivalCurve)fwd, log);
          ValidatePartialFactorLoadings(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
          ValidateParitialVolatilities(ccrModelCalibrateVolatilityCollection, ccrIncrementalModelCalibrateVolatilityCollection);
        }
        else if (fwd is FxCurve)
        {
          CCRCalibrationUtils.TryCalibrateFxCurve(asOf_, ccrIncrementalModelCalibrateFactorLoadingCollection,
            ccrIncrementalModelCalibrateVolatilityCollection, factorData, marketFactorNames, (FxCurve)fwd, log);
          ValidatePartialFactorLoadings(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
          ValidateParitialVolatilities(ccrModelCalibrateVolatilityCollection, ccrIncrementalModelCalibrateVolatilityCollection);
        }
        else if (fwd is CalibratedCurve)
        {
          CCRCalibrationUtils.TryCalibrateForwardCurve(asOf_, ccrIncrementalModelCalibrateFactorLoadingCollection,
            ccrIncrementalModelCalibrateVolatilityCollection, factorData, marketFactorNames, (CalibratedCurve)fwd, log);
          ValidatePartialFactorLoadings(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
        }
      }

      foreach (var fwd in input.CreditCurves)
      {
        CCRCalibrationUtils.TryCalibrateSurvivalCurve(asOf_, ccrIncrementalModelCalibrateFactorLoadingCollection,
          ccrIncrementalModelCalibrateVolatilityCollection, factorData, marketFactorNames, (SurvivalCurve)fwd, log);
        ValidatePartialFactorLoadings(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
        ValidateParitialVolatilities(ccrModelCalibrateVolatilityCollection, ccrIncrementalModelCalibrateVolatilityCollection);
      }

      foreach (var fwd in input.DiscountCurves)
      {
        CCRCalibrationUtils.TryCalibrateDiscountCurve(asOf_, null, false, ccrIncrementalModelCalibrateFactorLoadingCollection,
          ccrIncrementalModelCalibrateVolatilityCollection, factorData, marketFactorNames, (DiscountCurve)fwd);
        ValidatePartialFactorLoadings(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
        ValidateParitialVolatilities(ccrModelCalibrateVolatilityCollection, ccrIncrementalModelCalibrateVolatilityCollection);
      }

      foreach (var fx in primaryVar.OfType<FxCurve>())
      {
        CCRCalibrationUtils.TryCalibrateFxCurve(asOf_, ccrIncrementalModelCalibrateFactorLoadingCollection,
          ccrIncrementalModelCalibrateVolatilityCollection, factorData, marketFactorNames, fx, log);
        ValidatePartialFactorLoadings(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
        ValidateParitialVolatilities(ccrModelCalibrateVolatilityCollection, ccrIncrementalModelCalibrateVolatilityCollection);
      }

      ValidateFactorLoadingsUponCompletion(ccrModelCalibrateFactorLoadingCollection, ccrIncrementalModelCalibrateFactorLoadingCollection);
      ValidateVolatilitiesUponCompletion(ccrModelCalibrateVolatilityCollection, ccrIncrementalModelCalibrateVolatilityCollection);
    }

    private static void ValidatePartialFactorLoadings(FactorLoadingCollection ccrModelCalbrateFactorLoadingCollection, FactorLoadingCollection ccrIncermentalModelCalbrateFactorLoadingCollection)
    {
      var resultantMarketFactorNames = ccrModelCalbrateFactorLoadingCollection.MarketFactorNames;
      Assert.IsNotNull(resultantMarketFactorNames);
      foreach (var factorName in ccrIncermentalModelCalbrateFactorLoadingCollection.MarketFactorNames)
      {
        Assert.IsNotNull(factorName);
        Assert.IsTrue(resultantMarketFactorNames.Contains(factorName), "Market Factor " + factorName + " is not in both factor loadings collections");
      }

      var resultantTenors = ccrModelCalbrateFactorLoadingCollection.Tenors;
      Assert.IsNotNull(resultantTenors);
      foreach (var tenors in ccrIncermentalModelCalbrateFactorLoadingCollection.Tenors)
      {
        Assert.IsNotNull(tenors);
        Assert.IsTrue(resultantTenors.Contains(tenors), "Tenor " + tenors + " is in not in both factor loadings collections");
      }

      // cannot use one dictionaries key for the other as it uses object refs which do not match
      foreach (var keyForIncrementalSet in ccrIncermentalModelCalbrateFactorLoadingCollection.References)
      {
        var matchingPair = false;
        foreach (var keyForTraditionalSet in ccrModelCalbrateFactorLoadingCollection.References)
        {
          Assert.IsNotNull(keyForTraditionalSet);
          Assert.IsNotNull(keyForIncrementalSet);
          if (keyForTraditionalSet.ToString().Equals(keyForIncrementalSet.ToString()))
          {
            var id1 = FactorLoadingCollection.GetId(keyForTraditionalSet);
            var matrix1 = ccrModelCalbrateFactorLoadingCollection.GetFactorsAt(keyForTraditionalSet);

            var id2 = FactorLoadingCollection.GetId(keyForIncrementalSet);
            var matrix2 = ccrIncermentalModelCalbrateFactorLoadingCollection.GetFactorsAt(keyForIncrementalSet);

            Assert.AreEqual(id1, id2);
            CompareMatricies(matrix1, matrix2, 0.01);
            // indicates that a match occured. If it does not then will trigger the test to fail at the end of the internal loop
            matchingPair = true;
          }
        }
        Assert.IsTrue(matchingPair, "Unable to find a matching pair in the two Factor Loading sets");
      }
    }

    private static void ValidateFactorLoadingsUponCompletion(FactorLoadingCollection ccrModelCalbrateFactorLoadingCollection, FactorLoadingCollection ccrIncermentalModelCalbrateFactorLoadingCollection)
    {
      Assert.AreEqual(ccrModelCalbrateFactorLoadingCollection.Count, ccrIncermentalModelCalbrateFactorLoadingCollection.Count);
      Assert.AreEqual(ccrModelCalbrateFactorLoadingCollection.FactorCount, ccrIncermentalModelCalbrateFactorLoadingCollection.FactorCount);
      Assert.AreEqual(ccrModelCalbrateFactorLoadingCollection.References.Count(), ccrIncermentalModelCalbrateFactorLoadingCollection.References.Count());
      Assert.AreEqual(ccrModelCalbrateFactorLoadingCollection.TenorCount, ccrIncermentalModelCalbrateFactorLoadingCollection.TenorCount);

      var resultantMarketFactorNames = ccrIncermentalModelCalbrateFactorLoadingCollection.MarketFactorNames;
      Assert.IsNotNull(resultantMarketFactorNames);
      foreach (var factorName in ccrModelCalbrateFactorLoadingCollection.MarketFactorNames)
      {
        Assert.IsNotNull(factorName);
        Assert.IsTrue(resultantMarketFactorNames.Contains(factorName), "Market Factor " + factorName + " is not in both factor loadings collections");
      }

      var resultantTenors = ccrIncermentalModelCalbrateFactorLoadingCollection.Tenors;
      Assert.IsNotNull(resultantTenors);
      foreach (var tenors in ccrModelCalbrateFactorLoadingCollection.Tenors)
      {
        Assert.IsNotNull(tenors);
        Assert.IsTrue(resultantTenors.Contains(tenors), "Tenor " + tenors + " is in not in both factor loadings collections");
      }

      // cannot use one dictionaries key for the other as it uses object refs which do not match
      foreach (var keyForTraditionalSet in ccrModelCalbrateFactorLoadingCollection.References)
      {
        var matchingPair = false;
        foreach (var keyForIncrementalSet in ccrIncermentalModelCalbrateFactorLoadingCollection.References)
        {
          Assert.IsNotNull(keyForTraditionalSet);
          Assert.IsNotNull(keyForIncrementalSet);
          if (keyForTraditionalSet.ToString().Equals(keyForIncrementalSet.ToString()))
          {
            var id1 = FactorLoadingCollection.GetId(keyForTraditionalSet);
            var matrix1 = ccrModelCalbrateFactorLoadingCollection.GetFactorsAt(keyForTraditionalSet);

            var id2 = FactorLoadingCollection.GetId(keyForIncrementalSet);
            var matrix2 = ccrIncermentalModelCalbrateFactorLoadingCollection.GetFactorsAt(keyForIncrementalSet);

            Assert.AreEqual(id1, id2);
            CompareMatricies(matrix1, matrix2, 0.01);
            // indicates that a match occured. If it does not then will trigger the test to fail at the end of the internal loop
            matchingPair = true;
          }
        }
        Assert.IsTrue(matchingPair, "Unable to find a matching pair in the two Factor Loading sets");
      }      
    }

    private static void ValidateParitialVolatilities(VolatilityCollection ccrModelCalbrateVolatilityCollection, VolatilityCollection ccrIncermentalModelCalbrateVolatilityCollection)
    {
      // cannot use one dictionaries key for the other as it uses object refs which do not match
      foreach (var keyForIncrementalSet in ccrIncermentalModelCalbrateVolatilityCollection.References)
      {
        var matchingPair = false;
        foreach (var keyForTraditionalSet in ccrModelCalbrateVolatilityCollection.References)
        {
          Assert.IsNotNull(keyForTraditionalSet);
          Assert.IsNotNull(keyForIncrementalSet);
          if (keyForTraditionalSet.ToString().Equals(keyForIncrementalSet.ToString()))
          {
            var id1 = FactorLoadingCollection.GetId(keyForTraditionalSet);
            var v1 = ccrModelCalbrateVolatilityCollection.GetVolsAt(keyForTraditionalSet);

            var id2 = FactorLoadingCollection.GetId(keyForIncrementalSet);
            var v2 = ccrIncermentalModelCalbrateVolatilityCollection.GetVolsAt(keyForIncrementalSet);

            Assert.AreEqual(id1, id2);
            CompareVolCurves(v1, v2, 0.01);
            // indicates that a match occured. If it does not then will trigger the test to fail at the end of the internal loop
            matchingPair = true;
          }
        }
        Assert.IsTrue(matchingPair, "Unable to find a matching pair in the two Factor Loading sets");
      }
    }

    private static void ValidateVolatilitiesUponCompletion(VolatilityCollection ccrModelCalbrateVolatilityCollection, VolatilityCollection ccrIncermentalModelCalbrateVolatilityCollection)
    {
      Assert.AreEqual(ccrModelCalbrateVolatilityCollection.Count, ccrIncermentalModelCalbrateVolatilityCollection.Count);
      Assert.AreEqual(ccrModelCalbrateVolatilityCollection.References.Count(), ccrIncermentalModelCalbrateVolatilityCollection.References.Count());
      Assert.AreEqual(ccrModelCalbrateVolatilityCollection.TenorCount, ccrIncermentalModelCalbrateVolatilityCollection.TenorCount);

      var resultantTenors = ccrIncermentalModelCalbrateVolatilityCollection.Tenors;
      Assert.IsNotNull(resultantTenors);
      foreach (var tenors in ccrModelCalbrateVolatilityCollection.Tenors)
      {
        Assert.IsNotNull(tenors);
        Assert.IsTrue(resultantTenors.Contains(tenors), "Tenor " + tenors + " is in not in both factor loadings collections");
      }

      // cannot use one dictionaries key for the other as it uses object refs which do not match
      foreach (var keyForIncrementalSet in ccrIncermentalModelCalbrateVolatilityCollection.References)
      {
        var matchingPair = false;
        foreach (var keyForTraditionalSet in ccrModelCalbrateVolatilityCollection.References)
        {
          Assert.IsNotNull(keyForTraditionalSet);
          Assert.IsNotNull(keyForIncrementalSet);
          if (keyForTraditionalSet.ToString().Equals(keyForIncrementalSet.ToString()))
          {
            var id1 = FactorLoadingCollection.GetId(keyForTraditionalSet);
            var v1 = ccrModelCalbrateVolatilityCollection.GetVolsAt(keyForTraditionalSet);

            var id2 = FactorLoadingCollection.GetId(keyForIncrementalSet);
            var v2 = ccrIncermentalModelCalbrateVolatilityCollection.GetVolsAt(keyForIncrementalSet);

            Assert.AreEqual(id1, id2);
            CompareVolCurves(v1, v2, 0.01);
            // indicates that a match occured. If it does not then will trigger the test to fail at the end of the internal loop
            matchingPair = true;
          }
        }
        Assert.IsTrue(matchingPair, "Unable to find a matching pair in the two Factor Loading sets");
      }    
    }

    private static void CompareMatricies(double[,] m1, double[,] m2, double tolerance)
    {
      Assert.AreEqual(m1.GetLength(0), m2.GetLength(0));
      Assert.AreEqual(m1.GetLength(1), m2.GetLength(1));

      for (var i = 0; i < m1.GetLength(0); ++i)
      {
        for (var j = 0; j < m1.GetLength(1); ++j)
        {
          Assert.AreEqual(m1[i, j], m2[i, j], tolerance);
        }
      }
    }

    private static void CompareVolCurves(VolatilityCurve[] v1, VolatilityCurve[] v2, double tolerance)
    {
      Assert.AreEqual(v1.Count(), v2.Count());
      foreach (var vol2 in v2)
      {
        var matchingPair = false;
        foreach (var vol1 in v1)
        {
          Assert.IsNotNull(vol1);
          Assert.IsNotNull(vol1.ToString());
          Assert.IsNotNull(vol2);
          Assert.IsNotNull(vol2.ToString());
          if (vol2.ToString().Equals(vol1.ToString()))
          {
            Assert.AreEqual(vol1.DistributionType, vol2.DistributionType);
            Assert.AreEqual(vol1.Count, vol2.Count);
            Assert.AreEqual(vol1.AsOf, vol2.AsOf);
            Assert.AreEqual(vol1.CacheEnabled, vol2.CacheEnabled);
            Assert.AreEqual(vol1.Name, vol2.Name);
            Assert.AreEqual(vol1.Category, vol2.Category);
            Assert.AreEqual(vol1.Frequency, vol2.Frequency);
            Assert.AreEqual(vol1.Points.Count, vol2.Points.Count);

            for (var i = 0; i < vol1.Points.Count; ++i)
            {
              Assert.AreEqual(vol1.Points[i].Date, vol2.Points[i].Date);
              Assert.AreEqual(vol1.Points[i].Value, vol2.Points[i].Value, tolerance);
            }

            matchingPair = true;
          }
        }
        Assert.IsTrue(matchingPair, "Unable to match vol curves");
      }
    }

    [Test]
    public void TestFactorizedVolatilitySystem()
    {
      var dc1 = new DiscountCurve(asOf_, 0.03);
      dc1.Name = "Curve1";
      var dc2 = new DiscountCurve(asOf_, 0.05);
      dc1.Name = "Curve2";
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y" };
      var factorLoadingCollection = new FactorLoadingCollection(CCRCalibrationUtils.GetFactorNames(tenors.Length).ToArray(), tenors.Select(Tenor.Parse).ToArray());
      double[,] array = new double[,] { {1, 2, 3, 4,5,6, 7 ,8, 9, 10, 11} };
      factorLoadingCollection.AddFactors(dc1, array);
      factorLoadingCollection.AddFactors(dc2, array);

      var refs = factorLoadingCollection.References;

      Assert.AreEqual(2, refs.Count());
      Assert.IsTrue(refs.Contains(dc1));
      Assert.IsTrue(refs.Contains(dc2));
    }

    [Test]
    public void TestVolatilityCollection()
    {
      var dc1 = new DiscountCurve(asOf_, 0.03);
      dc1.Name = "Curve1";
      var dc2 = new DiscountCurve(asOf_, 0.05);
      dc2.Name = "Curve2";
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y" };
      var volatilityCollection = new VolatilityCollection(tenors.Select(Tenor.Parse).ToArray());
      double[,] array = new double[,] { { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } };

      VolatilityCurve[] curves = new VolatilityCurve[] { new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_), new VolatilityCurve(asOf_) };

      volatilityCollection.Add(dc1, curves);
      volatilityCollection.Add(dc2, curves);

      var refs = volatilityCollection.References;

      Assert.AreEqual(2, refs.Count());
      Assert.IsTrue(refs.Contains(dc1));
      Assert.IsTrue(refs.Contains(dc2));
    }

    #endregion
  }
}
