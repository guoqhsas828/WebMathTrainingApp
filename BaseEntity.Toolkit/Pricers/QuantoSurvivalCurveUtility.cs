using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using QMath=BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Utilities to deal with survival probability under domestic/foreign numeraires 
  /// </summary>
  public static class QuantoSurvivalCurveUtilities
  {
    /// <summary>
    /// Max covariation for given parametrization
    /// </summary>
    private const double MaxCovariation = 0.80475;

    private const double GaussTreshold = 16.0;

    private const double TinyProbability = 1e-15;

    public const double TinyCorrelation = 1E-10;

    /// <summary>
    /// Convert default probability under the foreign measure to default probability under the domestic measure
    /// </summary>
    /// <param name="foreignDefaultProbability">Foreign default probability</param>
    /// <param name="time">Time (in years)</param>
    /// <param name="sigma">Volatility of forward FX</param>
    /// <param name="rho">Correlation between BM driving forward FX and default time</param>
    /// <param name="theta">FX jump at default</param>
    /// <param name="covariation">Covariation between BM driving forward FX and r.v. driving default time</param>
    /// <returns>Default probability in the doestic measure</returns>
    private static double ToDomesticForwardMeasure(
      double foreignDefaultProbability, double time, double sigma,
      double rho, double theta, out double covariation)
    {
      if (Math.Abs(rho) < TinyCorrelation && Math.Abs(theta) < TinyCorrelation)
      {
        covariation = 0.0;
        return foreignDefaultProbability;
      }
      //- <m>\Lambda_t = \rho \int_{0}^{t} \lambda_s ds</m> with <m>\lambda_s = \frac{1}{1+s}</m>.
      //- The covariation between <m>W_T</m> and <m>M_\infty</m>, <m>\theta = 1</m>. 
      covariation = rho * Math.Log(1 + time);
      var psi = foreignDefaultProbability;
      if (sigma > TinyCorrelation)
      {
        psi = Math.Min(Math.Max(psi, TinyProbability), 1.0 - TinyProbability);
        //- <m>X = \Phi^{-1}\left(F(T)\right)</m>
        var x = Math.Max(-GaussTreshold, Math.Min(
          QMath.NormalInverseCdf(psi), GaussTreshold));
        //- <m>\psi = \Phi\left(X - \Lambda_t \sigma\right)</m>
        psi = QMath.NormalCdf(x - covariation * sigma);
      }
      //- <m>Q^{d}\left(\tau \leq T\right)
      //-   \equiv \frac{E\left[\mathrm{FX}_T(T) I_{\tau \leq T}\right]}{\mathrm{FX}_0(T)}
      //-    = \left[\Phi\left(X - \Lambda_t \sigma\right) \right]^{1+\theta}
      //- </m>
      return Math.Pow(psi, (1.0 + theta));
    }

    /// <summary>
    /// Get the survival probability under the forward measure associated to zero bonds denominated in numeraire currency, 
    /// given the survival probability in the forward measure associated to zero bonds denominated in sc.Ccy
    /// </summary>
    /// <param name="sc">Survival probabilities in sc.Ccy forward measure</param>
    /// <param name="toDate">Last tenor in the curve</param>
    /// <param name="domesticDiscount">Domestic discount curve</param>
    /// <param name="fxAtmVolatility">At the money volatility of forward fx rate</param>
    /// <param name="fxCorrelation">Correlation between <m>\mathrm{FX}_T(T)</m> and default time</param>
    /// <param name="fxDevaluation">Jump of FX at default time. <m>\theta \mathrm{FX}_{\tau-} = \mathrm{FX}_{\tau-}-\mathrm{FX}_{\tau} </m></param>
    /// <param name="stepSize">Step size for term term structure partition</param>
    /// <param name="stepUnit">Step unit for term structure partition</param>
    /// <returns>Survival curve under numeraire currency</returns>
    /// <remarks>
    /// Correlation is here meant as the correlation under the sc.Ccy forward measure  
    /// between the forward FX rate FROM numeraire currency TO denomination currency 
    /// and the Gaussian factor driving default times. The FX devaluation is the jump of the 
    /// forward FX rate FROM numeraire currency TO denomination currency at default.
    /// </remarks>
    /// 
    ///<remarks>
    /// Let <m>W_t</m> be the Brownian motion driving FX rates,
    /// Default time is driven by the closing random variable of the driving martingale i.e. <m>\tau = F^{-1}(\Phi(M_\infty))</m> where 
    ///<m>M_\infty = \int_0^\infty \frac{\sqrt{\theta}}{1 + \theta t} dZ_t</m> with  <m>\langle W,Z\rangle_t = \rho t</m> 
    /// </remarks>
    public static SurvivalCurve ToDomesticForwardMeasure(
      this SurvivalCurve sc, Dt toDate, DiscountCurve domesticDiscount,
      VolatilityCurve fxAtmVolatility, double fxCorrelation,
      double fxDevaluation, int stepSize, TimeUnit stepUnit)
    {
      if (sc == null || sc.AsOf > toDate)
        return null;
      if (sc.Count <= 0)
        throw new ArgumentException("No curve points found");

      // Do we need to compute the implied survival probabilities? 
      if ((sc.Ccy == Currency.None) || (sc.Ccy == domesticDiscount.Ccy)
        || (Math.Abs(fxCorrelation) < TinyCorrelation && Math.Abs(fxDevaluation) < TinyCorrelation)
          || (sc.Defaulted != Defaulted.NotDefaulted))
      {
        return sc.CopyAsDomesticCurve(domesticDiscount);
      }

      // Always construct a new curve with appropriate Ccy and domestic specifications.
      SurvivalCurve retVal = CreateDomesticCurve(sc, domesticDiscount);

      // Now, compute the implied survival on a time grid...
      if (stepSize <= 0 || stepUnit == TimeUnit.None)
      {
        stepSize = 3;
        stepUnit = TimeUnit.Months;
      }
      var dt = sc.AsOf;
      var lastCurveDate = sc.GetDt(sc.Count - 1);
      if (lastCurveDate < toDate) lastCurveDate = toDate;
      var grid = sc.Select(p => p.Date).MergeTimeGrid(dt, lastCurveDate, stepSize, stepUnit, 10);
      //convert to correlation between default time and foreign->domestic FX
      var rho = -Math.Max(Math.Min(fxCorrelation / MaxCovariation, 1.0), -1.0);
      //FX process cannot go negative
      var theta = Math.Max(fxDevaluation, -0.999);
      //jump of foreign->domestic FX at default
      theta = -theta / (1.0 + theta);
      for (int i = 0, n = grid.Count; i < n; ++i)
      {
        dt = grid[i];
        var time = Dt.Years(sc.AsOf, dt, DayCount.Actual365Fixed);
        var pf = 1.0 - sc.Interpolate(dt);
        if (pf >= 1.0)
        {
          retVal.Add(dt, 0.0);
          continue;
        }
        var sigma = fxAtmVolatility.Interpolate(dt);
        double covariation;
        var pd = ToDomesticForwardMeasure(
          pf, time, sigma, rho, theta, out covariation);
        retVal.Add(dt, 1.0 - pd);
        if (i >= 3 && grid[i - 3] > toDate)
          break;
      }
      return retVal;
    }

    private static SurvivalCurve CreateDomesticCurve(SurvivalCurve sc,
      DiscountCurve domesticDiscount)
    {
      var interp = new Weighted(new Const(), new Const());
      var dayCount = DayCount.Actual365Fixed;
      var freq = Frequency.Continuous;
      var calibrator = sc.SurvivalCalibrator;
      if (calibrator != null)
      {
        calibrator = (SurvivalCalibrator) calibrator.ShallowCopy();
        calibrator.DiscountCurve = domesticDiscount;
        return new SurvivalCurve(calibrator, interp, dayCount, freq)
        {
          Ccy = domesticDiscount.Ccy,
        };
      }
      return new SurvivalCurve(sc.AsOf)
      {
        Interp = interp,
        DayCount = dayCount,
        Frequency = freq,
        Ccy = domesticDiscount.Ccy,
      };
    }

    private static SurvivalCurve CopyAsDomesticCurve(
      this SurvivalCurve sc,
      DiscountCurve domesticDiscount)
    {
      SurvivalCurve retVal;
      if (sc.CustomInterpolator == null)
      {
        retVal = CreateDomesticCurve(sc, domesticDiscount);
        // We copy the curve points...
        retVal.Copy(sc);
        // ...and make sure we are using the appropriate calibrator
        retVal.Calibrator = sc.Calibrator;
        return retVal;
      }
      retVal = sc.CloneObjectGraph();
      retVal.Ccy = domesticDiscount.Ccy;
      return retVal;
    }

    /// <summary>
    /// Get the cap adjustment for the quanto structure over the domestic protection leg
    /// </summary>
    /// <remarks>Correlation is here meant as the correlation under the sc.Ccy forward measure
    /// between the forward FX rate FROM numeraire currency TO denomination currency
    /// and the gaussian factor driving default times.
    /// FxDevaluation is the jump of the forward FX rate FROM numeraire currency TO denomination currency
    /// at default.</remarks>
    /// <param name="fsc">Foreign survival curve</param>
    /// <param name="dsc">Domestic survival curve</param>
    /// <param name="asOf">The pricing date</param>
    /// <param name="protectionStart">The protection start date.</param>
    /// <param name="dateGrid">Time grid for pricing</param>
    /// <param name="includeLast">Whether to include the maturity date in protection.</param>
    /// <param name="fxCurve">FX curve</param>
    /// <param name="recovery">Recovery rate</param>
    /// <param name="notionalCap">Notional cap</param>
    /// <param name="contractualFx">contractually decideded conversion rate</param>
    /// <param name="fxAtmVolatility">At the money volatility of forward fx rate</param>
    /// <param name="fxCorrelation">Correlation between <m>\mathrm{FX}_T(T)</m> and default time</param>
    /// <param name="fxDevaluation">Jump of <m>\mathrm{FX}_T(T)</m>at default time</param>
    /// <returns>Survival curve under numeraire currency</returns>
    public static double QuantoCapValue(
      this SurvivalCurve fsc, SurvivalCurve dsc,
      Dt asOf, Dt protectionStart, IList<Dt> dateGrid, bool includeLast,
      FxCurve fxCurve, double recovery,
      double? notionalCap, double? contractualFx,
      VolatilityCurve fxAtmVolatility, double fxCorrelation,
      double fxDevaluation)
    {
      var lgd = 1- recovery;
      return fsc.GetIncrementalProtections(dsc, OptionType.Call,
        asOf, protectionStart, dateGrid, includeLast,
        fxCurve, dt => lgd, notionalCap, contractualFx, fxAtmVolatility,
        fxCorrelation, fxDevaluation).Select(p => p.OptionValue).Sum();
    }

    public static double QuantoValue(
      this SurvivalCurve fsc, SurvivalCurve dsc, OptionType otype,
      Dt asOf, Dt protectionStart, IList<Dt> dateGrid, bool includeLast,
      FxCurve fxCurve, Func<Dt, double> lgdFn,
      double? notionalCap, double? contractualFx,
      VolatilityCurve fxAtmVolatility, double fxCorrelation,
      double fxDevaluation)
    {
      return fsc.GetIncrementalProtections(dsc, otype,
        asOf, protectionStart, dateGrid, includeLast,
        fxCurve, lgdFn, notionalCap, contractualFx, fxAtmVolatility,
        fxCorrelation, fxDevaluation).Select(p => p.OptionValue).Sum();
    }

    public struct IncrementalProtection
    {
      public IncrementalProtection(Dt date, double df, double dfltValue, double optValue)
      {
        Date = date;
        DiscountFactor = df;
        DefaultValue = dfltValue;
        OptionValue = optValue;
      }
      public readonly double OptionValue;
      public readonly double DefaultValue;
      public readonly double DiscountFactor;
      public readonly Dt Date;
    }

    public static IEnumerable<IncrementalProtection> GetIncrementalProtections(
      this SurvivalCurve fsc, SurvivalCurve dsc, OptionType otype,
      Dt asOf, Dt protectionStart, IList<Dt> dateGrid, bool includeLast,
      FxCurve fxCurve, Func<Dt, double> lgdFn,
      double? notionalCap, double? contractualFx,
      VolatilityCurve fxAtmVolatility, double fxCorrelation,
      double fxDevaluation)
    {
      if (fsc == null || dateGrid.IsNullOrEmpty())
      {
        yield return new IncrementalProtection();
        yield break;
      }
      if (fxCurve.Ccy1DiscountCurve == null || fxCurve.Ccy2DiscountCurve == null)
        throw new ArgumentException(
          "FxCurve does not contain foreign and domestic discount curves.");
      var recoveryCcy = fsc.Ccy;
      var numeraireCcy = (fxCurve.Ccy1 == recoveryCcy)
        ? fxCurve.Ccy2 : fxCurve.Ccy1;
      var domesticDiscountCurve = (fxCurve.Ccy1 == numeraireCcy)
        ? fxCurve.Ccy1DiscountCurve : fxCurve.Ccy2DiscountCurve;
      //convert to correlation between default time and foreign->domestic FX
      var rho = -Math.Max(Math.Min(fxCorrelation / MaxCovariation, 1.0), -1.0);
      //FX process cannot go negative
      var theta = Math.Max(fxDevaluation, -0.999);
      //jump of foreign->domestic FX at default
      theta = -theta / (1.0 + theta);
      //spot FX at default
      var spotFx = fxCurve.SpotFxRate.GetRate(recoveryCcy, numeraireCcy);
      var scaledSpotFx = spotFx / (1 + theta);
      var fxAtInception = (contractualFx.HasValue &&
        contractualFx.Value > 1e-9) ? contractualFx.Value : spotFx;
      var cap = notionalCap.HasValue ? notionalCap.Value : 1.0;
      var fxFactor = spotFx / fxAtInception;
      var ddf0 = domesticDiscountCurve.Interpolate(asOf);
      var fdf0 = ddf0 * fxCurve.GetRelativeFxRate(asOf, recoveryCcy, numeraireCcy, spotFx);
      var multiplier = scaledSpotFx / spotFx;
      Dt fromDate = protectionStart;
      var ddf = domesticDiscountCurve.Interpolate(fromDate) / ddf0;
      var fdf = ddf * fxCurve.GetRelativeFxRate(fromDate,
        recoveryCcy, numeraireCcy, spotFx) / fdf0;

      // Check whether the entity has already defaulted.
      if ((fsc.Defaulted != Defaulted.NotDefaulted) && (fsc.DefaultDate.IsValid()))
      {
        var adjLgd = lgdFn(fromDate) * fxFactor;
        int sign = otype == OptionType.Call ? 1 : -1;
        yield return new IncrementalProtection(fromDate, ddf, 1.0,
          Math.Max(sign * (ddf * cap - fdf * adjLgd), 0.0) * multiplier);
        yield break;
      }

      //foreign default probability
      var fsurv0 = fsc.Interpolate(fromDate);
      var fsp = 1.0;
      var dsurv0 = dsc.Interpolate(fromDate);
      var dsp = 1.0;
      for (int i = 0, last = dateGrid.Count - 1; i <= last; ++i)
      {
        fromDate = dateGrid[i];
        var time = Dt.Years(asOf, fromDate, DayCount.Actual365Fixed);
        var fdfOld = fdf;
        var ddfOld = ddf;
        var fspOld = fsp;
        var dspOld = dsp;
        var sigma = fxAtmVolatility.Interpolate(fromDate);
        ddf = domesticDiscountCurve.Interpolate(fromDate) / ddf0;
        fdf = ddf * fxCurve.GetRelativeFxRate(fromDate,
          recoveryCcy, numeraireCcy, spotFx) / fdf0;
        Dt protDate = i == last && includeLast ? Dt.Add(fromDate, 1) : fromDate;
        var adjLgd = lgdFn(protDate) * fxFactor;
        fsp = fsc.Interpolate(protDate) / fsurv0;
        dsp = dsc.Interpolate(protDate) / dsurv0;
        var fep = 0.5 * (fdfOld + fdf) * (fspOld - fsp);
        var adf = 0.5 * (ddfOld + ddf);
        var dep = adf * (dspOld - dsp);
        if (fep * dep <= 0)
        {
          // Sometimes we may end up with negative forward, in which case
          // we switch to intrinsic value as the estimate of the option value.
          int sign = otype == OptionType.Call ? 1 : -1;
          yield return new IncrementalProtection(protDate, adf,
            dep, Math.Max(sign * (dep * cap - adjLgd * fep), 0));
          continue;
        }
        //this discretization ensures consistency with the way we discretize protection PV
        var forward = dep * cap / fep;
        double cov = GetCovariance(time, rho);
        var optval = Black.P(otype, 1.0, forward, adjLgd,
          sigma * Math.Sqrt(Math.Abs(time - cov * cov)));
        yield return new IncrementalProtection(protDate, adf,
          dep, optval * fep * multiplier);
      }
    }


    /// <summary>
    /// Imply a Quanto correlation from SurvivalCurve under foreign and domestic numeraire (i.e. under the measures associated to zero bonds in different currencies) 
    /// </summary>
    /// <param name="survivalCurveCc1">Survival curve under forward measure in Ccy1</param>
    /// <param name="survivalCurveCc2">Survival curve under forward measure in Ccy2</param>
    /// <param name="tenors">Tenors to calibrate to</param>
    /// <param name="fxAtmVolatility">At the money volatility of forward fx rate</param>
    /// <param name="corrRange">Bounds for correlation parameter</param>
    /// <param name="devalRange">Bounds for devaluation parameter</param>
    /// <returns>Implied correlation between forward FX (from Ccy1 to Ccy2) and default time Gaussian transform under the Ccy1 forward measure,
    /// and implied jump size of forward FX (from Ccy1 to Ccy2) at default</returns>
    public static double[] ImpliedQuantoCorrelation(
      this SurvivalCurve survivalCurveCc1, SurvivalCurve survivalCurveCc2,
      Dt[] tenors, VolatilityCurve fxAtmVolatility, double[] corrRange,
      double[] devalRange)
    {
      var targets = Array.ConvertAll(tenors, survivalCurveCc2.Interpolate);
      var transforms = Array.ConvertAll(tenors, dt =>
        Normal.inverseCumulative(1.0 - survivalCurveCc1.Interpolate(dt),
        0.0, 1.0));
      var vols = Array.ConvertAll(tenors, fxAtmVolatility.Interpolate);
      var times = Array.ConvertAll(tenors, dt => Dt.Years(
        survivalCurveCc1.AsOf, dt, DayCount.Actual365Fixed));
      Action<IReadOnlyList<double>, IList<double>, IList<double>> func = (x, f, g) =>
      {
        if (f.Count != 0)
        {
          var rho = x[0] / MaxCovariation;
          var theta = x[1];
          for (int i = 0; i < targets.Length; ++i)
          {
            var covariation = rho * Math.Log(1 + times[i]);
            var psi =
              Normal.cumulative(
                transforms[i] - covariation * vols[i], 0.0, 1.0);
            var survival = Math.Pow(1 - psi, 1 + theta);
            f[i] = targets[i] - survival;
          }
        }
      };
      var optimizer = new NLS(2);
      corrRange = (corrRange.SafeCount() == 2)
        ? Array.ConvertAll(corrRange, r => Math.Min(
          Math.Max(-MaxCovariation, r), MaxCovariation))
        : new[] { -MaxCovariation, MaxCovariation };
      devalRange = (devalRange.SafeCount() == 2)
        ? Array.ConvertAll(devalRange, d => Math.Max(-1.0, d))
        : new[] { -1.0, 1000.0 };
      optimizer.setLowerBounds(new[] { corrRange[0], devalRange[0] });
      optimizer.setUpperBounds(new[] { corrRange[1], devalRange[1] });
      optimizer.setMaxIterations(10000);
      optimizer.setMaxEvaluations(10000);
      var optimizerFunc = DelegateOptimizerFn.Create(
        2, tenors.Length, func, false);
      optimizer.Minimize(optimizerFunc);
      return optimizer.CurrentSolution.ToArray();
    }

    #region Helpers
    /// <summary>
    ///   Make sure all the relevant dates in the base grids are included in the final date grid.
    /// </summary>
    public static IList<Dt> MergeTimeGrid(
      this IEnumerable<Dt> baseGrids, Dt beginDate, Dt endDate,
      int stepSize, TimeUnit stepUnit, int toleranceDays)
    {
      Debug.Assert(beginDate.IsValid() && beginDate < endDate);
      if (stepSize <= 0 || stepUnit == TimeUnit.None)
      {
        return baseGrids == null
          ? null
          : baseGrids.Where(d => d > beginDate && d < endDate)
            .Append(endDate).ToList();
      }
      var grids = (baseGrids as IList<Dt>) ??
        (baseGrids != null ? baseGrids.ToList() : null);
      var list = new UniqueSequence<Dt>();
      int baseIdx = 0, baseCount = grids == null ? 0 : grids.Count;
      while (beginDate < endDate)
      {
        Dt nextStopDate = endDate;
        for (; baseIdx < baseCount; ++baseIdx)
        {
          // ReSharper disable PossibleNullReferenceException
          var nextBaseDate = grids[baseIdx];
          // ReSharper restore PossibleNullReferenceException
          if (nextBaseDate <= beginDate) continue;
          if (nextBaseDate >= endDate)
          {
            baseIdx = baseCount;
            break;
          }
          nextStopDate = nextBaseDate;
          ++baseIdx;
          break;
        }
        while (true)
        {
          beginDate = Dt.Add(beginDate, stepSize, stepUnit);
          if (nextStopDate - beginDate <= toleranceDays)
          {
            list.Add(beginDate = nextStopDate);
            break;
          }
          list.Add(beginDate);
        }
      }
      list.Add(endDate);
      return list;
    }

    private static int SafeCount<T>(this T[] array)
    {
      if (array == null)
        return -1;
      return array.Length;
    }

    private static double GetCovariance(double time, double rho)
    {
      return rho * Math.Log(1 + time);
    }

    private static double GetRelativeFxRate(this FxCurve fxCurve,
      Dt date, Currency recoveryCcy, Currency numeraireCcy, double spotRate)
    {
      double forward = fxCurve.FxRate(date, recoveryCcy, numeraireCcy);
      return forward / spotRate;
    }
    #endregion
  }
}
