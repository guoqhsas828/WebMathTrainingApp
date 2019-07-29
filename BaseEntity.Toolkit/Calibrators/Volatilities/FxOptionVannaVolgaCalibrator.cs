/*
 * FxOptionVannaVolgaCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// info holder class for volatility skew information
  /// </summary>
  [Serializable]
  public class VolatilitySkewHolder : PlainVolatilityTenor, IVolatilityLevelHolder
  {
    #region Data
    // The delta for this tenor.
    private double delta_;
    #endregion

    /// <summary>
    /// constructor for the volatility Skew holder class
    /// </summary>
    /// <param name="name">The tenor name.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="delta">The delta.</param>
    /// <param name="sigAtm">The sig atm.</param>
    /// <param name="sigRR">The sig RR.</param>
    /// <param name="sigBF">The sig BF.</param>
    /// <remarks></remarks>
    public VolatilitySkewHolder(
      string name, Dt maturity, double delta,
      double sigAtm, double sigRR, double sigBF)
      : base(name, maturity)
    {
      delta_ = (!(delta > 0)) ? 0.25 : delta;
      Volatilities = new[] { sigAtm, sigRR, sigBF };
    }

    /// <summary>
    ///  The delta size
    /// </summary>
    public double Delta
    {
      get { return delta_; }
    }

    /// <summary>
    /// ATM Volatility
    /// </summary>
    public double AtmVol
    {
      get { return QuoteValues[0]; }
    }

    /// <summary>
    /// Gets or sets the volatility level (which is the ATM level for ATM-RR-BF quotes).
    /// </summary>
    /// <value>The level.</value>
    double IVolatilityLevelHolder.Level
    {
      get { return QuoteValues[0]; }
      set { QuoteValues[0] = value; }
    }

    /// <summary>
    /// Risk Reversal Volatility
    /// </summary>
    public double RiskReversalVol
    {
      get { return QuoteValues[1]; }
    }

    /// <summary>
    /// ButterFly Volatility
    /// </summary>
    public double ButterFlyVol
    {
      get { return QuoteValues[2]; }
    }
  }

  /// <summary>
  /// Calibrator that calibrates the Fx option Vols based on the Vanna-Volga methods
  /// </summary>
  [Serializable]
  public class FxOptionVannaVolgaCalibrator : FxVolatilityCalibrator
  {
    #region Constructors

    /// <summary>
    /// constructor for the FX option calibrator class
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="domesticRateCurve">The domestic rate curve.</param>
    /// <param name="foreignRateCurve">The foreign rate curve.</param>
    /// <param name="fxCurve">The FX curve.</param>
    /// <param name="volatilityCurveInterp">The volatility curve interpolation method (for ATM, Put and Call).</param>
    /// <param name="underlying">The terms of the underlying FX opions</param>
    /// <remarks></remarks>
    public FxOptionVannaVolgaCalibrator(
      Dt asOf, Dt settle,
      DiscountCurve domesticRateCurve,
      DiscountCurve foreignRateCurve,
      FxCurve fxCurve,
      Interp[] volatilityCurveInterp,
      FxVolatilityUnderlying underlying = null)
      : base(asOf, domesticRateCurve, foreignRateCurve, fxCurve)
    {
      settle_ = settle;
      volCurveInterp_ = new Interp[3];
      if (volatilityCurveInterp == null || volatilityCurveInterp.Length == 0)
      {
        volCurveInterp_[0] = volCurveInterp_[1] = volCurveInterp_[2]
          = new Linear(new Const(), new Const());
      }
      else if (volatilityCurveInterp.Length == 1)
      {
        volCurveInterp_[0] = volCurveInterp_[1] = volCurveInterp_[2]
          = volatilityCurveInterp[0];
      }
      else if (volatilityCurveInterp.Length == 2)
      {
        volCurveInterp_[1] = volatilityCurveInterp[0];//ATM volatilitiess
        volCurveInterp_[0] = volCurveInterp_[2] = volatilityCurveInterp[1];
      }
      else
      {
        volCurveInterp_[1] = volatilityCurveInterp[0];//ATM volatilitiess
        volCurveInterp_[0] = volatilityCurveInterp[1];
        volCurveInterp_[2] = volatilityCurveInterp[2];
      }
      _underlying = underlying;
    }

    /// <summary>
    /// Fit method to be overridden in the inheritors
    /// </summary>
    /// <param name="surface"></param>
    /// <param name="idx"></param>
    public override void FitFrom(CalibratedVolatilitySurface surface, int idx)
    {
      double settleDf = DomesticRateCurve.DiscountFactor(AsOf, Settle);
      FxCurveSet fxCurve = GetFxCurveSet();
      double spotFx = fxCurve.FxRate.Rate;
      anchorVolsCurves_ = new Curve[3];
      anchorStrikesCurves_ = new Curve[3];
      for (int i = 0; i < 3; i++)
      {
        anchorVolsCurves_[i] = new Curve(AsOf) {Interp = volCurveInterp_[i]};
        anchorStrikesCurves_[i] = new Curve(AsOf) { Interp = volCurveInterp_[i] };
      }
      var volatilitySkew = GetTenors(surface.Tenors);
      for (int i = 0; i < volatilitySkew.Length; i++)
      {
        var date = volatilitySkew[i].Maturity;
        var input = fxCurve.GetRatesSigmaTime(AsOf, date, null);
        double T = input.T, sqrT = Math.Sqrt(T), rd = input.Rd, rf = input.Rf;

        double pf = IsForwardDelta(T) ? Math.Exp(-rf * T) : 1.0;
        double delta = volatilitySkew[i].Delta;
        if (IsForwardDelta(T)) delta *= pf;

        var sigatm = volatilitySkew[i].AtmVol;
        var rr = volatilitySkew[i].RiskReversalVol;
        var bf = volatilitySkew[i].ButterFlyVol;
        if (IsOneVolButterfly(T))
        {
          bf = VannaVolgaCalculator.CalculateMarketConsistentBf(
            IsPremiumIncludedDelta(T), delta, T, spotFx, rd, rf, sigatm, rr, bf);
        }
        var sig25C = sigatm + bf + 0.5*rr;
        var sig25P = sigatm + bf - 0.5*rr;

        double fwd = fxCurve.ForwardFxRate(date);
        double kAtm, kc, kp;
        if (SmileConsistent)
        {
          if (IsPremiumIncludedDelta(T))
          {
            // The ATM strike
            kAtm = IsForwardAtm(T)
              ?spotFx * Math.Exp((rd - rf)*T)
              :spotFx * Math.Exp((rd - rf - 0.5 * sigatm * sigatm) * T);
            // 25D put strike
            kp = VannaVolgaCalculator.CalculatePremiumIncludedStrike(
              OptionType.Put, T, spotFx, rd, rf, sig25P, delta);
            // 25D call strike
            kc = VannaVolgaCalculator.CalculatePremiumIncludedStrike(
              OptionType.Call, T, spotFx, rd, rf, sig25C, delta);
          }
          else
          {
            // The ATM strike
            kAtm = IsForwardAtm(T)
              ? fwd
              : fwd * Math.Exp(0.5 * sigatm * sigatm * input.T);
            var alpha = -QMath.NormalInverseCdf(delta * Math.Exp(input.Rf * T));
            kp = fwd * Math.Exp((-alpha * sqrT + 0.5 * sig25P * T) * sig25P);
            kc = fwd * Math.Exp((alpha * sqrT + 0.5 * sig25C * T) * sig25C);
          }
        }
        else
        {
          kAtm = fwd;
          kc = VannaVolgaCalibrator.SolveDelta(spotFx,
            input.T, input.Rf, input.Rd, sig25C, OptionType.Call, delta*settleDf);
          kp = VannaVolgaCalibrator.SolveDelta(spotFx,
            input.T, input.Rf, input.Rd, sig25P, OptionType.Put, -delta*settleDf);
        }

        if (Math.Abs(delta - DefaultDelta * pf) > 1E-4)
        {
          VannaVolgaCalculator.Solve25Delta(false, pf,
            spotFx, input.T, input.Rd, input.Rf,
            sig25P, sigatm, sig25C, kp, kAtm, kc,
            out sig25P, out sig25C, out kp, out kc);
        }

        anchorVolsCurves_[0].Add(date, sig25P);
        anchorVolsCurves_[1].Add(date, sigatm);
        anchorVolsCurves_[2].Add(date, sig25C);

        anchorStrikesCurves_[0].Add(date, kp);
        anchorStrikesCurves_[1].Add(date, kAtm);
        anchorStrikesCurves_[2].Add(date, kc);
      }
    }

    private static VolatilitySkewHolder[] GetTenors(IList<IVolatilityTenor> tenors)
    {
      var holders = tenors as VolatilitySkewHolder[];
      if(holders!=null) return holders;

      return tenors.Select(tenor =>
      {
        var holder = tenor as VolatilitySkewHolder;
        if (holder != null) return holder;
        var rb = tenor as FxRrBfVolatilityTenor;
        if (rb == null)
        {
          throw new ToolkitException(String.Format(
            "Expect FxRrBfVolatilityTenor, not {0}",
            tenor.GetType().Name));
        }
        int k = 0;
        double min = Double.MaxValue;
        for (int i = 0, n = rb.Deltas.Length;i<n;++i)
        {
          var e = Math.Abs(rb.Deltas[i] - 0.25);
          if(e < min) k = i;
        }
        return new VolatilitySkewHolder(rb.Name, rb.Maturity, rb.Deltas[k],
          rb.AtmQuote, rb.RiskReversalQuotes[k], rb.ButterflyQuotes[k]);
      }).ToArray();
    }

    /// <summary>
    /// Interpolates the specified maturity.
    /// </summary>
    /// <param name="surface">The volatility surface</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="strike">The strike.</param>
    /// <returns></returns>
    public override double Interpolate(VolatilitySurface surface, Dt maturity, double strike)
    {
      var fxCurve = GetFxCurveSet();
      var spotFx = fxCurve.FxRate.Rate;
      var input = fxCurve.GetRatesSigmaTime(AsOf, maturity, null);

      var anchorVols = new double[3];
      for (int i = 0; i < 3; i++)
      {
        anchorVols[i] = anchorVolsCurves_[i].Interpolate(maturity);
      }

      var anchorStrikes = new double[3];
      if (SmileConsistent)
      {
        SmileConsistentStrikes(anchorVols, input, spotFx, anchorStrikes);
      }
      else
      {
        for (int i = 0; i < 3; i++)
        {
          anchorStrikes[i] = anchorStrikesCurves_[i].Interpolate(maturity);
        }
      }
      return VannaVolgaCalibrator.ImpliedVolatility(spotFx, input.T, strike,
        input.Rf, input.Rd, anchorVols, anchorStrikes, UseAsymtoticModel);
    }

    private void SmileConsistentStrikes(double[] anchorVols,
      FxCurveSet.RatesSigmaTime input, double spotFx, double[] anchorStrikes)
    {
      double rd = input.Rd, rf = input.Rf, T = input.T,
        sig25p = anchorVols[0], sigatm = anchorVols[1], sig25c=anchorVols[2];

      double vvdelta = DefaultDelta;
      if (IsForwardDelta(T)) vvdelta *= Math.Exp(-rf * T);

      if (IsPremiumIncludedDelta(T))
      {
        // The ATM strike
        anchorStrikes[1] = IsForwardAtm(T)
          ? spotFx*Math.Exp((rd - rf)*T)
          : spotFx*Math.Exp((rd - rf - 0.5*sigatm*sigatm)*T);
        // 25D put strike
        anchorStrikes[0] = VannaVolgaCalculator.CalculatePremiumIncludedStrike(
          OptionType.Put, T, spotFx, rd, rf, sig25p, vvdelta);
        // 25D call strike
        anchorStrikes[2] = VannaVolgaCalculator.CalculatePremiumIncludedStrike(
          OptionType.Call, T, spotFx, rd, rf, sig25c, vvdelta);

        // Check the strikes are correct.
        VannaVolgaCalculator.CheckVannaVolgaStrikesPi(
          IsForwardAtm(T), vvdelta, spotFx, T, rd, rf,
          anchorVols[0], anchorVols[1], anchorVols[2],
          anchorStrikes[0], anchorStrikes[1], anchorStrikes[2]);
        return;
      }

      // This is the ATM strike
      anchorStrikes[1] = IsForwardAtm(T)
        ? spotFx*Math.Exp((rd - rf)*T)
        : spotFx*Math.Exp((rd - rf + 0.5*sigatm*sigatm)*T);

      double alpha = -QMath.NormalInverseCdf(vvdelta*Math.Exp(rf*T));
      double sqrtT = Math.Sqrt(T);

      // 25D put strike
      anchorStrikes[0] = spotFx*Math.Exp(-alpha*sig25p*sqrtT + (rd - rf + 0.5*sig25p*sig25p)*T);
      // 25D call strike
      anchorStrikes[2] = spotFx*Math.Exp(alpha*sig25c*sqrtT + (rd - rf + 0.5*sig25c*sig25c)*T);

      // Check the strikes are correct.
      VannaVolgaCalculator.CheckVannaVolgaStrikes(
        IsForwardAtm(T), vvdelta, spotFx, T, rd, rf,
        anchorVols[0], anchorVols[1], anchorVols[2],
        anchorStrikes[0], anchorStrikes[1], anchorStrikes[2]);
    }

    /// <summary>
    ///  Create a Curve set to encapsulate the rate calculations.
    /// </summary>
    /// <returns></returns>
    private FxCurveSet GetFxCurveSet()
    {
      // The real calculation always starts with the as-Of date,
      // which is the date the volatility starts.
      // All the values are converted to settle date only
      // at the input/output port.
      return new FxCurveSet(AsOf, FxCurve.SpotFxRate,
                            FxCurve.WithDiscountCurve ? null : FxCurve,
                            DomesticRateCurve,
                            ForeignRateCurve,
                            FxCurve.BasisCurve,
                            //TOTO: backward compatible only, need to revisit
                            FxCurve.SpotDays == 0);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="marketApproach"></param>
    /// <param name="surface"></param>
    /// <param name="maturity"></param>
    /// <returns></returns>
    /// <exclude/>
    public VannaVolgaCoefficientsCollection OverHedgeCoefs(
      bool marketApproach, CalibratedVolatilitySurface surface, Dt maturity)
    {
      VannaVolgaCoefficientsCollection coefs;
#if EnableCache
      var cache = marketApproach ? _vvcoefsMarket : _vvcoefs;
      if (cache.TryGetValue(maturity, out coefs))
      {
        return coefs;
      }
#endif

      var fxCurve = GetFxCurveSet();
      var spotFx = fxCurve.FxRate.Rate;
      var input = fxCurve.GetRatesSigmaTime(AsOf, maturity, null);
      var anchorVols = new double[3];
      for (int i = 0; i < 3; i++)
      {
        anchorVols[i] = anchorVolsCurves_[i].Interpolate(maturity);
      }
      var anchorStrikes = new double[3];
      if (SmileConsistent)
      {
        SmileConsistentStrikes(anchorVols, input, spotFx, anchorStrikes);
      }
      else
      {
        for (int i = 0; i < 3; i++)
        {
          anchorStrikes[i] = anchorStrikesCurves_[i].Interpolate(maturity);
        }
      }
      coefs = VannaVolgaCalculator.GetCoefficients(
        marketApproach, spotFx, input.T, input.Rd, input.Rf,
        anchorVols[0], anchorVols[1], anchorVols[2],
        anchorStrikes[0], anchorStrikes[1], anchorStrikes[2]);
#if EnableCache
      cache[maturity] = coefs;
#endif
      return coefs;
    }
    #endregion

    #region Quote terms helpers

    //TODO: Handle delta neutral ATM
    private bool IsForwardAtm(double time)
    {
      if (_underlying == null)
        return time > fwdAsAtmAfterYears_;
      return _underlying.QuoteTerm.GetAtmKind(GetTenor(time)) == AtmKind.Forward;
    }

    private bool IsForwardDelta(double time)
    {
      if (_underlying == null)
        return time > fwdDeltaAfterYears_;
      return (_underlying.QuoteTerm.GetDeltaStyle(GetTenor(time))
        & DeltaStyle.Forward) != 0;
    }

    private bool IsOneVolButterfly(double time)
    {
      if (_underlying == null)
        return (flags_ & MarketConsistentFlag) != 0; ;
      return (_underlying.QuoteTerm.GetFlags(GetTenor(time))
        & FxVolatilityQuoteFlags.OneVolatilityBufferfly) != 0;
    }

    private bool IsPremiumIncludedDelta(double time)
    {
      if (_underlying == null)
        return (flags_ & PremiumIncludedFlag) != 0; ;
      return (_underlying.QuoteTerm.GetDeltaStyle(GetTenor(time))
        & DeltaStyle.PremiumIncluded) != 0;
    }

    private static Tenor GetTenor(double time)
    {
      Debug.Assert(time >= 0);
      return TenorIndexedValues.DaysToTenor((int) (time*360 + 2));
    }
    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets a value indicating whether [use asymtotic model].
    /// </summary>
    /// <value><c>true</c> if [use asymtotic model]; otherwise, <c>false</c>.</value>
    public bool UseAsymtoticModel
    {
      get { return (flags_ & AsymtoticModelFlag) != 0; }
      set { flags_ = flags_.SetBitIf(value, AsymtoticModelFlag); }
    }

    /// <summary>
    /// Gets the anchor strikes curves.
    /// </summary>
    /// <value>The anchor strikes curves.</value>
    public Curve[] AnchorStrikesCurves
    {
      get { return anchorStrikesCurves_; }
    }

    /// <summary>
    /// Gets the anchor volatility curves.
    /// </summary>
    /// <value>The anchor volatility curves.</value>
    public Curve[] AnchorVolatilityCurves
    {
      get { return anchorVolsCurves_; }
    }

    /// <summary>
    /// Gets the settle date.
    /// </summary>
    /// <value>The settle date.</value>
    public Dt Settle
    {
      get { return settle_; }
    }

    /// <summary>
    ///   Smile consistent.
    /// </summary>
    public bool SmileConsistent
    {
      get { return (flags_ & SmileConsistentFlag) != 0; }
      set { flags_ = flags_.SetBitIf(value, SmileConsistentFlag); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the delta is premium included.
    /// </summary>
    public bool PremiumIncludedDelta
    {
      get { return (flags_ & PremiumIncludedFlag) != 0; }
      set { flags_ = flags_.SetBitIf(value, PremiumIncludedFlag); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the delta is premium included.
    /// </summary>
    public bool AdjustMarketButterfly
    {
      get { return (flags_ & MarketConsistentFlag) != 0; }
      set { flags_ = flags_.SetBitIf(value, MarketConsistentFlag); }
    }

    /// <summary>
    /// Gets or sets the years to use forward as ATM strike.
    /// </summary>
    /// <value>The years to use forward as ATM strike.</value>
    /// <remarks></remarks>
    public double YearsToUseForwardAsAtmStrike
    {
      get { return fwdAsAtmAfterYears_; }
      set { fwdAsAtmAfterYears_ = value; }
    }

    /// <summary>
    /// Gets or sets the years to use forward as ATM strike.
    /// </summary>
    /// <value>The years to use forward as ATM strike.</value>
    /// <remarks></remarks>
    public double YearsToUseForwardDelta
    {
      get { return fwdDeltaAfterYears_; }
      set { fwdDeltaAfterYears_ = value; }
    }
    #endregion

    #region data

    private const double DefaultDelta = 0.25;

    private readonly Dt settle_;
    private readonly Interp[] volCurveInterp_;

    private double fwdAsAtmAfterYears_ = 100;//TODO: change this to 1.99
    private double fwdDeltaAfterYears_ = 100;//TODO: change this to 1.99
    private Curve[] anchorStrikesCurves_;
    private Curve[] anchorVolsCurves_;
    private uint flags_ = AsymtoticModelFlag;
    private const uint AsymtoticModelFlag = 1;
    private const uint SmileConsistentFlag = 2;
    private const uint PremiumIncludedFlag = 4;
    private const uint MarketConsistentFlag = 8;
    private Dictionary<Dt, VannaVolgaCoefficientsCollection> _vvcoefs
      = new Dictionary<Dt, VannaVolgaCoefficientsCollection>();
    private Dictionary<Dt, VannaVolgaCoefficientsCollection> _vvcoefsMarket
      = new Dictionary<Dt, VannaVolgaCoefficientsCollection>();

    private readonly FxVolatilityUnderlying _underlying;
    #endregion
  }
}