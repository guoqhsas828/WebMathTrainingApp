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
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("TestBondFutureOptionBlackPricer01")]
  [TestFixture("TestBondFutureOptionBlackPricer02")]
  [TestFixture("TestBondFutureOptionBlackPricer03")]
  [TestFixture("TestBondFutureOptionBlackPricer04")]
  [TestFixture("TestBondFutureOptionBlackPricer05")]
  public class TestBondFutureOptionBlackPricer : ToolkitTestBase
  {

    public TestBondFutureOptionBlackPricer(string name) : base(name)
    {      
    }

    /// <summary>
    /// Initialize the pricer
    /// </summary>
    [OneTimeSetUp]
     public void Initialize()
    {
      var bondFuture = new BondFuture( Maturity, 0.06, 100000.0, 0.01);
      var option = new BondFutureOption(bondFuture, Expiration, Type, Style, Strike);
      DiscountData = GetDiscountData(AsOf);
      var ctdBond = new Bond(Dt.Add(CtdBondMaturity, -15, TimeUnit.Years), CtdBondMaturity, Currency.USD, BondType.USGovt, 0.025, DayCount.Thirty360, CycleRule.None, Frequency.SemiAnnual,
                             BDConvention.Modified, Calendar.NYB);
      var dc = DiscountData.GetDiscountCurve();
      _pricer = new BondFutureOptionBlackPricer(option, AsOf, AsOf, FuturesPrice, dc, CalibratedVolatilitySurface.FromFlatVolatility(AsOf, FlatVol), ctdBond, CtdMarketQuote, QuotingConvention.FlatPrice, ConversionFactor, 1.0);
     
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
      TestNumeric(pricer => pricer.Delta()/pricer.Notional);
    }

    /// <summary>
    /// Vega
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Vega()
    {
      TestNumeric(pricer => pricer.Vega() / pricer.Notional);
    }

    /// <summary>
    /// Rho
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Rho()
    {
      TestNumeric(pricer => pricer.Rho() / pricer.Notional);
    }

    /// <summary>
    /// DualDelta
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void DualDelta()
    {
      TestNumeric(pricer => pricer.DualDelta() / pricer.Notional);
    }

    /// <summary>
    /// Gamma
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Gamma()
    {
      TestNumeric(pricer => pricer.Gamma() / pricer.Notional);
    }

    /// <summary>
    /// Theta
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Theta()
    {
      TestNumeric(pricer => pricer.Theta() / pricer.Notional);
    }

    /// <summary>
    /// Charm
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Charm()
    {
      TestNumeric(pricer => pricer.Charm() / pricer.Notional);
    }

    /// <summary>
    /// Color
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void Color()
    {
      TestNumeric(pricer => pricer.Color() / pricer.Notional);
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
      TestNumeric(pricer => pricer.Pv() / pricer.Notional);
    }

    /// <summary>
    /// Ctd Bond Pv01
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void CtdPv01()
    {
      TestNumeric(pricer => pricer.CtdPv01() / pricer.Notional);
    }

    /// <summary>
    /// Ctd Bond Repo01
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void CtdRepo01()
    {
      TestNumeric(pricer => pricer.CtdRepo01(DayCount.Actual365Fixed) / pricer.Notional);
    }

    /// <summary>
    /// Ctd Bond Price01
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void CtdPrice01()
    {
      TestNumeric(pricer => pricer.CtdPrice01() / pricer.Notional);
    }
    #endregion Tests

    #region Utils

    private void TestNumeric(Func<BondFutureOptionBlackPricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((BondFutureOptionBlackPricer)obj));
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
    private BondFutureOptionBlackPricer _pricer;
    public Dt CtdBondMaturity { get; set; }
    public Dt Maturity { get; set; } // Futures maturity 
    public Dt Expiration { get; set; } // Option expiration
    public double Strike { get; set; }
    public OptionType Type { get; set; }
    public OptionStyle Style { get; set; }
    public double FuturesPrice { get; set; }
    public double FlatVol { get; set; }
    public double CtdMarketQuote { get; set; }
    public double ConversionFactor { get; set; }

    #endregion
  }
}
