/*
 * RateVolatilityATMCapCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Calibrate caplet ForwardVolatilityCube <m>\Sigma(t,T,K)</m> from a set of ATM Cap quotes.
  /// </summary>
  [Serializable]
  public class RateVolatilityATMCapCalibrator : RateVolatilityCalibrator, IVolatilityTenorsProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor 
    /// </summary>
    /// <param name = "asOf">as of date</param>
    /// <param name = "settle">Settle Date </param>
    /// <param name = "discountCurve">discount curve</param>
    /// <param name = "projectionCurveSelector">projection curve (possibly one for each cap maturity)</param>
    /// <param name = "projectionIndexSelector">projection index (possibly one for each cap maturity)</param>
    /// <param name = "capStrikes">cap Strikes</param>
    /// <param name = "capMaturities">cap Maturities</param>
    /// <param name = "capVols">cap Volatilities </param>
    /// <param name = "lambda">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m></param>
    /// <param name = "volatilityType">volatility Type</param>
    /// <remarks>If more than one reference index is provided (i.e. 3M Libor and 6M Libor) then the calibrated index is that corresponding to the shortest tenor.
    /// The volatility of caplets with longer tenor is computed by standard freezing arguments from the relationship <m>1 + \Delta L^{\Delta} = \Pi_i (1 +\delta_i L_i^{\delta_i}) + B</m>
    /// where B is a deterministic basis and <m>L_i</m> are assumed perfectly correlated.
    /// </remarks>
    public RateVolatilityATMCapCalibrator(
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      Func<Dt, InterestRateIndex> projectionIndexSelector,
      Func<Dt, DiscountCurve> projectionCurveSelector,
      double[] capStrikes,
      Dt[] capMaturities,
      double[] capVols,
      double lambda,
      VolatilityType volatilityType)
      : this(asOf, settle, discountCurve, projectionIndexSelector, projectionCurveSelector,
         capStrikes, null, capMaturities, capVols, lambda, volatilityType)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="RateVolatilityATMCapCalibrator"/> class.
    /// </summary>
    /// <param name="asOf">As of date.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="projectionIndexSelector">The projection index selector.</param>
    /// <param name="projectionCurveSelector">The projection curve selector.</param>
    /// <param name="capStrikes">The cap strikes.</param>
    /// <param name="capTenors">The cap tenor names.</param>
    /// <param name="capMaturities">The cap maturities.</param>
    /// <param name="capVols">The cap vols.</param>
    /// <param name="lambda">The lambda.</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <exception cref="ToolkitException">cap tenors and expiries not match</exception>
    public RateVolatilityATMCapCalibrator(
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      Func<Dt, InterestRateIndex> projectionIndexSelector,
      Func<Dt, DiscountCurve> projectionCurveSelector,
      double[] capStrikes,
      string[] capTenors,
      Dt[] capMaturities,
      double[] capVols,
      double lambda,
      VolatilityType volatilityType)
      : base(asOf, discountCurve, TargetCurve(capMaturities, projectionIndexSelector, projectionCurveSelector), TargetIndex(capMaturities, projectionIndexSelector), volatilityType, null, null, capStrikes)
    {
      if (capTenors == null || capTenors.Length == 0)
        _capTenors = null;
      else if (capTenors.Length != capMaturities.Length)
        throw new ToolkitException("cap tenors and expiries not match");
      else
        _capTenors = capTenors;

      CapVolatilities = capVols;
      Settle = settle;
      CapMaturities = capMaturities;
      Lambda = lambda;
      Expiries = GenerateIntermediateMaturities(capMaturities);
      Dates = Expiries;
      if (capMaturities.Select(projectionIndexSelector).Distinct().Count() > 1)
      {
        ProjectionCurves = new Dictionary<Dt, Tuple<InterestRateIndex, DiscountCurve>>();
        foreach (var dt in capMaturities)
          ProjectionCurves[dt] = new Tuple<InterestRateIndex, DiscountCurve>(projectionIndexSelector(dt), projectionCurveSelector(dt));
      }
    }

    #endregion Constructors

    #region Methods
   
    /// <summary>
    /// Validate
    /// </summary>
    /// <param name = "errors">ArrayList for errors</param>
     public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      for (int i = 0; i < Strikes.Length; i++)
      {
        if (Strikes[i] <= 0)
          InvalidValue.AddError(errors, this,
                                String.Format("Cap Strikes have to be greater than 0 and not {0}", Strikes[i]));
        if (CapVolatilities[i] <= 0)
          InvalidValue.AddError(errors, this,
                                String.Format("Cap Volatilities have to be greater than 0 and not {0}", CapVolatilities[i]));
      }
    }

    /// <summary>
    /// Fit the cube
    /// </summary>
    /// <param name="surface">The cube.</param>
    /// <param name="fromIdx">Start index</param>
    public override void FitFrom(CalibratedVolatilitySurface surface, int fromIdx)
    {
      Curve curve;
      try
      {
        double[] fitErrors;
        CapletVolatilitiesBootstrapper.BootstrapAtmCapletCurve(Settle, CapVolatilities, CapMaturities, Strikes, DiscountCurve,
                                                               dt =>
                                                               (ProjectionCurves == null)
                                                                 ? new Tuple<InterestRateIndex, DiscountCurve>(RateIndex, RateProjectionCurve)
                                                                 : ProjectionCurves[dt], VolatilityType, Lambda, out curve, out fitErrors);
      }
      catch
      {
        throw new ToolkitException(
          String.Format(
            "Unable to bootstrap ATM Volatility surface with smoothing parameter {0}",
            Lambda));
      }

      // Read bootstrapped curves into fwd-fwd vol cube
      Dt[] dates = ArrayUtil.Generate(Expiries.Length, i => i == 0 ? Settle : Expiries[i - 1]);
      var fwdVols = ((RateVolatilityCube)surface).FwdVols;
      for (int i = 0; i < Expiries.Length; i++)
      {
        double vol = curve.Interpolate(Expiries[i]);
        for (int j = 0; j < Strikes.Length; j++)
        {
          for (int k = 0; k < dates.Length; k++)
          {
            fwdVols.AddVolatility(k, i, j, vol);
          }
        }
      }
    }

    internal Dt[] GenerateIntermediateMaturities(Dt[] input)
    {
      var result = new Dt[2*input.Length - 1];
      for (int i = 0; i < input.Length; i++) result[2*i] = input[i];
      for (int i = 2; i <= input.Length; i++)
      {
        int diff = Dt.Diff(result[2*i - 4], result[2*i - 2]);
        int daysToAdd = (diff/2);
        result[2*i - 3] = Dt.AddDays(result[2*i - 4], daysToAdd, Calendar.None);
      }
      return result;
    }

    /// <summary>
    /// Interpolated Caplet volatility (square root of the integrated forward caplet vol) for a given expiry and strike
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiryDate">The expiry date.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    public override double Interpolate(CalibratedVolatilitySurface surface,
      Dt expiryDate, double forwardRate, double strike)
    {
      return CalcCapletVolatilityFromCube(surface, expiryDate, strike);
    }

    private static DiscountCurve TargetCurve(Dt[] capMaturities, Func<Dt, InterestRateIndex> indexSelector, Func<Dt, DiscountCurve> curveSelector)
    {
      if (capMaturities == null || capMaturities.Length == 0)
        return null;
      var index = indexSelector(capMaturities[0]);
      var curve = curveSelector(capMaturities[0]);
      for (int i = 0; ++i < capMaturities.Length; )
      {
        var pi = indexSelector(capMaturities[i]);
        var pc = curveSelector(capMaturities[i]);
        if (pi.IndexTenor.Days < index.IndexTenor.Days)
        {
          index = pi;
          curve = pc;
        }
      }
      return curve;
    }

    private static InterestRateIndex TargetIndex(Dt[] capMaturities, Func<Dt, InterestRateIndex> selector)
    {
      if (capMaturities == null || capMaturities.Length == 0)
        return selector(Dt.Empty);
      var retVal = selector(capMaturities[0]);
      for (int i = 0; ++i < capMaturities.Length; )
      {
        var pi = selector(capMaturities[i]);
        if (pi.IndexTenor.Days < retVal.IndexTenor.Days)
          retVal = pi;
      }
      return retVal;
    }

    #endregion Methods

    #region Properties
   
    /// <summary>
    ///   Settle Date for the calibrating securities
    /// </summary>
    public Dt Settle{ get; private set;}
    /// <summary>
    /// Quoted cap vols
    /// </summary>
    public double[] CapVolatilities { get; private set; }

    /// <summary>
    /// Underlying cap maturities
    /// </summary>
    public Dt[] CapMaturities { get; private set; }

    /// <summary>
    /// Smoothing penalty parameter (0.0 = no penalty on curvature) 
    /// </summary>
    public double Lambda { get; private set; }

    /// <summary>
    /// Projection curves for each underlying cap maturity underlying tenors (i.e. 3M, 6M). 
    /// </summary>
    private Dictionary<Dt, Tuple<InterestRateIndex, DiscountCurve>> ProjectionCurves { get; set; }
    
    #endregion Properties

    #region Data

    private readonly string[] _capTenors;

    #endregion

    #region IVolatilityTenorsProvider Members

    IEnumerable<IVolatilityTenor> IVolatilityTenorsProvider.EnumerateTenors()
    {
      return EnumerateTenors(AsOf, CapVolatilities, CapMaturities, _capTenors);
    }

    #endregion
  }
}