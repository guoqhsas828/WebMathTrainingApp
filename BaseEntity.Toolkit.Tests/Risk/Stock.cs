using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Database.Engine;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Risk
{
  [Serializable]
  [Entity(EntityId = 309, Description = "Stock", DisplayName = "Stock", Category = "Equities")]
  [StandardProduct]
  public class Stock : Product, IReferenceCredit, IReferenceCreditsOwner, ISingleNameProduct
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    public Stock()
    { }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var clone = (Stock)base.Clone();

      clone.dividends_ = CloneUtil.CloneToGenericList(dividends_);

      return clone;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    /// <param name="errors">List to append errors to</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (Issuer == null)
        InvalidValue.AddError(errors, this, "Issuer", "Issuer can not be null");

      // Validate dividends
      if (dividends_ != null)
      {
        foreach (Dividend d in dividends_)
          d.Validate(errors);
      }

      ValidateCreditEvent(errors);

      return;
    }

    /// <summary>
    ///   Determine if a negative amount means its a Buy
    /// </summary>
    /// <remarks>
    ///   <para>For most products a positive notional is a buy. Credit products like CDS follow the
    ///   reverse convention so that a positive notional means long credit.</para>
    /// </remarks>
    /// <returns>true - negative amt is buy, false - positive amt is buy</returns>
    public override bool BuyIsNegative()
    {
      return false;
    }

    /// <summary>
    ///   Calculate natural settle date for this product if traded on specified asof date
    /// </summary>
    /// <param name="asOf"></param>
    /// <returns>Settlement date</returns>
    public override Dt CalcSettle(Dt asOf)
    {
      return Dt.AddDays(asOf, this.DaysToSettle, this.Calendar);
    }

    /// <summary>
    /// Convert to toolkit stock
    /// </summary>
    /// <param name="asOf">The asof date </param>
    /// <param name="stockDividendInput">Stock Dividend Inputs, such as 
    /// DividendSchedule, DividendYield</param>
    /// <returns></returns>
    public Toolkit.Products.Stock ToToolkitStock(Dt asOf, Toolkit.Products.Stock.StockDividendInput stockDividendInput
      = Toolkit.Products.Stock.StockDividendInput.None)
    {
      return new Toolkit.Products.Stock(Currency, Ticker,
        (stockDividendInput != Toolkit.Products.Stock.StockDividendInput.DividendYield)
          ? Dividends?.Where(d => d.ExDivDate > asOf).Select(d =>
            new Toolkit.Products.Stock.Dividend(d.ExDivDate, d.PaymentDate,
              DividendSchedule.DividendType.Fixed, d.Amount)).ToList()
          : new List<Toolkit.Products.Stock.Dividend>());
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Ticker
    /// </summary>
    [StringProperty(MaxLength = 16)]
    public string Ticker
    {
      get { return ticker_; }
      set { ticker_ = value; }
    }

    /// <summary>
    ///   Cusip
    /// </summary>
    [StringProperty(MaxLength = 16)]
    public string Cusip
    {
      get { return cusip_; }
      set { cusip_ = value; }
    }

    /// <summary>
    ///   ISIN
    /// </summary>
    [StringProperty(MaxLength = 16)]
    public string ISIN
    {
      get { return isin_; }
      set { isin_ = value; }
    }

    /// <summary>
    ///   Dividends
    /// </summary>
    [ComponentCollectionProperty(TableName = "Dividend", KeyColumns = new string[] {"StockId"}, IndexColumn = "Idx")]
    public IList<Dividend> Dividends
    {
      get { return dividends_ ?? (dividends_ = new List<Dividend>()); }
      set { dividends_ = value; }
    }

    /// <summary>
    ///   Holiday calendar (used to determine valid settlement + dividend payment days)
    /// </summary>
    [CalendarProperty]
    public Calendar Calendar
    {
      get { return calendar_; }
      set { calendar_ = value; }
    }

    /// <summary>
    ///   Name of issuer of product
    /// </summary>
    [ManyToOneProperty(Column = "IssuerId")]
    public LegalEntity Issuer
    {
      get { return (LegalEntity)ObjectRef.Resolve(issuer_); }
      set { issuer_ = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long? IssuerId => issuer_ == null || issuer_.IsNull ? null : (long?)issuer_.Id;

    #endregion

    #region IReferenceCredit Members

    /// <summary>
    ///   Reference entity
    /// </summary>
    public LegalEntity ReferenceEntity
    {
      get { return Issuer; }
    }

    /// <summary>
    /// 
    /// </summary>
    public long ReferenceEntityId
    {
      get { return issuer_ == null || issuer_.IsNull ? 0 : issuer_.Id; }
    }

    /// <summary>
    /// 
    /// </summary>
    public ReferenceObligation ReferenceObligation
    {
      get { return null; }
    }

    /// <summary>
    /// 
    /// </summary>
    public long ReferenceObligationId
    {
      get { return 0; }
    }

    /// <summary>
    ///   Restructuring type
    /// </summary>
    public RestructuringType RestructuringType
    {
      get { return Issuer.DefaultRestructuring; }
    }

    /// <summary>
    ///   Seniority
    /// </summary>
    public Seniority Seniority
    {
      get { return Seniority.Equity; }
    }

    /// <summary>
    ///   Cancellability
    /// </summary>
    public Cancellability Cancellability
    {
      get { return Cancellability.None; }
    }

    /// <summary>
    ///   Currency
    /// </summary>
    public Currency Currency
    {
      get { return Ccy; }
    }

    /// <summary>
    ///   CreditEvent
    /// </summary>
    [ManyToOneProperty]
    public CreditEvent CreditEvent
    {
      get { return (CreditEvent)ObjectRef.Resolve(creditEvent_); }
      set { creditEvent_ = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long CreditEventId
    {
      get { return creditEvent_ == null || creditEvent_.IsNull ? 0 : creditEvent_.Id; }
    }

    /// <summary>
    ///   Cancellation Event
    /// </summary>
    public CancellationEvent CancellationEvent
    {
      get { return null; }
      set
      {
        throw new InvalidOperationException(
          String.Format("Cancellation Event cannot be assiciated with this Product type [{0}].", this.GetType().Name));
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public long CancellationEventId
    {
      get { return 0; }
    }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.EventDeterminationDate"/>
    /// </summary>
    public Dt OverriddenEventDeterminationDate
    {
      get { return Dt.Empty; }
      set
      {
        throw new InvalidOperationException(
          String.Format("Cannot override Credit Event details for this Product type [{0}].", this.GetType().Name));
      }
    }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.RecoveryAnnounceDate"/>
    /// </summary>
    public Dt OverriddenRecoveryAnnounceDate
    {
      get { return Dt.Empty; }
      set
      {
        throw new InvalidOperationException(
          String.Format("Cannot override Credit Event details for this Product type [{0}].", this.GetType().Name));
      }
    }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.RecoverySettlementDate"/>
    /// </summary>
    public Dt OverriddenRecoverySettlementDate
    {
      get { return Dt.Empty; }
      set
      {
        throw new InvalidOperationException(
          String.Format("Cannot override Credit Event details for this Product type [{0}].", this.GetType().Name));
      }
    }

    /// <summary>
    ///   Override rate for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.RealizedRecoveryRate"/>
    /// </summary>
    public double? OverriddenRealizedRecoveryRate
    {
      get { return null; }
      set
      {
        throw new InvalidOperationException(
          String.Format("Cannot override Credit Event details for this Product type [{0}].", this.GetType().Name));
      }
    }

    /// <inheritdoc cref="ReferenceCreditUtil.GetEventDeterminationDate(IReferenceCredit)" select="summary|remarks" />
    public Dt EventDeterminationDate
    {
      get { return ReferenceCreditUtil.GetEventDeterminationDate(this); }
    }

    /// <inheritdoc cref="ReferenceCreditUtil.GetRecoveryAnnounceDate(IReferenceCredit)" select="summary|remarks" />
    public Dt RecoveryAnnounceDate
    {
      get { return ReferenceCreditUtil.GetRecoveryAnnounceDate(this); }
    }

    /// <inheritdoc cref="ReferenceCreditUtil.GetRecoverySettlementDate(IReferenceCredit)" select="summary|remarks" />
    public Dt RecoverySettlementDate
    {
      get { return ReferenceCreditUtil.GetRecoverySettlementDate(this); }
    }

    /// <inheritdoc cref="ReferenceCreditUtil.GetRealizedRecoveryRate(IReferenceCredit)" select="summary|remarks" />
    public double RealizedRecoveryRate
    {
      get { return ReferenceCreditUtil.GetRealizedRecoveryRate(this); }
    }

    /// <inheritdoc cref="ReferenceCreditUtil.IsOverridden(IReferenceCredit)" select="summary|remarks" />
    public bool IsOverridden
    {
      get { return ReferenceCreditUtil.IsOverridden(this); }
    }

    /// <inheritdoc cref="ReferenceCreditUtil.IsDefaultedOn(Dt, IReferenceCredit)" select="summary|remarks|returns" />
    /// <param name="asOf">date to test</param>
    public bool IsDefaultedOn(Dt asOf)
    {
      return ReferenceCreditUtil.IsDefaultedOn(asOf, this);
    }

    /// <inheritdoc cref="ReferenceCreditUtil.IsRecoveryAnnouncedOn(Dt, IReferenceCredit)" select="summary|remarks|returns" />
    /// <param name="asOf">date to test</param>
    public bool IsRecoveryAnnouncedOn(Dt asOf)
    {
      return ReferenceCreditUtil.IsRecoveryAnnouncedOn(asOf, this);
    }

    /// <inheritdoc cref="ReferenceCreditUtil.IsRecoverySettledOn(Dt, IReferenceCredit)" select="summary|remarks|returns" />
    /// <param name="asOf">date to test</param>
    public bool IsRecoverySettledOn(Dt asOf)
    {
      return ReferenceCreditUtil.IsRecoverySettledOn(asOf, this);
    }

    /// <inheritdoc cref="ReferenceCreditUtil.ValidateCreditEvent(ArrayList, IReferenceCredit)" select="summary|remarks|returns" />
    /// <param name="errors">List to add reported errors to</param>
    public void ValidateCreditEvent(ArrayList errors)
    {
      ReferenceCreditUtil.ValidateCreditEvent(errors, this);
    }

    #endregion

    #region IReferenceCreditsOwner Members

    /// <summary>
    ///   ObjectId of the Reference Credits Owner 
    /// </summary>
    long IReferenceCreditsOwner.ObjectId
    {
      get { return this.ObjectId; }
    }

    /// <summary>
    ///   Name of the Reference Credits Owner
    /// </summary>
    string IReferenceCreditsOwner.Name
    {
      get { return this.Name; }
    }

    /// <summary>
    ///   List of Reference Credits owned by this object
    /// </summary>
    IList<IReferenceCredit> IReferenceCreditsOwner.ReferenceCredits
    {
      get { return new List<IReferenceCredit> {this}; }
    }

    /// <summary>
    ///   Apply Corporate Action
    /// </summary>
    IList<CreditBasketUnderlyingDelta> IReferenceCreditsOwner.ApplyCorporateAction(IList<CorporateActionEventItem> corpActionItems)
    {
      // Validate
      if (corpActionItems.Count > 1)
      {
        throw new BusinessEventApplyException(String.Format("Cannot automatically apply Mergers/Demergers to Stock [{0}]. Manually Unwind the Position.",
          Name));
      }

      CorporateActionEventItem item = corpActionItems[0];
      if (!item.PercentDebtTransferred.ApproximatelyEqualsTo(1.0))
      {
        throw new BusinessEventApplyException(
          String.Format(
            "Cannot automatically apply a Corporate Action to Stock [{0}] where 100% of the debt is not transferred to the new Entity. Manually Unwind the Position.",
            Name));
      }

      if (this.Issuer != item.OldReferenceEntity)
        throw new BusinessEventApplyException(String.Format("ReferenceEntity on Stock [{0}] does not match entity [{1}]", Name, item.OldReferenceEntity.Name));

      // Apply
      this.Issuer = item.NewReferenceEntity;
      return null;
    }

    /// <summary>
    ///   Unapply Corporate Action
    /// </summary>
    void IReferenceCreditsOwner.UnApplyCorporateAction(IList<CorporateActionEventItem> corpActionItems, IList<CreditBasketUnderlyingDelta> underlyingDeltas)
    {
      // Validate
      CorporateActionEventItem item = corpActionItems[0];
      if (this.Issuer != item.NewReferenceEntity)
        throw new BusinessEventRollbackException(
          String.Format("ReferenceEntity on Stock [{0}] does not match entity [{1}]", Name, item.NewReferenceEntity.Name));

      // Unapply
      this.Issuer = item.OldReferenceEntity;
    }

    #endregion

    #region ICreditProduct Members

    /// <summary>
    ///   List of objects that owns one or more reference credits
    /// </summary>
    IEnumerable<IReferenceCreditsOwner> ICreditProduct.ReferenceCreditsOwners
    {
      get { yield return this; }
    }

    #endregion

    #region ISingleNameProduct Members

    /// <summary>
    ///  Reference credit
    /// </summary>
    IReferenceCredit ISingleNameProduct.ReferenceCredit
    {
      get { return this; }
    }

    #endregion

    #region Data

    private string ticker_; // Equity ticker
    private string cusip_;
    private string isin_;
    private IList<Dividend> dividends_;
    private Calendar calendar_;

    private ObjectRef creditEvent_;
    private ObjectRef issuer_;

    #endregion

    /// <summary>
    ///   Financial Instrument Global Identifier (Formerly BBGID)
    /// </summary>
    [StringProperty(MaxLength = 32)]
    public string FIGI { get; set; }

  } // class Stock

}
