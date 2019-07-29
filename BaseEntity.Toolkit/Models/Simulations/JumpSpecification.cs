/*
 * 
 */
using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using JumpKind = BaseEntity.Toolkit.Sensitivity.ScenarioShiftType;
using static BaseEntity.Toolkit.Models.Simulations.MarketEnvironment;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Interface IJumpSpecification, currently used to specify
  /// the jumps of market values on counterparty default.
  /// </summary>
  public interface IJumpSpecification
  {
    /// <summary>
    /// Gets the market object associated with the jumps
    /// </summary>
    /// <value>The market object</value>
    object MarketObject { get; }

    /// <summary>
    /// Applies the jump on the specified to the associated market object.
    /// </summary>
    /// <param name="date">The date</param>
    void ApplyJump(Dt date);
  }

  /// <summary>
  /// The class of factory methods to create various jump specifications.
  /// </summary>
  public static class JumpSpecification
  {
    /// <summary>
    /// Create the jump specification for FX rates
    /// </summary>
    /// <param name="fxRate">The FX rate</param>
    /// <param name="kind">The type of jumps (Absolute, Relative, or Specified)</param>
    /// <param name="valueFn">The value function</param>
    /// <returns>IJumpSpecification</returns>
    public static IJumpSpecification FxRateJump(
      FxRate fxRate,
      JumpKind kind,
      Func<Dt, double> valueFn)
    {
      return new FxRateJumpSpec(fxRate, kind, valueFn);
    }

    /// <summary>
    /// Create the jump specification for spot prices
    /// </summary>
    /// <param name="priceCurve">The spot based forward price curve</param>
    /// <param name="kind">The type of jumps (Absolute, Relative, or Specified)</param>
    /// <param name="valueFn">The value function</param>
    /// <returns>IJumpSpecification</returns>
    public static IJumpSpecification SpotJump(
      IForwardPriceCurve priceCurve,
      JumpKind kind,
      Func<Dt, double> valueFn)
    {
      return new SpotBasedJumpSpec(priceCurve, kind, valueFn);
    }

    /// <summary>
    /// Create the jump specification for the specified curve
    /// </summary>
    /// <param name="curve">The curve</param>
    /// <param name="kind">The type of jumps (Absolute, Relative, or Specified)</param>
    /// <param name="valueFn">The value function</param>
    /// <returns>IJumpSpecification</returns>
    public static IJumpSpecification CurveJump(
      Curve curve,
      JumpKind kind,
      Func<Dt, double> valueFn)
    {
      return (curve.DayCount == DayCount.None)
        ? new CurveValueJumpSpec(curve, kind, valueFn)
        : (JumpSpec) new CurveRateJumpSpec(curve, kind, valueFn);
    }
  }

  #region JumpSpec base class

  internal abstract class JumpSpec : IJumpSpecification
  {
    internal JumpSpec(
      object mktObj,
      JumpKind kind,
      Func<Dt, double> fn)
    {
      MarketObject = mktObj;
      Kind = kind;
      ValueFn = fn;
    }

    public object MarketObject { get; }

    public JumpKind Kind { get; }

    public Func<Dt, double> ValueFn { get; }

    public abstract void ApplyJump(Dt date);
  }

  #endregion

  #region CurveRateJumpSpec

  internal class CurveRateJumpSpec : JumpSpec
  {
    internal CurveRateJumpSpec(
      Curve curve,
      JumpKind kind,
      Func<Dt, double> fn)
      : base(curve, kind, fn)
    {
    }

    private Curve Curve => (Curve) MarketObject;

    public override void ApplyJump(Dt date)
    {
      Debug.Assert(Curve != null);
      var curve = GetNative(Curve);
      for (int i = 0, n = curve.Size(); i < n; ++i)
      {
        if (curve.GetDt(i) <= date) continue;
        var rate = curve.GetY(i);
        var jump = ValueFn(date);
        switch (Kind)
        {
        case JumpKind.Absolute:
          curve.SetRate(i, rate + jump);
          break;
        case JumpKind.Relative:
          curve.SetRate(i, rate*jump);
          break;
        case JumpKind.Specified:
          curve.SetRate(i, jump);
          break;
        }

      }
      return;
    }

  }

  #endregion

  #region CurveValueJumpSpec

  internal class CurveValueJumpSpec : JumpSpec
  {
    internal CurveValueJumpSpec(
      Curve curve,
      JumpKind kind,
      Func<Dt, double> fn)
      : base(curve, kind, fn)
    {
    }

    private Curve Curve => (Curve) MarketObject;

    public override void ApplyJump(Dt date)
    {
      Debug.Assert(Curve != null);
      var curve = GetNative(Curve);
      for (int i = 0, n = curve.Size(); i < n; ++i)
      {
        if (curve.GetDt(i) < date) continue;
        var value = curve.GetVal(i);
        var jump = ValueFn(date);
        switch (Kind)
        {
        case JumpKind.Absolute:
          curve.SetVal(i, value + jump);
          break;
        case JumpKind.Relative:
          curve.SetVal(i, value*jump);
          break;
        case JumpKind.Specified:
          curve.SetVal(i, jump);
          break;
        }

      }
      return;
    }

  }

  #endregion

  #region FxRateJumpSpec

  [Serializable]
  internal class FxRateJumpSpec : JumpSpec
  {
    internal FxRateJumpSpec(
      FxRate fx,
      JumpKind kind,
      Func<Dt, double> fn)
      : base(fx, kind, fn)
    {
    }

    private FxRate FxRate => (FxRate) MarketObject;

    public override void ApplyJump(Dt date)
    {
      Debug.Assert(FxRate != null);
      var fx = FxRate;
      var rate = fx.Rate;
      var value = ValueFn(date);
      switch (Kind)
      {
      case JumpKind.Absolute:
        rate += value;
        break;
      case JumpKind.Relative:
        rate *= value;
        break;
      case JumpKind.Specified:
        rate = value;
        break;
      }
      fx.Rate = rate;
    }
  }

  #endregion

  #region SpotBasedJumpSpec

  internal class SpotBasedJumpSpec : JumpSpec
  {
    internal SpotBasedJumpSpec(
      IForwardPriceCurve curve,
      JumpKind kind,
      Func<Dt, double> fn)
      : base(curve, kind, fn)
    {
    }

    private IForwardPriceCurve Curve => (IForwardPriceCurve) MarketObject;

    public override void ApplyJump(Dt date)
    {
      Debug.Assert(Curve != null);
      var spot = Curve.Spot;
      var jump = ValueFn(date);
      switch (Kind)
      {
      case JumpKind.Absolute:
        spot.Value += jump;
        break;
      case JumpKind.Relative:
        spot.Value *= jump;
        break;
      case JumpKind.Specified:
        spot.Value = jump;
        break;
      }
      return;
    }
  }

  #endregion
}
