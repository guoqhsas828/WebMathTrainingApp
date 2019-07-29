/*
 * SingleAssetOption.cs
 *
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using System.Collections;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{

  ///
  /// <summary>
  ///   Abstract parent class for simple options.
  /// </summary>
  ///
  /// <remarks>
  ///   Options need not derive from this class and it is simply provided as
  ///   a helpful class for options with a single strike price on a single
  ///   underlying product.
  /// </remarks>
  ///
  [Serializable]
  [ReadOnly(true)]
  public abstract class DoubleAssetOption : Product
  {
    #region Constructors

    /// <summary>
    ///   Default Constructor
    /// </summary>
    protected
    DoubleAssetOption()
    { }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying1">First Underlying product</param>
    /// <param name="underlying2">Second Underlying product</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    ///
    protected
    DoubleAssetOption(Dt effective, Dt maturity, Currency ccy, IProduct underlying1, IProduct underlying2,
                      Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(effective, maturity, ccy)
    {
      // Use properties for validation
      Underlying1 = underlying1;
      Underlying2 = underlying2;
      Expiration = expiration;
      Type = type;
      Style = style;
      Strike = strike;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      DoubleAssetOption obj = (DoubleAssetOption)base.Clone();

      obj.underlying1_ = (IProduct)underlying1_.Clone();
      obj.underlying2_ = (IProduct)underlying2_.Clone();

      return obj;
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

      // Invalid Underlying Product
      if (underlying1_ == null)
        InvalidValue.AddError(errors, this, "Underlying1", String.Format("Invalid Underlying {0} ", underlying1_));

      // Invalid Underlying Product
      if (underlying2_ == null)
        InvalidValue.AddError(errors, this, "Underlying2", String.Format("Invalid Underlying {0} ", underlying2_));

      if (!expiration_.IsEmpty() && !expiration_.IsValid())
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Invalid expiration date. Must be empty or valid date, not {0}", expiration_));

      // Expiration date after effective date
      if( expiration_ > underlying1_.Effective )
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Option Expiration {0} must be on or before underlying1 effective date {1}", expiration_, underlying1_.Effective));
      if( expiration_ > underlying2_.Effective )
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Option Expiration {0} must be on or before underlying2 effective date {1}", expiration_, underlying2_.Effective));

      // Expiration date before maturity
      if( expiration_ > underlying1_.Maturity )
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Expiration {0} must be before underlying product maturity {1}", expiration_, underlying1_.Maturity));
      if( expiration_ > underlying2_.Maturity )
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Expiration {0} must be before underlying product maturity {1}", expiration_, underlying2_.Maturity));

      // Invalid Option Type
      if (type_ == OptionType.None)
        InvalidValue.AddError(errors, this, "Type", String.Format("Invalid Option Type. Can not be {0}", type_));

      // Invalid Option Style
      if (style_ == OptionStyle.None)
        InvalidValue.AddError(errors, this, "Style", String.Format("Invalid Option Style. Can not be {0}", style_));

      //  Strike >= 0 
      if (strike_ < 0)
        InvalidValue.AddError(errors, this, "Strike", String.Format("Invalid Strike. Must be +Ve, Not {0}", strike_));

      // Upfront fee has to be within a [-2.0 - 2.0] range 
      if (financingSpread_ < -2.0 || financingSpread_ > 2.0)
        InvalidValue.AddError(errors, this, "FinancingSpread", String.Format("Invalid fin spread. Must be between 0 and 1, Not {0}", financingSpread_));

      underlying1_.Validate(errors);
      underlying2_.Validate(errors);

      return;
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Underlying product(s)
    /// </summary>
    [Category("Underlying")]
    public IProduct Underlying1
    {
      get { return underlying1_; }
      set
      {
        underlying1_ = value;
      }
    }

    /// <summary>
    ///   Second underlying product
    /// </summary>
    [Category("Underlying")]
    public IProduct Underlying2
    {
      get { return underlying2_; }
      set
      {
        underlying2_ = value;
      }
    }


    /// <summary>
    ///   Expiration date of option.
    /// </summary>
    [Category("Option")]
    public Dt Expiration
    {
      get { return expiration_; }
      set
      {
        expiration_ = value;
      }
    }


    /// <summary>
    ///   Option type
    /// </summary>
    [Category("Option")]
    public OptionType Type
    {
      get { return type_; }
      set
      {
        type_ = value;
      }
    }


    /// <summary>
    ///   Option style
    /// </summary>
    [Category("Option")]
    public OptionStyle Style
    {
      get { return style_; }
      set
      {
        style_ = value;
      }
    }


    /// <summary>
    ///   Option strike price
    /// </summary>
    [Category("Option")]
    public double Strike
    {
      get { return strike_; }
      set
      {
        strike_ = value;
      }
    }


    /// <summary>
    ///   Financing spread (Actual/360)
    /// </summary>
    [Category("Base")]
    public double FinancingSpread
    {
      get { return financingSpread_; }
      set
      {
        financingSpread_ = value;
      }
    }

    #endregion // Properties

    #region Data

    private IProduct underlying1_;
    private IProduct underlying2_;
    private Dt expiration_;
    private OptionType type_;
    private OptionStyle style_;
    private double strike_;
    private double financingSpread_;

    #endregion // Data

  } // class DoubleAssetOption

}
