// 
// Copyright (c)    2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestStockReturnLeg
  {
    #region Tests

    [TestCase(20160117, 20160718)] // no dividend in between
    [TestCase(20160113, 20170718)] // one dividend in between
    [TestCase(20170112, 20170718)] // begin on 1D before ex-div date
    [TestCase(20170113, 20170718)] // begin on ex-div date
    [TestCase(20170114, 20170718)] // begin on 1D after ex-div date
    [TestCase(20160117, 20180112)] // end on 1D before ex-div date
    [TestCase(20160117, 20180113)] // end on ex-div date
    [TestCase(20160117, 20180114)] // end on 1D after ex-div date
    [TestCase(20160117, 20260718)] // many dividends in between
    public void ArbitrageOnePeriod(int begin, int end)
    {
      // The tests should work with arbitrary curve as-of date
      // provided it is on or before the begin (effective) date.
      Dt curveAsOf = new Dt(20151111), effective = new Dt(begin);
      var valueDates = new[] {new Dt(end)};
      CheckArbitrage(curveAsOf, effective, effective, valueDates, true);

      // Now check the case pricing after effective
      Dt pricingDate = effective + Dt.Diff(effective, valueDates[0])/2;
      CheckArbitrage(curveAsOf, pricingDate, effective, valueDates, true);
    }

    [TestCase(20160113, 20170718, true)] // one dividend in between
    [TestCase(20170112, 20170718, true)] // begin on 1D before ex-div date
    [TestCase(20170113, 20170718, true)] // begin on ex-div date
    [TestCase(20170114, 20170718, true)] // begin on 1D after ex-div date
    [TestCase(20160117, 20180112, true)] // end on 1D before ex-div date
    [TestCase(20160117, 20180113, true)] // end on ex-div date
    [TestCase(20160117, 20180114, true)] // end on 1D after ex-div date
    [TestCase(20160117, 20260718, true)] // many dividends in between

    [TestCase(20160117, 20160718, false)] // no dividend in between
    [TestCase(20170112, 20170718, false)] // begin on 1D before ex-div date
    [TestCase(20170113, 20170718, false)] // begin on ex-div date
    [TestCase(20170114, 20170718, false)] // begin on 1D after ex-div date
    [TestCase(20160117, 20180112, false)] // end on 1D before ex-div date
    public void ArbitrageTwoPeriod(int begin, int end, bool resetNotional)
    {
      ArbitrageTwoPeriod(false, begin, end, resetNotional);

      // Pricing after effective
      ArbitrageTwoPeriod(true, begin, end, resetNotional);
    }

    [Test] // many dividends in between
    public void PricingOnMaturity()
    {
      // The tests should work with arbitrary curve as-of date
      // provided it is on or before the begin (effective) date.
      Dt curveAsOf = new Dt(20151111), effective = new Dt(20160117);
      var valueDates = new[] { new Dt(20260718) };
      CheckArbitrage(curveAsOf, valueDates[0], effective, valueDates, true);
    }
    #endregion

    #region Helpers
    private void ArbitrageTwoPeriod(bool pricingAfterEffective,
      int begin, int end, bool resetNotional)
    {
      Dt curveAsOf = new Dt(20151111),
        effective = new Dt(begin), maturity = new Dt(end);
      var valueDates = new[]
      {
        effective + Dt.Diff(effective, maturity)/2,
        maturity
      };
      Dt pricingDt = pricingAfterEffective
        ? (valueDates[0] + Dt.Diff(valueDates[0], valueDates[1])/2)
        : effective;
      CheckArbitrage(curveAsOf, pricingDt, effective,
        valueDates, resetNotional);
    }


    private static void CheckArbitrage(
      Dt curveAsOf, Dt pricingDate,
      Dt effective, IReadOnlyList<Dt> valueDates, bool resettingNotional)
    {
      var discountCurve = new DiscountCurve(curveAsOf, 0.02);
      var stock = GetStock();

      // Build a stock curve and check it is valid
      var stockCurve = new StockCurve(curveAsOf, 90,
        discountCurve, 0.0, stock);
      if (valueDates.Count == 1 || !resettingNotional)
      {
        // Make sure the stock curve is perfect
        CheckPriceReturnParity(stockCurve, effective, valueDates);
      }

      // Find the initial and current prices
      var initPrice = stockCurve.Interpolate(effective);
      var currentPrice = stockCurve.Interpolate(pricingDate);

      // Find the current return period
      var index = valueDates.Select((d, i) => d > pricingDate ? i : -1)
        .FirstOrDefault(i => i > -1);
      var df = discountCurve.DiscountFactor(pricingDate, valueDates[index]);

      // Build return leg pricer and check the net pv
      var stockLeg = AssetReturnLeg.Create(stock, effective, valueDates.Last(),
        Currency.USD, initPrice, Calendar.None, BDConvention.None,
        0, valueDates, resettingNotional);
      var pricer = new StockReturnLegPricer(stockLeg,
        pricingDate, pricingDate, discountCurve, stockCurve)
      { Notional = initPrice };
      var pv = pricer.Pv() - pricer.UnrealizedGain()*(
        resettingNotional ? df : 1.0);

      // The expect is calculated by simple arbitrage
      var expect = CalculateExpect(stockCurve, pricingDate,
        valueDates, resettingNotional);
      if (!resettingNotional) expect *= initPrice/currentPrice;

      // No arbitrage condition
      pv.IsExpected(To.Match(expect).Within(pricer.Notional*5E-16));
    }

    /// <summary>
    ///  Calculates the expected total return with a perfect stock curve 
    ///  (no hidden costs/benefits from credit risk, liquidities, etc.)
    /// </summary>
    /// <param name="stockCurve">The stock curve</param>
    /// <param name="begin">The begin date</param>
    /// <param name="valueDates">The value dates</param>
    /// <param name="resetNotional">if set to <c>true</c>, reset notional in each return periods</param>
    /// <returns>System.Double.</returns>
    private static double CalculateExpect(StockCurve stockCurve,
      Dt begin, IReadOnlyList<Dt> valueDates, bool resetNotional)
    {
      var dc = stockCurve.DiscountCurve;
      if (!resetNotional)
      {
        // Expect is (1-DiscountFactor)*initialPrice.  This applies to two cases:
        //  (a) A single return period with possibly many dividend payments, or
        //  (b) Multiple return periods without dividend and notional resetting.
        var initialPrice = stockCurve.Interpolate(begin);
        var end = valueDates.Last();
        return (1 - dc.Interpolate(begin, end))*initialPrice;
      }

      // With notional resetting, we calculate the expect from a sequence
      // of single period total returns, i.e., the discounted sum of
      // a sequence of the values in the form (1-periodDf)*periodBeginPrice.
      var pv = 0.0;
      Dt last = begin;
      for (int i = 0, n = valueDates.Count; i < n; ++i)
      {
        var date = valueDates[i];
        if (date <= begin) continue;
        var initialPrice = stockCurve.Interpolate(last);
        pv += (1 - dc.Interpolate(last, date))*initialPrice
          *(i == 0 ? 1.0 : dc.Interpolate(begin, last));
        last = date;
      }
      return pv;
    }

    /// <summary>
    ///  Checks the price return parity for the case of either (a) single
    ///  return period, or (b) multiple return periods without dividend and
    ///  notional resetting.
    /// </summary>
    /// <param name="stockCurve">The stock curve.</param>
    /// <param name="begin">The begin.</param>
    /// <param name="valueDates">The value dates.</param>
    /// <returns>The expected PV</returns>
    private static void CheckPriceReturnParity(
      StockCurve stockCurve, Dt begin, IReadOnlyList<Dt> valueDates)
    {
      var end = valueDates.Last();
      var dc = stockCurve.DiscountCurve;

      var pricePv = CalculatePriceReturnPv(stockCurve, begin, valueDates);
      var divPv = CalculateDividendPv(stockCurve.Stock, begin, end, 
        stockCurve.Dividends, dc);
      var pv = pricePv + divPv;

      var initialPrice = stockCurve.Interpolate(begin);
      var expect = (1 - dc.Interpolate(begin, end))*initialPrice;

      pv.IsExpected(To.Match(expect).Within(5E-16*initialPrice));
    }

    private static double CalculatePriceReturnPv(
      StockCurve stockCurve, Dt begin, IEnumerable<Dt> valueDates)
    {
      var dc = stockCurve.DiscountCurve;
      var initialPrice = stockCurve.Interpolate(begin);
      var endPrice = initialPrice;
      double pv = 0;
      foreach (var date in valueDates)
      {
        var lastPrice = endPrice;
        Dt end = date;
        endPrice = stockCurve.Interpolate(end);
        var rate = (endPrice - lastPrice)/lastPrice;
        pv += rate*dc.Interpolate(begin, end);
      }
      return pv*initialPrice;
    }

    private static double CalculateDividendPv(Stock stock, Dt settle, Dt end,
      DividendSchedule schedule, DiscountCurve dCurve)
    {
      double retVal = 0.0;
      Dt from = settle, to = end;
      StockCurve.GetFromToDates(stock, ref from, ref to);
      for (int i = 0; i < schedule.Size(); i++)
      {
        var dt = schedule.GetDt(i);
        if (dt <= from) continue; // never include settle
        if (dt > to) break;
        double amount = schedule.GetAmount(i);
        retVal += amount * dCurve.DiscountFactor(dt);
      }
      return retVal/dCurve.DiscountFactor(settle);
    }


    private static DividendSchedule GetDivSchedule(
      Dt asOf, Stock stock)
    {
      return new DividendSchedule(asOf,
        stock.DeclaredDividends.Select(
          d => Tuple.Create(d.PayDate, d.Type, d.Amount)));
    }

    private static Stock GetStock()
    {
      return new Stock(Currency.USD, "XYZ", new List<Stock.Dividend>
      {
        Dividend(20170113, 2.0),
        Dividend(20180113, 2.2),
        Dividend(20190113, 2.4),
        Dividend(20200113, 2.6),
        Dividend(20210113, 2.5),
        Dividend(20220113, 2.8),
        Dividend(20230113, 3.0),
        Dividend(20240113, 3.5),
        Dividend(20250113, 4.0),
        Dividend(20260113, 3.7),
        Dividend(20270113, 4.0),
      });
    }

    private static Stock.Dividend Dividend(int date, double amount)
    {
      Dt exDiv = new Dt(date), payDt = Dt.Add(exDiv, 30); 
      return new Stock.Dividend(exDiv, payDt,
        DividendSchedule.DividendType.Fixed, amount);
    }

    #endregion
  }
}
