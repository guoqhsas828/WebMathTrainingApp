/*
 * StatisticsBuilder.cs
 *
 *  -2008. All rights reserved.    
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Build common statistics, include mean, variance, skew and kurtosis incrementally.
  /// </summary>
  /// 
  /// <example>
  /// <code language="C#">
  ///   // The data values and weights
  ///   double[] x = new double[]{ 0.1, 0.6, 0.3, 0.5, 0.8, 0.2 };
  ///   double[] w = new double[]{ 0.5, 1.5, 2.2, 3.0, 1.8, 1.0 };
  ///
  ///   // Create a statistics builder
  ///   StatisticsBuilder sb = new StatisticsBuilder();
  /// 
  ///   // Add data to the builder
  ///   for (int i = 0; i &lt; x.Length; ++i)
  ///     sb.Add(w[i], x[i]);
  ///
  ///   // Get back the statistics
  ///   double mean = sb.Mean;
  ///   double variance = sb.Variance;
  ///   double skew = sb.Skew;
  ///   double kurtosis = sb.Kurtosis;
  /// </code>
  /// </example>
  [Serializable]
  public class StatisticsBuilder
  {
    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    /// <remarks>
    ///   The builder constructed in this way does not
    ///   build histogram.
    /// </remarks>
    public StatisticsBuilder()
    {
      count_ = nstrata_ = 0;
      scale_ = sumW_ = avgX3_ = avgX4_ = 0;
      strata_ = null;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Add a data point
    /// </summary>
    /// <remarks>
    ///   The weight need not be normalized.  Only the relative proportions
    ///   matters.
    /// </remarks>
    /// <param name="stratum">Index of the stratum the data belongs to</param>
    /// <param name="w">Weight of the point</param>
    /// <param name="x">Value of the point</param>
    public void Add(int stratum, double w, double x)
    {
      if (w < 0 || stratum < 0)
        throw new ArgumentException("weight and stratum cannot be negative");
      ++count_;
      if (w > 0)
      {
        // We scale the data in order to avoid overflow/underflow.
        double s = 1, s2 = 1;
        double ax = x < 0 ? (-x) : x;
        if (ax > scale_)
        {
          s = scale_ / ax;
          avgX1_ *= s;
          s2 = s * s;
          avgX2_ *= s2;
          avgX3_ *= s2 * s;
          avgX4_ *= s2 * s2;
          scale_ = ax;
          x /= scale_;
          if (count_ > 1)
          {
            min_ *= s;
            max_ *= s;
          }
        }
        else if (ax > 0)
          x /= scale_;

        // Add the weight and the scaled averages
				min_ = (x < min_ ? x : min_);
				max_ = (x > max_ ? x : max_);
        sumW_ += w;
        avgX1_ += w * ( x - avgX1_) / sumW_;
        double x2 = x * x;
        avgX2_ += w * (x2 - avgX2_) / sumW_;
        avgX3_ += w * (x2 * x - avgX3_) / sumW_;
        avgX4_ += w * (x2 * x2 - avgX4_) / sumW_;
        GetStratum(stratum).Add(w, s, x, s2, x2);
      }
      else
        ++GetStratum(stratum).Count;
      return;
    }

    /// <summary>
    ///   Add a data point
    /// </summary>
    /// <remarks>
    ///   The weight need not be normalized.  Only the relative proportions
    ///   matters.
    /// </remarks>
    /// <param name="w">Weight of the point</param>
    /// <param name="x">Value of the point</param>
    public void Add(double w, double x)
    {
      Add(0, w, x);
    }

    /// <summary>
    ///   Add a data point with weight 1.0
    /// </summary>
    /// <param name="x">Value of the point</param>
    public void Add(double x)
    {
      Add(1.0, x);
    }
    #endregion Methods

    #region Properties

    /// <summary>
    ///   Number of data points already added
    /// </summary>
    public int Count
    {
      get { return count_; }
    }

		/// <summary>
		/// The minimum value of the distribution.
		/// </summary>
		public double Min
		{
			get { return scale_*min_; }
		}

		/// <summary>
		/// The maximum value of the distribution.
		/// </summary>
		public double Max
		{
			get { return scale_ * max_; }
		}

    /// <summary>
    ///   The mean of data
    /// </summary>
    /// <remarks>The mean is defined as the weighted average of the <c>x</c>
    /// <formula>
    ///   \bar{x} = \frac{\sum_{i=1}^n w_i\,x_i}{\sum_{i=1}^n w_i}
    /// </formula>
    /// </remarks>
    public double Mean
    {
      get { return avgX1_ * scale_; }
    }

    /// <summary>
    ///  The variance of data
    /// </summary>
    /// <remarks>
    ///  The weighted variance is defined as
    /// <formula>
    ///   v = \frac{\sum_{i=1}^n w_i\,(x_i - \bar{x})^2}{\sum_{i=1}^n w_i}
    /// </formula>
    ///   where <formula inline="true">\bar{x}</formula> is the weighted mean.
    /// </remarks>
    public double Variance
    {
      get
      {
        double s2 = avgX2_ - avgX1_ * avgX1_;
        if (s2 <= 0) return 0;
        return scale_ * scale_ * s2;
      }
    }

    /// <summary>
    ///  The standard deviation of data
    /// </summary>
    /// <remarks>
    ///  The weighted standard deviation is defined as
    /// <formula>
    ///   \sigma = \sqrt{\frac{\sum_{i=1}^n w_i\,(x_i - \bar{x})^2}{\sum_{i=1}^n w_i}}
    /// </formula>
    ///   where <formula inline="true">\bar{x}</formula> is the weighted mean.
    /// </remarks>
    public double StdDev
    {
      get
      {
        double s2 = avgX2_ - avgX1_ * avgX1_;
        if (s2 <= 0) return 0;
        return scale_ * Math.Sqrt(s2);
      }
    }

    /// <summary>
    ///   The variance of the estimator
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the estimated variance of the sample mean
    /// as an estimator of the population
    /// mean.
    /// It is different than the variance of the sample,
    /// which is an estimator of the population variance itself.
    /// </para>
    /// 
    /// <para>
    ///   For a samples with <formula inline="true">K</formula> strata
    ///   and <formula inline="true">M_k</formula> sample in the stratum
    ///   <formula inline="true">k</formula>, the variance is given by
    /// <formula>
    ///   \mathrm{Var}(\bar{X}) =
    ///   \frac{\sum_{k=1}^{K} q_k \hat{\sigma}_k}{\sum_{k=1}^{K} W_k}
    /// </formula>
    /// where
    /// <formula>
    ///   W_k = \sum_{i=1}^{M_k} w_{i k}
    /// </formula>
    /// <formula>
    ///   q_k = \sum_{i=1}^{M_k} w_{i k}^2 / \sum_{j=1}^{K} W_j
    /// </formula>
    /// <formula>
    ///   \hat{\sigma_k} = \sum_{i=1}^{M_k} w_{i k} (x_{i k} - \bar{x}_k)^2 / \sum_{i=1}^{M_k} w_{i k}
    /// </formula>
    /// </para>
    /// </remarks>
    public double EstimatorVariance
    {
      get
      {
        MeanVariance m;
        double v = 0;
        for (int i = 0; i < nstrata_; ++i)
          if ((m = strata_[i]) != null)
          {
            v += (m.SumW2 / sumW_) * m.Variance;
          }
        return (v / sumW_) * scale_ * scale_;
      }
    }

    /// <summary>
    ///   The standard error of the estimator
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the square root of the estimator variance.
    /// </para>
    /// </remarks>
    public double EstimatorStdError
    {
      get
      {
        MeanVariance m;
        double v = 0;
        for (int i = 0; i < nstrata_; ++i)
          if ((m = strata_[i]) != null)
          {
            v += (m.SumW2 / sumW_) * m.Variance;
          }
        return Math.Sqrt(v / sumW_) * scale_;
      }
    }

    /// <summary>
    ///   The skew of data
    /// </summary>
    /// <remarks>
    /// The weighted skew is defined as
    /// <formula>
    ///   \gamma = \frac{\sum_{i=1}^n w_i\,(x_i - \bar{x})^3}{\sigma^3\,\sum_{i=1}^n w_i}
    /// </formula>
    /// where <formula inline="true">\bar{x}</formula> is the weighted mean and
    /// <formula inline="true">\sigma</formula> is the standard deviation.
    /// </remarks>
    public double Skew
    {
      get
      {
        double s2 = avgX2_ - avgX1_ * avgX1_;
        if (s2 <= 0) return 0;
        double s = Math.Sqrt(s2);
        if (s2 * s <= 1E-12) return 0;
        double mean = avgX1_;
        return mean * ((avgX3_ / mean - mean * mean) / s2 - 3) / s;
      }
    }

    /// <summary>
    ///   The kurtosis of data
    /// </summary>
    /// <remarks>
    ///   The weighted kurtosis is defined as
    /// <formula>
    ///   \kappa = \frac{\sum_{i=1}^n w_i\,(x_i - \bar{x})^4}{\sigma^4\,\sum_{i=1}^n w_i} - 3
    /// </formula>
    /// where <formula inline="true">\bar{x}</formula> is the weighted mean and
    /// <formula inline="true">\sigma</formula> is the standard deviation.
    /// </remarks>
    public double Kurtosis
    {
      get
      {
        double s2 = avgX2_ - avgX1_ * avgX1_;
        if (s2 <= 0) return 0;
        double s4 = s2 * s2;
        if (s4 <= 1E-12) return 0;
        double mean = avgX1_;
        double ax2 = mean * mean;
        return ax2 * ((avgX4_ / ax2 - 4 * avgX3_ / mean + 3 * ax2) / s2 + 6) / s2 - 3;
      }
    }

    #endregion Properties

    #region Strata management
    /// <summary>
    ///   Helper class recording mean and variance of a strata
    /// </summary>
    class MeanVariance
    {
      internal void Add(double w, double s, double x, double s2, double x2)
      {
        ++Count;
        if (s != 1.0)
        {
          AvgX *= s;
          AvgX2 *= s2;
        }
        SumW2 += w * w;
        SumW += w;
        AvgX += w * (x - AvgX) / SumW;
        AvgX2 += w * (x2 - AvgX2) / SumW;
      }
      public double Mean
      {
        get { return AvgX; }
      }
      public double Variance
      {
        get { return Count <= 1 || SumW <= 0.0 ? 0.0 : (AvgX2 - AvgX * AvgX); }
      }
      public int Count = 0;
      public double SumW = 0;
      public double SumW2 = 0;
      public double AvgX = 0;
      public double AvgX2 = 0;
    }

    private MeanVariance GetStratum(int istrata)
    {
      if (istrata >= nstrata_)
      {
        if (strata_ == null)
          strata_ = new MeanVariance[((istrata + 15) / 8) * 8];
        else if (istrata >= strata_.Length)
        {
          int len;
          if (istrata < Int32.MaxValue / 2)
          {
            len = strata_.Length;
            do { len *= 2; } while (istrata >= len);
          }
          else if (istrata < Int32.MaxValue)
            len = istrata + 1;
          else
            throw new ArgumentOutOfRangeException(String.Format(
              "The stratum index ({0}) is too big", istrata));
          Array.Resize<MeanVariance>(ref strata_, len);
        }
        nstrata_ = istrata + 1;
      }
      MeanVariance m = strata_[istrata];
      if (m == null)
        m = strata_[istrata] = new MeanVariance();
      return m;
    }

    private void Calculate(out double mean, out double mean2)
    {
      MeanVariance m;
      mean = mean2 = 0;
      for (int i = 0; i < nstrata_; ++i)
        if ((m = strata_[i]) != null)
        {
          mean += (m.SumW / sumW_) * m.AvgX;
          mean2 += (m.SumW / sumW_) * m.AvgX2;
        }
      return;
    }
    #endregion Strata management

    #region Data
    private int count_ = 0;
    private double scale_ = 0;
    private double sumW_ = 0;
    private double avgX1_ = 0;
    private double avgX2_ = 0;
    private double avgX3_ = 0;
    private double avgX4_ = 0;
		private double min_ = double.MaxValue;
		private double max_ = double.MinValue;
    private MeanVariance[] strata_;
    private int nstrata_ = 0;
    #endregion Data

  } // class StatisticsBuilder
}
