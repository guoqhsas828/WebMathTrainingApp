/*
 * TimeToDefaultOptimizedRng.cs
 *
 *   2008-2011. All rights reserved.
 *
 * $Id$
 *
 */
#define IGNORE_HOURS_AND_MINUTES

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  /// <summary>
  ///   Times to default generator based on advanced sampling techniques.
  /// </summary>
  [Serializable]
  public class TimeToDefaultOptimizedRng : RandomNumberGenerator, ITimeToDefaultRng
  {
    #region Constructors

    internal TimeToDefaultOptimizedRng(
      int numPaths, Dt start, Dt end, CurveSolver[] curves, double[] probabilities, Dt referenceDate,
      double[] refProbabilities, int[] strata, int[] allocations, double[] factors, RandomNumberGenerator rng)
      : base(rng)
    {
      start_ = start;
      end_ = end;
      curves_ = curves;

      // The strata is based on the defaults between the start and the reference dates.
      if (referenceDate <= start || referenceDate >= end)
      {
        referenceDate = end;
        refProbabilities = probabilities;
      }
      rng_ = new DefaultProbabilityGenerator(numPaths, strata, allocations, refProbabilities, factors,
                                             Internal_GetCoreGenerator());

      // other variaable
      // The thresholds of default within the whole period
      thresholds_ = probabilities;
      weight_ = 0;
      numDefaults_ = 0;
      dates_ = new Dt[probabilities.Length];
      indices_ = new int[probabilities.Length];
      withSort_ = true;

      return;
    }

    /// <summary>
    ///   Construct an optimized sampler
    /// </summary>
    /// <param name="numPaths">
    ///   Total number of paths to draw
    /// </param>
    /// <param name="start">
    ///   Start date of the period
    /// </param>
    /// <param name="end">
    ///   End date of the period
    /// </param>
    /// <param name="survivalCurves">
    ///   An array of survival curves by credit names
    /// </param>
    /// <param name="altSurvivalCurves">
    ///   List of alternative sets of survival curves by credit names, for sensitivity calculations.
    ///   The curves in each set should be of the same number and in the same order as the base curves.
    /// </param>
    /// <param name="referenceDate">
    ///   The reference date to determine the strata, which are based on
    ///   the number of defaults between the start and the reference dates.
    ///   if <c>referenceDate</c> is empty, then the end date is used.
    /// </param>
    /// <param name="strata">
    ///   <para>An array of default numbers to determine default scenarios.
    ///   The <i>i</i>th stratum is the scenario in which there are more than
    ///   <c>strata[i-1]</c> defaults but no more than <c>strata[i]</c>
    ///   defaults.</para>
    /// 
    ///   <para>Conceptually <c>strata[-1]</c> is always -1.  Therefore
    ///   <c>strata[0] = 0</c> means that the first scenario is the case of
    ///   no default.</para>
    /// </param>
    /// <param name="allocations">
    ///   <para>An array of suggested allocations of the paths to strata.
    ///    If <c>allocations[i]</c> is greater than 0, then it is the 
    ///    number of paths to draw from the stratum <c>i</c>.  Otherwise,
    ///    the paths of stratum <c>i</c> is determined by the simulator
    ///    in proportion to the probability of the scenario.
    ///   </para>
    /// 
    ///   <para>For example, if a user want to draw only one path for the 
    ///   case of no default and allocate the remaining paths to other cases
    ///   base on probabilities, then he may set <c>allocations[0]</c> to one
    ///   and leaves all the other elements to zero.</para>
    /// </param>
    /// <param name="factors">
    ///   Array of correlation factors
    /// </param>
    /// <param name="rng">
    ///   The underlying random number generator used to generate the uniform variates.
    /// </param>
    public TimeToDefaultOptimizedRng(
      int numPaths, Dt start, Dt end, SurvivalCurve[] survivalCurves, List<SurvivalCurve[]> altSurvivalCurves,
      Dt referenceDate, int[] strata, int[] allocations, double[] factors, RandomNumberGenerator rng)
      : this(
        numPaths, start, end, GetCurveSolvers(start, end, survivalCurves), GetProbabilities(start, end, survivalCurves),
        referenceDate, referenceDate <= start ? null : GetProbabilities(start, referenceDate, survivalCurves), strata,
        allocations, factors, rng)
    {
      if (altSurvivalCurves != null && altSurvivalCurves.Count > 0)
      {
        altCurves_ = new List<CurveSolver[]>();
        altThresholds_ = new List<double[]>();
        foreach (SurvivalCurve[] sc in altSurvivalCurves)
          if (sc != null)
          {
            if (sc.Length != survivalCurves.Length)
              throw new ArgumentException(
                String.Format("Lengths of survival curves [{0}] and alternative curves [{1}] not match",
                              survivalCurves.Length, sc.Length));
            altCurves_.Add(ArrayUtil.Generate(sc.Length,
                                              delegate(int i)
                                              {
                                                return sc[i] == survivalCurves[i]
                                                         ? curves_[i]
                                                         : new CurveSolver(sc[i], start, end);
                                              }));
            altThresholds_.Add(Array.ConvertAll(sc,
                                                delegate(SurvivalCurve s) { return 1.0 - s.Interpolate(start, end); }));
          }
        if (altCurves_.Count == 0)
        {
          altCurves_ = null;
          altThresholds_ = null;
        }
      }

      return;
    }

    /// <summary>
    ///   Construct an optimized sampler
    /// </summary>
    /// <param name="numPaths">
    ///   Total number of paths to draw
    /// </param>
    /// <param name="start">
    ///   Start date of the period
    /// </param>
    /// <param name="end">
    ///   End date of the period
    /// </param>
    /// <param name="survivalCurves">
    ///   An array of survival curves by credit names
    /// </param>
    /// <param name="altSurvivalCurves">
    ///   An array of alternative survival curves by credit names, for sensitivity calculations
    /// </param>
    /// <param name="referenceDate">
    ///   The reference date to determine the strata, which are based on
    ///   the number of defaults between the start and the reference dates.
    ///   if <c>referenceDate</c> is empty, then the end date is used.
    /// </param>
    /// <param name="strata">
    ///   <para>An array of default numbers to determine default scenarios.
    ///   The <i>i</i>th stratum is the scenario in which there are more than
    ///   <c>strata[i-1]</c> defaults but no more than <c>strata[i]</c>
    ///   defaults.</para>
    /// 
    ///   <para>Conceptually <c>strata[-1]</c> is always -1.  Therefore
    ///   <c>strata[0] = 0</c> means that the first scenario is the case of
    ///   no default.</para>
    /// </param>
    /// <param name="allocations">
    ///   <para>An array of suggested allocations of the paths to strata.
    ///    If <c>allocations[i]</c> is greater than 0, then it is the 
    ///    number of paths to draw from the stratum <c>i</c>.  Otherwise,
    ///    the paths of stratum <c>i</c> is determined by the simulator
    ///    in proportion to the probability of the scenario.
    ///   </para>
    /// 
    ///   <para>For example, if a user want to draw only one path for the 
    ///   case of no default and allocate the remaining paths to other cases
    ///   base on probabilities, then he may set <c>allocations[0]</c> to one
    ///   and leaves all the other elements to zero.</para>
    /// </param>
    /// <param name="singleFactor">
    ///   The correlation factor
    /// </param>
    /// <param name="rng">
    ///   The underlying random number generator used to generate the uniform variates.
    /// </param>
    public TimeToDefaultOptimizedRng(
      int numPaths, Dt start, Dt end, SurvivalCurve[] survivalCurves, List<SurvivalCurve[]> altSurvivalCurves,
      Dt referenceDate, int[] strata, int[] allocations, double singleFactor, RandomNumberGenerator rng)
      : this(
        numPaths, start, end, survivalCurves, altSurvivalCurves, referenceDate, strata, allocations,
        ArrayUtil.Generate(survivalCurves.Length, delegate { return singleFactor; }), rng) {}

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Get the name index of the <i>n</i>th default
    /// </summary>
    public int GetDefaultName(int n)
    {
      if (n < numDefaults_) return indices_[n];
      throw new ToolkitException(String.Format("Number of defaults [{0}] less than {1}", numDefaults_, n));
    }

    /// <summary>
    ///   Get the date of the <i>n</i>th default
    /// </summary>
    public Dt GetDefaultDate(int n)
    {
      if (n < numDefaults_) return dates_[n];
      throw new ToolkitException(String.Format("Number of defaults [{0}] less than {1}", numDefaults_, n));
    }

    /// <summary>
    ///   Draw a path and return the number of defaults
    /// </summary>
    /// <returns>
    ///   Number of defaults in the path, or -1 if no more paths available.
    /// </returns>
    public int Draw()
    {
      // clear base path
      basePath_ = null;

      // compute default times
      weight_ = 0;
      double[] u = rng_.Draw(out stratum_);
      if (u == null) return -1;

      weight_ = u[0];
      int lastIndex = numDefaults_ = GenerateDefaults(start_, end_, u, thresholds_, curves_, indices_, dates_);

      // sort the defaults by dates
      if (lastIndex > 0 && withSort_) Array.Sort(dates_, indices_, 0, lastIndex);

      return lastIndex;
    }

    /// <summary>
    ///   Get the simulated path generated by the base curves
    /// </summary>
    /// <returns>A path of the times to default by names</returns>
    public TimeToDefaultInfo GetBasePath()
    {
      if (basePath_ == null && numDefaults_ >= 0)
      {
        basePath_ = numDefaults_ <= 0
                      ? new TimeToDefaultInfo(stratum_, weight_, numDefaults_, null, null, true)
                      : new TimeToDefaultInfo(stratum_, weight_, numDefaults_,
                                              ArrayUtil.Generate(numDefaults_, delegate(int i) { return indices_[i]; }),
                                              ArrayUtil.Generate(numDefaults_, delegate(int i) { return dates_[i]; }),
                                              true);
      }
      return basePath_;
    }

    /// <summary>
    ///   Get the simulated path generated entirely by the alternative curves in a given set
    /// </summary>
    /// <param name="set">The index of the alternative curve set</param>
    /// <returns>A path of the times to default by names</returns>
    public TimeToDefaultInfo GetAlternativePath(int set)
    {
      if (altCurves_ == null) throw new NullReferenceException("No alternative curves specified");
      if (set >= altCurves_.Count) throw new IndexOutOfRangeException("Alternative curves");

      double[] u = rng_.DefaultProbabilities;
      if (u == null) return null;

      CurveSolver[] altCurves = altCurves_[set];
      int[] names = new int[altCurves.Length];
      Dt[] dates = new Dt[altCurves.Length];
      int lastIndex = GenerateDefaults(start_, end_, u, altThresholds_[set], altCurves, names, dates);

      // sort the defaults by dates
      if (lastIndex > 0 && withSort_) Array.Sort(dates, names, 0, lastIndex);

      return lastIndex <= 0
               ? new TimeToDefaultInfo(stratum_, weight_, lastIndex, null, null, false)
               : new TimeToDefaultInfo(stratum_, weight_, lastIndex, names, dates, false);
    }

    /// <summary>
    ///   Get the path generated by replacing the <i>i</i>th curve
    ///   by its alternative in a given alternative set
    ///   while keeping all the others unchanged.
    /// </summary>
    /// <param name="set">The index of the alternative curve set</param>
    /// <param name="ith">The index of the curve in the set to replace</param>
    /// <returns>A path of the times to default by names</returns>
    public TimeToDefaultInfo GetAlternativePath(int set, int ith)
    {
      if (altCurves_ == null || set >= altCurves_.Count)
        throw new ArgumentOutOfRangeException(String.Format("Curve index {0} sould be less than {1}", ith,
                                                            altCurves_.Count));

      CurveSolver[] altCurves = altCurves_[set];
      if (altCurves == null || ith >= altCurves.Length)
        throw new ArgumentOutOfRangeException(String.Format("Curve index {0} sould be less than {1}", ith,
                                                            altCurves.Length));
      double[] altThresholds = altThresholds_[set];

      // The same curve, return the base path
      if (altCurves[ith] == curves_[ith]) return GetBasePath();

      // No draw, return null
      double[] u = rng_.DefaultProbabilities;
      if (u == null) return null;

      // Initialize to be not defaulted before end_
      Dt dt = Dt.Add(end_, 1);
      if (u[ith + 1] >= altThresholds[ith])
      {
        // if the curve is not defaulted in both the base and alternative scenarios,
        // return the base path.
        if (!IsDefaulted(ith)) return GetBasePath();
      }
      else
      {
        double t = altCurves[ith].Solve(1.0 - u[ith + 1]);
        dt = new Dt(start_, t/365.0);
      }

      int count = numDefaults_;
      int[] indices = new int[count + 1];
      Dt[] dates = new Dt[count + 1];
      int j = 0, last = 0;
      for (; j < count; ++j)
      {
        Dt date = dates_[j];
        if (date > dt) break;
        // exclude the name
        int name = indices_[j];
        if (ith != name)
        {
          indices[last] = name;
          dates[last] = date;
          ++last;
        }
      }
      // include the name only when it defaults
      if (dt < end_)
      {
        dates[last] = dt;
        indices[last] = ith;
        ++last;
      }
      for (; j < count; ++j)
      {
        // exclude the name
        int name = indices_[j];
        if (ith != name)
        {
          indices[last] = name;
          dates[last] = dates_[j];
          ++last;
        }
      }
      return new TimeToDefaultInfo(stratum_, weight_, last, indices, dates, false);
    }

    /// <summary>
    ///   Generate defaults based on a draw of default probabilities
    /// </summary>
    /// <param name="start">The start of the period</param>
    /// <param name="maturity">The end of the period</param>
    /// <param name="u">An array of default probabilities</param>
    /// <param name="thresholds">An array of default thresholds</param>
    /// <param name="curves">An array of survival curve solvers</param>
    /// <param name="defaultNames">
    ///   A reference to an array to receive the default names.
    ///   with the length as large as the curves.
    /// </param>
    /// <param name="defaultDates">
    ///   A reference to an array to receive the default dates
    ///   with the length as large as the curves.
    /// </param>
    /// <returns>Number of defaults</returns>
    private static int GenerateDefaults(
      Dt start, Dt maturity, double[] u, double[] thresholds, CurveSolver[] curves, int[] defaultNames,
      Dt[] defaultDates)
    {
      int lastIndex = 0;
      int nBasket = curves.Length;
      for (int i = 0; i < nBasket; ++i)
        if (u[i + 1] < thresholds[i])
        {
          double t = curves[i].Solve(1.0 - u[i + 1]);
          Dt dt = new Dt(start, t/365.0);
          // In rare case, we might have a date after the maturity
          // date, due to the lack of accuracy of Curve::Solve.
          // Here we do the check
#if IGNORE_HOURS_AND_MINUTES
          dt = new Dt(dt.Day, dt.Month, dt.Year);
#endif
          if (dt < maturity)
          {
            defaultDates[lastIndex] = dt;
            defaultNames[lastIndex] = i;
            ++lastIndex;
          }
          // end if
        }

      return lastIndex;
    }

    /// <summary>
    ///    Check if a name is defaulted in the base path
    /// </summary>
    /// <param name="ith">Name index to check</param>
    /// <returns>True if the name defaulted; false otherwise</returns>
    private bool IsDefaulted(int ith)
    {
      if (numDefaults_ == 0) return false;
      else if (ith >= curves_.Length) return false;
      if (defaulted_ == null)
      {
        defaulted_ = new bool[curves_.Length];
        for (int i = 0; i < numDefaults_; ++i) defaulted_[indices_[i]] = true;
      }
      return defaulted_[ith];
    }

    #endregion Methods

    #region Small helpers

    private static CurveSolver[] GetCurveSolvers(Dt start, Dt end, SurvivalCurve[] survivalCurves)
    {
      return Array.ConvertAll(survivalCurves, delegate(SurvivalCurve s) { return new CurveSolver(s, start, end); });
    }

    private static double[] GetProbabilities(Dt start, Dt end, SurvivalCurve[] survivalCurves)
    {
      return Array.ConvertAll(survivalCurves, delegate(SurvivalCurve s) { return 1.0 - s.Interpolate(start, end); });
    }

    #endregion Small helpers

    #region Properties

    /// <summary>
    ///   Number of defaults
    /// </summary>
    public bool SortDefaults { get { return withSort_; } set { withSort_ = value; } }

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
    ///   Survival curve solvers
    /// </summary>
    public CurveSolver[] CurveSolvers { get { return curves_; } }

    internal double[] Thresholds { get { return thresholds_; } }

    #endregion Properties

    #region Data

    private readonly Dt start_, end_;
    private readonly CurveSolver[] curves_;
    private readonly DefaultProbabilityGenerator rng_;
    private readonly double[] thresholds_;
    private int stratum_;
    private double weight_;
    private readonly Dt[] dates_;
    private readonly int[] indices_;
    private int numDefaults_;
    private bool withSort_;

    //- For sensitivity calculations
    private readonly List<CurveSolver[]> altCurves_;
    private readonly List<double[]> altThresholds_;
    private TimeToDefaultInfo basePath_;
    private bool[] defaulted_;

    #endregion Data

    #region Low Level Interface

    [Serializable]
    private class DefaultProbabilityGenerator
    {
      internal DefaultProbabilityGenerator(
        int numPaths, int[] strata, int[] allocations, double[] probabilities, double[] factors, CoreGenerator rng)
      {
        if (numPaths <= 0) throw new ArgumentException("Number of simulated paths must be positive");
        if (probabilities == null || probabilities.Length == 0) throw new ArgumentException("Probabilitity array cannot be empty");

        if (strata == null || strata.Length == 0) strata = new[] {probabilities.Length};
        if (allocations == null || allocations.Length == 0) allocations = new int[strata.Length];
        else if (strata.Length != allocations.Length) throw new ArgumentException("Lengths of strata and allocations must match");

        IntPtr ptr = PInvoke_BasketDefaultOptimizedRng_Create(numPaths, strata.Length, strata, allocations,
                                                              probabilities.Length, probabilities, factors,
                                                              Numerics.CoreGenerator.getCPtr(rng),
                                                              PInvokeException.Receiver);
        Exception ex = PInvokeException.Exception;
        if (ex != null) throw ex;

        cptr_ = new HandleRef(this, ptr);
        result_ = new double[probabilities.Length + 1];
      }

      internal double[] Draw(out int stratum)
      {
        stratum = -1;
        int n = PInvoke_BasketDefaultProbabilityRng_Draw(cptr_, result_, ref stratum, PInvokeException.Receiver);
        Exception ex = PInvokeException.Exception;
        if (ex != null) throw ex;
        return n == 0 ? null : result_;
      }

      internal double[] DefaultProbabilities { get { return result_; } }

      /// <exclude />
      ~DefaultProbabilityGenerator()
      {
        Dispose();
      }

      /// <exclude />
      public virtual void Dispose()
      {
        if (cptr_.Handle != IntPtr.Zero)
        {
          PInvoke_BasketDefaultProbabilityRng_Delete(cptr_);
        }
        cptr_ = new HandleRef(null, IntPtr.Zero);
        GC.SuppressFinalize(this);
      }

      [SuppressUnmanagedCodeSecurity]
      [DllImport("MagnoliaIGNative", EntryPoint = "qn_BasketDefaultStratifiedRng_Create", CallingConvention = CallingConvention.Cdecl)]
      private static extern IntPtr PInvoke_BasketDefaultOptimizedRng_Create(
        [In] int numPaths, [In] int numStrata, [In] int[] strata, [In] int[] allocations, [In] int numNames,
        [In] double[] probabilities, [In] double[] factors, HandleRef uniformRng, StringBuilder exceptMsg);

      [SuppressUnmanagedCodeSecurity]
      [DllImport("MagnoliaIGNative", EntryPoint = "qn_BasketDefaultProbabilityRng_Delete", CallingConvention = CallingConvention.Cdecl)]
      private static extern void PInvoke_BasketDefaultProbabilityRng_Delete(HandleRef sampler);

      [SuppressUnmanagedCodeSecurity]
      [DllImport("MagnoliaIGNative", EntryPoint = "qn_BasketDefaultProbabilityRng_Draw", CallingConvention = CallingConvention.Cdecl)]
      private static extern int PInvoke_BasketDefaultProbabilityRng_Draw(
        HandleRef sampler, [Out] double[] result, ref int stratum, StringBuilder exceptMsg);

      private readonly double[] result_;
      private HandleRef cptr_;
    }

    #endregion Low Level Interface
  }
}
