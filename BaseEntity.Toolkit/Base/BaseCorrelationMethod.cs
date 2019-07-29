//
// BaseCorrelationMethod.cs
//
//   2005-2014. All rights reserved.
//

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	/// Specifies the method for calculating base correlations.
	/// </summary>
	/// <remarks>
  /// <para>There are two ways to calculate base correlations from the market spread quotes
  /// of a consecutive sequence of tranches, such as the tranches 0~3%, 3~6%, 6~9%, ...,
  /// for CDS Indices. The first is <see cref="ProtectionMatching"/> which was the original
  /// method published by JP Morgan. The second is <see cref="ArbitrageFree"/> which is an
  /// updated method.</para>
  /// <para>In practice, both methods yield almost the same results in most of the times.</para>
	/// </remarks>
  /// <seealso cref="BaseCorrelation">Base Correlation Overview</seealso>
	public enum BaseCorrelationMethod
	{
		/// <summary>
		/// Protection Matching method
		/// <para>The protection pv is matched between tranches and is the method
		/// originally published by JP Morgan</para>
    /// <para>Suppose the detachment points are
    /// <formula inline="true">d_1, d_2, \ldots</formula> and let <formula inline="true">d_0 = 0</formula>.
    /// This method first finds the implied tranche correlation to match the market spread for each tranche
    /// <formula inline="true">[d_{i-1}, d_i]</formula> and uses the correlation
    /// to calculate the protection PV, <formula inline="true">\mathrm{Prot}[d_{i-1},d_i]</formula>, of the tranche.</para>
    /// <para>Then it finds the protection PV of each first loss tranche <formula inline="true">[0, d_i]</formula>
    /// by recursion:
    /// <formula>
    ///   \mathrm{Prot}[0,d_i] = \mathrm{Prot}[0, d_{i-1}] + \mathrm{Prot}[d_{i-1},d_i]
    ///   \qquad i = 2, 3, \ldots
    /// </formula></para>
    /// <para>Once the protection PVs on the first loss tranches are known, the base correlation are calculated
    /// as the implied correlations matching the protection values.</para>
    /// <para>This is the method originally published by JP Morgan.</para>
    /// </summary>
		ProtectionMatching,

		/// <summary>
		/// Arbitrage Free method
		/// <para>This is a more recently adopted method where an arbitrage-free
		/// method of calculating the base correlations is used.</para>
    /// <para>This method finds a sequence of correlations such that if you long on the first
    /// loss tranche <formula inline="true">[0,d_i]</formula> and short on the tranche
    /// <formula inline="true">[0,d_{i-1}]</formula>, you should have no arbitrage advantage nor disadvantage
    /// over longing on the tranche <formula inline="true">[d_{i-1},d_i]</formula>.</para>
    /// <para>The calculation is performed by recursion, starting with the equity tranche
    /// <formula inline="true">[0,d_1]</formula>, the correlation of which is simply the implied
    /// tranche correlation.  Once we know the correlation of
    /// the first loss tranche <formula inline="true">[0,d_i]</formula>, we calculate the price of the tranche
    /// using the market spread of the next tranche
    /// <formula inline="true">[d_i,d_{i+1}]</formula>.  Then we find the implied correlation which matches the price
    /// on the new first loss tranche <formula inline="true">[0,d_{i+1}]</formula>.</para>
    /// </summary>
		ArbitrageFree
	}
}
