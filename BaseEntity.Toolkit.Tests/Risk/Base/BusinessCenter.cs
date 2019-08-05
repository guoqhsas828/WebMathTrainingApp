using System;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 180, AuditPolicy = AuditPolicy.History)]
  public class BusinessCenter : AuditedObject
  {
    #region Properties

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(IsKey = true, MaxLength = 4)]
    public string Name { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string Description { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 4)]
    public string FpmlCode { get; set; }

    #endregion
  }
}
