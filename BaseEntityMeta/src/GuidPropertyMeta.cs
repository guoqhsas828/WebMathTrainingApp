// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Describes a DateTime property
  /// </summary>
  /// <remarks></remarks>
  public abstract class GuidPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="GuidPropertyMeta"/> class.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop meta attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected GuidPropertyMeta(ClassMeta entity, GuidPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      if (PropertyType != typeof (Guid))
      {
        throw new InvalidPropertyTypeException(entity, propInfo);
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
    /// Maps the scalar.
    /// </summary>
    /// <param name="ownerElement">The owner element.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="mustAllowNull">if set to <c>true</c> [must allow null].</param>
    public override void MapScalar(XmlElement ownerElement, string prefix, bool mustAllowNull)
    {
      // MJT: Need different way of generating HBM for properties
      ownerElement.SetAttribute("type", "NHibernate.Type.GuidType, NHibernate");
    }

    /// <summary>
    /// Maps the XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <returns></returns>
    public override bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      element.SchemaTypeName = new XmlQualifiedName("string", XsdGenerator.XmlSchemaNs);
      return true;
    }

    /// <summary>
    /// Builds the key string.
    /// </summary>
    /// <param name="obj">The object.</param>
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
      return "reader.ReadGuid()";
    }
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class GuidPropertyMeta<T> : GuidPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public GuidPropertyMeta(ClassMeta entity, GuidPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, Guid>(propInfo);
      Setter = CreatePropertySetter<T, Guid>(propInfo);
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
      Setter((T) obj, (Guid) value);
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
      return valueA == valueB ? null : new ScalarDelta<Guid>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<Guid>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadGuid();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadGuid());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((Guid) value);
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
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      return Getter((T) obj) == Guid.Empty;
    }

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public override void Validate(object obj, ArrayList errors)
    {
      var propValue = Getter((T) obj);

      if (!IsNullable)
      {
        if (propValue == Guid.Empty)
          InvalidValue.AddError(errors, obj, Name, String.Format("{0}.{1} - Value cannot be null!", Entity.Name, Name));
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return Guid.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, Guid.Empty);
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, Guid> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, Guid> Setter;
  }
}