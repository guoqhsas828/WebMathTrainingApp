using System.Collections.Generic;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public interface IHasHierarchy
  {
    /// <summary>
    /// 
    /// </summary>
    IList<HierarchyElement> HierarchyElements { get; }
  }
}
