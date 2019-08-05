//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Ccr;
using NUnit.Framework;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRAnalyticDualCurve : TestCCRBase
  {
    #region Init
    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateInput(2, 25, Tenor.Empty, 1, 2, false, false, false, true, 0.4, 0.6);
      var groups = new UniqueSequence<string>(input_.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      netting_ = new Netting(groupArray, subGroupArray, null);
    }

    protected override ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      SimulatorFlags flags = SimulatorFlags.Forward;
      return Simulations.SimulateCounterpartyCreditRisks(input.AsOf, input.Pricers, input.Names, input.Cpty,
                                                         input.CptyRec, input.TenorDates,
                                                         new[] {input.DiscountCurves[0]}, null, input.FwdCurves, input.CreditCurves, input.Volatilities,
                                                         input.FactorLoadings, cptyDefaultTimeCorrelation_, input.Sample,
                                                         input.SimulDates, flags, input.GridSize, false);
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      var projCurve = input_.FwdCurves.FirstOrDefault(c => c is DiscountCurve) as DiscountCurve;
      var pricer = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), projCurve, input_.DiscountCurves[0]);
      var pricers = new IPricer[] {pricer};
      id = new[] {"A"};
      return pricers;
    }

    #endregion

    #region Tests

    [Test]
    public void CVACalculations()
    {
      BaseCVACalculations();
    }

    [Test]
    public void RnTest()
    {
      BaseRnTest();
    }

    [Test]
    public void RateSensitivities()
    {
      BaseRateSensitivities();
    }


    [Test]
    public void RateVolSensitivities()
    {
      BaseRateVolSensitivities();
    }


    [Test]
    public void RateFactorSensitivities()
    {
      BaseRateFactorSensitivities();
    }

    [Test]
    public void FwdSensitivities()
    {
      BaseFwdSensitivities();
    }


    [Test]
    public void FwdVolSensitivities()
    {
      BaseFwdVolSensitivities();
    }


    [Test]
    public void FwdFactorSensitivities()
    {
      BaseFwdFactorSensitivities();
    }

    [Test]
    public void CreditSpreadSensitivities()
    {
      BaseCreditSpreadSensitivities();
    }


    [Test]
    public void CreditVolSensitivities()
    {
      BaseCreditVolSensitivities();
    }


    [Test]
    public void CreditFactorSensitivities()
    {
      BaseCreditFactorSensitivities();
    }

    [Test]
    public void TestTowerProperty()
    {
      BaseTestTowerProperty(null);
    }

    [Test]
    public void TestTowerPropertyNormalRates()
    {
      BaseTestTowerPropertyNormalRates(null);
    }

    [Test]
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping(CreateInput(2, 1, Tenor.Empty, 1, 2, false, false, false, true, 0.4, 0.6));
    }
    #endregion
  }
}