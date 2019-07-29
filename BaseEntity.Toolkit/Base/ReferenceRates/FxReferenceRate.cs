//
//   2016-2018. All rights reserved.
//

using System;
using System.ComponentModel;
using System.Linq;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  ///<preliminary/>
  /// <summary>
  ///   Definition of a FX reference price.
  /// </summary>
  /// <remarks>
  ///   <para>FX reference price based on a defined price or index. This is used to identify the
  ///   underlying FX or index for a derivative transaction. This implements the
  ///   equivalent of the ISDA FX Reference Price.</para>
  ///   <para>Examples include EURUSD, AUDCAD, USDJPY.</para>
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <example>
  /// <para>The following examples demonstrate finding the GBPUSD index.</para>
  /// <code language="C#">
  ///    // Find GBPUSD index
  ///    var index = FxReferenceRate.Get("GBPUSD");
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]
  public class FxReferenceRate : ReferenceRate
  {
    #region Constructors

    /// <summary>
    /// Constructor (immutable)
    /// </summary>
    /// <remarks>
    ///   <para><see cref="IReferenceRate">Reference Rates</see> sould not be constructed outside of
    ///   initialisation or unusual usage. They should be looked up by name using <see cref="Get(string)"/></para>
    /// </remarks>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="foreignCcy">Currency of denomination of the index</param>
    /// <param name="domesticCcy">Currency of denomination of the index</param>
    /// <param name="baseCalendar">Base Calendar</param>
    /// <param name="quoteCalendar">Quote Calendar</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value (settlement) date</param>
    /// <param name="rollConvention">Date Roll convention</param>
    public FxReferenceRate(string name, string description, Currency foreignCcy, Currency domesticCcy, Calendar quoteCalendar,
      Calendar baseCalendar, int daysToSpot, BDConvention rollConvention)
      : base(name, description, domesticCcy, Frequency.Daily, Tenor.Empty, baseCalendar)
    {
      ForeignCurrency = foreignCcy;
      QuoteCalendar = quoteCalendar;
      DaysToSpot = daysToSpot;
      RollConvention = rollConvention;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Foreign currency
    /// </summary>
    public Currency ForeignCurrency { get; }

    /// <summary>
    /// Domestic currency
    /// </summary>
    public Currency DomesticCurrency => Currency;

    /// <summary>
    /// Quote Calendar
    /// </summary>
    public Calendar QuoteCalendar { get; }

    /// <summary>
    /// Spot settlement delay
    /// </summary>
    public int DaysToSpot { get; }

    /// <summary>
    /// Business roll convention
    /// </summary>
    public BDConvention RollConvention { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Create reference index
    /// </summary>
    /// <param name="tenor">Index tenor (ignored)</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      return new ReferenceIndices.FxRateIndex(this);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="FxReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="FxReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="FxReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static FxReferenceRate Get(string name)
    {
      return GetValue<FxReferenceRate>(name);
    }

    /// <summary>
    /// Find <see cref="FxReferenceRate"/> matching specified currency pair
    /// </summary>
    /// <param name="foreignCcy">Currency of denomination of the index</param>
    /// <param name="domesticCcy">Currency of denomination of the index</param>
    /// <returns>Found <see cref="FxReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="FxReferenceRate"/> matching currency pair not found or multiple found</exception>
    public static FxReferenceRate Get(Currency foreignCcy, Currency domesticCcy)
    {
      var fxrr = GetValueWhere<FxReferenceRate>(rr => rr.ForeignCurrency == foreignCcy && rr.Currency == domesticCcy).FirstOrDefault();
      if (fxrr == null)
        throw new ArgumentException($"Pre-defined FxReferenceRate not found for currency pair {foreignCcy}{domesticCcy}");
      return fxrr;
    }

    #endregion Static Methods
  }
}
