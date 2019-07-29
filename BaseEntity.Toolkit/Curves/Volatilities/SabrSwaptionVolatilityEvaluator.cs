using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   Evaluate SABR smile based on the lists of swaption durations and 
  ///   the corresponding time series of parameters.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class SabrSwaptionVolatilityEvaluator : BaseEntityObject
  {
    private readonly double[] _durations;
    private readonly SabrVolatilityEvaluator[] _evaluators;
    private readonly Interp _interp;

    /// <summary>
    /// Initializes a new instance of the <see cref="SabrSwaptionVolatilityEvaluator"/> class.
    /// </summary>
    /// <param name="durations">The durations.</param>
    /// <param name="evaluators">The evaluators.</param>
    /// <param name="interp">The interpolation method.</param>
    /// <remarks></remarks>
    public SabrSwaptionVolatilityEvaluator(double[] durations,
      SabrVolatilityEvaluator[] evaluators,
      Interp interp)
    {
      Debug.Assert(durations != null);
      Debug.Assert(evaluators != null);
      Debug.Assert(durations.Length == evaluators.Length);

      _durations = durations;
      _evaluators = evaluators;

      if (interp == null)
      {
        var extrap = new Smooth();
        interp = new Linear(extrap, extrap);
      }
      _interp = interp;
    }

    /// <summary>
    /// Evaluates the volatility of a sweaption with specified expiry, duration
    /// and the at the mnoey swap rate.
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="duration">The duration.</param>
    /// <param name="rate">The rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public double Evaluate(Dt expiry, double duration, double rate, double strike)
    {
      var volatilities = _evaluators.Select(e => e.Evaluate(expiry, rate, strike)).ToArray();
      var interpolator = new Interpolator(_interp, _durations, volatilities);
      return interpolator.evaluate(duration);
    }
  }
}
