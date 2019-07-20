// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Describes a component property
  /// </summary>
  /// <remarks>Components are owned by the containing entity and have value semantics.
  /// The properties of the component map to columns in the table of the parent.</remarks>
  public abstract class ComponentPropertyMeta : PropertyMeta
  {
    /// <summary>
    /// Construct runtime instance from ComponentPropertyMeta
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop meta attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected ComponentPropertyMeta(ClassMeta entity, ComponentPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      _prefix = propAttr.Prefix ?? Name;
    }

    #region Properties

    /// <summary>
    /// Prefix to use when forming database column names (defaults to name of property)
    /// </summary>
    /// <remarks></remarks>
    public string Prefix
    {
      get { return _prefix; }
    }

    /// <summary>
    /// Gets the child entity.
    /// </summary>
    /// <remarks></remarks>
    public ClassMeta ChildEntity
    {
      get
      {
        if (_childEntity == null)
        {
          _childEntity = ClassCache.Find(PropertyType);
          if (_childEntity == null)
          {
            throw new MetadataException(String.Format(
              "No entity for type={0}", PropertyType));
          }
        }

        return _childEntity;
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="classCache"></param>
    public override void SecondPassInit(IClassCache classCache)
    {
      base.SecondPassInit(classCache);

      _childEntity = classCache.Find(PropertyType);
      if (_childEntity == null)
      {
        throw new MetadataException(String.Format(
          "No entity for type={0}", PropertyType));
      }

      if (ChildEntity.HasChildKey)
      {
        throw new MetadataException(String.Format(
          "Invalid property [{0}.{1}] : referenced component cannot have ChildKey defined",
          Entity.Name, Name));
      }
    }

    /// <summary>
    /// Thirds the pass init.
    /// </summary>
    /// <remarks></remarks>
    public override void ThirdPassInit()
    {
      if (ChildEntity.HasChildKey)
      {
        throw new MetadataException(String.Format(
          "Invalid property [{0}.{1}] : referenced component cannot have ChildKey defined",
          Entity.Name, Name));
      }
    }

    #endregion

    #region Data

    private readonly string _prefix;
    private ClassMeta _childEntity;

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public class ComponentPropertyMeta<T, TValue> : ComponentPropertyMeta where T : BaseEntityObject
    where TValue : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    public ComponentPropertyMeta(ClassMeta entity, ComponentPropertyAttribute propAttr, PropertyInfo propInfo)
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
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override bool IsSame(object objA, object objB)
    {
      return ClassMeta.IsSame(Getter((T) objA), Getter((T) objB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      return ClassMeta.CreateDelta(Getter((T) objA), Getter((T) objB));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadObjectDelta();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override object Read(IEntityReader reader)
    {
      return reader.ReadComponent();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    public override void Read(BaseEntityObject obj, IEntityReader reader)
    {
      Setter((T) obj, (TValue) reader.ReadComponent());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return string.Format("Read{0}(reader)", typeof (TValue).Name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public override void Write(IEntityWriter writer, object value)
    {
      writer.WriteComponent((BaseEntityObject) value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public override void Write(BaseEntityObject obj, IEntityWriter writer)
    {
      writer.WriteComponent(Getter((T) obj));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="propValue"></param>
    public override void WritePropertyMap(BinaryEntityWriter writer, object propValue)
    {
      ((ComponentData) propValue).Write(writer);
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
    /// Validate class invariants
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <param name="errors">The errors.</param>
    /// <remarks></remarks>
    public override void Validate(object obj, ArrayList errors)
    {
      TValue propValue = Getter((T) obj);
      if (propValue != null)
      {
        propValue.Validate(errors);
      }
      else
      {
        // Null component means null fields (check not-null constraint)
        foreach (var pm in ChildEntity.PropertyList.Where(compProperty => !compProperty.IsNullable))
        {
          InvalidValue.AddError(errors, obj, pm.DisplayName, "Value cannot be empty!");
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
    protected readonly Func<T, TValue> Getter;

    /// <summary>
    /// 
    /// </summary>
    protected readonly Action<T, TValue> Setter;
  }
}