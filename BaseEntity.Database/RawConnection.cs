// 
// Copyright (c) WebMathTraining Inc 2002-2016. All rights reserved.
// 

using System;
using System.Data;
using System.Data.SqlClient;
using BaseEntity.Database.Engine;
using BaseEntity.Database.Extension;

namespace BaseEntity.Database
{
  /// <summary>
  /// Proxy for a real ADO.NET connection that also handles the setting and unsetting of a SQL Server Application Role as necessary
  /// </summary>
  public sealed class RawConnection : IDbConnection
  {
    private SqlConnection _connection;
    private readonly object _applicationRoleCookie;

    /// <summary>
    /// Initializes a new instance of the <see cref="RawConnection"/> class.
    /// </summary>
    public RawConnection()
    {
      _connection = new SqlConnection
      {
        ConnectionString = (SessionFactory.EncryptedPassword.Length > 0)
          ? CfgFactory.GetDecryptedConnectionString(SessionFactory.FactoryParams)
          : SessionFactory.ConnectString
      };

      _connection.Open();

      if (!string.IsNullOrEmpty(SessionFactory.ApplicationRoleName))
      {
        _applicationRoleCookie = _connection.SetApplicationRole(
          SessionFactory.ApplicationRoleName, CfgFactory.GetDecryptedApplicationRolePassword(
            SessionFactory.FactoryParams));
      }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting resources.
    /// </summary>
    public void Dispose()
    {
      UnsetAppRole();
      CloseConnection();
    }

    private void UnsetAppRole()
    {
      if (_applicationRoleCookie != null)
        _connection.UnsetApplicationRole(_applicationRoleCookie);
    }

    private void CloseConnection()
    {
      if (_connection != null)
      {
        _connection.Close();
        _connection = null;
      }
    }

    #region IDbConnection Members

    /// <summary>
    /// Begins a database transaction with the specified <see cref="T:System.Data.IsolationLevel"/> value.
    /// </summary>
    /// <param name="il">One of the <see cref="T:System.Data.IsolationLevel"/> values.</param>
    /// <returns>
    /// An object representing the new transaction.
    /// </returns>
    public IDbTransaction BeginTransaction(IsolationLevel il)
    {
      return _connection.BeginTransaction(il);
    }

    /// <summary>
    /// Begins a database transaction.
    /// </summary>
    /// <returns>
    /// An object representing the new transaction.
    /// </returns>
    public IDbTransaction BeginTransaction()
    {
      return _connection.BeginTransaction();
    }

    /// <summary>
    /// Changes the current database for an open Connection object.
    /// </summary>
    /// <param name="databaseName">The name of the database to use in place of the current database.</param>
    public void ChangeDatabase(string databaseName)
    {
      _connection.ChangeDatabase(databaseName);
    }

    /// <summary>
    /// Closes the connection to the database.
    /// </summary>
    public void Close()
    {
      Dispose();
    }

    /// <summary>
    /// Gets or sets the string used to open a database.
    /// </summary>
    /// <value></value>
    /// <returns>A string containing connection settings.</returns>
    public string ConnectionString
    {
      get { return _connection.ConnectionString; }
      set { _connection.ConnectionString = value; }
    }

    /// <summary>
    /// Gets the time to wait while trying to establish a connection before terminating the attempt and generating an error.
    /// </summary>
    /// <value></value>
    /// <returns>The time (in seconds) to wait for a connection to open. The default value is 15 seconds.</returns>
    public int ConnectionTimeout
    {
      get { return _connection.ConnectionTimeout; }
    }

    /// <summary>
    /// 
    /// </summary>
    public SqlConnection Connection
    {
      get { return _connection; }
    }

    /// <summary>
    /// Creates and returns a Command object associated with the connection.
    /// </summary>
    /// <returns>
    /// A Command object associated with the connection.
    /// </returns>
    public IDbCommand CreateCommand()
    {
      var cmd = _connection.CreateCommand();
      cmd.CommandTimeout = SessionFactory.CommandTimeout;
      return cmd;
    }

    /// <summary>
    /// Gets the name of the current database or the database to be used after a connection is opened.
    /// </summary>
    /// <value></value>
    /// <returns>The name of the current database or the name of the database to be used once a connection is open. The default value is an empty string.</returns>
    public string Database
    {
      get { return _connection.Database; }
    }

    /// <summary>
    /// Opens a database connection with the settings specified by the ConnectionString property of the provider-specific Connection object.
    /// </summary>
    public void Open()
    {
      _connection.Open();
    }

    /// <summary>
    /// Gets the current state of the connection.
    /// </summary>
    /// <value></value>
    /// <returns>One of the <see cref="T:System.Data.State"/> values.</returns>
    public ConnectionState State
    {
      get { return _connection.State; }
    }

    /// <summary>
    /// 
    /// </summary>
    public SqlConnection Impl
    {
      get { return _connection; }
    }

    #endregion
  }
}