//
//   2015. All rights reserved.
//

using System;
using System.ComponentModel;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  ///<preliminary/>
  /// <summary>
  ///   Definition of a Equity Price reference rate
  /// </summary>
  /// <remarks>
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]

  public class EquityReferenceRate : ReferenceRate
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
    /// <param name="calendar">Underlying bond calendar</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value (settlement) date</param>
    public EquityReferenceRate(string name, string description, Currency ccy, Calendar calendar, int daysToSpot)
      : base(name, description, ccy, Frequency.Daily, Tenor.Empty, calendar)
    {
      DaysToSpot = daysToSpot;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Settlement days (business days to spot rate)
    /// </summary>
    public int DaysToSpot { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Create reference index
    /// </summary>
    /// <param name="tenor">tenor (ignored)</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      return new ReferenceIndices.EquityPriceIndex(this);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="EquityReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="EquityReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="EquityReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static EquityReferenceRate Get(string name)
    {
      return GetValue<EquityReferenceRate>(name);
    }

    #endregion Static Methods
  }
}
