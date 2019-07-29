/*
 * BaseCorrelationCalibrationMethod.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	///   Specifies the method for calibrating the base correlation term structure.
	/// </summary>
  ///
  /// <remarks>
  ///  <para>There are two ways to hand the term structure of base correlations.</para>
  ///   <list type="bullet">
  ///     <item><term>MaturityMatch</term>
  ///     <description>Backs out correlations from tranche spreads with given maturity (T).
  ///     To calibrate the T-maturity skew only T-maturity tranche spreads are
  ///     taken into account while spreads of shorter maturities are ignored.</description></item>
  ///     <item><term>TermStructure</term>
  ///     <description>Backs out correlations from all available tranche spreads
  ///     (for all available maturities).
  ///     To calibrate the T-maturity skew the bootstrapping method uses
  ///     not only T-maturity tranche spreads but also tranche spreads from all available
  ///     shorter maturities.</description></item>
  ///   </list>
  /// </remarks>
	public enum BaseCorrelationCalibrationMethod
	{
    /// <summary>Maturity Matching method</summary>
    /// <remarks>
    ///   <para>Backs out correlations from tranche spreads with given maturity (T).</para>
    /// </remarks>
    MaturityMatch,
    /// <summary>Term Structure method</summary>
    /// <remarks>
    ///   <para>Backs out correlations from all available tranche spreads</para>
    /// </remarks>
    TermStructure
	}
}
