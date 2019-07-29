/*
 * SurvivalCurveScaling.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Helper class to bump on survival hazard rates directly
  /// </summary>
  internal class SurvivalCurveScaling
  {
    #region Index Enumerator
    /// <summary>
    ///   An enumerator representing a range of consecutive indices
    /// </summary>
    private class IndexEnumerator : IEnumerator<int>, IEnumerable<int>
    {
      public IndexEnumerator(int start, int end)
      {
        end_ = end; start_ = start; curr_ = start - 1;
      }
      private readonly int start_, end_;
      private int curr_;

      #region IEnumerator<int> Members

      public int Current
      {
        get
        {
          if (curr_ < start_ || curr_ >= end_)
            throw new InvalidOperationException();
          return curr_;
        }
      }

      #endregion

      #region IDisposable Members

      public void Dispose()
      {
        // nothing to do
      }

      #endregion

      #region IEnumerator Members

      object System.Collections.IEnumerator.Current
      {
        get
        {
          if (curr_ < start_ || curr_ >= end_)
            throw new InvalidOperationException();
          return curr_;
        }
      }

      public bool MoveNext()
      {
        ++curr_;
        if (curr_ >= end_)
          return false;
        return true;
      }

      public void Reset()
      {
        curr_ = start_ - 1;
      }

      #endregion

      #region IEnumerable<int> Members

      public IEnumerator<int> GetEnumerator()
      {
        return this;
      }

      #endregion

      #region IEnumerable Members

      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
      {
        return this;
      }

      #endregion
    }
    #endregion Index Enumerator

    #region Constructors
    /// <summary>
    ///   Default constructor
    /// </summary>
    private SurvivalCurveScaling() { }

    /// <summary>
    ///   Static constructor
    /// </summary>
    /// <param name="curve">The curve to scale</param>
    /// <returns>An object that does nothing</returns>
    internal SurvivalCurveScaling(SurvivalCurve curve)
    {
      scaleCurve_ = curve;      
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="curveToScale">The curve to scale, modified on output</param>
    /// <param name="curvePoints">
    ///   Optional curve points to replace all the curve points inside the curve
    /// </param>
    /// <param name="lastStartIndex">The start of the last date to scale to</param>
    internal SurvivalCurveScaling(SurvivalCurve curveToScale,
      CurvePoint[] curvePoints, int lastStartIndex)
    {
      scaleCurve_ = curveToScale;
      if (curvePoints != null)
      {
        curvePoints_ = curvePoints;
        scaleCurve_.Clear();
        for (int i = 0; i < curvePoints.Length; ++i)
          scaleCurve_.Add(curvePoints[i].Date, curvePoints[i].Value);
      }
      else
      {
        curvePoints_ = ArrayUtil.Generate<CurvePoint>(scaleCurve_.Count,
          delegate(int i) { return new CurvePoint(scaleCurve_.GetDt(i), scaleCurve_.GetVal(i)); });
      }
      startIndex_ = endIndex_ = 0;
      lastStartIndex_ = lastStartIndex;
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Get a bump range that starts from the stop date of the last bump
    ///   and ends with the first curve point no earlier than a given date.
    /// </summary>
    /// <param name="date">The CDX date</param>
    internal IEnumerable<int> GetBumpIndices(Dt date)
    {
      if (CurvePoints == null || CurvePoints.Length == 0)
        return null;
      SetBumpRange(date);
      return new IndexEnumerator(StartIndex, EndIndex);
    }

    /// <summary>
    ///   Set bump range to be starting from the stop date of the last bump
    ///   and ending with the first curve point no earlier than a given date.
    /// </summary>
    /// <param name="date">The date</param>
    internal void SetBumpRange(Dt date)
    {
      startIndex_ = endIndex_;
      if (curvePoints_ == null)
        return; // no scale is required
      int count = curvePoints_.Length;
      if (startIndex_ >= count
        || (startIndex_ > 0 && curvePoints_[startIndex_ - 1].Date >= date))
      {
        return; // nothing to bump
      }
      if (startIndex_ < lastStartIndex_)
      {
        for (int i = startIndex_; i < count; ++i)
          if (curvePoints_[i].Date >= date)
          {
            endIndex_ = i + 1;
            return;
          }
      }
      // Note:
      //   If startIndex >= lastStartIndex, then this is the
      //   last date to match and we bump every tenors up to the end.
      endIndex_ = count;
      return;
    }

    /// <summary>
    ///   Dumy function doing nothing
    /// </summary>
    /// <param name="x">Size to bump</param>
    /// <param name="relative">Relative or absolute</param>
    internal void Bump(double x, bool relative)
    {
      // no scaling
      Bump(x, relative, true);
    }

    /// <summary>
    ///   Dumy function doing nothing
    /// </summary>
    /// <param name="x">Size to bump</param>
    /// <param name="relative">Relative or absolute</param>
    /// <param name="refitSpread">Whether to refit the bumped spread</param>
    internal virtual void Bump(double x, bool relative, bool refitSpread)
    {
      // no scaling
      return;
    }

    #endregion Methods

    #region Properties

    internal SurvivalCurve ScaledCurve
    {
      get { return scaleCurve_; }
    }

    internal protected int StartIndex
    {
      get { return startIndex_; }
    }

    internal protected int EndIndex
    {
      get { return endIndex_; }
    }

    internal protected CurvePoint[] CurvePoints
    {
      get { return curvePoints_; }
    }
    #endregion Properties

    #region Data

    private int startIndex_, endIndex_;
    private readonly int lastStartIndex_;
    private SurvivalCurve scaleCurve_;
    private CurvePoint[] curvePoints_;

    #endregion Data
  }

}
