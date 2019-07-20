// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Linq.Expressions;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// A cache of items from the WebMathTraining database.
  ///   Criteria can be provided to allow the cache to be initialized from the database (by default
  ///   all items of the specified type will be loaded). Works with the active session,
  ///   or a <see cref="Session"/> or <see cref="DirectRepository{T}"/> can be provided in the constructor.
  /// </summary>
  /// <typeparam name="T">
  /// The type of persistent object to cache
  /// </typeparam>
  public class EntityCache<T> : CachingRepository<T>
    where T : PersistentObject
  {
    #region Constructors and Destructors

    /// <summary>
    ///   Initializes a new instance of the <see cref = "EntityCache{T}" /> class.
    /// </summary>
    public EntityCache()
      : this(_ => true)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityCache{T}"/> class.
    /// </summary>
    /// <param name="criteria">
    /// The criteria.
    /// </param>
    public EntityCache(Expression<Func<T, bool>> criteria)
      : this(criteria, (IQueryableAndEditableEntityContext)EntityContext.Current)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityCache{T}"/> class.
    /// </summary>
    /// <param name="criteria">
    ///   The criteria.
    /// </param>
    /// <param name="session">
    ///   The session.
    /// </param>
    public EntityCache(Expression<Func<T, bool>> criteria, IQueryableAndEditableEntityContext session)
      : this(criteria, new DirectRepository<T>(session))
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityCache{T}"/> class.
    /// </summary>
    /// <param name="criteria">
    /// The criteria.
    /// </param>
    /// <param name="source">
    /// The source.
    /// </param>
    public EntityCache(Expression<Func<T, bool>> criteria, DirectRepository<T> source)
      : base(source, criteria)
    {}

    #endregion
  }
}