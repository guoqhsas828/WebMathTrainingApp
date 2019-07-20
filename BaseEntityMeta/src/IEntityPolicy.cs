using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// interface for entity policy.
  /// </summary>
  public interface IEntityPolicy
  {
    /// <summary>
    /// boolean type. is applicable.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    bool IsApplicable(Type type);

    /// <summary>
    /// boolean type. check policy
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    bool CheckPolicy(ISnapshotDelta delta);

    /// <summary>
    /// boolean type. Check policy
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    bool CheckPolicy(PersistentObject entity, ItemAction action);
  }
}
