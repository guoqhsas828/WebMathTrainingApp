using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Dividend schedule extension methods
  /// </summary>
  public static class DividendScheduleExtensions
  {
    #region Methods

    /// <summary>
    /// Calculate Pv of stream of dividends
    /// </summary>
    /// <param name="dividendSchedule">The dividend schedule</param>
    /// <param name="spotDate">Spot date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="spot">Spot asset value</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <returns>Pv</returns>
    public static double Pv(this DividendSchedule dividendSchedule,
      Dt spotDate, Dt maturity, double spot, DiscountCurve discountCurve)
    {
      return dividendSchedule.Pv(spotDate, spot, discountCurve, spotDate, maturity);
    }

    /// <summary>
    /// Implied continuous dividend yield
    /// </summary>
    /// <param name="dividendSchedule">The dividend schedule</param>
    /// <param name="spotDate">Spot date</param>
    /// <param name="spot">Spot asset value</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="from">From date</param>
    /// <param name="to">To date</param>
    /// <returns>Equivalent dividend yield betwee from and to dates</returns>
    public static double EquivalentYield(this DividendSchedule dividendSchedule,
      Dt spotDate, double spot, DiscountCurve discountCurve, Dt from, Dt to)
    {
      if (to <= from || spot.AlmostEquals(0.0))
        return 0.0;
      double T = Dt.FractDiff(from, to) / 365.25;
      discountCurve = discountCurve ?? new DiscountCurve(dividendSchedule.AsOf, 0.0);
      double pv = dividendSchedule.Pv(spotDate, spot, discountCurve, from, to);
      return Math.Log(1.0 + pv / spot) / T;
    }

    #endregion
  }
}
