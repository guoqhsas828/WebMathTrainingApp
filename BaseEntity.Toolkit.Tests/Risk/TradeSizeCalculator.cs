using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Database;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public class TradeSizeCalculator
  {
    private static readonly TradeSizeCalculator DefaultCalculator = new TradeSizeCalculator();
    private static readonly ConcurrentDictionary<Type, TradeSizeCalculator> Calculators = new ConcurrentDictionary<Type, TradeSizeCalculator>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="productType"></param>
    /// <returns></returns>
    public static TradeSizeCalculator GetInstance(Type productType)
    {
      return Calculators.GetOrAdd(productType, AddCalculator);
    }

    private static TradeSizeCalculator AddCalculator(Type productType)
    {
      var attr = productType.GetCustomAttributes(typeof(TradeSizeCalculatorAttribute), true).Cast<TradeSizeCalculatorAttribute>().FirstOrDefault();
      return (attr == null) ? DefaultCalculator : (TradeSizeCalculator)Activator.CreateInstance(attr.Calculator);
    }

    /// <summary>
    /// Calculate the size in "native" currency
    /// </summary>
    /// <param name="product">The Product</param>
    /// <param name="holdingAmount">Trade amount * Product notional</param>
    public virtual double GetSize(Product product, double holdingAmount)
    {
      if (product == null)
      {
        throw new ArgumentException("product");
      }

      return holdingAmount;
    }

    /// <summary>
    /// Calculate the Quantity; if currency denominated then the value is in native currency
    /// </summary>
    /// <param name="product">The Product</param>
    /// <param name="holdingAmount">Trade amount</param>
    public virtual double GetQuantity(Product product, double holdingAmount)
    {
      if (product == null)
      {
        throw new ArgumentException("product");
      }

      return GetSize(product, holdingAmount);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectIds"></param>
    /// <returns></returns>
    protected string GetObjectIdCriteria(ICollection<long> objectIds)
    {
      var sb = new StringBuilder();

      if (objectIds != null && objectIds.Count > 0)
      {
        sb.Append("t.ObjectId IN (");

        for (int i = 0; i < objectIds.Count; ++i)
        {
          if (i > 0) sb.Append(",");
          sb.Append("@p" + i);
        }

        sb.Append(")");
      }

      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectIds"></param>
    /// <returns></returns>
    protected IList<DbDataParameter> GetObjectIdParameters(ICollection<long> objectIds)
    {
      var parameters = new List<DbDataParameter>();

      int i = 0;
      foreach (var objectId in objectIds)
      {
        parameters.Add(new DbDataParameter("p" + i, objectId));
        ++i;
      }

      return parameters;
    }
  }

}
