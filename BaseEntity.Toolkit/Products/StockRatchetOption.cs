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
  /// Ratchet option on a Stock
  /// </summary>
  /// <remarks>
  /// <para>An option is a contract that gives the buyer the right but not the obligation to trade an
  /// underlying asset at a pre-determined price.</para>
  /// <para> Ratchet options are options where the strike is reset periodically. They are essentially a series of
  /// consecutive forward start options. Each option expires on a reset date with a new option effective from that date
  /// struck at the current underlying stock price (at-the-money).</para>
  /// <para>The premium is paid upfront and the payouts of each option can be payed at each reset date or at
  /// the final maturity.</para>
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class StockRatchetOption : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor for ratchet stock option
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="resetDates">Reset dates</param>
    /// <param name="type">Option type</param>
    /// <param name="strike">Strike price</param>
    public StockRatchetOption(Dt effective, Currency ccy, Dt expiration, Dt[] resetDates,
      OptionType type, double strike) : base(effective, expiration, ccy)
    {
      ResetDates = resetDates;
      Type = type;
      Strike = strike;
      PayoutOnResetDate = true;
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
      if (!Expiration.IsEmpty() && !Expiration.IsValid())
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Invalid expiration date. Must be empty or valid date, not {0}", Expiration));
      if (Type == OptionType.None)
        InvalidValue.AddError(errors, this, "Type", String.Format("Invalid Option Type. Can not be {0}", Type));
      if (Strike < 0)
        InvalidValue.AddError(errors, this, "Strike", String.Format("Invalid Strike. Must be +Ve, Not {0}", Strike));
      if (ResetDates != null)
      {
        foreach (var d in ResetDates)
          if (Dt.Cmp(d, Expiration) > 0)
            InvalidValue.AddError(errors, this, "ResetDates", String.Format("Reset date {0} is after expiration", d));
      }
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Expiration date of option.
    /// </summary>
    [Category("Option")]
    public Dt Expiration { get { return Maturity; } }

    /// <summary>
    /// Reset dates
    /// </summary>
    [Category("Option")]
    public Dt[] ResetDates { get; set; }

    /// <summary>
    /// Option type
    /// </summary>
    [Category("Option")]
    public OptionType Type { get; set; }

    /// <summary>
    /// Option strike price
    /// </summary>
    [Category("Option")]
    public double Strike { get; set; }

    /// <summary>
    /// If true, strike-resetting related payout is handed out on reset date, otherwise on option maturity date
    /// </summary>
    public bool PayoutOnResetDate { get; set; }

    #endregion Properties
  }
}