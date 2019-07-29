/*
 * ScaledSurvivalCurveBuilder.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;
using Cashflow = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Helper class to build a survival curve as a copy of an original curve,
  ///   possibly with some additional tenor points added to enclose a given 
  ///   array of reference dates.
  /// </summary>
  /// <remarks>
  ///   <para>Normally we want to scale a survival curve to match all the market quotes
  ///   on a set of reference dates.  This may not be possible if the curve points
  ///   do not enclose the reference dates.</para>
  /// 
  ///   <para>A curve is said enclosing a set of reference dates if it satisfies
  ///   the following conditions:</para>
  /// 
  ///   <para>(1) For any date <c>t</c> in the reference set, there is a curve point
  ///   with the date <c>d</c> such that <c>d &gt;= t</c>;</para>
  /// 
  ///   <para>(2) For any pair of dates <c>t1</c> and <c>t2</c>, <c>t1 &lt; t2</c>,
  ///    in the reference set, there is a curve point with the date <c>d</c> such that
  ///    <c> t1 &lt;= d &lt; t2</c>.</para>
  /// 
  ///   <para>This class tries to find a minimum set of additional date points such that
  ///   with these points added, the curve encloses a given set of reference dates.
  ///   For each of the additional dates, a CDS tenor is constructed with the break-even
  ///   premium implied by the original survival curve.
  ///   </para>
  /// </remarks>
  internal class ScaledSurvivalCurveBuilder
  {
    #region Internal Interfaces

    internal ScaledSurvivalCurveBuilder(
      SurvivalCurve originalCurve,
      Dt[] referenceDates,
      bool failOnInadequateTenors)
    {
      origCurve_ = originalCurve;
      dates_ = referenceDates;
      failOnInadequateTenors_ = failOnInadequateTenors;
      for (int i = 0; i < referenceDates.Length; ++i)
        AddDate(i);
      AddRemainingCurvePoints();
      return;
    }

    internal static void SynchronizeCurveQuotes(SurvivalCurve curve)
    {
      Calibrator cal = curve.Calibrator;
      int tenorCount = curve.Tenors.Count;
      for (int i = 0; i < tenorCount; ++i)
      {
        CurveTenor tenor = curve.Tenors[i];
        double prem = tenor.UpdateQuote(
          tenor.CurrentQuote.Type, curve, cal);
        if (prem < 0)
        {
          throw new ArgumentException(String.Format(
            "Negative spread ({0}) found at tenor {1} of curve {2}.",
            prem, tenor.Name, curve.Name));
        }
      }
      return;
    }

    internal CurvePoint[] CurvePoints
    {
      get { return curvePoints_.ToArray(); }
    }

    internal CurveTenorCollection Tenors
    {
      get
      {
        if (fullTenors_ == null)
          MergeTenors();
        return fullTenors_;
      }
    }

    internal int LastStartIndex
    {
      get { return lastStartIndex_; }
    }
    #endregion Internal Interfaces

    #region Private Methods

    private void AddDate(int dateIndex)
    {
      Dt date = dates_[dateIndex];
      startIndex_ = endIndex_;

      // Save the start point in the curve points array 
      lastStartIndex_ = curvePoints_.Count;

      // Find the curve point greater or equal the date
      Dt curveDate = Dt.Empty;
      int count = origCurve_.Count;
      for (int i = startIndex_; i < count; ++i)
      {
        curveDate = origCurve_.GetDt(i);
        if (curveDate >= date)
        {
          endIndex_ = i + 1;
          break;
        }
      }

      // If endIndex_ is not set to a larger value,
      // then we are at the end of the curve points.
      if (endIndex_ == startIndex_)
      {
        // Do we need to append a curve point?
        if (startIndex_ >= count)
        {
          if (failOnInadequateTenors_)
          {
            throw new ToolkitException(String.Format(
              "The curve {0} need more tenors at the end", origCurve_.Name));
          }
          Dt curveDt = AddCDS(date, dateIndex < dates_.Length - 1 ? dates_[dateIndex + 1] : Dt.MaxValue);
          curvePoints_.Add(new CurvePoint(curveDt, origCurve_.Interpolate(curveDt)));
          ++endIndex_;
        }
        else
        {
          endIndex_ = count;
          AddCurvePoints();
        }
        return;
      }

      // Do we need to add a curve point in the middle?
      if (dateIndex < dates_.Length - 1 && dates_[dateIndex + 1] <= curveDate)
      {
        if (failOnInadequateTenors_)
        {
          throw new ToolkitException(String.Format(
            "The curve {0} need more additional tenor between {1} and {2}",
            origCurve_.Name, date, dates_[dateIndex + 1]));
        }
        --endIndex_; // don't include the last curve date
        AddCurvePoints();
        Dt curveDt = AddCDS(date, dates_[dateIndex + 1]);
        curvePoints_.Add(new CurvePoint(curveDt, origCurve_.Interpolate(curveDt)));
        return;
      }

      // No need to add
      AddCurvePoints();
      return;
    }

    /// <summary>
    ///   Add a standard CDS with a maturity
    ///   in the date range [first, second)
    /// </summary>
    /// <returns>
    ///   The curve date correponding to the cds maturity.
    /// </returns>
    private Dt AddCDS(Dt first, Dt second)
    {
      CDS cds = CreateCDS(first);
      SetMaturity(cds, first, second);
      return AddCDS(cds);
    }

    /// <summary>
    ///   This function tries to set CDS maturity in the standard durations of
    ///   "6M", "1Y", "2Y", etc., and if no such maturity falls in the range
    ///   [first, second), it sets the maturity to the <c>first</c> date.
    /// </summary>
    private void SetMaturity(CDS cds, Dt first, Dt second)
    {
      Dt effective = cds.Effective;
      int months = 12 * (first.Year - effective.Year)
          + first.Month - effective.Month;
      Dt maturity = months <= 6 ? Dt.CDSMaturity(effective, 6, TimeUnit.Months)
        : Dt.CDSMaturity(effective, (months + 6) / 12, TimeUnit.Years);
      if (cds.Calendar != Calendar.None)
        maturity = Dt.Roll(maturity, BDConvention.Following, cds.Calendar);
      if (maturity < first)
      {
        int years = (11 + months) / 12;
        maturity = Dt.CDSMaturity(effective, years, TimeUnit.Years);
        if (cds.Calendar != Calendar.None)
          maturity = Dt.Roll(maturity, BDConvention.Following, cds.Calendar);
      }
      if (maturity >= first && maturity < second)
      {
        // regular case
        cds.Maturity = maturity;
        cds.Description = Utils.ToTenorName(effective, maturity, true);
      }
      else
      {
        // irregular case
        cds.Maturity = first;
        cds.Description = Utils.ToTenorName(effective, first, true)
          + "@" + curvePoints_.Count;
      }
      return;
    }

    private Dt AddCDS(CDS cds)
    {
      ICDSPricer pricer = (ICDSPricer)
        origCurve_.SurvivalCalibrator.GetPricer(origCurve_, cds);
      cds.Premium = pricer.BreakEvenPremium();
      CurveTenor tenor = new CurveTenor(cds.Description, cds, 0.0);
      tenor.ModelPv = pricer.Pv();
      newTenors_.Add(tenor);
      // get curve date
      Cashflow cf = ((ICashflowPricer)pricer).Cashflow;
      Dt curveDt = (SurvivalFitCalibrator.UseCashflowMaturity && cf.Count > 0)
        ? cf.GetDt(cf.Count - 1) : cds.Maturity;
      if (pricer.IncludeMaturityProtection)
        curveDt = Dt.Add(curveDt, 1);
      return curveDt;
    }

    private CDS CreateCDS(Dt maturity)
    {
      CDS cds = null;
      foreach (CurveTenor tenor in origCurve_.Tenors)
        if ((cds = tenor.Product as CDS) != null) break;
      if (cds != null)
        return new CDS(origCurve_.AsOf, maturity, cds.Ccy,
          0.0, cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar);
      return new CDS(origCurve_.AsOf, maturity, Currency.None,
        0.0, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.None);
    }

    private void AddCurvePoints()
    {
      for (int i = startIndex_; i < endIndex_; ++i)
        curvePoints_.Add(new CurvePoint(origCurve_.GetDt(i), origCurve_.GetVal(i)));
      return;
    }

    private void AddRemainingCurvePoints()
    {
      int count = origCurve_.Count;
      for (int i = endIndex_; i < count; ++i)
        curvePoints_.Add(new CurvePoint(origCurve_.GetDt(i), origCurve_.GetVal(i)));
    }

    private void MergeTenors()
    {
      int count = newTenors_.Count;
      if (count == 0)
      {
        fullTenors_ = (CurveTenorCollection)origCurve_.Tenors.Clone();
        return;
      }

      CurveTenorCollection tenors = new CurveTenorCollection();
      int first = 0;
      foreach (CurveTenor tenor in origCurve_.Tenors)
      {
        first = AddNewTenors(tenors, tenor.Product.Maturity, first);
        tenors.Add(tenor);
      }
      AddNewTenors(tenors, Dt.MaxValue, first);
      fullTenors_= tenors;
      return;
    }

    private int AddNewTenors(CurveTenorCollection tenors, Dt date, int first)
    {
      int count = newTenors_.Count;
      for (; first < count; ++first)
      {
        if (newTenors_[first].Product.Maturity < date)
          tenors.Add(newTenors_[first]);
        else
          break;
      }
      return first;
    }

    #endregion Private Methods

    #region Data

    SurvivalCurve origCurve_;
    Dt[] dates_;
    int startIndex_ = 0, endIndex_ = 0, lastStartIndex_ = 0;
    List<CurvePoint> curvePoints_ = new List<CurvePoint>();
    CurveTenorCollection newTenors_ = new CurveTenorCollection();
    CurveTenorCollection fullTenors_ = null;
    bool failOnInadequateTenors_ = true;
    #endregion Data
  }
}
