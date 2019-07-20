
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using NHibernate.Connection;
using NHibernate.Util;
using log4net;
using BaseEntity.Database.Extension;
using BaseEntity.Shared;
using System.Threading;
using BaseEntity.Configuration;
using IDbConnection = System.Data.Common.DbConnection;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// Extension to NHibernate's DriverConnectionProvider that handles setting and unsetting of a SQL Server application role.
  /// </summary>
  public sealed class SqlApplicationRoleConnectionProvider : DriverConnectionProvider
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(SqlApplicationRoleConnectionProvider));

    private String _applicationRoleName;
    private String _applicationRolePassword;
    private Boolean _useApplicationRole;

    // Cache the cookies of each connection on which we set the application role.
    // NOTE: The SqlConnection returned by ADO.NET will potentially be a different wrapper even when the actual SQL Server
    // connection (from the connection pool) was recycled, so the hash code we use here is ONLY useful for subsequently looking up
    // the cookie in order to finally unset the application role, and not for determining whether or not the application role has been
    // previously set.
    private readonly ConcurrentDictionary<int, object> _appRoleCookieCache = new ConcurrentDictionary<int, object>();

    /// <summary>
    /// Gets a new open <see cref="IDbConnection"/> through
    /// the <see cref="NHibernate.Driver.IDriver"/>.
    /// </summary>
    /// <returns>An Open <see cref="IDbConnection"/>.</returns>
    /// <exception cref="Exception">
    /// If there is any problem creating or opening the <see cref="IDbConnection"/>.
    /// </exception>
    public override System.Data.Common.DbConnection GetConnection()
    {
      var conn = base.GetConnection();
      if (_useApplicationRole)
      {
        var sqlConn = conn as SqlConnection;
        if (sqlConn != null)
        {
          SetApplicationRole(sqlConn);
        }
      }
      return conn;
    }

    /// <summary>
    /// Closes and Disposes of the <see cref="IDbConnection"/>.
    /// </summary>
    /// <param name="conn">The <see cref="IDbConnection"/> to clean up.</param>
    public override void CloseConnection(IDbConnection conn)
    {
      if (_useApplicationRole)
      {
        var sqlConn = conn as SqlConnection;
        if (sqlConn != null)
        {
          UnsetApplicationRole(sqlConn);
        }
      }
      base.CloseConnection(conn);
    }

    /// <summary>
    /// Configures the ConnectionProvider with the Driver and the ConnectionString.
    /// </summary>
    /// <param name="settings">An <see cref="System.Collections.IDictionary"/> that contains the settings for this ConnectionProvider.</param>
    /// <exception cref="NHibernate.HibernateException">
    /// Thrown when a <see cref="NHibernate.Cfg.Environment.ConnectionString"/> could not be found
    /// in the <c>settings</c> parameter or the Driver Class could not be loaded.
    /// </exception>
    public override void Configure(IDictionary<String, String> settings)
    {
      base.Configure(settings);

      Initialize(PropertiesHelper.GetString("connection.application_role_name", settings, ""), 
                 PropertiesHelper.GetString("connection.application_role_password", settings, ""));
    }

    private void Initialize(String applicationRoleName, String applicationRolePassword)
    {
      _applicationRoleName = applicationRoleName;
      _applicationRolePassword = applicationRolePassword;
      _useApplicationRole = !String.IsNullOrEmpty(_applicationRoleName) ||
                            !String.IsNullOrEmpty(_applicationRolePassword);
    }

    private void SetApplicationRole(SqlConnection connection)
    {
      Log.DebugFormat("Setting Application Role for connection: {0} [{1}]", connection.GetHashCode(), connection.ConnectionString);
      Log.Verbose(string.Format("Connection originated via: {0}", Environment.StackTrace));

      var appRoleCookie = connection.SetApplicationRole(_applicationRoleName, _applicationRolePassword);

      Log.DebugFormat("Application Role cookie: {0}", appRoleCookie);

      var key = connection.GetHashCode();
      if (!_appRoleCookieCache.TryAdd(key, appRoleCookie))
      {
        throw new DatabaseException("Invalid operation: _appRoleCookieCache already contains a cookie for connection: " + key);
      }
    }

    private void UnsetApplicationRole(SqlConnection connection)
    {
      var key = connection.GetHashCode();

      object cookie;
      if (!_appRoleCookieCache.TryRemove(key, out cookie))
      {
        return;
      }

      Log.DebugFormat("Unsetting Application Role for connection: {0} [{1}]", key, cookie);

      connection.UnsetApplicationRole(cookie);
    }
  }
}
