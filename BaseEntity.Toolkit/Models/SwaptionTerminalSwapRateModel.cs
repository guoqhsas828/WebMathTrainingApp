using System;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Integrals;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Linear terminal swap rate model for swaption
  /// </summary>
  /// 
  ///   <remarks>
  /// Consider a swap settleing at <m>T_0</m> and ending at <m>T_n</m>, the swap 
  /// rate calculated at time <m>t(\le T_0)</m> is
  /// <math> \tag {1}
  /// S_t = \frac{V_{flt}(t)}{A(t)}     
  /// </math>
  /// 
  /// where <m>V_{flt}</m> is the pv of the floating leg and the annunity calculated
  /// at time <m>t(\le T_0)</m> is
  /// 
  /// <math> \tag {2}
  /// A(t) = \sum_{i=1}^n \delta_i P(t, T_i)     
  /// </math>
  /// 
  /// The physical-settled swaption with strike <m>K</m> at time <m>t</m> is:
  /// 
  /// <math> \tag {3}
  /// V_{pss}^{pay} = A(t)*E^A[(S_{T_0}-K)^+]
  /// </math>
  /// 
  /// For the cash-settled swpation the annunity is different, which is:
  /// 
  /// <math> \tag {4}
  /// G(s) =
  /// \frac{1}{m}*\sum_{i=1}^n(1+\frac{s}{m})^{-i}*n_i 
  /// </math>
  /// where <m>n_i</m> is the principle at the period <m>i</m>
  /// 
  /// and <m>m</m> is the number of payment per year.
  /// 
  /// 
  /// The value of cash-settled swaption at time <m>T_0</m> is given:
  /// 
  /// <math>\tag {5}
  /// V_{css}^{pay}(T_0) = E^Q[(S_{T_0}-K)^+*G(S_{T_0})]
  /// </math>
  /// 
  /// then its value at time <m>t(\le T_0)</m> is:
  /// 
  /// <math>\tag {6}
  /// V_{css}^{pay}(t) = A(t)*E^A[\frac{G(S_{T_0})(S_{T_0}-K)^+}{A(T_0)}]
  /// </math>
  /// 
  /// Market standard model \\
  /// 
  /// in the market standard practice, people freeze the ratio <m>\frac{G(S_{T_0})}{A(T_0)}</m>
  /// at t's level, that is,
  /// 
  /// <math> \tag {7}
  /// V_{css}^{pay}(t) = G(S_t)*E^A[S_{T_0}-K)^+]
  /// </math>
  /// 
  /// </remarks>

  public class SwaptionTerminalSwapRateModel
  {
    #region LinearTSR

    /// <summary>
    /// Calculate swaption value with terminal swap rate model.
    /// </summary>
    /// 
    /// <remarks>
    /// 
    /// Linear terminal swap rate model replaces the part <m>1/A(T_0)</m> in the 
    /// cash-settled value formula by the function <m>\alpha(t, S(T_0))</m> which is
    /// dependent on swap rate, that is,
    /// 
    /// <math> \tag {8}
    /// V_{css}^{pay}(t) = A(t)*E^A[\alpha(t, S_{T_0})*G(S_{T_0})*S_{T_0}-K)^+]
    /// </math>
    /// 
    /// where
    /// 
    /// <math> \tag {8}
    /// \alpha(t, S(T_0))= \alpha_0(t)*S_{T_0} +\alpha_1
    /// </math>
    /// 
    /// and 
    /// 
    /// <math> \tag {9}
    /// \alpha_0 = \frac{1}{S_t}(\frac{P(t, T_0)}{A(t)}-\alpha_1), \alpha_1 = \frac{1}{\sum_{i=1}^n\delta_i}
    /// </math>
    /// 
    /// We will use quadrature method directly integrate the formula (8). 
    /// Here we should assume that forward swap rate <m>S</m> follows log-normal 
    /// distribution with known initial value at <m>T_0</m>.
    /// </remarks>
    /// 
    /// <param name="asOf">asof date</param>
    /// <param name="settle">settle date</param>
    /// <param name="swpn">swaptin product</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="referenceCurve">reference curve</param>
    /// <param name="volatilityObject">volatility object</param>
    /// <param name="rateResets">rate reset</param>
    /// <returns></returns>
    internal static double LinearTsrPv(Swaption swpn, Dt asOf, Dt settle, 
      DiscountCurve discountCurve, DiscountCurve referenceCurve,
      IVolatilityObject volatilityObject, RateResets rateResets)
    {
      GetPhysicalSet(swpn, asOf, settle, discountCurve, referenceCurve,
        rateResets, out var unitFixedCfa, out var pAnnunity0,
        out var pSwapRate0, out var pStrike0);

      //calculate the alpha1 and alpha2
      CalculateAlphas(settle, unitFixedCfa, discountCurve, pSwapRate0,
        pAnnunity0, out var alph1, out var alpha2);

      var sigma = GetVolatility(asOf, settle, swpn, discountCurve, referenceCurve,
        rateResets, volatilityObject);

      var factorFn = GetFactorFn(alph1, alpha2,
        (int)swpn.UnderlyingFixedLeg.Freq, unitFixedCfa);

      double integral;
      var volType = volatilityObject.DistributionType;
      if (volType == DistributionType.LogNormal)
      {
        integral = (swpn.OptionType == OptionType.Call
          ? RightHalfIntegral(pSwapRate0, pStrike0, sigma,
            x => factorFn(x) * (x - pStrike0))
          : LeftHalfIntegral(pSwapRate0,
            pStrike0, sigma, x => factorFn(x) * (pStrike0 - x)));
      }
      else if (volType == DistributionType.Normal)
      {
        integral = (swpn.OptionType == OptionType.Call)
          ? HalfOpenIntegration.NormalRightIntegral(pSwapRate0, pStrike0,
            sigma, x => factorFn(x)*(x - pStrike0))
          : HalfOpenIntegration.NormalLeftIntegral(pSwapRate0, pStrike0,
            sigma, x => factorFn(x)*(pStrike0 - x));
      }
      else
      {
        throw new ToolkitException("DistributionType {0} not supported!", volType);
      }

      return pAnnunity0*integral;
    }

    internal static void GetPhysicalSet(Swaption swpn, Dt asOf, Dt settle,
      DiscountCurve discountCurve, DiscountCurve referenceCurve,
      RateResets rateResets, out CashflowAdapter unitFixedCfa,
      out double pAnnunity0, out double pSwapRate0, out double pStrike0)
    {
      var fixedLeg = swpn.UnderlyingFixedLeg;
      RateVolatilityUtil.GenerateCashflowsFromPaymentSchedules(fixedLeg,
        swpn.UnderlyingFloatLeg, asOf, settle, discountCurve,
        referenceCurve, rateResets, true, out unitFixedCfa,
        out var fixedCfa, out var unitFloatCfa, out var floatCfa);

      //The settlement type should be physical
      RateVolatilityUtil.CalculateLevelRateStrike(settle, swpn.Maturity,
        unitFixedCfa, fixedCfa, unitFloatCfa, floatCfa, discountCurve,
        fixedLeg.Freq, out pAnnunity0, out pSwapRate0, out pStrike0);
    }

    private static double LeftHalfIntegral(double s, double k,
      double sigma, Func<double, double> fn)
    {
      if (sigma.AlmostEquals(0.0))
        return k - s;
      var d = sigma / 2 + Math.Log(k / s) / sigma;
      var c = s * Math.Exp(-sigma * sigma / 2);
      var quad = new LogNormal(d, sigma);
      return quad.LeftIntegral(x => fn(c * x));
    }

    private static double RightHalfIntegral(
      double s, double k, double sigma,
      Func<double, double> fn)
    {
      if (sigma.AlmostEquals(0.0))
        return s - k;
      var d = sigma / 2 + Math.Log(k / s) / sigma;
      var c = s * Math.Exp(-sigma * sigma / 2);
      var quad = new LogNormal(d, sigma);
      return quad.RightIntegral(x => fn(c * x));
    }

    private static double GetVolatility(Dt asOf, Dt settle, Swaption swpn,
      DiscountCurve discountCurve, DiscountCurve referenceCurve,
      RateResets rateResets, IVolatilityObject volatilityObject)
    {
      var pricer = new SwaptionBlackPricer(swpn, asOf, settle,
        discountCurve, referenceCurve, rateResets, volatilityObject);
      return pricer.Volatility;
    }

    #endregion LinearTSR

    #region Helpers

    internal static Func<double, double> GetFactorFn(double alpha1,
      double alpha2, int m, CashflowAdapter cfa)
    {
      return x => (alpha1 * x + alpha2) * CashSettledAnnunity(cfa, m, x);
    }

    /// <summary>
    /// Calculate cash-settle swaption annunity
    /// </summary>
    /// 
    /// <remarks>
    /// 
    /// <math> \tag {4}
    /// G(s) =
    /// \frac{1}{m}*\sum_{i=1}^n(1+\frac{s}{m})^{-i}*n_i 
    /// </math>
    /// where <m>n_i</m> is the principle at the period <m>i</m>
    /// 
    /// </remarks>
    /// 
    /// <param name="cfa">cash flow adapter</param>
    /// <param name="freq">payment frequency per year</param>
    /// <param name="rate">swap rate</param>
    /// <returns></returns>
    internal static double CashSettledAnnunity(CashflowAdapter cfa,
      int freq, double rate)
    {
      Debug.Assert(freq > 0);
      if (cfa == null || cfa.Count == 0)
        return 0.0;

      int n = cfa.Count;
      var a = 1.0 + 1.0 * rate / freq;
      var b = 1.0 / freq;
      var csa = 0.0;
      for (int i = 1; i <= n; ++i)
      {
        csa += b * Math.Pow(1.0 / a, i) * cfa.GetPrincipalAt(i - 1);
      }

      return csa;
    }

    /// <summary>
    /// Calculate alpha1 and alpha2
    /// </summary>
    /// 
    /// <remarks>
    /// <math> 
    /// \alpha_0 = \frac{1}{S_t}(\frac{P(t, T_0)}{A(t)}-\alpha_1), \\
    /// \alpha_1 = \frac{1}{\sum_{i=1}^n\delta_i}
    /// </math>
    /// 
    /// where <m>P(t, T_0)</m> is the discount factor from <m>t</m> to <m>T_0</m>
    /// </remarks>
    /// <param name="settle">settle date</param>
    /// <param name="cfa">Cashflow Adapter</param>
    /// <param name="dc">discount curve</param>
    /// <param name="pFwdRate0">physical-settle forward rate at time 0</param>
    /// <param name="pAnnunity0">physical-settle annunity at time 0</param>
    /// <param name="alpha1">alpha1</param>
    /// <param name="alpha2">alpha2</param>
    internal static void CalculateAlphas(Dt settle,
      CashflowAdapter cfa, DiscountCurve dc,
      double pFwdRate0, double pAnnunity0,
      out double alpha1, out double alpha2)
    {
      alpha1 = alpha2 = 0.0;
      int n = cfa.Count;
      for (int i = 0; i < n; ++i)
      {
        alpha2 += cfa.GetPeriodFraction(i);
      }

      alpha2 = 1.0 / alpha2;
      alpha1 = (1 / pFwdRate0) * (dc.Interpolate(settle) / pAnnunity0 - alpha2);
    }

    #endregion Helpers
  }
}
