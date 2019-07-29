using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

using SmileFunc = System.Func<double,
  BaseEntity.Toolkit.Curves.Volatilities.SmileInputKind, double>;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   The base class for volatility surfaces based on Black-Scholes model.
  /// </summary>
  [Serializable]
  public abstract class BlackScholesSurfaceCalibrator
    : IVolatilitySurfaceCalibrator, IBlackScholesParameterDataProvider
  {
    private readonly Interp _smileInterp, _timeInterp;
    private readonly SmileModel _model;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackScholesSurfaceCalibrator" /> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="smileInterp">The smile interp.</param>
    /// <param name="timeInterp">The time interp.</param>
    /// <param name="model">The smile model.</param>
    protected BlackScholesSurfaceCalibrator(
      Dt asOf, Interp smileInterp, Interp timeInterp, SmileModel model)
    {
      AsOf = asOf;
      _smileInterp = smileInterp;
      _timeInterp = timeInterp;
      _model = model;
    }

    /// <summary>
    /// Builds a new volatility surface from the specified tenors.
    /// </summary>
    /// <param name="tenors">The tenors.</param>
    /// <param name="smileInputKind">The smile input kind.</param>
    /// <returns>The volatility surface.</returns>
    public CalibratedVolatilitySurface BuildSurface(
      IEnumerable<IVolatilityTenor> tenors, SmileInputKind smileInputKind)
    {
      Debug.Assert(tenors != null);
      var tenorArray = tenors.ToArray();
      if (tenorArray.Length == 0)
      {
        throw new ToolkitException("Empty tenors.");
      }
      return new CalibratedVolatilitySurface(AsOf, tenorArray, this,
        new VolatilitySurfaceInterpolator(GetTime,
          Fit(tenorArray, smileInputKind)));
    }

    /// <summary>
    /// Gets the time in year fraction between two dates.
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <returns>The time in year fraction.</returns>
    /// <remarks>
    /// The default implementation simply calculates the year fraction as the number of days
    /// between the start and the end date divided by 365.25.  The derived classes may
    /// implemented different logic based on their own business day convention.
    /// </remarks>
    protected virtual double GetTime(Dt start, Dt end)
    {
      return Dt.RelativeTime(start, end);
    }

    /// <summary>
    /// Gets the Black-Scholes model parameters for the specified forward date.
    /// </summary>
    /// <param name="date">The forward date.</param>
    /// <returns>The Black-Scholes model parameters.</returns>
    /// <remarks>
    /// The derived classes must implement this method for their specific products.
    /// </remarks>
    public abstract IBlackScholesParameterData GetParameters(Dt date);

    /// <summary>
    /// Gets or sets the as-of date.
    /// </summary>
    /// <value>The as-of date.</value>
    public Dt AsOf { get; set; }

    #region IVolatilitySurfaceCalibrator Members

    /// <summary>
    /// Fit a surface from the specified tenor points.
    /// </summary>
    /// <param name="surface">The volatility surface to fit.</param>
    /// <param name="fromTenorIdx">The tenor index to start fit.</param>
    /// <remarks></remarks>
    public void FitFrom(CalibratedVolatilitySurface surface, int fromTenorIdx)
    {
      var interpolator = surface.Interpolator as VolatilitySurfaceInterpolator;
      Debug.Assert(interpolator != null);
      // To fit the volatility surface, we only need to update the interpolation function.
      var fn = Fit(surface.Tenors, surface.SmileInputKind);
      if (fn == null)
      {
        throw new ToolkitException("Not enuough valid points to build surface");
      }
      interpolator.SurfaceFunction = fn;
    }

    /// <summary>
    /// Fits the surface interpolator from a list of volatility tenors.
    /// </summary>
    /// <param name="tenors">The volatility tenors.</param>
    /// <param name="inputKind">The smile input kind.</param>
    /// <returns></returns>
    private Func<double, double, SmileInputKind, double> Fit(
      IEnumerable<IVolatilityTenor> tenors,
      SmileInputKind inputKind)
    {
      Debug.Assert(tenors != null);
      if (_model == SmileModel.SplineInterpolation)
      {
        return tenors.Select(tenor => SplineSmile(tenor, inputKind))
          .BuildBlackScholesSurface(_timeInterp);
      }
      return tenors.Select(QuadraticSmile)
        .BuildBlackScholesSurface(_timeInterp);
    }

    private KeyValuePair<double, SmileFunc> QuadraticSmile(
      IVolatilityTenor tenor)
    {
      var par = GetParameters(tenor.Maturity);
      var data = tenor as ISmileDataProvider;
      if (data == null)
      {
        throw new ToolkitException("Not a Smile Data Provider");
      }
      var smile = data.GetStrikeVolatilityPairs(par)
        .BuildQuadraticRegressionSmile(par, _smileInterp);
      return new KeyValuePair<double, SmileFunc>(
        par.Time, smile.Evaluate);
    }

    private KeyValuePair<double, SmileFunc> SplineSmile(
      IVolatilityTenor tenor, SmileInputKind inputKind)
    {
      var par = GetParameters(tenor.Maturity);
      var data = tenor as ISmileDataProvider;
      if (data == null)
      {
        throw new ToolkitException("Not a Smile Data Provider");
      }
      var smile = data.GetStrikeVolatilityPairs(par)
        .BuildPlainInterpolationSmile(par, _smileInterp, inputKind);
      return new KeyValuePair<double, SmileFunc>(par.Time,
        smile == null ? null : (SmileFunc)smile.Evaluate);
    }
    #endregion

    #region IBlackScholesParameterDataProvider Members

    IBlackScholesParameterData IBlackScholesParameterDataProvider
      .GetParameters(Dt expiry)
    {
      return GetParameters(expiry);
    }

    #endregion
  }
}
