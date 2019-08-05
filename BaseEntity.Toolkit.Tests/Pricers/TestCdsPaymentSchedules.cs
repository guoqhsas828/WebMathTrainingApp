//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestCdsPaymentSchedules
  {
    #region Tests

    [TestCaseSource(nameof(PricerFiles))]
    public static void ProtectionPv(string file)
    {
      DoTest<CDSCashflowPricer>(file, ValidateProtectionPv);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void FullFeePv(string file)
    {
      DoTest<CDSCashflowPricer>(file, ValidateFeePv);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void ProductPv(string file)
    {
      DoTest<CDSCashflowPricer>(file, ValidateProductPv);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void BreakEvenPremium(string file)
    {
      DoTest<CDSCashflowPricer>(file, ValidateBreakEvenPremium);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void RiskyDuration(string file)
    {
      DoTest<CDSCashflowPricer>(file, ValidateRiskyDuration);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void Premium01(string file)
    {
      DoTest<CDSCashflowPricer>(file, ValidatePremium01);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void FwdPremium01(string file)
    {
      DoTest<CDSCashflowPricer>(file, ValidateFwdPremium01);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void VOD(string file)
    {
      DoTest<IPricer>(file, ValidateVod);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void Theta(string file)
    {
      DoTest<IPricer>(file, ValidateTheta);
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void Cashflows(string file)
    {
      DoTest<CDSCashflowPricer>(file, pricer =>
        TestPaymentCashflowUtil.AssertEqualCashflows(pricer.Cashflow,
          pricer.PaymentScheduleToCashflow()));
    }

    [TestCaseSource(nameof(PricerFiles))]
    public static void CashflowsFromSettle(string file)
    {
      DoTest<CDSCashflowPricer>(file, pricer =>
      {
        var settle = pricer.Settle;
        TestPaymentCashflowUtil.AssertEqualCashflows(
          pricer.GenerateCashflow(null, settle),
          pricer.PaymentScheduleToCashflow(settle));
      });
    }

    private static void DoTest<T>(string file, Action<T> action)
      where T: class, IPricer
    {
      var pricer = (IPricer)XmlSerialization.ReadXmlFile(
        Path.Combine(PricerFolder, $"{file}.xml"));
      var thePricer = pricer as T;
      if (thePricer != null) action(thePricer);
    }

    #endregion

    #region Case source

    private static readonly string PricerFolder = Path.Combine(
      BaseEntityContext.InstallDir, @"toolkit/test/data/CreditPricers/");

    private static IEnumerable<string> PricerFiles
    {
      get
      {
        foreach (var path in Directory.EnumerateFiles(PricerFolder))
        {
          var file = Path.GetFileNameWithoutExtension(path);
          if (!string.IsNullOrEmpty(file)) yield return file;
        }
      }
    }

    #endregion

    #region More tests

    [TestCase(20170620, 0.0, 0.5)]
    [TestCase(20170920, 0.0, 0.5)]
    [TestCase(20180620, 0.0, 0.5)]
    [TestCase(20170620, 0.5, 0.0)]
    [TestCase(20170920, 0.5, 0.0)]
    [TestCase(20180620, 0.5, 0.0)]
    [TestCase(20170620, 0.1, 0.05)]
    [TestCase(20170920, 0.1, 0.05)]
    [TestCase(20180620, 0.1, 0.05)]
    public void PvNoGrids(int maturityDate,
      double discountRate, double hazardRate)
    {
      ValidatePv(maturityDate, discountRate, hazardRate, false, false);
    }

    [TestCase(20170620, 0.0, 0.5)]
    [TestCase(20170920, 0.0, 0.5)]
    [TestCase(20180620, 0.0, 0.5)]
    [TestCase(20170620, 0.5, 0.0)]
    [TestCase(20170920, 0.5, 0.0)]
    [TestCase(20180620, 0.5, 0.0)]
    [TestCase(20170620, 0.1, 0.05)]
    [TestCase(20170920, 0.1, 0.05)]
    [TestCase(20180620, 0.1, 0.05)]
    public void PvGrids(int maturityDate,
      double discountRate, double hazardRate)
    {
      ValidatePv(maturityDate, discountRate, hazardRate, true, false);
    }

    [TestCase(20170620, 0.0, 0.5)]
    [TestCase(20170920, 0.0, 0.5)]
    [TestCase(20180620, 0.0, 0.5)]
    [TestCase(20170620, 0.5, 0.0)]
    [TestCase(20170920, 0.5, 0.0)]
    [TestCase(20180620, 0.5, 0.0)]
    [TestCase(20170620, 0.1, 0.05)]
    [TestCase(20170920, 0.1, 0.05)]
    [TestCase(20180620, 0.1, 0.05)]
    public void PvLogLinearNoGrids(int maturityDate,
      double discountRate, double hazardRate)
    {
      ValidatePv(maturityDate, discountRate, hazardRate, false, true);
    }

    [TestCase(20170620, 0.0, 0.5)]
    [TestCase(20170920, 0.0, 0.5)]
    [TestCase(20180620, 0.0, 0.5)]
    [TestCase(20170620, 0.5, 0.0)]
    [TestCase(20170920, 0.5, 0.0)]
    [TestCase(20180620, 0.5, 0.0)]
    [TestCase(20170620, 0.1, 0.05)]
    [TestCase(20170920, 0.1, 0.05)]
    [TestCase(20180620, 0.1, 0.05)]
    public void PvLogLinearGrids(int maturityDate,
      double discountRate, double hazardRate)
    {
      ValidatePv(maturityDate, discountRate, hazardRate, true, true);
    }

    public void ValidatePv(int maturityDate,
      double discountRate, double hazardRate,
      bool withGrid, bool logLinear)
    {
      Dt asOf = new Dt(20170215);
      Dt settle = new Dt(20170412);
      Dt effective = new Dt(20170328);
      Dt maturity = new Dt(maturityDate);
      double premium = 0.01;
      Frequency freq = Frequency.Quarterly;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      double notional = 1.0;

      // Create CDS pricer
      CDS cds = new CDS(effective, maturity, Currency.USD, premium,
        DayCount.Actual360, freq, roll, cal);
      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate);
      SurvivalCurve survivalCurve = new SurvivalCurve(asOf, hazardRate)
      {
        Stressed = logLinear
      };
      CDSCashflowPricer pricer = new CDSCashflowPricer(cds, asOf, settle,
        discountCurve, survivalCurve, 0, TimeUnit.None);
      pricer.Notional = notional;
      if (withGrid)
      {
        pricer.StepSize = 1;
        pricer.StepUnit = TimeUnit.Months;
      }

      ValidateProtectionPv(pricer);
      ValidateFeePv(pricer);
      ValidateProductPv(pricer);
      ValidateBreakEvenPremium(pricer);
      ValidateRiskyDuration(pricer);
      ValidatePremium01(pricer);
      ValidateFwdPremium01(pricer);
      ValidateVod(pricer);
      ValidateTheta(pricer);
    }

    #endregion

    #region Assertions
    private static void ValidateProtectionPv(CDSCashflowPricer pricer)
    {
      var psProtectionPv = pricer.ProtectionPv();
      var cfProtectionPv = pricer.CDS.Funded ? 0
        : pricer.CfProtectionPv();
      if (cfProtectionPv.AlmostEquals(0.0))
        Assert.AreEqual(cfProtectionPv, psProtectionPv, 1E-15, "ProtectionPv");
      else
        Assert.AreEqual(1.0, psProtectionPv / cfProtectionPv, 1E-13, "ProtectionPv");
    }

    private static void ValidateFeePv(CDSCashflowPricer pricer)
    {
      var psFeePv = pricer.FullFeePv();
      var cfFeePv = pricer.CfFeePv();
      if (cfFeePv.AlmostEquals(0.0))
        Assert.AreEqual(cfFeePv, psFeePv, 1E-15, "FeePv");
      else
        Assert.AreEqual(1.0, psFeePv / cfFeePv, 2E-14, "FeePv");

      var psPv = pricer.ProductPv();
      var cfPv = pricer.CfPv();
      if (cfPv.AlmostEquals(0.0))
        Assert.AreEqual(cfPv, psPv, 1E-15, "Pv");
      else
        Assert.AreEqual(1.0, psPv / cfPv, 5E-14, "Pv");
    }

    private static void ValidateProductPv(CDSCashflowPricer pricer)
    {
      var psPv = pricer.ProductPv();
      var cfPv = pricer.CfPv();
      if (cfPv.AlmostEquals(0.0))
        Assert.AreEqual(cfPv, psPv, 1E-15, "Pv");
      else
        Assert.AreEqual(1.0, psPv / cfPv, 5E-14, "Pv");
    }

    private static void ValidateBreakEvenPremium(
      CDSCashflowPricer pricer)
    {
      const string what = "BreakEvenPremium";
      var settle = pricer.Settle;
      var psPv = pricer.PsBreakEvenPremium(settle, false);
      var cfPv = pricer.CfBreakEvenPremium(settle, false);
      if (double.IsInfinity(cfPv) && !cfPv.Equals(psPv))
      {
        Assert.AreEqual(0.0, psPv, 1E-15, what);
      }
      else if (cfPv.AlmostEquals(0.0))
        Assert.AreEqual(cfPv, psPv, 1E-15, what);
      else
        Assert.AreEqual(1.0, psPv/cfPv, 5E-14, what);
    }

    private static void ValidateRiskyDuration(
      CDSCashflowPricer cdsPricer)
    {
      var tol = cdsPricer.CDS.Funded ? 5E-11 : 5E-14;
      var psDuration = cdsPricer.PsRiskyDuration();
      var cfDuration = cdsPricer.CfRiskyDuration();
      if (cfDuration.AlmostEquals(0.0))
        Assert.AreEqual(cfDuration, psDuration, 1E-15, "Duration");
      else
        Assert.AreEqual(1.0, psDuration/cfDuration, tol, "Duration");
    }

    private static void ValidatePremium01(
      CDSCashflowPricer cdsPricer)
    {
      var tol = cdsPricer.CDS.Funded ? 5E-11 : 5E-14;
      Dt settle = cdsPricer.Settle;
      var psPremium01 = cdsPricer.PsFwdPremium01(settle);
      var cfPremium01 = cdsPricer.CfFwdPremium01(settle);
      if (cfPremium01.AlmostEquals(0.0))
        Assert.AreEqual(cfPremium01, psPremium01, 1E-15, "Premium01");
      else
        Assert.AreEqual(1.0, psPremium01/cfPremium01, tol, "Premium01");
    }

    private static void ValidateFwdPremium01(
      CDSCashflowPricer cdsPricer)
    {
      var tol = cdsPricer.CDS.Funded ? 5E-11 : 5E-14;
      Dt fwdSettle = Dt.AddDays(cdsPricer.AsOf, 72, Calendar.NYB);
      var psPremium01 = cdsPricer.PsFwdPremium01(fwdSettle);
      var cfPremium01 = cdsPricer.CfFwdPremium01(fwdSettle);
      if (cfPremium01.AlmostEquals(0.0))
        Assert.AreEqual(cfPremium01, psPremium01, 1E-15, "FwdPremium01");
      else
        Assert.AreEqual(1.0, psPremium01/cfPremium01, tol, "FwdPremium01");
    }

    private static void ValidateVod(IPricer pricer)
    {
      var cdsPricer = pricer as CDSCashflowPricer;
      if (cdsPricer != null)
      {
        if (!cdsPricer.SurvivalCurve.DefaultDate.IsEmpty())
          return;

        var psVod = Sensitivities.VOD(cdsPricer, "ProductPv");
        var cfVod = Sensitivities.VOD(cdsPricer, "CfPv");
        if (cfVod.AlmostEquals(0.0))
          Assert.AreEqual(cfVod, psVod, 1E-15, "VOD");
        else
          Assert.AreEqual(1.0, psVod/cfVod, 5E-14, "VOD");
      }
      else
      {
        var psVod = Sensitivities.VOD(pricer, "Pv");
        var cfVod = CfCalc(() => Sensitivities.VOD(pricer, "Pv"));
        if (cfVod.AlmostEquals(0.0))
          Assert.AreEqual(cfVod, psVod, 1E-15, "VOD");
        else
          Assert.AreEqual(1.0, psVod/cfVod, 5E-14, "VOD");
      }
    }

    private static void ValidateTheta(IPricer pricer)
    {
      Dt toAsOf = pricer.AsOf + 1, toSettle = pricer.Settle + 1;
      var cdsPricer = pricer as CDSCashflowPricer;
      if (cdsPricer != null)
      {
        if (!cdsPricer.SurvivalCurve.DefaultDate.IsEmpty())
          return;

        var psTheta = Sensitivities.Theta(
          cdsPricer, "ProductPv", toAsOf, toSettle,
          ThetaFlags.None, SensitivityRescaleStrikes.No);
        var cfTheta = Sensitivities.Theta(
          cdsPricer, "CfPv", toAsOf, toSettle,
          ThetaFlags.None, SensitivityRescaleStrikes.No);
        if (cfTheta.AlmostEquals(0.0))
          Assert.AreEqual(cfTheta, psTheta, 1E-15, "Theta");
        else
          Assert.AreEqual(1.0, psTheta/cfTheta, 5E-9, "Theta");
      }
      else
      {
        var psTheta = Sensitivities.Theta(
          pricer, "Pv", toAsOf, toSettle,
          ThetaFlags.None, SensitivityRescaleStrikes.No);
        var cfTheta = CfCalc(() => Sensitivities.Theta(
          pricer, "Pv", toAsOf, toSettle,
          ThetaFlags.None, SensitivityRescaleStrikes.No));
        if (cfTheta.AlmostEquals(0.0))
          Assert.AreEqual(cfTheta, psTheta, 1E-15, "Theta");
        else
          Assert.AreEqual(1.0, psTheta/cfTheta, 5E-9, "Theta");
      }
    }

    private static double CfCalc(Func<double> fn)
    {
      var enabled = CDSCashflowPricer.IsLegacyCashflowEnabled;
      try
      {
        CDSCashflowPricer.IsLegacyCashflowEnabled = true;
        return fn();
      }
      finally
      {
        CDSCashflowPricer.IsLegacyCashflowEnabled = enabled;
      }
    }

    #endregion
  }
}
