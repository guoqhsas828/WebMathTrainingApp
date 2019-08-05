//
// Compare CashflowModel CDS value calculations
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using System.IO;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestCashflowModel : ToolkitTestBase
  {
    #region Data

    private string tenor_ = "5Y";
    private double discountRate_ = 0.04;
    private double hazardRate_ = 0.0025;
    private double recoveryRate_ = 0.40;

    private double premium_ = 100;
    private double fee_ = 0;
    private int feeSettleDate_ = 0;

    private Dt asOf_, settle_;
    #endregion // Data

    #region SetUp
    [OneTimeSetUp]
    public void Initialize()
    {
      // Get the user input dates
      if (this.PricingDate == 0)
      {
        asOf_ = new Dt(20180517);// Dt.Today();
        settle_ = Dt.Add(asOf_, 1);
      }
      else
      {
        asOf_ = new Dt(this.PricingDate);
        settle_ = this.SettleDate == 0 ? Dt.Add(asOf_, 1) : new Dt(this.SettleDate);
      }
      return;
    }

    /// <summary>
    ///   Create a CDS pricer
    /// </summary>
    /// <returns>CDS pricer</returns>
    private ICDSPricer CreateCDSPricer()
    {
      Dt asOf = asOf_;
      Dt settle = settle_;
      Dt effective = this.EffectiveDate == 0 ? settle : new Dt(this.EffectiveDate);
      Dt maturity = this.MaturityDate == 0 ? Dt.CDSMaturity(effective, tenor_) : new Dt(this.MaturityDate);

      CDS cds = CreateCDS(effective, maturity);
      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate_);
      SurvivalCurve survivalCurve = CreateSurvivalCurve(asOf, settle);

      ICDSPricer pricer = new CDSCashflowPricer(cds, asOf, settle,
        discountCurve, survivalCurve, null, 0.0, 0, TimeUnit.None);
      return pricer;
    }

    /// <summary>
    ///   Create a CDS product
    /// </summary>
    private CDS CreateCDS(Dt effective, Dt maturity)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      CDS cds = new CDS(effective, maturity, ccy,
        this.FirstPremDate == 0 ? new Dt() : new Dt(this.FirstPremDate),
        premium_ /10000, dayCount, freq, roll, calendar,
        fee_, feeSettleDate_ == 0 ? new Dt() : new Dt(feeSettleDate_));

      return cds;
    }

    /// <summary>
    ///   Create a survival curve
    /// </summary>
    private SurvivalCurve CreateSurvivalCurve(Dt asOf, Dt settle)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      Dt maturity = Dt.CDSMaturity(settle, tenor_);
      SurvivalCurve survivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        asOf, ccy, "None",
        BaseEntity.Toolkit.Numerics.InterpMethod.Linear, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
        new Dt[] { maturity },
        new double[] { Math.Exp(-hazardRate_ * Dt.FractDiff(settle, maturity) / 365) },
        new string[] { tenor_ },
        new DayCount[] { dayCount }, new Frequency[] { freq },
        new BDConvention[] { roll }, new Calendar[] { calendar },
        new double[] { recoveryRate_ }, 0.0);
      return survivalCurve;
    }
    #endregion // SetUp

    #region Type
    class MyCashflowPricer : CashflowPricer
    {
      private PaymentSchedule _ps;
      public MyCashflowPricer(PaymentSchedule ps)
        : base(null)
      {
        _ps = ps;
      }

      public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
      {
        return _ps;
      }
    }

    IPricer CreateCashflowPricer(ICDSPricer pricer)
    {
      IPricer p = new MyCashflowPricer(
          ((CashflowPricer)pricer).Payments);
      p.AsOf = pricer.AsOf;
      p.Settle = pricer.Settle;
      return p;
    }
    #endregion Type

    #region Helpers
    void TestFeeOnSettle(string label, DiscountCurve discountCurve)
    {
      const double tol = 1E-7; // this is temporary to get around the 1 day discounting

      ICDSPricer pricer = CreateCDSPricer();
      if (discountCurve!=null)
        pricer.DiscountCurve = discountCurve;
      pricer.CDS.Fee = 0;
      double pv = pricer.Pv();

      // Setting Fee=-pv on settle
      pricer.CDS.Fee = -pv / pricer.Notional;
      pricer.CDS.FeeSettle = pricer.Settle;
      pricer.Reset();
      AssertEqual(label + ": Pv unchanged",
        pv, pricer.Pv(), tol * pricer.Notional);

      // Default on settle should have pv = (recoveryRate - 1)*notional
      pricer.SurvivalCurve = (SurvivalCurve)pricer.SurvivalCurve.Clone();
      pricer.SurvivalCurve.DefaultDate = pricer.Settle;
      double df = pricer.DiscountCurve.DiscountFactor(pricer.AsOf, pricer.Settle);
      pricer.Reset();
      AssertEqual(label + ": VOD",
        (pricer.RecoveryRate - 1) * pricer.Notional * df + pricer.Accrued(),
        pricer.Pv(), tol * pricer.Notional);
    }

    void TestFeeAfterSettle(string label, DiscountCurve discountCurve, int days)
    {
      const double tol = 1E-4; // this is temporary to get around the 1 day discounting

      ICDSPricer pricer = CreateCDSPricer();
      if (discountCurve != null)
        pricer.DiscountCurve = discountCurve;
      pricer.CDS.Fee = 0;
      double pv = pricer.Pv();

      // Setting Fee=-pv on settle makes the new pricer have 0 Value
      Dt feeSettle = Dt.Add(pricer.Settle, days);
      double df = 0.5 + 0.5 * pricer.DiscountCurve.DiscountFactor(pricer.AsOf, feeSettle);
      pricer.CDS.Fee = -pv / pricer.Notional / df;
      pricer.CDS.FeeSettle = feeSettle;
      pricer.Reset();
      AssertEqual(label + ": Pv == 0", 0.0, pricer.Pv(), tol * pricer.Notional);

      // Default on fee settle should have pv = (fee + recoveryRate - 1)*notional
      pricer.SurvivalCurve = (SurvivalCurve)pricer.SurvivalCurve.Clone();
      Dt defaultDate = feeSettle;
      pricer.SurvivalCurve.DefaultDate = defaultDate;
      pricer.Reset();
      AssertEqual(label + ": VOD on feeSettle",
        (pricer.CDS.Fee + pricer.RecoveryRate - 1) * pricer.Notional * df,
        pricer.Pv(), tol * pricer.Notional);

      if (days >= 2)
      {
        // Default between settle and fee settle should have pv = (fee + recoveryRate - 1)*notional
        pricer.SurvivalCurve = (SurvivalCurve)pricer.SurvivalCurve.Clone();
        defaultDate = Dt.Add(pricer.Settle, days / 2);
        df = pricer.DiscountCurve.DiscountFactor(pricer.AsOf, defaultDate);
        pricer.SurvivalCurve.DefaultDate = defaultDate;
        pricer.Reset();
        AssertEqual(label + ": VOD on feeSettle",
          (pricer.CDS.Fee + pricer.RecoveryRate - 1) * pricer.Notional * df,
          pricer.Pv(), tol * pricer.Notional);
      }

      // Default on settle should also have pv = (fee + recoveryRate - 1)*notional
      pricer.SurvivalCurve = (SurvivalCurve)pricer.SurvivalCurve.Clone();
      defaultDate = pricer.Settle;
      df = pricer.DiscountCurve.DiscountFactor(pricer.AsOf, defaultDate);
      pricer.SurvivalCurve.DefaultDate = defaultDate;
      pricer.Reset();
      AssertEqual(label + ": VOD on settle",
        (pricer.CDS.Fee + pricer.RecoveryRate - 1) * pricer.Notional * df,
        pricer.Pv(), tol * pricer.Notional);
    }

    private void TestMaturityOnSettle(string label)
    {
      const double tol = 1E-4;
      IPricer myPricer;
      ICDSPricer pricer = CreateCDSPricer();
      pricer.Settle = pricer.CDS.Calendar == Calendar.None ? pricer.CDS.FirstPrem
        : Dt.Roll(pricer.CDS.FirstPrem, pricer.CDS.BDConvention, pricer.CDS.Calendar);
      AssertEqual(label + " CDS settle on first premium, Accrued == 0",
        0.0, pricer.Accrued(), tol * pricer.Notional);
      myPricer = CreateCashflowPricer(pricer);
      AssertEqual(label + " settle on first premium, Accrued == 0",
        0.0, myPricer.Accrued(), tol * pricer.Notional);

      pricer.CDS.Maturity = pricer.Settle;
      pricer.Reset();
      AssertEqual(label + " CDS maturity on settle, Accrued == 0", 0.0, pricer.Accrued(), tol * pricer.Notional);
      myPricer = CreateCashflowPricer(pricer);
      AssertEqual(label + " maturity on settle, Accrued == 0", 0.0, myPricer.Accrued(), tol * pricer.Notional);

      pricer = CreateCDSPricer();
      pricer.Settle = Dt.Add(pricer.CDS.FirstPrem, 30);
      pricer.CDS.Maturity = Dt.CDSMaturity(pricer.Settle, "3Y");
      pricer.Reset();
      Assert.Greater(pricer.Accrued(), 0.0, label + " CDS settle after first premium: Accrued");
      myPricer = CreateCashflowPricer(pricer);
      Assert.Greater(myPricer.Accrued(), 0.0, label + " settle after first premium: Accrued");

      pricer.CDS.Maturity = pricer.Settle;
      pricer.Reset();
      AssertEqual(label + " CDS maturity on settle: Accrued", 0.0, pricer.Accrued(), tol * pricer.Notional);
      myPricer = CreateCashflowPricer(pricer);
      AssertEqual(label + " maturity on settle: Accrued", 0.0, myPricer.Accrued(), tol * pricer.Notional);
    }
    #endregion // Helpers

    #region Tests
    [Test, Smoke]
    public void MaturityOnSettle()
    {
      // Test the cashflow pricer
      if (!Settings.CashflowPricer.DiscountingAccrued)
        // Test the normal case
        TestMaturityOnSettle("Cashflow");
    }

    [Test, Smoke]
    public void FeeOnSettle()
    {
      // Test the old price function in cashflow model
      if (Settings.CashflowPricer.DiscountingAccrued)
      {
        // Test the normal case
        TestFeeOnSettle("Old, normal", null);
        // Test the case with zero discount rate
        TestFeeOnSettle("Old, zero IR", new DiscountCurve(asOf_, 1E-10));
      }
      else
      {
        // Test the new price function in cashflow model
        // Test the normal case
        TestFeeOnSettle("New, normal", null);
        // Test the case with zero discount rate
        TestFeeOnSettle("New, zero IR", new DiscountCurve(asOf_, 1E-10));
      }

      return;
    }

    [Test, Smoke]
    public void FeeOnSettlePlusOne()
    {
      if (Settings.CashflowPricer.DiscountingAccrued)
      {
        // Test the old price function in cashflow model
        // Test the normal case
        TestFeeAfterSettle("Old, normal", null, 1);
        // Test the case with zero discount rate
        TestFeeAfterSettle("Old, zero IR", new DiscountCurve(asOf_, 1E-10), 1);
      }
      else
      {
        // Test the new price function in cashflow model
        // Test the normal case
        TestFeeAfterSettle("New, normal", null, 1);
        // Test the case with zero discount rate
        TestFeeAfterSettle("New, zero IR", new DiscountCurve(asOf_, 1E-10), 1);
      }

      return;
    }

    [Test, Smoke]
    public void FeeOnSettlePlusTwo()
    {
      if (Settings.CashflowPricer.DiscountingAccrued)
      {
        // Test the old price function in cashflow model
        // Test the normal case
        TestFeeAfterSettle("Old, normal", null, 2);
        // Test the case with zero discount rate
        TestFeeAfterSettle("Old, zero IR", new DiscountCurve(asOf_, 1E-10), 2);
      }
      else
      {
        // Test the new price function in cashflow model
        // Test the normal case
        TestFeeAfterSettle("New, normal", null, 2);
        // Test the case with zero discount rate
        TestFeeAfterSettle("New, zero IR", new DiscountCurve(asOf_, 1E-10), 2);
      }

      return;
    }

    [Test, Smoke]
    public void FeeOnSettlePlusThree()
    {
      bool discountingAccrued = Settings.CashflowPricer.DiscountingAccrued;
      if (discountingAccrued)
      {
        // Test the old price function in cashflow model
        // Test the normal case
        TestFeeAfterSettle("Old, normal", null, 3);
        // Test the case with zero discount rate
        TestFeeAfterSettle("Old, zero IR", new DiscountCurve(asOf_, 1E-10), 3);
      }
      else
      {
        // Test the new price function in cashflow model
        // Test the normal case
        TestFeeAfterSettle("New, normal", null, 3);
        // Test the case with zero discount rate
        TestFeeAfterSettle("New, zero IR", new DiscountCurve(asOf_, 1E-10), 3);
      }
      return;
    }

    #endregion // Tests
  }

 /* [TestFixture]
  public class TestCashflowPricerPs
  {
    [Test]
    public void TestCdsPaymentScheduleMethod()
    {
      const double expect = -47222.222222222226;
      var filePath = String.Format(@"toolkit\test\data\CashflowPricers\CdsPricer0210a.xml");
      var fullFilePath = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = XmlSerialization.ReadXmlFile(fullFilePath) as CDSCashflowPricer;
      if (pricer != null)
      {
        var accrued = pricer.Accrued();
        NUnit.Framework.Assert.AreEqual(expect, accrued, 1E-10);
      }
    }

    [Test]
    public void TestRecoveryLockPsMethod()
    {
      const double expect = 29759.170682800734;
      var filePath = String.Format(@"toolkit\test\data\CashflowPricers\RecoveryLockPricer1807.xml");
      var fullFilePath = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = XmlSerialization.ReadXmlFile(fullFilePath) as RecoverySwapPricer;
      if (pricer != null)
      {
        var pv = pricer.Pv();
        NUnit.Framework.Assert.AreEqual(expect, pv, 1E-14 * Math.Abs(pricer.Notional));
      }
    }

    [Test]
    public void TestRecoveryLockVODMethod()
    {
      const double expect = -2088.1417968265682;
      var filePath = String.Format(@"toolkit\test\data\CashflowPricers\RecoveryLockPricer1800.xml");
      var fullFilePath = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = XmlSerialization.ReadXmlFile(fullFilePath) as RecoverySwapPricer;
      if (pricer != null)
      {
        var vod = Sensitivities.VOD(pricer, "Pv");
        NUnit.Framework.Assert.AreEqual(expect, vod, 5E-11);
      }
    }

    [Test]
    public void TestCdsPsMethod()
    {
      const double expect = -1599.3055555555557;
      var filePath = String.Format(@"toolkit\test\data\CashflowPricers\BasketCdsPricer2923.xml");
      var fullFilePath = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = XmlSerialization.ReadXmlFile(fullFilePath) as BasketCDSPricer;
      if (pricer != null)
      {
        var pv = pricer.Accrued();
        NUnit.Framework.Assert.AreEqual(expect, pv, 1E-14);
      }
    }
  }*/


}
