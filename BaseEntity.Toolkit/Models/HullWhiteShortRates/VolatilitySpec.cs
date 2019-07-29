//
//   2011-2014. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Util;

using static BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  /// <summary>
  /// Volatility Specification
  /// </summary>
  public struct VolatilitySpec
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilitySpec"/> struct.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="shift">The shift.</param>
    internal VolatilitySpec(DistributionType type, double shift = 0)
    {
      Distribution = type;
      Shift = type == DistributionType.ShiftedLogNormal ? shift : 0;
    }

    /// <summary>
    /// Normals this instance.
    /// </summary>
    /// <returns>VolatilitySpec.</returns>
    public static VolatilitySpec Normal()
    {
      return new VolatilitySpec(DistributionType.Normal);
    }

    /// <summary>
    /// Logs the normal.
    /// </summary>
    /// <returns>VolatilitySpec.</returns>
    public static VolatilitySpec LogNormal()
    {
      return new VolatilitySpec(DistributionType.LogNormal);
    }

    /// <summary>
    /// Shifted log normal with the specified shift.
    /// </summary>
    /// <param name="shift">The shift.</param>
    /// <returns>VolatilitySpec.</returns>
    public static VolatilitySpec ShiftedLogNormal(double shift)
    {
      return new VolatilitySpec(DistributionType.ShiftedLogNormal, shift);
    }

    #endregion

    #region Option calculations

    /// <summary>
    /// Calculates the option price.
    /// </summary>
    /// <param name="volatility">The volatility.</param>
    /// <param name="time">The time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="ToolkitException"></exception>
    public double CalculateOptionPrice(
      double volatility, double time,
      double forward, double strike)
    {
      const OptionType call = OptionType.Call;
      switch (Distribution)
      {
      case DistributionType.Normal:
        return BlackNormal.P(call, time, 0, forward, strike, volatility);
      case DistributionType.LogNormal:
        return Black.P(call, time, forward, strike, volatility);
      case DistributionType.ShiftedLogNormal:
        return Black.P(call, time, forward + Shift, strike + Shift, volatility);
      default:
        throw new ToolkitException($"Unknown distribution type {Distribution}");
      }
    }

    /// <summary>
    /// Implies the volatility.
    /// </summary>
    /// <param name="isCall"><c>true</c> for call option, <c>false</c> for put option</param>
    /// <param name="price">The price.</param>
    /// <param name="time">The time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="ToolkitException"></exception>
    public double ImplyVolatility(
      bool isCall, double price, double time,
      double forward, double strike)
    {
      if (time <= 0) return 0;

      double totalVolatility;
      switch (Distribution)
      {
      case DistributionType.Normal:
        totalVolatility = BlackNormalImpliedVolatility(price,
          isCall ? (forward - strike) : (strike - forward), 0);
        break;

      case DistributionType.LogNormal:
        totalVolatility = LogNormalImpliedVolatility(
          isCall, price, forward, strike);
        break;

      case DistributionType.ShiftedLogNormal:
        totalVolatility = LogNormalImpliedVolatility(
          isCall, price, forward + Shift, strike + Shift);
        break;

      default:
        throw new ToolkitException($"Unknown distribution type {Distribution}");
      }

      return totalVolatility/Math.Sqrt(time);
    }

    private static double LogNormalImpliedVolatility(
      bool isCall, double price, double forward, double strike)
    {
      return BlackScholesImpliedVolatility(
        isCall, price/forward, strike/forward, 1E-14);
    }

    #endregion

    /// <summary>
    /// Gets the distribution.
    /// </summary>
    /// <value>The distribution.</value>
    public DistributionType Distribution { get; }

    /// <summary>
    /// Gets the shift.
    /// </summary>
    /// <value>The shift.</value>
    public double Shift { get; }
  }

}
