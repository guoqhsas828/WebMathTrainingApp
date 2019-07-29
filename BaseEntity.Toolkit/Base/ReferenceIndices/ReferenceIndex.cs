//
//  -2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Base.ReferenceIndices
{
  /// <summary>
  /// Abstract base class of all reference indices
  /// </summary>
  /// <remarks>
  /// <para>This class contains the data to represent a published index</para>
  /// </remarks>
  [Serializable]
  public abstract class ReferenceIndex : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="referenceRate">Reference Rate</param>
    /// <param name="indexTenor">Index tenor</param>
    protected internal ReferenceIndex(IReferenceRate referenceRate, Tenor indexTenor)
    {
      var indexName = referenceRate.Key;
      Name = indexName + ((indexName.Contains(indexTenor.ToString("S", null)) || indexTenor == Tenor.Empty || indexTenor == Tenor.OneDay)
               ? "" : "_" + indexTenor.ToString("S", null));
      ReferenceRate = referenceRate;
      IndexTenor = indexTenor;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Clone method
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public abstract override object Clone();

    /// <summary>
    /// Test equality
    /// </summary>
    /// <param name="other">Other index</param>
    /// <returns>True if this ReferenceIndex equals other</returns>
    public virtual bool Equals(ReferenceIndex other)
    {
      if (other == null)
        return false;
      if (this == other)
        return true;
      if (GetType() != other.GetType())
        return false;
      if (Currency != other.Currency)
        return false;
      if (IndexTenor != other.IndexTenor)
        return false;
      return true;
    }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return string.IsNullOrEmpty(IndexName) ? base.ToString() : $"{this.GetType().Name}:{Name}";
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Reference index name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Index tenor
    /// </summary>
    public Tenor IndexTenor { get; }

    /// <summary>
    /// ReferenceRate
    /// </summary>
    public IReferenceRate ReferenceRate { get; }

    #region ReferenceRate Properties

    /// <summary>
    /// Index name
    /// </summary>
    public string IndexName => ReferenceRate.Key;

    /// <summary>
    /// Currency of denomination of the index
    /// </summary>
    public Currency Currency => ReferenceRate.Currency;

    /// <summary>
    /// Calendar
    /// </summary>
    public Calendar Calendar => ReferenceRate.Calendar;

    /// <summary>
    /// Frequency of historical observations
    /// </summary>
    public Frequency PublicationFrequency => ReferenceRate.PublicationFrequency;

    #endregion ReferenceRate Properties

    #region ReferenceRate Properties (derived)

    /// <summary>
    /// Business day convention
    /// </summary>
    public virtual BDConvention Roll => BDConvention.Modified;

    /// <summary>
    /// Daycount used for determination of the resets
    /// </summary>
    public virtual DayCount DayCount => DayCount.None;

    /// <summary>
    /// Settlement days
    /// </summary>
    public virtual int SettlementDays => 0;

    /// <summary>
    /// Cycle rule for fixing reset dates
    /// </summary>
    public virtual CycleRule ResetDateRule => CycleRule.None;

    #endregion Reference Rate Properties (derived)

    /// <summary>
    /// Projection types for the index: could be more than one 
    /// </summary>
    public abstract ProjectionType[] ProjectionTypes { get; }

    ///<summary>
    /// Historical observations
    ///</summary>
    public RateResets HistoricalObservations { get; set; }

    #endregion Properties
  }

  /// <summary>
  /// Interest rate index, such as Libor index
  /// </summary>
  [Serializable]
  public class InterestRateIndex : ReferenceIndex
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(InterestRateIndex));

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="indexTenor">Index tenor</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="publicationFreq">Frequency of publication of the fixing</param>
    /// <param name="resetDtRule">Reset date rule: supported rules are None, in which case the reset date is driven by the settlement days 
    /// (or resetLag set in projectionParams), Monday-Friday for weekly observations (i.e. every week on monday), First-EOM for monthly observations, 
    /// i.e every fifteenth of the month </param>
    /// <param name="settlementDays">Number of settlement days</param>
    public InterestRateIndex(string indexName, Tenor indexTenor, Currency ccy, DayCount dayCount, Calendar calendar, BDConvention roll,
                             Frequency publicationFreq, CycleRule resetDtRule, int settlementDays)
      : base(FindInterestReferenceRate(indexName, indexName, ccy, dayCount, settlementDays,
        settlementDays, calendar, roll, calendar, resetDtRule, publicationFreq, new Tenor[] {}, Tenor.Empty),
          indexTenor)
    { }

    /// <summary>
    /// Constructor (OIS)
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="indexTenor">Index tenor</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="settlementDays">Settlement days</param>
    public InterestRateIndex(string indexName, Tenor indexTenor, Currency ccy, DayCount dayCount, Calendar calendar, BDConvention roll, int settlementDays)
      : base(FindInterestReferenceRate(indexName, indexName, ccy, dayCount, settlementDays,
          settlementDays, calendar, roll, calendar, CycleRule.None, Frequency.Daily, new Tenor[] { }, Tenor.OneDay),
        indexTenor)
    { }

    ///<summary>
    /// Constructor (OIS)
    ///</summary>
    /// <remarks >
    /// <para>Tenor matches index frequency, roll convention as Modified</para>
    /// </remarks>
    ///<param name="indexName">Index name</param>
    ///<param name="indexFrequency">Index frequency</param>
    ///<param name="ccy">Currency of denomination of the index</param>
    ///<param name="dayCount">Daycount convention used for reset computation</param>
    ///<param name="calendar">Calendar</param>
    /// <param name="settlementDays">Settlement days</param>
    public InterestRateIndex(string indexName, Frequency indexFrequency, Currency ccy, DayCount dayCount, Calendar calendar, int settlementDays)
      : base(FindInterestReferenceRate(indexName, indexName, ccy, dayCount, settlementDays,
          settlementDays, calendar, BDConvention.Modified, calendar, CycleRule.None, Frequency.Daily, new Tenor[] { }, Tenor.Empty),
        new Tenor(indexFrequency))
    { }

    /// <summary>
    /// Constructor from <see cref="InterestReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">Interest Reference Rate</param>
    /// <param name="indexTenor">Index tenor</param>
    public InterestRateIndex(InterestReferenceRate referenceRate, Tenor indexTenor)
      : base(referenceRate, indexTenor)
    { }

    /// <summary>
    /// Constructor from <see cref="ISDAInterestReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">ISDA Interest Reference Rate</param>
    public InterestRateIndex(ISDAInterestReferenceRate referenceRate)
      : base(referenceRate, referenceRate.Tenor)
    { }

    #endregion

    #region Methods

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      if( ReferenceRate is InterestReferenceRate )
        return new InterestRateIndex(((InterestReferenceRate)ReferenceRate).Clone() as InterestReferenceRate, IndexTenor)
        { HistoricalObservations = HistoricalObservations };
      else if (ReferenceRate is ISDAInterestReferenceRate)
        return new InterestRateIndex(((ISDAInterestReferenceRate)ReferenceRate).Clone() as ISDAInterestReferenceRate)
        { HistoricalObservations = HistoricalObservations };
      else
        throw new Exception($"Internal error: InterestRate ReferenceRate invalid type {typeof(ReferenceRate)}");
    }


    /// <summary>
    /// Find or construct InterestReferenceRate
    /// </summary>
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
    private static InterestReferenceRate FindInterestReferenceRate(string name, string description, Currency currency, DayCount dayCount,
      int onDaysToSpot, int daysToSpot, Calendar calendar, BDConvention roll, Calendar rollCalendar, CycleRule resetDateRule,
      Frequency publicationFreq, Tenor[] validTenors, Tenor defaultTenor)
    {
#if DEBUG
      var irr = ReferenceRates.ReferenceRate.GetValueWhere<InterestReferenceRate>(
        rr => rr.Currency == currency && rr.DayCount == dayCount && rr.OvernightDaysToSpot == onDaysToSpot && rr.DaysToSpot == daysToSpot &&
              rr.Calendar == calendar && rr.BDConvention == roll && rr.RollCalendar == rollCalendar && rr.ResetDateRule == resetDateRule &&
              rr.PublicationFrequency == publicationFreq
      ).FirstOrDefault();
      logger.Debug($"Creating temp InterestReferenceRate {name}");
      logger.Debug(irr != null ? $"Matched built in terms {irr.Key}" : $"Does not match any built in terms");
#endif
      return new InterestReferenceRate(name, description, currency, dayCount, onDaysToSpot,
               daysToSpot, calendar, roll, rollCalendar, resetDateRule,
               publicationFreq, validTenors, defaultTenor);
    }

#endregion

#region Properties

#region Reference Rate Properties

    /// <summary>
    /// Business day convention
    /// </summary>
    public override BDConvention Roll
    {
      get
      {
        if (ReferenceRate is InterestReferenceRate)
          return ((InterestReferenceRate)ReferenceRate).BDConvention;
        else if (ReferenceRate is ISDAInterestReferenceRate)
          return ((ISDAInterestReferenceRate)ReferenceRate).BDConvention;
        else
          throw new Exception($"Internal error: InterestRate ReferenceRate invalid type {typeof(ReferenceRate)}");
      }
    }

    /// <summary>
    /// Daycount used for determination of the resets
    /// </summary>
    public override DayCount DayCount
    {
      get
      {
        if (ReferenceRate is InterestReferenceRate)
          return ((InterestReferenceRate)ReferenceRate).DayCount;
        else if (ReferenceRate is ISDAInterestReferenceRate)
          return ((ISDAInterestReferenceRate)ReferenceRate).MoneyMarketDayCount;
        else
          throw new Exception($"Internal error: InterestRate ReferenceRate invalid type {typeof(ReferenceRate)}");
      }
    }

    /// <summary>
    /// Settlement days
    /// </summary>
    public override int SettlementDays
    {
      get
      {
        if (ReferenceRate is InterestReferenceRate)
          return (IndexTenor.Units == TimeUnit.Days && IndexTenor.N == 1)
            ? ((InterestReferenceRate)ReferenceRate).OvernightDaysToSpot
            : ((InterestReferenceRate)ReferenceRate).DaysToSpot;
        else if (ReferenceRate is ISDAInterestReferenceRate)
          return ((ISDAInterestReferenceRate)ReferenceRate).DaysToSpot;
        else
          throw new Exception($"Internal error: InterestRate ReferenceRate invalid type {typeof(ReferenceRate)}");
      }
    }

    /// <summary>
    /// Cycle rule for fixing reset dates
    /// </summary>
    public override CycleRule ResetDateRule
    {
      get
      {
        if (ReferenceRate is InterestReferenceRate)
          return ((InterestReferenceRate)ReferenceRate).ResetDateRule;
        else if (ReferenceRate is ISDAInterestReferenceRate)
          return CycleRule.None;
        else
          throw new Exception($"Internal error: InterestRate ReferenceRate invalid type {typeof(ReferenceRate)}");
      }
    }

#endregion Reference Rate Properties

    /// <summary>
    /// Admissible projection types
    /// </summary>
    public override ProjectionType[] ProjectionTypes => new[]
    {
      ProjectionType.SimpleProjection, ProjectionType.GeometricAverageRate,
      ProjectionType.ArithmeticAverageRate, ProjectionType.TBillArithmeticAverageRate,
      ProjectionType.CPArithmeticAverageRate
    };

#endregion
  }

  /// <summary>
  /// Swap rate index, i.e constant maturity swap index
  /// </summary>
  [Serializable]
  public class SwapRateIndex : ReferenceIndex
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SwapRateIndex));

#region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="indexTenor">Index tenor</param>
    /// <param name="indexFrequency">Fixed leg payment frequency</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="settlementDays">Settlement days</param>
    /// <param name="fwdRateIndex">Underline forward rate index</param>
    public SwapRateIndex(string indexName, Tenor indexTenor, Frequency indexFrequency, Currency ccy, DayCount dayCount,
      Calendar calendar, BDConvention roll, int settlementDays, InterestRateIndex fwdRateIndex)
      : base(FindSwapReferenceRate(indexName, indexName, ccy, settlementDays, dayCount, roll, calendar, indexFrequency,
        fwdRateIndex?.ReferenceRate as InterestReferenceRate, fwdRateIndex?.IndexTenor ?? Tenor.Empty, new Tenor[] {}, Tenor.Empty), indexTenor)
    {
      ForwardRateIndex = fwdRateIndex;
    }

    /// <summary>
    /// Constructor from <see cref="SwapReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">Swap Reference Rate</param>
    /// <param name="indexTenor">Index tenor</param>
    public SwapRateIndex(SwapReferenceRate referenceRate, Tenor indexTenor)
      : base(referenceRate, indexTenor)
    {
      ForwardRateIndex = new InterestRateIndex(referenceRate.FloatingReferenceRate, referenceRate.FloatingRateTenor);
    }

#endregion

#region Properties

    /// <summary>
    /// Swap Reference Rate
    /// </summary>
    public SwapReferenceRate SwapReferenceRate => ReferenceRate as SwapReferenceRate;

    /// <summary>
    /// Forward rate tenor. This is only needed if the projection curve and the discounting curve are different
    /// </summary>
    public InterestRateIndex ForwardRateIndex { get; }

    /// <summary>
    /// Swap fixed leg payment frequency
    /// </summary>
    public Frequency IndexFrequency => SwapReferenceRate.FixedPaymentFreq;

    /// <summary>
    /// Business day convention
    /// </summary>
    public override BDConvention Roll => SwapReferenceRate.FixedBdConvention;

    /// <summary>
    /// Daycount used for determination of the resets
    /// </summary>
    public override DayCount DayCount => SwapReferenceRate.FixedDayCount;

    /// <summary>
    /// Settlement days
    /// </summary>
    public override int SettlementDays => SwapReferenceRate.DaysToSpot;

    /// <summary>
    /// Admissible projection types
    /// </summary>
    public override ProjectionType[] ProjectionTypes => new[] {ProjectionType.SwapRate};

#endregion

#region Methods

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      return new SwapRateIndex(SwapReferenceRate.Clone() as SwapReferenceRate, IndexTenor);
    }

    /// <summary>
    /// Find or construct SwapReferenceRate
    /// </summary>
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
    private static SwapReferenceRate FindSwapReferenceRate(string name, string description, Currency currency, int daysToSpot,
      DayCount fixedDayCount, BDConvention fixedBdConvention, Calendar fixedCalendar, Frequency fixedPaymentFreq,
      InterestReferenceRate floatingReferenceRate, Tenor floatingRateTenor, Tenor[] validTenors, Tenor defaultTenor)
    {
#if DEBUG
      var irr = ReferenceRates.ReferenceRate.GetValueWhere<SwapReferenceRate>(
        rr => rr.Currency == currency && rr.DaysToSpot == daysToSpot && rr.FixedDayCount == fixedDayCount &&
              rr.FixedBdConvention == fixedBdConvention && rr.FixedCalendar == fixedCalendar && rr.FixedPaymentFreq == fixedPaymentFreq &&
              rr.FloatingReferenceRate?.Currency == floatingReferenceRate?.Currency && rr.FloatingReferenceRate?.DayCount == floatingReferenceRate?.DayCount &&
              rr.FloatingReferenceRate?.OvernightDaysToSpot == floatingReferenceRate?.OvernightDaysToSpot && rr.FloatingReferenceRate?.DaysToSpot == floatingReferenceRate?.DaysToSpot &&
              rr.FloatingReferenceRate?.Calendar == floatingReferenceRate?.Calendar && rr.FloatingReferenceRate?.BDConvention == floatingReferenceRate?.BDConvention &&
              rr.FloatingReferenceRate?.RollCalendar == floatingReferenceRate?.RollCalendar && rr.FloatingReferenceRate?.ResetDateRule == floatingReferenceRate?.ResetDateRule &&
              rr.FloatingReferenceRate?.PublicationFrequency == floatingReferenceRate?.PublicationFrequency
      ).FirstOrDefault();
      logger.Debug($"Creating temp SwapReferenceRate {name}");
      logger.Debug(irr != null ? $"Matched built in terms {irr.Key}" : $"Does not match any built in terms");
#endif
      return new SwapReferenceRate(name, description, currency, daysToSpot, fixedDayCount, fixedBdConvention, fixedCalendar, fixedPaymentFreq,
               floatingReferenceRate, floatingRateTenor, validTenors, defaultTenor);
    }

#endregion
  }

  /// <summary>
  /// Inflation index
  /// </summary>
  [Serializable]
  public class InflationIndex : ReferenceIndex
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(InflationIndex));

#region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="observationFrequency">Frequency of historical observations</param>
    /// <param name="publicationLag">Lag between inflation effective and index publication date</param>
    /// <remarks>Unless otherwise specified inflation rate tenor is yearly (YoY)</remarks>
    public InflationIndex(string indexName, Currency ccy, DayCount dayCount, Calendar calendar, BDConvention roll, Frequency observationFrequency,
      Tenor publicationLag)
      : base(new InflationReferenceRate(indexName, indexName, ccy, dayCount, calendar, roll, observationFrequency, publicationLag), Tenor.OneYear)
    { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="indexTenor">Index tenor</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="observationFrequency">Frequency of historical observations</param>
    /// <param name="publicationLag">Lag between inflation effective and index publication date</param>
    /// <remarks>Unless otherwise specified inflation rate tenor is yearly (YoY)</remarks>
    public InflationIndex(string indexName, Tenor indexTenor, Currency ccy, DayCount dayCount, Calendar calendar, BDConvention roll, Frequency observationFrequency,
      Tenor publicationLag)
      : base(FindInflationReferenceRate(indexName, indexName, ccy, dayCount, calendar, roll, observationFrequency, publicationLag), indexTenor)
    { }

    /// <summary>
    /// Constructor from <see cref="InflationReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">Inflation Reference Rate</param>
    public InflationIndex(InflationReferenceRate referenceRate)
      : base(referenceRate, new Tenor(referenceRate.PublicationFrequency))
    { }

#endregion

#region Properties

    /// <summary>
    /// Inflation Reference Rate
    /// </summary>
    InflationReferenceRate InflationReferenceRate => ReferenceRate as InflationReferenceRate;

#region Reference Rate Properties

    /// <summary>
    /// Business day convention
    /// </summary>
    public override BDConvention Roll => InflationReferenceRate.Roll;

    /// <summary>
    /// Daycount used for determination of the resets
    /// </summary>
    public override DayCount DayCount => InflationReferenceRate.DayCount;

    /// <summary>
    /// Cycle rule for fixing reset dates
    /// </summary>
    public override CycleRule ResetDateRule => CycleRule.First;

#endregion Reference Rate Properties

    /// <summary>
    /// Admissible projection types
    /// </summary>
    public override ProjectionType[] ProjectionTypes => new[] {ProjectionType.InflationRate, ProjectionType.InflationForward};

    /// <summary>
    /// Lag between the end of the inflation reference period and index publication date 
    /// </summary>
    public Tenor PublicationLag => InflationReferenceRate.PublicationLag;

#endregion

#region Methods

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      return new InflationIndex(InflationReferenceRate.Clone() as InflationReferenceRate)
        {HistoricalObservations = HistoricalObservations};
    }

    /// <summary>
    /// Find or create InflationReferenceRate
    /// </summary>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="observationFrequency">Frequency of historical observations (usually Annual)</param>
    /// <param name="publicationLag">Lag between as-of date and publication date</param>
    private static InflationReferenceRate FindInflationReferenceRate(string name, string description, Currency ccy, DayCount dayCount, Calendar calendar,
      BDConvention roll, Frequency observationFrequency, Tenor publicationLag)
    {
#if DEBUG
      var irr = ReferenceRates.ReferenceRate.GetValueWhere<InflationReferenceRate>(
        rr => rr.Currency == ccy && rr.DayCount == dayCount && rr.Calendar == calendar &&
        rr.Roll == roll && rr.PublicationFrequency == observationFrequency && rr.PublicationLag == publicationLag
      ).FirstOrDefault();
      logger.Debug($"Creating temp InflationReferenceRate {name}");
      logger.Debug(irr != null ? $"Matched built in terms {irr.Key}" : $"Does not match any built in terms");
#endif
      return new InflationReferenceRate(name, description, ccy, dayCount, calendar,
        roll, observationFrequency, publicationLag);
    }

#endregion
  }

  /// <summary>
  /// Forward yield index
  /// </summary>
  [Serializable]
  public class ForwardYieldIndex : ReferenceIndex
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ForwardYieldIndex));

#region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="indexTenor">Index tenor</param>
    /// <param name="indexFrequency">Underlying bond coupon payment frequency</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="settlementDays">Settlement days</param>
    public ForwardYieldIndex(string indexName, Tenor indexTenor, Frequency indexFrequency, Currency ccy,
                             DayCount dayCount, Calendar calendar, BDConvention roll, int settlementDays)
      : base(FindTreasuryReferenceRate(indexName, indexName, ccy, indexFrequency, dayCount,
        calendar, roll, settlementDays, new [] { indexTenor }, indexTenor), indexTenor)
    { }

    /// <summary>
    /// Constructor from <see cref="TreasuryReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">Treasury Reference Rate</param>
    /// <param name="tenor">Tenor</param>
    public ForwardYieldIndex(TreasuryReferenceRate referenceRate, Tenor tenor)
      : base(referenceRate, tenor)
    { }

#endregion

#region Methods

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      return new ForwardYieldIndex(TreasuryReferenceRate.Clone() as TreasuryReferenceRate, IndexTenor)
        { HistoricalObservations = HistoricalObservations };
    }

    /// <summary>
    /// Find or construct TreasuryReferenceRate
    /// </summary>
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
    private static TreasuryReferenceRate FindTreasuryReferenceRate(string name, string description, Currency ccy, Frequency frequency, DayCount dayCount,
      Calendar calendar, BDConvention roll, int daysToSpot, Tenor[] validTenors, Tenor defaultTenor)
    {
#if DEBUG
      var trr = ReferenceRates.ReferenceRate.GetValueWhere<TreasuryReferenceRate>(
        rr => rr.Currency == ccy && rr.Frequency == frequency && rr.DayCount == dayCount &&
        rr.Calendar == calendar && rr.Roll == roll && rr.DaysToSpot == daysToSpot
      ).FirstOrDefault();
      logger.Debug($"Creating temp TreasuryReferenceRate {name}");
      logger.Debug(trr != null ? $"Matched built in terms {trr.Key}" : $"Does not match any built in terms");
#endif
      return new TreasuryReferenceRate(name, description, ccy, frequency, dayCount,
        calendar, roll, daysToSpot, validTenors, defaultTenor);
    }

#endregion

#region Properties

    /// <summary>
    /// Treasury Reference Rate
    /// </summary>
    public TreasuryReferenceRate TreasuryReferenceRate => ReferenceRate as TreasuryReferenceRate;

#region Reference Rate Properties

    /// <summary>
    /// Business day convention
    /// </summary>
    public override BDConvention Roll => TreasuryReferenceRate.Roll;

    /// <summary>
    /// Daycount used for determination of the resets
    /// </summary>
    public override DayCount DayCount => TreasuryReferenceRate.DayCount;

    /// <summary>
    /// Settlement days
    /// </summary>
    public override int SettlementDays => TreasuryReferenceRate.DaysToSpot;

#endregion Reference Rate Properties

    /// <summary>
    /// Index frequency, either weekly averaging or monthly averaging
    /// </summary>
    public Frequency IndexFrequency => TreasuryReferenceRate.Frequency;

    /// <summary>
    /// Admissible projection types
    /// </summary>
    public override ProjectionType[] ProjectionTypes => new[] {ProjectionType.ParYield};

#endregion
  }

  /// <summary>
  /// Commodity Price Index
  /// </summary>
  [Serializable]
  public class CommodityPriceIndex : ReferenceIndex
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CommodityPriceIndex));

#region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="settleDays">Settlement days</param>
    /// <param name="observationFrequency">Price index observation frequency</param>
    public CommodityPriceIndex(string indexName, Currency ccy, DayCount dayCount, Calendar calendar, BDConvention roll, int settleDays,
      Frequency observationFrequency)
      : base(FindCommodityReferenceRate(indexName, indexName, ccy, calendar, observationFrequency), Tenor.Empty)
    {
      DayCount = dayCount;
      Roll = roll;
      SettlementDays = settleDays;
    }

    /// <summary>
    /// Constructor from <see cref="CommodityReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">Treasury Reference Rate</param>
    public CommodityPriceIndex(CommodityReferenceRate referenceRate)
      : base(referenceRate, Tenor.Empty)
    {
      DayCount = DayCount.Actual360;
      Roll = BDConvention.Following;
      SettlementDays = 0;
    }

#endregion

#region Methods

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      return new CommodityPriceIndex(CommodityReferenceRate.Clone() as CommodityReferenceRate)
        { HistoricalObservations = HistoricalObservations };
    }

    /// <summary>
    /// Find or construct CommodityReferenceRate
    /// </summary>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="publicationFreq">Frequency of publication of the fixing</param>
    private static CommodityReferenceRate FindCommodityReferenceRate(string name, string description, Currency ccy,
      Calendar calendar, Frequency publicationFreq)
    {
#if DEBUG
      var crr = ReferenceRates.ReferenceRate.GetValueWhere<CommodityReferenceRate>(
        rr => rr.Currency == ccy && rr.Calendar == calendar && rr.PublicationFrequency == publicationFreq
      ).FirstOrDefault();
      logger.Debug($"Creating temp CommodityReferenceRate {name}");
      logger.Debug(crr != null ? $"Matched built in terms {crr.Key}" : $"Does not match any built in terms");
#endif
      return new CommodityReferenceRate(name, description, ccy, calendar, publicationFreq);
    }

#endregion

#region Properties

    /// <summary>
    /// Daycount used for determination of the resets
    /// </summary>
    public override DayCount DayCount { get; }

    /// <summary>
    /// Business day convention
    /// </summary>
    public override BDConvention Roll { get; }

    /// <summary>
    /// Settlement days
    /// </summary>
    public override int SettlementDays { get; }

    /// <summary>
    /// Commodity Reference Rate
    /// </summary>
    CommodityReferenceRate CommodityReferenceRate => ReferenceRate as CommodityReferenceRate;

    /// <summary>
    /// Admissible projection types
    /// </summary>
    public override ProjectionType[] ProjectionTypes => new [] { ProjectionType.CommodityPrice, ProjectionType.AverageCommodityPrice };

#endregion
  }

  /// <summary>
  /// Equity Price Index
  /// </summary>
  [Serializable]
  public class EquityPriceIndex : ReferenceIndex
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(EquityPriceIndex));

#region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="dayCount">Daycount convention used for reset computation</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="settleDays">Settlement days</param>
    public EquityPriceIndex(string indexName, Currency ccy, DayCount dayCount, Calendar calendar, BDConvention roll, int settleDays)
      : base(FindEquityReferenceRate(indexName, indexName, ccy, calendar, settleDays), Tenor.Empty)
    {
      DayCount = dayCount;
      Roll = roll;
    }

    /// <summary>
    /// Constructor from <see cref="EquityReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">Equity Reference Rate</param>
    public EquityPriceIndex(EquityReferenceRate referenceRate)
      : base(referenceRate, Tenor.Empty)
    {
      DayCount = DayCount.None;
      Roll = BDConvention.Following;
    }

#endregion

#region Methods

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      return new EquityPriceIndex(EquityReferenceRate.Clone() as EquityReferenceRate)
        {HistoricalObservations = HistoricalObservations};
    }

    /// <summary>
    /// Find or construct EquityReferenceRate
    /// </summary>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="calendar">Underlying bond calendar</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value (settlement) date</param>
    private static EquityReferenceRate FindEquityReferenceRate(string name, string description, Currency ccy, Calendar calendar, int daysToSpot)
    {
#if DEBUG
      var err = ReferenceRates.ReferenceRate.GetValueWhere<EquityReferenceRate>(
        rr => rr.Currency == ccy && rr.Calendar == calendar && rr.DaysToSpot == daysToSpot
      ).FirstOrDefault();
      logger.Debug($"Creating temp EquityReferenceRate {name}");
      logger.Debug(err != null ? $"Matched built in terms {err.Key}" : $"Does not match any built in terms");
#endif
      return new EquityReferenceRate(name, description, ccy, calendar, daysToSpot);
    }

#endregion

#region Properties

    /// <summary>
    /// Equity Reference Rate
    /// </summary>
    public EquityReferenceRate EquityReferenceRate => ReferenceRate as EquityReferenceRate;

#region Reference Rate Properties

    /// <summary>
    /// Business day convention
    /// </summary>
    public override BDConvention Roll { get; }

    /// <summary>
    /// Daycount used for determination of the resets
    /// </summary>
    public override DayCount DayCount { get; }

    /// <summary>
    /// Settlement days
    /// </summary>
    public override int SettlementDays => EquityReferenceRate.DaysToSpot;

#endregion Reference Rate Properties

    /// <summary>
    /// Admissible projection types
    /// </summary>
    public override ProjectionType[] ProjectionTypes => new[] { ProjectionType.EquityPrice };

#endregion
  }

  /// <summary>
  /// FX Price Index
  /// </summary>
  [Serializable]
  public class FxRateIndex : ReferenceIndex
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(FxRateIndex));

#region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="forCcy">Foreign Currency</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="quoteCalendar">Quote Calendar</param>
    /// <param name="settleDays">Settlement days</param>
    public FxRateIndex(string indexName, Currency forCcy, Currency ccy, Calendar calendar, Calendar quoteCalendar, int settleDays)
      : base(FindFxReferenceRate(indexName, indexName, forCcy, ccy, quoteCalendar, calendar, settleDays, BDConvention.None), Tenor.Empty)
    { }

    /// <summary>
    /// Constructor from <see cref="FxReferenceRate"/>
    /// </summary>
    /// <param name="referenceRate">Fx Reference Rate</param>
    public FxRateIndex(FxReferenceRate referenceRate)
      : base(referenceRate, Tenor.Empty)
    { }

#endregion

#region Methods

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      return new FxRateIndex(FxReferenceRate.Clone() as FxReferenceRate)
        {HistoricalObservations = HistoricalObservations};
    }

    /// <summary>
    /// Find or construct FxReferenceRate
    /// </summary>
    /// <param name="name">Name of Reference Rate</param>
    /// <param name="description">Description</param>
    /// <param name="foreignCcy">Currency of denomination of the index</param>
    /// <param name="domesticCcy">Currency of denomination of the index</param>
    /// <param name="baseCalendar">Base Calendar</param>
    /// <param name="quoteCalendar">Quote Calendar</param>
    /// <param name="daysToSpot">Number of business days from as-of date to value (settlement) date</param>
    /// <param name="rollConvention">Date Roll convention</param>
    private static FxReferenceRate FindFxReferenceRate(string name, string description, Currency foreignCcy, Currency domesticCcy, Calendar quoteCalendar,
      Calendar baseCalendar, int daysToSpot, BDConvention rollConvention)
    {
#if DEBUG
      var fxrr = ReferenceRates.ReferenceRate.GetValueWhere<FxReferenceRate>(
        rr => rr.ForeignCurrency == foreignCcy && rr.Currency == domesticCcy && rr.QuoteCalendar == quoteCalendar &&
        rr.Calendar == baseCalendar && rr.DaysToSpot == daysToSpot && rr.RollConvention == rollConvention
      ).FirstOrDefault();
      logger.Debug($"Creating temp FxReferenceRate {name}");
      logger.Debug(fxrr != null ? $"Matched built in terms {fxrr.Key}" : $"Does not match any built in terms");
#endif
      return new FxReferenceRate(name, description, foreignCcy, domesticCcy, quoteCalendar,
        baseCalendar, daysToSpot, rollConvention);
    }

#endregion

#region Properties

    /// <summary>
    /// Fx Reference Rate
    /// </summary>
    public FxReferenceRate FxReferenceRate => ReferenceRate as FxReferenceRate;

#region Reference Rate Properties

    /// <summary>
    /// Business day convention
    /// </summary>
    public override BDConvention Roll => FxReferenceRate.RollConvention;

    /// <summary>
    /// Settlement days
    /// </summary>
    public override int SettlementDays => FxReferenceRate.DaysToSpot;

    /// <summary>
    ///   Currency of foreign index
    /// </summary>
    public Currency ForeignCurrency => FxReferenceRate.ForeignCurrency;

    /// <summary>
    ///   Quote Calendar
    /// </summary>
    public Calendar QuoteCalendar => FxReferenceRate.QuoteCalendar;

    /// <summary>
    ///   Spot settlement delay
    /// </summary>
    public int DaysToSpot => FxReferenceRate.DaysToSpot;

#endregion Reference Rate Properties

#endregion

    /// <summary>
    /// Admissible projection types
    /// </summary>
    public override ProjectionType[] ProjectionTypes => new[] { ProjectionType.FxRate };

#region Properties

#endregion
  }

  /// <summary>
  /// Extension methods for ReferenceIndex
  /// </summary>
  public static class ReferenceIndexUtils
  {
    /// <summary>
    /// Is this ReferenceIndex same as another ReferenceIndex, or does it have the same name?
    /// </summary>
    /// <param name="referenceIndex"></param>
    /// <param name="otherIndex"></param>
    /// <returns></returns>
    public static bool IsEqual(this ReferenceIndex referenceIndex, ReferenceIndex otherIndex)
    {
      if (referenceIndex == null || otherIndex == null)
        return false;
      return referenceIndex == otherIndex || (referenceIndex.IndexName == otherIndex.IndexName
        && referenceIndex.IndexTenor == otherIndex.IndexTenor);
    }

    /// <summary>
    /// Is this ReferenceIndex equal (in AreEqual sense) to any of the provided indices?
    /// </summary>
    /// <param name="index"></param>
    /// <param name="indices"></param>
    /// <returns></returns>
    public static bool IsEqualToAnyOf(this ReferenceIndex index, IEnumerable<ReferenceIndex> indices)
    {
      if (indices == null) return index == null;
      return indices.Any(i => IsEqual(i, index));
    }
  }
}