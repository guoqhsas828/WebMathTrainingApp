using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///  Miscellaneous calculators related to pricers.
  /// </summary>
  public static class PricerCalculators
  {
    /// <summary>
    /// Calculates the discount spread.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="targetPv">The target pv.</param>
    /// <returns>System.Double.</returns>
    public static double CalculateDiscountSpread(
      this IPricer pricer,
      DiscountCurve discountCurve,
      double targetPv)
    {
      var cloned = new object[] {pricer, discountCurve}.CloneObjectGraph();
      pricer = (IPricer)cloned[0];
      discountCurve = (DiscountCurve)cloned[1];

      double origSpread = discountCurve.Spread;

      // Create a delegate
      Func<double, double> evaluatePrice = (x) =>
      {
        double savedSpread = discountCurve.Spread;

        // Update spread
        discountCurve.Spread = origSpread + x;

        // Re-price (and refit tree with shifted discount curve)
        pricer.Reset();
        double price = pricer.Pv();

        // Restore spread
        discountCurve.Spread = savedSpread;

        return price;
      };

      try
      {
        return evaluatePrice.SolveDiscountSpread(targetPv);
      }
      finally
      {
        discountCurve.Spread = origSpread;
      }
    }
  }
}
