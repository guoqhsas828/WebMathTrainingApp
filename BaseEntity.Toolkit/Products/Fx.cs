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
  /// <summary>
  /// Spot Fx product
  /// </summary>
  /// <remarks>
  /// <para>A foreign exchange spot transaction, also known as FX spot, is an agreement between
  /// two parties to buy one currency against selling another currency at an agreed price for
  /// settlement on the spot date. The exchange rate at which the transaction is done is called
  /// the spot exchange rate. As of 2010, the average daily turnover of global FX spot transactions
  /// reached nearly 1.5 trillion USD, counting 37.4% of all foreign exchange transactions.</para>
  /// <para>The standard settlement timeframe for foreign exchange spot transactions is T + 2 days;
  /// i.e., two business days from the trade date. A notable exception is the USD/CAD currency pair,
  /// which settles at T + 1.</para>
  /// <para><i> </i></para>
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class Fx : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="receiveCcy">Receive currency</param>
    /// <param name="payCcy">Currency of Fx</param>
    public Fx(Currency receiveCcy, Currency payCcy)
      : base(Dt.Empty, Dt.MaxValue, payCcy)
    {
      ReceiveCcy = receiveCcy;
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
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if( PayCcy == ReceiveCcy )
        InvalidValue.AddError(errors, this, "PayCcy", String.Format("Pay currency {0} cannot be same as receive currency {1}", PayCcy, ReceiveCcy));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Pay currency
    /// </summary>
    public Currency PayCcy
    {
      get { return Ccy; }
      set { Ccy = value; }
    }

    /// <summary>
    /// Receive currency
    /// </summary>
    public Currency ReceiveCcy { get; set; }

    #endregion Properties

  }
}
