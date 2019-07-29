/*
 * VolCubeInterpolator.cs
 *
 *   2010. All rights reserved.
 *
 */
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  ///<summary>
  /// Interpolator for searching volatility data out of 3-d cube
  ///</summary>
  [Serializable]
  public class VolCubeInterpolator : ISwapVolatilitySurfaceInterpolator
  {
    ///<summary>
    /// Default constructor
    ///</summary>
    public VolCubeInterpolator()
    {
      strikeInterp_ = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Smooth, -10.0, 10.0);
      timeInterp_ = InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Smooth, -10.0, 10.0);
    }

    ///<summary>
    /// Constructor specifying interp schema on strike dimension and time dimension
    ///</summary>
    ///<param name="strikeInterp">The interpolation schema for strike dimension</param>
    ///<param name="timeInterp">The interpolation schema for expiration and swaption maturity dimension</param>
    public VolCubeInterpolator(Interp strikeInterp, Interp timeInterp) : this()
    {
      strikeInterp_ = strikeInterp;
      timeInterp_ = timeInterp;
    }

    /// <summary>
    /// Interpolates a volatility at given expiration date and fwdDuration based on the ATM surface.
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiryDt">The expiry date.</param>
    /// <param name="fwdDuration">The maturity/strike.</param>
    /// <returns>The volatility at the given date and maturity/strike.</returns>
    public double Interpolate(RateVolatilitySurface surface, Dt expiryDt, double fwdDuration)  // ATM surface interpolation
    {
      var cube = surface as SwaptionVolatilityCube;
      var spline = cube != null ? cube.Skew : (surface as SwaptionVolatilitySpline);
      if (spline == null)
        throw new ArgumentException("SwaptionVolatilitySpline type is expected");

      var expiry = Dt.Diff(spline.AsOf, expiryDt)/365.0;
      var expiryBucket = VolatilityLookupBucket.FindBucket(spline.ExpiryAxis.ToArray(), expiry);
      var tenorBucket = VolatilityLookupBucket.FindBucket(spline.TenorAxis.ToArray(), fwdDuration);
      var match1 = spline.SearchCubeData(expiryBucket.StartPoint, tenorBucket.StartPoint, 0);
      var match3 = spline.SearchCubeData(expiryBucket.EndPoint, tenorBucket.StartPoint, 0);
      var match2 = spline.SearchCubeData(expiryBucket.StartPoint, tenorBucket.EndPoint, 0);
      var match4 = spline.SearchCubeData(expiryBucket.EndPoint, tenorBucket.EndPoint, 0);
      var expiryval1 = spline.ExpiryAxis[expiryBucket.StartPoint];
      var expiryval2 = spline.ExpiryAxis[expiryBucket.EndPoint];
      var interp1 = 0.0;
      var interp2 = 0.0;

      var tenorval1 = spline.TenorAxis[tenorBucket.StartPoint];
      var tenorval2 = spline.TenorAxis[tenorBucket.EndPoint];

      interp1 = tenorBucket.ExactMatchFound
                  ? match2
                  :
                    new Interpolator(timeInterp_, new double[] {tenorval1, tenorval2}, new double[] {match1, match2})
                      .evaluate(fwdDuration);

      interp2 = tenorBucket.ExactMatchFound
                  ? match4
                  : new Interpolator(timeInterp_, new double[] {tenorval1, tenorval2}, new double[] {match3, match4}).
                      evaluate(fwdDuration);


      return expiryBucket.ExactMatchFound
               ? interp2
               : new Interpolator(timeInterp_, new double[] {expiryval1, expiryval2}, new double[] {interp1, interp2})
                   .evaluate(expiry);
    }


    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified cube.
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiryDt">The date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The forward maturity.</param>
    /// <returns>The volatility.</returns>
    public double Interpolate(RateVolatilitySurface surface, Dt expiryDt, double strike, double maturity)
    {
      var cube = surface as SwaptionVolatilityCube;
      var spline = cube != null ? cube.Skew : (surface as SwaptionVolatilitySpline);
      if (spline == null)
        throw new ArgumentException("SwaptionVolatilitySpline type is expected");

      if (spline.DataView.Data == null || spline.DataView.Data.Length == 0 || !(spline.DataView.Data.Any(tenor => tenor.Strikes.Count >0)))
        return 0.0;

      var strikeBucket = VolatilityLookupBucket.FindBucket(spline.Strikes, strike);
      var expiry = Dt.Diff(spline.AsOf, expiryDt)/365.0;
      var expiryBucket = VolatilityLookupBucket.FindBucket(spline.ExpiryAxis.ToArray(), expiry);
      var tenorBucket = VolatilityLookupBucket.FindBucket(spline.TenorAxis.ToArray(), maturity);

      var tenorval1 = spline.TenorAxis[tenorBucket.StartPoint];
      var tenorval2 = spline.TenorAxis[tenorBucket.EndPoint];

      var expiryval1 = spline.ExpiryAxis[expiryBucket.StartPoint];
      var expiryval2 = spline.ExpiryAxis[expiryBucket.EndPoint];

      var match1 = spline.SearchCubeData(expiryBucket.StartPoint, tenorBucket.StartPoint, strikeBucket.StartPoint);
      var match2 = spline.SearchCubeData(expiryBucket.StartPoint, tenorBucket.StartPoint, strikeBucket.EndPoint);
      var interp1 = strikeBucket.ExactMatchFound
                      ? match2
                      :
                        new Interpolator(strikeInterp_,
                                         new[]
                                           {
                                             spline.Strikes[strikeBucket.StartPoint],
                                             spline.Strikes[strikeBucket.EndPoint]
                                           }, new[] {match1, match2})
                          .evaluate(strike);

      var match3 = spline.SearchCubeData(expiryBucket.StartPoint, tenorBucket.EndPoint, strikeBucket.StartPoint);
      var match4 = spline.SearchCubeData(expiryBucket.StartPoint, tenorBucket.EndPoint, strikeBucket.EndPoint);
      var interp2 = strikeBucket.ExactMatchFound
                      ? match4
                      : new Interpolator(strikeInterp_,
                                         new[]
                                           {
                                             spline.Strikes[strikeBucket.StartPoint],
                                             spline.Strikes[strikeBucket.EndPoint]
                                           }, new[] {match3, match4})
                          .evaluate(strike);
      var match5 =  spline.SearchCubeData(expiryBucket.EndPoint, tenorBucket.StartPoint, strikeBucket.StartPoint);
      var match6 = spline.SearchCubeData(expiryBucket.EndPoint, tenorBucket.StartPoint, strikeBucket.EndPoint);
      var interp3 = strikeBucket.ExactMatchFound
                      ? match6
                      :
                        new Interpolator(strikeInterp_,
                                         new[]
                                           {
                                             spline.Strikes[strikeBucket.StartPoint],
                                             spline.Strikes[strikeBucket.EndPoint]
                                           }, new[] {match5, match6})
                          .evaluate(strike);
      var match7 = spline.SearchCubeData(expiryBucket.EndPoint, tenorBucket.EndPoint, strikeBucket.StartPoint);
      var match8 = spline.SearchCubeData(expiryBucket.EndPoint, tenorBucket.EndPoint, strikeBucket.EndPoint);
      var interp4 = strikeBucket.ExactMatchFound
                      ? match8
                      : new Interpolator(strikeInterp_,
                                         new[]
                                           {
                                             spline.Strikes[strikeBucket.StartPoint],
                                             spline.Strikes[strikeBucket.EndPoint]
                                           }, new[] {match7, match8})
                          .evaluate(strike);


      var interp5 = tenorBucket.ExactMatchFound
                      ? interp2
                      : new Interpolator(timeInterp_,
                                         new[] {tenorval1 ,tenorval2}, new[] {interp1, interp2}).evaluate(maturity);
      var interp6 = tenorBucket.ExactMatchFound
                      ? interp4
                      : new Interpolator(timeInterp_,
                                         new[]{tenorval1,tenorval2}, new[] {interp3, interp4}).evaluate(maturity);
      return expiryBucket.ExactMatchFound
               ? interp6
               : new Interpolator(timeInterp_,
                                  new[]{expiryval1,expiryval2 }, new[] {interp5, interp6}).evaluate(expiry);

    }


    private Interp strikeInterp_ = new Tension();
    private Interp timeInterp_ = new Linear(new Const(), new Const());


    #region IVolatilitySurfaceInterpolator Members

    double IVolatilitySurfaceInterpolator.Interpolate(VolatilitySurface surface, Dt expiry, double strike)
    {
      return Interpolate(surface as RateVolatilitySurface, expiry, strike);
    }

    #endregion
  }
}
