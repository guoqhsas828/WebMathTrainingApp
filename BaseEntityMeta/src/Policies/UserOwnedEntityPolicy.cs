using System;
using BaseEntity.Configuration;

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Built-in <see cref="EntityPolicy"/> use by entities that implement <see cref="IUserOwned"/>
  /// </summary>
  public class UserOwnedEntityPolicy : EntityPolicy
  {
    private readonly Type _entityType;

    private static readonly Lazy<string> LazyUserName = new Lazy<string>(InitUserName);

    private static string UserName
    {
      get { return LazyUserName.Value; }
    }

    private static string InitUserName()
    {
      return Configurator.Resolve<ISecurityPolicyImplementor>().UserName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityType"></param>
    public UserOwnedEntityPolicy(Type entityType)
    {
      if (!typeof(IUserOwned).IsAssignableFrom(entityType))
        throw new MetadataException("Entity [" + entityType.Name + "] does not implement IUserOwned");

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
      var po = (IUserOwned) entity;
      if (po.Owner == null) return false;
      return string.Compare(po.Owner.Name, UserName, StringComparison.InvariantCultureIgnoreCase) == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    public override bool CheckPolicy(ISnapshotDelta delta)
    {
      return true;
    }
  }
}
