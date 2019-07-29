// 
//  -2012. All rights reserved.
// 

using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Common interface for option products
  /// </summary>
  public interface IOptionProduct : IProduct
  {
    /// <summary>Gets the option expiration date</summary>
    Dt Expiration { get; }

    /// <summary>Gets the underlying product of the option</summary>
    /// <remarks>This property can return null if the option has no underlying product.</remarks>
    IProduct Underlying { get; }
  }

  /// <summary>
  ///   Common interface for basic exotic options with barriers and/or digital payments
  /// </summary>
  public interface IBasicExoticOption : IOptionProduct
  {
    #region Settlement

    /// <summary>
    ///  Settlement type
    /// </summary>
    SettlementType SettlementType { get; }

    #endregion

    #region Digital

    /// <summary>
    /// Option payoff type
    /// </summary>
    OptionPayoffType PayoffType { get; }

    /// <summary>
    /// Rebate  amount (for digital options)
    /// </summary>
    double Rebate { get; }

    #endregion Digital

    #region Barrier

    /// <summary>
    /// Barrier monitoring frequency.
    /// </summary>
    Frequency BarrierMonitoringFrequency { get; }

    /// <summary>
    /// Gets or sets the barrier payoff time.
    /// </summary>
    BarrierOptionPayoffTime BarrierPayoffTime { get; }

    /// <summary>
    /// List of barriers.
    /// </summary>
    IList<Barrier> Barriers { get; }

    #endregion Barrier

  }

  internal static class BasicExoticOptionUtility
  {
    #region Informational

    /// <summary>
    /// Is regular (vanilla) option
    /// </summary>
    /// <param name="option">The option.</param>
    /// <returns><c>true</c> for options with regular payoffs; otherwise, <c>false</c>.</returns>
    internal static bool IsRegular(this IBasicExoticOption option)
    {
      return !(option.IsDigital() || option.IsBarrier());
    }

    /// <summary>
    /// True if option is digital
    /// </summary>
    /// <param name="option">The option.</param>
    /// <returns><c>true</c> for options with digital payoffs; otherwise, <c>false</c>.</returns>
    public static bool IsDigital(this IBasicExoticOption option)
    {
      return (option.PayoffType == OptionPayoffType.Digital);
    }

    /// <summary>
    /// True if option has one or more barriers
    /// </summary>
    /// <param name="option">The option.</param>
    /// <returns><c>true</c> for barrier options; otherwise, <c>false</c>.</returns>
    public static bool IsBarrier(this IBasicExoticOption option)
    {
      return (option.Barriers != null && option.Barriers.Count > 0);
    }

    /// <summary>
    /// True if option has two barriers
    /// </summary>
    /// <param name="option">The option.</param>
    /// <returns><c>true</c> for is single barrier option; otherwise, <c>false</c>.</returns>
    public static bool IsSingleBarrier(this IBasicExoticOption option)
    {
      return (option.Barriers != null && option.Barriers.Count == 1);
    }

    /// <summary>
    /// True if option has two barriers
    /// </summary>
    /// <param name="option">The option.</param>
    /// <returns><c>true</c> for double barrier options; otherwise, <c>false</c>.</returns>
    public static bool IsDoubleBarrier(this IBasicExoticOption option)
    {
      return (option.Barriers != null && option.Barriers.Count == 2);
    }

    /// <summary>
    /// Is barrier touch option
    /// </summary>
    internal static bool IsTouchOption(this IBasicExoticOption option)
    {
      var barriers = option.Barriers;
      return barriers != null &&
        ((barriers.Count == 1 && barriers[0].IsTouch) ||
          (barriers.Count == 2 && (barriers[0].IsTouch || barriers[1].IsTouch)));
    }

    #endregion Informational
  }
}
