/*
 * ICorrelationBump.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
	interface ICorrelationBump
	{
    /// <summary>
    ///  Bump all the correlations simultaneously
    /// </summary>
    /// 
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlations</returns>
    double BumpCorrelations(double bump, bool relative, bool factor);

    ///
    /// <summary>
    ///   Bump correlations by index
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
    /// </remarks>
    ///
    /// <param name="i">Index of name i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    double BumpCorrelations(int i, double bump, bool relative, bool factor);

    /// <summary>
    ///   Get name of item i
    /// </summary>
    /// <param name="i">index</param>
    /// <returns>name</returns>
    string GetName(int i);

    /// <summary>
    ///   Number of names
    /// </summary>
    int NameCount { get;}
  }
}
