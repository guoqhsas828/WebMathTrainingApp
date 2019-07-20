using System.Collections;
using log4net.Core;
using BaseEntity.Metadata;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// </summary>
  public delegate void Reporter(Level logLevel, string format, params object[] args);

  /// <summary>
  ///  Public interface to custom data purge handler
  /// </summary>
  internal interface IPurgeHandler
  {
    /// <summary>
    ///
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="po"></param>
    /// <param name="reporter"></param>
    /// <returns>IList</returns>
    IList FindReferences(ClassMeta cm, PersistentObject po, Reporter reporter);
  }
}