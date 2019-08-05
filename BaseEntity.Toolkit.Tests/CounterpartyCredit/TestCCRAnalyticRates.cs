//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Ccr;
using NUnit.Framework;
using BaseEntity.Toolkit.Util;


namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRAnalyticRates : TestCCRBase
  {
    #region Init
    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateInput(2, 25, Tenor.Empty, 1, 2, false, false, false, false, 0.4, 0.6);
      var groups = new UniqueSequence<string>(input_.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      netting_ = new Netting(groupArray, subGroupArray, null);
    }

    protected override ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      SimulatorFlags flags = SimulatorFlags.Rate;
      return Simulations.SimulateCounterpartyCreditRisks(input.AsOf, input.Pricers, input.Names, input.Cpty,
                                                         input.CptyRec, input.TenorDates, new[] {input.DiscountCurves[0]}, null, null, input.CreditCurves, input.Volatilities,
                                                         input.FactorLoadings, cptyDefaultTimeCorrelation_, input.Sample, input.SimulDates, flags,
                                                         input.GridSize, false);
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      var pricers = new IPricer[1];
      pricers[0] = new TestBondPricer(asOf_, asOf_, Dt.Add(asOf_, "10Y"), null, input_.DiscountCurves[0]);
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
      BaseTestSensitivityVsBumping(CreateInput(2, 1, Tenor.Empty, 1, 2, false, false, false, false, 0.4, 0.6));
    }

    [Test]
    public void TestCapletVolConversionFromSwaptionSurface()
    {
      var timer = new Timer();
      timer.Start();
      var dc = new DiscountCurve(asOf_, 0.03);
      var volatilitySurface = GetMarketSwaptionVolatility(asOf_, dc);
      var tenors = new[] {"1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y"};
      var ten = Array.ConvertAll(tenors, Tenor.Parse);
      var correlation = new FactorLoadingCollection(new[] {"factor0", "factor1"}, ten);
      var vols = new VolatilityCollection(ten);
      var fl = GenerateFactors(new Random(3), tenors.Length, 2, 1.0);
      correlation.AddFactors(dc, fl);
      CCRCalibrationUtils.FromVolatilityObject(dc, volatilitySurface, true, correlation, vols);
      timer.Stop();
      var results = ToCalibrationResultData(correlation, vols, timer.Elapsed);
      MatchExpects(results);
    }



    [Test]
    public void TestModelCalibration()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 1, 2, false, false, false, false);
      var tenors = new[] {"1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y"};
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var dc = input.DiscountCurves[0];
      var index = new InterestRateIndex(String.Concat(dc.Ccy, "_LIBOR"), Frequency.Quarterly, dc.Ccy, dc.DayCount, dc.SpotCalendar, 2);
      var volCube = GetCapletVolCube(asOf_, index, dc, capTenors_, usdCapVols_, VolatilityType.LogNormal);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      CCRCalibrationUtils.CalibrateSemiAnalyticIrModel(asOf_, tenors.Select(Tenor.Parse).ToArray(), dc, new[] {Tenor.Parse("1Y"), Tenor.Parse("10Y")},
                                                                 volCube,
                                                                 corr[0, 1], input.CreditCurves, GenerateBetas(rand, 2, fl, corr, 0.5),
                                                                 new[] {Tenor.Parse("5Y"), Tenor.Parse("5Y")},
                                                                 input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] {"3M"}, 0.65, 0.025)).ToArray(), true,
                                                                 out factors,
                                                                 out vols);
      timer.Stop();
      var results = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(results);
    }

    [Test]
    public void TestModelCalibrationNormal()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 1, 2, false, false, false, false);
      var tenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y" };
      input.TenorDates = tenors.Select(t => Dt.Add(asOf_, t)).ToArray();
      var dc = input.DiscountCurves[0];
      var index = new InterestRateIndex(String.Concat(dc.Ccy, "_LIBOR"), Frequency.Quarterly, dc.Ccy, dc.DayCount, dc.SpotCalendar, 2);
      var volCube = GetCapletVolCube(asOf_, index, dc, capTenors_, usdCapVols_.Select(v=>v*0.05).ToArray(), VolatilityType.Normal);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      CCRCalibrationUtils.CalibrateSemiAnalyticIrModel(asOf_, tenors.Select(Tenor.Parse).ToArray(), dc, new[] { Tenor.Parse("1Y"), Tenor.Parse("10Y") },
                                                                 volCube, corr[0, 1], input.CreditCurves, GenerateBetas(rand, 2, fl, corr, 0.5),
                                                                 new[] { Tenor.Parse("5Y"), Tenor.Parse("5Y") },
                                                                 input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] { "3M" }, 0.65, 0.025)).ToArray(), true,
                                                                 out factors,
                                                                 out vols);
      timer.Stop();
      var results = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(results);
    }


    #endregion
  }
}