// 
// DayOfMonth.cs
// Copyright (c)    2014. All rights reserved.
// 

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Specification for a day of month
  /// </summary>
  /// <remarks>
  /// <para>This is useful to specify exchange contract dates.</para>
  /// <para>This is related to <see cref="CycleRule"/> as some swaps use
  /// exchange contract related dates. <see cref="CycleRule"/>, however, is an ISDA
  /// defined concept and relavent for ISDA related date calculations.</para>
  /// </remarks>
  public enum DayOfMonth
  {
    // Note: Day of Month calculations in Dt.cs rely on this order
    /// <summary>1st day of the month</summary>
    First = 1,
    /// <summary>2nd day of the month</summary>
    Second,
    /// <summary>3rd day of the month</summary>
    Third,
    /// <summary>4th day of the month</summary>
    Fourth,
    /// <summary>5th day of the month</summary>
    Fifth,
    /// <summary>6th day of the month</summary>
    Sixth,
    /// <summary>7th day of the month</summary>
    Seventh,
    /// <summary>8th day of the month</summary>
    Eighth,
    /// <summary>9th day of the month</summary>
    Ninth,
    /// <summary>10th day of the month</summary>
    Tenth,
    /// <summary>11th day of the month</summary>
    Eleventh,
    /// <summary>12th day of the month</summary>
    Twelfth,
    /// <summary>13th day of the month</summary>
    Thirteenth,
    /// <summary>14th day of the month</summary>
    Fourteenth,
    /// <summary>15th day of the month</summary>
    Fifteenth,
    /// <summary>16th day of the month</summary>
    Sixteenth,
    /// <summary>17th day of the month</summary>
    Seventeenth,
    /// <summary>18th day of the month</summary>
    Eighteenth,
    /// <summary>19th day of the month</summary>
    Nineteenth,
    /// <summary>20th day of the month</summary>
    Twentieth,
    /// <summary>21st day of the month</summary>
    TwentyFirst,
    /// <summary>22nd day of the month</summary>
    TwentySecond,
    /// <summary>23rd day of the month</summary>
    TwentyThird,
    /// <summary>24th day of the month</summary>
    TwentyFourth,
    /// <summary>25th day of the month</summary>
    TwentyFifth,
    /// <summary>26th day of the month</summary>
    TwentySixth,
    /// <summary>27th day of the month</summary>
    TwentySeventh,
    /// <summary>28th day of the month</summary>
    TwentyEighth,
    /// <summary>29th day of the month</summary>
    TwentyNinth,
    /// <summary>30th day of the month</summary>
    Thirtieth,
    /// <summary>Last day of the month</summary>
    Last,
    /// <summary>The first Monday</summary>
    FirstMonday,
    /// <summary>The second Monday</summary>
    SecondMonday,
    /// <summary>The third Monday</summary>
    ThirdMonday,
    /// <summary>The first Tuesday</summary>
    FirstTuesday,
    /// <summary>The second Tuesday</summary>
    SecondTuesday,
    /// <summary>The third Tuesday</summary>
    ThirdTuesday,
    /// <summary>The first Wednesday</summary>
    FirstWednesday,
    /// <summary>The second Wednesday</summary>
    SecondWednesday,
    /// <summary>The third Wednesday</summary>
    ThirdWednesday,
    /// <summary>The first Thursday</summary>
    FirstThursday,
    /// <summary>The second Thursday</summary>
    SecondThursday,
    /// <summary>The third Thursday</summary>
    ThirdThursday,
    /// <summary>The first Friday</summary>
    FirstFriday,
    /// <summary>The second Friday</summary>
    SecondFriday,
    /// <summary>The third Friday</summary>
    ThirdFriday,
    /// <summary>First Wednesday after the ninth</summary>
    FirstWednesdayAfterNinth
  }
}
