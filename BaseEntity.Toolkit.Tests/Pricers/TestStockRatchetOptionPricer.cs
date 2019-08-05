// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
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
  /// Regression tests for stock ratchet Option Pricers.
  /// </summary>
  //[TestFixture]
  [TestFixture("TestStockRatchetOptionPricer1")]
  [TestFixture("TestStockRatchetOptionPricer2")]
  [TestFixture("TestStockRatchetOptionPricer3")]
  [TestFixture("TestStockRatchetOptionPricer4")]
  [TestFixture("TestStockRatchetOptionPricer5")]

  public class TestStockRatchetOptionPricer : SensitivityTest
  {
    public TestStockRatchetOptionPricer(string name) : base(name) { }

    #region Data

    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestStockRatchetOptionPricer));

    private StockRatchetOptionPricer _pricer;
    private StockOptionPricer _vanillaPricer;

    #endregion

    #region Constructors

    [OneTimeSetUp]
    public void Initialize()
    {
      var option = new StockOption(Expiration, OptionType, OptionStyle, EffectiveStrike);
      var resetDates = RemainingReset > 0 ? new Dt[] { Dt.Add(AsOf, -7, TimeUnit.Days), Dt.Add(AsOf, "1Y") }
        : new Dt[] { Dt.Add(AsOf, -7, TimeUnit.Days) };
      var strikeResets = new RateResets();
      strikeResets.Add(new RateReset(Dt.Empty, Strike));
      strikeResets.Add(new RateReset(resetDates[0], EffectiveStrike));
      var ratchetOption = new StockRatchetOption(Dt.Empty, Currency.USD, Expiration, resetDates, OptionType, EffectiveStrike)
      { PayoutOnResetDate = PayoutOnResetDate > 0 };
      var strikeInterp = InterpScheme.FromString("WeightedTensionC1", ExtrapMethod.Const, ExtrapMethod.Const).ToInterp();
      var timeInterp = InterpScheme.FromString("Linear", ExtrapMethod.Smooth, ExtrapMethod.Smooth).ToInterp();
      var vinterp = new VolatilityPlainInterpolator(strikeInterp, timeInterp);
      var volatilitySurface = Volatility.ToVolatilitySurface(AsOf, Settle, Expiration, Strike, null, vinterp);
      var yield = 0.0146;
      var stockCurve = new StockCurve(AsOf, StockPrice, new DiscountCurve(AsOf, 0.00462), yield, null);
      stockCurve.ImpliedYieldCurve.SetRelativeTimeRate(yield);
      var dc = new DiscountCurve(AsOf, 0.00462);
      _pricer = new StockRatchetOptionPricer(ratchetOption, AsOf, Settle, StockPrice, dc, yield, null, volatilitySurface)
      {
        Notional = NumContracts * ContractSize,
        StrikeResets = strikeResets
      };

      _vanillaPricer = new StockOptionPricer(option, AsOf, Settle, stockCurve, stockCurve.DiscountCurve, volatilitySurface)
      {
        Notional = NumContracts * ContractSize
      };
      _pricer.Validate();
      _vanillaPricer.Validate();
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

    [Test, Smoke, Category("Pricing")]
    public void RatchetVsVanilla()
    {
      var vanillaPv = _vanillaPricer.FairPrice();
      var pv = _pricer.FairValue();

      if (RemainingReset <= 0)
      {

        if (PayoutOnResetDate > 0)
        {
          Assert.AreEqual(vanillaPv, pv, 1e-6, "Ratchet vs Vanilla Pv");
        }
        else
        {
          Assert.Less(vanillaPv, pv, "Ratchet vs Vanilla Pv");
        }
      }
      else
      {
        Assert.AreNotEqual(vanillaPv, pv, "Ratchet vs Vanilla Pv");
      }

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
    public double EffectiveStrike { get; set; }
    public int PayoutOnResetDate { get; set; }
    public int RemainingReset { get; set; }

    #endregion

    #region Utilities

    private void TestNumeric(Func<StockRatchetOptionPricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((StockRatchetOptionPricer)obj));
    }

    #endregion
  }
}