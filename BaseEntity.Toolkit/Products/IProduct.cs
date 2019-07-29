//
// IProduct.cs
//   2008. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using System.Collections;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Defines the public interface for Products
  /// </summary>
  /// <remarks>
  ///   <para>Products are data representations of financial products.</para>
  ///   <para>Products are independent from any models which are
  ///   used to price them or any risk analysis performed.</para>
  ///   <para>Products are linked to models via the Pricer class
  ///   which adds state and model-specific information.</para>
  /// </remarks>
  /// <seealso cref="Product"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.IPricer"/>
  /// <seealso cref="BaseEntity.Toolkit.Models"/>
  public interface IProduct : IValidatable, ICloneable
  {
    #region Methods

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
    bool IsActive(Dt asOf);

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Description of product
    /// </summary>
    string Description { get; set; }

    /// <summary>
    ///   Product primary currency
    /// </summary>
    Currency Ccy { get; set; }

    /// <summary>
    ///   Effective date (date accrual and protection start)
    /// </summary>
    Dt Effective { get; set; }

    /// <summary>
    ///   Maturity or maturity date
    /// </summary>
    Dt Maturity { get; set; }

    ///<summary>
    ///   Final date the product keeps active
    ///</summary>
    Dt EffectiveMaturity{ get;}

    /// <summary>
    ///   Notional value
    /// </summary>
    /// <remarks>
    ///   <para>Specifies the nominal amount of a security.</para>
    /// <para>This is the notional size per trade</para>
    /// </remarks>
    double Notional { get; set; }

    #endregion Properties

  }
}
