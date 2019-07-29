/*
 * End of year effect
 * 
 *  -2011. All rights reserved.
 * 
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Turn of year effect
  /// </summary>
  [Serializable]
  public class TurnOfYearEffect
  {
    #region Data

    private readonly Calendar calendar_;
    private readonly DayCount dc_;
    private readonly Dt[] jumpEndDts_;
    private readonly double[] jumpSize_;
    private readonly Dt[] jumpStartDts_;
    private readonly Dt settle_;
    private readonly Dt[] years_;

    #endregion

    ///<summary>
    ///  Simplified Constructor for constant year end turn effect
    ///</summary>
    ///<param name = "settle">Settle date</param>
    ///<param name = "calendar">Calendar</param>
    ///<param name = "dc">Day count convention</param>
    ///<param name = "jumpSize">jump size</param>
    public TurnOfYearEffect(Dt settle, Calendar calendar, DayCount dc, double jumpSize)
    {
      jumpStartDts_ = null;
      jumpEndDts_ = null;
      years_ = new Dt[50];
      jumpSize_ = new double[50];
      for (int i = 0; i < 50; i++)
      {
        years_[i] = new Dt(31, 12, settle.Year + i);
        jumpSize_[i] = jumpSize;
      }
      settle_ = settle;
      calendar_ = calendar;
      dc_ = dc;
      return;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name = "settle"> Settle dates </param>
    /// <param name = "years"> Jump dates</param>
    /// <param name = "calendar">Calendar</param>
    /// <param name = "dc">Day count convention</param>
    /// <param name = "jumpSize">Array of jump sizes</param>
    public TurnOfYearEffect(Dt settle, Dt[] years, Calendar calendar, DayCount dc, double[] jumpSize)
    {
      if (years.Length != jumpSize.Length)
        throw new ToolkitException("Number of years should be the same as the number of jump sizes");
      jumpSize_ = jumpSize;
      jumpStartDts_ = null;
      jumpEndDts_ = null;
      calendar_ = calendar;
      years_ = years;
      settle_ = settle;
      dc_ = dc;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name = "settle">Settle date</param>
    /// <param name = "calendar">Calendar</param>
    /// <param name = "dc">Day count convention</param>
    /// <param name = "jumpSize">Array of jump sizes</param>
    /// <param name = "jumpStartDts">Jump start dates</param>
    /// <param name = "jumpEndDts"> Jump end dates</param>
    public TurnOfYearEffect(Dt settle, Calendar calendar, DayCount dc, double[] jumpSize, Dt[] jumpStartDts,
                            Dt[] jumpEndDts)
    {
      if (jumpStartDts.Length > 0 && !(jumpStartDts.Length == jumpEndDts.Length && jumpEndDts.Length == jumpSize.Length))
        throw new ArgumentException("Number of dates should match number of jump sizes");
      jumpSize_ = jumpSize;
      jumpStartDts_ = jumpStartDts;
      jumpEndDts_ = jumpEndDts;
      calendar_ = calendar;
      settle_ = settle;
      dc_ = dc;
    }

    /// <summary>
    ///   Computes the turn of year adjustment
    /// </summary>
    /// <returns>Total turn of year adjustment curve</returns>
    public Curve TurnOfYearAdjustment()
    {
      var dates = new List<Dt>();
      var adj = new List<double>();
      Dt lastBd, firstBd;
      var retVal = new Curve(settle_, new Flat(), dc_, Frequency.Continuous) { Name = "TurnOfYearAdjustments" };
      bool datesAreFixed = (jumpStartDts_ != null && jumpStartDts_.Length > 0);
      for (int i = 0; i < jumpSize_.Length; i++)
      {
        lastBd = datesAreFixed
                   ? jumpStartDts_[i]
                   : Dt.Roll(new Dt(31, 12, years_[i].Year), BDConvention.Modified, calendar_);
        firstBd = datesAreFixed
                    ? jumpEndDts_[i]
                    : Dt.Roll(new Dt(1, 1, years_[i].Year + 1), BDConvention.Modified, calendar_);
        dates.Add(new Dt(lastBd.Day - 1, lastBd.Month, lastBd.Year));
        adj.Add(0.0);
        dates.Add(lastBd);
        adj.Add(jumpSize_[i]);
        dates.Add(firstBd);
        adj.Add(jumpSize_[i]);
        dates.Add(new Dt(firstBd.Day + 1, firstBd.Month, firstBd.Year));
        adj.Add(0.0);
      }
      var adjZeroes = new double[adj.Count];
      adjZeroes[0] = 1;
      for (int i = 1; i < adj.Count; i++)
        adjZeroes[i] = adjZeroes[i - 1]*Math.Exp(- adj[i - 1]*Dt.Years(dates[i - 1], dates[i], dc_));
      if (dates.Count > 0 && settle_ > dates[0])
        retVal.AsOf = dates[0];
      retVal.Add(dates.ToArray(), adjZeroes);
      return retVal;
    }
  }
}