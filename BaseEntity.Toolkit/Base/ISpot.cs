
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Interface INamed
  /// </summary>
  public interface INamed
  {
    /// <summary>
    /// Gets the name
    /// </summary>
    string Name { get; set; }
  }

  #region ISpot

  /// <summary>
  /// Spot asset
  /// </summary>
  public interface ISpot: INamed
  {
    /// <summary>
    /// Denomination currency
    /// </summary>
    Currency Ccy { get; }

    /// <summary>
    /// Spot date
    /// </summary>
    Dt Spot { get; set; }

    /// <summary>
    /// Asset value at spot date
    /// </summary>
    double Value { get; set; }
  }

  #endregion

  #region IForwardPriceCurve

  /// <summary>
  /// Term structure of forward prices
  /// </summary>
  public interface IForwardPriceCurve
  {
    /// <summary>
    /// Spot asset
    /// </summary>
    ISpot Spot { get; }

    /// <summary>
    /// Forward price at delivery
    /// </summary>
    /// <param name="deliveryDate">Delivery date</param>
    /// <returns>Forward price</returns>
    double Interpolate(Dt deliveryDate);

    /// <summary>
    /// Underlying discount curve
    /// </summary>
    DiscountCurve DiscountCurve { get; }

    /// <summary>
    /// Average carry rate net of risk free rate, between spot date and delivery date. 
    /// The Spot-Forward parity then states that 
    /// <m>F_t(T) = S_t\exp(\int_t^T f_t(u) + \delta_t(u)) </m>
    /// The forward carry rate is given by <m>c_t(u) = f_t(u) + \delta_t(u)</m> where <m>f_t(u)</m> is the forward funding cost, 
    /// <m>s_t(u)</m> is the forward storage cost and <m>\delta_t(u)</m> arises from one or more of the following
    /// <list type="bullet">
    /// <item><description>Continuously paid dividends</description></item>
    /// <item><description>Storage costs</description></item>
    /// <item><description>Convenience yield</description></item>
    /// </list>  
    /// </summary>
    double CarryRateAdjustment(Dt spot, Dt delivery);

    /// <summary>
    /// Discrete cashflows associated to holding the spot asset 
    /// </summary>
    IEnumerable<Tuple<Dt,DividendSchedule.DividendType,double>> CarryCashflow { get; }
  }

  #endregion
}
