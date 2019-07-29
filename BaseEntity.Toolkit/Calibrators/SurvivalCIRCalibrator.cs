/*
 * SurvivalCIRCalibrator.cs
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
  ///<summary>
  ///  Calibrate a survival curve assuming a CIR process
  ///  for the hazard rate. Calibration is done by running an
  ///  optimizer over pricing of the whole curve.
  ///</summary>
  ///<remarks>
  ///  <para>The SurvivalCIRCalibrator constructs a survival curve
  ///  based on a set of products by running an optimizer over the
  ///  pricing of all of the tenors assuming an CIR process for
  ///  the hazard rate.</para>
  ///
  ///  <para>The hazard rate process has a mean-reverting drift with
  ///  volatility proportional to
  ///  the square root of <formula inline = "true">\lambda</formula>.</para>
  ///
  ///  <para>The dynamic of <formula inline = "true">\lambda</formula> is described by:</para>
  ///  <formula>
  ///    d\lambda_t = \kappa_\lambda(\theta - \lambda_t)dt + \sigma_\lambda\sqrt{\lambda_t} dW_{\lambda_t}
  ///  </formula>
  ///
  ///  <para>The objective function for fit is
  ///  <formula inline = "true">\sum_{i=1}^n W_i (P_i - M_i)^2</formula>
  ///  where P is the market full price and M is the calculated model price
  ///  and W is the weighting.</para>
  ///
  ///  <para>The parameter set is:</para>
  ///  <list type = "table">
  ///    <listheader><term>Name</term><description>Description</description></listheader>
  ///    <item><term><formula inline = "true">\lambda_0</formula></term><description>Default intensity <formula inline = "true">\lambda</formula> at time 0</description></item>
  ///    <item><term><formula inline = "true">\kappa</formula></term><description>Reversion rate of <formula inline = "true">\lambda</formula></description></item>
  ///    <item><term><formula inline = "true">\theta</formula></term><description>Long term mean of <formula inline = "true">\lambda</formula></description></item>
  ///    <item><term><formula inline = "true">\sigma</formula></term><description>Volatility of <formula inline = "true">\lambda</formula></description></item>
  ///  </list>
  /// </remarks>
  /// <example>
  ///  <code language = "C#">
  ///    // Calibrate a survival curve to a term structure of CDS quotes with an CIR hazard
  ///    // rate process.
  ///    //
  ///    Dt asOf = Dt.today();                                   // Pricing date
  ///    Dt settle = asOf;                                       // Settlement date
  ///    double forwardRate = 0.02;                              // Continuous forward rate for simple ir curve
  ///    string[] cdsTenors = new string[] { "1y", "5y", "10y" };// CDS tenors
  ///    int[] cdsQuotes = new int[] { 32, 48, 58 };             // Matching CDS quotes in bp
  ///    double recoveryRate = 0.40;                             // Assumed recovery rate as % of face
  ///
  ///    // Create simple constant forward rate discounting curve
  ///    DiscountCurve discountCurve = new DiscountCurve(asOf, forwardRate);
  ///
  ///    // Create survival calibrator
  ///    SurvivalCIRCalibrator calibrator =
  ///    new SurvivalCIRCalibrator(
  ///      asOf,                      // Pricing date
  ///      settle,                    // Settlement date
  ///      recoveryRate,              // Flat single recovery rate
  ///      discountCurve              // Discount curve for calibration
  ///    );
  ///
  ///    // Create survival curve
  ///    SurvivalCurve survivalCurve = new SurvivalCurve(calibrator);
  ///
  ///    // Add cds
  ///    for( int i=0; i &lt; cdsMaturity.Count; i++ )
  ///    {
  ///      // Add CDS tenor to calibration
  ///      Dt maturity = Dt.cdsMaturity( asOf, cdsTenor[i]);
  ///      survivalCurve.AddCDS(
  ///      maturity,                         // Maturity date of CDS
  ///      cdsQuotes[i]/10000.0,             // CDS quote in %
  ///      DayCount.Actual360,               // Premium daycount
  ///      Frequency.Quarterly,              // Premium payment frequency
  ///      BDConvention.Modified,            // Premium roll convention
  ///      Calendar.NYB                      // Calendar for premium payments
  ///      };
  ///    }
  ///
  ///    // Back out implied survival probabilities from term structure of CDS
  ///    survivalCurve.Fit();
  ///
  ///    // Print out the survival probabilities matching each cds maturity
  ///    for( int i=0; i &lt; cdsMaturity.Count; i++ )
  ///    {
  ///      Dt maturity = Dt.cdsMaturity( asOf, cdsTenor[i]);
  ///      double sp = survivalCurve.SurvivalProb(maturity);
  ///      Console.WriteLine("{0} : {1}", maturity, sp);
  ///    }
  ///
  ///    // Print out the CIR fitted parameters
  ///    Console.WriteLine( "Fitted x0={0}, kappa={1}, theta={2}, sigma={3}",
  ///    survivalCurve.Parameters[0], survivalCurve.Parameters[1],
  ///    survivalCurve.Parameters[2], survivalCurve.Parameters[3] );
  ///  </code>
  ///</example>
  public unsafe class SurvivalCIRCalibrator : SurvivalModelCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (SurvivalCIRCalibrator));

    #region Constructors

    /// <summary>
    /// Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    /// <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    protected SurvivalCIRCalibrator(Dt asOf) : base(asOf)
    {
      // x[0] = x0, x[1] = kappa, x[2] = theta, x[3] = sigma
      Parameters = new double[4] {0.02, 0.5, 0.02, 0.10};
      ToFit = new bool[4] {true, true, true, true};
      UpperBounds = new double[4] {2.0, 1.0, 2.0, 2.0};
      LowerBounds = new double[4] {0.0, -1.0, 0.0, 0.0};
    }

    /// <summary>
    /// Constructor given as-of and settlement dates
    /// </summary>
    /// <remarks>
    /// <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    /// <param name = "settle">Settlement date</param>
    public SurvivalCIRCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
      // x[0] = x0, x[1] = kappa, x[2] = theta, x[3] = sigma
      Parameters = new double[4] {0.02, 0.5, 0.02, 0.10};
      ToFit = new bool[4] {true, true, true, true};
      UpperBounds = new double[4] {2.0, 1.0, 2.0, 2.0};
      LowerBounds = new double[4] {0.0, -1.0, 0.0, 0.0};
    }

    /// <summary>
    /// Constructor given recovery and discount curves
    /// </summary>
    /// <param name = "asOf">As-of date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "recoveryCurve">Recovery curve</param>
    /// <param name = "discountCurve">Discount Curve</param>
    public SurvivalCIRCalibrator(Dt asOf, Dt settle, RecoveryCurve recoveryCurve, DiscountCurve discountCurve)
      : base(asOf, settle, recoveryCurve, discountCurve)
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
    /// Objective function for fit called by optimizer.
    /// </summary>
    /// <returns>sum of squares of differences between calculated model price
    ///   and target market price for each product.</returns>
    protected override double Evaluate(double* x)
    {
      double diff = 0.0;

      // Update dataset with new arguments
      for (int i = 0; i < SurvivalCurve.Count; i++)
      {
        double t = (i + 1)*0.25;
        double sp = CIR.P(t, x[0], x[1], x[2], x[3]);
        SurvivalCurve.SetVal(i, sp);
      }

      // Price each product
      for (int i = 0; i < SurvivalCurve.Tenors.Count; i++)
      {
        CurveTenor tenor = SurvivalCurve.Tenors[i];
        if (tenor.Weight != 0.0)
        {
          IPricer pricer = GetPricer(SurvivalCurve, tenor.Product, x);
          tenor.ModelPv = pricer.Pv();

          diff += (tenor.ModelPv - tenor.MarketPv)*(tenor.ModelPv - tenor.MarketPv)*tenor.Weight;
        }
      } // foreach

      logger.Debug(String.Format("Evaluating x0={0}, kappa={1}, theta={2}, sigma={3} -> {4}", x[0], x[1], x[2], x[3],
                                 diff));

      return diff;
    }

    /// <summary>
    /// Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name = "survivalCurve">Curve to calibrate</param>
    /// <param name = "product">Product to price</param>
    /// <param name = "x">Model parameter set or null for Calibrator parameters</param>
    /// <returns>Constructed pricer for product</returns>
    private IPricer GetPricer(SurvivalCurve survivalCurve, IProduct product, double* x)
    {
      if (product is CDSOption)
      {
        var pricer = new CDSOptionHazardPricer((CDSOption) product);
        pricer.AsOf = AsOf;
        pricer.Settle = Settle;
        pricer.DiscountCurve = DiscountCurve;
        pricer.Lambda0 = (x != null) ? x[0] : Parameters[0];
        pricer.Kappa = (x != null) ? x[1] : Parameters[1];
        pricer.Theta = (x != null) ? x[2] : Parameters[2];
        pricer.Sigma = (x != null) ? x[3] : Parameters[3];
        pricer.RecoveryRate = RecoveryCurve.RecoveryRate(((CDSOption)product).CDS.Maturity);

        return pricer;
      }
      else
      {
        ICashflowPricer pricer = CashflowPricerFactory.PricerForProduct(product);

        pricer.AsOf = AsOf;
        pricer.Settle = Settle;
        pricer.DiscountCurve = DiscountCurve;
        pricer.ReferenceCurve = ReferenceCurve;
        pricer.SurvivalCurve = survivalCurve;
        pricer.StepSize = stepSize_;
        pricer.StepUnit = stepUnit_;
        pricer.RecoveryCurve = RecoveryCurve;

        return pricer;
      }
    }

    /// <summary>
    /// Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      return GetPricer((SurvivalCurve) curve, product, null);
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

    #endregion Properties

    #region Data

    private int stepSize_; // Pricing grid step size
    private TimeUnit stepUnit_; // Pricing grid step unit

    #endregion Data
  }
}