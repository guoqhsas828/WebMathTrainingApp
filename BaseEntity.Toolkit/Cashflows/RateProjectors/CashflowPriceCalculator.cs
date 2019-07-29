// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows.RateProjectors
{
  /// <summary>
  ///   Price projector
  /// </summary>
  public interface IPriceCalculator : IRateProjector
  {
    /// <summary>
    /// Get the price on the specified reset date
    /// </summary>
    /// <param name="valueDate">The date on which the price is observed/determined</param>
    /// <param name="referenceDate">The reference date, i.e. the payment date, related to this observation</param>
    /// <returns>The price and reset state</returns>
    Fixing GetPrice(Dt valueDate, Dt referenceDate);
  }

  /// <summary>
  /// Utility methods related to price calculator
  /// </summary>
  public static class PriceCalculatorUtility
  {
    /// <summary>
    /// Gets the price on the specified date
    /// </summary>
    /// <param name="priceCalculator">The price calculator.</param>
    /// <param name="date">The date.</param>
    /// <returns>Fixing.</returns>
    public static Fixing GetPrice(
      this IPriceCalculator priceCalculator, Dt date)
    {
      return priceCalculator.GetPrice(date, date);
    }

    public static double CalculateReturn(
      double beginPrice, double endPrice, bool absolute)
    {
      if (absolute) return endPrice - beginPrice;
      if (beginPrice.Equals(0.0) && Math.Abs(endPrice) < 1E-9)
      {
        return 0;
      }
      return endPrice/beginPrice - 1;
    }
  }

  /// <summary>
  /// Calculate the forward price, or find the historical price,
  /// of the specified underlying cash flow payments.
  /// </summary>
  [Serializable]
  public class CashflowPriceCalculator : IPriceCalculator
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CashflowPriceCalculator" /> class.
    /// </summary>
    /// <param name="asOfDate">As of date.</param>
    /// <param name="underlyingPayments">The underlying payments.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="historicalPriceObservations">The historical price observations.</param>
    /// <param name="projectedPriceDeflater">The deflater applied the projected price,
    ///  such as the inflation index ratio for inflation bond, or notional factor for amortizing bond</param>
    /// <param name="historicalPriceAdjustment">The historical price adjustment function</param>
    /// <param name="survivalFunction">The survival function</param>
    public CashflowPriceCalculator(
      Dt asOfDate,
      PaymentSchedule underlyingPayments,
      DiscountCurve discountCurve,
      RateResets historicalPriceObservations,
      Func<Dt, double> projectedPriceDeflater = null,
      Func<double, Dt, double> historicalPriceAdjustment = null,
      Func<Dt, double> survivalFunction = null)
    {
      AsOf = asOfDate;
      UnderlyingPaymentsByCutoff = underlyingPayments.GroupByCutoff().ToList();
      LastCutoffDate = UnderlyingPaymentsByCutoff.Select(p=>p.Key).Max();
      DiscountCurve = discountCurve;
      HistoricalObservations = historicalPriceObservations;
      ProjectionPriceDeflater = projectedPriceDeflater;
      HistoricalPriceAdjustmentFn = historicalPriceAdjustment;
      SurvivalFn = survivalFunction;
      UseAsOfResets = true;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Fixes the price at the specified reset date.
    /// </summary>
    /// <param name="resetDate">The reset date.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <returns>The price and reset state.</returns>
    /// <exception cref="ToolkitException"></exception>
    public Fixing GetPrice(Dt resetDate, Dt paymentDate)
    {
      Dt asOf = AsOf,
        fwdStart = resetDate > paymentDate ? resetDate : paymentDate;

      RateResetState state;
      var price = RateResetUtil.FindRate(resetDate, asOf,
        HistoricalObservations, UseAsOfResets, out state);
      switch (state)
      {
        case RateResetState.ObservationFound:
          case RateResetState.ResetFound:
          if (HistoricalPriceAdjustmentFn != null)
          {
            price = HistoricalPriceAdjustmentFn(price, resetDate);
          }
          break;

        case RateResetState.IsProjected:
          price = CalculateForwardPrice(fwdStart, fwdStart);
          break;
        case RateResetState.Missing:
          if (!RateResetUtil.ProjectMissingRateReset(resetDate, asOf, fwdStart))
          {
            throw new MissingFixingException(String.Format(
              "Historical bond price not found on {0}", resetDate));
          }
          state = RateResetState.IsProjected;
          price = CalculateForwardPrice(fwdStart, fwdStart);
          break;
      }
      return new Fixing {Forward = price, RateResetState = state};
    }

    /// <summary>
    /// Calculates the full price, on the forward as-of date,
    /// of the cash flow payments after the specified forward start date.
    /// </summary>
    /// <param name="forwardAsOfDate">The forward as-of date.</param>
    /// <param name="forwardBeginDate">The date after which the payments become effective.</param>
    /// <returns>System.Double.</returns>
    public double CalculateForwardPrice(
      Dt forwardAsOfDate, Dt forwardBeginDate)
    {
      if (forwardBeginDate > LastCutoffDate)
      {
        return 0;
      }
      var pv = UnderlyingPaymentsByCutoff.CalculatePv(
        forwardAsOfDate, forwardBeginDate, DiscountCurve,
        SurvivalFn, !PricingDatePaymentsExcluded, true);
      if (ProjectionPriceDeflater != null)
      {
        pv /= ProjectionPriceDeflater(forwardAsOfDate);
      }
      return pv;
    }

    /// <summary>
    /// Gets the fixing schedule.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <returns>FixingSchedule.</returns>
    public FixingSchedule GetFixingSchedule(Dt valueDate, Dt paymentDate)
    {
      return new ForwardPriceFixingSchedule
      {
        ResetDate = valueDate, ReferenceDate = paymentDate
      };
    }

    #endregion

    #region Properties

    /// <summary>
    /// The date before which all the prices are historical
    /// </summary>
    /// <value>As of.</value>
    public Dt AsOf { get; set; }

    /// <summary>
    /// If provided reset matches AsOf then use it
    /// </summary>
    /// <value><c>true</c> if [use as of resets]; otherwise, <c>false</c>.</value>
    public bool UseAsOfResets { get; set; }


    /// <summary>
    /// Gets the underlying payments.
    /// </summary>
    /// <value>The underlying payments.</value>
    public IEnumerable<Payment> UnderlyingPayments =>
      UnderlyingPaymentsByCutoff.SelectMany(g => g.Value);

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    public DiscountCurve DiscountCurve { get; }

    /// <summary>
    /// Gets or sets a value indicating whether pricing date payments
    /// are excluded from price.
    /// </summary>
    /// <value><c>true</c> if pricing date payments are excluded; otherwise, <c>false</c>.</value>
    public bool PricingDatePaymentsExcluded { get; set; }

    /// <summary>
    /// Gets the inflation index ratio calculator.
    /// </summary>
    /// <value>The inflation index ratio calculator.</value>
    public Func<Dt, double> ProjectionPriceDeflater { get; }

    /// <summary>
    /// Gets the historical price adjustment function.
    /// </summary>
    /// <value>The historical price adjustment function.</value>
    public Func<double,Dt,double> HistoricalPriceAdjustmentFn { get; }

    public Func<Dt, double> SurvivalFn { get; } 

    /// <summary>
    /// Gets or sets the last date.
    /// </summary>
    /// <value>The last date.</value>
    private Dt LastCutoffDate { get; }

    private IReadOnlyList<KeyValuePair<Dt, IList<Payment>>> 
      UnderlyingPaymentsByCutoff { get; }

    #endregion

    #region IRateProjector Implementation

    /// <summary>
    /// Name of Index
    /// </summary>
    /// <value>The name of the index.</value>
    public string IndexName { get; set; }

    /// <summary>
    /// Historical index fixings
    /// </summary>
    /// <value>The historical observations.</value>
    public RateResets HistoricalObservations { get; set; }

    /// <summary>
    /// Fixing on reset
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns>Fixing.</returns>
    public Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var fs = fixingSchedule as ForwardPriceFixingSchedule;
      return fs != null
        ? GetPrice(fs.ResetDate, fs.ReferenceDate)
        : GetPrice(fixingSchedule.ResetDate, fixingSchedule.ResetDate);
    }

    /// <summary>
    /// Initialize fixing schedule
    /// </summary>
    /// <param name="prevPayDt">Previous payment date</param>
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="payDt">Payment date</param>
    /// <returns>Fixing schedule</returns>
    public FixingSchedule GetFixingSchedule(Dt prevPayDt,
      Dt periodStart, Dt periodEnd, Dt payDt)
    {
      return GetFixingSchedule(periodEnd, payDt);
    }

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns>Reset info for each component of the fixing</returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public List<RateResets.ResetInfo> GetResetInfo(
      FixingSchedule schedule)
    {
      throw new NotImplementedException();
    }

    #endregion
  }
 
}
