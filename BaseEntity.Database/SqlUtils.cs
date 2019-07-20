// 
// Copyright (c) WebMathTraining Inc 2002-2012. All rights reserved.
// 

using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class SqlUtils
  {
    /// <summary>
    /// Utility method to help when creating a sql statement
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="tableName"></param>
    /// <param name="colName"></param>
    /// <param name="values"></param>
    /// <param name="addedWhere"></param>
    public static void AddWhereClause(StringBuilder sb, string tableName, string colName, IList values, ref bool addedWhere)
    {
      if (values != null && values.Count > 0)
      {
        if (!addedWhere)
        {
          sb.Append(" where ");
          addedWhere = true;
        }
        else
        {
          sb.Append(" and ");
        }

        sb.Append(string.Format("[{0}].[{1}] in (", tableName, colName));

        var delim = "";
        foreach (var s in values)
        {
          sb.Append(delim);

          if (s is string)
            sb.Append(string.Format("'{0}'", s));
          else
            sb.Append(s);

          delim = ",";
        }

        sb.Append(")");
      }
    }

    /// <summary>
    /// Strip out any characters that would not be friendly for a sql column or table name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string CleanName(string name)
    {
      var n = name.Replace(' ', '_')
        .Replace("%", "")
        .Replace("*", "")
        .Replace("&", "")
        .Replace("'", "")
        .Replace("\"", "")
        .Replace(",", "")
        .Replace(".", "")
        .Replace(">", "GT")
        .Replace("<", "LT");

      return n;
    }

    /// <summary>
    /// Utility method to produce a sql clause and parameters given an enumerable of longs
    /// Foreach item in items this will add a parameter named paramPrefix[#] and add the item as a comma delimited item to queryString
    /// </summary>
    /// <param name="items"></param>
    /// <param name="paramPrefix"></param>
    /// <param name="parameters"></param>
    /// <param name="queryString"></param>
    public static void BuildParameterizedQueryString(IEnumerable<long> items, string paramPrefix, List<DbDataParameter> parameters, StringBuilder queryString)
    {
      var i = 0;
      foreach (var id in items)
      {
        string paramName = string.Format("@{0}{1}", paramPrefix, i);

        parameters.Add(new DbDataParameter(paramName, id));

        if (i > 0)
          queryString.Append(",");

        queryString.Append(paramName);

        i++;
      }
    }

    /// <summary>
    /// Utility method to produce a sql clause and parameters given an enumerable of longs
    /// Foreach item in items this will add a parameter named paramPrefix[#] and add the item as a comma delimited item to queryString
    /// </summary>
    /// <param name="items"></param>
    /// <param name="paramPrefix"></param>
    /// <param name="parameters"></param>
    /// <param name="queryString"></param>
    public static void BuildParameterizedQueryString(IEnumerable<string> items, string paramPrefix, List<DbDataParameter> parameters, StringBuilder queryString)
    {
      var i = 0;
      foreach (var id in items)
      {
        string paramName = string.Format("@{0}{1}", paramPrefix, i);

        parameters.Add(new DbDataParameter(paramName, id));

        if (i > 0)
          queryString.Append(",");

        queryString.Append(paramName);

        i++;
      }
    }
  }
}