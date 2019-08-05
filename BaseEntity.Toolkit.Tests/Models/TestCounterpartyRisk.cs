//
// Compare CDS protection values with closed-form analytical solutions in simple settings
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture, Smoke]
  public class TestCounterpartyRisk : ToolkitTestBase
  {
    #region Data

    const double epsilon = 1.0e-7;
    const double principal = 10000;
    const double recoveryRate = 0.4;
    const double corr = 0.3;
    const double hazardRate = 0.03;
    const double counterpartyHazardRate = 0.02;
    const double discountRate = 0.025;
    const double premium = 0.0080;
    const int stepSize = 1;
    const TimeUnit stepUnit = TimeUnit.Months;
    const DayCount dayCount = DayCount.Actual360;

    #endregion Data

    #region Helpers

    IEnumerable<Dt> TestDates
    {
      get
      {
        Dt end = new Dt(20130101);
        for (Dt date = new Dt(20090101); date < end; date = Dt.Add(date, 1))
          yield return date;
      }
    }

    private static SurvivalCurve
    CreateSurvivalCurve( Dt asOfDate,
                         double hazardRate )
    {
      SurvivalCurve SurvCurve = new SurvivalCurve(asOfDate, hazardRate);
      return SurvCurve;
    }

    private static DiscountCurve
    CreateDiscountCurve(Dt asOfDate, double discountRate)
    {
      return new DiscountCurve(asOfDate, discountRate);
    }

    private static CDS
    CreateCDS(Dt issueDate, Dt maturityDate, double premium)
    {
      CDS cds = new CDS( issueDate, maturityDate,
                         Currency.USD,
                         premium,
                         DayCount.Actual365Fixed,
                         Frequency.Quarterly,
                         BDConvention.None,
                         Calendar.NYB);
      cds.Validate();

      return cds;
    }

    private double
    AnalyticalProtection(
      Dt asOf, Dt settle, Dt maturity,
      double r,  // equivalent discount rate
      double h,  // credit hazard rate
      double hc,  // counterparty hazard rate
      bool includeMaturityProtection
                        )
    {
      // Why this does not work?
      //double T = Dt.fraction(settle, maturity, dayCount) ;

      double t = (includeMaturityProtection ? 1 : 0) / 365.0;
      double T = Dt.Diff(settle, maturity, dayCount) / 365.0;
      double protect =
        (Math.Exp(-(r+h+hc)*(t+T)) - 1) * (1 - recoveryRate)
        * h / (r + h + hc) ;
      T = Dt.Diff(asOf, settle, dayCount) / 365.0 ;
      protect *= Math.Exp(-r*T);
      return protect;
    }

    #endregion Helpers

    #region Tests

    [Test, Smoke]
    public void
    ProtectionNoCounterpartyRisk()
    {
      const double tolerance = epsilon;
      foreach (Dt asOf in TestDates)
      {
        Dt settle = asOf;
        Dt issue = asOf;
        Dt maturity = Dt.Add(issue, 5, TimeUnit.Years);

        SurvivalCurve survCurve
          = CreateSurvivalCurve(asOf, hazardRate);
        DiscountCurve discountCurve
          = CreateDiscountCurve(asOf, discountRate);
        CDS cds = CreateCDS(issue, maturity, premium);

        CDSCashflowPricer pricer = new CDSCashflowPricer(
          cds, asOf, settle, discountCurve, survCurve,
          stepSize, stepUnit);

        // analytical protection w/o counterpart risks
        double analyticProtection = AnalyticalProtection(
          asOf, settle, maturity, discountRate,
          hazardRate, 0.0, pricer.IncludeMaturityProtection);

        // model protection w/o counterpart risks
        pricer.RecoveryCurve = new RecoveryCurve(asOf, recoveryRate);
        double modelProtection = pricer.ProtectionPv();

        if (Math.Abs(analyticProtection - modelProtection) > tolerance)
        {
          Assert.AreEqual(analyticProtection, modelProtection, tolerance,
            asOf.ToInt().ToString());
        }
      }
      return;
    }

    [Test, Smoke]
    public void
    ProtectionNoCounterpartyRiskLaterSettle()
    {
      const double tolerance = epsilon;
      foreach (Dt asOf in TestDates)
      {
        Dt settle = Dt.Add(asOf, 15, TimeUnit.Days);
        Dt issue = asOf;
        Dt maturity = Dt.Add(issue, 5, TimeUnit.Years);

        SurvivalCurve survCurve
          = CreateSurvivalCurve(asOf, hazardRate);
        DiscountCurve discountCurve
          = CreateDiscountCurve(asOf, discountRate);
        CDS cds = CreateCDS(issue, maturity, premium);

        CDSCashflowPricer pricer = new CDSCashflowPricer(
          cds, asOf, settle, discountCurve, survCurve,
          stepSize, stepUnit);

        // analytical protection w/o counterpart risks
        double analyticProtection = AnalyticalProtection(
          asOf, settle, maturity, discountRate,
          hazardRate, 0.0, pricer.IncludeMaturityProtection);

        // model protection w/o counterpart risks
        pricer.RecoveryCurve = new RecoveryCurve(asOf, recoveryRate);
        double modelProtection = pricer.ProtectionPv();

        if (Math.Abs(analyticProtection - modelProtection) > tolerance)
        {
          Assert.AreEqual(analyticProtection, modelProtection, tolerance,
            asOf.ToInt().ToString());
        }
      }
      return;
    }

    [Test, Smoke]
    public void
    ProtectionWithCounterpartyRisk()
    {
      const double tolerance = epsilon;
      foreach (Dt asOf in TestDates)
      {
        Dt settle = asOf;
        Dt issue = asOf;
        Dt maturity = Dt.Add(issue, 5, TimeUnit.Years);

        SurvivalCurve survCurve
          = CreateSurvivalCurve(asOf, hazardRate);
        SurvivalCurve counterpartySurvCurve
          = CreateSurvivalCurve(asOf, counterpartyHazardRate);
        DiscountCurve discountCurve
          = CreateDiscountCurve(asOf, discountRate);
        CDS cds = CreateCDS(issue, maturity, premium);

        CDSCashflowPricer pricer = new CDSCashflowPricer(
          cds, asOf, settle, discountCurve,
          survCurve, counterpartySurvCurve, 0.0, // correlation
          stepSize, stepUnit);
        pricer.RecoveryCurve = new RecoveryCurve(asOf, recoveryRate);
        pricer.IncludeMaturityProtection = false;


        // analytical protection w/o counterpart risks
        double analyticProtection = AnalyticalProtection(
          asOf, settle, maturity, discountRate, hazardRate,
          counterpartyHazardRate, pricer.IncludeMaturityProtection);

        // model protection w/o counterpart risks
        double modelProtection = pricer.ProtectionPv();

        if (Math.Abs(analyticProtection - modelProtection) > tolerance)
        {
          Assert.AreEqual(analyticProtection, modelProtection, tolerance,
            asOf.ToInt().ToString());
        }
      }
      return;
    }

    [Test, Smoke]
    public void
    ProtectionWithCounterpartyRiskLaterSettle()
    {
      const double tolerance = epsilon * 3;
      foreach (Dt asOf in TestDates)
      {
        Dt settle = Dt.Add(asOf, 15, TimeUnit.Days);
        Dt issue = asOf;
        Dt maturity = Dt.Add(issue, 5, TimeUnit.Years);

        SurvivalCurve survCurve
          = CreateSurvivalCurve(asOf, hazardRate);
        SurvivalCurve counterpartySurvCurve
          = CreateSurvivalCurve(asOf, counterpartyHazardRate);
        DiscountCurve discountCurve
          = CreateDiscountCurve(asOf, discountRate);
        CDS cds = CreateCDS(issue, maturity, premium);

        CDSCashflowPricer pricer = new CDSCashflowPricer(
          cds, asOf, settle, discountCurve,
          survCurve, counterpartySurvCurve, 0.0, // correlation
          stepSize, stepUnit);
        pricer.IncludeMaturityProtection = false;
        pricer.RecoveryCurve = new RecoveryCurve(asOf, recoveryRate);

        // analytical protection w/o counterpart risks
        double analyticProtection = AnalyticalProtection(
          asOf, settle, maturity, discountRate, hazardRate,
          counterpartyHazardRate, pricer.IncludeMaturityProtection);

        // model protection w/o counterpart risks
        double modelProtection = pricer.ProtectionPv();

        if (Math.Abs(analyticProtection - modelProtection) > tolerance)
        {
          Assert.AreEqual(analyticProtection, modelProtection, tolerance,
            asOf.ToInt().ToString());
        }
      }
      return;
    }
    #endregion Tests

  } // TestCounterpartyRisk
} 
