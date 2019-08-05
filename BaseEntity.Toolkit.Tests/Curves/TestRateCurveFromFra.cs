//
// Copyright (c)    2002-2016. All rights reserved.
//
using System;
using System.Linq;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestRateCurveFromFra
  {
    [Test]
    public void FraRoundtrip()
    {
      // Market Data
      Dt asOf = new Dt(20140107);
      var tenors = new[]
      {
        "1W",
        "1M",
        "2M",
        "3M",
        "29D x 57D",
        "57D x 85D",
        "85D x 120D",
        "120D x 148D",
        "148D x 176D",
        "176D x 211D",
        "211D x 239D",
        "239D x 274D",
        "274D x 302D",
        "302D x 330D",
      };
      var quotes = new[]
      {
        0.00120,
        0.00152,
        0.00193,
        0.00231,
        0.00308,
        0.00306,
        0.00312,
        0.00314,
        0.0031,
        0.00318,
        0.00312,
        0.00316,
        0.00329,
        0.00349,
      };
      var instr = new[]
      {
        "MM",
        "MM",
        "MM",
        "MM",
        "FRA",
        "FRA",
        "FRA",
        "FRA",
        "FRA",
        "FRA",
        "FRA",
        "FRA",
        "FRA",
        "FRA",
      };
      const int firstFraIndex = 4;

      // Curve calibration
      var terms = RateCurveTermsUtil.CreateDefaultCurveTerms("BBSW_3M");
      var settings = new CalibratorSettings
      {
        Tolerance = 1E-15
      };
      var dc = DiscountCurveFitCalibrator.DiscountCurveFit(
        asOf, terms, "Curve FRA", quotes, instr, tenors, 
        settings);

      // Curve order
      for (int i = 1, n = dc.Tenors.Count; i < n; ++i)
      {
        NUnit.Framework.Assert.Less(dc.Tenors[i-1].CurveDate,
          dc.Tenors[i].CurveDate, tenors[i] + " curve date order");
      }

      // Round tripping
      var index = terms.ReferenceIndex;
      var settle = Dt.Roll(Dt.Add(asOf, index.SettlementDays), index.Roll, index.Calendar);
      for (int i = firstFraIndex; i < tenors.Length; ++i)
      {
        var t = tenors[i].Split(' ', 'x')
          .Where(s => !String.IsNullOrEmpty(s)).ToArray();
        var date1 = Dt.Roll(Dt.Add(settle, t[0]), index.Roll, index.Calendar);
        var date2 = Dt.Roll(Dt.Add(settle, t[1]), index.Roll, index.Calendar);
        var df = dc.DiscountFactor(date1, date2);
        var frac = Dt.Fraction(date1, date2, index.DayCount);
        var rate = (1/df - 1)/frac;
        NUnit.Framework.Assert.AreEqual(quotes[i], rate, 2E-10,
          tenors[i] + " rate");
      }
    }
  }
}
