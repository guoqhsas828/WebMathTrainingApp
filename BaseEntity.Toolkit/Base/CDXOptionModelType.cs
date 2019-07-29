/*
 * CDXOptionModelType.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Types of credit index option models.
  /// </summary>
  public enum CDXOptionModelType
  {
    /// <summary>
    ///   Spread volatility model
    /// </summary>
    Black,
    /// <summary>
    ///   Price volatility model
    /// </summary>
    BlackPrice,
    /// <summary>
    ///   Modified spread volatility model
    /// </summary>
    ModifiedBlack,
    /// <summary>
    ///   Spread volatility arbitrage-free model
    /// </summary>
    BlackArbitrageFree,
    /// <summary>
    ///   Modified spread volatility model with continuously-paid coupon approximation
    /// </summary>
    /// <remarks>
    /// <para>
    /// Assuming flat hazard rate <m>\lambda</m> and continuously paying coupon, we have<math env="align*">
    ///   \mathrm{Protection} &amp;= (1-R)\,\int_0^T D_t\,d\left(1- e^{-\lambda t}\right)
    ///     = (1-R)\,\lambda \int_0^T D_t\, e^{-\lambda t}\, d t \\
    ///   \mathrm{Annuity} &amp;= \int_0^T D_t\, e^{-\lambda t}\, d t \\
    ///   \mathrm{Spread} &amp;= \frac{\mathrm{Protection}}{\mathrm{Annuity}} = (1-R)\,\lambda
    /// </math>
    ///   where <m>D_t</m> is the discount factor at time <m>t</m> and <m>R</m> the recovery rate given default.
    /// </para>
    /// </remarks>
    FullSpread,
  }

}
