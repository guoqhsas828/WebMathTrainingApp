/*
 * RestructuringType.cs
 *
 */

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Indicates whether restructuring is included as a credit event and, if it is included,
  ///   what limitations, if any, apply to the deliverable obligations.
  /// </summary>
  public enum RestructuringType
  {
    /// <summary>
    ///  Exclude restructuring as a credit event
    /// </summary>
    /// <markit>XR</markit>
    Excluded,

    /// <summary>Include restructuring as a credit event, according to the original language of the ISDA 1999 Definitions</summary>
    /// <markit>CR</markit>
    Included,

    /// <summary>Include restructuring, with "Restructuring Maturity Limitation and Fully Transferable Obligation Applicable" as per the May 11, 2001 "Restructuring Supplement To the 1999 ISDA Credit Derivatives Definitions"</summary>
    /// <markit>MR</markit>
    Modified,

    /// <summary>Include restructuring, with "Restructuring Maturity Limitation and Conditionally Transferable Obligation Applicable" as per the ISDA 2003 Definitions</summary>
    /// <markit>MM</markit>
    ModifiedModified,
    /// <summary>
    ///  Exclude restructuring as a credit event as per the ISDA 2014 Definitions
    /// </summary>
    /// <markit>XR14</markit>
    Excluded14,

    /// <summary>Include restructuring as a credit event, according to the original language of the ISDA 2014 Definitions</summary>
    /// <markit>CR14</markit>
    Included14,

    /// <summary>Include restructuring, with "Restructuring Maturity Limitation and Fully Transferable Obligation Applicable" as per the May 11, 2001 "Restructuring Supplement To the 2014 ISDA Credit Derivatives Definitions"</summary>
    /// <markit>MR14</markit>
    Modified14,

    /// <summary>Include restructuring, with "Restructuring Maturity Limitation and Conditionally Transferable Obligation Applicable" as per the ISDA 2014 Definitions</summary>
    /// <markit>MM14</markit>
    ModifiedModified14
  }

  // RestructuringType
}
