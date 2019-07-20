// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Data;
using NHibernate;
using NHibernate.Dialect;
using NHibernate.Engine;
#if NETSTANDARD2_0
using IDataReader = System.Data.Common.DbDataReader;
using IDbCommand = System.Data.Common.DbCommand;
#endif

namespace BaseEntity.Database.Types
{
  /// <summary>
  /// Used to map DateTime property values where IsTreatedAsDateOnly = true
  /// </summary>
  [Serializable]
  public class DateType : NHibernate.Type.DateType
  {
    private static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rs"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public override object Get(IDataReader rs, int index, ISessionImplementor session)
    {
      try
      {
        var dbValue = Convert.ToDateTime(rs[index]);
        return dbValue == SqlMinDate ? DateTime.MinValue : dbValue.Date;
      }
      catch (Exception ex)
      {
        throw new FormatException(string.Format("Input string '{0}' was not in the correct format.", rs[index]), ex);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="st"></param>
    /// <param name="value"></param>
    /// <param name="index"></param>
    public override void Set(IDbCommand st, object value, int index, ISessionImplementor session)
    {
      DateTime dtValue = (DateTime)value;
      var dbValue = dtValue == DateTime.MinValue ? SqlMinDate : dtValue.Date;
      ((IDataParameter)st.Parameters[index]).Value = dbValue;
    }

    #region IVersionType Members

    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public override bool IsEqual(object x, object y)
    {
      if (x == y)
      {
        return true;
      }

      if (x == null || y == null)
      {
        return false;
      }

      var date1 = (DateTime)x;
      var date2 = (DateTime)y;

      if (date1.Equals(date2))
      {
        return true;
      }

      return date1.Year == date2.Year && date1.Month == date2.Month && date1.Day == date2.Day;
    }

    #endregion

    /// <summary>
    /// </summary>
    public override string Name
    {
      get { return "Date"; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    public override object FromStringValue(string xml)
    {
      return DateTime.Parse(xml);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="dialect"></param>
    /// <returns></returns>
    public override string ObjectToSQLString(object value, Dialect dialect)
    {
      var dtValue = (DateTime)value;
      if (dtValue == DateTime.MinValue)
        dtValue = SqlMinDate;

      return "'" + dtValue.ToString(SqlFormat) + "'";
    }

    private const string SqlFormat = "yyyy-MM-dd";

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string ToString(DateTime dt)
    {
      return dt == DateTime.MinValue ? "17530101" : dt.ToString("yyyyMMdd");
    }
  }
}