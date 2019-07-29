/*
 *  -2012. All rights reserved.
 */
using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for AssetSpot contracts. 
  /// This pricer is only used for hedges in curve building that require a point at AsOf
  ///  </summary>
  /// <summary>
  ///   <para>Price a <see cref="SpotAsset">Asset Spot</see> using a AssetForwardCurve
  ///   forward curve and interest rate discount curve.</para>
  /// </summary>
  /// 
  /// <seealso cref="ForwardPriceCurve">Asset Forward Curve</seealso>
  [Serializable]
  [ReadOnly(true)]
  internal sealed class SpotAssetPricer : PricerBase, IPricer
  {
    #region Constructors
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Asset Spot contract</param>
    /// <param name="asOf">Pricing (asOf) date</param>
    /// <param name="notional">Notional</param>
    /// <param name="forwardPriceCurve">reference curve</param>
    public SpotAssetPricer(SpotAsset product, Dt asOf, double notional, ForwardPriceCurve forwardPriceCurve)
      : base(product, asOf, asOf)
    {
      Notional = notional;
      ReferenceCurve = forwardPriceCurve;
    }

    #endregion

    #region Properties
    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return ReferenceCurve.DiscountCurve; }
    }
    /// <summary>
    /// Asset forward curve
    /// </summary>
    public ForwardPriceCurve ReferenceCurve { get; private set; }
    /// <summary>
    /// Product
    /// </summary>
    public SpotAsset SpotAsset
    {
      get { return Product as SpotAsset; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Pv
    /// </summary>
    /// <returns></returns>
    public override double ProductPv()
    {
      return Notional*ReferenceCurve.Interpolate(Settle);
    }

    #endregion
  }
}
