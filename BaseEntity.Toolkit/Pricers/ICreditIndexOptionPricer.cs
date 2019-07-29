using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Interface ICreditIndexOptionPricer
  /// </summary>
  public interface ICreditIndexOptionPricer : IPricer<CDXOption>
  {
    /// <summary>
    /// Gets the notional.
    /// </summary>
    /// <value>The notional.</value>
    double Notional { get; }

    /// <summary>
    /// Gets the effective notional.
    /// </summary>
    /// <value>The effective notional.</value>
    double EffectiveNotional { get; }

    /// <summary>
    /// Gets the current notional.
    /// </summary>
    /// <value>The effective notional.</value>
    double CurrentNotional { get; }

    /// <summary>
    /// Gets the underlying credit index option.
    /// </summary>
    /// <value>The underlying credit index option.</value>
    CDXOption CDXOption { get; }

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    DiscountCurve DiscountCurve { get; }

    /// <summary>
    /// Gets the volatility surface.
    /// </summary>
    /// <value>The volatility surface.</value>
    VolatilitySurface VolatilitySurface { get; }

    /// <summary>
    /// Gets the interpolated volatility value at the option expiry.
    /// </summary>
    /// <value>The volatility value.</value>
    double Volatility { get; }

    /// <summary>
    /// Gets at the money forward value.
    /// </summary>
    /// <value>At the money forward value.</value>
    double AtTheMoneyForwardValue { get; }

    /// <summary>
    /// Gets the forward strike value.
    /// </summary>
    /// <value>The forward strike value.</value>
    double ForwardStrikeValue { get; }

    /// <summary>
    /// Gets the forward upfront value.
    /// </summary>
    /// <value>The forward upfront value.</value>
    double ForwardUpfrontValue { get; }

    /// <summary>
    /// Gets the forward front end protection.
    /// </summary>
    /// <value>The front end protection.</value>
    double FrontEndProtection { get; }

    /// <summary>
    /// Gets the known, existing loss at the pricing date.
    /// </summary>
    /// <value>The existing loss.</value>
    double ExistingLoss { get; }

    /// <summary>
    /// Gets the expected survival.
    /// </summary>
    /// <value>The expected survival.</value>
    double ExpectedSurvival { get; }

    /// <summary>
    /// Gets the the forward PV01 of the underlying index.
    /// </summary>
    /// <value>The forward PV01 of the underlying index.</value>
    double ForwardPv01 { get; }

    /// <summary>
    /// Gets the discounted option intrinsic value at the expiration.
    /// </summary>
    /// <value>The discounted option intrinsic value.</value>
    double OptionIntrinsicValue { get; }

    /// <summary>
    /// Gets the pricer for the underlying credit index.
    /// </summary>
    /// <returns>ICDXPricer.</returns>
    CDXPricer GetPricerForUnderlying();

    /// <summary>
    /// Calculates the fair price of the option.
    /// </summary>
    /// <returns>System.Double.</returns>
    double CalculateFairPrice(double volatility);

    /// <summary>
    /// Implies the volatility.
    /// </summary>
    /// <param name="fairValue">The fair value.</param>
    /// <returns>System.Double.</returns>
    double ImplyVolatility(double fairValue);

    /// <summary>
    /// Calculates the probability that the option ends in the meny on the expiration.
    /// </summary>
    /// <returns>System.Double.</returns>
    double CalculateExerciseProbability(double volatility);

    /// <summary>
    ///  Create a new copy of this pricer with the specified quote
    ///  while everything else are copied memberwise to the new pricer.
    /// </summary>
    /// <param name="quote">The market quote.</param>
    /// <returns>ICreditIndexOptionPricer.</returns>
    ICreditIndexOptionPricer Update(MarketQuote quote);

    /// <summary>
    /// Model basis.
    /// </summary>
    double ModelBasis { get; }
  }
}
