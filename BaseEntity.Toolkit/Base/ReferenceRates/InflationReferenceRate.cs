//
//   2015. All rights reserved.
//

using System;
using System.ComponentModel;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  ///<preliminary/>
  /// <summary>
  ///   Terms for a inflation reference rate.
  /// </summary>
  /// <remarks>
  ///   <para>Inflation reference rate or index. This is used to identify the
  ///   underlying inflation rate or index for a derivative transaction. This implements the
  ///   equivalent of the ISDA Floating Rate Option.</para>
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <example>
  /// The following example is of looking up an index by name.
  /// <code language="C#">
  ///    // Find USD CPI index
  ///    var index = InflationReferenceRate.Get("USDCPI");
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]
  public class InflationReferenceRate : ReferenceRate
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
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="observationFrequency">Frequency of historical observations (usually Annual)</param>
    /// <param name="publicationLag">Lag between as-of date and publication date</param>
    public InflationReferenceRate(string name, string description, Currency ccy, DayCount dayCount, Calendar calendar, BDConvention roll,
      Frequency observationFrequency, Tenor publicationLag)
      : base(name, description, ccy, observationFrequency, publicationLag, calendar)
    {
      DayCount = dayCount;
      Roll = roll;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Daycount used for determination of the resets
    /// </summary>
    public DayCount DayCount { get; }

    /// <summary>
    ///   Business day convention
    /// </summary>
    public BDConvention Roll { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Create reference index
    /// </summary>
    /// <param name="tenor">Index tenor (ignored)</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      return new ReferenceIndices.InflationIndex(this);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="InflationReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="InflationReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="InflationReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static InflationReferenceRate Get(string name)
    {
      return GetValue<InflationReferenceRate>(name);
    }

    #endregion Static Methods
  }
}
