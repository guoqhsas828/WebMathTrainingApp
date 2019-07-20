namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public enum HistorizationPolicy
  {
    /// <summary>
    ///  Don't historize
    /// </summary>
    None,
    /// <summary>
    ///   Historize both newly captured entities and updates to existing entities
    /// </summary>
    All,
    /// <summary>
    ///   Historize only updates to existing entities
    /// </summary>
    UpdatesOnly,
  }
}