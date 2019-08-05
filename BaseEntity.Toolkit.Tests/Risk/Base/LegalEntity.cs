using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  ///
  /// <summary>
  ///   Representation for a legal entity.
	/// </summary>
	///
	/// <remarks>
	///   <para>The LegalEntity class maps closely to the ISDA definition of an LegalEntity.</para>
	///
	///   <para>Entities can have many different roles including a Party in a trade, a
	///   Guarantor or a Broker.</para>
  /// </remarks>
  ///
  [DataContract]
  [Serializable]
  [Entity(EntityId = 100, Name = "LegalEntity", AuditPolicy = AuditPolicy.History, Description = "The LegalEntity class maps closely to the ISDA definition of an LegalEntity")]
  public class LegalEntity : AuditedObject, IComparable, IHasRatings, IHasTags
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    protected LegalEntity()
    {}

    #endregion

    #region Properties

    /// <summary>
    ///   Short name
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 64, IsKey = true)]
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    ///   Full company legal name, including punctuation
    /// </summary>
    [StringProperty(MaxLength = 128, AllowNullValue = false)]
    public string LongName
    {
      get { return longName_; }
      set { longName_ = value; }
    }

    /// <summary>
    ///   Issuer equity ticker
    /// </summary>
    [StringProperty(MaxLength = 20)]
    public string Ticker
    {
      get { return ticker_; }
      set { ticker_ = value; }
    }

    /// <summary>
    ///   Markit RED identifier
    /// </summary>
    [StringProperty(MaxLength = 6)]
    public string CLIP
    {
      get { return clip_; }
      set { clip_ = value; }
    }

    /// <summary>
    ///   Global Legal Entity Identifier (LEI)
    /// </summary>
    [StringProperty(MaxLength = 64, AllowNullValue = true)]
    public string LEI
    {
      // See, for example, http://www2.isda.org/functional-areas/technology-infrastructure/data-and-reporting/identifiers
      // It has been stated that this identifier is just 20 characters, but specifying more, just in case ...
      get { return lei_; }
      set { lei_ = value; }
    }


		/// <summary>
		/// 
		/// </summary>
    [ComponentCollectionProperty(TableName = "LegalEntityTag", CollectionType = "bag")]
    public IList<Tag> Tags
    {
      get { return tags_ ?? (tags_ = new List<Tag>()); }
      set { tags_ = value; }
    }

    /// <summary>
    ///   Country of operation
    /// </summary>
    [ManyToOneProperty]
    public Country Country
    {
      get { return (Country)ObjectRef.Resolve(country_); }
      set { country_ = ObjectRef.Create(value); }
    }

    /// <summary>
    ///
    /// </summary>
    public long CountryId
    {
      get { return country_ == null || country_.IsNull ? 0 : country_.Id; }
    }

    /// <summary>
    /// Credit Classification of Entity
    /// </summary>
    [EnumProperty]
    public CreditGrade CreditGrade
    {
      get { return creditGrade_; }
      set { creditGrade_ = value; }
    }

    /// <summary>
    ///   Rating per tier
    /// </summary>
    /// <remarks>
    /// </remarks>
    [ElementCollectionProperty(IndexColumn = "Tier", ElementColumn = "Rating")]
    public IDictionary<Seniority, SP8Rating> CapitalStructureRatings
    {
      get { return capitalStructureRatings_ ?? (capitalStructureRatings_ = new Dictionary<Seniority, SP8Rating>()); }
      set { capitalStructureRatings_ = value; }
    }

    /// <summary>
    ///   Roles played by this entity
    /// </summary>
    [EnumProperty]
    public LegalEntityRoles EntityRoles
    {
      get { return roles_; }
      set { roles_ = value; }
    }

    /// <summary>
    ///   Roles played by this entity
    /// </summary>
    [EnumProperty]
    public RestructuringType DefaultRestructuring
    {
      get { return defaultRestructuring_; }
      set { defaultRestructuring_ = value; }
    }

    /// <summary>
    ///   Date this entity is valid from.
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
    ///   Date this entity is valid to.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Entities may merge or restructure into other entities.</para>
    /// </remarks>
    ///
    [DtProperty]
    public Dt RedValidTo
    {
      get { return _redValidTo; }
      set { _redValidTo = value; }
    }

    /// <summary>
    ///   DTCC Participant ID
    /// </summary>
    [StringProperty(MaxLength = 20)]
    public string DTCCParticipantID { get; set; }

    /// <summary>
    /// The history of rating actions.
    /// </summary>
    [ComponentCollectionProperty(CollectionType = "bag")]
    public IList<RatingItem> RatingActions
    {
      get { return ratingActions_ ?? (ratingActions_ = new List<RatingItem>()); }
      set { ratingActions_ = value; }
    }

    /// <summary>
    /// The Sectors that have been assigned.
    /// </summary>
    [ComponentCollectionProperty(CollectionType = "bag")]
    public IList<SectorItem> Sectors
    {
      get { return sectors_ ?? (sectors_ = new List<SectorItem>()); }
      set { sectors_ = value; }
    }

    /// <summary>
    /// The Currency to use to get the Survival Curve
    /// </summary>
    [EnumProperty(AllowNullValue = false)]
    public Currency CurveCurrency { get; set; }

    /// <summary>
    /// The Seniority to use to get the Survival Curve
    /// </summary>
    [EnumProperty(AllowNullValue = false)]
    public Seniority CurveSeniority { get; set; }

    /// <summary>
    /// Parent LegalEntity
    /// </summary>
    [ManyToOneProperty]
    public LegalEntity Parent
    {
      get { return (LegalEntity)ObjectRef.Resolve(parent_); }
      set { parent_ = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long? ParentId
    {
      get { return parent_ == null ? null : (long?)parent_.Id; }
    }

    /// <summary>
    ///   Issuer equity ticker for looking up the borrowing curve
    /// </summary>
    [StringProperty(MaxLength = 20, AllowNullValue = true)]
    public string BorrowingTicker { get; set; }

    /// <summary>
    ///   Issuer equity ticker for looking up the lending curve
    /// </summary>
    [StringProperty(MaxLength = 20, AllowNullValue = true)]
    public string LendingTicker { get; set; }

    #endregion

    #region Methods

		/// <summary>
		///   Does this LegalEntity have the specified roles
		/// </summary>
		///
		/// <param name="roles">LegalEntityRoles to test</param>
    ///
    /// <returns>True if LegalEntity has all the roles specified</returns>
    ///
    public bool HasRole(LegalEntityRoles roles)
    {
      return ((roles & roles_) == roles);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      var clone = (LegalEntity)base.Clone();

      clone.capitalStructureRatings_ = (capitalStructureRatings_ == null) ? null : new Dictionary<Seniority, SP8Rating>(capitalStructureRatings_);

      clone.ratingActions_ = CloneUtil.CloneToGenericList(ratingActions_);
      clone.tags_ = CloneUtil.CloneToGenericList(tags_);
      clone.sectors_ = CloneUtil.CloneToGenericList(sectors_);

      return clone;
    }

    /// <summary>
    ///   Validates that a Legal Entity has Ticker to be in Reference Entity role.
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (this.HasRole(LegalEntityRoles.Reference))
      {
        if (!StringUtil.HasValue(this.Ticker))
        {
          InvalidValue.AddError(errors, this, "Ticker",
            "Ticker is required for a Legal Entity to be in ReferenceEntity role.");
        }
      }

      this.ValidateRatingActions(errors);
    }

    #endregion

    #region IComparable methods

    /// <summary>
    /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>
    /// A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has these meanings:
    /// Value
    /// Meaning
    /// Less than zero
    /// This instance is less than <paramref name="obj"/>.
    /// Zero
    /// This instance is equal to <paramref name="obj"/>.
    /// Greater than zero
    /// This instance is greater than <paramref name="obj"/>.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">
    /// 	<paramref name="obj"/> is not the same type as this instance.
    /// </exception>
    public int CompareTo(object obj)
    {
      var other = obj as LegalEntity;
      if (other != null)
      {
        var ownName = String.IsNullOrEmpty(Ticker) ? Name : Ticker;
        var otherName = String.IsNullOrEmpty(other.Ticker) ? other.Name : other.Ticker;
        return ownName.CompareTo(otherName);
      }
      throw new ArgumentException();
    }

    #endregion

    #region Data

		private string name_;
		private string longName_;
    private ObjectRef country_;
	  private CreditGrade creditGrade_;
		private string ticker_;
		private string clip_;
    private string lei_;
	  private IList<Tag> tags_;
    private IDictionary<Seniority, SP8Rating> capitalStructureRatings_;
    private LegalEntityRoles roles_;
    private RestructuringType defaultRestructuring_;
    private Dt _redValidFrom;
    private Dt _redValidTo;
    private IList<RatingItem> ratingActions_;
    private IList<SectorItem> sectors_;
    private ObjectRef parent_;

    #endregion

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="System.String" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
      return Name;
    }
  }
}