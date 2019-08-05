// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{


  /// <summary>
  /// Test the resolve of overlapping tenors
  /// </summary>
  [TestFixture]
  public class TestOverlapTreatment : ToolkitTestBase
  {
    #region Tests

    [SetUp]
    public void SetUpDiscount()
    {
      AsOf = new Dt(6, 12, 2010);
    }

    /// <summary>
    /// Utility method to generate a discount curve with FRA tenors for testing purpose
    /// </summary>
    /// <returns>Calibrated discount curve</returns>
    public static DiscountCurve GetDiscountCurve(Dt asOf, string[] fraNames, double[] fraQuotes, InstrumentType[] overlapOrders)
    {
            ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");

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
                   "H1",
                   "M1",
                   "U1",
                   "Z1",
                   "H2",
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
          dates_[idx] = Dt.Add(asOf , Tenor.Parse(names_[idx]));
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
        bdates_[idx] =  Dt.Add(asOf, Tenor.Parse(bnames_[idx]));
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

      var setting = new CalibratorSettings();
      setting.Method = CurveFitMethod.SmoothForwards;
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
        else if (i > 5 && i < 11)
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


    protected Dt AsOf { get; set; }

    /// <summary>
    /// Test the case of empty overlap treatment order, which means all-securities included
    /// </summary>
    [Test]
    public void TestOverlapTreatmentAllSecurities()
    {
      var fraNames = new string[] {"4 X 7", "12 X 15"};
      var fraQuotes = new double[] {0.012, 0.0145};
      var overlapOrders = new InstrumentType[] {};

      var discountCurve = GetDiscountCurve(AsOf , fraNames, fraQuotes, overlapOrders);

      Assert.AreEqual( discountCurve.Tenors.Count, 27, 0, "Total tenors count is expected to be 27" );

    }

    /// <summary>
    /// Test the case of overlap treatment order FRA > FUT > Swap > MM, in the setup some of the FUT/MM tenors will be excluded
    /// </summary>
    [Test]
    public void TestOverlapTreatmentFraFutSwapMM()
    {
      var fraNames = new string[] { "4 X 7", "6 X 9", "9 X 12" };
      var fraQuotes = new double[] { 0.012, 0.0125, 0.0135 };
      var overlapOrders = new InstrumentType[] {InstrumentType.FRA, InstrumentType.FUT, InstrumentType.Swap, InstrumentType.MM };

      var discountCurve = GetDiscountCurve(AsOf, fraNames, fraQuotes, overlapOrders);
      // we shall have all FRAs, 2 futures (all other futures, except Z1/H2 overlapped with FRAs), all swaps, 3 MMs (1M, 2M, 3M)
      var fraCount = fraNames.Length;
      var futCount = 2;
      var swapCount = 14;
      var mmCount = 3;
      foreach (CurveTenor ten in discountCurve.Tenors)
      {
        if (ten.Product is FRA)
          fraCount--;
        else if (ten.Product is StirFuture)
          futCount--;
        else if (ten.Product is Swap || ten.Product is SwapLeg)
          swapCount--;
        else if (ten.Product is Note)
          mmCount--;
      }
      Assert.AreEqual(fraCount, 0, 0, "FRA tenors count is expected to be " + fraNames.Length);
      Assert.AreEqual(futCount, 0, 0, "Futures tenors count is expected to be " + 2);
      Assert.AreEqual(swapCount, 0, 0, "Swap tenors count is expected to be " + 14);
      Assert.AreEqual(mmCount, 0, 0, "MM tenors count is expected to be " + 3);

    }

    /// <summary>
    /// Test the case of overlap treatment order FUT > FRA > Swap > MM, in the setup some of the FRA/MM tenors will be excluded
    /// </summary>
    [Test]
    public void TestOverlapTreatmentFutFRASwapMM()
    {
      var fraNames = new string[] { "4 X 7", "6 X 9", "9 X 12" };
      var fraQuotes = new double[] { 0.012, 0.0125, 0.0135 };
      var overlapOrders = new InstrumentType[] { InstrumentType.FUT, InstrumentType.FRA, InstrumentType.Swap, InstrumentType.MM };

      var discountCurve = GetDiscountCurve(AsOf, fraNames, fraQuotes, overlapOrders);
      // we shall have 0 FRAs, 5 futures , all swaps, 3 MMs (1M, 2M, 3M)
      var fraCount = 0;
      var futCount = 5;
      var swapCount = 14;
      var mmCount = 3;
      foreach (CurveTenor ten in discountCurve.Tenors)
      {
        if (ten.Product is FRA)
          fraCount--;
        else if (ten.Product is StirFuture)
          futCount--;
        else if (ten.Product is Swap || ten.Product is SwapLeg)
          swapCount--;
        else if (ten.Product is Note)
          mmCount--;
      }
      Assert.AreEqual(fraCount, 0, 0, "FRA tenors count is expected to be " + 0);
      Assert.AreEqual(futCount, 0, 0, "Futures tenors count is expected to be " + 5);
      Assert.AreEqual(swapCount, 0, 0, "Swap tenors count is expected to be " + 14);
      Assert.AreEqual(mmCount, 0, 0, "MM tenors count is expected to be " + 3);

    }

    /// <summary>
    /// Test the case of overlap treatment order MM > FUT > FRA > Swap, in the setup some of the FUT/FRA tenors will be excluded
    /// </summary>
    [Test]
    public void TestOverlapTreatmentMMFutFRASwap()
    {
      var fraNames = new string[] { "4 X 7", "6 X 9", "9 X 12" };
      var fraQuotes = new double[] { 0.012, 0.0125, 0.0135 };
      var overlapOrders = new InstrumentType[] { InstrumentType.MM, InstrumentType.FUT, InstrumentType.FRA, InstrumentType.Swap };

      var discountCurve = GetDiscountCurve(AsOf, fraNames, fraQuotes, overlapOrders);
      // we shall have 0 FRAs, 2 futures , all swaps, all MMs 
      var fraCount = 0;
      var futCount = 2;
      var swapCount = 14;
      var mmCount = 6;
      foreach (CurveTenor ten in discountCurve.Tenors)
      {
        if (ten.Product is FRA)
          fraCount--;
        else if (ten.Product is StirFuture)
          futCount--;
        else if (ten.Product is Swap || ten.Product is SwapLeg)
          swapCount--;
        else if (ten.Product is Note)
          mmCount--;
      }
      Assert.AreEqual(fraCount, 0, 0, "FRA tenors count is expected to be " + 0);
      Assert.AreEqual(futCount, 0, 0, "Futures tenors count is expected to be " + 2);
      Assert.AreEqual(swapCount, 0, 0, "Swap tenors count is expected to be " + 14);
      Assert.AreEqual(mmCount, 0, 0, "MM tenors count is expected to be " + 6);

    }

    /// <summary>
    /// Test the case of overlap treatment order MM > FRA > FUT > Swap, in the setup some of the FUT/FRA tenors will be excluded
    /// </summary>
    [Test]
    public void TestOverlapTreatmentMMFRAFutSwap()
    {
      var fraNames = new string[] { "4 X 7", "6 X 9", "9 X 12" };
      var fraQuotes = new double[] { 0.012, 0.0125, 0.0135 };
      var overlapOrders = new InstrumentType[] { InstrumentType.MM, InstrumentType.FUT, InstrumentType.FRA, InstrumentType.Swap };

      var discountCurve = GetDiscountCurve(AsOf, fraNames, fraQuotes, overlapOrders);
      // we shall have 0 FRAs, 2 futures , all swaps, all MMs 
      var fraCount = 0;
      var futCount = 2;
      var swapCount = 14;
      var mmCount = 6;
      foreach (CurveTenor ten in discountCurve.Tenors)
      {
        if (ten.Product is FRA)
          fraCount--;
        else if (ten.Product is StirFuture)
          futCount--;
        else if (ten.Product is Swap || ten.Product is SwapLeg)
          swapCount--;
        else if (ten.Product is Note)
          mmCount--;
      }
      Assert.AreEqual(fraCount, 0, 0, "FRA tenors count is expected to be " + 0);
      Assert.AreEqual(futCount, 0, 0, "Futures tenors count is expected to be " + 2);
      Assert.AreEqual(swapCount, 0, 0, "Swap tenors count is expected to be " + 14);
      Assert.AreEqual(mmCount, 0, 0, "MM tenors count is expected to be " + 6);

    }

    #endregion Tests

    #region Duplicated keys and related tests
    object[,] _data = new object[,]
      {
        {"MM", "1D", 0.00091},
        {"Swap", "1W", 0.0009},
        {"Swap", "2W", 0.00092},
        {"Swap", "1M", 0.00099},
        {"Swap", "2M", 0.00102},
        {"Swap", "3M", 0.00104},
        {"Swap", "4M", 0.00105},
        {"Swap", "5M", 0.00105},
        {"Swap", "6M", 0.00108},
        {"Swap", "7M", 0.00108},
        {"Swap", "8M", 0.0011},
        {"Swap", "9M", 0.00114},
        {"Swap", "10M", 0.00118},
        {"Swap", "11M", 0.00122},
        {"Swap", "12M", 0.00126},
        {"Swap", "18M", 0.00153},
        {"SWAP", "2Y", 0.00192},
      };

    [Test]
    public void Regular()
    {
      var data = _data;
      var types = data.Column(0).Cast<string>().ToArray();
      var tenors = data.Column(1).Cast<string>().ToArray();
      var quotes = data.Column(2).Cast<double>().ToArray();
      var asOf = Dt.Today();

      var terms = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      var settings = new CalibratorSettings();
      var disc = DiscountCurveFitCalibrator.DiscountCurveFit(
        asOf, terms, "curve", quotes, types, tenors, settings);
      var expect = disc.Tenors.Count;
      disc.ResolveOverlap(new OverlapTreatment(new[] {
        InstrumentType.MM, InstrumentType.Swap, }));
      Assert.AreEqual(expect, disc.Tenors.Count);
    }

    [Test]
    public void DuplicatedKeys()
    {
      var data = _data;
      var types = data.Column(0).Cast<string>().ToArray();
      var tenors = data.Column(1).Cast<string>().ToArray();
      var quotes = data.Column(2).Cast<double>().ToArray();
      var asOf = Dt.Today();

      var terms = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      var settings = new CalibratorSettings();
      var disc = DiscountCurveFitCalibrator.DiscountCurveFit(
        asOf, terms, "curve", quotes, types, tenors, settings);
      var expect = disc.Tenors.Count;
      disc.ResolveOverlap(new OverlapTreatment(new[] { InstrumentType.MM,
        InstrumentType.Swap, InstrumentType.Swap, }));
      Assert.AreEqual(expect, disc.Tenors.Count);
    }

    [Test]
    public void NullPriorities()
    {
      var data = _data;
      var types = data.Column(0).Cast<string>().ToArray();
      var tenors = data.Column(1).Cast<string>().ToArray();
      var quotes = data.Column(2).Cast<double>().ToArray();
      var asOf = Dt.Today();

      var terms = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      var settings = new CalibratorSettings();
      var disc = DiscountCurveFitCalibrator.DiscountCurveFit(
        asOf, terms, "curve", quotes, types, tenors, settings);
      var expect = disc.Tenors.Count;
      disc.ResolveOverlap(new OverlapTreatment(null));
      Assert.AreEqual(expect, disc.Tenors.Count);
    }
    #endregion
  }


}
