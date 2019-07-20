using System;
using System.Data;
using System.Data.SqlTypes;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public static class DataReaderExtensions
  {
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader"></param>
    /// <param name="idx"></param>
    /// <returns></returns>
    public static T GetValue<T>(this IDataReader reader, int idx)
    {
      object value = reader[idx];
      return (value == DBNull.Value) ? default(T) : (T) value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public static T GetValue<T>(this IDataReader reader, string column)
    {
      object value = reader[column];
      return (value == DBNull.Value) ? default(T) : (T)value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader"></param>
    /// <param name="idx"></param>
    /// <returns></returns>
    public static T? GetNullableValue<T>(this IDataReader reader, int idx) where T : struct
    {
      object value = reader[idx];
      return (value == DBNull.Value) ? null : (T?) value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public static T? GetNullableValue<T>(this IDataReader reader, string column) where T : struct
    {
      object value = reader[column];
      return (value == DBNull.Value) ? null : (T?)value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public static DateTime GetDate(this IDataReader reader, string column)
    {
      object value = reader[column];
      if (value == DBNull.Value)
        return DateTime.MinValue;
      var date = (DateTime)value;
      return date == SqlDateTime.MinValue.Value ? DateTime.MinValue : date;
    }

    /// <summary>
    ///   Gets the UTC date and time of the data value of the specified field
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="idx">Index of the field to find</param>
    /// <returns></returns>
    public static DateTime GetUtcDateTime(this IDataReader reader, int idx)
    {
      return DateTime.SpecifyKind(reader.GetDateTime(idx), DateTimeKind.Utc);
    }

    /// <summary>
    ///   Gets the UTC date and time of the data value of the specified field
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="column">Index of the field to find</param>
    /// <returns></returns>
    public static DateTime GetUtcDateTime(this IDataReader reader, string column)
    {
      return DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(column)), DateTimeKind.Utc);
    }

    /// <summary>
    ///   Gets the UTC date and time of the data value of the specified field
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="idx">Index of the field to find</param>
    /// <returns></returns>
    public static DateTime? GetNullableUtcDateTime(this IDataReader reader, int idx)
    {
      object value = reader[idx];
      return (value == DBNull.Value) ? null : new DateTime?(DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc));
    }
  }
}