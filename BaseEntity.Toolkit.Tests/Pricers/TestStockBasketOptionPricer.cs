// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using log4net;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Regression tests for stock basket Option Pricers.
  /// </summary>
  [TestFixture("01"), TestFixture("02")]
  public class TestStockBasketOptionPricer : SensitivityTest
  {
    public TestStockBasketOptionPricer(string name) : base(name)
    {}

    #region Data

    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestStockBasketOptionPricer));

    private StockBasketOptionPricer _pricer;

    #endregion

    #region Constructors

    [OneTimeSetUp]
    public void Initialize()
    {
      List<double> amts = new List<double>();
      List<double> stockPrices = new List<double>();
      GetBasketInfo(Basket, ref amts, ref stockPrices);
      var dividendYields = ArrayUtil.NewArray(amts.Count, 0.0);

      var option = new StockBasketOption(Dt.Empty, Currency.USD, amts.ToArray(), Expiration, OptionType, OptionStyle, Strike);
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
      var volatilitySurface = Volatility.ToVolatilitySurface(AsOf, Settle, Expiration, Strike, null, vinterp);
      var dc = new DiscountCurve(AsOf, 0.00462);
      var volSurfaces = ArrayUtil.NewArray(amts.Count, volatilitySurface);
      var correlations = new double[amts.Count, amts.Count];
      for (int i = 0; i < amts.Count; ++i)
        for (int j = 0; j < amts.Count; ++j)
          correlations[i, j] = 1.0;
      _pricer = new StockBasketOptionPricer(option, AsOf, Settle, stockPrices.ToArray(), dc, dividendYields, null, volSurfaces, correlations)
                {
                  Notional = NumContracts * ContractSize
                };
      _pricer.Validate();
    }

    #endregion

    [Test, Smoke, Category("Pricing")]
    public void Pv()
    {
      TestNumeric(p => p.Pv());
    }

    [Test, Smoke, Category("Pricing")]
    public void FairPrice()
    {
      TestNumeric(p => p.FairValue());
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
      TestNumeric(p => p.IVol(p.FairValue()));
    }

    #region Properties

    public Dt AsOf { get; set; }
    public Dt Settle { get; set; }
    public Dt Effective { get; set; }
    public Dt Expiration { get; set; }
    public double Strike { get; set; }
    public OptionType OptionType { get; set; }
    public OptionStyle OptionStyle { get; set; }
    public double StockPrice { get; set; }
    public string[,] Volatility { get; set; }
    public int ContractSize { get; set; }
    public int NumContracts { get; set; }

    public Frequency BarrierMonitoringFreq { get; set; }
    public OptionBarrierType BarrierType { get; set; }
    public double BarrierLevel { get; set; }

    public string[,] Basket { get; set; }

    #endregion

    #region Utilities

    private void TestNumeric(Func<StockBasketOptionPricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((StockBasketOptionPricer)obj));
    }

    private void GetBasketInfo(string[,] data, ref List<double> amts, ref List<double> prices )
    {

      if (data.Length > 1)
      {
        int rows = data.GetLength(0), cols = data.GetLength(1);
        if (cols == 3)
        {
          foreach (var row in Enumerable.Range(0, rows))
          {
            var name = data[row, 0];
            amts.Add(Double.Parse(data[row, 1]));
            prices.Add(Double.Parse(data[row, 2]));
          }
        }
        else
        {
          throw new Exception("Invalid Basket data.");
        }
      }
      else
      {
        throw new Exception("Invalid Basket data.");
      }

    }

    #endregion
  }
}