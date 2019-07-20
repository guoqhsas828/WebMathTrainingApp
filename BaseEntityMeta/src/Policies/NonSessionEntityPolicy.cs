using System;

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Built-in <see cref="EntityPolicy"/> for entities that should only be written by direct ADO.NET and have permissions controlled at a higher level (typically by a service operation)
  /// </summary>
  public class NonSessionEntityPolicy : EntityPolicy
  {
    private readonly Type _entityType;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityType"></param>
    public NonSessionEntityPolicy(Type entityType)
    {
      _entityType = entityType;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public override bool IsApplicable(Type type)
    {
      return type == _entityType;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public override bool CheckPolicy(PersistentObject entity, ItemAction action)
    {
      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    public override bool CheckPolicy(ISnapshotDelta delta)
    {
      return false;
    }
  }
}
