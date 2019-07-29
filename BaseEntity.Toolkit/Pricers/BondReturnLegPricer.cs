// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Pricers.AssetReturnLegPricerFactory;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Bond Return Leg Pricer
  /// </summary>
  /// <remarks>
  /// 
  /// <para>This is the pricer to evaluate the bond total returns.</para>
  /// 
  /// <para>Consider a possibly amortizing and defaultable bond with <m>N</m> coupon periods.
  ///   Assuming the initial notional <m>n_1 = 1</m>.  Let</para>
  /// <ul>
  ///   <li><m>T_i</m>, <m>i=1, \ldots, N</m>, be coupon payment dates;</li>
  ///   <li><m>c_i</m>, <m>i=1, \ldots, N</m>, be coupon amounts per unit notional;</li>
  ///   <li><m>a_i</m>, <m>i=1, \ldots, N</m>, be amortizing amounts,
  ///     paid at <m>T_i</m>, respectively;</li>
  ///   <li><m>n_i</m>, <m>i=1, \ldots, N</m>, be notional at period begin;</li>
  ///   <li><m>T^e_i</m>, <m>i=1, \ldots, N</m>, be cut-off (ex-div) dates.
  ///     We assume <m>T^e_i \leq T_i</m>;</li>
  ///   <li><m>D(t, T)</m> denote the discount factor from <m>t</m> to <m>T</m>,
  ///     of the investor's funding curve;</li>
  ///   <li><m>D^b(t, T)</m> denote the discount factor
  ///     of the repo curve specific to the bond;</li>
  ///   <li><m>S(t, T)</m> denote the survival probability from <m>t</m> to <m>T</m>;</li>
  ///   <li><m>\gamma(t)</m> denote the recovery rate if the bond defaults at time <m>t</m>.</li>
  /// </ul>
  /// <para>The notional <m>n_i</m> and amortization <m>a_i</m> have the following relations</para>
  /// <math>
  /// n_{k} = \sum_{i=k}^N a_i
  /// ,\quad
  /// a_k = n_{k} - n_{k+1}
  /// ,\quad
  /// n_{N+1} = 0
  /// ,\quad
  /// k = 1,\ldots,N
  /// </math>
  /// <para>In particular, <m>1 = n_1 = \sum_1^N a_i</m>.  For regular non-amortizing bond,
  ///   we have <m>a_i = 0</m> for <m>i \lt N</m> and <m>a_N = 1</m>.</para>
  /// 
  /// <para><b>The bond price projection</b></para>
  /// 
  /// <para>Let <m>\tilde{c}_i = c_i\,n_i + a_i</m>.
  ///   Then the full price of the bond per unit bond notional at <m>t</m> is given by</para>
  /// <math>
  /// p(t) = \frac{1}{\eta(t)}\left(
  ///   \sum_{T^e_i \geq t}{\tilde{c}_i\,\tilde{D}^b(t,T_i)} +
  ///   \int_{t \leq \tau \lt T_N} 
  ///    {\gamma(\tau)\,\eta(\tau)\, D^b(t, \tau)\, d S(t, \tau) }
  ///   \tag{1}\label{eq:price}
  /// \right)
  /// </math>
  /// <para>where <m>D^b(t, T)</m> is the discount factor of the repo curve
  ///  specific to the bond, <m>\tilde{D}^b(t, T) \equiv S(t, T) D^b(t, T)</m>
  ///  is the corresponding <em>risky discount factor</em>, while
  ///  <m>\eta(t)</m> is the remaining notional at time <m>t</m>,
  ///   defined by</para>
  /// <math>
  ///  \eta(t) \equiv \begin{cases}
  ///      n_k &amp; T_{k-1} \lt t \leq T_k
  ///   \\ \quad
  ///   \\ 0 &amp; \text{otherwise}
  ///  \end{cases}
  /// </math>
  /// <para>The full price (i.e., clean price plus accrued) represents the total
  ///   amount paid by the investor for one dollar of the remaining bond notional.
  ///   It is also the total amount of the money he would receive if he sells it.
  /// </para>
  /// 
  /// <para><b>The bond total returns</b></para>
  /// 
  /// <para>Let <m>p(t)</m> be the full price of the bond at time <m>t</m>.
  /// The total returns on the bond between time <m>t_1</m> and <m>t_2</m>,
  /// <m>t_1 \lt t_2</m>, consists of the following four components:</para>
  /// <ul>
  ///   <li>The underlying coupons, <m>c_i</m>, of the eligible periods <m>i</m>
  ///     such that <m>t_1 \leq T^e_i \lt t_2</m>;</li>
  ///   <li>The reference amounts from amortization, <m>(1/p(t_1) - 1)\,a_i</m>,
  ///     of the eligible periods;</li>
  ///   <li>The price return from <m>t_1</m> to <m>t_2</m>, 
  ///     which is <m>(p(t_2)/p(t_1) - 1)</m> per unit investment;</li>
  ///   <li>The recovery return of <m>(\gamma(\tau)/p(t_1) - 1)</m> per unit remaining notional,
  ///     if the bond defaults at <m>\tau \in (t_1, t_2]</m>.</li>
  /// </ul>
  /// <para>Mathematically, the returns
  ///   at the evaluation time <m>t \in [t_1, t_2)</m>,
  ///   of one dollar investment made at <m>t_1</m>,
  ///   can be written as</para>
  /// <math>\begin{align}
  /// \mathrm{TotalReturns}(t, t_2) \equiv R(t, t_2) &amp;= 
  ///   \frac{1}{p(t_1)}\sum_{t \leq T^e_i \lt t_2}{\frac{c_i\,n_i}{\eta(t_1)}\,\tilde{D}(t, T_i)}
  ///  \notag \\ &amp;\quad +
  ///   \left(\frac{1}{p(t_1)} - 1\right)
  ///     \sum_{t \leq T^e_i \lt t_2}{\frac{a_i}{\eta(t_1)}\tilde{D}(t, T_i)}
  ///  \notag \\ &amp;\quad +
  ///    \left(\frac{p(t_2)}{p(t_1)} - 1\right)\frac{\eta(t_2)}{\eta(t_1)} \tilde{D}(t, t_2)
  ///  \notag \\ &amp;\quad +
  ///    \int_{t \leq \tau \lt t_2}{\left(\frac{\gamma(\tau)}{p(t_1)} - 1\right)
  ///    \frac{\eta(\tau)}{\eta(t_1)}\,D(t,\tau)\, d S(t, \tau)}
  ///   \tag{2}\label{eq:trs}
  /// \end{align}</math>
  /// <para>where <m>\tilde{D}(t, T_i) \equiv S(t, T_i)\,D(t, T_i)</m>
  ///   is the <em>risky discount factor</em>.</para>
  /// 
  /// <para>Our present value approach is based on equation (<m>\ref{eq:trs}</m>), where <m>t_1</m>
  ///  and <m>t_2</m> are interpreted as the begin and the end dates of a
  ///  price return period (or the effective and the maturity dates
  ///  of the bond return leg if there is only one price return period),
  ///  respectively, while <m>t</m> is the pricing date.
  ///  The present value of this leg is</para>
  /// <math>
  ///    \mathrm{PV}(t) \equiv \mathrm{TotalReturns}(t, t_2) \cdot \mathrm{Notional}(t_1)
  /// </math>
  /// <para>where <m>\mathrm{Notional}(t_1)</m> is the original total investment
  ///  at the TRS effective date.</para>
  /// 
  /// <para><b>Unrealized gain</b></para>
  /// 
  /// <para>Suppose a price return period begins at <m>t_1</m>
  ///   with price <m>p_1</m>, and it ends at some future time <m>t_2</m>.
  ///   The unrealized gain at time <m>t \in (t_1, t_2)</m> is the
  ///   the capital gain or loss if the investor sells the bond
  ///   at the current price <m>p(t)</m>.  Hence</para>
  /// <math>
  ///   \mathrm{UnrealizedGain}(t_1, t) \equiv
  ///      \left(p(t) - p_1\right) \cdot \mathrm{RemainingBondNotional}
  /// </math>
  /// <para>Or</para>
  /// <math>
  ///   \mathrm{UnrealizedGain}(t_1, t) =
  ///      \left(\frac{p(t)}{p_1} - 1\right)
  ///      \cdot \frac{\eta(t)}{\eta(t_1)}
  ///      \cdot \mathrm{Notional}(t_1)
  /// </math>
  /// 
  /// <para><b>A special case</b></para>
  /// 
  /// <para>When <m>D^p(t, T) = D(t, T)</m> for all <m>t</m> and <m>T</m>,
  ///  and when we price at time <m>t = t_1</m>,
  ///  the bond price equation (<m>\ref{eq:price}</m>)
  ///  implies that</para>
  /// <math>\begin{align}
  /// p(t_1)\eta(t_1)
  ///  &amp;= \sum_{T_i^e \geq t_1}\tilde{c}_i\,\tilde{D}(t_1, T_i)
  ///   + \int_{t_1 \leq \tau \lt T_N} {\gamma(\tau)\eta(\tau)
  ///     D(t_1, \tau)\, d S(t_1, \tau) }
  ///  \notag \\&amp;= \sum_{t_1 \leq T_i^e \lt t_2}\tilde{c}_i\,\tilde{D}(t_1, T_i)
  ///   + \int_{t_1 \leq \tau \lt t_2} {\gamma(\tau)\eta(\tau)
  ///     D(t_1, \tau)\, d S(t_1, \tau) }
  ///   + \tilde{D}(t_1, t_2)\,p(t_2)\eta(t_2)
  /// \end{align}</math>
  /// <para>Combine with the total return equation (<m>\ref{eq:trs}</m>), we have</para>
  /// <math>\begin{align}
  /// R(t_1, t_2) &amp;= 
  ///   1 - \sum_{t_1 \leq T^e_i \lt t_2}{\frac{a_i}{\eta(t_1)} \tilde{D}(t_1,T_i)}
  ///   - \frac{\eta(t_2)}{\eta(t_1)}\tilde{D}(t_1, t_2)
  ///   - \int_{t_1\leq \tau \lt t_2}
  ///    {\frac{\eta(\tau)}{\eta(t_1)} D(t_1,\tau)\,d S(t_1, \tau)}
  /// \end{align}</math>
  /// <para>In the case of non-amortizing, risk-less bond, 
  ///  the above equation reduces to the well-known formula</para>
  /// <math>
  ///   R(t_1, t_2) = 1 - D(t_1,\, t_2 \wedge T_N) 
  /// </math>
  /// <para>Both of the above equations can be used to verify that our pricing
  ///  model is correctly implemented.</para>
  /// </remarks>
  [Serializable]
  public class BondReturnLegPricer : AssetReturnLegPricer, IAssetReturnLegPricer<Bond>
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Product to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="discountForPriceProjection">The discount curve for price projection</param>
    /// <param name="survivalCurve">The survival curve associated with the underlying bond</param>
    /// <param name="assetPriceIndex">The asset price index</param>
    public BondReturnLegPricer(
      AssetReturnLeg<Bond> product,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve discountForPriceProjection,
      SurvivalCurve survivalCurve,
      IAssetPriceIndex assetPriceIndex)
      : base(product, asOf, settle, discountCurve,
        new CalibratedCurve[] {discountForPriceProjection, survivalCurve},
        assetPriceIndex)
    {
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the underlying bond pricer
    /// </summary>
    /// <returns>BondPricer.</returns>
    public BondPricer GetUnderlyerPricer()
    {
      var settle = Settle;
      var currentPrice = GetPriceCalculator().GetPrice(settle).Value;
      return new BondPricer(Bond, settle, settle,
        DiscountCurveForPriceProjection, SurvivalCurve, 0, TimeUnit.None,
        SurvivalCurve?.SurvivalCalibrator?.RecoveryCurve?.RecoveryRate(Bond.Maturity) ?? 0.0,
        CallableBondPricingMethod.None)
      {
        QuotingConvention = QuotingConvention.FullPrice,
        MarketQuote = currentPrice,
        Notional = BondNotional
      };
    }

    /// <summary>
    ///  Calculates the bond accrued interest
    /// </summary>
    /// <returns>System.Double</returns>
    public override double Accrued()
    {
      return BondNotional*new BondPricer(Bond, Settle, Settle).Accrued();
    }

    /// <summary>
    /// Gets the bond coupon payments.
    /// </summary>
    /// <param name="begin">The begin.</param>
    /// <param name="end">The end.</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    public override IEnumerable<Payment> GetUnderlyerPayments(Dt begin, Dt end)
    {
      // All bond payments from effective to maturity
      var bond = Bond;
      var bondPayments = bond.GetPaymentSchedule(SurvivalCurve);
      Dt defaultDate = AssetDefaultDate;
      if (!defaultDate.IsEmpty())
      {
        var notionalFn = GetPrjectionDeflator(bondPayments, bond.Notional);
        bondPayments = GetPaymentsWithDefault(
          bondPayments, Settle, defaultDate,
          notionalFn?.Invoke(defaultDate) ?? 1.0,
          SurvivalCurve, bond.Ccy);
      }
      // Find payments with cutoff dates in the period [begin, end),
      // where the begin date is included in and the end date
      // is excluded from the period.
      double initialBalance = bond.Notional, balance = initialBalance;
      foreach (var date in bondPayments.GetPaymentDates())
      {
        var payments = bondPayments.GetPaymentsOnDate(date);
        Dt cutoff = Dt.Empty, crEnd = Dt.Empty;
        double included = 0;
        foreach (var payment in payments.OfType<PrincipalExchange>())
        {
          crEnd = payment.GetCreditRiskEndDate();
          cutoff = payment.GetCutoffDate();
          if (cutoff < begin)
          {
            initialBalance -= payment.Amount;
            balance = initialBalance;
          }
          else if (cutoff < end)
          {
            included += payment.Amount;
          }
        }
        var balanceBeforePayment = balance;
        balance = balanceBeforePayment - included;
        if (included > 0 || included < 0)
        {
          yield return new BalanceChangeAnnotation(
            date, balanceBeforePayment/initialBalance,
            balance/initialBalance)
          {
            CutoffDate = cutoff,
            CreditRiskEndDate = crEnd,
          };
        }
        foreach (var payment in payments.Where(p=>!(p is PrincipalExchange)))
        {
          var rp = payment as RecoveryPayment;
          if (rp != null)
          {
            if (rp.EndDate > begin && rp.BeginDate < end)
            {
              yield return rp;
            }
            continue;
          }
          cutoff = payment.GetCutoffDate();
          if (cutoff >= begin && cutoff < end)
          {
            if (initialBalance.AlmostEquals(1.0))
              yield return payment;
            else
              yield return payment.UpdateNotional(1.0/initialBalance);
          }
        }
      }
    }

    /// <summary>
    /// Creates the price calculator
    /// </summary>
    /// <returns>IPriceCalculator</returns>
    protected override IPriceCalculator CreatePriceCalculator()
    {
      // All bond payments from effective to maturity
      var bond = Bond;
      var bondPayments = bond.GetPaymentSchedule(SurvivalCurve);
      var notionalFn = GetPrjectionDeflator(bondPayments, bond.Notional);
      var survivalCurve = SurvivalCurve;
      if (survivalCurve != null)
      {
        Dt defaultdate = survivalCurve.DefaultDate;
        if (!defaultdate.IsEmpty())
        {
        bondPayments = GetPaymentsWithDefault(
          bondPayments, Settle, defaultdate,
          notionalFn?.Invoke(defaultdate) ?? 1.0,
          survivalCurve, bond.Ccy);
        survivalCurve = null; // Does not need it anymore
        }
      }
      return new CashflowPriceCalculator(AsOf, bondPayments,
        DiscountCurveForPriceProjection, HistoricalPrices,
        notionalFn, GetPriceAdjustment(), survivalCurve)
      {
        PricingDatePaymentsExcluded = false,
        IndexName = bond.Description
      };
    }
    /// <summary>
    /// Gets the function to convert flat price to full price.
    /// </summary>
    /// <returns>Func&lt;System.Double, Dt, System.Double&gt;.</returns>
    private Func<double, Dt, double> GetPriceAdjustment()
    {
      if (AssetPriceIndex == null ||
        AssetPriceIndex.PriceType != QuotingConvention.FlatPrice)
      {
        return null;
      }
      return (new AccruedInterestAdjustment
      {
        Bond = Bond,
        PriceIndex = AssetPriceIndex,
        SurvivalCurve = SurvivalCurve,
      }).FlatToFullPrice;
    }

    private static Func<Dt, double> GetPrjectionDeflator(
      PaymentSchedule bondPayments, double initialBondFaceValue)
    {
      List<Dt> dates = null;
      List<double> values = null;
      // Find bond coupon payments in the period [begin, end),
      // where the begin date is included in and the end date
      // is excluded from the period.
      var balance = initialBondFaceValue;
      foreach (var payments in bondPayments.OfType<PrincipalExchange>()
        .GroupBy(p=>p.GetCutoffDate()))
      {
        var date = payments.Key;
        var repayment = payments.Aggregate(0.0, (v, p) => v + p.Amount);
        if (repayment > 0 || repayment < 0)
        {
          var originalBalance = balance;
          balance -= repayment;
          if (dates == null)
          {
            dates = new List<Dt>();
            values = new List<double>();
          }
          dates.Add(date);
          values.Add(originalBalance);
        }
      }
      if (dates == null || dates.Count <= 1)
        return null;
      values.Add(balance);
      return new IntervalIndexedValues<Dt, double>(
        dates.ToArray(), values.ToArray()).GetValue;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Underlying bond
    /// </summary>
    /// <value>Underlying bond</value>
    public Bond Bond => BondReturnLeg.UnderlyingAsset;

    /// <summary>
    /// Bond return leg
    /// </summary>
    /// <value>The product</value>
    public AssetReturnLeg<Bond> BondReturnLeg => (AssetReturnLeg<Bond>) Product;

    /// <summary>
    /// Gets notional (total face value) of the underlying bond.
    /// </summary>
    /// <value>The bond notional.</value>
    public double BondNotional => Notional/InitialPrice;

    /// <summary>
    /// Gets the bond return leg
    /// </summary>
    /// <value>The product</value>
    IAssetReturnLeg<Bond> IPricer<IAssetReturnLeg<Bond>>.Product => BondReturnLeg;

    /// <summary>
    /// Gets the discount curve for bond price projection,
    ///  which can be the repo curve specific to the underlying bond.
    /// </summary>
    /// <value>The discount curve for bond price projection</value>
    public DiscountCurve DiscountCurveForPriceProjection 
      => Get<DiscountCurve>(ReferenceCurves) ?? DiscountCurve;

    #endregion

    #region Data members

    [NonSerialized, NoClone] private IPriceCalculator _priceCalculator;

    #endregion

    #region Bond clean price to full price

    [Serializable]
    private class AccruedInterestAdjustment
    {
      public double FlatToFullPrice(double cleanPrice, Dt date)
      {
        var pi = PriceIndex;
        if (pi.SettlementDays != 0)
        {
          date = Dt.AddDays(date, pi.SettlementDays, pi.Calendar);
        }
        Dt dfltDate;
        if (SurvivalCurve != null &&
          !(dfltDate=SurvivalCurve.DefaultDate).IsEmpty() &&
          date <= dfltDate)
        {
          return 0.0;
        }
        return cleanPrice + new BondPricer(Bond, date).Accrued();
      }

      public Bond Bond { private get; set; }
      public IAssetPriceIndex PriceIndex { private get; set; }
      public SurvivalCurve SurvivalCurve { private get; set; }
    }

    #endregion
  }

}
