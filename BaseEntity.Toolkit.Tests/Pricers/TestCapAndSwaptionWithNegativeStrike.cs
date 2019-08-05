//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Calibrators;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
   [TestFixture]
   public class TestCapAndSwationWithNegativeStrike : ToolkitTestBase
   {

     [SetUp]
     public void Initialize()
     {
       #region Common Data

       _strike = -0.025;
       _epsilon = 0.000001;
       _asOf = new Dt(20110609);
       _settle = new Dt(20110613);
       _capEffecitve = new Dt(20110913);
       _lastPaymentDay = new Dt(20151220);
       _ccy = Currency.EUR;
       _calendar = Calendar.TGT;
       _dayCount = DayCount.Actual365Fixed;
       _roll = BDConvention.Following;
       _frequency = Frequency.SemiAnnual;

       _disCurveN = GetDiscountCurve() as DiscountCurve;
       _disCurveP = new RateCurveBuilder().CreateRateCurves(_asOf) as DiscountCurve;
       _rateIndex = (InterestRateIndex)StandardReferenceIndices.Create("EURLIBOR_3M");
       _payOrRec = PayerReceiver.Payer;
       _optionStyle = OptionStyle.European;

       #endregion

       #region Data for Atm Curve and Cap/Floor vol surface

       _capType = CapFloorType.Cap;
       _fitToMarket = 0.5;
       _capExpiryTenors = new[] {"1Y", "3Y", "5Y", "7Y", "10Y", "15Y"};
       _capStrikesForCurve = new[] {0.0029, 0.0058, 0.0181, 0.0246, 0.0306, 0.0356};
       _capNormalVolsForCurve = new[] {0.0075, 0.0070, 0.0065, 0.0060, 0.0055, 0.0050};
       _capLogNormalVolsForCurve = new[] {0.75, 0.701, 0.652, 0.601, 0.552, 0.508};
       _capStrikesForSurface = new[] {0.01, 0.015, 0.02, 0.03, 0.035, 0.040, 0.050};
       _capNormalVolsForSurface = new[,]
       {
         {0.0075, 0.0092, 0.0106, 0.01327, 0.0145, 0.0156, 0.0178},
         {0.0081, 0.0098, 0.0112, 0.0136, 0.0146, 0.0155, 0.0172},
         {0.0090, 0.01034, 0.01124, 0.01246, 0.01298, 0.01351, 0.0147},
         {0.0094, 0.01047, 0.01114, 0.01194, 0.01224, 0.01259, 0.01341},
         {0.0097, 0.01047, 0.01095, 0.01149, 0.01168, 0.01192, 0.01253},
         {0.0094, 0.0099, 0.0103, 0.0106, 0.0107, 0.01086, 0.0113}
       };

       _capLogNormalVolsForSurface = new[,]
       {
         {0.9260, 0.8985, 0.8790, 0.8520, 0.8420, 0.8335, 0.8190},
         {0.684, 0.6425, 0.6135, 0.5745, 0.56, 0.5480, 0.5290},
         {0.61, 0.5415, 0.49, 0.419, 0.395, 0.378, 0.358},
         {0.5555, 0.486, 0.4345, 0.3625, 0.338, 0.32, 0.2975},
         {0.5075, 0.4415, 0.3930, 0.3265, 0.3035, 0.2865, 0.2645},
         {0.4545, 0.394, 0.35, 0.2895, 0.269, 0.253, 0.2315},
       };

       #endregion

       #region Data for Swaption vol cube and Bgm vol surface

       _expiryTenorForSwaption = new[] {"1Y", "2Y", "5Y"};
       _fwdTenorsForSwaption = new[] {"1Y", "5Y", "10Y", "15Y"};
       _atmLogNormalVols = new[,]
       {
         {0.70, 0.35, 0.25, 0.20},
         {0.45, 0.30, 0.25, 0.20},
         {0.30, 0.25, 0.20, 0.18}
       };
       _atmNormalVols = new[,]
       {
         {0.0090, 0.0110, 0.0105, 0.0100},
         {0.0120, 0.0115, 0.0110, 0.0100},
         {0.0115, 0.0110, 0.0105, 0.0095}
       };

       _skewStrikes = new[] {-0.01, -0.005, 0.0, 0.005, 0.01};
       _normalVolCubeStrikesSkews
         = new double[_expiryTenorForSwaption.Length*_fwdTenorsForSwaption.Length, _skewStrikes.Length];
       for (int i = 0, n = _expiryTenorForSwaption.Length*_fwdTenorsForSwaption.Length; i < n; i++)
         for (int j = 0, m = _skewStrikes.Length; j < m; j++)
         {
           _normalVolCubeStrikesSkews[i, j] = 0.0;
         }

       _logNormalVolCubeStrikeSkews
         = new double[_expiryTenorForSwaption.Length*_fwdTenorsForSwaption.Length, _skewStrikes.Length];
       for (int idx = 0; idx < _expiryTenorForSwaption.Length*_fwdTenorsForSwaption.Length; idx++)
         for (int jdx = 0; jdx < _skewStrikes.Length; jdx++)
         {
           _logNormalVolCubeStrikeSkews[idx, jdx] = 0.0;
         }

       _fixedDayCount = DayCount.Thirty360;
       _fixedFrequency = Frequency.SemiAnnual;
       _floatingDayCount = DayCount.Actual360;
       _floatingFrequency = Frequency.Quarterly;
       _notiDay = 2;

       #endregion

       #region Data for Sabr

       _sabrUpperBounds = new[] {0.2, 0.7, 0.9};
       _sabrLowerBounds = new[] {0.001, -0.9, 0.001};
       _sabrAlpha = new[] {0.0, 0.0, 0.0, 0.0, 0.0};
       _sabrBeta = new[] {0.45, 0.60, 0.50, 0.45, 0.35};
       _sabrRho = new[] {0.0, 0.0, 0.0, 0.0, 0.0};
       _sabrNu = new[] {0.0, 0.0, 0.0, 0.0, 0.0};
       _sabrDateTenors = new[] {"2D", "2Y", "3Y", "10Y", "15Y"};

       #endregion
     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloorSabr, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.Normal)]
     public void TestCapFloorPRateNStrikeNormal(VolatilityObjectKind volKind, 
       VolatilityType volType)
     {
       TestCapFloorPricer(volKind, volType, false, true);
     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.Normal)]
     public void TestCapFloorNRatePStrikeNormal(VolatilityObjectKind volKind,
       VolatilityType volType)
     {
       TestCapFloorPricer(volKind, volType, true, false);
     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.Normal)]
     public void TestCapFloorNRateNStrikeNormal(VolatilityObjectKind volKind, 
       VolatilityType volType)
     {
       TestCapFloorPricer(volKind, volType, true, true);
     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.Normal)]
     public void TestSwaptionNRateNStrikeNormal(VolatilityObjectKind volKind,
       VolatilityType volType)
     {
       TestSwaptionPricer(volKind, volType, true, true);
     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloorSabr, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.Normal)]
     public void TestSwaptionPRateNStrikeNormal(VolatilityObjectKind volKind,
       VolatilityType volType)
     {
       TestSwaptionPricer(volKind, volType, false, true);
     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.Normal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.Normal)]
     public void TestSwaptionNRatePStrikeNormal(VolatilityObjectKind volKind,
       VolatilityType volType)
     {
       TestSwaptionPricer(volKind, volType, true, false);
     }

     private static void TestCapFloorPricer(VolatilityObjectKind volKind,
       VolatilityType volType, bool negativeRate, bool negativeStrike)
     {
       var discountCurve = negativeRate ? _disCurveN : _disCurveP;
       var strike = negativeStrike ? _strike : 0.001;
       var pricer = GetCapFloorPricer(volKind, strike, volType, discountCurve);
       var pricer0 = GetCapFloorPricer(volKind, strike - _epsilon, volType, discountCurve);
       var pricer1 = GetCapFloorPricer(volKind, strike + _epsilon, volType, discountCurve);

       var pvMinus = pricer0.Pv();
       var pvPlus = pricer1.Pv();
       var capDeriv = (pvMinus - pvPlus) / (2 * _epsilon);

       double ptot = 0.0, pv0 = 0.0, pv1 = 0.0;
       var caplets = pricer.Caplets;
       foreach (var cplt in caplets )
       {
         var caplet = cplt as CapletPayment;
         if (caplet != null)
         {
           var rate = discountCurve.F(caplet.RateFixing, caplet.TenorDate, _dayCount, Frequency.None);
           var vol = RateVolatilityUtil.CapletVolatility(pricer.VolatilityCube,
             caplet.Expiry, rate, strike, _rateIndex);
           var delta = caplet.PeriodFraction;
           var T = CapFloorPricerBase.CalculateTime(_asOf, caplet.Expiry, _dayCount);
           var df = discountCurve.DiscountFactor(caplet.PayDt);
           var x = (rate - caplet.Strike)/(vol*Math.Sqrt(T));
           var p = df*delta*Normal.cumulative(x, 0.0, 1.0);
           ptot += p;

           var p0 = df*delta*BlackNormal.P(OptionType.Call, T, 0, rate, strike - _epsilon, vol);
           var p1 = df*delta*BlackNormal.P(OptionType.Call, T, 0, rate, strike + _epsilon, vol);
           pv0 += p0;
           pv1 += p1;
           var capletDeriv = (p0 - p1)/(2*_epsilon);
           Assert.AreEqual(p, capletDeriv, 1e-8);
         }
       }
       Assert.AreEqual(pv0, pvMinus, 1e-14);
       Assert.AreEqual(pv1, pvPlus, 1e-14);
       Assert.AreEqual(ptot, capDeriv, 1e-8);
     }


     private static void TestSwaptionPricer(VolatilityObjectKind volKind,
       VolatilityType volType, bool negativeRate, bool negativeStrike)
     {
       var strike = negativeStrike ? _strike : 0.05;
       var discountCurve = negativeRate ? _disCurveN : _disCurveP;
       var pricer = GetSwaptionPricer(volKind, volType, strike, discountCurve);
       var swpn = pricer.Swaption;
       var pricer0 = GetSwaptionPricer(volKind, volType, strike - _epsilon, discountCurve);
       var pricer1 = GetSwaptionPricer(volKind, volType, strike + _epsilon, discountCurve);
       var pv0 = pricer0.Pv();
       var pv1 = pricer1.Pv();
       var deriv = (pv0 - pv1)/(2*_epsilon);
       var vol = pricer.Volatility;
       var T = (swpn.Expiration - pricer.AsOf)/365.0;
       double level, rate;
       double effectiveStrike = swpn.EffectiveSwaptionStrike(_asOf, _settle,
         discountCurve, discountCurve, null, false, out rate, out level);

       var pv00 = level*BlackNormal.P(swpn.OptionType, T, 0, rate, effectiveStrike - _epsilon, vol);
       var pv11 = level*BlackNormal.P(swpn.OptionType, T, 0, rate, effectiveStrike + _epsilon, vol);

       var x = (rate - effectiveStrike)/(vol*Math.Sqrt(T));
       var p = level*Normal.cumulative(x, 0.0, 1.0);
       Assert.AreEqual(pv0, pv00, negativeRate ? 5e-8 : 1e-10);
       Assert.AreEqual(pv1, pv11, negativeRate ? 5e-8 : 1e-10);
       //In the case of positive strike & negative rate, the swaption pv value is 
       //very small, derivative is sensitive to the stike change.Meanwhile the 
       //smile effect on the Cap/Floor surface will affect the derivative.
       //Therefore, the accuracy is low. In the cap pricing, the negative 
       //rate actually only affects the first couples of caplets.
       Assert.AreEqual(deriv, p, negativeRate ? 6e-4 : 1e-5);

     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.CapFloorSabr, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.LogNormal)]
     public void TestSwaptionNStrikePRateLogNormal(VolatilityObjectKind volKind,
       VolatilityType volType)
     {
       var discountCurve = _disCurveP;
       var pricer = GetSwaptionPricer(volKind, volType, _strike, discountCurve);
       var pv = pricer.ProductPv();
       var swpn = pricer.Swaption;
       double level, rate;
       double strike = swpn.EffectiveSwaptionStrike(_asOf, _settle,
         discountCurve, discountCurve, null, false, out rate, out level);
       var intrinsic = level*Math.Max(rate - strike, 0.0);
       Assert.AreEqual(intrinsic, pv, "Swaption Pv must be the intrinsic value");
     }

     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.CapFloorSabr, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.CapFloorSabr, VolatilityType.Normal)]
     public void TestNegativeForwardRate(VolatilityObjectKind volKind,
       VolatilityType volType)
     {
       Assert.That(() => GetVolObject(volKind, volType, _disCurveN),
         Throws.InstanceOf<ToolkitException>().Or.InstanceOf(typeof(AggregateException)));
     }

     [TestCase(VolatilityObjectKind.Bgm, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.VolCurve, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.CapFloor, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.CapFloorSabr, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.SwaptionMarket, VolatilityType.LogNormal)]
     [TestCase(VolatilityObjectKind.Flat, VolatilityType.LogNormal)]
     public void TestCapFloorNegativeStrikeLogNormal(VolatilityObjectKind volKind,
       VolatilityType volType)
     {
       var pricer = GetCapFloorPricer(volKind, _strike, volType, _disCurveP);
       var caplets = pricer.Caplets;
       foreach (var cplt in caplets)
       {
         var caplet = cplt as CapletPayment;
         if (caplet != null)
         {
           var capletPv = pricer.CapletPv(caplet);

           var strike = caplet.Strike;
           var rate = _disCurveP.F(caplet.RateFixing, caplet.TenorDate, _dayCount, Frequency.None);
           var delta = caplet.PeriodFraction;
           var df = _disCurveP.DiscountFactor(caplet.PayDt);
           var intrinsic = df*delta*Math.Max(rate - strike, 0.0);

           Assert.AreEqual(intrinsic, capletPv, "Caplet PV must be the intrinsic value");
         }
       }
     }

     private static CapFloorPricer GetCapFloorPricer(VolatilityObjectKind volKind, 
       double strike, VolatilityType volType, DiscountCurve dc)
     {
       IVolatilityObject volCube = GetVolObject(volKind, volType, dc);

       Cap cap = new Cap(_capEffecitve, _lastPaymentDay, _ccy, _capType, strike,
         _dayCount, _frequency, _roll, _calendar);
       cap.AccrueOnCycle = false;
       cap.CycleRule=CycleRule.None;
       cap.RateIndex = _rateIndex;
       cap.Validate();

       CapFloorPricer pricer = new CapFloorPricer(cap, _asOf, _settle, dc,
         dc, volCube) {Notional = 1.0};

       if (pricer.LastExpiry != Dt.Empty)
         pricer.Resets.Add(new RateReset(pricer.LastExpiry, 0.0));
       pricer.Payment = null;
       pricer.Validate();
       return pricer;
     }

     private static SwaptionBlackPricer GetSwaptionPricer(
       VolatilityObjectKind volKind, VolatilityType volType, double strike, DiscountCurve dc)
     {

       var volObject = GetVolObject(volKind, volType, dc);

       var oExpiry = RateVolatilityUtil.SwaptionStandardExpiry(_asOf,
         _rateIndex, Tenor.Parse("1Y"));

       var swapEffective = RateVolatilityUtil
         .SwaptionStandardForwardSwapEffective(oExpiry, _notiDay, _calendar);

       var swapMaturity = Dt.Roll(Dt.Add(swapEffective, Tenor.Parse("1Y")), 
         _rateIndex.Roll, _rateIndex.Calendar);

       var fixedLeg= new SwapLeg(swapEffective, swapMaturity, _ccy, strike,
         _fixedDayCount, _fixedFrequency, _roll, _calendar, false);
       var floatingLeg = new SwapLeg(swapEffective, swapMaturity, _floatingFrequency,
        0.0, _rateIndex, _ccy, _floatingDayCount, _roll, _calendar);

       var swpn= new Swaption(_asOf, swapEffective, _ccy, fixedLeg,
         floatingLeg, _notiDay, _payOrRec, _optionStyle, strike);
       swpn.Validate();

       var pricer = new SwaptionBlackPricer(swpn, _asOf, _settle, dc, dc, volObject);
       pricer.Validate();

       return pricer;
     }


     #region VolatilityObject

     private static IVolatilityObject GetVolObject(VolatilityObjectKind volKind,
       VolatilityType volType, DiscountCurve dc)
     {
       switch (volKind)
       {
         case VolatilityObjectKind.Flat:
           return GetFlatVolaility(volType);
         case VolatilityObjectKind.Bgm:
           return GetBgmVolatilitySurface(volType, dc);
         case VolatilityObjectKind.VolCurve:
         case VolatilityObjectKind.CapFloor:
         case VolatilityObjectKind.CapFloorSabr:
           return GetAtmCurveOrCapFloorVolSurface(volKind, volType, dc);
         case VolatilityObjectKind.SwaptionMarket:
           return GetSwaptionMarketCube(volType, dc);
         default:
           throw new Exception("Volatility object has not implemented");
       }
     }

     private static SwaptionVolatilityCube GetSwaptionMarketCube(VolatilityType volType,
       DiscountCurve dc)
     {
       var surfaceVols = volType == VolatilityType.LogNormal 
         ? _atmLogNormalVols : _atmNormalVols;
       var skewVols = volType == VolatilityType.LogNormal 
         ? _logNormalVolCubeStrikeSkews : _normalVolCubeStrikesSkews;

       return SwaptionVolatilityCube.CreateSwaptionMarketCube(_asOf, dc,
         _expiryTenorForSwaption, _fwdTenorsForSwaption, surfaceVols, _rateIndex, volType,
         _expiryTenorForSwaption, _skewStrikes, _fwdTenorsForSwaption, skewVols,
         null, null, _fixedDayCount, _roll, _fixedFrequency, _calendar, _notiDay);
     }

     private static BgmForwardVolatilitySurface GetBgmVolatilitySurface(VolatilityType volType,
        DiscountCurve dc)
     {
       var volValues = volType == VolatilityType.LogNormal 
         ? _atmLogNormalVols : _atmNormalVols;
       var distributionType = volType == VolatilityType.LogNormal 
         ? DistributionType.LogNormal : DistributionType.Normal;
       var correlation = BgmCorrelation.CreateBgmCorrelation(
         BgmCorrelationType.PerfectCorrelation, _expiryTenorForSwaption.Length, new double[0, 0]);
       return BgmForwardVolatilitySurface.Create(_asOf, new BgmCalibrationParameters(), 
         dc, _expiryTenorForSwaption, _fwdTenorsForSwaption, 
         CycleRule.None, _roll, _calendar, correlation, volValues, distributionType);
     }

     private static RateVolatilityCube GetFlatVolaility(VolatilityType volType)
     {
       Dt settle = Cap.StandardSettle(_asOf, _rateIndex);
       var expiry = Dt.Add(settle, Tenor.Parse("20Y"));
       var flatVol = volType == VolatilityType.LogNormal ? 0.29 : 0.29/100.0;
       RateVolatilityCube cube = RateVolatilityCube.CreateFlatVolatilityCube(_asOf, new[] { expiry },
         new[] { flatVol }, volType, _rateIndex);
       cube.ExpiryTenors = new[] { Tenor.Parse("20Y") };
       cube.Validate();
       return cube;
     }

     private static RateVolatilityCube GetAtmCurveOrCapFloorVolSurface( 
       VolatilityObjectKind volKind, VolatilityType volType, DiscountCurve dc)
     {
       var fitSettings = GetFitSetting();
       var futPrices = new double[0];
       var futStrikes = new double[0];
       var futDates = new Dt[0];
       Dt settle = Cap.StandardSettle(_asOf, _rateIndex);
       var capMaturityDates = _capExpiryTenors.Select(t => Dt.Add(settle, t)).ToArray();
       var indexSelector = GetProjectionIndexSelector(capMaturityDates, _rateIndex, null);
       var curveSelector = GetProjectionCurveSelector(indexSelector, dc, null);

       var curveVols = volType == VolatilityType.LogNormal
         ? _capLogNormalVolsForCurve : _capNormalVolsForCurve;

       var surfaceVols = volType == VolatilityType.LogNormal 
         ? _capLogNormalVolsForSurface : _capNormalVolsForSurface;

       double[] lambdaEdf, lambdaCap;
       RateVolatilityUtil.MapFitSettings(fitSettings, futStrikes.Length, 
         _capStrikesForSurface.Length, volType, false, out lambdaEdf, out lambdaCap);
       RateVolatilityCalibrator calibrator;
       if (volKind==VolatilityObjectKind.CapFloorSabr)
         calibrator = new RateVolatilityCapSabrCalibrator(_asOf, settle, dc,
           indexSelector, curveSelector, volType, futDates, futStrikes, futPrices, null,
            null, _capExpiryTenors, capMaturityDates,
           _capStrikesForSurface, surfaceVols, lambdaEdf, lambdaCap, 
           VolatilityBootstrapMethod.PiecewiseQuadratic,
           fitSettings.SabrLowerBounds, fitSettings.SabrUpperBounds, fitSettings.SabrBeta,
           fitSettings.SabrAlpha, fitSettings.SabrRho, fitSettings.SabrNu);
       else if (volKind == VolatilityObjectKind.CapFloor)
         calibrator = new RateVolatilityCapBootstrapCalibrator(
           _asOf, settle, dc, indexSelector, curveSelector, volType,
           futDates, futStrikes, futPrices, null, null,
           _capExpiryTenors, capMaturityDates, _capStrikesForSurface,
           surfaceVols, lambdaEdf, lambdaCap,
           VolatilityBootstrapMethod.PiecewiseQuadratic);
       else  //Vol Curve
       {
         double lambda = 0.05 + 0.05 * _fitToMarket;
         calibrator = new RateVolatilityATMCapCalibrator(_asOf, settle,
           dc, indexSelector, curveSelector,
           _capStrikesForCurve, _capExpiryTenors, capMaturityDates, curveVols,
           lambda, volType);
       }
       var cube = new RateVolatilityCube(calibrator)
       {
         ExpiryTenors = _capExpiryTenors.Select(Tenor.Parse).ToArray()
       };
       cube.Validate();
       cube.Fit();
       return cube;
     }

     #endregion VolaitlityObject

     #region Helpers

     private static DiscountCurve GetDiscountCurve()
     {
       var calibrator = new DiscountBootstrapCalibrator(_asOf, _asOf);
       calibrator.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);
       calibrator.SwapCalibrationMethod = SwapCalibrationMethod.Extrap;
       var discountCurve = new DiscountCurve(calibrator);
       discountCurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
       discountCurve.Ccy = _ccy;

       for (int i = 0; i < _mmTenors.Length; i++)
         _mmDates[i] = String.IsNullOrEmpty(_mmTenors[i])
           ? Dt.Empty
           : Dt.Add(_asOf, _mmTenors[i]);
       for (int i = 0; i < _mmTenors.Length; i++)
       {
         int last = discountCurve.Tenors.Count;
         discountCurve.AddMoneyMarket(_mmTenors[i], _mmDates[i], _mmRates[i], _mmDaycount);
         ((Note) discountCurve.Tenors[last].Product).BDConvention = BDConvention.None;
       }
       for (int i = 0; i < _swapTenors.Length; i++)
         _swapDates[i] = String.IsNullOrEmpty(_swapTenors[i])
           ? Dt.Empty
           : Dt.Add(_asOf, _swapTenors[i]);
       for (int i = 0; i < _swapTenors.Length; i++)
         discountCurve.AddSwap(_swapTenors[i], _swapDates[i], _swapRates[i],
           _swapDaycount, _swapFreq, BDConvention.None, Calendar.None);

       discountCurve.Fit();
       return discountCurve;
     }


     private static RateVolatilityFitSettings GetFitSetting()
     {
       var sabrDates = new Dt[_sabrDateTenors.Length];
       for (int i = 0, n = sabrDates.Length; i < n; ++i)
       {
         sabrDates[i] = Dt.Add(_asOf, Tenor.Parse(_sabrDateTenors[i]));
       }

       Curve inputBeta = CleanAndCreateCurve(_asOf, sabrDates, _sabrBeta);
       Curve guessAlpha = CleanAndCreateCurve(_asOf, sabrDates, _sabrAlpha);
       Curve guessRho = CleanAndCreateCurve(_asOf, sabrDates, _sabrRho);
       Curve guessNu = CleanAndCreateCurve(_asOf, sabrDates, _sabrNu);

       return new RateVolatilityFitSettings
       {
         FitToMarket = 1.0,
         SabrLowerBounds = _sabrLowerBounds,
         SabrUpperBounds = _sabrUpperBounds,
         SabrBeta = inputBeta,
         SabrAlpha = guessAlpha,
         SabrRho = guessRho,
         SabrNu = guessNu
       };
     }

     private static Curve CleanAndCreateCurve(Dt asOf, Dt[] dates, double[] values)
     {
       if (values == null || values.Length == 0)
         return null;
       var result = new Curve(asOf);
       for (int i = 0; i < dates.Length; i++)
         result.Add(dates[i], values[i]);
       return result;
     }

     private static Func<Dt, InterestRateIndex> GetProjectionIndexSelector(
       Dt[] expiries, InterestRateIndex rateIndex, InterestRateIndex[] projectionIndex)
     {
       if ((projectionIndex == null) || (projectionIndex.Length == 0))
         return dt => rateIndex;
       if (projectionIndex.Length != expiries.Length)
         throw new ArgumentException("Must provide one InterestRateIndex per underlying Cap");
       return dt =>
       {
         int idx = Array.BinarySearch(expiries, dt);
         if (idx < 0)
           throw new ArgumentException(String.Format("RateIndex for expiry {0} not found", dt));
         return projectionIndex[idx];
       };
     }

     private static Func<Dt, DiscountCurve> GetProjectionCurveSelector(
       Func<Dt, InterestRateIndex> indexSelector, DiscountCurve discountCurve, DiscountCurve[] projectionCurve)
     {
       if ((projectionCurve == null) || (projectionCurve.Length == 0))
         return dt => discountCurve;
       if (projectionCurve.Length == 1)
         return dt => projectionCurve[0];
       return dt =>
       {
         var rateIdx = indexSelector(dt);
         return projectionCurve.FirstOrDefault(c => rateIdx.Equals(c.ReferenceIndex)) ?? discountCurve;
       };
     }

     #endregion

     public enum VolatilityObjectKind
     {
       Flat = 1,
       VolCurve=2,
       SwaptionMarket = 4,
       Bgm = 8,
       CapFloor = 10,
       CapFloorSabr = 20
     }

     #region Data

     private static Dt _asOf, 
       _settle, 
       _capEffecitve, 
       _lastPaymentDay;
     private static Calendar _calendar;
     private static DayCount _dayCount;
     private static BDConvention _roll;
     private static Frequency _frequency;
     private static DiscountCurve _disCurveP,
       _disCurveN;
     private static InterestRateIndex _rateIndex;
     private static PayerReceiver _payOrRec;
     private static OptionStyle  _optionStyle;
     private static string[] _capExpiryTenors,
       _expiryTenorForSwaption, 
       _fwdTenorsForSwaption;

     private static double[] _capStrikesForCurve,
       _capNormalVolsForCurve,
       _capLogNormalVolsForCurve,
       _capStrikesForSurface,
       _skewStrikes;

     private static double[,] _capNormalVolsForSurface,
       _capLogNormalVolsForSurface,
       _atmNormalVols,
       _normalVolCubeStrikesSkews,
       _atmLogNormalVols,
       _logNormalVolCubeStrikeSkews;

     private static double _fitToMarket, _epsilon;
     private static CapFloorType _capType;
     private static Currency _ccy;
     private static double _strike;

     private static DayCount _fixedDayCount, _floatingDayCount;
     private static Frequency _fixedFrequency, _floatingFrequency;
     private static int _notiDay;

     private static string[] _sabrDateTenors;
     private static double[] _sabrLowerBounds,
       _sabrUpperBounds,
       _sabrAlpha,
       _sabrBeta,
       _sabrRho,
       _sabrNu;

     private static DayCount _mmDaycount = DayCount.Actual360;
     private static string[] _mmTenors = { "6 Month", "1 Year" };
     private static Dt[] _mmDates = new Dt[_mmTenors.Length];
     private static double[] _mmRates = {-0.02, -0.017};
     private static DayCount _swapDaycount = DayCount.Thirty360;
     private static Frequency _swapFreq = Frequency.SemiAnnual;
     private static string[] _swapTenors = {"2 Year", "3 Year", "5 Year", "7 Year", "10 Year"};
     private static double[] _swapRates = {-0.01, -0.001, 0.0264, 0.0368, 0.0372};
     private static Dt[] _swapDates = new Dt[_swapRates.Length];

     #endregion Data
   }
}
