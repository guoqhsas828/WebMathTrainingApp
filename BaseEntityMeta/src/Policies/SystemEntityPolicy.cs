using System;

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Built-in policy for entities that require Administrator privilege
  /// </summary>
  public class SystemEntityPolicy : EntityPolicy
  {
    private readonly Type _entityType;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityType"></param>
    public SystemEntityPolicy(Type entityType)
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
