//
// QUnit test of EmpiricalDistribution
// Copyright (c)    2002-2018. All rights reserved.
//

using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  /// <exclude />
  [TestFixture]
  public class TestEmpiricalDistribution
  {
    #region Tests
    /// <exclude />
    [Test, Smoke]
    public void TestMinValue()
    {
      // Setup distribution
      EmpiricalDistribution dist = new EmpiricalDistribution(
        new []{0.0,1.0,2.0,3.0},
        new[] { .25, .5, .75, 1.0 }
        );

      // Test CDF
      AssertEqual("Left of 1st Sample (Cumulative)", 0.25, dist.Cumulative(-1.0));
      AssertEqual("Equals 1st Sample (Cumulative)", 0.25, dist.Cumulative(0.0));
      AssertEqual("Right of 1st Sample (Cumulative)", 0.25 + 0.25 / 2.0, dist.Cumulative(0.5));
      Assert.AreEqual(0.25 + 0.25/2.0, dist.Cumulative(0.5 + EmpiricalDistribution.Tolerance),
        EmpiricalDistribution.Tolerance);

      // Test Quantiles
      AssertEqual("Left of 1st Sample (Quantile)", 0.0, dist.Quantile(-1.0));
      AssertEqual("Equals 1st Sample (Quantile)", 0.0, dist.Quantile(.25));
      AssertEqual("Right of 1st Sample (Quantile)", 0.5, dist.Quantile(0.25+.25/2.0));
    }
    
    /// <exclude />
    [Test, Smoke]
    public void TestMaxValue()
    {
      // Setup distribution
      EmpiricalDistribution dist = new EmpiricalDistribution(
        new[] { 0.0, 1.0, 2.0, 3.0 },
        new[] { .25, .5, .75, 1.0 }
        );

      // Test CDF
      AssertEqual("Left of Last Sample at Tolerance (Cumulative)",
                      .75 + (.25*(1.0 - EmpiricalDistribution.Tolerance)),
                      dist.Cumulative(3.0 - EmpiricalDistribution.Tolerance));
      AssertEqual("Left of Last Sample (Cumulative)", .75 + .25 / 2.0, dist.Cumulative(2.5));
      AssertEqual("Equals Last Sample (Cumulative)", 1.0, dist.Cumulative(3.0));
      AssertEqual("Right of Last Sample (Cumulative)", 1.0, dist.Cumulative(3.5));

      // Test Quantiles
      AssertEqual("Left of Last Sample (Quantile)", 2.5, dist.Quantile(.75+.25/2.0));
      AssertEqual("Equals Last Sample (Quantile)", 3.0, dist.Quantile(1.0));
      AssertEqual("Right of Last Sample (Quantile)", 3.0, dist.Quantile(1.1));
    }

    /// <exclude />
    [Test, Smoke]
    public void TestInnerValue()
    {
      // Setup distribution
      EmpiricalDistribution dist = new EmpiricalDistribution(
        new[] { 0.0, 1.0, 2.0, 3.0 },
        new[] { .25, .5, .75, 1.0 }
        );

      // Test CDF
      AssertEqual("Left of Sample (Cumulative)", .5 + .25 / 2.0, dist.Cumulative(1.5));
      AssertEqual("Left of Sample at Tolerance (Cumulative)", .5 + (.25*(1.0 - EmpiricalDistribution.Tolerance)),
                      dist.Cumulative(2.0 - EmpiricalDistribution.Tolerance));
      AssertEqual("Equals Sample (Cumulative)", .75, dist.Cumulative(2.0));
      AssertEqual("Right of Sample at Tolerance (Cumulative)", 0.75 + (0.25 * EmpiricalDistribution.Tolerance),
                      dist.Cumulative(2.0 + EmpiricalDistribution.Tolerance));
      AssertEqual("Right of Sample (Cumulative)", 0.75 + 0.25 / 2.0, dist.Cumulative(2.5));

      // Test Quantiles
      AssertEqual("Left of Sample (Quantile)", 1.5, dist.Quantile(.5+.25/2.0));
      AssertEqual("Equals Sample (Quantile)", 2.0, dist.Quantile(.75));
      AssertEqual("Right of Sample (Quantile)", 2.5, dist.Quantile(.75+.25/2.0));
    }
    #endregion

    #region Helpers

    private static void AssertEqual<T>(string label, T expect, T actual)
    {
      Assert.AreEqual(expect, actual, label);
    }
    #endregion
  }
}
