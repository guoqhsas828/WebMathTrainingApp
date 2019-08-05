using System;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Used to mark Trade-derived types to indicate the Product-derived type they trade
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class ProductAttribute : Attribute
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductAttribute"/> class.
    /// </summary>
    /// <param name="productType">Type of the product.</param>
    public ProductAttribute(Type productType)
    {
      if (!typeof (Product).IsAssignableFrom(productType))
      {
        throw new ArgumentException(String.Format("Type '{0}' does not derive from Product", productType), "productType");
      }
      ProductType = productType;
    }

    /// <summary>
    /// Gets or sets the type of the product.
    /// </summary>
    /// <value>The type of the product.</value>
    public Type ProductType { get; set; }

    /// <summary>
    ///   Product Name Alias for TradeId
    /// </summary>
    /// <value>The type of the product.</value>
    public string TradeIdAlias { get; set; }
  }
}