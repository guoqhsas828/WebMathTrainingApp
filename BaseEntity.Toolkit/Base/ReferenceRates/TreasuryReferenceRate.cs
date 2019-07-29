//
//   2015. All rights reserved.
//

using System;
using System.ComponentModel;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  ///<preliminary/>
  /// <summary>
  ///   Definition of a Constant Maturity Treasury (CMT) yield reference rate
  /// </summary>
  /// <remarks>
  ///   <para>Treasury bond yield reference rates are the yields for a defined constant maturity
  ///   fixed rate treasury bond. These are commonly called Constant Maturity Treasury (CMT) rates.</para>
  ///   <para>Treasury reference rates are uniquely identified by their name.</para>
  ///   <para>This implements the equivalent of the ISDA Floating Rate Option.</para>
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <example>
  /// The following example is of looking up a bond yield index by name.
  /// <code language="C#">
  ///    // Find USD UST index
  ///    var index = TreasuryReferenceRate.Get("UST");
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]

  public class TreasuryReferenceRate : ReferenceRate
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
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="frequency">Underlying bond coupon payment frequency</param>
    /// <param name="dayCount">Underlying bond daycount convention</param>
    /// <param name="calendar">Underlying bond calendar</param>
    /// <param name="roll">Underlying bond business day convention</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value (settlement) date</param>
    /// <param name="validTenors">List of valid tenors for this index</param>
    /// <param name="defaultTenor">Default tenor</param>
    public TreasuryReferenceRate(string name, string description, Currency ccy, Frequency frequency, DayCount dayCount,
      Calendar calendar, BDConvention roll, int daysToSpot, Tenor[] validTenors, Tenor defaultTenor)
      : base(name, description, ccy, Frequency.Daily, Tenor.Empty, calendar)
    {
      DayCount = dayCount;
      Roll = roll;
      DaysToSpot = daysToSpot;
      Frequency = frequency;
      ValidTenors = validTenors;
      DefaultTenor = (defaultTenor.IsEmpty && validTenors.Length > 0) ? validTenors[0] : defaultTenor;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Underlying bond coupon payment frequency
    /// </summary>
    public Frequency Frequency { get; }

    /// <summary>
    ///   Underlying bond daycount convention
    /// </summary>
    public DayCount DayCount { get; }

    /// <summary>
    ///   Underlying bond business day convention
    /// </summary>
    public BDConvention Roll { get; }

    /// <summary>
    ///   Settlement days (business days to spot rate)
    /// </summary>
    public int DaysToSpot { get; }

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
    /// Create reference index given underlying bond tenor
    /// </summary>
    /// <remarks>
    ///   <para>If <paramref name="tenor"/> is empty, <see cref="DefaultTenor"/> is used.</para>
    /// </remarks>
    /// <param name="tenor">Underlying bond tenor</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      if (tenor.IsEmpty)
        throw new ArgumentException("Tenor must be specified for a CMT reference rate");
      return new ReferenceIndices.ForwardYieldIndex(this, tenor);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="TreasuryReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="TreasuryReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="TreasuryReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static TreasuryReferenceRate Get(string name)
    {
      return GetValue<TreasuryReferenceRate>(name);
    }

    #endregion Static Methods
  }
}
