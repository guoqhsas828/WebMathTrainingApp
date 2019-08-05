using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using static System.Math;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture]
  public class StockOptionConsistencyTests
  {
    [TestCase(100, 0.6)]
    [TestCase(102, 0.6)]
    [TestCase(104, 0.6)]
    public void FromCurves(double strike, double sigma)
    {
      Dt asOf = new Dt(20180620), expiry = new Dt(20181220);
      var discountCurve = BuildDiscountCurve(asOf,
        new[] { "1D", "1W", "1M", "3M", "6M" },
        new[] { .01, .02, .03, .035, .04 });
      var stockCurve = BuildStockCurve(asOf, 100,
        new[] { "1M", "3M", "6M" }, new[] { 99.0, 101, 102 },
        discountCurve);
      var option = new StockOption(expiry,
        OptionType.Call, OptionStyle.European, strike);
      var pricer = new StockOptionPricer(option, asOf, asOf,
        stockCurve, discountCurve, new FlatVolatility { Volatility = sigma });
      CheckConsistency(pricer);
    }

    [TestCase(100, 99, 0.04, 0.06, 0.4)]
    [TestCase(99, 100, 0.07, 0.06, 0.4)]
    public static void DirectRates(
      double strike,
      double spotPrice,
      double rate,
      double yield,
      double sigma)
    {
      Dt asOf = new Dt(20180620), expiry = new Dt(20181220);
      var option = new StockOption(expiry,
        OptionType.Call, OptionStyle.European, strike);
      var pricer = new StockOptionPricer(option,
        asOf, asOf, spotPrice, rate, yield, sigma);

      // We should roundtrip the input rates.
      Assert.AreEqual(rate, pricer.Rfr, 1E-15, "Interest rate");
      Assert.AreEqual(yield, pricer.Dividend, 1E-15, "Yield rate");
      CheckConsistency(pricer);
    }

    internal static void CheckConsistency(StockOptionPricer pricer)
    {
      var option = pricer.StockOption;
      Dt asOf = pricer.AsOf, expiry = option.Expiration;
      var time = pricer.Time;

      // Roundtrip the discount factor
      var rate = pricer.Rfr;
      var discountCurve = pricer.DiscountCurve;
      var discountFactor = discountCurve.Interpolate(asOf, expiry);
      {
        var expect = Exp(-rate * time);
        Assert.AreEqual(expect, discountFactor, 1E-15, "Discount factor");
      }

      // Roundtrip the dividend yield factor
      var yield = pricer.Dividend;
      var stockCurve = pricer.StockCurve;
      {
        var actual = stockCurve.ImpliedYieldCurve.Interpolate(asOf, expiry);
        var expect = Exp(-yield * time);
        Assert.AreEqual(expect, actual, 1E-15, "Dividend yield factor");
      }

      // Roundtrip the forward price
      var spotPrice = pricer.UnderlyingPrice;
      var forwardPrice = stockCurve.Interpolate(expiry);
      {
        var expect = spotPrice * Exp((rate - yield) * time);
        Assert.AreEqual(expect, forwardPrice, 1E-15 * spotPrice, "Forward price");
      }

      // Calculate the option value in two ways
      if (option.IsBarrier || option.IsDigital ||
        option.Style != OptionStyle.European)
      {
        //TODO: add tests later
        return;
      }

      // The regular option
      var strike = option.Strike;
      var sigma = pricer.Volatility;
      var pv1 = pricer.ProductPv() / pricer.Notional;

      // Calculate option value with forward price.
      // The values of rate and dividend have to be zero.
      var pv2 = BlackScholes.P(OptionStyle.European, option.Type,
        time, forwardPrice, strike, 0, 0, sigma) * discountFactor;
      Assert.AreEqual(pv1, pv2, 1E-15 * spotPrice, "Option Value");
    }

    #region Build curves

    private static DiscountCurve BuildDiscountCurve(
      Dt asOf, string[] tenors, double[] fwdRates)
    {
      var dc = new DiscountCurve(asOf);
      double t0 = 0, df = 1;
      for (int i = 0; i < tenors.Length; ++i)
      {
        var dt = Dt.Add(asOf, tenors[i]);
        var t = (dt - asOf) / 365.0;
        df *= 1 / (1 + (t - t0) * fwdRates[i]);
        dc.Add(dt, df);
      }

      return dc;
    }

    private static StockCurve BuildStockCurve(
      Dt asOf, double spotPrice,
      string[] tenors, double[] fwdPrices,
      DiscountCurve discountCurve)
    {
      return StockCurve.FitStockForwardCurve(asOf, asOf,
        new CalibratorSettings(), "stock", spotPrice,
        BDConvention.Modified, Calendar.NYB,
        Array.ConvertAll(tenors, t => Dt.Add(asOf, t)), tenors,
        Array.ConvertAll(tenors, t => InstrumentType.Forward),
        fwdPrices, discountCurve, new Stock());
    }

    #endregion
  }
}
