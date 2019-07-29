// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.Serialization;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Convenient abstract parent class for product implementations
  /// </summary>
  /// <remarks>
  ///   <para>Products are data representations of financial products.</para>
  ///   <para>Products are independent from any models which are
  ///   used to price them or any risk analysis performed.</para>
  ///   <para>Products are linked to models via the Pricer class
  ///   which adds state and model-specific information.</para>
  ///   <para>Products need not inherit from this class but for
  ///   convenience this provides common properties.</para>
  /// </remarks>
  /// <seealso cref="IProduct"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.IPricer"/>
  /// <seealso cref="BaseEntity.Toolkit.Models"/>
  [Serializable]
  [ReadOnly(true)]
  [DataContract]
  public abstract class Product : BaseEntityObject, IProduct
  {
    #region Constructors

    /// <summary>
    /// Internal Constructor
    /// </summary>
    protected Product()
    {
      Effective = Dt.Empty;
      Maturity = Dt.MaxValue;
      Ccy = Currency.None;
    }

    /// <summary>
    /// Internal Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    protected Product(Dt effective, Dt maturity)
    {
      Effective = effective;
      Maturity = maturity;
      Ccy = Currency.None;
    }

    /// <summary>
    /// Internal onstructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    protected Product(Dt effective, Dt maturity, Currency ccy)
    {
      Effective = effective;
      Maturity = maturity;
      Ccy = ccy;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public override object Clone()
    {
      Product obj = (Product)base.Clone();
      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Convert this product to a string
    /// </summary>
    public override string ToString()
    {
      if (string.IsNullOrEmpty(Description))
      {
        // Provide minimal description of object
        return GetType().Name;
      }
      return Description;
    }

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">if product not valid</exception>
    public override void Validate(ArrayList errors)
    {
      // Effective date before maturity date
      if (Effective >= Maturity)
        InvalidValue.AddError(errors, this, String.Format("Effective date {0} must be before maturity date {1}", Effective, Maturity));
      // Notional >= 0
      if (Notional < 0)
        InvalidValue.AddError(errors, this, "Notional", String.Format("Invalid Principal. Must be +Ve, Not {0}", Notional));
    }

    /// <summary>
    /// True if this product is active on the specified pricing date
    /// </summary>
    /// <remarks>
    ///   <para>A product is active there is any residual risk. Ie there are any
    ///   unsettled cashflows.</para>
    ///   <para>For most products this is if the pricing date is on or before
    ///   the maturity date.</para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <returns>true if product is active</returns>
    public virtual bool IsActive(Dt asOf)
    {
      return !((asOf < Effective) || (asOf > Maturity));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Description of product
    /// </summary>
    [Category("Base")]
    public string Description
    {
      get { return description_; }
      set { description_ = value; }
    }

    /// <summary>
    ///   Product primary currency
    /// </summary>
    [Category("Base")]
    public virtual Currency Ccy
    {
      get { return ccy_; }
      set { ccy_ = value; }
    }

    /// <summary>
    ///   Effective date (date accrual and protection start)
    /// </summary>
    [Category("Base")]
    public virtual Dt Effective
    {
      get { return effective_; }
      set { effective_ = value; }
    }

    /// <summary>
    ///   Maturity date
    /// </summary>
    [Category("Base")]
    public virtual Dt Maturity
    {
      get { return maturity_; }
      set { maturity_ = value; }
    }

    /// <summary>
    ///  Final date the product keeps to be active
    /// </summary>
    [Category("Base")]
    public virtual Dt EffectiveMaturity
    {
      get { return Maturity; }
    }

    /// <summary>
    ///   Notional size per trade
    /// </summary>
    [Category("Base")]
    [DataMember]
    public double Notional
    {
      get { return notional_; }
      set { notional_ = value; }
    }

    /// <summary>
    /// Payments that override ones generated from Product
    /// </summary>
    public PaymentSchedule CustomPaymentSchedule { get; set; }

    #endregion

    #region Data

    private string description_ = "";
    private Currency ccy_ = Currency.None;
    private Dt effective_;
    private Dt maturity_;
    private double notional_ = 1.0;

    #endregion
  }
}
