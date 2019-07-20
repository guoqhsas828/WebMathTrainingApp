// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Iesi.Collections;
using log4net;
using BaseEntity.Shared;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
using HashedSet = System.Collections.Generic.HashSet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// An element collection is similar to a component collection, but the items in the collection
  /// are stored in a single column in the database.  Typically this is used for collections of
  /// strings, enums, or other basic types.
  /// </summary>
  /// <remarks></remarks>
  public abstract class ElementCollectionPropertyMeta : CollectionPropertyMeta
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof (ElementCollectionPropertyMeta));

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementCollectionPropertyMeta"/> class.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop meta attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected ElementCollectionPropertyMeta(ClassMeta entity, ElementCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      _elementColumn = propAttr.ElementColumn;
      _elementMaxLength = propAttr.ElementMaxLength;
      _elementType = GetElementType(entity, propAttr, propInfo);
      _tableName = propAttr.TableName;

      Logger.DebugFormat("[{0}.{1}] : CollectionType={2}", Entity.Name, Name, CollectionType);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the name of the table.
    /// </summary>
    /// <remarks>Note that this table will not have its own generated id, as it gets its identify from the parent.</remarks>
    public string TableName
    {
      get { return _tableName; }
    }

    /// <summary>
    /// Gets the type of the element.
    /// </summary>
    /// <remarks></remarks>
    public Type ElementType
    {
      get { return _elementType; }
    }

    /// <summary>
    /// Gets the length of the element max.
    /// </summary>
    /// <remarks></remarks>
    public int ElementMaxLength
    {
      get { return _elementMaxLength; }
    }

    /// <summary>
    /// Gets the element column.
    /// </summary>
    /// <remarks></remarks>
    public string ElementColumn
    {
      get { return _elementColumn; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    /// <returns></returns>
    public static Type GetElementType(ClassMeta entity, ElementCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
    {
      var propType = propInfo.PropertyType;
      if (propType.IsGenericType)
      {
        // Derive ElementType from PropertyType
        Type[] typeArgs = propType.GetGenericArguments();
        var collectionType = GetCollectionType(propAttr, propInfo);
        Type elementType = collectionType == "map" ? typeArgs[1] : typeArgs[0];
        if ((propAttr.ElementType != null) && (elementType != propAttr.ElementType))
        {
          throw new MetadataException(String.Format(
            "ElementType [{0}] does not match generic type argument [{1}] for property [{2}] in entity [{3}]",
            propAttr.ElementType, elementType, propInfo.Name, entity.Name));
        }

        return elementType;
      }

      if (propType != typeof (ISet<>))
      {
        throw new MetadataException(String.Format(
          "Invalid property type [{0}] for {1}.{2} : non-generic component collections must be of type ISet!",
          propType, entity.Name, propInfo.Name));
      }

      if (propAttr.ElementType != null)
      {
        return propAttr.ElementType;
      }

      throw new MetadataException(String.Format(
        "Must specify ElementType for property [{0}] in entity [{1}]", propInfo.Name, entity.Name));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return string.Format("Read{0}{1}(reader)", Entity.Name, Name);
    }

    #endregion

    #region Data

    private readonly string _tableName;
    private readonly string _elementColumn;
    private readonly Type _elementType;
    private readonly int _elementMaxLength;

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class SetElementCollectionPropertyMeta<T, TValue> : ElementCollectionPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public SetElementCollectionPropertyMeta(ClassMeta entity, ElementCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, ISet>(propInfo);
      Setter = CreatePropertySetter<T, ISet>(propInfo);
    }

    /// <summary>
    /// Thirds the pass init.
    /// </summary>
    /// <remarks></remarks>
    public override void ThirdPassInit()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override ICollection CreateCollection()
    {
      return (ICollection) new HashedSet();
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
      Setter((T) obj, (ISet) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var set = Getter((T) obj);
      return set == null || set.Count == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var setA = Getter((T) objA);
      var setB = Getter((T) objB);
      return SetCollectionHelper<TValue>.IsSame(setA, setB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var setA = Getter((T) objA);
      var setB = Getter((T) objB);
      return SetCollectionHelper<TValue>.CreateDelta(setA, setB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return SetCollectionHelper<TValue>.CreateDelta(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadSetCollection<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadSetCollection<TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderFunction()
    {
      var sb = new StringBuilder();
      sb.AppendLine("def Read" + Entity.Name + Name + "(reader):");
      sb.AppendLine("  contents = HashSet[" + typeof (TValue).Name + "]()");
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    contents.Add(reader.ReadValue[" + typeof (TValue).Name + "]())");
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
      writer.WriteSet<TValue>((ISet) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.WriteSet<TValue>(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// Once we change from ISet to ISet{T} we can simply delegate to the Write method
    /// </remarks>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      var set = (ISet<TValue>) propValue;
      writer.Write(set.Count);
      foreach (var item in set)
      {
        writer.WriteValue(item);
      }
    }

    /// <summary>
    ///   Validate
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public override void Validate(object obj, ArrayList errors)
    {
      if (typeof (TValue) == typeof (DateTime))
      {
        var set = Getter((T) obj);
        foreach (DateTime dt in set)
        {
          string errorMsg;
          if (!dt.TryValidate(out errorMsg))
          {
            InvalidValue.AddError(errors, obj, Name, String.Format("{0}.{1} - {2}", Entity.Name, Name, errorMsg));
            break;
          }
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return new HashedSet();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, new HashedSet());
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, ISet> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, ISet> Setter;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  /// <typeparam name="TKey"></typeparam>
  public class MapElementCollectionPropertyMeta<T, TKey, TValue> : ElementCollectionPropertyMeta
    where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public MapElementCollectionPropertyMeta(ClassMeta entity, ElementCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, IDictionary<TKey, TValue>>(propInfo);
      Setter = CreatePropertySetter<T, IDictionary<TKey, TValue>>(propInfo);
    }

    /// <summary>
    /// Thirds the pass init.
    /// </summary>
    /// <remarks></remarks>
    public override void ThirdPassInit()
    {
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
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var map = Getter((T) obj);
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
      sb.AppendLine("def Read" + Entity.Name + Name + "(reader):");
      sb.AppendLine("  contents = Dictionary[" + typeof (TKey).Name + "," + typeof (TValue).Name + "]()");
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    key = reader.ReadValue[" + typeof (TKey).Name + "]()");
      sb.AppendLine("    if reader.ReadBoolean():");
      sb.AppendLine("      value = reader.ReadValue[" + typeof (TValue).Name + "]()");
      sb.AppendLine("    else:");
      sb.AppendLine("      value = " + typeof (TValue).Name + "()");
      sb.AppendLine("    contents[key] = value");
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
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      Write(writer, propValue);
    }

    /// <summary>
    ///   Validate
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public override void Validate(object obj, ArrayList errors)
    {
      var map = Getter((T) obj);

      if (typeof (TKey) == typeof (DateTime))
      {
        foreach (var dt in map.Keys.Select(item => Convert.ToDateTime(item)))
        {
          string errorMsg;
          if (!dt.TryValidate(out errorMsg))
          {
            InvalidValue.AddError(errors, obj, Name, String.Format("{0}.{1} - {2}", Entity.Name, Name, errorMsg));
            break;
          }
        }
      }

      if (typeof (TValue) == typeof (DateTime))
      {
        foreach (var dt in map.Values.Select(item => Convert.ToDateTime(item)))
        {
          string errorMsg;
          if (!dt.TryValidate(out errorMsg))
          {
            InvalidValue.AddError(errors, obj, Name, String.Format("{0}.{1} - {2}", Entity.Name, Name, errorMsg));
            break;
          }
        }
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
  public class ListElementCollectionPropertyMeta<T, TValue> : ElementCollectionPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public ListElementCollectionPropertyMeta(ClassMeta entity, ElementCollectionPropertyAttribute propAttr,
      PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, IList<TValue>>(propInfo);
      Setter = CreatePropertySetter<T, IList<TValue>>(propInfo);
    }

    /// <summary>
    /// Thirds the pass init.
    /// </summary>
    /// <remarks></remarks>
    public override void ThirdPassInit()
    {
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

      return CollectionType == "bag"
        ? BagCollectionHelper<TValue>.IsSame(listA, listB)
        : ListCollectionHelper<TValue>.IsSame(listA, listB);
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

      return CollectionType == "bag"
        ? BagCollectionHelper<TValue>.CreateDelta(listA, listB)
        : ListCollectionHelper<TValue>.CreateDelta(listA, listB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
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
      sb.AppendLine("def Read" + Entity.Name + Name + "(reader):");
      sb.AppendLine("  contents = List[" + typeof (TValue).Name + "]()");
      sb.AppendLine("  numItems = reader.ReadInt32()");
      sb.AppendLine("  for i in range(0, numItems):");
      sb.AppendLine("    if reader.ReadBoolean():");
      sb.AppendLine("      contents.Add(reader.ReadValue[" + typeof (TValue).Name + "]())");
      sb.AppendLine("    else:");
      sb.AppendLine("      contents.Add(" + typeof (TValue).Name + "())");
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
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      Write(writer, propValue);
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
    ///   Validate
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public override void Validate(object obj, ArrayList errors)
    {
      if (typeof (TValue) == typeof (DateTime))
      {
        var list = Getter((T) obj);
        foreach (var dt in list.Select(item => Convert.ToDateTime(item)))
        {
          string errorMsg;
          if (!dt.TryValidate(out errorMsg))
          {
            InvalidValue.AddError(errors, obj, Name, String.Format("{0}.{1} - {2}", Entity.Name, Name, errorMsg));
            break;
          }
        }
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