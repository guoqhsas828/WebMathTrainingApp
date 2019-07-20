// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Reflection;
using System.Xml.Schema;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <remarks></remarks>
  public abstract class EnumPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="EnumPropertyMeta"/> class.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected EnumPropertyMeta(ClassMeta entity, EnumPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      NullableType = PropertyType.IsGenericType && PropertyType.GetGenericTypeDefinition() == typeof (Nullable<>);

      EnumType = NullableType ? Nullable.GetUnderlyingType(PropertyType) : PropertyType;

      if (!EnumType.IsEnum)
      {
        throw new InvalidPropertyTypeException(entity, propInfo);
      }
    }

    /// <summary>
    /// Maps the XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <returns></returns>
    public override bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      var nullable = IsNullable;
      var enumType = PropertyType;
      if (enumType.IsGenericType && enumType.GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        enumType = enumType.GetGenericArguments()[0];
        nullable = true;
      }
      element.MinOccurs = XsdGenerator.IsSparseSchemaMode(xmlSchema) || nullable ? 0 : 1;
      element.MaxOccurs = 1;

      XsdGenerator.SetEnumXmlSchemaType(xmlSchema, element, enumType);
      return true;
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
    /// The type of Enum (may be different than PropertyType if property is Nullable)
    /// </summary>
    public Type EnumType { get; private set; }

    private bool NullableType { get; set; }
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class EnumPropertyMeta<T, TValue> : EnumPropertyMeta where T : BaseEntityObject where TValue : struct
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public EnumPropertyMeta(ClassMeta entity, EnumPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, TValue>(propInfo);
      Setter = CreatePropertySetter<T, TValue>(propInfo);
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
      Setter((T) obj, (TValue) value);
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return default(TValue);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, default(TValue));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      return default(TValue).Equals(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      return ValueComparer<TValue>.IsSame(Getter((T) objA), Getter((T) objB));
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
      return ValueComparer<TValue>.IsSame(valueA, valueB)
        ? null
        : new ScalarDelta<TValue>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadEnum<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadEnum<TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.WriteEnum((TValue) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.WriteEnum(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return String.Format("reader.ReadEnum[{0}]()", typeof (TValue).Name);
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, TValue> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, TValue> Setter;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class NullableEnumPropertyMeta<T, TValue> : EnumPropertyMeta
    where T : BaseEntityObject
    where TValue : struct
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public NullableEnumPropertyMeta(ClassMeta entity, EnumPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, TValue?>(propInfo);
      Setter = CreatePropertySetter<T, TValue?>(propInfo);
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
      Setter((T) obj, (TValue?) value);
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
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      return Getter((T) obj) == null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var valueA = Getter((T) objA);
      var valueB = Getter((T) objB);

      if (valueA == null)
      {
        return valueB == null;
      }
      if (valueB == null)
      {
        return false;
      }

      return valueA.Equals(valueB);
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
      return ValueComparer<TValue?>.IsSame(valueA, valueB)
        ? null
        : new ScalarDelta<TValue?>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<TValue?>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadNullableEnum<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, (TValue?) reader.ReadNullableEnum<TValue>());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.WriteNullableEnum((TValue?) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.WriteNullableEnum(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return string.Format("reader.ReadNullableEnum[{0}]()", typeof (TValue).Name);
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, TValue?> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, TValue?> Setter;
  }
}