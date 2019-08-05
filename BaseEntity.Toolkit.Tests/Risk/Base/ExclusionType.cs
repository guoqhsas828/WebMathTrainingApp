using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Risk
{  
  /// <summary>
  ///  Exclusion Types
  /// </summary>
  [Flags]
	public enum ExclusionType
  {
    /// <summary>
    /// Exclude by Spread level, must provide an ExclusionTypePeriod to specify tenor of the spread to use.
    /// </summary>
    Spread = 0x0002,
    /// <summary>
    /// Exclude by DefaultProbability
    /// </summary>
    DefaultProbability = 0x0003,
    /// <summary>
    /// Exclude by SurvivalProbability
    /// </summary>
    SurvivalProbability = 0x0004
  }
}
