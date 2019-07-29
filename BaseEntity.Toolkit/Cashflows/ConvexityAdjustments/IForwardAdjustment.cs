using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Interface for convexity adjustments 
  /// </summary>
  public interface IForwardAdjustment
  {
    /// <summary>
    /// Convexity adjustment
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>Convexity adjustment</returns>
    double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing);

    /// <summary>
    /// Value of the floor embedded in the payment (if applicable)
    /// </summary>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <param name="floor">Floor on fixing</param>
    /// <param name="coupon">Spread over floating rate</param>
    /// <param name="cvxyAdj">Convexity adjustment</param>
    /// <returns>Floor value</returns>
    double FloorValue(FixingSchedule fixingSchedule, Fixing fixing, double floor, double coupon, double cvxyAdj);

    /// <summary>
    /// Value of the cap embedded in the payment (if applicable)
    /// </summary>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <param name="cap">Cap on fixing</param>
    /// <param name="coupon">Spread over floating rate</param>
    /// <param name="cvxyAdj">Convexity adjustment</param>
    /// <returns>Floor value</returns>
    double CapValue(FixingSchedule fixingSchedule, Fixing fixing, double cap, double coupon, double cvxyAdj);
  }
}