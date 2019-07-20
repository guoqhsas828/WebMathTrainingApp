using System;
using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Maintains a cache of expensive-to-establish values derived from a versioned object.
  /// Compile-time type-safe, but requires an instance of the versioned object.
  /// When presented with a new version (instance) of a versioned object, the associated derived value for that object will be re-established.
  /// </summary>
  /// <typeparam name="TVersioned">The type of the versioned object.</typeparam>
  /// <typeparam name="TCached">The type of the cached value.</typeparam>
  public class VersionedCache<TVersioned, TCached>
    where TVersioned : VersionedObject 
    where TCached : class
  {
  	private Dictionary<long, VersionedCacheValue<TCached>> _cache;
		private readonly Func<TVersioned, TCached> _cacheLoader;
  	private readonly Func<Dictionary<long, VersionedCacheValue<TCached>>> _cacheInitializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedCache&lt;TVersioned, TCached&gt;"/> class.
    /// </summary>
    /// <param name="cacheLoader">The cache loader.</param>
    public VersionedCache(Func<TVersioned, TCached> cacheLoader)
    {
      _cacheLoader = cacheLoader;
			_cacheInitializer = ()=>new Dictionary<long, VersionedCacheValue<TCached>>();
    }

		/// <summary>
		/// Initializes a new instance of the <see cref="VersionedCache&lt;TVersioned, TCached&gt;"/> class.
		/// </summary>
		/// <param name="cacheLoader">loads the cache entry for a particular versioned object</param>
		/// <param name="cacheInitializer">called once to initialize the entire cache the first time the cache is needed</param>
		public VersionedCache(Func<TVersioned, TCached> cacheLoader, Func<Dictionary<long,VersionedCacheValue<TCached>>> cacheInitializer)
		{
			_cacheLoader = cacheLoader;
			_cacheInitializer = cacheInitializer;
		}

    /// <summary>
    /// Gets the cached derived value for the specified versioned object.
    /// </summary>
    /// <param name="versionedObject">The versioned object.</param>
    /// <returns></returns>
    public TCached Get(TVersioned versionedObject)
    {
			if (_cache == null)
				_cache = _cacheInitializer();

      var key = versionedObject.ObjectId;
      VersionedCacheValue<TCached> versionedCacheValue;
      if (!_cache.TryGetValue(key, out versionedCacheValue))
      {
        // Cache miss
        _cache[key] = versionedCacheValue = new VersionedCacheValue<TCached> { Version = versionedObject.ObjectVersion };
      }
      if (versionedCacheValue.Version < versionedObject.ObjectVersion || versionedCacheValue.Value == null)
      {
        // Establish (or re-establish) the derived value
        versionedCacheValue.Value = _cacheLoader(versionedObject);
        versionedCacheValue.Version = versionedObject.ObjectVersion;
      }
      return versionedCacheValue.Value;
    }
  }

	/// <summary>
	/// Item within a versioned cache <see cref="VersionedCache&lt;TVersioned, TCached&gt;"/>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class VersionedCacheValue<T>
	{
		/// <summary>
		/// Version of versioned object
		/// </summary>
		public int Version { get; set; }

		/// <summary>
		/// Cache value for a particular versioned object
		/// </summary>
		public T Value { get; set; }
	}
}
