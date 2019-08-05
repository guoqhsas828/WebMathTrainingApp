//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Linq;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Tests.Pricers.CmsCapFloorTestData;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture(Name.SimpleSurfaceFlat)]
  [TestFixture(Name.RateCubeFlat)]
  [TestFixture(Name.SwaptionMarketCube)]
  [TestFixture(Name.SwaptionMarketCubeWithSkew)]
  public class TestCmsCapFloorPricer
  {
    #region Set up and tear down

    public TestCmsCapFloorPricer(Name name)
    {
      _data = Data.All.Single(d => d.Name == name);
      _asOfShift = 10;
    }

    [OneTimeSetUp]
    public void SetUp()
    {
      _pricer = _data.GetPricer(_asOfShift);

      _expects = ExpectsStore.Load(System.IO.Path.Combine(
        BaseEntityContext.InstallDir, "toolkit", "test", "data", "expects",
        $"{nameof(TestCmsCapFloorPricer)}_{_data.Name}.expects"));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
      _pricer = null;

      _expects?.Dispose();
      _expects = null;
    }

    private readonly Data _data;
    private readonly int _asOfShift;
    private IExpectsStore _expects;
    private CmsCapFloorPricer _pricer;

    #endregion

    #region Regression tests

    /// <exclude />
    [Test]
    public void Accrued()
    {
      _pricer.Accrued().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void CapPv()
    {
      _pricer.CapPv().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void Clone()
    {
      _pricer.Clone().IsExpected(To.Match(_pricer));
    }

    /// <exclude />
    [Test]
    public void Delta_Old()
    {
      _pricer.Delta_Old().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void DeltaBlack()
    {
      _pricer.DeltaBlack().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void DeltaSabr()
    {
      _pricer.DeltaSabr().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void Gamma_Old()
    {
      _pricer.Gamma_Old().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void ImpliedVolatility()
    {
      _pricer.ImpliedVolatility().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void Intrinsic()
    {
      _pricer.Intrinsic().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void PaymentPv()
    {
      _pricer.PaymentPv().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void ProductPv()
    {
      _pricer.ProductPv().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void Pv()
    {
      _pricer.Pv().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void Rate01()
    {
      _pricer.Rate01().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void RateGamma()
    {
      _pricer.RateGamma().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void VannaSabr()
    {
      _pricer.VannaSabr().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void Vega()
    {
      _pricer.Vega().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void VegaBlack()
    {
      _pricer.VegaBlack().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void VegaHedge()
    {
      _pricer.VegaHedge().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void VegaSabr()
    {
      _pricer.VegaSabr().IsExpected(To.Match(_expects).Within(1E-9));
    }

    /// <exclude />
    [Test]
    public void VolgaSabr()
    {
      _pricer.VolgaSabr().IsExpected(To.Match(_expects).Within(1E-9));
    }

    #endregion
  }
}
