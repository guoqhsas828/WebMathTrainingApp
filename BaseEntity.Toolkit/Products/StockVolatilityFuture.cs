// 
//  -2013. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  ///<summary>
  /// Stock and Stock Index Volatility Future product
  ///</summary>
  /// <remarks>
  ///   <para>A stock index volatility future is an exchange traded future where the payoff at maturity
  ///   is the implied volatility from quoted stock index options.</para>
  /// 
  ///   <para><b>History</b></para>
  ///   <para>The idea of a volatility index, and financial instruments based on such an index, was first developed and described by Prof. Menachem
  ///   Brenner and Prof. Dan Galai in 1986, and first published in New Financial Instruments for Hedging Changes in Volatility, appearing
  ///   in the July-August 1989 issue of Financial Analysts Journal.
  ///   <a href="http://people.stern.nyu.edu/mbrenner/research/FAJ_articleon_Volatility_Der.pdf">Brenner, Menachem, and Galai, Dan. New Financial Instruments for
  ///   Hedging Changes in Volatility, Financial Analysts Journal, July-August 1989.</a></para>
  ///   <para>In a subsequent paper, Professors Brenner and Galai proposed a formula to compute the volatility index.
  ///   <a href="http://people.stern.nyu.edu/mbrenner/research/JOD_article_of_Vol_Index_Computation.pdf">Brenner, Menachem, and Galai, Dan.
  ///   Hedging Volatility in Foreign Currencies, The Journal of Derivatives, Fall, 1993.</a></para>
  ///   <para>In 1992, the CBOE commissioned Prof. Robert Whaley to create a stock market volatility index based on index option prices.
  ///   In January 1993, the CBOE held a news conference in which Prof. Whaley introduced the index, trademarked by the CBOE as "VIX".
  ///   Subsequently, the CBOE has computed VIX on a real-time basis.  Based on the history of index option prices, Prof. Whaley computed
  ///   daily VIX levels in a data series commencing January 1986, available on the CBOE website.  Prof. Whaleys research for the
  ///   CBOE appeared in the Journal of Derivatives. <a href="http://www2.owen.vanderbilt.edu/bobwhaley/Research/Publications/jd93.pdf">
  ///   Robert E. Whaley, 1993, Derivatives on market volatility: Hedging tools long overdue, Journal of Derivatives 1 (Fall), 71-84.</a></para>
  ///   <para>The VIX is quoted in percentage points and translates, roughly, to the expected movement in the S&amp;P 500 index over the
  ///   upcoming 30-day period, which is then annualized.</para>
  /// 
  ///   <para><b>VIX</b></para>
  ///   <para>The VIX is calculated and disseminated in real-time by the Chicago Board Options Exchange. Theoretically it is a weighted
  ///   blend of prices for a range of options on the S&amp;P 500 index. On March 26, 2004, the first-ever trading in futures on the VIX began
  ///   on CBOE Futures Exchange (CFE).</para>
  ///   <para>As of February 24, 2006, it became possible to trade VIX options contracts. Several exchange-traded funds seek to track its
  ///   performance.  The formula uses a kernel-smoothed estimator that takes as inputs the current market prices for all out-of-the-money
  ///   calls and puts for the front month and second month expirations.</para>
  ///   <para>The goal is to estimate the implied volatility of the S&amp;P 500 index over the next 30 days The VIX is calculated as the square
  ///   root of the par variance swap rate for a 30 day term initiated today.  Note that the VIX is the volatility of a variance swap and
  ///   not that of a volatility swap (volatility being the square root of variance, or standard deviation). A variance swap can be perfectly
  ///   statically replicated through vanilla puts and calls whereas a volatility swap requires dynamic hedging. The VIX is the square-root
  ///   of the risk neutral expectation of the S&amp;P 500 variance over the next 30 calendar days.  The VIX is quoted as an annualized standard
  ///   deviation.</para>
  ///   <para>The VIX has replaced the older VXO as the preferred volatility index used by the media. VXO was a measure of implied volatility
  ///   calculated using 30-day S&amp;P 100 index at-the-money options.</para>
  ///   <para>Common equity index volatility futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.cboe.com/micro/VIX/vixintro.aspx">VIX Futures</a></description></item>
  ///   </list>
  ///
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <seealso cref="FutureBase"/>
  /// <example>
  /// <para>The following example demonstrates constructing a stock index volatility future.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var future = new StockVolatilityFuture(
  ///    expirationDate,                          // Expiration
  ///    100000,                                  // Contract size
  ///    0.01                                     // Tick size
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class StockVolatilityFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public StockVolatilityFuture(Dt lastDeliveryDate, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {}

    #endregion Constructors
  }
}
