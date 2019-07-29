/*
 * DefaultSettleInfo.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id$
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///   Default settlement infomation
  ///   <preliminary />
  /// </summary>
  /// <remarks>
  ///   By convention, all losses and recoveries are normalized by the
  ///   basket total principal.
  /// </remarks>
  /// <exclude />
  [Serializable]
  internal class DefaultSettleInfo : BaseEntityObject
  {
    public override object Clone()
    {
      DefaultSettleInfo obj = (DefaultSettleInfo)base.Clone();
      obj.settleDates_ = CloneUtil.Clone(settleDates_);
      obj.defaultDates_ = CloneUtil.Clone(defaultDates_);
      obj.losses_ = CloneUtil.Clone(losses_);
      obj.recoveries_ = CloneUtil.Clone(recoveries_);
      obj.includeStartDates_ = CloneUtil.Clone(includeStartDates_);
      return obj;
    }
    public bool IsEmpty
    {
      get { return settleDates_.Length == 0; }
    }
    public Dt[] SettleDates
    {
      get { return settleDates_; }
    }
    public Dt[] DefaultDates
    {
      get { return defaultDates_; }
    }
    public double[] Recoveries
    {
      get { return recoveries_; }
    }
    public double[] Losses
    {
      get { return losses_; }
    }
    public bool[] IncludeStartDates
    {
      get { return includeStartDates_; }
    }
    private Dt[] settleDates_;
    private Dt[] defaultDates_;
    private double[] recoveries_;
    private double[] losses_;
    private bool[] includeStartDates_;

    internal class Builder
    {
      /// <summary>
      ///   Add an settle info item and sort the list by the settle dates
      /// </summary>
      /// <param name="settleDate">The date when the recovery settles</param>
      /// <param name="defaultDate">The date when the default occurs</param>
      /// <param name="recovery">Recovery amount to settle</param>
      /// <param name="loss">Loss amount to settle</param>
      /// <param name="includeStartDate">Whether to include this payment
      /// when it is on the pricer settle date.</param>
      internal void Add(Dt settleDate, Dt defaultDate,
        double recovery, double loss, bool includeStartDate)
      {
        // search for a place for insert the settle date
        int pos = 0, n = settleDates_.Count;
        for (; pos < n; ++pos)
        {
          // First try order by settle dates.
          var cmp = Dt.Cmp(settleDate, settleDates_[pos]);
          if (cmp < 0) break;
          if (cmp > 0) continue;
          // For the same settle dates, order by default dates
          cmp = Dt.Cmp(defaultDate, defaultDates_[pos]);
          if (cmp < 0) break;
          if (cmp > 0) continue;
          // For the same default and settle dates, order by includeStartDates.
          if (includeStartDates_[pos] && !includeStartDate)
            break;
        }
        settleDates_.Insert(pos, settleDate);
        defaultDates_.Insert(pos, defaultDate);
        recoveries_.Insert(pos, recovery);
        losses_.Insert(pos, loss);
        includeStartDates_.Insert(pos, includeStartDate);
      }

      /// <summary>
      ///   Construct an DefaultSettleInfo object from the builder 
      /// </summary>
      /// <returns>DefaultSettleInfo object</returns>
      internal DefaultSettleInfo ToDefaultSettleInfo(double totalPrincipal)
      {
        DefaultSettleInfo info = new DefaultSettleInfo();
        info.settleDates_ = settleDates_.ToArray();
        info.defaultDates_ = defaultDates_.ToArray();
        info.recoveries_ = recoveries_.ToArray();
        info.losses_ = losses_.ToArray();
        info.includeStartDates_ = includeStartDates_.ToArray();
        int N = info.settleDates_.Length;
        for (int i = 0; i < N; ++i)
        {
          info.Losses[i] /= totalPrincipal;
          info.Recoveries[i] /= totalPrincipal;
        }
        return info;
      }

      List<Dt> settleDates_ = new List<Dt>();
      List<Dt> defaultDates_ = new List<Dt>();
      List<double> recoveries_ = new List<double>();
      List<double> losses_ = new List<double>();
      List<bool> includeStartDates_ = new List<bool>();
    }

  } // class DefaultSettleInfo
}
