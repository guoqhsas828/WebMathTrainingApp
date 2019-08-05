//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestCDPricer : ToolkitTestBase
  {
    #region Tests

    /// <summary>
    /// Test CD interest
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010428, 20010827, 0.0564, DayCount.Actual360, 0.0564, ExpectedResult = 0.01895667)]
    [NUnit.Framework.TestCase(20000911, 20000812, 20010827, 0.0622, DayCount.Actual360, 0.0622, ExpectedResult = 0.06565556)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020411, 0.0176, DayCount.Actual360, 0.0176, ExpectedResult = 0.00283556)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020613, 0.0180, DayCount.Actual360, 0.0180, ExpectedResult = 0.00605000)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020912, 0.0200, DayCount.Actual360, 0.0200, ExpectedResult = 0.01177778)]
    [NUnit.Framework.TestCase(20010911, 20010812, 20020307, 0.0317, DayCount.Actual360, 0.0317, ExpectedResult = 0.01822750)]
    public double Interest(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).Interest(), 8);
    }

    /// <summary>
    /// Price
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010428, 20010827, 0.0564, DayCount.Actual360, 0.0564, ExpectedResult = 1.00463394)]
    [NUnit.Framework.TestCase(20000911, 20000812, 20010827, 0.0622, DayCount.Actual360, 0.0622, ExpectedResult = 1.00488776)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020411, 0.0176, DayCount.Actual360, 0.0176, ExpectedResult = 1.00146466)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020613, 0.0180, DayCount.Actual360, 0.0180, ExpectedResult = 1.00149321)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020912, 0.0200, DayCount.Actual360, 0.0200, ExpectedResult = 1.00164998)]
    [NUnit.Framework.TestCase(20010911, 20010812, 20020307, 0.0317, DayCount.Actual360, 0.0317, ExpectedResult = 1.00260113)]
    public double Price(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).Price(), 8);
    }

    /// <summary>
    /// Equivalent discount rate
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010428, 20010827, 0.0564, DayCount.Actual360, 0.0564, ExpectedResult = 0.05560723)]
    [NUnit.Framework.TestCase(20000911, 20000812, 20010827, 0.0622, DayCount.Actual360, 0.0622, ExpectedResult = 0.05865312)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020411, 0.0176, DayCount.Actual360, 0.0176, ExpectedResult = 0.01757594)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020613, 0.0180, DayCount.Actual360, 0.0180, ExpectedResult = 0.01791847)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020912, 0.0200, DayCount.Actual360, 0.0200, ExpectedResult = 0.01979980)]
    [NUnit.Framework.TestCase(20010911, 20010812, 20020307, 0.0317, DayCount.Actual360, 0.0317, ExpectedResult = 0.03121351)]
    public double DiscountRate(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).DiscountRate(DayCount.Actual360), 8);
    }

    /// <summary>
    /// Bond Basis yield
    /// </summary>
    public double BondYield(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).BondYield(DayCount.ActualActualBond, Frequency.SemiAnnual), 8);
    }

    /// <summary>
    /// Change in value for a 1bp drop in CD rate
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010428, 20010827, 0.0564, DayCount.Actual360, 0.0564, ExpectedResult = 0.2504)]
    [NUnit.Framework.TestCase(20000911, 20000812, 20010827, 0.0622, DayCount.Actual360, 0.0622, ExpectedResult = 0.9213)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020411, 0.0176, DayCount.Actual360, 0.0176, ExpectedResult = 0.0778)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020613, 0.0180, DayCount.Actual360, 0.0180, ExpectedResult = 0.2520)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020912, 0.0200, DayCount.Actual360, 0.0200, ExpectedResult = 0.5013)]
    [NUnit.Framework.TestCase(20010911, 20010812, 20020307, 0.0317, DayCount.Actual360, 0.0317, ExpectedResult = 0.4854)]
    public double Pv01(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).Pv01(), 4);
    }

    /// <summary>
    /// Change in value for a 1bp drop in bond basis Yield
    /// </summary>
    public double BondPv01(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).BondPv01(), 8);
    }

    /// <summary>
    /// Calculates duration as the weighted average time to cash flows
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010428, 20010827, 0.0564, DayCount.Actual360, 0.0564, ExpectedResult = 0.24931507)]
    [NUnit.Framework.TestCase(20000911, 20000812, 20010827, 0.0622, DayCount.Actual360, 0.0622, ExpectedResult = 0.95890411)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020411, 0.0176, DayCount.Actual360, 0.0176, ExpectedResult = 0.07671233)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020613, 0.0180, DayCount.Actual360, 0.0180, ExpectedResult = 0.24931507)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020912, 0.0200, DayCount.Actual360, 0.0200, ExpectedResult = 0.49863014)]
    [NUnit.Framework.TestCase(20010911, 20010812, 20020307, 0.0317, DayCount.Actual360, 0.0317, ExpectedResult = 0.48493151)]
    public double Duration(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).Duration(), 8);
    }

    /// <summary>
    /// Calculates modified duration as the percentage price change for a 1bp drop in yield
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010428, 20010827, 0.0564, DayCount.Actual360, 0.0564, ExpectedResult = 0.24923088)]
    [NUnit.Framework.TestCase(20000911, 20000812, 20010827, 0.0622, DayCount.Actual360, 0.0622, ExpectedResult = 0.91686641)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020411, 0.0176, DayCount.Actual360, 0.0176, ExpectedResult = 0.07767206)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020613, 0.0180, DayCount.Actual360, 0.0180, ExpectedResult = 0.25163918)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020912, 0.0200, DayCount.Actual360, 0.0200, ExpectedResult = 0.50052005)]
    [NUnit.Framework.TestCase(20010911, 20010812, 20020307, 0.0317, DayCount.Actual360, 0.0317, ExpectedResult = 0.48414467)]
    public double ModDuration(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).ModDuration(), 8);
    }

    /// <summary>
    /// Calculates modified duration as the percentage price change for a 1bp drop in yield
    /// </summary>
    public double BondModDuration(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Math.Round(Pricer(settle, effective, maturity, coupon, dayCount, yield).BondModDuration(), 8);
    }

    /// <summary>
    /// Number of days from settlement to maturity
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010428, 20010827, 0.0564, DayCount.Actual360, 0.0564, ExpectedResult = 91)]
    [NUnit.Framework.TestCase(20000911, 20000812, 20010827, 0.0622, DayCount.Actual360, 0.0622, ExpectedResult = 350)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020411, 0.0176, DayCount.Actual360, 0.0176, ExpectedResult = 28)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020613, 0.0180, DayCount.Actual360, 0.0180, ExpectedResult = 91)]
    [NUnit.Framework.TestCase(20020314, 20020212, 20020912, 0.0200, DayCount.Actual360, 0.0200, ExpectedResult = 182)]
    [NUnit.Framework.TestCase(20010911, 20010812, 20020307, 0.0317, DayCount.Actual360, 0.0317, ExpectedResult = 177)]
    public int DaysToMaturity(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield)
    {
      return Pricer(settle, effective, maturity, coupon, dayCount, yield).DaysToMaturity();
    }

    #endregion Tests

    #region Utils

    private CDPricer Pricer(int settle, int effective, int maturity, double coupon, DayCount dayCount, double yield )
    {
      ToolkitConfigurator.Init();
      var cd = new CD( new Dt(effective), new Dt(maturity), Currency.None, coupon, dayCount);
      var pricer = new CDPricer(cd, new Dt(settle), new Dt(settle), QuotingConvention.Yield, yield, null, null, null, 1.0);
      return pricer;
    }

    #endregion Utils
  }
}
