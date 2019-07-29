/*
 * SurvivalModelCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Abstract base class for all single survival curve calibrators based on
  ///   factor models. Calibration is done by running an
  ///   optimizer over pricing of the whole curve.
  /// </summary>
  /// <remarks>
  ///   <para>The SurvivalModelCalibrator constructs a survival curve
  ///   based on a set of products by running an optimizer over the
  ///   pricing of all of the tenors assuming a specified process for the
  ///   the hazard rate.</para>
  ///   <para>The objective function for fit is
  ///   <formula inline = "true">\sum_{i=1}^n W_i (P_i - M_i)^2</formula>
  ///   where P is the market full price and M is the calculated model price
  ///   and W is the weighting.</para>
  /// </remarks>
  public abstract unsafe class SurvivalModelCalibrator : SurvivalCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (SurvivalModelCalibrator));

    #region Constructors

    /// <summary>
    /// Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    /// <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    protected SurvivalModelCalibrator(Dt asOf) : base(asOf)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SurvivalModelCalibrator"/> class.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settlement date</param>
    protected SurvivalModelCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
    }

    /// <summary>
    /// Constructor given recovery and discount curves
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="discountCurve">Discount Curve</param>
    protected SurvivalModelCalibrator(Dt asOf, Dt settle, RecoveryCurve recoveryCurve, DiscountCurve discountCurve)
      : base(asOf, settle, recoveryCurve, discountCurve)
    {
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Fit from a specified point assuming all initial test/etc. have been performed.
    /// </summary>
    /// <param name="curve">Survival curve to fit</param>
    /// <param name="fromIdx">Index to start fit from</param>
    /// <remarks>
    /// Objective function for fit is
    /// <formula inline="true">\sum_{i=1}^n (P_i - M_i)^2 \* W_i</formula>
    /// where P is the market full price and M is the calculated model price
    /// and W is the weighting.
    /// </remarks>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var survivalCurve = (SurvivalCurve) curve;

      // Some validation
      if (DiscountCurve == null || DiscountCurve.Count <= 0)
        throw new ToolkitException("Cannot fit survival curve without discount curve set");
      if (Parameters.Length != LowerBounds.Length)
        throw new ToolkitException("Must have same number of parameters and lower bounds for model calibration");
      if (Parameters.Length != UpperBounds.Length)
        throw new ToolkitException("Must have same number of parameters and upper bounds for model calibration");

      // Construct curve tenors if we have not done so.
      if (survivalCurve.Count == 0)
      {
        for (int i = 0; i < 30*4; i++)
        {
          double t = (i + 1)*0.25;
          Dt dt = Dt.Add(AsOf, (int) (t*365.0));
          survivalCurve.Add(dt, 1.0);
        }
      }

      // Initialise tracking of -ve survival probabilities
      NegSPFound = false;

      // Set up optimizer
      var parameters = new double[parameters_.Length];
      var ub = new double[parameters_.Length];
      var lb = new double[parameters_.Length];
      int numFit = 0;
      for (int i = 0; i < parameters_.Length; i++)
      {
        if (ToFit[i])
        {
          parameters[numFit] = parameters_[i];
          ub[numFit] = ub_[i];
          lb[numFit] = lb_[i];
          numFit++;
        }
      }
#if USE_IMSL
			DirectImsl2 opt = new DirectImsl2(numFit);
#else
      var opt = new NelderMeadeSimplex(numFit);
#endif
      opt.setInitialPoint(parameters);
      opt.setToleranceX(1e-4);

      // Bound valid results
      opt.setLowerBounds(lb);
      opt.setUpperBounds(ub);
      opt.setMaxEvaluations(800);
      opt.setMaxIterations(320);

      fn_ = new Double_Vector_Fn(Evaluate);
      objFn_ = new DelegateOptimizerFn(Parameters.Length, fn_, null);
      sc_ = survivalCurve;

      // Fit
      opt.minimize(objFn_);

      // Save results
      double* x = opt.getCurrentSolution();
      for (int i = 0, j = 0; i < Parameters.Length; i++)
      {
        if (ToFit[i])
        {
          Parameters[i] = x[j++];
        }
      }

      // Clear optimizer
      fn_ = null;
      objFn_ = null;
      sc_ = null;

      return;
    }

    ///<summary>
    ///  Objective function for fit called by optimizer.
    ///</summary>
    ///<remarks>
    ///  Specific model calibrators implement this method.
    ///</remarks>
    ///<returns>sum of squares of differences between calculated model price
    ///  and target market price for each product.</returns>
    protected abstract double Evaluate(double* x);

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Parameter set for model
    /// </summary>
    public double[] Parameters
    {
      get { return parameters_; }
      set { parameters_ = value; }
    }

    /// <summary>
    ///   Flags indicating if parameter to be fitted
    /// </summary>
    public bool[] ToFit
    {
      get { return toFit_; }
      set { toFit_ = value; }
    }

    /// <summary>
    ///   Parameter lower bounds
    /// </summary>
    public double[] LowerBounds
    {
      get { return lb_; }
      set { lb_ = value; }
    }

    /// <summary>
    ///   Parameter upper bounds
    /// </summary>
    public double[] UpperBounds
    {
      get { return ub_; }
      set { ub_ = value; }
    }

    /// <summary>
    ///   Survival curve for this fit
    /// </summary>
    protected SurvivalCurve SurvivalCurve
    {
      get { return sc_; }
    }

    #endregion Properties

    #region Data

    private Double_Vector_Fn fn_;
    private double[] lb_; // Model parameter set
    private DelegateOptimizerFn objFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD
    private double[] parameters_; // Model parameter set
    private SurvivalCurve sc_; // Survival curve for this fit
    private bool[] toFit_; // True if fit this model parameter
    private double[] ub_; // Model parameter set

    #endregion Data
  }
}