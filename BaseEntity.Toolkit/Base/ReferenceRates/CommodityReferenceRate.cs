//
//   2015-2018. All rights reserved.
//

using System;
using System.ComponentModel;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  ///<preliminary/>
  /// <summary>
  ///   Definition of a commodity reference price.
  /// </summary>
  /// <remarks>
  ///   <para>Commodity reference price based on a defined price or index. This is used to identify the
  ///   underlying commodity or index for a derivative transaction. This implements the
  ///   equivalent of the ISDA Commodity Reference Price.</para>
  ///   <para>Examples include WTI Crude, Brent Crude Oil, Henry Hub Gas.</para>
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <example>
  /// <para>The following examples demonstrate finding the WTI Crude index.</para>
  /// <code language="C#">
  ///    // Find WTI Crude index
  ///    var index = CommodityReferenceRate.Get("WTICRUDE");
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]
  public class CommodityReferenceRate : ReferenceRate
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
    /// <param name="calendar">Calendar</param>
    /// <param name="publicationFreq">Frequency of publication of the fixing</param>
    public CommodityReferenceRate(string name, string description, Currency ccy, Calendar calendar, Frequency publicationFreq)
      : base(name, description, ccy, publicationFreq, Tenor.Empty, calendar)
    {}

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
    /// <param name="calendar">Calendar</param>
    public CommodityReferenceRate(string name, string description, Currency ccy, Calendar calendar)
      : base(name, description, ccy, Frequency.Daily, Tenor.Empty, calendar)
    { }

    #endregion

    #region Methods

    /// <summary>
    /// Create reference index
    /// </summary>
    /// <param name="tenor">Index tenor (ignored)</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      return new ReferenceIndices.CommodityPriceIndex(this);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="CommodityReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="CommodityReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="CommodityReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static CommodityReferenceRate Get(string name)
    {
      return GetValue<CommodityReferenceRate>(name);
    }

    #endregion Static Methods
  }
}
