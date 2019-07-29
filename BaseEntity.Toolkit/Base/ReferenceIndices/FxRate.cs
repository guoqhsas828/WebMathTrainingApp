/*
 *  -2012. All rights reserved.
 */
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base.ReferenceIndices
{
  /// <summary>
  /// Exchange rate class
  /// </summary>
  [Serializable]
  public class FxRate : BaseEntityObject, ISpot
  {
    #region Constructor

    /// <summary>
    /// Constructor of exchange rate from currency fromCcy to currency toCcy, i.e. rate = fromCcy/toCcy
    /// </summary>
    /// <param name="asOf">AsOf (trade/pricing/horizon) date</param>
    /// <param name="spot">Spot date</param>
    /// <param name="fromCcy">The currency to convert from (foreign/base/destination/from/pay)</param>
    /// <param name="toCcy">The currency to convert to (domestic/quoting/original/to/receive)</param>
    /// <param name="rate">Spot exchange rate baseCcy/quoteCcy at asOf date</param>
    public FxRate(Dt asOf, Dt spot, Currency fromCcy, Currency toCcy, double rate)
    {
      AsOf = asOf;
      Spot = spot;
      FromCcy = fromCcy;
      ToCcy = toCcy;
      Rate = rate;
    }

    /// <summary>
    /// Constructor of exchange rate from currency baseCcy to currency quoteCcy, i.e. rate = baseCcy/quoteCcy
    /// </summary>
    /// <param name="asOf">AsOf/Trade/Pricing/Horizon date</param>
    /// <param name="settleDays">Settlement days to spot date</param>
    /// <param name="fromCcy">The currency to convert from (foreign/base/destination)</param>
    /// <param name="toCcy">The currency to convert to (domestic/quoting/original)</param>
    /// <param name="rate">Spot exchange rate baseCcy/quoteCcy at asOf date</param>
    /// <param name="fromCcyCalendar">Calendar used for the currency of origin</param>
    /// <param name="toCcyCalendar">Calendar used for the currency of destination</param>
    public FxRate(Dt asOf, int settleDays, Currency fromCcy, Currency toCcy, double rate, Calendar fromCcyCalendar, Calendar toCcyCalendar)
    {
      AsOf = asOf;
      SettleDays = settleDays;
      FromCcyCalendar = fromCcyCalendar;
      ToCcyCalendar = toCcyCalendar;
      FromCcy = fromCcy;
      ToCcy = toCcy;
      Rate = rate;
      Spot = FxUtil.FxSpotDate(AsOf, SettleDays, ToCcyCalendar, FromCcyCalendar);
    }

    /// <summary>
    /// Crate the inverse FxRate
    /// </summary>
    /// <returns>New FxRate matching inverse of this FxRate</returns>
    public FxRate InverseFxRate()
    {
      // allow for Spot having been moved out of sync with AsOf, e.g. during a simulation process
      return new FxRate(AsOf, SettleDays, ToCcy, FromCcy, 1.0 / Rate, ToCcyCalendar, FromCcyCalendar) { Spot = Spot };
    }

    #endregion

    #region Methods

    /// <summary>
    /// Test equality
    /// </summary>
    /// <param name="obj">Other fx rate</param>
    /// <returns>True if this == obj</returns>
    public override bool Equals(object obj)
    {
      var other = obj as FxRate;
      if (other == null)
        return false;
      return (FromCcy == other.FromCcy && ToCcy == other.ToCcy);
    }

    /// <summary>
    /// Hash code
    /// </summary>
    /// <returns>Hash code</returns>
    public override int GetHashCode()
    {
      return FromCcy.GetHashCode() ^ ToCcy.GetHashCode();
    }

    /// <summary>
    /// String representation
    /// </summary>
    /// <returns>String</returns>
    public override string ToString()
    {
      return String.Format("{0}/{1} {2} {3}", FromCcy, ToCcy, Spot, Rate);
    }

    /// <summary>
    /// Set spot rate
    /// </summary>
    /// <param name="fromCcy">The currency to convert from (foreign/base/destination)</param>
    /// <param name="toCcy">The currency to convert to (domestic/quoting/original)</param>
    /// <param name="value">Spot rate level</param>
    public void SetRate(Currency fromCcy, Currency toCcy, double value)
    {
      Update(fromCcy, toCcy, value);
    }

    internal virtual void Update(
      Currency fromCcy, Currency toCcy, double rate)
    {
      if ((fromCcy == FromCcy) && (toCcy == ToCcy))
      {
        Rate = rate;
        return;
      }
      if ((fromCcy == ToCcy) && (toCcy == FromCcy))
      {
        Rate = 1.0 / rate;
        return;
      }
      throw new ArgumentException("Invalid currency");
    }

    internal void Update(Dt newAsOf,
      Currency fromCcy, Currency toCcy, double value)
    {
      if (!newAsOf.IsEmpty())
      {
#if FIX_SPOT_DAYS
        Spot = FxSpotDate(newAsOf);
#else
        Spot = newAsOf;
#endif
      }
      Update(fromCcy, toCcy, value);
    }

    /// <summary>
    /// Get spot rate 
    /// </summary>
    /// <param name="fromCcy">From currency</param>
    /// <param name="toCcy">To currency</param>
    /// <returns>Spot rate</returns>
    public double GetRate(Currency fromCcy, Currency toCcy)
    {
      if (fromCcy == FromCcy && toCcy == ToCcy)
        return Rate;
      if (fromCcy == ToCcy && toCcy == FromCcy)
        return 1.0 / Rate;
      throw new ArgumentException("Invalid currency");
    }

    /// <summary>
    /// Determine an fx spot date for a given date
    /// </summary>
    /// <param name="asOf">Date to test from</param>
    /// <returns>Fx Spot date</returns>
    public Dt FxSpotDate(Dt asOf)
    {
      return FxUtil.FxSpotDate(asOf, SettleDays, FromCcyCalendar, ToCcyCalendar);
    }

    /// <summary>
    /// Find next valid settlement considering both fx calendars
    /// </summary>
    /// <param name="asOf">Date to test from</param>
    /// <returns>Next valid settlement</returns>
    public Dt Roll(Dt asOf)
    {
      return FxUtil.FxSpotDate(asOf, 0, FromCcyCalendar, ToCcyCalendar);
    }

    /// <summary>
    /// Is currency order inverse or not
    /// </summary>
    /// <param name="fromCcy">The currency to convert from</param>
    /// <param name="toCcy">The currency to convert to</param>
    /// <returns>If currency order is inverse or not</returns>
    public bool IsInverse(Currency fromCcy, Currency toCcy)
    {
      if (fromCcy == FromCcy && toCcy == ToCcy)
        return false;
      else if (fromCcy == ToCcy && toCcy == FromCcy)
        return true;
      else if (fromCcy == Currency.None && toCcy == Currency.None)
        return false;
      else
        throw new ToolkitException(String.Format("From/To currencies ({0}/{1}) not match quote ({2}/{3})", fromCcy,
                                                 toCcy, FromCcy, ToCcy));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Denomination currency
    /// </summary>
    public Currency Ccy
    {
      get { return ToCcy; }
    }

    /// <summary>
    /// Fx name
    /// </summary>
    public string Name
    {
      get { return String.IsNullOrEmpty(_name) ? String.Concat(FromCcy, ToCcy) : _name; }
      set { _name = value; }
    }

    /// <summary>
    /// Accessor for base currency
    /// </summary>
    public Currency FromCcy { get; private set; }

    /// <summary>
    /// Accessor for price currency
    /// </summary>
    public Currency ToCcy { get; private set; }

    /// <summary>
    /// Spot fx rate
    /// </summary>
    public double Rate { get; set; }

    /// <summary>
    /// AsOf date of FxRate
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// Set Spot Date 
    /// </summary>
    /// <remarks>Setter will *not* update SettleDays automatically</remarks>
    public Dt Spot { get; set; }

    /// <summary>
    /// Fx level at spot date
    /// </summary>
    public double Value
    {
      get { return Rate; }
      set { Rate = value; }
    }

    ///<summary>
    /// Business day calendar for the From Currency
    ///</summary>
    public Calendar FromCcyCalendar { get; set; }

    ///<summary>
    /// Business day calendar for the To Currency
    ///</summary>
    internal Calendar ToCcyCalendar { get; set; }

    ///<summary>
    /// Number of spot days in this market
    ///</summary>
    public int SettleDays { get; set; }

    #endregion

    #region Data

    string _name;

    #endregion
  }
}
