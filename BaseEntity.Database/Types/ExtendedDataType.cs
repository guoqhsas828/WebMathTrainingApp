// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Data;
using System.Data.Common;
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
  /// Used to persist ExtendedData properties
  /// </summary>
  [Serializable]
  internal class ExtendedDataType : IUserType
  {
    public new bool Equals(object x, object y)
    {
      var xData = (string)x;
      var yData = (string)y;

      bool isSame;

      if (xData == null)
      {
        isSame = yData == null;
      }
      else
      {
        isSame = xData.Equals(yData);
      }

      return isSame;
    }

    public int GetHashCode(object x)
    {
      return x.GetHashCode();
    }

    public object NullSafeGet(IDataReader rs, string[] names, ISessionImplementor session, object owner)
    {
      if (names.Length != 1)
      {
        throw new InvalidOperationException("Invalid names array (must have only one element)!");
      }

      var value = rs[names[0]];
      return value == DBNull.Value ? null : value;
    }

    public void NullSafeSet(IDbCommand cmd, object value, int index, ISessionImplementor session)
    {
      var parameter = (DbParameter)cmd.Parameters[index];

      if (value == null)
      {
        parameter.Value = DBNull.Value;
      }
      else
      {
        parameter.Value = (string)value;
      }
    }

    public object DeepCopy(object value)
    {
      return value;
    }

    public object Replace(object original, object target, object owner)
    {
      return DeepCopy(original);
    }

    public object Assemble(object cached, object owner)
    {
      return DeepCopy(cached);
    }

    public object Disassemble(object value)
    {
      return DeepCopy(value);
    }

    public SqlType[] SqlTypes
    {
      get { return new SqlType[] {new SqlXmlType()}; }
    }

    public Type ReturnedType
    {
      get { return typeof(string); }
    }

    public bool IsMutable
    {
      get { return true; }
    }
  }
}