//
//   2015. All rights reserved.
//

using System;
using System.ComponentModel;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  ///<preliminary/>
  /// <summary>
  ///   Terms for defining an interest rate reference rate
  /// </summary>
  /// <remarks>
  ///   <para>Reference interest rate or index. This is used to identify the
  ///   underlying interest rate or index for a derivative transaction. This implements the
  ///   equivalent of the ISDA Floating Rate Option.</para>
  ///   <para>Interest reference rates fall into two major categories - OIS indices and Libor indices.</para>
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <example>
  /// <para>The following example is of looking up an interest reference rate by name.</para>
  /// <code language="C#">
  ///    // Find USD Libor index
  ///    var index = InterestReferenceRate.Get("USDLIBOR");
  /// </code>
  /// <para>This is the equivalent of the more general:</para>
  /// <code language="C#">
  ///    // Find USD Libor index
  ///    var index = ReferenceRateCache.GetValue{InterestReferenceRate}("USDLIBOR");
  /// </code>
  /// <para>The following example is of finding all interest rate indices of a specific currency.</para>
  /// <code language="C#">
  ///    // Find all USD interest rate indices
  ///    var currency = Currency.USD;
  ///    var rateIndices = ReferenceRateCache.All.OfType{InterestReferenceRate}().Where(i => i.Currency == currency);
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]
  public class InterestReferenceRate : ReferenceRate
  {
    #region Constructors

    /// <summary>
    /// Constructor (immutable)
    /// </summary>
    /// <remarks>
    ///   <para><see cref="IReferenceRate">Reference Rates</see> sould not be constructed outside of
    ///   initialisation or unusual usage. They should be looked up by name using <see cref="Get"/></para>
    /// </remarks>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="currency">Currency of the index</param>
    /// <param name="dayCount">Daycount convention of underlying rate</param>
    /// <param name="onDaysToSpot">Number of business days from as-of date to value date for O/N rate</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value date for tenors other than O/N</param>
    /// <param name="calendar">Calendar for value date calculation</param>
    /// <param name="roll">Business day convention for value date roll</param>
    /// <param name="rollCalendar">Calendar for value date roll. If not specified then <paramref name="calendar"/> is used</param>
    /// <param name="resetDateRule">Reset date rule: supported rules are None, in which case the reset date is driven by the settlement days 
    ///   (or resetLag set in projectionParams), Monday-Friday for weekly observations (i.e. every week on monday), First-EOM for monthly observations, 
    ///   i.e every fifteenth of the month </param>
    /// <param name="publicationFreq">Frequency of publication of the fixing</param>
    /// <param name="validTenors">List of valid tenors for this index</param>
    /// <param name="defaultTenor">Default tenor</param>
    public InterestReferenceRate(string name, string description, Currency currency, DayCount dayCount, int onDaysToSpot,
      int daysToSpot, Calendar calendar, BDConvention roll, Calendar rollCalendar, CycleRule resetDateRule,
      Frequency publicationFreq, Tenor[] validTenors, Tenor defaultTenor)
      : base(name, description, currency, publicationFreq, Tenor.Empty, calendar)
    {
      DayCount = dayCount;
      BDConvention = roll;
      OvernightDaysToSpot = onDaysToSpot;
      DaysToSpot = daysToSpot;
      ResetDateRule = resetDateRule;
      RollCalendar = rollCalendar;
      ValidTenors = validTenors;
      DefaultTenor = (defaultTenor.IsEmpty && validTenors.Length > 0) ? validTenors[0] : defaultTenor;
    }

    /// <summary>
    /// Constructor for an OIS-like interest reference reference
    /// </summary>
    /// <remarks>
    ///   <para><see cref="IReferenceRate">Reference Rates</see> sould not be constructed outside of
    ///   initialisation or unusual usage. They should be looked up by name using <see cref="Get"/></para>
    /// </remarks>
    /// <param name="name">Unique name</param>
    /// <param name="description">Description</param>
    /// <param name="currency">Currency of the index</param>
    /// <param name="dayCount">Daycount convention of underlying rate</param>
    /// <param name="calendar">Calendar for value date calculation</param>
    /// <param name="spotLag">Number of business days from as-of date to value date for tenors other than O/N</param>
    public InterestReferenceRate(string name, string description, Currency currency, DayCount dayCount, Calendar calendar, int spotLag)
      : this(name, description, currency, dayCount, spotLag, spotLag, calendar, /*BDConvention.Following should be? RD Jun'15*/ BDConvention.Modified, calendar, CycleRule.None,
      Frequency.Daily, new [] { Tenor.OneDay }, Tenor.OneDay)
    { }

    #endregion

    #region Properties

    /// <summary>
    ///   Daycount convention for the underlying rate
    /// </summary>
    public DayCount DayCount { get; }

    /// <summary>
    ///   Settlement days (business days to spot rate) for O/N rate
    /// </summary>
    public int OvernightDaysToSpot { get; }

    /// <summary>
    ///   Settlement days (business days to spot rate) for tenors other than the O/N rate
    /// </summary>
    public int DaysToSpot { get; }

    /// <summary>
    ///   Business day convention for settlement date
    /// </summary>
    public BDConvention BDConvention { get; }

    /// <summary>
    ///   Calendar for business day roll. If not specified, then <see cref="Calendar"/> is used
    /// </summary>
    public Calendar RollCalendar { get; }

    /// <summary>
    ///   Cycle rule for fixing reset dates
    /// </summary>
    public CycleRule ResetDateRule { get; }

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
    /// <param name="tenor">Tenor</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      return new ReferenceIndices.InterestRateIndex(this, tenor);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="InterestReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="InterestReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="InterestReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static InterestReferenceRate Get(string name)
    {
      return GetValue<InterestReferenceRate>(name);
    }

    #endregion Static Methods
  }
}
