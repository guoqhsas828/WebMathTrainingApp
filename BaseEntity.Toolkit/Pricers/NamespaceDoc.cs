/*
 * NamespaceDoc.cs
 * Namespace documentation for the BaseEntity.Toolkit.Pricers namespace.
 * All namespaces need a dummy class NamespaceDoc to define the documentation for the namespace.
 *
 *  -2010. All rights reserved.
 *
 */
namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Classes providing the primary interface for pricing financial products.
  /// </summary>
  /// <remarks>
  /// <para>Pricers encapsulate the logic to price a particular product with a particular pricing model.
  /// One advantage in this approach is that there can be more than one pricer for a given product.</para>
  /// <para>Calculations are methods of the pricer and each pricer has all the information required
  /// to perform calculations.</para>
  /// <para>Typical usage is:</para>
  /// <list type="number">
  ///   <item>Create the required curves, surfaces, etc.</item>
  ///   <item>Create the product to be priced</item>
  ///   <item>Create the pricer</item>
  ///   <item>Call methods of the pricer to perform calculations</item>
  /// </list>
  /// <para>Pricers implement the <seealso cref="IPricer"/> interface. Some derive from convenient
  /// base classes such as <see cref="BlackScholesPricerBase"/>. In general the class hiearchy
  /// is intentionally kept very shallow.</para>
  /// <para>Pricers are stateful objects.  The actual state data differs depending on the pricer, but
  /// will include the product to be priced, the as-of date for pricing, and any other supporting data
  /// (for example, interest rate or credit curves).</para>
  /// <para>Common Pricers include:</para>
  /// <list type="bullet">
  /// <item><description>Fixed income pricers such as
  ///   <see cref="BondPricer">Bond Market Pricer</see>,
  ///   <see cref="BondFuturePricer">Bond Futures</see>,
  ///   <see cref="BillPricer">Treasury Bills, Discount Bills, Commercial Paper and Bankers Acceptances</see> and
  ///   <see cref="CDPricer">Certificates of Deposit and Fed Funds</see>.</description></item>
  /// <item><description>Interest rate derivative pricers such as
  ///   <see cref="SwapPricer">IR Swaps</see>,
  ///   <see cref="CapFloorPricer">Caps/Floors</see>,
  ///   <see cref="SwaptionBlackPricer">Swaptions</see>,
  ///   <see cref="FRAPricer">FRAs</see> and
  ///   <see cref="IRateFuturesPricer">STIR Futures</see>.</description></item>
  /// <item><description>Inflation derivative pricers such as
  ///   <see cref="InflationBondPricer">Inflation-linked Bonds</see> and
  ///   <see cref="SwapPricer">Inflation-linked Swaps</see>.</description></item>
  /// <item><description>Foreign exchange pricers such as
  ///   <see cref="FxForwardPricer">FX Forwards</see>,
  ///   <see cref="FxNonDeliverableForwardPricer">FX NDFs</see>,
  ///   <see cref="FxSwapPricer">FX Swap</see>,
  ///   <see cref="FxOptionVanillaPricer">Vanilla Option on FX</see> and
  ///   <see cref="FxOptionDoubleBarrierPricer">Double Barrier Option on FX</see>.</description></item>
  /// <item><description>Single name credit products such as
  ///   <see cref="CDSCashflowPricer">CDS</see>.</description></item>
  /// <item><description>Basket credit pricers such as
  ///   <see cref="SyntheticCDOPricer">CDOs</see>,
  ///   <see cref="FTDPricer">Nth to Default</see> and
  ///   <see cref="CDXPricer">CDS Indices</see>.</description></item>
  /// <item><description>Credit option products such as
  ///   <see cref="CDSOptionPricer">Options on CDS</see> and
  ///   <see cref="CDXOptionPricer">Options on Indices</see>.</description></item>
  /// <item><description>Equity pricers such as
  ///   <see cref="StockFuturePricer">Equity Futures</see>,
  ///   <see cref="StockFutureOptionBlackPricer">Options on Equity Futures</see> and
  ///   <see cref="StockOptionPricer">Vanilla and Exotic Options on Stocks</see>.</description></item>
  /// <item><description>Commodity pricers such as
  ///   <see cref="CommodityFuturesPricer">Commodity Futures</see>,
  ///   <see cref="CommodityForwardPricer">Commodity Forwards</see>,
  ///   <see cref="CommodityOptionPricer">Commodity Options</see> and
  ///   <see cref="CommodityFutureOptionBlackPricer">Commodity Future Options</see>.</description></item>
  /// </list>
  /// </remarks>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}

namespace BaseEntity.Toolkit.Pricers.BasketForNtdPricers
{
  /// <summary>
  /// Nth to Default pricing functions.
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}


namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  /// <summary>
  /// Credit basket and loss distribution calculation functions
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}


namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  /// Credit basket and loss distribution calculation functions
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}


namespace BaseEntity.Toolkit.Pricers.BGM
{
  /// <summary>
  /// BGM/LMM calculation functions
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}
