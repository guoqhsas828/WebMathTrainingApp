// 
//  -2012. All rights reserved.
// 

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   Provide an evaluation function for sensitivity calculation.
  /// </summary>
  /// <remarks></remarks>
  public interface IEvaluatorProvider
  {
    /// <summary>
    /// Gets the evaluation function for the specified measure.
    /// </summary>
    /// <param name="measure">The measure.</param>
    /// <returns>A delegate to calculate the specified measure.</returns>
    /// <remarks></remarks>
    Func<double> GetEvaluator(string measure);
  }

  /// <summary>
  /// A wrapper class to IPricer object
  /// </summary>
  /// <remarks>
  ///   <para>For internal use only.</para>
  /// </remarks>
  /// <exclude />
  public sealed class PricerEvaluator : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <exclude />
    public PricerEvaluator(IPricer pricer)
      : this(pricer, "Pv", false, true)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="methodName">Name of the evaluation method</param>
    /// <exclude />
    public PricerEvaluator(IPricer pricer, string methodName)
      : this(pricer, methodName, false, String.IsNullOrEmpty(methodName) || methodName.Equals("Pv") )
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="methodName">Name of the evaluation method</param>
    /// <param name="allowMissing">Allow missing method. In this case return 0 when evaluated</param>
    /// <param name="isAdditive">Whether the pricer measure is additive</param>
    /// <exclude />
    public PricerEvaluator(IPricer pricer, string methodName, bool allowMissing, bool isAdditive)
      : this(pricer, GetEvaluator(pricer, String.IsNullOrEmpty(methodName) ? "Pv" : methodName, allowMissing))
    {
      SensitivityFlags = isAdditive ? AdditiveFlag : 0;
      methodName_ = String.IsNullOrEmpty(methodName) ? "Pv" : methodName;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="methodInfo">Evaluation method</param>
    /// <exclude />
    public PricerEvaluator(IPricer pricer, MethodInfo methodInfo)
    {
#if DEBUG
      if (pricer == null)
        throw new ArgumentException("pricer cannot be null");
#endif
      if (methodInfo == null)
      {
        Method = DoublePricerFnBuilder.CreateDelegate(pricer.GetType(), "Pv");
        SensitivityFlags = AdditiveFlag;
        methodName_ = "Pv";
      }
      else
      {
        Method = DoublePricerFnBuilder.CreateDelegate(methodInfo, pricer.GetType(), true);
        methodName_ = methodInfo.Name;
      }
      Pricer = pricer;
      InitializeBasketGetter();
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="function">Evaluation function</param>
    /// <exclude />
    public PricerEvaluator(IPricer pricer, Double_Pricer_Fn function)
    {
#if DEBUG
      if (pricer == null)
        throw new ArgumentException("pricer cannot be null");
#endif
      Pricer = pricer;
      Method = function;
      InitializeBasketGetter();
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <returns>Cloned evaluator</returns>
    public override object Clone()
    {
      var obj = (PricerEvaluator)base.Clone();
      obj.Pricer = (IPricer)Pricer.Clone();
      if (methodName_ != null && Method != null)
      {
        obj.Method = GetEvaluator(obj.Pricer,
          methodName_, false) ?? obj.Method;
      }
      obj.InitializeBasketGetter();
      return obj;
    }

    private static Double_Pricer_Fn GetEvaluator(
      IPricer pricer, string measure, bool allowMissing)
    {
      var provider = pricer as IEvaluatorProvider;
      if (provider != null)
      {
        var fn = provider.GetEvaluator(measure);
        if (fn != null) return p => fn();
      }
      if (!allowMissing)
      {
        return DoublePricerFnBuilder.CreateDelegate(pricer.GetType(), measure);
      }
      // Here we allow missing methods.
      var pricerType = pricer.GetType();
      var method = DoublePricerFnBuilder.FindMethod(pricerType, measure);
      return (method != null) ? DoublePricerFnBuilder.CreateDelegate(method, pricerType, false) : null;
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    /// Evaluate the required value
    /// </summary>
    /// <remarks>
    ///   <para>This function invokes the underlying pricer method and returns the result.</para>
    /// </remarks>
    /// <returns>value</returns>
    public double Evaluate()
    {
      return (Method != null) ? Method(Pricer) : 0.0;
    }

    /// <summary>
    /// Evaluate the required value given a specified external pricer
    /// </summary>
    /// <remarks>
    ///   <para>This function invokes the specified pricer method and returns the result.</para>
    /// </remarks>
    /// <returns>value</returns>
    public double Evaluate(IPricer pricer)
    {
      return Substitute(pricer).Evaluate();
    }

    /// <summary>
    /// Create a copy with substituted pricer
    /// </summary>
    /// <param name="pricer">pricer </param>
    /// <returns>New adapter</returns>
    public PricerEvaluator Substitute(IPricer pricer)
    {
#if DEBUG
      if (!Pricer.GetType().IsInstanceOfType(pricer))
        throw new ArgumentException(String.Format(
          "Pricer type {0} not match {1}", pricer.GetType(), Pricer.GetType()));
#endif
      var evaluator = (PricerEvaluator)ShallowCopy();
      if (methodName_ != null && Method != null)
        evaluator.Method = GetEvaluator(pricer, methodName_, false) ?? Method;
      evaluator.Pricer = pricer;
      InitializeBasketGetter();
      return evaluator;
    }

    /// <summary>
    /// Total accrued for product to as-of date given pricing arguments
    /// </summary>
    /// <returns>Total accrued interest</returns>
    public double Accrued()
    {
      return Pricer.Accrued();
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some internal state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that this internate state should be cleared.</para>
    /// </remarks>
    public PricerEvaluator Reset()
    {
      BasketPricer basket = Basket;
      if (basket != null)
      {
        if (DefaultChanged && basket.ExactJumpToDefault)
        {
          basket.Reset(null);
          var cdoPricer = Pricer as SyntheticCDOPricer;
          if (cdoPricer != null)
            cdoPricer.UpdateEffectiveNotional();
        }
        else if (IncludeRecoverySensitivity)
          basket.ResetRecoveryRates();
        basket.Reset();
      }
      if (!(Pricer is SyntheticCDOPricer))
        Pricer.Reset();
      return this;
    }

    /// <summary>
    /// Reset the pricer for modified correlations and/or recovery rates
    /// </summary>
    internal void Reset(bool recoveryRateModified, bool correlationModified)
    {
      BasketPricer basket = Basket;
      if (basket != null)
      {
        if (DefaultChanged && basket.ExactJumpToDefault)
        {
          basket.Reset(null);
          var cdoPricer = Pricer as SyntheticCDOPricer;
          if (cdoPricer != null)
            cdoPricer.UpdateEffectiveNotional();
        }
        else if (recoveryRateModified)
          basket.ResetRecoveryRates();
        if (correlationModified)
          basket.ResetCorrelation();
        basket.Reset();
      }
      if (!(Pricer is SyntheticCDOPricer))
        Pricer.Reset();
    }

    /// <summary>
    /// Initialize the property getters of basket based
    /// pricers: currently SyntheticCDOPricer and CDOOptionPricer.
    /// </summary>
    internal void InitializeBasketGetter()
    {
      if (Pricer is SyntheticCDOPricer)
      {
        basketGetter_ = cdoBasketGetter_;
        discountCurveGetter_ = cdoDiscountCurveGetter_;
        return;
      }
      if (Pricer is CDOOptionPricer)
      {
        basketGetter_ = cdoOptionBasketGetter_;
        discountCurveGetter_ = cdoOptionDiscountCurveGetter_;
        return;
      }

      basketGetter_ = null;
      discountCurveGetter_ = null;
    }

    #endregion Methods

    #region PricerPropertyAccess
    /// <summary>
    /// Funding curves (i.e. curves using for discounting cash-flows)
    /// </summary>
    public DiscountCurve[] DiscountCurves
    {
      get
      {
        if (DiscountCurvesGetter == null)
          DiscountCurvesGetter = PropertyGetBuilder.CreateFundingCurveGetter(Pricer.GetType()).Get;
        return DiscountCurvesGetter(Pricer);
      }
    }


    /// <summary>
    /// Survival Curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get
      {
        if (SurvivalCurvesGetter == null)
          SurvivalCurvesGetter = PropertyGetBuilder.CreateSurvivalGetter(Pricer.GetType()).Get;
        return SurvivalCurvesGetter(Pricer);
      }
    }

    /// <summary>
    /// Recovery Curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get
      {
        if (RecoveryCurvesGetter == null)
          RecoveryCurvesGetter = PropertyGetBuilder.CreateRecoveryGetter(Pricer.GetType()).Get;
        return RecoveryCurvesGetter(Pricer);
      }
    }

    /// <summary>
    /// Reference curves
    /// </summary>
    public CalibratedCurve[] ReferenceCurves
    {
      get
      {
        if (ReferenceCurvesGetter == null)
          ReferenceCurvesGetter = PropertyGetBuilder.CreateReferenceCurveGetter(Pricer.GetType()).Get;
        return ReferenceCurvesGetter(Pricer);
      }
    }


    /// <summary>
    /// Stock curves
    /// </summary>
    public StockCurve[] StockCurves
    {
      get
      {
        if (StockCurvesGetter == null)
          StockCurvesGetter = PropertyGetBuilder.CreateStockCurveGetter(Pricer.GetType()).Get;
        return StockCurvesGetter(Pricer);
      }
    }

    /// <summary>
    /// Commodity curves
    /// </summary>
    public CommodityCurve[] CommodityCurves
    {
      get
      {
        if (CommodityCurvesGetter == null)
          CommodityCurvesGetter = PropertyGetBuilder.CreateCommodityCurveGetter(Pricer.GetType()).Get;
        return CommodityCurvesGetter(Pricer);
      }
    }

    /// <summary>
    /// Inflation curves
    /// </summary>
    public InflationCurve[] InflationCurves
    {
      get
      {
        if (InflationCurvesGetter == null)
          InflationCurvesGetter = PropertyGetBuilder.CreateInflationCurveGetter(Pricer.GetType()).Get;
        return InflationCurvesGetter(Pricer);
      }
    }


    /// <summary>
    /// Discount Curves
    /// </summary>
    public CalibratedCurve[] RateCurves
    {
      get
      {
        if (RateCurvesGetter == null)
          RateCurvesGetter = PropertyGetBuilder.CreateDiscountGetter(Pricer.GetType()).Get;
        if (FxCurveGetter == null)
          FxCurveGetter = PropertyGetBuilder.CreateFxCurveGetter(Pricer.GetType()).Get;
        CalibratedCurve[] dcs = RateCurvesGetter(Pricer);
        CalibratedCurve[] fx = FxCurveGetter(Pricer);
        if (fx == null || fx.Length <= 0)
          return dcs;
        var retVal = new List<CalibratedCurve>();
        if (dcs != null && dcs.Length > 0)
          retVal.AddRange(dcs);
        foreach (FxCurve crv in fx)
        {
          if (!crv.IsSupplied)
          {
            var c = crv.Ccy2DiscountCurve;
            if (!retVal.Contains(c)) retVal.Add(c);
            c = crv.Ccy1DiscountCurve;
            if (!retVal.Contains(c)) retVal.Add(c);
          }
        }
        return retVal.ToArray();
      }
    }

    /// <summary>
    /// Rate Volatility Cubes for the pricer
    /// </summary>
    public RateVolatilityCube[] RateVolatilityCubes
    {
      get
      {
        if (RateVolatilityCubeGetter == null)
          RateVolatilityCubeGetter = PropertyGetBuilder.CreateRateVolatilityCubeGetter(Pricer.GetType()).Get;
        return RateVolatilityCubeGetter(Pricer);
      }
    }

    /// <summary>
    /// Fx Curves
    /// </summary>
    public CalibratedCurve[] FxCurve
    {
      get
      {
        if (FxCurveGetter == null)
          FxCurveGetter = PropertyGetBuilder.CreateFxCurveGetter(Pricer.GetType()).Get;
        return FxCurveGetter(Pricer);
      }
    }

    /// <summary>
    /// Basis Adjustment Curves
    /// </summary>
    public CalibratedCurve[] BasisAdjustmentCurves
    {
      get
      {
        if (BasisAdjustmentGetter == null)
          BasisAdjustmentGetter = PropertyGetBuilder.CreateBasisCurveGetter(Pricer.GetType()).Get;
        return BasisAdjustmentGetter(Pricer);
      }
    }

    /// <summary>
    /// Correlation Objects Getter
    /// </summary>
    public CorrelationObject[] Correlations
    {
      get
      {
        if (CorrelationsGetter == null)
          CorrelationsGetter = PropertyGetBuilder.CreateCorrelationGetter(Pricer.GetType()).Get;
        return CorrelationsGetter(Pricer);
      }
    }

    /// <summary>
    /// Correlation Object
    /// </summary>
    public CorrelationObject Correlation
    {
      get
      {
        CorrelationObject[] corrs = Correlations;
        if (corrs == null || corrs.Length == 0)
          return null;
        return corrs[0];
      }
    }

    /// <summary>
    /// Determine if the pricer depends on a curve
    /// </summary>
    /// <param name="curve">Curve to look for</param>
    /// <returns>True if the pricer depends on the curve; False otherwise.</returns>
    /// <remarks>
    ///   <para>This method is an O(n) operation, where n is the Length of array.
    ///   If the performance turns out not acceptable, consider change it to an
    ///   O(log(n)) operation with some set-up overhead.
    ///  </para>
    /// </remarks>
    public bool DependsOn(Curve curve)
    {
      if (dependentCurves_ != null && Array.IndexOf(dependentCurves_, curve) >= 0)
        return true;
      var ccurve = curve as CalibratedCurve;
      if (ccurve != null && ccurve.HasFxForwardQuote())
      {
        var curves = new[] {this}.GetFxForwardCurves(false);
        return (curves != null) && curves.Contains(ccurve);
      }
      var sc = curve as SurvivalCurve;
      if (sc != null)
      {
        var curves = Basket?.OriginalBasket?.SurvivalCurves ?? SurvivalCurves;
        return (curves != null) && Array.IndexOf(curves, sc) >= 0;
      }
      var dc = curve as DiscountCurve;
      if (dc != null)
      {
        var curves = RateCurves;
        var calibrator = dc.Calibrator as OverlayCalibrator;
        if (calibrator != null)
        {
          CalibratedCurve[] refs;
          return curves.Any(c => c == calibrator.BaseCurve)
            || (refs = ReferenceCurves) != null && refs.OfType<InflationCurve>().Any(
              c => c != null && c.TargetCurve == calibrator.BaseCurve);
        }
        return (curves != null) && curves.Contains(curve);
      }
      var fx = curve as FxCurve;
      if (fx != null)
      {
        var curves = FxCurve;
        return (curves != null) && curves.Contains(curve);
      }
      var rc = curve as CalibratedCurve;
      if (rc != null)
      {
        var curves = ReferenceCurves;
        if ((curves != null) && curves.Contains(curve))
          return true;
      }
      var stockCurve = curve as StockCurve;
      if (stockCurve != null)
      {
        var curves = StockCurves;
        return ((curves != null) && curves.Contains(curve));
      }
      var commodityCurve = curve as CommodityCurve;
      if (commodityCurve != null)
      {
        var curves = CommodityCurves;
        return ((curves != null) && curves.Contains(curve));
      }
      var inflationCurve = curve as InflationCurve;
      if (inflationCurve != null)
      {
        var curves = InflationCurves;
        return (curves != null) && curves.Contains(curve);
      }
      return false;
    }

    internal void SetDependentCurves(Curve[] curves)
    {
      dependentCurves_ = curves;
    }

    #endregion PricerPropertyAccess

    #region Properties

    /// <summary>
    /// Underlying pricer
    /// </summary>
    public IPricer Pricer { get; private set; }

    /// <summary>
    /// Underlying basket
    /// </summary>
    /// <remarks>
    ///   This property returns an associated basket if the underlying
    ///   pricer is a SyntheticCDOPricer or CDOOptionPricer;
    ///   otherwise, it returns null.
    /// </remarks>
    internal BasketPricer Basket
    {
      get { return (BasketPricer)(basketGetter_ == null ? null : basketGetter_(Pricer)); }
    }

    /// <summary>
    /// Underlying discount curve
    /// </summary>
    /// <remarks>
    ///   This property returns an associated discount curve
    ///   if the underlying pricer is a SyntheticCDOPricer
    ///   or CDOOptionPricer; otherwise, it returns null.
    /// </remarks>
    internal DiscountCurve DiscountCurve
    {
      get { return (DiscountCurve)(discountCurveGetter_ == null ? null : discountCurveGetter_(Pricer)); }
    }

    /// <summary>
    /// Underlying pricer type
    /// </summary>
    public Type PricerType
    {
      get { return Pricer.GetType(); }
    }

    /// <summary>
    /// Underlying PVFlags
    /// </summary>
    internal PricerFlags PricerFlags
    {
      get
      {
        var p = Pricer as PricerBase;
        if (p != null)
          return p.PricerFlags;
        return 0;
      }
      set
      {
        var p = Pricer as PricerBase;
        if (p != null)
          p.PricerFlags = value;
      }
    }

    /// <summary>
    /// Evaluation method
    /// </summary>
    public string MethodName
    {
      get { return methodName_ ?? Method.Method.Name; }
    }

    /// <summary>
    /// Evaluation method
    /// </summary>
    private Double_Pricer_Fn Method { get; set; }

    /// <summary>
    /// Evaluation method
    /// </summary>
    public SurvivalDeltaCalculator SurvivalBumpedEval
    {
      get
      {
        if (survivalBumpedEval_ == null)
          survivalBumpedEval_ = SurvivalDeltaCalculator.Create(Pricer, MethodName);
        return survivalBumpedEval_;
      }
    }

    #region IPricer_Properties

    /// <summary>
    /// As-of date
    /// </summary>
    public Dt AsOf
    {
      get { return Pricer.AsOf; }
      set
      {
        Pricer.AsOf = value;
        if (Pricer.PaymentPricer != null)
          Pricer.PaymentPricer.AsOf = value;
      }
    }

    /// <summary>
    /// Settle date
    /// </summary>
    public Dt Settle
    {
      get { return Pricer.Settle; }
      set
      {
        Pricer.Settle = value;
        if (Pricer.PaymentPricer != null)
          Pricer.PaymentPricer.Settle = value;
      }
    }

    /// <summary>
    /// Product
    /// </summary>
    public IProduct Product { get { return Pricer.Product; } }

    #endregion IPricer_Properties
    
    /// <summary>Commodity Curves Getter</summary>
    internal Func<IPricer, InflationCurve[]> InflationCurvesGetter { get; set; }

    /// <summary>Commodity Curves Getter</summary>
    internal Func<IPricer, CommodityCurve[]> CommodityCurvesGetter { get; set; }
    
    /// <summary>Stock Curves Getter</summary>
    internal Func<IPricer, StockCurve[]> StockCurvesGetter { get; set; }
    
    /// <summary>Reference Curves Getter</summary>
    internal Func<IPricer, DiscountCurve[]> DiscountCurvesGetter { get; set; }

    /// <summary>Reference Curves Getter</summary>
    internal Func<IPricer, CalibratedCurve[]> ReferenceCurvesGetter { get; set; }

    /// <summary>Rate Curves Getter</summary>
    internal Func<IPricer, CalibratedCurve[]> RateCurvesGetter { get; set; }

    /// <summary>Rate Curves Getter</summary>
    internal Func<IPricer, CalibratedCurve[]> FxCurveGetter { get; set; }

    /// <summary>Survival Curves Getter</summary>
    internal Func<IPricer, SurvivalCurve[]> SurvivalCurvesGetter { get; set; }

    /// <summary>Recovery Curves Getter</summary>
    internal Func<IPricer, RecoveryCurve[]> RecoveryCurvesGetter { get; set; }

    /// <summary>Recovery Curves Getter</summary>
    internal Func<IPricer, CorrelationObject[]> CorrelationsGetter { get; set; }

    /// <summary>Rate volatility Getter</summary>
    internal Func<IPricer, RateVolatilityCube[]> RateVolatilityCubeGetter { get; set; }

    /// <summary>Basis adjustment Getter</summary>
    internal Func<IPricer, CalibratedCurve[]> BasisAdjustmentGetter { get; set; }

    #endregion Properties

    #region Data

    //-
    // The following members are created on the first access
    // to the related properties/methods.
    //-
    private SurvivalDeltaCalculator survivalBumpedEval_ = null;
    private Curve[] dependentCurves_ = null;
    private Object_Object_Fn basketGetter_;
    private Object_Object_Fn discountCurveGetter_;
    private readonly string methodName_;

    #region Static_Data

    private static Object_Object_Fn cdoBasketGetter_ = delegate(object p) { return ((SyntheticCDOPricer)p).Basket; };
    private static Object_Object_Fn cdoOptionBasketGetter_ = delegate(object p) { return ((CDOOptionPricer)p).Basket; };
    private static Object_Object_Fn cdoDiscountCurveGetter_ = delegate(object p) { return ((SyntheticCDOPricer)p).DiscountCurve; };
    private static Object_Object_Fn cdoOptionDiscountCurveGetter_ = delegate(object p) { return ((CDOOptionPricer)p).DiscountCurve; };

    #endregion Static_Data

    #endregion Data

    #region Efficiency_Hacks

    internal const int AdditiveFlag = 1;
    internal const int DefaultChangedFlag = 2;
    internal const int WithRecoverySensitivity = 4;

    /// <summary>
    ///   Sensitivity flags
    /// </summary>
    internal int SensitivityFlags { get; set; }

    /// <summary>
    ///   Has the default status changed?
    /// </summary>
    public bool DefaultChanged
    {
      get { return (SensitivityFlags & DefaultChangedFlag) != 0; }
    }

    /// <summary>
    ///   Is the price measure additive?
    /// </summary>
    public bool IsAdditive
    {
      get { return (SensitivityFlags & AdditiveFlag) != 0; }
    }

    /// <summary>
    ///   Include recovery sensitivity in survival delta calculations
    ///   <preliminary />
    /// </summary>
    public bool IncludeRecoverySensitivity
    {
      get { return (SensitivityFlags & WithRecoverySensitivity) != 0; }
      set
      {
        if (value)
          SensitivityFlags |= WithRecoverySensitivity;
        else
          SensitivityFlags &= ~WithRecoverySensitivity;
      }
    }

    #endregion Efficiency_Hacks
  }
}
