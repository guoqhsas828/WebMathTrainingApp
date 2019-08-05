/*
 * CreditEvent.cs
 *
*/

using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 214, AuditPolicy = AuditPolicy.History, Key = new[] { "ReferenceEntity", "Seniority", "Bucket", "EventDeterminationDate" }, Description = "Financial event related to a legal entity which triggers specific protection provided by a credit derivative")]
  public class CreditEvent : AuditedObject
  {
    #region Constructors


    /// <summary>
    /// Initializes a new instance of the <see cref="CreditEvent"/> class.
    /// </summary>
    public CreditEvent()
    {
      Bucket = "*";
      CreditEventType = CreditEventType.Unknown;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Reference Legal Entity
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity ReferenceEntity
    {
      get { return (LegalEntity)ObjectRef.Resolve(_referenceEntity); }
      set { _referenceEntity = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   ObjectId of the ReferenceEntity. 0 if not set.
    /// </summary>
    public long ReferenceEntityId => _referenceEntity == null || _referenceEntity.IsNull ? 0 : _referenceEntity.Id;

    /// <summary>
    /// Gets or sets the seniority.
    /// </summary>
    /// <value>The seniority.</value>
    [EnumProperty(AllowNullValue = false)]
    public Seniority Seniority { get; set; }

    /// <summary>
    /// Gets or sets the type of the credit event.
    /// </summary>
    /// <value>The type of the credit event.</value>
    [EnumProperty(AllowNullValue = false)]
    public CreditEventType CreditEventType { get; set; }

    /// <summary>
    /// Gets or sets the maturity bucket.
    /// </summary>
    /// <value>The bucket.</value>
    [StringProperty(MaxLength = 8)]
    public string Bucket { get; set; }

    /// <summary>
    /// Gets or sets the default date.
    /// </summary>
    /// <value>The default date.</value>
    [DtProperty]
    public Dt DefaultDate { get; set; }

    /// <summary>
    ///  Gets or sets the event determination date.
    /// </summary>
    /// <value>The event determination date.</value>
    [DtProperty(AllowNullValue = false)]
    public Dt EventDeterminationDate { get; set; }

    /// <summary>
    /// Gets or sets the recovery details are announced.
    /// </summary>
    /// <value>The recovery settlement announce date.</value>
    [DtProperty]
    public Dt RecoveryAnnounceDate { get; set; }

    /// <summary>
    /// Gets or sets the date when teh recovery is settled.
    /// </summary>
    /// <value>The recovery settlement date.</value>
    [DtProperty]
    public Dt RecoverySettlementDate { get; set; }

    /// <summary>
    /// Gets or sets the realized recovery rate.
    /// </summary>
    /// <value>The realized recovery rate.</value>
    [NumericProperty(Format = NumberFormat.Percentage)]
    public double RealizedRecoveryRate { get; set; }

    /// <summary>
    /// Gets or sets the ISDA_Auction Curreny.
    /// </summary>
    /// <value>The ISDA_Auction Curreny.</value>
    [EnumProperty(AllowNullValue = false)]
    public Currency ISDA_AuctionCcy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether [ISD a_ rebate].
    /// </summary>
    /// <value><c>true</c> if accruals are calulated using ISDA full coupon and rebate methodology; otherwise, <c>false</c>.</value>
    [BooleanProperty]
    public bool ISDA_Rebate { get; set; }

    ///// <summary>
    ///// Gets or sets the ISDA_ConfirmedData
    ///// </summary>
    ///// <value>The ISDA_ConfirmedData.</value>
    //[EnumProperty(AllowNullValue = false)]
    //public ISDAConfirmed ISDA_ConfirmedData { get; set; }

    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    /// <value>The notes.</value>
    [StringProperty(AllowNullValue = true, MaxLength = 256)]
    public string Notes { get; set; }

    #endregion

    #region Methods

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

      #region VALIDATE EventDeterminationDate

      // MUST HAVE Event Determination Date
      if (EventDeterminationDate.IsEmpty())
      {
        InvalidValue.AddError(errors, this, "EventDeterminationDate", "Value cannot be empty!");
      }
      else if (!EventDeterminationDate.IsValid())
      { // MUST HAVE Valid Event Determination Date
        InvalidValue.AddError(errors, this, "EventDeterminationDate", String.Format("Event Determination Date must be valid [{0}] ", EventDeterminationDate));
      } 

      #endregion

      #region VALIDATE RecoveryAnnounceDate

      // If RecoveryAnnounceDate is not Empty, Validate it.
      if (!RecoveryAnnounceDate.IsEmpty() &&
            RecoveryAnnounceDate.IsValid() &&
            EventDeterminationDate.IsValid() &&
            RecoveryAnnounceDate < EventDeterminationDate)
      {
        InvalidValue.AddError(errors, this, "RecoveryAnnounceDate",
                              String.Format(
                                "RecoveryAnnounceDate [{0}] must be on or after EventDeterminationDate [{1}]",
                                RecoveryAnnounceDate.ToDateTime().ToShortDateString(),
                                EventDeterminationDate.ToDateTime().ToShortDateString()));
      } 
      #endregion

      #region VALIDATE RecoverySettlementDate

      if (!RecoverySettlementDate.IsEmpty())
      {
        // If RecoveryAnnounceDate is not Empty, validate off it.
        if (!RecoveryAnnounceDate.IsEmpty() &&
          RecoveryAnnounceDate.IsValid() &&
                  RecoverySettlementDate < RecoveryAnnounceDate
          )
        {
          InvalidValue.AddError(errors, this, "RecoverySettlementDate",
                                String.Format(
                                  "RecoverySettlementDate [{0}] must be on or after RecoveryAnnounceDate [{1}]",
                                  RecoverySettlementDate.ToDateTime().ToShortDateString(),
                                  RecoveryAnnounceDate.ToDateTime().ToShortDateString()));
        }
        else if (RecoverySettlementDate.IsValid() &&
                  EventDeterminationDate.IsValid() &&
                  RecoverySettlementDate < EventDeterminationDate)
        {
          InvalidValue.AddError(errors, this, "RecoverySettlementDate",
                                String.Format(
                                  "RecoverySettlementDate [{0}] must be on or after EventDeterminationDate [{1}]",
                                  RecoverySettlementDate.ToDateTime().ToShortDateString(),
                                  EventDeterminationDate.ToDateTime().ToShortDateString()));
        }
      } 
      #endregion

      //VALIDATE RealizedRecoveryRate
      if (RealizedRecoveryRate < 0 || RealizedRecoveryRate > 1)
        InvalidValue.AddError(errors, this, "RealizedRecoveryRate", "RealizedRecoveryRate must be between 0% and 100%");
    
    }


    /// <summary>
    ///  Determines whether the Event Determination Date is on or before the specified asOf date
    /// </summary>
    /// <param name="asOf">Asof.</param>
    /// <returns>
    /// 	<c>true</c> if Event Determination Date is on or before the specified asOf date; otherwise, <c>false</c>.
    /// </returns>
    public bool IsDefaultedOn(Dt asOf)
    {
      return (EventDeterminationDate.IsValid() && asOf.IsValid() && EventDeterminationDate <= asOf);
    }


    /// <summary>
    ///  Determines whether the RecoverySettlementDate is on or before the specified asOf date
    /// </summary>
    /// <param name="asOf">Asof.</param>
    /// <returns>
    /// 	<c>true</c> if Event RecoverySettlementDate is on or before the specified asOf date; otherwise, <c>false</c>.
    /// </returns>
    public bool IsRecoverySettledOn(Dt asOf)
    {
      return (RecoverySettlementDate.IsValid() && asOf.IsValid() && RecoverySettlementDate <= asOf);
    }

    /// <summary>
    ///  Determines whether the RecoveryAnnounceDate is on or before the specified asOf date
    /// </summary>
    /// <param name="asOf">Asof.</param>
    /// <returns>
    /// 	<c>true</c> if Event RecoveryAnnounceDate is on or before the specified asOf date; otherwise, <c>false</c>.
    /// </returns>
    public bool IsRecoveryAnnouncedOn(Dt asOf)
    {
      return (RecoveryAnnounceDate.IsValid() && asOf.IsValid() && RecoveryAnnounceDate <= asOf);
    }


    /// <summary>
    ///   Determines whether the specified other is same.
    /// </summary>
    /// <param name="other">The other Credit Event.</param>
    /// <returns>
    /// 	<c>true</c> if the specified other is same; otherwise, <c>false</c>.
    /// </returns>
    public bool IsSame(CreditEvent other)
    {
      if (ReferenceEntity != other.ReferenceEntity) return false;
      if (Seniority != other.Seniority) return false;
      if (EventDeterminationDate != other.EventDeterminationDate) return false;
      if (!String.Equals(Bucket, other.Bucket, StringComparison.OrdinalIgnoreCase)) return false;
      if (CreditEventType != other.CreditEventType) return false;
      if (DefaultDate != other.DefaultDate) return false;      
      if (RecoveryAnnounceDate != other.RecoveryAnnounceDate) return false;
      if (RecoverySettlementDate != other.RecoverySettlementDate) return false;
      if (!RealizedRecoveryRate.ApproximatelyEqualsTo(other.RealizedRecoveryRate)) return false;
      if (ISDA_AuctionCcy != other.ISDA_AuctionCcy) return false;
      //if (ISDA_ConfirmedData != other.ISDA_ConfirmedData) return false;
      if (ISDA_Rebate != other.ISDA_Rebate) return false;
      return true;
    }

    #endregion

    #region Data

    private ObjectRef _referenceEntity;

    #endregion
  }
}
