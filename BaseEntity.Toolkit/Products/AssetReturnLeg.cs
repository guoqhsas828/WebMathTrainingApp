// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using NotionalSchedule = BaseEntity.Toolkit.Util.
  IntervalIndexedValues<BaseEntity.Toolkit.Base.Dt, System.Func<double>>;
using IValuationSchedule = System.Collections.Generic.IReadOnlyList<
  BaseEntity.Toolkit.Base.ValueAndPaymentDatePair>;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Interface representing an asset return leg
  /// </summary>
  /// 
  /// <remarks>
  ///   <para>
  ///   The asset return leg represents the "return" leg of a total return
  ///   swap (TRS), where the returns conceptually consist of the capital
  ///   gains/losses (i.e. the appreciation/depreciation of the asset price)
  ///   plus all the incomes (interests, coupons, dividends, etc.) produced
  ///   by the underlying asset.
  ///  </para>
  /// 
  ///  <para>
  ///   For pricing purpose, we assume all the relevant incomes from the
  ///   underlying asset are distributed to the asset return receiver
  ///   immediately without any delay.
  ///   The distribution period begins on, and includes, the Effective
  ///   date of the asset return leg, and it ends on, but excludes, the Maturity
  ///   date or any early Termination date, whichever comes earlier.
  ///   Only the incomes generated during this period are handled over to
  ///   the asset return receiver. 
  ///  </para>
  /// 
  ///  <para>
  ///   The capital gains/losses are determined and paid
  ///   periodically based on the valuation schedule, which consists
  ///   of a sequence of valuation dates after the Effective date.
  ///   Each valuation date is associated
  ///   with a payment date, usually 3-7 business days after.
  ///   On a valuation date, the asset price is observed or
  ///   determined, so does the appreciation/depreciation of the asset.  Then
  ///   the corresponding gains/losses are paid on the associated payment date.
  ///  </para>
  /// 
  ///  <para><b>Price returns without amortization</b></para>
  /// 
  ///  <para>
  ///   Suppose we have a sequence of valuation dates, <m>T_1, T_2, \ldots, T_n</m>.
  ///   Let <m>P_1, P_2, \ldots, P_n</m> be the corresponding asset prices observed,
  ///   and let <m>P_0</m> be the initial price contractually determined.
  ///   Let <m>N_0, N_1, \ldots, N_{n-1}</m> be the corresponding TRS notional,
  ///   where <m>N_0</m> is the initial investment.
  ///   Then the overall amount of the capital gains/losses is determined by
  ///   the following formula<math>
  ///      Y_i = \frac{P_i - P_{i-1}}{P_{i-1}} \times N_{i-1}
  ///      \qquad i = 1, \ldots, n
  ///      \tag{1}
  ///  </math>
  ///  </para>
  /// 
  ///  <para>
  ///   The flag <var>ResettingNotional</var> controls how the notional <m>N_i</m>
  ///   evolves.  With <c>ResettingNotional</c> being false, the notional
  ///   <m>N_i</m> is constant and fixed to the initial investment <m>N_0</m>;
  ///   otherwise, it is reset to <m>N_{i-1} + Y_i</m> after the every periods.
  ///   Formally, for <m>i = 1, \ldots, n</m>, we have<math>
  ///     N_i = \begin{cases}
  ///          N_0 &amp; \text{without resetting notional}
  ///       \\ \quad
  ///       \\ N_{i-1} + Y_i &amp; \text{with resetting notional}
  ///     \end{cases}
  ///  </math>
  ///  </para>
  /// 
  ///  <para>
  ///   Combining with equation (1), the above formula can be simplified as<math>
  ///     N_i = \begin{cases}
  ///          N_0 &amp; \text{without resetting notional}
  ///       \\ \quad
  ///       \\ \displaystyle P_i\frac{N_0}{P_0} &amp; \text{with resetting notional}
  ///     \end{cases}
  ///     \tag{2}
  ///   </math>
  /// </para>
  /// 
  ///  <para><b>Price returns with amortization</b></para>
  /// 
  ///  <para>
  ///   Some underlying assets such as Bond may pay amortization (partial
  ///   redemption) during the life of the total return swap.  When this
  ///   happens, the total return receiver gets the so called the <c>reference
  ///   amount</c>, which is the cash value of the redemption on the redemption
  ///   date, minus the value of the redemption evaluated at the price on the
  ///   prior valuation date.
  ///  </para>
  /// 
  ///  <para>
  ///   Take bond as an example.  Let <m>t_1, t_2, \ldots</m>, be the partial
  ///   redemption dates and <m>a_1, a_2, \ldots</m>, the face value of the
  ///   redemption.  Then on each date <m>t_j</m>, the reference amount is given by
  ///   <math>
  ///     R_j = a_j - a_j P_i
  ///     \qquad \text{where} \quad i = \max\{k: T_k &lt; t_j\}
  ///   </math>such that <m>T_i</m> is the valuation date immediately preceding
  ///   <m>t_j</m> and <m>P_i</m> is the price on that date.
  ///  </para>
  /// 
  ///  <para>
  ///   In this case, the price return amount corresponding to valuation date
  ///   <m>T_i</m> becomes
  ///   <math>
  ///      Y_i = \frac{P_i - P_{i-1}}{P_{i-1}} \times (N_{i-1} - \tilde{A}_i)
  ///      \qquad i = 1, \ldots, n
  ///   </math>
  ///   where <m>N_{i-1}</m> evolves according to equation (2) and
  ///   <m>\tilde{A}_i</m> is the cumulative reduction of the TRS notional
  ///   due to amortization
  ///   <math>
  ///     \tilde{A}_i = \begin{cases}
  ///         P_0\; A_i &amp; \text{without resetting notional}
  ///       \\ \quad
  ///      \\ P_{i-1}\; A_i &amp; \text{with resetting notional}
  ///     \end{cases}
  ///   </math>and <m>A_i</m> is the cumulative amortization of the bond face value
  ///   from the TRS effective up to the value date <m>T_i</m><math>
  ///     A_i = \sum_{T_0 \leq T_k \leq T_i} a_k
  ///   </math>
  ///  </para>
  /// </remarks>
  public interface IAssetReturnLeg : IProduct
  {
    /// <summary>
    /// Gets the underlying asset of the asset return leg
    /// </summary>
    /// <value>The underlying asset</value>
    IProduct UnderlyingAsset { get; }

    /// <summary>
    /// Gets the valuation dates after effective, on which
    /// the asset prices are observed or determined.
    /// </summary>
    /// <value>The valuation dates</value>
    IValuationSchedule ValuationSchedule { get; }

    /// <summary>
    /// Gets the initial price of the underlying asset
    /// </summary>
    /// <value>The initial price</value>
    double InitialPrice { get; }

    /// <summary>
    ///  Whether to reset the remaining notional by the asset prices
    ///  observed at the end of the preceding periods in the case of
    ///  multiple price return periods.
    /// </summary>
    /// <value><c>true</c> if reset notional; otherwise, <c>false</c>.</value>
    bool ResettingNotional { get; }
  }

  /// <summary>
  /// Interface representing an asset return leg
  /// </summary>
  /// <typeparam name="T">The type of the underlying asset</typeparam>
  public interface IAssetReturnLeg<T> : IAssetReturnLeg where T : IProduct
  {
    /// <summary>
    /// Gets the underlying asset.
    /// </summary>
    /// <value>The underlying asset</value>
    new T UnderlyingAsset { get; }
  }

  /// <summary>
  ///  An implementation of asset return leg
  /// </summary>
  /// <typeparam name="T">The underlying asset type</typeparam>
  [Serializable]
  public class AssetReturnLeg<T> : Product, IAssetReturnLeg<T>
    where T : IProduct
  {
    /// <summary>
    /// Initializes a new instance of the <c>AssetReturnLeg&lt;T&gt;</c> class.
    /// </summary>
    /// <param name="underlyingAsset">The underlying asset</param>
    /// <param name="effective">The effective (accrual start) date</param>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="ccy">The currency of the total return payments</param>
    /// <param name="initialPrice">The initial price</param>
    /// <param name="valuationSchedule">The valuation schedule.</param>
    /// <param name="resettingNotional"><c>true</c> if reset notional;
    ///   otherwise, <c>false</c></param>
    public AssetReturnLeg(
      T underlyingAsset,
      Dt effective, Dt maturity,
      Currency ccy,
      double initialPrice,
      IValuationSchedule valuationSchedule,
      bool resettingNotional)
      : base(effective, maturity, ccy)
    {
      UnderlyingAsset = underlyingAsset;
      InitialPrice = initialPrice;
      ValuationSchedule = valuationSchedule;
      ResettingNotional = resettingNotional;
    }


    /// <summary>
    /// Validate the product
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      var underlier = UnderlyingAsset as Bond;
      if(underlier != null && underlier.Floating)
        throw new ArgumentException("Return Leg with floating bond not supported yet");
    }


    /// <summary>
    /// Gets the initial price.
    /// </summary>
    /// <value>The initial price.</value>
    public double InitialPrice { get; }

    /// <summary>
    /// Gets the underlying asset
    /// </summary>
    /// <value>The underlying asset</value>
    public T UnderlyingAsset { get; }

    /// <summary>
    /// Gets the value dates after effective, on which
    /// the asset prices are observed or determined.
    /// </summary>
    /// <value>The value dates.</value>
    public IValuationSchedule ValuationSchedule { get; }

    /// <summary>
    /// In the case of multiple price return periods, whether to reset
    /// the remaining notional by the asset prices observed at the end
    /// of the preceding periods.
    /// </summary>
    /// <value><c>true</c> if reset notional; otherwise, <c>false</c>.</value>
    public bool ResettingNotional { get; }

    /// <summary>
    /// Gets the underlying asset.
    /// </summary>
    /// <value>The underlying asset</value>
    IProduct IAssetReturnLeg.UnderlyingAsset => UnderlyingAsset;
  }

  /// <summary>
  ///  Provides static methods to create and manipulate asset return legs
  /// </summary>
  public static class AssetReturnLeg
  {
    /// <summary>
    /// Creates a return leg for the specified underlying asset,
    /// with the valuation schedule relative to value dates.
    /// </summary>
    /// <typeparam name="T">The type of the underlying asset</typeparam>
    /// <param name="underlyingAsset">The underlying asset</param>
    /// <param name="effective">The effective of the asset return leg</param>
    /// <param name="maturity">The maturity date of the asset return leg</param>
    /// <param name="ccy">The currency of the total return payments</param>
    /// <param name="initialPrice">The initial price</param>
    /// <param name="calendar">The business day calendar</param>
    /// <param name="bdc">The business day roll convention</param>
    /// <param name="paymentLag">Number of business days between the valuation date and payment date</param>
    /// <param name="valueDates">The list valuation dates</param>
    /// <param name="resettingNotional"><c>true</c> if reset notional; otherwise, false</param>
    /// <returns>IAssetReturnLeg&lt;T&gt;.</returns>
    /// <exception cref="System.ArgumentNullException">underlyingAsset</exception>
    public static AssetReturnLeg<T> Create<T>(
      T underlyingAsset,
      Dt effective, Dt maturity, Currency ccy,
      double initialPrice,
      Calendar calendar,
      BDConvention bdc = BDConvention.Following,
      int paymentLag = 0,
      IEnumerable<Dt> valueDates = null,
      bool resettingNotional = false) where T : IProduct
    {
      if (underlyingAsset == null)
      {
        throw new ArgumentNullException(nameof(underlyingAsset));
      }
      if (maturity <= effective)
      {
        throw new ArgumentException(
          $"Maturity ({maturity}) must be later than Effective ({effective})");
      }
      var schedule = new ValuationScheduleRelativeToValueDates
      {
        ValueDates = valueDates.Normalize(paymentLag,
          effective, maturity, bdc, calendar),
        PaymentLag = paymentLag,
        Calendar = calendar,
      };
      return new AssetReturnLeg<T>(underlyingAsset, effective,
        maturity, ccy, initialPrice, schedule, resettingNotional);
    }


    /// <summary>
    /// Infers the instance type of the underlying asset and
    /// creates a return leg accordingly,
    /// with the valuation schedule relative to value dates,.
    /// </summary>
    /// <param name="underlyingAsset">The underlying asset</param>
    /// <param name="effective">The effective of the asset return leg</param>
    /// <param name="maturity">The maturity date of the asset return leg</param>
    /// <param name="ccy">The currency of the total return payments</param>
    /// <param name="initialPrice">The initial price</param>
    /// <param name="calendar">The business day calendar</param>
    /// <param name="bdc">The business day roll convention</param>
    /// <param name="paymentLag">Number of business days between the valuation date and payment date</param>
    /// <param name="valueDates">The list valuation dates</param>
    /// <param name="resettingNotional"><c>true</c> if reset notional; otherwise, false</param>
    /// <returns>IAssetReturnLeg.</returns>
    /// <exception cref="System.ArgumentNullException">underlyingAsset</exception>
    public static IAssetReturnLeg Make(
      IProduct underlyingAsset,
      Dt effective, Dt maturity, Currency ccy,
      double initialPrice,
      Calendar calendar,
      BDConvention bdc = BDConvention.Following,
      int paymentLag = 0,
      IEnumerable<Dt> valueDates = null,
      bool resettingNotional = false)
    {
      if (underlyingAsset == null)
      {
        throw new ArgumentNullException(nameof(underlyingAsset));
      }
      var schedule = new ValuationScheduleRelativeToValueDates
      {
        ValueDates = valueDates.Normalize(paymentLag,
          effective, maturity, bdc, calendar),
        PaymentLag = paymentLag,
        Calendar = calendar,
      };
      var type = typeof(AssetReturnLeg<>).MakeGenericType(underlyingAsset.GetType());
      return (IAssetReturnLeg)Activator.CreateInstance(type, underlyingAsset,
        effective, maturity, ccy, initialPrice, schedule, resettingNotional);
    }

    /// <summary>
    /// Normalizes the value dates.
    /// </summary>
    /// <param name="valueDates">The value dates.</param>
    /// <param name="paymentLag">The payment lag.</param>
    /// <param name="effective">The effective.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="bdc">The roll convention.</param>
    /// <param name="calendar">The business day calendar.</param>
    /// <returns>IReadOnlyList&lt;Dt&gt;.</returns>
    internal static IList<Dt> Normalize(
      this IEnumerable<Dt> valueDates,
      int paymentLag,
      Dt effective, Dt maturity,
      BDConvention bdc, Calendar calendar)
    {
      // According the ISDA document, the final value date
      //  is N business days prior to the final termination
      //  date, where N is normally 3.
      Dt finalPaymentDate = Dt.Roll(maturity, bdc, calendar),
        finalValueDate = Dt.AddDays(finalPaymentDate, -paymentLag, calendar);

      // If no value dates are specified assume there is only
      //  a single final value date after the effective.
      if (valueDates == null)
      {
        return new[] { finalValueDate };
      }

      // According to ISDA documentation, value dates must roll
      //  to a business day and the final one must be some B-days
      //  before the termination date.
      return valueDates.Select(dt => Dt.Roll(dt, bdc, calendar))
        .Where(dt => dt > effective && dt < finalValueDate)
        .Append(finalValueDate)
        .OrderBy(d => d).Distinct().ToList();
    }

    /// <summary>
    /// Gets the price return payments.
    /// </summary>
    /// <param name="assetReturnLeg">The asset return leg</param>
    /// <param name="fromDate">The start date to include the payments</param>
    /// <param name="priceCalculator">The price calculator</param>
    /// <param name="initialPrice">The initial price</param>
    /// <param name="underlierBalanceInfo">List of the relevant changes in the underlier balance</param>
    /// <param name="resettingNotional">if set to <c>true</c>, price return is absolute; otherwise, it is relative return</param>
    /// <param name="defaultSettleDate">The default settle date</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    public static IEnumerable<Payment> GetPriceReturnPayments(
      this IAssetReturnLeg assetReturnLeg,
      Dt fromDate,
      IPriceCalculator priceCalculator,
      double initialPrice,
      IReadOnlyList<INotionalChangeInfo> underlierBalanceInfo,
      bool resettingNotional,
      Dt defaultSettleDate)
    {
      Debug.Assert(!double.IsNaN(initialPrice));

      bool defaulted = !defaultSettleDate.IsEmpty();
      if (defaulted && defaultSettleDate < fromDate)
        yield break;

      var ccy = assetReturnLeg.Ccy;
      Dt begin = assetReturnLeg.Effective, lastPayDt = begin;
      int balanceIndex = 0;
      var balance = GetNotional(underlierBalanceInfo, begin, ref balanceIndex);
      if (balance <= 0) yield break;

      var schedule = assetReturnLeg.ValuationSchedule;
      for (int i = 0, n = schedule.Count; i < n; ++i)
      {
        var date = schedule.GetValueDate(i);
        Debug.Assert(date > begin, "value dates out of order");

        Dt payDt = schedule.GetPaymentDate(i);
        if (defaulted && defaultSettleDate <= payDt)
        {
          balance = GetNotional(underlierBalanceInfo,
            defaultSettleDate, ref balanceIndex);
          yield return new PriceReturnPayment(lastPayDt,
            defaultSettleDate, ccy, begin, defaultSettleDate + 1, priceCalculator,
            (i == 0 ? initialPrice : double.NaN), resettingNotional)
            .ScaleBy(resettingNotional ? (balance/initialPrice) : balance);
          yield break;
        }

        var lastIndex = balanceIndex;
        balance = GetNotional(underlierBalanceInfo, payDt, ref balanceIndex);
        if (fromDate >= payDt)
        {
          if (balance <= 0) yield break;
          goto next;
        }

        Debug.Assert(balance > -1E-15);

        // Is there any amortization?
        if (balanceIndex != lastIndex)
        {
          var amounts = GetReferenceAmounts(underlierBalanceInfo,
            lastIndex, balanceIndex, ccy, initialPrice,
            begin, priceCalculator,
            i == 0 ? initialPrice : double.NaN);
          foreach (var amount in amounts)
          {
            yield return amount;
          }
        }

        // Do we have any remaining balance?
        if (balance <= 0) yield break;

        // Price return payment
        yield return new PriceReturnPayment(lastPayDt,
          payDt, ccy, begin, date, priceCalculator,
          (i == 0 ? initialPrice : double.NaN), resettingNotional)
          .ScaleBy(resettingNotional ? (balance/initialPrice) : balance);

        next:
        // Move to the next period
        lastPayDt = payDt;
        begin = date;
      }
    }

    internal static IEnumerable<Payment> GetRecoveryReturns(
      this IEnumerable<Payment> payments,
      Dt underlyingMaturity,
      IReadOnlyList<Dt> timeGrids,
      Func<Dt, double> recoveryFunction)
    {
      if (timeGrids.Count == 0) timeGrids = null;

      // ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var payment in payments)
      {
        var u = GetUnderlyingPayment(payment);
        var p = u as PriceReturnPayment;
        if (p != null)
        {
          var end = p.GetCreditRiskEndDate();
          if (end > underlyingMaturity)
          {
            p.CreditRiskEndDate = end = underlyingMaturity;
          }
          yield return new RecoveryReturnPayment(p.LastPayDt,
            end, p.Ccy, recoveryFunction?.Invoke(p.PayDt) ?? 0.0,
            p.BeginDate, p.PriceCalculator, p.BeginPriceOverride,
            p.IsAbsolute)
          {
            CutoffDate = p.CutoffDate,
            TimeGrids = timeGrids,
          }.ScaleAccordingTo(payment);
        }
        else
        {
          var r = u as ReferenceAmountPayment;
          if (r == null) continue;
          var end = r.GetCreditRiskEndDate();
          if (end > underlyingMaturity)
          {
            r.CreditRiskEndDate = end = underlyingMaturity;
          }
          yield return new RecoveryReturnPayment(r.ValueDate,
            end, r.Ccy, recoveryFunction?.Invoke(r.PayDt) ?? 0.0,
            r.ValueDate, r.PriceCalculator, r.PriceOverride,
            true)
          {
            CutoffDate = r.CutoffDate,
            TimeGrids = timeGrids,
          }.ScaleAccordingTo(payment, r.PrincipalPaymentAmount);
        }
      }
    }

    public static IReadOnlyList<Dt> GetTimeGrids(
      this IEnumerable<CreditContingentPayment> payments)
    {
      var timeGrids = new UniqueSequence<Dt>();
      foreach (var payment in payments)
      {
        if (payment.TimeGrids != null && payment.TimeGrids.Count != 0)
        {
          timeGrids.Add(payment.TimeGrids);
        }
        else
        {
          timeGrids.Add(payment.BeginDate);
          timeGrids.Add(payment.EndDate);
        }
      }
      return timeGrids;
    }

    private static double GetNotional(
      IReadOnlyList<INotionalChangeInfo> notionals,
      Dt endDate, ref int index)
    {
      if (notionals == null || notionals.Count == 0)
        return 1.0;

      int n = notionals.Count;
      for (int i = index; i < n; ++i)
      {
        int cmp = Dt.Cmp(endDate, notionals[i].Date);
        if (cmp < 0)
        {
          index = i;
          return notionals[i].NotionalBeforeChange;
        }
        if (cmp == 0)
        {
          index = i + 1;
          return notionals[i].NotionalAfterChange;
        }
      }
      index = n;
      return notionals[n - 1].NotionalAfterChange;
    }

    private static IEnumerable<ReferenceAmountPayment> GetReferenceAmounts(
      IReadOnlyList<INotionalChangeInfo> changes, int begin, int end,
      Currency ccy, double initialPrice,
      Dt valueDate, IPriceCalculator priceCalculator, double priceOverride)
    {
      for (int i = begin; i < end; ++i)
      {
        var change = changes[i];
        double balance = change.NotionalAfterChange,
          lastBalance = change.NotionalBeforeChange;
        yield return new ReferenceAmountPayment(
          change.Date, ccy,
          // Here we want the balance change corresponding to
          // $1 initial investment in this asset return leg.
          lastBalance/initialPrice, (lastBalance - balance)/initialPrice,
          valueDate, priceCalculator, priceOverride)
        {
          CreditRiskEndDate = (change as Payment)?.GetCreditRiskEndDate() ?? Dt.Empty
        };
      }
    }

    private static Payment ScaleBy(this Payment payment, double balance)
    {
      return balance.AlmostEquals(1.0) ? payment
          : new ScaledPayment(payment, balance);
    }

    internal static Dt GetPrecedinggValueDate(
      this IAssetReturnLeg assetReturnLeg, Dt referenceDate)
    {
      Dt begin = assetReturnLeg.Effective;
      var schedule = assetReturnLeg.ValuationSchedule;
      for (int i = 0, n = schedule.Count; i < n; ++i)
      {
        var date = schedule.GetValueDate(i);
        if (date >= referenceDate) break;
        begin = date;
      }
      return begin;
    }

    /// <summary>
    /// Get the ultimate underlying payment
    /// </summary>
    /// <param name="payment">The underlying payment</param>
    /// <returns></returns>
    public static Payment GetUnderlyingPayment(this Payment payment)
    {
      ScaledPayment sp;
      while ((sp = payment as ScaledPayment) != null)
        payment = sp.UnderlyingPayment;
      return payment;
    }

    private static Payment ScaleAccordingTo(this Payment toBeScaled,
      Payment payment, double notional = 1.0)
    {
      ScaledPayment sp;
      while ((sp = payment as ScaledPayment) != null)
      {
        notional *= sp.Notional;
        payment = sp.UnderlyingPayment;
      }
      return notional.AlmostEquals(1.0)
        ? toBeScaled : toBeScaled.ScaleBy(notional);
    }


    internal static void GetNotionalAndUnderlyingPayment(
      this Payment payment,
      ref double notional, out PriceReturnPayment priceReturnPayment)
    {
      ScaledPayment sp;
      while ((sp = payment as ScaledPayment) != null)
      {
        payment = sp.UnderlyingPayment;
        notional *= sp.Notional;
      }
      priceReturnPayment = (PriceReturnPayment)payment;
    }

    internal static NotionalSchedule GetNotionalSchedule(
      IAssetReturnLegPricer pricer)
    {
      var underlierBalanceInfo = pricer.GetPaymentSchedule(pricer.Settle)
        .OfType<INotionalChangeInfo>().ToArray();
      Dt fromDate = pricer.Settle;
      var assetReturnLeg = pricer.AssetReturnLeg;
      var priceCalculator = assetReturnLeg.ResettingNotional
        ? pricer.GetPriceCalculator() : null;
      var initialPrice = assetReturnLeg.InitialPrice;

      Dt begin = assetReturnLeg.Effective, lastPayDt = begin;
      int balanceIndex = 0;
      var balance = GetNotional(underlierBalanceInfo, begin, ref balanceIndex);
      if (balance <= 0) return null;

      List<Dt> dates = null;
      List<Func<double>> values = null;

      var schedule = assetReturnLeg.ValuationSchedule;
      for (int i = 0, n = schedule.Count; i < n; ++i)
      {
        var date = schedule.GetValueDate(i);
        Debug.Assert(date > begin, "value dates out of order");

        Dt payDt = schedule.GetPaymentDate(i);
        var lastIndex = balanceIndex;
        var lastBalance = balance;
        balance = GetNotional(underlierBalanceInfo, payDt, ref balanceIndex);
        if (fromDate >= payDt || (priceCalculator == null && balanceIndex == lastIndex))
        {
          if (balance <= 0) return null;
          goto next;
        }
        Debug.Assert(balance > -1E-15);

        if (dates == null)
        {
          dates = new List<Dt>();
          values = new List<Func<double>>();
        }

        dates.Add(payDt);
        values.Add(new NotionalFactorCalculator(
          priceCalculator, begin, priceCalculator == null
            ? lastBalance : (lastBalance/initialPrice)
          ).CalculateNotionalFactor);

        next:
        // Move to the next period
        begin = date;
      }

      if (dates == null) return null;
      values.Add(new NotionalFactorCalculator(
        priceCalculator, begin,
        priceCalculator == null
          ? balance : (balance/initialPrice)
        ).CalculateNotionalFactor);
      return new NotionalSchedule(dates.ToArray(), values.ToArray());
    }

    private static Dt GetValueDate(this IValuationSchedule schedule, int index)
    {
      return schedule[index].ValueDate;
    }

    private static Dt GetPaymentDate(this IValuationSchedule schedule, int index)
    {
      return schedule[index].PaymentDate;
    }

    #region Nested type: RecoveryReturnPayment

    [Serializable]
    private class RecoveryReturnPayment : CreditContingentPayment
    {
      public RecoveryReturnPayment(
        Dt beginDt, Dt endDate, Currency ccy,
        double recoveryRate,
        Dt valueDate, IPriceCalculator calculator,
        double priceOverride, bool isAbsolute)
        : base(beginDt, endDate, ccy)
      {
        RecoveryRate = recoveryRate;
        ValueDate = valueDate;
        PriceCalculator = calculator;
        PriceOverride = priceOverride;
        IsAbsolute = isAbsolute;
      }

      private bool IsAbsolute { get; }

      private double  RecoveryRate { get; }

      private Dt ValueDate { get; }

      private IPriceCalculator PriceCalculator { get; }

      private double PriceOverride { get; }

      private double Price => double.IsNaN(PriceOverride)
        ? PriceCalculator.GetPrice(ValueDate).Value
        : PriceOverride;

      protected override double ComputeAmount()
      {
        return PriceCalculatorUtility.CalculateReturn(
          Price, RecoveryRate, IsAbsolute);
      }
    }

    #endregion

    #region Nested type: ReferenceAmountPayment

      // Amount derived from principal repayment
    [Serializable]
    private class ReferenceAmountPayment: Payment, INotionalChangeInfo
    {
      public ReferenceAmountPayment(
        Dt payDate, Currency ccy,
        double balanceBeforePayment,
        double principalPaymentAmount,
        Dt valueDate, IPriceCalculator calculator,
        double priceOverride)
        :base(payDate, ccy)
      {
        PrincipalPaymentAmount = principalPaymentAmount;
        NotionalBeforeChange = balanceBeforePayment;
        ValueDate = valueDate;
        PriceCalculator = calculator;
        PriceOverride = priceOverride;
      }

      internal double PrincipalPaymentAmount { get; }
      internal Dt ValueDate { get; }
      internal IPriceCalculator PriceCalculator { get; }

      internal double PriceOverride { get; }

      private double Price => double.IsNaN(PriceOverride)
        ? PriceCalculator.GetPrice(ValueDate).Value
        : PriceOverride;

      protected override double ComputeAmount()
      {
        return (1 - Price)*PrincipalPaymentAmount;
      }

      public Dt Date => PayDt;

      public double NotionalBeforeChange { get; }

      public double NotionalAfterChange
        => NotionalBeforeChange - PrincipalPaymentAmount;
    }
    #endregion

    #region Notional factor calculator

    [Serializable]
    class NotionalFactorCalculator
    {
      public NotionalFactorCalculator(
        IPriceCalculator priceCalculator,
        Dt valueDate,
        double underlierAmount)
      {
        Calculator = priceCalculator;
        ValueDate = valueDate;
        NotionalUnits = underlierAmount;
      }

      public double CalculateNotionalFactor()
      {
        return Calculator?.GetPrice(ValueDate).Value*NotionalUnits
          ?? NotionalUnits;
      }

      private IPriceCalculator Calculator { get; }
      private Dt ValueDate { get; }
      private double NotionalUnits { get; }
    }

    #endregion
  }

  /// <summary>
  ///  Interface INotionalChangeInfo provides the information
  ///  about notional changes, in most cases as results of
  ///  principal repayments.
  /// </summary>
  public interface INotionalChangeInfo
  {
    /// <summary>
    /// Gets the date on which the change happens.
    /// </summary>
    /// <value>The date</value>
    Dt Date { get; }

    /// <summary>
    /// Gets the notional before the change.
    /// </summary>
    /// <value>The underlying payment.</value>
    double NotionalBeforeChange { get; }

    /// <summary>
    /// Gets the notional after the change.
    /// </summary>
    /// <value>The remaining balance.</value>
    double NotionalAfterChange { get; }
  }
}
