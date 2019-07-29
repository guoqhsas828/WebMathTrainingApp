/*
 * ExpectationBuilder.cs
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
  ///   Incremental build the expectation
  /// </summary>
  [Serializable]
  public class ExpectationBuilder
  {
    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    /// <remarks>
    ///   The builder constructed in this way does not
    ///   build histogram.
    /// </remarks>
    public ExpectationBuilder()
    {
      count_ = 0;
      scale_ = sumW_ = avgX_ = 0;
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
    /// 
    /// <param name="w">Weight of the point</param>
    /// <param name="x">Value of the point</param>
    public void Add(double w, double x)
    {
      if (w < 0)
        throw new ArgumentException("weight cannot be negative");
      ++count_;
      if (w > 0)
      {
        // We scale the data in order to avoid overflow/underflow.
        double ax = x < 0 ? (-x) : x;
        if (ax > scale_)
        {
          double s = scale_ / ax;
          avgX_ *= s;
          scale_ = ax; 
          x /= scale_;
        }
        else if (ax > 0)
          x /= scale_;

        // Add the weight and the scaled averages
        sumW_ += w;
        avgX_ += w * (x - avgX_) / sumW_;
      }
      return;
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
    ///   The mean of data
    /// </summary>
    /// <remarks>The mean is defined as the weighted average of the <c>x</c>
    /// <formula>
    ///   \bar{x} = \frac{\sum_{i=1}^n w_i\,x_i}{\sum_{i=1}^n w_i}
    /// </formula>
    /// </remarks>
    public double Mean
    {
      get { return avgX_ * scale_; }
    }
    #endregion Properties

    #region Data
    private int count_ = 0;
    private double scale_ = 0;
    private double sumW_ = 0;
    private double avgX_ = 0;
    #endregion Data

  } // class MeanBuilder
}
