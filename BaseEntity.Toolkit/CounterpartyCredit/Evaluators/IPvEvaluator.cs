/*
 *  -2015. All rights reserved.
 *
 */
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///  Interface for all the exposure calculators
  /// </summary>
  public interface IPvEvaluator : ISimulationPricer
  {
    /// <summary>
    ///  Calculate the MtM value on the exposure date.
    /// </summary>
    /// <param name="exposureIndex">Index of the exposure date in the array of all the exposure dates</param>
    /// <param name="exposureDate">The exposure date</param>
    /// <returns>MtM value on the exposure date</returns>
    double FastPv(int exposureIndex, Dt exposureDate);
  }

}
