//
// Copyright (c)    2018. All rights reserved.
//

using System;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  class TestCdsFromTerms
  {
    /// <summary>
    /// Test consistency between CDS from terms and CDS
    /// </summary>
    [Test, Smoke]
    public void CdsFromTermsConsistency()
    {
      const double tolerance = 1.0E-6;
      const double notional = 5000000;
      var traded = new Dt(20, 06, 2017);
      var tenor = "5Y";
      var asOf = Dt.AddDays(traded, 10, Calendar.NYB);
      var settle = asOf;

      var cds = CreateCDS(traded, tenor);
      cds.Premium = 0.02;
      var cdsType = "SNAC".ToCreditDerivativeTransactionType();
      var cdsFromTerms = StandardProductTermsUtil.GetStandardCds(cdsType,
        traded, tenor, cds.Ccy, cds.Premium, cds.Fee, true);

      var discountCurve = CreateIRCurve(asOf);
      var survivalCurve = CreateCDSCurve(asOf, discountCurve);

      // Test CashflowPricer
      var pricer = CreateCDSPricer(asOf, settle, cds, notional, discountCurve, survivalCurve);
      var pricerFromTerms = CreateCDSPricer(asOf, settle, cdsFromTerms, notional, discountCurve, survivalCurve);

      // Test accrued
      var result = pricerFromTerms.Accrued();
      var expect = pricer.Accrued();
      Assert.AreEqual(expect, result, tolerance*Math.Abs(expect), "Accrued");

      // Test accrued
      result = pricerFromTerms.ProductPv();
      expect = pricer.ProductPv();
      Assert.AreEqual(expect, result, tolerance*Math.Abs(expect), "Product Pv");
    }

    /// <summary>
    ///   Create a IR Curve
    /// </summary>
    private static DiscountCurve CreateIRCurve(Dt asOf)
    {
      string[] mmTenors = new string[] { "6 Month", "1 Year" };
      double[] mmRates = new double[] { 0.0369, 0.0386 };
      Dt[] mmMaturities = new Dt[mmTenors.Length];
      for (int i = 0; i < mmTenors.Length; i++)
        mmMaturities[i] = Dt.Add(asOf, mmTenors[i]);
      DayCount mmDayCount = DayCount.Actual360;

      string[] swapTenors = new string[] { "2 Year", "3 Year", "5 Year", "7 Year", "10 Year" };
      double[] swapRates = new double[] { 0.0399, 0.0407, 0.0417, 0.0426, 0.044 };
      Dt[] swapMaturities = new Dt[swapTenors.Length];
      for (int i = 0; i < swapTenors.Length; i++)
        swapMaturities[i] = Dt.Add(asOf, swapTenors[i]);
      DayCount swapDayCount = DayCount.Thirty360;

      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf, asOf);
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
    private static SurvivalCurve CreateCDSCurve(Dt asOf, DiscountCurve discountCurve)
    {
      var premiums = new double[] { 65.00, 112.00, 130.00, 163.00, 182.00, 194.00, 206.00 };
      var tenorNames = new string[]{
        "6 Month","1 Year","2 Year","3 Year","5 Year","7 Year","10 Year"};
      var tenorDates = new Dt[tenorNames.Length];
      for (var i = 0; i < tenorNames.Length; i++)
        tenorDates[i] = Dt.CDSMaturity(asOf, tenorNames[i]);
      var recoveryRate = 0.4;
      var recoveryCurve = new RecoveryCurve(asOf, recoveryRate);
      var calibrator =
        new SurvivalFitCalibrator(asOf, asOf, recoveryCurve, discountCurve);
      calibrator.NegSPTreatment = NegSPTreatment.Allow;
      calibrator.ForceFit = false;

      var curve = new SurvivalCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      curve.Name = "CDS_CURVE";
      for (var i = 0; i < tenorDates.Length; i++)
        if (premiums[i] > 0.0)
          curve.AddCDS(tenorNames[i],
                        tenorDates[i], 0.0,
                        premiums[i] / 10000.0,
                        DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      curve.ReFit(0);

      return curve;
    }

    /// <summary>
    ///   Create a CDS product
    /// </summary>
    private static CDS CreateCDS(Dt tradeDt, string tenor)
    {
      var settle = ToolkitConfigurator.Settings.SurvivalCalibrator.UseNaturalSettlement ? Dt.Add(tradeDt, 1) : tradeDt;
      var maturity = Dt.CDSMaturity(settle, tenor);
      return new CDS(
        tradeDt, maturity,
        Currency.USD, 182.0 / 10000,
        DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB);
    }

    /// <summary>
    ///   Create a CDS Pricer
    /// </summary>
    private static CDSCashflowPricer CreateCDSPricer(
      Dt asOf, Dt settle, CDS cds, double notional, DiscountCurve discountCurve, SurvivalCurve survivalCurve)
    {
      CDSCashflowPricer pricer = new CDSCashflowPricer(
        cds, asOf, settle, discountCurve, survivalCurve, 0, TimeUnit.None);
      pricer.Notional = notional;
      return pricer;
    }

  }
}
