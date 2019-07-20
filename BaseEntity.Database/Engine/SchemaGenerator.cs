/*
 * SchemaGenerator.cs
 *
 * Copyright (c) WebMathTraining Inc 2010. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using NHibernate;
using NHibernate.Connection;
using NHibernate.Dialect;
using NHibernate.Mapping;

using NHibernate.Tool.hbm2ddl;
using NHibernate.Util;
using BaseEntity.Configuration;
using BaseEntity.Metadata;
#if NETSTANDARD2_0
using IDbConnection = System.Data.Common.DbConnection;
using IDbCommand = System.Data.Common.DbCommand;
#endif

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// This class contains the logic to generate DDL statements consistent with compiled metadata
  /// </summary>
  public class SchemaGenerator
  {
    #region Nested Classes

    class OutputWriter : IDisposable
    {
      public OutputWriter(string outFile)
      {
        if (outFile == null)
        {
          _action = Console.WriteLine;
        }
        else
        {
          _writer = GetWriter(outFile);
          if (_writer != null)
          {
            _action = _writer.WriteLine;
          }
        }
      }

      void IDisposable.Dispose()
      {
        _action = null;
        if (_writer != null)
        {
          _writer.Close();
          _writer = null;
        }
      }

      public bool IsValid
      {
        get { return (_action != null); }
      }

      public void WriteLine(string value)
      {
        _action(value);
      }

      private static StreamWriter GetWriter(string outFile)
      {
        StreamWriter sw = null;
        try
        {
          sw = new StreamWriter(outFile);
        }
        catch (SystemException ex)
        {
          Console.WriteLine(ex.Message);
        }
        return sw;
      }

      private Action<string> _action;
      private StreamWriter _writer;
    }

    #endregion

    #region Constructors
    
    /// <summary>
    /// 
    /// </summary>
    public SchemaGenerator()
    {
      _cfg = SessionFactory.Cfg;
      _connectionProperties = _cfg.Properties;
      _dialect = Dialect.GetDialect(_connectionProperties);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="outputFile"></param>
    /// <param name="exec"></param>
    /// <param name="pluginAssemblyNames"></param>
    /// <returns></returns>
		public bool Update(string outputFile, bool exec, IEnumerable<string> pluginAssemblyNames)
    {
      Console.WriteLine("Updating schema...");
      using (var writer = new OutputWriter(outputFile))
      {
        if (!writer.IsValid) return false;

        var schemaUpdate = new SchemaUpdate(_cfg);

        // For purposes of SchemaUpdate we need to remove references to DurableInstancing tables
        var fieldInfo = typeof(NHibernate.Cfg.Configuration).GetField("tables", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fieldInfo == null)
        {
          throw new DatabaseException("Unable to get tables FieldInfo");
        }
        var tables = (IDictionary<string, Table>)fieldInfo.GetValue(_cfg);
        var tablesToRemove = tables.Keys.Where(k => k.StartsWith("[System.Activities.DurableInstancing]")).ToList();
        foreach (var key in tablesToRemove)
        {
          tables.Remove(key);
        }
        
        IDictionary<string, string> props = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> de in _dialect.DefaultProperties)
        {
          props[de.Key] = de.Value;
        }

        if (_connectionProperties != null)
        {
          foreach (KeyValuePair<string, string> de in _connectionProperties)
            props[de.Key] = de.Value;
        }

        IList<Table> newTables;
        IConnectionProvider connectionProvider = null;
        DbConnection connection = null;
        try
        {
          // Open connection to load database metadata
          connectionProvider = ConnectionProviderFactory.NewConnectionProvider(props);
          connection = (DbConnection)connectionProvider.GetConnection();

          var databaseMetadata = new DatabaseMetadata(connection, _dialect);
          var defaultCatalog = PropertiesHelper.GetString(NHibernate.Cfg.Environment.DefaultCatalog, props, null);
          var defaultSchema = PropertiesHelper.GetString(NHibernate.Cfg.Environment.DefaultSchema, props, null);

          newTables = tables.Values
            .Where(table => databaseMetadata.GetTableMetadata(table.Name, table.Schema ?? defaultSchema, table.Catalog ?? defaultCatalog, table.IsQuoted) == null)
            .ToList();
        }
        finally
        {
          if (connection != null)
          {
            connectionProvider.CloseConnection(connection);
            connectionProvider.Dispose();
          }
        }

        schemaUpdate.Execute(writer.WriteLine, exec);

        if (newTables.Any())
        {
          // Create unique constraint on business key for new tables
          CreateUniqueConstraints(newTables, writer, exec);
        }
				// Populate PluginAssembly Table
				GenerateInsertPluginSql(writer, exec, pluginAssemblyNames, true);

        return true;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="outputFile"></param>
    /// <param name="exec"></param>
    /// <param name="pluginAssemblyNames"></param>
    /// <returns></returns>
    public bool Create(string outputFile, bool exec, IEnumerable<string> pluginAssemblyNames)
    {
      Console.WriteLine("Creating schema...");
      using (var writer = new OutputWriter(outputFile))
      {
        if (!writer.IsValid)
        {
          return false;
        }

        var schemaExport = new SchemaExport(_cfg, _cfg.Properties);

        // For purposes of SchemaCreate we need to remove references to DurableInstancing tables
        var fieldInfo = typeof(NHibernate.Cfg.Configuration).GetField("tables", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fieldInfo == null)
        {
          throw new DatabaseException("Unable to get tables FieldInfo");
        }
        var tables = (IDictionary<string, Table>)fieldInfo.GetValue(_cfg);
        var tablesToRemove = tables.Keys.Where(k => k.StartsWith("[System.Activities.DurableInstancing]")).ToList();
        foreach (var key in tablesToRemove)
        {
          tables.Remove(key);
        }

        schemaExport.Execute(writer.WriteLine, exec, false);

        // Get the TableMappings collection so that we can generate unique key constraints
        var propInfo = typeof(NHibernate.Cfg.Configuration).GetProperty("TableMappings", BindingFlags.NonPublic | BindingFlags.Instance);
        var tableMappings = (ICollection<Table>)propInfo.GetValue(_cfg);

        // Create unique constraints on business keys (NHibernate does not allow us to control the order for composite keys)
        CreateUniqueConstraints(tableMappings, writer, exec);

        // Create tables not described via metadata
        CreateMiscTables(writer, exec);

        // Create stored procedures
        CreateMiscStoredProcs(writer, exec);

        // Populate PluginAssembly table
				GenerateInsertPluginSql(writer,exec, pluginAssemblyNames, false);

        return true;
      }
    }

		/// <summary>
		/// Get all enabled plugin assembly file names from the plugin table
		/// </summary>
		/// <returns></returns>
		public static List<string> GetPluginAssemblyNames()
		{
			var returnList = new List<string>();

      try
      {
        using (var conn = new RawConnection())
        using (var cmd = conn.CreateCommand())
        {
          cmd.CommandText = "SELECT PluginType,FileName from PluginAssembly where Enabled=1";
          using (var reader = cmd.ExecuteReader())
          {
            while (reader.Read())
            {
              var pluginType = (PluginType) reader[0];
              if (pluginType.HasFlag(PluginType.EntityModel))
              {
                returnList.Add((string) reader[1]);
              }
            }
          }
        }
      }
      catch (SqlException ex)
      {
        Console.Error.WriteLine("Could not read plugin table: " + ex.Message);
        throw;
      }

			return returnList;
		}


  	#endregion

		#region Helper Methods

		private static Table GetTable(IEnumerable<Table> tables, string name )
    {
      foreach (var table in tables)
      {
        if (table.Name == name)
          return table;
      }
      return null;
    }

    private void CreateUniqueConstraints(ICollection<Table> tables, OutputWriter writer, bool exec)
    {
      IList<string> list = new List<string>();
      foreach (ClassMeta entity in ClassCache.FindAll())
      {
        if (entity.TableName == null)
        {
          continue;
        }
        if (entity.BaseEntity != null &&
            entity.BaseEntity.SubclassMapping == SubclassMappingStrategy.TablePerClassHierarchy)
        {
          continue;
        }

        Table table = GetTable(tables, entity.UnquotedTableName);
        if (table == null)
        {
          continue;
        }

        // Check if the business key is defined on this table or on base class.
        bool myKey = true;
        for (var baseEntity = entity.BaseEntity; baseEntity != null; baseEntity = baseEntity.BaseEntity)
        {
          if (baseEntity.TableName != null)
          {
            myKey = false;
            break;
          }
        }
        if (myKey)
        {
          // Create unique constraint on key columns
          if (entity.HasKey)
          {
            var buf = new StringBuilder($"ALTER TABLE {table.GetQualifiedName(_dialect)} ADD CONSTRAINT {entity.Name}_AltKey UNIQUE (");
            bool commaNeeded = false;
            foreach (var pm in entity.KeyPropertyList)
            {
              if (commaNeeded)
                buf.Append(", ");
              buf.Append(pm.Column);
              commaNeeded = true;
            }
            buf.Append(")");
            list.Add(buf.ToString());
          }
        }

        // Add primary key to keyed collection tables
        foreach (var pair in entity.PersistentPropertyMap)
        {
          PropertyMeta pm = pair.Value;
          if (pm.ExtendedData)
          {
            continue;
          }
          var ccpm = (pm as ComponentCollectionPropertyMeta);
          if (ccpm != null)
          {
            if (ccpm.CollectionType == "bag")
            {
              // Check to see if the property is defined at the base class
              bool myProp = true;
              for (var baseEntity = entity.BaseEntity; baseEntity != null; baseEntity = baseEntity.BaseEntity)
              {
                if (baseEntity.PersistentPropertyMap.ContainsKey(pair.Key))
                {
                  myProp = false;
                  break;
                }
              }

              if (myProp)
              {
                var childEntity = ccpm.ChildEntity;
                if (childEntity.HasChildKey)
                {
                  var joinColumn = ccpm.KeyColumns[0];
                  var sb = new StringBuilder(String.Format(
                                               "ALTER TABLE {0} ADD CONSTRAINT PK_{0} PRIMARY KEY ({1}",
                                               ccpm.TableName, joinColumn));
                  foreach (var keyProp in childEntity.ChildKeyPropertyList)
                  {
                    if (keyProp.Name == joinColumn)
                    {
                      continue;
                    }
                    sb.Append(", ");
                    sb.Append(keyProp.Column);
                  }
                  sb.Append(")");
                  list.Add(sb.ToString());
                }
              }

            }
          }
        }
      }

      foreach (string sql in list)
      {
        writer.WriteLine(sql);
      }
      if (exec)
      {
        Execute(list);
      }
    }

		private void GenerateInsertPluginSql(OutputWriter writer, bool exec, IEnumerable<string> pluginAssemblyNames, bool update)
		{
			if (pluginAssemblyNames == null)
				return;

			var list = new List<string>();

			foreach (var pluginAssemblyName in pluginAssemblyNames)
			{
				var pluginName = Path.GetFileNameWithoutExtension(pluginAssemblyName);

				var sql = string.Format(
					"INSERT INTO [PluginAssembly] (ObjectId, ObjectVersion, Name, Description, FileName, LastUpdated, UpdatedById, ValidFrom, Enabled, PluginType)" + 
					" SELECT 133*(POWER(CONVERT(BIGINT,2), 48)) + (SELECT next_hi FROM PluginAssembly_id), 1, '{0}', 'Risk Entities', '{1}', GETDATE(), " +
					"(SELECT TOP 1 ObjectId FROM [USER] WHERE RoleId in (select ObjectId from [UserRole] where Administrator=1)),'1753-01-01',1,{2}", 
          pluginName, pluginAssemblyName, (int)PluginType.EntityModel);

				if (update)
				{
					sql += string.Format(" WHERE 0 = (select count(*) from [PluginAssembly] where Name = '{0}')", pluginName);
				}

				list.Add(sql);

				list.Add("UPDATE [PluginAssembly_id] set next_hi = next_hi+256");
			}

			foreach (string sql in list)
			{
				writer.WriteLine(sql);
			}

			if (exec)
			{
				Execute(list);
			}
		}

  	/// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="exec"></param>
    private void CreateMiscTables(OutputWriter writer, bool exec)
    {
      var list = new List<string>();

      // Should not have reference to Trade_key here.
      // Need to provide a way for apps to register a plugin that
      // can specify custom schema generation commands.

      list.AddRange(new[]
                      {
                        "CREATE TABLE Trade_key (next_id int NOT NULL)",
                        "CREATE TABLE BusinessEvent_key (next_id int NOT NULL)",
                        "CREATE TABLE PnlAdjustment_key (next_id int NOT NULL)",
                        "CREATE TABLE SystemConfig (id int identity(1,1) NOT NULL, metamodel nvarchar(max) null)",
                        "INSERT INTO Trade_key VALUES (1)",
                        "INSERT INTO BusinessEvent_key VALUES (1)",
                        "INSERT INTO PnlAdjustment_key VALUES (1)",
                      });

      foreach (string sql in list)
      {
        writer.WriteLine(sql);
      }

      if (exec)
      {
        Execute(list.Where(s => s != "GO").ToList());
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="exec"></param>
    private void CreateMiscStoredProcs(OutputWriter writer, bool exec)
    {
      var list = new List<string>();

      list.AddRange(new[]
      {
        "GO", // create procedure needs to be the first command in a batch
        "create procedure InsertCommitLog @userId bigint, @comment nvarchar(140), @transactionId UNIQUEIDENTIFIER as declare @tid bigint; insert CommitLog (LastUpdated, UpdatedBy, Comment, TransactionId) select getutcdate(), @userId, @comment, @transactionId; set @tid = (select SCOPE_IDENTITY()); return @tid",
        "GO"
      });

      foreach (string sql in list)
      {
        writer.WriteLine(sql);
      }

      if (exec)
      {
        Execute(list.Where(s => s != "GO").ToList());
      }
    }
   
    /// <summary>
    /// Get list of tables known to NHibernate
    /// </summary>
    /// <remarks>
    /// We ignore tables where the schema is specified. The only case where we have this 
    /// currently is System.Activities.DurableInstancing.Instance, and this is a pseudo-table 
    /// so we do not want SchemaGenerate to attempt to generate.
    /// </remarks>
    /// <returns></returns>
    private IEnumerable<Table> GetMappedTables()
    {
      var propInfo = typeof(NHibernate.Cfg.Configuration).GetProperty("TableMappings", BindingFlags.NonPublic | BindingFlags.Instance);
      if (propInfo == null)
      {
        throw new DatabaseException("Cannot get TableMappings property for Configuration");
      }
      var tableMappings = (ICollection<Table>)propInfo.GetValue(_cfg);
      return tableMappings.Where(tm => !tm.Name.Contains(".")).ToList();
    }
    
	  /// <summary>
    /// Executes the Export of the Schema.
    /// </summary>
    private void Execute(IList<string> createSql)
    {
      IDbConnection connection = null;
      IConnectionProvider connectionProvider = null;
      IDbCommand statement = null;

      IDictionary<string, string> props = new Dictionary<string, string>();
      foreach (KeyValuePair<string, string> de in _dialect.DefaultProperties)
      {
        props[de.Key] = de.Value;
      }

      if (_connectionProperties != null)
      {
        foreach (KeyValuePair<string, string> de in _connectionProperties)
          props[de.Key] = de.Value;
      }

      try
      {
        connectionProvider = ConnectionProviderFactory.NewConnectionProvider(props);
        connection = connectionProvider.GetConnection();
        statement = connection.CreateCommand();

        for (int j = 0; j < createSql.Count; j++)
        {
          try
          {
            statement.CommandText = createSql[j];
            statement.CommandType = CommandType.Text;
            statement.ExecuteNonQuery();
          }
          catch (Exception e)
          {
            Console.WriteLine(createSql[j]);
            Console.WriteLine("Unsuccessful: " + e.Message);

            // Fail on create script errors
            throw;
          }
        }
      }
      catch (HibernateException)
      {
        throw;
      }
      catch (Exception e)
      {
        Console.Write(e.StackTrace);
        throw new HibernateException(e.Message, e);
      }
      finally
      {
        try
        {
          if (statement != null)
          {
            statement.Dispose();
          }
          if (connection != null)
          {
            connectionProvider.CloseConnection(connection);
            connectionProvider.Dispose();
          }
        }
        catch (Exception e)
        {
          Console.Error.WriteLine("Could not close connection: " + e.Message);
        }
      }
    }

    #endregion

    #region Data

    private readonly NHibernate.Cfg.Configuration _cfg;
    private readonly Dialect _dialect;
    private readonly IDictionary<string, string> _connectionProperties;
    
    #endregion
  }
}