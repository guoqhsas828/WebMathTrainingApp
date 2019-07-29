/*
 * CDSOption.cs
 *
 */

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  /// Option to enter into a CDS.
  /// </summary>
  /// <remarks>
  /// <para>A Default Swaption or CDS Option is the option to buy or sell
  /// single name CDS protection at a fixed spread on a future date.</para>
  /// <para>The option to buy protection is called a protection call or payer
  /// default swaption and the right to sell protection is called a protection
  /// put or receiver default swaption.</para>
  /// <para>CDS Options are typically European and are quoted in cents upfront
  /// with typically T+3 settlement.</para>
  /// 
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <seealso cref="SingleAssetOptionBase"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CDSOptionPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class CDSOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor for standard Option on Corporate CDS
    /// </summary>
    /// <remarks>
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Premium DayCount of Actual360.</description></item>
    ///     <item><description>Premium payment frequency of Quarterly.</description></item>
    ///     <item><description>Premium payment business day convention of Following.</description></item>
    ///     <item><description>The option style is European</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="effective">Effective date of option</param>
    /// <param name="maturityInYears">CDS years to maturity or scheduled termination date from the effective date (eg 5)</param>
    /// <param name="cal">CDS calendar for premium payments</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="strike">Strike price</param>
    public
    CDSOption(Dt effective, int maturityInYears, Calendar cal, Dt expiration, PayerReceiver type, double strike)
      : base(effective, Currency.None, new CDS(expiration, maturityInYears, strike, cal),
      expiration, (type == PayerReceiver.Payer) ? OptionType.Put : OptionType.Call, OptionStyle.European, strike)
    {
      Knockout = true;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="effective">Effective date of option</param>
    /// <param name="maturity">CDS maturity date</param>
    /// <param name="ccy">CDS currency</param>
    /// <param name="dayCount">CDS daycount of premium accrual</param>
    /// <param name="freq">CDS frequency (per year) of premium payments</param>
    /// <param name="roll">CDS ISDA business day convention for premium payments</param>
    /// <param name="cal">CDS calendar for premium payments</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public
    CDSOption(Dt effective, Dt maturity, Currency ccy, DayCount dayCount,
              Frequency freq, BDConvention roll, Calendar cal,
              Dt expiration, PayerReceiver type, OptionStyle style, double strike)
      : base(effective, ccy,
             new CDS(expiration, maturity, ccy, strike, dayCount, freq, roll, cal),
             expiration, (type == PayerReceiver.Payer) ? OptionType.Put : OptionType.Call, style, strike)
    {
      Knockout = true;
    }
    
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying CDS</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option payer/receiver type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public
    CDSOption(Dt effective, Currency ccy, CDS underlying,
              Dt expiration, PayerReceiver type, OptionStyle style, double strike)
      : base(effective,ccy, underlying, expiration, (type == PayerReceiver.Payer) ? OptionType.Put : OptionType.Call, style, strike)
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying CDS
    /// </summary>
    [Category("Underlying")]
    public CDS CDS
    {
      get { return (CDS)Underlying; }
    }

    /// <summary>
    /// Indicating if knock out on default
    /// </summary>
    public bool Knockout { get; set; }

    #endregion Properties
  }
}
