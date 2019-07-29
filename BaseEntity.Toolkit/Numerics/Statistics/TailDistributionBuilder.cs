/*
 * TailDistributionBuilder.cs
 *
 *  -2008. All rights reserved.    
 *
 */
using System;
using System.Diagnostics;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Tail distribution type.
  /// </summary>
  public enum TailDistributionType
  {
    /// <summary>
    ///  The distribution of the lower tail.
    /// </summary>
    Lower,

    /// <summary>
    ///  The distribution of the upper tail.
    /// </summary>
    Upper,
  } ;

  /// <summary>
  ///   Build tail distribution.
  /// </summary>
  public class TailDistributionBuilder
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="TailDistributionBuilder"/> class.
    /// </summary>
    /// <param name="sampleSize">The sample size.</param>
    /// <param name="tailProbability">The tail probability.</param>
    /// <param name="type">Upper or lower tail type.</param>
    public TailDistributionBuilder(
      int sampleSize, double tailProbability,
      TailDistributionType type)
    {
      // add 1% for safety
      tailProbability = Math.Max(tailProbability, 0.0) + 0.01;
      int size = Math.Max(8, (int) Math.Ceiling(sampleSize*tailProbability));
      sortedKeys_ = new double[size];
      sortedValues_ = new double[size];
      isLowerTail_ = type == TailDistributionType.Lower;
      sampleCount_ = valueCount_ = 0;
    }

    /// <summary>
    /// Adds the specified value to the builder.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void Add(double value)
    {
      Add(value,value); 
    }
    
    /// <summary>
    /// Adds the specified value to the builder.
    /// </summary>
    /// <param name="key">use for ranking within the distribution</param>
    /// <param name="value">the value</param>
    public void Add(double key, double value)
    {
      if (Double.IsNaN(key))
        return;
      ++sampleCount_;
      if (valueCount_ == 0)
      {
        valueCount_ = sampleCount_ = 1;
        sortedKeys_[0] = key;
        sortedValues_[0] = value;
        return;
      }
      if (isLowerTail_)
        AddLowerTail(key, value);
      else
        AddLowerTail(-key, value);
      return;
    }

    /// <summary>
    ///  Calculate the conditional expectation at the tail.
    /// </summary>
    /// <param name="quantile"></param>
    /// <returns>Conditional expectation.</returns>
    public double Conditional(double quantile)
    {
      Debug.Assert(quantile >= 0);
      double dpos = quantile*sampleCount_;
      var pos = (int) Math.Ceiling(dpos);
      if (pos == 0)
        return 0; // the case quantile = 0;
      if (pos > valueCount_)
        throw new ArgumentException("quantile is out of the valid range.");
      double sum = 0;
      for (int i = 0; i < pos; ++i)
      {
        sum += sortedValues_[i];
      }

      if (pos < valueCount_)
      {
        double part = dpos - pos + 1;
        double avg = (sum + part * sortedValues_[pos]) / (pos + part);
        return avg;
      }

      return sum/valueCount_;
    }

    private void AddLowerTail(double key, double value)
    {
      bool full = valueCount_ >= sortedKeys_.Length;
      bool bigger = sortedKeys_[valueCount_ - 1] <= key;
      if (bigger)
      {
        if (full)
          return;
        sortedKeys_[valueCount_] = key;
        sortedValues_[valueCount_] = value;
        ++valueCount_;
        return;
      }
      // The value is less than the largest value in the array.
      int pos = Array.BinarySearch(sortedKeys_, 0, valueCount_, key);
      if (pos < 0)
        pos = ~pos;
      if (pos >= valueCount_)
      {
        if (full)
          return;
        sortedKeys_[valueCount_] = key;
        sortedValues_[valueCount_] = value;
        ++valueCount_;
        return;
      }
      // shift values up to make space to insert new value
      for (int i = valueCount_; i > pos; --i)
      {
        // if we are shifting off the end, end value will be lost
        if (i < sortedKeys_.Length)
        {
          sortedKeys_[i] = sortedKeys_[i - 1];
          sortedValues_[i] = sortedValues_[i - 1];
        }
      }
      sortedKeys_[pos] = key;
      sortedValues_[pos] = value;
      if (full)
        return;
      ++valueCount_;
      return;
    }

    private readonly bool isLowerTail_;
    private readonly double[] sortedKeys_;
    private readonly double[] sortedValues_; 
    private int sampleCount_;
    private int valueCount_;
  } // class 
}