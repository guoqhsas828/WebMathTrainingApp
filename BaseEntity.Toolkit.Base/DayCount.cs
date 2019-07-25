/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   DayCounts for date arithmetic
  /// </summary>
  ///
  /// <remarks>
  ///   <para>The day count convention determines how interest is accrued over
  ///   time and how the coupon is determined.</para>
  ///
  ///   <para>The main regulatory agencies for defining day count standards are:</para>
  ///   <list type="number">
  ///     <item><description><a href="http://www.icma-group.org/">International Capital Market Association (ICMA) (formed from ISMA, IPMA, AIBD)</a></description></item>
  ///     <item><description><a href="http://www.isda.org/">International Swaps and Derivatives Association (ISDA)</a></description></item>
  ///     <item><description><a href="http://www.sifma.org/">Securities Industry and Financial Management Association (SIFMA) (formed from SIA, TBMA/BMA/PSA</a></description></item>
  ///   </list>
  ///
  ///   <para>See also:</para>
  ///   <list type="number">
  ///     <item><description><u>ISDA 2000 Section 4.16</u></description></item>
  ///     <item><description><u>ISDA 2006 Section 4.16</u></description></item>
  ///     <item><description><a href="http://www.isda.org/c_and_a/pdf/ICMA-Rule-251.pdf">ICMA Rule 251</a></description></item>
  ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
  ///   </list>
  /// </remarks>
  ///
  [BaseEntity.Shared.AlphabeticalOrderEnum]
  public enum DayCount
  {
    /// <summary>
    ///   None
    /// </summary>
    None = 0,

    /// <summary>
    ///   <para>1/1 ISDA</para>
    ///   <para>Per Annex to the 2000 ISDA Definitions (June 2000 Version),
    ///   Section 4.16. Day Count Fraction, paragraph (a), i.e. if "1/1"
    ///   is specified, 1.</para>
    /// </summary>
    OneOne,

    /// <summary>
    ///   <para>Act/Act ISDA</para>
    ///
    ///   <para>Also knon as Act/365 ISDA.</para>
    ///
    ///   <para>Per Annex to the 2000 ISDA Definitions (June 2000 Version),
    ///   Section 4.16. Day Count Fraction, paragraph (b), i.e. If
    ///   "Actual/365", "Act/365", "A/365", "Actual/Actual" or "Act/Act"
    ///   is specified, the actual number of days in the Calculation Period
    ///   or Compounding Period in respect of which the payment is being made
    ///   divided by 365 (or, if any portion of that Calculation Period or
    ///   Compounding Period falls in a leap year, the sum of (i) the actual
    ///   number of days in that portion of the Calculation Period or
    ///   Compounding Period falling in a leap year divided by 366 and
    ///   (ii) the actual number of days in that portion of the Calculation
    ///   Period or Compounding Period falling in a non-leap year divided
    ///   by 365).</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description>ISDA 2000 Section 4.16(b)</description></item>
    ///     <item><description>ISDA 2006 Section 4.16(b)</description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    ActualActual,

    /// <summary>
    ///   <para>Act/Act Bond ISDA</para>
    ///
    ///   <para>Also known as UST and Act/Act ISMA, Act/Act ICMA, ISMA-99.</para>
    ///
    ///   <para>The Fixed/Floating Amount will be calculated in accordance with
    ///   Rule 251 of the statutes, by-laws, rules and recommendations of
    ///   the International Securities Market Association, as published in
    ///   April 1999, as applied to straight and convertible bonds issued
    ///   after December 31, 1998, as though the Fixed/Floating Amount were
    ///   the interest coupon on such a bond.
    ///   Ie. Actual days in calculation period divided by the actual
    ///   days in the coupon period</para>
    ///
    ///   <para>See also:</para>
    ///   <list type="number">
    ///     <item><description>ISDA 2006 Section 4.16(c)</description></item>
    ///     <item><description><a href="http://www.isda.org/c_and_a/pdf/ICMA-Rule-251.pdf">ICMA Rule 251</a></description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    ActualActualBond,

    /// <summary>
    ///   <para>Act/Act ISDA Euro</para>
    ///
    ///   <para>Also known as Act/Act AFB.</para>
    ///   <para>The Fixed/Floating Amount will be calculated in accordance
    ///   with the "BASE EXACT/EXACT" day count fraction, as defined
    ///   in the "Definitions Communes lusieurs Additifs Techniques"
    ///   published by the Association Franse des Banques in September 1994.
    ///   = n1/365 + n2/366 where n1=actual number of days in the
    ///   accrual period falling in a non-leap year and n2=the
    ///   actual number of days falling in a leap year; where a
    ///   year is defined as starting March 1 and ending on the
    ///   last day of February.</para>
    /// </summary>
    ///
    ActualActualEuro,

    /// <summary>
    ///   <para>Act/365 Fixed ISDA</para>
    ///
    ///   <para>Also known as Act/365 ISMA, and English.</para>
    ///
    ///   <para>Per Annex to the 2000 ISDA Definitions (June 2000 Version),
    ///   Section 4.16. Day Count Fraction, paragraph (c), i.e. if
    ///   "Actual/365 (Fixed)", "Act/365 (Fixed)", "A/365 (Fixed)" or
    ///   "A/365F" is specified, the actual number of days in the
    ///   Calculation Period or Compounding Period in respect of which
    ///   payment is being made divided by 365.</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description>ISDA 2000 Section 4.16(c)</description></item>
    ///     <item><description>ISDA 2006 Section 4.16(d)</description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    Actual365Fixed,

    /// <summary>
    ///  <para>Act/360 ISDA</para>
    ///
    ///   <para>Also known as Act/360 ISMA and French.</para>
    ///
    ///   <para>ISDA Per Annex to the 2000 ISDA Definitions
    ///   (June 2000 Version), Section 4.16. Day Count Fraction,
    ///   paragraph (d), i.e. if "Actual/360", "Act/360" or "A/360" is
    ///   specified, the actual number of days in the Calculation
    ///   Period or Compounding Period in respect of which payment is
    ///   being made divided by 360.</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description>ISDA 2000 Section 4.16(d)</description></item>
    ///     <item><description>ISDA 2006 Section 4.16(e)</description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    Actual360,

    /// <summary>
    ///   <para>30/360 ISMA</para>
    ///
    ///   <para>Also known as 30U/360, Bond Basis, US Muni 30/360, and 30/360 US.</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description>ISDA 2006 Section 4.16(g)</description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    Thirty360Isma,

    /// <summary>
    ///   <para>30/360 ISDA</para>
    ///
    ///   <para>Per Annex to the 2000 ISDA Definitions
    ///   (June 2000 Version), Section 4.16. Day Count Fraction, 
    ///   paragraph (e), i.e. if "30/360", "360/360" or "Bond Basis"
    ///   is specified, the number of days in the Calculation Period
    ///   or Compounding Period in respect of which payment is being
    ///   made divided by 360 (the number of days to be calculated on
    ///   the basis of a year of 360 days with 12 30-day months (unless
    ///   (i) the last day of the Calculation Period or Compounding
    ///   Period is the 31st day of a month but the first day of the
    ///   Calculation Period or Compounding Period is a day other than
    ///   the 30th or 31st day of a month, in which case the month that
    ///   includes that last day shall not be considered to be
    ///   shortened to a 30-day month, or (ii) the last day of the
    ///   Calculation Period or Compounding Period is the last day of
    ///   the month of February, in which case the month of February
    ///   shall not be considered to be lengthened to a 30-day month)).</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description>ISDA 2006 Section 4.16(g)</description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    Thirty360,

    /// <summary>
    ///   <para>30E/360 ISDA</para>
    ///
    ///   <para>Also known as 30E/360 (Euro) ISMA, 30/360 ICMA,
    ///   30S/360, Eurobond basis (ISDA 2000) and German.</para>
    ///
    ///   <para>ISDA Per Annex to the 2000 ISDA Definitions
    ///   (June 2000 Version), Section 4.16. Day Count Fraction,
    ///   paragraph (f), i.e. if "30E/360" or "Eurobond Basis" is
    ///   specified, the number of days in the Calculation Period or
    ///   Compounding Period in respect of which payment is being made
    ///   divided by 360 (the number of days to be calculated on the
    ///   basis of a year of 360 days with 12 30-day months, without
    ///   regard to the date of the first day or last day of the
    ///   Calculation Period or Compounding Period unless, in the case
    ///   of the final Calculation Period or Compounding Period, the
    ///   Termination Date is the last day of the month of February,
    ///   in which case the month of February shall not be considered
    ///   to be lengthened to a 30-day month).</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description>ISDA 2006 Section 4.16(h)</description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    ThirtyE360,

    /// <summary>Act/366</summary>
    Actual366,

    /// <summary>
    ///   <para>Number of months</para>
    ///
    ///   <para>Number of months in period / number of years in period / 12</para>
    ///
    ///   <para>Common for inflation linked securities.</para>
    /// </summary>
    Months,

    /// <summary>
    ///   <para>30E+/360</para>
    ///
    ///   <para>If start date of the period is 31 it is set to 30 while
    ///   if the end date of the period is the 31st the end date is set to
    ///   the 1st of the following month.</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    ThirtyEP360,

    /// <summary>
    ///   <para>Act/365L</para>
    ///
    ///   <para>ISMA Year and Act/Act AFB.</para>
    ///
    ///   <list type="number">See also:
    ///     <item><description><a href="http://www.isda.org/c_and_a/pdf/ICMA-Rule-251.pdf">ICMA Rule 251.1(i) (euro-sterling)</a></description></item>
    ///     <item><description><a href="http://en.wikipedia.org/wiki/Day_count_convention">Wikipedia</a></description></item>
    ///   </list>
    /// </summary>
    ///
    Actual365L
  }

}
