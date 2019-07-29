/*
 * LCDS.cs
 *
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Loan Credit Default Swap product (LCDS).
  /// </summary>
  /// <remarks>
  ///   <para>Loan Credit Default Swaps (LCDS) are OTC contracts where a protection buyer pays a periodic
  ///   fee (usually quarterly) to a protection seller for 'insurance' against a credit event.</para>
  ///   <para>An LCDS is similar to a <see cref="CDS">Corporate Credit Default Swap</see>
  ///   where the underlying reference asset is a leveraged loan. Like a regular CDS, the Protection Buyer
  ///   pays a periodic premium and will receive a payment from the Protection Seller if a credit
  ///   event (e,g, bankruptcy) occurs to a Reference Credit. Unlike a regular corporate credit default
  ///   swap, there is also the risk that the underlying loan will be prepayed. If the underlying loan
  ///   prepays and there are no suitable substibute reference assets, the LCDS will be cancelled.</para>
  ///   <h1 align="center"><img src="LCDS.png" width="80%"/></h1>
  ///   <para>Ctpy A - The Protection Buyer (also referred to as the Fixed Rate Payer) pays the
  ///   premium through periodic interest payments (typically expressed in basis points).</para>
  ///   <para>Ctpy B - The Protection Seller (also referred to as the Floating Rate Payer) makes
  ///   a Credit Event Payment if there is a Credit Event on the Reference Credit.</para>
  ///   <para>Key differences between corporate CDS and LCDS include:</para>
  ///   <list type="bullet">
  ///     <item><description>Different point in capital structure (secured loan Vs unsecured bond)</description></item>
  ///     <item><description>Cancelable if underlying loan refinanced and no suitable substitution found</description></item>
  ///     <item><description>Recoveries generally much higher than CDS (75% Vs 40% for corporate)</description></item>
  ///   </list>
  ///   <para>European versions of LCDS initially started as cancelleable on any loan repayment
  ///   migrated towards the US standard of cancelleable only if no suitable substitution found.</para>
  ///
  ///   <para><b>LCDS Standard Contract Terms</b></para>
  ///   <para>LCDS trade under standardized ISDA documentation and credit lines/collateral agreements.
  ///   Definitions of the Credit Event, the underlying credit and/or reference entity, the maturity,
  ///   fee structure and nature of payment on a credit event vary. Fee may be paid in advance or
  ///   periodically. Most common is quarterly. Most liquid maturity is 5 years.</para>
  ///   <para>Each individual LCDS requires 6 specific terms (like standard CDS):</para>
	///   <list type="table">
	///     <listheader><term>Name</term><description>Description</description></listheader>
	///     <item><term>Notional Amount</term><description>Risk Exposure</description></item>
	///     <item><term>Reference Entity</term><description>Credit to be Hedged or Assumed</description></item>
	///     <item><term>Reference Obligation</term><description>Establish Seniority of Credit Risk. Specified or "Secured List”</description></item>
	///     <item><term>Maturity</term><description>Standard 5yr Mar, Jun, Sep, Dec</description></item>
	///     <item><term>Credit Events</term><description>Bankruptcy, Failure to Pay in US; Bankruptcy, Failure to Pay and Restructuring in Europe)</description></item>
  ///     <item><term>Premium</term><description>Fixed Rate to be Paid/Received</description></item>
	///   </list>
  ///   <para>Deliverable Obligations must be Syndicated Secured obligations</para>
  ///   <para>Standard maturity dates follow IMM roll dates (March 20th, June 20th, September 20th
  ///   and December 20th). The first premium period is a short stub which rolls 30 days before
  ///   the next IMM date. Variations include Defined payment on default (digital) Partial or
  ///   full up-front payment rather than running premium.</para>
  ///
  ///   <para><b>Public Vs Private</b></para>
  ///   <para>Many loan agreements include covenants that require the borrower to disclose
  ///   private information or syndicate information.</para>
  ///   <para>Current documentation has included amendments to standard ISDA representations
  ///   and warranties where parties acknowledge that one party may be contractually
  ///   prohibited from sharing information under the relevant loan documentation.</para>
  ///   <para>Loan CDS are being traded on both the private and public side.</para>
  ///   <para>The U.S. LCDS contract uses the secured list and a polling mechanism
  ///   to classify loans as Syndicated Secured without the need for all LCDS market
  ///   participants to access loan documentation.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.LCDSCashflowPricer">Cashflow Pricer</seealso>
  ///
  /// <example>
  /// <para>The following example demonstrates constructing a Loan Credit Default Swap.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = Dt.Today();                                  // Effective date is today
  ///   Dt maturity = Dt.CDSMaturity(effectiveDate, 5, TimeUnit.Years); // Maturity date is standard 5Yr CDS maturity
  ///
  ///   LCDS lcds =
  ///     new LCDS( effectiveDate,                                  // Effective date
  ///               maturityDate,                                   // Maturity date
  ///               Currency.EUR,                                   // Currency is Euro
  ///               0.02,                                           // Premium is 200bp
  ///               DayCount.Actual360,                             // Acrual Daycount is Actual/360
  ///               Frequency.Quarterly,                            // Quarterly payment frequency
  ///               Calendar.TGT,                                   // Calendar is Target
  ///             );
  ///
  /// </code>
  /// </example>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class LCDS : CDS
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturity">Maturity or scheduled temination date</param>
    /// <param name="ccy">Currency of premium and recovery payments</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="freq">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="cal">Calendar for premium payments</param>
    ///
    public
    LCDS(Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, maturity, ccy, premium, dayCount, freq, roll, cal)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturity">Maturity or scheduled temination date</param>
    /// <param name="ccy">Currency of premium and recovery payments</param>
    /// <param name="firstPrem">First premium payment date</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="freq">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="cal">Calendar for premium payments</param>
    ///
    public
    LCDS(Dt effective, Dt maturity, Currency ccy, Dt firstPrem, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, maturity, ccy, firstPrem, premium, dayCount, freq, roll, cal)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturity">Maturity or scheduled temination date</param>
    /// <param name="ccy">Currency of premium and recovery payments</param>
    /// <param name="firstPrem">First premium payment date</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="freq">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="cal">Calendar for premium payments</param>
    /// <param name="fee">Upfront fee in percent (0.1 =10pc)</param>
    /// <param name="feeSettle">Upfront fee payment date</param>
    ///
    public
    LCDS(Dt effective, Dt maturity, Currency ccy, Dt firstPrem, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal, double fee, Dt feeSettle)
      : base(effective, maturity, ccy, firstPrem, premium, dayCount, freq, roll, cal, fee, feeSettle)
    {}

    #endregion Constructors

  } // class LCDS
}
