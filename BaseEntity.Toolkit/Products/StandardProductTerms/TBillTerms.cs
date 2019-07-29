//
// TBillTerms.cs
//   2015. All rights reserved.
//
using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Terms for US treasury bills, on the run.
  /// </summary>
  /// <remarks>
  ///   <seealso cref="Note"/>
  /// </remarks>
  [DebuggerDisplay("TBill")]
  [Serializable]
  public sealed class TBillTerms : StandardProductTermsBase
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    public TBillTerms()
      : base("TBill")
    {}

    #endregion

    #region Properties

    /// <summary>
    ///   Unique key for this term
    /// </summary>
    public override string Key { get { return GetKey(); } }

    /// <summary>Currency</summary>
    public static readonly Currency Currency = Currency.USD;

    /// <summary>Business day convention</summary>
    public static readonly BDConvention Roll = BDConvention.Following;

    /// <summary>Holiday calendar</summary>
    public static readonly Calendar Calendar = Calendar.NYB;

    /// <summary>Day count convention</summary>
    public static readonly DayCount DayCount = DayCount.Actual360;

    #endregion

    #region Methods

    /// <summary>
    ///   Create standard product given a date, a tenor, and a market quote
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="tenorName">Tenor name</param>
    /// <param name="quote">Market quote</param>
    /// <returns>Standard <see cref="Note"/></returns>
    [ProductBuilder]
    public Note GetProduct(Dt asOf, string tenorName, IMarketQuote quote)
    {
      var weeks = GetWeeks(tenorName);
      var effective = GetEffective(asOf, weeks);
      var maturity = GetMaturity(effective, weeks);
      return new Note(effective, maturity, Currency, GetCoupon(quote, effective, maturity), DayCount, Frequency.None, BDConvention.None, Calendar);
    }

    /// <summary>
    /// Create unique key for TBill Terms
    /// </summary>
    /// <returns>Unique key</returns>
    public static string GetKey()
    {
      return @"TBill";
    }

    #endregion

    #region Local Methods

    #region Find the auction dates

    // Let's fix some dates as the base dates for calculation.
    // These are Monday in the week there is an auction.
    private static readonly int BaseMonday = (int)new Dt(8, 12, 2014).ToJulian();

    // special auction dates not following the rules
    private static readonly int[] Auc4WIrregularDates =
    {
      // 4W bills
      (int) new Dt(29, 10, 2012).ToJulian(),
    };

    private static readonly int[] Auc13WIrregularDates =
    {
      // 13W bills
      (int) new Dt(3, 12, 2013).ToJulian(),
    };

    // Find the last auction date before as-of date.
    //   Auction dates:
    //     4W bill - every Tuesday;
    //     13W and 26W - every Monday;
    //     52W - every 4th Tuesday;
    //   Roll to following date if on holiday
    private static Dt GetAuctionDate(Dt asOf, int weeks)
    {
      Dt auctionDate;
      // We need a loop in case the date rolls.
      int anchor = (int)asOf.ToJulian() + 6;
      do
      {
        if (anchor < 7)
        {
          throw new ArgumentException(String.Format("Failed to find TBill auction date before {0}", asOf));
        }
        anchor -= 7; // move back to the last week
        int[] irregulars = null;
        int julian;
        switch (weeks)
        {
          case 4:
            julian = BaseMonday + ((anchor - BaseMonday) / 7) * 7;
            irregulars = Auc4WIrregularDates;
            break;
          case 13:
            julian = BaseMonday + ((anchor - BaseMonday) / 7) * 7;
            irregulars = Auc13WIrregularDates;
            break;
          case 26:
            julian = BaseMonday + ((anchor - BaseMonday) / 7) * 7;
            irregulars = Auc13WIrregularDates;
            break;
          case 52:
            julian = BaseMonday + ((anchor - BaseMonday) / 28) * 28;
            break;
          default:
            throw new ArgumentException(String.Format("Invalid TBill tenor {0}W", weeks));
        }
        auctionDate = GetAuctionDate(julian, irregulars, weeks);
      } while (auctionDate >= asOf);
      return auctionDate;
    }

    private static Dt GetAuctionDate(int juMonday, int[] irregulars, int weeks)
    {
      // If there is any irregular dates not following the rule below, check it.
      if (irregulars != null)
      {
        for (int i = 0; i < irregulars.Length; ++i)
        {
          var n = irregulars[i];
          // Is it in the same week and before Thursday?
          if (n >= juMonday && n <= juMonday + 2)
            return new Dt((uint)n);
        }
      }

      // Rule 1: 13W and 26W auction date is Monday rolled;
      Dt monday = new Dt((uint)juMonday);
      var auctionDate = RollAuctionDate(monday, (uint)juMonday);
      if (weeks != 4 && weeks != 52) return auctionDate;

      // Rule 2: If the next day after the 13W/26W auction is not a holiday
      //    and it is before Thursday, then it is the auction date for 4W and 52W bills;
      Dt nextDay = auctionDate + 1;
      if (nextDay.ToJulian() - juMonday <= 2 && IsValidTreasuryDate(nextDay))
        return nextDay;

      // Rule 3: If this Tuesday is not a holiday,
      // then it is the auction date for 4W and 52W bills;
      Dt tuesday = monday + 1;
      if (IsValidTreasuryDate(tuesday)) return tuesday;

      // Rule 4: If this Tuesday is a holiday but Wednesday is not,
      // then it is auction date for 4W and 52W bills;
      Dt wednesday = tuesday + 1;
      if (IsValidTreasuryDate(wednesday)) return wednesday;

      // Rule 4: If both Tuesday and Wednesday are holidays but Monday is not,
      // then this Monday serves as the auction date for 4W and 52W bills;
      if (IsValidTreasuryDate(monday)) return monday;

      // Unknown case: just roll Tuesday.
      return Dt.Roll(tuesday, Roll, Calendar);
    }

    private static Dt RollAuctionDate(Dt date, uint julian)
    {
      for (uint i = 1; !IsValidTreasuryDate(date); ++i)
      {
        date = new Dt(julian + i);
      }
      return date;
    }

    private static bool IsValidTreasuryDate(Dt date)
    {
      // We added new year's eve and xmas eve as holidays
      return CalendarCalc.IsValidSettlement(Calendar,
        date.Day, date.Month, date.Year) &&
        (date.Month != 12 || (date.Day != 24 && date.Day != 31));
    }
    #endregion

    // Get on the run TBill issue date, which is the Thursday following auction.
    //   On holiday, it rolls to following date.
    private static Dt GetEffective(Dt asOf, int weeks)
    {
      var auctionDate = GetAuctionDate(asOf, weeks);
      // Monday is 0, Thursday is 3
      const int thursday = (int)BaseEntity.Toolkit.Base.DayOfWeek.Thursday;
      var weekday = (int)auctionDate.DayOfWeek();
      if (weekday > thursday)
        throw new ApplicationException(String.Format("Unexpected auction date {0}", auctionDate));
      return Dt.Roll(Dt.Add(auctionDate, thursday - weekday), Roll, Calendar);
    }

    // Maturity always on the Thursday of the target week, 
    // rolling to following if on holiday.
    private static Dt GetMaturity(Dt effective, int weeks)
    {
      var date = Dt.Add(effective, weeks, TimeUnit.Weeks);
      const int thursday = (int)BaseEntity.Toolkit.Base.DayOfWeek.Thursday;
      var weekday = (int)date.DayOfWeek();
      if (weekday != thursday)
        date += thursday - weekday;
      return Dt.Roll(date, Roll, Calendar);
    }

    // Get tenor in weeks
    private static int GetWeeks(string tenorName)
    {
      var tenor = Tenor.Parse(tenorName);
      int n = tenor.N;
      if (n > 0)
      {
        switch (tenor.Units)
        {
          case TimeUnit.Days:
            if (n % 7 != 0) break;
            return n / 7;
          case TimeUnit.Weeks:
            return n;
          case TimeUnit.Months:
            if (n == 1) return 4;
            if (n == 3 || n == 6 || n == 12) return (n / 3) * 13;
            break;
          case TimeUnit.Years:
            if (n == 1) return 52;
            break;
        }
      }
      throw new ArgumentException(String.Format("Invalid TBill tenor {0}", tenorName));
    }

    private static double GetCoupon(IMarketQuote quote, Dt effective, Dt maturity)
    {
      if (quote.Type == QuotingConvention.Yield)
        return quote.Value;

      var frac = Dt.Fraction(effective, maturity, DayCount);
      var price = quote.Value;
      switch (quote.Type)
      {
        case QuotingConvention.FlatPrice:
        case QuotingConvention.ForwardFlatPrice:
        case QuotingConvention.ForwardFullPrice:
          break;
        case QuotingConvention.DiscountRate:
          price = 1 - quote.Value * frac;
          break;
        default:
          throw new ArgumentException(String.Format("Unexpected quote type {0}", quote.Type));
      }
      return (1.0 / price - 1) / frac;
    }

    #endregion
  }
}
