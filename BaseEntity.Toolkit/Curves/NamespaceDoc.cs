//
// NamespaceDoc.cs
// Namespace documentation for the BaseEntity.Toolkit.Curves namespace.
// All namespaces need a dummy class NamespaceDoc to define the documentation for the namespace.
//  -2010. All rights reserved.
//
namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Data curve classes and methods.
  /// </summary>
  /// <remarks>
  /// <para><see cref="Curve">Curves</see> are general data repositories and are
  /// independent from any particular <see cref="BaseEntity.Toolkit.Models">Models</see> or method of
  /// calibration. Curves are fitted to market data using
  /// <see cref="BaseEntity.Toolkit.Calibrators">Calibrators</see>. This independence increases flexibility
  /// and reduces dependecies within the Toolkit.</para>
  /// <para>All curves inherit from the parent <see cref="Curve">Curve</see> class which
  /// implements an ordered, dated sequence (in x, as years) of (x,y) points. The <see cref="Curve">Curve</see>
  /// class implements typical vector services such as indexing with the addition of specialised services such as
  /// interpolation and root finding.</para>
  /// <para>Common types of curves include:</para>
  /// <list type="bullet">
  /// <item><description><see cref="Curve">Generic term structure of rates or values</see></description></item>
  /// <item><description><see cref="DiscountCurve">Interest rate discount term structure</see></description></item>
  /// <item><description><see cref="SurvivalCurve">Credit survival term structure</see></description></item>
  /// <item><description><see cref="RecoveryCurve">Recovery rate term structure</see></description></item>
  /// <item><description><see cref="FxCurve">FX curve</see></description></item>
  /// <item><description><see cref="StockCurve">Equity forward price curve</see></description></item>
  /// <item><description><see cref="CommodityCurve">Commodity curve</see></description></item>
  /// </list>
  /// </remarks>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}

namespace BaseEntity.Toolkit.Curves.Bump
{
  /// <summary>
  /// Data curve bumping-related classes and methods.
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}

namespace BaseEntity.Toolkit.Curves.Commodities
{
  /// <summary>
  /// Commodity curve classes and methods.
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}

namespace BaseEntity.Toolkit.Curves.Native
{
  /// <summary>
  /// Low level curve classes and methods.
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  /// Volatility surface classes and methods.
  /// </summary>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}
