// 
//  -2012. All rights reserved.
// 

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Classes that are used to model the terms and conditions of financial products.
  /// </summary>
  /// <remarks>
  /// <para>To facilitate rapid model and product development, products are independent of implemented
  /// models and in general are simple data repositories for the indicative information needed by the pricers
  /// for each product. As such, Product classes typically do not contain business logic. In order to price a
  /// product, you use a class from the <see cref="BaseEntity.Toolkit.Pricers"/> namespace.</para>
  /// <para>Products implement the <see cref="IProduct"/> interface. Many derive from a convenient
  /// base class <see cref="Product"/>.</para>
  /// <para>Products supported by the Toolkit include:</para>
  /// <list type="bullet">
  /// <item><description>Fixed income products such as
  ///   <see cref="Bond">Global Sovereign, Corporate, Convertible and Callable Bonds</see>,
  ///   <see cref="BondFuture">Bond Futures</see>,
  ///   <see cref="Bill">Treasury Bills, Discount Bills, Commercial Paper and Bankers Acceptances</see> and
  ///   <see cref="CD">Certificates of Deposit and Fed Funds</see>.</description></item>
  /// <item><description>Interest rate derivative products such as
  ///   <see cref="Swap">IR Swaps</see>,
  ///   <see cref="Cap">Caps/Floors</see>,
  ///   <see cref="Swaption">Swaptions</see>,
  ///   <see cref="FRA">FRAs</see> and
  ///   <see cref="StirFuture">Short Term Interest Rate (STIR) Futures</see>.</description></item>
  /// <item><description>Inflation derivative products such as
  ///   <see cref="InflationBond">Inflation-linked Bonds</see> and
  ///   <see cref="SwapLeg">Inflation-linked Swaps</see>.</description></item>
  /// <item><description>Foreign exchange products such as
  ///   <see cref="Fx">Spot FX</see>,
  ///   <see cref="FxForward">FX Forwards</see>,
  ///   <see cref="FxNonDeliverableForward">FX NDFs</see>,
  ///   <see cref="FxSwap">FX Swap</see>,
  ///   <see cref="FxForwardOption">Vanilla and Exotic Option on FX</see> and
  ///   <see cref="FxForwardOption">Option on FX Forwards</see>.</description></item>
  /// <item><description>Single name credit products such as
  ///   <see cref="CDS">Credit Default Swaps (CDS)</see> and
  ///   <see cref="TRS">Total Return Swaps</see>.</description></item>
  /// <item><description>Basket credit products such as
  ///   <see cref="SyntheticCDO">CDOs</see>,
  ///   <see cref="FTD">Nth to Default</see> and
  ///   <see cref="CDX">Credit Indices</see>.</description></item>
  /// <item><description>Credit option products such as
  ///   <see cref="CDSOption">Options on CDS</see> and
  ///   <see cref="CDXOption">Options on Indices</see>.</description></item>
  /// <item><description>Equity products such as
  ///   <see cref="Stock">Stocks</see>,
  ///   <see cref="StockOption">Vanilla and Exotic Options on Stocks</see>,
  ///   <see cref="StockCliquetOption">Cliquet Options on Stocks</see>,
  ///   <see cref="StockSwapLeg">Equity Swap</see>,
  ///   <see cref="StockFuture">Equity Futures</see>,
  ///   <see cref="StockForward">Equity Forwards</see>,
  ///   <see cref="StockVarianceFuture">Equity Variance Future</see>,
  ///   <see cref="StockBasketOption">Basket Options on Stocks</see>.</description></item>
  /// <item><description>Commodity pricers such as
  ///   <see cref="CommodityFuture">Commodity Futures</see>,
  ///   <see cref="CommodityForward">Commodity Forwards</see>,
  ///   <see cref="CommodityOption">Commodity Options</see> and
  ///   <see cref="CommodityFutureOption">Commodity Future Options</see>.</description></item>
  /// </list>
  /// <seealso cref="N:BaseEntity.Toolkit.Pricers">Pricers</seealso>
  /// <seealso cref="N:BaseEntity.Toolkit.Models">Models</seealso>
  /// </remarks>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  internal class NamespaceDoc
  {}
}
