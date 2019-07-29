
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Interface for Quanto-Credit derivatives, i.e. credit derivatives subject to FX risk.
  /// </summary>
  public interface IQuantoDefaultSwapPricer
  {
    /// <summary>
    /// FxCurve
    /// </summary>
    FxCurve FxCurve { get; }

    /// <summary>
    /// At the money forward FX Black volatility
    /// </summary>
    VolatilityCurve FxVolatility { get; }

    /// <summary>
    /// Correlation between FX (from numeraire currency to quote currency) forward and the Gaussian transform of default time / default times.  
    /// </summary>
    double FxCorrelation { get; set; }

    /// <summary>
    /// Jump of forward FX (from numeraire currency to quote currency)<m>\theta FX_{\tau-} = FX_{\tau} - FX_{\tau-}</m>  at default times
    /// </summary>
    double FxDevaluation { get; set; }
  }
}
