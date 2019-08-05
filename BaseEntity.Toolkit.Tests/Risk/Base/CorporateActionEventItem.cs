using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Component of the Corporate Action Business Event describing whole or part of a Corporate Action
  /// </summary>
  [Component(ChildKey = new[] { "OldReferenceEntity", "NewReferenceEntity" })]
  [Serializable]
  public class CorporateActionEventItem : BaseEntityObject
  {
    #region Properties

    /// <summary>
    ///   Old Refreence Entity for a Corporate Action Event
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity OldReferenceEntity
    {
      get { return (LegalEntity)ObjectRef.Resolve(_oldReferenceEntity); }
      set { _oldReferenceEntity = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long OldReferenceEntityId => _oldReferenceEntity == null || _oldReferenceEntity.IsNull ? 0 : _oldReferenceEntity.Id;

    /// <summary>
    ///   New Reference Entity for a Corporate Action Event
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity NewReferenceEntity
    {
      get { return (LegalEntity)ObjectRef.Resolve(_newReferenceEntity); }
      set { _newReferenceEntity = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long NewReferenceEntityId => _newReferenceEntity == null || _newReferenceEntity.IsNull ? 0 : _newReferenceEntity.Id;

    /// <summary>
    ///   Percent Debt Transferred from old Reference Entity to New Reference Entity
    /// </summary>
    [NumericProperty(AllowNullValue = false, Format = NumberFormat.Percentage)]
    public double PercentDebtTransferred { get; set; }

    #endregion

    #region Data

    private ObjectRef _oldReferenceEntity;
    private ObjectRef _newReferenceEntity;

    #endregion
  }
}