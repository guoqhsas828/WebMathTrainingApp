using System.Collections;
using System.Collections.Generic;

namespace BaseEntity.Risk
{
  ///<summary>
  /// Should be implemented by Trade class hierarchy to support access to risky counterparty or issuer
  ///</summary>
  public interface IRiskyCounterparty
  {
    ///<summary>
    /// The risky entity
    ///</summary>
    LegalEntity Counterparty { get; }

    /// <summary>
    /// 
    /// </summary>
    long? CounterpartyId { get; }

    ///<summary>
    /// The booking entity
    ///</summary>
    LegalEntity BookingEntity { get; }

    /// <summary>
    /// 
    /// </summary>
    long? BookingEntityId { get; }
  }
  /// <summary>
  /// 
  /// </summary>
  public interface IRiskyPartyResolver
  {
    /// <summary>
    /// Return the legal entity subject to counter-party risk for a supplied trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    LegalEntity GetRiskyCounterParty(Trade trade);

    /// <summary>
    /// Return the legal entity subject to booking-entity risk for a supplied trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    LegalEntity GetRiskyBookingEntity(Trade trade);

    /// <summary>
    /// Return a list of valid master agreements for a supplied trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    IList<MasterAgreement> GetMasterAgreements(Trade trade);

    /// <summary>
    /// Return a list of valid master agreements for a valid counterParty/bookingEntity pair.
    /// </summary>
    /// <param name="counterParty"></param>
    /// <param name="bookingEntity"></param>
    /// <returns></returns>
    IList<MasterAgreement> GetMasterAgreements(LegalEntity counterParty, LegalEntity bookingEntity);

    /// <summary>
    /// Return the risky party legal entity for a supplied legal entity
    /// </summary>
    /// <param name="le">Legal entity</param>
    /// <returns>The risky party</returns>
    LegalEntity GetRiskyParty(LegalEntity le);
  }

}