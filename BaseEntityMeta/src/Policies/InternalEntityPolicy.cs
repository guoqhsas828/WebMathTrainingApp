using System;
using System.Diagnostics;

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// A built-in <see cref="EntityPolicy"/> where INSERT/UPDATE/DELETE permission is always enabled. 
  /// </summary>
  /// <remarks>
  /// This is intended to be used for entities that are not exposed to applications and 
  /// whose permissions are enforced at a higher level within the service layer.
  /// </remarks>
  public sealed class InternalEntityPolicy : EntityPolicy
  {
    private readonly Type _entityType;

    /// <summary>
    /// Create instance for given entityType
    /// </summary>
    /// <param name="entityType"></param>
    public InternalEntityPolicy(Type entityType)
    {
      if (entityType == null) throw new ArgumentNullException("entityType");
      Debug.Assert(typeof(PersistentObject).IsAssignableFrom(entityType));
      _entityType = entityType;
    }

    /// <summary>
    /// Return true if this policy instance applies to the specified type.
    /// </summary>
    public override bool IsApplicable(Type type)
    {
      return _entityType == type;
    }

    /// <summary>
    /// Returns true
    /// </summary>
    public override bool CheckPolicy(PersistentObject entity, ItemAction action)
    {
      // TODO: In the future we could potentially check to make sure we are running within service and throw if not
      return true;
    }

    /// <summary>
    /// Returns true
    /// </summary>
    public override bool CheckPolicy(ISnapshotDelta delta)
    {
      // TODO: In the future we could potentially check to make sure we are running within service and throw if not
      return true;
    }
  }
}
