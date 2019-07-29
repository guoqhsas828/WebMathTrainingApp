using System;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Make use of model basis adjustment for bringing model price to market price 
  /// </summary>
  public interface ISupportModelBasis
  {
    /// <summary>
    /// Model Basis Valuation contribution to product pv when used to match model price to market price
    /// </summary>
    double ModelBasisValue { get; }

  }
}
