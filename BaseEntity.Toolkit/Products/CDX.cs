//
// CDX.cs
//  -2008. All rights reserved.
//

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// CDS Index (iTraxx and CDX) Funded and Unfunded Notes.
  /// </summary>
  /// <remarks>
  ///   <para>CDS Indices are contracts which provide default protection on a standardized portfolio of bonds or Credit Default Swaps.
  ///   CDS Indices are traded in both Swap (Unfunded) or Note (Funded) form.</para>
  ///   <para>Originally these indices developed from a set of competing products but now have been consolidated into a multi-dealer
  ///   set of indices known as CDX and iTraxx.</para>
  ///   <h1 align="center"><img src="CDXHistory.png" width="70%" /></h1>
  ///   <para>The primary indexes are North American and European Investment grade. Each consists of 125 equally weighted CDS for
  ///   terms of 5 or 10 years. A new on-the-run series begins every six months (March 20th and September 20th) at
  ///   which time the composition of the reference portfolio is reviewed and may be changed. By market convention, investors can
  ///   roll into the on the run series at mid market.</para>
  ///   <para>Indexes pay a fixed (deal) premium that is set at issuance. Most indices trade on a market quoted spread. On settlement
  ///   a upfront price is calculated based on the pv of the difference between the market spread and the deal spread using an
  ///   <see href="http://www.cdsmodel.com/cdsmodel/">ISDA CDS Standard Calculator</see>.</para>
  ///   <para>Some indices such as CDX.NA.HY and CDX.EM are quoted on an upfront price basis.</para>
  ///   <para>Indices provide protection on Bankruptcy and failure to pay which differs from the standard CDS. The maturity of the
  ///   index may also differ from the current standard CDS maturity.</para>
  /// 
  ///   <para><b>Credit Events</b></para>
  ///   <para>Upon the declaration of a credit by the ISDA Determinations Committee, the index will be reversioned, and trading in
  ///   the new index version will commence. The initial issuance is version 1 (e.g. iTraxx Europe Series 19 Version 1), and the
  ///   version is incremented for each name in the index that has defaulted.</para>
  ///   <para>In the event of a "Failure to Pay", or a "Bankruptcy" credit event, the protection seller makes a payment to the
  ///   protection buyer on the credit event settlement date. The size of the payment is equal to that which would be paid if
  ///   protection had been bought on a single name CDS with a notional scaled down by the constituent's weighting in the index.</para>
  ///   <para>In the event of a "Restructuring" credit event, the index is still reversioned. Instead of simply being settled,
  ///   however, a single name CDS is spun off which can then undergo the usual single name optional triggering process.</para>
  /// 
  ///   <para><b>Clearing</b></para>
  ///   <para>vHistorically, CDS indices have always been traded as a bilateral contracts directly between parties. This brings
  ///   with it the additional risk of counterparty default - where one party to a trade fails to meet its obligations under the
  ///   trade. To mitigate this risk, clearing through Central CounterParties (CCPs) was introduces. In this model, both parties
  ///   to the trade face the CCP, and all members of the CCP pay in to a fund to cover costs in the event that one member defaults.</para>
  ///   <para>Indices are currently cleared through several CCPs, with ICE Clear Credit[5] (formerly ICE Trust) and ICE Clear Europe,
  ///   and Chicago Merchantile Exchange (CME) launching in 2009, and LCH.Clearnet in 2012.</para>
  ///   <para>From March 2013, certain indices under the USA's CFTC's jurisdiction became mandated to clear on trade date.</para>
  /// 
  ///   <para><b>iTraxx Indices</b></para>
  ///   <list type="table">
  ///     <listHeader><term>Family</term><term>Type</term><term>Index name</term><term>No Entities</term><term>Description</term></listHeader>
  ///     <item><description>Europe</description><description>Benchmark Indices</description><description>iTraxx Europe</description><description>125</description><description>Most actively traded names in the six months prior to the index roll</description></item>
  ///     <item><description></description><description></description><description>iTraxx Europe HiVol</description><description>30</description><description>Highest spread (riskiest) non-financial names from iTraxx Europe index</description></item>
  ///     <item><description></description><description></description><description>iTraxx Europe Crossover</description><description>40</description><description>Sub-investment grade names</description></item>
  ///     <item><description></description><description></description><description>iTraxx LEVX</description><description>40</description><description>European 1st Lien Loan CDS</description></item>
  ///     <item><description></description><description>Sector Indices</description><description>iTraxx Non-Financials</description><description>100</description><description>Non-financial names</description></item>
  ///     <item><description></description><description></description><description>iTraxx Financials Senior</description><description>25</description><description>Senior subordination financial names</description></item>
  ///     <item><description></description><description></description><description>iTraxx Financials Sub</description><description>25</description><description>Junior subordination financial names</description></item>
  ///     <item><description></description><description></description><description>iTraxx TMT</description><description>20</description><description>Telecommunications, media and technology</description></item>
  ///     <item><description></description><description></description><description>iTraxx Industrials</description><description>20</description><description>Industrial names</description></item>
  ///     <item><description></description><description></description><description>iTraxx Energy</description><description>20</description><description>Energy industry names</description></item>
  ///     <item><description></description><description></description><description>iTraxx Consumers</description><description>30</description><description>Manufacturers of consumer products</description></item>
  ///     <item><description></description><description></description><description>iTraxx Autos</description><description>10</description><description>Automobile industry names</description></item>
  ///     <item><description>Asia</description><description></description><description>iTraxx Asia</description><description>50</description><description>Asia ex-Japan Investment Grade</description></item>
  ///     <item><description></description><description></description><description>iTraxx Asia HY</description><description>20</description><description>Asia ex-Japan High Yield</description></item>
  ///     <item><description></description><description></description><description>iTraxx Japan</description><description>50</description><description>Japan</description></item>
  ///     <item><description></description><description></description><description>iTraxx Australia</description><description>25</description><description>Australia</description></item>
  ///     <item><description>Sovereign</description><description></description><description>iTraxx SOVX West Europe</description><description>15</description><description>Sovereign West Europe CDS</description></item>
  ///     <item><description>Sovereign</description><description></description><description>iTraxx SOVX CEEMEA</description><description>15</description><description>Sovereign Central/East Europe, Middle East &amp; Africa</description></item>
  ///     <item><description>Sovereign</description><description></description><description>iTraxx SOVX Asia Pacific</description><description>10</description><description>Sovereign Asia Pacific</description></item>
  ///     <item><description>Sovereign</description><description></description><description>iTraxx SOVX Latin America</description><description>8</description><description>Sovereign Latin America</description></item>
  ///     <item><description>Sovereign</description><description></description><description>iTraxx SOVX IG</description><description></description><description>Sovereign Global Liquid Investment Grade</description></item>
  ///     <item><description>Sovereign</description><description></description><description>iTraxx SOVX G7</description><description></description><description>Sovereign G7</description></item>
  ///     <item><description>Sovereign</description><description></description><description>iTraxx SOVX BRIC</description><description></description><description>Sovereign Brazil, Russia, India, China</description></item>
  ///   </list>
  /// 
  ///   <para><b>CDX Indices</b></para>
  ///   <list type="table">
  ///     <listHeader><term>Index name</term><term>No Entities</term><term>Description</term></listHeader>
  ///     <item><description>CDX.NA.IG</description><description>125</description><description>Investment grade CDSs</description></item>
  ///     <item><description>CDX.NA.IG.HVOL</description><description>30</description><description>High Volatility investment grade CDSs</description></item>
  ///     <item><description>CDX.NA.HY</description><description>100</description><description>High Yield CDSs</description></item>
  ///     <item><description>CDX.NA.HY.BB</description><description>37</description><description>Index of high yield CDSs with a BB rating</description></item>
  ///     <item><description>CDX.NA.HY.B</description><description>46</description><description>Index of high yield CDSs with a B rating</description></item>
  ///     <item><description>CDX.NA.XO</description><description>35</description><description>CDSs that are at the crossover point between investment grade and junk</description></item>
  ///     <item><description>CDX.EM</description><description>14</description><description>Emerging market CDSs</description></item>
  ///     <item><description>CDX.EM Diversified</description>40<description></description><description>Emerging market CDSs</description></item>
  ///     <item><description>LCDX</description><description>100</description><description>NA First Lien Leverage Loans CDSs</description></item>
  ///   </list>
  ///   <para><i> .</i></para>
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing an CDX NA IG.3 Swap (Unfunded).</para>
  /// <code language="C#">
  ///   // Create a standard CDX. The Daycount defaults to Actual/360, the premium payment
  ///   // frequency defaults to Quarterly, the business day roll convention defaults to
  ///   // Following and the first premium payment date defaults to the next IMM date after
  ///   // the effective date.
  ///   CDX cdx = new CDX(
  ///     new Dt(20,9,2004),                       // Effective date
  ///     new Dt(20,9,2009),                       // Scheduled termination date
  ///     Currency.USD,                            // Currency is USD
  ///     0.00465,                                 // Deal premium is 46.5bp per annum
  ///     Calendar.NYB                             // Calendar is NY Banking
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class CDX : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Construct a CDX Product
    /// </summary>
    /// <remarks>
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Premium DayCount of Actual360.</description></item>
    ///     <item><description>Premium payment frequency of Quarterly.</description></item>
    ///     <item><description>Premium payment business day convention of Following.</description></item>
    ///     <item><description>The calendar is None.</description></item>
    ///     <item><description>The first premium payment date is the based on the IMM roll.</description></item>
    ///     <item><description>The index weights are equal.</description></item>
    ///   </list>
    /// </remarks>
    /// <example>
    /// <para>The following example demonstrates constructing an CDX NA IG.3 Swap (Unfunded).</para>
    /// <code language="C#">
    ///   // Create a standard CDX. The Daycount defaults to Actual/360, the premium payment
    ///   // frequency defaults to Quarterly, the business day roll convention defaults to
    ///   // Following and the first premium payment date defaults to the next IMM date after
    ///   // the effective date.
    ///   CDX cdx = new CDX(
    ///     new Dt(20,9,2004),                       // Effective date
    ///     new Dt(20,9,2009),                       // Scheduled termination date
    ///     Currency.USD,                            // Currency is USD
    ///     0.00465,                                 // Deal premium is 46.5bp per annum
    ///     Calendar.NYB                             // Calendar is NY Banking
    ///   );
    /// </code>
    /// </example>
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    public CDX( Dt effective, Dt maturity, Currency ccy, double premium, Calendar cal )
      : this(effective, maturity, ccy, premium, DayCount.Actual360, Frequency.Quarterly,
      BDConvention.Following, cal, null)
    {}

    /// <summary>
    ///   Construct a CDX Product
    /// </summary>
    /// <remarks>
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Premium DayCount of Actual360.</description></item>
    ///     <item><description>Premium payment frequency of Quarterly.</description></item>
    ///     <item><description>Premium payment business day convention of Following.</description></item>
    ///     <item><description>The calendar is None.</description></item>
    ///     <item><description>The first premium payment date is the based on the IMM roll.</description></item>
    ///     <item><description>The index weights are equal.</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <param name="weights">Weights for each name (should sum to one).</param>
    public CDX( Dt effective, Dt maturity, Currency ccy, double premium, Calendar cal, double[] weights )
      : this(effective, maturity, ccy, premium, DayCount.Actual360, Frequency.Quarterly,
      BDConvention.Following, cal, weights)
    {}

    /// <summary>
    ///   Construct a CDX Product
    /// </summary>
    /// <remarks>
    ///   <para>The first premium date defaults to the next IMM Roll date after the effective date.</para>
    /// </remarks>
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    public CDX(
      Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount, Frequency freq,
      BDConvention roll, Calendar cal
      )
      : this(effective, maturity, ccy, premium, dayCount, freq, roll, cal, null)
    {}

    /// <summary>
    ///   Construct a CDX Product
    /// </summary>
    /// <remarks>
    ///   <para>The first premium date defaults to the next IMM Roll date after the effective date.</para>
    /// </remarks>
    /// <param name="effective">Effective date (date premium accrual and protection start) </param>
    /// <param name="maturity">Scheduled termination (maturity) date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised original issue or deal premium of index</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <param name="weights">Weights for each name (should sum to one).</param>
    public CDX(
      Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount, Frequency freq,
      BDConvention roll, Calendar cal, double[] weights
      )
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, roll, cal, CycleRule.None,
        CashflowFlag.IncludeDefaultDate | CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeMaturityAccrual)
    {
      Premium = premium;
      DayCount = dayCount;
      Weights = weights;
      return;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CDX obj = (CDX)base.Clone();

      obj.Weights = (Weights != null) ? (double[])Weights.Clone() : null;

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (Premium < 0.0 || Premium > 200.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", Premium));

      // Validate weights
      if( Weights != null )
      {
        double total = 0.0;
        foreach( double d in Weights )
          total += d;
        if( !total.AlmostEquals(1.0) )
          InvalidValue.AddError(errors, this, "Weights", String.Format("Index weights must total 100%, Not {0}", total));
      }

      return;
    }

    /// <summary>
    /// Get first coupon date
    /// </summary>
    /// <returns></returns>
    protected override Dt GetDefaultFirstCoupon()
    {
      return CDS.GetDefaultFirstPremiumDate(Effective, Maturity);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Cdx type
    /// </summary>
    [Category("Base")]
    public CdxType CdxType { get; set; }

    /// <summary>
    ///   First premium payment date
    /// </summary>
    /// <remarks>
    ///   <para>If the first premium date has not been set, calculates the default on the fly.</para>
    /// </remarks>
    [Category("Base")]
    public Dt FirstPrem
    {
      get { return FirstCoupon; }
      set { FirstCoupon = value; }
    }

    /// <summary>
    ///   Deal or original issue premium of index as a number (100bp = 0.01).
    ///   For a funded floating note this is the spread.
    /// </summary>
    [Category("Base")]
    public double Premium { get; set; }

    /// <summary>
    ///   Daycount of premium
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

    /// <summary>
    ///   Note is in funded form (principal exchanged)
    /// </summary>
    [Category("Base")]
    public bool Funded { get; set; }

    /// <summary>
    ///   Array of weights associated with each name (may be null for equally weighted)
    /// </summary>
    [Category("Base")]
    public double[] Weights { get; set; }

    ///<summary>
    /// Date the version of the index was issued. If greater than the index's EffectiveDate
    /// the controls ....
    /// <preliminary>
    /// For internal use only in 9.0.
    /// </preliminary>
    ///</summary>
    /// <exclude/>
    [Category("Base")]
    public Dt AnnexDate { get; set; }

    #endregion Properties
  } // class CDX
}
