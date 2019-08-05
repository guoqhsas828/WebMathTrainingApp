//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRAmericanMonteCarlo : TestCCRBase
  {
    #region Data

    private int sample_ = 5000;
    private int nFactors_ = 5;
    private Tenor gridSize_ = Tenor.Empty;

    #endregion

    #region Setup
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateThisInput(nFactors_, sample_);
      var groups = new UniqueSequence<string>(input_.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      netting_ = new Netting(groupArray, subGroupArray, null);
    }

    private Input CreateThisInput(int nFactors, int sample)
    {
      var retVal = CreateInput(nFactors, sample, gridSize_, currencies_.Length, 2, false, false, false, false);
      SwapPricer p0, p1;
      var pricers = retVal.Pricers;
      var newPricers = new IPricer[pricers.Length + 2];
      pricers.CopyTo(newPricers, 0);
      newPricers[newPricers.Length - 2] = CreateBermudanSwaptionPricer(1e7, retVal, out p0);
      newPricers[newPricers.Length - 1] = CreateBermudanFxSwaptionPricer(1e7, retVal, out p1);
      retVal.Pricers = newPricers;
      retVal.Names = ArrayUtil.Generate(newPricers.Length, i => (i % 2 == 0) ? "A" : "B"); //2 netting groups
      return retVal;
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      throw new NotImplementedException();
    }

    protected override ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      return Simulations.SimulateCounterpartyCreditRisks(MultiStreamRng.Type.MersenneTwister, input.AsOf, input.Pricers,
                                                         input.Names, input.Cpty,
                                                         input.CptyRec, input.TenorDates, input.DiscountCurves, input.FxRates, input.FwdCurves, input.CreditCurves,
                                                         input.Volatilities, input.FactorLoadings, -100.0, input.Sample, input.SimulDates, gridSize_, false);
    }

    #endregion

    #region Utilitis
    private static double TestSwapCashflowNodes(SwapPricer swapPricer)
    {
      var nodes = ((ICashflowNodesGenerator) swapPricer).Cashflow;
      var swapPv = swapPricer.Pv();
      var pvfromCashflow = nodes.Aggregate(0.0, (pv, cf) => pv + cf.RealizedAmount() * cf.RiskyDiscount() * cf.FxRate());
      return swapPv - pvfromCashflow*swapPricer.Notional;
    }

    private LeastSquaresMonteCarloCcrPricer CreateSwapPricer(double notional, out SwapPricer swapPricer)
    {
      swapPricer = CreateSwapPricer(input_.AsOf, notional, input_.DiscountCurves[0], indexTenor_[0], dayCounts_[0],
                                    calendar_[0], roll_[0], swapFrequency_[0], roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
      var cf = ((ICashflowNodesGenerator)swapPricer).Cashflow;
      var iamc = swapPricer as IAmericanMonteCarloAdapter;
      var p0 = new LeastSquaresMonteCarloCcrPricer(swapPricer.Product.Description, input_.AsOf, swapPricer.Settle, iamc.Notional,
                                                   swapPricer.ValuationCurrency, cf, null, null, iamc.DiscountCurves.ToArray(),
                                                   iamc.FxRates.ToArray(), null, null, null);
      p0.SimulationDates = input_.SimulDates;
      p0.Volatilities = input_.Volatilities;
      p0.FactorLoadings = input_.FactorLoadings;
      p0.RngType = MultiStreamRng.Type.MersenneTwister;
      p0.PathCount = input_.Sample;
      return p0;
    }


    private LeastSquaresMonteCarloCcrPricer CreateFxSwapPricer(double notional, out SwapPricer swapPricer)
    {
      swapPricer = CreateXccySwapPricer(input_.AsOf, notional, input_.FxRates[0], input_.DiscountCurves[0], input_.DiscountCurves[1], indexTenor_[1], dayCounts_[1],
                                            calendar_[1], roll_[1], swapFrequency_[0], roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
      var cf = ((ICashflowNodesGenerator)swapPricer).Cashflow;
      var iamc = swapPricer as IAmericanMonteCarloAdapter;
      var p0 = new LeastSquaresMonteCarloCcrPricer(swapPricer.Product.Description, input_.AsOf, swapPricer.Settle, iamc.Notional,
                                                   swapPricer.ValuationCurrency, cf, null, null, iamc.DiscountCurves.ToArray(),
                                                   iamc.FxRates.ToArray(), null, null, null);
      p0.SimulationDates = input_.SimulDates;
      p0.Volatilities = input_.Volatilities;
      p0.FactorLoadings = input_.FactorLoadings;
      p0.RngType = MultiStreamRng.Type.MersenneTwister;
      p0.PathCount = input_.Sample;
      return p0;
    }

    private LeastSquaresMonteCarloCcrPricer CreateBermudanFxSwaptionPricer(double notional, Input input, out SwapPricer swapPricer)
    {
      swapPricer = CreateXccySwapPricer(input.AsOf, notional, input.FxRates[0], input.DiscountCurves[0], input.DiscountCurves[1], indexTenor_[1], dayCounts_[1],
                                            calendar_[1], roll_[1], swapFrequency_[0], roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
      var underlier = CreateXccySwapPricer(input.AsOf, -1.0, input.FxRates[0], input.DiscountCurves[0], input.DiscountCurves[1], indexTenor_[1], dayCounts_[1],
                                            calendar_[1], roll_[1], swapFrequency_[0], roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
      var iamc = swapPricer as IAmericanMonteCarloAdapter;
      var p0 = new LeastSquaresMonteCarloCcrPricer(swapPricer.Product.Description, input.AsOf, input.AsOf, notional,
                                                   swapPricer.ValuationCurrency, null,
                                                   null, LeastSquaresMonteCarloCcrUtils.ExerciseEvaluatorFactory(underlier, null, null, false),
                                                   iamc.DiscountCurves.ToArray(), iamc.FxRates.ToArray(), null, null, null);
      p0.Volatilities = input.Volatilities;
      p0.FactorLoadings = input.FactorLoadings;
      p0.RngType = MultiStreamRng.Type.MersenneTwister;
      p0.PathCount = input.Sample;
      return p0;
    }

    private LeastSquaresMonteCarloCcrPricer CreateBermudanSwaptionPricer(double notional, Input input, out SwapPricer swapPricer)
    {
      swapPricer = CreateSwapPricer(asOf_, notional, input.DiscountCurves[0], indexTenor_[0], dayCounts_[0], calendar_[0], roll_[0], swapFrequency_[0], roll_[0],
                                        "10Y", dayCounts_[0], calendar_[0], 2);
      var underlier = CreateSwapPricer(asOf_, -1.0, input.DiscountCurves[0], indexTenor_[0], dayCounts_[0], calendar_[0], roll_[0], swapFrequency_[0], roll_[0],
                                        "10Y", dayCounts_[0], calendar_[0], 2);
      var iamc = swapPricer as IAmericanMonteCarloAdapter;
      var p0 = new LeastSquaresMonteCarloCcrPricer(swapPricer.Product.Description, input.AsOf, input.AsOf, notional, swapPricer.ValuationCurrency, null,
                                                   null, LeastSquaresMonteCarloCcrUtils.ExerciseEvaluatorFactory(underlier, null, null, false), 
                                                   iamc.DiscountCurves.ToArray(), null, null, null, null);
      p0.Volatilities = input.Volatilities;
      p0.FactorLoadings = input.FactorLoadings;
      p0.RngType = MultiStreamRng.Type.MersenneTwister;
      p0.PathCount = input.Sample;
      return p0;
    }

    private ResultData ToResultData(DataTable dataTable, bool calcGamma, double timeUsed)
    {
      int cols = calcGamma ? 2 : 1;
      int rows = dataTable.Rows.Count;
      var labels = new string[rows];
      var deltas = new double[rows];
      var gammas = new double[rows];
      for (int i = 0; i < rows; i++)
      {
        DataRow row = dataTable.Rows[i];
        labels[i] = string.Format("{0}.{1}", (string) row["InputName"], (string) row["Tenor"]);
        deltas[i] = (double) row["Delta"];
        if (calcGamma)
          gammas[i] = (double) row["Gamma"];
      }
      ResultData rd = LoadExpects();
      rd.Accuracy = 1e-1;
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }
      {
        rd.Results[0].Name = "Delta";
        rd.Results[0].Labels = labels;
        rd.Results[0].Actuals = deltas;
      }
      if (calcGamma)
      {
        rd.Results[1].Name = "Gamma";
        rd.Results[1].Labels = labels;
        rd.Results[1].Actuals = gammas;
      }
      rd.TimeUsed = timeUsed;
      return rd;
    }
    #endregion

    #region Tests
    [Test]
    public void TestSwapCashflowNodes()
    {
     var pricers = input_.Pricers;
     var err = new double[3];
      err[0] = TestSwapCashflowNodes((SwapPricer)pricers[0]);
      err[1] = TestSwapCashflowNodes((SwapPricer)pricers[1]);
      err[2] = TestSwapCashflowNodes((SwapPricer)pricers[2]);
      for (int i = 0; i < 3; ++i)
        Assert.AreEqual(err[i], 0.0, 1e-8);
    }


    [Test]
    public void TestInflationBondCashflowNodes()
    {
      var inflFactor = new InflationFactorCurve(input_.AsOf);
      inflFactor.Add(input_.AsOf, 1.0);
      var inflationCurve = new InflationCurve(input_.AsOf, spotInflation_, inflFactor, null);
      

      var inflPricer = CreateInflationBondPricer(asOf_, input_.DiscountCurves[0], inflationCurve, "10Y", 1.0, 0.05, dayCounts_[0], calendar_[0],
                                                 roll_[0], dayCounts_[0], Frequency.SemiAnnual, roll_[0], calendar_[0], spotInflation_);

      var nodes = ((ICashflowNodesGenerator)inflPricer).Cashflow;
      double pvfromCN = 0.0, pv = inflPricer.Pv();
      for (int i = 0; i < nodes.Count; ++i)
        pvfromCN += nodes[i].RealizedAmount() * nodes[i].RiskyDiscount();
      Assert.AreEqual(pv - pvfromCN, 0.0, 3e-6);
    }

    [Test]
    public void TestCallableSwap()
    {
      SwapPricer sp;  
      var p0 = CreateBermudanSwaptionPricer(100, input_, out sp);
      double pv0 = p0.Pv();
      var iamc = sp as IAmericanMonteCarloAdapter;
      var p1 = new LeastSquaresMonteCarloCcrPricer(sp.Product.Description, input_.AsOf, input_.AsOf, p0.Notional, sp.ValuationCurrency, iamc.Cashflow, null,
                                                   LeastSquaresMonteCarloCcrUtils.ExerciseEvaluatorFactory(new Func<Dt, double>(dt => 0.0), null, null, true), 
                                                   iamc.DiscountCurves.ToArray(), null, null, null, null)
                                                   {
                                                     Volatilities = input_.Volatilities,
                                                     FactorLoadings = input_.FactorLoadings,
                                                     RngType = MultiStreamRng.Type.MersenneTwister,
                                                     PathCount = input_.Sample
                                                   };
      var p2 = new LeastSquaresMonteCarloCcrPricer(sp.Product.Description, input_.AsOf, input_.AsOf, 100.0, sp.ValuationCurrency,
                                                   iamc.Cashflow, null, null, iamc.DiscountCurves.ToArray(), iamc.FxRates.ToArray(),
                                                   null, null, null)
      {
        Volatilities = input_.Volatilities,
        FactorLoadings = input_.FactorLoadings,
        RngType = MultiStreamRng.Type.MersenneTwister,
        PathCount = input_.Sample
      };
      double pv1 = p1.Pv() - p2.Pv();
      Assert.AreEqual((pv0-pv1)/pv1, 0.0, 5e-2);
    }


    [Test]
    public void TestCallableFxSwap()
    {
      SwapPricer sp;
      var p0 = CreateBermudanFxSwaptionPricer(100.0, input_, out sp);
      double pv0 = p0.Pv();
      var iamc = sp as IAmericanMonteCarloAdapter;
      var p1 = new LeastSquaresMonteCarloCcrPricer(sp.Product.Description, input_.AsOf, input_.AsOf, 100.0, sp.ValuationCurrency,
                                                   iamc.Cashflow, null, LeastSquaresMonteCarloCcrUtils.ExerciseEvaluatorFactory(new Func<Dt,double>(dt => 0.0), null, null, true),
                                                   iamc.DiscountCurves.ToArray(), iamc.FxRates.ToArray(), null, null, null)
               {
                 Volatilities = input_.Volatilities,
                 FactorLoadings = input_.FactorLoadings,
                 RngType = MultiStreamRng.Type.MersenneTwister,
                 PathCount = input_.Sample
               };

      var p2 = new LeastSquaresMonteCarloCcrPricer(sp.Product.Description, input_.AsOf, input_.AsOf, 100.0, sp.ValuationCurrency,
                                                   iamc.Cashflow, null, null, iamc.DiscountCurves.ToArray(), iamc.FxRates.ToArray(),
                                                   null, null, null)
                                                   {
                                                     Volatilities = input_.Volatilities,
                                                     FactorLoadings = input_.FactorLoadings,
                                                     RngType = MultiStreamRng.Type.MersenneTwister,
                                                     PathCount = input_.Sample
                                                   };
      double pv1 = p1.Pv() - p2.Pv();
      Assert.AreEqual((pv0 - pv1)/pv0, 0.0, 5e-2);
    }

    [Test]
    public void TestCallableFxSwapRateSensitivities()
    {
      SwapPricer sp;
      var p1 = CreateBermudanFxSwaptionPricer(1e7, input_, out sp);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.RateSensitivities(upBump_, downBump_, bumpRelative_, bumpType_,
                                                 quoteTarget_, bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestCallableFxSwapFXSensitivities()
    {
      SwapPricer sp;
      var p1 = CreateBermudanFxSwaptionPricer(1e7, input_, out sp);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.FxSensitivities(upBump_, downBump_, bumpRelative_,
                                               BumpType.Parallel, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestCallableFxSwapRateVolSensitivities()
    {
      SwapPricer sp;
      var p1 = CreateBermudanFxSwaptionPricer(1e7, input_, out sp);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.RateVolatilitiesSensitivities(1.01, 0.99, true, bumpType_,
                                                                      bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestCallableFxSwapRateFactorSensitivities()
    {
      SwapPricer sp;
      var p1 = CreateBermudanFxSwaptionPricer(1e7, input_, out sp);
      var upBump = new double[nFactors_];
      var downBump = new double[nFactors_];
      for (int i = 0; i < nFactors_; ++i)
      {
        double bump = (i % 2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = -bump;
      }
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.RateFactorsSensitivities(upBump, downBump, false, bumpType_,
                                                        calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestCallableFxSwapFXVolSensitivities()
    {
      SwapPricer sp;
      var p1 = CreateBermudanFxSwaptionPricer(1e7, input_, out sp);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.FxVolatilitiesSensitivities(1.01, 0.99, true, bumpType_,
                                                           bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestCallableFxSwapFXFactorSensitivities()
    {
      SwapPricer sp;
      var p1 = CreateBermudanFxSwaptionPricer(1e7, input_, out sp);
      var upBump = new double[nFactors_];
      var downBump = new double[nFactors_];
      for (int i = 0; i < nFactors_; ++i)
      {
        double bump = (i%2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = -bump;
      }
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.FxFactorsSensitivities(upBump, downBump, false, bumpType_,
                                                      calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    [Test]
    public void TestFxSwap()
    {
      const double notional = 1e7;
      SwapPricer swapPricer;
      var p0 = CreateFxSwapPricer(notional, out swapPricer);
      p0.PathCount = 10000;
      double pv0 = p0.Pv();
      double pv1 = swapPricer.Pv();
      Assert.AreEqual(pv0 - pv1, 0.0, 1e-2 * notional);
    }

    [Test]
    public void TestSwap()
    {
      const double notional = 1e7;
      SwapPricer swapPricer;
      var p0 = CreateSwapPricer(notional, out swapPricer);
      double pv0 = p0.Pv();
      double pv1 = swapPricer.Pv();
      Assert.AreEqual(pv0 - pv1, 0.0, 1e-2 * notional);
    }


    [Test]
    public void TestFxSwapRateSensitivities()
    {
      SwapPricer swapPricer;
      const double notional = 1e7;
      var p1 = CreateFxSwapPricer(notional, out swapPricer);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.RateSensitivities(upBump_, downBump_, bumpRelative_, bumpType_,
                                                 quoteTarget_, bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestFxSwapFXSensitivities()
    {
      SwapPricer swapPricer;
      const double notional = 1e7;
      var p1 = CreateFxSwapPricer(notional, out swapPricer);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.FxSensitivities(upBump_, downBump_, bumpRelative_,
                                               BumpType.Parallel, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestFxSwapRateVolSensitivities()
    {
      SwapPricer swapPricer;
      const double notional = 1e7;
      var p1 = CreateFxSwapPricer(notional, out swapPricer);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.RateVolatilitiesSensitivities(1.01, 0.99, true, bumpType_,
                                                                      bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestFxSwapRateFactorSensitivities()
    {
      SwapPricer swapPricer;
      const double notional = 1e7;
      var p1 = CreateFxSwapPricer(notional, out swapPricer);
      var upBump = new double[nFactors_];
      var downBump = new double[nFactors_];
      for (int i = 0; i < nFactors_; ++i)
      {
        double bump = (i % 2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = -bump;
      }
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.RateFactorsSensitivities(upBump, downBump, false, bumpType_,
                                                        calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestFxSwapFXVolSensitivities()
    {
      SwapPricer swapPricer;
      const double notional = 1e7;
      var p1 = CreateFxSwapPricer(notional, out swapPricer);
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.FxVolatilitiesSensitivities(1.01, 0.99, true, bumpType_,
                                                           bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    [Test]
    public void TestFxSwapFXFactorSensitivities()
    {
      SwapPricer swapPricer;
      const double notional = 1e7;
      var p1 = CreateFxSwapPricer(notional, out swapPricer);
      var upBump = new double[nFactors_];
      var downBump = new double[nFactors_];
      for (int i = 0; i < nFactors_; ++i)
      {
        double bump = (i % 2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = -bump;
      }
      var timer = new Timer();
      timer.Start();
      DataTable dataTable = p1.FxFactorsSensitivities(upBump, downBump, false, bumpType_,
                                                      calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    [Test]
    public void CVACalculations()
    {
      BaseCVACalculations();
    }

    
    [Test]
    public void PathwiseCVACalculationsAMC()
    {
      Timer timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      engine.Execute();
      DataTable expectsTable = FormatResults(engine, netting_);
      IRunSimulationPath pathWiseEngine = CreatePathwiseEngine(input_, null);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.CVA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.DVA, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.EE, 1.0);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.PFE, 0.99);
      pathWiseEngine.AddMeasureAccumulator(CCRMeasure.NEE, 1.0);
                   
      var precalced = pathWiseEngine.PrecalculateExotics();
      var paths = pathWiseEngine.RunSimulationPaths(0, sample_);
      
      DataTable resultsTable = FormatResults(pathWiseEngine);
      timer.Stop();
      ResultData rd = ToResultData(expectsTable, resultsTable, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping(CreateThisInput(nFactors_, 4));
    }


    [Test]
    [Ignore("Unknown.  TODO")]
    public void RateSensitivities()
    {
      BaseRateSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void RateVolSensitivities()
    {
      BaseRateVolSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void RateFactorSensitivities()
    {
      BaseRateFactorSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void FxSensitivities()
    {
      BaseFxSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void FxVolSensitivities()
    {
      BaseFxVolSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void FxFactorSensitivities()
    {
      BaseFxFactorSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void CreditSpreadSensitivities()
    {
      BaseCreditSpreadSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void CreditVolSensitivities()
    {
      BaseCreditVolSensitivities();
    }

    [Test]
    [Ignore("Unknown.  TODO")]
    public void CreditFactorSensitivities()
    {
      BaseCreditFactorSensitivities();
    }
    #endregion
    
  }
}
