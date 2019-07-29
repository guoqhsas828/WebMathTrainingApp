/*
 * SwaptionVolatilityFactory.cs
 *
 *  -2011. All Rights Reserved.
 *
 */
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  #region Config
  /// <exclude />
  [Serializable]
  public class SwaptionVolatilityFactoryConfig
  {
    /// <exclude />
    [ToolkitConfig("Whether to use the at-the-money volatilities for swaptions and Bermudan swaps.")]
    public readonly bool AtmVolatilityOnly = false;

    /// <exclude />
    [ToolkitConfig("Whether to allow extrapolation of volatilities outside the region of input data.")]
    public readonly bool AllowExtrapolation = true;

    /// <exclude />
    [ToolkitConfig("Whether to use a model consistent with Bloomberg to price Bermudan swaption.")]
    public readonly bool BloombergConsistentSwapBermudan = true;
  }
  #endregion Config

  /// <summary>
  /// Swaption volatility factory
  /// </summary>
  public static class SwaptionVolatilityFactory
  {
    ///<summary>
    /// Get the calculator for volatility searching on the volatility object
    ///</summary>
    ///<param name="volatilityObject">Volatility object</param>
    ///<param name="swaption">Swaption product</param>
    ///<param name="additionalData">Additional data for volatility searching to support more exotic product</param>
    ///<returns>Function to search black volatility</returns>
    ///<exception cref="ToolkitException"></exception>
    public static Func<Dt, double> GetCalculator(
      IVolatilityObject volatilityObject,
      Swaption swaption, object additionalData)
    {
      // If the object implements IVolatilityCalculator
      {
        var p = volatilityObject as IVolatilityCalculator;
        if (p != null) return dt => p.CalculateVolatility(swaption, additionalData);
      }
      // If the object implements IVolatilityCalculatorProvider
      {
        var p = volatilityObject as IVolatilityCalculatorProvider;
        if (p != null) return p.GetCalculator(swaption);
      }
      // For swaption volatility spline
      {
        var s = volatilityObject as SwaptionVolatilitySpline;
        if (s != null)
        {
          if (s.RateVolatilityCalibrator is RateVolatilitySwaptionMarketCalibrator) //For swaption volatility cube adjusted from cap/floor calibration
          {
            return (asOf) => s.Evaluate(swaption.GetExpiration,
              RateVolatilityUtil.EffectiveSwaptionDuration((SwaptionBlackPricer) additionalData));
          }
          else
          {
            return (asOf) => SwaptionVolatility(s, asOf, swaption, additionalData);
          }
          
        }
      }
      // For swaption volatility cube
      {
        var c = volatilityObject as SwaptionVolatilityCube;
        if (c != null)
        {
          var atm = GetCalculator(c.AtmVolatilityObject, swaption, additionalData);
          if (c.Skew == null) return atm;
          return (asOf) => Math.Max(0.0, atm(asOf) + c.Skew.Evaluate(swaption.GetExpiration,
            RateVolatilityUtil.EffectiveSwaptionDuration((SwaptionBlackPricer)additionalData),
            EffectiveStrike((SwaptionBlackPricer) additionalData)));
        }

        // For flat and caplet volatility cube
        {
          var rv = volatilityObject as RateVolatilityCube;
          if (rv != null && rv.RateVolatilityCalibrator is RateVolatilityFlatCalibrator)
            return (asOf) => rv.Interpolate(swaption.Maturity, swaption.Strike);
          else if (rv != null)
            return (asOf) => SwaptionVolatility(rv, asOf, swaption, additionalData);
        }
      }

      throw new ToolkitException(String.Format(
        "{0} is not a known volatility object.",
        volatilityObject.GetType().FullName));
    }

    public static Dt GetExpiration(this Swaption swaption, Dt forwardStartDate)
    {
      var diff = (int)(forwardStartDate - swaption.Maturity);
      return swaption.Expiration + diff;
    }

    private static double SwaptionVolatility(RateVolatilitySurface cube,
      Dt asOf, Swaption swaption, object additionalData)
    {
      if (ToolkitConfigurator.Settings.SwaptionVolatilityFactory.AtmVolatilityOnly)
      {
        return cube.SwaptionVolatilityFromAtmVols(swaption.Expiration,
          swaption.UnderlyingFixedLeg.Maturity);
      }
      return cube.CapVolatility(cube.RateVolatilityCalibrator.DiscountCurve,
        RateVolatilityUtil.CreateEquivalentCap((SwaptionBlackPricer) additionalData));
    }

    ///<summary>
    /// Calculate the effective swaption strike when there is coupon schedule on underlying swap leg
    ///</summary>
    ///<param name="pricer">Swaption pricer</param>
    ///<returns>Effective swaption strike</returns>
    ///<exception cref="ToolkitException"></exception>
    public static double EffectiveStrike(SwaptionBlackPricer pricer)
    {
      double effectiveRate, level;
      return RateVolatilityUtil.EffectiveSwaptionStrike(
        pricer.Swaption, pricer.AsOf, pricer.Settle, 
        pricer.DiscountCurve, pricer.ReferenceCurve, pricer.RateResets,
        false, out effectiveRate, out level);
    }

    public static double EvaluateVolatility(
      IVolatilityObject volatilityObject,
      Dt expiry, double rate, double strike,
      SwapRateIndex index)
    {
      // If the object implements IVolatilityCalculator
      {
        var p = volatilityObject as SabrSwaptionVolatilityEvaluator;
        if (p != null)
        {
          return p.Evaluate(expiry, index.IndexTenor.Months/12.0, rate, strike);
        }
      }
      // For swaption volatility spline
      {
        var s = volatilityObject as SwaptionVolatilitySpline;
        if (s != null)
        {
          return s.Evaluate(expiry, index.IndexTenor.Months);
        }
      }
      // For swaption volatility cube
      {
        var c = volatilityObject as SwaptionVolatilityCube;
        if (c != null)
        {
          var atm = EvaluateVolatility(c.AtmVolatilityObject,
            expiry, rate, strike, index);
          if (c.Skew == null) return atm;
          return Math.Max(0.0, atm + c.Skew.Evaluate(
            expiry, index.IndexTenor.Months, rate, strike));
        }
      }
      // Fall back, for anything implemented IRateModelParameters
      {
        var rateInterp = volatilityObject as IModelParameter;
        if (rateInterp != null)
        {
          return rateInterp.Interpolate(expiry, strike, index);
        }
      }

      throw new ToolkitException(String.Format(
        "{0} is not a known volatility object.",
        volatilityObject.GetType().FullName));

    }

  }
}
