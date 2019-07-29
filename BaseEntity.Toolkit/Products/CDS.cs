//
// CDS.cs
//  -2008. All rights reserved.
//
// TBD: Issue date is effective date for CDS, maturity date is scheduledTerminationDate. RD Dec04

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Credit Default Swap product (CDS)
  /// </summary>
  /// <remarks>
  ///   <para>Credit Default swaps are OTC contracts where a protection buyer pays a periodic
  ///   fee (usually quarterly) to a protection seller for 'insurance' against a credit event.</para>
  ///   <para>A Credit Default Swap is essentially an insurance contract whereby a Protection Buyer
  ///   pays a periodic premium and will receive a payment from the Protection Seller if a credit
  ///   event (e,g, bankruptcy) occurs to a Reference Credit.</para>
  ///   <h1 align="center"><img src="CDS.png" width="50%" /></h1>
  ///   <para>Ctpy A - The Protection Buyer (also referred to as the Fixed Rate Payer) pays the
  ///   premium through periodic interest payments (typically expressed in basis points).</para>
  ///   <para>Ctpy B - The Protection Seller (also referred to as the Floating Rate Payer) makes
  ///   a Credit Event Payment if there is a Credit Event on the Reference Credit.</para>
  ///   <para>ISDA provides standard documentation for Credit Default Swaps. Definitions of the
  ///   Credit Event, the underlying credit and/or reference entity, the maturity, fee structure
  ///   and nature of payment on a credit event vary. Underlying credit may be a class of assets
  ///   or an individual security. Fee may be paid in advance or periodically. Most common is
  ///   quarterly. Most liquid maturity is 5 years followed by 10 years.</para>
  ///   <para>Standard maturity dates follow IMM roll dates (March 20th, June 20th, September 20th
  ///   and December 20th). The first premium period is a short stub which rolls 30 days before
  ///   the next IMM date. Variations include Defined payment on default (digital) Partial or
  ///   full up-front payment rather than running premium.</para>
  ///
  ///   <para><b>Standard Credit Default Swaps</b></para>
  ///   <para>April 8th, 2009 ISDA introduced a Standard North American Corporate (SNAC) CDS contract.
  ///   Subsequently a similar standard contract was released for the European and Asian markets.
  ///   This contract included several key features:</para>
  ///   <list type="bullet">
  ///     <item><description>Fixed coupons - Contracts trade with fixed spreads - either 100bp or 500bp running</description></item>
  ///     <item><description>Full first coupon - The first coupon is paid in full with accrued being exchanged at
  ///     settlement. Previously the first coupon accrued from T+1.</description></item>
  ///     <item><description>No restructuring - Contracts now trade with no restructuring. Previously most NA contracts
  ///     traded with modified restructuring.</description></item>
  ///     <item><description>Cash settlement - Contracts now trade cash settled based on an auction.</description></item>
  ///     <item><description>Rolling look back provision - The effective date for credit events is now 60 days prior
  ///     to the trade date and 90 days prior to the trade date for succession events.</description></item>
  ///     <item><description>Determination committee - A determination committee is now responsible for determining
  ///     whether credit and seccession events have occured.</description></item>
  ///   </list>
  ///   <para>Standard CDS settle on a cash basis with a fixed running spread. They may be quoted in terms of
  ///   an up-front fee (excluding accrued), a conventional (flat) spread, or a par (old style) spread.</para>
  ///   <para>ISDA has published a standard CDS calculator to convert from conventional spreads to up-front fees.
  ///   The standard calculator is based on a flat spread curve and a standard recovery rate. 40% for senior
  ///   unsecured CDS and 20% for subordinated.</para>
  /// 
  ///   <para><b>Example default swap</b></para>
  ///   <para>An example of an standard default swap is:</para>
  ///   <list type="bullet">
  ///     <item><description>5 year CDS quoted at 2 points upfront.</description></item>
  ///     <item><description>$100M notional</description></item>
  ///   </list>
  ///   <para>At settlement the protection buyer pays 2% of 100M = 2M</para>
  ///   <para>At settlement the protection seller pays acrrued to T+1</para>
  ///   <para>Until a credit event occurs, the protection buyer pays 100bp/4 * 100M quarterly.</para>
  ///   <para>If a credit event occurs, the protection seller pays 100M minus the
  ///   auction recovery amount on the recovery settlement date</para>
  ///   <para>An example of an old style default swap is:</para>
  ///   <list type="bullet">
  ///     <item><description>5 year at-market default swap on Goldman Sachs.</description></item>
  ///     <item><description>Premium is 53 bp per annum paid quarterly.</description></item>
  ///     <item><description>$100m notional</description></item>
  ///     <item><description>Reference asset is specific bond A.</description></item>
  ///     <item><description>Settlement is cash settlement.</description></item>
  ///   </list>
  ///   <para>Until a credit event occurs, the protection buyer pays 53bp/4 * 100MM quarterly.</para>
  ///   <para>If a credit event occurs, the protection seller pays the face value value of the
  ///   reference asset plus accrued premium.</para>
  /// 
  ///   <para><b>Some Intuition</b></para>
  ///   <list type="bullet">
  ///     <item><description>A Default swap can be thought of as providing insurance
  ///     against default for a bond.</description></item>
  ///     <item><description>Alternatively it can be thought of as a financed (at
  ///     LIBOR) purchase of a floating rate corporate bond.</description></item>
  ///   </list>
  ///
  ///   <para><b>Some Subtleties</b></para>
  ///   <list type="bullet">
  ///     <item><description>Treatment of bond coupons and default swap premium
  ///     under default are not identical so you cannot perfectly
  ///     hedge the credit risk of a bond with a default swap.</description></item>
  ///     <item><description>Legal issues still exists re definitions of a credit event.</description></item>
  ///   </list>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CDSCashflowPricer">Cashflow Pricer</seealso>
  /// <seealso href="http://www.probability.net/credit.pdf">A Beginner's Guide to Credit Derivatives - Nomura</seealso>
  /// <seealso href="http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.139.4044&amp;rep=rep1&amp;type=pdf">Credit Swap Valuation - Duffie</seealso>
  /// <seealso href="http://www.frbatlanta.org/filelegacydocs/erq407_mengle.pdf">Credit Derivatives: An Overview. Mengle, Economic Review (FRB Atlanta), 2007</seealso>
  /// <seealso href="http://www.isda.org/educat/faqs.html">Product description: Credit default swaps - ISDA</seealso>
  /// <seealso href="http://en.wikipedia.org/wiki/Credit_default_swap">Credit default swap. Wikipedia</seealso>
  /// <example>
  /// <para>The following example demonstrates constructing a Credit Default Swap.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = Dt.Today();                     // Effective date is today
  ///   Dt maturity = Dt.CDSMaturity(effectiveDate, 5, TimeUnit.Years); // Maturity date is standard 5Yr CDS maturity
  ///
  ///   // Create CDS
  ///   CDS cds = new CDs(
  ///     effectiveDate,                                   // Effective date
  ///     maturityDate,                                    // Maturity date
  ///     Currency.EUR,                                    // Currency is Euro
  ///     0.02,                                            // Premium is 200bp
  ///     DayCount.Actual360,                              // Acrual Daycount is Actual/360
  ///     Frequency.Quarterly,                             // Quarterly payment frequency
  ///     Calendar.TGT,                                    // Calendar is Target
  ///   );
  ///
  ///   // Alternate method of creating standard 5Yr CDS.
  ///   CDS cds2 = new CDs(
  ///     effectiveDate,                                   // Effective date
  ///     5,                                               // Standard 5 Year maturity
  ///     0.02,                                            // Premium is 200bp
  ///     Calendar.TGT,                                    // Calendar is Euro Target
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class CDS : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    ///   Constructor for a standard CDS based on years to maturity
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Premium DayCount of Actual360.</description></item>
    ///     <item><description>Premium payment frequency of Quarterly.</description></item>
    ///     <item><description>Premium payment business day convention of Following.</description></item>
    ///     <item><description>The calendar is None.</description></item>
    ///     <item><description>The first premium payment date is the based on the IMM roll.</description></item>
    ///     <item><description>The last premium payment date is the maturity.</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturityInYears">Years to maturity or scheduled termination date from the effective date (eg 5)</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="cal">Calendar for premium payment</param>
    ///
    public CDS(Dt effective, int maturityInYears, double premium, Calendar cal)
      : this(effective, Dt.CDSMaturity(effective, new Tenor(maturityInYears, TimeUnit.Years)), Currency.None,
      Dt.Empty, premium, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, cal, 0.0, effective)
    { }

    /// <summary>
    ///   Constructor for a standard CDS based on maturity tenor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Premium DayCount of Actual360.</description></item>
    ///     <item><description>Premium payment frequency of Quarterly.</description></item>
    ///     <item><description>Premium payment business day convention of Following.</description></item>
    ///     <item><description>The calendar is None.</description></item>
    ///     <item><description>The first premium payment date is the based on the IMM roll.</description></item>
    ///     <item><description>The last premium payment date is the maturity.</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturityTenor">Maturity or scheduled temination tenor from the effective date</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="cal">Calendar for premium payment</param>
    ///
    public CDS(Dt effective, Tenor maturityTenor, double premium, Calendar cal)
      : this(effective, Dt.CDSMaturity(effective, maturityTenor), Currency.None, Dt.Empty,
      premium, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, cal, 0.0, effective)
    { }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Sets default first premium payment date based on bond maturity and premium
    ///   payment frequency.</para>
    /// </remarks>
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
    public CDS(Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : this(effective, maturity, ccy, Dt.Empty, premium, dayCount, freq, roll, cal, 0.0, effective)
    { }

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
    public CDS(Dt effective, Dt maturity, Currency ccy, Dt firstPrem, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : this(effective, maturity, ccy, firstPrem, premium, dayCount, freq, roll, cal, 0.0, effective)
    { }

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
    public CDS(Dt effective, Dt maturity, Currency ccy, Dt firstPrem, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal, double fee, Dt feeSettle)
      : base(effective, maturity, firstPrem, Dt.Empty, ccy, freq, roll, cal, CycleRule.None,
        CashflowFlag.IncludeDefaultDate | CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeMaturityAccrual)
    {
      Premium = premium;
      DayCount = dayCount;
      Fee = fee;
      FeeSettle = feeSettle;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CDS obj = (CDS)base.Clone();

      obj.amortSched_ = CloneUtil.Clone(amortSched_);
      obj.premiumSched_ = CloneUtil.Clone(premiumSched_);

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

      if (!FeeSettle.IsEmpty())
      {
        // Fee settlement on or after effective date and on or before maturity date
        if (FeeSettle < Effective)
          InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Fee settlement {0} must be on or after effective {1}", FeeSettle, Effective));
        if (FeeSettle > Maturity)
          InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Fee settlement {0} must be on or before maturity {1}", FeeSettle, Maturity));
      }

      if (Premium < 0.0 || Premium > 200.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", Premium));

      if (Fee < -2.0 || Fee > 2.0)
        InvalidValue.AddError(errors, this, "Fee", String.Format("Invalid fee. Must be between -2 and 2, Not {0}", Fee));

      // Validate schedules
      AmortizationUtil.Validate(amortSched_, errors);
      CouponPeriodUtil.Validate(premiumSched_, errors);

      return;
    }

    /// <summary>
    ///   Calculate the default first premium date.
    /// </summary>
    /// 
    /// <remarks>
    ///    The first premium date is simply the earlier date of CDSRoll(effective) and maturity.
    ///    Unlike the first coupon payment date, it never rolls on holidays.
    /// </remarks>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    ///
    /// <returns>Default first premium date</returns>
    /// <exclude/>
    public static Dt GetDefaultFirstPremiumDate(Dt effective, Dt maturity)
    {
      Dt dt = Dt.CDSRoll(effective, false);
      if (maturity < dt)
        return maturity;
      return dt;
    }

    /// <summary>
    /// Gets the default first coupon date.
    /// </summary>
    /// <returns>The default first coupon date.</returns>
    protected override Dt GetDefaultFirstCoupon()
    {
      return GetDefaultFirstPremiumDate(Effective, Maturity);
    }

    /// <summary>
    ///   Calculate the next premium date after a reference date.
    /// </summary>
    ///
    /// <param name="date">Reference date</param>
    ///
    /// <returns>Next premium date</returns>
    ///
    public Dt GetNextPremiumDate(Dt date)
    {
      Dt firstPrem = FirstPrem;
      if (date < firstPrem)
        return firstPrem;
      return Dt.Min(this.Maturity, Dt.CDSMaturity(date, 1, TimeUnit.Days));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   premium as a number (100bp = 0.01).
    /// </summary>
    /// <remarks>
    ///   <para>This premium is ignored if a step-up premium schedule exists</para>
    /// </remarks>
    [Category("Base")]
    public double Premium { get; set; }

    /// <summary>
    ///   Up-front fee in percent.
    /// </summary>
    [Category("Base")]
    public double Fee { get; set; }

    /// <summary>
    ///   Fee payment date
    /// </summary>
    [Category("Base")]
    public Dt FeeSettle { get; set; }

    /// <summary>
    ///   Daycount of premium
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

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
    ///   Last regular premium payment date
    /// </summary>
    /// <remarks>
    ///   <para>If the last premium date has not been set, calculates the default on the fly.</para>
    /// </remarks>
    [Category("Base")]
    public Dt LastPrem
    {
      get { return LastCoupon; }
      set { LastCoupon = value; }
    }

    /// <summary>
    ///   CDS is in funded form (principal exchanged)
    /// </summary>
    [Category("Base")]
    public bool Funded
    {
      get { return (CdsType != CdsType.Unfunded); }
    }

    /// <summary>
    ///   Cds type
    /// </summary>
    [Category("Base")]
    public CdsType CdsType { get; set; }

    /// <summary>
    ///   True if premium accrued on default.
    /// </summary>
    [Category("Base")]
    public bool AccruedOnDefault
    {
      get { return (CashflowFlag & CashflowFlag.AccruedPaidOnDefault) != 0; }

      set
      {
        if (value)
        {
          CashflowFlag |= CashflowFlag.AccruedPaidOnDefault;
        }
        else
        {
          CashflowFlag &= ~CashflowFlag.AccruedPaidOnDefault;
        }
      }
    }

    /// <summary>
    ///   True if fixed recovery rate.
    /// </summary>
    [Category("Base")]
    public bool FixedRecovery { get; set; }

    /// <summary>
    ///   Fixed recovery rate (used if FixedRecovery is true)
    /// </summary>
    [Category("Base")]
    public double FixedRecoveryRate { get; set; }

    /// <summary>
    ///   Amortization schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule
    {
      get
      {
        return amortSched_;
      }
    }

    /// <summary>
    ///   True if CDS amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes
    {
      get { return (amortSched_.Count == 0) ? false : true; }
    }

    /// <summary>
    ///   Premium schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<CouponPeriod> PremiumSchedule
    {
      get
      {
        return premiumSched_;
      }
    }

    /// <summary>
    ///   True if CDS has premium schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp
    {
      get { return (premiumSched_.Count == 0) ? false : true; }
    }

    /// <summary>
    ///   True if CDS contract regulates to pay recovery at maturity
    /// </summary>    
    public bool PayRecoveryAtMaturity { get; set; }

    /// <summary>
    ///  Bullet payment convention (null means non-bullet)
    /// </summary>
    public BulletConvention Bullet { get; set; }

    /// <summary>
    ///   Whether or not is a Standard CDS Contract
    /// </summary>
    /// <value>The type of the constract.</value>
    public bool IsStandardCDSContract { get; set; }

    /// <summary>
    /// Reference index for funded floating
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; set; }

    /// <summary>
    ///  Denomination currency of loss payment 
    /// </summary>
    ///
    [Category("Base")]
    public Currency RecoveryCcy
    {
      get
      {
        if (recoveryCcy_ == Currency.None) return Ccy;
        return recoveryCcy_;
      }
      set
      {
        recoveryCcy_ = value;
      }
    }

    /// <summary>
    ///  Notional cap
    /// </summary>
    ///
    [Category("Base")]
    public double? QuantoNotionalCap { get; set; }

    /// <summary>
    /// Spot Fx (from RecoveryCcy to numeraire currency (DiscountCurve.Ccy)) at inception 
    /// </summary>
    ///
    [Category("Base")]
    public double? FxAtInception { get; set; }

    #endregion Properties

    #region Data

    private Currency recoveryCcy_;
    // These two should never be null.
    private List<Amortization> amortSched_ = new List<Amortization>();
    private List<CouponPeriod> premiumSched_ = new List<CouponPeriod>();
    #endregion Data

  } // class CDS
}
