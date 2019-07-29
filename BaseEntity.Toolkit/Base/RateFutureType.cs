//
// RateFutureType.cs
//  -2014. All rights reserved.
//

namespace BaseEntity.Toolkit.Base
{
  ///<summary>
  /// Category of STIR future contracts
  ///</summary>
  public enum RateFutureType
  {
    /// <summary>
    /// Physical T-bill futures quoted on an indexed yield
    /// </summary>
    /// <remarks>
    /// <para>Examples of this type of future include CME TBill Futures.</para>
    /// </remarks>
    TBill,

    /// <summary>
    /// Interbank cash rate futures with price quoted as principal plus return determined by yield
    /// </summary>
    /// <remarks>
    /// <para>Examples of this type of future include CME 3M Eurodollar Futures and
    /// Liffe Euribor Futures</para>
    /// </remarks>
    MoneyMarketCashRate,

    /// <summary>
    /// Physical bank bill futures with price quoted as principal discount
    /// </summary>
    /// <remarks>
    /// <para>Examples of this type of future include ASX 90 day bill futures.</para>
    /// </remarks>
    ASXBankBill,

    /// <summary>
    /// Underlying is arithmetic average of a quoted rate
    /// </summary>
    /// <remarks>
    /// <para>Examples of this type of future include CME Fed Fund Futures and
    /// ASX 30 day interbank cash rate futures.</para>
    /// </remarks>
    ArithmeticAverageRate,

    /// <summary>
    /// Underlying is a geometric average of a quoted rate
    /// </summary>
    /// <remarks>
    /// <para>Examples of this type of future include OIS Futures.</para>
    /// </remarks>
    GeometricAverageRate
  }

  ///<summary>
  /// Category of STIR future contracts
  ///</summary>
  public static class RateFutureTypeExtensions
  {
    /// <summary>
    /// True of rate future type is based on an underlying discount rate
    /// </summary>
    /// <param name="type">Rate future type</param>
    /// <returns>True if rate future type is based on an underlying discount rate</returns>
    public static bool IsDiscountRateFutureType(this RateFutureType type)
    {
      return (type == RateFutureType.ASXBankBill || type == RateFutureType.TBill);
    }

    /// <summary>
    /// True of rate future type is based on an underlying deposit rate
    /// </summary>
    /// <param name="type">Rate future type</param>
    /// <returns>True if rate future type is based on an underlying deposit rate</returns>
    public static bool IsDepositRateFutureType(this RateFutureType type)
    {
      return (type == RateFutureType.ASXBankBill || type == RateFutureType.TBill);
    }
  }

  ///<summary>
  /// Quote type of rate future trade level 
  ///</summary>
  public enum RateFutureTradeLevelType
  {
    ///<summary>
    /// Market quote of future contract price
    ///</summary>
    RateFutureQuote,
    ///<summary>
    /// Change from previous closing price in basis points
    ///</summary>
    MoveFromPreviousClose
  }

  ///<summary>
  /// Color code representing the Eurodollar future pack trading terminology
  ///</summary>
  [BaseEntity.Shared.AlphabeticalOrderEnum]
  public enum EDFuturePack_Color
  {
    ///<summary>
    /// ED Futures Series1-4
    ///</summary>
    Whites = 1,
    ///<summary>
    /// ED Futures Series5-8
    ///</summary>
    Reds = 5,
    ///<summary>
    /// ED Futures Series9-12
    ///</summary>
    Greens = 9,
    ///<summary>
    /// ED Futures Series13-16
    ///</summary>
    Blues = 13,
    ///<summary>
    /// ED Futures Series17-20
    ///</summary>
    Golds = 17,
    ///<summary>
    /// ED Futures Series21-24
    ///</summary>
    Purples = 21,
    ///<summary>
    /// ED Futures Series25-28
    ///</summary>
    Oranges = 25,
    ///<summary>
    /// ED Futures Series29-32
    ///</summary>
    Pinks = 29,
    ///<summary>
    /// ED Futures Series33-36
    ///</summary>
    Silvers = 33,
    ///<summary>
    /// ED Futures Series37-40
    ///</summary>
    Coppers = 37
  }

  ///<summary>
  /// Code to mark the series of eurodollar future contract
  ///</summary>
  public enum EDFutureCode
  {
    ///<summary>
    /// First regular series of eudordollar future contract
    ///</summary>
    ED1 = 1,
    ///<summary>
    ///</summary>
    ED2,
    ///<summary>
    ///</summary>
    ED3,
    ///<summary>
    ///</summary>
    ED4,
    ///<summary>
    ///</summary>
    ED5,
    ///<summary>
    ///</summary>
    ED6,
    ///<summary>
    ///</summary>
    ED7,
    ///<summary>
    ///</summary>
    ED8,
    ///<summary>
    ///</summary>
    ED9,
    ///<summary>
    ///</summary>
    ED10,
    ///<summary>
    ///</summary>
    ED11,
    ///<summary>
    ///</summary>
    ED12,
    ///<summary>
    ///</summary>
    ED13,
    ///<summary>
    ///</summary>
    ED14,
    ///<summary>
    ///</summary>
    ED15,
    ///<summary>
    ///</summary>
    ED16,
    ///<summary>
    ///</summary>
    ED17,
    ///<summary>
    ///</summary>
    ED18,
    ///<summary>
    ///</summary>
    ED19,
    ///<summary>
    ///</summary>
    ED20,
    ///<summary>
    ///</summary>
    ED21,
    ///<summary>
    ///</summary>
    ED22,
    ///<summary>
    ///</summary>
    ED23,
    ///<summary>
    ///</summary>
    ED24,
    ///<summary>
    ///</summary>
    ED25,
    ///<summary>
    ///</summary>
    ED26,
    ///<summary>
    ///</summary>
    ED27,
    ///<summary>
    ///</summary>
    ED28,
    ///<summary>
    ///</summary>
    ED29,
    ///<summary>
    ///</summary>
    ED30,
    ///<summary>
    ///</summary>
    ED31,
    ///<summary>
    ///</summary>
    ED32,
    ///<summary>
    ///</summary>
    ED33,
    ///<summary>
    ///</summary>
    ED34,
    ///<summary>
    ///</summary>
    ED35,
    ///<summary>
    ///</summary>
    ED36,
    ///<summary>
    ///</summary>
    ED37,
    ///<summary>
    ///</summary>
    ED38,
    ///<summary>
    ///</summary>
    ED39,
    ///<summary>
    ///</summary>
    ED40
  }

}
