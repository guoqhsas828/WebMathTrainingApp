// 
// IReferenceCredit.cs
// 

using System.Collections;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Interface to be implemented by any object that is a Reference Credit
  ///   and will be impacted by a Credit/Cancellation/CorporateAction Events.
  /// </summary>
  public interface IReferenceCredit
  {
    #region Properties

    /// <summary>
    ///   Reference LegalEntity
    /// </summary>
    LegalEntity ReferenceEntity { get; }

    /// <summary>
    /// ObjectId of ReferenceEntity if set, otherwise 0
    /// </summary>
    long ReferenceEntityId { get; }

    /// <summary>
    /// 
    /// </summary>
    ReferenceObligation ReferenceObligation { get; }

    /// <summary>
    /// 
    /// </summary>
    long ReferenceObligationId { get; }

    /// <summary>
    ///   Seniority
    /// </summary>
    Seniority Seniority { get; }

    /// <summary>
    ///   Currency
    /// </summary>
    Currency Currency { get; }

    /// <summary>
    ///   RestructuringType
    /// </summary>
    RestructuringType RestructuringType { get; }

    /// <summary>
    ///   Cancellability
    /// </summary>
    Cancellability Cancellability { get; }

    /// <summary>
    ///   Applied Credit Event
    /// </summary>
    CreditEvent CreditEvent { get; set; }

    /// <summary>
    /// 
    /// </summary>
    long CreditEventId { get; }

    /// <summary>
    ///   Applied Cancellation Event
    /// </summary>
    CancellationEvent CancellationEvent { get; set; }

    /// <summary>
    /// 
    /// </summary>
    long CancellationEventId { get; }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    /// </summary>
    Dt OverriddenEventDeterminationDate { get; set; }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    /// </summary>
    Dt OverriddenRecoveryAnnounceDate { get; set; }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    /// </summary>
    Dt OverriddenRecoverySettlementDate { get; set; }

    /// <summary>
    ///   Override rate for the applied CreditEvent 
    /// </summary>
    double? OverriddenRealizedRecoveryRate { get; set; }

    /// <summary>
    /// The event determination date
    /// </summary>
    /// <remarks>
    ///   Either the <see cref="OverriddenEventDeterminationDate"/> if set; 
    ///   otherwise the applied CreditEvent
    /// </remarks>
    Dt EventDeterminationDate { get; }

    /// <summary>
    /// The recovery announcement date
    /// </summary>
    /// <remarks>
    ///   Either the <see cref="OverriddenRecoveryAnnounceDate"/> if set; 
    ///   otherwise the applied CreditEvent
    /// </remarks>
    Dt RecoveryAnnounceDate { get; }

    /// <summary>
    /// The recovery settlement date
    /// </summary>
    /// <remarks>
    ///   Either the <see cref="OverriddenRecoverySettlementDate"/> if set; 
    ///   otherwise the applied CreditEvent
    /// </remarks>
    Dt RecoverySettlementDate { get; }

    /// <summary>
    ///   The realized recovery rate
    /// </summary>
    /// <remarks>
    ///   Either the <see cref="OverriddenRealizedRecoveryRate"/> if set; 
    ///   otherwise the applied CreditEvent
    /// </remarks>
    double RealizedRecoveryRate { get; }

    /// <summary>
    ///   Whether or not any CreditEvent details  
    ///   are overridden after a CreditEvent is applied
    /// </summary>
    bool IsOverridden { get; }

    #endregion

    #region Methods

    /// <summary>
    ///   Whether or not the ReferenceCredit is  
    ///   defaulted on or before the specified date.
    /// </summary>
    /// <param name="asOf">date to test</param>
    /// <returns>Returns true if ReferenceCredit defaulted on or before date specified</returns>
    bool IsDefaultedOn(Dt asOf);

    /// <summary>
    ///   Whether or not the Realized Recovery is 
    ///   announced on or before the specified date. 
    /// </summary>
    /// <param name="asOf">date to test</param>
    /// <returns>True if realized recovery has been announced by the specified date</returns>
    bool IsRecoveryAnnouncedOn(Dt asOf);

    /// <summary>
    ///   Whether or not the Recovery is 
    ///   settled on or before the specified date. 
    /// </summary>
    /// <param name="asOf">Date to test</param>
    /// <returns>True if recovery settled by specified date</returns>
    bool IsRecoverySettledOn(Dt asOf);

    /// <summary>
    ///   Validate the overridden Credit Event details 
    ///   with respect to the applied Credit Event details
    /// </summary>
    /// <param name="errors">List to add reported errors to</param>
    void ValidateCreditEvent(ArrayList errors);
    
    #endregion
  }
}
