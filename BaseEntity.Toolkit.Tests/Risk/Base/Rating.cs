/*
 * Rating.cs
 *
 */

using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// A rating that may be assigned to something.
  /// </summary>
  [Serializable]
  [Component(ChildKey = new[] { "Name" })]
  public class Rating : BaseEntityObject
  {
    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    public Rating()
    {
      Name = "";
      Description = "";
    }
    #endregion

    #region Properties
    /// <summary>
    /// The name of the Rating
    /// </summary>
    [StringProperty(MaxLength = 32)]
    public string Name { get; set; }

    /// <summary>
    /// The description of the Rating.
    /// </summary>
    [StringProperty(MaxLength = 256)]
    public string Description { get; set; }
    #endregion

    #region Clone
    /// <summary>
    /// Clones the rating.
    /// </summary>
    /// 
    /// <returns>Rating</returns>
    /// 
    public override object Clone()
    {
      Rating newRating = new Rating();
      newRating.Name = Name;
      newRating.Description = Description;
      return newRating;
    }
    #endregion

    #region Data
    #endregion
  }
}
