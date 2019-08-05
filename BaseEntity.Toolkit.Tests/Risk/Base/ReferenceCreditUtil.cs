using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Utility methods for classes that implement <see cref="IReferenceCredit"/>.
  /// </summary>
  /// <seealso cref="IReferenceCredit"/>
  public class ReferenceCreditUtil
  {
    /// <summary>
    ///   Either the <see cref="IReferenceCredit.OverriddenEventDeterminationDate"/> if set; 
    ///   otherwise the applied <see cref="IReferenceCredit.CreditEvent"/>
    ///   <see cref="CreditEvent.EventDeterminationDate"/>
    /// </summary>
    /// <param name="referenceCredit">Reference credit</param>
    /// <returns>Event determination date</returns>
    public static Dt GetEventDeterminationDate(IReferenceCredit referenceCredit)
    {
      if (referenceCredit.OverriddenEventDeterminationDate.IsValid())
        return referenceCredit.OverriddenEventDeterminationDate;

      if (referenceCredit.CreditEvent != null)
        return referenceCredit.CreditEvent.EventDeterminationDate;

      return (referenceCredit.CancellationEvent != null) ? referenceCredit.CancellationEvent.CancellationDate : Dt.Empty;
    }

    /// <summary>
    ///   Either the <see cref="IReferenceCredit.OverriddenRecoveryAnnounceDate"/> if set; 
    ///   otherwise the applied <see cref="IReferenceCredit.CreditEvent"/>
    ///   <see cref="CreditEvent.RecoveryAnnounceDate"/>
    /// </summary>
    /// <param name="referenceCredit">Reference credit</param>
    /// <returns>Recovery announcement date</returns>
    public static Dt GetRecoveryAnnounceDate(IReferenceCredit referenceCredit)
    {
      if (referenceCredit.OverriddenRecoveryAnnounceDate.IsValid())
        return referenceCredit.OverriddenRecoveryAnnounceDate;

      if (referenceCredit.CreditEvent != null)
        return referenceCredit.CreditEvent.RecoveryAnnounceDate;

      return (referenceCredit.CancellationEvent != null) ? referenceCredit.CancellationEvent.CancellationDate : Dt.Empty;
    }

    /// <summary>
    ///   Either the <see cref="IReferenceCredit.OverriddenRecoverySettlementDate"/> if set; 
    ///   otherwise the applied <see cref="IReferenceCredit.CreditEvent"/>
    ///   <see cref="CreditEvent.RecoverySettlementDate"/>
    /// </summary>
    /// <param name="referenceCredit">Reference credit</param>
    /// <returns>Recovery settlement date</returns>
    public static Dt GetRecoverySettlementDate(IReferenceCredit referenceCredit)
    {
      if (referenceCredit.OverriddenRecoverySettlementDate.IsValid())
        return referenceCredit.OverriddenRecoverySettlementDate;

      if (referenceCredit.CreditEvent != null)
        return referenceCredit.CreditEvent.RecoverySettlementDate;

      return (referenceCredit.CancellationEvent != null) ? referenceCredit.CancellationEvent.CancellationDate : Dt.Empty;
    }

    /// <summary>
    ///   Either the <see cref="IReferenceCredit.OverriddenRealizedRecoveryRate"/> if set; 
    ///   otherwise the applied <see cref="IReferenceCredit.CreditEvent"/>
    ///   <see cref="CreditEvent.RealizedRecoveryRate"/>
    /// </summary>
    /// <param name="referenceCredit">Reference credit</param>
    /// <returns>Realized recovery rate</returns>
    public static double GetRealizedRecoveryRate(IReferenceCredit referenceCredit)
    {
      if (referenceCredit.OverriddenRealizedRecoveryRate.HasValue)
        return referenceCredit.OverriddenRealizedRecoveryRate.Value;

      if (referenceCredit.CreditEvent != null)
        return referenceCredit.CreditEvent.RealizedRecoveryRate;

        // Cancellation Events have 100% recovery.
      return (referenceCredit.CancellationEvent != null) ? 1.0 : 0.0;
     }

    /// <summary>
    ///   Whether or not any CreditEvent details  
    ///   are overridden after a CreditEvent is applied
    /// </summary>
    /// <param name="referenceCredit">Reference credit</param>
    /// <returns>True if any CreditEvent features are overriden</returns>
    public static bool IsOverridden(IReferenceCredit referenceCredit)
    {
      return (referenceCredit.OverriddenEventDeterminationDate.IsValid() ||
              referenceCredit.OverriddenRecoveryAnnounceDate.IsValid() ||
              referenceCredit.OverriddenRecoverySettlementDate.IsValid() ||
              referenceCredit.OverriddenRealizedRecoveryRate.HasValue);
    }

    /// <summary>
    ///   Whether or not the ReferenceCredit is  
    ///   defaulted on or before the specified date.
    /// </summary>
    /// <param name="asOf">date to test</param>
    /// <param name="referenceCredit"></param>
    /// <returns>Returns true if ReferenceCredit defaulted on or before date specified</returns>
    public static bool IsDefaultedOn(Dt asOf, IReferenceCredit referenceCredit)
    {
      return (referenceCredit.EventDeterminationDate.IsValid() && referenceCredit.EventDeterminationDate <= asOf);
    }

    /// <summary>
    ///   Whether or not the Realized Recovery is 
    ///   announced on or before the specified date. 
    /// </summary>
    /// <param name="asOf">date to test</param>
    /// <param name="referenceCredit"></param>
    /// <returns>True if realized recovery has been announced by the specified date</returns>
    public static bool IsRecoveryAnnouncedOn(Dt asOf, IReferenceCredit referenceCredit)
    {
      return (referenceCredit.RecoveryAnnounceDate.IsValid() && referenceCredit.RecoveryAnnounceDate <= asOf);
    }

    /// <summary>
    ///   Whether or not the Recovery is 
    ///   settled on or before the specified date. 
    /// </summary>
    /// <param name="asOf">Date to test</param>
    /// <param name="referenceCredit"></param>
    /// <returns>True if recovery settled by specified date</returns>
    public static bool IsRecoverySettledOn(Dt asOf, IReferenceCredit referenceCredit)
    {
      return (referenceCredit.RecoverySettlementDate.IsValid() && referenceCredit.RecoverySettlementDate <= asOf);
    }

    /// <summary>
    ///   Validate the overridden Credit Event details 
    ///   with respect to the applied Credit Event details
    /// </summary>
    /// <param name="errors">List to add reported errors to</param>
    /// <param name="referenceCredit"></param>
    public static void ValidateCreditEvent(ArrayList errors, IReferenceCredit referenceCredit)
    {
      if (referenceCredit.CreditEvent != null && referenceCredit.CancellationEvent != null)
        InvalidValue.AddError(errors, referenceCredit, "CancellationEvent",
                              "Cannot have both Credit Event and Cancellation Event applied.");

      if(referenceCredit.IsOverridden)
      {
        if (referenceCredit.CreditEvent == null)
        {
          InvalidValue.AddError(errors, referenceCredit, "CreditEvent",
                                "Cannot override recovery details without applying a Credit Event");
        }

        if (referenceCredit.CancellationEvent != null)
        {
          InvalidValue.AddError(errors, referenceCredit, "CancellationEvent",
                                "Cannot override recovery details after applying a Cancellation Event");
        }

        if (referenceCredit.RecoveryAnnounceDate.IsValid() && referenceCredit.RecoveryAnnounceDate < referenceCredit.EventDeterminationDate)
        {
          InvalidValue.AddError(errors, referenceCredit, "CreditEvent",
                                String.Format(
                                  "Recovery Announce Date [{0}] must be on or after the Event Determination Date [{1}] for Credit [{2}.{3}]",
                                  referenceCredit.RecoveryAnnounceDate, referenceCredit.EventDeterminationDate, referenceCredit.ReferenceEntity.Ticker, referenceCredit.Seniority));
        }

        if (referenceCredit.RecoverySettlementDate.IsValid() && referenceCredit.RecoverySettlementDate < referenceCredit.EventDeterminationDate)
        {
          InvalidValue.AddError(errors, referenceCredit, "CreditEvent",
                                String.Format(
                                  "Recovery Settlement Date [{0}] must be on or after the Event Determination Date [{1}] for Credit [{2}.{3}]",
                                  referenceCredit.RecoverySettlementDate, referenceCredit.EventDeterminationDate, referenceCredit.ReferenceEntity.Ticker, referenceCredit.Seniority));
        }

        if (referenceCredit.RecoverySettlementDate.IsValid())
        {
          if (referenceCredit.RecoveryAnnounceDate.IsEmpty())
          {
            InvalidValue.AddError(errors, referenceCredit, "CreditEvent",
                                  "Recovery Announce Date cannot be empty with a non-empty Recovery Settlement Date.");
          }
          else
          {
            if (referenceCredit.RecoverySettlementDate < referenceCredit.RecoveryAnnounceDate)
            {
              InvalidValue.AddError(errors, referenceCredit, "CreditEvent",
                                    String.Format(
                                      "Recovery Settlement Date [{0}] must be on or after the Recovery Announce Date [{1}] for Credit [{2}.{3}]",
                                      referenceCredit.RecoverySettlementDate, referenceCredit.RecoveryAnnounceDate,
                                      referenceCredit.ReferenceEntity.Ticker, referenceCredit.Seniority));
            }
          }
        }
      }
    }
  }
}
