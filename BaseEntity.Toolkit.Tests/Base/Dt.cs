//
// Dt tests including converted old Dt test routines
// Copyright (c) 2004-2008,   . All rights reserved.
//

using System;
using System.Linq;

using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Details;
using BaseEntity.Toolkit.Tests;

namespace BaseEntity.Toolkit.Tests
{
  /// <summary>
  /// NUnit test for Dt methods
  /// </summary>
  [TestFixture]
  public class TestDt
  {
    private const double DoublePrecision = 1e-6; // 1e-8

    #region Helpers

    // Helper (converting from older test setup)
    public static DayCount ParseDayCountCode(int dayCountCode)
    {
      // Valid daycounts are 0=None, 1=OneOne, 2=A/A ISDA, 3=A/A ISMA, 4=A/A AFB, 5=A/365, 6=A/360, 7=30/360 ISMA, 8=30/360 ISDA, 9=30E/360, 10=A/366
      switch (dayCountCode)
      {
        case 0:
          return DayCount.None;
        case 1:
          return DayCount.OneOne;
        case 2:
          return DayCount.ActualActual;
        case 3:
          return DayCount.ActualActualBond;
        case 4:
          return DayCount.ActualActualEuro;
        case 5:
          return DayCount.Actual365Fixed;
        case 6:
          return DayCount.Actual360;
        case 7:
          return DayCount.Thirty360Isma;
        case 8:
          return DayCount.Thirty360;
        case 9:
          return DayCount.ThirtyE360;
        case 10:
          return DayCount.Actual366;
        default:
          throw new Exception("Must specify valid daycount code.");
      }
    }

    #endregion

    [OneTimeSetUp]
    public void Init()
    {
      // Initialisation for tests.
      // Turn off stdout for exceptions
      // TBD. RTD. Nov04
      return;
    }

    [Smoke]
    [TestCase(19920229, 1)]
    [TestCase(19930229, 0)]
    [TestCase(19930331, 1)]
    [TestCase(19930332, 0)]
    [TestCase(20080229, 1)]
    [TestCase(20080201, 1)]
    [TestCase(20081130, 1)]
    [TestCase(20081131, 0)]
    [TestCase(20090229, 0)]
    [TestCase(20091031, 1)]
    [TestCase(22510101, 0)]
    [TestCase(18991220, 0)]
    [TestCase(20000001, 0)]
    [TestCase(20001301, 0)]
    [TestCase(20000100, 0)]
    [TestCase(20000132, 0)]
    public void IsValid(int idt, int isValid)
    {
      if (isValid != 0)
        Assert.IsTrue(Dt.IsValid(idt));
      else
        Assert.IsFalse(Dt.IsValid(idt));
    }

    [Smoke]
    [TestCase(20150507, "None", 1)]
    [TestCase(20150509, "None", 0)]
    [TestCase(20150525, "None", 1)]
    [TestCase(20080201, "None", 1)]
    [TestCase(20080202, "None", 0)]
    [TestCase(20030526, "None", 1)]
    [TestCase(20030525, "None", 0)]
    [TestCase(20150525, "NYB", 0)]
    [TestCase(20030526, "NYB", 0)]
    [TestCase(20030527, "NYB", 1)]
    [TestCase(20080201, "NYB", 1)]
    [TestCase(20080202, "NYB", 0)]
    [TestCase(20151225, "LNB", 0)]
    [TestCase(20151226, "LNB", 0)]
    [TestCase(20190506, "LNB", 0)]
    [TestCase(20190527, "LNB", 0)]
    [TestCase(20190503, "TKB", 0)]
    [TestCase(20190506, "TKB", 0)]
    public void IsValidSettlement(int idate, string cal, int valid)
    {
      var date = new Dt(idate);
      var calendar = CalendarCalculator.GetCalendar(cal);
      if (valid != 0)
        Assert.IsTrue(date.IsValidSettlement(calendar));
      else
        Assert.IsFalse(date.IsValidSettlement(calendar));
    }

    [Smoke]
    // Test of Diff (Act/Act SIA )
    [TestCase(19930101, 19930221, 3, 51)]
    [TestCase(19930201, 19930301, 3, 28)]
    [TestCase(19920201, 19920301, 3, 29)]
    [TestCase(19930101, 19940101, 3, 365)]
    [TestCase(19930115, 19930201, 3, 17)]
    [TestCase(19930215, 19930401, 3, 45)]
    [TestCase(19930331, 19930430, 3, 30)]
    [TestCase(19930331, 19931231, 3, 275)]
    [TestCase(19930315, 19930615, 3, 92)]
    [TestCase(19931101, 19940301, 3, 120)]
    [TestCase(19931231, 19940201, 3, 32)]
    [TestCase(19930715, 19930915, 3, 62)]
    [TestCase(19930821, 19940411, 3, 233)]
    [TestCase(19930331, 19930401, 3, 1)]
    [TestCase(19931215, 19931231, 3, 16)]
    [TestCase(19931215, 19931230, 3, 15)]
    // Test of Diff (Act/Act SIA across leap years)
    [TestCase(20000101, 20010101, 3, 366)]
    [TestCase(20000301, 20010301, 3, 365)]
    [TestCase(20000101, 20040101, 3, 1461)]
    [TestCase(20000301, 20040101, 3, 1401)]
    [TestCase(20000301, 20040301, 3, 1461)]
    // Test of Diff (30/360 SIA)
    [TestCase(19930101, 19930221, 7, 50)]
    [TestCase(19930201, 19930301, 7, 30)]
    [TestCase(19920201, 19920301, 7, 30)]
    [TestCase(19930101, 19940101, 7, 360)]
    [TestCase(19930115, 19930201, 7, 16)]
    [TestCase(19930215, 19930401, 7, 46)]
    [TestCase(19930331, 19930430, 7, 30)]
    [TestCase(19930331, 19931231, 7, 270)]
    [TestCase(19930315, 19930615, 7, 90)]
    [TestCase(19931101, 19940301, 7, 120)]
    [TestCase(19931231, 19940201, 7, 31)]
    [TestCase(19930715, 19930915, 7, 60)]
    [TestCase(19930821, 19940411, 7, 230)]
    [TestCase(19930331, 19930401, 7, 1)]
    [TestCase(19931215, 19931231, 7, 16)]
    [TestCase(19931215, 19931230, 7, 15)]
    // Test of Diff (30/360 SIA paranoid cased)
    [TestCase(19940228, 19940528, 7, 88)]
    [TestCase(19940228, 19940328, 7, 28)]
    // Additional 30/360 SIA (Isma) tests. xref FB case 43559, tie out vs Bloomberg for ISIN XS1028947403
    [TestCase(20140806, 20150228, 7, 202)]
 	  [TestCase(20150228, 20150831, 7, 180)]
 	  [TestCase(20150228, 20150831, 7, 180)]
 	  [TestCase(20190831, 20200228, 7, 178)]
    // Test of Diff (30/360 ISDA)
    [TestCase(19910129, 19910201, 8, 2)]
    [TestCase(19910129, 19910131, 8, 2)]
    [TestCase(19910130, 19910131, 8, 0)]
    [TestCase(19910131, 19910201, 8, 1)]
    [TestCase(19910228, 19910401, 8, 33)]
    [TestCase(19910201, 19910228, 8, 27)]
    [TestCase(19910301, 19910401, 8, 30)]
    [TestCase(19910201, 19910401, 8, 60)]
    [TestCase(19910701, 19911231, 8, 180)]
    [TestCase(19910801, 19920131, 8, 180)]
    [TestCase(19910901, 19920229, 8, 178)]
    [TestCase(19911001, 19920331, 8, 180)]
    [TestCase(19911101, 19920430, 8, 179)]
    [TestCase(19911201, 19920531, 8, 180)]
    [TestCase(19920101, 19920630, 8, 179)]
    [TestCase(19920201, 19920731, 8, 180)]
    [TestCase(19920301, 19920831, 8, 180)]
    [TestCase(19920401, 19920930, 8, 179)]
    [TestCase(19920501, 19921031, 8, 180)]
    [TestCase(19920601, 19921130, 8, 179)]
    // Test of Diff (30E/360 ISDA)
    [TestCase(19910129, 19910201, 9, 2)]
    [TestCase(19910129, 19910131, 9, 1)]
    [TestCase(19910130, 19910131, 9, 0)]
    [TestCase(19910131, 19910201, 9, 1)]
    [TestCase(19910228, 19910401, 9, 33)]
    [TestCase(19910201, 19910228, 9, 27)]
    [TestCase(19910301, 19910401, 9, 30)]
    [TestCase(19910201, 19910401, 9, 60)]
    [TestCase(19910701, 19911231, 9, 179)]
    [TestCase(19910801, 19920131, 9, 179)]
    [TestCase(19910901, 19920229, 9, 178)]
    [TestCase(19911001, 19920331, 9, 179)]
    [TestCase(19911101, 19920430, 9, 179)]
    [TestCase(19911201, 19920531, 9, 179)]
    [TestCase(19920101, 19920630, 9, 179)]
    [TestCase(19920201, 19920731, 9, 179)]
    [TestCase(19920301, 19920831, 9, 179)]
    [TestCase(19920401, 19920930, 9, 179)]
    [TestCase(19920501, 19921031, 9, 179)]
    [TestCase(19920601, 19921130, 9, 179)]
    public void Diff(int istartDate, int iendDate, int idayCount, int expected)
    {
      var startDate = new Dt(istartDate);
      var endDate = new Dt(iendDate);
      var dayCount = ParseDayCountCode(idayCount);
      Assert.AreEqual(expected, Dt.Diff(startDate, endDate, dayCount));
    }

    [Smoke]
    [TestCase(19930101, 19930221, 51.0)]
    public void FractDiff(int istartDate, int iendDate, double expected)
    {
      var startDate = new Dt(istartDate);
      var endDate = new Dt(iendDate);
      Assert.AreEqual(expected, Dt.FractDiff(startDate, endDate), DoublePrecision);
    }

    [Smoke]
    // Test of Diff (Act/Act SIA )
    [TestCase(19930221, 19930101, 7, -50)]
    [TestCase(19930401, 19930331, 7, -1)]
    [TestCase(19931231, 19931215, 3, -16)]
    public void SignedDiff(int istartDate, int iendDate, int idayCount, int expected)
    {
      var startDate = new Dt(istartDate);
      var endDate = new Dt(iendDate);
      var dayCount = ParseDayCountCode(idayCount);
      Assert.AreEqual(expected, Dt.SignedDiff(startDate, endDate, dayCount));
    }

    [Smoke]
    [TestCase(19950503, 19950504, -1)]
    [TestCase(19950504, 19950504, 0)]
    [TestCase(19950505, 19950504, 1)]
    public void Cmp(int idate1, int idate2, int expected)
    {
      var date1 = new Dt(idate1);
      var date2 = new Dt(idate2);
      Assert.AreEqual(expected, Dt.Cmp(date1, date2));
    }

    [Smoke]
    [TestCase(20040504, "1d", 20040505)]
    [TestCase(20040504, "1w", 20040511)]
    [TestCase(20040504, "1m", 20040604)]
    [TestCase(20040504, "1y", 20050504)]
    [TestCase(20040504, "1y5m", 20051004)]
    [TestCase(20040504, "1y 5m", 20051004)]
    [TestCase(20040504, "7m5d", 20041209)]
    [TestCase(20040504, "5d7m", 20041209)]
    [TestCase(20040504, "2w2d", 20040520)]
    [TestCase(20040504, "2d2w", 20040520)]
    [TestCase(20040504, "2 d 2 w ", 20040520)]
    [TestCase(20120131, "1m3m", 20120529)]
    [TestCase(20120131, "4m", 20120531)]
    [TestCase(20120131, "4y1M1W", 20160307)]
    [TestCase(20120131, "4y1M2W3D", 20160317)]
    [TestCase(20120331, " 4y1M2W3 D", 20160517)]
    [TestCase(20140504, "2000d", 20191025)]
    public void AddTenor(int idt, string tenor, int iexpected)
    {
      var dt = new Dt(idt);
      var expected = new Dt(iexpected);
      Assert.AreEqual(expected, Dt.Add(dt, tenor));
    }

    [TestCase(20140505, "2Y2", 20191025)]
    public void AddTenorExpectException(int idt, string tenor, int iexpected)
    {
      Assert.Throws<ArgumentException>(() => AddTenor(idt, tenor, iexpected));
    }

    [Smoke]
    [TestCase(0, TimeUnit.None)]
    [TestCase(15, TimeUnit.Days)]
    [TestCase(2800, TimeUnit.Days)]
    [TestCase(50, TimeUnit.Weeks)]
    [TestCase(51, TimeUnit.Months)]
    [TestCase(52, TimeUnit.Years)]
    public static void MakeTenor(int n, TimeUnit u)
    {
      var tenor = new Tenor(n, u);
      Assert.AreEqual(n, tenor.N);
      Assert.AreEqual(u, tenor.Units);

      // Let's try stringify and parse
      var t2 = Tenor.Parse(tenor.IsEmpty ? "" : tenor.ToString());
      Assert.AreEqual(tenor, t2);
    }

    [TestCase(10, TimeUnit.None)]
    [TestCase(-15, TimeUnit.Days)]
    public static void MakeTenorExpectException(int n, TimeUnit u)
    {
      Assert.Throws<ArgumentException>(() => MakeTenor(n, u));
    }

    [Smoke]
    [TestCase(20150504, 17, "None", 20150527)]
    [TestCase(20150504, 17, "NYB", 20150528)]
    [TestCase(20150504, 18, "LNB", 20150529)]
    [TestCase(19990514, 0, "None", 19990514)]
    [TestCase(19990514, 1, "None", 19990517)]
    [TestCase(19990514, 2, "None", 19990518)]
    [TestCase(19990514, 5, "None", 19990521)]
    [TestCase(19990514, 6, "None", 19990524)]
    [TestCase(19990516, 0, "None", 19990516)]
    [TestCase(19990517, 1, "None", 19990518)]
    [TestCase(19990517, 5, "None", 19990524)]
    public void AddDays(int istartDate, int days, string icalendar, int iexpected)
    {
      var startDate = new Dt(istartDate);
      var calendar = CalendarCalculator.GetCalendar(icalendar);
      var expected = new Dt(iexpected);
      Assert.AreEqual(expected, Dt.AddDays(startDate, days, calendar));
    }

    [Smoke]
    [TestCase(20000101, 31, 1, 20000201)]
    [TestCase(19000101, 0, 1, 19000101)]
    [TestCase(19000101, 1, 1, 19000102)]
    [TestCase(19000101, 365, 1, 19010101)]
    [TestCase(19000101, 37983, 1, 20031230)]
    [TestCase(19000101, 37984, 1, 20031231)]
    [TestCase(19950504, 5, 1, 19950509)]
    [TestCase(19940101, 1, 3, 19940201)]
    [TestCase(19940101, 12, 3, 19950101)]
    [TestCase(19940101, -1, 3, 19931201)]
    [TestCase(19940101, -12, 3, 19930101)]
    [TestCase(19940101, 17, 3, 19950601)]
    [TestCase(20031031, 1, 3, 20031130)]
    [TestCase(20000229, 1, 4, 20010228)]
    [TestCase(20000201, 29, 1, 20000301)]
    [TestCase(20010101, 31, 1, 20010201)]
    [TestCase(20010201, 29, 1, 20010302)]
    public void Add(int istartDate, int number, int iunit, int iexpected)
    {
      var startDate = new Dt(istartDate);
      var units = (TimeUnit)iunit;
      var expected = new Dt(iexpected);
      Assert.AreEqual(expected, Dt.Add(startDate, number, units));
    }

    [Smoke]
    [TestCase(19961120, "None", 0, 19961120)]
    [TestCase(19961120, "None", 2, 19961120)]
    [TestCase(19961123, "None", 2, 19961125)]
    [TestCase(19961123, "None", 1, 19961125)]
    [TestCase(19961130, "None", 1, 19961202)]
    [TestCase(19961130, "None", 2, 19961129)]
    [TestCase(19961130, "None", 3, 19961129)]
    [TestCase(19980809, "None", 0, 19980809)]
    [TestCase(19980809, "None", 1, 19980810)]
    [TestCase(19980809, "None", 3, 19980807)]
    [TestCase(19980809, "None", 2, 19980810)]
    [TestCase(19980810, "None", 0, 19980810)]
    [TestCase(19980809, "None", 1, 19980810)]
    [TestCase(19980809, "None", 3, 19980807)]
    [TestCase(19980809, "None", 2, 19980810)]
    [TestCase(19981031, "None", 0, 19981031)]
    [TestCase(19981031, "None", 1, 19981102)]
    [TestCase(19981031, "None", 3, 19981030)]
    [TestCase(19981031, "None", 2, 19981030)]
    public void Roll(int idt, string cal, int iroll, int iexpected)
    {
      var date = new Dt(idt);
      var roll = (BDConvention)iroll;
      var calendar = CalendarCalculator.GetCalendar(cal);
      var expected = new Dt(iexpected);
      Assert.AreEqual(Dt.Roll(date, roll, calendar), expected);
    }

    [Smoke]
    [TestCase(5, 2015, 20, "None", 20150528)]
    [TestCase(5, 2015, 20, "NYB", 20150529)]
    public void NthDay(int month, int year, int n, string ical, int iexpected)
    {
      var calendar = CalendarCalculator.GetCalendar(ical);
      var expected = new Dt(iexpected);
      Assert.AreEqual(expected, Dt.NthDay(month, year, n, calendar));
    }

    [Smoke]
    [TestCase(2, 1992, 19920229)]
    [TestCase(2, 1993, 19930228)]
    public void LastDay(int month, int year, int iexpected)
    {
      var expected = new Dt(iexpected);
      Assert.AreEqual(expected, Dt.LastDay(month, year));
    }

    [Test, Smoke]
    [TestCase(5, 1995, 3, 2, 19950517)]
    public void NthWeekDay(int month, int year, int n, int idow, int iexpects)
    {
      var dow = (Toolkit.Base.DayOfWeek)idow;
      var expects = new Dt(iexpects);
      Assert.AreEqual(expects, Dt.NthWeekDay(month, year, n, dow));
    }

    [Smoke]
    [TestCase(19950504, 3)]
    [TestCase(19900904, 1)]
    public void DayOfWeek(int idt, int idow)
    {
      var dt = new Dt(idt);
      var dow = (Toolkit.Base.DayOfWeek)idow;
      Assert.AreEqual(dow, dt.DayOfWeek());
    }

    [Smoke]
    [TestCase(19950102, "%D", "01/02/95")]
    [TestCase(19950102, "%d/%m/%y", "02/01/95")]
    [TestCase(19950102, "%h%d'%y", "Jan02'95")]
    [TestCase(19950502, "%a", "Tue")]
    public void ToStr(int idt, string format, string expects)
    {
      var dt = new Dt(idt);
      Assert.AreEqual(expects, dt.ToStr(format));
    }

    [Smoke]
    [TestCase("2008-05-01 07:34:42", null, 1, 5, 2008, 7, 34, 42)]
    [TestCase("2008-05-01 7:34:42", null, 1, 5, 2008, 7, 34, 42)]
    [TestCase("Thu, 01 May 2008 07:34:42", null, 1, 5, 2008, 7, 34, 42)]
    [TestCase("1May2008 07:34:42", null, 1, 5, 2008, 7, 34, 42)]
    [TestCase("1-May-08 07:34:42", null, 1, 5, 2008, 7, 34, 42)]
    [TestCase("01-May-2008 07:34:42", null, 1, 5, 2008, 7, 34, 42)]
    [TestCase("20080501", null, 1, 5, 2008, 0, 0, 0)]
    [TestCase("5/1/2008 7:34:42", "en-US", 1, 5, 2008, 7, 34, 42)]
    [TestCase("1/5/2008 7:34:42", "en-GB", 1, 5, 2008, 7, 34, 42)]
    public void Parse(string strDt, string culture, int day, int month, int year, int hour, int min, int sec)
    {
      var target = new Dt(day, month, year, hour, min, sec);
      var src = (culture != null) ? Dt.Parse(strDt, new System.Globalization.CultureInfo(culture)) : Dt.Parse(strDt);
      Assert.IsTrue(src == target);
    }

    [Smoke]
    [TestCase(1, 5, 2008, 0, 0, 0)]
    [TestCase(31, 12, 2149, 23, 59, 59)] // Max date supported
    [TestCase(31, 12, 2149, 23, 59, 59)] // Min date supported
    public void ToExcelDate(int day, int month, int year, int hour, int min, int sec)
    {
      // Round trip consistency
      var dt = new Dt(day, month, year, hour, min, sec);
      Assert.IsTrue(Dt.Parse(Dt.ToExcelDate(dt).ToString()) == dt);
    }

    [Smoke]
    // Tests from ISDA Documents: http://www.isda.org/c_and_a/pdf/mktc1198.pdf																			 	  
    [TestCase(20031101, 20040501, 20031101, 20040501, 2, 0.497724381)]
    [TestCase(20031101, 20040501, 20031101, 20040501, 3, 0.5)]
    [TestCase(20031101, 20040501, 20031101, 20040501, 4, 0.497267759)]
    // short first calculation period (first period)													 	  
    // Note here we use the notional start of the period											 	  
    [TestCase(19980701, 19990701, 19990201, 19990701, 2, 0.410958904110)]
    [TestCase(19980701, 19990701, 19990201, 19990701, 3, 0.410958904110)]
    [TestCase(19980701, 19990701, 19990201, 19990701, 4, 0.410958904110)]
    // short first calculation period (second period)													 	  
    [TestCase(19990701, 20000701, 19990701, 20000701, 2, 1.001377348600)]
    [TestCase(19990701, 20000701, 19990701, 20000701, 3, 1.000000000000)]
    [TestCase(19990701, 20000701, 19990701, 20000701, 4, 1.000000000000)]
    // long first calculation period (first period)														 	  
    // Note here we use the notional start of the period											 	  
    [TestCase(20020715, 20030715, 20020815, 20030715, 2, 0.915068493151)]
    //[TestCase(20020715, 20030715, 20020815, 20030715, 3, 0.915760869565)]
    [TestCase(20020715, 20030715, 20020815, 20030715, 4, 0.915068493151)]
    // long first calculation period (second period)													 	  
    // Note here we use the notional start of the second period		
    // test 12 below differs from the mktc1198.pdf doc which has an error for this calculation		 	  
    [TestCase(20030115, 20040115, 20030715, 20040115, 2, 0.504004790778)]
    //[TestCase(20030115, 20040115, 20030715, 20040115, 3, 0.500000000000)]
    [TestCase(20030115, 20040115, 20030715, 20040115, 4, 0.504109589041)]
    // short final calculation period (penultimate period)										 	  
    [TestCase(19990730, 20000130, 19990730, 20000130, 2, 0.503892506924)]
    [TestCase(19990730, 20000130, 19990730, 20000130, 3, 0.500000000000)]
    [TestCase(19990730, 20000130, 19990730, 20000130, 4, 0.504109589041)]
    // short final calculation period (final period)													 	  
    // Note we use the notional maturity date																	 	  
    [TestCase(20000130, 20000730, 20000130, 20000630, 2, 0.415300546448)]
    [TestCase(20000130, 20000730, 20000130, 20000630, 3, 0.417582417582)]
    [TestCase(20000130, 20000730, 20000130, 20000630, 4, 0.41530054644)]
    // long final period																											 	  
    [TestCase(19991130, 20000229, 19991130, 20000430, 2, 0.415540085336)]
    //[TestCase(19991130, 20000229, 19991130, 20000430, 3, 0.415760869565)]
    [TestCase(19991130, 20000229, 19991130, 20000430, 4, 0.415300546448)]
    // calculation period longer than one year
    [TestCase(20040618, 20050708, 20040618, 20050708, 2, 1.05331985927)]
    [TestCase(20040618, 20060618, 20040618, 20060618, 2, 1.99852533872)]
    // Failing tests based on the mktc1198.pdf doc
    // Return these tests to the Fraction section above after we sort out the ISMA Actual/Actual calculations.
    //[TestCase(20020715, 20030715, 20020815, 20030715, 3, 0.915760869565)]
    //[TestCase(20030115, 20040115, 20030715, 20040115, 3, 0.500000000000)]
    //[TestCase(19991130, 20000229, 19991130, 20000430, 3, 0.415760869565)]
    public void Fraction(int iperiodStartDate, int iperiodEndDate, int istartDate, int iendDate, int idayCount, double expected)
    {
      var periodStartDate = new Dt(iperiodStartDate);
      var periodEndDate = new Dt(iperiodEndDate);
      var startDate = new Dt(istartDate);
      var endDate = new Dt(iendDate);
      var dayCount = ParseDayCountCode(idayCount);
      NUnit.Framework.Assert.AreEqual(expected, Dt.Fraction(periodStartDate, periodEndDate, startDate, endDate, dayCount, Frequency.None), DoublePrecision);
    }

    /// <summary>
    ///   Test daycount fraction from SWX Docs
    /// </summary>
    [Smoke]
    [TestCase(20031022, 20031223, 20040422, "ThirtyE360", 0.1694444444)]
    [TestCase(20031022, 20031223, 20040422, "Actual365Fixed", 0.1698630137)]
    [TestCase(20031022, 20031223, 20040422, "Actual360", 0.1722222222)]
    [TestCase(20031022, 20031223, 20040422, "Thirty360Isma", 0.1694444444)]
    [TestCase(20031022, 20031223, 20040422, "Actual365L", 0.1693989071)]
    [TestCase(20031022, 20031223, 20040422, "ActualActualBond", 0.1693989071)]
    public void TestFractionSWX(int ipstart, int isettle, int ipend, string dc, double expects)
    {
      var pstart = new Dt(ipstart);
      var settle = new Dt(isettle);
      var pend = new Dt(ipend);
      var dayCount = (DayCount)Enum.Parse(typeof(DayCount), dc);
      Assert.AreEqual(expects, Dt.Fraction(pstart, pend, pstart, settle, dayCount, Frequency.None), DoublePrecision);
    }

    /// <summary>
    ///   Test parsing of CME Futures codes
    /// </summary>
    [Test, Smoke]
    public void ImmDate()
    {
      // Test samples cases
      Assert.AreEqual(new Dt(20, Month.February, 2008), Dt.ImmDate(new Dt(12, Month.December, 2007), "EDG8"));
      Assert.AreEqual(new Dt(16, Month.March, 2011), Dt.ImmDate(new Dt(12, Month.December, 2007), "EDH1"));
      Assert.AreEqual(new Dt(20, Month.September, 2017), Dt.ImmDate(new Dt(12, Month.December, 2007), "EDU7"));
      Assert.AreEqual(new Dt(17, Month.December, 2008), Dt.ImmDate(new Dt(12, Month.December, 2008), "EDZ8"));

      // Test roll on futures expiration date
      Assert.AreEqual(new Dt(19, Month.December, 2007), Dt.ImmDate(new Dt(1, Month.August, 2007), "EDZ7"));
      Assert.AreEqual(new Dt(19, Month.December, 2007), Dt.ImmDate(new Dt(16, Month.December, 2007), "EDZ7"));

      // It should not roll on the last trade date
      Assert.AreEqual(new Dt(19, Month.December, 2007), Dt.ImmDate(new Dt(17, Month.December, 2007), "EDZ7"));
      // It should roll on one day after the last trade date
      Assert.AreEqual(new Dt(20, Month.December, 2017), Dt.ImmDate(new Dt(18, Month.December, 2007), "EDZ7"));

      Assert.AreEqual(new Dt(20, Month.December, 2017), Dt.ImmDate(new Dt(22, Month.December, 2007), "EDZ7"));
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
    
    [Test, Smoke]
    public void DayOfYear()
    {
      string testName = "DayOfYear";
      int[] dates = new int[24] { 20030101, 20030201, 20030301, 20030401, 20030501, 20030601, 20030701, 20030801, 20030901, 
        20031001, 20031101, 20031201, 20040101, 20040201, 20040301, 20040401, 20040501, 20040601, 20040701, 20040801, 20040901, 
        20041001, 20041101, 20041201};

      int[] values = new int[24] { 1, 32, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 1, 32, 61, 92, 122, 153, 183, 214, 245, 275, 306, 336 };

      for (int i = 0; i < dates.Length; ++i)
      {
        Dt dt = new Dt(dates[i]);
        Assert.AreEqual(values[i], dt.DayOfYear(), String.Format("{0} test {1}", testName, i));
      }

      return;
    }

    [Smoke]
    [TestCase(19010101,367)]
    [TestCase(19700101,25569)]
    [TestCase(19710101,25934)]
    [TestCase(19990101,36161)]
    [TestCase(20000101,36526)]
    [TestCase(20010101, 36892)]
    public void ToExcelDate(int idt, int expected)
    {
      Assert.AreEqual(expected, Dt.ToExcelDate(new Dt(idt)));
    }

    [Smoke]
    [TestCase(19700101, 40588)]
    [TestCase(19710101, 40953)]
    [TestCase(19900101, 47893)]
    [TestCase(20000101, 51545)]
    [TestCase(20000301, 51605)]
    [TestCase(20010101, 51911)]
    public void Julian(int idt, int expected)
    {
      var dt = new Dt(idt);
      Assert.AreEqual(expected, dt.ToJulian());
    }

    [Test, Smoke]
    public void Time()
    {
      string testName = "Time";
      int[] testData = new int[22] { 19910129, 19910129, 19910130, 19910131, 19910228, 19910201, 
        19910301, 19910201, 19910701, 19910801, 19910901, 19911001, 19911101, 19911201, 19920101, 
        19920201, 20030101, 20030228, 20031231, 20040101, 20040229, 20041231 };

      for (int i = 0; i < testData.Length; ++i)
      {
        Dt dt = new Dt(testData[i]);
        double t = dt.ToDouble();
        Dt dt2 = new Dt(t);

        Assert.AreEqual(0, Dt.Cmp(dt, dt2), String.Format("{0} test {1}", testName, i));
      }

      return;
    }

    [Smoke]
    [TestCase(20041114, 20041115, "NYB", 1)]
    [TestCase(20041115, 20041116, "NYB", 1)]
    [TestCase(20041114, 20041121, "NYB", 5)]
    [TestCase(20041114, 20041214, "NYB", 21)]
    [TestCase(20041119, 20041120, "NYB", 0)]
    [TestCase(20041119, 20041122, "NYB", 1)]
    public void BusinessDays(int istartDate, int iendDate, string cal, int expected)
    {
      var startDate = new Dt(istartDate);
      var endDate = new Dt(iendDate);
      var calendar = CalendarCalculator.GetCalendar(cal);
      Assert.AreEqual(expected, Dt.BusinessDays(startDate, endDate, calendar));
    }

    /// <summary>
    /// Tests a custom weekend where Sunday is a valid settlement date and Friday and Saturday are not.
    /// </summary>
    [Test, Smoke]
    public void CustomizedWeekend()
    {
      Dt fri = new Dt(2, 11, 2007);
      Dt sat = new Dt(3, 11, 2007);
      Dt sun = new Dt(4, 11, 2007);

      //test for sunday
      Assert.AreEqual(false, fri.IsValidSettlement(Calendar.DBB), "Friday");
      Assert.AreEqual(false, sat.IsValidSettlement(Calendar.DBB), "Saturday");
      Assert.AreEqual(true, sun.IsValidSettlement(Calendar.DBB), "Sunday");
    }

    /// <summary>
    /// Tests a standard weekend where Friday is a valid settlement date and Saturday and Sunday are not.
    /// </summary>
    [Test, Smoke]
    public void StandardWeekend()
    {
      Dt fri = new Dt(2, 11, 2007);
      Dt sat = new Dt(3, 11, 2007);
      Dt sun = new Dt(4, 11, 2007);

      //test for sunday
      Assert.AreEqual(true, fri.IsValidSettlement(Calendar.NYB), "Friday");
      Assert.AreEqual(false, sat.IsValidSettlement(Calendar.NYB), "Saturday");
      Assert.AreEqual(false, sun.IsValidSettlement(Calendar.NYB), "Sunday");
    }

    /// <summary>
    /// Test StdNACorpAccrualBegin against table in http://www.markit.com/information/cds-model/contentParagraphs/01/text_files/file3/Standard%20CDS%20Contract%20Specification.pdf
    /// </summary>
    [Test, Smoke]
    public void StdNACorpAccrualBegin()
    {
      int[] First        = new int[]{20090319,20090621,20090920,20091220,20100321,20100620,20100919,20101219,20110320,20110619,20110919,20111219,20120319,20120619,20120919,20121219,20130319,20130619,20130919 };
      int[] Last         = new int[]{20090620,20090919,20091219,20100320,20100619,20100918,20101218,20110319,20110618,20110918,20111218,20120318,20120618,20120918,20121218,20130318,20130618,20130918,20131218 };
      int[] AccrualBegin = new int[]{20090320,20090622,20090921,20091221,20100322,20100621,20100920,20101220,20110321,20110620,20110920,20111220,20120320,20120620,20120920,20121220,20130320,20130620,20130920 };
      string[] tradingPeriod = new string[]{"2009Q2","2009Q3","2009Q4","2010Q1","2010Q2","2010Q3","2010Q4",
                                   "2011Q1","2011Q2","2011Q3","2011Q4","2012Q1","2012Q2","2012Q3","2012Q4",
                                   "2013Q1","2013Q2","2013Q3","2013Q4" };
      for(int i=0;i<First.Length;++i)
      {
        Dt first = new Dt(First[i]);
        Dt last = new Dt(Last[i]);
        Dt accrualBegin = new Dt(AccrualBegin[i]);

        Assert.AreEqual(accrualBegin, Dt.SNACFirstAccrualStart(first, Calendar.NYB),
                        String.Format("First date in trading period {0}", tradingPeriod[i]));
        Assert.AreEqual(accrualBegin, Dt.SNACFirstAccrualStart(last, Calendar.NYB),
                        String.Format("Last date in trading period {0}", tradingPeriod[i]));
      }
    }

    /// <summary>
    /// Test StdNACorpAccrualBegin against table in http://www.markit.com/information/cds-model/contentParagraphs/01/text_files/file3/Standard%20CDS%20Contract%20Specification.pdf
    /// <remarks>runs through complete set of dates rather than just border cases</remarks>
    /// </summary>
    [Test]
    public void StdNACorpAccrualBeginFullTest()
    {
      int[] First = new int[] { 20090319, 20090621, 20090920, 20091220, 20100321, 20100620, 20100919, 20101219, 20110320, 20110619, 20110919, 20111219, 20120319, 20120619, 20120919, 20121219, 20130319, 20130619, 20130919 };
      int[] Last = new int[] { 20090620, 20090919, 20091219, 20100320, 20100619, 20100918, 20101218, 20110319, 20110618, 20110918, 20111218, 20120318, 20120618, 20120918, 20121218, 20130318, 20130618, 20130918, 20131218 };
      int[] AccrualBegin = new int[] { 20090320, 20090622, 20090921, 20091221, 20100322, 20100621, 20100920, 20101220, 20110321, 20110620, 20110920, 20111220, 20120320, 20120620, 20120920, 20121220, 20130320, 20130620, 20130920 };
      string[] tradingPeriod = new string[]{"2009Q2","2009Q3","2009Q4","2010Q1","2010Q2","2010Q3","2010Q4",
                                   "2011Q1","2011Q2","2011Q3","2011Q4","2012Q1","2012Q2","2012Q3","2012Q4",
                                   "2013Q1","2013Q2","2013Q3","2013Q4" };
      for (int i = 0; i < First.Length; ++i)
      {
        Dt T = new Dt(First[i]);
        Dt last = new Dt(Last[i]);
        Dt accrualBegin = new Dt(AccrualBegin[i]);

        while(T <= last)
        {
          Assert.AreEqual(accrualBegin, Dt.SNACFirstAccrualStart(T, Calendar.NYB),
                          String.Format("T={0} in trading period {1}",T,tradingPeriod[i]));
          T = Dt.Add(T, 1, TimeUnit.Days); 
        }

        
      }
    }

    #region Standard, on the run, CDS maturity, 6M Roll

    // Validation taken from the ISDA FAQ document, page 5.
    //  http://www2.isda.org/attachment/NzkzNQ==/Amend_Single%20Name_On%20The%20Run_Frequency_FAQ%20(REVISED%20as%20of%2010.13.15).pdf
    [TestCase(20150922, "0M", ExpectedResult = 20151220)]
    [TestCase(20150922, "3M", ExpectedResult = 20160320)]
    [TestCase(20150922, "6M", ExpectedResult = 20160620)]
    [TestCase(20150922, "9M", ExpectedResult = 20160920)]
    [TestCase(20150922, "1Y", ExpectedResult = 20161220)]
    [TestCase(20150922, "2Y", ExpectedResult = 20171220)]
    [TestCase(20150922, "3Y", ExpectedResult = 20181220)]
    [TestCase(20150922, "4Y", ExpectedResult = 20191220)]
    [TestCase(20150922, "5Y", ExpectedResult = 20201220)]

    [TestCase(20151222, "0M", ExpectedResult = 20151220)]
    [TestCase(20151222, "3M", ExpectedResult = 20160320)]
    [TestCase(20151222, "6M", ExpectedResult = 20160620)]
    [TestCase(20151222, "9M", ExpectedResult = 20160920)]
    [TestCase(20151222, "1Y", ExpectedResult = 20161220)]
    [TestCase(20151222, "2Y", ExpectedResult = 20171220)]
    [TestCase(20151222, "3Y", ExpectedResult = 20181220)]
    [TestCase(20151222, "4Y", ExpectedResult = 20191220)]
    [TestCase(20151222, "5Y", ExpectedResult = 20201220)]

    [TestCase(20160322, "0M", ExpectedResult = 20160620)]
    [TestCase(20160322, "3M", ExpectedResult = 20160920)]
    [TestCase(20160322, "6M", ExpectedResult = 20161220)]
    [TestCase(20160322, "9M", ExpectedResult = 20170320)]
    [TestCase(20160322, "1Y", ExpectedResult = 20170620)]
    [TestCase(20160322, "2Y", ExpectedResult = 20180620)]
    [TestCase(20160322, "3Y", ExpectedResult = 20190620)]
    [TestCase(20160322, "4Y", ExpectedResult = 20200620)]
    [TestCase(20160322, "5Y", ExpectedResult = 20210620)]

    [TestCase(20160622, "0M", ExpectedResult = 20160620)]
    [TestCase(20160622, "3M", ExpectedResult = 20160920)]
    [TestCase(20160622, "6M", ExpectedResult = 20161220)]
    [TestCase(20160622, "9M", ExpectedResult = 20170320)]
    [TestCase(20160622, "1Y", ExpectedResult = 20170620)]
    [TestCase(20160622, "2Y", ExpectedResult = 20180620)]
    [TestCase(20160622, "3Y", ExpectedResult = 20190620)]
    [TestCase(20160622, "4Y", ExpectedResult = 20200620)]
    [TestCase(20160622, "5Y", ExpectedResult = 20210620)]

    [TestCase(20160922, "0M", ExpectedResult = 20161220)]
    [TestCase(20160922, "3M", ExpectedResult = 20170320)]
    [TestCase(20160922, "6M", ExpectedResult = 20170620)]
    [TestCase(20160922, "9M", ExpectedResult = 20170920)]
    [TestCase(20160922, "1Y", ExpectedResult = 20171220)]
    [TestCase(20160922, "2Y", ExpectedResult = 20181220)]
    [TestCase(20160922, "3Y", ExpectedResult = 20191220)]
    [TestCase(20160922, "4Y", ExpectedResult = 20201220)]
    [TestCase(20160922, "5Y", ExpectedResult = 20211220)]

    [TestCase(20161222, "0M", ExpectedResult = 20161220)]
    [TestCase(20161222, "3M", ExpectedResult = 20170320)]
    [TestCase(20161222, "6M", ExpectedResult = 20170620)]
    [TestCase(20161222, "9M", ExpectedResult = 20170920)]
    [TestCase(20161222, "1Y", ExpectedResult = 20171220)]
    [TestCase(20161222, "2Y", ExpectedResult = 20181220)]
    [TestCase(20161222, "3Y", ExpectedResult = 20191220)]
    [TestCase(20161222, "4Y", ExpectedResult = 20201220)]
    [TestCase(20161222, "5Y", ExpectedResult = 20211220)]

    // Behavior before contract roll date
    [TestCase(20160318, "0M", ExpectedResult = 20151220)]
    [TestCase(20160318, "3M", ExpectedResult = 20160320)]
    [TestCase(20160318, "6M", ExpectedResult = 20160620)]
    [TestCase(20160318, "9M", ExpectedResult = 20160920)]
    [TestCase(20160318, "1Y", ExpectedResult = 20161220)]
    [TestCase(20160318, "2Y", ExpectedResult = 20171220)]
    [TestCase(20160318, "3Y", ExpectedResult = 20181220)]
    [TestCase(20160318, "4Y", ExpectedResult = 20191220)]
    [TestCase(20160318, "5Y", ExpectedResult = 20201220)]

    [TestCase(20160918, "0M", ExpectedResult = 20160620)]
    [TestCase(20160918, "3M", ExpectedResult = 20160920)]
    [TestCase(20160918, "6M", ExpectedResult = 20161220)]
    [TestCase(20160918, "9M", ExpectedResult = 20170320)]
    [TestCase(20160918, "1Y", ExpectedResult = 20170620)]
    [TestCase(20160918, "2Y", ExpectedResult = 20180620)]
    [TestCase(20160918, "3Y", ExpectedResult = 20190620)]
    [TestCase(20160918, "4Y", ExpectedResult = 20200620)]
    [TestCase(20160918, "5Y", ExpectedResult = 20210620)]

    // The following cases test the behavior on some non-standard tenors.

    // Behavior of the tenors exactly equivalent to 3M
    [TestCase(20151222, "91D", ExpectedResult = 20160320)]
    [TestCase(20151222, "13W", ExpectedResult = 20160320)]

    // Behavior of the tenors exactly equivalent to 6M
    [TestCase(20151222, "182D", ExpectedResult = 20160620)]
    [TestCase(20151222, "26W", ExpectedResult = 20160620)]

    // Tenors > 0 less than 3M (91D, 13W).
    [TestCase(20151222, "1D", ExpectedResult = 20160320)]
    [TestCase(20151222, "1W", ExpectedResult = 20160320)]
    [TestCase(20151222, "1M", ExpectedResult = 20160320)]
    [TestCase(20151222, "2M", ExpectedResult = 20160320)]

    // Tenors > 3M less than 6M (182D, 26W) 
    [TestCase(20151222, "92D", ExpectedResult = 20160620)]
    [TestCase(20151222, "14W", ExpectedResult = 20160620)]
    [TestCase(20151222, "4M", ExpectedResult = 20160620)]
    [TestCase(20151222, "5M", ExpectedResult = 20160620)]

    // Tenors > 6M up to 9M (273D, 39W)
    [TestCase(20151222, "183D", ExpectedResult = 20160920)]
    [TestCase(20151222, "273D", ExpectedResult = 20160920)]
    [TestCase(20151222, "27W", ExpectedResult = 20160920)]
    [TestCase(20151222, "39W", ExpectedResult = 20160920)]
    [TestCase(20151222, "7M", ExpectedResult = 20160920)]
    [TestCase(20151222, "8M", ExpectedResult = 20160920)]

    public int TestCdsMaturityRoll6M(int asOf, string tenor)
    {
      var t = Tenor.Parse(tenor);
      return Dt.CdsMaturityRoll6M(new Dt(asOf), t.N, t.Units).ToInt();
    }

    #endregion

    #region Standard, on the run, CDS maturity, 3M Roll

    [TestCase(20140922, "0M", ExpectedResult = 20141220)]
    [TestCase(20140922, "3M", ExpectedResult = 20150320)]
    [TestCase(20140922, "6M", ExpectedResult = 20150620)]
    [TestCase(20140922, "9M", ExpectedResult = 20150920)]
    [TestCase(20140922, "1Y", ExpectedResult = 20151220)]
    [TestCase(20140922, "2Y", ExpectedResult = 20161220)]
    [TestCase(20140922, "3Y", ExpectedResult = 20171220)]
    [TestCase(20140922, "4Y", ExpectedResult = 20181220)]
    [TestCase(20140922, "5Y", ExpectedResult = 20191220)]

    [TestCase(20141222, "0M", ExpectedResult = 20150320)]
    [TestCase(20141222, "3M", ExpectedResult = 20150620)]
    [TestCase(20141222, "6M", ExpectedResult = 20150920)]
    [TestCase(20141222, "9M", ExpectedResult = 20151220)]
    [TestCase(20141222, "1Y", ExpectedResult = 20160320)]
    [TestCase(20141222, "2Y", ExpectedResult = 20170320)]
    [TestCase(20141222, "3Y", ExpectedResult = 20180320)]
    [TestCase(20141222, "4Y", ExpectedResult = 20190320)]
    [TestCase(20141222, "5Y", ExpectedResult = 20200320)]

    [TestCase(20150322, "0M", ExpectedResult = 20150620)]
    [TestCase(20150322, "3M", ExpectedResult = 20150920)]
    [TestCase(20150322, "6M", ExpectedResult = 20151220)]
    [TestCase(20150322, "9M", ExpectedResult = 20160320)]
    [TestCase(20150322, "1Y", ExpectedResult = 20160620)]
    [TestCase(20150322, "2Y", ExpectedResult = 20170620)]
    [TestCase(20150322, "3Y", ExpectedResult = 20180620)]
    [TestCase(20150322, "4Y", ExpectedResult = 20190620)]
    [TestCase(20150322, "5Y", ExpectedResult = 20200620)]

    [TestCase(20150622, "0M", ExpectedResult = 20150920)]
    [TestCase(20150622, "3M", ExpectedResult = 20151220)]
    [TestCase(20150622, "6M", ExpectedResult = 20160320)]
    [TestCase(20150622, "9M", ExpectedResult = 20160620)]
    [TestCase(20150622, "1Y", ExpectedResult = 20160920)]
    [TestCase(20150622, "2Y", ExpectedResult = 20170920)]
    [TestCase(20150622, "3Y", ExpectedResult = 20180920)]
    [TestCase(20150622, "4Y", ExpectedResult = 20190920)]
    [TestCase(20150622, "5Y", ExpectedResult = 20200920)]

    // Behavior of the tenors exactly equivalent to 3M
    [TestCase(20151222, "3M", ExpectedResult = 20160620)]
    [TestCase(20151222, "91D", ExpectedResult = 20160620)]
    [TestCase(20151222, "13W", ExpectedResult = 20160620)]

    // Behavior of the tenors exactly equivalent to 6M
    [TestCase(20151222, "6M", ExpectedResult = 20160920)]
    [TestCase(20151222, "182D", ExpectedResult = 20160920)]
    [TestCase(20151222, "26W", ExpectedResult = 20160920)]

    // Tenor > 0 less than 3M (91D, 13W)
    [TestCase(20151222, "1D", ExpectedResult = 20160320)]
    [TestCase(20151222, "1W", ExpectedResult = 20160320)]
    [TestCase(20151222, "1M", ExpectedResult = 20160320)]
    [TestCase(20151222, "2M", ExpectedResult = 20160320)]

    // Tenor > 3M less than 6M (182D, 26W)
    [TestCase(20151222, "92D", ExpectedResult = 20160620)]
    [TestCase(20151222, "14W", ExpectedResult = 20160620)]
    [TestCase(20151222, "4M", ExpectedResult = 20160620)]
    [TestCase(20151222, "5M", ExpectedResult = 20160620)]

    // Tenor > 6M less than 9M (273D, 39W)
    [TestCase(20151222, "183D", ExpectedResult = 20160920)]
    [TestCase(20151222, "273D", ExpectedResult = 20160920)]
    [TestCase(20151222, "27W", ExpectedResult = 20160920)]
    [TestCase(20151222, "39W", ExpectedResult = 20160920)]
    [TestCase(20151222, "7M", ExpectedResult = 20160920)]
    [TestCase(20151222, "8M", ExpectedResult = 20160920)]

    public int TestCdsMaurityRoll3M(int asOf, string tenor)
    {
      var t = Tenor.Parse(tenor);
      return Dt.CdsMaturityRoll3M(new Dt(asOf), t.N, t.Units).ToInt();
    }

    #endregion

    #region CDS maturity consistency, roll 6M vs roll 3M

    // In the following cases, we expect the same results rolling 6M and 3M.

    // Tenor < 3M and not cross 20th
    [TestCase(20150410, "2M", ExpectedResult = 20150620)]
    [TestCase(20150510, "1M", ExpectedResult = 20150620)]
    [TestCase(20150610, "1W", ExpectedResult = 20150620)]
    [TestCase(20150618, "1D", ExpectedResult = 20150620)]
    [TestCase(20150619, "1D", ExpectedResult = 20150620)]
    [TestCase(20150622, "1D", ExpectedResult = 20150920)]
    [TestCase(20150622, "1W", ExpectedResult = 20150920)]
    [TestCase(20150622, "1M", ExpectedResult = 20150920)]
    [TestCase(20150622, "2M", ExpectedResult = 20150920)]

    // Tenor < 3M and cross 20th
    [TestCase(20150415, "10W", ExpectedResult = 20150920)]
    [TestCase(20150510, "2M", ExpectedResult = 20150920)]
    [TestCase(20150610, "2W", ExpectedResult = 20150920)]
    [TestCase(20150619, "2D", ExpectedResult = 20150920)]
    [TestCase(20150619, "1W", ExpectedResult = 20150920)]
    [TestCase(20150619, "1M", ExpectedResult = 20150920)]
    [TestCase(20150619, "2M", ExpectedResult = 20150920)]

    // Tenor < 3M and not cross 20th
    [TestCase(20150710, "2M", ExpectedResult = 20150920)]
    [TestCase(20150810, "1M", ExpectedResult = 20150920)]
    [TestCase(20150910, "1W", ExpectedResult = 20150920)]
    [TestCase(20150918, "1D", ExpectedResult = 20150920)]
    [TestCase(20150919, "1D", ExpectedResult = 20150920)]
    [TestCase(20150922, "1D", ExpectedResult = 20151220)]
    [TestCase(20150922, "1W", ExpectedResult = 20151220)]
    [TestCase(20150922, "1M", ExpectedResult = 20151220)]
    [TestCase(20150922, "2M", ExpectedResult = 20151220)]

    // Tenor < 3M and cross 20th
    [TestCase(20150715, "10W", ExpectedResult = 20151220)]
    [TestCase(20150810, "2M", ExpectedResult = 20151220)]
    [TestCase(20150910, "2W", ExpectedResult = 20151220)]
    [TestCase(20150919, "2D", ExpectedResult = 20151220)]
    [TestCase(20150919, "1W", ExpectedResult = 20151220)]
    [TestCase(20150919, "1M", ExpectedResult = 20151220)]
    [TestCase(20150919, "2M", ExpectedResult = 20151220)]

    public int TestCdsMaturityConsistency6Mvs3M(int asOf, string tenor)
    {
      var t = Tenor.Parse(tenor);
      var date = new Dt(asOf);
      var expect = Dt.CdsMaturityRoll3M(date, t.N, t.Units);
      Assert.AreEqual(expect, Dt.CdsMaturityRoll6M(date, t.N, t.Units));
      return expect.ToInt();
    }

    #endregion

    #region CDS maturity, cut over

    [Test]
    public void TestCdsMaturityCutover()
    {
      Dt cutover = new Dt(ToolkitBaseConfigurator.Settings.Dt.StdCdsRollCutoverDate);
      var tenors = Array.ConvertAll(
        new[] {"0M", "1D", "3M", "6M", "9M", "1Y"},
        Tenor.Parse).ToArray();
      for (int i = -365; i < 365; ++i)
      {
        var asOf = cutover + i;
        foreach (var t in tenors)
        {
          var expect = i < 0
            ? Dt.CdsMaturityRoll3M(asOf, t.N, t.Units)
            : Dt.CdsMaturityRoll6M(asOf, t.N, t.Units);
          Assert.AreEqual(expect, Dt.CDSMaturity(asOf, t.N, t.Units));
        }
      }

    }

    #endregion

  }
}