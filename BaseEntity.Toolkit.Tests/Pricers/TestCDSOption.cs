//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test CDS Option Pricer functions based on external creadit data
  /// </summary>
  [TestFixture("TestCDSOption_knockout_CDX.NA.HY.7", Category = "Smoke")]
  [TestFixture("TestCDSOptionPricer_CDX.NA.HY.7")]
  public class TestCDSOption : SensitivityTest
  {

    public TestCDSOption(string name) : base(name)
    {}

    #region ClassMethods
    [Test, Smoke, Category("ClassMethods")]
    public void Pv()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).Pv();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void FairPrice()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).FairPrice();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Accrued()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).Accrued();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void FrontEndProtection()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).FrontEndProtection();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void IVol()
    {
      double[] fvs = CalcValues(cdsoPricers_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).FairPrice();
        });
      TestNumeric(cdsoPricers_, fvs, cdsoNames_,
        delegate(object p, double fv)
        {
          return ((CDSOptionPricer)p).IVol(fv);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void ForwardPremium()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).ForwardPremium();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Numeraire()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).Numeraire();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Intrinsic()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).Intrinsic();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void Vega()
    {
      double bump = VegaBump;
      if (Double.IsNaN(bump))
        bump = 0.01;
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).Vega(bump);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void BpVolatility()
    {
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).BpVolatility();
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void MarketDelta()
    {
      double bump = DeltaBump;
      if (Double.IsNaN(bump))
        bump = 0.0001;
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).MarketDelta(bump);
        });
    }

    [Test, Smoke, Category("ClassMethods")]
    public void MarketGamma()
    {
      double bump = GammaBump;
      bool scale = GammaScale;
      if (Double.IsNaN(bump))
        bump = 0.0010;
      TestNumeric(cdsoPricers_, cdsoNames_,
        delegate(object p)
        {
          return ((CDSOptionPricer)p).MarketGamma(bump, scale);
        });
    }
    #endregion // ClassMethods

    #region SummaryRiskMethods
    [Test, Smoke, Category("SummaryRiskMethods")]
    public void Spread01()
    {
      Spread01(cdsoPricers_, cdsoNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void SpreadGamma()
    {
      SpreadGamma(cdsoPricers_, cdsoNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void SpreadHedge()
    {
      SpreadHedge(cdsoPricers_, cdsoNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void IR01()
    {
      IR01(cdsoPricers_, cdsoNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void Recovery01()
    {
      Recovery01(cdsoPricers_, cdsoNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void VOD()
    {
      VOD(cdsoPricers_, cdsoNames_);
    }

    [Test, Smoke, Category("SummaryRiskMethods")]
    public void Theta()
    {
      Theta(cdsoPricers_, cdsoNames_);
    }
    #endregion //SummaryRiskMethods

    #region RiskMethods
    [Test, Category("RiskMethods")]
    public void SpreadSensitivity()
    {
      Spread(cdsoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void RateSensitivity()
    {
      Rate(cdsoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(cdsoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity()
    {
      Recovery(cdsoPricers_);
    }
    #endregion // RiskMethods

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
      // Load discount curve
      string filename = GetTestFilePath(LiborDataFile);
      DiscountData dd = (DiscountData)XmlLoadData(filename, typeof(DiscountData));
      DiscountCurve discountCurve = dd.GetDiscountCurve();
      if (discountCurve == null)
        throw new System.Exception(filename + ": Invalid discount data");

      // Load credit Curves
      filename = GetTestFilePath(CreditDataFile);
      CreditData cd = (CreditData)XmlLoadData(filename, typeof(CreditData));
      SurvivalCurve[] survivalCurves = cd.GetSurvivalCurves(discountCurve);
      if (survivalCurves == null)
        throw new System.Exception(filename + ": Invalid credit data");

      // Create CDS pricers
      cdsoPricers_ = new IPricer[survivalCurves.Length];
      cdsoNames_ = new string[survivalCurves.Length];
      Dt effective = this.EffectiveDate != 0 ?
        new Dt(this.EffectiveDate) : survivalCurves[0].AsOf;
      Dt expiration = this.ExpirationDate != 0 ?
        new Dt(this.ExpirationDate) : Dt.Add(effective, "3M");
      Dt maturity = this.MaturityDate != 0 ?
        new Dt(this.MaturityDate) : Dt.CDSMaturity(effective, "5Y");
      Dt asOf = asOf_ = this.PricingDate != 0 ?
        new Dt(this.PricingDate) : survivalCurves[0].AsOf;
      Dt settle = this.SettleDate != 0 ?
        new Dt(this.SettleDate) : Dt.Add(asOf, 1);
      double notional = this.Notional == 0 ? 1000000 : this.Notional;
      for (int i = 0; i < survivalCurves.Length; ++i)
      {
        cdsoNames_[i] = survivalCurves[i].Name;
        CurveTenor tenor = survivalCurves[i].TenorAfter(maturity);
        double premium = ((CDS)tenor.Product).Premium;
        double strike = Double.IsNaN(Strike) || Strike < 0 ? premium : (Strike / 10000);
        CDSOption cdso = new CDSOption(effective, maturity, cd.Currency,
          cd.DayCount, cd.Frequency, cd.Roll, cd.Calendar,
          expiration, type_, OptionStyle.European, strike);
        cdso.Knockout = KnockOut;
        cdso.Validate();

        CDSOptionPricer pricer = new CDSOptionPricer(cdso, asOf, settle,
          discountCurve, survivalCurves[i], Volatility);
        pricer.Notional = notional;
        if (RecoveryRate >= 0.0)
          pricer.RecoveryCurve = new RecoveryCurve(asOf, RecoveryRate);
        pricer.Skew = Skew;
        pricer.Validate();

        cdsoPricers_[i] = pricer;
      }

      return;
    }

    #endregion SetUp

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
    ///   CDS pricers based on CashflowStreamPricer instead of CashflowPricer
    /// </summary>
    public bool UseCashflowStreamPricer { get; set; } = false;

    /// <summary>
    ///   Option expiration date
    /// </summary>
    public int ExpirationDate { get; set; } = 0;

    /// <summary>
    ///   Option strike
    /// </summary>
    public double Strike { get; set; } = Double.NaN;

    /// <summary>
    ///   Option is knock-out or not
    /// </summary>
    public bool KnockOut { get; set; } = false;

    /// <summary>
    ///   Spread volatility
    /// </summary>
    public double Volatility { get; set; } = 0.35;

    /// <summary>
    ///   Skewness parameter (2=Normal)
    /// </summary>
    public double Skew { get; set; } = 2.0;

    /// <summary>
    ///   Recovery rate for insured instrument (if -ve use survival curve recovery)
    /// </summary>
    public double RecoveryRate { get; set; } = 0;

    public double VegaBump { get; set; } = Double.NaN;

    public double DeltaBump { get; set; } = Double.NaN;

    public double GammaBump { get; set; } = Double.NaN;

    public bool GammaScale { get; set; } = false;

    #endregion //Properties

    #region Data
    const double epsilon = 1.0E-7;

    // Calibrator Pricer configuration parameters
    //private bool configUseCashflowStream_ = true;
    //private bool includeMaturityAccrual_ = true;

    // CDS Pricer configuration parameters
    private IPricer[] cdsoPricers_ = null;
    private string[] cdsoNames_ = null;
    private Dt asOf_;

    // Parameters

    private PayerReceiver type_ = PayerReceiver.Payer;

    #endregion // Data
  }
}
