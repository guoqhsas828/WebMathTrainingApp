//
// CDOOption.cs
//  -2008. All rights reserved.
//

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{

  ///
  /// <summary>
  ///   Option to enter into a CDO tranche.
  /// </summary>
  /// <remarks>
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class CDOOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    ///   Default Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying CDO tranche</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    ///
    public
    CDOOption(
      Dt effective,
      Currency ccy,
      SyntheticCDO underlying,
      Dt expiration,
      OptionType type,
      OptionStyle style,
      double strike)
      : base(effective, ccy, underlying, expiration, type, style, strike)
    { }


    /// <summary>
    ///   Default Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying CDO tranche</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike on spread or market value</param>
    /// <param name="strikeIsValue">If true, strike is interpreted as  market value; else, as spread</param>
    ///
    public
    CDOOption(Dt effective,
               Currency ccy,
               SyntheticCDO underlying,
               Dt expiration,
               PayerReceiver type,
               OptionStyle style,
               double strike,
               bool strikeIsValue)
      : base(effective, ccy, underlying, expiration,
              (type == PayerReceiver.Payer) ? OptionType.Put : OptionType.Call,
              style, strike)
    {
      strikeIsValue_ = strikeIsValue;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Underlying CDO tranche
    /// </summary>
    [Category("Underlying")]
    public SyntheticCDO CDO
    {
      get { return (SyntheticCDO)Underlying; }
    }

    /// <summary>
    ///   Underlying CDO tranche
    /// </summary>
    [Category("Option")]
    public bool StrikeIsPrice
    {
      get { return strikeIsValue_; }
      set { strikeIsValue_ = value; }
    }

    #endregion // Properties

    #region Data

    private bool strikeIsValue_;

    #endregion // Data

  } // class CDOOption

}
