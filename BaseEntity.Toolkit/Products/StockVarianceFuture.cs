// 
//  -2013. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  ///<summary>
  /// Stock and Stock Index Variance Future product
  ///</summary>
  /// <remarks>
  ///   <para>A stock index variance future is an exchange traded future where the payoff at maturity
  ///   is the realised variance on an underlying equity index. More precisely, the payoff
  ///   of a variance swap is given by the formula:</para>
  ///   <math>Settlement = Notional * (Realised Variance – Variance Strike)</math>
  ///   <para>where realised variance is defined as:</para>
  ///   <math>variance =  252 * \sum_{i=1}^{N-1} \frac{R_{i}^2}{N-1}</math>
  ///   <para>and <m>R_i = ln(P_{i+1}/P_i)</m> is the percentage return of the asset from day
  ///   <m>i</m> to day <m>i+1</m> and <m>N</m> is the number of prices observed.</para>
  ///   <para>The variance strike is fixed and reflects the index price at the trade date
  ///   and the market’s expectation of realised variance. The variance strike is often quoted
  ///   as the square root of variance to allow easily comparison to volatility.</para>
  ///   <para>Common equity index variance futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://cfe.cboe.com/Products/Spec_VA.aspx">S&amp;P 500 Variance Futures</a></description></item>
  ///   </list>
  ///
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <seealso cref="FutureBase"/>
  /// <example>
  /// <para>The following example demonstrates constructing a stock index variance future.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var future = new StockVarianceFuture(
  ///    expirationDate,                          // Expiration
  ///    100000,                                  // Contract size
  ///    0.01                                     // Tick size
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class StockVarianceFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public StockVarianceFuture(Dt lastDeliveryDate, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {}

    #endregion Constructors
  }
}
