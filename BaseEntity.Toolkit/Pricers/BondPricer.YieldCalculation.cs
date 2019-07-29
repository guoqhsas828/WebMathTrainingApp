//
// 
//

using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// The yield and the date
  /// </summary>
  public struct YieldInfo
  {
    /// <summary>
    /// Initializes a new instance of <see cref="YieldInfo" />.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="redemptionPrice">The redemption price.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="yield01">The yield01.</param>
    internal YieldInfo(
      Dt date, double redemptionPrice,
      double yield, double yield01 = double.NaN)
    {
      Date = date;
      RedemptionPrice = redemptionPrice;
      Yield = yield;
      Yield01 = yield01;
    }

    /// <summary>
    /// The date on which the yield is calculated
    /// </summary>
    public readonly Dt Date;

    /// <summary>
    /// The date on which the yield is calculated
    /// </summary>
    public readonly double RedemptionPrice;

    /// <summary>
    /// The yield value
    /// </summary>
    public readonly double Yield;

    /// <summary>
    /// The value of yield01.  It would be <c>NaN</c> if not calculated.
    /// </summary>
    public readonly double Yield01;
  }

  partial class BondPricer
  {
    /// <summary>
    /// Calculate the yield01 assuming the bond matures on the call date with the worst yield
    /// </summary>
    /// <returns>System.Double.</returns>
    public YieldInfo YieldToWorst01()
    {
      double yield;
      var pricer = GetWorstYieldPricer(out yield);
      return new YieldInfo(pricer.Bond.Maturity,
        pricer.Bond.FinalRedemptionValue, yield, pricer.PV01());
    }


    /// <summary>
    /// Gets the pricer with the bond maturing on the date with the worst yield
    /// </summary>
    /// <param name="worstYield">The worst yield value</param>
    /// <returns>BondPricer.</returns>
    public BondPricer GetWorstYieldPricer(
      out double worstYield)
    {
      var bond = Bond;
      var pricer = (BondPricer) Clone();
      pricer.Product = (Bond)bond.Clone();

      var worst = new YieldInfo();
      bool hasValue = false;
      foreach (var r in CalculateYields(pricer, bond.CallSchedule, false))
      {
        if (hasValue && worst.Yield <= r.Yield) continue;
        worst = r;
        hasValue = true;
      }

      worstYield = worst.Yield;
      ChangeMaturity(pricer.Bond, worst.Date, worst.RedemptionPrice,
        bond.FirstCoupon, bond.LastCoupon);
      pricer.Reset();
      return pricer;
    }

    public IEnumerable<YieldInfo> CalculateYields(
      IEnumerable<CallPeriod> callSchedules)
    {
      return CalculateYields(this, callSchedules, true);
    }

    /// <summary>
    /// Calculates the yields by call dates
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="periods">The periods.</param>
    /// <param name="preservePricer">if set to <c>true</c>, 
    ///   a clone of the input pricer is used, leaving the input pricer untouched</param>
    /// <returns>IEnumerable&lt;YieldInfo&gt;.</returns>
    private static IEnumerable<YieldInfo> CalculateYields(
      BondPricer pricer,
      IEnumerable<CallPeriod> periods,
      bool preservePricer)
    {
      var bond = pricer.Bond;
      Dt maturity = bond.Maturity,
        firstCoupon = bond.FirstCoupon,
        lastCoupon = bond.LastCoupon;
      double redemptionPrice = bond.FinalRedemptionValue;

      if (preservePricer)
      {
        var wb = (Bond)bond.Clone();
        wb.CallSchedule.Clear();

        pricer = (BondPricer)pricer.Clone();
        pricer.Product = bond = wb;
        pricer.Reset();
      }

      if (periods != null)
      {
        foreach (var call in periods.GetActivePeriods(
          pricer.AsOf, bond.GetNotificationDays()))
        {
          var date = call.StartDate;
          if (date >= maturity) break;
          yield return new YieldInfo(date, call.CallPrice, CalculateYield(
            pricer, date, call.CallPrice, firstCoupon, lastCoupon));
        }
      }

      yield return new YieldInfo(maturity, redemptionPrice, CalculateYield(
        pricer, maturity, redemptionPrice, firstCoupon, lastCoupon));
    }

    /// <summary>
    /// Changes the bond maturity and removes all the call features.
    /// </summary>
    /// <param name="pricer">The pricer</param>
    /// <param name="maturity">The maturity date</param>
    /// <param name="redemptionPrice">The redemption price at the new maturity date</param>
    /// <param name="firstCoupon">The original first coupon date</param>
    /// <param name="lastCoupon">The original last coupon date</param>
    private static double CalculateYield(
      BondPricer pricer,
      Dt maturity, double redemptionPrice,
      Dt firstCoupon, Dt lastCoupon)
    {
      ChangeMaturity(pricer.Bond, maturity, redemptionPrice,
        firstCoupon, lastCoupon);
      pricer.Reset();
      return CalculateYield(pricer);
    }

    /// <summary>
    /// Calculates the yield.
    /// </summary>
    /// <param name="pricer">The pricer</param>
    /// <returns>System.Double.</returns>
    private static double CalculateYield(BondPricer pricer)
    {
      var bond = pricer.Bond;
      var finalRedemptionValue = bond.FinalRedemptionValue;
      var principal = pricer.Principal*finalRedemptionValue;
      var accrued = pricer.AccruedInterest();
      var price = pricer.FullPrice() - accrued;

      if (bond.IsCustom)
      {
        var factor = pricer.NotionalFactor;
        accrued *= factor;
        price *= factor;

        //For an Amortizing Bond we first normalize the Accrued and Flat price value from their 
        //respective dollar amounts to a unit notional
        var cfAdapter = pricer.BondCashflowAdapter;
        if (!finalRedemptionValue.AlmostEquals(1.0))
        {
          var last = cfAdapter.Count - 1;
          cfAdapter.SetAmount(last, finalRedemptionValue/cfAdapter.GetAmount(last));
        }
        return PaymentScheduleUtils.PriceToYtm(
          cfAdapter, pricer.Settle, pricer.ProtectionStart,
          bond.DayCount, bond.Freq, price, accrued);
      }

      return BondModelUtil.YieldToMaturity(pricer.Settle, bond,
        pricer.PreviousCycleDate(), pricer.NextCouponDate(), bond.Maturity,
        pricer.RemainingCoupons(), price, accrued, principal, pricer.CurrentRate,
        pricer.RecoveryRate, pricer.IgnoreExDivDateInCashflow, pricer.CumDiv());
    }

    /// <summary>
    /// Changes the bond maturity and removes all the call features.
    /// </summary>
    /// <param name="bond">The bond to change</param>
    /// <param name="maturity">The maturity date</param>
    /// <param name="redemptionPrice">The redemption price at the new maturity date</param>
    /// <param name="firstCoupon">The original first coupon date</param>
    /// <param name="lastCoupon">The original last coupon date</param>
    private static void ChangeMaturity(
      Bond bond, Dt maturity, double redemptionPrice,
      Dt firstCoupon, Dt lastCoupon)
    {
      bond.CallSchedule.Clear();

      bond.Maturity = maturity;
      if (redemptionPrice > 0.0)
      {
        var finalNotionalFactor = NotionalFactorAt(bond, maturity);
        bond.FinalRedemptionValue = redemptionPrice*finalNotionalFactor;
      }

      bond.FirstCoupon = firstCoupon > maturity ? Dt.Empty : firstCoupon;
      bond.LastCoupon = lastCoupon > maturity ? Dt.Empty : lastCoupon;
    }

  }
}
