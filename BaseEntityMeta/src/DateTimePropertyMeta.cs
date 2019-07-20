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
  /// Describes a DateTime property
  /// </summary>
  /// <remarks></remarks>
  public abstract class DateTimePropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// Value to represent a psuedo null DateTime in the database.
    /// This is the value for a DateTime that has not been given a value but is saved to the db.
    /// </summary>
    public static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1, 0, 0, 0);

    /// <summary>
    /// Value to represent a psuedo null UTC DateTime in the database.
    /// This is the value for a UTC DateTime that has not been given a value but is saved to the db.
    /// </summary>
    public static readonly DateTime SqlMinDateUtc = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimePropertyMeta"/> class.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop meta attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected DateTimePropertyMeta(ClassMeta entity, DateTimePropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      IsTreatedAsDateOnly = propAttr.IsTreatedAsDateOnly;
      if (PropertyType == typeof (DateTime))
      {
      }
      else if (PropertyType != typeof (DateTime?))
      {
        throw new InvalidPropertyTypeException(entity, propInfo);
      }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is treated as date only.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is treated as date only; otherwise, <c>false</c>.
    /// </value>
    public bool IsTreatedAsDateOnly { get; }

    /// <summary>
    /// Thirds the pass init.
    /// </summary>
    /// <remarks></remarks>
    public override void ThirdPassInit()
    {
    }

    /// <summary>
    /// Formats the specified pm.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns></returns>
    public override string Format(object o, string propertyName = "Name")
    {
      //140224 - Added to handle nullable dates as well.
      if (IsNullable && PropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof (Nullable<>))
      {
        var value = (DateTime?) GetValue(o);

        return (!value.HasValue || value.Value == DateTime.MinValue) ? "" : GetValue(o).ToString();
      }
      else
      {
        var value = (DateTime) GetValue(o);
        return value == DateTime.MinValue ? "" : GetValue(o).ToString();
      }
    }

    /// <summary>
    /// Builds the key string.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <returns></returns>
    public override string BuildKeyString(object obj)
    {
      var dtValue = (DateTime) obj;
      return dtValue == DateTime.MinValue ? "17530101" : dtValue.ToString("yyyyMMdd");
    }

    /// <summary>
    /// 
    /// </summary>
    protected DateTime DefaultValue { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      if (IsTreatedAsDateOnly)
      {
        return PropertyType == typeof (DateTime) ? "reader.ReadDate()" : "reader.ReadNullableDate()";
      }

      return PropertyType == typeof (DateTime) ? "reader.ReadDateTime()" : "reader.ReadNullableDateTime()";
    }

    /// <summary>
    /// Maps the scalar.
    /// </summary>
    /// <param name="ownerElement">The owner element.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="mustAllowNull">if set to <c>true</c> [must allow null].</param>
    public override void MapScalar(XmlElement ownerElement, string prefix, bool mustAllowNull)
    {
      // MJT: Need to figure out a different way to configure HBM output for properties to eliminate hard-coded type strings
      ownerElement.SetAttribute("type", IsTreatedAsDateOnly ? "BaseEntity.Database.Types.DateType, BaseEntity.Database" : "NHibernate.Type.UtcDateTimeType, NHibernate");
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool MapXmlSchema(XmlSchema xmlSchema, XmlSchemaElement element)
    {
      // Can't use metadata IsNullable to determine if a DateTime element can be empty
      // We must allow all dates to potentially be empty, since ExportUtil will output empty elements
      // for DateTime.MinValue regardless of what PropertyMeta.IsNullable says.
      //if (IsNullable)
      //{
      // TODO: use explicit nillable instead of allowing empty element (would diverge from original DataExporter format)?
      /*
        <xs:simpleType>
          <xs:union>
            <xs:simpleType>
              <xs:restriction base="xs:dateTime" />
            </xs:simpleType>
            <xs:simpleType>
              <xs:restriction base="xs:string">
                <xs:length value="0" />
              </xs:restriction>
            </xs:simpleType>
          </xs:union>
        </xs:simpleType>
      */
      var simpleType = new XmlSchemaSimpleType();
      element.SchemaType = simpleType;
      var union = new XmlSchemaSimpleTypeUnion();
      simpleType.Content = union;
      var dtType = new XmlSchemaSimpleType();
      union.BaseTypes.Add(dtType);
      var dtRestriction = new XmlSchemaSimpleTypeRestriction();
      dtType.Content = dtRestriction;
      dtRestriction.BaseTypeName = IsTreatedAsDateOnly ? new XmlQualifiedName("date", XsdGenerator.XmlSchemaNs) : new XmlQualifiedName("dateTime", XsdGenerator.XmlSchemaNs);
      var emptyType = new XmlSchemaSimpleType();
      union.BaseTypes.Add(emptyType);
      var emptyRestriction = new XmlSchemaSimpleTypeRestriction();
      emptyType.Content = emptyRestriction;
      emptyRestriction.BaseTypeName = new XmlQualifiedName("string", XsdGenerator.XmlSchemaNs);
      var lengthFacet = new XmlSchemaLengthFacet();
      emptyRestriction.Facets.Add(lengthFacet);
      lengthFacet.Value = "0";
      //}
      //else
      //{
      //  /*
      //    <xs:element ... type="xs:dateTime" />
      //  */
      //  element.SchemaTypeName = IsTreatedAsDateOnly ? new XmlQualifiedName("date", XsdGenerator.XmlSchemaNs) : new XmlQualifiedName("dateTime", XsdGenerator.XmlSchemaNs);
      //}
      return true;
    }
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class DateTimePropertyMeta<T> : DateTimePropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public DateTimePropertyMeta(ClassMeta entity, DateTimePropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, DateTime>(propInfo);
      Setter = CreatePropertySetter<T, DateTime>(propInfo);

      DefaultValue = IsTreatedAsDateOnly ? DateTime.MinValue : SqlMinDateUtc;
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
      var dateTimeValue = (DateTime) value;
      Setter((T) obj, IsTreatedAsDateOnly ? dateTimeValue.Date : dateTimeValue.ToUniversalTime());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      var valueA = Getter((T)objA).TrimMilliSeconds();
      var valueB = Getter((T)objB).TrimMilliSeconds();
      return valueA == valueB;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      var valueA = Getter((T)objA).TrimMilliSeconds();
      var valueB = Getter((T)objB).TrimMilliSeconds();
      return valueA == valueB ? null : new ScalarDelta<DateTime>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<DateTime>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return IsTreatedAsDateOnly ? reader.ReadDate() : reader.ReadDateTime();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, IsTreatedAsDateOnly ? reader.ReadDate() : reader.ReadDateTime());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      if (IsTreatedAsDateOnly)
        writer.WriteDate((DateTime) value);
      else
        writer.Write((DateTime) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      if (IsTreatedAsDateOnly)
        writer.WriteDate(Getter((T) obj));
      else
        writer.Write(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      if (IsTreatedAsDateOnly)
        writer.WriteDate((DateTime) propValue);
      else
        writer.Write((DateTime) propValue);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      return Getter((T) obj) == DefaultValue;
    }

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public override void Validate(object obj, System.Collections.ArrayList errors)
    {
      var dateTime = Getter((T) obj);

      string errorMsg = null;

      if (IsTreatedAsDateOnly)
      {
        if (dateTime == DateTime.MinValue)
        {
        }
        else if (dateTime < SqlMinDate)
        {
          errorMsg = $"DateTime value [{dateTime.Date}] < MinValue [{SqlMinDate.ToLocalTime()}]";
        }
        else if (dateTime.Kind == DateTimeKind.Utc)
        {
          errorMsg = "DateTime value is UTC!";
        }
      }
      else
      {
        dateTime.TryValidate(out errorMsg);
      }

      if (!string.IsNullOrEmpty(errorMsg))
        InvalidValue.AddError(errors, obj, Name, $"{Entity.Name}.{Name} - {errorMsg}");
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return DefaultValue;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, DefaultValue);
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, DateTime> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, DateTime> Setter;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class NullableDateTimePropertyMeta<T> : DateTimePropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public NullableDateTimePropertyMeta(ClassMeta entity, DateTimePropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, DateTime?>(propInfo);
      Setter = CreatePropertySetter<T, DateTime?>(propInfo);
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
      var dateTimeValue = (DateTime?) value;
      Setter((T) obj,
        dateTimeValue.HasValue
          ? (IsTreatedAsDateOnly ? dateTimeValue.Value.Date : dateTimeValue.Value.ToUniversalTime())
          : (DateTime?) null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      DateTime? valueA = Getter((T)objA)?.TrimMilliSeconds();
      DateTime? valueB = Getter((T)objB)?.TrimMilliSeconds();
      return valueA == valueB;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      DateTime? valueA = Getter((T)objA)?.TrimMilliSeconds();
      DateTime? valueB = Getter((T)objB)?.TrimMilliSeconds();
      return valueA == valueB ? null : new ScalarDelta<DateTime?>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<DateTime?>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return IsTreatedAsDateOnly ? reader.ReadNullableDate() : reader.ReadNullableDateTime();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, IsTreatedAsDateOnly ? reader.ReadNullableDate() : reader.ReadNullableDateTime());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      if (IsTreatedAsDateOnly)
        writer.WriteDate((DateTime?) value);
      else
        writer.Write((DateTime?) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      if (IsTreatedAsDateOnly)
        writer.WriteDate(Getter((T) obj));
      else
        writer.Write(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      if (IsTreatedAsDateOnly)
        writer.WriteDate((DateTime?) propValue);
      else
        writer.Write((DateTime?) propValue);
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
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public override void Validate(object obj, System.Collections.ArrayList errors)
    {
      DateTime? propValue = Getter((T) obj);

      string errorMsg = null;

      if (propValue == null)
      {
        if (!IsNullable)
          errorMsg = $"{Entity.Name}.{Name} - Value cannot be empty!";
      }
      else
      {
        var dateTime = propValue.Value;

        if (IsTreatedAsDateOnly)
        {
          if (dateTime == DateTime.MinValue)
          {
          }
          else if (dateTime < SqlMinDate)
          {
            errorMsg = $"DateTime value [{dateTime.Date}] < MinValue [{SqlMinDate.ToLocalTime()}]";
          }
          else if (dateTime.Kind == DateTimeKind.Utc)
          {
            errorMsg = "DateTime value is UTC!";
          }
        }
        else
        {
          dateTime.TryValidate(out errorMsg);
        }
      }

      if (!string.IsNullOrEmpty(errorMsg))
        InvalidValue.AddError(errors, obj, Name, $"{Entity.Name}.{Name} - {errorMsg}");
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
    protected readonly Func<T, DateTime?> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, DateTime?> Setter;
  }
}