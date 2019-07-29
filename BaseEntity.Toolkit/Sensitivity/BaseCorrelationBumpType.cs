/*
 * BaseCorrelationBumpType.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 * 
 */

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   Types of base correlation bumping in sensitivity analysis
  /// </summary>
  public enum BaseCorrelationBumpType
  {
    /// <summary>
    /// Bump everything together.
    /// </summary>
    /// <remarks>
    ///   Bump the correlation in all the selected in the selected components, tenors
    ///   and at the selected detachment points simulataniouly.
    /// </remarks>
    Uniform,

    /// <summary>
    /// Bump each Tenor separately.
    /// </summary>
    /// <remarks>
    ///   For each tenor, bump the corresponding correlations in the selected components
    ///   and at the selected detachment points simulataniouly.
    /// </remarks>
    ByTenor,

    /// <summary>
    /// Bump each Detachment Point separately.
    /// </summary>
    /// <remarks>
    ///   For each detachment point, bump the corresponding correlations in all the selected
    ///   components and tenors simultaniously.    
    /// </remarks>
    ByStrike,

    /// <summary>
    /// Bump each Surface depended on separately.
    /// </summary>
    /// <remarks>
    ///   For each component, bump the corresponding correlations in the selected tenors
    ///   and at the selected detachment points simulataniouly. 
    /// </remarks>
    ByComponent,

    /// <summary>
    /// Bump each DetachmentPoint/Tenor combination separately.
    /// </summary>
    /// <remarks>
    ///   Bump the selected correlations one by one,  separately for each component, tenor
    ///   and selected detachment point combination.    
    /// </remarks>
    ByPoint
  }

}
