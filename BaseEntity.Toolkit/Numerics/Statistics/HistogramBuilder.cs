/*
 * HistogramBuilder.cs
 *
 *  -2008. All rights reserved.    
 *
 */
using System;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Incremental build histogram
  /// </summary>
  /// 
  /// <example>
  /// <code language="C#">
  ///   // The data values and weights
  ///   double[] x = new double[]{ 0.1, 0.6, 0.3, 0.5, 0.8, 0.2 };
  ///   double[] w = new double[]{ 0.5, 1.5, 2.2, 3.0, 1.8, 1.0 };
  ///
  ///   // The bin array
  ///   double[] bin = new double[]{ 0.2, 0.4, 0.6, 0.8 };
  /// 
  ///   // Create a histogram builder
  ///   HistogramBuilder hb = new HistogramBuilder(bin);
  /// 
  ///   // Add data to the builder
  ///   for (int i = 0; i &lt; x.Length; ++i)
  ///     hb.Add(w[i], x[i]);
  ///
  ///   // Get back the bin and the frequency distribution over the bin
  ///   double[] bin = hb.Bins;
  ///   double[] freq = hb.Frequencies;
  /// 
  ///   // Get the emiprical distribution
  ///   EmpiricalDistribution dist = hb.Distribution;
  /// 
  ///   // Calculate the cumulative probability at x = 0.5
  ///   double p = dist.Cumulative(0.5);
  /// 
  ///   // Calculate the 10% quantile at the lower end
  ///   double p = dist.Quantile(0.10);
  /// </code>
  /// </example>
  [Serializable]
  public class HistogramBuilder
  {
    /// <summary>
    ///   Construct a histogram builder
    /// </summary>
    /// <remarks>
    ///  <para>The <paramref name="histogramBin"/> needs not be sorted.
    ///   The constructor makes a clone of it and sort it in ascending order.</para>
    /// </remarks>
    /// <param name="histogramBin">Delimters of histogram bins</param>
    public HistogramBuilder(double[] histogramBin)
    {
      if (histogramBin == null || histogramBin.Length == 0)
        throw new ToolkitException("The histogram bin array cannot be empty");
      bin_ = (double[])histogramBin.Clone();
      Array.Sort<double>(bin_);
      freq_ = new double[histogramBin.Length + 1];
      min_ = Double.MaxValue;
      max_ = Double.MinValue;
      count_ = 0;
      sumW_ = 0;
    }

    /// <summary>
    ///   Add a data point
    /// </summary>
    /// <remarks>
    ///   The weight need not be normalized.  Only the relative proportions
    ///   matters.
    /// </remarks>
    /// 
    /// <param name="w">Weight of the point</param>
    /// <param name="x">Value of the point</param>
    public void Add(double w, double x)
    {
      if (w < 0)
        throw new ArgumentException("weight and stratum cannot be negative");
      else if (w > 0)
      {
        // Add to histogram
        int idx = Array.BinarySearch<double>(bin_, x);
        if (idx < 0) idx = ~idx;
        freq_[idx] += w;
      }
      if (x < min_) min_ = x;
      if (x > max_) max_ = x;
      ++count_;
			sumW_ += w;
      return;
    }

    /// <summary>
    ///   Create an empirical distribution from the histogram
    /// </summary>
    /// <returns>Empirical distribution</returns>
    private EmpiricalDistribution ToDistribution()
    {
      if (freq_ == null || count_ <= 0 || sumW_ <= 0)
        return null;

      const double tol = 1E-14;
      List<double> x = new List<double>(), y = new List<double>();

      // We always set the lower bound probability to zero
      double lastx = min_ - (1 + Math.Abs(min_)) * tol;
      double lastp = 0.0;
      x.Add(lastx);
      y.Add(0.0);

      int i = 0;
      for (; i < bin_.Length; ++i)
      {
        if (bin_[i] > lastx) break;
        lastp += freq_[i] / sumW_;
      }
      for (; i < bin_.Length; ++i)
      {
        if (bin_[i] >= max_)
          break;
        x.Add(lastx = bin_[i]);
        lastp += freq_[i] / sumW_;
        if (lastp >= 1.0)
        {
          y.Add(lastp = 1.0);
          break;
        }
        y.Add(lastp);
      }

      // The last point always correspondss probability one.
      x.Add(max_); y.Add(1.0);

      return new EmpiricalDistribution(x.ToArray(), y.ToArray());
    }

    /// <summary>
    ///   The histogram frequency of data
    /// </summary>
    /// <remarks>
    ///   <para>The frequencies are built based on the bin values supplied
    ///   when constructing the builder.</para>
    /// 
    ///  <para>Let <c>n</c> be the number of bin nodes, this property returns an array of size <c>(n+1)</c>,
    ///    in which at position 0 is the number of data points less than or equal to the first bin node,
    ///    at position <c>i</c>
    ///    <formula inline="true">(0 &lt; i &lt; n)</formula> is the number of data points greater
    ///    than the <c>i</c>th node
    ///    and less than or equal to the <c>(i+1)</c>th node,
    ///    at position n is the number of data points greater than the last node.</para>
    /// 
    ///   <para>The return value is a duplicated clone and modifying
    ///   it does not affect the builder.</para>
    /// </remarks>
    public double[] Frequencies
    {
      get
      {
        if (freq_ == null || sumW_ <= 0) return null;
        return Array.ConvertAll<double, double>(freq_,
          delegate(double f) { return f / sumW_; });
      }
    }

    /// <summary>
    ///   The histogram of data
    /// </summary>
    /// <remarks>
    ///   The return value is a duplicated clone and modifying
    ///   it does not affect the builder.
    /// </remarks>
    public double[] Bins
    {
      get { return (double[])bin_.Clone(); }
    }

    /// <summary>
    ///   The empirical distribution
    /// </summary>
    public EmpiricalDistribution Distribution
    {
      get { return ToDistribution(); }
    }

    /// <summary>
    ///   The minimum value of the data
    /// </summary>
    public double MinValue
    {
      get { return min_ == Double.MaxValue ? Double.NaN : min_; }
    }

    /// <summary>
    ///  The maximum value of the data
    /// </summary>
    public double MaxValue
    {
      get { return max_ == Double.MinValue ? Double.NaN : max_; }
    }

    private int count_;
    private double sumW_;
    private double min_, max_;
    private readonly double[] bin_;
    private readonly double[] freq_;
  }
}
