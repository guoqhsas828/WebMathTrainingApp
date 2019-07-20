using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  [Flags]
  public enum Cardinality
  {
    /// <summary>
    /// 
    /// </summary>
    None = 0x0,
    
    /// <summary>
    /// 
    /// </summary>
    OneToOne = 0x1,

    /// <summary>
    /// 
    /// </summary>
    ManyToOne = 0x2,

    /// <summary>
    /// 
    /// </summary>
    OneToMany = 0x4,

    /// <summary>
    /// 
    /// </summary>
    ManyToMany = 0x8
  }
}