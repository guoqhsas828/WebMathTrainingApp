/*
 * TradeProductOwnershipResolver.cs -
 *
 *
 */

using System;
using System.Collections.Generic;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  internal class TradeProductOwnershipResolver : IOwnershipResolver
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentType"></param>
    /// <returns></returns>
    public string GetCascade(Type parentType)
    {
      if (parentType == typeof(Trade))
      {
        return "save-update";
      }
      var productAttr = GetProductAttribute(parentType);
      if (productAttr == null)
      {
        throw new ArgumentException("No ProductAttribute found for [" + parentType.Name + "]");
      }
      return IsStandardProduct(productAttr.ProductType) ? "none" : "save-update";
    }

    /// <summary>
    /// Gets the concrete type of the owned entity.
    /// </summary>
    /// <param name="parentType">Type of the parent.</param>
    /// <returns></returns>
    public Type GetOwnedConcreteType(Type parentType)
    {
      return GetProductAttribute(parentType).ProductType;
    }

    public static ProductAttribute GetProductAttribute(Type type)
    {
      if (!type.IsSubclassOf(typeof(Trade)))
      {
        throw new ArgumentException("Type [" + type + "] does not derive from [Trade]");
      }
      object[] attrs = type.GetCustomAttributes(typeof(ProductAttribute), false);
      return (attrs.Length > 0) ? (ProductAttribute)attrs[0] : null;
    }
    
    /// <summary>
    ///   Whether or not the ProductType is a Standard Product
    /// </summary>
    /// <param name="productType"></param>
    /// <returns></returns>
    public static bool IsStandardProduct(Type productType)
    {
      if (!productType.IsSubclassOf(typeof(Product)))
      {
        throw new ArgumentException("Invalid property value: [" + productType + "] does not derive from [Product]");
      }

      return productType.IsDefined(typeof(StandardProductAttribute), false);
    }
  }
}
