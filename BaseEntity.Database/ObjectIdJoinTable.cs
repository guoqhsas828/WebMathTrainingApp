// 
// Copyright (c) WebMathTraining Inc 2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class ObjectIdJoinTable : IDisposable
  {
    private readonly string _dropTableSql;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="objectIds"></param>
    public ObjectIdJoinTable(string tableName, IEnumerable<long> objectIds)
      : this(CreateDataTable(tableName, "ObjectId", objectIds))
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dt"></param>
    public ObjectIdJoinTable(DataTable dt)
    {
      _dropTableSql = "DROP TABLE " + dt.TableName;
      Session.BulkUpdate("CREATE TABLE " + dt.TableName + " (ObjectId bigint NOT NULL PRIMARY KEY (ObjectId))");
      Session.BulkInsert(dt);
    }

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
      Session.BulkUpdate(_dropTableSql);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="columnName"></param>
    /// <param name="objectIds"></param>
    /// <returns></returns>
    public static DataTable CreateDataTable(string tableName, string columnName, IEnumerable<long> objectIds)
    {
      var dt = new DataTable(tableName);
      dt.Columns.Add(columnName, typeof(long));
      foreach (var objectId in objectIds)
      {
        var row = dt.NewRow();
        row[0] = objectId;
        dt.Rows.Add(row);
      }
      return dt;
    }
  }
}