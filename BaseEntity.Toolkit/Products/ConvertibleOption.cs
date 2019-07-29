// 
//  -2012. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Option on Convertible bond
  /// </summary>
  /// <summary>
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </summary>
  /// <preliminary>
  ///   This class is preliminary and not supported for customer use. This may be
  ///   removed or moved to a separate product at a future date.
  /// </preliminary>
  /// <seealso cref="SingleAssetOptionBase"/>
  [Serializable]
  [ReadOnly(true)]
  public class ConvertibleOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="underlying">Underlying bond</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strikeType">Option strike type</param>
    /// <param name="strike">Strike price</param>
    public ConvertibleOption(Convertible underlying, Dt expiration, OptionType type, OptionStyle style, OptionStrikeType strikeType, double strike)
      : base(underlying, expiration, type, style, strike)
    {
      StrikeType = strikeType;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying Convertible Bond
    /// </summary>
    [Category("Underlying")]
    public Convertible Convertible
    {
      get { return (Convertible)Underlying; }
    }

    /// <summary>
    /// Option strike type (Price/Yield/Spread)
    /// </summary>
    [Category("Option")]
    public OptionStrikeType StrikeType { get; set; }

    #endregion Properties
  }
}
