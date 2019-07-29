/*
 * StochasticEquations.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Stochastic equation evaluator.
  /// </summary>
  public delegate void StochasticEquations(
    double t, double[] startX,
    double[] weiner, double dt,
    double[] endX);

  /// <summary>
  ///   Generate and evaluate stochastic paths.
  /// </summary>
  public class StochasticPathGenerator
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="StochasticPathGenerator"/> class.
    /// </summary>
    /// <param name="pathParameters">The path parameters.</param>
    /// <param name="randomShocksGenerator">The random shocks generator.</param>
    public StochasticPathGenerator(
      PathParameters pathParameters,
      Action<double[]> randomShocksGenerator)
    {
      int xdim = pathParameters.InitialX.Length;
      x0_ = new double[xdim];
      x1_ = new double[xdim];
      w_ = new double[pathParameters.RandomDimension];
      parameters_ = pathParameters;
      if (randomShocksGenerator != null)
      {
        randomShocksGenerator_ = randomShocksGenerator;
      }
      else
      {
        var rng = new MersenneTwister();
        randomShocksGenerator_ = (a) => rng.StdNormal(a);
      }
    }

    /// <summary>
    /// Draws the path.
    /// </summary>
    /// <param name="calculateNextStates">The calculate next states.</param>
    /// <param name="stateEvaluator">The state evaluator.</param>
    public void DrawPath(
      StochasticEquations calculateNextStates,
      Func<int, double[], bool> stateEvaluator)
    {
      var drawRandomShocks = randomShocksGenerator_;
      var evaluate = stateEvaluator;
      int xdim = parameters_.InitialX.Length;
      double[] x1 = x1_, x0 = x0_, w = w_;
      Array.Copy(parameters_.InitialX, x1, xdim);
      var delta = parameters_.StepSizes;
      var stepCounts = parameters_.StepCounts;
      // for each tenor
      double t = 0;
      for (int i = 0; i < stepCounts.Length; ++i)
      {
        double dt = delta[i];
        t += dt;
        // for each time step
        int steps = stepCounts[i];
        for (int s = 0; s < steps; ++s)
        {
          drawRandomShocks(w); // generate random shocks
          Array.Copy(x1, x0, xdim); // new initial values
          calculateNextStates(t, x0, w, dt, x1); // step forward
        }
        if(!evaluate(i, x1))
        {
          return;
        }
      }
      return;
    }

    private double[] x0_, x1_, w_;
    private PathParameters parameters_;
    private Action<double[]> randomShocksGenerator_;

    /// <summary>
    /// Path parameters
    /// </summary>
    public class PathParameters
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="randomDimension"></param>
      /// <param name="initX"></param>
      /// <param name="stepSizes"></param>
      /// <param name="stepCounts"></param>
      internal protected PathParameters(
        int randomDimension,
        double[] initX,
        double[] stepSizes,
        int[] stepCounts)
      {
        RandomDimension = randomDimension;
        InitialX = initX;
        StepSizes = stepSizes;
        StepCounts = stepCounts;
      }
      /// <summary>
      ///   The dimension of random shocks.
      /// </summary>
      public readonly int RandomDimension;
      /// <summary>
      ///   The initial values of state variables.
      /// </summary>
      public readonly double[] InitialX;
      /// <summary>
      ///   The array of step sizes.
      /// </summary>
      public readonly double[] StepSizes;
      /// <summary>
      ///   The array of step counts.
      /// </summary>
      public readonly int[] StepCounts;
    } // class PathParameters

    /// <summary>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ReadOnly<T>
    {
      /// <summary>
      /// Constructor
      /// </summary>
      public T Value {get{ return value_;}}
      private T value_;

      /// <summary>
      /// </summary>
      /// <param name="length"></param>
      /// <param name="generate"></param>
      /// <returns></returns>
      public static ReadOnly<T>[] MakeArray(int length, Func<int,T> generate)
      {
        var a = new ReadOnly<T>[length];
        for (int i = 0; i < length; ++i)
        {
          a[i].value_ = generate(i);
        }
        return a;
      }
    }

    /// <summary>
    /// Creates the path parameter.
    /// </summary>
    /// <param name="today">The today.</param>
    /// <param name="tenorDates">The tenor dates.</param>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="initialX">The initial X.</param>
    /// <param name="randomDimesion">The random dimesion.</param>
    /// <returns>An instance of path papermer obejct.</returns>
    public static PathParameters CreatePathParameter(
      Dt today, Dt[] tenorDates, double stepSize,
      double[] initialX, int randomDimesion)
    {
      if(stepSize <= 0){ stepSize = 1.0; }
      List<int> stepCounts = new List<int>();
      List<double> stepSizes = new List<double>();
      double t = 0;
      for(int i = 0; i < tenorDates.Length; ++i)
      {
        double delta = t;
        t = Dt.Diff(today, tenorDates[i])/365.0;
        delta = t - delta;
        int steps = (int) Math.Ceiling(delta/stepSize);
        stepCounts.Add(steps);
        stepSizes.Add(delta/steps);
      }
      return new PathParameters(randomDimesion, initialX,
        stepSizes.ToArray(), stepCounts.ToArray());
    }

    ///<summary>
    /// Try to round the path to be simple years
    ///</summary>
    ///<param name="today"></param>
    ///<param name="tenorDates"></param>
    ///<param name="stepSize"></param>
    ///<param name="initialX"></param>
    ///<param name="randomDimesion"></param>
    ///<returns></returns>
    public static PathParameters CreateSpecialPathParameter(
      Dt today, Dt[] tenorDates, double stepSize,
      double[] initialX, int randomDimesion)
    {
      if (stepSize <= 0) { stepSize = 1.0; }
      List<int> stepCounts = new List<int>();
      List<double> stepSizes = new List<double>();
      double t = 0;
      for (int i = 0; i < tenorDates.Length; ++i)
      {
        double delta = t;
        t = Dt.Diff(today, tenorDates[i]) / 365.0;
        delta = t - delta;
        int steps = (int)Math.Round(delta / stepSize);
        stepCounts.Add(steps);
        stepSizes.Add(delta / steps);
      }
      return new PathParameters(randomDimesion, initialX,
        stepSizes.ToArray(), stepCounts.ToArray());
    }
  }

  /// <summary>
  ///   An implementation of the Mersenne Twister algorithm.
  /// </summary>
  internal class MersenneTwister : BaseEntityObject
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="MersenneTwister"/> class.
    /// </summary>
    public MersenneTwister()
      : this(DefaultSeed)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MersenneTwister"/> class.
    /// </summary>
    /// <param name="seed">The seed.</param>
    public MersenneTwister(UInt32 seed)
    {
      seed = (seed > 0 ? seed : DefaultSeed);
      SetSeed(seed);
    }

    /// <summary>
    /// Return a new object that is a deep copy of this instance
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the lifecycle of the referenced object).
    /// </remarks>
    public override object Clone()
    {
      var o = (MersenneTwister)base.Clone();
      o.mti = this.mti;
      o.mt = CloneUtil.Clone(this.mt);
      return o;
    }

    /// <summary>
    /// Reset the generator with the specified seed
    /// </summary>
    /// <param name="seed">The seed.</param>
    public void SetSeed(UInt32 seed)
    {

      var s = (seed != 0 ? seed : DefaultSeed);
      mt[0] = s & 0xffffffffU;
      for (mti = 1; mti < N; mti++)
      {
        mt[mti] = (uint)
          (1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + mti);
        mt[mti] &= 0xffffffffU;
      }
    }

    /// <summary>
    /// Gets the lower bound on the possible values for GetNextUInt32().
    /// </summary>
    /// <value>The lower bound.</value>
    public UInt32 LowerBound
    {
      get { return UInt32.MinValue; }
    }

    /// <summary>
    /// Gets the upper bound on the possible values for GetNextUInt32().
    /// </summary>
    /// <value>The upper bound.</value>
    public UInt32 UpperBound
    {
      get { return UInt32.MaxValue; }
    }

    /// <summary>
    /// Gets a draw of uniformly distributed integer
    /// between the LowerBound and UpperBound inclusive.
    /// </summary>
    public UInt32 GetNextUInt()
    {

      if (mti >= N)
      { /* generate N words at one time */
        DoTwist();
      }

      UInt32 y = mt[mti++];

      /* Tempering */
      y ^= (y >> 11);
      y ^= (y << 7) & 0x9d2c5680U;
      y ^= (y << 15) & 0xefc60000U;
      y ^= (y >> 18);

      return y;
    }


    /// <summary>
    /// Return a draw from a standard uniform (0,1) distribution.
    /// </summary>
    /// <returns>A number</returns>
    public double GetNextDouble()
    {
      return (((double)GetNextUInt()) + 0.5) / 4294967296.0;
    }

    /// <summary>
    /// Return a draw from a standard uniform (0,1) distribution.
    /// </summary>
    /// <returns>A number</returns>
    public double StdUniform()
    {
      return (((double)GetNextUInt()) + 0.5) / 4294967296.0;
    }

    /// <summary>
    ///   draw a standard uniform (0,1) along with a random bit.
    /// </summary>
    /// <param name="d">will be populated with a uniform (0,1) draw</param>
    /// <param name="bit">will take value true or false with probability 0.5.
    ///   d and bit appear statistically independent.</param>
    public void GetNextDoubleWithBit(ref double d, ref bool bit)
    {
      d = 2 * GetNextDouble() - 1.0;
      bit = (d > 0);
      d = Math.Abs(d);
    }

    private const UInt32 DefaultSeed = 23490324;
    private const int N = 624;
    private const int M = 397;
    private const UInt32 MATRIX_A = 0x9908b0dfU;
    private const UInt32 UPPER_MASK = 0x80000000U;
    private const UInt32 LOWER_MASK = 0x7fffffffU;
    private static readonly UInt32[] mag01 = { 0x0U, MATRIX_A };

    UInt32[] mt = new UInt32[N];
    int mti;

    // This is called once for each N draws.  Consider taking out of line.
    private void DoTwist()
    {

      // temporary hack to avoid linker problems
      int kk;
      UInt32 y;
      /* mag01[x] = x * MATRIX_A  for x=0,1 */


      for (kk = 0; kk < N - M; kk++)
      {
        y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
        mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1UL];
      }
      for (; kk < N - 1; kk++)
      {
        y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
        mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1UL];
      }
      y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
      mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1UL];

      mti = 0;
    }
  } // class MersenneTwisterCoreGenerator

  internal static class CoreGeneratorExtensions
  {
    #region Standard Normal Variates
    /// Return a draw from a standard normal (0,1) distribution.
    public static double StdNormal(this MersenneTwister core)
    {
      const double B = 2.50662827463100;

      // note: there may be a slightly faster way to
      // implement this.  See the authors' paper.
      double v;

      double x = 2 * B * core.StdUniform() - B;
      if (Math.Abs(x) < 1.17741)
      {
        return (x);
      }

      double y = core.StdUniform();

      if (Math.Log(y) < 0.6931472 - .5 * (x * x))
      {
        return (x);
      }

      x = (x > 0) ? .8857913 * (2.506628 - x) : -.8857913 * (2.506628 + x);

      if (Math.Log(1.8857913 - y) < .5718733 - .5 * (x * x))
      {
        return (x);
      }

      // sample in the tail
      do
      {
        v = 2.0 * core.StdUniform() - 1.0;
        x = -1.0 * Math.Log(Math.Abs(v)) * .3989423;
        y = -1.0 * Math.Log(core.StdUniform());
      } while (y + y < x * x);

      return ((v > 0 ? B + x : -1.0 * B - x));
    }
    #endregion

    #region Vector version of generators

    public static void StdUniform(this MersenneTwister rng, double[] x)
    {
      if (x == null || x.Length == 0)
      {
        return;
      }
      for (int i = 0; i < x.Length; ++i)
        x[i] = rng.StdUniform();
    }

    public static void StdNormal(this MersenneTwister rng, double[] x)
    {
      if (x == null || x.Length == 0)
      {
        return;
      }
      for (int i = 0; i < x.Length; ++i)
        x[i] = rng.StdNormal();
    }
    #endregion
  } // class CoreGeneratorExtensions
}
