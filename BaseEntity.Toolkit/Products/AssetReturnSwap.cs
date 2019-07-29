// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Class AssetReturnSwap.
  /// </summary>
  [Serializable]
  public class AssetReturnSwap : Product
  {
    /// <summary>
    /// Construct from two legs
    /// </summary>
    /// <param name="receiverLeg">SwapLeg object</param>
    /// <param name="payerLeg">SwapLeg object</param>
    internal AssetReturnSwap(IAssetReturnLeg receiverLeg, SwapLeg payerLeg)
      : base(Dt.Min(receiverLeg.Effective, payerLeg.Effective),
        (Dt.Cmp(receiverLeg.Maturity, payerLeg.Maturity) > 0) ? receiverLeg.Maturity : payerLeg.Maturity,
        (payerLeg.Ccy == receiverLeg.Ccy) ? receiverLeg.Ccy : Currency.None)
    {
      ReturnsLeg = receiverLeg;
      FundingLeg = payerLeg;
    }

    /// <summary>
    /// Gets the funding leg.
    /// </summary>
    /// <value>The funding leg.</value>
    public SwapLeg FundingLeg { get; private set; }

    /// <summary>
    /// Gets the returns leg.
    /// </summary>
    /// <value>The returns leg.</value>
    public IAssetReturnLeg ReturnsLeg { get; private set; }
  }

  /// <summary>
  /// Class AssetReturnSwap.
  /// </summary>
  /// <typeparam name="T">The type of the underlying asset</typeparam>
  [Serializable]
  public class AssetReturnSwap<T> : AssetReturnSwap where T:IProduct
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetReturnSwap{T}"/> class.
    /// </summary>
    /// <param name="returnsLeg">The returns leg.</param>
    /// <param name="fundingLeg">The funding leg.</param>
    internal AssetReturnSwap(
      IAssetReturnLeg<T> returnsLeg, SwapLeg fundingLeg)
      :base(returnsLeg, fundingLeg)
    { }

    /// <summary>
    /// Gets the returns leg.
    /// </summary>
    /// <value>The returns leg.</value>
    public new IAssetReturnLeg<T> ReturnsLeg
    {
      get { return (IAssetReturnLeg<T>) base.ReturnsLeg; }
    }
  }
}
