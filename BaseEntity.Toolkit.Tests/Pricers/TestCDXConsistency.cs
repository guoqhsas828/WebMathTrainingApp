//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Tests.Helpers.Quotes;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("TestCDXConsistency Negative Notional")]
  [TestFixture("TestCDXConsistency Positive Notional")]
  public class TestCDXConsistency : ToolkitTestBase
  {
    public TestCDXConsistency(string name) : base(name)
    {}
    #region Set Up

    private void CreateCurves()
    {
      if (curves_ == null)
      {
        discountCurve_ = new DiscountCurve(new Dt(19700101), 0.05);
        basketQuotes_ = QuoteUtil.LoadBasketQuotes(BasketQuotesFile);
        curves_ = QuoteUtil.CreateSurvivalCurves(
          basketQuotes_.CdsQuotes, discountCurve_, true, false);
        //isLCDX_ = basketQuotes_.Index.IndexName.Contains("LCDX");
      }
      return;
    }
    private void CreateIndexCalibrator()
    {
      if (index_ == null)
      {
        CreateCurves();
        index_ = QuoteUtil.CreateScalingCalibrator(
        basketQuotes_.Index, basketQuotes_.IndexQuotes, curves_,
        discountCurve_, CDXScalingMethod.Model, true, true, null);
        index_.ActionOnInadequateTenors = ActionOnInadequateTenors.AddCurveTenors;
      }
      return;
    }

    #endregion // Set up

    [Test, Smoke]
    public void ConsistencyChecks()
    {
      CreateIndexCalibrator();
      for (int i = 0; i < index_.UseTenors.Length; ++i)
        if (index_.UseTenors[i])
        {
          // Prepare for consistency checks
          MarketQuote q = index_.Quotes[i];
          ICDXPricer pricer = CreateCdxPricer(index_.Indexes[i],
            index_.AsOf, index_.Settle, discountCurve_,
            index_.GetScaleSurvivalCurves());
          if (this.Notional != 0)
            pricer.Notional = this.Notional;
          pricer.MarketRecoveryRate = index_.MarketRecoveryRate;
          pricer.QuotingConvention = q.Type;
          pricer.MarketQuote = q.Value;

          // Scaling check: IntrinsicValue = MarketValue
          double accuracy = 1E-7 * Math.Abs(pricer.Notional);
          double actual = pricer.IntrinsicValue(false);
          double expect = pricer.MarketValue();
          Assert.AreEqual(expect, actual, accuracy,
            index_.TenorNames[i] + ".Intrinsic.Market");

          // Consistency check: (MarketPrice - 1) * CurrentNotional + Accrued = MarketValue
          actual = (pricer.MarketPrice() - 1) * pricer.EffectiveNotional + pricer.Accrued();
          Assert.AreEqual(expect, actual, accuracy,
            index_.TenorNames[i] + ".Price.Value");

          // Consistency check: price spread conversion
          if (q.Type == QuotingConvention.CreditSpread)
          {
            // Test function PriceToSpread()
            double flatprice = pricer.MarketPrice();
            double spread = pricer.PriceToSpread(flatprice);
            Assert.AreEqual(q.Value*10000, spread*10000, 1E-2,
              index_.TenorNames[i] + ".spread");

            // Test function SpreadToPrice()
            double price = pricer.SpreadToPrice(spread);
            Assert.AreEqual(flatprice*100, price*100, 1E-3,
              index_.TenorNames[i] + ".pricer.spread");

            // Test internal convertion
            pricer.QuotingConvention = QuotingConvention.FlatPrice;
            pricer.MarketQuote = flatprice;
            spread = pricer.MarketPremium;
            Assert.AreEqual(q.Value*10000, spread*10000, 1E-2,
              index_.TenorNames[i] + ".market.premium");
          }
          else
          {
            // Round trip check MarketPrice()
            double flatprice = pricer.MarketPrice();
            Assert.AreEqual(q.Value*100, flatprice*100, 1E-3,
              index_.TenorNames[i] + ".price");

            // Round trip check PriceToSpread() and SpreadToPrice()
            double spread = pricer.PriceToSpread(flatprice);
            Assert.AreEqual(q.Value*100, pricer.SpreadToPrice(spread)*100,
              1E-3, index_.TenorNames[i] + ".price.spread");
          }

        } // for..if

      return;
    }

    #region ICDXPricer interfaces

    private static ICDXPricer CreateCdxPricer(
      CDX cdx, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
    {
      return (cdx is LCDX) ?
        (ICDXPricer)(new MyLCDXPricer((LCDX)cdx, asOf, settle, discountCurve, survivalCurves))
        :
        (ICDXPricer)(new MyCDXPricer(cdx, asOf, settle, discountCurve, survivalCurves));
    }

    interface ICDXPricer : IPricer
    {
      CDX CDX { get;}
      SurvivalCurve[] SurvivalCurves { get;set;}
      QuotingConvention QuotingConvention { get;set;}
      double Notional { get;set;}
      double MarketQuote { get;set;}
      double MarketPremium { get;}
      double MarketRecoveryRate { get;set;}
      double EffectiveNotional { get;}
      double IntrinsicValue(bool currentMarket);
      double MarketValue();
      double MarketPrice();
      double PriceToSpread(double cleanPrice);
      double SpreadToPrice(double marketSpread);
    }

    class MyCDXPricer : CDXPricer, ICDXPricer
    {
      internal MyCDXPricer(CDX cdx, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
        : base(cdx, asOf, settle, discountCurve, survivalCurves)
      { }
    }
    class MyLCDXPricer : LCDXPricer, ICDXPricer
    {
      internal MyLCDXPricer(LCDX lcdx, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
        : base(lcdx, asOf, settle, discountCurve, survivalCurves)
      { }
      public CDX CDX => this.LCDX;

      public QuotingConvention QuotingConvention
      {
        get { return QuotingConvention.FlatPrice; }
        set
        {
          if (value != QuotingConvention.FlatPrice)
            throw new Exception("LCDX quote convention is not FLatPrice");
        }
      }
      public double MarketPremium => PriceToSpread(MarketQuote);
    }

    #endregion ICDXPricer Interfaces


    public string BasketQuotesFile { get; set; } = "data/indices/CDX.NA.HY.9-V3_20080915.data";

    // Constructed data
    private DiscountCurve discountCurve_;
    private BasketQuotes basketQuotes_;
    private SurvivalCurve[] curves_;
    private IndexScalingCalibrator index_;
  }
}
