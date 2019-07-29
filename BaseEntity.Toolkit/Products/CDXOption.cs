// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option to enter into a CDX/iTraxx.
  /// </summary>
  /// <remarks>
  /// <para>A CDX/iTraxx Option is the option to buy or sell CDX/iTraxx protection
  /// at a fixed spread on a future date.</para>
  /// <para>The option to buy protection is called a protection call or payer
  /// default swaption and the right to sell protection is called a protection
  /// put or receiver default swaption.</para>
  /// <para>CDX/iTraxx options have fixed expiry dates that match the index
  /// coupon dates (March 20, June 20, September 20 and December 20), although other
  /// maturities are available. The options are European-style meaning they can only
  /// be exercised on the expiry date. At inception, the option buyer pays an upfront
  /// premium to the option seller (T+3 settlement).</para>
  /// <para>Both CDX and ITraxx options are usually quote the strike of an option as a
  /// basis point spreads amount. The exception is CDX.NA High Yield, which is
  /// quoted with a strike price, since the index trades on a price rather than spread
  /// basis.</para>
  ///
  /// <para><b>CDX/iTraxx Option standard terms</b></para>
  /// <list type="table">
  ///   <item><term>Option Style</term><description>European</description></item>
  ///   <item><term>Premium</term><description>Quoted in cents upfront</description></item>
  ///   <item><term>Premium payment date</term><description>Trade date + 3 business days</description></item>
  ///   <item><term>Expiration time</term><description>1am New York time, 4pm London time</description></item>
  ///   <item><term>Settlement</term><description>Physical</description></item>
  ///   <item><term>Settlement terms</term><description>Expiry + 3 business days</description></item>
  ///   <item><term>Settlement amount</term><description></description></item>
  ///   <item><term>a. if no credit events before expiry</term>
  ///     <description>Settlement by buying or selling the index at strike at expiry</description></item>
  ///   <item><term>b. if one or more credit events before expiry</term>
  ///     <description>Settlement by buying or selling the index at strike at expiry.
  ///     Subsequently, protection buyer triggers the contract in regard to any
  ///     defaulted credits under the standard procedures.</description></item>
  /// </list>
  /// <para>CDX/iTraxx options do not “Knockout” if there is a default on an underlying name.
  /// Standard CDX/iTraxx options do not roll onto the "on-the-run" index, but remain with the
  /// referenced series. If a name defaults, an investor’s contract is on the original series
  /// that includes the defaulted name. An investor who bought a payer option would be
  /// able to exercise on the defaulted name, once they were entered into a long protection
  /// position on the index at the option expiry. Investors typically use the CDS settlement
  /// protocol to settle the defaulted name.</para>
  ///
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <seealso cref="SingleAssetOptionBase"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CDXOptionPricer"/>
  /// <example>
  /// <code language="C#">
  ///   // Create underlying credit index
  ///   CDX note = new CDX(
  ///     new Dt(20, 6, 2004),                 // Effective date of the CDS index
  ///     new Dt(20, 6, 2009),                 // Maturity date of the CDS index
  ///     Currency.USD,                        // Currency of the CDS index
  ///     40/10000,                            // CDS index premium (40bp)
  ///     DayCount.Actual360,                  // Daycount of the CDS index premium
  ///     Frequency.Quarterly,                 // Payment frequency of the CDS index premium
  ///     BDConvention.Following,              // Business day convention of the CDS index
  ///     Calendar.NYB                         // Calendar for the CDS Index
  ///   );
  ///   // Create the credit index option
  ///   CDXOption cdxOption = new CDXOption(
  ///     new Dt(20, 9, 2004),                 // Effective date of option
  ///     Currency.USD,                        // Currency of the CDS
  ///     note,                                // Underlying index note
  ///     new Dt(20, 11, 2004),                // Option expiration
  ///     PayerReceiver.Payer,                 // Option type
  ///     OptionStyle.European,                // Option style
  ///     0.0075,                              // Option strike of 75bp
  ///     false );                             // Strike spreads instead of values
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class CDXOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor for Option on CDX
    /// </summary>
    /// <param name="effective">Effective date of option</param>
    /// <param name="expiration">Expiration date of option</param>
    /// <param name="cdxEffective">CDX effective date</param>
    /// <param name="cdxMaturity">CDX maturity date</param>
    /// <param name="currency">Currency</param>
    /// <param name="dealPremium">Deal or original index premium in raw number (0.02 means 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="freq">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="cal">Calendar for premium payments</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="strikeIsPrice">If true, strike is interpreted as price; else, as spread</param>
    /// <param name="indexFactor">The index factor at the time when option is struck.  If NaN, to be determined by pricer based on credit curves.</param>
    public CDXOption(Dt effective, Dt expiration, Dt cdxEffective, Dt cdxMaturity,
                     Currency currency, double dealPremium, DayCount dayCount, Frequency freq, BDConvention roll,
                     Calendar cal, PayerReceiver type, OptionStyle style, double strike, bool strikeIsPrice, double indexFactor = Double.NaN)
      : base(effective, currency,
             new CDX(cdxEffective, cdxMaturity, currency, dealPremium, dayCount, freq, roll, cal),
             expiration, (type == PayerReceiver.Payer) ? OptionType.Put : OptionType.Call, style, strike)
    {
      StrikeIsPrice = strikeIsPrice;
      IndexFactor = indexFactor;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying CDS Index</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public CDXOption(Dt effective, Currency ccy, CDX underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(effective, ccy, underlying, expiration, type, style, strike)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <example>
    /// <code language="C#">
    ///   // Create underlying credit index
    ///   CDX note = new CDX(
    ///     new Dt(20, 6, 2004),                 // Effective date of the CDS index
    ///     new Dt(20, 6, 2009),                 // Maturity date of the CDS index
    ///     Currency.USD,                        // Currency of the CDS index
    ///     40/10000,                            // CDS index premium (40bp)
    ///     DayCount.Actual360,                  // Daycount of the CDS index premium
    ///     Frequency.Quarterly,                 // Payment frequency of the CDS index premium
    ///     BDConvention.Following,              // Business day convention of the CDS index
    ///     Calendar.NYB                         // Calendar for the CDS Index
    ///   );
    ///   // Create the credit index option
    ///   CDXOption cdxOption = new CDXOption(
    ///     new Dt(20, 9, 2004),                 // Effective date of option
    ///     Currency.USD,                        // Currency of the CDS
    ///     note,                                // Underlying index note
    ///     new Dt(20, 11, 2004),                // Option expiration
    ///     PayerReceiver.Payer,                 // Option type
    ///     OptionStyle.European,                // Option style
    ///     0.0075,                              // Option strike of 75bp
    ///     false );                             // Strike spreads instead of values
    /// </code>
    /// </example>
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying CDS Index</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike on spread or market price</param>
    /// <param name="strikeIsPrice">If true, strike is interpreted as price; else, as spread</param>
    /// <param name="indexFactor">The index factor at the time when option is struck.  If NaN, to be determined by pricer based on credit curves.</param>
    public CDXOption(Dt effective, Currency ccy, CDX underlying, Dt expiration, PayerReceiver type,
      OptionStyle style, double strike, bool strikeIsPrice, double indexFactor = Double.NaN)
      : base(effective, ccy, underlying, expiration,
             (type == PayerReceiver.Payer) ? OptionType.Put : OptionType.Call,
             style, strike)
    {
      StrikeIsPrice = strikeIsPrice;
      IndexFactor = indexFactor;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Underlying credit index
    /// </summary>
    [Category("Underlying")]
    public CDX CDX
    {
      get { return (CDX)Underlying; }
    }

    /// <summary>
    ///   Underlying credit index factor at the time when option is struck
    /// </summary>
    /// <remarks>
    ///   If this value is not supplied (as indicated by value NaN),
    ///   the credit index option pricer will try to determine it
    ///   from the credit curves, or simply assume it is 100%
    ///   when the credit curves are not available.</remarks>
    [Category("Underlying")]
    internal double IndexFactor { get; }

    /// <summary>
    ///   Underlying CDO tranche
    /// </summary>
    [Category("Option")]
    public bool StrikeIsPrice { get; set; }

    #endregion Properties
  }
}