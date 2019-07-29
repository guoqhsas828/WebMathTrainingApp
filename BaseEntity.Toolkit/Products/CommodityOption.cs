// 
//  -2013. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option on a Commodity
  /// </summary>
  /// <remarks>
  /// <para>Commodity options are options where the underlying asset is a commodity.</para>
  /// <para>An option is a financial contract between two parties that gives the right but not the obligation for one party
  /// to buy an underlying asset a reference price (the strike) at a future date (the expiration). This right has a value
  /// based on the likely difference between the reference price and the price of the underlying asset on the expiration
  /// date. This value (the premium) is typically paid upfront by the buyer of the option to the seller of the option.</para>
  ///
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a commodity option.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 6, 2016);  // Expiration is june 16, 2016
  ///
  ///   var option = new CommodityOption(
  ///     expirationDate,                         // Option Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     125.0                                   // Strike is 125.0
  ///   );
  /// </code>
  /// </example>
  /// <seealso cref="SingleAssetOptionBase"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CommodityOptionPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class CommodityOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor for vanilla option
    /// </summary>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public CommodityOption(Dt expiration, OptionType type, OptionStyle style, double strike)
      : base( new Commodity(), expiration, type, style, strike )
    {}

    /// <summary>
    /// Constructor for barrier option
    /// </summary>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="barrier1Type">First barrier type</param>
    /// <param name="barrier1Level">First barrier level</param>
    /// <param name="barrier2Type">Second barrier type</param>
    /// <param name="barrier2Level">Second barrier level</param>
    public CommodityOption(
      Dt expiration, OptionType type, OptionStyle style, double strike,
      OptionBarrierType barrier1Type, double barrier1Level,
      OptionBarrierType barrier2Type, double barrier2Level
      )
      : base( new Commodity(), expiration, type, style, strike, barrier1Type, barrier1Level, barrier2Type, barrier2Level )
    {}

    /// <summary>
    /// Constructor for digital option
    /// </summary>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="rebate">Rebate</param>
    public CommodityOption( Dt expiration, OptionType type, OptionStyle style, double strike, double rebate )
      : base( new Commodity(), expiration, type, style, strike, rebate )
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying Commodity
    /// </summary>
    public Commodity Commodity { get { return (Commodity)Underlying; } }

    #endregion Properties
  }
}
