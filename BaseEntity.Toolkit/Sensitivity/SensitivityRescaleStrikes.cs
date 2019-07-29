using System;

namespace BaseEntity.Toolkit.Sensitivity
{
  ///<summary>
  /// Rescale strike points when using base correlations.
  ///</summary>
  /// <remarks>  
  /// RescaleStrikes can be overriden on Sensitivity RiskCalculator or the setting can be taken from the pricer
  /// </remarks>
  public enum SensitivityRescaleStrikes
  {
    /// <summary>Use the RescaleStrikes value set on the Product's pricer.</summary>
    UsePricerSetting,
    /// <summary>Do not rescale strikes.</summary>
    No,
    /// <summary>Rescale strikes.</summary>
    Yes
  }
}
