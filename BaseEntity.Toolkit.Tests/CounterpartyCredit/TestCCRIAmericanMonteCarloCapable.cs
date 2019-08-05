//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRIAmericanMonteCarloCapable : TestCCRBase
  {
    #region Data
    private int nFactors_ = 5;
    private int sample_ = 5000;
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
      var retVal = CreateInput(nFactors, sample, Tenor.Empty, currencies_.Length, 2, false, false, false, false);
      var pricers =  retVal.Pricers;
      var newPricers = new IPricer[retVal.Pricers.Length + 2];
      pricers.CopyTo(newPricers, 0);
      var vanillaBermudan = CreateSwapPricer(asOf_, 1e7, retVal.DiscountCurves[0], indexTenor_[0], dayCounts_[0], calendar_[0], roll_[0], swapFrequency_[0],
                                             roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);

      var xCcyBermudan = CreateXccySwapPricer(retVal.AsOf, 1e7, retVal.FxRates[0], retVal.DiscountCurves[0], retVal.DiscountCurves[1], indexTenor_[1],
                                              dayCounts_[1], calendar_[1], roll_[1], swapFrequency_[0], roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
      var op0 = new List<IOptionPeriod>();
      var op1 = new List<IOptionPeriod>();
      op0.Add(new CallPeriod(retVal.AsOf, Dt.Add(vanillaBermudan.Product.Maturity, 1), 1.0, 1.0, OptionStyle.Bermudan, 0));
      op1.Add(new CallPeriod(retVal.AsOf, Dt.Add(xCcyBermudan.Product.Maturity, 1), 1.0, 1.0, OptionStyle.Bermudan, 0));
      vanillaBermudan.Swap.ExerciseSchedule = op0;
      xCcyBermudan.Swap.ExerciseSchedule = op1;
      vanillaBermudan.Swap.HasOptionRight = true;
      xCcyBermudan.Swap.HasOptionRight = true;
      newPricers[newPricers.Length - 2] = vanillaBermudan;
      newPricers[newPricers.Length - 1] = xCcyBermudan;
      retVal.Pricers = newPricers;
      retVal.Names = ArrayUtil.Generate(newPricers.Length, (i) => (i % 2 == 0) ? "A" : "B"); //2 netting groups
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
                                                         input.CptyRec, input.TenorDates, input.DiscountCurves,
                                                         input.FxRates, input.FwdCurves, input.CreditCurves, 
                                                         input.Volatilities, input.FactorLoadings,  -100.0, input.Sample, input.SimulDates,
                                                         input.GridSize, false);
    }

    #endregion


    #region Tests
    [Test]
    public void CVACalculations()
    {
      BaseCVACalculations();
    }

    [Test]
    public void CvaHedges()
    {
      BaseTestHedgeNotionals(CreateThisInput(nFactors_, sample_));
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
      pathWiseEngine.PrecalculateExotics();
      pathWiseEngine.RunSimulationPaths(0, sample_);
      DataTable resultsTable = FormatResults(pathWiseEngine);
      timer.Stop();
      ResultData rd = ToResultData(expectsTable, resultsTable, timer.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void TestBermudanSwaptionTreeVsAMC()
    {
      string[] expiries = {
                            "1M", "2M", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "15Y", "20Y", "25Y",
                            "30Y"
                          };
      Dt asOf = asOf_;
      var resets = new RateResets(0.04, 0.0);
      var dc = new DiscountCurve(asOf_, 0.04);
      dc.Ccy = input_.DiscountCurves[0].Ccy;
      var ten = Array.ConvertAll(expiries, Tenor.Parse);
      var vols = Array.ConvertAll(ten, t => new VolatilityCurve(asOf, 0.35));
      var correlation = new FactorLoadingCollection(new[] {"USD"}, ten);
      var corr = new double[ten.Length, 1];
      for (int i = 0; i < ten.Length; ++i)
        corr[i, 0] = 1.0; //one factor model
      correlation.AddFactors(dc, corr);
      var ccrVols = new VolatilityCollection(ten);
      ccrVols.Add(dc, vols);
      //CcrCalibrationUtils.FromForwardVolatilitySurface(dc, volatilitySurface, correlation, ref ccrVols);
      var pricer = CreateSwapPricer(asOf, 1000, dc, indexTenor_[0], dayCounts_[0], calendar_[0], roll_[0], swapFrequency_[0], roll_[0], "10Y",
                                    dayCounts_[0], calendar_[0], 2);
      pricer.ReceiverSwapPricer.RateResets = resets;
      pricer.PayerSwapPricer.RateResets = resets;
      pricer.Swap.ExerciseSchedule = new List<IOptionPeriod> {new PutPeriod(pricer.Settle, pricer.Swap.Maturity, 1.0, OptionStyle.Bermudan)};
      var swap = pricer.Swap;
      swap.OptionRight = OptionRight.RightToEnter;
      var treePricer = new SwapBermudanBgmTreePricer(swap, asOf, asOf, dc, dc, null, vols[0]) { Notional = pricer.Notional, AmcNoForwardValueProcess = true };
      double treePv = treePricer.Pv();
      var amcPricer = PortfolioData.GetAmcAdapter(treePricer);
      var mcPricer = new LeastSquaresMonteCarloCcrPricer(pricer.Swap.Description, asOf, asOf, amcPricer.Notional, dc.Ccy, amcPricer.Cashflow,
                                                         amcPricer.CallEvaluator, amcPricer.PutEvaluator,amcPricer.DiscountCurves.ToArray(), null,
                                                         null, null, null);
      mcPricer.RngType = MultiStreamRng.Type.MersenneTwister;
      mcPricer.Volatilities = ccrVols;
      mcPricer.FactorLoadings = correlation;
      mcPricer.PathCount = 20000;
      double mcPv = mcPricer.Pv();
      Assert.AreEqual(0.0, (mcPv - treePv)/treePricer.Notional, 2.5e-2);
    }


    [Test]
    public void TestBermudanSwaptionTreeVsAMCWithPayment()
    {
      string[] expiries = {
                            "1M", "2M", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "15Y", "20Y", "25Y",
                            "30Y"
                          };
      Dt asOf = asOf_;
      var resets = new RateResets(0.04, 0.0);
      var dc = new DiscountCurve(asOf_, 0.04);
      dc.Ccy = input_.DiscountCurves[0].Ccy;
      var ten = Array.ConvertAll(expiries, Tenor.Parse);
      var vols = Array.ConvertAll(ten, t => new VolatilityCurve(asOf, 0.35));
      var correlation = new FactorLoadingCollection(new[] { "USD" }, ten);
      var corr = new double[ten.Length, 1];
      for (int i = 0; i < ten.Length; ++i)
        corr[i, 0] = 1.0; //one factor model
      correlation.AddFactors(dc, corr);
      var ccrVols = new VolatilityCollection(ten);
      ccrVols.Add(dc, vols);

      var expiry = Dt.Add(asOf, "1Y");
      var pricer = CreateSwapPricer(expiry, 10000000, dc, indexTenor_[0], dayCounts_[0], calendar_[0], roll_[0], swapFrequency_[0], roll_[0], "10Y",
                                    dayCounts_[0], calendar_[0], 2);
      var extraPayment = new UpfrontFee(expiry, 1000000 / pricer.DiscountCurve.Interpolate(expiry), pricer.Swap.Ccy);
      pricer.ReceiverSwapPricer.RateResets = resets;
      pricer.PayerSwapPricer.RateResets = resets;

      
      var swaption = new Swaption(asOf, expiry, pricer.Swap.Ccy, pricer.Swap.ReceiverLeg, pricer.Swap.PayerLeg,
       2, PayerReceiver.Payer, OptionStyle.American, 0){SettlementType = SettlementType.Physical};
      var exerciseSchedule = new List<IOptionPeriod>(); // { new PutPeriod(pricer.Settle, expiry, 1.0, OptionStyle.American) };
      var treePricer = new SwapBermudanBgmTreePricer(swaption, asOf, asOf, dc, dc, null, exerciseSchedule , vols[0]) { Notional = pricer.Notional, Payment = extraPayment};
      double treePv = treePricer.Pv();
      var amcPricer = PortfolioData.GetAmcAdapter(treePricer);
      var cashflow = amcPricer.Cashflow ?? new List<ICashflowNode>();
      extraPayment.Scale(1.0 / amcPricer.Notional);
      cashflow.Add(extraPayment.ToCashflowNode(1, pricer.DiscountCurve, null));
      var mcPricer = new LeastSquaresMonteCarloCcrPricer("American Swaption", asOf, asOf, amcPricer.Notional, dc.Ccy, cashflow,
                                                         amcPricer.CallEvaluator, amcPricer.PutEvaluator, amcPricer.DiscountCurves.ToArray(), null,
                                                         null, null, null);
      mcPricer.RngType = MultiStreamRng.Type.MersenneTwister;
      mcPricer.Volatilities = ccrVols;
      mcPricer.FactorLoadings = correlation;
      mcPricer.PathCount = 20000;
      double mcPv = mcPricer.Pv();
      Assert.AreEqual(0.0, (mcPv - treePv) / treePricer.Notional, 1e-2);
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

    [Test]
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping(CreateThisInput(nFactors_, 10));
    }

    #endregion

  }
}
