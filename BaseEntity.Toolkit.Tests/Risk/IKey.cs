using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Defines interface to be implemented by keys in a dependency graph and key/value store
  /// </summary>
  public interface IKey
  {
    /// <summary>
    /// Type of Object key identifies
    /// </summary>
    Type ValueType
    {
      get;
    }

    /// <summary>
    /// Convert key into readable string
    /// </summary>
    string ToString();

    /// <summary>
    /// Unique hash code based on key
    /// </summary>
    int GetHashCode();
  }
}
