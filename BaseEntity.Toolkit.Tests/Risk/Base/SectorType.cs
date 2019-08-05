/*
 * SectorType.cs
 *
 */
 using System;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// A group of Sectors that may be assigned to a LegalEntity.
  /// </summary>
  [Serializable]
	[Entity(EntityId = 137, DisplayName = "Sector Type", AuditPolicy = AuditPolicy.History, Description = "A group of Sectors that may be assigned to a LegalEntity")]
  public class SectorType : AuditedObject
  {
    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    public SectorType()
    {
      Name = "";
      Description = "";
    }
    #endregion

    #region Properties

    /// <summary>
    /// The name of the type.
    /// </summary>
    [StringProperty(MaxLength = 32, IsKey = true)]
    public string Name { get; set; }

    /// <summary>
    /// A description of the type.
    /// </summary>
    [StringProperty(MaxLength = 64)]
    public string Description { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Clone
    /// </summary>
    /// 
    /// <returns>SectorType</returns>
    /// 
    public override object Clone()
    {
      return new SectorType {Name = Name, Description = Description};
    }

    #endregion
  }
}
