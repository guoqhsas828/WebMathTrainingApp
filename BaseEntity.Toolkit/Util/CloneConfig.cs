//
// CloneConfig.cs
//   2012-2013. All rights reserved.
//
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///   Cloning method configuration.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class CloneConfig
  {
    /// <summary>
    /// Set the default method used to clone object grpahs.
    /// </summary>
    [ToolkitConfig("Set the default method used to clone object graphs (Serialization or FastClone).")]
    public readonly CloneMethod DefaultObjectGraphCloneMethod = CloneMethod.FastClone;

    // This method is called by configurator through reflection
    // every time when a new configuration is loaded.
    private void Validate()
    {
      switch (DefaultObjectGraphCloneMethod)
      {
      case CloneMethod.Serialization:
        CloneUtil.DefaultObjectGraphCloneMethod = CloneMethod.Serialization;
        return;
      case CloneMethod.FastClone:
        CloneUtil.DefaultObjectGraphCloneMethod = CloneMethod.FastClone;
        return;
      }
      // In case the user input a number.
      throw new ToolkitConfigException(String.Format("Unknown clone method number: {0}",
        (int)DefaultObjectGraphCloneMethod), null);
    }
  }
}
