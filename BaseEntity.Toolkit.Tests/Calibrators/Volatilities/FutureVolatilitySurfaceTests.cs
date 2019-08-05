// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  [TestFixture]
  public class FuturesVolatilitySurfaceTests : ToolkitTestBase
  {
    [Test]
    public void TestFuturesVolatilitySurface()
    {
      var quoteType = VolatilityQuoteType.StrikePrice;
      var smileModel = SmileModel.SplineInterpolation;

      var asOf = _asOf;
      var data = _data;
      var discountCurve = GetDiscountCurve(asOf);
      var spotCurve = GetSpotCurve(asOf, data);
      var term = VolatilityUnderlying.Create(
        spotCurve, discountCurve, discountCurve);

      var futPrices = data.Row(0).Skip(1).Select(s => s.ParseDouble()).ToArray();
      var expiries = data.Row(1).Skip(1).Select(s => s.ParseDt()).ToArray();
      var specs = StrikeArray.Create(data.Column(0).Skip(2).ToArray(),
        VolatilityQuoteType.StrikePrice).AsStrikeSpec();
      int nrow = expiries.Length, ncol = specs.Length;
      var quotes = Enumerable.Range(2, ncol)
        .Select(i => data.Row(i).Skip(1).Select(s => s.ParseDouble()))
        .ToArray2DTransposed(nrow, ncol);

      var ivols = new double[nrow,ncol];
      for(int i = 0; i < nrow; ++i)
      {
        var expiry = expiries[i];
        var ulPrice = futPrices[i];
        if(expiry.IsEmpty()) continue;
        for (int j = 0; j < ncol; ++j)
        {
          var spec = specs[j];
          var prem = quotes[i, j];
          if(spec.IsEmpty || !(prem > 0)) continue;
          var par = BlackScholesSurfaceBuilder.GetParameters(
            asOf, expiry, null, term.SpotPrice.Interpolate(expiry),
            term.YieldCurve, term.DiscountCurve);
          var v = StockFutureImpliedVolatility(expiry, spec.Type,
            spec.Strike, ulPrice, par.Rate1, null, 
            (DiscountCurve)term.DiscountCurve, prem);
          ivols[i, j] = v;
        }
      }

      var surface = VolatilitySurfaceFactory.Create(asOf,
        null, expiries, specs, quotes, quoteType, term,
        smileModel, null, null,
        new VolatilityFitSettings {ImpliedVolatilityAccuracy = 0.0},
        "VolatilitySurface");

      var halfcol = ncol / 2;
      var vols = new double[nrow, halfcol];
      for (int i = 0; i < nrow; ++i)
      {
        var expiry = expiries[i];
        if (expiry.IsEmpty()) continue;
        for (int j = 0; j < halfcol; ++j)
        {
          var spec = specs[j];
          vols[i, j] = surface.Interpolate(expiry, spec.Strike);
        }
      }

      for (int i = 0; i < nrow; ++i)
      {
        for (int j = 0; j < halfcol; ++j)
        {
          var e1 = ivols[i, j];
          var e2 = ivols[i, j + halfcol];
          var e = e1.Equals(0.0) ? e2 : (e2.Equals(0.0) ? e1 : ((e1 + e2) / 2));
          if (e.Equals(0.0)) continue;
          var a = vols[i, j];
          Assert.AreEqual(e, a, 1E-15);
        }
      }
      return;
    }

    private static Curve GetSpotCurve(Dt asOf, string[,] data)
    {
      var curve = new Curve(asOf, DayCount.None, Frequency.None)
      {
        Interp = new Linear(new Const(), new Const())
      };
      var futPrices = data.Row(0).Skip(1).Select(s => s.ParseDouble());
      var expiries = data.Row(1).Skip(1).Select(s => s.ParseDt());
      var count = expiries.Zip(futPrices, (dt, val) =>
      {
        if (dt.IsEmpty() || !(val > 0)) return 0;
        curve.Add(dt, val);
        return 1;
      }).Sum();
      if (count==0)
        throw new ApplicationException("No spot");
      return curve;
    }

    private double StockFutureImpliedVolatility(
      Dt expiration, OptionType type, double strike,
      double futPrice, double divYield, DividendSchedule divSched,
      DiscountCurve discountCurve, double premium)
    {
      var pricer = GetStockFuturePricer(expiration, type, strike,
        futPrice, divYield, divSched, discountCurve, 0.0);
      pricer.ImpliedVolatilityAccuracy = 0;
      try
      {
        var v = pricer.IVol(premium);
        RoundtripPrice(pricer, v, premium);
        return v;
      }
      catch (ToolkitException)
      {
        return 0;
      }
    }

    private static void RoundtripPrice(StockFutureOptionBlackPricer pricer,
      double volatility, double price)
    {
      pricer.VolatilitySurface = CalibratedVolatilitySurface.
        FromFlatVolatility(pricer.AsOf, volatility);
      pricer.Reset();
      var a = pricer.ProductPv();
      if(Math.Abs(a-price) < 1E-14) return;
      Assert.AreEqual(price, a, 5E-13);
      return;
    }

    private StockFutureOptionBlackPricer GetStockFuturePricer(
      Dt expiration, OptionType type, double strike,
      double futPrice, double divYield, DividendSchedule divSched,
      DiscountCurve discountCurve, double volatility)
    {
      const OptionStyle style = OptionStyle.European;
      Dt pricingDate = _asOf, settleDate = _settle;

      // Calculate implied vol
      var o = new StockFutureOption(expiration, 1.0, expiration, type, style, strike);
      o.Validate();
      var stock = Stock.GetStockWithConvertedDividend(Currency.None, null, divSched);  
      var stockCurve = new StockCurve(pricingDate, futPrice, discountCurve, divYield, stock);
      var pricer = new StockFutureOptionBlackPricer(
        o, pricingDate, settleDate, stockCurve, discountCurve,
        CalibratedVolatilitySurface.FromFlatVolatility(pricingDate, volatility),
        1.0)
      {
        QuotedFuturePrice = futPrice,
      };
      pricer.FuturesModelBasis = pricer.FuturesModelPrice() - futPrice;
      pricer.Validate();
      return pricer;
    }

    private static DiscountCurve GetDiscountCurve(Dt asOf)
    {
      DiscountCurve unused, liborCurve;
      new RateCurveBuilder().GetRateCurves(asOf, out unused, out liborCurve);
      return liborCurve;
    }

    private static Dt _D(string input)
    {
      return input.ParseDt();
    }

    private readonly Dt _asOf = _D("5-Dec-12"), _settle = _D("7-Dec-12");
    private readonly double _spotPrice = 90.730;

    private string[,] _data = {
      {"Futures", "90.730", "90.730", "90.730", "90.730", "90.730", "90.941", "91.122",
        "91.261", "91.379", "91.459", "91.508", "91.475"},
      {"Expiration", "14-Dec-12", "16-Jan-13", "14-Feb-13", "15-Mar-13", "17-Apr-13", "16-May-13",
        "17-Jun-13", "17-Jul-13", "15-Aug-13", "17-Sep-13", "17-Oct-13", "15-Nov-13"},
      {"82 Call", "6.08", "7.58", "8.98", "", "", "12.34", "", "", "", "", "", "15.37"},
      {"82.5 Call", "5.62", "7.18", "8.60", "", "", "11.99", "", "", "", "", "", "15.05"},
      {"83 Call", "5.17", "6.80", "8.23", "9.46", "10.72", "11.65", "", "", "", "", "", "14.72"},
      {"83.5 Call", "4.72", "6.42", "", "9.11", "", "11.31", "", "", "", "", "", "14.40"},
      {"84 Call", "4.28", "6.04", "7.52", "8.77", "", "10.98", "", "", "", "", "", "14.08"},
      {"84.5 Call", "3.86", "5.68", "", "8.43", "", "10.65", "", "", "", "", "", "13.77"},
      {"85 Call", "3.45", "5.33", "6.83", "8.10", "9.37", "10.33", "11.18", "", "12.30", "", "", "13.46"},
      {"85.5 Call", "3.05", "4.99", "6.50", "", "", "10.01", "", "", "", "", "", "13.15"},
      {"86 Call", "2.68", "4.66", "6.17", "7.46", "", "9.70", "", "", "", "", "", "12.84"},
      {"86.5 Call", "2.33", "4.34", "5.86", "", "", "9.40", "", "", "", "", "", "12.54"},
      {"87 Call", "1.99", "4.04", "5.55", "6.85", "8.12", "9.10", "", "", "", "", "", "12.25"},
      {"87.5 Call", "1.69", "3.74", "5.25", "6.55", "7.82", "8.80", "9.66", "", "", "", "", "11.96"},
      {"88 Call", "1.40", "3.46", "4.97", "6.26", "7.53", "8.51", "", "", "", "", "", "11.67"},
      {"88.5 Call", "1.16", "3.18", "4.69", "5.98", "", "8.23", "", "", "", "", "", "11.38"},
      {"89 Call", "0.95", "2.93", "4.41", "5.70", "6.97", "7.95", "", "9.38", "9.92", "", "", "11.10"},
      {"89.5 Call", "0.76", "2.70", "4.16", "5.43", "", "7.67", "", "9.11", "9.64", "", "", "10.83"},
      {"90 Call", "0.60", "2.47", "3.91", "5.18", "6.43", "7.40", "8.25", "8.83", "9.37", "9.83", "10.26", "10.55"},
      {"90.5 Call", "0.47", "2.26", "3.68", "4.93", "6.16", "7.14", "", "", "9.10", "", "", "10.29"},
      {"91 Call", "0.38", "2.06", "3.45", "4.69", "", "6.89", "7.72", "8.30", "8.83", "", "9.72", "10.02"},
      {"91.5 Call", "0.30", "1.87", "3.23", "4.46", "5.68", "6.64", "7.47", "", "8.57", "", "9.45", "9.76"},
      {"92 Call", "0.23", "1.70", "3.02", "4.24", "", "6.40", "", "", "8.32", "", "", "9.51"},
      {"92.5 Call", "0.18", "1.53", "2.82", "4.02", "", "6.17", "", "", "8.07", "", "", "9.26"},
      {"93 Call", "0.14", "1.39", "2.63", "3.81", "5.00", "5.94", "", "", "7.83", "", "", "9.01"},
      {"93.5 Call", "0.11", "1.26", "2.46", "", "", "5.71", "", "", "7.60", "", "", "8.77"},
      {"94 Call", "0.09", "1.13", "2.29", "3.41", "4.58", "5.49", "6.30", "", "7.37", "", "", "8.54"},
      {"94.5 Call", "0.08", "1.02", "2.14", "", "4.37", "5.28", "", "", "7.14", "", "", "8.30"},
      {"95 Call", "0.06", "0.91", "1.99", "3.05", "4.18", "5.07", "5.87", "6.41", "6.92", "7.37", "7.77", "8.08"},
      {"95.5 Call", "0.05", "0.82", "1.85", "", "", "4.87", "5.66", "", "6.70", "", "", "7.85"},
      {"96 Call", "0.05", "0.74", "1.71", "2.71", "3.80", "4.67", "", "", "", "", "", "7.63"},
      {"96.5 Call", "0.04", "0.67", "1.59", "2.55", "", "4.48", "", "", "", "", "", "7.41"},
      {"97 Call", "0.04", "0.60", "1.47", "2.39", "3.45", "4.29", "", "", "", "", "", "7.20"},
      {"97.5 Call", "0.03", "0.54", "1.36", "2.25", "", "4.11", "4.86", "", "", "", "", "6.99"},
      {"98 Call", "0.03", "0.48", "1.25", "2.11", "3.12", "3.94", "", "", "5.67", "", "", "6.78"},
      {"98.5 Call", "0.03", "0.43", "1.16", "1.99", "2.97", "3.77", "", "5.01", "", "", "", "6.58"},
      {"99 Call", "0.03", "0.39", "1.08", "1.88", "", "3.60", "4.32", "", "5.29", "", "", "6.38"},
      {"99.5 Call", "0.02", "0.35", "1.00", "1.77", "", "3.44", "", "", "", "", "", "6.19"},
      {"100 Call", "0.02", "0.32", "0.93", "1.67", "2.53", "3.30", "3.98", "4.47", "4.93", "5.34", "5.71", "6.00"},
      {"100.5 Call", "0.02", "0.30", "0.86", "1.58", "", "3.16", "", "", "", "", "", "5.81"},
      {"101 Call", "0.02", "0.27", "0.80", "1.49", "", "3.03", "", "", "", "", "", "5.63"},
      {"101.5 Call", "0.02", "0.25", "0.74", "1.40", "", "", "", "", "", "", "", "5.45"},
      {"102 Call", "0.02", "0.23", "0.68", "1.32", "2.07", "2.77", "", "", "", "", "", "5.28"},
      {"102.5 Call", "0.02", "0.21", "0.63", "1.24", "", "2.65", "", "", "", "", "", "5.10"},
      {"103 Call", "0.02", "0.19", "0.58", "1.17", "1.87", "2.54", "3.14", "", "", "", "", "4.94"},
      {"103.5 Call", "0.02", "0.18", "0.54", "1.10", "", "2.43", "", "", "", "", "", "4.79"},
      {"104 Call", "0.02", "0.16", "0.50", "1.03", "1.68", "2.32", "", "", "", "", "", "4.65"},
      {"104.5 Call", "0.02", "0.15", "0.46", "", "", "2.21", "", "", "", "", "", "4.51"},
      {"105 Call", "0.02", "0.14", "0.42", "0.91", "1.51", "2.11", "2.68", "3.10", "3.45", "3.81", "4.15", "4.37"},
      {"105.5 Call", "0.02", "0.14", "0.40", "0.86", "1.43", "2.02", "2.57", "", "", "", "", ""},
      {"106 Call", "0.02", "0.13", "0.38", "0.80", "1.35", "1.92", "", "", "", "", "", "4.10"},
      {"106.5 Call", "0.02", "0.12", "0.36", "", "", "1.83", "", "", "", "", "", ""},
      {"107 Call", "0.02", "0.11", "0.34", "0.71", "1.21", "1.75", "", "", "", "", "", "3.85"},
      {"107.5 Call", "0.01", "0.11", "0.32", "", "", "1.67", "", "", "", "", "", "3.73"},
      {"108 Call", "0.01", "0.10", "0.30", "0.63", "1.08", "1.59", "2.10", "2.48", "2.79", "3.12", "3.43", "3.61"},
      {"108.5 Call", "0.01", "0.10", "0.29", "", "", "", "", "", "", "", "3.32", ""},
      {"109 Call", "0.01", "0.09", "0.27", "0.57", "0.97", "1.44", "", "", "", "", "", "3.39"},
      {"109.5 Call", "0.01", "0.09", "0.26", "", "", "", "", "", "", "", "", ""},
      {"110 Call", "0.01", "0.09", "0.24", "0.52", "0.88", "1.32", "1.77", "2.12", "2.41", "2.72", "3.02", "3.17"},
      {"110.5 Call", "0.01", "0.08", "0.23", "", "", "", "", "", "", "", "", ""},
      {"111 Call", "0.01", "0.08", "0.22", "", "", "", "", "", "", "", "", ""},
      {"111.5 Call", "0.01", "0.08", "0.21", "", "", "", "", "", "", "", "", "2.87"},
      {"112 Call", "0.01", "0.07", "0.20", "0.43", "0.72", "1.12", "", "", "", "", "", "2.77"},
      {"82 Put", "0.20", "1.11", "1.84", "2.43", "3.14", "3.62", "", "", "", "", "", "5.88"},
      {"82.5 Put", "0.24", "1.22", "1.96", "2.57", "3.28", "3.77", "", "", "", "", "", "6.05"},
      {"83 Put", "0.29", "1.33", "2.10", "2.71", "3.43", "3.93", "", "", "", "", "", "6.22"},
      {"83.5 Put", "0.34", "1.45", "2.23", "", "", "4.09", "", "", "", "", "", "6.40"},
      {"84 Put", "0.40", "1.57", "2.38", "3.02", "", "4.26", "", "", "", "", "", "6.58"},
      {"84.5 Put", "0.48", "1.71", "2.53", "", "", "", "", "", "", "", "", "6.76"},
      {"85 Put", "0.57", "1.86", "2.69", "3.36", "4.07", "4.61", "5.17", "5.58", "5.97", "6.36", "6.71", "6.95"},
      {"85.5 Put", "0.67", "2.02", "2.86", "3.53", "", "4.79", "5.36", "", "", "", "", "7.14"},
      {"86 Put", "0.80", "2.19", "3.03", "3.71", "", "4.98", "", "", "", "", "", "7.34"},
      {"86.5 Put", "0.95", "2.37", "3.22", "3.90", "", "5.17", "", "", "", "", "", "7.54"},
      {"87 Put", "1.11", "2.57", "3.41", "4.10", "4.82", "5.37", "5.94", "6.35", "6.74", "7.14", "7.48", "7.74"},
      {"87.5 Put", "1.31", "2.77", "3.62", "4.30", "5.02", "5.57", "", "", "", "", "", "7.95"},
      {"88 Put", "1.52", "2.99", "3.83", "4.51", "5.23", "5.78", "6.35", "6.76", "7.15", "7.55", "7.89", "8.16"},
      {"88.5 Put", "1.78", "3.21", "4.05", "4.73", "", "6.00", "", "", "", "", "", "8.37"},
      {"89 Put", "2.07", "3.46", "4.27", "4.95", "5.67", "6.22", "", "7.19", "7.58", "", "", "8.59"},
      {"89.5 Put", "2.38", "3.73", "4.52", "5.18", "", "6.44", "", "7.41", "7.80", "", "", "8.81"},
      {"90 Put", "2.72", "4.00", "4.77", "5.43", "6.13", "6.67", "7.23", "7.64", "8.03", "8.42", "8.76", "9.04"},
      {"90.5 Put", "3.09", "4.29", "5.04", "", "6.36", "6.91", "", "", "8.26", "", "", "9.27"},
      {"91 Put", "3.50", "4.59", "5.31", "5.94", "", "7.16", "7.70", "", "8.49", "", "9.22", "9.50"},
      {"91.5 Put", "3.92", "4.90", "5.59", "", "", "7.41", "7.95", "", "", "", "", "9.74"},
      {"92 Put", "4.35", "5.23", "5.88", "6.49", "", "7.67", "", "", "8.98", "", "", "9.99"},
      {"92.5 Put", "4.80", "5.56", "6.18", "", "", "7.93", "", "", "9.23", "", "", "10.24"},
      {"93 Put", "5.26", "5.92", "6.49", "7.06", "7.70", "8.20", "", "", "", "", "", "10.49"},
      {"93.5 Put", "5.73", "6.28", "6.82", "", "", "8.48", "", "", "9.75", "", "", "10.75"},
      {"94 Put", "6.21", "6.66", "7.15", "7.66", "8.27", "8.76", "9.28", "", "", "", "", "11.01"},
      {"94.5 Put", "6.70", "7.05", "7.49", "", "8.57", "9.05", "", "", "", "", "", "11.28"},
      {"95 Put", "7.18", "7.44", "7.84", "8.29", "8.87", "9.34", "9.84", "10.21", "10.57", "10.94", "11.26", "11.55"},
      {"95.5 Put", "7.67", "7.85", "8.20", "", "", "9.63", "", "", "", "", "", "11.82"},
      {"96 Put", "8.17", "8.27", "8.57", "", "", "9.94", "", "", "", "", "", "12.10"},
      {"96.5 Put", "8.66", "8.69", "8.94", "", "", "10.24", "", "", "", "", "", "12.38"},
      {"97 Put", "9.16", "9.13", "9.32", "9.64", "10.14", "10.56", "11.03", "11.37", "", "", "", "12.67"},
      {"97.5 Put", "9.65", "9.56", "9.71", "", "", "10.87", "11.33", "", "", "", "", "12.95"},
      {"98 Put", "10.15", "10.01", "10.11", "10.35", "10.82", "11.20", "", "", "12.32", "", "", "13.25"},
      {"98.5 Put", "10.65", "10.46", "10.52", "10.73", "", "11.53", "", "12.29", "", "", "", "13.54"},
      {"99 Put", "11.15", "10.91", "10.94", "11.12", "", "11.86", "", "", "12.93", "", "", "13.85"},
      {"99.5 Put", "11.64", "11.38", "11.36", "", "", "12.20", "", "", "", "", "", "14.15"},
      {"100 Put", "12.14", "11.85", "11.78", "11.92", "12.22", "12.56", "12.95", "13.26", "13.57", "13.90", "14.19", "14.46"},
      {"100.5 Put", "12.64", "12.32", "12.22", "", "", "12.92", "", "", "", "", "", "14.77"},
      {"101 Put", "0.00", "12.80", "12.65", "", "", "", "", "", "", "", "", "15.09"},
      {"101.5 Put", "0.00", "0.00", "0.00", "", "", "", "", "", "", "", "", "15.41"},
      {"102 Put", "14.14", "13.75", "0.00", "", "", "14.03", "", "", "", "", "", "15.73"},
      {"102.5 Put", "0.00", "0.00", "0.00", "", "", "14.41", "", "", "", "", "", "16.06"},
      {"103 Put", "0.00", "0.00", "0.00", "", "", "14.79", "", "", "", "", "", "16.39"},
      {"103.5 Put", "0.00", "0.00", "0.00", "", "", "15.18", "", "", "", "", "", "16.74"},
      {"104 Put", "16.14", "0.00", "15.35", "", "", "15.57", "", "", "", "", "", "17.09"},
      {"104.5 Put", "16.64", "0.00", "15.81", "", "", "15.97", "", "", "", "", "", "17.45"},
      {"105 Put", "17.14", "16.67", "16.28", "16.15", "", "16.37", "", "", "", "", "", "17.81"},
      {"105.5 Put", "17.64", "0.00", "0.00", "", "", "16.77", "", "", "", "", "", ""},
      {"106 Put", "18.14", "0.00", "17.23", "", "", "17.18", "", "", "", "", "", "18.55"},
      {"106.5 Put", "0.00", "0.00", "0.00", "", "", "17.59", "", "", "", "", "", ""},
      {"107 Put", "0.00", "0.00", "18.19", "", "", "18.00", "", "", "", "", "", "19.29"},
      {"107.5 Put", "19.63", "19.13", "0.00", "", "", "18.42", "", "", "", "", "", "19.67"},
      {"108 Put", "0.00", "0.00", "0.00", "", "", "", "", "", "", "", "", "20.05"},
      {"108.5 Put", "20.63", "0.00", "19.64", "", "", "", "", "", "", "", "20.29", ""},
      {"109 Put", "21.13", "0.00", "0.00", "", "", "", "", "", "", "", "", ""},
      {"109.5 Put", "0.00", "", "0.00", "", "", "", "", "", "", "", "", ""},
      {"110 Put", "22.13", "21.61", "0.00", "", "", "20.57", "", "", "21.04", "", "", "21.60"},
      {"110.5 Put", "0.00", "0.00", "0.00", "", "", "", "", "", "", "", "", ""},
      {"111 Put", "0.00", "0.00", "0.00", "", "", "", "", "", "", "", "", ""},
      {"111.5 Put", "0.00", "0.00", "0.00", "", "", "", "", "", "", "", "", "22.80"},
      {"112 Put", "0.00", "0.00", "0.00", "", "", "", "", "", "", "", "", ""},
    };
  }
}
