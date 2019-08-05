//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Calibrators.BaseCorrelations
{
  /// <summary>
  /// The data is based on spreadsheet under case 18724
  /// </summary>
  [TestFixture]
  class TestStrikeMethods : ToolkitTestBase
  {
    public TestStrikeMethods() : base(null)
    {}

    [OneTimeSetUp]
    public void Initialize()
    {
      // Build IR curve
      #region IR curve
      Dt pricingDate = new Dt(17, 8, 2009);
      DayCount mmDayCount = DayCount.Actual360;
      DayCount swapDayCount = DayCount.Actual360;
      Frequency swapFreq = Frequency.SemiAnnual;
      
      string[] mmTenors = new string[] { "1 Days", "1 Days", "1 Weeks", "1 Months", "2 Months", "3 Months" };
      Dt[] mmTenorDates = new Dt[]{
        new Dt(18, 8, 2009), new Dt(19,  8, 2009), new Dt(24,  8, 2009), new Dt(17,  9, 2009), 
        new Dt(17,10, 2009), new Dt(17, 11, 2009)};
      double[] mmRates = new double[]{
        0.002375, 0.002375, 0.0026, 0.0027875, 0.00315, 0.0043125};
      string[] swapTenors = new string[] { "1 Yr", "2 Yr", "3 Yr", "4 Yr", "5 Yr", "6 Yr", "7 Yr", "8 Yr", "9 Yr" };
      Dt[] swapTenorDates = new Dt[]{
        new Dt(17,8,2010),new Dt(17,8,2011),new Dt(17,8,2012),new Dt(17,8,2013),new Dt(17,8, 2014),
        new Dt(17,8,2015),new Dt(17,8,2016),new Dt(17,8,2017),new Dt(17,8,2018)};
      double[] swapRates = new double[]{
        0.0073, 0.01425, 0.02028, 0.02479, 0.02819, 0.03089, 0.03302, 0.03466, 0.03593
      };
      BaseEntity.Toolkit.Numerics.InterpMethod interpMethod = BaseEntity.Toolkit.Numerics.InterpMethod.Weighted;
      BaseEntity.Toolkit.Numerics.ExtrapMethod extrapMethod = BaseEntity.Toolkit.Numerics.ExtrapMethod.Const;
      BaseEntity.Toolkit.Numerics.InterpMethod swapInterp = BaseEntity.Toolkit.Numerics.InterpMethod.Cubic;
      BaseEntity.Toolkit.Numerics.ExtrapMethod swapExtrap = BaseEntity.Toolkit.Numerics.ExtrapMethod.Const;
      DiscountBootstrapCalibrator calibrator =
        new DiscountBootstrapCalibrator(pricingDate, pricingDate);
      calibrator.SwapInterp = BaseEntity.Toolkit.Numerics.InterpFactory.FromMethod(swapInterp, swapExtrap);
      calibrator.FuturesCAMethod = FuturesCAMethod.Hull;
      irCurve_ = new DiscountCurve(calibrator);
      irCurve_.Interp = BaseEntity.Toolkit.Numerics.InterpFactory.FromMethod(interpMethod, extrapMethod);
      irCurve_.Ccy = Currency.USD;
      irCurve_.Category = "None";
      for (int i = 0; i < mmTenorDates.Length; i++)
        irCurve_.AddMoneyMarket(mmTenors[i], mmTenorDates[i], mmRates[i], mmDayCount);
      for (int i = 0; i < swapTenors.Length; i++)
        irCurve_.AddSwap(swapTenors[i], swapTenorDates[i], swapRates[i], swapDayCount,
                       swapFreq, BDConvention.None, Calendar.None);      
      irCurve_.Fit();
      #endregion IR curve

      // Get the survival curves with defaulted status
      #region Survival Curves
      string fileName = GetTestFilePath(creditDataFile_);
      CreditData cd = (CreditData)XmlLoadData(fileName, typeof(CreditData));
      survivalCurves_ = cd.GetSurvivalCurves(irCurve_);
      if (this.WithDefaultedNames)
      {
        for (int i = 0; i < defaultIndex.Length; i++)
        {
          survivalCurves_[defaultIndex[i]].SetDefaulted(defaultDates[i], true);
          if (defaultSettleDates[i].IsValid())
            survivalCurves_[i].SurvivalCalibrator.RecoveryCurve.JumpDate= defaultSettleDates[i];
        }
      }
      #endregion Survival Curves

      // Get the single CDX 
      #region CDX
      cdx_ = new CDX(effectiveDate_, maturity_, ccy_, dealPreium_/10000.0, dayCount_, freq_, roll_, cal_);
      #endregion CDX
    }

    #region Tests
    [Test, Smoke]
    public void TestExpectedLossRatio()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.ExpectedLossRatio;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // Tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      // Test less than unity
      TestLessThanUnity(pricers[pricers.Length-1], strikes, strikeMethod);

      // Test unity of CDO(0, 100%)
      TestCDO100(pricers, strikes, strikeMethod);      

      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);      

      // Test exhausted CDO's strike
      TestExhausticCDO(bco, pricers[pricers.Length-1], strikeMethod);
      return;
    }

    [Test, Smoke]
    public void TestExpectedLoss()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.ExpectedLoss;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // First do the tie outs, these can be put into a common function      
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      TestCDO100(pricers, strikes, strikeMethod);

      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);

      // Test exhausted CDO's strike
      TestExhausticCDO(bco, pricers[pricers.Length-1], strikeMethod);
    }

    [Test, Smoke]
    public void TestExpectedLossPvRatio()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.ExpectedLossPvRatio;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      // Test less than unity
      TestLessThanUnity(pricers[pricers.Length - 1], strikes, strikeMethod);

      // Test unity of CDO(0, 100%)
      TestCDO100(pricers, strikes, strikeMethod);

      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);

      // Test exhausted CDO's strike
      TestExhausticCDO(bco, pricers[pricers.Length-1], strikeMethod);
      return;
    }

    [Test, Smoke]
    public void TestExpectedLossPv()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.ExpectedLossPV;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      // Test unity of CDO(0, 100%)
      TestCDO100(pricers, strikes, strikeMethod);

      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);

      // Test exhausted CDO's strike
      TestExhausticCDO(bco, pricers[pricers.Length - 1], strikeMethod);
      return;
    }

    [Test, Smoke]
    public void TestProtection()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.Protection;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      TestCDO100(pricers, strikes, strikeMethod);

      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);

      // Test exhausted CDO's strike
      TestExhausticCDO(bco, pricers[pricers.Length - 1], strikeMethod);
    }

    [Test, Smoke]
    public void TestProtectionPv()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.ProtectionPv;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      TestCDO100(pricers, strikes, strikeMethod);

      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);

      // Test exhausted CDO's strike
      TestExhausticCDO(bco, pricers[pricers.Length - 1], strikeMethod);
    }

    [Test, Smoke]
    public void TestEquityProtection()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.EquityProtection;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      TestCDO100(pricers, strikes, strikeMethod);

      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);

      // Test exhausted CDO's strike
      TestExhausticCDO(bco, pricers[pricers.Length - 1], strikeMethod);
    }

    [Test, Smoke]
    public void TestEquitySpread()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.EquitySpread;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);

      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);

      // Test descending order of strikes
      TestMonotonicity(strikes, strikeMethod);
    }

    [Test, Smoke]
    public void TestSeniorSpread()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.EquitySpread;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);
      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);
      // Test descending order of strikes
      TestMonotonicity(strikes, strikeMethod);
    }

    [Test, Smoke]
    public void TestProbability()
    {
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.EquitySpread;
      BaseCorrelationTermStruct bco = Calibrate(strikeMethod);
      SyntheticCDO[] cdos;
      SyntheticCDOPricer[] pricers;
      GetCDOPricers(bco, out cdos, out pricers, strikeMethod);
      // Strikes tie out
      double[] strikes = TieOutStrikes(pricers, bco, strikeMethod);
      // Test ascending order of strikes
      TestMonotonicity(strikes, strikeMethod);

      TestCDO100(pricers, strikes, strikeMethod);

      TestExhausticCDO(bco, pricers[pricers.Length - 1], strikeMethod);
    }

    #endregion Tests

    #region Helper

    private void SetCopula(BaseCorrelationParam par, string copula,
      int dfCommon, int dfIdiosyn, double min, double max)
    {
      BaseCorrelation bc = new BaseCorrelation(BaseCorrelationMethod.ArbitrageFree,
         BaseCorrelationStrikeMethod.Unscaled, new double[1], new double[1]);
      bc.Extended = max > 1;
      par.Copula = new Copula(CopulaType.ExtendedGauss);
      par.CopulaType = par.Copula.CopulaType;
      par.DfCommon = dfCommon;
      par.DfIndividual = dfIdiosyn;
      par.Max = max;
      par.Min = min;
      return;
    }
    
    private void BuildBCParams()
    {
      double accuracy = 0.0;
      int quadPoints = BasketPricerFactory.GetQuadraturePoints(ref accuracy);
      SetCopula(par_, "ExtendedGauss", 0, 0, 0, 2);
      par_.StepSize = 3;
      par_.StepUnit = TimeUnit.Months;
      par_.QuadraturePoints = quadPoints;
      par_.AccuracyLevel = accuracy;
      par_.GridSize = 0;
      par_.ToleranceF = 0;
      par_.ToleranceX = 0;
      par_.StrikeInterp = BaseEntity.Toolkit.Numerics.InterpMethod.PCHIP;
      par_.StrikeExtrap = BaseEntity.Toolkit.Numerics.ExtrapMethod.Smooth;
      par_.TenorInterp = BaseEntity.Toolkit.Numerics.InterpMethod.Linear;
      par_.TenorExtrap = BaseEntity.Toolkit.Numerics.ExtrapMethod.Const;
      par_.Min = 0;
      par_.Max = 2.0;
      par_.Model = Toolkit.Pricers.Baskets.BasketModelType.SemiAnalytic;
      par_.InterpOnFactors = false;
      par_.BottomUp = false;
      par_.Method = BaseCorrelationMethod.ArbitrageFree;
    }

    private BaseCorrelationTermStruct Calibrate(BaseCorrelationStrikeMethod strikeMethod)
    {
      BaseCorrelationTermStruct bco = BaseCorrelationFactory.BaseCorrelationFromMarketQuotes(
        bcCalibmethod_, strikeMethod, scalingCalibrator_, runningPremium_, detach_, trancheQuotes_, new CDX[] { cdx_ },
        tracheMaturity_, null, survivalCurves_, par_);
      return bco;
    }

    private double GetExhaustedDetachment()
    {
      int n = survivalCurves_.Length;
      double maxLoss = 0;
      for (int i = 0; i < n; i++)
      {
        double loss = (1 - survivalCurves_[i].SurvivalCalibrator.RecoveryCurve.Points[0].Value);
        if (maxLoss < loss)
          maxLoss = loss;
      }
      return maxLoss;
    }

    private void GetCDOPricers(BaseCorrelationTermStruct bco, 
      out SyntheticCDO[] cdos, out SyntheticCDOPricer[] pricers, BaseCorrelationStrikeMethod strikeMethod)
    {
      
      double[] attachments = null;
      double[] detachments = null;
      double[] premia = null;
      double[] fees = null;
      double[] notionals = null; 

      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionPv:
          attachments = new double[] { 0.0, 0.0, 0.0, 0.0 };
          detachments = new double[] { 0.15, 0.25, 0.35, 1.0 };
          premia = new double[] { 0, 500, 500, 500 };
          fees = new double[] { 0, 0, 0, 0 };
          notionals = Array.ConvertAll<double, double>(detachments, x => x * totalNotional_);
          break;
        case BaseCorrelationStrikeMethod.SeniorSpread:
          attachments = new double[] { 0.15, 0.25, 0.35 };
          detachments = new double[] { 1.0, 1.0, 1.0 };
          premia = new double[] { 0, 500, 500};
          fees = new double[] { 0, 0, 0 };
          notionals = Array.ConvertAll<double, double>(attachments, x => (1-x) * totalNotional_);
          break;
      }
      cdos = BuildCDO(attachments, detachments, premia, fees, notionals);
      pricers = BuildCDOPricers(cdos, notionals, bco);
      return;
    }

    private SyntheticCDO[] BuildCDO(double[] attach, double[] detach, double[] premia, double[] fees, double[] notional)
    {
      SyntheticCDO[] cdos = new SyntheticCDO[attach.Length];
      for (int i = 0; i < attach.Length; i++)
      {
        cdos[i] = new SyntheticCDO(effectiveDate_, maturity_, ccy_, premia[i] / 10000.0, dayCount_, freq_, roll_, cal_);
        cdos[i].FirstPrem = Dt.Empty;//Schedule.DefaultFirstCouponDate(effectiveDate_, freq_, maturity_, false);
        cdos[i].LastPrem = Dt.Empty;//Schedule.DefaultLastCouponDate(Dt.Empty, freq_, maturity_, false);
        cdos[i].Attachment = attach[i];
        cdos[i].Detachment = detach[i];
        cdos[i].Notional = notional[i];
        cdos[i].CdoType = CdoType.Unfunded;
        if (fees[i] != 0.0)
        {
          cdos[i].Fee = fees[i];
          cdos[i].FeeSettle = effectiveDate_;
        }
      }
      return cdos;
    }
    
    private SyntheticCDOPricer[] BuildCDOPricers(SyntheticCDO[] cdos, double[] notionals, BaseCorrelationTermStruct bc)
    {
      Copula copulaObj = new Copula(CopulaType.ExtendedGauss);
      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[cdos.Length];
      
      pricers = BasketPricerFactory.CDOPricerSemiAnalytic(cdos, new Dt(), asOf_, settle_, irCurve_, new Dt[]{maturity_}, 
        survivalCurves_, null, copulaObj, bc, 3, TimeUnit.Months, 0, 0, notionals, false, false, null);
      return pricers;
    }

    private Dt[] GetLossDates()
    {
      // Get the loss dates between settle date and the maturity
      List<Dt> dates = new List<Dt>();
      dates.Add(settle_);
      Dt dt = settle_;
      while (dt <= maturity_)
      {
        dt = Dt.Add(dt, 3, TimeUnit.Months);
        if (dt < maturity_)
          dates.Add(dt);
        else
          dates.Add(maturity_);
      }
      return dates.ToArray();
    }

    private double ComputeBasketLossOrPv(SyntheticCDOPricer portfolioPricer, bool needPv)
    {
      Dt[] lossDates = GetLossDates();
      double[] lossToDates = new double[lossDates.Length];
      double[] discFac = Array.ConvertAll<Dt, double>(lossDates, d => irCurve_.DiscountFactor(asOf_, d));
      lossToDates = Array.ConvertAll<Dt, double>(lossDates, d => portfolioPricer.LossToDate(d));
      int[] index = new int[lossDates.Length - 1];
      for (int i = 0; i < index.Length; i++) index[i] = i;
      double[] periodLoss = Array.ConvertAll<int, double>(index, i => lossToDates[i + 1] - lossToDates[i]);
      double[] averDiscFac = Array.ConvertAll<int, double>(index, i => 0.5 * (discFac[i] + discFac[i + 1]));
      double basketLossOrPv = 0;
      for (int i = 0; i < index.Length; i++)
        basketLossOrPv += (periodLoss[i] * (needPv ? averDiscFac[i] : 1.0));
      return basketLossOrPv;
    }

    private bool IsAsending(double[] strikes)
    {
      int n = strikes.Length;
      for (int i = 1; i < n; i++)
        if (strikes[i] < strikes[i - 1])
          return false;
      return true;
    }

    private bool IsDesending(double[] strikes)
    {
      int n = strikes.Length;
      for (int i = 1; i < n; i++)
        if (strikes[i] > strikes[i - 1])
          return false;
      return true;
    }

    private void TestMonotonicity(double[] strikes, BaseCorrelationStrikeMethod strikeMethod)
    {
      bool isMonotomic = false;
      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.Probability:
          isMonotomic = IsAsending(strikes);
          Assert.AreEqual(true, isMonotomic, 
            "Strike Monotonicity Test Failed for "+strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
          isMonotomic = IsDesending(strikes);
          Assert.AreEqual(true, isMonotomic,
            "Strike Monotonicity Test Failed for " + strikeMethod.ToString());
          break;
      }
      return;
    }

    private void TestLessThanUnity(SyntheticCDOPricer pricer, double[] strikes, BaseCorrelationStrikeMethod strikeMethod)
    {
      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
          bool[] lessThanUnity = Array.ConvertAll<double, bool>(Toolkit.Base.Utils.Round(strikes, 5), s => s <= 1.0);
          for (int i = 0; i < lessThanUnity.Length; i++)
            Assert.AreEqual(true, lessThanUnity[i], "Strike is larger than unity for " + strikeMethod.ToString());
          break;
      }
    }
    
    private double[] TieOutStrikes(SyntheticCDOPricer[] pricers, 
      BaseCorrelationTermStruct bco, BaseCorrelationStrikeMethod strikeMethod)
    {
      double[] strikes = new double[pricers.Length];
      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
          double[] expectedLosses = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p => p.ExpectedLoss());
          strikes = Array.ConvertAll<double, double>(expectedLosses, x => x / expectedLosses[expectedLosses.Length - 1]);
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
          double basketLossPv = ComputeBasketLossOrPv(pricers[pricers.Length - 1], true);
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p=>-p.ProtectionPv()/basketLossPv);
          break;
        case BaseCorrelationStrikeMethod.ExpectedLoss:
          double basketLoss = ComputeBasketLossOrPv(pricers[pricers.Length - 1], false);
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p => p.CDO.Detachment * totalNotional_ / basketLoss);
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
          basketLossPv = ComputeBasketLossOrPv(pricers[pricers.Length - 1], true);
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p => p.CDO.Detachment * totalNotional_ / basketLossPv);
          break;
        case BaseCorrelationStrikeMethod.Protection:
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p => p.LossToDate(maturity_)/totalNotional_);
          break;
        case BaseCorrelationStrikeMethod.ProtectionPv:
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p => -p.ProtectionPv() / totalNotional_);
          break;
        case BaseCorrelationStrikeMethod.EquityProtection:
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(
            pricers, p => p.LossToDate(maturity_) / totalNotional_ / p.CDO.Detachment);
          break;
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(
            pricers, p => -p.ProtectionPv() / totalNotional_ / p.CDO.Detachment);
          break;
        case BaseCorrelationStrikeMethod.EquitySpread:
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p => p.BreakEvenPremium());
          break;
        case BaseCorrelationStrikeMethod.SeniorSpread:
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(pricers, p => p.BreakEvenPremium());
          break;
        case BaseCorrelationStrikeMethod.Probability:
          strikes = Array.ConvertAll<SyntheticCDOPricer, double>(
            pricers, p => p.Basket.CalcLossDistribution(true, maturity_, new double[] { 0.0, p.CDO.Detachment })[1, 1]);          
          break;
      }

      for (int i = 0; i < strikes.Length - 1; i++)
        Assert.AreEqual(strikes[i], 
          bco.BaseCorrelations[0].Strikes[i], 6e-3, "Strike TieOut failed for " + strikeMethod.ToString());

      return strikes;
    }

    private void TestExhausticCDO(BaseCorrelationTermStruct bco, 
      SyntheticCDOPricer portfolioPricer, BaseCorrelationStrikeMethod strikeMethod)
    {
      double det = GetExhaustedDetachment();
      SyntheticCDO[] cdos = 
        BuildCDO(new double[] { 0 }, new double[] { det }, new double[] { 100 }, new double[] { 0 }, new double[] { totalNotional_ });
      SyntheticCDOPricer pricer = BuildCDOPricers(cdos, new double[] { totalNotional_ * det}, bco)[0];
      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.Probability:
          Assert.AreEqual(1.0, ((BaseCorrelationBasketPricer)pricer.Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not 1 for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.ExpectedLoss:
          double basketLoss = ComputeBasketLossOrPv(portfolioPricer, false);
          Assert.AreEqual(totalNotional_ * det/basketLoss, ((BaseCorrelationBasketPricer)pricer.Basket).DPStrike, 
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
          double basketLossPv = ComputeBasketLossOrPv(portfolioPricer, true);
          Assert.AreEqual(totalNotional_ * det / basketLossPv, ((BaseCorrelationBasketPricer)pricer.Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.Protection:
          double exhaustedCDOLoss = pricer.LossToDate(maturity_);
          Assert.AreEqual(exhaustedCDOLoss / totalNotional_, ((BaseCorrelationBasketPricer)pricer.Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.ProtectionPv:
          double protectionPv = -pricer.ProtectionPv();
          Assert.AreEqual(protectionPv / totalNotional_, ((BaseCorrelationBasketPricer)pricer.Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.EquityProtection:
          double strike = pricer.LossToDate(maturity_) / totalNotional_ / pricer.CDO.Detachment;
          Assert.AreEqual(strike, ((BaseCorrelationBasketPricer)pricer.Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
          strike = -pricer.ProtectionPv() / totalNotional_ / pricer.CDO.Detachment;
          Assert.AreEqual(strike, ((BaseCorrelationBasketPricer)pricer.Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
      }      
    }
    
    private void TestCDO100(SyntheticCDOPricer[] pricers, double[] strikes, BaseCorrelationStrikeMethod strikeMethod)
    {
      switch (strikeMethod)
      {
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.Probability:
          Assert.AreEqual(1.0, strikes[strikes.Length - 1], 1e-5, "CDO(0, 100%) strike is not 1 for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.ExpectedLoss:
          double basketLoss = ComputeBasketLossOrPv(pricers[pricers.Length - 1], false);
          Assert.AreEqual(totalNotional_/basketLoss, strikes[strikes.Length - 1], 1e-5, 
            "CDO(0, 100%) strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
          double basketLossPv = ComputeBasketLossOrPv(pricers[pricers.Length - 1], true);
          Assert.AreEqual(totalNotional_ / basketLossPv, strikes[strikes.Length - 1], 1e-5,
            "CDO(0, 100%) strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.Protection:
          double loss = pricers[pricers.Length-1].LossToDate(maturity_);
          Assert.AreEqual(loss / totalNotional_, ((BaseCorrelationBasketPricer)pricers[pricers.Length - 1].Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
        case BaseCorrelationStrikeMethod.ProtectionPv:
          double protectionPv = -pricers[pricers.Length - 1].ProtectionPv();
          Assert.AreEqual(protectionPv / totalNotional_, ((BaseCorrelationBasketPricer)pricers[pricers.Length - 1].Basket).DPStrike,
            1e-5, "Exhausted CDO strike is not correct for " + strikeMethod.ToString());
          break;
      }
    }
    
    #endregion Helper

    public bool WithDefaultedNames { get; set; } = true;

    #region data
    // Credit Curves Data
    private string creditDataFile_ = "data/CreditData_Strike_ExpectedLossRatio.xml";
    int[] defaultIndex = new int[13]{0, 16, 18, 42, 50, 55, 68, 71, 78, 85, 86, 90, 97};
    Dt[] defaultDates = new Dt[]{new Dt(27, 3, 2009), new Dt(19, 3, 2009), new Dt(27, 3, 2009), new Dt(1, 6, 2009), 
      new Dt(31, 3, 2009), new Dt(2, 7, 2009), new Dt(14, 1, 2009), new Dt(15, 6, 2009), new Dt(18, 5, 2009), 
      new Dt(26, 1, 2009), new Dt(4, 3, 2009), new Dt(9, 12, 2008), new Dt(28, 5, 2009)};
    Dt[] defaultSettleDates = new Dt[] {new Dt(24, 4, 2009), new Dt(21, 4, 2009), new Dt(28, 4, 2009), new Dt(18, 6, 2009),
      new Dt(1, 5, 2009), new Dt(28, 7, 2009), new Dt(18, 2, 2009), new Dt(16, 7, 2009), new Dt(18, 6, 2009), new Dt(26, 2, 2009),
      new Dt(7, 4, 2009), new Dt(16, 1, 2009), new Dt(1, 7, 2009)};

    DiscountCurve irCurve_ = null;
    SurvivalCurve[] survivalCurves_ = null;

    private Dt asOf_ = new Dt(17, 8, 2009);
    private Dt settle_ = new Dt(18, 8, 2009);
    double[] marketQuote = new double[] { 790.3 };

    Currency ccy_ = Currency.USD;
    DayCount dayCount_ = DayCount.Actual360;
    Frequency freq_ = Frequency.Quarterly;
    BDConvention roll_ = BDConvention.Following;
    Calendar cal_ = Calendar.NYB;
    Dt effectiveDate_ = new Dt(28, 3, 2008);
    Dt maturity_ = new Dt(20, 6, 2011);
    double dealPreium_ = 500.0;
    CDX cdx_ = null;
    double totalNotional_ = 100000000.0;
    BaseCorrelationParam par_ = null;
    BaseCorrelationCalibrationMethod bcCalibmethod_ = BaseCorrelationCalibrationMethod.MaturityMatch;    
    IndexScalingCalibrator scalingCalibrator_ = null;
    double[,] runningPremium_ = new double[3,1]{{0}, {500}, {500}};
    double[] detach_ = new double[]{0.15, 0.25, 0.35};
    double[,] trancheQuotes_ = new double[3,1] { {0.49875}, {0.19750}, {0.0625} };
    Dt[] tracheMaturity_ = new Dt[] { new Dt(20, 6, 2011) };

    #endregion data
  }
}