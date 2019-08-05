//
// QUnit test of HistogramBuilder
// Copyright (c)    2002-2018. All rights reserved.
//

using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  /// <exclude />
  [TestFixture]
  public class TestHistogramBuilder
  {
    #region Tests
    /// <exclude />
    [Test, Smoke]
    public void TestLeftBin()
    {
      double[] bins = new[] {0.25, 0.5, 0.75, 1.0};
      double[] sample = new[] {0.0,0.0,0.25,0.3};
      double[] weights = new[] {0.25, 0.25, 0.25, 0.25};
      HistogramBuilder hist = new HistogramBuilder(bins);

      // Build
      for(int i = 0; i < sample.Length; i++)
        hist.Add(weights[i], sample[i]);

      // Test
      AssertEqual("Min", 0.0, hist.MinValue);
      AssertEqual("Max", 0.3, hist.MaxValue);
      
      // Test Bins
      AssertEqual("Bin 0", 0.75, hist.Frequencies[0]);
      AssertEqual("Bin 1", .25, hist.Frequencies[1]);
      AssertEqual("Bin 2", 0, hist.Frequencies[2]);
      AssertEqual("Bin 3", 0, hist.Frequencies[3]);
      AssertEqual("Bin 4", 0, hist.Frequencies[4]);
    }

    /// <exclude />
    [Test, Smoke]
    public void TestRightBin()
    {
      double[] bins = new[] { 0.25, 0.5, 0.75, 1.0 };
      double[] sample = new[] { 1.0, 1.1, .85, 1.5 };
      double[] weights = new[] { 0.25, 0.25, 0.25, 0.25 };
      HistogramBuilder hist = new HistogramBuilder(bins);

      // Build
      for (int i = 0; i < sample.Length; i++)
        hist.Add(weights[i], sample[i]);

      // Test
      AssertEqual("Min", 0.85, hist.MinValue);
      AssertEqual("Max", 1.5, hist.MaxValue);

      // Test Bins
      AssertEqual("Bin 0", 0, hist.Frequencies[0]);
      AssertEqual("Bin 1", 0, hist.Frequencies[1]);
      AssertEqual("Bin 2", 0, hist.Frequencies[2]);
      AssertEqual("Bin 3", .5, hist.Frequencies[3]);
      AssertEqual("Bin 4", .5, hist.Frequencies[4]);
    }

    /// <exclude />
    [Test, Smoke]
    public void TestInnerBin()
    {
      double[] bins = new[] { 0.25, 0.5, 0.75, 1.0 };
      double[] sample = new[] { 0.51, 0.75, 0.85, .5 };
      double[] weights = new[] { 0.25, 0.25, 0.25, 0.25 };
      HistogramBuilder hist = new HistogramBuilder(bins);

      // Build
      for (int i = 0; i < sample.Length; i++)
        hist.Add(weights[i], sample[i]);

      // Test
      AssertEqual("Min", 0.5, hist.MinValue);
      AssertEqual("Max", 0.85, hist.MaxValue);

      // Test Bins
      AssertEqual("Bin 0", 0, hist.Frequencies[0]);
      AssertEqual("Bin 1",0.25, hist.Frequencies[1]);
      AssertEqual("Bin 2",0.5, hist.Frequencies[2]);
      AssertEqual("Bin 3", .25, hist.Frequencies[3]);
      AssertEqual("Bin 4", 0, hist.Frequencies[4]);
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
