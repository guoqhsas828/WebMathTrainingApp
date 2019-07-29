/*
 * BaseCorrelationCombiningMethod.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Base Correlation combining methods
  /// </summary>
  public enum BaseCorrelationCombiningMethod
  {
    /// <summary>
    ///   Directly join surfaces by weighted averaging
    /// </summary>
    JoinSurfaces,

    /// <summary>
    ///   Average the loss distributions based on compoenent correlations
    /// </summary>
    PvAveraging,

    /// <summary>
    ///   Each name has a factor correlation based on its assciated base correlation
    /// </summary>
    ByName,

    /// <summary>
    ///   Default method for combining base correlation surfaces
    /// </summary>
    Default,

    /// <summary>
    ///   Directly merge surfaces by weighted averaging
    /// </summary>
    MergeSurfaces,
  }

}