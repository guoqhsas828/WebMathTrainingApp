// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// 
  /// </summary>
  public interface IPluginLoader
  {
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEnumerable<PluginItem> Load();
  }
}