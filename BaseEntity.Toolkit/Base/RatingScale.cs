/*
 * RatingScale.cs
 *
 *   2008. All rights reserved.
 *
 * Created by rsmulktis on 9/17/2008 11:02:37 AM
 *
 */

using System;
using System.Runtime.InteropServices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Enumeration of the Rating Agencies.
  /// </summary>
  [Serializable]
  public class RatingScale
  {
    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    /// 
    /// <param name="id">The identifier of the Rating Agency.</param>
    /// <param name="name">The name of the Rating Agency.</param>
    /// <param name="notRated">The rating for "Not Rated" (do not include in ratings list).</param>
    /// <param name="ratings">The valid Ratings.</param>
    /// 
    public RatingScale(int id, string name, string notRated, string[] ratings)
    {
      id_ = id;
      name_ = name;
      notRated_ = new Rating(id, -1, notRated);

      // Setup ratings
      if (ratings != null && ratings.Length > 0)
      {
        ratings_ = new Rating[ratings.Length];
        for (int i = 0; i < ratings.Length; i++)
        {
          ratings_[i] = new Rating(id, i, ratings[i]);
        }
      }
      else
        ratings_ = new Rating[] { };
    }
    #endregion

    #region Properties
    /// <summary>
    /// The ID of the Rating Agency.
    /// </summary>
    public int Id
    {
      get { return id_; }
    }
    /// <summary>
    /// The name of the rating agency.
    /// </summary>
    public string Name
    {
      get { return name_; }
    }

    /// <summary>
    /// The ratings that may be assigned.
    /// </summary>
    public Rating[] Ratings
    {
      get { return ratings_; }
    }

    /// <summary>
    /// The rating for something not rated by the agency.
    /// </summary>
    public Rating NotRated
    {
      get { return notRated_; }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Determines whether a rating is valid for the RatingScale.
    /// </summary>
    /// 
    /// <param name="rating">Rating</param>
    /// 
    /// <returns>Boolean</returns>
    /// 
    public bool IsValidOrEmptyRating(string rating)
    {
      // Must be given something
      if (!StringUtil.HasValue(rating))
        return true;

      // Get clean and all lower case 
      string cleanRating = rating.Trim().ToLower();

      // Check Not Rated
      if (cleanRating == NotRated.ToString().ToLower())
        return true;

      // Check in Ratings array
      for (int i = 0; i < ratings_.Length; i++)
        if (cleanRating == ratings_[i].ToString().ToLower())
          return true;

      // Not Found
      return false;
    }

    /// <summary>
    /// Determines which rating is higher.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Assumes both ratings are valid and part of this RatingScale.</para>
    /// <para>Returns 0 if the ratings are equal, 1 if rating1 is higher than rating2 and -1 if rating2 is higher than rating1.</para>
    /// </remarks>
    /// 
    /// <param name="rating1">First rating</param>
    /// <param name="rating2">Second rating</param>
    /// 
    /// <returns>Integer</returns>
    /// 
    public int Compare(Rating rating1, Rating rating2)
    {
      if (rating1.RatingScaleId != rating2.RatingScaleId)
        throw new InvalidOperationException(String.Format("You cannot compare the Ratings [{0}] and [{1}] because they belong to different RatingScales!", rating1, rating2));
      return -rating1.Id.CompareTo(rating2.Id);
    }

    /// <summary>
    /// Parses the Rating name to a Rating.
    /// </summary>
    /// 
    /// <param name="name">The rating name</param>
    /// 
    /// <returns>Rating</returns>
    /// 
    public Rating ParseRating(string name)
    {
      // Must be given something
      if (!StringUtil.HasValue(name))
        return NotRated;

      // Get clean and all lower case 
      string cleanRating = name.Trim().ToLower();

      // Check Not Rated
      if (cleanRating == NotRated.ToString().ToLower())
        return NotRated;

      // Check in Ratings array
      for (int i = 0; i < ratings_.Length; i++)
        if (cleanRating == ratings_[i].ToString().ToLower())
          return ratings_[i];

      // Not Found
      throw new ToolkitException(String.Format("The Rating [{0}] is not valid for Rating Agency [{1}]!", name, name_));
    }

    /// <summary>
    /// Parses a name of a Rating Agency to one of the built-in Rating Agencies.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>There are 4 built in RatingScales:</para>
    /// <list type="bullet">
    /// <item>Empty (Not Specified)</item>
    /// <item>Moody's</item>
    /// <item>S&amp;P</item>
    /// <item>Fitch</item>
    /// </list>
    /// <para>Each of the 3 "non-empty" RatingScales are based on the respective agency's 
    /// long-term corporate obligation scale. When using this function to parse a RatingScale 
    /// name into a RatingScale instance, the following strings are recognized:</para>
    /// <list type="table">
    ///   <listheader>
    ///    <term>RatingScale</term>
    ///     <description>Acceptable Values</description>
    ///   </listheader>
    ///  <item>
    ///     <term>Moody's</term>
    ///     <description>moodys, moody's, moody</description>
    ///   </item>
    ///  <item>
    ///     <term>S&amp;P</term>
    ///     <description>sp, s&amp;p, sandp</description>
    ///  </item>
    ///  <item>
    ///     <term>Fitch</term>
    ///     <description>fitch</description>
    ///  </item>
    /// </list>
    /// <para>Note that case is ignored when parsing and that any string not recognized 
    /// will be parsed as the [Empty] RatingScale.</para>
    /// </remarks>
    /// 
    /// <param name="name">Name</param>
    /// 
    /// <returns>RatingScale</returns>
    /// 
    public static RatingScale Parse(string name)
    {
      // Make sure we are given a name
      if (!StringUtil.HasValue(name))
        return RatingScale.Empty;

      // Find agency
      string cleanName = name.Trim().ToLower().Replace(" ", "");
      switch (cleanName)
      {
        case "moodys":
        case "moody's":
        case "moody":
          return RatingScale.Moodys;
        case "sp":
        case "s&p":
        case "sandp":
          return RatingScale.SP;
        case "fitch":
          return RatingScale.Fitch;
        default:
          return RatingScale.Empty;
      }
    }

    /// <summary>
    /// Gets the RatingScale with the given ID.
    /// </summary>
    /// <param name="id">The id</param>
    /// <returns>RatingScale</returns>
    public static RatingScale FindScale(int id)
    {
      if(id == Moodys.Id)
        return Moodys;
      if(id == SP.Id)
        return SP;
      if(id == Fitch.Id)
        return Fitch;
      return Empty;
    }

    /// <summary>
    /// The string representation of the Rating Agency.
    /// </summary>
    /// 
    /// <returns>String</returns>
    /// 
    public override string ToString()
    {
      return name_;
    }

    /// <summary>
    /// The hash code for the Rating Agency.
    /// </summary>
    /// 
    /// <returns>Integer</returns>
    /// 
    public override int GetHashCode()
    {
      return id_.GetHashCode();
    }

    /// <summary>
    /// Determines whether the RatingScale equals another scale.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
      if (obj == null)
        return false;
      if(!(obj is RatingScale))
        throw new InvalidOperationException("The object is not a RatingScale");
      var scale = obj as RatingScale;
      return id_ == scale.Id;
    }

    #endregion

    #region Data
    private readonly int id_;
    private readonly string name_;
    private readonly Rating notRated_;
    private readonly Rating[] ratings_;
    #endregion

    #region Operators
    /// <summary>
    /// Compares 2 Rating Agencies for equality.
    /// </summary>
    /// 
    /// <param name="agency1">First Rating Agency</param>
    /// <param name="agency2">Second Rating Agency</param>
    /// 
    /// <returns>Boolean</returns>
    /// 
    public static bool operator ==(RatingScale agency1, RatingScale agency2)
    {
      if (null == (object)agency1 && null == (object)agency2)
        return true;
      else if (null == (object)agency1 || null == (object)agency2)
        return false;
      else 
        return agency1.Equals(agency2);
    }

    /// <summary>
    /// Compares 2 Rating Agencies for inequality.
    /// </summary>
    /// 
    /// <param name="agency1">First Rating Agency</param>
    /// <param name="agency2">Second Rating Agency</param>
    /// 
    /// <returns>Boolean</returns>
    /// 
    public static bool operator !=(RatingScale agency1, RatingScale agency2)
    {
      if (null == (object)agency1 && null == (object)agency2)
        return false;
      else if (null == (object)agency1 || null == (object)agency2)
        return true;
      else
        return !agency1.Equals(agency2);
    }

    #endregion

    #region Built-in Rating Agencies
    /// <summary>
    /// Empty or no Rating Agency.
    /// </summary>
    public static RatingScale Empty = new RatingScale(0, "Empty", "NR", null);

    /// <summary>
    /// Moody's
    /// </summary>
    public static RatingScale Moodys = new RatingScale(1, "Moodys", "NR",
                                                         new[]
                                                           {
                                                             "Aaa", "Aa1", "Aa2", "Aa3", "A1", "A2", "A3", "Baa1",
                                                             "Baa2"
                                                             , "Baa3", "Ba1", "Ba2", "Ba3", "B1", "B2", "B3", "Caa1",
                                                             "Caa2", "Caa3", "Ca", "C"
                                                           });

    /// <summary>
    /// Standard and Poor's
    /// </summary>
    public static RatingScale SP = new RatingScale(2, "SP", "NR",
                                                     new[]
                                                       {
                                                         "AAA", "AA+", "AA", "AA-", "A+", "A", "A-", "BBB+", "BBB",
                                                         "BBB-", "BB+", "BB", "BB-", "B+", "B", "B-", "CCC+", "CCC",
                                                         "CCC-", "CC", "C", "SD", "D"
                                                       });

    /// <summary>
    /// Fitch
    /// </summary>
    public static RatingScale Fitch = new RatingScale(3, "Fitch", "NR",
                                                        new[]
                                                          {
                                                            "AAA", "AA+", "AA", "AA-", "A+", "A", "A-", "BBB+", "BBB",
                                                            "BBB-", "BB+", "BB", "BB-", "B+", "B", "B-", "CCC+", "CCC",
                                                            "CCC-", "CC", "C", "SD", "D"
                                                          });
    #endregion
  }
}
