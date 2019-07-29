using System;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
   public class HullWhiteParameter
  {
    #region Constructor
    /// <summary>
    /// The object of Hull-white parameters
    /// </summary>
    /// <param name="timePoints">The time grid in the calibrations</param>
    /// <param name="rateCurve">The rate curve</param>
    /// <param name="meanReversionCurve">The mean reversion curve</param>
    /// <param name="sigmaCurve">The sigma curve</param>
    public HullWhiteParameter(Dt[] timePoints,
      DiscountCurve rateCurve,
      VolatilityCurve meanReversionCurve,
      VolatilityCurve sigmaCurve)
    {
      TimePoints = timePoints;
      RateCurve = rateCurve;
      MeanReversionCurve = meanReversionCurve;
      SigmaCurve = sigmaCurve;
    }

    #endregion

    #region Properties
    /// <summary>
    /// As-Of date
    /// </summary>
    public Dt AsOf => MeanReversionCurve?.AsOf ?? Dt.Empty;

    /// <summary>
    /// The time grid
    /// </summary>
    public Dt[] TimePoints { get; }

    /// <summary>
    /// The rate curve
    /// </summary>
    public DiscountCurve RateCurve { get; }
    
    /// <summary>
    /// The mean reversion curve
    /// </summary>
    public VolatilityCurve MeanReversionCurve { get; }
    
    /// <summary>
    /// The sigma curve
    /// </summary>
    public VolatilityCurve SigmaCurve { get; }

    internal double[] Dfs => _dfs ??
               (_dfs = TimePoints.Select(tp =>
               HullWhiteUtil.GetDiscountCurve(RateCurve)
               .Interpolate(tp)).ToArray());

    internal double[] Pfs => _pfs ?? (_pfs = TimePoints.Select(tp =>
                 RateCurve.Interpolate(tp)).ToArray());

    internal double[] SpreadFactors => _spreads ??
        (_spreads = HullWhiteUtil.CalcSpreadFactors(Dfs, Pfs));

    #endregion

    #region Interpolators

    /// <summary>
    /// Interpolator for swaption volatility
    /// </summary>
    /// <param name="start">The start date</param>
    /// <param name="expiry">The expiry date</param>
    /// <param name="maturity">The maturity date</param>
    /// <param name="volType">The volatility type</param>
    /// <returns></returns>
    public double InterpolateSwptVolatility(Dt start, Dt expiry,
     Dt maturity, DistributionType volType)
    {
      if (start >= expiry || expiry >= maturity) return 0.0;

      double[] tps, means, sigmas;
      int eI, mI;
      GetCalcValues(expiry, maturity, out tps, out means, out sigmas, out eI, out mI);
      double annuity;
      var swapRate = HullWhiteUtil.CalcSwapRate(eI, mI, Pfs, Dfs, tps, out annuity);
      var calc = BaseEntity.Toolkit.Models.HullWhiteShortRates.PiecewiseConstantCalculator.Create(tps, sigmas, means);
      var modelPv = calc.EvaluateSwaptionPayer(eI, mI, swapRate, Dfs,
        tps, GetFractions(tps), SpreadFactors);
      return volType == DistributionType.Normal
        ? BlackNormal.ImpliedVolatility(OptionType.Call, tps[eI], 0, swapRate, 
        swapRate, modelPv / annuity)
        : Black.ImpliedVolatility(OptionType.Call, tps[eI], swapRate, swapRate,
        modelPv / annuity);
    }


   
    /// <summary>
    /// Interpolator for swaption volatility
    /// </summary>
    /// <param name="expiry">Expiry date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="volType">Volatility type</param>
    /// <returns></returns>
    public double InterpolateSwptVolatility(Dt expiry, Dt maturity, 
      DistributionType volType)
    {
      return InterpolateSwptVolatility(AsOf, expiry, maturity, volType);
    }

    /// <summary>
    /// Interpolator for caplet volatility
    /// </summary>
    /// <param name="start">Start date</param>
    /// <param name="resetDate">Reset date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="volType">Volatility type</param>
    /// <returns></returns>
    public double InterpolateCapletVolatility(Dt start,
      Dt resetDate, Dt maturity,
      DistributionType volType)
    {
      if (start >= resetDate || resetDate >= maturity) return 0.0;
      double[] tps, means, sigmas;
      int eI, mI;
      GetCalcValues(resetDate, maturity, out tps, out means,
        out sigmas, out eI, out mI);
      var calc = BaseEntity.Toolkit.Models.HullWhiteShortRates.PiecewiseConstantCalculator.Create(tps, sigmas, means);
      return calc.CalculateCapletVolatility(eI, mI);
    }

  
    /// <summary>
    /// Interpolator for caplet volatility 
    /// </summary>
    /// <param name="expiry">Expiry date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="volType">Volatility Type</param>
    /// <returns></returns>
    public double InterpolateCapletVolatility(Dt expiry, Dt maturity, 
      DistributionType volType)
    {
      return InterpolateCapletVolatility(AsOf, expiry, maturity, volType);
    }

    #endregion Interpolators

    #region Helpers

    private void GetCalcValues(Dt begin, Dt end, out double[] times,
    out double[] means, out double[] sigmas, out int eI, out int mI)
    {
      times = ConvertToDoubles(TimePoints, DayCount.Actual365Fixed);
      means = TimePoints.Select(tp => MeanReversionCurve.Interpolate(tp)).ToArray();
      sigmas = TimePoints.Select(tp => SigmaCurve.Interpolate(tp)).ToArray();
      eI = HullWhiteUtil.GetIndex(TimePoints, begin);
      mI = HullWhiteUtil.GetIndex(TimePoints, end);
    }

    private double[] GetFractions(double[] times)
    {
      var n = times.Length;
      var fractions = new double[n];
      for (int i = 0; i < n; i++)
      {
        fractions[i] = times[i] - (i == 0 ? 0.0 : times[i - 1]);
      }
      return fractions;
    }
    private double[] ConvertToDoubles(Dt[] dates, DayCount dayCount)
    {
      return dates.Select(d => Dt.Fraction(AsOf, d, dayCount)).ToArray();
    }

    #endregion Helpers

    #region Data

    private double[] _dfs;
    private double[] _pfs;
    private double[] _spreads;

    #endregion Data



  }
}
