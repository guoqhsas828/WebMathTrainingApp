// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using NHibernate;
using NHibernate.Type;
#if NETSTANDARD2_0
using IDbCommand = System.Data.Common.DbCommand;
#endif

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class DbDataParameter
  {
    private static readonly Dictionary<Type, NullableType> _typeConverters = new Dictionary<Type, NullableType>();

    static DbDataParameter()
    {
      _typeConverters[typeof(Int16)] = NHibernateUtil.Int16;
      _typeConverters[typeof(Int32)] = NHibernateUtil.Int32;
      _typeConverters[typeof(Int64)] = NHibernateUtil.Int64;
      _typeConverters[typeof(Boolean)] = NHibernateUtil.Boolean;
      _typeConverters[typeof(String)] = NHibernateUtil.String;
      _typeConverters[typeof(Double)] = NHibernateUtil.Double;
      _typeConverters[typeof(DateTime)] = NHibernateUtil.DateTime;
      _typeConverters[typeof(Guid)] = NHibernateUtil.Guid;
      _typeConverters[typeof(byte[])] = NHibernateUtil.Binary;
    }

    /// <summary>
    /// Registers the type converter.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="converter">The converter.</param>
    public static void RegisterTypeConverter(Type type, NullableType converter)
    {
      _typeConverters[type] = converter;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="type"></param>
    /// <param name="value"></param>
    public DbDataParameter(string name, Type type, object value)
    {
      if (name == null)
        throw new ArgumentNullException("name");
      if (type == null)
        throw new ArgumentNullException("type");

      Name = name;
      Converter = GetConverter(type);
      Value = value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public DbDataParameter(string name, object value)
    {
      if (name == null)
        throw new ArgumentNullException("name");
      if (value == null)
        throw new ArgumentNullException("value");

      Name = name;
      Converter = GetConverter(value.GetType());
      Value = value;
    }

    private static NullableType GetConverter(Type type)
    {
      if (type.IsEnum)
        return NHibernateUtil.Int32;
      NullableType converter;
      if (_typeConverters.TryGetValue(type, out converter))
      {
        return converter;
      }
      throw new ArgumentException("Cannot determine NHibernateType for [" + type + "]");
    }

    /// <summary>
    /// 
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public object Value { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    private NullableType Converter { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="index"></param>
    /// <param name="session"></param>
    public void AddToCommand(IDbCommand cmd, int index, ISession session)
    {
      IDbDataParameter p = cmd.CreateParameter();
      p.ParameterName = Name;
      p.DbType = Converter.SqlType.DbType;
      cmd.Parameters.Add(p);
      Converter.NullSafeSet(cmd,
        Value,
        index,
        session.GetSessionImplementation());
    }
  }
}