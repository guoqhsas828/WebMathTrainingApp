using System;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// Interface to be implemented explicitly by pricers that support semi-analytic sensitivities computation
  /// </summary>
  public interface IAnalyticDerivativesProvider
  {
    /// <summary>
    /// True if instance of pricer supports computation of semi-analytic sensitivities
    /// </summary>
    bool HasAnalyticDerivatives { get; }


    /// <summary>
    ///Returns an IDerivativeCollection which contains derivatives of the PV  
    ///with respect to the ordinates of each underlying curve  
    /// </summary>
    /// <returns>IDerivativeCollection object</returns>
    IDerivativeCollection GetDerivativesWrtOrdinates();
  }
}
