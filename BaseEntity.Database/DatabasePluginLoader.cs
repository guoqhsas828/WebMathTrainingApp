// 
// Copyright (c) WebMathTraining Inc 2002-2017. All rights reserved.
// 

using System.Collections.Generic;
using System.Data;
using BaseEntity.Configuration;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class DatabasePluginLoader : IPluginLoader
  {
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PluginItem> Load()
    {
      var pluginItems = new List<PluginItem>
      {
        new PluginItem(typeof(User).Assembly.Location, PluginType.EntityModel),
        new PluginItem(typeof(SessionFactory).Assembly.Location, PluginType.EntityModel)
      };

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT Name,FileName,PluginType from PluginAssembly where Enabled=1";

        using (IDataReader reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            pluginItems.Add(
              new PluginItem((string)reader[1],
                (PluginType)reader[2]));
          }
        }
      }

      return pluginItems;
    }
  }
}