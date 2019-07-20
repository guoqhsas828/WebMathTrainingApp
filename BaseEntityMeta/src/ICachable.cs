namespace BaseEntity.Metadata
{
  /// <summary>
  /// All objects that can be stored in the WebMathTraining.Database.Caching object cache should implement this
  /// </summary>
  public interface ICachable
  {
    /// <summary>
    /// Return the cache key for an object
    /// </summary>
    string GetCacheKey { get; }
  }
}
