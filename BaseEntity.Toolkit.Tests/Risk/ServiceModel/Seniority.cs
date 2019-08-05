/*
 * Seniority.cs
 *
 *
 */

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Seniority level within capital structure
  /// </summary>
  public enum Seniority
  {
    /// <summary>
    ///  Secured (corp) or domestic ccy sovereign (gov) debt
    /// </summary>
    SeniorSecured,

    /// <summary>
    ///  Senior unsecured (corp) or foreign ccy sovereign (gov) debt
    /// </summary>
    SeniorUnsecured,

    /// <summary>
    ///  Subordinated or Lower Tier2 debt
    /// </summary>
    Subordinated,

    /// <summary>
    ///  Junior subordinated or Upper Tier2 debt
    /// </summary>
    JuniorSubordinated,

    /// <summary>
    ///  Preference shares or Lien1 Capital
    /// </summary>
    Equity,

    /// <summary>
    /// 1st Lien Loans in the capital structure. Used for LCDS.
    /// </summary>
    Lien1,

    /// <summary>
    /// 2nd Lien Loans in the capital structure. Used for LCDS.
    /// </summary>
    Lien2,

    /// <summary>
    /// 3rd Lien Loans in the capital structure. Used for LCDS.
    /// </summary>
    Lien3,

    /// <summary>
    /// Mezzanine Lien Loans in the capital structure. Used for LCDS.
    /// </summary>
    Mezzanine,

    /// <summary>
    /// Encompasses Contractual subordination, Structural subordination and Statutory subordination.
    /// </summary>
    SeniorLossAbsorbingCapacity
  }

  // enum Seniority
}
 