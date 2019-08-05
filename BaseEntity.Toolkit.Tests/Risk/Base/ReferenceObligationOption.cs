namespace BaseEntity.Risk
{
  /// <summary>
  /// The options of specifying the reference obligation for a CDS trade
  /// </summary>
  public enum ReferenceObligationOption
  {
    /// <summary>
    /// A specific reference obligation is selected for a CDS trade
    /// </summary>
    SpecificObligation,

    /// <summary>
    /// SRO - Standard Reference Obligation (published externally for the reference entity specified)
    /// </summary>
    SRO,

    /// <summary>
    /// No reference obligation used for the trade
    /// </summary>
    NoRefOb
  }
}