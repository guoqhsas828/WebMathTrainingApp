//
// QUnit test of StatisticsBuilder
// Copyright (c)    2002-2018. All rights reserved.
//

using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  /// <exclude />
  [TestFixture]
  public class TestStatisticsBuilder
  {
    #region Tests
    /// <exclude />
    [Test, Smoke]
    public void BuildInAscendingOrder()
    {
      StatisticsBuilder stats = new StatisticsBuilder();
      double[] sample = new[] {1.0, 2.0, 3.0, 4.0};
      double[] weights = new[] {0.25, 0.25, 0.25, 0.25};

      // Add
      for(int i = 0; i < sample.Length; i++)
        stats.Add(weights[i], sample[i]);

      // Test
      Assert.AreEqual(1.0, stats.Min, "Min");
      Assert.AreEqual(4.0, stats.Max, "Max");
      Assert.AreEqual(.25+.5+.75+1.0, stats.Mean, "Avg");
    }

    /// <exclude />
    [Test, Smoke]
    public void BuildInDescendingOrder()
    {
      StatisticsBuilder stats = new StatisticsBuilder();
      double[] sample = new[] { 1.0, 2.0, 3.0, 4.0 };
      double[] weights = new[] { 0.25, 0.25, 0.25, 0.25 };

      // Add
      for (int i = sample.Length - 1; i >= 0; i--)
        stats.Add(weights[i], sample[i]);

      // Test
      Assert.AreEqual(1.0, stats.Min, "Min");
      Assert.AreEqual(4.0, stats.Max, "Max");
      Assert.AreEqual(.25 + .5 + .75 + 1.0, stats.Mean, "Avg");
    }

    /// <exclude />
    [Test, Smoke]
    public void BuildInAnyOrder()
    {
      StatisticsBuilder stats = new StatisticsBuilder();
      double[] sample = new[] { 3.0, 1.0, 4.0, 2.0 };
      double[] weights = new[] { 0.25, 0.25, 0.25, 0.25 };

      // Add
      for (int i = 0; i < sample.Length; i++)
        stats.Add(weights[i], sample[i]);

      // Test
      Assert.AreEqual(1.0, stats.Min, "Min");
      Assert.AreEqual(4.0, stats.Max, "Max");
      Assert.AreEqual(.25 + .5 + .75 + 1.0, stats.Mean, "Avg");
    }
    #endregion
  }
}
