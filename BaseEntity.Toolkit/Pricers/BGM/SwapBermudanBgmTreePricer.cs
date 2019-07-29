/*
 * SwapBermudanBgmTreePricer.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Util.Configuration;
using Cashflow = BaseEntity.Toolkit.Cashflows.CashflowAdapter;
using SwaptionInfo = BaseEntity.Toolkit.Models.BGM.Native.SwaptionInfo;


namespace BaseEntity.Toolkit.Pricers.BGM
{
  /// <summary>
  ///   Swap Bermudan pricer based on the binomial tree model.
  /// </summary>
  /// <remarks>
  /// <para>This pricer is based on a binomial tree implementation of <abbr>LIBOR</abbr> market model
  /// which preserves martingale properties on every tree nodes.</para>
  /// 
  /// <para>&#xa0;</para>
  /// <para><b>The <abbr>LMM</abbr> model</b></para>
  /// 
  /// <para><i>Notations</i></para>
  /// <para>Let</para><ul>
  ///   <li><m>T_0 = 0</m>, and <m>T_n</m>, <m>n = 1, \ldots, N</m>, be the a set of forward dates;</li>
  ///   <li><m>\delta_n</m> the year fractions of the periods <m>(T_{n-1}, T_n]</m>,
  ///     <m>n = 1, \ldots, N</m>;</li>
  ///   <li><m>L_n(t)</m> the forward rate corresponding to period <m>(T_{n-1}, T_n]</m>
  ///     evaluated at time <m>t</m>;</li>
  ///   <li><m>\sigma_n(t)</m> the instantaneous volatilities of the forward rate <m>L_n</m>
  ///     as the function of time;</li>
  ///   <li><m>W_n(t)</m> be standard Brownian motions.</li>
  /// </ul>
  /// 
  /// <para><b>The process of forward rates</b></para>
  /// 
  /// <para><i>The log-normal case</i></para>
  /// <para>Under <m>T_N</m> measure, the processes of <m>L_n</m>'s are given by</para><math>
  ///   d L_n(t) = -L_n(t)\,\mu_n(t)\, d t + \sigma_n(t)\, L_n(t)\, d W_n(t)
  ///   ,\qquad n = 1, \ldots, N
  /// </math>
  /// <para>where</para><math>
  ///  \mu_N(t) = 0
  ///  ,\quad
  ///   \mu_n(t) = \sigma_n(t)\sum_{i=n+1}^{N}{\frac{
  ///     \rho_{i,n}\,\sigma_i(t)\,\delta_i\,L_i(t)}{1+\delta_i\,L_i(t)}}
  ///  \quad\text{for}\quad 0 \leq n \lt N
  /// </math>
  /// <para>Rewriting it in integral form, we have</para><math>
  ///   L_n(t) = L_n(0)\exp\left(
  ///     - \int_{0}^{t}{\mu_n(s)\,ds}
  ///     - \frac{1}{2}\int_{0}^{t}{\sigma_n^2(s)\,ds}
  ///     + \int_{0}^{t}{\sigma_n(s)\,d W_n(s)}
  ///   \right)
  /// </math>
  /// <para>The corresponding annuity under the terminal measure <m>T_N</m> is given by</para><math>
  /// A_N(t) = 1
  /// ,\quad
  /// A_n(t) = \prod_{i=n+1}^{N}\left(1+\delta_i\,L_i(t)\right)
  /// \quad\text{for}\quad 0 \leq n \lt N
  /// </math>
  /// <para>In order to apply binomial approximation to the process, we make two further assumptions:</para><ul>
  ///   <li><b>One factor</b>:
  ///     <m>\rho_{i,j} = \pm 1</m> and <m>W_n(t) = W(t)</m> for all <m>i, j, n</m>;</li>
  ///   <li><b>Common volatility component</b>:
  ///     <m>\sigma_n(t) = \beta_n\,\sigma(t)</m>,
  ///       where <m>\sigma(t) \geq 0</m> is common volatility to all rates,
  ///       <m>\beta_n</m>'s are rate specific factors which can be either positive or negative.</li>
  /// </ul>
  /// <para>In this case, the forward rate can be written as</para><math>
  ///   L_n(t) = G_n(t)\exp\left(
  ///     - \int_{0}^{t}{\mu_n(s)\,ds}
  ///     + \beta_n\,\int_{0}^{t}{\sigma(s)\,d W(s)}
  ///   \right)
  /// </math>
  /// <para>where <m>G_n(t)</m> is a deterministic function of time, given by</para><math>
  /// G_n(t) = L_n(0) \exp\left(-\frac{1}{2}\beta_n^2\int_0^t \sigma^2(s)\,ds\right)
  /// </math>
  /// <para>And <m>\mu_n(t)</m> is simplified to</para><math>
  ///   \mu_n(t) = \beta_n\,\sigma^2(t)\,\sum_{i=n+1}^{N}
  ///   \frac{\beta_i\,\delta_i\,L_i}{1+\delta_i\,L_i}
  /// </math>
  /// <para>Let</para><math>
  /// U_i(t) = \int_{0}^{t}{\sigma^2(s)
  /// \frac{\beta_n\,\delta_i\,L_i(s)}{1+\delta_i\,L_i(s)}
  /// }\,ds
  /// </math>
  /// <para>Then the rate becomes</para><math>
  /// L_n(t) = G_n(t)\,\exp\left(-\beta_n\sum_{i=n+1}^N{U_i(t)} + \beta_n\int_0^t \sigma(s)\,d W(s)\right)
  /// </math><para><i>The normal case</i></para>
  /// <para>With normal volatility we have</para><math>
  /// d L_n = -\sigma_n(t)\sum_{i=n+1}^{N}{\frac{\rho_{n,i}\,\sigma_i(t)\,\delta_i}{1+\delta_i\,L_i(t)}}
  /// + \sigma_n(t) d W_n(t)
  /// </math>
  /// <para>Under the assumption <m>\rho_{ij}=1</m> and <m>\sigma_n(t) = \beta_n\,\sigma(t)</m>, we have</para><math>
  /// d L_n(t) = -\beta_n\,\sigma^2(t)\sum_{i=n+1}^{N}{\frac{\beta_i\,\delta_i}{1+\delta_i\,L_i(t)}}
  /// + \beta_n\,\sigma(t)\,d W(t)
  /// </math>
  /// <para>In the integral form</para><math>
  /// L_n(t) = L_n(0) - \beta_n\sum_{i=n+1}^N \bar{U}_i(t) + \beta_n\int_0^t{\sigma(s)\,d W(s)}
  /// </math>
  /// <para>where</para><math>
  /// \bar{U}_i = \int_0^t {\frac{\beta_i\,\delta_i\sigma^2(s)}{1+\delta_i\,L_i(s)} ds}
  /// </math><para><i>The shifted log-normal case</i></para>
  /// <para>The shifted log-normal dynamics is given by</para><math>
  /// \frac{d L_n}{L_n + \alpha_n} = -\sigma_n(t)\sum_{i=n+1}^{N}{\frac{
  ///   \rho_{n,i}\,\sigma_i(t)\,\delta_i\,(L_i + \alpha_i)}{1+\delta_i\,L_i(t)}}
  /// + \sigma_n(t) d W_n(t)
  /// </math>
  /// <para>Under our assumptions on volatilities and correlations, it reduces to</para><math>
  /// \frac{d L_n(t)}{L_n(t) + \alpha_n} = -\beta_n\,\sigma^2(t)\sum_{i=n+1}^{N}{\frac{
  ///   \beta_i\,\delta_i\,(L_i(t) + \alpha_i)}{1+\delta_i\,L_i(t)}}
  /// + \beta_n\,\sigma(t)\,d W(t)
  /// </math>
  /// <para>In the integral form</para><math>
  /// L_n(t) = \tilde{G}_n(t)\,\exp\left(-\beta_n\sum_{i=n+1}^N{\tilde{U}_i(t)}
  ///   + \beta_n\int_0^t \sigma(s)\,d W(s)\right) - \alpha_n
  /// </math>
  /// <para>where</para><math>
  /// \tilde{G}_n(t) = \left(L_n(0) + \alpha_n\right)
  ///   \exp\left(-\frac{1}{2}\beta_n^2\int_0^t \sigma^2(s)\,ds\right)
  /// </math><math>
  /// \tilde{U}_i(t) = \int_{0}^{t}{\sigma^2(s)
  /// \frac{\beta_n\,\delta_i\,\left(L_i(s) + \alpha_i\right)}{1+\delta_i\,L_i(s)}
  /// }\,ds
  /// </math>
  /// 
  /// <para>&#xa0;</para>
  /// <para><b>The binomial approximation</b></para>
  /// <para>Our binomial tree is built on a set of time grids
  /// <m>\mathscr{P} \equiv \{t_m: m = 0, 1, \ldots, M\}</m>
  /// such that
  /// (a) <m>t_0 = 0</m>, <m>t_{m-1} \lt t_{m}</m> for all <m>m \gt 0</m>;
  /// (b) <m>T_n \in \mathscr{P}</m> for all <m>0 \leq n \leq N</m>.</para>
  /// <para>Let <m>\Delta_{m} \equiv t_{m} - t_{m-1}</m>.
  /// Denote <m>\Delta_{\!\mathscr{P}} \equiv \max\{t_{m}-t_{m-1}: m &gt; 0\}</m>,
  /// which measures the maximum step size of the time grid.
  /// For consistency we require the binomial tree converges
  /// to the rate processes as we refine the time grids
  /// such that <m>\Delta_{\!\mathscr{P}} \rightarrow 0</m>.</para>
  /// <para>There is exactly one jump at each time step.
  /// Denote by <m>p_m</m> the probability of an up jump from <m>t_{m-1}</m> to <m>t_m</m>.
  /// We allow <m>p_m</m> to vary accross time.</para>
  /// <para>The state is denoted by a pair <m>(m, k)</m>,
  /// where <m>m</m> is the number of steps, i.e., the time <m>t_m</m>,
  /// and <m>k</m> is the total number of <em>up</em> jumps realized
  /// during the period from 0 to <m>t_m</m>.
  /// Denote by <m>P(m,k)</m> the probability that exactly <m>k</m> up jumps
  /// by the time <m>t_m</m>.</para>
  /// <para>The forward rates and annuities in state <m>(m, k)</m> are approximated by</para><math>\begin{align}
  ///     L_n(m, k) &amp;= G_n(m)\,\exp\left(\beta_n\sum_{i=n+1}^N{U_i(m,k)} + \beta_n\,k\,d \right)
  ///  \\ A_n(m, k) &amp;= \prod_{n+1}^{N}\left(1 + \delta_i\,L_i(m,k)\right)
  ///  ,\qquad n = 1, \ldots, N
  /// \end{align}</math>
  /// <para>where <m>G_n(m)</m> is determined by the martingale condition that</para><math>
  ///   \mathrm{E}\left[L_n(t) A_n(t) \right]
  ///   = L_n(0) A_n(0)
  /// </math>
  /// <para>which yields</para><math>
  /// G_n(m) = \frac{L_n(0) A_n(0)}{
  /// \displaystyle
  ///  \sum_{k=0}^{m}{
  ///   P(m,k)\,e^{\beta_n\,\sum_{i&gt;n}U_i(m,k)+\beta_n k\,d}\,A_n(m,k)
  ///  }
  /// }
  /// </math>
  /// <para>The term <m>U(m,k)</m> approximates <m>U_n(t)</m>,
  /// which is defined by the following recursive formula</para><math>\begin{split}
  /// U_n(m, k) = q_{m,k}\left(U_n(m-1, k-1) + \mu_n(m-1, k-1)\,\Delta_m\,\sigma^2_m \right)
  ///   \\ + \left(1-q_{m,k}\right)\left(U_n(m-1,k) + \mu_n(m-1, k)\,\Delta_m\,\sigma^2_m \right)
  /// \end{split}
  /// </math><math>
  /// U(0, 0) = 0,\quad\text{and}\quad U(m, k) = 0\quad\text{for}\quad k \lt 0 \text{ or }k &gt; m
  /// </math>
  /// <para>where <m>\sigma_m \equiv \sigma(t_m)</m>,
  ///   <m>q_{m,k}</m> is the conditional probability that the system
  /// enters the state <m>(m,k)</m> by an up jump, given its already in it.</para><math>
  /// q_{m,k} = \frac{p_m P(m-1, k-1)}{p_m P(m-1, k-1) + (1-p_m)P(m-1, k)}
  /// </math>
  /// <para>And <m>\mu_n(m, k)</m> is the incremental drift from time <m>t_m</m> to <m>t_{m+1}</m>
  ///   starting from the state <m>(m,k)</m></para><math>
  /// \mu_n(m, k) = \sum_{i=n+1}^{N}{
  ///   \frac{\beta_i\,\delta_i\,L_i(m,k)}{1+\delta_i\,L_i(m,k)}
  ///  }
  ///  ,\qquad m = 0, \ldots, M-1
  /// </math>
  /// <para>Let </para><math>
  /// a_n(m, k) \equiv \sum_{i=n}^N \frac{\beta_i\,\delta_i\, L_i(m,k)}{1 + \delta_i\, L_i(m,k)}
  ///  =  \frac{\beta_n\,\delta_n\, L_n(m,k)}{1 + \delta_n\, L_n(m,k)} + a_{n+1}(m,k)
  /// </math>
  /// <para>Then we have <m>\mu_n(m,k) = \beta_n\,\sigma^2(t_m)\,a_{n+1}(m, k)</m>.</para>
  /// <para>The parameter <m>d</m> measures the total jump size (the sum of up jump an down jump)
  ///   per step, which is selected by</para><math>
  /// d = 2 \sqrt{\displaystyle \max_{0 \lt m \leq M}
  ///   \int_{t_{m-1}}^{t_m}{\sigma^2(s) ds}}
  /// </math>
  /// <para>Given <m>d</m>, the probability of an up jump at time <m>t_m</m> is determined
  /// by matching the volatility</para><math>
  ///  p_m(1-p_m)d^2 = \int_{t_{m-1}}^{t_m}{\sigma^2(s) ds}
  /// </math>
  /// <para>There are the two candidate solutions of the above equation, among which we pick the smaller one</para><math>
  /// p_m = \frac{1}{2}-\frac{1}{2}\sqrt{1 - \lambda_m^2}
  /// \quad\text{where}\;
  /// \lambda^2_m = \frac{
  /// \displaystyle \int_{t_{m-1}}^{t_m}{\sigma^2(s) ds}
  /// }{
  /// \displaystyle \max_{0 \lt i \leq M}
  ///   \int_{t_{i-1}}^{t_i}{\sigma^2(s) ds}
  /// }</math>
  /// <para>To simplify, we pick the time grids <m>t_m</m>'s such that
  ///   <m>\lambda^2_m = 1</m> and hence <m>p_m = \frac{1}{2}</m>
  ///   for most time points.</para>
  /// 
  /// <para>&#xa0;</para>
  /// <para><b>The evaluation of Bermudan swaptions</b></para>
  /// 
  /// <para>A Bermudan swaption consists a sequence of swaptions with 
  /// increasing expiration dates <m>T^e_1 \lt T^e_2 \lt \cdots \lt T^e_K</m>
  /// a common maturity date <m>T^m</m>, and possibly different strikes <m>C_k</m>.
  /// We adopt the following procedure to evaluate it.</para>
  /// 
  /// <para>1. Find the implied volatility and the price of each co-terminal swaptions,
  /// <m>(T^e_k, T^m)</m>, <m>k = 1, \ldost, K</m>, at the trade-specific strikes <m>C_k</m>,
  /// from the swaption volatility cube built from the market instruments;</para>
  /// 
  /// <para>2. Calibrate the binomial tree to match the prices of all the co-terminal
  /// swaptions simultaneously, assuming the forward forward volatility has the flat structure
  /// <m>\sigma_n(t) = \sigma_n</m>.  The tree volatility <m>\sigma_n</m>can be solved by
  /// bootstrapping;</para>
  /// 
  /// <para>3. Evaluate the Bermudan swaption on the resulting tree by backward induction.</para>
  /// 
  /// </remarks>
  [Serializable]
  public class SwapBermudanBgmTreePricer : PricerBase, IPricer,
    IAmericanMonteCarloAdapterProvider, ILockedRatesPricerProvider
  {
    // Logger
    private static readonly log4net.ILog logger =
      log4net.LogManager.GetLogger(typeof(SwapBermudanBgmTreePricer));

    #region Data
    private IVolatilityObject _volatilityObject;
    private DiscountCurve _discountCurve;
    private DiscountCurve _referenceCurve;
    private RateResets _rateResets;
    private readonly Swaption _swaption;

    // transitional data
    [NonSerialized, NoClone, Mutable]
    private Func<int, double> _volatilityCalculator = null;
    // transitional data
    [NonSerialized, NoClone, Mutable]
    private IRateSystemDistributions _tree = null;

    private const int NoConversionToLogNormalFlag = 1;
    private const int NoForwardValueProcessFlag = 2;
    private int _flags;
    #endregion Data

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="SwapBermudanBgmTreePricer"/> class.
    /// </summary>
    /// <param name="swap">The swap with embedded options.</param>
    /// <param name="asOf">The pricing date.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="volatilityObject">The volatility object.</param>
    public SwapBermudanBgmTreePricer(
      Swap swap,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      RateResets rateResets,
      IVolatilityObject volatilityObject)
      : base(swap, asOf, settle)
    {
      _discountCurve = discountCurve;
      _referenceCurve = referenceCurve;
      _rateResets = rateResets;
      _volatilityObject = volatilityObject;
      _swaption = null;
      NoConversionLogNormal = !ToolkitConfigurator.Settings
        .SwaptionVolatilityFactory.BloombergConsistentSwapBermudan;
      IsAmericanOption = GetOptionStyle(swap) == OptionStyle.American;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwapBermudanBgmTreePricer"/> class.
    /// </summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="asOf">The pricing date.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="exercisePeriods">The exercise periods.</param>
    /// <param name="volatilityObject">The volatility object.</param>
    public SwapBermudanBgmTreePricer(
      Swaption swaption,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      RateResets rateResets,
      IEnumerable<IOptionPeriod> exercisePeriods,
      IVolatilityObject volatilityObject)
      : base(SwapFrom(swaption, exercisePeriods), asOf, settle)
    {
      _discountCurve = discountCurve;
      _referenceCurve = referenceCurve;
      _rateResets = rateResets;
      _volatilityObject = volatilityObject;
      _swaption = swaption;
      NoConversionLogNormal = !ToolkitConfigurator.Settings
        .SwaptionVolatilityFactory.BloombergConsistentSwapBermudan;
      IsAmericanOption = GetOptionStyle(swaption) == OptionStyle.American;
    }

    private static Swap SwapFrom(Swaption swaption,
      IEnumerable<IOptionPeriod> exercisePeriods)
    {
      SwapLeg payer, receiver;
      switch (swaption.Type)
      {
      case PayerReceiver.Receiver:
        receiver = swaption.UnderlyingFixedLeg;
        payer = swaption.UnderlyingFloatLeg;
        break;
      case PayerReceiver.Payer:
        receiver = swaption.UnderlyingFloatLeg;
        payer = swaption.UnderlyingFixedLeg;
        break;
      default:
        throw new ToolkitException(String.Format(
          "Invalid swaption type \'{0}\'", swaption.Type));
      }
      IList<IOptionPeriod> exercisePeriodsList = null;
      if (exercisePeriods != null)
        exercisePeriodsList = exercisePeriods.ToList();
      if (NeedDefaultSchedule(exercisePeriodsList, swaption))
        exercisePeriodsList = GetDefaultExerciseSchedule(swaption);
      return new Swap(receiver, payer)
      {
        ExerciseSchedule = exercisePeriodsList,
        NotificationDays = swaption.NotificationDays
      };
    }

    private static bool NeedDefaultSchedule(IList<IOptionPeriod> exercisePeriodsList, Swaption swaption )
    {
      return (exercisePeriodsList == null || exercisePeriodsList.Count == 0) &&
             (GetOptionStyle(swaption) == OptionStyle.Bermudan 
             || GetOptionStyle(swaption) == OptionStyle.American);
    }

    private static IList<IOptionPeriod> GetDefaultExerciseSchedule(Swaption swaption)
    {
      // Create a default exercise schedule from the swaption expiration to the underlying swap maturity.
      var ret = new List<IOptionPeriod>();
      double exercisePremium = 0;
      IOptionPeriod p = (swaption.OptionType == OptionType.Call
        ? (IOptionPeriod)new CallPeriod(swaption.Effective, swaption.Maturity,
          1 + exercisePremium, 0, GetOptionStyle(swaption), 0)
        : new PutPeriod(swaption.Effective, swaption.Maturity,
          1 + exercisePremium, GetOptionStyle(swaption)));
      ret.Add(p);
      return ret;
    }

    private static OptionStyle GetOptionStyle(Swaption swpn)
    {
      return swpn.Style == OptionStyle.American
        ? (swpn.Swap.OptionTiming == SwapStartTiming.NextPeriod
          ? OptionStyle.Bermudan
          : swpn.Style)
        : swpn.Style;
    }

    #endregion

    #region Overrides of PricerBase

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      // Before check IsActive, we must make sure the product is not null
      // and all dates are valid!!!
      base.Validate(errors);

      if (!IsActive())
        return;

      if (_discountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Discount curve cannot be null");
      if (_referenceCurve == null)
        InvalidValue.AddError(errors, this, "ReferenceCurve", "Reference curve cannot be null");
      if (_volatilityObject == null)
      {
        InvalidValue.AddError(errors, this, "VolatilityObject",
          "Volatility object cannot be null or empty");
      }
      else
      {
        _volatilityObject.Validate(errors);
      }

      // base.Validate does not call Product.Validate.  We do it here.
      if (Swap.ExerciseSchedule.IsNullOrEmpty())
      {
        InvalidValue.AddError(errors, this, "ExerciseSchedule",
          "ExerciseSchedule cannot be null or empty");
      }
      Swap.Validate(errors);

      return;
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some public state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that all public states should be cleared or updated.</para>
    ///   <para>Derived Pricers may implement this and should call base.Reset()</para>
    /// </remarks>
    public override void Reset()
    {
      base.Reset();
      _tree = null;
    }

    /// <summary>
    /// Net present value of the product, excluding the value
    /// of any additional payment.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override double ProductPv()
    {
      if (Swap.OptionRight == OptionRight.RightToCancel && !HasScriptCoupon)
      {
        return BermudanValue() + SwapValue();
      }
      return BermudanValue();
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Get an instance of the BGM rate tree
    /// </summary>
    /// <returns>The BGM rate tree</returns>
    public IRateSystemDistributions GetRateTree()
    {
      return _tree ?? (_tree = BuildRateTree());
    }

    /// <summary>
    /// Calculate the underlying swap value.
    /// </summary>
    /// <returns>The swap value</returns>
    public double SwapValue()
    {
      bool isPayer;
      SwapLeg fixedLeg, floatLeg;
      var swap = Swap;
      if (swap.IsPayerFixed)
      {
        isPayer = true;
        fixedLeg = swap.PayerLeg;
        floatLeg = swap.ReceiverLeg;
      }
      else if (swap.IsReceiverFixed)
      {
        isPayer = false;
        fixedLeg = swap.ReceiverLeg;
        floatLeg = swap.PayerLeg;
      }
      else
      {
        throw new ToolkitException("Unable to handle swap without fixed leg yet.");
      }

      // Create a swap pricer and calculate the PV for the underlying swap:
      //   Receiver = fixlegPv - floatlegPv
      //   Payer = floatlegPv - fixlegPv
      double pv = new SwapLegPricer(fixedLeg, AsOf, Settle,
        1.0, DiscountCurve, null, null, null, null, null).Pv();
      pv -= new SwapLegPricer(floatLeg, AsOf, Settle,
        1.0, DiscountCurve, floatLeg.ReferenceIndex, ReferenceCurve,
        RateResets, null, null).Pv();
      return (isPayer ? -pv : pv) * CurrentNotional;
    }

    /// <summary>
    ///  Calculate the deltas with respect to co-terminal swap rates.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Delta is the change in <see cref="ProductPv"/> of the <c>Swap Bermudan</c>
    /// given a parallel change in the co-terminal swap rates. </para>
    /// 
    /// </remarks>
    /// <returns>The delta</returns>
    public double[] Delta()
    {
      return CalculateDelta(BuildCoTerminalSwaptions(), 0.0001);
    }

    /// <summary>
    ///  Calculate the gammas with respect to co-terminal swap rates.
    /// </summary>
    /// <param name="gammaBump">The gamma bump in bps.</param>
    /// <returns>The gamma values</returns>
    public double[] Gamma(double gammaBump)
    {
      var swpns = BuildCoTerminalSwaptions();
      if (swpns == null) return null;
      var delta = CalculateDelta(swpns, 0.0001);
      // Now move the rates up by 1bp.
      for (int i = 0; i < swpns.Length; ++i)
        swpns[i].Rate += gammaBump / 10000;
      var deltaUp = CalculateDelta(swpns, 0.0001);
      for (int i = 0; i < deltaUp.Length; ++i)
        deltaUp[i] -= delta[i];
      return deltaUp;
    }

    /// <summary>
    /// Calculate the PV change due to parallel bump of volatility
    /// by the specified amount.
    /// </summary>
    /// <param name="bump">The bump size (0.01 = 1%).</param>
    /// <returns>The vega.</returns>
    public double Vega(double bump)
    {
      var swpns = BuildCoTerminalSwaptions();
      double pv = CalculateBermudanPv(swpns);
      if (DistributionType == DistributionType.Normal)
      {
        for (int i = 0; i < swpns.Length; ++i)
          swpns[i].Volatility += bump * swpns[i].Rate;
      }
      else
      {
        for (int i = 0; i < swpns.Length; ++i)
          swpns[i].Volatility += bump;
      }
      return CalculateBermudanPv(swpns) - pv;
    }

    /// <summary>
    /// Calculate the pv change due to parallel bump of volatility
    /// by the specified amount.
    /// </summary>
    /// <param name="bump">The bump size (0.01 = 1%).</param>
    /// <returns>The vega.</returns>
    public double[] CalcCoTerminalSwaptionVegas(double bump)
    {
      var swpns = BuildCoTerminalSwaptions();
      var vegas = new double[swpns.Length];
      double pv = CalculateBermudanPv(swpns);
      for (int i = 0; i < swpns.Length; ++i)
      {
        swpns[i].Volatility += bump;
        vegas[i] = (CalculateBermudanPv(swpns) - pv) * Notional;
        swpns[i].Volatility -= bump;
      }

      return vegas;
    }

    /// <summary>
    ///  Calculate the deltas with respect to co-terminal swap rates.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Delta is the change in <see cref="ProductPv"/> of the <c>Swap Bermudan</c>
    /// given a parallel change in the co-terminal swap rates. </para>
    /// 
    /// </remarks>
    /// <returns>The delta</returns>
    public double DeltaHedge()
    {
      const double bump = 0.0001;
      var swpns = BuildCoTerminalSwaptions();
      if (swpns.Length == 0)
        return 0.0;
      for (int i = 0; i < swpns.Length; ++i)
      {
        swpns[i].Rate += bump;
      }
      double swpnDelta = CalculateBermudanPv(swpns);
      for (int i = 0; i < swpns.Length; ++i)
      {
        swpns[i].Rate -= bump;
      }
      swpnDelta -= CalculateBermudanPv(swpns);
      return swpnDelta / swpns[0].Level;
    }

    /// <summary>
    ///   Calculate the DV 01
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The DV 01 is the change in PV (MTM)
    ///   if the underlying discount curve is shifted in parallel up by
    ///   one basis point(for both swap legs).</para>
    ///
    ///   <para>The DV 01 is calculated by calculating the PV (MTM)
    ///   then bumping up the underlying discount curve by 1 bps
    ///   and re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    ///
    /// <returns>DV01</returns>
    ///
    public double DV01()
    {
      return Rate01(0.0, 4.0, 4.0)[0];
    }

    /// <summary>
    ///  Calculate the gamma with respect to 1bp parallel rate bump.
    /// </summary>
    /// <returns>The gamma value</returns>
    public double Gamma()
    {
      return Rate01(0.0, 4.0, 4.0)[1];
    }

    /// <summary>
    ///  Calculates interest rate 01 and returns both delta and gamma.
    /// </summary>
    /// <param name="initialBump">The initial bump.</param>
    /// <param name="upBump">Up bump.</param>
    /// <param name="downBump">Down bump.</param>
    /// <returns>Rate 01</returns>
    private double[] Rate01(double initialBump, double upBump, double downBump)
    {
      var swpns = BuildCoTerminalSwaptions();
      if (swpns.Length == 0)
        return new double[] {0.0,0.0};
      var pricer = (SwapBermudanBgmTreePricer)ShallowCopy();

      //For backward compatibility, we keep the volatility constant.
      pricer._volatilityCalculator = (i) => swpns[i].Volatility;

      // Calculate
      //Note: this is NOT thread safe for it bumps the original curves!!!
      DataTable dataTable = Sensitivities.Rate(
        pricer, null, initialBump, upBump, downBump, false, true,
        BumpType.Uniform, null, true, false, null, false, null);

      double delta = (double)(dataTable.Rows[0])["Delta"];
      double gamma = (double)(dataTable.Rows[0])["Gamma"];

      if (GetSwaptionType() == PayerReceiver.Payer)
        return new[] { delta, gamma };
      else
        return new[] { -delta, gamma };
    }

    /// <summary>
    /// Calculate the implied flat log-normal volatility for the specified fair value.
    /// </summary>
    /// <param name="fv">The fair value.</param>
    /// <param name="outputType">Type of the output.</param>
    /// <param name="volUpperBound">The volatility upper bound (0 to use the default).</param>
    /// <returns>The implied volatility.</returns>
    public double ImpliedVolatility(double fv,
      DistributionType outputType, double volUpperBound)
    {
      var swpns = BuildCoTerminalSwaptions();
      if (swpns.Length == 0 || fv <=0)
        return 0.0;
      var pricer = (SwapBermudanBgmTreePricer)ShallowCopy();
      pricer._volatilityObject = new FlatVolatility
      {
        DistributionType = outputType
      };

      Func<double, double> fn = (v) =>
      {
        for (int i = 0; i < swpns.Length; ++i)
        {
          swpns[i].Volatility = v;
        }
        return pricer.CalculateBermudanPv(swpns);
      };

      var solver = new Brent2();
      solver.setToleranceX(1E-6);
      solver.setToleranceF(1E-6);
      solver.setLowerBounds(0.0);
      if (volUpperBound <= 0)
      {
        volUpperBound = outputType == DistributionType.Normal
          ? 2.0 : 20.0;
      }
      double x0 = swpns[0].Volatility;
      if (_volatilityObject.DistributionType == DistributionType.LogNormal
        && outputType == DistributionType.Normal)
      {
        x0 *= 0.04;
      }
      else if (_volatilityObject.DistributionType == DistributionType.Normal
        && outputType == DistributionType.LogNormal)
      {
        x0 /= 0.04;
      }
      solver.setUpperBounds(volUpperBound);
      return solver.solve(fn, null, fv, x0 / 2, x0 * 2);
    }

    ///<summary>
    /// Build a sequence of co-terminal swaption pricers from the Bermudan.
    ///</summary>
    ///<returns>An array of co-terminal swaption pricers.</returns>
    public SwaptionBlackPricer[] CoTerminalSwaptionPricers()
    {
      var pricers = BuildCoTerminalSwaptions(true).Item2;
      foreach (var pricer in pricers)
        pricer.Notional = Notional;
      return pricers;
    }

    /// <summary>
    /// Determine next notification date (after AsOf date)
    /// </summary>
    /// <returns></returns>
    public Dt NextNotificationDate()
    {
      var swaptions = BuildCoTerminalSwaptions();
      return swaptions.Length > 0 ? BuildCoTerminalSwaptions()[0].Date : Dt.Empty;
    }

    /// <summary>
    /// Calculate effective strike at next call date (after AsOf date)
    /// </summary>
    /// <returns></returns>
    public double EffectiveStrikeAtNextCall()
    {
      var swaptions = BuildCoTerminalSwaptions();
      return swaptions.Length > 0 ? BuildCoTerminalSwaptions()[0].Coupon : 0.0;
    }

    /// <summary>
    /// Calculates the delta.
    /// </summary>
    /// <param name="swpns">The co-terminal swaption</param>
    /// <param name="bump">The bump.</param>
    /// <returns>The delta</returns>
    private double[] CalculateDelta(SwaptionInfo[] swpns,
      double bump)
    {
      if (swpns == null || swpns.Length == 0) return null;
      double[] deltas = new double[swpns.Length];
      for (int i = 0; i < swpns.Length; ++i)
      {
        double rate = swpns[i].Rate;
        swpns[i].Rate = rate - bump;
        double dp = CalculateBermudanPv(swpns);
        swpns[i].Rate = rate + bump;
        double up = CalculateBermudanPv(swpns);
        deltas[i] = (up - dp) / (2 * bump);
        swpns[i].Rate = rate;
      }
      return deltas;
    }

    /// <summary>
    ///  Calculate the Bermudan value.
    /// </summary>
    /// <returns>The Bermudan value.</returns>
    private double BermudanValue()
    {
      var swpns = BuildCoTerminalSwaptions();
      return CalculateBermudanPv(swpns) * CurrentNotional;
    }

    /// <summary>
    /// Calculates the net present value for the Bermudan represented
    /// by a sequence of co-terminal swaption.
    /// </summary>
    /// <param name="swpns">The co-terminal swaption</param>
    /// <returns>The present value.</returns>
    private double CalculateBermudanPv(SwaptionInfo[] swpns)
    {
      if (swpns.Length == 0)
        return 0.0;

      if (StepSize.N > 0 && StepSize.Units != TimeUnit.None)
      {
        int step = StepSize.Days;
        Dt begin = AsOf;
        var interval = Dt.Diff(begin, swpns[0].Date);
        swpns[0].Steps = Math.Max(
          Math.Min(10, interval), (interval + step - 1) / step);
        begin = swpns[0].Date;
        for (int i = 1; i < swpns.Length; ++i)
        {
          interval = Dt.Diff(begin, swpns[i].Date);
          swpns[i].Steps = Math.Max(
            Math.Min(5, interval), (interval + step - 1) / step);
          begin = swpns[i].Date;
        }
      }

      if (HasScriptCoupon)
      {
        return CalculateBermudanPvWithScriptCoupon(swpns);
      }

      return BgmTreeSwaptionEvaluation.CalculateBermudanPv(
        swpns, AsOf, Swap.Maturity, _volatilityObject.DistributionType,
        !NoConversionLogNormal, IsAmericanOption, BgmTreeOptions);
    }


    private bool HasScriptCoupon
    {
      get
      {
        return Swap != null && (Swap.PayerLeg.CouponFunction != null
          || Swap.ReceiverLeg.CouponFunction != null);
      }
    }

    private double CalculateBermudanPvWithScriptCoupon(
      SwaptionInfo[] swpns)
    {
      var isExercisable = new bool[swpns.Length + 1];
      for (int i = 0; i < swpns.Length; ++i)
      {
        var set = isExercisable[i + 1] = (swpns[i].OptionType != OptionType.None);
        if (!set) swpns[i].OptionType = OptionType.Call;
      }
      var tree = _tree = BuildRateTree(false);

      // Set up what to do on each node dates
      var nodeDateKinds = new NodeDateKind[isExercisable.Length];

      int firstCallableDate = 1;
      if (Swap.OptionRight == OptionRight.RightToEnter)
      {
        // Ignore cash flows until the first call date.
        for (int i = 1; i < isExercisable.Length; ++i)
        {
          if (isExercisable[i])
          {
            firstCallableDate = i;
            break;
          }
          nodeDateKinds[i] = NodeDateKind.Ignore;
        }
      }

      for (int i = firstCallableDate; i < isExercisable.Length; ++i)
      {
        if (!isExercisable[i]) continue;
        nodeDateKinds[i] = NodeDateKind.Exercisable;
      }

      // Calculate and return the option value
      return tree.EvaluateBermudan(nodeDateKinds,
        GetCouponCalculator(Settle, Swap), null);
    }

    private IRateSystemDistributions BuildRateTree(bool check = true)
    {
      var swpns = BuildCoTerminalSwaptions();
      if (check)
      {
        for (int i = 0; i < swpns.Length; ++i)
        {
          if (swpns[i].OptionType == OptionType.None)
            swpns[i].OptionType = OptionType.Call;
        }
      }

      Dt asOf = AsOf, maturity = Swap.Maturity;
      var tree = new RateSystem();
      Models.BGM.Native.BgmBinomialTree.calibrateCoTerminalSwaptions(
        asOf, maturity, swpns, 1E-8, 0, tree);

      tree.AsOf = asOf;
      tree.TenorDates = swpns.Select(s => s.Date).Append(maturity).ToArray();
      tree.NodeDates = Enumerable.Repeat(asOf, 1)
        .Concat(swpns.Select(s => s.Date)).ToArray();
      return tree;
    }

    private static Func<IRateSystemDistributions, int, int, double>
      GetCouponCalculator(Dt settle, Swap swap)
    {
      Func<IPeriod, IRateCalculator, double> fn;
      double fixedLegCoupon = 0;
      int sign = 1;
      SwapLeg leg = swap.ReceiverLeg;
      if (leg.CouponFunction != null)
      {
        fixedLegCoupon = swap.PayerLeg.Coupon;
      }
      else if ((leg=swap.PayerLeg).CouponFunction!= null)
      {
        fixedLegCoupon = swap.ReceiverLeg.Coupon;
        sign = 1;
      }
      else
      {
        throw new ToolkitException("No coupon function found");
      }
      fn = leg.CouponFunction;

      var schedule = leg.GetSchedule();
      var firstCoupon = schedule.GetNextCouponDate(settle);
      var accrualStart = schedule.GetPrevCouponDate(settle);
      return (rsd, date, state) =>
      {
        var period = date == 0
          ? new Period(accrualStart, firstCoupon)
          : new Period(rsd.NodeDates[date], rsd.TenorDates[date]);
        var calc = new TreeNodeRateCalculator
        {
          ProjectionCurve = rsd.GetDiscountCurve(date, state).Curve
        };
        var cpn = (fn(period, calc) - fixedLegCoupon) * sign;
        return cpn;
      };
    }

    /// <summary>
    ///  Gets or sets the indicator whether this should be treated as an American option.
    /// </summary>
    public bool IsAmericanOption { get; set; }

    /// <summary>
    ///  Gets or sets the suggested step size.
    /// </summary>
    public Tenor StepSize { get; set; }

    /// <summary>
    /// Builds a sequence of co-terminal swaption from the Bermudan.
    /// </summary>
    /// <returns>An array of co-terminal swaption</returns>
    private SwaptionInfo[] BuildCoTerminalSwaptions()
    {
      var swpn = BuildCoTerminalSwaptions(false).Item1;
      return swpn;
    }

    /// <summary>
    /// Builds a sequence of co-terminal swaption from the Bermudan.
    /// </summary>
    /// <param name="withPricer">if set to <c>true</c>, include an array
    ///  the swaption black pricers; otherwise, the pricer array is null.</param>
    /// <returns>An array of co-terminal swaption</returns>
    public Tuple<SwaptionInfo[], SwaptionBlackPricer[]>
      BuildCoTerminalSwaptions(bool withPricer)
    {
      bool onlyCouponDatesCallable = false;
      var periods = Swap.ExerciseSchedule.ToArray();
      var swpn = _swaption;
      if (swpn == null)
      {
        GetSwaptionAndPeriods(ref periods, out swpn); 
        onlyCouponDatesCallable = GetOptionStyle(swpn) == OptionStyle.Bermudan;
      }
      else
        swpn = (Swaption) swpn.CloneObjectGraph(); 

      var modPeriods = GetOptionStyle(swpn) == OptionStyle.American
        ? periods.OrderBy(p => p.StartDate).MergeOverlapPeriods() : periods;

      return BuildCoTerminalSwaptions(swpn,
        onlyCouponDatesCallable, modPeriods.ToArray(), withPricer);
    }

    /// <summary>
    /// Gets the type of the swaption.
    /// </summary>
    /// <returns></returns>
    private PayerReceiver GetSwaptionType()
    {
      if (_swaption != null) return _swaption.Type;
      var swap = Swap;
      bool isPayer = (swap.IsPayerFixed);
      bool isRightToCancel = swap.OptionRight == OptionRight.RightToCancel;
      return ((isPayer && isRightToCancel) || !(isPayer ||
        isRightToCancel)) ? PayerReceiver.Receiver : PayerReceiver.Payer;
    }

    /// <summary>
    /// Creates the swaption and and builds exercise periods.
    /// </summary>
    /// <param name="periods">The periods.</param>
    /// <param name="swaption">The swaption.</param>
    private void GetSwaptionAndPeriods(
      ref IOptionPeriod[] periods,
      out Swaption swaption)
    {
      if (periods == null)
      {
        throw new ToolkitException("Periods cannot be null.");
      }
      bool isPayer;
      SwapLeg fixedLeg, floatLeg;
      var swap = Swap;
      if (swap.IsPayerFixed)
      {
        isPayer = true;
        fixedLeg = swap.PayerLeg;
        floatLeg = swap.ReceiverLeg;
      }
      else if (swap.IsReceiverFixed)
      {
        isPayer = false;
        fixedLeg = swap.ReceiverLeg;
        floatLeg = swap.PayerLeg;
      }
      else
      {
        throw new ToolkitException("Unable to handle swap without fixed leg yet.");
      }

      // Now evaluate the option value.
      // For the right to enter, the option type is Call for payer
      //   and Put for receiver;
      // For the right to cancel, the option type is just opposite:
      //   Put for payer and call for receiver.
      // We make sure the call periods have the right types.
      if (swap.OptionRight != OptionRight.None)
      {
        bool isRightToCancel = swap.OptionRight == OptionRight.RightToCancel;
        periods = ((isPayer && isRightToCancel) ||
          !(isPayer || isRightToCancel)
          ? periods.Select((p) => (IOptionPeriod)new PutPeriod(
            p.StartDate, p.EndDate, p.ExercisePrice, p.Style))
          : periods.Select((p) => (IOptionPeriod)new CallPeriod(
            p.StartDate, p.EndDate, p.ExercisePrice, 1.0, p.Style, 0)))
          .ToArray();
      }

      // Create a swaption
      swaption = new Swaption(AsOf, Settle, swap.Ccy,
        fixedLeg, floatLeg, swap.NotificationDays,
        isPayer ? PayerReceiver.Payer : PayerReceiver.Receiver,
        swap.OptionStyle, Double.NaN){SwapStartTiming = swap.OptionTiming};
      return;
    }

    private static OptionStyle GetOptionStyle(Swap swap)
    {
      return swap.OptionStyle == OptionStyle.American
             && swap.OptionTiming == SwapStartTiming.NextPeriod
        ? OptionStyle.Bermudan : swap.OptionStyle;
    }
    /// <summary>
    /// Builds a sequence of co-terminal swaption from the Bermudan.
    /// </summary>
    /// <param name="swpn">The swaption.</param>
    /// <param name="onlyCouponDatesCallable">if set to <c>true</c> [only coupon dates callable].</param>
    /// <param name="optionPeriods">The option periods.</param>
    /// <param name="withPricer">if set to <c>true</c> [with pricer].</param>
    /// <returns></returns>
    private Tuple<SwaptionInfo[], SwaptionBlackPricer[]>
      BuildCoTerminalSwaptions(Swaption swpn,
      bool onlyCouponDatesCallable,
      IOptionPeriod[] optionPeriods,
      bool withPricer)
    {
      if (HasScriptCoupon)
      {
        return BuildCoTerminalSwaptionsForScriptPayoffs(
          swpn, optionPeriods, withPricer);
      }

      var getVol = _volatilityCalculator;
      Dt asOf = AsOf;
      Dt settle = Settle;

      // Find all the call dates.
      var dates = GetCallDates(swpn, onlyCouponDatesCallable, optionPeriods);
      if (dates.Item1.Length <= 0)
      {
        logger.DebugFormat("No exercisable date in {0}", swpn.Description);
        return new Tuple<SwaptionInfo[], SwaptionBlackPricer[]>(
          new SwaptionInfo[0], new SwaptionBlackPricer[0]);
      }

      // Generate cash flows
      var rateResets = RateResets;
      var discountCurve = DiscountCurve;
      var referenceCurve = ReferenceCurve;

      var treeOpt = BgmTreeOptions;
      var accuracy = treeOpt != null && treeOpt.CalibrationTolerance > 0
        ? treeOpt.CalibrationTolerance : 1E-6;

      // Build co-terminal swaptions
      var infos = new List<SwaptionInfo>();
      var pricers = withPricer ? new List<SwaptionBlackPricer>() : null;
      for (int i = 0; i < dates.Item1.Length; ++i)
      {
        Dt fwdStart = dates.Item1[i];
        int idx = optionPeriods.IndexOf(fwdStart);
        Debug.Assert(idx >= 0 && idx < optionPeriods.Length,
          "index out of range.");

        Cashflow unitCf, fixedCf, unitFloatCf, floatCf;
        SetSwaptionStart(swpn, swpn.OptionRight == OptionRight.RightToEnter ? fwdStart : dates.Item2);
        swpn.GenerateCashflows(asOf, settle, discountCurve,
          referenceCurve, rateResets, true, out unitCf, out fixedCf,
          out unitFloatCf, out floatCf);

        // We store the exercise price, which is defined as (1 + exercisePremium)
        double exerciseCost = (optionPeriods[idx].ExercisePrice - 1)
          * fixedCf.GetRemainingNotional(fwdStart)
            * discountCurve.DiscountFactor(asOf, fwdStart)/fixedCf.OriginalPrincipal;

        double level, rate, strike;
        RateVolatilityUtil.CalculateLevelRateStrike(asOf, fwdStart,
          unitCf, fixedCf, unitFloatCf, floatCf, discountCurve, swpn.UnderlyingFixedLeg.Freq,
          out level, out rate, out strike);
        if (Math.Abs(level) < 1E-15)
        {
          if (logger.IsDebugEnabled)
          {
            logger.DebugFormat("Date {0}, Level {1}, Rate {2}, Strike {3}",
              fwdStart, level, rate, strike);
          }
          continue;
        }
        if ((rate < 0) && _volatilityObject.DistributionType == DistributionType.LogNormal)
        {
          throw new ToolkitException(
            "The implied swap rate {0} is negative at date {1}",
            rate, fwdStart);
        }
        var optype = optionPeriods[idx].Type;
        if (optype == OptionType.Call)
        {
          strike += exerciseCost / level;
        }
        else
        {
          strike -= exerciseCost / level;
        }

        swpn.Strike = strike;
        if (!optionPeriods[idx].NotificationDate.IsEmpty())
          swpn.Expiration = optionPeriods[idx].NotificationDate;
        swpn.Maturity = fwdStart;
        swpn.Type = optype == OptionType.Call
          ? PayerReceiver.Payer : PayerReceiver.Receiver;

        if (swpn.Expiration <= Settle ||
          (infos.Count > 0 && infos.Last().Date >= swpn.Expiration))
        {
          continue;
        }

        var pricer = new SwaptionBlackPricer(swpn, asOf, settle,
          referenceCurve, discountCurve, getVol == null ? _volatilityObject
            : new FlatVolatility
            {
              Volatility = getVol(i),
              DistributionType = _volatilityObject.DistributionType,
            }) { RateResets = rateResets };
        pricer.DebugValidate();
        var price = pricer.ProductPv();
        if (Double.IsNaN(price) || price < 0)
        {
          throw new ToolkitException(
            "One of the CoTerminal swaptions have invalid value {0} at {1}",
            price, fwdStart);
        }
        var vol = price <= 0.0 ? pricer.Volatility : pricer.IVol();
        if (pricers != null)
        {
          // Create an independent swaption.
          pricer.Product = (Swaption)swpn.Clone();
          pricer.Notional = Notional;
          pricers.Add(pricer);
        }

        int steps = treeOpt == null ? 0 : (infos.Count == 0
          ? treeOpt.InitialSteps : treeOpt.MiddleSteps);
        infos.Add(new SwaptionInfo
        {
          Date = swpn.Expiration,
          Level = level,
          Rate = rate,
          Coupon = strike,
          Value = price,
          Volatility = vol,
          OptionType = optype,
          Steps = steps,
          Accuracy = accuracy
        });
        if (logger.IsDebugEnabled)
        {
          logger.DebugFormat(
            "Date {0}, Level {1}, Rate {2}, Strike {3}, Vol {4}, Price {5}",
            fwdStart, level, rate, strike, vol, price);
        }
      }
      logger.DebugFormat("Number of exercisable dates: {0}", infos.Count);
      return new Tuple<SwaptionInfo[], SwaptionBlackPricer[]>(
        infos.ToArray().CheckTreeOptions(asOf, DistributionType, BgmTreeOptions),
        pricers != null ? pricers.ToArray() : null);
    }


    /// <summary>
    /// Builds a sequence of co-terminal swaption from the Bermudan.
    /// </summary>
    /// <param name="swpn">The swaption.</param>
    /// <param name="optionPeriods">The option periods.</param>
    /// <param name="withPricer">if set to <c>true</c> [with pricer].</param>
    /// <returns></returns>
    private Tuple<SwaptionInfo[], SwaptionBlackPricer[]>
      BuildCoTerminalSwaptionsForScriptPayoffs(Swaption swpn,
      IOptionPeriod[] optionPeriods,
      bool withPricer)
    {
      var getVol = _volatilityCalculator;
      Dt asOf = AsOf;
      Dt settle = Settle;
      var maturity = swpn.Swap.Maturity;

      // Generate cash flows
      var rateResets = RateResets;
      var discountCurve = DiscountCurve;
      var referenceCurve = ReferenceCurve;

      Cashflow unitCf, fixedCf, unitFloatCf, floatCf;
      swpn.GenerateCashflows(asOf, settle, discountCurve,
        referenceCurve, rateResets, true, out unitCf, out fixedCf,
        out unitFloatCf, out floatCf);

      // Build co-terminal swaptions
      var infos = new List<SwaptionInfo>();
      var pricers = withPricer ? new List<SwaptionBlackPricer>() : null;
      var count = unitFloatCf.Count;
      for (int i = 0; i < count; ++i)
      {
        Dt fwdStart = unitFloatCf.GetDt(i);
        if (fwdStart <= settle) continue;
        if (fwdStart >= maturity) break;

        SetSwaptionStart(swpn, fwdStart);

        double level, rate, strike;
        RateVolatilityUtil.CalculateLevelRateStrike(asOf, fwdStart,
          unitCf, fixedCf, unitFloatCf, floatCf, discountCurve, swpn.UnderlyingFixedLeg.Freq,
          out level, out rate, out strike);
        if (Math.Abs(level) < 1E-15)
        {
          if (logger.IsDebugEnabled)
          {
            logger.DebugFormat("Date {0}, Level {1}, Rate {2}, Strike {3}",
              fwdStart, level, rate, strike);
          }
          continue;
        }
        if (rate < 0)// && volatilityObject_.DistributionType == DistributionType.LogNormal)
        {
          throw new ToolkitException(
            "The implied swap rate {0} is negative at date {1}",
            rate, fwdStart);
        }

        int idx = optionPeriods.IndexOf(fwdStart);
        var optype = idx < 0 ? OptionType.Call : optionPeriods[idx].Type;
        swpn.Strike = strike = rate;
        swpn.Maturity = fwdStart;
        swpn.Type = optype == OptionType.Call
          ? PayerReceiver.Payer : PayerReceiver.Receiver;

        if (swpn.Expiration <= Settle ||
          (infos.Count > 0 && infos.Last().Date >= swpn.Expiration))
        {
          continue;
        }

        var pricer = new SwaptionBlackPricer(swpn, asOf, settle,
          referenceCurve, discountCurve, getVol == null ? _volatilityObject
            : new FlatVolatility
            {
              Volatility = getVol(i),
              DistributionType = _volatilityObject.DistributionType,
            }) { RateResets = rateResets };
        pricer.DebugValidate();
        var price = pricer.ProductPv();
        if (Double.IsNaN(price) || price < 0)
        {
          throw new ToolkitException(
            "One of the CoTerminal swaptions have invalid value {0} at {1}",
            price, fwdStart);
        }
        var vol = price <= 0.0 ? pricer.Volatility : pricer.IVol();
        if (pricers != null)
        {
          // Create an independent swaption.
          pricer.Product = (Swaption)swpn.Clone();
          pricer.Notional = Notional;
          pricers.Add(pricer);
        }

        infos.Add(new SwaptionInfo
        {
          Date = swpn.Expiration,
          Level = level,
          Rate = rate,
          Coupon = strike,
          Value = price,
          Volatility = vol,
          OptionType = idx < 0 ? OptionType.None : optype,
          Accuracy = 1E-6
        });
        if (logger.IsDebugEnabled)
        {
          logger.DebugFormat(
            "Date {0}, Level {1}, Rate {2}, Strike {3}, Vol {4}, Price {5}",
            fwdStart, level, rate, strike, vol, price);
        }
      }
      logger.DebugFormat("Number of exercisable dates: {0}", infos.Count);
      return new Tuple<SwaptionInfo[], SwaptionBlackPricer[]>(
        infos.ToArray(), pricers != null ? pricers.ToArray() : null);
    }

    // Create a shallow copy of swap with the effective date changed.
    private static SwapLeg ChangeSwaplegEffective(SwapLeg swap, Dt effective)
    {
      swap = (SwapLeg)swap.ShallowCopy();
      swap.Effective = effective;
      if (Roll(swap.FirstCoupon, swap) <= effective) swap.FirstCoupon = Dt.Empty;
      if (!swap.LastCoupon.IsEmpty() && Roll(swap.LastCoupon, swap) <= effective)
      {
        swap.Effective = swap.LastCoupon;
        swap.LastCoupon = Dt.Empty;
      }
      swap.Validate();
      return swap;
    }

    // Roll the intermediate coupon dates according to cashflow flags.
    //TODO: This can be made a general schedule utility.
    private static Dt Roll(Dt date, IScheduleParams sp)
    {
      return date.IsEmpty() || (sp.CashflowFlag & CashflowFlag.AccrueOnCycle) != 0
        ? date : Dt.Roll(date, sp.Roll, sp.Calendar);
    }

    // Find all the call dates.
    private Tuple<Dt[], Dt> GetCallDates(Swaption swpn,
      bool onlyCouponDatesCallable,
      IList<IOptionPeriod> optionPeriods)
    {
      Dt asOf = AsOf;
      Dt settle = Settle;
      Dt nextStart = Dt.Empty;

      var savedFixedLeg = swpn.UnderlyingFixedLeg;

      // If no rate reset, we do extra check to see if we need that.
      if (swpn.UnderlyingFixedLeg.Effective < settle)
      {
        // Find the first coupon date after the settle.
        var periods = swpn.UnderlyingFixedLeg.GetSchedule().Periods;
        var idx = periods.IndexOf(settle);
        if (idx < 0)
        {
          throw new ToolkitException(
            "Settle date out of the coupon schedule range");
        }

        nextStart = periods[idx].AccrualEnd;
        SetSwaptionStart(swpn, nextStart);
      }

      var fixedPs = new SwapLegPricer(swpn.UnderlyingFixedLeg,
        asOf, settle, 1.0, DiscountCurve, null, null, null, null, null)
        .GetPaymentSchedule(null, settle);

      Cashflow unitCf = RateVolatilityUtil.GetPsCashflowAdapter(fixedPs,
        false, swpn.UnderlyingFixedLeg.Notional);
      // UniqueSequence automatically sorts the dates and removes duplicates.
      Dt maturity = swpn.UnderlyingFixedLeg.Maturity;
      Dt[] dates;
      if (onlyCouponDatesCallable)
      {
        // Pick those dates AFTER settle and BEFORE maturity and on the cash flows
        dates = unitCf.EnumerateDates(true).Where((dt) => dt < maturity
          && dt > settle && optionPeriods.IndexOf(dt) >= 0).ToArray();
      }
      else
      {
        var partDates = new UniqueSequence<Dt>
        {
          // Pick those dates AFTER settle and BEFORE maturity and on the cash flows
          unitCf.EnumerateDates(true).Where(dt => dt < maturity &&
            dt > settle && optionPeriods.IndexOf(dt) >= 0).ToList(),
          // ...Plus those single dates specified in the periods
          optionPeriods.Where(p => p.StartDate == p.EndDate &&
            p.StartDate > settle && p.StartDate < maturity)
            .Select(p => p.StartDate).ToList(),
        };

        dates = GetOptionStyle(swpn) == OptionStyle.American
          ? new UniqueSequence<Dt>
          {
            partDates.Concat(optionPeriods.Select(
              p => p.EndDate).Where(dt => dt < maturity && dt > settle)).ToList()
          }.ToArray()
          : partDates.ToArray();
      }

      swpn.UnderlyingFixedLeg = savedFixedLeg;
      return new Tuple<Dt[], Dt>(dates, nextStart);
    }

    private static void SetSwaptionStart(Swaption swpn, Dt effective)
    {
      if (effective.IsEmpty())
        return;

      swpn.UnderlyingFloatLeg = ChangeSwaplegEffective(
        swpn.UnderlyingFloatLeg, effective);
      swpn.UnderlyingFixedLeg = ChangeSwaplegEffective(
        swpn.UnderlyingFixedLeg, effective);
    }

    #endregion Methods

    #region Properties
    /// <summary>
    /// Gets the swap.
    /// </summary>
    /// <value>The swap.</value>
    public Swap Swap
    {
      get { return (Swap)Product; }
    }

    /// <summary>
    /// Gets or sets the reference curve for the floating leg.
    /// </summary>
    /// <value>The reference curve.</value>
    public DiscountCurve ReferenceCurve
    {
      get { return _referenceCurve; }
      set { _referenceCurve = value; }
    }

    /// <summary>
    /// Gets or sets the discount curve.
    /// </summary>
    /// <value>The reference curve.</value>
    public DiscountCurve DiscountCurve
    {
      get { return _discountCurve; }
      set { _discountCurve = value; }
    }

    /// <summary>
    ///   Historical rate fixings
    /// </summary>
    public RateResets RateResets
    {
      get
      {
        if (_rateResets == null)
          _rateResets = new RateResets();
        return _rateResets;
      }
      set { _rateResets = value; }
    }

    /// <summary>
    /// Gets all the effective call dates after the settle date.
    /// </summary>
    /// <value>The call dates.</value>
    public Dt[] CallDates
    {
      get
      {
        bool onlyCouponDatesCallable = false;
        var periods = Swap.ExerciseSchedule.ToArray();
        var swpn = _swaption;
        if (swpn == null)
        {
          GetSwaptionAndPeriods(ref periods, out swpn);
          onlyCouponDatesCallable = GetOptionStyle(swpn) == OptionStyle.Bermudan;
        }
        else
          swpn = (Swaption) swpn.CloneObjectGraph();

        return GetCallDates(swpn, onlyCouponDatesCallable, periods).Item1;
      }
    }

    /// <summary>
    /// Gets the volatility object.
    /// </summary>
    /// <value>The volatility object.</value>
    public IVolatilityObject VolatilityObject
    {
      get { return _volatilityObject; }
      set { _volatilityObject = value; }
    }

    /// <summary>
    /// BGM tree option for callable bonds
    /// </summary>
    public BgmTreeOptions BgmTreeOptions { get; set; }

    /// <summary>
    /// Gets the volatility distribution type.
    /// </summary>
    /// <value>The volatility distribution type.</value>
    public DistributionType DistributionType
    {
      get { return _volatilityObject.DistributionType; }
    }

    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    public bool NoConversionLogNormal
    {
      get { return (_flags & NoConversionToLogNormalFlag) != 0; }
      set { _flags = _flags.SetFlag(NoConversionToLogNormalFlag, value); }
    }
 
    public bool AmcNoForwardValueProcess
    {
      get { return (_flags & NoForwardValueProcessFlag) != 0; }
      set { _flags = _flags.SetFlag(NoForwardValueProcessFlag, value); }
    }
    #endregion Properties

    #region IAmericanMonteCarloAdapterProvider Members

    IAmericanMonteCarloAdapter IAmericanMonteCarloAdapterProvider.GetAdapter()
    {
      Swaption swaption;
      var pricer = GetSwapPricer(out swaption);
      var evaluator = new SwapExerciseEvaluator(pricer, CallDates,
        swaption.SettlementType == SettlementType.Cash,
        AmcNoForwardValueProcess ? null : BuildCoTerminalSwaptions());
      return new SwapBermudanAmcAdapter(AsOf,Notional, evaluator, null);
    }

    private SwapPricer GetSwapPricer(out Swaption swaption)
    {
      var onlyCouponDatesCallable = false;
      var periods = Swap.ExerciseSchedule.ToArray();
      bool isPayerFixed = Swap.IsPayerFixed;
      swaption = _swaption;
      if (swaption == null)
      {
        GetSwaptionAndPeriods(ref periods, out swaption);
        onlyCouponDatesCallable = GetOptionStyle(swaption) == OptionStyle.Bermudan;
      }
      else
        swaption = (Swaption) swaption.CloneObjectGraph();

      var dates = GetCallDates(swaption, onlyCouponDatesCallable, periods);
      SetSwaptionStart(swaption, dates.Item2);
      bool isRightToCancel = (swaption.OptionRight == OptionRight.RightToCancel);
      var receiverLeg = isPayerFixed ? swaption.UnderlyingFloatLeg : swaption.UnderlyingFixedLeg;
      var payerLeg = isPayerFixed ? swaption.UnderlyingFixedLeg : swaption.UnderlyingFloatLeg;
      var receiverPricer = new SwapLegPricer(receiverLeg, AsOf, Settle, 1.0, DiscountCurve,
        Swap.ReceiverLeg.ReferenceIndex, ReferenceCurve, RateResets, null, null);
      var payerPricer = new SwapLegPricer(payerLeg, AsOf, Settle, -1.0, DiscountCurve,
        Swap.PayerLeg.ReferenceIndex, ReferenceCurve, RateResets, null, null);
      if (isRightToCancel)
      {
        receiverPricer.Notional *= -1.0;
        payerPricer.Notional *= -1.0;
        return new SwapPricer(payerPricer, receiverPricer);
      }
      var pricer = new SwapPricer(receiverPricer, payerPricer);
      pricer.Swap.ExerciseSchedule = periods;
      return pricer;
    }

    #endregion

    #region ILockedRatesPricerProvider Members

    /// <summary>
    ///   Get a pricer in which all the rate fixings with the reset dates on
    ///   or before the anchor date are fixed at the current projected values.
    /// </summary>
    /// <param name="anchorDate">The anchor date.</param>
    /// <returns>The original pricer instance if no rate locked;
    ///   Otherwise, the cloned pricer with the rates locked.</returns>
    /// <remarks>This method never modifies the original pricer,
    ///  whose states and behaviors remain exactly the same before
    ///  and after calling this method.</remarks>
    IPricer ILockedRatesPricerProvider.LockRatesAt(Dt anchorDate)
    {
      var swapleg = Swap.ReceiverLeg;
      if (!swapleg.Floating) swapleg = Swap.PayerLeg;
      if (!swapleg.Floating) return this;

      // We lock the rates using a dumy pricer for the underlying floating leg
      // as the locked rates provider.
      var swaplegPricer = new SwapLegPricer(swapleg, Settle, Settle,
        1.0, DiscountCurve, swapleg.ReferenceIndex, ReferenceCurve,
        RateResets, null, null);
      var lockedRatesPricer = (SwapLegPricer)
        ((ILockedRatesPricerProvider)swaplegPricer).LockRatesAt(anchorDate);

      // If nothing locked, return this instance.
      if (ReferenceEquals(lockedRatesPricer, swaplegPricer)) return this;
      // Otherwise, returns a clone with the rates locked.
      return this.FastClone(new FastCloningContext
      {
        {swaplegPricer.RateResets, lockedRatesPricer.RateResets},
        {swaplegPricer.ReferenceIndex, lockedRatesPricer.ReferenceIndex}
      });
    }

    IPricer ILockedRatesPricerProvider.LockRateAt(Dt asOf, IPricer otherPricer)
    {
      return this;
    }

    #endregion
  }

  #region SwapBermudanAmcAdapter

  [Serializable]
  public class SwapBermudanAmcAdapter : IAmericanMonteCarloAdapter
  {
    private readonly SwapExerciseEvaluator _evaluator;
    private readonly BasisFunctions _basis;
    private readonly double _notional;
    private readonly Dt _asOf;

    public SwapBermudanAmcAdapter(
      Dt asOf, 
      double notional,
      SwapExerciseEvaluator evaluator,
      BasisFunctions basisFunctions)
    {
      _asOf = asOf;
      _notional = notional;
      _evaluator = evaluator;
      _basis = basisFunctions ?? evaluator.Basis;
    }

    #region IAmericanMonteCarloAdapter Members

    public Currency ValuationCurrency
    {
      get { return _evaluator.ValuationCurrency; }
    }

    public IEnumerable<DiscountCurve> DiscountCurves
    {
      get { yield return _evaluator.DiscountCurve; }
    }

    public IEnumerable<CalibratedCurve> ReferenceCurves
    {
      get
      {
        var dc = _evaluator.DiscountCurve;
        return _evaluator.ReferenceCurves.Where(c => c != null && c != dc);
      }
    }

    public IEnumerable<SurvivalCurve> SurvivalCurves
    {
      get { yield break; }
    }

    public IEnumerable<FxRate> FxRates
    {
      get { return _evaluator.FxCurves.Select(c => c.SpotFxRate); }
    }

    public IList<ICashflowNode> Cashflow
    {
      get { return null; }
    }

    public ExerciseEvaluator CallEvaluator
    {
      get { return null; }
    }

    public ExerciseEvaluator PutEvaluator
    {
      get { return _evaluator; }
    }

    public BasisFunctions Basis
    {
      get { return _basis; }
    }

    public double Notional
    {
      get { return _notional; }
    }

    public bool Exotic
    {
      get { return true; }
    }

    public Dt[] ExposureDates
    {
      get
      {
        if (_exposureDts == null)
          _exposureDts = InitExposureDates(null);
        return _exposureDts;
      }
      set { _exposureDts = InitExposureDates(value); }
    }

    private Dt[] InitExposureDates(Dt[]  inputDates)
    {
      Dt max = Dt.Roll(PutEvaluator.TerminationDate, BDConvention.Following, Calendar.None);
      var	dates = new UniqueSequence<Dt>();
      if (inputDates != null && inputDates.Any(dt => dt <= max))
      {
        dates.Add(inputDates.Where(dt => dt <= max).ToArray());
        var lastDt = dates.Max();
        if (lastDt < max && inputDates.Any(dt => dt > max))
          dates.Add(inputDates.First(dt => dt > max)); 
        max = Dt.Earlier(inputDates.First(), max);
      }
      foreach (var exDt in PutEvaluator.ExerciseDates)
      {
        if (exDt > max)
          break;
        var beforeDt = Dt.Add(exDt, -1);
        if (beforeDt > _asOf)
          dates.Add(beforeDt);
        dates.Add(exDt);
      }
      dates.Add(_asOf);

      if (!PutEvaluator.CashSettled)
      {
        var cashflows = _evaluator.UnderlyerCashflow;
        if (cashflows != null)
        {
          foreach (var cashflowNode in cashflows)
          {
            var payDt = cashflowNode.PayDt; 
            if (payDt > max)
              break;
            var beforeDt = Dt.Add(payDt, -1);
            if (beforeDt > _asOf)
              dates.Add(beforeDt);
            dates.Add(payDt);
          }
        }
      }

      return dates.ToArray();
    }



    private Dt[] _exposureDts; 

    #endregion
  }

  #endregion

  #region Exercise evaluator

  [Serializable]
  public class SwapExerciseEvaluator : ExerciseEvaluator, IAmcForwardValueProcessor
  {
    #region Data and Properties

    private readonly SwapPricer _pricer;
    private readonly SwapLegCcrPricer _payer, _receiver;
    private readonly SwaptionInfo[] _swpns;
    private readonly SwapBasisFunctions _basis;
    private Dt _cacheDate;
    private double _payerValue, _receiverValue;

    public SwapLegCcrPricer Payer => _payer;

    public SwapLegCcrPricer Receiver => _receiver;

    private SwapPricer SwapPricer => _pricer;

    private IList<IOptionPeriod> ExerciseSchedule => SwapPricer.Swap.ExerciseSchedule;

    public bool IsReceiverFloating => SwapPricer.ReceiverSwapPricer.SwapLeg.Floating;

    public DiscountCurve DiscountCurve => SwapPricer.DiscountCurve;

    public CalibratedCurve[] ReferenceCurves => SwapPricer.ReferenceCurves;

    public FxCurve[] FxCurves => SwapPricer.FxCurves;

    public Currency ValuationCurrency => SwapPricer.ValuationCurrency;

    public double Notional => SwapPricer.Notional;

    public BasisFunctions Basis => _basis;

    public IList<ICashflowNode> UnderlyerCashflow
      => ((ICashflowNodesGenerator)SwapPricer).Cashflow;

    #endregion

    #region Methods

    public SwapExerciseEvaluator(SwapPricer pricer,
      Dt[] exerciseDates, bool cashSettled,
      SwaptionInfo[] swpns)
      : base(exerciseDates, cashSettled, pricer.Swap.Maturity, true)
    {
      _pricer = pricer;
      _payer = new SwapLegCcrPricer(pricer.PayerSwapPricer);
      _receiver = new SwapLegCcrPricer(pricer.ReceiverSwapPricer);
      _swpns = swpns;
      _basis = new SwapBasisFunctions(this);
    }

    public override double Value(Dt date)
    {
      Evaluate(date);
      return _payerValue + _receiverValue;
    }

    public override void Reset()
    {
      _cacheDate = Dt.Empty;
      _payerValue = _receiverValue = 0.0;
    }

    public override double Price(Dt date)
    {
      double price;
      return ExerciseSchedule.TryGetExercisePriceByDate(date, out price)
        ? (price - 1.0) : 0.0;
    }

    private void Evaluate(Dt date)
    {
      if (_pricer == null || _cacheDate == date)
        return;

      _cacheDate = date;
      var fwdDate = ExerciseDates.FirstOrDefault(d => d == date);
      if (!fwdDate.IsEmpty())
      {
        // On the exercise date, calculate the clean pv.
        _payerValue = _payer.FastPv(date, true);
        _receiverValue = _receiver.FastPv(date, true);
        return;
      }
      // Otherwise, the full pv
      _payerValue = _payer.FastPv(date);
      _receiverValue = _receiver.FastPv(date);
    }

    private double State(Dt date)
    {
      Evaluate(date);
      return IsReceiverFloating
        ? Ratio(_receiverValue, _payerValue)
        : Ratio(_payerValue, _receiverValue);
    }

    private static double Ratio(double enume, double denom)
    {
      if (denom < 0) denom = -denom;
      else enume = -enume;
      return enume / Math.Max(denom, 1E-6);
    }

    void IAmcForwardValueProcessor.ProcessForwardValues(
      IList<Dt> dates,
      Func<int, IList<double>> getValuesAtDate,
      Func<int, IList<double>> getDiscountFactorsAtDate)
    {
      var swpns = _swpns;
      if (swpns == null || swpns.Length == 0) return;

      // Now we scale the swap values to match
      // the sequence of co-terminal swaptions.
      var callDates = (Dt[])ExerciseDates;
      int endEndIndex = dates.Count;
      // swpns = swpns.Take(1).ToArray();
      for (int i = swpns.Length; --i >= 0;)
      {
        endEndIndex = ProcessForwardValues(swpns[i], callDates[i],
          endEndIndex, dates, getValuesAtDate, getDiscountFactorsAtDate);
        if (endEndIndex <= 0) break;
      }
      return;
    }

    private static int ProcessForwardValues(
      SwaptionInfo swpn, Dt callDate,
      int endDateIndex,
      IList<Dt> dates,
      Func<int, IList<double>> getValuesAtDate,
      Func<int, IList<double>> getDiscountFactorsAtDate)
    {
      double europeanValue = swpn.Value;
      if (!(europeanValue > 0)) return -1;

      int callDateIndex = -1;
      for (int i = endDateIndex; i > 0; --i)
      {
        int cmp = Dt.Cmp(dates[i-1], callDate);
        if (cmp >= 0) continue;
        callDateIndex = i;
        break;
      }
      if (callDateIndex < 0 || callDateIndex >= endDateIndex)
        return -1;

      var values = getValuesAtDate(callDateIndex);
      int count = values == null ? 0 : values.Count;
      if (count <= 0) return callDateIndex;

      var df = getDiscountFactorsAtDate(callDateIndex);
      double sum = 0;
      for (int i = 0; i < count; ++i)
        sum += Math.Max(values[i], 0.0)*df[i];
      if (sum <= 0) return callDateIndex;

      double a = europeanValue/(sum/count);

      for (int t = callDateIndex; t < endDateIndex; ++t)
      {
        values = getValuesAtDate(t);
        if (values == null || values.Count == 0) continue;
        for (int i = 0, n = values.Count; i < n; ++i)
          values[i] *= a;
      }

      return callDateIndex;
    }

    #endregion

    #region BasisFunctions

    [Serializable]
    private class SwapBasisFunctions : BasisFunctions
    {
      #region Data

      private const int N = 1;
      private readonly SwapExerciseEvaluator _put;

      #endregion

      #region Constructors

      public SwapBasisFunctions(SwapExerciseEvaluator put)
      {
        _put = put;
        Dimension = 1 + N + N * (N + 1) / 2 + N * (N * (N + 3) + 2) / 6;
      }

      #endregion

      #region Methods

      public override void Generate(Dt date, double[] retVal)
      {
        retVal[0] = 1.0;
        retVal[1] = _put.State(date);
        var index = 2;
        for (int i = 1; i <= N; ++i)
          for (int j = 1; j <= i; ++j, ++index)
            retVal[index] = retVal[i] * retVal[j];
        for (int i = 1; i <= N; ++i)
          for (int j = 1; j <= i; ++j)
            for (int k = 1; k <= j; ++k, ++index)
              retVal[index] = retVal[i] * retVal[j] * retVal[k];
      }

      #endregion
    }

    #endregion
  }

  #endregion
}