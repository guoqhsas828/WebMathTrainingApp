using System;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Util
{
  /// <exclude />
  [Serializable]
  public class CpuExtensionsConfig
  {
    /// <exclude />
    [ToolkitConfig("Fused-multiply-add SIMD extensions. Disable FMA3 extensions for better numerical stability against older CPUs")]
    public readonly bool Fma3 = false;

  }
}
