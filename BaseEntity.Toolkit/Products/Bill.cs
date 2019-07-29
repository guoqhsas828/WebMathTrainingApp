//
// Bill.cs
//  -2011. All rights reserved.
//

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Bill (TBill, discount bill, commercial paper, bankers acceptance)
  /// </summary>
  /// <remarks>
  ///   <para>Discount securities are traded on a discount and pay par at maturity. They are quoted
  ///   on a discount yield.</para>
  ///   <math>
  ///     \text{Discount Yield (\%)} = \frac{\text{Face Value} - \text{Purchase Price}}{\text{Face Value}} \times \frac{\text{360}}{\text{Days Till Maturity}} \times 100 \text{ (\%)}
  ///   </math>
  ///   <para>Examples of discount securities include:</para>
  ///   <list type="bullet">
  ///     <item><description>US Treasury Bills</description></item>
  ///     <item><description>Agency Discount Bills</description></item>
  ///     <item><description>Commercial Paper</description></item>
  ///     <item><description>Bankers Acceptance</description></item>
  ///   </list>
  /// 
  ///   <para><b>US Treasury Bills</b></para>
  ///   <para>Treasury bills (or T-Bills) mature in one year or less. Like zero-coupon bonds, they do
  ///   not pay interest prior to maturity; instead they are sold at a discount of the par value to
  ///   create a positive yield to maturity. Many regard Treasury bills as the least risky investment
  ///   available to U.S. investors.</para>
  ///   <para>Regular weekly T-Bills are commonly issued with maturity dates of 28 days (or 4 weeks,
  ///   about a month), 91 days (or 13 weeks, about 3 months), 182 days (or 26 weeks, about 6 months),
  ///   and 364 days (or 52 weeks, about 1 year). Treasury bills are sold by single-price auctions
  ///   held weekly. Offering amounts for 13-week and 26-week bills are announced each Thursday for
  ///   auction, usually at 11:30 a.m., on the following Monday and settlement, or issuance, on
  ///   Thursday. Offering amounts for 4-week bills are announced on Monday for auction the next day,
  ///   Tuesday, usually at 11:30 a.m., and issuance on Thursday. Offering amounts for 52-week bills
  ///   are announced every fourth Thursday for auction the next Tuesday, usually at 11:30 am, and
  ///   issuance on Thursday. Purchase orders at TreasuryDirect must be entered before 11:00 on
  ///   the Monday of the auction. The minimum purchase, effective April 7, 2008, is $100. (This
  ///   amount formerly had been $1,000.) Mature T-bills are also redeemed on each Thursday. Banks
  ///   and financial institutions, especially primary dealers, are the largest purchasers of T-bills.</para>
  ///   <para>Like other securities, individual issues of T-bills are identified with a unique CUSIP
  ///   number. The 13-week bill issued three months after a 26-week bill is considered a re-opening
  ///   of the 26-week bill and is given the same CUSIP number. The 4-week bill issued two months
  ///   after that and maturing on the same day is also considered a re-opening of the 26-week bill
  ///   and shares the same CUSIP number. For example, the 26-week bill issued on March 22, 2007, and
  ///   maturing on September 20, 2007, has the same CUSIP number (912795A27) as the 13-week bill issued
  ///   on June 21, 2007, and maturing on September 20, 2007, and as the 4-week bill issued on August 23,
  ///   2007 that matures on September 20, 2007.</para>
  ///   <para>During periods when Treasury cash balances are particularly low, the Treasury may sell
  ///   cash management bills (or CMBs). These are sold at a discount and by auction just like weekly
  ///   Treasury bills. They differ in that they are irregular in amount, term (often less than 21 days),
  ///   and day of the week for auction, issuance, and maturity. When CMBs mature on the same day as a
  ///   regular weekly bill, usually Thursday, they are said to be on-cycle. The CMB is considered
  ///   another reopening of the bill and has the same CUSIP. When CMBs mature on any other day, they
  ///   are off-cycle and have a different CUSIP number.</para>
  /// 
  ///   <para><b>UK Treasury Bills</b></para>
  ///   <para>Treasury bills are routinely issued at weekly tenders, held by the DMO on the last
  ///   business day of each week (i.e. usually on Fridays), for settlement on the following business
  ///   day. Treasury bills can be issued with maturities of 1 month (approximately 28 days), 3 months
  ///   (approximately 91 days), 6 months (approximately 182 days) or 12 months (up to 364 days),
  ///   although to date no 12 month tenders have been held. Members of the public wishing to purchase
  ///   Treasury bills at the tenders will have to do so through one of the Treasury bill Primary
  ///   Participants and purchase a minimum of £500,000 nominal of bills.</para>
  /// 
  ///   <para><b>Commercial Paper</b></para>
  ///   <para>Commercial paper (CP) consists of short-term, promissory notes issued primarily by
  ///   corporations. Maturities range up to 270 days but average about 30 days. Many companies use
  ///   CP to raise cash needed for current transactions, and many find it to be a lower-cost
  ///   alternative to bank loans.</para>
  /// 
  ///   <para><i> .</i></para>
  /// </remarks>
  /// <seealso href="http://www.treasurydirect.gov/indiv/products/prod_tbills_glance.htm">Treasury Bills. U.S. Department of Treasury, Bureau of Public Debt. April 22, 2011</seealso>
  /// <seealso href="http://en.wikipedia.org/wiki/United_States_Treasury_security">United States Treasury Security. Wikipedia</seealso>
  /// <seealso href="http://www.dmo.gov.uk/index.aspx?page=tbills/about_tbills">About Treasury Bills. UK Debt Management Office</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.BillPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class Bill : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <returns>Constructed bill</returns>
    public Bill(Dt effective, Dt maturity, Currency ccy)
      : base(effective, maturity, ccy)
    {
      Notional = 1.0;
    }

    #endregion Constructors
  }
}
