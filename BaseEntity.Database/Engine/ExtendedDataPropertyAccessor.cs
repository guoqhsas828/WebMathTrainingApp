// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NHibernate.Engine;
using NHibernate.Properties;
using BaseEntity.Metadata;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// Provides the mechanism for storing extended properties (persistent
  /// properties not exposed to NHibernate) in an XML column on the table
  /// of the owning entity.
  /// </summary>
  internal class ExtendedDataPropertyAccessor : IPropertyAccessor
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public IGetter GetGetter(Type theClass, string propertyName)
    {
      return new ExtendedDataPropertyGetter(theClass, propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public ISetter GetSetter(Type theClass, string propertyName)
    {
      return new ExtendedDataPropertySetter(theClass, propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool CanAccessThroughReflectionOptimizer
    {
      get { return false; }
    }
  }

  /// <summary>
  /// 
  /// </summary>
  internal class ExtendedDataPropertyGetter : IGetter
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerType"></param>
    /// <param name="propertyName"></param>
    public ExtendedDataPropertyGetter(Type ownerType, string propertyName)
    {
      string ownerName = propertyName.Replace("ExtendedData", String.Empty);
      _classMeta = ClassCache.Find(ownerName);
      _propertyName = propertyName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public object Get(object target)
    {
      var obj = target as PersistentObject;
      if (obj == null)
      {
        return null;
      }

      var sb = new StringBuilder();
      using (var writer = new XmlEntityWriter(sb))
      {
        ClassMeta derivedEntity = null;
        bool getDerivedEntityValues = false;

        if (_classMeta.IsBaseEntity)
        {
          // Check if extended property values for the derived entity are stored in the ExtendedData 
          // column on the BaseEntity table. If so, this method will only be called for the BaseEntity,
          // so we need to get the extended property values for the derived entity as well.

          if (_classMeta.SubclassMapping == SubclassMappingStrategy.TablePerClassHierarchy)
          {
            derivedEntity = ClassCache.Find(obj);
            getDerivedEntityValues = true;
          }
          else if (_classMeta.SubclassMapping == SubclassMappingStrategy.Hybrid)
          {
            derivedEntity = ClassCache.Find(obj);
            if (derivedEntity.PropertyMapping == PropertyMappingStrategy.ExtendedOnly)
              getDerivedEntityValues = true;
          }
        }

        var propertyMetaList = new List<PropertyMeta>();
        if (getDerivedEntityValues)
        {
          // In this case the ExtendedData column on the BaseEntity table is used to store extended data 
          // from the derived entity as well. This method will only be called once for the BaseEntity, so
          // we need to get the values owned by both the base and derived entities.

          for (var myEntity = derivedEntity; myEntity != null; myEntity = myEntity.BaseEntity)
          {
            propertyMetaList.AddRange(myEntity.ExtendedPropertyMap.Values);
          }
        }
        else
        {
          propertyMetaList.AddRange(_classMeta.ExtendedPropertyMap.Values);
        }

        if (propertyMetaList.Count != 0)
        {
          writer.WriteEntity(obj, propertyMetaList);
        }
      }

      return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    public Type ReturnType
    {
      get { return typeof(string); }
    }

    /// <summary>
    /// 
    /// </summary>
    public string PropertyName
    {
      get { return _propertyName; }
    }

    /// <summary>
    /// 
    /// </summary>
    public MethodInfo Method
    {
      get { throw new NotImplementedException(); }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="mergeMap"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    public object GetForInsert(object owner, IDictionary mergeMap, ISessionImplementor session)
    {
      return Get(owner);
    }

    private readonly ClassMeta _classMeta;
    private readonly string _propertyName;
  }

  /// <summary>
  /// 
  /// </summary>
  internal class ExtendedDataPropertySetter : ISetter
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerType"></param>
    /// <param name="propertyName"></param>
    public ExtendedDataPropertySetter(Type ownerType, string propertyName)
    {
      _propertyName = propertyName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="value"></param>
    public void Set(object target, object value)
    {
      var xml = (string)value;
      if (xml != null)
      {
        using (var reader = new XmlEntityReader(xml))
        {
          var po = (PersistentObject)target;
          reader.ReadEntity(po);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public string PropertyName
    {
      get { return _propertyName; }
    }

    /// <summary>
    /// 
    /// </summary>
    public MethodInfo Method
    {
      get { throw new NotImplementedException(); }
    }

    private readonly string _propertyName;
  }
}