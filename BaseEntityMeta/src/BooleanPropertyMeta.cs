// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Describes a Boolean property
  /// </summary>
  /// <remarks></remarks>
  public abstract class BooleanPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanPropertyMeta"/> class.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop meta attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected BooleanPropertyMeta(ClassMeta entity, BooleanPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      // For bool types, we ignore the AllowNull attribute and determine
      // whether the property allows nulls based on whether the property
      // type is nullable or not.
      if (PropertyType == typeof (bool))
      {
        IsNullable = false;
      }
      else if (PropertyType == typeof (bool?))
      {
        IsNullable = true;
      }
      else
      {
        throw new MetadataException($"Invalid PropertyType [{PropertyType}] for [{entity.Name}.{Name}]");
      }
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
    /// <param name="obj"></param>
    /// <returns></returns>
    public override string BuildKeyString(object obj)
    {
      return obj.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return "reader.ReadBoolean()";
    }

    /// <summary>
    /// Maps the XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <returns></returns>
    public override bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      element.SchemaTypeName = new XmlQualifiedName("boolean", XsdGenerator.XmlSchemaNs);
      return true;
    }
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class BooleanPropertyMeta<T> : BooleanPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public BooleanPropertyMeta(ClassMeta entity, BooleanPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, bool>(propInfo);
      Setter = CreatePropertySetter<T, bool>(propInfo);
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
      Setter((T) obj, (bool) value);
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      return Getter((T) obj) == false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      return Getter((T) objA) == Getter((T) objB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var valueA = Getter((T) objA);
      var valueB = Getter((T) objB);
      return valueA == valueB ? null : new ScalarDelta<bool>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<bool>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadBoolean();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadBoolean());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((bool) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.Write(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, bool> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, bool> Setter;
  }
}