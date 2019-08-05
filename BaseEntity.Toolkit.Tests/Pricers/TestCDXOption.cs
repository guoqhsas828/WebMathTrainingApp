//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test CDX Option Pricer functions based on external creadit data
  /// </summary>
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Payer_Black_Market", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Payer_Black_Market_Price", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Payer_Black_Relative", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Payer_Black_Relative_Price", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Payer_Modified_Market", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Payer_Modified_Relative", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Receiver_Black_Market", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Receiver_Black_Market_Price", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Receiver_Black_Relative", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Receiver_Black_Relative_Price", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Receiver_Modified_Market", Category = "Smoke")]
  [TestFixture("TestCDXOption_CDX.NA.IG.7_Receiver_Modified_Relative", Category = "Smoke")]
  [TestFixture("CDXOption_CrossOver_Payer500_Black")]
  [TestFixture("CDXOption_CrossOver_Payer500_BlackPrice")]
  [TestFixture("CDXOption_CrossOver_Payer500_Modified")]
  [TestFixture("CDXOption_CrossOver_Receiver400_Black")]
  [TestFixture("CDXOption_CrossOver_Receiver400_BlackPrice")]
  [TestFixture("CDXOption_CrossOver_Receiver400_Modified")]
  public class TestCDXOption : SensitivityTest
  {
    public TestCDXOption(string name) : base(name)
    {}

    #region ClassMethods
    [Test, Smoke, Category("ClassMethods")]
    public void Pv()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).Pv();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Accrued()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).Accrued();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void MarketValue()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).MarketValue();
        });
    }

    [Test, Category("ClassMethods")]
    public void MarketValueWithParam()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double vol = ParamVolatility;
          if (Double.IsNaN(ParamVolatility))
            vol = ((ICreditIndexOptionPricer)p).Volatility * 1.05;
          return ((ICreditIndexOptionPricer)p).MarketValue(vol);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Intrinsic()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object pr)
        {
          var p = (ICreditIndexOptionPricer)pr;
          return p.Intrinsic() / p.Notional;
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Vega()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double bump = vegaBump_;
          if (Double.IsNaN(bump))
            bump = 0.05 * ((ICreditIndexOptionPricer)p).Volatility;
          return ((ICreditIndexOptionPricer)p).Vega(bump);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void FairValue()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).FairValue();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void FairValueWithParam()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double vol = ParamVolatility;
          if (Double.IsNaN(ParamVolatility))
            vol = ((ICreditIndexOptionPricer)p).Volatility * 1.05;
          return ((ICreditIndexOptionPricer)p).FairValue(vol);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void FairPrice()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).FairPrice();
        });
    }


    [Test, Smoke, Category("ClassMethods")]
    public void FairPriceWithParam()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double vol = ParamVolatility;
          if (Double.IsNaN(ParamVolatility))
            vol = ((ICreditIndexOptionPricer)p).Volatility * 1.05;
          return ((ICreditIndexOptionPricer)p).FairPrice(vol);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void ExercisePrice()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return -((ICreditIndexOptionPricer)p).ExerciseValue();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void StrikePrice()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).StrikeValue();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void IVol()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double fv = ivolFv_;
          if (Double.IsNaN(fv))
            fv = ((ICreditIndexOptionPricer)p).FairPrice();
          return ((ICreditIndexOptionPricer)p).IVol(fv);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void ForwardValue()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).ForwardValue();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void AdjustedForwardSpread()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).AdjustedForwardSpread();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void BpVolatility()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).BpVolatility();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Delta()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double bump = marketDeltaBump_;
          if (Double.IsNaN(bump))
            bump = ((ICreditIndexOptionPricer)p).GetIndexSpread() * 0.05;
          return ((ICreditIndexOptionPricer)p).MarketDelta(bump);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Gamma()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double bump = marketGammaBump_;
          if (Double.IsNaN(bump))
            bump = ((ICreditIndexOptionPricer)p).GetIndexSpread() * 0.05;
          return ((ICreditIndexOptionPricer)p).MarketGamma(bump, bump, true);
        });
    }


    [Test, Smoke, Category("ClassMethods")]
    public void GammaNoScale()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          double bump = marketGammaBump_;
          if (Double.IsNaN(bump))
            bump = ((ICreditIndexOptionPricer)p).GetIndexSpread() * 0.05;
          return ((ICreditIndexOptionPricer)p).MarketGamma(bump, bump, false);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Theta()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          Dt toAsOf = Dt.Add(((ICreditIndexOptionPricer)p).AsOf, this.ThetaPeriod);
          Dt toSettle = Dt.Add(((ICreditIndexOptionPricer)p).Settle, this.ThetaPeriod);
          return ((ICreditIndexOptionPricer)p).MarketTheta(toAsOf, toSettle);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void IndexUpfrontPrice()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).IndexUpfrontValue();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void ExpectedSurvival()
    {
      TestNumeric(pricers_, optionNames_,
        delegate(object p)
        {
          return ((ICreditIndexOptionPricer)p).ExpectedSurvival;
        });
    }
    #endregion // ClassMethods

    #region SummaryRiskMethods
    [Test, Category("SummaryRiskMethods")]
    public void SensitivitySpread01()
    {
      Spread01(pricers_, optionNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadGamma()
    {
      SpreadGamma(pricers_, optionNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadHedge()
    {
      SpreadHedge(pricers_, optionNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void IR01()
    {
      IR01(pricers_, optionNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Recovery01()
    {
      Recovery01(pricers_, optionNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void VOD()
    {
      VOD(pricers_, optionNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SensitivityTheta()
    {
      Theta(pricers_, optionNames_);
    }
    #endregion //SummaryRiskMethods

    #region RiskMethods
    [Test, Category("RiskMethods")]
    public void SpreadSensitivity()
    {
      Spread(pricers_);
    }

    [Test, Category("RiskMethods")]
    public void RateSensitivity()
    {
      Rate(pricers_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(pricers_);
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity()
    {
      Recovery(pricers_);
    }

    #endregion // RiskMethods

    #region SetUp
    /// <summary>
    ///   Initialize the test fixture
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      DiscountCurve discountCurve = LoadDiscountCurve(LiborDataFile);

      SurvivalCurve[] survivalCurves = CreditDataFile == null ? null
        : LoadCreditCurves(CreditDataFile, discountCurve);

      // pricing date
      Dt asOf = ToDt(PricingDate);
      Dt settle = ToDt(SettleDate);

      // Create index
      Dt indexEffective = IndexEffective;
      Dt maturity = IndexMaturity;
      Dt firstPrem = IndexFirstPrem;
      CDX index = new CDX(indexEffective, maturity,
        Get(Currency.USD), DealPremium / 10000.0,
        Get(DayCount.Actual360), Get(Frequency.Quarterly),
        Get(BDConvention.Following), Get(Calendar.NYB));
      if (firstPrem.IsValid())
        index.FirstPrem = firstPrem;
      Dt expiry = OptionExpiration;
      Dt optionEffective = optionEffective_.IsEmpty() ? asOf : optionEffective_;

      // Create option
      var cdxo = new CDXOption(optionEffective, index.Ccy, index, expiry,
        OptionType, OptionStyle.European, 0, OptionStrikeIsPrice);

      // create option pricer
      var quote = MarketQuoteIsPrice
        ? new MarketQuote(MarketQuote / 100, QuotingConvention.FlatPrice)
        : new MarketQuote(MarketQuote / 10000, QuotingConvention.CreditSpread);
      var volatility = new FlatVolatility { Volatility = SpreadVolatility };
      var modelData = new CDXOptionModelData()
      {
        Choice = Choice //| CDXOptionModelParam.MarketPayoffConsistent
      };

      double[] strikes = this.OptionStrikes;
      var pricers = new ICreditIndexOptionPricer[strikes.Length];
      string[] names = new string[strikes.Length];
      for (int i = 0; i < strikes.Length; ++i)
      {
        names[i] = OptionNames.Length > i ? OptionNames[i] : ("#" + (i + 1));
        pricers[i] = CreatePricer(asOf, settle,
          discountCurve, quote, survivalCurves,
          OptionStrikeIsPrice ? (strikes[i] / 100) : (strikes[i] / 10000),
          cdxo, names[i], modelData, volatility);
      }
      optionNames_ = names;
      pricers_ = pricers;
      return;
    }

    private ICreditIndexOptionPricer CreatePricer(
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      MarketQuote quote, SurvivalCurve[] survivalCurves,
      double strike, CDXOption cdxo, string name,
      CDXOptionModelData data, 
      CalibratedVolatilitySurface volatility)
    {
      CDXOption co = (CDXOption)cdxo.Clone();
      co.Strike = strike;
      co.Description = name;
      co.Validate();

      return co.CreatePricer(asOf, settle, discountCurve, quote,
        Dt.Empty, IndexRecoveryRate, BasketSize, survivalCurves,
        OptionModel, data, volatility, GetNotional(10000000), null);
    }


    #endregion // SetUp

    #region Properties
    public bool PriceVolatilityApproach => OptionModel == CDXOptionModelType.BlackPrice;

    /// <summary>
    ///   Index effective date
    /// </summary>
    public Dt IndexEffective { get; set; }

    /// <summary>
    ///   Index first premium date
    /// </summary>
    public Dt IndexFirstPrem { get; set; }

    /// <summary>
    ///   Index maturity date
    /// </summary>
    public Dt IndexMaturity { get; set; }

    /// <summary>
    ///   Deal premium
    /// </summary>
    public double DealPremium { get; set; } = Double.NaN;

    /// <summary>
    ///   Market recovery rate of the index
    /// </summary>
    public double IndexRecoveryRate { get; set; } = 0.4;

    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = "data/USD.LIBOR_Data.xml";

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string CreditDataFile { get; set; } = null;

    /// <summary>
    ///   Option Model ("Black" or "Modified")
    /// </summary>
    public CDXOptionModelType OptionModel { get; set; } = CDXOptionModelType.Black;

    public CDXOptionModelParam Choice { get; set; } = CDXOptionModelParam.AdjustSpread
        | CDXOptionModelParam.ForceFlatMarketCurve
        | CDXOptionModelParam.AdjustSpreadByMarketMethod;

    /// <summary>
    ///   Option product name
    /// </summary>
    public string[] OptionNames
    {
      get
      {
        if (optionNames_ == null)
          optionNames_ = new string[0];
        return optionNames_;
      }
      set { optionNames_ = value; }
    }

    /// <summary>
    ///   Option effective date
    /// </summary>
    public Dt OptionEffective
    {
      get { return optionEffective_; }
      set { optionEffective_ = value; }
    }

    /// <summary>
    ///   Option expiration date
    /// </summary>
    public Dt OptionExpiration { get; set; }

    /// <summary>
    ///   Option strike
    /// </summary>
    public double[] OptionStrikes
    {
      get
      {
        if (optionStrikes_ == null)
          optionStrikes_ = new double[1];
        return optionStrikes_;
      }
      set { optionStrikes_ = value; }
    }

    /// <summary>
    ///   Indicator whether option strike is price
    /// </summary>
    public bool OptionStrikeIsPrice { get; set; } = false;

    /// <summary>
    ///   Market quote for spread or price of the index
    /// </summary>
    public double MarketQuote { get; set; } = Double.NaN;

    /// <summary>
    ///   Indicator whether underlying index is quoted in price
    /// </summary>
    public bool MarketQuoteIsPrice { get; set; } = false;

    /// <summary>
    ///   Option type
    /// </summary>
    public PayerReceiver OptionType { get; set; } = PayerReceiver.Receiver;

    /// <summary>
    ///   Spread volatility
    /// </summary>
    public double SpreadVolatility { get; set; } = Double.NaN;

    /// <summary>
    ///   Spread center
    /// </summary>
    public double SpreadCenter { get; set; } = Double.NaN;

    /// <summary>
    ///   Adjust spread or not
    /// </summary>
    public bool AdjustSpread => (Choice & CDXOptionModelParam.AdjustSpread) != 0;

    /// <summary>
    ///   Volatility as input param
    /// </summary>
    public double ParamVolatility { get; set; } = Double.NaN;

    public int BasketSize { get; set; } = 0;

    #endregion //Properties

    #region Data
    // Accuracy
    const double epsilon = 1.0E-7;

    // Interest rate and credit data

    // Index terms

    // Option terms
    private string[] optionNames_ = null;
    private Dt optionEffective_;
    private double[] optionStrikes_ = null;

    // Pricer data

    // Test input data
    private double vegaBump_ = Double.NaN;
    private double marketDeltaBump_ = Double.NaN;
    private double marketGammaBump_ = Double.NaN;
    private double ivolFv_ = Double.NaN;

    // Data to be initialized by set up routined
    private ICreditIndexOptionPricer[] pricers_ = null;

    #endregion // Data
  }
}
