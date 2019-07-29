/*
 * SurvivalBootstrapCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Credit curve bootstrap using flat forward hazard rates.
  /// </summary>
  public class SurvivalBootstrapCalibrator : SurvivalCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (SurvivalBootstrapCalibrator));

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name = "asOf">As-of (pricing) date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "dayCount">Premium daycount</param>
    /// <param name = "freq">Premium payment frequency</param>
    /// <param name = "calendar">Calendar for premium payments</param>
    /// <param name = "roll">Business day roll convention for premium payments</param>
    /// <param name = "recoveryCurve">Recovery curve</param>
    /// <param name = "discountCurve">Discount Curve</param>
    public SurvivalBootstrapCalibrator(Dt asOf, Dt settle, DayCount dayCount, Frequency freq, Calendar calendar,
                                       BDConvention roll, RecoveryCurve recoveryCurve, DiscountCurve discountCurve)
      : base(asOf, settle, recoveryCurve, discountCurve)
    {
      // Use attributes for validation
      CDSDayCount = dayCount;
      CDSFreq = freq;
      CDSCalendar = calendar;
      CDSBDConvention = roll;
      CDSInterp = new Cubic();
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name = "asOf">As-of (pricing) date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "dayCount">Premium daycount</param>
    /// <param name = "freq">Premium payment frequency</param>
    /// <param name = "calendar">Calendar for premium payments</param>
    /// <param name = "roll">Business day roll convention for premium payments</param>
    /// <param name = "recoveryRate">Recovery rate</param>
    /// <param name = "discountCurve">Discount Curve</param>
    public SurvivalBootstrapCalibrator(Dt asOf, Dt settle, DayCount dayCount, Frequency freq, Calendar calendar,
                                       BDConvention roll, double recoveryRate, DiscountCurve discountCurve)
      : base(asOf, settle, recoveryRate, discountCurve)
    {
      // Use attributes for validation
      CDSDayCount = dayCount;
      CDSFreq = freq;
      CDSCalendar = calendar;
      CDSBDConvention = roll;
      CDSInterp = new Cubic();
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Fit from a specified point assuming all initial test/etc. have been performed.
    /// </summary>
    /// <param name = "curve">Survival curve to fit</param>
    /// <param name = "fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var survivalCurve = (SurvivalCurve) curve;

      // Initialise tracking of -ve survival probabilities
      NegSPFound = false;

      // Set up curve of CDS rates to interpolate from
      var cdsCurve = new Curve(AsOf);
      cdsCurve.Interp = CDSInterp;
      foreach (CurveTenor tenor in survivalCurve.Tenors)
      {
        if (!(tenor.Product is CDS))
          throw new ToolkitException(String.Format("Invalid product for SurvivalBootstrapCalibrator. Must be CDS"));
        var c = (CDS) tenor.Product;
        cdsCurve.Add(c.Maturity, c.Premium);
      } // foreach

      // Construct vectors of regular CDS for bootstrap
      int periods = 30*(int) cdsFreq_; // 30 years
      var cds = new double[periods];
      var df = new double[periods];
      var deltaT = new double[periods];
      var recovery = new double[periods];

      Dt current = AsOf;
      for (int i = 0; i < periods; i++)
      {
        // Get the next CDS date
        Dt next = Dt.CDSRoll(current, false);
        cds[i] = cdsCurve.Interpolate(next);
        deltaT[i] = Dt.Fraction(current, next, CDSDayCount);
        df[i] = DiscountCurve.DiscountFactor(next);
        recovery[i] = RecoveryCurve.RecoveryRate(next);
        current = next;
      }

      // Do bootstrap
      double[] survival = Bootstrap.CDSToSurvival(cds, df, deltaT, recovery, NegSPTreatment);

      // Save survival curve
      current = AsOf;
      survivalCurve.Clear();
      for (int i = 0; i < survival.Length; i++)
      {
        Dt next = Dt.ImmNext(current);
        survivalCurve.Add(next, survival[i]);
        current = next;
      }

      return;
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

    #endregion Methods

    #region Properties

    /// <summary>
    /// Daycount for default swap spreads.
    /// </summary>
    /// <remarks>
    /// Default is Actual/360
    /// </remarks>
    public DayCount CDSDayCount
    {
      get { return cdsDayCount_; }
      set
      {
        //QN_THROWIF( dayCount == DayCounts::None, InvalidArgumentException("Must set valid daycount"));
        cdsDayCount_ = value;
      }
    }

    /// <summary>
    /// Frequency to generate survival curve (12 = monthly)
    /// </summary>
    public Frequency CDSFreq
    {
      get { return cdsFreq_; }
      set { cdsFreq_ = value; }
    }

    /// <summary>
    /// Roll convention for premium payment schedule
    /// </summary>
    public BDConvention CDSBDConvention
    {
      get { return cdsRoll_; }
      set { cdsRoll_ = value; }
    }

    /// <summary>
    /// Calendar for premium payment schedule
    /// </summary>
    public Calendar CDSCalendar
    {
      get { return cdsCal_; }
      set { cdsCal_ = value; }
    }

    /// <summary>
    /// CDS Interpolation method
    /// </summary>
    public Interp CDSInterp
    {
      get { return cdsInterp_; }
      set { cdsInterp_ = value; }
    }

    /// <summary>
    /// Step size for pricing grid
    /// </summary>
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
    public TimeUnit StepUnit
    {
      get { return stepUnit_; }
      set { stepUnit_ = value; }
    }

    #endregion Properties

    #region Data

    private Calendar cdsCal_;
    private DayCount cdsDayCount_;
    private Frequency cdsFreq_;
    private Interp cdsInterp_;
    private BDConvention cdsRoll_;
    private int stepSize_; // Pricing grid step size
    private TimeUnit stepUnit_; // Pricing grid step unit

    #endregion Data
  }
}