// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using Calendar = BaseEntity.Toolkit.Base.Calendar;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  internal static class Util
  {
    public static double ParsePct(this string pct)
    {
      if (pct.Contains(NumberFormatInfo.CurrentInfo.PercentSymbol))
      {
        pct = pct.Replace(NumberFormatInfo.CurrentInfo.PercentSymbol, String.Empty);
        return Double.Parse(pct) / 100;
      }
      throw new FormatException(String.Format("Cannot parse {0}", pct));
    }
  }

  [TestFixture]
  public class TestTreasuryCurveCalibration : ToolkitTestBase
  {
    private readonly Dt asOf_ = new Dt(9, 6, 2011);
    private readonly CultureInfo us_ = new CultureInfo("en-US");

    private struct CalibrationBond
    {
      public string Tenor;
      public Dt Issue;
      public Dt Maturity;
      public double YieldQuote;
      public double Coupon;
    };

    private readonly CalibrationBond[] calibrationBonds_ =
      new[]
        {
          new CalibrationBond {Tenor = "1Y", Issue = new Dt(6, 3, 2011), Maturity = new Dt(6, 9, 2012), YieldQuote = "4%".ParsePct(), Coupon = "5%".ParsePct()},
          new CalibrationBond {Tenor = "2Y", Issue = new Dt(6, 3, 2011), Maturity = new Dt(6, 9, 2013), YieldQuote = "5%".ParsePct(), Coupon = "6%".ParsePct()},
          new CalibrationBond {Tenor = "5Y", Issue = new Dt(6, 3, 2011), Maturity = new Dt(6, 9, 2016), YieldQuote = "6%".ParsePct(), Coupon = "7%".ParsePct()}
        };

    [Test]
    public void OneBond()
    {
      IEnumerable<CalibrationBond> calibrateFrom = calibrationBonds_.Take(1);
      RoundTripCalibration(calibrateFrom);
    }

    [Test]
    public void TwoBonds()
    {
      IEnumerable<CalibrationBond> calibrateFrom = calibrationBonds_.Take(2);
      RoundTripCalibration(calibrateFrom);
    }

    [Test]
    public void ThreeBonds()
    {
      IEnumerable<CalibrationBond> calibrateFrom = calibrationBonds_.Take(3);
      RoundTripCalibration(calibrateFrom);
    }

    [Test]
    public void PriceNonCalibrationBond()
    {
      IEnumerable<CalibrationBond> calibrateFrom = calibrationBonds_.Take(2);
      DiscountCurve dc = Calibrate(calibrateFrom);

      var settle = Dt.AddDays(dc.AsOf, dc.SpotDays, dc.SpotCalendar);
      var issue = settle;
      foreach (var i in new[] {1, 2, 3, 4, 5, 6})
      {
        Dt maturity = Dt.Add(issue, new Tenor(i, TimeUnit.Years));
        double price = dc.DiscountFactor(settle, maturity);
        double coupon = 2 * (Math.Pow(1 / price, 1.0 / (2.0 * i)) - 1);

        var bond = new Bond(settle,
                            maturity,
                            Currency.USD,
                            BondType.USGovt,
                            coupon,
                            DayCount.ActualActualBond,
                            CycleRule.None,
                            Frequency.SemiAnnual,
                            BDConvention.Following,
                            Calendar.NYB);
        var bondPricer = new BondPricer(bond, asOf_, settle, dc, null, 0, TimeUnit.None, 0.0)
                           {
                             MarketQuote = 1.0,
                             QuotingConvention = QuotingConvention.FullPrice,
                           };

        var y = bondPricer.YieldToMaturity();
        Assert.AreEqual(coupon, y, 1e-6);
        double p = bondPricer.FullPrice() * bondPricer.Notional;
        double pv = bondPricer.Pv() / dc.DiscountFactor(asOf_, bondPricer.Settle); // since full price is for Settle date
        Assert.AreEqual(1.0, pv / p, 1e-3);
      }
    }

    /// <summary>
    /// Work in progress - not sure what right test is here
    /// </summary>
    [Test, Ignore("Known not work")]
    public void PriceForwardNonCalibrationBond()
    {
      IEnumerable<CalibrationBond> calibrateFrom = calibrationBonds_.Take(2);
      DiscountCurve dc = Calibrate(calibrateFrom);

      var settle = Dt.AddDays(dc.AsOf, dc.SpotDays, dc.SpotCalendar);
      var issue = settle;
      Dt maturity;
      double price;
      double coupon;
      issue = Dt.Add(settle, "1Y");
      maturity = Dt.Add(issue, "2Y");
      price = dc.DiscountFactor(issue, maturity);
      coupon = 2 * (Math.Pow(1 / price, 1.0 / (2.0 * 1)) - 1);

      var bond = new Bond(issue,
                          maturity,
                          Currency.USD,
                          BondType.USGovt,
                          coupon,
                          DayCount.ActualActualBond,
                          CycleRule.None,
                          Frequency.SemiAnnual,
                          BDConvention.Following,
                          Calendar.NYB);
      var bondPricer = new BondPricer(bond, asOf_, settle, dc, null, 0, TimeUnit.None, 0.0)
                         {
                           MarketQuote = 1.0,
                           QuotingConvention = QuotingConvention.ForwardFlatPrice
                         };
      double p = bondPricer.FwdFullPrice(issue);
      double pv = bondPricer.Pv() / dc.DiscountFactor(asOf_, issue);
    }
    
    private void RoundTripCalibration(IEnumerable<CalibrationBond> calibrateFrom)
    {
      DiscountCurve dc = Calibrate(calibrateFrom);

      var bonds = calibrateFrom.Select(b => new
                                  {
                                    Bond = new Bond(b.Issue,
                                                    b.Maturity,
                                                    Currency.USD,
                                                    BondType.USGovt,
                                                    b.Coupon,
                                                    DayCount.ActualActualBond,
                                                    CycleRule.None,
                                                    Frequency.SemiAnnual,
                                                    BDConvention.Following,
                                                    Calendar.NYB),
                                    MarketQuote = b.YieldQuote
                                  });
      foreach (var bond in bonds)
      {
        var bondPricer = new BondPricer(bond.Bond, asOf_, Dt.AddDays(dc.AsOf, dc.SpotDays, dc.SpotCalendar), dc, null, 0, TimeUnit.None, 0.0)
                           {
                             MarketQuote = bond.MarketQuote,
                             QuotingConvention = QuotingConvention.Yield,
                             Notional = double.Parse("1,000,000", us_)
                           };

        double p = bondPricer.FullPrice() * bondPricer.Notional;
        double pv = bondPricer.Pv() / dc.DiscountFactor(asOf_, bondPricer.Settle); // since full price is for Settle date

        Assert.AreEqual(p, pv, 1e-8);
      }
    }

    private DiscountCurve Calibrate(IEnumerable<CalibrationBond> x)
    {
      var rateCurveTerms = new CurveTerms("TSY",
                                              Currency.USD,
                                              new InterestRateIndex("USDTSY",
                                                                    Tenor.Parse("6M"),
                                                                    Currency.USD,
                                                                    DayCount.ActualActualBond,
                                                                    Calendar.NYB,
                                                                    BDConvention.Following,
                                                                    2),
                                              new[]
                                                {
                                                  new AssetRateCurveTerm(
                                                    InstrumentType.Bond, 2,
                                                    BDConvention.Following,
                                                    DayCount.ActualActualBond,
                                                    Calendar.NYB,
                                                    Frequency.SemiAnnual, ProjectionType.None, null)
                                                });
      CurveFitSettings fitSettings = CreateFitSettings(asOf_, rateCurveTerms);
      var calibratorSettings = new CalibratorSettings(fitSettings);
      var paymentSettings = x.Select(xi => RateCurveTermsUtil.GetPaymentSettings(rateCurveTerms, InstrumentType.Bond.ToString())).ToArray();
      for (int i = 0; i < paymentSettings.Length; i++)
      {
        paymentSettings[i].BondType = BondType.USGovt;
        paymentSettings[i].Coupon = calibrationBonds_.ElementAt(i).Coupon;
        paymentSettings[i].QuoteConvention = QuotingConvention.Yield;
      }

      DiscountCurve curve = DiscountCurveFitCalibrator.DiscountCurveFit(asOf_,
                                                                        rateCurveTerms,
                                                                        "USDTSY",
                                                                        "",
                                                                        x.Select(p => p.YieldQuote).ToArray(),
                                                                        x.Select(p => "Bond").ToArray(),
                                                                        x.Select(p => p.Tenor).ToArray(),
                                                                        calibratorSettings,
                                                                        null,
                                                                        x.Select(p => p.Maturity).ToArray(),
                                                                        null,
                                                                        paymentSettings);
      return curve;
    }

    private CurveFitSettings CreateFitSettings(Dt tradeDt,
                                               CurveTerms discountRateCurveTerms)
    {
      int spotDays = discountRateCurveTerms.ReferenceIndex.SettlementDays;
      Calendar spotCal = discountRateCurveTerms.ReferenceIndex.Calendar;

      var fitSettings = new CurveFitSettings
                          {
                            CurveAsOf = asOf_,
                            Method = CurveFitMethod.Bootstrap,
                            InterpScheme = InterpScheme.FromString("Weighted", ExtrapMethod.Const, ExtrapMethod.Const),
                            CurveSpotDays = spotDays,
                            CurveSpotCalendar = spotCal
                          };
      return fitSettings;
    }
  }
}