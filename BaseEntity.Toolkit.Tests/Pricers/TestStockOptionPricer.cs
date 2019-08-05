// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using log4net;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Regression tests for stock Option Pricers.
  /// </summary>
  [TestFixture("TestStockOptionPricer01")]
  [TestFixture("TestStockOptionPricer02")]
  [TestFixture("TestStockOptionPricer03")]
  [TestFixture("TestStockOptionPricer04")]
  [TestFixture("TestStockOptionPricer05")]
  [TestFixture("TestStockOptionPricer06")]
  [TestFixture("TestStockOptionPricer07")]
  [TestFixture("TestStockOptionPricer08")]
  [TestFixture("TestStockOptionPricer09")]
  [TestFixture("TestStockOptionPricer10")]
  [TestFixture("TestStockOptionPricer11")]
  [TestFixture("TestStockOptionPricer12")]
  [TestFixture("TestStockOptionPricer13")]
  [TestFixture("TestStockOptionPricer14")]
  [TestFixture("TestStockOptionPricer15", Ignore = "FB47570: Failing on Haswell")]
  public class TestStockOptionPricer : SensitivityTest
  {
    public TestStockOptionPricer(string name) : base(name)
    {}

    #region Data

    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestStockOptionPricer));

    private StockOptionPricer _pricer;

    #endregion

    #region Constructors

    [OneTimeSetUp]
    public void Initialize()
    {
      var option = new StockOption(Expiration, OptionType, OptionStyle, Strike, Rebate)
                   {
                     SettlementType = SettlementType,
                     PayoffType = PayoffType
                   };
      if (BarrierType != OptionBarrierType.None)
      {
        option.Barriers.Add(new Barrier
                            {
                              BarrierType = BarrierType,
                              MonitoringFrequency = BarrierMonitoringFreq,
                              Value = BarrierLevel
                            });
      }
      var strikeInterp = InterpScheme.FromString("WeightedTensionC1", ExtrapMethod.Const, ExtrapMethod.Const).ToInterp();
      var timeInterp = InterpScheme.FromString("Linear", ExtrapMethod.Smooth, ExtrapMethod.Smooth).ToInterp();
      var vinterp = new VolatilityPlainInterpolator(strikeInterp, timeInterp);
            var stockCurve = new StockCurve(AsOf, StockPrice, new DiscountCurve(AsOf, 0.00462), DividendYield, null);

      var volatilitySurface = VolSurfaceModel == RateModelParameters.Model.SABR ?
        CalibrateSabrVolatilitySurface(Volatility, AsOf, StockPrice, DividendYield, option.Maturity, 
        option.Strike, stockCurve.DiscountCurve, timeInterp) :
        Volatility.ToVolatilitySurface(AsOf, Settle, Expiration, Strike, null, vinterp);
      _pricer = new StockOptionPricer(option, AsOf, Settle, stockCurve, stockCurve.DiscountCurve, volatilitySurface)
                {
                  Notional = NumContracts * ContractSize
                };
      _pricer.Validate();
    }

    #endregion

    [Test, Smoke, Category("Pricing")]
    public void Consistency()
    {
      StockOptionConsistencyTests.CheckConsistency(_pricer);
    }

    [Test, Smoke, Category("Pricing")]
    public void Pv()
    {
      TestNumeric(p => p.Pv());
    }

    [Test, Smoke, Category("Pricing")]
    public void FairPrice()
    {
      TestNumeric(p => p.FairPrice());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Delta()
    {
      TestNumeric(p => p.Delta() / p.Notional);
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Gamma()
    {
      TestNumeric(p => p.Gamma() / p.Notional);
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Vega()
    {
      TestNumeric(p => p.Vega() / p.Notional);
    }

    [Test, Smoke, Category("Sensitivities")]
    public void ImpliedVol()
    {
      var fv = _pricer.FairPrice();
      var iv = _pricer.IVol(fv);
      //Can not guarantee the round-triping of implied vol and fv when american-style option returns the intrinsic value
      if (OptionStyle == OptionStyle.American && fv <= Math.Max((StockPrice - Strike) * (OptionType == OptionType.Call ? 1.0 : -1.0), 0.0))
        return;
      Assert.AreEqual(_pricer.Volatility, iv, 1E-5);
      TestNumeric(_pricer, "", p => iv);
    }

    #region Properties

    public Dt AsOf { get; set; }
    public Dt Settle { get; set; }
    public Dt Effective { get; set; }
    public Dt Expiration { get; set; }
    public double Strike { get; set; }
    public double Rebate { get; set; }
    public OptionType OptionType { get; set; }
    public OptionStyle OptionStyle { get; set; }
    public SettlementType SettlementType { get; set; }
    public OptionPayoffType PayoffType { get; set; }
    public double StockPrice { get; set; }
    public string[,] Volatility { get; set; }
    public int ContractSize { get; set; }
    public int NumContracts { get; set; }
    public double DividendYield { get; set; }
    public Frequency BarrierMonitoringFreq { get; set; }
    public OptionBarrierType BarrierType { get; set; }
    public double BarrierLevel { get; set; }
    public BaseEntity.Toolkit.Models.RateModelParameters.Model VolSurfaceModel { get; set; }

    #endregion

    #region Utilities

    private void TestNumeric(Func<StockOptionPricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((StockOptionPricer)obj));
    }

    private static CalibratedVolatilitySurface CalibrateSabrVolatilitySurface(
      string[,] data,
      Dt asOf, double stockPrice, double dvdYield, Dt maturity, double strike,
      DiscountCurve discountCurve,
      Interp timeInterp)
    {
      PlainVolatilityTenor[] tenors;

      if (data.Length > 1)
      {
        int rows = data.GetLength(0), cols = data.GetLength(1);
        if (cols == 3)
        {
          var tenorDict = new Dictionary<Dt, PlainVolatilityTenor>();
          foreach (var row in Enumerable.Range(0, rows))
          {
            var date = Dt.FromStr(data[row, 0]);
            if (!tenorDict.ContainsKey(date))
            {
              tenorDict.Add(date, new PlainVolatilityTenor(data[row, 0], date)
              {
                Strikes = new List<double>(),
                Volatilities = new List<double>()
              });
            }
            tenorDict[date].Strikes.Add(ToDouble(data[row, 1]));
            tenorDict[date].QuoteValues.Add(ToDouble(data[row, 2]));
          }
          tenors = tenorDict.Values.ToArray();
        }
        else
        {
          throw new Exception("Invalid volatility data.");
        }
      }
      else
      {
        var name = Tenor.FromDateInterval(asOf, maturity).ToString();
        double vol = data.Length == 1 ? ToDouble(data[0, 0]) : 0.1;
        tenors = new[]
                 {
                   new PlainVolatilityTenor(name, maturity)
                   {
                     Strikes = new List<double>() {strike},
                     Volatilities = new List<double>() {vol}
                   }
                 };
      }
      var expirations = tenors.Select(t => t.Maturity).Distinct().OrderBy(m => m).ToList();
      var strikes = tenors.SelectMany(t => t.Strikes).Distinct().OrderBy(k => k).ToList();
      var volQuotes = new double[strikes.Count, expirations.Count];
      for (var i = 0; i < tenors.Length; i++)
      {
        var volTenor = (PlainVolatilityTenor)tenors[i];
        for (var j = 0; j < volTenor.Strikes.Count; j++)
        {
          volQuotes[strikes.IndexOf(volTenor.Strikes[j]), i] = volTenor.QuoteValues[j];
        }
      }
      var stockFwdPrices = SabrSurfaceCalibrator.CalculateAtmForwards(asOf, expirations.ToArray(), stockPrice, dvdYield, discountCurve);
      var volatilitySurface = SabrSurfaceCalibrator.CreateSurfaceFromForwards(asOf, expirations.ToArray(), stockFwdPrices, strikes.ToArray(), volQuotes, null, null,
                                                                   timeInterp);
      return volatilitySurface;
    }

    private static double ToDouble(string data)
    {
      double scale = 1.0;
      if (data.EndsWith("%"))
      {
        scale = 100;
        data = data.TrimEnd('%');
      }
      return Double.Parse(data) / scale;
    }

    #endregion
  }
}