/*
 * ABSPricer.cs
 *
 */
using System;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{

  ///
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CDS">ABS</see> using the
  ///   <see cref="BasketPricer">Basket Pricer Model</see>.</para>
  /// </summary>
  ///
  /// <seealso cref="BaseEntity.Toolkit.Products.CDS">ABS Product</seealso>
  ///
  [Serializable]
  public class ABSPricer : PricerBase, IPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ABSPricer));

    #region Constructors

    /// <summary>
    ///   <para>Create a pricer for a ABS based on the  
    ///   credit-contingent cashflow model.</para>
    /// </summary>
    /// 
    /// <param name="product">ABS Product</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="intensity">Intensity of jump probability</param>
    /// <param name="recovery">Overall recovery rate</param>
    /// <param name="events">Number of credit events</param>
    /// <param name="cashflowGenerator">Cash flow generator</param>
    ///
    public ABSPricer(
      CDS product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      double intensity,
      double recovery,
      int events,
      ABSCashFlowGenerator cashflowGenerator
      )
      : base(product, asOf, settle)
    {
      if (events <= 0)
        throw new ArgumentOutOfRangeException("events", "Number of buckets must be positive");
      if (recovery < 0 || recovery > 1)
        throw new ArgumentOutOfRangeException("recovery", "Recovery must be between 0 and 1");

      this.discountCurve_ = discountCurve;
      this.distribution_ = new Curve2D();
      this.scalings_ = null;
      this.tenorDates_ = null;
      this.singleJump_ = true;
      this.simpleModel_ = new SimpleModel(
        settle, product.Maturity, product.Freq, cashflowGenerator,
        intensity, new RecoveryCurve(asOf,recovery), events);
      simpleModel_.GenerateStates();
      this.intensities_ = simpleModel_.Intensities;
      this.stateAmortizes_ = simpleModel_.StateAmortizes;
      this.stateLosses_ = simpleModel_.StateLosses;
      int nStates = stateLosses_.GetLength(1);
      initializeDistribution(nStates, simpleModel_.Dates);
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">ABS product</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="dates">Array of pricing grid dates</param>
    /// <param name="stateLosses">2-dimensional array of losses by grid dates and states</param>
    /// <param name="stateAmortizes">2-dimensional array of amortizes/repayments by grid dates and states</param>
    /// <param name="intensities">Array of intensities by states</param>
    /// <param name="scalings">Array probability scaling factors by states</param>
    /// <exclude />
    public ABSPricer(
        CDS product,
        Dt asOf,
        Dt settle,
        DiscountCurve discountCurve,
        Dt[] dates,
        double[,] stateLosses,
        double[,] stateAmortizes,
        double[] intensities,
        double[] scalings
        )
      : base(product, asOf, settle)
    {
      if (dates.Length != stateLosses.GetLength(0) || dates.Length != stateAmortizes.GetLength(0)
        || intensities.Length != stateLosses.GetLength(1) || intensities.Length != stateAmortizes.GetLength(1))
        throw new ToolkitException("dates, stateLosses and stateAmortizes not match");

      this.discountCurve_ = discountCurve;
      this.stateLosses_ = stateLosses;
      this.stateAmortizes_ = stateAmortizes;
      this.distribution_ = new Curve2D();
      this.intensities_ = intensities;
      this.scalings_ = scalings;
      this.tenorDates_ = null;
      int nStates = stateLosses.GetLength(1);
      initializeDistribution(nStates, dates);
    }

    /// <summary>
    ///   Constructor, allow term-structure of intensity
    /// </summary>
    ///
    /// <param name="product">ABS product</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="dates">Array of pricing grid dates</param>
    /// <param name="stateLosses">2-dimensional array of losses by dates and states</param>
    /// <param name="stateAmortizes">2-dimensional array of amortizes/repayments by dates and states</param>
    /// <param name="intensityTenors">Array of tenor dates for intensities</param>
    /// <param name="intensities">2-dimensional array of intensities by tenor dates and states</param>
    /// <param name="scalings">2-dimensional array probability scaling factors by tenor dates and states</param>
    /// 
    /// <exclude />
    public ABSPricer(
        CDS product,
        Dt asOf,
        Dt settle,
        DiscountCurve discountCurve,
        Dt[] dates,
        double[,] stateLosses,
        double[,] stateAmortizes,
        Dt[] intensityTenors,
        double[,] intensities,
        double[,] scalings)
      : base(product, asOf, settle)
    {
      if (dates.Length != stateLosses.GetLength(0) || dates.Length != stateAmortizes.GetLength(0))
        throw new ToolkitException("dates, stateLosses and stateAmortizes not match");
      if (intensityTenors.Length != intensities.GetLength(0) || 
          (scalings != null && intensityTenors.Length != scalings.GetLength(0)) )
        throw new ToolkitException("dates, intensities and scalings not match");

      this.discountCurve_ = discountCurve;
      this.stateLosses_ = stateLosses;
      this.stateAmortizes_ = stateAmortizes;
      this.distribution_ = new Curve2D();
      this.tenorDates_ = intensityTenors;
      this.intensities_ = intensities;
      this.scalings_ = scalings;
      int nStates = stateLosses.GetLength(1);
      initializeDistribution(nStates, dates);

    }
    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      ABSPricer obj = (ABSPricer)base.Clone();
      obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();
      obj.distribution_ = distribution_.clone();
      return obj;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Calculate pv of ABS protection leg.
    /// </summary>
    ///
    /// <param name="t">Settle date</param>
    /// 
    /// <returns>the PV of the protection leg in this setting</returns>
    ///
    public double
    ProtectionPv(Dt t)
    {
      CDS abs = ABS;
      Dt maturity = abs.Maturity;
      CashflowStream cashflow = GenerateCashflowForProtection(t);
      Curve lossCurve = GenerateLossCurve();
      double pv = CashflowStreamModel.ProtectionPv(cashflow, t, t,
          discountCurve_, lossCurve, null,
          this.DefaultTiming, this.AccruedFractionOnDefault,
          false/*includeSettlePayments_*/,
          0/*StepSize*/, TimeUnit.None/*StepUnit*/);
      pv *= this.Notional * discountCurve_.DiscountFactor(t);
      return pv;
      // To be done
    }

    /// <summary>
    ///   Calculate pv of ABS fee leg.
    /// </summary>
    ///
    /// <param name="t">Settle date</param>
    /// <param name="premium">Premium</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double
    FeePv(Dt t, double premium)
    {
      double pv = 0.0;
      CDS abs = ABS;

      CashflowStream cashflow = GenerateCashflowForFee(t, premium);
      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
      Curve lossCurve = GenerateLossCurve();
      Curve amorCurve = GenerateAmorCurve();
      pv = CashflowStreamModel.FeePv(cashflow, t, t,
              discountCurve_, lossCurve, amorCurve,
              this.DefaultTiming, this.AccruedFractionOnDefault,
              false/*includeSettlePayments_*/,
              stepSize, stepUnit);
      pv *= this.Notional * discountCurve_.DiscountFactor(t);
      return pv;
    }

    /// <summary>
    ///   Fee pv (clean) of ABS fee leg on forward date.
    /// </summary>
    ///
    /// <remarks>This function does not include up-front fee</remarks>
    ///
    /// <param name="forwardDate">Forward settle date</param>
    /// <param name="premium">Premium</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    public double FlatFeePv(Dt forwardDate, double premium)
    {
      double pv = FeePv(forwardDate, premium);
      return pv;
    }

    /// <summary>
    ///   Up-front fee on forward date
    /// </summary>
    ///
    /// <param name="forwardSettle">forward settle date</param>
    /// 
    /// <returns>Up-front fee</returns>
    ///
    public double UpFrontFeePv(Dt forwardSettle)
    {
      CDS abs = ABS;
      if (abs.Fee != 0.0 && Dt.Cmp(abs.FeeSettle, forwardSettle) > 0)
      {
        return abs.Fee * Notional;
      }
      else
      return 0.0;
    }

    /// <summary>
    ///   Calculate pv of ABS protection leg.
    /// </summary>
    ///
    /// <returns>the PV of the protection leg in this setting</returns>
    ///
    public double
    ProtectionPv()
    {
      return ProtectionPv(Settle);
    }

    /// <summary>
    ///   Calculate pv of ABS fee leg.
    /// </summary>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double
    FeePv()
    {
      return FeePv(Settle, this.ABS.Premium) + UpFrontFeePv(Settle);
    }

    /// <summary>
    ///   Calculate pv (clean) of ABS
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee.</remarks>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FlatFeePv()
    {
      return FlatFeePv(Settle, ABS.Premium) + UpFrontFeePv(Settle);
    }

    /// <summary>
    ///   Calculate pv of the ABS
    /// </summary>
    ///
    /// <returns>the PV of the ABS</returns>
    ///
    public override double
    ProductPv()
    {
      return ProtectionPv() + FeePv();
    }

    /// <summary>
    ///   Calculate flat price of the ABS.
    /// </summary>
    ///
    /// <returns>the PV of the ABS</returns>
    ///
    public double
    FlatPrice()
    {
      return ProtectionPv() + FlatFeePv();
    }

    /// <summary>
    ///   Calculate full price of the ABS.
    /// </summary>
    ///
    /// <returns>the PV of the ABS</returns>
    ///
    public double
    FullPrice()
    {
      return ProtectionPv() + FeePv() + Accrued();
    }

    /// <summary>
    ///   Calculate accrued for ABS
    /// </summary>
    ///
    /// <returns>Accrued to settlement for the ABS</returns>
    ///
    public override double Accrued()
    {
      return this.Accrued(Settle);
    }

    /// <summary>
    ///   Calculate accrued for ABS
    /// </summary>
    ///
    /// <param name="settle">Settlement date</param>
    /// 
    /// <returns>Accrued to settlement for the ABS</returns>
    ///
    public double
    Accrued(Dt settle)
    {
      BaseEntity.Toolkit.Products.CDS product = this.ABS;
      double premium = this.ABS.Premium;

      // Generate out payment dates from settlement
      Schedule sched = new Schedule(settle, product.Effective, product.FirstPrem, product.Maturity,
        product.Freq, product.BDConvention, product.Calendar);

      // Calculate accrued to settlement.
      Dt start = sched.GetPeriodStart(0);
      Dt end = sched.GetPeriodEnd(0);
      // Note schedule currently includes last date in schedule period. This may get changed in
      // the future so to handle this we test if we are on a coupon date.
      if (Dt.Cmp(settle, start) == 0 || Dt.Cmp(settle, end) == 0)
        return 0.0;
      else
        return (Dt.Fraction(start, settle, product.DayCount) * premium) * Notional;
    }

    /// <summary>
    ///   Calculate the break even premium of the ABS
    /// </summary>
    ///
    /// <returns>the break even premium</returns>
    ///
    public double
    BreakEvenPremium()
    {
      double protection = ProtectionPv(Settle);
      double upfrontfee = UpFrontFeePv(Settle);

      double fee01 = FlatFeePv(Settle, 1); // based on clean price

      return (-protection - upfrontfee) / fee01;
    }

    /// <summary>
    ///   Calculate the risky duration of the ABS
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The risky duration is defined as the ration of the fee pv divided by the fee</para>
    /// </remarks>
    ///
    /// <returns>risky duration</returns>
    ///
    public double
    RiskyDuration()
    {
      double pv = FeePv(Settle, 1.0) / Notional;
      return pv;
    }

    /// <summary>
    ///   Calculate probability distributions over time
    /// </summary>
    /// <returns>Curve2D representing probability distributions over time</returns>
    public Curve2D CalculateDistribution()
    {
      calculateDistribution(Settle, Maturity);
      return this.Distribution;
    }


    /// <summary>
    ///   Calculate the carry of the ABS
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The carry is daily income from premium and is simply
    ///   the premium divided by 360 times the notional.</para>
    /// </remarks>
    ///
    /// <returns>Carry of the CSO</returns>
    ///
    public double Carry()
    {
      return ABS.Premium / 360 * Notional;
    }

    /// <summary>
    ///   Calculate the MTM Carry of the ABS
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The MTM carry is the daily income of the MTM level
    ///   of the credit default swap. It is the Break Even Premium
    ///   divided by 360 times the notional.</para>
    /// </remarks>
    /// 
    /// <returns>MTM Carry of Credit Default Swap</returns>
    /// 
    public double MTMCarry()
    {
      return BreakEvenPremium() / 360 * Notional;
    }

    /// <summary>
    ///   Calculate the Premium 01 of the ABS
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Premium01 is the change in PV (MTM) for an ABS
    ///   tranche if the premium is increased by one basis point.</para>
    ///
    ///   <para>The Premium 01 is calculated by calculating the PV (MTM) of the
    ///   ABS then bumping up the premium by one basis point
    ///   and re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    ///
    /// <returns>Premium 01 of the Synthetic CDO tranche</returns>
    ///
    public double Premium01()
    {
      CDS abs = ABS;
      return (FeePv(Settle, abs.Premium + 0.0001) - FeePv(Settle, abs.Premium));
    }

    /// <summary>
    ///   Calculate expected loss
    /// </summary>
    ///
    /// <remarks>
    ///   The expected loss is expressed as dollar amount.
    /// </remarks>
    ///
    /// <returns>Expected loss at the given date</returns>
    ///
    public double ExpectedLoss(Dt date)
    {
      Curve curve = GenerateLossCurve();
      double loss = curve.Interpolate(date);
      return loss * this.Notional;
    }
    
    /// <summary>
    ///   Calculate expected survival
    /// </summary>
    ///
    /// <remarks>
    ///   The expected survival is expressed as percentage of tranche notional (0.01 = 1%)
    /// </remarks>
    ///
    /// <returns>Expected survival at the maturity date</returns>
    ///
    public double ExpectedSurvival(Dt date)
    {
      Curve curve = GenerateLossCurve();
      double loss = curve.Interpolate(date);
      return 1.0 - loss;
    }

    /// <summary>
    ///   Calculate the implied intensity
    /// </summary>
    /// <param name="pricer">ABS pricer</param>
    /// <param name="date">Refernce date</param>
    /// <param name="survival">Expected survival on the reference date</param>
    /// <returns>Transity intensity</returns>
    public static double ImpliedIntensity(
      ABSPricer pricer, Dt date, double survival)
    {
      // Set up root finder
      Brent rf = new Brent();

      rf.setToleranceF(1.0E-6);
      rf.setToleranceX(1.0E-5);
      rf.setLowerBounds(1E-10);

      // Solve
      SolverFn fn = new SurvivalEvaluator(pricer, date);
      double result = Double.NaN;
      result = rf.solve(fn, survival, 0.01);

      return result;
    }

    class SurvivalEvaluator : SolverFn
    {
      public SurvivalEvaluator(ABSPricer pricer, Dt date)
      {
        pricer_ = pricer;
        date_ = date;
      }

      public override double evaluate(double x)
      {
        pricer_.setIntensity(x);
        pricer_.Reset();
        return pricer_.ExpectedSurvival(date_);
      }

      private ABSPricer pricer_;
      private Dt date_;
    }

    /// <summary>
    ///   Reset the pricer to recalculate everything
    /// </summary>
    public override void Reset()
    {
      distributionComputed_ = false;
      if (distribution_ != null)
        distribution_.SetAsOf(Settle);
      base.Reset();
    }
    #endregion // Methods

    #region Properties

    /// <summary>
    ///   ABS to price
    /// </summary>
    public CDS ABS
    {
      get { return (CDS)Product; }
    }

    /// <summary>
    ///   Maturity date
    /// </summary>
    public Dt Maturity
    {
      get { return ABS.Maturity; }
      set { ABS.Maturity = value; }
    }

    /// <summary>
    ///   Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
    }

    /// <summary>
    ///   Computed distribution for basket
    /// </summary>
    public Curve2D Distribution
    {
      get { return distribution_; }
      set { distribution_ = value; }
    }

    /// <summary>
    ///   Indicator for how accrued is handled under default.  A value of zero indicates no accrued will be used and a 
    ///   value of one means the entire period's (in the time grid sense) accrued will be counted.  This is further controlled
    ///   by the CDO property AccruedOnDefault, which, if FALSE, means this value is ignored.  Default value is 0.5 and any value
    ///   must be between zero and one.
    /// </summary>
    public double AccruedFractionOnDefault
    {
      get { return accruedFractionOnDefault_; }
      set
      {
        accruedFractionOnDefault_ = value;
      }
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
    ///    Use simple model or not
    /// </summary>
    /// <exclude />
    public bool UseSingleJumpModel
    {
      get { return singleJump_; }
      set { singleJump_ = value; }
    }

    /// <summary>
    ///   intensity array
    /// </summary>
    /// <exclude />
    public Array IntensityArray
    {
      get { return intensities_; }
    }

    /// <summary>
    ///   Recovery curve
    /// </summary>
    public RecoveryCurve RecoveryCurve
    {
      get {
        if (simpleModel_ == null)
          return null;
        return simpleModel_.RecoveryCurve; 
      }
      set
      {
        if (simpleModel_ != null)
          simpleModel_.RecoveryCurve = value;
      }
    }

    /// <summary>
    ///   Cumulative loss curve
    /// </summary>
    public Curve CumulativeLossCurve
    {
      get { return GenerateLossCurve(); }
    }

    /// <summary>
    ///   Cumulative amortization curve
    /// </summary>
    public Curve CumulativeAmortizeCurve
    {
      get { return GenerateAmorCurve(); }
    }
    #endregion // Properties

    #region Data
    private DiscountCurve discountCurve_;
    private Dt[] tenorDates_;
    private Array intensities_;
    private Array scalings_;
    private double[,] stateLosses_;
    private double[,] stateAmortizes_;
    private Curve2D distribution_;
    private bool distributionComputed_;
    private SimpleModel simpleModel_;
    private bool singleJump_;

    private double accruedFractionOnDefault_ = 0.5;
    private double defaultTiming_ = 0.5;
    #endregion // Data

    #region Helpers

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

      // Accrued Fraction of default has to be within a [0.0 - 1.0] range 
      if (accruedFractionOnDefault_ < 0.0 || accruedFractionOnDefault_ > 1.0)
        InvalidValue.AddError(errors, this, "AccruedFractionOnDefault", String.Format("Invalid fraction. Must be between 0 and 1, Not {0}", accruedFractionOnDefault_));

      // Default Timing has to be within a [0.0 - 1.0] range 
      if (defaultTiming_ < 0.0 || defaultTiming_ > 1.0)
        InvalidValue.AddError(errors, this, "DefaultTiming", String.Format("Invalid timing of defaults. Must be between 0 and 1, Not {0}", accruedFractionOnDefault_));
      
      return;
    }

    private void initializeDistribution(int nStates, Dt[] dates)
    {
      int nDates = dates.Length;
      int nLevels = nStates;
      if (Dt.Cmp(Settle, dates[0]) < 0)
      {
        // we have start with the settle date
        nDates++;
        distribution_.Initialize(nDates, nLevels, 1);
        distribution_.SetAsOf(Settle);
        distribution_.SetDate(0, Settle);
        for (int i = 1; i < nDates; ++i)
          distribution_.SetDate(i, dates[i-1]);
      }
      else
      {
        distribution_.Initialize(nDates, nLevels, 1);
        distribution_.SetAsOf(Settle);
        for (int i = 0; i < nDates; ++i)
          distribution_.SetDate(i, dates[i]);
      }
      for (int i = 0; i < nLevels; ++i)
      {
        distribution_.SetLevel(i, (double)i);
        distribution_.SetValue(i, 0.0);
      }
      distribution_.SetValue(0, 1.0); // start with no default at settlement date
      return;
    }

    private void calculateDistribution(Dt start, Dt stop)
    {
      if (simpleModel_ != null)
      {
        simpleModel_.GenerateStates();
        intensities_ = simpleModel_.Intensities;
        stateAmortizes_ = simpleModel_.StateAmortizes;
        stateLosses_ = simpleModel_.StateLosses;
      }
      if (tenorDates_ == null)
      {
        Toolkit.Models.MarkovianBasketModel.ComputeDistributions(
            start, stop,
            (double[])intensities_,
            scalings_ == null ? new double[0] : (double[])scalings_,
            this.UseSingleJumpModel,
            distribution_);
        distributionComputed_ = true;
        return;
      }

      int nStates = distribution_.NumLevels();
      double[,] intensities = (double[,])intensities_;
      double[,] scalings = (double[,])scalings_;
      double[] lambdas = new double[nStates];
      double[] scales = (scalings_ == null ? null : new double[nStates]);
      Dt t0 = start;
      for (int tenor = 0; tenor < tenorDates_.Length; ++tenor)
      {
        Dt t1 = tenorDates_[tenor];
        if (Dt.Cmp(t0, t1) < 0)
        {
          for (int i = 0; i < nStates; ++i)
          {
            lambdas[i] = intensities[tenor, i];
            if (scales != null)
              scales[i] = scalings[tenor, i];
          }

          if (Dt.Cmp(stop, t1) < 0)
            t1 = stop;

          Toolkit.Models.MarkovianBasketModel.ComputeDistributions(
            t0, t1, lambdas, scales == null ? new double[0] : scales,
            this.UseSingleJumpModel, distribution_);

          if (Dt.Cmp(stop, t1) <= 0)
            break;
        }
        t0 = t1;
      }
      distributionComputed_ = true;
    }

    /// <summary>
    ///   Calculate the expected incrementa value
    /// </summary>
    private double expectedIncrement(int dateIdx, double[,] deltas)
    {
      int nStates = distribution_.NumLevels();
      int diff = distribution_.NumDates() - deltas.GetLength(0);
      double result = 0;
      if (dateIdx >= diff)
      {
        for (int stateIdx = 0; stateIdx < nStates; ++stateIdx)
        {
          double stateProbability = distribution_.GetValue(dateIdx, stateIdx);
          double stateLoss = deltas[dateIdx - diff, stateIdx];
          result += stateProbability * stateLoss;
        }
      }
      return result;
    }

    /// <summary>
    ///   Generate expected loss or amortization curve
    /// </summary>
    private Curve GenerateCurve(bool amor)
    {
      Curve curve = null;
      if (distributionComputed_ == false)
        calculateDistribution(Settle, Maturity);
      if (distribution_ == null || distribution_.NumLevels() == 0)
        return curve;

      double[,] delta = amor ? stateAmortizes_ : stateLosses_;

      curve = new Curve(Settle);
      double loss = 0;
      int startIdx = 0;
      if (Dt.Cmp(Settle, distribution_.GetDate(0)) != 0)
      {
        Dt current = Settle;
        loss += expectedIncrement(0, delta);
        curve.Add(current, loss);
        startIdx++;
      }
      int N = distribution_.NumDates();
      for (int i = startIdx; i < N; ++i)
      {
        Dt current = distribution_.GetDate(i);
        loss += expectedIncrement(i, delta);
        curve.Add(current, loss);
      }
      return curve;
    }

    private Curve GenerateLossCurve()
    {
      return GenerateCurve(false);
    }

    private Curve GenerateAmorCurve()
    {
      return GenerateCurve(true);
    }

    // Generate simple cashflow stream
    private CashflowStream GenerateCashflowForFee(Dt t, double coupon)
    {
      CDS abs = ABS;

      // Generate cashflow
      CashflowStream cf = new CashflowStream(t);
      CashflowStreamFactory.FillFixed(cf, t, abs.Effective, abs.FirstPrem, abs.Maturity, abs.Ccy,
          0.0, abs.FeeSettle, // no fee at this moment.
          coupon, abs.DayCount, abs.Freq, abs.BDConvention, abs.Calendar,
          null/*couponSched*/, 1.0/*origPrincipal*/, null/*amortSched*/,
          false/*exchangePrincipal*/,
          false/*accruedPaid*/,
          false/*includeDfltDate*/,
          false/*accrueOnCycle, TBD: should change to false later on*/,
          false/*adjustNext*/,
          false/*adjustLast*/,
          true/*eomRule*/,
          true/*includeMaturityAccrual*/);

      return cf;
    }

    // Generate simple cashflow stream
    private CashflowStream GenerateCashflowForProtection(Dt settle)
    {
      CDS abs = ABS;

      // Generate cashflow
      CashflowStream cf = new CashflowStream(settle);
      cf.Currency = abs.Ccy;
      cf.AccruedPaidOnDefault = false; // not used?
      cf.AccruedIncludingDefaultDate = false; // not used?

      // using the pricing grid to generate cash flow grid
      Dt current = settle;
      // add (principalRepayment = 0.0, interest = 0.0, notional = 1.0)
      cf.Add(current, 0.0, 0.0, 1.0);

      int N = distribution_.NumDates();
      int i;
      for (i = 0; i < N; ++i)
      {
        current = distribution_.GetDate(i);
        if (Dt.Cmp(current, settle) > 0)
          break;
      }
      for (; i < N; ++i)
      {
        current = distribution_.GetDate(i);
        // add (principalRepayment = 0.0, interest = 0.0, notional = 1.0)
        cf.Add(current, 0.0, 0.0, 1.0);
      }
      if (Dt.Cmp(abs.Maturity, current) > 0)
        // add (principalRepayment = 0.0, interest = 0.0, notional = 1.0)
        cf.Add(abs.Maturity, 0.0, 0.0, 1.0);

      return cf;
    }

    private void setIntensity(double intensity)
    {
      if (simpleModel_ != null)
      {
        simpleModel_.Intensity = intensity;
      }
      else
      {
        Array intensities = intensities_;
        int count = intensities.Length;
        for (int i = 0; i < count; ++i)
          intensities.SetValue(intensity, i);
      }
      return;
    }
    #endregion // Helpers

    #region SimpleModel
    class SimpleModel
    {
      #region Constructors
      public SimpleModel(
        Dt start, Dt stop, Frequency freq, ABSCashFlowGenerator cfg,
        double intensity, RecoveryCurve recoveryCurve, int events)
      {
        intensity_ = intensity;
        recoveryCurve_ = recoveryCurve;
        numEvents_ = events;
        createPricingGrids(start, stop, freq);
        balances_ = getBalances(cfg != null ? cfg.Cashflows : null);
      }
      #endregion // Constructors

      #region Methods
      public void GenerateStates()
      {
        double[] balances = balances_;
        Dt[] dates = dates_;
        int nDates = dates.Length;
        int nEvents = numEvents_;
        int nStates = nEvents + 1;
        double recovery = recoveryCurve_.Interpolate(dates[nDates - 1]);
        double amorPerEvent = recovery / nEvents;
        double lossPerEvent = (1 - recovery) / nEvents;
        double probJump = 1 - Math.Exp(-intensity_);
        double[] intensities = new double[nStates];
        double[,] stateLosses = new double[nDates, nStates];
        double[,] stateAmortizes = new double[nDates, nStates];
        for (int i = 0; i < nEvents; ++i)
        {
          intensities[i] = intensity_;
          double survival = 1 - ((double)i) / numEvents_;
          double prevBal = 1;
          for (int t = 0; t < nDates; ++t)
          {
            stateLosses[t, i] = probJump * lossPerEvent * prevBal;
            double repay = prevBal - balances[t];
            stateAmortizes[t, i] = probJump * (amorPerEvent * prevBal - repay / nEvents) + survival * repay;
            prevBal = balances[t];
          }
        }
        intensities_ = intensities;
        stateLosses_ = stateLosses;
        stateAmortizes_ = stateAmortizes;
      }

      private void createPricingGrids(Dt start, Dt stop, Frequency freq)
      {
        if (freq == Frequency.None)
          freq = Frequency.Quarterly;
        System.Collections.ArrayList list = new System.Collections.ArrayList();
        //list.Add(start);
        while (Dt.Cmp(start, stop) < 0)
        {
          start = Dt.Add(start, freq, true);
          if (Dt.Cmp(start, stop) > 0)
            start = stop;
          list.Add(start);
        }
        dates_ = (Dt[])list.ToArray(typeof(Dt));
      }

      private double[] getBalances(CashflowStream cf)
      {
        Dt[] dates = dates_;
        int nDates = dates.Length;
        double[] balances = new double[nDates];
        if (cf == null)
        {
          for (int i = 0; i < nDates; ++i)
            balances[i] = 1;
          return balances;
        }
        int cfLast = cf.Count - 1;
        int cfIdx = 0;
        for (int t = 0; t < nDates; ++t)
        {
          if (cfIdx >= cfLast)
            return balances;
          Dt date = dates[t];
          for (; Dt.Cmp(date, cf.GetDate(cfIdx)) > 0; ++cfIdx)
            if (cfIdx == cfLast)
              break;
          balances[t] = cf.GetNotional(cfIdx);
        }
        return balances;
      }
      #endregion // Methods

      #region Properties
      public Dt[] Dates
      {
        get { return dates_; }
      }

      public double[] Intensities
      {
        get { return intensities_; }
      }

      public double[,] StateLosses
      {
        get { return stateLosses_; }
      }

      public double[,] StateAmortizes
      {
        get { return stateAmortizes_; }
      }

      public double Intensity
      {
        get { return intensity_; }
        set { intensity_ = value; }
      }

      public RecoveryCurve RecoveryCurve
      {
        get { return recoveryCurve_; }
        set {
          if (value == null)
            throw new ArgumentException("Recovery curve cannot be null");
          recoveryCurve_ = value;
        }
      }
      #endregion // Properties

      #region Data
      // output data
      Dt[] dates_;
      double[] intensities_;
      double[,] stateLosses_;
      double[,] stateAmortizes_;

      // input data
      private double intensity_;
      private RecoveryCurve recoveryCurve_;
      private int numEvents_;
      private double[] balances_;
      #endregion // Data
    }
    #endregion // SimpleModel
  } // class ABSPricer

}
