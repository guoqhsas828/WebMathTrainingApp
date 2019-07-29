//
// SwapTerms.cs
// 
//
using System;
using System.Collections;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Terms for a market-standard vanilla single currency fixed vs floating interest rate swap
  /// </summary>
  /// <remarks>
  ///   <para>Defined terms for a market-standard single currency fixed vs floating <see cref="Swap">interest rate swap</see>.</para>
  ///   <para>Interest rate swaps are identified uniquely by the floating rate index and traded currency.</para>
  ///   <para>The function <see cref="GetProduct(Dt,string,double)"/> creates the Interest Rate Swap from the Interest Rate Swap Terms.</para>
  ///   <inheritdoc cref="SwapTerms.GetProduct(Dt,string,double)"/>
  ///   <example>
  ///   <para>The following example demonstrates creating an interest rate swap based on standard terms.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var floatingIndex = ReferenceRateCache.GetValue("USDLIBOR");
  ///     var swapTenor = "10Y";
  ///     var coupon = 0.02;
  ///     // Look up product terms
  ///     var terms = StandardProductTermsUtil.GetSwapTerms(floatingIndex);
  ///     // Create product
  ///     var swap = terms.GetProduct(asOf, swapTenor, coupon);
  ///   </code>
  ///   <para>A convenience function is provided to simplify creating a standard product directly.
  ///   The following example demonstrates creating a vanilla USD interest rate swap (US conventions) using this convenience function.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var floatingIndex = ReferenceRateCache.GetValue("USDLIBOR");
  ///     var swapTenor = "10Y";
  ///     var coupon = 0.02;
  ///     // Create product
  ///     var swap = StandardProductTermsUtil.GetStandardSwap(floatingIndex, asOf, swapTenor, coupon);
  ///   </code>
  ///   </example>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="GetProduct(Dt,string,double)"/>
  /// <seealso cref="Swap"/>
  [Serializable]
  public class SwapTerms : StandardProductTermsBase
  {
    #region Constructor

    /// <summary>
    /// fixed-float swap constructor.  The swap is always created receive floating, pay fixed.
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="location">Trading currency. Some swaps have different conventions depending on trading currency</param>
    /// <param name="currency">Currency indicating leg currency.</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="fixedDayCount">Fixed leg day count</param>
    /// <param name="fixedBdConvention">Fixed leg business day convention</param>
    /// <param name="fixedCalendar">Fixed leg calendar</param>
    /// <param name="fixedPaymentFreq">Fixed leg payment frequencies (short and long). A frequency of none means pay at maturity</param>
    /// <param name="floatingIndexName">Floating leg reference index name</param>
    /// <param name="floatingRateTenor">Floating leg rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="floatingPaymentFreq">Floating leg payment frequencies (short and long)</param>
    /// <param name="floatingCompoundingConvention">Compounding convention for floating leg (if compounding)</param>
    /// <param name="floatingProjectionType">Floating leg projection type</param>
    /// <param name="paymentLag">Lag between the last fixing date and the payment date.</param>
    /// <param name="mac">ISDA Market agreed coupon convention</param>
    public SwapTerms(
      string description, Currency location, Currency currency, string floatingIndexName, Tenor floatingRateTenor,
      DayCount fixedDayCount, BDConvention fixedBdConvention, Calendar fixedCalendar, Frequency fixedPaymentFreq,
      Frequency floatingPaymentFreq, CompoundingConvention floatingCompoundingConvention, ProjectionType floatingProjectionType, 
      int spotDays, int paymentLag, bool mac)
      : base(description)
    {
      // Interest Rate Swap
      Leg1Currency = Leg2Currency = currency;
      Location = location != Currency.None ? location : currency;
      SpotDays = spotDays;
      Leg2Calendar = fixedCalendar;
      Leg2FixedDayCount = fixedDayCount;
      Leg1FixedBdConvention = fixedBdConvention;
      Leg2FixedBdConvention = fixedBdConvention;
      Leg2PaymentFreq = fixedPaymentFreq;
      Leg1FloatIndexName = floatingIndexName;
      Leg1FloatIndexTenor = (floatingRateTenor != Tenor.Empty) ? floatingRateTenor : new Tenor(floatingPaymentFreq);
      Leg1PaymentFreq = floatingPaymentFreq;
      Leg1CompoundingConvention = floatingCompoundingConvention;
      Leg1FloatProjectionType = floatingProjectionType;
      PaymentLag = paymentLag;
      Mac = mac;
    }

    /// <summary>
    /// fixed-float swap constructor.  The swap is always created receive floating, pay fixed.
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="location">Trading currency. Some swaps have different conventions depending on trading currency</param>
    /// <param name="currency">Currency indicating leg currency.</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="lastShortTenor">Last short tenor (if terms differ between shorter and longer dated tenors)</param>
    /// <param name="fixedDayCount">Fixed leg day count</param>
    /// <param name="fixedBdConvention">Fixed leg business day convention</param>
    /// <param name="calendar">Fixed leg calendar</param>
    /// <param name="fixedPaymentFreq">Fixed leg payment frequencies (short and long). A frequency of none means pay at maturity</param>
    /// <param name="floatingIndexName">Floating leg reference index name</param>
    /// <param name="floatingRateTenor">Floating leg rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="floatingPaymentFreq">Floating leg payment frequencies (short and long)</param>
    /// <param name="floatingCompoundingConvention">Compounding convention for floating leg (if compounding)</param>
    /// <param name="floatingProjectionType">Floating leg projection type</param>
    /// <param name="paymentLag">Lag between the last fixing date and the payment date.</param>
    /// <param name="mac">ISDA Market agreed coupon convention</param>
    public SwapTerms(
      string description, Currency location, Currency currency, int spotDays, Tenor lastShortTenor,
      DayCount fixedDayCount, BDConvention fixedBdConvention, Calendar calendar, Frequency fixedPaymentFreq,
      string floatingIndexName, Tenor floatingRateTenor, Frequency floatingPaymentFreq,
      CompoundingConvention floatingCompoundingConvention, ProjectionType floatingProjectionType, int paymentLag,
      bool mac)
      : base(description)
    {
      // Interest Rate Swap
      Leg1Currency = Leg2Currency = currency;
      Location = location != Currency.None ? location : currency;
      SpotDays = spotDays;
      Leg2Calendar = calendar;
      Leg2FixedDayCount = fixedDayCount;
      Leg1FixedBdConvention = fixedBdConvention;
      Leg2FixedBdConvention = fixedBdConvention;
      Leg2PaymentFreq = fixedPaymentFreq;
      Leg1FloatIndexName = floatingIndexName;
      Leg1FloatIndexTenor = (floatingRateTenor != Tenor.Empty) ? floatingRateTenor : new Tenor(floatingPaymentFreq);
      Leg1PaymentFreq = floatingPaymentFreq;
      Leg1CompoundingConvention = floatingCompoundingConvention;
      Leg1FloatProjectionType = floatingProjectionType;
      PaymentLag = paymentLag;
      Mac = mac;
    }

    /// <summary>
    /// Basis swap constructor
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="calendarLeg1">Calendar</param>
    /// <param name="calendarLeg2">Calendar</param>
    /// <param name="BDconvention">Business daycount convention</param>
    /// <param name="spreadOnLeg1">Spread on leg 1 or leg 2</param>
    /// <param name="leg1currency">Currency indicating leg currency.</param>
    /// <param name="leg1floatingIndexName">Floating leg reference index name</param>
    /// <param name="leg1floatingRateTenor">Floating leg rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="leg1floatingPaymentFreq">Floating leg payment frequencies (short and long)</param>
    /// <param name="leg1floatingCompoundingConvention">Compounding convention for floating leg (if compounding)</param>
    /// <param name="leg1floatingProjectionType">Floating leg projection type</param>
    /// <param name="leg2currency">Currency indicating leg currency.</param>
    /// <param name="leg2floatingIndexName">Floating leg reference index name</param>
    /// <param name="leg2floatingRateTenor">Floating leg rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="leg2floatingPaymentFreq">Floating leg payment frequencies (short and long)</param>
    /// <param name="leg2floatingCompoundingConvention">Compounding convention for floating leg (if compounding)</param>
    /// <param name="leg2floatingProjectionType">Floating leg projection type</param>
    /// <param name="paymentLag">Lag between the last fixing date and the payment date.</param>
    /// <param name="mac">ISDA Market agreed coupon convention</param>
    /// <param name="principalExchange">Pricipal exchange</param>
    public SwapTerms(
      string description, int spotDays, bool spreadOnLeg1, 
      Calendar calendarLeg1, Calendar calendarLeg2, 
      BDConvention BDconvention, 
      Currency leg1currency, string leg1floatingIndexName, Tenor leg1floatingRateTenor, Frequency leg1floatingPaymentFreq,
      CompoundingConvention leg1floatingCompoundingConvention, ProjectionType leg1floatingProjectionType,
      Currency leg2currency, string leg2floatingIndexName, Tenor leg2floatingRateTenor, Frequency leg2floatingPaymentFreq,
      CompoundingConvention leg2floatingCompoundingConvention, ProjectionType leg2floatingProjectionType,
      int paymentLag,
      bool mac,
      bool principalExchange)
      : this(description, spotDays, spreadOnLeg1, calendarLeg1, calendarLeg2, 
       leg1currency, DayCount.None, BDconvention,
       leg1floatingIndexName, leg1floatingRateTenor, leg1floatingPaymentFreq,
       leg1floatingCompoundingConvention, leg1floatingProjectionType,
       leg2currency, DayCount.None, BDconvention,
       leg2floatingIndexName, leg2floatingRateTenor, leg2floatingPaymentFreq,
       leg2floatingCompoundingConvention, leg2floatingProjectionType,
       paymentLag, mac, principalExchange)
    {
    }

    /// <summary>
    /// Basis and Cross Currency swap constructor
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="calendarLeg1">Calendar</param>
    /// <param name="calendarLeg2">Calendar</param>
    /// <param name="spreadOnLeg1">Spread on leg 1 or leg 2</param>
    /// <param name="leg1Currency">Currency indicating leg currency.</param>
    /// <param name="leg1FixedBdConvention">Leg 1 fixed business day convention</param>
    /// <param name="leg1FixedDayCount">Leg 1 fixed daycount</param>
    /// <param name="leg1FloatingIndexName">Floating leg reference index name</param>
    /// <param name="leg1FloatingRateTenor">Floating leg rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="leg1FloatingPaymentFreq">Floating leg payment frequencies (short and long)</param>
    /// <param name="leg1FloatingCompoundingConvention">Compounding convention for floating leg (if compounding)</param>
    /// <param name="leg1FloatingProjectionType">Floating leg projection type</param>
    /// <param name="leg2Currency">Currency indicating leg currency.</param>
    /// <param name="leg2FixedBdConvention">Leg 2 fixed business day convention</param>
    /// <param name="leg2FixedDayCount">Leg 2 fixed daycount</param>
    /// <param name="leg2FloatingIndexName">Leg 2 floating reference index name</param>
    /// <param name="leg2FloatingRateTenor">Leg 2 floating rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="leg2FloatingPaymentFreq">Leg 2 floating leg payment frequencies (short and long)</param>
    /// <param name="leg2FloatingCompoundingConvention">Leg 2 floating compounding convention</param>
    /// <param name="leg2FloatingProjectionType">Leg 2 floating leg projection type</param>
    /// <param name="paymentLag">Lag between the last fixing date and the payment date.</param>
    /// <param name="mac">ISDA Market agreed coupon convention</param>
    /// <param name="principalExchange">Pricipal exchange flag</param>
    public SwapTerms(
      string description, int spotDays, bool spreadOnLeg1, 
      Calendar calendarLeg1, Calendar calendarLeg2, 
      Currency leg1Currency, DayCount leg1FixedDayCount, BDConvention leg1FixedBdConvention,
      string leg1FloatingIndexName, Tenor leg1FloatingRateTenor, Frequency leg1FloatingPaymentFreq,
      CompoundingConvention leg1FloatingCompoundingConvention, ProjectionType leg1FloatingProjectionType,
      Currency leg2Currency, DayCount leg2FixedDayCount, BDConvention leg2FixedBdConvention,
      string leg2FloatingIndexName, Tenor leg2FloatingRateTenor, Frequency leg2FloatingPaymentFreq,
      CompoundingConvention leg2FloatingCompoundingConvention, ProjectionType leg2FloatingProjectionType,
      int paymentLag,
      bool mac,
      bool principalExchange)
      : base(description)
    {
      // Basis swap common terms
      Location = Currency.None;
      SpreadOnLeg1 = spreadOnLeg1;
      Leg1Calendar = calendarLeg1;
      Leg2Calendar = calendarLeg2;
      SpotDays = spotDays;
      PaymentLag = paymentLag;
      Mac = mac;
      PrincipalExchange = principalExchange;

      // Leg 1 terms
      Leg1Currency = leg1Currency;
      Leg1PaymentFreq = leg1FloatingPaymentFreq;
      Leg1CompoundingConvention = leg1FloatingCompoundingConvention;
      Leg1FixedDayCount = leg1FixedDayCount;
      Leg1FixedBdConvention = leg1FixedBdConvention;
      Leg1FloatIndexName = leg1FloatingIndexName;
      Leg1FloatIndexTenor = (leg1FloatingRateTenor != Tenor.Empty) ? leg1FloatingRateTenor : new Tenor(leg1FloatingPaymentFreq);
      Leg1FloatProjectionType = leg1FloatingProjectionType;
      Leg1CompoundingFreq = GetCompoundingFrequency(Leg1FloatIndexTenor, Leg1PaymentFreq);

      // Leg 2 terms
      Leg2Currency = leg2Currency;
      Leg2PaymentFreq = leg2FloatingPaymentFreq;
      Leg2CompoundingConvention = leg2FloatingCompoundingConvention;
      Leg2FixedDayCount = leg2FixedDayCount;
      Leg2FixedBdConvention = leg2FixedBdConvention;
      Leg2FloatIndexName = leg2FloatingIndexName;
      Leg2FloatIndexTenor = (leg2FloatingRateTenor != Tenor.Empty) ? leg2FloatingRateTenor : new Tenor(leg2FloatingPaymentFreq);
      Leg2FloatProjectionType = leg2FloatingProjectionType;
      Leg1CompoundingFreq = GetCompoundingFrequency(Leg2FloatIndexTenor, Leg2PaymentFreq);
    }

    private Frequency GetCompoundingFrequency(Tenor rateTenor, Frequency payFreq)
    {
      var floatingTenor = (rateTenor != Tenor.Empty) ? rateTenor : new Tenor(payFreq);
      var floatingTenorFreq = (rateTenor != Tenor.Empty) ? floatingTenor.ToFrequency() : payFreq;
      return (payFreq < floatingTenorFreq) ? /*payment less frequent*/ floatingTenorFreq
        : floatingTenorFreq > payFreq ? floatingTenorFreq : Frequency.None;
    }

    /// <summary>
    /// Constructor for simple libor swap
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="location">Currency indicating trading currency. Some swaps have different conventions depending on trading currency</param>
    /// <param name="currency">Currency of swap</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="fixedDayCount">Fixed leg day count</param>
    /// <param name="fixedCalendar">Fixed leg calendar</param>
    /// <param name="fixedPaymentFreq">Fixed leg payment frequencies or none to match tenor of swap</param>
    /// <param name="floatingIndexName">Floating leg reference index name</param>
    /// <param name="floatingPaymentFreq">Floating leg rate tenor and payment frequency</param>
    public SwapTerms(string description, Currency location, Currency currency, int spotDays, DayCount fixedDayCount, Calendar fixedCalendar, Frequency fixedPaymentFreq, string floatingIndexName, Frequency floatingPaymentFreq)
      : this(description, location, currency, spotDays, Tenor.Empty, fixedDayCount, BDConvention.Modified, fixedCalendar, fixedPaymentFreq,
        floatingIndexName, Tenor.Empty, floatingPaymentFreq, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false )
    {}

    /// <summary>
    /// Constructor for simple libor swap
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="location">Currency indicating trading currency. Some swaps have different conventions depending on trading currency</param>
    /// <param name="currency">Currency of swap</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="fixedDayCount">Fixed leg day count</param>
    /// <param name="fixedCalendar">Fixed leg calendar</param>
    /// <param name="fixedPaymentFreq">Fixed leg payment frequencies or none to match tenor of swap</param>
    /// <param name="floatingIndexName">Floating leg reference index name</param>
    /// <param name="floatingPaymentFreq">Floating leg rate tenor and payment frequency</param>
    /// <param name="compConvention">Floating rate compounding convention</param>
    /// <param name="indexTenor">tenor rate tenor</param>
    /// <param name="compFreq">Compounding frequency</param>
    public SwapTerms(string description, Currency location, Currency currency, int spotDays, DayCount fixedDayCount, Calendar fixedCalendar, Frequency fixedPaymentFreq, 
      string floatingIndexName, Frequency floatingPaymentFreq, Tenor indexTenor, Frequency compFreq, CompoundingConvention compConvention)
      : this(description, location, currency, spotDays, Tenor.Empty, fixedDayCount, BDConvention.Modified, fixedCalendar, fixedPaymentFreq,
        floatingIndexName, indexTenor, compFreq, compConvention, ProjectionType.SimpleProjection, 0, false)
    { }

    /// <summary>
    ///   Constructor for simple OIS swap
    /// </summary>
    /// <remarks>
    ///   <para>Bullet vs daily to 1 year then annual vs daily.</para>
    /// </remarks>
    /// <param name="description">Description</param>
    /// <param name="currency">Currency of swap</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="fixedDayCount">Fixed leg day count</param>
    /// <param name="fixedCalendar">Fixed leg calendar</param>
    /// <param name="floatingIndexName">Floating leg reference index name</param>
    /// <param name="floatingProjectionType">Floating leg projection type</param>
    /// <param name="paymentLag">Lag between the last fixing date and the payment date.</param>
    /// <param name="compConvention">Compounding convention</param>
    public SwapTerms(string description, Currency currency, int spotDays, DayCount fixedDayCount, Calendar fixedCalendar, string floatingIndexName, ProjectionType floatingProjectionType, int paymentLag, 
      CompoundingConvention compConvention)
      : this(description, currency, currency, spotDays, Tenor.OneYear, fixedDayCount, BDConvention.Modified, fixedCalendar, Frequency.Annual,
        floatingIndexName, Tenor.OneDay, Frequency.Annual, compConvention, floatingProjectionType, paymentLag, false)
    {}

    /// <summary>
    /// Constructor for a simple basis swap
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="currency">Payment currency</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="spreadOnLeg1">Spread is quoted on leg 1, otherwise leg2</param>
    /// <param name="calendarLeg1">Calendar for swap effective and maturity dates</param>
    /// <param name="calendarLeg2">Calendar for swap effective and maturity dates</param>
    /// <param name="daycount">Business daycount</param>
    /// <param name="floatingIndexName1">Floating leg 1 reference index name</param>
    /// <param name="floatingRateTenor1">Floating leg 1 rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="floatingPaymentFreq1">Floating leg 1 payment frequencies (short and long)</param>
    /// <param name="floatingCompoundingConvention1">Floating leg 1 compounding convention (if compounding)</param>
    /// <param name="floatingProjectionType1">Floating leg 1 projection type</param>
    /// <param name="floatingIndexName2">Floating leg 2 reference index name.</param>
    /// <param name="floatingRateTenor2">Floating leg 2 rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="floatingPaymentFreq2">Floating leg 2 payment frequencies (short and long).</param>
    /// <param name="floatingCompoundingConvention2">Floating leg 2 compounding convention (if compounding)</param>
    /// <param name="floatingProjectionType2">Floating leg 2 projection type</param>
    /// <param name="paymentLag">Lag between the last fixing date and the payment date.</param>
    /// <param name="mac">ISDA Market agreed coupon convention</param>
    public SwapTerms(
      string description, Currency currency, int spotDays, bool spreadOnLeg1, 
      Calendar calendarLeg1, Calendar calendarLeg2,
      BDConvention daycount,
      string floatingIndexName1, Tenor floatingRateTenor1, Frequency floatingPaymentFreq1,
      CompoundingConvention floatingCompoundingConvention1, ProjectionType floatingProjectionType1,
      string floatingIndexName2, Tenor floatingRateTenor2, Frequency floatingPaymentFreq2,
      CompoundingConvention floatingCompoundingConvention2, ProjectionType floatingProjectionType2,
      int paymentLag,
      bool mac
      )
      : this(description, spotDays, spreadOnLeg1, calendarLeg1, calendarLeg2,
          daycount, currency, 
          floatingIndexName1, 
          floatingRateTenor1, floatingPaymentFreq1,
          floatingCompoundingConvention1, floatingProjectionType1, 
          currency, floatingIndexName2, floatingRateTenor2, floatingPaymentFreq2,
          floatingCompoundingConvention2, floatingProjectionType2, paymentLag, mac, false)
    { }

    /// <summary>
    /// Constructor for a simple cross currency swap
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="ccy1">Leg 1 currency</param>
    /// <param name="ccy2">Leg 2 currency</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="spreadOnLeg1">Spread is quoted on leg 1, otherwise leg2</param>
    /// <param name="calendar">Calendar for swap effective and maturity dates</param>
    /// <param name="daycount">Business daycount</param>
    /// <param name="floatingIndexName1">Floating leg 1 reference index name</param>
    /// <param name="floatingRateTenor1">Floating leg 1 rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="floatingPaymentFreq1">Floating leg 1 payment frequencies (short and long)</param>
    /// <param name="floatingCompoundingConvention1">Floating leg 1 compounding convention (if compounding)</param>
    /// <param name="floatingProjectionType1">Floating leg 1 projection type</param>
    /// <param name="floatingIndexName2">Floating leg 2 reference index name.</param>
    /// <param name="floatingRateTenor2">Floating leg 2 rate tenors (short and long). If not specified, use tenor implied by floating rate payment frequency</param>
    /// <param name="floatingPaymentFreq2">Floating leg 2 payment frequencies (short and long).</param>
    /// <param name="floatingCompoundingConvention2">Floating leg 2 compounding convention (if compounding)</param>
    /// <param name="floatingProjectionType2">Floating leg 2 projection type</param>
    /// <param name="paymentLag">Lag between the last fixing date and the payment date.</param>
    /// <param name="mac">ISDA Market agreed coupon convention</param>
    /// <param name="principalExchange">Principal exchange</param>
    public SwapTerms(
      string description, Currency ccy1, Currency ccy2, int spotDays, bool spreadOnLeg1, Calendar calendar,
      BDConvention daycount,
      string floatingIndexName1, Tenor floatingRateTenor1, Frequency floatingPaymentFreq1,
      CompoundingConvention floatingCompoundingConvention1, ProjectionType floatingProjectionType1,
      string floatingIndexName2, Tenor floatingRateTenor2, Frequency floatingPaymentFreq2,
      CompoundingConvention floatingCompoundingConvention2, ProjectionType floatingProjectionType2,
      int paymentLag,
      bool mac, bool principalExchange
      )
      : this(description, spotDays, spreadOnLeg1, calendar, calendar, daycount, ccy1,
          floatingIndexName1,
          floatingRateTenor1, floatingPaymentFreq1,
          floatingCompoundingConvention1, floatingProjectionType1,
          ccy2, floatingIndexName2, floatingRateTenor2, floatingPaymentFreq2,
          floatingCompoundingConvention2, floatingProjectionType2, paymentLag, mac, principalExchange)
    { }
    
    #endregion

    #region Properties

    /// <summary>
    ///   Unique key for this term
    /// </summary>
    public override string Key => MakeKey(Location, Leg1FloatIndexName, Leg1Currency, Leg1FloatIndexTenor, 
      Leg2FloatIndexName, Leg2Currency, Leg2FloatIndexTenor);

    /// <summary>
    ///   Trading location.  Different trading locations may have different swap terms.
    /// </summary>
    public Currency Location { get; private set; }

    /// <summary>
    ///   Days to settle
    /// </summary>
    public int SpotDays { get; private set; }

    /// <summary>
    ///   Currency indicating currency of trading
    /// </summary>
    public Currency Leg1Currency { get; private set; }

    /// <summary>
    ///   leg1 DayCount
    /// </summary>
    public DayCount Leg1FixedDayCount { get; private set; }

    /// <summary>
    ///   Leg1 business-day convention
    /// </summary>
    public BDConvention Leg1FixedBdConvention { get; private set; }

    /// <summary>
    ///   Leg1 payment calendar
    /// </summary>
    public Calendar Leg1Calendar { get; private set; }

    /// <summary>
    ///   Leg1 reference index name
    /// </summary>
    public string Leg1FloatIndexName { get; private set; }

    /// <summary>
    ///   Optional leg1 floating leg rate tenor (indexed by the swap tenor).
    ///   If not specified, the tenor of the reference index is used by the floating rate payment frequency is used
    /// </summary>
    public Tenor Leg1FloatIndexTenor { get; private set; }

    /// <summary>
    ///   Leg1 payment frequency (indexed by the swap tenor).
    ///   If not specified, the fixed payment frequency is used
    /// </summary>
    public Frequency Leg1PaymentFreq { get; private set; }

    /// <summary>
    ///   Leg1 compounding convention (if compounding)
    /// </summary>
    public CompoundingConvention Leg1CompoundingConvention { get; private set; }

    /// <summary>
    ///   Leg1 compounding frequency (if compounding)
    /// </summary>
    public Frequency Leg1CompoundingFreq { get; private set; }

    /// <summary>
    ///   Leg1 projection type
    /// </summary>
    public ProjectionType Leg1FloatProjectionType { get; private set; }

    /// <summary>
    ///   Leg1 interest reference rate for this swap
    /// </summary>
    public InterestReferenceRate Leg1InterestReferenceRate
    {
      get
      {
        if (_leg1InterestReferenceRate != null) return _leg1InterestReferenceRate;
        if (!string.IsNullOrEmpty(Leg1FloatIndexName))
          _leg1InterestReferenceRate = InterestReferenceRate.Get(Leg1FloatIndexName);
        return _leg1InterestReferenceRate;
      }
    }

    /// <summary>
    ///   Leg2 currency
    /// </summary>
    public Currency Leg2Currency { get; private set; }

    /// <summary>
    ///   Leg2 DayCount
    /// </summary>
    public DayCount Leg2FixedDayCount { get; private set; }

    /// <summary>
    ///   Leg2 business-day convention
    /// </summary>
    public BDConvention Leg2FixedBdConvention { get; private set; }

    /// <summary>
    ///   Leg2 payment calendar
    /// </summary>
    public Calendar Leg2Calendar { get; private set; }

    /// <summary>
    ///   Leg2 floating leg reference index name
    /// </summary>
    public string Leg2FloatIndexName { get; private set; }

    /// <summary>
    ///   Optional Leg2 floating leg rate tenor (indexed by the swap tenor).
    ///   If not specified, the tenor of the reference index is used by the floating rate payment frequency is used
    /// </summary>
    public Tenor Leg2FloatIndexTenor { get; private set; }

    /// <summary>
    ///   Leg2 Floating leg payment frequency (indexed by the swap tenor).
    ///   If not specified, the fixed payment frequency is used
    /// </summary>
    public Frequency Leg2PaymentFreq { get; private set; }

    /// <summary>
    ///   Leg2 Floating leg compounding convention (if compounding)
    /// </summary>
    public CompoundingConvention Leg2CompoundingConvention { get; private set; }

    /// <summary>
    ///   Leg2 Floating leg compounding frequency (if compounding)
    /// </summary>
    public Frequency Leg2CompoundingFreq { get; private set; }

    /// <summary>
    ///   Leg2 Floating leg projection type
    /// </summary>
    public ProjectionType Leg2FloatProjectionType { get; private set; }

    /// <summary>
    ///   Leg2 interest reference rate for this swap
    /// </summary>
    public InterestReferenceRate Leg2InterestReferenceRate
    {
      get
      {
        if (_leg2InterestReferenceRate != null) return _leg2InterestReferenceRate;
        if (!string.IsNullOrEmpty(Leg2FloatIndexName))
          _leg2InterestReferenceRate = InterestReferenceRate.Get(Leg2FloatIndexName);
        return _leg2InterestReferenceRate;
      }
    }

    /// <summary>
    ///   Spread is quoted on leg 1, otherwise leg2
    /// </summary>
    public bool SpreadOnLeg1 { get; set; }

    /// <summary>
    ///   Business days between the last fixing date and the payment date
    /// </summary>
    public int PaymentLag { get; private set; }

    /// <summary>
    ///   ISDA market agreed coupon conventions
    /// </summary>
    public bool Mac { get; private set; }

    /// <summary>
    ///   Principal exchange
    /// </summary>
    public bool PrincipalExchange { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///   Returns whether the type of swap is a basis swap or not
    /// </summary>
    /// <returns></returns>
    public bool IsBasisSwap()
    {
      return (Leg1InterestReferenceRate != null && Leg2InterestReferenceRate != null);
    }

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
      if (string.IsNullOrWhiteSpace(Leg1FloatIndexName) && Leg1FixedDayCount == DayCount.None)
        InvalidValue.AddError(errors, this, "Leg1FixedDayCount", "Daycount for fixed leg must be specified");
      if (string.IsNullOrWhiteSpace(Leg2FloatIndexName) && Leg2FixedDayCount == DayCount.None)
        InvalidValue.AddError(errors, this, "Leg2FixedDayCount", "Daycount for fixed leg must be specified");
      if (!string.IsNullOrEmpty(Leg1FloatIndexName) && Leg1FloatIndexTenor == Tenor.Empty)
        InvalidValue.AddError(errors, this, "Leg1FloatingRateTenor", "If specified, one floating rate tenor must be specified");
      if (!string.IsNullOrEmpty(Leg2FloatIndexName) && Leg2FloatIndexTenor == Tenor.Empty)
        InvalidValue.AddError(errors, this, "Leg2FloatingRateTenor", "If specified, one floating rate tenor must be specified");
    }

    /// <summary>
    ///   Create standard interest rate swap given a date, a maturity tenor, and a fixed rate
    /// </summary>
    /// <remarks>
    ///   <para>Given the swap terms, the swap is created given a date and a maturity tenor, and a fixed rate as follows:</para>
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
    ///       <term><see cref="ProductWithSchedule.Freq"/></term>
    ///       <description>Set to the <see cref="SwapTerms.Leg2PaymentFreq"/>.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.Coupon"/></term>
    ///       <description>Set to the specified coupon.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.DayCount"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FixedDayCount"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.BDConvention"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FixedBdConvention"/>.</description>
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
    ///       <description>Set to the floating rate index matching the Terms
    ///       <see cref="SwapTerms.Leg2FloatIndexName"/> and <see cref="SwapTerms.Leg2FloatIndexTenor"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to the Reference Index <see cref="ReferenceRate.Currency"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Freq"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2PaymentFreq"/>.</description>
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
    ///       <term><see cref="SwapLeg.CompoundingConvention"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2CompoundingConvention"/>.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.CompoundingFrequency"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FloatIndexTenor"/> if the
    ///       payment frequency is less than the floating rate tenor.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.ProjectionType"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FloatProjectionType"/>.</description>
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
    /// <param name="fixedRate">Pay fixed swap leg coupon</param>
    /// <returns>Standard <see cref="Swap"/></returns>
    [ProductBuilder]
    public Swap GetProduct(Dt asOf, string tenorName, double fixedRate)
    {
      // Check to ensure fixed-float swap is not float-fixed
      var receiveRate = Leg1InterestReferenceRate ?? Leg2InterestReferenceRate;
      var payRate = Leg1InterestReferenceRate != null ? Leg2InterestReferenceRate ?? null : null;
      // for basis swaps both pay and receive both exist
      return GetSwap(asOf, tenorName, receiveRate, payRate, SpreadOnLeg1 ? fixedRate : 0.0, SpreadOnLeg1 ? 0.0 : fixedRate);
    }

    /// <summary>
    ///   Create standard interest rate swap given a date, a maturity tenor, and a fixed rate
    /// </summary>
    /// <remarks>
    ///   <para>Given the swap terms, the swap is created given a date and a maturity tenor, and a fixed rate as follows:</para>
    ///   <para><see cref="SwapTerms.Leg2PaymentFreq"/>, <see cref="SwapTerms.Leg2PaymentFreq"/>, and
    ///   <see cref="SwapTerms.Leg2FloatIndexTenor"/> to use.</para>
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
    ///       <term><see cref="ProductWithSchedule.Freq"/></term>
    ///       <description>Set to the <see cref="SwapTerms.Leg2PaymentFreq"/>.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.Coupon"/></term>
    ///       <description>Set to the specified coupon.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.DayCount"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FixedDayCount"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.BDConvention"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FixedBdConvention"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Calendar"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg1Calendar"/>.</description>
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
    ///       <description>Set to the floating rate index matching the Terms
    ///       <see cref="SwapTerms.Leg2FloatIndexName"/> and <see cref="SwapTerms.Leg2FloatIndexTenor"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to the Reference Index <see cref="ReferenceRate.Currency"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Freq"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2PaymentFreq"/>.</description>
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
    ///       <term><see cref="SwapLeg.CompoundingConvention"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2CompoundingConvention"/>.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.CompoundingFrequency"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FloatIndexTenor"/> if the
    ///       payment frequency is less than the floating rate tenor.</description>
    ///     </item><item>
    ///       <term><see cref="SwapLeg.ProjectionType"/></term>
    ///       <description>Set to the Terms <see cref="SwapTerms.Leg2FloatProjectionType"/>.</description>
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
    /// <param name="receiveRate">Receive reference rate</param>
    /// <param name="payRate">Pay reference rate</param>
    /// <param name="maturityTenor">Tenor name</param>
    /// <param name="payRateOrMargin">Pay fixed swap leg coupon or pay floating leg margin</param>
    /// <param name="recRateOrMargin">Receive fixed swap leg coupon or pay floating leg margin</param>
    /// <returns>Standard <see cref="Swap"/></returns>
    public Swap GetSwap(Dt asOf, string maturityTenor, 
      InterestReferenceRate receiveRate, InterestReferenceRate payRate,
      double recRateOrMargin, double payRateOrMargin)
    {
      var effective = GetSettlement(asOf);
      var maturity = GetMaturity(effective, maturityTenor);
      
      var recLeg1 = receiveRate == Leg1InterestReferenceRate && payRate == Leg2InterestReferenceRate;
      
      var fixedRate = recLeg1 ? recRateOrMargin : payRateOrMargin;
      var leg1 = (Leg1InterestReferenceRate == null)
          ? CreateFixedLeg(Leg1Currency, effective, maturity, Leg1PaymentFreq, Leg2FixedDayCount, Leg1Calendar,
          Leg1FixedBdConvention, Leg1CompoundingFreq, fixedRate, PrincipalExchange)
          : CreateFloatingLeg(Leg1Currency, effective, maturity, 
          Leg1Calendar == Calendar.None ? Leg1InterestReferenceRate.Calendar : Leg1Calendar, 
          Leg1PaymentFreq,
          fixedRate, Leg1InterestReferenceRate,
          Leg1FloatIndexTenor != Tenor.Empty ? Leg1FloatIndexTenor : new Tenor(Leg1PaymentFreq), Leg1FloatProjectionType,
          Leg1CompoundingFreq, Leg1CompoundingConvention, PrincipalExchange);
      
      fixedRate = recLeg1 ? payRateOrMargin : recRateOrMargin;
      var leg2 = (Leg2InterestReferenceRate == null)
        ? CreateFixedLeg(Leg2Currency, effective, maturity, Leg2PaymentFreq, Leg2FixedDayCount, Leg2Calendar,
        Leg2FixedBdConvention, Leg2CompoundingFreq, fixedRate, PrincipalExchange)
        : CreateFloatingLeg(Leg2Currency, effective, maturity,
        Leg2Calendar == Calendar.None ? Leg2InterestReferenceRate.Calendar : Leg2Calendar,
        Leg2PaymentFreq, 
        fixedRate, Leg2InterestReferenceRate,
        Leg2FloatIndexTenor != Tenor.Empty ? Leg2FloatIndexTenor : new Tenor(Leg2PaymentFreq), Leg2FloatProjectionType,
        Leg2CompoundingFreq, Leg2CompoundingConvention, PrincipalExchange);
      
      var swap = recLeg1 ? new Swap(leg1, leg2) : new Swap(leg2, leg1);
      swap.Validate();
      return swap;
    }

    /// <summary>
    /// Create fixed swap leg
    /// </summary>
    /// <param name="ccy">Currency</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="payFrequency">Fixed frequency</param>
    /// <param name="dayCount">Fixed day count</param>
    /// <param name="calendar">Fixed calendar</param>
    /// <param name="convention">Fixed BD convention</param>
    /// <param name="rate">Fixed rate</param>
    /// <param name="finalExchange">Final principal exchange</param>
    /// <param name="compFrequency">Compounding frequency</param>
    /// <returns>SwapLeg</returns>
    private SwapLeg CreateFixedLeg(Currency ccy, Dt effective, Dt maturity, 
      Frequency payFrequency, DayCount dayCount, Calendar calendar, BDConvention convention, Frequency compFrequency, double rate, bool finalExchange)
    {
      var leg = new SwapLeg(effective, maturity, ccy, rate, dayCount, payFrequency, convention, calendar, false)
      {
        FinalExchange = finalExchange,
        IsZeroCoupon = (payFrequency == Frequency.None),
        CompoundingFrequency = compFrequency
      };
      var psp = (IScheduleParams)leg.Schedule;
      leg.CycleRule = psp.CycleRule;
      leg.Maturity = psp.Maturity;
      return leg;
    }

    /// <summary>
    /// Create floating leg
    /// </summary>
    /// <param name="ccy">Currency</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="payFreq">Payment frequency</param>
    /// <param name="margin">Margin</param>
    /// <param name="rate">Reference rate</param>
    /// <param name="rateTenor">Rate tenor</param>
    /// <param name="projType">Rate projection type</param>
    /// <param name="compFreq">Compounding frequency</param>
    /// <param name="compoundingConv">Compounding frequency</param>
    /// <param name="finalExchange">Final principal exchange</param>
    /// <returns>SwapLeg</returns>
    private SwapLeg CreateFloatingLeg(Currency ccy, Dt effective, Dt maturity, Calendar calendar, Frequency payFreq, 
      double margin, InterestReferenceRate rate, Tenor rateTenor, 
      ProjectionType projType, Frequency compFreq, CompoundingConvention compoundingConv, bool finalExchange     
      )
    {
      var floatingTenor = (rateTenor != Tenor.Empty) ? rateTenor : new Tenor(payFreq);
      var floatingTenorFreq = (rateTenor != Tenor.Empty) ? floatingTenor.ToFrequency() : payFreq;
      var compoundingFrequency = (payFreq < floatingTenorFreq) ? /*payment less frequent*/ floatingTenorFreq 
        : floatingTenorFreq > payFreq ? floatingTenorFreq : Frequency.None;
      var referenceIndex = new BaseEntity.Toolkit.Base.ReferenceIndices.InterestRateIndex(rate, rateTenor);
      var leg = new SwapLeg(effective, maturity, payFreq, margin, referenceIndex, ccy, rate.DayCount, rate.BDConvention, calendar)
      {
        ProjectionType = projType,
        CompoundingConvention = compoundingConv,
        FinalExchange = finalExchange,
        CompoundingFrequency = compoundingFrequency,
        ResetLag = new Tenor(rate.DaysToSpot, TimeUnit.Days)
      };

      leg.InArrears = (projType == ProjectionType.ArithmeticAverageRate || projType == ProjectionType.GeometricAverageRate
                    || projType == ProjectionType.TBillArithmeticAverageRate || projType == ProjectionType.CPArithmeticAverageRate);

      var psp = (IScheduleParams)leg.Schedule;
      leg.CycleRule = psp.CycleRule;
      leg.Maturity = psp.Maturity;
      return leg;
    }

    /// <summary>
    /// Get settlement date for a standard swap given a pricing as-of date
    /// </summary>
    /// <param name="asOf">Pricing as-of date</param>
    /// <returns>Settlement date</returns>
    public Dt GetSettlement(Dt asOf)
    {
      return Dt.AddDays(asOf, SpotDays, _cals);
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
    /// <param name="issueDate">Settle date</param>
    /// <param name="tenor">Product description</param>
    /// <returns>Maturity date</returns>
    public Dt GetMaturity(Dt issueDate, string tenor)
    {
      Tenor realTenor;
      double d;
      Dt dt;
      if (tenor == "O/N" || tenor == "T/N")
        // Special keywords
        dt = Dt.AddDays(issueDate, 1, _cals);
      else if (Tenor.TryParse(tenor, out realTenor))
        // Tenor
        dt = (realTenor.Units == TimeUnit.Days) ? Dt.AddDays(issueDate, realTenor.N, _cals) : Dt.Add(issueDate, realTenor);
      else if (double.TryParse(tenor, out d))
        // XL date
        dt = Dt.FromExcelDate(d);
      else if (!Dt.TryParse(tenor, out dt))
        throw new ArgumentException($"Invalid swap maturity {tenor}. Can be O/N, T/N, Tenor, or date");
      return dt;
    }

    /// <summary>
    ///  Get unique key for Swap Terms
    /// </summary>
    /// <param name="location">Trading location</param>
    /// <param name="recFloatingIndexName">Pay floating rate index name</param>
    /// <param name="recIndexTenor">Pay floating rate index name</param>
    /// <param name="recCcy">Receive leg currency</param>
    /// <returns>Unique key</returns>
    public static string GetKey(
      Currency location,
      string recFloatingIndexName, Currency recCcy, Tenor recIndexTenor)
    {
      return GetKey(location, recFloatingIndexName, recCcy,
        recIndexTenor, "", recCcy, Tenor.Empty);
    }

    /// <summary>
    ///  Get unique key for Swap Terms
    /// </summary>
    /// <param name="location">Trading location</param>
    /// <param name="recFloatingIndexName">Pay floating rate index name</param>
    /// <param name="payFloatingIndexName">Receive floating rate index name</param>
    /// <param name="recIndexTenor">Pay floating rate index name</param>
    /// <param name="payIndexTenor">Receive floating rate index name</param>
    /// <param name="recCcy">Receive leg currency</param>
    /// <param name="payCcy">Pay leg currency</param>
    /// <returns>Unique key</returns>
    public static string GetKey(
      Currency location,
      string recFloatingIndexName, Currency recCcy, Tenor recIndexTenor, 
      string payFloatingIndexName, Currency payCcy, Tenor payIndexTenor)
    {
      return MakeKey(location, recFloatingIndexName, recCcy, 
        recIndexTenor, payFloatingIndexName, payCcy, payIndexTenor);
    }

    private static string MakeKey(
      Currency location,
      string recFloatingIndexName, Currency recCcy, Tenor recIndexTenor,
      string payFloatingIndexName, Currency payCcy, Tenor payIndexTenor
      )
    {
      var sb = new StringBuilder();

      if ((payCcy != Currency.None || recCcy != Currency.None) 
        && (location != Currency.None && location != payCcy && location != recCcy))
        sb.Append(location).Append('.');

      // Cross currency swap (unused for now)
      if (payCcy != Currency.None && recCcy != Currency.None && recCcy != payCcy)
      {
        if (!string.IsNullOrEmpty(recFloatingIndexName))
          sb.AppendIndexName(recFloatingIndexName, recIndexTenor);
        else
          sb.Append('.').Append(recCcy);

        if (!string.IsNullOrEmpty(payFloatingIndexName))
          sb.Append('.').AppendIndexName(payFloatingIndexName, payIndexTenor);
        else
          sb.Append('.').Append(payCcy);
        sb.Append(".BasisSwap");
      }
      else
      {
        if (!string.IsNullOrEmpty(recFloatingIndexName))
        {
          sb.AppendIndexName(recFloatingIndexName, recIndexTenor);
          if (!string.IsNullOrEmpty(payFloatingIndexName))
          {
            sb.Append('.').AppendIndexName(payFloatingIndexName, payIndexTenor);
            sb.Append(".BasisSwap");
          }
          else
          {
            sb.Append(".Swap");
          }
        }
        else
        {
          if (!string.IsNullOrEmpty(payFloatingIndexName))
            sb.AppendIndexName(payFloatingIndexName, payIndexTenor);
          sb.Append(".Swap");
        }
        
      }
      return sb.ToString();
    }

    /// <summary>
    /// Provides an interface to return the name of a quote
    /// </summary>
    /// <returns></returns>
    public override string GetQuoteName(string tenor)
    {
      return GetKeyForMaturity(tenor, this);
    }

    /// <summary>
    /// Get curve tenor name for a given maturity tenor
    /// </summary>
    /// <param name="tenor"></param>
    /// <param name="terms"></param>
    /// <returns></returns>
    public static string GetKeyForMaturity(
      string tenor,
      SwapTerms terms)
    {
      var tenorLeg1 = string.IsNullOrEmpty(terms.Leg1FloatIndexName) 
        ? Tenor.Empty : terms.Leg1FloatIndexTenor;
      var tenorLeg2 = string.IsNullOrEmpty(terms.Leg2FloatIndexName) 
        ? Tenor.Empty : terms.Leg2FloatIndexTenor;
      return GetKey(terms.Location, terms.Leg1FloatIndexName, terms.Leg1Currency, tenorLeg1,
        terms.Leg2FloatIndexName, terms.Leg2Currency, tenorLeg2);
    }

    #endregion

    #region Data

    private InterestReferenceRate _leg1InterestReferenceRate;
    private InterestReferenceRate _leg2InterestReferenceRate;
    private Calendar _cals => new Calendar(new Calendar[] { Leg1Calendar, Leg2Calendar });

    #endregion
  }
}
