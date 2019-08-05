//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Util;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  [TestFixture]
  public class ResetsTest : ToolkitTestBase
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof (ResetsTest));

    [Test]
    public void SwapResets()
    {
      var effective = new Dt(1, 1, 2009);
      var asOf = new Dt(1, 7, 2009);
      var maturity = new Dt(1, 1, 2011);
      var sl = new SwapLeg(effective, maturity, Currency.USD, .05, DayCount.Actual360, Frequency.Quarterly,
                           BDConvention.Following, Calendar.NYB, false, new Tenor(Frequency.Quarterly), "USDLIBOR");
      IDictionary<Dt, RateResets.ResetInfo> resetInfo = sl.GetResetInfo(asOf,
                                                                        new RateResets(new Dictionary<Dt, double>()),
                                                                        null);
      Assert.IsTrue(resetInfo.Count > 0);
    }

    [Test]
    public void LongStub()
    {
      var effective = new Dt(30, 10, 2012);
      var maturity = new Dt(22, 2, 2018);
      var ccy = Currency.EUR;
      var dayCount = DayCount.Actual360;
      var freq = Frequency.SemiAnnual;
      var calendar = Calendar.TGT;
      var roll = BDConvention.Following;
      var publicationFrequency = Frequency.Daily;
      var resetDtRule = CycleRule.None;
      var settlementDays = 2;
      var sl = new SwapLeg(effective, maturity, ccy, 0.0, dayCount, freq,
        roll, calendar, false, new Tenor(Frequency.SemiAnnual), "EURIBOR");

      Func<string, double, RateReset> rateReset = (s, v) =>
        new RateReset(Dt.FromStr(s), v / 100);

      Func<string, RateReset[], InterestRateIndex> makeIndex = (s, a) =>
        new InterestRateIndex("EURIBOR_" + s, Tenor.Parse(s),
          ccy, dayCount, calendar, roll, publicationFrequency, resetDtRule,
          settlementDays)
        {
          HistoricalObservations = new RateResets(a),
        };

      var indices = new[]
      {
        makeIndex("3M", new[]
        {
          rateReset("25-Oct-12", 0.20100),
          rateReset("26-Oct-12", 0.19900),
          rateReset("29-Oct-12", 0.19600),
          rateReset("30-Oct-12", 0.19800),
          rateReset("31-Oct-12", 0.19700),
        }),
        makeIndex("4M", new[]
        {
          rateReset("25-Oct-12", 0.26500),
          rateReset("26-Oct-12", 0.26400),
          rateReset("29-Oct-12", 0.26100),
          rateReset("30-Oct-12", 0.25900),
          rateReset("31-Oct-12", 0.25800),
        }),
        makeIndex("6M", new[]
        {
          rateReset("25-Oct-12", 0.27500),
          rateReset("26-Oct-12", 0.27400),
          rateReset("29-Oct-12", 0.27100),
          rateReset("30-Oct-12", 0.26900),
          rateReset("31-Oct-12", 0.26800),
        }),
      };

      // Test the regular case
      var rate = RateResetUtil.FixingOnReset(indices, sl, effective, Dt.Empty);
      Assert.AreEqual(0.25055, rate * 100, 5E-6);

      // Test the case without historical observations
      indices[0] = makeIndex("3M", null);
      ExpectException(typeof(ToolkitException), "has no rate",
        () => RateResetUtil.FixingOnReset(indices, sl, effective, Dt.Empty));

      // Test the case without no reset info
      ExpectException(typeof(ArgumentException), "Need at least one InterestRateIndex",
        () => RateResetUtil.FixingOnReset(new InterestRateIndex[0], sl, effective, Dt.Empty));
    }

    private static void ExpectException(
      Type exceptionType, string msgSuffix, TestDelegate action)
    {
      Assert.That(action,Throws.InstanceOf(exceptionType).With.Message.EndWith(msgSuffix));
    }
  }
}