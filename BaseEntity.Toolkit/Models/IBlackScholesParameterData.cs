/*
 *   2012. All rights reserved.
 */

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  ///   The interface holding 4 parameters, <c>Spot</c>, <c>Time to expiry</c>, <c>Rate 1</c> and <c>Rate 2</c>, to build volatility smile with Black-Scholes model.
  /// </summary>
  /// <remarks>
  ///   With these data, the fair prices of the options can be obtained from
  ///   the strikes and the volatilities by the standard Black-Scholes formula<math>
  ///     \mathrm{price} \equiv \omega S e^{-r_1 T} \Phi(\omega d_1) - \omega K e^{-r_2 T} \Phi(\omega d_2)
  ///   </math>where<math>
  ///     d_1 = \frac{\log(S/K) + (r_2 - r_1 +\sigma^2/2)T}{\sigma\sqrt{T}},\quad d_2 = d_1 - \sigma\sqrt{T}
  ///   </math>and
  ///   <list type="bullet">
  ///     <item><description><m>\omega = 1</m> for call and <m>\omega=-1</m> for put;</description></item>
  ///     <item><description><m>\Phi(\cdot)</m> is the standard normal distribution function;</description></item>
  ///     <item><description><m>K</m> is the strike and <m>\sigma</m> is the volatility;</description></item>
  ///     <item><description><m>\{T, S, r_1, r_2\}</m> come from the properties <c>{Time, Spot, Rate1, Rate2}</c>.</description></item>
  ///   </list>
  /// </remarks>
  public interface IBlackScholesParameterData
  {
    /// <summary>
    ///   Gets the time to expiry.
    /// </summary>
    double Time { get; }

    /// <summary>
    ///   Gets the spot price.
    /// </summary>
    double Spot { get; }

    /// <summary>
    ///   Gets the continuously compounded rate associated with the spot price
    ///   (normally the dividend rate or foreign interest rate).
    /// </summary>
    double Rate1 { get; }

    /// <summary>
    ///   Gets the continuously compounded rate associated with the strike price
    ///   (normally the risk free rate or domestic interest rate).
    /// </summary>
    double Rate2 { get; }

    /// <summary>
    /// Gets the shift for the shifted log-normal and related models.
    /// </summary>
    /// <value>The shift.</value>
    double Shift { get; }
  }

  public interface IBlackScholesParameterDataProvider
  {
    IBlackScholesParameterData GetParameters(Dt expiry);
  }
}
