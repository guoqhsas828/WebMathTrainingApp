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
  /// Describes a variable-length string property
  /// </summary>
  /// <remarks></remarks>
  public abstract class StringPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// Construct new instance using custom attribute
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop meta attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected StringPropertyMeta(ClassMeta entity, StringPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      if (propAttr.MaxLength <= 0)
      {
        throw new MetadataException($"MaxLength for property [{Name}] in entity [{Entity.Name}] must be > 0");
      }

      MaxLength = propAttr.MaxLength;

      MinLength = propAttr.IsKey ? 1 : (int?) null;
    }

    #region Methods

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
      ownerElement.SetAttribute("type", $"String({MaxLength})");
    }

    /// <summary>
    /// Maps the XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <returns></returns>
    public override bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      XsdGenerator.SetXmlSchemaSimpleType(xmlSchema, element, typeof(string), MaxLength, IsKey);
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

    #endregion

    #region Properties

    /// <summary>
    /// Maximum length of string
    /// </summary>
    /// <remarks></remarks>
    public int MaxLength { get; private set; }

    /// <summary>
    /// Minimum length of string. Set for IsKey properties.
    /// </summary>
    /// <remarks></remarks>
    public int? MinLength { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return "reader.ReadString()";
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class StringPropertyMeta<T> : StringPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public StringPropertyMeta(ClassMeta entity, StringPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, string>(propInfo);
      Setter = CreatePropertySetter<T, string>(propInfo);
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
      Setter((T) obj, (string) value);
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
      return valueA == valueB ? null : new ScalarDelta<string>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<string>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadString());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((string) value);
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
      return Getter((T) obj) == null;
    }

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <param name="errors">The errors.</param>
    /// <remarks></remarks>
    public override void Validate(object obj, ArrayList errors)
    {
      var propValue = Getter((T) obj);

      if (!IsNullable)
      {
        if (string.IsNullOrWhiteSpace(propValue))
        {
          InvalidValue.AddError(errors, obj, Name, $"{Entity.Name}.{Name} - Value cannot be empty!");
        }
      }

      if (propValue != null)
      {
        if (propValue.Length > MaxLength)
        {
          InvalidValue.AddError(errors, obj, Name,
            $"{Entity.Name}.{Name} String [{propValue}] : Length [{propValue.Length}] > MaxLength [{MaxLength}]");
        }
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
      Setter((T) obj, null);
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, string> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, string> Setter;
  }
}