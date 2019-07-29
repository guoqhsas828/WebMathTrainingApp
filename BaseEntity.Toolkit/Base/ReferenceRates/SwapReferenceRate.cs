//
//   2015. All rights reserved.
//

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  ///<preliminary/>
  /// <summary>
  ///   Definition for a Constant Maturity Swap (CMS) reference rate
  /// </summary>
  /// <remarks>
  ///   <para>Swap reference rates are the fixed rate for a defined constant maturity fixed/floating swap. These
  ///   are commonly called Constant Maturity Swap (CMS) rates.</para>
  ///   <para>This implements the equivalent of the ISDA Floating Rate Option.</para>
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <example>
  /// The following example is of looking up a swap reference rate by name.
  /// <code language="C#">
  ///    // Find USD CMS index
  ///    var index = SwapReferenceRate.Get("USDCMS");
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]
  public class SwapReferenceRate : ReferenceRate
  {
    #region Constructors

    /// <summary>
    /// Constructor (immutable)
    /// </summary>
    /// <remarks>
    ///   <para>This constructor allows specifying the floating reference rate name and defers looking up the floating reference rate till used.
    ///   This is convenient for initialisation where SwapReferenceRates depend on InterestReferenceRates.</para>
    ///   <para><see cref="IReferenceRate">Reference Rates</see> sould not be constructed outside of
    ///   initialisation or unusual usage. They should be looked up by name using <see cref="Get"/></para>
    /// </remarks>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="currency">Swap currency</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value (settlement) date</param>
    /// <param name="fixedDayCount">Fixed leg daycount</param>
    /// <param name="fixedBdConvention">Fixed leg business day convention</param>
    /// <param name="fixedCalendar">Fixed leg calendar</param>
    /// <param name="fixedPaymentFreq">Fixed leg payment frequency</param>
    /// <param name="floatingIndexName">Swap floating rate index name</param>
    /// <param name="floatingRateTenor">Swap floating rate tenor</param>
    /// <param name="validTenors">Valid (swap) tenors for this index</param>
    /// <param name="defaultTenor">Default tenor</param>
    public SwapReferenceRate(string name, string description, Currency currency, int daysToSpot,
      DayCount fixedDayCount, BDConvention fixedBdConvention, Calendar fixedCalendar, Frequency fixedPaymentFreq,
      string floatingIndexName, Tenor floatingRateTenor, Tenor[] validTenors, Tenor defaultTenor)
      : base(name, description, currency, Frequency.Daily, Tenor.Empty, fixedCalendar)
    {
      DaysToSpot = daysToSpot;
      FixedCurrency = currency;
      FixedDayCount = fixedDayCount;
      FixedBdConvention = fixedBdConvention;
      FixedPaymentFreq = fixedPaymentFreq;
      _floatingReferenceRateName = floatingIndexName; // reference index is looked up on usage
      FloatingRateTenor = floatingRateTenor;
      ValidTenors = validTenors;
      DefaultTenor = (defaultTenor.IsEmpty && validTenors.Length > 0) ? validTenors[0] : defaultTenor;
    }

    /// <summary>
    /// Constructor (immutable)
    /// </summary>
    /// <remarks>
    ///   <para>This constructor takds in an existing floating reference rate.</para>
    ///   <para><see cref="IReferenceRate">Reference Rates</see> sould not be constructed outside of
    ///   initialisation or unusual usage. They should be looked up by name using <see cref="Get"/></para>
    /// </remarks>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="currency">Swap currency</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value (settlement) date</param>
    /// <param name="fixedDayCount">Fixed leg daycount</param>
    /// <param name="fixedBdConvention">Fixed leg business day convention</param>
    /// <param name="fixedCalendar">Fixed leg calendar</param>
    /// <param name="fixedPaymentFreq">Fixed leg payment frequency</param>
    /// <param name="floatingReferenceRate">Swap floating reference rate</param>
    /// <param name="floatingRateTenor">Swap floating rate tenor</param>
    /// <param name="validTenors">Valid (swap) tenors for this index</param>
    /// <param name="defaultTenor">Default tenor</param>
    public SwapReferenceRate(string name, string description, Currency currency, int daysToSpot,
      DayCount fixedDayCount, BDConvention fixedBdConvention, Calendar fixedCalendar, Frequency fixedPaymentFreq,
      InterestReferenceRate floatingReferenceRate, Tenor floatingRateTenor, Tenor[] validTenors, Tenor defaultTenor)
      : base(name, description, currency, Frequency.Daily, Tenor.Empty, fixedCalendar)
    {
      DaysToSpot = daysToSpot;
      FixedCurrency = currency;
      FixedDayCount = fixedDayCount;
      FixedBdConvention = fixedBdConvention;
      FixedPaymentFreq = fixedPaymentFreq;
      _floatingReferenceRate = floatingReferenceRate;
      FloatingRateTenor = floatingRateTenor;
      ValidTenors = validTenors;
      DefaultTenor = (defaultTenor.IsEmpty && validTenors.Length > 0) ? validTenors[0] : defaultTenor;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Days to settle
    /// </summary>
    public int DaysToSpot { get; }

    /// <summary>
    ///   Fixed leg currency
    /// </summary>
    public Currency FixedCurrency { get; }

    /// <summary>
    ///   Fixed leg DayCount
    /// </summary>
    public DayCount FixedDayCount { get; }

    /// <summary>
    ///   Fixed leg Business-day convention
    /// </summary>
    public BDConvention FixedBdConvention { get; }

    /// <summary>
    ///   Fixed leg calendar
    /// </summary>
    public Calendar FixedCalendar => Calendar;

    /// <summary>
    ///   Fixed leg payment frequency
    /// </summary>
    public Frequency FixedPaymentFreq { get; }

    /// <summary>
    /// Floating leg reference rate
    /// </summary>
    public InterestReferenceRate FloatingReferenceRate
    {
      get
      {
        // Below allows for defered loading of interest reference rate, convenient for initialisation and xml load
        if (_floatingReferenceRate == null)
          _floatingReferenceRate = InterestReferenceRate.Get(_floatingReferenceRateName);
        return _floatingReferenceRate;
      }
    }

    /// <summary>
    ///   Floating leg rate tenor
    /// </summary>
    public Tenor FloatingRateTenor { get; }

    /// <summary>
    /// Default tenor. For some types of reference rates this is empty.
    /// </summary>
    public override Tenor DefaultTenor { get; }

    /// <summary>
    /// List of valid tenors. For some types of reference rates this is empty
    /// </summary>
    public override Tenor[] ValidTenors { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Create reference index
    /// </summary>
    /// <remarks>
    ///   <para>If <paramref name="tenor"/> is empty, <see cref="DefaultTenor"/> is used.</para>
    /// </remarks>
    /// <param name="tenor">Index tenor</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      return new ReferenceIndices.SwapRateIndex(this, tenor);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="SwapReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="SwapReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="SwapReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static SwapReferenceRate Get(string name)
    {
      return GetValue<SwapReferenceRate>(name);
    }

    #endregion Static Methods

    #region Data

    private readonly string _floatingReferenceRateName;
    [XmlIgnore]
    private InterestReferenceRate _floatingReferenceRate; // Don't load/save this to xml, look up from name

    #endregion Data

  }
}
