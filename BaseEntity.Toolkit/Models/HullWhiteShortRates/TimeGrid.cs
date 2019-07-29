//
//   2017. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using CashflowAdapter = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  /// <summary>
  /// Build time grids for tree models
  /// </summary>
  internal struct TimeGrid
  {
    #region Methods

    private static BusinessDays GetNotificationDays(ICallable callable)
    {
      if (callable != null && callable.NotificationDays > 0)
      {
        Calendar calendar = Calendar.None;
        if (callable.HasPropertyOrField("Calendar"))
        {
          var cal = callable.GetValue<object>("Calendar") as Calendar?;
          if (cal != null) calendar = cal.Value;
        }
        return new BusinessDays(callable.NotificationDays, calendar);
      }
      return new BusinessDays();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the call prices.
    /// </summary>
    /// <value>The call prices.</value>
    public double[] CallPrices { get; private set; }

    /// <summary>
    /// Gets the time grid dates.
    /// </summary>
    /// <value>The dates.</value>
    public Dt[] Dates { get; private set; }

    /// <summary>
    /// Gets the notifications dates.
    /// </summary>
    /// <value>The dates.</value>
    public Dt[] NoticeDates { get; private set; }

    /// <summary>
    /// Gets the time in years by time grid dates.
    /// </summary>
    /// <value>The time.</value>
    public double[] Time { get; private set; }

    /// <summary>
    /// Gets the coupon and principal payments by time grid dates.
    /// </summary>
    /// <value>The payments.</value>
    public double[] Payments { get; private set; }

    /// <summary>
    /// Gets the accrued by time grid dates.
    /// </summary>
    /// <value>The accrued.</value>
    public double[] Accrued { get; private set; }

    /// <summary>
    /// Gets the contingent default amounts by time grid dates.
    /// </summary>
    /// <value>The default amounts.</value>
    public double[] DefaultAmounts { get; private set; }

    #endregion

    #region The new builder

    internal void Build(
      CashflowAdapter cf, ICallable callable,
      Dt settle, double accrued)
    {
      var infos = GetDateInfo(cf, callable, settle);
      var count = infos.Count;

      var dates = Dates = new Dt[count];
      var noticeDates = NoticeDates = new Dt[count];
      var callPrices = CallPrices =
        (callable == null ? null : new double[count]);

      var timeGridPayments = Payments = new double[count];
      var timeGridAccrued = Accrued = new double[count];
      var defaultAmts = DefaultAmounts = new double[count];

      // time zero payment
      timeGridAccrued[0] = accrued; // this should add to strike price.
      var lastDefaultAmount = cf.GetDefaultAmount(0);

      var remainingNotional = cf.GetPrincipalAt(0);
      var notificationDays = GetNotificationDays(callable);
      for (int i = 0, cfCount = cf.Count; i < count; ++i)
      {
        var info = infos[i];
        Dt date = dates[i] = info.Date;

        var idx = infos[i].CouponIndex;
        if (idx >= 0)
        {
          var nextIdx = idx + 1;
          if (nextIdx < cf.Count)
          {
            remainingNotional = (nextIdx >= cfCount ?
              0.0 : cf.GetPrincipalAt(nextIdx));
            lastDefaultAmount = cf.GetDefaultAmount(i > idx ? nextIdx : idx);
          }
          else
          {
            remainingNotional = 0;
          }
          timeGridPayments[i] = cf.GetAccrued(idx) + cf.GetAmount(idx);
        }
        defaultAmts[i] = lastDefaultAmount;

        if (callable != null)
        {
          var price = info.CouponIndex >= 0
            ? callable.GetExercisePriceByDate(date) : info.CallPrice;

          if (double.IsNaN(price) || remainingNotional.Equals(0.0))
          {
            callPrices[i] = double.NaN;
          }
          else
          {
            callPrices[i] = price*remainingNotional;
            noticeDates[i] = date - notificationDays;
          }
        }
      }

      Time = Array.ConvertAll(dates, dt => Dt.TimeInYears(settle, dt));
    }

    private static IReadOnlyList<DateInfo> GetDateInfo(
      CashflowAdapter cf, ICallable callable, Dt settle)
    {
      var infos = new UniqueSequence<DateInfo>();
      int count = cf.Count;
      for (int i = 0; i < count; ++i)
      {
        var date = cf.GetDt(i);
        Debug.Assert(date >= settle);
        infos.Add(new DateInfo(date, i));
      }

      var periodStartIndex = 0;
      var exerciseSchedule = callable?.ExerciseSchedule;
      if (exerciseSchedule != null)
      {
        foreach (var period in exerciseSchedule)
        {
          var date = period.StartDate;
          if (date < settle || date != period.EndDate ||
            double.IsNaN(period.ExercisePrice))
          {
            continue;
          }

          var info = new DateInfo(date, -1, period.ExercisePrice);
          var index = infos.IndexOf(info);
          if (index >= 0)
          {
            // This is on the coupon date
            infos[index].CallPrice = period.ExercisePrice;
            continue;
          }

          // The call date is in the middle of a coupon period
          if (!callable.FullExercisePrice)
          {
            info.CallPrice += GetPartialAccruedPerUnitNotional(
              cf, info.Date, ref periodStartIndex);
          }
          infos.Add(info);
        }
      }

      if (infos[0].Date > settle) infos.Add(new DateInfo(settle));
      return infos;
    }

    private static double GetPartialAccruedPerUnitNotional(
      CashflowAdapter cf, Dt date, ref int startIndex)
    {
      int last = cf.Count - 1;
      if (last < 0) return 0;

      for (int i = startIndex; i <= last; ++i)
      {
        Dt end = cf.GetEndDt(i);
        if (date > end) continue;

        Dt begin = cf.GetStartDt(i);
        if (date <= begin)
        {
          // Before accrual start.  It must be the first period.
          Debug.Assert(i == 0);
          return 0;
        }

        // Now we have: begin < date <= end
        startIndex = i; // remember the start index for later use.

        var dc = cf.GetDayCount(i);
        const Frequency freq = Frequency.None;
        var frac = Dt.Fraction(begin, end, begin, date, dc, freq)
          /Dt.Fraction(begin, end, begin, end, dc, freq);
        return frac*cf.GetAccrued(i)/cf.GetPrincipalAt(i);
      }

      // If we are here, it must be in the last period.
      Debug.Assert(date >= cf.GetEndDt(last));
      startIndex = last;
      return 0;
    }

    private class DateInfo : IComparable<DateInfo>
    {
      /// <summary>
      /// The coupon date or call date
      /// </summary>
      public readonly Dt Date;

      /// <summary>
      /// The cash flow index, or -1 if not on a cash flow date
      /// </summary>
      public readonly int CouponIndex;

      /// <summary>
      /// The call price, or NaN if not on a known exercise date
      /// </summary>
      public double CallPrice;

      public DateInfo(Dt date, int index = -1, double price = double.NaN)
      {
        Date = date;
        CouponIndex = index;
        CallPrice = price;
      }

      public int CompareTo(DateInfo other)
      {
        return Date.CompareTo(other.Date);
      }
    }

    #endregion
  }
}
