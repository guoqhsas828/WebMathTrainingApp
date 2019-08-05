using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Commodities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{
  [TestFixture]
  public class TestForwardPriceCurveSensitivities
  {
    [TestCase(BumpFlags.None, BumpTarget.StockPrice)]
    [TestCase(BumpFlags.None, BumpTarget.CommodityPrice)]
    [TestCase(BumpFlags.BumpInPlace, BumpTarget.StockPrice)]
    [TestCase(BumpFlags.BumpInPlace, BumpTarget.CommodityPrice)]
    public void TestRoundTrip(BumpFlags bumpFlags, BumpTarget bumpTarget)
    {
      var commodityPricer = GetComForwardPricer(_asOf, 
        GetCommodityCurve(_asOf, "originalCurve"));
      var stockPricer = GetStockForwardPricer(_asOf, 
        GetStockCurve(_asOf, "originalCurve"));
      TestPvRoundTrip(commodityPricer, bumpFlags, bumpTarget);
      TestPvRoundTrip(stockPricer, bumpFlags, bumpTarget);
    }


    private static void TestPvRoundTrip(IPricer pricer, BumpFlags bumpFlags,
      BumpTarget bumpTarget)
    {
      var bumpStockPrice = (bumpTarget & BumpTarget.StockPrice) != 0;
      var asOf = pricer.AsOf;

      var pv1 = pricer.Pv();
      var bumpedPvs = new List<double>();
      int length = bumpStockPrice ? _stockCurveQuotes.Length : _quotes.Length;

      for (int i = 0; i < length; i++)
      {
        ForwardPriceCurve bumpedCurve = null;
        IPricer bumpedPricer = null;
        if (bumpStockPrice)
        {
          bumpedCurve = GetStockCurve(asOf, string.Format("BumpedCurve{0}", i), i, 4.0);
          bumpedPricer = GetStockForwardPricer(asOf, (StockCurve)bumpedCurve);
        }
        else
        {
          bumpedCurve = GetCommodityCurve(asOf, string.Format("BumpedCurve{0}", i), i, 4.0);
          bumpedPricer = GetComForwardPricer(asOf, (CommodityCurve)bumpedCurve);
        }
        var pv2 = bumpedPricer.Pv();
        var diff = pv2 - pv1;
        bumpedPvs.Add(diff);
      }

      string measure = bumpStockPrice ? "Pv" : "ProductPv";
      var table = Sensitivities2.Calculate(new IPricer[] {pricer}, measure, null,
        bumpTarget, 4.0, 0.0, BumpType.ByTenor, bumpFlags, null, false, false, null,
        false, false, null);

      for (int i = 0; i < table.Rows.Count; i++)
      {
        NUnit.Framework.Assert.AreEqual(bumpedPvs[i], (double)table.Rows[i]["Delta"], 1E-12);
      }

      var pv3 = pricer.Pv();
      Assert.AreEqual(pv1, pv3, 1E-14);
    }

    private static CommodityForwardPricer GetComForwardPricer(Dt asOf, 
      CommodityCurve commodityCurve)
    {
      var commodityForward = GetCommodityForward();

      return new CommodityForwardPricer(commodityForward, asOf, asOf,
        _discountCurve, commodityCurve);
    }


    private static CommodityForward GetCommodityForward()
    {
      return new CommodityForward(Dt.Empty, _deliveryDate, _deliveryPrice, 
        _bdConvention, _calendar);
    }


    private static StockForwardPricer GetStockForwardPricer(Dt asOf, StockCurve stockCurve)
    {
      
      var deliverDate = Dt.Add(_asOf, Tenor.Parse("3Y"));
      var stockForward = new StockForward(deliverDate, 2023.12, Currency.None);
      return new StockForwardPricer(stockForward, _asOf, _asOf, 1.0, _discountCurve, 
        stockCurve);
    }


    private static StockCurve GetStockCurve(Dt asOf, string name,
      int index = int.MaxValue, double bumpSize = 0.0)
    {
      Dt[] maturities = Array.ConvertAll(_stockTenorNames, 
        t => Dt.Add(asOf, Tenor.Parse(t))).ToArray();

      InstrumentType[] instrumentTypes = Array.ConvertAll(_stockCurveInstr,
        a => (InstrumentType)Enum.Parse(typeof(InstrumentType), a, true));

      var quotes = (double[])_stockCurveQuotes.Clone();

      for (int i = 0; i < quotes.Length; i++)
      {
        if (i == index) quotes[i] += bumpSize;
      }

      return StockCurve.FitStockForwardCurve(
        asOf, asOf, new CalibratorSettings(GetCurveFitSetting()), "StockCurve", 
        _stockSpotPrice, _bdConvention, _calendar, maturities, 
        _stockTenorNames, instrumentTypes, quotes, _discountCurve, null);
    }


    private static CommodityCurve GetCommodityCurve(Dt asOf, string name,
      int index = int.MaxValue, double bumpSize= 0.0)
    {
      var futureTerm = GetCommodityFutureTerm();

      var curveTerm = new CurveTerms("OilCommodityTerm", Currency.USD, 
        futureTerm.CommodityPriceIndex,
        new List<AssetCurveTerm>() {futureTerm});

      var quotes = (double[])_quotes.Clone();

      for (int i = 0; i < quotes.Length; i++)
      {
        if (i == index) quotes[i] += bumpSize;
      }

      return CommodityCurve.Create(name, asOf, curveTerm, _comFutures,
        _tenorName, quotes, _discountCurve, _spotPrice,
        new CalibratorSettings(GetCurveFitSetting()), null, null, null, null, null);
    }

    private static CurveFitSettings GetCurveFitSetting()
    {
      return new CurveFitSettings
      {
        CurveAsOf = _asOf,
        Method = CashflowCalibrator.CurveFittingMethod.Bootstrap,
        InterpScheme = InterpScheme.FromString("Weighted", ExtrapMethod.Const, ExtrapMethod.Const),
        CurveSpotDays = 0,
        ApproximateRateProjection = false
      };
    }

    private static CommodityFutureAssetCurveTerm GetCommodityFutureTerm()
    {
      var comIndex = new CommodityPriceIndex("WTIOilIndex", Currency.USD, _dayCount,
        _calendar, _bdConvention, 2, Frequency.Quarterly);

      return new CommodityFutureAssetCurveTerm(_bdConvention, _calendar,
        _commodityContractSize, _comTickSize, _comTickValue, comIndex);
    }

    #region DATA

    private static Dt _asOf =new Dt(20151205);
    private static Dt _deliveryDate =new Dt(20181205);
    private  static DayCount _dayCount = DayCount.Actual360;
    private static Calendar _calendar = Calendar.NYB;
    private static BDConvention _bdConvention = BDConvention.Modified;
    private static int _commodityContractSize = 1000;
    private static double _comTickSize = 0.01;
    private static double _comTickValue = 10;

    private static string[] _comFutures = {"FUT", "FUT", "FUT", "FUT", "FUT"};

    private static string[] _tenorName = {"Aug16", "Sep16", "Oct16", "Nov16", "Dec16"};
    private static readonly double[] _quotes = {87.39, 87.31, 87.23, 87.16, 87.02};
    private static double _deliveryPrice = 101.2;
    private static double _spotPrice = 89.95;
    private static DiscountCurve _discountCurve = new DiscountCurve(_asOf, 0.02)
    {
      Name = "DiscountCurve"
    };


    private static string[] _stockCurveInstr = {"FUT", "FUT", "FUT", "FUT", "FUT", "Forward", "Forward"};

    private static readonly double[] _stockCurveQuotes =
    {
      2091.23, 2092.54, 2093.25,
      2100.75, 2104.14, 2105.23,
      2121.14
    };

    private static double _stockSpotPrice = 2087.23;

    private static string[] _stockTenorNames = {"3M", "6M", "9M", "12M", "15M", "18M", "2Y"};

    #endregion DATA
  }
}
