/*
 * CCCCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Calibrate the Cross-currency credit contingent model.
  /// </summary>
  public unsafe class CCCCalibrator : BaseEntityObject
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (CCCCalibrator));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    public CCCCalibrator()
    {
      tenors_ = new CurveTenorCollection();
    }

    /// <summary>
    ///   Clone object for calibrator
    /// </summary>
    /// <remarks>
    ///   Due to embedded references within the clone object, a
    ///   specialised clone method is required.
    /// </remarks>
    public override object Clone()
    {
      var obj = (CCCCalibrator) base.Clone();

      obj.tenors_ = (CurveTenorCollection) tenors_.Clone();

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Add product (tenor) to calibration
    /// </summary>
    /// <param name = "product">Product to fit</param>
    /// <param name = "fullPrice">Market full price to match</param>
    /// <param name = "weight">Weight to assign to this pricer.</param>
    /// <param name = "coupon">Last coupon if requireq (for floating rate assets)</param>
    /// <param name = "finSpread">Financing spread</param>
    public void Add(IProduct product, double fullPrice, double weight, double coupon, double finSpread)
    {
      var tenor = new CurveTenor(product.Description, product, fullPrice, coupon, finSpread, weight);
      tenors_.Add(tenor);
    }

    /// <summary>
    ///   Fit CCC model
    /// </summary>
    /// <remarks>
    ///   Objective function for fit is
    ///   <formula inline = "true">\sum_{i=1}^n (P_i - M_i)^2 \* W_i</formula>
    ///   where P is the market full price and M is the calculated model price
    ///   and W is the weighting.
    /// </remarks>
    public void Fit()
    {
      // Start timer
      logger.Debug(String.Format("Starting fit for {0}", Name));
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      // Some validation
      int psize = 4 + Tenors.Count + Tenors.Count + 4 + Tenors.Count + Tenors.Count + 2 + Tenors.Count + 4;
      if (Tenors.Count <= 0)
      {
        throw new ArgumentException("Must set up tenors before fit attempted");
      }
      if (Parameters.Length != psize)
      {
        throw new ArgumentException(String.Format("Expected {0} parameters", psize));
      }
      if (LowerBounds.Length != psize)
      {
        throw new ArgumentException(String.Format("Expected {0} lower bounds", psize));
      }
      if (UpperBounds.Length != psize)
      {
        throw new ArgumentException(String.Format("Expected {0} upper bounds", psize));
      }

      // Set up curves for fit
      rTheta_ = new Curve(AsOf);
      rSigma_ = new Curve(AsOf);
      rfTheta_ = new Curve(AsOf);
      rfSigma_ = new Curve(AsOf);
      fxSigma_ = new Curve(AsOf);
      foreach (CurveTenor tenor in Tenors)
      {
        rTheta_.Add(tenor.Maturity, 0.0);
        rSigma_.Add(tenor.Maturity, 0.0);
        rfTheta_.Add(tenor.Maturity, 0.0);
        rfSigma_.Add(tenor.Maturity, 0.0);
        fxSigma_.Add(tenor.Maturity, 0.0);
      }

      // find the number of parameters to fit
      int numFit = 0;
      for (int i = 0; i < Parameters.Length; i++) if (ToFit[i]) ++numFit;

      // Set up values to fit
      var parameters = new double[numFit];
      var ub = new double[numFit];
      var lb = new double[numFit];
      for (int i = 0, idx = 0; i < Parameters.Length; i++)
      {
        if (ToFit[i])
        {
          parameters[idx] = Parameters[i];
          ub[idx] = upperBounds_[i];
          lb[idx] = lowerBounds_[i];
          idx++;
        }
      }

      // Set up optimizer
      //DirectImsl2 opt = new DirectImsl2(numFit);
      var opt = new NelderMeadeSimplex(numFit);
      opt.setInitialPoint(parameters);
      opt.setToleranceX(1e-4);
      opt.setLowerBounds(lb);
      opt.setUpperBounds(ub);
      opt.setMaxEvaluations(8000);
      opt.setMaxIterations(3200);

      fn_ = new Double_Vector_Fn(Evaluate);
      objFn_ = new DelegateOptimizerFn(numFit, fn_, null);

      // Fit
      opt.minimize(objFn_);

      // Save results
      double* x = opt.getCurrentSolution();
      for (int i = 0, j = 0; i < Parameters.Length; i++) if (ToFit[i]) Parameters[i] = x[j++];

      fn_ = null;
      objFn_ = null;

      // save time
      stopwatch.Stop();
      TimeSpan diff = stopwatch.Elapsed;
      calibrationTime = diff.TotalSeconds;
      logger.Debug(String.Format("Completed fit for {0} in {1} seconds", Name, calibrationTime));

      return;
    }

    /// <summary>
    ///   Called by optimizer with list of parameters (survival probabilities
    ///   in our case).
    /// </summary>
    /// <returns>sum of squares of differences between calculated model price
    ///   and target market price for each product.</returns>
    private double Evaluate(double* x)
    {
      double diff = 0.0;

      foreach (CurveTenor tenor in Tenors)
      {
        CCCPricer pricer;

        if (tenor.Product is SwapLeg)
        {
          pricer = new SwapLegCCCPricer((SwapLeg) tenor.Product);
        }
        else if (tenor.Product is Cap)
        {
          pricer = new CapCCCPricer((Cap) tenor.Product);
        }
        else if (tenor.Product is FxOption)
        {
          pricer = new FxOptionCCCPricer((FxOption) tenor.Product);
        }
        else if (tenor.Product is Note)
        {
          pricer = new NoteCCCPricer((Note) tenor.Product);
        }
        else
        {
          throw new ArgumentException(String.Format("Cannot price {0} in CCC model", tenor.Product.Description));
        }

        pricer.AsOf = AsOf;
        pricer.Settle = Settle;
        pricer.StepSize = StepSize;
        pricer.StepUnit = StepUnit;
        pricer.Ccy = Ccy;
        pricer.FxCcy = FxCcy;
        pricer.RTheta = rTheta_;
        pricer.RSigma = rSigma_;
        pricer.RfTheta = rfTheta_;
        pricer.RfSigma = rfSigma_;
        pricer.FxSigma = fxSigma_;
        pricer.Correlation = Correlation;
        FillParameters(pricer, x);

        tenor.ModelPv = pricer.ProductPv();
        diff += (tenor.ModelPv - tenor.MarketPv)*(tenor.ModelPv - tenor.MarketPv)*tenor.Weight;
      }

      logger.Debug(String.Format("Evaluating r0={0}, rKappa={1}, rTheta={2} -> diff {3}", x[0], x[1], x[2], diff));

      return diff;
    }

    // Fill model parameters for pricer
    private void FillParameters(CCCPricer pricer, double* xvar)
    {
      double[] x = Parameters;
      for (int j = 0, idx = 0; j < x.Length; j++) if (ToFit[j]) x[j] = xvar[idx++];

      // Set parameters
      double mean;
      int i = 0;

      pricer.R0 = x[i++];
      pricer.RKappa = x[i++];
      mean = x[i++];
      for (int j = 0; j < rTheta_.Count; j++) rTheta_.SetVal(j, mean + x[i++]);
      mean = x[i++];
      for (int j = 0; j < rSigma_.Count; j++) rSigma_.SetVal(j, mean + x[i++]);
      pricer.Rf0 = x[i++];
      pricer.RfKappa = x[i++];
      mean = x[i++];
      for (int j = 0; j < rfTheta_.Count; j++) rfTheta_.SetVal(j, mean + x[i++]);
      mean = x[i++];
      for (int j = 0; j < rfSigma_.Count; j++) rfSigma_.SetVal(j, mean + x[i++]);
      pricer.Fx0 = x[i++];
      mean = x[i++];
      for (int j = 0; j < fxSigma_.Count; j++) fxSigma_.SetVal(j, mean + x[i++]);
      pricer.L0 = x[i++];
      pricer.LKappa = x[i++];
      pricer.LTheta = x[i++];
      pricer.LSigma = x[i++];
    }

    /// <summary>
    ///   Clear calibration.
    /// </summary>
    public void Clear()
    {
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   As of date
    /// </summary>
    [Category("Base")]
    public Dt AsOf
    {
      get { return asOf_; }
      set
      {
        if (!value.IsValid())
          throw new ArgumentException(String.Format("Invalid as-of date. Must be valid date, not {0}", value));
        asOf_ = value;
      }
    }

    /// <summary>
    ///   Settlement date
    /// </summary>
    [Category("Base")]
    public Dt Settle
    {
      get { return settle_; }
      set
      {
        if (!value.IsValid())
          throw new ArgumentException(String.Format("Invalid settlement date. Must be valid date, not {0}", value));
        settle_ = value;
      }
    }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    [Category("Base")]
    public int StepSize
    {
      get { return stepSize_; }
      set
      {
        if (value < 0) throw new ArgumentException(String.Format("Invalid step size. Must be >= 0, not {0}", value));
        stepSize_ = value;
      }
    }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    [Category("Base")]
    public TimeUnit StepUnit
    {
      get { return stepUnit_; }
      set { stepUnit_ = value; }
    }

    /// <summary>
    ///   Domestic Currency
    /// </summary>
    public Currency Ccy
    {
      get { return ccy_; }
      set { ccy_ = value; }
    }

    /// <summary>
    ///   Foreign Currency
    /// </summary>
    public Currency FxCcy
    {
      get { return fxCcy_; }
      set { fxCcy_ = value; }
    }

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
      get { return lowerBounds_; }
      set { lowerBounds_ = value; }
    }

    /// <summary>
    ///   Parameter upper bounds
    /// </summary>
    public double[] UpperBounds
    {
      get { return upperBounds_; }
      set { upperBounds_ = value; }
    }

    /// <summary>
    ///   Correlation matrix
    /// </summary>
    public double[,] Correlation
    {
      get { return correlations_; }
      set
      {
        if (value.GetLength(0) != 4 || value.GetLength(1) != 4)
        {
          throw new ArgumentException("Correlation matrix must be 4 x 4");
        }
        for (int i = 0; i < 4; i++)
        {
          if (value[i, i] != 1.0)
            throw new ToolkitException(String.Format("Out of range error: correlation with self ({0}) must be 1",
                                                     value[i, i]));
          for (int j = 0; j < i; j++)
          {
            if (value[j, i] != value[i, j])
              throw new ToolkitException("Out of range error: correlation matrix must be symmetric");
            if (Math.Abs(value[j, i]) > 1)
              throw new ToolkitException(String.Format(
                                           "Out of range error: correlation ({0}) must be between +1 and -1",
                                           value[i, j]));
          }
        }
        correlations_ = value;
      }
    }

    /// <summary>
    ///   ArrayList of curve tenor points
    /// </summary>
    [Category("Base")]
    public CurveTenorCollection Tenors
    {
      get { return tenors_; }
    }

    /// <summary>
    ///   Name
    /// </summary>
    [Category("Base")]
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    ///   The time taken to complete the previous calibration
    /// </summary>
    [Category("Base")]
    public double CalibrationTime
    {
      get { return calibrationTime; }
    }

    #endregion Properties

    #region Data

    private Dt asOf_;
    private double calibrationTime;

    private Currency ccy_;
    private double[,] correlations_;
    private Double_Vector_Fn fn_;
    private Currency fxCcy_;
    private Curve fxSigma_;
    private double[] lowerBounds_; // Lower bound for model parameters
    private string name_;
    private DelegateOptimizerFn objFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD

    private double[] parameters_; // Model parameter set
    private Curve rfSigma_;
    private Curve rfTheta_;
    private Curve rSigma_;
    private Curve rTheta_;
    private Dt settle_;
    private int stepSize_;
    private TimeUnit stepUnit_;
    private CurveTenorCollection tenors_;
    private bool[] toFit_; // True if to fit this model parameter
    private double[] upperBounds_; // Upper bound for model parameters

    #endregion Data
  }
}