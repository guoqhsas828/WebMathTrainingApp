/*
 * CD.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// CD (Certificates of Deposit, Fed Funds)
  /// </summary>
  /// <remarks>
  ///   <para>Short term securities that pay interest at maturity. They are quoted
  ///   on a simple interest basis.</para>
  ///   <para>CDs are issued at par and pay principle plus interest at maturity.</para>
  ///   <math>
  ///     Payment_T = C_cd * \frac{t_i}{T_cd}
  ///   </math>
  ///   <para>where</para>
  ///   <list type="bullet">
  ///			<item><description><m>Payment_T</m> is the payment at maturity</description></item>
  ///     <item><description><m>C_cd</m> is the anualised CD coupon</description></item>
  ///			<item><description><m>t_i</m> is the number of days from issue (effective) to maturity</description></item>
  ///			<item><description><m>T_cd</m> is the number of days in the coupon daycount period (eg 360)</description></item>
  ///   </list>
  ///   <para>Examples of interest at maturity money market securities include:</para>
  ///   <list type="bullet">
  ///     <item><description>Certificates of Deposit (CDs) of less than one year term</description></item>
  ///     <item><description>Fed Funds</description></item>
  ///   </list>
  ///
  ///   <para><b>Certificates of Deposit (CDs)</b></para>
  ///   <para>A certificate of deposit (CD) is a time deposit, a financial product commonly sold in
  ///   the United States by banks, thrift institutions, and credit unions.</para>
  ///   <para>CDs are similar to savings accounts in that they are insured and thus virtually risk
  ///   free; they are "money in the bank." In the USA, CDs are insured by the Federal Deposit
  ///   Insurance Corporation (FDIC) for banks and by the National Credit Union Administration (NCUA)
  ///   for credit unions. They are different from savings accounts in that the CD has a specific,
  ///   fixed term (often monthly, three months, six months, or one to five years) and, usually, a
  ///   fixed interest rate. It is intended that the CD be held until maturity, at which time the
  ///   money may be withdrawn together with the accrued interest.</para>
  ///   <para>In exchange for keeping the money on deposit for the agreed-on term, institutions
  ///   usually grant higher interest rates than they do on accounts from which money may be
  ///   withdrawn on demand, although this may not be the case in an inverted yield curve situation.
  ///   Fixed rates are common, but some institutions offer CDs with various forms of variable rates.
  ///   For example, in mid-2004, interest rates were expected to rise, many banks and credit unions
  ///   began to offer CDs with a "bump-up" feature. These allow for a single readjustment of the
  ///   interest rate, at a time of the consumer's choosing, during the term of the CD. Sometimes,
  ///   CDs that are indexed to the stock market, the bond market, or other indices are introduced.</para>
  /// 
  ///   <para><b>Fed Funds</b></para>
  ///   <para>In the United States, federal funds are overnight borrowings between banks and other
  ///   entities to maintain their bank reserves at the Federal Reserve. Banks keep reserves at Federal
  ///   Reserve Banks to meet their reserve requirements and to clear financial transactions. Transactions
  ///   in the federal funds market enable depository institutions with reserve balances in excess of
  ///   reserve requirements to lend reserves to institutions with reserve deficiencies. These loans are
  ///   usually made for one day only, that is, "overnight". The interest rate at which these deals are
  ///   done is called the federal funds rate. Federal funds are not collateralized; like eurodollars,
  ///   they are an unsecured interbank loan.</para>
  ///   <para>Federal funds transactions by regulated financial institutions neither increase nor
  ///   decrease total bank reserves. Instead, they redistribute reserves and enable otherwise idle
  ///   funds to yield a return. Banks may borrow these funds to avoid an overdraft (that is, the balance
  ///   going below reserve requirement) of their reserve account, or in order to meet the reserves
  ///   required to back their deposits. Federal funds are definitive money, meaning that they are
  ///   available for immediate spending, while checks and many other forms of money must be cleared
  ///   by banks and typically take several days before becoming available for spending.</para>
  ///   <para>Participants in the federal funds market include commercial banks, savings and loan
  ///   associations, government-sponsored enterprises, branches of foreign banks in the United States,
  ///   federal agencies, and securities firms. Many relatively small institutions that accumulate
  ///   reserves in excess of their requirements lend reserves overnight to money center and large
  ///   regional banks, as well as to foreign banks operating in the United States. Federal agencies
  ///   also lend idle funds in the federal funds market.</para>
  /// 
  ///   <para><i> .</i></para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CDPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class CD : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="effective">Effective date (issue date, date interest starts accruing)</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="coupon">Coupon of CD</param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <returns>Constructed CD</returns>
    public CD(Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount)
      : base(effective, maturity, ccy)
    {
      Coupon = coupon;
      DayCount = dayCount;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Annualised coupon
    /// </summary>
    public double Coupon { get; set; }

    /// <summary>
    /// Coupon accrual daycount
    /// </summary>
    public DayCount DayCount { get; set; }

    #endregion Properties
  }
}
