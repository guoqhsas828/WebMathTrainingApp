/*
 * Rating.cs
 *
 *   2010. All rights reserved.
 *
 * Created by rsmulktis on 2/4/2010 11:02:37 AM
 *
 */
using System;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// A Rating that can be assigned.
  /// </summary>
  [Serializable]
  public struct Rating
  {
    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    /// 
    /// <param name="scaleId">The ID of the rating scale the rating belongs to</param>
    /// <param name="id">The ID</param>
    /// <param name="name">The name</param>
    public Rating(int scaleId, int id, string name)
    {
      id_ = id;
      name_ = name;
      scale_ = scaleId;
    }
    #endregion

    #region Methods
    /// <summary>
    /// The string representation of the Rating.
    /// </summary>
    /// 
    /// <returns>String</returns>
    /// 
    public override string ToString()
    {
      return name_;
    }

    /// <summary>
    /// The identifier of the Rating.
    /// </summary>
    public int Id
    {
      get { return id_; }
    }

    /// <summary>
    /// The ID of the rating scale the rating belongs to.
    /// </summary>
    public int RatingScaleId
    {
      get { return scale_; }
    }

    /// <summary>
    /// Determines whether the Rating is equal to this Rating.
    /// </summary>
    /// <param name="obj">Rating</param>
    /// <returns>Boolean</returns>
    public override bool Equals(object obj)
    {
      if(!(obj is Rating))
        throw new InvalidOperationException("The given object is not a Rating");
      
      // Implemented this way on the assumption that most rating compares will not be equal but will 
      // be valid (ie from same scale)
      Rating other = (Rating)obj;

      // Check Id first
      if (Id != other.Id)
        return false;

      // Confirm same scale
      return (RatingScaleId == other.RatingScaleId);
    }

    /// <summary>
    /// Gets a hash code for the rating.
    /// </summary>
    /// <returns>Integer</returns>
    public override int GetHashCode()
    {
      return id_.GetHashCode();
    }
    #endregion

    #region Operators
    /// <summary>
    /// Compares 2 ratings.
    /// </summary>
    /// <param name="rating1">First rating</param>
    /// <param name="rating2">Second rating</param>
    /// <returns>Boolean</returns>
    public static bool operator==(Rating rating1, Rating rating2)
    {
      return rating1.Equals(rating2);
    }

    /// <summary>
    /// Compares 2 ratings.
    /// </summary>
    /// <param name="rating1">First rating</param>
    /// <param name="rating2">Second rating</param>
    /// <returns>Boolean</returns>
    public static bool operator !=(Rating rating1, Rating rating2)
    {
      return !rating1.Equals(rating2);
    }

    /// <summary>
    /// Greater than or equal to operator for 2 Ratings.
    /// </summary>
    /// <param name="rating1">Rating 1</param>
    /// <param name="rating2">Rating 2</param>
    /// <returns>Boolean</returns>
    public static bool operator >=(Rating rating1, Rating rating2)
    {
      if (rating1.RatingScaleId != rating2.RatingScaleId)
        throw new InvalidOperationException(String.Format("You cannot compare the Ratings [{0}] and [{1}] because they belong to different RatingScales!", rating1, rating2));
      return rating1.Id <= rating2.Id;
    }

    /// <summary>
    /// Less than or equal to operator for 2 Ratings.
    /// </summary>
    /// <param name="rating1">Rating 1</param>
    /// <param name="rating2">Rating 2</param>
    /// <returns>Boolean</returns>
    public static bool operator <=(Rating rating1, Rating rating2)
    {
      if (rating1.RatingScaleId != rating2.RatingScaleId)
        throw new InvalidOperationException(String.Format("You cannot compare the Ratings [{0}] and [{1}] because they belong to different RatingScales!", rating1, rating2));
      return rating1.Id >= rating2.Id;
    }

    /// <summary>
    /// Greater than operator for 2 Ratings.
    /// </summary>
    /// <param name="rating1">Rating 1</param>
    /// <param name="rating2">Rating 2</param>
    /// <returns>Boolean</returns>
    public static bool operator >(Rating rating1, Rating rating2)
    {
      if (rating1.RatingScaleId != rating2.RatingScaleId)
        throw new InvalidOperationException(String.Format("You cannot compare the Ratings [{0}] and [{1}] because they belong to different RatingScales!", rating1, rating2));
      return rating1.Id < rating2.Id;
    }

    /// <summary>
    /// Less than operator for 2 Ratings.
    /// </summary>
    /// <param name="rating1">Rating 1</param>
    /// <param name="rating2">Rating 2</param>
    /// <returns>Boolean</returns>
    public static bool operator <(Rating rating1, Rating rating2)
    {
      if (rating1.RatingScaleId != rating2.RatingScaleId)
        throw new InvalidOperationException(String.Format("You cannot compare the Ratings [{0}] and [{1}] because they belong to different RatingScales!", rating1, rating2));
      return rating1.Id > rating2.Id;
    }

    #endregion

    #region Data
    private readonly int id_;
    private readonly string name_;
    private readonly int scale_;
    #endregion
  }
}
