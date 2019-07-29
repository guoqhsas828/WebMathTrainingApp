/*
 * TimeToDefaultConditionalRng.cs
 *
 *   2008-2011. All rights reserved.
 *
 */

using System;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  /// <summary>
  /// TimeToDefaultConditionalRng class.
  /// </summary>
  [Serializable]
  public class TimeToDefaultConditionalRng : ITimeToDefaultRng
  {
    #region Data

    //logger
    private static ILog Log = LogManager.GetLogger(typeof (TimeToDefaultConditionalRng));

    // Data
    private readonly TimeToDefaultTwoStagesRng rng_;
    private TimeToDefaultTwoStagesRng.ContinuationRng crng_;
    private int initPaths_;
    private int contPaths_;
    private TimeToDefaultInfo info_;

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public TimeToDefaultConditionalRng(
      int numInitialPaths, int numContinuePaths, Dt start, Dt middle, Dt end, SurvivalCurve[] survivalCurves,
      int[] strata, int[] allocations, double[] factors, RandomNumberGenerator rng)
    {
      // Defaults
      initPaths_ = 0;
      contPaths_ = numContinuePaths; // Set to reset immediately!

      // Setup 2 stage sampler
      rng_ = new TimeToDefaultTwoStagesRng(numInitialPaths, numContinuePaths, start, middle, end, survivalCurves, strata,
                                           allocations, factors, rng);
    }

    #endregion

    #region ITimeToDefaultRng Members

    /// <summary>
    /// Draw a new path.
    /// </summary>
    /// 
    /// <returns>The number of defaults on the path.</returns>
    /// 
    public int Draw()
    {
      if (contPaths_ < rng_.NumberOfContinuationPaths)
      {
        contPaths_++;
        info_ = null;
        return crng_.Draw();
      }
      else
      {
        info_ = rng_.DrawInitialPath();
        crng_ = rng_.CreateContinuationRng(info_);
        contPaths_ = 0;
        initPaths_++;
        return info_.NumberDefaults;
      }
    }

    /// <summary>
    /// The nth default date.
    /// </summary>
    /// 
    /// <param name="n">The index into the array of curves that defaulted.</param>
    /// 
    /// <returns>The default date.</returns>
    /// 
    public Dt GetDefaultDate(int n)
    {
      if (info_ == null) return crng_.GetDefaultDate(n);
      else return info_.GetDefaultDate(n);
    }

    /// <summary>
    /// The nth defaulted curve name.
    /// </summary>
    /// 
    /// <param name="n">The index into the array of curves that defaulted.</param>
    /// 
    /// <returns>The index of the defaulted curve into the array of all survival curves.</returns>
    /// 
    public int GetDefaultName(int n)
    {
      if (info_ == null) return crng_.GetDefaultName(n);
      else return info_.GetDefaultName(n);
    }

    /// <summary>
    /// The Stratum the path belongs to.
    /// </summary>
    public int Stratum
    {
      get
      {
        if (info_ == null) return crng_.Stratum;
        else return info_.Stratum;
      }
    }

    /// <summary>
    /// The Weight of the path.
    /// </summary>
    public double Weight
    {
      get
      {
        if (info_ == null) return crng_.Weight;
        else return info_.Weight;
      }
    }

    #endregion
  }
}
