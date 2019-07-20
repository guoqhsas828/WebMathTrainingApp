// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <remarks></remarks>
  public interface ISnapshotDelta
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    void Serialize(IEntityDeltaWriter writer);

    /// <summary>
    /// 
    /// </summary>
    bool IsScalar { get; }
  }
}