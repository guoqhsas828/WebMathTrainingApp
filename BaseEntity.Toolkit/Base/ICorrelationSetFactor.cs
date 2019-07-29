/*
 * ICorrelationSetFactor.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
	interface ICorrelationSetFactor
	{
    /// <summary>
    ///   Set all the correlation factors to a given value
    /// </summary>
    ///
    /// <param name="factor">Factor (square root of correlation) to set</param>
    void SetFactor(double factor);

    /// <summary>
    ///   Set all the correlation factors from a given tenor to a given value
    /// </summary>
    ///
    /// <param name="factor">Factor (square root of correlation) to set</param>
    /// <param name="fromDate">
    ///   Maturity date to set, effective only when the correlation is a term structure,
    ///   in which case all the factors with maturity dates on or later than the from
    ///   date are set to the specified value.
    /// </param>
    void SetFactor(Dt fromDate, double factor);
	}
}
