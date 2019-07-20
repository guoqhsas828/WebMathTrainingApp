//// Decompiled with JetBrains decompiler
//// Type: System.Data.Common.DbProviderFactories
//// Assembly: System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
//// MVID: E2DB00CC-D2CA-4684-80B1-AD421CF4E823
//// Assembly location: C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Data.dll

//using System.Configuration;
//using System.Data.Odbc;
////using System.Data.OleDb;
//using System.Data.SqlClient;
//using System.Data.Common;
//using System.Reflection;

//namespace System.Data.Common
//  {
//    /// <summary>Represents a set of static methods for creating one or more instances of <see cref="T:System.Data.Common.DbProviderFactory" /> classes.</summary>
//    public static class DbProviderFactories
//    {
//      private static object _lockobj = new object();
//      private const string AssemblyQualifiedName = "AssemblyQualifiedName";
//      private const string Instance = "Instance";
//      private const string InvariantName = "InvariantName";
//      private const string Name = "Name";
//      private const string Description = "Description";
//      private static ConnectionState _initState;
//      private static DataTable _providerTable;

//      /// <summary>Returns an instance of a <see cref="T:System.Data.Common.DbProviderFactory" />.</summary>
//      /// <param name="providerInvariantName">Invariant name of a provider.</param>
//      /// <returns>An instance of a <see cref="T:System.Data.Common.DbProviderFactory" /> for a specified provider name.</returns>
//      public static DbProviderFactory GetFactory(string providerInvariantName)
//      {
//        //ADP.CheckArgumentLength(providerInvariantName, nameof(providerInvariantName));
//        DataTable providerTable = DbProviderFactories.GetProviderTable();
//        if (providerTable != null)
//        {
//          DataRow providerRow = providerTable.Rows.Find((object)providerInvariantName);
//          if (providerRow != null)
//            return DbProviderFactories.GetFactory(providerRow);
//        }
//      throw new Exception("ConfigProviderNotFound");
//        //throw ADP.ConfigProviderNotFound();
//      }

//      /// <summary>Returns an instance of a <see cref="T:System.Data.Common.DbProviderFactory" />.</summary>
//      /// <param name="providerRow">
//      /// <see cref="T:System.Data.DataRow" /> containing the provider's configuration information.</param>
//      /// <returns>An instance of a <see cref="T:System.Data.Common.DbProviderFactory" /> for a specified <see cref="T:System.Data.DataRow" />.</returns>
//      public static DbProviderFactory GetFactory(DataRow providerRow)
//      {
//        //ADP.CheckArgumentNull((object)providerRow, nameof(providerRow));
//        DataColumn column = providerRow.Table.Columns["AssemblyQualifiedName"];
//        if (column != null)
//        {
//          string str = providerRow[column] as string;
//          if (!String.IsNullOrEmpty(str))
//          {
//            Type type = Type.GetType(str);
//            if ((Type)null != type)
//            {
//              FieldInfo field = type.GetField("Instance", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
//              if ((FieldInfo)null != field && field.FieldType.IsSubclassOf(typeof(DbProviderFactory)))
//              {
//                object obj = field.GetValue((object)null);
//                if (obj != null)
//                  return (DbProviderFactory)obj;
//              }
//              throw new Exception("ADP.ConfigProviderInvalid()");
//            }
//            throw new Exception("ADP.ConfigProviderNotInstalled()");
//          }
//        }
//        throw new Exception("ADP.ConfigProviderMissing()");
//      }

//      ///// <summary>Returns an instance of a <see cref="T:System.Data.Common.DbProviderFactory" />.</summary>
//      ///// <param name="connection">The connection used.</param>
//      ///// <returns>An instance of a <see cref="T:System.Data.Common.DbProviderFactory" /> for a specified connection.</returns>
//      //public static DbProviderFactory GetFactory(DbConnection connection)
//      //{
//      //  ADP.CheckArgumentNull((object)connection, nameof(connection));
//      //  return connection.ProviderFactory;
//      //}

//      /// <summary>Returns a <see cref="T:System.Data.DataTable" /> that contains information about all installed providers that implement <see cref="T:System.Data.Common.DbProviderFactory" />.</summary>
//      /// <returns>Returns a <see cref="T:System.Data.DataTable" /> containing <see cref="T:System.Data.DataRow" /> objects that contain the following data. Column ordinalColumn nameDescription0
//      ///   Name
//      /// Human-readable name for the data provider.1
//      ///   Description
//      /// Human-readable description of the data provider.2
//      ///   InvariantName
//      /// Name that can be used programmatically to refer to the data provider.3
//      ///   AssemblyQualifiedName
//      /// Fully qualified name of the factory class, which contains enough information to instantiate the object.</returns>
//      //public static DataTable GetFactoryClasses()
//      //{
//      //  DataTable providerTable = DbProviderFactories.GetProviderTable();
//      //  return providerTable == null ? DbProviderFactoriesConfigurationHandler.CreateProviderDataTable() : providerTable.Copy();
//      //}

//      //private static DataTable IncludeFrameworkFactoryClasses(DataTable configDataTable)
//      //{
//      //  DataTable providerDataTable = DbProviderFactoriesConfigurationHandler.CreateProviderDataTable();
//      //  DbProviderFactoryConfigSection[] factoryConfigSectionArray = new DbProviderFactoryConfigSection[4]
//      //  {
//      //  new DbProviderFactoryConfigSection(typeof (OdbcFactory), "Odbc Data Provider", ".Net Framework Data Provider for Odbc"),
//      //  new DbProviderFactoryConfigSection(typeof (OleDbFactory), "OleDb Data Provider", ".Net Framework Data Provider for OleDb"),
//      //  new DbProviderFactoryConfigSection("OracleClient Data Provider", "System.Data.OracleClient", ".Net Framework Data Provider for Oracle", typeof (SqlClientFactory).AssemblyQualifiedName.ToString().Replace("System.Data.SqlClient.SqlClientFactory, System.Data,", "System.Data.OracleClient.OracleClientFactory, System.Data.OracleClient,")),
//      //  new DbProviderFactoryConfigSection(typeof (SqlClientFactory), "SqlClient Data Provider", ".Net Framework Data Provider for SqlServer")
//      //  };
//      //  for (int index = 0; index < factoryConfigSectionArray.Length; ++index)
//      //  {
//      //    if (!factoryConfigSectionArray[index].IsNull())
//      //    {
//      //      bool flag = false;
//      //      if (index == 2)
//      //      {
//      //        Type type = Type.GetType(factoryConfigSectionArray[index].AssemblyQualifiedName);
//      //        if (type != (Type)null)
//      //        {
//      //          FieldInfo field = type.GetField("Instance", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
//      //          if ((FieldInfo)null != field && field.FieldType.IsSubclassOf(typeof(DbProviderFactory)) && field.GetValue((object)null) != null)
//      //            flag = true;
//      //        }
//      //      }
//      //      else
//      //        flag = true;
//      //      if (flag)
//      //      {
//      //        DataRow row = providerDataTable.NewRow();
//      //        row["Name"] = (object)factoryConfigSectionArray[index].Name;
//      //        row["InvariantName"] = (object)factoryConfigSectionArray[index].InvariantName;
//      //        row["Description"] = (object)factoryConfigSectionArray[index].Description;
//      //        row["AssemblyQualifiedName"] = (object)factoryConfigSectionArray[index].AssemblyQualifiedName;
//      //        providerDataTable.Rows.Add(row);
//      //      }
//      //    }
//      //  }
//      //  int index1 = 0;
//      //  while (configDataTable != null)
//      //  {
//      //    if (index1 < configDataTable.Rows.Count)
//      //    {
//      //      try
//      //      {
//      //        bool flag = false;
//      //        if (configDataTable.Rows[index1]["AssemblyQualifiedName"].ToString().ToLowerInvariant().Contains("System.Data.OracleClient".ToString().ToLowerInvariant()))
//      //        {
//      //          Type type = Type.GetType(configDataTable.Rows[index1]["AssemblyQualifiedName"].ToString());
//      //          if (type != (Type)null)
//      //          {
//      //            FieldInfo field = type.GetField("Instance", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
//      //            if ((FieldInfo)null != field && field.FieldType.IsSubclassOf(typeof(DbProviderFactory)) && field.GetValue((object)null) != null)
//      //              flag = true;
//      //          }
//      //        }
//      //        else
//      //          flag = true;
//      //        if (flag)
//      //          providerDataTable.Rows.Add(configDataTable.Rows[index1].ItemArray);
//      //      }
//      //      catch (ConstraintException ex)
//      //      {
//      //      }
//      //      ++index1;
//      //    }
//      //    else
//      //      break;
//      //  }
//      //  return providerDataTable;
//      //}

//      private static DataTable GetProviderTable()
//      {
//        DbProviderFactories.Initialize();
//        return DbProviderFactories._providerTable;
//      }

//      private static void Initialize()
//      {
//        if (ConnectionState.Open == DbProviderFactories._initState)
//          return;
//        lock (DbProviderFactories._lockobj)
//        {
//          switch (DbProviderFactories._initState)
//          {
//            case ConnectionState.Closed:
//              DbProviderFactories._initState = ConnectionState.Connecting;
//              try
//              {
//                DataSet section = PrivilegedConfigurationManager.GetSection("system.data") as DataSet;
//                DbProviderFactories._providerTable = section != null ? DbProviderFactories.IncludeFrameworkFactoryClasses(section.Tables[nameof(DbProviderFactories)]) : DbProviderFactories.IncludeFrameworkFactoryClasses((DataTable)null);
//                break;
//              }
//              finally
//              {
//                DbProviderFactories._initState = ConnectionState.Open;
//              }
//          }
//        }
//      }
//    }

//  //internal static class ADP
//  //{
//  //  private static Task<bool> _trueTask = (Task<bool>)null;
//  //  private static Task<bool> _falseTask = (Task<bool>)null;
//  //  private static readonly Type StackOverflowType = typeof(StackOverflowException);
//  //  private static readonly Type OutOfMemoryType = typeof(OutOfMemoryException);
//  //  private static readonly Type ThreadAbortType = typeof(ThreadAbortException);
//  //  private static readonly Type NullReferenceType = typeof(NullReferenceException);
//  //  private static readonly Type AccessViolationType = typeof(AccessViolationException);
//  //  private static readonly Type SecurityType = typeof(SecurityException);
//  //  internal static readonly string StrEmpty = "";
//  //  internal static readonly IntPtr PtrZero = new IntPtr(0);
//  //  internal static readonly int PtrSize = IntPtr.Size;
//  //  internal static readonly IntPtr InvalidPtr = new IntPtr(-1);
//  //  internal static readonly IntPtr RecordsUnaffected = new IntPtr(-1);
//  //  internal static readonly HandleRef NullHandleRef = new HandleRef((object)null, IntPtr.Zero);
//  //  internal static readonly bool IsWindowsNT = PlatformID.Win32NT == Environment.OSVersion.Platform;
//  //  internal static readonly bool IsPlatformNT5 = ADP.IsWindowsNT && Environment.OSVersion.Version.Major >= 5;
//  //  internal static readonly string[] AzureSqlServerEndpoints = new string[4]
//  //  {
//  //    System.Data.Res.GetString("AZURESQL_GenericEndpoint"),
//  //    System.Data.Res.GetString("AZURESQL_GermanEndpoint"),
//  //    System.Data.Res.GetString("AZURESQL_UsGovEndpoint"),
//  //    System.Data.Res.GetString("AZURESQL_ChinaEndpoint")
//  //  };
//  //  internal const string Append = "Append";
//  //  internal const string BeginExecuteNonQuery = "BeginExecuteNonQuery";
//  //  internal const string BeginExecuteReader = "BeginExecuteReader";
//  //  internal const string BeginTransaction = "BeginTransaction";
//  //  internal const string BeginExecuteXmlReader = "BeginExecuteXmlReader";
//  //  internal const string ChangeDatabase = "ChangeDatabase";
//  //  internal const string Cancel = "Cancel";
//  //  internal const string Clone = "Clone";
//  //  internal const string ColumnEncryptionSystemProviderNamePrefix = "MSSQL_";
//  //  internal const string CommitTransaction = "CommitTransaction";
//  //  internal const string CommandTimeout = "CommandTimeout";
//  //  internal const string ConnectionString = "ConnectionString";
//  //  internal const string DataSetColumn = "DataSetColumn";
//  //  internal const string DataSetTable = "DataSetTable";
//  //  internal const string Delete = "Delete";
//  //  internal const string DeleteCommand = "DeleteCommand";
//  //  internal const string DeriveParameters = "DeriveParameters";
//  //  internal const string EndExecuteNonQuery = "EndExecuteNonQuery";
//  //  internal const string EndExecuteReader = "EndExecuteReader";
//  //  internal const string EndExecuteXmlReader = "EndExecuteXmlReader";
//  //  internal const string ExecuteReader = "ExecuteReader";
//  //  internal const string ExecuteRow = "ExecuteRow";
//  //  internal const string ExecuteNonQuery = "ExecuteNonQuery";
//  //  internal const string ExecuteScalar = "ExecuteScalar";
//  //  internal const string ExecuteSqlScalar = "ExecuteSqlScalar";
//  //  internal const string ExecuteXmlReader = "ExecuteXmlReader";
//  //  internal const string Fill = "Fill";
//  //  internal const string FillPage = "FillPage";
//  //  internal const string FillSchema = "FillSchema";
//  //  internal const string GetBytes = "GetBytes";
//  //  internal const string GetChars = "GetChars";
//  //  internal const string GetOleDbSchemaTable = "GetOleDbSchemaTable";
//  //  internal const string GetProperties = "GetProperties";
//  //  internal const string GetSchema = "GetSchema";
//  //  internal const string GetSchemaTable = "GetSchemaTable";
//  //  internal const string GetServerTransactionLevel = "GetServerTransactionLevel";
//  //  internal const string Insert = "Insert";
//  //  internal const string Open = "Open";
//  //  internal const string Parameter = "Parameter";
//  //  internal const string ParameterBuffer = "buffer";
//  //  internal const string ParameterCount = "count";
//  //  internal const string ParameterDestinationType = "destinationType";
//  //  internal const string ParameterIndex = "index";
//  //  internal const string ParameterName = "ParameterName";
//  //  internal const string ParameterOffset = "offset";
//  //  internal const string ParameterSetPosition = "set_Position";
//  //  internal const string ParameterService = "Service";
//  //  internal const string ParameterTimeout = "Timeout";
//  //  internal const string ParameterUserData = "UserData";
//  //  internal const string Prepare = "Prepare";
//  //  internal const string QuoteIdentifier = "QuoteIdentifier";
//  //  internal const string Read = "Read";
//  //  internal const string ReadAsync = "ReadAsync";
//  //  internal const string Remove = "Remove";
//  //  internal const string RollbackTransaction = "RollbackTransaction";
//  //  internal const string SaveTransaction = "SaveTransaction";
//  //  internal const string SetProperties = "SetProperties";
//  //  internal const string SourceColumn = "SourceColumn";
//  //  internal const string SourceVersion = "SourceVersion";
//  //  internal const string SourceTable = "SourceTable";
//  //  internal const string UnquoteIdentifier = "UnquoteIdentifier";
//  //  internal const string Update = "Update";
//  //  internal const string UpdateCommand = "UpdateCommand";
//  //  internal const string UpdateRows = "UpdateRows";
//  //  internal const CompareOptions compareOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth;
//  //  internal const int DecimalMaxPrecision = 29;
//  //  internal const int DecimalMaxPrecision28 = 28;
//  //  internal const int DefaultCommandTimeout = 30;
//  //  internal const int DefaultConnectionTimeout = 15;
//  //  internal const float FailoverTimeoutStep = 0.08f;
//  //  internal const float FailoverTimeoutStepForTnir = 0.125f;
//  //  internal const int MinimumTimeoutForTnirMs = 500;
//  //  internal const int CharSize = 2;
//  //  private const string hexDigits = "0123456789abcdef";
//  //  private static Version _systemDataVersion;

//  //  internal static Task<T> CreatedTaskWithException<T>(Exception ex)
//  //  {
//  //    TaskCompletionSource<T> completionSource = new TaskCompletionSource<T>();
//  //    completionSource.SetException(ex);
//  //    return completionSource.Task;
//  //  }

//  //  internal static Task<T> CreatedTaskWithCancellation<T>()
//  //  {
//  //    TaskCompletionSource<T> completionSource = new TaskCompletionSource<T>();
//  //    completionSource.SetCanceled();
//  //    return completionSource.Task;
//  //  }

//  //  internal static Exception ExceptionWithStackTrace(Exception e)
//  //  {
//  //    try
//  //    {
//  //      throw e;
//  //    }
//  //    catch (Exception ex)
//  //    {
//  //      return ex;
//  //    }
//  //  }

//  //  internal static Task<bool> TrueTask
//  //  {
//  //    get
//  //    {
//  //      if (ADP._trueTask == null)
//  //        ADP._trueTask = Task.FromResult<bool>(true);
//  //      return ADP._trueTask;
//  //    }
//  //  }

//  //  internal static Task<bool> FalseTask
//  //  {
//  //    get
//  //    {
//  //      if (ADP._falseTask == null)
//  //        ADP._falseTask = Task.FromResult<bool>(false);
//  //      return ADP._falseTask;
//  //    }
//  //  }

//  //  private static void TraceException(string trace, Exception e)
//  //  {
//  //    if (e == null)
//  //      return;
//  //    Bid.Trace(trace, e.ToString());
//  //  }

//  //  internal static void TraceExceptionAsReturnValue(Exception e)
//  //  {
//  //    ADP.TraceException("<comm.ADP.TraceException|ERR|THROW> '%ls'\n", e);
//  //  }

//  //  internal static void TraceExceptionForCapture(Exception e)
//  //  {
//  //    ADP.TraceException("<comm.ADP.TraceException|ERR|CATCH> '%ls'\n", e);
//  //  }

//  //  internal static void TraceExceptionWithoutRethrow(Exception e)
//  //  {
//  //    ADP.TraceException("<comm.ADP.TraceException|ERR|CATCH> '%ls'\n", e);
//  //  }

//  //  internal static ArgumentException Argument(string error)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException Argument(string error, Exception inner)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(error, inner);
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException Argument(string error, string parameter)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(error, parameter);
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException Argument(string error, string parameter, Exception inner)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(error, parameter, inner);
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentNullException ArgumentNull(string parameter)
//  //  {
//  //    ArgumentNullException argumentNullException = new ArgumentNullException(parameter);
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentNullException);
//  //    return argumentNullException;
//  //  }

//  //  internal static ArgumentNullException ArgumentNull(string parameter, string error)
//  //  {
//  //    ArgumentNullException argumentNullException = new ArgumentNullException(parameter, error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentNullException);
//  //    return argumentNullException;
//  //  }

//  //  internal static ArgumentOutOfRangeException ArgumentOutOfRange(string parameterName)
//  //  {
//  //    ArgumentOutOfRangeException ofRangeException = new ArgumentOutOfRangeException(parameterName);
//  //    ADP.TraceExceptionAsReturnValue((Exception)ofRangeException);
//  //    return ofRangeException;
//  //  }

//  //  internal static ArgumentOutOfRangeException ArgumentOutOfRange(string message, string parameterName)
//  //  {
//  //    ArgumentOutOfRangeException ofRangeException = new ArgumentOutOfRangeException(parameterName, message);
//  //    ADP.TraceExceptionAsReturnValue((Exception)ofRangeException);
//  //    return ofRangeException;
//  //  }

//  //  internal static ArgumentOutOfRangeException ArgumentOutOfRange(string message, string parameterName, object value)
//  //  {
//  //    ArgumentOutOfRangeException ofRangeException = new ArgumentOutOfRangeException(parameterName, value, message);
//  //    ADP.TraceExceptionAsReturnValue((Exception)ofRangeException);
//  //    return ofRangeException;
//  //  }

//  //  internal static ConfigurationException Configuration(string message)
//  //  {
//  //    ConfigurationException configurationException = (ConfigurationException)new ConfigurationErrorsException(message);
//  //    ADP.TraceExceptionAsReturnValue((Exception)configurationException);
//  //    return configurationException;
//  //  }

//  //  internal static ConfigurationException Configuration(string message, XmlNode node)
//  //  {
//  //    ConfigurationException configurationException = (ConfigurationException)new ConfigurationErrorsException(message, node);
//  //    ADP.TraceExceptionAsReturnValue((Exception)configurationException);
//  //    return configurationException;
//  //  }

//  //  internal static DataException Data(string message)
//  //  {
//  //    DataException dataException = new DataException(message);
//  //    ADP.TraceExceptionAsReturnValue((Exception)dataException);
//  //    return dataException;
//  //  }

//  //  internal static IndexOutOfRangeException IndexOutOfRange(int value)
//  //  {
//  //    IndexOutOfRangeException ofRangeException = new IndexOutOfRangeException(value.ToString((IFormatProvider)CultureInfo.InvariantCulture));
//  //    ADP.TraceExceptionAsReturnValue((Exception)ofRangeException);
//  //    return ofRangeException;
//  //  }

//  //  internal static IndexOutOfRangeException IndexOutOfRange(string error)
//  //  {
//  //    IndexOutOfRangeException ofRangeException = new IndexOutOfRangeException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)ofRangeException);
//  //    return ofRangeException;
//  //  }

//  //  internal static IndexOutOfRangeException IndexOutOfRange()
//  //  {
//  //    IndexOutOfRangeException ofRangeException = new IndexOutOfRangeException();
//  //    ADP.TraceExceptionAsReturnValue((Exception)ofRangeException);
//  //    return ofRangeException;
//  //  }

//  //  internal static InvalidCastException InvalidCast(string error)
//  //  {
//  //    return ADP.InvalidCast(error, (Exception)null);
//  //  }

//  //  internal static InvalidCastException InvalidCast(string error, Exception inner)
//  //  {
//  //    InvalidCastException invalidCastException = new InvalidCastException(error, inner);
//  //    ADP.TraceExceptionAsReturnValue((Exception)invalidCastException);
//  //    return invalidCastException;
//  //  }

//  //  internal static InvalidOperationException InvalidOperation(string error)
//  //  {
//  //    InvalidOperationException operationException = new InvalidOperationException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)operationException);
//  //    return operationException;
//  //  }

//  //  internal static TimeoutException TimeoutException(string error)
//  //  {
//  //    TimeoutException timeoutException = new TimeoutException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)timeoutException);
//  //    return timeoutException;
//  //  }

//  //  internal static InvalidOperationException InvalidOperation(string error, Exception inner)
//  //  {
//  //    InvalidOperationException operationException = new InvalidOperationException(error, inner);
//  //    ADP.TraceExceptionAsReturnValue((Exception)operationException);
//  //    return operationException;
//  //  }

//  //  internal static NotImplementedException NotImplemented(string error)
//  //  {
//  //    NotImplementedException implementedException = new NotImplementedException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)implementedException);
//  //    return implementedException;
//  //  }

//  //  internal static NotSupportedException NotSupported()
//  //  {
//  //    NotSupportedException supportedException = new NotSupportedException();
//  //    ADP.TraceExceptionAsReturnValue((Exception)supportedException);
//  //    return supportedException;
//  //  }

//  //  internal static NotSupportedException NotSupported(string error)
//  //  {
//  //    NotSupportedException supportedException = new NotSupportedException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)supportedException);
//  //    return supportedException;
//  //  }

//  //  internal static OverflowException Overflow(string error)
//  //  {
//  //    return ADP.Overflow(error, (Exception)null);
//  //  }

//  //  internal static OverflowException Overflow(string error, Exception inner)
//  //  {
//  //    OverflowException overflowException = new OverflowException(error, inner);
//  //    ADP.TraceExceptionAsReturnValue((Exception)overflowException);
//  //    return overflowException;
//  //  }

//  //  internal static PlatformNotSupportedException PropertyNotSupported(string property)
//  //  {
//  //    PlatformNotSupportedException supportedException = new PlatformNotSupportedException(System.Data.Res.GetString("ADP_PropertyNotSupported", new object[1]
//  //    {
//  //      (object) property
//  //    }));
//  //    ADP.TraceExceptionAsReturnValue((Exception)supportedException);
//  //    return supportedException;
//  //  }

//  //  internal static TypeLoadException TypeLoad(string error)
//  //  {
//  //    TypeLoadException typeLoadException = new TypeLoadException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)typeLoadException);
//  //    return typeLoadException;
//  //  }

//  //  internal static InvalidCastException InvalidCast()
//  //  {
//  //    InvalidCastException invalidCastException = new InvalidCastException();
//  //    ADP.TraceExceptionAsReturnValue((Exception)invalidCastException);
//  //    return invalidCastException;
//  //  }

//  //  internal static IOException IO(string error)
//  //  {
//  //    IOException ioException = new IOException(error);
//  //    ADP.TraceExceptionAsReturnValue((Exception)ioException);
//  //    return ioException;
//  //  }

//  //  internal static IOException IO(string error, Exception inner)
//  //  {
//  //    IOException ioException = new IOException(error, inner);
//  //    ADP.TraceExceptionAsReturnValue((Exception)ioException);
//  //    return ioException;
//  //  }

//  //  internal static InvalidOperationException DataAdapter(string error)
//  //  {
//  //    return ADP.InvalidOperation(error);
//  //  }

//  //  internal static InvalidOperationException DataAdapter(string error, Exception inner)
//  //  {
//  //    return ADP.InvalidOperation(error, inner);
//  //  }

//  //  private static InvalidOperationException Provider(string error)
//  //  {
//  //    return ADP.InvalidOperation(error);
//  //  }

//  //  internal static ObjectDisposedException ObjectDisposed(object instance)
//  //  {
//  //    ObjectDisposedException disposedException = new ObjectDisposedException(instance.GetType().Name);
//  //    ADP.TraceExceptionAsReturnValue((Exception)disposedException);
//  //    return disposedException;
//  //  }

//  //  internal static InvalidOperationException MethodCalledTwice(string method)
//  //  {
//  //    InvalidOperationException operationException = new InvalidOperationException(System.Data.Res.GetString("ADP_CalledTwice", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //    ADP.TraceExceptionAsReturnValue((Exception)operationException);
//  //    return operationException;
//  //  }

//  //  internal static ArgumentException IncorrectAsyncResult()
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_IncorrectAsyncResult"), "AsyncResult");
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException SingleValuedProperty(string propertyName, string value)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_SingleValuedProperty", (object)propertyName, (object)value));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException DoubleValuedProperty(string propertyName, string value1, string value2)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_DoubleValuedProperty", (object)propertyName, (object)value1, (object)value2));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException InvalidPrefixSuffix()
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_InvalidPrefixSuffix"));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException InvalidMultipartName(string property, string value)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_InvalidMultipartName", (object)System.Data.Res.GetString(property), (object)value));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException InvalidMultipartNameIncorrectUsageOfQuotes(string property, string value)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_InvalidMultipartNameQuoteUsage", (object)System.Data.Res.GetString(property), (object)value));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException InvalidMultipartNameToManyParts(string property, string value, int limit)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_InvalidMultipartNameToManyParts", (object)System.Data.Res.GetString(property), (object)value, (object)limit));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException BadParameterName(string parameterName)
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_BadParameterName", new object[1]
//  //    {
//  //      (object) parameterName
//  //    }));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static ArgumentException MultipleReturnValue()
//  //  {
//  //    ArgumentException argumentException = new ArgumentException(System.Data.Res.GetString("ADP_MultipleReturnValue"));
//  //    ADP.TraceExceptionAsReturnValue((Exception)argumentException);
//  //    return argumentException;
//  //  }

//  //  internal static void CheckArgumentLength(string value, string parameterName)
//  //  {
//  //    ADP.CheckArgumentNull((object)value, parameterName);
//  //    if (value.Length == 0)
//  //      throw ADP.Argument(System.Data.Res.GetString("ADP_EmptyString", new object[1]
//  //      {
//  //        (object) parameterName
//  //      }));
//  //  }

//  //  internal static void CheckArgumentLength(Array value, string parameterName)
//  //  {
//  //    ADP.CheckArgumentNull((object)value, parameterName);
//  //    if (value.Length == 0)
//  //      throw ADP.Argument(System.Data.Res.GetString("ADP_EmptyArray", new object[1]
//  //      {
//  //        (object) parameterName
//  //      }));
//  //  }

//  //  internal static void CheckArgumentNull(object value, string parameterName)
//  //  {
//  //    if (value == null)
//  //      throw ADP.ArgumentNull(parameterName);
//  //  }

//  //  internal static bool IsCatchableExceptionType(Exception e)
//  //  {
//  //    Type type = e.GetType();
//  //    if (type != ADP.StackOverflowType && type != ADP.OutOfMemoryType && (type != ADP.ThreadAbortType && type != ADP.NullReferenceType) && type != ADP.AccessViolationType)
//  //      return !ADP.SecurityType.IsAssignableFrom(type);
//  //    return false;
//  //  }

//  //  internal static bool IsCatchableOrSecurityExceptionType(Exception e)
//  //  {
//  //    Type type = e.GetType();
//  //    if (type != ADP.StackOverflowType && type != ADP.OutOfMemoryType && (type != ADP.ThreadAbortType && type != ADP.NullReferenceType))
//  //      return type != ADP.AccessViolationType;
//  //    return false;
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidEnumerationValue(Type type, int value)
//  //  {
//  //    return ADP.ArgumentOutOfRange(System.Data.Res.GetString("ADP_InvalidEnumerationValue", (object)type.Name, (object)value.ToString((IFormatProvider)CultureInfo.InvariantCulture)), type.Name);
//  //  }

//  //  internal static ArgumentOutOfRangeException NotSupportedEnumerationValue(Type type, string value, string method)
//  //  {
//  //    return ADP.ArgumentOutOfRange(System.Data.Res.GetString("ADP_NotSupportedEnumerationValue", (object)type.Name, (object)value, (object)method), type.Name);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidAcceptRejectRule(AcceptRejectRule value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(AcceptRejectRule), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidCatalogLocation(CatalogLocation value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(CatalogLocation), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidCommandBehavior(CommandBehavior value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(CommandBehavior), (int)value);
//  //  }

//  //  internal static void ValidateCommandBehavior(CommandBehavior value)
//  //  {
//  //    if (value < CommandBehavior.Default || (CommandBehavior.SingleResult | CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection) < value)
//  //      throw ADP.InvalidCommandBehavior(value);
//  //  }

//  //  internal static ArgumentException InvalidArgumentLength(string argumentName, int limit)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidArgumentLength", (object)argumentName, (object)limit));
//  //  }

//  //  internal static ArgumentException MustBeReadOnly(string argumentName)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_MustBeReadOnly", new object[1]
//  //    {
//  //      (object) argumentName
//  //    }));
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidCommandType(CommandType value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(CommandType), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidConflictOptions(ConflictOption value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(ConflictOption), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidDataRowState(DataRowState value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(DataRowState), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidDataRowVersion(DataRowVersion value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(DataRowVersion), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidIsolationLevel(System.Data.IsolationLevel value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(System.Data.IsolationLevel), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidKeyRestrictionBehavior(KeyRestrictionBehavior value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(KeyRestrictionBehavior), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidLoadOption(LoadOption value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(LoadOption), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidMissingMappingAction(MissingMappingAction value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(MissingMappingAction), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidMissingSchemaAction(MissingSchemaAction value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(MissingSchemaAction), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidParameterDirection(ParameterDirection value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(ParameterDirection), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidPermissionState(PermissionState value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(PermissionState), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidRule(Rule value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(Rule), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidSchemaType(SchemaType value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(SchemaType), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidStatementType(StatementType value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(StatementType), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidUpdateRowSource(UpdateRowSource value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(UpdateRowSource), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidUpdateStatus(UpdateStatus value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(UpdateStatus), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException NotSupportedCommandBehavior(CommandBehavior value, string method)
//  //  {
//  //    return ADP.NotSupportedEnumerationValue(typeof(CommandBehavior), value.ToString(), method);
//  //  }

//  //  internal static ArgumentOutOfRangeException NotSupportedStatementType(StatementType value, string method)
//  //  {
//  //    return ADP.NotSupportedEnumerationValue(typeof(StatementType), value.ToString(), method);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidUserDefinedTypeSerializationFormat(Format value)
//  //  {
//  //    return ADP.InvalidEnumerationValue(typeof(Format), (int)value);
//  //  }

//  //  internal static ArgumentOutOfRangeException NotSupportedUserDefinedTypeSerializationFormat(Format value, string method)
//  //  {
//  //    return ADP.NotSupportedEnumerationValue(typeof(Format), value.ToString(), method);
//  //  }

//  //  internal static ArgumentException ConfigProviderNotFound()
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString(nameof(ConfigProviderNotFound)));
//  //  }

//  //  internal static InvalidOperationException ConfigProviderInvalid()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString(nameof(ConfigProviderInvalid)));
//  //  }

//  //  internal static ConfigurationException ConfigProviderNotInstalled()
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigProviderNotInstalled)));
//  //  }

//  //  internal static ConfigurationException ConfigProviderMissing()
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigProviderMissing)));
//  //  }

//  //  internal static ConfigurationException ConfigBaseNoChildNodes(XmlNode node)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigBaseNoChildNodes)), node);
//  //  }

//  //  internal static ConfigurationException ConfigBaseElementsOnly(XmlNode node)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigBaseElementsOnly)), node);
//  //  }

//  //  internal static ConfigurationException ConfigUnrecognizedAttributes(XmlNode node)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigUnrecognizedAttributes), new object[1]
//  //    {
//  //      (object) node.Attributes[0].Name
//  //    }), node);
//  //  }

//  //  internal static ConfigurationException ConfigUnrecognizedElement(XmlNode node)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigUnrecognizedElement)), node);
//  //  }

//  //  internal static ConfigurationException ConfigSectionsUnique(string sectionName)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigSectionsUnique), new object[1]
//  //    {
//  //      (object) sectionName
//  //    }));
//  //  }

//  //  internal static ConfigurationException ConfigRequiredAttributeMissing(string name, XmlNode node)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigRequiredAttributeMissing), new object[1]
//  //    {
//  //      (object) name
//  //    }), node);
//  //  }

//  //  internal static ConfigurationException ConfigRequiredAttributeEmpty(string name, XmlNode node)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString(nameof(ConfigRequiredAttributeEmpty), new object[1]
//  //    {
//  //      (object) name
//  //    }), node);
//  //  }

//  //  internal static ArgumentException ConnectionStringSyntax(int index)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_ConnectionStringSyntax", new object[1]
//  //    {
//  //      (object) index
//  //    }));
//  //  }

//  //  internal static ArgumentException KeywordNotSupported(string keyword)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_KeywordNotSupported", new object[1]
//  //    {
//  //      (object) keyword
//  //    }));
//  //  }

//  //  internal static ArgumentException UdlFileError(Exception inner)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_UdlFileError"), inner);
//  //  }

//  //  internal static ArgumentException InvalidUDL()
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidUDL"));
//  //  }

//  //  internal static InvalidOperationException InvalidDataDirectory()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidDataDirectory"));
//  //  }

//  //  internal static ArgumentException InvalidKeyname(string parameterName)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidKey"), parameterName);
//  //  }

//  //  internal static ArgumentException InvalidValue(string parameterName)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidValue"), parameterName);
//  //  }

//  //  internal static ArgumentException InvalidMinMaxPoolSizeValues()
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidMinMaxPoolSizeValues"));
//  //  }

//  //  internal static ArgumentException ConvertFailed(Type fromType, Type toType, Exception innerException)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("SqlConvert_ConvertFailed", (object)fromType.FullName, (object)toType.FullName), innerException);
//  //  }

//  //  internal static InvalidOperationException InvalidMixedUsageOfSecureAndClearCredential()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfSecureAndClearCredential"));
//  //  }

//  //  internal static ArgumentException InvalidMixedArgumentOfSecureAndClearCredential()
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidMixedUsageOfSecureAndClearCredential"));
//  //  }

//  //  internal static InvalidOperationException InvalidMixedUsageOfSecureCredentialAndIntegratedSecurity()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfSecureCredentialAndIntegratedSecurity"));
//  //  }

//  //  internal static ArgumentException InvalidMixedArgumentOfSecureCredentialAndIntegratedSecurity()
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidMixedUsageOfSecureCredentialAndIntegratedSecurity"));
//  //  }

//  //  internal static InvalidOperationException InvalidMixedUsageOfSecureCredentialAndContextConnection()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfSecureCredentialAndContextConnection"));
//  //  }

//  //  internal static ArgumentException InvalidMixedArgumentOfSecureCredentialAndContextConnection()
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidMixedUsageOfSecureCredentialAndContextConnection"));
//  //  }

//  //  internal static InvalidOperationException InvalidMixedUsageOfAccessTokenAndContextConnection()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfAccessTokenAndContextConnection"));
//  //  }

//  //  internal static InvalidOperationException InvalidMixedUsageOfAccessTokenAndIntegratedSecurity()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfAccessTokenAndIntegratedSecurity"));
//  //  }

//  //  internal static InvalidOperationException InvalidMixedUsageOfAccessTokenAndUserIDPassword()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfAccessTokenAndUserIDPassword"));
//  //  }

//  //  internal static Exception InvalidMixedUsageOfAccessTokenAndCredential()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfAccessTokenAndCredential"));
//  //  }

//  //  internal static Exception InvalidMixedUsageOfAccessTokenAndAuthentication()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfAccessTokenAndAuthentication"));
//  //  }

//  //  internal static Exception InvalidMixedUsageOfCredentialAndAccessToken()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMixedUsageOfCredentialAndAccessToken"));
//  //  }

//  //  internal static InvalidOperationException NoConnectionString()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_NoConnectionString"));
//  //  }

//  //  internal static NotImplementedException MethodNotImplemented(string methodName)
//  //  {
//  //    NotImplementedException implementedException = new NotImplementedException(methodName);
//  //    ADP.TraceExceptionAsReturnValue((Exception)implementedException);
//  //    return implementedException;
//  //  }

//  //  private static string ConnectionStateMsg(ConnectionState state)
//  //  {
//  //    switch (state)
//  //    {
//  //      case ConnectionState.Closed:
//  //      case ConnectionState.Connecting | ConnectionState.Broken:
//  //        return System.Data.Res.GetString("ADP_ConnectionStateMsg_Closed");
//  //      case ConnectionState.Open:
//  //        return System.Data.Res.GetString("ADP_ConnectionStateMsg_Open");
//  //      case ConnectionState.Connecting:
//  //        return System.Data.Res.GetString("ADP_ConnectionStateMsg_Connecting");
//  //      case ConnectionState.Open | ConnectionState.Executing:
//  //        return System.Data.Res.GetString("ADP_ConnectionStateMsg_OpenExecuting");
//  //      case ConnectionState.Open | ConnectionState.Fetching:
//  //        return System.Data.Res.GetString("ADP_ConnectionStateMsg_OpenFetching");
//  //      default:
//  //        return System.Data.Res.GetString("ADP_ConnectionStateMsg", new object[1]
//  //        {
//  //          (object) state.ToString()
//  //        });
//  //    }
//  //  }

//  //  internal static ConfigurationException ConfigUnableToLoadXmlMetaDataFile(string settingName)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString("OleDb_ConfigUnableToLoadXmlMetaDataFile", new object[1]
//  //    {
//  //      (object) settingName
//  //    }));
//  //  }

//  //  internal static ConfigurationException ConfigWrongNumberOfValues(string settingName)
//  //  {
//  //    return ADP.Configuration(System.Data.Res.GetString("OleDb_ConfigWrongNumberOfValues", new object[1]
//  //    {
//  //      (object) settingName
//  //    }));
//  //  }

//  //  internal static Exception InvalidConnectionOptionValue(string key)
//  //  {
//  //    return ADP.InvalidConnectionOptionValue(key, (Exception)null);
//  //  }

//  //  internal static Exception InvalidConnectionOptionValueLength(string key, int limit)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidConnectionOptionValueLength", (object)key, (object)limit));
//  //  }

//  //  internal static Exception InvalidConnectionOptionValue(string key, Exception inner)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidConnectionOptionValue", new object[1]
//  //    {
//  //      (object) key
//  //    }), inner);
//  //  }

//  //  internal static Exception MissingConnectionOptionValue(string key, string requiredAdditionalKey)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_MissingConnectionOptionValue", (object)key, (object)requiredAdditionalKey));
//  //  }

//  //  internal static Exception InvalidXMLBadVersion()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidXMLBadVersion"));
//  //  }

//  //  internal static Exception NotAPermissionElement()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_NotAPermissionElement"));
//  //  }

//  //  internal static Exception PermissionTypeMismatch()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_PermissionTypeMismatch"));
//  //  }

//  //  internal static Exception WrongType(Type got, Type expected)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("SQL_WrongType", (object)got.ToString(), (object)expected.ToString()));
//  //  }

//  //  internal static Exception OdbcNoTypesFromProvider()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_OdbcNoTypesFromProvider"));
//  //  }

//  //  internal static Exception PooledOpenTimeout()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_PooledOpenTimeout"));
//  //  }

//  //  internal static Exception NonPooledOpenTimeout()
//  //  {
//  //    return (Exception)ADP.TimeoutException(System.Data.Res.GetString("ADP_NonPooledOpenTimeout"));
//  //  }

//  //  internal static ArgumentException CollectionRemoveInvalidObject(Type itemType, ICollection collection)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_CollectionRemoveInvalidObject", (object)itemType.Name, (object)collection.GetType().Name));
//  //  }

//  //  internal static ArgumentNullException CollectionNullValue(string parameter, Type collection, Type itemType)
//  //  {
//  //    return ADP.ArgumentNull(parameter, System.Data.Res.GetString("ADP_CollectionNullValue", (object)collection.Name, (object)itemType.Name));
//  //  }

//  //  internal static IndexOutOfRangeException CollectionIndexInt32(int index, Type collection, int count)
//  //  {
//  //    return ADP.IndexOutOfRange(System.Data.Res.GetString("ADP_CollectionIndexInt32", (object)index.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)collection.Name, (object)count.ToString((IFormatProvider)CultureInfo.InvariantCulture)));
//  //  }

//  //  internal static IndexOutOfRangeException CollectionIndexString(Type itemType, string propertyName, string propertyValue, Type collection)
//  //  {
//  //    return ADP.IndexOutOfRange(System.Data.Res.GetString("ADP_CollectionIndexString", (object)itemType.Name, (object)propertyName, (object)propertyValue, (object)collection.Name));
//  //  }

//  //  internal static InvalidCastException CollectionInvalidType(Type collection, Type itemType, object invalidValue)
//  //  {
//  //    return ADP.InvalidCast(System.Data.Res.GetString("ADP_CollectionInvalidType", (object)collection.Name, (object)itemType.Name, (object)invalidValue.GetType().Name));
//  //  }

//  //  internal static Exception CollectionUniqueValue(Type itemType, string propertyName, string propertyValue)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_CollectionUniqueValue", (object)itemType.Name, (object)propertyName, (object)propertyValue));
//  //  }

//  //  internal static ArgumentException ParametersIsNotParent(Type parameterType, ICollection collection)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_CollectionIsNotParent", (object)parameterType.Name, (object)collection.GetType().Name));
//  //  }

//  //  internal static ArgumentException ParametersIsParent(Type parameterType, ICollection collection)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_CollectionIsNotParent", (object)parameterType.Name, (object)collection.GetType().Name));
//  //  }

//  //  internal static InvalidOperationException TransactionConnectionMismatch()
//  //  {
//  //    return ADP.Provider(System.Data.Res.GetString("ADP_TransactionConnectionMismatch"));
//  //  }

//  //  internal static InvalidOperationException TransactionCompletedButNotDisposed()
//  //  {
//  //    return ADP.Provider(System.Data.Res.GetString("ADP_TransactionCompletedButNotDisposed"));
//  //  }

//  //  internal static InvalidOperationException TransactionRequired(string method)
//  //  {
//  //    return ADP.Provider(System.Data.Res.GetString("ADP_TransactionRequired", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //  }

//  //  internal static InvalidOperationException MissingSelectCommand(string method)
//  //  {
//  //    return ADP.Provider(System.Data.Res.GetString("ADP_MissingSelectCommand", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //  }

//  //  private static InvalidOperationException DataMapping(string error)
//  //  {
//  //    return ADP.InvalidOperation(error);
//  //  }

//  //  internal static InvalidOperationException ColumnSchemaExpression(string srcColumn, string cacheColumn)
//  //  {
//  //    return ADP.DataMapping(System.Data.Res.GetString("ADP_ColumnSchemaExpression", (object)srcColumn, (object)cacheColumn));
//  //  }

//  //  internal static InvalidOperationException ColumnSchemaMismatch(string srcColumn, Type srcType, DataColumn column)
//  //  {
//  //    return ADP.DataMapping(System.Data.Res.GetString("ADP_ColumnSchemaMismatch", (object)srcColumn, (object)srcType.Name, (object)column.ColumnName, (object)column.DataType.Name));
//  //  }

//  //  internal static InvalidOperationException ColumnSchemaMissing(string cacheColumn, string tableName, string srcColumn)
//  //  {
//  //    if (ADP.IsEmpty(tableName))
//  //      return ADP.InvalidOperation(System.Data.Res.GetString("ADP_ColumnSchemaMissing1", (object)cacheColumn, (object)tableName, (object)srcColumn));
//  //    return ADP.DataMapping(System.Data.Res.GetString("ADP_ColumnSchemaMissing2", (object)cacheColumn, (object)tableName, (object)srcColumn));
//  //  }

//  //  internal static InvalidOperationException MissingColumnMapping(string srcColumn)
//  //  {
//  //    return ADP.DataMapping(System.Data.Res.GetString("ADP_MissingColumnMapping", new object[1]
//  //    {
//  //      (object) srcColumn
//  //    }));
//  //  }

//  //  internal static InvalidOperationException MissingTableSchema(string cacheTable, string srcTable)
//  //  {
//  //    return ADP.DataMapping(System.Data.Res.GetString("ADP_MissingTableSchema", (object)cacheTable, (object)srcTable));
//  //  }

//  //  internal static InvalidOperationException MissingTableMapping(string srcTable)
//  //  {
//  //    return ADP.DataMapping(System.Data.Res.GetString("ADP_MissingTableMapping", new object[1]
//  //    {
//  //      (object) srcTable
//  //    }));
//  //  }

//  //  internal static InvalidOperationException MissingTableMappingDestination(string dstTable)
//  //  {
//  //    return ADP.DataMapping(System.Data.Res.GetString("ADP_MissingTableMappingDestination", new object[1]
//  //    {
//  //      (object) dstTable
//  //    }));
//  //  }

//  //  internal static Exception InvalidSourceColumn(string parameter)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidSourceColumn"), parameter);
//  //  }

//  //  internal static Exception ColumnsAddNullAttempt(string parameter)
//  //  {
//  //    return (Exception)ADP.CollectionNullValue(parameter, typeof(DataColumnMappingCollection), typeof(DataColumnMapping));
//  //  }

//  //  internal static Exception ColumnsDataSetColumn(string cacheColumn)
//  //  {
//  //    return (Exception)ADP.CollectionIndexString(typeof(DataColumnMapping), "DataSetColumn", cacheColumn, typeof(DataColumnMappingCollection));
//  //  }

//  //  internal static Exception ColumnsIndexInt32(int index, IColumnMappingCollection collection)
//  //  {
//  //    return (Exception)ADP.CollectionIndexInt32(index, collection.GetType(), collection.Count);
//  //  }

//  //  internal static Exception ColumnsIndexSource(string srcColumn)
//  //  {
//  //    return (Exception)ADP.CollectionIndexString(typeof(DataColumnMapping), "SourceColumn", srcColumn, typeof(DataColumnMappingCollection));
//  //  }

//  //  internal static Exception ColumnsIsNotParent(ICollection collection)
//  //  {
//  //    return (Exception)ADP.ParametersIsNotParent(typeof(DataColumnMapping), collection);
//  //  }

//  //  internal static Exception ColumnsIsParent(ICollection collection)
//  //  {
//  //    return (Exception)ADP.ParametersIsParent(typeof(DataColumnMapping), collection);
//  //  }

//  //  internal static Exception ColumnsUniqueSourceColumn(string srcColumn)
//  //  {
//  //    return ADP.CollectionUniqueValue(typeof(DataColumnMapping), "SourceColumn", srcColumn);
//  //  }

//  //  internal static Exception NotADataColumnMapping(object value)
//  //  {
//  //    return (Exception)ADP.CollectionInvalidType(typeof(DataColumnMappingCollection), typeof(DataColumnMapping), value);
//  //  }

//  //  internal static Exception InvalidSourceTable(string parameter)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidSourceTable"), parameter);
//  //  }

//  //  internal static Exception TablesAddNullAttempt(string parameter)
//  //  {
//  //    return (Exception)ADP.CollectionNullValue(parameter, typeof(DataTableMappingCollection), typeof(DataTableMapping));
//  //  }

//  //  internal static Exception TablesDataSetTable(string cacheTable)
//  //  {
//  //    return (Exception)ADP.CollectionIndexString(typeof(DataTableMapping), "DataSetTable", cacheTable, typeof(DataTableMappingCollection));
//  //  }

//  //  internal static Exception TablesIndexInt32(int index, ITableMappingCollection collection)
//  //  {
//  //    return (Exception)ADP.CollectionIndexInt32(index, collection.GetType(), collection.Count);
//  //  }

//  //  internal static Exception TablesIsNotParent(ICollection collection)
//  //  {
//  //    return (Exception)ADP.ParametersIsNotParent(typeof(DataTableMapping), collection);
//  //  }

//  //  internal static Exception TablesIsParent(ICollection collection)
//  //  {
//  //    return (Exception)ADP.ParametersIsParent(typeof(DataTableMapping), collection);
//  //  }

//  //  internal static Exception TablesSourceIndex(string srcTable)
//  //  {
//  //    return (Exception)ADP.CollectionIndexString(typeof(DataTableMapping), "SourceTable", srcTable, typeof(DataTableMappingCollection));
//  //  }

//  //  internal static Exception TablesUniqueSourceTable(string srcTable)
//  //  {
//  //    return ADP.CollectionUniqueValue(typeof(DataTableMapping), "SourceTable", srcTable);
//  //  }

//  //  internal static Exception NotADataTableMapping(object value)
//  //  {
//  //    return (Exception)ADP.CollectionInvalidType(typeof(DataTableMappingCollection), typeof(DataTableMapping), value);
//  //  }

//  //  internal static InvalidOperationException CommandAsyncOperationCompleted()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("SQL_AsyncOperationCompleted"));
//  //  }

//  //  internal static Exception CommandTextRequired(string method)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_CommandTextRequired", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //  }

//  //  internal static InvalidOperationException ConnectionRequired(string method)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_ConnectionRequired", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //  }

//  //  internal static InvalidOperationException OpenConnectionRequired(string method, ConnectionState state)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_OpenConnectionRequired", (object)method, (object)ADP.ConnectionStateMsg(state)));
//  //  }

//  //  internal static InvalidOperationException UpdateConnectionRequired(StatementType statementType, bool isRowUpdatingCommand)
//  //  {
//  //    string name;
//  //    if (isRowUpdatingCommand)
//  //    {
//  //      name = "ADP_ConnectionRequired_Clone";
//  //    }
//  //    else
//  //    {
//  //      switch (statementType)
//  //      {
//  //        case StatementType.Insert:
//  //          name = "ADP_ConnectionRequired_Insert";
//  //          goto label_8;
//  //        case StatementType.Update:
//  //          name = "ADP_ConnectionRequired_Update";
//  //          goto label_8;
//  //        case StatementType.Delete:
//  //          name = "ADP_ConnectionRequired_Delete";
//  //          goto label_8;
//  //      }
//  //      throw ADP.InvalidStatementType(statementType);
//  //    }
//  //    label_8:
//  //    return ADP.InvalidOperation(System.Data.Res.GetString(name));
//  //  }

//  //  internal static InvalidOperationException ConnectionRequired_Res(string method)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_ConnectionRequired_" + method));
//  //  }

//  //  internal static InvalidOperationException UpdateOpenConnectionRequired(StatementType statementType, bool isRowUpdatingCommand, ConnectionState state)
//  //  {
//  //    string name;
//  //    if (isRowUpdatingCommand)
//  //    {
//  //      name = "ADP_OpenConnectionRequired_Clone";
//  //    }
//  //    else
//  //    {
//  //      switch (statementType)
//  //      {
//  //        case StatementType.Insert:
//  //          name = "ADP_OpenConnectionRequired_Insert";
//  //          break;
//  //        case StatementType.Update:
//  //          name = "ADP_OpenConnectionRequired_Update";
//  //          break;
//  //        case StatementType.Delete:
//  //          name = "ADP_OpenConnectionRequired_Delete";
//  //          break;
//  //        default:
//  //          throw ADP.InvalidStatementType(statementType);
//  //      }
//  //    }
//  //    return ADP.InvalidOperation(System.Data.Res.GetString(name, new object[1]
//  //    {
//  //      (object) ADP.ConnectionStateMsg(state)
//  //    }));
//  //  }

//  //  internal static Exception NoStoredProcedureExists(string sproc)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_NoStoredProcedureExists", new object[1]
//  //    {
//  //      (object) sproc
//  //    }));
//  //  }

//  //  internal static Exception OpenReaderExists()
//  //  {
//  //    return ADP.OpenReaderExists((Exception)null);
//  //  }

//  //  internal static Exception OpenReaderExists(Exception e)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_OpenReaderExists"), e);
//  //  }

//  //  internal static Exception TransactionCompleted()
//  //  {
//  //    return (Exception)ADP.DataAdapter(System.Data.Res.GetString("ADP_TransactionCompleted"));
//  //  }

//  //  internal static Exception NonSeqByteAccess(long badIndex, long currIndex, string method)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_NonSeqByteAccess", (object)badIndex.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)currIndex.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)method));
//  //  }

//  //  internal static Exception NegativeParameter(string parameterName)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_NegativeParameter", new object[1]
//  //    {
//  //      (object) parameterName
//  //    }));
//  //  }

//  //  internal static Exception NumericToDecimalOverflow()
//  //  {
//  //    return (Exception)ADP.InvalidCast(System.Data.Res.GetString("ADP_NumericToDecimalOverflow"));
//  //  }

//  //  internal static Exception ExceedsMaxDataLength(long specifiedLength, long maxLength)
//  //  {
//  //    return (Exception)ADP.IndexOutOfRange(System.Data.Res.GetString("SQL_ExceedsMaxDataLength", (object)specifiedLength.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)maxLength.ToString((IFormatProvider)CultureInfo.InvariantCulture)));
//  //  }

//  //  internal static Exception InvalidSeekOrigin(string parameterName)
//  //  {
//  //    return (Exception)ADP.ArgumentOutOfRange(System.Data.Res.GetString("ADP_InvalidSeekOrigin"), parameterName);
//  //  }

//  //  internal static Exception InvalidImplicitConversion(Type fromtype, string totype)
//  //  {
//  //    return (Exception)ADP.InvalidCast(System.Data.Res.GetString("ADP_InvalidImplicitConversion", (object)fromtype.Name, (object)totype));
//  //  }

//  //  internal static Exception InvalidMetaDataValue()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidMetaDataValue"));
//  //  }

//  //  internal static Exception NotRowType()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_NotRowType"));
//  //  }

//  //  internal static ArgumentException UnwantedStatementType(StatementType statementType)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_UnwantedStatementType", new object[1]
//  //    {
//  //      (object) statementType.ToString()
//  //    }));
//  //  }

//  //  internal static InvalidOperationException NonSequentialColumnAccess(int badCol, int currCol)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_NonSequentialColumnAccess", (object)badCol.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)currCol.ToString((IFormatProvider)CultureInfo.InvariantCulture)));
//  //  }

//  //  internal static Exception FillSchemaRequiresSourceTableName(string parameter)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_FillSchemaRequiresSourceTableName"), parameter);
//  //  }

//  //  internal static Exception InvalidMaxRecords(string parameter, int max)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidMaxRecords", new object[1]
//  //    {
//  //      (object) max.ToString((IFormatProvider) CultureInfo.InvariantCulture)
//  //    }), parameter);
//  //  }

//  //  internal static Exception InvalidStartRecord(string parameter, int start)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidStartRecord", new object[1]
//  //    {
//  //      (object) start.ToString((IFormatProvider) CultureInfo.InvariantCulture)
//  //    }), parameter);
//  //  }

//  //  internal static Exception FillRequires(string parameter)
//  //  {
//  //    return (Exception)ADP.ArgumentNull(parameter);
//  //  }

//  //  internal static Exception FillRequiresSourceTableName(string parameter)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_FillRequiresSourceTableName"), parameter);
//  //  }

//  //  internal static Exception FillChapterAutoIncrement()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_FillChapterAutoIncrement"));
//  //  }

//  //  internal static InvalidOperationException MissingDataReaderFieldType(int index)
//  //  {
//  //    return ADP.DataAdapter(System.Data.Res.GetString("ADP_MissingDataReaderFieldType", new object[1]
//  //    {
//  //      (object) index
//  //    }));
//  //  }

//  //  internal static InvalidOperationException OnlyOneTableForStartRecordOrMaxRecords()
//  //  {
//  //    return ADP.DataAdapter(System.Data.Res.GetString("ADP_OnlyOneTableForStartRecordOrMaxRecords"));
//  //  }

//  //  internal static ArgumentNullException UpdateRequiresNonNullDataSet(string parameter)
//  //  {
//  //    return ADP.ArgumentNull(parameter);
//  //  }

//  //  internal static InvalidOperationException UpdateRequiresSourceTable(string defaultSrcTableName)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_UpdateRequiresSourceTable", new object[1]
//  //    {
//  //      (object) defaultSrcTableName
//  //    }));
//  //  }

//  //  internal static InvalidOperationException UpdateRequiresSourceTableName(string srcTable)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_UpdateRequiresSourceTableName", new object[1]
//  //    {
//  //      (object) srcTable
//  //    }));
//  //  }

//  //  internal static ArgumentNullException UpdateRequiresDataTable(string parameter)
//  //  {
//  //    return ADP.ArgumentNull(parameter);
//  //  }

//  //  internal static Exception UpdateConcurrencyViolation(StatementType statementType, int affected, int expected, DataRow[] dataRows)
//  //  {
//  //    string name;
//  //    switch (statementType)
//  //    {
//  //      case StatementType.Update:
//  //        name = "ADP_UpdateConcurrencyViolation_Update";
//  //        break;
//  //      case StatementType.Delete:
//  //        name = "ADP_UpdateConcurrencyViolation_Delete";
//  //        break;
//  //      case StatementType.Batch:
//  //        name = "ADP_UpdateConcurrencyViolation_Batch";
//  //        break;
//  //      default:
//  //        throw ADP.InvalidStatementType(statementType);
//  //    }
//  //    DBConcurrencyException concurrencyException = new DBConcurrencyException(System.Data.Res.GetString(name, (object)affected.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)expected.ToString((IFormatProvider)CultureInfo.InvariantCulture)), (Exception)null, dataRows);
//  //    ADP.TraceExceptionAsReturnValue((Exception)concurrencyException);
//  //    return (Exception)concurrencyException;
//  //  }

//  //  internal static InvalidOperationException UpdateRequiresCommand(StatementType statementType, bool isRowUpdatingCommand)
//  //  {
//  //    string name;
//  //    if (isRowUpdatingCommand)
//  //    {
//  //      name = "ADP_UpdateRequiresCommandClone";
//  //    }
//  //    else
//  //    {
//  //      switch (statementType)
//  //      {
//  //        case StatementType.Select:
//  //          name = "ADP_UpdateRequiresCommandSelect";
//  //          break;
//  //        case StatementType.Insert:
//  //          name = "ADP_UpdateRequiresCommandInsert";
//  //          break;
//  //        case StatementType.Update:
//  //          name = "ADP_UpdateRequiresCommandUpdate";
//  //          break;
//  //        case StatementType.Delete:
//  //          name = "ADP_UpdateRequiresCommandDelete";
//  //          break;
//  //        default:
//  //          throw ADP.InvalidStatementType(statementType);
//  //      }
//  //    }
//  //    return ADP.InvalidOperation(System.Data.Res.GetString(name));
//  //  }

//  //  internal static ArgumentException UpdateMismatchRowTable(int i)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_UpdateMismatchRowTable", new object[1]
//  //    {
//  //      (object) i.ToString((IFormatProvider) CultureInfo.InvariantCulture)
//  //    }));
//  //  }

//  //  internal static DataException RowUpdatedErrors()
//  //  {
//  //    return ADP.Data(System.Data.Res.GetString("ADP_RowUpdatedErrors"));
//  //  }

//  //  internal static DataException RowUpdatingErrors()
//  //  {
//  //    return ADP.Data(System.Data.Res.GetString("ADP_RowUpdatingErrors"));
//  //  }

//  //  internal static InvalidOperationException ResultsNotAllowedDuringBatch()
//  //  {
//  //    return ADP.DataAdapter(System.Data.Res.GetString("ADP_ResultsNotAllowedDuringBatch"));
//  //  }

//  //  internal static Exception InvalidCommandTimeout(int value)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidCommandTimeout", new object[1]
//  //    {
//  //      (object) value.ToString((IFormatProvider) CultureInfo.InvariantCulture)
//  //    }), "CommandTimeout");
//  //  }

//  //  internal static Exception DeriveParametersNotSupported(IDbCommand value)
//  //  {
//  //    return (Exception)ADP.DataAdapter(System.Data.Res.GetString("ADP_DeriveParametersNotSupported", (object)value.GetType().Name, (object)value.CommandType.ToString()));
//  //  }

//  //  internal static Exception UninitializedParameterSize(int index, Type dataType)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_UninitializedParameterSize", (object)index.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)dataType.Name));
//  //  }

//  //  internal static Exception PrepareParameterType(IDbCommand cmd)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_PrepareParameterType", new object[1]
//  //    {
//  //      (object) cmd.GetType().Name
//  //    }));
//  //  }

//  //  internal static Exception PrepareParameterSize(IDbCommand cmd)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_PrepareParameterSize", new object[1]
//  //    {
//  //      (object) cmd.GetType().Name
//  //    }));
//  //  }

//  //  internal static Exception PrepareParameterScale(IDbCommand cmd, string type)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_PrepareParameterScale", (object)cmd.GetType().Name, (object)type));
//  //  }

//  //  internal static Exception MismatchedAsyncResult(string expectedMethod, string gotMethod)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_MismatchedAsyncResult", (object)expectedMethod, (object)gotMethod));
//  //  }

//  //  internal static Exception ConnectionIsDisabled(Exception InnerException)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_ConnectionIsDisabled"), InnerException);
//  //  }

//  //  internal static Exception ClosedConnectionError()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_ClosedConnectionError"));
//  //  }

//  //  internal static Exception ConnectionAlreadyOpen(ConnectionState state)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_ConnectionAlreadyOpen", new object[1]
//  //    {
//  //      (object) ADP.ConnectionStateMsg(state)
//  //    }));
//  //  }

//  //  internal static Exception DelegatedTransactionPresent()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_DelegatedTransactionPresent"));
//  //  }

//  //  internal static Exception TransactionPresent()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_TransactionPresent"));
//  //  }

//  //  internal static Exception LocalTransactionPresent()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_LocalTransactionPresent"));
//  //  }

//  //  internal static Exception OpenConnectionPropertySet(string property, ConnectionState state)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_OpenConnectionPropertySet", (object)property, (object)ADP.ConnectionStateMsg(state)));
//  //  }

//  //  internal static Exception EmptyDatabaseName()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_EmptyDatabaseName"));
//  //  }

//  //  internal static Exception DatabaseNameTooLong()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_DatabaseNameTooLong"));
//  //  }

//  //  internal static Exception InternalConnectionError(ADP.ConnectionError internalError)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_InternalConnectionError", new object[1]
//  //    {
//  //      (object) internalError
//  //    }));
//  //  }

//  //  internal static Exception InternalError(ADP.InternalErrorCode internalError)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_InternalProviderError", new object[1]
//  //    {
//  //      (object) internalError
//  //    }));
//  //  }

//  //  internal static Exception InternalError(ADP.InternalErrorCode internalError, Exception innerException)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_InternalProviderError", new object[1]
//  //    {
//  //      (object) internalError
//  //    }), innerException);
//  //  }

//  //  internal static Exception InvalidConnectTimeoutValue()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidConnectTimeoutValue"));
//  //  }

//  //  internal static Exception InvalidConnectRetryCountValue()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("SQLCR_InvalidConnectRetryCountValue"));
//  //  }

//  //  internal static Exception InvalidConnectRetryIntervalValue()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("SQLCR_InvalidConnectRetryIntervalValue"));
//  //  }

//  //  internal static Exception DataReaderNoData()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_DataReaderNoData"));
//  //  }

//  //  internal static Exception DataReaderClosed(string method)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_DataReaderClosed", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidSourceBufferIndex(int maxLen, long srcOffset, string parameterName)
//  //  {
//  //    return ADP.ArgumentOutOfRange(System.Data.Res.GetString("ADP_InvalidSourceBufferIndex", (object)maxLen.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)srcOffset.ToString((IFormatProvider)CultureInfo.InvariantCulture)), parameterName);
//  //  }

//  //  internal static ArgumentOutOfRangeException InvalidDestinationBufferIndex(int maxLen, int dstOffset, string parameterName)
//  //  {
//  //    return ADP.ArgumentOutOfRange(System.Data.Res.GetString("ADP_InvalidDestinationBufferIndex", (object)maxLen.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)dstOffset.ToString((IFormatProvider)CultureInfo.InvariantCulture)), parameterName);
//  //  }

//  //  internal static IndexOutOfRangeException InvalidBufferSizeOrIndex(int numBytes, int bufferIndex)
//  //  {
//  //    return ADP.IndexOutOfRange(System.Data.Res.GetString("SQL_InvalidBufferSizeOrIndex", (object)numBytes.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)bufferIndex.ToString((IFormatProvider)CultureInfo.InvariantCulture)));
//  //  }

//  //  internal static Exception InvalidDataLength(long length)
//  //  {
//  //    return (Exception)ADP.IndexOutOfRange(System.Data.Res.GetString("SQL_InvalidDataLength", new object[1]
//  //    {
//  //      (object) length.ToString((IFormatProvider) CultureInfo.InvariantCulture)
//  //    }));
//  //  }

//  //  internal static InvalidOperationException AsyncOperationPending()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_PendingAsyncOperation"));
//  //  }

//  //  internal static Exception StreamClosed(string method)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_StreamClosed", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //  }

//  //  internal static IOException ErrorReadingFromStream(Exception internalException)
//  //  {
//  //    return ADP.IO(System.Data.Res.GetString("SqlMisc_StreamErrorMessage"), internalException);
//  //  }

//  //  internal static InvalidOperationException DynamicSQLJoinUnsupported()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_DynamicSQLJoinUnsupported"));
//  //  }

//  //  internal static InvalidOperationException DynamicSQLNoTableInfo()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_DynamicSQLNoTableInfo"));
//  //  }

//  //  internal static InvalidOperationException DynamicSQLNoKeyInfoDelete()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_DynamicSQLNoKeyInfoDelete"));
//  //  }

//  //  internal static InvalidOperationException DynamicSQLNoKeyInfoUpdate()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_DynamicSQLNoKeyInfoUpdate"));
//  //  }

//  //  internal static InvalidOperationException DynamicSQLNoKeyInfoRowVersionDelete()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_DynamicSQLNoKeyInfoRowVersionDelete"));
//  //  }

//  //  internal static InvalidOperationException DynamicSQLNoKeyInfoRowVersionUpdate()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_DynamicSQLNoKeyInfoRowVersionUpdate"));
//  //  }

//  //  internal static InvalidOperationException DynamicSQLNestedQuote(string name, string quote)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_DynamicSQLNestedQuote", (object)name, (object)quote));
//  //  }

//  //  internal static InvalidOperationException NoQuoteChange()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_NoQuoteChange"));
//  //  }

//  //  internal static InvalidOperationException ComputerNameEx(int lastError)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_ComputerNameEx", new object[1]
//  //    {
//  //      (object) lastError
//  //    }));
//  //  }

//  //  internal static InvalidOperationException MissingSourceCommand()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_MissingSourceCommand"));
//  //  }

//  //  internal static InvalidOperationException MissingSourceCommandConnection()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_MissingSourceCommandConnection"));
//  //  }

//  //  internal static ArgumentException InvalidDataType(TypeCode typecode)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidDataType", new object[1]
//  //    {
//  //      (object) typecode.ToString()
//  //    }));
//  //  }

//  //  internal static ArgumentException UnknownDataType(Type dataType)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_UnknownDataType", new object[1]
//  //    {
//  //      (object) dataType.FullName
//  //    }));
//  //  }

//  //  internal static ArgumentException DbTypeNotSupported(DbType type, Type enumtype)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_DbTypeNotSupported", (object)type.ToString(), (object)enumtype.Name));
//  //  }

//  //  internal static ArgumentException UnknownDataTypeCode(Type dataType, TypeCode typeCode)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_UnknownDataTypeCode", (object)((int)typeCode).ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)dataType.FullName));
//  //  }

//  //  internal static ArgumentException InvalidOffsetValue(int value)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidOffsetValue", new object[1]
//  //    {
//  //      (object) value.ToString((IFormatProvider) CultureInfo.InvariantCulture)
//  //    }));
//  //  }

//  //  internal static ArgumentException InvalidSizeValue(int value)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_InvalidSizeValue", new object[1]
//  //    {
//  //      (object) value.ToString((IFormatProvider) CultureInfo.InvariantCulture)
//  //    }));
//  //  }

//  //  internal static ArgumentException ParameterValueOutOfRange(Decimal value)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_ParameterValueOutOfRange", new object[1]
//  //    {
//  //      (object) value.ToString((IFormatProvider) null)
//  //    }));
//  //  }

//  //  internal static ArgumentException ParameterValueOutOfRange(SqlDecimal value)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_ParameterValueOutOfRange", new object[1]
//  //    {
//  //      (object) value.ToString()
//  //    }));
//  //  }

//  //  internal static ArgumentException ParameterValueOutOfRange(string value)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_ParameterValueOutOfRange", new object[1]
//  //    {
//  //      (object) value
//  //    }));
//  //  }

//  //  internal static ArgumentException VersionDoesNotSupportDataType(string typeName)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("ADP_VersionDoesNotSupportDataType", new object[1]
//  //    {
//  //      (object) typeName
//  //    }));
//  //  }

//  //  internal static Exception ParameterConversionFailed(object value, Type destType, Exception inner)
//  //  {
//  //    string message = System.Data.Res.GetString("ADP_ParameterConversionFailed", (object)value.GetType().Name, (object)destType.Name);
//  //    Exception e = !(inner is ArgumentException) ? (!(inner is FormatException) ? (!(inner is InvalidCastException) ? (!(inner is OverflowException) ? inner : (Exception)new OverflowException(message, inner)) : (Exception)new InvalidCastException(message, inner)) : (Exception)new FormatException(message, inner)) : (Exception)new ArgumentException(message, inner);
//  //    ADP.TraceExceptionAsReturnValue(e);
//  //    return e;
//  //  }

//  //  internal static Exception ParametersMappingIndex(int index, IDataParameterCollection collection)
//  //  {
//  //    return (Exception)ADP.CollectionIndexInt32(index, collection.GetType(), collection.Count);
//  //  }

//  //  internal static Exception ParametersSourceIndex(string parameterName, IDataParameterCollection collection, Type parameterType)
//  //  {
//  //    return (Exception)ADP.CollectionIndexString(parameterType, "ParameterName", parameterName, collection.GetType());
//  //  }

//  //  internal static Exception ParameterNull(string parameter, IDataParameterCollection collection, Type parameterType)
//  //  {
//  //    return (Exception)ADP.CollectionNullValue(parameter, collection.GetType(), parameterType);
//  //  }

//  //  internal static Exception InvalidParameterType(IDataParameterCollection collection, Type parameterType, object invalidValue)
//  //  {
//  //    return (Exception)ADP.CollectionInvalidType(collection.GetType(), parameterType, invalidValue);
//  //  }

//  //  internal static Exception ParallelTransactionsNotSupported(IDbConnection obj)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_ParallelTransactionsNotSupported", new object[1]
//  //    {
//  //      (object) obj.GetType().Name
//  //    }));
//  //  }

//  //  internal static Exception TransactionZombied(IDbTransaction obj)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_TransactionZombied", new object[1]
//  //    {
//  //      (object) obj.GetType().Name
//  //    }));
//  //  }

//  //  internal static Exception DbRecordReadOnly(string methodname)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_DbRecordReadOnly", new object[1]
//  //    {
//  //      (object) methodname
//  //    }));
//  //  }

//  //  internal static Exception OffsetOutOfRangeException()
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("ADP_OffsetOutOfRangeException"));
//  //  }

//  //  internal static Exception AmbigousCollectionName(string collectionName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_AmbigousCollectionName", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }));
//  //  }

//  //  internal static Exception CollectionNameIsNotUnique(string collectionName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_CollectionNameISNotUnique", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }));
//  //  }

//  //  internal static Exception DataTableDoesNotExist(string collectionName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_DataTableDoesNotExist", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }));
//  //  }

//  //  internal static Exception IncorrectNumberOfDataSourceInformationRows()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_IncorrectNumberOfDataSourceInformationRows"));
//  //  }

//  //  internal static ArgumentException InvalidRestrictionValue(string collectionName, string restrictionName, string restrictionValue)
//  //  {
//  //    return ADP.Argument(System.Data.Res.GetString("MDF_InvalidRestrictionValue", (object)collectionName, (object)restrictionName, (object)restrictionValue));
//  //  }

//  //  internal static Exception InvalidXml()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_InvalidXml"));
//  //  }

//  //  internal static Exception InvalidXmlMissingColumn(string collectionName, string columnName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_InvalidXmlMissingColumn", (object)collectionName, (object)columnName));
//  //  }

//  //  internal static Exception InvalidXmlInvalidValue(string collectionName, string columnName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_InvalidXmlInvalidValue", (object)collectionName, (object)columnName));
//  //  }

//  //  internal static Exception MissingDataSourceInformationColumn()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_MissingDataSourceInformationColumn"));
//  //  }

//  //  internal static Exception MissingRestrictionColumn()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_MissingRestrictionColumn"));
//  //  }

//  //  internal static Exception MissingRestrictionRow()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_MissingRestrictionRow"));
//  //  }

//  //  internal static Exception NoColumns()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_NoColumns"));
//  //  }

//  //  internal static Exception QueryFailed(string collectionName, Exception e)
//  //  {
//  //    return (Exception)ADP.InvalidOperation(System.Data.Res.GetString("MDF_QueryFailed", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }), e);
//  //  }

//  //  internal static Exception TooManyRestrictions(string collectionName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_TooManyRestrictions", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }));
//  //  }

//  //  internal static Exception UnableToBuildCollection(string collectionName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_UnableToBuildCollection", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }));
//  //  }

//  //  internal static Exception UndefinedCollection(string collectionName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_UndefinedCollection", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }));
//  //  }

//  //  internal static Exception UndefinedPopulationMechanism(string populationMechanism)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_UndefinedPopulationMechanism", new object[1]
//  //    {
//  //      (object) populationMechanism
//  //    }));
//  //  }

//  //  internal static Exception UnsupportedVersion(string collectionName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("MDF_UnsupportedVersion", new object[1]
//  //    {
//  //      (object) collectionName
//  //    }));
//  //  }

//  //  internal static InvalidOperationException InvalidDateTimeDigits(string dataTypeName)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidDateTimeDigits", new object[1]
//  //    {
//  //      (object) dataTypeName
//  //    }));
//  //  }

//  //  internal static Exception InvalidFormatValue()
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidFormatValue"));
//  //  }

//  //  internal static InvalidOperationException InvalidMaximumScale(string dataTypeName)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_InvalidMaximumScale", new object[1]
//  //    {
//  //      (object) dataTypeName
//  //    }));
//  //  }

//  //  internal static Exception LiteralValueIsInvalid(string dataTypeName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_LiteralValueIsInvalid", new object[1]
//  //    {
//  //      (object) dataTypeName
//  //    }));
//  //  }

//  //  internal static Exception EvenLengthLiteralValue(string argumentName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_EvenLengthLiteralValue"), argumentName);
//  //  }

//  //  internal static Exception HexDigitLiteralValue(string argumentName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_HexDigitLiteralValue"), argumentName);
//  //  }

//  //  internal static InvalidOperationException QuotePrefixNotSet(string method)
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_QuotePrefixNotSet", new object[1]
//  //    {
//  //      (object) method
//  //    }));
//  //  }

//  //  internal static InvalidOperationException UnableToCreateBooleanLiteral()
//  //  {
//  //    return ADP.InvalidOperation(System.Data.Res.GetString("ADP_UnableToCreateBooleanLiteral"));
//  //  }

//  //  internal static Exception UnsupportedNativeDataTypeOleDb(string dataTypeName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_UnsupportedNativeDataTypeOleDb", new object[1]
//  //    {
//  //      (object) dataTypeName
//  //    }));
//  //  }

//  //  internal static Exception InvalidArgumentValue(string methodName)
//  //  {
//  //    return (Exception)ADP.Argument(System.Data.Res.GetString("ADP_InvalidArgumentValue", new object[1]
//  //    {
//  //      (object) methodName
//  //    }));
//  //  }

//  //  internal static bool CompareInsensitiveInvariant(string strvalue, string strconst)
//  //  {
//  //    return CultureInfo.InvariantCulture.CompareInfo.Compare(strvalue, strconst, CompareOptions.IgnoreCase) == 0;
//  //  }

//  //  internal static Delegate FindBuilder(MulticastDelegate mcd)
//  //  {
//  //    if ((object)mcd != null)
//  //    {
//  //      Delegate[] invocationList = mcd.GetInvocationList();
//  //      for (int index = 0; index < invocationList.Length; ++index)
//  //      {
//  //        if (invocationList[index].Target is DbCommandBuilder)
//  //          return invocationList[index];
//  //      }
//  //    }
//  //    return (Delegate)null;
//  //  }

//  //  internal static Transaction GetCurrentTransaction()
//  //  {
//  //    return Transaction.Current;
//  //  }

//  //  internal static void SetCurrentTransaction(Transaction transaction)
//  //  {
//  //    Transaction.Current = transaction;
//  //  }

//  //  internal static IDtcTransaction GetOletxTransaction(Transaction transaction)
//  //  {
//  //    IDtcTransaction dtcTransaction = (IDtcTransaction)null;
//  //    if ((Transaction)null != transaction)
//  //      dtcTransaction = TransactionInterop.GetDtcTransaction(transaction);
//  //    return dtcTransaction;
//  //  }

//  //  [MethodImpl(MethodImplOptions.NoInlining)]
//  //  internal static bool IsSysTxEqualSysEsTransaction()
//  //  {
//  //    return !ContextUtil.IsInTransaction && (Transaction)null == Transaction.Current || ContextUtil.IsInTransaction && Transaction.Current == ContextUtil.SystemTransaction;
//  //  }

//  //  internal static bool NeedManualEnlistment()
//  //  {
//  //    if (ADP.IsWindowsNT)
//  //    {
//  //      bool flag = !InOutOfProcHelper.InProc;
//  //      if (flag && !ADP.IsSysTxEqualSysEsTransaction() || !flag && (Transaction)null != Transaction.Current)
//  //        return true;
//  //    }
//  //    return false;
//  //  }

//  //  internal static void TimerCurrent(out long ticks)
//  //  {
//  //    ticks = DateTime.UtcNow.ToFileTimeUtc();
//  //  }

//  //  internal static long TimerCurrent()
//  //  {
//  //    return DateTime.UtcNow.ToFileTimeUtc();
//  //  }

//  //  internal static long TimerFromSeconds(int seconds)
//  //  {
//  //    return checked((long)seconds * 10000000L);
//  //  }

//  //  internal static long TimerFromMilliseconds(long milliseconds)
//  //  {
//  //    return checked(milliseconds * 10000L);
//  //  }

//  //  internal static bool TimerHasExpired(long timerExpire)
//  //  {
//  //    return ADP.TimerCurrent() > timerExpire;
//  //  }

//  //  internal static long TimerRemaining(long timerExpire)
//  //  {
//  //    long num = ADP.TimerCurrent();
//  //    return checked(timerExpire - num);
//  //  }

//  //  internal static long TimerRemainingMilliseconds(long timerExpire)
//  //  {
//  //    return ADP.TimerToMilliseconds(ADP.TimerRemaining(timerExpire));
//  //  }

//  //  internal static long TimerRemainingSeconds(long timerExpire)
//  //  {
//  //    return ADP.TimerToSeconds(ADP.TimerRemaining(timerExpire));
//  //  }

//  //  internal static long TimerToMilliseconds(long timerValue)
//  //  {
//  //    return timerValue / 10000L;
//  //  }

//  //  private static long TimerToSeconds(long timerValue)
//  //  {
//  //    return timerValue / 10000000L;
//  //  }

//  //  [EnvironmentPermission(SecurityAction.Assert, Read = "COMPUTERNAME")]
//  //  internal static string MachineName()
//  //  {
//  //    return Environment.MachineName;
//  //  }

//  //  internal static string BuildQuotedString(string quotePrefix, string quoteSuffix, string unQuotedString)
//  //  {
//  //    StringBuilder stringBuilder = new StringBuilder();
//  //    if (!ADP.IsEmpty(quotePrefix))
//  //      stringBuilder.Append(quotePrefix);
//  //    if (!ADP.IsEmpty(quoteSuffix))
//  //    {
//  //      stringBuilder.Append(unQuotedString.Replace(quoteSuffix, quoteSuffix + quoteSuffix));
//  //      stringBuilder.Append(quoteSuffix);
//  //    }
//  //    else
//  //      stringBuilder.Append(unQuotedString);
//  //    return stringBuilder.ToString();
//  //  }

//  //  internal static byte[] ByteArrayFromString(string hexString, string dataTypeName)
//  //  {
//  //    if ((hexString.Length & 1) != 0)
//  //      throw ADP.LiteralValueIsInvalid(dataTypeName);
//  //    char[] charArray = hexString.ToCharArray();
//  //    byte[] numArray = new byte[hexString.Length / 2];
//  //    CultureInfo invariantCulture = CultureInfo.InvariantCulture;
//  //    int index = 0;
//  //    while (index < hexString.Length)
//  //    {
//  //      int num1 = "0123456789abcdef".IndexOf(char.ToLower(charArray[index], invariantCulture));
//  //      int num2 = "0123456789abcdef".IndexOf(char.ToLower(charArray[index + 1], invariantCulture));
//  //      if (num1 < 0 || num2 < 0)
//  //        throw ADP.LiteralValueIsInvalid(dataTypeName);
//  //      numArray[index / 2] = (byte)(num1 << 4 | num2);
//  //      index += 2;
//  //    }
//  //    return numArray;
//  //  }

//  //  internal static void EscapeSpecialCharacters(string unescapedString, StringBuilder escapedString)
//  //  {
//  //    foreach (char ch in unescapedString)
//  //    {
//  //      if (".$^{[(|)*+?\\]".IndexOf(ch) >= 0)
//  //        escapedString.Append("\\");
//  //      escapedString.Append(ch);
//  //    }
//  //  }

//  //  internal static string FixUpDecimalSeparator(string numericString, bool formatLiteral, string decimalSeparator, char[] exponentSymbols)
//  //  {
//  //    string str;
//  //    if (numericString.IndexOfAny(exponentSymbols) == -1)
//  //    {
//  //      if (ADP.IsEmpty(decimalSeparator))
//  //        decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
//  //      str = !formatLiteral ? numericString.Replace(decimalSeparator, ".") : numericString.Replace(".", decimalSeparator);
//  //    }
//  //    else
//  //      str = numericString;
//  //    return str;
//  //  }

//  //  [FileIOPermission(SecurityAction.Assert, AllFiles = FileIOPermissionAccess.PathDiscovery)]
//  //  internal static string GetFullPath(string filename)
//  //  {
//  //    return Path.GetFullPath(filename);
//  //  }

//  //  internal static string GetComputerNameDnsFullyQualified()
//  //  {
//  //    string str;
//  //    if (ADP.IsPlatformNT5)
//  //    {
//  //      int bufferSize = 0;
//  //      int lastError = 0;
//  //      if (SafeNativeMethods.GetComputerNameEx(3, (StringBuilder)null, ref bufferSize) == 0)
//  //        lastError = Marshal.GetLastWin32Error();
//  //      if (lastError != 0 && lastError != 234 || bufferSize <= 0)
//  //        throw ADP.ComputerNameEx(lastError);
//  //      StringBuilder nameBuffer = new StringBuilder(bufferSize);
//  //      bufferSize = nameBuffer.Capacity;
//  //      if (SafeNativeMethods.GetComputerNameEx(3, nameBuffer, ref bufferSize) == 0)
//  //        throw ADP.ComputerNameEx(Marshal.GetLastWin32Error());
//  //      str = nameBuffer.ToString();
//  //    }
//  //    else
//  //      str = ADP.MachineName();
//  //    return str;
//  //  }

//  //  internal static Stream GetFileStream(string filename)
//  //  {
//  //    new FileIOPermission(FileIOPermissionAccess.Read, filename).Assert();
//  //    try
//  //    {
//  //      return (Stream)new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
//  //    }
//  //    finally
//  //    {
//  //      CodeAccessPermission.RevertAssert();
//  //    }
//  //  }

//  //  internal static FileVersionInfo GetVersionInfo(string filename)
//  //  {
//  //    new FileIOPermission(FileIOPermissionAccess.Read, filename).Assert();
//  //    try
//  //    {
//  //      return FileVersionInfo.GetVersionInfo(filename);
//  //    }
//  //    finally
//  //    {
//  //      CodeAccessPermission.RevertAssert();
//  //    }
//  //  }

//  //  internal static Stream GetXmlStreamFromValues(string[] values, string errorString)
//  //  {
//  //    if (values.Length != 1)
//  //      throw ADP.ConfigWrongNumberOfValues(errorString);
//  //    return ADP.GetXmlStream(values[0], errorString);
//  //  }

//  //  internal static Stream GetXmlStream(string value, string errorString)
//  //  {
//  //    string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
//  //    if (runtimeDirectory == null)
//  //      throw ADP.ConfigUnableToLoadXmlMetaDataFile(errorString);
//  //    StringBuilder stringBuilder = new StringBuilder(runtimeDirectory.Length + "config\\".Length + value.Length);
//  //    stringBuilder.Append(runtimeDirectory);
//  //    stringBuilder.Append("config\\");
//  //    stringBuilder.Append(value);
//  //    string filename = stringBuilder.ToString();
//  //    if (ADP.GetFullPath(filename) != filename)
//  //      throw ADP.ConfigUnableToLoadXmlMetaDataFile(errorString);
//  //    try
//  //    {
//  //      return ADP.GetFileStream(filename);
//  //    }
//  //    catch (Exception ex)
//  //    {
//  //      if (ADP.IsCatchableExceptionType(ex))
//  //        throw ADP.ConfigUnableToLoadXmlMetaDataFile(errorString);
//  //      throw;
//  //    }
//  //  }

//  //  internal static object ClassesRootRegistryValue(string subkey, string queryvalue)
//  //  {
//  //    new RegistryPermission(RegistryPermissionAccess.Read, "HKEY_CLASSES_ROOT\\" + subkey).Assert();
//  //    try
//  //    {
//  //      using (RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(subkey, false))
//  //        return registryKey?.GetValue(queryvalue);
//  //    }
//  //    catch (SecurityException ex)
//  //    {
//  //      ADP.TraceExceptionWithoutRethrow((Exception)ex);
//  //      return (object)null;
//  //    }
//  //    finally
//  //    {
//  //      CodeAccessPermission.RevertAssert();
//  //    }
//  //  }

//  //  internal static object LocalMachineRegistryValue(string subkey, string queryvalue)
//  //  {
//  //    new RegistryPermission(RegistryPermissionAccess.Read, "HKEY_LOCAL_MACHINE\\" + subkey).Assert();
//  //    try
//  //    {
//  //      using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(subkey, false))
//  //        return registryKey?.GetValue(queryvalue);
//  //    }
//  //    catch (SecurityException ex)
//  //    {
//  //      ADP.TraceExceptionWithoutRethrow((Exception)ex);
//  //      return (object)null;
//  //    }
//  //    finally
//  //    {
//  //      CodeAccessPermission.RevertAssert();
//  //    }
//  //  }

//  //  internal static void CheckVersionMDAC(bool ifodbcelseoledb)
//  //  {
//  //    string str;
//  //    int fileMajorPart;
//  //    int fileMinorPart;
//  //    int fileBuildPart;
//  //    try
//  //    {
//  //      str = (string)ADP.LocalMachineRegistryValue("Software\\Microsoft\\DataAccess", "FullInstallVer");
//  //      if (ADP.IsEmpty(str))
//  //      {
//  //        FileVersionInfo versionInfo = ADP.GetVersionInfo((string)ADP.ClassesRootRegistryValue("CLSID\\{2206CDB2-19C1-11D1-89E0-00C04FD7A829}\\InprocServer32", ADP.StrEmpty));
//  //        fileMajorPart = versionInfo.FileMajorPart;
//  //        fileMinorPart = versionInfo.FileMinorPart;
//  //        fileBuildPart = versionInfo.FileBuildPart;
//  //        str = versionInfo.FileVersion;
//  //      }
//  //      else
//  //      {
//  //        string[] strArray = str.Split('.');
//  //        fileMajorPart = int.Parse(strArray[0], NumberStyles.None, (IFormatProvider)CultureInfo.InvariantCulture);
//  //        fileMinorPart = int.Parse(strArray[1], NumberStyles.None, (IFormatProvider)CultureInfo.InvariantCulture);
//  //        fileBuildPart = int.Parse(strArray[2], NumberStyles.None, (IFormatProvider)CultureInfo.InvariantCulture);
//  //        int.Parse(strArray[3], NumberStyles.None, (IFormatProvider)CultureInfo.InvariantCulture);
//  //      }
//  //    }
//  //    catch (Exception ex)
//  //    {
//  //      if (ADP.IsCatchableExceptionType(ex))
//  //        throw ODB.MDACNotAvailable(ex);
//  //      throw;
//  //    }
//  //    if (fileMajorPart >= 2 && (fileMajorPart != 2 || fileMinorPart >= 60 && (fileMinorPart != 60 || fileBuildPart >= 6526)))
//  //      return;
//  //    if (ifodbcelseoledb)
//  //      throw ADP.DataAdapter(System.Data.Res.GetString("Odbc_MDACWrongVersion", new object[1]
//  //      {
//  //        (object) str
//  //      }));
//  //    throw ADP.DataAdapter(System.Data.Res.GetString("OleDb_MDACWrongVersion", new object[1]
//  //    {
//  //      (object) str
//  //    }));
//  //  }

//  //  internal static bool RemoveStringQuotes(string quotePrefix, string quoteSuffix, string quotedString, out string unquotedString)
//  //  {
//  //    int startIndex = quotePrefix != null ? quotePrefix.Length : 0;
//  //    int num = quoteSuffix != null ? quoteSuffix.Length : 0;
//  //    if (num + startIndex == 0)
//  //    {
//  //      unquotedString = quotedString;
//  //      return true;
//  //    }
//  //    if (quotedString == null)
//  //    {
//  //      unquotedString = quotedString;
//  //      return false;
//  //    }
//  //    int length = quotedString.Length;
//  //    if (length < startIndex + num)
//  //    {
//  //      unquotedString = quotedString;
//  //      return false;
//  //    }
//  //    if (startIndex > 0 && !quotedString.StartsWith(quotePrefix, StringComparison.Ordinal))
//  //    {
//  //      unquotedString = quotedString;
//  //      return false;
//  //    }
//  //    if (num > 0)
//  //    {
//  //      if (!quotedString.EndsWith(quoteSuffix, StringComparison.Ordinal))
//  //      {
//  //        unquotedString = quotedString;
//  //        return false;
//  //      }
//  //      unquotedString = quotedString.Substring(startIndex, length - (startIndex + num)).Replace(quoteSuffix + quoteSuffix, quoteSuffix);
//  //    }
//  //    else
//  //      unquotedString = quotedString.Substring(startIndex, length - startIndex);
//  //    return true;
//  //  }

//  //  internal static DataRow[] SelectAdapterRows(DataTable dataTable, bool sorted)
//  //  {
//  //    int num1 = 0;
//  //    int num2 = 0;
//  //    int num3 = 0;
//  //    DataRowCollection rows = dataTable.Rows;
//  //    foreach (DataRow dataRow in (InternalDataCollectionBase)rows)
//  //    {
//  //      switch (dataRow.RowState)
//  //      {
//  //        case DataRowState.Added:
//  //          ++num1;
//  //          continue;
//  //        case DataRowState.Deleted:
//  //          ++num2;
//  //          continue;
//  //        case DataRowState.Modified:
//  //          ++num3;
//  //          continue;
//  //        default:
//  //          continue;
//  //      }
//  //    }
//  //    DataRow[] dataRowArray = new DataRow[num1 + num2 + num3];
//  //    if (sorted)
//  //    {
//  //      int num4 = num1 + num2;
//  //      int num5 = num1;
//  //      int num6 = 0;
//  //      foreach (DataRow dataRow in (InternalDataCollectionBase)rows)
//  //      {
//  //        switch (dataRow.RowState)
//  //        {
//  //          case DataRowState.Added:
//  //            dataRowArray[num6++] = dataRow;
//  //            continue;
//  //          case DataRowState.Deleted:
//  //            dataRowArray[num5++] = dataRow;
//  //            continue;
//  //          case DataRowState.Modified:
//  //            dataRowArray[num4++] = dataRow;
//  //            continue;
//  //          default:
//  //            continue;
//  //        }
//  //      }
//  //    }
//  //    else
//  //    {
//  //      int num4 = 0;
//  //      foreach (DataRow dataRow in (InternalDataCollectionBase)rows)
//  //      {
//  //        if ((dataRow.RowState & (DataRowState.Added | DataRowState.Deleted | DataRowState.Modified)) != (DataRowState)0)
//  //        {
//  //          dataRowArray[num4++] = dataRow;
//  //          if (num4 == dataRowArray.Length)
//  //            break;
//  //        }
//  //      }
//  //    }
//  //    return dataRowArray;
//  //  }

//  //  internal static int StringLength(string inputString)
//  //  {
//  //    if (inputString == null)
//  //      return 0;
//  //    return inputString.Length;
//  //  }

//  //  internal static void BuildSchemaTableInfoTableNames(string[] columnNameArray)
//  //  {
//  //    Dictionary<string, int> hash = new Dictionary<string, int>(columnNameArray.Length);
//  //    int val1 = columnNameArray.Length;
//  //    for (int index = columnNameArray.Length - 1; 0 <= index; --index)
//  //    {
//  //      string columnName = columnNameArray[index];
//  //      if (columnName != null && 0 < columnName.Length)
//  //      {
//  //        string lower = columnName.ToLower(CultureInfo.InvariantCulture);
//  //        int val2;
//  //        if (hash.TryGetValue(lower, out val2))
//  //          val1 = Math.Min(val1, val2);
//  //        hash[lower] = index;
//  //      }
//  //      else
//  //      {
//  //        columnNameArray[index] = ADP.StrEmpty;
//  //        val1 = index;
//  //      }
//  //    }
//  //    int uniqueIndex = 1;
//  //    for (int index = val1; index < columnNameArray.Length; ++index)
//  //    {
//  //      string columnName = columnNameArray[index];
//  //      if (columnName.Length == 0)
//  //      {
//  //        columnNameArray[index] = "Column";
//  //        uniqueIndex = ADP.GenerateUniqueName(hash, ref columnNameArray[index], index, uniqueIndex);
//  //      }
//  //      else
//  //      {
//  //        string lower = columnName.ToLower(CultureInfo.InvariantCulture);
//  //        if (index != hash[lower])
//  //          ADP.GenerateUniqueName(hash, ref columnNameArray[index], index, 1);
//  //      }
//  //    }
//  //  }

//  //  private static int GenerateUniqueName(Dictionary<string, int> hash, ref string columnName, int index, int uniqueIndex)
//  //  {
//  //    string str;
//  //    string lower;
//  //    while (true)
//  //    {
//  //      str = columnName + uniqueIndex.ToString((IFormatProvider)CultureInfo.InvariantCulture);
//  //      lower = str.ToLower(CultureInfo.InvariantCulture);
//  //      if (hash.ContainsKey(lower))
//  //        ++uniqueIndex;
//  //      else
//  //        break;
//  //    }
//  //    columnName = str;
//  //    hash.Add(lower, index);
//  //    return uniqueIndex;
//  //  }

//  //  [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
//  //  internal static IntPtr IntPtrOffset(IntPtr pbase, int offset)
//  //  {
//  //    if (4 == ADP.PtrSize)
//  //      return (IntPtr)checked(pbase.ToInt32() + offset);
//  //    return (IntPtr)checked(pbase.ToInt64() + (long)offset);
//  //  }

//  //  internal static int IntPtrToInt32(IntPtr value)
//  //  {
//  //    if (4 == ADP.PtrSize)
//  //      return (int)value;
//  //    return (int)Math.Max((long)int.MinValue, Math.Min((long)int.MaxValue, (long)value));
//  //  }

//  //  internal static int SrcCompare(string strA, string strB)
//  //  {
//  //    return !(strA == strB) ? 1 : 0;
//  //  }

//  //  internal static int DstCompare(string strA, string strB)
//  //  {
//  //    return CultureInfo.CurrentCulture.CompareInfo.Compare(strA, strB, CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth);
//  //  }

//  //  internal static bool IsDirection(IDataParameter value, ParameterDirection condition)
//  //  {
//  //    return condition == (condition & value.Direction);
//  //  }

//  //  internal static bool IsEmpty(string str)
//  //  {
//  //    if (str != null)
//  //      return str.Length == 0;
//  //    return true;
//  //  }

//  //  internal static bool IsEmptyArray(string[] array)
//  //  {
//  //    if (array != null)
//  //      return array.Length == 0;
//  //    return true;
//  //  }

//  //  internal static bool IsNull(object value)
//  //  {
//  //    if (value == null || DBNull.Value == value)
//  //      return true;
//  //    INullable nullable = value as INullable;
//  //    if (nullable != null)
//  //      return nullable.IsNull;
//  //    return false;
//  //  }

//  //  internal static void IsNullOrSqlType(object value, out bool isNull, out bool isSqlType)
//  //  {
//  //    if (value == null || value == DBNull.Value)
//  //    {
//  //      isNull = true;
//  //      isSqlType = false;
//  //    }
//  //    else
//  //    {
//  //      INullable nullable = value as INullable;
//  //      if (nullable != null)
//  //      {
//  //        isNull = nullable.IsNull;
//  //        isSqlType = DataStorage.IsSqlType(value.GetType());
//  //      }
//  //      else
//  //      {
//  //        isNull = false;
//  //        isSqlType = false;
//  //      }
//  //    }
//  //  }

//  //  internal static Version GetAssemblyVersion()
//  //  {
//  //    if (ADP._systemDataVersion == (Version)null)
//  //      ADP._systemDataVersion = new Version("4.7.3163.0");
//  //    return ADP._systemDataVersion;
//  //  }

//  //  internal static bool IsAzureSqlServerEndpoint(string dataSource)
//  //  {
//  //    int length1 = dataSource.LastIndexOf(',');
//  //    if (length1 >= 0)
//  //      dataSource = dataSource.Substring(0, length1);
//  //    int length2 = dataSource.LastIndexOf('\\');
//  //    if (length2 >= 0)
//  //      dataSource = dataSource.Substring(0, length2);
//  //    dataSource = dataSource.Trim();
//  //    for (int index = 0; index < ADP.AzureSqlServerEndpoints.Length; ++index)
//  //    {
//  //      if (dataSource.EndsWith(ADP.AzureSqlServerEndpoints[index], StringComparison.OrdinalIgnoreCase))
//  //        return true;
//  //    }
//  //    return false;
//  //  }

//  //  internal enum ConnectionError
//  //  {
//  //    BeginGetConnectionReturnsNull,
//  //    GetConnectionReturnsNull,
//  //    ConnectionOptionsMissing,
//  //    CouldNotSwitchToClosedPreviouslyOpenedState,
//  //  }

//  //  internal enum InternalErrorCode
//  //  {
//  //    UnpooledObjectHasOwner = 0,
//  //    UnpooledObjectHasWrongOwner = 1,
//  //    PushingObjectSecondTime = 2,
//  //    PooledObjectHasOwner = 3,
//  //    PooledObjectInPoolMoreThanOnce = 4,
//  //    CreateObjectReturnedNull = 5,
//  //    NewObjectCannotBePooled = 6,
//  //    NonPooledObjectUsedMoreThanOnce = 7,
//  //    AttemptingToPoolOnRestrictedToken = 8,
//  //    ConvertSidToStringSidWReturnedNull = 10, // 0x0000000A
//  //    AttemptingToConstructReferenceCollectionOnStaticObject = 12, // 0x0000000C
//  //    AttemptingToEnlistTwice = 13, // 0x0000000D
//  //    CreateReferenceCollectionReturnedNull = 14, // 0x0000000E
//  //    PooledObjectWithoutPool = 15, // 0x0000000F
//  //    UnexpectedWaitAnyResult = 16, // 0x00000010
//  //    SynchronousConnectReturnedPending = 17, // 0x00000011
//  //    CompletedConnectReturnedPending = 18, // 0x00000012
//  //    NameValuePairNext = 20, // 0x00000014
//  //    InvalidParserState1 = 21, // 0x00000015
//  //    InvalidParserState2 = 22, // 0x00000016
//  //    InvalidParserState3 = 23, // 0x00000017
//  //    InvalidBuffer = 30, // 0x0000001E
//  //    UnimplementedSMIMethod = 40, // 0x00000028
//  //    InvalidSmiCall = 41, // 0x00000029
//  //    SqlDependencyObtainProcessDispatcherFailureObjectHandle = 50, // 0x00000032
//  //    SqlDependencyProcessDispatcherFailureCreateInstance = 51, // 0x00000033
//  //    SqlDependencyProcessDispatcherFailureAppDomain = 52, // 0x00000034
//  //    SqlDependencyCommandHashIsNotAssociatedWithNotification = 53, // 0x00000035
//  //    UnknownTransactionFailure = 60, // 0x0000003C
//  //  }
//  //}
//}

