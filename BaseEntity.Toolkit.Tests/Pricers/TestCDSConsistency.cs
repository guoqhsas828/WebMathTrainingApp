//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Data;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{

  /// <summary>
  /// Compare CDS pricing models with defaulted curves
  /// </summary>
  [TestFixture, Smoke]
  public class TestCDSConsistency : ToolkitTestBase
  {

    #region Data
    private new readonly ToolkitConfigSettings settings_ = ToolkitConfigurator.Settings;

    static Dt effective_ = new Dt(20050926);
    static Dt maturity_ = new Dt(20101220);
    static Dt asOf_ = new Dt(20050926);
    static Dt settle_ = new Dt(20050927);
    const double notional_ = 5000000;

    const double recoveryRate_ = 0.4;

    #endregion Data

    #region Utilities

    /// <summary>
    ///   Create a CDS product
    /// </summary>
    private static CDS CreateCDS()
    {
      return new CDS(
        effective_, maturity_, 
        Currency.USD, 182.0 / 10000, 
        DayCount.Actual360, Frequency.Quarterly, 
        BDConvention.Following, Calendar.NYB);
    }

    /// <summary>
    ///   Create a IR Curve
    /// </summary>
    private static DiscountCurve CreateIRCurve()
    {
      string[] mmTenors = new string[]{"6 Month","1 Year"};
      double[] mmRates = new double[]{0.0369, 0.0386};
      Dt[] mmMaturities = new Dt[mmTenors.Length];
      for (int i = 0; i < mmTenors.Length; i++)
        mmMaturities[i] = Dt.Add(asOf_, mmTenors[i]);
      DayCount mmDayCount = DayCount.Actual360;

      string[] swapTenors = new string[]{"2 Year","3 Year","5 Year","7 Year","10 Year"};
      double[] swapRates = new double[]{0.0399,0.0407,0.0417,0.0426,0.044};
      Dt[] swapMaturities = new Dt[swapTenors.Length];
      for (int i = 0; i < swapTenors.Length; i++)
        swapMaturities[i] = Dt.Add(asOf_, swapTenors[i]);
      DayCount swapDayCount = DayCount.Thirty360;

      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf_, asOf_);
      calibrator.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      curve.Name = "USD_LIBOR";

      // Add MM rates
      for (int i = 0; i < mmTenors.Length; i++)
        if (mmRates[i] > 0.0)
          curve.AddMoneyMarket(mmTenors[i], mmMaturities[i], mmRates[i], mmDayCount);

      // Add swap rates
      for (int i = 0; i < swapTenors.Length; i++)
        if (swapRates[i] > 0.0)
          curve.AddSwap(swapTenors[i], swapMaturities[i], swapRates[i], swapDayCount,
                         Frequency.SemiAnnual, BDConvention.None, Calendar.None);

      curve.Fit();

      return curve;
    }

    /// <summary>
    ///  Create a CDS curve
    /// </summary>
    private static SurvivalCurve CreateCDSCurve(DiscountCurve discountCurve)
    {
      double[] premiums = new double[]{65.00,112.00,130.00,163.00,182.00,194.00,206.00};

      string[] tenorNames = new string[]{
        "6 Month","1 Year","2 Year","3 Year","5 Year","7 Year","10 Year"};
      Dt[] tenorDates = new Dt[tenorNames.Length];
      for (int i = 0; i < tenorNames.Length; i++)
        tenorDates[i] = Dt.CDSMaturity(asOf_, tenorNames[i]);
      RecoveryCurve recoveryCurve = new RecoveryCurve(asOf_, recoveryRate_);
      SurvivalFitCalibrator calibrator =
        new SurvivalFitCalibrator( asOf_, asOf_, recoveryCurve, discountCurve );
      calibrator.NegSPTreatment = NegSPTreatment.Allow;
      calibrator.ForceFit = false;

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      curve.Name = "CDS_CURVE";
      for( int i = 0; i < tenorDates.Length; i++ )
        if( premiums[i] > 0.0 )
          curve.AddCDS( tenorNames[i],
                        tenorDates[i], 0.0, 
                        premiums[i]/10000.0,
                        DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      curve.ReFit(0);

      return curve;
    }

    /// <summary>
    ///   Create a CDS Pricer
    /// </summary>
    private static CDSCashflowPricer CreateCDSPricer(
      CDS cds, DiscountCurve discountCurve, SurvivalCurve survivalCurve)
    {
      return CreateCDSPricer(asOf_, settle_, cds, discountCurve, survivalCurve);
    }

    /// <summary>
    ///   Create a CDS Pricer
    /// </summary>
    private static CDSCashflowPricer CreateCDSPricer(
      Dt asOf, Dt settle, CDS cds, DiscountCurve discountCurve, SurvivalCurve survivalCurve )
    {
      CDSCashflowPricer pricer = new CDSCashflowPricer(
        cds, asOf, settle, discountCurve, survivalCurve, 0, TimeUnit.None);
      pricer.Notional = notional_;
      return pricer;
    }

    #endregion Utilities

    #region Tests

    /// <summary>
    ///   Test consistency among FeePv, Accrued, RiskyDuration, etc...
    /// </summary>
    [Test, Smoke]
    public void CashflowPricerConsistency()
    {
      const double tolerance = 1.0E-6;

      Dt asOf = Dt.Add(effective_, 10);
      Dt settle = Dt.Add(asOf, 10);

      CDS cds = CreateCDS();
      cds.Premium = 0.02;
      CDS cdsNoAccrued = new CDS(settle, cds.Maturity, cds.Ccy, cds.FirstPrem, cds.Premium,
        cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar, cds.Fee, cds.FeeSettle);

      DiscountCurve discountCurve = CreateIRCurve();
      SurvivalCurve survivalCurve = CreateCDSCurve(discountCurve);

      // Test CashflowPricer
      CDSCashflowPricer pricer = CreateCDSPricer(asOf, settle, cds, discountCurve, survivalCurve);
      CDSCashflowPricer pricerNoAccrued = CreateCDSPricer(asOf, settle, cdsNoAccrued, discountCurve, survivalCurve);

      // Test accrued: (1) zero accrued to cdsNoAccrued;
      double result = pricerNoAccrued.Accrued();
      double expect = 0;
      AssertEqual("Accrued = 0", expect, result, tolerance * Math.Abs(expect));

      // Test accrued: (2) FeePv - FlatFeePv = Accrued;
      result = pricer.FeePv() - pricer.FlatFeePv();
      expect = pricer.Accrued();
      AssertEqual("FeePv - FlatFeePv = Accrued", expect, result, tolerance * Math.Abs(expect));

      // Test risky duration: FeePv = RiskyDuration * premium * notional
      result = pricerNoAccrued.FeePv();
      expect = pricer.RiskyDuration() * cds.Premium * pricer.Notional;
      AssertEqual("FeePv = RiskyDuration * premium * notional",
        expect, result, tolerance);

      // Some variables
      double DF0 = discountCurve.DiscountFactor(asOf, settle);
      double DF1 = discountCurve.DiscountFactor(asOf, cds.FirstPrem);
      double DP1 = survivalCurve.DefaultProb(settle, cds.FirstPrem);

      bool discountingAccrued = settings_.CashflowPricer.DiscountingAccrued;
      if (discountingAccrued)
      {
        // Test Risky Duration - old interface:
        //    FeePv - Accrued * adjust = RiskyDuration * premium * notional
        pricer.Reset();
        AssertEqual(
          "FeePv - Accrued * adjust = RiskyDuration * premium * notional",
          expect,
          pricer.FeePv() - pricer.Accrued() * (DF1 - DP1 * (DF0 - DF1) / 2),
          tolerance * Math.Abs(expect));
      }
      else
      {
        // Test Risky Duration - new interface:
        //    FeePv - Accrued = RiskyDuration * premium * notional
        pricer.Reset();
        AssertEqual(
          "FeePv - Accrued = RiskyDuration * premium * notional",
          expect,
          pricer.FeePv() - pricer.Accrued(),
          tolerance * Math.Abs(expect));
      }

      // Test Pv: Pv = ProtectionPv + FeePv
      result = pricer.Pv();
      expect = pricer.ProtectionPv() + pricer.FeePv();
      AssertEqual("Pv = ProtectionPv + FeePv", expect, result, tolerance / 1000);

      // Test FullPrice: Pv = FullPrice * notional
      expect = pricer.FullModelPrice() * pricer.Notional;
      AssertEqual("Pv = FullPrice * notional", expect, result, tolerance / 1000);

      // Test FlatPrice: ProtectionPv + FlatFeePv = FlatPrice * notional
      result = pricer.ProtectionPv() + pricer.FlatFeePv();
      expect = pricer.FlatPrice() * pricer.Notional;
      AssertEqual("ProtectionPv + FlatFeePv = FlatPrice * notional", expect, result, tolerance / 1000);

      if (discountingAccrued)
      {
        // Test Premium01 - Old interface:
        //   Premium01 * Discount(asOf,settle) = FullFeePv(0.0001)
        pricer.Reset();
        result = pricer.Premium01() * DF0;
        pricer.CDS.Premium = 0.0001;
        pricer.Reset();
        expect = pricer.FullFeePv();
        AssertEqual("Premium01 * Discount(asOf,settle) = FullFeePv(0.0001)", expect, result, tolerance / 1000);
      }
      else
      {
        // Test Premium01 - new interface:
        //   Premium01 * Discount(asOf,settle) + Accrued * (1 - Discount(asOf,settle) = FullFeePv(0.0001)
        pricer.CDS.Premium = 0.0001;
        pricer.Reset();
        result = pricer.Premium01() * DF0 + pricer.Accrued() * (1 - DF0);
        pricer.CDS.Premium = 0.0001;
        pricer.Reset();
        expect = pricer.FullFeePv();
        AssertEqual("Premium01 * DF0 + Accrued * (1 - Df0) = FullFeePv(0.0001)", expect, result, tolerance / 1000);
      }
      return;
    }

    /// <summary>
    ///   Test consistency for amortizing CDS
    /// </summary>
    [Test, Smoke]
    [Ignore("Need to update support for amortizing CDS")]
    public void AmortizationConsistency()
    {
      const double tolerance = 1.0E-6;

      Dt asOf = Dt.Add(effective_, 10);
      Dt settle = Dt.Add(asOf, 10);

      CDS cds = CreateCDS();
      cds.AmortizationSchedule.Add(new Amortization(effective_, AmortizationType.PercentOfInitialNotional, 0.5));
      CDS cdsNoAmort = CreateCDS();

      DiscountCurve discountCurve = CreateIRCurve();
      SurvivalCurve survivalCurve = CreateCDSCurve(discountCurve);

      // Test CashflowPricer
      CDSCashflowPricer pricer = CreateCDSPricer(asOf, settle, cds, discountCurve, survivalCurve);
      CDSCashflowPricer pricerNoAmort = CreateCDSPricer(asOf, settle, cdsNoAmort, discountCurve, survivalCurve);

      // Test accrued
      double result = pricerNoAmort.Accrued();
      double expect = pricer.Accrued() * 0.5;
      AssertEqual("Accrued", expect, result, tolerance * Math.Abs(expect));

      // Test Pv
      result = pricerNoAmort.Pv();
      expect = pricer.Pv() * 0.5;
      AssertEqual("Pv", expect, result, tolerance * Math.Abs(expect));

      // Test risky duration
      result = pricerNoAmort.RiskyDuration();
      expect = pricer.RiskyDuration();
      AssertEqual("RiskyDuration", expect, result, tolerance * Math.Abs(expect));

      // Test break even premium
      result = pricerNoAmort.BreakEvenPremium();
      expect = pricer.BreakEvenPremium();
      AssertEqual("Accrued", expect, result, tolerance * Math.Abs(expect));

      // Test FullPrice
      result = pricerNoAmort.FullModelPrice();
      expect = pricer.FullModelPrice();
      AssertEqual("FullPrice", expect, result, tolerance * Math.Abs(expect));

      // Test FlatPrice
      result = pricerNoAmort.FlatPrice();
      expect = pricer.FlatPrice();
      AssertEqual("FlatPrice", expect, result, tolerance * Math.Abs(expect));

      // Test premium01
      result = pricerNoAmort.Premium01();
      expect = pricer.Premium01();
      AssertEqual("Accrued", expect, result, tolerance * Math.Abs(expect));

      return;
    }

    /// <summary>
    ///    Test CDS protection pv with credit defaulted on settle date
    /// </summary>
    [Test, Smoke]
    public void DefaultedOnSettle()
    {
      CDS cds = CreateCDS();
      DiscountCurve discountCurve = CreateIRCurve();
      SurvivalCurve survivalCurve = CreateCDSCurve(discountCurve);
      SurvivalCurve defaultedCurve = (SurvivalCurve) survivalCurve.Clone();
      defaultedCurve.DefaultDate = settle_;

      // Test CashflowPricer
      double result, expect;
      var normalPricer = CreateCDSPricer(cds, discountCurve, survivalCurve);
      var defaultedPricer = CreateCDSPricer(cds, discountCurve, defaultedCurve);

      // Test Protection Pv = DiscountedLosses
      result = defaultedPricer.ProtectionPv();
      expect = notional_*(recoveryRate_ - 1)*
        discountCurve.DiscountFactor(asOf_, settle_);
      AssertEqual("ProtectionPv", expect, result, 1.0E-9);

      // Test Fee Pv = Accrued + OneDayCarry
      result = defaultedPricer.FeePv();
      var accrued = ToolkitConfigurator.Settings.CashflowPricer.IncludeAccruedOnDefaultAtSettle
          ? (normalPricer.Accrued() + normalPricer.Carry()) : 0;
      expect = accrued;
      AssertEqual("FeePv", expect, result, 1.0E-9);

      // Test Pv
      result = defaultedPricer.Pv();
      expect = notional_*(recoveryRate_ - 1)*discountCurve.DiscountFactor(asOf_, settle_)
          + accrued;
      AssertEqual("Pv", expect, result, 1.0E-9);

      // Test JTD
      double basePv = normalPricer.Pv();
      expect -= basePv;

      DataTable dt = Sensitivities.Curve(normalPricer, null,
        new [] {survivalCurve}, new [] {defaultedCurve},
        BumpType.Uniform, false, "none", null);
      result = (double) (dt.Rows[0])["Delta"];
      AssertEqual("JTD", expect, result, 1.0E-9);
    }

    /// <summary>
    ///    Test CDS protection pv with credit defaulted before settle date
    ///    and various default settlement scenarios.
    /// </summary>
    [Test, Smoke]
    public void DefaultedBeforeSettle()
    {
      CDS cds = CreateCDS();
      cds = new CDS(Dt.SNACFirstAccrualStart(Dt.Add(cds.Effective, -200), cds.Calendar),
        cds.Maturity, cds.Ccy, Dt.Empty, cds.Premium,
        cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar, cds.Fee, Dt.Empty);
      DiscountCurve discountCurve = CreateIRCurve();
      SurvivalCurve survivalCurve = CreateCDSCurve(discountCurve);
      SurvivalCurve defaultedCurve = (SurvivalCurve)survivalCurve.Clone();
      defaultedCurve.DefaultDate = Dt.Add(settle_, -10);

      // Test the case defaulted and no settle info
      {
        // Test absolution value
        CDSCashflowPricer pricer = CreateCDSPricer(cds, discountCurve, defaultedCurve);
        double result = pricer.Pv();
        AssertEqual("NoDefaultSettleInfo.Pv",
          0.0, result, 1.0E-9);
        AssertEqual("NoDefaultSettleInfo.CurrentNotional",
          0.0, pricer.CurrentNotional, 1.0E-9);
        AssertEqual("NoDefaultSettleInfo.EffectiveNotional",
          0.0, pricer.EffectiveNotional, 1.0E-9);
      }

      // Test the case defaulted and settled
      {
        // Test absolution value
        defaultedCurve.SurvivalCalibrator.RecoveryCurve.JumpDate = Dt.Add(settle_, -5);
        CDSCashflowPricer pricer = CreateCDSPricer(cds, discountCurve, defaultedCurve);
        double result = pricer.Pv();
        AssertEqual("DefaultSettled.Pv",
          0.0, result, 1.0E-9);
        AssertEqual("DefaultSettled.CurrentNotional",
          0.0, pricer.CurrentNotional, 1.0E-9);
        AssertEqual("DefaultSettled.EffectiveNotional",
          0.0, pricer.EffectiveNotional, 1.0E-9);
      }

      // Test the case defaulted and not settled
      {
        // Test absolution value
        Dt dfltSettleDate = Dt.Add(settle_, 2);
        RecoveryCurve rc = defaultedCurve.SurvivalCalibrator.RecoveryCurve;
        rc.JumpDate= dfltSettleDate;
        CDSCashflowPricer pricer = CreateCDSPricer(cds, discountCurve, defaultedCurve);
        double result = pricer.ProtectionPv();
        double expect = -pricer.DiscountCurve.DiscountFactor(pricer.AsOf, dfltSettleDate)
          * (1 - rc.Interpolate(dfltSettleDate)) * pricer.Notional;
        AssertEqual("DefaultNotSettled.ProtectionPv",
          expect, result, 1.0E-9);
        AssertEqual("DefaultNotSettled.FlatFeePv",
          0.0, pricer.FlatFeePv(), 1.0E-9);
        AssertEqual("DefaultNotSettled.CurrentNotional",
          0.0, pricer.CurrentNotional, 1.0E-9);
        AssertEqual("DefaultNotSettled.EffectiveNotional",
          pricer.Notional, pricer.EffectiveNotional, 1.0E-9);
      }

      return;
    }

    /// <summary>
    ///    Test CDS protection pv with credit defaulted after settle date
    /// </summary>
    [Test, Smoke]
    public void DefaultedOnLaterDate()
    {
      CDS cds = CreateCDS();
      DiscountCurve discountCurve = CreateIRCurve();
      SurvivalCurve survivalCurve = CreateCDSCurve(discountCurve);
      SurvivalCurve defaultedCurve = (SurvivalCurve)survivalCurve.Clone();
      defaultedCurve.DefaultDate = new Dt(20061202);

      // Test CashflowPricer
      {
        CDSCashflowPricer pricer = CreateCDSPricer(cds, discountCurve, defaultedCurve);
        double result = pricer.Pv();
        double expect = result;

        // Test deltas
        pricer = CreateCDSPricer(cds, discountCurve, survivalCurve);
        double basePv = pricer.Pv();
        expect -= basePv;

        DataTable dt = Sensitivities.Curve( pricer, null,
            new SurvivalCurve[] { survivalCurve }, new SurvivalCurve[] { defaultedCurve },
            BumpType.Uniform, false, "none", null);
        result = (double)(dt.Rows[0])["Delta"];
        AssertEqual("CashflowPricer Delta", expect, result, 1.0E-9);
      }

    }

    [Test, Smoke]
    public void AccruedTreatments()
    {
      // effective on holiday, and settle on the next business day
      AccruedTreatments(new Dt(20080321), new Dt(20080324), 4);
      AccruedTreatments(new Dt(20080322), new Dt(20080324), 3);
      AccruedTreatments(new Dt(20080323), new Dt(20080324), 2);
      AccruedTreatments(new Dt(20080324), new Dt(20080324), 1);
      AccruedTreatments(new Dt(20080325), new Dt(20080324), 0);

      // forward start cds
      AccruedTreatments(new Dt(20080325), new Dt(20080324), 0);
      AccruedTreatments(new Dt(20080326), new Dt(20080324), 0);

      // the first payment date rolls
      Dt effective = new Dt(20080321);
      int days = Dt.Diff(new Dt(20080922), new Dt(20081220));
      AccruedTreatments(effective, new Dt(20081219), days);
      AccruedTreatments(effective, new Dt(20081220), days + 1);
      AccruedTreatments(effective, new Dt(20081221), 0);
      AccruedTreatments(effective, new Dt(20081222), 1);
    }

    private static void AccruedTreatments(
      Dt effective, Dt asOf, int daysExpect)
    {
      Calendar calendar = Calendar.NYB;
      Dt settle = Dt.Add(asOf, 1);
      CDS cds = new CDS(effective, 5, 1, calendar);
      CDSCashflowPricer pricer = new CDSCashflowPricer(
        cds, asOf, settle, new DiscountCurve(asOf, 0.0),
        null, 0, TimeUnit.None);
      AssertEqual("AccrualDays ("
        + effective.ToInt() + " - " + settle.ToInt() + ')',
        daysExpect, pricer.AccrualDays());
      AssertEqual("Accrued ("
        + effective.ToInt() + " - " + settle.ToInt() + ')',
        daysExpect / 360.0, pricer.Accrued());
    }

    #endregion Tests

    #region Test_CDS_Cashflows

    /// <summary>
    ///    Test CDS Cashflows using CDS CashflowPricer
    /// </summary>
    [Test, Smoke]
    public void TestCDSCashflows()
    {
      CDS cds = CreateCDS();
      DiscountCurve discountCurve = CreateIRCurve();
      SurvivalCurve survivalCurve = CreateCDSCurve(discountCurve);

      //
      // Test expected cashflow structure
      //
      Timer timer = new Timer();
      timer.Start();

      CDSCashflowPricer pricer = CreateCDSPricer(cds, discountCurve, survivalCurve);
      //CDSCashflowStreamPricer pricer = CreateCDSStreamPricer(cds, discountCurve, null);
      DataTable dataTable = CDSCashflows(pricer, pricer.Settle);

      timer.Stop();
      ResultData rd = ToResultData(dataTable, timer.Elapsed);
      MatchExpects(rd);
    }

    /// <summary>
    ///   Return the contingent cashflows for a CDS 
    /// </summary>
    ///
    /// <param name="pricer">CDS Cashflow(Stream) Pricer</param>
    /// <param name="from">Date to get cashflows on or after. Default is settlement date</param>
    ///
    /// <returns>Contingent cashflows of the CDS on or after the settlement date or specified
    /// from date.</returns>
    ///
    protected DataTable
    CDSCashflows(
      CDSCashflowPricer pricer,
      Dt from
      )
    {
      
      if (from.IsEmpty())
        from = pricer.Settle;
      Dt settle = pricer.Settle;

      DiscountCurve dc = pricer.DiscountCurve;
      SurvivalCurve sc = pricer.SurvivalCurve;
      Cashflow cf = pricer.GenerateCashflow(null, from);
      

      //- Create the payments schedule. (this replicates schedule used in CashflowFactory.cpp)
      Schedule psched;
      CDS cds = pricer.CDS;
      if (cds.Effective == cds.Maturity)
      {
        // If effective and maturity are the same day, we construct a schedule
        // with a single period such that:
        //   periodStart = periodEnd = paymentDate = effective
        //
        psched = new Schedule(pricer.AsOf, cds.Effective, cds.Effective, cds.Maturity,
          new Dt[1] { cds.Effective } , new Dt[1] { cds.Effective });
      }
      else
      {
        psched = new Schedule(from, cds.Effective, cds.FirstPrem, cds.Maturity,
                   cds.Freq, cds.BDConvention, cds.Calendar);
      }

      DataTable dataTable = new DataTable("Cashflow table");
      dataTable.Columns.Add(new DataColumn("Accrual Start", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Accrual End", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Payment Date", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Accrual", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Loss", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Amount", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Discount Factor", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Survival Prob", typeof(double)));

      int lastIdx = cf.Count - 1;
      for (int i = 0; i <= psched.Count - 1; i++)
      {
        DataRow row = dataTable.NewRow();
        double accrual = cf.GetAccrued(i);
        Dt pStart = psched.GetPeriodStart(i);
        Dt pEnd = psched.GetPeriodEnd(i);
        row["Accrual Start"] = pStart;

        if (i == lastIdx && settings_.CDSCashflowPricer.IncludeMaturityAccrual)
          row["Accrual End"] = pEnd;
        else
          row["Accrual End"] = Dt.Add(pEnd, -1);

        row["Payment Date"] = psched.GetPaymentDate(i);
        row["Accrual"] = accrual;
        row["Loss"] = cf.GetDefaultAmount(i);
        row["Amount"] = pricer.Notional * (cf.GetAmount(i) + accrual);
        row["Discount Factor"] = dc.DiscountFactor(psched.GetPaymentDate(i));

        if (sc == null)
          row["Survival Prob"] = 1.0;
        else if (i == lastIdx && settings_.CDSCashflowPricer.IncludeMaturityProtection)
          row["Survival Prob"] = sc.SurvivalProb(Dt.Add(psched.GetPaymentDate(i), 1));
        else
          row["Survival Prob"] = sc.SurvivalProb(psched.GetPaymentDate(i));

        dataTable.Rows.Add(row);
      }
      return dataTable;
    }

    /// <summary>
    ///   Convert CDS cashflows table to a result data object
    /// </summary>
    /// <param name="table">Data table</param>
    /// <param name="timeUsed">Time used to complete the tests</param>
    /// <returns>ResultData</returns>
    protected ResultData ToResultData( System.Data.DataTable table, double timeUsed )
    {
      // Total
      int rows = table.Rows.Count;
      int cols = table.Columns.Count;

      // string[] labels = null;
      double[] accrualStartDate = new double[rows];
      double[] accrualEndDate = new double[rows];
      double[] paymentDate = new double[rows];
      double[] accrual = new double[rows];
      double[] loss = new double[rows];
      double[] amount = new double[rows];
      double[] principals = new double[rows];
      double[] discountFactors = new double[rows];
      double[] survivalProbs = new double[rows];

      for (int i = 0; i < rows; i++)
      {
        System.Data.DataRow row = table.Rows[i];

        //labels[i] = (string)row["Element"];
        accrualStartDate[i] = (double)((Dt)row["Accrual Start"]).ToInt();
        accrualEndDate[i] = (double)((Dt)row["Accrual End"]).ToInt();
        paymentDate[i] = (double)((Dt)row["Payment Date"]).ToInt();
        accrual[i] = (double)row["Accrual"];
        loss[i] = (double)row["Loss"];
        amount[i] = (double)row["Amount"];
        discountFactors[i] = (double)row["Discount Factor"];
        survivalProbs[i] = (double)row["Survival Prob"];
      }

      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }

      int idx = 0;
      rd.Results[idx].Name = "Accrual Start";
      rd.Results[idx].Actuals = accrualStartDate;
      idx++;

      rd.Results[idx].Name = "Accrual End";
      rd.Results[idx].Actuals = accrualEndDate;
      idx++;

      rd.Results[idx].Name = "Payment Date";
      rd.Results[idx].Actuals = paymentDate;
      idx++;

      rd.Results[idx].Name = "Accrual";
      //rd.Results[idx].Labels = labels;
      rd.Results[idx].Actuals = accrual;
      idx++;

      rd.Results[idx].Name = "Loss";
      //rd.Results[idx].Labels = labels;
      rd.Results[idx].Actuals = loss;
      idx++;

      rd.Results[idx].Name = "Amount";
      //rd.Results[idx].Labels = labels;
      rd.Results[idx].Actuals = amount;
      idx++;

      rd.Results[idx].Name = "Discount Factor";
      //rd.Results[idx].Labels = labels;
      rd.Results[idx].Actuals = discountFactors;
      idx++;

      rd.Results[idx].Name = "Survival Prob";
      //rd.Results[idx].Labels = labels;
      rd.Results[idx].Actuals = survivalProbs;

      rd.TimeUsed = timeUsed;
      return rd;
    }

    #endregion // Test_CDS_Cashflows
  }
}
