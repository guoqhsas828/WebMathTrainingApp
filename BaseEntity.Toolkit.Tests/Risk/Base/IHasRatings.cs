using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using System.Collections;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Interface for products that can be rated by a Rating Agency.
  /// </summary>
  public interface IHasRatings
  {
    /// <summary>
    /// The history of all Ratings action to this entity.
    /// </summary>
    IList<RatingItem> RatingActions { get; set; }
  }

  /// <summary>
  /// A extension class to IHasRatings
  /// </summary>
  public static class IHasRatingsExt
  {
    #region Extension Methods
    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="errors"></param>
    public static void ValidateRatingActions(this IHasRatings obj, ArrayList errors)
    {
      var ratingTypes = new HashSet<string>();

      foreach (var item in obj.RatingActions)
      {
        var key = string.Format("{0}-{1}", item.RatingType.Name, item.Date);

        var ratingType = item.RatingType;

        if (ratingType == null)
          InvalidValue.AddError(errors, obj, "RatingActions", "Invalid null RatingType ");
        // Validate Rating Type
        else if (ratingTypes.Contains(key))
          InvalidValue.AddError(errors, obj, "RatingActions", string.Format("Multiple Ratings have been assigned to Rating Type {0} at Date {1}", item.RatingType.Name, item.Date));
        else
          ratingTypes.Add(key);

        // Validate Rating
        if (item.RatingType != null && !IsValidRating(item.Rating, item.RatingType))
          InvalidValue.AddError(errors, obj, "RatingActions", String.Format("The Rating [{0}] is not valid for Rating Type [{1}].", item.Rating, item.RatingType.Name));
      }
    }


    /// <summary>
    /// Determines whether a rating is valid for a RatingType.
    /// </summary>
    /// 
    /// <param name="rating">The rating</param>
    /// <param name="ratingType">The type of rating</param>
    /// 
    /// <returns>Boolean</returns>
    /// 
    public static bool IsValidRating(string rating, RatingType ratingType)
    {
      var matchedRating = from r in ratingType.Ratings where r.Name == rating select r;
      return matchedRating.Any();
    }

    /// <summary>
    /// Gets the Rating for a given RatingType on a date.
    /// </summary>
    /// 
    /// <param name="obj">IHasRatings object</param>
    /// <param name="type">RatingType</param>
    /// <param name="date"></param>
    /// 
    /// <returns>Rating</returns>
    /// 
    public static string RatingOn(this IHasRatings obj, RatingType type, Dt date)
    {
      IList<RatingItem> ratings = obj.RatingActions;
      var ratingBeforeDate = from r in ratings where r.Date <= date && r.RatingType.Name == type.Name orderby r.Date descending select r;
      var effectiveRating = ratingBeforeDate.FirstOrDefault();
      if (effectiveRating != null)
        return effectiveRating.Rating;
      else // not found
        return type.NotRatedRating;
    }

    /// <summary>
    /// Gets the first Rating found for a given RatingType. 
    /// </summary>
    /// 
    /// <param name="obj">IHasRatings object</param>
    /// <param name="type">RatingType</param>
    /// 
    /// <returns>Rating</returns>
    /// 
    public static string CurrentRating(this IHasRatings obj, RatingType type)
    {
      IList<RatingItem> ratings = obj.RatingActions;
      var ratingOfGivenType = from r in ratings where r.RatingType.Name == type.Name orderby r.Date descending select r;
      var lastRating = ratingOfGivenType.FirstOrDefault();
      if (lastRating != null)
        return lastRating.Rating;
      else // not found
        return type.NotRatedRating;
    }

    #endregion Extension Methods

  }
}
