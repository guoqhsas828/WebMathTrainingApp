// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Xml;
using log4net;
using NHibernate;
using NHibernate.Engine;
using NHibernate.Event;
using NHibernate.Id;
using NHibernate.Mapping;
using NHibernate.Metadata;
using NHibernate.Type;
using BaseEntity.Configuration;
using BaseEntity.Database.Engine;
using BaseEntity.Database.Types;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using Environment = System.Environment;

namespace BaseEntity.Database
{
  /// <summary>
  ///   This class is the primary interface for database access
  /// </summary>
  public static class SessionFactory
  {
    private static SessionFactoryParams InitSessionFactoryParams()
    {
      var fp = new SessionFactoryParams {Dialect = "MsSql2005"};

      var configXml = Configurator.GetConfigXml("Database", null);
      if (configXml == null)
      {
        throw new DatabaseException("No 'Database' element found!");
      }

      foreach (XmlNode childNode in configXml.ChildNodes)
      {
        if (childNode.NodeType != XmlNodeType.Element)
        {
          continue;
        }

        string n = childNode.Name;
        string v = childNode.InnerText;

        switch (n)
        {
          case "CommandTimeout":
            int ival;
            if (!Int32.TryParse(v, out ival) || ival < 0)
            {
              throw new DatabaseException($"Invalid CommandTimeout setting [{v}]");
            }
            fp.CommandTimeout = ival;
            break;

          case "ConnectString":
            fp.ConnectString = v;
            break;

          case "DefaultSchema":
            fp.DefaultSchema = v;
            break;

          case "Password":
            fp.Password = v;
            break;

          case "ApplicationRole":
            if (childNode.Attributes == null ||
                childNode.Attributes["name"] == null ||
                childNode.Attributes["password"] == null)
            {
              throw new DatabaseException($"Invalid ApplicationRole setting [{childNode.InnerText}]");
            }
            fp.AppRoleName = childNode.Attributes["name"].Value;
            fp.AppRolePassword = childNode.Attributes["password"].Value;
            break;

          default:
            throw new DatabaseException($"Invalid config element: {n}");
        }
      }

      // Allow override of config setting from command-line or environment

      string connectStr = GetEnvironmentVariable("WebMathTraining_CONNECT_STRING");
      if (!String.IsNullOrEmpty(connectStr))
      {
        fp.ConnectString = connectStr.Trim('"');
      }
      else
      {
        string[] cmdLineArgs = Environment.GetCommandLineArgs();

        int argInd;
        for (argInd = 1; argInd < cmdLineArgs.Length; argInd++)
        {
          string arg = cmdLineArgs[argInd];
          if (arg.StartsWith("--qConnectString="))
          {
            int idx = arg.IndexOf('=') + 1;
            string strValue = arg.Substring(idx);
            fp.ConnectString = strValue.Trim('"');
          }
        }

        // Set standard command-line arguments
        List<CmdLineOption> options = CmdLineParser.GetStandardOptions();
        options.Add(new StringCmdLineOption("--qConnectString", "ConnectString", "Database connection string"));
        CmdLineParser.SetStandardOptions(options);
      }

      string appRoleNameEnv = GetEnvironmentVariable("WebMathTraining_APP_ROLE_NAME");
      if (StringUtil.HasValue(appRoleNameEnv))
      {
        fp.AppRoleName = appRoleNameEnv;
      }

      string appRolePassEnv = GetEnvironmentVariable("WebMathTraining_APP_ROLE_PWD");
      if (StringUtil.HasValue(appRolePassEnv))
      {
        fp.AppRolePassword = appRolePassEnv;
      }

      return fp;
    }

    private static string _signature;
    private static NHibernate.Cfg.Configuration InitCfg()
    {
      var cfgFactory = new CfgFactory(FactoryParams);
      var cfg = cfgFactory.GetConfiguration();
      _signature = cfgFactory.Signature;
      foreach (var p in cfg.ClassMappings.SelectMany(cm => cm.PropertyIterator))
      {
          var manyToOne = p.Value as ManyToOne;
          if (manyToOne != null)
          {
            var fieldInfo = manyToOne.GetType().GetField("type", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
            {
              throw new DatabaseException("Unable to reference type field on ManyToOne");
            }
            var oldType = manyToOne.Type;
            var newType = new ObjectRefType(oldType.Name);
            fieldInfo.SetValue(manyToOne, newType);
          }
          else
          {
            var component = p.Value as Component;
            if (component != null)
              ReplaceManyToOneType(component);
          }
        }
      cfg.BuildMappings();
      foreach (var cm in cfg.CollectionMappings)
      {
        var component = cm.Element as Component;
        if (component != null)
          ReplaceManyToOneType(component);
      }
      return cfg;
    }

    /// <summary>
    /// Gets the schema signature.
    /// </summary>
    /// <value>
    /// The schema signature.
    /// </value>
    public static string Signature {
      get
      {
        var cfg = Cfg;
        Logger.InfoFormat("Cfg: {0}", cfg);
        return _signature;
      } 
    }

    private static void ReplaceManyToOneType(Component ce)
    {
      var fieldInfo = ce.Type.GetType().GetField("propertyTypes", BindingFlags.NonPublic | BindingFlags.Instance);
      if (fieldInfo == null)
      {
        throw new DatabaseException("Unable to references propertyTypes");
      }
      var propertyTypes = (IType[])fieldInfo.GetValue(ce.Type);
      for (int i = 0; i < propertyTypes.Length; ++i)
      {
        var propertyType = propertyTypes[i] as ManyToOneType;
        if (propertyType != null)
          propertyTypes[i] = new ObjectRefType(propertyType.Name);
      }
    }

    private static ISessionFactory InitSessionFactory()
    {
      var cfg = Cfg;

      // At this point the ClassCache has been initialized so we can compare our saved metamodel w/ or current one.
      if (!string.Equals(AppDomain.CurrentDomain.FriendlyName, "SchemaUtil.exe", StringComparison.InvariantCultureIgnoreCase))
      {
        var saved = LoadMetaModel();
        if (saved != null)
        {
          var current = ClassCache.PrintMetaModel();
          if (saved != current)
            throw new DatabaseException("Saved metamodel does not match current metamodel!");
        }
      }

      cfg.EventListeners.LoadEventListeners = new ILoadEventListener[] {new MyLoadEventListener()};
      cfg.EventListeners.InitializeCollectionEventListeners = new IInitializeCollectionEventListener[] {new MyInitializeCollectionEventListener()};
      cfg.EventListeners.FlushEventListeners = new IFlushEventListener[] {new MyFlushEventListener()};

      foreach (var callback in LazyInitActions)
      {
        callback(cfg);
      }

      LazyInitActions.Clear();

      return cfg.BuildSessionFactory();
    }

    /// <summary>
    /// Register callback to be invoked when the Configuration instance is initialized
    /// </summary>
    /// <param name="action"></param>
    public static void RegisterInitAction(Action<NHibernate.Cfg.Configuration> action)
    {
      if (LazyCfg.IsValueCreated)
      {
        throw new InvalidOperationException("Attempt to initialize already initialized SessionFactory");
      }

      LazyInitActions.Add(action);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static string LoadMetaModel()
    {
      string metaModel = null;
      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT metamodel FROM SystemConfig";
        using (var reader = cmd.ExecuteReader())
        {
          if (reader.Read())
            metaModel = reader[0] as string;
        }
      }
      return metaModel;
    }

    /// <summary>
    /// 
    /// </summary>
    public static IDbConnection OpenConnection()
    {
      return ((ISessionFactoryImplementor)LazyFactory.Value).ConnectionProvider.GetConnection();
    }

    /// <summary>
    /// </summary>
    public static void CloseConnection(IDbConnection conn)
    {
      ((ISessionFactoryImplementor)LazyFactory.Value).ConnectionProvider.CloseConnection(conn as System.Data.Common.DbConnection);
    }

    /// <summary>
    /// Open new NHibernate session and begin a transaction
    /// </summary>
    /// <returns>ISession</returns>
    /// <exclude />
    internal static ISession OpenSession(NHibernateEntityContext context)
    {
      return Factory.OpenSession(new AuditInterceptor(context));
    }

    /// <summary>
    /// Open new NHibernate stateless session, but do not begin a transaction. 
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// Stateless sessions in NH are normally used for bulk operations. 
    /// They lack a first-level cache that stateful sessions possess.
    /// </remarks>
    internal static IStatelessSession OpenStatelessSession()
    {
      return Factory.OpenStatelessSession();
    }

    /// <summary>
    /// Open a new NHibernate stateless session for the given connection,
    /// but do not begin a transaction. 
    /// </summary>
    /// <returns></returns>
    /// <param name="conn">
    /// The <see cref="IDbConnection">IDbConnection</see> on which to open the Stateless Session.
    /// </param>
    /// <remarks>
    /// Stateless sessions in NH are normally used for bulk operations. 
    /// They lack a first-level cache that stateful sessions possess.
    /// </remarks>
    internal static IStatelessSession OpenStatelessSession(IDbConnection conn)
    {
      return Factory.OpenStatelessSession(conn as System.Data.Common.DbConnection);
    }

    private static string GetEnvironmentVariable(string name)
    {
      return Environment.GetEnvironmentVariable(name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="classMeta"></param>
    /// <returns></returns>
    internal static IClassMetadata GetClassMetadata(ClassMeta classMeta)
    {
      return Factory.GetClassMetadata(classMeta.FullName);
    }

    /// <summary>
    /// 
    /// </summary>
    public static IIdentifierGenerator GetIdGenerator(Type type)
    {
      var entity = ClassCache.Find(type);
      var baseEntity = entity.BaseEntity ?? entity;
      var factoryImpl = (ISessionFactoryImplementor)Factory;
      return factoryImpl.GetIdentifierGenerator(baseEntity.FullName);
    }

    /// <summary>
    ///   This method returns the .Net Data Provider (currently always System.Data.SqlClient)
    /// </summary>
    /// <returns>
    ///   Currently implements only
    /// </returns>
    public static string GetProvider()
    {
      return "System.Data.SqlClient";
    }

    #region Properties

    /// <summary>
    ///   Name for the Application Role if using SQL Server
    /// </summary>
    public static string ApplicationRoleName => LazyFactoryParams.Value.AppRoleName;

    /// <summary>
    ///   Password for the Application Role if using SQL Server
    /// </summary>
    public static string ApplicationRolePassword => LazyFactoryParams.Value.AppRolePassword;

    /// <summary>
    /// 
    /// </summary>
    internal static SessionFactoryParams FactoryParams => LazyFactoryParams.Value;

    /// <summary>
    /// 
    /// </summary>
    public static NHibernate.Cfg.Configuration Cfg => LazyCfg.Value;

    /// <summary>
    /// 
    /// </summary>
    internal static ISessionFactory Factory => LazyFactory.Value;

    /// <summary>
    /// If set, this will be used to fully qualify table names
    /// </summary>
    public static string DefaultSchema => LazyFactoryParams.Value.DefaultSchema;

    /// <summary>
    /// Used to configure database connection parameters
    /// </summary>
    /// <remarks>
    /// <para>ConnectString is of the form:</para>
    /// <para>Server={server};initial catalog={database};User ID={login};Password={password}</para>
    /// </remarks>
    public static string ConnectString => LazyFactoryParams.Value.ConnectString;

    /// <summary>
    /// SQL dialect
    /// </summary>
    /// <remarks>
    /// Currently, the only valid value is "MsSql2000".
    /// </remarks>
    public static string Dialect => LazyFactoryParams.Value.Dialect;

    /// <summary>
    /// Used to configure the default timeout for database queries
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static int CommandTimeout => LazyFactoryParams.Value.CommandTimeout;

    /// <summary>
    /// Maximum number of parameters to use in a single parameterized query.
    /// </summary>
    /// <remarks>
    /// <para>For now, just hard-code a value appropriate for SQL Server.  When we add
    /// support for other servers we will need to make this server dependent.</para>
    /// </remarks>
    public static int BatchSize => 2000;

    /// <summary>
    /// If using encryption for login this is the encrypted password string from WebMathTraining.xml
    /// </summary>
    public static string EncryptedPassword => LazyFactoryParams.Value.Password;

    /// <summary>
    /// Value to represent a psuedo null DateTime in the database.
    /// This is the value for a DateTime that has not been given a value but is saved to the db.
    /// </summary>
    public static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1, 0, 0, 0);

    /// <summary>
    /// Value to represent a psuedo null UTC DateTime in the database.
    /// This is the value for a UTC DateTime that has not been given a value but is saved to the db.
    /// </summary>
    public static readonly DateTime SqlMinDateUtc = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    #endregion

    #region Data

    private static readonly Lazy<SessionFactoryParams> LazyFactoryParams =
      new Lazy<SessionFactoryParams>(InitSessionFactoryParams);

    private static readonly Lazy<NHibernate.Cfg.Configuration> LazyCfg =
      new Lazy<NHibernate.Cfg.Configuration>(InitCfg);

    private static readonly List<Action<NHibernate.Cfg.Configuration>> LazyInitActions =
      new List<Action<NHibernate.Cfg.Configuration>>();

    private static readonly Lazy<ISessionFactory> LazyFactory =
      new Lazy<ISessionFactory>(InitSessionFactory);

    private static readonly ILog Logger = LogManager.GetLogger(typeof(SessionFactory));

    #endregion
  }
}