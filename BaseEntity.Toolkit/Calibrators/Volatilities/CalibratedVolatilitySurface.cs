/*
 * CalibratedVolatilitySurface.cs
 *
 *  -2012. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Calibrated unified volatility surface
  /// </summary>
  /// <remarks>
  ///   <para>A unified volatility surface containing a volatility surface representation.</para>
  ///   <para>Volatility surfaces can be specified directly or calibrated
  ///   to market data using <see cref="IVolatilitySurfaceCalibrator">Volatility calibrators</see></para>
  ///   <para>Common types of volatility calibrators include:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="Toolkit.Calibrators.RateVolatilityCapSabrCalibrator">Cap-floor SABR volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.Volatilities.FxOptionVannaVolgaCalibrator">Vanna Volga fx option volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.SwaptionVolatilityBaseCalibrator">Swaption volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.Volatilities.GenericBlackScholesCalibrator">Vanilla option Black-scholes volatility calibrator</see></description></item>
  ///   </list>
  /// </remarks>
  [Serializable]
  public class CalibratedVolatilitySurface : VolatilitySurface
  {
    #region Instance Methods

    /// <summary>
    /// constructor for the volatility surface
    /// </summary>
    /// <param name="asOf">As of date.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <param name="interpolator">The interpolator.</param>
    public CalibratedVolatilitySurface(Dt asOf,
                             IVolatilityTenor[] tenors,
                             IVolatilitySurfaceCalibrator calibrator,
                             IVolatilitySurfaceInterpolator interpolator)
      : base(asOf, interpolator, SmileInputKind.Strike)
    {
      tenors_ = tenors;
      calibrator_ = calibrator;
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="CalibratedVolatilitySurface"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <param name="kind">The kind.</param>
    /// <remarks></remarks>
    internal protected CalibratedVolatilitySurface(Dt asOf,
      IVolatilityTenor[] tenors,
      IVolatilitySurfaceCalibrator calibrator,
      IVolatilitySurfaceInterpolator interpolator,
      SmileInputKind kind)
      : base(asOf, interpolator, kind)
    {
      tenors_ = tenors;
      calibrator_ = calibrator;
    }

    /// <summary>
    /// Return a new object with a deep copy of all the tenor data.
    /// </summary>
    /// <returns>Cloned object</returns>
    /// <remarks>
    /// This method does not clone the calibrator and interpolator.
    /// The user must make sure the later is update.
    /// </remarks>
    public override object Clone()
    {
      var obj = (CalibratedVolatilitySurface)base.Clone();
      obj.tenors_ = CloneUtil.Clone(tenors_);
      // Following calibrated curve convention,
      // Calibrator and interpolator are NOT cloned.
      return obj;
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is meta data driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (tenors_ != null && tenors_.Length != 0)
      {
        foreach (IVolatilityTenor tenor in tenors_)
        {
          tenor.Validate(errors);
        }
      }
    }

    /// <summary>
    /// Fits this instance.
    /// </summary>
    public void Fit()
    {
      if (calibrator_ != null)
        calibrator_.FitFrom(this, 0);
    }

    #endregion

    #region Factory methods

    /// <summary>
    ///  Create a volatility surface from a flat volatility.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="sigma">The volatility value.</param>
    /// <returns>The volatility surface</returns>
    public static CalibratedVolatilitySurface FromFlatVolatility(Dt asOf, double sigma)
    {
      // For flat volatility, the actual asOf date does not matter but it has to be nonempty.
      if (asOf.IsEmpty()) asOf = Dt.Today();
      return new CalibratedVolatilitySurface(asOf,
                                   new[]
                                     {
                                       new PlainVolatilityTenor(asOf.ToString(), asOf)
                                         {
                                           Volatilities = new[] {sigma}
                                         }
                                     },
                                   null, new VolatilityPlainInterpolator());
    }

    /// <summary>
    /// Create a volatility surface from a volatility curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The volatility surface</returns>
    public static CalibratedVolatilitySurface FromCurve(Curve curve)
    {
      if (curve == null)
      {
        throw new ToolkitException("Curve cannot be empty");
      }
      var curveTenors = GetCurveTenors(curve);
      Dt asOf = curve.AsOf;
      int count = curve.Count;
      var tenors = new IVolatilityTenor[count];
      for (int i = 0; i < count; ++i)
      {
        var dt = curve.GetDt(i);
        tenors[i] = new PlainVolatilityTenor(GetTenorName(curveTenors, i, dt, asOf), dt)
        {
          Volatilities = new[] { curve.GetVal(i) }
        };
      }
      var calibrator = new CurveWrapper(curve);
      return new CalibratedVolatilitySurface(asOf, tenors, calibrator, calibrator);
    }

    private static IList<CurveTenor> GetCurveTenors(Curve curve)
    {
      var ccurve = curve as CalibratedCurve;
      if (ccurve == null || ccurve.Tenors == null
        || ccurve.Tenors.Count != ccurve.Count)
      {
        return null;
      }
      return ccurve.Tenors;
    }

    private static string GetTenorName(
      IList<CurveTenor> tenors, int index, Dt date, Dt asOf)
    {
      var name = tenors != null ? tenors[index].Name : null;
      return String.IsNullOrEmpty(name)
        ? Tenor.FromDateInterval(asOf, date).ToString()
        : name;
    }
    #endregion

    #region Properties

    /// <summary>
    /// Gets the volatility tenors.
    /// </summary>
    /// <value>The tenors.</value>
    public IVolatilityTenor[] Tenors
    {
      get { return tenors_; }
    }

    /// <summary>
    /// Gets the calibrator.
    /// </summary>
    public IVolatilitySurfaceCalibrator Calibrator
    {
      get { return calibrator_; }
    }

    #endregion

    #region data

    private readonly IVolatilitySurfaceCalibrator calibrator_;
    private IVolatilityTenor[] tenors_;

    #endregion

    #region Private types

    [Serializable]
    private class CurveWrapper : IVolatilitySurfaceInterpolator, IVolatilitySurfaceCalibrator
    {
      private readonly Curve curve_;

      internal CurveWrapper(Curve curve)
      {
        if (curve == null)
        {
          throw new ToolkitException("Curve cannot be null");
        }
        curve_ = curve;
      }

      #region IVolatilityInterpolator Members

      double IVolatilitySurfaceInterpolator.Interpolate(
        VolatilitySurface surface, Dt date, double strike)
      {
        return curve_.Interpolate(date);
      }

      #endregion

      #region IVolatilitySurfaceCalibrator Members

      void IVolatilitySurfaceCalibrator.FitFrom(
        CalibratedVolatilitySurface surface, int fromTenorIdx)
      {
        curve_.Clear();

        var tenors = surface.Tenors;
        int count = tenors.Length;
        if (count == 0)
        {
          return;
        }
        for (int i = 0; i < count; ++i)
        {
          var tenor = tenors[i] as PlainVolatilityTenor;
          if (tenor == null)
          {
            throw new ToolkitException("Not a VolatilityTenor");
          }
          IList<double> tvs = tenor.QuoteValues;
          if (tvs == null || tvs.Count == 0)
          {
            continue;
          }
          if (tvs.Count != 1)
          {
            throw new ToolkitException(String.Format("Tenor {0} is not flat", i));
          }
          curve_.Add(tenor.Maturity, tvs[0]);
        }
      }

      #endregion
    } // class CurveWrapper

    #endregion
  }
}
