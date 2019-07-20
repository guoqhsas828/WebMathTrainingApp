// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Data;

namespace BaseEntity.Database
{
  /// <summary>
  ///   Used to generate the next event order
  /// </summary>
  internal class BusinessEventOrderGenerator
  {
    private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(BusinessEventOrderGenerator));

    /// <summary>
    ///   Generate the next event order
    /// </summary>
    public static long Generate()
    {
      IDbConnection conn = SessionFactory.OpenConnection();

      int rows;
      try
      {
        string sql = "select next_id from BusinessEvent_key";
        IDbTransaction trans = conn.BeginTransaction();

        long result;
        do
        {
          //the loop ensures atomicity of the
          //select + update even for no transaction
          //or read committed isolation level (needed for .net?)

          IDbCommand cmd = conn.CreateCommand();
          cmd.CommandType = CommandType.Text;
          cmd.CommandText = sql;
          cmd.Connection = conn;
          cmd.Transaction = trans;

          IDataReader rs = null;
          try
          {
            rs = cmd.ExecuteReader();
            if (!rs.Read())
            {
              throw new DatabaseException("Error generating business event order");
            }

            result = Convert.ToInt32(rs[0]);
          }
          catch (Exception e)
          {
            Log.Error("could not read next_id value", e);
            throw;
          }
          finally
          {
            if (rs != null) rs.Close();
            cmd.Dispose();
          }

          sql = String.Format("update BusinessEvent_key set next_id={0} where next_id={1}", result + 1, result);
          IDbCommand ups = conn.CreateCommand();
          ups.CommandType = CommandType.Text;
          ups.CommandText = sql;
          ups.Transaction = trans;
          ups.Connection = conn;

          try
          {
            rows = ups.ExecuteNonQuery();
          }
          catch (Exception)
          {
            Log.Error("could not update next_id value");
            throw;
          }
          finally
          {
            ups.Dispose();
          }
        } while (rows == 0);

        trans.Commit();

        return result;
      }
      finally
      {
        Session.CloseConnection(conn);
      }
    }
  } // class BusinessEventOrderGenerator
}