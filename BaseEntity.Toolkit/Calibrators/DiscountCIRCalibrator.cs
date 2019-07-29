/*
 * DiscountCIRCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Calibrate a discount curve assuming a CIR
  ///   for the spot rate. Calibration is done by running an
  ///   optimizer over pricing of the whole curve.
  /// </summary>
  /// <remarks>
  ///   <para>The DiscountCIRCalibrator constructs a discount curve
  ///   based on a set of products by running an optimizer over the
  ///   pricing of all of the tenors assuming an CIR process for
  ///   the hazard rate.</para>
  ///
  ///   <para>The hazard rate process has a mean-reverting drift with
  ///   volatility proportional to
  ///   the square root of <formula inline = "true">\lambda</formula>.</para>
  ///
  ///   <para>The dynamics of <formula inline = "true">\lambda</formula> is described by:
  ///   <formula>
  ///     d\lambda_t = \kappa_\lambda(\theta - \lambda_t)dt + \sigma_\lambda\sqrt{\lambda_t} dW_{\lambda_t}
  ///   </formula></para>
  ///
  ///   <para>The objective function for fit is
  ///   <formula inline = "true">\sum_{i=1}^n W_i (P_i - M_i)^2</formula>
  ///   where P is the market full price and M is the calculated model price
  ///   and W is the weighting.</para>
  ///
  ///   <para>The parameter set is:</para>
  ///   <list type = "table">
  ///     <listheader><term>Name</term><description>Description</description></listheader>
  ///     <item><term>x0</term><description>Risk free rate at time 0</description></item>
  ///     <item><term>kappa</term><description>mean reversion rate</description></item>
  ///     <item><term>theta</term><description>drift rate</description></item>
  ///     <item><term>sigma</term><description>volatility</description></item>
  ///   </list>
  /// </remarks>
  /// <example>
  ///   <code language = "C#">
  ///   // Calibrate a discount curve to a term structure of zero coupon bond yields assuming
  ///   // a CIR forward rate process.
  ///   //
  ///   Dt asof = Dt.today();                                         // Pricing date of today
  ///   Dt settle = asOf;                                             // Assume settlement today
  ///   string [] tenors = new string[] { "1y", "3y", "5y" };         // Zero coupon Tenors
  ///   double [] ylds = new double[] { 0.03, 0.05, 0.06 };           // Matching bond yields
  ///
  ///   // Create discount calibrator
  ///   DiscountCIRCalibrator calibrator = new DiscountCIRCalibrator(
  ///     asOf,          // Pricing date
  ///     settle );      // Settlement date
  ///   );
  ///
  ///   // Create discount curve
  ///   DiscountCurve discountCurve = new DiscountCurve( calibrator );
  ///
  ///   // Add zero coupon bonds
  ///   for( int i=0; i &lt; dfs.Count; i++ )
  ///   {
  ///     Dt maturity = Dt.addMonth(today, i+1, TimeUnit.Years);
  ///     discountCurve.AddZeroYield(maturity, ylds[i], DayCount.Thirty360, Frequency.SemiAnnual);
  ///   }
  ///
  ///   // Back out implied cir parameters from term structure
  ///   discountCurve.Fit();
  ///
  ///   // Print out the CIR fitted parameters
  ///   Console.WriteLine( "Fitted x0={0}, kappa={1}, theta={2}, sigma={3}",
  ///   discountCurve.Parameters[0], discountCurve.Parameters[1],
  ///   discountCurve.Parameters[2], discountCurve.Parameters[3] );
  ///   </code>
  /// </example>
  public unsafe class DiscountCIRCalibrator : DiscountModelCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (DiscountCIRCalibrator));

    #region Constructors

    /// <summary>
    ///   Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    protected DiscountCIRCalibrator(Dt asOf) : base(asOf)
    {
      // x[0] = x0, x[1] = kappa, x[2] = theta, x[3] = sigma
      Parameters = new double[4] {0.02, 0.5, 0.02, 0.10};
      ToFit = new bool[4] {true, true, true, true};
      UpperBounds = new double[4] {2.0, 1.0, 2.0, 2.0};
      LowerBounds = new double[4] {0.0, -1.0, 0.0, 0.0};
    }

    /// <summary>
    ///   Constructor given as-of and settlement dates
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    /// <param name = "settle">Settlement date</param>
    public DiscountCIRCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
      // x[0] = x0, x[1] = kappa, x[2] = theta, x[3] = sigma
      Parameters = new double[4] {0.02, 0.5, 0.02, 0.10};
      ToFit = new bool[4] {true, true, true, true};
      UpperBounds = new double[4] {2.0, 1.0, 2.0, 2.0};
      LowerBounds = new double[4] {0.0, -1.0, 0.0, 0.0};
    }

    /// <summary>
    ///   Constructor given recovery and discount curves
    /// </summary>
    /// <param name = "asOf">As-of date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "survivalCurve">Survival Curve</param>
    /// <param name = "recoveryCurve">Recovery curve</param>
    public DiscountCIRCalibrator(Dt asOf, Dt settle, SurvivalCurve survivalCurve, RecoveryCurve recoveryCurve)
      : base(asOf, settle, survivalCurve, recoveryCurve)
    {
      // x[0] = x0, x[1] = kappa, x[2] = theta, x[3] = sigma
      Parameters = new double[4] {0.02, 0.5, 0.02, 0.10};
      ToFit = new bool[4] {true, true, true, true};
      UpperBounds = new double[4] {2.0, 1.0, 2.0, 2.0};
      LowerBounds = new double[4] {0.0, -1.0, 0.0, 0.0};
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Objective function for fit called by optimizer.
    /// </summary>
    /// <returns>sum of squares of differences between calculated model price
    /// and target market price for each product.</returns>
    protected override double Evaluate(double* x)
    {
      double diff = 0.0;

      // Update dataset with new arguments
      for (int i = 0; i < DiscountCurve.Count; i++)
      {
        double t = (i + 1)*0.25;
        double df = CIR.P(t, x[0], x[1], x[2], x[3]);
        DiscountCurve.SetVal(i, df);
      }

      // Price each product
      for (int i = 0; i < DiscountCurve.Tenors.Count; i++)
      {
        CurveTenor tenor = DiscountCurve.Tenors[i];
        if (tenor.Weight != 0.0)
        {
          IPricer pricer = GetPricer(DiscountCurve, tenor.Product);
          tenor.ModelPv = pricer.Pv();

          diff += (tenor.ModelPv - tenor.MarketPv)*(tenor.ModelPv - tenor.MarketPv)*tenor.Weight;
        }
      } // foreach

      logger.Debug(String.Format("Evaluating x0={0}, kappa={1}, theta={2}, sigma={3} -> {4}", x[0], x[1], x[2], x[3],
                                 diff));

      return diff;
    }

    /// <summary>
    ///   Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var discountCurve = (DiscountCurve) curve;

      ICashflowPricer pricer = CashflowPricerFactory.PricerForProduct(product);

      pricer.AsOf = AsOf;
      pricer.Settle = Settle;
      pricer.DiscountCurve = discountCurve;
      pricer.ReferenceCurve = discountCurve;
      pricer.SurvivalCurve = SurvivalCurve;
      pricer.StepSize = stepSize_;
      pricer.StepUnit = stepUnit_;
      pricer.RecoveryCurve = RecoveryCurve;

      return pricer;
    }

    #endregion Methods

    #region Properties

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

    #endregion Properties

    #region Data

    private int stepSize_; // Pricing grid step size
    private TimeUnit stepUnit_; // Pricing grid step unit

    #endregion Data
  }
}