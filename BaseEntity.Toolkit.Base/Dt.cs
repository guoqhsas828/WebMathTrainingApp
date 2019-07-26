//
// Dt.cs
// Copyright (c)    2002-2014. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;
using JetBrains.Annotations;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Business date class
  /// </summary>
  /// <remarks>
  ///   <para>The current implentation of Dt represents Dates/times from 1900 to 2150 to the nearest 10 minutes.</para>
  ///   <para>The class is optimized for speed and efficiency with the date fitting into a 32 bits.</para>
  ///   <para>The date class is immutable. Any operations results in a new instance of a Dt without modifying the original.</para>
  ///   <para>See also:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.12x30.net/intro.html">Hollon, B. An Introduction to Calendars</a></description></item>
  ///   <item><description><u>Seidelmann, P. K. Explanatory Supplement to the Astronomical Almanac,
  ///                      Mill Valley, CA: University Science Books, 1992</u></description></item>
  ///   <item><description><u>Vardi, I. The Julian Calendar, Section 3.5.1 in Computational Recreations in Mathematica,
  ///                      Redwood City, CA: Addison-Wesley, p. 44, 1991</u></description></item>
  ///   <item><description><a href="http://www.emailman.com/leapday">Starr, A. Leap Day/Leap Year</a></description></item>
  ///   <item><description><a href="http://www.mystro.com/leap.htm">Strohsacker, J. February 29 Leap Day</a></description></item>
  ///   <item><description><a href="http://www.tondering.dk/claus/calendar.html">Frequently Asked Questions about Calendars</a></description></item>
  ///   </list>
  /// </remarks>
  /// <example>
  /// <para>The following sample demonstrates usage of common <see cref="Dt"/> methods.</para>
  /// <code language="C#">
  ///   // Get today's date
  ///   Dt today = Dt.Today();
  ///   Console.WriteLine( "Today's date is: {0}", today );
  ///
  ///   // Get first day of this month
  ///   Dt startOfMonth = new Dt( 1, today.Month, today.Year);
  ///   Console.WriteLine( "First day of this month is: {0}", startOfMonth );
  ///
  ///   // Get last day of this month
  ///   Dt endOfMonth = Dt.LastDay( today.Month, today.Year );
  ///   Console.WriteLine( "Last day of this month is: {0}", endOfMonth );
  ///   // Test if the last day of the month is a valid NY settlement date
  ///   Console.WriteLine( "The last day of the month is {0} a valid NY Bank settlement date",
  ///     endOfMonth.IsValidSettlement( Calendar.NYB ) ? " ": " NOT " );
  ///
  ///   // Get the number of NY Banking business days from today to the end of the month
  ///   int bdaysToEndOfMonth = Dt.BusinessDays( today, endOfMonth, Calendar.NYB );
  ///   Console.WriteLine( "There are {0} business days till the end of the month", bdaysToEndOfMonth );
  ///
  ///   // Count the number of actual days from today to the end of the month
  ///   int daysToEndOfMonth = Dt.Diff( today, endOfMonth );
  ///   Console.WriteLine( "There are {0} actual days till the end of the month", daysToEndOfMonth );
  ///
  ///   // Calculate the accrual from the start of the month to day using 30/360 Isda daycount convention
  ///   // Round result to six decimal places
  ///   DayCount dc = DayCount.Thirty360Isma;
  ///   double frac = Dt.Fraction( startOfMonth, endOfMonth, startOfMonth, today, dc );
  ///   Console.WriteLine( "Accrued within this month is {0:F6} using {1} daycount convention", frac, dc );
  ///
  ///   // Step monthly from today for three months.
  ///   Dt dt = Dt.Today();
  ///   Console.WriteLine( "Next three months are:" );
  ///   for( int i = 0; i &lt; 3; i++ )
  ///   {
  ///     dt = Dt.Add( dt, 1, TimeUnit.Months );
  ///     Console.WriteLine( "    {0}", dt );
  ///   }
  ///
  ///   // Example of creating an empty date
  ///   Dt emptyDt = Dt.Empty;
  ///   if( emptyDt.IsEmpty() &amp;&amp; emptyDt.IsValid() )
  ///     Console.WriteLine("An Empty date is still a valid date!");
  ///
  ///   // Example of date comparisons
  ///   if( today >= endOfMonth )
  ///     Console.WriteLine("Today is the end of the month!");
  ///
  ///   // Next IMM date
  ///   Dt immNext = Dt.ImmNext(today);
  ///   Console.WriteLine( "Next IMM date is {0}", immNext );
  ///
  ///   // Standard CDS Maturity
  ///   Dt cdsMaturity = Dt.CDSMaturity(today, "5 Year");
  ///   Console.WriteLine( "Standard 5Yr Corporate CDS Maturity is {0}", cdsMaturity );
  ///
  /// </code>
  /// </example>
  [Serializable]
  public struct Dt : IComparable, IComparable<Dt>, IXmlSerializable, IFormattable
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger( typeof(Dt) );

    #region Private Data and Methods

    private const int YearBase = 1900;            // Base century for year
    private const int MaxYear = YearBase + 250;  // We support years to 2150
    private const int YearCutover = 60;           // Century cutover for 2 digit year.
    private const int MaxTicksPerDay = 24 * 6;

    // Days in month
    //
    private static readonly uint[] Dim  = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};
    private static readonly uint[] DimL = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};
    private static uint DaysInMonth( int month, bool leap )
    {
      if (month < 1 || month > 12)
        throw new ArgumentOutOfRangeException(nameof(month), $@"Invalid month {month}");
      return( leap ? DimL[month - 1]: Dim[month - 1] );
    }

    private static uint DaysInMonth(int month, int year)
    {
      if (year < YearBase || year > MaxYear)
        throw new ArgumentOutOfRangeException(nameof(year), $@"Invalid year {year}");
      if (month < 1 || month > 12)
        throw new ArgumentOutOfRangeException(nameof(month), $@"Invalid month {month}");
      return (IsLeapYear(year) ? DimL[month - 1] : Dim[month - 1]);
    }

    // Days from start of year to start of month
    //
    private static readonly uint[] Dtm = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334};
    private static readonly uint[] DtmL = { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335};

    private static uint DaysToMonth(int month, bool leap)
    {
      if (month < 1 || month > 12)
        throw new ArgumentOutOfRangeException(nameof(month), $@"Invalid month {month}");
      return (leap) ? DtmL[month - 1] : Dtm[month - 1];
    }

    // Days from YEAR_BASE to start of year
    //
    static readonly uint[] Dty = {
      0,        365,    730,   1095,   1460,   1826,   2191,   2556,  2921,    3287,   // 1900-1009
      3652,    4017,   4382,   4748,   5113,   5478,   5843,   6209,  6574,    6939,   // 1910-1919
      7304,    7670,   8035,   8400,   8765,   9131,   9496,   9861,  10226,  10592,   // 1920-1929
      10957,  11322,  11687,  12053,  12418,  12783,  13148,  13514,  13879,  14244,   // 1930-1939
      14609,  14975,  15340,  15705,  16070,  16436,  16801,  17166,  17531,  17897,   // 1940-1949
      18262,  18627,  18992,  19358,  19723,  20088,  20453,  20819,  21184,  21549,   // 1950-1959
      21914,  22280,  22645,  23010,  23375,  23741,  24106,  24471,  24836,  25202,   // 1960-1969
      25567,  25932,  26297,  26663,  27028,  27393,  27758,  28124,  28489,  28854,   // 1970-1979
      29219,  29585,  29950,  30315,  30680,  31046,  31411,  31776,  32141,  32507,   // 1980-1989
      32872,  33237,  33602,  33968,  34333,  34698,  35063,  35429,  35794,  36159,   // 1990-1999
      36524,  36890,  37255,  37620,  37985,  38351,  38716,  39081,  39446,  39812,   // 2000-2009
      40177,  40542,  40907,  41273,  41638,  42003,  42368,  42734,  43099,  43464,   // 2010-2019
      43829,  44195,  44560,  44925,  45290,  45656,  46021,  46386,  46751,  47117,   // 2020-2029
      47482,  47847,  48212,  48578,  48943,  49308,  49673,  50039,  50404,  50769,   // 2030-2039
      51134,  51500,  51865,  52230,  52595,  52961,  53326,  53691,  54056,  54422,   // 2040-2049
      54787,  55152,  55517,  55883,  56248,  56613,  56978,  57344,  57709,  58074,   // 2050-2059
      58439,  58805,  59170,  59535,  59900,  60266,  60631,  60996,  61361,  61727,   // 2060-2069
      62092,  62457,  62822,  63188,  63553,  63918,  64283,  64649,  65014,  65379,   // 2070-2079
      65744,  66110,  66475,  66840,  67205,  67571,  67936,  68301,  68666,  69032,   // 2080-2089
      69397,  69762,  70127,  70493,  70858,  71223,  71588,  71954,  72319,  72684,   // 2090-2099
      73049,  73414,  73779,  74144,  74509,  74875,  75240,  75605,  75970,  76336,   // 2100-2109
      76701,  77066,  77431,  77797,  78162,  78527,  78892,  79258,  79623,  79988,   // 2110-2119
      80353,  80719,  81084,  81449,  81814,  82180,  82545,  82910,  83275,  83641,   // 2120-2129
      84006,  84371,  84736,  85102,  85467,  85832,  86197,  86563,  86928,  87293,   // 2130-2139
      87658,  88024,  88389,  88754,  89119,  89485,  89850,  90215,  90580,  90946,   // 2140-2149
      91311,  91676,  92041,  92407,  92772,  93137,  93502,  93868,  94233,  94598,   // 2150-2159
      94963,  95329,  95694,  96059,  96424,  96790,  97155,  97520,  97885,  98251,   // 2160-2159
      98616,  98981,  99346,  99712, 100077, 100442, 100807, 101173, 101538, 101903    // 2160-2169
    };

    private const uint MaxJulianDays = 90946 + 365;

    private static uint DaysToYear( int year )
    {
      return Dty[year - YearBase];
    }

    private static uint DaysToByteYear(byte year)
    {
      if (year > 250)
        throw new ArgumentOutOfRangeException(nameof(year), $@"Invalid year {1900 + year}");

      return Dty[year];
    }

    // Days in year
    //
    private static int DaysInYear( int year )
    {
      return( IsLeapYear(year) ? 366 : 365 );
    }

    // Map of standard month abbreviations to integer equivalent
    private static readonly Dictionary<string, int> MonthAbbrevs = new Dictionary<string, int> { { "JAN", 1 }, { "FEB", 2 }, { "MAR", 3 }, { "APR", 4 }, { "MAY", 5 }, { "JUN", 6 }, { "JUL", 7 }, { "AUG", 8 }, { "SEP", 9 }, { "OCT", 10 }, { "NOV", 11 }, { "DEC", 12 } };

    // Standard futures codes
    //
    private const string FuturesCodes = "FGHJKMNQUVXZ";

    #endregion Private Data and Methods

    #region Constants

    /// <summary>
    ///   Empty date
    /// </summary>
    public static readonly Dt Empty = new Dt();

    /// <summary>
    /// The maximum date supported (12/31/2149 23:50 in this version).
    /// We support the multiples of 10-min interval, yet need to provide 
    /// a second number since the Dt constructor requires it as a param.. 
    /// </summary>
    public static readonly Dt MaxValue = new Dt(31, 12, 2149, 23, 50, 0);

    /// <summary>
    /// The minimum date supported (1/1/1900 00:00 in this version).
    /// We support the multiples of 10-min interval, yet need to provide
    /// a second number since the Dt constructor requires it as a param..
    /// </summary>
    public static readonly Dt MinValue = new Dt(1, 1, 1900, 0, 0, 0);

    #endregion Constants

    #region Constructors

    /// <summary>
    ///   Construct a date from <see cref="System.DateTime"/>
    /// </summary>
    /// <param name="datetime"><see cref="System.DateTime"/> to construct date from</param>
    /// <remarks>
    ///   The default time for the constructed <see cref="Dt"/> is midnight.
    /// </remarks>
    /// <remarks>
    /// When the input is before the Dt.MinValue(01/01/1901 00:00), it is converted to Dt.Empty(NULL);
    /// When the input is greater than Dt.MaxValue(12/31/2149 23:50), it is converted to Dt.MaxValue;
    /// Else, perform the usual Dt construction.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">
    ///   <para>Thrown when <paramref name="datetime"/> is invalid.</para>
    /// </exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from a <see cref="System.DateTime"/>.</para>
    /// <code language="C#">
    ///   // Create a DateTime for 28th of April, 2002.
    ///   DateTime datetime = new DateTime( 2002, 4, 28 );
    ///
    ///   // Create the matching Dt.
    ///   Dt date = new Dt( datetime );
    /// </code>
    /// </example>
    public Dt(DateTime datetime)
    {
      //make it more effective with const long
      const long min = 599266080000000000L; //Dt.MinValue.ToDataTime().Ticks
      const long max = 678158778000000000L; //Dt.MaxValue.ToDataTime().Ticks
      if (datetime.Ticks < min)
      {
        _value = 0;
        return;
      }
      if (datetime.Ticks > max)
      {
        this = MaxValue;
        return;
      }

      _value = Value((byte) (datetime.Year - YearBase),
        (byte) datetime.Month, (byte) datetime.Day,
        (byte) (datetime.Hour*6 + datetime.Minute/10));
    }

    /// <summary>
    /// Implicit operator to convert System.DateTime type input object into Dt object
    /// </summary>
    /// <param name="dt">DateTime input</param>
    /// <returns>Equivalent Dt object</returns>
    public static implicit operator Dt(DateTime dt)
    {
      return new Dt(dt);
    }

    /// <summary>
    ///   Construct a date from day, month and year
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (January-December)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <remarks>
    ///   The default time for the constructed <see cref="Dt"/> is midnight.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">
    ///   <para><paramref name="year"/> is less than 1 or greater than 2150.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="month"/> is less than 1 or greater than 12.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="day"/> is less than 1 or greater than the number of days in <paramref name="month"/>.</para>
    /// </exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from a day, month and year.</para>
    /// <code language="C#">
    ///   // Create date 28th of April, 2002.
    ///   Dt date = new Dt( 28, Month.April, 2002 );
    /// </code>
    /// </example>
    public Dt( int day, Month month, int year )
      : this(day, (int)month, year, 0)
    {}

    /// <summary>
    ///   Construct date from day, month and year
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <remarks>
    ///   The default time for the constructed <see cref="Dt"/> is midnight.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">
    ///   <para><paramref name="year"/> is less than 1 or greater than 2150.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="month"/> is less than 1 or greater than 12.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="day"/> is less than 1 or greater than the number of days in <paramref name="month"/>.</para>
    /// </exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from a day, month and year.</para>
    /// <code language="C#">
    ///   // Create date 28th of April, 2002.
    ///   Dt date = new Dt( 28, 4, 2002 );
    /// </code>
    /// </example>
    public Dt( int day, int month, int year )
      : this(day, month, year, 0)
    {}

    /// <summary>
    ///   Const a date from day, month, year, hour, minute and second
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <param name="hour">Hour (0-23)</param>
    /// <param name="minute">Minute (0-59)</param>
    /// <param name="second">Second (0-59)</param>
    /// <exception cref="System.ArgumentOutOfRangeException">
    ///   <para><paramref name="year"/> is less than 1 or greater than 2150.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="month"/> is less than 1 or greater than 12.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="day"/> is less than 1 or greater than the number of days in <paramref name="month"/>.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="hour"/> is less than 0 or greater than 23.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="minute"/> is less than 0 or greater than 59.</para>
    ///   <para>-or-</para>
    ///   <para><paramref name="second"/> is less than 0 or greater than 59.</para>
    /// </exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from a day, month, year, hour, minute and second.</para>
    /// <code language="C#">
    ///   // Create date midday, 28th of April, 2002.
    ///   Dt date = new Dt( 28, 4, 2002, 12, 0, 0 );
    /// </code>
    /// </example>
    public Dt( int day, int month, int year, int hour, int minute, int second )
      : this(day, month, year, hour * 6 + minute / 10)
    {}

    /// <summary>
    ///   Construct a date from an integer YYYYMMDD format
    /// </summary>
    /// <param name="date">integer date in YYYYMMDD format</param>
    /// <remarks>
    ///   The default time for the constructed <see cref="Dt"/> is midnight.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">
    ///   <para>YYYY is less than 1 or greater than 2150.</para>
    ///   <para>-or-</para>
    ///   <para>MM is less than 1 or greater than 12.</para>
    ///   <para>-or-</para>
    ///   <para>DD is less than 1 or greater than the number of days in MM.</para>
    /// </exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from an integer in YYYYMMDD format.</para>
    /// <code language="C#">
    ///   // Create date 28th of April, 2002.
    ///   Dt date = new Dt( 20020428 );
    /// </code>
    /// </example>
    public Dt( int date )
      : this(date % 100, (date % 10000) / 100, date / 10000, 0)
    {}

    /// <summary>
    ///   Construct a date from a Time (double).
    /// </summary>
    /// <remarks>
    ///   <para>Time is days from Jan 1, 1900 / 365.</para>
    ///   <para>Subtleties exist when converting to and from continuous time. For
    ///   Consistency, always convert dates using the Time operator
    ///   and use the difference between two Times to calculate in
    ///   continuous time.</para>
    /// </remarks>
    /// <param name="time">Time (double) in years</param>
    /// <exception cref="System.Exception">Thrown when <paramref name="time"/> is invalid</exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from a Time (double).</para>
    /// <code language="C#">
    ///   // Create date which is 365 days from Jan 1st, 1900.
    ///   Dt date = new Dt( 1.0 );
    /// </code>
    /// </example>
    public Dt(double time)
    {
      const double osec = 1.0/24.0/60.0/60.0;    // 1 second for rounding
      // Convert to days
      double days = time * 365.0;
      // Calculate date
      this = Add( new Dt(1,1,YearBase), (int)Math.Floor(days + osec));
      // Calculate time
      minute_ = (byte)((((days - Math.Floor(days))*60.0*24.0+osec)/10.0) % 144); // Minutes in day
    }

    /// <summary>
    ///   Construct a date from a relative Time.
    /// </summary>
    /// <remarks>
    ///   <para>Time is days from date / 365.</para>
    ///   <para>Subtleties exist when converting to and from continuous time. For
    ///   Consistency, always convert dates using the Time operator
    ///   and use the difference between two Times to calculate in
    ///   continuous time.</para>
    /// </remarks>
    /// <param name="date">Start relative date</param>
    /// <param name="t">Time (double) in years</param>
    /// <exception cref="System.Exception">Thrown when resulting date is invalid</exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from a date and a relative Time (double).</para>
    /// <code language="C#">
    ///   // Create date which is 365 days from Jan 1st, 2004.
    ///   Dt start = new Dt( 1, 1, 2004 );
    ///   Dt date = new Dt( start, 1.0 );
    /// </code>
    /// </example>
    public Dt(Dt date, double t)
      : this(date, t, true)
    {}

    /// <summary>
    ///   Construct a date from a Modified Julian.
    /// </summary>
    /// <param name="julianDate">Julian date</param>
    /// <exception cref="System.Exception">Thrown when resulting date is invalid</exception>
    /// <example>
    /// <para>The following sample demonstrates constructing a <see cref="Dt"/> from a modified Julian date.</para>
    /// <code language="C#">
    ///   // Julian date
    ///   uint julian = 124;
    ///   // Construct equivalent date
    ///   Dt date = new Dt( julian );
    /// </code>
    /// </example>
    public Dt(uint julianDate)
      : this(julianDate, 0)
    {}

    #endregion Constructors

    #region Internal Constructors

    /// <summary>
    /// Internal constructor
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <param name="ticks">Ticks (10 min intervals) within day</param>
    private Dt(int day, int month, int year, int ticks)
    {
      if (day == 0 && month == 0 && ticks == 0 && (year == YearBase || year == 0))
      {
        _value = 0;
        return;
      }
      Validate(day, month, year, ticks);
      _value = Value((byte)(year - YearBase), (byte)month, (byte)day, (byte)ticks);
    }

    /// <summary>
    /// Internal constructor
    /// </summary>
    /// <param name="date">Base date</param>
    /// <param name="time">Time to add</param>
    /// <param name="timeIsYears">True if time is in years, otherwise in days</param>
    private Dt(Dt date, double time, bool timeIsYears)
    {
      if (date.IsEmpty())
      {
        _value = 0;
        return;
      }
      const double osec = 1.0 / 24.0 / 60.0 / 60.0;    // 1 second for rounding
      // Convert to days
      double days = timeIsYears ? (time * 365.0) : time;
      // Add the fraction part of the begin date
      days += date.minute_ / 6.0 / 24.0;
      // Calculate date
      this = Add(date, (int)Math.Floor(days + osec));
      // Calculate time
      minute_ = (byte)((((days - Math.Floor(days)) * 60.0 * 24.0 + osec) / 10.0) % 144); // Minutes in day
    }

    /// <summary>
    /// Internal constructor
    /// </summary>
    /// <param name="julianDate">Julian date</param>
    /// <param name="minute">Minutes of day</param>
    private Dt(uint julianDate, byte minute)
    {
      if (julianDate < 15020 || julianDate > MaxJulianDays + 15020)
        throw new ArgumentOutOfRangeException(nameof(julianDate), @"Year outside the range 1900-2150");
      if (minute >= MaxTicksPerDay)
        throw new ArgumentOutOfRangeException(nameof(minute), $@"Raw minuteYear {minute} outside the range 0-{MaxTicksPerDay - 1}");
      // Move to 1900
      julianDate -= 15020;
      // Guess year
      var year_ = (byte)(julianDate / 365.0);
      // Adjust if necessary
      if (julianDate <= DaysToYear(year_ + YearBase))
        year_--;
      bool lp = IsLeapYear(year_ + YearBase);
      julianDate -= DaysToYear(year_ + YearBase);
      // Guess month
      var month_ = (byte)(julianDate / 32 + 1);
      // Adjust if necessary
      if (julianDate > (DaysToMonth(month_, lp) + DaysInMonth(month_, lp)))
        month_++;
      var day_ = (byte)(julianDate - DaysToMonth(month_, lp));
      _value = Value(year_, month_, day_, minute);
    }

    #endregion Internal Constructors

    #region Operators

    /// <summary>Greater that operator</summary>
    public static bool operator>(Dt d1, Dt d2 ) { return(Cmp(d1, d2) > 0 ); }

    /// <summary>Greater or equal than operator</summary>
    public static bool operator>=(Dt d1, Dt d2) { return(Cmp(d1, d2) >= 0); }

    /// <summary>Less than operator</summary>
    public static bool operator<(Dt d1, Dt d2) { return(Cmp(d1, d2) < 0); }

    /// <summary>Less or equal to operator</summary>
    public static bool operator<=(Dt d1, Dt d2) { return(Cmp(d1, d2) <= 0); }

    /// <summary>Equal to operator</summary>
    public static bool operator==(Dt d1, Dt d2) { return(Cmp(d1, d2) == 0); }

    /// <summary>Not equal to operator</summary>
    public static bool operator !=(Dt d1, Dt d2) { return(Cmp(d1, d2) != 0); }

    ///// <summary>Equal to operator</summary>
    //public static bool operator ==(Dt d1, DateTime d2) { return (Cmp(d1, d2) == 0); }

    ///// <summary>Not equal to operator</summary>
    //public static bool operator !=(Dt d1, DateTime d2) { return (Cmp(d1, d2) != 0); }

    /// <summary>Calculate the difference (d1 - d2) as number of days.
    /// The difference in hours/minutes/seconds is counted in the fraction part.</summary>
    public static double operator -(Dt d1, Dt d2) { return SignedFractDiff(d2, d1); }

    /// <summary>Subtracts the specified number of calendar days from the specified date.</summary>
    /// <remarks>This operator only counts weekends as non-business days.</remarks>
    public static Dt operator -(Dt d1, int days) { return Add(d1, -days); }

    /// <summary>Adds the specified number of calendar days to the specified date.</summary>
    /// <remarks>This operator only counts weekends as non-business days.</remarks>
    public static Dt operator +(Dt d1, int days) { return Add(d1, days); }

    /// <summary>
    ///  Returns the later of the the two dates.
    /// </summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    public static Dt Later(Dt d1, Dt d2) { return d1 < d2 ? d2 : d1; }

    /// <summary>
    ///  Returns the earlier of the the two dates.
    /// </summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    public static Dt Earlier(Dt d1, Dt d2) { return d1 < d2 ? d1 : d2; }

    /// <summary>
    /// Equals operator override
    /// </summary>
    public override bool Equals(object other)
    {
      if (!(other is Dt)) return false;
      var otherDt = (Dt)other;
      // Order of comparison is from most-likely to differ to least-likely
      return (day_ == otherDt.day_ && month_ == otherDt.month_ && year_ == otherDt.year_ && minute_ == otherDt.minute_);
    }

    /// <summary>
    /// GetHashCode override
    /// </summary>
    public override int GetHashCode()
    {
      return (((int)year_ << 3) | ((int)month_ << 2) | ((int)day_ << 1) | (int)minute_);
    }

    #endregion Operators

    #region IComparable

    /// <summary>
    /// IComparable.CompareTo implementation.
    /// </summary>
    public int CompareTo(object obj)
    {
      if (!(obj is Dt))
        throw new ArgumentException("object is not a Dt");
      return Cmp(this, (Dt)obj);
    }

    /// <summary>
    /// IComparable&lt;T&gt;.CompareTo implementation.
    /// </summary>
    public int CompareTo(Dt obj)
    {
      return Cmp(this, obj);
    }

    #endregion IComparable

    #region Methods

    #region Validate

    /// <summary>
    ///   Validate integer date
    /// </summary>
    /// <param name="date">Integer date in YYYYMMDD format</param>
    /// <returns>true if date is valid</returns>
    /// <example>
    /// <para>The following sample demonstrates testing if a integer YYYYMMDD is a valid date.</para>
    /// <code language="C#">
    ///   // Create date April 28, 2002.
    ///   int date = 20020428;
    ///
    ///   // Test if date in YYYYMMDD format is a valid date.
    ///   if( Dt.IsValid(date) )
    ///   {
    ///     Console.WriteLine( "{0} is a valid date", date );
    ///   }
    /// </code>
    /// </example>
    public static bool IsValid(int date)
    {
      int y = date / 10000;
      int m = date / 100 - y * 100;
      int d = date - m * 100 - y * 10000;
      return IsValid(d, m, y, 0);
    }

    /// <summary>
    ///   Test for valid date.
    /// </summary>
    /// <returns>true if date valid (i.e. not empty)</returns>
    /// <note>This does not generate any exception if the date is not valid.</note>
    /// <example>
    /// <para>The following sample demonstrates testing if a <see cref="Dt"/> is valid.</para>
    /// <code language="C#">
    ///   // Test if month/day/year is a valid date.
    ///   Dt date = new Dt( day, month, year );
    ///
    ///   if( date.IsValid() )
    ///   {
    ///     Console.WriteLine( "{0}/{1}/{2} is a valid date", month, day, year);
    ///   }
    /// </code>
    /// </example>
    public bool IsValid()
    {
      return IsValid(Day, Month, Year, Ticks);
    }

    /// <summary>
    ///   Validate the date, throwing an exception if invalid.
    /// </summary>
    /// <exception cref="System.Exception">Thrown if date is invalid</exception>
    /// <example>
    /// <para>The following sample demonstrates validating a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Create date to test
    ///   Dt date = new Dt( day, month, year );
    ///
    ///   // Test if month/day/year is a valid date.
    ///   try
    ///   {
    ///     date.Validate();
    ///   }
    ///   catch( Exception ex )
    ///   {
    ///     Console.WriteLine( "Date {0} is invalid - {1}", date, ex );
    ///   }
    /// </code>
    /// </example>
    public void Validate()
    {
      if (!IsValid(Day, Month, Year, Ticks))
        throw new ArgumentOutOfRangeException($"Invalid date {Day}/{Month}/{Year} {Ticks * 10}min");
    }

    /// <summary>
    ///   Test for valid date and time
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <param name="ticks">10 second intervals within day</param>
    /// <returns>true if date valid</returns>
    private static void Validate(int day, int month, int year, int ticks)
    {
      if (!IsValid(day, month, year, ticks))
        throw new ArgumentOutOfRangeException($"Invalid date {day}/{month}/{year} {ticks * 10}min");
    }

    /// <summary>
    ///   Test for valid date and time
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <param name="ticks">10 second intervals within day</param>
    /// <returns>true if date valid</returns>
    private static bool IsValid(int day, int month, int year, int ticks)
    {
      return (
            (year >= YearBase && year < MaxYear) &&
            (month >= 1 && month <= 12) &&
            (day >= 1 && day <= DaysInMonth(month, year)) &&
            (ticks >= 0 && ticks < MaxTicksPerDay)
            );
    }

    #endregion Validate

    #region Is

    /// <summary>
    ///   Test for empty date.
    /// </summary>
    /// <remarks>
    ///   An empty date is an uninitialised or cleared
    ///   date. Sometimes this is useful to distinguish
    ///   separate from an invalid date.
    /// </remarks>
    /// <returns>true if date empty</returns>
    /// <example>
    /// <para>The following sample demonstrates testing if a <see cref="Dt"/> is clear.</para>
    /// <code language="C#">
    ///   // Create date to test
    ///   Dt date = Dt.Empty;
    ///
    ///   // Test if it's clear
    ///   if( date.IsEmpty() )
    ///     Console.WriteLine( "date is clear" );
    /// </code>
    /// </example>
    [Pure]
    public bool IsEmpty()
    {
      // Note: support year = 1900 for XL
      return ((day_ == 0) && (month_ == 0) && (year_ == 0));
    }

    /// <summary>
    ///   Test for valid settlement date.
    /// </summary>
    /// <returns>true if date is a valid settlement date for calendar</returns>
    /// <example>
    /// <para>The following sample demonstrates testing if this <see cref="Dt"/> is valid settlement date for the <see cref="Calendar"/>.</para>
    /// <code language="C#">
    ///   // Test if month/day/year is a valid NY banking settlement date.
    ///   Dt date = new Dt( day, month, year );
    ///
    ///   if( date.IsValid(Calendar.NYB) )
    ///   {
    ///     Console.WriteLine("{0}/{1}/{2} is a valid NY settlement date", month, day, year);
    ///   }
    /// </code>
    /// </example>
    public bool IsValidSettlement(Calendar cal)
    {
      if (!IsValid())
        throw new ArgumentOutOfRangeException(nameof(cal), @"Invalid date");
      return cal.IsValidSettlement(day_, month_, year_ + YearBase);
    }

    /// <summary>
    ///   Test if last day of month.
    /// </summary>
    /// <returns>true if date is last day of month</returns>
    /// <example>
    /// <para>The following sample demonstrates testing if this <see cref="Dt"/> is the last day of the month.</para>
    /// <code language="C#">
    ///   // Create a date
    ///   Dt date = new Dt( day, month, year );
    ///
    ///   if( date.IsLastDayOfMonth() )
    ///   {
    ///     Console.WriteLine("{0} is the last day of {1}", date, date.Month );
    ///   }
    /// </code>
    /// </example>
    public bool IsLastDayOfMonth()
    {
      return( day_ == DaysInMonth(month_, year_ + YearBase) );
    }

    ///<summary>
    /// Test if last business day of the month
    ///</summary>
    ///<param name="dt">Date to be tested on</param>
    ///<param name="cal">Calendar</param>
    ///<returns>true if the date is the last business day of its month</returns>
    public static bool IsLastBusinessDayOfMonth(Dt dt, Calendar cal)
    {
      if (!dt.IsValidSettlement(cal))
        return false;

      if (dt.IsLastDayOfMonth())
        return true;

      var nextDt = AddDays(dt, 1, cal);
      if (nextDt.Month != dt.Month)
        return true;

      return false;
    }

    #endregion Is

    #region Add

    /// <summary>
    /// Adds to the specified date the specified relative time.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="time">The time.</param>
    /// <returns>Dt.</returns>
    public static Dt Add(Dt date, RelativeTime time)
    {
      return new Dt(date, time.Days, false);
    }
    
    /// <summary>
    ///   Add tenor to date.
    /// </summary>
    /// <remarks>
    ///   <para>If resulting date is invalid, sets date to last valid date.
    ///   E.g. Mar 31 + 1 month = Apr 30.</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="tenor">Tenor to add</param>
    /// <returns>New date</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a <see cref="Tenor"/> to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 5 years from today
    ///   Tenor fiveYearTenor = new Tenor( 5, TimeUnit.Years );
    ///   Dt fiveYears = Dt.Add( today, fiveYearTenor );
    ///
    ///   // Get 1 day from today
    ///   Tenor oneDayTenor = new Tenor( "1 D" );
    ///   Dt oneDay = Dt.Add( today, oneDayTenor );
    /// </code>
    /// </example>
    public static Dt Add(Dt date, Tenor tenor)
    {
      return Add(date, tenor.N, tenor.Units);
    }

    /// <summary>
    ///   Adds n weeks to a date and adjust the end date to a specified week day.
    /// </summary>
    /// <param name="date">The original date.</param>
    /// <param name="n">The number of weeks.</param>
    /// <param name="rule">The cycle rule.</param>
    /// <returns>The end date</returns>
    public static Dt AddWeeks(Dt date, int n, CycleRule rule)
    {
      int shift = 0;
      if  (rule >= CycleRule.Monday && rule <= CycleRule.Sunday)
      {
        // How many days to move in order to reach the required week day?
        shift = (int) rule - (int)CycleRule.Monday - (int)date.DayOfWeek();
      }
      return Add(date, n*7 + shift);
    }

    /// <summary>
    ///   Adds n months to a date and adjust the end date to a day specified by the cycle rule.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="n">The number of months.</param>
    /// <param name="rule">The cycle rule.</param>
    /// <returns>The end date</returns>
    public static Dt AddMonths(Dt date, int n, CycleRule rule)
    {
      if (!date.IsEmpty()) date.Validate();

      int origDay = date.Day;
      int origMonth = date.Month;
      int origYear = date.Year;

      // Add months
      int month = origMonth + n;

      int year;
      // Adjust months and year
      if (month < 1)
      {
        year = origYear - (12 - month)/12;
        month = 12 - (-month)%12;
      }
      else if (month > 12)
      {
        year = origYear + (month - 1)/12;
        month = (month - 1)%12 + 1;
      }
      else
      {
        year = origYear;
      }

      // User requests IMM date?
      if (rule == CycleRule.IMM)
      {
        // The third Wednesday of the target month.
        return NthWeekDay(month, year, 3, Base.DayOfWeek.Wednesday);
      } else if (rule == CycleRule.IMMAUD)
      {
        // One Sydney business day preceding the second Friday of the relevant settlement month.
        var secondFriday = NthWeekDay(month, year, 2, Base.DayOfWeek.Friday);
        return AddDays(secondFriday, -1, Calendar.SYB);
      } else if (rule == CycleRule.IMMNZD)
      {
        // The first Wednesday after the ninth day of the relevant settlement month.
        for (int i = 1; i < 6; i++)
        {
          var wed = NthWeekDay(month, year, i, Base.DayOfWeek.Wednesday);
          if (wed.Day > 9)
          {
            return wed;
          }
        }
        // ultra caution, never going to get here
        throw new ArgumentException($"Couldn't find first Wednesday after ninth day of month {month} in year {year}");
      }

      // Special handling for cycling on fixed day or EOM
      int day;
      if (rule >= CycleRule.First && rule <= CycleRule.Thirtieth)
        day = rule - CycleRule.First + 1;
      else if (rule == CycleRule.EOM)
        day = 31;
      else if (rule == CycleRule.None)
        day = origDay;
      else
      {
        throw new ArgumentException($"CycleRule {rule} is not compatible with the frequency");
      }

      // Adjust to fit within month
      day = Math.Min(day, (int) DaysInMonth(month, year));

      return new Dt(day, month, year, date.minute_);
    }

    /// <summary>
    ///  Adds n cycle periods to a date based on a cycle rule.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="freq">The frequency.</param>
    /// <param name="n">The number of periods.</param>
    /// <param name="rule">The cycle rule.</param>
    /// <exception cref="NotImplementedException"></exception>
    /// <returns>Date</returns>
    public static Dt Add(Dt date, Frequency freq, int n, CycleRule rule)
    {
      switch (rule)
      {
        case CycleRule.IMM:
        case CycleRule.IMMAUD:
        case CycleRule.IMMNZD:
          if (freq >= Frequency.BiWeekly)
          {
            throw new ArgumentException($"Invalid frequency for {rule}");
          }
          break;
        case CycleRule.IMMCad:
        case CycleRule.FRN:
        case CycleRule.SFE:
        case CycleRule.TBill:
          throw new NotImplementedException($"{rule} not supported yet.");
        default:
          break;
      }

      switch (freq)
      {
      case Frequency.Continuous:
        throw new ArgumentException("Invalid frequency");
      case Frequency.None:
        if(!date.IsEmpty()) date.Validate();
        return date;
      case Frequency.TwentyEightDays:
        return Add(date, 28*n);
      case Frequency.BiWeekly:
        return AddWeeks(date, 2*n, rule);
      case Frequency.Weekly:
        return AddWeeks(date, n, rule);
      case Frequency.Daily:
        return Add(date, n);
      default:
        // fraction of year
        return AddMonths(date, n*12/(int) freq, rule);
      }
    }

    /// <summary>
    ///   Add n frequency periods to date.
    /// </summary>
    /// <remarks>
    ///   <para>If resulting date is invalid, sets date to last valid date.</para>
    ///   <para>E.g. Mar 31 + 1 month = Apr 30.</para>
    ///   <para>See Dt.Add( Dt date, int n, TimeUnit u )</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="frequency">Frequency (per year) to add</param>
    /// <param name="n">Number of frequency period to add</param>
    /// <param name="anchorDate">Will always stay on the same day of month if possible; the point is that we care about the original anchor date and not the date that we are currently adding to.</param>
    /// <param name="eomDeterminationDate">Date that detemines if EOM is in effect</param>
    /// <param name="eomRule">All dates will be on the end of month if this is true, AND the anchor date is also set to a day at the end of the month.</param>
    /// <returns>Date with added frequency</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a <see cref="Frequency"/> to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 3 months but also goto the 15th
    ///   Dt threeMonthsForward = Dt.Add( today, Frequency.Quarterly, 1, new Dt(15,1,2000 );
    /// </code>
    /// </example>
    public static Dt Add(Dt date, Frequency frequency, int n, Dt anchorDate, Dt eomDeterminationDate, bool eomRule)
    {
      switch (frequency)
      {
        case Frequency.Continuous:
          throw new ArgumentOutOfRangeException(nameof(frequency), @"Invalid frequency");
        case Frequency.None:
          if(!date.IsEmpty()) date.Validate();
          return date;
        case Frequency.TwentyEightDays:
          return Add(date, 28 * n);
        case Frequency.BiWeekly:
          return Add(date, 14 * n);
        case Frequency.Weekly:
          return Add(date, 7 * n);
        case Frequency.Daily:
          return Add(date, n);
        default:
          // fraction of year
          return AddMonth(date, n * 12 / (int)frequency, anchorDate, eomDeterminationDate, eomRule);
      }
    }

    /// <summary>
    ///   Add tenor as a string to date.
    /// </summary>
    /// <remarks>
    ///   <para>If resulting date is invalid, sets date to last valid date.
    ///     E.g. Mar 31 + 1 month = Apr 30.</para>
    ///   <para>The input string <c>str</c> can be either a single tenor like "1Y",
    ///     or a composite tenor like "1Y1M1W" (meaning 1 year plus 1 month plus 1 week).
    ///     In the later case, the function adds sequentially each of the single tenors
    ///     within the composite and returns the final result date.
    ///   </para>
    ///   <para>For example, with "1Y1M1W",
    ///     the date is first added by 1Y to get a valid result date,
    ///     then the result date is added by 1M to get a new valid result date,
    ///     and finally the new result date is added by 1W.</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="str">string containing sequence of tenors to add to asOf date</param>
    /// <returns>New date</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a <see cref="Tenor">Tenor</see>
    /// string to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 5 years from today
    ///   Dt fiveYears = Dt.Add( today, "5 Years" );
    ///
    ///   // Get 1 day from today
    ///   Dt oneDay = Dt.Add( today, "1 D" );
    /// </code>
    /// <para>The function accepts composite tenor such as "1M3M" with repeated time units.
    ///   But please note that "1M3M" is not functionally equivalent to "4M",
    ///   as shown in the following examples.
    ///</para>
    /// <code language="C#">
    ///   Dt date1 = Dt.Add(new Dt(20120131), "4M");
    ///   // date1 is 2012-05-31
    ///
    ///   Dt date2 = Dt.Add(new Dt(20120131), "1M3M");
    ///   // date2 is 2012-05-29, because 2012-01-31 add 1M yields 2012-02-29.
    /// </code>
    /// </example>
    public static Dt Add(Dt date, string str)
    {
      if (String.IsNullOrEmpty(str))
        return date;

      var match = Regex.Match(str, @"^\s*(\d+\s*\D+)+$");
      if (!match.Success)
        throw new ArgumentException($"Unknown tenor format [{str}]");
      var captures = match.Groups[1].Captures;
      for (int i = 0, n = captures.Count; i < n; ++i)
      {
        Tenor t = Tenor.Parse(captures[i].Value);
        date = Add(date, t);
      }
      return date;
    }

    /// <summary>
    ///   Add frequency to date.
    /// </summary>
    /// <remarks>
    ///   <para>If resulting date is invalid, sets date to last valid date.</para>
    ///   <para>E.g. Mar 31 + 1 month = Apr 30.</para>
    ///   <para>See Dt.Add( Dt date, int n, TimeUnit u )</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="frequency">Frequency (per year) to add</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if the starting date was the end of the month</param>
    /// <returns>Date with added frequency</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a <see cref="Frequency"/> to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 3 months using the end-of-month rule
    ///   Dt threeMonthsForward = Dt.Add( today, Frequency.Quarterly, true );
    /// </code>
    /// </example>
    public static Dt Add(Dt date, Frequency frequency, bool eomRule)
    {
      return Add(date, frequency, 1, eomRule);
    }

    /// <summary>
    ///   Subtract frequency from date.
    /// </summary>
    /// <remarks>
    ///   <para>If resulting date is invalid, sets date to last valid date.</para>
    ///   <para>E.g. Mar 31 + 1 month = Apr 30.</para>
    ///   <para>see Dt.Add( Dt date, int n, TimeUnit u )</para>
    /// </remarks>
    /// <param name="date">Date to subtract from</param>
    /// <param name="frequency">Frequency (per year) to subtract</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///                the starting date was the end of the month</param>
    ///
    /// <returns>Date with frequency substrated</returns>
    /// <example>
    /// <para>The following sample demonstrates subtracting a <see cref="Frequency"/> from a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Subtract 3 months ignoring the eom rule
    ///   Dt threeMonthsBack = Dt.Subtract( today, Frequency.Quarterly, false );
    /// </code>
    /// </example>
    public static Dt Subtract(Dt date, Frequency frequency, bool eomRule)
    {
      return Add(date, frequency, -1, eomRule);
    }

    /// <summary>
    ///   Add n frequency periods to date.
    /// </summary>
    /// <remarks>
    ///   <para>If resulting date is invalid, sets date to last valid date.</para>
    ///   <para>E.g. Mar 31 + 1 month = Apr 30.</para>
    ///   <para>See Dt.Add( Dt date, int n, TimeUnit u )</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="frequency">Frequency (per year) to add</param>
    /// <param name="n">Number of frequency period to add</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if the starting date was the end of the month</param>
    /// <returns>Date with added frequency</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a <see cref="Frequency"/> to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///   // Get 3 months using the end-of-month rule
    ///   Dt threeMonthsForward = Dt.Add( today, Frequency.Quarterly, true );
    /// </code>
    /// </example>
    public static Dt Add(Dt date, Frequency frequency, int n, bool eomRule)
    {
      return Add(date, frequency, n, Dt.Empty, Dt.Empty, eomRule);
    }

    /// <summary>
    /// Add actual number days to date.
    /// </summary>
    /// <remarks>
    /// <para>If resulting date is invalid, sets date to last valid date.</para>
    /// <para>E.g. Mar 31 + 1 month = Apr 30.</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="n">Number of days to add. May be positive or negative.</param>
    /// <returns><paramref name="date"/> + <paramref name="n"/> days</returns>
    /// <example>
    /// <para>The following sample demonstrates adding days to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Add 30 days to today
    ///   Dt thirtyDaysForward = Dt.Add( today, 30 );
    ///
    ///   // Subtract 30 days from today
    ///   Dt thirtyDaysBack = Dt.Add( today, -30 );
    /// </code>
    /// </example>
    public static Dt Add(Dt date, int n)
    {
      uint days = (uint)(date.ToJulian() + n);
      return new Dt(days, date.minute_);
    }

    /// <summary>
    ///   Add months to date.
    /// </summary>
    /// <param name="date">Date to add to</param>
    /// <param name="n">Number of months to add. May be positive or negative.</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///                the starting date was the end of the month</param>
    /// <returns><paramref name="date"/> + <paramref name="n"/> months</returns>
    /// <example>
    /// <para>The following sample demonstrates adding months to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 3 months forward using the end-of-month rule
    ///   Dt threeMonthsForward = Dt.Add( today, 3, true );
    ///
    ///   // Get 3 months before ignoring the end-of-month rule
    ///   Dt threeMonthsBack = Dt.Add( today, -3, true );
    /// </code>
    /// </example>
    public static Dt AddMonth(Dt date, int n, bool eomRule)
    {
      return AddMonth(date, n, Dt.Empty, Dt.Empty, eomRule);
    }

    /// <summary>
    ///   Add months to date.
    /// </summary>
    /// <param name="date">Date to add to</param>
    /// <param name="n">Number of months to add. May be positive or negative.</param>
    /// <param name="anchorDate">Will always stay on the same day of month if possible; the point is that we care about the original anchor date and not the date that we are currently adding to.</param>
    /// <param name="eomDeterminationDate">If this date empty, only ever apply EOM if this date is the end of the month; otherwise look at the 'date' param.</param>
    /// <param name="eomRule">All dates will be on the end of month if this is true, AND the anchor date is also set to a day at the end of the month.</param>
    /// <returns><paramref name="date"/> + <paramref name="n"/> months</returns>
    /// <example>
    /// <para>The following sample demonstrates adding months to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 3 months forward and also goto the 15th of that month.
    ///   Dt threeMonthsForward = Dt.Add( today, 3, new Dt(15, 1, 2000) );
    ///
    ///   // Get 3 months and also goto the 15th of that month.
    ///   Dt threeMonthsBack = Dt.Add( today, -3, new Dt(15, 1, 2000) );
    /// </code>
    /// </example>
    private static Dt AddMonth(Dt date, int n, Dt anchorDate, Dt eomDeterminationDate, bool eomRule)
    {
      if(!date.IsEmpty()) date.Validate();

      int day = anchorDate.IsEmpty() ? date.Day : anchorDate.Day;
      int month = date.Month;
      int year = date.Year;

      // Add months
      month += n;

      // Adjust months and year
      if (month < 1)
      {
        year -= (12 - month)/12;
        month = 12 - (-month)%12;
      }
      else if (month > 12)
      {
        year += (month - 1)/12;
        month = (month - 1)%12 + 1;
      }

      if (eomRule && !eomDeterminationDate.IsEmpty() &&
          eomDeterminationDate.Day == DaysInMonth(eomDeterminationDate.Month, eomDeterminationDate.Year))
      {
        // Adjust final date to end of month if starting date end of month.
        day = (int) DaysInMonth(month, year);
      }
      else if (eomRule && eomDeterminationDate.IsEmpty() && day == DaysInMonth(date.Month, date.Year))
      {
        // Adjust final date to end of month if starting date end of month.
        day = (int) DaysInMonth(month, year);
      }
      else if (day > DaysInMonth(month, year))
      {
        // Adjust day back into month if past month end.
        day = (int) DaysInMonth(month, year);
      }

      return new Dt(day, month, year, date.minute_);
    }

    /// <summary>
    ///   Add actual number of specified time units to date.
    /// </summary>
    /// <remarks>
    ///   <para>If resulting date is invalid, sets date to last valid date.</para>
    ///   <para>E.g. Mar 31 + 1 month = Apr 30.</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="n">Number of time units to add</param>
    /// <param name="timeUnit">Time units to add</param>
    /// <returns>New date</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a time period to <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 5 years from today
    ///   Dt fiveYears = Dt.Add( today, 5, TimeUnit.Years );
    ///
    ///   // Get 1 day from today
    ///   Dt oneDay = Dt.Add( today, 1, TimeUnit.Days );
    /// </code>
    /// </example>
    public static Dt Add(Dt date, int n, TimeUnit timeUnit)
    {
      switch (timeUnit)
      {
        case TimeUnit.Days:
          return Add(date, n);
        case TimeUnit.Weeks:
          return Add(date, n*7);
        case TimeUnit.Months:
          return AddMonth(date, n, false);
        case TimeUnit.Years:
        {
          int day = date.Day;
          int month = date.Month;
          int year = date.Year;

          if (day > DaysInMonth(month, year))
            throw new ArgumentOutOfRangeException(nameof(date), @"Invalid day");

          // Add years
          year += n;

          // Adjust day back into month if past month end.
          if (day > DaysInMonth(month, year))
            day = (int) DaysInMonth(month, year);

          return new Dt(day, month, year, date.minute_);
        }
        default:
          throw new ArgumentOutOfRangeException(nameof(timeUnit), "Invalid TimeUnit");
      }
    }

    /// <summary>
    ///   Add business days to date.
    /// </summary>
    /// <remarks>
    ///   <para>Calendar of none means just skip weekends.</para>
    /// </remarks>
    /// <param name="date">Date to add to</param>
    /// <param name="days">Number of days to add</param>
    /// <param name="calendar">Calendar for business day calculation</param>
    /// <returns>new date</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a number of business days to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get fifth business day in NY.
    ///   Dt fifthNYBusDay = Dt.AddDays( today, 5, Calendar.NYB );
    ///
    ///   // Get next week day.
    ///   Dt oneDay = Dt.AddDays( today, 1, Calendar.None );
    /// </code>
    /// </example>
    public static Dt AddDays(Dt date, int days, Calendar calendar)
    {
      if (!date.IsEmpty()) date.Validate();

      int day = date.Day;
      int month = date.Month;
      int year = date.Year;
      bool lp = IsLeapYear(year);

      if (days < 0)
      {
        while (days < 0)
        {
          day--;
          if (day < 1)
            month--;
          if (month < (int)Base.Month.January)
          {
            month = (int)Base.Month.December;
            year--;
            lp = IsLeapYear(year);
          }
          if (day < 1)
            day = (int)DaysInMonth(month, lp);
          if (calendar.IsValidSettlement(day, month, year))
            days++;
        }
      }
      else if (days > 0)
      {
        while (days > 0)
        {
          day++;
          if (day > DaysInMonth(month, lp))
          {
            month++;
            day = 1;
          }
          if (month > (int)Base.Month.December)
          {
            year++;
            lp = IsLeapYear(year);
            month = (int)Base.Month.January;
          }
          if (calendar.IsValidSettlement(day, month, year))
            days--;
        }
      }

      return new Dt(day, month, year, date.minute_);
    }

    #endregion Add

    #region Diff

    /// <summary>
    ///   Calculate number of business days between two dates given calendar.
    /// </summary>
    /// <param name="date1">Earlier date</param>
    /// <param name="date2">Later date</param>
    /// <param name="calendar">Calendar</param>
    /// <returns>Number of business days between two dates</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the number of business days between two <see cref="Dt"/>s.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get 5 days from today
    ///   Dt fiveDays = Dt.Add( today, 5, TimeUnit.Days );
    ///
    ///   // Calculate the number of NY business days between today and 4 days time.
    ///   int count = Dt.BusinessDays(today, fiveDays, Calendar.NYC);
    ///
    ///   Console.WriteLine( "Number of NY business days between {0} and {1} is {2}", today, fiveDays, count );
    /// </code>
    /// </example>
    public static int BusinessDays(Dt date1, Dt date2, Calendar calendar)
    {
      int days = 0;

      // Validate dates
      if (!date1.IsValid())
        throw new ArgumentOutOfRangeException(nameof(date1), "Invalid date");
      if (!date2.IsValid())
        throw new ArgumentOutOfRangeException( nameof(date2), "Invalid date" );

      // Check order of dates
      if( date1 > date2 )
        throw new ArgumentOutOfRangeException( nameof(date2), $"Dates out of order ({date1} > {date2})");

      while( date1 < date2 )
      {
        // Add calendar date
        date1 = Add(date1, 1);
        if (date1.IsValidSettlement(calendar))
          days++;
      }

      return days;
    }

    /// <summary>
    ///   Calculate actual number of days from and including
    ///   <paramref name="start"/> to and excluding <paramref name="end"/> dates.
    /// </summary>
    /// <param name="start">Earlier date</param>
    /// <param name="end">Later date</param>
    /// <returns>Number of days between <paramref name="start"/> and <paramref name="end"/></returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the number of actual days between two <see cref="Dt"/>s.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next IMM roll date from today.
    ///   Dt immRoll = Dt.ImmNext( today );
    ///
    ///   // Calculate the number days to the next IMM roll date
    ///   int count = Dt.Diff( today, immRoll );
    ///
    ///   Console.WriteLine( "Number of days from today {0} to the next IMM roll {1} is {2}", today, immRoll, count );
    /// </code>
    /// </example>
    public static int Diff(Dt start, Dt end)
    {
      if (!start.IsValid())
        throw new ArgumentOutOfRangeException(
          $"The date {start} is invalid");
      if (!end.IsValid())
        throw new ArgumentOutOfRangeException(
          $"The date {end} is invalid");

      int day1 = start.Day;
      int month1 = start.Month;
      int year1 = start.Year;
      int day2 = end.Day;
      int month2 = end.Month;
      int year2 = end.Year;

      return(int)(DaysToYear(year2) - DaysToYear(year1) +
                  DaysToMonth(month2, IsLeapYear(year2)) - DaysToMonth(month1, IsLeapYear(year1)) +
                  day2 - day1);
    }

    /// <summary>
    ///   Calculate days between two dates
    /// </summary>
    /// <remarks>
    ///   Calculate days from and including <paramref name="start">the start date</paramref> to and excluding
    ///   <paramref name="end">the end date</paramref> given a <paramref name="dc">DayCount</paramref>.
    /// </remarks>
    /// <param name="start">Earlier date</param>
    /// <param name="end">Later date</param>
    /// <param name="dc">Daycount</param>
    /// <returns>number of days between two dates</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the number of days between two <see cref="Dt"/>s based on a <see cref="DayCount"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next IMM roll date from today.
    ///   Dt immRoll = Dt.ImmNext( today );
    ///
    ///   // Calculate the number of 30/360 days to the next IMM roll date
    ///   int count = Dt.Diff( today, immRoll, DayCount.Thirty360 );
    ///
    ///   Console.WriteLine( "Number of 30/360 days from today {0} to the next IMM roll {1} is {2}", today, immRoll, count );
    /// </code>
    /// </example>
    public static int Diff(Dt start, Dt end, DayCount dc)
    {
      if (!start.IsValid())
        throw new ArgumentOutOfRangeException(nameof(start), @"Invalid date" );
      if (!end.IsValid())
        throw new ArgumentOutOfRangeException(nameof(end), @"Invalid date");

      int day1 = start.Day;
      int month1 = start.Month;
      int year1 = start.Year;
      int day2 = end.Day;
      int month2 = end.Month;
      int year2 = end.Year;
      int days = 0;

      switch (dc)
      {
        case DayCount.Thirty360Isma:
          // 30/360 SIA 
          // Ref "Standard Securities Calculation Methods, Fixed Income Securities Formulas for Price, Yield, and Accrued Interest, Volume I", by Jan Mayle, 2007 (Third Edition), ISBN 1-882936-01-9
          // For purchase at http://www.sifma.org/research/bookstore.aspx
          if (IsEndOfFeb(day2, month2, year2) &&
              IsEndOfFeb(day1, month1, year1))
            day2 = 30;
          if (IsEndOfFeb(day1, month1, year1))
            day1 = 30;
          // Note that based on client comparison of cashflows vs. Bloomberg for ISIN XS1028947403, we have modified this:
          // "If after the preceding test the first day is the 30th and the second day is the 31st then the second day is changed to the 30th."
          // to be more generally interpreted as:
          // "If after the preceding test the first day is the 30th and the second day is the 31st *or the last day in February* then the second day is changed to the 30th."
          // This gives an accrual fraction of 0.5 for a semiannual period running from 8/31/xx to 2/28/(xx+1).
          if ( (day2 > 30 || IsEndOfFeb(day2, month2, year2)) && day1 >= 30)
            day2 = 30;
          if (day1 == 31)
            day1 = 30;
          days = 360*(year2 - year1) + 30*(month2 - month1) + (day2 - day1);
          break;
        case DayCount.Thirty360:
          // 30/360 ISDA. Ref 1991 ISDA Definitions
          if (day1 > 30)
            day1 = 30;
          if (day2 > 30 && day1 >= 30)
            day2 = 30;
          days = 360*(year2 - year1) + 30*(month2 - month1) + (day2 - day1);
          break;
        case DayCount.ThirtyE360:
          // Ref. CSFB Guide to Yield Calculations, 1988
          // Ref. ISDA. Ref 1991 ISDA Definitions
          if (day1 > 30)
            day1 = 30;
          if (day2 > 30)
            day2 = 30;
          days = 360*(year2 - year1) + 30*(month2 - month1) + (day2 - day1);
          break;
        case DayCount.ThirtyEP360:
          // 30E+/360. Ref Wikipedia
          if (day1 > 30)
            day1 = 30;
          if (day2 > 30)
          {
            month2++;
            day2 = 1;
          }
          days = 360*(year2 - year1) + 30*(month2 - month1) + (day2 - day1);
          break;
        case DayCount.Actual360:
        case DayCount.Actual365Fixed:
        case DayCount.Actual365L:
        case DayCount.Actual366:
        case DayCount.ActualActualBond:
        case DayCount.ActualActual:
        case DayCount.ActualActualEuro:
          {
            days = (int) (DaysToYear(year2) - DaysToYear(year1) +
                          DaysToMonth(month2, IsLeapYear(year2)) - DaysToMonth(month1, IsLeapYear(year1)) +
                          day2 - day1);
          }
          break;
        case DayCount.OneOne:
        case DayCount.Months:
        case DayCount.None:
          return 0;
      }

      return days;
    }

    /// <summary>
    ///   Calculate days between two dates for daycount, regardless of date order.
    /// </summary>
    /// <param name="start">First date</param>
    /// <param name="end">Second date</param>
    /// <param name="dc">Daycount</param>
    /// <returns>Number of days between <paramref name="start"/> and <paramref name="end"/> for daycount,
    ///          negative if <paramref name="start"/> > <paramref name="end"/></returns>
    public static int SignedDiff(Dt start, Dt end, DayCount dc)
    {
      // Count days, depending on order of dates
      if( start > end )
        return -Diff(end, start, dc);
      else
        return Diff(start, end, dc);
    }

    /// <summary>
    ///   Calculate fraction of days between two dates.
    /// </summary>
    /// <param name="start">Earlier date</param>
    /// <param name="end">Later date</param>
    /// <returns>number of days between <paramref name="start"/> and <paramref name="end"/> as a double</returns>
    public static double FractDiff(Dt start, Dt end)
    {
      int days = Diff( start, end );
      int seconds = ((end.Hour-start.Hour)*60+(end.Minute-start.Minute))*60+(end.Second-start.Second);

      if (days < 0 || (days == 0 && seconds < 0))
      {
        throw new ArgumentException($"Dates out of order ({start} > {end})");
      }

      return(double)days + (double)seconds/86400.0;
    }

    private static double SignedFractDiff(Dt start, Dt end)
    {
      int days = Diff( start, end );
      int seconds = ((end.Hour-start.Hour)*60+(end.Minute-start.Minute))*60+(end.Second-start.Second);
      return(double)days + (double)seconds/86400.0;
    }

    #endregion Diff

    #region Fraction

    /// <summary>
    ///   Calculate the annualised accrual fraction of a period given a daycount.
    /// </summary>
    /// <remarks>
    ///   <para>The DayCount convention dictates the accrual conventions and
    ///   this function returns the fraction of the annual
    ///   coupon accrued from and including <paramref name="start"/> to and
    ///   excluding <paramref name="end"/>.</para>
    /// </remarks>
    /// <note>For more complex daycounts (eg. Act/Act), <see cref="Fraction(Dt, Dt, Dt, Dt, DayCount, Frequency)"/> must be used.</note>
    /// <param name="start">start date of range</param>
    /// <param name="end">end date of range</param>
    /// <param name="dayCount">Daycount</param>
    /// <returns>fraction of period of date range</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the fraction of a period useful for accrual calculations.</para>
    /// <code language="C#">
    ///   // CDS premium is 10bp.
    ///   double premium = 0.001;
    ///
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next CDS premium date
    ///   Dt next = Dt.CDSRoll( today );
    ///
    ///   // Calculate the previous CDS premium date
    ///   Dt prev = Dt.Subtract( next, Frequency.Quarterly, false );
    ///
    ///   // Calculate the accrued premium to today (as a percent of notional).
    ///   double accrued = Dt.Fraction( prev, today, DayCount.Actual360 ) * premium;
    ///   Console.WriteLine("The CDS Accrued is {0}", accrued );
    /// </code>
    /// </example>
    public static double Fraction(Dt start, Dt end, DayCount dayCount)
    {
      // Validate days overlap period
      if( start >= end )
        return 0.0;
      return FastFraction(start, end, start, end, dayCount, Frequency.None);
    }

    /// <summary>
    ///   Calculate the annualised accrual fraction of a period given a daycount.
    /// </summary>
    /// <remarks>
    ///   <para>The DayCount convention dictates the accrual conventions and
    ///   this function returns the fraction of the annual
    ///   coupon accrued from and including <paramref name="start">the accrual start date</paramref>
    ///   to and exluding <paramref name="end">the accrual end date</paramref> over a
    ///   period from the last coupon cycle or <paramref name="pstart">period start date</paramref> to
    ///   the next coupon cycle or <paramref name="pend">period end date</paramref>.</para>
    ///   <para><paramref name="pstart">The period start date</paramref> should be the
    ///   regular period start date in cases of a long or short coupon. For example, for
    ///   a short first coupon of a bond, <paramref name="pstart">the period start date</paramref>
    ///   should be what the regular coupon period start date would have been and the start
    ///   date should be the accrual start date.</para>
    ///   <note>For simple daycounts (eg. 30/360), <paramref name="pstart">the period start date</paramref>
    ///   and <paramref name="pend">the period end date</paramref> are not used.</note>
    /// </remarks>
    /// <param name="pstart">period start date</param>
    /// <param name="pend">period end date</param>
    /// <param name="start">accrual start date</param>
    /// <param name="end">accrual end date</param>
    /// <param name="dayCount">Daycount</param>
    /// <param name="freq">Payment frequency</param>
    /// <returns>fraction of period of date range</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the fraction of a period useful for accrual calculations.</para>
    /// <code language="C#">
    ///   // CDS premium is 10bp.
    ///   double premium = 0.001;
    ///
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next CDS premium date
    ///   Dt next = Dt.CDSRoll( today );
    ///
    ///   // Calculate the previous CDS premium date
    ///   Dt prev = Dt.Subtract( next, Frequency.Quarterly, false );
    ///
    ///   // Calculate the accrued premium to today (as a percent of notional).
    ///   double accrued = Dt.Fraction( prev, next, prev, today, DayCount.Actual360, Frequency.Quarterly ) * premium;
    ///   Console.WriteLine("The CDS Accrued is {0}", accrued );
    /// </code>
    /// </example>
    public static double Fraction(Dt pstart, Dt pend, Dt start, Dt end, DayCount dayCount, Frequency freq)
    {
      // Validate days overlap period
      if( (pstart >= pend) || (start >= end) || (end < pstart) || (start > pend) )
        return 0.0;

      switch (dayCount)
      {
        case DayCount.ActualActualBond:
        case DayCount.ActualActualEuro:
        case DayCount.ActualActual:
        case DayCount.Actual365L:
          {
            // Fraction depends on period dates so deal with overlapping periods, stepping though each period
            double fraction = 0.0;
            if (pstart > start)
            {
              Logger.Debug(" splitting long first period(s)");
              int months = (int) (0.5 + ((double) Diff(pstart, pend))/365.0*12.0); // Do here as not commonly executed
              if (months < 1) months = 1;
              Dt ppstart = pstart;
              do
              {
                Dt ppend = ppstart;
                ppstart = Add(ppend, -months, TimeUnit.Months);
                fraction += FastFraction(ppstart, ppend, (ppstart > start) ? ppstart : start, ppend, dayCount, freq);
              } while (ppstart >= start);
              start = pstart;
            }
            if (pend < end)
            {
              Logger.Debug(" splitting long last period(s)");
              int months = (int) (0.5 + ((double) Diff(pstart, pend))/365.0*12.0); // Do here as not commonly executed
              if (months < 1) months = 1;
              Dt ppend = pend;
              do
              {
                Dt ppstart = ppend;
                ppend = Add(ppstart, months, TimeUnit.Months);
                fraction += FastFraction(ppstart, ppend, ppstart, (ppend < end) ? ppend : end, dayCount, freq);
              } while (ppend <= end);
              end = pend;
            }
            return FastFraction(pstart, pend, start, end, dayCount, freq) + fraction;
          }
        case DayCount.Actual365Fixed:
        case DayCount.Actual360:
        case DayCount.Thirty360Isma:
        case DayCount.ThirtyE360:
        case DayCount.Thirty360:
        case DayCount.Actual366:
        case DayCount.OneOne:
        case DayCount.Months:
        case DayCount.None:
        default:
          return FastFraction(pstart, pend, start, end, dayCount, freq);
      }
    }

    // Local shared function to do work of fraction methods.
    //
    private static double FastFraction(Dt pstart, Dt pend, Dt start, Dt end, DayCount dc, Frequency freq)
    {
      if (!pstart.IsValid() || !pend.IsValid() || !start.IsValid() || !end.IsValid())
        throw new ArgumentOutOfRangeException("Date", "Invalid date");
     

      //logger.DebugFormat( "Dt.fraction({0},{1},{2},{3},{4},{5})", (int)pstart, (int)pend, (int)start, (int)end, dc, freq );

      // Imply frequency if not specified and we need it. This should be replace later by an exception. RTD Oct'11
      if( freq == Frequency.None && (dc == DayCount.ActualActualBond || dc == DayCount.Actual365L) )
      {
          int period = Dt.Diff(pstart, pend);
          int months = (int)(0.5 + ((double)period) / 365.0 * 12.0);
          if (months < 1)
            throw new ArgumentException("Period cannot be shorter than half month.");
          if (months > 12)
            throw new ArgumentException("Period cannot be longer than 12 months.");
        freq = (Frequency)Enum.ToObject(typeof(Frequency), 12/months);
      }

      // Calculate for simple period
      switch (dc)
      {
      case DayCount.ActualActualBond:
        {
					if (Logger.IsDebugEnabled)
						Logger.DebugFormat( " Actual/Actual returning Diff({0},{1}) / (Diff({2},{3}) * {4})",
							start.ToInt(), end.ToInt(), pstart.ToInt(), pend.ToInt(), (int)freq);
          return((double)Diff(start, end) / ((double)Diff(pstart, pend)*(double)freq));
        }
      case DayCount.ActualActualEuro:
        {
          double daysInYear = 365.0; // default denominator
          int startYear = start.Year;
          int endYear = end.Year;
          if (startYear == endYear && IsLeapYear(startYear))
          {
            // Period within one leap year
            int leapDay = new Dt(29, 2, startYear).DayOfYear();
            if (start.DayOfYear() <= leapDay && end.DayOfYear() >= leapDay)
            {
              daysInYear = 366.0;
            }
          }
          else if (IsLeapYear(startYear))
          {
            // Period starts in a leap year
            int leapDay = new Dt(29, 2, startYear).DayOfYear();
            if (start.DayOfYear() <= leapDay)
            {
              daysInYear = 366.0;
            }
          }
          else if (IsLeapYear(endYear))
          {
            // Period ends in a leap year
            int leapDay = new Dt(29, 2, endYear).DayOfYear();
            if (end.DayOfYear() >= leapDay)
            {
              daysInYear = 366.0;
            }
          }

          return( (double)Diff(start, end)/daysInYear );
        }
      case DayCount.ActualActual:
        {
          int days;
          int daysInYear;
          int startYear = start.Year;
          int endYear = end.Year;
          double fraction;

          if (startYear == endYear)
          {
            days = Diff(start, end);
            daysInYear = DaysInYear(startYear);
            fraction = ((double)days)/((double)daysInYear);
						if (Logger.IsDebugEnabled)
							Logger.DebugFormat( " Actual/ActualI (intra-year) returning Diff({0:D8},{1:D8})/{2} = {3}", 
                start.ToInt(), end.ToInt(), daysInYear, fraction );
          }
          else
          {
            // Fractional portion in start year
            Dt endOfStartYear = new Dt(31, 12, startYear);
            daysInYear = DaysInYear(startYear);
            days = Diff(start, endOfStartYear) + 1;
            fraction = ((double)days)/((double)daysInYear);

            // Whole years
            fraction += endYear - startYear - 1;

            // Fractional portion in end year
            Dt startOfEndYear = new Dt(1, 1, endYear);
            daysInYear = DaysInYear(endYear);
            days = Diff(startOfEndYear, end);
            fraction += ((double)days)/((double)daysInYear);
          }

          return fraction;
        }
      case DayCount.Actual365Fixed:
        return(double)Diff(start, end)/365.0;
      case DayCount.Actual360:
        return(double)Diff(start, end)/360.0;
      case DayCount.Thirty360:
      case DayCount.Thirty360Isma:
      case DayCount.ThirtyE360:
      case DayCount.ThirtyEP360:
        return(double)Diff(start, end, dc)/360.0;
      case DayCount.Actual365L:
          {
            int daysInYear;
            if (freq == Frequency.Annual)
            {
              // Test if coupon period covers Feb 29th. Note: this assumes period is <= 1 year
              Dt nextFeb29th = pstart;
              if (IsLeapYear(pstart.Year))
                nextFeb29th = new Dt(29, Base.Month.February, pstart.Year);
              else if (IsLeapYear(pend.Year))
                nextFeb29th = new Dt(29, Base.Month.February, pend.Year);
              if (pstart < nextFeb29th && pend >= nextFeb29th)
                daysInYear = 366;
              else
                daysInYear = 365;
            }
            else
              daysInYear = IsLeapYear(pend.Year) ? 366 : 365;
            return (double) Diff(start, end)/(double) daysInYear;
          }
        case DayCount.Actual366:
        return (double)Diff(start, end)/366.0;
      case DayCount.OneOne:
        return 1.0;
      case DayCount.Months:
        return end.Year - start.Year + (double)(end.Month - start.Month) / 12.0;
      case DayCount.None:
      default:
        return 0.0;
      }
    }

    /// <summary>
    ///   Calculate the number of days in a period.
    /// </summary>
    /// <remarks>
    ///   <para>The DayCount convention dictates the accrual conventions and
    ///   this function effectively returns the number of days accrual in the
    ///   coupon period.</para>
    /// </remarks>
    /// <note>For more complex daycounts (eg. Act/Act), <see cref="FractionDays(Dt, Dt, Dt, Dt, DayCount)"/> must be used.</note>
    /// <param name="start">start date of range</param>
    /// <param name="end">end date of range</param>
    /// <param name="dayCount">Daycount</param>
    /// <returns>number of days of period of date range</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the days of a period useful for accrual calculations.</para>
    /// <code language="C#">
    ///   // CDS premium is 10bp.
    ///   double premium = 0.001;
    ///
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next CDS premium date
    ///   Dt next = Dt.CDSRoll( today );
    ///
    ///   // Calculate the previous CDS premium date
    ///   Dt prev = Dt.Subtract( next, Frequency.Quarterly, false );
    ///
    ///   // Calculate the accrued days to today.
    ///   int days = Dt.FractionDays( prev, today, DayCount.Actual360 );
    ///   Console.WriteLine("The CDS has {0} days accrual", days );
    /// </code>
    /// </example>
    public static int FractionDays(Dt start, Dt end, DayCount dayCount)
    {
      // Validate days overlap period
      if( start >= end )
        return 0;
      return FastFractionDays(start, end, dayCount);
    }

    /// <summary>
    ///   Calculate the number of days in a period.
    /// </summary>
    /// <remarks>
    ///   <para>The DayCount convention dictates the accrual conventions and
    ///   this function effectively returns the number of days accrual
    ///   from and including start to and excluding end over a period from
    ///   the last coupon payment pstart to the next coupon payment pend.</para>
    /// </remarks>
    /// <note>For simple daycounts (eg. 30/360), pstart and pend are not used.</note>
    /// <param name="pstart">period start date</param>
    /// <param name="pend">period end date</param>
    /// <param name="start">start date of range</param>
    /// <param name="end">end date of range</param>
    /// <param name="dayCount">Daycount</param>
    /// <returns>number of days of period of date range</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the days of a period useful for accrual calculations.</para>
    /// <code language="C#">
    ///   // CDS premium is 10bp.
    ///   double premium = 0.001;
    ///
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next CDS premium date
    ///   Dt next = Dt.CDSRoll( today );
    ///
    ///   // Calculate the previous CDS premium date
    ///   Dt prev = Dt.Subtract( next, Frequency.Quarterly, false );
    ///
    ///   // Calculate the accrued days to today.
    ///   int days = Dt.Fraction( prev, next, prev, today, DayCount.Actual360 );
    ///   Console.WriteLine("The CDS has {0} days accrual", days );
    /// </code>
    /// </example>
    public static int FractionDays(Dt pstart, Dt pend, Dt start, Dt end, DayCount dayCount)
    {
      if (!pstart.IsValid())
        throw new ArgumentOutOfRangeException(nameof(pstart), @"Invalid date");
      if (!pend.IsValid())
        throw new ArgumentOutOfRangeException(nameof(pend), @"Invalid date");
      if (!start.IsValid())
        throw new ArgumentOutOfRangeException(nameof(start), @"Invalid date");
      if (!end.IsValid())
        throw new ArgumentOutOfRangeException(nameof(end), @"Invalid date");

      // Validate days overlap period
      if( (pstart >= pend) || (start >= end) || (end < pstart)|| (start > pend) )
        return 0;

      // If overlapping period, split into two periods
      if( pstart > start )
      {
        // Accrual starts before period, split on start of period
        Logger.Debug( " splitting starting overlapping period" );
        return FastFractionDays(start, pstart, dayCount) +
          FastFractionDays(pstart, end, dayCount);
      }
      else if( pend < end )
      {
        // Accrual ends after period, split on end of period
        Logger.Debug(" splitting ending overlapping period");
        return FastFractionDays(start, pend, dayCount) +
          FastFractionDays(pend, end, dayCount);
      }
      else
      {
        return FastFractionDays(start, end, dayCount);
      }
    }

    // Local shared function to do work of fraction days methods.
    //
    private static int FastFractionDays(Dt start, Dt end, DayCount dc)
    {
      //logger.DebugFormat( "Dt.FractionDays({0},{1},{2},{3},{4})", (int)pstart, (int)pend, (int)start, (int)end, dc );

      // Calculate for simple period
      switch (dc)
      {
      case DayCount.ActualActual:
      case DayCount.ActualActualBond:
      case DayCount.ActualActualEuro:
      case DayCount.Actual365Fixed:
      case DayCount.Actual360:
      case DayCount.Actual365L:
      case DayCount.Actual366:
        return( Diff(start, end) );
      case DayCount.Thirty360:
      case DayCount.Thirty360Isma:
      case DayCount.ThirtyE360:
      case DayCount.ThirtyEP360:
        return( Diff(start, end, dc) );
      default:
        throw new ArgumentOutOfRangeException( nameof(dc), @"Invalid daycount" );
      }
    }

    #endregion Fraction

    #region Compare

    /// <summary>
    ///   Compare two dates
    /// </summary>
    /// <param name="date1"> first date</param>
    /// <param name="date2"> second date</param>
    /// <returns> &lt; 0 if date1 &lt;  date2</returns>
    /// <returns> 0 if date1 = date2</returns>
    /// <returns> &gt; 0 if date1 &gt; date2</returns>
    /// <example>
    /// <para>The following sample demonstrates comparing two <see cref="Dt"/>s.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next IMM roll date from today.
    ///   Dt immRoll = Dt.ImmNext( today );
    ///
    ///   // Get standard cds roll date (standard first premium payment date) from today.
    ///   Dt cdsRoll = Dt.CDSRoll( today );
    ///
    ///   if( Dt.Cmp(immRoll, cdsRoll) &lt; 0 )
    ///   {
    ///     Console.WriteLine( "Next IMM {0} is before CDS roll {1} - short first period!", immRoll, cdsRoll );
    ///   }
    /// </code>
    /// </example>
    public static int Cmp(Dt date1, Dt date2)
    {
      int result;

      result = date1.year_ - date2.year_;
      if (result != 0)
        return result;

      result = date1.month_ - date2.month_;
      if (result != 0)
        return result;

      result = date1.day_ - date2.day_;
      if (result != 0)
        return result;

      return date1.minute_ - date2.minute_;
    }

    /// <summary>
    ///   Compare two dates
    /// </summary>
    /// <param name="day1">Day of first date (1-31)</param>
    /// <param name="month1">Month of first date (1-12; January = 1)</param>
    /// <param name="year1">Year of first date (1900-2150)</param>
    /// <param name="day2">Day of second date (1-31)</param>
    /// <param name="month2">Month of second date (1-12; January = 1)</param>
    /// <param name="year2">Year of second date (1900-2150)</param>
    /// <returns> &lt; 0 if date1 &lt;  date2</returns>
    /// <returns> 0 if date1 = date2</returns>
    /// <returns> &gt; 0 if date1 &gt; date2</returns>
    /// <example>
    /// <para>The following sample demonstrates comparing two day/month/years.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next IMM roll date from today.
    ///   Dt immRoll = Dt.ImmNext( today );
    ///
    ///   // Get standard cds roll date (standard first premium payment date) from today.
    ///   Dt cdsRoll = Dt.CDSRoll( today );
    ///
    ///   if( Dt.Cmp(immRoll.Day, immRoll.Month, immRoll.Year, cdsRoll.Day, cdsRoll.Month, cdsRoll.Year) &lt; 0 )
    ///   {
    ///     Console.WriteLine( "Next IMM {0} is before CDS roll {1} - short first period!", immRoll, cdsRoll );
    ///   }
    /// </code>
    /// </example>
    public static int Cmp(int day1, int month1, int year1, int day2, int month2, int year2)
    {
      return( (year1*10000+month1*100+day1) - (year2*10000+month2*100+day2) );
    }

    #endregion Compare

    #region Misc

    /// <summary>
    /// Returns DateTime at start of specified date
    /// </summary>
    /// <returns>Created DateTime at start of specified date</returns>
    public static DateTime StartOfDay(Dt dt)
    {
      return new DateTime(dt.Year, dt.Month, dt.Day);
    }

    /// <summary>
    /// Returns <see cref="DateTime"/> at end of specified date
    /// </summary>
    /// <returns>Created <see cref="DateTime"/> at end of specified date</returns>
    public static DateTime EndOfDay(Dt dt)
    {
      return new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, 999);
    }

    /// <summary>
    /// Finds the earliest date in the list of specified dates ignoring empty/null/cleared dates. If all
    /// dates are clear/empty or no dates are specified then the method return an empty date.
    /// </summary>
    /// <param name="dates">Array of dates</param>
    /// <returns>Minimum date</returns>
    public static Dt Min(params Dt[] dates)
    {
      Dt result = Empty;
      foreach (Dt date in dates)
      {
        if (result.IsEmpty())
          result = date;
        else if (!date.IsEmpty() && date < result)
          result = date;
      }
      return result;
    }

    /// <summary>
    /// Finds the earliest date ignoring empty/null/cleared dates. If both dates are clear then
    /// the method return an empty date.
    /// </summary>
    /// <param name="date1">First date</param>
    /// <param name="date2">Second date</param>
    /// <returns>Earliest date</returns>
    public static Dt Min(Dt date1, Dt date2)
    {
      return Min(date1, date2, Empty);
    }

    /// <summary>
    ///   Julian conversion
    /// </summary>
    /// <returns>Modified Julian date equivalent of date</returns>
    public uint ToJulian()
    {
      bool isLeapYear = IsLeapYear(year_ + YearBase);
      if (day_ > DaysInMonth(month_, isLeapYear))
        throw new ArgumentOutOfRangeException(string.Format("The day is out of the range"));
      return DaysToByteYear(year_) + DaysToMonth(month_, isLeapYear) + day_ + 15020;
    }

    /// <summary>
    ///   Convert to DateTime
    /// </summary>
    public DateTime ToDateTime()
    {
      return (IsEmpty()) ? new DateTime() : new DateTime(year_ + YearBase, month_, day_, minute_/6, (minute_%6)*10, 0);
    }

    /// <summary>
    ///  Calculates the relative time in years between two dates,
    ///  based on the convention of 365.25 days per year
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <returns>RelativeTime.</returns>
    public static RelativeTime RelativeTime(Dt start, Dt end)
    {
      return new RelativeTime(start, end);
    }

    /// <summary>
    ///   Return time from date
    /// </summary>
    /// <remarks>
    ///   Time is days / 365
    /// </remarks>
    /// <param name="start">Starting reference date</param>
    /// <param name="dt">date</param>
    /// <returns>Time in years from start date to dt</returns>
    public static double TimeInYears(Dt start, Dt dt)
    {
      return FractDiff(start, dt) / 365.0;
    }

    /// <summary>
    ///   Get today's date
    /// </summary>
    /// <remarks>
    ///   This call caches the current date
    ///   for performance.
    ///   The time is set to midnight.
    ///   Use now() if you need an uncached date and time.
    /// </remarks>
    /// <returns>today's date (time is midnight)</returns>
    /// <example>
    /// <para>The following sample demonstrates getting the current date.</para>
    /// <code language="C#">
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    /// </code>
    /// </example>
    public static Dt Today()
    {
      DateTime date = DateTime.Now;
      Dt dt = new Dt(date.Day, date.Month, date.Year, 0, 0, 0);
      return dt;
    }

    /// <summary>
    ///   Determine if year is a leap year.
    /// </summary>
    /// <remarks>
    ///   <para>Acording to the Gregorian calendar, every fourth year is
    ///   a leap year except for century years that are not divisible
    ///   by 400.</para>
    ///   <para>This uses a fast test valid from 1900-2100.</para>
    /// </remarks>
    /// <returns>true if this date is in a leap year</returns>
    /// <example>
    /// <para>The following sample demonstrates testing if a <see cref="Dt"/> is in a leap year.</para>
    /// <code language="C#">
    ///   int year;
    ///
    ///   // ...
    ///
    ///   // Test if year is a leap year
    ///   if( Dt.IsLeapYear(year) )
    ///   {
    ///     Console.WriteLine("{0} is a leap year", year);
    ///   }
    ///   else
    ///   {
    ///     Console.WriteLine("{0} is not a leap year", year);
    ///   }
    /// </code>
    /// </example>
    public bool IsLeapYear()
    {
      return IsLeapYear(Year);
    }

    /// <summary>
    ///   Determine if year is a leap year.
    /// </summary>
    /// <remarks>
    ///   <para>Acording to the Gregorian calendar, every fourth year is
    ///   a leap year except for century years that are not divisible
    ///   by 400.</para>
    ///   <para>This uses a fast test valid from 1900-2100.</para>
    /// </remarks>
    /// <param name="year">Year</param>
    /// <returns>true if <paramref name="year"/> is a leap year</returns>
    /// <example>
    /// <para>The following sample demonstrates testing if a year is in a leap year.</para>
    /// <code language="C#">
    ///   int year;
    ///
    ///   // ...
    ///
    ///   // Test if year is a leap year
    ///   if( Dt.IsLeapYear(year) )
    ///   {
    ///     Console.WriteLine("{0} is a leap year", year);
    ///   }
    ///   else
    ///   {
    ///     Console.WriteLine("{0} is not a leap year", year);
    ///   }
    /// </code>
    /// </example>
    public static bool IsLeapYear(int year)
    {
      // Accurate test is: ( !(year % 4) && ((year % 100) || !(year % 400)) )
      // below is a abbreviated fast test valid from 1900-2150.
      //
      return (year % 4) == 0 && year != 1900 && year != 2100;
    }

    /// <summary>
    ///   Get if date is end of Febuary.
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <returns>true if date is in leap year</returns>
    /// <example>
    /// <para>The following sample demonstrates testing if a day/month/year is the last day of February.</para>
    /// <code language="C#">
    ///   int day, month, year;
    ///
    ///   // ...
    ///
    ///   // Test if day/month/year is the last day of February
    ///   if( Dt.IsEndOfFeb(day, month, year) )
    ///   {
    ///     Console.WriteLine("{0}/{1}/{2} is the last day of February", day, month, year );
    ///   }
    /// </code>
    /// </example>
    public static bool IsEndOfFeb(int day, int month, int year)
    {
      return( month==2 && (day==29 || (!IsLeapYear(year) && day==28)) );
    }

    /// <summary>
    ///   Get day of week for date.
    /// </summary>
    /// <returns>day of week of date</returns>
    /// <example>
    /// <para>The following sample demonstrates getting the <see cref="Base.DayOfWeek"/> for this <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get day of week for today
    ///   DayOfWeek dow = today.DayOfWeek();
    ///
    ///   Console.WriteLine("Day of week of {0} is {1}", today, dow );
    /// </code>
    /// </example>
    public DayOfWeek DayOfWeek()
    {
      return DayOfWeek(Day, Month, Year);
    }

    /// <summary>
    ///   Get day of week for date.
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <returns>day of week of date</returns>
    /// <note>Date not validated.</note>
    /// <example>
    /// <para>The following sample demonstrates getting the <see cref="Base.DayOfWeek"/> for a day/month/year.</para>
    /// <code language="C#">
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get day of week for today
    ///   DayOfWeek dow = Dt.DayOfWeek(today.Day, today.Month, today.Year);
    ///
    ///   Console.WriteLine("Day of week of {0} is {1}", today, dow );
    /// </code>
    /// </example>
    public static DayOfWeek DayOfWeek(int day, int month, int year)
    {
      int daysFromBase = Diff(new Dt(1, 1, YearBase), new Dt(day, month, year));
      return (DayOfWeek)(daysFromBase % 7);
    }

    /// <summary>
    /// Get day of month
    /// </summary>
    /// <param name="dom">Day of month specification</param>
    /// <param name="month">Month</param>
    /// <param name="year">Year</param>
    /// <param name="bdc">Business day roll convention</param>
    /// <param name="cal">Calendar for any busines day roll</param>
    /// <returns>Day of month</returns>
    public static Dt DayOfMonth(int month, int year, DayOfMonth dom, BDConvention bdc, Calendar cal)
    {
      int day;
      if (dom >= Base.DayOfMonth.First && dom <= Base.DayOfMonth.Thirtieth)
        // specified day of month
        day = (int)dom;
      else if (dom >= Base.DayOfMonth.FirstMonday && dom <= Base.DayOfMonth.ThirdFriday)
      {
        // nth week
        var n = (dom - Base.DayOfMonth.FirstMonday) % 3 + 1;
        var dow = Base.DayOfWeek.Monday + (dom - Base.DayOfMonth.FirstMonday)/3;
        day = NthWeekDayOfMonth(month, year, n, dow);
      } else if( dom == Base.DayOfMonth.Last )
        day = (int)DaysInMonth(month, year);
      else
        throw new ArgumentException($"Invalid DayOfMonth {dom}");
      var dt = new Dt(day, month, year);
      return Dt.Roll(dt, bdc, cal);
    }

    /// <summary>
    ///   Get day of year of date.
    /// </summary>
    /// <returns>day of year for date</returns>
    /// <example>
    /// <para>The following sample demonstrates getting the day of the year for a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get day of year for today
    ///   int doy = today.DayOfYear();
    ///
    ///   Console.WriteLine("Day of year of {0} is {1}", today, doy );
    /// </code>
    /// </example>
    public int DayOfYear()
    {
      return DayOfYear(Day, Month, Year);
    }

    /// <summary>
    ///   Get day of year of date.
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <returns>day of year for date</returns>
    /// <example>
    /// <para>The following sample demonstrates getting the day of the year for a day/month/year.</para>
    /// <code language="C#">
    ///   // Get todays date.
    ///   Dt today = Dt.Today();
    ///
    ///   // Get day of year for today
    ///   int doy = today.DayOfYear( today.Day, today.Month, today.Year );
    ///
    ///   Console.WriteLine("Day of year of {0} is {1}", today, doy );
    /// </code>
    /// </example>
    public static int DayOfYear(int day, int month, int year)
    {
      return day + (int)DaysToMonth(month, IsLeapYear(year));
    }

    private static readonly int[] easterMonday_ = {
      107,  98,  90, 103,  95, 114, 106,  91, 111, 102,   // 1900-1909
      87,  107,  99,  83, 103,  95, 115,  99,  91, 111,   // 1910-1919
      96,   87, 107,  92, 112, 103,  95, 108, 100,  91,   // 1920-1929
      111,  96,  88, 107,  92, 112, 104,  88, 108, 100,   // 1930-1939
      85,  104,  96, 116, 101,  92, 112,  97,  89, 108,   // 1940-1949
      100,  85, 105,  96, 109, 101,  93, 112,  97,  89,   // 1950-1959
      109,  93, 113, 105,  90, 109, 101,  86, 106,  97,   // 1960-1969
      89,  102,  94, 113, 105,  90, 110, 101,  86, 106,   // 1970-1979
      98,  110, 102,  94, 114,  98,  90, 110,  95,  86,   // 1980-1989
      106,  91, 111, 102,  94, 107,  99,  90, 103,  95,   // 1990-1999
      115, 106,  91, 111, 103,  87, 107,  99,  84, 103,   // 2000-2009
      95,  115, 100,  91, 111,  96,  88, 107,  92, 112,   // 2010-2019
      104,  95, 108, 100,  92, 111,  96,  88, 108,  92,   // 2020-2029
      112, 104,  89, 108, 100,  85, 105,  96, 116, 101,   // 2030-2039
      93,  112,  97,  89, 109, 100,  85, 105,  97, 109,   // 2040-2049
      101,  93, 113,  97,  89, 109,  94, 113, 105,  90,   // 2050-2059
      110, 101,  86, 106,  98,  89, 102,  94, 114, 105,   // 2060-2069
      90,  110, 102,  86, 106,  98, 111, 102,  94, 107,   // 2070-2079
      99,   90, 110,  95,  87, 106,  91, 111, 103,  94,   // 2080-2089
      107,  99,  91, 103,  95, 115, 107,  91, 111, 103,   // 2090-2099
      88,  108, 100,  85, 105,  96, 109, 101,  93, 112,   // 2100-2109
      97,   89, 109,  93, 113, 105,  90, 109, 101,  86,   // 2110-2119
      106,  97,  89, 102,  94, 113, 105,  90, 110, 101,   // 2120-2129
      86,  106,  98, 110, 102,  94, 114,  98,  90, 110,   // 2130-2139
      95,   86, 106,  91, 111, 102,  94, 107,  99,  90    // 2140-2149
    };

    /// <summary>
    ///   Returns day of year of easter monday
    /// </summary>
    /// <param name="year">Year (1900-2150)</param>
    /// <returns>day of year of easter monday</returns>
    public static int EasterMonday(int year)
    {
      return easterMonday_[year-1900];
    }

    /// <summary>
    ///   Calculate nth business day of month and year.
    /// </summary>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <param name="n">Number of business days from start of month</param>
    /// <param name="cal">Calendar to use</param>
    /// <returns>nth business day in month and year</returns>
    public static Dt NthDay(int month, int year, int n, Calendar cal)
    {
      return AddDays( new Dt(1, month, year), n-1, cal);
    }

    /// <summary>
    ///   Calculate last day of month and year
    /// </summary>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <returns>last day of month and year</returns>
    public static Dt LastDay(int month, int year)
    {
      var day = (int)DaysInMonth(month, year);
      return new Dt(day, month, year);
    }

    /// <summary>
    /// Calculate the day of month of the nth week of month and year (eg. 2nd monday)
    /// </summary>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <param name="n">Number of week day</param>
    /// <param name="dow">Day of week</param>
    /// <returns>Specified day of week in <paramref name="n"/>th week of month and year</returns>
    public static int NthWeekDayOfMonth(int month, int year, int n, DayOfWeek dow)
    {
      // Get dow of first day in month.
      var first = (int)(new Dt(1, month, year).DayOfWeek());
      var dowi = (int)dow;
      // Calculate offset to nth day of week
      int day;
      if( first > dowi )
      {
        day = (7 + dowi) - first + 1 + 7*(n-1);
      }
      else
      {
        day = dowi - first + 1 + 7*(n-1);
      }
      return day;
    }

    /// <summary>
    ///   Calculate the day of the nth week of month and year (eg. 2nd monday)
    /// </summary>
    /// <param name="month">Month of year (1-12; January = 1)</param>
    /// <param name="year">Year (1900-2150)</param>
    /// <param name="n">Number of week day</param>
    /// <param name="dow">Day of week</param>
    /// <returns>Specified day of week in <paramref name="n"/>th week of month and year</returns>
    public static Dt NthWeekDay(int month, int year, int n, DayOfWeek dow)
    {
      var day = NthWeekDayOfMonth(month, year, n, dow);
      return new Dt(day, month, year);
    }

    /// <summary>
    ///   Roll settlement date
    /// </summary>
    /// <remarks>
    ///   If date is not a valid settlement date, rolls date to next valid
    ///   settlement date based on roll convention.
    /// </remarks>
    /// <param name="date">Date to roll from</param>
    /// <param name="bdc">Business day convention</param>
    /// <param name="calendar">Calendar for business days</param>
    /// <returns>modified date</returns>
    /// <example>
    /// <para>The following sample demonstrates implementing the ISDA Business Day Convention for dealing with non-business day.</para>
    /// <code language="C#">
    ///   // Get todays date.
    ///   Dt holiday = new Dt(25, 12, 2004);
    ///
    ///   // Find the next NY business day
    ///   Dt nextBD Dt.Roll( holiday, BDConvention.Following, Calendar.NYB );
    ///   Console.WriteLine("Next NY Business day after {0} is {1}", holiday, nextBD );
    ///
    ///   // Find the previous NY business day
    ///   Dt prevBD Dt.Roll( holiday, BDConvention.Preceding, Calendar.NYB );
    ///   Console.WriteLine("Previous NY Business day before {0} is {1}", holiday, prevBD );
    /// </code>
    /// </example>
    public static Dt Roll(Dt date, BDConvention bdc, Calendar calendar)
    {
      if (bdc == BDConvention.None)
        return date;
      if (date.IsValidSettlement(calendar))
        return date;
      // Need to roll
      switch (bdc)
      {
        case BDConvention.Following:
          return AddDays(date, 1, calendar);
        case BDConvention.Modified:
        case BDConvention.FRN:
        {
          Dt dt = AddDays(date, 1, calendar);
          if (dt.Month != date.Month)
            // Rolled over end of month, move back into month
            return AddDays(dt, -1, calendar);
          else
            return dt;
        }
        case BDConvention.Preceding:
          return AddDays(date, -1, calendar);
        case BDConvention.ModPreceding:
        {
          Dt dt = AddDays(date, -1, calendar);
          if (dt.Month != date.Month)
            // Rolled back over start of month, move back into month
            return AddDays(dt, 1, calendar);
          else
            return dt;
        }
        default:
          return date;
      } // switch
    }

    /// <summary>
    ///   Convert to integer of form YYYYMMDD
    /// </summary>
    public int ToInt()
    {
      return this.Year * 10000 + this.Month * 100 + this.Day;
    }

    /// <summary>
    ///   Time conversion
    /// </summary>
    /// <remarks>
    ///   <para>Time is days from Jan 1, 1900 / 365.</para>
    ///   <para>Subtleties exist when converting to and from continuous time. For
    ///   Consistency, always convert dates using the Time operator
    ///   and use the difference between two Times to calculate in
    ///   continuous time.</para>
    /// </remarks>
    public double ToDouble()
    {
      return FractDiff( new Dt(1, 1, YearBase), this ) / 365.0;
    }

    /// <summary>
    ///   Make a clone of double array
    /// </summary>
    /// <param name="a">Array of Dt</param>
    public static Dt[] CloneArray( Dt[] a )
    {
      var b = new Dt[ a.Length ];
      for( var i = 0; i < a.Length; ++i )
        b[i] = a[i];
      return b;
    }

    #endregion Misc

    #region Excel Date Methods

    /// <summary>
    ///   Convert from date to numeric value that can be used by Excel
    /// </summary>
    /// <remarks>
    ///   Integral part is days from 1/1/1900.
    /// </remarks>
    /// <param name="dt">Date object</param>
    /// <returns>real value that can used by Excel (integral part is days from 1/1/1900)</returns>
    /// <note>A known feature of Excel is that it treats 1900 as a leap year</note>
    public static double ToExcelDate(Dt dt)
    {
      if (dt < new Dt(1, 1, 1901))
      {
        throw new ArgumentOutOfRangeException(nameof(dt), @"dates prior to 1/1/1901 not supported");
      }
      return 2.0 + FractDiff(new Dt(1, 1, 1900), dt);
    }

    /// <summary>
    ///   Convert from an array of dates to array of numeric values that can be used by Excel
    /// </summary>
    /// <param name="dts">An array of Date objects</param>
    /// <returns></returns>
    public static double[] ToExcelDates(Dt[] dts)
    {
      double[] doubleDates = new double[dts.Length];
      for (int i = 0; i < dts.Length; ++i)
        doubleDates[i] = ToExcelDate(dts[i]);
      return doubleDates;
    }

    /// <summary>
    ///   Convert real value from Excel (days from 1st January, 1900) to date
    /// </summary>
    /// <param name="numeric">the real value provided from Excel (XllOper, XLOPER12, or other source)</param>
    /// <param name="dt">Returned Dt</param>
    /// <note>A known feature of Excel is that it treats 1900 as a leap year</note>
    /// <returns>True if conversion successful</returns>
    public static bool TryFromExcelDate(double numeric, out Dt dt)
    {
      if (numeric.IsAlmostSameAs(0.0))
      {
        // Special case of no date
        dt = Empty;
        return true;
      }
      if (numeric < 367.0 || numeric >= 91313.0)
      {
        // Date out of range
        dt = Empty;
        return false;
      }
      int days = (int)Math.Floor(numeric);
      Dt date = Add(new Dt(1, 1, YearBase, 0, 0, 0), days - 2);
      dt = new Dt(date.Day, date.Month, date.Year, 0, (int)(24 * 60 * (numeric - days) + 0.5 / 60), 0);
      return true;
    }

    /// <summary>
    ///   Convert real value from Excel (days from 1st January, 1900) to date
    /// </summary>
    /// <param name="numeric">the real value provided from Excel (XllOper, XLOPER12, or other source)</param>
    /// <note>A known feature of Excel is that it treats 1900 as a leap year</note>
    /// <returns>Date converted</returns>
    public static Dt FromExcelDate(double numeric)
    {
      Dt dt;
      if (!TryFromExcelDate(numeric, out dt))
        throw new ArgumentOutOfRangeException(nameof(numeric), @"dates prior to 1 Jan, 1901 or on or after 31 Dec, 2149 not supported");
      return dt;
    }

    /// <summary>
    ///   Convert real values from Excel (days from 1st January, 1900) to dates
    /// </summary>
    /// <param name="numerics">the real values provided from Excel (XllOper, XLOPER12, or other source)</param>
    /// <note>A known feature of Excel is that it treats 1900 as a leap year</note>
    /// <returns>Dates converted</returns>
    public static Dt[] FromExcelDates(double[] numerics)
    {
      if (numerics == null || numerics.Length == 0)
        return null;
      Dt[] dates = new Dt[numerics.Length];
      for (int i = 0; i < numerics.Length; ++i)
        dates[i] = FromExcelDate(numerics[i]);
      return dates;
    }

    #endregion Excel Date Methods

    #region CDS Date Methods

    /// <summary>
    ///   Calculate standard CDS maturity given a tenor.
    /// </summary>
    /// <remarks>
    ///   <para>Adds the tenor to the as-of date and returns the next IMM date after that.</para>
    /// </remarks>
    /// <param name="effective">CDS protection start date</param>
    /// <param name="tenor">Tenor of CDS</param>
    /// <returns>Standard CDS maturity date given tenor and settlement</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a tenor to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt effective = Dt.Today() + 1;
    ///
    ///   // Get standard maturity date for 5Yr CDS from today.
    ///   Tenor t = new Tenor( 5, TimeUnit.Years );
    ///   Dt cdsMaturity = Dt.cdsMaturity( effective, t );
    /// </code>
    /// </example>
    public static Dt CDSMaturity(Dt effective, Tenor tenor)
    {
      return CDSMaturity(effective, tenor.N, tenor.Units);
    }

    /// <summary>
    ///   Calculate CDX maturity given a tenor.
    /// </summary>
    /// <remarks>
    ///   <para>Adds the tenor to the as-of date and returns the next IMM date after that.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="tenor">Tenor of CDX</param>
    /// <returns>CDX maturity date given tenor and settlement</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a tenor to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get standard maturity date for 5Yr CDX from today.
    ///   Tenor t = new Tenor( 5, TimeUnit.Years );
    ///   Dt cdxMaturity = Dt.CdxMaturity( today, t );
    /// </code>
    /// </example>
    public static Dt CDXMaturity(Dt asOf, Tenor tenor)
    {
      Dt date = Add(asOf, tenor.N, tenor.Units);
      int month = date.Month;
      int year = date.Year;
      if (date.Day >= 20)
        month++;
      month = ((month + 2) / 3) * 3;
      if (month > (int)Base.Month.December)
      {
        year++;
        month -= 12;
      }

      return new Dt(20, month, year);
    }

    /// <summary>
    ///   Calculate standard CDS maturity given a tenor.
    /// </summary>
    /// <remarks>
    ///   <para>Adds the tenor to the as-of date and returns the next CDS IMM date after that.</para>
    /// </remarks>
    /// <param name="effective">The protection start date</param>
    /// <param name="n">Number of time units to maturity of CDS</param>
    /// <param name="timeUnit">Time units to maturity of CDs</param>
    /// <returns>Standard CDS maturity date given time units and settlement</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating a standard CDS maturity date.</para>
    /// <code language="C#">
    ///   // Get the protection start date
    ///   Dt effective = Dt.Today() + 1;
    ///
    ///   // Get standard maturity date for 5Yr CDS from today.
    ///   Dt cdsMaturity = Dt.CDSMaturity( effective, 5, TimeUnit.Years );
    /// </code>
    /// </example>
    public static Dt CDSMaturity(Dt effective, int n, TimeUnit timeUnit)
    {
      return IsCdsRoll6M(effective)
         ? CdsMaturityRoll6M(effective, n, timeUnit)
         : CdsMaturityRoll3M(effective, n, timeUnit);
    }

    /// <summary>
    ///  Calculate the standard, on the run, CDS maturity based on
    ///  3 month rolling of contracts, the convention effective 
    ///  before December 21, 2015.
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="n">Number of time units to maturity of CDS</param>
    /// <param name="timeUnit">Time units to maturity of CDs</param>
    /// <returns>Standard CDS maturity date given time units and settlement</returns>
    public static Dt CdsMaturityRoll3M(Dt asOf, int n, TimeUnit timeUnit)
    {
      Dt date = Add(asOf, n, timeUnit);
      int month = date.Month;
      int year = date.Year;
      if (date.Day > 20 || (!ToolkitBaseConfigurator.Settings.Dt.RollFollowingCDSDate) && date.Day == 20)
        month++;
      month = ((month + 2) / 3) * 3;
      if (month > (int)Base.Month.December)
      {
        year++;
        month -= 12;
      }

      return new Dt(20, month, year);
    }

    /// <summary>
    ///  Calculate the standard, on the run, CDS maturity based on
    ///  6 month rolling of contracts, the convention effective 
    ///  since December 21, 2015.
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="n">Number of time units to maturity of CDS</param>
    /// <param name="unit">Time units to maturity of CDS</param>
    /// <returns>The standard maturity date</returns>
    public static Dt CdsMaturityRoll6M(Dt asOf, int n, TimeUnit unit)
    {
      // Find the days representing the tenor.
      int tenorDays = new Tenor(n, unit).Days;

      // Tenor > 0 and < 3M: use 3M rolling for full backward compatibility.
      if (tenorDays > 0 && tenorDays < 90)
      {
        return CdsMaturityRoll3M(asOf, n, unit);
      }

      // Find the maturity month.
      int months = GetLastRollMonth(asOf.Month, asOf.Day)
        + (1 + RounUpToQuarters(n, unit))*3;
      System.Diagnostics.Debug.Assert(tenorDays == 0 ||
        months > asOf.Month || (months == asOf.Month && 20 >= asOf.Day));

      // This happens with 0M contract on dates before March 20.
      if (months == 0) return new Dt(20, 12, asOf.Year - 1);

      // Regular case
      months -= 1;
      return new Dt(20, 1 + months%12, asOf.Year + months/12);
    }

    /// <summary>
    ///  Get the last rolling month before the specified month and day.
    /// </summary>
    /// <returns>
    ///   3 possible values: -3, September last year; 3, March; 9, September.
    /// </returns>
    private static int GetLastRollMonth(int month, int day)
    {
      if ((month == 3 || month == 9) && (day > 20 || (day == 20 &&
        !ToolkitBaseConfigurator.Settings.Dt.RollFollowingCDSDate)))
      {
        return month;
      }
      return month > 9 ? 9 : (month > 3 ? 3 : -3);
    }

    private static int RounUpToQuarters(int n, TimeUnit unit)
    {
      switch(unit)
      {
      case TimeUnit.None:
        return 0;
      case TimeUnit.Days:
        return (n + 90) / 91;
      case TimeUnit.Weeks:
        return (n + 12) / 13;
      case TimeUnit.Months:
        return (n + 2) / 3;
      case TimeUnit.Years:
        return n * 4;
      }
      throw new ArgumentException($"Invalid time unit: {unit}");
    }

    private static bool IsCdsRoll6M(Dt asOf)
    {
      int date = ToolkitBaseConfigurator.Settings.Dt.StdCdsRollCutoverDate,
        year = date/10000;
      if (asOf.Year > year) return true;
      if (asOf.Year < year) return false;
      int month = (date/100)%100;
      if (asOf.Month > month) return true;
      if (asOf.Month < month) return false;
      return asOf.Day >= (date%100);
    }

    /// <summary>
    /// Calculate Accrual Begin Date for a SNAC contract from a given date
    /// </summary>
    /// <remarks>
    ///   <para>Takes a pricing as-of date and returns the first business day from previous CDS IMM date. This is based on
    ///   the standard defined ISDA terms:</para>
    ///   <list type="bullet">
    ///   <listheader><description>From Standard North American Corporate CDS Contract Specification:</description></listheader>
    ///     <item><term>Business Day Calendar</term><description>currency dependent</description></item>
    ///     <item><term>Adjusted CDS Dates</term><description>CDS Dates, business day adjusted Following</description></item>
    ///     <item><term>Accrual Begin Date</term><description>latest Adjusted CDS Date on or before T+1 calendar</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="calendar">Calendar to use for BD roll. This should be based on the currency of the standard CDS</param>
    /// <returns>Standard CDS effective date for given date and calendar</returns>
    public static Dt SNACFirstAccrualStart(Dt asOf, Calendar calendar)
    {
      // T+1
      Dt Tplus1 = Add(asOf, 1, TimeUnit.Days);

      // find first unadjusted CDS Date after T+1
      int month = Tplus1.Month;
      int year = Tplus1.Year;

      if (Tplus1.Day >= Roll(new Dt(20, month, year), BDConvention.Following, calendar).Day)
        month++;
      month = ((month + 2) / 3) * 3;
      if (month > (int)Base.Month.December)
      {
        year++;
        month -= 12;
      }
      Dt firstCDSDateAfterTplus1 = new Dt(20, month, year);

      // latest unadjusted CDS Date on or before T+1
      Dt prevCDSDate = AddMonth(firstCDSDateAfterTplus1, -3, false);
      // latest Adjusted CDS Date on or before T+1 calendar
      Dt accrualBegin = Roll(prevCDSDate, BDConvention.Following, calendar);

      return accrualBegin;

    }

    /// <summary>
    ///   Calculate standard CDS maturity given a tenor as a string.
    /// </summary>
    /// <remarks>
    ///   Adds the tenor to the as-of date and returns the next IMM date after that.
    /// </remarks>
    /// <param name="effective">The protection start date</param>
    /// <param name="str">CDS maturity tenor as a string</param>
    /// <returns>Standard CDS maturity date given tenor and settlement</returns>
    /// <example>
    /// <para>The following sample demonstrates adding a tenor as a string to a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt effective = Dt.Today() + 1;
    ///
    ///   // Get standard maturity date for 5Yr CDS from today.
    ///   Dt cdsMaturity = Dt.CDSMaturity( effective, "5 Years" );
    /// </code>
    /// </example>
    public static Dt CDSMaturity(Dt effective, string str)
    {
      if (str.Length <= 0)
        return effective;

      Tenor tenor = Tenor.Parse(str);
      return CDSMaturity(effective, tenor.N, tenor.Units);
    }

    /// <summary>
    ///   The method takes an effective date and array of cds maturities, 
    ///   and returns an array of tenors
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturities">Array of cds maturities</param>
    /// <returns>Array of cds/cdx tenors</returns>
    public static string[] GetTenorsFromCDXMaturities(Dt effective, Dt[] maturities)
    {
      if (maturities == null || maturities.Length == 0)
        throw new ArgumentException("Must provide at least one maturity");
      string[] tenors = new string[maturities.Length];
      string[] allTenors = new string[] {"3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", 
                                         "6Y", "7Y", "8Y", "9Y", "10Y", 
                                         "11Y", "12Y", "13Y", "14Y", "15Y",
                                         "16Y", "17Y", "18Y", "19Y", "20Y",
                                         "21Y","22Y","23Y","24Y","25Y",
                                         "26Y","27Y","28Y","29Y","30Y"};
      int pos = 0;
      for (int t = 0; t < maturities.Length; ++t)
      {
        for (; pos < allTenors.Length; ++pos)
        {
          Dt mat = Dt.CDXMaturity(effective, Tenor.Parse(allTenors[pos]));
          if (maturities[t].CompareTo(mat) == 0)
          {
            tenors[t] = allTenors[pos];
            break;
          }
        }
      }
      return tenors;
    }

    /// <summary>
    ///   Calculate the next CDS roll date appropriate for the standard first payment date for a standard CDS.
    /// </summary>
    /// <remarks>
    ///   <para>The stadard CDS contract irgores the one-month coupon rule. No matter the next IMM date is more than 
    ///         or less than one month after the specified date, theis method always returns the next IMM date.</para>
    ///   <para>Note that this differs from CDSMaturity in that the roll occurs one month
    ///   before the next IMM date following the market convention for the first premium
    ///   payment date</para>
    /// </remarks>
    /// <param name="date">Date</param>
    /// <returns>Standard CDS roll after the specified date</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the next CDS roll after a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get standard cds roll date (standard first premium payment date) from today.
    ///   Dt cdsRoll = Dt.CDSRoll( today );
    /// </code>
    /// </example>
    public static Dt CDSRoll(Dt date)
    {
      return CDSRoll(date, false);
    }

    /// <summary>
    ///  Calculate the next CDS roll date appropriate for the first payment date for a CDS.
    ///  When the Boolean parameter is true the standard CDS roll convention is applied namely
    ///  no 30-day first coupon rule. When the parameter is false the 30-day coupon rule is 
    ///  used. 
    /// </summary>
    /// <param name="date">Specified date</param>
    /// <param name="isStandard">True/False for standard/Nonstandard CDS roll convention</param>
    /// <returns>CDS roll date after the specified date</returns>
    public static Dt CDSRoll(Dt date, bool isStandard)
    {
      if (!date.IsEmpty()) date.Validate();

      int month = date.Month;
      int year = date.Year;

      if (!isStandard)
        month++;
      if (date.Day >= 20)
        month++;
      month = ((month + 2) / 3) * 3;
      if (month > (int)Base.Month.December)
      {
        year++;
        month -= 12;
      }

      return new Dt(20, month, year);
    }

    #endregion CDS Date Methods

    #region Exchange Dates

    ///<summary>
    /// The method finds the BBA maturity date following calendar and BDConvention, as well as the end-end basis
    ///</summary>
    ///<param name="valueDt">Value date</param>
    ///<param name="period">Period of maturity</param>
    ///<param name="cal">Calendar</param>
    ///<param name="roll">BD Convention</param>
    ///<returns>BBA maturity date</returns>
    // This function is not general and should be removed. RD Aug'14
    public static Dt LiborMaturity(Dt valueDt, Tenor period, Calendar cal, BDConvention roll)
    {
      if (IsLastBusinessDayOfMonth(valueDt, cal))
      {
        Dt maturity = Add(valueDt, period);
        maturity = new Dt((int)DaysInMonth(maturity.Month, maturity.Year), maturity.Month, maturity.Year);
        while (!maturity.IsValidSettlement(cal))
          maturity = Add(maturity, -1);
        return maturity;
      }
      else
      {
        return Roll(Add(valueDt, period), roll, cal);
      }
    }

    /// <summary>
    /// Test if the exchange code for an exchange traded contract looks correct
    /// </summary>
    /// <remarks>
    ///   <para>This method tests if the format looks reasonable. The exchange date code may
    ///   also only be reasonable relative to a particular date.</para>
    ///   <para>Examples of exchange codes include EDZ8, EDZ08, EDZ2008, and FEU3Q14.
    ///   MMMYY format is also accepted.</para>
    /// </remarks>
    /// <param name="code">exchange date code</param>
    /// <returns>Contract date for specified exchange date code</returns>
    /// <example>
    /// <para>The following sample demonstrates test a particular CME futures code</para>
    /// <code language="C#">
    ///   // Test the Dec 2008 futures code
    ///   if( ImmIsValid( "EDZ8" )
    ///     Console.Writeline("Look ok");
    /// </code>
    /// </example>
    public static bool ExchangeCodeIsValid(string code)
    {
      // First check if null
      if (code == null)
        return false;

      // Check for MMMYY (e.g. DEC08)
      var regex = new Regex(@"^(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)([0-9]{2})$", RegexOptions.IgnoreCase);
      var match = regex.Match(code);
      if (match.Success)
        return true;

      // Check for contract identifier (e.g. EDZ8, EDZ08, EDZ2008, FEU3Q14)
      regex = new Regex(@"^(\w*)([FGHJKMNQUVXZ])([0-9]{1,4})$", RegexOptions.IgnoreCase);
      match = regex.Match(code);
      return match.Success;
    }

    /// <summary>
    /// Parse the month and year from an exchange expiration code (e.g. Z18)
    /// </summary>
    /// <remarks>
    ///   <para>The format for an exchange expiration is [month code][year].</para>
    ///   <para>Examples of exchange expirations include Z8, Z08, Z2008, and 3Q14.
    ///   MMMYY format such as Jan14 is also accepted.</para>
    ///   <para>Slightly faster than <see cref="ParseMonthYearFromExchangeCode(Dt,string,out string,out int,out int)"/>
    ///   for when we just have the exchange expiration.</para>
    /// </remarks>
    /// <param name="asOf">As-of date (only used to estimate year if code has single year digit)</param>
    /// <param name="expiration">futures expiration in one of the formats like Z9 or 13</param>
    /// <param name="month">parsed out month, like 1, 2, ..., 12</param>
    /// <param name="year">parse out year, like 2013</param>
    /// <returns>true if parsed successfully, false otherwise</returns>
    public static bool ParseMonthYearFromExchangeExpiration(Dt asOf, string expiration, out int month, out int year)
    {
      month = 0;
      year = 0;

      // Check for MMMYY (e.g. DEC08)
      var regex = new Regex(@"^(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)([0-9]{2})$", RegexOptions.IgnoreCase);
      //regex = new Regex(@"([A-Z]{3})([0-9]{2})$", RegexOptions.IgnoreCase);
      var match = regex.Match(expiration);
      if (match.Success)
      {
        month = 0;
        var monthStr = match.Groups[1].Value.ToUpper();
        if (MonthAbbrevs.ContainsKey(monthStr))
          month = MonthAbbrevs[monthStr];
        else
          throw new ArgumentException($"Invalid month: {monthStr}");
        year = int.Parse(match.Groups[2].Value) + 2000;
        return true;  // Success
      }

      // Check for contract identifier (e.g. EDZ8, EDZ08, EDZ2008, FEU3Q14)
      regex = new Regex(@"^([FGHJKMNQUVXZ])([0-9]{1,4})$", RegexOptions.IgnoreCase);
      match = regex.Match(expiration);
      if (match.Success)
      {
        month = ExchangeCodeMonth(match.Groups[1].Value[0]);
        year = int.Parse(match.Groups[2].Value);
        if (year >= 10 && year < 100)
          year += 2000;
        else if (year < 10)
        {
          // try to guess digits, but only if we have a asOf date
          if (!asOf.IsValid())
            return false;
          // Date next contract is issued - date of expiration (2BD before Wen or next if holiday)
          // We first find the third Wednesday, and then step back two days to find the expiration.
          Dt frontExpiration = NthWeekDay(asOf.Month, asOf.Year, 3, Base.DayOfWeek.Wednesday);
          frontExpiration = Dt.Add(frontExpiration, -2);
          year += (asOf.Year / 10) * 10;
          if (year < asOf.Year || (year == asOf.Year && month < asOf.Month) ||
              (year == asOf.Year && month == asOf.Month && frontExpiration < asOf))
            year += 10;
        }
        return true;  // Success
      }
      return false;
    }

    /// <summary>
    /// Parse the month and year from an exchange traded product code.
    /// </summary>
    /// <remarks>
    ///   <para>The format for an exchange product code is [contract code][month code][year].</para>
    ///   <para>Examples of exchange codes include EDZ8, EDZ08, EDZ2008, and FEU3Q14.
    ///   MMMYY format such as Jan14 is also accepted.</para>
    ///   <para>This function can also be used without the contract code (eg Z18).</para>
    /// </remarks>
    /// <param name="asOf">As-of date (only used to estimate year if code has single year digit)</param>
    /// <param name="code">futures code in one of the formats like EDZ9 or SEP13</param>
    /// <param name="contractCode">parsed out contract code, like ED</param>
    /// <param name="month">parsed out month, like 1, 2, ..., 12</param>
    /// <param name="year">parse out year, like 2013</param>
    /// <returns>true if parsed successfully, false otherwise</returns>
    public static bool ParseMonthYearFromExchangeCode(Dt asOf, string code, out string contractCode, out int month, out int year)
    {
      contractCode = String.Empty;
      month = 0;
      year = 0;

      // Check for MMMYY (e.g. DEC08)
      var regex = new Regex(@"^(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)([0-9]{2})$", RegexOptions.IgnoreCase);
      //regex = new Regex(@"([A-Z]{3})([0-9]{2})$", RegexOptions.IgnoreCase);
      var match = regex.Match(code);
      if (match.Success)
      {
        month = 0;
        var monthStr = match.Groups[1].Value.ToUpper();
        if (MonthAbbrevs.ContainsKey(monthStr))
          month = MonthAbbrevs[monthStr];
        else
          throw new ArgumentException($"Invalid month: {monthStr}");
        year = int.Parse(match.Groups[2].Value) + 2000;
        return true;  // Success
      }

      // Check for contract identifier (e.g. EDZ8, EDZ08, EDZ2008, FEU3Q14)
      regex = new Regex(@"^(\w*)([FGHJKMNQUVXZ])([0-9]{1,4})$", RegexOptions.IgnoreCase);
      match = regex.Match(code);
      if (match.Success)
      {
        contractCode = match.Groups[1].Value;
        month = ExchangeCodeMonth(match.Groups[2].Value[0]);
        year = int.Parse(match.Groups[3].Value);
        if (year >= 10 && year < 100)
          year += 2000;
        else if (year < 10)
        {
          // try to guess digits
          // Date next contract is issued - date of expiration (2BD before Wen or next if holiday)
          // We first find the third Wednesday, and then step back two days to find the expiration.
          Dt frontExpiration = NthWeekDay(asOf.Month, asOf.Year, 3, Base.DayOfWeek.Wednesday);
          frontExpiration = Dt.Add(frontExpiration, -2);
          year += (asOf.Year / 10) * 10;
          if (year < asOf.Year || (year == asOf.Year && month < asOf.Month) ||
              (year == asOf.Year && month == asOf.Month && frontExpiration < asOf))
            year += 10;
        }
        return true;  // Success
      }

      return false;
    }

    /// <summary>
    /// Return the exchange date code for the month and year of the specified date
    /// </summary>
    /// <remarks>
    /// <para>Exchanges use abreviated month and year codes
    /// to denote the contract month.</para>
    /// <para>The format is CYY where C is the exchange month code
    /// and YY is the last two year digits.</para>
    /// </remarks>
    /// <seealso cref="ExchangeDateCode(string,int,int,int)"/>
    /// <param name="lastTradingDate">Last trading date of contract</param>
    /// <param name="digits">Number of year digits (1, 2 or 4)</param>
    /// <returns>Exchange date code</returns>
    public static string ExchangeDateCode(Dt lastTradingDate, int digits = 2)
    {
      return ExchangeDateCode(String.Empty, lastTradingDate.Month, lastTradingDate.Year, digits);
    }

    /// <summary>
    /// Return the exchange date code for the specified month and year
    /// </summary>
    /// <remarks>
    /// <para>Exchanges use abreviated month and year codes.
    /// The format is [month code][year] where month code is a single
    /// digit and the year is the last two digits of the year.</para>
    /// </remarks>
    /// <seealso cref="ExchangeDateCode(string,int,int,int)"/>
    /// <param name="month">Month</param>
    /// <param name="year">Year</param>
    /// <param name="digits">Number of year digits (1, 2 or 4)</param>
    /// <returns>Exchange date code</returns>
    public static string ExchangeDateCode(int month, int year, int digits = 2)
    {
      return ExchangeDateCode(String.Empty, month, year, digits);
    }

    /// <summary>
    /// Return the exchange date code for the specified month and year
    /// </summary>
    /// <remarks>
    ///   <para>Exchanges use codes to denote individual contracts. The
    ///   format is [contract code][month code][year] where the contract
    ///   code is one to four letters, the month code is a single digit
    ///   and the year is the last two digits of the year.</para>
    ///   <para>Examples of exchange codes include EDZ8, EDZ08, EDZ2008, and FEU3Q14.
    ///   MMMYY format such as Jan14 is also accepted.</para>
    /// </remarks>
    /// <seealso cref="ExchangeMonthCode"/>
    /// <param name="contractCode">Contract code</param>
    /// <param name="month">Month</param>
    /// <param name="year">Year</param>
    /// <param name="digits">Number of year digits</param>
    /// <returns>Exchange date code</returns>
    public static string ExchangeDateCode(string contractCode, int month, int year, int digits = 2)
    {
      return $"{contractCode}{ExchangeMonthCode(month)}{year % Math.Pow(10, digits)}";
    }

    /// <summary>
    /// Return the exchange month code for the specified month
    /// </summary>
    /// <remarks>
    /// <para>These are the month codes commonly used by exchanges to
    /// specify the contract month.</para>
    /// <list type="table">
    ///   <listheader><term>Month</term><description>Code</description></listheader>
    ///   <item><term>January</term><description>F</description></item>
    ///   <item><term>February</term><description>G</description></item>
    ///   <item><term>March</term><description>H</description></item>
    ///   <item><term>April</term><description>J</description></item>
    ///   <item><term>May</term><description>K</description></item>
    ///   <item><term>June</term><description>M</description></item>
    ///   <item><term>July</term><description>N</description></item>
    ///   <item><term>August</term><description>Q</description></item>
    ///   <item><term>September</term><description>U</description></item>
    ///   <item><term>October</term><description>V</description></item>
    ///   <item><term>November</term><description>X</description></item>
    ///   <item><term>December</term><description>Z</description></item>
    /// </list>
    /// </remarks>
    /// <param name="month">Futures month</param>
    /// <returns>Futures code for specified month</returns>
    public static char ExchangeMonthCode(int month)
    {
      if (month < 0 || month > 12)
        throw new ArgumentOutOfRangeException(nameof(month), @"Invalid month. Must be 1-12");
      return FuturesCodes[month - 1];
    }

    /// <summary>
    /// Return the month given the exchange contract month code
    /// </summary>
    /// <seealso cref="ExchangeMonthCode(int)"/>
    /// <param name="monthCode">Exchange contract month code</param>
    /// <returns>Month of year matching the futures code</returns>
    public static int ExchangeCodeMonth(char monthCode)
    {
      var idx = FuturesCodes.IndexOf(monthCode);
      if (idx < 0)
        throw new ArgumentOutOfRangeException(nameof(monthCode), @"Invalid Future month code");
      return idx + 1;
    }


    #region IMM // All these are obsolete RD Aug'14

    /// <summary>
    ///   Calculate the next quarterly IMM date after the specified date.
    /// </summary>
    /// <remarks>
    ///   IMM roll dates are the third Wednesday of March, June, September, and December.
    /// </remarks>
    /// <param name="date">Date</param>
    /// <returns>Next quarterly IMM roll after the specified date</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the next IMM roll after a <see cref="Dt"/>.</para>
    /// <code language="C#">
    ///   // Get todays date
    ///   Dt today = Dt.Today();
    ///
    ///   // Get the next IMM roll date from today.
    ///   Dt immRoll = Dt.ImmNext( today );
    /// </code>
    /// </example>
    public static Dt ImmNext(Dt date)
    {
      if (!date.IsEmpty()) date.Validate();

      int month = date.Month;
      int year = date.Year;


      // Get IMM roll from this month.
      month = ((month + 2) / 3) * 3;
      if (month > (int)Base.Month.December)
      {
        year++;
        month -= 12;
      }

      Dt thisRoll = ImmDate(month, year);
      if (date >= thisRoll)
      {
        // Date in IMM roll month after roll date so move to next one
        month = ((month + 3 + 2) / 3) * 3;
        if (month > (int)Base.Month.December)
        {
          year++;
          month -= 12;
        }
        thisRoll = ImmDate(month, year);
      }

      return thisRoll;
    }

    /// <summary>
    ///   Calculate the contract date for a Eurodollar Future from the product code.
    /// </summary>
    /// <remarks>
    ///   <para>IMM roll dates are the third Wednesday of March, June, September, and December.</para>
    ///   <para>The year code is interpreted based on the asOf year. If the year code is greater than
    ///   or equal to the current year then it is assumed to be from 2010, otherwise it is assumed
    ///   to be from 2000.</para>
    ///   <para>See also:</para>
    ///   <list type="number">
    ///   <item><description><a href="http://www.cme.com/">Chicago Mercantile Exchange</a></description></item>
    ///   <item><description><a href="http://www.rulebook.cme.com/Rulebook/Chapters/pdffiles/452.pdf">CME Rulebook Chapter 452</a></description></item>
    ///   </list>
    /// </remarks>
    /// <param name="asOf">As-of date for calculation of IMM year</param>
    /// <param name="code">CME product code</param>
    /// <returns>Contract date for specified product code</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the contract date for a specific Eurodollar futures contract</para>
    /// <code language="C#">
    ///   Dt today = Dt.Today();
    ///   // Get the Dec 2008 futures date
    ///   Dt immRoll = Dt.ImmDate( today, "EDZ8" );
    /// </code>
    /// </example>
    public static Dt ImmDate(Dt asOf, string code)
    {
      int month, year;
      string contractCode;
      var isSucc = ParseMonthYearFromExchangeCode(asOf, code, out contractCode, out month, out year);
      if (isSucc)
        return ImmDate(month, year);
      // Give up
      throw new ArgumentException($"Invalid ED futures contract identifier: {code}");
    }

    /// <summary>
    /// Calculate the contract date for Eurodollar/ASX/NZD future from the product code
    /// </summary>
    /// <param name="asOf">As-of date for calculation of IMM year</param>
    /// <param name="code">Product code</param>
    /// <param name="cycleRule">Cycle rule</param>
    /// <returns>Contract date for specified product cod</returns>
    /// <exception cref="ArgumentException"></exception>
    public static Dt ImmDate(Dt asOf, string code, CycleRule cycleRule)
    {
      int month, year;
      string contractCode;
      var isSucc = ParseMonthYearFromExchangeCode(asOf, code, out contractCode, out month, out year);
      if (isSucc)
        return ImmDate(month, year, cycleRule);
      // Give up
      throw new ArgumentException($"Invalid futures contract identifier: {code}");
    }

    /// <summary>
    ///   Calculate the contract date for a Eurodollar Future from the month and year.
    /// </summary>
    /// <remarks>
    ///   IMM roll dates are the third Wednesday of March, June, September, and December.
    /// </remarks>
    /// <param name="month">Month</param>
    /// <param name="year">Year</param>
    /// <returns>Contract date for specified month and year</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the Eurodollar futures contract date for a month and year</para>
    /// <code language="C#">
    ///   // Get the Dec 2008 futures date
    ///   Dt immRoll = Dt.ImmDate( 12, 2008 );
    /// </code>
    /// </example>
    public static Dt ImmDate(int month, int year)
    {
      return NthWeekDay(month, year, 3, Base.DayOfWeek.Wednesday);
    }

    /// <summary>
    /// Calculate the contract date for Eurodolloar/ASX/NZD future from the month and year
    /// </summary>
    /// <param name="month">Month</param>
    /// <param name="year">Year</param>
    /// <param name="cycleRule">Cycle rule</param>
    /// <returns>Contract date for specified month and year</returns>
    private static Dt ImmDate(int month, int year, CycleRule cycleRule)
    {
      switch (cycleRule)
      {
        case CycleRule.IMMAUD:
          var secondFriday = NthWeekDay(month, year, 2, Base.DayOfWeek.Friday);
          return Dt.AddDays(secondFriday, -1, Calendar.SYB);
        case CycleRule.IMMNZD:
          // The first Wednesday after the ninth day of the relevant settlement month.
          for (int i = 1; i < 6; i++)
          {
            var wed = NthWeekDay(month, year, i, Base.DayOfWeek.Wednesday);
            if (wed.Day > 9)
            {
              return wed;
            }
          }
          // ultra caution, never going to get here
          throw new ArgumentException($"Couldn't find first Wednesday after ninth day of month {month} in year {year}");
        default:
          return ImmDate(month, year);
      }
    }

    #endregion IMM

    #endregion Exchange Dates

    #region Commodity Date Methods

    /// <summary>
    /// Calculates a multiplier to be applied to a quantity in a commodity notional quantity
    /// </summary>
    /// <param name="start">The period start date.</param>
    /// <param name="end">The period end date.</param>
    /// <param name="qfreq">The Quantity Frequency.</param>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public static int Multiple(Dt start, Dt end, QuantityFrequency qfreq)
    {
      switch (qfreq)
      {
        case QuantityFrequency.PerCalendarDay:
          return Diff(start, end);
        case QuantityFrequency.PerCalculationPeriod:
          return 1;
        default:
          throw new NotImplementedException($"Quantity Frequency {qfreq} not yet implemented");
      }
    }

    #endregion Commodity Date Methods

    #region IFormattable

    /// <summary>
    ///   Converts date to string format using default format (dd-mmm-yyyy).
    /// </summary>
    /// <returns>string containing formated date</returns>
    public override string ToString()
    {
      return ToString(null, null);
    }

    /// <summary>
    /// Convert to string representation
    /// </summary>
    /// <param name="format">Format specifier</param>
    /// <returns></returns>
    public string ToString(string format)
    {
      return ToString(format, null);
    }

    /// <summary>
    /// Convert to string representation
    /// </summary>
    /// <param name="format">Format specifier</param>
    /// <param name="formatProvider">Format provider (ignored)</param>
    /// <returns></returns>
    public string ToString(string format, IFormatProvider formatProvider)
    {
      if (IsEmpty())
        return "<null>";
      else if (!IsValid())
        return "<invalid>";
      if (String.IsNullOrEmpty(format))
        format = "dd-MMM-yyyy";
      // Leverage DateTime format
      var datetime = ToDateTime();
      return (formatProvider != null) ? datetime.ToString(format, formatProvider) : datetime.ToString(format);
    }

    #endregion IFormattable

    #region Parse

    // Compiled regex for Parse()
    private static readonly Regex XlDateMatch = new Regex(@"^(\d+?\.?\d*)$", RegexOptions.Compiled);
    private static readonly Regex YyMmDdMatch = new Regex(@"^(\d{4})(\d{2})(\d{2})$", RegexOptions.Compiled);

    /// <summary>
    ///   Convert string representation of a date to it's date equivalent
    /// </summary>
    /// <remarks>
    ///   <para>Supports all DateTime formats as well as yyyymmdd and XL.</para>
    /// </remarks>
    /// <param name="str">string containing date string</param>
    /// <exception cref="FormatException">If string format is not valid</exception>
    /// <returns>created Dt</returns>
    public static Dt Parse(string str)
    {
      Dt dt;
      if (!TryParse(str, null, DateTimeStyles.None, out dt))
        throw new FormatException("Invalid date format");
      return dt;
    }

    /// <summary>
    ///   Convert string representation of a date to it's date equivalent
    /// </summary>
    /// <remarks>
    ///   <para>Supports all DateTime formats as well as yyyymmdd and XL.</para>
    /// </remarks>
    /// <param name="str">string containing date string</param>
    /// <param name="provider">Culture-specific format information</param>
    /// <exception cref="FormatException">If string format is not valid</exception>
    /// <returns>created Dt</returns>
    public static Dt Parse(string str, IFormatProvider provider)
    {
      Dt dt;
      if (!TryParse(str, provider, DateTimeStyles.None, out dt))
        throw new FormatException("Invalid date format");
      return dt;
    }

    /// <summary>
    ///   Convert string representation of a date to it's date equivalent
    /// </summary>
    /// <remarks>
    ///   <para>Supports all DateTime formats as well as yyyymmdd and XL.</para>
    /// </remarks>
    /// <param name="str">string containing date string</param>
    /// <param name="provider">Culture-specific format information</param>
    /// <param name="styles">Bitwise combination of enumerations dictating how to enterpret the parsed date</param>
    /// <exception cref="FormatException">If string format is not valid</exception>
    /// <returns>created Dt</returns>
    public static Dt Parse(string str, IFormatProvider provider, DateTimeStyles styles)
    {
      Dt dt;
      if (!TryParse(str, provider, styles, out dt))
        throw new FormatException("Invalid date format");
      return dt;
    }

    /// <summary>
    ///   Convert string representation of a date to it's date equivalent
    /// </summary>
    /// <remarks>
    ///   <para>Supports all DateTime formats as well as yyyymmdd and XL.</para>
    ///   <note>Does not throw an exception.</note>
    /// </remarks>
    /// <param name="str">string containing date string</param>
    /// <param name="dt">Returned date</param>
    /// <returns>True if parse successful</returns>
    public static bool TryParse(string str, out Dt dt)
    {
      return TryParse(str, null, DateTimeStyles.None, out dt);
    }

    /// <summary>
    ///   Convert string representation of a date to it's date equivalent
    /// </summary>
    /// <remarks>
    ///   <para>Supports all DateTime formats as well as yyyymmdd and XL.</para>
    ///   <note>Does not throw an exception.</note>
    /// </remarks>
    /// <param name="str">string containing date string</param>
    /// <param name="provider">Culture-specific format information</param>
    /// <param name="styles">Bitwise combination of enumerations dictating how to enterpret the parsed date</param>
    /// <param name="dt">Returned date</param>
    /// <returns>True if parse successful</returns>
    public static bool TryParse(string str, IFormatProvider provider, DateTimeStyles styles, out Dt dt)
    {
      if (str == null || str == "<null>" || str == "<invalid>" || str.Length == 0)
      {
        // Special case of no date
        dt = Empty;
        return true;
      }
      // Backward compatible yyyymmdd
      int yyyymmdd;
      if (str.Length == 8 && Int32.TryParse(str, out yyyymmdd))
      {
        int day = yyyymmdd % 100;
        int month = (yyyymmdd % 10000) / 100;
        int year = yyyymmdd / 10000;
        if (!IsValid(day, month, year, 0))
        {
          dt = Empty;
          return false;
        }
        dt = new Dt(day, month, year, 0);
        return true;
      }
      // Backward compatible XL date
      double dbl;
      if (Double.TryParse(str, out dbl) )
        return TryFromExcelDate(dbl, out dt);
      // Other, leverage DateTime
      DateTime datetime;
      var res = (provider != null) ? DateTime.TryParse(str, provider, styles, out datetime) : DateTime.TryParse(str, out datetime);
      if (!res || !IsValid(datetime.Day, datetime.Month, datetime.Year, 0))
      {
        dt = Empty;
        return false;
      }
      dt = new Dt(datetime);
      return true;
    }

    #endregion Parse

    #region Old Formatting Methods

    /// <summary>
    /// Default date format
    /// </summary>
    public static string FormatDefault
    {
      get { return "%d-%b-%Y"; }
    }

    /// <summary>
    ///   Converts date to string format and stores in str.
    /// </summary>
    /// <remarks>
    ///   <para>The fmt parameter is similar to the unix date command format.</para>
    ///   <para>Field Descriptors:</para>
    ///   <list type="bullet">
    ///     <item><description>%D - date as mm/dd/yy</description></item>
    ///     <item><description>%F - date as mm/dd/yyyy</description></item>
    ///     <item><description>%c - date and time - Nov 04 11:30:00, 1999</description></item>
    ///     <item><description>%m - month of year - 01 to 12</description></item>
    ///     <item><description>%d - day of month - 01 to 31</description></item>
    ///     <item><description>%e - day of month - 1 to 31 (not zero, but space, in front of single digits)</description></item>
    ///     <item><description>%y - last two digits of year - 00 to 99</description></item>
    ///     <item><description>%Y - four digit year.</description></item>
    ///     <item><description>%A - full weekday - Sunday to Saturday</description></item>
    ///     <item><description>%a - abbreviated weekday - Sun to Sat</description></item>
    ///     <item><description>%B - full month - January to December</description></item>
    ///     <item><description>%b - abbreviated month - Jan to Dec</description></item>
    ///     <item><description>%h - same as %b%</description></item>
    ///     <item><description>%H - hour - 00 to 23</description></item>
    ///     <item><description>%M - minute - 00 to 59</description></item>
    ///     <item><description>%S - second - 00 to 59</description></item>
    ///     <item><description>%T - time as HH:MM:SS</description></item>
    ///   </list>
    ///   <para>If fmt is empty, the string is formatted as dd-mmm-yyyy.</para>
    /// </remarks>
    /// <returns>reference to str</returns>
    public string ToStr(string fmt)
    {
      if (IsEmpty())
        return "<null>";
      else if (!IsValid())
        return "<invalid>";
      else if (fmt == "%d-%b-%Y")
        return $"{Day:D2}-{$"{(Month)Month}".Substring(0, 3)}-{Year:D4}";
      else if (fmt == "%D")
        return $"{Month:D2}/{Day:D2}/{Year % 100:D2}";
      else if (fmt == "%F")
        return $"{Month:D2}/{Day:D2}/{Year:D2}";
      else if (fmt == "%c")
        return $"{(Month)Month} {Day:D2} {Hour:D2}:{Minute:D2}:{Second:D2} {Year:D4}";
      else if (fmt == "%D %T")
        return $"{Month:D2}/{Day:D2}/{Year % 100:D2} {Hour:D2}:{Minute:D2}:{Second:D2}";
      else
      {
        StringBuilder buf = new StringBuilder();
        Regex regex = new Regex(@"[^%]+|%[DFcmdeyYAaBbhHMST]");
        MatchCollection mc = regex.Matches(fmt);
        for (int i = 0; i < mc.Count; i++)
        {
          string m = mc[i].Value;
          if (m.Substring(0, 1) == "%")
          {
            switch (char.Parse(m.Substring(1, 1)))
            {
              case 'm':
                buf.Append($"{Month:D2}");
                break;
              case 'd':
              case 'e':
                buf.Append($"{Day:D2}");
                break;
              case 'y':
                buf.Append($"{Year % 100:D2}");
                break;
              case 'Y':
                buf.Append($"{Year:D4}");
                break;
              case 'D':
                buf.Append($"{Month:D2}/{Day:D2}/{Year % 100:D2}");
                break;
              case 'F':
                buf.Append($"{Month:D2}/{Day:D2}/{Year:D4}");
                break;
              case 'A':
                buf.Append($"{DayOfWeek()}");
                break;
              case 'a':
                buf.Append($"{DayOfWeek()}".Substring(0, 3));
                break;
              case 'B':
                buf.Append($"{(Month)Month}");
                break;
              case 'h':
              case 'b':
                buf.Append($"{(Month)Month}".Substring(0, 3));
                break;
              case 'H':
                buf.Append($"{Hour:D2}");
                break;
              case 'M':
                buf.Append($"{Minute:D2}");
                break;
              case 'S':
                buf.Append($"{Second:D2}");
                break;
              case 'T':
                buf.Append($"{Hour:D2}:{Minute:D2}:{Second:D2}");
                break;
            }
          }
          else
          {
            buf.Append(m);
          }
        }

        return buf.ToString();
      }
    }
    
    /// <summary>
    ///   Convert an array of strings to array of dates
    /// </summary>
    /// <param name="strs">Array of strings containing date strings</param>
    /// <param name="fmt">string format of date</param>
    /// <returns>Array of dates converted from strings</returns>
    public static Dt[] FromStr(string[] strs, string fmt)
    {
      if (strs == null || strs.Length == 0)
        return null;
      Dt[] res = new Dt[strs.Length];
      for (int i = 0; i < strs.Length; ++i)
      {
        res[i] = FromStr(strs[i], fmt);
      }
      return res;
    }

    /// <summary>
    ///   Convert string to date
    /// </summary>
    /// <param name="str">string containing date string</param>
    /// <param name="fmt">string format of date</param>
    /// <param name="d"> date to be returned if parsed</param>
    /// <returns>boolean indicating if string was converted to date</returns>
    public static bool TryFromStr(string str, string fmt, out Dt d)
    {
      d = FromStr(str, fmt, false);
      return !d.IsEmpty();
    }

	  ///<summary>
    /// Utility method to decipher the FRA term A * B where A and B are dates
    ///</summary>
    ///<param name="strValue">FRA composite term in the form of A * B</param>
    /// <param name="fmt">string format of date</param>
    ///<param name="settleDt">FRA settlement date</param>
    ///<param name="maturityDt">FRA maturity date</param>
    ///<returns>True if the input is in valid composite Dt format, false otherwise</returns>
    public static bool TryFromStrComposite(string strValue, string fmt, out Dt settleDt, out Dt maturityDt)
    {
      strValue = strValue.ToLower();
      settleDt = Dt.Empty;
      maturityDt = Dt.Empty;
      if (!StringUtil.HasValue(strValue) || (strValue.IndexOf('*') <= 0 && strValue.IndexOf('x') <= 0))
      {
        return false;
      }

      var splitter = '*';
      if (strValue.IndexOf('x') > 0)
        splitter = 'x';
      var components = strValue.Split(splitter);
      return (TryFromStr(components[0].Trim(), fmt,  out settleDt) && TryFromStr(components[1].Trim(), fmt, out maturityDt));
    }

    /// <summary>
    ///   Convert string to date
    /// </summary>
    /// <param name="str">string containing date string</param>
    /// <param name="fmt">string format of date</param>
    /// <returns>Date converted</returns>
    public static Dt FromStr(string str, string fmt)
    {
      return FromStr(str, fmt, true);
    }

    /// <summary>
    ///   Convert string to date
    /// </summary>
    /// <param name="str">string containing date string</param>
    /// <returns>Date converted</returns>
    public static Dt FromStr(string str)
    {
      return FromStr(str, FormatDefault, true);
    }

    private static Dt FromStr(string str, string fmt, bool throwOnParseError)
    {
      if (str == null || str == "<null>" || str == "<invalid>" || str.Length == 0)
      {
        // Special case of no date
        return Empty;
      }
      else if (fmt == "%d-%b-%Y")
      {
        Regex regex = new Regex(@"^([0-9]+)-([a-zA-Z]+)-([0-9]+)$");
        Match match = regex.Match(str);
        if (!match.Success)
        {
          return handleParseException(throwOnParseError, $"Invalid date string: {str}");
        }
        int d = int.Parse(match.Groups[1].Value);
        int m = -1;
        foreach (string s in Enum.GetNames(typeof(Base.Month)))
        {
          if (s.StartsWith(match.Groups[2].Value, true, null))
          {
            m = (int)Enum.Parse(typeof(Base.Month), s);
            break;
          }
        }
        int y = int.Parse(match.Groups[3].Value);
        if (y < 1000)
          // Adjust for 2 digit year
          y += (y < YearCutover ? 2000 : YearBase);
        return new Dt(d, m, y);
      }
      else if (fmt == "%D %T")
      {
        Regex regex = new Regex(@"^([0-9]+)/([0-9]+)/([0-9]+) ([0-9]+):([0-9]+):([0-9]+)$");
        Match match = regex.Match(str);
        if (!match.Success)
        {
          return handleParseException(throwOnParseError, $"Invalid date string: {str}");
        }

        int m = int.Parse(match.Groups[1].Value);
        int d = int.Parse(match.Groups[2].Value);
        int y = int.Parse(match.Groups[3].Value);
        int h = int.Parse(match.Groups[4].Value);
        int i = int.Parse(match.Groups[5].Value);
        int s = int.Parse(match.Groups[6].Value);
        if (y < 1000)
          // Adjust for 2 digit year
          y += (y < YearCutover ? 2000 : YearBase);
        return new Dt(d, m, y, h, i, s);
      }
      else if ((fmt == "%D") || (fmt == "%F"))
      {
        Regex regex = new Regex(@"^([0-9]+)/([0-9]+)/([0-9]+)$");
        Match match = regex.Match(str);
        if (!match.Success)
        {
          return handleParseException(throwOnParseError, $"Invalid date string: {str}");
        }
        int m = int.Parse(match.Groups[1].Value);
        int d = int.Parse(match.Groups[2].Value);
        int y = int.Parse(match.Groups[3].Value);
        // Common quick format
        if (y < 1000)
          // Adjust for 2 digit year
          y += (y < YearCutover ? 2000 : YearBase);
        return new Dt(d, m, y);
      }
      else if (fmt == "%Y%m%d")
      {
        if (str.Length != 8)
        {
          return handleParseException(throwOnParseError, $"Invalid date string: {str}");
        }

        try
        {
          int year = int.Parse(str.Substring(0, 4));
          int month = int.Parse(str.Substring(4, 2));
          int day = int.Parse(str.Substring(6, 2));

          return new Dt(day, month, year);
        }
        catch (Exception)
        {
          return handleParseException(throwOnParseError, $"Invalid date string: {str}");
        }
      }
      else
      {
        return handleParseException(throwOnParseError, $"Unsupported format string: {fmt}");
      }
    }

    private static Dt handleParseException(bool throwOnParseError, string message)
    {
      if (throwOnParseError)
        throw new ArgumentOutOfRangeException(message);

      return Empty;
    }

    /// <summary>
    ///   Parse month/year string
    /// </summary>
    public static void ParseMonthYear(string str, out int month, out int year)
    {
      try
      {
        // For now, assume MM/YY or MM/YYYY
        // TODO: Use specialized parse logic based on regional settings
        string[] parts = str.Split(new char[] { '/' });
        if (parts.Length != 2)
          throw new ArgumentException("Invalid date: " + str);
        else
        {
          month = int.Parse(parts[0]);
          if (month < 1 || month > 12)
            throw new ArgumentException("Invalid month: " + month);
          else
          {
            year = int.Parse(parts[1]);
            if (year < 0)
              throw new ArgumentException("Invalid year: " + year);

            if (year < 1000)
            {
              // Adjust for 2 digit year
              year += (year < YearCutover ? 2000 : YearBase);
            }
          }
        }
      }
      catch (FormatException)
      {
        throw new ArgumentException($"Invalid date string: {str}");
      }
    }

    #endregion Old Formatting Methods

    #region IXmlSerializable

    //we want this class to be serialized as below
    //<Effective>10/20/2006</Effective>

    /// <summary>
    /// Get Schema - returns null
    /// </summary>
    XmlSchema IXmlSerializable.GetSchema()
    {
      return null;
    }
    /// <summary>
    /// read Dt from XML file
    /// </summary>
    void IXmlSerializable.ReadXml(XmlReader reader)
    {
      DateTime date;
      string value = reader.ReadString();
      if (string.IsNullOrEmpty(value))
      {
        this = Empty;
      }
      else if (!value.Contains("/") && DateTime.TryParse(value, out date))
      {
        this = new Dt(date);
      }
      else
      {
        string fmt = value.Contains(":") ? "%D %T" : "%D";
        this = FromStr(value, fmt);
      }
      reader.Read(); //this is a must to skip the end element
    }
    /// <summary>
    /// write Dt into XML file
    /// </summary>
    void IXmlSerializable.WriteXml(XmlWriter writer)
    {
      writer.WriteString(ToDateTime()
        .ToString(minute_ == 0 ? "yyyy-MM-dd" : "s"));
    }

    #endregion IXmlSerializable

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Year of date. Eg. 2004.
    /// </summary>
    public int Year => (int)year_ + YearBase;

    /// <summary>
    ///   Month of year (1-12, 1 = January).
    /// </summary>
    public int Month => (int)month_;

    /// <summary>
    ///   Day of month (1-12).
    /// </summary>
    public int Day => (int)day_;

    /// <summary>
    ///  Hour of day (0-23)
    /// </summary>
    public int Hour => (int)minute_ / 6;

    /// <summary>
    ///  Minute of hour (0-59)
    /// </summary>
    public int Minute => ((int)minute_ % 6) * 10;

    /// <summary>
    ///    Second second of minute (0-59)
    /// </summary>
    public int Second => 0;

    /// <summary>
    /// 10 minute intervals within day
    /// </summary>
    public int Ticks => (int)minute_;

    #region Methods

    /// <summary>
    ///  Get a Day Fraction in Years
    /// </summary>
    /// <param name="start">start date</param>
    /// <param name="end">end date</param>
    /// <param name="dc">day count</param>
    /// <returns>Numer of years as a fraction</returns>
    public static double Years(Dt start, Dt end, DayCount dc)
    {
      Dt pstart = start;
      Dt pend = Dt.Add(start, 1, TimeUnit.Years);
      return Fraction(start, pend, start, end, dc, Frequency.None);
    }

    #endregion Methods

    #endregion Properties

    #region Data

    private UInt32 _value;

    #endregion

    #region Conversion

    [StructLayout(LayoutKind.Explicit)]
    struct Union
    {
      [FieldOffset(0)] public UInt32 u;
      //[FieldOffset(0)] public Int32 i;
      [FieldOffset(0)] public Data d;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Data
    {
      public byte year;
      public byte month;
      public byte day;
      public byte ticks;
    }

    private Union U => new Union {u = _value};

    private static UInt32 Value(byte year, byte month, byte day, byte ticks = 0)
    {
      var d = new Data {year = year, month = month, day = day, ticks = ticks};
      return new Union {d = d}.u;
    }
      
    private byte year_ => U.d.year;

    private byte month_ => U.d.month;

    private byte day_ => U.d.day;

    private byte minute_
    {
      get => U.d.ticks;
      set
      {
        var v = U;
        v.d.ticks = value;
        _value = v.u;
      }
    }

    #endregion Data
  } // struct Dt..

  /// <exclude/>
  [Serializable]
  public class CalendarCalcConfig
  {
    /// <exclude/>
    [ToolkitConfig("Path to a directory constaining holiday files.")]
    public readonly string CalendarDir = "Data/hols/";
  }

  /// <exclude/>
  [Serializable]
  public class DtConfig
  {
    /// <exclude/>
    [ToolkitConfig("Roll maturity dates of new CDS contracts for protection starting day after each IMM date.")]
    public readonly bool RollFollowingCDSDate = true;

    /// <exclude/>
    [ToolkitConfig("The date after which on the run CDS contract rolls every 6, instead of 3, months.")]
    public readonly int StdCdsRollCutoverDate = 20151221;
  }

}
