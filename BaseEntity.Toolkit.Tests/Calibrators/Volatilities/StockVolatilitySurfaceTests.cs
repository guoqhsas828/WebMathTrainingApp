// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using DividendType = BaseEntity.Toolkit.Base.DividendSchedule.DividendType;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  [TestFixture]
  public class StockVolatilitySurfaceTest : ToolkitTestBase
  {
    [Test]
    public void FlatVolatilityPrice()
    {
      var data = GetInputData();
      var asOf = data.Today;
      var ds = data.DividendSchedule;
      var expect = BlackScholes.P(OptionStyle.European, data.Type, data.Time,
        data.Spot, data.Strike, data.Rate, 0, ds, data.Volatility);

      // Create the product
      Dt expiry = data.Expiry;
      var so = new StockOption(expiry, data.Type, OptionStyle.European, 100);

      // Create the discount curve
      var dc = new DiscountCurve(asOf).SetRelativeTimeRate(data.Rate);

      // Create the stock curve
      var stock = Stock.GetStockWithConvertedDividend(Currency.None, null, ds);
      var stockCurve = new StockCurve(asOf, data.Spot, dc, 0.0, stock);

      #region Test volatility term
      // Create and check volatility term
      var term = StockVolatilityUnderlying.Create(asOf, data.Spot, ds, dc)
        as IVolatilityUnderlying;
      var par = BlackScholesSurfaceBuilder.GetParameters(asOf, expiry,
        (b, e) => Dt.RelativeTime(b, e), term.Spot.Interpolate(expiry),
        term.Curve1, term.Curve2);
      var call = BlackScholes.P(OptionStyle.European, OptionType.Call,
        par.Time, par.Spot, data.Strike, par.Rate2, par.Rate1, data.Volatility);
      Assert.AreEqual(expect, call, 1E-14, "Pv.Term");
      #endregion

      #region Test strike surface
      // Create the sticky strike surface
      var surface = VolatilitySurfaceFactory.Create(
        asOf, new[] {"7M"}, new[] {expiry}, new[] {data.Strike},
        new[,] {{data.Volatility}}, VolatilityQuoteType.StickyStrike, term,
        SmileModel.SplineInterpolation, null, null, null, "VolSurface");

      // Create the pricer and evaluate pv
      var sop = new StockOptionPricer(so, asOf, asOf,
        stockCurve, dc, surface);
      var pv = sop.ProductPv();
      Assert.AreEqual(expect, pv, 1E-14, "Pv.StickyStrike");
      #endregion

      #region Test Moneyness surface
      // Create the moneyness surface
      var forward = data.Forward;
      surface = VolatilitySurfaceFactory.Create(
        asOf, new[] { "7M" }, new[] { expiry },
        new[] { data.Strike / forward }, new[,] { { data.Volatility } },
        VolatilityQuoteType.Moneyness, term,
        SmileModel.SplineInterpolation, null, null, null, "VolSurface");

      // Create the pricer
      sop = new StockOptionPricer(so, asOf, asOf, stockCurve, dc, surface);
      pv = sop.ProductPv();
      Assert.AreEqual(expect, pv, 1E-14, "Pv.StrikePrice");
      #endregion

      #region Test strike-price surface
      // Create the sticky strike surface
      surface = VolatilitySurfaceFactory.Create(
        asOf, new[] { "7M" }, new[] { expiry }, 
        new[] { StrikeSpec.Create(data.Strike,data.Type) },
        new[,] { { expect } }, VolatilityQuoteType.StrikePrice, term,
        SmileModel.SplineInterpolation, null, null, null, "VolSurface");

      // Create the pricer
      sop = new StockOptionPricer(so, asOf, asOf, stockCurve, dc, surface);
      pv = sop.ProductPv();
      Assert.AreEqual(expect, pv, 5E-10, "Pv.StrikePrice");
      #endregion
    }

    #region Data
    class BsData
    {
      public readonly Dt Today, Expiry;
      public readonly double Spot, Strike, Rate, Dividend, Volatility, Time;
      public readonly DividendSchedule DividendSchedule;
      public readonly OptionType Type = OptionType.Call;
      public BsData(Dt asOf, Dt expiry, double S, double K,
        double r, double d, DividendSchedule ds, double v, double T)
      {
        Today = asOf;
        Expiry = expiry;
        Time = T;
        Spot = S;
        Strike = K;
        Rate = r;
        Dividend = d;
        DividendSchedule = ds;
        Volatility = v;
      }
      public double Forward => Spot * Math.Exp((Rate - Dividend) * Time);
    }

    private BsData _bsData;
    private BsData GetInputData()
    {
      if (_bsData == null)
      {
        // The input data
        Dt asOf = new Dt(21, 6, 2013);
        Dt date1 = asOf + (RelativeTime)0.25;
        Dt date2 = asOf + (RelativeTime)0.5;
        var ds = new DividendSchedule(asOf, new[]
        {
          Tuple.Create(date1, DividendType.Fixed, 2.0),
          Tuple.Create(date2, DividendType.Fixed, 2.0),
        });
        double S = 100, K = 100, r = 0.05, T = 7 / 12.0, v = 0.3;

        // Round trip of the rate with discount curve
        Dt expiry = asOf + (RelativeTime)T;

        // Calculate the divident yield.
        var pv = ds.PresentValue(S, r, T);
        var d = RateCalc.RateFromPrice(1 - pv / S, asOf, expiry);

        _bsData = new BsData(asOf, expiry, S, K, r, d, ds, v, T);
      }
      return _bsData;
    }
    #endregion
  }
}
