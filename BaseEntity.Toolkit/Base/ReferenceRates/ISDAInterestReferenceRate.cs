//
//   2016. All rights reserved.
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
  ///    // Find USD ISDA reference rate
  ///    var index = ISDAInterestReferenceRate.Get("USD_ISDA");
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  [Serializable]
  [ImmutableObject(true)]
  public class ISDAInterestReferenceRate : ReferenceRate
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
    /// <param name="daysToSpot">Number of business days from as-of date to value date for tenors other than O/N</param>
    /// <param name="calendar">Calendar for business day roll</param>
    /// <param name="roll">Business day convention for value date roll</param>
    /// <param name="tenor">Currency specific tenor for ISDA curve</param>
    /// <param name="mmDayCount">Daycount convention of underlying rate</param>
    /// <param name="swapDayCount">Fixed daycount convention</param>
    /// <param name="swapFreq">Swap fixed leg frequency</param>
    public ISDAInterestReferenceRate(string name, string description, Currency currency, int daysToSpot, Calendar calendar,
      BDConvention roll, Tenor tenor, DayCount mmDayCount, DayCount swapDayCount, Frequency swapFreq)
      : base(name, description, currency, Frequency.Daily, Tenor.Empty, calendar)
    {
      DaysToSpot = daysToSpot;
      BDConvention = roll;
      MoneyMarketDayCount = mmDayCount;
      SwapFixedDayCount = swapDayCount;
      SwapFixedFrequency = swapFreq;
      Tenor = tenor;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Daycount convention for the underlying rate
    /// </summary>
    public DayCount MoneyMarketDayCount { get; }

    /// <summary>
    ///   Daycount convention for the underlying rate
    /// </summary>
    public DayCount SwapFixedDayCount { get; }

    /// <summary>
    ///   Daycount convention for the underlying rate
    /// </summary>
    public Frequency SwapFixedFrequency { get; }

    /// <summary>
    ///   Settlement days (business days to spot rate) for tenors other than the O/N rate
    /// </summary>
    public int DaysToSpot { get; }

    /// <summary>
    ///   Business day convention for settlement date
    /// </summary>
    public BDConvention BDConvention { get; }

    /// <summary>
    ///   Tenor for this index
    /// </summary>
    public Tenor Tenor { get; }

    /// <summary>
    /// Default tenor. For some types of reference rates this is empty.
    /// </summary>
    public override Tenor DefaultTenor => Tenor;

    /// <summary>
    /// List of valid tenors. For some types of reference rates this is empty
    /// </summary>
    public override Tenor[] ValidTenors => new[] {Tenor};

    #endregion

    #region Methods

    /// <summary>
    /// Create reference index
    /// </summary>
    /// <param name="tenor">Index tenor (ignored)</param>
    public override ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor)
    {
      return new ReferenceIndices.InterestRateIndex(this);
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Find <see cref="ISDAInterestReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="ISDAInterestReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="ISDAInterestReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public ISDAInterestReferenceRate Get(string name)
    {
      return GetValue<ISDAInterestReferenceRate>(name);
    }

    #endregion Static Methods
  }
}