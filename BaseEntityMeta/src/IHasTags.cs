using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// </summary>
  public interface IHasTags
  {
    /// <summary>
    /// </summary>
    IList<Tag> Tags { get; }
  }
}