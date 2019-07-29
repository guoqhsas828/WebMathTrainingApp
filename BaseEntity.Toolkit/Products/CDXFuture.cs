// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  ///<summary>
  /// CDS Index Futures product
  ///</summary>
  /// <remarks>
  ///   <para>A CDX future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   CDS Index on a specified future expiration date at a predetermined price.</para>
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a CDX future.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2020); // Expiration is December 16, 2020
  /// 
  ///   var future = new CDXFuture(
  ///    expirationDate,                          // Expiration
  ///    0.00465,                                 // Deal premium is 46.5bp per annum
  ///    100000                                   // Contract size
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class CDXFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, and Currency are unset.
    /// The TickSize is 0.01, the TickValue is TickSize*ContractSize and the SettlementType is Physical.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public CDXFuture(Dt lastDeliveryDate, double premium, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {
      Premium = premium;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (Premium <= 0 || Premium > 2.0)
        InvalidValue.AddError(errors, this, "NominalCoupon", "Invalid nominal coupon");
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Premium of underlying CDX contract
    /// </summary>
    public double Premium { get; set; }

    #endregion Properties
  }
}
