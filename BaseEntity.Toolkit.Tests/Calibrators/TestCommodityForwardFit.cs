// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Commodities;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  /// <summary>
  ///   Test CommodityForwardFitCalibrator
  /// </summary>
  /// <remarks> 
  /// <para>Questions</para>
  /// <list type="number">
  ///  <item><description>What happens if a commodity does not have spot price?</description></item>
  /// </list>
  /// </remarks>
  [TestFixture]
  public class TestCommodityForwardFit : ToolkitTestBase
  {
    #region Data and Properties

    private string _commodityName = "CME Live Cattle";
    private Dt _tradeDate = new Dt(24, 1, 2013);
    private double _spotPrice = 125.0;

    private string[,] _quotes = {
                                  {"Feb13", "125.875", "FUT"},
                                  {"Apr13", "130.35", "FUT"},
                                  {"Jun13", "126.8", "FUT"},
                                  {"Aug13", "127.75", "FUT"},
                                  {"Oct13", "132.075", "FUT"},
                                  {"Dec13", "133.675", "FUT"},
                                  {"Feb14", "134.9", "FUT"},
                                  {"Apr14", "136.5", "FUT"},
                                  {"Jun14", "132", "FUT"},
                                };

    private IEnumerable<string> Tenors
    {
      get
      {
        return Enumerable.Range(0, _quotes.GetLength(0))
          .Select(i => _quotes[i, 0]);
      }
    }

    private IEnumerable<double> Prices
    {
      get
      {
        return Enumerable.Range(0, _quotes.GetLength(0))
          .Select(i => Double.Parse(_quotes[i, 1]));
      }
    }

    private IEnumerable<InstrumentType> InstrumentTypes
    {
      get
      {
        return Enumerable.Range(0, _quotes.GetLength(0)).Select(i =>
                                                                (InstrumentType)Enum.Parse(typeof(InstrumentType), _quotes[i, 2]));
      }
    }

    #endregion

   
    [Test]
    public void RoundTripPrice()
    {
      Dt asOf = _tradeDate;
      var name = "TestCurve";
      var spotPrice = _spotPrice;
      var curveFitSettings = new CalibratorSettings
                             {
                               CurveAsOf = asOf,
                             };

      var discountCurve = new DiscountCurve(asOf, 0.05);
      var projectionCurves = new List<CalibratedCurve> {discountCurve};
      var referenceIndex = new CommodityPriceIndex(
        _commodityName, Currency.USD, DayCount.Actual365Fixed, Calendar.NYB,
        BDConvention.Modified, 0, Frequency.None);

      double[] quotes = Prices.ToArray();
      InstrumentType[] instrumentTypes = InstrumentTypes.ToArray();
      string[] tenors = Tenors.ToArray();
      Dt[] settles = null;
      Dt[] maturities = null;
      double[] weights = null;
      Frequency[,] freqs = null;
      BDConvention[] rolls = null;
      Calendar[] calendars = null;
      var settings = Enumerable.Repeat(new CommodityFuturePaymentSettings{ContractSize = 1000.0, TickSize = 0.005, TickValue  = 5.0}, quotes.Length).ToArray();
      var curve = CommodityCurve.Create(name, spotPrice,
                                        curveFitSettings, discountCurve, projectionCurves,
                                        referenceIndex, null, quotes, instrumentTypes, settles, maturities, tenors,
                                        weights, freqs, rolls, calendars, settings);

      var calibrator = curve.Calibrator;
      var tenotPricers = curve.Tenors
        .Select(t => calibrator.GetPricer(curve, t.Product))
        .ToArray();
      Assert.AreEqual(quotes.Length + 1, tenotPricers.Length);
      for (int i = Double.IsNaN(spotPrice) ? 1 : 0, n = tenotPricers.Length;
           i < n;
           ++i)
      {
        var expect = (i == 0 ? spotPrice : quotes[i - 1]);
        var actual = tenotPricers[i].Pv();
        Assert.AreEqual(expect, actual, Math.Abs(1 + expect) * 1E-9);
      }

    }

    [Test]
    public void RoundTripPriceNoObservableSpot()
    {
      Dt asOf = _tradeDate;
      var name = "TestCurve";
      var curveFitSettings = new CalibratorSettings
                             {
                               CurveAsOf = asOf,
                             };

      var discountCurve = new DiscountCurve(asOf, 0.05);
      var projectionCurves = new List<CalibratedCurve> {discountCurve};
      var referenceIndex = new CommodityPriceIndex(
        _commodityName, Currency.USD, DayCount.Actual365Fixed, Calendar.NYB,
        BDConvention.Modified, 0, Frequency.None);

      double[] quotes = Prices.ToArray();
      InstrumentType[] instrumentTypes = InstrumentTypes.ToArray();
      string[] tenors = Tenors.ToArray();
      Dt[] settles = null;
      Dt[] maturities = null;
      double[] weights = null;
      Frequency[,] freqs = null;
      BDConvention[] rolls = null;
      Calendar[] calendars = null;
      var settings = Enumerable.Repeat(new CommodityFuturePaymentSettings { ContractSize = 1000.0, TickSize = 0.005, TickValue = 5.0 }, quotes.Length).ToArray();

      var curve = CommodityCurve.Create(name, null,
                                        curveFitSettings, discountCurve, projectionCurves,
                                        referenceIndex, null, quotes, instrumentTypes, settles, maturities, tenors,
                                        weights, freqs, rolls, calendars, settings);

      var calibrator = curve.Calibrator;
      var tenotPricers = curve.Tenors
        .Select(t => calibrator.GetPricer(curve, t.Product))
        .ToArray();
      Assert.AreEqual(quotes.Length, tenotPricers.Length);
      for (int i = 0, n = tenotPricers.Length;i < n; ++i)
      {
        var expect = quotes[i];
        var actual = tenotPricers[i].Pv();
        Assert.AreEqual(expect, actual, Math.Abs(1 + expect) * 1E-9);
      }

    }
  }
}