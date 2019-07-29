/*
 *  LCDXPricer.cs
 *
 * 
 */
//#define NEW_DURATION_SOLVER

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers
{
  #region Config
  /// <exclude />
  [Serializable]
  public class LCDXPricerConfig
  {
    /// <exclude />
    [Util.Configuration.ToolkitConfig("AdjustDurationForRemainingNotional when basket has alraedy-occured defaults")]
    public readonly bool AdjustDurationForRemainingNotional = true;
  }
  #endregion Config

  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.LCDX">LCDX/LevX note</see>
  /// using the standard market pricing conventions.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.LCDX" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.LCDX">LCDX/LevX (LCDS Index) Note</seealso>
  [Serializable]
  public partial class LCDXPricer : PricerBase, ICDXPricer, IAnalyticDerivativesProvider
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(LCDXPricer));

    /// <summary>
    /// Adjust risky duration for remaining notional when basket has defaulted names
    /// </summary>
    /// <exclude />
    public bool AdjustDurationForRemainingNotional
    {
      get { return adjustDurationForRemainingNotional_; }
      set { adjustDurationForRemainingNotional_ = value; }
    }

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
    /// <param name="product">LCDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    /// <param name="marketQuote">Current market quote (price where 1 = par)</param>
    ///
    public LCDXPricer(
      LCDX product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves,
      double marketQuote
      )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0) ? null : survivalCurves;
      if (survivalCurves != null)
        GetPrepaymentInfosFromSurvivalCurves();
      MarketQuote = marketQuote;
      return;
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
    /// <param name="product">LCDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///<param name="referenceCurve">Reference curve for floating payment forecasts</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    /// <param name="marketQuote">Current market quote (price where 1 = par)</param>
    ///
    public LCDXPricer(
      LCDX product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve[] survivalCurves,
      double marketQuote
      )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0) ? null : survivalCurves;
      if (survivalCurves != null)
        GetPrepaymentInfosFromSurvivalCurves();
      MarketQuote = marketQuote;
      return;
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
    /// <param name="product">LCDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    ///
    public LCDXPricer(
      LCDX product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves
      )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = survivalCurves;
      if (survivalCurves != null)
        GetPrepaymentInfosFromSurvivalCurves();
      return;
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
    /// <param name="product">LCDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    ///
    public LCDXPricer(
      LCDX product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve[] survivalCurves
      )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      SurvivalCurves = survivalCurves;
      if (survivalCurves != null)
        GetPrepaymentInfosFromSurvivalCurves();
      return;
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
    /// <param name="product">LCDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    /// <param name="prepaymentCurves">Prepayment probability curves matching credits of Index</param>
    /// <param name="prepaymentCorrelations">Correlations between prepayment and default</param>
    /// <param name="marketQuote">Market premiums</param>
    ///
    public LCDXPricer(
      LCDX product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves,
      SurvivalCurve[] prepaymentCurves, double[] prepaymentCorrelations, double marketQuote
      )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0) ? null : survivalCurves;
      MarketQuote = marketQuote;

      if (prepaymentCurves != null)
      {
        if (survivalCurves == null || prepaymentCurves.Length != survivalCurves.Length)
          throw new System.ArgumentException(String.Format(
            "refinance curves (len={0}) and survival curves (len={1}) not match",
            prepaymentCurves.Length, survivalCurves == null ? 0 : survivalCurves.Length));

        if (prepaymentCorrelations == null || prepaymentCorrelations.Length != prepaymentCurves.Length)
          throw new System.ArgumentException(String.Format(
            "refinance curves (len={0}) and correlations (len={1}) not match",
          prepaymentCurves.Length, prepaymentCorrelations == null ? 0 : prepaymentCorrelations.Length));

        this.PrepaymentCurves = prepaymentCurves;
        this.PrepaymentCorrelations = prepaymentCorrelations;
      }
      else if (survivalCurves != null)
      {
        // Prepayment curve not supplied, so extract from survival curves
        GetPrepaymentInfosFromSurvivalCurves();
        if (prepaymentCorrelations != null)
        {
          if (prepaymentCorrelations.Length != survivalCurves.Length)
            throw new System.ArgumentException(String.Format(
              "Survival curves (len={0}) and refinance correlations (len={1}) not match",
            prepaymentCurves.Length, prepaymentCorrelations.Length));
          this.PrepaymentCorrelations = prepaymentCorrelations;
        }
      }

      return;
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
    /// <param name="product">LCDX Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecasts</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    /// <param name="prepaymentCurves">Prepayment probability curves matching credits of Index</param>
    /// <param name="prepaymentCorrelations">Correlations between prepayment and default</param>
    /// <param name="marketQuote">Market premiums</param>
    ///
    public LCDXPricer(
      LCDX product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve[] survivalCurves,
      SurvivalCurve[] prepaymentCurves, double[] prepaymentCorrelations, double marketQuote
      )
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0) ? null : survivalCurves;
      MarketQuote = marketQuote;

      if (prepaymentCurves != null)
      {
        if (survivalCurves == null || prepaymentCurves.Length != survivalCurves.Length)
          throw new System.ArgumentException(String.Format(
            "refinance curves (len={0}) and survival curves (len={1}) not match",
            prepaymentCurves.Length, survivalCurves == null ? 0 : survivalCurves.Length));

        if (prepaymentCorrelations == null || prepaymentCorrelations.Length != prepaymentCurves.Length)
          throw new System.ArgumentException(String.Format(
            "refinance curves (len={0}) and correlations (len={1}) not match",
          prepaymentCurves.Length, prepaymentCorrelations == null ? 0 : prepaymentCorrelations.Length));

        this.PrepaymentCurves = prepaymentCurves;
        this.PrepaymentCorrelations = prepaymentCorrelations;
      }
      else if (survivalCurves != null)
      {
        // Prepayment curve not supplied, so extract from survival curves
        GetPrepaymentInfosFromSurvivalCurves();
        if (prepaymentCorrelations != null)
        {
          if (prepaymentCorrelations.Length != survivalCurves.Length)
            throw new System.ArgumentException(String.Format(
              "Survival curves (len={0}) and refinance correlations (len={1}) not match",
            prepaymentCurves.Length, prepaymentCorrelations.Length));
          this.PrepaymentCorrelations = prepaymentCorrelations;
        }
      }

      return;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      LCDXPricer obj = (LCDXPricer)base.Clone();

      if (survivalCurves_ != null)
      {
        obj.survivalCurves_ = new SurvivalCurve[survivalCurves_.Length];
        for (int i = 0; i < survivalCurves_.Length; i++)
          obj.survivalCurves_[i] = (SurvivalCurve)survivalCurves_[i].Clone();
      }
      obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();
      obj.referenceCurve_ = (DiscountCurve)referenceCurve_.Clone();
      obj.cashflow_ = null;
      if (prepaymentCurves_ != null)
      {
        obj.prepaymentCurves_ = new SurvivalCurve[prepaymentCurves_.Length];
        for (int i = 0; i < prepaymentCurves_.Length; i++)
          obj.prepaymentCurves_[i] = (SurvivalCurve)prepaymentCurves_[i].Clone();
      }
      obj.prepaymentCorrelations_ = CloneUtil.Clone(prepaymentCorrelations_);

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

      LCDX p = (LCDX)Product;
      return CDXPricerUtil.GetPaymentSchedule(ps, from, Settle, 
        p.Effective, p.FirstPrem, p.Maturity, p.AnnexDate, 
        p.Ccy, p.Premium, p.DayCount, p.Freq, 
        CycleRule.None, p.BDConvention,
        p.Calendar, p.CdxType != CdxType.Unfunded,
        MarketRecoveryRate, DiscountCurve,
        referenceCurve, p.CdxType == CdxType.FundedFloating,
        RateResets, SurvivalCurves, p.Weights,
        RecoveryCurves);
    }

    /// <summary>
    ///   Calculate the value of the LCDX Note.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market value is the settlement value in dollars (or other currency) of the index.</para>
    ///
    ///   <para>The market value is
    ///   <formula inline="true">(\mathrm{Market Quote} - 1) * \mathrm{Notional} + \mathrm{Accrued}</formula>.</para>
    /// </remarks>
    ///
    /// <returns>Value of the LCDX Note at current market quote</returns>
    ///
    public double
    MarketValue()
    {
      return this.MarketValue(this.MarketQuote);
    }

    /// <summary>
    ///   Calculate the value of the LCDX Note at the specified
    ///   quoted level (as a value in currency terms)
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market value is the settlement value in dollars (or other currency) of the index.</para>
    /// 
    ///   <para>The Market Value includes the accrued of the LCDX</para>
    /// </remarks>
    ///
    /// <param name="marketQuote">Current quote number (1=par)</param>
    ///
    /// <returns>Value of the LCDX Note at the specified market quote</returns>
    ///
    public double MarketValue(double marketQuote)
    {
      return Settle > LCDX.Maturity ? 0.0 : CDXPricerUtil.MarketValue(
        this, marketQuote, QuotingConvention.FlatPrice,
        () => GetEquivalentCDSPricer(marketQuote, false));
    }

    /// <summary>
    ///   Calculate the clean price (as a percentage of notional) of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market price is simply the market quoted price.</para>
    /// </remarks>
    ///
    /// <returns>Price of the LCDX Note</returns>
    ///
    public double
    MarketPrice()
    {
      return this.MarketPrice(this.MarketQuote);
    }

    /// <summary>
    ///   Calculate the clean price (as a percentage of notional) of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market price is simply the market quoted price.</para>
    /// </remarks>
    ///
    /// <param name="marketQuote">Current market quote (1.0=par)</param>
    ///
    /// <returns>Price of the LCDX Note at the specified quote</returns>
    ///
    public double
    MarketPrice(double marketQuote)
    {
      return marketQuote;
    }

    /// <summary>
    ///   Calculate the accrued premium for a LCDX Note
    /// </summary>
    ///
    /// <returns>Accrued premium of LCDX Note</returns>
    ///
    public override double Accrued()
    {
      // Since the accrued amount does not depend on survival curve in equivalent CDS pricer
      // in case not to fail survival curve fitting inside equivalent CDS pricer, the Accrued
      // should be calculated by using an equivalent CDS pricer with a null survival curve

      return Settle > LCDX.Maturity ? 0.0 : CDXPricerUtil.Accrued(this, () => GetEquivalentCDSPricer(MarketQuote, null));
    }

    /// <summary>
    ///   Calculate the number of accrual days for a Credit Default Swap
    /// </summary>
    ///
    /// <returns>The number of days accrual for a Credit Default Swap</returns>
    ///
    public int
    AccrualDays()
    {
      return Settle > LCDX.Maturity ? 0 : EquivalentCDSPricer.AccrualDays();
    }

    /// <summary>
    ///   Calculate the risky duration of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The risky duration is the fee pv of a LCDX with a premium of 1(10000bps) and
    ///    a notional of 1.0.</para>
    ///
    ///   <para>The risky duration is based on the remaining premium that is uncertain and
    ///   as such does not include any accrued.</para>
    ///
    ///   <para>The Duration is related to the Protection Leg PV, the Break Even Premium and the
    ///   Notional by <formula inline="true">Duration = ProtectionLegPv / { BreakEvenPremium * Notional }</formula></para>
    /// </remarks>
    ///
    /// <returns>Risky duration of the LCDX Note at current market quoted spread</returns>
    ///
    public double
    RiskyDuration()
    {
      return Settle > LCDX.Maturity
         ? 0.0
         :
           AdjustDurationForRemainingNotional
             ? EquivalentCDSPricer.RiskyDuration() * CurrentNotional / Notional
             : EquivalentCDSPricer.RiskyDuration();
    }

    /// <summary>
    ///   Calculate the carry of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The carry is daily income from premium and is simply
    ///   the premium divided by 360 times the notional.</para>
    /// </remarks>
    ///
    /// <returns>Carry of the LCDX Note</returns>
    ///
    public double
    Carry()
    {
      return Settle > LCDX.Maturity ? 0.0 : LCDX.Premium / 360 * CurrentNotional;
    }

    /// <summary>
    ///   Calculate the MTM Carry of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The MTM carry is the daily income of the MTM level
    ///   of the credit default swap. It is the Break Even Premium
    ///   divided by 360 times the notional.</para>
    /// </remarks>
    ///
    /// <returns>MTM Carry of LCDX Note</returns>
    ///
    public double
    MTMCarry()
    {
      return Settle > LCDX.Maturity ? 0.0 : EquivalentCDSPricer.BreakEvenPremium() / 360 * CurrentNotional;
    }


    /// <summary>
    ///   Calculate the clean market price given a market quoted spread for the LCDX Index
    /// </summary>
    ///
    /// <param name="marketSpread">The quoted market spread</param>
    ///
    /// <returns>Equivalent market price for a LCDX (with 1 = par)</returns>
    public double
    SpreadToPrice(double marketSpread)
    {
      if (marketSpread <= 0.0)
        throw new ArgumentOutOfRangeException("marketSpread", "marketSpread must be positive.");
      return (1.0 + GetEquivalentCDSPricer(marketSpread, true).FlatPrice());
    }

    /// <summary>
    ///   Calculate the implied market quoted spread given a market value for the LCDS Index
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Solves for the market quoted spread equivalent to the input price quote (unit based).</para>para>
    /// </remarks>
    ///
    /// <param name="cleanPrice">LCDX price quote (1 = par)</param>
    ///
    /// <returns>Equivalent market quoted spread for a LCDX</returns>
    ///
    public double
    PriceToSpread(double cleanPrice)
    {
      return Settle > LCDX.Maturity ? 0.0 : GetEquivalentCDSPricer(cleanPrice, false).BreakEvenPremium();
    }

    /// <summary>
    ///   Calculate the Spread 01 using Market calculations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Spread 01 is the implied change in market value from a one basis point
    ///   shift in credit spreads.</para>
    ///
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the LCDX.</para>
    ///
    ///   <para>The Spread 01 is calculated by calculating the market value of the LCDX and
    ///   then bumping up the implied credit curve by four basis points and re-calculating
    ///   the value of the LCDX and returning the difference in value divided by four.</para>
    /// <para>The default bump flag is BumpInPlace</para>
    /// </remarks>
    ///
    /// <returns>Spread 01</returns>
    ///
    public double
    EquivCDSSpread01()
    {
      if (Settle > LCDX.Maturity)
        return 0.0;
      return Sensitivities2.Spread01(EquivalentCDSPricer, null, 4, 0, BumpFlags.BumpInPlace);
    }

    /// <summary>
    /// calculate equivalent cds spread01
    /// </summary>
    /// <param name="bumpFlags">BumpFlag, such as BumpRelative, BumpInPlace and so on</param>
    /// <returns></returns>
    public double
    EquivCDSSpread01(BumpFlags bumpFlags)
    {
      if (Settle > LCDX.Maturity)
        return 0.0;
      return Sensitivities2.Spread01(EquivalentCDSPricer, null, 4, 0, bumpFlags);
    }

    /// <summary>
    ///   Calculate the Spread Gamma using Market calculations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Spread Gamma is the implied change in delta from a one basis point
    ///   shift in credit spreads.</para>
    ///
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the LCDX.</para>
    ///
    ///   <para>The Spread Gamma is calculated by calculating the market value of the LCDX and
    ///   then bumping down the implied credit curve by two basis points and calculating the
    ///   market value of the LCDX, then bumping up the implied credit curve by two basis points
    ///   and re-calculating the value of the LCDX and returning the difference in calculated
    ///   deltas.</para>
    /// </remarks>
    ///
    /// <returns>Spread 01</returns>
    ///
    public double
    EquivCDSSpreadGamma()
    {
      if (Settle > LCDX.Maturity)
        return 0.0;
      return Sensitivities2.SpreadGamma(EquivalentCDSPricer, "Pv", 2, 2, BumpFlags.BumpInPlace);
    }

    /// <summary>
    /// Calculate equiv CDS spread gamma.
    /// </summary>
    /// <param name="bumpFlags">BumpFlag, such as BumpInPlace, RefitCurve and so on.</param>
    /// <returns></returns>
    public double EquivCDSSpreadGamma(BumpFlags bumpFlags)
    {
      if (Settle > LCDX.Maturity)
        return 0.0;
      return Sensitivities2.SpreadGamma(EquivalentCDSPricer, "Pv", 2, 2, bumpFlags);
    }

    /// <summary>
    ///   Calculate the Rate 01 using Market calculations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Rate 01 is the implied change in market value from a one basis point
    ///   shift in the interest rate curve.</para>
    ///
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the LCDX.</para>
    ///
    ///   <para>The Rate 01 is calculated by calculating the market value of the LCDX and
    ///   then bumping up the interest rate curve by four basis points, re-calibrating the
    ///   implied market credit curve and re-calculating the value of the LCDX and
    ///   returning the difference in value divided by four.</para>
    /// </remarks>
    ///
    /// <returns>IR 01</returns>
    ///
    public double
    EquivCDSRate01()
    {
      return Settle > LCDX.Maturity ? 0.0 : Sensitivities.IR01(EquivalentCDSPricer, 4, 0, true);
    }

    /// <summary>
    ///   Calculate the Recovery 01 using Market calculations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Recovery 01 is the implied change in market value from a one percent
    ///   increase in recovery rates.</para>
    ///
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the LCDX.</para>
    ///
    ///   <para>The Recovery 01 is calculated by calculating the market value of the LCDX and
    ///   then bumping up the recovery rate used for the implied credit curve by one percent,
    ///   re-calibrating the implied credit curve, then re-calculating the value of the LCDX
    ///   and returning the difference in value divided by four.</para>
    /// </remarks>
    ///
    /// <returns>Recovery 01</returns>
    ///
    public double
    EquivCDSRecovery01()
    {
      return Settle > LCDX.Maturity ? 0.0 : Sensitivities.Recovery01(EquivalentCDSPricer, 0.01, 0, true);
    }

    /// <summary>
    ///   Calculate theta using Market calculations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The theta is calculated as the difference between the current
    ///   MTM value, and the MTM value at the specified future pricing date
    ///   <paramref name="toAsOf"/> and future settlement date
    ///   <paramref name="toSettle"/>.</para>
    ///
    ///   <para>This calculation uses the standard market pricing similar to pricing a
    ///   single-name CDS where the underlying credit curve is implied from the market
    ///   price of the LCDX.</para>
    /// 
    ///   <para>All term structures are held constant while moving the
    ///   the pricing and settlement dates (ie the 30 day survival probability
    ///   and the 30 day discount factor remain unchanged relative to the
    ///   pricing dates.</para>
    /// </remarks>
    ///
    /// <param name="toAsOf">Forward pricing date</param>
    /// <param name="toSettle">Forward settlement date</param>
    ///
    /// <returns>Impact on MTM value of moving pricing and settlement dates forward</returns>
    ///
    public double
    EquivCDSTheta(Dt toAsOf, Dt toSettle)
    {
      return Settle > LCDX.Maturity ? 0.0 : Sensitivities.Theta(EquivalentCDSPricer, null, toAsOf, toSettle, ThetaFlags.None, SensitivityRescaleStrikes.No);
    }

    #endregion MarketMethods

    #region RelativeValueMethods

    /// <summary>
    ///   Calculate the intrinsic value of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The intrinsic value is the NPV of the portfolio of underlying
    ///   LCDS priced using the current market LCDS levels.</para>
    ///
    ///   <para>The intrinsic value is calculated by present valuing (at the
    ///   current LCDS market levels), the portfolio of LCDS (whose premium
    ///   is the deal spread) composing the index weighted by their proportion
    ///   of the index and subtracting accrued</para>
    ///
    ///   <para>The general approach is to construct a replicating portfolio of
    ///   single name LCDS with characteristics that match the index, and then
    ///   pricing the portfolio using the law of one price: if two financial
    ///   instruments generate the same cashflows and liabilities, then they
    ///   must share the same price.  In this case the Index must have the same
    ///   value as the portfolio.</para>
    ///
    ///   <para>The intrinsic value includes the accrued of the LCDX</para>
    /// </remarks>
    ///
    /// <returns>Intrinsic value of LCDX Note</returns>
    ///
    public double IntrinsicValue(bool currentMarket)
    {
      logger.Debug("Calculating intrinsic value of LCDX Note...");
      if (Settle > LCDX.Maturity)
        return 0.0;
      ICDSPricer pricer = GetCDSPricer(AsOf, Settle);
      pricer.Notional = this.Notional;
      //send pricer directives to single name pricers
      ((PricerBase)pricer).PricerFlags = PricerFlags;

      Double_Pricer_Fn cdsEvalFn =
        DoublePricerFnBuilder.CreateDelegate(pricer.GetType(), "Pv");
      double totPv = EvaluateAdditive(pricer, cdsEvalFn);

      logger.DebugFormat("Returning index intrinsic value {0}", totPv);
      return totPv;
    }

    /// <summary>
    ///   Calculate the intrinsic value of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The intrinsic value is the NPV of the portfolio of underlying
    ///   LCDS priced using the current market LCDS levels.</para>
    ///
    ///   <para>The intrinsic value is calculated by present valuing (at the
    ///   current LCDS market levels), the portfolio of LCDS (whose premium
    ///   is the deal spread) composing the index weighted by their proportion
    ///   of the index and subtracting accrued</para>
    ///
    ///   <para>The general approach is to construct a replicating portfolio of
    ///   single name LCDS with characteristics that match the index, and then
    ///   pricing the portfolio using the law of one price: if two financial
    ///   instruments generate the same cashflows and liabilities, then they
    ///   must share the same price.  In this case the Index must have the same
    ///   value as the portfolio.</para>
    ///
    ///   <para>The intrinsic value includes the accrued of the LCDX</para>
    /// </remarks>
    ///
    /// <returns>Intrinsic value of LCDX Note</returns>
    ///
    public double IntrinsicValue()
    {
      return IntrinsicValue(false);
    }

    /// <summary>
    ///   Calculate the intrinsic price of the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The intrinsic price is the value of the replicating portfolio
    ///   of LCDS quoted using the conventionf or the LCDX (quote - 1) + accrued = NPV.</para>
    ///
    ///   <para>The intrinsic value is calculated by present valuing (at the
    ///   current LCDS market levels), the portfolio of LCDS (whose premium
    ///   is the deal spread) composing the index weighted by their proportion
    ///   of the index and subtracting accrued</para>
    ///
    ///   <para>The general approach is to construct a replicating portfolio of
    ///   single name LCDS with characteristics that match the index, and then
    ///   pricing the portfolio using the law of one price: if two financial
    ///   instruments generate the same cashflows and liabilities, then they
    ///   must share the same price.  In this case the Index must have the same
    ///   value as the portfolio.</para>
    ///
    ///   <para>The intrinsic price does not include the accrued of the LCDX (1 = par)</para>
    /// </remarks>
    ///
    /// <returns>Intrinsic price of LCDX Note</returns>
    ///
    public double
    IntrinsicPrice()
    {
      return ((IntrinsicValue() - this.Accrued()) / this.Notional + 1.0);
    }

    /// <summary>
    ///   Calculate the risky duration based on the intrinsic value of LCDX
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The risky duration is the weighted premium01
    ///   of the underlying credit curves on one dollar notional.</para>
    ///
    ///   <para>This calculation is based on a short first coupon to be
    ///   consistent with current bank applications</para>
    /// </remarks>
    ///
    /// <returns>Risky duration based on the LCDX Intrinsic value</returns>
    ///
    public double
    IntrinsicRiskyDuration()
    {
      logger.Debug("Calculating intrinsic risky duration of LCDX Note...");

      ICDSPricer pricer = GetCDSPricer(AsOf, Settle);
      Double_Pricer_Fn cdsEvalFn =
        DoublePricerFnBuilder.CreateDelegate(pricer.GetType(), "RiskyDuration");
      double totPv = EvaluateAdditive(pricer, cdsEvalFn);

      logger.DebugFormat("Returning index intrinsic risky duration {0}", totPv);
      return totPv;
    }

    /// <summary>
    ///   Calculate average survival probability
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The result is the weighted average of the survival probabilities by names.</para>
    /// </remarks>
    ///
    /// <returns>Average survival probability at the maturity date</returns>
    ///
    public double
    IntrinsicSurvivalProbability()
    {
      logger.Debug("Calculating intrinsic survival of LCDX Note...");

      ICDSPricer pricer = GetCDSPricer(AsOf, Settle);
      Double_Pricer_Fn cdsEvalFn =
        DoublePricerFnBuilder.CreateDelegate(pricer.GetType(), "SurvivalProbability");

      double survival = EvaluateAdditive(pricer, cdsEvalFn);

      logger.DebugFormat("Returning index intrinsic survival {0}", survival);
      return survival;
    }

    /// <summary>
    ///   Calculate the present value (full price * Notional) of the cash flow stream
    /// </summary>
    ///
    /// <remarks>
    ///   <para>By definition, the present value of the Note is the present value
    ///   of a default swap paying a premium of the note and priced of a flat
    ///   CDS curve of the original index issue premium.</para>
    /// </remarks>
    ///
    /// <returns>Present value to the settlement date of the cashflow stream</returns>
    ///
    public override double
    ProductPv()
    {
      return IntrinsicValue();
    }

    /// <summary>
    ///   Calculate the full price (percentage of Notional) of the cash flow stream
    /// </summary>
    ///
    /// <returns>Present value to the settlement date of the cashflow stream as a percentage of Notional</returns>
    ///
    public double
    FullPrice()
    {
      return IntrinsicValue() / Notional;
    }

    /// <summary>
    ///   Calculate the implied basis for the LCDX Note
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculate the implied basis for the LCDX Note by solving for the spread we
    ///   need to shift each underlying credit curve so that the implied Fair Value
    ///   equals the market value.</para>
    ///
    ///   <para>Solves for the spread to add to each of the underlying credits so that the
    ///   fair value matches the market value of the index.</para>
    /// </remarks>
    ///
    /// <param name="marketValue">Current market value of LCDX Note in dollars including accrued</param>
    ///
    /// <returns>Implied basis for the LCDX Note</returns>
    ///
    public double
    Basis(double marketValue)
    {
      logger.Debug("Calculating basis of CDS Index...");
      if (survivalCurves_ == null)
        throw new ArgumentException("LCDX Relative value calcs require SurvivalCurves to be set");

      // Set up root finder
      //
      Brent rf = new Brent();
      rf.setToleranceX(BasisFactorCalc.ToleranceX);
      rf.setToleranceF(BasisFactorCalc.ToleranceF);

      fn_ = new Double_Double_Fn(this.EvaluateBasis);
      solveFn_ = new DelegateSolverFn(fn_, null);

      // construct a set of curves to bump
      SurvivalCurve[] bumpedSurvivalCurves = new SurvivalCurve[survivalCurves_.Length];
      for (int i = 0; i < survivalCurves_.Length; i++)
        bumpedSurvivalCurves[i] = (SurvivalCurve)survivalCurves_[i].Clone();

      // Solve
      savedCurves_ = survivalCurves_;
      survivalCurves_ = bumpedSurvivalCurves;
      double res;
      try
      {
        res = rf.solve(solveFn_, marketValue, -0.0001, 0.001);
      }
      finally
      {
        // Make sure we restore the original survival curves, even on exception.
        survivalCurves_ = savedCurves_;

        // Tidy up transient data
        fn_ = null;
        solveFn_ = null;
      }

      return res;
    }


    //
    // Function for root find evaluation.
    // Called by root find to find fair value of index given curve spread.
    //
    private double
    EvaluateBasis(double x, out string exceptDesc)
    {
      double fv = 0.0;
      exceptDesc = null;

      logger.DebugFormat("Trying factor {0}", x);

      try
      {
        // Bump up curves
        CurveUtil.CurveBump(survivalCurves_, (string[])null, new double[] { x }, true, false, true, null);
        // Calculate fair value
        fv = IntrinsicValue(true);
        // Restore curves
        //CurveUtil.CurveBump(survivalCurves_, (string[])null, new double[] { x }, false, false, false, null);
        CurveUtil.CurveRestoreQuotes(survivalCurves_, savedCurves_);

        // Return results scaled to percent of notional
        logger.DebugFormat("Returning index fair value {0} for factor {1}", fv, x);
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }

      return fv;
    }

    /// <summary>
    ///   Calculate the implied scaling factor which matches the specified market value.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculate the implied scale for the LCDX Note by solving for the ratio we
    ///   need to shift each underlying credit curve so that the implied Fair Value
    ///   equals the market value.</para>
    ///
    ///   <para>Solves for the ratio to scale each of the underlying credits so that the
    ///   fair value matches the market value of the index.</para>
    /// </remarks>
    ///
    /// <param name="marketValue">Current market value of CDS Index in dollars including accrued</param>
    ///
    /// <returns>Implied scale for the CDS Index</returns>
    ///
    public double
    Factor(
          double marketValue
          )
    {
      logger.DebugFormat("Calculating basis factor of CDS Index which matches a market value of {0}...", marketValue);
      return BasisFactor(null, null, null, marketValue, null);
    }

    /// <summary>
    ///   Calculate the implied scale for the LCDX Note by solving for the ratio we
    ///   need to shift each underlying credit curve so that the implied Fair Value
    ///   equals the market value.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Solves for the ratio to scale each of the underlying credits so that the
    ///   fair value matches the market value of the index.</para>
    /// </remarks>
    ///
    /// <param name="tenorNamesScaled">List of tenors already scaled</param>
    /// <param name="scalingFactors">Scaling factors for each tenor</param>
    /// <param name="tenorNamesToScale">List of tenor names to scale</param>
    /// <param name="marketValue">Current market value of CDS Index in dollars including accrued</param>
    /// <param name="scalingWeights">Array of scaling weights for each curve</param>
    ///
    /// <returns>Implied scale for the CDS Index</returns>
    ///
    public double
    BasisFactor(
               string[] tenorNamesScaled,
               double[] scalingFactors,
               string[] tenorNamesToScale,
               double marketValue,
               double[] scalingWeights
               )
    {
      logger.DebugFormat("Calculating basis factor of CDS Index which matches a market value of {0}...", marketValue);
      if (survivalCurves_ == null)
        throw new ArgumentException("LCDX Relative value calcs require SurvivalCurves to be set");

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
      double res = BasisFactorCalc.Solve(this, survivalCurves_, tenorNamesScaled,
        scalingFactors, tenorNamesToScale, marketValue, scalingWeights);

      logger.DebugFormat("Returning basis factor of {0}", res);

      return res;
    }

    /// <summary>
    ///   Calculate the implied scaling factors for a set of LCDX Notes by solving for the factors
    ///   needed to scale each underlying credit curve so that the underlying credit curves are
    ///   consistent with the market quotes for the LCDX Notes.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>LCDX Notes typically trade at a spread or basis relative to a portfolio of
    ///   replicating LCDS. This is due to a number of factors including differences in terms
    ///   between the LCDX and LCDS, liquidity and market segmentation.</para>
    ///
    ///   <para>When pricing credit products on the Indices such as the liquid
    ///   tranches, market best practice is to scale the underlying LCDS quotes to be
    ///   consistent with where the LCDX trade.</para>
    ///
    ///   <para>This function provides several alternate ways of scaling the underlying CDS
    ///   to be be consistent with a term structure of LCDX quotes.</para>
    ///
    ///   <para>Each of the tenors specified by <paramref name="tenors"/> for each for each of
    ///   the underlying survival curves in <paramref name="survivalCurves"/> are scaled in
    ///   sequence.</para>
    ///
    ///   <para>For each tenor, the <paramref name="scalingMethods"/> dictate how the scaling
    ///   is performed.</para>
    /// </remarks>
    ///
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="lcdx">List of LCDX to base scaling on</param>
    /// <param name="tenors">List of tenors to scale</param>
    /// <param name="quotes">Market price quotes for each LCDX</param>
    /// <param name="scalingMethods">Scaling method for each tenor</param>
    /// <param name="overrideFactors">Specific override factors for each tenor</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="scalingWeights">Array of scaling weights for each curve</param>
    ///
    /// <returns>Implied scaling factor for the LCDX Note</returns>
    ///
    public static double[]
    Scaling(
           Dt asOf,
           Dt settle,
           LCDX[] lcdx,
           string[] tenors,
           double[] quotes,
           CDXScalingMethod[] scalingMethods,
           double[] overrideFactors,
           DiscountCurve discountCurve,
           SurvivalCurve[] survivalCurves,
           double[] scalingWeights
           )
    {
      // Validate
      if (tenors.Length != lcdx.Length ||
           tenors.Length != quotes.Length ||
           tenors.Length != scalingMethods.Length)
        throw new ArgumentException("Number of tenors, LCDX, quoted spreads, and scaling methods must be the same");
      if (scalingWeights != null && scalingWeights.Length > 0 && scalingWeights.Length != survivalCurves.Length)
        throw new ArgumentException("Number of scaling weights must match number of survival curves");

      // Want weights, if provided, to sum to one.
      if (scalingWeights == null || scalingWeights.Length == 0)
      {
        scalingWeights = new double[survivalCurves.Length];
        for (int i = 0; i < scalingWeights.Length; ++i)
          scalingWeights[i] = 1.0;
      }


      double[] equivSpreads = new double[quotes.Length];
      for (int i = 0; i < quotes.Length; i++)
      {
        // need a CDXPricer to convert prices to spreads
        if (quotes[i] > 0.0 && (scalingMethods[i] == CDXScalingMethod.Spread
          || scalingMethods[i] == CDXScalingMethod.Duration))
        {
          LCDXPricer lcdxPricer = new LCDXPricer(lcdx[i], asOf, settle, discountCurve, survivalCurves, quotes[i]);
          // Quotes as prices are CLEAN, so need to add Accrued() to the implied clean value to get the right spread
          equivSpreads[i] = lcdxPricer.PriceToSpread(quotes[i]);
        }
        else
        {
          equivSpreads[i] = 0.0;
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
            (quotes[tenor] <= 0.0 &&
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
              (quotes[endTenor] <= 0.0 &&
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
              // Calculate average CDS matching index
              double sumWeights = 0.0;
              double sumTenorWeights = 0.0;
              double averageSpread = 0.0;
              double[] cdxWeights = lcdx[tenor].Weights;
              for (int i = 0; i < survivalCurves.Length; i++)
              {
                double tenorWeight = (cdxWeights == null ? 1.0 : cdxWeights[i]) * scalingWeights[i];
                averageSpread += tenorWeight *
                  CurveUtil.ImpliedSpread(survivalCurves[i], lcdx[tenor].Maturity, lcdx[tenor].DayCount,
                  lcdx[tenor].Freq, lcdx[tenor].BDConvention, lcdx[tenor].Calendar);
                sumTenorWeights += tenorWeight;
                sumWeights += scalingWeights[i];
              }
              averageSpread /= sumTenorWeights;
              if (Math.Abs(sumWeights) < 10.0 * Double.Epsilon)
                throw new ArgumentException("Scaling weights must not sum to zero.");

              logger.DebugFormat("Average spread is {0}", averageSpread);

              // Factor is just ration of average duration to market quoted spread.
              if (equivSpreads[tenor] > averageSpread)
                factor = equivSpreads[tenor] / averageSpread - 1.0;
              else
                factor = 1.0 - averageSpread / equivSpreads[tenor];
              factor *= ((double)(survivalCurves.Length) / sumWeights);
              break;
            }
          case CDXScalingMethod.Duration:
          case CDXScalingMethod.Model:
            {
              // Group info for this scaling
              //-
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

              // Create LCDX pricer for scaling function
              LCDXPricer pricer = new LCDXPricer(lcdx[tenor], asOf, settle, discountCurve, survivalCurves, 0.0);
              pricer.Notional = 1.0;

              // Scale
              double targetValue = equivSpreads[tenor];
              if (scalingMethods[tenor] == CDXScalingMethod.Model)
              {
                targetValue = pricer.MarketValue(quotes[tenor]);
              }
              logger.DebugFormat("Scaling tenor {0} to spread {1}, market value {2}", tenors[tenor], equivSpreads[tenor], targetValue);
              logger.DebugFormat("Tenors scaled:");

              for (int i = 0; i < tenorNamesScaled.Length; i++)
                logger.DebugFormat("{0} factor {1}", tenorNamesScaled[i], scalingFactors[i]);

              logger.DebugFormat("Tenors to scale:");

              for (int i = 0; i < tenorNamesToScale.Length; i++)
                logger.DebugFormat("{0}", tenorNamesToScale[i]);

              if (scalingMethods[tenor] == CDXScalingMethod.Model)
                factor = BasisFactorCalc.Solve(pricer, survivalCurves, tenorNamesScaled,
                  scalingFactors, tenorNamesToScale, targetValue, scalingWeights);
              else
                factor = DurationFactorCalc.Solve(lcdx[tenor], survivalCurves, tenorNamesScaled,
                  scalingFactors, tenorNamesToScale, targetValue, scalingWeights);
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
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      if (marketQuote_ < 0.0 || marketQuote_ > 200.0)
        InvalidValue.AddError(errors, this, "MarketQuote", String.Format("Invalid market quote {0}. Must be >= 0 and <= 200", marketQuote_));


      return;
    }


    /// <summary>
    ///   Create equivalent CDS Pricer for LCDX market standard calculations
    /// </summary>
    ///
    /// <param name="marketQuote">Market quote for LCDX</param>
    /// <param name="quoteIsSpread">True if quote is spread; false if quote is price</param>
    ///
    /// <returns>CDSCashflowPricer for LCDX</returns>
    private CDSCashflowPricer
    GetEquivalentCDSPricer(double marketQuote, bool quoteIsSpread)
    {
      LCDX note = this.LCDX;
      SurvivalCurve survivalCurve;
      if (double.IsNaN(marketQuote))
        throw new ArgumentException("LCDX Market calcs require market quote to be set");
      else if (marketQuote < 0.0)
        throw new ArgumentException("LCDX market quote cannot be negative: " + marketQuote);

      // Manually find the first premium date after the settle.
      // Note: Cannot rely on SurvivalCurve.AddCDS() to determine the first premium date.
      //       For example, when effective is 27/3/2007 and settle is 22/5/2007,
      //       SurvivalCurve.AddCDS() gives the first premium on 20/9/2007 instead of 20/6/2007.  HJ 21May07
      Dt firstPrem = note.FirstPrem;
      Dt maturity = note.Maturity;

      // Calibration settle is actually the protection start date.
      // For forward lcdx, protection start with the effective
      Dt settle = Settle;
      if (Dt.Cmp(settle, note.Effective) <= 0)
      {
        settle = note.Effective;
      }
      else
      {
        while (Dt.Cmp(firstPrem, settle) <= 0)
          firstPrem = Dt.CDSRoll(firstPrem, false);
        if (Dt.Cmp(firstPrem, maturity) > 0)
          firstPrem = maturity;
      }

      SurvivalCalibrator calibrator = new SurvivalFitCalibrator(AsOf, settle, MarketRecoveryRate, DiscountCurve);
      survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.Ccy = note.Ccy;
      double spread = quoteIsSpread ? marketQuote : note.Premium;
      double fee = quoteIsSpread ? 0.0 : (-marketQuote + 1.0);
      survivalCurve.AddCDS("None", note.Maturity, fee, firstPrem, spread,
        note.DayCount, note.Freq, note.BDConvention, note.Calendar);
      survivalCurve.Fit();

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

      CDSCashflowPricer pricer = new CDSCashflowPricer(cds, AsOf, Settle, DiscountCurve, ReferenceCurve, survivalCurve, 0, TimeUnit.None); ;
      pricer.Notional = EffectiveNotional;

      return pricer;
    }

    /// <summary>
    ///   Create equivalent CDS Pricer for LCDX market standard calculations
    /// </summary>
    ///
    /// <param name="marketQuote">Market quote for LCDX</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <returns>CDSCashflowPricer for LCDX</returns>
    ///
    private CDSCashflowPricer
    GetEquivalentCDSPricer(double marketQuote, SurvivalCurve survivalCurve)
    {
      LCDX note = this.LCDX;
      if (double.IsNaN(marketQuote))
        throw new ArgumentException("LCDX Market calcs require market quote to be set");
      else if (marketQuote < 0.0)
        throw new ArgumentException("LCDX market quote cannot be negative: " + marketQuote);

      // Manually find the first premium date after the settle.
      // Note: Cannot rely on SurvivalCurve.AddCDS() to determine the first premium date.
      //       For example, when effective is 27/3/2007 and settle is 22/5/2007,
      //       SurvivalCurve.AddCDS() gives the first premium on 20/9/2007 instead of 20/6/2007.  HJ 21May07
      Dt firstPrem = note.FirstPrem;
      Dt maturity = note.Maturity;

      // Calibration settle is actually the protection start date.
      // For forward lcdx, protection start with the effective
      Dt settle = Settle;
      if (Dt.Cmp(settle, note.Effective) <= 0)
      {
        settle = note.Effective;
      }
      else
      {
        while (Dt.Cmp(firstPrem, settle) <= 0)
          firstPrem = Dt.CDSRoll(firstPrem, false);
        if (Dt.Cmp(firstPrem, maturity) > 0)
          firstPrem = maturity;
      }

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
      if (survivalCurve != null && survivalCurve.SurvivalCalibrator!=null)
        survivalCurve.SurvivalCalibrator.CounterpartyCurve = null;
      CDSCashflowPricer pricer = new CDSCashflowPricer(cds, AsOf, Settle, DiscountCurve, ReferenceCurve, survivalCurve, 0, TimeUnit.None); ;
      pricer.Notional = EffectiveNotional;

      return pricer;
    }

    /// <summary>
    ///   Create equivalent LCDS Curve (calibrated to LCDS spreads and refi probs)
    /// </summary>
    ///
    /// <param name="marketQuote">Market quote for LCDX</param>
    /// <param name="refinancingCurve">Market Refinancing Curve for LCDX</param>
    /// <param name="correlation">Market refinancing correlation for LCDX</param>
    ///
    /// <returns>Equivalent LCDS Curve</returns>
    ///
    private SurvivalCurve
    GetEquivalentLCDSCurve(double marketQuote, SurvivalCurve refinancingCurve, double correlation)
    {
      LCDX note = this.LCDX;
      SurvivalCurve survivalCurve;
      if (double.IsNaN(marketQuote))
        throw new ArgumentException("LCDX Market calcs require market quote to be set");
      else if (marketQuote < 0.0)
        throw new ArgumentException("LCDX market quote cannot be negative: " + marketQuote);

      // Manually find the first premium date after the settle.
      // Note: Cannot rely on SurvivalCurve.AddCDS() to determine the first premium date.
      //       For example, when effective is 27/3/2007 and settle is 22/5/2007,
      //       SurvivalCurve.AddCDS() gives the first premium on 20/9/2007 instead of 20/6/2007.  HJ 21May07
      Dt firstPrem = note.FirstPrem;
      Dt maturity = note.Maturity;

      // Calibration settle is actually the protection start date.
      // For forward lcdx, protection start with the effective
      Dt settle = Settle;
      if (Dt.Cmp(settle, note.Effective) <= 0)
      {
        settle = note.Effective;
      }
      else
      {
        while (Dt.Cmp(firstPrem, settle) <= 0)
          firstPrem = Dt.CDSRoll(firstPrem, false);
        if (Dt.Cmp(firstPrem, maturity) > 0)
          firstPrem = maturity;
      }

      SurvivalCalibrator calibrator = new SurvivalFitCalibrator(AsOf, settle, MarketRecoveryRate, DiscountCurve);
      calibrator.CounterpartyCurve = refinancingCurve;
      calibrator.CounterpartyCorrelation = correlation;
      survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.Ccy = note.Ccy;
      survivalCurve.AddCDS("None", note.Maturity, -marketQuote + 1.0,
        firstPrem, note.Premium, note.DayCount, note.Freq, note.BDConvention, note.Calendar);
      survivalCurve.Fit();

      return survivalCurve;
    }



    /// <summary>
    ///   Calculate adjusted (Equivalent CDS) LCDX market quote
    /// </summary>
    ///
    /// <param name="marketQuote">Market quote for LCDX</param>
    ///
    /// <returns>Adjusted (Equivalent CDS) Market Quote (1 = par)</returns>
    ///
    public double
    EquivalentCDSMarketQuote(double marketQuote)
    {
      LCDX note = this.LCDX;
      double meanCorrelation = 0;
      double durationWeightedRefiRate = 0;
      double durationSum = 0;

      // add duration weighted refi rate (extract refi at 1Y tenor) to the EquivalentCDSPricer
      for (int i = 0; i < SurvivalCurves.Length; ++i)
      {
        double duration = CurveUtil.ImpliedDuration(SurvivalCurves[i], note.Maturity, note.DayCount,
                                                    note.Freq, note.BDConvention, note.Calendar);
        durationSum += duration;
        meanCorrelation += SurvivalCurves[i].SurvivalCalibrator.CounterpartyCorrelation / SurvivalCurves.Length;

        double refinancingRate = 0;
        SurvivalCalibrator calibrator = SurvivalCurves[i].SurvivalCalibrator;
        if (calibrator is SurvivalFitCalibrator)
        {
          SurvivalFitCalibrator cal = (SurvivalFitCalibrator)calibrator.Clone();
          refinancingRate = cal.CounterpartyCurve.DefaultProb(Dt.Add(AsOf, 1, TimeUnit.Years));
        }
        durationWeightedRefiRate += refinancingRate * duration;
      }
      durationWeightedRefiRate /= durationSum;

      // construct refi curve w single annual refinancing probability
      Dt[] tenorDates = new Dt[] { Dt.Add(AsOf, 1, TimeUnit.Years) };
      string[] tenorNames = new string[] { "1Y" };
      double[] nonRefiProbs = new double[] { 1.0 - durationWeightedRefiRate };
      SurvivalCurve refinancingCurve = SurvivalCurve.FromProbabilitiesWithBond(AsOf, Currency.None, null, InterpMethod.Weighted,
        ExtrapMethod.Const, tenorDates, nonRefiProbs, tenorNames, null, null, null, 0);

      // calculate equivalent/adjusted LCDX quote
      SurvivalCurve sc = GetEquivalentLCDSCurve(marketQuote, refinancingCurve, meanCorrelation);
      CDSCashflowPricer equivalentCDSPricer = GetEquivalentCDSPricer(marketQuote, sc);
      double adjustedLCDSSpread = equivalentCDSPricer.BreakEvenPremium();
      double adjustedUpfront = (equivalentCDSPricer.CDS.Premium - adjustedLCDSSpread) * equivalentCDSPricer.RiskyDuration();
      double adjustedMarketQuote = (adjustedUpfront + 1);

      return adjustedMarketQuote;
    }
 
    /// <summary>
    ///   Check if the survival curves contain any refinance info
    /// </summary>
    /// <param name="curves">survival curves</param>
    /// <returns>True if contain refianace info</returns>
    public static bool HasPrepaymentCurves(SurvivalCurve[] curves)
    {
      foreach (SurvivalCurve curve in curves)
      {
        if (curve.SurvivalCalibrator != null && curve.SurvivalCalibrator.CounterpartyCurve != null)
          return true;
      }
      return false;
    }

    /// <summary>
    ///   Get refinance curves and correlations from survivalCurves
    /// </summary>
    private void GetPrepaymentInfosFromSurvivalCurves()
    {
      SurvivalCurve[] refinanceCurves = null;
      double[] refinanceCorrelations = null;
      SurvivalCurve[] survivalCurves = this.SurvivalCurves;
      if (HasPrepaymentCurves(survivalCurves))
      {
        int N = survivalCurves.Length;
        refinanceCurves = new SurvivalCurve[N];
        refinanceCorrelations = new double[N];
        for (int i = 0; i < N; ++i)
        {
          SurvivalCurve curve = survivalCurves[i];
          if (curve.SurvivalCalibrator != null)
          {
            SurvivalCalibrator calibrator = curve.SurvivalCalibrator;
            refinanceCurves[i] = calibrator.CounterpartyCurve;
            refinanceCorrelations[i] = calibrator.CounterpartyCorrelation;
          }
        }
      }
      this.PrepaymentCurves = refinanceCurves;
      this.PrepaymentCorrelations = refinanceCorrelations;
      return;
    }

    /// <summary>
    ///   Reset the pricers internal caches
    /// </summary>
    ///
    /// <remarks>
    ///   <para>There are some pricers which need to remember some internal state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that this internate state should be cleared.</para>
    /// </remarks>
    ///
    public override void Reset()
    {
      cashflow_ = null;
      equivalentCDSPricer_ = null;
    }

    #endregion UtilityMethods

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
          if (survivalCurves_[i].Calibrator != null)
            recoveryCurves[i] = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
          else
            throw new ArgumentException(String.Format("Must specify recoveries as curve {0} does not have recoveries from calibration", survivalCurves_[i].Name));
        }
        return recoveryCurves;
      }
    }

    /// <summary>
    ///   Current market quote (price where 1=par)
    /// </summary>
    public double MarketQuote
    {
      get { return marketQuote_; }
      set
      {
        marketQuote_ = value;
        Reset();
      }
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
    public LCDX LCDX
    {
      get { return (LCDX)Product; }
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
          survivalCurves_, LCDX.Weights, LCDX.AnnexDate) * Notional;
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
          survivalCurves_, LCDX.Weights) * Notional;
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
        return double.NaN;
      }
    }

    /// <summary>
    ///   Create equivalent CDS Pricer for LCDX market standard calculations
    /// </summary>
    public CDSCashflowPricer EquivalentCDSPricer
    {
      get
      {
        if (equivalentCDSPricer_ == null)
          equivalentCDSPricer_ = GetEquivalentCDSPricer(MarketQuote, false);
        return equivalentCDSPricer_;
      }
    }

    /// <summary>
    ///   The market-implied survival curve for the LCDX
    /// </summary>
    /// <exclude />
    public SurvivalCurve MarketSurvivalCurve
    {
      get { return EquivalentCDSPricer.SurvivalCurve; }
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
    ///   Prepayment probability curves
    /// </summary>
    /// <exclude />
    public SurvivalCurve[] PrepaymentCurves
    {
      get { return prepaymentCurves_; }
      set { prepaymentCurves_ = value; }
    }

    /// <summary>
    ///   Correlations between prepayment and default
    /// </summary>
    /// <exclude />
    public double[] PrepaymentCorrelations
    {
      get { return prepaymentCorrelations_; }
      set { prepaymentCorrelations_ = value; }
    }

    /// <summary>
    /// Reference Curve used for floating payments forecasts
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set { referenceCurve_ = value; }
    }

    #endregion Properties

    #region Solver

    // Transient solver-related info. Here because of subtle issues re persistance of unmanaged delegates. RTD
    DelegateSolverFn solveFn_;
    Double_Double_Fn fn_;
    SurvivalCurve[] savedCurves_;

    #endregion

    #region Helpers

    /// <summary>
    ///   Duration factor calculator
    /// </summary>
    public class DurationFactorCalc : SolverFn
    {
      /// <summary>
      ///   Constructor
      /// </summary>
      public DurationFactorCalc(
          SurvivalCurve[] survivalCurves,
          string[] tenorNamesScaled,
          double[] scalingFactors,
          string[] tenorNamesToScale,
          double[] scalingWeights,
          LCDX lcdx)
      {
        checkWeights(scalingWeights);

        // construct a set of curves to bump
        CalibratedCurve[] bumpedSurvivalCurves = CloneUtil.Clone(survivalCurves);

        // scale the curves for the tenors we already know the factors
        if ((null != scalingFactors) && (scalingFactors.Length > 0))
        {
          CurveUtil.CurveBump(bumpedSurvivalCurves, tenorNamesScaled, scalingFactors, true, true, true, scalingWeights);
          this.savedCurves_ = bumpedSurvivalCurves;
          this.survivalCurves_ = CloneUtil.Clone(bumpedSurvivalCurves);
        }
        else
        {
          this.savedCurves_ = survivalCurves;
          this.survivalCurves_ = bumpedSurvivalCurves;
        }

        // set the data members
        this.tenorNamesToScale_ = tenorNamesToScale;
        this.scalingWeights_ = scalingWeights;
        this.cdx_ = lcdx;
      }

#if NEW_DURATION_SOLVER
      /// <summary>
      ///   Solve for a scaling factor
      /// </summary>
      /// <returns>evaluated objective function f(x)</returns>
      public static double
      Solve(
          LCDX cdx,
          SurvivalCurve[] survivalCurves,
          string[] tenorNamesScaled,
          double[] scalingFactors,
          string[] tenorNamesToScale,
          double targetSpread,
          double[] scalingWeights)
      {
        // tolerance in spread error (0.1 basis point)
        double tolerance = ToleranceF;

        // maximum iterations
        int maxIter = MaxIterations;

        // create solver function
        DurationFactor fn = new DurationFactor(survivalCurves,
          tenorNamesScaled, scalingFactors, tenorNamesToScale, scalingWeights, cdx);

        // the duration weighted spead based on unscaled curves
        double spread = fn.evaluate(0.0);

        // initial scaling ratio (1.0 means no scaling)
        double ratio = 1.0;
        double factor = 0.0;

        // iteration loop
        for (int iter = 0; iter < maxIter; ++iter)
        {
          // calculate the scaling factor
          factor = (ratio >= 1 ? ratio - 1.0 : 1.0 - 1 / ratio);

          // calculate the duration weighted spread based on the scaled curves
          spread = fn.evaluate(factor);

          // check convergence
          if (Math.Abs(targetSpread - spread) < tolerance)
            return factor;

          // spread should not be too close to zero
          if (Math.Abs(spread) < 1E-9)
          {
            logger.InfoFormat("Duration weighted spread ({0}) too small", spread);
            break;
          }

          // calculate the new ratio
          ratio *= targetSpread / spread;
        }

        // fail to converge, log a message so the user can check it
        logger.InfoFormat("Solver fails, the best factor is {0} with duration weighted spread {1}",
            factor, spread);

        return factor;
      }

#else
      /// <summary>
      ///   Solve for a scaling factor
      /// </summary>
      /// <returns>evaluated objective function f(x)</returns>
      public static double
      Solve(
          LCDX lcdx,
          SurvivalCurve[] survivalCurves,
          string[] tenorNamesScaled,
          double[] scalingFactors,
          string[] tenorNamesToScale,
          double targetSpread,
          double[] scalingWeights)
      {
        checkWeights(scalingWeights);

        // Set up root finder
        //
        Brent rf = new Brent();
        rf.setToleranceX(ToleranceX);
        rf.setToleranceF(ToleranceF);
        if (MaxIterations > 0)
          rf.setMaxIterations(MaxIterations);

        // Solve
        DurationFactorCalc fn = new DurationFactorCalc(survivalCurves,
          tenorNamesScaled, scalingFactors, tenorNamesToScale, scalingWeights, lcdx);

        // Solve
        double res;
        try
        {
          res = rf.solve(fn, targetSpread, -0.2, 0.2);
        }
        catch (Exception e)
        {
          // When the solver fails, we check the accuracy of 8.1 solution
          // and fall back to it.

          // Need to reconstruct the function object since it might
          // be left in some weird state by the exception.
          fn = new DurationFactorCalc(survivalCurves,
            tenorNamesScaled, scalingFactors, tenorNamesToScale, scalingWeights, lcdx);

          // calculate the duration weighted spread based on the unscaled curves
          double spread = fn.evaluate(0.0);

          // calculate the scaling factor by our old, simple, not so consistent method
          if (targetSpread > spread)
            res = targetSpread / spread - 1.0;
          else
            res = 1.0 - spread / targetSpread;

          // calculate the duration weighted spread based on the scaled curves
          spread = fn.evaluate(res);

          // log an info message so the user can check it
          logger.InfoFormat(e.Message);

          // check the fall back tolerance
          if (Math.Abs(spread - targetSpread) >= FallBackTolerance)
            logger.InfoFormat("Using factor {0} with a duration spread {1} outside the tolerance {2}",
              res, spread, FallBackTolerance);
          else
            logger.InfoFormat("Using factor {0} with a duration spread {1} within the tolerance {2}",
              res, spread, FallBackTolerance);
        }

        return res;
      }
#endif

      /// <summary>
      ///  Compute duration weighted spread with x as the scaling factor
      /// </summary>
      /// <returns>evaluated objective function f(x)</returns>
      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying factor {0}", x);

        LCDX cdx = cdx_;
        CalibratedCurve[] survivalCurves = survivalCurves_;
        string[] tenorsToScale = tenorNamesToScale_;
        double[] scalingWeights = scalingWeights_;

        // Bump up curves
        CurveUtil.CurveBump(survivalCurves, tenorsToScale, new double[] { x }, true, true, true, scalingWeights);

        // Calculate average CDS scaling weights
        double weightedSpread = 0.0;
        double durationSum = 0.0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          CDSCashflowPricer pricer = CurveUtil.ImpliedPricer(
            (SurvivalCurve)survivalCurves[i], cdx.Maturity, cdx.DayCount, cdx.Freq, cdx.BDConvention, cdx.Calendar);

          // The documentation says cdx.Weights may be null for equal weights
          double weight = cdx.Weights == null ? 1.0 : cdx.Weights[i];

          double duration = pricer.RiskyDuration();
          double spread = pricer.BreakEvenPremium();
          weightedSpread += duration * spread * weight;
          durationSum += duration * weight;
        }
        weightedSpread /= durationSum;

        // Restore curves
        //CurveUtil.CurveBump(survivalCurves, tenorsToScale, new double[] { x }, false, true, false, scalingWeights_);
        CurveUtil.CurveRestoreQuotes(survivalCurves_, savedCurves_);

        // Return results scaled to percent of notional
        logger.DebugFormat("Returning index fair value {0} for factor {1}", weightedSpread, x);

        return weightedSpread;
      }

      private static void checkWeights(double[] scalingWieghts)
      {
        double sumWeights = 0.0;
        for (int i = 0; i < scalingWieghts.Length; i++)
          sumWeights += scalingWieghts[i];

        if (Math.Abs(sumWeights) < 10.0 * Double.Epsilon)
          throw new ArgumentException("Scaling weights must not sum to zero.");
      }

      private readonly CalibratedCurve[] survivalCurves_;
      private readonly CalibratedCurve[] savedCurves_;
      private readonly string[] tenorNamesToScale_;
      private readonly double[] scalingWeights_;
      private readonly LCDX cdx_;

      /// <summary>
      ///   Tolerance of factor error
      /// </summary>
      public static double ToleranceX = 1E-5;

      /// <summary>
      ///   Tolerance of spread error
      /// </summary>
      public static double ToleranceF = 1E-6;

      /// <summary>
      ///   Maximum iterations allowed
      /// </summary>
      public static int MaxIterations = 20;

      /// <summary>
      ///   Spread error tolerance in fall back routine
      /// </summary>
      public static double FallBackTolerance = 0.0001;
    }; // class DurationFactorFn

    /// <summary>
    ///   Basis factor calculator
    /// </summary>
    public class BasisFactorCalc : SolverFn
    {
      /// <summary>
      ///   Constructor
      /// </summary>
      public BasisFactorCalc(
          SurvivalCurve[] survivalCurves,
          string[] tenorNamesScaled,
          double[] scalingFactors,
          string[] tenorNamesToScale,
          double[] scalingWeights,
          LCDXPricer pricer)
      {
        checkWeights(scalingWeights);

        // construct a set of curves to bump
        SurvivalCurve[] bumpedSurvivalCurves = new SurvivalCurve[survivalCurves.Length];
        for (int i = 0; i < survivalCurves.Length; i++)
          bumpedSurvivalCurves[i] = (SurvivalCurve)survivalCurves[i].Clone();

        // scale the curves for the tenors we already know the factors
        if ((null != scalingFactors) && (scalingFactors.Length > 0))
        {
          CurveUtil.CurveBump(bumpedSurvivalCurves, tenorNamesScaled, scalingFactors, true, true, true, scalingWeights);
          this.savedCurves_ = CloneUtil.Clone(bumpedSurvivalCurves);
          this.survivalCurves_ = bumpedSurvivalCurves;
        }
        else
        {
          this.savedCurves_ = survivalCurves;
          this.survivalCurves_ = bumpedSurvivalCurves;
        }

        // set the data members
        this.tenorNamesToScale_ = tenorNamesToScale;
        this.scalingWeights_ = scalingWeights;
        this.pricer_ = pricer;
      }

      /// <summary>
      ///   Solve for a scaling factor
      /// </summary>
      /// <returns>evaluated objective function f(x)</returns>
      public static double
      Solve(
          LCDXPricer pricer,
          SurvivalCurve[] survivalCurves,
          string[] tenorNamesScaled,
          double[] scalingFactors,
          string[] tenorNamesToScale,
          double marketValue,
          double[] scalingWeights)
      {
        checkWeights(scalingWeights);

        // Set up root finder
        //
        Brent rf = new Brent();
        rf.setToleranceX(ToleranceX);
        rf.setToleranceF(ToleranceF);
        if (MaxIterations > 0)
          rf.setMaxIterations(MaxIterations);

        // remember original curves
        SurvivalCurve[] origCurves = pricer.SurvivalCurves;
        // Solve
        try
        {
          BasisFactorCalc fn = new BasisFactorCalc(survivalCurves,
            tenorNamesScaled, scalingFactors, tenorNamesToScale, scalingWeights, pricer);
          pricer.SurvivalCurves = fn.survivalCurves_;
          double res = rf.solve(fn, marketValue, -0.2, 0.2);
          return res;
        }
        finally
        {
          pricer.SurvivalCurves = origCurves;
        }

      }

      /// <summary>
      ///  Compute duration weighted spread with x as the scaling factor
      /// </summary>
      /// <returns>evaluated objective function f(x)</returns>
      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying factor {0}", x);

        LCDXPricer pricer = pricer_;
        CalibratedCurve[] survivalCurves = survivalCurves_;
        string[] tenorsToScale = tenorNamesToScale_;
        double[] scalingWeights = scalingWeights_;

        // Bump up curves
        CurveUtil.CurveBump(survivalCurves, tenorsToScale, new double[] { x }, true, true, true, scalingWeights);
        // Calculate full intrinsic value
        double fv = pricer.IntrinsicValue(true);
        // Restore curves
        //CurveUtil.CurveBump(survivalCurves, tenorsToScale, new double[] { x }, false, true, false, scalingWeights_);
        CurveUtil.CurveRestoreQuotes(survivalCurves_, savedCurves_);

        // Return results scaled to percent of notional
        logger.DebugFormat("Returning index fair value {0} for factor {1}", fv, x);

        return fv;
      }

      private static void checkWeights(double[] scalingWieghts)
      {
        double sumWeights = 0.0;
        for (int i = 0; i < scalingWieghts.Length; i++)
          sumWeights += scalingWieghts[i];

        if (Math.Abs(sumWeights) < 10.0 * Double.Epsilon)
          throw new ArgumentException("Scaling weights must not sum to zero.");
      }

      private readonly SurvivalCurve[] survivalCurves_;
      private readonly CalibratedCurve[] savedCurves_;
      private readonly string[] tenorNamesToScale_;
      private readonly double[] scalingWeights_;
      private readonly LCDXPricer pricer_;

      /// <summary>
      ///   Tolerance of factor error
      /// </summary>
      public static double ToleranceX = 1E-2;

      /// <summary>
      ///   Tolerance of spread error
      /// </summary>
      public static double ToleranceF = 1E-6;

      /// <summary>
      ///   Maximum iterations allowed
      /// </summary>
      public static int MaxIterations = 0;
    }; // class DurationFactorFn

    #endregion

    #region GenericCalculations

    /// <summary>
    ///   Fast calculation of parallelly bumped values
    /// </summary>
    /// <param name="altSurvivalCurves">Bumped survival curves</param>
    /// <returns>Array of bumped values</returns>
    public double[] BumpedIntrinsicValue(SurvivalCurve[] altSurvivalCurves)
    {
      logger.Debug("Calculating bumped intrinsic values of LCDX Note...");

      ICDSPricer pricer = GetCDSPricer(AsOf, Settle);
      pricer.Notional = this.Notional;
      Double_Pricer_Fn cdsEvalFn =
        DoublePricerFnBuilder.CreateDelegate(pricer.GetType(), "Pv");
      double[] result = EvaluateAdditive(pricer, cdsEvalFn, altSurvivalCurves);

      return result;
    }

    /// <summary>
    ///   Fast calculation of parallelly bumped values
    /// </summary>
    /// <param name="altSurvivalCurves">Bumped survival curves</param>
    /// <returns>Array of bumped values</returns>
    public double[] BumpedPv(SurvivalCurve[] altSurvivalCurves)
    {
      return BumpedIntrinsicValue(altSurvivalCurves);
    }

    /// <summary>
    ///   Fast calculation of parallelly bumped values
    /// </summary>
    /// <param name="altSurvivalCurves">Bumped survival curves</param>
    /// <returns>Array of bumped values</returns>
    public double[] BumpedRiskyDuration(SurvivalCurve[] altSurvivalCurves)
    {
      logger.Debug("Calculating bumped intrinsic values of LCDX Note...");

      ICDSPricer pricer = GetCDSPricer(AsOf, Settle);
      Double_Pricer_Fn cdsEvalFn =
        DoublePricerFnBuilder.CreateDelegate(pricer.GetType(), "RiskyDuration");
      double[] result = EvaluateAdditive(pricer, cdsEvalFn, altSurvivalCurves);

      return result;
    }

    /// <summary>
    ///   Evaluate the value of a additive price measure
    /// </summary>
    /// <param name="pricer">CDS Pricer</param>
    /// <param name="cdsEvalFn">Function to evaluate</param>
    /// <returns>Value of the price measure</returns>
    private double EvaluateAdditive(
      ICDSPricer pricer,
      Double_Pricer_Fn cdsEvalFn)
    {
      if (survivalCurves_ == null)
        throw new ArgumentException("LCDX Relative value calcs require SurvivalCurves to be set");
      if (prepaymentCurves_ != null)
      {
        if (prepaymentCurves_.Length != survivalCurves_.Length)
          throw new ArgumentException("Counterparty curves and survival curves not match");
        if (prepaymentCorrelations_ == null || prepaymentCorrelations_.Length != prepaymentCurves_.Length)
          throw new ArgumentException("Counterparty curves and correlations not match");
      }

      double[] weights = LCDX.Weights;

      // Price index components off each market curve
      double totPv = 0.0;
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        // Create pricer
        pricer.SurvivalCurve = survivalCurves_[i];
        pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        if (prepaymentCurves_ != null)
        {
          pricer.CounterpartyCurve = prepaymentCurves_[i];
          pricer.Correlation = prepaymentCorrelations_[i];
        }
        double pv;
        //because pricing relies on weights to be non zero, yet defaults that happen before/on annexation should be zero pv, the special case is JTD/VOD calculation with DaysToSettle = 0
        if (!survivalCurves_[i].DefaultDate.IsEmpty() && (LCDX.AnnexDate > survivalCurves_[i].DefaultDate
                                                          ||
                                                          (survivalCurves_[i].Defaulted != Defaulted.WillDefault &&
                                                           LCDX.AnnexDate == survivalCurves_[i].DefaultDate)))
        {
          pv = 0;
        }
        else
        {
          var counterparyCurve = pricer.CounterpartyCurve;
          try
          {
            if (pricer.SurvivalCurve != null && (pricer.SurvivalCurve.DefaultDate.IsValid() && pricer.SurvivalCurve.DefaultDate <= pricer.AsOf))
            {
              var defaultPaymentDate = pricer.RecoveryCurve != null
                ? pricer.RecoveryCurve.DefaultSettlementDate
                : Dt.Add(pricer.SurvivalCurve.SurvivalCalibrator.Settle, 1);
              // We have to temporarily do this becasue the CashflowModel.Pv will preprocess cross default probability between credit curve 
              // and counterparty curve based on cashflow dates and it will throw exception on historical dates.
              if (defaultPaymentDate <= pricer.Settle)
                pricer.CounterpartyCurve = null;
            }
            pv = cdsEvalFn(pricer);
          }
          finally
          {
            pricer.CounterpartyCurve = counterparyCurve;
          }
        }
        logger.DebugFormat("Calculated index component {0} value = {1}", survivalCurves_[i].Name, pv);
        totPv += pv * ((weights != null) ? weights[i] : (1.0 / survivalCurves_.Length));
      }
      return totPv;
    }

    private double[] EvaluateAdditive(
      ICDSPricer pricer,
      Double_Pricer_Fn cdsEvalFn,
      SurvivalCurve[] altSurvivalCurves)
    {
      if (survivalCurves_ == null)
        throw new ArgumentException("LCDX Relative value calcs require SurvivalCurves to be set");
      if (altSurvivalCurves == null || altSurvivalCurves.Length != survivalCurves_.Length)
        throw new ArgumentException("Alternative survival curves does not match LCDX SurvivalCurves");
      if (prepaymentCurves_ != null)
      {
        if (prepaymentCurves_.Length != survivalCurves_.Length)
          throw new ArgumentException("Counterparty curves and survival curves not match");
        if (prepaymentCorrelations_ == null || prepaymentCorrelations_.Length != prepaymentCurves_.Length)
          throw new ArgumentException("Counterparty curves and correlations not match");
      }

      double[] weights = LCDX.Weights;

      // Price index components off each market curve
      int N = survivalCurves_.Length;
      double[] pvs = new double[N + 1];
      double totPv = 0.0;
      for (int i = 0; i < N; i++)
      {
        // Create pricer
        pricer.SurvivalCurve = survivalCurves_[i];
        pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        if (prepaymentCurves_ != null)
        {
          pricer.CounterpartyCurve = prepaymentCurves_[i];
          pricer.Correlation = prepaymentCorrelations_[i];
        }
        double pv0 = cdsEvalFn(pricer);
        double w = weights != null ? weights[i] : (1.0 / survivalCurves_.Length);
        totPv += w * pv0;

        double pv1 = pv0;
        if (altSurvivalCurves[i] != null && altSurvivalCurves[i] != survivalCurves_[i])
        {
          pricer.SurvivalCurve = altSurvivalCurves[i];
          pricer.RecoveryCurve = altSurvivalCurves[i].SurvivalCalibrator.RecoveryCurve;
          pv1 = cdsEvalFn(pricer);
          pvs[i + 1] = w * (pv1 - pv0);
        }

        logger.DebugFormat("Calculated index component {0} value base = {1}, bumped = {2}",
          survivalCurves_[i].Name, pv0, pv1);
      }

      pvs[0] = totPv;
      for (int i = 1; i <= N; ++i)
      {
        pvs[i] += totPv;
      }

      return pvs;
    }

    private ICDSPricer GetCDSPricer(Dt asOf, Dt settle)
    {
      LCDX note = LCDX;
      CDS lcds = new CDS(note.Effective, note.Maturity, note.Ccy, note.FirstPrem,
        note.Premium, note.DayCount, note.Freq, note.BDConvention, note.Calendar);
      lcds.CopyScheduleParams(note);
      if (note.CdxType == CdxType.FundedFixed)
        lcds.CdsType = CdsType.FundedFixed;
      else if (note.CdxType == CdxType.FundedFloating)
        lcds.CdsType = CdsType.FundedFloating;
      else
        lcds.CdsType = CdsType.Unfunded;

      ICDSPricer pricer = new CDSCashflowPricer(lcds, asOf, settle, DiscountCurve, ReferenceCurve, null, 0, TimeUnit.None);

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
        throw new ArgumentException("LCDX Relative value calcs require SurvivalCurves to be set");
      if (prepaymentCurves_ != null)
      {
        if (prepaymentCurves_.Length != survivalCurves_.Length)
          throw new ArgumentException("Counterparty curves and survival curves not match");
        if (prepaymentCorrelations_ == null || prepaymentCorrelations_.Length != prepaymentCurves_.Length)
          throw new ArgumentException("Counterparty curves and correlations not match");
      }
      double[] weights = LCDX.Weights;
      if (survivalCurves_.Length == 1)
        weights = null;
      DerivativeCollection retVal = new DerivativeCollection(survivalCurves_.Length);
      ICDSPricer pricer = GetCDSPricer(AsOf, Settle);
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        pricer.SurvivalCurve = survivalCurves_[i];
        pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        if (prepaymentCurves_ != null)
        {
          pricer.CounterpartyCurve = prepaymentCurves_[i];
          pricer.Correlation = prepaymentCorrelations_[i];
        }
        double w = (weights != null) ? weights[i] : (1.0 / survivalCurves_.Length);
        var p = pricer as IAnalyticDerivativesProvider;
        if (p == null)
          throw new ToolkitException("Only Cashflow pricers implement analytic sensitivities for the time being");
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

    #region Data

    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private SurvivalCurve[] survivalCurves_;
    private double marketQuote_ = Double.NaN;
    private double marketRecoveryRate_ = 0.75;         // Market standard recovery rate
    private CDSCashflowPricer equivalentCDSPricer_;    // Matching CDS Pricer for Market calcs
    private SurvivalCurve[] prepaymentCurves_ = null;  // Prepayment probability
    private double[] prepaymentCorrelations_ = null;   // Correlation between prepayment and default
    private List<RateReset> rateResets_ = null;
    private bool adjustDurationForRemainingNotional_ = ToolkitConfigurator.Settings.LCDXPricer.AdjustDurationForRemainingNotional;
   #endregion Data

  } // class  LCDXPricer
}
