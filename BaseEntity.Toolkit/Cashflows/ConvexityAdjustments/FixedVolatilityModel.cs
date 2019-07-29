using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions.Utilities;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

// ReSharper disable once CheckNamespace
namespace BaseEntity.Toolkit.Cashflows
{
  static class ConvexityAdjustmentUtility
  {
    /// <summary>
    /// Attaches the volatility fixers to the specified payments.
    /// The volatility levels will be fixed on the first calculation.
    /// </summary>
    /// <param name="payments">The payments.</param>
    public static void AttachVolatilityFixers(
      IEnumerable<Payment> payments)
    {
      if (payments == null) return;

      foreach (var payment in payments)
      {
        AttachVolatilityFixer(payment);
      }
    }

    /// <summary>
    /// Fixes the convexity adjustment volatilities
    /// at the current rate levels for the specified
    /// volatility start dates.
    /// </summary>
    /// <param name="payments">The payments.</param>
    /// <param name="volatilityStartDates">The volatility start dates.</param>
    public static void FixVolatility(
      IEnumerable<Payment> payments,
      IReadOnlyList<Dt> volatilityStartDates)
    {
      if (payments == null) return;

      foreach (var payment in payments)
      {
        FixVolatility(payment, volatilityStartDates);
      }
    }

    private static void AttachVolatilityFixer(Payment payment)
    {
      FixVolatility(payment, null);
    }

    private static void FixVolatility(
      Payment payment, IReadOnlyList<Dt> dates)
    {
      var scaled = payment as ScaledPayment;
      while (scaled != null)
      {
        payment = scaled.UnderlyingPayment;
        scaled = payment as ScaledPayment;
      }

      var ip = payment as IHasForwardAdjustment;
      if (ip?.ForwardAdjustment == null) return;

      ip.ForwardAdjustment = ip.ForwardAdjustment.WithVolatilityFixing();
      if (dates == null || dates.Count == 0) return;

      // Force rate calculations at all the exposure dates
      var savedDt = payment.VolatilityStartDt;
      for (int i = 0, n = dates.Count; i < n; ++i)
      {
        payment.VolatilityStartDt = dates[i];
        CalculateAmount(payment);
      }
      payment.VolatilityStartDt = savedDt;
    }

    private static double CalculateAmount(Payment payment)
    {
      return payment.DomesticAmount;
    }

    private static RateModelParameters WithVolatilityFixing(
      this RateModelParameters rateModelParameters)
    {
      var parameters = (RateModelParameters1D[])
        rateModelParameters.Parameters.Clone();
      for (int i = 0; i < parameters.Length; ++i)
      {
        var p = parameters[i];
        parameters[i] = p.ReplaceModel(new FixedVolatilityModel(p.Model));
      }
      var cloned = (RateModelParameters) rateModelParameters.ShallowCopy();
      cloned.Parameters = parameters;
      return cloned;
    }

    private static ForwardAdjustment WithVolatilityFixing(
      this IForwardAdjustment forwardAdjustment)
    {
      var fwd = forwardAdjustment as ForwardAdjustment;
      if (fwd == null) return fwd;
      var cloned = (ForwardAdjustment) fwd.ShallowCopy();
      cloned.RateModelParameters = cloned.
        RateModelParameters.WithVolatilityFixing();
      return cloned;
    }

  }

  class FixedVolatilityModel : ForwardModel
  {
    #region Nested Types

    // Volatility kind
    enum Kind
    {
      Normal,
      LogNormal,
      ShiftedLogNormal
    }

    struct Key
    {
      public readonly double Time;
      public readonly Dt Tenor;

      public Key(double time, Dt tenor)
      {
        Time = time;
        Tenor = tenor;
      }
    }

    struct Input
    {
      public double Sigma, Kappa;
    }

    class Comparer : IEqualityComparer<Key>
    {
      public bool Equals(Key x, Key y)
      {
        return x.Time.AlmostEquals(y.Time)
          && x.Tenor == y.Tenor;
      }

      public int GetHashCode(Key obj)
      {
        return HashCodeCombiner.Combine(
          obj.Time.GetHashCode(), obj.Tenor.GetHashCode());
      }
    }

    #endregion

    #region Data members

    private static readonly Comparer KeyComparer = new Comparer();

    private readonly Dictionary<Key, Input> _m2Data
      = new Dictionary<Key, Input>(KeyComparer);

    private readonly Dictionary<Key, Input> _optionData
      = new Dictionary<Key, Input>(KeyComparer);

    private readonly Kind _kind;
    private readonly ForwardModel _model;
    public ForwardModel BackModel => _model;

    #endregion

    #region Constructor

    public FixedVolatilityModel(ForwardModel model)
    {
      var fvm = model as FixedVolatilityModel;
      if (fvm != null)
      {
        _model = fvm._model;
        _kind = fvm._kind;
        return;
      }

      _model = model;
      if (model is LogNormalBlack || model is Replication || model is Sabr)
      {
        _kind = Kind.LogNormal;
      }
      else if (model is NormalBlack || model is ReplicationNormal)
      {
        _kind = Kind.Normal;
      }
      else if (model is ShiftedLogNormal)
      {
        _kind = Kind.ShiftedLogNormal;
      }
      else
      {
        throw new ToolkitException("Unknown model");
      }
    }

    #endregion

    public override double ImpliedVolatility(double f, double t,
      double strike, RateModelParameters1D parameters, Dt tenor)
    {
      throw new NotImplementedException();
    }

    public override double ImpliedNormalVolatility(double f, double t,
      double strike, RateModelParameters1D parameters, Dt tenor)
    {
      throw new NotImplementedException();
    }

    #region Option

    public override double Option(OptionType type, double f, double ca,
      double t, double strike, RateModelParameters1D parameters, Dt tenor)
    {
      var value = GetOptionInput(f, t, strike, parameters, tenor);
      var v = value.Sigma;
      switch (_kind)
      {
      case Kind.LogNormal:
        return ForwardModelUtils.BlackPrice(type, f + ca, strike, t, v);
      case Kind.Normal:
        return ForwardModelUtils.NormalBlackPrice(type, f + ca, strike, t, v);
      case Kind.ShiftedLogNormal:
      {
        var kappa = value.Kappa;
        return ForwardModelUtils.BlackPrice(type, f - kappa + ca, strike - kappa, t, v);
      }
      }
      throw new ToolkitException($"Invalid model kind {_kind}");
    }

    private Input GetOptionInput(double f, double t,
      double strike, RateModelParameters1D parameters, Dt tenor)
    {
      if (t < 1e-3) return Zero;

      Input value;
      if (_optionData.TryGetValue(new Key(t, tenor), out value))
        return value;

      value = CalculateOptionInput(f, t, strike, parameters, tenor);
      _optionData.Add(new Key(t, tenor), value);
      return value;
    }

    private Input CalculateOptionInput(double f, double t,
      double strike, RateModelParameters1D parameters, Dt tenor)
    {
      double sigma, kappa = 0;
      var model = _model;
      if (model is NormalBlack)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, f, parameters.ReferenceIndex); // ATM
      }
      else if (model is ReplicationNormal)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, strike, parameters.ReferenceIndex); ;
      }
      else if (model is LogNormalBlack)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, f, parameters.ReferenceIndex); // ATM
      }
      else if (model is Replication)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, strike, parameters.ReferenceIndex);
      }
      else if (model is Sabr)
      {
        sigma = ((Sabr)model).ImpliedVolatility(f, t, strike, parameters, tenor);
      }
      else if (model is ShiftedLogNormal)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, strike, parameters.ReferenceIndex);
        kappa = parameters.Interpolate(RateModelParameters.Param.Kappa,
          tenor, strike, parameters.ReferenceIndex);
      }
      else
      {
        throw new ToolkitException($"Unable to handle forward model '{model}'");
      }
      return new Input { Sigma = sigma, Kappa = kappa };
    }

    #endregion

    #region Second moments

    public override double SecondMoment(double f, double t,
      RateModelParameters1D parameters, Dt tenor)
    {
      var value = GetM2Input(f, t, parameters, tenor);
      var v = value.Sigma;
      if (v.AlmostEquals(0.0))
      {
        return f*f;
      }
      switch (_kind)
      {
      case Kind.LogNormal:
        return f*f*Math.Exp(v*v*t);
      case Kind.Normal:
        return f*f + v*v*t;
      case Kind.ShiftedLogNormal:
      {
        var kappa = value.Kappa;
        double init = f - kappa;
        return init*init*Math.Exp(v*v*t) + 2*kappa*f - kappa*kappa;
      }
      }
      throw new ToolkitException($"Invalid model kind {_kind}");
    }

    private Input GetM2Input(double f, double t,
      RateModelParameters1D parameters, Dt tenor)
    {
      if (t < 1e-3) return Zero;

      Input value;
      if (_m2Data.TryGetValue(new Key(t, tenor), out value))
        return value;

      value = CalculateM2Input(f, t, parameters, tenor);
      _m2Data.Add(new Key(t, tenor), value);
      return value;
    }

    private Input CalculateM2Input(double f, double t,
      RateModelParameters1D parameters, Dt tenor)
    {
      double sigma, kappa = 0;
      var model = _model;
      if (model is NormalBlack)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, f, parameters.ReferenceIndex);
      }
      else if (model is ReplicationNormal)
      {
        var e2 = model.SecondMoment(f, t, parameters, tenor);
        var v2 = e2 - f*f;
        sigma = v2 > 0 ? Math.Sqrt(v2/t) : 0;
      }
      else if (model is LogNormalBlack)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, f, parameters.ReferenceIndex);
      }
      else if (model is Replication || model is Sabr)
      {
        double e2 = model.SecondMoment(f, t, parameters, tenor);
        var v2 = Math.Log(e2) - Math.Log(f*f);
        sigma = v2 > 0 ? Math.Sqrt(v2/t) : 0;
      }
      else if (model is ShiftedLogNormal)
      {
        sigma = parameters.Interpolate(RateModelParameters.Param.Sigma,
          tenor, f, parameters.ReferenceIndex);
        kappa = parameters.Interpolate(RateModelParameters.Param.Kappa,
          tenor, f, parameters.ReferenceIndex);
      }
      else
      {
        throw new ToolkitException($"Unable to handle forward model '{model}'");
      }
      return new Input {Sigma = sigma, Kappa = kappa};
    }

    #endregion

    private static readonly Input Zero = new Input {Sigma = 0, Kappa = 0};
  }

}
