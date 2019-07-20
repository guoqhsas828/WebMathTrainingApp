// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///  Describes a numeric property
  /// </summary>
  /// <remarks>
  ///  Currently the following numeric types are supported: Double, Int32, Int64
  /// </remarks>
  public abstract class VersionPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected VersionPropertyMeta(ClassMeta entity, VersionPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      if (propInfo.PropertyType != typeof(Int32))
      {
        throw new MetadataException("Invalid PropertyType: version property must be of type Int32");
      }

      IsNullable = false;
      ReadOnly = true;
    }

    /// <summary>
    /// 
    /// </summary>
    public override void ThirdPassInit()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return "reader.ReadInt32()";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="xmlSchema"></param>
    /// <param name="element"></param>
    /// <returns></returns>
    public override bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      element.SchemaTypeName = new XmlQualifiedName("int", XsdGenerator.XmlSchemaNs);
      element.IsNillable = false;
      return true;
    }
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class VersionPropertyMeta<T> : VersionPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public VersionPropertyMeta(ClassMeta entity, VersionPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, int>(propInfo);
      Setter = CreatePropertySetter<T, int>(propInfo);
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
      Setter((T)obj, (int)value);
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T)obj, 0);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      return Getter((T)obj) == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      return Getter((T)objA) == Getter((T)objB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      int valueA = Getter((T)objA);
      int valueB = Getter((T)objB);
      return valueA == valueB ? null : new ScalarDelta<int>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<int>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadInt32();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T)obj, reader.ReadInt32());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((int)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.Write(Getter((T)obj));
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, int> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, int> Setter;
  }
}