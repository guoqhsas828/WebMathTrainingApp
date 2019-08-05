// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestCdxTrancheConsistent
  {
    [OneTimeSetUp]
    public void SetUp()
    {
      _discountCurve = new RateCurveBuilder().CreateDiscountCurve(_asOf);
      _survivalCurves = new RateCurveBuilder().CreateSurvivalCurves(_asOf, 0);
      _cdx = GetCdx();
      _cdx.Weights = ArrayUtil.NewArray(_survivalCurves.Length, 1.0/_survivalCurves.Length);
      _tempConfig = new ConfigItems
      {
        {"BasketPricer.ExactJumpToDefault", true}
      }.Update();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
      _tempConfig?.Dispose();
      _tempConfig = null;
    }


    [Test]
    public void TestDiffs()
    {
      var corr = CreateBaseCorrelation(BaseCorrelationStrikeMethod.ExpectedLossPV) 
        as CorrelationObject;
      var cdoPricers = GetCdoPricers(_asOf, _settle, _survivalCurves, corr, _detachments,
        false);
      for (int i = 1; i < cdoPricers.Length; ++i)
      {
        var pv = cdoPricers[i].Pv();
        var accrued = cdoPricers[i].Accrued();
        var pvMinusAccrued = pv - accrued;
        var diff = pvMinusAccrued + _quotes[i, 0];

        Assert.AreEqual(0.0, diff, 1E-11);
      }
    }


    [TestCase(BaseCorrelationStrikeMethod.UnscaledForward)]
    [TestCase(BaseCorrelationStrikeMethod.ExpectedLossForward)]
    [TestCase(BaseCorrelationStrikeMethod.EquityProtectionForward)]
    [TestCase(BaseCorrelationStrikeMethod.EquityProtectionPvForward)]
    [TestCase(BaseCorrelationStrikeMethod.ExpectedLossPVForward)]
    [TestCase(BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward)]
    [TestCase(BaseCorrelationStrikeMethod.ExpectedLossRatioForward)]
    [TestCase(BaseCorrelationStrikeMethod.ProtectionForward)]
    [TestCase(BaseCorrelationStrikeMethod.ProtectionPvForward)]
    public void TestForwardStrikeMappingMethodsRoundTrip(BaseCorrelationStrikeMethod strikeMethod)
    {
      //Clone the original curve
      var sCurves = _survivalCurves.CloneObjectGraph();

      //Set the first curve default.
      var firstCurve = sCurves.First();
      var firstRecoveryCurve = firstCurve.SurvivalCalibrator.RecoveryCurve;
      firstCurve.SetDefaulted(_asOf, true);
      firstRecoveryCurve.JumpDate = _asOf;

      // Correlation
      var corr = CreateBaseCorrelation(strikeMethod) as CorrelationObject;

      // Create a pricer including the default names
      var pricers1 = GetCdoPricers(_asOf, _settle, sCurves, corr, _detachments, false);

      // Create a basket removing the default names.
      const int howManyNamesRemoved = 1;
      var sCurvesRemoveOne = new RateCurveBuilder().CreateSurvivalCurves(_asOf, howManyNamesRemoved);

      //Adjust the detachment points
      var recoveryRate =  firstRecoveryCurve.RecoveryRate(_asOf);
      var weight = 1.0/_survivalCurves.Length;
      var length = _detachments.Length;
      var dps = new Double[length];
      for (int i = 0; i < length; i++)
      {
        var loss = (1 - recoveryRate)*weight;
        dps[i] = (_detachments[i] - loss)/(1 - loss - recoveryRate*weight);
      }

      // Create a pricer excluding the default names
      var pricers2 = GetCdoPricers(_asOf, _settle, sCurvesRemoveOne, corr, dps, true);

      // Test
      for (int i = 0; i < pricers1.Length; i++)
      {
        var pv1 = pricers1[i].Pv();
        var pv2 = pricers2[i].Pv();

        Assert.AreEqual(pv1, pv2, 1E-14);
      }
    }


    #region Pricer

    private static SyntheticCDOPricer[] GetCdoPricers(Dt asOf, Dt settle,
      SurvivalCurve[] survivalCurves, CorrelationObject corr, double [] dps, 
      bool adjustNotional)
    {
      var retVal = new List<SyntheticCDOPricer>();
      SyntheticCDO cdo;
      var accuracy = 0.0;
      var copula = new Copula();
      var quadPoints = BasketPricerFactory.GetQuadraturePoints(ref accuracy);
      var scaledSurvCurves = CreateScaleCalibrator(survivalCurves)
        .GetScaleSurvivalCurves().ToArray();
      var principals = ArrayUtil.NewArray(
        scaledSurvCurves.Length, 1.0 / scaledSurvCurves.Length);
      double ap, dp;

      for (int i = 0; i < dps.Length; ++i)
      {
        if (i == 0)
        {
          ap = 0.0;
          dp = dps[i];
        }
        else
        {
          ap = dps[i - 1];
          dp = dps[i];
        }
        cdo = GetCdo(_runningPrem[i, 0], _quotes[i, 0], ap, dp);

        //Adjust notional
        var notional = (i == 0 && adjustNotional) ? GetAdjustedNotional() : 1.0;
        var  pricer = BasketPricerFactory.CDOPricerSemiAnalytic(cdo, Dt.Empty,
          asOf, settle, _discountCurve, null, scaledSurvCurves, principals,
          copula, corr, 3, TimeUnit.Months, quadPoints, 0.0, notional, true, false,
          MakeResetList(new Product[] {cdo}, null, settle));
        pricer.Basket.AccuracyLevel = accuracy;
        pricer.Validate();

        retVal.Add(pricer);
      }

      return retVal.ToArray();
    }

    #endregion Pricer

    #region Products

    private static CDX GetCdx()
    {
      return new CDX(_effective, _cdxMaturity, Currency.USD, 100.0 / 10000.0,
        DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        CdxType = CdxType.Unfunded,
        FirstPrem = CDS.GetDefaultFirstPremiumDate(_effective, _cdxMaturity),
        Description = "CDX",
      };
    }

    private static SyntheticCDO GetCdo(double premium, double fee, double attach, double detach)
    {
      var cdo = new SyntheticCDO(_effective, _cdxMaturity,
        Currency.USD, premium / 10000.0, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        Fee = fee,
        FeeSettle = _effective,
        Attachment = attach,
        Detachment = detach,
        CdoType = CdoType.Unfunded,
        AmortizePremium = true,
        FeeGuaranteed = false
      };

      cdo.Validate();

      return cdo;
    }

    #endregion Products
   
    #region BaseCorrelation

    private static BaseCorrelationTermStruct CreateBaseCorrelation(
      BaseCorrelationStrikeMethod strikeMethod)
    {
      var cdx = new[] { _cdx };
      var maturities = new[] { _cdxMaturity };
      var useTenors = new[] { true };

      return BaseCorrelationFactory.BaseCorrelationFromMarketQuotes(
        BaseCorrelationCalibrationMethod.MaturityMatch,
        strikeMethod,
        CreateScaleCalibrator(_survivalCurves),
        _runningPrem,
        _detachments,
        _quotes,
        cdx, maturities, useTenors, null, CreateBCParam());
    }

    private static BaseCorrelationParam CreateBCParam()
    {
      var accuracy = 0.0;
      int quadPoints = BasketPricerFactory.GetQuadraturePoints(ref accuracy);
      BaseCorrelationParam par = new BaseCorrelationParam();
      SetCopula(par, "Gauss", 0, 0, 0, 2);
      par.StepSize = 3;
      par.StepUnit = TimeUnit.Months;
      par.QuadraturePoints = quadPoints;
      par.AccuracyLevel = accuracy;
      par.GridSize = 0;
      par.ToleranceF = 1E-15;
      par.ToleranceX = 1E-15;
      par.StrikeInterp = InterpMethod.PCHIP;
      par.StrikeExtrap = ExtrapMethod.Smooth;
      par.TenorInterp = InterpMethod.Linear;
      par.TenorExtrap = ExtrapMethod.Const;
      par.Min = 0;
      par.Max = 2;
      par.Model = BasketModelType.SemiAnalytic;
      par.InterpOnFactors = false;
      par.BottomUp = true;
      par.WithCorrelatedRecovery = true;
      par.QCRModel = RecoveryCorrelationType.ZeroOne;

      return par;
    }

    #endregion BaseCorrelation

    #region ScaleCalibrator

    private static IndexScalingCalibrator CreateScaleCalibrator(
      SurvivalCurve[] survivalCurves)
    {
      var tenors = Toolkit.Base.Utils.ToTenorName(_effective, _cdxMaturity, true);
      var includes = ArrayUtil.NewArray(survivalCurves.Length, true);
      var cdx = GetCdx();
      cdx.Weights = ArrayUtil.NewArray(survivalCurves.Length, 1.0/survivalCurves.Length);
      var indexScaleCalibrator = new IndexScalingCalibrator(_asOf, _settle, new[] { cdx }, new[] { tenors },
        new[] { indexQuote_ / 10000.0 }, false, new[] { CDXScalingMethod.Model }, true, false, _discountCurve,
        survivalCurves, includes, 0.4)
      {
        ScaleHazardRates = true,
        ScalingType = BumpMethod.Relative,
        ActionOnInadequateTenors = ActionOnInadequateTenors.DropFirstIndexTenor
      };
      if (indexScaleCalibrator.ActionOnInadequateTenors == ActionOnInadequateTenors.DropFirstIndexTenor)
        indexScaleCalibrator.CheckUseTenors();

      return indexScaleCalibrator;
    }

    #endregion ScaleCalibrator

   
    #region HelpMethods

    private static List<RateReset>[] MakeResetList(Product[] products, double[] lastResets, Dt settle)
    {
      if (products == null || lastResets == null || lastResets.Length == 0)
        return new List<RateReset>[0];
      List<RateReset>[] rateResets = new List<RateReset>[products.Length];
      for (int i = 0; i < products.Length; ++i)
        if (products[i] != null)
        {
          // Set the RateResets to support returning the current coupon
          double lastReset = lastResets[i < lastResets.Length ? i : 0];
          rateResets[i] = new List<RateReset>();
          if (!lastReset.AlmostEquals(0.0) || products[i].Effective < settle)
            rateResets[i].Add(new RateReset(products[i].Effective, lastReset));
        }
      return rateResets;
    }

    private static void SetCopula(BaseCorrelationParam par, string copula,
                                  int dfCommon, int dfIdiosyn, double min, double max)
    {
      BaseCorrelation bc = new BaseCorrelation(BaseCorrelationMethod.ArbitrageFree,
        BaseCorrelationStrikeMethod.UnscaledForward, new double[1], new double[1]);
      bc.Extended = max > 1;
      par.Copula = new Copula();
      par.CopulaType = par.Copula.CopulaType;
      par.DfCommon = dfCommon;
      par.DfIndividual = dfIdiosyn;
      par.Max = max;
      par.Min = min;
      return;
    }

    private static double GetAdjustedNotional()
    {
      const double dp = 0.03;
      const double ap = 0.0;
      const double recovery = 0.4;
      const double weight = 0.008;
      var loss = (1 - recovery)*weight;
      var amort = recovery*weight;
      var adjustDp = (dp - loss)/(1 - loss - amort);
      return (adjustDp - ap)*(1 - loss - amort)/(dp - ap);
    }


    #endregion HelpMethods

    #region Data

    private static readonly Dt _asOf = new Dt(20141222); // Trade asof Date
    private static readonly Dt _settle = _asOf; 
    private static readonly Dt _effective = new Dt(20130922);
    private static readonly Dt _cdxMaturity = new Dt(20191220);
    private static readonly double indexQuote_ = 63.47;

    private static DiscountCurve _discountCurve;
    private static SurvivalCurve[] _survivalCurves;
    private static CDX _cdx;

    private IDisposable _tempConfig;

    #region CDOData

    private static readonly double[,] _runningPrem = { { 500.0 }, {100.0}, {100.0} };
    private static readonly double[] _detachments = {0.03, 0.07, 0.15};
    private static readonly double[,] _quotes = { { 0.28125 }, { 0.09102 }, { -0.00531 } };

    #endregion CDOData


    #endregion Data
  }
}

