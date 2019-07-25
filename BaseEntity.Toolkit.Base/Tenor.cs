/*
 * Tenor.cs
 * Copyright (c)    2002-2014. All rights reserved.
 */

using System;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Representation of a tenor or length of time.
  /// </summary>
  /// <remarks>
  ///   Each tenor has an associated number (of time
  ///   units) and units of time (days/months/years).
  /// </remarks>
  [Serializable]
  public struct Tenor : IFormattable, IComparable, IComparable<Tenor>, IXmlSerializable
  {
    #region Constructors

    /// <summary>
    ///   Constructor from number of time units.
    /// </summary>
    /// <param name="n">Number of time units</param>
    /// <param name="u">Units of time</param>
    public Tenor(int n, TimeUnit u)
    {
      if( (n < 0) || (n != 0 && u == TimeUnit.None) )
        throw new ArgumentException( "Invalid step or TimeUnit for tenor" );

      n_ = n;
      u_ = u;
      return;
    }

    /// <summary>
    ///   Construct from frequency per year.
    /// </summary>
    /// <param name="freq">Number of tenors per year</param>
    public Tenor(Frequency freq)
    {
      switch( freq )
      {
      case Frequency.Continuous:
      case Frequency.None:
        n_ = 0;
        u_ = TimeUnit.None;
        break;
      case Frequency.BiWeekly:
        n_ = 2;
        u_ = TimeUnit.Weeks;
        break;
      case Frequency.Weekly:
        n_ = 1;
        u_ = TimeUnit.Weeks;
        break;
      case Frequency.TwentyEightDays:
        n_ = 28;
        u_ = TimeUnit.Days;
        break;
      case Frequency.Daily:
        n_ = 1;
        u_ = TimeUnit.Days;
        break;
      default:
        // fraction of year
        if( (12 % (int)freq ) != 0 )
          throw new ArgumentOutOfRangeException( "freq", @"Invalid frequency" );
        n_ = 12 / (int)freq;
        u_ = TimeUnit.Months;
        break;
      }
    }

    /// <summary>
    ///   Constructor from string.
    /// </summary>
    /// <param name="s">string representation of tenor</param>
    /// <remarks>
    ///   Valid string representations of a tenor are:
    ///   <list type="bullet">
    ///     <item><description>[N] [y|yr|year|years]         N years</description></item>
    ///     <item><description>[N] [m|mn|month|months]       N months</description></item>
    ///     <item><description>[N] [w|wk|week|weeks]         N weeks</description></item>
    ///     <item><description>[N] [d|day|days]              N days</description></item>
    ///     <item><description>[N] [a|ann|annual]            N year</description></item>
    ///     <item><description>[N] [s|sa|semiann|semiannual] N * 6 month</description></item>
    ///     <item><description>[N] [q|qtr|quarterly]         N * 4 month</description></item>
    ///   </list>
    /// </remarks>
    [Obsolete("Replaced by Parse")]
    public Tenor( string s )
    {
      this = Tenor.Parse(s);
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Number of time units
    /// </summary>
    public int N
    {
      get { return n_; }
    }

    /// <summary>
    ///   Units of time
    /// </summary>
    public TimeUnit Units
    {
      get { return u_; }
    }

    /// <summary>
    ///   True if tenor empty
    /// </summary>
    public bool IsEmpty
    {
      get { return (n_ == 0 && u_ == TimeUnit.None); }
    }

    /// <summary>
    ///   Estimate number of days for this tenor based on 30 day months.
    /// </summary>
    public int Days
    {
      get
      {
        switch( u_ )
        {
          case TimeUnit.Days:
            return n_;
          case TimeUnit.Weeks:
            return n_*7;
          case TimeUnit.Months:
            return n_*30;
          case TimeUnit.Years:
            return n_*30*12;
          default:
            throw new ArgumentException( "Invalid format" );
        }
      }
    }

    /// <summary>
    ///   Estimate number of years for this tenor based on 30 day months.
    /// </summary>
    public double Years
    {
      get
      {
        switch (u_) {
          case TimeUnit.Days:
            return n_ / (30.0*12.0);
          case TimeUnit.Weeks:
            return n_ / (52.0);
          case TimeUnit.Months:
            return n_ / 12.0;
          case TimeUnit.Years:
            return n_;
          default:
            throw new ArgumentException("Invalid format");
        }
      }
    }

    /// <summary>
    ///   Estimate number of months for this tenor based on 30 day months.
    /// </summary>
    public double Months
    {
      get
      {
        switch (u_)
        {
          case TimeUnit.Days:
            return n_ / 30.0;
          case TimeUnit.Weeks:
            return (n_*7) / 30.0  ;
          case TimeUnit.Months:
            return n_;
          case TimeUnit.Years:
            return n_*12;
          default:
            throw new ArgumentException("Invalid format");
        }
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Part of operator == overload.
    /// </summary>
    /// <param name="t1">First tenor</param>
    /// <param name="t2">Second tenor</param>
    /// <returns>True if tenors equal</returns>
    public static bool operator==(Tenor t1, Tenor t2)
    {
      return t1.Equals(t2);
    }

    /// <summary>
    /// Part of operator == overload.
    /// </summary>
    /// <param name="t1">First tenor</param>
    /// <param name="t2">Second tenor</param>
    /// <returns>True if tenors NOT equal</returns>
    public static bool operator!=(Tenor t1, Tenor t2)
    {
      return !(t1 == t2);
    }

    /// <summary>
    /// Part of operator == overload.
    /// </summary>
    /// <param name="obj">Tenor to compare</param>
    /// <returns>True if tenors equal</returns>
    public override bool Equals(object obj)
    {
      if (!(obj is Tenor))
        return false;
      var t = (Tenor)obj;
      if (Units == TimeUnit.Weeks && t.Units == TimeUnit.Days)
        return (N * 7 == t.N);
      else if (Units == TimeUnit.Days && t.Units == TimeUnit.Weeks)
        return (N == t.N * 7);
      else if (Units == TimeUnit.Years && t.Units == TimeUnit.Months)
        return (N * 12 == t.N);
      else if (Units == TimeUnit.Months && t.Units == TimeUnit.Years)
        return (N == t.N * 12);
      else
        return (Units == t.Units && N == t.N);
    }

    /// <summary>
    /// Part of operator == overload.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return Units.GetHashCode() ^ N;
    }

    /// <summary>
    ///   Converts the string representation of a tenor to an equivalent Tenor instance.
    /// </summary>
    /// <param name="strValue"></param>
    /// <returns>Tenor</returns>
    public static Tenor Parse(string strValue)
    {
      Tenor tenor;
      Parse(strValue, out tenor, true);
      return tenor;
    }

    /// <summary>
    /// Utility method to decipher a tenor object out of the string input
    /// </summary>
    /// <param name="strValue">String input</param>
    /// <param name="tenor">Tenor object of parsing result</param>
    /// <returns>bool</returns>
    public static bool TryParse(string strValue, out Tenor tenor)
    {
      return Parse(strValue, out tenor, false);
    }

    ///<summary>
    /// Utility method to decipher the FRA term A * B
    ///</summary>
    ///<param name="strValue">FRA composite term in the form of A * B</param>
    ///<param name="settleTenor">Tenor in month unit from spot date to settlement date</param>
    ///<param name="maturityTenor">Tenor in month unit from settlement date to maturity date</param>
    ///<returns>True if the input is in valid composite tenor format, false otherwise</returns>
    public static bool TryParseComposite(string strValue, out Tenor settleTenor, out Tenor maturityTenor)
    {
      strValue = strValue.ToLower();
      if (!StringUtil.HasValue(strValue) || (strValue.IndexOf('*') <= 0 && strValue.IndexOf('x') <= 0))
      {
        settleTenor = new Tenor();
        maturityTenor = new Tenor();
        return false;
      }

      var splitter = '*';
      var attacher = "m";
      if (strValue.IndexOf('x') > 0)
        splitter = 'x';
      if (strValue.IndexOf('m') > 0)
        attacher = "";
      var components = strValue.Split(splitter);
      return (TryParse(components[0]+attacher, out settleTenor) & TryParse(components[1]+attacher, out maturityTenor));
    }

    ///<summary>
    /// Utility method to parse a forward rate code of the form A x B, where A is the tenor
    /// to the start of the forward rate, and B is the tenor to the end of the forward
    /// rate (thus tenor B should be greater than A)
    ///</summary>
    ///<param name="strValue">Forward rate composite term in the form of A x B</param>
    ///<param name="startTenor">Tenor to the start of the forward rate. 
    /// Note: 0 will be a valid format for the start tenor, as well as 0D, 0W, 0M, 0Y</param>
    ///<param name="endTenor">Tenor to the end of the forward rate</param>
    ///<returns>True if the input is in valid composite forward rate tenor format, false otherwise</returns>
    ///<remarks>This method is similar to TryParseComposite(), but the tenor TimeUnits are explicit rather than the implicit Months of that method.
    /// This version is used predominantly for CCR applications.</remarks>
    public static bool TryParseForwardRateStartEndTenor(string strValue, out Tenor startTenor, out Tenor endTenor)
    {
      const char sep = 'x';
      startTenor = new Tenor();
      endTenor = new Tenor();
      if (String.IsNullOrEmpty(strValue)) return false;
      strValue = strValue.ToLower();
      string[] components = strValue.Split(sep);
      if (components.Length != 2) return false;
      components[0] = components[0].Trim();
      components[1] = components[1].Trim();
      if (String.IsNullOrEmpty(components[0]) || String.IsNullOrEmpty(components[1])) return false;
      bool res = true;
      if (components[0] == "0") // 0 will be a valid format for the start tenor, as well as 0D, 0W, 0M, 0Y
        startTenor = new Tenor(0, TimeUnit.Days);
      else
        res = Tenor.TryParse(components[0], out startTenor);
      if (res == false) return false; // Invalid start tenor format
      res = Tenor.TryParse(components[1], out endTenor);
      if (res == false) return false; // Invalid end tenor format
      int cmp = startTenor.CompareTo(endTenor);
      if (cmp >= 0) return false; // Start tenor must be < end tenor
      return true;
    }

    ///<summary>
    /// Utility method to format a Forward Rate header as A x B, where A is the tenor
    /// to the start of the forward rate, and B is the tenor to the end of the forward
    /// rate (thus tenor B should be greater than A). This is the inverse of the TryParseForwardRateStartEndTenor() method.
    ///</summary>
    ///<param name="startTenor">Tenor to the start of the forward rate.</param>
    ///<param name="endTenor">Tenor to the end of the forward rate</param>
    ///<returns>The formatted string as start_tenor x end_tenor; will return null if start tenor is greater or equal to the end tenor</returns>
    public static string FormatForwardRateStartEndTenor(Tenor startTenor, Tenor endTenor)
    {
      if (startTenor == Tenor.Empty) startTenor = new Tenor(0, TimeUnit.Days);
      if (endTenor == Tenor.Empty) endTenor = new Tenor(0, TimeUnit.Days);
      int cmp = startTenor.CompareTo(endTenor);
      if (cmp >= 0) return null; // Start tenor must be < end tenor
      string res = String.Format("{0} x {1}", startTenor.ToString("S", null), endTenor.ToString("S", null));
      return res;
    }

    /// <summary>
    /// Internal parse method
    /// </summary>
    private static bool Parse(string strValue, out Tenor tenor, bool throwOnParseError)
    {
      if ( String.IsNullOrWhiteSpace(strValue))
      {
        tenor = Tenor.Empty;
        return false;
      }

      if(String.Equals(strValue.Trim(), "0"))
      {
        tenor = Tenor.Empty;
        return true;
      }

      // Fast but not guaranteed correct parser
      var match = regex.Match(strValue);
      if( !match.Success )
      {
        if (throwOnParseError)
          throw new ArgumentException( String.Format("Unrecognised Tenor format [{0}]", strValue));
        tenor = Tenor.Empty;
        return false;
      }

      TimeUnit u;
      int n = int.Parse( match.Groups[1].Value );
      char c = char.Parse( match.Groups[2].Value );
      switch( c )
      {
      case 'y': case 'Y':
        u = TimeUnit.Years;
        break;
      case 'm': case 'M':
        u = TimeUnit.Months;
        break;
      case 'w': case 'W':
        u = TimeUnit.Weeks;
        break;
      case 'd': case 'D':
        u = TimeUnit.Days;
        break;
      case 'a': case 'A':
        u = TimeUnit.Years;
        break;
      case 's': case 'S':
        n *= 6;
        u = TimeUnit.Months;
        break;
      case 'q': case 'Q':
        n *= 3;
        u = TimeUnit.Months;
        break;
      case 'n': case 'N':
        u = TimeUnit.None;
        break;
      default:
        if (throwOnParseError)
          throw new ArgumentException( String.Format("Unrecognised TimeUnit [{0}]", c));

        tenor = new Tenor();
        return false;
      }

      tenor = new Tenor(n, u);
      return true;
    }

    #endregion

    #region IFormattable

    /// <summary>
    /// Convert to string representation
    /// </summary>
    /// <param name="format">Format specifier</param>
    /// <param name="formatProvider">Format provider (ignored)</param>
    /// <returns>String representation of tenor</returns>
    public string ToString(string format, IFormatProvider formatProvider)
    {
      string[] abbrevs = (format == "S") ? UpperCaseAbbrevs : (format == "s") ? LowerCaseAbbrevs : null;

      return (abbrevs != null) ?
        String.Format( "{0}{1}", n_, abbrevs[ (int)u_ ] ) :
        String.Format( "{0} {1}", n_, u_ );
    }

    /// <summary>
    ///  Default string formatting
    /// </summary>
    public override string ToString()
    {
      return ToString(null, null);
    }

    #endregion IFormattable

    #region IComparable

    /// <summary>
    /// IComparable.CompareTo implementation.
    /// </summary>
    public int CompareTo(object obj)
    {
      if (obj is Tenor)
      {
        var other = (Tenor)obj;
        //need to handle None case
        if (u_ == TimeUnit.None || other.u_ == TimeUnit.None)
          return u_.CompareTo(other.u_); //enum compare works
        if (u_ == other.u_)
          return n_.CompareTo(other.n_);
        return (Days - other.Days);
      }
      else
        throw new ArgumentException("object is not a Tenor");
    }

    #endregion IComparable

    #region Data

    private int n_;
    private TimeUnit u_;
    static private Regex regex = new Regex(@"^\s*([0-9]+)\s{0,1}([YyMmWwDdAaSsQqNn]).*$");

    #endregion Data

    #region Static Data

    /// <summary>Empty tenor</summary>
    public static readonly Tenor Empty = new Tenor(0, TimeUnit.None);
    /// <summary>One day</summary>
    public static Tenor OneDay = new Tenor(1, TimeUnit.Days);
    /// <summary>One week</summary>
    public static Tenor OneWeek = new Tenor(7, TimeUnit.Days);
    /// <summary>Two weeks</summary>
    public static Tenor TwoWeeks = new Tenor(14, TimeUnit.Days);
    /// <summary>Twentyeight days</summary>
    public static Tenor TwentyEightDays = new Tenor(28, TimeUnit.Days);
    /// <summary>One month</summary>
    public static Tenor OneMonth = new Tenor(1, TimeUnit.Months);
    /// <summary>Two months</summary>
    public static Tenor TwoMonths = new Tenor(2, TimeUnit.Months);
    /// <summary>Ninety days</summary>
    public static Tenor NinetyDays = new Tenor(90, TimeUnit.Days);
    /// <summary>Three months</summary>
    public static Tenor ThreeMonths = new Tenor(3, TimeUnit.Months);
    /// <summary>Four months</summary>
    public static Tenor FourMonths = new Tenor(4, TimeUnit.Months);
    /// <summary>Five months</summary>
    public static Tenor FiveMonths = new Tenor(5, TimeUnit.Months);
    /// <summary>Six months</summary>
    public static Tenor SixMonths = new Tenor(6, TimeUnit.Months);
    /// <summary>Seven months</summary>
    public static Tenor SevenMonths = new Tenor(7, TimeUnit.Months);
    /// <summary>Eight months</summary>
    public static Tenor EightMonths = new Tenor(8, TimeUnit.Months);
    /// <summary>Nine months</summary>
    public static Tenor NineMonths = new Tenor(9, TimeUnit.Months);
    /// <summary>Ten months</summary>
    public static Tenor TenMonths = new Tenor(10, TimeUnit.Months);
    /// <summary>Eleven months</summary>
    public static Tenor ElevenMonths = new Tenor(11, TimeUnit.Months);
    /// <summary>One Year</summary>
    public static Tenor OneYear = new Tenor(1, TimeUnit.Years);
    /// <summary>Two Years</summary>
    public static Tenor TwoYears = new Tenor(2, TimeUnit.Years);
    /// <summary>Three Years</summary>
    public static Tenor ThreeYears = new Tenor(3, TimeUnit.Years);
    /// <summary>Four Years</summary>
    public static Tenor FourYears = new Tenor(4, TimeUnit.Years);
    /// <summary>Five Years</summary>
    public static Tenor FiveYears = new Tenor(5, TimeUnit.Years);
    /// <summary>Six Years</summary>
    public static Tenor SixYears = new Tenor(6, TimeUnit.Years);
    /// <summary>Seven Years</summary>
    public static Tenor SevenYears = new Tenor(7, TimeUnit.Years);
    /// <summary>Eight Years</summary>
    public static Tenor EightYears = new Tenor(8, TimeUnit.Years);
    /// <summary>Nine Years</summary>
    public static Tenor NineYears = new Tenor(9, TimeUnit.Years);
    /// <summary>Ten Years</summary>
    public static Tenor TenYears = new Tenor(10, TimeUnit.Years);
    /// <summary>Fifteen Years</summary>
    public static Tenor FifteenYears = new Tenor(15, TimeUnit.Years);
    /// <summary>Twenty Years</summary>
    public static Tenor TwentyYears = new Tenor(20, TimeUnit.Years);
    /// <summary>Twenty Years</summary>
    public static Tenor ThirtyYears = new Tenor(30, TimeUnit.Years);

    private static string[] UpperCaseAbbrevs = new string[]{"", "D", "W", "M", "Y"};
    private static string[] LowerCaseAbbrevs = new string[]{"", "d", "w", "m", "y"};

    #endregion

    #region Util

    /// <summary>
    /// If the Tenor is based on month/week/day/year and less/equal that a year, convert it to a frequency, otherwise return none.
    /// </summary>
    /// <returns></returns>
    public Frequency ToFrequency()
    {
      if (Units == TimeUnit.Months)
      {
        switch (N)
        {
          case 1:
            return Frequency.Monthly;
          case 3:
            return Frequency.Quarterly;
          case 4:
            return Frequency.TriAnnual;
          case 6:
            return Frequency.SemiAnnual;
          case 12:
            return Frequency.Annual;
          default:
            break;
        }
      }
      else if (Units == TimeUnit.Weeks)
      {
        switch (N)
        {
          case 1:
            return Frequency.Weekly;
          case 2:
            return Frequency.BiWeekly;
          default:
            break;
        }
      }
      else if (Units == TimeUnit.Days)
      {
        switch (N)
        {
          case 1:
            return Frequency.Daily;
          case 28:
            return Frequency.TwentyEightDays;
          default:
            break;
        }
      }
      else if (Units == TimeUnit.Years && N == 1)
      {
        return Frequency.Annual;
      }
      return Frequency.None;
    }

    /// <summary>
    ///   Create an approximate tenor from the specified date interval.
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <returns>The approximate tenor.</returns>
    /// <remarks>
    ///   The result tenor depends on the number of days, <c>n</c>, between start and end dates.
    ///   <list type="table">
    ///     <item><term><c>n &lt; 6</c></term><description><c>n Days</c>;</description></item>
    ///     <item><term><c>n &lt; 30</c></term><description><c>m Weeks</c>, where <c>m</c> is <c>(n+3)/7</c> truncated to integer;
    ///       or <c>1 Months</c> if <c>m</c> equals <c>4</c>.</description></item>
    ///     <item><term><c>otherwise</c></term><description><c>m Months</c>, where <c>m</c> is closest number of months;
    ///       or <c>k Years</c> if <c>m</c> is exactly <c>12</c> times <c>k</c>.</description></item>
    ///   </list>
    /// </remarks>
    public static Tenor FromDateInterval(Dt start, Dt end)
    {
      return FromDays((int)(end - start));
    }

    /// <summary>
    ///   Create an approximate tenor from the specified number of days in the interval.
    /// </summary>
    /// <param name="days">The number of days in the interval.</param>
    /// <returns>The approximate tenor.</returns>
    /// <remarks>
    ///   The result tenor depends on the number of days, <c>n</c>, between start and end dates.
    ///   <list type="table">
    ///     <item><term><c>n &lt; 6</c></term><description><c>n Days</c>;</description></item>
    ///     <item><term><c>n &lt; 30</c></term><description><c>m Weeks</c>, where <c>m</c> is <c>(n+3)/7</c> truncated to integer;
    ///       or <c>1 Months</c> if <c>m</c> equals <c>4</c>.</description></item>
    ///     <item><term><c>otherwise</c></term><description><c>m Months</c>, where <c>m</c> is closest number of months;
    ///       or <c>k Years</c> if <c>m</c> is exactly <c>12</c> times <c>k</c>.</description></item>
    ///   </list>
    /// </remarks>
    public static Tenor FromDays(int days)
    {
      if (days < 6) return new Tenor(days, TimeUnit.Days);
      if (days < 30)
      {
        var weeks = (days + 3) / 7;
        return weeks == 4
          ? new Tenor(1, TimeUnit.Months)
          : new Tenor(weeks, TimeUnit.Weeks);
      }
      var years = (days + 6) / 365;
      var months = (days - years * 365 + 4) / 30;
      return months <= 0
        ? new Tenor(years, TimeUnit.Years)
        : new Tenor(years * 12 + months, TimeUnit.Months);
    }

    #endregion

    #region IComparable<Tenor> Members

    /// <summary>
    /// Compare to specified object
    /// </summary>
    /// <param name="other">Object to compare</param>
    /// <returns>Comparison result</returns>
    public int CompareTo(Tenor other)
    {
      //need to handle None case
      if (u_ == TimeUnit.None || other.u_ == TimeUnit.None)
        return u_.CompareTo(other.u_); //enum compare works
      if (u_ == other.u_)
        return n_.CompareTo(other.n_);
      return (Days - other.Days);
    }

    #endregion

    #region IXmlSerializer

    //we want this class to be serialized as below
    //<Tenor>3 Years</Tenor>
    /// <summary>
    /// Get schema
    /// </summary>
    XmlSchema IXmlSerializable.GetSchema()
    {
      return null;
    }

    /// <summary>
    /// Read Tenor from XML file
    /// </summary>
    void IXmlSerializable.ReadXml(XmlReader reader)
    {
      string name = reader.ReadString();
      if (string.IsNullOrEmpty(name))
      {
        n_ = 0;
        u_ = TimeUnit.None;
      }
      else
      {
        this = Parse(name);
      }
      reader.Read(); //this is a must to skip the end element
    }

    /// <summary>
    /// Write Tenor into XML file
    /// </summary>
    void IXmlSerializable.WriteXml(XmlWriter writer)
    {
      writer.WriteString(ToString());
    }

    #endregion
  } // class Tenor
}
