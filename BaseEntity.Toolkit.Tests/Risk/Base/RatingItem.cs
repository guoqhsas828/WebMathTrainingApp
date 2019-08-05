using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// A rating assigned to something (typically a Product or LegalEntity).
  /// </summary>
  [Component(ChildKey = new[] { "RatingType", "Date" })]
  [Serializable]
  public class RatingItem : BaseEntityObject
  {
    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    public RatingItem()
    {
      Rating = null;
      RatingType = null;
      Date = Dt.Empty;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The rating type.
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public RatingType RatingType
    {
      get { return (RatingType)ObjectRef.Resolve(ratingType_); }
      set { ratingType_ = ObjectRef.Create(value); }
    }

    /// <summary>
    /// The date the rating was assigned.
    /// </summary>
    [DtProperty(AllowNullValue = false)]
    public Dt Date { get; set; }

    /// <summary>
    /// The assigned rating.
    /// </summary>
    [StringProperty(AllowNullValue = false, MaxLength = 10)]
    public string Rating { get; set; }

    #endregion

    #region Methods
    /// <summary>
    /// Validate the object.
    /// </summary>
    /// 
    /// <param name="errors">List of Errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Validate
      if (Date == Dt.Empty)
        InvalidValue.AddError(errors, this, "Date", "The Rating Change date cannot be empty!");
      if (RatingType == null)
        InvalidValue.AddError(errors, this, "RatingType", "A Rating Type must be specified!");
      if (!StringUtil.HasValue(Rating))
        InvalidValue.AddError(errors, this, "CurrentRatings",
                              "A Rating has not been assigned to type " +
                              (RatingType == null ? "[Unspecified]" : RatingType.Name));
    }
    /// <summary>
    /// Clones tthe object.
    /// </summary>
    /// 
    /// <returns>RatingItem</returns>
    /// 
    public override object Clone()
    {
      var clone = new RatingItem();
      clone.Rating = Rating;
      clone.Date = Date;
      clone.RatingType = RatingType;
      return clone;
    }

    #endregion

    #region Data
    private ObjectRef ratingType_;
    #endregion

  }
}
