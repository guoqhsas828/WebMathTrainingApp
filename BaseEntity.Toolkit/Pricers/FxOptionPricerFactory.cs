/*
 * FxOptionPricerFactory.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Factory class for IFxOptionPricers.
  /// </summary>
  public static class FxOptionPricerFactory
  {
    /// <summary>
    /// Creates a new IFxOptionPricer of the appropriate type.
    /// </summary>
    /// <param name="fxOption">The fx option.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="domesticCurve">The domestic curve.</param>
    /// <param name="foreignCurve">The foreign curve.</param>
    /// <param name="fxCurve">The fx curve.</param>
    /// <param name="volatilitySurface">The volatility surface.</param>
    /// <param name="premiumPayment">The premium payment.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>
    ///   <see cref="IFxOptionPricer"/>
    /// </returns>
    public static IFxOptionPricer NewPricer(FxOption fxOption, Dt asOf, Dt settle, double notional, DiscountCurve domesticCurve, DiscountCurve foreignCurve, FxCurve fxCurve, CalibratedVolatilitySurface volatilitySurface, Payment premiumPayment, int flags)
    {
      IFxOptionPricer pricer;

      if (fxOption.IsSingleBarrier)
      {
        pricer = new FxOptionSingleBarrierPricer(fxOption, asOf, settle, domesticCurve, foreignCurve, fxCurve, volatilitySurface, flags)
        {
          Notional = notional,
          Payment = premiumPayment
        };
      }
      else if(fxOption.IsDoubleBarrier)
      {
        pricer = new FxOptionDoubleBarrierPricer(fxOption, asOf, settle, domesticCurve, foreignCurve, fxCurve, volatilitySurface, flags)
        {
          Notional = notional,
          Payment = premiumPayment
        };
      }
      else
      {
        pricer = new FxOptionVanillaPricer(fxOption, asOf, settle, domesticCurve, foreignCurve, fxCurve, volatilitySurface)
                   {
                     Notional = notional,
                     Payment = premiumPayment
                   };
      }

      // Validate
      pricer.Validate();

      // Done
      return pricer;
    }

    #region Vanna-Volga pricing helpers

    internal static double UnitDelta(
      FxOptionPricerBase pricer,
      Func<double> recalculateUnitPrice)
    {
      var savedSpot = pricer.SpotFxRate;
      double delta;
      try
      {
        var bumpSize = 0.0001;

        // Up
        pricer.SpotFxRate = savedSpot + bumpSize;
        var pu = recalculateUnitPrice();

        // Down
        pricer.SpotFxRate = savedSpot - bumpSize;
        var pd = recalculateUnitPrice();

        // Finite difference
        delta = (pu - pd)/(2*bumpSize);
      }
      finally
      {
        //restore
        pricer.SpotFxRate = savedSpot;
      }

      // Done
      return delta;
    }

    internal static double UnitGamma(
      FxOptionPricerBase pricer,
      Func<double> recalculateUnitPrice)
    {
      var savedSpot = pricer.SpotFxRate;
      double gamma;
      try
      {
        var bumpSize = 0.0001;

        // Up
        pricer.SpotFxRate = savedSpot + bumpSize;
        var pu = UnitDelta(pricer, recalculateUnitPrice);

        // Down
        pricer.SpotFxRate = savedSpot - bumpSize;
        var pd = UnitDelta(pricer, recalculateUnitPrice);

        // Finite Difference
        gamma = (pu - pd)/(2*bumpSize);
      }
      finally
      {
        pricer.SpotFxRate = savedSpot;
      }

      // Done
      return gamma;
    }

    internal static double UnitVega(
      FxOptionPricerBase pricer,
      Func<double> recalculateUnitPrice)
    {
      var volatilityCurve = pricer.VolatilityCurve;
      double savedSpread = volatilityCurve.Spread;
      try
      {
        double bump = 100.0/10000;
        // Up
        volatilityCurve.Spread += bump;
        var uSig = pricer.CalculateAverageVolatility(
          pricer.AsOf, pricer.FxOption.Maturity);
        var upPv = recalculateUnitPrice();

        // Down
        volatilityCurve.Spread -= bump + bump;
        var dSig = pricer.CalculateAverageVolatility(
          pricer.AsOf, pricer.FxOption.Maturity);
        var downPv = recalculateUnitPrice();

        // Finite Difference
        double vol01 = (upPv - downPv)/(uSig - dSig);

        // Done
        return vol01;
      }
      finally
      {
        volatilityCurve.Spread = savedSpread;
      }
    }

    internal static double UnitVanna(
      FxOptionPricerBase pricer,
      Func<double> recalculateUnitPrice)
    {
      var savedSpot = pricer.SpotFxRate;
      double vanna;
      try
      {
        var bumpSize = 0.01;

        // Up
        pricer.SpotFxRate = savedSpot + bumpSize;
        var pu = UnitVega(pricer, recalculateUnitPrice);

        // Down
        pricer.SpotFxRate = savedSpot - bumpSize;
        var pd = UnitVega(pricer, recalculateUnitPrice);

        // Finite Difference
        vanna = (pu - pd)/(2*bumpSize);
      }
      finally
      {
        pricer.SpotFxRate = savedSpot;
      }

      // Done
      return vanna;
    }

    internal static double UnitVolga(
      FxOptionPricerBase pricer,
      Func<double> recalculateUnitPrice)
    {
      var volatilityCurve = pricer.VolatilityCurve;
      var savedSpread = volatilityCurve.Spread;
      try
      {
        // Base
        var origSig = pricer.CalculateAverageVolatility(
          pricer.AsOf, pricer.FxOption.Maturity);
        var p = recalculateUnitPrice();

        // Up
        volatilityCurve.Spread += 100.0/10000;
        var uSig = pricer.CalculateAverageVolatility(
          pricer.AsOf, pricer.FxOption.Maturity);
        var uv = (recalculateUnitPrice() - p)/(uSig - origSig);

        // Down
        volatilityCurve.Spread -= 200.0/10000;
        var dSig = pricer.CalculateAverageVolatility(
          pricer.AsOf, pricer.FxOption.Maturity);
        var dv = (recalculateUnitPrice() - p)/(dSig - origSig);

        // Finite Difference
        double volga = (uv - dv)/0.01;

        // Done
        return volga;
      }
      finally
      {
        volatilityCurve.Spread = savedSpread;
      }
    }

    #endregion
  }
}
