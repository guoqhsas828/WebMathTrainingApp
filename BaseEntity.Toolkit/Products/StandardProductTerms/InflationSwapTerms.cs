//
// SwapTerms.cs
//   2015. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Terms for a market-standard inflation swaps
  /// </summary>
  /// <remarks>
  ///   <para>Defined terms for a market-standard single currency fixed vs floating <see cref="Swap">inflation swaps</see>.</para>
  ///   <para>Inflation swaps are identified uniquely by the floating rate index and traded location.</para>
  ///   <para>The function <see cref="GetProduct(Dt,string,double)"/> creates the Inflation Swap from the Inflation Swap Terms.</para>
  ///   <inheritdoc cref="InflationSwapTerms.GetProduct(Dt,string,double)"/>
  ///   <example>
  ///   <para>A convenience function is provided to simplify creating a standard product directly.
  ///   The following example demonstrates creating a zero coupon rate swap (UK conventions) using this convenience function.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var floatingIndex = ReferenceRateCache.GetValue("RPI_GBP");
  ///     var swapTenor = "10Y";
  ///     var freq = Frequency.None;
  ///     var loc = Currency.None;
  ///     var coupon = 0.02;
  ///     // Look up product terms
  ///     var terms = StandardProductTermsUtil.GetInflationSwapTerms(floatingIndex, freq, loc);
  ///     // Create product
  ///     var swap = terms.GetProduct(asOf, swapTenor, coupon);
  ///   </code>
  ///   </example>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="GetProduct(Dt,string,double)"/>
  /// <seealso cref="Swap"/>
  [Serializable]
  public class InflationSwapTerms : StandardProductTermsBase
  {
    #region Constructor

    /// <summary>
    /// Constructor for year-on-year and zero coupon swap terms
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="ccy">Currency</param>
    /// <param name="location">Currency indicating trading location. Some swaps have different conventions depending on trading location</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="leg1FixedDayCount">Fixed leg day count</param>
    /// <param name="leg1BdConvention">Fixed leg business day convention</param>
    /// <param name="leg1Calendar">Fixed leg calendar</param>
    /// <param name="leg1CompoundingConv">Fixed leg compounding convention</param>
    /// <param name="floatingLeg2IndexName">Floating leg reference index name</param>
    /// <param name="leg2FloatingProjectionType">Floating leg projection type</param>
    /// <param name="leg1CompoundingFreq">Fixed compounding</param>
    /// <param name="leg2IndexationMethod">Inflation indexation method</param>
    /// <param name="leg2ResetLag">Reset lag between inflation rate observation and reset date</param>
    /// <param name="paymentLag">Payment delay in days</param>
    /// <param name="accrueOnCycle">Accrue on cycle dates or adjust accrual dates to payment dates</param>
    /// <param name="adjustLast">Adjust last accrual date to payment date</param>
    public InflationSwapTerms(
      string description, Currency ccy, Currency location, Frequency freq, int spotDays, DayCount leg1FixedDayCount, BDConvention leg1BdConvention, Calendar leg1Calendar,
      Frequency leg1CompoundingFreq, CompoundingConvention leg1CompoundingConv,
      string floatingLeg2IndexName, ProjectionType leg2FloatingProjectionType, IndexationMethod leg2IndexationMethod, Tenor leg2ResetLag, int paymentLag, bool accrueOnCycle, 
      bool adjustLast)
      : base(description)
    {
      Currency = ccy;
      Location = location;
      SpotDays = spotDays;
      Leg1FixedDayCount = leg1FixedDayCount;
      Leg1BdConvention = leg1BdConvention;
      Leg1Calendar = leg1Calendar;
      Leg1CompoundingFreq = leg1CompoundingFreq;
      Leg1CompoundingConvention = leg1CompoundingConv;
      Leg2IndexName = floatingLeg2IndexName;
      Leg2FloatingProjectionType = leg2FloatingProjectionType;
      Leg2IndexationMethod = leg2IndexationMethod;
      Leg2ResetLag = leg2ResetLag;
      Leg1PaymentFreq = freq;
      Leg2PaymentFreq = freq;
      PaymentLag = paymentLag;
      AdjustLast = adjustLast;
      AccrueOnCycle = accrueOnCycle;
    }

    /// <summary>
    /// Constructor for zero coupon swap terms
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="ccy">Currency and location</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="leg1FixedDayCount">Fixed leg day count</param>
    /// <param name="leg1BdConvention">Fixed leg business day convention</param>
    /// <param name="leg1Calendar">Fixed leg calendar</param>
    /// <param name="leg1CompoundingConv">Fixed leg compounding convention</param>
    /// <param name="floatingLeg2IndexName">Floating leg reference index name</param>
    /// <param name="leg2FloatingProjectionType">Floating leg projection type</param>
    /// <param name="leg1CompoundingFreq">Fixed compounding</param>
    /// <param name="leg2IndexationMethod">Inflation indexation method</param>
    /// <param name="leg2ResetLag">Reset lag between inflation rate observation and reset date</param>
    /// <param name="paymentLag">Payment delay in days</param>
    /// <param name="accrueOnCycle">Accrue on cycle dates or adjust accrual dates to payment dates</param>
    /// <param name="adjustLast">Adjust last accrual date to payment date</param>
    public InflationSwapTerms(
      string description, Currency ccy, int spotDays, DayCount leg1FixedDayCount, BDConvention leg1BdConvention, Calendar leg1Calendar, 
      Frequency leg1CompoundingFreq, CompoundingConvention leg1CompoundingConv,
      string floatingLeg2IndexName, ProjectionType leg2FloatingProjectionType, IndexationMethod leg2IndexationMethod, Tenor leg2ResetLag, int paymentLag,
      bool accrueOnCycle, bool adjustLast)
      : this(description, ccy, ccy, Frequency.None, spotDays, leg1FixedDayCount, leg1BdConvention, leg1Calendar, leg1CompoundingFreq,
          leg1CompoundingConv, floatingLeg2IndexName, leg2FloatingProjectionType, leg2IndexationMethod, leg2ResetLag, paymentLag, accrueOnCycle, adjustLast)
    { }

    #endregion

    #region Properties

    /// <summary>
    ///   Unique key for this term
    /// </summary>
    public override string Key => MakeKey(Leg2IndexName, Location, IsZeroCoupon(), IsYoYSwap());

    private bool IsYoYSwap()
    {
      return Leg1PaymentFreq == Frequency.Annual && Leg2PaymentFreq == Frequency.Annual;
    }

    private bool IsZeroCoupon()
    {
      return Leg1PaymentFreq == Frequency.None && Leg2PaymentFreq == Frequency.None;
    }

    /// <summary>
    ///   Currency indicating location of trading
    /// </summary>
    public Currency Currency { get; private set; }

    /// <summary>
    ///   Currency indicating location of trading
    /// </summary>
    public Currency Location { get; private set; }

    /// <summary>
    ///   Days to spot settlement
    /// </summary>
    public int SpotDays { get; private set; }

    /// <summary>
    ///   Days to payment
    /// </summary>
    public int PaymentLag { get; private set; }

    /// <summary>
    ///   Fixed leg DayCount
    /// </summary>
    public DayCount Leg1FixedDayCount { get; private set; }

    /// <summary>
    ///   Fixed leg Business-day convention
    /// </summary>
    public BDConvention Leg1BdConvention { get; private set; }

    /// <summary>
    ///   Fixed leg payment calendar
    /// </summary>
    public Calendar Leg1Calendar { get; private set; }

    /// <summary>
    ///   Fixed leg compounding frequency.
    ///   A frequency of none means pay at payment frequency.
    ///   Must be less than or equal to the fixed leg payment frequency.
    /// </summary>
    public Frequency Leg1CompoundingFreq { get; private set; }

    /// <summary>
    ///   Leg1 payment frequency (indexed by the swap tenor).
    ///   If not specified, the fixed payment frequency is used
    /// </summary>
    public Frequency Leg1PaymentFreq { get; private set; }

    /// <summary>
    ///   Fixed leg compounding convention (if compounding)
    /// </summary>
    public CompoundingConvention Leg1CompoundingConvention { get; private set; }

    /// <summary>
    ///   Floating leg reference index name
    /// </summary>
    public string Leg2IndexName { get; private set; }
    
    /// <summary>
    ///   Floating leg projection type
    /// </summary>
    public ProjectionType Leg2FloatingProjectionType { get; private set; }

    /// <summary>
    ///   Leg1 payment frequency (indexed by the swap tenor).
    ///   If not specified, the fixed payment frequency is used
    /// </summary>
    public Frequency Leg2PaymentFreq { get; private set; }

    /// <summary>
    ///   Reset lag between the index observation and the reset date
    /// </summary>
    public Tenor Leg2ResetLag { get; private set; }

    /// <summary>
    ///   Interest reference rate for this swap
    /// </summary>
    public InflationReferenceRate InflationReferenceRate
    {
      get
      {
        if (_inflationReferenceRate != null) return _inflationReferenceRate;
        _inflationReferenceRate = InflationReferenceRate.Get(Leg2IndexName);
        return _inflationReferenceRate;
      }
    }

    /// <summary>
    ///   Interest reference rate for this swap
    /// </summary>
    public IndexationMethod Leg2IndexationMethod { get; private set; }

    /// <summary>
    ///  Accrue on cycle dates or adjust accrual dates to payment dates
    /// </summary>
    public bool AccrueOnCycle { get; private set; }

    /// <summary>
    ///  Adjust last accrual date to payment date
    /// </summary>
    public bool AdjustLast { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Validate terms
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (SpotDays < 0)
        InvalidValue.AddError(errors, this, "SpotDays", "Invalid number of days to settmement");
      if (Leg1FixedDayCount == DayCount.None)
        InvalidValue.AddError(errors, this, "FixedDayCount", "Daycount for fixed leg must be specified");
      if (string.IsNullOrEmpty(Leg2IndexName))
        InvalidValue.AddError(errors, this, "InflationIndexName", "Index name for floating leg must be specified");
    }

    /// <summary>
    ///   Create standard interest rate swap given a date, a maturity tenor, and a fixed rate
    /// </summary>
    /// <remarks>
    ///   <para>Given the swap terms, the swap is created given a date and a maturity tenor, and a fixed rate as follows:</para>
    ///   <para>For some swaps, the terms differ based on the maturity of the swap. For swaps where terms differ based on maturity</para>
    ///   <list type="table">
    ///     <listheader><term>Swap Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="Product.Effective"/></term>
    ///       <description>Calculated as <see cref="SwapTerms.SpotDays"/> business days after the specified date.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Maturity"/></term>
    ///       <description>Calculated from the <see cref="Product.Effective"/> based on the swap tenor. The swap tenor can be
    ///       O/N or T/N (one day), a tenor, or a specific date.</description>
    ///     </item>
    ///   </list>
    ///   <para>For fixed leg 1 (payer):</para>
    ///   <list type="table">
    ///     <listheader><term>Swap Leg Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to <see cref="ReferenceRate.Currency"/>.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.Coupon"/></term>
    ///       <description>Set to the specified coupon.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.FinalExchange"/></term>
    ///       <description>Set to false.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.IsZeroCoupon"/></term>
    ///       <description>Set to the true if the payment frequency is none.</description>
    ///     </item>
    ///   </list>
    ///   <para>For floating swap leg 2 (receiver):</para>
    ///   <list type="table">
    ///     <listheader><term>Swap Leg Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="SwapLeg.Index"/></term>
    ///       <description>Set to the floating rate index matching the Terms.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to the Reference Index <see cref="ReferenceRate.Currency"/>.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.Coupon"/></term>
    ///       <description>Set to 0.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.DayCount"/></term>
    ///       <description>Set to the Reference Index <see cref="BaseEntity.Toolkit.Base.ReferenceRates.InterestReferenceRate.DayCount"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.BDConvention"/></term>
    ///       <description>Set to the Reference Index <see cref="BaseEntity.Toolkit.Base.ReferenceRates.InterestReferenceRate.BDConvention"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Calendar"/></term>
    ///       <description>Set to the Reference index <see cref="ReferenceRate.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.ResetLag"/></term>
    ///       <description>Set to the Reference Index <see cref="BaseEntity.Toolkit.Base.ReferenceRates.InterestReferenceRate.DaysToSpot"/> days.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.FinalExchange"/></term>
    ///       <description>Set to false.</description>
    ///     </item>
    ///   </list>
    /// </remarks>    
    /// <param name="asOf">As-of date</param>
    /// <param name="tenorName">Tenor name</param>
    /// <param name="fixedRate">Fixed swap leg coupon</param>
    /// <returns>Standard <see cref="Swap"/></returns>
    [ProductBuilder]
    public Swap GetProduct(Dt asOf, string tenorName, double fixedRate)
    {
      var effective = GetSettlement(asOf);
      var maturity = GetMaturity(effective, tenorName);
      var rateIndex = InflationReferenceRate;
      
      // Pay fixed
      var payer = new SwapLeg(effective, maturity, rateIndex.Currency, fixedRate, Leg1FixedDayCount, Leg1PaymentFreq, Leg1BdConvention, Leg1Calendar, false)
      {
        FinalExchange = false,
        CompoundingFrequency = this.Leg1CompoundingFreq,
        CompoundingConvention = this.Leg1CompoundingConvention,
        IsZeroCoupon = Leg1PaymentFreq == Frequency.None,
        AdjustLast = AdjustLast,
        AccrueOnCycle = AccrueOnCycle
      };
      payer.Validate();
      var psp = (IScheduleParams)payer.Schedule;
      payer.CycleRule = psp.CycleRule;

      // Receive floating
      var refIndex = new BaseEntity.Toolkit.Base.ReferenceIndices.InflationIndex(InflationReferenceRate);
      var receiver = new InflationSwapLeg(effective, maturity, Leg2PaymentFreq, 0.0, refIndex)
      {
        InArrears = true,
        IsZeroCoupon = Leg2PaymentFreq == Frequency.None,
        CycleRule = psp.CycleRule,
        ResetLag = Leg2ResetLag,
        ProjectionType = ProjectionType.InflationRate,
        IndexationMethod = Leg2IndexationMethod,
        AdjustLast = AdjustLast,
        AccrueOnCycle = AccrueOnCycle
      };
      receiver.Validate();
      var swap = new Swap(receiver, payer);
      swap.Validate();
      return swap;
    }

    /// <summary>
    /// Get settlement date for a standard swap given a pricing as-of date
    /// </summary>
    /// <param name="asOf">Pricing as-of date</param>
    /// <returns>Settlement date</returns>
    public Dt GetSettlement(Dt asOf)
    {
      return Dt.AddDays(asOf, SpotDays, Leg1Calendar);
    }

    /// <summary>
    ///   Gets maturity of the swap relative to a specified settlement date
    /// </summary>
    /// <remarks>
    ///   <para>A number of tenor formats as supported.</para>
    ///   <list type="bullet">
    ///     <item><description>A specific date</description></item>
    ///     <item><description>O/N or T/N which is the next business day</description></item>
    ///     <item><description>A tenor in days which is the specified number of business days (eg 3 Days)</description></item>
    ///     <item><description>Any other tenor which is the number of calendar days (eg 1 Month)</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="settle">Settle date</param>
    /// <param name="tenor">Product description</param>
    /// <returns>Maturity date</returns>
    public Dt GetMaturity(Dt settle, string tenor)
    {
      Tenor realTenor;
      double d;
      var dt = settle;//Dt.AddDays(settle, SpotDays, Leg1Calendar);
      if (tenor == "O/N" || tenor == "T/N")
        // Special keywords
        dt = Dt.AddDays(dt, 1, Leg1Calendar);
      else if (Tenor.TryParse(tenor, out realTenor))
        // Tenor
        dt = (realTenor.Units == TimeUnit.Days) ? Dt.AddDays(dt, realTenor.N, Leg1Calendar) : Dt.Add(dt, realTenor);
      else if (double.TryParse(tenor, out d))
        // XL date
        dt = Dt.FromExcelDate(d);
      else if (!Dt.TryParse(tenor, out dt))
        throw new ArgumentException($"Invalid swap maturity {tenor}. Can be O/N, T/N, Tenor, or date");
      dt = Dt.Add(dt, Frequency.Daily, PaymentLag, CycleRule.None);
      return Dt.Roll(dt, Leg1BdConvention, Leg1Calendar);
    }

    /// <summary>
    /// Create unique key for Swap Terms
    /// </summary>
    /// <param name="floatingIndexName">Floating rate index name</param>
    /// <param name="location">Currency (Country) of trading</param>
    /// <param name="payFreq">Zero coupon swap</param>
    /// <returns>Unique key</returns>
    public static string GetKey(string floatingIndexName, Currency location, Frequency payFreq)
    {
      var zeroCoupon = payFreq == Frequency.None;
      var yoYSwap = payFreq == Frequency.Annual;
      return MakeKey(floatingIndexName, location, zeroCoupon, yoYSwap);
    }

    private static string MakeKey(string floatingIndexName,
      Currency location, bool zeroCoupon, bool yoYSwap)
    {
      return new StringBuilder()
        .Append(location).Append('.')
        .Append(floatingIndexName)
        .Append(zeroCoupon ? ".ZeroCouponSwap" : yoYSwap ? ".YoYSwap" : "InflationSwap")
        .ToString();
    }

    #endregion

    #region Data

    private InflationReferenceRate _inflationReferenceRate;

    #endregion
  }
}
