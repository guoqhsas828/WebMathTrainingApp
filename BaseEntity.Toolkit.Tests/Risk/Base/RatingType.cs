/*
 * RatingType.cs
 *

 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// A type of Rating, issued by a Rating Agency, that may be assigned to something and take one of a 
  /// list of possible values (Ratings).
  /// </summary>
  [Serializable]
	[Entity(EntityId = 135, DisplayName = "Rating Type", AuditPolicy = AuditPolicy.History, Description = "A type of Rating, issued by a Rating Agency, that may be assigned to something and take one of a list of possible values (Ratings).")]
  public class RatingType : AuditedObject
  {
    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    public RatingType()
    {
      Ratings = new List<Rating>();
      NotRatedRating = null;
      Name = "";
      Description = "";
    }

    #endregion

    #region Properties
    /// <summary>
    /// The Ratings that may be assigned.
    /// </summary>
    [ComponentCollectionProperty(
       TableName = "RatingTypeRatings", 
       Clazz = typeof(Rating),
       CollectionType = "list")]
    public IList<Rating> Ratings { get; set; }

    /// <summary>
    /// The Rating for something that was not rated.
    /// </summary>
    [StringProperty(MaxLength = 10)]
    public string NotRatedRating { get; set; }

    /// <summary>
    /// The name of the type.
    /// </summary>
    [StringProperty(MaxLength = 64, IsKey = true)]
    public string Name { get; set; }

    /// <summary>
    /// A description of the type.
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string Description { get; set; }

    /// <summary>
    /// Whether the Rating Type can be applied to Products (debt).
    /// </summary>
    [BooleanProperty]
    public bool IsProductRating { get; set; }

    /// <summary>
    /// Whether the Rating Type can be applied to Legal Entities.
    /// </summary>
    [BooleanProperty]
    public bool IsLegalEntityRating { get; set; }
    #endregion

    #region Methods
    /// <summary>
    /// Validate the Rating Type.
    /// </summary>
    /// 
    /// <param name="errors">Validation errors.</param>
    /// 
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      // I own them so I need to validate them.
      foreach (Rating rating in Ratings)
        rating.Validate(errors);

      // Validate Unique Rating Names
      List<string> ratingNames = new List<string>();
      foreach(Rating rating in Ratings)
      {
        if(ratingNames.Contains(rating.Name))
          InvalidValue.AddError(errors, this, "Ratings", "Only 1 Rating may be named [" + rating.Name + "].");
        else
          ratingNames.Add(rating.Name);
      }

      // Validate NR
      bool isValid = false;
      foreach (Rating rating in Ratings)
      {
        if (rating.Name == NotRatedRating)
        {
          isValid = true;
          break;
        }
      }
      if(!isValid)
        InvalidValue.AddError(errors, this, "NotRatedRating", "Not Rating Rating [" + NotRatedRating + "] is not an available Rating!");

      // Validate that the rating type is applicable to something
      if(!IsProductRating && !IsLegalEntityRating)
        InvalidValue.AddError(errors, this, "IsLegalEntityRating", "The RatingType must be applicable to either Products or LegalEntities");
    }

    /// <summary>
    /// Clone the rating type.
    /// </summary>
    /// 
    /// <returns>RatingType</returns>
    /// 
    public override object Clone()
    {
      RatingType type = new RatingType();

      // Clone fields
      type.Name = Name;
      type.Description = Description;
      type.NotRatedRating = NotRatedRating;
      type.IsLegalEntityRating = IsLegalEntityRating;
      type.IsProductRating = IsProductRating;
      
      // Clone Ratings
      type.Ratings = new List<Rating>();
      for (int i = 0; i < Ratings.Count; i++)
        type.Ratings.Add((Rating) Ratings[i].Clone());

      // Done
      return type;
    }
    #endregion

    #region Data
    #endregion
  }
}
