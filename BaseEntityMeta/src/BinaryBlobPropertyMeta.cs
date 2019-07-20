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
  /// 
  /// </summary>
  public abstract class BinaryBlobPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// Construct new instance using custom attribute
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected BinaryBlobPropertyMeta(ClassMeta entity, BinaryBlobPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      if (PropertyType != typeof (byte[]))
      {
        throw new MetadataException($"Invalid property [{Entity.Name}.{Name}] : BinaryBlobProperty requires PropertyType of byte[]");
      }
    }

    #region Methods

    /// <summary>
    /// 
    /// </summary>
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
      ownerElement.SetAttribute("type", "BinaryBlob");
      ownerElement.SetAttribute("length", Int32.MaxValue.ToString());
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
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return "reader.ReadBinaryBlog()";
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class BinaryBlobPropertyMeta<T> : BinaryBlobPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public BinaryBlobPropertyMeta(ClassMeta entity, BinaryBlobPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, byte[]>(propInfo);
      Setter = CreatePropertySetter<T, byte[]>(propInfo);
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
      Setter((T) obj, (byte[]) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      byte[] blobA = Getter((T) objA);
      byte[] blobB = Getter((T) objB);

      if (blobA == null)
      {
        return (blobB == null);
      }
      if (blobB == null)
      {
        return false;
      }

      if (blobA.Length != blobB.Length)
      {
        return false;
      }

      for (int i = 0; i < blobA.Length; i++)
      {
        if (blobA[i] != blobB[i])
          return false;
      }

      return true;
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
      var contents = Getter((T) obj);
      return contents == null || contents.Length == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      if (IsSame(objA, objB))
      {
        return null;
      }

      var valueA = Getter((T) objA);
      var valueB = Getter((T) objB);
      return new ScalarDelta<byte[]>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<byte[]>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadBinaryBlob();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadBinaryBlob());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((byte[]) value);
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
    ///  Validate
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      if (!IsNullable)
      {
        if (Getter((T) obj) == null)
        {
          InvalidValue.AddError(errors, obj, Name,
            $"{Entity.Name}.{Name} - Value cannot be empty!");
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, byte[]> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, byte[]> Setter;
  }
}