/*
 * Copyright (c) 2005-2014,   . All rights reserved.
 */
using System;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{
  /// <summary>
  /// Test of scenario functions
  /// </summary>
  [TestFixture, Smoke]  
  public class TestPricerScenario : ToolkitTestBase
  {
    #region Types for Custom Pricer

    public enum TestType
    {
      Recovery,
      Survival,
      InterestRate,
      Correlation,
      Volatility
    }

    [Serializable]
    public class CustomProduct : Product, IProduct
    {
    }

    // Define the dummy pricer that return some trival computation results
    // A pricer, implementing the IPricer interface, must have array of 
    // survival curves, one or array of discount curves, dates ...
    [Serializable]
    public class CustomPricer : IPricer
    {
      public CustomPricer(Dt asOf, Dt settle, TestType type, IProduct product,
        SurvivalCurve[] survivalCurves, DiscountCurve discountCurve,
        Correlation corr, CalibratedVolatilitySurface vol)
      {
        Product = product;
        AsOf = asOf;
        Settle = settle;
        SurvivalCurves = survivalCurves;
        DiscountCurve = discountCurve;
        Corr =  corr;
        Volatility = vol;
        Type = type;
      }

      #region properties

      public Correlation Corr { get; private set; }
      public SurvivalCurve[] SurvivalCurves { get; private set; }
      public DiscountCurve DiscountCurve { get; private set; }
      public CalibratedVolatilitySurface Volatility { get; private set; }
      public TestType Type { get; private set; }
      public Dt AsOf { get; set; }
      public Dt Settle { get; set; }
      public IProduct Product { get; private set; }
      public IPricer PaymentPricer => null;

      #endregion properties

      #region IPricer Members

      public double Pv()
      {
        switch (Type)
        {
          case TestType.Recovery:
            return AverageRecovery();
          case TestType.Survival:
            return AverageSurvivalQuotes() * 10000;
          case TestType.InterestRate:
            return AverageInterestRate() * 10000;
          case TestType.Correlation:
            return AverageCorrelation();
          case TestType.Volatility:
            return AverageVolatility();
          default:
            break;
        }
        throw new System.Exception("Unknown test type!");        
      }

      private double AverageVolatility()
      {
        if (Volatility == null)
          return 0;
        return Volatility.Interpolate(Volatility.AsOf, 0.0);
      }

      private double AverageCorrelation()
      {
        if (Corr == null)
          return 0;
        var fac = Corr.Correlations[0];
        return fac * fac;
      }

      private double AverageSurvivalQuotes()
      {
        if (SurvivalCurves == null)
          return 0;
        double sum = 0;
        foreach (var curve in SurvivalCurves)
          if (curve.Tenors.Count > 0)
          {
            double avg = 0;
            foreach (CurveTenor tenor in curve.Tenors)
              avg += CurveUtil.MarketQuote(tenor);
            sum += avg / curve.Tenors.Count;  //each tenor has a spread (quote)
          }
        return sum / SurvivalCurves.Length;
      }

      private double AverageRecovery()
      {
        return SurvivalCurves == null
          ? 0
          : SurvivalCurves.Select(curve => curve.SurvivalCalibrator.RecoveryCurve)
            .Select(rc => rc.RecoveryRate(AsOf))
            .Average();
      }

      private double AverageInterestRate()
      {
        if (DiscountCurve == null || DiscountCurve.Tenors.Count <= 0)
          return 0;
        double avg = 0;
        foreach (CurveTenor tenor in DiscountCurve.Tenors)
          avg += CurveUtil.MarketQuote(tenor);
        return avg / DiscountCurve.Tenors.Count; //each tenor has a spread (quote)
      }

      public double Accrued()
      {
        return 0;
      }

      public Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
      {
        return null;
      }

      public void Reset()
      {
      }

      public Currency ValuationCurrency => Product.Ccy;

      #endregion

      #region ICloneable Members

      public object Clone()
      {
        return MemberwiseClone();
      }

      #endregion
    }

    #endregion Types for Custom Pricer

    [OneTimeSetUp]
    public void Init()
    {
    }

    #region Test Custom Pricer scenarios

    // Custom pricer recovery bump
    [Test, Smoke]    
    public void TestCustomRecovery()
    {
      var pricer = CreateCustomPricer(TestType.Recovery);
      var savedRecoveryCurves = pricer.SurvivalCurves.Select(s => s.SurvivalCalibrator.RecoveryCurve.CloneObjectGraph(CloneMethod.Serialization)).ToArray();
      var recoveryBumps = GenerateRecoveryBumps(savedRecoveryCurves.Length);
      var averageRecoveryRates = recoveryBumps.Average();

      var shift = new ScenarioShiftCreditCurves(pricer.SurvivalCurves, null, ScenarioShiftType.Absolute, recoveryBumps, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(averageRecoveryRates, delta, 1E-12, "Recover Bump");
      for( var i = 0; i < pricer.SurvivalCurves.Length; ++i )
        AssertMatch("RecoveryCurve", pricer.SurvivalCurves[i].SurvivalCalibrator.RecoveryCurve, savedRecoveryCurves[i]);
    }

    // Custom pricer cds spread bump
    [Test, Smoke]
    public void TestCustomSurvival()
    {
      var pricer = CreateCustomPricer(TestType.Survival);
      var savedSurvivalStates = SaveCurveStates(pricer.SurvivalCurves);
      var survivalBumps = GenerateSurvivalBumps(pricer.SurvivalCurves.Length);
      var averSurvivalBumps = survivalBumps.Average();
      var shift = new ScenarioShiftCreditCurves(pricer.SurvivalCurves, survivalBumps, ScenarioShiftType.Absolute, null, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(averSurvivalBumps, delta, 1E-12, "Survival Bumps");
      AssertCurveStatesUnchanged(pricer.SurvivalCurves, savedSurvivalStates);
    }

    // Custom pricer IR bump
    [Test, Smoke]
    public void TestCustomIR()
    {
      const double irBump = 10;
      var pricer = CreateCustomPricer(TestType.InterestRate);
      var savedSurvivalStates = SaveCurveStates(pricer.SurvivalCurves);
      var savedDiscountStates = pricer.DiscountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCurves(new[] { pricer.DiscountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, true);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(irBump, delta, 1E-12);
      AssertCurveStatesUnchanged(pricer.SurvivalCurves, savedSurvivalStates);
      AssertMatch("DiscountCurve", pricer.DiscountCurve, savedDiscountStates);
    }

    // Custom pricer correlation bump
    [Test, Smoke]
    public void TestCustomCorrelation()
    {
      var pricer = CreateCustomPricer(TestType.Correlation);
      const double corrBump = 0.1;
      var savedCorr = pricer.Corr.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCorrelation(new[] { pricer.Corr }, new[] { corrBump }, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      AssertMatch("Corr", pricer.Corr, savedCorr);
      Assert.AreEqual(corrBump, delta, 1E-12);
    }

    // Custom pricer volatility bump
    [Test, Smoke]
    public void TestCustomVolatility()
    {
      var pricer = CreateCustomPricer(TestType.Volatility);
      const double bump = 100; // bp
      var savedVol = pricer.Volatility.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftVolatilities(new[] { pricer.Volatility }, new[] { bump }, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      AssertMatch("Corr", pricer.Volatility, savedVol);
      Assert.AreEqual(bump/10000.0, delta, 1E-12);
    }

    #endregion Test Custom Pricer scenarios

    #region Test CDS bumps

    // CDS recovery bump
    [Test, Smoke]
    public void TestCDSRecovery()
    {
      // Base case
      const double rBump = 0.1;
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateCDSPricer(0, 0, rBump);
      double pv2 = pricer2.Pv(), expectDiff = pv2 - pv1;
      // Sensitivity
      var savedRecoveryCurve = pricer.RecoveryCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCreditCurves(new[] { pricer.SurvivalCurve }, null, ScenarioShiftType.Absolute, new[] { rBump }, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("RecoveryCurve", pricer.RecoveryCurve, savedRecoveryCurve);
      AssertDontMatch("RecoveryCurve", pricer2.RecoveryCurve, savedRecoveryCurve);
    }

    // CDS survival bump
    [Test, Smoke]
    public void TestCDSSurvival()
    {
      // Base case
      var sBump = 1.0;
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateCDSPricer(sBump);
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var savedSurvivalCurve = pricer.SurvivalCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCreditCurves(new[] { pricer.SurvivalCurve }, new[] { sBump }, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("SurvivalCurve", pricer.SurvivalCurve, savedSurvivalCurve);
      AssertDontMatch("SurvivalCurve", pricer2.SurvivalCurve, savedSurvivalCurve);
    }

    // CDS survival combo bump
    // Series of bumps where the order will matter
    [Test, Smoke]
    public void TestCDSSurvivalCombo()
    {
      // Base case
      var sBump = 1.0; // bp
      var sBumpm = 0.1; // percent
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateCDSPricer(sBump, sBumpm);
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var savedSurvivalCurve = pricer.SurvivalCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCreditCurves(new[] { pricer.SurvivalCurve }, new[] { sBump }, ScenarioShiftType.Absolute);
      var shift2 = new ScenarioShiftCreditCurves(new[] { pricer.SurvivalCurve }, new[] { sBumpm }, ScenarioShiftType.Relative);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift, shift2 }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("SurvivalCurve", pricer.SurvivalCurve, savedSurvivalCurve);
      AssertDontMatch("SurvivalCurve", pricer2.SurvivalCurve, savedSurvivalCurve);
    }

    // CDS default event
    [Test, Smoke]
    public void TestCDSDefault()
    {
      // Base case
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var recoveryRate = pricer.SurvivalCurve.SurvivalCalibrator.RecoveryCurve.Interpolate(pricer.Product.Maturity);
      var pv2 = ((recoveryRate-1) * pricer.DiscountCurve.DiscountFactor(pricer.AsOf, pricer.Settle) + pricer.AccruedOnSettleDefault()) * pricer.Notional;
      var expectDiff = pv2 - pv1;
      // Scenario default
      var savedSurvivalCurve = pricer.SurvivalCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftDefaults(new[] { pricer.SurvivalCurve });
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("Default", pricer.SurvivalCurve, savedSurvivalCurve);
    }

    // CDS IR bump
    // The case without recalibration. Simply set the discount curve
    [Test, Smoke]
    public void TestCDSIRNoRefit()
    {
      // Base Pricer case
      const double irBump = 10.0; // bp
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateCDSPricer();
      pricer2.DiscountCurve = SensitivityTestUtil.CreateIRCurve(GetAsOf(), irBump);
      pricer2.Reset();
      var pv2 = pricer2.Pv();
      var expectedDiff = pv2 - pv1;
      // Scenario
      var savedDiscountCurve = pricer.DiscountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCurves(new[] { pricer.DiscountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, false);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectedDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("Discount", pricer.DiscountCurve, savedDiscountCurve);
      AssertDontMatch("Discount", pricer2.DiscountCurve, savedDiscountCurve);
    }

    // CDS IR bump
    // The case with recalibration
    [Test, Smoke]
    public void TestCDSIRRefit()
    {
      // Base Pricer case
      const double irBump = 10.0; // bp
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateCDSPricer(0, 0, 0, irBump);
      double pv2 = pricer2.Pv(), expectedDiff = pv2 - pv1;
      // Scenario
      var savedDiscountCurve = pricer.DiscountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCurves(new[] { pricer.DiscountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, true);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectedDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("DiscountCurve", pricer.DiscountCurve, savedDiscountCurve);
      AssertDontMatch("DiscountCurve", pricer2.DiscountCurve, savedDiscountCurve);
    }

    // CDS combined spread, recovery and rate shift for CDS
    [Test, Smoke]
    public void TestCDSCombo()
    {
      // Base case
      const double sBump = 1.0;
      const double rBump = 0.1;
      const double irBump = 10.0; // bp
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateCDSPricer(sBump, 0, rBump, irBump);
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var savedRecoveryCurve = pricer.RecoveryCurve.CloneObjectGraph(CloneMethod.Serialization);
      var savedSurvivalCurve = pricer.SurvivalCurve.CloneObjectGraph(CloneMethod.Serialization);
      var savedDiscountCurve = pricer.DiscountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCreditCurves(new[] { pricer.SurvivalCurve }, new[] { sBump }, ScenarioShiftType.Absolute, new[] { rBump }, ScenarioShiftType.Absolute);
      var shift2 = new ScenarioShiftCurves(new[] { pricer.DiscountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, true);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift, shift2 }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("SurvivalCurve", pricer.SurvivalCurve, savedSurvivalCurve);
      AssertMatch("RecoveryCurve", pricer.RecoveryCurve, savedRecoveryCurve);
      AssertMatch("DiscountCurve", pricer.DiscountCurve, savedDiscountCurve);
      AssertDontMatch("SurvivalCurve", pricer2.SurvivalCurve, savedSurvivalCurve);
      AssertDontMatch("RecoveryCurve", pricer2.RecoveryCurve, savedRecoveryCurve);
      AssertDontMatch("DiscountCurve", pricer2.DiscountCurve, savedDiscountCurve);
    }

    // CDS shift Premium
    [Test, Smoke]
    public void TestCDSPricerPremium()
    {
      // Base case
      var pricer = CreateCDSPricer();
      var premium = pricer.CDS.Premium;
      var pv1 = pricer.Pv();
      // Manual bump
      const double pBump = 0.005;
      var pricer2 = CreateCDSPricer();
      pricer2.CDS.Premium = premium+pBump;
      pricer2.Reset();
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var shift = new ScenarioShiftPricerTerms(new[] { "Premium" }, new object[] { premium+pBump });
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.CDS.Premium, premium, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
    }

    // CDS shift CDS
    [Test, Smoke]
    public void TestCDSPricerSurvivalCurve()
    {
      // Base case
      var pricer = CreateCDSPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      const double rate = 0.01;
      var pricer2 = CreateCDSPricer();
      pricer2.SurvivalCurve = new SurvivalCurve(pricer2.AsOf, rate);
      pricer2.Reset();
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var savedSurvivalCurve = pricer.SurvivalCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftPricerTerms(new[] { "SurvivalCurve" }, new object[] { rate });
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("SurvivalCurve", pricer.SurvivalCurve, savedSurvivalCurve);
      AssertDontMatch("SurvivalCurve", pricer2.SurvivalCurve, savedSurvivalCurve);
    }

    #endregion Test CDS bumps

    #region Test CDX bumps

    // CDX recovery scenario
    [Test, Smoke]
    public void TestCDXRecovery()
    {
      // Base case
      var pricer = CreateCDXPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var bumps = GenerateRecoveryBumps(125);
      var pricer2 = CreateCDXPricer(null, bumps);
      var pv2 = pricer2.Pv();
      var expextedDiff = pv2 - pv1;
      // Scenario
      var savedStates = pricer.SurvivalCurves[0].SurvivalCalibrator.RecoveryCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCreditCurves(pricer.SurvivalCurves, null, ScenarioShiftType.Absolute, bumps, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expextedDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("Recovery", pricer.SurvivalCurves[0].SurvivalCalibrator.RecoveryCurve, savedStates);
      AssertDontMatch("Recovery", pricer2.SurvivalCurves[0].SurvivalCalibrator.RecoveryCurve, savedStates);
    }

    // CDX IR scenario
    [Test, Smoke]
    public void TestCDXIR()
    {
      // Base case
      var pricer = CreateCDXPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      const double irBump = 10;
      var pricer2 = CreateCDXPricer(null, null, irBump);
      var pv2 = pricer2.Pv();
      var expectedDiff = pv2 - pv1;
      // Scenario
      var savedDiscountCurve = pricer.DiscountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var savedSurvivalCurves = SaveCurveStates(pricer.SurvivalCurves);
      var shift = new ScenarioShiftCurves(new[] { pricer.DiscountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, true);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, true, true);
      Assert.AreEqual(expectedDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("Discount1", pricer.DiscountCurve, savedDiscountCurve);
      AssertCurveStatesUnchanged(pricer.SurvivalCurves, savedSurvivalCurves);
    }

    // CDX survival scenario
    [Test, Smoke]
    public void TestCDXSurvival()
    {
      // Base case
      var pricer = CreateCDXPricer();
      var pv1 = pricer.Pv();
      // Manual Bump
      var bumps = GenerateSurvivalBumps(pricer.SurvivalCurves.Length);
      var pricer2 = CreateCDXPricer(bumps);
      var pv2 = pricer2.Pv();
      var expectedDiff = pv2 - pv1;
      // Scenario
      var savedSurvivalCurves = SaveCurveStates(pricer.SurvivalCurves);
      var shift = new ScenarioShiftCreditCurves(pricer.SurvivalCurves, bumps, ScenarioShiftType.Absolute, null, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, true, true);
      Assert.AreEqual(expectedDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertCurveStatesUnchanged(pricer.SurvivalCurves, savedSurvivalCurves);
    }

    #endregion Test CDX bumps

    #region Test CDO bumps

    [Test, Smoke]
    public void TestCDOCorrelation()
    {
      // Base case
      const double corrBump = 0.1;
      var pricers = CreateCDOPricers();
      var pv1 = new double[pricers.Length];
      var diff = new double[pricers.Length];
      for (var i = 0; i < pricers.Length; i++)
        pv1[i] = pricers[i].Pv();
      // Manual bump
      var pv2 = new double[pricers.Length];
      var pricers2 = CreateCDOPricers(null, null, 0, corrBump);
      for (var i = 0; i < pricers.Length; i++)
      {
        pv2[i] = pricers2[i].Pv();
        diff[i] = pv2[i] - pv1[i];
      }
      // Scenario
      var savedCorr = pricers[0].Correlation.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCorrelation(new[] { pricers[0].Correlation }, new[] { corrBump }, ScenarioShiftType.Absolute);
      var deltas = Scenarios.CalcScenario(pricers, "Pv", new IScenarioShift[] { shift }, true, true);
      for (var i = 0; i < pricers.Length; i++)
      {
        Assert.AreEqual(diff[i], deltas[i], 1E-12);
        Assert.AreEqual(pricers[i].Pv(), pv1[i], 1E-12);
      }
      AssertMatch("Correlation", pricers[0].Correlation, savedCorr);
      AssertDontMatch("Correlation", pricers2[0].Correlation, savedCorr);
      Init();
    }

    [Test, Smoke]
    public void TestCDOSurvival()
    {
      // Base case
      var pricers = CreateCDOPricers();
      var survivalCurves = pricers[0].SurvivalCurves;
      var pv1 = new double[pricers.Length];
      var diff = new double[pricers.Length];
      for (var i = 0; i < pricers.Length; i++)
        pv1[i] = pricers[i].Pv();
      // Manual bump
      var pv2 = new double[pricers.Length];
      var sBumps = GenerateSurvivalBumps(survivalCurves.Length);
      var pricers2 = CreateCDOPricers(sBumps);
      for (var i = 0; i < pricers.Length; i++)
      {
        pv2[i] = pricers2[i].Pv();
        diff[i] = pv2[i] - pv1[i];
      }
      // Scenarios
      var savedSurvivalCurves = SaveCurveStates(survivalCurves);
      var shift = new ScenarioShiftCreditCurves(survivalCurves, sBumps, ScenarioShiftType.Absolute, null, ScenarioShiftType.Absolute);
      var deltas = Scenarios.CalcScenario(pricers, "Pv", new IScenarioShift[] { shift }, true, true);
      for (var i = 0; i < pricers.Length; i++)
      {
        Assert.AreEqual(diff[i], deltas[i], 1E-12);
        Assert.AreEqual(pricers[i].Pv(), pv1[i], 1E-12);
      }
      AssertCurveStatesUnchanged(survivalCurves, savedSurvivalCurves);
      Init();
    }

    [Test, Smoke]
    public void TestCDORecovery()
    {
      // Base case
      var pricers = CreateCDOPricers();
      var survivalCurves = pricers[0].SurvivalCurves;
      var pv1 = new double[pricers.Length];
      var diff = new double[pricers.Length];
      for (var i = 0; i < pricers.Length; i++)
        pv1[i] = pricers[i].Pv();
      // Manual bump
      var pv2 = new double[pricers.Length];
      var rBumps = GenerateRecoveryBumps(survivalCurves.Length);
      var pricers2 = CreateCDOPricers(null, rBumps);
      for (var i = 0; i < pricers.Length; i++)
      {
        pv2[i] = pricers2[i].Pv();
        diff[i] = pv2[i] - pv1[i];
      }
      using (new CheckStates(true, pricers))
      {
        var shift = new ScenarioShiftCreditCurves(survivalCurves, null, ScenarioShiftType.Absolute, rBumps, ScenarioShiftType.Absolute);
        var deltas = Scenarios.CalcScenario(pricers, "Pv", new IScenarioShift[] { shift }, false, true);
        for (var i = 0; i < pricers.Length; i++)
        {
          Assert.AreEqual(diff[i], deltas[i], 1E-12, "Pricer " + (i + 1));
          Assert.AreEqual(pricers[i].Pv(), pv1[i], 1E-12);
        }
      }
      Init();
    }

    // The case without recalibrating the survival curve. Simply set the discount curve            
    [Test, Smoke]
    public void TestCDOIRNoRefit()
    {
      // Base pricing
      const double irBump = 10;
      var pricers = CreateCDOPricers();
      var pv1 = new double[pricers.Length];
      var diff = new double[pricers.Length];
      for (var i = 0; i < pricers.Length; i++)
        pv1[i] = pricers[i].Pv();
      // Manual bump
      var pricers2 = CreateCDOPricers();
      var pv2 = new double[pricers.Length];
      var discountCurve = SensitivityTestUtil.CreateIRCurve(GetAsOf(), irBump);
      for (var i = 0; i < pricers.Length; i++)
      {
        pricers2[i].DiscountCurve = discountCurve;
        pricers2[i].Reset();
        pv2[i] = pricers2[i].Pv();
        diff[i] = pv2[i] - pv1[i];
      }
      // Scenario
      discountCurve = pricers[0].SurvivalCurves[0].SurvivalCalibrator.DiscountCurve;
      var savedIRCurve = discountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCurves(new[] { discountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, false);
      var deltas = Scenarios.CalcScenario(pricers, "Pv", new IScenarioShift[] { shift }, false, true);
      for (var i = 0; i < pricers.Length; i++)
      {
        Assert.AreEqual(diff[i], deltas[i], 1E-12);
        Assert.AreEqual(pricers[i].Pv(), pv1[i], 1E-12);
      }
      AssertMatch("IRCurve", discountCurve, savedIRCurve);
      AssertDontMatch("IRCurve", pricers2[0].SurvivalCurves[0].SurvivalCalibrator.DiscountCurve, savedIRCurve);
      Init();
    }

    // The case with recalibrating the survival curve.            
    [Test, Smoke]
    public void TestCDOIRRefit()
    {
      // Base pricing
      const double irBump = 10;
      var pricers = CreateCDOPricers();
      var pv1 = new double[pricers.Length];
      var diff = new double[pricers.Length];
      for (var i = 0; i < pricers.Length; i++)
        pv1[i] = pricers[i].Pv();
      // Manual bump
      var pricers2 = CreateCDOPricers(null, null, irBump);
      var pv2 = new double[pricers.Length];
      for (var i = 0; i < pricers.Length; i++)
      {
        pv2[i] = pricers2[i].Pv();
        diff[i] = pv2[i] - pv1[i];
      }
      // Scenario
      var discountCurve = pricers[0].SurvivalCurves[0].SurvivalCalibrator.DiscountCurve;
      var savedIRCurve = discountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCurves(new[] { discountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, true);
      var deltas = Scenarios.CalcScenario(pricers, "Pv", new IScenarioShift[] { shift }, false, true);
      for (var i = 0; i < pricers.Length; i++)
      {
        Assert.AreEqual(diff[i], deltas[i], 1E-12);
        Assert.AreEqual(pricers[i].Pv(), pv1[i], 1E-12);
      }
      AssertMatch("IRCurve", discountCurve, savedIRCurve);
      AssertDontMatch("IRCurve", pricers2[0].SurvivalCurves[0].SurvivalCalibrator.DiscountCurve, savedIRCurve);
      Init();
    }

    #endregion Test CDO bumps

    #region Test StockFutureOption bumps

    // StockFutureOption volatility bump
    [Test, Smoke]
    public void TestStockFutureOptionVolatility()
    {
      // Base case
      const double vBump = 100; // bp
      var pricer = CreateStockFutureOptionPricer();
      var volatilitySurface = pricer.VolatilitySurface as CalibratedVolatilitySurface;
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateStockFutureOptionPricer(0, 0, vBump);
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var savedVolatilitySurface = volatilitySurface.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftVolatilities(new[] { volatilitySurface }, new[] { vBump }, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("VolatilitySurface", volatilitySurface, savedVolatilitySurface);
      AssertDontMatch("VolatilitySurface", pricer2.VolatilitySurface, savedVolatilitySurface);
    }

    // StockFutureOption underlying price bump
    [Test]
    public void TestStockFutureOptionUl()
    {
      // Base case
      const double ulBump = 1.0;
      var pricer = CreateStockFutureOptionPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateStockFutureOptionPricer(ulBump);
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var savedUlCurve = pricer.StockCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftStockCurves(new[] { pricer.StockCurve }, new[] { ulBump }, ScenarioShiftType.Absolute, null, ScenarioShiftType.None);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("StockCurve", pricer.StockCurve, savedUlCurve);
      AssertDontMatch("VolatilitySurface", pricer2.StockCurve, savedUlCurve);
    }

    // StockFutureOption IR bump
    [Test, Smoke]
    public void TestStockFutureOptionIR()
    {
      // Base Pricer case
      const double irBump = 10.0; // bp
      var pricer = CreateStockFutureOptionPricer();
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateStockFutureOptionPricer(0, irBump);
      var pv2 = pricer2.Pv();
      var expectedDiff = pv2 - pv1;
      // Scenario
      var savedDiscountCurve = pricer.DiscountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftCurves(new[] { pricer.DiscountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, true);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectedDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("DiscountCurve", pricer.DiscountCurve, savedDiscountCurve);
      AssertDontMatch("DiscountCurve", pricer2.DiscountCurve, savedDiscountCurve);
    }

    // StockFutureOption combined ul price, vol and rate shift
    [Test]
    public void TestStockFutureOptionCombo()
    {
      // Base case
      const double ulBump = 1.0;
      const double irBump = 10.0; // bp
      const double vBump = 0.01;
      var pricer = CreateStockFutureOptionPricer();
      var volatilitySurface = pricer.VolatilitySurface as CalibratedVolatilitySurface;
      var pv1 = pricer.Pv();
      // Manual bump
      var pricer2 = CreateStockFutureOptionPricer(ulBump, irBump, vBump);
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var savedUlCurve = pricer.StockCurve.CloneObjectGraph(CloneMethod.Serialization);
      var savedDiscountCurve = pricer.DiscountCurve.CloneObjectGraph(CloneMethod.Serialization);
      var savedVolatilitySurface = volatilitySurface.CloneObjectGraph(CloneMethod.Serialization);
      var shift = new ScenarioShiftStockCurves(new[] { pricer.StockCurve }, new[] { ulBump }, ScenarioShiftType.Absolute, null, ScenarioShiftType.None);
      var shift1 = new ScenarioShiftCurves(new[] { pricer.DiscountCurve }, new[] { irBump }, ScenarioShiftType.Absolute, true);
      var shift2 = new ScenarioShiftVolatilities(new[] { volatilitySurface }, new[] { vBump }, ScenarioShiftType.Absolute);
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift, shift1, shift2 }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      AssertMatch("StockCurve", pricer.StockCurve, savedUlCurve);
      AssertDontMatch("VolatilitySurface", pricer2.StockCurve, savedUlCurve);
      AssertMatch("VolatilitySurface", volatilitySurface, savedVolatilitySurface);
      AssertDontMatch("VolatilitySurface", pricer2.VolatilitySurface, savedVolatilitySurface);
      AssertMatch("DiscountCurve", pricer.DiscountCurve, savedDiscountCurve);
      AssertDontMatch("DiscountCurve", pricer2.DiscountCurve, savedDiscountCurve);
    }

    // StockFutureOption shift Strike
    [Test, Smoke]
    public void TestStockFutureOptionPricerPremium()
    {
      // Base case
      var pricer = CreateStockFutureOptionPricer();
      var strike = pricer.StockFutureOption.Strike;
      var pv1 = pricer.Pv();
      // Manual bump
      const double sBump = 0.5;
      var pricer2 = CreateStockFutureOptionPricer();
      pricer2.StockFutureOption.Strike = strike + sBump;
      pricer2.Reset();
      var pv2 = pricer2.Pv();
      var expectDiff = pv2 - pv1;
      // Scenario
      var shift = new ScenarioShiftPricerTerms(new[] { "Strike" }, new object[] { strike + sBump });
      var delta = Scenarios.CalcScenario(pricer, "Pv", new IScenarioShift[] { shift }, false, true);
      Assert.AreEqual(expectDiff, delta, 1E-12);
      Assert.AreEqual(pricer.Pv(), pv1, 1E-12);
      Assert.AreEqual(pricer.StockFutureOption.Strike, strike, 1E-12);
    }

    #endregion Test StockFutureOption bumps

    #region Helper Methods

    #region Product Pricer Creaters

    // Create dummy pricer
    private CustomPricer CreateCustomPricer(TestType type)
    {
      var asOf = GetAsOf();
      var settle = GetSettle();
      var product = new CustomProduct {Description = "CustomProduct"};
      var survivalCurves = SensitivityTestUtil.CreateSurvialCurvesArray(asOf, settle);
      var discountCurve = survivalCurves[0].SurvivalCalibrator.DiscountCurve;
      var corr =  new SingleFactorCorrelation(new[] {"singleCorr"}, Math.Sqrt(0.5));
      var vol = CalibratedVolatilitySurface.FromFlatVolatility(asOf, 0.05);
      return new CustomPricer(asOf, settle, type, product, survivalCurves, discountCurve, corr, vol);
    }

    // Create CDS pricer
    private CDSCashflowPricer CreateCDSPricer(double sBump=0.0, double sBumpm=0.0, double rBump=0.0, double irBump=0.0)
    {
      const double recovery = 0.4;
      var asOf = GetAsOf();
      var settle = GetSettle();
      var maturity = Dt.Add(asOf, 5, TimeUnit.Years);
      var cds = new CDS(asOf, maturity, Currency.USD, 0.01, DayCount.Actual360, 
                    Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      var sc = SensitivityTestUtil.CreateSurvivalCurve(GetAsOf(), GetSettle(), null, recovery, sBump, sBumpm, rBump, irBump);
      return new CDSCashflowPricer(cds, asOf, settle, sc.SurvivalCalibrator.DiscountCurve, sc, 0, TimeUnit.None);
    }

    // Create CDX Pricer
    private CDXPricer CreateCDXPricer(double[] sBumps=null, double[] rBumps=null, double irBump=0.0)
    {
      var asOf = GetAsOf();
      var settle = GetSettle();
      //46.5bp is the premium
      var cdx = new CDX(settle, Dt.Add(settle, "5 year"), Currency.USD, 46.5/10000, Calendar.NYB);
      var sc = SensitivityTestUtil.CreateSurvialCurvesArray(GetAsOf(), GetSettle(), sBumps, rBumps, irBump);
      return new CDXPricer(cdx, asOf, settle, sc[0].SurvivalCalibrator.DiscountCurve, sc, 50.0 / 10000);
    }

    // Create Stock Index Option Pricer
    private StockFutureOptionBlackPricer CreateStockFutureOptionPricer(double ulBump=0.0, double irBump=0.0, double vBump=0.0)
    {
      const double futuresPrice = 90;
      const double ulPrice = 89;
      const double divYield = 0.03;
      var asOf = GetAsOf();
      var settle = GetSettle();
      var sfo = new StockFutureOption(Dt.Add(asOf, "1 Month"), 1000, Dt.Add(asOf, "4 Month"), OptionType.Call,
        OptionStyle.European, futuresPrice);
      var discountCurve = SensitivityTestUtil.CreateIRCurve(asOf, irBump);
      var vol = CreateVolatility(asOf, vBump);
      var stockCurve = new StockCurve(asOf, ulPrice+ulBump, discountCurve, divYield, null);
      return new StockFutureOptionBlackPricer(sfo, asOf, settle, futuresPrice, stockCurve, discountCurve, vol, 1.0);
    }

    // Create CDO Pricers
    private SyntheticCDOPricer[] CreateCDOPricers(
      double[] sBumps = null, double[] rBumps = null, double irBump = 0.0, double cBump=0.0
      )
    {
      // Create Synthetic CDOs
      const double corrFactor = 0.5;
      var asOf = GetAsOf();
      var settle = GetSettle();
      var maturity = Dt.Add(settle, "10 year");
      var fees = new [] { 0.4, 0, 0, 0, 0 };
      var premia = new [] { 0.05, 0.055, 0.03, 0.015, 0.004 };
      var attachments = new [] { 0, 0.03, 0.07, 0.10, 0.15 };
      var detachments = new [] { 0.03, 0.07, 0.10, 0.15, 0.30 };
      var desc = new [] { "0%-3%", "3%-7%", "7%-10%", "10%-15%", "15%-30%" };
      var cdos = new SyntheticCDO[fees.Length];
      for (var i = 0; i < cdos.Length; i++)
      {
        cdos[i] = new SyntheticCDO(
          settle, maturity, Currency.USD, fees[i], premia[i], DayCount.Actual360,
          Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
        {
          CdoType = CdoType.Unfunded,
          Attachment = attachments[i],
          Detachment = detachments[i],
          FeeSettle = settle,
          AmortizePremium = true,
          FeeGuaranteed = false,
          Description = desc[i]
        };
        cdos[i].Validate();
      }

      var survivalCurves = SensitivityTestUtil.CreateSurvialCurvesArray(asOf, settle, sBumps, rBumps, irBump);
      var discountCurve = survivalCurves[0].SurvivalCalibrator.DiscountCurve;
      var corrObj = CreateCorrelation(survivalCurves.Length, corrFactor + cBump);

      var principals = new double[survivalCurves.Length];
      for (var i = 0; i < principals.Length; i++)
        principals[i] = 1.0 / principals.Length;

      var pricers = BasketPricerFactory.CDOPricerSemiAnalytic(
        cdos, Dt.Empty, asOf, settle, discountCurve, survivalCurves, principals,
        new Copula(), corrObj, 3, TimeUnit.Months, 0, 0.0, null, false, false);
      pricers[0].Basket.Principals = principals;
      return pricers;
    }

    #endregion Product Pricer Creator

    #region Curves Creaters

    /// <summary>
    /// Create single factor correlation
    /// </summary>
    private static Correlation CreateCorrelation(int basketSize, double corr)
    {
      var names = new string[basketSize];
      for (var i = 0; i < basketSize; i++)
        names[i] = "singleCorr";
      return new SingleFactorCorrelation(names, Math.Sqrt(corr));
    }

    /// <summary>
    /// Create volatility surface
    /// </summary>
    private static CalibratedVolatilitySurface CreateVolatility(Dt asOf, double vBump=0)
    {
      const double vol = 0.15;
      return CalibratedVolatilitySurface.FromFlatVolatility(asOf, vol+vBump/10000.0);
    }

    // Generate set of CDS bumps
    private static double[] GenerateSurvivalBumps(int num)
    {      
      var rand = new Random(-1234);
      var bumps = new double[num];
      for (var i = 0; i < num; i++)
        bumps[i] = 1.0 + 0.5 * rand.NextDouble();
      return bumps;
    }

    // Generate set of recovery rate bumps
    private static double[] GenerateRecoveryBumps(int num)
    {
      var rand = new Random(-1234);
      var bumps = new double[num];
      for(var  i = 0; i < num ; i++)
        bumps[i] = 0.1 * rand.NextDouble();
      return bumps;
    }

    #endregion Curve Creaters

    private Dt GetAsOf()
    {
      return PricingDate == 0 ? Dt.Today() : ToDt(PricingDate);
    }

    private Dt GetSettle()
    {
      Dt asOf = GetAsOf();
      return SettleDate == 0 ? Dt.Add(asOf, 1, TimeUnit.Days) : ToDt(SettleDate);
    }

    private static Curve[] SaveCurveStates(Curve[] curves)
    {
      var count = curves.Length;
      if (count <= 0)
        return null;
      return curves.CloneObjectGraph(CloneMethod.Serialization);
    }

    private static void AssertCurveStatesUnchanged(Curve[] curves, Curve[] saved)
    {
      if (saved == null)
        return;
      for (var i = 0; i < saved.Length; ++i)
        AssertMatch(curves[i].Name, curves[i], saved[i]);
    }

    #endregion Helper Methods
  }
}
