/*
 * BgmCalibratedColatilities.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.ComponentModel;
using System.Diagnostics;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BgmCorrelation = BaseEntity.Toolkit.Models.BGM.BgmCorrelation;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  ///   Volatility calibration.
  ///   <preliminary>For internal use only.</preliminary>
  /// </summary>
  [Serializable]
  public class BgmCalibratedVolatilities : IForwardVolatilityInfo
  {
    private static int TenorToMonths(string tenor)
    {
      Tenor t = Tenor.Parse(tenor);
      int m = 0;
      switch (t.Units)
      {
      case TimeUnit.Years:
        m = 12;
        break;
      case TimeUnit.Months:
        m = 1;
        break;
      default:
        throw new NotSupportedException(
          "Tenors shorter than a month not supported");
      }
      return m*t.N;
    }

    /// <summary>
    /// Calibrate caplet instantaneous forward volatilities from At-The-Money
    /// swaption quotes by the cascade algorithm of Brigo.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="rule">The cycle rule.</param>
    /// <param name="bdc">The business day convention.</param>
    /// <param name="cal">The calendar.</param>
    /// <param name="correlation">The correlation.</param>
    /// <param name="swpnVolatilities">The swaption volatilities.</param>
    /// <param name="volatilityModel">The volatility model.</param>
    /// <returns></returns>
    public static BgmCalibratedVolatilities CascadeCalibrate(
      Dt asOf,
      DiscountCurve discountCurve,
      string[] expiries, string[] tenors,
      CycleRule rule, BDConvention bdc, Calendar cal,
      BgmCorrelation correlation,
      double[,] swpnVolatilities,
      DistributionType volatilityModel)
    {
      BgmCalibrationInputs data = BgmCalibrationInputs.Create(
        asOf, discountCurve,
        Array.ConvertAll(expiries, TenorToMonths),
        Array.ConvertAll(tenors, TenorToMonths),
        rule, bdc, cal,
        swpnVolatilities);
      correlation.resize(data.GetRateCount());
      BgmCalibrations.CascadeCalibrateGeneric(
        volatilityModel == DistributionType.Normal, correlation, data);
      double[] fractions = data.GetFractions();
      double[] discounts = data.GetDiscountFactors();
      double[] tenorDates = data.GetTenors();
      if (data.GetEffectiveTenorCount() < tenorDates.Length)
      {
        int count = data.GetEffectiveTenorCount();
        Array.Resize(ref tenorDates, count);
        Array.Resize(ref discounts, count);
        Array.Resize(ref fractions, count);
      }
#if NoMore
      var resets = Array.ConvertAll(tenorDates, (t) => new Dt(asOf, t));
      if(discounts.Length< resets.Length)
      {
        Array.Resize(ref resets, discounts.Length);
      }
#endif
      double[,] fwdVolatilities = data.GetVolatilities();
      var volatilities = new BgmCalibratedVolatilities
      {
        asOf_ = asOf,
        method_ = VolatilityBootstrapMethod.Cascading,
        discountFactors_ = discounts,
        fractions_ = fractions,
        tenors_ = tenorDates,
        resetDates_ = Array.ConvertAll(tenorDates, t => new Dt(asOf, t)),
        correlation_ = correlation,
        calibratedParameters_ = fwdVolatilities,
        swpnVolatilities_ = swpnVolatilities,
        volatilityModel_ = volatilityModel
      };
      return volatilities;
    }

    /// <summary>
    /// Calibrate caplet instantaneous forward vols from ATM swaption quotes by fitting parametric forms of the type <m>\Psi(T_i)\Phi(t)</m> or <m>\Psi(T_i)\Phi(T_i - t)</m>
    /// </summary>
    /// <param name="asFunctionOfLength">True to calibrate instantaneous vols of the form <m>\Psi(T_i)\Phi(T_i - t)</m></param>
    /// <param name="calibrateCorrelation">True if correlation among libor rates is to be calibrated jointly to volatilities</param>
    /// <param name="tolerance">Error tolerance</param>
    /// <param name="shapeControls">Constraints to control the shape of functions <m>\Psi</m> and <m>\Phi</m></param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="expiries">String for expiry tenors</param>
    /// <param name="tenors">String for swap tenors</param>
    /// <param name="correlations">Pairwise correlation among libor rates</param>
    /// <param name="swpnVolatilities">At the money Black swaption volatilities</param>
    /// <param name="volatilityModel">The volatility model.</param>
    /// <returns>Calibrated BgmVolatilities object</returns>
    public static BgmCalibratedVolatilities PiecewiseConstantFit(
      bool asFunctionOfLength,
      bool calibrateCorrelation,
      double tolerance,
      double[] shapeControls,
      Dt asOf,
      DiscountCurve discountCurve,
      string[] expiries, string[] tenors,
      BgmCorrelation correlations,
      double[,] swpnVolatilities,
      DistributionType volatilityModel)
    {
      CycleRule rule = CycleRule.None;
      BDConvention bdc = BDConvention.None;
      Calendar cal = Calendar.None;
      BgmCalibrationInputs data = BgmCalibrationInputs.Create(
        asOf, discountCurve,
        Array.ConvertAll(expiries, TenorToMonths),
        Array.ConvertAll(tenors, TenorToMonths),
        rule, bdc, cal,
        swpnVolatilities);
      int rateCount = data.GetRateCount();
      correlations.resize(rateCount);
      var results = new double[rateCount,
        calibrateCorrelation ? 3 : 2];
      int modelChoice = 0;
      var method = VolatilityBootstrapMethod.PiecewiseFitTime;
      if (asFunctionOfLength)
      {
        modelChoice |= 1;
        method = VolatilityBootstrapMethod.PiecewiseFitLength;
      }
      if (calibrateCorrelation)
        modelChoice |= 2;
      BgmCalibrations.PiecewiseConstantFitGeneric(
        volatilityModel == DistributionType.Normal,
        modelChoice, tolerance, shapeControls,
        correlations, data, results);

      double[] discounts = data.GetDiscountFactors();
      double[] tenorDates = data.GetTenors();
      var volatilities = new BgmCalibratedVolatilities
      {
        asOf_ = asOf,
        method_ = method,
        discountFactors_ = discounts,
        fractions_ = data.GetFractions(),
        tenors_ = tenorDates,
        resetDates_ = Array.ConvertAll(tenorDates, (t) => new Dt(asOf, t)),
        correlation_ = correlations,
        calibratedParameters_ = results,
        swpnVolatilities_ = data.GetVolatilities(),
        volatilityModel_ = volatilityModel
      };
      return volatilities;
    }

    //#if KeepObsolete
    [Obsolete]
    internal static BgmCalibratedVolatilities CascadeCalibrate(
      Dt asOf,
      Dt[] maturities,
      DiscountCurve discountCurve,
      BgmCorrelation correlation,
      double[,] swpnVolatilities)
    {
      const DistributionType volatilityModel = DistributionType.LogNormal;
      int nrow = swpnVolatilities.GetLength(0);
      int ncol = swpnVolatilities.GetLength(1);
      if (maturities.Length < nrow + ncol)
      {
        throw new ArgumentException(
          "Not enough rate maturities dates to calibrate all the swaptions.");
      }
      var tuple = SetUpDiscountsAndDates(asOf, maturities,
        discountCurve, Math.Min(maturities.Length - 1, nrow));
      double[] expiries = tuple.Item1;
      double[] discounts = tuple.Item2;
      double[] fractions = tuple.Item3;
      double[,] fwdVolatilities = swpnVolatilities;
      BgmCalibrations.CascadeCalibrate(
        volatilityModel == DistributionType.Normal, fractions,
        discounts, correlation, expiries, fwdVolatilities);
      var swpVolatilities = new double[nrow, ncol];
      BgmCalibrations.SwaptionVolatilities(
        volatilityModel == DistributionType.Normal, fractions, discounts,
        correlation, expiries, fwdVolatilities, swpVolatilities);
      var volatilities = new BgmCalibratedVolatilities
      {
        asOf_ = asOf,
        method_ = VolatilityBootstrapMethod.Cascading,
        resetDates_ = maturities,
        discountFactors_ = discounts,
        fractions_ = fractions,
        tenors_ = expiries,
        correlation_ = correlation,
        calibratedParameters_ = fwdVolatilities,
        swpnVolatilities_ = swpVolatilities,
        volatilityModel_ = volatilityModel
      };
      return volatilities;
    }

    [Obsolete]
    internal static BgmCalibratedVolatilities PiecewiseConstantFit(
      bool asFunctionOfLength,
      bool calibrateCorrelation,
      double tolerance,
      double[] shapeControls,
      Dt asOf,
      Dt[] maturities,
      DiscountCurve discountCurve,
      BgmCorrelation correlations,
      double[,] swpnVolatilities)
    {
      int nrow = swpnVolatilities.GetLength(0);
      int ncol = swpnVolatilities.GetLength(1);
      if (maturities.Length < nrow + ncol)
      {
        throw new ArgumentException(
          "Not enough rate maturities dates to calibrate all the swaptions.");
      }
      Tuple<double[], double[], double[]> tuple = SetUpDiscountsAndDates(asOf,
        maturities,
        discountCurve, maturities.Length - 1);
      double[] expiries = tuple.Item1;
      double[] discounts = tuple.Item2;
      var results = new double[discounts.Length,
        calibrateCorrelation ? 3 : 2];
      swpnVolatilities = (double[,])swpnVolatilities.Clone();
      int modelChoice = 0;
      var method = VolatilityBootstrapMethod.PiecewiseFitTime;
      if (asFunctionOfLength)
      {
        modelChoice |= 1;
        method = VolatilityBootstrapMethod.PiecewiseFitLength;
      }
      if (calibrateCorrelation)
        modelChoice |= 2;
      BgmCalibrations.PiecewiseConstantFit(
        modelChoice, tolerance, shapeControls,
        discounts, expiries,
        correlations, swpnVolatilities, results);
      var volatilities = new BgmCalibratedVolatilities
      {
        asOf_ = asOf,
        method_ = method,
        resetDates_ = maturities,
        discountFactors_ = discounts,
        fractions_ = tuple.Item3,
        tenors_ = expiries,
        correlation_ = correlations,
        calibratedParameters_ = results,
        swpnVolatilities_ = swpnVolatilities,
        volatilityModel_ = DistributionType.LogNormal
      };
      return volatilities;
    }

    //#endif

    private static Tuple<double[], double[], double[]> SetUpDiscountsAndDates(
      Dt asOf,
      Dt[] maturities,
      DiscountCurve discountCurve,
      int expiryCount)
    {
      int nActiveRates = maturities.Length - 1;
      var expiries = new double[expiryCount];
      var discounts = new double[nActiveRates];
      var fractions = new double[nActiveRates];
      {
        Dt firstReset = maturities[0];
        Dt lastDate = firstReset;
        Dt date = firstReset;
        for (int i = 0; i < nActiveRates; ++i)
        {
          if (i < expiries.Length)
          {
            expiries[i] = Dt.Fraction(asOf, date, DayCount.Actual365Fixed);
          }
          date = maturities[i + 1];
          discounts[i] = discountCurve.DiscountFactor(firstReset, date);
          fractions[i] = Dt.Fraction(lastDate, date, DayCount.Actual365Fixed);
          lastDate = date;
        }
      }
      return new Tuple<double[], double[], double[]>
      (
        expiries,
        discounts,
        fractions
      );
    }

    /// <summary>
    /// Gets as-of date.
    /// </summary>
    /// <value>As-of date.</value>
    public Dt AsOf
    {
      get { return asOf_; }
    }

    /// <summary>
    /// Calibrated swaption volatilities
    /// </summary>
    [Browsable(false)]
    public double[,] SwaptionVolatilities
    {
      get { return swpnVolatilities_; }
    }

    /// <summary>
    /// Calibrated parameters  
    /// </summary>
    [Browsable(false)]
    public double[,] CalibratedParameters
    {
      get { return calibratedParameters_; }
    }

    /// <summary>
    /// Correlation among libor rates
    /// </summary>
    [Browsable(false)]
    public BgmCorrelation Correlation
    {
      get { return correlation_; }
    }

    /// <summary>
    /// Calibrated reset dates. 
    /// </summary>
    public Dt[] ResetDates
    {
      get { return resetDates_; }
    }

    /// <summary>
    /// Libor tenors as double
    /// </summary>
    public double[] TenorDates
    {
      get { return tenors_; }
    }

    /// <summary>
    /// Discount factor at each tenor
    /// </summary>
    public double[] DiscountFactors
    {
      get { return discountFactors_; }
    }

    /// <summary>
    /// Choice of calibration method (parametric form)
    /// </summary>
    public VolatilityBootstrapMethod Method
    {
      get { return method_; }
    }

    /// <summary>
    /// Gets the type of the underlying distribution.
    /// </summary>
    /// <value>The type of the distribution.</value>
    public DistributionType DistributionType
    {
      get { return volatilityModel_; }
    }

    /// <summary>
    /// Gets the volatility curves.
    /// </summary>
    /// <remarks></remarks>
    public VolatilityCurve[] ForwardVolatilityCurves
    {
      get { return BuildForwardVolatilityCurves(); }
    }
    #region Forward Volatility Calculations

    /// <summary>
    /// Compute the forward instantaneous volatilities of the underlying libor rates 
    /// </summary>
    /// <returns>Instantaneous forward volatilities</returns>
    /// <remarks>retVal[i,j] = <m>\sigma_i(t)</m> for <m>t \in [T_{j-1},T_j]</m> with <m>T_{-1} =</m> asOf date, where i is the index of the libor rate</remarks>
    public double[,] GetForwardVolatilities()
    {
      int xdim = swpnVolatilities_.GetLength(0);
      int rdim = xdim + swpnVolatilities_.GetLength(1) - 1;
      double[,] result;
      if (method_ == VolatilityBootstrapMethod.Cascading)
      {
        result = GetCascadingForwardVolatilities(
          resetDates_.Length, calibratedParameters_);
      }
      else if (method_ == VolatilityBootstrapMethod.PiecewiseFitTime)
      {
        result = GetTimeFittedForwardVolatilities(calibratedParameters_,
          rdim, xdim);
      }
      else if (method_ == VolatilityBootstrapMethod.PiecewiseFitLength)
      {
        result = GetLengthFittedForwardVolatilities(calibratedParameters_,
          rdim, xdim);
      }
      else
      {
        return null;
      }
      return result;
    }

    private static double[,] GetTimeFittedForwardVolatilities(
      double[,] parameters, int rdim, int xdim)
    {
      int nrow = parameters.GetLength(0);
      if (xdim > nrow || rdim > nrow)
        throw new ArgumentException("Dimension conflict.");
      var results = new double[rdim,xdim];
      for (int i = 0; i < rdim; ++i)
      {
        for (int j = Math.Min(i, xdim - 1); j >= 0; --j)
          results[i, j] = parameters[i, 1]*parameters[j, 0];
      }
      return results;
    }

    private static double[,] GetLengthFittedForwardVolatilities(
      double[,] parameters, int rdim, int xdim)
    {
      int nrow = parameters.GetLength(0);
      if (xdim > nrow || rdim > nrow)
        throw new ArgumentException("Dimension conflict.");
      var results = new double[rdim,xdim];
      for (int i = 0; i < rdim; ++i)
      {
        for (int j = Math.Min(i, xdim - 1); j >= 0; --j)
          results[i, j] = parameters[i, 1]*parameters[i - j, 0];
      }
      return results;
    }

    private static double[,] GetCascadingForwardVolatilities(
      int effectiveTenorCount, double[,] fwdVolatilities)
    {
      int nrow = fwdVolatilities.GetLength(0);
      int ncol = fwdVolatilities.GetLength(1);
      int N = Math.Min(effectiveTenorCount, nrow + ncol - 1);
      var fwdvols = new double[N,nrow];
      for (int n = 0; n < N; ++n)
      {
        int m0 = Math.Max(n - ncol, -1);
        int mstop = Math.Min(n, nrow - 1);
        for (int m = m0 + 1; m <= mstop; ++m)
        {
          double sigma = fwdVolatilities[m, n - m];
          fwdvols[n, m] = sigma;
          if (n - m == ncol - 1)
          {
            for (int j = 0; j < m; ++j)
              fwdvols[n, j] = sigma;
          }
        }
      }
      return fwdvols;
    }

    /// <summary>
    /// Builds an array of the forward volatility curves from the calibration results.
    /// </summary>
    /// <returns>The forward volatility curves</returns>
    public VolatilityCurve[] BuildForwardVolatilityCurves()
    {
      Dt asOf = AsOf;
      var resets = ResetDates;
      var fwdVols = GetForwardVolatilities();
      int nrow = fwdVols.GetLength(0), ncol = fwdVols.GetLength(1);
      var curves = new VolatilityCurve[nrow];
      var interp = new Flat(1.0);
      // Add tenors
      for (int row = 0; row < nrow; ++row)
      {
        int count = row >= ncol ? ncol : (row + 1);
        var curve = new VolatilityCurve(asOf) {Interp = interp};
        for (int j = 0; j < count; ++j)
        {
          double v = fwdVols[row, j];
          if (v <= 0) continue;
          curve.AddVolatility(resets[j], v);
        }
        curve.Fit();
        curve.VerifyFlatCurveLeftContinous();
        curve.Validate();
        curves[row] = curve;
      }

      return curves;
    }

    /// <summary>
    /// Builds an array of the Black volatility curves from the calibration results.
    /// </summary>
    /// <returns>The forward volatility curves</returns>
    public VolatilityCurve[] BuildBlackVolatilityCurves()
    {
      Dt asOf = AsOf;
      var resets = ResetDates;
      var fwdVols = GetForwardVolatilities();
      int nrow = fwdVols.GetLength(0), ncol = fwdVols.GetLength(1);
      var curves = new VolatilityCurve[nrow];

      // Create the interpolator.
      var interp = new SquareLinearVolatilityInterp();

      // Add tenors
      for (int row = 0; row < nrow; ++row)
      {
        int count = row >= ncol ? ncol : (row + 1);
        var curve = new VolatilityCurve(asOf);
        curve.Interp = interp;
        double sumsq= 0, t = 0;
        for (int j = 0; j < count; ++j)
        {
          double v = fwdVols[row, j];
          if (v <= 0) continue;
          double t1 = resets[j] - asOf;
          sumsq += v*v*(t1 - t);
          t = t1;
          curve.AddVolatility(resets[j], Math.Sqrt(sumsq/t));
        }
        curve.Fit();
        curve.Validate();
        curves[row] = curve;
      }

      return curves;
    }


    /// <summary>
    /// Builds a Black (square root average integrated volatility) volatility curve from the calibration result.
    /// </summary>
    /// <returns>The black vol curve <m>\sigma(T)</m></returns>
    public VolatilityCurve BuildBlackVolatilityCurve()
    {
      Dt asOf = AsOf;
      var resets = ResetDates;
      var curves = BuildBlackVolatilityCurves();
      // Create the interpolator.
      var retVal = new VolatilityCurve(asOf);
      for (int i = 0; i < curves.Length; ++i)
        retVal.AddVolatility(resets[i], curves[i].Interpolate(resets[i]));
      retVal.Fit();
      retVal.Validate();
      return retVal;
    }

    #endregion Forward Volatility Calculations


    private Dt asOf_;
    private VolatilityBootstrapMethod method_;
    private Dt[] resetDates_;
    private double[] discountFactors_;
    private double[] fractions_;
    private double[] tenors_;
    private BgmCorrelation correlation_;
    private double[,] calibratedParameters_;
    private double[,] swpnVolatilities_;
    private DistributionType volatilityModel_;

    #region IForwardVolatilities Members

    #endregion
  }
}