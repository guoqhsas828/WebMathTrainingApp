/*
 * TradeIdGenerator.cs
 *
 *
 */

using System;
using System.Data;
using System.Linq;
using BaseEntity.Core.Logging;
using BaseEntity.Database;
using log4net;

namespace BaseEntity.Risk
{
	/// <summary>
	/// </summary>
	public class TradeIdGenerator
	{
		private static readonly ILog log = QLogManager.GetLogger(typeof(TradeIdGenerator));

		/// <summary>
		///   Modified hi/lo algorithm
		/// </summary>
		public virtual string Generate(Trade trade, bool transient)
		{
			return (transient)? GenerateTransient(trade): Generate(trade.GetType());
		}

	  /// <summary>
	  ///   Modified hi/lo algorithm
	  /// </summary>
	  public virtual string Generate(Trade trade)
	  {
	    return Generate((trade.GetType()));
	  }

		/// <summary>
		///   Modified hi/lo algorithm
		/// </summary>
    public virtual string Generate(Type tradeType)
		{
		  if (!typeof (Trade).IsAssignableFrom(tradeType))
		    throw new RiskException("Specified Type [" + tradeType.Name + "] is not a valid Trade Type");

      IDbConnection conn = SessionFactory.OpenConnection();
		  var attr = (ProductAttribute)tradeType.GetCustomAttributes(typeof(ProductAttribute), true).FirstOrDefault();
		  string productTypeName = !String.IsNullOrWhiteSpace(attr.TradeIdAlias) ? attr.TradeIdAlias : attr.ProductType.Name;

		  try
      {
        int result;
        int rows;

        string sql = "select next_id from Trade_key";

        IDbTransaction trans = conn.BeginTransaction();

        do
        {
          //the loop ensures atomicity of the
          //select + update even for no transaction
          //or read committed isolation level (needed for .net?)

          IDbCommand qps = conn.CreateCommand();
          qps.CommandType = CommandType.Text;
          qps.CommandText = sql;
          qps.Connection = conn;
          qps.Transaction = trans;

          IDataReader rs = null;
          try
          {
            rs = qps.ExecuteReader();
            if (!rs.Read())
            {
              throw new DatabaseException("Error generating trade id");
            }

            result = Convert.ToInt32(rs[0]);
          }
          catch (Exception e)
          {
            log.Error("could not read next_id value", e);
            throw;
          }
          finally
          {
            if (rs != null) rs.Close();
            qps.Dispose();
          }

          sql = String.Format("update Trade_key set next_id={0} where next_id={1}", result + 1, result);
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
            log.Error("could not update next_id value");
            throw;
          }
          finally
          {
            ups.Dispose();
          }

        }
        while (rows == 0);

        trans.Commit();

        return String.Format("{0}{1:D7}", productTypeName, result);
      }
      finally
      {
        Session.CloseConnection(conn);
      }
		}

	  /// <summary>
		///  Generate transient ProductName (only good for life of this process)
		/// </summary>
		/// <param name="trade"></param>
		/// <returns>string</returns>
		private string GenerateTransient(Trade trade)
		{
			nextTransientId_++;

			return nextTransientId_.ToString();
		}

		private int nextTransientId_ = 0;

	} // class TradeIdGenerator
}  
