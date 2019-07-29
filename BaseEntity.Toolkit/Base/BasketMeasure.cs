/*
 * BasketMeasure.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	///   Specifies a BasetMeasure.
	/// </summary>
	public enum BasketMeasure
	{
        /// <summary>
        ///  Average basket spread
        /// </summary>
        AverageSpread,

        /// <summary>
        ///   Duration-weighted average basket spread
        /// </summary>
        DurationWeightedAvgSpread,

        /// <summary>
        ///   ExpectedLossPv
        /// </summary>
        ExpectedLossPv,

        /// <summary>
        ///    Expected Loss
        /// </summary>
        ExpectedLoss,
        
        /// <summary>
        ///  The dispersion of duration-weighted basket spreads
        /// </summary>
        DurationWeightedSpreadDispersion
	}
}
