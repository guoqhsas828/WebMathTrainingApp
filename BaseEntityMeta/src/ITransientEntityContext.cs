// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Provides the ability to lookup unique <see cref="PersistentObject"/> instances by ObjectId.
  /// </summary>
  public interface ITransientEntityContext : IEntityContext
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    long GenerateTransientId(Type type);

    /// <summary>
    /// Generate a transient id for the specified anonymous <see cref="PersistentObject"/> and register with this context.
    /// </summary>
    /// <param name="po">The <see cref="PersistentObject"/> to register.</param>
    /// <remarks>
    /// 
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if po == null.</exception>
    /// <exception cref="ArgumentException">Thrown if !po.IsAnonymous.</exception>
    long RegisterTransient(PersistentObject po);

    /// <summary>
    /// Assign the specified id to the specified anonymous <see cref="PersistentObject"/> and associate with this context.
    /// </summary>
    /// <param name="po">The anonymous <see cref="PersistentObject"/> to be registered.</param>
    /// <param name="id">The id to use when registering the <see cref="PersistentObject"/>.</param>
    /// <remarks>
    /// Intended for internal use only when loading a transient entity using an <see cref="IEntityReader"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if po == null.</exception>
    /// <exception cref="ArgumentException">Thrown if !po.IsAnonymous, or id is not a transient id, or an entity with this id is already registered with this context.</exception>
    void RegisterTransient(PersistentObject po, long id);

    /// <summary>
    /// Register any reachable anonymous instances.
    /// </summary>
    /// <remarks>
    /// Used to register any reachable child entities that are anonymous with this context.
    /// </remarks>
    void RegisterTransients();
  }
}