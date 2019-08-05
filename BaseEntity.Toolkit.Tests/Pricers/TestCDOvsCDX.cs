//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// NUnit tests of CDO pricers, for quick tests
  /// Just wrapped calls to the Main() of the stand-alone program BasketPricerTest
  /// </summary>
  [TestFixture]
  public class TestCDOvsCDX : ToolkitTestBase
  {
    const string theDatafile = "Index Tranche Pricing using Base Correlation Basket.xml";
    const double eps = 0.0005;//0.00001;
    const double notional = 1000000;
    const double dealPremium = 60;

    // Simple wrapper to the stand alone program BasketPricerTest
    private static IPricer[] GetPricers(string basketDataFile)
    { 
      IPricer[] pricers = new IPricer[2];

      // Extract basket data
      string filename = GetTestFilePath(basketDataFile);
      BasketData bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      BasketPricer basketPricer = bd.GetBasketPricer();

      // extract basket discount curve
      DiscountCurve discountCurve = bd.DiscountData.GetDiscountCurve();

      // Create a CDX
      CDX cdx = CreateCDX(bd.IndexData);
      cdx.Maturity = basketPricer.Maturity;
      cdx.Premium = dealPremium / 10000.0;
      cdx.Description = "Index";

      // Create a comparable [0-100] cdo
      SyntheticCDO cdo = new SyntheticCDO(
        cdx.Effective, cdx.Maturity, cdx.Ccy, cdx.Premium, cdx.DayCount, 
        cdx.Freq, cdx.BDConvention, cdx.Calendar);
      cdo.Attachment = 0.0;
      cdo.Detachment = 1.0;
      cdx.Description = "Index";
      
      // create [0-100] tranche pricer
      SyntheticCDOPricer cdoPricer = new SyntheticCDOPricer(cdo, basketPricer, discountCurve, notional);

      // create cdx pricer with 1m notional
      CDXPricer cdxPricer = new CDXPricer(cdx, basketPricer.AsOf, basketPricer.Settle,
        discountCurve, basketPricer.SurvivalCurves);
      cdxPricer.Notional = notional;
      //cdxPricer.MarketPremium = cdxPricer.CDX.Premium; // just use deal premium as market premium
      cdxPricer.QuotingConvention = QuotingConvention.CreditSpread;
      cdxPricer.MarketQuote = cdxPricer.CDX.Premium; // just use deal premium as market premium

      pricers[0] = cdxPricer;
      pricers[1] = cdoPricer;
      return pricers;
    }

    // Assert equal with relative eps
    private static void AssertEqual(double d0, double d1, double eps, string msg)
    {
      Assert.AreEqual(d0, d1, Math.Max(0.001, Math.Max(Math.Abs(d0), Math.Abs(d1))) * eps, msg);
    }

    /// <summary>
    ///   Create an array of pricers from a basket data file
    /// </summary>
    /// <param name="basketDataFile">basket data filename</param>
    /// <returns>Array of synthetic CDO pricers</returns>
    private static BasketData GetBasketData(string basketDataFile)
    {
      string filename = GetTestFilePath(basketDataFile);
      BasketData bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      return bd;
    }

    /// <summary>
    ///   Create CDX Products based on IndexData
    /// </summary>
    /// <param name="data">Index data</param>
    /// <returns>Array of CDXs</returns>
    private static CDX
    CreateCDX(BasketData.Index data)
    {
      if (data == null)
        throw new System.Exception("Cannot create CDXPricer because IndexData is null");
      Dt issueDate = Dt.FromStr(data.Effective, "%D");
      Dt maturityDate = Dt.FromStr(data.Maturities[0], "%D");
      CDX cdx = new CDX(issueDate, maturityDate, data.Currency, data.DealPremia[0] / 10000.0,
        data.DayCount, data.Frequency, data.Roll, data.Calendar);
      if (data.FirstPremium != null)
        cdx.FirstPrem = Dt.FromStr(data.FirstPremium, "%D");
      cdx.Funded = data.Funded;
      cdx.Description = (data.TenorNames == null ? data.Name : data.TenorNames[0]);
      return cdx;
    }

    [Test]
    public void Pv()
    {
      double resultCDX = 0;
      double resultCDO = 0;
      IPricer[] pricers = GetPricers(theDatafile);
      CDXPricer cdxPricer = (CDXPricer)pricers[0];
      SyntheticCDOPricer cdoPricer = (SyntheticCDOPricer)pricers[1];

      //unfunded 
      cdxPricer.CDX.CdxType = CdxType.Unfunded;
      cdoPricer.CDO.CdoType = CdoType.Unfunded;
      resultCDX = cdxPricer.Pv();
      resultCDO = cdoPricer.Pv();
      AssertEqual(resultCDX, resultCDO, eps, "Unfunded: Intrinisc CDX value does not match [0-100] CDO Pv");

      //funded 
      cdxPricer.CDX.CdxType = CdxType.FundedFixed;
      cdoPricer.CDO.CdoType = CdoType.FundedFixed;
      resultCDX = cdxPricer.Pv();
      resultCDO = cdoPricer.Pv();
      AssertEqual(resultCDX, resultCDO, eps, "Funded: Intrinisc CDX value does not match [0-100] CDO Pv");
    }

    [Test]
    public void Accrued()
    {
      double resultCDX = 0;
      double resultCDO = 0;
      IPricer[] pricers = GetPricers(theDatafile);
      CDXPricer cdxPricer = (CDXPricer)pricers[0];
      SyntheticCDOPricer cdoPricer = (SyntheticCDOPricer)pricers[1];

      //unfunded 
      cdxPricer.CDX.CdxType = CdxType.Unfunded;
      cdoPricer.CDO.CdoType = CdoType.Unfunded;
      resultCDX = cdxPricer.Accrued();
      resultCDO = cdoPricer.Accrued();
      AssertEqual(resultCDX, resultCDO, eps, "Unfunded: Accrued values do not match");

      //funded 
      cdxPricer.CDX.CdxType = CdxType.FundedFixed;
      cdoPricer.CDO.CdoType = CdoType.FundedFixed;
      resultCDX = cdxPricer.Accrued();
      resultCDO = cdoPricer.Accrued();
      AssertEqual(resultCDX, resultCDO, eps, "Funded: Accrued values do not match");

    }
    

#if NOT_USED // Obsolete. RTD Aug'07
    // This is not right since we do not have an intrinsic break even premium function
    //[Test]
    public void BreakEvenPremium()
    {
      double resultCDX = 0;
      double resultCDO = 0;
      IPricer[] pricers = GetPricers(theDatafile);
      CDXPricer cdxPricer = (CDXPricer)pricers[0];
      SyntheticCDOPricer cdoPricer = (SyntheticCDOPricer)pricers[1];

      //unfunded 
      cdxPricer.CDX.Funded = false;
      cdoPricer.CDO.CdoType = CdoType.Unfunded;
      resultCDX = cdxPricer.BreakEvenPremium();
      resultCDO = cdoPricer.BreakEvenPremium();
      AssertEqual(resultCDX, resultCDO, eps, "Unfunded: Breakeven premiums do not match");

      //funded 
      cdxPricer.CDX.Funded = true;
      cdoPricer.CDO.CdoType = CdoType.FundedFixed;
      resultCDX = cdxPricer.BreakEvenPremium();
      resultCDO = cdoPricer.BreakEvenPremium();
      AssertEqual(resultCDX, resultCDO, eps, "Funded: Breakeven premiums do not match");
      
    }
#endif

    [Test]
    public void RiskyDuration()
    {
      double resultCDX = 0;
      double resultCDO = 0;
      IPricer[] pricers = GetPricers(theDatafile);
      CDXPricer cdxPricer = (CDXPricer)pricers[0];
      SyntheticCDOPricer cdoPricer = (SyntheticCDOPricer)pricers[1];

      //unfunded 
      cdxPricer.CDX.CdxType = CdxType.Unfunded;
      cdoPricer.CDO.CdoType = CdoType.Unfunded;
      resultCDX = cdxPricer.IntrinsicRiskyDuration();
      resultCDO = cdoPricer.RiskyDuration();
      AssertEqual(resultCDX, resultCDO, eps, " Unfunded: CDX Intrinisc Risky Duration does not match cdo risky Duration");

      //funded 
      cdxPricer.CDX.CdxType = CdxType.FundedFixed;
      cdoPricer.CDO.CdoType = CdoType.FundedFixed;
      resultCDX = cdxPricer.IntrinsicRiskyDuration();
      resultCDO = cdoPricer.RiskyDuration();
      AssertEqual(resultCDX, resultCDO, eps, " Funded: CDX Intrinisc Risky Duration does not match cdo risky Duration");
    }


    [Test]
    public void PremiumSensitivity()
    {
      double resultCDX = 0;
      double resultCDO = 0;
      IPricer[] pricers = GetPricers(theDatafile);
      CDXPricer cdxPricer = (CDXPricer)pricers[0];
      SyntheticCDOPricer cdoPricer = (SyntheticCDOPricer)pricers[1];

      //unfunded 
      cdxPricer.CDX.CdxType = CdxType.Unfunded;
      cdoPricer.CDO.CdoType = CdoType.Unfunded;
      resultCDX = CDXIntrinsicPremium01(cdxPricer);
      resultCDO = cdoPricer.Premium01();
      AssertEqual(resultCDX, resultCDO, eps, "Unfunded: Premium01 does not match");

      //funded 
      cdxPricer.CDX.CdxType = CdxType.FundedFixed;
      cdoPricer.CDO.CdoType = CdoType.FundedFixed;
      resultCDX = CDXIntrinsicPremium01(cdxPricer);
      resultCDO = cdoPricer.Premium01();
      AssertEqual(resultCDX, resultCDO, eps, "Funded: Premium01 does not match");
    }

    private double CDXIntrinsicPremium01(CDXPricer pricer)
    {
      pricer.CDX.Premium += 0.0001;
      double result = pricer.Pv();
      pricer.CDX.Premium -= 0.0001;
      result -= pricer.Pv();
      return result;
    }
  }
}  
