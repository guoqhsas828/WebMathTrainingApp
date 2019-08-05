namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public enum ProductFetchStrategy
  {
    /// <summary>
    /// Do not eager fetch product
    /// </summary>
    None,

    /// <summary>
    /// Eager fetch product
    /// </summary>
    Shallow,

    /// <summary>
    /// Eager fetch product and any of its "join" cascades
    /// </summary>
    Deep
  }
}