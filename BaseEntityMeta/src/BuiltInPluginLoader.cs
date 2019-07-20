// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Configuration;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class BuiltInPluginLoader : IPluginLoader
  {
    private readonly Lazy<IList<PluginItem>> _lazyPluginItems;

    /// <summary>
    /// 
    /// </summary>
    public BuiltInPluginLoader(string assemblyNames)
    {
      _lazyPluginItems = new Lazy<IList<PluginItem>>(
        () => InitPluginItems(assemblyNames));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PluginItem> Load()
    {
      return _lazyPluginItems.Value;
    }

    /// <summary>
    /// Load cache with all enabled plugins
    /// </summary>
    /// <returns></returns>
    private static IList<PluginItem> InitPluginItems(string assemblyNames)
    {
      var results = string.IsNullOrEmpty(assemblyNames) ? new List<PluginItem>() : assemblyNames.Split(',').Select(assemblyName => new PluginItem(assemblyName, PluginType.EntityModel)).ToList();

      results.AddRange(
        new[]
        {
          //new PluginItem("WebMathTraining.Toolkit.dll", PluginType.EntityModel),
          new PluginItem("BaseEntity.Metadata.dll", PluginType.EntityModel),
          new PluginItem("BaseEntity.Database.dll", PluginType.EntityModel)
        });

      return results;
    }
  }
}