/*
 * CancellationEvent.cs
 *
*/

using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Represents the prepayment type for a Cancellation Event
  /// </summary>
  public enum PrepaymentType
  {
    /// <summary>
    /// 
    /// </summary>
    Refinancing,
    /// <summary>
    /// 
    /// </summary>
    Other
  }

  /// <summary>
  ///   
  /// </summary>
  [Serializable]
  [Entity(EntityId = 215, AuditPolicy = AuditPolicy.History, Key = new[] { "ReferenceEntity", "Seniority" }, Description = "Loan Cancellation Event")]
  public class CancellationEvent : AuditedObject
  {
    #region Constructors


    /// <summary>
    /// Initializes a new instance of the <see cref="CancellationEvent"/> class.
    /// </summary>
    public CancellationEvent()
    {
      Seniority = Seniority.Lien1;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Gets or sets the Reference Legal Entity
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity ReferenceEntity
    {
      get { return (LegalEntity)ObjectRef.Resolve(_referenceEntity); }
      set { _referenceEntity = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long ReferenceEntityId => _referenceEntity == null || _referenceEntity.IsNull ? 0 : _referenceEntity.Id;

    /// <summary>
    /// Gets or sets the seniority.
    /// </summary>
    /// <value>The seniority.</value>
    [EnumProperty(AllowNullValue = false)]
    public Seniority Seniority { get; set; }


    /// <summary>
    /// Gets or sets the cancellation date.
    /// </summary>
    /// <value>The cancellation date.</value>
    [DtProperty(AllowNullValue = false)]
    public Dt CancellationDate { get; set; }


    /// <summary>
    /// Gets or sets the type of the prepayment.
    /// </summary>
    /// <value>The type of the prepayment.</value>
    [EnumProperty(AllowNullValue = false)]
    public PrepaymentType PrepaymentType { get; set; }


    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    /// <value>The notes.</value>
    [StringProperty(MaxLength = 256)]
    public string Notes { get; set; }

    #endregion

    #region Methods

    /// <summary>
    ///   Whether or Event is effective on or before the AsOf date.
    /// </summary>
    /// <param name="asOf"></param>
    /// <returns></returns>
    public bool IsCancelledOn(Dt asOf)
    {
      return (asOf.IsValid() && CancellationDate <= asOf);
    }


    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      if(CancellationDate.IsEmpty())
      {
        InvalidValue.AddError(errors, this, "CancellationDate", "Value cannot be empty!");
      }
    }

    #endregion

    #region Data

    private ObjectRef _referenceEntity;

    #endregion
  }
}
