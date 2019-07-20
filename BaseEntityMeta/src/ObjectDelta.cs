
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Used to describe the result of diff'ing two different objects (either different types,
  /// or different identities).  Also used to describe the result of diff'ing an object with
  /// a null value.
  /// </summary>
  public sealed class ObjectDelta : ISnapshotDelta, ICollectionItemDelta
  {
    /// <summary>
    /// Create an ObjectDelta for an added or removed instance
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="state">The state.</param>
    /// <remarks></remarks>
    public ObjectDelta(ItemAction action, BaseEntityObject state)
    {
      if (state == null)
      {
        throw new ArgumentNullException("state");
      }

      switch (action)
      {
        case ItemAction.Added:
          NewState = state;
          break;
        case ItemAction.Removed:
          OldState = state;
          break;
        default:
          throw new ArgumentException("Invalid action [" + action + "]");
      }

      ItemAction = action;

      ClassMeta = ClassCache.Find(state);

      var po = state as PersistentObject;
      if (po != null)
      {
        if (po.ObjectId == 0)
        {
          throw new InvalidOperationException("Cannot create ObjectDelta for transient (unsaved) entity");
        }
        Key = new EntityKey(po.ObjectId);
      }
      else
      {
        Key = new ComponentKey(state);
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDelta"/> class.for a Changed action
    /// </summary>
    /// <param name="classMeta">The <see cref="ClassMeta"/></param>
    /// <param name="key">The key.</param>
    /// <param name="oldState"></param>
    /// <param name="newState"></param>
    /// <remarks></remarks>
    public ObjectDelta(ClassMeta classMeta, ObjectKey key, BaseEntityObject oldState, BaseEntityObject newState)
    {
      if (classMeta == null)
      {
        throw new ArgumentNullException("classMeta");
      }
      if (key == null)
      {
        throw new ArgumentNullException("key");
      }

      ClassMeta = classMeta;

      Key = key;

      PropertyDeltas = new Dictionary<PropertyMeta, ISnapshotDelta>();
      foreach (PropertyMeta pm in classMeta.PersistentPropertyMap.Values)
      {
        var propertyDelta = pm.CreateDelta(oldState, newState);
        if (propertyDelta != null)
          PropertyDeltas[pm] = propertyDelta;
      }

      OldState = oldState;
      NewState = newState;

      ItemAction = ItemAction.Changed;
    }

    /// <summary>
    /// Gets or sets the entity.
    /// </summary>
    /// <value>The entity.</value>
    /// <remarks></remarks>
    public ClassMeta ClassMeta { get; set; }

    /// <summary>
    /// Gets the action.
    /// </summary>
    /// <remarks></remarks>
    public ItemAction ItemAction { get; private set; }

    /// <summary>
    /// Gets the key.
    /// </summary>
    /// <remarks></remarks>
    public ObjectKey Key { get; private set; }

    /// <summary>
    /// Gets the old state.
    /// </summary>
    /// <remarks></remarks>
    public BaseEntityObject OldState { get; private set; }

    /// <summary>
    /// Gets the new state.
    /// </summary>
    /// <remarks></remarks>
    public BaseEntityObject NewState { get; private set; }

    /// <summary>
    /// Gets the property deltas.
    /// </summary>
    /// <remarks></remarks>
    public IDictionary<PropertyMeta, ISnapshotDelta> PropertyDeltas { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public ISnapshotDelta ItemDelta { get { return this; } }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    public void Serialize(IEntityDeltaWriter writer)
    {
      writer.WriteDelta(this);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsScalar
    {
      get { return false; }
    }

    /// <summary>
    /// ToString function.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return string.Format("{0} {1}", ItemAction, ClassMeta.Name);
    }
  }
}