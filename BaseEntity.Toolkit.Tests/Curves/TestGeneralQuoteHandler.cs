//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;
using System.Reflection;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Curves
{
  /// <summary>
  ///  The tests here intend to cover all the cases regarding
  ///  allowing/forbidding crossing zero.
  /// </summary>
  [TestFixture]
  public class TestGeneralQuoteHandler
  {
    #region Tests

    [Test]
    public static void CdsBumpCrossZero()
    {
      var handler = QuoteHandler;
      var cds = new CDS(new Dt(20, 9, 2013), new Tenor(10, TimeUnit.Years), 0, Calendar.None);
      var tenor = new CurveTenor("CDS", cds, 0.0);
      double bump = 1000, premium = 0.05;
      double bumped;

      // Default: forbid down crossing zero
      cds.Premium = premium;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump", 5000*premium, bumped, 2E-16);
      AssertEqual("Down bump result", 0.5*premium, premium - cds.Premium, 2E-16);

      // Explicitly allow down crossing zero
      cds.Premium = premium;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowNegativeCDSSpreads);
      AssertEqual("Down bump allow negative", bump, bumped, 2E-16);
      AssertEqual("Down bump result", bump/10000, premium - cds.Premium, 2E-16);

      // Default: allow up bump crossing zero
      cds.Premium = -premium;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump", bump, bumped, 2E-16);
      AssertEqual("Up bump result", bump/10000, premium + cds.Premium, 2E-16);

      // Explicitly forbid up crossing zero
      cds.Premium = -premium;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 5000*premium, bumped, 2E-16);
      AssertEqual("Up bump result", 0.5*premium, premium + cds.Premium, 2E-16);

      bumped = handler.BumpQuote(tenor, bump, BumpFlags.AllowNegativeCDSSpreads);
      AssertEqual("Up bump allow negative", bump, bumped, 2E-16);

      // Relative bump
      cds.Premium = -premium;
      bump = 2.0;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpRelative | BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 250, bumped, 2E-16);
      double expect = 10000*bump*Math.Abs(cds.Premium);
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpRelative | BumpFlags.AllowNegativeCDSSpreads);
      AssertEqual("Up bump allow negative", expect, bumped, 2E-16);
    }

    [Test]
    public static void SwaplegBumpCrossZero()
    {
      var handler = QuoteHandler;
      var term = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      var swap = new SwapLeg(new Dt(20, 9, 2013), new Dt(20, 9, 2023),
        Frequency.Quarterly, 0.0, term.ReferenceIndex);
      var tenor = new CurveTenor("Swapleg", swap, 0.0);
      const double bump = 1000, coupon = 0.05;
      double bumped;

      // Default: forbid down crossing zero
      swap.Coupon = coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump", 5000*coupon, bumped, 2E-16);
      AssertEqual("Down bump result", 0.5*coupon, coupon - swap.Coupon, 2E-16);

      // Explicitly allow down crossing zero
      swap.Coupon = coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowDownCrossingZero);
      AssertEqual("Down bump", bump, bumped, 2E-16);
      AssertEqual("Down bump result", bump/10000, coupon - swap.Coupon, 2E-16);

      // Explicitly forbid up crossing zero
      swap.Coupon = -coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 5000*coupon, bumped, 2E-16);
      AssertEqual("Up bump result", 0.5*coupon, coupon + swap.Coupon, 2E-16);

      // Default: allow up bump crossing zero
      swap.Coupon = -coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump", bump, bumped, 2E-16);
      AssertEqual("Up bump result", bump/10000, coupon + swap.Coupon, 2E-16);
    }

    [Test]
    public static void SwapBumpCrossZero()
    {
      var handler = QuoteHandler;
      var term = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      var payer = new SwapLeg(new Dt(20, 9, 2013), new Dt(20, 9, 2023),
        Frequency.Quarterly, 0.0, term.ReferenceIndex);
      var receiver = new SwapLeg(new Dt(20, 9, 2013), new Dt(20, 9, 2023),
        Frequency.Quarterly, 0.0, term.ReferenceIndex);
      var swap = new Swap(receiver, payer);
      var tenor = new CurveTenor("Swap", swap, 0.0);
      const double bump = 1000, coupon = 0.05;
      double bumped;

      // For spread on floating leg
      swap.PayerLeg.Coupon = coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump floating", bump, bumped, 2E-16);

      swap.PayerLeg.Coupon = -coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump floating", bump, bumped, 2E-16);

      // For fixed leg.
      var swapLeg = swap.PayerLeg;
      swapLeg.Index = null;

      // Default: forbid down crossing zero
      swapLeg.Coupon = coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump fixed", 5000*coupon, bumped, 2E-16);
      AssertEqual("Down bump result", 0.5*coupon, coupon - swapLeg.Coupon, 2E-16);

      // Explicitly allow down crossing zero
      swapLeg.Coupon = coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowDownCrossingZero);
      AssertEqual("Down bump fixed", bump, bumped, 2E-16);
      AssertEqual("Down bump result", bump/10000, coupon - swapLeg.Coupon, 2E-16);

      // Explicitly forbid up crossing zero
      swapLeg.Coupon = -coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump fixed", 5000*coupon, bumped, 2E-16);
      AssertEqual("Up bump result", 0.5*coupon, coupon + swapLeg.Coupon, 2E-16);

      // Default: allow up bump crossing zero
      swapLeg.Coupon = -coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump fixed", bump, bumped, 2E-16);
      AssertEqual("Up bump result", bump/10000, coupon + swapLeg.Coupon, 2E-16);
    }

    [Test]
    public static void NoteBumpCrossZero()
    {
      var handler = QuoteHandler;
      var note = new Note(new Dt(20, 9, 2013), new Dt(20, 9, 2023),
        Currency.USD, 0.0, DayCount.Actual365Fixed, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB);
      var tenor = new CurveTenor("Note", note, 0.0);
      const double bump = 1000, coupon = 0.05;
      double bumped;

      // Default: forbid down crossing zero
      note.Coupon = coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump", 5000*coupon, bumped, 2E-16);
      AssertEqual("Down bump result", 0.5*coupon, coupon - note.Coupon, 2E-16);

      // Explicitly allow down crossing zero
      note.Coupon = coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowDownCrossingZero);
      AssertEqual("Down bump", bump, bumped, 2E-16);
      AssertEqual("Down bump result", bump/10000, coupon - note.Coupon, 2E-16);

      // Explicitly forbid up crossing zero
      note.Coupon = -coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 5000*coupon, bumped, 2E-16);
      AssertEqual("Up bump result", 0.5*coupon, coupon + note.Coupon, 2E-16);

      // Default: allow up bump crossing zero
      note.Coupon = -coupon;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump", bump, bumped, 2E-16);
      AssertEqual("Up bump result", bump/10000, coupon + note.Coupon, 2E-16);
    }

    [Test]
    public static void RateFutureBumpCrossZero()
    {
      var handler = QuoteHandler;
      var lastDelivery = new Dt(20, 9, 2014);
      var ten = new Tenor(3, TimeUnit.Months);
      var depositAccrualStart = lastDelivery;
      var depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, ten), BDConvention.Following, Calendar.LNB);
      var index = new InterestRateIndex(String.Empty, ten, Currency.USD, DayCount.Actual360, Calendar.LNB, BDConvention.Following, 2);
      var future = new StirFuture(RateFutureType.MoneyMarketCashRate, lastDelivery, depositAccrualStart, depositAccrualEnd, index,
        1000000, 0.5 / 1e4, 12.5);
      var tenor = new CurveTenor("ED Future", future, 0.0);
      const double bump = 1000, gain = 0.05;
      double bumped;

      // Default: forbid down crossing zero
      tenor.MarketPv = 1 - gain;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump", 5000*gain, bumped, 2E-12);
      AssertEqual("Down bump result", 0.5*gain, tenor.MarketPv - (1 - gain), 2E-16);

      // Explicitly allow down crossing zero
      tenor.MarketPv = 1 - gain;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowDownCrossingZero);
      AssertEqual("Down bump", bump, bumped, 2E-12);
      AssertEqual("Down bump result", bump/10000, tenor.MarketPv - (1 - gain), 2E-16);

      // Explicitly forbid up crossing zero
      tenor.MarketPv = 1 + gain;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 5000*gain, bumped, 2E-12);
      AssertEqual("Up bump result", 0.5*gain, 1 + gain - tenor.MarketPv, 2E-16);

      // Default: allow up bump crossing zero
      tenor.MarketPv = 1 + gain;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump", bump, bumped, 2E-12);
      AssertEqual("Up bump result", bump/10000, 1 + gain - tenor.MarketPv, 2E-16);
    }

    [Test]
    public static void BondBumpCrossZero()
    {
      var handler = QuoteHandler;
      var note = new Bond(new Dt(20, 9, 2013), new Dt(20, 9, 2023),
        Currency.USD, BondType.None, 0.0, DayCount.Actual365Fixed,
        CycleRule.None, Frequency.Quarterly, BDConvention.Following,
        Calendar.NYB);
      var tenor = new CurveTenor("Bond", note, 0.0);

      // These are ridiculous numbers, but we just test mechanics.
      const double bump = 1000, pv = 0.05;
      double bumped;

      // Default: forbid down crossing zero
      tenor.MarketPv = -pv;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump", 5000*pv, bumped, 2E-13);
      AssertEqual("Down bump result", 0.5*pv, pv + tenor.MarketPv, 2E-16);

      // Explicitly allow down crossing zero
      tenor.MarketPv = -pv;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowDownCrossingZero);
      AssertEqual("Down bump", bump, bumped, 2E-13);
      AssertEqual("Down bump result", bump/10000, pv + tenor.MarketPv, 2E-16);

      // Explicitly forbid up crossing zero
      tenor.MarketPv = pv;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 5000*pv, bumped, 2E-13);
      AssertEqual("Up bump result", 0.5*pv, pv - tenor.MarketPv, 2E-16);

      // Default: allow up bump crossing zero
      tenor.MarketPv = pv;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump", bump, bumped, 2E-13);
      AssertEqual("Up bump result", bump/10000, pv - tenor.MarketPv, 2E-16);
    }

    [Test]
    public static void FraBumpCrossZero()
    {
      var handler = QuoteHandler;
      var term = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      var note = new FRA(new Dt(20, 9, 2013), new Dt(20, 9, 2023),
        Frequency.Quarterly, 0.0, term.ReferenceIndex, new Dt(20, 12, 2013),
        Currency.USD, DayCount.Actual365Fixed, Calendar.NYB, BDConvention.Following);
      var tenor = new CurveTenor("FRA", note, 0.0);

      // These are ridiculous numbers, but we just test mechanics.
      const double bump = 1000, strike = 0.05;
      double bumped;

      // Default: forbid down crossing zero
      note.Strike = strike;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump", 5000*strike, bumped, 2E-13);
      AssertEqual("Down bump result", 0.5*strike, strike - note.Strike, 2E-16);

      // Explicitly allow down crossing zero
      note.Strike = strike;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowDownCrossingZero);
      AssertEqual("Down bump", bump, bumped, 2E-13);
      AssertEqual("Down bump result", bump/10000, strike - note.Strike, 2E-16);

      // Explicitly forbid up crossing zero
      note.Strike = -strike;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 5000*strike, bumped, 2E-13);
      AssertEqual("Up bump result", 0.5*strike, note.Strike + strike, 2E-16);

      // Default: allow up bump crossing zero
      note.Strike = -strike;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump", bump, bumped, 2E-13);
      AssertEqual("Up bump result", bump/10000, note.Strike + strike, 2E-16);
    }

    [Test]
    public static void CurvePointHolderBumpCrossZero()
    {
      var handler = QuoteHandler;
      var note = new CurvePointHolder(new Dt(20, 9, 2013), new Dt(20, 9, 2023), 0.0);
      var tenor = new CurveTenor("CPH", note, 0.0);

      // These are ridiculous numbers, but we just test mechanics.
      const double bump = 0.1, value = 0.05;
      double bumped;

      // Default: forbid down crossing zero
      note.Value = 0.05;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown);
      AssertEqual("Down bump", 0.5*value, bumped, 2E-16);
      AssertEqual("Down bump result", 0.5*value, value - note.Value, 2E-16);

      // Explicitly allow down crossing zero
      note.Value = 0.05;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.BumpDown | BumpFlags.AllowDownCrossingZero);
      AssertEqual("Down bump", bump, bumped, 2E-16);
      AssertEqual("Down bump result", bump, value - note.Value, 2E-16);

      // Explicitly forbid up crossing zero
      note.Value = -0.05;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.ForbidUpCrossingZero);
      AssertEqual("Up bump", 0.5*value, bumped, 2E-16);
      AssertEqual("Up bump result", 0.5*value, note.Value + value, 2E-16);

      // Default: allow up bump crossing zero
      note.Value = -0.05;
      bumped = handler.BumpQuote(tenor, bump, BumpFlags.None);
      AssertEqual("Up bump", bump, bumped, 2E-16);
      AssertEqual("Up bump result", bump, note.Value + value, 2E-16);
    }
    #endregion

    #region TestBumpFlags

    [TestCase(BumpFlags.None)]
    [TestCase(BumpFlags.AllowDownCrossingZero)]
    [TestCase(BumpFlags.ForbidUpCrossingZero)]
    [TestCase(BumpFlags.ForbidUpCrossingZero | BumpFlags.AllowDownCrossingZero)]
    [TestCase(BumpFlags.BumpDown)]
    [TestCase(BumpFlags.AllowDownCrossingZero | BumpFlags.BumpDown)]
    [TestCase(BumpFlags.ForbidUpCrossingZero | BumpFlags.BumpDown)]
    [TestCase(BumpFlags.ForbidUpCrossingZero | BumpFlags.AllowDownCrossingZero | BumpFlags.BumpDown)]
    public static void TestBumpFlags(BumpFlags bumpFlag)
    {
      const double bumpSize = 10;
      var asOf = new Dt(20161020);
      var downBump = (bumpFlag & BumpFlags.BumpDown) != 0;
      var rate = downBump ? 0.0008 : -0.0008;
      var curve = CreateDiscountCurve(asOf, rate);
      var oQuotes = curve.Tenors[0].CurrentQuote;
      CurveUtil.CurveBump(new[] { (CalibratedCurve)curve }, null, new[] { bumpSize }, bumpFlag, false, null);
      var bQuotes = curve.Tenors[0].CurrentQuote;
      var diff = bQuotes.Value - oQuotes.Value;

      var allowDown = (bumpFlag & BumpFlags.AllowDownCrossingZero) != 0;
      var forbidUp = (bumpFlag & BumpFlags.ForbidUpCrossingZero) != 0;
      if (downBump)
      {
        // Test flags AllowDownCrossingZero or (AllowDownCrossingZero | ForbidUpCrossingZero)
        if (allowDown) Assert.AreEqual(diff, -bumpSize / 10000.0);
        // Test default or ForbidUpCrossingZero 
        else Assert.AreEqual(diff, -Math.Abs(oQuotes.Value) / 2);
      }
      else
      {
        //Test ForbidUpCrossingZero or (ForbidUpCrossingZero | AllowDownCrossingZero)
        if (forbidUp) Assert.AreEqual(diff, Math.Abs(oQuotes.Value) / 2);
        // Test default or AllowDownCrossingZero 
        else Assert.AreEqual(diff, bumpSize / 10000);
      }
    }

    private static DiscountCurve CreateDiscountCurve(Dt asOf, double rate)
    {
      var curveDate = new Dt(20241230);
      var calibrator = new DiscountRateCalibrator(asOf, asOf);
      var curve = new DiscountCurve(calibrator)
      {
        Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
        Ccy = Currency.USD,
        Category = "None",
        Name = "USDLIBOR_3M"
      };
      curve.AddZeroYield(curveDate, rate, DayCount.Actual360, Frequency.Quarterly);
      curve.Fit();
      return curve;
    }


    #endregion

    #region Helpers
    private static ICurveTenorQuoteHandler _generalQuoteHandler;

    private static ICurveTenorQuoteHandler QuoteHandler
    {
      get
      {
        if (_generalQuoteHandler == null)
        {
          const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic;
          var type = typeof(CurveTenorQuoteHandlers).GetNestedType(
            "GeneralQuoteHandler", bf);
          if (type == null)
            throw new AssertionException("Nested type GeneralQuoteHandler not found");
          var obj = Activator.CreateInstance(type, true) as ICurveTenorQuoteHandler;
          if (obj == null)
            throw new AssertionException("Failed to construct GeneralQuoteHandler");
          _generalQuoteHandler = obj;
        }
        return _generalQuoteHandler;
      }
    }

    #endregion
  }
}
