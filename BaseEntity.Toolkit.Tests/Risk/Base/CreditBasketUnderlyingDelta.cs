using System;
using System.Runtime.Serialization;
using BaseEntity.Database;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{

  ///
	/// <summary>
	///   Underlying component of a BasketSubstitutionEvent and CorporateActionEvent 
	///   representing the change in the underlyings of a credit basket
	/// </summary>
	///
  /// <remarks>
  ///   <para>This represents an individual underlying reference credit change for a basket</para>
  /// </remarks>
  ///
	[Component]
  [DataContract]
  [Serializable]
  public class CreditBasketUnderlyingDelta : BaseEntityObject
	{
		#region Constructors


    /// <summary>
    /// Initializes a new instance of the <see cref="CreditBasketUnderlyingDelta"/> class.
    /// </summary>
    public CreditBasketUnderlyingDelta()
    {
    }

		#endregion

		#region Properties

    /// <summary>
    /// Gets or sets a value inndicating whether this participation was added, removed, or amended
    /// </summary>
    [DataMember]
    [EnumProperty]
    public ObjectChangedType Type
    {
      get { return _type; }
      set { _type = value; }
    }

    /// <summary>
    ///   Gets or sets the reference LegalEntity
    /// </summary>
    [ManyToOneProperty(Column="ReferenceEntityId", AllowNullValue = false)]
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
    ///   Gets or sets the Currency of reference obligation
    /// </summary>
    [DataMember]
    [EnumProperty]
		public Currency Currency
		{
			get { return _currency; }
			set { _currency = value; }
		}


		/// <summary>
		///   Gets or sets the Seniority of the underlying credit
		/// </summary>
    [DataMember]
    [EnumProperty]
		public Seniority Seniority
		{
			get { return _seniority; }
			set { _seniority = value; }
		}

    /// <summary>
    ///   Gets or sets the Cancellability of the underlying credit
    /// </summary>
    [DataMember]
    [EnumProperty]
    public Cancellability Cancellability 
    {
      get;
      set;
    }

    /// <summary>
    ///   Gets or sets the ISDA restructuring treatment for the underlying credit
    /// </summary>
    ///
    [DataMember]
    [EnumProperty]
    public RestructuringType RestructuringType
    {
      get { return _restructuringType; }
      set { _restructuringType = value; }
    }

		/// <summary>
		///   Gets or sets the Reference Obligation
		/// </summary>
		[ManyToOneProperty]
		public ReferenceObligation ReferenceObligation
		{
			get { return (ReferenceObligation)ObjectRef.Resolve(_referenceObligation); }
			set { _referenceObligation = ObjectRef.Create(value); }
		}

    /// <summary>
    ///   Gets or sets the optional Per Name Fixed Recovery Rate 
    /// </summary>
    ///
    [DataMember]
    [NumericProperty(Format = NumberFormat.Percentage)]
    public double FixedRecoveryRate
    {
      get { return _fixedRecoveryRate; }
      set { _fixedRecoveryRate = value; }
    }

    /// <summary>
    ///   Gets or sets the Old Percentage of the full Notional amount for this element
    /// </summary>
    ///
    [DataMember]
    [NumericProperty(Format = NumberFormat.Percentage)]
    public double OldPercentage
    {
      get { return _oldPercentage; }
      set { _oldPercentage = value; }
    }

    /// <summary>
    ///   Gets or sets the New Percentage of the full Notional amount for this element
    /// </summary>
    ///
    [DataMember]
    [NumericProperty(Format = NumberFormat.Percentage)]
    public double NewPercentage
    {
      get { return _newPercentage; }
      set { _newPercentage = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    public string Key
    {
      get
      {
        return String.Format("{0}.{1}.{2}.{3}.{4}", ReferenceEntity.Ticker, Currency, Seniority, RestructuringType,
                             Cancellability);
      }
    }

    #endregion

		#region Data

    private ObjectChangedType _type;
    private Currency _currency;
    private Seniority _seniority;
		private RestructuringType _restructuringType;
    [DataMember] private ObjectRef _referenceEntity;
    [DataMember] private ObjectRef _referenceObligation;
    private double _fixedRecoveryRate;
    private double _oldPercentage;
    private double _newPercentage;

		#endregion
	}
}
