//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  public class TestStockReturnSwap
  {
    #region Tests

    [TestCase(CreationFlags.None)]
    [TestCase(CreationFlags.Arbitrage)]
    [TestCase(CreationFlags.ResetNotional)]
    [TestCase(CreationFlags.HasDividend)]
    [TestCase(CreationFlags.HasDividend | CreationFlags.ResetNotional)]
    [TestCase(CreationFlags.Arbitrage | CreationFlags.ResetNotional)]
    [TestCase(CreationFlags.HasDividend | CreationFlags.Arbitrage)]
    [TestCase(CreationFlags.HasDividend | CreationFlags.Arbitrage | CreationFlags.ResetNotional)]
    public void TestSinglePeriod(CreationFlags flags)
    {
      bool hasDiv, testArbitrage, resetNotional;
      GetFlags(flags, out hasDiv, out testArbitrage, out resetNotional);
      var maturity = Dt.Roll(Dt.Add(AsOf, "3M"), Bdc, Cal);
      var divDt = hasDiv ? Dt.Add(AsOf, -75) : Dt.Empty;
      var stock = GetStock(hasDiv, divDt);
      var divSched = GetDividendSchedule(divDt, stock);
      var stockCurve = hasDiv
        ? GetStockCurve(AsOf, SpotPrice, DisCurve, 0.0, divSched)
        : GetStockCurve(AsOf, SpotPrice, DisCurve);
      var beginPrice = stockCurve.Interpolate(AsOf);
      var stockLeg = AssetReturnLeg.Create(stock, Dt.Add(AsOf, -365), maturity, Ccy,
        beginPrice, Cal, Bdc, 0, new Dt[] {maturity}, resetNotional);
      var stockLegPricer = new StockReturnLegPricer(stockLeg, AsOf, AsOf, DisCurve, stockCurve)
      {Notional = beginPrice};

      var pv = stockLegPricer.ProductPv();
      var expect = GetExpectValue(hasDiv, testArbitrage, beginPrice,
        maturity, stockCurve, DisCurve, divSched);

      Assert.That(pv, Is.EqualTo(expect).Within(2E-14));
    }


    private static double GetExpectValue(bool hasDiv, bool testArbitrage, double beginPrice,
      Dt maturity, StockCurve stockCurve, DiscountCurve disCurve, DividendSchedule divSched)
    {
      if (testArbitrage)
        return beginPrice*(1 - disCurve.DiscountFactor(maturity));

      return (stockCurve.Interpolate(maturity) - stockCurve.Interpolate(Effective))
             *disCurve.DiscountFactor(maturity)
             + (hasDiv ? GetDividendPv(Effective, maturity, divSched, DisCurve) : 0.0);
    }


    [TestCase(CreationFlags.None)]
    [TestCase(CreationFlags.ResetNotional)]
    [TestCase(CreationFlags.Arbitrage)]
    [TestCase(CreationFlags.Arbitrage | CreationFlags.ResetNotional)]
    public void TestMultiPeriods(CreationFlags flags)
    {
      bool hasDiv, testArbitrage, resetNotional;
      GetFlags(flags, out hasDiv, out testArbitrage, out resetNotional);
      string[] tenors = {"3M", "6M"};
      Dt[] valueDates = tenors.Select(
        t => Dt.Roll(Dt.Add(AsOf, Tenor.Parse(t)), Bdc, Cal)).ToArray();
      var maturity = valueDates.Last();
      var stock = GetStock(false, Dt.Empty);
      var stockCurve = GetStockCurve(AsOf, SpotPrice, DisCurve);
      var beginPrice = stockCurve.Interpolate(AsOf);
      var stockLeg = GetStockLeg(stock, Dt.Add(Effective, -90), maturity,
        beginPrice, valueDates, resetNotional);

      var stockLegPricer = GetStockLegPricer(stockLeg, AsOf, AsOf, stockCurve, DisCurve);

      var disSum = 0.0;
      var begin = Effective;
      var paySchedules = stockLegPricer.GetPaymentSchedule(null, Effective);
      foreach (var s in paySchedules)
      {
        disSum += DisCurve.DiscountFactor(begin, s.PayDt);
        begin = s.PayDt;
      }

      var pv = stockLegPricer.ProductPv();
      var expect = resetNotional
        ? paySchedules.Count - disSum
        : 1 - DisCurve.DiscountFactor(maturity);

     Assert.That(pv, Is.EqualTo(expect).Within(1E-14));
    }

    [Test]
    public void TestStockReturnLegHistoricalPrices()
    {
      Assert.Throws<MissingFixingException>(() =>
      {
        Dt effective = new Dt(20150901);
        Dt maturity = new Dt(20170101);
        const Frequency freq = Frequency.Quarterly;
        var valueDates = GetValueDates(effective, maturity, freq).ToArray();
        var stock = GetStock(false, Dt.Empty);
        var stockCurve = GetStockCurve(AsOf, SpotPrice, DisCurve);
        var stockLeg = AssetReturnLeg.Create(stock, effective, maturity,
          Ccy, stockCurve.Interpolate(AsOf), Cal, Bdc, 0, valueDates);

        var stockLegPricer = GetStockLegPricer(stockLeg, AsOf, AsOf, stockCurve, DisCurve);
        var pv = stockLegPricer.Pv();
      });
    }

    private static IEnumerable<Dt> GetValueDates(Dt effective, Dt maturity, Frequency freq)
    {
      Dt valueDate = effective;
      for (;;)
      {
        valueDate = Dt.Roll(Dt.Add(valueDate, freq, false), Bdc, Cal);
        if (Dt.Cmp(valueDate, maturity) > 0) yield break;
        yield return valueDate;
      }
    }



    [Flags]
    public enum CreationFlags
    {
      None = 0,
      HasDividend = 1,
      Arbitrage = 2,
      ResetNotional = 4
    }

    private static void GetFlags(CreationFlags flags, out bool wantDiv, out bool 
      testArbitrage, out bool resetNotional)
    {
      wantDiv = (flags & CreationFlags.HasDividend) != 0;
      testArbitrage = (flags & CreationFlags.Arbitrage) != 0;
      resetNotional = (flags & CreationFlags.ResetNotional) != 0;
    }


    //Test two legs can tie out.
    [TestCase(true)]
    [TestCase(false)]
    public void TestPayOffEquationHeld(bool calParCoupon)
    {
      var maturity = Dt.Roll(Dt.Add(AsOf, "3M"), Bdc, Cal);
      var stock = GetStock(false, Dt.Empty);
      var stockCurve = GetStockCurve(AsOf, SpotPrice, DisCurve);
      var beginPrice = stockCurve.Interpolate(AsOf);
      var stockLeg = AssetReturnLeg.Create(stock, Effective, maturity, Ccy,
        beginPrice, Cal, Bdc);
      var stockLegPricer = new StockReturnLegPricer(stockLeg, AsOf, Effective,
        DisCurve, stockCurve) {Notional = OriginalNotional};

      SwapLegPricer fundingLegPricer;
      if (calParCoupon)
      {
        var fundingLeg = GetFundingLeg(Effective, maturity, 1.0, Frequency.Quarterly);
        fundingLegPricer = GetFundingLegPricer(AsOf, Effective, fundingLeg);
        var fundingPv = fundingLegPricer.Pv();
        var parCoupon = stockLegPricer.Pv()/fundingPv;
        fundingLegPricer.SwapLeg.Coupon = -parCoupon;
      }
      else
      {
        var fraction = Dt.Fraction(Effective, maturity, DayCount);
        var coupon = (1/DisCurve.DiscountFactor(maturity) - 1)/fraction;
        var fundingLeg = GetFundingLeg(Effective, maturity, -coupon, Frequency.Quarterly);
        fundingLegPricer = GetFundingLegPricer(AsOf, Effective, fundingLeg);
        var shares = OriginalNotional/beginPrice;
        var pv1 = fundingLegPricer.ProductPv()/shares;
        var pv2 = DisCurve.DiscountFactor(maturity)*beginPrice*fraction*coupon;
        (pv1 + pv2).IsExpected(To.Match(0.0).Within(1E-14));
      }

      var assetSwapPricer = new AssetReturnSwapPricer<Stock>(stockLegPricer, fundingLegPricer);
      var pv = assetSwapPricer.ProductPv();
      pv.IsExpected(To.Match(0.0).Within(1E-14));
    }


    //Test two legs can tie out. 
    [Test]
    public void TestPayOffEquationHeld2()
    {
      string[] tenors = {"3M", "6M"};
      Dt[] valueDates = tenors.Select(
        t => Dt.Roll(Dt.Add(AsOf, Tenor.Parse(t)), Bdc, Cal)).ToArray();
      var maturity = valueDates.Last();
      var stock = GetStock(false, Dt.Empty);
      var stockCurve = GetStockCurve(AsOf, SpotPrice, DisCurve);
      var beginPrice = stockCurve.Interpolate(AsOf);
      var stockLeg = AssetReturnLeg.Create(stock, Effective, maturity, Ccy,
        beginPrice, Cal, Bdc, 0, valueDates, true);
      var stockLegPricer = new StockReturnLegPricer(stockLeg, AsOf, AsOf,
        DisCurve, stockCurve) {Notional = OriginalNotional};
      var disSum = 0.0;
      var dayFraction = 0.0;
      var begin = Effective;
      var paySchedules = stockLegPricer.GetPaymentSchedule(null, Effective);
      foreach (var s in paySchedules)
      {
        var df = DisCurve.DiscountFactor(begin, s.PayDt);
        disSum += df;
        dayFraction += Dt.Fraction(begin, s.PayDt, DayCount)*DisCurve.DiscountFactor(Effective, s.PayDt);
        begin = s.PayDt;
      }

      var coupon = (paySchedules.Count - disSum)/dayFraction;
      var fundingLeg = GetFundingLeg(Effective, maturity, -coupon, Frequency.Quarterly);
      var flp = GetFundingLegPricer(AsOf, Effective, fundingLeg);
      var assetSwapPricer = new AssetReturnSwapPricer<Stock>(stockLegPricer, flp);
      assetSwapPricer.ProductPv().IsExpected(To.Match(0.0).Within(1E-16*OriginalNotional));
    }

    #endregion Tests

    #region Methods

    private static AssetReturnLeg<Stock> GetStockLeg(Stock stock, Dt effective,
      Dt maturity, double beginPrice, IEnumerable<Dt> valueDates, bool resetNotional)
    {
      return AssetReturnLeg.Create(stock, effective, maturity,
        Ccy, beginPrice, Cal, Bdc, 0, valueDates, resetNotional);
    }


    private static StockReturnLegPricer GetStockLegPricer(AssetReturnLeg<Stock> stockLeg,
      Dt asOf, Dt settle, StockCurve stockCurve, DiscountCurve disCurve)
    {
      return new StockReturnLegPricer(stockLeg, asOf, settle, disCurve, stockCurve);
    }

    private static double GetDividendPv(Dt begin, Dt end, DividendSchedule schedule,
      DiscountCurve dCurve)
    {
      double retVal = 0.0;
      for (int i = 0; i < schedule.Size(); i++)
      {
        var dt = schedule.GetDt(i);
        if (dt < begin) continue;
        if (dt > end) break;
        double amount = schedule.GetAmount(i);
        retVal += amount*dCurve.DiscountFactor(dt);
      }
      return retVal;
    }

    private static SwapLeg GetFundingLeg(Dt effective, Dt maturity, double coupon, Frequency freq)
    {
      return new SwapLeg(effective, maturity, Ccy, coupon, DayCount,
        freq, Bdc, Cal, false);
    }

    private static SwapLegPricer GetFundingLegPricer(Dt asOf, Dt settle, SwapLeg swapLeg)
    {
      return new SwapLegPricer(swapLeg, asOf, settle, OriginalNotional, DisCurve,
        null, null, null, null, null);
    }

    private static Stock GetStock(bool wantDiv, Dt divDt, List<Stock.Dividend> dividends = null )
    {
      if (wantDiv && divDt.IsEmpty())
        throw new Exception("The dividend date cannot be empty to get a dividend schedule");

      return wantDiv
        ? new Stock(Ccy, "IBM", dividends ?? GetDefaultStockDividend(divDt))
        : new Stock(Ccy, "IBM");
    }

    private static List<Stock.Dividend> GetDefaultStockDividend(Dt divDt)
    {
      string[] tenors = {"3M", "6M", "9M", "1Y"};
      double[] dividends = {0.85, 0.85, 0.95, 0.95};
      return CreateStockDividend(divDt, tenors, dividends);
    }

    private static List<Stock.Dividend> CreateStockDividend(Dt divDt,
      string[] tenors, double[] dividends)
    {
      Dt[] dates = tenors.Select(
        t => Dt.Roll(Dt.Add(divDt, Tenor.Parse(t)), Bdc, Cal)).ToArray();

      return dates.Select((d, i) => new Stock.Dividend(d, d + 30,
        DividendSchedule.DividendType.Fixed, dividends[i])).ToList();
    }

    private static DividendSchedule GetDividendSchedule(Dt asOf,
      Stock stock)
    {
      if (asOf.IsEmpty() || stock == null) return null;
      return new DividendSchedule(asOf, stock.DeclaredDividends.
        Select(div => Tuple.Create(div.PayDate, div.Type, div.Amount)));
    }

    private static StockCurve GetStockCurve(Dt asOf, double spotPrice,
      DiscountCurve discoutCurve, double dividendYield = 0.0, DividendSchedule dividendSchedule = null)
    {
      var dividends = dividendSchedule?.Select(d => new Stock.Dividend(d.Item1, d.Item1, d.Item2, d.Item3)).ToList();
      var stock = new Stock(Currency.None, null, dividends);

      return new StockCurve(asOf, spotPrice, discoutCurve, dividendYield, stock);
    }

    #endregion Methods

    #region Data

    private static readonly Dt AsOf = new Dt(20160204);
    private static readonly Dt Effective = AsOf;
    private static readonly Currency Ccy = Currency.USD;
    private static readonly BDConvention Bdc = BDConvention.None;
    private static readonly Calendar Cal = Calendar.None;
    private static readonly DayCount DayCount = DayCount.Actual360;
    private static readonly DiscountCurve DisCurve = new DiscountCurve(AsOf, 0.02);
    private static readonly double SpotPrice = 95.0;
    private static readonly double OriginalNotional = 10000000.0;

    #endregion Data
  }
}
