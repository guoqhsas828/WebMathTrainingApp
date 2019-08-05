/*
 * SectorItem.cs
 *
 *
 */

using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// An assigned Sector.
  /// </summary>
  ///
  [Serializable]
  [Component(ChildKey = new []{"Sector"})]
  public class SectorItem : BaseEntityObject
  {
    #region Constructors
    /// <summary>
    /// Default Constructor.
    /// </summary>
    ///
    public SectorItem()
    {
    }
    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    public SectorType SectorType
    {
      get { return Sector == null ? null : Sector.SectorType; }
    }


    /// <summary>
    /// The Sector
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public Sector Sector
    {
      get { return (Sector)ObjectRef.Resolve(_sector); }
      set { _sector = ObjectRef.Create(value); }
    }

    /// <summary>
    /// ObjectId of referenced <see cref="Sector"/>
    /// </summary>
    public long SectorId
    {
      get { return _sector == null || _sector.IsNull ? 0 : _sector.Id; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      if (Sector == null)
        InvalidValue.AddError(errors, this, "Sector", "Sector cannot be empty");
    }

    #endregion

    #region Data

    private ObjectRef _sector;

    #endregion
  }
}
