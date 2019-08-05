using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests
{
  //[QUnitFixture]
  public class RateIndexTest
  {
    //private readonly static ILog logger = LogManager.GetLogger(typeof(AnchorDatesTest));


    //[Test]
    //public void TestSwapRateInParallel()
    //{
    //  Dt asOf = new Dt(1, 1, 2009);
    //  Dt maturity = new Dt(1, 1, 2011);
    //  DiscountCurve discountCurve = new DiscountCurve(asOf);
    //  discountCurve.Add(asOf, .04);
    //  discountCurve.Add(maturity, .06);

    //  ScheduleParams sp = AnchorDateUtil.FindDates(asOf, Dt.Empty, Dt.Empty, maturity, Frequency.Monthly, Dt.Empty, false);

    //  IList<RateReset> resets = new List<RateReset>();
    //  resets.Add(new RateReset(asOf, 0.029));

    //  SwapRateCalculator index = new SwapRateCalculator("USDLIBOR", Currency.USD, Calendar.None, DayCount.ActualActual,
    //                                                  0, Tenor.Parse("1 month"), discountCurve, Frequency.Monthly, BDConvention.Modified,
    //                                                  resets);
    //  Dt next = Dt.Add(sp.FirstCouponDate, Frequency.Monthly, false);

    //  bool projected1 = false;
    //  bool projected2 = false;
    //  no reset lag, no rolled payment
    //      double rate1 = index.RateOnReset(sp.FirstCouponDate, sp.FirstCouponDate, next, next, out projected1);
    //  double rate2 = index.RateOnResetOld(sp.FirstCouponDate, sp.FirstCouponDate, next, next, out projected2);

    //  Assert.AreEqual(rate1, rate2);
    //  Assert.That(projected1);
    //  Assert.That(projected2);
    //}

  }
}