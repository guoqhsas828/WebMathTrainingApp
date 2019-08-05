//
// Trade.cs
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Risk
{
  ///
  /// <summary>
  ///   A trade consists of the purchase or sale of an individual product.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>Each trade is associated with a single strategy.</para>
  ///   <para>Each trade is associated with a single counterparty.</para>
  ///   <para>Related trades may be grouped by using trade ids.</para>
  /// </remarks>
  ///
  [DataContract]
  [Serializable]
  [Entity(SubclassMapping = SubclassMappingStrategy.TablePerClassHierarchy,
    AuditPolicy = AuditPolicy.History)]
  public abstract class Trade : AuditedObject, IHasTags
  {
    #region Constructor

    /// <summary>
    ///   Clone Trade
    /// </summary>
    /// <returns>Cloned trade</returns>
    public override object Clone()
    {
    	throw new NotImplementedException("Use Trade.Copy method instead of Clone");
    }

    #endregion

    #region Methods

    

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string GetClearingBrokerName()
    {
      // Note: do not confuse this with the ClearingBroker property.
      // The ClearingBroker property is added as per FB case 44405 and currently applies just to the 4 futures trade types:
      // BondFuture, CommodityFuture, EquityIndexFuture, RateFuture.
      // While this was added earlier for "Cleared" trades (as per FB case 42367) (meaning those trade types for which clearing is applicable,
      // AND IsCleared flag is true)
      if (IsCleared && Counterparty != null)
      {
        if (Counterparty.HasRole(LegalEntityRoles.ClearingHouse) && ClearingHouse != null && Counterparty.ObjectId == ClearingHouse.ObjectId)
          // we are therefore a clearing member ourselves and not using a clearing broker
          return string.Empty;

        if (Counterparty.HasRole(LegalEntityRoles.Broker))
          return Counterparty.Name;
      }
      return string.Empty;
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <remarks>
    ///   <para>Validates the properties of a trade. This includes
    ///   things like having a valid trade date, valid settlement date,
    ///   traded date is on or before settlement date, etc.</para>
    ///   <para>Any errors found are returned in <paramref name="errors"/>.</para>
    /// </remarks>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Must have valid Traded and Settle dates
      if (Traded.IsEmpty())
      {
        InvalidValue.AddError(errors, this, "Traded", "Traded date is required");
      }

      if (Settle.IsEmpty())
      {
        InvalidValue.AddError(errors, this, "Settle", "Settle date is required");
      }

      // Traded date must be on or before settle date
      if (!Traded.IsEmpty() && !Settle.IsEmpty() && Dt.Cmp(Traded, Settle) > 0)
      {
        InvalidValue.AddError(errors, this, "Traded", "Traded date must be on or before Settle date");
      }

      // Maturity date must be on or after Traded date
      if (!Traded.IsEmpty() && !Termination.IsEmpty() && Dt.Cmp(Traded, Termination) > 0)
      {
        InvalidValue.AddError(errors, this, "Termination", "Termination date must be on or after Traded date");
      }

      // If have payment, must have a PaymentSettle date
      if (Payment != 0.0 && PaymentSettle.IsEmpty())
      {
        InvalidValue.AddError(errors, this, "PaymentSettle", "Must specify PaymentSettle date for a payment");
      }

      // Payment Settle date must be on or after Traded date
      if (!Traded.IsEmpty() && !PaymentSettle.IsEmpty() && Dt.Cmp(Traded, PaymentSettle) > 0)
      {
        InvalidValue.AddError(errors, this, "PaymentSettle", "PaymentSettle date must be on or after Traded date");
      }

      if (Strategy == null)
      {
        InvalidValue.AddError(errors, this, "Strategy", "Trade must have a strategy defined.");
      }

      // Validate relationship wtih Product (product is validated by base.Validate)
      if (Product != null)
      {
        // Product Maturity must be on or after Trade settle date
        if (!Settle.IsEmpty() && !Product.IsValidSettle(Settle))
        {
          InvalidValue.AddError(errors, this, "Settle", "Settle date must be on or before Maturity date");
        }
      }

      if (Product == null || !StringUtil.HasValue(Product.Name))
      {
        InvalidValue.AddError(errors, this, "Product", "Product is required");
      }

      //if (MasterAgreement != null)
      //{
      //  var iRiskyCpty = this as IRiskyCounterparty;
      //  var iRiskyPty = RiskConfigurator.RiskyPartyResolver;
      //  var riskyCounterparty = iRiskyPty.GetRiskyCounterParty(this);
      //  var riskyBookingEntity = iRiskyPty.GetRiskyBookingEntity(this);

      //  if (iRiskyCpty == null)
      //  {
      //    InvalidValue.AddError(errors, this, "MasterAgreement",
      //                          String.Format(
      //                            "Trade type {0} is not subject to Counterparty Risk, so should not fall under any Master Agreement.",
      //                            GetType()));
      //  }

      //  if (iRiskyCpty != null && riskyCounterparty == null)
      //  {
      //    InvalidValue.AddError(errors, this, "MasterAgreement", "Risky Counterparty must not be null if MasterAgreement is set");
      //  }

      //  //if (iRiskyCpty != null && riskyCounterparty != null &&
      //  //    MasterAgreement.Counterparty.Name != riskyCounterparty.Name)
      //  //{
      //  //  InvalidValue.AddError(errors, this, "MasterAgreement",
      //  //                        "Counterparty for MasterAgreement does not match Risky Counterparty of Trade");
      //  //}

      //  if (riskyBookingEntity == null)
      //  {
      //    InvalidValue.AddError(errors, this, "BookingEntity",
      //                          "BookingEntity must not be null if MasterAgreement is set");
      //  }

      //  //if (riskyBookingEntity != null && MasterAgreement.BookingEntity.Name != riskyBookingEntity.Name)
      //  //{
      //  //  InvalidValue.AddError(errors, this, "MasterAgreement",
      //  //                        "BookingEntity for MasterAgreement does not match trade BookingEntity");
      //  //}

      //  //if (Product != null && MasterAgreement.ProductTypes.Any() &&
      //  //    !MasterAgreement.ProductTypes.Contains(Product.GetType().Name))
      //  //{
      //  //  InvalidValue.AddError(errors, this, "MasterAgreement",
      //  //                        "MasterAgreement does cover product type " + Product.GetType().Name);
      //  //}

      //  if (IsCleared)
      //  {
      //    if (!TradeUtil.IsClearingApplicable(this.GetType()))
      //      InvalidValue.AddError(errors, this, "IsCleared",
      //                          "Clearing is not applicable to trade type " + this.GetType().Name);
      //    else
      //    {
      //      if (ClearingHouse != null && ((ClearingHouse.EntityRoles & LegalEntityRoles.ClearingHouse) == 0))
      //        InvalidValue.AddError(errors, this, "ClearingHouse",
      //                          "The legal entity selected as Clearing House must have ClearingHouse role specified.");
      //      if (OriginalCounterparty != null && ((OriginalCounterparty.EntityRoles & LegalEntityRoles.Party) == 0))
      //        InvalidValue.AddError(errors, this, "OriginalCounterparty",
      //                          "The legal entity selected as Original Counterparty must have Party role specified.");
      //    }
      //  }
      //}

      if (!CancellationDate.IsEmpty() && TradeStatus != TradeStatus.Canceled)
      {
        InvalidValue.AddError(errors, this, "CancellationDate", "Cancellation date can only be set on a canceled trade");
      }

    }

    /// <summary>
    ///   Returns true if either the Position or Payment are active on the specified date.
    /// </summary>
    /// <param name="date">Date to test</param>
    /// <returns>True if Position or Payment active on the specified date</returns>
    public bool IsActive(Dt date)
    {
      return PaymentIsActive(date) || PositionIsActive(date);
    }

    /// <summary>
    ///   Boolean method returning true if the Positions is active on
    ///   the specified date. This does not include an Active Payment check.
    /// </summary>
    /// <param name="date">Date to test</param>
    /// <returns>true if Position is active on the specified date</returns>
    public bool PositionIsActive(Dt date)
    {
      if (Traded > date)
        return false;
      if (IsTerminated(date))
        return false;
      return Product.IsActive(date);
    }

    /// <summary>
    ///   Return true if the trade has been terminate as of the specified date.
    /// </summary>
    /// <param name="date">Date to test</param>
    public bool HasBeenTerminated(Dt date)
    {
      if (Termination.IsEmpty()) return false;
      if (Product != null && !Product.Maturity.IsEmpty() && Product.Maturity < Termination)
        return false; // Even if the Termination date was entered after maturity, and it passed validation, we do not consider the Trade terminated in this case, but normally matured ...
      if (Termination <= date)
        return true;
      return false;
    }

    /// <summary>
    ///   Boolean method returning true if there is a Payment pending on or after the specified date
    /// </summary>
    /// <param name="date">Date to test</param>
    /// <returns>True if payment pending</returns>
    public bool PaymentIsActive(Dt date)
    {
      if (Traded <= date && Payment != 0.0)
      {
        return (Settle == date) ? PaymentSettle >= date : PaymentSettle > date;
      }
      return false;
    }

    /// <summary>
    ///  Boolean method returning true if termination is on or before specified date
    /// </summary>
    /// <param name="date">Date to test</param>
    /// <returns>True if termination on or before specified date</returns>
    public bool IsTerminated(Dt date)
    {
      return (!Termination.IsEmpty() && Termination <= date);
    }

    /// <summary>
    /// Boolean method returning true if the trade is cancelled on or before the specified date
    /// </summary>
    /// <param name="date">Date to test</param>
    /// <returns>True if trade is cancelled on or before the test date</returns>
    public bool HasBeenCancelled(Dt date)
    {
      if (TradeStatus != TradeStatus.Canceled)
        return false;

      if (CancellationDate.IsEmpty())
        return true;

      return CancellationDate <= date;
    }

    /// <summary>
    ///   Initialize default values for a new trade
    /// </summary>
    /// <remarks>
    ///   Trade must already have a valid Product set
    ///   If traded date is set then will retain that else it will default traded to today
    /// </remarks>
    /// <param name="generateTradeId">If True, trade id is generated</param>
    public virtual void InitializeDefaultValues(bool generateTradeId)
    {
      InitializeBasicDefaultValues(generateTradeId);
    }

    /// <summary>
    /// We delegate the InitializeDefaultValues() function into this separate function to be able to call it directly
    /// from a derived class when we want to avoid calling the derived version of InitializeDefaultValues()
    /// </summary>
    /// <param name="generateTradeId">If True, trade id is generated</param>
    public void InitializeBasicDefaultValues(bool generateTradeId)
    {
      // Generate a temporary TradeId if the generateTradeId is false
      // to be able to bind this trade to a Market Environment in case 
      // we want to create valid list of in memory trades
      TradeId = (generateTradeId) ? IdGenerator.Generate(this) : TradeUtil.GetNextTransientTradeId();
      if (Traded.IsEmpty())
        Traded = Dt.Today();
      if (SecurityPolicy.CheckNamedPolicy("ShowInTraderList"))
        Trader = EntityContextFactory.User;
      TradeStatus = TradeStatus.Pending;
    }

    /// <summary>
    /// Create a copy of this trade
    /// </summary>
    /// <param name="context"></param>
    /// <returns>Created trade</returns>
    public override PersistentObject CopyAsNew(IEditableEntityContext context)
    {
      var trade = (Trade) base.CopyAsNew(context);

      // Generate new TradeId
      var tradeId = new TradeIdGenerator().Generate(this);
      trade.TradeId = tradeId;

      // For new bespoke products generate unique product name
      if (TradeType == TradeType.New && !ProductUtil.IsStandardProduct(Product))
      {
        trade.Product.Name = tradeId;
      }

      trade.Termination = Dt.Empty;
      trade.CancellationDate = Dt.Empty;
      trade.Deal = null;

      return trade;
    }

    /// <summary>
    ///   Compute the up-front trade payment
    /// </summary>
    /// <remarks>
    ///   <para>Compute the up-front trade payment for a trade using some of the trade properties, and, possibly, some market data.</para>
    ///   <para>We re-use the Payment class to pass the trade payment in the Amount field, trade payment settle in the PayDt field, and the 
    ///   payment currency in the Ccy field.</para>
    ///   <para>Since we do not support yet the trade payment currency different from the trade currency, the Ccy field will be set to
    ///   trade currency for now.</para>
    /// </remarks>
    /// <param name="calcEnvName">Calculation environment name</param>
    /// <param name="eMess">Returns message</param>
    public virtual Payment ComputeTradePayment(string calcEnvName, out string eMess)
    {
      eMess = string.Empty;
      return null; // alternatively, return 0 in the base class; or, make it abstract
    }

    /// <summary>
    ///   Assign Payment and PaymentSettle values from the Payment object returned from
    ///   ComputeTradePayment()
    /// </summary>
    /// <param name="pmt">Payment</param>
    public void AssignTradePayment(Payment pmt)
    {
      Payment = pmt.Amount;
      PaymentSettle = pmt.PayDt;
    }

    /// <summary>
    /// Is trade payment computation supported for this type of trade?
    /// </summary>
    public virtual bool CanComputeTradePayment
    {
      get { return false; }
    }

  	#endregion

    #region Properties

    /// <summary>
    ///   Trade id
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 24, IsKey = true, ReadOnly = true)]
    public string TradeId
    {
      get { return _tradeId; }
      set { _tradeId = value; }
    }

    /// <summary>
    ///   Description of trade
    /// </summary>
    [DataMember]
    public string Description
    {
      get { return "Trade " + TradeId + ": " + Product.Description; }
    }

    /// <summary>
    ///  Trade status
    /// </summary>
    [DataMember]
    [EnumProperty(ReadOnly = true)]
    public TradeStatus TradeStatus
    {
      get { return _tradeStatus; }
      set { _tradeStatus = value; }
    }

    /// <summary>
    ///  Trade type
    /// </summary>
    [DataMember]
    [EnumProperty]
    public TradeType TradeType
    {
      get { return _tradeType; }
      set { _tradeType = value; }
    }

    /// <summary>
    ///   Trade date
    /// </summary>
    /// <remarks>
    ///   The date that the parties agree to the terms of the trade.
    /// </remarks>
    [DataMember]
    [DtProperty(AllowNullValue = false)]
    public Dt Traded
    {
      get { return _traded; }
      set { _traded = value; }
    }

    /// <summary>
    ///   Settlement date of the trade
    /// </summary>
    /// <remarks>
    ///   <para>The date of the earliest payment or start of any credit protection.</para>
    /// </remarks>
    [DataMember]
    [DtProperty(AllowNullValue = false)]
    public Dt Settle
    {
      get { return _settle; }
      set { _settle = value; }
    }

    /// <summary>
    ///   Maturity date of the trade
    /// </summary>
    [DataMember]
    [DtProperty]
    public Dt Termination
    {
      get { return _termination; }
      set { _termination = value; }
    }

    /// <summary>
    ///   Number of contracts traded
    /// </summary>
    /// <remarks>
    ///   <para>This is the number of contracts traded. 
    ///   The Size of a trade is Trade.Amount * Trade.Product.Notional
    ///   For a derivatives trade the Trade.Product.Notional is typically 1.</para>
    /// </remarks>
    [DataMember]
    [NumericProperty(AllowNullValue = false)]
    public double Amount
    {
      get { return _amount; }
      set { _amount = value; }
    }

    /// <summary>
    ///   Trade Settlement Amount. Positive For Outgoing Cash.
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNullValue = false, Format = NumberFormat.Currency, RelatedProperty = "Currency")]
    public double Payment
    {
      get { return _payment; }
      set { _payment = value; }
    }

    /// <summary>
    ///   Payment Settle date
    /// </summary>
    [DataMember]
    [DtProperty(AllowNullValue = true)]
    public Dt PaymentSettle
    {
      get { return _paymentSettle; }
      set { _paymentSettle = value; }
    }

    /// <summary>
    ///   Traded Quoted Level. aka Unwind Premium
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNullValue = false)]
    public double TradedLevel
    {
      get { return _tradedLevel; }
      set { _tradedLevel = value; }
    }

    /// <summary>
    ///   Defines the content of the TradedLevel property
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public QuoteType TradedLevelType
    {
      get { return (QuoteType) ObjectRef.Resolve(_tradedLevelType); }
      set { _tradedLevelType = ObjectRef.Create(value); }
    }

    /// <summary>
    /// ObjectId of referenced TradedLevelType if any, otherwise 0
    /// </summary>
    [DataMember]
    public long TradedLevelTypeId
    {
      get { return _tradedLevelType == null || _tradedLevelType.IsNull ? 0 : _tradedLevelType.Id; }
    }

    /// <summary>
    ///   Traded price currency
    /// </summary>
    [DataMember]
    [EnumProperty(Persistent = false)]
    public Currency Currency
    {
      get { return Product == null ? Currency.None : Product.Ccy; }
    }

    /// <summary>
    ///   Trade strategy
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public Strategy Strategy
    {
      get { return (Strategy) ObjectRef.Resolve(_strategy); }
      set { _strategy = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   Trade strategy
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public SubStrategy SubStrategy
    {
      get { return (SubStrategy) ObjectRef.Resolve(_subStrategy); }
      set { _subStrategy = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   User who booked this trade
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public User Trader
    {
      get { return (User) ObjectRef.Resolve(_trader); }
      set { _trader = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public long TraderId
    {
      get { return _trader == null || _trader.IsNull ? 0 : _trader.Id; }
    }

    /// <summary>
    ///   Counterparty for this trade
    ///   If IsCleared is true, then the Counterparty is assumed to be the Clearing Broker and the
    ///   Clearing Broker field will show the counterparty name, UNLESS the counterparty is the same as Clearing House,
    ///   in which case we assume that there is no clearing broker and the clearing broker field will show blank.
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public LegalEntity Counterparty
    {
      // Note: for Cleared trades, this will change to the Clearing Broker or Clearing House, while the "original" counterparty
      // will be stored in the OriginalCounterparty field.
      get { return (LegalEntity) ObjectRef.Resolve(_counterparty); }
      set { _counterparty = ObjectRef.Create(value); }
    }

    ///// <summary>
    /////   Master Agreement that this trade falls under. If null, trade is treated as individually documented and not subject to netting.
    ///// </summary>
    //[DataMember]
    //[ManyToOneProperty(AllowNullValue = true)]
    //public MasterAgreement MasterAgreement
    //{
    //  get { return (MasterAgreement) ObjectRef.Resolve(_masterAgreement); }
    //  set { _masterAgreement = ObjectRef.Create(value); }
    //}

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public long MasterAgreementId
    {
      get { return _masterAgreement == null || _masterAgreement.IsNull ? 0 : _masterAgreement.Id; }
    }

    /// <summary>
    ///   Guarantor for this trade
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public LegalEntity Guarantor
    {
      get { return (LegalEntity) ObjectRef.Resolve(_guarantor); }
      set { _guarantor = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   The party that brokered this trade. For OTC trades, the Prime Broker. For cleared trades, the executing broker
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public LegalEntity Broker
    {
      // Note: for Cleared trades, this will be the Executing Broker. The Clearing broker is defined in the GetClearingBroker() method.
      get { return (LegalEntity) ObjectRef.Resolve(_broker); }
      set { _broker = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   The clearing broker for this trade
    /// </summary>
    [DataMember]
    [ManyToOneProperty(AllowNullValue = true)]
    public LegalEntity ClearingBroker
    {
      // Note: this property has been added as per FB case 44405 and currently applies just to the 4 futures trade types
      // (not to be confused with the GetClearingBroker() method - see comments there).
      get { return (LegalEntity)ObjectRef.Resolve(_clearingBroker); }
      set { _clearingBroker = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   List of arbitrary name/value pairs for trade
    /// </summary>
    [DataMember]
    [ComponentCollectionProperty(TableName = "TradeTag", CollectionType = "bag")]
    public IList<Tag> Tags
    {
      get { return _tags ?? (_tags = new List<Tag>()); }
      set { _tags = value; }
    }

    /// <summary>
    /// Legal Entity for tax purposes of this trade
    /// </summary>
    [DataMember]
    [ManyToOneProperty]
    public LegalEntity BookingEntity
    {
      get { return (LegalEntity) ObjectRef.Resolve(_bookingEntity); }
      set { _bookingEntity = ObjectRef.Create(value); }
    }

    // Add fields for trades cleared through a Central Clearing Counterparty (aka Clearing House):

    /// <summary>
    ///   If true, this trade will be cleared through a Central Clearing Counterparty (Clearing House)
    /// </summary>
    [DataMember]
    [BooleanProperty]
    public bool IsCleared
    {
      get { return _isCleared; }
      set { _isCleared = value; }
    }

    /// <summary>
    /// Central Clearing Counterparty (Clearing House) (applicable if IsCleared is true)
    /// </summary>
    [DataMember]
    [ManyToOneProperty(AllowNullValue = true)]
    public LegalEntity ClearingHouse
    {
      get { return (LegalEntity)ObjectRef.Resolve(_clearingHouse); }
      set { _clearingHouse = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public long? ClearingHouseId
    {
      get { return _clearingHouse == null || _clearingHouse.IsNull ? null : (long?)_clearingHouse.Id; }
    }

    /// <summary>
    /// Trade Reference from the affirmation platform.
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 256, AllowNullValue = true)]
    public string TradeReference
    {
      get { return _tradeReference; }
      set { _tradeReference = value; }
    }

    /// <summary>
    /// Unique Trade Identifier (aka Unique Swap Identifier, aka Exchange Deal ID)
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 256, AllowNullValue = true)]
    public string UniqueTradeIdentifier
    {
      get { return _uniqueTradeIdentifier; }
      set { _uniqueTradeIdentifier = value; }
    }

    /// <summary>
    /// Unique Product Identifier (UPI) - defines the type of swap (or other OTC product) that has been traded
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 256, AllowNullValue = true)]
    public string UniqueProductIdentifier
    {
      // NOTE: this ideally should be either an enumeration, or a selection from a pre-defined list; however, for now, specifying as a free-text string ...
      // See, for example, http://www2.isda.org/functional-areas/technology-infrastructure/data-and-reporting/identifiers
      get { return _uniqueProductIdentifier; }
      set { _uniqueProductIdentifier = value; }
    }

    /// <summary>
    ///   Date and Time of clearing
    /// </summary>
    [DataMember]
    [DateTimeProperty(AllowNullValue = true)]
    public DateTime? DateTimeCleared
    {
      get { return _dateTimeCleared; }
      set { _dateTimeCleared = value; }
    }

    /// <summary>
    /// When a trade is cleared, the Central Clearing Counterparty (Clearing House), or the Clearing Broker will become the counterparty on the trade.
    /// However, we may still want to keep track of what the original counterparty of the trade was.
    /// </summary>
    [DataMember]
    [ManyToOneProperty(AllowNullValue = true)]
    public LegalEntity OriginalCounterparty
    {
      get { return (LegalEntity)ObjectRef.Resolve(_originalCounterparty); }
      set { _originalCounterparty = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   Product traded
    /// </summary>
    [DataMember]
    [ManyToOneProperty(ReadOnly = true, AllowNullValue = false, Cascade = "save-update", OwnershipResolverType = typeof (TradeProductOwnershipResolver))]
    public Product Product
    {
      get { return (Product) ObjectRef.Resolve(_product); }
      set { _product = ObjectRef.Create(value); }
    }

    /// <summary>
    /// The date on which the trade is cancelled
    /// </summary>
    [DataMember]
    [DtProperty]
    public Dt CancellationDate { get; set; }

    /// <summary>
    ///   ObjectId of referenced Product
    /// </summary>
    [DataMember]
    public long ProductId
    {
      get
      {
        return _product == null ? 0 : ((_product.Id == 0) ? Product.ObjectId : _product.Id);
      }
    }

    /// <summary>
    ///   ObjectId of referenced Strategy
    /// </summary>
    [DataMember]
    public long? StrategyId
    {
      get { return (_strategy == null) ? null : (long?) _strategy.Id; }
    }

    /// <summary>
    ///   ObjectId of Booking entity
    /// </summary>
    [DataMember]
    public long? BookingEntityId
    {
      get { return (_bookingEntity == null) ? null : (long?)_bookingEntity.Id; }
    }

    /// <summary>
    ///  ObjectId of Counterparty
    /// </summary>
    [DataMember]
    public long? CounterpartyId
    {
      get { return (_counterparty == null) ? null : (long?)_counterparty.Id; }
    }

    /// <summary>
    ///   Broker id
    /// </summary>
    [DataMember]
    public long? BrokerId
    {
      get { return (_broker == null) ? null : (long?)_broker.Id; }
    }

    /// <summary>
    ///   Clearing Broker id
    /// </summary>
    [DataMember]
    public long? ClearingBrokerId
    {
      get { return (_clearingBroker == null) ? null : (long?)_clearingBroker.Id; }
    }


    /// <summary>
    ///   ObjectId of referenced SubStrategy
    /// </summary>
    [DataMember]
    public long? SubStrategyId
    {
      get { return (_subStrategy == null) ? null : (long?) _subStrategy.Id; }
    }

    /// <summary>
    ///   Trade to use to determine reference entity
    /// </summary>
    [DataMember]
    [ManyToOneProperty(OwnershipResolverType = typeof(LeadTradeOwnershipResolver))]
    public Trade LeadTrade
    {
      get { return (Trade) ObjectRef.Resolve(_leadTrade); }
      set { _leadTrade = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public long LeadTradeId
    {
      get { return _leadTrade == null || _leadTrade.IsNull ? 0 : _leadTrade.Id; }
    }

    /// <summary>
    ///   Initial margin
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNullValue = false, Format = NumberFormat.Percentage)]
    public double InitialMargin
    {
      get { return _initialMargin; }
      set { _initialMargin = value; }
    }

    /// <summary>
    ///   The Deal this Trade Belongs To
    /// </summary>
    [DataMember]
    [ManyToOneProperty(AllowNullValue = true)]
    [Browsable(false)]
    public Deal Deal
    {
      get { return (Deal)ObjectRef.Resolve(_deal); }
      set { _deal = ObjectRef.Create(value); }
    }

    /// <summary>
    /// The ObjectId of the Deal for this Trade, else 0
    /// </summary>
    [DataMember]
    public long DealId
    {
      get { return _deal == null || _deal.IsNull ? 0 : _deal.Id; }
    }

    

    #endregion

    #region Non-persistent Properties

    /// <summary>
    ///   Whether or not the trade is Full/Partial Unwind/Assign
    /// </summary>
    public bool IsUnwindOrAssign
    {
      get { return (TradeType != TradeType.New); }
    }
   

    #endregion

    #region Data

    private static readonly TradeIdGenerator IdGenerator = new TradeIdGenerator();
    private double _amount;
    private ObjectRef _bookingEntity;
    private ObjectRef _broker;
    private ObjectRef _counterparty;
    private ObjectRef _deal;
    private ObjectRef _guarantor;
    private double _initialMargin;
    private ObjectRef _leadTrade;
    private ObjectRef _masterAgreement;
    private Dt _paymentSettle;
    private double _payment;
    private ObjectRef _clearingBroker;

    private ObjectRef _product;
    private Dt _settle;
    private ObjectRef _strategy;
    private ObjectRef _subStrategy;
    private IList<Tag> _tags;
    private Dt _termination;
    private string _tradeId;
    private TradeStatus _tradeStatus;
    private TradeType _tradeType;
    private ObjectRef _tradedLevelType;
    private double _tradedLevel;
    private Dt _traded;
    private ObjectRef _trader;
    private bool _isCleared;
    private ObjectRef _clearingHouse;
    private string _tradeReference;
    private string _uniqueTradeIdentifier;
    private string _uniqueProductIdentifier ;
    private DateTime? _dateTimeCleared;
    private ObjectRef _originalCounterparty;

    #endregion
  }
}
