//
// Test unit for BondFutureModel
// Copyright (c)    2002-2018. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestBondFutureModel : ToolkitTestBase
  {
    #region Tests

    /// <summary>
    /// Test CME TBond Futures conversion factor
    /// </summary>
    [NUnit.Framework.TestCase(20110901, 20130615, 0.01125, ExpectedResult = 0.9201)]
    [NUnit.Framework.TestCase(20110901, 20130630, 0.03375, ExpectedResult = 0.9569)]
    [NUnit.Framework.TestCase(20110901, 20130630, 0.00375, ExpectedResult = 0.9079)]
    [NUnit.Framework.TestCase(20110901, 20130715, 0.01, ExpectedResult = 0.9144)]
    [NUnit.Framework.TestCase(20110901, 20130731, 0.03375, ExpectedResult = 0.955)]
    [NUnit.Framework.TestCase(20110901, 20130731, 0.00375, ExpectedResult = 0.9037)]
    [NUnit.Framework.TestCase(20110901, 20130815, 0.0075, ExpectedResult = 0.9063)]
    [NUnit.Framework.TestCase(20110901, 20130831, 0.03125, ExpectedResult = 0.9486)]
    [NUnit.Framework.TestCase(20111201, 20130915, 0.0075, ExpectedResult = 0.914)]
    // All 3Yr deliverables
    [NUnit.Framework.TestCase(20110901, 20140630, 0.02625, ExpectedResult = 0.9156)]
    [NUnit.Framework.TestCase(20110901, 20140715, 0.00625, ExpectedResult = 0.8618)]
    [NUnit.Framework.TestCase(20110901, 20140731, 0.02625, ExpectedResult = 0.9132)]
    // 5Yr Deliverables
    [NUnit.Framework.TestCase(20110901, 20151130, 0.01375, ExpectedResult = 0.8317)]
    [NUnit.Framework.TestCase(20110901, 20151231, 0.02125, ExpectedResult = 0.8565)]
    [NUnit.Framework.TestCase(20110901, 20160131, 0.02, ExpectedResult = 0.8493)]
    // 10Yr TNote Deliverables
    [NUnit.Framework.TestCase(20110901, 20180331, 0.02875, ExpectedResult = 0.8338)]
    // US TBond Futures Deliverables (subset)
    [NUnit.Framework.TestCase(20110901, 20261115, 0.065, ExpectedResult = 1.049)]
    [NUnit.Framework.TestCase(20110901, 20270215, 0.06625, ExpectedResult = 1.0618)]
    [NUnit.Framework.TestCase(20110901, 20270815, 0.06375, ExpectedResult = 1.0377)]
    // ref: Calculating US Treasury Futures Conversion Factors - CME, 2009
    [NUnit.Framework.TestCase(20081201, 20101031, 0.015, ExpectedResult = 0.9229)]
    [NUnit.Framework.TestCase(20090301, 20120115, 0.01125, ExpectedResult = 0.8747)]
    [NUnit.Framework.TestCase(20081201, 20131031, 0.0275, ExpectedResult = 0.8653)]
    [NUnit.Framework.TestCase(20081201, 20181115, 0.0375, ExpectedResult = 0.8357)]
    [NUnit.Framework.TestCase(20081201, 20380515, 0.045, ExpectedResult = 0.7943)]
    public double CmeTBondFutureConversionFactor(int expiration, int maturity, double coupon)
    {
      return BondFutureModel.CmeTBondFutureConversionFactor(new Dt(expiration), new Dt(maturity), coupon);
    }

    /// <summary>
    /// Test standard futures price (for margin) calculation.
    /// See <a href="http://www.cmegroup.com/trading/interest-rates/files/TreasuryFuturesPriceRoundingConventions_Mar_24_Final.pdf">
    /// </summary>
    [NUnit.Framework.TestCase(1.155234375, 10, ExpectedResult = 115523.44)]
    [NUnit.Framework.TestCase(1.168515625, 10, ExpectedResult = 116851.56)]
    [NUnit.Framework.TestCase(1.155234375, 10, ExpectedResult = 115523.44)]
    [NUnit.Framework.TestCase(0.977421875, 20, ExpectedResult = 195484.38)]
    public double FuturePrice(double price, double pointValue)
    {
      return BondFutureModel.FuturePrice(price, pointValue);
    }

    /// <summary>
    /// Test ASX Futures price (from yield) calculation
    /// See <a href="http://www.sfe.com.au/content/sfe/products/pricing.pdf">A Guide to the Pricing Conventions of ASX Interest Rate Products</a>
    /// </summary>
    [NUnit.Framework.TestCase(0.95505, 0.06, 3, ExpectedResult = 104180.10)]
    [NUnit.Framework.TestCase(0.9449, 0.06, 3, ExpectedResult = 101338.06)]
    [NUnit.Framework.TestCase(0.955, 0.06, 10, ExpectedResult = 111972.78)]
    [NUnit.Framework.TestCase(0.9436, 0.06, 10, ExpectedResult = 102723.06)]
    [NUnit.Framework.TestCase(0.9435, 0.06, 10, ExpectedResult = 102646.19)]
    public double AsxTBondFuturePrice(double price, double nominalCoupon, int years)
    {
      return BondFutureModel.AsxTBondFuturePrice(price, nominalCoupon, years, 100000.0);
    }

    #endregion Tests
  }
}