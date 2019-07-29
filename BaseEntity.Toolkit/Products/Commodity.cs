// 
//  -2012. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Commodity product
  /// </summary>
  /// <remarks>
  /// <para>A physical traded commodity.</para>
  /// <para>A commodity market is a market that trades in primary rather than manufactured products. Soft
  /// commodities are agricultural products such as wheat, coffee, cocoa and sugar. Hard commodities are mined,
  /// such as (gold, rubber and oil). Investors access about 50 major commodity markets worldwide with purely
  /// financial transactions increasingly outnumbering physical trades in which goods are delivered. Futures
  /// contracts are the oldest way of investing in commodities. Futures are secured by physical assets.
  /// Commodity markets can include physical trading and derivatives trading using spot prices, forwards, futures,
  /// and options on futures. Farmers have used a simple form of derivative trading in the commodity market for
  /// centuries for price risk management.</para>
  /// </remarks>
  /// <seealso href="http://en.wikipedia.org/wiki/List_of_traded_commodities">List of traded commodities</seealso>
  [Serializable]
  public class Commodity : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public Commodity()
      : base(Dt.Empty, Dt.MaxValue, Currency.None)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    public Commodity(Dt effective, Dt maturity, Currency ccy)
      : base(effective, maturity, ccy)
    {}

    #endregion Constructors
  }
}
