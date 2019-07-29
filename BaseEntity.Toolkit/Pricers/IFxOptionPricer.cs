// 
//  -2012. All rights reserved.
// 

using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Interface for FxOption pricers.
  /// </summary>
  public interface IFxOptionPricer : IPricer
  {
    /// <summary>
    /// Calculates the Delta.
    /// </summary>
    /// <remarks>
    /// <para>Gamma is the change in <see cref="Delta"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point of change. Thus, the Gamma function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="Delta"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model. Exotic <see cref="FxOption">FxOptions</see>, including single and double 
    /// barriers, calculate the derivative using a second order, central finite-difference method. The spot Fx rate 
    /// is shifted up and down by 1bp and the derivative is calculated.</para> 
    /// </remarks>
    /// <returns>Option delta</returns>
    double Delta();

    /// <summary>
    /// Calculates the Gamma.
    /// </summary>
    /// <remarks>
    /// <para>Gamma is the change in <see cref="Delta"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point of change. Thus, the Gamma function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="Delta"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model. Exotic <see cref="FxOption">FxOptions</see>, including single and double 
    /// barriers, calculate the derivative using a second order, central finite-difference method. The spot Fx rate 
    /// is shifted up and down by 1bp and the derivative is calculated.</para> 
    /// </remarks>
    /// <returns>Option gamma</returns>
    double Gamma();

    /// <summary>
    /// Calculates the Vega.
    /// </summary>
    /// <remarks>
    /// <para>Vega is the change in <see cref="IPricer.Pv"/> of the <see cref="FxOption"/> given a change in the volatility. 
    /// This derivative is first calculated and then scaled to be per volatility (1%). Thus, the Vega function's 
    /// value can be multiplied by a projected change in the volatility (in percentage terms) to estimate the change in 
    /// <see cref="IPricer.Pv"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model. Exotic <see cref="FxOption">FxOptions</see>, including single and double 
    /// barriers, calculate the derivative using a second order, central finite-difference method. The volatility  
    /// is shifted up and down by 1% and the derivative is calculated.</para> 
    /// </remarks>
    /// <returns>Option vega</returns>
    double Vega();

    /// <summary>
    /// Calculates the Vanna.
    /// </summary>
    /// <remarks>
    /// <para>Vanna is the change in <see cref="Vega"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point. Thus, the Vanna function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="Vega"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model. Exotic <see cref="FxOption">FxOptions</see>, including single and double 
    /// barriers, calculate the derivative using a second order, central finite-difference method. The spot Fx rate  
    /// is shifted up and down by 1bp and the derivative is calculated.</para> 
    /// </remarks>
    /// <returns>Option vanna</returns>
    double Vanna();

    /// <summary>
    /// Calculates the Volga.
    /// </summary>
    /// <remarks>
    /// <para>Volga is the change in <see cref="Vega"/> of the <see cref="FxOption"/> given a change in the volatility. 
    /// This derivative is first calculated and then scaled to be per volatility (1%). Thus, the Volga function's 
    /// value can be multiplied by a projected change in the volatility (in percentage terms) to estimate the change in 
    /// <see cref="Vega"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes" /> model. Exotic <see cref="FxOption">FxOptions</see>, including single and double 
    /// barriers, calculate the derivative using a second order, central finite-difference method. The volatility  
    /// is shifted up and down by 1% and the derivative is calculated.</para> 
    /// </remarks>
    /// <returns>Option volga</returns>
    double Volga();

    /// <summary>
    /// Calculates the theta.
    /// </summary>
    /// <remarks>
    /// <para>Theta is the change in <see cref="IPricer.Pv"/> of the <see cref="FxOption"/> given a change in time.</para>
    /// </remarks>
    /// <returns>Option theta</returns>
    double Theta();

    /// <summary>
    /// Validates the pricer.
    /// </summary>
    void Validate();

    /// <summary>
    /// Calculates the single flat volatility for an Fx Option.
    /// </summary>
    /// <remarks>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> return the <see cref="FxOptionVanillaPricer.ImpliedVolatility"/>. 
    /// Exotic <see cref="FxOption">FxOptions</see>, including single and double barriers, return the average volatility.</para>
    /// </remarks>
    /// <returns>Flat volatility</returns>
    double FlatVolatility();

    /// <summary>
    /// Calculates the forward Fx rate.
    /// </summary>
    /// <returns>Forward fx rate</returns>
    double ForwardFxRate();

    /// <summary>
    /// Calculates the forward Fx points.
    /// </summary>
    /// <returns>Forward fx points</returns>
    double ForwardFxPoints();

    /// <summary>
    /// Calculates the spot fx rate.
    /// </summary>
    /// <returns>Spot fx rate</returns>
    double SpotFxRate { get; }

    /// <summary>
    /// Gets the volatility curve.
    /// </summary>
    VolatilityCurve VolatilityCurve { get; }
  }
}
