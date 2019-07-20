
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.Data;
using System.Data.Common;
using System.Xml;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using NHibernate.Engine;
#if NETSTANDARD2_0
using IDataReader = System.Data.Common.DbDataReader;
using IDbCommand = System.Data.Common.DbCommand;
#endif

namespace BaseEntity.Database.Types
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  internal class XmlType : IUserType
  {
    public new bool Equals(object x, object y)
    {
      if (x == null)
      {
        return (y == null) ? true : false;
      }
      else if (y == null)
      {
        return false;
      }

      var xdoc = (XmlDocument)x;
      var ydoc = (XmlDocument)y;
      return ydoc.OuterXml == xdoc.OuterXml;
    }

    public int GetHashCode(object x)
    {
      return x.GetHashCode();
    }

    public object NullSafeGet(IDataReader rs, string[] names, ISessionImplementor session, object owner)
    {
      if (names.Length != 1)
      {
        throw new InvalidOperationException("names array has more than one element. can't handle this!");
      }

      var value = rs[names[0]];
      if (value is DBNull)
      {
        return null;
      }

      var doc = new XmlDocument();
      doc.Load((string) value);
      return doc;
    }

    public void NullSafeSet(IDbCommand cmd, object value, int index, ISessionImplementor session)
    {
      var parameter = (DbParameter)cmd.Parameters[index];

      if (value == null)
      {
        parameter.Value = DBNull.Value;
        return;
      }

      parameter.Value = ((XmlDocument)value).OuterXml;
    }

    public object DeepCopy(object value)
    {
      var toCopy = value as XmlDocument;

      if (toCopy == null)
        return null;

      var copy = new XmlDocument();
      copy.LoadXml(toCopy.OuterXml);
      return copy;
    }

    public object Replace(object original, object target, object owner)
    {
      throw new NotImplementedException();
    }

    public object Assemble(object cached, object owner)
    {
      var str = cached as string;
      if (str != null)
      {
        var doc = new XmlDocument();
        doc.LoadXml(str);
        return doc;
      }
      else
      {
        return null;
      }
    }

    public object Disassemble(object value)
    {
      var val = value as XmlDocument;
      if (val != null)
      {
        return val.OuterXml;
      }
      else
      {
        return null;
      }
    }

    public SqlType[] SqlTypes
    {
      get
      {
        return new SqlType[] { new SqlXmlType() };
      }
    }

    public Type ReturnedType
    {
      get { return typeof(XmlDocument); }
    }

    public bool IsMutable
    {
      get { return true; }
    }
  }

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  internal class SqlXmlType : SqlType
  {
    /// <summary>
    /// 
    /// </summary>
    public SqlXmlType() : base(DbType.Xml)
    {
    }
  }
}


