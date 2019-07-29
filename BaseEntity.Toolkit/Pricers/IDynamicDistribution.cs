using BaseEntity.Toolkit.Base;


namespace BaseEntity.Toolkit.Pricers
{
  #region IDynamicDistribution

  /// <summary>
  /// Loss distribution conditional on <m>Z = z_i</m> where <m>Z</m> is the systemic Gaussian factor 
  /// </summary>
  public interface IDynamicDistribution
  {
    /// <summary>
    /// Expectation/Probability conditional on Gaussian factor realization <m>z_i</m> 
    /// </summary>
    /// <param name="i">Factor index</param>
    void ConditionOn(int i);

    /// <summary>
    /// Probability that underlying is fully exhausted by the loss
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns>Probability of exhaustion</returns>
    double ExhaustionProbability(Dt date);

    /// <summary>
    /// Quadrature weights
    /// </summary>
    double[] QuadratureWeights { get; }

    /// <summary>
    /// Quadrature weights
    /// </summary>
    double[] QuadraturePoints { get; }
  }

  #endregion
}
