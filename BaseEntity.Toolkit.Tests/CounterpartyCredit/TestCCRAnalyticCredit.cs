//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Ccr;
using NUnit.Framework;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRAnalyticCredit : TestCCRBase
  {
    #region Init
    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateInput(2, 25, Tenor.Empty, 1, lambda_.Length, true, false, false, false, 0.4, 0.6);
      var groups = new UniqueSequence<string>(input_.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      netting_ = new Netting(groupArray, subGroupArray, null);
    }


   

    protected override ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      SimulatorFlags flags = SimulatorFlags.Credit;
      return Simulations.SimulateCounterpartyCreditRisks(input.AsOf, input.Pricers, input.Names, input.Cpty,
                                                         input.CptyRec, input.TenorDates,
                                                         new[] {input.DiscountCurves[0]},
                                                         null, null, input.CreditCurves, input.Volatilities, input.FactorLoadings, cptyDefaultTimeCorrelation_, input.Sample,
                                                         input.SimulDates, flags, input.GridSize, false);
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      var pricers = new IPricer[1];
      pricers[0] = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), input_.CreditCurves[0],
                                      input_.DiscountCurves[0]);
      id = new[] {"A"};
      return pricers;
    }

    #endregion

    #region Tests
    [Test]
    public void RnTest()
    {
      BaseRnTest();
    }

    [Test]
    public void CVACalculations()
    {
      BaseCVACalculations();
    }


    [Test]
    public void CreditSpreadSensitivities()
    {
      BaseCreditSpreadSensitivities();
    }


    [Test]
    public  void CreditVolSensitivities()
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
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping(CreateInput(2, 1, Tenor.Empty, 1, lambda_.Length, true, false, false, false, 0.4, 0.6));
    }

    [Test]
    public void TestModelCalibration()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 1, lambda_.Length, false, false, false, false);
      var rand = new Random(5);
      var creditVols = input_.CreditCurves.Skip(2).Select(cc => GetVolCurve(cc.Name, new[] {"6M"}, rand.NextDouble(), 0.002)).ToArray();
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      var betas = GenerateBetas(rand, lambda_.Length - 2, fl, corr, 1.0);
      var cpty = new[] {input.CreditCurves[0], input.CreditCurves[1]};
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      CCRCalibrationUtils.CalibrateSemiAnalyticCreditModel(asOf_, tenors_.Select(Tenor.Parse).ToArray(), new[] {"ITraxx", "CDX"}, corr[0, 1],
                                                                     input.CreditCurves.Skip(2).ToArray(), creditVols.Select(v => Tenor.Parse("5Y")).ToArray(),
                                                                     creditVols, betas, cpty, new[] {Tenor.Parse("5Y"), Tenor.Parse("5Y")},
                                                                     input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] {"3M"}, 0.65, 0.025)).ToArray(),
                                                                     GenerateBetas(rand, 2, fl, corr, 0.5), out factors,
                                                                     out vols);
      timer.Stop();
      var results = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(results);
    }

    #endregion
  }
}
