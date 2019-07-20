// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataRowExtensions.cs" company="WebMathTraining, Inc">
//   (c) 2011 WebMathTraining, Inc
// </copyright>
// <summary>
//   The data row extensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace BaseEntity.Database.Extension
{
  using System;
  using System.Data;
  using System.Diagnostics;

  using BaseEntity.Shared;

  /// <summary>
  /// Some extension methods to make a System.Data.DataRow more useful for doing flexible type conversions to WebMathTraining types
  /// </summary>
  public static class DataRowExtensions
  {
    #region Public Methods

    /// <summary>
    /// Flexibly converts a field (column value) of a DataRow to a value of a specific type.
    /// Will try several methods for conversion - if no direct conversion is possible some string conversion to WebMathTraining built-in
    /// types will be attempted.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>The converted value</returns>
    public static T ConvertField<T>(this DataRow row, string columnName) where T : struct
    {
      if (row == null)
      {
        throw new ArgumentNullException("row");
      }
      if (columnName == null)
      {
        throw new ArgumentNullException("columnName");
      }

      try
      {
        // Does the requested type match exactly?
        return row.Field<T>(columnName);
      }
      catch (InvalidCastException)
      {
        try
        {
          // Is is directly convertible to the requested type?
          return (T)Convert.ChangeType(row[columnName], typeof(T));
        }
        catch (Exception)
        {
          // Last chance - try some string conversions

          // Enum?
          var stringValue = row[columnName].ToString().Trim();
          if (typeof(Enum).IsAssignableFrom(typeof(T)))
          {
            return EnumUtil.Parse<T>(stringValue, columnName, true);
          }

          // DateTime?
          if (typeof(DateTime).IsAssignableFrom(typeof(T)))
          {
            try
            {
              try
              {
                var dateTime = row.ConvertField<DateTime>(columnName);
                return (T)(object)dateTime;
              }
              catch (FormatException)
              {
                // Try excel format
                var excelDate = row.ConvertField<double>(columnName);
                return (T)(object)NumericUtils.ExcelDateToDateTime(excelDate);
              }
            }
            catch (FormatException fex)
            {
              throw new FormatException(string.Format("Invalid date [{0}] for {1}", row[columnName], columnName), fex);
            }
          }

#if WMB 
          // Refactoring to remove toolkit dependency - only one call site (BondImport sample) actually uses Calendar, 
          // so changing that sample to import a string and convert to calendar there
          // Calendar?
          if (typeof(Calendar).IsAssignableFrom(typeof(T)))
          {
            var cal = CalendarCalc.GetCalendar(stringValue);
            if (!CalendarCalc.IsValidCalendar(cal))
            {
              throw new FormatException(string.Format("Invalid calendar [{0}] for {1}", cal, columnName));
            }
            return (T) (object) cal;
          }
#endif

          // Out of ideas
          try
          {
            return (T)Convert.ChangeType(stringValue, typeof(T));
          }
          catch (FormatException fex)
          {
            throw new FormatException(string.Format("Invalid {0} [{1}] for {2}", typeof(T), stringValue, columnName), fex);
          }
        }
      }
    }

    /// <summary>
    /// Flexibly converts a field (column value) of a DataRow to a value of a specific type.
    /// Returns the default value for the type if a conversion cannot be made.
    /// Will try several methods for conversion - if no direct conversion is possible some string conversion to WebMathTraining built-in
    /// types will be attempted.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>The converted value, or the default value for the type</returns>
    public static T ConvertFieldOrDefault<T>(this DataRow row, string columnName) where T : struct
    {
      return row.ConvertFieldOrDefault(columnName, default(T));
    }

    /// <summary>
    /// Flexibly converts a field (column value) of a DataRow to a value of a specific type.
    /// Returns the default value for the type if a conversion cannot be made.
    /// Will try several methods for conversion - if no direct conversion is possible some string conversion to WebMathTraining built-in
    /// types will be attempted.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="row">The row.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="defaultValue">The default value to use if no conversion can be made</param>
    /// <returns>The converted value, or the default value specified</returns>
    public static T ConvertFieldOrDefault<T>(this DataRow row, string columnName, T defaultValue) where T : struct
    {
      try
      {
        return row.ConvertField<T>(columnName);
      }
      catch (FormatException)
      {
        return defaultValue;
      }
    }
    #endregion

    #region MS DataRowExtensions

    /// <summary>
    ///  This method provides access to the values in each of the columns in a given row. 
    ///  This method makes casts unnecessary when accessing columns. 
    ///  Additionally, Field supports nullable types and maps automatically between DBNull and 
    ///  Nullable when the generic type is nullable. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow
    /// </param>
    /// <param name="columnName">
    ///   The input column name specificy which row value to retrieve.
    /// </param>
    /// <returns>
    ///   The DataRow value for the column specified.
    /// </returns> 
    public static T Field<T>(this DataRow row, string columnName)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      return UnboxT<T>.Unbox(row[columnName]);
    }

    /// <summary>
    ///  This method provides access to the values in each of the columns in a given row. 
    ///  This method makes casts unnecessary when accessing columns. 
    ///  Additionally, Field supports nullable types and maps automatically between DBNull and 
    ///  Nullable when the generic type is nullable. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow
    /// </param>
    /// <param name="column">
    ///   The input DataColumn specificy which row value to retrieve.
    /// </param>
    /// <returns>
    ///   The DataRow value for the column specified.
    /// </returns> 
    public static T Field<T>(this DataRow row, DataColumn column)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      return UnboxT<T>.Unbox(row[column]);
    }

    /// <summary>
    ///  This method provides access to the values in each of the columns in a given row. 
    ///  This method makes casts unnecessary when accessing columns. 
    ///  Additionally, Field supports nullable types and maps automatically between DBNull and 
    ///  Nullable when the generic type is nullable. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow
    /// </param>
    /// <param name="columnIndex">
    ///   The input ordinal specificy which row value to retrieve.
    /// </param>
    /// <returns>
    ///   The DataRow value for the column specified.
    /// </returns> 
    public static T Field<T>(this DataRow row, int columnIndex)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      return UnboxT<T>.Unbox(row[columnIndex]);
    }

    /// <summary>
    ///  This method provides access to the values in each of the columns in a given row. 
    ///  This method makes casts unnecessary when accessing columns. 
    ///  Additionally, Field supports nullable types and maps automatically between DBNull and 
    ///  Nullable when the generic type is nullable. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow
    /// </param>
    /// <param name="columnIndex">
    ///   The input ordinal specificy which row value to retrieve.
    /// </param>
    /// <param name="version">
    ///   The DataRow version for which row value to retrieve.
    /// </param>
    /// <returns>
    ///   The DataRow value for the column specified.
    /// </returns> 
    public static T Field<T>(this DataRow row, int columnIndex, DataRowVersion version)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      return UnboxT<T>.Unbox(row[columnIndex, version]);
    }

    /// <summary>
    ///  This method provides access to the values in each of the columns in a given row. 
    ///  This method makes casts unnecessary when accessing columns. 
    ///  Additionally, Field supports nullable types and maps automatically between DBNull and 
    ///  Nullable when the generic type is nullable. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow
    /// </param>
    /// <param name="columnName">
    ///   The input column name specificy which row value to retrieve.
    /// </param>
    /// <param name="version">
    ///   The DataRow version for which row value to retrieve.
    /// </param>
    /// <returns>
    ///   The DataRow value for the column specified.
    /// </returns> 
    public static T Field<T>(this DataRow row, string columnName, DataRowVersion version)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      return UnboxT<T>.Unbox(row[columnName, version]);
    }

    /// <summary>
    ///  This method provides access to the values in each of the columns in a given row. 
    ///  This method makes casts unnecessary when accessing columns. 
    ///  Additionally, Field supports nullable types and maps automatically between DBNull and 
    ///  Nullable when the generic type is nullable. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow
    /// </param>
    /// <param name="column">
    ///   The input DataColumn specificy which row value to retrieve.
    /// </param>
    /// <param name="version">
    ///   The DataRow version for which row value to retrieve.
    /// </param>
    /// <returns>
    ///   The DataRow value for the column specified.
    /// </returns> 
    public static T Field<T>(this DataRow row, DataColumn column, DataRowVersion version)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      return UnboxT<T>.Unbox(row[column, version]);
    }

    /// <summary>
    ///  This method sets a new value for the specified column for the DataRow it’s called on. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow.
    /// </param>
    /// <param name="columnIndex">
    ///   The input ordinal specifying which row value to set.
    /// </param>
    /// <param name="value">
    ///   The new row value for the specified column.
    /// </param>
    public static void SetField<T>(this DataRow row, int columnIndex, T value)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      row[columnIndex] = (object)value ?? DBNull.Value;
    }

    /// <summary>
    ///  This method sets a new value for the specified column for the DataRow it’s called on. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow.
    /// </param>
    /// <param name="columnName">
    ///   The input column name specificy which row value to retrieve.
    /// </param>
    /// <param name="value">
    ///   The new row value for the specified column.
    /// </param>
    public static void SetField<T>(this DataRow row, string columnName, T value)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      row[columnName] = (object)value ?? DBNull.Value;
    }

    /// <summary>
    ///  This method sets a new value for the specified column for the DataRow it’s called on. 
    /// </summary>
    /// <param name="row">
    ///   The input DataRow.
    /// </param>
    /// <param name="column">
    ///   The input DataColumn specificy which row value to retrieve.
    /// </param>
    /// <param name="value">
    ///   The new row value for the specified column.
    /// </param>
    public static void SetField<T>(this DataRow row, DataColumn column, T value)
    {
      DataSetUtil.CheckArgumentNull(row, "row");
      row[column] = (object)value ?? DBNull.Value;
    }

    private static class UnboxT<T>
    {
      internal static readonly Converter<object, T> Unbox = Create(typeof(T));

      private static Converter<object, T> Create(Type type)
      {
        if (type.IsValueType)
        {
          if (type.IsGenericType && !type.IsGenericTypeDefinition && (typeof(Nullable<>) == type.GetGenericTypeDefinition()))
          {
            return (Converter<object, T>)Delegate.CreateDelegate(
                typeof(Converter<object, T>),
                    typeof(UnboxT<T>)
                        .GetMethod("NullableField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                        .MakeGenericMethod(type.GetGenericArguments()[0]));
          }
          return ValueField;
        }
        return ReferenceField;
      }

      private static T ReferenceField(object value)
      {
        return ((DBNull.Value == value) ? default(T) : (T)value);
      }

      private static T ValueField(object value)
      {
        if (DBNull.Value == value)
        {
          throw DataSetUtil.InvalidCast(typeof(T).ToString()); //Strings.DataSetLinq_NonNullableCast(typeof(T).ToString())
        }
        return (T)value;
      }

      private static Nullable<TElem> NullableField<TElem>(object value) where TElem : struct
      {
        if (DBNull.Value == value)
        {
          return default(Nullable<TElem>);
        }
        return new Nullable<TElem>((TElem)value);
      }
    }

    internal static class DataSetUtil
    {
      #region CheckArgument
      internal static void CheckArgumentNull<T>(T argumentValue, string argumentName) where T : class
      {
        if (null == argumentValue)
        {
          throw ArgumentNull(argumentName);
        }
      }
      #endregion

      #region Trace
      private static T TraceException<T>(string trace, T e)
      {
        Debug.Assert(null != e, "TraceException: null Exception");
        if (null != e)
        {
          //Bid.Trace(trace, e.ToString()); // will include callstack if permission is available
        }
        return e;
      }

      private static T TraceExceptionAsReturnValue<T>(T e)
      {
        return TraceException("<comm.ADP.TraceException|ERR|THROW> '%ls'\n", e);
      }
      #endregion

      #region new Exception
      internal static ArgumentException Argument(string message)
      {
        return TraceExceptionAsReturnValue(new ArgumentException(message));
      }

      internal static ArgumentNullException ArgumentNull(string message)
      {
        return TraceExceptionAsReturnValue(new ArgumentNullException(message));
      }

      internal static ArgumentOutOfRangeException ArgumentOutOfRange(string message, string parameterName)
      {
        return TraceExceptionAsReturnValue(new ArgumentOutOfRangeException(parameterName, message));
      }

      internal static InvalidCastException InvalidCast(string message)
      {
        return TraceExceptionAsReturnValue(new InvalidCastException(message));
      }

      internal static InvalidOperationException InvalidOperation(string message)
      {
        return TraceExceptionAsReturnValue(new InvalidOperationException(message));
      }

      internal static NotSupportedException NotSupported(string message)
      {
        return TraceExceptionAsReturnValue(new NotSupportedException(message));
      }
      #endregion

      #region new EnumerationValueNotValid
      static internal ArgumentOutOfRangeException InvalidEnumerationValue(Type type, int value)
      {
        return ArgumentOutOfRange(type.Name, value.ToString()); //Strings.DataSetLinq_InvalidEnumerationValue(type.Name, value.ToString(System.Globalization.CultureInfo.InvariantCulture)), type.Name
      }

      static internal ArgumentOutOfRangeException InvalidDataRowState(DataRowState value)
      {
#if DEBUG
        switch (value)
        {
          case DataRowState.Detached:
          case DataRowState.Unchanged:
          case DataRowState.Added:
          case DataRowState.Deleted:
          case DataRowState.Modified:
            Debug.Assert(false, "valid DataRowState " + value.ToString());
            break;
        }
#endif
        return InvalidEnumerationValue(typeof(DataRowState), (int)value);
      }

      static internal ArgumentOutOfRangeException InvalidLoadOption(LoadOption value)
      {
#if DEBUG
        switch (value)
        {
          case LoadOption.OverwriteChanges:
          case LoadOption.PreserveChanges:
          case LoadOption.Upsert:
            Debug.Assert(false, "valid LoadOption " + value.ToString());
            break;
        }
#endif
        return InvalidEnumerationValue(typeof(LoadOption), (int)value);
      }
      #endregion

      // only StackOverflowException & ThreadAbortException are sealed classes
      static private readonly Type StackOverflowType = typeof(System.StackOverflowException);
      static private readonly Type OutOfMemoryType = typeof(System.OutOfMemoryException);
      static private readonly Type ThreadAbortType = typeof(System.Threading.ThreadAbortException);
      static private readonly Type NullReferenceType = typeof(System.NullReferenceException);
      static private readonly Type AccessViolationType = typeof(System.AccessViolationException);
      static private readonly Type SecurityType = typeof(System.Security.SecurityException);

      static internal bool IsCatchableExceptionType(Exception e)
      {
        // a 'catchable' exception is defined by what it is not.
        Type type = e.GetType();

        return ((type != StackOverflowType) &&
                 (type != OutOfMemoryType) &&
                 (type != ThreadAbortType) &&
                 (type != NullReferenceType) &&
                 (type != AccessViolationType) &&
                 !SecurityType.IsAssignableFrom(type));
      }
    }
    #endregion
  }
}