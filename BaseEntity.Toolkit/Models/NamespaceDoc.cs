// 
//  -2012. All rights reserved.
// 

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// <para>Model implementations and supporting classes.</para>
  /// </summary>
  /// <remarks>
  /// <para>Models are numerical implementations of pricing algorithms that are generally independent
  /// of any particular financial product. In general, models are implemented in a way that is
  /// general enough so that they can be applied to price a variety of products.</para>
  /// <para>Model classes typically contain only static methods. Conceptually, these classes
  /// can be thought as stateless calculators, encapsulating a pricing algorithm. Often, the algorithm
  /// is implemented in C++ for best performance.</para>
  /// <para>In most cases, developers will not use model classes directly.  The classes in the 
  /// <see cref="BaseEntity.Toolkit.Pricers">BaseEntity.Toolkit.Pricers</see> namespace provide a higher level
  /// interface for pricing.</para>
  /// <para>A wide variety of models are supported by the Toolkit. A sample includes:</para>
  /// <list type="bullet">
  /// <item><description>One factor models such as
  ///   <see cref="CIR">CIR</see>, 
  ///   <see cref="BK">Black-Karasinski</see> ,
  ///   <see cref="AffineJ">Affine with Jumps</see>,
  ///   <see cref="HullWhite">Generalised Hull-White</see>,
  ///   <see cref="Vasicek">Vasicek</see>, 
  ///   <see cref="MRIJumps">Mean-reverting Intensities with Jumps</see>,
  ///   <see cref="CEV">CEV</see>,
  ///   <see cref="Black">Black</see>,
  ///   <see cref="BlackScholes">Black Scholes</see> and
  ///   <see cref="BlackNormal">Black Normal</see>
  /// </description></item>
  /// <item><description>Multi factor models such as
  ///   <see cref="Heston">Heston stochastic volatility</see> and
  ///   <see cref="TwoFactorAffineJ">Generalised 2 factor affine model with jumps</see>
  /// </description></item>
  /// <item><description>Tree models such as 
  ///   <see cref="BinomialTree">Binomial Tree</see>,
  ///   <see cref="BlackKarasinskiBinomialTreeModel">Black-Karasinski Tree</see>,
  ///   <see cref="BDT">Black-Derman-Toy</see>,
  ///   <see cref="HullWhite">Hull White Tree</see>,
  ///   <see cref="GeneralizedHWTree">Generalize Hull White Tree</see> and
  ///   <see cref="TriModel">Generalized trinomial model</see>
  /// </description></item>
  /// <item><description>Exotic Option models such as
  ///   <see cref="DigitalOption">Digitals</see>,
  ///   <see cref="BarrierOption">Barriers</see>,
  ///   <see cref="TimeDependentBarrierOption">Time dependent barriers</see>,
  ///   <see cref="DigitalBarrierOption">Digital Barriers</see>, 
  ///   <see cref="ExchangeOption">Exchange</see>,
  ///   <see cref="PowerOption">Power</see>,
  ///   <see cref="CliquetOption">Rubinstein (1990) cliquet model</see>,
  ///   <see cref="LookbackFixedStrikeOption">Goldman, Sosin &amp; Satto (1979) lookback fixed strike</see> and
  ///   <see cref="LookbackFloatingStrikeOption">Goldman, Sosin and Satto (1979) lookback floating strike</see>
  /// </description></item>
  /// <item><description>Basket models such as
  ///   <see cref="MonteCarloBasketModel">Monte Carlo</see>,
  ///   <see cref="HomogeneousBasketModel">Homogeneous</see> and
  ///   <see cref="HeterogeneousBasketModel">Heterogeneous</see>.
  /// </description></item>
  /// <item><description>General cashflow models such as
  ///   <see cref="CashflowModel">Credit Contingent Cashflows</see>
  /// </description></item>
  /// </list>
  /// <seealso cref="BaseEntity.Toolkit.Products">Products</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Pricers">Pricers</seealso>
  /// </remarks>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  internal class NamespaceDoc
  {}
}
