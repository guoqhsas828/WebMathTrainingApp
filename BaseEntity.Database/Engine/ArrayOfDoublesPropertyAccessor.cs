
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using NHibernate.Engine;
using NHibernate.Properties;
using BaseEntity.DatabaseEngine;
using BaseEntity.Metadata;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// Provides the mechanism for storing extended properties (persistent
  /// properties not exposed to NHibernate) in an XML column on the table
  /// of the owning entity.
  /// </summary>
  internal class ArrayOfDoublesPropertyAccessor : IPropertyAccessor
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public IGetter GetGetter(Type theClass, string propertyName)
    {
      return new ArrayOfDoublesPropertyGetter(theClass, propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public ISetter GetSetter(Type theClass, string propertyName)
    {
      return new ArrayOfDoublesPropertySetter(theClass, propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool CanAccessThroughReflectionOptimizer
    {
      get { return false; }
    }
  }
}

namespace BaseEntity.DatabaseEngine
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  internal class ArrayOfDoublesPropertyGetter : IGetter, ISerializable
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerType"></param>
    /// <param name="propertyName"></param>
    public ArrayOfDoublesPropertyGetter(Type ownerType, string propertyName)
    {
      _entity = ClassCache.Find(ownerType);
      _property = _entity.GetProperty(propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public ArrayOfDoublesPropertyGetter(SerializationInfo info, StreamingContext context)
    {
      _entity = ClassCache.Find((string)info.GetValue("_entity", typeof(string)));
      _property = _entity.GetProperty((string)info.GetValue("_property", typeof(string)));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("_entity", _entity.Name);
      info.AddValue("_property", _property.Name);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public object Get(object target)
    {
      if (target == null)
      {
        return null;
      }
      var doubles = (double[]) _property.GetValue(target);
      var byteArray = new byte[doubles.Length * sizeof(double)];
      for (int i = 0, j = 0; i < doubles.Length; ++i)
      {
        foreach (byte b in BitConverter.GetBytes(doubles[i]))
          byteArray[j++] = b;
      }
      return byteArray;
    }

    /// <summary>
    /// 
    /// </summary>
    public Type ReturnType
    {
      get { return typeof(byte[]); }
    }

    /// <summary>
    /// 
    /// </summary>
    public string PropertyName
    {
      get { return _property.Name; }
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

    private readonly ClassMeta _entity;
    private readonly PropertyMeta _property;
  }

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  internal class ArrayOfDoublesPropertySetter : ISetter, ISerializable
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerType"></param>
    /// <param name="propertyName"></param>
    public ArrayOfDoublesPropertySetter(Type ownerType, string propertyName)
    {
      _entity = ClassCache.Find(ownerType);
      _property = _entity.GetProperty(propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public ArrayOfDoublesPropertySetter(SerializationInfo info, StreamingContext context)
    {
      _entity = ClassCache.Find((string)info.GetValue("_entity", typeof(string)));
      _property = _entity.GetProperty((string)info.GetValue("_property", typeof(string)));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("_entity", _entity.Name);
      info.AddValue("_property", _property.Name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="value"></param>
    public void Set(object target, object value)
    {
      if (value == null)
      {
        return;
      }
      var bytes = (byte[]) value;
      int length = bytes.Length / sizeof(double);
      var doubles = new double[length];
      for (int i = 0; i < length; ++i)
      {
        doubles[i] = BitConverter.ToDouble(bytes, i * sizeof(double));
      }
      _property.SetValue(target, doubles);
    }

    /// <summary>
    /// 
    /// </summary>
    public string PropertyName
    {
      get { return _property.Name; }
    }

    /// <summary>
    /// 
    /// </summary>
    public MethodInfo Method
    {
      get { throw new NotImplementedException(); }
    }

    private readonly ClassMeta _entity;
    private readonly PropertyMeta _property;
  }
}

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// Provides the mechanism for storing extended properties (persistent
  /// properties not exposed to NHibernate) in an XML column on the table
  /// of the owning entity.
  /// </summary>
  internal class ArrayOf2DDoublesPropertyAccessor : IPropertyAccessor
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public IGetter GetGetter(Type theClass, string propertyName)
    {
      return new ArrayOf2DDoublesPropertyGetter(theClass, propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="theClass"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public ISetter GetSetter(Type theClass, string propertyName)
    {
      return new ArrayOf2DDoublesPropertySetter(theClass, propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool CanAccessThroughReflectionOptimizer
    {
      get { return false; }
    }
  }
}

namespace BaseEntity.DatabaseEngine
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  internal class ArrayOf2DDoublesPropertyGetter : IGetter, ISerializable
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerType"></param>
    /// <param name="propertyName"></param>
    public ArrayOf2DDoublesPropertyGetter(Type ownerType, string propertyName)
    {
      _entity = ClassCache.Find(ownerType);
      _property = _entity.GetProperty(propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public ArrayOf2DDoublesPropertyGetter(SerializationInfo info, StreamingContext context)
    {
      _entity = ClassCache.Find((string)info.GetValue("_entity", typeof(string)));
      _property = _entity.GetProperty((string)info.GetValue("_property", typeof(string)));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("_entity", _entity.Name);
      info.AddValue("_property", _property.Name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public object Get(object target)
    {
      if (target == null)
      {
        return null;
      }
      var doubles = (double[,])_property.GetValue(target);
      var byteArray = new byte[doubles.Length * sizeof(double)];

      var k = 0;
      for (var i = 0; i < doubles.GetLength(0); ++i)
      {
        for (var j = 0; j < doubles.GetLength(1); ++j)
        {
          foreach (var b in BitConverter.GetBytes(doubles[i, j]))
          {
            byteArray[k++] = b;
          }
        }
      }
      return byteArray;
    }

    /// <summary>
    /// 
    /// </summary>
    public Type ReturnType
    {
      get { return typeof(byte[]); }
    }

    /// <summary>
    /// 
    /// </summary>
    public string PropertyName
    {
      get { return _property.Name; }
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

    private readonly ClassMeta _entity;
    private readonly PropertyMeta _property;
  }

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  internal class ArrayOf2DDoublesPropertySetter : ISetter, ISerializable
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerType"></param>
    /// <param name="propertyName"></param>
    public ArrayOf2DDoublesPropertySetter(Type ownerType, string propertyName)
    {
      _entity = ClassCache.Find(ownerType);
      _property = _entity.GetProperty(propertyName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public ArrayOf2DDoublesPropertySetter(SerializationInfo info, StreamingContext context)
    {
      _entity = ClassCache.Find((string)info.GetValue("_entity", typeof(string)));
      _property = _entity.GetProperty((string)info.GetValue("_property", typeof(string)));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("_entity", _entity.Name);
      info.AddValue("_property", _property.Name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="value"></param>
    public void Set(object target, object value)
    {
      if (value == null)
      {
        return;
      }
      var bytes = (byte[])value;

      var dim = (int) Math.Sqrt(bytes.Length / sizeof(double));

      var k = 0;
      var doubles = new double[dim, dim];
      for (var i = 0; i < doubles.GetLength(0); ++i)
      {
        for (var j = 0; j < doubles.GetLength(1); ++j)
        {
          doubles[i,j] = BitConverter.ToDouble(bytes, sizeof(double) * k++);
        }
      }
      _property.SetValue(target, doubles);
    }

    /// <summary>
    /// 
    /// </summary>
    public string PropertyName
    {
      get { return _property.Name; }
    }

    /// <summary>
    /// 
    /// </summary>
    public MethodInfo Method
    {
      get { throw new NotImplementedException(); }
    }

    private readonly ClassMeta _entity;
    private readonly PropertyMeta _property;
  }
}
