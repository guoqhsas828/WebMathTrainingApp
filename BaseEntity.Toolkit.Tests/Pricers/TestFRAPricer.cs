//
// Copyright (c)    2018. All rights reserved.
//

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{


  /// <summary>
  /// Test the FRA pricer
  /// </summary>
  [TestFixture]
  public class TestFRAPricer : ToolkitTestBase
  {
    #region Tests

    [SetUp]
    public void SetUpDiscount()
    {
      AsOf = new Dt(28, 1, 2010);
    }

    /// <summary>
    /// Utility method to generate a discount curve with FRA tenors for testing purpose
    /// </summary>
    /// <returns>Calibrated discount curve</returns>
    public static DiscountCurve GetDiscountCurve(Dt asOf, string[] fraNames, double[] fraQuotes, InstrumentType[] overlapOrders)
    {
      var quotes_ = new double[]
                  {
                    0.00231,
                    0.00239,
                    0.00249,
                    0.00390,
                    0.0063,
                    0.00875,
                    0.99715,
                    0.99610,
                    0.99330,
                    0.98960,
                    0.9858,
                    0.0126,
                    0.01731,
                    0.0245,
                    0.02659,
                    0.02977,
                    0.03229,
                    0.03421,
                    0.03573,
                    0.03707,
                    0.03921,
                    0.04142,
                    0.04295,
                    0.04361,
                    0.04398
                  };

      var types_ = new[]
                 {
                   "MM", "MM", "MM", "MM", "MM", "MM",
                   "FUT", "FUT", "FUT", "FUT", "FUT",
                   "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap",
                   "Swap", "Swap"
                 };

      var names_ = new string[]
                 {
                   "1M",
                   "2M",
                   "3M",
                   "6M",
                   "9M",
                   "1Y",
                   "H0",
                   "M0",
                   "U0",
                   "Z0",
                   "H1",
                   "2Yr",
                   "3Yr",
                   "4Yr",
                   "5Yr",
                   "6Yr",
                   "7Yr",
                   "8Yr",
                   "9Yr",
                   "10Yr",
                   "12Yr",
                   "15Yr",
                   "20Yr",
                   "25Yr",
                   "30Yr"
                 };

      var dates_ = new Dt[quotes_.Length];
      for (int idx = 0; idx < dates_.Length; idx++)
      {
        var type = RateCurveTermsUtil.ConvertInstrumentType(types_[idx], true, InstrumentType.None);
        if (type == InstrumentType.FUT)
          dates_[idx] = Dt.ImmDate(asOf, names_[idx]);
        else
          dates_[idx] = Dt.Add(asOf, Tenor.Parse(names_[idx]));
      }

      var bnames_ = new string[]
                 {
                  "2Yr",
                   "3Yr",
                   "4Yr",
                   "5Yr",
                   "6Yr",
                   "7Yr",
                   "8Yr",
                   "9Yr",
                   "10Yr",
                   "12Yr",
                   "15Yr",
                   "20Yr",
                   "25Yr",
                   "30Yr"
                 };

      var bquotes_ = new double[] { 4, 6, 8, 10, 12, 14, 16, 18, 20, 24, 30, 40, 50, 60 };

      var bdates_ = new Dt[bquotes_.Length];
      for (int idx = 0; idx < bdates_.Length; idx++)
      {
        bdates_[idx] = Dt.Add(asOf, Tenor.Parse(bnames_[idx]));
      }

      var fraNames_ = CloneUtil.Clone(fraNames);
      var fraQuotes_ = CloneUtil.Clone(fraQuotes);
      var fraDates_ = new Dt[fraQuotes_.Length];

      fraDates_ = new Dt[fraQuotes_.Length];
      for (int idx = 0; idx < fraDates_.Length; idx++)
      {
        Tenor settleTenor;
        Tenor contractTenor;
        if (Tenor.TryParseComposite(fraNames_[idx], out settleTenor, out contractTenor))
          fraDates_[idx] = Dt.Add(asOf, settleTenor);
      }

      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var setting = new CalibratorSettings();
      setting.Method = CurveFitMethod.IterativeBootstrap;
      setting.MarketWeight = 1.0;
      setting.SlopeWeight = 0.0;
      setting.InterpScheme = new InterpScheme { Method = InterpMethod.Weighted };
      var ibs0 = new DiscountCurveFitCalibrator(asOf, ri, setting);
      DiscountCurve disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
          continue;

        }
        if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
        {
          disc.AddSwap(names_[i], dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly, BDConvention.Following,
                       Calendar.NYB);
        }
      }

      for (int i = 0; i < fraQuotes_.Length; i++)
        disc.AddFRA(fraNames_[i], 1.0, asOf, fraDates_[i], fraQuotes_[i], ri);

      disc.ResolveOverlap(new OverlapTreatment(overlapOrders));
      disc.Fit();
      return disc;
    }

    /// <summary>
    /// Utility method to generate a discount curve with FRA tenors for testing purpose
    /// </summary>
    /// <returns>Calibrated discount curve</returns>
    public static DiscountCurve GetSimpleDiscountCurve(Dt asOf, Dt settle, string[] fraNames, double[] fraQuotes, InstrumentType[] overlapOrders)
    {
      var quotes_ = new double[]
                  {
                    0.00231,
                  };

      var types_ = new[]
                 {
                   "MM"
                 };

      var names_ = new string[]
                 {
                   "1M"
                 };

      var dates_ = new Dt[quotes_.Length];
      for (int idx = 0; idx < dates_.Length; idx++)
      {
        var type = RateCurveTermsUtil.ConvertInstrumentType(types_[idx], true, InstrumentType.None);
          dates_[idx] = Dt.Add(settle, Tenor.Parse(names_[idx]));
      }

      var fraNames_ = CloneUtil.Clone(fraNames);
      var fraQuotes_ = CloneUtil.Clone(fraQuotes);
      var fraDates_ = new Dt[fraQuotes_.Length];

      fraDates_ = new Dt[fraQuotes_.Length];
      for (int idx = 0; idx < fraDates_.Length; idx++)
      {
        Tenor settleTenor;
        Tenor contractTenor;
        if (Tenor.TryParseComposite(fraNames_[idx], out settleTenor, out contractTenor))
          fraDates_[idx] = Dt.Add(settle, settleTenor);
      }

      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var setting = new CalibratorSettings();
      setting.Method = CurveFitMethod.IterativeBootstrap;
      setting.MarketWeight = 1.0;
      setting.SlopeWeight = 0.0;
      setting.InterpScheme = new InterpScheme { Method = InterpMethod.Weighted };
      var ibs0 = new DiscountCurveFitCalibrator(asOf, ri, setting);
      DiscountCurve disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
          continue;

        }
      }

      for (int i = 0; i < fraQuotes_.Length; i++)
        disc.AddFRA(fraNames_[i], 1.0, Dt.AddDays(asOf, ri.SettlementDays, ri.Calendar), fraDates_[i], fraQuotes_[i], ri);

      disc.ResolveOverlap(new OverlapTreatment(overlapOrders));
      disc.Fit();
      return disc;
    }

    protected Dt AsOf { get; set; }

    /// <summary>
    /// Test implied FRA rate against the FRA rates used to bootstrap a curve
    /// </summary>
    [Test]
    public void RoundTripCalculation()
    {
      var fraNames = new string[] { "4 X 7",  "9 X 12" };
      var fraQuotes = new double[] { 0.012,  0.0135 };
      var overlapOrders = new InstrumentType[] { InstrumentType.FRA, InstrumentType.Swap, InstrumentType.FUT, InstrumentType.MM };
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");

      Dt spotDate = Dt.AddDays(AsOf, ri.SettlementDays, ri.Calendar);

      var discountCurve = GetSimpleDiscountCurve(AsOf, spotDate, fraNames, fraQuotes, overlapOrders);

      var notional = 1e6; // 1MM
      var i = 0;
      for (int idx = 0; idx < fraNames.Length; idx++)
      {
        var tenor = fraNames[idx];
        Tenor settleTenor;
        Tenor contractTenor;
        if (Tenor.TryParseComposite(tenor, out settleTenor, out contractTenor))
        {
          var fra = new FRA(spotDate, settleTenor, ri.IndexTenor.ToFrequency(), fraQuotes[idx], ri, contractTenor, ri.Currency,
                            ri.DayCount, ri.Calendar, ri.Roll) {FixingLag = new Tenor(ri.SettlementDays, TimeUnit.Days)};

          var fraPricer = new FRAPricer(fra, AsOf, spotDate, discountCurve,
                                        discountCurve, notional);

          var tolerance = 0.00001; // 0.1 bp
          Assert.AreEqual(fraQuotes[idx], fraPricer.ImpliedFraRate, tolerance,
            "Implied rate for FRA tenor " + tenor.ToString());

        }
      }

      return;
    }

    #endregion Tests


  }


}
