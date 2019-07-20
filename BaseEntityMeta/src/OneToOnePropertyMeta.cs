// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Describes a standard foreign-key reference
  /// </summary>
  public abstract class OneToOnePropertyMeta : ScalarPropertyMeta, ICascade
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected OneToOnePropertyMeta(ClassMeta entity, OneToOnePropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Clazz = propInfo.PropertyType;
      Fetch = propAttr.Fetch ?? "select";

      _cascade = propAttr.Cascade ?? "all-delete-orphan";

      if (!_cascade.Contains("all"))
      {
        throw new MetadataException($"Invalid Cascade [{_cascade}] for property [{Entity.Name}.{Name}]");
      }
      if (Entity.IsComponent)
      {
        throw new MetadataException(
          $"Invalid property [{Entity.Name}.{Name}] : Cannot define OneToOneProperty on Component");
      }

      Column = propAttr.Column ?? propInfo.Name + "Id";
    }

    #region Properties

    /// <summary>
    /// Type of the referenced entity
    /// </summary>
    public Type Clazz { get; }

    /// <summary>
    ///
    /// </summary>
    public string Fetch { get; }

    #endregion

    #region ICascade Members

    /// <summary>
    /// Returns the <see cref="ClassMeta">ClassMeta</see> for the referenced entity.
    /// </summary>
    public ClassMeta ReferencedEntity => _referencedEntity;

    /// <summary>
    /// 
    /// </summary>
    public ICascade InverseCascade => null;

    /// <summary>
    /// Strategy used for propagating database operations (insert/update/delete) from parent to child
    /// </summary>
    public string Cascade => _cascade;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public IEnumerable<PersistentObject> ReferencedObjects(object obj)
    {
      var po = (PersistentObject) GetValue(obj);
      return po == null ? new PersistentObject[0] : new[] {po};
    }

    /// <summary>
    /// 
    /// </summary>
    public string JoinColumn => Column;

    /// <summary>
    /// 
    /// </summary>
    public Cardinality Cardinality => Cardinality.OneToOne;

    /// <summary>
    /// 
    /// </summary>
    public bool IsInverse => false;

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="classCache"></param>
    public override void SecondPassInit(IClassCache classCache)
    {
      _referencedEntity = classCache.Find(Clazz);
      if (_referencedEntity == null)
      {
        throw new MetadataException($"Invalid ReferencedEntity [{Clazz.Name}] for property [{Entity.Name}.{Name}]");
      }

      _isChild = _referencedEntity.IsChildEntity && _cascade.Contains("all");

#if MJT
      if (_referencedEntity.IsRootEntity)
      {
        throw new MetadataException(String.Format(
          "Invalid property [{0}.{1}] : Cannot define OneToOneProperty on RootEntity",
          Entity.Name, Name));
      }
#endif
    }

    /// <summary>
    /// 
    /// </summary>
    public override void ThirdPassInit()
    {
      if (_isChild)
      {
        if (Entity.OldStyleValidFrom)
        {
          if (!_referencedEntity.OldStyleValidFrom)
          {
            throw new MetadataException(
              $"Entity [{Entity.Name}] has OldStyleValidFrom=true but ReferencedEntity [{ReferencedEntity.Name}] has OldStyleValidFrom=false");
          }
        }
        else
        {
          if (_referencedEntity.OldStyleValidFrom)
          {
            throw new MetadataException(
              $"Entity [{Entity.Name}] has OldStyleValidFrom=false but ReferencedEntity [{ReferencedEntity.Name}] has OldStyleValidFrom=true");
          }
        }
      }
    }

    ///// <summary>
    ///// Return true if the objRef is either null or has IsNull=true
    ///// </summary>
    ///// <param name="propValue"></param>
    ///// <returns></returns>
    //public override bool CheckDefaultValue(object propValue)
    //{
    //  var objRef = (ObjectRef)propValue;
    //  return (objRef == null || objRef.IsNull);
    //}

    /// <summary>
    ///  Validate
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      var refObj = (BaseEntityObject) GetValue(obj);

      if (refObj == null)
      {
        if (!IsNullable)
          InvalidValue.AddError(errors, obj, Name, "Value cannot be empty!");
      }
      else
      {
        if (Cascade.Contains("all-delete-orphan"))
          refObj.Validate(errors);
      }
    }

    /// <summary>
    /// Formats the specified pm.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns></returns>
    public override string Format(object o, string propertyName = "Name")
    {
      var entity = ClassCache.Find(PropertyType);
      var nameProp = entity.GetProperty(propertyName);

      var choiceItem = GetValue(o);

      return (choiceItem == null || nameProp == null) ? "" : (String) nameProp.GetValue(choiceItem);
    }

    /// <exclude />
    /// <summary>
    /// Return the type name for the entity that maps to this type
    /// </summary>
    /// <remarks>
    /// Logic must match HbmGenerator.EntityTypeName(Type type)
    /// </remarks>
    /// <param name="type"></param>
    /// <returns></returns>
    private static string HbmEntityTypeName(Type type)
    {
      // Lookup the entity for this type.  In the case of customer
      // data extendibility, both the WebMathTraining (base) type and the
      // customer (derived) type will map to the same entity.
      var entity = ClassCache.Find(type);

      // Return the type associated with this entity.  In the
      // case of customer data extendibility, this would be the
      // type of the customer (derived) class.
      return entity.Type.GetAssemblyQualifiedShortName();
    }

    /// <summary>
    /// Form column name from unqualified part and (optional) prefix
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="column"></param>
    /// <remarks>
    /// Logic must match HbmGenerator.ColumnName(string prefix, string column)
    /// </remarks>
    private static string HbmColumnName(string prefix, string column)
    {
      return prefix == null ? column : prefix + column;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// <param name="ownerElement">The owner element.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="mustAllowNull">if set to <c>true</c> [must allow null].</param>
    public override void MapScalar(XmlElement ownerElement, string prefix, bool mustAllowNull)
    {
      ownerElement.SetAttribute("name", Name);
      ownerElement.SetAttribute("column", HbmColumnName(prefix, Column));
      if (!IsNullable && !mustAllowNull)
      {
        ownerElement.SetAttribute("not-null", "true");
      }
      ownerElement.SetAttribute("access", "BaseEntity.Database.ObjectRefAccessor, BaseEntity.Database");
      ownerElement.SetAttribute("class", HbmEntityTypeName(Clazz));
      ownerElement.SetAttribute("cascade", Cascade);
      ownerElement.SetAttribute("fetch", Fetch);
    }
    /// <summary>
    /// Maps the XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <returns></returns>
    public override bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      var refClassMeta = ClassCache.Find(Clazz);
      bool isOwned = !Cascade.Equals("none");
      if (isOwned)
      {
        XsdGenerator.SetReferencedEntityXmlSchemaType(xmlSchema, element, refClassMeta, null, null);

        XsdGenerator.MapClass(xmlSchema, refClassMeta);
        return true;
      }

      return XsdGenerator.SetReferencedEntityKeyXmlSchemaType(xmlSchema, element, refClassMeta, null, null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return "reader.ReadObjectId()";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      writer.WriteObjectId((long) propValue);
    }

    #endregion

    #region Data

    private readonly string _cascade;
    private ClassMeta _referencedEntity;
    private bool _isChild;

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class OneToOnePropertyMeta<T, TValue> : OneToOnePropertyMeta
    where T : BaseEntityObject
    where TValue : PersistentObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public OneToOnePropertyMeta(ClassMeta entity, OneToOnePropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      var fieldInfo = GetField(propInfo);
      _propertyGetter = CreatePropertyGetter<T, TValue>(propInfo);
      _propertySetter = CreatePropertySetter<T, TValue>(propInfo);
      _fieldGetter = CreateFieldGetter<T, ObjectRef>(fieldInfo);
      _fieldSetter = CreateFieldSetter<T, ObjectRef>(fieldInfo);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override object GetValue(object obj)
    {
      return _propertyGetter((T) obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public override void SetValue(object obj, object value)
    {
      _propertySetter((T) obj, (TValue) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override object GetFieldValue(object obj)
    {
      return _fieldGetter((T) obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public override void SetFieldValue(object obj, object value)
    {
      if (value == null)
      {
        _fieldSetter((T) obj, null);
      }
      else
      {
        var objectRef = value as ObjectRef;
        _fieldSetter((T) obj, objectRef ?? ObjectRef.Create((PersistentObject) value));
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var objectRef = _fieldGetter((T) obj);
      return objectRef == null || objectRef.IsNull;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var objectRefA = _fieldGetter((T) objA);
      var objectRefB = _fieldGetter((T) objB);

      if (objectRefA == null)
      {
        return objectRefB == null;
      }
      if (objectRefB == null)
      {
        return false;
      }

      return objectRefA.Id == objectRefB.Id;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var objectRefA = _fieldGetter((T) objA);
      var objectRefB = _fieldGetter((T) objB);

      if (objectRefA == null || objectRefA.IsNull)
      {
        return objectRefB == null || objectRefB.IsNull ? null : new ScalarDelta<ObjectRef>(null, objectRefB);
      }

      if (objectRefB == null || objectRefB.IsNull)
      {
        return new ScalarDelta<ObjectRef>(objectRefA, null);
      }

      return objectRefA.Id == objectRefB.Id ? null : new ScalarDelta<ObjectRef>(objectRefA, objectRefB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<ObjectRef>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadObjectRef();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      _fieldSetter((T) obj, reader.ReadObjectRef());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((ObjectRef) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.Write(_fieldGetter((T) obj));
    }

    /// <summary>
    ///  Validate
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      var objectRef = _fieldGetter((T) obj);
      if (objectRef == null || objectRef.IsNull)
      {
        if (!IsNullable)
          InvalidValue.AddError(errors, obj, Name, "Value cannot be empty!");
      }
      else if (Cascade != "none")
      {
        var po = ObjectRef.Resolve(objectRef);
        po.Validate(errors);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      _fieldSetter((T) obj, null);
    }

    /// <summary>
    /// 
    /// </summary>
    private readonly Func<T, TValue> _propertyGetter;

    /// <summary>
    /// 
    /// </summary>
    private readonly Action<T, TValue> _propertySetter;

    /// <summary>
    /// 
    /// </summary>
    private readonly Func<T, ObjectRef> _fieldGetter;

    /// <summary>
    /// 
    /// </summary>
    private readonly Action<T, ObjectRef> _fieldSetter;
  }
}