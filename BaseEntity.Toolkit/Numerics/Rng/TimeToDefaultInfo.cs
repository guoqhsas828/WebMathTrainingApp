/*
 * TimeToDefaultInfo.cs
 *
 *  -2011. All rights reserved.    
 *
 */
using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  /// <summary>
  ///   Times to default information: what names default at what time.
  /// </summary>
  [Serializable]
  public class TimeToDefaultInfo
  {
    #region Internal Constructors and Data

    private TimeToDefaultInfo()
    {
      stratum_ = 0;
      weight_ = 0;
      numDefaults_ = 0;
      names_ = null;
      dates_ = null;
      isBasePath_ = false;
    }

    internal TimeToDefaultInfo(int stratum, double weight, int defaults, int[] names, Dt[] dates, bool isBasePath)
    {
      stratum_ = stratum;
      weight_ = weight;
      numDefaults_ = defaults;
      names_ = names;
      dates_ = dates;
      isBasePath_ = isBasePath;
    }

    private readonly int numDefaults_;
    private readonly int stratum_;
    private readonly double weight_;
    private readonly int[] names_;
    private readonly Dt[] dates_;
    private readonly bool isBasePath_;

    #endregion Internal Constructors and Data

    #region Public Methods and Properties

    /// <summary>
    ///   Get the name index of the <i>n</i>th default
    /// </summary>
    public int GetDefaultName(int n)
    {
      if (n < numDefaults_) return names_[n];
      throw new ArgumentOutOfRangeException(String.Format("Index [{1}] must be less than the number of defaults [{0}]",
                                                          numDefaults_, n));
    }

    /// <summary>
    ///   Get the date of the <i>n</i>th default
    /// </summary>
    public Dt GetDefaultDate(int n)
    {
      if (n < numDefaults_) return dates_[n];
      throw new ArgumentOutOfRangeException(String.Format("Index [{1}] must be less than the number of defaults [{0}]",
                                                          numDefaults_, n));
    }

    /// <summary>
    ///   Number of defaults in the current path
    /// </summary>
    public int NumberDefaults { get { return numDefaults_; } }

    /// <summary>
    ///   The weight of the current path
    /// </summary>
    public double Weight { get { return weight_; } }

    /// <summary>
    ///   The stratum index of the current path
    /// </summary>
    public int Stratum { get { return stratum_; } }

    /// <summary>
    ///   The stratum index of the current path
    /// </summary>
    public bool IsBasePath { get { return isBasePath_; } }

    #endregion Public Methods and Properties
  }
}
