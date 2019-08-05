//
// ExchangeDates.cs
// Copyright (c)    2011. All rights reserved.
//

using BaseEntity.Toolkit.Base;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class ExchangeDateTest
  {
    // CME Soybean Futures
    [TestCase("SQ14", ExpectedResult = "S")]
    [TestCase("SU14", ExpectedResult = "S")]
    [TestCase("SX14", ExpectedResult = "S")]
    [TestCase("SF15", ExpectedResult = "S")]
    [TestCase("SH15", ExpectedResult = "S")]
    [TestCase("SK15", ExpectedResult = "S")]
    [TestCase("SN15", ExpectedResult = "S")]
    // CME Fed Fund Futures (old style)
    [TestCase("FFQ4", ExpectedResult = "FF")]
    [TestCase("FFU4", ExpectedResult = "FF")]
    [TestCase("FFV4", ExpectedResult = "FF")]
    [TestCase("FFX4", ExpectedResult = "FF")]
    [TestCase("FFZ4", ExpectedResult = "FF")]
    [TestCase("FFF5", ExpectedResult = "FF")]
    [TestCase("FFG5", ExpectedResult = "FF")]
    [TestCase("FFH5", ExpectedResult = "FF")]
    [TestCase("FFJ5", ExpectedResult = "FF")]
    [TestCase("FFK5", ExpectedResult = "FF")]
    [TestCase("FFM5", ExpectedResult = "FF")]
    [TestCase("FFN5", ExpectedResult = "FF")]
    // ASX 30D Cash Rate Futures
    [TestCase("IBQ14", ExpectedResult = "IB")]
    [TestCase("IBU14", ExpectedResult = "IB")]
    [TestCase("IBV14", ExpectedResult = "IB")]
    [TestCase("IBX14", ExpectedResult = "IB")]
    [TestCase("IBZ14", ExpectedResult = "IB")]
    [TestCase("IBF15", ExpectedResult = "IB")]
    [TestCase("IBG15", ExpectedResult = "IB")]
    [TestCase("IBH15", ExpectedResult = "IB")]
    [TestCase("IBJ15", ExpectedResult = "IB")]
    [TestCase("IBK15", ExpectedResult = "IB")]
    [TestCase("IBM15", ExpectedResult = "IB")]
    [TestCase("IBN15", ExpectedResult = "IB")]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q14", ExpectedResult = "FEU3")]
    [TestCase("FEU3U14", ExpectedResult = "FEU3")]
    [TestCase("FEU3V14", ExpectedResult = "FEU3")]
    [TestCase("FEU3X14", ExpectedResult = "FEU3")]
    [TestCase("FEU3Z14", ExpectedResult = "FEU3")]
    [TestCase("FEU3F15", ExpectedResult = "FEU3")]
    [TestCase("FEU3G15", ExpectedResult = "FEU3")]
    [TestCase("FEU3H15", ExpectedResult = "FEU3")]
    [TestCase("FEU3J15", ExpectedResult = "FEU3")]
    [TestCase("FEU3K15", ExpectedResult = "FEU3")]
    [TestCase("FEU3M15", ExpectedResult = "FEU3")]
    [TestCase("FEU3N15", ExpectedResult = "FEU3")]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q2014", ExpectedResult = "FEU3")]
    [TestCase("FEU3U2014", ExpectedResult = "FEU3")]
    [TestCase("FEU3V2014", ExpectedResult = "FEU3")]
    [TestCase("FEU3X2014", ExpectedResult = "FEU3")]
    [TestCase("FEU3Z2014", ExpectedResult = "FEU3")]
    [TestCase("FEU3F2015", ExpectedResult = "FEU3")]
    [TestCase("FEU3G2015", ExpectedResult = "FEU3")]
    [TestCase("FEU3H2015", ExpectedResult = "FEU3")]
    [TestCase("FEU3J2015", ExpectedResult = "FEU3")]
    [TestCase("FEU3K2015", ExpectedResult = "FEU3")]
    [TestCase("FEU3M2015", ExpectedResult = "FEU3")]
    [TestCase("FEU3N2015", ExpectedResult = "FEU3")]
    // MMMYY
    [TestCase("JAN14", ExpectedResult = "")]
    [TestCase("FEB14", ExpectedResult = "")]
    [TestCase("MAR14", ExpectedResult = "")]
    [TestCase("APR14", ExpectedResult = "")]
    [TestCase("MAY14", ExpectedResult = "")]
    [TestCase("JUN14", ExpectedResult = "")]
    [TestCase("JUL14", ExpectedResult = "")]
    [TestCase("AUG14", ExpectedResult = "")]
    [TestCase("SEP14", ExpectedResult = "")]
    [TestCase("OCT14", ExpectedResult = "")]
    [TestCase("NOV14", ExpectedResult = "")]
    [TestCase("DEC14", ExpectedResult = "")]
    [TestCase("DEC4", ExpectedResult = "")]
    [TestCase("DEC2014", ExpectedResult = "")]
    // Just exchange date
    [TestCase("Q14", ExpectedResult = "")]
    [TestCase("F14", ExpectedResult = "")]
    [TestCase("V14", ExpectedResult = "")]
    [TestCase("X14", ExpectedResult = "")]
    [TestCase("Z14", ExpectedResult = "")]
    [TestCase("F15", ExpectedResult = "")]
    [TestCase("G15", ExpectedResult = "")]
    [TestCase("H15", ExpectedResult = "")]
    [TestCase("J15", ExpectedResult = "")]
    [TestCase("K15", ExpectedResult = "")]
    [TestCase("M15", ExpectedResult = "")]
    [TestCase("N15", ExpectedResult = "")]
    [TestCase("N5", ExpectedResult = "")]
    [TestCase("N2015", ExpectedResult = "")]
    public string ParseContractCode(string code)
    {
      var asOf = new Dt(20140601);
      int month, year;
      string contractCode;
      Dt.ParseMonthYearFromExchangeCode(asOf, code, out contractCode, out month, out year);
      return contractCode;
    }

    // CME Soybean Futures
    [TestCase("SQ14", ExpectedResult = 8)]
    [TestCase("SU14", ExpectedResult = 9)]
    [TestCase("SX14", ExpectedResult = 11)]
    [TestCase("SF15", ExpectedResult = 1)]
    [TestCase("SH15", ExpectedResult = 3)]
    [TestCase("SK15", ExpectedResult = 5)]
    [TestCase("SN15", ExpectedResult = 7)]
    // CME Fed Fund Futures (old style)
    [TestCase("FFQ4", ExpectedResult = 8)]
    [TestCase("FFU4", ExpectedResult = 9)]
    [TestCase("FFV4", ExpectedResult = 10)]
    [TestCase("FFX4", ExpectedResult = 11)]
    [TestCase("FFZ4", ExpectedResult = 12)]
    [TestCase("FFF5", ExpectedResult = 1)]
    [TestCase("FFG5", ExpectedResult = 2)]
    [TestCase("FFH5", ExpectedResult = 3)]
    [TestCase("FFJ5", ExpectedResult = 4)]
    [TestCase("FFK5", ExpectedResult = 5)]
    [TestCase("FFM5", ExpectedResult = 6)]
    [TestCase("FFN5", ExpectedResult = 7)]
    // ASX 30D Cash Rate Futures
    [TestCase("IBQ14", ExpectedResult = 8)]
    [TestCase("IBU14", ExpectedResult = 9)]
    [TestCase("IBV14", ExpectedResult = 10)]
    [TestCase("IBX14", ExpectedResult = 11)]
    [TestCase("IBZ14", ExpectedResult = 12)]
    [TestCase("IBF15", ExpectedResult = 1)]
    [TestCase("IBG15", ExpectedResult = 2)]
    [TestCase("IBH15", ExpectedResult = 3)]
    [TestCase("IBJ15", ExpectedResult = 4)]
    [TestCase("IBK15", ExpectedResult = 5)]
    [TestCase("IBM15", ExpectedResult = 6)]
    [TestCase("IBN15", ExpectedResult = 7)]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q14", ExpectedResult = 8)]
    [TestCase("FEU3U14", ExpectedResult = 9)]
    [TestCase("FEU3V14", ExpectedResult = 10)]
    [TestCase("FEU3X14", ExpectedResult = 11)]
    [TestCase("FEU3Z14", ExpectedResult = 12)]
    [TestCase("FEU3F15", ExpectedResult = 1)]
    [TestCase("FEU3G15", ExpectedResult = 2)]
    [TestCase("FEU3H15", ExpectedResult = 3)]
    [TestCase("FEU3J15", ExpectedResult = 4)]
    [TestCase("FEU3K15", ExpectedResult = 5)]
    [TestCase("FEU3M15", ExpectedResult = 6)]
    [TestCase("FEU3N15", ExpectedResult = 7)]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q2014", ExpectedResult = 8)]
    [TestCase("FEU3U2014", ExpectedResult = 9)]
    [TestCase("FEU3V2014", ExpectedResult = 10)]
    [TestCase("FEU3X2014", ExpectedResult = 11)]
    [TestCase("FEU3Z2014", ExpectedResult = 12)]
    [TestCase("FEU3F2015", ExpectedResult = 1)]
    [TestCase("FEU3G2015", ExpectedResult = 2)]
    [TestCase("FEU3H2015", ExpectedResult = 3)]
    [TestCase("FEU3J2015", ExpectedResult = 4)]
    [TestCase("FEU3K2015", ExpectedResult = 5)]
    [TestCase("FEU3M2015", ExpectedResult = 6)]
    [TestCase("FEU3N2015", ExpectedResult = 7)]
    // MMMYY
    [TestCase("JAN14", ExpectedResult = 1)]
    [TestCase("FEB14", ExpectedResult = 2)]
    [TestCase("MAR14", ExpectedResult = 3)]
    [TestCase("APR14", ExpectedResult = 4)]
    [TestCase("MAY14", ExpectedResult = 5)]
    [TestCase("JUN14", ExpectedResult = 6)]
    [TestCase("JUL14", ExpectedResult = 7)]
    [TestCase("AUG14", ExpectedResult = 8)]
    [TestCase("SEP14", ExpectedResult = 9)]
    [TestCase("OCT14", ExpectedResult = 10)]
    [TestCase("NOV14", ExpectedResult = 11)]
    [TestCase("DEC14", ExpectedResult = 12)]
    // Just exchange date
    [TestCase("Q14", ExpectedResult = 8)]
    [TestCase("U14", ExpectedResult = 9)]
    [TestCase("V14", ExpectedResult = 10)]
    [TestCase("X14", ExpectedResult = 11)]
    [TestCase("Z14", ExpectedResult = 12)]
    [TestCase("F15", ExpectedResult = 1)]
    [TestCase("G15", ExpectedResult = 2)]
    [TestCase("H15", ExpectedResult = 3)]
    [TestCase("J15", ExpectedResult = 4)]
    [TestCase("K15", ExpectedResult = 5)]
    [TestCase("M15", ExpectedResult = 6)]
    [TestCase("N15", ExpectedResult = 7)]
    [TestCase("N5", ExpectedResult = 7)]
    [TestCase("N2015", ExpectedResult = 7)]
    public int ParseContractMonth(string code)
    {
      var asOf = new Dt(20140601);
      int month, year;
      string contractCode;
      Dt.ParseMonthYearFromExchangeCode(asOf, code, out contractCode, out month, out year);
      return month;
    }

    // CME Soybean Futures
    [TestCase("SQ14", ExpectedResult = 2014)]
    [TestCase("SU14", ExpectedResult = 2014)]
    [TestCase("SX14", ExpectedResult = 2014)]
    [TestCase("SF15", ExpectedResult = 2015)]
    [TestCase("SH15", ExpectedResult = 2015)]
    [TestCase("SK15", ExpectedResult = 2015)]
    [TestCase("SN15", ExpectedResult = 2015)]
    // CME Fed Fund Futures (old style)
    [TestCase("FFQ4", ExpectedResult = 2014)]
    [TestCase("FFU4", ExpectedResult = 2014)]
    [TestCase("FFV4", ExpectedResult = 2014)]
    [TestCase("FFX4", ExpectedResult = 2014)]
    [TestCase("FFZ4", ExpectedResult = 2014)]
    [TestCase("FFF5", ExpectedResult = 2015)]
    [TestCase("FFG5", ExpectedResult = 2015)]
    [TestCase("FFH5", ExpectedResult = 2015)]
    [TestCase("FFJ5", ExpectedResult = 2015)]
    [TestCase("FFK5", ExpectedResult = 2015)]
    [TestCase("FFM5", ExpectedResult = 2015)]
    [TestCase("FFN5", ExpectedResult = 2015)]
    // ASX 30D Cash Rate Futures
    [TestCase("IBQ14", ExpectedResult = 2014)]
    [TestCase("IBU14", ExpectedResult = 2014)]
    [TestCase("IBV14", ExpectedResult = 2014)]
    [TestCase("IBX14", ExpectedResult = 2014)]
    [TestCase("IBZ14", ExpectedResult = 2014)]
    [TestCase("IBF15", ExpectedResult = 2015)]
    [TestCase("IBG15", ExpectedResult = 2015)]
    [TestCase("IBH15", ExpectedResult = 2015)]
    [TestCase("IBJ15", ExpectedResult = 2015)]
    [TestCase("IBK15", ExpectedResult = 2015)]
    [TestCase("IBM15", ExpectedResult = 2015)]
    [TestCase("IBN15", ExpectedResult = 2015)]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q14", ExpectedResult = 2014)]
    [TestCase("FEU3U14", ExpectedResult = 2014)]
    [TestCase("FEU3V14", ExpectedResult = 2014)]
    [TestCase("FEU3X14", ExpectedResult = 2014)]
    [TestCase("FEU3Z14", ExpectedResult = 2014)]
    [TestCase("FEU3F15", ExpectedResult = 2015)]
    [TestCase("FEU3G15", ExpectedResult = 2015)]
    [TestCase("FEU3H15", ExpectedResult = 2015)]
    [TestCase("FEU3J15", ExpectedResult = 2015)]
    [TestCase("FEU3K15", ExpectedResult = 2015)]
    [TestCase("FEU3M15", ExpectedResult = 2015)]
    [TestCase("FEU3N15", ExpectedResult = 2015)]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q2014", ExpectedResult = 2014)]
    [TestCase("FEU3U2014", ExpectedResult = 2014)]
    [TestCase("FEU3V2014", ExpectedResult = 2014)]
    [TestCase("FEU3X2014", ExpectedResult = 2014)]
    [TestCase("FEU3Z2014", ExpectedResult = 2014)]
    [TestCase("FEU3F2015", ExpectedResult = 2015)]
    [TestCase("FEU3G2015", ExpectedResult = 2015)]
    [TestCase("FEU3H2015", ExpectedResult = 2015)]
    [TestCase("FEU3J2015", ExpectedResult = 2015)]
    [TestCase("FEU3K2015", ExpectedResult = 2015)]
    [TestCase("FEU3M2015", ExpectedResult = 2015)]
    [TestCase("FEU3N2015", ExpectedResult = 2015)]
    // MMMYY
    [TestCase("JAN14", ExpectedResult = 2014)]
    [TestCase("FEB14", ExpectedResult = 2014)]
    [TestCase("MAR14", ExpectedResult = 2014)]
    [TestCase("APR14", ExpectedResult = 2014)]
    [TestCase("MAY14", ExpectedResult = 2014)]
    [TestCase("JUN14", ExpectedResult = 2014)]
    [TestCase("JUL14", ExpectedResult = 2014)]
    [TestCase("AUG14", ExpectedResult = 2014)]
    [TestCase("SEP14", ExpectedResult = 2014)]
    [TestCase("OCT14", ExpectedResult = 2014)]
    [TestCase("NOV14", ExpectedResult = 2014)]
    [TestCase("DEC14", ExpectedResult = 2014)]
    // Just exchange date
    [TestCase("Q14", ExpectedResult = 2014)]
    [TestCase("F14", ExpectedResult = 2014)]
    [TestCase("V14", ExpectedResult = 2014)]
    [TestCase("X14", ExpectedResult = 2014)]
    [TestCase("Z14", ExpectedResult = 2014)]
    [TestCase("F15", ExpectedResult = 2015)]
    [TestCase("G15", ExpectedResult = 2015)]
    [TestCase("H15", ExpectedResult = 2015)]
    [TestCase("J15", ExpectedResult = 2015)]
    [TestCase("K15", ExpectedResult = 2015)]
    [TestCase("M15", ExpectedResult = 2015)]
    [TestCase("N15", ExpectedResult = 2015)]
    [TestCase("N5", ExpectedResult = 2015)]
    [TestCase("N2015", ExpectedResult = 2015)]
    public int ParseContractYear(string code)
    {
      var asOf = new Dt(20140601);
      int month, year;
      string contractCode;
      Dt.ParseMonthYearFromExchangeCode(asOf, code, out contractCode, out month, out year);
      return year;
    }

    // CME Soybean Futures
    [TestCase("SQ14", ExpectedResult = true)]
    [TestCase("SU14", ExpectedResult = true)]
    [TestCase("SX14", ExpectedResult = true)]
    [TestCase("SF15", ExpectedResult = true)]
    [TestCase("SH15", ExpectedResult = true)]
    [TestCase("SK15", ExpectedResult = true)]
    [TestCase("SN15", ExpectedResult = true)]
    [TestCase("SN2015", ExpectedResult = true)]
    [TestCase("SN.15", ExpectedResult = false)]
    [TestCase("SB15", ExpectedResult = false)]
    [TestCase("SN15a", ExpectedResult = false)]
    [TestCase("SN", ExpectedResult = false)]
    [TestCase("2014", ExpectedResult = false)]
    // CME Fed Fund Futures (old style)
    [TestCase("FFQ4", ExpectedResult = true)]
    [TestCase("FFU4", ExpectedResult = true)]
    [TestCase("FFV4", ExpectedResult = true)]
    [TestCase("FFX4", ExpectedResult = true)]
    [TestCase("FFZ4", ExpectedResult = true)]
    [TestCase("FFF5", ExpectedResult = true)]
    [TestCase("FFG5", ExpectedResult = true)]
    [TestCase("FFH5", ExpectedResult = true)]
    [TestCase("FFJ5", ExpectedResult = true)]
    [TestCase("FFK5", ExpectedResult = true)]
    [TestCase("FFM5", ExpectedResult = true)]
    [TestCase("FFN5", ExpectedResult = true)]
    // ASX 30D Cash Rate Futures
    [TestCase("IBQ14", ExpectedResult = true)]
    [TestCase("IBU14", ExpectedResult = true)]
    [TestCase("IBV14", ExpectedResult = true)]
    [TestCase("IBX14", ExpectedResult = true)]
    [TestCase("IBZ14", ExpectedResult = true)]
    [TestCase("IBF15", ExpectedResult = true)]
    [TestCase("IBG15", ExpectedResult = true)]
    [TestCase("IBH15", ExpectedResult = true)]
    [TestCase("IBJ15", ExpectedResult = true)]
    [TestCase("IBK15", ExpectedResult = true)]
    [TestCase("IBM15", ExpectedResult = true)]
    [TestCase("IBN15", ExpectedResult = true)]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q14", ExpectedResult = true)]
    [TestCase("FEU3U14", ExpectedResult = true)]
    [TestCase("FEU3V14", ExpectedResult = true)]
    [TestCase("FEU3X14", ExpectedResult = true)]
    [TestCase("FEU3Z14", ExpectedResult = true)]
    [TestCase("FEU3F15", ExpectedResult = true)]
    [TestCase("FEU3G15", ExpectedResult = true)]
    [TestCase("FEU3H15", ExpectedResult = true)]
    [TestCase("FEU3J15", ExpectedResult = true)]
    [TestCase("FEU3K15", ExpectedResult = true)]
    [TestCase("FEU3M15", ExpectedResult = true)]
    [TestCase("FEU3N15", ExpectedResult = true)]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3Q2014", ExpectedResult = true)]
    [TestCase("FEU3U2014", ExpectedResult = true)]
    [TestCase("FEU3V2014", ExpectedResult = true)]
    [TestCase("FEU3X2014", ExpectedResult = true)]
    [TestCase("FEU3Z2014", ExpectedResult = true)]
    [TestCase("FEU3F2015", ExpectedResult = true)]
    [TestCase("FEU3G2015", ExpectedResult = true)]
    [TestCase("FEU3H2015", ExpectedResult = true)]
    [TestCase("FEU3J2015", ExpectedResult = true)]
    [TestCase("FEU3K2015", ExpectedResult = true)]
    [TestCase("FEU3M2015", ExpectedResult = true)]
    [TestCase("FEU3N2015", ExpectedResult = true)]
    // MMMYY
    [TestCase("JAN14", ExpectedResult = true)]
    [TestCase("FEB14", ExpectedResult = true)]
    [TestCase("MAR14", ExpectedResult = true)]
    [TestCase("APR14", ExpectedResult = true)]
    [TestCase("MAY14", ExpectedResult = true)]
    [TestCase("JUN14", ExpectedResult = true)]
    [TestCase("JUL14", ExpectedResult = true)]
    [TestCase("AUG14", ExpectedResult = true)]
    [TestCase("SEP14", ExpectedResult = true)]
    [TestCase("OCT14", ExpectedResult = true)]
    [TestCase("NOV14", ExpectedResult = true)]
    [TestCase("DEC14", ExpectedResult = true)]
    [TestCase("DEC4", ExpectedResult = false)]
    [TestCase("DEC2014", ExpectedResult = false)]
    [TestCase("Dec14", ExpectedResult = true)]
    [TestCase("December14", ExpectedResult = false)]
    // Just exchange date
    [TestCase("Q14", ExpectedResult = true)]
    [TestCase("F14", ExpectedResult = true)]
    [TestCase("V14", ExpectedResult = true)]
    [TestCase("X14", ExpectedResult = true)]
    [TestCase("Z14", ExpectedResult = true)]
    [TestCase("F15", ExpectedResult = true)]
    [TestCase("G15", ExpectedResult = true)]
    [TestCase("H15", ExpectedResult = true)]
    [TestCase("J15", ExpectedResult = true)]
    [TestCase("K15", ExpectedResult = true)]
    [TestCase("M15", ExpectedResult = true)]
    [TestCase("N15", ExpectedResult = true)]
    [TestCase("N5", ExpectedResult = true)]
    [TestCase("N2015", ExpectedResult = true)]
    // Existing tests
    [TestCase("EDF0", ExpectedResult = true)]
    [TestCase("EZZ9", ExpectedResult = true)]
    [TestCase("SW0Z9", ExpectedResult = true)]
    [TestCase("S2Z9", ExpectedResult = true)]
    [TestCase("M9", ExpectedResult = true)]
    [TestCase("EDA9", ExpectedResult = false)]
    [TestCase("EDZ9x", ExpectedResult = false)]
    [TestCase("EDA-9", ExpectedResult = false)]
    public bool ExchangeCodeIsValid(string code)
    {
      return Dt.ExchangeCodeIsValid(code);
    }

    // ASX 30D Cash Rate Futures
    [TestCase("IB", 8, 2014, ExpectedResult = "IBQ14")]
    [TestCase("IB", 9, 2014, ExpectedResult = "IBU14")]
    [TestCase("IB", 10, 2014, ExpectedResult = "IBV14")]
    [TestCase("IB", 11, 2014, ExpectedResult = "IBX14")]
    [TestCase("IB", 12, 2014, ExpectedResult = "IBZ14")]
    [TestCase("IB", 1, 2015, ExpectedResult = "IBF15")]
    [TestCase("IB", 2, 2015, ExpectedResult = "IBG15")]
    [TestCase("IB", 3, 2015, ExpectedResult = "IBH15")]
    [TestCase("IB", 4, 2015, ExpectedResult = "IBJ15")]
    [TestCase("IB", 5, 2015, ExpectedResult = "IBK15")]
    [TestCase("IB", 6, 2015, ExpectedResult = "IBM15")]
    [TestCase("IB", 7, 2015, ExpectedResult = "IBN15")]
    // Eurex 3M Euribor Futures
    [TestCase("FEU3", 8, 2014, ExpectedResult = "FEU3Q14")]
    [TestCase("FEU3", 9, 2014, ExpectedResult = "FEU3U14")]
    [TestCase("FEU3", 10, 2014, ExpectedResult = "FEU3V14")]
    [TestCase("FEU3", 11, 2014, ExpectedResult = "FEU3X14")]
    [TestCase("FEU3", 12, 2014, ExpectedResult = "FEU3Z14")]
    [TestCase("FEU3", 1, 2015, ExpectedResult = "FEU3F15")]
    [TestCase("FEU3", 2, 2015, ExpectedResult = "FEU3G15")]
    [TestCase("FEU3", 3, 2015, ExpectedResult = "FEU3H15")]
    [TestCase("FEU3", 4, 2015, ExpectedResult = "FEU3J15")]
    [TestCase("FEU3", 5, 2015, ExpectedResult = "FEU3K15")]
    [TestCase("FEU3", 6, 2015, ExpectedResult = "FEU3M15")]
    [TestCase("FEU3", 7, 2015, ExpectedResult = "FEU3N15")]
    public string ExchangeDateCode(string contractCode, int month, int year)
    {
      return Dt.ExchangeDateCode(contractCode, month, year);
    }

    /// <summary>
    /// Test parsing of CME Futures codes
    /// </summary>
    [TestCase("EDG8", 20071212, ExpectedResult = 20080220)]
    [TestCase("EDH1", 20071212, ExpectedResult = 20110316)]
    [TestCase("EDU7", 20071212, ExpectedResult = 20170920)]
    // Test roll on futures expiration date
    [TestCase("EDZ7", 20070801, ExpectedResult = 20071219)]
    [TestCase("EDZ7", 20071216, ExpectedResult = 20071219)]
    // It should not roll on the last trade date
    [TestCase("EDZ7", 20071217, ExpectedResult = 20071219)]
    // It should roll on one day after the last trade date
    [TestCase("EDZ7", 20071218, ExpectedResult = 20171220)]
    [TestCase("EDZ7", 20071222, ExpectedResult = 20171220)]
    public int ImmCodeDate( string code, int asOf )
    {
      return Dt.ImmDate(new Dt(asOf), code).ToInt();
    }

    /// <summary>
    ///   Test CME Futures dates arround the last trade dates
    /// </summary>
    [Test, Smoke]
    public void TestImmDateArroundLastTradeDate()
    {
      // The following dates and correponding codes come from the CME website
      int[] lastTradeDates = new int[]{
        20080114,
        20080218,
        20080317,
        20080414,
        20080519,
        20080616,
        20080714,
        20080915,
        20081215,
        20090316,
        20090615,
        20090914,
        20091214,
        20100315,
        20100614,
        20100913,
        20101213,
        20110314,
        20110613,
        20110919,
        20111219,
        20120319,
        20120618,
        20120917,
        20121217,
        20130318,
        20130617,
        20130916,
        20131216,
        20140317,
        20140616,
        20140915,
        20141215,
        20150316,
        20150615,
        20150914,
        20151214,
        20160314,
        20160613,
        20160919,
        20161219,
        20170313,
        20170619,
        20170918,
        20171218
      };
      string[] productCodes = new string[] {
        "EDF8",
        "EDG8",
        "EDH8",
        "EDJ8",
        "EDK8",
        "EDM8",
        "EDN8",
        "EDU8",
        "EDZ8",
        "EDH9",
        "EDM9",
        "EDU9",
        "EDZ9",
        "EDH0",
        "EDM0",
        "EDU0",
        "EDZ0",
        "EDH1",
        "EDM1",
        "EDU1",
        "EDZ1",
        "EDH2",
        "EDM2",
        "EDU2",
        "EDZ2",
        "EDH3",
        "EDM3",
        "EDU3",
        "EDZ3",
        "EDH4",
        "EDM4",
        "EDU4",
        "EDZ4",
        "EDH5",
        "EDM5",
        "EDU5",
        "EDZ5",
        "EDH6",
        "EDM6",
        "EDU6",
        "EDZ6",
        "EDH7",
        "EDM7",
        "EDU7",
        "EDZ7"
      };

      // Let T be the last trade dates, we test the relationship:
      //   ImmDate(T,code) == ImmDate(T-1,code)
      //   ImmDate(T+1,code) == ImmDate(T,code) rolls to the next decade.
      // In other words, the futures should NOT roll on the last trade date
      // and it rolls on the day AFTER the last trade date.
      for (int i = 0; i < lastTradeDates.Length; ++i)
      {
        Dt frontExpiration = new Dt(lastTradeDates[i]);
        Dt immBefore = Dt.ImmDate(Dt.Add(frontExpiration, -1), productCodes[i]);
        Dt immOn = Dt.ImmDate(frontExpiration, productCodes[i]);
        Dt immAfter = Dt.ImmDate(Dt.Add(frontExpiration, +1), productCodes[i]);
        Assert.AreEqual(immBefore, immOn, "Before-On " + i);
        Assert.AreEqual(10, immAfter.Year - immOn.Year, "Rolls " + i);
      }

      return;
    }
    
  }
}
