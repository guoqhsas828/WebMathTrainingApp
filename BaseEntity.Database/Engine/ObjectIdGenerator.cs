
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.Data;
using System.Runtime.CompilerServices;
using System.Collections;
using NHibernate.Id;
using NHibernate.Engine;
using NHibernate.Dialect;
using NHibernate.SqlCommand;
using NHibernate.SqlTypes;
using NHibernate.Type;
using NHibernate.Util;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Metadata;
using System.Threading.Tasks;
using System.Threading;
using IDbConnection = System.Data.Common.DbConnection;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  ///  Used to generate unique ObjectId values using a modified hi/lo algorithm.
  /// </summary>
  /// <remarks>
  /// <para>An ObjectId is a bitmask that is constructed as follows:</para>
  /// <para>[ entity id (16 bits) ][ hi (40 bits) ][ lo (8 bits) ]</para>
  /// <para>The entity id part is the same for all objects of the same type.</para>
  /// <para>The "hi" part is obtained by incrementing a shared counter (this counter
  /// is stored in the "next_hi" column in the "id" table for this type.</para>
  /// <para>The "lo" part is obtained by incrementing a private counter (this 
  /// counter is kept in memory and is initialized when the generator is
  /// constructed.</para>
  /// <para>When the generator increments the "hi" counter, it is reserving a 
  /// block of 256 id values that it can use before it needs to increment the 
  /// shared counter again.  This does leave a lot of gaps in the id sequence, 
  /// however this is not a problem as there are sufficient bits in the "hi" 
  /// portion alone that we do not expect to run out of id values.</para>
  /// </remarks>
  public class ObjectIdGenerator : IPersistentIdentifierGenerator, IConfigurable
  {
    private static readonly log4net.ILog logger_ = log4net.LogManager.GetLogger(typeof(ObjectIdGenerator));

    // This is the number of id values we are reserving.  For now, we use
    // a constant value, but in the future it may make sense to make this
    // configurable per entity, or even to provide an interface to allow
    // callers to reserve an arbitrary block of id's (for example, for
    // bulk loading data).
    private const long NumLo = 256;

    private long entityId_;
    private string tableName_;
    private string query_;
    private SqlString updateSql_;
    private long entityPart_;
    private long hiPart_;
    private long loPart_;

    #region IConfigurable Members

    /// <summary>
    /// Configures the ObjectIdGenerator
    /// </summary>
    /// <param name="type">The <see cref="IType"/> the identifier should be.</param>
    /// <param name="parms">An <see cref="IDictionary"/> of Param values that are keyed by parameter name.</param>
    /// <param name="dialect">The <see cref="Dialect"/> to help with Configuration.</param>
    public void Configure(IType type, System.Collections.Generic.IDictionary<string, string> parms, Dialect dialect)
    {
      tableName_ = PropertiesHelper.GetString("table", parms, null);
      entityId_ = long.Parse(PropertiesHelper.GetString("entity", parms, null));
		  
      query_ = "select next_hi from " + tableName_;

      // Build the parameterized update SQL     
      //Parameter setParam = new Parameter( "next_hi", SqlTypeFactory.Int64 );
      //Parameter whereParam = new Parameter("next_hi", SqlTypeFactory.Int64);

      var builder = new SqlStringBuilder();
      builder.Add("update " + tableName_ + " set next_hi = ").Add(Parameter.Placeholder).Add(" where next_hi = ").Add(Parameter.Placeholder);
      updateSql_ = builder.ToSqlString();

      entityPart_ = (entityId_ == 0) ? 0 : entityId_ << 48;
      loPart_ = NumLo;
    }

    #endregion

    #region IIdentifierGenerator Members

    /// <summary>
    /// Generate unique id for this object instance
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public object Generate(ISessionImplementor session, object obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException("obj");
      }

      Type type = obj.GetType();

      if (loPart_ == NumLo) 
      {
        long hiVal = GetHi(session);
        hiPart_ = hiVal;
        loPart_ = 0;
      }

      // Form object id from its parts

      long entityPart;
      if (entityPart_ != 0)
      {
        entityPart = entityPart_;
      }
      else
      {
        // The entity id will only be stored with the generator
        // for table-per-concrete-class mappings.  In all other
        // cases, we need to get it from the object meta.
        var entity = ClassCache.Find(type);
        long entityId = entity.EntityId;
        if (entityId == 0)
        {
          throw new ArgumentException(String.Format(
            "Cannot generate ObjectId for Entity [{0}] : no EntityId", entity.Name));
        }
        entityPart = entityId << 48;
      }

      long objectId = entityPart | hiPart_ | loPart_;

      loPart_++;

      return objectId;
    }

    /// <summary>
    /// Generate unique id for this object instance
    /// </summary>
    [MethodImpl]
    public async Task<object> GenerateAsync(ISessionImplementor session, object obj, CancellationToken token)
    {
      return Task.Run(() => Generate(session, obj));
    }

    /// <summary>
    /// Get next unique hi value
    /// </summary>
    private long GetHi( ISessionImplementor session)
    {
      IDbConnection conn = session.Factory.ConnectionProvider.GetConnection();

      try
      {
        IDbTransaction trans = conn.BeginTransaction();

        long result;
        int rows;
        do
        {
          IDbCommand qps = conn.CreateCommand();
          IDataReader rs = null;
          qps.CommandText = query_;
          qps.CommandType = CommandType.Text;
          qps.Transaction = trans;
          try
          {
            rs = qps.ExecuteReader();
            if( !rs.Read() )
            {
              LogUtil.ThrowException(logger_, "Could not read a hi value - you need to populate the table: " + tableName_);
            }
            result = Convert.ToInt64( rs[ 0 ] );
            if ((result%NumLo) != 0)
            {
              LogUtil.ThrowException(logger_, "Invalid next_hi value [" + result + "] for table [" + tableName_ + "]");
            }
          } 
          catch( Exception e )
          {
            logger_.Error( "could not read a hi value", e );
            throw;
          }
          finally
          {
            if ( rs != null ) rs.Close();
            qps.Dispose();
          }

          IDbCommand ups = session.Factory.ConnectionProvider.Driver.GenerateCommand(CommandType.Text, updateSql_, new [] { SqlTypeFactory.Int64, SqlTypeFactory.Int64 });
          ups.Connection = conn;
          ups.Transaction = trans;

          try
          {
            ((IDbDataParameter)ups.Parameters[0]).Value = result + NumLo;
            ((IDbDataParameter) ups.Parameters[1]).Value = result;
            rows = ups.ExecuteNonQuery();
          } 
          catch( Exception e )
          {
            logger_.Error( "could not update hi value in: " + tableName_, e );
            throw;
          }
          finally
          {
            ups.Dispose();
          }

        }
        while( rows == 0 );

        trans.Commit();

        return result;
      }
      finally
      {
        session.Factory.ConnectionProvider.CloseConnection( conn );
      }
    }

    #endregion

    #region IPersistentIdentifierGenerator Members
		
    /// <summary>
    /// The SQL required to create the database objects for an ObjectIdGenerator
    /// </summary>
    /// <param name="dialect">The <see cref="Dialect"/> to help with creating the sql.</param>
    /// <returns>
    /// An array of <see cref="String"/> objects that contain the Dialect specific sql to 
    /// create the necessary database objects and to create the first value as <c>1</c> 
    /// for the TableGenerator.
    /// </returns>
    public string[ ] SqlCreateStrings( Dialect dialect )
    {
      return new[]
               {
                 "CREATE TABLE " + tableName_ + " ( next_hi " + dialect.GetTypeName(SqlTypeFactory.Int64) + " )",
                 "INSERT INTO " + tableName_ + " VALUES ( 0 )"
               };
    }

    /// <summary>
    /// The SQL required to remove the underlying database objects for a TableGenerator.
    /// </summary>
    /// <param name="dialect">The <see cref="Dialect"/> to help with creating the sql.</param>
    /// <returns>
    /// A <see cref="String"/> that will drop the database objects for the TableGenerator.
    /// </returns>
    public string[] SqlDropString( Dialect dialect )
    {
      return new[] {dialect.GetDropTableString(tableName_)};
    }

    /// <summary>
    /// Return a key unique to the underlying database objects for a TableGenerator.
    /// </summary>
    /// <returns>
    /// The configured table name.
    /// </returns>
    public string GeneratorKey()
    {
      return tableName_;
    }
		
    #endregion
  }
}