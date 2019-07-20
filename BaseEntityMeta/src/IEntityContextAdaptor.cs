// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Provides a mechanism for specifying the behavior when reading or copying entities into a <see cref="IEntityContext"/>
  /// </summary>
  /// <remarks>
  /// At least two use cases need to be supported when using an <see cref="IEntityReader"/> to deserialize
  /// entities into an <see cref="IEditableEntityContext"/>. The first use case involves lazy-loading entities
  /// into the context from a backing store. In the second use case, we are treating the entities as edits to
  /// the existing context that need to be committed back to the backing store upon commit.
  /// </remarks>
  public interface IEntityContextAdaptor
  {
    /// <summary>
    /// Get the entity from the context. If it does not exist, throw an exception.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    PersistentObject Get(long id, ClassMeta entityMeta);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    ObjectRef GetObjectRef(long id);
  }
}