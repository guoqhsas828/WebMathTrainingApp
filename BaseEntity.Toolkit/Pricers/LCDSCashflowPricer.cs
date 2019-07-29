/*
 * LCDSCashflowPricer.cs
 *
 *  -2008. All rights reserved.     
 *
 * $Id $
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.LCDS">LCDS</see> using the
  /// <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see>.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.LCDX" />
  /// 
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="CashflowPricer" />
  ///
  /// <para><b>Adjustment for prepayment risk</b></para>
  /// <para>The lattice method described above may be adjusted to account for prepayment of the
  /// asset in a similar way to that used for counterparty risk. In this case
  /// the three possible states would be "no default", "reference name defaults
  /// prior to protection seller", and "asset prepaid prior to reference name defaulting".</para>
  /// <para>The analysis is per above, with the calculation of probabilities being done
  /// using a survival curve and a probability of prepayment, plus the correlation
  /// between the two. These are combined using a Gaussian factor copula which allows for
  /// direct computation of the required probabilities via quadrature routines.</para>
  ///
  /// <para><b>Recovery Rates</b></para>
  /// <para>Recovery rate are specified in one of three ways,</para>
  /// <list type="bullet">
  ///   <item>If the LCDS is a fixed recovery deal, the fixed recovery will be used</item>
  ///   <item>If a recovery rate is specified, that recovery rate will be used</item>
  ///   <item>Otherwise the recovery rate used in the calibration of the survival curve will be used</item>
  /// </list>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.LCDS">LCDS Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel">Cashflow pricing model</seealso>
  [Serializable]
  public class LCDSCashflowPricer : CDSCashflowPricer
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(LCDSCashflowPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    ///
    public
    LCDSCashflowPricer(LCDS product)
      : base(product)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rate, refinance curve and correlation are taken from the survival curve</para>
    /// </remarks>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    LCDSCashflowPricer(
      LCDS product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      int stepSize,
      TimeUnit stepUnit)
      : this(product, asOf, settle, discountCurve, null, survivalCurve, stepSize, stepUnit)
    {}


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rate, refinance curve and correlation are taken from the survival curve</para>
    /// </remarks>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve used for floating payments forecasts</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    LCDSCashflowPricer(
      LCDS product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      int stepSize,
      TimeUnit stepUnit)
      : base(product, asOf, settle, discountCurve, referenceCurve, survivalCurve, stepSize, stepUnit)
    {
      if (survivalCurve.Calibrator != null && survivalCurve.Calibrator is SurvivalCalibrator)
      {
        SurvivalCalibrator calibrator = survivalCurve.SurvivalCalibrator;
        this.CounterpartyCurve = calibrator.CounterpartyCurve;
        this.Correlation = calibrator.CounterpartyCorrelation;
      }
    }

    /// <summary>
    ///   Constructor with explicit refinance risks
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve of underlying credit</param>
    /// <param name="prepayCurve">Curve representing the probability of prepayment (refinancing).</param>
    /// <param name="correlation">Correlation between default and prepayment</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    LCDSCashflowPricer(
      CDS product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      SurvivalCurve prepayCurve,
      double correlation,
      int stepSize,
      TimeUnit stepUnit)
      : this(product, asOf, settle, discountCurve, null, survivalCurve,
        prepayCurve, correlation, stepSize, stepUnit)
    {
    }


    /// <summary>
    ///   Constructor with explicit refinance risks
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for forecasting of floating payments</param>
    /// <param name="survivalCurve">Survival Curve of underlying credit</param>
    /// <param name="prepayCurve">Curve representing the probability of prepayment (refinancing).</param>
    /// <param name="correlation">Correlation between default and prepayment</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    LCDSCashflowPricer(
      CDS product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve,
      SurvivalCurve prepayCurve,
      double correlation,
      int stepSize,
      TimeUnit stepUnit)
      : base(product, asOf, settle, discountCurve, referenceCurve, survivalCurve,
        prepayCurve, correlation, stepSize, stepUnit)
    {
    }

    #endregion Constructors

    #region Methods
    /// <summary>
    ///   The probability of observing a default event between
    ///   the settle date and a given date.
    /// </summary>
    /// 
    /// <remarks>
    ///   Let <formula inline="true">\tau_D</formula> be the (random)
    ///   default time, <formula inline="true">\tau_P</formula> be the
    ///   (random) prepayment time.
    ///   The probability of observing a default event between time 0 and
    ///   <formula inline="true">t</formula>, <formula inline="true">P(t)</formula>,
    ///   is defined as the probability that the reference entity defaults
    ///   before time <formula inline="true">t</formula> and no prepayment
    ///   happens before the default time:
    ///   <formula>
    ///     P(t) \equiv \mathrm{Prob}\{\tau_D \lt t, \tau_D \lt \tau_P \}
    ///   </formula>
    ///   Please do not confuse this with the marginal probability of default,
    ///   which is defined as
    ///   <formula inline="true">\mathrm{prob}(\tau_D \lt t)</formula>.
    /// </remarks>
    /// 
    /// <param name="date">
    ///   The date to calculate the default probability.
    /// </param>
    /// <returns>
    ///   The probability to observe a default before the given date.
    /// </returns>
    public double DefaultProbability(Dt date)
    {
      return CounterpartyRisk.CreditDefaultProbability(Settle, date,
        SurvivalCurve, PrepaymentCurve, PrepaymentCorrelation,
        StepSize, StepUnit);
    }

    /// <summary>
    ///   The probability of observing a prepayment event between
    ///   the settle date and a given date.
    /// </summary>
    /// 
    /// <remarks>
    ///   Let <formula inline="true">\tau_D</formula> be the (random)
    ///   default time, <formula inline="true">\tau_P</formula> be the
    ///   (random) prepayment time.
    ///   The probability of observing a prepayment event between time 0 and
    ///   <formula inline="true">t</formula>, <formula inline="true">Q(t)</formula>,
    ///   is defined as the probability that the reference entity prepays
    ///   before time <formula inline="true">t</formula> and no default
    ///   happens before the prepayment time:
    ///   <formula>
    ///     Q(t) \equiv \mathrm{Prob}\{\tau_P \lt t, \tau_P \lt \tau_D \}
    ///   </formula>
    ///   Please do not confuse this with the marginal probability of prepayment,
    ///   which is defined as
    ///   <formula inline="true">\mathrm{prob}(\tau_P \lt t)</formula>.
    /// </remarks>
    /// 
    /// <param name="date">
    ///   The date to calculate the default probability.
    /// </param>
    /// <returns>
    ///   The probability to observe a default before the given date.
    /// </returns>
    public double PrepaymentProbability(Dt date)
    {
      return CounterpartyRisk.CounterpartyDefaultProbability(Settle, date,
        SurvivalCurve, PrepaymentCurve, PrepaymentCorrelation,
        StepSize, StepUnit);
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Prepayment curve
    /// </summary>
    public SurvivalCurve PrepaymentCurve
    {
      get { return CounterpartyCurve; }
      set { CounterpartyCurve = value; }
    }

    /// <summary>
    ///   Prepayment correlation
    /// </summary>
    public double PrepaymentCorrelation
    {
      get { return Correlation; }
      set { Correlation = value; }
    }

#if Include_Obsolete
    /// <summary>
    ///   Refinance curve
    /// </summary>
    public SurvivalCurve RefinanceCurve
    {
      get { return CounterpartyCurve; }
      set { CounterpartyCurve = value; }
    }

    /// <summary>
    ///   Refinance correlation
    /// </summary>
    public double RefinanceCorrelation
    {
      get { return Correlation; }
      set { Correlation = value; }
    }
#endif // Include_Obsolete

    /// <summary>
    ///   Product to price
    /// </summary>
    public CDS LCDS
    {
      get { return (LCDS)Product; }
    }

    /// <summary>
    ///   SurvivalCurves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return new SurvivalCurve[] { this.SurvivalCurve }; }
    }
    #endregion Properties

  } // class CDSCashflowPricer
}
