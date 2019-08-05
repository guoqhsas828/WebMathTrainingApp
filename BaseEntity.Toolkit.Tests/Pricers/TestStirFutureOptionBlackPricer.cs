//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using NUnit.Framework;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("TestStirFutureOptionBlackPricer01")]
  [TestFixture("TestStirFutureOptionBlackPricer02")]
  public class TestStirFutureOptionBlackPricer : ToolkitTestBase
  {

    public TestStirFutureOptionBlackPricer(string name) : base(name)
    {
    }

    /// <summary>
    /// Initialize the pricer
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      var stirFuture = StandardProductTermsUtil.GetStandardFuture<StirFuture>("ED", "CME", Dt.Today(), "Z16") as StirFuture;
      var option = new StirFutureOption(
        stirFuture,                             // Stir futures contract
        Expiration,                             // Option Expiration
        Type,                                   // Call option 
        Style,                                  // American option
        Strike                                  // Strike is 95.0
      );

      DiscountData = GetDiscountData(AsOf);
      var dc = DiscountData.GetDiscountCurve();
      _pricer = new StirFutureOptionBlackPricer(option, AsOf, AsOf, FuturesPrice, dc, dc, CalibratedVolatilitySurface.FromFlatVolatility(AsOf, FlatVol), 1.0);

    }
    #region Tests

    /// <summary>
    /// Fair Price
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void FairPrice()
    {
      TestNumeric(pricer => pricer.FairPrice());
    }

    /// <summary>
    /// Delta
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Delta()
    {
      TestNumeric(pricer => pricer.Delta());
    }

    /// <summary>
    /// Vega
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Vega()
    {
      TestNumeric(pricer => pricer.Vega());
    }

    /// <summary>
    /// Rho
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Rho()
    {
      TestNumeric(pricer => pricer.Rho());
    }

    /// <summary>
    /// DualDelta
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void DualDelta()
    {
      TestNumeric(pricer => pricer.DualDelta());
    }

    /// <summary>
    /// Gamma
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Gamma()
    {
      TestNumeric(pricer => pricer.Gamma());
    }

    /// <summary>
    /// Theta
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Theta()
    {
      TestNumeric(pricer => pricer.Theta());
    }

    /// <summary>
    /// Charm
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Charm()
    {
      TestNumeric(pricer => pricer.Charm());
    }

    /// <summary>
    /// Color
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Color()
    {
      TestNumeric(pricer => pricer.Color());
    }

    /// <summary>
    /// IVol
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void IVol()
    {
      TestNumeric(pricer => pricer.IVol(pricer.FairPrice()) - FlatVol);
    }

    /// <summary>
    /// Gearing
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Gearing()
    {
      TestNumeric(pricer => pricer.Gearing());
    }

    /// <summary>
    /// Product Pv ???
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void ProductPv()
    {
      TestNumeric(pricer => pricer.Pv());
    }

    #endregion Tests

    #region Utils

    private void TestNumeric(Func<StirFutureOptionBlackPricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((StirFutureOptionBlackPricer)obj));
    }

    /// <exclude></exclude>
    internal static DiscountData GetDiscountData(Dt asOf)
    {
      return new DiscountData
      {
        AsOf = asOf.ToStr("%D"),
        Bootst = new DiscountData.Bootstrap
        {
          MmDayCount = DayCount.Actual360,
          MmTenors = new[] { "1M", "2M", "3M", "6M", "9M" },
          MmRates = new[] { 0.011, 0.012, 0.013, 0.016, 0.019 },
          SwapDayCount = DayCount.Actual360,
          SwapFrequency = Frequency.SemiAnnual,
          SwapInterp = InterpMethod.Cubic,
          SwapExtrap = ExtrapMethod.Const,
          // of fixed leg of swap
          SwapTenors =
            new[] { "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "30Y" },
          SwapRates =
            new[] { 0.022, 0.023, 0.024, 0.025, 0.026, 0.027, 0.028, 0.029, 0.030, 0.035, 0.04 }
        },
        Category = "Empty",
        // null works badly
        Name = "MyDiscountCurve" // ditto
      };
    }

    #endregion Utils

    #region Data

    public Dt AsOf { get; set; }
    protected DiscountData DiscountData { get; set; }
    private StirFutureOptionBlackPricer _pricer;
    public Dt Maturity { get; set; } // Futures maturity
    public Dt Expiration { get; set; } // Option expiration
    public double FuturesPrice { get; set; }
    public double Strike { get; set; }
    public OptionType Type { get; set; }
    public OptionStyle Style { get; set; }
    public double FlatVol { get; set; }

    #endregion
  }
}
