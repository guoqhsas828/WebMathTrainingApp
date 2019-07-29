/*
 * BumpSize.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Shared;

using BaseEntity.Toolkit.Calibrators.BaseCorrelation;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Bump size object
  /// </summary>
  [Serializable]
  public class BumpSize : BaseEntityObject
  {
    #region Constructors
    /// <summary>
    ///   Default constructor
    /// </summary>
    public BumpSize() { }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="size">Size to bump</param>
    /// <param name="unit">Unit of the size</param>
    public BumpSize(double size, BumpUnit unit)
    {
      size_ = size; unit_ = unit;
    }

    internal static BumpSize[] CreatArray(
      double[] bumpSizes, BumpUnit unit,
      double lowerBound, double upperBound)
    {
      if (bumpSizes == null) return null;

      BumpSize[] bumps = new BumpSize[bumpSizes.Length];
      for (int i = 0; i < bumpSizes.Length; ++i)
      {
        BumpSize b = new BumpSize();
        b.Size = bumpSizes[i];
        b.Unit = unit;
        b.LowerBound = lowerBound;
        b.UpperBound = upperBound;
      }
      return bumps;
    }
    internal static BumpSize[] CreatArray(
      double[] bumpSizes,
      BumpUnit[] units,
      double lowerBound, double upperBound)
    {
      if (bumpSizes == null) return null;

      BumpSize[] bumps = new BumpSize[bumpSizes.Length];
      for (int i = 0; i < bumpSizes.Length; ++i)
      {
        BumpSize b = new BumpSize();
        b.Size = bumpSizes[i];
        b.Unit = units == null ? BumpUnit.None : units[i];
        b.LowerBound = lowerBound;
        b.UpperBound = upperBound;
      }
      return bumps;
    }
    #endregion Constructors

    #region Methods
    internal double BumpAbsolute(double orig, QuotingConvention type)
    {
      if (Double.IsNaN(orig))
        return Double.NaN;
      double value = GetBump(type) + orig;
      if (value < lowerBound_)
        return lowerBound_;
      else if (value > upperBound_)
        return upperBound_;
      return value;
    }

    internal double BumpRelative(double orig, QuotingConvention type)
    {
      if (Double.IsNaN(orig))
        return Double.NaN;
      double bump = GetBump(type);
      bump = orig * ((bump > 0.0) ? bump : (1.0 / (1.0 - bump) - 1.0));
      double value = bump + orig;
      if (value < lowerBound_)
        return lowerBound_;
      else if (value > upperBound_)
        return upperBound_;
      return value;
    }

    internal double GetBump(QuotingConvention type)
    {
      switch (unit_)
      {
        case BumpUnit.BasisPoint:
          return size_ / 10000.0;
        case BumpUnit.Percentage:
          return size_ / 100.0;
        case BumpUnit.None:
          return size_;
        case BumpUnit.Natural:
          break;
      }

      // bump in natural unit
      switch (type)
      {
        case QuotingConvention.CreditSpread:
        case QuotingConvention.YieldSpread:
        case QuotingConvention.ZSpread:
          return size_ / 10000.0;
        case QuotingConvention.FlatPrice:
        case QuotingConvention.FullPrice:
        case QuotingConvention.Correlation:
        case QuotingConvention.Fee:
          return size_ / 100.0;
        default:
          return size_;
      }
    }

    internal double GetDiff(double orig, double bumped, QuotingConvention type)
    {
      double diff = bumped - orig;
      switch (unit_)
      {
        case BumpUnit.BasisPoint:
          return diff * 10000.0;
        case BumpUnit.Percentage:
          return diff * 100.0;
        case BumpUnit.None:
          return diff;
        case BumpUnit.Natural:
          break;
      }

      // return difference in natural unit
      switch (type)
      {
        case QuotingConvention.CreditSpread:
        case QuotingConvention.YieldSpread:
        case QuotingConvention.ZSpread:
          return diff * 10000.0;
        case QuotingConvention.FlatPrice:
        case QuotingConvention.FullPrice:
        case QuotingConvention.Correlation:
        case QuotingConvention.Fee:
          return diff * 100.0;
        default:
          return diff;
      }
    }

    internal void CheckBounds(QuotingConvention type)
    {
      if (Double.IsNaN(lowerBound_))
        lowerBound_ = type == QuotingConvention.Fee ? -1.0 : 0.0;
      if (Double.IsNaN(upperBound_))
        upperBound_ = type == QuotingConvention.Correlation ? 1.0 : Double.MaxValue;
      return;
    }
    #endregion Methods

    #region Properties
    /// <summary>
    ///   Deal premium
    /// </summary>
    public double Size
    {
      get { return size_; }
      set { size_ = value; }
    }

    /// <summary>
    ///   Quoted value
    /// </summary>
    public BumpUnit Unit
    {
      get { return unit_; }
      set { unit_ = value; }
    }

    /// <summary>
    ///   Lower bound of the bumped value
    /// </summary>
    public double LowerBound
    {
      get { return lowerBound_; }
      set { lowerBound_ = value; }
    }

    /// <summary>
    ///   Upper bound of the bumped value
    /// </summary>
    public double UpperBound
    {
      get { return upperBound_; }
      set { upperBound_ = value; }
    }
    #endregion Properties

    private double size_;
    private BumpUnit unit_;
    private double lowerBound_ = Double.NaN;
    private double upperBound_ = Double.NaN;
  };
}
