/*
 * VolatilityLookupBucket.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  ///<summary>
  /// Class to store the look-up result on volatility cube
  ///</summary>
  [Serializable]
  public class VolatilityLookupBucket
  {
    ///<summary>
    /// Constructor
    ///</summary>
    ///<param name="start">start index</param>
    ///<param name="end">end index</param>
    ///<param name="found">Bucket found</param>
    ///<param name="exactMatch">Matching exact point</param>
    public VolatilityLookupBucket(int start, int end, bool found, bool exactMatch)
    {
      StartPoint = start;
      EndPoint = end;
      PointFound = found;
      ExactMatchFound = exactMatch;
      IsExtrapolationPoint = false;
    }

    #region Utility Method
    ///<summary>
    /// This method finds out the bucket that contains search target from an array
    ///</summary>
    ///<param name="yvalues">Array of data to be searched</param>
    ///<param name="item">The search target</param>
    ///<typeparam name="T">Type of target data</typeparam>
    ///<returns>The bucket information</returns>
    ///<exception cref="ToolkitException"></exception>
    public static VolatilityLookupBucket FindBucket<T>(T[] yvalues, T item) where T : IComparable<T>
    {
      var last = yvalues.Length - 1;
      for (int i = 0; i < last; i++)
      {
        if (yvalues[i].Equals(item))
        {
          return new VolatilityLookupBucket(i, i, true, true);
        }
        else if (yvalues[i].CompareTo(item) < 0 && yvalues[i + 1].CompareTo(item) > 0)
        {
          return new VolatilityLookupBucket(i, i + 1, true, false);
        }
        else if (yvalues[i + 1].Equals(item))
          return new VolatilityLookupBucket(i + 1, i + 1, true, true);
      }

      if (item.CompareTo(yvalues[0]) < 0)
      {
        return AllowExtrapolation && last > 0
          ? new VolatilityLookupBucket(0, 1, true, false) {IsExtrapolationPoint = true}
          : new VolatilityLookupBucket(0, 0, true, true);
      }
      else if (item.CompareTo(yvalues[last]) >= 0)
      {
        return AllowExtrapolation && last > 0
          ? new VolatilityLookupBucket(last - 1, last, true, false) {IsExtrapolationPoint = true}
          : new VolatilityLookupBucket(last, last, true, true);
      }
      else
        throw new ToolkitException("Error lookup volatility bucket");
    }

    private static bool AllowExtrapolation
    {
      get
      {
        return Util.Configuration.ToolkitConfigurator
          .Settings.SwaptionVolatilityFactory.AllowExtrapolation;
      }
    }

    #endregion
    ///<summary>
    /// Accessor for the start point
    ///</summary>
    public int StartPoint { get; set; }
    ///<summary>
    /// Accessor for the end point
    ///</summary>
    public int EndPoint { get; set; }
    ///<summary>
    /// Indicate whether a point has been found
    ///</summary>
    public bool PointFound { get; set; }
    ///<summary>
    /// Indicate whether the point found is an exact match
    ///</summary>
    public bool ExactMatchFound { get; set; }

    /// <summary>
    /// Indicate whether the point is using extra-polation
    /// </summary>
    public bool IsExtrapolationPoint { get; set; }
  }
}
