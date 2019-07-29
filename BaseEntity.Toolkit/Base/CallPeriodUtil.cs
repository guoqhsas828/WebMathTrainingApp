/*
 * CallPeriodUtil.cs
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
  ///   Utility methods for the <see cref="CallPeriod"/> class
  /// </summary>
  ///
  /// <seealso cref="CallPeriod"/>
  ///
  public static class CallPeriodUtil
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
    public static double CallPriceByDate(IList<CallPeriod> schedule, Dt date)
    {
      return schedule.ExercisePriceByDate(date);
    }

    /// <summary>
    ///   Validate CallPeriod schedule
    /// </summary>
    ///
    /// <param name="schedule">Amortization schedule</param>
    /// <param name="errors">List of errors to add to</param>
    ///
    public static void Validate(IList<CallPeriod> schedule, ArrayList errors)
    {
      schedule.Validate(errors, "CallPeriod schedule");
    }

  } // class CallPeriodUtil
}
