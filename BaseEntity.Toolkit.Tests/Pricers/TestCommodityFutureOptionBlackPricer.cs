// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using NUnit.Framework;
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
  /// Regression tests for Commodity Future Option Pricers.
  /// </summary>
  [TestFixture("TestCommodityFutureOptionBlackPricer1")]
  [TestFixture("TestCommodityFutureOptionBlackPricer2")]
  [TestFixture("TestCommodityFutureOptionBlackPricer3")]
  public class TestCommodityFutureOptionBlackPricer : SensitivityTest
  {
    public TestCommodityFutureOptionBlackPricer(string name) : base(name)
    {}

    #region Data

    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestCommodityFutureOptionBlackPricer));

    private CommodityFutureOptionBlackPricer _pricer;

    #endregion

    #region Constructors

    [OneTimeSetUp]
    public void Initialize()
    {
      var future = new CommodityFuture(Expiration, ContractSize);
      var option = new CommodityFutureOption(future, Expiration, OptionType, OptionStyle, Strike);
      var strikeInterp = InterpScheme.FromString("WeightedTensionC1", ExtrapMethod.Const, ExtrapMethod.Const).ToInterp();
      var timeInterp = InterpScheme.FromString("Linear", ExtrapMethod.Smooth, ExtrapMethod.Smooth).ToInterp();
      var vinterp = new VolatilityPlainInterpolator(strikeInterp, timeInterp);
      var volatilitySurface = Volatility.ToVolatilitySurface(AsOf, Settle, Expiration, Strike, null, vinterp);
      // *** TBD COMMODITY: Create commodityCurve from futures price. RTD Feb'13
      var discountCurve = new DiscountCurve(AsOf, 0.0);
      _pricer = new CommodityFutureOptionBlackPricer(option, AsOf, Settle, discountCurve, null, volatilitySurface, NumContracts)
                {QuotedFuturePrice = FuturesPrice};
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
      TestNumeric(p => p.IVol(p.FairPrice()));
    }

    #region Properties

    public Dt AsOf { get; set; }
    public Dt Settle { get; set; }
    public Dt Effective { get; set; }
    public Dt Expiration { get; set; }
    public double Strike { get; set; }
    public OptionType OptionType { get; set; }
    public OptionStyle OptionStyle { get; set; }
    public double FuturesPrice { get; set; }
    public string[,] Volatility { get; set; }
    public int ContractSize { get; set; }
    public int NumContracts { get; set; }

    #endregion

    #region Utilities

    private void TestNumeric(Func<CommodityFutureOptionBlackPricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((CommodityFutureOptionBlackPricer)obj));
    }

    #endregion
  }
}