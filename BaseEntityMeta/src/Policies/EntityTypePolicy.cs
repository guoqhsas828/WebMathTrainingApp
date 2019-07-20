using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Used to specify permssions based solely on the type of entity
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public class EntityTypePolicy : EntityPolicy
  {
    /// <summary>
    /// 
    /// </summary>
    public EntityTypePolicy()
    {
      Permissions = new Dictionary<string, Permission>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public override bool IsApplicable(Type type)
    {
      return Permissions.ContainsKey(type.Name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public override bool CheckPolicy(PersistentObject entity, ItemAction action)
    {
      var type = ClassCache.Find(entity).Type;

      Permission p;
      if (Permissions.TryGetValue(type.Name, out p))
      {
        switch (action)
        {
          case ItemAction.Added:
            return p.CanCreate;
          case ItemAction.Changed:
            return p.CanUpdate;
          case ItemAction.Removed:
            return p.CanDelete;
        }
      }

      return false;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      foreach (var entityName in Permissions.Keys)
      {
        var cm = ClassCache.Find(entityName);
        if (cm == null)
        {
          InvalidValue.AddError(errors, this, $"Invalid EntityTypePolicy : entity [{entityName}] does not exist");
        }
        else if (cm.IsAbstract)
        {
          InvalidValue.AddError(errors, this, $"Invalid EntityTypePolicy : entity [{entityName}] is abstract");
        }
        else if (cm.IsChildEntity)
        {
          InvalidValue.AddError(errors, this, $"Invalid EntityTypePolicy : entity [{entityName}] is child entity");
        }
        else if (cm.EntityPolicy != null)
        {
          InvalidValue.AddError(errors, this, $"Invalid EntityTypePolicy : entity [{entityName}] has built-in policy defined");
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [ComponentCollectionProperty]
    public IDictionary<string, Permission> Permissions { get; private set; }
  }
}
