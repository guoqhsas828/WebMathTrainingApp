/*
 *  -2013. All rights reserved.
 */
using System;
using System.Collections;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Index quote object
  /// </summary>
  [Serializable]
  public struct MarketQuote : IMarketQuote, IStructuralEquatable
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="value">Quoted value</param>
    /// <param name="type">Quote type</param>
    public MarketQuote(double value, QuotingConvention type)
    {
      Value = value; Type = type;
    }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return String.Format("{0}, {1}", Value, Type);
    }

    /// <summary>
    ///   Quoted value
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    ///   Quote type
    /// </summary>
    public QuotingConvention Type { get; set; }

    #region IStructuralEquatable Members

    bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
    {
      if (!(other is MarketQuote)) return false;
      var o = (MarketQuote)other;
      return comparer.Equals(Value, o.Value) && comparer.Equals(Type, o.Type);
    }

    int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
    {
      var h1 = comparer.GetHashCode(Value);
      var h2 = comparer.GetHashCode(Type);
      return (h1 << 5) + h1 ^ h2;
    }

    #endregion
  };
}
