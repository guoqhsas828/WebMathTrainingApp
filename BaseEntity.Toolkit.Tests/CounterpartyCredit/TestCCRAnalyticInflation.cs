//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using NUnit.Framework;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRAnalyticInflation : TestCCRBase
  {
    #region Init
    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateInput(2, 25, Tenor.Empty, 1, 2, false, true, false, false, 0.4, 0.6);
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
                                                         input.FactorLoadings, cptyDefaultTimeCorrelation_, input.Sample, input.SimulDates, flags, input.GridSize, false);
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      InflationIndex cpi = CreateInflationIndex(currencies_[0], calendar_[0], dayCounts_[0], roll_[0]);
      var product = new InflationBond(asOf_, Dt.Add(asOf_, "10Y"), currencies_[0], BondType.USTBill, 0.0, DayCount.Actual360, CycleRule.None, Frequency.None, BDConvention.Following,
        calendar_[0], cpi, spotInflation_, Tenor.Parse("3M"));
      var inflCurve = input_.FwdCurves.FirstOrDefault(c => c is InflationCurve) as InflationCurve;
      var pricer = new InflationBondPricer(product, asOf_, asOf_, 1.0, input_.DiscountCurves[0], cpi, inflCurve, null, null);
      var pricers = new IPricer[] { pricer };
      id = new[] { "A" };
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
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping(CreateInput(2, 25, Tenor.Empty, 1, 2, false, true, false, false, 0.4, 0.6));
    }

    [Test]
    public void TestModelCalibration()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 1, 2, false, true, false, false);
      var tenors = new[] {"1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y"};
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var dc = input.DiscountCurves[0];
      var index = new InterestRateIndex(String.Concat(dc.Ccy, "_LIBOR"), Frequency.Quarterly, dc.Ccy, dc.DayCount, dc.SpotCalendar, 2);
      var volCube = GetCapletVolCube(asOf_, index, dc, capTenors_, usdCapVols_, VolatilityType.LogNormal);
      var cpi = input.FwdCurves[0];
      var cpiVol = GetVolCurve(cpi.Name, tenors, 0.05, 0.005);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      CCRCalibrationUtils.CalibrateSemiAnalyticForwardModel(asOf_, tenors.Select(Tenor.Parse).ToArray(), dc, cpi, corr[0, 1], volCube,
                                                                      cpiVol, input.CreditCurves, GenerateBetas(rand, 2, fl, corr, 0.5),
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
      var input = CreateInput(2, 0, Tenor.Empty, 1, 2, false, true, false, false);
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y" };
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var dc = input.DiscountCurves[0];
      var index = new InterestRateIndex(String.Concat(dc.Ccy, "_LIBOR"), Frequency.Quarterly, dc.Ccy, dc.DayCount, dc.SpotCalendar, 2);
      var volCube = GetCapletVolCube(asOf_, index, dc, capTenors_, usdCapVols_.Select(v=>0.05*v).ToArray(), VolatilityType.Normal);
      var cpi = input.FwdCurves[0];
      var cpiVol = GetVolCurve(cpi.Name, tenors, 0.05, 0.005);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      CCRCalibrationUtils.CalibrateSemiAnalyticForwardModel(asOf_, tenors.Select(Tenor.Parse).ToArray(), dc, cpi, corr[0, 1], volCube,
                                                                      cpiVol, input.CreditCurves, GenerateBetas(rand, 2, fl, corr, 0.5),
                                                                      new[] { Tenor.Parse("5Y"), Tenor.Parse("5Y") },
                                                                      input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] { "3M" }, 0.65, 0.025)).ToArray(),
                                                                      out factors,
                                                                      out vols);
      timer.Stop();
      var result = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(result);
    }

    #endregion
  }
}