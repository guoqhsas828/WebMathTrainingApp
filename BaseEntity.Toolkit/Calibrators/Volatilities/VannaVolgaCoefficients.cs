using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Util;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Coefficients of vega, vanna and volga.
  /// </summary>
  [Serializable]
  public class VannaVolgaCoefficients
  {
    /// <summary>
    ///   Coefficients of vega.
    /// </summary>
    public double Vega { get; set; }
    /// <summary>
    /// Coefficients of volga.
    /// </summary>
    public double Volga { get; set; }
    /// <summary>
    /// Coefficients of vanna.
    /// </summary>
    public double Vanna { get; set; }
  }

  /// <summary>
  ///   A collection of Vanna-Volga coefficients for price and greek calculations.
  /// </summary>
  [Serializable]
  public class VannaVolgaCoefficientsCollection
  {
    private VannaVolgaCoefficients[] data_ = new VannaVolgaCoefficients[
      Enum.GetNames(typeof (VannaVolgaTarget)).Length];
    private Func<VannaVolgaTarget,VannaVolgaCoefficients> calculator_;

    internal VannaVolgaCoefficientsCollection(
      Func<VannaVolgaTarget, VannaVolgaCoefficients> calculator)
    {
      if (calculator == null)
      {
        throw new ToolkitException("VannaVolgaCalculator cannot be null.");
      }
      calculator_ = calculator;
    }

    /// <summary>
    /// Gets the <see cref="VannaVolgaCoefficients"/> with the specified target.
    /// </summary>
    /// <remarks></remarks>
    public VannaVolgaCoefficients this[VannaVolgaTarget target]
    {
      get
      {
        int idx = (int) target;
        if(idx < 0||idx >= data_.Length)
          throw new ToolkitException("Invalid VannaVolgaTarget {0}", target);
        var coefs = data_[idx];
        if (coefs == null)
          coefs = data_[idx] = calculator_(target);
        return coefs;
      }
    }
  }

  /// <summary>
  ///  Type of values (price or a specific greek) to be computed by Vanna-Volga approach.
  /// </summary>
  /// <remarks></remarks>
  public enum VannaVolgaTarget
  {
    /// <summary>Price</summary>
    Price,
    /// <summary>Delta</summary>
    Delta,
    /// <summary>Gamma</summary>
    Gamma,
    /// <summary>Vega</summary>
    Vega,
    /// <summary>Volga</summary>
    Volga,
    /// <summary>Vanna</summary>
    Vanna,
    /// <summary>Rho</summary>
    Rho,
    /// <summary>Theta</summary>
    Theta,
  }

  internal static class VannaVolgaCalculator
  {
    public static VannaVolgaCoefficientsCollection GetCoefficients(
      bool marketApproach,
      double S, double T, double rd, double rf,
      double sig25p, double sigatm, double sig25c,
      double k25p, double katm, double k25c)
    {
      return new VannaVolgaCoefficientsCollection((target) =>
        CalculateCoefficients(marketApproach, target,
          S, T, rd, rf, sig25p, sigatm, sig25c, k25p, katm, k25c));
    }

    public static double CalculateMarketConsistentBf(
      bool premiumIncluded, double delta,
      double T, double S, double rd, double rf,
      double sigatm, double rr, double bf)
    {
      double katm = S*Math.Exp((rd - rf)*T
        + (premiumIncluded ? 1 : -1)*0.5*sigatm*sigatm*T);
      double alpha = -Math.Sqrt(T)*QMath.NormalInverseCdf(delta*Math.Exp(rf*T));
      double sigb = sigatm + bf;
      var model = new Model(T, S, rd, rf);
      Func<double, double> fn = vwb =>
        {
          double sigp = sigatm + vwb - 0.5*rr;
          double kp = calculateStrike(OptionType.Put,
            premiumIncluded, delta, model, sigp, alpha);

          double sigc = sigatm + vwb + 0.5*rr;
          double kc = calculateStrike(OptionType.Call,
            premiumIncluded, delta, model, sigc, alpha);
          var beta = CalculateCoefficients(false, VannaVolgaTarget.Price,
            S, T, rd, rf, sigp, sigatm, sigc, kp, katm, kc);

          double kp1 = calculateStrike(OptionType.Put,
            premiumIncluded, delta, model, sigb, alpha);
          double p1 = model.Bs(OptionType.Put, kp1, sigb).Price;
          var bs = model.Bs(OptionType.Put, kp1, sigatm);
          double p = bs.Price + beta.Vega * bs.Vega +
            beta.Vanna * bs.Vanna + beta.Volga * bs.Volga;

          double kc1 = calculateStrike(OptionType.Call,
            premiumIncluded, delta, model, sigb, alpha);
          double c1 = model.Bs(OptionType.Call, kc1, sigb).Price;
          bs = model.Bs(OptionType.Call, kc1, sigatm);
          double c = bs.Price + beta.Vega * bs.Vega +
            beta.Vanna * bs.Vanna + beta.Volga * bs.Volga;

          double s = c1 + p1 - c - p;
          return s;
        };

      var solver = new Brent2();
      solver.setToleranceF(1E-13);
      solver.setToleranceX(1E-14);
      double x = solver.solve(fn, null, 0.0, bf);
      return x;
    }

    private static double calculateStrike(
      OptionType otype, bool premiumIncluded, double delta,
      Model model, double sigma, double alpha)
    {
      if (premiumIncluded)
      {
        return model.PremiumIncludedStrike(otype, delta, sigma);
      }
      double logFwd = model.LogForward;
      double T = model.Time;
      int w = otype == OptionType.Call ? 1 : -1;
      return Math.Exp(logFwd + (w*alpha + 0.5*sigma*T)*sigma);
    }

    internal static double CalculatePremiumIncludedStrike(
      OptionType otype, double T, double S, double rd, double rf,
      double sigma, double delta)
    {
      var model = new Model(T, S, rd, rf);
      return model.PremiumIncludedStrike(otype, delta, sigma);
    }

    public static void Solve25Delta(
      bool marketApproach, double pf,
      double S, double T, double rd, double rf,
      double sigp, double sigatm, double sigc,
      double kp, double katm, double kc,
      out double sig25p, out double sig25c,
      out double k25p, out double k25c)
    {
      var beta = new VannaVolgaCoefficientsCollection((target) =>
        CalculateCoefficients(marketApproach, target,
        S, T, rd, rf, sigp, sigatm, sigc,
        kp, katm, kc))[VannaVolgaTarget.Price];
      var model = new Model(T, S, rd, rf);
      model.SolveDelta(0.25*pf, OptionType.Call, beta, sigatm,
        out k25p, out sig25p);
      model.SolveDelta(0.25*pf, OptionType.Put, beta, sigatm,
        out k25c, out sig25c);
    }

    private class Model
    {
      private readonly double T_, S_, rd_, rf_;
      private readonly double sqrT_, logDrift_;

      public Model(double T, double S, double rd, double rf)
      {
        T_ = T;
        S_ = S;
        rd_ = rd;
        rf_ = rf;
        sqrT_ = Math.Sqrt(T);
        logDrift_ = Math.Log(S) + (rd - rf) * T;
      }

      public BlackScholesResult Bs(OptionType tp, double strike, double sigma)
      {
        return new BlackScholesResult(this, tp, strike, sigma);
      }

      internal double LogForward { get { return logDrift_; } }
      internal double Time { get { return T_; } }

      public double PremiumIncludedStrike(
        OptionType otype, double delta, double sigma)
      {
        int w = otype == OptionType.Call ? 1 : -1;
        double sig = sigma * Math.Sqrt(T_);
        double alpha = -QMath.NormalInverseCdf(delta * Math.Exp(rf_ * T_));
        double k0 = S_ * Math.Exp(w * alpha * sig + (rd_ - rf_) * T_ + 0.5 * sig * sig);
        var solver = new Brent2();
        solver.setToleranceF(1E-13);
        solver.setToleranceX(1E-14);
        solver.setLowerBounds(1E-16);
        double k1 = solver.solve((k) =>
        {
          double pd = Math.Exp(-rd_ * T_);
          double sk = S_ / k;
          double logDrift = Math.Log(sk) + (rd_ - rf_) * T_;
          double d1 = (logDrift + 0.5 * sig * sig) / sig;
          double d2 = d1 - sig;
          double di = pd * QMath.NormalCdf(w * d2) / sk;
          return di;
        }, null, delta, k0);
        return k1;
      }

      public bool SolveDelta(double target,
        OptionType tp,
        VannaVolgaCoefficients beta,
        double sigatm,
        out double strike, out double sigma)
      {
        int sign = tp == OptionType.Call ? 1 : -1;
        var alpha = -SpecialFunctions.NormalInverseCdf(
          target * Math.Exp(rf_ * T_));

        var solver = new Brent2();
        solver.setToleranceF(1E-9);
        solver.setToleranceX(1E-6);



        double solution = solver.solve(x =>
          {
            var k = Math.Exp(logDrift_ + (sign*alpha*sqrT_ + 0.5*x*T_)*x);
            var bs = Bs(tp, k, sigatm);
            var price = bs.Price + beta.Vega*bs.Vega + beta.Vanna*bs.Vanna + beta.Volga*bs.Volga;
            var sig = BlackScholes.ImpliedVolatility(OptionStyle.European, tp,
              T_, S_, k, rd_, rf_, price);
            bs = Bs(tp, k, sig);
            return sign*bs.Delta*100;
          }, null, target*100, sigatm*0.9, sigatm*1.1);
        sigma = solution;
        strike = Math.Exp(logDrift_ + (sign*alpha*sqrT_ + 0.5*sigma*T_)*sigma);
        return true;
      }

      public class BlackScholesResult
      {
        private readonly Model bs_;

        public readonly double Price, Delta, Gamma,Theta,
          Rho, Vega, Volga, Vanna, D1, D2;

        public double this[VannaVolgaTarget target]
        {
          get
          {
            switch (target)
            {
            case VannaVolgaTarget.Price:
              return Price;
            case VannaVolgaTarget.Delta:
              return Delta;
            case VannaVolgaTarget.Gamma:
              return Gamma;
            case VannaVolgaTarget.Rho:
              return Rho;
            case VannaVolgaTarget.Theta:
              return Theta;
            case VannaVolgaTarget.Vega:
              return Vega;
            case VannaVolgaTarget.Vanna:
              return Vanna;
            case VannaVolgaTarget.Volga:
              return Volga;
            default:
              break;
            }
            throw new ToolkitException("Unknown target");
          }
        }

        [Conditional("DEBUG")]
        private void CheckPrices(Model bs,
          OptionType tp, double strike, double sigma)
        {
          int w = tp == OptionType.Call ? 1 : -1;
          double pd = Math.Exp(-bs.rd_*bs.T_);
          double pf = Math.Exp(-bs.rf_*bs.T_);
          double fx = bs.S_*Math.Exp((bs.rd_ - bs.rf_)*bs.T_);
          double price = pd*w*(fx*QMath.NormalCdf(w*D1) - strike*QMath.NormalCdf(w*D2));
          if (Math.Abs(Price - price) / (1 + Math.Abs(price)) > 1E-13)
          {
            logger.Error(String.Format("Prices ({0} vs {1}) not match.",
              Price, price));
          }
          double vega = pf*bs.S_*bs.sqrT_*QMath.NormalPdf(D1);
          if (Math.Abs(Vega - vega)/(1+Math.Abs(vega)) > 1E-11)
          {
            logger.Error(String.Format("Vegas ({0} vs {1}) not match.",
              Vega, vega));
          }
          double volga = vega*D1*D2/sigma;
          if (Math.Abs(Volga - volga)/(1 + Math.Abs(volga)) > 1E-9)
          {
            logger.Error(String.Format("Volgas ({0} vs {1}) not match.",
              Volga, volga));
          }
          double vanna = -pf * QMath.NormalPdf(D1) * D2 / sigma;
          if (Math.Abs(Vanna - vanna) / (1 + Math.Abs(vanna)) > 1E-9)
          {
            logger.Error(String.Format("Vannas ({0} vs {1}) not match.",
              Vanna, vanna));
          }
        }

        public BlackScholesResult(Model bs, OptionType tp, double strike, double sigma)
        {
          bs_ = bs;
          double lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
            zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
          Price = BlackScholes.P(OptionStyle.European, tp, bs.T_, bs.S_, strike, bs.rd_, bs.rf_, sigma,
            ref Delta, ref Gamma, ref Theta, ref Vega, ref Rho, ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm,
            ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
          var logK = Math.Log(strike);
          double sig = sigma*bs.sqrT_;
          D1 = (bs_.logDrift_ - logK + 0.5*sig*sig)/sig;
          D2 = D1 - sig;
          Vanna = -Vega*D2/(bs_.S_*sig);
          Volga = Vega*D1*D2/sigma;
          CheckPrices(bs, tp, strike, sigma);
        }
      }
    }

    private static VannaVolgaCoefficients CalculateCoefficients(
      bool marketApproach, VannaVolgaTarget target,
      double S, double T, double rd, double rf,
      double sig25p, double sigatm, double sig25c,
      double k25p, double katm, double k25c)
    {
      // Special case: no skew or no volatility at time 0.
      if ((Math.Abs(sig25p - sigatm) < 1E-14 && Math.Abs(sig25c - sigatm) < 1E-14)
        || T < 1E-14)
      {
        return new VannaVolgaCoefficients { Vanna = 0, Vega = 0, Volga = 0 };
      }

      // Sanity checks
      if (S < 1E-12)
      {
        throw new ToolkitException(String.Format("Invalid vv Spot {0}", S));
      }
      if (sigatm < 1E-12 || sig25c < 1E-12 || sig25p < 1E-12)
      {
        throw new ToolkitException(String.Format(
          "Invalid vv volatilties {0}, {1}, {2}", sig25p, sigatm, sig25c));
      }
      if (katm - k25p < 1E-12 || k25c - katm < 1E-12 || k25p < 1E-12)
      {
        throw new ToolkitException(String.Format(
          "Invalid vv strikes {0}, {1}, {2}", k25p, katm, k25c));
      }
      var model = new Model(T, S, rd, rf);
      if (marketApproach)
      {
        var a25c = model.Bs(OptionType.Call, k25c, sigatm);
        var a25p = model.Bs(OptionType.Put, k25p, sigatm);
        var c25c = model.Bs(OptionType.Call, k25c, sig25c);
        var p25p = model.Bs(OptionType.Put, k25p, sig25p);

        var coefs = new VannaVolgaCoefficients();
        double rr = c25c.Vanna - p25p.Vanna;
        if (Math.Abs(rr) > 0)
        {
          coefs.Vanna = ((c25c[target] - p25p[target])
            - (a25c[target] - a25p[target])) / rr;
        }
        double bf = c25c.Volga + p25p.Volga;
        if (Math.Abs(bf) > 0)
        {
          coefs.Volga = ((c25c[target] + p25p[target])
            - (a25c[target] + a25p[target])) / bf;
        }
        return coefs;
      }

      double sigT = sigatm*Math.Sqrt(T);
      var mkt25p = model.Bs(OptionType.Call, k25p, sig25p);
      var mkt25c =model.Bs(OptionType.Call, k25c, sig25c);

      var callatm = model.Bs(OptionType.Call, katm, sigatm);
      var call25p = model.Bs(OptionType.Call, k25p, sigatm);
      var call25c = model.Bs(OptionType.Call, k25c, sigatm);

      double d11 = call25p.D1, d21 = call25p.D2;
      double d12 = callatm.D1, d22 = callatm.D2;
      double d13 = call25c.D1, d23 = call25c.D2;
      double z1 = (mkt25p[target] - call25p[target]) / call25p.Vega / (d21 - d22),
        z2 = 0,
        z3 = (mkt25c[target] - call25c[target]) / call25c.Vega / (d23 - d22);
      double beta2 = (z3 - z1) / (d23 - d21);
      double beta3 = z1 - (d21 + d22 + sigT) * beta2;
      double beta1 = z2 - d12 * d22 * beta2 - d22 * beta3;
      beta2 *= sigatm;
      beta3 *= -S * sigT;

      CheckEquations(target, S, T, rd, rf, sig25p, sigatm, sig25c,
        k25p, katm, k25c, beta1, beta2, beta3);

      return new VannaVolgaCoefficients
      {
        Vega = beta1,
        Volga = beta2,
        Vanna = beta3
      };
    }

    [Conditional("DEBUG")]
    private static void CheckEquations(VannaVolgaTarget target,
      double S, double T, double rd, double rf,
      double sig25p, double sigatm, double sig25c,
      double k25p, double katm, double k25c,
      double beta1, double beta2, double beta3)
    {
      var model = new Model(T, S, rd, rf);
      var a25c = model.Bs(OptionType.Call, k25c, sigatm);
      var a25p = model.Bs(OptionType.Put, k25p, sigatm);
      var catm = model.Bs(OptionType.Call, katm, sigatm);
      var patm = model.Bs(OptionType.Put, katm, sigatm);

      var c25c = model.Bs(OptionType.Call, k25c, sig25c);
      var p25p = model.Bs(OptionType.Put, k25p, sig25p);

      // Check that the BF equation holds.
      var bfmkt = 0.5 * (c25c[target] + p25p[target]) - 0.5 * (catm[target] + patm[target]);
      var bfatm = 0.5*(a25c[target] + a25p[target]) - 0.5*(catm[target] + patm[target]);
      double bfcost = bfmkt - bfatm;
      var bfvega = 0.5 * (a25c.Vega + a25p.Vega) - 0.5 * (catm.Vega + patm.Vega);
      var bfvolga = 0.5 * (a25c.Volga + a25p.Volga) - 0.5 * (catm.Volga + patm.Volga);
      var bfvanna = 0.5 * (a25c.Vanna + a25p.Vanna) - 0.5 * (catm.Vanna + patm.Vanna);
      double bfdelta = beta1 * bfvega + beta2 * bfvolga + beta3 * bfvanna;
      if (Math.Abs(bfdelta - bfcost) > 1E-13)
      {
        logger.Error(String.Format("BF delta {0} - cost {1} != 0",
          bfdelta, bfcost));
      }

      // Check that the RR equation holds.
      var rrmkt = c25c[target] - p25p[target];
      var rratm = a25c[target] - a25p[target];
      double rrcost = rrmkt - rratm;
      var rrvega = a25c.Vega - a25p.Vega;
      var rrvolga = a25c.Volga - a25p.Volga;
      var rrvanna = a25c.Vanna - a25p.Vanna;
      double rrdelta = beta1 * rrvega + beta2 * rrvolga + beta3 * rrvanna;
      if (Math.Abs(rrdelta - rrcost) > 1E-13)
      {
        logger.Error(String.Format("RR delta {0} - cost {1} != 0",
          rrdelta, rrcost));
      }

      // Check that the ATM equation holds.
      var atmvega = catm.Vega;
      var atmvolga = catm.Volga;
      var atmvanna = catm.Vanna;
      double atmdelta = beta1 * atmvega + beta2 * atmvolga + beta3 * atmvanna;
      if (Math.Abs(atmdelta) > 1E-13)
      {
        logger.Error(String.Format("ATM delta {0} != 0", atmdelta));
      }
    }

    [Conditional("DEBUG")]
    internal static void CheckVannaVolgaStrikes(
      bool atmIsForward, double targetDelta,
      double S, double T, double rd, double rf,
      double sig25p, double sigatm, double sig25c,
      double k25p, double katm, double k25c)
    {
      var model = new Model(T, S, rd, rf);
      if (!atmIsForward)
      {
        var catm = model.Bs(OptionType.Call, katm, sigatm);
        var patm = model.Bs(OptionType.Put, katm, sigatm);
        if (Math.Abs(catm.Delta + patm.Delta) > epsilon)
        {
          logger.Error(String.Format(
            "call delta ({0}) + put delta ({1}) != 0",
            catm.Delta, patm.Delta));
        }
      }

      var c25c = model.Bs(OptionType.Call, k25c, sig25c);
      if (Math.Abs(c25c.Delta - targetDelta) > epsilon)
      {
        logger.Error(String.Format("call delta ({0}) != {1}",
          c25c.Delta, targetDelta));
      }

      var p25p = model.Bs(OptionType.Put, k25p, sig25p);
      if(Math.Abs(p25p.Delta + targetDelta) > epsilon)
      {
        logger.Error(String.Format("put delta ({0}) != -{1}",
          p25p.Delta, targetDelta));
      }
    }

    [Conditional("DEBUG")]
    internal static void CheckVannaVolgaStrikesPi(
      bool atmIsForward, double targetDelta,
      double S, double T, double rd, double rf,
      double sig25p, double sigatm, double sig25c,
      double k25p, double katm, double k25c)
    {
      var model = new Model(T, S, rd, rf);
      double cDelta, pDelta;
      if (!atmIsForward)
      {
        var catm = model.Bs(OptionType.Call, katm, sigatm);
        cDelta = catm.Delta - catm.Price / S;
        var patm = model.Bs(OptionType.Put, katm, sigatm);
        pDelta = patm.Delta - patm.Price / S;
        if (Math.Abs(cDelta + pDelta) > epsilon)
        {
          logger.Error(String.Format(
            "call delta ({0}) + put delta ({1}) != 0",
            cDelta, pDelta));
        }
      }

      var c25c = model.Bs(OptionType.Call, k25c, sig25c);
      cDelta = c25c.Delta - c25c.Price/S;
      if (Math.Abs(cDelta - targetDelta) > epsilon)
      {
        logger.Error(String.Format("call delta ({0}) != {1}",
          cDelta, targetDelta));
      }

      var p25p = model.Bs(OptionType.Put, k25p, sig25p);
      pDelta = p25p.Delta - p25p.Price/S;
      if (Math.Abs(pDelta + targetDelta) > epsilon)
      {
        logger.Error(String.Format("put delta ({0}) != -{1}",
          pDelta, targetDelta));
      }
    }

    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(VannaVolgaCalculator));
    const double epsilon = 1E-12;
  }
}
