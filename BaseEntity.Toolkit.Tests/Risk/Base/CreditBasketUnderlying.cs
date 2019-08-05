/*
 * CreditBasketUnderlying.cs
 *
 */

using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{

  ///
	/// <summary>
	///   Underlying component of a credit basket
	/// </summary>
	///
  /// <remarks>
  ///   <para>This represents an individual underlying reference credit for a basket</para>
	///
  /// </remarks>
  ///
  [Component(ChildKey = new[] { "ReferenceEntity", "Seniority", "RestructuringType", "Currency", "Cancellability" })]
  [Serializable]
  public class CreditBasketUnderlying : BaseEntityObject, IComparable, IReferenceCredit
	{
		#region Constructors

    /// <summary>
    ///   constructor
    /// </summary>
    public CreditBasketUnderlying()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="referenceEntity"></param>
    /// <param name="referenceObligation"></param>
    /// <param name="currency"></param>
    /// <param name="seniority"></param>
    /// <param name="restructuringType"></param>
    /// <param name="percentage"></param>
    public CreditBasketUnderlying(LegalEntity referenceEntity, ReferenceObligation referenceObligation, Currency currency, Seniority seniority, RestructuringType restructuringType, double percentage)
      : this(referenceEntity, referenceObligation, currency, seniority, restructuringType, percentage, null, null)
    {
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="referenceEntity"></param>
    /// <param name="referenceObligation"></param>
    /// <param name="currency"></param>
    /// <param name="seniority"></param>
    /// <param name="restructuringType"></param>
    /// <param name="percentage"></param>
    /// <param name="creditEvent"></param>
    public CreditBasketUnderlying(LegalEntity referenceEntity, ReferenceObligation referenceObligation, Currency currency, Seniority seniority, RestructuringType restructuringType, double percentage, CreditEvent creditEvent)
      : this(referenceEntity, referenceObligation, currency, seniority, restructuringType, percentage, creditEvent, null)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="referenceEntity"></param>
    /// <param name="referenceObligation"></param>
    /// <param name="currency"></param>
    /// <param name="seniority"></param>
    /// <param name="restructuringType"></param>
    /// <param name="percentage"></param>
    /// <param name="cancellationEvent"></param>
    public CreditBasketUnderlying(LegalEntity referenceEntity, ReferenceObligation referenceObligation, Currency currency, Seniority seniority, RestructuringType restructuringType, double percentage, CancellationEvent cancellationEvent)
      : this(referenceEntity, referenceObligation, currency, seniority, restructuringType, percentage, null, cancellationEvent)
    {
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="referenceEntity"></param>
    /// <param name="referenceObligation"></param>
    /// <param name="currency"></param>
    /// <param name="seniority"></param>
    /// <param name="restructuringType"></param>
    /// <param name="percentage"></param>
    /// <param name="creditEvent"></param>
    /// <param name="cancellationEvent"></param>
    public CreditBasketUnderlying(LegalEntity referenceEntity, ReferenceObligation referenceObligation, Currency currency, Seniority seniority, RestructuringType restructuringType, double percentage, CreditEvent creditEvent, CancellationEvent cancellationEvent)
    {
      this.ReferenceEntity = referenceEntity;
      this.ReferenceObligation = referenceObligation;
      this.Currency = currency;
      this.Seniority = seniority;
      this.RestructuringType = restructuringType;
      this.Percentage = percentage;
      this.CreditEvent = creditEvent;
      this.CancellationEvent = cancellationEvent;
    }

		#endregion

		#region Methods

		/// <summary>
		///   Validate
		/// </summary>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (this.ReferenceEntity == null)
        InvalidValue.AddError(errors, this, "ReferenceEntity cannot be null!");

		  ValidateCreditEvent(errors);

		  return;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      var clone = (CreditBasketUnderlying)base.Clone();
      clone.ReferenceEntity = this.ReferenceEntity;
      clone.ReferenceObligation = this.ReferenceObligation;
      
      clone.CreditEvent = this.CreditEvent;
      clone.CancellationEvent = this.CancellationEvent;

      clone.OverriddenEventDeterminationDate = this.OverriddenEventDeterminationDate;
      clone.OverriddenRecoveryAnnounceDate = this.OverriddenRecoveryAnnounceDate;
      clone.OverriddenRecoverySettlementDate = this.OverriddenRecoveryAnnounceDate;
      clone.OverriddenRealizedRecoveryRate = this.OverriddenRealizedRecoveryRate;

      return clone;
    }

    /// <summary>
    ///  Overriden ToString() method
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return ((Cancellability != Cancellability.None))
               ? String.Format("{0}.{1}.{2}.{3}.{4}", Ticker, Currency, Seniority, RestructuringType, Cancellability)
               : String.Format("{0}.{1}.{2}.{3}", Ticker, Currency, Seniority, RestructuringType);
    }

		#endregion // methods

    #region IComparable

    /// <summary>
    ///   IComparable.CompareTo implementation.
    /// </summary>
    public int CompareTo(object obj)
    {
      if (obj is CreditBasketUnderlying)
        return ((IComparable)ReferenceEntity).CompareTo(((CreditBasketUnderlying)obj).ReferenceEntity);

      throw new ArgumentException("object is not a CreditBasketUnderlying");
    }

    #endregion IComparable

		#region Properties

    /// <summary>
    ///   Reference LegalEntity
    /// </summary>
    [ManyToOneProperty(Column = "ReferenceEntityId", AllowNullValue = false, Fetch = "join")]
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
		///   Currency of reference obligation
		/// </summary>
		[EnumProperty]
    public Currency Currency { get; set; }

		/// <summary>
		///   Seniority
		/// </summary>
		[EnumProperty]
    public Seniority Seniority { get; set; }

    /// <summary>
    ///   ISDA restructuring treatment
    /// </summary>
    ///
		[EnumProperty]
    public RestructuringType RestructuringType { get; set; }

    /// <summary>
    ///   Cancellability
    /// </summary>
    [EnumProperty]
    public Cancellability Cancellability { get; set; }

    /// <summary>
    ///   Percentage of the full Notional amount for this element
    /// </summary>
    ///
    [NumericProperty(Format = NumberFormat.Percentage, AllowNullValue = false)]
    public double Percentage { get; set; }

    /// <summary>
    ///   Optional per name Fixed Recovery Rate
    /// </summary>
    ///
    [NumericProperty(Format = NumberFormat.Percentage)]
    public double FixedRecoveryRate { get; set; }

		/// <summary>
		///   Reference Obligation
		/// </summary>
		[ManyToOneProperty]
		public ReferenceObligation ReferenceObligation
		{
			get { return (ReferenceObligation)ObjectRef.Resolve(_referenceObligation); }
			set { _referenceObligation = ObjectRef.Create(value); }
		}

    /// <summary>
    /// 
    /// </summary>
    public long ReferenceObligationId => _referenceObligation == null || _referenceObligation.IsNull ? 0 : _referenceObligation.Id;

	  /// <summary>
    /// 
    /// </summary>
    public string Ticker
    {
      get { return this.ReferenceEntity.Ticker; }
    }

    /// <summary>
    /// 
    /// </summary>
    public string Key => $"{ReferenceEntity.Ticker}.{Currency}.{Seniority}.{RestructuringType}.{Cancellability}";

	  #endregion

    #region IReferenceCredit Members

    /// <summary>
    ///   CreditEvent
    /// </summary>
    [ManyToOneProperty]
    public CreditEvent CreditEvent
    {
      get { return (CreditEvent)ObjectRef.Resolve(_creditEvent); }
      set { _creditEvent = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long CreditEventId
    {
      get { return _creditEvent == null || _creditEvent.IsNull ? 0 : _creditEvent.Id; }
    }

    /// <summary>
    ///   CancellationEvent
    /// </summary>
    [ManyToOneProperty]
    public CancellationEvent CancellationEvent
    {
      get { return (CancellationEvent)ObjectRef.Resolve(_cancellationEvent); }
      set { _cancellationEvent = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long CancellationEventId
    {
      get { return _cancellationEvent == null || _cancellationEvent.IsNull ? 0 : _cancellationEvent.Id; }
    }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.EventDeterminationDate"/>
    /// </summary>
    [DtProperty]
    public Dt OverriddenEventDeterminationDate { get; set; }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.RecoveryAnnounceDate"/>
    /// </summary>
    [DtProperty]
    public Dt OverriddenRecoveryAnnounceDate { get; set; }

    /// <summary>
    ///   Override date for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.RecoverySettlementDate"/>
    /// </summary>
    [DtProperty]
    public Dt OverriddenRecoverySettlementDate { get; set; }

    /// <summary>
    ///   Override rate for the applied CreditEvent 
    ///   <see cref="BaseEntity.Risk.CreditEvent.RealizedRecoveryRate"/>
    /// </summary>
    [NumericProperty(AllowNullValue = true)]
    public double? OverriddenRealizedRecoveryRate { get; set; }

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

    #region Data

    private ObjectRef _referenceEntity;
    private ObjectRef _referenceObligation;

    private ObjectRef _creditEvent;
    private ObjectRef _cancellationEvent;

#if NOTNOW
    private ObjectRef cDSScaling_;
#endif

    #endregion
    
  } // CreditBasketUnderlying
}  
