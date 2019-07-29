/*
 * LCDX.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  ///   Loan CDS Index (LCDX/LevX) Notes
  /// </summary>
  /// <remarks>
  ///   <para>LCDX is an index with 100 equally-weighted underlying single-name
  ///   <see cref="LCDS">loan-only credit default swaps (LCDS)</see>.
  ///   The default swaps each reference an entity whose loans trade in 
  ///   the secondary leveraged loan market, and in the LCDS market.</para>
  ///   <para>LCDX is simply the buying or selling of protection on the 100 names that comprise the 
  ///   LCDX portfolio. It literally is the buying and selling of 100 single-name LCDS, except that 
  ///   there is a fixed coupon. Similarly to LCDS, the index is an unfunded product.</para>
  ///   <h1 align="center"><img src="LCDX.png" width="80%"/></h1>
  ///   <para>The Protection Seller (also referred to as the Floating Rate Payer) makes
  ///   a Credit Event Payment if there is a Credit Event on the Reference Credit.</para>
  ///   <para>The Protection Buyer (also referred to as the Fixed Rate Payer) pays the
  ///   premium through periodic interest payments (typically expressed in basis points).</para>
  ///   <para>An LCDS is similar to a <see cref="CDS">Corporate Credit Default Swap</see>
  ///   where the underlying reference asset is a leveraged loan. Like a regular CDS, the Protection Buyer
  ///   pays a fixed (deal) premium that is set at issuance and receives a payment from the protection
  ///   seller if a credit event (e.g. bankruptcy) occures to a Reference Credit.</para>
  ///   <para>Unlike with traditional unsecured CDS, the LCDS contract is cancellable if an issuer repays 
  ///   its debt without issuing new senior syndicated secured debt. This change was made in recognition of
  ///   this being a regular occurrence in the loan market, especially when an issuers rating moves from 
  ///   high-yield to investment grade. When a credit has repaid its debt, the name is polled for and removed 
  ///   from the syndicated secured list. This triggers an automatic 30 business day search period in which 
  ///   new debt is searched for (this allows time for companies who re-finance their debt to get their new debt
  ///   into the market) and if after 30 days no new debt is found the name is removed from the index with a 
  ///   dealer vote. </para>
  ///   <para>LCDX trade on a quoted clean price. On settlement a upfront price is
  ///   calculated as 100 - price.</para>
  ///   <para>Key differences between corporate CDS and LCDS include:</para>
  ///   <list type="bullet">
  ///     <item><description>Different point in capital structure (secured loan Vs unsecured bond)</description></item>
  ///     <item><description>Cancelable if underlying loan refinanced and no suitable substitution found</description></item>
  ///     <item><description>Recoveries generally much higher than CDS (75% Vs 40% for corporate)</description></item>
  ///   </list>
  ///   <para>European versions of LCDS initially started as cancelleable on any loan repayment
  ///   migrated towards the US standard of cancelleable only if no suitable substitution found.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.LCDXPricer">LCDX Pricer</seealso>
  ///
  /// <example>
  /// <para>The following example demonstrates constructing an LCDX NA Series 8 Swap</para>
  /// <code language="C#">
  ///   Dt effectiveDate = new Dt(22,5,2007);                       // Effective date of index
  ///   Dt maturity = new Dt(20,6,2012);                            // Scheduled termination date
  ///
  ///   // Create a standard LCDX. The Daycount defaults to Actual/360, the premium payment
  ///   // frequency defaults to Quarterly, the business day roll convention defaults to
  ///   // Following and the first premium payment date defaults to the next IMM date after
  ///   // the effective date.
  ///   LCDX lcdx =
  ///     new LCDX( effectiveDate,                                  // Effective date
  ///               maturityDate,                                   // Scheduled termination
  ///               Currency.USD,                                   // Currency is USD
  ///               0.0120,                                         // Deal premium is 120bp per annum
  ///               Calendar.NYB                                    // Calendar is NY Banking
  ///             );
  /// </code>
  /// </example>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class LCDX : CDX
  {
    #region Constructors

    /// <summary>
    ///   Construct a LCDX Product
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Daycount defaults to Actual360, the frequency defaults to Quarterly, the
    ///   roll convention defaults to Following, and the first premium date defaults to the next
    ///   IMM Roll date after the effective date.</para>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    ///
    public
    LCDX(Dt effective,
         Dt maturity,
         Currency ccy,
         double premium,
         Calendar cal)
      : base(effective, maturity, ccy, premium, cal)
    {}

    /// <summary>
    ///   Construct a LCDX Product
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Daycount defaults to Actual360, the frequency defaults to Quarterly, the
    ///   roll convention defaults to Following, and the first premium date defaults to the next
    ///   IMM Roll date after the effective date.</para>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <param name="weights">Weights for each name (should sum to one).</param>
    ///
    public
    LCDX(Dt effective,
         Dt maturity,
         Currency ccy,
         double premium,
         Calendar cal,
         double[] weights)
      : base(effective, maturity, ccy, premium, cal, weights)
    {}

    /// <summary>
    ///   Construct a LCDX Product
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The first premium date defaults to the next IMM Roll date after the effective date.</para>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    ///
    public
    LCDX(Dt effective,
         Dt maturity,
         Currency ccy,
         double premium,
         DayCount dayCount,
         Frequency freq,
         BDConvention roll,
         Calendar cal)
      : base(effective, maturity, ccy, premium, dayCount, freq, roll, cal)
    {}

    /// <summary>
    ///   Construct a LCDX Product
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The first premium date defaults to the next IMM Roll date after the effective date.</para>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <param name="weights">Weights for each name (should sum to one).</param>
    ///
    public
    LCDX(Dt effective,
         Dt maturity,
         Currency ccy,
         double premium,
         DayCount dayCount,
         Frequency freq,
         BDConvention roll,
         Calendar cal,
         double[] weights)
      : base(effective, maturity, ccy, premium, dayCount, freq, roll, cal, weights)
    {}

    #endregion Constructors

  } // class LCDX
}
