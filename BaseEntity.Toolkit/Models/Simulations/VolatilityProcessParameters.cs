using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Curves;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// The class VolatilityProcessParameters contains factory methods to build volatility parameters.
  /// </summary>
  public static class VolatilityProcessParameters
  {
    /// <summary>
    ///  Collects the parameters for Heston process.
    /// </summary>
    /// <param name="initialSigma">The initial volatility level</param>
    /// <param name="theta">The long-term volatility level</param>
    /// <param name="kappa">The mean-reversion coefficient</param>
    /// <param name="nu">The volatility of volatility process</param>
    /// <param name="rho">The correlation between volatility
    ///  and the corresponding price/rate processes</param>
    /// <returns>IVolatilityProcessParameter.</returns>
    /// <remarks>
    /// <para>The volatility follows the process<math>
    ///  d{\sigma^2_t} = \kappa\,(\theta^2 - \sigma^2_t)\,d{t} + \nu\,\sigma_t\,d{W_t}
    /// </math>where
    ///  <m>\kappa \geq 0</m> is mean reversion,
    ///  <m>\theta \geq 0</m> is long-term volatility level,
    ///  <m>\nu \geq 0</m> is volatility of the volatility process.
    ///  In addition, there is another parameter <m>\rho</m> is another parameter
    ///  for the correlation between the volatility process <m>d{W}</m>
    ///  and the price process <m>d{W^P}</m>,
    ///   <m>\rho = \langle d{W}, d{W^p} \rangle</m>.
    ///  The initial volatility level is given by <m>\sigma_0</m>.
    /// </para>
    /// </remarks>
    public static IVolatilityProcessParameter Heston(
      double initialSigma,
      double theta, double kappa, double nu, double rho)
    {
      return new HestonProcessParameter
      {
        InitialSigma = initialSigma,
        Theta = theta,
        Kappa = kappa,
        Nu = nu,
        Rho = rho,
      };
    }
  }

  [Serializable]
  public class StaticVolatilityCurves
    : IVolatilityProcessParameter, IReadOnlyList<NativeCurve>
  {
    public StaticVolatilityCurves(VolatilityCurve[] curves)
    {
      Curves = curves;
    }

    public StaticVolatilityCurves(VolatilityCurve curve)
    {
      Curves = new[] {curve};
    }

    public static implicit operator StaticVolatilityCurves(
      VolatilityCurve[] curves)
    {
      return new StaticVolatilityCurves(curves);
    }

    public VolatilityCurve[] Curves { get; }

    public VolatilityProcessKind Kind => VolatilityProcessKind.Static;

    #region IReadOnlyList<NativeCurve> members

    IEnumerator<NativeCurve> IEnumerable<NativeCurve>.GetEnumerator()
    {
      for (int i = 0, n = Count; i < n; ++i)
        yield return Curves[i].NativeCurve;
    }

    IEnumerator IEnumerable.GetEnumerator()
      => ((IEnumerable<NativeCurve>) this).GetEnumerator();

    public int Count => Curves?.Length ?? 0;

    NativeCurve IReadOnlyList<NativeCurve>.this[int index]
      => Curves[index].NativeCurve;

    #endregion
  }

}
