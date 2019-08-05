//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers
{

  /// <summary>
  /// Test the swap leg pricer
  /// </summary>
  [TestFixture]
  public class TestAssetSwapPricerParameterized : ToolkitTestBase
  {
    [SetUp]
    public void SetUpDiscount()
    {
      AsOf = new Dt(19, 5, 1999);
      DiscountData = SwapLegTestUtils.GetDiscountData(AsOf);
    }

    // [NUnit.Framework.Test]
    public void InflationPARPrice()
    {
      ToolkitConfigurator.Init();
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer p = GetAssetSwapPricer(AssetSwapQuoteType.PAR, DiscountData.GetDiscountCurve(), AsOf, .06);

      NUnit.Framework.Assert.LessOrEqual(p.PriceFromAssetSwapSpread(), 96.9413);
      NUnit.Framework.Assert.GreaterOrEqual(p.PriceFromAssetSwapSpread(), 96.9412);

      AssetSwapPricer pp = GetAssetSwapPricer(AssetSwapQuoteType.PAR, DiscountData.GetDiscountCurve(), AsOf, p.AssetSwapSpread());

      NUnit.Framework.Assert.AreEqual(pp.PriceFromAssetSwapSpread(), 100.00);

      t.Stop();
      
    }

    public ResultData GenerateResults(SwapLegPricer pay, SwapLegPricer receive)
    {
      string[] labels = new string[8];
      double[] values = new double[8];

      SwapPricer sp = null;

      if (pay != null && receive != null)
      {
        sp = new SwapPricer(receive, pay);
      }

      Timer t = new Timer();
      t.Start();

      if (pay != null)
      {
        labels[0] = "MTM1";
        values[0] = pay.Pv();
        labels[1] = "Accrued1";
        values[1] = pay.Accrued();
        labels[2] = "IR011";
        ((SwapLeg)pay.Product).InitialExchange = true;
        ((SwapLeg)pay.Product).IntermediateExchange = true;
        ((SwapLeg)pay.Product).FinalExchange = true;
        values[2] = Sensitivities.IR01(pay, "Pv", 1.0, 0, true);
      }

      if (receive != null)
      {
        labels[3] = "MTM2";
        values[3] = receive.Pv();
        labels[4] = "Accrued2";
        values[4] = receive.Accrued();
        ((SwapLeg)receive.Product).InitialExchange = true;
        ((SwapLeg)receive.Product).IntermediateExchange = true;
        ((SwapLeg)receive.Product).FinalExchange = true;
        labels[5] = "IR012";
        values[5] = Sensitivities.IR01(receive, "Pv", 1.0, 0, true);
      }

      if (sp != null)
      {
        labels[6] = "ParCoupon";
        values[6] = sp.ParCoupon();
        labels[7] = "PV";
        values[7] = sp.Pv();
      }

      t.Stop();

      return ToResultData(values, labels, t.Elapsed);
    }

   
    [Test]
    public void AssetBuyerReceiverCFCheckPar()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer ap = GetAssetSwapPricer(AssetSwapQuoteType.PAR);
      t.Stop();
      DataTable dt = ap.GetAssetSwapBuyerReceiverSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void AssetBuyerPayerCFCheckPar()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer ap = GetAssetSwapPricer(AssetSwapQuoteType.PAR);
      t.Stop();
      DataTable dt = ap.GetAssetSwapBuyerPayerSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void AssetBuyerReceiverCFCheckMarket()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer ap = GetAssetSwapPricer(AssetSwapQuoteType.MARKET);
      t.Stop();
      DataTable dt = ap.GetAssetSwapBuyerReceiverSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void AssetBuyerPayerCFCheckMarket()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer ap = GetAssetSwapPricer(AssetSwapQuoteType.MARKET);
      t.Stop();
      DataTable dt = ap.GetAssetSwapBuyerPayerSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    private AssetSwapPricer GetAssetSwapPricer(AssetSwapQuoteType quoteType)
    {
      return GetAssetSwapPricer(quoteType, DiscountData.GetDiscountCurve(), AsOf, 0 );
    }

    private AssetSwapPricer GetAssetSwapPricer(AssetSwapQuoteType quoteType, double spread)
    {
      return GetAssetSwapPricer(quoteType, DiscountData.GetDiscountCurve(), AsOf, spread);
    }

    private static AssetSwapPricer GetAssetSwapPricer(AssetSwapQuoteType quoteType, DiscountCurve dc, Dt asOf, double spread)
    {
      SurvivalCurve sc = new SurvivalCurve(asOf, .01);
      Dt effective = new Dt(20, 5, 1999);
      double coupon = .05625;

      var rr = new RateResets();
      rr.AllResets.Add(new Dt(1, 1, 1999), 1.0);
      rr.AllResets.Add(new Dt(1, 2, 1999), 1.0);
      rr.AllResets.Add(new Dt(1, 3, 1999), 1.0);
      rr.AllResets.Add(new Dt(1, 4, 1999), 1.0);
      rr.AllResets.Add(new Dt(1, 5, 1999), 1.0);

      InflationBond b = null;
      InflationBondPricer assetPricer = null;
      Dt maturity = Dt.Add(effective, 3, TimeUnit.Years);
      RateModelParameters rmp = SwapLegTestUtils.GetBGMRateModelParameters(asOf, new Tenor("3M"));
      InflationIndex ii = new InflationIndex("USDCPI", Currency.USD, DayCount.Thirty360, Calendar.None,
                                             BDConvention.None, Frequency.Quarterly, Tenor.Empty) {HistoricalObservations = rr};
      b = new InflationBond(effective, maturity, Currency.USD, BondType.USCorp, coupon, DayCount.Thirty360,
                            CycleRule.None, Frequency.Annual, BDConvention.None, Calendar.None, ii, 1,
                            new Tenor(3, TimeUnit.Months));
      var inflationFactor = new InflationFactorCurve(asOf);
      inflationFactor.Add(asOf, 1.0);
      inflationFactor.Add(maturity, 1.2);
      InflationCurve ic = new InflationCurve(asOf, 1, inflationFactor, null);
      assetPricer = new InflationBondPricer(b, asOf, effective, 1, dc, ii, ic, rr, rmp);
      AssetSwap asw = new AssetSwap(assetPricer.Product, effective, quoteType, DayCount.Actual365Fixed,
                                    Frequency.SemiAnnual, Calendar.None, BDConvention.None,
                                    spread, assetPricer.Pv());
      return new AssetSwapPricer(assetPricer, asw, dc, rmp, rr, dc, SwapLegTestUtils.GetLiborIndex("3m"));
    }

    [Test]
    public void AssetPARPV()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer p = GetAssetSwapPricer(AssetSwapQuoteType.PAR);
      double pv = p.Pv();
      double asw = p.AssetSwapSpread();
      AssetSwapPricer parPricer = GetAssetSwapPricer(AssetSwapQuoteType.PAR, p.AssetSwapSpread());
      double pvAtPar = parPricer.Pv();
      t.Stop();
      ResultData rd = ToResultData(new List<double>() { pv, asw, pvAtPar }, new List<string>() { "pv", "asw", "pvAtPar" }, t.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void AssetMarketPV()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer p = GetAssetSwapPricer(AssetSwapQuoteType.MARKET);
      double pv = p.Pv();
      double asw = p.AssetSwapSpread();
      AssetSwapPricer parPricer = GetAssetSwapPricer(AssetSwapQuoteType.MARKET, p.AssetSwapSpread());
      double pvAtPar = parPricer.Pv();
      t.Stop();
      ResultData rd = ToResultData(new List<double>() { pv, asw, pvAtPar }, new List<string>() { "pv", "asw", "pvAtPar" }, t.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void SolveForPar()
    {
      Timer t = new Timer();
      t.Start();
      double spread = 20e-4;
      AssetSwapPricer p = GetAssetSwapPricer(AssetSwapQuoteType.PAR);
      p.AssetSwap.Spread = spread;
      p.AssetSwap.DealPrice = p.PriceFromAssetSwapSpread();
      double asw = p.AssetSwapSpread();
      Assert.AreEqual(spread, asw, 1e-7);
    }

    [Test]
    public void SolveMarketForPar()
    {
      Timer t = new Timer();
      t.Start();
      double spread = 20e-4;
      AssetSwapPricer p = GetAssetSwapPricer(AssetSwapQuoteType.MARKET);
      p.AssetSwap.Spread = spread;
      p.AssetSwap.DealPrice = p.PriceFromAssetSwapSpread();
      double asw = p.AssetSwapSpread();
      Assert.AreEqual(spread, asw, 1e-7);
    }

    [Test]
    public void BondPV()
    {
      Timer t = new Timer();
      t.Start();
      IPricer p = ((IPricer)GetAssetSwapPricer(AssetSwapQuoteType.MARKET).AssetPricer);
      double fullpv = ((InflationBondPricer)p).Pv() * 100;
      double accrued = p.Accrued()*100;
      double flatpv = fullpv - accrued;
      t.Stop();
      ResultData rd = ToResultData(new List<double>() { fullpv, accrued, flatpv }, new List<string>() { "fullpv", "accrued", "flatpv" }, t.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void BondCF()
    {
      Timer t = new Timer();
      t.Start();
      IPricer p = ((IPricer)GetAssetSwapPricer(AssetSwapQuoteType.MARKET).AssetPricer);
      PaymentSchedule ps = ((InflationBondPricer)p).GetPaymentSchedule(null, AsOf);
      t.Stop();
      ResultData rd = GetResultData(ps.ToDataTable());
      MatchExpects(rd);
    }

    [Test]
    public void InflationBondPV()
    {
      Timer t = new Timer();
      t.Start();
      IPricer p = ((IPricer)GetAssetSwapPricer(AssetSwapQuoteType.MARKET, DiscountData.GetDiscountCurve(), AsOf, 0).AssetPricer);
      double fullpv = ((InflationBondPricer)p).Pv() * 100;
      double accrued = p.Accrued() * 100;
      double flatpv = fullpv - accrued;
      t.Stop();
      ResultData rd = ToResultData(new List<double>() { fullpv, accrued, flatpv }, new List<string>() { "fullpv", "accrued", "flatpv" }, t.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void InflationBondCF()
    {
      Timer t = new Timer();
      t.Start();
      IPricer p = ((IPricer)GetAssetSwapPricer(AssetSwapQuoteType.MARKET, DiscountData.GetDiscountCurve(), AsOf, 0).AssetPricer);
      PaymentSchedule ps = ((InflationBondPricer)p).GetPaymentSchedule(null, AsOf);
      t.Stop();
      ResultData rd = GetResultData(ps.ToDataTable());
      MatchExpects(rd);
    }

    [Test]
    public void InflationSwapLegCF()
    {
      Timer t = new Timer();
      t.Start();
      SwapLegPricer p =
        (GetAssetSwapPricer(AssetSwapQuoteType.MARKET, DiscountData.GetDiscountCurve(), AsOf, 0)).FloatingLegPricer;
      PaymentSchedule ps = p.GetPaymentSchedule(null, AsOf);
      t.Stop();
      ResultData rd = GetResultData(ps.ToDataTable());
      MatchExpects(rd);
    }

    [Test]
    public void InflationPARPV()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer p = GetAssetSwapPricer(AssetSwapQuoteType.PAR, DiscountData.GetDiscountCurve(), AsOf, 0);
      double pv = p.Pv();
      double asw = p.AssetSwapSpread();
      AssetSwapPricer parPricer = GetAssetSwapPricer(AssetSwapQuoteType.PAR, DiscountData.GetDiscountCurve(), AsOf, p.AssetSwapSpread());
      double pvAtPar = parPricer.Pv();
      t.Stop();
      ResultData rd = ToResultData(new List<double>() { pv, asw, pvAtPar }, new List<string>() { "pv", "asw", "pvAtPar" }, t.Elapsed);
      MatchExpects(rd);
    }

    [Test]
    public void InflationMarketPV()
    {
      Timer t = new Timer();
      t.Start();
      AssetSwapPricer p = GetAssetSwapPricer(AssetSwapQuoteType.MARKET, DiscountData.GetDiscountCurve(), AsOf, 0);
      double pv = p.Pv();
      double asw = p.AssetSwapSpread();
      AssetSwapPricer parPricer = GetAssetSwapPricer(AssetSwapQuoteType.MARKET, DiscountData.GetDiscountCurve(), AsOf,
                                                     p.AssetSwapSpread());
      double pvAtPar = parPricer.Pv();
      t.Stop();
      ResultData rd = ToResultData(new List<double> { pv, asw, pvAtPar },
                          new List<string> {"pv", "asw", "pvAtPar"}, t.Elapsed);
      MatchExpects(rd);
    }


    private ResultData GetResultData(DataTable dt)
    {
      ResultData rd = LoadExpects();
      //1st col is the label, rest are actual data points
      int cols = 0;

      DataColumn labelCol = null;
      foreach (DataColumn column in dt.Columns)
      {
        if (labelCol == null && column.DataType.Name.Equals("String"))
        {
          labelCol = column;
          continue;
        }
        if (column.DataType == typeof(double))
          cols++;
      }

      if (rd.Results.Length == 1 && rd.Results[0].Expects == null || rd.Results.Length != cols)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }
      if (cols >= 1)
      {
        int i = 0;

        foreach (DataColumn column in dt.Columns)
        {
          if (column == labelCol)
            continue;

          if (column.DataType != typeof(double))
            continue;

          rd.Results[i].Name = column.ColumnName;
          rd.Results[i].Labels = new string[dt.Rows.Count];
          rd.Results[i].Actuals = new double[dt.Rows.Count];
          int j = 0;
          foreach (DataRow row in dt.Rows)
          {
            rd.Results[i].Labels[j] = row[labelCol.ColumnName] is DBNull ? "" : (string)row[labelCol.ColumnName];
            rd.Results[i].Actuals[j] = row[column.ColumnName] is DBNull ? 0 : (double)row[column.ColumnName];
            j++;
          }
          i++;
        }
      }
      else
      {
        rd = ToResultData(new List<double>() {0}, new List<string>() {"EMPTY"}, 0);
      }
      return rd;
    }

    protected Dt AsOf { get; set; }
    protected DiscountData DiscountData { get; set; }
  }

}
