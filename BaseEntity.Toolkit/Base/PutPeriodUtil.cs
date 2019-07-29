/*
 * PutPeriodUtil.cs
 *
 *   2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{

  /// <summary>
  ///   Utility methods for the <see cref="PutPeriod"/> class
  /// </summary>
  ///
  /// <seealso cref="PutPeriod"/>
  ///
  public static class PutPeriodUtil
  {
    /// <summary>
    ///   Get call price by date from a list of call periods.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns zero if no call is active.</para>
    /// </remarks>
    ///
    /// <param name="schedule">Call schedule</param>
    /// <param name="date">Date at which call price is requested.</param>
    ///
    public static double
    PutPriceByDate(IList<PutPeriod> schedule, Dt date)
    {
      return schedule.ExercisePriceByDate(date);
    }

    /// <summary>
    ///   Validate PutPeriod schedule
    /// </summary>
    ///
    /// <param name="schedule">Amortization schedule</param>
    /// <param name="errors">List of errors to add to</param>
    ///
    public static void Validate(IList<PutPeriod> schedule, ArrayList errors)
    {
      schedule.Validate(errors, "PutPeriod schedule");
    }

  } // class PutPeriodUtil
}
