using System;
using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Maintains a cache of expensive-to-establish values derived from a versioned object.
  /// NOT compile-time type-safe, but does not require an instance of the versioned object.
  /// When presented with a new version id and version for a versioned object, the associated derived value for that object will be re-established.
  /// </summary>
  /// <typeparam name="TVersioned">The type of the versioned object.</typeparam>
  /// <typeparam name="TCached">The type of the cached value.</typeparam>
  public class VersionedCacheLite<TVersioned, TCached>
    where TVersioned : VersionedObject
    where TCached : class
  {
    private Dictionary<long, VersionedCacheValue<TCached>> _cache;
    private readonly Func<long, int, TCached> _cacheLoader;
    private readonly Func<Dictionary<long, VersionedCacheValue<TCached>>> _cacheInitializer;
    private static readonly int EntityId;

    static VersionedCacheLite()
    {
      var classMeta = ClassCache.Find(typeof(TVersioned));
      EntityId = classMeta.EntityId;
    } 

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedCache&lt;TVersioned, TCached&gt;"/> class.
    /// </summary>
    /// <param name="cacheLoader">The cache loader.</param>
    public VersionedCacheLite(Func<long, int, TCached> cacheLoader)
    {
      _cacheLoader = cacheLoader;
      _cacheInitializer = () => new Dictionary<long, VersionedCacheValue<TCached>>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedCache&lt;TVersioned, TCached&gt;"/> class.
    /// </summary>
    /// <param name="cacheLoader">loads the cache entry for a particular versioned object</param>
    /// <param name="cacheInitializer">called once to initialize the entire cache the first time the cache is needed</param>
    public VersionedCacheLite(Func<long, int, TCached> cacheLoader, Func<Dictionary<long, VersionedCacheValue<TCached>>> cacheInitializer)
    {
      _cacheLoader = cacheLoader;
      _cacheInitializer = cacheInitializer;
    }

    /// <summary>
    /// Gets the cached derived value for the specified versioned object.
    /// </summary>
    /// <param name="versionedObjectId">The versioned object id.</param>
    /// <param name="objectVersion">The object version.</param>
    /// <returns></returns>
    public TCached Get(long versionedObjectId, int objectVersion)
    {
      var versionedObjectEntityId = EntityHelper.GetEntityIdFromObjectId(versionedObjectId);
      if (versionedObjectEntityId != EntityId)
      {
        throw new MetadataException(string.Format("ObjectId {0} (EntityId {1}) is incompatible with this cache (Entity Id {2})", versionedObjectId, versionedObjectEntityId, EntityId));
      }

      if (_cache == null)
        _cache = _cacheInitializer();

      var key = versionedObjectId;
      VersionedCacheValue<TCached> versionedCacheValue;
      if (!_cache.TryGetValue(key, out versionedCacheValue))
      {
        // Cache miss
        _cache[key] = versionedCacheValue = new VersionedCacheValue<TCached> { Version = objectVersion };
      }
      if (versionedCacheValue.Version < objectVersion || versionedCacheValue.Value == null)
      {
        // Establish (or re-establish) the derived value
        versionedCacheValue.Value = _cacheLoader(versionedObjectId, objectVersion);
        versionedCacheValue.Version = objectVersion;
      }
      return versionedCacheValue.Value;
    }
  }
}
