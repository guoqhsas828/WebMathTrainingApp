using System;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util.Configuration;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Pricers
{
  #region Factory
  /// <summary>
  /// Factory class
  /// </summary>
  public static class CreditLinkedNotePricerFactory
  {
    /// <summary>
    /// Create a CreditLinkedNotePricer
    /// </summary>
    /// <param name="asOf">AsOf date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="notional">Notional</param>
    /// <param name="product">Product</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurves">Underlier survival curves</param>
    /// <param name="principals">Underlier principals</param>
    /// <param name="correlation">Underlier correlation</param>
    /// <param name="creditFactorVolatility">Volatility of underlying credit factor</param>
    /// <param name="collateralSurvivalCurve">Survival curve of the collateral</param>
    /// <param name="collateralRecovery">Recovery of the collateral</param>
    /// <param name="collateralCorrelation">Correlation (in Gaussian sense) of collateral default time to </param>
    /// <param name="stepSize">Step size</param>
    /// <param name="stepUnit">Step unit</param>
    /// <param name="quadraturePoints">Quadrature points for contingent leg optionality calculation (Default is 25)</param>
    /// <returns>CreditLinkedNotePricer</returns>
    public static CreditLinkedNotePricer Create(
      Dt asOf,
      Dt settle,
      double notional,
      CreditLinkedNote product,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CorrelationObject correlation,
      VolatilityCurve creditFactorVolatility,
      SurvivalCurve collateralSurvivalCurve,
      double collateralRecovery,
      double collateralCorrelation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints)
    {
      if(product == null)
        throw new ArgumentException("Non null product expected");
      if (survivalCurves == null || survivalCurves.Length == 0)
        throw new ArgumentException("Non null survivalCurves expected");
      var underlier = product.CreditDerivative;
      int qPoints = (quadraturePoints == 0) ? 25 : quadraturePoints;
      principals = (principals == null || principals.Length != survivalCurves.Length)
                     ? Array.ConvertAll(survivalCurves, sc => 1.0/survivalCurves.Length)
                     : principals; 
      if (underlier is SyntheticCDO)
      {
        return new CdoLinkedNotePricer(asOf, settle, notional, product, discountCurve, survivalCurves, principals, correlation, 
                                       creditFactorVolatility, collateralSurvivalCurve, collateralRecovery, Math.Sqrt(collateralCorrelation), 
                                       stepSize, stepUnit) {QuadraturePoints = qPoints};
      }
      if (underlier is FTD)
      {
        var corr = correlation as Correlation;
        if (corr == null)
          throw new ArgumentException("Correlation of type Correlation expected");
        return new NtdLinkedNotePricer(asOf, settle, notional, product, discountCurve, survivalCurves, principals, 
                                       corr, creditFactorVolatility, collateralSurvivalCurve, collateralRecovery, 
                                       Math.Sqrt(collateralCorrelation), stepSize, stepUnit) 
                                       { QuadraturePoints = qPoints };
      }
      if (underlier is BasketCDS)
      {
        return new BasketCdsLinkedNotePricer(asOf, settle, notional, product, discountCurve, survivalCurves, principals, 
                                             creditFactorVolatility, collateralSurvivalCurve, collateralRecovery, 
                                             Math.Sqrt(collateralCorrelation), stepSize, stepUnit)
                                             { QuadraturePoints = qPoints };

      }
      if (underlier is CDS)
      {
        return new CdsLinkedNotePricer(asOf, settle, notional, product, discountCurve, survivalCurves[0], 
                                       creditFactorVolatility, collateralSurvivalCurve, collateralRecovery, 
                                       Math.Sqrt(collateralCorrelation), stepSize, stepUnit) { QuadraturePoints = qPoints };
      }
      throw new ArgumentException(String.Format("Product {0} not supported", product.GetType()));
    }
  }

  #endregion

  #region CreditLinkedNotePricer

  /// <summary>
  /// Pricer for Credit Linked Notes
  /// </summary>
  [Serializable]
  public abstract partial class CreditLinkedNotePricer : PricerBase, IPricer
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="product">Product</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="notional">Notional</param>
    /// <param name="creditFactorVolatility">Volatility of the credit spread/spreads of credits underlying the CreditDerivative (asssumed the same for all names) </param>
    /// <param name="collateralSurvivalCurve">Survival curve of the collateral piece</param>
    /// <param name="collateralRecovery">Collateral recovery curve</param>
    /// <param name="collateralCorrelation">Correlation in Gaussian copula sense (square of factor loading of collateral default time)</param>
    /// <param name="stepSize">Step size for time grid in loss calculation</param>
    /// <param name="stepUnit">Step unit for time grid in loss calculation</param>
    protected CreditLinkedNotePricer(
      Dt asOf,
      Dt settle,
      double notional,
      CreditLinkedNote product,
      DiscountCurve discountCurve,
      VolatilityCurve creditFactorVolatility,
      SurvivalCurve collateralSurvivalCurve,
      double collateralRecovery,
      double collateralCorrelation,
      int stepSize,
      TimeUnit stepUnit
      )
      : base(product, asOf, settle)
    {
      QuadraturePoints = 25;
      StepSize = stepSize;
      StepUnit = stepUnit;
      DiscountCurve = discountCurve;
      CollateralSurvivalCurve = collateralSurvivalCurve;
      CollateralCorrelation = collateralCorrelation;
      CollateralRecovery = collateralRecovery;
      Notional = notional;
      CreditFactorVolatility = creditFactorVolatility;
      AccruedFractionAtDefault = 0.5;
      DefaultTiming = 0.5;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Model parameters for discount/projection rates
    /// </summary>
    public RateModelParameters ModelParameters;

    /// <summary>
    /// Accrued fraction on default (by default 0.5)
    /// </summary>
    public double AccruedFractionAtDefault { get; set; }

    /// <summary>
    ///  Default timing (by default 0.5)
    ///  </summary>
    public double DefaultTiming { get; set; }

    /// <summary>
    /// Quadrature points 
    /// </summary>
    public int QuadraturePoints { get; set; }

    /// <summary>
    /// All inclusive historical rate resets
    /// </summary>
    public RateResets RateResets { get; set; }

    /// <summary>
    /// True to include cashflows at settle
    /// </summary>
    public bool IncludeSettlePayments { get; set; }

    /// <summary>
    /// time unit for loss calculation
    /// </summary>
    public TimeUnit StepUnit { get; private set; }

    /// <summary>
    /// Time step for loss calculation
    /// </summary>
    public int StepSize { get; private set; }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Survival curve
    /// </summary>
    public SurvivalCurve[] SurvivalCurves { get; set; }

    /// <summary>
    /// Survival curve of the credit referencing collateral piece 
    /// </summary>
    public SurvivalCurve CollateralSurvivalCurve { get; private set; }

    /// <summary>
    /// Collateral recovery curve (if null use that in CollateralSurvivalCurve)
    /// </summary>
    public double CollateralRecovery
    {
      get
      {
        if (collateralRecovery_ >= 0.0)
          return collateralRecovery_;
        if (CollateralSurvivalCurve != null && CollateralSurvivalCurve.SurvivalCalibrator != null &&
            CollateralSurvivalCurve.SurvivalCalibrator.RecoveryCurve != null)
          return
            CollateralSurvivalCurve.SurvivalCalibrator.RecoveryCurve.RecoveryRate(CreditLinkedNote.Collateral.Maturity);
        return 0.0;
      }
      private set
      {
        collateralRecovery_ = value;
      }
    }

    /// <summary>
    /// Survival curve of the credits referencing credit derivative piece 
    /// </summary>
    public abstract SurvivalCurve[] UnderlierSurvivalCurves { get; }

    /// <summary>
    /// Correlation of underlier
    /// </summary>
    public abstract CorrelationObject Correlation { get; }

    /// <summary>
    /// Product
    /// </summary>
    public CreditLinkedNote CreditLinkedNote
    {
      get { return (CreditLinkedNote)Product; }
    }

    /// <summary>
    /// Correlation (in Gaussian copula sense) of the credit underlying the collateral piece
    /// </summary>
    public double CollateralCorrelation { get; set; }

    /// <summary>
    /// Volatility of the spread of credits underlying the reference CreditDerivative 
    /// </summary>
    public VolatilityCurve CreditFactorVolatility { get; set; }

    /// <summary>
    /// Discount amount accrued to Settle back to AsOf date
    /// </summary>
    public bool DiscountingAccrued { get; set; }

    /// <summary>
    /// Default date (earliest of collateral default date and exhaustion of the underlying derivative)
    /// </summary>
    public Dt DefaultDt { set; get; }

    /// <summary>
    /// Default settlement date
    /// </summary>
    public Dt DefaultSettleDt { set; get; }
    
    /// <summary>
    /// Surviving notional 
    /// </summary>
    protected abstract Func<Dt,double> SurvivalFunction { get; }

    /// <summary>
    /// Get dynamic pricer
    /// </summary>
    protected abstract IDynamicPricer DynamicPricer { get; }
    #endregion

    #region Data

    private double collateralRecovery_;
    #endregion

    #region Methods
    /// <summary>
    /// Delta to a 1% (relative) parallel shift of underlier volatility
    /// </summary>
    /// <returns>Delta</returns>
    public double VolatilityDelta()
    {
      var origCurve = (VolatilityCurve)CreditFactorVolatility.Clone();
      for (int i = 0; i < CreditFactorVolatility.Count; ++i)
        CreditFactorVolatility.SetVal(i, origCurve.GetVal(i) * 1.01);
      double ppv = Pv();
      for (int i = 0; i < CreditFactorVolatility.Count; ++i)
        CreditFactorVolatility.SetVal(i, origCurve.GetVal(i) * 0.99);
      double mpv = Pv();
      double retVal = 0.5 * (ppv - mpv);
      for (int i = 0; i < CreditFactorVolatility.Count; ++i)
        CreditFactorVolatility.SetVal(i, origCurve.GetVal(i));
      return retVal;
    }

    /// <summary>
    /// Gamma to a 1% (relative) parallel shift of underlier volatility
    /// </summary>
    /// <returns>Gamma</returns>
    public double VolatilityGamma()
    {
      double pv = Pv();
      var origCurve = (VolatilityCurve) CreditFactorVolatility.Clone();
      for (int i = 0; i < CreditFactorVolatility.Count; ++i)
        CreditFactorVolatility.SetVal(i, origCurve.GetVal(i)*1.01);
      double ppv = Pv();
      for (int i = 0; i < CreditFactorVolatility.Count; ++i)
        CreditFactorVolatility.SetVal(i, origCurve.GetVal(i)*0.99);
      double mpv = Pv();
      double retVal = ppv - 2*pv + mpv;
      for (int i = 0; i < CreditFactorVolatility.Count; ++i)
        CreditFactorVolatility.SetVal(i, origCurve.GetVal(i));
      return retVal;
    }

    /// <summary>
    /// Delta to 1% change of collateral correlation
    /// </summary>
    /// <returns>Delta</returns>
    public double CollateralCorrelationDelta()
    {
      double origCorr = CollateralCorrelation;
      CollateralCorrelation = Math.Max(-1.0, Math.Min(origCorr*1.01, 1.0));
      double ppv = Pv();
      CollateralCorrelation = Math.Max(-1.0, Math.Min(origCorr*0.99, 1.0));
      double mpv = Pv();
      double retVal = 0.5*(ppv - mpv);
      CollateralCorrelation = origCorr;
      return retVal;
    }

    /// <summary>
    /// Gamma to 1% change of collateral correlation
    /// </summary>
    /// <returns>Gamma</returns>
    public double CollateralCorrelationGamma()
    {
      double pv = Pv();
      double origCorr = CollateralCorrelation;
      CollateralCorrelation = Math.Max(-1.0, Math.Min(origCorr*1.01, 1.0));
      double ppv = Pv();
      CollateralCorrelation = Math.Max(-1.0, Math.Min(origCorr*0.99, 1.0));
      double mpv = Pv();
      double retVal = ppv - 2*pv + mpv;
      CollateralCorrelation = origCorr;
      return retVal;
    }

    /// <summary>
    /// Delta to 1bps parallel shift of callateral spread
    /// </summary>
    /// <returns>Delta</returns>
    public double CollateralSpreadDelta()
    {
      if (CollateralSurvivalCurve == null)
        return 0.0;
      var origCurve = CloneUtil.Clone(CollateralSurvivalCurve);
      var flags = BumpFlags.RefitCurve;
      CollateralSurvivalCurve.BumpQuotes(null, QuotingConvention.CreditConventionalSpread, new[] {1.0}, flags);
      double ppv = Pv();
      CurveUtil.CurveRestoreQuotes(new CalibratedCurve[] {CollateralSurvivalCurve}, new CalibratedCurve[] {origCurve});
      flags |= BumpFlags.BumpDown;
      CollateralSurvivalCurve.BumpQuotes(null, QuotingConvention.CreditConventionalSpread, new[] {1.0}, flags);
      double mpv = Pv();
      CurveUtil.CurveRestoreQuotes(new CalibratedCurve[] {CollateralSurvivalCurve}, new CalibratedCurve[] {origCurve});
      new Curve[] {CollateralSurvivalCurve}.CurveSet(new Curve[] {origCurve});
      return 0.5 * (ppv - mpv);
    }

    /// <summary>
    /// Pv including initial payment if IncludeSettlePayments is set to true
    /// </summary>
    /// <returns>Pv</returns>
    public override double Pv()
    {
      if (DefaultDt.IsValid() && DefaultSettleDt < Settle)
        return 0.0;
      double pv = ProductPv();
      if (IncludeSettlePayments)
        pv -= PaymentPv();
      return pv;
    }

    /// <summary>
    /// Accrued
    /// </summary>
    /// <returns></returns>
    public override double Accrued()
    {
      if (Settle <= Product.Effective || Settle >= Product.Maturity)
        return 0.0;
      var paymentSchedule = GetPaymentSchedule(null, Settle, Settle);
      var fips = paymentSchedule.GetPaymentsByType<InterestPayment>().ToArray();
      if (fips.Length == 0)
        return 0.0;
      var ip = fips[0];
      double accrual;
      return (ip.Notional / CreditLinkedNote.Notional) * Notional * ip.Accrued(Settle, out accrual);
    }

    /// <summary>
    /// Payment schedule  
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">from date</param>
    /// <returns>Payment schedule</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      return GetPaymentSchedule(paymentSchedule, from, Dt.Empty);
    }

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">From date</param>
    /// <param name="to">To date</param>
    /// <returns></returns>
    private PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from, Dt to)
    {
      var cln = CreditLinkedNote;
      if (CreditLinkedNote.ReferenceIndex == null)
        paymentSchedule = PaymentScheduleUtils.FixedRatePaymentSchedule(from, to, cln.Ccy, cln.Schedule,
                                                                        cln.CashflowFlag,
                                                                        cln.Coupon, cln.CouponSchedule, cln.Notional,
                                                                        cln.AmortizationSchedule, cln.Amortizes,
                                                                        cln.DayCount, Frequency.None, null,
                                                                        IncludeSettlePayments, DefaultDt,
                                                                        DefaultSettleDt, null, null);
      else
      {
        var pp = GetProjectionParams();
        var rateProjector = GetRateProjector(pp);
        var forwardAdjust = GetForwardAdjustment(pp);
        paymentSchedule = PaymentScheduleUtils.FloatingRatePaymentSchedule(from, to, cln.Ccy, rateProjector,
                                                                           forwardAdjust,
                                                                           RateResets, cln.Schedule, cln.CashflowFlag,
                                                                           cln.Coupon,
                                                                           cln.CouponSchedule,
                                                                           cln.Notional, cln.AmortizationSchedule, cln.Amortizes,
                                                                           cln.DayCount,
                                                                           null, pp, cln.Cap, cln.Floor,
                                                                           IncludeSettlePayments,
                                                                           DefaultDt, DefaultSettleDt, null, null);
      }
      if (from <= cln.Effective)
        paymentSchedule.AddPayment(new PrincipalExchange(cln.Effective,
                                                         -cln.AmortizationSchedule.PrincipalAt(cln.Notional,
                                                                                               cln.Effective), cln.Ccy));
      Dt maturity = cln.Schedule.GetPaymentDate(cln.Schedule.Count - 1);
      if (!to.IsValid() || maturity <= to)
        paymentSchedule.AddPayment(new PrincipalExchange(maturity,
                                                         cln.AmortizationSchedule.PrincipalAt(cln.Notional, maturity),
                                                         cln.Ccy));
      return paymentSchedule;
    }

    private ProjectionParams GetProjectionParams()
    {
      var retVal = new ProjectionParams
                     {
                       ProjectionType =
                         (CreditLinkedNote.ProjectionType == ProjectionType.None)
                           ? ProjectionType.SimpleProjection
                           : CreditLinkedNote.ProjectionType
                     };
      return retVal;
    }

    private IRateProjector GetRateProjector(ProjectionParams projectionParams)
    {
      return CouponCalculator.Get(AsOf, CreditLinkedNote.ReferenceIndex, DiscountCurve, DiscountCurve, projectionParams);
    }

    private IForwardAdjustment GetForwardAdjustment(ProjectionParams projectionParams)
    {
      return ForwardAdjustment.Get(AsOf, DiscountCurve, ModelParameters, projectionParams);
    }

    private static void EvolveCollateralCurve(int z, double[,] conditionalSurvival, SurvivalCurve survivalCurve)
    {
      if (survivalCurve == null)
        return;
      for (int t = 0; t < conditionalSurvival.GetLength(0); ++t)
        survivalCurve.SetVal(t, conditionalSurvival[t, z]);
    }

 
    private static double CollateralDefaultIndicator(SurvivalCurve collateralSurvival, Dt start, Dt end)
    {
      if (collateralSurvival == null)
        return 0.0;
      return collateralSurvival.Interpolate(start) - collateralSurvival.Interpolate(end);
    }

    private Dt[] CreateDateArray(Dt from)
    {
      var retVal = new List<Dt>();
      if (StepSize == 0 || StepUnit == TimeUnit.None)
      {
        StepSize = 3;
        StepUnit = TimeUnit.Months;
      }
      Dt current = from;
      for (;;)
      {
        if (current >= CreditLinkedNote.Maturity)
          break;
        retVal.Add(current);
        current = Dt.Add(current, StepSize, StepUnit);
      }
      retVal.Add(CreditLinkedNote.Maturity);
      return retVal.ToArray();
    }

    /// <summary>
    /// Fee leg pv
    /// </summary>
    /// <returns></returns>
    public double FeeLegPv()
    {
      var ps = GetPaymentSchedule(null, AsOf);
      var surv = SurvivalFunction;
      var pv = ps.Pv(AsOf, Settle, DiscountCurve, surv, IncludeSettlePayments, DiscountingAccrued);
      return Notional*pv;
    }

    /// <summary>
    /// Protection pv
    /// </summary>
    /// <returns></returns>
    public double ProtectionPv()
    {
      return Notional*ContingentLegPv(CreditLinkedNote.PayRecoveryAtMaturity);
    }

    /// <summary>
    /// Risky duration
    /// </summary>
    /// <returns></returns>
    public double RiskyDuration()
    {
      var cln = CreditLinkedNote;
      var paymentSchedule = PaymentScheduleUtils.FixedRatePaymentSchedule(AsOf, Dt.Empty, cln.Ccy, cln.Schedule,
                                                                          cln.CashflowFlag,
                                                                          1.0, cln.CouponSchedule, cln.Notional,
                                                                          cln.AmortizationSchedule, cln.Amortizes,
                                                                          cln.DayCount, Frequency.None, null,
                                                                          IncludeSettlePayments, DefaultDt,
                                                                          DefaultSettleDt, null, null);
      var surv = SurvivalFunction;
      var pv = paymentSchedule.Pv(AsOf, Settle, DiscountCurve, surv, IncludeSettlePayments, DiscountingAccrued);
      return pv;
    }

    private double ContingentLegPv(bool recoveryAtMaturity)
    {
      var riskyCollateral = (CollateralSurvivalCurve != null);
      var dates = CreateDateArray(Settle);
      if (dates.Length == 0)
        return 0.0;
      SurvivalCurve collateralWorkspace = null;
      double[,] collateralConditionalSurvival = null;
      var dynamicPricer = DynamicPricer;
      if(riskyCollateral)
      {
        var quadPoints = new double[QuadraturePoints];
        var quadWeights = new double[QuadraturePoints];
        collateralConditionalSurvival = new double[dates.Length,QuadraturePoints];
        collateralWorkspace = new SurvivalCurve(AsOf);
        foreach (var dt in dates)
          collateralWorkspace.Add(dt, 0.0);
        LossProcess.InitializeSurvival(AsOf, dates, new[] {CollateralCorrelation}, CollateralSurvivalCurve, quadPoints,
                                       quadWeights, collateralConditionalSurvival);
      }
      var collateralCfa = CollateralCashflowAdapter();
      var collateralRecovery = CollateralRecovery;
      var df = Array.ConvertAll(dates, d => DiscountCurve.Interpolate(AsOf, d));
      var grid = new double[dates.Length - 1, QuadraturePoints];
      for(int z = 0; z < QuadraturePoints; ++z)
      {
        dynamicPricer.Evolve(z);
        EvolveCollateralCurve(z, collateralConditionalSurvival, collateralWorkspace);
        for (int t = 0; t < dates.Length -  1; ++t)
        {
          var v = 0.0;
          if (riskyCollateral)
          {
            var pv = dynamicPricer.Pv(dates[t]);
            var dy = CollateralDefaultIndicator(collateralWorkspace, dates[t], dates[t + 1]);
            //if collateral defaults unwind position and investor receives counterparty collateral + Value
            v += df[t]*Math.Max(collateralRecovery + pv, 0.0)*dy;
          }
          var contingentPayment = dynamicPricer.ContingentPayment(dates[t], dates[t + 1]);
          var pvc = PsCollateralPv(dates[t], dates[t], collateralCfa, collateralWorkspace, DiscountCurve);
          var dx = dynamicPricer.ExhaustionIndicator(dates[t], dates[t + 1]);
          //if credit derivative defaults unwind collateral and investor receives collateral Value net of contingent payment
          var intrinsic = Math.Max(pvc - contingentPayment, 0.0);
          v += recoveryAtMaturity ? df[df.Length - 1]*intrinsic*dx : df[t]*intrinsic*dx;
          grid[t, z] = v;
        }
      }
      var simulator = Simulator.CreateProjectiveSimulatorFactory(AsOf, dates, new[] {dates[dates.Length - 1]}, QuadraturePoints, new[] {""}).Simulator;
      var rng = MultiStreamRng.Create(QuadraturePoints, 1, Array.ConvertAll(dates, d => d - AsOf));
      Models.Simulations.Native.Simulator.AddSurvivalProcess(simulator, new SurvivalCurve(AsOf, 0.0), new Curve[]{CreditFactorVolatility}, new[,] {{1.0}}, true);
      double mass = 0.0;
      double contingentLegPv = 0.0;
      Parallel.For(0, QuadraturePoints, () => new ThreadData(),
                   (i, thread) =>
                   {
                     var path = simulator.GetSimulatedPath(i, rng);
                       var weight = path.Weight;
                       thread.Mass += weight;
                       double v = 0.0;
                       for (int t = 0; t < dates.Length - 1; ++t)
                         v += LossProcess.Evolve(t, 0, simulator, path, dynamicPricer.QuadraturePoints,
                                                 dynamicPricer.QuadratureWeights, grid);
                       thread.ContingentLeg += weight*v;
                     },
                   thread =>
                     {
                       mass += thread.Mass;
                       contingentLegPv += thread.ContingentLeg;
                     });
      return (mass > 0.0) ? contingentLegPv/mass : 0.0;
    }

    private CashflowAdapter CollateralCashflowAdapter()
    {
      var pricer = new BondPricer(CreditLinkedNote.Collateral, AsOf, Settle, 
        DiscountCurve, CollateralSurvivalCurve, 0,
          TimeUnit.None, CollateralRecovery)
        { ReferenceCurve = DiscountCurve };
      if (RateResets != null)
        pricer.CurrentRate = RateResets.CurrentReset;
      Dt next = pricer.NextCouponDate();
      Dt exDivDate = BondModelUtil.ExDivDate(pricer.Bond, next);
      return (pricer.TradeSettle < exDivDate)
        ? pricer.BondTradeCashflowAdapter
        : pricer.BondCashflowAdapter;
    }

    private double PsCollateralPv(Dt asOf, Dt settle, CashflowAdapter cfa, 
      SurvivalCurve survivalCurve, DiscountCurve discountCurve)
    {
      var pv = cfa.Pv(asOf, settle, discountCurve, survivalCurve, null, 0.0,
        StepSize, StepUnit, AdapterUtil.CreateFlags(false, false, DiscountingAccrued));
      
      if (!ToolkitConfigurator.Settings.CashflowPricer
        .IgnoreAccruedInProtection)
      {
        pv += BackwardFlagIgnoreAccruedInProtection(asOf, settle, 
          cfa, survivalCurve, discountCurve);
      }

      if (survivalCurve != null)
        pv *= survivalCurve.Interpolate(settle);
      return pv;
    }

    //this is only for test. Under current xml configuration,
    //we won't go into this function.
    private double BackwardFlagIgnoreAccruedInProtection(Dt asOf, Dt settle,
      CashflowAdapter cfa, SurvivalCurve survivalCurve, 
      DiscountCurve discountCurve)
    {
      var pv = 0.0;
      var ps = cfa.Ps;
      var ips = ps.OfType<InterestPayment>().OrderBy(p => p.PayDt).ToList();
      var maturity = ips.LastOrDefault()?.AccrualEnd ?? Dt.Empty;
      var defaultRisk = (survivalCurve == null)
        ? null
        : new DefaultRiskCalculator(asOf, settle, maturity, survivalCurve,
          null, 0.0, false, false, false, StepSize, StepUnit);
      var discountFn = LegacyCashflowCompatible.GetFn(discountCurve, settle);
      for (int i = 0; i < ips.Count; ++i)
      {
        var ip = ips[i];
        if (ip != null && defaultRisk != null)
        {
          var originalTimeFrac = ip.AccruedFractionAtDefault;
          try
          {
            if (ip.PeriodEndDate < settle)
              continue;
            ip.AccruedFractionAtDefault = 0.5;
            var accrued = ip.Accrued(settle, out var accrual);
            var riskyDiscount = defaultRisk.AccrualOnDefault(ip, discountFn);
            pv += cfa.GetDefaultAmount(i) * accrual * riskyDiscount;
          }
          finally
          {
            ip.AccruedFractionAtDefault = originalTimeFrac;
          }
        }
      }

      return pv;
    }


    /// <summary>
    /// Product Pv (no initial payment)
    /// </summary>
    /// <returns>Pv</returns>
    public override double ProductPv()
    {
      double feePv = FeeLegPv();
      double contingentLegPv = ProtectionPv();
      return (feePv + contingentLegPv);
    }
    #endregion

    #region ThreadData

    private class ThreadData
    {
      /// <summary>
      /// Constructor
      /// </summary>
      public ThreadData()
      {
        ContingentLeg = 0.0;
        Mass = 0.0;
      }

      /// <summary>
      /// Contingent leg
      /// </summary>
      public double ContingentLeg { get; set; }

      /// <summary>
      /// Probability mass
      /// </summary>
      public double Mass { get; set; }
    }

    #endregion

    #region IDynamicPricer

    /// <summary>
    /// IDynamicPricer
    /// </summary>
    protected interface IDynamicPricer
    {
      /// <summary>
      /// Evolve the curves/loss to t
      /// </summary>
      /// <param name="z">Quadrature point index</param>
      void Evolve(int z);
      /// <summary>
      /// Default contingent payment conditional on default between start and end
      /// </summary>
      /// <param name="start">start</param>
      /// <param name="end">end</param>
      /// <returns>Default contingent payment</returns>
      double ContingentPayment(Dt start, Dt end);
      /// <summary>
      /// Calc pv
      /// </summary>
      /// <param name="dt">Date</param>
      /// <returns>Pv</returns>
      double Pv(Dt dt);
      /// <summary>
      /// Indicator that the trade notional is exhausted by credit events 
      /// </summary>
      /// <param name="start">start date</param>
      /// <param name="end">end date</param>
      /// <returns><m>I_{\{\tau \leq t\}}</m></returns>
      double ExhaustionIndicator(Dt start, Dt end);
      /// <summary>
      /// Quadrature points
      /// </summary>
      double[] QuadraturePoints { get; }
      /// <summary>
      /// Quadrature weights
      /// </summary>
      double[] QuadratureWeights { get; }
     
    }
    #endregion
  }

  #endregion

  #region CdoLinkedNotePricer

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  public partial class CdoLinkedNotePricer : CreditLinkedNotePricer
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="notional">Notional</param>
    /// <param name="product">Product</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurves">Underlying credits</param>
    /// <param name="principals">Underlying principals</param>
    /// <param name="correlation">Correlation object</param>
    /// <param name="creditFactorVolatility">Volatility of undelying credit spreads</param>
    /// <param name="collateralSurvivalCurve">Survival curve of collateral piece</param>
    /// <param name="collateralRecovery">Recovery of collateral piece</param>
    /// <param name="collateralCorrelation">Correlation of collateral default time</param>
    /// <param name="stepSize">Step size for loss distribution calculation</param>
    /// <param name="stepUnit">Step unit for loss distribution calculation</param>
    public CdoLinkedNotePricer(
      Dt asOf,
      Dt settle,
      double notional,
      CreditLinkedNote product,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CorrelationObject correlation,
      VolatilityCurve creditFactorVolatility,
      SurvivalCurve collateralSurvivalCurve,
      double collateralRecovery,
      double collateralCorrelation,
      int stepSize,
      TimeUnit stepUnit)
      : base(
        asOf, settle, notional, product, discountCurve, creditFactorVolatility, collateralSurvivalCurve,
        collateralRecovery, collateralCorrelation, stepSize, stepUnit)
    {
      var cdo = (SyntheticCDO)product.CreditDerivative;
      RecoveryCurve[] rc = null;
      if (cdo.FixedRecovery && (cdo.FixedRecoveryRates != null) && (cdo.FixedRecoveryRates.Length > 0))
      {
        if (cdo.FixedRecoveryRates.Length == survivalCurves.Length)
          rc = ArrayUtil.Generate(survivalCurves.Length, i => new RecoveryCurve(asOf, cdo.FixedRecoveryRates[i]));
        else if (cdo.FixedRecoveryRates.Length == 1)
          rc = ArrayUtil.Generate(survivalCurves.Length, i => new RecoveryCurve(asOf, cdo.FixedRecoveryRates[0]));
      }
      basketPricer_ = new BaseCorrelationBasketPricerWithCpty(asOf, settle, product.Maturity, discountCurve,
                                                              collateralSurvivalCurve, collateralCorrelation,
                                                              survivalCurves, rc, correlation, principals,
                                                              stepSize, stepUnit, cdo.Attachment, cdo.Detachment, true);
      basketPricer_.NoAmortization = SyntheticCDOPricer.NeedAmortization(cdo, basketPricer_);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Survival curves of the underlying derivative
    /// </summary>
    public override SurvivalCurve[] UnderlierSurvivalCurves
    {
      get { return basketPricer_.SurvivalCurves; }
    }

    /// <summary>
    /// Survival function
    /// </summary>
    protected override Func<Dt, double> SurvivalFunction
    {
      get
      {
        var cdo = (SyntheticCDO) CreditLinkedNote.CreditDerivative;
        return date =>
        {
          double trancheWidth = cdo.Detachment - cdo.Attachment;
          double loss = basketPricer_.AccumulatedLoss(date, cdo.Attachment, cdo.Detachment);
          double cptySurvProb = (basketPricer_.CounterpartyCurve == null)
            ? 1.0
            : basketPricer_.CounterpartyCurve.Interpolate(date);
          if (cdo.AmortizePremium)
            loss += basketPricer_.AmortizedAmount(date, cdo.Attachment, cdo.Detachment);
          return Math.Max(0.0, cptySurvProb - loss / trancheWidth);
        };
      }
    }

    /// <summary>
    /// Dynamic pricer
    /// </summary>
    protected override IDynamicPricer DynamicPricer
    {
      get
      {
        return new CdoDynamicPricer(basketPricer_, DiscountCurve, CreditFactorVolatility,
          (SyntheticCDO) CreditLinkedNote.CreditDerivative, AccruedFractionAtDefault,
          DefaultTiming);
      }
    }

    /// <summary>
    /// Correlation of underlier
    /// </summary>
    public override CorrelationObject Correlation
    {
      get { return basketPricer_.Correlation; }
    }

    /// <summary>
    /// Reset loss
    /// </summary>
    public override void Reset()
    {
      basketPricer_.Reset();
    }

    #endregion

    #region Data

    private readonly BaseCorrelationBasketPricerWithCpty basketPricer_;

    #endregion

    #region IDynamicPricer

    private partial class CdoDynamicPricer : IDynamicPricer
    {
      #region Constructors

      public CdoDynamicPricer(BasketPricer basketPricer, DiscountCurve discountCurve,
        VolatilityCurve volatilityCurve, SyntheticCDO cdo, 
        double accruedFractionOnDefault, double defaultTiming)
      {
        basketPricer_ = new BaseCorrelationBasketPricerDynamic(
          basketPricer.AsOf, basketPricer.Settle,
          basketPricer.Maturity, discountCurve,
          basketPricer.SurvivalCurves, basketPricer.RecoveryCurves,
          basketPricer.Principals, basketPricer.Correlation,
          basketPricer.StepSize, basketPricer.StepUnit, true, cdo.Attachment,
          cdo.Detachment, true);
        cdo_ = cdo;
        defaultTiming_ = defaultTiming;
        accruedFractionAtDefault_ = accruedFractionOnDefault;
        discount_ = discountCurve;
        if (_usePaymentSchedule)
        {
          protectionPs_ = GeneratePsForProtection(cdo, basketPricer.Settle);
          feePs_ = GeneratePsForFee(cdo, basketPricer.Settle);
        }
        else
        {
          protectionCf_ = GenerateCashflowForProtection(cdo, basketPricer.Settle);
          feeCf_ = GenerateCashflowForFee(cdo, basketPricer.Settle);
        }
      }

      #endregion

      #region Methods

      private double LossAt(Dt dt)
      {
        double trancheWidth = cdo_.Detachment - cdo_.Attachment;
        return basketPricer_.AccumulatedLoss(dt, cdo_.Attachment, cdo_.Detachment) / trancheWidth;
      }

      private double SurvivalAt(Dt dt)
      {
        double trancheWidth = cdo_.Detachment - cdo_.Attachment;
        double loss = basketPricer_.AccumulatedLoss(dt, cdo_.Attachment, cdo_.Detachment);
        if (cdo_.AmortizePremium)
          loss += basketPricer_.AmortizedAmount(dt, cdo_.Attachment, cdo_.Detachment);
        return Math.Max(0.0, 1.0 - loss / trancheWidth);
      }

      private static PaymentSchedule GeneratePsForFee(SyntheticCDO cdo, Dt settle)
      {
        return PriceCalc.GeneratePsForFee(settle, cdo.Premium, 
          cdo.Effective, cdo.FirstPrem, cdo.Maturity,
          cdo.Ccy, cdo.DayCount, cdo.Freq, cdo.BDConvention, cdo.Calendar,
          null, false, false, null, null, null);
      }

      private static PaymentSchedule GeneratePsForProtection(SyntheticCDO cdo, Dt settle)
      {
        return PriceCalc.GeneratePsForProtection(settle, cdo.Maturity, cdo.Ccy, null);
      }

      private double ProtectionPv(Dt settle, CashflowAdapter cfa, 
        SyntheticCDO cdo, BasketPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming, 
        double accruedFractionOnDefault)
      {
        Dt maturity = cdo.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cfa, settle, discountCurve, 
          LossAt, SurvivalAt, null, false, true, false,
          defaultTiming, accruedFractionOnDefault, 
          basketPricer.StepSize, basketPricer.StepUnit, cfa.Count);
        pv += basketPricer.DefaultSettlementPv(settle, 
          cdo.Maturity, discountCurve, cdo.Attachment, cdo.Detachment,
          true, false);
        return pv;
      }

      private double FeePv(Dt settle, CashflowAdapter cfa,
        SyntheticCDO cdo, BasketPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        if (settle > cdo.Maturity)
          return 0.0;
        return PriceCalc.Price(cfa, settle, discountCurve,
          LossAt, SurvivalAt, null, true, false, false, defaultTiming,
          accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cfa.Count);
      }

      #endregion Methods

      #region IDynamicPricer Members

      public void Evolve(int z)
      {
        var basket = basketPricer_ as IDynamicDistribution;
        basket.ConditionOn(z);
      }
     
      public double ContingentPayment(Dt start, Dt end)
      {
        return 1.0;
      }

      public double Pv(Dt dt)
      {
        return _usePaymentSchedule ? PsPv(dt) : CfPv(dt);
      }

      private double PsPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, new CashflowAdapter(protectionPs_),
          cdo_, basketPricer_, discount_, defaultTiming_, accruedFractionAtDefault_);
        double fpv = FeePv(dt, new CashflowAdapter(feePs_), cdo_,
          basketPricer_, discount_, defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      public double ExhaustionIndicator(Dt start, Dt end)
      {
        var basket = basketPricer_ as IDynamicDistribution;
        return basket.ExhaustionProbability(end) - basket.ExhaustionProbability(start);
      }

      public double[] QuadraturePoints
      {
        get
        {
          var basket = basketPricer_ as IDynamicDistribution;
        return basket.QuadraturePoints;
        }
      }

      public double[] QuadratureWeights
      {
        get
        {
          var basket = basketPricer_ as IDynamicDistribution;
          return basket.QuadratureWeights;
        }
      }
      #endregion

      #region Data
      private readonly double defaultTiming_;
      private readonly double accruedFractionAtDefault_;
      private readonly BaseCorrelationBasketPricerDynamic basketPricer_;
      private readonly SyntheticCDO cdo_;
      private readonly DiscountCurve discount_;
      private readonly PaymentSchedule feePs_;
      private readonly PaymentSchedule protectionPs_;
      private readonly bool _usePaymentSchedule = true;

      #endregion
    }

    #endregion
  }

  #endregion

  #region NtdLinkedNotePricer

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  public partial class NtdLinkedNotePricer : CreditLinkedNotePricer
  {
    #region Constructor

    public NtdLinkedNotePricer(
      Dt asOf,
      Dt settle,
      double notional,
      CreditLinkedNote product,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Correlation correlation,
      VolatilityCurve creditFactorVolatility,
      SurvivalCurve collateralSurvivalCurve,
      double collateralRecovery,
      double collateralCorrelation,
      int stepSize,
      TimeUnit stepUnit)
      : base(
        asOf, settle, notional, product, discountCurve, creditFactorVolatility, collateralSurvivalCurve,
        collateralRecovery, collateralCorrelation, stepSize, stepUnit)
    {
      var ntd = (FTD)product.CreditDerivative;
      RecoveryCurve[] rc = null;
      if (ntd.FixedRecovery != null && ntd.FixedRecovery.Length > 0)
      {
        if (ntd.FixedRecovery.Length == survivalCurves.Length)
          rc = ArrayUtil.Generate(survivalCurves.Length, i => new RecoveryCurve(asOf, ntd.FixedRecovery[i]));
        else if (ntd.FixedRecovery.Length == 1)
          rc = ArrayUtil.Generate(survivalCurves.Length, i => new RecoveryCurve(asOf, ntd.FixedRecovery[0]));
      }
      basketPricer_ = new SemiAnalyticBasketForNtdPricerWithCpty(asOf, settle, product.Maturity, survivalCurves,
                                                                 rc, principals, correlation,
                                                                 collateralSurvivalCurve, collateralCorrelation,
                                                                 stepSize, stepUnit);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Survival curves of the underlying derivative
    /// </summary>
    public override SurvivalCurve[] UnderlierSurvivalCurves
    {
      get { return basketPricer_.SurvivalCurves; }
    }

    /// <summary>
    /// Survival function
    /// </summary>
    protected override Func<Dt, double> SurvivalFunction
    {
      get
      {
        var ntd = (FTD)CreditLinkedNote.CreditDerivative;
        var curve = basketPricer_.NthSurvivalCurve(ntd.First);
        return curve.Interpolate;
      }
    }

    /// <summary>
    /// Dynamic pricer
    /// </summary>
    protected override IDynamicPricer DynamicPricer
    {
      get
      {
        return new NtdDynamicPricer(basketPricer_, DiscountCurve, (FTD)CreditLinkedNote.CreditDerivative,
                                    AccruedFractionAtDefault,
                                    DefaultTiming);
      }
    }

    /// <summary>
    /// Correlation of underlier
    /// </summary>
    public override CorrelationObject Correlation
    {
      get { return basketPricer_.Correlation; }
    }

    /// <summary>
    /// Reset loss
    /// </summary>
    public override void Reset()
    {
      basketPricer_.Reset();
    }

    #endregion

    #region Data

    private readonly SemiAnalyticBasketForNtdPricerWithCpty basketPricer_;

    #endregion
    
    #region IDynamicPricer

    private partial class NtdDynamicPricer : IDynamicPricer
    {
      #region Constructors

      public NtdDynamicPricer(BasketForNtdPricer basketPricer, DiscountCurve discountCurve,
        FTD ntd, double accruedFractionOnDefault, double defaultTiming)
      {
        basketPricer_ = new SemiAnalyticBasketForNtdPricerDynamic(ntd.First, 
          basketPricer.AsOf, basketPricer.Settle,
          basketPricer.Maturity, basketPricer.SurvivalCurves,
          basketPricer.RecoveryCurves, basketPricer.Principals,
          basketPricer.Correlation,
          basketPricer.StepSize,
          basketPricer.StepUnit);
        ntd_ = ntd;
        defaultTiming_ = defaultTiming;
        accruedFractionAtDefault_ = accruedFractionOnDefault;
        discount_ = discountCurve;
        if (_usePaymentSchedule)
        {
          protectionPs_ = GeneratePsForProtection(ntd, basketPricer.Settle);
          feePs_ = GeneratePsForFee(ntd, basketPricer.Settle);
        }
        else
        {
          protectionCf_ = GenerateCashflowForProtection(ntd, basketPricer.Settle);
          feeCf_ = GenerateCashflowForFee(ntd, basketPricer.Settle);
        }
      }

      #endregion

      #region Methods

      private double LossAt(Dt dt)
      {
        return basketPricer_.NthLossCurve(ntd_.First).Interpolate(dt);
      }

      private double SurvivalAt(Dt dt)
      {
        return basketPricer_.NthSurvivalCurve(ntd_.First).Interpolate(dt);
      }
      private static PaymentSchedule GeneratePsForFee(FTD ntd, Dt settle)
      {
        return PriceCalc.GeneratePsForFee(settle, ntd.Premium, 
          ntd.Effective, ntd.FirstPrem, ntd.Maturity,
          ntd.Ccy, ntd.DayCount, ntd.Freq, ntd.BDConvention, ntd.Calendar,
          null, false, false, null, null, null);
      }

      private static PaymentSchedule GeneratePsForProtection(FTD ntd, Dt settle)
      {
        return PriceCalc.GeneratePsForProtection(settle, 
          ntd.Maturity, ntd.Ccy, null);
      }

      private double ProtectionPv(Dt settle, CashflowAdapter cf, 
        FTD ntd, BasketForNtdPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming, 
        double accruedFractionOnDefault)
      {
        Dt maturity = ntd.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cf, settle, discountCurve, 
          LossAt, SurvivalAt, null, false, true, false,
          defaultTiming, accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cf.Count);
        pv += basketPricer.DefaultSettlementPv(settle, ntd.Maturity, 
          discountCurve, ntd.First, 1, true, false);
        return pv;
      }

      private double FeePv(Dt settle, CashflowAdapter cf, FTD ntd,
        BasketForNtdPricer basketPricer,
        DiscountCurve discountCurve,
        double defaultTiming, double accruedFractionOnDefault)
      {
        if (settle > ntd.Maturity)
          return 0.0;
        return PriceCalc.Price(cf, settle, discountCurve, LossAt,
          SurvivalAt, null, true, false, false, defaultTiming,
          accruedFractionOnDefault,
          basketPricer.StepSize, basketPricer.StepUnit, cf.Count);
      }

      #endregion

      #region IDynamicPricer Members

      public void Evolve(int z)
      {
        var basket = basketPricer_ as IDynamicDistribution;
        basket.ConditionOn(z);
      }

      public double ContingentPayment(Dt start, Dt end)
      {
        var loss = basketPricer_.NthLossCurve(ntd_.First);
        var surv = basketPricer_.NthSurvivalCurve(ntd_.First);
        return (loss.Interpolate(end) - loss.Interpolate(start))/(surv.Interpolate(start) - surv.Interpolate(end));//E(\delta^(n)|\tau^(n) \in dt)
      }

      public double Pv(Dt dt)
      {
        return _usePaymentSchedule ? PsPv(dt) : CfPv(dt);
      }

      private double PsPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, new CashflowAdapter(protectionPs_), ntd_, basketPricer_, discount_, defaultTiming_,
          accruedFractionAtDefault_);
        double fpv = FeePv(dt, new CashflowAdapter(feePs_), ntd_, basketPricer_, discount_, defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      public double ExhaustionIndicator(Dt start, Dt end)
      {
        var basket = basketPricer_ as IDynamicDistribution;
        return basket.ExhaustionProbability(end) - basket.ExhaustionProbability(start);
      }

      public double[] QuadraturePoints
      {
        get
        {
          var basket = basketPricer_ as IDynamicDistribution;
          return basket.QuadraturePoints;
        }
      }

      public double[] QuadratureWeights
      {
        get
        {
          var basket = basketPricer_ as IDynamicDistribution;
          return basket.QuadratureWeights;
        }
      }
      #endregion

      #region Data

      private readonly double accruedFractionAtDefault_;
      private readonly SemiAnalyticBasketForNtdPricerDynamic basketPricer_;
      private readonly double defaultTiming_;
      private readonly DiscountCurve discount_;
      private readonly PaymentSchedule feePs_;
      private readonly PaymentSchedule protectionPs_;
      private readonly FTD ntd_;
      private readonly bool _usePaymentSchedule = true;
      #endregion
    }

    #endregion
  }

  #endregion

  #region CdsLinkedNotePricer

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  public partial class CdsLinkedNotePricer : CreditLinkedNotePricer
  {
    #region Constructor

    public CdsLinkedNotePricer(
      Dt asOf,
      Dt settle,
      double notional,
      CreditLinkedNote product,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      VolatilityCurve creditFactorVolatility,
      SurvivalCurve collateralSurvivalCurve,
      double collateralRecovery,
      double collateralCorrelation,
      int stepSize,
      TimeUnit stepUnit)
      : base(
        asOf, settle, notional, product, discountCurve, creditFactorVolatility, collateralSurvivalCurve,
        collateralRecovery, collateralCorrelation, stepSize, stepUnit)
    {
      cds_ = product.CreditDerivative as CDS;
      survivalCurve_ = survivalCurve;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Survival curves of the underlying derivative
    /// </summary>
    public override SurvivalCurve[] UnderlierSurvivalCurves
    {
      get { return new[] { survivalCurve_ }; }
    }

    /// <summary>
    /// Survival function
    /// </summary>
    protected override Func<Dt, double> SurvivalFunction
    {
      get
      {
        if (CollateralSurvivalCurve != null)
        {
          var cds = (CDS)CreditLinkedNote.CreditDerivative;
          var underlyingCredit = survivalCurve_;
          var survivalCurve = new SurvivalCurve(underlyingCredit.AsOf);
          var dates = CreateDateArray(underlyingCredit.AsOf, cds.Maturity, StepSize, StepUnit);
          foreach (var dt in dates)
          {
            double cp = 1.0 - CollateralSurvivalCurve.Interpolate(dt);
            double up = 1.0 - underlyingCredit.Interpolate(dt);
            if (cp < 1e-6 || up < 1e-6)
            {
              survivalCurve.Add(dt, 1.0);
              continue;
            }
            double cx = Normal.inverseCumulative(cp, 0.0, 1.0);
            double ux = Normal.inverseCumulative(up, 0.0, 1.0);
            double survival = BivariateNormal.cumulative(-cx, 0.0, 1.0, -ux, 0.0, 1.0, CollateralCorrelation);
            survivalCurve.Add(dt, Math.Min(Math.Max(survival, 0.0), 1.0)); //P(\tau_c > T, \tau_u > T) 
          }
          return survivalCurve.Interpolate;
        }
        return survivalCurve_.Interpolate;
      }
    }

    private Dt[] CreateDateArray(Dt from, Dt to, int stepSize, TimeUnit stepUnit)
     {
       var retVal = new List<Dt>();
       if (stepSize == 0 || stepUnit == TimeUnit.None)
       {
         stepSize = 3;
         stepUnit = TimeUnit.Months;
       }
       Dt current = from;
       for (;;)
       {
         if (current >= to)
           break;
         retVal.Add(current);
         current = Dt.Add(current, stepSize, stepUnit);
       }
       retVal.Add(to);
       return retVal.ToArray();
     }
    /// <summary>
    /// Dynamic pricer
    /// </summary>
    protected override IDynamicPricer DynamicPricer
    {
      get
      {
        return new CdsDynamicPricer(AsOf, Settle, survivalCurve_, DiscountCurve,
                                    (CollateralCorrelation > 0.0) ? Math.Sqrt(CollateralCorrelation) : 0.9, cds_,
                                    AccruedFractionAtDefault, DefaultTiming, QuadraturePoints, StepSize, StepUnit);
      }
    }

    /// <summary>
    /// Correlation of underlier
    /// </summary>
    public override CorrelationObject Correlation
    {
      get { return null; }
    }

    /// <summary>
    /// Reset loss
    /// </summary>
    public override void Reset()
    {
      return;
    }

    #endregion

    #region Data

    private readonly CDS cds_;
    private readonly SurvivalCurve survivalCurve_;
    #endregion

    #region IDynamicPricer

    private partial class CdsDynamicPricer : IDynamicPricer
    {
      #region IDynamicDistribution
      private class DynamicSurvivalCurve : IDynamicDistribution
      {
        #region Data
        private readonly Dt asOf_;
        private readonly Dt maturity_;
        private readonly double factorLoading_;
        private readonly SurvivalCurve survivalCurve_;
        private readonly int quadraturePoints_;
        private SurvivalCurve workSpace_;
        private double[] quadPoints_;
        private double[] quadWeights_;
        private double[,] conditionalSurvival_;
        private bool distributionComputed_;
        
        #endregion

        #region Constructor
        public DynamicSurvivalCurve(Dt asOf, Dt maturity, SurvivalCurve survivalCurve, double factorLoading, int quadraturePoints, int stepSize, TimeUnit stepUnit)
        {
          asOf_ = asOf;
          maturity_ = maturity;
          survivalCurve_ = survivalCurve;
          quadraturePoints_ = quadraturePoints;
          StepSize = stepSize;
          StepUnit = stepUnit;
          factorLoading_ = factorLoading;
        }
        #endregion

        #region Properties
        double[] IDynamicDistribution.QuadratureWeights { get { return quadWeights_; } }
        double[] IDynamicDistribution.QuadraturePoints { get { return quadPoints_; } }
        public int StepSize { get; private set; }
        public TimeUnit StepUnit { get; private set; }
        #endregion 

        #region Methods
        public double Interpolate(Dt dt)
        {
          return workSpace_.Interpolate(dt);
        }

        private static Dt[] GenerateGridDates(
          Dt start, Dt stop, int stepSize, TimeUnit stepUnit)
        {
          if (stepSize == 0 || stepUnit == TimeUnit.None)
          {
            stepSize = 3;
            stepUnit = TimeUnit.Months;
          }
          var timeGrid = new List<Dt>();
          for (Dt date = start;
               Dt.Cmp(date, stop) < 0;
               date = Dt.Add(date, stepSize, stepUnit))
          {
            timeGrid.Add(date);
          }
          timeGrid.Add(stop);
          return timeGrid.ToArray();
        }

        private void ComputeAndSaveDistribution()
        {
          if(!distributionComputed_)
          {
            var dts = GenerateGridDates(asOf_, maturity_, StepSize, StepUnit);
            conditionalSurvival_ = new double[dts.Length, quadraturePoints_];
            quadPoints_ = new double[quadraturePoints_];
            quadWeights_ = new double[quadraturePoints_];
            workSpace_ = new SurvivalCurve(asOf_);
            foreach (var dt in dts)
              workSpace_.Add(dt, 0.0);
            LossProcess.InitializeSurvival(asOf_, dts, new[] {factorLoading_}, survivalCurve_, quadPoints_,
                                           quadWeights_, conditionalSurvival_);
            distributionComputed_ = true;
          }
        }

        #endregion
        
        #region IDynamicDistribution Members

        void IDynamicDistribution.ConditionOn(int i)
        {
          if (!distributionComputed_)
            ComputeAndSaveDistribution();
          for (int t = 0; t < workSpace_.Count; ++t)
            workSpace_.SetVal(t, conditionalSurvival_[t, i]);
        }

        double IDynamicDistribution.ExhaustionProbability(Dt date)
        {
          return 1.0 - workSpace_.Interpolate(date);
        }



        #endregion
      }
      #endregion

      #region Constructors

      public CdsDynamicPricer(Dt asOf, Dt settle, 
        SurvivalCurve survivalCurve,
        DiscountCurve discountCurve, double factorLoading, 
        CDS cds, double accruedFractionOnDefault,
        double defaultTiming, int quadraturePoints, 
        int stepSize, TimeUnit stepUnit)
      {
        cds_ = cds;
        dynamicSurvival_ = new DynamicSurvivalCurve(asOf, cds.Maturity, 
          survivalCurve, factorLoading, quadraturePoints,
          stepSize, stepUnit);
        recoveryRate_ = cds.FixedRecovery
          ? cds.FixedRecoveryRate
          : (survivalCurve.SurvivalCalibrator != null &&
             survivalCurve.SurvivalCalibrator.RecoveryCurve != null)
            ? survivalCurve.SurvivalCalibrator.RecoveryCurve.RecoveryRate(cds.Maturity)
            : 0.0;
        defaultTiming_ = defaultTiming;
        accruedFractionAtDefault_ = accruedFractionOnDefault;
        discount_ = discountCurve;
        if (_usePaymentSchedule)
        {
          protectionPs_ = GeneratePsForProtection(cds, settle);
          feePs_ = GeneratePsForFee(cds, settle);
        }
        else
        {
          protectionCf_ = GenerateCashflowForProtection(cds, settle);
          feeCf_ = GenerateCashflowForFee(cds, settle);
        }
      }

      #endregion

      #region Methods

      private double LossAt(Dt dt)
      {
        return (1.0 - dynamicSurvival_.Interpolate(dt)) * (1 - recoveryRate_);
      }

      private double SurvivalAt(Dt dt)
      {
        return dynamicSurvival_.Interpolate(dt);
      }

      private static PaymentSchedule GeneratePsForFee(CDS cds, Dt settle)
      {
        return PriceCalc.GeneratePsForFee(settle, cds.Premium, cds.Effective, cds.FirstPrem, cds.Maturity,
          cds.Ccy, cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar,
          null, false, false, null, null, null);
      }

      private static PaymentSchedule GeneratePsForProtection(CDS cds, Dt settle)
      {
        return PriceCalc.GeneratePsForProtection(settle, cds.Maturity, cds.Ccy, null);
      }


      private double ProtectionPv(Dt settle, CashflowAdapter cf, CDS cds, DiscountCurve discountCurve, double defaultTiming,
                                  double accruedFractionOnDefault)
      {
        Dt maturity = cds.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cf, settle, discountCurve, LossAt, SurvivalAt, null, false, true, false,
                                    defaultTiming, accruedFractionOnDefault, dynamicSurvival_.StepSize, dynamicSurvival_.StepUnit, cf.Count);
        return pv;
      }

      private double FeePv(Dt settle, CashflowAdapter cf, CDS cds, DiscountCurve discountCurve, double defaultTiming,
                           double accruedFractionOnDefault)
      {
        if (settle > cds.Maturity)
          return 0.0;
        return PriceCalc.Price(cf, settle, discountCurve, LossAt, SurvivalAt, null, true, false, false, defaultTiming,
                               accruedFractionOnDefault, dynamicSurvival_.StepSize, dynamicSurvival_.StepUnit, cf.Count);
      }

      #endregion
      
      #region IDynamicPricer Members

      public void Evolve(int z)
      {
        var dynamicDist = dynamicSurvival_ as IDynamicDistribution;
        dynamicDist.ConditionOn(z);
      }

      public double ContingentPayment(Dt start, Dt end)
      {
        return 1.0 - recoveryRate_;
      }

      public double Pv(Dt dt)
      {
        return _usePaymentSchedule ? PsPv(dt) : CfPv(dt);
      }

      private double PsPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, new CashflowAdapter(protectionPs_), cds_, discount_, defaultTiming_, accruedFractionAtDefault_);
        double fpv = FeePv(dt, new CashflowAdapter(feePs_), cds_, discount_, defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      public double ExhaustionIndicator(Dt start, Dt end)
      {
        var dynamicDist = dynamicSurvival_ as IDynamicDistribution;
        return dynamicDist.ExhaustionProbability(end) - dynamicDist.ExhaustionProbability(start);
      }
      
      public double[] QuadraturePoints
      {
        get
        {
          var dynamicDist = dynamicSurvival_ as IDynamicDistribution;
          return dynamicDist.QuadraturePoints;
        }
      }

      public double[] QuadratureWeights
      {
        get
        {
          var dynamicDist = dynamicSurvival_ as IDynamicDistribution;
          return dynamicDist.QuadratureWeights;
        }
      }
      #endregion

      #region Data
      private readonly CDS cds_;
      private readonly double accruedFractionAtDefault_;
      private readonly double defaultTiming_;
      private readonly DiscountCurve discount_;
      private readonly PaymentSchedule feePs_;
      private readonly PaymentSchedule protectionPs_;
      private readonly double recoveryRate_;
      private readonly DynamicSurvivalCurve dynamicSurvival_;
      private readonly bool _usePaymentSchedule = true;

      #endregion
    }

    #endregion
  }
  #endregion

  #region BasketCdsLinkedNotePricer

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  public partial class BasketCdsLinkedNotePricer : CreditLinkedNotePricer
  {
    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="notional">Notional</param>
    /// <param name="product">Product</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurves">Underlying credits</param>
    /// <param name="principals">Underlying principals</param>
    /// <param name="creditFactorVolatility">Volatility of undelying credit spreads</param>
    /// <param name="collateralSurvivalCurve">Survival curve of collateral piece</param>
    /// <param name="collateralRecovery">Recovery curve of collateral piece</param>
    /// <param name="collateralCorrelation">Correlation of collateral default time</param>
    /// <param name="stepSize">Step size for loss distribution calculation</param>
    /// <param name="stepUnit">Step unit for loss distribution calculation</param>
    public BasketCdsLinkedNotePricer(
      Dt asOf,
      Dt settle,
      double notional,
      CreditLinkedNote product,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      VolatilityCurve creditFactorVolatility,
      SurvivalCurve collateralSurvivalCurve,
      double collateralRecovery,
      double collateralCorrelation,
      int stepSize,
      TimeUnit stepUnit)
      : base(
        asOf, settle, notional, product, discountCurve, creditFactorVolatility, collateralSurvivalCurve,
        collateralRecovery, collateralCorrelation, stepSize, stepUnit)
    {
      var basketCds = (BasketCDS)product.CreditDerivative;
      RecoveryCurve[] rc = null;
      if (basketCds.FixedRecovery && basketCds.FixedRecoveryRate >= 0.0)
        rc = ArrayUtil.Generate(survivalCurves.Length, i => new RecoveryCurve(asOf, basketCds.FixedRecoveryRate));
      basketPricer_ = new BaseCorrelationBasketPricerWithCpty(asOf, settle, product.Maturity, discountCurve,
                                                              collateralSurvivalCurve, collateralCorrelation,
                                                              survivalCurves, rc, null, principals,
                                                              stepSize, stepUnit, 0.0, 1.0, true) {NoAmortization = false};
    }

    #endregion

    #region Properties

    /// <summary>
    /// Survival curves of the underlying derivative
    /// </summary>
    public override SurvivalCurve[] UnderlierSurvivalCurves
    {
      get { return basketPricer_.SurvivalCurves; }
    }

    /// <summary>
    /// Survival function
    /// </summary>
    protected override Func<Dt, double> SurvivalFunction
    {
      get
      {
        return date =>
               {
                 double loss = basketPricer_.AccumulatedLoss(date, 0.0, 1.0);
                 loss += basketPricer_.AmortizedAmount(date, 0.0, 1.0);
                 double cptySurvivalProb = (basketPricer_.CounterpartyCurve == null)
                                             ? 1.0
                                             : basketPricer_.CounterpartyCurve.Interpolate(date);
                 return Math.Max(0.0, cptySurvivalProb - loss);
               };
      }
    }

    /// <summary>
    /// Dynamic pricer
    /// </summary>
    protected override IDynamicPricer DynamicPricer
    {
      get
      {
        return new
          BasketCdsDynamicPricer(basketPricer_, (BasketCDS)CreditLinkedNote.CreditDerivative, DiscountCurve,
                                  AccruedFractionAtDefault, DefaultTiming);
      }
    }

    /// <summary>
    /// Correlation of underlier
    /// </summary>
    public override CorrelationObject Correlation
    {
      get { return basketPricer_.Correlation; }
    }

    /// <summary>
    /// Reset loss
    /// </summary>
    public override void Reset()
    {
      basketPricer_.Reset();
    }

    #endregion

    #region Data

    private readonly BaseCorrelationBasketPricerWithCpty basketPricer_;
    
    #endregion
    
    #region IDynamicPricer

    private partial class BasketCdsDynamicPricer : IDynamicPricer
    {
      #region Constructors
     

      public BasketCdsDynamicPricer(BasketPricer basketPricer, BasketCDS basketCds, DiscountCurve discountCurve, double accruedFractionOnDefault, double defaultTiming)
      {
        basketPricer_ = new BaseCorrelationBasketPricerDynamic(basketPricer.AsOf, basketPricer.Settle,
                                                               basketPricer.Maturity, discountCurve,
                                                               basketPricer.SurvivalCurves, basketPricer.RecoveryCurves,
                                                               basketPricer.Principals, null,
                                                               basketPricer.StepSize,
                                                               basketPricer.StepUnit, true, 0.0,
                                                               1.0, true);
        maxLoss_ = basketPricer_.MaximumLossLevel();
        basketCds_ = basketCds;
        defaultTiming_ = defaultTiming;
        accruedFractionAtDefault_ = accruedFractionOnDefault;
        discount_ = discountCurve;
        if (_usePaymentSchedule)
        {
          protectionPs_ = GeneratePsForProtection(basketCds, basketPricer.Settle);
          feePs_ = GeneratePsForFee(basketCds, basketPricer.Settle);
        }
        else
        {
          protectionCf_ = GenerateCashflowForProtection(basketCds, basketPricer.Settle);
          feeCf_ = GenerateCashflowForFee(basketCds, basketPricer.Settle);
        }
      }

      #endregion

      #region Methods

      private double LossAt(Dt dt)
      {
        return basketPricer_.AccumulatedLoss(dt, 0.0, 1.0);
      }

      private double SurvivalAt(Dt dt)
      {
        double loss = basketPricer_.AccumulatedLoss(dt, 0.0, 1.0);
        loss += basketPricer_.AmortizedAmount(dt, 0.0, 1.0);
        return Math.Max(0.0, 1.0 - loss);
      }


      private static PaymentSchedule GeneratePsForFee(BasketCDS basketCds, Dt settle)
      {
        return PriceCalc.GeneratePsForFee(settle, basketCds.Premium, basketCds.Effective, basketCds.FirstPrem,
          basketCds.Maturity, basketCds.Ccy, basketCds.DayCount, basketCds.Freq,
          basketCds.BDConvention, basketCds.Calendar, null, false, false, null, null, null);
      }

      private static PaymentSchedule GeneratePsForProtection(BasketCDS basketCds, Dt settle)
      {
        return PriceCalc.GeneratePsForProtection(settle, basketCds.Maturity, basketCds.Ccy, null);
      }

      private double ProtectionPv(Dt settle, CashflowAdapter cf, BasketCDS basketCds, BasketPricer basketPricer, DiscountCurve discountCurve,
                                  double defaultTiming, double accruedFractionOnDefault)
      {
        Dt maturity = basketCds.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cf, settle, discountCurve, LossAt, SurvivalAt, null, false, true, false,
                                    defaultTiming, accruedFractionOnDefault, basketPricer.StepSize,
                                    basketPricer.StepUnit, cf.Count);
        pv += basketPricer.DefaultSettlementPv(settle, basketCds.Maturity, discountCurve, 0.0, 1.0, true, false);
        return pv;
      }

      private double FeePv(Dt settle, CashflowAdapter cf, BasketCDS basketCds, BasketPricer basketPricer,
                           DiscountCurve discountCurve, double defaultTiming, double accruedFractionOnDefault)
      {
        if (settle > basketCds.Maturity)
          return 0.0;
        return PriceCalc.Price(cf, settle, discountCurve, LossAt, SurvivalAt, null, true, false, false, defaultTiming,
                               accruedFractionOnDefault, basketPricer.StepSize, basketPricer.StepUnit, cf.Count);
      }

      #endregion     

      #region IDynamicPricer Members
      public void Evolve(int z)
      {
        var basket = basketPricer_ as IDynamicDistribution;
        basket.ConditionOn(z);
      }

      public double ContingentPayment(Dt start, Dt end)
      {
        return maxLoss_;
      }

      public double Pv(Dt dt)
      {
        return _usePaymentSchedule ? PsPv(dt) : CfPv(dt);
      }

      private double PsPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, new CashflowAdapter(protectionPs_), 
          basketCds_, basketPricer_, discount_, defaultTiming_,
          accruedFractionAtDefault_);
        double fpv = FeePv(dt, new CashflowAdapter(feePs_), basketCds_, 
          basketPricer_, discount_, defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      public double ExhaustionIndicator(Dt start, Dt end)
      {
        var basket = basketPricer_ as IDynamicDistribution;
        return basket.ExhaustionProbability(end) - basket.ExhaustionProbability(start);
      }

      public double[] QuadraturePoints
      {
        get
        {
          var basket = basketPricer_ as IDynamicDistribution;
          return basket.QuadraturePoints;
        }
      }

      public double[] QuadratureWeights
      {
        get
        {
          var basket = basketPricer_ as IDynamicDistribution;
          return basket.QuadratureWeights;
        }
      }
      #endregion

      #region Data

      private readonly double accruedFractionAtDefault_;
      private readonly BaseCorrelationBasketPricerDynamic basketPricer_;
      private readonly BasketCDS basketCds_;
      private readonly double defaultTiming_;
      private readonly DiscountCurve discount_;
      private readonly PaymentSchedule feePs_;
      private readonly PaymentSchedule protectionPs_;
      private readonly double maxLoss_;
      private readonly bool _usePaymentSchedule = true;

      #endregion
    }

    #endregion
  }
  #endregion
}