/*
 * TimeToDefaultTwoStagesRng.cs
 *
 *  -2011. All rights reserved.    
 *
 */
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  /// <summary>
  ///   Draw times to default path in two stages: it first draws an initial path;
  ///   then given an initial path, it constructs a continuation sampler to
  ///   draw the times to default in the second stage conditional on
  ///   the initial path.
  /// </summary>
  /// 
  /// <example>
  /// <code language="C#">
  ///   // Use 500 x 100 = 50,000 paths in total
  ///   int numInitialPaths = 500;
  ///   int numContinuePathsPerInitialPath = 100;
  ///   
  ///   // Divide the whole period into two stages:
  ///   //   the first 3 months is the initial period,
  ///   //   all the rest is the continuation period.
  ///   Dt start = settle;
  ///   Dt middle = Dt.Add(settle, "3M");
  ///   Dt end = maturity;
  ///   
  ///   // Create the sampler
  ///   TimeToDefaultTwoStagesRng sampler = new TimeToDefaultTwoStagesRng(
  ///     numInitialPaths,
  ///     numContinuePathsPerInitialPath,
  ///     start, middle, end,
  ///     survivalCurves,
  ///     strata, allocations,
  ///     factors,
  ///     new RandomNumberGenerator(seed));
  ///     
  ///   // Perform two stages sampling
  ///   TimeToDefaultInfo initialPath;
  ///   while ((initialPath = sampler.DrawInitialPath()) != null)
  ///   {
  ///     // For the initial path, construct a continuation sampler
  ///     ITimeToDefaultRng rng = sampler.CreateContinuationRng(initialPath);
  ///       
  ///     // Use continuation sampler to draw full paths
  ///     int totalDefaults;
  ///     while ((totalDefaults = rng.Draw()) &gt;= 0)
  ///     {
  ///       // Note that totalDefaults includes the defaults in the initial path
  ///       for (int i = 0; i &lt; totalDefaults; ++i)
  ///       {
  ///         Dt ithDefaultDate = rng.GetDefaultDate(i);
  ///         int ithDefaultName = rng.GetDefaultName(i);
  ///       }
  ///       // works with this path....
  ///     }
  ///     
  ///     // More works, for example, get the conditional expectation given the initial path
  ///   }
  /// </code>
  /// </example>
  [Serializable]
  public class TimeToDefaultTwoStagesRng : RandomNumberGenerator
  {
    #region ContinuationRng

    /// <summary>
    ///   Generate times to default path conditional on a given initial path
    /// </summary>
    [Serializable]
    public class ContinuationRng : RandomNumberGenerator, ITimeToDefaultRng
    {
      internal ContinuationRng(TimeToDefaultInfo initialPath, int[] map, ITimeToDefaultRng rng)
      {
        rng_ = rng;
        map_ = map;
        initialPath_ = initialPath;
      }

      /// <summary>
      ///   The initial path
      /// </summary>
      public TimeToDefaultInfo InitialPath { get { return initialPath_; } }

      private readonly ITimeToDefaultRng rng_;
      private readonly TimeToDefaultInfo initialPath_;
      private readonly int[] map_;

      #region ITimeToDefaultRng Members

      /// <summary>
      /// Draws the default times that occur in the next path and sets the next path as the current path.
      /// </summary>
      /// 
      /// <returns>The number of defaults that occur.</returns>
      /// 
      public int Draw()
      {
        return (rng_ == null ? initialPath_.NumberDefaults : rng_.Draw());
      }

      /// <summary>
      /// Gets the default date for name n on the current path.
      /// </summary>
      /// 
      /// <param name="n">The index of the default.</param>
      /// 
      /// <returns>The default date</returns>
      /// 
      public Dt GetDefaultDate(int n)
      {
        int start = initialPath_.NumberDefaults;
        return (n < start ? initialPath_.GetDefaultDate(n) : rng_.GetDefaultDate(n - start));
      }

      /// <summary>
      /// Gets the index of the defaulted name in the array of Survival Curves.
      /// </summary>
      /// 
      /// <param name="n">The index of the default.</param>
      /// 
      /// <returns>The index in the survival curve array.</returns>
      /// 
      public int GetDefaultName(int n)
      {
        int start = initialPath_.NumberDefaults;
        return (n < start
                  ? initialPath_.GetDefaultName(n)
                  : (map_ == null ? rng_.GetDefaultName(n - start) : map_[rng_.GetDefaultName(n - start)]));
      }

      /// <summary>
      /// Gets the Stratum that the path was drawn from.
      /// </summary>
      public int Stratum { get { return rng_ == null ? 0 : rng_.Stratum; } }

      /// <summary>
      /// Gets the Weight of the path drawn.
      /// </summary>
      public double Weight { get { return rng_ == null ? 1.0 : rng_.Weight; } }

      #endregion
    } ;

    #endregion ContinuationRng

    #region Constructors

    /// <summary>
    ///   Construct a two stages times to default sampler
    /// </summary>
    /// <param name="numInitialPaths">Sample size of the initial paths</param>
    /// <param name="numContinuePaths">Sample size of the continuation paths per initial path</param>
    /// <param name="start">Start date of the whole period</param>
    /// <param name="middle">End date of the initial period</param>
    /// <param name="end">End date of the whole period</param>
    /// <param name="survivalCurves">Array of survival curves</param>
    /// <param name="strata">Array of strata specifications</param>
    /// <param name="allocations">Array of suggested strata allocations</param>
    /// <param name="factors">Array of correlation factors</param>
    /// <param name="rng">The underlying random number generator</param>
    public TimeToDefaultTwoStagesRng(
      int numInitialPaths, int numContinuePaths, Dt start, Dt middle, Dt end, SurvivalCurve[] survivalCurves,
      int[] strata, int[] allocations, double[] factors, RandomNumberGenerator rng) : base(rng)
    {
      // Sampler for the initial stage
      rng_ = new TimeToDefaultOptimizedRng(numInitialPaths, start, middle, survivalCurves, null, middle, strata,
                                           allocations, factors, rng);
      defaulted_ = new bool[survivalCurves.Length];

      // Prepare common data for the continuation stage
      numContinuePaths_ = numContinuePaths;
      numInitialPaths_ = numInitialPaths;
      curves_ = Array.ConvertAll(survivalCurves, delegate(SurvivalCurve s) { return new CurveSolver(s, middle, end); });
      probabilities_ = Array.ConvertAll(survivalCurves,
                                        delegate(SurvivalCurve s) { return 1.0 - s.Interpolate(middle, end); });
      middle_ = middle;
      end_ = end;
      strata_ = strata;
      allocations_ = allocations;
      factors_ = factors;

      return;
    }

    #endregion Constructors

    #region Data

    private readonly TimeToDefaultOptimizedRng rng_;
    private readonly bool[] defaulted_;

    private readonly Dt middle_, end_;
    private readonly int[] strata_, allocations_;
    private readonly CurveSolver[] curves_;
    private readonly double[] factors_;
    private readonly double[] probabilities_;
    private readonly int numContinuePaths_, numInitialPaths_;

    #endregion Data

    #region Methods

    /// <summary>
    ///   Draw an initial path
    /// </summary>
    /// <returns>Initial path</returns>
    public TimeToDefaultInfo DrawInitialPath()
    {
      rng_.Draw();
      return rng_.GetBasePath();
    }

    /// <summary>
    ///   Create a continuation sampler condtional on an initial path
    /// </summary>
    /// <param name="initialPath">The initial path</param>
    /// <returns>A Continuation sampler</returns>
    public ContinuationRng CreateContinuationRng(TimeToDefaultInfo initialPath)
    {
      int numInitDflts = initialPath.NumberDefaults;
      if (numInitDflts <= 0)
      {
        // no name defaults
        TimeToDefaultOptimizedRng rng = new TimeToDefaultOptimizedRng(numContinuePaths_, middle_, end_, curves_,
                                                                      probabilities_, Dt.Empty, null, strata_,
                                                                      allocations_, factors_, rng_.CoreGenerator);
        return new ContinuationRng(initialPath, null, rng);
      }
      else if (numInitDflts >= defaulted_.Length)
      {
        // all names defaulted
        return new ContinuationRng(initialPath, null, null);
      }

      // prepare data
      for (int i = 0; i < defaulted_.Length; ++i) defaulted_[i] = false;
      for (int j = 0; j < numInitDflts; ++j) defaulted_[initialPath.GetDefaultName(j)] = true;
      CurveSolver[] cs = new CurveSolver[defaulted_.Length - numInitDflts];
      int[] map = new int[defaulted_.Length - numInitDflts];
      double[] p = new double[defaulted_.Length - numInitDflts];
      for (int i = 0, idx = 0; i < defaulted_.Length; ++i)
        if (!defaulted_[i])
        {
          map[idx] = i;
          cs[idx] = curves_[i];
          p[idx] = probabilities_[i];
          ++idx;
        }

      // create rng
      return new ContinuationRng(initialPath, map,
                                 new TimeToDefaultOptimizedRng(numContinuePaths_, middle_, end_, cs, p, Dt.Empty, null,
                                                               strata_, allocations_, factors_, rng_.CoreGenerator));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// The number of paths to draw for the conditional sampling (stage 2).
    /// </summary>
    public int NumberOfContinuationPaths { get { return numContinuePaths_; } }

    /// <summary>
    /// The number of paths to draw for the unconditional sampling (stage 1).
    /// </summary>
    public int NumberOfInitialPaths { get { return numInitialPaths_; } }

    #endregion

#if SAMPLES
    private static void TimeToDefaultTwoStagesRng_Sample(
      Dt settle, Dt maturity,
      SurvivalCurve[] survivalCurves,
      int[] strata, int[] allocations,
      double[] factors, uint seed)
    {
      // Use 500 x 100 = 50,000 paths in total
      int numInitialPaths = 500;
      int numContinuePathsPerInitialPath = 100;

      // Divide the whole period into two stages:
      //   the first 3 months is the initial period,
      //   all the rest is the continuation period.
      Dt start = settle;
      Dt middle = Dt.Add(settle, "3M");
      Dt end = maturity;

      // Create the sampler
      TimeToDefaultTwoStagesRng sampler = new TimeToDefaultTwoStagesRng(
        numInitialPaths,
        numContinuePathsPerInitialPath,
        start, middle, end,
        survivalCurves,
        strata, allocations,
        factors,
        new RandomNumberGenerator(seed));

      // Perform two stages sampling
      TimeToDefaultInfo initialPath;
      while ((initialPath = sampler.DrawInitialPath()) != null)
      {
        // For each initial path, construct a continuation sampler
        ITimeToDefaultRng rng = sampler.CreateContinuationRng(initialPath);

        // Use continuation sampler to draw full paths
        int totalDefaults;
        while ((totalDefaults = rng.Draw()) >= 0)
        {
          // Note that totalDefaults includes the defaults in the initial path
          for (int i = 0; i < totalDefaults; ++i)
          {
            Dt ithDefaultDate = rng.GetDefaultDate(i);
            int ithDefaultName = rng.GetDefaultName(i);
          }
          // works with this path....
        }

        // More works, for example, get the conditional expectation given the initial path
      }

      return;
    }
#endif
    // SAMPLES
  }
}
