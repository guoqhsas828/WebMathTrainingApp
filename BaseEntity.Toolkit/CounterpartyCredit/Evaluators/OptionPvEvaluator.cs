/*
 * 
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Ccr;

namespace BaseEntity.Toolkit.Ccr
{
  internal class OptionPvEvaluator : IPvEvaluator
  {
    #region Nested type: Forward

    /// <summary>
    /// This simply wraps the original underlying for volatility.
    /// The value and numeraire are from evaluable expressions.
    /// </summary>
    class Forward : IUnderlier
    {
      private readonly IUnderlier _original;
      private double _level, _numeraire;

      public Forward(IUnderlier original)
      {
        _original = original;
        _level = double.NaN;
        _numeraire = double.NaN;
      }

      public void Set(double level, double numeraire)
      {
        _level = level;
        _numeraire = numeraire;
      }

      public double Value(Dt dt, out double numeraire)
      {
        numeraire = _numeraire;
        return _level;
      }

      public double Vol(Dt dt)
      {
        return _original.Vol(dt);
      }
    }

    #endregion

    #region Instance members

    private readonly OptionCcrPricer _ccrPricer;
    private readonly Action<int, Dt, Forward> _setForwardAction;

    private OptionPvEvaluator(OptionCcrPricer ccrPricer,
      Action<int, Dt, Forward> setter)
    {
      var under = new Forward(ccrPricer.Underlier);
      _ccrPricer = ccrPricer;
      _ccrPricer.Underlier = under;
      _setForwardAction = setter;
    }

    public double FastPv(int exposureIndex, Dt exposureDate)
    {
      var underlier = (Forward) _ccrPricer.Underlier;
      _setForwardAction(exposureIndex, exposureDate, underlier);
      return _ccrPricer.FastPv(exposureDate);
    }

    #endregion

    #region Static builder

    public static IPvEvaluator Get(OptionCcrPricer pricer,
      IReadOnlyList<Dt> exposureDates)
    {
      if (pricer == null) return null;

      pricer = (OptionCcrPricer) pricer.ShallowCopy();
      var under = pricer.Underlier;
      var setter = GetForwardSetter(under as SwapRate, exposureDates)
        ?? GetForwardSetter(under as ForwardFxRate, exposureDates);
      if (setter == null) return null;
      return new OptionPvEvaluator(pricer, setter);
    }

    #endregion

    #region ISimulationPricer Members

    public Currency Ccy
    {
      get { return _ccrPricer.Ccy; }
    }

    public double FastPv(Dt settle)
    {
      throw new NotImplementedException();
    }

    public bool DependsOn(object marketObject)
    {
      return _ccrPricer.DependsOn(marketObject);
    }

    public Dt[] ExposureDates
    {
      get { return _ccrPricer.ExposureDates; }
      set { _ccrPricer.ExposureDates = value; }
    }

    #endregion

    #region Forward value setters

    private static Action<int, Dt, Forward> GetForwardSetter(
      SwapRate swapRate, IReadOnlyList<Dt> exposureDates)
    {
      if (swapRate == null) return null;

      var fixedLeg = PvEvaluator.Get(swapRate.FixedLegPricer, exposureDates);
      var floatLeg= PvEvaluator.Get(swapRate.FloatingLegPricer, exposureDates);
      return (exposureIndex, exposureDate, forward) =>
      {
        var annuity = fixedLeg.FastPv(exposureIndex, exposureDate)/swapRate.UnitCoupon;
        var floatValue = floatLeg.FastPv(exposureIndex, exposureDate);
        var rateLevel = floatValue/annuity;
        forward.Set(rateLevel, annuity);
      };
    }

    private static Action<int, Dt, Forward> GetForwardSetter(
      ForwardFxRate fxRate, IReadOnlyList<Dt> exposureDates)
    {
      if (fxRate == null) return null;

      var spot = fxRate.ForwardFxCurve.SpotFxRate;
      var discountCurve = fxRate.DiscountCurve;

      int count = exposureDates.Count;
      var numeraires = new Evaluable[count];
      var fwdFxRates = new Evaluable[count];
      for (int i = 0; i < count; ++i)
      {
        var dt = exposureDates[i];
        if (dt >= fxRate.Maturity)
        {
          numeraires[i] = 1.0;
          fwdFxRates[i] = Evaluable.SpotRate(spot);
          continue;
        }
        numeraires[i] = Evaluable.Interpolate(discountCurve, fxRate.Maturity)
          /Evaluable.Interpolate(discountCurve, dt);
        fwdFxRates[i] = Evaluable.FxRate(fxRate.ForwardFxCurve,
          fxRate.Maturity, spot.FromCcy, spot.ToCcy);
      }
      Evaluable.RecordCommonExpressions(numeraires);
      Evaluable.RecordCommonExpressions(fwdFxRates);

      return (exposureIndex, exposureDate, forward) =>
      {
        forward.Set(
          fwdFxRates[exposureIndex].Evaluate(),
          numeraires[exposureIndex].Evaluate());
      };
    }

    #endregion
  }
}
