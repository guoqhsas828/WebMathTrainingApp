/*
 * BasketDefaultInfo.cs
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
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Curves;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///   Summary info about defaulted names in a basket
  /// </summary>
  /// <remarks>
  ///   By convention, all losses and amortizations are normalized by the
  ///   basket total principal.
  /// </remarks>
  [Serializable]
  internal class BasketDefaultInfo : BaseEntityObject
  {
    #region Public Methods
    /// <summary>
    /// Return a new object that is a deep copy of this instance
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the lifecycle of the referenced object).
    /// </remarks>
    public override object Clone()
    {
      BasketDefaultInfo obj = (BasketDefaultInfo)base.Clone();
      obj.dates_ = CloneUtil.Clone(dates_);
      obj.cumuLosses_ = CloneUtil.Clone(cumuLosses_);
      obj.cumuAmorts_ = CloneUtil.Clone(cumuAmorts_);
      obj.cumuDflts_ = CloneUtil.Clone(cumuDflts_);
      obj.settleInfo_ = (DefaultSettleInfo)settleInfo_.Clone();
      return obj;
    }

    /// <summary>
    ///   Build BasketDefaultInfo object from an array of survival curves
    /// </summary>
    /// <param name="survivalCurves">Array of survival curves</param>
    /// <param name="recoveryRates">Array of recovery rates</param>
    /// <param name="principals">Array of principals</param>
    /// <returns>BasketDefaultInfo object</returns>
    public static BasketDefaultInfo BuildFrom(
      SurvivalCurve[] survivalCurves,
      double[] recoveryRates,
      double[] principals)
    {
      Builder build = new Builder();
      double totalPrincipal = 0;
      int N = survivalCurves.Length;
      for (int i = 0; i < N; ++i)
      {
        if (!survivalCurves[i].DefaultDate.IsEmpty())
        {
          Dt defaultDate = survivalCurves[i].DefaultDate;
          double recoveryRate = recoveryRates[i];
          double amor = principals[i] * recoveryRate;
          double loss = principals[i] - amor;
          build.Add(defaultDate, loss, amor);

          if (survivalCurves[i].SurvivalCalibrator == null)
            continue;
          RecoveryCurve rc = survivalCurves[i].SurvivalCalibrator.RecoveryCurve;
          if (rc == null || rc.JumpDate.IsEmpty())
            continue;
          build.AddSettle(rc.JumpDate, defaultDate, amor, loss);
        }
        totalPrincipal += principals[i];
      }
      return build.ToDefaultInfo(totalPrincipal);
    }

    /// <summary>
    ///   Calculate accrual fraction with the relevant defaulted names included
    /// </summary>
    /// <param name="begin">Accrual begin date</param>
    /// <param name="end">Accrual end date</param>
    /// <param name="dayCount">Day count</param>
    /// <param name="ap">Tranche attachment point</param>
    /// <param name="dp">Tranche detachment point</param>
    /// <returns>Accrual fraction</returns>
    public double AccrualFraction(
      Dt begin, Dt end, DayCount dayCount,
      double ap, double dp)
    {
      return AccrualTillDefaultSettled
        ? AccrualFractionToSettleDate(begin, end, dayCount, ap, dp)
        : AccrualFractionToDefaultDate(begin, end, dayCount, ap, dp);
    }

    private double AccrualFractionToDefaultDate(
      Dt begin, Dt end, DayCount dayCount,
      double ap, double dp)
    {
      Dt[] dates = dates_;
      if (dates == null || dates.Length <= 0)
        return Dt.Fraction(begin, end, dayCount);

      double accrual = 0, preLoss = 0, preAmor = 0, survival = 1;
      int N = dates.Length;
      for (int i = 0; i < N; ++i)
      {
        Dt defaultDate = dates[i];
        if (defaultDate >= end)
          break; // we do the last date outside the loop
        else if (defaultDate > begin)
        {
          survival = BasketPricer.TrancheSurvival(preLoss, preAmor, ap, dp);
          accrual += survival * Dt.Fraction(begin, defaultDate, dayCount);
          begin = defaultDate;
        }
        preLoss = cumuLosses_[i];
        preAmor = cumuAmorts_[i];
      }
      // include the last default date
      survival = BasketPricer.TrancheSurvival(preLoss, preAmor, ap, dp);
      accrual += survival * Dt.Fraction(begin, end, dayCount);
      return accrual;
    }

    // This is the flag defined in the ToolkitConfig
    private static readonly bool AccrualTillDefaultSettled = ToolkitConfigurator.Settings.SyntheticCDOPricer.SupportAccrualRebateAfterDefault;

    /// <summary>
    ///  Calculate the average accrual fraction such that
    ///   (1) For the defaults not settled before the end date,
    ///       accrual to the end date (full accrual);
    ///   (2) For the defaults already settled before the end date,
    ///       accrual to the default date (partial accrual).
    /// </summary>
    /// <param name="begin">Accrual begin date</param>
    /// <param name="end">Calculation end date (or the pricer settle date)</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="ap">The attachment point</param>
    /// <param name="dp">The detachment point</param>
    /// <returns>System.Double.</returns>
    private double AccrualFractionToSettleDate(
      Dt begin, Dt end, DayCount dayCount,
      double ap, double dp)
    {
      Dt[] dates = dates_;
      if (dates == null || dates.Length <= 0)
        return Dt.Fraction(begin, end, dayCount);

      double accrual = 0, preDefaultPrincipal = 0, survival = 1;
      int N = dates.Length;
      for (int i = 0; i < N; ++i)
      {
        Dt defaultDate = dates[i];
        if (defaultDate >= end)
          break; // we do the last date outside the loop

        double loss = cumuLosses_[i], amor = cumuAmorts_[i];
        if (defaultDate > begin)
        {
          var defaultPrincipal = loss + amor - preDefaultPrincipal;
          var remain = BasketPricer.TrancheSurvival(loss, amor, ap, dp);
          accrual += (survival - remain)*CalculateAccrualFraction(
            defaultDate, defaultPrincipal, begin, end, dayCount);
          survival = remain;
        }
        else
        {
          survival = BasketPricer.TrancheSurvival(loss, amor, ap, dp);
        }
        preDefaultPrincipal = loss + amor;
      }
      // include the last default date
      accrual += survival*Dt.Fraction(begin, end, dayCount);
      return accrual;
    }

    /// <summary>
    /// Calculates the average accrual fraction for possibly
    ///  multiple defaults on the same date
    /// </summary>
    /// <param name="defaultDate">The default date</param>
    /// <param name="totalDefaultPrincipal">The default principal</param>
    /// <param name="begin">Accrual begin date</param>
    /// <param name="end">Calculation end date (or the pricer settle date)</param>
    /// <param name="dayCount">The day count.</param>
    /// <returns>System.Double.</returns>
    private double CalculateAccrualFraction(
      Dt defaultDate, double totalDefaultPrincipal,
      Dt begin, Dt end, DayCount dayCount)
    {
      var info = SettlementInfo;
      if (info == null || info.IsEmpty)
        return Dt.Fraction(begin, defaultDate, dayCount);

      var principalUnsettled = 0.0;
      var settles = info.SettleDates;
      for (int i = 0, n = settles.Length; i < n; ++i)
      {
        if (info.DefaultDates[i] != defaultDate || settles[i] < end)
          continue;
        principalUnsettled += info.Losses[i] + info.Recoveries[i];
      }

      if (principalUnsettled <= 0)
        return Dt.Fraction(begin, defaultDate, dayCount);
      if (principalUnsettled >= totalDefaultPrincipal)
        return Dt.Fraction(begin, end, dayCount);

      var p = principalUnsettled/totalDefaultPrincipal;
      return p*Dt.Fraction(begin, end, dayCount)
        + (1 - p)*Dt.Fraction(begin, defaultDate, dayCount);
    }

    /// <summary>
    ///   Calculate the pv of the values from the names
    ///   which have defaulted but need to be settled after the pricer settle date.
    /// </summary>
    /// <param name="asOf">Pricing date (all values are discounted back to this date)</param>
    /// <param name="settle">Pricer settle date</param>
    /// <param name="maturity">Pricer maturity date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="ap">Tranche attachment</param>
    /// <param name="dp">Tranche detachment</param>
    /// <param name="includeLoss">True if include the (negative) loss values</param>
    /// <param name="includeRecovery">True if include the (positive) recovery values</param>
    /// <returns>Pv of the settlement values</returns>
    public double DefaultSettlementPv(
      Dt asOf, Dt settle, Dt maturity,
      DiscountCurve discountCurve,
      double ap, double dp, bool includeLoss, bool includeRecovery)
    {
      if (dp <= ap) return 0;

      DefaultSettleInfo info = SettlementInfo;
      if (info == null || info.IsEmpty)
        return 0;

      Dt[] dates = info.SettleDates;
      bool[] includeSettle = info.IncludeStartDates;

      // find the first date
      int N = dates.Length;
      int first = N;
      for (int i = 0; i < N; ++i)
      {
        int cmp = Dt.Cmp(dates[i], settle);
        if (cmp > 0 || (cmp == 0 && (includeSettle[i]
          // include the default settled on pricer settle
          // if it is defaulted before the date
          || info.DefaultDates[i] < settle))) 
        {
          first = i;
          break;
        }
      }

      // work through all the relevant settle dates
      bool settleOnMaturity = (settle == maturity);
      double pv = 0;
      for (int i = first; i < N; ++i)
      {
        // In the special case defaultSettle == pricerSettle == maturity,
        // we don't include the loss, in order to be consistent with CDO pricing
        // where it hard codes includeSettle to false and the price in the special
        // case is always zero.
        if (dates[i] > maturity || (dates[i] == settle && settleOnMaturity))
        {
          // default settled after maturity is excluded only when
          // it is defaulted on or after maturity
          if (info.DefaultDates[i] >= maturity) break;
        }
        if (info.DefaultDates[i] > settle) continue;

        // Find index of the default date in the Dates array
        // for retrieving the cumulative loss/amortization informations
        int pos = Array.BinarySearch(dates_, info.DefaultDates[i]);
        if (pos < 0)
          throw new System.InvalidOperationException(
            "Corrupted or badly constructed BasketDefaultInfo");
        double preLoss = 0, preAmor = 0;
        if (pos > 0)
        {
          preLoss = CumulativeLosses[pos - 1];
          preAmor = CumulativeAmorts[pos - 1];
        }
        double newLoss = CumulativeLosses[pos] - preLoss;
        double newAmor = CumulativeAmorts[pos] - preAmor;

        // Calculate the unsettled loss/recoveries to the tranche
        double v = 0;
        if (includeLoss && info.Losses[i] != 0)
          v -= TrancheLoss(ap, dp, preLoss, newLoss) * info.Losses[i] / newLoss;
        if (includeRecovery && info.Recoveries[i] != 0)
          v += TrancheLoss(1-dp, 1-ap, preAmor, newAmor) * info.Recoveries[i] / newAmor;
        if (discountCurve != null)
          pv += v * discountCurve.DiscountFactor(asOf, dates[i]);
        else
          pv += v;
      }

      // normalized to be based on unit tranche (original) notional
      return pv / (dp - ap);
    }

    private static double TrancheLoss(
      double ap, double dp, double preLoss, double newLoss)
    {
      if (ap > preLoss)
      {
        ap -= preLoss;
        dp -= preLoss;
      }
      else if (dp > preLoss)
      {
        ap = 0;
        dp -= preLoss;
      }
      else
        return 0; // tranche already exhausted

      if (newLoss <= ap)
        return 0; // not reach the tranche yet
      newLoss -= ap;
      dp -= ap;
      if (newLoss > dp)
        newLoss = dp; // truncate the loss at the detachment
      return newLoss;
    }

    internal double UnsettledAccrualAdjustment(Dt payDt,
      Dt begin, Dt end, DayCount dayCount,
      double ap, double dp,
      DiscountCurve discountCurve)
    {
      Dt[] dates = dates_;
      if (dates == null || dates.Length <= 0)
        return 0;

      double payDf = discountCurve.DiscountFactor(payDt),
        discountedFullFraction = Dt.Fraction(begin, end, dayCount)*payDf,
        adjustment = 0, preLoss = 0, preAmor = 0;
      int count = dates.Length;
      for (int i = 0; i < count; ++i)
      {
        Dt defaultDate = dates[i];
        if (defaultDate >= end)
          break;

        if (defaultDate > begin)
        {
          double loss = cumuLosses_[i], amor = cumuAmorts_[i],
            affected = BasketPricer.TrancheSurvival(preLoss, preAmor, ap, dp)
              - BasketPricer.TrancheSurvival(loss, amor, ap, dp);
          if (affected <= 0)
          {
            // The tranche is not affected.
            preLoss = loss;
            preAmor = amor;
            continue;
          }
          var thisDefaultSize = (loss + amor) - (preLoss + preAmor);
          adjustment += affected*CalculateUnsettledAccrualAdjustment(
            begin, end, dayCount, defaultDate, thisDefaultSize,
            discountCurve, discountedFullFraction);
        }
        preLoss = cumuLosses_[i];
        preAmor = cumuAmorts_[i];
      }
      return adjustment;
    }

    private double CalculateUnsettledAccrualAdjustment(
      Dt begin, Dt end, DayCount dayCount,
      Dt defaultDate, double totalDefaultSize,
      DiscountCurve discountCurve, double discountedFullFraction)
    {
      double rebateFraction = Dt.Fraction(begin, end,
        defaultDate, end, dayCount, Frequency.None),
        sumPrincipal = 0, sumAdjustment = 0;
      foreach (var settle in GetPrincipalBySettleDates(defaultDate))
      {
        if (settle.Date < end) continue;
        var principal = settle.Value;
        sumAdjustment += principal*(discountedFullFraction
          - rebateFraction*discountCurve.DiscountFactor(settle.Date));
        sumPrincipal += principal;
      }
      return sumAdjustment/Math.Max(sumPrincipal, totalDefaultSize);
    }

    private IEnumerable<CurvePoint> GetPrincipalBySettleDates(Dt defaultDate)
    {
      var settleInfo = SettlementInfo;
      if (settleInfo == null)
      {
        yield break;
      }
      var defaultDates = settleInfo.DefaultDates;
      if (defaultDates == null || defaultDates.Length == 0)
      {
        yield break;
      }
      for (int i = 0, n = defaultDates.Length; i < n; ++i)
      {
        if (defaultDates[i] != defaultDate) continue;
        yield return new CurvePoint(settleInfo.SettleDates[i],
          settleInfo.Losses[i] + settleInfo.Recoveries[i]);
      }
    }

    #endregion Public Methods

    #region Public Properties
    /// <summary>
    ///   Whether the there is no default
    /// </summary>
    public bool IsEmpty
    {
      get { return dates_.Length == 0; }
    }

    /// <summary>
    ///   Array of default dates
    /// </summary>
    public Dt[] Dates
    {
      get { return dates_; }
    }

    /// <summary>
    ///   Array of cumulative losses.
    /// </summary>
    public double[] CumulativeLosses
    {
      get { return cumuLosses_; }
    }

    /// <summary>
    ///   Array of cumulative amortizations.
    /// </summary>
    public double[] CumulativeAmorts
    {
      get { return cumuAmorts_; }
    }

    /// <summary>
    ///   Array of cumulative default counts.
    /// </summary>
    public int[] CumulativeDefaultCounts
    {
      get { return cumuDflts_; }
    }

    /// <summary>
    ///   Object representing default settle dates and amounts
    /// </summary>
    public DefaultSettleInfo SettlementInfo
    {
      get { return settleInfo_; }
    }

    #endregion Public Properties

    #region Private types
    private class LossAmor
    {
      internal double Loss;
      internal double Amor;
      internal int Count;
    }

    internal class Builder
    {
      public void Add(Dt date, double loss, double amor)
      {
        int defaultDate = date.ToInt();
        LossAmor la;
        if (dist.TryGetValue(defaultDate, out la))
        {
          la.Amor += amor;
          la.Loss += loss;
          la.Count += 1;
        }
        else
        {
          la = new LossAmor();
          la.Amor = amor;
          la.Loss = loss;
          la.Count = 1;
          dist.Add(defaultDate, la);
        }
        return;
      }

      public void AddSettle(Dt setlleDate, Dt defaultDate, double recovery, double loss)
      {
        settleBuilder.Add(setlleDate, defaultDate, recovery, loss, false);
      }
      public void AddSettle(Dt setlleDate, Dt defaultDate,
        double recovery, double loss, bool inclStart)
      {
        settleBuilder.Add(setlleDate, defaultDate, recovery, loss, inclStart);
      }

      public BasketDefaultInfo ToDefaultInfo(double totalPrincipal)
      {
        if (totalPrincipal <= 0)
          totalPrincipal = 1.0;

        int N = dist.Count;
        Dt[] dates = new Dt[N];
        double[] losses = new double[N];
        double[] amorts = new double[N];
        int[] counts = new int[N];
        double cumuLoss = 0, cumuAmor = 0;
        int cumuCount = 0;
        IList<int> keys = dist.Keys;
        for (int i = 0; i < N; ++i)
        {
          dates[i] = new Dt(keys[i]);
          LossAmor la = dist[keys[i]];
          losses[i] = (cumuLoss += la.Loss) / totalPrincipal;
          amorts[i] = (cumuAmor += la.Amor) / totalPrincipal;
          counts[i] = (cumuCount += la.Count);
        }

        BasketDefaultInfo info = new BasketDefaultInfo();
        info.dates_ = dates;
        info.cumuLosses_ = losses;
        info.cumuAmorts_ = amorts;
        info.cumuDflts_ = counts;
        info.settleInfo_ = settleBuilder.ToDefaultSettleInfo(totalPrincipal);
        return info;
      }

      private SortedList<int, LossAmor> dist = new SortedList<int, LossAmor>();
      private DefaultSettleInfo.Builder settleBuilder = new DefaultSettleInfo.Builder();
    }

    #endregion Private Types
        
    #region Data

    private Dt[] dates_;
    private double[] cumuLosses_;
    private double[] cumuAmorts_;
    private int[] cumuDflts_;
    private DefaultSettleInfo settleInfo_;

    #endregion Data

  } // class BasketDefaultInfo

}
