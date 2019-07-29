using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  ///  An interface for lockable floating rates.
  /// </summary>
  internal interface IRatesLockable
  {
    /// <summary>
    /// Gets or sets the locked rates.
    /// </summary>
    /// <value>The locked rates.</value>
    RateResets LockedRates{ get; set;}
    /// <summary>
    /// Gets the projected rates.
    /// </summary>
    /// <value>The projected rates.</value>
    IEnumerable<RateReset> ProjectedRates { get; }
  }

  #region Extension methods for lockable rates
  internal static class LockableRatesUpdaterUtility
  {
    public static IEnumerable<RateReset> EnumerateProjectedRates(
      this PaymentSchedule ps)
    {
      if (ps == null) yield break;
      foreach (Dt d in ps.GetPaymentDates())
      {
        foreach (Payment p in ps.GetPaymentsOnDate(d))
        {
          var fip = p as FloatingInterestPayment;
          if (fip == null || !fip.IsProjected) continue;
          yield return new RateReset(fip.ResetDate, fip.EffectiveRate);
        }
      }
    }

    public static void Initialize(this IList<RateReset> rateResets, RateResets data)
    {
      rateResets.Clear();
      if (data == null || data.AllResets == null) return;
      foreach (var d in data.AllResets)
      {
        rateResets.Add(new RateReset(d.Key, d.Value));
      }
    }
  #endregion
  } //static class RateResetsUpdaterUtility
}
