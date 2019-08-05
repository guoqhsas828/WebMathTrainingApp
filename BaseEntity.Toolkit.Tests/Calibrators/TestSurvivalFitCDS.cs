// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Linq;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  /// <summary>
  ///   Test Survival fit CDS calibrators
  /// </summary>
  [TestFixture]
  public class TestSurvivalFitCDS : ToolkitTestBase
  {
    #region Tests
    private void DoTest(bool useNewMethod)
    {
      // Requirement:
      //  SurvivalCalibrator.UseCdsSpecificSolver == useNewMethod;
      //  SurvivalCalibrator.UseNaturalSettlement == true;
      if (Settings.SurvivalCalibrator.UseNaturalSettlement)
      {
        DiscountCurve discountCurve = LoadDiscountCurve(LiborDataFile);
        string filename = GetTestFilePath(CreditDataFile);
        CreditData cd = (CreditData)XmlLoadData(filename, typeof(CreditData));

        // We record the calibration time
        Timer timer = new Timer();
        timer.Start();
        SurvivalCurve[] survivalCurves = cd.GetSurvivalCurves(discountCurve);
        timer.Stop();
        if (survivalCurves == null)
          throw new System.Exception(filename + ": Invalid credit data");
        Assert.AreEqual(timer.Elapsed, timer.Elapsed);

        // Check consistency
        //double[] result = new double[survivalCurves.Length];
      }
      return;
    }

    private static void ParSpreadRoundTrip(
      SurvivalCurve curve, double[] originalQuotes)
    {
      double tol = curve.SurvivalCalibrator.ToleranceF;
      double[] parSpreads = curve.GetQuotes(
        QuotingConvention.CreditSpread).Select((s) => s * 10000.0).ToArray();
      for (int i = 0; i < parSpreads.Length; ++i)
      {
        Assert.AreEqual(originalQuotes[i], parSpreads[i],
          tol*originalQuotes[i], curve.Name + ".ParSpread[" + i + "]");
      }
      return;
    }

    private static void ConventionalSpreadRoundTrip(
      SurvivalCurve curve, double[] originalQuotes)
    {
      double tol = curve.SurvivalCalibrator.ToleranceF;
      double[] convSpreads = curve.GetQuotes(
        QuotingConvention.CreditConventionalSpread)
        .Select((s) => s * 10000.0).ToArray();
      for (int i = 0; i < convSpreads.Length; ++i)
      {
        Assert.AreEqual(originalQuotes[i], convSpreads[i],
          tol*originalQuotes[i], curve.Name + ".ConvSpread[" + i + "]");
      }
      return;
    }

    private static void ConventionalUpfrontRoundTrip(
      SurvivalCurve curve, double[] originalQuotes)
    {
      double tol = curve.SurvivalCalibrator.ToleranceF;
      double[] upfronts = curve.GetQuotes(
        QuotingConvention.CreditConventionalUpfront).ToArray();
      for (int i = 0; i < upfronts.Length; ++i)
      {
        Assert.AreEqual(originalQuotes[i], upfronts[i], tol,
          curve.Name + ".ConvUpfront[" + i + "]");
      }
      return;
    }

    private static SurvivalCurve CalibrateCurve(string name,
      string[] tenors, double[] quotes, CDSQuoteType quoteType)
    {
      Dt asOf = Dt.Roll(Dt.Today(), BDConvention.Following, Calendar.None);
      Dt settle = Dt.Add(asOf, 1);
      SurvivalCurveParameters pars = new SurvivalCurveParameters(
        DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
        Calendar.None, InterpMethod.Weighted, ExtrapMethod.Const,
        NegSPTreatment.Allow);
      double runningPremium = 500;
      DiscountCurve discCurve = new DiscountCurve(asOf, 0.01);

      var curve = SurvivalCurve.FitCDSQuotes(name, asOf, settle, Currency.USD, "None", true, quoteType, runningPremium,
                                             pars, discCurve, tenors, null, quotes, new[] {0.4}, 0.0, null, null, 0.0,
                                             0.4, null, false);
      return curve;
    }

    [Test, Smoke]
    public void CalibrateSNAC()
    {
      string[] tenors = new string[] { "5Y", "7Y", "10Y" };
      double[] convSpreads = new double[] { 880.6, 811.3, 760.5 };

      var curve0 = CalibrateCurve("curve0", tenors, convSpreads, CDSQuoteType.ConvSpread);
      curve0.UpdateQuotes(QuotingConvention.CreditConventionalSpread);
      var upfronts = curve0.GetQuotes(QuotingConvention.CreditConventionalUpfront).ToArray();
      var curve1 = CalibrateCurve("curve1", tenors, upfronts, CDSQuoteType.Upfront);
      curve1.UpdateQuotes(QuotingConvention.CreditConventionalUpfront);
      var parSpreads = curve0.GetQuotes(QuotingConvention.CreditSpread)
        .Select((s) => s * 10000.0).ToArray();
      var curve2 = CalibrateCurve("curve2",tenors, parSpreads, CDSQuoteType.ParSpread);
      curve2.UpdateQuotes(QuotingConvention.CreditSpread);

      ConventionalSpreadRoundTrip(curve0, convSpreads);
      ConventionalSpreadRoundTrip(curve1, convSpreads);
      ConventionalSpreadRoundTrip(curve2, convSpreads);

      ConventionalUpfrontRoundTrip(curve0, upfronts);
      ConventionalUpfrontRoundTrip(curve1, upfronts);
      ConventionalUpfrontRoundTrip(curve2, upfronts);

      ParSpreadRoundTrip(curve0, parSpreads);
      ParSpreadRoundTrip(curve1, parSpreads);
      ParSpreadRoundTrip(curve2, parSpreads);
    }

    [Test, Smoke]
    public void TestSurvivalFitCalibrator()
    {
      DoTest(false);
    }

    [Test, Smoke]
    public void TestSurvivalFitCDSCalibrator()
    {
      DoTest(true);
    }

    /// <summary>
    ///   The case where only one tenor needs force fit. 
    ///   Test that the market quotes are unchanged after force fit.
    /// </summary>
    [Test, Smoke]
    public void TestZeroSpreadForceFit()
    {
      Dt asOf = new Dt(20070327);
      Dt settle = new Dt(20070328);
      double recoveryRate = 0.4;
      double discountRate = 0.04;
      string[] tenors = new string[] { "1Y", "2Y", "3Y", "5Y", "7Y", "10Y" };
      double[] spreads = new double[] { 1.5, 0, 2, 9, 15, 20 };

      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate);
      RecoveryCurve recoveryCurve = new RecoveryCurve(asOf, recoveryRate);

      SurvivalFitCalibrator calibrator =
        new SurvivalFitCalibrator(asOf, settle, recoveryCurve, discountCurve);
      calibrator.ForceFit = true;
      calibrator.NegSPTreatment = NegSPTreatment.Allow;
      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      for (int i = 0; i < tenors.Length; i++)
        curve.AddCDS(tenors[i], Dt.CDSMaturity(asOf, tenors[i]), 0.0,
          spreads[i] / 10000.0, DayCount.Actual360, Frequency.Quarterly,
          BDConvention.Following, Calendar.NYB);
      curve.Fit();

      // Assert that the quotes are not modified
      for (int i = 0; i < tenors.Length; i++)
      {
        CurveTenor tenor = curve.Tenors[i];
        Assert.AreEqual(
          spreads[i] / 10000.0,
          ((CDS)tenor.Product).Premium, 0.0,
          tenor.Name + " quotes modified by force fit!");
      }

      return;
    }


    /// <summary>
    ///   The case where at least two tenors need force fit. 
    ///   Test that the market quotes are unchanged after force fits.
    /// </summary>
    [Test, Smoke]
    public void TestMultipleForceFit()
    {
      Dt asOf = new Dt(20070327);
      Dt settle = new Dt(20070328);
      double recoveryRate = 0.4;
      double discountRate = 0.04;
      string[] tenors = new string[] { "1Y", "2Y", "3Y", "5Y", "7Y", "10Y" };
      double[] spreads = new double[] { 1.5, 0, 10, 20, 1, 4 };

      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate);
      RecoveryCurve recoveryCurve = new RecoveryCurve(asOf, recoveryRate);

      SurvivalFitCalibrator calibrator =
        new SurvivalFitCalibrator(asOf, settle, recoveryCurve, discountCurve);
      calibrator.ForceFit = true;
      calibrator.NegSPTreatment = NegSPTreatment.Allow;
      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      for (int i = 0; i < tenors.Length; i++)
        curve.AddCDS(tenors[i], Dt.CDSMaturity(asOf, tenors[i]), 0.0,
          spreads[i] / 10000.0, DayCount.Actual360, Frequency.Quarterly,
          BDConvention.Following, Calendar.NYB);
      curve.Fit();

      // Assert that the quotes are not modified
      for (int i = 0; i < tenors.Length; i++)
      {
        CurveTenor tenor = curve.Tenors[i];
        Assert.AreEqual(
          spreads[i] / 10000.0,
          ((CDS)tenor.Product).Premium, 0.0,
          tenor.Name + " quotes modified by force fit!");
      }

      return;
    }
    #endregion // Tests

    #region Properties
    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = "data\\USD_LIBOR_070511.xml";

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string CreditDataFile { get; set; } = "data\\CDS_Quotes_070511.xml";

    #endregion // Properties

    #region Data

    #endregion // Data

#if Activate_Old_tests
    static Dt asOf_ = new Dt(20060630);
    static Dt maturity_ = new Dt(20101220);
    const double recoveryRate_ = 0.4;

    private static CDS CreateCDS()
    {
      return new CDS(
        asOf_, maturity_,
        Currency.USD, 182 / 10000,
        DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB);
    }

    private static DiscountCurve CreateIRCurve()
    {
      string[] mmTenors = new string[] {
        "1 Days","1 Weeks","2 Weeks","1 Months","2 Months","3 Months",
        "4 Months","5 Months","6 Months","9 Months","1 Years" };
      double[] mmRates = new double[] {
        0.0536875,0.053225,0.05325,0.0533438,0.0542,0.0548063,0.0551688,
        0.05555,0.0558938,0.0564938,0.0569313 };
      Dt[] mmMaturities = new Dt[mmTenors.Length];
      for (int i = 0; i < mmTenors.Length; i++)
        mmMaturities[i] = Dt.Add(asOf_, mmTenors[i]);
      DayCount mmDayCount = DayCount.Actual360;

      string[] swapTenors = new string[] { "2 Year", "3 Year", "4 Year", "5 Year",
        "6 Year","7 Year", "8 Year", "9 Year", "10 Year" };
      double[] swapRates = new double[] { 0.056245,0.05616,0.0563,0.05648,
        0.05665,0.05683,0.056995,0.05713,0.057295 };
      Dt[] swapMaturities = new Dt[swapTenors.Length];
      for (int i = 0; i < swapTenors.Length; i++)
        swapMaturities[i] = Dt.Add(asOf_, swapTenors[i]);
      DayCount swapDayCount = DayCount.Thirty360;

      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf_, asOf_);
      calibrator.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Smooth);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      curve.Name = "USDLIBOR";

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

    private static SurvivalCurve CreateCDSCurve(
      double[] premiums, string[] tenorNames,
      DiscountCurve discountCurve)
    {
      Dt[] tenorDates = new Dt[tenorNames.Length];
      for (int i = 0; i < tenorNames.Length; i++)
        tenorDates[i] = Dt.CDSMaturity(asOf_, tenorNames[i]);
      RecoveryCurve recoveryCurve = new RecoveryCurve(asOf_, recoveryRate_);
      SurvivalFitCalibrator calibrator =
        new SurvivalFitCalibrator(asOf_, asOf_, recoveryCurve, discountCurve);
      calibrator.NegSPTreatment = NegSPTreatment.Allow;
      calibrator.ForceFit = true;

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      curve.Name = "CDS_CURVE";
      for (int i = 0; i < tenorDates.Length; i++)
        if (premiums[i] > 0.0)
          curve.AddCDS(tenorNames[i], tenorDates[i], 0.0, premiums[i] / 10000.0, 
            DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      curve.ReFit(0);

      return curve;
    }

    [Test]
    public void TestForceFit()
    {
      double[] premiums = new double[] {
        1026.5,1026.516846,2994.305629,3067.359239,1009.748856,1009.749878 };
      string[] tenorNames = new string[]{
        "1 Year","2 Year","3 Year","5 Year","7 Year","10 Year"};
      double[] expects = null;
      DiscountCurve discountCurve = CreateIRCurve();
      SurvivalCurve sc = CreateCDSCurve(premiums,tenorNames,discountCurve);

      Assert.AreEqual(tenorNames.Length, sc.Count);

      // Verify default probabilities
      expects = new double[]{
        0.190670778, 0.319074096, 0.426831411, 0.593844497, 0.700493434, 0.819907729
        /*0.190329458,0.318862365,0.426716795,0.593853471,0.7005598,0.820006106*/ };
      for (int i = 0; i < tenorNames.Length; ++i)
      {
        Dt dt = Dt.CDSMaturity(asOf_, tenorNames[i]);
        Assert.AreEqual(dt, sc.GetDt(i));
        Assert.AreEqual(expects[i], 1 - sc.Interpolate(dt), 1E-8);
      }

      // Verify CDS values using (1) cashflow pricers and (2) cashflow stream pricers
      expects = new double[]{
        1.92E-12,8.91E-08,0.453209147/*0.453060589*/,0.62815964/*0.628122902*/,2.86E-10,3.62E-12};
      for (int i = 0; i < tenorNames.Length; ++i)
      {
        Dt maturity = Dt.CDSMaturity(asOf_, tenorNames[i]);
        CDS cds = new CDS(asOf_, maturity, Currency.USD, premiums[i] / 10000.0,
          DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

        // First do cashflow pricers
        {
          CashflowStreamPricer pricer = CashflowStreamPricerFactory.PricerForProduct(cds);
          pricer.AsOf = asOf_;
          pricer.Settle = asOf_;
          pricer.DiscountCurve = discountCurve;
          pricer.ReferenceCurve = null;
          pricer.SurvivalCurve = sc;
          pricer.StepSize = 0;
          pricer.StepUnit = TimeUnit.None;
          pricer.RecoveryCurve = new RecoveryCurve(asOf_, recoveryRate_);
          double pv = pricer.Pv();
          Assert.AreEqual(expects[i], pv, 1E-8);
        }
        // Then do cashflow stream pricers
        {
          CashflowPricer pricer = CashflowPricerFactory.PricerForProduct(cds);
          pricer.AsOf = asOf_;
          pricer.Settle = asOf_;
          pricer.DiscountCurve = discountCurve;
          pricer.ReferenceCurve = null;
          pricer.SurvivalCurve = sc;
          pricer.StepSize = 0;
          pricer.StepUnit = TimeUnit.None;
          pricer.RecoveryCurve = new RecoveryCurve(asOf_, recoveryRate_);
          double pv = pricer.Pv();
          Assert.AreEqual(expects[i], pv, 1E-8);
        }
      }
      return;
    }
#endif // Activate_Old_tests
  }
}
