// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using NHibernate;
using NHibernate.Type;
using BaseEntity.Metadata;
#if NETSTANDARD2_0
using IDataReader = System.Data.Common.DbDataReader;
using IDbCommand = System.Data.Common.DbCommand;
#endif

namespace BaseEntity.Database
{
  /// <summary>
  /// Represents a database session
  /// </summary>
  public static class Session
  {
    /// <summary>
    /// Returns the <see cref="NHibernateEntityContext"/> bound to the current thread
    /// </summary>
    /// <returns></returns>
    public static NHibernateEntityContext EntityContext
    {
      get
      {
        var context = BaseEntity.Metadata.EntityContext.Current;
        if (context == null)
        {
          throw new DatabaseException("No current EntityContext");
        }
        var nhibernateEntityContext = context as NHibernateEntityContext;
        if (nhibernateEntityContext == null)
        {
          throw new DatabaseException("Invalid EntityContext type [" + context.GetType() + "]");
        }
        return nhibernateEntityContext;
      }
    }

    #region SessionFactory Members

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static IDbConnection OpenConnection()
    {
      return SessionFactory.OpenConnection();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conn"></param>
    public static void CloseConnection(IDbConnection conn)
    {
      SessionFactory.CloseConnection(conn);
    }

    #endregion

    #region NHibernateEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    public static IDbConnection Connection
    {
      get { return EntityContext.Connection; }
    }

    /// <summary>
    /// 
    /// </summary>
    public static DateTime AsOf
    {
      get { return EntityContext.AsOf; }
    }

    /// <summary>
    /// 
    /// </summary>
    public static ReadWriteMode ReadWriteMode
    {
      get { return EntityContext.ReadWriteMode; }
    }

    /// <summary>
    /// 
    /// </summary>
    public static Guid TransactionId
    {
      get { return EntityContext.TransactionId; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exclude />
    public static void Close()
    {
      EntityContext.Close();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exclude />
    public static void Flush()
    {
      EntityContext.Flush();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// <exclude />
    public static bool IsDirty
    {
      get { return EntityContext.IsDirty; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exclude />
    public static bool Contains(object obj)
    {
      return EntityContext.Contains(obj);
    }

    /// <summary>
    /// Perform an HQL query and return a list of objects
    /// </summary>
    /// <param name="query">HQL query to perform</param>
    /// <returns>List of objects. If not objects found an empty list is returned</returns>
    public static IList Find(string query)
    {
      return EntityContext.CreateQuery(query).List();
    }

    /// <summary>
    /// Perform an HQL query and return a list of objects. Allows one parameter for the HQL string.
    /// </summary>
    /// <param name="query">HQL query to perform</param>
    /// <param name="value">object to format within the HQL string</param>
    /// <param name="type">type of the value parameter</param>
    /// <returns>List of objects. If not objects found an empty list is returned</returns>
    public static IList Find(string query, object value, IType type)
    {
      return EntityContext.CreateQuery(query).SetParameter(0, value, type).List();
    }

    /// <summary>
    /// Perform an HQL query and return a list of objects. Allows multiple parameters for the HQL string.
    /// </summary>
    /// <param name="query">HQL query to perform</param>
    /// <param name="values">objects to format within the HQL string</param>
    /// <param name="types">types of the value parameters</param>
    /// <returns>List of objects. If not objects found an empty list is returned</returns>
    public static IList Find(string query, object[] values, IType[] types)
    {
      return EntityContext.Find(query, values, types);
    }

    /// <summary>
    /// Create a new instance of <c>Query</c> for the given query string
    /// </summary>
    /// <param name="queryString">A hibernate query string</param>
    /// <returns>The query</returns>
    public static IQuery CreateQuery(string queryString)
    {
      return EntityContext.CreateQuery(queryString);
    }

    /// <summary>
    /// Create a new instance of <c>Query</c> for the given query string
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="returnAlias"></param>
    /// <param name="returnClass"></param>
    public static IQuery CreateSqlQuery(string sql, string returnAlias, Type returnClass)
    {
      return EntityContext.CreateSqlQuery(sql, returnAlias, returnClass);
    }

    /// <summary>
    /// Create a new instance of <see cref="ISQLQuery" /> for the given SQL query string.
    /// </summary>
    /// <param name="queryString"></param>
    /// <returns></returns>
    /// <exclude />
    public static ISQLQuery CreateSqlQuery(string queryString)
    {
      return EntityContext.CreateSqlQuery(queryString);
    }

    /// <summary>
    /// Gets the instance of the object with the specified object id and type.
    /// The object will only be loaded from the database if it is not already in this session.
    /// </summary>
    /// <typeparam name="T">Type of object to find</typeparam>
    /// <param name="id">Object id of the object to find</param>
    /// <returns>null if object is not found</returns>
    /// <exclude />
    public static T Get<T>(object id) where T : PersistentObject
    {
      return (T)EntityContext.Get(typeof(T), id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    public static PersistentObject Get(long objectId)
    {
      return (PersistentObject)Get(EntityHelper.GetClassFromObjectId(objectId), objectId);
    }

    /// <summary>
    /// Gets the instance of the object with the specified object id and type.
    /// The object will only be loaded from the database if it is not already in this session.
    /// </summary>
    /// <param name="theType">Type of object to find</param>
    /// <param name="id">Object id of the object to find</param>
    /// <returns>null if object is not found</returns>
    public static object Get(Type theType, object id)
    {
      return EntityContext.Get(theType, id);
    }

    /// <summary>
    /// Similar to Get method but throws an exception if the object is not found
    /// </summary>
    /// <returns>object requested</returns>
    /// <exclude />
    public static object Load(Type theType, object id)
    {
      return EntityContext.Load(theType, id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exclude />
    public static void Refresh(Object obj)
    {
      EntityContext.Refresh(obj);
    }

    /// <summary>
    /// Marks the object in this session to be deleted from the database when CommitTransaction is called
    /// </summary>
    public static void Delete(object obj)
    {
      EntityContext.Delete(obj);
    }

    /// <summary>
    /// Bulk insert records into specified DataTable
    /// </summary>
    /// <param name="dataTable"></param>
    /// <remarks>
    ///  This function bypasses the ORM layer.
    /// </remarks>
    public static void BulkInsert(DataTable dataTable)
    {
      EntityContext.BulkInsert(dataTable);
    }

    /// <summary>
    ///  Execute bulk update or delete against the database
    /// </summary>
    /// <remarks>
    ///  This function operates directly at the SQL level.
    /// </remarks>
    public static int BulkUpdate(string sql)
    {
      return BulkUpdate(sql, null);
    }

    /// <summary>
    ///  Execute bulk update or delete against the database
    /// </summary>
    /// <remarks>
    ///  This function operates directly at the SQL level.
    /// </remarks>
    public static int BulkUpdate(string sql, SqlParameter[] parameters)
    {
      return EntityContext.BulkUpdate(sql, parameters);
    }

    /// <summary>
    ///  Execute direct SQL
    /// </summary>
    /// <remarks>
    ///  This function operates directly at the SQL level.
    /// </remarks>
    /// <returns></returns>
    public static IDataReader ExecuteReader(string sql)
    {
      return ExecuteSqlCommand(CreateDbCommand(sql));
    }

    /// <summary>
    /// Execute direct SQL
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameterName"></param>
    /// <param name="parameterValue"></param>
    /// <returns></returns>
    public static IDataReader ExecuteReader(string sql, string parameterName, object parameterValue)
    {
      return ExecuteReader(sql, new[] {new DbDataParameter(parameterName, parameterValue)});
    }

    /// <summary>
    /// Execute direct SQL
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static IDataReader ExecuteReader(string sql, IList<DbDataParameter> parameters)
    {
      return ExecuteSqlCommand(CreateDbCommand(sql, parameters));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cmd"></param>
    /// <returns></returns>
    private static IDataReader ExecuteSqlCommand(IDbCommand cmd)
    {
      return EntityContext.ExecuteSqlCommand(cmd);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    /// <exclude />
    private static IDbCommand CreateDbCommand(string sql)
    {
      return EntityContext.CreateDbCommand(sql);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static IDbCommand CreateDbCommand(string sql, IList<DbDataParameter> parameters)
    {
      return EntityContext.CreateDbCommand(sql, parameters);
    }

    /// <summary>
    /// This is a direct ADO.NET query and is used in GUI applications where we need the latest
    /// data but cannot call Session.Clear. For example a dropdown list of available strategies.
    /// </summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    /// <exclude />
    public static DataTable DBQuery(string sql)
    {
      return EntityContext.DBQuery(sql);
    }

    /// <summary>
    ///
    /// </summary>
    /// <exclude />
    public static void Evict(object obj)
    {
      EntityContext.Evict(obj);
    }

    /// <summary>
    /// Adds the object to this session so that it will be saved to the database when CommitTransaction is called
    /// </summary>
    /// <param name="obj">object to add to the session</param>
    public static object Save(object obj)
    {
      return EntityContext.Save(obj);
    }

    /// <summary>
    /// This should not be used. Use the Save method to add a new object to the session. Existing database objects
    /// are implicitly saved on the next CommitTransaction. They do not need any explicit call to Save or SaveOrUpdate.
    /// </summary>
    [Obsolete("No longer supported. Use Save for new objects. No call is required to save existing objects")]
    public static void SaveOrUpdate(object obj)
    {
      EntityContext.SaveOrUpdate(obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public static object Merge(object obj)
    {
      return EntityContext.Merge(obj);
    }

    /// <summary>
    /// Commit current work and start a new transaction
    /// </summary>
    /// <param name="comment">Optional comment to write to CommitLog</param>
    public static void CommitTransaction(string comment = null)
    {
      EntityContext.CommitTransaction(comment);
    }

    /// <summary>
    ///  Rollback current work and start a new transaction
    /// </summary>
    public static void RollbackTransaction()
    {
      EntityContext.RollbackTransaction();
    }

    /// <summary>
    /// Create a criteria query for the specified class
    /// </summary>
    public static ICriteria CreateCriteria(Type persistentClass)
    {
      return EntityContext.CreateCriteria(persistentClass);
    }

    /// <summary>
    /// Create a criteria query for the specified class, using the specified alias
    /// </summary>
    public static ICriteria CreateCriteria(Type persistentClass, string alias)
    {
      return EntityContext.CreateCriteria(persistentClass, alias);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static IMultiCriteria CreateMultiCriteria()
    {
      return EntityContext.CreateMultiCriteria();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    /// <exclude />
    public static EntityLock FindLock(PersistentObject po)
    {
      return EntityContext.FindLock(po);
    }

    /// <summary>
    /// Check entity policy
    /// </summary>
    /// <remarks>
    /// Note that because there is no concept of workflow here we cannot check workflow policy
    /// </remarks>
    public static bool CheckPermission(EntityLock @lock, out string errorMsg)
    {
      return EntityContext.CheckPermission(@lock, out errorMsg);
    }

    /// <summary>
    /// Obtains a LINQ query provider for NHibernate
    /// </summary>
    /// <typeparam name="T">Entity type to query on</typeparam>
    /// <returns>An NHibernate-queryable provider</returns>
    /// <exclude />
    public static IQueryable<T> Linq<T>()
    {
      return EntityContext.Query<T>();
    }

    /// <summary>
    /// Upserts an item matching the specified criteria.
    /// </summary>
    /// <typeparam name="T">The type of the item</typeparam>
    /// <param name="criteria">The criteria.</param>
    /// <returns>An <see cref = "Upserter&lt;T&gt;" /> for handling the upsert.</returns>
    public static Upserter<T> Upsert<T>(Expression<Func<T, bool>> criteria) where T : PersistentObject
    {
      return new Upserter<T>(criteria, new DirectRepository<T>(EntityContext));
    }

    /// <summary>
    /// Gets <see cref="AuditLog">AuditLogs</see> for any Added/Changed/Removed entities
    /// </summary>
    /// <returns></returns>
    public static List<AuditLog> GetAuditLogs()
    {
      return EntityContext.GetAuditLogs();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public static bool IsSaved(PersistentObject po)
    {
      return po.ObjectId != 0;
    }

    #endregion
  }
}