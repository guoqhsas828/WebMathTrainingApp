/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   The ISDA roll convention
  /// </summary>
  ///
  /// <remarks>
  ///   <para>The method for determining a sequence of interest period end dates.</para>
  ///   <para>It is used in conjunction with a specified frequency and the regular
  ///   period start date of a calculation period, e.g. semi-annual IMM roll dates.</para>
  /// </remarks>
  /// 
  public enum CycleRule
  {
    /// <summary>The roll convention is not specified; if required, it will be derived based on other parameters</summary>
    None,

    /// <summary>IMM Settlement Dates. The third Wednesday of the (delivery) month</summary>
    IMM,

    /// <summary>The last trading day/expiration day of the Canadian Derivatives Exchange (Bourse de Montreal Inc) Three-month Canadian Bankers' Acceptance Futures (Ticker Symbol BAX). The second London banking day prior to the third Wednesday of the contract month. If the determined day is a Bourse or bank holiday in Montreal or Toronto, the last trading day shall be the previous bank business day. Per Canadian Derivatives Exchange BAX contract specification</summary>
    IMMCad,

    /// <summary>Roll days are determined according to the FRN Convention or Eurodollar Convention as described in ISDA 2000 definitions</summary>
    FRN,

    /// <summary>Sydney Futures Exchange 90-Day Bank Accepted Bill Futures Settlement Dates. The second Friday of the (delivery) month</summary>
    SFE,

    /// <summary>13-week and 26-week U.S. Treasury Bill Auction Dates. Each Monday except for U.S. (New York) holidays when it will occur on a Tuesday</summary>
    TBill,

    /// <summary>Rolls on the 1st day of the month</summary>
    First,

    /// <summary>Rolls on the 2nd day of the month</summary>
    Second,

    /// <summary>Rolls on the 3rd day of the month</summary>
    Third,

    /// <summary>Rolls on the 4th day of the month</summary>
    Fourth,

    /// <summary>Rolls on the 5th day of the month</summary>
    Fifth,

    /// <summary>Rolls on the 6th day of the month</summary>
    Sixth,

    /// <summary>Rolls on the 7th day of the month</summary>
    Seventh,

    /// <summary>Rolls on the 8th day of the month</summary>
    Eighth,

    /// <summary>Rolls on the 9th day of the month</summary>
    Ninth,

    /// <summary>Rolls on the 10th day of the month</summary>
    Tenth,

    /// <summary>Rolls on the 11th day of the month</summary>
    Eleventh,

    /// <summary>Rolls on the 12th day of the month</summary>
    Twelfth,

    /// <summary>Rolls on the 13th day of the month</summary>
    Thirteenth,

    /// <summary>Rolls on the 14th day of the month</summary>
    Fourteenth,

    /// <summary>Rolls on the 15th day of the month</summary>
    Fifteenth,

    /// <summary>Rolls on the 16th day of the month</summary>
    Sixteenth,

    /// <summary>Rolls on the 17th day of the month</summary>
    Seventeenth,

    /// <summary>Rolls on the 18th day of the month</summary>
    Eighteenth,

    /// <summary>Rolls on the 19th day of the month</summary>
    Nineteenth,

    /// <summary>Rolls on the 20th day of the month</summary>
    Twentieth,

    /// <summary>Rolls on the 21st day of the month</summary>
    TwentyFirst,

    /// <summary>Rolls on the 22nd day of the month</summary>
    TwentySecond,

    /// <summary>Rolls on the 23rd day of the month</summary>
    TwentyThird,

    /// <summary>Rolls on the 24th day of the month</summary>
    TwentyFourth,

    /// <summary>Rolls on the 25th day of the month</summary>
    TwentyFifth,

    /// <summary>Rolls on the 26th day of the month</summary>
    TwentySixth,

    /// <summary>Rolls on the 27th day of the month</summary>
    TwentySeventh,

    /// <summary>Rolls on the 28th day of the month</summary>
    TwentyEighth,

    /// <summary>Rolls on the 29th day of the month</summary>
    TwentyNinth,

    /// <summary>Rolls on the 30th day of the month</summary>
    Thirtieth,

    /// <summary>Rolls on month end dates irrespective of the length of the month and the previous roll day</summary>
    EOM,

    /// <summary>Rolling weekly on a Monday</summary>
    Monday,

    /// <summary>Rolling weekly on a Tuesday</summary>
    Tuesday,

    /// <summary>Rolling weekly on a Wednesday</summary>
    Wednesday,

    /// <summary>Rolling weekly on a Thursday</summary>
    Thursday,

    /// <summary>Rolling weekly on a Friday</summary>
    Friday,

    /// <summary>Rolling weekly on a Saturday</summary>
    Saturday,

    /// <summary>Rolling weekly on a Sunday</summary>
    Sunday,

    /// <summary>The last trading day of the Sydney Futures Exchange 90 Day Bank Accepted Bills Futures contract (see http://www.sfe.com.au/content/sfe/trading/con_specs.pdf). One Sydney business day preceding the second Friday of the relevant settlement month</summary>
    IMMAUD,

    /// <summary>	The last trading day of the Sydney Futures Exchange NZ 90 Day Bank Bill Futures contract (see http://www.sfe.com.au/content/sfe/trading/con_specs.pdf). The first Wednesday after the ninth day of the relevant settlement month.</summary>
    IMMNZD
  }
}
