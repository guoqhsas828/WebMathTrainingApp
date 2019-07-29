/*
 * StochasticRng.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  /// <summary>
  ///  An adapter class to represent the law of motion of a diffusion system.
  ///   Target functions derived from this class must implement evaluate().
  /// </summary>
  ///
  public interface IStochasticFn
  {
    #region Methods

    /// <summary>
    ///   Evaluate a step of a stochastic difference equation
    /// </summary>
    /// 
    /// <param name="x0">Initial states (input)</param>
    /// <param name="t">Current date (input)</param>
    /// <param name="dt">Time interval to evaluate (input)</param>
    /// <param name="dw">Brownian motion move (input)</param>
    /// <param name="x1">Resulting states (output)</param>
    ///
    void evaluate(double[] x0, Dt t, double dt, double[] dw, double[] x1);

    #endregion Methods
  } ;


  /// <summary>
  ///   A class representing the dynamic system: M-Dimensional Vasicek process
  /// </summary>
  /// <formula>
  ///   dp_{1t} = \kappa_{p1}(\theta_p - p_{1t})dt + \sigma_{pt}  dW_{1t}
  ///   dp_{2t} = \kappa_{p2}(\theta_p - p_{2t})dt + \sigma_{pt}  dW_{2t}
  ///   ...............................................................
  ///   dp_{Mt} = \kappa_{pM}(\theta_p - p_{Mt})dt + \sigma_{pt}  dW_{Mt}
  /// </formula>
  public class MultiDimVasicekFn : IStochasticFn
  {
    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    /// 
    public MultiDimVasicekFn(double[] kappaP, double[] thetaP, double[] sigmaP)
    {
      kappaP_ = kappaP;
      thetaP_ = thetaP;
      sigmaP_ = sigmaP;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Evaluate state of multi-dimensional Vasicek at (t+1) given values at time t.
    /// </summary>
    /// 
    /// <param name="x0">Initial states (input)</param>
    /// <param name="t">Current date (input)</param>
    /// <param name="dt">Time interval to evaluate (input)</param>
    /// <param name="dw">Brownian motion move (input)</param>
    /// <param name="x1">Resulting states (output)</param>
    ///
    public void evaluate(double[] x0, Dt t, double dt, double[] dw, double[] x1)
    {
      nextP(x0, t, dt, dw, x1);
    }

    // p_t equation
    private void nextP(double[] ptOld, Dt t, double dt, double[] dw, double[] ptNew)
    {
      for (int i = 0; i < ptNew.Length; ++i)
      {
        ptNew[i] = ptOld[i] + kappaP_[i]*(thetaP_[i] - ptOld[i])*dt + sigmaP_[i]*dw[i];
        if (ptNew[i] <= 0) ptNew[i] = Double.Epsilon;
      }
      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Get access to kappaP param
    /// </summary>
    ///
    ///
    public double[] KappaP { get { return kappaP_; } }

    /// <summary>
    ///   Get access to thetaP param
    /// </summary>
    ///
    ///
    public double[] ThetaP { get { return thetaP_; } }

    /// <summary>
    ///   Get access to sigmaP param
    /// </summary>
    ///
    public double[] SigmaP { get { return sigmaP_; } }

    #endregion Properties

    #region Data

    private readonly double[] kappaP_;
    private readonly double[] thetaP_;
    private readonly double[] sigmaP_;

    #endregion Data
  } ;


  ///
  /// <summary>
  ///   An adapter class to represent the law of motion of a diffusion system.
  ///   Target functions derived from this class must implement evaluate().
  /// </summary>
  ///
  /// <remarks>
  ///   This is a class to generate deviates with multivariate
  ///   normal distributions.
  /// </remarks>
  ///
  [Serializable]
  public class StochasticRng : BaseEntityObject
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (StochasticRng));

    #region Constructors

    /// <summary>
    ///   Default Constructor 
    /// </summary>
    public StochasticRng()
    {
      rng_ = null;
      dw_ = null;
      ;
      deltas_ = null;
      dates_ = null;
      states_ = null;
    }

    /// <summary>
    ///   Constructor with pairwise correlation stored in an array
    ///   dates array and daycount
    /// </summary>
    /// <param name="x0">(multivariate) start vector</param>
    /// <param name="corr">correlation matrix</param>
    /// <param name="dates">dates array</param>
    /// <param name="dc">daycount</param>
    public StochasticRng(double[] x0, double[,] corr, Dt[] dates, DayCount dc)
    {
      if (x0.Length <= 0) throw new ArgumentException("x0", "Must be non-empty.");
      if (corr.GetLength(0) <= 0 || corr.GetLength(0) != corr.GetLength(1))
        throw new ArgumentException(String.Format("correlation ({0}x{1}) matrix not squared", corr.GetLength(0),
                                                  corr.GetLength(1)));

      // find the number of steps
      int N = dates.Length;

      // initialize storage for states
      int dim = x0.Length;
      dw_ = new double[dim];
      states_ = new double[N][];
      for (int i = 0; i < N; ++i) states_[i] = new double[dim];

      // set values of initial state to x0
      states_[0] = x0;

      // fill in dates and deltas
      deltas_[0] = 0;
      Dt date = dates_[0] = dates[0];
      for (int i = 1; i < N; ++i)
      {
        deltas_[i] = Dt.Fraction(date, dates[i], dc);
        date = dates_[i] = dates[i];
      }
      // new random number generator
      rng_ = new MultiNormalRng(corr);
    }

    /// <summary>
    ///   Constructor with pairwise correlation stored in an array
    ///   a daycount convention and a start/end date, stepSize/stepUnit
    ///   (needed to generate a dates array)
    /// </summary>
    /// 
    /// <param name="x0">(multivariate) start vector</param>
    /// <param name="corr">correlation matrix</param>
    /// <param name="start">start date</param>
    /// <param name="stop">stop date</param>
    /// <param name="stepSize">step size</param>
    /// <param name="stepUnit">step unit</param>
    /// <param name="includeDates">include dates array</param>
    /// <param name="dc">daycount</param>
    /// 
    public StochasticRng(
      double[] x0, double[,] corr, Dt start, Dt stop, int stepSize, TimeUnit stepUnit, Dt[] includeDates, DayCount dc)
    {
      if (x0.Length <= 0) throw new ArgumentOutOfRangeException("x0", "Must be non-empty.");
      if (corr.GetLength(0) <= 0 || corr.GetLength(0) != corr.GetLength(1))
        throw new ArgumentOutOfRangeException("corr",
                                              String.Format("correlation ({0}x{1}) matrix not squared",
                                                            corr.GetLength(0), corr.GetLength(1)));
      if (stepSize < 0) throw new ArgumentOutOfRangeException("stepSize", "Must be positive");
      if (start > stop) throw new ArgumentOutOfRangeException("stop", "start date must proceed stop date");

      // find the number of steps
      Dt[] dates = makeDatesArray(start, stop, stepSize, stepUnit, includeDates);

      // find the number of steps
      int N = dates.Length;

      // initialize storage for states
      int dim = x0.Length;
      dw_ = new double[dim];
      states_ = new double[N][];
      for (int i = 0; i < N; ++i) states_[i] = new double[dim];

      // set values of initial state to x0
      states_[0] = x0;

      // fill in dates and deltas
      dates_ = new Dt[N];
      deltas_ = new double[N];

      deltas_[0] = 0;
      Dt date = dates_[0] = dates[0];
      for (int i = 1; i < N; ++i)
      {
        deltas_[i] = Dt.Fraction(date, dates[i], dc);
        date = dates_[i] = dates[i];
      }
      // new random number generator
      rng_ = new MultiNormalRng(corr);
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      StochasticRng obj = (StochasticRng) base.Clone();

      MultiNormalRng rng = (MultiNormalRng) rng_.Clone();

      int N_states = states_.GetLength(0);

      double[][] states = new double[N_states][];
      for (int i = 0; i < N_states; ++i) states[i] = states_[i];

      int N_dw = dw_.GetLength(0);
      double[] dw = new double[N_dw];
      for (int i = 0; i < N_dw; ++i)
      {
        dw[i] = dw_[i];
      }

      int N_deltas = deltas_.GetLength(0);
      double[] deltas = new double[N_deltas];
      for (int i = 0; i < N_deltas; ++i)
      {
        deltas[i] = deltas_[i];
      }

      int N_dates = dates_.GetLength(0);
      Dt[] dates = new Dt[N_dates];
      for (int i = 0; i < N_dates; ++i)
      {
        dates[i] = dates_[i];
      }

      obj.rng_ = rng;
      obj.states_ = states;
      obj.dw_ = dw;
      obj.deltas_ = deltas;
      obj.dates_ = dates;

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///    Make Dates array
    /// </summary>
    /// 
    private static Dt[] makeDatesArray(Dt start, Dt stop, int stepSize, TimeUnit stepUnit, Dt[] includeDates)
    {
      if (start >= stop) throw new ArgumentException("Start date after the stop date");

      int incIdx = 0;
      int nIncludes = includeDates.GetLength(0);
      for (int i = 0; i < nIncludes; ++i)
      {
        if (includeDates[i] > start)
        {
          incIdx = i;
          break;
        }
      }

      ArrayList result = new ArrayList();
      while (incIdx < nIncludes)
      {
        result.Add(start);
        if (stepSize != 0) start = Dt.Add(start, stepSize, stepUnit);
        int cmp = Dt.Cmp(includeDates[incIdx], start);
        while (0 == cmp)
        {
          if (++incIdx >= nIncludes) break;
          cmp = Dt.Cmp(includeDates[incIdx], start);
        }
        if ((cmp < 0) || (cmp > 0 && stepSize == 0)) start = includeDates[incIdx++];

        if (start >= stop)
        {
          result.Add(stop);
          return (Dt[]) result.ToArray(typeof (Dt));
        }
      }

      while (true)
      {
        result.Add(start);
        start = Dt.Add(start, stepSize, stepUnit);
        if (start >= stop)
        {
          result.Add(stop);
          return (Dt[]) result.ToArray(typeof (Dt));
        }
      }
      //return (Dt[])result.ToArray(typeof(Dt));
    }

    /// <summary>
    ///    Simulate path of diffusion process
    /// </summary>
    ///
    public void Simulate(IStochasticFn fn)
    {
      if (null == rng_) throw new InvalidOperationException(String.Format("Diffusion generator is not initialized"));

      MultiNormalRng rng = rng_;
      double[] deltas = deltas_;
      Dt[] dates = dates_;
      int N = dates_.Length;
      int dim = dw_.Length;
      double[] dw = new double[dim];

      for (int i = 0; i < N - 1; ++i)
      {
        double deltaT = deltas[i + 1];

        // draw a vector of dw
        rng.Draw(dw);
        {
          double root = Math.Sqrt(deltaT);
          for (int k = 0; k < dim; ++k) dw[k] *= root;
        }

        // compute the next states
        double[] x0 = states_[i];
        double[] x1 = states_[i + 1];
        Dt date = dates[i + 1]; // date is not used in calculating next step

        fn.evaluate(x0, date, deltaT, dw, x1);
      }
      return;
    }

    /// <summary>
    ///    Get all states 
    /// </summary>
    ///
    public double[][] GetStates()
    {
      if (states_ == null || 0 == dates_.Length) throw new ArgumentException("Diffusion generator is not initialized");

      return states_;
    }

    /// <summary>
    ///    Get State i
    /// </summary>
    ///
    public double[] GetStates(int i)
    {
      if (states_ == null || 0 == dates_.Length) throw new ArgumentException("Diffusion generator is not initialized");
      if (i < 0 || i >= dates_.Length) throw new ArgumentOutOfRangeException("i", String.Format("Invalid index i {0}", i));

      return states_[i];
    }

    /// <summary>
    ///    Get State at time t
    /// </summary>
    ///
    public double[] GetStates(Dt t)
    {
      if (dates_[0] > t) throw new ArgumentOutOfRangeException("t", "must be at or after the start date");

      if (dates_[dates_.Length - 1] < t) throw new ArgumentOutOfRangeException("t", "must be at or before the stop date");

      int N = dates_.Length;
      for (int i = N - 1; i >= 0; --i) if (dates_[i] <= t) return GetStates(i);
      throw new ArgumentException("No state for given Date");
    }

    /// <summary>
    ///    Get DeltaT(i)
    /// </summary>
    ///
    public double GetDeltaT(int i)
    {
      if (states_ == null || 0 == dates_.Length) throw new ArgumentException("Diffusion generator is not initialized");
      if (i < 0 || i >= dates_.Length) throw new ArgumentOutOfRangeException("i", String.Format("Invalid index i (%d)", i));
      return deltas_[i];
    }

    /// <summary>
    ///    Get Date(i)
    /// </summary>
    ///
    public Dt GetDates(int i)
    {
      if (states_ == null || 0 == dates_.Length) throw new ArgumentException("Diffusion generator is not initialized");
      if (i < 0 || i >= dates_.Length) throw new ArgumentOutOfRangeException("i", String.Format("Invalid index i (%d)", i));
      return dates_[i];
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Get access to MultiNormal Rng
    /// </summary>
    ///
    ///
    public MultiNormalRng Rng { get { return rng_; } }

    #endregion Properties

    #region Data

    private MultiNormalRng rng_;
    private double[][] states_;
    private double[] dw_;
    private double[] deltas_;
    private Dt[] dates_;

    #endregion Data
  }
}
