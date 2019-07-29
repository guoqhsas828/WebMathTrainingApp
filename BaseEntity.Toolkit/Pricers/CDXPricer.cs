//
// CDXPricer.cs
//    2008-2014. All rights reserved.
//
// TBD: Retire BasisFactor. RTD Nov'05
// TBD: HJ to review scaling/basis functions. RTD Aug'07
//
//#define NEW_DURATION_SOLVER

using System;
using System.Collections;
using System.Collections.Generic;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Pricers
{
  #region Config
  /// <exclude />
  [Serializable]
  public class CDXPricerConfig
  { 
    /// <exclude />
    [Util.Configuration.ToolkitConfig("AdjustDurationForRemainingNotional when basket has alraedy-occured defaults")]
    public readonly bool AdjustDurationForRemainingNotional = true;

    /// <exclude />
    [Util.Configuration.ToolkitConfig("Always pricing CDX options based on consistent market payoffs")]
    public readonly bool MarketPayoffsForIndexOptions = true;

    /// <exclude />
    [Util.Configuration.ToolkitConfig("After a name is defaulted, convert a quote to market value based on the old factor until the recovery settlement date")]
    public readonly bool MarkAtPreviousFactorUntilRecovery = true;

  }
  #endregion Config

  /// <summary>
  ///   Scaling method to apply to each tenor of a credit curve.
  /// </summary>
  public enum CDXScalingMethod
  {
    /// <summary>
    ///   Do not apply any scaling for this credit curve tenor.
    /// </summary>
    None,
    /// <summary>
    ///   Use the specified override scaling factor for this tenor.
    /// </summary>
    Override,
    /// <summary>
    ///   <para>Scale this credit curve tenor together with the next
    ///   tenor.</para>
    ///
    ///   <para>This is used where several tenors are scaled together
    ///   based on a particular index quote. For example you may
    ///   want to scale 3 Year, 4 Year and 5 Year CDS quotes based on
    ///   the 5 Year CDX index level.</para>
    ///
    ///   <para>This may be used in sequence along with the Previous
    ///   scaling method to group tenors to be scaled together around
    ///   a particular maturity.</para>
    /// </summary>
    Next,
    /// <summary>
    ///   <para>Scale this credit curve tenor together with the previous
    ///   tenor.</para>
    ///
    ///   <para>This is used where several tenors are scaled together
    ///   based on a particular index quote. For example you may
    ///   want to scale 5 Year, 6 Year and 7 Year CDS quotes based on
    ///   the 5 Year CDX index level.</para>
    ///
    ///   <para>This may be used in sequence along with the Next
    ///   scaling method to group tenors to be scaled together around
    ///   a particular maturity.</para>
    /// </summary>
    Previous,
    /// <summary>
    ///   <para>Scale this credit curve tenor based on the ratio of the
    ///   average spread of the underlying CDS and the quoted spread
    ///   of the CDX.</para>
    /// </summary>
    Spread,
    /// <summary>
    ///   <para>Scale this credit curve tenor based on the ratio of the
    ///   duration weighted average spread of the underlying CDS and
    ///   the quoted spread of the CDX.</para>
    /// </summary>
    Duration,
    /// <summary>
    ///   <para>Scale this credit curve tenor by solving for the scaling
    ///   factor where the fair value of the underlying CDS matches the
    ///   market value of the CDX.</para>
    /// </summary>
    Model,
    /// <summary>
    ///   <para>Scale the off-the-run CDX by matching the expected loss</para>
    /// </summary>
    ProtectionMatch,
    /// <summary>
    ///   <para>Scale the off-the-run CDX by matching the market value to intrinsic value</para>
    /// </summary>
    Price
  }


  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.CDX">CDX or iTraxx Index funded or unfunded note</see>
  /// using the standard market pricing conventions.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDX" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDX">Credit Indices</seealso>
  [Serializable]
  public partial class CDXPricer : PricerBase, ICDXPricer, IAnalyticDerivativesProvider
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDXPricer));

    #region Config

    // 8.6
    private static readonly bool discountToSettle_ = false;

    /// <summary>
    ///   Include maturity date in accrual calculation for CDS/CDO pricers
    /// </summary>
    /// <exclude />
    public static bool DiscountToSettle
    {
      get { return discountToSettle_; }
    }

    /// <summary>
    /// Adjust risky duration for remaining notional when basket has defaulted names
    /// </summary>
    /// <exclude />
    public bool AdjustDurationForRemainingNotional
    {
      get { return adjustDurationForRemainingNotional_; }
      set { adjustDurationForRemainingNotional_ = value;}
    }

    /// <summary>
    /// After a name is defaulted in the credit index, convert a quote to market value based on the old factor until the recovery settlement date
    /// </summary>
    public bool MarkAtPreviousFactorUntilRecovery
    {
      get { return _markAtPreviousFactorUntilRecovery; }
      set { _markAtPreviousFactorUntilRecovery = value; }
    }

    #endregion Config

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For this constructor, the recovery rates are taken from
    ///   the survival curves.</para>
    /// </remarks>
    ///
    /// <param name="product">CDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    /// <param name="marketQuote">Current market quote as a number(if price, 1=par; if premium, 100bp = 0.01)</param>
    ///
    public
    CDXPricer(
             CDX product,
             Dt asOf,
             Dt settle,
             DiscountCurve discountCurve,
             SurvivalCurve[] survivalCurves,
             double marketQuote
             )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0 ?
                        null : survivalCurves);
      MarketQuote = marketQuote;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For this constructor, the recovery rates are taken from
    ///   the survival curves.</para>
    /// </remarks>
    ///
    /// <param name="product">CDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///<param name="referenceCurve">Reference Curve for floating payments forecasts</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    /// <param name="marketQuote">Current market quote as a number(if price, 1=par; if premium, 100bp = 0.01)</param>
    ///
    public
    CDXPricer(
             CDX product,
             Dt asOf,
             Dt settle,
             DiscountCurve discountCurve,
             DiscountCurve referenceCurve,
             SurvivalCurve[] survivalCurves,
             double marketQuote
             )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0 ?
                        null : survivalCurves);
      ReferenceCurve = referenceCurve;
      MarketQuote = marketQuote;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For this constructor, the recovery rates are taken from
    ///   the survival curves.</para>
    /// </remarks>
    ///
    /// <param name="product">CDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    ///
    public
    CDXPricer(
             CDX product,
             Dt asOf,
             Dt settle,
             DiscountCurve discountCurve,
             SurvivalCurve[] survivalCurves
             )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = survivalCurves;
      cashflow_ = null;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For this constructor, the recovery rates are taken from
    ///   the survival curves.</para>
    /// </remarks>
    ///
    /// <param name="product">CDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///<param name="referenceCurve">Reference curve for floating payments forecasts</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    ///
    public
    CDXPricer(
             CDX product,
             Dt asOf,
             Dt settle,
             DiscountCurve discountCurve,
             DiscountCurve referenceCurve,
             SurvivalCurve[] survivalCurves
             )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      SurvivalCurves = survivalCurves;
      cashflow_ = null;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is a constructor for pricing CDX based on market quote
    ///   only.  No link is made to undelying portfolio.</para>
    /// </remarks>
    ///
    /// <param name="product">CDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="marketQuote">Current quoted market premium as a number (100bp = 0.01)</param>
    ///
    public
    CDXPricer(
             CDX product,
             Dt asOf,
             Dt settle,
             DiscountCurve discountCurve,
             double marketQuote
             )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = null;
      MarketQuote = marketQuote;
      cashflow_ = null;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is a constructor for pricing CDX based on market quote
    ///   only.  No link is made to undelying portfolio.</para>
    /// </remarks>
    ///
    /// <param name="product">CDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payment forecasts</param>
    /// <param name="marketQuote">Current quoted market premium as a number (100bp = 0.01)</param>
    ///
    public
    CDXPricer(
             CDX product,
             Dt asOf,
             Dt settle,
             DiscountCurve discountCurve,
             DiscountCurve referenceCurve,
             double marketQuote
             )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      SurvivalCurves = null;
      MarketQuote = marketQuote;
      cashflow_ = null;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CDXPricer obj = (CDXPricer)base.Clone();

      obj.survivalCurves_ = CloneUtil.Clone<SurvivalCurve>(survivalCurves_);
      obj.discountCurve_ = CloneUtil.Clone(discountCurve_);
      obj.referenceCurve_ = CloneUtil.Clone(referenceCurve_);
      obj.cashflow_ = null;
      obj.equivalentCDSPricer_ = null;
      obj.marketSurvivalCurve_ = CloneUtil.Clone(marketSurvivalCurve_);
      obj.rateResets_ = CloneUtil.Clone(rateResets_);

      return obj;
    }

    #endregion Constructors

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
            paymentPricer_ = BuildPaymentPricer(Payment, discountCurve_);
        }
        return paymentPricer_;
      }
    }

    #region MarketMethods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ps"></param>
    /// <param name="from"></param>
    /// <returns></returns>
    public PaymentSchedule GeneratePayments(PaymentSchedule ps, Dt from)
    {
      DiscountCurve referenceCurve =
        (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;

      CDX p = (CDX)Product;
      return CDXPricerUtil.GetPaymentSchedule(ps, from, Settle, p.Effective,
        p.FirstPrem, p.Maturity, p.AnnexDate, p.Ccy, p.Premium,
        p.DayCount, p.Freq, CycleRule.None, p.BDConvention,
        p.Calendar, p.CdxType != CdxType.Unfunded,
        MarketRecoveryRate, DiscountCurve,
        referenceCurve, p.CdxType == CdxType.FundedFloating,
        RateResets, SurvivalCurves, p.Weights,
        RecoveryCurves);
    }


    /// <summary>
    ///   Calculate the value of the CDX Note.
    /// </summary>
    /// <remarks>
    ///   <para>The market value is the settlement value in dollars (or other currency) of the index.</para>
    ///   <para>The calculation of the market value is based on market
    ///   convention. This is the present value (full price) of a single name CDS whose premium is the
    ///   deal spread, priced using a flat credit curve at the market quote spread
    ///   and a 40 percent recovery.</para>
    ///   <para>The Market Value includes the accrued of the CDX</para>
    /// </remarks>
    /// <returns>Value of the CDX Note at current market quoted spread</returns>
    public double MarketValue()
    {
      return MarketValue(MarketQuote);
    }

    /// <summary>
    ///   Calculate the value of the CDX Note at the specified
    ///   quoted level (as a value in currency terms)
    /// </summary>
    /// <remarks>
    ///   <para>The market value is the settlement value in dollars (or other currency) of the index.</para>
    ///   <para>The calculation of the market value is based on market
    ///   convention. If the market quote is quoted as a price the dollar value is easily calculated.
    ///   For spread quotes, first, construct a single name CDS with the same characteristics as
    ///   the index.  In particular the CDS premium is the index deal spread.
    ///   The convention is to then price this product using a flat credit curve at the market quote spread
    ///   and a standard (default 40<m>\%</m>) recovery.</para>
    ///   <para>The Market Value includes the accrued of the CDX</para>
    /// </remarks>
    /// <param name="marketQuote">Current quoted market premium as a number (100bp = 0.01).</param>
    /// <returns>Value of the CDX Note at current market quoted spread</returns>
    public double MarketValue(double marketQuote)
    {
      return Settle > CDX.Maturity ? 0.0 : CDXPricerUtil.MarketValue(
        this, marketQuote, this.QuotingConvention,
        () => GetEquivalentCDSPricer(marketQuote));
    }

    /// <summary>
    ///   Calculate the clean price (as a percentage of notional) of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The market price is the settlement price as a percentage
    ///   of the remaining notional of the index.</para>
    ///   <para>The calculation of the market price is based on market
    ///   convention. First, construct a single name CDS with the same characteristics as
    ///   the index.  In particular the premium is the index deal spread.
    ///   The convention is to then price this product using a flat credit curve at the market quote spread
    ///   and a 40<formula inline="true">\%</formula> recovery.</para>
    /// </remarks>
    /// <returns>Price of the CDX Note at the quoted spread</returns>
    public double MarketPrice()
    {
      if (QuotingConvention == QuotingConvention.FlatPrice)
        return MarketQuote;
      else
        return MarketPrice(MarketQuote);
    }

    /// <summary>
    ///   Calculate the clean price (as a percentage of notional) of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The market price is the settlement price as a percentage
    ///   of the remaining notional of the index.</para>
    ///   <para>The calculation of the market price is based on market
    ///   convention. First, construct a single name CDS with the same characteristics as
    ///   the index.  In particular the premium is the index deal spread.
    ///   The convention is to then price this product using a flat credit curve at the market quote spread
    ///   and a 40<formula inline="true">\%</formula> recovery.</para>
    /// </remarks>
    /// <param name="marketSpread">Current quoted market premium as a number (100bp = 0.01).</param>
    /// <returns>Price of the CDX Note at the quoted spread</returns>
    public double MarketPrice(double marketSpread)
    {
      if (marketSpread < 0.0)
        throw new ArgumentOutOfRangeException("marketSpread", "marketSpread must be positive.");
      if (marketSpread == 0.0) return 0;

      double effectiveNotional = EffectiveNotional;
      // If effective notional is zero, price is zero
      if (Math.Abs(effectiveNotional) < 1E-15)
        return 0.0;

      // Note: 
      //   (1) CashflowPricer.FlatPrice() is simple clean Pv per unit notional.
      //       Since GetEquivalentCDSPricer returns a pricer with its notional
      //       set to the CurrentNotional, we are OK.
      //   (2) In addition, the price should include unsettled default loss,
      //       as MarkIt does.
      double price = GetEquivalentCDSPricer(marketSpread).FlatPrice();

      // TODO: This is a quick hack. Need to restore the commented logic.  see FB case 12804.
      //if (cf.DefaultPayment != null)
      //  price += (cf.DefaultPayment.Amount + cf.DefaultPayment.Loss)
      //    * Notional / effectiveNotional;

      return CDX.CdxType == CdxType.FundedFloating
        || CDX.CdxType == CdxType.FundedFixed ? price : (price + 1);
    }

    /// <summary>
    ///   Calculate the accrued premium for a CDX Note
    /// </summary>
    /// <returns>Accrued premium of CDX Note</returns>
    public override double Accrued()
    {
      var sc = new SurvivalCurve(AsOf, 0.0);
      return Settle > CDX.Maturity ? 0.0 : CDXPricerUtil.Accrued(
        this, () => GetEquivalentCDSPricer(CDX.Premium, sc));
    }

    /// <summary>
    ///   Calculate the number of accrual days for a Credit Default Swap
    /// </summary>
    /// <returns>The number of days accrued for a Credit Default Swap</returns>
    public int AccrualDays()
    {
      return Settle > CDX.Maturity ? 0 : EquivalentCDSPricer.AccrualDays();
    }

    /// <summary>
    ///   Calculate break-even premium for a CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">\mathrm{BreakEvenPremium = \frac{\displaystyle ProtectionLegPv}{ \displaystyle {Duration \times }Notional }}</formula></para>
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDX where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    /// <returns>Break-even premium for CDX Note in percent</returns>
    /// [Obsolete("Not seen to be useful for indices. RTD Sep07")]
    public double BreakEvenPremium()
    {
      return Settle > CDX.Maturity ? 0.0 : EquivalentCDSPricer.BreakEvenPremium();
    }

    /// <summary>
    ///   Calculate the forward break-even premium for a CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The forward break-even premium is the premium which would imply a
    ///   zero MTM value on the specified forward settlement date.</para>
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">\mathrm{BreakEvenPremium = \displaystyle{\frac{ProtectionLegPv}{ Duration \times Notional }}}</formula></para>
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDX where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    /// <param name="forwardSettle">Forward Settlement date</param>
    /// <returns>The forward break-even premium for the CDX Note in percent</returns>
    // [Obsolete("Not seen to be useful for indices. RTD Sep07")]
    public double FwdPremium(Dt forwardSettle)
    {
      return EquivalentCDSPricer.FwdPremium(forwardSettle);
    }

    /// <summary>
    ///   Calculate the Premium 01 of CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The Premium01 is the change in PV (MTM) for a CDX Note
    ///   if the premium is increased by one basis point.</para>
    ///   <para>The Premium01 is calculated by calculating the PV (MTM) of the
    ///   CDX Note then bumping up the premium by one basis point
    ///   and re-calculating the PV and returning the difference in value.</para>
    ///   <para>The Premium01 includes accrued.</para>
    /// </remarks>
    /// <returns>Premium01 of the CDX Note</returns>
    /// [Obsolete("Not seen to be useful for indices. RTD Sep07")]
    public double Premium01()
    {
      return EquivalentCDSPricer.Premium01();
    }

    /// <summary>
    ///   Calculate the Forward Premium 01 of CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The Forward Premium 01 is the change in PV (MTM) at a
    ///   future settlement date for a CDX NOte if
    ///   the premium is increased by one basis point.</para>
    ///   <para>The Forward Premium 01 is calculated by calculating
    ///   the PV (MTM) of the CDX Note at a specified
    ///   future settlement date and then bumping up the premium
    ///   by one basis point and re-calculating the MTM at the
    ///   same forward settlement date and returning the difference
    ///   in value.</para>
    ///   <para>The Foward Premium 01 includes accrued.</para>
    /// </remarks>
    /// <param name="forwardSettle">Forward settlement date</param>
    /// <returns>Forward Premium 01 of the CDX Note</returns>
    [Obsolete("Not seen to be useful for indices. RTD Sep07")]
    public double FwdPremium01(Dt forwardSettle)
    {
      return EquivalentCDSPricer.FwdPremium(forwardSettle);
    }

    /// <summary>
    ///   Calculate the risky duration of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The risky duration is the fee pv of a CDX with a premium of 1(10000bps) and
    ///    a notional of 1.0.</para>
    ///   <para>The risky duration is based on the remaining premium that is uncertain and
    ///   as such does not include any accrued.</para>
    ///   <para>The Duration is related to the Protection Leg PV, the Break Even Premium and the
    ///   Notional by <formula inline="true">\mathrm{Duration =\displaystyle{\frac{ProtectionLegPv}{ BreakEvenPremium \times Notional }}}</formula></para>
    /// </remarks>
    /// <returns>Risky duration of the CDX Note at current market quoted spread</returns>
    public double RiskyDuration()
    {
      return Settle > CDX.Maturity
               ? 0.0
               :
                 AdjustDurationForRemainingNotional
                   ? EquivalentCDSPricer.RiskyDuration()*CurrentNotional/Notional
                   : EquivalentCDSPricer.RiskyDuration();
    }

    /// <summary>
    ///   Calculate the carry of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The carry is daily income from premium and is simply
    ///   the premium divided by 360 times the notional, <formula inline="true">\mathrm{Carry=\displaystyle{\frac{Premium}{360}}\times Notional}</formula></para>
    /// </remarks>
    /// <returns>Carry of the CDX Note</returns>
    public double Carry()
    {
      return Settle > CDX.Maturity ? 0.0 : CDX.Premium / 360 * CurrentNotional;
    }

    /// <summary>
    ///   Calculate the MTM Carry of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The MTM carry is the daily income of the MTM level
    ///   of the credit default swap. It is the Break Even Premium
    ///   divided by 360 times the notional, <formula inline="true">\mathrm{Carry = \displaystyle{\frac{BreakEvenPremium}{360}}\times CurrentNotional}</formula></para>
    /// </remarks>
    /// <returns>MTM Carry of CDX Note</returns>
    public double MTMCarry()
    {
      return Settle > CDX.Maturity ? 0.0 : EquivalentCDSPricer.BreakEvenPremium() / 360 * CurrentNotional;
    }

    /// <summary>
    ///   Calculate the impact of a 1bp increase in the market spread of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>calculates the change in total value for a one basis point increase in
    ///   the index quoted market spread</para>
    /// </remarks>
    /// <returns>Spread 01 for CDX Note</returns>
    public double MarketSpread01()
    {
      return MarketSpread01(MarketQuote);
    }

    /// <summary>
    ///   Calculate the impact of a 1bp increase in the market spread of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>calculates the change in total value for a one basis point increase in
    ///   the index quoted market spread</para>
    /// </remarks>
    /// <param name="marketQuote">Current quoted market premium/price as a number (100bp = 0.01; 95 = 0.95).</param>
    /// <returns>Spread 01 for CDX Note</returns>
    public double MarketSpread01(double marketQuote)
    {
      if (QuotingConvention == QuotingConvention.FlatPrice)
      {
        double cleanPrice = marketQuote;
        double marketPremium = PriceToSpread(cleanPrice);
        double newMarketQuote = SpreadToPrice(marketPremium + 0.0001);
        return (MarketValue(newMarketQuote) - MarketValue(marketQuote));
      }

      return (MarketValue(marketQuote + 0.0001) - MarketValue(marketQuote));
    }

    /// <summary>
    ///   Calculate the implied market quoted spread given a market value for the CDS Index
    /// </summary>
    /// <remarks>
    ///   <para>Solves for the market quoted spread equivalent to the input price quote (unit based).</para>para>
    /// </remarks>
    /// <param name="cleanPrice">CDX price quote (1 = par)</param>
    /// <returns>Equivalent market quoted spread for a CDX</returns>
    public double PriceToSpread(double cleanPrice)
    {
      if (cleanPrice == 0) return 0;
      logger.Debug("Calculating implied quoted spread for CDS Index...");

      // Set up root finder
      //
      Brent rf = new Brent();
      rf.setToleranceX(1e-6);
      rf.setToleranceF(1e-10);
      rf.setLowerBounds(1E-10);

      SolverFn fn = new MarketPriceEvaluator(this);

      // Solve
      double res;
      if (CDX.Funded)
        if (CDX.CdxType == CdxType.FundedFloating)
        {
          res = rf.solve(fn, cleanPrice, CDX.Premium / 2.0, CDX.Premium * 2.0);
        }
        else
        {
          // Needs to be revisted
          double xLower = this.DiscountCurve.F(AsOf, CDX.Maturity);
          res = rf.solve(fn, cleanPrice, xLower, CDX.Premium * 100.0);
        }
      else
        res = rf.solve(fn, cleanPrice, CDX.Premium / 2.0, CDX.Premium * 2.0);

      return res;
    }

    /// <summary>
    ///   Calculate the clean market price for the CDS Index
    /// </summary>
    /// <returns>Equivalent market price for a CDX</returns>
    [Obsolete("Use SpreadToPrice(marketSpread) instead")]
    public double SpreadToPrice()
    {
      if (MarketPremium == 0.0)
        throw new ArgumentException("SpreadToPrice() calculation requires MarketPremium to be set. Use SpreadToPrice(marketSpread) otherwise.");
      return MarketPrice(MarketPremium);
    }

    /// <summary>
    ///   Calculate the clean market price given a market quoted spread for the CDS Index
    /// </summary>
    /// <param name="marketSpread">The quoted market spread (1bp = 0.0001)</param>
    /// <returns>Equivalent market price for a CDX (unit base, 1 = par)</returns>
    public double SpreadToPrice(double marketSpread)
    {
      return MarketPrice(marketSpread);
    }

    private class MarketPriceEvaluator : SolverFn
    {
      private CDXPricer pricer_;
      internal MarketPriceEvaluator(CDXPricer pricer)
      {
        pricer_ = pricer;
      }
      public override double evaluate(double spread)
      {
        logger.DebugFormat("Trying spread {0}", spread);
        double fv = pricer_.MarketPrice(spread);
        logger.DebugFormat("Returning clean index market price {0} for quote {1}", fv, spread);
        return fv;
      }
    }

    /// <summary>
    ///   Calculate the implied market quoted spread given a market value for the CDS Index
    /// </summary>
    /// <remarks>
    ///   <para>Solves for the market quoted spread which would imply a
    ///   value matching <paramref name="marketValue">the specified market value</paramref>.
    ///   This should be a full market value, including any accrued.</para>
    /// </remarks>
    /// <param name="marketValue">Value of the CDX in dollars including accrued</param>
    /// <returns>Implied market quoted spread for a CDX</returns>
    public double ImpliedQuotedSpread(double marketValue)
    {
      logger.Debug("Calculating implied quoted spread for CDS Index...");
      if (Settle > CDX.Maturity)
        return 0.0;
      // Set up root finder
      //
      Brent rf = new Brent();
      rf.setToleranceX(1e-6);
      rf.setToleranceF(1e-10);
      rf.setLowerBounds(1E-10);

      CDXPricer pricerClone = (CDXPricer)this.Clone();
      pricerClone.QuotingConvention = QuotingConvention.CreditSpread;
      pricerClone.MarketQuote = 0.01;
      pricerClone.Validate();

      // Solve
      double res = rf.solve(new QuotedSpreadEvaluator(pricerClone),
        marketValue, CDX.Premium / 2.0, CDX.Premium * 2.0);

      return res;
    }

    //
    // Function for root find evaluation.
    // Called by root find to find the market value of index given a quoted spread.
    //
    class QuotedSpreadEvaluator : SolverFn
    {
      internal QuotedSpreadEvaluator(CDXPricer pricer)
      {
        pricer_ = pricer;
      }
      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying spread {0}", x);
        double fv = pricer_.MarketValue(x);
        logger.DebugFormat("Returning index market value {0} for quote {1}", fv, x);
        return fv;
      }
      private CDXPricer pricer_;
    }

    /// <summary>
    ///   Calculate the Spread 01 using Market calculations
    /// </summary>
    /// <remarks>
    ///   <para>The Spread 01 is the implied change in market value from a one basis point
    ///   shift in credit spreads.</para>
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the CDX.</para>
    ///   <para>The Spread 01 is calculated by calculating the market value of the CDX and
    ///   then bumping up the implied credit curve by four basis points and re-calculating
    ///   the value of the CDX and returning the difference in value divided by four.</para>
    /// </remarks>
    /// <returns>Spread 01</returns>
    public double EquivCDSSpread01()
    {
      return Settle > CDX.Maturity ? 0.0 : Sensitivities.Spread01(EquivalentCDSPricer, 4, 0);
    }

   
    /// <summary>
    /// Calculate Equivalent CDS spread01
    /// </summary>
    /// <param name="bumpFlags">BumpFlags</param>
    /// <returns></returns>
    public double EquivCDSSpread01(BumpFlags bumpFlags)
    {
      if (Settle > CDX.Maturity)
        return 0.0;

      return Sensitivities2.Spread01(EquivalentCDSPricer, "Pv", 4, 0, bumpFlags);
    }


    /// <summary>
    ///   Calculate the Spread 01 using Market calculations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method simply calls EquivCDSSpread01(), for backward compatibility only.</para>
    ///   <seealso cref="EquivCDSSpread01()"/>
    /// </remarks>
    ///
    /// <returns>Spread 01</returns>
    [Obsolete("Replaced by EquivCDSSpread01()")]
    public double Spread01()
    {
      return Sensitivities2.Spread01(EquivalentCDSPricer, null, 4, 0, BumpFlags.BumpInPlace);
    }

    /// <summary>
    ///   Calculate the Spread Gamma using Market calculations
    /// </summary>
    /// <remarks>
    ///   <para>The Spread Gamma is the implied change in delta from a one basis point
    ///   shift in credit spreads.</para>
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the CDX.</para>
    ///   <para>The Spread Gamma is calculated by calculating the market value of the CDX and
    ///   then bumping down the implied credit curve by two basis points and calculating the
    ///   market value of the CDX, then bumping up the implied credit curve by two basis points
    ///   and re-calculating the value of the CDX and returning the difference in calculated
    ///   deltas.</para>
    /// </remarks>
    /// <returns>Spread 01</returns>
    public double EquivCDSSpreadGamma(BumpFlags bumpFlags)
    {
      if (Settle > CDX.Maturity)
        return 0.0;
      return Sensitivities2.SpreadGamma(EquivalentCDSPricer, "Pv", 2, 2, bumpFlags);
    }

    /// <summary>
    ///   Calculate the Rate 01 using Market calculations
    /// </summary>
    /// <remarks>
    ///   <para>The Rate 01 is the implied change in market value from a one basis point
    ///   shift in the interest rate curve.</para>
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the CDX.</para>
    ///   <para>The Rate 01 is calculated by calculating the market value of the CDX and
    ///   then bumping up the interest rate curve by four basis points, re-calibrating the
    ///   implied market credit curve and re-calculating the value of the CDX and
    ///   returning the difference in value divided by four.</para>
    /// </remarks>
    /// <returns>IR 01</returns>
    public double
    EquivCDSRate01()
    {
      return Settle > CDX.Maturity ? 0.0 : Sensitivities.IR01(EquivalentCDSPricer, 4, 0, true);
    }

    /// <summary>
    ///   Calculate the Recovery 01 using Market calculations
    /// </summary>
    /// <remarks>
    ///   <para>The Recovery 01 is the implied change in market value from a one percent
    ///   increase in recovery rates.</para>
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the CDX.</para>
    ///   <para>The Recovery 01 is calculated by calculating the market value of the CDX and
    ///   then bumping up the recovery rate used for the implied credit curve by one percent,
    ///   re-calibrating the implied credit curve, then re-calculating the value of the CDX
    ///   and returning the difference in value.</para>
    /// </remarks>
    /// <returns>Recovery 01</returns>
    public double EquivCDSRecovery01()
    {
      return Settle > CDX.Maturity ? 0.0 : Sensitivities.Recovery01(EquivalentCDSPricer, 0.01, 0, true);
    }

    /// <summary>
    ///   Calculate theta using Market calculations
    /// </summary>
    /// <remarks>
    ///   <para>The theta is calculated as the difference between the current
    ///   MTM value, and the MTM value at the specified 
    ///   <paramref name="toAsOf">future pricing date</paramref> and
    ///   <paramref name="toSettle">future settlement date</paramref>.</para>
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the CDX.</para>
    ///   <para>All term structures are held constant while moving the
    ///   the pricing and settlement dates (ie the 30-day survival probability
    ///   and the 30-day discount factor remain unchanged relative to the
    ///   pricing dates.</para>
    /// </remarks>
    /// <param name="toAsOf">Forward pricing date</param>
    /// <param name="toSettle">Forward settlement date</param>
    /// <returns>Impact on MTM value of moving pricing and settlement dates forward</returns>
    public double EquivCDSTheta(Dt toAsOf, Dt toSettle)
    {
      return Settle > CDX.Maturity ? 0.0 : Sensitivities.Theta(EquivalentCDSPricer, null, toAsOf, toSettle, ThetaFlags.None, SensitivityRescaleStrikes.No);
    }

    /// <summary>
    /// Compute the forward premium sensitivity using equivalent CDS pricer
    /// </summary>
    /// <param name="fwdSettle">Forward settle date</param>
    /// <returns></returns>
    public double EquivCDSFwdPremium01(Dt fwdSettle)
    {
      return EquivalentCDSPricer.FwdPremium01(fwdSettle);
    }
    /// <summary>
    ///   Calculate theta using Market calculations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method simply calls EquivCDSTheta(toAsOf, toSettle),
    ///   for backward compatibility only</para>
    /// </remarks>
    ///
    /// <param name="toAsOf">Forward pricing date</param>
    /// <param name="toSettle">Forward settlement date</param>
    ///
    /// <returns>Impact on MTM value of moving pricing and settlement dates forward</returns>
    ///
    [Obsolete("Replaced by EquivCDSTheta()")]
    public double
    Theta(Dt toAsOf, Dt toSettle)
    {
      return Sensitivities.Theta(EquivalentCDSPricer, null, toAsOf, toSettle, ThetaFlags.None, SensitivityRescaleStrikes.No);
    }
    #endregion MarketMethods

    #region RelativeValueMethods

    /// <summary>
    ///   Calculate the intrinsic value of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The intrinsic value is the NPV of the portfolio of underlying
    ///   CDS priced using the current market CDS levels.</para>
    ///   <para>The intrinsic value is calculated by present valuing (at the
    ///   current CDS market levels), the portfolio of CDS (whose premium
    ///   is the deal spread) composing the index weighted by their proportion
    ///   of the index and subtracting accrued</para>
    ///   <para>The general approach is to construct a replicating portfolio of
    ///   single name CDS with characteristics that match the index, and then
    ///   pricing the portfolio using the law of one price: if two financial
    ///   instruments generate the same cashflows and liabilities, then they
    ///   must share the same price.  In this case the Index must have the same
    ///   value as the portfolio.</para>
    ///   <para>The intrinsic value includes the accrued of the CDX</para>
    /// </remarks>
    /// <returns>Intrinsic value of CDX Note</returns>
    public double IntrinsicValue_direct()
    {
      logger.Debug("Calculating intrinsic value of CDX Note...");
      if (survivalCurves_ == null)
        throw new ArgumentException("CDX Relative value calcs require SurvivalCurves to be set");

      // Price index components off each market curve
      double totPv = 0.0;
      CDX note = CDX;

      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        // Create pricer
        CDS cds = new CDS(note.Effective, note.Maturity, note.Ccy, note.FirstPrem, note.Premium,
                          note.DayCount, note.Freq, note.BDConvention, note.Calendar);
        cds.CopyScheduleParams(note);
        if (note.CdxType == CdxType.FundedFixed)
          cds.CdsType = CdsType.FundedFixed;
        else if (note.CdxType == CdxType.FundedFloating)
          cds.CdsType = CdsType.FundedFloating;
        else
          cds.CdsType = CdsType.Unfunded;

        CDSCashflowPricer pricer = new CDSCashflowPricer(cds, AsOf, Settle, DiscountCurve, ReferenceCurve, survivalCurves_[i], 0, TimeUnit.None);
        pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        double pv = pricer.ProductPv();

        logger.DebugFormat("Calculated index component {0} value = {1}", survivalCurves_[i].Name, pv);
        totPv += pv * ((note.Weights != null) ? note.Weights[i] : (1.0 / survivalCurves_.Length)) * Notional;
      }
      logger.DebugFormat("Returning index intrinsic value {0}", totPv);
      return totPv;
    }

    /// <summary>
    ///   Calculate the present value (full price * Notional) of the cash flow stream
    /// </summary>
    /// <remarks>
    ///   <para>By definition, the present value of the Note is the present value
    ///   of a default swap paying a premium of the note and priced of a flat
    ///   CDS curve of the original index issue premium.</para>
    /// </remarks>
    /// <returns>Present value to the settlement date of the cashflow stream</returns>
    public override double ProductPv()
    {
      if (IsTerminated)
        return 0.0;
      return IntrinsicValue();
    }

    /// <summary>
    ///   Calculate the full price (percentage of Notional) of the cash flow stream
    /// </summary>
    /// <returns>Present value to the settlement date of the cashflow stream as a percentage of Notional</returns>
    public double FullPrice()
    {
      return IntrinsicValue() / Notional;
    }

    /// <summary>
    ///   Calculate the implied basis for the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>Calculate the implied basis for the CDX Note by solving for the spread we
    ///   need to shift each underlying credit curve so that the implied fair value
    ///   equals the market value.</para>
    ///   <para>Solves for the spread to add to each of the underlying credits so that the
    ///   fair value matches the market value of the index.</para>
    /// </remarks>
    /// <param name="marketValue">Current market value of CDX Note in dollars including accrued</param>
    /// <returns>Implied basis for the CDX Note</returns>
    public double Basis(double marketValue)
    {
      logger.Debug("Calculating basis of CDS Index...");
      if (survivalCurves_ == null)
        throw new ArgumentException("CDX Relative value calcs require SurvivalCurves to be set");
      if (Settle > CDX.Maturity)
        return 0.0;
      // Set up root finder
      //
      Brent rf = new Brent();
      rf.setToleranceX(Baskets.CDXBasisFactorCalc.ToleranceX);
      rf.setToleranceF(Baskets.CDXBasisFactorCalc.ToleranceF);

      // construct a set of curves to bump
      SurvivalCurve[] bumpedSurvivalCurves = new SurvivalCurve[survivalCurves_.Length];
      for (int i = 0; i < survivalCurves_.Length; i++)
        bumpedSurvivalCurves[i] = (SurvivalCurve)survivalCurves_[i].Clone();

      // Solve
      CDXPricer pricer = (CDXPricer)this.MemberwiseClone();
      pricer.survivalCurves_ = bumpedSurvivalCurves;
      double res = rf.solve(new BasisEvaluator(pricer, SurvivalCurves),
        marketValue, -0.0001, 0.001);
      return res;
    }

    class BasisEvaluator : SolverFn
    {
      internal BasisEvaluator(CDXPricer pricer, SurvivalCurve[] savedCurves)
      {
        pricer_ = pricer; savedCurves_ = savedCurves;
      }
      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying factor {0}", x);

        // Bump up curves
        CurveUtil.CurveBump(pricer_.survivalCurves_, null,
          new double[] { x }, true, false, true, null);
        // Calculate fair value
        double fv = pricer_.IntrinsicValue(true);
        // Restore curves
        //CurveUtil.CurveBump(survivalCurves_, (string[])null, new double[] { x }, false, false, false, null);
        CurveUtil.CurveRestoreQuotes(pricer_.survivalCurves_, savedCurves_);

        // Return results scaled to percent of notional
        logger.DebugFormat("Returning index fair value {0} for factor {1}", fv, x);

        return fv;
      }
      private CDXPricer pricer_;
      SurvivalCurve[] savedCurves_;
    }

    /// <summary>
    ///   Calculate the implied scaling factor which matches the specified market value.
    /// </summary>
    /// <remarks>
    ///   <para>Calculate the implied scale for the CDX Note by solving for the ratio we
    ///   need to shift each underlying credit curve so that the implied Fair Value
    ///   equals the market value.</para>
    ///   <para>Solves for the ratio to scale each of the underlying credits so that the
    ///   fair value matches the market value of the index.</para>
    /// </remarks>
    /// <param name="marketValue">Current market value of CDS Index in dollars including accrued</param>
    /// <returns>Implied scale for the CDS Index</returns>
    public double Factor( double marketValue )
    {
      logger.DebugFormat("Calculating basis factor of CDS Index which matches a market value of {0}...", marketValue);
      return BasisFactor(null, null, null, marketValue, null);
    }

    /// <summary>
    ///   Calculate the implied scale for the CDX Note by solving for the ratio we
    ///   need to shift each underlying credit curve so that the implied Fair Value
    ///   equals the market value.
    /// </summary>
    /// <remarks>
    ///   <para>Solves for the ratio to scale each of the underlying credits so that the
    ///   fair value matches the market value of the index.</para>
    /// </remarks>
    /// <param name="tenorNamesScaled">List of tenors already scaled</param>
    /// <param name="scalingFactors">Scaling factors for each tenor</param>
    /// <param name="tenorNamesToScale">List of tenor names to scale</param>
    /// <param name="marketValue">Current market value of CDS Index in dollars including accrued</param>
    /// <param name="scalingWeights">Array of scaling weights for each curve</param>
    /// <returns>Implied scale for the CDS Index</returns>
    public double BasisFactor(
      string[] tenorNamesScaled,
      double[] scalingFactors,
      string[] tenorNamesToScale,
      double marketValue,
      double[] scalingWeights
      )
    {
      logger.DebugFormat("Calculating basis factor of CDS Index which matches a market value of {0}...", marketValue);
      if (survivalCurves_ == null)
        throw new ArgumentException("CDX Relative value calcs require SurvivalCurves to be set");
      if (Settle > CDX.Maturity)
        return 0.0;
      // check validity of tenor names
      //
      if (null != tenorNamesScaled && !CurveUtil.CurveHasTenors(survivalCurves_, tenorNamesScaled))
        throw new ArgumentException("Some tenors scaled are not in any curve");
      if (null != tenorNamesToScale && !CurveUtil.CurveHasTenors(survivalCurves_, tenorNamesToScale))
        throw new ArgumentException("Some tenors to scale are not in any curve");

      // Want weights, if provided, to sum to one.
      if (scalingWeights == null || scalingWeights.Length == 0)
      {
        scalingWeights = new double[survivalCurves_.Length];
        for (int i = 0; i < scalingWeights.Length; ++i)
          scalingWeights[i] = 1.0;
      }

      // Solve
      double res = Baskets.CDXBasisFactorCalc.Solve(this, survivalCurves_, true,
        tenorNamesScaled, scalingFactors, tenorNamesToScale, marketValue, scalingWeights);

      logger.DebugFormat("Returning basis factor of {0}", res);

      return res;
    }

    /// <summary>
    ///   Calculate the implied scaling factors for a set of CDX Notes by solving for the factors
    ///   needed to scale each underlying credit curve so that the underlying credit curves are
    ///   consistent with the market quotes for the CDX Notes.
    /// </summary>
    /// <remarks>
    ///   <para>CDX Notes typically trade at a spread or basis relative to a portfolio of
    ///   replicating CDS. This is due to a number of factors including differences in terms
    ///   between the CDX and CDX, liquidity and market segmentation.</para>
    ///   <para>When pricing credit products on the Indices such as the liquid
    ///   tranches, market best practice is to scale the underlying CDS quotes to be
    ///   consistent with where the CDX trade.</para>
    ///   <para>This function provides several alternate ways of scaling the underlying CDS
    ///   to be consistent with a term structure of CDX quotes.</para>
    ///   <para>Each of the tenors specified by <paramref name="tenors"/> for each for each of
    ///   the underlying survival curves in <paramref name="survivalCurves"/> are scaled in
    ///   sequence.</para>
    ///   <para>For each tenor, the <paramref name="scalingMethods"/> dictate how the scaling
    ///   is performed.</para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="cdx">List of CDX to base scaling on</param>
    /// <param name="tenors">List of tenors to scale</param>
    /// <param name="quotedSpreads">Quoted market spreads for each CDX</param>
    /// <param name="scalingMethods">Scaling method for each tenor</param>
    /// <param name="overrideFactors">Specific override factors for each tenor</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="scalingWeights">Array of scaling weights for each curve</param>
    /// <returns>Implied scaling factor for the CDX Note</returns>
    public static double[] Scaling(
      Dt asOf,
      Dt settle,
      CDX[] cdx,
      string[] tenors,
      double[] quotedSpreads,
      CDXScalingMethod[] scalingMethods,
      double[] overrideFactors,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] scalingWeights
      )
    {
      return CDXPricer.Scaling(asOf, settle, cdx, tenors, quotedSpreads, false, scalingMethods,
        true, overrideFactors, discountCurve, survivalCurves, scalingWeights);
    }

    /// <summary>
    ///   Calculate the implied scaling factors for a set of CDX Notes by solving for the factors
    ///   needed to scale each underlying credit curve so that the underlying credit curves are
    ///   consistent with the market quotes for the CDX Notes.
    /// </summary>
    /// <remarks>
    ///   <para>CDX Notes typically trade at a spread or basis relative to a portfolio of
    ///   replicating CDS. This is due to a number of factors including differences in terms
    ///   between the CDX and CDX, liquidity and market segmentation.</para>
    ///   <para>When pricing credit products on the Indices such as the liquid
    ///   tranches, market best practice is to scale the underlying CDS quotes to be
    ///   consistent with where the CDX trade.</para>
    ///   <para>This function provides several alternate ways of scaling the underlying CDS
    ///   to be consistent with a term structure of CDX quotes.</para>
    ///   <para>Each of the tenors specified by <paramref name="tenors"/> for each for each of
    ///   the underlying survival curves in <paramref name="survivalCurves"/> are scaled in
    ///   sequence.</para>
    ///   <para>For each tenor, the <paramref name="scalingMethods"/> dictate how the scaling
    ///   is performed.</para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="cdx">List of CDX to base scaling on</param>
    /// <param name="tenors">List of tenors to scale</param>
    /// <param name="quotedSpreads">Quoted market spreads for each CDX</param>
    /// <param name="scalingMethods">Scaling method for each tenor</param>
    /// <param name="relativeScaling">True to bump relatively (%), false to bump absolutely (bps)</param>
    /// <param name="overrideFactors">Specific override factors for each tenor</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="scalingWeights">Array of scaling weights for each curve</param>
    /// <returns>Implied scaling factor for the CDX Note</returns>
    public static double[] Scaling(
      Dt asOf,
      Dt settle,
      CDX[] cdx,
      string[] tenors,
      double[] quotedSpreads,
      CDXScalingMethod[] scalingMethods,
      bool relativeScaling,
      double[] overrideFactors,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] scalingWeights)
    {
      return CDXPricer.Scaling(asOf, settle, cdx, tenors, quotedSpreads, false,
        scalingMethods, relativeScaling, overrideFactors,
        discountCurve, survivalCurves, scalingWeights);
    }

    /// <summary>
    ///   Calculate the implied scaling factors for a set of CDX Notes by solving for the factors
    ///   needed to scale each underlying credit curve so that the underlying credit curves are
    ///   consistent with the market quotes for the CDX Notes.
    /// </summary>
    /// <remarks>
    ///   <para>CDX Notes typically trade at a spread or basis relative to a portfolio of
    ///   replicating CDS. This is due to a number of factors including differences in terms
    ///   between the CDX and CDX, liquidity and market segmentation.</para>
    ///   <para>When pricing credit products on the Indices such as the liquid
    ///   tranches, market best practice is to scale the underlying CDS quotes to be
    ///   consistent with where the CDX trade.</para>
    ///   <para>This function provides several alternate ways of scaling the underlying CDS
    ///   to be consistent with a term structure of CDX quotes.</para>
    ///   <para>Each of the tenors specified by <paramref name="tenors"/> for each for each of
    ///   the underlying survival curves in <paramref name="survivalCurves"/> are scaled in
    ///   sequence.</para>
    ///   <para>For each tenor, the <paramref name="scalingMethods"/> dictate how the scaling
    ///   is performed.</para>
    ///   <para>If only one <paramref name="scalingMethods"/> is specified, this is used for
    ///   all tenors.</para>
    ///   <para>If any tenors are missing index quotes, and Override or No scaling has not
    ///   been specified, the next or previous tenor with a valid index quote will be used
    ///   for scaling (ie an implied Next or Previous).</para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="cdx">List of CDX to base scaling on</param>
    /// <param name="tenors">List of tenors to scale</param>
    /// <param name="quotes">Market quotes for each CDX</param>
    /// <param name="quotesArePrices">Use TRUE for price quotes (100 based) and FALSE for spreads</param>
    /// <param name="scalingMethods">Scaling method for each tenor</param>
    /// <param name="relativeScaling">True to bump relatively (%), false to bump absolutely (bps)</param>
    /// <param name="overrideFactors">Specific override factors for each tenor</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="scalingWeights">Array of scaling weights for each curve</param>
    /// <returns>Implied scaling factor for the CDX Note</returns>
    public static double[] Scaling(
      Dt asOf,
      Dt settle,
      CDX[] cdx,
      string[] tenors,
      double[] quotes,
      bool quotesArePrices,
      CDXScalingMethod[] scalingMethods,
      bool relativeScaling,
      double[] overrideFactors,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] scalingWeights)
    {
      return Scaling(asOf, settle, cdx, tenors, quotes, quotesArePrices,
        scalingMethods, relativeScaling, overrideFactors, discountCurve, survivalCurves, scalingWeights, 0.4);
    }

    /// <summary>
    ///   Calculate the implied scaling factors for a set of CDX Notes by solving for the factors
    ///   needed to scale each underlying credit curve so that the underlying credit curves are
    ///   consistent with the market quotes for the CDX Notes.
    /// </summary>
    /// <remarks>
    ///   <para>CDX Notes typically trade at a spread or basis relative to a portfolio of
    ///   replicating CDS. This is due to a number of factors including differences in terms
    ///   between the CDX and CDX, liquidity and market segmentation.</para>
    ///   <para>When pricing credit products on the Indices such as the liquid
    ///   tranches, market best practice is to scale the underlying CDS quotes to be
    ///   consistent with where the CDX trade.</para>
    ///   <para>This function provides several alternate ways of scaling the underlying CDS
    ///   to be consistent with a term structure of CDX quotes.</para>
    ///   <para>Each of the tenors specified by <paramref name="tenors"/> for each for each of
    ///   the underlying survival curves in <paramref name="survivalCurves"/> are scaled in
    ///   sequence.</para>
    ///   <para>For each tenor, the <paramref name="scalingMethods"/> dictate how the scaling
    ///   is performed.</para>
    ///   <para>If only one <paramref name="scalingMethods"/> is specified, this is used for
    ///   all tenors.</para>
    ///   <para>If any tenors are missing index quotes, and Override or No scaling has not
    ///   been specified, the next or previous tenor with a valid index quote will be used
    ///   for scaling (ie an implied Next or Previous).</para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="cdx">List of CDX to base scaling on</param>
    /// <param name="tenors">List of tenors to scale</param>
    /// <param name="quotes">Market quotes for each CDX</param>
    /// <param name="quotesArePrices">Use TRUE for price quotes (100 based) and FALSE for spreads</param>
    /// <param name="scalingMethods">Scaling method for each tenor</param>
    /// <param name="relativeScaling">True to bump relatively (%), false to bump absolutely (bps)</param>
    /// <param name="overrideFactors">Specific override factors for each tenor</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="scalingWeights">Array of scaling weights for each curve</param>
    /// <param name="recoveryRate">Recovery rate to use in Model method approach</param>
    /// <returns>Implied scaling factor for the CDX Note</returns>
    public static double[] Scaling(
      Dt asOf,
      Dt settle,
      CDX[] cdx,
      string[] tenors,
      double[] quotes,
      bool quotesArePrices,
      CDXScalingMethod[] scalingMethods,
      bool relativeScaling,
      double[] overrideFactors,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] scalingWeights,
      double recoveryRate)
    {
      // Validate
      if (tenors.Length != cdx.Length ||
           tenors.Length != quotes.Length ||
           (tenors.Length != scalingMethods.Length && scalingMethods.Length != 1))
        throw new ArgumentException("Number of tenors, CDX, quoted spreads, and scaling methods must be the same");
      if (scalingWeights != null && scalingWeights.Length > 0 && scalingWeights.Length != survivalCurves.Length)
        throw new ArgumentException("Number of scaling weights must match number of survival curves");

      // Fill out scalingMethod if we need to
      if (scalingMethods.Length == 1)
      {
        CDXScalingMethod method = scalingMethods[0];
        scalingMethods = new CDXScalingMethod[tenors.Length];
        for (int i = 0; i < scalingMethods.Length; i++)
          scalingMethods[i] = method;
      }

      // Want weights, if provided, to sum to one.
      if (scalingWeights == null || scalingWeights.Length == 0)
      {
        scalingWeights = new double[survivalCurves.Length];
        for (int i = 0; i < scalingWeights.Length; ++i)
          scalingWeights[i] = 1.0;
      }

      // Convert quotes to spreads if required
      double[] quotedSpreads = quotes;
      if (quotesArePrices)
      {
        quotedSpreads = new double[quotes.Length];
        for (int i = 0; i < quotes.Length; i++)
        {
          // need a CDXPricer to convert prices to spreads
          if (quotes[i] > 0.0)
          {
            CDXPricer cdxPricer = new CDXPricer(cdx[i], asOf, settle, discountCurve, survivalCurves, 0.50);
            // Quotes as prices are CLEAN, so need to add Accrued() to the implied clean value to get the right spread
            quotedSpreads[i] = cdxPricer.ImpliedQuotedSpread(cdxPricer.Notional * (quotes[i] - 100.0) / 100.0 + cdxPricer.Accrued());
          }
          else
            quotedSpreads[i] = 0.0;
        }
      }

      // Create holder for resulting scaling factors
      double[] factors = new double[tenors.Length];

      // Scale each tenor in sequence
      int tenor = 0;
      // Skip over any "None" at the beginning
      while (tenor < tenors.Length && scalingMethods[tenor] == CDXScalingMethod.None)
        tenor++;
      while (tenor < tenors.Length)
      {
        // Start scaling here. Skip to tenor to use for scaling
        int startTenor = tenor;
        while (tenor < tenors.Length)
        {
          if (scalingMethods[tenor] == CDXScalingMethod.Next ||
            (quotedSpreads[tenor] <= 0.0 &&
            scalingMethods[tenor] != CDXScalingMethod.None &&
            scalingMethods[tenor] != CDXScalingMethod.Override))
            // Skip this tenor of indicated to skip or of no index spread to scale too
            tenor++;
          else if (scalingMethods[tenor] == CDXScalingMethod.Previous)
            throw new ArgumentException("Tenor scaling methods are in an incorrect order");
          else
            break;
        }
        // tenor now points to tenor to scale

        // Look forward to see if there are any to be scaled from this tenor
        int endTenor = tenor + 1;
        while (endTenor < tenors.Length)
        {
          if (scalingMethods[endTenor] == CDXScalingMethod.Previous ||
              (quotedSpreads[endTenor] <= 0.0 &&
              scalingMethods[endTenor] != CDXScalingMethod.None &&
              scalingMethods[endTenor] != CDXScalingMethod.Override))
            // Include next tenor if indicated or no index spread to scale to
            endTenor++;
          else
            break;
        }
        // endTenor now points to tenor after this scaling group

        // Do scaling now
        //
        // tenor is index to tenor to scale at this step
        //
        double factor = 0.0;
        switch (scalingMethods[tenor])
        {
          case CDXScalingMethod.None:
            break;
          case CDXScalingMethod.Next:
          case CDXScalingMethod.Previous:
            throw new ToolkitException("Internal logic error in scaling. Unexpected scaling method");
          case CDXScalingMethod.Override:
            if (overrideFactors == null || tenor >= overrideFactors.Length)
              throw new ArgumentException("Override factor not specifed");
            factor = overrideFactors[tenor];
            break;
          case CDXScalingMethod.Spread:
            {
              ArrayList tenorsScaled = new ArrayList();
              ArrayList scaleFactors = new ArrayList();
              for (int i = 0; i < startTenor; i++)
                if ((scalingMethods[i] != CDXScalingMethod.None)
                     && (CurveUtil.CurveHasTenors(survivalCurves, new string[] { tenors[i] })))
                {
                  tenorsScaled.Add(tenors[i]);
                  scaleFactors.Add(factors[i]);
                }
              string[] tenorNamesScaled = (string[])tenorsScaled.ToArray(typeof(string));
              double[] scalingFactors = (double[])scaleFactors.ToArray(typeof(double));

              ArrayList tenorNamesToScaleAL = new ArrayList();
              for (int i = 0; i < endTenor - startTenor; i++)
                if (CurveUtil.CurveHasTenors(survivalCurves, new string[] { tenors[startTenor + i] }))
                  tenorNamesToScaleAL.Add(tenors[startTenor + i]);
              string[] tenorNamesToScale = (string[])tenorNamesToScaleAL.ToArray(typeof(string));

              if (relativeScaling)
              {
                // Calculate average CDS matching index
                double sumWeights = 0.0;
                double sumTenorWeights = 0.0;
                double averageSpread = 0.0;
                double[] cdxWeights = cdx[tenor].Weights;
                for (int i = 0; i < survivalCurves.Length; i++)
                {
                  double tenorWeight = (cdxWeights == null ? 1.0 : cdxWeights[i]) * scalingWeights[i];
                  averageSpread += tenorWeight *
                    CurveUtil.ImpliedSpread(survivalCurves[i], cdx[tenor].Maturity, cdx[tenor].DayCount,
                    cdx[tenor].Freq, cdx[tenor].BDConvention, cdx[tenor].Calendar);
                  sumTenorWeights += tenorWeight;
                  sumWeights += scalingWeights[i];
                }
                averageSpread /= sumTenorWeights;
                if (Math.Abs(sumWeights) < 10.0 * Double.Epsilon)
                  throw new ArgumentException("Scaling weights must not sum to zero.");

                logger.DebugFormat("Average spread is {0}", averageSpread);

                // Factor is just ration of average duration to market quoted spread.
                if (quotedSpreads[tenor] > averageSpread)
                  factor = quotedSpreads[tenor] / averageSpread - 1.0;
                else
                  factor = 1.0 - averageSpread / quotedSpreads[tenor];
                factor *= ((double)(survivalCurves.Length) / sumWeights);
              }
              else
              {
                // Scale
                double targetValue = quotedSpreads[tenor];
                factor = CDXSpreadFactorCalc.Solve(cdx[tenor], survivalCurves, relativeScaling, tenorNamesScaled,
                  scalingFactors, tenorNamesToScale, targetValue, scalingWeights);

                double sumWeights = 0;
                for (int i = 0; i < survivalCurves.Length; ++i)
                  sumWeights += scalingWeights[i];
                if (Math.Abs(sumWeights) < 10.0 * Double.Epsilon)
                  throw new ArgumentException("Scaling weights must not sum to zero.");

                factor *= ((double)(survivalCurves.Length) / sumWeights);
              }
              break;
            }
          case CDXScalingMethod.Duration:
          case CDXScalingMethod.Model:
            {
              // Group info for this scaling
              //
              // Note: Exclude tenors that are marked as unscaled or not in underlying curves.
              ArrayList tenorsScaled = new ArrayList();
              ArrayList scaleFactors = new ArrayList();
              for (int i = 0; i < startTenor; i++)
                if ((scalingMethods[i] != CDXScalingMethod.None)
                     && (CurveUtil.CurveHasTenors(survivalCurves, new string[] { tenors[i] })))
                {
                  tenorsScaled.Add(tenors[i]);
                  scaleFactors.Add(factors[i]);
                }
              string[] tenorNamesScaled = (string[])tenorsScaled.ToArray(typeof(string));
              double[] scalingFactors = (double[])scaleFactors.ToArray(typeof(double));

              ArrayList tenorNamesToScaleAL = new ArrayList();
              for (int i = 0; i < endTenor - startTenor; i++)
                if (CurveUtil.CurveHasTenors(survivalCurves, new string[] { tenors[startTenor + i] }))
                  tenorNamesToScaleAL.Add(tenors[startTenor + i]);
              string[] tenorNamesToScale = (string[])tenorNamesToScaleAL.ToArray(typeof(string));
              // tenorNamesScaled contains list of tenor names already scaled
              // scalingFactors contains list of scaling factors already scaled
              // tenorsNamesToScale contains list of tenor names to scale in this step.

              // Create CDX pricer for scaling function
              CDXPricer pricer = new CDXPricer(cdx[tenor], asOf, settle, discountCurve, survivalCurves, 0.0);
              pricer.Notional = 1.0;
              pricer.MarketRecoveryRate = recoveryRate;

              // Scale
              double targetValue = quotedSpreads[tenor];
              if (scalingMethods[tenor] == CDXScalingMethod.Model)
                targetValue = pricer.MarketValue(targetValue);
              logger.DebugFormat("Scaling tenor {0} to spread {1}, market value {2}", tenors[tenor], quotedSpreads[tenor], targetValue);
              logger.DebugFormat("Tenors scaled:");

              for (int i = 0; i < tenorNamesScaled.Length; i++)
                logger.DebugFormat("{0} factor {1}", tenorNamesScaled[i], scalingFactors[i]);

              logger.DebugFormat("Tenors to scale:");

              for (int i = 0; i < tenorNamesToScale.Length; i++)
                logger.DebugFormat("{0}", tenorNamesToScale[i]);

              if (scalingMethods[tenor] == CDXScalingMethod.Model)
                factor = CDXBasisFactorCalc.Solve(pricer, survivalCurves, relativeScaling,
                  tenorNamesScaled, scalingFactors, tenorNamesToScale, targetValue, scalingWeights);
              else
                factor = CDXDurationFactorCalc.Solve(cdx[tenor], survivalCurves, relativeScaling,
                  tenorNamesScaled, scalingFactors, tenorNamesToScale, targetValue, scalingWeights);
            }
            break;
        }
        logger.DebugFormat("Got factor {0}", factor);

        for (int i = startTenor; i < endTenor; i++)
          factors[i] = factor;
        tenor = endTenor;
      }

      return factors;
    }

    #endregion RelativeValueMethods

    #region UtilityMethods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      if (marketPremium_ < 0.0 || marketPremium_ > 200.0)
        InvalidValue.AddError(errors, this, "MarketPremium", String.Format("Invalid market premium. Must be between 0 and 200, Not {0}", marketPremium_));

      if (marketQuote_ < 0.0 || marketQuote_ > 200.0)
        InvalidValue.AddError(errors, this, "MarketQuote", String.Format("Invalid market quote. Must be between 0 and 200, Not {0}", marketQuote_));

      if (!(quotingConvention_ == QuotingConvention.CreditSpread || quotingConvention_ == QuotingConvention.FlatPrice))
        InvalidValue.AddError(errors, this, "QuotingConvention", String.Format("Invalid quoting convention. Must be either creditspread or flatprice", quotingConvention_));

      return;
    }

    /// <summary>
    ///   Create equivalent CDS Pricer for CDX market standard calculations
    /// </summary>
    /// <param name="marketPremium">Market quote for CDX</param>
    /// <returns>CDSCashflowPricer for CDX</returns>
    private CDSCashflowPricer
    GetEquivalentCDSPricer(double marketPremium,
      SurvivalCurve overrideMarketSurvivalCurve = null)
    {
      CDS cds = null;
      CDX note = CDX;
      SurvivalCurve survivalCurve = overrideMarketSurvivalCurve ?? UserSpecifiedMarketSurvivalCurve;
      if (survivalCurve == null)
      {
        if (double.IsNaN(marketPremium))
          throw new ArgumentException("CDX Market calcs require marketPremium to be set");
        else if (marketPremium < 0.0)
          throw new ArgumentOutOfRangeException("marketPremium",
                                                "CDX marketPremium cannot be negative: " + marketPremium);

        survivalCurve = CDXPricerUtil.FitFlatSurvivalCurve(AsOf, Settle, note, marketPremium,
                                                           MarketRecoveryRate, DiscountCurve, RateResets);

      }

      // Create pricer
      cds = new CDS(note.Effective, note.Maturity, note.Ccy, note.FirstPrem, note.Premium,
                        note.DayCount, note.Freq, note.BDConvention, note.Calendar);
      cds.CopyScheduleParams(note);

      if (note.CdxType == CdxType.FundedFloating)
      {
        cds.CdsType = CdsType.FundedFloating;
        DiscountCurve referenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
        if (referenceCurve == null || marketPremium <= 0.0)
          throw new ArgumentException("Must specify the reference curve and current coupon for a floating rate index");
      }
      else if (note.CdxType == CdxType.FundedFixed)
        cds.CdsType = CdsType.FundedFixed;
      else
        cds.CdsType = CdsType.Unfunded;

      CDSCashflowPricer pricer = new CDSCashflowPricer(cds, AsOf, Settle, DiscountCurve, ReferenceCurve, survivalCurve, 0, TimeUnit.None);
      pricer.Notional = double.IsNaN(MarkToMarketFactor) ? EffectiveNotional : Notional * MarkToMarketFactor; // TODO: change to CurrentNotional.  See case 12804.
      foreach (RateReset r in RateResets)
        pricer.RateResets.Add(r);
      return pricer;
    }

    /// <summary>
    ///   Reset the pricers internal caches
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some internal state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that this internate state should be cleared.</para>
    /// </remarks>
    public override void Reset()
    {
      cashflow_ = null;
      equivalentCDSPricer_ = null;
      marketPremium_ = Double.NaN;
    }

    #endregion UtilityMethods

    #region GenericCalculations

    /// <summary>
    ///   Calculate the intrinsic value of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The intrinsic value is the NPV of the portfolio of underlying
    ///   CDS priced using the current market CDS levels.</para>
    ///   <para>The intrinsic value is calculated by present valuing (at the
    ///   current CDS market levels), the portfolio of CDS (whose premium
    ///   is the deal spread) composing the index weighted by their proportion
    ///   of the index and subtracting accrued</para>
    ///   <para>The general approach is to construct a replicating portfolio of
    ///   single name CDS with characteristics that match the index, and then
    ///   pricing the portfolio using the law of one price: if two financial
    ///   instruments generate the same cashflows and liabilities, then they
    ///   must share the same price.  In this case the Index must have the same
    ///   value as the portfolio.</para>
    ///   <para>The intrinsic value includes the accrued of the CDX</para>
    /// </remarks>
    /// <returns>Intrinsic value of CDX Note</returns>
    public double IntrinsicValue(bool currentMarket)
    {
      logger.Debug("Calculating intrinsic value of CDX Note...");

      double notional = this.Notional;
      double totPv = EvaluateAdditive(delegate(CDSCashflowPricer pricer)
      {
        if (currentMarket)
          pricer.SupportAccrualRebateAfterDefault = false;
        return pricer.ProductPv() * notional;
      });

      logger.DebugFormat("Returning index intrinsic value {0}", totPv);
      return totPv;
    }

    /// <summary>
    ///   Calculate the intrinsic value of the CDX Note
    /// </summary>
    /// <remarks>
    ///   <para>The intrinsic value is the NPV of the portfolio of underlying
    ///   CDS priced using the current market CDS levels.</para>
    ///   <para>The intrinsic value is calculated by present valuing (at the
    ///   current CDS market levels), the portfolio of CDS (whose premium
    ///   is the deal spread) composing the index weighted by their proportion
    ///   of the index and subtracting accrued</para>
    ///   <para>The general approach is to construct a replicating portfolio of
    ///   single name CDS with characteristics that match the index, and then
    ///   pricing the portfolio using the law of one price: if two financial
    ///   instruments generate the same cashflows and liabilities, then they
    ///   must share the same price.  In this case the Index must have the same
    ///   value as the portfolio.</para>
    ///   <para>The intrinsic value includes the accrued of the CDX</para>
    /// </remarks>
    /// <returns>Intrinsic value of CDX Note</returns>
    public double IntrinsicValue()
    {
      return IntrinsicValue(false);
    }

    /// <summary>
    ///   Calculate the protection pv of the CDX Note
    /// </summary>
    /// <returns>Intrinsic value of CDX Note</returns>
    /// <exclude/>
    public double ProtectionPv()
    {
      logger.Debug("Calculating the protection pv of CDX Note...");

      double notional = this.Notional;
      double totalProtPv = EvaluateAdditive(delegate(CDSCashflowPricer pricer)
      {
        return pricer.ProtectionPv() * notional;
      });

      logger.DebugFormat("Returning index protection pv {0}", totalProtPv);
      return totalProtPv;
    }

    /// <summary>
    ///   Calculate the expected loss of the CDX note
    /// </summary>
    /// <returns>Expected loss of CDX note</returns>
    public double ExpectedLoss()
    {
      logger.Debug("Calculating the expected loss of CDX Note...");

      double totalExpectedLoss = EvaluateAdditive(delegate(CDSCashflowPricer pricer)
      {
        return pricer.ExpectedLoss();
      });
      return totalExpectedLoss;
    }

    /// <summary>
    ///   Calculate the risky duration based on the intrinsic value of CDX
    /// </summary>
    /// <remarks>
    ///   <para>The risky duration is the weighted premium01
    ///   of the underlying credit curves on one dollar notional.</para>
    ///   <para>This calculation is based on a short first coupon to be
    ///   consistent with current bank applications</para>
    /// </remarks>
    /// <returns>Risky duration based on the CDX Intrinsic value</returns>
    public double IntrinsicRiskyDuration()
    {
      logger.Debug("Calculating intrinsic risky duration of CDX Note...");

      double totPv = EvaluateAdditive(delegate(CDSCashflowPricer pricer)
      {
        return pricer.RiskyDuration();
      });

      logger.DebugFormat("Returning index intrinsic risky duration {0}", totPv);
      return totPv;
    }

    /// <summary>
    ///   Calculate the break-even premium based on the intrinsic value of CDX
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <returns>Break-even premium based on the CDX Intrinsic value</returns>
    public double IntrinsicBreakEvenPremium()
    {
      logger.Debug("Calculating intrinsic risky duration of CDX Note...");
      if (Settle > CDX.Maturity)
        return 0.0;
      double be;
      if (CDX.CdxType == CdxType.Unfunded)
      {
        double protectPv = -EvaluateAdditive(delegate(CDSCashflowPricer pricer)
        {
          return pricer.ProtectionPv();
        });
        double duration = EvaluateAdditive(delegate(CDSCashflowPricer pricer)
        {
          return pricer.RiskyDuration();
        });
        be = protectPv / duration;
      }
      else
      {
        // search for premium which sets the Clean Price = Pv - Accrued == 1
        // Set up root finder
        Brent rf = new Brent();
        rf.setToleranceX(1e-6);
        rf.setToleranceF(1e-10);
        rf.setLowerBounds(1E-10);

        be = rf.solve(new IntrinsicBEPremiumEvaluator(this),
          1.0, 1e-6, CDX.Premium * 100.0);
      }

      logger.DebugFormat("Returning index intrinsic break even premium {0}", be);
      return be;
    }

    //
    // Function for root finding evaluation.
    // Called by root finder to find the intrinsic BE premium .
    //
    private class IntrinsicBEPremiumEvaluator : SolverFn
    {
      internal IntrinsicBEPremiumEvaluator(CDXPricer pricer)
      {
        pricer_ = pricer;
      }
      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying premium {0}", x);
        double fv = pricer_.EvaluateAdditive(delegate(CDSCashflowPricer pricer)
        {
          pricer.CDS.Premium = x;
          pricer.Reset();
          return pricer.FlatPrice();
        });
        logger.DebugFormat("Returning clean index intrinsic value {0} for quote {1}", fv, x);
        return fv;
      }
      private CDXPricer pricer_;
    }

    /// <summary>
    ///   Calculate average survival probability
    /// </summary>
    /// <remarks>
    ///   <para>The result is the weighted average of the survival probabilities by names.</para>
    /// </remarks>
    /// <returns>Average survival probability at the maturity date</returns>
    public double IntrinsicSurvivalProbability()
    {
      logger.Debug("Calculating intrinsic survival of CDX Note...");

      double survival = EvaluateAdditive(delegate(CDSCashflowPricer pricer)
      {
        return pricer.SurvivalProbability();
      });

      logger.DebugFormat("Returning index intrinsic survival {0}", survival);
      return survival;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cdsEvalFn"></param>
    /// <returns></returns>
    private double EvaluateAdditive(Func<CDSCashflowPricer, double> cdsEvalFn)
    {
      if (survivalCurves_ == null)
        throw new ArgumentException("CDX Relative value calcs require SurvivalCurves to be set");

      double[] weights = CDX.Weights;
      if (survivalCurves_.Length == 1)
        weights = null;

      // Price index components off each market curve
      double totPv;
      if (survivalCurves_.Length > 4 && Parallel.Enabled)
      {
        // Instead of using parallel sum, here we first put
        // individual values in an array, and then sum them up
        // from the first to last.  This avoids the diffs from
        // round-off errors due to the indeterministic order
        // of summation in parallel sum.
        double[] values = new double[survivalCurves_.Length];
        Parallel.For<CDSCashflowPricer>(
          0, survivalCurves_.Length,
          delegate()
          {
            CDSCashflowPricer pricer = GetCDSPricer();
            //send pricer directives to single name pricers
            pricer.PricerFlags = PricerFlags;
            return pricer;
          },
          delegate(int i, CDSCashflowPricer pricer)
          {
            pricer.SurvivalCurve = survivalCurves_[i];
            if (survivalCurves_[i].SurvivalCalibrator != null)
              pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
            else
              pricer.RecoveryCurve = new RecoveryCurve(SurvivalCurves[i].AsOf, 0.4);

            double pv;
            //because pricing relies on weights to be non zero, yet defaults that happen before/on annexation should be zero pv, the special case is JTD/VOD calculation with DaysToSettle = 0
            if (!survivalCurves_[i].DefaultDate.IsEmpty() && (CDX.AnnexDate > survivalCurves_[i].DefaultDate
                                                              ||
                                                              (survivalCurves_[i].Defaulted != Defaulted.WillDefault &&
                                                               CDX.AnnexDate == survivalCurves_[i].DefaultDate)))
            {
              pv = 0;
            }
            else if (Settle > CDX.Maturity && survivalCurves_[i].DefaultDate.IsEmpty())
            {
              pv = 0.0;
            }
            else
            {
              pv= cdsEvalFn(pricer);
            }

            logger.DebugFormat("Calculated index component {0} value = {1}", survivalCurves_[i].Name, pv);
            values[i] = pv * ((weights != null) ? weights[i] : (1.0 / survivalCurves_.Length));
          });
        totPv = 0;
        for (int i = 0; i < values.Length; ++i)
          totPv += values[i];
      }
      else
      {
        CDSCashflowPricer pricer = GetCDSPricer();
        //send pricer directives to single name pricers
        pricer.PricerFlags = PricerFlags;
        totPv = 0;
        for (int i = 0; i < survivalCurves_.Length; i++)
        {
          pricer.SurvivalCurve = survivalCurves_[i];
          if (survivalCurves_[i].SurvivalCalibrator != null)
            pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
          else
            pricer.RecoveryCurve = new RecoveryCurve(SurvivalCurves[i].AsOf, 0.4);

          double pv = (Settle > CDX.Maturity && survivalCurves_[i].DefaultDate.IsEmpty())? 0.0 : cdsEvalFn(pricer);
          //because pricing relies on weights to be non zero, yet defaults that happen before/on annexation should be zero pv, the special case is JTD/VOD calculation with DaysToSettle = 0
          if (!survivalCurves_[i].DefaultDate.IsEmpty() && (CDX.AnnexDate > survivalCurves_[i].DefaultDate
                                                            || (survivalCurves_[i].Defaulted != Defaulted.WillDefault && CDX.AnnexDate == survivalCurves_[i].DefaultDate)))
            pv = 0;
          logger.DebugFormat("Calculated index component {0} value = {1}", survivalCurves_[i].Name, pv);
          totPv += pv * ((weights != null) ? weights[i] : (1.0 / survivalCurves_.Length));
        }
      }
      return totPv;
    }

    /// <summary>
    ///   Get equivalent CDS for market pricing
    /// </summary>
    private CDSCashflowPricer GetCDSPricer()
    {
      CDX note = CDX;
      CDS cds = new CDS(note.Effective, note.Maturity, note.Ccy, note.FirstPrem,
        note.Premium, note.DayCount, note.Freq, note.BDConvention, note.Calendar);
      cds.CopyScheduleParams(note);

      if (note.CdxType == CdxType.FundedFloating)
      {
        cds.CdsType = CdsType.FundedFloating;

        DiscountCurve referenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
        if (referenceCurve == null || note.Premium <= 0.0)
          throw new ArgumentException("Must specify the reference curve and current coupon for a floating rate cds");
      }
      else if (note.CdxType == CdxType.FundedFixed)
        cds.CdsType = CdsType.FundedFixed;
      else
        cds.CdsType = CdsType.Unfunded;

      CDSCashflowPricer pricer = new CDSCashflowPricer(
        cds, AsOf, Settle, DiscountCurve, null, 0, TimeUnit.None)
      {
        PricerFlags = PricerFlags
      };

      foreach (RateReset r in RateResets)
        pricer.RateResets.Add((RateReset)r.Clone());

      return pricer;
    }

    #endregion GenericCalculations

    #region SemiAnalyticSensitivitiesMethods

    /// <summary>
    /// Tests whether pricer supports semi-analytic derivatives
    /// </summary>
    /// <returns>True is semi-analytic derivatives are supported</returns>
    bool IAnalyticDerivativesProvider.HasAnalyticDerivatives
    {
      get { return true; }
    }

    /// <summary>
    /// Returns the collection of semi-analytic derivatives w.r.t each underlying reference curve
    /// </summary>
    /// <returns>IDerivativeCollection object</returns>
    IDerivativeCollection IAnalyticDerivativesProvider.GetDerivativesWrtOrdinates()
    {
      if (survivalCurves_ == null)
        throw new ArgumentException("CDX Relative value calcs require SurvivalCurves to be set");
      double[] weights = CDX.Weights;
      if (survivalCurves_.Length == 1)
        weights = null;
      DerivativeCollection retVal = new DerivativeCollection(survivalCurves_.Length);
      CDSCashflowPricer pricer = GetCDSPricer();
      pricer.PricerFlags = PricerFlags;
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        pricer.SurvivalCurve = survivalCurves_[i];
        if (survivalCurves_[i].SurvivalCalibrator != null)
          pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        else
          pricer.RecoveryCurve = new RecoveryCurve(SurvivalCurves[i].AsOf, 0.4);
        double w = (weights != null) ? weights[i] : (1.0 / survivalCurves_.Length);
        var p = pricer as IAnalyticDerivativesProvider;
        DerivativeCollection cdsSens = (DerivativeCollection)p.GetDerivativesWrtOrdinates();
        retVal.Add(cdsSens.GetDerivatives(0));
        {
          int kk = 0;
          for (int j = 0; j < retVal.GetDerivatives(i).Gradient.Length; j++)
          {
            retVal.GetDerivatives(i).Gradient[j] *= w;
            for (int k = 0; k <= j; k++)
            {
              retVal.GetDerivatives(i).Hessian[kk] *= w;
              kk++;
            }
          }
          retVal.GetDerivatives(i).RecoveryDelta *= w;
          retVal.GetDerivatives(i).Vod *= w;
        }
      }
      return retVal;
    }
    #endregion

    #region Properties

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set
      {
        discountCurve_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Survival curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return survivalCurves_; }
      set
      {
        // Survival curves may be null
        survivalCurves_ = (value != null && value.Length == 0 ?
                            null : value);
        Reset();
      }
    }

    /// <summary>
    ///  Recovery curves from curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get
      {
        // Check null SurvivalCurves
        if (survivalCurves_ == null)
          return null;
        RecoveryCurve[] recoveryCurves = new RecoveryCurve[survivalCurves_.Length];
        for (int i = 0; i < survivalCurves_.Length; i++)
        {
          if (survivalCurves_[i] != null && survivalCurves_[i].Calibrator != null)
            recoveryCurves[i] = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
          else
            throw new ArgumentException(String.Format("Must specify recoveries as curve {0} does not have recoveries from calibration", survivalCurves_[i] == null ? null : survivalCurves_[i].Name));
        }
        return recoveryCurves;
      }
    }

    /// <summary>
    ///   Quoting convention for CDX
    /// </summary>
    public QuotingConvention QuotingConvention
    {
      get
      {
        if (quotingConvention_ != QuotingConvention.FlatPrice)
          quotingConvention_ = QuotingConvention.CreditSpread;
        return quotingConvention_;
      }
      set { quotingConvention_ = value; }
    }

    /// <summary>
    ///   Current market quote 
    /// </summary>
    /// <details>
    ///   <para>CreditSpread and FlatPrice  quoting types are supported
    ///   and are set by <see cref="QuotingConvention"/>. The default
    ///   quoting convention is CreditSpread.</para>
    /// </details>
    public double MarketQuote
    {
      get { return marketQuote_; }
      set
      {
        marketQuote_ = value;
      }
    }

    /// <summary>
    ///   Current market premium as a number (100bp = 0.01).
    /// </summary>
    public double MarketPremium
    {
      get
      {
        if (quotingConvention_ == QuotingConvention.FlatPrice)
        {
          if (!Double.IsNaN(marketPremium_))
            return marketPremium_;
          CDX cdx = (CDX)Product;
          marketPremium_ = CDXPricerUtil.CDXPriceToSpread(AsOf, Settle, cdx, MarketQuote, DiscountCurve, SurvivalCurves, MarketRecoveryRate, CurrentRate);
          return marketPremium_;
        }
        else
          return marketQuote_;
      }
      //set
      //{
      //  marketPremium_ = value;
      //  Reset();
      //}
    }

    /// <summary>
    ///   Recovery rate for market standard calculations
    /// </summary>
    public double MarketRecoveryRate
    {
      get { return marketRecoveryRate_; }
      set { marketRecoveryRate_ = value; }
    }

    /// <summary>
    ///   Product to price
    /// </summary>
    public CDX CDX
    {
      get { return (CDX)Product; }
    }

    /// <summary>
    ///   The effective outstanding notional, including both
    ///   the names not defaulted and the names defaulted
    ///   but not settled.
    /// </summary>
    public override double EffectiveNotional
    {
      get
      {
        return CDXPricerUtil.EffectiveFactor(Settle,
          survivalCurves_, CDX.Weights, CDX.AnnexDate) * Notional;
      }
    }

    /// <summary>
    ///   Remaining notional (not defaulted)
    /// </summary>
    public override double CurrentNotional
    {
      get
      {
        return CDXPricerUtil.CurrentFactor(Settle,
          survivalCurves_, CDX.Weights) * Notional;
      }
    }

    /// <summary>
    ///   The effective outstanding notional, including both
    ///   the names not defaulted and the names defaulted
    ///   but not settled.
    /// </summary>
    public double MarkToMarketFactor
    {
      get
      {
        return !MarkAtPreviousFactorUntilRecovery ? double.NaN : CDXPricerUtil.MarkToMarketFactor(Settle,
          survivalCurves_, CDX.Weights, CDX.AnnexDate);
      }
    }

    /// <summary>
    ///   Create equivalent CDS Pricer for CDX market standard calculations
    /// </summary>
    public CDSCashflowPricer EquivalentCDSPricer
    {
      get
      {
        if (equivalentCDSPricer_ == null)
          equivalentCDSPricer_ = GetEquivalentCDSPricer(MarketPremium);
        return equivalentCDSPricer_;
      }
    }

    /// <summary>
    ///   User specified market survival curve
    /// </summary>
    /// <exclude />
    public SurvivalCurve UserSpecifiedMarketSurvivalCurve
    {
      get { return marketSurvivalCurve_; }
      set { marketSurvivalCurve_ = value; }
    }

    /// <summary>
    ///   User specified or constructed market survival curve
    /// </summary>
    /// <exclude />
    public SurvivalCurve MarketSurvivalCurve
    {
      get
      {
        if (marketSurvivalCurve_ != null)
          return marketSurvivalCurve_;
        return EquivalentCDSPricer.SurvivalCurve;
      }
    }

    

    /// <summary>
    ///   Historical rate fixings (only for funded note)
    /// </summary>
    public IList<RateReset> RateResets
    {
      get
      {
        if (rateResets_ == null)
          rateResets_ = new List<RateReset>();
        return rateResets_;
      }
      set { rateResets_ = (List<RateReset>)value; Reset(); }
    }

    /// <summary>
    ///   Current floating rate
    /// </summary>
    public double CurrentRate
    {
      get { return RateResetUtil.ResetAt(rateResets_, AsOf); }
      set
      {
        // Set the RateResets to support returning the current coupon
        rateResets_ = new List<RateReset>();
        rateResets_.Add(new RateReset(Product.Effective, value));
        Reset();
      }
    }

    /// <summary>
    /// Reference curve for floating payments forecast
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set
      {
        referenceCurve_ = value;
        Reset();
      }
    }

    public int BasketSize
    {
      get { return _basketSize; }
      set { _basketSize = value; }
    }
    #endregion Properties

    #region Data

    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private SurvivalCurve[] survivalCurves_;
    private int _basketSize;
    [Mutable]
    private double marketPremium_ = Double.NaN;
    private double marketRecoveryRate_ = 0.40;      // Market standard recovery rate
    [Mutable]
    private CDSCashflowPricer equivalentCDSPricer_; // Matching CDS Pricer for Market based calcs
    private QuotingConvention quotingConvention_ = QuotingConvention.CreditSpread;
    private double marketQuote_ = Double.NaN;
    // user specified market survival curve
    private SurvivalCurve marketSurvivalCurve_ = null;
    private List<RateReset> rateResets_ = null;
    private bool adjustDurationForRemainingNotional_ = ToolkitConfigurator.Settings.CDXPricer.AdjustDurationForRemainingNotional;
    private bool _markAtPreviousFactorUntilRecovery = ToolkitConfigurator.Settings.CDXPricer.MarkAtPreviousFactorUntilRecovery;
    private const bool _useCache = true;

    #endregion Data

  }
}
