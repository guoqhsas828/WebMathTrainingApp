/*
 * ICorrelationTermStruct.cs
 *
 * A class for base correlation data with term structure
 *
 *  . All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Interface of correlation term structure
  /// </summary>
	interface ICorrelationBumpTermStruct
	{
    /// <summary>
    ///   Bump correlations by index and tenor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    /// 
    ///   <para>This function bumps correlations of the given tenor.</para>
    /// </remarks>
    ///
    /// <param name="tenor">Index of tenor</param>
    /// <param name="i">Index of name i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    ///
    double BumpTenor(int tenor, int i, double bump, bool relative, bool factor);

    /// <summary>
    ///  Bump all the correlations in a given tenor simultaneously
    /// </summary>
    /// 
    /// <param name="tenor">Index of tenor</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlations</returns>
    double BumpTenor(int tenor, double bump, bool relative, bool factor);

    /// <summary>
    ///   Tenor dates
    /// </summary>
    Dt[] Dates { get; }
	}
}
