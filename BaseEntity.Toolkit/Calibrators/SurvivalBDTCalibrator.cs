/*
 * SurvivalBDTCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.ComponentModel;
using BaseEntity.Shared;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Calibrate a survival curve using a BDT model of risky rates.
  /// </summary>
  /// <remarks>
  ///   <para>The SurvivalBDT calibrator constructs a survival curve
  ///   based on a set of products by backing out the implied survival
  ///   probability to the maturity of each product in sequency.</para>
  /// </remarks>
  public class SurvivalBDTCalibrator : SurvivalCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (SurvivalBDTCalibrator));

    #region Constructors

    /// <summary>
    /// Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    /// <para>Settlement date defaults to as-of (pricing) date.</para>
    /// </remarks>
    public SurvivalBDTCalibrator(Dt asOf) : base(asOf)
    {
      currentIdx_ = 0;
      volatilityCurve_ = null;
    }

    /// <summary>
    /// Constructor given as-of (pricing) and settlement dates
    /// </summary>
    /// <remarks>
    /// <para>Settlement date defaults to as-of (pricing) date.</para>
    /// </remarks>
    public SurvivalBDTCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
      currentIdx_ = 0;
      volatilityCurve_ = null;
    }

    /// <summary>
    /// Constructor given recovery and discount curves
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    ///  <param name="recoveryCurve">Recovery curve</param>
    /// <param name="discountCurve">Discount Curve</param>
    public SurvivalBDTCalibrator(Dt asOf, Dt settle, RecoveryCurve recoveryCurve, DiscountCurve discountCurve)
      : base(asOf, settle, recoveryCurve, discountCurve)
    {
      currentIdx_ = 0;
      volatilityCurve_ = null;
    }

    /// <summary>
    /// Constructor give recovery, discount and volatility curves
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="discountCurve">Discount Curve</param>
    /// <param name="volatilityCurve">Volatility Curve</param>
    public SurvivalBDTCalibrator(Dt asOf, Dt settle, RecoveryCurve recoveryCurve, DiscountCurve discountCurve,
                                 VolatilityCurve volatilityCurve) : base(asOf, settle, recoveryCurve, discountCurve)
    {
      currentIdx_ = 0;
      volatilityCurve_ = volatilityCurve;
    }

    /// <summary>
    /// Clone object for calibrator
    /// </summary>
    public override object Clone()
    {
      var obj = (SurvivalBDTCalibrator) base.Clone();

      obj.volatilityCurve_ = (volatilityCurve_ != null) ? (VolatilityCurve) volatilityCurve_.Clone() : null;

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Fit from a specified point assuming all initial test/etc. have been performed.
    /// </summary>
    /// <param name="curve">Survival curve to fit</param>
    /// <param name="fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var survivalCurve = (SurvivalCurve) curve;

      // Construct curve tenors if we have not done so.
      if (survivalCurve.Count != survivalCurve.Tenors.Count)
      {
        survivalCurve.Clear();
        foreach (CurveTenor t in survivalCurve.Tenors)
        {
          survivalCurve.Add(t.Maturity, 1.0);
        }
      }

      // Initialise tracking of -ve survival probabilities
      NegSPFound = false;

      // Set up root finder for each tenor point
      // Note we are solving for the absolute survival probability so we can bracket effectively.
      //
      var rf = new Brent();
      rf.setToleranceX(1e-6);
      rf.setToleranceF(1e-6);
      // Bound valid results to positive
      rf.setLowerBounds(1E-10);
      rf.setUpperBounds(1.0);

      fn_ = new Double_Double_Fn(Evaluate);
      solveFn_ = new DelegateSolverFn(fn_, null);
      sc_ = survivalCurve;

      // Fit each security in sequence
      for (currentIdx_ = fromIdx; currentIdx_ < survivalCurve.Count; currentIdx_++)
      {
        CurveTenor tenor = survivalCurve.Tenors[currentIdx_];

        // Set up root finder function
        rf.restart();

        // remember financing spread
        double origSpread = DiscountCurve.Spread;

        // Find survival probability
        logger.Debug(String.Format("Tenor {0}: solving for price {1}", currentIdx_, tenor.MarketPv));

        double res;
        try
        {
          // Set financing spread
          DiscountCurve.Spread = origSpread + tenor.FinSpread;

          // Guess next level.
          double sp = (currentIdx_ > 0) ? survivalCurve.GetVal(currentIdx_ - 1) : 1.0;
          double deltaT = Dt.Fraction((currentIdx_ > 0) ? tenor.Maturity : AsOf, tenor.Maturity, DayCount.Actual360);

          // Solve for survival probability
          res = rf.solve(solveFn_, tenor.MarketPv, sp/(1.0 + .05*deltaT), sp);
        }
        catch (Exception e)
        {
          DiscountCurve.Spread = origSpread;
          throw new ToolkitException(
            String.Format(
              "Unable to fit {0} at tenor {1}. Best we could do is a survival prob {2} with a market price of {3}",
              survivalCurve.Name, tenor.Name, rf.getCurrentSolution(), rf.getCurrentF(), e));
        }
        finally
        {
          // Restore financing spread
          DiscountCurve.Spread = origSpread;
        }

        // Save result
        survivalCurve.SetVal(currentIdx_, res);
        tenor.ModelPv = rf.getCurrentF();
        logger.Debug(String.Format("Tenor {0}: res {1}, diff={2}", tenor.Name, res, tenor.ModelPv - tenor.MarketPv));

        // Adjust if necessary based on -ve forward survival treatment
        if ((currentIdx_ > 0) && (res > survivalCurve.GetVal(currentIdx_ - 1)))
        {
          NegSPFound = true;
          switch (NegSPTreatment)
          {
            case NegSPTreatment.Zero:
              logger.Debug(String.Format("Tenor {0}: adjusting forward hazard rate to 0.", tenor.Name));
              tenor.ModelPv = Evaluate(res);
              break;
            case NegSPTreatment.Adjust:
              // TBD RTD. 21Oct03
              break;
            default:
              break;
          }
        }
      } // for

      fn_ = null;
      solveFn_ = null;
      sc_ = null;

      return;
    }

    //
    // Function for root find evaluation. Wrapper for Evaluate(x) to propagate
    // error messages back through C++.
    // Called by root find for survival probability at current index.
    // pricer and curve validity assured by Fit().
    //
    private double Evaluate(double x, out string exceptDesc)
    {
      double pv = 0.0;
      exceptDesc = null;
      try
      {
        pv = Evaluate(x);
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }
      return pv;
    }

    //
    // Prices current product given survival probability at current time slice
    //
    private double Evaluate(double x)
    {
      double pv = 0.0;

      // Update current parameter
      sc_.SetVal(currentIdx_, x);

      // Price using this parameter
      CurveTenor tenor = sc_.Tenors[currentIdx_];
      IPricer pricer = GetPricer(sc_, tenor.Product);
      pv = pricer.Pv();
      //logger.Debug( String.Format("Evaluating {0}: sp = {1} -> pv = {2}", currentIdx_, x, pv) );

      return pv;
    }

    /// <summary>
    /// Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var survivalCurve = (SurvivalCurve) curve;

      if (product is Bond && ((Bond) product).Callable)
      {
        var pricer = new BondBDTPricer((Bond) product);

        pricer.AsOf = AsOf;
        pricer.Settle = Settle;
        pricer.DiscountCurve = DiscountCurve;
        pricer.SurvivalCurve = survivalCurve;
        pricer.VolatilityCurve = VolatilityCurve;
        pricer.RecoveryCurve = RecoveryCurve;

        return pricer;
      }
      else
      {
        ICashflowPricer pricer = CashflowPricerFactory.PricerForProduct(product);

        pricer.AsOf = AsOf;
        pricer.Settle = Settle;
        pricer.DiscountCurve = DiscountCurve;
        pricer.SurvivalCurve = survivalCurve;
        pricer.StepSize = stepSize_;
        pricer.StepUnit = stepUnit_;
        pricer.RecoveryCurve = RecoveryCurve;

        return pricer;
      }
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Step size for pricing grid
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
    /// Step units for pricing grid
    /// </summary>
    [Category("Base")]
    public TimeUnit StepUnit
    {
      get { return stepUnit_; }
      set { stepUnit_ = value; }
    }

    /// <summary>
    /// Risky Curve used for bdt pricing
    /// </summary>
    public VolatilityCurve VolatilityCurve
    {
      get { return volatilityCurve_; }
      set { volatilityCurve_ = value; }
    }

    #endregion Properties

    #region Data

    // Transient data for calibration
    private int currentIdx_; // Index to current tenor to fit
    [NonSerialized, NoClone] private Double_Double_Fn fn_;
    private SurvivalCurve sc_; // Survival curve for this fit

    [NonSerialized, NoClone] private DelegateSolverFn solveFn_;
    private int stepSize_;
    private TimeUnit stepUnit_;
    private VolatilityCurve volatilityCurve_; // Volatility curve
    // Here because of subtle issues re persistance of unmanaged delegates. RTD

    #endregion Data
  }
}