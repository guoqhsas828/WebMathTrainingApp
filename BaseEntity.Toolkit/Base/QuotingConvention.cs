/*
 * QuotingConvention.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{

  /// <summary>
  ///   QuotingConvention represents the standard quoting
  ///   convention for a financial instrument (price,
  ///   yield, spread, etc.)
  /// </summary>
  public enum QuotingConvention
  {
    /// <summary>None</summary>
    None,

    /// <summary>Full price</summary>
    FullPrice,

    /// <summary>Flat price</summary>
    FlatPrice,

    /// <summary>Partial Coupon</summary>
    PartialCoupon,

    /// <summary>Yield</summary>
    Yield,

    /// <summary>Yield spread</summary>
    YieldSpread,

    /// <summary>Zero coupon price spread</summary>
    ZSpread,

    /// <summary>Credit par spread</summary>
    CreditSpread,

    /// <summary>Upfront fee</summary>
    Fee,

    /// <summary>Correlation quotes</summary>
    Correlation,

    /// <summary>Asset swap spread (par)</summary>
    ASW_Par,

    /// <summary>Discount Margin</summary>
    DiscountMargin,

    /// <summary>Credit conventional spread</summary>
    CreditConventionalSpread,

    /// <summary>Credit conventional upfront points</summary>
    CreditConventionalUpfront,

    /// <summary>Discount Rate for Treasury Bills</summary>
    DiscountRate,

    /// <summary>Asset swap spread (market)</summary>
    ASW_Mkt,

    /// <summary>European option volatility quote</summary>
    Volatility,

    /// <summary>Risky Discount Spread</summary>
    RSpread,

    ///<summary>Forward Full Price</summary>
    ForwardFullPrice,

    ///<summary>Forward Flat Price</summary>
    ForwardFlatPrice,

    ///<summary>FX rate</summary>
    FxRate,

    ///<summary>Use the model price</summary>
    UseModelPrice,

    /// <summary>Option Price </summary>
    OptionPrice,
    
    /// <summary>Forward Price Spread </summary>
    ForwardPriceSpread,

    /// <summary>Yield To Worst Price</summary>
    YieldToWorst,

    /// <summary>Yield To Next Call Price</summary>
    YieldToNext
  }

}
