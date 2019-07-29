// 
//  -2012. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   The plain holder of volatility surface.
  /// </summary>
  /// <remarks>
  ///   This interpolator first performs interpolations, based on tenor
  ///   interpolator, on each tenor dates to get a sequence of volatilities
  ///   at the strike; then it performs time intepolation to get the volatility
  ///   at the given date.
  /// </remarks>
  [Serializable]
  public class VolatilityPlainHolder : IVolatilitySurfaceInterpolator, IVolatilitySurfaceCalibrator
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityPlainHolder"/> class.
    /// </summary>
    public VolatilityPlainHolder()
    {
      strikeInterp_ = new Tension();
      timeInterp_ = new Linear(new Const(), new Const());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityPlainHolder"/> class.
    /// </summary>
    /// <param name="strikeInterp">The strike interpolator.</param>
    /// <param name="timeInterp">The time interpolator.</param>
    public VolatilityPlainHolder(Interp strikeInterp, Interp timeInterp)
    {
      strikeInterp_ = strikeInterp;
      timeInterp_ = timeInterp;
    }

    #endregion Constructors

    #region IVolatilitySurfaceCalibrator Members

    /// <summary>
    /// Fit method to be overridden in the inheritors
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="idx">The idx.</param>
    public void FitFrom(CalibratedVolatilitySurface surface, int idx)
    {
      return; // no need to do anything.
    }

    #endregion

    #region IVolatilityInterpolator Members

    private double InterpolateStrike(IVolatilityTenor itenor, double strike)
    {
      var tenor = itenor as PlainVolatilityTenor;
      if (tenor == null)
      {
        throw new ToolkitException("Not VolatilityTenor");
      }
      int n = 0;
      var vols = tenor.QuoteValues;
      if (vols == null || (n = vols.Count) == 0)
      {
        throw new ToolkitException("No volatility data");
      }
      if (n == 1)
      {
        return vols[0];
      }
      return new Interpolator(strikeInterp_,
                              tenor.Strikes.ToArray(), vols.ToArray()).evaluate(strike);
    }

    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified surface.
    /// </summary>
    /// <param name="surfaceBase">The volatility surface.</param>
    /// <param name="date">The date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    /// The volatility at the given date and strike.
    /// </returns>
    public double Interpolate(VolatilitySurface surfaceBase, Dt date, double strike)
    {
      var surface = surfaceBase as CalibratedVolatilitySurface;
      if (surface == null)
      {
        throw new ToolkitException(String.Format("{0} is not a calibrated volatility surface",
                                                 surfaceBase.GetType().FullName));
      }
      var tenors = surface.Tenors;
      if (tenors == null || tenors.Length == 0)
      {
        throw new ToolkitException("No tenor");
      }
      int count = tenors.Length;
      if (count == 1)
      {
        return InterpolateStrike(tenors[0], strike);
      }
      Dt asOf = surface.AsOf;
      var x = new double[count];
      var y = new double[count];
      for (int i = 0; i < count; ++i)
      {
        var tenor = tenors[i];
        y[i] = InterpolateStrike(tenor, strike);
        x[i] = tenor.Maturity - asOf;
      }
      return new Interpolator(timeInterp_, x, y).evaluate(date - asOf);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the strike interpolator.
    /// </summary>
    /// <value>The strike interp.</value>
    public Interp StrikeInterp
    {
      get { return strikeInterp_; }
    }

    /// <summary>
    /// Gets the time interpolator.
    /// </summary>
    /// <value>The time interpolator.</value>
    public Interp TimeInterp
    {
      get { return timeInterp_; }
    }

    #endregion Properties

    #region Data

    private Interp strikeInterp_ = new Tension();
    private Interp timeInterp_ = new Linear(new Const(), new Const());

    #endregion Data
  }


}