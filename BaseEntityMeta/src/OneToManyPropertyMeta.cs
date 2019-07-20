// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///   Describes a one-to-many relationship with reference semantics.
  /// </summary>
  public abstract class OneToManyPropertyMeta : CollectionPropertyMeta, ICascade
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(OneToManyPropertyMeta));

    #region Constructors

    /// <summary>
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected OneToManyPropertyMeta(ClassMeta entity, OneToManyPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Type propType = propInfo.PropertyType;

      if (propType.IsGenericType)
      {
        Type typeDef = propType.GetGenericTypeDefinition();
        if (typeDef != typeof(IList<>) && typeDef != typeof(IDictionary<,>))
        {
          throw new MetadataException(String.Format(
            "Invalid property type [{0}] for {1}.{2} : OneToManyProperty must be of type IList<> or IDictionary<,>!",
            typeDef, entity.Name, propInfo.Name));
        }

        Type[] types = propType.GetGenericArguments();
        Type itemType = (typeDef == typeof(IList<>)) ? types[0] : types[1];
        if (propAttr.Clazz != null)
        {
          if (itemType != propAttr.Clazz)
          {
            throw new MetadataException(String.Format(
              "Clazz [{0}] does not match generic argument type [{1}]",
              types[0], propAttr.Clazz));
          }
        }

        Clazz = itemType;
      }
      else
      {
        throw new MetadataException(String.Format(
          "Invalid property type [{0}] for [{1}.{2}] : OneToManyProperty must be of type IList<> or IDictionary<,>!",
          propType.Name, entity.Name, propInfo.Name));
      }

      _cascade = propAttr.Cascade ?? "none";

      if (_cascade != "none" &&
          _cascade != "save-update" &&
          _cascade != "all" &&
          _cascade != "all-delete-orphan")
      {
        throw new MetadataException(String.Format(
          "Invalid Cascade [{0}] for property [{1}.{2}]",
          _cascade, Entity.Name, Name));
      }
      if (Entity.IsComponent)
      {
        throw new MetadataException(String.Format(
          "Invalid property [{0}.{1}] : Cannot define OneToMany property on Component",
          Entity.Name, Name));
      }

      Column = null;

      _fetch = propAttr.Fetch ?? "select";
      _adderName = propAttr.Adder;
      _removerName = propAttr.Remover;
      _isInverse = propAttr.IsInverse;
      UseJoinTable = propAttr.UseJoinTable;
      JoinTableName = propAttr.JoinTableName;

      Logger.DebugFormat("[{0}.{1}] : CollectionType={2}", Entity.Name, Name, CollectionType);
    }

    #endregion

    #region Properties

    /// <summary>
    ///  Type of collection item
    /// </summary>
    public Type Clazz { get; }

    /// <summary>
    /// Specifies the method to call to add items to a collection that
    /// is part of a bi-directional association.
    /// </summary>
    public MethodInfo Adder
    {
      get
      {
        if (_adder == null && _adderName != null)
          _adder = Entity.Type.GetMethod(_adderName);
        return _adder;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public MethodInfo Remover
    {
      get
      {
        if (_remover == null && _removerName != null)
          _remover = Entity.Type.GetMethod(_removerName);
        return _remover;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsChild => _isChild;

    /// <summary>
    ///   Name of the join table. Only applicable when UseJoinTable is set to true
    /// </summary>
    public string JoinTableName { get; private set; }

    /// <summary>
    ///   Use join table to link owner and collection items instead 
    ///   of adding a reference to owner in the collection item table
    /// </summary>
    public bool UseJoinTable { get; private set; }

    #endregion

    #region ICascade Members

    /// <summary>
    /// Returns the <see cref="ClassMeta">ClassMeta</see> for the referenced entity.
    /// </summary>
    public ClassMeta ReferencedEntity
    {
      get { return _referencedEntity; }
    }

    /// <summary>
    /// 
    /// </summary>
    public ICascade InverseCascade
    {
      get { return _inverseCascade; }
    }

    /// <summary>
    /// Strategy used for propagating database operations (insert/update/delete) from parent to child
    /// </summary>
    public string Cascade
    {
      get { return _cascade; }
    }

    /// <summary>
    /// </summary>
    public string Fetch
    {
      get { return _fetch; }
    }

    /// <summary>
    /// </summary>
    public bool IsInverse
    {
      get { return _isInverse; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public IEnumerable<PersistentObject> ReferencedObjects(object obj)
    {
      var coll = (ICollection)GetValue(obj);
      if (coll == null)
      {
        return new PersistentObject[0];
      }

      if (CollectionType == "map")
      {
        var dict = (IDictionary)coll;
        return dict.Values.Cast<PersistentObject>();
      }

      if (CollectionType == "list" || CollectionType == "bag")
      {
        return coll.Cast<PersistentObject>();
      }

      throw new MetadataException(String.Format(
        "Invalid CollectionType [{0}] for property [{1}.{2}]",
        CollectionType, Entity.Name, Name));
    }

    /// <summary>
    /// 
    /// </summary>
    public string JoinColumn
    {
      get { return KeyColumns[0]; }
    }

    /// <summary>
    /// 
    /// </summary>
    public Cardinality Cardinality
    {
      get { return Cardinality.OneToMany; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ICascade FindInverse()
    {
      var cascadeList = ReferencedEntity.CascadeList;
      foreach (var otherCascade in cascadeList)
      {
        if (otherCascade.ReferencedEntity == Entity &&
            otherCascade.Cardinality == Cardinality.ManyToOne &&
            otherCascade.JoinColumn == JoinColumn)
        {
          return otherCascade;
        }
      }
      if (_isInverse)
      {
        throw new MetadataException(String.Format(
          "Unable to find inverse of [{0}] in [{1}]",
          this, ReferencedEntity.Name));
      }
      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    public override void SecondPassInit(IClassCache classCache)
    {
      _referencedEntity = classCache.Find(Clazz);
      if (_referencedEntity == null)
      {
        throw new MetadataException(String.Format(
          "Invalid ReferencedEntity [{0}] for property [{1}.{2}]",
          Clazz.Name, Entity.Name, Name));
      }

      _isChild = _referencedEntity.IsChildEntity;
    }

    /// <summary>
    /// 
    /// </summary>
    public override void ThirdPassInit()
    {
      _inverseCascade = FindInverse();

      if (_isChild)
      {
        if (Entity.OldStyleValidFrom)
        {
          if (!_referencedEntity.OldStyleValidFrom)
          {
            throw new MetadataException(string.Format(
              "Entity [{0}] has OldStyleValidFrom=true but ReferencedEntity [{1}] has OldStyleValidFrom=false",
              Entity.Name, ReferencedEntity.Name));
          }
        }
        else
        {
          if (_referencedEntity.OldStyleValidFrom)
          {
            throw new MetadataException(string.Format(
              "Entity [{0}] has OldStyleValidFrom=false but ReferencedEntity [{1}] has OldStyleValidFrom=true",
              Entity.Name, ReferencedEntity.Name));
          }
        }
      }

      // Referenced entity must have business key defined unless it is owned or is the owner (this is a requirement of DataImporter)
      if (_cascade == "none" &&
          !_referencedEntity.HasKey &&
          (_inverseCascade == null || _inverseCascade.Cascade == "none"))
      {
        throw new MetadataException(String.Format(
          "Invalid ReferencedEntity [{0}] for property [{1}.{2}] : Does not define unique business key",
          Clazz.Name, Entity.Name, Name));
      }
    }

    #endregion

    #region Data

    private readonly string _cascade;
    private readonly string _fetch;
    private readonly bool _isInverse;
    private ICascade _inverseCascade;
    private ClassMeta _referencedEntity;
    private bool _isChild;
    private readonly string _adderName;
    private readonly string _removerName;
    private MethodInfo _adder;
    private MethodInfo _remover;

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  /// <typeparam name="TKey"></typeparam>
  public class MapOneToManyPropertyMeta<T, TKey, TValue> : OneToManyPropertyMeta 
    where T : BaseEntityObject 
    where TValue : PersistentObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public MapOneToManyPropertyMeta(ClassMeta entity, OneToManyPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, IDictionary<TKey, TValue>>(propInfo);
      Setter = CreatePropertySetter<T, IDictionary<TKey, TValue>>(propInfo);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override object GetValue(object obj)
    {
      return Getter((T)obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public override void SetValue(object obj, object value)
    {
      Setter((T)obj, (IDictionary<TKey, TValue>)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var map = Getter((T)obj);
      return map == null || map.Count == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var mapA = Getter((T)objA);
      var mapB = Getter((T)objB);

      return MapCollectionHelper<TKey, ObjectRef>.IsSame(
        CreateObjectRefMap(mapA), CreateObjectRefMap(mapB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var mapA = Getter((T)objA);
      var mapB = Getter((T)objB);

      return MapCollectionHelper<TKey, ObjectRef>.CreateDelta(
        CreateObjectRefMap(mapA), CreateObjectRefMap(mapB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return MapCollectionHelper<TKey, ObjectRef>.CreateDelta(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadMapCollection<TKey, ObjectRef>().ToDictionary(
        kvp => kvp.Key, kvp => (TValue)reader.Adaptor.Get(kvp.Value.Id, null));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      bool newMap = false;

      var map = Getter((T)obj);
      if (map == null)
      {
        newMap = true;
        map = (IDictionary<TKey, TValue>)CreateCollection();
      }

      var keys = new HashSet<TKey>(map.Keys);

      foreach (var kvp in reader.ReadMapCollection<TKey, ObjectRef>())
      {
        var key = kvp.Key;
        var objectRef = kvp.Value;
        map[key] = (TValue)reader.Adaptor.Get(objectRef.Id, null);
        keys.Remove(key);
      }

      foreach (var key in keys)
      {
        map.Remove(key);
      }

      if (newMap)
        Setter((T)obj, map);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      var map = (IDictionary<TKey, TValue>)value;

      writer.WriteMap(CreateObjectRefMap(map));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      var map = Getter((T)obj);

      writer.WriteMap(CreateObjectRefMap(map));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderFunction()
    {
      var sb = new StringBuilder();
      sb.AppendLine(string.Format("def Read{0}{1}(reader):", Entity.Name, Name));
      sb.AppendLine(string.Format("  contents = Dictionary[{0},Int64]()", typeof(TKey).Name));
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    key = reader.ReadValue[" + typeof(TKey).Name + "]()");
      sb.AppendLine("    if reader.ReadBoolean():");
      sb.AppendLine("      contents[key] = reader.ReadInt64()");
      sb.AppendLine("  return contents");
      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="propValue"></param>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      var map = (IDictionary<TKey, Int64>)propValue;

      writer.Write(map.Count);

      foreach (var kvp in map)
      {
        writer.WriteValue(kvp.Key);

        writer.Write(true);

        writer.Write(kvp.Value);
      }
    }

    /// <summary>
    ///  Validate items in collection
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      var dict = (IDictionary)Getter((T)obj);
      if (dict != null)
      {
        foreach (PersistentObject item in dict.Values)
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
      Setter((T)obj, new Dictionary<TKey, TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, IDictionary<TKey, TValue>> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, IDictionary<TKey, TValue>> Setter;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override ICollection CreateCollection()
    {
      return new Dictionary<TKey, TValue>();
    }

    private static IDictionary<TKey, ObjectRef> CreateObjectRefMap(IDictionary<TKey, TValue> map)
    {
      return map.ToDictionary(kvp => kvp.Key, kvp => ObjectRef.Create(kvp.Value));
    }
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class ListOneToManyPropertyMeta<T, TValue> : OneToManyPropertyMeta
    where T : BaseEntityObject
    where TValue : PersistentObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public ListOneToManyPropertyMeta(ClassMeta entity, OneToManyPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, IList<TValue>>(propInfo);
      Setter = CreatePropertySetter<T, IList<TValue>>(propInfo);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override object GetValue(object obj)
    {
      return Getter((T)obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public override void SetValue(object obj, object value)
    {
      Setter((T)obj, (IList<TValue>)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var list = Getter((T)obj);
      return list == null || list.Count == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var listA = Getter((T)objA);
      var listB = Getter((T)objB);
      return ListCollectionHelper<ObjectRef>.IsSame(
        CreateObjectRefList(listA), CreateObjectRefList(listB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var listA = Getter((T)objA);
      var listB = Getter((T)objB);

      return ListCollectionHelper<ObjectRef>.CreateDelta(
        CreateObjectRefList(listA), CreateObjectRefList(listB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return ListCollectionHelper<ObjectRef>.CreateDelta(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      var list = (IList<TValue>)CreateCollection();
      foreach (var objectRef in reader.ReadListCollection<ObjectRef>())
      {
        list.Add((TValue)reader.Adaptor.Get(objectRef.Id, null));
      }
      return list;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      bool newList = false;

      var list = Getter((T)obj);
      if (list == null)
      {
        newList = true;
        list = (IList<TValue>)CreateCollection();
      }

      var oldCount = list.Count;

      int newCount = 0;
      foreach (var objectRef in reader.ReadListCollection<ObjectRef>())
      {
        var value = (TValue)reader.Adaptor.Get(objectRef.Id, null);
        if (newCount >= list.Count)
        {
          list.Add(value);
        }
        else
        {
          list[newCount] = value;
        }
        ++newCount;
      }

      // Remove any items after last one read
      for (int j = oldCount - 1; j >= newCount; --j)
      {
        list.RemoveAt(j);
      }

      if (newList)
        Setter((T)obj, list);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      var list = (IList<TValue>)value;

      writer.WriteList(CreateObjectRefList(list));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      var list = Getter((T)obj);
      
      writer.WriteList(CreateObjectRefList(list));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderFunction()
    {
      var sb = new StringBuilder();
      sb.AppendLine(string.Format("def Read{0}{1}(reader):", Entity.Name, Name));
      sb.AppendLine("  contents = List[Int64]()");
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    if reader.ReadBoolean():");
      sb.AppendLine("      contents.Add(reader.ReadInt64())");
      sb.AppendLine("  return contents");
      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="propValue"></param>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      var list = (IList<Int64>)propValue;
      writer.Write(list.Count);
      foreach (var item in list)
      {
        writer.Write(true);
        writer.Write(item);
      }
    }

    /// <summary>
    ///  Validate items in collection
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      var list = (IList)Getter((T)obj);
      if (list == null)
        return;

      foreach (PersistentObject item in list)
      {
        item.Validate(errors);
      }
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
      Setter((T)obj, new List<TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, IList<TValue>> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, IList<TValue>> Setter;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override ICollection CreateCollection()
    {
      return new List<TValue>();
    }

    private static IList<ObjectRef> CreateObjectRefList(IEnumerable<TValue> list)
    {
      return list.Select(ObjectRef.Create).ToList();
    }
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class BagOneToManyPropertyMeta<T, TValue> : OneToManyPropertyMeta
    where T : BaseEntityObject
    where TValue : PersistentObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public BagOneToManyPropertyMeta(ClassMeta entity, OneToManyPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, IList<TValue>>(propInfo);
      Setter = CreatePropertySetter<T, IList<TValue>>(propInfo);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override object GetValue(object obj)
    {
      return Getter((T)obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public override void SetValue(object obj, object value)
    {
      Setter((T)obj, (IList<TValue>)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var list = Getter((T)obj);
      return list == null || list.Count == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var listA = Getter((T)objA);
      var listB = Getter((T)objB);
      return CompareBagCollection(CreateObjectRefList(listA), CreateObjectRefList(listB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var listA = Getter((T)objA);
      var listB = Getter((T)objB);

      return DiffBagCollection(
        CreateObjectRefList(listA), CreateObjectRefList(listB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadBagCollectionDelta<ObjectRef>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      var list = (IList<TValue>)CreateCollection();
      foreach (var objectRef in reader.ReadListCollection<ObjectRef>())
      {
        list.Add((TValue)reader.Adaptor.Get(objectRef.Id, null));
      }
      return list;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      bool newList = false;

      var list = Getter((T)obj);
      if (list == null)
      {
        newList = true;
        list = (IList<TValue>)CreateCollection(); 
      }

      var oldCount = list.Count;

      int newCount = 0;
      foreach (var objectRef in reader.ReadListCollection<ObjectRef>())
      {
        var value = (TValue)reader.Adaptor.Get(objectRef.Id, null);
        if (newCount >= list.Count)
        {
          list.Add(value);
        }
        else
        {
          list[newCount] = value;
        }
        ++newCount;
      }

      // Remove any items after last one read
      for (int j = oldCount - 1; j >= newCount; --j)
      {
        list.RemoveAt(j);
      }

      if (newList)
        Setter((T)obj, list);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      var list = (IList<TValue>)value;

      writer.WriteList(CreateObjectRefList(list));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      var list = Getter((T)obj);

      writer.WriteList(CreateObjectRefList(list));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderFunction()
    {
      var sb = new StringBuilder();
      sb.AppendLine(string.Format("def Read{0}{1}(reader):", Entity.Name, Name));
      sb.AppendLine("  contents = List[Int64]()");
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    if reader.ReadBoolean():");
      sb.AppendLine("      contents.Add(reader.ReadInt64())");
      sb.AppendLine("  return contents");
      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="propValue"></param>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      var list = (IList<Int64>)propValue;
      writer.Write(list.Count);
      foreach (var item in list)
      {
        writer.Write(true);
        writer.Write(item);
      }
    }

    /// <summary>
    ///  Validate items in collection
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      var list = (IList)Getter((T)obj);
      if (list == null)
        return;

      foreach (PersistentObject item in list)
      {
        item.Validate(errors);
      }
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
      Setter((T)obj, new List<TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, IList<TValue>> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, IList<TValue>> Setter;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override ICollection CreateCollection()
    {
      return new List<TValue>();
    }

    private static IList<ObjectRef> CreateObjectRefList(IEnumerable<TValue> list)
    {
      return list.Select(ObjectRef.Create).ToList();
    }

    /// <summary>
    /// Compares the keyed collection.
    /// </summary>
    /// <param name="oldList">The old list.</param>
    /// <param name="newList">The new list.</param>
    /// <returns></returns>
    /// <remarks>The collection is not a true bag collection since we assume no duplicates.</remarks>
    private static bool CompareBagCollection(IEnumerable<TValue> oldList, IEnumerable<TValue> newList)
    {
      var oldMap = oldList.ToDictionary(item => item.ObjectId);
      var newMap = newList.ToDictionary(item => item.ObjectId);

      var newKeys = new HashSet<long>();
      foreach (var key in newMap.Keys)
        newKeys.Add(key);

      foreach (var key in oldMap.Keys)
      {
        if (!newMap.ContainsKey(key))
          return false;

        TValue oldValue = oldMap[key];
        TValue newValue = newMap[key];
        if (!ClassMeta.IsSame(oldValue, newValue))
          return false;

        newKeys.Remove(key);
      }

      return newKeys.Count == 0;
    }

    /// <summary>
    /// Compares the bag collection
    /// </summary>
    /// <param name="oldList">The old list.</param>
    /// <param name="newList">The new list.</param>
    /// <returns></returns>
    /// <remarks>The collection is not a true bag collection since we assume no duplicates.</remarks>
    private static bool CompareBagCollection(IEnumerable<ObjectRef> oldList, IEnumerable<ObjectRef> newList)
    {
      var oldMap = oldList.ToDictionary(item => item.Id);
      var newMap = newList.ToDictionary(item => item.Id);

      var newKeys = new HashSet<long>();
      foreach (var key in newMap.Keys)
        newKeys.Add(key);

      foreach (var key in oldMap.Keys)
      {
        if (!newMap.ContainsKey(key))
          return false;

        var oldValue = oldMap[key];
        var newValue = newMap[key];
        if (!ObjectRef.IsSame(oldValue, newValue))
          return false;

        newKeys.Remove(key);
      }

      return newKeys.Count == 0;
    }

    /// <summary>
    /// Diffs the keyed collection.
    /// </summary>
    private static KeyedCollectionDelta<TValue> DiffBagCollection(IList<TValue> oldList, IList<TValue> newList)
    {
      if (IsNullOrEmpty(oldList) && IsNullOrEmpty(newList))
      {
        return null;
      }

      var mapA = oldList.ToDictionary(item => item.ObjectId);
      var mapB = newList.ToDictionary(item => item.ObjectId);

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
      var unmatchedItems = new Dictionary<long, TValue>(mapB);

      foreach (var iter in mapA)
      {
        var itemKey = iter.Key;
        var oldValue = iter.Value;

        TValue newValue;
        if (mapB.TryGetValue(itemKey, out newValue))
        {
          var objectDelta = (ObjectDelta)ClassMeta.CreateDelta(oldValue, newValue);
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

    /// <summary>
    /// Diffs the keyed collection.
    /// </summary>
    private static BagCollectionDelta<ObjectRef> DiffBagCollection(IList<ObjectRef> oldList, IList<ObjectRef> newList)
    {
      if (IsNullOrEmpty(oldList) && IsNullOrEmpty(newList))
      {
        return null;
      }

      var mapA = oldList.ToDictionary(item => item.Id);
      var mapB = newList.ToDictionary(item => item.Id);

      var itemDiffs = new List<BagCollectionItemDelta<ObjectRef>>();

      if (!mapA.Any())
      {
        itemDiffs.AddRange(mapB.Select(kvp => new BagCollectionItemDelta<ObjectRef>(ItemAction.Added, kvp.Value)));
        return new BagCollectionDelta<ObjectRef>(itemDiffs);
      }

      if (!mapB.Any())
      {
        itemDiffs.AddRange(mapA.Select(kvp => new BagCollectionItemDelta<ObjectRef>(ItemAction.Removed, kvp.Value)));
        return new BagCollectionDelta<ObjectRef>(itemDiffs);
      }

      // Used to detect unmatched items
      var unmatchedItems = new Dictionary<long, ObjectRef>(mapB);

      foreach (var iter in mapA)
      {
        var itemKey = iter.Key;
        var oldValue = iter.Value;

        ObjectRef newValue;
        if (mapB.TryGetValue(itemKey, out newValue))
        {
          unmatchedItems.Remove(itemKey);
        }
        else
        {
          itemDiffs.Add(new BagCollectionItemDelta<ObjectRef>(ItemAction.Removed, oldValue));
        }
      }

      itemDiffs.AddRange(unmatchedItems.Select(entry => new BagCollectionItemDelta<ObjectRef>(ItemAction.Added, entry.Value)));

      return itemDiffs.Count == 0 ? null : new BagCollectionDelta<ObjectRef>(itemDiffs);
    }

    private static bool IsNullOrEmpty(IList<TValue> oldList)
    {
      return oldList == null || oldList.Count == 0;
    }

    private static bool IsNullOrEmpty(IList<ObjectRef> oldList)
    {
      return oldList == null || oldList.Count == 0;
    }
  }
}