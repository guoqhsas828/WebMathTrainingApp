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
  /// Cliquet option on a Stock
  /// </summary>
  /// <remarks>
  /// <para>An option is a contract that gives the buyer the right but not the obligation to trade an
  /// underlying asset at a pre-determined price.</para>
  /// <para>A forward option has the strike determined at some future time
  ///  <m>t \gt 0</m> as <m>K_t = \alpha\,S_t</m> for some constant <m>\alpha</m>,
  ///  and it expires at <m>T \gt t</m>
  ///  with the payoffs <m>(S_T - \alpha S_t)^+</m> for call
  ///  and <m>(\alpha S_t - S_T)^+</m> for put.</para>
  /// 
  /// <para>A ratchet option consists of the options with the strikes reset periodically.
  ///  It is essentially a series of consecutive forward start options.  The payoff is<math>
  ///    \sum_{i=1}^{n} ( S_{t_i} - \alpha_i S_{t_{i-1}})^+\text{ for call}
  ///    ,\qquad\sum_{i=1}^{n} ( \alpha_i S_{t_{i-1}} -S_{t_i})^+\text{ for put}
  /// </math>
  /// </para>
  /// 
  /// <para>A <b>cliquet options</b> is an options with the following payoffs<math>
  ///   \max\left\{\sum_{i=1}^n {\max\left[L_i,
  ///      \min\left(\frac{S_{t_i}}{S_{t_{i-1}}} - 1, U_i\right)\right]},
  ///   \mathrm{GlobalFloor}\right\}
  /// </math>where <m>L_i</m> and <m>U_i</m> are local floors and local caps, respectively.</para>
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class StockCliquetOption : Product
  {
    #region Constructors
    /// <inheritdoc />
    /// <summary>
    /// Constructor for cliquet stock option
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="resetDates">Reset dates</param>
    /// <param name="notionalPrice">NotionalPrice of stock</param>
    /// <param name="cap">Local cap rate in each segment</param>
    /// <param name="floor">Local floor rate in each segment</param>
    /// <param name="gFloor">Global floor rate </param>
    public StockCliquetOption(Dt effective, Currency ccy, Dt expiration, Dt[] resetDates, double notionalPrice,
      double cap, double floor, double gFloor) : base(effective, expiration, ccy)
    {
      ResetDates = resetDates;
      NotionalPrice = notionalPrice;
      CapRate = cap;
      FloorRate = floor;
      GlobalFloor = gFloor;
    }
    #endregion Constructors

    #region Methods

    /// <inheritdoc />
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
    public Dt Expiration
    {
      get { return Maturity; }
    }

    /// <summary>
    /// Reset dates
    /// </summary>
    [Category("Option")]
    public Dt[] ResetDates { get; set; }

    /// <summary>
    /// Notional Underlying Stock Price: Option Gain=Size x NotionalPrice x Option Return
    /// </summary>
    [Category("Option")]
    public double NotionalPrice { get; set; }

    /// <summary>
    /// Maximum allowed return rate for each segment 
    /// </summary>
    [Category("Option")]
    public double CapRate { get; set; }

    /// <summary>
    /// Minimum guaranteed return for each segment
    /// </summary>
    [Category("Option")]
    public double FloorRate { get; set; }

    /// <summary>
    /// Minimum guaranteed return for the total life cycle of option
    /// Since option is right, it has to be larger than zero 
    /// </summary>
    [Category("Option")]
    public double GlobalFloor { get; set; }

    #endregion Properties
  }
}
