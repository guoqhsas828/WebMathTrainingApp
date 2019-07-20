// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// 
  /// </summary>
  public class NullPluginLoader : IPluginLoader
  {
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PluginItem> Load()
    {
      return new PluginItem[0];
    }
  }
}