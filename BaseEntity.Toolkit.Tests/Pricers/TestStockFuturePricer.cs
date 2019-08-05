//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("01")]
  [TestFixture("02")]
  [TestFixture("03")]
  public class TestStockFuturePricer : ToolkitTestBase
  {
    #region Set up

    public TestStockFuturePricer(string name) : base(name)
    {
    }

    [OneTimeSetUp]
    public void Initialize()
    {
      var stockFuture = new StockFuture(Expiration, 1.0, 0.01);
      var dc = new DiscountCurve(AsOf, 0.00462);
      _pricer = new StockFuturePricer(stockFuture, AsOf, AsOf, 0.00462, DividendYield, SpotPrice, 1.0);
      var stockCurve = new StockCurve(AsOf, SpotPrice, dc, DividendYield, null);
      _ccrPricer = new StockFuturePricer(stockFuture, AsOf, AsOf, stockCurve, 1.0);
    }

    #endregion

    #region Tests

    /// <summary>
    /// Compare pv between different construction of pricers.
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void ToolkitVsCcr()
    {
      Assert.AreEqual(0.0, _pricer.Pv() - _ccrPricer.Pv(), 1E-9, "Pv expected to match between different constructors");
    }

    /// <summary>
    /// Check whether SpotPrice() round-trips
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void SpotPriceRoundTrip()
    {
      TestNumeric(pricer => pricer.QuotedPriceIsSpecified ? pricer.QuotedPrice : pricer.ModelPrice() - SpotPrice);
    }

    /// <summary>
    /// Implied settlement
    /// </summary>
    [Test, Smoke, Category("Pricing")]
    public void ImpliedSettlement()
    {
      TestNumeric(pricer=>pricer.ModelPrice());
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

    private void TestNumeric(Func<StockFuturePricer, double> method)
    {
      TestNumeric(
        _pricer,
        "",
        obj => method((StockFuturePricer)obj));
    }

    #endregion Utils

    #region Data

    public Dt AsOf { get; set; }
    protected DiscountData DiscountData { get; set; }
    private StockFuturePricer _pricer;
    private StockFuturePricer _ccrPricer;
    public double SpotPrice { get; set; }
    public Dt Expiration { get; set; }
    public double DividendYield { get; set; }

    #endregion
  }
}
