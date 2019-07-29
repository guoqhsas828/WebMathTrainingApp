/*
 * RateVolatilityCube.cs
 *
 *   2005-2010. All rights reserved.
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Derived class that holds extra default SABR fit settings
  /// </summary>
  [Serializable]
  public class RateVolatilityFitSettings : VolatilityFitSettings
  {
    /// <summary>
    /// Default Constructor.
    /// </summary>
    public RateVolatilityFitSettings()
    {
    }

    /// <summary>
    /// Blank Settings
    /// </summary>
    public RateVolatilityFitSettings(Dt asOf)
    {
      FitToMarket = 0.2;
      SabrBounds = new Bounds<AlphaBetaRhoNu>(
        new AlphaBetaRhoNu(0.001, 0.35, -0.9, 0.001),
        new AlphaBetaRhoNu(0.1, 0.35, 0.5, 0.7));
      SabrBeta = new Curve(asOf, 0.35);
      SabrAlpha = null;
      SabrRho = null;
      SabrNu = null;
    }

    private static double[] GetAlphaRohNu(AlphaBetaRhoNu data)
    {
      return !data.HasValue ? null : new[] {data.Alpha, data.Rho, data.Nu};
    }

    private static AlphaBetaRhoNu SetAlphaRohNu(AlphaBetaRhoNu data, double[] v)
    {
      var beta = data.Beta;
      return new AlphaBetaRhoNu(v[0], beta, v[1], v[2]);
    }

    /// <summary>Fit To Market parameter ( number between 0 and 1)  </summary>
    public double FitToMarket { get; set; }

    /// <summary>Lower Bounds of the parameters (Alpha,Rho,Nu)</summary>
    public double[] SabrLowerBounds
    {
      get { return GetAlphaRohNu(SabrBounds.Lower); }
      set
      {
        SabrBounds = new Bounds<AlphaBetaRhoNu>(
          SetAlphaRohNu(SabrBounds.Lower, value), SabrBounds.Upper);
      }
    }

    /// <summary>Upper Bounds on the parameters  (Alpha,Rho,Nu)</summary>
    public double[] SabrUpperBounds
    {
      get { return GetAlphaRohNu(SabrBounds.Upper); }
      set
      {
        SabrBounds = new Bounds<AlphaBetaRhoNu>(
          SabrBounds.Lower, SetAlphaRohNu(SabrBounds.Upper, value));
      }
    }
  }

  /// <summary>
  ///  Common interface of rate volatility surface
  /// </summary>
  [Serializable]
  public abstract class RateVolatilitySurface : CalibratedVolatilitySurface, IVolatilityObject
  {
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="vol">The vol.</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    internal RateVolatilitySurface(Dt asOf, double vol, VolatilityType volatilityType)
      : base(asOf, new[]
      {
        new PlainVolatilityTenor(asOf.ToString(), asOf)
        {
          Volatilities = new[] {vol}
        }
      },
        null, new VolatilityPlainInterpolator())
    {
      _volatilityType = GetDistribution(volatilityType);
    }

    /// <summary>
    /// constructor for the volatility surface
    /// </summary>
    /// <param name="tenors">The tenors.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <param name="interpolator">The interpolator.</param>
    protected RateVolatilitySurface(
      RateVolatilityCalibrator calibrator,
      IVolatilityTenor[] tenors,
      IVolatilitySurfaceInterpolator interpolator)
      : base(calibrator.AsOf, tenors, calibrator, interpolator)
    {
      _volatilityType = GetDistribution(calibrator.VolatilityType);
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>By default validation is meta data driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.</remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      if (!(Calibrator is RateVolatilityCalibrator))
      {
        InvalidValue.AddError(errors, this, "Calibrator must be RateVolatilityCalibrator");
      }
      if (Tenors == null)
      {
        InvalidValue.AddError(errors, this, "Tenors cannot be null");
      }
      base.Validate(errors);
    }

    /// <summary>
    /// The calibrator.
    /// </summary>
    public RateVolatilityCalibrator RateVolatilityCalibrator
    {
      get { return (RateVolatilityCalibrator)Calibrator; }
    }

    /// <summary>
    /// Gets or sets the interpolator.
    /// </summary>
    public IVolatilitySurfaceInterpolator RateVolatilityInterpolator { get; set; }

    /// <summary>
    /// Gets the type of the underlying distribution.
    /// </summary>
    /// <value>
    /// The type of the distribution.
    /// </value>
    public DistributionType DistributionType
    {
      get { return _volatilityType; }
    }

    /// <summary>
    /// Gets the type of the volatility.
    /// </summary>
    public VolatilityType VolatilityType
    {
      get { return GetVolatilityType(_volatilityType); }
    }

    internal static DistributionType GetDistribution(VolatilityType volatilityType)
    {
      if (volatilityType == VolatilityType.Normal)
        return DistributionType.Normal;
      if (volatilityType == VolatilityType.LogNormal)
        return DistributionType.LogNormal;
      throw new ToolkitException("Invalid VolatilityType {0}", volatilityType);
    }

    private static VolatilityType GetVolatilityType(DistributionType distribution)
    {
      switch (distribution)
      {
        case DistributionType.LogNormal:
        case DistributionType.ShiftedLogNormal:
          return VolatilityType.LogNormal;
        case DistributionType.Normal:
          return VolatilityType.Normal;
      }
      throw new ToolkitException("Invalid DistributionType {0}", distribution);
    }

    /// <summary>
    /// Calculates the volatility for the given cap.
    /// </summary>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="cap">The cap.</param>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double CapVolatility(DiscountCurve discountCurve, Cap cap)
    {
      Dt settle = Cap.StandardSettle(AsOf, 2, cap.Calendar);
      var capFloorPricer = new CapFloorPricer(cap, AsOf, settle, discountCurve, discountCurve, this);
      var capValuations = capFloorPricer.CapPv();
      double pv = capFloorPricer.EffectiveNotional * capValuations.Item1;
      if (Math.Abs(pv) <= 1e-12)
        return capValuations.Item2.Min();
      else
      {
        return capFloorPricer.ImpliedVolatility(pv);
      }
    }

    /// <summary>
    /// Compute zeroth order approx. of swaption vol assuming libor rates are perfectly correlated and using ATM caplet vols.
    /// </summary>
    /// <param name="expiry">Expiry</param>
    /// <param name="maturity">Maturity</param>
    /// <returns>Swaption volatility</returns>
    internal double SwaptionVolatilityFromAtmVols(Dt expiry, Dt maturity)
    {
      if (expiry >= maturity)
        return 0.0;
      var isLogNormal = VolatilityType == VolatilityType.LogNormal;
      var interp = new Flat(1.0);
      var dc = RateVolatilityCalibrator.RateProjectionCurve;
      var wi = new List<double>(); //wi
      var fi = new List<double>(); //forward rates
      var vi = new List<Curve>(); //instantaneous vols of forward rates
      var denominator = 0.0;
      var tenor = RateVolatilityCalibrator.RateIndex.IndexTenor.IsEmpty
                    ? new Tenor(6, TimeUnit.Months)
                    : RateVolatilityCalibrator.RateIndex.IndexTenor;
      double delta = tenor.Years;
      Dt next = expiry;
      for (; ; )
      {
        Dt prev = next;
        next = Dt.Add(prev, tenor);
        var df = dc.Interpolate(next);
        var f = dc.F(prev, next);
        wi.Add(delta * df);
        fi.Add(f);
        denominator += delta * df;
        var v = new Curve(AsOf) { Interp = interp };
        foreach (var dt in RateVolatilityCalibrator.Dates)
        {
          if (dt >= expiry)
          {
            v.Add(expiry, ForwardVolatility(expiry, prev, f));
            break;
          }
          v.Add(dt, ForwardVolatility(dt, prev, f));
        }
        vi.Add(v);
        if (next >= maturity)
          break;
      }
      var swapRate = isLogNormal ? 0.0 : 1.0;
      var qv = 0.0;
      double t = Dt.FractDiff(AsOf, expiry);
      for (int i = 0; i < wi.Count; ++i)
      {
        wi[i] /= denominator;
        if (isLogNormal) swapRate += wi[i] * fi[i];
        for (int j = 0; j <= i; ++j)
        {
          double cov = Curve.Integral(AsOf, expiry, vi[i], vi[j]);
          if (isLogNormal) cov *= fi[i] * fi[j];
          qv += (i == j) ? (wi[i] * wi[j] * cov) : (2 * wi[i] * wi[j] * cov);
        }
      }
      if (qv <= 0.0)
        return 0.0;
      return Math.Sqrt(qv / (swapRate * swapRate * t));
    }

    /// <summary>
    ///  Calculate the forward volatility.
    /// </summary>
    /// <param name="fwdDate">The forward start date.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The forward volatility.</returns>
    public double ForwardVolatility(Dt fwdDate, Dt expiry, double strike)
    {
      if (fwdDate > expiry)
        return 0.0;
      if (RateVolatilityInterpolator != null)
        return RateVolatilityInterpolator.Interpolate(this, expiry, strike);
      var cube = this as RateVolatilityCube;
      if (cube != null)
        return cube.FwdVols.Interpolate(fwdDate, expiry, strike);
      throw new NullReferenceException("Null volatility interpolator");
    }

    private readonly DistributionType _volatilityType;
  }

  /// <summary>
  /// Interest rate volatility cube.
  /// </summary>
  [Serializable]
  public class RateVolatilityCube : RateVolatilitySurface, IModelParameter
  {
    #region Constructors

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="vol">The vol.</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    public RateVolatilityCube(Dt asOf, double vol, VolatilityType volatilityType)
      : base(asOf, vol, volatilityType)
    {
      fwdVols_ = new ForwardVolatilityCube(asOf, vol);
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="calibrator">The calibrator.</param>
    public RateVolatilityCube(RateVolatilityCalibrator calibrator)
      : this(calibrator, GetTenors(calibrator), calibrator)
    {
      fwdVols_ = new ForwardVolatilityCube(calibrator.AsOf, calibrator.Dates, calibrator.Expiries, calibrator.Strikes);
    }

    private static IVolatilityTenor[] GetTenors(RateVolatilityCalibrator calibrator)
    {
      var provider = calibrator as IVolatilityTenorsProvider;
      return provider != null
        ? provider.EnumerateTenors().Cast<IVolatilityTenor>().ToArray()
        : null;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="calibrator">The calibrator.</param>
    /// <param name="tenors">The tenors</param>
    /// <param name="interpolator">The interpolator</param>
    public RateVolatilityCube(RateVolatilityCalibrator calibrator, IVolatilityTenor[] tenors,
                              IVolatilitySurfaceInterpolator interpolator)
      : base(calibrator, tenors, interpolator)
    {
      fwdVols_ = new ForwardVolatilityCube(calibrator.AsOf,
                                           calibrator.Dates, calibrator.Expiries, calibrator.Strikes);
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>By default validation is meta data driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.</remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      if (Name == null) Name = Description;
      base.Validate(errors);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets as of.
    /// </summary>
    /// <value>As of.</value>
    public Dt AsOf
    {
      get { return fwdVols_.Native.AsOf; }
      set { fwdVols_.Native.AsOf = value; }
    }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public string Description
    {
      get { return fwdVols_.Description; }
      set { fwdVols_.Description = value; }
    }

    /// <summary>
    /// Gets or sets the expiry tenors.
    /// </summary>
    /// <value>The expiry tenors.</value>
    public double[] Strikes
    {
      get { return fwdVols_.Strikes; }
    }

    /// <summary>
    /// Gets or sets the expiry dates.
    /// </summary>
    /// <value>The expiry tenors.</value>
    public Dt[] Expiries
    {
      get { return fwdVols_.Expiries; }
    }

    /// <summary>
    /// Gets or sets the expiry dates.
    /// </summary>
    /// <value>The expiry tenors.</value>
    public Dt[] Dates
    {
      get { return fwdVols_.Dates; }
    }

    /// <summary>
    /// Gets or sets the expiry tenors.
    /// </summary>
    /// <value>The expiry tenors.</value>
    public Tenor[] ExpiryTenors
    {
      get { return fwdVols_.ExpiryTenors; }
      set { fwdVols_.ExpiryTenors = value; }
    }

    public ForwardVolatilityCube FwdVols
    {
      get { return fwdVols_; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the volatility value at the specified date, expiry and strike..
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="expiryIndex">Index of the expiry.</param>
    /// <param name="strikeIndex">Index of the strike.</param>
    /// <returns>Volatility</returns>
    public double GetValue(int dateIndex, int expiryIndex, int strikeIndex)
    {
      return fwdVols_.GetValue(dateIndex, expiryIndex, strikeIndex);
    }

    //TODO: The following two functions are used in the Tests only.
    //  A good design should allow the use to specify interp/extrap
    //  methods instead of this working around.
    public void SetIsFlatCube(bool set)
    {
      fwdVols_.Native.SetIsFlatCube(set);
    }

    public void SetFirstCapIdx(int[] idx)
    {
      fwdVols_.Native.SetFirstCapIdx(idx);
    }

    //Map caplet vol by freezing and assuming perfect correlation among component rates
    private double MapCapletVolatility(Dt expiry, double standardVol, double strike, ReferenceIndex referenceIndex)
    {
      int compoundingPeriods;
      if (IsFlat() || (RateVolatilityCalibrator.RateIndex == null) || (referenceIndex == null) || referenceIndex.Equals(RateVolatilityCalibrator.RateIndex) ||
          ((compoundingPeriods = (int)Math.Ceiling(referenceIndex.IndexTenor.Years / RateVolatilityCalibrator.RateIndex.IndexTenor.Years)) < 2))
        return standardVol;
      bool normal = (DistributionType == DistributionType.Normal);
      double s = 0.0, f = normal ? 0.0 : 1.0;
      Dt next = expiry;
      for (int i = 0; i < compoundingPeriods; ++i)
      {
        Dt prev = next;
        next = Dt.Add(prev, RateVolatilityCalibrator.RateIndex.IndexTenor);
        double l = RateVolatilityCalibrator.RateProjectionCurve.F(prev, next, RateVolatilityCalibrator.RateIndex.DayCount, Frequency.None);
        double v = CapletVolatility(prev, l, strike);
        double delta = Dt.Fraction(prev, next, RateVolatilityCalibrator.RateIndex.DayCount);
        double factor = (1.0 + delta * l);
        s += normal ? delta * v / factor : delta * v * l / factor;
        f = normal ? f + delta : f * factor;
      }
      return normal ? s / f : f / (f - 1.0) * s;
    }

    /// <summary>
    /// Computes the Caplet Volatility if projection index tenor is different from calibrated tenor by standard Rebonato style freezing arguments and 
    /// assuming perfect correlation among libors
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="referenceIndex">The strike.</param>
    /// <returns>
    ///  Cap vol for non-standard tenor
    /// </returns>
    public double CapletVolatility(Dt expiry, double forwardRate, double strike, ReferenceIndex referenceIndex)
    {
      return MapCapletVolatility(expiry, CapletVolatility(expiry, forwardRate, strike), strike, referenceIndex);
    }

    /// <summary>
    /// Computes the Caplet Volatility if projection index tenor is different from calibrated tenor by standard Rebonato style freezing arguments and 
    /// assuming perfect correlation among libors
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="referenceIndex">The strike.</param>
    /// <returns>
    ///  Cap vol for non-standard tenor
    /// </returns>
    public double CapletVolatility(Dt expiry, double strike, ReferenceIndex referenceIndex)
    {
      return MapCapletVolatility(expiry, CapletVolatility(expiry, strike), strike, referenceIndex);
    }

    /// <summary>
    /// Computes the Caplet Volatility
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double CapletVolatility(Dt expiry, double forwardRate, double strike)
    {
      var interp = RateVolatilityInterpolator as RateVolatilityCalibrator;
      if (interp != null)
      {
        return interp.Interpolate(this, expiry, forwardRate, strike);
      }
      return fwdVols_.Native.CalcCapletVolatility(expiry, strike);
    }

    /// <summary>
    /// Computes the Caplet Volatility
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double CapletVolatility(Dt expiry, double strike)
    {
      if (RateVolatilityInterpolator != null)
      {
        return RateVolatilityInterpolator.Interpolate(this, expiry, strike);
      }
      return fwdVols_.Native.CalcCapletVolatility(expiry, strike);
    }

    /// <summary>
    /// Factory method that creates a flat forward volatility cube , from a volatility curve
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="vols">The vols.</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <returns>
    ///   <see cref="RateVolatilityCube"/>
    /// </returns>
    public static RateVolatilityCube CreateFlatVolatilityCube(Dt asOf, Dt[] expiries, double[] vols,
                                                              VolatilityType volatilityType, InterestRateIndex rateIndex)
    {
      var cube =
        new RateVolatilityCube(new RateVolatilityFlatCalibrator(asOf, expiries, volatilityType, rateIndex, vols));
      cube.Fit();
      return cube;
    }

    public bool IsFlat()
    {
      if (RateVolatilityInterpolator != null || FwdVols == null || FwdVols.Native == null)
        return false;
      var native = FwdVols.Native;
      return native.NumDates() == 1 && 
        native.NumStrikes() == 1 && native.NumExpiries() == 1;
    }
    #endregion

    #region Data

    private readonly ForwardVolatilityCube fwdVols_;

    #endregion

    #region IModelParameter Members
    /// <summary>
    /// Interpolate caplet volatility for a fixed index tenor
    /// </summary>
    /// <param name="maturity">Maturity of the rate</param>
    /// <param name="strike">Strike of the rate</param>
    /// <param name="referenceIndex">Index </param>
    /// <returns></returns>
    double IModelParameter.Interpolate(Dt maturity, double strike, ReferenceIndex referenceIndex)
    {
      if (referenceIndex is InterestRateIndex)
        return CapletVolatility(maturity, strike, referenceIndex);
      if (referenceIndex is SwapRateIndex)
      {
        if (IsFlat())
          return FwdVols.Interpolate(AsOf, maturity, strike);

        Dt swapMaturity = Dt.Add(maturity, referenceIndex.IndexTenor);
        return SwaptionVolatilityFromAtmVols(maturity, swapMaturity);
      }
      throw new ToolkitException("Cannot use volatility of {0} for {1}", RateVolatilityCalibrator.RateIndex.IndexName,
                                 referenceIndex.IndexName);
    }
    #endregion
  }
}