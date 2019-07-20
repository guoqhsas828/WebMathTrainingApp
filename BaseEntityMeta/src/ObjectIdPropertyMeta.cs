// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Reflection;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// </summary>
  /// <remarks>
  ///  Currently the following numeric types are supported: Double, Int32, Int64
  /// </remarks>
  public abstract class ObjectIdPropertyMeta : ScalarPropertyMeta
  {
    /// <summary>
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    protected ObjectIdPropertyMeta(ClassMeta entity, ObjectIdPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      if (propInfo.PropertyType != typeof (long))
      {
        throw new MetadataException("Invalid PropertyType: ObjectId property must be of type Int64");
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
      return "reader.ReadObjectId()";
    }

  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ObjectIdPropertyMeta<T> : ObjectIdPropertyMeta where T : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public ObjectIdPropertyMeta(ClassMeta entity, ObjectIdPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      Getter = CreatePropertyGetter<T, long>(propInfo);
      Setter = CreatePropertySetter<T, long>(propInfo);
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
      Setter((T) obj, (long) value);
    }

    /// <summary>
    /// 
    /// </summary>
    public override object GetDefaultValue()
    {
      return 0L;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public override void SetDefaultValue(BaseEntityObject obj)
    {
      Setter((T) obj, 0L);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool HasDefaultValue(BaseEntityObject obj)
    {
      return Getter((T) obj) == 0;
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
      return valueA == valueB ? null : new ScalarDelta<long>(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadScalarDelta<long>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadObjectId();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, reader.ReadObjectId());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.WriteObjectId((long) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.WriteObjectId(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    protected readonly Func<T, long> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, long> Setter;
  }
}

