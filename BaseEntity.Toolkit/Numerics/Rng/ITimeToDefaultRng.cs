/*
 * ITimeToDefaultRng.cs
 *
 *   2008-2011. All rights reserved.
 *
 */

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  /// <summary>
  /// Interface for Time-to-Default Random Number Generators.
  /// </summary>
  public interface ITimeToDefaultRng
  {
    /// <summary>
    /// Draws the default times that occur in the next path and sets the next path as the current path.
    /// </summary>
    /// 
    /// <returns>The number of defaults that occur.</returns>
    /// 
    int Draw();

    /// <summary>
    /// Gets the default date for name n on the current path.
    /// </summary>
    /// 
    /// <param name="n">The index of the default.</param>
    /// 
    /// <returns>The default date</returns>
    /// 
    Dt GetDefaultDate(int n);

    /// <summary>
    /// Gets the index of the defaulted name in the array of Survival Curves.
    /// </summary>
    /// 
    /// <param name="n">The index of the default.</param>
    /// 
    /// <returns>The index in the survival curve array.</returns>
    /// 
    int GetDefaultName(int n);

    /// <summary>
    /// Gets the Stratum that the path was drawn from.
    /// </summary>
    int Stratum { get; }

    /// <summary>
    /// Gets the Weight of the path drawn.
    /// </summary>
    double Weight { get; }
  }
}
