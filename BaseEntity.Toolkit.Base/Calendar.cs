/*
 * Copyright (c)    2002-2011. All rights reserved.
 */
using System.Collections.Generic;
using System.Runtime.Serialization;
using System;
using System.Text;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Xml;
using BaseEntity.Toolkit.Base.Details;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Built-in calendars for date arithmetic.
  /// </summary>
  /// <remarks>
  ///  The calendar codes used match those published by Swaps Monitor (http://www.financialcalendar.com)
  /// </remarks>
  [Serializable]
  public struct Calendar : ISerializable, IComparable<Calendar>, IComparable, IXmlSerializable
  {
    #region Initialization

    // We have dual implementations of the Calendar Calculator, managed and native.
    //
    // If the toolkit is not loaded, then only the pure managed CalendarCalculator is used.
    // There is no dependence on the native.dll.
    //
    // If the toolkit is loaded, complications occurs from the need to synchronize
    // the managed and native calendar IDs for those calendars generated on the fly,
    // mostly the composite calendars such as "NYB+LNB".  To ensure that the IDs come
    // from a single source, we make the native CalendarCalc the only calculator.
    //
    // The switch bettween managed and native occurs in the initialization stage. 

    // Call Init() to switch to another calculator.
    internal static void Init(
      Func<string, Calendar> getCalendar,
      Func<Calendar, string> calendarName,
      Func<Calendar, bool> isValidCalendar,
      Func<Calendar, int, int, int, bool> isValidSettlement)
    {
      CalendarCalc.GetCalendar = CheckNonNull(getCalendar, "getCalendar");
      CalendarCalc.CalendarName = CheckNonNull(calendarName, "calendarName");
      CalendarCalc.IsValidCalendar = CheckNonNull(isValidCalendar, "isValidCalendar");
      CalendarCalc.IsValidSettlement = CheckNonNull(isValidSettlement, "isValidSettlement");
    }

    private static T CheckNonNull<T>(T value, string paramName)
      where T : class
    {
      if (value == null)
        throw new ArgumentNullException(paramName);
      return value;
    }

    private static class CalendarCalc
    {
      // Initially, we used the pure managed calendar calculator.
      internal static Func<string, Calendar> GetCalendar
        = CalendarCalculator.GetCalendar;
      internal static Func<Calendar, string> CalendarName
         = CalendarCalculator.CalendarName;
      internal static Func<Calendar, bool> IsValidCalendar
         = CalendarCalculator.IsValidCalendar;
      internal static Func<string[]> GetValidCalendars
         = CalendarCalculator.GetValidCalendars;
      internal static Func<string,string> GetCalendarDescription
        = CalendarCalculator.GetCalendarDescription;
      internal static Func<Calendar, int, int, int, bool> IsValidSettlement
         = CalendarCalculator.IsValidSettlement;
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Construct from calendar name
    /// </summary>
    /// <param name="name"></param>
    public Calendar(string name)
    {
      id_ = CalendarCalc.GetCalendar(name).id_;
    }

    /// <summary>
    /// Construct from integer value.
    /// </summary>
    /// <param name="value"></param>
    public Calendar(int value)
    {
      //Likely that we allow user to create invalid calendar. so no need to check value here.
      id_ = value;
    }

    /// <summary>
    /// Construct from a list of calendars
    /// </summary>
    /// <param name="cals">List of calendars</param>
    public Calendar(params Calendar[] cals)
    {
      // TBD: A better way to do this in the C++ code. RTD Jan'12
      var name = new StringBuilder();
      foreach (var c in cals)
      {
        if (c != Calendar.None)
        {
          if (name.Length > 0)
            name.Append("+");
          name.Append(c.Name);
        }
      }
      id_ = (name.Length > 0) ? CalendarCalc.GetCalendar(name.ToString()).id_ : 0;
    }

    /// <summary>
    /// used by serialization
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public Calendar(SerializationInfo info, StreamingContext context)
    {
      id_ = CalendarCalc.GetCalendar(info.GetString("name")).id_;
    }

    /// <summary>
    /// Determines whether the specified date is valid settlement (business) day.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="month">The month.</param>
    /// <param name="year">The year.</param>
    /// <returns><c>true</c> if the specified date is valid settlement (business) day; otherwise, <c>false</c>.</returns>
    public bool IsValidSettlement(int day, int month, int year)
    {
      return CalendarCalc.IsValidSettlement(this, day, month, year);
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Calendar Integer Id
    /// </summary>
    public int Id
    {
      get { return id_; }
    }

    /// <summary>
    /// Calendar Name
    /// </summary>
    public string Name
    {
      get { return CalendarCalc.CalendarName(this); }
    }

    #endregion Properties

    #region Misc Functions

    /// <summary>Equal to operator</summary>
    public static bool operator ==(Calendar c1, Calendar c2) { return c1.Equals(c2); }

    /// <summary>Not equal to operator</summary>
    public static bool operator !=(Calendar c1, Calendar c2) { return !c1.Equals(c2); }

    /// <summary>
    /// Determines whether the specified Object is equal to the current Object
    /// </summary>
    /// <param name="obj">The Object to compare with the current Object</param>
    /// <returns>true if the specified Object is equal to the current Object; otherwise, false</returns>
    public override bool Equals(object obj)
    {
      if (obj is Calendar)
        return (id_ == ((Calendar)obj).id_);
      return false;
    }

    /// <summary>
    /// Serves as a hash function for a particular type
    /// </summary>
    /// <returns>A hash code for the current Object</returns>
    public override int GetHashCode()
    {
      return id_.GetHashCode(); //the value_ is readonly, otherwise it is not safe.
    }

    /// <summary>
    /// Returns a string that represents the current object
    /// </summary>
    /// <returns>A string that represents the current object</returns>
    public override string ToString()
    {
      return Name;
    }

    /// <summary>
    /// Returns true if calendar contained in current calendar
    /// </summary>
    /// <param name="cal">Calendar to test</param>
    /// <returns>true if calendar contained in current calendar</returns>
    public bool Contains(Calendar cal)
    {
      // TBD: A better way to do this in the C++ code. RTD Jan'12
      return Name.Contains(cal.Name);
    }

    /// <summary>
    /// Create combined calendar
    /// </summary>
    /// <param name="calendars"></param>
    /// <returns></returns>
    public static Calendar CombineCalendar(params Calendar[] calendars)
    {
      var b = new StringBuilder();
      var uniqueCals = new HashSet<Calendar>(calendars);

      var first = true;
      foreach (var cal in uniqueCals)
      {
        if (!first && cal != Calendar.None)
        {
          b.Append('+');
          b.Append(cal.Name);
        }
        else if (cal != Calendar.None)
        {
          first = false;
          b.Append(cal.Name);
        }
      }

      return first ? Calendar.None : new Calendar(b.ToString());
    }

    #endregion Misc Functions

    #region Parse Related method

    /// <summary>
    /// Parse a text as Calendar.
    /// This function is required by some toolkit regression test code.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static Calendar Parse(string text)
    {
      return new Calendar(text);
    }

    /// <summary>
    /// Try to parse a text as Calendar.
    /// This function is required by some toolkit regression test code.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="calendar"></param>
    /// <returns>true if success</returns>
    public static bool TryParse(string text, out Calendar calendar)
    {
      calendar = new Calendar(text);
      if (CalendarCalc.IsValidCalendar(calendar))
        return true;
      calendar = Calendar.None;
      return false;
    }

    /// <summary>
    /// Return list of valid (simple) Calendar names
    /// </summary>
    /// <remarks>
    /// <para>NOTE: The order of the calendars in the array is not guaranteed.</para>
    /// </remarks>
    /// <returns>Array of valid calendar codes</returns>
    public static string[] GetValidCalendarNames()
    {
      return CalendarCalc.GetValidCalendars();
    }

    /// <summary>
    /// Return the details of a calendar specified by name
    /// </summary>
    /// <param name="name">Calendar name</param>
    /// <returns>Calendar description</returns>
    public static string GetCalendarDescription(string name)
    {
      return CalendarCalc.GetCalendarDescription(name);
    }
    #endregion

    #region ISerializable Members

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("name", CalendarCalc.CalendarName(this));
    }

    #endregion

    #region IComparable<Calendar>

    /// <summary>
    /// Compare to another calendar
    /// </summary>
    /// <param name="other">Calendar to compare to</param>
    /// <returns>Sort order, 0 if equal</returns>
    public int CompareTo(Calendar other)
    {
      return id_.CompareTo(other.id_);
    }

    #endregion IComparable<Calendar>

    #region IComparable Members

    /// <summary>
    /// Compare to another object
    /// </summary>
    /// <param name="obj">Object to compare to</param>
    /// <returns>Sort order, 0 if equal</returns>
    public int CompareTo(object obj)
    {
      if (obj == null) return 1;
      var cal = (Calendar)obj;
      return id_.CompareTo(cal.id_);
    }

    #endregion IComparable

    #region IXmlSerializable Members

    //we want this class to be serialized as below
    //<Calendar>NYB</Calendar>
    /// <summary>
    /// Get schema
    /// </summary>
    XmlSchema IXmlSerializable.GetSchema()
    {
      return null;
    }

    /// <summary>
    /// Read Calendar from XML file
    /// </summary>
    void IXmlSerializable.ReadXml(XmlReader reader)
    {
      string name = reader.ReadString();
      if (string.IsNullOrEmpty(name))
        id_ = 0; //Calendar.None
      else
      {
        var calendar = CalendarCalc.GetCalendar(name);
        id_ = calendar.id_;
      }
      reader.Read(); //this is a must to skip the end element
    }

    /// <summary>
    /// Write Calendar into XML file
    /// </summary>
    void IXmlSerializable.WriteXml(XmlWriter writer)
    {
      writer.WriteString(Name);
    }

    #endregion IXmlSerializable Members

    #region Data

    private int id_;

    #endregion Data

    #region Build-in Calendars

    /// <summary>Weekends only</summary>
    public static readonly Calendar None = new Calendar(0);
    /// <summary>Albania Tirana bank holidays</summary>
    public static readonly Calendar TIB = new Calendar(1);
    /// <summary>Andorra La Vella bank holidays  ALB  ADP  Bank</summary>
    public static readonly Calendar ALB = new Calendar(2);
    /// <summary>Argentina  Buenos Aires bank holidays  BAB  ARS  Bank</summary>
    public static readonly Calendar BAB = new Calendar(3);
    /// <summary>Argentina  Mercado de Valores de Buenos Aires settlement holidays  BAX  ARS  SE Settlement</summary>
    public static readonly Calendar BAX = new Calendar(4);
    /// <summary>Argentina  Mercado de Valores de Buenos Aires trading holidays  BAS  ARS  SE Trading</summary>
    public static readonly Calendar BAS = new Calendar(5);
    /// <summary>Armenia  Yerevan bank holidays  YRB  AMD  Bank</summary>
    public static readonly Calendar YRB = new Calendar(6);
    /// <summary>Aruba  Oranjestad bank holidays  ORB  AWG  Bank</summary>
    public static readonly Calendar ORB = new Calendar(7);
    /// <summary>Australia  Adelaide bank holidays  ADB  AUD  Bank</summary>
    public static readonly Calendar ADB = new Calendar(8);
    /// <summary>Australia  Australian RTGS payments system holidays  NAU  AUD  Bank</summary>
    public static readonly Calendar NAU = new Calendar(9);
    /// <summary>Australia  Australian Stock Exchange settlement holidays  SYX  AUD  SE Settlement</summary>
    public static readonly Calendar SYX = new Calendar(10);
    /// <summary>Australia  Australian Stock Exchange trading holidays  SYS  AUD  SE Trading</summary>
    public static readonly Calendar SYS = new Calendar(11);
    /// <summary>Australia  Brisbane bank holidays  BBB  AUD  Bank</summary>
    public static readonly Calendar BBB = new Calendar(12);
    /// <summary>Australia  Canberra bank holidays  CNB  AUD  Bank</summary>
    public static readonly Calendar CNB = new Calendar(13);
    /// <summary>Australia  Darwin bank holidays  DRB  AUD  Bank</summary>
    public static readonly Calendar DRB = new Calendar(14);
    /// <summary>Australia  Hobart bank holidays  HOB  AUD  Bank</summary>
    public static readonly Calendar HOB = new Calendar(15);
    /// <summary>Australia  Melbourne bank holidays  MEB  AUD  Bank</summary>
    public static readonly Calendar MEB = new Calendar(16);
    /// <summary>Australia  Perth bank holidays  PEB  AUD  Bank</summary>
    public static readonly Calendar PEB = new Calendar(17);
    /// <summary>Australia  Sydney bank holidays  SYB  AUD  Bank</summary>
    public static readonly Calendar SYB = new Calendar(18);
    /// <summary>Australia  Sydney Futures Exchange trading holidays  SYF  AUD  Futures Trading</summary>
    public static readonly Calendar SYF = new Calendar(19);
    /// <summary>Austria  Vienna bank holidays  VIB  ATS  Bank</summary>
    public static readonly Calendar VIB = new Calendar(20);
    /// <summary>Austria  Vienna bank holidays plus TARGET  VII  ATS  Bank</summary>
    public static readonly Calendar VII = new Calendar(21);
    /// <summary>Austria  Wiener Borsekammer settlement holidays  VIX  ATS  SE Settlement</summary>
    public static readonly Calendar VIX = new Calendar(22);
    /// <summary>Austria  Wiener Borsekammer trading holidays  VIS  ATS  SE Trading</summary>
    public static readonly Calendar VIS = new Calendar(23);
    /// <summary>Azerbaijan  Baku bank holidays  BUB  AZM  Bank</summary>
    public static readonly Calendar BUB = new Calendar(24);
    /// <summary>Bahamas  Bahamas International Securities Exchange settlement holidays  NAX  BSD  SE Settlement</summary>
    public static readonly Calendar NAX = new Calendar(25);
    /// <summary>Bahamas  Bahamas International Securities Exchange trading holidays  NAS  BSD  SE Trading</summary>
    public static readonly Calendar NAS = new Calendar(26);
    /// <summary>Bahamas  Nassau bank holidays  NAB  BSD  Bank</summary>
    public static readonly Calendar NAB = new Calendar(27);
    /// <summary>Bahrain  Bahrain bank holidays  BHB  BHD  Bank</summary>
    public static readonly Calendar BHB = new Calendar(28);
    /// <summary>Bangladesh  Dhaka bank holidays  DKB  BDT  Bank</summary>
    public static readonly Calendar DKB = new Calendar(29);
    /// <summary>Barbados  Bridgetown bank holidays  BGB  BBD  Bank</summary>
    public static readonly Calendar BGB = new Calendar(30);
    /// <summary>Belarus  Minsk bank holidays  MKB  BYR  Bank</summary>
    public static readonly Calendar MKB = new Calendar(31);
    /// <summary>Belgium  Brussels bank holidays  BRB  BEF  Bank</summary>
    public static readonly Calendar BRB = new Calendar(32);
    /// <summary>Belgium  Brussels bank holidays plus TARGET  BRI  BEF  Bank</summary>
    public static readonly Calendar BRI = new Calendar(33);
    /// <summary>Belgium  Euroclear holidays  ECL  BEF  Other</summary>
    public static readonly Calendar ECL = new Calendar(34);
    /// <summary>Belgium  Euronext (Brussels) settlement holidays  BRX  BEF  SE Settlement</summary>
    public static readonly Calendar BRX = new Calendar(35);
    /// <summary>Belgium  Euronext (Brussels) trading holidays  BRS  BEF  SE Trading</summary>
    public static readonly Calendar BRS = new Calendar(36);
    /// <summary>Belgium  NASDAQ Europe trading holidays  NDR  BEF  SE Trading</summary>
    public static readonly Calendar NDR = new Calendar(37);
    /// <summary>Belize  Belmopan bank holidays  BPB  BZD  Bank</summary>
    public static readonly Calendar BPB = new Calendar(38);
    /// <summary>Bermuda  Bermuda Stock Exchange settlement holidays  HMX  BMD  SE Settlement</summary>
    public static readonly Calendar HMX = new Calendar(39);
    /// <summary>Bermuda  Bermuda Stock Exchange trading holidays  HMS  BMD  SE Trading</summary>
    public static readonly Calendar HMS = new Calendar(40);
    /// <summary>Bermuda  Hamilton bank holidays  HMB  BMD  Bank</summary>
    public static readonly Calendar HMB = new Calendar(41);
    /// <summary>Bolivia  Bolsa Boliviana de Valores settlement holidays  LPX  BOB  SE Settlement</summary>
    public static readonly Calendar LPX = new Calendar(42);
    /// <summary>Bolivia  Bolsa Boliviana de Valores trading holidays  LPS  BOB  SE Trading</summary>
    public static readonly Calendar LPS = new Calendar(43);
    /// <summary>Bolivia  La Paz bank holidays  LPB  BOB  Bank</summary>
    public static readonly Calendar LPB = new Calendar(44);
    /// <summary>Bosnia-Herzegovina  Sarajevo bank holidays  SRB  BAM  Bank</summary>
    public static readonly Calendar SRB = new Calendar(45);
    /// <summary>Botswana  Gaborone bank holidays  GAB  BWP  Bank</summary>
    public static readonly Calendar GAB = new Calendar(46);
    /// <summary>Brazil  Bolsa de Mercadorias y Futuros settlement holidays (Govt securities)  RJX  BRL  SE Settlement</summary>
    public static readonly Calendar RJX = new Calendar(47);
    /// <summary>Brazil  Bolsa de Mercadorias y Futuros trading holidays (Futures contracts)  SPF  BRL  Futures Trading</summary>
    public static readonly Calendar SPF = new Calendar(48);
    /// <summary>Brazil  Bolsa de Mercadorias y Futuros trading holidays (Govt securities)  RJS  BRL  SE Trading</summary>
    public static readonly Calendar RJS = new Calendar(49);
    /// <summary>Brazil  Bolsa de Valores de Sao Paolo settlement holidays  SPX  BRL  SE Settlement</summary>
    public static readonly Calendar SPX = new Calendar(50);
    /// <summary>Brazil  Bolsa de Valores de Sao Paolo trading holidays  SPS  BRL  SE Trading</summary>
    public static readonly Calendar SPS = new Calendar(51);
    /// <summary>Brazil  Brazilian payments system holidays  NBR  BRL  Bank</summary>
    public static readonly Calendar NBR = new Calendar(52);
    /// <summary>Brazil  Rio de Janeiro bank holidays  RJB  BRL  Bank</summary>
    public static readonly Calendar RJB = new Calendar(53);
    /// <summary>Brazil  Sao Paolo bank holidays  SPB  BRL  Bank</summary>
    public static readonly Calendar SPB = new Calendar(54);
    /// <summary>Brunei  Bandar Seri Begawan bank holidays  BWB  BND  Bank</summary>
    public static readonly Calendar BWB = new Calendar(55);
    /// <summary>Bulgaria  Bulgarian Stock Exchange settlement holidays  SOX  BGL  SE Settlement</summary>
    public static readonly Calendar SOX = new Calendar(56);
    /// <summary>Bulgaria  Bulgarian Stock Exchange trading holidays  SOY  BGL  SE Trading</summary>
    public static readonly Calendar SOY = new Calendar(57);
    /// <summary>Bulgaria  Sofia bank holidays  SOB  BGL  Bank</summary>
    public static readonly Calendar SOB = new Calendar(58);
    /// <summary>Canada  Calgary bank holidays  CLB  CAD  Bank</summary>
    public static readonly Calendar CLB = new Calendar(59);
    /// <summary>Canada  Canadian RTGS payments system holidays  NCA  CAD  Bank</summary>
    public static readonly Calendar NCA = new Calendar(60);
    /// <summary>Canada  Edmonton bank holidays  EDB  CAD  Bank</summary>
    public static readonly Calendar EDB = new Calendar(61);
    /// <summary>Canada  Montreal bank holidays  MOB  CAD  Bank</summary>
    public static readonly Calendar MOB = new Calendar(62);
    /// <summary>Canada  Montreal Exchange trading holidays (Equity and index contracts)  MOT  CAD  Futures Trading</summary>
    public static readonly Calendar MOT = new Calendar(63);
    /// <summary>Canada  Montreal Exchange trading holidays (Interest rate contracts)  MOS  CAD  Futures Trading</summary>
    public static readonly Calendar MOS = new Calendar(64);
    /// <summary>Canada  Natural Gas Exchange trading holidays (Other contracts)  NGF  CAD  Futures Trading</summary>
    public static readonly Calendar NGF = new Calendar(65);
    /// <summary>Canada  Natural Gas Exchange trading holidays (Short-term contracts)  NGE  CAD  Futures Trading</summary>
    public static readonly Calendar NGE = new Calendar(66);
    /// <summary>Canada  Ottawa bank holidays  OTB  CAD  Bank</summary>
    public static readonly Calendar OTB = new Calendar(67);
    /// <summary>Canada  Quebec City bank holidays  QCB  CAD  Bank</summary>
    public static readonly Calendar QCB = new Calendar(68);
    /// <summary>Canada  Toronto bank holidays  TRB  CAD  Bank</summary>
    public static readonly Calendar TRB = new Calendar(69);
    /// <summary>Canada  Toronto Stock Exchange settlement holidays  TRX  CAD  SE Settlement</summary>
    public static readonly Calendar TRX = new Calendar(70);
    /// <summary>Canada  Toronto Stock Exchange trading holidays  TRS  CAD  SE Trading</summary>
    public static readonly Calendar TRS = new Calendar(71);
    /// <summary>Canada  TSX Venture Exchange settlement holidays  VAX  CAD  SE Settlement</summary>
    public static readonly Calendar VAX = new Calendar(72);
    /// <summary>Canada  TSX Venture Exchange trading holidays  VAS  CAD  SE Trading</summary>
    public static readonly Calendar VAS = new Calendar(73);
    /// <summary>Canada  Vancouver bank holidays  VAB  CAD  Bank</summary>
    public static readonly Calendar VAB = new Calendar(74);
    /// <summary>Canada  Winnipeg Commodity Exchange trading holidays  WCE  CAD  Futures Trading</summary>
    public static readonly Calendar WCE = new Calendar(75);
    /// <summary>Cayman Islands  George Town bank holidays  GTB  KYD  Bank</summary>
    public static readonly Calendar GTB = new Calendar(76);
    /// <summary>Channel Islands  St. Helier bank holidays  SZB  GBP  Bank</summary>
    public static readonly Calendar SZB = new Calendar(77);
    /// <summary>Chile  Bolsa de Comercio de Santiago settlement holidays  SAX  CLP  SE Settlement</summary>
    public static readonly Calendar SAX = new Calendar(78);
    /// <summary>Chile  Bolsa de Comercio de Santiago trading holidays  SAS  CLP  SE Trading</summary>
    public static readonly Calendar SAS = new Calendar(79);
    /// <summary>Chile  Santiago bank holidays  SAB  CLP  Bank</summary>
    public static readonly Calendar SAB = new Calendar(80);
    /// <summary>China  Beijing bank holidays  BEB  CNY  Bank</summary>
    public static readonly Calendar BEB = new Calendar(81);
    /// <summary>China  Guangzhou bank holidays  GUB  CNY  Bank</summary>
    public static readonly Calendar GUB = new Calendar(82);
    /// <summary>China  Shanghai bank holidays  SHB  CNY  Bank</summary>
    public static readonly Calendar SHB = new Calendar(83);
    /// <summary>China  Shanghai Futures Exchange trading holidays  SFS  CNY  Futures Trading</summary>
    public static readonly Calendar SFS = new Calendar(84);
    /// <summary>China  Shanghai Stock Exchange settlement holidays (A shares)  SHW  CNY  SE Settlement</summary>
    public static readonly Calendar SHW = new Calendar(85);
    /// <summary>China  Shanghai Stock Exchange settlement holidays (B shares)  SHX  CNY  SE Settlement</summary>
    public static readonly Calendar SHX = new Calendar(86);
    /// <summary>China  Shanghai Stock Exchange trading holidays  SHS  CNY  SE Trading</summary>
    public static readonly Calendar SHS = new Calendar(87);
    /// <summary>Colombia  Bogota bank holidays  BOB  COP  Bank</summary>
    public static readonly Calendar BOB = new Calendar(88);
    /// <summary>Colombia  Bolsa de Valores de Colombia settlement holidays  BOX  COP  SE Settlement</summary>
    public static readonly Calendar BOX = new Calendar(89);
    /// <summary>Colombia  Bolsa de Valores de Colombia trading holidays  BOS  COP  SE Trading</summary>
    public static readonly Calendar BOS = new Calendar(90);
    /// <summary>Costa Rica  San Jose bank holidays  SJB  CRC  Bank</summary>
    public static readonly Calendar SJB = new Calendar(91);
    /// <summary>Croatia  Zagreb bank holidays  ZAB  HRK  Bank</summary>
    public static readonly Calendar ZAB = new Calendar(92);
    /// <summary>Croatia  Zagreb Stock Exchange settlement holidays  ZAX  HRK  SE Settlement</summary>
    public static readonly Calendar ZAX = new Calendar(93);
    /// <summary>Croatia  Zagreb Stock Exchange trading holidays  ZAS  HRK  SE Trading</summary>
    public static readonly Calendar ZAS = new Calendar(94);
    /// <summary>Cyprus  Cyprus Stock Exchange settlement holidays  NIX  CYP  SE Settlement</summary>
    public static readonly Calendar NIX = new Calendar(95);
    /// <summary>Cyprus  Cyprus Stock Exchange trading holidays  NIS  CYP  SE Trading</summary>
    public static readonly Calendar NIS = new Calendar(96);
    /// <summary>Cyprus  Nicosia bank holidays  NIB  CYP  Bank</summary>
    public static readonly Calendar NIB = new Calendar(97);
    /// <summary>Czech Republic  Prague bank holidays  PRB  CZK  Bank</summary>
    public static readonly Calendar PRB = new Calendar(98);
    /// <summary>Czech Republic  Prague Stock Exchange settlement holidays  PRX  CZK  SE Settlement</summary>
    public static readonly Calendar PRX = new Calendar(99);
    /// <summary>Czech Republic  Prague Stock Exchange trading holidays  PRS  CZK  SE Trading</summary>
    public static readonly Calendar PRS = new Calendar(100);
    /// <summary>Denmark  Copenhagen bank holidays  COB  DKK  Bank</summary>
    public static readonly Calendar COB = new Calendar(101);
    /// <summary>Denmark  Kobenhavns Fondsbors settlement holidays  COX  DKK  SE Settlement</summary>
    public static readonly Calendar COX = new Calendar(102);
    /// <summary>Denmark  Kobenhavns Fondsbors trading holidays  COS  DKK  SE Trading</summary>
    public static readonly Calendar COS = new Calendar(103);
    /// <summary>Dominican Republic  Santo Domingo bank holidays  SNB  DOP  Bank</summary>
    public static readonly Calendar SNB = new Calendar(104);
    /// <summary>Ecuador  Bolsa de Quito settlement holidays  QIX  ECS  SE Settlement</summary>
    public static readonly Calendar QIX = new Calendar(105);
    /// <summary>Ecuador  Bolsa de Quito trading holidays  QIS  ECS  SE Trading</summary>
    public static readonly Calendar QIS = new Calendar(106);
    /// <summary>Ecuador  Quito bank holidays  QIB  ECS  Bank</summary>
    public static readonly Calendar QIB = new Calendar(107);
    /// <summary>Egypt  Cairo bank holidays  CRB  EGP  Bank</summary>
    public static readonly Calendar CRB = new Calendar(108);
    /// <summary>Egypt  Cairo Stock Exchange settlement holidays  CRX  EGP  SE Settlement</summary>
    public static readonly Calendar CRX = new Calendar(109);
    /// <summary>Egypt  Cairo Stock Exchange trading holidays  CRS  EGP  SE Trading</summary>
    public static readonly Calendar CRS = new Calendar(110);
    /// <summary>El Salvador  San Salvador bank holidays  SSB  SVC  Bank</summary>
    public static readonly Calendar SSB = new Calendar(111);
    /// <summary>England  EuroMTS settlement holidays (Austrian bonds)  EMH  GBP  Other</summary>
    public static readonly Calendar EHM = new Calendar(112);
    /// <summary>England  EuroMTS settlement holidays (Belgian bonds)  EMJ  GBP  Other</summary>
    public static readonly Calendar EMJ = new Calendar(113);
    /// <summary>England  EuroMTS settlement holidays (Dutch bonds)  EMO  GBP  Other</summary>
    public static readonly Calendar EMO = new Calendar(114);
    /// <summary>England  EuroMTS settlement holidays (Finnish bonds)  EMK  GBP  Other</summary>
    public static readonly Calendar EMK = new Calendar(115);
    /// <summary>England  EuroMTS settlement holidays (French bonds)  EML  GBP  Other</summary>
    public static readonly Calendar EML = new Calendar(116);
    /// <summary>England  EuroMTS settlement holidays (German bonds)  EMG  GBP  Other</summary>
    public static readonly Calendar EMG = new Calendar(117);
    /// <summary>England  EuroMTS settlement holidays (Greek bonds)  EMM  GBP  Other</summary>
    public static readonly Calendar EMM = new Calendar(118);
    /// <summary>England  EuroMTS settlement holidays (Irish bonds)  EMI  GBP  Other</summary>
    public static readonly Calendar EMI = new Calendar(119);
    /// <summary>England  EuroMTS settlement holidays (Italian bonds)  EMN  GBP  Other</summary>
    public static readonly Calendar EMN = new Calendar(120);
    /// <summary>England  EuroMTS settlement holidays (Portuguese bonds)  EMP  GBP  Other</summary>
    public static readonly Calendar EMP = new Calendar(121);
    /// <summary>England  EuroMTS settlement holidays (Quasi-government bonds)  EMQ  GBP  Other</summary>
    public static readonly Calendar EMQ = new Calendar(122);
    /// <summary>England  EuroMTS settlement holidays (Spanish bonds)  EMR  GBP  Other</summary>
    public static readonly Calendar EMR = new Calendar(123);
    /// <summary>England  EuroMTS trading holidays  EMS  GBP  Other</summary>
    public static readonly Calendar EMS = new Calendar(124);
    /// <summary>England  Euronext.liffe trading holidays (Commodity contracts)  LIH  GBP  Futures Trading</summary>
    public static readonly Calendar LIH = new Calendar(125);
    /// <summary>England  Euronext.liffe trading holidays (Continental equity contracts)  MAT  GBP  Futures Trading</summary>
    public static readonly Calendar MAT = new Calendar(126);
    /// <summary>England  Euronext.liffe trading holidays (Euribor contracts)  LIG  GBP  Futures Trading</summary>
    public static readonly Calendar LIG = new Calendar(127);
    /// <summary>England  Euronext.liffe trading holidays (UK financial contracts)  LIF  GBP  Futures Trading</summary>
    public static readonly Calendar LIF = new Calendar(128);
    /// <summary>England  International Petroleum Exchange (London) trading holidays  IPE  GBP  Futures Trading</summary>
    public static readonly Calendar IPE = new Calendar(129);
    /// <summary>England  London bank holidays  LNB  GBP  Bank</summary>
    public static readonly Calendar LNB = new Calendar(130);
    /// <summary>England  London Metal Exchange trading holidays  LME  GBP  Futures Trading</summary>
    public static readonly Calendar LME = new Calendar(131);
    /// <summary>England  London Stock Exchange settlement holidays  LNX  GBP  SE Settlement</summary>
    public static readonly Calendar LNX = new Calendar(132);
    /// <summary>England  London Stock Exchange trading holidays  LNS  GBP  SE Trading</summary>
    public static readonly Calendar LNS = new Calendar(133);
    /// <summary>England  Virt-x trading holidays  VRS  GBP  SE Trading</summary>
    public static readonly Calendar VRS = new Calendar(134);
    /// <summary>Estonia  Tallinn bank holidays  TNB  EEK  Bank</summary>
    public static readonly Calendar TNB = new Calendar(135);
    /// <summary>Estonia  Tallinn Stock Exchange settlement holidays  TNX  EEK  SE Settlement</summary>
    public static readonly Calendar TNX = new Calendar(136);
    /// <summary>Estonia  Tallinn Stock Exchange trading holidays  TNS  EEK  SE Trading</summary>
    public static readonly Calendar TNS = new Calendar(137);
    /// <summary>Fiji  Suva bank holidays  SQB  FJD  Bank</summary>
    public static readonly Calendar SQB = new Calendar(138);
    /// <summary>Finland  Helsinki bank holidays  HEB  FIM  Bank</summary>
    public static readonly Calendar HEB = new Calendar(139);
    /// <summary>Finland  Helsinki bank holidays plus TARGET  HEI  FIM  Bank</summary>
    public static readonly Calendar HEI = new Calendar(140);
    /// <summary>Finland  Helsinki Exchanges settlement holidays  HEX  FIM  SE Settlement</summary>
    public static readonly Calendar HEX = new Calendar(141);
    /// <summary>Finland  Helsinki Exchanges trading holidays  HES  FIM  SE Trading</summary>
    public static readonly Calendar HES = new Calendar(142);
    /// <summary>France  Euronext (Paris) settlement holidays  PAX  FRF  SE Settlement</summary>
    public static readonly Calendar PAX = new Calendar(143);
    /// <summary>France  Euronext (Paris) trading holidays  PAS  FRF  SE Trading</summary>
    public static readonly Calendar PAS = new Calendar(144);
    /// <summary>France  Paris bank holidays  PAB  FRF  Bank</summary>
    public static readonly Calendar PAB = new Calendar(145);
    /// <summary>France  Paris bank holidays plus TARGET  PAI  FRF  Bank</summary>
    public static readonly Calendar PAI = new Calendar(146);
    /// <summary>Georgia  Tbilisi bank holidays  TBB  GEL  Bank</summary>
    public static readonly Calendar TBB = new Calendar(147);
    /// <summary>Germany  Berlin bank holidays  BNB  DEM  Bank</summary>
    public static readonly Calendar BNB = new Calendar(148);
    /// <summary>Germany  Berlin bank holidays plus TARGET  BNI  DEM  Bank</summary>
    public static readonly Calendar BNI = new Calendar(149);
    /// <summary>Germany  Deutsche Borse (Frankfurt) settlement holidays  FRX  DEM  SE Settlement</summary>
    public static readonly Calendar FRX = new Calendar(150);
    /// <summary>Germany  Deutsche Borse (Frankfurt) trading holidays  FRS  DEM  SE Trading</summary>
    public static readonly Calendar FRS = new Calendar(151);
    /// <summary>Germany  Dusseldorf bank holidays  DSB  DEM  Bank</summary>
    public static readonly Calendar DSB = new Calendar(152);
    /// <summary>Germany  Dusseldorf bank holidays plus TARGET  DSI  DEM  Bank</summary>
    public static readonly Calendar DSI = new Calendar(153);
    /// <summary>Germany  Eurex trading holidays (CONF contracts)  SOO  DEM  Futures Trading</summary>
    public static readonly Calendar SOO = new Calendar(154);
    /// <summary>Germany  Eurex trading holidays (Dutch contracts)  SOG  DEM  Futures Trading</summary>
    public static readonly Calendar SOG = new Calendar(155);
    /// <summary>Germany  Eurex trading holidays (Euribor contracts)  SOL  DEM  Futures Trading</summary>
    public static readonly Calendar SOL = new Calendar(156);
    /// <summary>Germany  Eurex trading holidays (Finnish contracts)  SOH  DEM  Futures Trading</summary>
    public static readonly Calendar SOH = new Calendar(157);
    /// <summary>Germany  Eurex trading holidays (French contracts)  SOI  DEM  Futures Trading</summary>
    public static readonly Calendar SOI = new Calendar(158);
    /// <summary>Germany  Eurex trading holidays (German equity contracts)  SOM  DEM  Futures Trading</summary>
    public static readonly Calendar SOM = new Calendar(159);
    /// <summary>Germany  Eurex trading holidays (German fixed income contracts)  SOF  DEM  Futures Trading</summary>
    public static readonly Calendar SOF = new Calendar(160);
    /// <summary>Germany  Eurex trading holidays (Italian contracts)  SOJ  DEM  Futures Trading</summary>
    public static readonly Calendar SOJ = new Calendar(161);
    /// <summary>Germany  Eurex trading holidays (STOXX contracts)  SON  DEM  Futures Trading</summary>
    public static readonly Calendar SON = new Calendar(162);
    /// <summary>Germany  Eurex trading holidays (Swiss non-CONF contracts)  SOK  DEM  Futures Trading</summary>
    public static readonly Calendar SOK = new Calendar(163);
    /// <summary>Germany  Frankfurt bank holidays  FRB  DEM  Bank</summary>
    public static readonly Calendar FRB = new Calendar(164);
    /// <summary>Germany  Frankfurt bank holidays plus TARGET  FRI  DEM  Bank</summary>
    public static readonly Calendar FRI = new Calendar(165);
    /// <summary>Germany  Hamburg bank holidays  HBB  DEM  Bank</summary>
    public static readonly Calendar HBB = new Calendar(166);
    /// <summary>Germany  Hamburg bank holidays plus TARGET  HBI  DEM  Bank</summary>
    public static readonly Calendar HBI = new Calendar(167);
    /// <summary>Germany  Munich bank holidays  MUB  DEM  Bank</summary>
    public static readonly Calendar MUB = new Calendar(168);
    /// <summary>Germany  Munich bank holidays plus TARGET  MUI  DEM  Bank</summary>
    public static readonly Calendar MUI = new Calendar(169);
    /// <summary>Germany  Stuttgart bank holidays  SXB  DEM  Bank</summary>
    public static readonly Calendar SXB = new Calendar(170);
    /// <summary>Germany  Stuttgart bank holidays plus TARGET  SXI  DEM  Bank</summary>
    public static readonly Calendar SXI = new Calendar(171);
    /// <summary>Ghana  Accra bank holidays  ACB  GHC  Bank</summary>
    public static readonly Calendar ACB = new Calendar(172);
    /// <summary>Greece  Athens bank holidays  ATB  GRD  Bank</summary>
    public static readonly Calendar ATB = new Calendar(173);
    /// <summary>Greece  Athens bank holidays plus TARGET  ATI  GRD  Bank</summary>
    public static readonly Calendar ATI = new Calendar(174);
    /// <summary>Greece  Athens Stock Exchange settlement holidays  ATX  GRD  SE Settlement</summary>
    public static readonly Calendar ATX = new Calendar(175);
    /// <summary>Greece  Athens Stock Exchange trading holidays  ATS  GRD  SE Trading</summary>
    public static readonly Calendar ATS = new Calendar(176);
    /// <summary>Guatemala  Guatemala City bank holidays  GCB  GTQ  Bank</summary>
    public static readonly Calendar GCB = new Calendar(177);
    /// <summary>Guyana  Georgetown bank holidays  GGB  GYD  Bank</summary>
    public static readonly Calendar GGB = new Calendar(178);
    /// <summary>Honduras  Tegucigalpa bank holidays  TEB  HNL  Bank</summary>
    public static readonly Calendar TEB = new Calendar(179);
    /// <summary>Hong Kong  Hong Kong bank holidays  HKB  HKD  Bank</summary>
    public static readonly Calendar HKB = new Calendar(180);
    /// <summary>Hong Kong  Hong Kong Exchanges (Derivatives) trading holidays  HKF  HKD  Futures Trading</summary>
    public static readonly Calendar HKF = new Calendar(181);
    /// <summary>Hong Kong  Hong Kong Exchanges (stock market) settlement holidays  HKX  HKD  SE Settlement</summary>
    public static readonly Calendar HKX = new Calendar(182);
    /// <summary>Hong Kong  Hong Kong Exchanges (stock market) trading holidays  HKS  HKD  SE Trading</summary>
    public static readonly Calendar HKS = new Calendar(183);
    /// <summary>Hungary  Budapest bank holidays  BDB  HUF  Bank</summary>
    public static readonly Calendar DBD = new Calendar(184);
    /// <summary>Hungary  Budapest Stock Exchange settlement holidays  BDX  HUF  SE Settlement</summary>
    public static readonly Calendar BDX = new Calendar(185);
    /// <summary>Hungary  Budapest Stock Exchange trading holidays  BDS  HUF  SE Trading</summary>
    public static readonly Calendar BDS = new Calendar(186);
    /// <summary>Iceland  Iceland Stock Exchange settlement holidays  RKX  ISK  SE Settlement</summary>
    public static readonly Calendar RKX = new Calendar(187);
    /// <summary>Iceland  Iceland Stock Exchange trading holidays  RKS  ISK  SE Trading</summary>
    public static readonly Calendar RKS = new Calendar(188);
    /// <summary>Iceland  Reykjavik bank holidays  RKB  ISK  Bank</summary>
    public static readonly Calendar RKB = new Calendar(189);
    /// <summary>India  Mumbai bank holidays  BMB  INR  Bank</summary>
    public static readonly Calendar BMB = new Calendar(190);
    /// <summary>India  Mumbai Stock Exchange settlement holidays  BMX  INR  SE Settlement</summary>
    public static readonly Calendar BMX = new Calendar(191);
    /// <summary>India  Mumbai Stock Exchange trading holidays  BMS  INR  SE Trading</summary>
    public static readonly Calendar BMS = new Calendar(192);
    /// <summary>India  National Stock Exchange of India settlement holidays  INX  INR  SE Settlement</summary>
    public static readonly Calendar INX = new Calendar(193);
    /// <summary>India  National Stock Exchange of India trading holidays  INS  INR  SE Trading</summary>
    public static readonly Calendar INS = new Calendar(194);
    /// <summary>Indonesia  Bursa Efek Jakarta settlement holidays  JAX  IDR  SE Settlement</summary>
    public static readonly Calendar JAX = new Calendar(195);
    /// <summary>Indonesia  Bursa Efek Jakarta trading holidays  JAS  IDR  SE Trading</summary>
    public static readonly Calendar JAS = new Calendar(196);
    /// <summary>Indonesia  Jakarta bank holidays  JAB  IDR  Bank</summary>
    public static readonly Calendar JAB = new Calendar(197);
    /// <summary>Iran  Tehran bank holidays  THB  IRR  Bank</summary>
    public static readonly Calendar THB = new Calendar(198);
    /// <summary>Ireland  Dublin bank holidays  DUB  IEP  Bank</summary>
    public static readonly Calendar DUB = new Calendar(199);
    /// <summary>Ireland  Dublin bank holidays plus TARGET  DUI  IEP  Bank</summary>
    public static readonly Calendar DUI = new Calendar(200);
    /// <summary>Ireland  Irish Stock Exchange (Dublin) trading holidays  DUS  IEP  SE Trading</summary>
    public static readonly Calendar DUS = new Calendar(201);
    /// <summary>Ireland  Irish Stock Exchange settlement holidays  DUX  IEP  SE Settlement</summary>
    public static readonly Calendar DUX = new Calendar(202);
    /// <summary>Isle of Man  Douglas bank holidays  DGB  GBP  Bank</summary>
    public static readonly Calendar DGB = new Calendar(203);
    /// <summary>Israel  Jerusalem bank holidays  JEB  ILS  Bank</summary>
    public static readonly Calendar JEB = new Calendar(204);
    /// <summary>Israel  Tel Aviv bank holidays  TAB  ILS  Bank</summary>
    public static readonly Calendar TAB = new Calendar(205);
    /// <summary>Israel  Tel Aviv Stock Exchange settlement holidays  TAX  ILS  SE Settlement</summary>
    public static readonly Calendar TAX = new Calendar(206);
    /// <summary>Israel  Tel Aviv Stock Exchange trading holidays  TAS  ILS  SE Trading</summary>
    public static readonly Calendar TAS = new Calendar(207);
    /// <summary>Italy  Borsa Italiana (Milan) settlement holidays  MIX  ITL  SE Settlement</summary>
    public static readonly Calendar MIX = new Calendar(208);
    /// <summary>Italy  Borsa Italiana (Milan) trading holidays  MIS  ITL  SE Trading</summary>
    public static readonly Calendar MIS = new Calendar(209);
    /// <summary>Italy  Milan bank holidays  MIB  ITL  Bank</summary>
    public static readonly Calendar MIB = new Calendar(210);
    /// <summary>Italy  Milan bank holidays plus TARGET  MII  ITL  Bank</summary>
    public static readonly Calendar MII = new Calendar(211);
    /// <summary>Italy  Rome bank holidays  RMB  ITL  Bank</summary>
    public static readonly Calendar RMB = new Calendar(212);
    /// <summary>Italy  Rome bank holidays plus TARGET  RMI  ITL  Bank</summary>
    public static readonly Calendar RMI = new Calendar(213);
    /// <summary>Italy  Turin bank holidays  TOB  ITL  Bank</summary>
    public static readonly Calendar TOB = new Calendar(214);
    /// <summary>Italy  Turin bank holidays plus TARGET  TOI  ITL  Bank</summary>
    public static readonly Calendar TOI = new Calendar(215);
    /// <summary>Jamaica  Jamaica Stock Exchange settlement holidays  KGX  JMD  SE Settlement</summary>
    public static readonly Calendar KGX = new Calendar(216);
    /// <summary>Jamaica  Jamaica Stock Exchange trading holidays  KGY  JMD  SE Trading</summary>
    public static readonly Calendar KGY = new Calendar(217);
    /// <summary>Jamaica  Kingston bank holidays  KGB  JMD  Bank</summary>
    public static readonly Calendar KGB = new Calendar(218);
    /// <summary>Japan  JASDAQ settlement holidays  JDX  JPY  SE Settlement</summary>
    public static readonly Calendar JDX = new Calendar(219);
    /// <summary>Japan  JASDAQ trading holidays  JDQ  JPY  SE Trading</summary>
    public static readonly Calendar JDQ = new Calendar(220);
    /// <summary>Japan  Nagoya Stock Exchange settlement holidays  NGX  JPY  SE Settlement</summary>
    public static readonly Calendar NGX = new Calendar(221);
    /// <summary>Japan  Nagoya Stock Exchange trading holidays  NGS  JPY  SE Trading</summary>
    public static readonly Calendar NGS = new Calendar(222);
    /// <summary>Japan  Osaka Securities Exchange settlement holidays  OKX  JPY  SE Settlement</summary>
    public static readonly Calendar OKX = new Calendar(223);
    /// <summary>Japan  Osaka Securities Exchange trading holidays  OSA  JPY  Futures Trading</summary>
    public static readonly Calendar OSA = new Calendar(224);
    /// <summary>Japan  Tokyo bank holidays  TKB  JPY  Bank</summary>
    public static readonly Calendar TKB = new Calendar(225);
    /// <summary>Japan  Tokyo Commodity Exchange trading holidays  TCF  JPY  Futures Trading</summary>
    public static readonly Calendar TCF = new Calendar(226);
    /// <summary>Japan  Tokyo Grain Exchange trading holidays  TGF  JPY  Futures Trading</summary>
    public static readonly Calendar TGF = new Calendar(227);
    /// <summary>Japan  Tokyo International Financial Futures Exchange trading holidays  TKF  JPY  Futures Trading</summary>
    public static readonly Calendar TKF = new Calendar(228);
    /// <summary>Japan  Tokyo Stock Exchange settlement holidays  TKX  JPY  SE Settlement</summary>
    public static readonly Calendar TKX = new Calendar(229);
    /// <summary>Japan  Tokyo Stock Exchange trading holidays  TKS  JPY  SE Trading</summary>
    public static readonly Calendar TKS = new Calendar(230);
    /// <summary>Jordan  Amman bank holidays  AAB  JOD  Bank</summary>
    public static readonly Calendar AAB = new Calendar(231);
    /// <summary>Jordan  Amman Stock Exchange settlement holidays  AAX  JOD  SE Settlement</summary>
    public static readonly Calendar AAX = new Calendar(232);
    /// <summary>Jordan  Amman Stock Exchange trading holidays  AAS  JOD  SE Trading</summary>
    public static readonly Calendar AAS = new Calendar(233);
    /// <summary>Kazakhstan  Almaty bank holidays  AYB  KZT  Bank</summary>
    public static readonly Calendar AYB = new Calendar(234);
    /// <summary>Kenya  Nairobi bank holidays  NRB  KES  Bank</summary>
    public static readonly Calendar NRB = new Calendar(235);
    /// <summary>Kenya  Nairobi Stock Exchange settlement holidays  NRX  KES  SE Settlement</summary>
    public static readonly Calendar NRX = new Calendar(236);
    /// <summary>Kenya  Nairobi Stock Exchange trading holidays  NRS  KES  SE Trading</summary>
    public static readonly Calendar NRS = new Calendar(237);
    /// <summary>Kuwait  Kuwait bank holidays  KUB  KWD  Bank</summary>
    public static readonly Calendar KUB = new Calendar(238);
    /// <summary>Kyrgyzstan  Bishkek bank holidays  BQB  KGS  Bank</summary>
    public static readonly Calendar BQB = new Calendar(239);
    /// <summary>Latvia  Riga bank holidays  RGB  LVL  Bank</summary>
    public static readonly Calendar RGB = new Calendar(240);
    /// <summary>Latvia  Riga Stock Exchange settlement holidays  RGX  LVL  SE Settlement</summary>
    public static readonly Calendar RGX = new Calendar(241);
    /// <summary>Latvia  Riga Stock Exchange trading holidays  RGS  LVL  SE Trading</summary>
    public static readonly Calendar RGS = new Calendar(242);
    /// <summary>Lebanon  Beirut bank holidays  BIB  LBP  Bank</summary>
    public static readonly Calendar BIB = new Calendar(243);
    /// <summary>Lebanon  Beirut Stock Exchange settlement holidays  BIX  LBP  SE Settlement</summary>
    public static readonly Calendar BIX = new Calendar(244);
    /// <summary>Lebanon  Beirut Stock Exchange trading holidays  BIS  LBP  SE Trading</summary>
    public static readonly Calendar BIS = new Calendar(245);
    /// <summary>Lesotho  Maseru bank holidays  MSB  LSL  Bank</summary>
    public static readonly Calendar MSB = new Calendar(246);
    /// <summary>Liechtenstein  Vaduz bank holidays  VZB  CHF  Bank</summary>
    public static readonly Calendar VZB = new Calendar(247);
    /// <summary>Lithuania  National Stock Exchange of Lithuania settlement holidays  VNX  LTL  SE Settlement</summary>
    public static readonly Calendar VNX = new Calendar(248);
    /// <summary>Lithuania  National Stock Exchange of Lithuania trading holidays  VNS  LTL  SE Trading</summary>
    public static readonly Calendar VNS = new Calendar(249);
    /// <summary>Lithuania  Vilnius bank holidays  VNB  LTL  Bank</summary>
    public static readonly Calendar VNB = new Calendar(250);
    /// <summary>Luxembourg  Bourse de Luxembourg trading holidays  LXS  LUF  SE Trading</summary>
    public static readonly Calendar LXS = new Calendar(251);
    /// <summary>Luxembourg  Clearstream holidays  CED  LUF  Other</summary>
    public static readonly Calendar CED = new Calendar(252);
    /// <summary>Luxembourg  Luxembourg bank holidays  LXB  LUF  Bank</summary>
    public static readonly Calendar LXB = new Calendar(253);
    /// <summary>Luxembourg  Luxembourg bank holidays plus TARGET  LXI  LUF  Bank</summary>
    public static readonly Calendar LXI = new Calendar(254);
    /// <summary>Macau  Macau bank holidays  MAB  MOP  Bank</summary>
    public static readonly Calendar MAB = new Calendar(255);
    /// <summary>Macedonia  Skopje bank holidays  SKB  MKD  Bank</summary>
    public static readonly Calendar SKB = new Calendar(256);
    /// <summary>Madagascar  Antananarivo bank holidays  AVB  MGF  Bank</summary>
    public static readonly Calendar AVB = new Calendar(257);
    /// <summary>Malawi  Lilongwe bank holidays  LIB  MWK  Bank</summary>
    public static readonly Calendar LIB = new Calendar(258);
    /// <summary>Malaysia  Kuala Lumpur bank holidays  KLB  MYR  Bank</summary>
    public static readonly Calendar KLB = new Calendar(259);
    /// <summary>Malaysia  Kuala Lumpur Stock Exchange settlement holidays  KLX  MYR  SE Settlement</summary>
    public static readonly Calendar KLX = new Calendar(260);
    /// <summary>Malaysia  Kuala Lumpur Stock Exchange trading holidays  KLS  MYR  SE Trading</summary>
    public static readonly Calendar KLS = new Calendar(261);
    /// <summary>Malaysia  Labuan bank holidays  LAB  MYR  Bank</summary>
    public static readonly Calendar LAB = new Calendar(262);
    /// <summary>Malta  Malta Stock Exchange settlement holidays  VTX  MTL  SE Settlement</summary>
    public static readonly Calendar VTX = new Calendar(263);
    /// <summary>Malta  Malta Stock Exchange trading holidays  VTS  MTL  SE Trading</summary>
    public static readonly Calendar VTS = new Calendar(264);
    /// <summary>Malta  Valletta bank holidays  VTB  MTL  Bank</summary>
    public static readonly Calendar VTB = new Calendar(265);
    /// <summary>Mauritius  Port Louis bank holidays  PLB  MUR  Bank</summary>
    public static readonly Calendar PLB = new Calendar(266);
    /// <summary>Mauritius  Stock Exchange of Mauritius settlement holidays  PLX  MUR  SE Settlement</summary>
    public static readonly Calendar PLX = new Calendar(267);
    /// <summary>Mauritius  Stock Exchange of Mauritius trading holidays  PLS  MUR  SE Trading</summary>
    public static readonly Calendar PLS = new Calendar(268);
    /// <summary>Mexico  Bolsa Mexicana de Valores settlement holidays  MXX  MXN  SE Settlement</summary>
    public static readonly Calendar MXX = new Calendar(269);
    /// <summary>Mexico  Bolsa Mexicana de Valores trading holidays  MXS  MXN  SE Trading</summary>
    public static readonly Calendar MXS = new Calendar(270);
    /// <summary>Mexico  Mexico City bank holidays  MXB  MXN  Bank</summary>
    public static readonly Calendar MXB = new Calendar(271);
    /// <summary>Miscellaneous  ECU bank holidays  ECU  XEU  Bank</summary>
    public static readonly Calendar ECU = new Calendar(272);
    /// <summary>Miscellaneous  GLOBUS holidays  GLO  XCD  Bank</summary>
    public static readonly Calendar GLO = new Calendar(273);
    /// <summary>Miscellaneous  STOXX Index non-publication days  SXA  EUR  Other</summary>
    public static readonly Calendar SXA = new Calendar(274);
    /// <summary>Miscellaneous  TARGET holidays  TGT  EUR  Bank</summary>
    public static readonly Calendar TGT = new Calendar(275);
    /// <summary>Moldova  Kishinev bank holidays  KIB  MDL  Bank</summary>
    public static readonly Calendar KIB = new Calendar(276);
    /// <summary>Morocco  Casablanca bank holidays  CCB  MAD  Bank</summary>
    public static readonly Calendar CCB = new Calendar(277);
    /// <summary>Morocco  Casablanca Stock Exchange settlement holidays  CCX  MAD  SE Settlement</summary>
    public static readonly Calendar CCX = new Calendar(278);
    /// <summary>Morocco  Casablanca Stock Exchange trading holidays  CCS  MAD  SE Trading</summary>
    public static readonly Calendar CCS = new Calendar(279);
    /// <summary>Mozambique  Maputo bank holidays  MPB  MZM  Bank</summary>
    public static readonly Calendar MPB = new Calendar(280);
    /// <summary>Namibia  Namibian Stock Exchange settlement holidays  WIX  NAD  SE Settlement</summary>
    public static readonly Calendar WIX = new Calendar(281);
    /// <summary>Namibia  Namibian Stock Exchange trading holidays  WIS  NAD  SE Trading</summary>
    public static readonly Calendar WIS = new Calendar(282);
    /// <summary>Namibia  Windhoek bank holidays  WIB  NAD  Bank</summary>
    public static readonly Calendar WIB = new Calendar(283);
    /// <summary>Nepal  Kathmandu bank holidays  KTB  NPR  Bank</summary>
    public static readonly Calendar KTB = new Calendar(284);
    /// <summary>Netherlands  Amsterdam bank holidays  AMB  NLG  Bank</summary>
    public static readonly Calendar AMB = new Calendar(285);
    /// <summary>Netherlands  Amsterdam bank holidays plus TARGET  AMI  NLG  Bank</summary>
    public static readonly Calendar AMI = new Calendar(286);
    /// <summary>Netherlands  Euronext (Amsterdam) settlement holidays  AMX  NLG  SE Settlement</summary>
    public static readonly Calendar AMX = new Calendar(287);
    /// <summary>Netherlands  Euronext (Amsterdam) trading holidays  AMS  NLG  SE Trading</summary>
    public static readonly Calendar AMS = new Calendar(288);
    /// <summary>Netherlands  Rotterdam bank holidays  ROB  NLG  Bank</summary>
    public static readonly Calendar ROB = new Calendar(289);
    /// <summary>Netherlands  Rotterdam bank holidays plus TARGET  ROI  NLG  Bank</summary>
    public static readonly Calendar ROI = new Calendar(290);
    /// <summary>Netherlands  The Hague bank holidays  DHB  NLG  Bank</summary>
    public static readonly Calendar DBH = new Calendar(291);
    /// <summary>Netherlands  The Hague bank holidays plus TARGET  DHI  NLG  Bank</summary>
    public static readonly Calendar DHI = new Calendar(292);
    /// <summary>Netherlands Antilles  Curacao bank holidays  CUB  ANG  Bank</summary>
    public static readonly Calendar CUB = new Calendar(293);
    /// <summary>New Zealand  Auckland bank holidays  AUB  NZD  Bank</summary>
    public static readonly Calendar AUB = new Calendar(294);
    /// <summary>New Zealand  Christchurch bank holidays  CHB  NZD  Bank</summary>
    public static readonly Calendar CHB = new Calendar(295);
    /// <summary>New Zealand  Dunedin bank holidays  DNB  NZD  Bank</summary>
    public static readonly Calendar DNB = new Calendar(296);
    /// <summary>New Zealand  New Zealand RTGS payments system holidays  NNZ  NZD  Bank</summary>
    public static readonly Calendar NNZ = new Calendar(297);
    /// <summary>New Zealand  New Zealand Stock Exchange settlement holidays  WEX  NZD  SE Settlement</summary>
    public static readonly Calendar WEX = new Calendar(298);
    /// <summary>New Zealand  New Zealand Stock Exchange trading holidays  WES  NZD  SE Trading</summary>
    public static readonly Calendar WES = new Calendar(299);
    /// <summary>New Zealand  Wellington bank holidays  WEB  NZD  Bank</summary>
    public static readonly Calendar WEB = new Calendar(300);
    /// <summary>Nicaragua  Managua bank holidays  MGB  NIO  Bank</summary>
    public static readonly Calendar MGB = new Calendar(301);
    /// <summary>Northern Ireland  Northern Ireland public holidays  NIC  GBP  Other</summary>
    public static readonly Calendar NIC = new Calendar(302);
    /// <summary>Norway  Oslo bank holidays  OSB  NOK  Bank</summary>
    public static readonly Calendar OSB = new Calendar(303);
    /// <summary>Norway  Oslo Bors settlement holidays  OSX  NOK  SE Settlement</summary>
    public static readonly Calendar OSX = new Calendar(304);
    /// <summary>Norway  Oslo Bors trading holidays  OSS  NOK  SE Trading</summary>
    public static readonly Calendar OSS = new Calendar(305);
    /// <summary>Oman  Muscat bank holidays  MCB  OMR  Bank</summary>
    public static readonly Calendar MCB = new Calendar(306);
    /// <summary>Pakistan  Karachi bank holidays  KAB  PKR  Bank</summary>
    public static readonly Calendar KAB = new Calendar(307);
    /// <summary>Pakistan  Karachi Stock Exchange settlement holidays  KAX  PKR  SE Settlement</summary>
    public static readonly Calendar KAX = new Calendar(308);
    /// <summary>Pakistan  Karachi Stock Exchange trading holidays  KAS  PKR  SE Trading</summary>
    public static readonly Calendar KAS = new Calendar(309);
    /// <summary>Panama  Bolsa de Valores de Panama settlement holidays  PCX  PAB  SE Settlement</summary>
    public static readonly Calendar PCX = new Calendar(310);
    /// <summary>Panama  Bolsa de Valores de Panama trading holidays  PCS  PAB  SE Trading</summary>
    public static readonly Calendar PCS = new Calendar(311);
    /// <summary>Panama  Panama City bank holidays  PCB  PAB  Bank</summary>
    public static readonly Calendar PCB = new Calendar(312);
    /// <summary>Papua New Guinea  Port Moresby bank holidays  PMB  PGK  Bank</summary>
    public static readonly Calendar PMB = new Calendar(313);
    /// <summary>Paraguay  Asuncion bank holidays  ASB  PYG  Bank</summary>
    public static readonly Calendar ASB = new Calendar(314);
    /// <summary>Paraguay  Bolsa de Valores y Productos de Asuncion settlement holidays  ASX  PYG  SE Settlement</summary>
    public static readonly Calendar ASX = new Calendar(315);
    /// <summary>Paraguay  Bolsa de Valores y Productos de Asuncion trading holidays  ASS  PYG  SE Trading</summary>
    public static readonly Calendar ASS = new Calendar(316);
    /// <summary>Peru  Bolsa de Valores de Lima settlement holidays  LMX  PEN  SE Settlement</summary>
    public static readonly Calendar LMX = new Calendar(317);
    /// <summary>Peru  Bolsa de Valores de Lima trading holidays  LMS  PEN  SE Trading</summary>
    public static readonly Calendar LMS = new Calendar(318);
    /// <summary>Peru  Lima bank holidays  LMB  PEN  Bank</summary>
    public static readonly Calendar LMB = new Calendar(319);
    /// <summary>Philippines  Manila bank holidays  MNB  PHP  Bank</summary>
    public static readonly Calendar MNB = new Calendar(320);
    /// <summary>Philippines  Philippine Stock Exchange settlement holidays  MNX  PHP  SE Settlement</summary>
    public static readonly Calendar MNX = new Calendar(321);
    /// <summary>Philippines  Philippine Stock Exchange trading holidays  MNS  PHP  SE Trading</summary>
    public static readonly Calendar MNS = new Calendar(322);
    /// <summary>Poland  Warsaw bank holidays  WAB  PLN  Bank</summary>
    public static readonly Calendar WAB = new Calendar(323);
    /// <summary>Poland  Warsaw Stock Exchange settlement holidays  WAX  PLN  SE Settlement</summary>
    public static readonly Calendar WAX = new Calendar(324);
    /// <summary>Poland  Warsaw Stock Exchange trading holidays  WAS  PLN  SE Trading</summary>
    public static readonly Calendar WAS = new Calendar(325);
    /// <summary>Portugal  Euronext (Lisbon) settlement holidays  LSX  PTE  SE Settlement</summary>
    public static readonly Calendar LSX = new Calendar(326);
    /// <summary>Portugal  Euronext (Lisbon) trading holidays  LSS  PTE  SE Trading</summary>
    public static readonly Calendar LSS = new Calendar(327);
    /// <summary>Portugal  Lisbon bank holidays  LSB  PTE  Bank</summary>
    public static readonly Calendar LSB = new Calendar(328);
    /// <summary>Portugal  Lisbon bank holidays plus TARGET  LSI  PTE  Bank</summary>
    public static readonly Calendar LSI = new Calendar(329);
    /// <summary>Puerto Rico  San Juan bank holidays  SUB  USD  Bank</summary>
    public static readonly Calendar SUB = new Calendar(330);
    /// <summary>Qatar  Doha bank holidays  DOB  QAR  Bank</summary>
    public static readonly Calendar DOB = new Calendar(331);
    /// <summary>Romania  Bucharest bank holidays  BCB  ROL  Bank</summary>
    public static readonly Calendar BCB = new Calendar(332);
    /// <summary>Romania  Bucharest Stock Exchange trading holidays  BCS  ROL  SE Trading</summary>
    public static readonly Calendar BCS = new Calendar(333);
    /// <summary>Russia  Moscow bank holidays  MWB  RUB  Bank</summary>
    public static readonly Calendar MWB = new Calendar(334);
    /// <summary>Russia  Russian Trading System settlement holidays  RUX  RUB  SE Settlement</summary>
    public static readonly Calendar RUX = new Calendar(335);
    /// <summary>Russia  Russian Trading System trading holidays  RUS  RUB  SE Trading</summary>
    public static readonly Calendar RUS = new Calendar(336);
    /// <summary>Saudi Arabia  Riyadh bank holidays  RIB  SAR  Bank</summary>
    public static readonly Calendar RIB = new Calendar(337);
    /// <summary>Scotland  Scottish statutory holidays  SCC  GBP  Other</summary>
    public static readonly Calendar SCC = new Calendar(338);
    /// <summary>Seychelles  Victoria bank holidays  VCB  SCR  Bank</summary>
    public static readonly Calendar VCB = new Calendar(339);
    /// <summary>Sierra Leone  Freetown bank holidays  FTB  SLL  Bank</summary>
    public static readonly Calendar FTB = new Calendar(340);
    /// <summary>Singapore  Singapore bank holidays  SIB  SGD  Bank</summary>
    public static readonly Calendar SIB = new Calendar(341);
    /// <summary>Singapore  Singapore Commodity Exchange trading holidays  SIF  SGD  Futures Trading</summary>
    public static readonly Calendar SIF = new Calendar(342);
    /// <summary>Singapore  Singapore Exchange (stock market) settlement holidays  SIX  SGD  SE Settlement</summary>
    public static readonly Calendar SIX = new Calendar(343);
    /// <summary>Singapore  Singapore Exchange (stock market) trading holidays  SIS  SGD  SE Trading</summary>
    public static readonly Calendar SIS = new Calendar(344);
    /// <summary>Singapore  Singapore Exchange trading holidays (Eurodollar derivatives)  SIN  SGD  Futures Trading</summary>
    public static readonly Calendar SIN = new Calendar(345);
    /// <summary>Singapore  Singapore Exchange trading holidays (Euroyen derivatives)  SIQ  SGD  Futures Trading</summary>
    public static readonly Calendar SIQ = new Calendar(346);
    /// <summary>Singapore  Singapore Exchange trading holidays (Japanese derivatives)  SIM  SGD  Futures Trading</summary>
    public static readonly Calendar SIM = new Calendar(347);
    /// <summary>Singapore  Singapore Exchange trading holidays (Singapore derivatives)  SIO  SGD  Futures Trading</summary>
    public static readonly Calendar SIO = new Calendar(348);
    /// <summary>Singapore  Singapore Exchange trading holidays (Taiwanese derivatives)  SIP  SGD  Futures Trading</summary>
    public static readonly Calendar SIP = new Calendar(349);
    /// <summary>Slovak Republic  Bratislava bank holidays  BTB  SKK  Bank</summary>
    public static readonly Calendar BTB = new Calendar(350);
    /// <summary>Slovak Republic  Bratislava Stock Exchange trading holidays  BTS  SKK  SE Trading</summary>
    public static readonly Calendar BTS = new Calendar(351);
    /// <summary>Slovenia  Ljubljana bank holidays  LBB  SIT  Bank</summary>
    public static readonly Calendar LBB = new Calendar(352);
    /// <summary>Slovenia  Ljubljana Stock Exchange settlement holidays  LBX  SIT  SE Settlement</summary>
    public static readonly Calendar LBX = new Calendar(353);
    /// <summary>Slovenia  Ljubljana Stock Exchange trading holidays  LBS  SIT  SE Trading</summary>
    public static readonly Calendar LBS = new Calendar(354);
    /// <summary>South Africa  Johannesburg bank holidays  JOB  ZAR  Bank</summary>
    public static readonly Calendar JOB = new Calendar(355);
    /// <summary>South Africa  Johannesburg Stock Exchange settlement holidays  JOX  ZAR  SE Settlement</summary>
    public static readonly Calendar JOX = new Calendar(356);
    /// <summary>South Africa  Johannesburg Stock Exchange trading holidays  JOS  ZAR  SE Trading</summary>
    public static readonly Calendar JOS = new Calendar(357);
    /// <summary>South Korea  Korea Stock Exchange settlement holidays  SEX  KRW  SE Settlement</summary>
    public static readonly Calendar SEX = new Calendar(358);
    /// <summary>South Korea  Korea Stock Exchange trading holidays  SES  KRW  SE Trading</summary>
    public static readonly Calendar SES = new Calendar(359);
    /// <summary>South Korea  KOSDAQ settlement holidays  KDX  KRW  SE Settlement</summary>
    public static readonly Calendar KDX = new Calendar(360);
    /// <summary>South Korea  KOSDAQ trading holidays  KDQ  KRW  SE Trading</summary>
    public static readonly Calendar KDQ = new Calendar(361);
    /// <summary>South Korea  Seoul bank holidays  SEB  KRW  Bank</summary>
    public static readonly Calendar SEB = new Calendar(362);
    /// <summary>Spain  Barcelona bank holidays  BLB  ESP  Bank</summary>
    public static readonly Calendar BLB = new Calendar(363);
    /// <summary>Spain  Barcelona bank holidays plus TARGET  BLI  ESP  Bank</summary>
    public static readonly Calendar BLI = new Calendar(364);
    /// <summary>Spain  Madrid bank holidays  MDB  ESP  Bank</summary>
    public static readonly Calendar MDB = new Calendar(365);
    /// <summary>Spain  Madrid bank holidays plus TARGET  MDI  ESP  Bank</summary>
    public static readonly Calendar MDI = new Calendar(366);
    /// <summary>Spain  MEFF trading holidays (IBEX contracts)  MEF  ESP  Futures Trading</summary>
    public static readonly Calendar MEF = new Calendar(367);
    /// <summary>Spain  Mercado Continuo (SIBE) settlement holidays  MDX  ESP  SE Settlement</summary>
    public static readonly Calendar MDX = new Calendar(368);
    /// <summary>Spain  Mercado Continuo (SIBE) trading holidays  MDS  ESP  SE Trading</summary>
    public static readonly Calendar MDS = new Calendar(369);
    /// <summary>Sri Lanka  Colombo bank holidays  CMB  LKR  Bank</summary>
    public static readonly Calendar CMB = new Calendar(370);
    /// <summary>Sri Lanka  Colombo Stock Exchange settlement holidays  CMX  LKR  SE Settlement</summary>
    public static readonly Calendar CMX = new Calendar(371);
    /// <summary>Sri Lanka  Colombo Stock Exchange trading holidays  CMS  LKR  SE Trading</summary>
    public static readonly Calendar CMS = new Calendar(372);
    /// <summary>Sweden  Stockholm bank holidays  STB  SEK  Bank</summary>
    public static readonly Calendar STB = new Calendar(373);
    /// <summary>Sweden  Stockholmsborsen settlement holidays  STX  SEK  SE Settlement</summary>
    public static readonly Calendar STX = new Calendar(374);
    /// <summary>Sweden  Stockholmsborsen trading holidays  STS  SEK  SE Trading</summary>
    public static readonly Calendar STS = new Calendar(375);
    /// <summary>Switzerland  Basle bank holidays  BSB  CHF  Bank</summary>
    public static readonly Calendar BSB = new Calendar(376);
    /// <summary>Switzerland  Berne bank holidays  BZB  CHF  Bank</summary>
    public static readonly Calendar BZB = new Calendar(377);
    /// <summary>Switzerland  Geneva bank holidays  GEB  CHF  Bank</summary>
    public static readonly Calendar GEB = new Calendar(378);
    /// <summary>Switzerland  Lugano bank holidays  LUB  CHF  Bank</summary>
    public static readonly Calendar LUB = new Calendar(379);
    /// <summary>Switzerland  Swiss Exchange settlement holidays  ZUX  CHF  SE Settlement</summary>
    public static readonly Calendar ZUX = new Calendar(380);
    /// <summary>Switzerland  Swiss Exchange trading holidays  ZUS  CHF  SE Trading</summary>
    public static readonly Calendar ZUS = new Calendar(381);
    /// <summary>Switzerland  Swiss Interbank Clearing System holidays  NCH  CHF  Bank</summary>
    public static readonly Calendar NCH = new Calendar(382);
    /// <summary>Switzerland  Zurich bank holidays  ZUB  CHF  Bank</summary>
    public static readonly Calendar ZUB = new Calendar(383);
    /// <summary>Syria  Damascus bank holidays  DAB  SYP  Bank</summary>
    public static readonly Calendar DAB = new Calendar(384);
    /// <summary>Taiwan  Taipei bank holidays  TPB  TWD  Bank</summary>
    public static readonly Calendar TPB = new Calendar(385);
    /// <summary>Taiwan  Taiwan Stock Exchange settlement holidays  TPX  TWD  SE Settlement</summary>
    public static readonly Calendar TPX = new Calendar(386);
    /// <summary>Taiwan  Taiwan Stock Exchange trading holidays  TPS  TWD  SE Trading</summary>
    public static readonly Calendar TPS = new Calendar(387);
    /// <summary>Tajikistan  Dushanbe bank holidays  DZB  TJS  Bank</summary>
    public static readonly Calendar DZB = new Calendar(388);
    /// <summary>Tanzania  Dar es Salaam bank holidays  DEB  TZS  Bank</summary>
    public static readonly Calendar DEB = new Calendar(389);
    /// <summary>Thailand  Bangkok bank holidays  BKB  THB  Bank</summary>
    public static readonly Calendar BKB = new Calendar(390);
    /// <summary>Thailand  Stock Exchange of Thailand settlement holidays  BKX  THB  SE Settlement</summary>
    public static readonly Calendar BKX = new Calendar(391);
    /// <summary>Thailand  Stock Exchange of Thailand trading holidays  BKS  THB  SE Trading</summary>
    public static readonly Calendar BKS = new Calendar(392);
    /// <summary>Trinidad and Tobago  Port of Spain bank holidays  PSB  TTD  Bank</summary>
    public static readonly Calendar PSB = new Calendar(393);
    /// <summary>Tunisia  Tunis bank holidays  TUB  TND  Bank</summary>
    public static readonly Calendar TUB = new Calendar(394);
    /// <summary>Turkey  Ankara bank holidays  ANB  TRL  Bank</summary>
    public static readonly Calendar ANB = new Calendar(395);
    /// <summary>Turkey  Istanbul bank holidays  ISB  TRL  Bank</summary>
    public static readonly Calendar ISB = new Calendar(396);
    /// <summary>Turkey  Istanbul Stock Exchange settlement holidays  ISX  TRL  SE Settlement</summary>
    public static readonly Calendar ISX = new Calendar(397);
    /// <summary>Turkey  Istanbul Stock Exchange trading holidays  ISS  TRL  SE Trading</summary>
    public static readonly Calendar ISS = new Calendar(398);
    /// <summary>Turkmenistan  Ashkhabad bank holidays  AHB  TMM  Bank</summary>
    public static readonly Calendar AHB = new Calendar(399);
    /// <summary>Uganda  Kampala bank holidays  KPB  UGX  Bank</summary>
    public static readonly Calendar KPB = new Calendar(400);
    /// <summary>Ukraine  Kiev bank holidays  KVB  UAH  Bank</summary>
    public static readonly Calendar KVB = new Calendar(401);
    /// <summary>United Arab Emirates  Abu Dhabi bank holidays  AEB  AED  Bank</summary>
    public static readonly Calendar AEB = new Calendar(402);
    /// <summary>United Arab Emirates  Dubai bank holidays  DBB  AED  Bank</summary>
    public static readonly Calendar DBB = new Calendar(403);
    /// <summary>United States  American Stock Exchange trading holidays  AMQ  USD  SE Trading</summary>
    public static readonly Calendar AMQ = new Calendar(404);
    /// <summary>United States  Boston Stock Exchange trading holidays  BZS  USD  SE Trading</summary>
    public static readonly Calendar BZS = new Calendar(405);
    /// <summary>United States  Chicago Board of Trade trading holidays (Agricultural contracts)  MAS  USD  Futures Trading</summary>
    public static readonly Calendar MAS = new Calendar(406);
    /// <summary>United States  Chicago Board of Trade trading holidays (Financial contracts)  CBT  USD  Futures Trading</summary>
    public static readonly Calendar CBT = new Calendar(407);
    /// <summary>United States  Chicago Board of Trade trading holidays (Metals contracts)  MAR  USD  Futures Trading</summary>
    public static readonly Calendar MAR = new Calendar(408);
    /// <summary>United States  Chicago Board of Trade trading holidays (Stock index contracts)  CBV  USD  Futures Trading</summary>
    public static readonly Calendar CBV = new Calendar(409);
    /// <summary>United States  Chicago Board Options Exchange trading holidays (Equity contracts)  CBO  USD  Futures Trading</summary>
    public static readonly Calendar CBO = new Calendar(410);
    /// <summary>United States  Chicago Board Options Exchange trading holidays (Interest Rate contracts)  CBP  USD  Futures Trading</summary>
    public static readonly Calendar CBP = new Calendar(411);
    /// <summary>United States  Chicago Mercantile Exch. trading holidays (Agricultural contracts)  CMH  USD  Futures Trading</summary>
    public static readonly Calendar CMH = new Calendar(412);
    /// <summary>United States  Chicago Mercantile Exch. trading holidays (Currency contracts)  CMG  USD  Futures Trading</summary>
    public static readonly Calendar CMG = new Calendar(413);
    /// <summary>United States  Chicago Mercantile Exch. trading holidays (Equity contracts)  CMF  USD  Futures Trading</summary>
    public static readonly Calendar CMF = new Calendar(414);
    /// <summary>United States  Chicago Mercantile Exch. trading holidays (Interest rate contracts)  CME  USD  Futures Trading</summary>
    public static readonly Calendar CME = new Calendar(415);
    /// <summary>United States  Citrus Associates of the New York Cotton Exchange trading holidays  NYG  USD  Futures Trading</summary>
    public static readonly Calendar NYG = new Calendar(416);
    /// <summary>United States  Coffee Sugar and Cocoa Exchange trading holidays  CSF  USD  Futures Trading</summary>
    public static readonly Calendar CSF = new Calendar(417);
    /// <summary>United States  Commercial paper (H15) non-publication days  CPA  USD  Other</summary>
    public static readonly Calendar CPA = new Calendar(418);
    /// <summary>United States  FINEX trading holidays (New York Day Session)  FXD  USD  Futures Trading</summary>
    public static readonly Calendar FXD = new Calendar(419);
    /// <summary>United States  International Securities Exchange trading holidays  ISQ  USD  SE Trading</summary>
    public static readonly Calendar ISQ = new Calendar(420);
    /// <summary>United States  Kansas City Board of Trade trading holidays (Wheat contracts)  KCT  USD  Futures Trading</summary>
    public static readonly Calendar KCT = new Calendar(421);
    /// <summary>United States  Minneapolis Grain Exchange trading holidays  MGE  USD  Futures Trading</summary>
    public static readonly Calendar MGE = new Calendar(422);
    /// <summary>United States  NASDAQ trading holidays  NDQ  USD  SE Trading</summary>
    public static readonly Calendar NDQ = new Calendar(423);
    /// <summary>United States  NERC Off-Peak days  NER  USD  Other</summary>
    public static readonly Calendar NER = new Calendar(424);
    /// <summary>United States  New York bank holidays  NYB  USD  Bank</summary>
    public static readonly Calendar NYB = new Calendar(425);
    /// <summary>United States  New York Cotton Exchange trading holidays  NYF  USD  Futures Trading</summary>
    public static readonly Calendar NYF = new Calendar(426);
    /// <summary>United States  New York Futures Exchange trading holidays (Equity contracts)  NYE  USD  Futures Trading</summary>
    public static readonly Calendar NYE = new Calendar(427);
    /// <summary>United States  New York Mercantile Exchange trading holidays  NYM  USD  Futures Trading</summary>
    public static readonly Calendar NYM = new Calendar(428);
    /// <summary>United States  New York Stock Exchange settlement holidays  NYX  USD  SE Settlement</summary>
    public static readonly Calendar NYX = new Calendar(429);
    /// <summary>United States  New York Stock Exchange trading holidays  NYS  USD  SE Trading</summary>
    public static readonly Calendar NYS = new Calendar(430);
    /// <summary>United States  Pacific Exchange trading holidays  PFS  USD  SE Trading</summary>
    public static readonly Calendar PFS = new Calendar(431);
    /// <summary>United States  Philadelphia Stock Exchange trading holidays  PHS  USD  SE Trading</summary>
    public static readonly Calendar PHS = new Calendar(432);
    /// <summary>United States  US Treasuries (BMA recommended closings)  BMA  USD  Other</summary>
    public static readonly Calendar BMA = new Calendar(433);
    /// <summary>Uruguay  Bolsa de Valores de Montevideo settlement holidays  MVX  UYU  SE Settlement</summary>
    public static readonly Calendar MVX = new Calendar(434);
    /// <summary>Uruguay  Bolsa de Valores de Montevideo trading holidays  MVS  UYU  SE Trading</summary>
    public static readonly Calendar MVS = new Calendar(435);
    /// <summary>Uruguay  Montevideo bank holidays  MVB  UYU  Bank</summary>
    public static readonly Calendar MVB = new Calendar(436);
    /// <summary>Uzbekistan  Tashkent bank holidays  TTB  UZS  Bank</summary>
    public static readonly Calendar TTB = new Calendar(437);
    /// <summary>Vanuatu  Port Vila bank holidays  PVB  VUV  Bank</summary>
    public static readonly Calendar PVB = new Calendar(438);
    /// <summary>Venezuela  Bolsa de Valores de Caracas settlement holidays  CAX  VEB  SE Settlement</summary>
    public static readonly Calendar CAX = new Calendar(439);
    /// <summary>Venezuela  Bolsa de Valores de Caracas trading holidays  CAS  VEB  SE Trading</summary>
    public static readonly Calendar CAS = new Calendar(440);
    /// <summary>Venezuela  Caracas bank holidays  CAB  VEB  Bank</summary>
    public static readonly Calendar CAB = new Calendar(441);
    /// <summary>Vietnam  Hanoi bank holidays  HAB  VND  Bank</summary>
    public static readonly Calendar HAB = new Calendar(442);
    /// <summary>Yugoslavia  Belgrade bank holidays  BYB  YUM  Bank</summary>
    public static readonly Calendar BYB = new Calendar(443);
    /// <summary>Zambia  Lusaka bank holidays  LKB  ZMK  Bank</summary>
    public static readonly Calendar LKB = new Calendar(444);
    /// <summary>Zimbabwe  Harare bank holidays  HRB  ZWD  Bank</summary>
    public static readonly Calendar HRB = new Calendar(445);
    /// <summary>Zimbabwe  Zimbabwe Stock Exchange settlement holidays  HRX  ZWD  SE Settlement</summary>
    public static readonly Calendar HRX = new Calendar(446);
    /// <summary>Zimbabwe  Zimbabwe Stock Exchange trading holidays  HRS  ZWD  SE Trading</summary>
    public static readonly Calendar HRS = new Calendar(447);
    /// <summary>Japanese holidays  calendar published on https://cdsmodel.com to compute standard Japan CDS </summary>
    public static readonly Calendar TYO = new Calendar(448);

    #endregion Build-in Calendars
  }
}
