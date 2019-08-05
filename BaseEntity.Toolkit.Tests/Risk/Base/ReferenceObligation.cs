/*
 * ReferenceObligation.cs
 *
 */

using System;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
	/// <summary>
	///  Contains characteristics of a bond or other debt obligation 
  ///  used as an ISDA credit reference obligation
  /// </summary>
  /// 
  /// <remarks>
  ///   <para>This is is not a direct link to a reference bond to relax the need to have detailed
  ///   information on each reference obligation.</para>
  /// </remarks>
  ///
  [Serializable]
	[Entity(EntityId = 105, AuditPolicy = AuditPolicy.History, Description = "Contains characteristics of a bond or other debt obligation used as an ISDA credit reference obligation")]
	public class ReferenceObligation : AuditedObject
	{
		#region Constructors

		/// <summary>
		///   Constructor for internal use
		/// </summary>
		///
		internal ReferenceObligation()
		{
		}

		#endregion Constructors

		#region Properties

		/// <summary>
    ///   Name/Description of this Reference Obligation
		/// </summary>
		[StringProperty(MaxLength=64, IsKey=true)]
		public string Name
		{
			get { return name_; }
			set { name_ = value; }
		}

		/// <summary>
    ///   Currency of the Reference Obligation
    /// </summary>
		[EnumProperty]
		public Currency Ccy
		{
			get { return ccy_; }
			set { ccy_ = value; }
		}

    /// <summary>
    ///   Seniority of this Reference Obligation
    /// </summary>
		[EnumProperty]
    public Seniority Seniority
    {
      get { return seniority_; }
      set { seniority_ = value; }
    }

    /// <summary>
    ///   The legal entity primarily responsible for repaying debt to a creditor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>ISDA 2003 Term: Primary Obligor</para>
    /// </remarks>
    /// 
 		[ManyToOneProperty]
    public LegalEntity PrimaryObligor
    {
      get { return (LegalEntity)ObjectRef.Resolve(primaryObligor_); }
      set { primaryObligor_ = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   The legal entity that guarantees to pay the debts of the obligor of the obligor is unable to pay
    /// </summary>
    ///
    /// <remarks>
    ///   <para>ISDA 2003 Term: Guarantor</para>
    /// </remarks>
		[ManyToOneProperty]
    public LegalEntity Guarantor
    {
      get { return (LegalEntity)ObjectRef.Resolve(guarantor_); }
      set { guarantor_ = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   Maturity date of this Reference Obligation
    /// </summary>
		[DtProperty]
    public Dt Maturity
    {
      get { return maturity_; }
      set { maturity_ = value; }
    }

    /// <summary>
    ///   Coupon (if any) of this Reference Obligation
    /// </summary>
		[NumericProperty]
    public Nullable<Double> Coupon
    {
      get { return coupon_; }
      set { coupon_ = value; }
    }

		/// <summary>
		///   True if the market has adopted this obligation
		///   as the preferred reference obligation for this entity and
		///   tier of debt.
		/// </summary>
		[BooleanProperty]
		public bool IsPreferred
		{
			get { return isPreferred_; }
			set { isPreferred_ = value; }
		}

		/// <summary>
    ///   ISIN for this Reference Obligation
		/// </summary>
		[StringProperty(MaxLength=16)]
		public string ISIN
		{
			get { return isin_; }
			set { isin_ = value; }
		}

		/// <summary>
    ///   CLIP for this Reference Obligation
		/// </summary>
		[StringProperty(MaxLength=32)]
		public string CLIP
		{
			get { return clip_; }
			set { clip_ = value; }
		}

		/// <summary>
    ///   Cusip for this Reference Obligation
		/// </summary>
		[StringProperty(MaxLength=16)]
		public string Cusip
		{
			get { return cusip_; }
			set { cusip_ = value; }
		}

		/// <summary>
		///   Date this instance is valid from.
		/// </summary>
		///
		/// <remarks>
		///   <para>Entities may be created from the merger or restructuring
		///   of a previous entity.</para>
		/// </remarks>
		///
		[DtProperty]
		public Dt RedValidFrom
		{
			get { return _redValidFrom; }
			set { _redValidFrom = value; }
		}

		/// <summary>
		///   Date this instance is valid to.
		/// </summary>
		///
		/// <remarks>
		///   <para>ReferenceObligations may merge or restructure into other entities.</para>
		/// </remarks>
		///
		[DtProperty]
		public Dt RedValidTo
		{
			get { return _redValidTo; }
			set { _redValidTo = value; }
		}

		#endregion Properties

    #region Data

		private string name_;
    private Currency ccy_;
    private Seniority seniority_;
    private ObjectRef primaryObligor_;
    private ObjectRef guarantor_;
    private Dt maturity_;
    private Nullable<Double> coupon_;
		private bool isPreferred_;
    private string isin_;
    private string clip_;
    private string cusip_;
		private Dt _redValidFrom;
		private Dt _redValidTo;

		#endregion Data

	} // class ReferenceObligation
}  
