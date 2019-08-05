//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Tests.Helpers.Quotes;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Calibrators.BaseCorrelations
{
  [TestFixture]
  public class TestIndexScalingCalibrator : ToolkitTestBase
  {
    #region Tests
    [Test]
    public void DurationOnSpreads()
    {
      TestScaling(CDXScalingMethod.Duration, true, false);
      TestScaling(CDXScalingMethod.Duration, false, false);
    }

    [Test]
    public void SpreadOnSpreads()
    {
      TestScaling(CDXScalingMethod.Spread, true, false);
      TestScaling(CDXScalingMethod.Spread, false, false);
    }

    [Test]
    public void ModelOnSpreads()
    {
      TestScaling(CDXScalingMethod.Model, true, false);
      TestScaling(CDXScalingMethod.Model, false, false);
    }

    [Test]
    public void DurationOnHazardRates()
    {
      TestScaling(CDXScalingMethod.Duration, true, true);
      TestScaling(CDXScalingMethod.Duration, false, true);
    }

    [Test]
    public void SpreadOnHazardRatesRelative()
    {
      TestScaling(CDXScalingMethod.Spread, true, true);
    }

    [Test]
    public void SpreadOnHazardRatesAbsolute()
    {
      Assert.Throws<ArgumentException>(() =>
        TestScaling(CDXScalingMethod.Spread, false, true));
    }

    [Test]
    public void ModelOnHazardRates()
    {
      TestScaling(CDXScalingMethod.Model, true, true);
      TestScaling(CDXScalingMethod.Model, false, true);
    }
    #endregion Tests

    #region Helpers

    private void TestScaling(CDXScalingMethod scalingMethod,
      bool relativeScaling, bool scaleOnHazardRate)
    {
      // Calibrate
      DiscountCurve discountCurve = new DiscountCurve(
        new Dt(19700101), 0.05);
      BasketQuotes basketQuotes = QuoteUtil.LoadBasketQuotes(
        BasketQuotesFile);
      SurvivalCurve[] curves = QuoteUtil.CreateSurvivalCurves(
        basketQuotes.CdsQuotes, discountCurve, true, false);
      IndexScalingCalibrator index = QuoteUtil.CreateScalingCalibrator(
        basketQuotes.Index, basketQuotes.IndexQuotes, curves,
        discountCurve, scalingMethod, relativeScaling,
        scaleOnHazardRate, null);
      index.ActionOnInadequateTenors = ActionOnInadequateTenors.AddCurveTenors;

      // Round trip check of the scaled curves
      RoundTripCheck(index);

      // Create calibrator wrap and test its scaled curves
      double[] quotes; bool quotesArePrices;
      GetQuotes(index.Quotes, out quotes, out quotesArePrices);
      IndexScalingCalibrator wrap = new IndexScalingCalibrator(
        index.AsOf, index.Settle, index.Indexes, index.TenorNames, quotes, quotesArePrices,
        new CDXScalingMethod[]{scalingMethod},relativeScaling,scaleOnHazardRate,
        discountCurve,curves,index.ScalingIncludes,index.MarketRecoveryRate);
      wrap.SetScalingFactors(index.GetScalingFactors());
      wrap.ActionOnInadequateTenors = index.ActionOnInadequateTenors;
      CheckEqual(index.GetScaleSurvivalCurves(), wrap.GetScaleSurvivalCurves());
    }
    private static void RoundTripCheck(IndexScalingCalibrator index)
    {
      SurvivalCurve[] curves = index.GetScaleSurvivalCurves();
      bool[] useTenors = index.UseTenors;
      for (int i = 0; i < useTenors.Length;++i)
        if (useTenors[i])
        {
          ICDXPricer pricer = CreateCdxPricer(index.Indexes[i], index.AsOf, index.Settle,
            index.DiscountCurve, curves);
          if (index.CdxScaleMethod[0] == CDXScalingMethod.Model)
            RoundTripModel(pricer, index.Quotes[i], index.MarketRecoveryRate);
          else if (index.CdxScaleMethod[0] == CDXScalingMethod.Duration)
            RoundTripDuration(pricer, index.Quotes[i], index.MarketRecoveryRate);
          else if (index.CdxScaleMethod[0] == CDXScalingMethod.Spread)
            RoundTripSpread(pricer, index.Quotes[i], index.MarketRecoveryRate);
          else
            throw new Exception("Unknown scaling method: " + index.CdxScaleMethod[i].ToString());
        }
      return;
    }
    private static void RoundTripModel(ICDXPricer cdxPricer,
      MarketQuote quote, double marketRecoveryRate)
    {
      // Calculate the target value
      double expect;
      ICDXPricer pricer = cdxPricer;
      pricer.MarketQuote = quote.Value;
      if (quote.Type == QuotingConvention.FlatPrice)
      {
        expect = quote.Value - 1.0 + pricer.Accrued();
      }
      else
      {
        // Spread quote: we calculate the market value
        pricer.MarketRecoveryRate = marketRecoveryRate;
        expect = pricer.MarketValue();
      }
      double actual = pricer.IntrinsicValue(true);
      Assert.AreEqual(expect*10000, actual*10000, 1E-2,
        cdxPricer.CDX.Description + ".RoundTrip");
      return;
    }
    private static void RoundTripDuration(ICDXPricer cdxPricer,
      MarketQuote quote, double marketRecoveryRate)
    {
      // Calculate the target value
      double expect;
      if (quote.Type == QuotingConvention.FlatPrice)
      {
        ICDXPricer pricer = cdxPricer;
        pricer.MarketQuote = quote.Value;
        pricer.MarketRecoveryRate = marketRecoveryRate;
        expect = pricer.PriceToSpread(quote.Value);
      }
      else
      {
        expect = quote.Value;
      }

      // Calculate average CDS scaling weights
      SurvivalCurve[] curves = cdxPricer.SurvivalCurves;
      double weightedSpread = 0.0;
      double durationSum = 0.0;
      for (int i = 0; i < curves.Length; i++)
      {
        // May change here to use ALL curve. For skipped curve, just use updated saved curve
        CDSCashflowPricer pricer = CurveUtil.ImpliedPricer(
          curves[i], cdxPricer.CDX.Maturity, cdxPricer.CDX.DayCount,
          cdxPricer.CDX.Freq, cdxPricer.CDX.BDConvention, cdxPricer.CDX.Calendar);

        double weight = cdxPricer.CDX.Weights == null ? 1.0 : cdxPricer.CDX.Weights[i];
        double duration = pricer.RiskyDuration();
        double spread = pricer.BreakEvenPremium();
        weightedSpread += duration * spread * weight;
        durationSum += duration * weight;
      }
      weightedSpread /= durationSum;
      Assert.AreEqual(expect*10000, weightedSpread*10000, 1E-2,
        cdxPricer.CDX.Description + ".RoundTrip");
      return;
    }
    private static void RoundTripSpread(ICDXPricer cdxPricer,
      MarketQuote quote, double marketRecoveryRate)
    {
      // Calculate the target value
      double expect;
      if (quote.Type == QuotingConvention.FlatPrice)
      {
        ICDXPricer pricer = cdxPricer;
        pricer.MarketQuote = quote.Value;
        pricer.MarketRecoveryRate = marketRecoveryRate;
        expect = pricer.PriceToSpread(quote.Value);
      }
      else
      {
        expect = quote.Value;
      }

      // Calculate average CDS scaling weights
      SurvivalCurve[] curves = cdxPricer.SurvivalCurves;
      double weightedSpread = 0.0;
      double weightSum = 0.0;
      for (int i = 0; i < curves.Length; i++)
      {
        // May change here to use ALL curve. For skipped curve, just use updated saved curve
        CDSCashflowPricer pricer = CurveUtil.ImpliedPricer(
          curves[i], cdxPricer.CDX.Maturity, cdxPricer.CDX.DayCount,
          cdxPricer.CDX.Freq, cdxPricer.CDX.BDConvention, cdxPricer.CDX.Calendar);

        double weight = cdxPricer.CDX.Weights == null ? 1.0 : cdxPricer.CDX.Weights[i];
        double spread = pricer.BreakEvenPremium();
        weightedSpread += spread * weight;
        weightSum += weight;
      }
      weightedSpread /= weightSum;
      Assert.AreEqual(expect*10000, weightedSpread*10000, 1E-2,
        cdxPricer.CDX.Description + ".RoundTrip");
      return;
    }
    private static void GetQuotes(MarketQuote[] marketQuotes,
      out double[] quotes, out bool quoteArePrice)
    {
      quoteArePrice = false;
      quotes = new double[marketQuotes.Length];
      for (int i = 0; i < marketQuotes.Length; ++i)
      {
        if (marketQuotes[i].Type == QuotingConvention.None)
          continue;
        else if (marketQuotes[i].Type == QuotingConvention.FlatPrice)
          quoteArePrice = true;
        quotes[i] = marketQuotes[i].Value;
      }
      return;
    }
    private static void CheckEqual(SurvivalCurve[] sc1, SurvivalCurve[] sc2)
    {
      for (int i = 0; i < sc1.Length; ++i)
        CheckEqual(sc1[i], sc2[i]);
      return;
    }
    // Check equality of two survival curves
    private static void CheckEqual(SurvivalCurve s1, SurvivalCurve s2)
    {
      // Check tenor count
      int count = s1.Tenors.Count;
      Assert.AreEqual(count, s2.Tenors.Count, s1.Name + ".Tenors.Count");
      if (count > s2.Tenors.Count)
        count = s2.Tenors.Count;

      // check forced fit status
      Assert.AreEqual(((SurvivalFitCalibrator)s1.SurvivalCalibrator).FitWasForced,
        ((SurvivalFitCalibrator)s2.SurvivalCalibrator).FitWasForced,
        s1.Name + ".FitWasForced");
      // Check tenor quotes
      for (int i = 0; i < count; ++i)
      {
        CDS cds1 = s1.Tenors[i].Product as CDS;
        if (cds1 == null) continue;
        CDS cds2 = s2.Tenors[i].Product as CDS;
        Assert.AreEqual(cds1.Premium, cds2.Premium, 1E-6, s1.Name + ".Quote[" + i + ']');
      }

      // Check curve points count
      count = s1.Count;
      Assert.AreEqual(count, s2.Count, s1.Name + ".Points.Count");
      if (count > s2.Count)
        count = s2.Count;

      // Check curve points
      for (int i = 0; i < count; ++i)
      {
        Dt dt1 = s1.GetDt(i);
        Dt dt2 = s2.GetDt(i);
        Assert.AreEqual(dt1, dt2, s1.Name + ".Dt[" + i + ']');
        double sp1 = s1.GetVal(i);
        double sp2 = s2.GetVal(i);
        Assert.AreEqual(sp1, sp2, 1E-6, s1.Name + ".Val[" + i + ']');
      }
      return;
    }
    #endregion Helpers

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
    [Serializable]
    class MyCDXPricer : CDXPricer, ICDXPricer
    {
      internal MyCDXPricer(CDX cdx, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
        : base(cdx, asOf, settle, discountCurve, survivalCurves)
      { }
    }
    [Serializable]
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

    #region Properties
    /// <summary>
    ///   Basket Quotes file
    /// </summary>
    public string BasketQuotesFile { get; set; } = "data/indices/CDX.NA.HY.9-V3_20080915.data";

    #endregion Properties

    #region Data

    #endregion Data
  }
}
