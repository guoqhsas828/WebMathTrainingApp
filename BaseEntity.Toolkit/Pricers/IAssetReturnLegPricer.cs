// 
//  -2015. All rights reserved.
// 

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Interface representing an asset return leg pricer
  /// </summary>
  public interface IAssetReturnLegPricer : IPricer
  {
    /// <summary>
    /// Calculate the product PV.
    /// </summary>
    /// <returns>System.Double.</returns>
    double ProductPv();

    /// <summary>
    ///  Calculate the unrealized capital gains/losses from the last valuation date
    ///  to the settle date when the later is in the middle of a price return period;
    ///  Otherwise, it returns 0.
    /// </summary>
    /// <returns>The unrealized capital gains or losses</returns>
    /// <remarks>
    ///  This is the capital gains/losses based on the current price on the settle date.
    ///  Since the later is not a payment date, the position is yet to be closed and
    ///  the value gained or lost has yet to be cashed in.  Due to price changes, the
    ///  gains/losses are not guaranteed to get realized.  They may reverse on the next
    ///  valuation date.
    /// </remarks>
    double UnrealizedGain();

    /// <summary>
    /// Gets the price calculator.
    /// </summary>
    /// <returns>IPriceCalculator</returns>
    IPriceCalculator GetPriceCalculator();
    
    /// <summary>
    /// Gets the payment schedule.
    /// </summary>
    /// <param name="fromDate">From the date the payments start</param>
    /// <returns>PaymentSchedule.</returns>
    PaymentSchedule GetPaymentSchedule(Dt fromDate);

    /// <summary>
    /// Gets the notional.
    /// </summary>
    /// <value>The notional.</value>
    double Notional { get; set; }

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    DiscountCurve DiscountCurve { get; }

    /// <summary>
    /// Gets the reference curves.
    /// </summary>
    /// <value>The reference curves.</value>
    CalibratedCurve[] ReferenceCurves { get; }

    /// <summary>
    /// Gets the asset return leg.
    /// </summary>
    /// <value>The asset return leg.</value>
    IAssetReturnLeg AssetReturnLeg { get; }
  }

  /// <summary>
  /// Interface representing an asset return leg pricer
  /// </summary>
  /// <typeparam name="T">The type of the underlying asset</typeparam>
  public interface IAssetReturnLegPricer<T>
    : IAssetReturnLegPricer, IPricer<IAssetReturnLeg<T>>
    where T : IProduct
  {
  }

}
