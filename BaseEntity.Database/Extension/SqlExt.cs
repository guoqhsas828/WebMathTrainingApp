
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.Data;
using System.Data.SqlClient;

namespace BaseEntity.Database.Extension
{
  /// <summary>
  /// 
  /// </summary>
  internal static class SqlExt
  {

    /// <summary>
    /// Sets the application role on an existing, open SQL Server connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="applicationRoleName">Name of the application role.</param>
    /// <param name="applicationRolePassword">The application role password.</param>
    /// <returns>The cookie returned from SQL Server</returns>
    public static object SetApplicationRole(this SqlConnection connection, String applicationRoleName, String applicationRolePassword)
    {
      var cmd = connection.CreateCommand();
      cmd.CommandTimeout = SessionFactory.CommandTimeout;
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.CommandText = "sp_setapprole";
      cmd.Parameters.AddWithValue("@rolename", applicationRoleName);
      cmd.Parameters.AddWithValue("@password", applicationRolePassword);
      cmd.Parameters.AddWithValue("@fCreateCookie", true);
      SqlParameter cookie = cmd.Parameters.Add("@cookie", SqlDbType.VarBinary, 50);
      cookie.Direction = ParameterDirection.Output;
      cmd.ExecuteNonQuery();
      return cmd.Parameters["@cookie"].SqlValue;
    }

    /// <summary>
    /// Unsets the application role on an existing, open SQL Server connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="applicationRoleCookie">The application role cookie.</param>
    public static void UnsetApplicationRole(this SqlConnection connection, Object applicationRoleCookie)
    {
      var cmd = connection.CreateCommand();
      cmd.CommandTimeout = SessionFactory.CommandTimeout;
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.CommandText = "sp_unsetapprole";
      cmd.Parameters.AddWithValue("@cookie", applicationRoleCookie);
      cmd.ExecuteNonQuery();
    }
  }
}
