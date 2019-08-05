//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("TestFxFuturePricer01")]
  [TestFixture("TestFxFuturePricer02")]
  [TestFixture("TestFxFuturePricer03")]
  [TestFixture("TestFxFuturePricer04")]
  [TestFixture("TestFxFuturePricer05")]
  [TestFixture("TestFxFuturePricer06")]
  public class TestFxFuturePricer : ToolkitTestBase
  {
    public TestFxFuturePricer(string name) : base(name)
    {
    }

    [OneTimeSetUp]
    public void Initialize()
    {
      PayCcy = Currency.JPY;
      RcvCcy = Currency.USD;
      var fxFuture = new FxFuture(PayCcy, RcvCcy, Expiration, 1.0, 0.01);
      var dc = new DiscountCurve(AsOf, 0.00462);
      // Fx
      var fxRate = new FxRate(AsOf, 0, PayCcy, RcvCcy, SpotFxRate, Calendar.None, Calendar.None);
      var fxCurve = new FxCurve(fxRate, new[] { fxFuture.Maturity }, new[] { FwdFxRate }, new[] { "Maturity" }, null);
      _pricer = new FxFuturePricer(fxFuture, AsOf, AsOf, RcvCcy, fxCurve, 1.0) { DiscountCurve = dc };
    }

    #region Tests

    /// <summary>
    /// ForwardFxPoints
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void ForwardFxPoints()
    {
      TestNumeric(pricer => pricer.ForwardFxPoints(PayCcy, RcvCcy));
    }

    /// <summary>
    /// Check whether SpotFxRate() round-trips
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void SpotFxRateRoundTrip()
    {
      TestNumeric(pricer => pricer.SpotFxRate(PayCcy, RcvCcy) - SpotFxRate);
    }

    /// <summary>
    /// Check whether ForwardFxRate() round-trips
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void ForwardFxRateRoundTrip()
    {
      TestNumeric(pricer=>pricer.ForwardFxRate(PayCcy, RcvCcy) - FwdFxRate);
    }

    /// <summary>
    /// Model price ???
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void ModelPrice()
    {
      TestNumeric(pricer => pricer.ModelPrice());
    }

    /// <summary>
    /// Product Pv ???
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void ProductPv()
    {
      TestNumeric(pricer => pricer.ProductPv());
    }

    #endregion Tests

    #region Utils

    private void TestNumeric(Func<FxFuturePricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((FxFuturePricer)obj));
    }

    #endregion Utils

    #region Data

    public Dt AsOf { get; set; }
    protected DiscountData DiscountData { get; set; }
    private FxFuturePricer _pricer;
    public Currency RcvCcy { get; set; }
    public Currency PayCcy { get; set; }
    public double SpotFxRate { get; set; }
    public double FwdFxRate { get; set; }
    public Dt Expiration { get; set; }

    #endregion
  }
}
