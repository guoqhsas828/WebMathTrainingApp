// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestDividendYieldCalibration
  {
    private static readonly Dt AsOf = new Dt(14,1,2013);
    private static readonly Currency ccy_ = Currency.USD;
    private static readonly BDConvention roll_ = BDConvention.Following;
    private static readonly Calendar calendar_ = Calendar.NYB;
    private static readonly double SpotPrice = 100.0;
    private static readonly double[] divYield = { 0.02, 0.015, 0.025, 0.02, 0.015, 0.01, 0.005 };
    private static readonly string[] delivery_ = { "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "10Y" };
    private static readonly double forwardRate_ = 0.05;
    private static readonly DiscountCurve DisCurve = new DiscountCurve(AsOf, forwardRate_) { Ccy = ccy_ };

    private void DoTest()
    {
      var settings = new CalibratorSettings();
      var maturities = delivery_.Select(t => Dt.Add(AsOf, t)).ToArray();
      var fwdQuotes =
        maturities.Select(
          (dt, i) =>
            SpotPrice/DisCurve.Interpolate(dt)*Math.Exp(-divYield[i]*Dt.Fraction(AsOf, dt, DayCount.Actual365Fixed)))
          .ToArray();
      var stockForwardCurve = StockCurve.FitStockForwardCurve(AsOf, Dt.Add(AsOf, 2), settings, "IBM", SpotPrice,
        roll_, calendar_, maturities,
        delivery_, delivery_.Select(t => InstrumentType.Forward).ToArray(), fwdQuotes, DisCurve, null);
      var pricers =
        stockForwardCurve.Tenors.Where(t => t.Product is StockForward)
          .Select(t => stockForwardCurve.Calibrator.GetPricer(stockForwardCurve, t.Product))
          .ToArray();
      var basePvs = pricers.Select(p => p.Pv()).ToArray();
      var spotYields =
        maturities.Select(m => stockForwardCurve.ImpliedDividendYield(Dt.Add(AsOf, 2), Dt.Roll(m, roll_, calendar_)))
          .ToArray();
      for (int i = 0; i < basePvs.Length; ++i)
      {
        Assert.AreEqual(0.0, basePvs[i], 1E-4,
          "" + stockForwardCurve.Count + " tenors: " + pricers[i].Product.Description);
        Assert.AreEqual(spotYields[i], divYield[i], 1e-3);

      }
    }

    [Test]
    public void RoundTrip()
    {
      DoTest();
    }

    //Test S(t) = S(0)/D(0,t)-Div for one payment period and multi payment periods.
    [TestCase(DividendSchedule.DividendType.Fixed)]
    [TestCase(DividendSchedule.DividendType.Proportional)]
    public void TestStockCurveInterpolationWithDividend(DividendSchedule.DividendType divType)
    {
      //One dividend period
      bool fix = divType == DividendSchedule.DividendType.Fixed;
      var schedule = GetDividendSchedule(fix);
      var stockCurve = GetStockCurve(AsOf, SpotPrice, DisCurve, schedule);
      var date = schedule.GetDt(0);
      var calcValue = SpotPrice/DisCurve.Interpolate(date) - stockCurve.Interpolate(date);
      var expect = fix ? schedule.GetAmount(0)
        : stockCurve.Interpolate(date)*schedule.GetAmount(0);
      Assert.AreEqual(calcValue, expect, 5E-14);

      //Multi dividend periods
      double div = 0.0;
      var lastPayDay = schedule.GetDt(schedule.Size() - 1);
      for (int i = 0; i < schedule.Size(); i++)
      {
        div += fix
          ? schedule.GetAmount(i)/DisCurve.DiscountFactor(schedule.GetDt(i), lastPayDay)
          : stockCurve.Interpolate(schedule.GetDt(i))*schedule.GetAmount(i)
            /DisCurve.DiscountFactor(schedule.GetDt(i), lastPayDay);
      }
      var payDayValue = SpotPrice / DisCurve.Interpolate(lastPayDay) - stockCurve.Interpolate(lastPayDay);
      Assert.AreEqual(div, payDayValue, 1E-13);
    }

    private static DividendSchedule GetDividendSchedule(bool fix)
    {
      string[] tenors = { "3M", "6M", "9M", "1Y" };
      double[] dividends = { 0.85, 0.85, 0.95,0.95 };
      double[] ptg = Enumerable.Repeat(0.005, tenors.Length).ToArray();
      Dt[] dates = tenors.Select(t => Dt.Add(AsOf, Tenor.Parse(t))).ToArray();

      return fix
        ? new DividendSchedule(AsOf, dates.Select(
          (d, i) => d.IsEmpty()
            ? null
            : Tuple.Create(d, DividendSchedule.DividendType.Fixed, dividends[i]))
          .Where(t => t != null))
        : new DividendSchedule(AsOf, dates.Select(
          (d, i) => d.IsEmpty()
            ? null
            : Tuple.Create(d, DividendSchedule.DividendType.Proportional, ptg[i]))
          .Where(t => t != null));
    }

    private static StockCurve GetStockCurve(Dt asOf, double spotPrice,
      DiscountCurve discoutCurve, DividendSchedule dividendSchedule = null)
    {
      var stock = Stock.GetStockWithConvertedDividend(Currency.None, null, dividendSchedule);
      return new StockCurve(asOf, spotPrice, discoutCurve, 0.0, stock);
    }
  }
}
