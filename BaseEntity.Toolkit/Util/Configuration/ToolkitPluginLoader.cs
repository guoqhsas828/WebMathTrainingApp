// 
//  -2015. All rights reserved.
// 

using System.Collections.Generic;
using BaseEntity.Configuration;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Util.Configuration
{
  /// <summary>
  /// Initializes Toolkit internal data structures
  /// </summary>
  public class ToolkitPluginLoader : IPluginLoader
  {
    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<PluginItem> Load()
    {
      yield return new PluginItem("BaseEntity.Toolkit.dll", PluginType.EntityModel);
    }
  }
}