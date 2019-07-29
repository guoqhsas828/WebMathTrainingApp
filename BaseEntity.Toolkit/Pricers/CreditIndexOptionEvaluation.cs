//
// CreditIndexOptionEvaluation.cs
//  -2015. All rights reserved.
//

using System;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Integrals;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  using Forwards = CreditIndexOptionPricer.Forwards;

  /// <summary>
  ///  Implement various evaluation models for the CDX options
  /// </summary>
  internal static class CreditIndexOptionEvaluation
  {
    private static readonly log4net.ILog Logger
      = log4net.LogManager.GetLogger(typeof (CreditIndexOptionEvaluation));

    #region public interfaces

    public static double CalculateFairValue(this CDXOption cdxo,
      Dt asOf, DiscountCurve discountCurve, double recoveryRate,
      Forwards fwd, double volatility, CDXOptionModelType modelType)
    {
      return cdxo.GetModel(modelType, fwd, asOf, discountCurve, recoveryRate)
        .CalculateFairValue(volatility);
    }

    public static double CalculateExerciseProbability(this CDXOption cdxo,
      Dt asOf, DiscountCurve discountCurve, double recoveryRate,
      Forwards fwd, double volatility, CDXOptionModelType modelType)
    {
      return cdxo.GetModel(modelType, fwd, asOf, discountCurve, recoveryRate)
        .CalculateExerciseProbability(volatility);
    }

    public static double ImplyVolatility(
      this CDXOption cdxo, double fairValue,
      Dt asOf, DiscountCurve discountCurve, double recoveryRate,
      Forwards fwd, CDXOptionModelType modelType)
    {
      return cdxo.GetModel(modelType, fwd, asOf, discountCurve, recoveryRate)
        .ImplyVolatility(fairValue);
    }

    #endregion

    #region Forward upfront value calculation

    internal static double CalculateForwardUpfrontValue(
      this CDXOption cdxo, double quoteValue, bool quoteIsPrice,
      DiscountCurve discountCurve, double recoveryRate)
    {
      if (quoteIsPrice)
      {
        return 1 - quoteValue;
      }
      var fwddate = cdxo.Expiration;
      var pricer = new CDXPricer(cdxo.CDX,
        fwddate, fwddate, discountCurve, cdxo.Strike)
      {
        MarketQuote = quoteValue,
        QuotingConvention = QuotingConvention.CreditSpread,
        MarketRecoveryRate = recoveryRate
      }.EquivalentCDSPricer;
      pricer.CDS.Fee = 0;
      pricer.CDS.FeeSettle = Dt.Empty;
      return -pricer.FlatPrice();
    }

    #endregion

    #region Build models for evaluation

    private static void GetBarriers(Barrier barrier,
      ref double upper, ref double lower)
    {
      var bt = barrier.BarrierType;
      if (bt == OptionBarrierType.UpOut || bt == OptionBarrierType.DownIn)
        upper = barrier.Value;
      else if (bt == OptionBarrierType.UpIn || bt == OptionBarrierType.DownOut)
        lower = barrier.Value;
      else
        throw new ToolkitException(String.Format(
          "Barrier type {0} not supported", bt));
    }

    private static Model GetModel(this CDXOption cdxo,
      CDXOptionModelType modelType, Forwards fwd,
      Dt asOf, DiscountCurve discountCurve, double recoveryRate)
    {
      if (cdxo.IsBarrier)
      {
        double upper = Double.NaN, lower = Double.NaN;
        GetBarriers(cdxo.Barriers[0], ref upper, ref lower);
        if (cdxo.IsDoubleBarrier)
          GetBarriers(cdxo.Barriers[1], ref upper, ref lower);

        bool isPrice = cdxo.StrikeIsPrice;
        if (isPrice)
        {
          var tmp = upper;
          upper = lower;
          lower = tmp;
        }
        upper = Double.IsNaN(upper) ? Double.PositiveInfinity
          : cdxo.CalculateForwardUpfrontValue(upper, isPrice,
            discountCurve, recoveryRate);
        lower = Double.IsNaN(lower) ? Double.NegativeInfinity
          : cdxo.CalculateForwardUpfrontValue(lower, isPrice,
            discountCurve, recoveryRate);

        return new BarrierModel(cdxo.IsPayer(), fwd, upper, lower,
          cdxo.CreateCdsPricer(asOf, discountCurve, recoveryRate),
          cdxo.ForwardProtectionStartDate(asOf), cdxo.Expiration);
      }

      if (cdxo.IsDigital)
      {
        return modelType == CDXOptionModelType.BlackPrice
          ? (Model) new PriceDigitalModel(cdxo.IsPayer(), fwd)
          : new SpreadDigitalModel(cdxo.IsPayer(), fwd,
            cdxo.CreateCdsPricer(asOf, discountCurve, recoveryRate),
            cdxo.Expiration);
      }

      switch (modelType)
      {
      case CDXOptionModelType.Black:
        return new SpreadModel(cdxo.IsPayer(), fwd, cdxo.CDX.Premium);
      case CDXOptionModelType.BlackPrice:
        return new PriceModel(cdxo.IsPayer(), fwd);
      case CDXOptionModelType.BlackArbitrageFree:
        return new ArbitrageFreeSpreadModel(cdxo.IsPayer(), fwd,
          cdxo.CDX.Premium*CalculateForwardRisklessPv01(cdxo, discountCurve));
      case CDXOptionModelType.ModifiedBlack:
        return new ModifiedBlackModel(cdxo.IsPayer(), fwd,
          cdxo.CreateCdsPricer(asOf, discountCurve, recoveryRate),
          cdxo.Expiration);
      case CDXOptionModelType.FullSpread:
        return new FullSpreadModel(cdxo.IsPayer(), fwd,
          cdxo.CreateCdsPricer(asOf, discountCurve, recoveryRate),
          cdxo.ForwardProtectionStartDate(asOf), cdxo.Expiration);
      default:
        throw new ArgumentException(String.Format(
          "{0}: unknown model type", modelType));
      }
    }

    private static bool IsPayer(this CDXOption cdxo)
    {
      return cdxo.Type == OptionType.Put;
    }

    private static CDSCashflowPricer CreateCdsPricer(
      this CDXOption cdxo, Dt asOf, DiscountCurve discountCurve,
      double recoveryRate)
    {
      var p = new CDSCashflowPricer(cdxo.CDX.CreateCompatibleCds(),
        asOf, asOf, discountCurve, null, 0, TimeUnit.None)
      {
        RecoveryCurve = new RecoveryCurve(asOf, recoveryRate)
      };
      return p;
    }

    private static Dt ForwardProtectionStartDate(
      this CDXOption cdxo, Dt asOf)
    {
      return cdxo.Effective >= asOf ? cdxo.Effective : asOf;
    }

    private static double CalculateForwardRisklessPv01(
      CDXOption option, DiscountCurve discountCurve)
    {
      var fwddate = option.Expiration;
      var cds = option.CDX.CreateCompatibleCds();
      cds.Premium = 1.0;
      var pricer = new CDSCashflowPricer(cds,
        fwddate, fwddate, discountCurve, null, 0, TimeUnit.None);
      return pricer.FlatFeePv();
    }

    #endregion

    #region Nested type: Models

    private abstract class Model
    {
      protected readonly bool IsCall;
      protected readonly double Forward, Strike, Multiplier;


      protected Model(bool isCall,
        double forward, double strike, double multiplier)
      {
        IsCall = isCall;
        Forward = forward;
        Strike = strike;
        Multiplier = multiplier;
      }

      internal abstract double CalculateFairValue(double volatility);

      internal abstract double CalculateExerciseProbability(double volatility);

      internal virtual double ImplyVolatility(double fv)
      {
        return GenericImplyVolatility(fv, CalculateFairValue);
      }
    }

    private abstract class BlackModel : Model
    {
      protected BlackModel(bool isCall,
        double forward, double strike, double multiplier)
        : base(isCall, forward, strike, multiplier)
      {
      }

      internal override double CalculateFairValue(double volatility)
      {
        return CalculateBlackScholesValue(IsCall,
          Forward, Strike, Multiplier, volatility);
      }

      internal override double CalculateExerciseProbability(double volatility)
      {
        return CalculateLogNormalProbability(
          IsCall, Forward, Strike, volatility);
      }

      internal override double ImplyVolatility(double fv)
      {
        return ImplyBlackScholesVolatility(fv,
          IsCall, Forward, Strike, Multiplier);
      }
    }

    private class PriceModel : BlackModel
    {
      internal PriceModel(bool isPayer, Forwards fwd)
        : base(!isPayer, 1 - fwd.Value,
          1 - AdjustedStrike(fwd), AdjustedFactor(fwd))
      {
      }
    }

    private class SpreadModel : BlackModel
    {
      private SpreadModel(bool isPayer, double v, double k, double f, double a)
        : base(isPayer, v + a, k + a, f)
      {
      }

      internal SpreadModel(bool isPayer, Forwards fwd, double premium)
        : this(isPayer, fwd.Value, AdjustedStrike(fwd), AdjustedFactor(fwd),
          fwd.Factor*premium*fwd.Pv01)
      {
      }
    }

    private class ArbitrageFreeSpreadModel : BlackModel
    {
      private ArbitrageFreeSpreadModel(bool isPayer,
        double v, double k, double f, double a)
        : base(isPayer, v + a, k + a, f)
      {
      }

      internal ArbitrageFreeSpreadModel(bool isPayer,
        Forwards fwd, double annuity)
        : this(isPayer, fwd.Value, AdjustedStrike(fwd),
          AdjustedFactor(fwd), fwd.Factor*annuity)
      {
      }
    }


    private class ModifiedBlackModel : Model
    {
      #region Data and constructors

      private readonly CDSCashflowPricer _fwdCdsPricer;

      protected ModifiedBlackModel(CDSCashflowPricer fwdPricer, Dt fwdDate,
        bool isPayer, double fwdUpfront, double strikeValue, double factor)
        : base(isPayer, fwdUpfront, strikeValue, factor)
      {
        var pricer = _fwdCdsPricer = fwdPricer;
        pricer.AsOf = pricer.Settle = fwdDate;
        var sc = pricer.SurvivalCurve = new SurvivalCurve(pricer.AsOf, 0.0);
        sc.Flags |= CurveFlags.Stressed;
      }

      private ModifiedBlackModel(CDSCashflowPricer fwdPricer, Dt fwdDate,
        bool isPayer, Forwards fwd, double factor)
        : this(fwdPricer, fwdDate, isPayer, fwd.Upfront,
          fwd.DiscountFactor*(fwd.InitialFactor*fwd.StrikeValue
            - fwd.Loss - fwd.Factor*fwd.FrontEndProtection)/factor,
          factor)
      {
      }

      internal ModifiedBlackModel(bool isPayer, Forwards fwd,
        CDSCashflowPricer fwdCdsPricer, Dt fwdDate)
        : this(fwdCdsPricer, fwdDate, isPayer, fwd,
          fwd.Factor*fwd.DiscountFactor*fwd.SurvivalProbability)
      {
      }

      #endregion

      #region Overrides of Model

      internal override double CalculateFairValue(double v)
      {
        var isPayer = IsCall;
        var factor = Multiplier;
        var fwdValue = Forward;
        var fwdStrikeValue = Strike;

        if (v < 1E-12)
        {
          return factor*Math.Max(0.0,
            (isPayer ? 1 : -1)*(fwdValue - fwdStrikeValue));
        }

        var pricer = _fwdCdsPricer;
        var premium = pricer.CDS.Premium;
        var spread = CalibrateSpreadFromUpfront(pricer, fwdValue);
        var strike = CalibrateSpreadFromUpfront(pricer, fwdStrikeValue);
        var mu = CalibrateSpreadCenter(pricer, fwdValue, spread, strike, v);
        var s0 = Math.Exp(mu);
        var constant = pricer.Accrued() - fwdStrikeValue;
        Func<double, double> fn = x =>
        {
          var s = pricer.CDS.Premium = s0*x;
          pricer = CalibrateFromPv(pricer, 0.0, s);
          pricer.CDS.Premium = premium;
          pricer.Reset();
          var y = (isPayer ? 1 : -1)*(constant - pricer.ProductPv());
          if (y < -1E-12 && Logger.IsWarnEnabled)
          {
            Logger.Warn(String.Format("Expect non-negative, got {0}", y));
            log4net.GlobalContext.Properties["ModifiedBlack"] = y;
          }
          return y;
        };

#if DEBUG
        if (Logger.IsWarnEnabled)
        {
          var shouldBeZero = fn(strike/s0);
          if (Math.Abs(shouldBeZero) > 1E-12)
            Logger.Warn(String.Format("Expect zero, got {0}", shouldBeZero));
        }
#endif

        var d = (Math.Log(strike) - mu)/v;
        var quad = new LogNormal(d, v);
        var fv = isPayer ? quad.RightIntegral(fn) : quad.LeftIntegral(fn);
        return fv*factor;
      }

      internal override double CalculateExerciseProbability(double v)
      {
        var pricer = _fwdCdsPricer;
        var spread = CalibrateSpreadFromUpfront(pricer, Forward);
        var strike = CalibrateSpreadFromUpfront(pricer, Strike);
        var mu = CalibrateSpreadCenter(pricer, Forward, spread, strike, v);
        var s0 = Math.Exp(mu + 0.5*v*v);
        return CalculateLogNormalProbability(IsCall, s0, strike, v);
      }

      #endregion

      #region Helpers

      private static double CalibrateSpreadFromUpfront(
        CDSCashflowPricer pricer, double fwdUpfront)
      {
        return GetSpread(CalibrateFromPv(pricer, -fwdUpfront, 0));
      }

      private static double CalibrateSpreadCenter(
        CDSCashflowPricer pricer,
        double upfront, double spread, double bound,
        double stddev)
      {
        var targetPv = pricer.Accrued() - upfront;
        var mu0 = Math.Log(spread) - 0.5*stddev*stddev;
        var res = NonLinearLogNormalOption.Solve(
          mu => CalculateExpectation(pricer, bound, mu, stddev),
          targetPv, mu0 - 0.05, mu0 + 0.05);
        return res;
      }

      private static double CalculateExpectation(
        CDSCashflowPricer pricer,
        double bound, double mu, double stddev)
      {
        return NonLinearLogNormalOption.CalculateExpectation(
          mu, bound, stddev, s =>
          {
            var premium = pricer.CDS.Premium;
            try
            {
              pricer.CDS.Premium = s;
              pricer = CalibrateFromPv(pricer, 0.0, s);
            }
            finally
            {
              pricer.CDS.Premium = premium;
            }
            return pricer.ProductPv();
          });
      }

      private static CDSCashflowPricer CalibrateFromPv(
        CDSCashflowPricer pricer, double targetCleanPv,
        double guessedSpread)
      {
        var sc = pricer.SurvivalCurve;
        var targetPv = targetCleanPv + pricer.Accrued();
        var x0 = (guessedSpread > 0 ? guessedSpread : pricer.CDS.Premium)
          /(1 - pricer.RecoveryRate);
        var res = NonLinearLogNormalOption.SolveForX(targetPv, x =>
        {
          sc.Spread = x;
          pricer.Reset();
          return pricer.ProductPv();
        }, x0);
        sc.Spread = res;
        pricer.Reset();
        return pricer;
      }

      private static double GetSpread(CDSCashflowPricer pricer)
      {
        var premium = pricer.CDS.Premium;
        try
        {
          pricer.CDS.Premium = 1.0;
          pricer.Reset();
          return -pricer.ProtectionPv()/pricer.FlatFeePv();
        }
        finally
        {
          pricer.CDS.Premium = premium;
        }
      }

      #endregion
    }

    private class FullSpreadModel : Model
    {
      #region Data and constructors

      private readonly CDSCashflowPricer _fwdCdsPricer;
      private readonly Dt _fwdDate;

      public FullSpreadModel(bool isPayer, Forwards fwd,
        CDSCashflowPricer fwdCdsPricer,
        Dt protectionStartDate, Dt feeStartDate)
        : base(isPayer,
          fwd.DiscountFactor*fwd.Factor*fwd.Value/fwd.Factor,
          fwd.DiscountFactor*AdjustedStrike(fwd),
          fwd.Factor)
      {
        _fwdDate = feeStartDate;
        var pricer = _fwdCdsPricer = fwdCdsPricer;
        if (pricer.AsOf > protectionStartDate)
          pricer.AsOf = protectionStartDate;
        pricer.Settle = protectionStartDate;
        var sc = pricer.SurvivalCurve
          = new SurvivalCurve(_fwdCdsPricer.AsOf, 0.0);
        sc.Flags |= CurveFlags.Stressed;
      }

      #endregion

      #region Overrides of Model

      internal override double CalculateFairValue(double volatility)
      {
        return Multiplier*NonLinearLogNormalOption.CalculateValue(
          CalculateValue, Forward, Strike,
          IsCall, volatility, GetInitialGuessOfHazardRate());
      }

      internal override double CalculateExerciseProbability(double v)
      {
        return NonLinearLogNormalOption.CalculateProbability(
          CalculateValue, Forward, Strike,
          IsCall, v, GetInitialGuessOfHazardRate());
      }

      #endregion

      #region Helpers

      protected double GetInitialGuessOfHazardRate()
      {
        var pricer = _fwdCdsPricer;
        return pricer.CDS.Premium/(1 - pricer.RecoveryRate);
      }

      protected double CalculateValue(double hazardRate)
      {
        var pricer = _fwdCdsPricer;
        var fwdDate = _fwdDate;
        var sc = pricer.SurvivalCurve;
        sc.Spread = hazardRate;
        pricer.Reset();
        var begin = pricer.Settle;
        var protection = -pricer.ProtectionPv();
        try
        {
          pricer.Settle = fwdDate;
          pricer.Reset();
          var fee = pricer.FlatFeePv()*sc.SurvivalProb(begin, fwdDate);
          return protection - fee;
        }
        finally
        {
          pricer.Settle = begin;
        }
      }

      #endregion
    }

    private class SpreadDigitalModel : ModifiedBlackModel
    {
      internal SpreadDigitalModel(bool isPayer, Forwards fwd,
        CDSCashflowPricer fwdCdsPricer, Dt fwdDate)
        : base(fwdCdsPricer, fwdDate, isPayer, fwd.Upfront,
          fwd.StrikeValue, fwd.InitialFactor*fwd.DiscountFactor)
      {
      }

      internal override double CalculateFairValue(double volatility)
      {
        return Multiplier*CalculateExerciseProbability(volatility);
      }
    }

    private class PriceDigitalModel : Model
    {
      internal PriceDigitalModel(bool isPayer, Forwards fwd)
        : base(isPayer, 1 - fwd.Upfront, 1 - fwd.StrikeValue,
          fwd.InitialFactor*fwd.DiscountFactor)
      {
      }

      internal override double CalculateFairValue(double volatility)
      {
        return Multiplier*CalculateExerciseProbability(volatility);
      }

      internal override double CalculateExerciseProbability(double v)
      {
        return CalculateLogNormalProbability(IsCall, Forward, Strike, v);
      }
    }

    private class BarrierModel : FullSpreadModel
    {
      #region Data and constructors

      private readonly double _upperValue, _lowerValue;

      public BarrierModel(bool isPayer, Forwards fwd,
        double upperBarrierValue, double lowerBarrierValue,
        CDSCashflowPricer fwdCdsPricer,
        Dt protectionStartDate, Dt feeStartDate)
        : base(isPayer, fwd, fwdCdsPricer, protectionStartDate, feeStartDate)
      {
        _upperValue = (fwd.InitialFactor*upperBarrierValue - fwd.Loss)
          *fwd.DiscountFactor/fwd.Factor;
        _lowerValue = (fwd.InitialFactor*lowerBarrierValue - fwd.Loss)
          *fwd.DiscountFactor/fwd.Factor;
      }

      #endregion

      #region Overrides of Model

      internal override double CalculateFairValue(double volatility)
      {
        return Multiplier*NonLinearLogNormalOption.CalculateValue(
          CalculateValue, Forward, Strike, _lowerValue, _upperValue,
          IsCall, volatility, GetInitialGuessOfHazardRate());
      }

      internal override double CalculateExerciseProbability(double volatility)
      {
        throw new NotImplementedException();
      }

      #endregion
    }

    #endregion

    #region Helpers

    internal static double CalculateBlackScholesValue(bool isCall,
      double forward, double strike, double multiplier, double v)
    {
      if (v < 1E-12 || strike <= 0 || forward <= 0)
        return Math.Max((isCall ? 1 : -1)*(forward - strike)*multiplier, 0.0);
      return isCall
        ? (Black.B(forward/strike, v)*strike*multiplier)
        : (Black.B(strike/forward, v)*forward*multiplier);
    }

    internal static double CalculateLogNormalProbability(
      bool isCall, double forward, double strike, double v)
    {
      double p;
      if (v < 1E-12 || strike <= 0 || forward <= 0)
      {
        p = forward > strike ? 1 : 0;
      }
      else
      {
        double u = Math.Log(forward/strike)/v - 0.5*v;
        p = SpecialFunctions.NormalCdf(u);
      }
      return isCall ? p : (1 - p);
    }

    internal static double ImplyBlackScholesVolatility(double fv, bool isCall,
      double forward, double strike, double multiplier)
    {
      if (fv <= 0 || multiplier <= 0 || forward <= 0)
        return Double.NaN;
      var k = strike/forward;
      var target = fv/forward/multiplier;
      var gap = target - Math.Max(0, (isCall ? 1 : -1)*(1 - k));
      var tolerance = Math.Min(target/10000, 10*DoubleNumberComparison.MachineEpsilon);
      if (gap < 0.5 * tolerance)
      {
        return gap < -2 * tolerance ? Double.NaN : 0;
      }
      return SpecialFunctions.BlackScholesImpliedVolatility(
        isCall, target, k, tolerance);
    }

    private static double GenericImplyVolatility(double fv, Func<double, double> fn)
    {
      // Set up root finder
      Brent2 rf = new Brent2();
      rf.setToleranceX(1e-5);
      rf.setToleranceF(1e-7);
      rf.setLowerBounds(1E-10);
      rf.setUpperBounds(9.9999);

      // Solve
      try
      {
        double v = fn(0.1);
        if (v >= fv)
        {
          return rf.solve(fn, null, fv, 0.01, 0.10);
        }
        v = fn(1.0);
        if (v >= fv)
        {
          return rf.solve(fn, null, fv, 0.1, 1.0);
        }
        v = fn(2.0);
        if (v >= fv)
        {
          return rf.solve(fn, null, fv, 1.0, 2.0);
        }
        v = fn(4.0);
        if (v >= fv)
        {
          return rf.solve(fn, null, fv, 2.0, 4.0);
        }
        return rf.solve(fn, null, fv, 4.0, 8.0);
      }
      catch (SolverException ex)
      {
        return Double.NaN;
      }
    }

    private static double AdjustedStrike(Forwards fwd)
    {
      return (fwd.InitialFactor*fwd.StrikeValue - fwd.Loss)/fwd.Factor;
    }

    private static double AdjustedFactor(Forwards fwd)
    {
      return fwd.Factor*fwd.DiscountFactor;
    }

    #endregion
  }
}
