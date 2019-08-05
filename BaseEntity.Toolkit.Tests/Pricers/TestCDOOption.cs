//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
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
  [TestFixture("TestCDOOption_CDX.NA.IG.7_Payer")]
  [TestFixture("TestCDOOption_CDX.NA.IG.7_Receiver")]
  public class TestCDOOption : SensitivityTest
  {
    public TestCDOOption(string name) : base(name)
    {}

    #region ClassMethods
    [Test, Category("ClassMethods")]
    public void Pv()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).Pv();
        });
    }

    [Test, Category("ClassMethods")]
    public void Accrued()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).Accrued();
        });
    }

    [Test, Category("ClassMethods")]
    public void Intrinsic()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).Intrinsic();
        });
    }

    [Test, Category("ClassMethods")]
    public void Vega()
    {
      double bump = vegaBump_;
      if (Double.IsNaN(bump))
        bump = 0.05 * pricer_.Volatility;
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).Vega(bump);
        });
    }

    [Test, Category("ClassMethods")]
    public void FairValue()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).FairValue();
        });
    }

    [Test, Category("ClassMethods")]
    public void FairValueWithParam()
    {
      double vol = ParamVolatility;
      if(Double.IsNaN(ParamVolatility))
        vol = pricer_.Volatility * 1.05;
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).FairValue(vol);
        });
    }

    [Test, Category("ClassMethods")]
    public void ExercisePrice()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).ExercisePrice();
        });
    }

    [Test, Category("ClassMethods")]
    public void StrikePrice()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).StrikePrice();//.StrikeValue();
        });
    }

    [Test, Category("ClassMethods")]
    public void IVol()
    {
      double fv = ivolFv_;
      if (Double.IsNaN(fv))
        fv = pricer_.FairValue();
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).IVol(fv);
        });
    }

    [Test, Category("ClassMethods")]
    public void ForwardValue()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).ForwardValue();
        });
    }

    [Test, Category("ClassMethods")]
    public void AdjustedForwardSpread()
    {
      TestNumeric(pricer_, OptionName,
        delegate(object p)
        {
          return ((CDOOptionPricer)p).AdjustedForwardSpread();
        });
    }

    #endregion // ClassMethods

    #region SummaryRiskMethods
    [Test, Category("SummaryRiskMethods")]
    public void SensitivitySpread01()
    {
      Spread01(new CDOOptionPricer[] { pricer_ },
        new string[] { pricer_.Product.Description });
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadGamma()
    {
      SpreadGamma(new CDOOptionPricer[] { pricer_ },
        new string[] { pricer_.Product.Description });
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadHedge()
    {
      SpreadHedge(new CDOOptionPricer[] { pricer_ },
        new string[] { pricer_.Product.Description });
    }

    [Test, Category("SummaryRiskMethods")]
    public void IR01()
    {
      IR01(new CDOOptionPricer[] { pricer_ },
        new string[] { pricer_.Product.Description });
    }

    [Test, Category("SummaryRiskMethods")]
    public void Recovery01()
    {
      Recovery01(new CDOOptionPricer[] { pricer_ },
        new string[] { pricer_.Product.Description });
    }

    [Test, Category("SummaryRiskMethods")]
    public void VOD()
    {
      VOD(new[] {pricer_}, new[] {pricer_.Product.Description});
    }

    [Test, Category("SummaryRiskMethods")]
    public void SensitivityTheta()
    {
      Theta(new CDOOptionPricer[] { pricer_ },
        new string[] { pricer_.Product.Description });
    }
    #endregion //SummaryRiskMethods

    #region RiskMethods
    [Test, Category("RiskMethods")]
    public void SpreadSensitivity()
    {
      Spread(new CDOOptionPricer[] { pricer_ });
    }

    [Test, Category("RiskMethods")]
    public void RateSensitivity()
    {
      Rate(new CDOOptionPricer[] { pricer_ });
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(new [] { pricer_ });
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity()
    {
      Recovery(new CDOOptionPricer[] { pricer_ });
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
      string filename = GetTestFilePath(BasketDataFile);
      BasketData bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      BasketPricer bp = bd.GetBasketPricer();

      // Create CDO
      Dt cdoEffective = ToDt(TrancheEffective);
      Dt cdoMaturity = ToDt(TrancheMaturity);
      Dt cdoFirstPrem = ToDt(TracheFirstPrem);
      SyntheticCDO cdo = new SyntheticCDO(
        cdoEffective, cdoMaturity,
        Get(Currency.USD), TranchePremium / 10000.0,
        Get(DayCount.Actual360), Get(Frequency.Quarterly),
        Get(BDConvention.Following), Get(Calendar.NYB));
      if (!cdoFirstPrem.IsEmpty())
        cdo.FirstPrem = cdoFirstPrem;
      cdo.Attachment = attachment_;
      cdo.Detachment = detachment_;
      if (cdoFee_ != 0.0)
      {
        cdo.Fee = cdoFee_;
        cdo.FeeSettle = cdoEffective;
      }


      // Create option
      Dt effective = ToDt(OptionEffective);
      CDOOption co = new CDOOption(effective, Get(Currency.USD),
        cdo, ToDt(OptionExpiration),
        OptionType, OptionStyle.European,
        OptionStrike / 10000.0, OptionStrikeIsPrice);
      co.Description = OptionName;
      co.Validate();

      // create option pricer
      Dt asOf = ToDt(PricingDate);
      Dt settle = ToDt(SettleDate);
      double[] notionals = new double[] { GetNotional(10000000) };
      bool rescaleStrikes = false;
      SyntheticCDOPricer[] cdoPricer = BasketPricerFactory.CDOPricerHeterogeneous(
        new SyntheticCDO[] { cdo }, settle, asOf, settle,
        discountCurve, bp.SurvivalCurves, bp.Principals, bp.Copula, bp.Correlation,
        bp.StepSize, bp.StepUnit, 0, 0.0, notionals, rescaleStrikes);

      double spread = MarketQuote;

      // Create option pricer
      pricer_ = new CDOOptionPricer(
        co, asOf, settle, discountCurve, cdoPricer[0].Basket,
        spread / 10000.0, SpreadVolatility);
      pricer_.Notional = notionals[0];
      //pricer_.AdjustSpread = adjustSpread;
    }

    #endregion // SetUp

    #region Properties
    /// <summary>
    ///   Tranche effective date
    /// </summary>
    public int TrancheEffective { get; set; } = 0;

    /// <summary>
    ///   Tranche first premium date
    /// </summary>
    public int TracheFirstPrem { get; set; } = 0;

    /// <summary>
    ///   Tranche maturity date
    /// </summary>
    public int TrancheMaturity { get; set; } = 0;

    /// <summary>
    ///   Tranche premium
    /// </summary>
    public double TranchePremium { get; set; } = Double.NaN;

    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = "data/USD.LIBOR_Data.xml";

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string BasketDataFile { get; set; } = null;

    /// <summary>
    ///   Option Model ("Black" or "Modified")
    /// </summary>
    public string OptionModel { get; set; } = "Black";

    /// <summary>
    ///   Option product name
    /// </summary>
    public string OptionName { get; set; } = null;

    /// <summary>
    ///   Option effective date
    /// </summary>
    public int OptionEffective { get; set; } = 0;

    /// <summary>
    ///   Option expiration date
    /// </summary>
    public int OptionExpiration { get; set; } = 0;

    /// <summary>
    ///   Option strike
    /// </summary>
    public double OptionStrike { get; set; } = Double.NaN;

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
    ///   Adjust spread or not
    /// </summary>
    public bool AdjustSpread { get; set; } = true;

    /// <summary>
    ///   Volatility as input param
    /// </summary>
    public double ParamVolatility { get; set; } = Double.NaN;

    #endregion //Properties

    #region Data
    // Accuracy
    const double epsilon = 1.0E-7;

    // Interest rate and credit data

    // Index terms
    private double attachment_ = 0.0;
    private double detachment_ = 0.03;
    private double cdoFee_ = 0.0;

    // Option terms

    // Pricer data

    // Test input data
    private double vegaBump_ = Double.NaN;
    //private double marketDeltaBump_ = Double.NaN; Not used. RTD Sep07
    //private double marketGammaBump_ = Double.NaN; Not used. RTD Sep07
    private double ivolFv_ = Double.NaN;

    // Data to be initialized by set up routined
    private CDOOptionPricer pricer_ = null;
    #endregion // Data
  }
}
