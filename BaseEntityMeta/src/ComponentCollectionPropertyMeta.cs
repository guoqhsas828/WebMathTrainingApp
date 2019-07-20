// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///   Describes a one-to-many relationship with value semantics
  /// </summary>
  public abstract class ComponentCollectionPropertyMeta : CollectionPropertyMeta
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof (ComponentCollectionPropertyMeta));

    #region Constructors

    /// <summary>
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected ComponentCollectionPropertyMeta(ClassMeta entity, ComponentCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Type propType = propInfo.PropertyType;

      if (propType.IsGenericType)
      {
        Type typeDef = propType.GetGenericTypeDefinition();
        if (typeDef != typeof (IList<>) && typeDef != typeof (IDictionary<,>))
        {
          throw new MetadataException(String.Format(
            "Invalid property type [{0}] for {1}.{2} : generic collections must be of type IList<> or IDictionary<,>!",
            propType, entity.Name, propInfo.Name));
        }

        Type[] types = propType.GetGenericArguments();
        Type itemType = (typeDef == typeof (IList<>)) ? types[0] : types[1];
        if (propAttr.Clazz != null)
        {
          if (itemType != propAttr.Clazz)
            throw new MetadataException(String.Format("Clazz [{0}] does not match generic argument type [{1}]", types[0],
              propAttr.Clazz));
        }

        _clazz = itemType;
      }
      else
      {
        throw new MetadataException(String.Format(
          "Invalid property type [{0}] for {1}.{2} : generic collections must be of type IList<> or IDictionary<,>!",
          propType, entity.Name, propInfo.Name));
      }

      _tableName = propAttr.TableName ?? entity.Name + Name;
    }

    #endregion

    #region Properties

    /// <summary>
    ///  Type of collection item
    /// </summary>
    public Type Clazz
    {
      get { return _clazz; }
    }

    /// <summary>
    ///  Child entity
    /// </summary>
    public ClassMeta ChildEntity
    {
      get { return _childEntity; }
    }

    /// <summary>
    /// Table used to store component fields
    /// </summary>
    /// <remarks>
    ///  Note that this table will not have its own generated id, as it gets its identify from the parent.
    /// </remarks>
    public string TableName
    {
      get { return _tableName; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="classCache"></param>
    public override void SecondPassInit(IClassCache classCache)
    {
      base.SecondPassInit(classCache);

      _childEntity = classCache.Find(_clazz);
      if (_childEntity == null)
      {
        throw new MetadataException(String.Format("No entity for type={0}", _clazz));
      }

      Logger.DebugFormat(
        "[{0}.{1}] : CollectionType={2} ChildKey={3}",
        Entity.Name, Name, CollectionType, ChildEntity.HasChildKey);
    }

    /// <summary>
    /// 
    /// </summary>
    public override void ThirdPassInit()
    {
    }

    /// <summary>
    /// Validate that keys are unique
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="list"></param>
    /// <param name="errors"></param>
    protected void ValidateUniqueKeys(object obj, IList list, ArrayList errors)
    {
      var keys = new HashSet<ComponentKey>();
      foreach (BaseEntityObject item in list)
      {
        var key = new ComponentKey(item);
        if (keys.Contains(key))
        {
          InvalidValue.AddError(errors, obj, Name, "Duplicate key [" + key + "]");
        }
        else
        {
          keys.Add(key);
        }
      }
    }

    #endregion

    #region Data

    private readonly Type _clazz;
    private ClassMeta _childEntity;
    private readonly string _tableName;

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  /// <typeparam name="TKey"></typeparam>
  public class MapComponentCollectionPropertyMeta<T, TKey, TValue> : ComponentCollectionPropertyMeta
    where T : BaseEntityObject
    where TValue : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public MapComponentCollectionPropertyMeta(ClassMeta entity, ComponentCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, IDictionary<TKey, TValue>>(propInfo);
      Setter = CreatePropertySetter<T, IDictionary<TKey, TValue>>(propInfo);
    }

    /// <summary>
    /// 
    /// </summary>
    public override ICollection CreateCollection()
    {
      return new Dictionary<TKey, TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override object GetValue(object obj)
    {
      return Getter((T) obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public override void SetValue(object obj, object value)
    {
      Setter((T) obj, (IDictionary<TKey, TValue>) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var mapA = Getter((T) objA);
      var mapB = Getter((T) objB);
      return MapCollectionHelper<TKey, TValue>.IsSame(mapA, mapB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var mapA = Getter((T) objA);
      var mapB = Getter((T) objB);
      return MapCollectionHelper<TKey, TValue>.CreateDelta(mapA, mapB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return MapCollectionHelper<TKey, TValue>.CreateDelta(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadMapCollection<TKey, TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadMapCollection<TKey, TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderFunction()
    {
      var sb = new StringBuilder();
      sb.AppendLine(string.Format("def Read{0}{1}(reader):", Entity.Name, Name));
      sb.AppendLine(string.Format("  contents = Dictionary[{0},ComponentData]()", typeof (TKey).Name));
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    key = reader.ReadValue[" + typeof(TKey).Name + "]()");
      sb.AppendLine("    if reader.ReadBoolean():");
      sb.AppendLine("      contents.Add(key, Read" + ChildEntity.Name + "(reader))");
      sb.AppendLine("    else:");
      sb.AppendLine("      contents.Add(key, ComponentData(\"" + ChildEntity.Name + "\"))");
      sb.AppendLine("  return contents");
      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.WriteMap((IDictionary<TKey, TValue>) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.WriteMap(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="propValue"></param>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      var map = (IDictionary<TKey, ComponentData>) propValue;

      writer.Write(map.Count);

      foreach (var kvp in map)
      {
        writer.WriteValue(kvp.Key);
        if (kvp.Value == null)
        {
          writer.Write(false);
        }
        else
        {
          writer.Write(true);
          kvp.Value.Write(writer);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var map = Getter((T) obj);
      return map == null || map.Count == 0;
    }

    /// <summary>
    ///  Validate items in collection
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      var dict = (IDictionary) Getter((T) obj);
      if (dict != null)
      {
        foreach (BaseEntityObject item in dict.Values)
          item.Validate(errors);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return new Dictionary<TKey, TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, new Dictionary<TKey, TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, IDictionary<TKey, TValue>> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, IDictionary<TKey, TValue>> Setter;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class ListComponentCollectionPropertyMeta<T, TValue> : ComponentCollectionPropertyMeta
    where T : BaseEntityObject
    where TValue : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public ListComponentCollectionPropertyMeta(ClassMeta entity, ComponentCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, IList<TValue>>(propInfo);
      Setter = CreatePropertySetter<T, IList<TValue>>(propInfo);
    }

    /// <summary>
    /// 
    /// </summary>
    public override ICollection CreateCollection()
    {
      return new List<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override object GetValue(object obj)
    {
      return Getter((T) obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public override void SetValue(object obj, object value)
    {
      Setter((T) obj, (IList<TValue>) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var listA = Getter((T) objA);
      var listB = Getter((T) objB);

      if (HasDefaultValue(listA))
      {
        return HasDefaultValue(listB);
      }

      if (HasDefaultValue(listB))
      {
        return false;
      }

      if (listA.Count != listB.Count)
      {
        return false;
      }

      if (ChildEntity.HasChildKey)
      {
        return CompareKeyedCollection(listA, listB);
      }

      return CollectionType == "bag"
        ? BagCollectionHelper<TValue>.IsSame(listA, listB)
        : ListCollectionHelper<TValue>.IsSame(listA, listB);
    }

    /// <summary>
    /// Compares the keyed collection.
    /// </summary>
    /// <param name="oldList">The old list.</param>
    /// <param name="newList">The new list.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    private static bool CompareKeyedCollection(IList<TValue> oldList, IList<TValue> newList)
    {
      var oldMap = oldList.ToDictionary(item => new ComponentKey(item), item => item);
      var newMap = newList.ToDictionary(item => new ComponentKey(item), item => item);

      var newKeys = new HashSet<ComponentKey>();
      foreach (var key in newMap.Keys)
        newKeys.Add(key);

      foreach (var key in oldMap.Keys)
      {
        if (!newMap.ContainsKey(key))
          return false;

        TValue oldValue = oldMap[key];
        TValue newValue = newMap[key];
        if (!ValueComparer<TValue>.IsSame(oldValue, newValue))
          return false;

        newKeys.Remove(key);
      }

      return newKeys.Count == 0;
    }

    private static bool HasDefaultValue(IList<TValue> list)
    {
      return list == null || list.Count == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var listA = Getter((T) objA);
      var listB = Getter((T) objB);

      if (ChildEntity.HasChildKey)
      {
        return DiffKeyedCollection(listA, listB);
      }

      return CollectionType == "bag"
        ? BagCollectionHelper<TValue>.CreateDelta(listA, listB)
        : ListCollectionHelper<TValue>.CreateDelta(listA, listB);
    }

    /// <summary>
    /// Diffs the keyed collection.
    /// </summary>
    private static KeyedCollectionDelta<TValue> DiffKeyedCollection(IList<TValue> oldList, IList<TValue> newList)
    {
      if (IsNullOrEmpty(oldList) && IsNullOrEmpty(newList)) return null;

      oldList = oldList ?? new List<TValue>();
      newList = newList ?? new List<TValue>();

      var mapA = oldList.ToDictionary(item => new ComponentKey(item), item => item);
      var mapB = newList.ToDictionary(item => new ComponentKey(item), item => item);

      var itemDiffs = new List<ObjectDelta>();

      if (!mapA.Any())
      {
        itemDiffs.AddRange(mapB.Select(kvp => new ObjectDelta(ItemAction.Added, kvp.Value)));
        return new KeyedCollectionDelta<TValue>(itemDiffs);
      }

      if (!mapB.Any())
      {
        itemDiffs.AddRange(mapA.Select(kvp => new ObjectDelta(ItemAction.Removed, kvp.Value)));
        return new KeyedCollectionDelta<TValue>(itemDiffs);
      }

      // Used to detect unmatched items
      var unmatchedItems = new Dictionary<ComponentKey, TValue>(mapB);

      foreach (var iter in mapA)
      {
        var itemKey = iter.Key;
        var oldValue = iter.Value;

        TValue newValue;
        if (mapB.TryGetValue(itemKey, out newValue))
        {
          var objectDelta = (ObjectDelta) ClassMeta.CreateDelta(oldValue, newValue);
          if (objectDelta != null)
          {
            itemDiffs.Add(objectDelta);
          }

          unmatchedItems.Remove(itemKey);
        }
        else
        {
          itemDiffs.Add(new ObjectDelta(ItemAction.Removed, oldValue));
        }
      }

      itemDiffs.AddRange(unmatchedItems.Select(entry => new ObjectDelta(ItemAction.Added, entry.Value)));

      return (itemDiffs.Count == 0) ? null : new KeyedCollectionDelta<TValue>(itemDiffs);
    }

    private static bool IsNullOrEmpty(IList<TValue> oldList)
    {
      return oldList == null || oldList.Count == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      if (ChildEntity.HasChildKey)
      {
        return reader.ReadKeyedCollectionDelta<TValue>();
      }

      return CollectionType == "bag"
        ? BagCollectionHelper<TValue>.CreateDelta(reader)
        : ListCollectionHelper<TValue>.CreateDelta(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadListCollection<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadListCollection<TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderFunction()
    {
      var sb = new StringBuilder();
      sb.AppendLine(string.Format("def Read{0}{1}(reader):", Entity.Name, Name));
      sb.AppendLine("  contents = List[ComponentData]()");
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    if reader.ReadBoolean():");
      sb.AppendLine("      contents.Add(Read" + ChildEntity.Name + "(reader))");
      sb.AppendLine("    else:");
      sb.AppendLine("      contents.Add(ComponentData(\"" + ChildEntity.Name + "\"))");
      sb.AppendLine("  return contents");
      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.WriteList((IList<TValue>) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.WriteList(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="propValue"></param>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      var list = (IList<ComponentData>) propValue;

      writer.Write(list.Count);

      foreach (var item in list)
      {
        if (item == null)
        {
          writer.Write(false);
        }
        else
        {
          writer.Write(true);
          item.Write(writer);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var list = Getter((T) obj);
      return list == null || list.Count == 0;
    }

    /// <summary>
    ///  Validate items in collection
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      var list = (IList) Getter((T) obj);
      if (list == null)
        return;

      foreach (BaseEntityObject item in list)
      {
        item.Validate(errors);
      }

      if (ChildEntity.HasChildKey)
        ValidateUniqueKeys(obj, list, errors);
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return new List<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, new List<TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, IList<TValue>> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, IList<TValue>> Setter;
  }
}