// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// A repository for caching items sourced from another repository
  /// </summary>
  /// <typeparam name="T">
  /// The type of the repository (item)
  /// </typeparam>
  public class CachingRepository<T> : ICachingRepository<T>
  {
    #region Constants and Fields

    /// <summary>
    ///   The cache.
    /// </summary>
    private readonly List<T> _cache;

    /// <summary>
    ///   The deleted items.
    /// </summary>
    private readonly HashSet<T> _deleted = new HashSet<T>();

    /// <summary>
    ///   The inserted items.
    /// </summary>
    private readonly HashSet<T> _inserted = new HashSet<T>();

    /// <summary>
    ///   The source repository from which to cache items.
    /// </summary>
    private readonly IRepository<T> _source;

    /// <summary>
    ///   The updated items.
    /// </summary>
    private readonly HashSet<T> _updated = new HashSet<T>();

    #endregion

    #region Constructors and Destructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingRepository{T}"/> class.
    /// </summary>
    /// <param name="source">
    /// The source.
    /// </param>
    public CachingRepository(IRepository<T> source)
      : this(source, t => true)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingRepository{T}"/> class.
    /// </summary>
    /// <param name="source">
    /// The source.
    /// </param>
    /// <param name="criteria">
    /// The criteria.
    /// </param>
    public CachingRepository(IRepository<T> source, Expression<Func<T, bool>> criteria)
    {
      _source = source;
      _cache = _source.Items.Where(criteria).ToList();
    }

    #endregion

    #region Public Properties

    /// <summary>
    ///   Gets the removed items.
    /// </summary>
    /// <value>The removed items.</value>
    public IEnumerable<T> DeletedItems => _deleted;

    /// <summary>
    ///   Gets the inserted items.
    /// </summary>
    /// <value>The inserted items.</value>
    public IEnumerable<T> InsertedItems => _inserted;

    /// <summary>
    ///   Gets the items in the repository (query).
    /// </summary>
    /// <value>The items (query).</value>
    public IQueryable<T> Items => _cache.AsQueryable();

    /// <summary>
    ///   Gets the source repository.
    /// </summary>
    /// <value>The source repository.</value>
    public IRepository<T> Source => _source;

    /// <summary>
    ///   Gets the updated items.
    /// </summary>
    /// <value>The updated items.</value>
    public IEnumerable<T> UpdatedItems => _updated;

    #endregion

    #region Public Methods

    /// <summary>
    /// Begin updates to the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    public void BeginUpdate(T item)
    {
      _source.BeginUpdate(item);
      _updated.Add(item);
    }

    /// <summary>
    /// Creates an item.
    /// </summary>
    /// <returns>
    /// The newly created item
    /// </returns>
    public T Create()
    {
      return _source.Create();
    }

    /// <summary>
    /// Deletes the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    public void Delete(T item)
    {
      _cache.Remove(item);
      _source.Delete(item);
      _deleted.Add(item);
    }

    /// <summary>
    /// Saves the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    public void Save(T item)
    {
      _cache.Add(item);
      _source.Save(item);
      _inserted.Add(item);
    }

    #endregion
  }
}