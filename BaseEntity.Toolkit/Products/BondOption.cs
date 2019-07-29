// 
//  -2012. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option on a Bond
  /// </summary>
  /// <remarks>
  /// <para>Bond options are options where the underlying asset is a bond.</para>
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a bond option.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 6, 2016);  // Expiration is june 16, 2016
  ///   Bond bond;
  ///   var option = new BondOption(
  ///     bond,                                   // Underlying bond
  ///     expirationDate,                         // Option Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     95.0                                    // Strike is 95.0
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class BondOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor for vanilla option
    /// </summary>
    /// <param name="underlying">Underlying bond</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public BondOption(Bond underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base( underlying, expiration, type, style, strike )
    {}

    /// <summary>
    /// Constructor for barrier option
    /// </summary>
    /// <param name="underlying">Underlying bond</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="barrier1Type">First barrier type</param>
    /// <param name="barrier1Level">First barrier level</param>
    /// <param name="barrier2Type">Second barrier type</param>
    /// <param name="barrier2Level">Second barrier level</param>
    public BondOption(
      Bond underlying, Dt expiration, OptionType type, OptionStyle style, double strike,
      OptionBarrierType barrier1Type, double barrier1Level,
      OptionBarrierType barrier2Type, double barrier2Level
      )
      : base( underlying, expiration, type, style, strike, barrier1Type, barrier1Level, barrier2Type, barrier2Level )
    {}

    /// <summary>
    /// Constructor for digital option
    /// </summary>
    /// <param name="underlying">Underlying bond</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="rebate">Rebate</param>
    public BondOption( Bond underlying, Dt expiration, OptionType type, OptionStyle style, double strike, double rebate )
      : base( underlying, expiration, type, style, strike, rebate )
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying Bond
    /// </summary>
    public Bond Bond { get { return (Bond)Underlying; } }

    #endregion Properties
  }
}
