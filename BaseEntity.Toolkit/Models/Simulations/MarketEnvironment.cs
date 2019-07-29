using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Models.Simulations
{
  #region Configuration

  /// <summary>
  ///  Simulation configuration.
  /// </summary>
  /// <exclude />
  [Serializable]
  public class SimulationConfig
  {
    /// <exclude />
    [ToolkitConfig("Enable correction for the discrepancies caused by curve date change")]
    public readonly bool EnableCorrectionForCurveTenorChange = true;

    /// <exclude />
    [ToolkitConfig("Enable calibrate rate volatilities with dual curves")]
    public readonly bool EnableDualCurveVolatility = true;

    /// <exclude />
    [ToolkitConfig("If true, always use approximation for fast PV calculations; otherwise, use the pricer specific settings")]
    public readonly bool AlwaysUseApproximateForFastCalculation = true;
  }
  #endregion

  #region MarketEnvironment

  /// <summary>
  /// Market environment
  /// </summary>
  [Serializable]
  public class MarketEnvironment
  {
    #region Static constructor
    public static MarketEnvironment Create(
      Dt asOf, Dt[] tenors,
      Tenor gridSize,
      Currency valuationCurrency,
      Currency numeraireCurrency,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      DiscountCurve[] rateCurves,
      FxRate[] fxRates,
      CalibratedCurve[] fwdPriceCurves,
      SurvivalCurve[] creditCurves,
      IEnumerable<IJumpSpecification> jumpsOnDefault = null)
    {
      int numeraireIdx = Array.FindIndex(rateCurves, df => df.Ccy == valuationCurrency);
      if (numeraireIdx < 0)
        throw new ToolkitException("Discounting curve not found.");
      if (numeraireIdx > 0)
      {
        rateCurves = (DiscountCurve[])rateCurves.Clone();
        Swap(ref rateCurves[0], ref rateCurves[numeraireIdx]);
      }
      if (valuationCurrency != numeraireCurrency)
      {
        var fxRate = Array.Find(fxRates,
                                fx =>
                                (fx.ToCcy == valuationCurrency && fx.FromCcy == numeraireCurrency) ||
                                (fx.FromCcy == valuationCurrency && fx.ToCcy == numeraireCurrency));
        if (fxRate == null)
          throw new ToolkitException("FxRate from valuation currency to numeraire currency not found.");
      }
      if (tenors == null)
      {
        tenors = Array.ConvertAll(volatilities.Tenors, t => Dt.Add(asOf, t));
      }
      else
      {
        tenors = tenors.Where(d => d > asOf).ToArray();
      }
      return new MarketEnvironment(asOf, tenors, rateCurves,
        fwdPriceCurves.GetForwardBased(volatilities, factorLoadings),
        creditCurves, fxRates,
        fwdPriceCurves.GetSpotBased(volatilities, factorLoadings),
        jumpsOnDefault);
    }

    private static void Swap<T>(ref T object1, ref T object2)
    {
      var dum = object1;
      object1 = object2;
      object2 = dum;
    }

    #endregion

    #region Properties

    /// <summary>
    /// As of date
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// Forward tenors to simulate
    /// </summary>
    public Dt[] Tenors { get; private set; }

    /// <summary>
    /// Underlying credit curves
    /// </summary>
    public SurvivalCurve[] CreditCurves { get; private set; }

    /// <summary>
    /// Underlying discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves { get; private set; }

    /// <summary>
    /// Underlying forward price term structures
    /// </summary>
    public CalibratedCurve[] ForwardCurves { get; private set; }

    /// <summary>
    /// Underlying spot FX rates
    /// </summary>
    public FxRate[] FxRates { get; private set; }

    /// <summary>
    /// Underlying spot prices
    /// </summary>
    public CalibratedCurve[] SpotBasedCurves { get; private set; }

    private Jumps JumpsOnDefault { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">asOf date</param>
    /// <param name="tenors">Tenors defining the libor family modeled</param>
    /// <param name="discountCurves">Discount curves</param>
    /// <param name="forwardCurves">Term structure of forward prices</param>
    /// <param name="creditCurves">Credit curves</param>
    /// <param name="fxRates">Spot fx rates</param>
    /// <param name="spotBasedCurves">Term structures of forward prices based on spot asset price and zero yield curve</param>
    /// <param name="jumpsOnDefault">The jumps on default</param>
    /// <remarks>spotBasedCurves objects supplied as spotBasedCurves will be simulated as spot / zero bond</remarks>
    public MarketEnvironment(Dt asOf, Dt[] tenors,
      DiscountCurve[] discountCurves, CalibratedCurve[] forwardCurves,
      SurvivalCurve[] creditCurves, FxRate[] fxRates,
      CalibratedCurve[] spotBasedCurves, 
      IEnumerable<IJumpSpecification> jumpsOnDefault = null)
    {
      AsOf = asOf;
      Tenors = tenors ?? EmptyArray<Dt>.Instance;
      DiscountCurves = discountCurves ?? EmptyArray<DiscountCurve>.Instance;
      FxRates = SetUpFxRates(DiscountCurves, fxRates);
      SpotBasedCurves = spotBasedCurves ?? EmptyArray<CalibratedCurve>.Instance;
      ForwardCurves = forwardCurves ?? EmptyArray<CalibratedCurve>.Instance;
      CreditCurves = creditCurves ?? EmptyArray<SurvivalCurve>.Instance;
      JumpsOnDefault = Jumps.Create(DiscountCurves, ForwardCurves,
        CreditCurves, FxRates, SpotBasedCurves, jumpsOnDefault);
    }

    #endregion

    #region Methods
    
    private static FxRate[] SetUpFxRates(DiscountCurve[] discountCurves, FxRate[] fxRates)
    {
      if (fxRates == null || discountCurves.Length < 1)
        return new FxRate[0];
      Currency domesticCcy = discountCurves[0].Ccy;
      var foreignCurrencies = discountCurves.Skip(1).Select(d => d.Ccy).ToList(); 
      var foreignDomesticFx = discountCurves.Skip(1).Select(d =>
                                           {
                                             var retVal =
                                               fxRates.FirstOrDefault(
                                                 fx => (fx.FromCcy == d.Ccy && fx.ToCcy == domesticCcy) || (fx.ToCcy == d.Ccy && fx.FromCcy == domesticCcy));
                                             if (retVal != null)
                                               return retVal;
                                             throw new ArgumentException(String.Format("FxRate for ccy {0} not found", d.Ccy));
                                           });
      var foreignForeignFx = fxRates.Where(fx => foreignCurrencies.Contains(fx.FromCcy) && foreignCurrencies.Contains(fx.ToCcy));
      var sortedFxRates = foreignDomesticFx.ToList();
      sortedFxRates.AddRange(foreignForeignFx);
      return sortedFxRates.ToArray(); 
    }

    /// <summary>
    /// Conform curve tenors to those explicitly provided.  
    /// </summary>
    public void Conform()
    {
      if (Tenors.Length > 0)
      {
        foreach (DiscountCurve dc in DiscountCurves)
          Conform(dc, Tenors);
        foreach (SurvivalCurve cc in CreditCurves)
          Conform(cc, Tenors);
        foreach (CalibratedCurve fc in ForwardCurves)
          Conform(fc, Tenors);
      }
    }
    
    //Other interpolations are too time consuming
    private static void ResetInterp(Curve curve)
    {
      var method = InterpMethod.Custom;
      try
      {
        method = curve.InterpMethod;
      }
      catch (Exception)
      {}
      if (method == InterpMethod.Weighted || method == InterpMethod.Linear)
        return;
      curve.Interp = DefaultInterp ?? CreateLinearInterp();
    }

    private static Interp CreateLinearInterp()
    {
      Extrap lower = new Const();
      Extrap upper = new Smooth();
      return new Linear(upper, lower);
    }

    public static Interp DefaultInterp { get; set; }

    /// <summary>
    /// Conform curve tenors to those explicitly provided
    /// </summary>
    /// <param name="curve">CalibratedCurve object</param>
    /// <param name="tenors">Given tenors</param>
    private void Conform(Curve curve, Dt[] tenors)
    {
      if (curve == null)
        return;
      var points = MergePoints(curve, Tenors);
      if (tenors[0] <= curve.AsOf)
        curve.AsOf = AsOf;
      var y = Array.ConvertAll(tenors, curve.Interpolate);
      curve.Clear();
      ResetInterp(curve);
      curve.AsOf = AsOf;
      curve.Add(tenors, y);
      CreateOverlayCorrection(curve, points);
    }

    private static void CreateOverlayCorrection(
      Curve curve, DateAndValue<double>[] points)
    {
      if (points == null) return;

      var overlay = new Curve(curve.AsOf,
        new Weighted(), curve.DayCount, curve.Frequency);
      foreach (var pt in points)
      {
        double val = curve.Interpolate(pt.Date);
        if (Math.Abs(val) > double.Epsilon)
          overlay.Add(pt.Date, pt.Value / val);
        else // for very small denominator, factor is always 1.0
          overlay.Add(pt.Date, 1.0);
      }

      // Convert into a composite curve 
      var native = (NativeCurve)curve;
      var baseCurve = native.clone();
      native.SetCustomInterp(new CorrectiveOverlay(baseCurve, overlay));
    }

    private static DateAndValue<double>[] MergePoints(
      Curve curve, Dt[] tenors)
    {
      if (!ToolkitConfigurator.Settings.Simulations.
        EnableCorrectionForCurveTenorChange ||
        // bet on the safe side: do nothing if the curve is already composite
        curve.CustomInterpolator != null || curve.Overlay != null)
      {
        return null;
      }

      var dates = new UniqueSequence<Dt>(tenors);
      foreach (var pt in curve)
      {
        dates.Add(pt.Date);
      }
      var n = dates.Count;
      if (n == curve.Count)
      {
        // tenor date not changed
        return null;
      }
      if (dates[n - 1] == tenors[tenors.Length - 1])
      {
        dates.Add(Dt.Add(dates[n - 1], 1, TimeUnit.Years));
      }
      var points = new DateAndValue<double>[n];
      for (int i = 0; i < n; ++i)
      {
        points[i] = DateAndValue.Create(
          dates[i], curve.Interpolate(dates[i]));
      }
      return points;
    }

    public static NativeCurve GetNative(Curve curve)
    {
      var overlay = curve.CustomInterpolator as CorrectiveOverlay;
      if (overlay == null) return curve;
      return overlay.BaseCurve;
    }

    // Function to check that there is no corrective overlay.
    //
    // This is used when curves are first added to simulator,
    // which happens before we call MarketEnvironment.Conform().
    // Hence there should no corrective overlay.
    public static NativeCurve NoCorrectiveOverlay(Curve curve)
    {
      var overlay = curve.CustomInterpolator as CorrectiveOverlay;
      Debug.Assert(overlay == null);
      return curve;
    }

    #endregion

    #region Apply jumps

    public void ApplyDiscountJump(int index, Dt date)
    {
      JumpsOnDefault?.DiscountJumps?[index]?.ApplyJump(date);
    }

    public void ApplyForwardJump(int index, Dt date)
    {
      JumpsOnDefault?.ForwardJumps?[index]?.ApplyJump(date);
    }

    public void ApplyCreditJump(int index, Dt date)
    {
      JumpsOnDefault?.CreditJumps?[index]?.ApplyJump(date);
    }

    public void ApplyFxJump(int index, Dt date)
    {
      JumpsOnDefault?.FxJumps?[index]?.ApplyJump(date);
    }

    public void ApplySpotJump(int index, Dt date)
    {
      JumpsOnDefault?.SpotBasedJumps?[index]?.ApplyJump(date);
    }

    #endregion

    #region Nested type: Jumps

    [Serializable]
    private class Jumps
    {
      #region Constructor

      public static Jumps Create(
        DiscountCurve[] discountCurves, CalibratedCurve[] forwardCurves,
        SurvivalCurve[] creditCurves, FxRate[] fxRates,
        CalibratedCurve[] spotBasedCurves,
        IEnumerable<IJumpSpecification> jumpOnDefaults)
      {
        if (jumpOnDefaults == null) return null;

        bool hasJump = false;
        IJumpSpecification[] discountJumps = null, forwardJumps = null,
          creditJumps = null, fxJumps = null, spotBasedJumps = null;
        foreach (var jump in jumpOnDefaults)
        {
          hasJump |= (SetJump(jump, fxRates, ref fxJumps) ||
            SetJump(jump, spotBasedCurves, ref spotBasedJumps) ||
            SetJump(jump, creditCurves, ref creditJumps) ||
            SetJump(jump, forwardCurves, ref forwardJumps) ||
            SetJump(jump, discountCurves, ref discountJumps));
        }

        if (!hasJump) return null;
        return new Jumps(discountJumps, forwardJumps,
          creditJumps, fxJumps, spotBasedJumps);
      }

      private static bool SetJump<T>(
        IJumpSpecification jump,
        T[] array,
        ref IJumpSpecification[] jumps) where T: class
      {
        int idx;
        var t = jump.MarketObject as T;
        if (t != null && (idx = Array.IndexOf(array, t)) >= 0)
        {
          if(jumps == null) jumps = new IJumpSpecification[array.Length];
          jumps[idx] = jump;
          return true;
        }
        return false;
      }

      private Jumps(
        IJumpSpecification[] discountJumps,
        IJumpSpecification[] forwardJumps,
        IJumpSpecification[] creditJumps,
        IJumpSpecification[] fxJumps,
        IJumpSpecification[] spotBasedJumps)
      {
        DiscountJumps = discountJumps;
        ForwardJumps = forwardJumps;
        CreditJumps = creditJumps;
        FxJumps = fxJumps;
        SpotBasedJumps = spotBasedJumps;
      }

      #endregion

      #region Properties

      /// <summary>
      /// Underlying credit curves
      /// </summary>
      public IJumpSpecification[] CreditJumps { get; }

      /// <summary>
      /// Underlying discount curves
      /// </summary>
      public IJumpSpecification[] DiscountJumps { get; }

      /// <summary>
      /// Underlying forward price term structures
      /// </summary>
      public IJumpSpecification[] ForwardJumps { get; }

      /// <summary>
      /// Underlying spot FX rates
      /// </summary>
      public IJumpSpecification[] FxJumps { get; }

      /// <summary>
      /// Underlying spot prices
      /// </summary>
      public IJumpSpecification[] SpotBasedJumps { get; }

      #endregion
    }

    #endregion
  }

  /// <summary>
  /// Class CorrectiveOverlay is for simulation use only.
  /// </summary>
  [Serializable]
  public class CorrectiveOverlay : MultiplicativeOverlay
  {
    public CorrectiveOverlay(NativeCurve baseCurve, Curve overlay)
      : base(baseCurve, overlay) { }
  }

  #endregion
}