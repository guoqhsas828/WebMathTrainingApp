//
// IRateProjector.cs
//   2008-2014. All rights reserved.
//
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Interface for defining a rate projector which is intended to contain logic about determining rates on reset dates.
  /// </summary>
  public interface IRateProjector
  {
    /// <summary>
    /// Name of Index
    /// </summary>
    String IndexName { get; }

    /// <summary>
    /// Historical index fixings
    /// </summary>
    RateResets HistoricalObservations { get; }

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns></returns>
    Fixing Fixing(FixingSchedule fixingSchedule);

    /// <summary>
    /// Initialize fixing schedule
    /// </summary>
    /// <param name="prevPayDt">Previous payment date</param>
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="payDt">Payment date</param>
    /// <returns>Fixing schedule</returns>
    FixingSchedule GetFixingSchedule(Dt prevPayDt, Dt periodStart, Dt periodEnd, Dt payDt);

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns> Reset info for each component of the fixing</returns>
    List<RateResets.ResetInfo> GetResetInfo(FixingSchedule schedule);
  }
}