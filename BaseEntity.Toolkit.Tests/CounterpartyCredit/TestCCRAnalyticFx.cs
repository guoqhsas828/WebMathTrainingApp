//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Ccr;

using NUnit.Framework;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRAnalyticFx : TestCCRBase
  {
    #region Init
    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateInput(2, 25, Tenor.Empty, 2, 2, false, false, false, false, 0.4, 0.6);
      var groups = new UniqueSequence<string>(input_.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      netting_ = new Netting(groupArray, subGroupArray, null);
    }


    protected override ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      SimulatorFlags flags = SimulatorFlags.Rate;
      var retVal = Simulations.SimulateCounterpartyCreditRisks(input.AsOf, input.Pricers, input.Names, input.Cpty,
                                                               input.CptyRec, input.TenorDates,
                                                               new[] {input.DiscountCurves[0], input.DiscountCurves[1]},
                                                               new[] {input.FxRates[0]}, null, input.CreditCurves, input.Volatilities, input.FactorLoadings, cptyDefaultTimeCorrelation_,
                                                               input.Sample, input.SimulDates, flags, input.GridSize, false);
      return retVal;
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      var pricers = new IPricer[2];
      pricers[0] = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), null, input_.DiscountCurves[0]);
      pricers[1] = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), null, input_.DiscountCurves[1]);
      id = new[] {"A", "B"};
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
    public void FxSensitivities()
    {
      BaseFxSensitivities();
    }


    [Test]
    public void RateVolSensitivities()
    {
      BaseRateVolSensitivities();
    }


    [Test]
    public void FxVolSensitivities()
    {
      BaseFxVolSensitivities();
    }


    [Test]
    public void RateFactorSensitivities()
    {
      BaseRateFactorSensitivities();
    }


    [Test]
    public void FxFactorSensitivities()
    {
      BaseFxFactorSensitivities();
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
    public void TestTowerPropertyNormalRates()
    {
      BaseTestTowerPropertyNormalRates(null);
    }

    [Test]
    public void TestTowerProperty()
    {
      BaseTestTowerProperty(null);
    }

    [Test]
    public void TestModelCalibration()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 2, 2, false, false, false, false);
      var tenors = new[] {"1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y"};
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var dc0 = input.DiscountCurves[0];
      var index0 = new InterestRateIndex(String.Concat(dc0.Ccy, "_LIBOR"), Frequency.Quarterly, dc0.Ccy, dc0.DayCount, dc0.SpotCalendar, 2);
      var volCube0 = GetCapletVolCube(asOf_, index0, dc0, capTenors_, usdCapVols_, VolatilityType.LogNormal);
      var dc1 = input.DiscountCurves[1];
      var index1 = new InterestRateIndex(String.Concat(dc1.Ccy, "_LIBOR"), Frequency.Quarterly, dc1.Ccy, dc0.DayCount, dc1.SpotCalendar, 2);
      var volCube1 = GetCapletVolCube(asOf_, index1, dc1, capTenors_, eurCapVols_, VolatilityType.LogNormal);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      var fx = input.FxRates[0];
      CCRCalibrationUtils.CalibrateSemiAnalyticFxModel(asOf_, tenors.Select(Tenor.Parse).ToArray(),
                                                                 CreateFxCurve(fx, input.DiscountCurves.First(dc => dc.Ccy == fx.FromCcy),
                                                                               input.DiscountCurves.First(dc => dc.Ccy == fx.ToCcy)), corr[0, 1],
                                                                 volCube0, volCube1, GetVolCurve(fx.Name, tenors, 0.11, 0.025), input.CreditCurves,
                                                                 GenerateBetas(rand, 2, fl, corr, 0.5),
                                                                 new[] {Tenor.Parse("5Y"), Tenor.Parse("5Y")},
                                                                 input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] {"3M"}, 0.65, 0.025)).ToArray(),
                                                                 out factors,
                                                                 out vols);
      timer.Stop();
      var result = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(result);
    }

    [Test]
    public void TestModelCalibrationNormal()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 2, 2, false, false, false, false);
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y" };
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var dc0 = input.DiscountCurves[0];
      var index0 = new InterestRateIndex(String.Concat(dc0.Ccy, "_LIBOR"), Frequency.Quarterly, dc0.Ccy, dc0.DayCount, dc0.SpotCalendar, 2);
      var volCube0 = GetCapletVolCube(asOf_, index0, dc0, capTenors_, usdCapVols_.Select(v => v*0.05).ToArray(), VolatilityType.Normal);
      var dc1 = input.DiscountCurves[1];
      var index1 = new InterestRateIndex(String.Concat(dc1.Ccy, "_LIBOR"), Frequency.Quarterly, dc1.Ccy, dc0.DayCount, dc1.SpotCalendar, 2);
      var volCube1 = GetCapletVolCube(asOf_, index1, dc1, capTenors_, eurCapVols_, VolatilityType.LogNormal);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      var fx = input.FxRates[0];
      CCRCalibrationUtils.CalibrateSemiAnalyticFxModel(asOf_, tenors.Select(Tenor.Parse).ToArray(),
                                                                 CreateFxCurve(fx, input.DiscountCurves.First(dc => dc.Ccy == fx.FromCcy),
                                                                               input.DiscountCurves.First(dc => dc.Ccy == fx.ToCcy)), corr[0, 1],
                                                                 volCube0, volCube1, GetVolCurve(fx.Name, tenors, 0.11, 0.025), input.CreditCurves,
                                                                 GenerateBetas(rand, 2, fl, corr, 0.5),
                                                                 new[] { Tenor.Parse("5Y"), Tenor.Parse("5Y") },
                                                                 input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] { "3M" }, 0.65, 0.025)).ToArray(),
                                                                 out factors,
                                                                 out vols);
      timer.Stop();
      var result = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(result);
    }


    [Test]
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping( CreateInput(2, 25, Tenor.Empty, 2, 2, false, false, false, false, 0.4, 0.6));
    }
    #endregion
  }
}
