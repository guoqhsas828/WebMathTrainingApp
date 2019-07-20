/*
 * PropertyMeta.cs -
 *
 * Copyright (c) WebMathTraining 2002-2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Abstract base class for all property metadata types
  /// </summary>
  public abstract class PropertyMeta
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected PropertyMeta(ClassMeta entity, PropertyAttribute propAttr, PropertyInfo propInfo)
    {
      Entity = entity;
      PropertyInfo = propInfo;
      Name = propAttr.Name ?? propInfo.Name;
      DisplayName = propAttr.DisplayName ?? Name;
      Column = propAttr.Column ?? Name;
      RelatedPropertyName = propAttr.RelatedProperty;
      Persistent = propAttr.Persistent;
      ReadOnly = propAttr.ReadOnly;
      IsPrimaryKey = propAttr.IsPrimaryKey;
      IsKey = propAttr.IsKey;
      AllowNullableKey = propAttr.AllowNullableKey;
      if (IsPrimaryKey)
      {
        IsUnique = true;
        IsNullable = false;        
      }
      else if (IsKey)
      {
        IsUnique = true;
        IsNullable = AllowNullableKey && propAttr.AllowNull;
      }
      else
      {
        IsUnique = propAttr.IsUnique;
        IsNullable = propAttr.AllowNull;
      }

      IsSystemProperty =
        propInfo.DeclaringType == typeof(PersistentObject) ||
        propInfo.DeclaringType == typeof(VersionedObject) ||
        propInfo.DeclaringType == typeof(AuditedObject);

      // Set this attribute from the meta (may be updated during post-processing)
      ExtendedData = propAttr.ExtendedData;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Retrieves a string that indicates the current object
    /// </summary>
    public override string ToString()
    {
      return $"{GetType().Name}:{Entity.Name}.{Name}";
    }

    /// <summary>
    /// Can assume all referenced classes are initialized, but not their properties
    /// </summary>
    /// <param name="classCache"></param>
    public virtual void SecondPassInit(IClassCache classCache)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public abstract void ThirdPassInit();

    /// <summary>
    ///  Get the value of this property for the specified object
    /// </summary>
    public abstract object GetValue(object obj);

    /// <summary>
    ///  Set the value of this property in the specified object
    /// </summary>
    public abstract void SetValue(object obj, object value);

    /// <summary>
    /// Get the value of the field underlying this property
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public virtual object GetFieldValue(object obj)
    {
      return GetValue(obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public virtual void SetFieldValue(object obj, object value)
    {
      SetValue(obj, value);
    }

    /// <summary>
		///  Validate class invariants
		/// </summary>
		public virtual void Validate(object obj, ArrayList errors)
		{}

    /// <summary>
    /// 
    /// </summary>
    public abstract object GetDefaultValue();

    /// <summary>
    /// 
    /// </summary>
    public abstract void SetDefaultValue(BaseEntityObject obj);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public abstract bool HasDefaultValue(BaseEntityObject obj);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public abstract bool IsSame(object objA, object objB);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public abstract ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public abstract ISnapshotDelta CreateDelta(IEntityDeltaReader reader);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public abstract object Read(IEntityReader reader);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public abstract void Read(BaseEntityObject obj, IEntityReader reader);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public abstract void Write(IEntityWriter writer, object value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public abstract void Write(BaseEntityObject obj, IEntityWriter writer);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    protected static Func<T, TValue> CreatePropertyGetter<T, TValue>(PropertyInfo propInfo)
    {
      var getterType = typeof(Func<T, TValue>);
      return (Func<T, TValue>)Delegate.CreateDelegate(
        getterType, null, propInfo.GetGetMethod(true));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    protected static Action<T, TValue> CreatePropertySetter<T, TValue>(PropertyInfo propInfo)
    {
      if (propInfo.CanWrite)
      {
        var setterType = typeof(Action<T, TValue>);
        return (Action<T, TValue>)Delegate.CreateDelegate(
          setterType, null, propInfo.GetSetMethod(true));
      }

      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    protected static Func<T, TValue> CreateFieldGetter<T, TValue>(FieldInfo fieldInfo)
    {
      var obj = Expression.Parameter(typeof(T), "obj");

      Expression<Func<T, TValue>> expr = Expression.Lambda<Func<T, TValue>>(
        Expression.Field(obj, fieldInfo), obj);

      return expr.Compile();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    protected static Action<T, TValue> CreateFieldSetter<T, TValue>(FieldInfo fieldInfo)
    {
      if (fieldInfo.IsInitOnly) return null;

      var obj = Expression.Parameter(typeof(T), "obj");
      var value = Expression.Parameter(typeof(TValue), "value");

      Expression<Action<T, TValue>> expr =
        Expression.Lambda<Action<T, TValue>>(
          Expression.Assign(
            Expression.Field(obj, fieldInfo),
            Expression.Convert(value, fieldInfo.FieldType)),
          obj,
          value);

      return expr.Compile();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    protected static FieldInfo GetField(PropertyInfo property)
    {
      var declaringType = property.DeclaringType;
      if (declaringType == null)
      {
        throw new MetadataException(string.Format("Property [{0}] does not have a DeclaringType", property.Name));
      }
      var list = new List<FieldInfo>();
      var name = declaringType.Name;
      foreach (FieldInfo fieldInfo in declaringType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
      {
        if (fieldInfo.FieldType == typeof(ObjectRef) && IsMatch(fieldInfo.Name, property.Name))
          list.Add(fieldInfo);
      }
      if (list.Count == 0)
      {
        throw new MetadataException($"Cannot identify field for property [{declaringType}.{property.Name}]");
      }
      if (list.Count > 1)
      {
        throw new MetadataException($"Multiple possible FieldInfos found for [{declaringType}.{property.Name}]");
      }
      return list[0];
    }

    private static bool IsMatch(string fieldName, string propertyName)
    {
      return fieldName.Equals(propertyName + "_", StringComparison.OrdinalIgnoreCase) || fieldName.Equals("_" + propertyName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public virtual string GenerateHistoryReaderFunction()
    {
      return string.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract string GenerateHistoryReaderInvocation();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public virtual void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      Write(writer, propValue);
    }

    #endregion

    #region Properties

		/// <summary>
		///  Entity
		/// </summary>
    public ClassMeta Entity { get; set; }

    /// <summary>
		///  Name of property
		/// </summary>
    public string Name { get; }

    /// <summary>
    ///  Display name (defaults to Name)
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    ///  Indicates if property is user editable
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Indicates if property is mapped to database
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    ///  Column name (defaults to Name)
    /// </summary>
    public string Column { get; set; }

    /// <summary>
    ///  Enforce not-null constraint when saving to database
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    ///  Enforce unique constraint when saving to database
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Used to specify single field primary key
    /// </summary>
    /// <remarks>
    /// Implies IsUnique=true and IsNullable=false
    /// </remarks>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Used to specify business key(s)
    /// </summary>
    /// <remarks>
    /// For parent entities, implies IsUnique=true.  For both parent and child entities,
    /// key properties must have IsNullable=false.
    /// </remarks>
    public bool IsKey { get; set; }

    /// <summary>
    /// Indicates if this property is managed by the persistence layer
    /// </summary>
    public bool IsSystemProperty { get; }

    /// <summary>
    /// Name of <see cref="RelatedPropertyName">RelatedProperty</see>".
    /// </summary>
    /// <remarks>
    /// This property is set at configuration time and is used to
    /// resolve the <see cref="RelatedPropertyName">RelatedProperty</see> the
    /// first time it is required.
    /// </remarks>
    public string RelatedPropertyName { get; set; }

    /// <summary>
    /// Used to specify if property maps to column or blob in the case where the
    /// PropertyMappingStrategy for the entity is Hybrid (in the other two cases, 
    /// this setting is ignored as it is determined at the entity level).
    /// </summary>
    public bool ExtendedData { get; set; }

		/// <summary>
		/// Returns the .NET <see cref="System.Reflection.PropertyInfo">PropertyInfo</see> for this property.
		/// </summary>
    public PropertyInfo PropertyInfo { get; }

    /// <summary>
		/// Returns the .NET type of this property
		/// </summary>
    public Type PropertyType => PropertyInfo.PropertyType;

    /// <summary>
    /// Index of this property in the PropertyList of its owner
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Allow nullable key column
    /// </summary>
    public bool AllowNullableKey { get; private set; }

    /// <summary>
    /// True if the property has a setter, else False
    /// </summary>
    public bool CanWrite => PropertyInfo.CanWrite;

    #endregion

    /// <summary>
    /// Formats the specified pm.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns></returns>
    public virtual string Format(object o, string propertyName = "Name")
    {
      var oProp = GetValue(o);
      return oProp?.ToString() ?? "";
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual bool SkipSnapshotWrite { get { return false; } }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerElement"></param>
    /// <param name="prefix"></param>
    /// <param name="mustAllowNull"></param>
    public virtual void MapScalar(XmlElement ownerElement, string prefix, bool mustAllowNull)
    {
      // Custom NHibernate Type
      string typeStr = CustomPropertyType(PropertyType);
      if (typeStr != null)
      {
        ownerElement.SetAttribute("type", typeStr);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="xmlSchema"></param>
    /// <param name="element"></param>
    /// <returns></returns>
    public virtual bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      throw new NotSupportedException($"Scalar PropertyMeta not supported: {GetType().FullName}");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public virtual string BuildKeyString(object obj)
    {
      throw new Exception($"Invalid key property type: {GetType()}");
    }

    private static string CustomPropertyType(Type propertyType)
    {
      if (propertyType == typeof(double?)) return typeof(double?).GetAssemblyQualifiedShortName();
      if (propertyType == typeof(int?)) return typeof(int?).GetAssemblyQualifiedShortName();
      if (propertyType == typeof(long?)) return typeof(long?).GetAssemblyQualifiedShortName();
      return null;
    }
  }
}
