// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Provides the ability to lookup unique <see cref="PersistentObject" /> instances by ObjectId.
  /// </summary>
  /// <remarks>
  ///   <para>An <see cref="IEntityContext" /> is the base interface used to access a repository of <see cref="PersistentObject" /> (entity) instances. It makes no assumptions about whether
  /// the entities are stored in memory, in a database, or some other form. The only guarantee is that it provides a one-to-one mapping between an ObjectId and a
  /// <see cref="PersistentObject" />. In this sense, it can be thought of as an abstraction around a dictionary of entities.</para>
  ///   <para>For example, imagine that you get two <see cref="Trade" /> entities via the context and then dereferenced the Counterparty property on both trades. If both trades
  /// reference the same counterparty, then the context provides a guarantee that only one instance of the referenced LegalEntity will be returned. If the context
  /// supports lazy-loading, the call to the property getter for the first trade will cause the LegalEntity to be loaded, and the call to the same getter for the
  /// second trade will access the already loaded LegalEntity.</para>
  /// </remarks>
  public interface IEntityContext : IDisposable, IEnumerable<PersistentObject>
  {
    /// <summary>
    /// Indicates if this context is open (i.e. not disposed)
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Determines whether this instance is disposed.
    /// </summary>
    bool IsDisposed();

    /// <summary>
    /// Gets the <see cref="PersistentObject" /> with the specified id if it exists within the context, else null
    /// </summary>
    /// <param name="id">The ObjectId of the entity to get</param>
    /// <returns>A PersistentObject with the specified id. If none exists, then null.</returns>
    PersistentObject Get(long id);

    /// <summary>
    /// Gets an <see cref="ObjectRef"/> referencing the <see cref="PersistentObject"/> with the specified id if it exits, else null
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Null if the id represents a transient entity that does not exist within the contxt</returns>
    /// <remarks>
    /// For transient referencies, the ObjectRef that is returned will always be resolved.
    /// </remarks>
    ObjectRef GetObjectRef(long id);
  }
}
