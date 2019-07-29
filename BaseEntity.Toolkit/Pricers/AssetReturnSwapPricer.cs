// 
//  -2015. All rights reserved.
// 

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Class AssetreturnSwapPricer.
  /// </summary>
  [Serializable]
  public class AssetReturnSwapPricer : PricerBase, IPricer<AssetReturnSwap>
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetReturnSwapPricer" /> class.
    /// </summary>
    /// <param name="returnsLegPricer">The returns leg pricer.</param>
    /// <param name="fundingLegPricer">The funding leg pricer.</param>
    public AssetReturnSwapPricer(
      IAssetReturnLegPricer returnsLegPricer,
      SwapLegPricer fundingLegPricer)
      : base(new AssetReturnSwap(returnsLegPricer.AssetReturnLeg,
        fundingLegPricer.SwapLeg), returnsLegPricer.AsOf, returnsLegPricer.Settle)
    {
      ReturnsLegPricer = returnsLegPricer;
      FundingLegPricer = fundingLegPricer;
      PvOnAccrualBasis = false;
    }

    #region Methods

    /// <summary>
    /// Net present value of the product, excluding the value
    /// of any additional payment.
    /// </summary>
    /// <returns>System.Double.</returns>
    public override double ProductPv()
    {
      return PvOnAccrualBasis ? AccrualBasis() : (ReturnsLegPricer.ProductPv() + FundingLegPricer.ProductPv());
    }

    ///<summary>
    /// Present value of any additional payment associated with the pricer.
    ///</summary>
    ///<returns></returns>
    public override double PaymentPv()
    {
      return FundingLegPricer.PaymentPv() + ((ReturnsLegPricer as PricerBase)?.PaymentPv() ?? 0.0);
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks><para>There are some pricers which need to remember some internal state
    /// in order to skip redundant calculation steps. This method is provided
    /// to indicate that all internal states should be cleared or updated.</para>
    /// <para>Derived Pricers may implement this and should call base.Reset()</para></remarks>
    public override void Reset()
    {
      ReturnsLegPricer.Reset();
      FundingLegPricer.Reset();
      base.Reset();
    }

    /// <summary>
    ///  Calculate the accrual basis, namely, the net value of the unrealized gain and
    ///  the accrued interests.
    /// </summary>
    /// <returns>The accrual basis</returns>
    public double AccrualBasis()
    {
      return ReturnsLegPricer.UnrealizedGain()
        + FundingLegPricer.Accrued();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override double Accrued()
    {
      return FundingLegPricer.Accrued();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the swap.
    /// </summary>
    /// <value>The swap.</value>
    public AssetReturnSwap Swap => (AssetReturnSwap) Product;

    /// <summary>
    /// Gets the returns leg pricer.
    /// </summary>
    /// <value>The returns leg pricer.</value>
    public IAssetReturnLegPricer ReturnsLegPricer { get; private set; }

    /// <summary>
    /// Gets the funding leg pricer.
    /// </summary>
    /// <value>The funding leg pricer.</value>
    public SwapLegPricer FundingLegPricer { get; private set; }

    /// <summary>
    /// Discount curve
    /// </summary>
    /// <value>The discount curve.</value>
    /// <exception cref="ToolkitException">Payer and receiver legs are discounted with different curves, call is ambiguous</exception>
    public DiscountCurve DiscountCurve
    {
      get
      {
        if (ReturnsLegPricer.DiscountCurve != FundingLegPricer.DiscountCurve)
          throw new ToolkitException("Payer and receiver legs are discounted with different curves, call is ambiguous");
        return ReturnsLegPricer.DiscountCurve;
      }
    }

    /// <summary>
    /// Swap discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves
    {
      get
      {
        var list = ReturnsLegPricer.ReferenceCurves.OfType<DiscountCurve>().ToList();
        var curve = FundingLegPricer.ReferenceCurve as DiscountCurve;
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        curve = ReturnsLegPricer.DiscountCurve;
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        curve = FundingLegPricer.DiscountCurve;
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        return list.ToArray();
      }
    }

    /// <summary>
    /// Reference curves
    /// </summary>
    /// <value>The reference curves.</value>
    public CalibratedCurve[] ReferenceCurves
    {
      get
      {
        return ReturnsLegPricer.ReferenceCurves
          .Where(curve => !(curve is SurvivalCurve))
          .Append(FundingLegPricer.ReferenceCurve)
          .Distinct().ToArray();
      }
    }

    /// <summary>
    /// Gets the survival curve.
    /// </summary>
    /// <value>The survival curve.</value>
    public SurvivalCurve SurvivalCurve => ReturnsLegPricer
      .ReferenceCurves.OfType<SurvivalCurve>().FirstOrDefault();

    /// <summary>
    /// Product to price
    /// </summary>
    /// <value>The product.</value>
    /// <exception cref="System.NotImplementedException"></exception>
    AssetReturnSwap IPricer<AssetReturnSwap>.Product => Swap;

    /// <summary>
    /// If set true, define model Pv as Accrual Baiss
    /// </summary>
    public bool PvOnAccrualBasis { get; set; }

    #endregion
  }


  /// <summary>
  /// Class AssetReturnSwapPricer.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public class AssetReturnSwapPricer<T> : AssetReturnSwapPricer where T : IProduct
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetReturnSwapPricer" /> class.
    /// </summary>
    /// <param name="returnsLegPricer">The returns leg pricer.</param>
    /// <param name="fundingLegPricer">The funding leg pricer.</param>
    public AssetReturnSwapPricer(
      IAssetReturnLegPricer<T> returnsLegPricer,
      SwapLegPricer fundingLegPricer)
      : base(returnsLegPricer, fundingLegPricer)
    {
      var swap = base.Swap;
      Product = new AssetReturnSwap<T>(
        (IAssetReturnLeg<T>) swap.ReturnsLeg, swap.FundingLeg);
    }

    /// <summary>
    /// Gets the asset return swap.
    /// </summary>
    /// <value>The swap.</value>
    public new AssetReturnSwap<T> Swap
    {
      get { return (AssetReturnSwap<T>) Product; }
    }

  }
}
