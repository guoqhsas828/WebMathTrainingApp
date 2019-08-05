//
// Test of standard products terms
// Copyright (c)    2017. All rights reserved.
//

using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Products
{
  [TestFixture, Smoke, Ignore("Unknown. To investigate")]
  public class StandardProductTermsTests
  {
    // Ref http://www.eurexchange.com/blob/2835840/30622c2473f130411d55fe0a13595ad7/data/tradingcalendar_2017_en.pdf
    [TestCase("EUREX", "FGBL", "Euro-Buxl(R) Futures (FGBX)", Currency.EUR, 0.06, FuturesQuotingConvention.Price, 10,
      100000.0, 0.01, 10.0, SettlementType.Cash, 0, DayOfMonth.Tenth, -2, 0, DayOfMonth.Tenth, 0, 0, BDConvention.Preceding, "TGT", 20170101, "M17", 20170608, 20170612)]
    [TestCase("EUREX", "FGBL", "Euro-Buxl(R) Futures (FGBX)", Currency.EUR, 0.06, FuturesQuotingConvention.Price, 10,
      100000.0, 0.01, 10.0, SettlementType.Cash, 0, DayOfMonth.Tenth, -2, 0, DayOfMonth.Tenth, 0, 0, BDConvention.Preceding, "TGT", 20170101, "U17", 20170907, 20170911)]
    [TestCase("EUREX", "FGBL", "Euro-Buxl(R) Futures (FGBX)", Currency.EUR, 0.06, FuturesQuotingConvention.Price, 10,
      100000.0, 0.01, 10.0, SettlementType.Cash, 0, DayOfMonth.Tenth, -2, 0, DayOfMonth.Tenth, 0, 0, BDConvention.Preceding, "TGT", 20170101, "Z17", 20171207, 20171211)]
    public void BondFutureTerms(
      string exchange, string contractCode, string description, Currency ccy,
      double coupon, FuturesQuotingConvention convention, int term, double contractSize, double tickSize, double tickValue,
      SettlementType settlementType, int firstTrading,
      DayOfMonth lastTradingDayOfMonth, int lastTradingDayOffset, int lastTradingMonthOffset,
      DayOfMonth lastDeliveryDayOfMonth, int lastDeliveryDayOffset, int lastDeliveryMonthOffset,
      BDConvention bdConvention, string cal,
      int asOfInt, string expirationCode, int lastTrading, int lastDelivery)
    {
      var asOf = new Dt(asOfInt);
      var firstTradingDate = new Dt(firstTrading);
      var calendar = new Calendar(cal);
      var lastTradingDate = new Dt(lastTrading);
      var lastDeliveryDate = new Dt(lastDelivery);

      // Create built-in terms
      //var terms = new BondFutureTerms(exchange, contractCode, description, ccy, coupon, convention, term, contractSize,
      //  tickSize, tickValue, SettlementType.Cash, Dt.Empty,
      //  lastTradingDayOfMonth, lastDeliveryDayOffset, 0, lastTradingDayOfMonth, lastTradingDayOffset, 0,
      //  bdConvention, calendar);
      var terms = new BondFutureTerms(exchange, contractCode, description, ccy,
        coupon, convention, term,
        contractSize, tickSize, tickValue,
        lastTradingDayOfMonth, lastDeliveryDayOffset, lastTradingDayOffset, calendar);

      // Create target product
      BondFuture target;
      if (convention == FuturesQuotingConvention.Price)
      {
        // Price quoted futures (eg CME)
        target = new BondFuture(lastDeliveryDate, coupon, contractSize, tickSize / 100.0)
        { TickValue = tickValue };
      }
      else
      {
        // Index yield quoted futures (eg ASX)
        target = new BondFuture(lastDeliveryDate, coupon, term, contractSize, tickSize / 100.0);
      }
      target.Description = $"{contractCode}{expirationCode}";
      target.Ccy = ccy;
      target.SettlementType = settlementType;
      target.FirstTradingDate = firstTradingDate;
      target.LastTradingDate = lastTradingDate;

      // Compare generated product terms
      var generated = terms.GetProduct(asOf, expirationCode);
      Assert.AreEqual(generated.LastTradingDate, lastTradingDate, $"Expected last trading day {lastTradingDate} but generated {generated.LastTradingDate}");
      Assert.AreEqual(generated.LastDeliveryDate, lastDeliveryDate, $"Expected last delivery day {lastDeliveryDate} but generated {generated.LastDeliveryDate}");
      var mismatch = ObjectStatesChecker.Compare(target, generated);

      Assert.IsNull(mismatch, $"mismatch: {mismatch}");
    }

  }
}
