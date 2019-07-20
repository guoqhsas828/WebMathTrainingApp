/*
 * BinaryBlobPropertyMeta.cs -
 *
 * Copyright (c) WebMathTraining 2009. All rights reserved.
 *
 */

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
  public abstract class ArrayOfDoublesPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// Construct new instance using custom attribute
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected ArrayOfDoublesPropertyMeta(ClassMeta entity, ArrayOfDoublesPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      if (PropertyType != typeof(double[]) && PropertyType != typeof(double[,]))
      {
        throw new MetadataException($"Invalid property [{Entity.Name}.{Name}] : ArrayOfDoublesProperty requires PropertyType of double[] or double[,]");
      }

      if (propAttr.MaxLength <= 0)
      {
        throw new MetadataException($"MaxLength for property [{Name}] in entity [{Entity.Name}] must be > 0");
      }

      ColumnNames = propAttr.ColumnNames;
      MaxLength = propAttr.MaxLength;
    }

    /// <summary>
    /// Specific Column Names for the Array of Doubles, this overrides the automatically generated names within the generic form
    /// </summary>
    public string[] ColumnNames { get; set; }

    /// <summary>
    /// max length of this array of doubles
    /// </summary>
    public int MaxLength { get; set; }

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
      ownerElement.SetAttribute("length", (MaxLength * sizeof(double)).ToString());
      // MJT: Need better way of generating HBM for properties
      if (PropertyType == typeof(double[]))
      {
        ownerElement.SetAttribute("access", "BaseEntity.Database.Engine.ArrayOfDoublesPropertyAccessor, BaseEntity.Database");
      }
      else if (PropertyType == typeof(double[,]))
      {
        ownerElement.SetAttribute("access", "BaseEntity.Database.Engine.ArrayOf2DDoublesPropertyAccessor, BaseEntity.Database");
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
      element.SchemaTypeName = new XmlQualifiedName("string", XsdGenerator.XmlSchemaNs);
      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      if (PropertyType == typeof(double[]))
      {
        return "reader.ReadArrayOfDoubles()";
      }
      if (PropertyType == typeof(double[,]))
      {
        return "reader.ReadArrayOfDoubles2D()";
      }
      throw new InvalidOperationException("Invalid PropertyType [" + PropertyType.Name + "]");
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ArrayOfDoubles2DPropertyMeta<T> : ArrayOfDoublesPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public ArrayOfDoubles2DPropertyMeta(ClassMeta entity, ArrayOfDoublesPropertyAttribute propAttr, PropertyInfo propInfo) : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, double[,]>(propInfo);
      Setter = CreatePropertySetter<T, double[,]>(propInfo);
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
      Setter((T)obj, (double[,])value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
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
      Setter((T)obj, null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var contents = Getter((T)obj);
      return contents == null || contents.GetLength(0) == 0 || contents.GetLength(1) == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var arrayA = Getter((T)objA);
      var arrayB = Getter((T)objB);

      if (arrayA == null)
      {
        return (arrayB == null);
      }
      if (arrayB == null)
      {
        return false;
      }
      if (arrayA.GetLength(0) != arrayB.GetLength(0) || arrayA.GetLength(1) != arrayB.GetLength(1))
      {
        return false;
      }
      for (int i = 0; i < arrayA.GetLength(0); i++)
      {
        for (int j = 0; j < arrayA.GetLength(1); j++)
        {
          if (Math.Abs(arrayA[i, j] - arrayB[i, j]) > double.Epsilon) return false;
        }
      }
      return true;
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
      var valueA = Getter((T)objA);
      var valueB = Getter((T)objB);
      return new ScalarDelta<double[,]>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<double[,]>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadArrayOfDoubles2D();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T)obj, reader.ReadArrayOfDoubles2D());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((double[,])value);
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
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public override void Validate(object obj, ArrayList errors)
    {
      if (!IsNullable)
      {
        if (Getter((T)obj) == null)
          InvalidValue.AddError(errors, obj, Name, $"{Entity.Name}, {Name} - Value cannot be empty!");
      }

    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, double[,]> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, double[,]> Setter;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ArrayOfDoubles1DPropertyMeta<T> : ArrayOfDoublesPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public ArrayOfDoubles1DPropertyMeta(ClassMeta entity, ArrayOfDoublesPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, double[]>(propInfo);
      Setter = CreatePropertySetter<T, double[]>(propInfo);
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
      Setter((T)obj, (double[])value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      double[] arrayA = Getter((T)objA);
      double[] arrayB = Getter((T)objB);

      if (arrayA == null)
      {
        return (arrayB == null);
      }
      if (arrayB == null)
      {
        return false;
      }

      if (arrayA.Length != arrayB.Length)
      {
        return false;
      }

      for (int i = 0; i < arrayA.Length; i++)
      {
        if (Math.Abs(arrayA[i] - arrayB[i]) > double.Epsilon)
          return false;
      }

      return true;
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

      var valueA = Getter((T)objA);
      var valueB = Getter((T)objB);
      return new ScalarDelta<double[]>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<double[]>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadArrayOfDoubles();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T)obj, reader.ReadArrayOfDoubles());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.Write((double[])value);
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
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      var contents = Getter((T)obj);
      return contents == null || contents.Length == 0;
    }

    /// <summary>
    ///  Validate
    /// </summary>
    public override void Validate(object obj, ArrayList errors)
    {
      if (!IsNullable)
      {
        if (Getter((T)obj) == null)
          InvalidValue.AddError(errors, obj, Name, $"{Entity.Name}.{Name} - Value cannot be empty!");
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
      Setter((T)obj, null);
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, double[]> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, double[]> Setter;
  }
}
