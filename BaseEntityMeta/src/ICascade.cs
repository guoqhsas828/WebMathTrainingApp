// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface ICascade
  {
    /// <summary>
    /// The Name of the <see cref="PropertyMeta">PropertyMeta</see> for this Cascade
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The <see cref="ClassMeta">Entity</see> for this <see cref="PropertyMeta">PropertyMeta</see>
    /// </summary>
    /// <remarks>
    /// May specify an abstract base entity, in which case actual references would be to derived types
    /// </remarks>
    ClassMeta Entity { get; }

    /// <summary>
    /// The referenced <see cref="ClassMeta">Entity</see>
    /// </summary>
    /// <remarks>
    /// May specify an abstract base entity, in which case actual references would be to derived types.
    /// </remarks>
    ClassMeta ReferencedEntity { get; }

    /// <summary>
    /// Specifies the <see cref="Cardinality">Cardinality</see> of the relationship viewed from the owning <see cref="ClassMeta">Entity</see>
    /// </summary>
    Cardinality Cardinality { get; }

    /// <summary>
    /// The column used to join to the ReferencedEntity
    /// </summary>
    string JoinColumn { get; }

    /// <summary>
    /// Indicates if this is a bidirectional OneToMany or ManyToMany
    /// </summary>
    bool IsInverse { get; }

    /// <summary>
    /// 
    /// </summary>
    ICascade InverseCascade { get; }

    /// <summary>
    /// 
    /// </summary>
    string Cascade { get; }

    /// <summary>
    /// 
    /// </summary>
    string Fetch { get; }

    /// <summary>
    /// Returns an <see cref="IEnumerable{PersistentObject}">IEnumerable</see> on the referenced object or objects.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    IEnumerable<PersistentObject> ReferencedObjects(object obj);
  }
}