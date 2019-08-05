/*
 * Sector.cs
 *
 */

using System;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// A sector (or industry) that categorizes a group of similiar LegalEntities.
  /// </summary>
  [Serializable]
  [Entity(EntityId = 138, Key = new[]{"SectorType", "Name", "Value"}, AuditPolicy = AuditPolicy.History)]
  public class Sector : AuditedObject, IComparable<Sector>
  {
    #region Constructors

    /// <summary>
    /// Construct default instance
    /// </summary>
    public Sector()
    {
      Name = "";
      Description = "";
    }

    #endregion

    #region Properties

    /// <summary>
    /// The name of the Sector
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string Name { get; set; }

    /// <summary>
    /// The SectorType that the Sector belongs to.
    /// </summary>
    [ManyToOneProperty]
    public SectorType SectorType
    {
      get { return (SectorType)ObjectRef.Resolve(sectorType_); }
      set { sectorType_ = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long SectorTypeId
    {
      get { return sectorType_ == null || sectorType_.IsNull ? 0 : SectorType.ObjectId; }
    }

    /// <summary>
    /// The description of the Sector
    /// </summary>
    [StringProperty(MaxLength = 256)]
    public string Description { get; set; }

    /// <summary>
    /// A code or value assigned to the Sector. 
    /// </summary>
    [NumericProperty]
    public double Value { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Clone
    /// </summary>
    /// 
    /// <returns>Sector</returns>
    public override object Clone()
    {
      Sector sector = new Sector();

      // Copy
      sector.Name = Name;
      sector.Description = Description;
      sector.Value = Value;

      // Done
      return sector;
    }

    //#region IComparer<Sector> Members

    ///// <exclude />
    //int IComparer<Sector>.Compare(Sector x, Sector y)
    //{
    //  return String.Compare(x.Name, y.Name);  
    //}

    //#endregion

    #region IComparable<Sector> Members

    /// <exclude />
    public int CompareTo(Sector other)
    {
      return String.Compare(Name, other.Name);
    }

    #endregion

    #endregion

    #region Data

    private ObjectRef sectorType_;

    #endregion
  }
}
