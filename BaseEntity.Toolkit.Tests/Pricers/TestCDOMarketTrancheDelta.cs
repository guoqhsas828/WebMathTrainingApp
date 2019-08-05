//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;    //contains cash flow
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test cdo market tranche delta: 
  /// The market tranche delta is simply the ratio between cdo spread01
  /// and index spread01.The test idea is to use MarketTrancheDelta to
  ///  compute the delta and compare the result with manually calculated
  ///  results.
  /// NOTE: the number of quadrature points is set to be 128
  /// </summary>
  [TestFixture, Smoke]
  public class TestCDOMarketTrancheDelta : ToolkitTestBase
  {
    #region SetUP
    /// <summary>
    ///   Create an array of CDO Pricers
    /// </summary>
    /// <returns>CDO Pricers</returns>
    [OneTimeSetUp]
    public void Initialize()
    {
      string filename = GetTestFilePath(basketDataFile_);
      /*BasketData */
      bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      bd.RescaleStrikes = this.rescaleStrikes_;

      if (bd.CreditData == null)
      {
        if (creditDataFile_ == null)
          throw new System.Exception("No credit data");
        filename = GetTestFilePath(creditDataFile_);
        bd.CreditData = (CreditData)XmlLoadData(filename, typeof(CreditData));
      }

      if (bd.DiscountData == null)
      {
        if (irDataFile_ == null)
          throw new System.Exception("No interest rates data");
        filename = GetTestFilePath(irDataFile_);
        bd.DiscountData = (DiscountData)XmlLoadData(filename, typeof(DiscountData));
      }

      if (basketType_ != null && basketType_.Length > 0)
        bd.Type = (BasketData.BasketType)Enum.Parse(typeof(BasketData.BasketType), basketType_);

      if (copulaData_ != null && copulaData_.Length > 0)
      {
        string[] elems = copulaData_.Replace(" ", "").Split(new char[] { ',' });
        if (elems.Length < 3)
          throw new ArgumentException("Invalid copula data");
        bd.CopulaType = (CopulaType)Enum.Parse(typeof(CopulaType), elems[0]);
        bd.DfCommon = Int32.Parse(elems[1]);
        bd.DfIdiosyncratic = Int32.Parse(elems[2]);
      }

      ExtractAllParameters();

      // Create an array of CDO pricers with scaled survival curves
      CreateCDOPricers();

      return;
    }
    #endregion // SetUp

    #region helper methods

    private void CreateCDOPricers()
    {
      cdoPricers_ = bd.GetSyntheticCDOPricers(corrObj_, discountCurve_, scaledSurvCurves_);

      if (cdoPricers_ == null)
        throw new System.NullReferenceException("CDO Pricers not available");
      cdoNames_ = new string[cdoPricers_.Length];

      for (int i = 0; i < cdoNames_.Length; ++i)
      {
        //cdoPricers_[i].SurvivalCurves = scaledSurvCurves_;
        cdoNames_[i] = cdoPricers_[i].CDO.Description;
        cdoPricers_[i].Notional = 1000000;
        
        double breakEvenFeeOrPremium = (i == 0 ? cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium());
      }
    }

    private void ExtractAllParameters()
    {
      asOf_ = Dt.FromStr(bd.DiscountData.AsOf, "%D");
      settle_ = Dt.FromStr(bd.Settle, "%D");
      effective_ = Dt.FromStr(bd.IndexData.Effective, "%D");      
      ccy_ = bd.IndexData.Currency;
      dayCount_ = bd.IndexData.DayCount;
      freq_ = bd.IndexData.Frequency;
      calendar_ = bd.IndexData.Calendar;
      roll_ = bd.IndexData.Roll;

      if (bd.IndexData.TenorNames == null || bd.IndexData.TenorNames.Length == 0)
      {
        if (bd.IndexData.Maturities == null || bd.IndexData.TenorNames.Length == 0)
          throw new Exception("Index tenors and maturities can not both be null ");
        else
        {
          cdxMaturities_ = new Dt[bd.IndexData.Maturities.Length];
          for (int i = 0; i < bd.IndexData.Maturities.Length; ++i)
            cdxMaturities_[i] = Dt.FromStr(bd.IndexData.Maturities[i], "%D");
        }
      }
      else
      {
        if (bd.IndexData.Maturities != null &&
          (bd.IndexData.Maturities.Length != bd.IndexData.TenorNames.Length))
        {
          throw new Exception("Index tenors dimension must match index maturities dimension.");
        }
        cdxMaturities_ = new Dt[bd.IndexData.TenorNames.Length];
        for (int i = 0; i < bd.IndexData.TenorNames.Length; ++i)
        {
          Dt maturity = (bd.IndexData.Maturities == null || bd.IndexData.Maturities.Length == 0) ?
                        Dt.CDSMaturity(effective_, bd.IndexData.TenorNames[i]) : Dt.FromStr(bd.IndexData.Maturities[i], "%D");
          cdxMaturities_[i] = maturity;
        }
      }

      principals_ = bd.Principals;

      // Get the discount curve and survival curves
      discountCurve_ = bd.DiscountData.GetDiscountCurve();
      Dt[] testDt = new Dt[] 
      { 
        new Dt(1, 4, 2008), new Dt(10, 7, 2008),new Dt(18, 10, 2008),new Dt(26, 1, 2009),
        new Dt(6, 5, 2009),new Dt(14, 8, 2009),new Dt(22, 11, 2009),new Dt(2, 3, 2010),
        new Dt(10, 6, 2010),new Dt(18, 9, 2010),new Dt(27, 12, 2010),new Dt(6, 4, 2011),
      };

      survivalCurves_ = bd.CreditData.GetSurvivalCurves(discountCurve_);
      double[] prob = new double[testDt.Length];
      for (int i = 0; i < testDt.Length; ++i)
        prob[i] = 1 - survivalCurves_[0].DefaultProb(testDt[i]);

      dealPremia_ = bd.IndexData.DealPremia;
      marketQuotes_ = bd.IndexData.Quotes;

      if (bd.GetCorrelationObject() == null)
      {
        corrObj_ = (CorrelationObject)(BasketData.GetBaseCorrelation(bd.Correlation));
      }
      else
        corrObj_ = (CorrelationObject)(bd.GetCorrelationObject());
      bd.QuadraturePoints = 128; // According to Mark's spreadsheet: Market_Delta_Convexity.xls

      // Create an array of CDX notes
      CreateCDX();

      if (bd.CreditData.ScalingFactors != null && bd.CreditData.ScalingFactors.Length != 0)
      {
        scaledSurvCurves_ = new SurvivalCurve[survivalCurves_.Length];
        for (int i = 0; i < survivalCurves_.Length; ++i)
        {
          scaledSurvCurves_[i] = survivalCurves_[i];
        }
        CreateCDXPricers();
      }
      else
      {
        if (bd.CreditData.ScalingWeights == null || bd.CreditData.ScalingWeights.Length == 0)
          for (int i = 0; i < survivalCurves_.Length; ++i)
            scalingWeights_[i] = 1.0;
        else
          scalingWeights_ = Array.ConvertAll<double, double>(bd.CreditData.ScalingWeights,
            delegate(double p) { return p != 1.0 ? 1.0 : p; });
        scalingMethods_ = bd.IndexData.ScalingMethods;
        DoScaling();
      }

      return;
    }

    // Create an array of CDX notes before computing scaling factors
    private void CreateCDX()
    {
      // Get the number of CDX notes
      int nCDX = -1;
      if (bd.IndexData.TenorNames == null || bd.IndexData.TenorNames.Length == 0)
      {
        if (bd.IndexData.Maturities == null || bd.IndexData.TenorNames.Length == 0)
          throw new Exception("Index tenors and maturities can not both be null ");
        nCDX = bd.IndexData.Maturities.Length;
      }
      else
      {
        if (bd.IndexData.Maturities != null &&
          (bd.IndexData.Maturities.Length != bd.IndexData.TenorNames.Length))
        {
          throw new Exception("Index tenors dimension must match index maturities dimension.");
        }
        nCDX = bd.IndexData.TenorNames.Length;
      }

      cdx = new CDX[nCDX];
      for (int i = 0; i < nCDX; ++i)
      {
        // Below is checked for CDXpricers
        if (bd.IndexData.QuotesArePrices)
        {
          CDX note = new CDX(effective_, cdxMaturities_[i], bd.IndexData.Currency, dealPremia_[i] / 10000.0,
                             dayCount_, freq_, roll_, calendar_, null);
          if (!(Dt.FromStr(bd.IndexData.FirstPremium, "%D").IsEmpty()))
            note.FirstPrem = Dt.FromStr(bd.IndexData.FirstPremium, "%D");
          CDXPricer pricer = new CDXPricer(note, asOf_, settle_, discountCurve_, 0.01);
          marketQuotes_[i] = pricer.PriceToSpread(marketQuotes_[i] / 100) * 10000;
        }

        int basketSize = principals_.Length;
        double[] indexWeights = new double[basketSize];
        for (int j = 0; j < basketSize; ++j)
          indexWeights[j] = 1.0 / basketSize;

        cdx[i] = new CDX(effective_, cdxMaturities_[i], bd.IndexData.Currency,
                         dealPremia_[i]/10000.0, dayCount_, freq_, roll_, calendar_, indexWeights);

        if (bd.IndexData.FirstPremium != null)
        {
          cdx[i].FirstPrem = Dt.FromStr(bd.IndexData.FirstPremium, "%D");
        }                  

        cdx[i].Funded = false;
      }

      return;
    }

    // Create amn array of CDX pricers corresponding to CDXnotes
    private void CreateCDXPricers()
    {
      if (cdx == null || cdx.Length == 0)
      {
        CreateCDX();
      }
      int n = cdx.Length;
      cdxPricers_ = new CDXPricer[n];
      for (int i = 0; i < n; ++i)
      {
        cdxPricers_[i] = new CDXPricer(cdx[i], asOf_, settle_, discountCurve_, marketQuotes_[i] / 10000.0);
      }
      return;
    }

    // This method will calculate the index scaling factors and the scaled 
    // survival curves when no scaling factors are provided in credit data
    private void DoScaling()
    {
      if (bd.CreditData.ScalingFactors != null && bd.CreditData.ScalingFactors.Length > 0)
        // No need to scaled the survival curves. 
        // Done in CreditData.cs and names changed in ExtractAllParameters
        return;

      if (cdxMaturities_ != null && cdxMaturities_.Length != 0 && bd.IndexData.TenorNames.Length != cdxMaturities_.Length)
        throw new System.ArgumentException("Number of index tenors and maturities must be the same");

      if (indexWeights_ == null || indexWeights_.Length == 0)
      {
        indexWeights_ = new double[survivalCurves_.Length];
        double equalWeight = 1.0 / survivalCurves_.Length;
        for (int i = 0; i < survivalCurves_.Length; ++i)
          indexWeights_[i] = equalWeight;
      }

      double recoveryRate = 0.4;

      // Create indices for scaling
      if (cdx == null || cdx.Length == 0)
        CreateCDX();

      CreateCDXPricers();

      // Computing the scaling factors
      double[] quotes = new double[marketQuotes_.Length];
      for (int i = 0; i < marketQuotes_.Length; ++i)
        quotes[i] = marketQuotes_[i] / 10000.0;
      scalingFactors_ = CDXPricer.Scaling(
        asOf_, settle_, cdx, bd.IndexData.TenorNames, quotes,
        bd.IndexData.QuotesArePrices, scalingMethods_, !bd.IndexData.AbsoluteScaling,
        null, discountCurve_, survivalCurves_, scalingWeights_, recoveryRate);

      // Scale the survival curves
      scaledSurvCurves_ = new SurvivalCurve[survivalCurves_.Length];
      for (int i = 0; i < survivalCurves_.Length; ++i)
      {
        scaledSurvCurves_[i] = SurvivalCurve.Scale(survivalCurves_[i],
          bd.IndexData.TenorNames, scalingFactors_, !bd.IndexData.AbsoluteScaling);
        scaledSurvCurves_[i].Name = survivalCurves_[i].Name;
      }
      return;
    }

    private int[] GetAnArray(int x)
    {
      if (x <= 0)
        return null;
      else
      {
        int[] newArray = new int[x];
        for (int i = 0; i < x; i++)
          newArray[i] = i;
        return newArray;
      }
    }

    #endregion helper methods

    #region Test

    [Test, Smoke]
    public void TestMarketTrancheDeltaBP01()
    {
      Timer timer = new Timer();
      timer.Start();

      // calculate the index spread01 for the cdx pricers      
      double[] indexSpread01 = new double[cdxPricers_.Length];      
      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        double marketValue1 = cdxPricers_[0].MarketValue();
        double marketValue2 = cdxPricers_[0].MarketValue((marketQuotes_[0] + testBumps_[0]) / 10000);
        indexSpread01[i] = 1000000 * (marketValue2 - marketValue1) / testBumps_[0];        
      }

      // calculate the market tranche delta using function Sensitivities.MarketTrancheDelta
      List<double> marketTranchDeltaList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; ++i)
      {
        MarketQuote q = new MarketQuote(
          marketQuotes_[0] / 10000.0, QuotingConvention.CreditSpread);
        double[] marketTrancheDelta = Sensitivities.MarketTrancheDelta(
            cdoPricers_, cdx[0], discountCurve_, scaledSurvCurves_, testBumps_[0], q, true, false);
        marketTranchDeltaList.AddRange(marketTrancheDelta);
      }

      // calculate the market tranche delta manually and store in a list
      int[] intArray = GetAnArray(cdoPricers_.Length);
      List<double> marketTranchDeltaManualList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; i++)
      {
        double[] marketTranchDeltaManual = Array.ConvertAll<double, double>(
          Array.ConvertAll<int, double>(intArray, delegate(int k) { return cdoPricers_[k].Spread01("Pv", testBumps_[0], 0); }),
          delegate(double x) { return x / indexSpread01[i]; }
          );
        marketTranchDeltaManualList.AddRange(marketTranchDeltaManual);
      }

      // calculate the difference between the market tranche deltas between two methods
      double[] diff = new double[marketTranchDeltaList.Count];
      string[] desc = new string[marketTranchDeltaList.Count];
      
      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        for (int j = 0; j < cdoPricers_.Length; ++j)
        {
          int k = j+i*cdoPricers_.Length; 
          diff[k] = Math.Abs(marketTranchDeltaList[k] - marketTranchDeltaManualList[k]);          
          desc[k] = "CDOPricer_"+cdoPricers_[j].CDO.Description + "_CDX_" + (i+1).ToString();
        }
      }

      ResultData results = ToResultData(marketTranchDeltaList, desc, timer.Elapsed);

      timer.Stop();
      MatchExpects(results);
    }

    [Test, Smoke]
    public void TestMarketTrancheDeltaBP1()
    {
      Timer timer = new Timer();
      timer.Start();

      // calculate the index spread01 for the cdx pricers      
      double[] indexSpread01 = new double[cdxPricers_.Length];
      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        double marketValue1 = cdxPricers_[i].MarketValue();
        double marketValue2 = cdxPricers_[i].MarketValue((marketQuotes_[i] + testBumps_[1]) / 10000);
        indexSpread01[i] = 1000000 * (marketValue2 - marketValue1) / testBumps_[1];
      }

      // calculate the market tranche delta using function Sensitivities.MarketTrancheDelta
      List<double> marketTranchDeltaList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; ++i)
      {
        MarketQuote q = new MarketQuote(
          marketQuotes_[i] / 10000.0, QuotingConvention.CreditSpread);
        double[] marketTrancheDelta = Sensitivities.MarketTrancheDelta(cdoPricers_, cdx[i],
          discountCurve_, scaledSurvCurves_, testBumps_[1], q, true/*rescale strikes*/, false);
        marketTranchDeltaList.AddRange(marketTrancheDelta);
      }

      // calculate the market tranche delta manually and store in a list
      int[] intArray = GetAnArray(cdoPricers_.Length);
      List<double> marketTranchDeltaManualList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; i++)
      {
        double[] marketTranchDeltaManual = Array.ConvertAll<double, double>(
          Array.ConvertAll<int, double>(intArray, delegate(int k) { return cdoPricers_[k].Spread01("Pv", testBumps_[1], 0); }),
          delegate(double x) { return x / indexSpread01[i]; }
          );
        marketTranchDeltaManualList.AddRange(marketTranchDeltaManual);
      }

      // calculate the difference between the market tranche deltas between two methods
      double[] diff = new double[marketTranchDeltaList.Count];
      string[] desc = new string[marketTranchDeltaList.Count];

      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        for (int j = 0; j < cdoPricers_.Length; ++j)
        {
          int k = j + i * cdoPricers_.Length;
          diff[k] = Math.Abs(marketTranchDeltaList[k] - marketTranchDeltaManualList[k]);
          desc[k] = "CDOPricer_" + cdoPricers_[j].CDO.Description + "_CDX_" + (i + 1).ToString();
        }
      }

      ResultData results = ToResultData(marketTranchDeltaList, desc, timer.Elapsed);

      timer.Stop();
      MatchExpects(results);
    }

    [Test, Smoke]
    public void TestMarketTrancheDeltaBP10()
    {
      Timer timer = new Timer();
      timer.Start();

      // calculate the index spread01 for the cdx pricers      
      double[] indexSpread01 = new double[cdxPricers_.Length];
      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        double marketValue1 = cdxPricers_[i].MarketValue();
        double marketValue2 = cdxPricers_[i].MarketValue((marketQuotes_[i] + testBumps_[2]) / 10000);
        indexSpread01[i] = 1000000 * (marketValue2 - marketValue1) / testBumps_[2];
      }

      // calculate the market tranche delta using function Sensitivities.MarketTrancheDelta
      List<double> marketTranchDeltaList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; ++i)
      {
        MarketQuote q = new MarketQuote(
          marketQuotes_[0] / 10000.0, QuotingConvention.CreditSpread);
        double[] marketTrancheDelta = Sensitivities.MarketTrancheDelta(cdoPricers_, cdx[0],
          discountCurve_, scaledSurvCurves_, testBumps_[2], q, true/*rescale strikes*/, false);
        marketTranchDeltaList.AddRange(marketTrancheDelta);
      }

      // calculate the market tranche delta manually and store in a list
      int[] intArray = GetAnArray(cdoPricers_.Length);
      List<double> marketTranchDeltaManualList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; i++)
      {
        double[] marketTranchDeltaManual = Array.ConvertAll<double, double>(
          Array.ConvertAll<int, double>(intArray, delegate(int k) { return cdoPricers_[k].Spread01("Pv", testBumps_[2], 0); }),
          delegate(double x) { return x / indexSpread01[i]; }
          );
        marketTranchDeltaManualList.AddRange(marketTranchDeltaManual);
      }

      // calculate the difference between the market tranche deltas between two methods
      double[] diff = new double[marketTranchDeltaList.Count];
      string[] desc = new string[marketTranchDeltaList.Count];

      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        for (int j = 0; j < cdoPricers_.Length; ++j)
        {
          int k = j + i * cdoPricers_.Length;
          diff[k] = Math.Abs(marketTranchDeltaList[k] - marketTranchDeltaManualList[k]);
          desc[k] = "CDOPricer_" + cdoPricers_[j].CDO.Description + "_CDX_" + (i + 1).ToString();
        }
      }

      ResultData results = ToResultData(marketTranchDeltaList, desc, timer.Elapsed);

      timer.Stop();
      MatchExpects(results);
    }

    [Test, Smoke]
    public void TestMarketTrancheDeltaBP100()
    {
      Timer timer = new Timer();
      timer.Start();

      // calculate the index spread01 for the cdx pricers      
      double[] indexSpread01 = new double[cdxPricers_.Length];
      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        double marketValue1 = cdxPricers_[i].MarketValue();
        double marketValue2 = cdxPricers_[i].MarketValue((marketQuotes_[i] + testBumps_[3]) / 10000);
        indexSpread01[i] = 1000000 * (marketValue2 - marketValue1) / testBumps_[3];
      }

      // calculate the market tranche delta using function Sensitivities.MarketTrancheDelta
      List<double> marketTranchDeltaList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; ++i)
      {
        MarketQuote q = new MarketQuote(
          marketQuotes_[i] / 10000.0, QuotingConvention.CreditSpread);
        double[] marketTrancheDelta = Sensitivities.MarketTrancheDelta(cdoPricers_, cdx[i],
          discountCurve_, scaledSurvCurves_, testBumps_[3], q, true/*rescale strikes*/, false);
        marketTranchDeltaList.AddRange(marketTrancheDelta);
      }

      // calculate the market tranche delta manually and store in a list
      int[] intArray = GetAnArray(cdoPricers_.Length);
      List<double> marketTranchDeltaManualList = new List<double>();
      for (int i = 0; i < indexSpread01.Length; i++)
      {
        double[] marketTranchDeltaManual = Array.ConvertAll<double, double>(
          Array.ConvertAll<int, double>(intArray, delegate(int k) { return cdoPricers_[k].Spread01("Pv", testBumps_[3], 0); }),
          delegate(double x) { return x / indexSpread01[i]; }
          );
        marketTranchDeltaManualList.AddRange(marketTranchDeltaManual);
      }

      // calculate the difference between the market tranche deltas between two methods
      double[] diff = new double[marketTranchDeltaList.Count];
      string[] desc = new string[marketTranchDeltaList.Count];

      for (int i = 0; i < cdxPricers_.Length; ++i)
      {
        for (int j = 0; j < cdoPricers_.Length; ++j)
        {
          int k = j + i * cdoPricers_.Length;
          diff[k] = Math.Abs(marketTranchDeltaList[k] - marketTranchDeltaManualList[k]);
          desc[k] = "CDOPricer_" + cdoPricers_[j].CDO.Description + "_CDX_" + (i + 1).ToString();
        }
      }

      ResultData results = ToResultData(marketTranchDeltaList, desc, timer.Elapsed);

      timer.Stop();
      MatchExpects(results);
    }

    #endregion Test

    #region Data
    const double epsilon = 1.0E-9;

    //- Data files
    private string irDataFile_ = null;
    private string creditDataFile_ = null;
    private string basketDataFile_ = "data/TestMarketTrancheDelta_BasketData.xml";

    //- other params
    private string basketType_ = null;
    private string copulaData_ = null;
    private bool rescaleStrikes_ = true;

    Dt asOf_, settle_, effective_;
    Dt[] cdxMaturities_ = null;
    Currency ccy_;
    DayCount dayCount_;
    Frequency freq_;
    Calendar calendar_;
    BDConvention roll_;
        
    double[] dealPremia_;
    double[] marketQuotes_;
    double[] scalingFactors_;
    double[] scalingWeights_;
    double[] indexWeights_;
    CDXScalingMethod[] scalingMethods_;

    double[] testBumps_ = new double[] {0.1, 1.0, 10.0, 100.0};
    CorrelationObject corrObj_ = null;
    
    private SyntheticCDO[] cdos_ = null;
    private SyntheticCDOPricer[] cdoPricers_ = null;
    private CDX[] cdx = null;
    private CDXPricer[] cdxPricers_ = null;
    private string[] cdoNames_ = null;
    private DiscountCurve discountCurve_ = null;
    private SurvivalCurve[] survivalCurves_ = null;
    private SurvivalCurve[] scaledSurvCurves_ = null;
    private double[] principals_;

    private BasketData bd = null;
    #endregion // Data
  }
}
