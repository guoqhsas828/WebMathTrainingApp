/*
 * CopulaRng.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  ///
  /// <summary>
  ///   Generate pseudo-random numbers
  /// </summary>
  ///
  /// <remarks>
  ///   This is a class to generate deviates with multivariate
  ///   distributions according to various copula formulas.
  /// </remarks>
  ///
  /// <example>
  /// <para>The following example demonstrates generating the times to default.</para>
  /// <code language="C#">
  ///   CopulaRng.TimeToDefaultRng rng =
  ///     new CopulaRng.TimeToDefaultRng( startDate,        // start date of the period
  ///                                     endDate,          // end date of the period
  ///                                     survivalCurves,   // array survival curves
  ///                                     copula,           // copula type and parameters
  ///                                     correlation,      // correlation data
  ///                                     0,                // use the default seed
  ///                                     null );           // create a new core generator
  ///
  ///   // draw 1000 sample paths
  ///   int nRuns = 1000;
  ///   for( int iRun = 0; iRun &lt; nRun; ++iRun )
  ///   {
  ///     int nDefaults = rng.Draw(); // Draw a path and return the number defaults in this path
  ///
  ///     if( nDefaults &gt; 0 )
  ///     {
  ///        for( int nth = 0; nth &lt; nDefault; ++nth )
  ///        {
  ///          Dt dateWhenNthDefaultOccur = rng.GetDefaultDate( nth );
  ///          int indexOfNthDefault = rng.GetDefaultName( nth );
  ///          // do somthing with these information
  ///        }
  ///        // do more things for this path
  ///     }
  ///   }
  /// </code>
  /// </example>
  ///
  [Serializable]
  public abstract class CopulaRng : RandomNumberGenerator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (CopulaRng));

    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    internal CopulaRng(RandomNumberGenerator rng) : base(rng) {}

    /// <summary>
    ///   Create generator according to copula
    /// </summary>
    public static CopulaRng CreateGenerator(int dim, Copula copula, Correlation correlation)
    {
      // To be added
      return null;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CopulaRng obj = (CopulaRng) base.Clone();
      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Draw a vector of variates
    /// </summary>
    public abstract void Draw(double[] x);

    /// <summary>
    ///   Transform a level into cumulative probability
    /// </summary>
    /// <remarks>
    ///   Given a level <formula inline="true">x</formula>,
    ///   the cumulative probability is defined as 
    ///   <formula inline="true">p = P[X \leq x]</formula>,
    ///   where <formula inline="true">X</formula> is a random
    ///   variable with the distribution given by the copula.
    /// </remarks>
    /// <param name="x">the level</param>
    /// <returns>the cumulative probability</returns>
    public virtual double ProbabilityFromLevel(double x)
    {
      return x; // Assuming [0,1] uniform distribution by default, will be overloaded
    }

    /// <summary>
    ///   Transform a cumulative probability into the corresponding level
    /// </summary>
    /// <remarks>
    ///   Given a probability <formula inline="true">p</formula>,
    ///   this function find a level defined by
    ///   <formula inline="true">x \equiv \min\{z: P[X \leq z] \geq p \}</formula>,
    ///   where <formula inline="true">X</formula> is a random
    ///   variable with the distribution given by the copula.
    /// </remarks>
    /// <param name="p">cumulative probability</param>
    /// <returns>the level</returns>
    public virtual double LevelFromProbability(double p)
    {
      return p; // Assuming [0,1] uniform distribution by default, will be overloaded
    }

    #endregion Methods

    #region Properties

    #endregion Properties

    #region TimeToDefaultRng_Implementation

    /// <summary>
    ///   Time to default generator based on copulas
    /// </summary>
    [Serializable]
    public class TimeToDefaultRng : RandomNumberGenerator, ITimeToDefaultRng
    {
      #region Constructors

      /// <summary>
      ///   Constructor
      /// </summary>
      ///
      /// <param name="start">Start date of the period</param>
      /// <param name="end">End date of the period</param>
      /// <param name="survCurves">Array of survival curves</param>
      /// <param name="copula">Copula</param>
      /// <param name="correlation">Correlation data</param>
      /// <param name="seed">Seed (0 to use the default value</param>
      /// <param name="rng">Core random number generator (null to create a new one)</param>
      public TimeToDefaultRng(
        Dt start, Dt end, SurvivalCurve[] survCurves, Copula copula, Correlation correlation, uint seed,
        RandomNumberGenerator rng) : base(rng)
      {
        this.start_ = start;
        this.end_ = end;
        this.curveSolvers_ = GetCurveSolvers(start, end, survCurves);
        this.survCurves_ = survCurves;

        int nNames = survCurves.Length;
        this.u_ = new double[nNames];
        this.thresholds_ = new double[nNames];
        this.dates_ = new Dt[nNames];
        this.indices_ = new int[nNames];
        this.numDefaults_ = 0;

        this.use_antithetic_ = true;
        this.antithetic_ = false;

        this.rng_ = CreateCopulaRng(copula, correlation, seed);
      }

      #endregion // Constructors

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
      /// <returns>Number of defaults in the path</returns>
      public int Draw()
      {
        CopulaRng rng = rng_;

        // draw default probabilities
        if (antithetic_)
        {
          int N = u_.Length;
          for (int i = 0; i < N; ++i) u_[i] = -u_[i];
          antithetic_ = false;
        }
        else
        {
          rng.Draw(u_);
          if (use_antithetic_) antithetic_ = true;
        }

        // compute default times
        Dt start = this.start_;
        int lastIndex = 0;
        double[] u = this.u_;
        double[] thresholds = this.thresholds_;
        CurveSolver[] curveSolvers = this.curveSolvers_;
        Dt maturity = this.end_;
        int nBasket = curveSolvers.Length;
        for (int i = 0; i < nBasket; ++i)
        {
          if (u[i] < thresholds[i])
          {
            CurveSolver s = curveSolvers[i];
            double t = s.Solve(1.0 - rng.ProbabilityFromLevel(u[i]));
            Dt dt = new Dt(start, t/365.0);
            // In rare case, we might have a date after the maturity
            // date, due to the lack of accuracy of Curve::Solve.
            // Here we do the check
            if (dt < maturity)
            {
              dates_[lastIndex] = dt;
              indices_[lastIndex] = i;
              ++lastIndex;
            }
            // end if
          }
        }

        // sort the defaults by dates
        this.numDefaults_ = lastIndex;
        if (lastIndex > 0 && withSort_)
        {
          Array.Sort(dates_, indices_, 0, lastIndex);
        }

        return lastIndex;
      }

      /// <summary>
      ///   Create a copula generator
      /// </summary>
      private CopulaRng CreateCopulaRng(Copula copula, Correlation correlation, uint seed)
      {
        int basketSize = survCurves_.Length;

        CopulaRng rng = null;
        switch (copula.CopulaType)
        {
          case CopulaType.Gauss:
          case CopulaType.ExtendedGauss:
          {
            correlation = CorrelationFactory.CreateGeneralCorrelation(correlation);
            if (basketSize != correlation.BasketSize)
              throw new ToolkitException(String.Format("Size of correlation [{0}] not match basket size [{1}]",
                                                       correlation.BasketSize, basketSize));
            rng = new GaussCopulaRng(this, basketSize, correlation.Correlations);
            this.use_antithetic_ = true;
          }
            break;
          case CopulaType.StudentT:
          {
            int df = copula.DfCommon;
            if (df <= 0) throw new ToolkitException(String.Format("Degree of freedoms [{0}] must be positive", df));
            correlation = CorrelationFactory.CreateGeneralCorrelation(correlation);
            if (basketSize != correlation.BasketSize)
              throw new ToolkitException(String.Format("Size of correlation [{0}] not match basket size [{1}]",
                                                       correlation.BasketSize, basketSize));
            rng = new StudentTCopulaRng(this, basketSize, correlation.Correlations, df);
            this.use_antithetic_ = true;
          }
            break;
          case CopulaType.Clayton:
          {
            double corr = (basketSize <= 1 ? correlation.Correlations[0] : correlation.Correlations[1]);
            if (corr < 0) throw new ToolkitException(String.Format("tau ({0}) must be non-negative in Clayton copula.", corr));
            rng = new ClaytonCopulaRng(this, basketSize, corr);
            use_antithetic_ = false;
          }
            break;
          case CopulaType.Frank:
          {
            double corr = (basketSize <= 1 ? correlation.Correlations[0] : correlation.Correlations[1]);
            if (corr < 0) throw new ToolkitException(String.Format("tau ({0}) must be non-negative in Frank copula.", corr));
            rng = new FrankCopulaRng(this, basketSize, corr);
            use_antithetic_ = false;
          }
            break;
          case CopulaType.Gumbel:
          {
            double corr = (basketSize <= 1 ? correlation.Correlations[0] : correlation.Correlations[1]);
            if (corr < 0) throw new ToolkitException(String.Format("tau ({0}) must be non-negative in Gumbel copula.", corr));
            rng = new GumbelCopulaRng(this, basketSize, corr);
            use_antithetic_ = false;
          }
            break;
          default:
            throw new ToolkitException(String.Format("Copula type {0} not supported in Monte Carlo approach.",
                                                     copula.CopulaType));
        }

        if (seed > 0) rng.Seed = seed;

        // calculate the threshold values
        SurvivalCurve[] survCurves = this.survCurves_;
        Dt start = this.start_;
        Dt end = this.end_;
        for (int i = 0; i < basketSize; ++i)
        {
          SurvivalCurve s = survCurves_[i];
          thresholds_[i] = rng.LevelFromProbability(1.0 - s.Interpolate(start, end));
        }

        return rng;
      }

      /// <summary>
      ///   Create a conditional survival curve
      /// </summary>
      private static SurvivalCurve ConditionalSurvivalCurve(Dt asOf, SurvivalCurve sc0)
      {
        int cmp = Dt.Cmp(asOf, sc0.AsOf);
        if (cmp < 0)
          throw new ToolkitException(String.Format(
            "Basket as-of date [{0}]earlier than survival curve as-of date [{1}]", asOf, sc0.AsOf));
        if (cmp <= 0) return sc0;

        SurvivalCurve sc = new SurvivalCurve(asOf);
        sc.Add(asOf, 1.0);
        double sp0 = sc0.Interpolate(asOf);
        int N = sc0.Count;
        for (int i = 0; i < N; ++i)
        {
          Dt dt = sc0.GetDt(i);
          if (asOf < dt) sc.Add(dt, sc0.GetVal(i)/sp0);
        }

        return sc;
      }

      /// <summary>
      ///   Create an array of survival curves
      /// </summary>
      private static SurvivalCurve[] ConditionalSurvivalCurve(Dt asOf, SurvivalCurve[] sc0)
      {
        int N = sc0.Length;
        SurvivalCurve[] sc = new SurvivalCurve[N];
        for (int i = 0; i < N; ++i) sc[i] = ConditionalSurvivalCurve(asOf, sc0[i]);
        return sc;
      }

      /// <summary>
      ///   Create a vector of Times To Default (in months)
      /// </summary>
      public int[] GetTimesToDefaultNthDraw()
      {
        int day1, month1, year1;
        int day2, month2, year2;
        SurvivalCurve[] survCurves = this.survCurves_;
        SurvivalCurve sc0 = survCurves[0];
        int length = survCurves.GetLength(0);
        int[] T2D_IN_MONTH = new int[length];
        Dt asOfDate = Dt.Empty;
        asOfDate = sc0.AsOf;

        // generate times 2 default
        for (int j = 0; j < T2D_IN_MONTH.Length; j++)
        {
          T2D_IN_MONTH[j] = 1000; // ! 80 years
        }
        int nDefaults = this.Draw(); // Draw a path and return the number defaults in this path
        if (nDefaults > 0)
        {
          for (int nth = 0; nth < nDefaults; ++nth)
          {
            Dt dateWhenNthDefaultOccur = GetDefaultDate(nth);
            int indexOfNthDefault = GetDefaultName(nth);
            day1 = asOfDate.Day;
            month1 = asOfDate.Month;
            year1 = asOfDate.Year;
            day2 = dateWhenNthDefaultOccur.Day;
            month2 = dateWhenNthDefaultOccur.Month;
            year2 = dateWhenNthDefaultOccur.Year;
            T2D_IN_MONTH[indexOfNthDefault] = 12*(year2 - year1) + month2 - month1;
          }
        }
        return T2D_IN_MONTH;
      }

      /// <summary>
      /// A single stratum.
      /// </summary>
      public int Stratum { get { return 0; } }

      /// <summary>
      /// Every path is weighted the same.
      /// </summary>
      public double Weight { get { return 1.0; } }

      #endregion // Methods

      #region Properties

      /// <summary>
      ///   Number of defaults
      /// </summary>
      private bool SortDefaults { get { return withSort_; } set { withSort_ = value; } }

      /// <summary>
      ///   Number of defaults
      /// </summary>
      private int NumberDefaults { get { return numDefaults_; } }

      /// <summary>
      ///   SurvivalCurves
      /// </summary>
      private SurvivalCurve[] SurvCurves { get { return survCurves_; } }

      #endregion // Properties

      #region data

      private readonly CopulaRng rng_;

      private readonly Dt start_;
      private readonly Dt end_;
      private readonly SurvivalCurve[] survCurves_;
      private readonly CurveSolver[] curveSolvers_;
      private readonly double[] u_;
      private readonly double[] thresholds_;
      private readonly Dt[] dates_;
      private readonly int[] indices_;
      private int numDefaults_;

      private bool use_antithetic_;
      private bool withSort_ = true;
      private bool antithetic_;

      #endregion //data

      #region Small helpers

      private static CurveSolver[] GetCurveSolvers(Dt start, Dt end, SurvivalCurve[] survivalCurves)
      {
        return Array.ConvertAll(survivalCurves, delegate(SurvivalCurve s) { return new CurveSolver(s, start, end); });
      }

      #endregion Small helpers
    }


    // class TimeToDefaultrng

    #endregion // TimeToDefaultRng_Implementation

    #region Implementation_of_Various_Copulas

    /// <summary>
    ///   Gauss copula generator
    /// </summary>
    [Serializable]
    private class GaussCopulaRng : CopulaRng
    {
      internal GaussCopulaRng(RandomNumberGenerator rng, int dim, double[] correlation) : base(rng)
      {
        rng_ = new MultiNormalRng(rng, dim, correlation);
      }

      public override object Clone()
      {
        GaussCopulaRng obj = (GaussCopulaRng) base.Clone();
        return obj;
      }

      public override void Draw(double[] x)
      {
        rng_.Draw(x);
      }

      public override double ProbabilityFromLevel(double z)
      {
        return Numerics.Normal.cumulative(z, 0.0, 1.0);
      }

      public override double LevelFromProbability(double p)
      {
        return Numerics.Normal.inverseCumulative(p, 0.0, 1.0);
      }

      private readonly MultiNormalRng rng_;
    }


    /// <summary>
    ///   Student t copula generator
    /// </summary>
    [Serializable]
    private class StudentTCopulaRng : CopulaRng
    {
      internal StudentTCopulaRng(RandomNumberGenerator rng, int dim, double[] correlation, int df) : base(rng)
      {
        rng_ = new MultiNormalRng(rng, dim, correlation);
        df_ = df;
      }

      public override object Clone()
      {
        StudentTCopulaRng obj = (StudentTCopulaRng) base.Clone();
        return obj;
      }

      public override void Draw(double[] x)
      {
        rng_.Draw(x);
        double s = Math.Sqrt(base.ChiSquared(df_)/df_);
        int N = x.Length;
        for (int i = 0; i < N; ++i) x[i] /= s;
      }

      public override double ProbabilityFromLevel(double z)
      {
        return StudentT.cumulative(z, df_);
      }

      public override double LevelFromProbability(double p)
      {
        return StudentT.inverseCumulative(p, df_);
      }

      private readonly MultiNormalRng rng_;
      private readonly int df_;
    }


    /// <summary>
    ///   Clayton copula generator
    /// </summary>
    [Serializable]
    private class ClaytonCopulaRng : CopulaRng
    {
      internal ClaytonCopulaRng(RandomNumberGenerator rng, int dim, double tau) : base(rng)
      {
        alpha_ = (1 - tau)/(2*tau);
      }

      public override object Clone()
      {
        ClaytonCopulaRng obj = (ClaytonCopulaRng) base.Clone();
        return obj;
      }

      public override void Draw(double[] x)
      {
        base.StdUniform(x);
        if (alpha_ < 1000)
        {
          double y = base.Gamma(alpha_, 1.0);
          int N = x.Length;
          for (int i = 0; i < N; ++i)
          {
            double v = - Math.Log(x[i])/y;
            x[i] = Math.Pow(1 + v, - alpha_);
          }
        }
        return;
      }

      private readonly double alpha_;
    }


    // class ClaytonCopulaRng

    /// <summary>
    ///   Frank copula generator
    /// </summary>
    [Serializable]
    private class FrankCopulaRng : CopulaRng
    {
      internal FrankCopulaRng(RandomNumberGenerator rng, int dim, double tau) : base(rng)
      {
        // Note: we use tau = theta / (10 + theta)
        //   as a measure of correlation
        double theta = 10*tau/(1 - tau);
        init(theta);
        return;
      }

      public override object Clone()
      {
        FrankCopulaRng obj = (FrankCopulaRng) base.Clone();
        obj.init(1/A_);
        return obj;
      }

      public override void Draw(double[] x)
      {
        base.StdUniform(x);
        int lastIdx = lastIdx_;
        if (lastIdx > 0)
        {
          double u = base.StdUniform();
          double P = 0;
          int M = lastIdx;
          int y = M;
          for (int i = 0; i < M; ++i)
          {
            P += probs_[i];
            if (u <= P)
            {
              y = 1 + i;
              break;
            }
          }

          int N = x.Length;
          for (int i = 0; i < N; ++i)
          {
            double v = - Math.Log(x[i])/y;
            x[i] = -A_*Math.Log(1 - alpha_*Math.Exp(-v));
          }
        }
        return;
      }

      private void init(double theta)
      {
        if (theta <= Double.Epsilon) throw new ToolkitException("delta cannot be zero or negative");

        A_ = 1/theta;
        alpha_ = 1 - Math.Exp(- theta);

        double alpha = alpha_;
        const int N = 1000;
        probs_ = new double[N];
        lastIdx_ = -1;
        for (int i = 1; i <= N; ++i)
        {
          double pi = Math.Pow(alpha, i)/i/theta;
          probs_[i - 1] = pi;
          if (pi < 1e-6)
          {
            lastIdx_ = i;
            break;
          }
        }
        if (lastIdx_ < 0) lastIdx_ = N;
      }

      private double[] probs_;
      private int lastIdx_;
      private double A_;
      private double alpha_;
    }


    // class FrankCopulaRng

    /// <summary>
    ///   Gumbel copula generator
    /// </summary>
    [Serializable]
    private class GumbelCopulaRng : CopulaRng
    {
      internal GumbelCopulaRng(RandomNumberGenerator rng, int dim, double tau) : base(rng)
      {
        alpha_ = (1 - tau);
      }

      public override object Clone()
      {
        GumbelCopulaRng obj = (GumbelCopulaRng) base.Clone();
        return obj;
      }

      private double levyRng()
      {
        const double abseps = 1.0e-7;

        double alpha = alpha_;
        double u = Math.PI*(base.StdUniform() - 0.5);

        if (Math.Abs(alpha - 1) <= abseps) /* cauchy case */
        {
          double tt = Math.Tan(u);
          return Math.Abs(tt); // we want positive part
        }

        double v = 0;
        do
        {
          v = base.StdExponential();
        } while (v == 0.0);

        if (Math.Abs(alpha - 2) <= abseps) /* gaussian case */
        {
          double tt = 2*Math.Sin(u)*Math.Sqrt(v);
          return Math.Abs(tt); // we want positive part
        }

        /* general case */
        double t = Math.Sin(alpha*u)/Math.Pow(Math.Cos(u), 1/alpha);
        double s = Math.Pow(Math.Cos((alpha - 1)*u)/v, (1 - alpha)/alpha);

        return Math.Abs(t*s); // we want positive part
      }

      public override void Draw(double[] x)
      {
        base.StdUniform(x);
        double y = levyRng();
        double alpha = alpha_;
        int N = x.Length;
        for (int i = 0; i < N; ++i)
        {
          double v = - Math.Log(x[i])/y;
          x[i] = Math.Exp(- Math.Pow(v, alpha));
        }
        return;
      }

      private readonly double alpha_;
    }


    // class GumbelCopulaRng

    #endregion // Implementation_of_Various_Copulas
  }
}
