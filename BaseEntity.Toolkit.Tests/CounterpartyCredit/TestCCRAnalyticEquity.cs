//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRAnalyticEquity : TestCCRBase
  {
    #region Init
    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = CreateInput(2, 25, Tenor.Empty, 1, 2, false, false, true, false, 0.4, 0.6);
      var groups = new UniqueSequence<string>(input_.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      netting_ = new Netting(groupArray, subGroupArray, null);
    }

    

    protected override ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      SimulatorFlags flags = SimulatorFlags.Spot;
      return Simulations.SimulateCounterpartyCreditRisks(input.AsOf, input.Pricers, input.Names, input.Cpty,
                                                         input.CptyRec, input.TenorDates,
                                                         new[] {input.DiscountCurves[0]}, null, input.FwdCurves, input.CreditCurves, input.Volatilities,
                                                         input.FactorLoadings, cptyDefaultTimeCorrelation_, input.Sample, input.SimulDates,
                                                         flags, input.GridSize, false);
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      id = null;
      return null;
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
    public void SpotSensitivities()
    {
      BaseSpotSensitivities();
    }


    [Test]
    public void SpotVolSensitivities()
    {
      BaseSpotVolSensitivities();
    }


    [Test]
    public void SpotFactorSensitivities()
    {
      BaseSpotFactorSensitivities();
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
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 25, Tenor.Empty, 1, 0, false, false, true, false);
      MakeNormalVols(input);
      var stockForwardCurve = (StockCurve)input.FwdCurves.First(c => c is StockCurve);
      CCRCalibrationUtils.CalibrateSemiAnalyticSpotVolatility(asOf_, stockForwardCurve, new VolatilityCurve(asOf_, stockVol_), input.Volatilities,
                                                              input.FactorLoadings, null, null);
      var stockOptionPricer = CreateStockOptionPricer(asOf_, stockForwardCurve, stockForwardCurve.Interpolate(Dt.Add(asOf_, "5Y")), "5Y", OptionType.Call,
                                                      new VolatilityCurve(asOf_, stockVol_), 1.0);
      input.Pricers = new IPricer[] {stockOptionPricer};
      input.Names = new[] {"OptionPricer"};
      BaseTestTowerPropertyNormalRates(input);
    }

    [Test]
    public void TestTowerProperty()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 25, Tenor.Empty, 1, 0, false, false, true, false);
      var stockForwardCurve = (StockCurve)input.FwdCurves.First(c => c is StockCurve);
      CCRCalibrationUtils.CalibrateSemiAnalyticSpotVolatility(asOf_, stockForwardCurve, new VolatilityCurve(asOf_, stockVol_), input.Volatilities,
                                                              input.FactorLoadings, null, null);
      var stockOptionPricer = CreateStockOptionPricer(asOf_, stockForwardCurve, stockForwardCurve.Interpolate(Dt.Add(asOf_, "5Y")), "5Y", OptionType.Call,
                                                      new VolatilityCurve(asOf_, stockVol_), 1.0);
      input.Pricers = new IPricer[] {stockOptionPricer};
      input.Names = new[] {"OptionPricer"};
      BaseTestTowerProperty(input);
    }

    [Test]
    public void TestModelCalibration()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 1, 0, false, false, true, false);

      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var dc0 = input.DiscountCurves[0];
      var index0 = new InterestRateIndex(String.Concat(dc0.Ccy, "_LIBOR"), Frequency.Quarterly, dc0.Ccy, dc0.DayCount, dc0.SpotCalendar, 2);
      var volCube0 = GetCapletVolCube(asOf_, index0, dc0, capTenors_, usdCapVols_, VolatilityType.LogNormal);
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      var spot = (StockCurve)input.FwdCurves.First(c => c is StockCurve);
      var volCurve = GetVolCurve(spot.Name, tenors_, 0.3, 0.025);
      CCRCalibrationUtils.CalibrateSemiAnalyticSpotModel(asOf_, tenors_.Select(Tenor.Parse).ToArray(), spot, volCube0, volCurve,
                                                         input.CreditCurves, GenerateBetas(rand, 2, fl, corr, 0.5),
                                                         new[] {Tenor.Parse("5Y"), Tenor.Parse("5Y")},
                                                         input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] {"3M"}, 0.65, 0.025)).ToArray(),
                                                         out factors, out vols);
      timer.Stop();
      var result = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(result);
    }

    [Test]
    public void TestModelCalibrationNormal()
    {
      var timer = new Timer();
      timer.Start();
      var input = CreateInput(2, 0, Tenor.Empty, 1, 0, false, false, true, false);
      VolatilityCollection vols;
      FactorLoadingCollection factors;
      var dc0 = input.DiscountCurves[0];
      var index0 = new InterestRateIndex(String.Concat(dc0.Ccy, "_LIBOR"), Frequency.Quarterly, dc0.Ccy, dc0.DayCount, dc0.SpotCalendar, 2);
      var volCube0 = GetCapletVolCube(asOf_, index0, dc0, capTenors_, usdCapVols_.Select(v => v * 0.05).ToArray(), VolatilityType.Normal);
      var rand = new Random(5);
      var fl = GenerateFactors(rand, 2, 2, 1.0);
      var corr = GenerateCorrelationMatrix(fl);
      var spot = (StockCurve)input.FwdCurves.First(c => c is StockCurve);
      var volCurve = GetVolCurve(spot.Name, tenors_, 0.3, 0.025);
      CCRCalibrationUtils.CalibrateSemiAnalyticSpotModel(asOf_, tenors_.Select(Tenor.Parse).ToArray(), spot, volCube0, volCurve,
                                                         input.CreditCurves, GenerateBetas(rand, 2, fl, corr, 0.5),
                                                         new[] { Tenor.Parse("5Y"), Tenor.Parse("5Y") },
                                                         input.CreditCurves.Select(c => GetVolCurve(c.Name, new[] { "3M" }, 0.65, 0.025)).ToArray(),
                                                         out factors, out vols);
      timer.Stop();
      var result = ToCalibrationResultData(factors, vols, timer.Elapsed);
      MatchExpects(result);
    }



    [Test]
    public void TestBumpingVsSensitivities()
    {
      BaseTestSensitivityVsBumping(CreateInput(2, 25, Tenor.Empty, 1, 2, false, false, true, false, 0.4, 0.6));
    }

    #endregion

   
  }
}
