//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Ccr;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test CDX Pricer functions based on external creadit data
  /// </summary>
  [TestFixture("TestCDXPricer_CDX.NA.HY.7")]
  [TestFixture("TestCDXPricer_CDX.NA.IG.7", Category = "Smoke")]
  [TestFixture("TestCDXPricer_FundedFloating")]
  public class TestCDX : SensitivityTest
  {
    public TestCDX(string name) : base(name)
    {}

    #region MarketMethods
    [Test, Smoke, Category("MarketMethods")]
    public void MarketValue()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).MarketValue();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void MarketValueWithInputPremium()
    {
      double marketPremium = 0.0100;
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).MarketValue(marketPremium);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void MarketPrice()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).MarketPrice() - 1;
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void MarketPriceWithInputPremium()
    {
      double marketPremium = 0.0100;
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).MarketValue(marketPremium);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void Accrued()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).Accrued();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void AccrualDays()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).AccrualDays();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void BreakEvenPremium()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).BreakEvenPremium();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void FwdPremium()
    {
      Dt forwardSettle = Dt.Add(asOf_, "3 Months");
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).FwdPremium(forwardSettle);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void FwdPremium01()
    {
      Dt forwardSettle = Dt.Add(asOf_, "3 Months");
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).EquivCDSFwdPremium01(forwardSettle);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void Premium01()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).Premium01();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void RiskyDuration()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).RiskyDuration();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void Carry()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).Carry();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void MTMCarry()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).MTMCarry();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void MarketSpread01()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).MarketSpread01();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void MarketSpread01WithInputPremium()
    {
      double marketPremium = 0.0100;
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).MarketSpread01(marketPremium);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void PriceToSpreadWithInputPrice()
    {
      double[] prices = CalcValues(cdxPricers_,
        delegate(object p)
        {
          return ((CDXPricer)p).SpreadToPrice(((CDXPricer)p).MarketPremium);
        });
      TestNumeric<double>(cdxPricers_, prices, cdxNames_,
        delegate(object p, double price)
        {
          return ((CDXPricer)p).PriceToSpread(price);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void SpreadToPrice()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).SpreadToPrice(((CDXPricer)p).MarketPremium) * 100;
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void SpreadToPriceWithInputSpread()
    {
      double marketPremium = 0.0100;
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).SpreadToPrice(marketPremium) * 100;
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void ImpliedQuotedSpread()
    {
      double[] values = CalcValues(cdxPricers_,
        delegate(object p)
        {
          return ((CDXPricer)p).MarketValue();
        });
      TestNumeric<double>(cdxPricers_, values, cdxNames_,
        delegate(object p, double value)
        {
          return ((CDXPricer)p).ImpliedQuotedSpread(value);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void Spread01()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).EquivCDSSpread01();
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void Theta()
    {
      Dt toAsOf = Dt.Add(asOf_, 30);
      Dt toSettle = Dt.Add(asOf_, 31);
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).EquivCDSTheta(toAsOf, toSettle);// Theta(toAsOf, toSettle);
        });
    }

    [Test, Smoke, Category("MarketMethods")]
    public void Test_ReferenceDifferentFromDiscount()
    {
      CDXPricer pricer = (CDXPricer) cdxPricers_[0].Clone();
      pricer.CDX.CdxType = CdxType.FundedFloating;
      pricer.CDX.Premium = 0.005;
      double pv = pricer.Pv();
      pricer.ReferenceCurve = pricer.DiscountCurve;
      double pvR = pricer.Pv();
      Assert.AreEqual(pv, pvR, 1e-16, "pv");
    }

    #endregion // Test_MarketMethods

    #region RelativeValueMethods
    [Test, Smoke, Category("RelativeValueMethods")]
    public void IntrinsicValue()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).IntrinsicValue();
        });
    }

    [Test, Smoke, Category("RelativeValueMethods")]
    public void Pv()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).Pv();
        });
    }

    [Test, Smoke, Category("RelativeValueMethods")]
    public void FullPrice()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).FullPrice();
        });
    }

    [Test, Smoke, Category("RelativeValueMethods")]
    public void IntrinsicRiskyDuration()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).IntrinsicRiskyDuration();
        });
    }

    [Test, Smoke, Category("RelativeValueMethods")]
    public void IntrinsicSurvivalProbability()
    {
      TestNumeric(cdxPricers_, cdxNames_,
        delegate(object p)
        {
          return ((CDXPricer)p).IntrinsicSurvivalProbability();
        });
    }

    [Test, Smoke, Category("RelativeValueMethods")]
    public void Basis()
    {
      double[] values = CalcValues(cdxPricers_,
        delegate(object p)
        {
          return ((CDXPricer)p).IntrinsicValue();
        });
      TestNumeric<double>(cdxPricers_, values, cdxNames_,
        delegate(object p, double value)
        {
          return ((CDXPricer)p).Basis(value);
        });
    }

    [Test, Smoke, Category("RelativeValueMethods")]
    public void Factor()
    {
      double[] values = CalcValues(cdxPricers_,
        delegate(object p)
        {
          return ((CDXPricer)p).IntrinsicValue();
        });
      TestNumeric<double>(cdxPricers_, values, cdxNames_,
        delegate(object p, double value)
        {
          return ((CDXPricer)p).Factor(value);
        });
    }
    #endregion // Test_RelativeValueMethods

    #region SummaryRiskMethods
    [Test, Smoke, Category("SummaryRiskMethods")]
    public void SensitivitySpread01()
    {
      Spread01(cdxPricers_, cdxNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void SpreadGamma()
    {
      SpreadGamma(cdxPricers_, cdxNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void SpreadHedge()
    {
      SpreadHedge(cdxPricers_, cdxNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void IR01()
    {
      IR01(cdxPricers_, cdxNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void Recovery01()
    {
      Recovery01(cdxPricers_, cdxNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void VOD()
    {
      VOD(cdxPricers_, cdxNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void SensitivityTheta()
    {
      Theta(cdxPricers_, cdxNames_);
    }
    #endregion //SummaryRiskMethods

    #region RiskMethods
    [Test, Category("RiskMethods")]
    public void SpreadSensitivity()
    {
      Spread(cdxPricers_);
    }

    [Test, Category("RiskMethods")]
    public void RateSensitivity()
    {
      Rate(cdxPricers_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(cdxPricers_);
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity()
    {
      Recovery(cdxPricers_);
    }

    #endregion // RiskMethods

    [Test, Category("PricingMethods")]
    [Ignore("Not work yet")]
    public void TestFastPv()
    {
      var pricer = (CDXPricer)cdxPricers_[0].Clone();
      pricer.Settle = pricer.AsOf; 
      var stopWatch = new Stopwatch();
      CcrPricer lazyPricer = CcrPricer.Get(pricer);
      double pvlazy = 0;
      pvlazy = lazyPricer.FastPv(pricer.Settle);
      double pv = 0;
      pv = pricer.Pv();
      Assert.AreEqual(pv , pvlazy, 1e-6, "pv");
    }

    #region SetUp
    /// <summary>
    ///    Initializer
    /// </summary>
    /// 
    /// <remarks>
    ///   This function is called once after a class object is constructed 
    ///   and public properties are set.
    /// </remarks>
    /// 
    [OneTimeSetUp]
    public void Initialize()
    {
      cdxPricers_ = CreateCDXPricers(
        LiborDataFile, CreditDataFile, IndexDataFile,
        ToDt(this.PricingDate), ToDt(this.SettleDate));
      if (cdxPricers_ != null && cdxPricers_.Length > 0)
      {
        asOf_ = cdxPricers_[0].AsOf;
        cdxNames_ = new string[cdxPricers_.Length];
        for (int i = 0; i < cdxNames_.Length; ++i)
          cdxNames_[i] = cdxPricers_[i].CDX.Description;
      }
      return;
    }

    /// <summary>
    ///   Create an array of CDX pricers
    /// </summary>
    /// <param name="irDataFile">Discount data</param>
    /// <param name="creditDataFile">CDS data</param>
    /// <param name="indexDataFile">Index data</param>
    /// <param name="asOf">pricing date</param>
    /// <param name="settle">settle date</param>
    /// <returns>CDX Pricers</returns>
    public CDXPricer[] CreateCDXPricers(
      string irDataFile,
      string creditDataFile,
      string indexDataFile,
      Dt asOf, Dt settle)
    {
      // Load discount and survival curves
      DiscountCurve discountCurve = LoadDiscountCurve(irDataFile);
      SurvivalCurve[] survivalCurves = LoadCreditCurves(creditDataFile, discountCurve);

      // Load index data
      string filename = GetTestFilePath(indexDataFile);
      BasketData.Index id = (BasketData.Index)XmlLoadData(filename, typeof(BasketData.Index));

      // Check survivalcurves
      if (id.CreditNames != null && id.CreditNames.Length != survivalCurves.Length)
      {
        SurvivalCurve[] sc = survivalCurves;
        survivalCurves = new SurvivalCurve[id.CreditNames.Length];
        int idx = 0;
        foreach (string name in id.CreditNames)
          survivalCurves[idx++] = (SurvivalCurve)FindCurve(name, sc);
      }

      // Count positive CDX quotes
      int quotesCount = 0;
      foreach (double q in id.Quotes)
        if (q > 0) ++quotesCount;
      if (quotesCount <= 0)
        throw new System.Exception(filename + ": index quotes data not found");

      // Create CDX pricers
      CDXPricer[] cdxPricers = new CDXPricer[quotesCount];
      Dt effective = Dt.FromStr(id.Effective, "%D");
      if (asOf.IsEmpty())
        asOf = survivalCurves[0].AsOf;
      if (settle.IsEmpty())
        settle = Dt.Add(asOf, 1);
      for (int i = 0, idx = 0; i < id.Quotes.Length; ++i)
      {
        if (id.Quotes[i] <= 0)
          continue;
        Dt maturity;
        if (id.Maturities != null)
          maturity = Dt.FromStr(id.Maturities[i], "%D");
        else if (id.TenorNames != null)
          //maturity = Dt.CDSMaturity(effective, id.TenorNames[i]);
          maturity = Dt.Add(effective, id.TenorNames[i]);
        else
          //maturity = Dt.CDSMaturity(effective, "5Y");
          maturity = Dt.Add(effective, "5Y");
        CDX cdx = new CDX(effective, maturity,
          id.Currency, id.DealPremia[i] / 10000, id.DayCount, id.Frequency, id.Roll, id.Calendar);
        cdx.LastCoupon = GetLastCoupon(cdx);
        cdx.CdxType = this.CdxType;
        if (cdx.CdxType == CdxType.FundedFixed || cdx.CdxType == CdxType.FundedFloating)
          cdx.Funded = true;
        else
          cdx.Funded = false;

        double spread = id.Quotes[i] / 10000.0;
        CDXPricer pricer = new CDXPricer(cdx, asOf, settle, discountCurve, survivalCurves, spread);
        if (id.QuotesArePrices)
        {
          pricer.MarketQuote = id.Quotes[i] / 100;
          pricer.QuotingConvention = QuotingConvention.FlatPrice;
        }
        pricer.CurrentRate = this.ResetRate;
        if (Notional != 0) pricer.Notional = Notional;
        cdxPricers[idx++] = pricer;
      }

      return cdxPricers;
    }

    private Dt GetLastCoupon(CDX cdx)
    {
      Dt first = cdx.FirstPrem;
      Dt last = first, next = first;
      while(next < cdx.Maturity)
      {
        last = next;
        next = Dt.CDSRoll(last);
      }
      return last;
    }

    /// <summary>
    ///    Helper to find a curve with given name
    /// </summary>
    /// <param name="name">name</param>
    /// <param name="curves">array of curves</param>
    /// <returns>the curve with given name</returns>
    private static Curve FindCurve(string name, Curve[] curves)
    {
      foreach (Curve c in curves)
        if (String.Compare(name, c.Name) == 0)
          return c;
      throw new System.Exception(String.Format("Curve name '{0}' not found", name));
    }
    #endregion // SetUp

    #region Properties
    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = "data/USD.LIBOR_Data.xml";

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string CreditDataFile { get; set; } = "data/CDX.NA.HY.7_CreditData.xml";

    /// <summary>
    ///   Data for index notes
    /// </summary>
    public string IndexDataFile { get; set; } = "data/CDX.NA.HY.7_IndexData.xml";

    /// <summary>
    ///   Rate reset
    /// </summary>
    public double ResetRate { get; set; } = 0;

    /// <summary>
    ///   CDX type (Unfunded, FundedFixed, FundedFloating)
    /// </summary>
    public CdxType CdxType { get; set; } = CdxType.Unfunded;

    #endregion //Properties

    #region Data
    const double epsilon = 1.0E-7;

    // Data to be initialized by set up routined
    private CDXPricer[] cdxPricers_ = null;
    private string[] cdxNames_ = null;
    private Dt asOf_;
    //private double notional_ = 1000000;

    #endregion // Data
  } // TestCDX
}  
