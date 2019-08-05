// 
// Copyright (c)    2017. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture, Smoke] //, Ignore("No parameter specified")]

  public class TestVarianceSwapPricer : ToolkitTestBase
  {
    #region data

    private Dt AsOf;
    private EquityPriceIndex stockIndex;
    private StockCurve stockCurve;
    private DiscountCurve discountCurve;
    private VolatilityCurve varianceSwapCurve; // Note these quotes are given as volatilities!
    private RateResets rateResets; // Stock prices
    private Calendar calendar;

    #endregion

    #region SetUp

    [SetUp]
    public void SetUpCurves()
    {
      AsOf = new Dt(31, 3, 2014);
      calendar = Calendar.NYB;

      ReferenceRate.CacheInitialise();
      ToolkitCache.StandardProductTermsCache.Initialise();

      discountCurve = new DiscountCurve(AsOf, 0.05);

      varianceSwapCurve = new VolatilityCurve(AsOf)
      {
        Interp = InterpFactory.FromMethod(InterpMethod.Quadratic, ExtrapMethod.Smooth, double.MinValue, double.MaxValue),
        DistributionType = DistributionType.LogNormal,
        Name = "VarianceSwapVolQuotes"
      };

      for (var i = 0; i < 10; i++)
        varianceSwapCurve.AddVolatility(Dt.Add(AsOf, i, TimeUnit.Years), 0.1 + (0.01 * (double)i));

      // Fit and validate
      varianceSwapCurve.Fit();
      varianceSwapCurve.Validate();

      rateResets = new RateResets();
      const double stock0 = 0.14;
      for (var i=1;i<260;i++)
      {
        var price = stock0 + stock0 * (i % 2 == 0 ? i / 10.0 * 0.5 / 252.0 : -i / 10.0 * 0.5 / 252.0);
        rateResets.Add(Dt.AddDays(AsOf, -i, calendar), price);
      }
    }

    #endregion

    #region Tests

    //[Test]
    //public void VarianceSwapMarketValue()
    //{
    //  var varNotional = 10000.0;
    //  var effective = AsOf;
    //  var maturity = Dt.Add(AsOf, 1, TimeUnit.Years);
    //  var varSwap = new VarianceSwap(effective, maturity, maturity, Currency.USD, "INDEX", 1.0);
    //  var varSwapPricer = new VarianceSwapPricer(varSwap, AsOf, AsOf, varNotional, stockCurve, discountCurve, varianceSwapCurve, null, false);
    //  var varSwapPv = varSwapPricer.Pv();
    //  Assert.AreEqual(100.0, varSwapPv, 1E-8);
    //}

    [Test]
    public void SimpleVarianceSwapRoundtrip()
    {
      var asOf = new Dt(31, 3, 2014);
      var vegaNotional = 10000.0;
      var strike = 20.0; // annualized strike in percent
      var varNotional = vegaNotional / (2 * strike); // 2,500
      var effective = asOf;
      var maturity = Dt.Add(asOf, 1, TimeUnit.Years);
      var varSwapCurve = new VolatilityCurve(asOf, strike/100.0); // as a fraction
      var discount = new DiscountCurve(asOf, 0.005);
      var varSwap = new VarianceSwap(effective, maturity, maturity, Currency.USD, "INDEX", strike);
      var varSwapPricer = new VarianceSwapPricer(varSwap, asOf, asOf, varNotional, null, discount, varSwapCurve, null, false);
      var varSwapPv = varSwapPricer.Pv();
      Assert.AreEqual(0.0, varSwapPv, 1E-8);
    }

    [Test]
    public void SimpleVarianceSwapPositivePandL()
    {
      var asOf = new Dt(31, 3, 2014);
      var vegaNotional = 100000.0;
      var strike = 20.0; // annualized strike in percent
      var volQuote = 25.0;
      var varNotional = vegaNotional / (2 * strike); // 2,500
      var effective = asOf;
      var maturity = Dt.Add(asOf, 1, TimeUnit.Years);
      var varSwapCurve = new VolatilityCurve(asOf, volQuote / 100.0); // as a fraction
      var discount = new DiscountCurve(asOf, 0.000);
      var varSwap = new VarianceSwap(effective, maturity, maturity, Currency.USD, "INDEX", strike);
      var varSwapPricer = new VarianceSwapPricer(varSwap, asOf, asOf, varNotional, null, discount, varSwapCurve, null, false);
      var varSwapPv = varSwapPricer.Pv();
      var varSwapPvCalculation = vegaNotional * (volQuote * volQuote - strike * strike) / (2.0 * strike);
      Assert.AreEqual(varSwapPvCalculation, varSwapPv, 1E-8);
    }

    [Test]
    public void SimpleVarianceSwapNegativePandL()
    {
      var asOf = new Dt(31, 3, 2014);
      var vegaNotional = 100000.0;
      var strike = 20.0; // annualized strike in percent
      var volQuote = 15.0;
      var varNotional = vegaNotional / (2 * strike); // 2,500
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Years);
      var varSwapCurve = new VolatilityCurve(asOf, volQuote / 100.0); // as a fraction
      var discount = new DiscountCurve(asOf, 0.000);
      var varSwap = new VarianceSwap(effective, maturity, maturity, Currency.USD, "INDEX", strike);
      var varSwapPricer = new VarianceSwapPricer(varSwap, asOf, asOf, varNotional, null, discount, varSwapCurve, null, false);
      var varSwapPv = varSwapPricer.Pv();
      var varSwapPvCalculation = vegaNotional * (volQuote * volQuote - strike * strike) / (2.0 * strike);
      Assert.AreEqual(varSwapPvCalculation, varSwapPv, 1E-8);
    }

    [Test]
    public void SimpleVarianceSwapWithResetsRoundtrip()
    {
      var vegaNotional = 10000.0;
      var strike = 20.0; // annualized strike in percent
      var varNotional = vegaNotional / (2 * strike); // 2,500
      var effective = new Dt(2, 1, 2014);
      var maturity = Dt.Add(effective, 1, TimeUnit.Years);
      var varSwapCurve = new VolatilityCurve(effective, strike / 100.0); // as a fraction
      var varSwap = new VarianceSwap(effective, maturity, maturity, Currency.USD, "INDEX", strike, calendar, BDConvention.Following);

      var asOf = new Dt(31, 3, 2014);
      var discount = new DiscountCurve(asOf, 0.005);
      var varSwapPricer = new VarianceSwapPricer(varSwap, asOf, asOf, varNotional, null, discount, varSwapCurve, rateResets, false);

      var n = Dt.BusinessDays(effective, asOf, calendar); // We do not use as of fixings so no need to add 1.
      var N = Dt.BusinessDays(effective, maturity, calendar) + 1;
      var date = effective;
      var prevDate = effective;
      var sum = 0.0;
      for (int i = 1; i < n; i++)
      {
        date = Dt.AddDays(date, 1, calendar);
        var logDiff = Math.Log(rateResets.AllResets[date] / rateResets.AllResets[prevDate]);
        sum += logDiff * logDiff;
        prevDate = date;
      }
      var historicVar = 252.0 / n * sum;
      var impliedVol = varSwapCurve.Interpolate(maturity);
      var impliedVar = impliedVol * impliedVol;

      var payoff = historicVar * n / N + impliedVar * (N - n) / N - strike*strike / 10000.0;
      var varSwapPvCalc = varNotional * payoff * 10000 * discount.DiscountFactor(asOf, maturity);

      var varSwapPv = varSwapPricer.Pv();
      Assert.AreEqual(varSwapPvCalc, varSwapPv, 1E-8);
    }

    #endregion
  }
}
