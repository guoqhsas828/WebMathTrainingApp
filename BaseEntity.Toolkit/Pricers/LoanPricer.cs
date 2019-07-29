/*
 * LoanPricer.cs
 *
 *   2009. All rights reserved.
 *
 * Created by rsmulktis on 4/20/2000 12:28:26 PM
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using System.Data;
using System.Diagnostics;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a Bank <see cref="BaseEntity.Toolkit.Products.Loan">Loan</see> 
  /// (also known as Syndicated, Leveraged, Investment 
  /// Grade, etc. Loan) using both market standard calculations for 
  /// floating rate instruments along with credit relative 
  /// value calculations based on the <see 
  /// cref="BaseEntity.Toolkit.Models.LoanModel">Loan Model</see>.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Loan" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.LoanModel" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Loan" />
  /// <seealso cref="BaseEntity.Toolkit.Models.LoanModel" />
  [Serializable]
  public partial class LoanPricer : PricerBase, IPricer, IRecoverySensitivityCurvesGetter
  {
    #region Constructors

    /// <summary>
    /// Constructor.
    /// </summary>
    public LoanPricer(
      Loan loan,
      Dt asOf,
      Dt settle,
      double totalCommitment,
      double drawnCommitment,
      DiscountCurve discCurve,
      DiscountCurve refCurve,
      SurvivalCurve survCurve,
      RecoveryCurve recovCurve,
      SurvivalCurve prepayCurve,
      VolatilityCurve volatilityCurve,
      double refinancingCost,
      string curLevel,
      double[] usage,
      double[] endDistribution,
      MarketQuote quote,
      CalibrationType calType,
      double knownMarketSpread) :
      this(loan, asOf, settle, totalCommitment, drawnCommitment, true, discCurve, refCurve,
           survCurve, recovCurve, prepayCurve, volatilityCurve, refinancingCost, curLevel,
           usage, endDistribution, quote, calType, knownMarketSpread, 0)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public LoanPricer(
      Loan loan,
      Dt asOf,
      Dt settle,
      double totalCommitment,
      double drawnCommitment,
      DiscountCurve discCurve,
      DiscountCurve refCurve,
      SurvivalCurve survCurve,
      RecoveryCurve recovCurve,
      SurvivalCurve prepayCurve,
      VolatilityCurve volatilityCurve,
      double refinancingCost,
      string curLevel,
      double[] usage,
      double[] endDistribution,
      MarketQuote quote,
      CalibrationType calType,
      double knownMarketSpread,
      double rateVol) : 
      this(loan, asOf, settle,totalCommitment,drawnCommitment,true, discCurve, refCurve,
           survCurve, recovCurve, prepayCurve, volatilityCurve, refinancingCost, curLevel,
           usage, endDistribution, quote, calType, knownMarketSpread, rateVol)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public LoanPricer(
      Loan loan,
      Dt asOf,
      Dt settle,
      double totalCommitment,
      double drawnCommitment,
      bool useDrawnNotional,
      DiscountCurve discCurve,
      DiscountCurve refCurve,
      SurvivalCurve survCurve,
      RecoveryCurve recovCurve,
      SurvivalCurve prepayCurve,
      VolatilityCurve volatilityCurve,
      double refinancingCost,
      string curLevel,
      double[] usage,
      double[] endDistribution,
      MarketQuote quote,
      CalibrationType calType,
      double knownMarketSpread)
      : this(loan, asOf, settle, totalCommitment, drawnCommitment, useDrawnNotional,
        discCurve, refCurve, survCurve, recovCurve, prepayCurve, 
        volatilityCurve, refinancingCost, curLevel,
        usage, endDistribution, quote, calType, knownMarketSpread, 0)
    {
    }


    /// <summary>
    /// Constructor.
    /// </summary>
    public LoanPricer(
      Loan loan,
      Dt asOf,
      Dt settle,
      double totalCommitment,
      double drawnCommitment,
      bool useDrawnNotional,
      DiscountCurve discCurve,
      DiscountCurve refCurve,
      SurvivalCurve survCurve,
      RecoveryCurve recovCurve,
      SurvivalCurve prepayCurve,
      VolatilityCurve volatilityCurve,
      double refinancingCost,
      string curLevel,
      double[] usage,
      double[] endDistribution,
      MarketQuote quote,
      CalibrationType calType,
      double knownMarketSpread,
      double rateVol)
      : base(loan, asOf, settle)
    {
      // Assignments
      Notional = totalCommitment;
      drawnCommitment_ = drawnCommitment;
      useDrawnNotional_ = useDrawnNotional;
      discountCurve_ = discCurve;
      referenceCurve_ = refCurve;
      survivalCurve_ = survCurve;
      recoveryCurve_ = recovCurve;
      prepaymentCurve_ = prepayCurve;
      volatilityCurve_ = volatilityCurve;
      refinancingCost_ = refinancingCost;
      currentLevel_ = curLevel;
      usage_ = usage;
      endDistribution_ = endDistribution;
      marketQuote_ = quote;
      calibrationType_ = calType;
      marketSpread_ = knownMarketSpread;
      rateVol_ = rateVol;
      floorLevel_ = loan.LoanFloor;
      
      // Default initializations
      interestPeriods_ = new List<InterestPeriod>();
      fullDrawOnDefault_ = true;

      // Find the index
      currentLevelIdx_ = ((IList<string>)loan.PerformanceLevels).IndexOf(currentLevel_);

      // Auto-calculate the end distribution if not specified
      if (!ArrayUtil.HasValue(endDistribution_))
        endDistribution_ = LoanModel.CalculateEndDistribution(loan.PerformanceLevels, curLevel);
      if (!ArrayUtil.HasValue(usage_))
        usage_ = ArrayUtil.NewArray(loan.PerformanceLevels.Length, 
          drawnCommitment / totalCommitment);
    }
    #endregion

    #region Methods

    #region Pricing
    /// <summary>
    /// Calculate the present value (full price <formula inline = "true">\times </formula>Notional) of the expected 
    /// future cash flows. 
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Cash flows after the settlement date are projected 
    /// probabilistically across the pricing grid (if 
    /// applicable) at each coupon date and discounted back to the pricing date.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.ModelFullPrice"/>
    /// <seealso cref="LoanPricer.ModelFlatPrice"/>
    /// 
    /// <returns>Present value of the expected cashflows to the pricing date</returns>
    /// 
    public override double ProductPv()
    {
      return this.Pv(true);
    }

    /// <summary>
    /// Calculate the present value (full price 
    /// <formula inline = "true">\times </formula>Notional) of the expected 
    /// future cash flows. 
    /// </summary>
    /// 
    /// <param name="allowPrepayment">Whether to include the 
    /// possibility the Loan prepays</param>
    /// 
    /// <remarks>
    /// <para>Cash flows after the settlement date are projected 
    /// probabilistically across the pricing grid (if 
    /// applicable) at each coupon date and discounted back to the 
    /// pricing date.</para>
    /// <para>Turning off prepayment allows the model to calculate 
    /// the present value of a theoretical Loan that 
    /// is not allowed to prepay.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.ModelFullPrice"/>
    /// <seealso cref="LoanPricer.ModelFlatPrice"/>
    /// 
    /// <returns>Present value of the expected cashflows to the pricing date</returns>
    /// 
    public double Pv(bool allowPrepayment)
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return 0.0;

      // PV
      return (LoanModel.Pv(
        this.AsOf, this.Settle, Loan.GetScheduleParams(), Loan.DayCount,
        Loan.IsFloating, CurrentLevel, currentLevelIdx_, this.Loan.PerformanceLevels,
        Loan.PricingGrid, Loan.CommitmentFee, this.Usage, this.PerformanceLevelDistribution,
        this.DiscountCurve, this.ReferenceCurve, GetSurvivalCurve(),
        this.RecoveryCurve, this.VolatilityCurve, this.FullDrawOnDefault,
        allowPrepayment, this.PrepaymentCurve, this.RefinancingCost,
        AdjustedAmortization, this.InterestPeriods, CalibrationType,
        marketSpread_, this.UseDrawnNotional,
        this.DrawnCommitment/this.TotalCommitment, FloorValues,
        CurrentFloatingCoupon()) + ModelBasis)*this.DrawnCommitment*NotionalFactorAt(Settle);
    }

    /// <summary>
    ///   Calculate the full model price as a percentage of drawn notional
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Cash flows after the settlement date are projected 
    /// probabilistically across the pricing grid (if 
    /// applicable) at each coupon date and discounted back to the pricing date.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.ModelFlatPrice"/>
    /// <seealso cref="LoanPricer.Pv"/>
    /// <seealso cref="LoanPricer.AccruedInterest"/>
    /// <seealso cref="LoanPricer.CreditForUndrawnCommitment"/>
    /// 
    /// <returns>Present value to the settlement date of the 
    /// Loan as a percentage of current Notional</returns>
    ///
    public double ModelFullPrice()
    {
      if (Loan.LoanType == LoanType.Revolver && UseDrawnNotional == false)
      {
        double pv = ProductPv();
        double ai = AccruedInterest();
        double res = (pv + (TotalCommitment - DrawnCommitment) * (1 + ai)) / TotalCommitment;
        return res;
      }
      else if (Loan.LoanType == LoanType.Term)
        return ProductPv() / (TotalCommitment * NotionalFactorAt(Settle));
      else
        return ProductPv()/DrawnCommitment;
    }

    /// <summary>
    /// Calculate the flat model price (<see cref="ModelFullPrice">Full Price</see> - 
    /// <see cref="AccruedInterest">Accrued</see>) as a percentage of the drawn notional.
    /// </summary>
    /// 
    /// <seealso cref="LoanPricer.ModelFullPrice"/>
    /// <seealso cref="LoanPricer.Pv"/>
    /// <seealso cref="LoanPricer.AccruedInterest"/>
    /// 
    /// <returns>Price (without accrued)</returns>
    /// 
    public double ModelFlatPrice()
    {
      return this.ModelFullPrice() - this.AccruedInterest();
    }

    /// <summary>
    /// The full price as quoted in the market (<see cref="MarketFlatPrice">Flat Price</see> + 
    /// <see cref="AccruedInterest">Accrued</see>).
    /// </summary>
    /// 
    /// <returns>Price (including accrued)</returns>
    /// 
    public double MarketFullPrice()
    {
      return MarketPrice(true);
    }

    /// <summary>
    /// The credit for undrawn commmitment
    /// </summary>
    /// <remarks>
    /// <para>When there is an undrawn portion under the revolving facility, the buyer will 
    /// receive a credit for the undrawn portion equal to the undrawn amount 
    /// multiplied by 1 minus the purchase rate. 
    /// The buyer receives a credit because at some point in the future, 
    /// if and when the borrower is able to borrow the undrawn amount 
    /// under the revolving facility, the buyer will be required to lend 
    /// funds to the borrower at 100 cents on the dollar. 
    /// Providing the buyer with a credit on the undrawn portion at the 
    /// time of the assignment is advance compensation for this possibility</para>
    /// </remarks>
    /// <returns>Credit for undrawn commmitment</returns>
    /// 
    public double CreditForUndrawnCommitment(double price)
    {
      double credit = (Loan.LoanType == LoanType.Revolver && UseDrawnNotional == false)
        ? (1.0 - price)*(TotalCommitment - DrawnCommitment)
        : 0;
      return credit;
    }

    /// <summary>
    /// Adjust loan price to account for Undrawn commmitment
    /// </summary>
    /// 
    /// <see cref="CreditForUndrawnCommitment"/>
    /// 
    /// <returns>Adjusted loan price</returns>
    /// 
    public double PriceAdjustedForUndrawnCommitment(double price)
    {
      double refPrice = 0;
      
      if (price.ApproximatelyEqualsTo(MarketFullPrice()))
        refPrice = MarketFlatPrice();
      else
        refPrice = ModelFlatPrice();

      return price - CreditForUndrawnCommitment(refPrice) / DrawnCommitment;
    }

    /// <summary>
    /// The market value of the loan (<see cref="MarketFlatPrice">Flat Price</see> + 
    /// </summary>
    /// /// <remarks>
    /// <para>The market value of a revolver loan is the nominal market value minus the credit 
    /// to compensate for risk due to the future draw. </para>
    /// </remarks>
    /// 
    /// <see cref="CreditForUndrawnCommitment"/>
    /// 
    /// <returns>Market value (including accrued)</returns>
    /// 
    public double MarketValue()
    {
      return MarketFullPrice() * DrawnCommitment * NotionalFactorAt(Settle) 
        - CreditForUndrawnCommitment(MarketFlatPrice());
    }

    /// <summary>
    /// The flat price as quoted in the market.
    /// </summary>
    /// 
    /// <returns>Price (excluding accrued)</returns>
    /// 
    public double MarketFlatPrice()
    {
      return MarketPrice(false);
    }

    /// <summary>
    /// The theoretical market price of a Loan with no prepayment optionality.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>The estimated price of Loan with no prepayment option (referred to as a Bond)
    /// as it would trade in the market. Calculated by adding 
    /// the <see cref="MarketFullPrice">Market Full Price</see> 
    /// and the Model's <see cref="OptionPrice">Option Price</see>.</para>
    /// </remarks>
    /// 
    /// <returns>Price</returns>
    /// 
    public double TheoreticalBondMarketFullPrice()
    {
      return MarketFullPrice() + OptionPrice();
    }

    /// <summary>
    ///   Total dollar accrued for the product to pricing settlement date given pricing arguments
    /// </summary>
    /// 
    /// <remarks>Calculated based on the prior coupon date or the Loan's effective date. This 
    /// does not take into account any traded date.</remarks>
    /// 
    /// <seealso cref="AccruedInterest"/>
    ///
    /// <returns>Total dollar accrued interest</returns>
    ///
    public override double Accrued()
    {
      if (Loan.LoanType == LoanType.Term)
        return AccruedInterest() * this.TotalCommitment * NotionalFactorAt(Settle);
      return this.AccruedInterest() * this.DrawnCommitment;
    }

    /// <summary>
    ///   Calculate accrued interest on the specified settlement date as a percentage of face.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates accrued interest on the specified settlement date.</para>
    ///   <para>Note does not support forward settlement or historical settlement 
    ///   when the rate resets have note been set.</para> 
    ///   <para>Calculated based on the prior coupon date or the Loan's effective date. This 
    ///   does not take into account any traded date.</para>
    /// </remarks>
    /// 
    /// <seealso cref="Accrued"/>
    ///
    /// <returns>Accrued interest on the settlement date</returns>
    ///
    public double AccruedInterest()
    {
      // Calc and cache
      if (accruedInterest_ == null)
      {
        // Handle matured Loans
        if (Settle < Loan.Maturity)
        {
          if (Loan.LoanType == LoanType.Term)
            accruedInterest_ = AccruedInterestForTermLoan();
          else
            accruedInterest_ = LoanModel.Accrued(Settle, Loan.GetScheduleParams(), 
              Loan.DayCount, Loan.IsFloating, CurrentSpread,
              Loan.CommitmentFee, this.InterestPeriods);
        }
        else
          accruedInterest_ = 0.0;
      }

      // Done
      return (double)accruedInterest_;
    }

    /// <summary>
    /// The current coupon
    /// </summary>
    /// <returns></returns>
    public double CurrentCoupon()
    {
      double cpn = CurrentSpread;
      if (!Loan.IsFloating)
      {
        return cpn;
      }
      else if (Loan.LoanType == LoanType.Term)
      {
        ScheduleParams scheduleParams = Loan.GetScheduleParams();
        InterestPeriod ip = InterestPeriodUtil.DefaultInterestPeriod(Settle,
          scheduleParams, DayCount.None, 0.0, 0.0);
        Dt periodStart = ip.StartDate;
        Dt nextCouponDate = ip.EndDate;
        // In this case, cpn is the spread over LIBOR
        double rate = CurrentFloatingRateForTermLoan(periodStart, nextCouponDate, cpn);
        return rate;
      }
      else
      {
        var cp = InterestPeriodUtil.InterestPeriodsForDate(Settle, InterestPeriods).OrderByDescending(ip => ip.StartDate).FirstOrDefault();

        // Validate we have at least 1 interest period
        if (cp == null)
          throw new ToolkitException("You must specify at least 1 Interest Period for a Floating Rate Loan!");

          return cp.AnnualizedCoupon;
      }
    }

    /// <summary>
    ///   Calculate accrued interest for a Term Loan on the specified 
    /// settlement date, as a percentage of face.
    /// </summary>
    private double AccruedInterestForTermLoan()
    {
      ScheduleParams scheduleParams = Loan.GetScheduleParams();
      InterestPeriod ip = InterestPeriodUtil.DefaultInterestPeriod(Settle, 
        scheduleParams, DayCount.None, 0.0, 0.0);
      if (ip == null)
      {
        return 0.0;
      }
      Dt periodStart = ip.StartDate;
      Dt nextCouponDate = ip.EndDate;
      DayCount dayCount = Loan.DayCount;
      double cpn = CurrentSpread;
      double accrualFactor = Schedule.Fraction(periodStart, Settle, dayCount);
      // Handle fixed rate loans first
      if (!Loan.IsFloating)
      {
        return cpn * accrualFactor;
      }
      // In this case, cpn is the spread over LIBOR
      double rate = CurrentFloatingRateForTermLoan(periodStart, nextCouponDate, cpn);  
      return rate * accrualFactor;
    }

    /// <summary>
    /// Notional factor as of a specific date, based on amortization schedule.
    /// </summary>
    private double NotionalFactorAt(Dt date)
    {
      // TODO: check this for the Revolver loans
      if (Loan.LoanType != LoanType.Term || !Loan.IsAmortizing) return 1.0; 
      // For the Term loan, the assumption is that the full amount of 
      //commitment is drawn, and then amortization applies, if any.
      double fact = 1.0;
      foreach (Amortization item in Loan.AmortizationSchedule)
      {
        if (item.Date <= date)
          fact -= item.Amount;
      }
      if (fact < 0) fact = 0; // just in case we end up with numerical error and small number < 0
      return fact;
    }

    /// <summary>
    /// The single rate that discounts the expected future cash 
    /// flows to match the <see cref="MarketFullPrice">Market Full Price</see>.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Calculates the single (flat) rate that causes the present 
    /// value of the 
    /// expected future cashflows to match the 
    /// <see cref="MarketFullPrice">Market Full Price</see>. 
    /// Future cash flows are based on the forward rate 
    /// <see cref="ReferenceCurve">Reference Curve</see> and 
    /// the expected coupons across all performance levels.</para>
    /// </remarks>
    /// 
    /// <returns>The public rate of return</returns>
    /// 
    public double Irr()
    {
      return Irr(MarketFullPrice(), false);
    }

    /// <summary>
    /// Calculate the single rate that discounts the expected future cash 
    /// flows to match the given price.
    /// </summary>
    /// 
    /// <param name="price">The price to match.</param>
    /// <param name="allowPrepay">Whether to allow prepayment or not.</param>
    /// 
    /// <remarks>
    /// <para>Calculates the single (flat) rate that causes the model price to match the 
    /// given price. Future cash flows are based on the forward rate 
    /// <see cref="ReferenceCurve">Reference Curve</see> and the expected coupons across 
    /// all performance levels.</para>
    /// </remarks>
    /// 
    /// <returns>The public rate of return</returns>
    /// 
    public double Irr(double price, bool allowPrepay)
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity)
        || (this.Settle == this.Loan.Maturity))
        return 0.0;

      DiscountCurve newCurve = new DiscountCurve(
        (DiscountCalibrator)DiscountCurve.Calibrator.Clone(),
        DiscountCurve.Interp, Loan.DayCount, Loan.Frequency);

      // calculate Current Libor and Libor_Stub for the term strucure 
      newCurve.Add(Loan.Maturity, 1.0);

      // Setup empty survival curve
      SurvivalCurve sc = new SurvivalCurve(AsOf, 0);

      // adjust for the undrawn portion of the loan
      price = PriceAdjustedForUndrawnCommitment(price);

      // Solve for spread
      double irr = LoanModel.ImpliedDiscountSpread(AsOf, Settle,
        Loan.GetScheduleParams(), Loan.DayCount, newCurve,
        Loan.IsFloating, this.ReferenceCurve, sc, RecoveryCurve,
        FullDrawOnDefault, allowPrepay, PrepaymentCurve,
        VolatilityCurve, RefinancingCost, CurrentLevel,
        currentLevelIdx_, Loan.PerformanceLevels, Loan.PricingGrid,
        Loan.CommitmentFee, Usage, PerformanceLevelDistribution,
        AdjustedAmortization, InterestPeriods, price, UseDrawnNotional,
        DrawnCommitment/TotalCommitment, FloorValues, CurrentFloatingCoupon());

      // Done
      return irr;
    }
    #endregion

    #region Prepayment
    /// <summary>
    /// The present value (in total dollars) of the borrower's option to prepay the Loan.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>There are two primary reasons for prepaying the Loan. First, a borrower may 
    /// be able to refinance the Loan at a cheaper rate, thus lowering their interest expense. 
    /// Second, a borrower may have taken the Loan for a specific purpose or project which 
    /// may have completed or been cancelled prior to the Loan's agreed maturity. </para>
    /// <para>The value of the embedded option considers both types of prepayment. Due 
    /// to the floating interest rate nature of most Loans the option is a call against 
    /// the credit spreads of the borrower (credit quality, market cost of credit risk, etc.).</para>
    /// <para>Calculated as the difference between the <see cref="Pv(bool)">PV
    ///  without prepayment</see> and 
    /// the <see cref="Pv">PV with prepayment</see>.</para>
    /// </remarks>
    /// 
    /// <seealso cref="Pv" />
    /// <seealso cref="Pv(bool)" />
    /// <seealso cref="OptionPrice" />
    /// <seealso cref="OptionSpreadValue" />
    /// 
    /// <returns>Present value of the borrower's option to call (prepay) the Loan</returns>
    /// 
    public double OptionPv()
    {
      // done
      if(ToolkitConfigurator.Settings.LoanModel.AllowNegativeOptionValue)
        return Pv(false) - Pv(true);
      else 
        return Math.Max(Pv(false) - Pv(true), 0);
    }

    /// <summary>
    /// The price of the borrower's option to prepay the Loan
    ///  as a percentage of the drawn notional.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Calculated as:</para>
    /// 
    /// <formula>Price_{Option} = \frac{Option PV}{Drawn Commitment}</formula>
    /// </remarks>
    /// 
    /// <seealso cref="EffectiveNotional" />
    /// <seealso cref="OptionPv" />
    /// <seealso cref="OptionSpreadValue" />
    ///  
    /// <returns>Price of the borrower's option to call (prepay) the Loan</returns>
    /// 
    public double OptionPrice()
    {
      return OptionPv() / this.EffectiveNotional;
    }

    /// <summary>
    /// The spread value of the borrower's option to prepay.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Calculated as the difference between the 
    /// <see cref="Irr(double,bool)">IRR</see> implied by the 
    /// <see cref="MarketFullPrice">Market Full Price</see> 
    /// and the <see cref="Irr(double,bool)">IRR</see> implied 
    /// by the <see cref="TheoreticalBondMarketFullPrice">
    /// Theoretical Bond Price</see>.</para>
    /// <para>This is the spread that compensates the lender 
    /// for the borrower's right to prepay the Loan.</para>
    /// </remarks>
    /// 
    /// <seealso cref="Irr()"/>
    /// <seealso cref="Irr(double,bool)"/>
    /// <seealso cref="TheoreticalBondMarketFullPrice" />
    /// <seealso cref="MarketFullPrice" />
    /// 
    /// <returns>Option adjusted spread</returns>
    /// 
    public double OptionSpreadValue()
    {
      // OAS is the difference between ZSpread of Loan at theoretical bond price and 
      // ZSpread of Loan at market price
      return Irr(MarketFullPrice(), false) - Irr(TheoreticalBondMarketFullPrice(), false);
    }
    #endregion

    #region Risk
    /// <summary>
    /// The Loan's sensitivity to a change in interest rates.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is the interest rate sensitivity to a 1bp increase in swap rates
    ///   as a percentage of notional. The applied bump size is 25 bps and the result is scaled
    ///   back per bp. 
    ///  </para>
    ///  <para>Since most loans have floating interest rate features this is typically a very 
    ///   small risk factor.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.Pv"/>
    /// <seealso cref="Sensitivities.IR01(IPricer,double,double,bool,bool[])"/>
    ///
    /// <returns>Interest rate sensitivity</returns>
    ///
    public double Rate01()
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity) || (this.Settle == this.Loan.Maturity))
        return 0.0;

      double ir01;
      double dY = 25; //bps

      // Delta
      ir01 = Sensitivities.IR01(this, dY, dY, false);

      // Done
      return ir01;
    }

    /// <summary>
    ///   The Loan's effective interest rate convexity.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is the interest rate convexity of the Loan. Together, the duration and convexity estimate
    ///   the percentage change in the Loan price following a parallel shift of the swap rate curve as:
    ///   <formula>
    ///     C = \frac {\delta{^2P}}{\delta{r}^2} * \frac{1}{P}
    ///   </formula>
    ///   where <formula inline="true">\delta{Y}</formula> is the shift in percentage points to the swap rates, i.e. a 25bps shift is 
    ///   <formula inline="true">\delta{Y} = 0.25</formula>. The convexity is calculated from the Loan values when the swap rates are shifted 
    ///   up 25bps and shifted down 25bps using the Loan price (no shift).
    ///   The results are scaled back per bp.
    ///   </para>
    ///  <para>Since most loans have floating interest rate features this is typically a very 
    ///   small risk factor.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.RateDuration"/>
    /// <seealso cref="LoanPricer.Rate01"/>
    /// <seealso cref="Sensitivities.Rate(IPricer,double,double,double,bool,bool,BaseEntity.Toolkit.Sensitivity.BumpType,string[],bool,bool,string,bool,System.Data.DataTable,bool[])"/>
    ///
    /// <returns>Interest Rate Convexity</returns>
    ///
    public double RateConvexity()
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity) || (this.Settle == this.Loan.Maturity))
        return 0.0;

      double d2PdY2;
      double dY = 25; //bps
      double origSpread = DiscountCurve.Spread;

      // Calculate the 2nd derivative
      DataTable tbl = Sensitivities.Rate(new[] {this}, null, 0, dY, dY, false,
        true, BumpType.Uniform, null, true, false, null, false, null, null,
        "Keep", null);
      d2PdY2 = (double)tbl.Rows[0]["Gamma"] * 10000.0 * 10000.0;

      // Convexity
      return d2PdY2 / (this.EffectiveNotional * this.ModelFullPrice());
    }

    /// <summary>
    ///   Calculate the Loan effective interest rate duration.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is the interest rate duration of the Loan. The duration is calculated using the Loan values
    ///   arising when the Libor curve is shifted up 1bp and down 1bp. The applied shift is 25bps and the result
    ///   is scaled back per bp.
    ///  </para>
    ///  <para> 
    ///   <formula>
    ///     D = \frac {\delta{P}}{\delta{r}} * \frac{1}{P}
    ///   </formula>
    ///  </para>
    ///  <para>Since most loans have floating interest rate features this is typically a very 
    ///   small risk factor.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.RateConvexity"/>
    /// <seealso cref="LoanPricer.Rate01"/>
    ///
    /// <returns>Interest rate duration</returns>
    ///
    public double RateDuration()
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity) || (this.Settle == this.Loan.Maturity))
        return 0.0;

      return -10000.0 * this.Rate01() / this.EffectiveNotional / this.ModelFullPrice();
    }

    /// <summary>
    ///   The Loan's sensitivity to credit spreads.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The spread sensitivity is the loan (dollar) price change (using loan drawn notional) per 1bp decrease  
    ///   in the Loan's CDS spread. The spread sensitivity is calculated using the loan values arising when the
    ///   Loan CDS curve is parallel shifted up 5bp and down 5bp and the results are scaled back per bp. 
    ///   The Libor Curve is kept unchanged when the CDS curve is shifted.</para>
    ///   <para>For Loans without liquid CDS trading the <see cref="ImpliedFlatCDSCurve()">Implied Flat CDS Curve</see> 
    ///   is shifted.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.Pv"/>
    /// <seealso cref="ImpliedFlatCDSCurve()" />
    ///
    /// <returns>Credit spread sensitivity</returns>
    ///

    public double Spread01()
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity) || (this.Settle == this.Loan.Maturity))
        return 0.0;

      double s01;
      double dY = 5; //bps
      BumpFlags bumpFlags = BumpFlags.BumpInPlace;

      // Delta
      s01 = Sensitivities2.Spread01(this, null, dY, dY, bumpFlags);

      // Done
      return s01;
    }

    /// <summary>
    /// The Loan's spread duration.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>
    ///   The spread duration gives the percentage increase in the Loan price per 1bp decrease 
    ///   in the Loan CDS spread. The spread duration is calculated using the Loan values arising when the
    ///   Loan CDS curve is parallel shifted up 5bp and down 5bp and results are scaled back per bps. 
    ///   The Libor Curve is kept unchanged when the CDS curve is shifted.
    ///   </para>
    ///  <para> 
    ///   <formula>
    ///     D = \frac {\delta{P}}{\delta{S}} * \frac{1}{P}
    ///   </formula>
    ///  </para>
    ///   <para>For Loans without liquid CDS trading the 
    ///   <see cref="LoanPricer.ImpliedFlatCDSCurve()">Implied Flat CDS Curve</see> 
    ///   is shifted.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.Spread01"/>
    /// <seealso cref="LoanPricer.SpreadConvexity"/>
    ///
    /// <returns>loan Spread Duration</returns>
    ///
    public double SpreadDuration()
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity) || (this.Settle == this.Loan.Maturity))
        return 0.0;

      if (this.SurvivalCurve == null)
        throw new ToolkitException("Must specify survival curve to calculate SpreadDuration");

      return -10000.0 * this.Spread01() / this.EffectiveNotional / this.ModelFullPrice();
    }

    /// <summary>
    ///   The spread convexity of the Loan.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>
    ///   <formula>
    ///     C = \frac {\delta{^2P}}{\delta{S}^2} * \frac{1}{P}
    ///   </formula>
    ///   </para>
    ///   <para>Calculated with 25bp up bump, 25bp down bump, and scaled back per bp</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.SpreadDuration"/>
    /// <seealso cref="LoanPricer.Spread01"/>
    /// <seealso cref="Sensitivities.SpreadGamma(IPricer,double,double,bool[])" />
    /// 
    /// <returns>Spread Convexity of the Loan</returns>
    ///

    public double SpreadConvexity()
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity) || (this.Settle == this.Loan.Maturity))
        return 0.0;

      if (this.SurvivalCurve == null)
        throw new ToolkitException("Must specify survival curve to calculate SpreadConvexity");

      double d2PdY2;
      double dY = 25; //bps
      BumpFlags bumpFlags = BumpFlags.BumpInPlace;

      // 2nd Derivative
      double gamma = Sensitivities2.SpreadGamma(this, null, dY, dY, bumpFlags);
      d2PdY2 = gamma * 10000.0 * 10000.0;

      // Convexity
      return d2PdY2 / (this.EffectiveNotional * this.ModelFullPrice());
    }

    #endregion

    #region Misc

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some public state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that all internate states should be cleared or updated.</para>
    ///   <para>Derived Pricers may implement this and should call base.Reset()</para>
    /// </remarks>
    public override void Reset()
    {
      base.Reset();
      schedule_ = null;
      stubRate_= nextCouponDate_= currentFloatingRate_= 
        accruedInterest_= marketFlatPrice_ = null;
      adjustedAmortization_ = null;
      currentFloatingCoupon_ = null;
    }

    public void ResetAfterTheta()
    {
      interestPeriodsForTheta_ = null;     
    }

    /// <summary>
    /// Calibrates the market spread.
    /// </summary>
    public void Calibrate()
    {
      marketSpread_ = MarketSpread(CalibrationType);
    }

    /// <summary>
    /// The distribution of the Loan's performance across time and performance level. 
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Here, the first dimension is time and the second dimension is performance 
    /// level with prepayment at index 0, in order according to the Loan's performance levels, 
    /// and with default at the final index.</para></remarks>
    /// 
    /// <returns>Distribution array.</returns>
    /// 
    public double[,] PerformanceDistribution()
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return new double[0,Loan.PerformanceLevels.Length];

      // Calc Distribution
      return LoanModel.Distribution(
        AsOf, Settle, Loan.GetScheduleParams(), GetSurvivalCurve(), 
        PrepaymentCurve, VolatilityCurve,
        currentLevelIdx_, PerformanceLevelDistribution, 
        CalibrationType, marketSpread_);
    }

    
    /// <summary>
    /// The transition matrix for the Loan on a given date.
    /// </summary>
    /// 
    /// <param name="date">The date</param>
    /// 
    /// <remarks><para>Here, the order is prepayment in the first index, then according to the Loan's 
    /// performance levels, with default being last.</para></remarks>
    /// 
    /// <returns>Transition matrix</returns>
    /// 
    public double[,] TransitionMatrix(Dt date)
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return new double[Loan.PerformanceLevels.Length+2, 
          Loan.PerformanceLevels.Length+2];

      // Calc Distribution
      return LoanModel.TransitionMatrix(
        date, AsOf, Settle, Loan.GetScheduleParams(), GetSurvivalCurve(),
        PrepaymentCurve, VolatilityCurve, currentLevelIdx_, 
        PerformanceLevelDistribution, CalibrationType, marketSpread_);
    }

    /// <summary>
    /// The matrix of forward values of the Loan at each payment date conditional on performing at each level.
    /// </summary>
    /// 
    /// <param name="allowPrepayment">Whether to allow prepayment when calculating the forward values.</param>
    /// 
    /// <returns>Forward Value Matrix</returns>
    /// 
    public double[,] ForwardValues(bool allowPrepayment)
    {
      return LoanModel.ForwardValues(AsOf, Settle, Loan.GetScheduleParams(),
        Loan.DayCount,
        Loan.IsFloating,
        CurrentLevel, currentLevelIdx_,
        Loan.PerformanceLevels, Loan.PricingGrid, Loan.CommitmentFee,
        Usage, PerformanceLevelDistribution,
        DiscountCurve, ReferenceCurve, GetSurvivalCurve(), 
        RecoveryCurve, VolatilityCurve,
        FullDrawOnDefault, allowPrepayment, PrepaymentCurve,
        RefinancingCost, AdjustedAmortization, InterestPeriods, 
        FloorValues, CurrentFloatingCoupon());
    }
    #endregion

    #region Payment Schedule Generation

    public PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from, Dt callDate)
    {
      var loan = Loan;
      var schedule = loan.Schedule;
      var rateResets = (loan.LoanType == LoanType.Term
        ? loan.GetAllRateResets(AsOf, Settle, loan.Schedule, InterestPeriods)
        : InterestPeriodUtil.TransformToRateReset(from, loan.Effective, InterestPeriods));
      if (callDate != Dt.Empty)
        schedule = Schedule.CreateScheduleForCashflowFactory(
          loan.GetScheduleParamsToCall(callDate));
      return loan.GetPaymentSchedule(ps, from, callDate, DiscountCurve,
        ReferenceCurve, schedule, new RateResets(rateResets), CurrentLevel,
        InterestPeriods, FloorValues, DrawnCommitment / TotalCommitment,
        Dt.Empty, Dt.Empty, RecoveryRate);
    }

    #endregion Payment Schedule Generation

    #region Implied Curves
    /// <summary>
    ///   The implied flat CDS curve for the market price.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Implies a flat CDS curve from the <see cref="MarketFullPrice">Market Full Price</see> 
    ///     and the Loan's <see cref="RecoveryRate">Recovery Rate</see>.</para>
    /// 
    ///   <para>The implied curve is the CDS Curve with a CDS maturing at the 
    ///   <see cref="Product.Maturity">Loan's Maturity</see> with a <see cref="CDS.Premium">Premium</see> of the 
    ///   <see cref="CDSLevel()">CDS Level</see> assumed to recover the Loan's 
    ///   <see cref="RecoveryRate">Recovery</see> on a default.</para>
    /// 
    ///   <para>This function does not require an existing survival curve and does not replace the existing 
    ///   <see cref="SurvivalCurve">Survival Curve</see>.</para>
    /// </remarks>
    /// 
    /// <seealso cref="BaseEntity.Toolkit.Curves.SurvivalCurve" />
    /// <seealso cref="CDS" />
    /// <seealso cref="MarketFullPrice()"/>
    /// <seealso cref="CDSLevel()"/>
    /// <seealso cref="ImpliedFlatCDSCurve(double)" />
    /// <seealso cref="RecoveryRate" />
    /// <seealso cref="SurvivalCurve" />
    ///
    /// <returns>Implied SurvivalCurve fitted from the market price and Loan recovery</returns>
    ///
    public SurvivalCurve ImpliedFlatCDSCurve()
    {
        return ImpliedFlatCDSCurve(MarketFullPrice());
    }

    /// <summary>
    ///   The Implied flat CDS Curve from a given full price.
    /// </summary>
    ///
    /// <param name="price">The price to match</param>
    ///  
    /// <remarks>
    ///   <para>Implies a flat CDS curve from the given price and the Loan's 
    ///   <see cref="RecoveryRate">Recovery Rate</see>.</para>
    /// 
    ///   <para>This function does not require an existing survival curve and does not replace the existing 
    ///   <see cref="SurvivalCurve">Survival Curve</see>.</para>
    /// </remarks>
    /// 
    /// <seealso cref="BaseEntity.Toolkit.Curves.SurvivalCurve" />
    /// <seealso cref="CDS" /> 
    /// <seealso cref="RecoveryRate" />
    /// <seealso cref="SurvivalCurve" />
    ///
    /// <returns>Implied Survival Curve fitted from a full price and the Loan recovery rate</returns>
    ///
    public SurvivalCurve ImpliedFlatCDSCurve(double price)
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity)
        || (this.Settle == this.Loan.Maturity))
        return null;

      // adjust for the un-drawn part
      price = PriceAdjustedForUndrawnCommitment(price);

      // Calculate
      SurvivalCurve sc = LoanModel.ImpliedCDSCurve(AsOf, Settle,
        Loan.GetScheduleParams(), Loan.DayCount, DiscountCurve, Loan.IsFloating,
        ReferenceCurve, RecoveryCurve, FullDrawOnDefault,
        PrepaymentCurve, VolatilityCurve, RefinancingCost, CurrentLevel,
        currentLevelIdx_, Loan.PerformanceLevels, Loan.PricingGrid, Loan.CommitmentFee, Usage,
        PerformanceLevelDistribution, AdjustedAmortization, InterestPeriods, price, UseDrawnNotional,
        DrawnCommitment/TotalCommitment, FloorValues, CurrentFloatingCoupon());

      // Set default name
      if (sc != null)
        sc.Name = Loan.Description + "_Implied";

      // Done
      return sc;
    }

    /// <summary>
    ///   The implied flat prepayment curve from a given expected weighted average life (WAL).
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Implies a flat prepayment curve from the given 
    ///   <see cref="ExpectedWAL">Expected Weighted Average Life</see>.</para>
    /// 
    ///   <para>This function does not require an existing prepayment curve and does not replace the 
    ///   current <see cref="PrepaymentCurve">Prepayment Curve</see>.</para>
    /// </remarks>
    ///
    /// <param name="wal">The Expected WAL to match</param>
    /// 
    /// <seealso cref="BaseEntity.Toolkit.Curves.SurvivalCurve" />
    /// <seealso cref="LoanPricer.WAL"/>
    /// <seealso cref="LoanPricer.ExpectedWAL"/>
    ///
    /// <returns>Implied prepayment curve fitted from a WAL</returns>
    ///
    public SurvivalCurve ImpliedFlatPrepaymentCurve(double wal)
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity)
        || (this.Settle == this.Loan.Maturity))
        return null;

      // Calculate
      SurvivalCurve pc = LoanModel.ImpliedPrepaymentCurve(AsOf, Settle,
        Loan.GetScheduleParams(), Loan.Ccy, Loan.DayCount,
        AdjustedAmortization, EffectiveNotional, wal);

      // Set default name
      if (pc != null)
        pc.Name = Loan.Description + "_Prepay";

      // Done
      return pc;
    }
    #endregion

    #region Credit Spreads
    /// <summary>
    /// Calculate the spread (yield) over today's rate fixing implied by the full market price of the loan.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Calculates the fixed add-on to the current LIBOR rate (the 3 month LIBOR if the 
    ///   Loan frequency is 3 months) that is required to reprice the loan in the market.</para>
    ///
    ///   <para>Discount margin measures yield relative to the current LIBOR level and does not take
    ///   into account the term structure of interest rates. It is in some sense analogous to the YTM 
    ///   of a fixed rate instrument.</para>
    ///
    ///   <para>The expected cashflows of an Loan are ususally based on forward LIBOR rates. For the Discount Margin 
    ///   calculation it is assumed that all future realises LIBOR rates are equal to 
    ///   the current LIBOR rate ("Assumed Rate" in Bloomberg) . Future cashflows/coupons are therefore 
    ///   LIBOR + spread except the cashflows for the known <see cref="InterestPeriods">Interest Periods</see>.</para>
    ///
    ///   <para>The cashflow for the first coupon date is discounted using the LIBOR fix rate ("Index To" in Bloomberg).</para>
    /// 
    ///   <para>Note, this calculation does not consider the embedded credit spread option.</para>
    /// </remarks>
    ///
    /// <returns>The spread over today's rate fixing implied by price.</returns>
    /// 
    public double DiscountMargin()
    {
      return DiscountMargin(StubRate(), CurrentFloatingRate(), MarketFullPrice(), false);
    }

    /// <summary>
    /// Calculate the spread (yield) over today's rate fixing implied by the full price of the Loan.
    /// </summary>
    ///
    /// <param name="liborStub">LIBOR from the pricing date to the next coupon date.</param>
    /// <param name="currentLibor">LIBOR from the pricing date to one payment period in the future.</param>
    /// <param name="price">The price to match.</param>
    /// <param name="allowPrepay">Whether to consider the prepayment option.</param>
    /// 
    /// <remarks>
    ///   <para>Calculates the fixed add-on to the current LIBOR rate (the 3 month LIBOR if the 
    ///   Loan frequency is 3 months) that is required to reprice the Loan.</para>
    ///
    ///   <para> Discount margin measures yield relative to the current LIBOR level and does not take
    ///   into account the term structure of interest rates. It is in some sense analogous to the YTM 
    ///   of a fixed rate instrument.</para>
    ///
    ///   <para>The expected cashflows of a Loan are ususally based on forward LIBOR rates. For the 
    ///   Discount Margin calculation it is assumed that all future realised LIBOR rates are equal to 
    ///   the current LIBOR rate ("Assumed Rate" in Bloomberg) . Future cashflows/coupons are therefore 
    ///   LIBOR + spread except the cashflows for the known <see cref="InterestPeriods">Interest Periods</see>.</para>
    ///
    ///   <para>The cashflow for the first coupon date is discounted using the LIBOR fix rate ("Index To" in Bloomberg). 
    ///   Both currentLibor and liborFix are passed in as parameters to the discount margin function</para>
    /// </remarks>
    ///
    /// <returns>The Discount Margin implied by price</returns>
    /// 
    public double DiscountMargin(double liborStub, double currentLibor, 
      double price, bool allowPrepay)
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) 
        || (Settle == Loan.Maturity))
        return 0.0;

      if (!Loan.IsFloating)
        throw new ArgumentException("The Discount Margin calculation " +
                                    "only applies to floating rate loans");

      GetNewCurves(liborStub, currentLibor, out var newCurve, out var newRefCurve);
      // Setup empty survival curve
      SurvivalCurve sc = new SurvivalCurve(AsOf, 0);

      // adjust for the undrawn portion of the loan
      price = PriceAdjustedForUndrawnCommitment(price);

      // Solve for spread
      double discountMargin = LoanModel.ImpliedDiscountSpread(AsOf, Settle, 
        Loan.GetScheduleParams(), Loan.DayCount, newCurve,
        Loan.IsFloating, newRefCurve, sc, RecoveryCurve, FullDrawOnDefault,
        allowPrepay, PrepaymentCurve, VolatilityCurve, RefinancingCost, 
        CurrentLevel, currentLevelIdx_, Loan.PerformanceLevels,
        Loan.PricingGrid, Loan.CommitmentFee, Usage, PerformanceLevelDistribution, 
        AdjustedAmortization,
        InterestPeriods, price, UseDrawnNotional, DrawnCommitment / TotalCommitment, 
        CalcFloorValues, CurrentFloatingCoupon());

      // Done
      return discountMargin;
    }

    /// <summary>
    /// Calculates the spread on the Loan over the current rate fixing 
    /// assuming no prepayment until a full repayment in 2 years.
    /// </summary>
    /// <remarks><para>Note, if the call date is after Loan maturity this 
    /// function returns 0.</para></remarks>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double SpreadToTwoYearCall()
    {
      // Calculate call date
      Dt callDate = Dt.Add(Settle, 2, TimeUnit.Years);

      // If loan matures before then return 0
      if (Loan.Maturity < callDate)
        return 0;

      // Calc
      return SpreadToCall(callDate);
    }

    /// <summary>
    /// Calculates the spread on the Loan over the current rate fixing assuming 
    /// no prepayment until a full repayment in 3 years.
    /// </summary>
    /// <remarks><para>Note, if the call date is after Loan maturity this 
    /// function returns 0.</para></remarks>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double SpreadToThreeYearCall()
    {
      // Calculate call date
      Dt callDate = Dt.Add(Settle, 3, TimeUnit.Years);

      // If loan matures before then return 0
      if (Loan.Maturity < callDate)
        return 0;

      // Calc
      return SpreadToCall(callDate);
    }

    /// <summary>
    /// Calculates the lowest of the spread to maturity (Z-Spread), 
    /// spread to 2 year call, and spread to 3 year call.
    /// </summary>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double SpreadToWorst()
    {
      // Calculate spreads
      double spreadToMaturity = ZSpread();
      double spreadTo2Yrs = SpreadToTwoYearCall();
      double spreadTo3Yrs = SpreadToThreeYearCall();

      // Handle dates
      if (spreadTo2Yrs == 0)
        return spreadToMaturity;
      else if (spreadTo3Yrs == 0)
        return Math.Min(spreadTo2Yrs, spreadToMaturity);
      else
        return Math.Min(spreadToMaturity, Math.Min(spreadTo2Yrs, spreadTo3Yrs));
    }

    /// <summary>
    /// Calculates the spread on the Loan over the current rate fixing 
    /// assuming no prepayment until the call date given and 
    /// full repayment on that date.
    /// </summary>
    /// <remarks><para>Note, if the callDate is after Loan maturity this 
    /// function returns 0.</para></remarks>
    /// <param name="callDate">The call date.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double SpreadToCall(Dt callDate)
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return 0.0;
      if (!Loan.IsFloating)
        throw new ArgumentException("The SpreadToCall calculation only " +
                                    "applies to floating rate loans");
      if (Loan.Maturity < callDate)
        return 0;

      // Create a copy of the discount curve
      DiscountCurve newCurve;
      if (Loan.DayCount == DayCount.ActualActualBond)
        newCurve = new DiscountCurve((DiscountCalibrator)DiscountCurve.Calibrator.Clone(),
        DiscountCurve.Interp, DayCount.Actual365Fixed, Loan.Frequency);
      else
        newCurve = new DiscountCurve((DiscountCalibrator)DiscountCurve.Calibrator.Clone(),
        DiscountCurve.Interp, Loan.DayCount, Loan.Frequency);
      newCurve.Interp = DiscountCurve.Interp;
      for (int i = 0; i < DiscountCurve.Count; ++i)
        newCurve.Add(DiscountCurve.GetDt(i), DiscountCurve.GetVal(i));

      // Setup empty survival curve
      SurvivalCurve sc = new SurvivalCurve(AsOf, 0);

      // Setup schedule for call
      // Note: consider instead the following line of code. But keeping the 
      // original code for now to reduce regressions:
      // ScheduleParams scheduleParams = Loan.GetScheduleParamsToCall(callDate);
      var schedule = new Schedule(Loan.GetScheduleParams());
      var scheduleParams = (ScheduleParams)Loan.GetScheduleParams().Clone();
      scheduleParams.FirstCouponDate = schedule.GetNextCouponDate(Loan.Effective);
      scheduleParams.NextToLastCouponDate = schedule.GetPrevCouponDate(callDate);
      scheduleParams.Maturity = callDate;

      // Solve for spread
      double spreadToCall = LoanModel.ImpliedDiscountSpread(AsOf, Settle, 
        scheduleParams,Loan.DayCount, newCurve, Loan.IsFloating, ReferenceCurve, 
        sc, RecoveryCurve, FullDrawOnDefault, false, null, VolatilityCurve, 
        RefinancingCost, CurrentLevel, 
        currentLevelIdx_, Loan.PerformanceLevels, Loan.PricingGrid,
        Loan.CommitmentFee, Usage, PerformanceLevelDistribution, AdjustedAmortization, 
        InterestPeriods,
        MarketFullPrice(), UseDrawnNotional, DrawnCommitment / TotalCommitment, 
        FloorValues, CurrentFloatingCoupon()); 
      
      // Done
      return spreadToCall;
    }

    /// <summary>Calculate IRR of cashflows using the current market price</summary>
    public double YieldToMaturity(Dt asOf, Dt settle,
      Frequency cmpdFrequency)
    {
      return _usePaymentSchedule
        ? YieldToMaturityPs(asOf, settle, cmpdFrequency)
        : YieldToMaturityCf(asOf, settle, cmpdFrequency);
    }

    private double YieldToMaturityPs(Dt asOf, Dt settle, Frequency cmpdFrequency)
    {
      var schedule = Loan.CallSchedule;
      var maturity = Loan.Maturity;
      if (schedule != null && schedule.Count > 0)
      {
        var callDates = schedule.Select(s => s.StartDate).ToArray();
        var idx = GetIndex(callDates, maturity);
        if (idx >= 0)
          return YieldToCall(asOf, settle, cmpdFrequency,
            schedule[idx].StartDate, schedule[idx].CallPrice);
      }
      CashflowAdapter cfa = new CashflowAdapter(GetPaymentSchedule(null, settle, Dt.Empty));
      CashflowModelFlags flags = (CashflowModelFlags.IncludeFees
                                  | CashflowModelFlags.IncludeProtection);
      double settlementCash = MarketFullPrice();
      DayCount daycount = (Loan.DayCount == DayCount.ActualActualBond
        ? DayCount.Actual365Fixed : Loan.DayCount);
      double yield = SolveIrr(cfa, asOf, settle,
        flags, daycount, cmpdFrequency, settlementCash);
      return yield;
    }

    private static int GetIndex(Dt[] dates, Dt date)
    {
      if (dates != null)
      {
        for (int i = 0; i < dates.Length; i++)
        {
          if (Dt.Cmp(dates[i], date) == 0) return i;
        }
      }
      return -1;
    }

    /// <summary>Calculate IRR of cashflows to a call date using 
    /// the current market price</summary>
    public double YieldToCall(Dt asOf, Dt settle, Frequency cmpdfrequency, 
      Dt callDate, double callStrike = Double.NaN)
    {
      return _usePaymentSchedule
        ? YieldToCallPs(asOf, settle, cmpdfrequency, callDate, callStrike)
        : YieldToCallCf(asOf, settle, cmpdfrequency, callDate, callStrike);
    }

    private double YieldToCallPs(Dt asOf, Dt settle, Frequency cmpdfrequency,
      Dt callDate, double callStrike = Double.NaN)
    {
      if (Loan.LoanType != LoanType.Term)
        throw new ToolkitException("Yield to call is currently " +
                                   "supported only for Term loans.");
      CashflowAdapter cf = new CashflowAdapter(GetPaymentSchedule(null, settle, callDate));
      CashflowModelFlags flags =
        (CashflowModelFlags.IncludeFees 
          | CashflowModelFlags.IncludeProtection);

      if (!Double.IsNaN(callStrike))
      {
        var last = cf.Count - 1;
        Debug.Assert(cf.GetPrincipalAt(last).AlmostEquals(cf.GetAmount(last)));
        cf.SetAmount(last, callStrike);
      }
      double settlementCash = MarketFullPrice();
      DayCount daycount = (Loan.DayCount == DayCount.ActualActualBond 
        ? DayCount.Actual365Fixed : Loan.DayCount);

      double yield = SolveIrr(cf, asOf, settle,
        flags, daycount, cmpdfrequency, settlementCash);
      return yield;
    }

    /// <summary>Calculate IRR of cashflows using the current market price</summary>
    public double YieldToMaturity()
    {
      return YieldToMaturity(AsOf, Settle, Loan.Frequency);
    }

    /// <summary>Calculate IRR of cashflows to a call date using 
    /// the current market price</summary>
    public double YieldToCall(Dt callDate, double callStrike = Double.NaN)
    {
      if (callDate > Loan.Maturity)
          return YieldToMaturity();
      return YieldToCall(AsOf, Settle, Loan.Frequency, callDate, callStrike);
    }

    /// <summary>Calculate yield to a call date which is a given 
    /// number of years from the pricer settle</summary>
    public double YieldToCall(int nYears)
    {
      Dt callDate = Dt.Add(Settle, nYears, TimeUnit.Years);
      return YieldToCall(callDate);
    }

    /// <summary>Calculate yield to a call date which is one year 
    /// from the pricer settle.
    /// If the call date is beyond the loan maturity, will calculate 
    /// yield to maturity.</summary>
    public double YieldToOneYearCall()
    {
      return YieldToCall(1);
    }

    /// <summary>Calculate yield to a call date which is two years 
    /// from the pricer settle.
    /// If the call date is beyond the loan maturity, will calculate 
    /// yield to maturity.</summary>
    public double YieldToTwoYearCall()
    {
      return YieldToCall(2);
    }

    /// <summary>Calculate yield to a call date which is three years 
    /// from the pricer settle.
    /// If the call date is beyond the loan maturity, will calculate 
    /// yield to maturity.</summary>
    public double YieldToThreeYearCall()
    {
      return YieldToCall(3);
    }

    /// <summary>
    /// Calculate yield to call in the call schedule and yield to 
    /// maturity, and return the smallest; if there is no call 
    /// schedule, calculate yield to call to T+30, 1 Year, 3 years 
    /// and yield to maturity with par strike, and return the smallest.
    /// </summary>
    public double YieldToWorst()
    {
      if (Loan.LoanType != LoanType.Term)
        throw new ToolkitException("Yield to worst is currently " +
                                   "supported only for Term loans.");
      var allYields = new List<double>();
      allYields.Add(YieldToMaturity());
      foreach (var schedule in GetAllCallInfo())
      {
        var callDate = schedule.StartDate;
        var callStrike = schedule.CallPrice;
        if (callDate < Settle || callDate >= Loan.Maturity)
          continue;
        var yld = YieldToCall(AsOf, Settle, Loan.Frequency, callDate, callStrike);
        allYields.Add(yld);
      }
      return allYields.Min();
    }

    private IList<CallPeriod> GetAllCallInfo(double defaultCallPrice = 1.0)
    {
      var settle = Settle;
      var maturity = Loan.Maturity;

      var defaultCallDates = new List<Dt>
      {
        Dt.Add(settle, 30, TimeUnit.Days),
        Dt.Add(settle, 1, TimeUnit.Years),
        Dt.Add(settle, 3, TimeUnit.Years)
      };

      var retVal = new List<CallPeriod>();

      // no call schedule, use the default call dates
      var schedule = Loan.CallSchedule;
      if (schedule == null || schedule.Count == 0)
      {
        foreach (var callDate in defaultCallDates)
        {
          if(callDate > maturity)
            continue;
          retVal.Add(new CallPeriod(callDate, callDate, defaultCallPrice, 0.0,
            OptionStyle.European, 0));
        }
        return retVal;
      }


      //call dates are all in the future.
      if (schedule.First().StartDate > settle)
      {
        foreach (var sched in schedule)
          retVal.Add(sched);

        AddLastTwoDefaultDates(retVal, defaultCallDates);
        return retVal;
      }

      //the first call date is in the past
      var add30 = defaultCallDates[0];
      var index = 0;
      for (int i = 0; i < schedule.Count; ++i)
      {
        index = i;
        if (schedule[i].StartDate > add30)
        {
          index = --i;
          break;
        }
      }
      retVal.Add(new CallPeriod(add30, add30, schedule[index].CallPrice,
        0.0, OptionStyle.European, 0));

      foreach (var callInfo in schedule)
      {
        if (callInfo.StartDate < add30)
          continue;
        retVal.Add(callInfo);
      }

      AddLastTwoDefaultDates(retVal, defaultCallDates);

      return retVal;
    }


    private void AddLastTwoDefaultDates(List<CallPeriod> callSchedule, 
      List<Dt> defaultCallDates)
    {
      Debug.Assert(callSchedule.Count > 0);
      var lastDt = callSchedule.Last().StartDate;
      var callStrike = callSchedule.Last().CallPrice;
      for (int i = 1; i < defaultCallDates.Count; ++i)
      {
        if (defaultCallDates[i] > lastDt)
          callSchedule.Add(new CallPeriod(defaultCallDates[i],
            defaultCallDates[i], callStrike, 0.0, OptionStyle.European, 0));
      }
    }

    /// <summary>
    /// Calculate a flat price for specific YieldToWorst in the loan 
    /// call schedule; if there is no call schedule, calculate a 
    /// price that corresponds to YieldToWorst of T+30, 1, 3 years 
    /// and maturity 
    /// </summary>
    /// <param name="yield">The target yield</param>
    /// <returns></returns>
    public double PriceFromYieldToWorst(double yield)
    {
      var maturity = Loan.Maturity;
      var allPrices = new List<double>();
      var callSchedule = GetAllCallInfo().ToList();
      allPrices.Add(PriceFromYield(yield, maturity));
      
      allPrices.AddRange(callSchedule
        .Where(c => !(c.StartDate < Settle || c.StartDate >= maturity))
        .Select(c => PriceFromYield(yield, c.StartDate, c.CallPrice)));

      return allPrices.Min() - AccruedInterest();
    }

    private double PriceFromYield(double yield, Dt callDate, 
      double callStrike = double.NaN)
    {
      return _usePaymentSchedule
        ? PriceFromYieldPs(yield, callDate, callStrike)
        : PriceFromYieldCf(yield, callDate, callStrike);
    }

    private double PriceFromYieldPs(double yield, Dt callDate,
      double callStrike = double.NaN)
    {
      CashflowAdapter cf = callDate == Loan.Maturity
        ? new CashflowAdapter(GetPaymentSchedule(null, Settle, Dt.Empty))
        : new CashflowAdapter(GetPaymentSchedule(null, Settle, callDate));
      DayCount dayCount = (Loan.DayCount == DayCount.ActualActualBond
       ? DayCount.Actual365Fixed
       : Loan.DayCount);

      if (!Double.IsNaN(callStrike))
      {
        var last = cf.Count - 1;
        Debug.Assert(cf.GetPrincipalAt(last).AlmostEquals(cf.GetAmount(last)));
        cf.SetAmount(last, callStrike);
      }
     
      var dc = DiscountCurveFromYield(yield, AsOf,
        Enumerable.Range(0, cf.Count).Select(i => cf.GetDt(i)),
        dayCount, Loan.Frequency);
      int n = cf.Count;
      double pv = 0.0;
      double accrued = 0.0;
      for (int i = 0; i < n; i++)
      {
        double accrual;
        var df = dc.Interpolate(dc.GetDt(i));
        accrued += ((List<InterestPayment>)cf.Data)[i].Accrued(dc.AsOf, out accrual);
        pv += df * (cf.PeAmounts[i] + accrual);
      }
      return accrued + pv;
    }

    #region Helper
    private static DiscountCurve DiscountCurveFromYield(
      double yield, Dt asOf, IEnumerable<Dt> dates,
      DayCount dayCount, Frequency freq)
    {
      var dc = new DiscountCurve(asOf)
      {
        Interp = new Linear(new Const(), new Const()),
        DayCount = dayCount,
        Frequency = freq,
      };
      foreach (var date in dates)
      {
        double df = RateCalc.PriceFromRate(yield, asOf, date, dayCount, freq);
        dc.Add(date, df);
      }
      return dc;
    }

 
    private static double SolveIrr(CashflowAdapter cfa, Dt asOf,
      Dt settle, CashflowModelFlags flags,
      DayCount dc, Frequency freq, double settlementCash)
    {
      var solver = new Generic();
      var fn = new LoanIrrFn(cfa, asOf, settle, flags, dc, freq);

      solver.setLowerBounds(RateCalc.RateLowerBound(freq));
      solver.setUpperBounds(200.0);
      solver.setInitialPoint(0.05);
      return solver.solve(fn, settlementCash, 0.01, 0.20);
    }

    #endregion Helper

    #region Solver
    public class LoanIrrFn : SolverFn
    {
      public LoanIrrFn(CashflowAdapter cfa, Dt asOf, Dt settle,
        CashflowModelFlags flags, DayCount dayCount, Frequency freq)
      {
        _cfa = cfa;
        _asOf = asOf;
        _settle = settle;
        _dayCount = dayCount;
        _freq = freq;

        bool isp = (flags & CashflowModelFlags.IncludeSettlePayments) != 0;
        bool crtpd = (flags & CashflowModelFlags.CreditRiskToPaymentDate) != 0;
        int index = 0;
        for (int i = 0; i < cfa.Count; i++)
        {
          if (Dt.Cmp(GetPeriodEndDate(cfa, i, crtpd), settle) > 0
              || (isp && Dt.Cmp(GetPeriodEndDate(cfa, i, crtpd), settle) == 0))
          {
            index = i;
            break;
          }
        }
        _firstIndex = index;

        if (_discountCurve == null)
          _discountCurve = new DiscountCurve(_asOf);
        _discountCurve.Clear();
        _discountCurve.AsOf = _asOf;
        _discountCurve.DayCount = _dayCount;
        _discountCurve.Frequency = _freq;

        for (int i = index; i < cfa.Count; i++)
        {
          _discountCurve.Add(cfa.GetDt(i), 0.0);
        }
      }

      public override double evaluate(double x)
      {
        double pv = 0.0;
        double accrued = 0.0;
        int n = _cfa.Count;
        for (int i = _firstIndex; i < n; i++)
        {
          double accrual;
          double df = RateCalc.PriceFromRate(x, _discountCurve.AsOf, _cfa.GetDt(i),
            _dayCount, _freq);
          accrued += ((List<InterestPayment>)_cfa.Data)[i].Accrued(_settle, out accrual);

          pv += df * (_cfa.PeAmounts[i] + accrual);
        }
        return accrued + pv;
      }

      private static Dt GetPeriodEndDate(CashflowAdapter cfa, int index,
        bool creditRiskToPaymentDate)
      {
        return creditRiskToPaymentDate ? cfa.GetDt(index) : cfa.GetEndDt(index);
      }

      private CashflowAdapter _cfa;
      private Dt _asOf, _settle;
      private DiscountCurve _discountCurve;
      private DayCount _dayCount;
      private Frequency _freq;
      private int _firstIndex;
    }

    #endregion Solver

    /// <summary>
    ///   Calculates the discount rate spread implied by the full price of the Loan.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is also commonly called the Zero Discount Margin.</para>
    /// 
    ///   <para>Calculates the constant spread over 
    /// the <see cref="DiscountCurve">Discount Curve</see> for the 
    ///   <see cref="ModelFullPrice">Model Full Price</see> to match 
    /// the <see cref="MarketFullPrice">Market Full Price</see>.</para>
    /// 
    ///   <para>Note, the ZSpread does not take into account the embedded credit 
    ///   spread option. </para>
    /// </remarks>
    /// 
    /// <seealso cref="DiscountCurve" />
    /// <seealso cref="ModelFullPrice" />
    /// <seealso cref="MarketFullPrice" />
    ///
    /// <returns>Spread over discount curve implied by price</returns>
    ///
    public double ZSpread()
    {
      return ZSpread(MarketFullPrice(), false);
    }

    /// <summary>
    ///   Calculates the discount rate spread implied by the given full price of the Loan.
    /// </summary>
    /// 
    /// <param name="price">The market price to match.</param>
    /// <param name="allowPrepay">Whether to consider the prepayment option, or not.</param>
    ///
    /// <remarks>
    ///   <para>This is also commonly called the <b>Zero Discount Margin</b>.</para>
    /// 
    ///   <para>Calculates the constant spread over the discount 
    /// curve for the <see cref="ModelFullPrice">Model Full Price</see> 
    ///   to match the given price.</para>
    /// 
    ///   <para>Note, you may specify whether to allow the prepayment 
    /// option to be considered during the calculation.</para>
    /// </remarks>
    /// 
    /// <seealso cref="DiscountCurve" />
    /// <seealso cref="ModelFullPrice" />
    ///
    /// <returns>Spread over discount curve implied by price</returns>
    ///
    public double ZSpread(double price, bool allowPrepay)
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return 0.0;

      // Create a copy of the discount curve
      DiscountCurve newCurve;
      if (this.Loan.DayCount == DayCount.ActualActualBond)
        newCurve = new DiscountCurve((DiscountCalibrator)DiscountCurve.Calibrator.Clone(),
        DiscountCurve.Interp, DayCount.Actual365Fixed, Loan.Frequency);
      else
        newCurve = new DiscountCurve((DiscountCalibrator)DiscountCurve.Calibrator.Clone(),
        DiscountCurve.Interp, Loan.DayCount, Loan.Frequency);
      newCurve.Interp = DiscountCurve.Interp;
      for (int i = 0; i < DiscountCurve.Count; ++i)
        newCurve.Add(DiscountCurve.GetDt(i), DiscountCurve.GetVal(i));

      // Setup empty survival curve
      SurvivalCurve sc = new SurvivalCurve(AsOf, 0);

      // adjust for the undrawn portion of the loan
      price = PriceAdjustedForUndrawnCommitment(price);

      // Calculate spread 
      double zspread = LoanModel.ImpliedDiscountSpread(AsOf, Settle, 
        Loan.GetScheduleParams(), Loan.DayCount, newCurve,
        Loan.IsFloating, this.ReferenceCurve, sc, RecoveryCurve, FullDrawOnDefault,
        allowPrepay, PrepaymentCurve, VolatilityCurve, RefinancingCost, 
        CurrentLevel, currentLevelIdx_, Loan.PerformanceLevels,
        Loan.PricingGrid, Loan.CommitmentFee, Usage, PerformanceLevelDistribution, 
        AdjustedAmortization,
        InterestPeriods, price, UseDrawnNotional, DrawnCommitment / TotalCommitment, 
        FloorValues, CurrentFloatingCoupon());

      // Done
      return zspread;
    }

    /// <summary>
    /// Option Adjusted Spread; discount rate spread implied by the full market price of 
    /// the Loan, considering optionality.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>This is a measure of credit risk that includes the embedded credit option. 
    /// The calculation is equivalent to the <see cref="ZSpread()">Z-Spread</see> of a Loan that is not 
    /// callable.</para>
    /// 
    /// <para>Calculated as the spread over the full term structure of interest rates that 
    /// discounts the non-callable Loan's cash flows to match the 
    /// <see cref="TheoreticalBondMarketFullPrice">Theoretical Market Price</see> 
    /// of a Loan without the prepayment option. </para>
    /// </remarks>
    /// 
    /// <seealso cref="TheoreticalBondMarketFullPrice" />
    /// <seealso cref="ZSpread()" />
    /// <seealso cref="ZSpread(double,bool)" />
    /// <seealso cref="DiscountCurve" />
    /// 
    /// <returns>Option Adjusted Spread</returns>
    /// 
    public double OAS()
    {
      return ZSpread(TheoreticalBondMarketFullPrice(), false);
    }

    /// <summary>
    /// CDS level implied by the market price and recovery rate.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The constant spread (premium) for the <see cref="CDS">CDS</see> 
    ///   which implies <see cref="BaseEntity.Toolkit.Curves.
    /// SurvivalCurve.SurvivalProb(BaseEntity.Toolkit.Base.Dt)">Survival Probabilities</see> 
    ///   that discount the <see cref="ModelFullPrice">Model Full Price</see> to match 
    ///   the <see cref="MarketFullPrice">Market Full Price</see>. The CDS is assumed 
    ///   to mature at the Loan's maturity date.</para>
    ///   
    ///   <para>Disregards any existing <see cref="SurvivalCurve">Survival Curve</see>.</para>
    /// </remarks>
    /// 
    /// <seealso cref="SurvivalCurve" />
    /// <seealso cref="BaseEntity.Toolkit.Curves.SurvivalCurve.
    /// SurvivalProb(BaseEntity.Toolkit.Base.Dt)" />
    /// <seealso cref="MarketFullPrice" />
    /// <seealso cref="ModelFullPrice" />
    /// <seealso cref="LoanPricer.ImpliedFlatCDSCurve()"/>
    /// <seealso cref="LoanPricer.ImpliedFlatCDSCurve(double)"/>
    ///
    /// <returns>Loan-Implied CDS Premium</returns>
    ///
    public double CDSLevel()
    {
      return CDSLevel(Loan.Maturity);
    }

    /// <summary>
    /// CDS level implied by the market price and recovery rate.
    /// </summary>
    /// 
    /// <param name="workoutDate">The maturity date to use for the CDS.</param>
    ///
    /// <remarks>
    ///   <para>The constant spread (premium) for the <see cref="CDS">CDS</see> 
    ///   which implies <see cref="BaseEntity.Toolkit.Curves.
    /// SurvivalCurve.SurvivalProb(BaseEntity.Toolkit.Base.Dt)">Survival Probabilities</see> 
    ///   that discount the <see cref="ModelFullPrice">Model Full Price</see> to match 
    ///   the <see cref="MarketFullPrice">Market Full Price</see>.</para>
    ///   
    ///   <para>Disregards any existing <see cref="SurvivalCurve">Survival Curve</see>.</para>
    /// </remarks>
    /// 
    /// <seealso cref="SurvivalCurve" />
    /// <seealso cref="BaseEntity.Toolkit.Curves.SurvivalCurve.
    /// SurvivalProb(BaseEntity.Toolkit.Base.Dt)" />
    /// <seealso cref="MarketFullPrice" />
    /// <seealso cref="ModelFullPrice" />
    /// <seealso cref="LoanPricer.ImpliedFlatCDSCurve()"/>
    /// <seealso cref="LoanPricer.ImpliedFlatCDSCurve(double)"/>
    ///
    /// <returns>Loan-Implied CDS Premium</returns>
    ///
    public double CDSLevel(Dt workoutDate)
    {
      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, this.Loan.Maturity)
        || (this.Settle == this.Loan.Maturity))
        return 0.0;

      // Get implied survival curve
      SurvivalCurve loanImpliedSC = ImpliedFlatCDSCurve();
      if (loanImpliedSC == null)
        return Double.NaN;

      // Calculate loan duration and extract spread at the duration generated date.
      double lvl = CurveUtil.ImpliedSpread(loanImpliedSC, workoutDate);

      if (ToolkitConfigurator.Settings.LoanModel.AllowNegativeOptionValue)
        return lvl + HSpread();
      else
        return lvl;
    }

    /// <summary>
    ///   Calculates the premium for a CDS over the existing 
    /// survival curve implied by the current
    ///   market price. 
    /// </summary>
    ///
    /// <remarks>
    ///  <para>Calculates the difference between the <see cref="CDSLevel()">CDS Level</see> 
    ///  and the implied spread from the <see cref="SurvivalCurve">Survival Curve</see>.
    ///  </para>
    ///  <para>Both CDS spreads are calculated using the Loan's 
    /// maturity date.</para>
    ///  <para>Also known as the Credit Basis.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.CDSLevel()"/>
    /// <seealso cref="LoanPricer.CDSLevel(Dt)"/>
    ///
    /// <returns>Credit Spread Basis</returns>
    ///
    public double CDSBasis()
    {
      return CDSBasis(Loan.Maturity);
    }

    /// <summary>
    ///   Calculates the premium for a CDS over the existing 
    /// survival curve implied by the current
    ///   market price. 
    /// </summary>
    ///
    /// <param name="workoutDate">The CDS maturity date.</param>
    ///
    /// <remarks>
    ///  <para>Calculates the difference between the 
    /// <see cref="CDSLevel()">CDS Level</see> 
    ///  and the implied spread from the 
    /// <see cref="SurvivalCurve">Survival Curve</see>.
    ///  </para>
    ///  <para>Both CDS spreads are calculated using a 
    /// maturity of the given workout date.</para>
    ///  <para>Also known as the Credit Basis.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.CDSLevel()"/>
    /// <seealso cref="LoanPricer.CDSLevel(Dt)"/>
    ///
    /// <returns>Credit Spread Basis</returns>
    ///
    public double CDSBasis(Dt workoutDate)
    {
      double basis = 0;

      // Validate
      if (!IsActive(this.Settle, this.Loan.Effective, 
        this.Loan.Maturity) || (this.Settle == this.Loan.Maturity))
        return 0.0;
      if (SurvivalCurve == null)
        return Double.NaN;

      // CDS implied from Loan price
      double impliedCDSLevel = CDSLevel(workoutDate);

      // CDS Spread on given SurvivalCurve at Loan maturity
      double curveLevel = CurveUtil.ImpliedSpread(SurvivalCurve, workoutDate);

      // Find basis: basis = curveLevel - impliedCDSLevel
      basis = curveLevel - impliedCDSLevel;

      // Done
      return basis;
    }

    /// <summary>
    /// The spread that calibrates the model to the market.
    /// </summary>
    /// 
    /// <remarks>This may be a spread over the discount 
    /// curve or over the survival curve. For Loans
    /// priced using Loan Implied CDS Curve the Market 
    /// Spread, by definition, is 0.</remarks>
    /// 
    /// <param name="type">Calibration target</param>
    /// 
    /// <returns>Double</returns>
    /// 
    private double MarketSpread(CalibrationType type)
    {
      double rspread = 0;

      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) 
        || (Settle == Loan.Maturity))
        return 0.0;
      
      var newCurve = (DiscountCurve)DiscountCurve.CloneWithCalibrator();
      
      // adjust for the undrawn portion of the loan
      var price = PriceAdjustedForUndrawnCommitment(MarketFullPrice());

      if (type == CalibrationType.DiscountCurve)
        rspread = LoanModel.ImpliedDiscountSpread(AsOf, Settle, Loan.GetScheduleParams(), 
          Loan.DayCount, newCurve,
          Loan.IsFloating, ReferenceCurve, GetSurvivalCurve(), RecoveryCurve,
          FullDrawOnDefault, true, PrepaymentCurve, VolatilityCurve, RefinancingCost,
          CurrentLevel, currentLevelIdx_, Loan.PerformanceLevels,
          Loan.PricingGrid, Loan.CommitmentFee, Usage,
          PerformanceLevelDistribution, AdjustedAmortization,
          InterestPeriods, price, UseDrawnNotional, DrawnCommitment/TotalCommitment, 
          FloorValues,CurrentFloatingCoupon());
      else if (type == CalibrationType.SurvivalCurve)
        rspread = LoanModel.ImpliedCreditSpread(AsOf, Settle, Loan.GetScheduleParams(), 
          Loan.DayCount, newCurve,
          Loan.IsFloating, ReferenceCurve, GetSurvivalCurve(), RecoveryCurve,
          FullDrawOnDefault, true, PrepaymentCurve, VolatilityCurve, RefinancingCost,
          CurrentLevel, currentLevelIdx_, Loan.PerformanceLevels,
          Loan.PricingGrid, Loan.CommitmentFee, Usage,
          PerformanceLevelDistribution, AdjustedAmortization,
          InterestPeriods, price, UseDrawnNotional, DrawnCommitment/TotalCommitment, 
          FloorValues,CurrentFloatingCoupon());

      // Done
      return rspread;
    }

    /// <summary>
    /// The spread that calibrates the model to the market by shifting the discount curve.
    /// </summary>
    /// 
    /// <returns>Double</returns>
    /// 
    public double RSpread()
    {
      return MarketSpread(CalibrationType.DiscountCurve);
    }

    /// <summary>
    /// The spread that calibrates the model to the market by shifting the survival curve.
    /// </summary>
    /// 
    /// <returns>Double</returns>
    /// 
    public double HSpread()
    {
      return MarketSpread(CalibrationType.SurvivalCurve);
    }
    #endregion

    #region Average Life
    /// <summary>
    /// The weighted average life of the Loan
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The <c>Weighted-Average Life (WAL)</c>, also called <c>average life</c>,
    ///   is the weighted average time to principal repayment, 
    /// assuming no default or prepayment risk.</para>
    ///   
    ///   <para>In a formula, <formula>
    ///     \mathrm{WAL} = \sum_{i=1}^{n} P_i*t_i
    ///   </formula>
    ///   where <formula inline="true">P_i</formula> is the expected repayment
    ///   per unit original notional during coupon period i,
    ///   <formula inline="true">t_i</formula> is the time from the settle to date i.</para>
    /// 
    ///   <para>No default or prepayment risk is considered in this measure.</para>
    /// </remarks>
    /// 
    /// <seealso cref="ExpectedWAL"/>
    /// 
    /// <returns>WAL</returns>
    /// 
    public double WAL()
    {
      return LoanModel.ExpectedWAL(Settle, Loan.GetScheduleParams(), Loan.Ccy,
        Loan.DayCount, EffectiveNotional, AdjustedAmortization, null);
    }

    /// <summary>
    /// The probabilistic weighted average life of the Loan
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The <c>Expected Weighted-Average Life (Expected WAL)</c>, 
    ///   is the weighted average time to principal repayment, assuming no default risk, but  
    ///   considering the probability of prepayment before maturity.</para>
    ///   
    ///   <para>In a formula, <formula>
    ///     \mathrm{E[WAL]} = \sum_{i=1}^{n} t_i * (p_i * P_i + q_{i-1,i} * N_i);
    ///   </formula>
    ///   Where, 
    ///   </para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">t_i</formula> is the time 
    /// from the settle to date i</description></item>
    ///     <item><description><formula inline="true">p_i</formula> is the 
    /// probability of no prepayment to date i</description></item>
    ///     <item><description><formula inline="true">P_i</formula> is the 
    /// expected repayment per unit original notional during coupon 
    /// period i</description></item>
    ///     <item><description><formula inline="true">q_{i-1,i}</formula> 
    /// is the probability of prepayment between dates i-1 and i</description></item>
    ///     <item><description><formula inline="true">N_i</formula> is 
    /// the outstanding notional at time i</description></item>
    ///   </list>
    /// 
    ///   <para>No default risk is considered in this measure.</para>
    /// </remarks>
    /// 
    /// <seealso cref="LoanPricer.WAL"/>
    /// <seealso cref="PrepaymentCurve" />
    /// 
    /// <returns>WAL</returns>
    /// 
    public double ExpectedWAL()
    {
      return LoanModel.ExpectedWAL(Settle, Loan.GetScheduleParams(), Loan.Ccy,
        Loan.DayCount, EffectiveNotional, AdjustedAmortization, PrepaymentCurve);
    }
    #endregion

    #endregion

    #region Utils
    /// <summary>
    /// Calculates the market price of the Loan.
    /// </summary>
    /// 
    /// <param name="includeAccrued">Whether to include accrued interest or not.</param>
    /// 
    /// <returns>Market Price</returns>
    /// 
    private double MarketPrice(bool includeAccrued)
    {
      if (marketFlatPrice_ == null)
      {
        if (MarketQuote.Type == QuotingConvention.FlatPrice)
          marketFlatPrice_ = MarketQuote.Value;
        else if (MarketQuote.Type == QuotingConvention.FullPrice)
          marketFlatPrice_ = MarketQuote.Value - this.AccruedInterest();
        else if (MarketQuote.Type == QuotingConvention.DiscountMargin)
        {
          marketFlatPrice_ = GetFullPriceFromDiscountMargin(MarketQuote.Value)
            - AccruedInterest();
        }
        else
          throw new NotSupportedException(String.Format("The given " +
                                                        "Quoting Convention '{0}' is not supported!", 
                                                        MarketQuote.Type));
      }

      // Done
      return (double)marketFlatPrice_ + (includeAccrued ? this.AccruedInterest() : 0.0);
    }


    private double GetFullPriceFromDiscountMargin(double discountMargin)
    {
      const bool allowPrepay = false;
      var asOf = AsOf;
      var settle = Settle;
      var liborStub = StubRate();
      var currentLibor = CurrentFloatingRate();
      var loan = Loan;
      GetNewCurves(liborStub, currentLibor, out var newDisCurve, out var newRefCurve);
      var survivalCurve = new SurvivalCurve(asOf, 0.0);


      var df0 = newDisCurve.DiscountFactor(asOf, settle);
      var origSpread = this.DiscountCurve.Spread;
      newDisCurve.Spread = origSpread + discountMargin;
 
      var pv = LoanModel.Pv(asOf, settle, loan.GetScheduleParams(), 
        loan.DayCount, loan.IsFloating, CurrentLevel,
        currentLevelIdx_, loan.PerformanceLevels, loan.PricingGrid,
        loan.CommitmentFee, Usage, PerformanceLevelDistribution, 
        newDisCurve, newRefCurve, survivalCurve, RecoveryCurve, 
        VolatilityCurve, FullDrawOnDefault,
        allowPrepay, PrepaymentCurve, RefinancingCost,
        AdjustedAmortization, InterestPeriods, CalibrationType.None, 0,
        UseDrawnNotional, DrawnCommitment/TotalCommitment, 
        CalcFloorValues(newRefCurve), CurrentFloatingCoupon());

      pv *= df0/newDisCurve.DiscountFactor(asOf, settle);

      return pv;
    }



    private void GetNewCurves(double liborStub, double currentLibor,
      out DiscountCurve newDisCurve, out DiscountCurve newRefCurve)
    {
      var asOf = AsOf;
      var loan = Loan;
      var origDisCurve = DiscountCurve;
      newDisCurve = new DiscountCurve((DiscountCalibrator)
        origDisCurve.Calibrator.Clone(), origDisCurve.Interp,
        loan.DayCount, loan.Frequency);

      newRefCurve = new DiscountCurve((DiscountCalibrator)
        origDisCurve.Calibrator.Clone(),
        origDisCurve.Interp, loan.DayCount, loan.Frequency);

      Dt nextCouponDate = NextCouponDate();
      double df1 = RateCalc.PriceFromRate(liborStub, asOf, nextCouponDate,
        loan.DayCount, loan.Frequency);
      newDisCurve.Add(nextCouponDate, df1);
      newRefCurve.Add(nextCouponDate, df1);

      // add flat Libor rate to the discount curve 
      // (at the frequency of the frn note (e.g 3 months Libor rate))
      string endTenor = "35 Year";
      Dt endDate = Dt.Add(asOf, endTenor);
      double df2 = df1 * RateCalc.PriceFromRate(currentLibor, nextCouponDate,
        endDate, loan.DayCount, loan.Frequency);
      newDisCurve.Add(endDate, df2);
      newRefCurve.Add(endDate, df2);
    }


    /// <summary>
    ///   Get floating stub rate
    /// </summary>
    ///
    /// <returns>Floating stub rate or 0 if floating rate reference curve not set</returns>
    ///
    private double StubRate()
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return 0.0;

      if (stubRate_ == null)
        stubRate_ = (ReferenceCurve != null) 
          ? ReferenceCurve.F(Settle, NextCouponDate(), Loan.DayCount, Loan.Frequency) 
          : 0.0;

      return (double)stubRate_;
    }

    /// <summary>
    ///   Get next coupon date
    /// </summary>
    ///
    /// <returns>Next coupon date or empty date if no next coupon</returns>
    ///
    private Dt NextCouponDate()
    {
      if (nextCouponDate_ == null)
        nextCouponDate_ = NextCouponDate(this.Settle);

      return (Dt)nextCouponDate_;
    }

    /// <summary>
    ///   Get next coupon date
    /// </summary>
    ///
    /// <param name="settle">Settlement date</param>
    ///
    /// <returns>Next coupon date on or after specified settlement 
    /// date or empty date if no next coupon</returns>
    ///
    private Dt NextCouponDate(Dt settle)
    {
      return Schedule.GetNextCouponDate(settle);
    }

    /// <summary>
    ///   Get current libor rate for coupon frequency
    /// </summary>
    ///
    /// <returns>Current libor rate matching coupon frequency or 0 
    /// if floating rate reference curve not set</returns>
    ///
    private double CurrentFloatingRate()
    {
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return 0.0;

      if (currentFloatingRate_ == null)
        currentFloatingRate_ = (ReferenceCurve != null) 
          ? ReferenceCurve.F(AsOf, Dt.Add(AsOf, Loan.Frequency, 
          Loan.CycleRule == CycleRule.EOM), Loan.DayCount, Loan.Frequency) 
          : 0.0;

      return (double)currentFloatingRate_;
    }

    /// <summary>
    ///   Get current floating coupon rate - Term loan only
    /// </summary>
    private double? CurrentFloatingCoupon()
    {
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) 
        || (Settle == Loan.Maturity) 
        || Loan.LoanType != LoanType.Term || !Loan.IsFloating)
        return null;
      if (currentFloatingCoupon_.HasValue) // already cached
        return currentFloatingCoupon_.Value;
      ScheduleParams scheduleParams = Loan.GetScheduleParams();
      InterestPeriod ip = InterestPeriodUtil.DefaultInterestPeriod(
        Settle, scheduleParams, DayCount.None, 0.0, 0.0);
      Dt periodStart = ip.StartDate;
      Dt nextCouponDate = ip.EndDate;
      double cpn = CurrentSpread;
      double rate = CurrentFloatingRateForTermLoan(periodStart, 
        nextCouponDate, cpn);  // In this case, cpn is the spread over LIBOR
      currentFloatingCoupon_ = rate;  // cache it to re-use
      return rate;
    }

    /// <summary>
    ///   Get current coupon rate for a floating rate term loan for the purposes of Accrued computation:
    ///   if a rate reset is extracted from Interest Periods, use it, otherwise, project from the projection curve.
    /// </summary>
    private double CurrentFloatingRateForTermLoan(Dt periodStart, Dt periodEnd, 
      double spread)
    {
      RateReset rr = Loan.GetCurrentReset(AsOf, Settle, Loan.Schedule, 
        this.InterestPeriods);
      if (rr != null)
        return rr.Rate;
      Dt resetDate = Dt.AddDays(periodStart, -Loan.RateResetDays, Loan.Calendar);
      if (AsOf > resetDate)
        throw new ToolkitException("No rate reset available from the " +
                                   "Interest Periods for the current pricing date. " +
                                   "Note that the start and end date " +
                                   "in the interest periods must match " +
        (this.Loan.PeriodAdjustment ? "(adjusted) coupon dates." : 
        "(adjusted or unadjusted) coupon dates."));
      // Otherwise, project from the rate curve:
      if (ReferenceCurve == null)
        throw new ToolkitException("No reference curve available " +
                                   "to project the floating coupon rate.");
      double currentFloatingRate = ReferenceCurve.F(periodStart, 
        periodEnd, Loan.DayCount, Loan.Frequency);
      if (Loan.LoanFloor.HasValue && currentFloatingRate < Loan.LoanFloor.Value)
        currentFloatingRate = Loan.LoanFloor.Value;
      return currentFloatingRate + spread;
    }

    /// <summary>
    ///   True if product is active
    /// </summary>
    ///
    /// <remarks>
    ///   <para>A product is active if the pricing AsOf date is on or after the
    ///   product effective date or before the product maturity date.</para>
    /// </remarks>
    ///
    /// <returns>true if product is active</returns>
    /// 
    static private bool IsActive(Dt asOf, Dt effective, Dt maturity)
    {
      return ((asOf < effective) || (asOf > maturity)) ? false : true;
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (Loan.LoanType == LoanType.Term && !TotalCommitment.
        ApproximatelyEqualsTo(DrawnCommitment))
        InvalidValue.AddError(errors, this, "DrawnCommitment", 
          "For a term loan, the Drawn Commitment must be equal " +
          "to the Total Commitment");
    }

    #endregion

    #region Properties
    /// <summary>
    /// Payment pricer.
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
    /// The total commitment that can be drawn for the Loan.
    /// </summary>
    public double TotalCommitment
    {
      get { return this.Notional; }
    }

    /// <summary>
    /// The commitment currently drawn.
    /// </summary>
    public double DrawnCommitment
    {
      get { return this.drawnCommitment_; }
    }

    /// <summary>
    /// The commitment currently drawn.
    /// </summary>
    public override double EffectiveNotional
    {
      get
      {
        // For Term Loan, drawnCommitment_ is the same as Notional 
        // (Notional is the total original amount borrowed, before any amortizations)
        if (Loan.LoanType == LoanType.Term) 
          return Notional * NotionalFactorAt(Settle);
        return useDrawnNotional_ ? this.drawnCommitment_ : this.Notional;
      }
    }

    /// <summary>
    /// The Loan being priced.
    /// </summary>
    public Loan Loan
    {
      get { return (Loan)this.Product; }
    }

    /// <summary>
    /// The interest rate curve for discounting.
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
    }

    /// <summary>
    /// The interest rate curve for projecting forward rates.
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
    }

    /// <summary>
    /// The survival curve.
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get { return survivalCurve_; }
      set { survivalCurve_ = value; }
    }

    private SurvivalCurve GetSurvivalCurve()
    {
      // Create a "dummy" survival curve when a null survival curve was passed.
      if (survivalCurve_ != null)
        return survivalCurve_;
      else
      {
        if (dummySurvivalCurve_ == null)
          dummySurvivalCurve_ = new SurvivalCurve(AsOf, 0);
        return dummySurvivalCurve_;
      }
    }

    /// <summary>
    /// The recovery curve.
    /// </summary>
    public RecoveryCurve RecoveryCurve
    {
      get { return recoveryCurve_; }
    }

    /// <summary>
    ///  Get the volatility curve
    /// </summary>
    public VolatilityCurve VolatilityCurve
    {
      get { return volatilityCurve_; }
    }
    /// <summary>
    ///   Return the recovery rate matching the maturity of the Loan.
    /// </summary>
    public double RecoveryRate
    {
      get { return (RecoveryCurve != null) ? RecoveryCurve.RecoveryRate(Product.Maturity) : 0.0; }
    }

    /// <summary>
    /// The prepayment curve.
    /// </summary>
    public SurvivalCurve PrepaymentCurve
    {
      get { return prepaymentCurve_; }
      set { prepaymentCurve_ = value; }
    }

    /// <summary>
    /// The cost (as a rate) of refinancing from the Loan into a new Loan. Here, 200 bps is expressed 
    /// as 0.02. 
    /// </summary>
    public double RefinancingCost
    {
      get { return refinancingCost_; }
    }

    /// <summary>
    /// The current performance level of the Loan.
    /// </summary>
    public string CurrentLevel
    {
      get { return currentLevel_; }
    }

    /// <summary>
    /// The current floating coupon spread based on the current performance level of the Loan.
    /// </summary>
    public double CurrentSpread
    {
      get { return Loan.PricingGrid[CurrentLevel];  }
    }

    /// <summary>
    /// The probability of the Loan being in each performance level at maturity.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>This is the conditional probability of performance; where, the probabilities 
    /// are conditional up on no default and no prepayment.</para>
    /// </remarks>
    /// 
    public double[] PerformanceLevelDistribution
    {
      get { return endDistribution_; }
    }

    /// <summary>
    /// The expected usage of the Loan when it is performing at each of the levels.
    /// </summary>
    public double[] Usage
    {
      get { return usage_; }
    }

    /// <summary>
    /// The market quote for the Loan; currently support flat price and discount margin.
    /// </summary>
    public MarketQuote MarketQuote
    {
      get { return marketQuote_; }
    }

    /// <summary>
    /// The Interest Periods for the Loan.
    /// </summary>
    public IList<InterestPeriod> InterestPeriods
    {
      get
      {
        if (interestPeriodsForTheta_ != null)
          return interestPeriodsForTheta_;
        return interestPeriods_;
      }
    }

    /// <summary>
    /// Whether the Loan is assumed to be fully drawn on a default.
    /// </summary>
    public bool FullDrawOnDefault
    {
      get { return fullDrawOnDefault_; }
      set { fullDrawOnDefault_ = value; }
    }


    public double RateVol => rateVol_;

    /// <summary>
    /// The coupon payment schedule for the Loan
    /// </summary>
    public Schedule Schedule
    {
      get
      {
        if (schedule_ == null)
        {
          CashflowFlag flags = CashflowFlag.None;
          if (Loan.PeriodAdjustment)
            flags |= CashflowFlag.AdjustLast;
          else
            flags |= CashflowFlag.AccrueOnCycle;
          schedule_ = new Schedule(Settle, Loan.Effective, Loan.FirstCoupon, 
            Loan.LastCoupon, Loan.Maturity,
            Loan.Frequency, Loan.BDConvention, Loan.Calendar, 
            Loan.CycleRule, flags);
        }
        return (Schedule)schedule_;
      }
    }

    /// <summary>
    /// The original amortization schedule of a loan product may have entries 
    /// that happened in the past (before pricer settle date). We re-scale 
    /// this schedule to pass it into the loan model, and include only the items
    /// in the future.
    /// </summary>
    private IList<Amortization> AdjustedAmortization
    {
      get
      {
        if (adjustedAmortization_ == null)
        {
          if (Loan.IsAmortizing)
          {
            if (Loan.LoanType != LoanType.Term) // Leave it the way it is for revolver loans for now ...
            {
              adjustedAmortization_ = CloneUtil.Clone<Amortization>(
                (List<Amortization>)Loan.AmortizationSchedule);
            }
            else
            {
              adjustedAmortization_ = new List<Amortization>();
              // all amortizations that already happened up to (and including) the pricer settle date.
              double notFactor = NotionalFactorAt(Settle); 
              foreach (Amortization item in Loan.AmortizationSchedule)
              {
                if (item.Date > Settle)
                  adjustedAmortization_.Add(new Amortization(item.Date, item.Amount / notFactor));
              }
            }
          }
          else
          {
            adjustedAmortization_ = new List<Amortization>();  // Just avoid passing null ...
          }
        }
        return adjustedAmortization_;
      }
    }

    /// <summary>
    /// The method of calibrating the model to the market.
    /// </summary>
    public CalibrationType CalibrationType
    {
      get { return calibrationType_; }
    }

    /// <summary>
    /// Use DrawnCommitment as notional.
    /// </summary>
    public bool UseDrawnNotional
    {
      get { return useDrawnNotional_; }
      set { useDrawnNotional_ = value; }
    }

    /// <summary>
    /// The method of determining current coupon rate for a floating loan. 
    /// Typically, rely on default option, which is Interest Periods.
    /// </summary>
    public LoanNextCouponTreatment NextCouponTreatment
    {
      get { return nextCouponTreatment_;  }
      set { nextCouponTreatment_ = value;  }
    }

    /// <summary>
    /// The option values of floorlets.
    /// </summary>
    public double[] FloorValues
    {
      get { return CalcFloorValues(referenceCurve_); }
    }

    /// <summary>
    /// Calculate floor values.
    /// </summary>
    public double[] CalcFloorValues(DiscountCurve referenceCurve)
    {
      var floorValues = new double[Schedule.Count];
      
      for (int i = 0; i < Schedule.Count;++i )
      {
        double T = Dt.Fraction(AsOf, Schedule.GetCycleStart(i), Loan.DayCount);
        if(T > 0 && floorLevel_ != null)
        {
          double rate = referenceCurve.F(Schedule.GetPeriodStart(i), 
            Schedule.GetPeriodEnd(i), Loan.DayCount, Loan.Frequency);
          
          // floorValues_[i] = Black.P(OptionType.Put, T, rate, (double)floorLevel_, rateVol_);
          // we use normal model in case there is negative forward rate
          floorValues[i] = BlackNormal.P(OptionType.Put, T, 0, 
            rate, (double)floorLevel_, rateVol_);
        }
        else
        {
          floorValues[i] = 0;
        }
      }
     
      return floorValues;
    }

    /// <summary>
    /// The following function will calculate the interest periods to be used for theta calculation; 
    /// will return null, if the "original" interest periods should be re-used.
    /// </summary>
    public IList<InterestPeriod> CalculateInterestPeriodsForTheta(Dt toAsOf, Dt toSettle)
    {
      if (!Loan.IsFloating || Loan.LoanType != LoanType.Term)
        return null; // Implement this logic for the term loans only.
      List<InterestPeriod> currentInterestPeriods = 
        InterestPeriodUtil.InterestPeriodsForDate(Settle, interestPeriods_);
      // We only need to generate interest periods for Theta if the new settle date 
      // moves beyond the interest period selected for the current settle date.
      if (currentInterestPeriods != null 
        && currentInterestPeriods.Count == 1 
        && toSettle < currentInterestPeriods[0].EndDate)
        return null; 
      LoanNextCouponTreatment projectionOption = 
        (NextCouponTreatment == LoanNextCouponTreatment.InterestPeriods
        ? LoanNextCouponTreatment.CurrentFixing
        : NextCouponTreatment);
      IList<InterestPeriod> interestPeriods = 
        LoanPricerFactory.CreateInterestPeriods(projectionOption, toAsOf, toSettle, Loan,
        CurrentSpread, DrawnCommitment / TotalCommitment, referenceCurve_, null);
      return interestPeriods;
    }

    public void SetInterestPeriodsForTheta(Dt toAsOf, Dt toSettle)
    {
      interestPeriodsForTheta_ = CalculateInterestPeriodsForTheta(toAsOf, toSettle);
    }

    /// <summary>
    /// Adjustment to use for model pricing. Used to match model futures price to market futures price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the contract to match quote (if available).
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// </remarks>
    public double ModelBasis { get; set; }

    #endregion

    #region Data
    // Given data
    private readonly double drawnCommitment_;
    private bool useDrawnNotional_;
    private readonly DiscountCurve discountCurve_;
    private readonly DiscountCurve referenceCurve_;
    private readonly RecoveryCurve recoveryCurve_;
    private readonly double refinancingCost_;
    private readonly string currentLevel_;
    private readonly double[] endDistribution_;
    private readonly double[] usage_;
    private readonly MarketQuote marketQuote_;
    private readonly IList<InterestPeriod> interestPeriods_;
    private IList<InterestPeriod> interestPeriodsForTheta_;
    private readonly CalibrationType calibrationType_;
    private readonly double rateVol_;
    private readonly double? floorLevel_;
    private readonly int currentLevelIdx_;
    private LoanNextCouponTreatment nextCouponTreatment_ = LoanNextCouponTreatment.InterestPeriods;
    private static bool _usePaymentSchedule = true;

    // Settings
    private bool fullDrawOnDefault_;
    private double marketSpread_;
    private SurvivalCurve survivalCurve_;
    private SurvivalCurve dummySurvivalCurve_;
    private SurvivalCurve prepaymentCurve_;
    private VolatilityCurve volatilityCurve_;

    // Cached data
    [Mutable]
    private object stubRate_;
    [Mutable]
    private object nextCouponDate_;
    [Mutable]
    private object currentFloatingRate_;
    [Mutable]
    private double? currentFloatingCoupon_;
    [Mutable]
    private object accruedInterest_;
    [Mutable]
    private object marketFlatPrice_;
    [Mutable]
    private object schedule_;
    [Mutable]
    private IList<Amortization> adjustedAmortization_;
    #endregion

    #region Explicit Implementation of Recovery Sensitivity
    /// <summary>
    /// Gets the Recovery Curve and/or Survival Curves (containing a 
    /// Recovery Curve) that the Loan is sensitive to.
    /// </summary>
    /// 
    /// <returns>List of Curves</returns>
    /// 
    IList<Curve> IRecoverySensitivityCurvesGetter.GetCurves()
    {
      List<Curve> curves = new List<Curve>();

      // Always add the survival curve; this will get the right 
      // recovery curve if the survival curve is implied
      if (SurvivalCurve != null)
        curves.Add(SurvivalCurve);

      // Add RecoveryCurve explicitly if it is different from the 
      // Survival one (ie SurvivalCurve is a CDS Curve)
      if ((SurvivalCurve == null || 
        RecoveryCurve != SurvivalCurve.SurvivalCalibrator.RecoveryCurve) 
        && RecoveryCurve != null)
        curves.Add(RecoveryCurve);

      // Done
      return curves;
    }
    #endregion
  }
}
