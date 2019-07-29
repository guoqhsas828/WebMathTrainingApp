/*
 * FTDPricer.cs
 *
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Numerics;
using PriceCalc = BaseEntity.Toolkit.Pricers.Baskets.PriceCalc;

namespace BaseEntity.Toolkit.Pricers
{

	/// <summary>
	/// <para>Price a <see cref="BaseEntity.Toolkit.Products.FTD">FTD</see> using the
	/// <see cref="BasketPricer">Basket Pricer Model</see>.</para>
	/// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.FTD" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.FTDModel" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.FTD">FTD Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.FTDModel">FTD Basket Model</seealso>
  [Serializable]
	public partial class FTDPricer : PricerBase, IPricer, IRatesLockable, IQuantoDefaultSwapPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(FTDPricer));

    #region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Nth to default swap product to price</param>
		/// <param name="basket">The basket model used to price</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		///
    public
    FTDPricer(
              FTD product,
              BasketForNtdPricer basket,
              DiscountCurve discountCurve
              )
      : this(product, basket, discountCurve, GetNotional(basket, product))
    {
    }

    private static double GetNotional(BasketForNtdPricer basket, FTD product)
    {
      return basket.TotalPrincipal / basket.Count * product.NumberCovered;
    }

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Nth to default swap product to price</param>
		/// <param name="basket">The basket model used to price</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		///
    [Obsolete("For backward compatibility only")]
    public
    FTDPricer(
              FTD product,
              BasketPricer basket,
              DiscountCurve discountCurve
              )
      : this(product, GetBasket(basket), discountCurve)
    {
    }

    private static BasketForNtdPricer GetBasket(BasketPricer basket)
    {
      if (basket is MonteCarloBasketPricer)
        return new MonteCarloBasketForNtdPricer(
          basket.AsOf,
          basket.Settle,
          basket.Maturity,
          basket.SurvivalCurves,
          basket.RecoveryCurves,
          basket.Principals,
          basket.Copula,
          (Correlation) basket.Correlation,
          basket.StepSize,
          basket.StepUnit) {SampleSize = basket.SampleSize};
      return new SemiAnalyticBasketForNtdPricer(basket.AsOf,
                                                basket.Settle,
                                                basket.Maturity,
                                                basket.SurvivalCurves,
                                                basket.RecoveryCurves,
                                                basket.Principals,
                                                basket.Copula,
                                                (Correlation) basket.Correlation,
                                                basket.StepSize,
                                                basket.StepUnit)
               {
                 IntegrationPointsFirst = basket.IntegrationPointsFirst,
                 IntegrationPointsSecond = basket.IntegrationPointsSecond
               };
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">Nth to default swap product to price</param>
    /// <param name="basket">The basket model used to price</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating paymentes forecast</param>
    /// <param name="notional">notional</param>
    ///
    public
    FTDPricer(
              FTD product,
              BasketForNtdPricer basket,
              DiscountCurve discountCurve,
              DiscountCurve referenceCurve,
              double notional
              )
      : this(product, basket, discountCurve, referenceCurve, notional, null)
    {
    }
    
    
    
    
    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Nth to default swap product to price</param>
		/// <param name="basket">The basket model used to price</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="notional">notional</param>
		///
    public
		FTDPricer(
							FTD product,
							BasketForNtdPricer basket,
							DiscountCurve discountCurve,
							double notional
							)
			: this(product, basket, discountCurve, notional, null)
		{
		}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">Nth to default swap product to price</param>
    /// <param name="basket">The basket model used to price</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="notional">notional</param>
    /// <param name="rateResets">A list of rate resets</param>
    ///

    public
    FTDPricer(
              FTD product,
              BasketForNtdPricer basket,
              DiscountCurve discountCurve,
              double notional,
              List<RateReset> rateResets
              ) : this(product, basket, discountCurve, null, notional, rateResets)
    {
    }
    
    
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">Nth to default swap product to price</param>
    /// <param name="basket">The basket model used to price</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///<param name="referenceCurve">Reference curve used for floating payments forecast</param>
    /// <param name="notional">notional</param>
    /// <param name="rateResets">A list of rate resets</param>
    ///

    public
    FTDPricer(
              FTD product,
              BasketForNtdPricer basket,
              DiscountCurve discountCurve,
              DiscountCurve referenceCurve,
              double notional,
              List<RateReset> rateResets
              )
      : base(product, basket.AsOf, basket.Settle)
    {
      discountCurve_ = discountCurve;
      referenceCurve_ = referenceCurve;
      basket_ = basket;
      if (product.FixedRecovery != null)
      {
        basket.FixedRecovery = product.FixedRecovery;
      }
      
      base.Notional = notional;
      rateResets_ = rateResets;
      if (!product.AccruedOnDefault)
        accruedFractionOnDefault_ = 0;
    }

		/// <summary>
		///   Clone
		/// </summary>
		public override object
		Clone()
		{
			FTDPricer obj = (FTDPricer)base.Clone();
			obj.basket_ = (BasketForNtdPricer)basket_.Clone();
			obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();
      obj.rateResets_ = CloneUtil.Clone(rateResets_);
		  obj.referenceCurve_ = (ReferenceCurve == null) ? null : (DiscountCurve) referenceCurve_.Clone();
      return obj;
		}

		#endregion Constructors

		#region Methods

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

      if (basket_ == null)
        InvalidValue.AddError(errors, this, "Basket", String.Format("Invalid basket. Cannot be null."));

      if (accruedFractionOnDefault_ < 0.0 || accruedFractionOnDefault_ > 1.0)
        InvalidValue.AddError(errors, this, "AccruedFractionOnDefault", String.Format("Invalid accrued on default {0}. Must be >= 0 and <= 1", accruedFractionOnDefault_));

      if (defaultTiming_ < 0.0 || defaultTiming_ > 1.0)
        InvalidValue.AddError(errors, this, "DefaultTiming", String.Format("Invalid accrued on default {0}. Must be >= 0 and <= 1", accruedFractionOnDefault_));

      int start = FTD.First - 1;
      if (start < 0)
        InvalidValue.AddError(errors, this, "NTD.First", "First must postive");
      if (start + FTD.NumberCovered > Basket.Count)
        InvalidValue.AddError(errors, this, "NTD.NumberCovered", "Number covered must be no more than issuers");

      if (FTD.NtdType == NTDType.FundedFloating)
        if (rateResets_ == null || rateResets_.Count == 0)
          InvalidValue.AddError(errors, this, "RateResets", String.Format("RateResets can neither be empty nor null for floating coupon pricing."));

      RateResetUtil.Validate(rateResets_, errors);
      return;
    }

    /// <summary>
    ///   Clear the internal state
    /// </summary>
    /// <remarks>
    ///   The FTD pricer remembers some internal states in order to skip
    ///   redundant calculation steps.
    ///   This function clears the internal states and therefore
    ///   force the pricers to recalculate all the steps.
    /// </remarks>
    override public void Reset()
    {
      basket_.Reset();
      basket_.FixedRecovery = FTD.FixedRecovery;
    }

    /// <summary>
		///   Calculate pv of Nth to default swap protection leg.
		/// </summary>
		///
		/// <returns>the PV of the protection leg in this setting</returns>
		///
    public double ProtectionPv()
    {
      FTD ftd = this.FTD;
      if (IsFunded(ftd))
        return 0.0;

      double pv = 0;
      BasketForNtdPricer basket = this.Basket;
      int first = ftd.First;
      int covered = ftd.NumberCovered;
      for (int i = 0; i < covered; ++i)
        pv += ProtectionPv(first + i, basket.Settle);

      // Add any unsettled default losses
      // Note for FundedFixed and FundedFloating the DefaultSettlementPv = 0
      if (!(this.FTD.NtdType == NTDType.FundedFixed || this.FTD.NtdType == NTDType.FundedFloating))
        pv += basket.DefaultSettlementPv(basket.Settle, ftd.Maturity,
                                         DiscountCurve, first, covered, true, false)*Notional;

      // discount back to the as-of date
      return pv*DiscountCurve.DiscountFactor(basket.AsOf, Basket.Settle);
    }

    /// <summary>
    /// Calculate flat pv of Nth to default swap fee leg.
    /// </summary>
    /// <param name="premium">Premium with non-bullet frequenct, or bonus rate with bullet frequency</param>
    /// <returns>
    /// the flat Pv of the fee leg in this setting
    /// </returns>
    public double FlatFeePv(double premium)
    {
      double pv = FeePv(premium);
      // minus accrued to get clean fee
      pv -= Accrued(FTD, Basket.Settle, premium) * Notional;
      return pv;
		}

    /// <summary>
    ///   Calculate pv (clean) of NTD fee leg.
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee.</remarks>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FlatFeePv()
    {
      FTD ftd = FTD;
      return FlatFeePv(ftd.Premium) + UpFrontFeePv();
    }

    /// <summary>
    ///   Calculate pv of Nth to default swap fee leg.
    /// </summary>
    ///
    /// <param name="premium">Premium with non-bullet frequenct, or bonus rate with bullet frequency</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FeePv(double premium)
    {
      double pv = 0;
      BasketForNtdPricer basket = this.Basket;
      FTD ftd = this.FTD;
      int first = ftd.First;
      int covered = ftd.NumberCovered;

      // Need to add the optional bonus part to feepv
      pv += BulletCouponUtil.GetDiscountedBonus(this);

      for (int i = 0; i < covered; ++i)
        pv += FeePv(first + i, basket.Settle, premium, ftd.DayCount, ftd.Freq, discountCurve_);
      // discount back to the as-of date
      pv *= discountCurve_.DiscountFactor(basket.AsOf, basket.Settle);

      if (IsFunded(ftd))
      {
        // Add any unsettled default recoveries
        pv += basket.DefaultSettlementPv(basket.Settle, ftd.Maturity,
          DiscountCurve, first, covered, false, true) * Notional;
      }

      return pv;
    }


		/// <summary>
		///   Calculate the pv of up-front fee
		/// </summary>
		///
		/// <returns>pv fee</returns>
		///
		public double UpFrontFeePv()
		{
			double pv;

		  FTD ftd = FTD;
			if( ftd.Fee != 0.0 && Dt.Cmp(ftd.FeeSettle,this.Settle) > 0 )
			{
			  pv = ftd.Fee * Notional;
			}
			else
				pv = 0.0;
			return pv;
		}

    /// <summary>
    ///   Fee pv (full) of NTD
    /// </summary>
    ///
    /// <returns>the PV of the fee leg</returns>
    ///
		public double FeePv()
		{
		  FTD ftd = FTD;
      return FeePv(ftd.Premium) + UpFrontFeePv();
		}


		/// <summary>
		///   Calculate pv of the Nth to default swap.
		/// </summary>
		///
		/// <returns>the PV of the Nth to default swap</returns>
		///
		public override double ProductPv()
    {
		  return ProtectionPv() + FeePv();
		}

		/// <summary>
		///   Calculate flat price of the FTD.
		/// </summary>
		///
		/// <returns>the PV of the CDO</returns>
		///
		public double FlatPrice()
		{
		  return ProtectionPv() + FlatFeePv();
		}

		/// <summary>
		///   Calculate full price of the FTD.
		/// </summary>
		///
		/// <returns>the PV (with Accrued) of the CDO</returns>
		///
		public double FullPrice()
		{
		  return ProtectionPv() + FeePv();
		}

    /// <summary>
    ///   Calculate accrual days for Nth to default swap
    /// </summary>
    ///
    /// <returns>Accrual days to settlement for Nth to default swap</returns>
    ///
    public int AccrualDays()
    {
      if (this.FTD.NtdType == NTDType.PO || Dt.Cmp(Settle, FTD.Maturity) > 0)
      {
        return 0;
      }
      return (AccrualDays(FTD, basket_.Settle));
    }

    /// <summary>
    ///   Calcluate accrual days for the Nth to default swap
    /// </summary>
    ///
    /// <param name="ftd">Nth to default swap product</param>
    /// <param name="settle">Settlement date</param>
    ///
    /// <returns>AccrualDays to settlement for Nth to default swap</returns>
    ///
    public int AccrualDays(FTD ftd, Dt settle)
    {
      // Same logic as the Accrued(...) method
      if (Dt.Cmp(settle, ftd.Maturity) > 0 || this.FTD.NtdType == NTDType.PO)
        return 0;

      bool eomRule = (ftd.CycleRule == CycleRule.EOM);
      
      // Generate out payment dates from settlement
      Schedule sched = new Schedule(settle, ftd.Effective, ftd.FirstPrem, ftd.LastPrem, ftd.Maturity,
                                    ftd.Freq, ftd.BDConvention, ftd.Calendar, false, false, false, eomRule);

      // Calculate accrued to settlement.
      Dt start = sched.GetPeriodStart(0);
      Dt end = sched.GetPeriodEnd(0);
      // Note schedule currently includes last date in schedule period. This may get changed in
      // the future so to handle this we test if we are on a coupon date.
      if (Dt.Cmp(settle, start) == 0 || Dt.Cmp(settle, end) == 0)
        return 0;
      else
        return (Basket.AccrualFractionDays(start, settle, ftd.DayCount,
          ftd.First, ftd.NumberCovered));
    }

    /// <summary>
    ///   Calculate accrued for Nth to default swap
    /// </summary>
    ///
    /// <returns>Accrued to settlement for Nth to default swap</returns>
    ///
    public override double Accrued()
    {
      if (this.FTD.NtdType == NTDType.PO || Dt.Cmp(Settle, FTD.Maturity) > 0)
      {
        return 0;
      }
      FTD ftd = FTD;
      return (Accrued(FTD, basket_.Settle, ftd.Premium) * Notional);
    }

    /// <summary>
    ///   Calcluate accrued for the Nth to default swap as a percentage of Notional
    /// </summary>
    ///
    /// <param name="ftd">Nth to default swap product</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="premium">Accrual coupon rate (premium with non-bullet frequency, or periodic coupon rate with bullet frequency</param>
    ///
    /// <returns>Accrued to settlement for Nth to default swap</returns>
    ///
    private double Accrued(FTD ftd, Dt settle, double premium)
    {
      return _usePaymentSchedule
        ? PsAccrued(ftd, settle, premium)
        : CfAccrued(ftd, settle, premium);
    }

    private double PsAccrued(FTD ftd, Dt settle, double premium)
    {
      if (Dt.Cmp(settle, ftd.Maturity) > 0 || FTD.NtdType == NTDType.PO)
        return 0;
      if (Math.Abs(premium) < 1E-15)
        return 0;

      // If no periodic frequency or no premium
      if (((int)ftd.Freq) < 1)
        return 0.0;

      var cfa = new CashflowAdapter(PriceCalc.GeneratePsForFee(
        settle, premium, ftd.Effective, ftd.FirstPrem, ftd.Maturity,
        ftd.Ccy, ftd.DayCount, ftd.Freq, ftd.BDConvention, ftd.Calendar,
        null, ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.IOFundedFloating,
        false, DiscountCurve,
        RateResets, DiscountCurve));

      // Find first cashflow on or after settlement (depending on includeSettle flag)
      int N = cfa.Count;
      int firstIdx;
      for (firstIdx = 0; firstIdx < N; firstIdx++)
      {
        if (Dt.Cmp(cfa.GetDt(firstIdx), settle) > 0)
          break;
      }
      if (firstIdx >= N)
        return 0.0;  // This may happen when the forward date is after maturity, for example.

      //TODO: revist this to consider start and end dates.
      Dt accrualStart = (firstIdx > 0) ? cfa.GetDt(firstIdx - 1) : cfa.Effective;
      if (Dt.Cmp(accrualStart, settle) > 0)
        return 0.0; // this may occur for forward starting CDO

      Dt nextDate = cfa.GetDt(firstIdx);
      double paymentPeriod = Dt.Fraction(accrualStart, nextDate, accrualStart, nextDate, ftd.DayCount, ftd.Freq);
      if (paymentPeriod < 1E-10)
        return 0.0; // this may happen if maturity on settle, for example

      double accrued = Basket.AccrualFraction(accrualStart, settle, ftd.DayCount, ftd.Freq,
        ftd.First, ftd.NumberCovered, ftd.AccruedOnDefault) / paymentPeriod * cfa.GetAccrued(firstIdx);
      return accrued;
    }

    /// <summary>
    ///   Calculate the Premium 01 of the CDO
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Premium01 is the change in PV (MTM) for an NTD
    ///   if the premium is increased by one basis point.</para>
    ///
    ///   <para>The Premium 01 is calculated by calculating the PV (MTM) of the
    ///   NTD then bumping up the premium by one basis point and
    ///   re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    ///
    /// <returns>Premium 01 of the NTD</returns>
    ///
    public double Premium01()
    {
      FTD ftd = this.FTD;
      return (FeePv(ftd.Premium + 0.0001) - FeePv(ftd.Premium));
    }

    /// <summary>
    ///   Calculate the break even premium of the NTD.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">\mathrm{BreakEvenPremium = \displaystyle{\frac{ProtectionLegPv}{ Duration \times Notional }}}</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    ///
    /// <returns>the break even premium</returns>
    ///
		public double BreakEvenPremium()
		{
      return BreakEvenPremiumSolver.Solve(this, this.AsOf, GetProtectionStart());
		}

    /// <summary>
    ///   Calculate the break even fee of the NTD.
    /// </summary>
    ///
    /// <returns>the break even fee</returns>
    ///
    public double BreakEvenFee()
    {
      if (this.FTD.NtdType == NTDType.Unfunded)
        return ((-ProtectionPv() - FlatFeePv()) / Notional);
      else
      {
        NTDType savedType = this.FTD.NtdType;
        this.FTD.NtdType = NTDType.Unfunded;
        this.Basket.Reset();
        double bef = 0.0;
        try
        {
          bef = ((-ProtectionPv() - FlatFeePv()) / Notional);
        }
        finally
        {
          this.FTD.NtdType = savedType;
          this.Basket.Reset();
        }
        return bef;
      }
    }

		/// <summary>
		///   Calculate the risky duration of the Nth to default swap
		/// </summary>
		///
		/// <remarks>
		///   <para>The risky duration is defined as the ratio of the fee pv divided by the fee</para>
		/// </remarks>
		///
		/// <returns>risky duration</returns>
		///
		public double RiskyDuration()
		{
      double pv = 0;
      if (FTD.NtdType == NTDType.PO)
      {
        double timeToMaturity = Dt.TimeInYears(Settle, this.FTD.Maturity);
        pv = FlatFeePv() * timeToMaturity / Notional;
      }
			else
        pv = FlatFeePv(1.0) / Notional;
			return pv ;
		}

		/// <summary>
		///   Calculate expected loss at maturity date
		/// </summary>
		///
		/// <returns>Expected loss at the maturity date</returns>
		///
		public double ExpectedLoss()
		{
		  FTD ftd = this.FTD;
			return LossToDate( ftd.Maturity );
		}


		/// <summary>
		///   Calculate expected survival rate at the maturity date
		/// </summary>
		public double ExpectedSurvival()
		{
		  FTD ftd = this.FTD;
			return SurvivalToDate( ftd.Maturity );
		}

		/// <summary>
		///   Calculate the implied break-even correlation for a Nth to default swap
		/// </summary>
		///
		/// <remarks>
		///   <para>Calculates the uniform one-factor correlation which implies a
		///   present value of zero for the tranche.</para>
		/// </remarks>
		///
		/// <returns>Break-even correlation for Nth to default swap</returns>
		///
		public double ImpliedCorrelation()
		{
			// Save original basket pricer and correlation
			BasketForNtdPricer origBasket = basket_;
      try
      {
        basket_ = (BasketForNtdPricer)origBasket.Clone();
        double result = EstimateImpliedCorrelationPv(0.0);
        return result;
      }
      finally
      {
        basket_ = origBasket;
      }
		}

		/// <summary>
		///   The survival curve of nth to default product
		/// </summary>
		public SurvivalCurve NthSurvivalCurve( int nth )
		{
			return Basket.NthSurvivalCurve( nth );
		}

		/// <summary>
		///   The loss curve of nth to default product
		/// </summary>
		public Curve NthLossCurve( int nth )
		{
			return Basket.NthLossCurve( nth );
		}

    /// <summary>
    ///   Calculate the protection pv
    /// </summary>
    private double ProtectionPv(int nth, Dt settle)
    {
      return _usePaymentSchedule
        ? PsProtectionPv(nth, settle)
        : CfProtectionPv(nth, settle);
    }


    private double PsProtectionPv(int nth, Dt settle)
		{
		  FTD ftd = this.FTD;
		  if (Dt.Cmp(GetProtectionStart(), ftd.Maturity) > 0)
		    return 0.0;
		  Curve lossCurve = Basket.NthLossCurve(nth);
		  Dt dfltDate = lossCurve.JumpDate;
		  if (!dfltDate.IsEmpty() && dfltDate <= settle)
		    return 0.0;
		  var cfa =new CashflowAdapter(PriceCalc.GeneratePsForProtection(
		    settle, ftd.Maturity, ftd.Ccy, null));

		  // Use 0-flat discount curve to hornor PayRecoveryAtMaturity = true
		  DiscountCurve flatDiscCurve = new DiscountCurve(AsOf, 0);
		  double pv = PriceCalc.Price(cfa, settle,
		                              FTD.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurveForContingentLeg,
		                              lossCurve.Interpolate,
		                              (dt) => 1.0, // not used
		                              null, false, true, false,
		                              DefaultTiming, AccruedFractionOnDefault,
		                              StepSize, StepUnit, cfa.Count);
		  if (FTD.PayRecoveryAtMaturity)
		    pv *= DiscountCurveForContingentLeg.DiscountFactor(settle, FTD.Maturity);
		  return pv*Notional/ftd.NumberCovered;
		}

    /// <summary>
    ///   Calculate the protection pv
    /// </summary>
    private double RecoveryPv(int nth, Dt settle, Curve nthSurvival)
    {
      return _usePaymentSchedule
        ? PsRecoveryPv(nth, settle, nthSurvival)
        : CfRecoveryPv(nth, settle, nthSurvival);
    }

    private double PsRecoveryPv(int nth, Dt settle, Curve nthSurvival)
    {
      FTD ftd = FTD;
      if (Dt.Cmp(GetProtectionStart(), ftd.Maturity) > 0)
        return 0.0;
      Curve nthLoss = Basket.NthLossCurve(nth);
      Dt dfltDate = nthLoss.JumpDate;
      if (!dfltDate.IsEmpty() && dfltDate <= settle)
        return 0.0;
  
      var cfa = new CashflowAdapter(PriceCalc.GeneratePsForProtection(settle, 
        ftd.Maturity, ftd.Ccy, null));

      // Use 0-flat discount curve to hornor PayRecoveryAtMaturity = true
      DiscountCurve flatDiscCurve = new DiscountCurve(AsOf, 0);
      if (IsQuantoStructure)
      {
        //the survival curve is under domestic measure, while the loss curve is under the foreign measure, 
        //so we need to compute separately
        double fpv = PriceCalc.Price(cfa, settle,
                                     ftd.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurve,
                                     (dt) => 1 - nthSurvival.Interpolate(dt),
                                     (dt) => 1.0, // not used
                                     null, false, true, false,
                                     DefaultTiming, AccruedFractionOnDefault,
                                     StepSize, StepUnit, cfa.Count);
        if (ftd.PayRecoveryAtMaturity)
          fpv *= DiscountCurve.DiscountFactor(settle, ftd.Maturity);
        double ppv = PriceCalc.Price(cfa, settle,
                                     ftd.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurveForContingentLeg,
                                     nthLoss.Interpolate, (dt) => 1.0, // not used
                                     null, false, true, false,
                                     DefaultTiming, AccruedFractionOnDefault,
                                     StepSize, StepUnit, cfa.Count);
        if (ftd.PayRecoveryAtMaturity)
          ppv *= DiscountCurveForContingentLeg.DiscountFactor(settle, ftd.Maturity);
        return -(fpv - ppv); // reverse the sign since this is recovery, not loss.
      }
      double pv = PriceCalc.Price(cfa, settle,
                                  ftd.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurve,
                                  (dt) => 1 - nthSurvival.Interpolate(dt) - nthLoss.Interpolate(dt),
                                  (dt) => 1.0, // not used
                                  null, false, true, false,
                                  DefaultTiming, AccruedFractionOnDefault,
                                  StepSize, StepUnit, cfa.Count);
      if (ftd.PayRecoveryAtMaturity)
        pv *= DiscountCurve.DiscountFactor(settle, ftd.Maturity);
      return -pv; // reverse the sign since this is recovery, not loss.
    }

    /// <summary>
    ///   Calculate the fee pv from the periodic accrual payments.
    ///   This code also handles the Funded case
    /// </summary>
    private double FeePv(int nth, Dt settle, double premium,
      DayCount dayCount, Frequency freq, DiscountCurve discountCurve)
    {
      return _usePaymentSchedule
        ? PsFeePv(nth, settle, premium, dayCount, freq, discountCurve)
        : CfFeePv(nth, settle, premium, dayCount, freq, discountCurve);
    }

    private double PsFeePv(int nth, Dt settle, double premium,
      DayCount dayCount, Frequency freq, DiscountCurve discountCurve)
    {
      FTD ftd = this.FTD;
      if (Dt.Cmp(settle, ftd.Maturity) > 0)
        return 0;

      // Get the survival curve
      BasketForNtdPricer basket = Basket;
      SurvivalCurve curve = basket.NthSurvivalCurve(nth);
      Dt dfltDate = curve.JumpDate;
      if (!dfltDate.IsEmpty() && dfltDate <= settle)
      {
        // The default settlement payment is handled in other places.
        // Here we need to add the accrued.
        FTD ftdOne = (FTD)ftd.Clone();
        ftdOne.First = nth;
        ftdOne.NumberCovered = 1;
        ftdOne.DayCount = dayCount;
        ftd.Freq = freq;
        double accrued = Accrued(ftdOne, settle, premium);
        return accrued * Notional / ftd.NumberCovered
          / discountCurve.DiscountFactor(AsOf, settle);
      }
      var referenceCurve = ReferenceCurve?? DiscountCurve;
      // We cannot simply call PriceCalcObsolete.GenerateCashflowForFee()
      // with the parameter floating to be false, because we MUST handle
      // floating rate in the cashflow generator.
      var cfa = new CashflowAdapter(PriceCalc.GeneratePsForFee(
        settle, premium, ftd.Effective, ftd.FirstPrem, ftd.Maturity,
        ftd.Ccy, dayCount, freq, ftd.BDConvention, ftd.Calendar, null,
        ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.IOFundedFloating,
        false, referenceCurve, RateResets, DiscountCurve));

      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;

      // This mimics the Funded CDO: add back the credit-contingent maturity fee 
      double fundPaidBack = 0;
      if (settle < Maturity && (ftd.NtdType == NTDType.FundedFixed
        || ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.PO))
      {
        // Fund payback is the recovery payback plus the remaining notional at maturity.
        fundPaidBack = RecoveryPv(nth, settle, curve)
          + curve.Interpolate(ftd.Maturity)
            *discountCurve.DiscountFactor(settle, ftd.Maturity);
      }
      if (ftd.NtdType == NTDType.PO)
        return fundPaidBack*Notional/ftd.NumberCovered;

		  double pv = PriceCalc.Price(
		    cfa, settle, discountCurve,
		    (dt) => 0.0,
		    curve.Interpolate,
		    null, true, false, false,
		    DefaultTiming, AccruedFractionOnDefault,
		    stepSize, stepUnit, cfa.Count);
      pv += fundPaidBack;

      return pv*Notional/ftd.NumberCovered;
    }

    private double SurvivalToDate(Dt date)
    {
      BasketForNtdPricer basket = this.Basket;
      FTD ftd = this.FTD;
      int first = ftd.First;
      int covered = ftd.NumberCovered;
      double accumulatedSurvival = 0;
      for (int i = 0; i < covered; ++i)
      {
        // Get teh survival curve
        Curve curve = basket.NthLossCurve(first + i);
        double survival = 1 - curve.Interpolate(date);
        accumulatedSurvival += survival;
      }
      return accumulatedSurvival / covered; ;
    }

		private double LossToDate( Dt date )
		{
			BasketForNtdPricer basket = this.Basket;
		  FTD ftd = this.FTD;
			int first = ftd.First;
			int covered = ftd.NumberCovered;
			double accumulatedLoss = 0;
			for( int i = 0; i < covered; ++i )
			{
			  // Get the survival curve
			  Curve curve = basket.NthLossCurve( first + i );
			  double loss = curve.Interpolate( date );
				accumulatedLoss += loss;
			}
			return accumulatedLoss * Notional / covered ;
		}

    /// <summary>
    ///   Calculate the carry of the NTD
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The carry is daily income from premium and is simply
    ///   the premium divided by 360 times the notional, <formula inline="true">\mathrm{Carry=\frac{Premium}{360}\times Notional}</formula></para>
    /// </remarks>
    ///
    /// <returns>Carry of the NTD</returns>
    ///
    public double Carry()
    {
      return _usePaymentSchedule ? PsCarry() : CfCarry();
    }

    private double PsCarry()
    {
      FTD ftd = FTD;

      // Floating coupon case
      if (ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.IOFundedFloating)
      {
        Dt settle = GetProtectionStart();

        // If settle date is later than NTD maturity, nothing to carry
        if (settle >= ftd.Maturity)
          return 0.0;

        // If no periodic frequency or no premium
        if (((int) ftd.Freq) < 1 || Math.Abs(ftd.Premium) < 1E-14)
          return 0.0;

        var cfa = new CashflowAdapter(PriceCalc.GeneratePsForFee(
          settle, ftd.Premium, ftd.Effective, ftd.FirstPrem, ftd.Maturity,
          ftd.Ccy, ftd.DayCount, ftd.Freq, ftd.BDConvention, ftd.Calendar, null,
          true, false, DiscountCurve, RateResets, DiscountCurve));

        //- Find the latest cashflow on or before settlement
        int N = cfa.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cfa.GetDt(firstIdx), settle) > 0)
            break;
        }
        //TODO: Revisit this to use start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cfa.GetDt(firstIdx - 1) : cfa.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0.0;

        Dt nextDate = cfa.GetDt(firstIdx);
        double paymentPeriod = Dt.Fraction(accrualStart, nextDate, accrualStart, nextDate, ftd.DayCount, ftd.Freq);
        if (paymentPeriod < 1E-10)
          return 0.0;

        //- find the effective coupon on the settle date
        double coupon = cfa.GetAccrued(firstIdx)/paymentPeriod;
        return coupon/360*this.CurrentNotional;
      }
      return ftd.Premium/360*this.CurrentNotional;
    }

    /// <summary>
    ///   Calculate the MTM Carry of the NTD
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The MTM carry is the daily income of the MTM level
    ///   of the credit default swap. It is the Break Even Premium
    ///   divided by 360 times the notional, <formula inline="true">\mathrm{Carry=\frac{BreakEvenPremium}{360}\times Notional}</formula></para>
    /// </remarks>
    /// 
    /// <returns>MTM Carry of NTD</returns>
    /// 
    public double MTMCarry()
    {
      return BreakEvenPremium() / 360 * CurrentNotional;
    }

    /// <summary>
    ///   Get protection starting date
    /// </summary>
    /// <returns>protection start date</returns>
    internal Dt GetProtectionStart()
    {
      Dt settle = Settle;
      Dt effective = FTD.Effective;
      if (Dt.Cmp(effective, settle) > 0)
        return effective;
      return settle;
    }

    private static bool IsFunded(FTD ftd)
    {
      return ftd.NtdType == NTDType.FundedFixed || ftd.NtdType == NTDType.FundedFloating
        ||ftd.NtdType == NTDType.IOFundedFixed || ftd.NtdType == NTDType.IOFundedFloating
        ||ftd.NtdType == NTDType.PO;
    }
		#endregion Methods

		#region Properties

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

		/// <summary>
		///   CDO to price
		/// </summary>
		public FTD FTD
		{
			get { return (FTD)Product; }
		}
    
		/// <summary>
		///   Underlying portfolio basket
		/// </summary>
		public BasketForNtdPricer Basket
		{
			get { return basket_; }
		}

    /// <summary>
    ///   Effective notional on the settle date
    /// </summary>
    public override double EffectiveNotional
    {
      get {
        return Basket.EffectiveSurvival(FTD.First,
          FTD.NumberCovered, true) * Notional; 
      }
    }

    /// <summary>
    /// Current outstanding notional on the settlement date
    /// </summary>
    /// <value></value>
    /// <remarks>
    /// This is the current notional at the settlement
    /// date, excluding al the names defaulted before the settle date.
    /// </remarks>
    public override double CurrentNotional
    {
      get
      {
        return Basket.EffectiveSurvival(FTD.First,
          FTD.NumberCovered, false) * Notional;
      }
    }

    /// <summary>
 		///   As-of date
 		/// </summary>
    public override sealed Dt AsOf
 		{
 			get { return Basket.AsOf; }
 			set { base.AsOf = Basket.AsOf = value; }
 		}
    
 		/// <summary>
 		///   Settlement date
 		/// </summary>
 		public override sealed Dt Settle
 		{
 			get { return Basket.Settle; }
 			set { base.Settle = Basket.Settle = value; }
 		}

		/// <summary>
		///   Maturity date
		/// </summary>
		public Dt Maturity
		{
			get { return Basket.Maturity; }
			set { Basket.Maturity = value; }
		}
    
    /// <summary>
    ///   Step size for pricing grid
		/// </summary>
    public int StepSize
		{
			get { return Basket.StepSize; }
			set { Basket.StepSize = value; }
		}
    
		/// <summary>
    ///   Step units for pricing grid
		/// </summary>
    public TimeUnit StepUnit
		{
			get { return Basket.StepUnit; }
			set { Basket.StepUnit = value; }
		}
    
		/// <summary>
    ///   Correlation structure of the basket
		/// </summary>
    public Correlation Correlation
		{
			get { return Basket.Correlation; }
		}
    
		/// <summary>
    ///   Copula structure
		/// </summary>
    public Copula Copula
		{
			get { return Basket.Copula; }
			set { Basket.Copula = value; }
		}
    
		/// <summary>
		///  Survival curves from curves
		/// </summary>
		public SurvivalCurve[] SurvivalCurves
		{
			get { return Basket.SurvivalCurves; }
		}
    
		/// <summary>
		///  Recovery curves from curves
		/// </summary>
		public RecoveryCurve[] RecoveryCurves
		{
			get { return Basket.RecoveryCurves; }
		}
    
		/// <summary>
		///   Recovery rates from curves
		/// </summary>
    public double[] RecoveryRates
    {
      get
      {
        Dt date = Maturity;
        return Array.ConvertAll<RecoveryCurve, double>(
          Basket.RecoveryCurves, (r) => r.Interpolate(date));
      }
    }
    
    /// <summary>
		///   Discount Curve used for pricing
		/// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set { discountCurve_ = value; }
    }

    /// <summary>
    ///   Historical rate fixings
    /// </summary>
    public List<RateReset> RateResets
    {
      get
      {
        if (rateResets_ == null)
          rateResets_ = new List<RateReset>();
        return rateResets_;
      }
      set
      {
        rateResets_ = value;
      }
    }

    /// <summary>
    ///   Current floating rate
    /// </summary>
    public double CurrentRate
    {
      get { return RateResetUtil.ResetAt(rateResets_, AsOf); }
      // To be inline with Hehui's 7457, make CurrentRate readonly
      /*set
      {
        // Set the RateResets to support returning the current coupon
        rateResets_ = new List<RateReset>();
        rateResets_.Add(new RateReset(Product.Effective, value));
        Reset();
      }
       */ 
    }
    
		/// <summary>
    ///   Principal or face values for each name in the basket
		/// </summary>
		public double [] Principals
		{
			get { return Basket.Principals; }
		}

		/// <summary>
		///   Notional amount for pricing
		/// </summary>
		public double TotalPrincipal
		{
			get { return Notional / FTD.NumberCovered * Basket.Count; }
		}

		/// <summary>
		///   Indicator for when default is considered to occur within a time period (in the time grid sense).  A value of zero
		///   indicates the beginning of the period is assumed and a value of one means the end of the period.  Default value is 0.5 and any value
		///   must be between zero and one.
		/// </summary>
		public double DefaultTiming
		{
			get { return defaultTiming_; }
			set
			{
				defaultTiming_ = value;
			}
		}

		/// <summary>
		///   Indicator for how accrued is handled under default.  A value of zero indicates no accrued will be used and a
		///   value of one means the entire period's (in the time grid sense) accrued will be counted.  This is further controlled
		///   by the FTD property AccruedOnDefault, which, if FALSE, means this value is ignored.  Default value is 0.5 and any value
		///   must be between zero and one.
		/// </summary>
    public double AccruedFractionOnDefault
    {
      get { return accruedFractionOnDefault_; }
      set { accruedFractionOnDefault_ = value; }
    }

    /// <summary>
    /// Reference curve for floating payments forecast
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set { referenceCurve_ = value; }
    }
#if FTD_SCRIPT
		/// <summary>
		/// </summary>
		public Script CashflowScript
		{
			get { return cashflowScript_; }
			set { cashflowScript_ = value; }
		}
		#endif

		#endregion Properties

    #region Quanto Properties

    /// <summary>
    /// Check for Quanto structure
    /// </summary>
    private bool IsQuantoStructure
    {
      get
      {
        var b = Basket as SemiAnalyticBasketForNtdPricerQuanto;
        return b != null;
      }
    }

    /// <summary>
    /// FxCurve
    /// </summary>
    public FxCurve FxCurve
    {
      get
      {
        if(IsQuantoStructure)
        {
          var basket = Basket as SemiAnalyticBasketForNtdPricerQuanto;
          return basket.FxCurve;
        }
        return null;
      }
    }
   
    /// <summary>
    /// At the money forward FX volatility
    /// </summary>
    public VolatilityCurve FxVolatility
    {
      get
      {
        if (IsQuantoStructure)
        {
          var basket = Basket as SemiAnalyticBasketForNtdPricerQuanto;
          return basket.AtmFxVolatility;
        }
        return null;
      }
    }

    /// <summary>
    /// At the money forward FX volatility
    /// </summary>
    public double FxCorrelation
    {
      get
      {
        if (IsQuantoStructure)
        {
          var basket = Basket as SemiAnalyticBasketForNtdPricerQuanto;
          return basket.FxCorrelation;
        }
        return 0.0;
      }
      set
      {
        if (IsQuantoStructure)
        {
          var basket = Basket as SemiAnalyticBasketForNtdPricerQuanto;
          basket.FxCorrelation = value;
        }
      }
    }

    /// <summary>
    /// Fx devaluation
    /// </summary>
    public double FxDevaluation
    {
      get
      {
        if(IsQuantoStructure)
        {
          var basket = Basket as SemiAnalyticBasketForNtdPricerQuanto;
          return basket.FxDevaluation;
        }
        return 0.0;
      }
      set
      {
        if (IsQuantoStructure)
        {
          var basket = Basket as SemiAnalyticBasketForNtdPricerQuanto;
          basket.FxDevaluation = value;
        }
      }
    }
    
    /// <summary>
    /// Discount curve for contingent payment
    /// </summary>
    public DiscountCurve DiscountCurveForContingentLeg
    {
      get
      {
        if (IsQuantoStructure)
        {
          var basket = Basket as SemiAnalyticBasketForNtdPricerQuanto;
          if (basket.RecoveryCcy == SurvivalCurves[0].Ccy) //true Quanto structure
          {
            var fxCurve = basket.FxCurve;
            var dc = (DiscountCurve.Ccy == fxCurve.Ccy1)
                     ? fxCurve.Ccy2DiscountCurve
                     : (DiscountCurve.Ccy == fxCurve.Ccy2) ? fxCurve.Ccy1DiscountCurve : DiscountCurve;
            if (dc == null)
              throw new ArgumentException(String.Format("A valid {0} DiscountCurve must be present in the FxCurve {1}",
                                                        basket.RecoveryCcy, FxCurve.Name));
          }
          return DiscountCurve; //Foreign NTD
        }
        return DiscountCurve;
      }
    }
    #endregion

    #region Data

    private BasketForNtdPricer basket_;
    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private List<RateReset> rateResets_ = null;
		private double accruedFractionOnDefault_ = 0.5;
		private double defaultTiming_ = 0.5;
    private bool _usePaymentSchedule = true;

		#if FTD_SCRIPT
		private Script cashflowScript_;
		#endif
		#endregion Data

		#region CorrelationStuff
		private double
		EstimateImpliedCorrelationPv( double target )
		{
			string exceptDesc = null;

		  double xl, fl, xh, fh;
		  xl = 0.40;
			fl = EvaluatePv(xl, out exceptDesc);
			if (target <= fl) {
			  xh = xl; fh = fl;
				xl = 0.0;
				fl = EvaluatePv(xl, out exceptDesc);
				if (target <= fl) {
			    if (fl - target < 1.0e-7)
						return xl*xl;
					throw new ArgumentException(String.Format("The break even correlation cannot be found, possibly because the premium {0} is too big.", FTD.Premium));
				}
			}
			else // target > fl
			{
				xh = 0.80;
				fh = EvaluatePv(xh, out exceptDesc);
				if (target >= fh) {
				  xl = xh; fl = fh;
					xh = 1.0;
					fh = EvaluatePv(xh, out exceptDesc);
					if (target >= fh) {
					  if (target - fh < 1.0e-7)
							return xh*xh;
						throw new ArgumentException(String.Format("The break even correlation cannot be found, possibly because the premium {0} is too small.", FTD.Premium));
					}
				}
			}

      Double_Double_Fn fn = new Double_Double_Fn(this.EvaluatePv);
			double x0 = BrentSolver( fn, target,
															 xl, fl, xh, fh,
															 5e-7, 5e-7, out exceptDesc );

			return x0*x0;
		}

		private static double
		BrentSolver( Double_Double_Fn fn,
								 double target,
								 double a, double fa,
								 double b, double fb,
								 double toleranceF,
								 double toleranceX,
								 out string message )
		{
		  const int maxIterations = 1000;
			message = null;

			double c, d, e;
			fa -= target; fb -= target;
			double fc = fb;
			c = b;
			e = d = b-a;
      for(int  numIter = 1; numIter <= maxIterations; numIter++ )
      {
        if( (fb*fc) >= 0.0)
        {
          c = a;
          fc = fa;
          e = d = b-a;
        }
        if( Math.Abs(fc) <= Math.Abs(fb) )
        {
          a = b;
          b = c;
          c = a;
          fa = fb;
          fb = fc;
          fc = fa;
        }
        double tol1 = 2.0*toleranceX*Math.Abs(b)+0.5*toleranceF;
        double xm = 0.5*(c-b);
        if( Math.Abs(xm) <= tol1 || Math.Abs(fb) <= toleranceF )
          return b;
        if( Math.Abs(e) >= tol1 && Math.Abs(fa) > Math.Abs(fb) )
        {
				  double p, q;
          double s = fb/fa;
          if( a == c )
          {
            p = 2.0*xm*s;
            q = 1.0-s;
          }
          else
          {
            double r = fb/fc;
            q = fa/fc;
            p = s*(2.0*xm*q*(q-r) - (b-a)*(r-1.0));
            q = (q-1.0)*(r-1.0)*(s-1.0);
          }
          if( p > 0.0 )
            q = -q;
          p = Math.Abs(p);
          double min1 = 3.0*xm*q - Math.Abs(tol1*q);
          double min2 = Math.Abs(e*q);
          if( 2.0*p < (min1 < min2 ? min1 : min2) )
          {
            e = d;
            d = p/q;
          }
          else
          {
            d = xm;
            e = d;
          }
        }
        else
        {
          d = xm;
          e = d;
        }
        a = b;
        fa = fb;
        if( Math.Abs(d) > tol1 )
          b += d;
        else
          b += (xm > 0.0 ? Math.Abs(tol1) : -Math.Abs(tol1));
        fb = fn(b, out message) - target;
      }

			return b;
		}

    //
    // Private function for root find for detachment correlation.
		// Called by ImpliedDetachmentCorrelation()
    //
    private double
    EvaluatePv(double x, out string exceptDesc)
    {
      double pv = 0.0;
      exceptDesc = null;
      try
      {
		    basket_.SetFactor(x);
				basket_.Reset();
				pv = this.ProductPv();
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }
      return pv;
    }
		#endregion CorrelationStuff

    #region Helpers
    #region Break Even Calculations
    class BreakEvenPremiumSolver : SolverFn
    {
      internal static double Solve(FTDPricer pricer, Dt asOf, Dt settle)
      {
        NTDType type = pricer.FTD.NtdType;
        if (type == NTDType.Unfunded)
        {
          // SolveBepUnfunded handles bullet bonus
          return SolveBepUnfunded(pricer, asOf, settle);
        }
        else
        {
          NTDType savedType = pricer.FTD.NtdType;
          pricer.FTD.NtdType = NTDType.Unfunded;
          pricer.Basket.Reset();
          double bep = 0.0;
          try
          {
            bep = SolveBepUnfunded(pricer, asOf, settle);
          }
          finally
          {
            pricer.FTD.NtdType = savedType;
            pricer.Basket.Reset();
          }
          return bep;
        }
      }

      internal static double SolveBepUnfunded(FTDPricer pricer, Dt asOf, Dt settle)
      {
        NTDType type = pricer.FTD.NtdType;
        //store the original bonus
        var bullet = pricer.FTD.Bullet;
        try
        {
          // ignore bonus when calculating BE  
          pricer.FTD.Bullet = null; 
          double denominator = pricer.FlatFeePv(1.0);
          if (Math.Abs(denominator / pricer.Notional) < 1e-9)
            return 0;
          return (-pricer.ProtectionPv() - pricer.UpFrontFeePv()) / pricer.FlatFeePv(1.0);
        }
        finally
        {
          pricer.FTD.Bullet = bullet; 
        }
      }

      public override double evaluate(double x)
      {
        double res = netProtection_ - pricer_.Accrued(pricer_.FTD, settle_, x)*pricer_.Notional;
        for (int i = 0; i < pricer_.FTD.NumberCovered; ++i)
          res += pricer_.FeePv(pricer_.FTD.First + i, settle_, x, pricer_.FTD.DayCount,pricer_.FTD.Freq,pricer_.DiscountCurve) * df_;
        if (pricer_.FTD.Bullet != null && pricer_.FTD.Bullet.CouponRate > 0)
        {
          res += BulletCouponUtil.GetDiscountedBonus(pricer_) * df_;
        }
        return res;
      }
      private BreakEvenPremiumSolver(FTDPricer pricer, double netProtection, Dt settle, double df)        
      {
        pricer_ = pricer;
        settle_ = settle;
        df_ = df;
        netProtection_ = netProtection;
        currentNotional_ = pricer.CurrentNotional;
      }
      private double netProtection_, currentNotional_;      
      private Dt settle_;
      private double df_;
      private FTDPricer pricer_;
    }
    #endregion Break Even Calculations
    #endregion Helpers

    #region IRatesLockable Members

    RateResets IRatesLockable.LockedRates
    {
      get{ return new RateResets(RateResets);}
      set { RateResets.Initialize(value); }
    }

    IEnumerable<RateReset> IRatesLockable.ProjectedRates
    {
      get
      {
        if (FTD.NtdType == NTDType.FundedFloating
          || FTD.NtdType == NTDType.IOFundedFloating)
        {
          var ftd = FTD;
          var feePs = PriceCalc.GeneratePsForFee(GetProtectionStart(),
            ftd.Premium, ftd.Effective, ftd.FirstPrem, ftd.Maturity,
            ftd.Ccy, ftd.DayCount, ftd.Freq, ftd.BDConvention, ftd.Calendar, null,
            true, false, DiscountCurve, RateResets, DiscountCurve);
          return feePs.EnumerateProjectedRates();
        }
        return null;
      }
    }

    #endregion
  }
}
