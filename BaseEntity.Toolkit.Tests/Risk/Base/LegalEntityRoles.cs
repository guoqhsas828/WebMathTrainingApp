using System;

namespace BaseEntity.Risk
{
  ///
  /// <summary>
  ///   Individual roles that an LegalEntity may take on.
  /// </summary>
  ///
  [Flags]
  public enum LegalEntityRoles
  {
    /// <summary>Party in a trade</summary>
    Party = 0x0001,

    /// <summary>Obligor</summary>
    Obligor = 0x0002,

    /// <summary>Guarantor</summary>
    Guarantor = 0x0004,

    /// <summary>Reference LegalEntity</summary>
    Reference = 0x0008,

    /// <summary>Calculation Agent</summary>
    CalculationAgent = 0x0010,

    /// <summary>Broker</summary>
    Broker = 0x0020,

    /// <summary>Issuer</summary>
    Issuer = 0x0040,

    /// <summary>Trustee</summary>
    Trustee = 0x0080,

    /// <summary>Booking Entity</summary>
    BookingEntity = 0x0100,

    /// <summary>Administrative Agent</summary>
    AdministrativeAgent = 0x0200,

    /// <summary>Syndication Agent</summary>
    SyndicationAgent = 0x0400,

    /// <summary>Bookrunner</summary>
    Bookrunner = 0x0800,

    /// <summary>Collateral Agent</summary>
    CollateralAgent = 0x1000,

    /// <summary>Arranger</summary>
    Arranger = 0x2000,

    /// <summary>Manager</summary>
    Manager = 0x4000,

    /// <summary>Clearing House</summary>
    ClearingHouse = 0x8000,

    ///<summary> Trade Exchange</summary>
    TradeExchange = 0x10000,

    ///<summary>  Counterparty Group </summary>
    CounterpartyGroup = 0x20000,

    ///<summary>Non-contractual Counterparty</summary>
    PartyNonContractual = 0x40000,

    ///<summary>Custodian</summary>
    Custodian = 0x80000
  } // enum EntityRoles
}  