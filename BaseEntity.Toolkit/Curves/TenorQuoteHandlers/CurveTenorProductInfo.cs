//
//  -2014. All rights reserved.
//

using System;
using System.Collections;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Curves.TenorQuoteHandlers
{
  /// <summary>
  ///  Information about product and its market value.
  /// </summary>
  [Serializable]
  internal struct CurveTenorProductInfo : IStructuralEquatable
  {
    /// <summary>
    ///   The product
    /// </summary>
    public readonly IProduct Product;

    /// <summary>
    ///   The market present value.
    /// </summary>
    public readonly double TargetValue;

    /// <summary>
    ///   Constructor 
    /// </summary>
    /// <param name="product">The product</param>
    /// <param name="marketPv">The market PV</param>
    public CurveTenorProductInfo(IProduct product,
      double marketPv = 0.0)
    {
      Product = product;
      TargetValue = marketPv;
    }

    /// <summary>
    ///   Constructor 
    /// </summary>
    /// <param name="product">The product</param>
    /// <param name="quote">The market quote</param>
    /// <param name="calculator">The calculator to compute the market Pv</param>
    public CurveTenorProductInfo(IProduct product,
      IMarketQuote quote,
      Func<IProduct, IMarketQuote, double> calculator)
      : this(product, calculator == null ? 0.0 : calculator(product, quote))
    {
    }

    /// <summary>
    ///   Get the instance of NULL product
    /// </summary>
    public static readonly CurveTenorProductInfo Empty
      = new CurveTenorProductInfo();

    #region IStructuralEquatable Members

    bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
    {
      if (!(other is CurveTenorProductInfo)) return false;
      var o = (CurveTenorProductInfo) other;
      return comparer.Equals(Product, o.Product) && comparer.Equals(TargetValue, o.TargetValue);
    }

    int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
    {
      var h1 = comparer.GetHashCode(TargetValue);
      if (Product == null) return h1;
      var h2 = comparer.GetHashCode(Product);
      return (h1 << 5) + h1 ^ h2;
    }

    #endregion
  }
}
