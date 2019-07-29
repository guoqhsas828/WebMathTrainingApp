/*
 *  -2013. All rights reserved.
 */
using System;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// CapVolatilityTerm
  /// </summary>
  /// <preliminary>This class is not final and the interface is subject to changes.</preliminary>
  [Serializable]
  public class CapVolatilityUnderlying : IVolatilityUnderlying, IVolatilitySurfaceBuilder
  {
    private readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CapVolatilityUnderlying));

    #region Data

    /// <summary>
    /// The discount curve
    /// </summary>
    private readonly DiscountCurve _discCurve;
    /// <summary>
    /// The rate index
    /// </summary>
    private readonly InterestRateIndex _rateIndex;
    /// <summary>
    /// The method
    /// </summary>
    private readonly VolatilityBootstrapMethod _method;
    /// <summary>
    /// The volatility type
    /// </summary>
    private readonly VolatilityType _volType;
    /// <summary>
    /// The projection curves
    /// </summary>
    private readonly DiscountCurve[] _projCurves;
    /// <summary>
    /// The projection indices
    /// </summary>
    private readonly InterestRateIndex[] _projIndex;

    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="CapVolatilityUnderlying"/> class.
    /// </summary>
    /// <param name="discCurve">The disc curve.</param>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="method">The method.</param>
    /// <param name="volType">Type of the vol.</param>
    /// <param name="projCurves">The proj curves.</param>
    /// <param name="projIndex">Index of the proj.</param>
    public CapVolatilityUnderlying(DiscountCurve discCurve,
      InterestRateIndex rateIndex,
      VolatilityBootstrapMethod method,
      VolatilityType volType,
      DiscountCurve[] projCurves,
      InterestRateIndex[] projIndex)
    {
      _discCurve = discCurve;
      _rateIndex = rateIndex;
      _method = method;
      _volType = volType;
      _projCurves = projCurves;
      _projIndex = projIndex;
    }
    #endregion

    #region IVolatilitySurfaceBuilder Members
    /// <summary>
    /// Creates the cap-floor volatility cube (backward compatiple version).
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="quotes">The quotes.</param>
    /// <param name="quoteType">Type of the quote.</param>
    /// <param name="smileModel">The smile model.</param>
    /// <param name="smileInterp">The smile interp.</param>
    /// <param name="timeInterp">The time interp.</param>
    /// <param name="volFitSettings">The volatility fit settings.</param>
    /// <param name="surfaceName">Name of the surface.</param>
    /// <returns>CalibratedVolatilitySurface.</returns>
    /// <exception cref="System.ArgumentException">
    /// </exception>
    /// <exception cref="BaseEntity.Toolkit.Util.ToolkitException"></exception>
    CalibratedVolatilitySurface IVolatilitySurfaceBuilder.BuildSurface(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      StrikeArray strikes,
      double[,] quotes,
      VolatilityQuoteType quoteType,
      SmileModel smileModel,
      Interp smileInterp,
      Interp timeInterp,
      VolatilityFitSettings volFitSettings,
      string surfaceName)
    {
      if (quoteType != VolatilityQuoteType.StickyStrike && logger.IsErrorEnabled)
      {
        logger.Error(String.Format(
          "Cap volatility do not support strike type {0} yet, using StickyStrike instead",
          quoteType));
      }

      //Some Validation 
      //Remove empty tenors from Euro Dollar Options 
      double[] cleanEdfPrices, cleanFutStrikes;
      double[,] cleanCallOptPrices, cleanPutOptPrices;
      Dt[] cleanEdfDates;
      cleanEdfPrices = EmptyArray<double>.Instance;
      cleanFutStrikes = EmptyArray<double>.Instance;
      cleanCallOptPrices = null;
      cleanPutOptPrices = null;
      cleanEdfDates = new Dt[0];
      // Clean Cap/Floor Expiries, strikes, and tenors
      var capTenors = tenorNames;
      var capStrikes = strikes.AsNumbers();
      var capVols = quotes;

      var cleanExpiries = capTenors.Where(t => !string.IsNullOrEmpty(t)).ToArray();
      var cleanStrikes = capStrikes.Where(k => k > 0.0).ToArray();
      var cleanVols = new double[cleanExpiries.Length,cleanStrikes.Length];
      for (int i = 0; i < cleanExpiries.Length; i++)
        for (int j = 0; j < cleanStrikes.Length; j++)
          cleanVols[i, j] = capVols[i, j];
      //Validate the Cap Expiry Tenors 
      foreach (var exp in cleanExpiries)
      {
        Tenor tenor;
        if (!Tenor.TryParse(exp, out tenor))
          throw new ArgumentException(String.Format("Invalid Cap Expiry Tenor {0}", exp));
      }

      // Handle optional fit settings
      var fitSettings = volFitSettings as RateVolatilityFitSettings;
      if (fitSettings == null)
      {
        if (volFitSettings != null)
        {
          throw new ArgumentException(String.Format(
            "Expect RateVolatilityFitSettings, but got {0}",
            volFitSettings.GetType().Name));
        }
        fitSettings = new RateVolatilityFitSettings(asOf);
      }

      var rateIndex = _rateIndex;
      var projIndex = _projIndex;
      var targetIndex = ((projIndex != null) && (projIndex.Length > 1)) ? projIndex.OrderBy(ri => ri.IndexTenor).First() : rateIndex;
      Dt settle = Cap.StandardSettle(asOf, targetIndex);
      var capMaturityDates = cleanExpiries.Select(t => Dt.Add(settle, t)).ToArray();

      var discCurve = _discCurve;
      var projCurve = _projCurves;
      var indexSelector = GetProjectionIndexSelector(capMaturityDates, rateIndex, projIndex);
      var curveSelector = GetProjectionCurveSelector(indexSelector, discCurve, projCurve);

      // Adjust normal vols if nec
      var volType = _volType;
      if (volType == VolatilityType.Normal)
      {
        for (int i = 0; i < cleanVols.GetLength(0); i++)
          for (int j = 0; j < cleanVols.GetLength(1); j++)
          {
            if (cleanVols[i, j] <= 0.0)
              throw new ArgumentException(String.Format("Cap Volatilities must be positive {0} ", cleanVols[i, j]));
            cleanVols[i, j] /= 10000.0;
          }
      }

      var method = _method;
      double[] lambdaEdf, lambdaCap;
      RateVolatilityUtil.MapFitSettings(fitSettings, cleanFutStrikes.Length, cleanStrikes.Length, volType, true, out lambdaEdf,
        out lambdaCap);

      RateVolatilityCalibrator calibrator;
      switch (smileModel)
      {
      case SmileModel.Sabr:
      case SmileModel.Heston:
        calibrator = new RateVolatilityCapSabrCalibrator(asOf, settle,
          discCurve, indexSelector, curveSelector, volType,
          cleanEdfDates, cleanFutStrikes,
          cleanEdfPrices, cleanCallOptPrices,
          cleanPutOptPrices,
          cleanExpiries, capMaturityDates,
          cleanStrikes, cleanVols,
          lambdaEdf,
          lambdaCap,
          method,
          fitSettings.SabrLowerBounds,
          fitSettings.SabrUpperBounds,
          fitSettings.SabrBeta,
          fitSettings.SabrAlpha,
          fitSettings.SabrRho,
          fitSettings.SabrNu);
        break;
      case SmileModel.None:
      case SmileModel.SplineInterpolation:
        calibrator = new RateVolatilityCapBootstrapCalibrator(asOf, settle,
          discCurve, indexSelector, curveSelector, volType, cleanEdfDates, 
          cleanFutStrikes, cleanEdfPrices, cleanCallOptPrices, cleanPutOptPrices,
          cleanExpiries, capMaturityDates, cleanStrikes, cleanVols,
          lambdaEdf, lambdaCap, method);
        break;
      default:
        throw new ToolkitException(String.Format("Smile model {0} not supported yet", smileModel));
      }
      var cube = new RateVolatilityCube(calibrator)
      {
        Description = surfaceName,
        ExpiryTenors = cleanExpiries.Select(Tenor.Parse).ToArray()
      };
      cube.Validate();
      cube.Fit();
      // Done
      return cube;
    }

    #region Helper Methods

    /// <summary>
    /// Gets the projection index selector.
    /// </summary>
    /// <param name="expiries">The expiries.</param>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="projectionIndex">Index of the projection.</param>
    /// <returns>Func{DtInterestRateIndex}.</returns>
    /// <exception cref="System.ArgumentException">Must provide one InterestRateIndex per underlying Cap</exception>
    private static Func<Dt, InterestRateIndex> GetProjectionIndexSelector(Dt[] expiries, InterestRateIndex rateIndex, InterestRateIndex[] projectionIndex)
    {
      if ((projectionIndex == null) || (projectionIndex.Length == 0))
        return dt => rateIndex;
      if (projectionIndex.Length != expiries.Length)
        throw new ArgumentException("Must provide one InterestRateIndex per underlying Cap");
      return dt =>
      {
        int idx = Array.BinarySearch(expiries, dt);
        if (idx < 0)
          throw new ArgumentException(String.Format("RateIndex for expiry {0} not found", dt));
        return projectionIndex[idx];
      };
    }

    /// <summary>
    /// Gets the projection curve selector.
    /// </summary>
    /// <param name="indexSelector">The index selector.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="projectionCurve">The projection curve.</param>
    /// <returns>Func{DtDiscountCurve}.</returns>
    private static Func<Dt, DiscountCurve> GetProjectionCurveSelector(Func<Dt, InterestRateIndex> indexSelector, DiscountCurve discountCurve,
                                                                      DiscountCurve[] projectionCurve)
    {
      if ((projectionCurve == null) || (projectionCurve.Length == 0))
        return dt => discountCurve;
      if (projectionCurve.Length == 1)
        return dt => projectionCurve[0];
      return dt =>
      {
        var rateIdx = indexSelector(dt);
        return projectionCurve.FirstOrDefault(c => rateIdx.Equals(c.ReferenceIndex)) ?? discountCurve;
      };
    }

    #endregion
    #endregion

    #region IVolatilityUnderlying Members

    /// <summary>
    /// Gets the curve1.
    /// </summary>
    /// <value>The curve1.</value>
    /// <exception cref="System.NotImplementedException"></exception>
    IFactorCurve IVolatilityUnderlying.Curve1
    {
      get { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Gets the curve2.
    /// </summary>
    /// <value>The curve2.</value>
    /// <exception cref="System.NotImplementedException"></exception>
    IFactorCurve IVolatilityUnderlying.Curve2
    {
      get { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Gets the spot rate or price.
    /// </summary>
    /// <value>The spot.</value>
    /// <exception cref="System.NotImplementedException"></exception>
    ISpotCurve IVolatilityUnderlying.Spot
    {
      get { throw new NotImplementedException(); }
    }

    ISpotCurve IVolatilityUnderlying.Deflator
    {
      get { return ConstantSpotCurve.One; }
    }

    #endregion
  }

}
