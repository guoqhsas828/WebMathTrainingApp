/*
 * SurvivalFitCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using Cashflow = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///  Calibrate a survival curve using the fit algorithm.
  /// </summary>
  /// <remarks>
  ///  <para>The SurvivalFit calibrator constructs a survival curve based on a set of products by
  ///  backing out the implied survival probability to the maturity of each product in sequence.
  ///  There are various options in terms of the form of the survival probability which allow for
  ///  alternate forms of the hazard rate such as flat, smooth or time-weighted.</para>
  ///
  ///  <para>Any product that can be priced using the CashflowPricer can be used to calibrate.</para>
  ///
  ///  <para>The SurvivalFit calibrator implements a generalization of the general bootstrap method.
  ///  Underlying construction of the credit curve is a model of default based on the concept of
  ///  hazard rates. Defaults are presumed to arrive randomly in a Poisson fashion so that the basic
  ///  object of interest, the probability that a firm will survive t units of time from
  ///  today, with hazard rate or intensity of default <formula inline = "true"> \lambda(t)</formula></para>
  ///
  ///  <formula>
  ///    (1)\;\; p(t) = E[P(\tau \gt t)] = E[P(\tau \gt t|{\lambda(s)\: 0\leq s \leq t})]
  ///  </formula>
  /// 
  ///  <para>is given by the expression</para>
  /// 
  ///  <formula>
  ///    (2)\;\; p(t) = e^{-\int_0^t \lambda(s)ds}
  ///  </formula>
  ///
  ///  <para>The standard boostrap model is based on the simplest of all hazard rate models. In
  ///  this case the hazard rate is assumed to be piecewise constant, with dates on which the rate
  ///  changes known beforehand. This allows for the exact fitting of market data in certain situations,
  ///  and leads to</para>
  ///
  ///  <formula>
  ///    (3)\;\; p(t) = e^{-\sum_{i=1}^n \lambda_i(t_i - t_{i-1})}
  ///  </formula>
  ///  
  ///  <para>for some grid of times <formula inline = "true"> 0=t_0</formula>&lt;
  ///  <formula inline = "true">t_1</formula>&lt;<formula inline = "true">t_2</formula>&lt;
  ///  <formula inline = "true">\cdots</formula>&lt;<formula inline = "true">t_n=t</formula>.
  ///  Note that the integral in Equation (2) is now a sum since the hazard rate is constant between
  ///  grid points.</para>
  ///
  ///  <para>Rather than using a step function as in the normal bootstrap model, the generalized
  ///  bootstrap model offers a range of continuous functions that are parameterized to
  ///  match market data in the same way that the bootstrap model does. The fitting criteria is that
  ///  the integral of the hazard rate over defined time segments must provide a pricing relationship
  ///  that matches the market data supplied, just as in the bootstrap model. In this case, however,
  ///  the integral isn't of a constant but of a chosen class of continuous functions. The Toolkit
  ///  provides a choice of piecewise linear, which is analogous to standard linear interpolation,
  ///  piecewise exponential, and piecewise cubic. In the piecewise cubic case, the resulting hazard
  ///  rate will also be continuously differentiable.  In the generalized bootstrap case, Equation (2)
  ///  applies and is easily calculated in closed form. Specifically...</para>
  ///
  ///  <para><b>Case 1:</b> If a linear function is chosen, the
  ///  hazard rate would be expressed as:</para>
  ///
  ///  <formula>
  ///    (4)\;\; \lambda(t) = a_it + b_i
  ///  </formula>
  ///
  ///  <para>when <formula inline = "true"> t_{i-1}</formula>&lt;<formula inline = "true">t \leq t_i </formula>,
  ///  which when evaluating Equation (2) yields</para>
  ///
  ///  <formula>
  ///    (5)\;\; p(t)=e^{-\left(\displaystyle\frac{a_k}{2}(t^2-t_k^2) + b_k(t-t_k) +\sum_{i=1}^k \left(\frac{a_i}{2}(t_i^2-t_{i-1}^2) + b_i(t_i-t_{i-1})\right)\right)}
  ///  </formula>
  ///
  ///  <para>where <formula inline = "true"> k </formula> is the index
  ///  satisfying <formula inline = "true"> t_{k-1}</formula>&lt; t &lt;<formula inline = "true">t_k </formula>.</para>
  ///
  ///  <para><b>Case 2:</b> If a piecewise cubic function is chosen, the
  ///  hazard rate would be expressed as:</para>
  ///
  ///  <formula>
  ///    (6)\;\; \lambda(t) = a_i t^3 + b_i t^2 + c_i t + d_i
  ///  </formula>
  ///
  ///  <para>when <formula inline = "true"> t_{i-1}</formula>&lt;<formula inline = "true">t \leq t_i</formula>.
  ///  Define a function <formula inline = "true">g(a, b, c, d, t, s) = \displaystyle{\frac{a}{4}(t^4-s^4) + \frac{b}{3}(t^3-s^3)
  ///  + \frac{c}{2}(t^2-s^2) + d(t-s)}</formula> for convenience, and note now that
  ///  evaluating Equation (2) we obtain</para>
  ///
  ///  <formula>
  ///    (7)\;\; p(t) = e^{-\left(  \displaystyle{g(a_k, b_k, c_k, d_k, t, t_k) + \sum_{i=1}^k g(a_i, b_i, c_i, d_i, t_i, t_{i-1})}  \right)}
  ///  </formula>
  ///
  ///  <para>where <formula inline = "true"> k </formula> is again the
  ///  index satisfying <formula inline = "true"> t_{k-1}</formula>&lt; t &lt;<formula inline = "true">t_k </formula></para>
  ///
  ///  <para><b>Case 3:</b> Finally, if a piecewise exponential fit is chosen, the
  ///  hazard rate would be expressed as:</para>
  ///
  ///  <formula>
  ///    (8)\;\; \lambda(t) = a_i + b_i e^{\displaystyle{c_i t}}
  ///  </formula>
  ///
  ///  <para>when <formula inline = "true"> t_{i-1}</formula>&lt;<formula inline = "true">t \leq t_i</formula>, which implies that
  ///  Equation (2) is now more explicitly calculated as</para>
  ///
  ///  <formula>
  ///    (9)\;\; p(t)=e^{-\left(\displaystyle{a_k(t-t_k) + \frac{b_k}{c_k}e^{c_k(t-t_k)}} + \sum_{i=1}^k \left(a_i(t_i-t_{i-1}) + \frac{b_i}{c_i}e^{c_i(t_i-t_{i-1})}\right)  \right)}
  ///  </formula>
  ///
  ///  <para>where <formula inline = "true"> k </formula> is again, naturally, the index satisfying
  ///  <formula inline = "true"> t_{k-1}</formula>&lt; t &lt;<formula inline = "true">t_k </formula>.</para>
  /// </remarks>
  /// <note>
  ///  <para>For a simpler wrapper for common curve construction needs
  ///  <see cref = "CalibratorUtil">Calibrator Utilities</see></para>
  /// </note>
  /// <example>
  ///  Fit a Survival curve to a term structure of CDS quotes
  ///  <code language = "C#">
  ///    Dt asOf = Dt.today();                                   // Pricing date
  ///    Dt settle = asOf;                                       // Settlement date
  ///    double forwardRate = 0.02;                              // Continuous forward rate for simple ir curve
  ///    string[] cdsTenors = new string[] { "1y", "5y", "10y" };// Vector of CDS tenors
  ///    int[] cdsQuotes = new int[] { 32, 48, 58 };             // Vector of matching CDS quotes in bp
  ///    double recoveryRate = 0.40;                             // Assumed recovery rate as % of face
  ///
  ///    // Create simple constant forward rate discounting curve
  ///    DiscountCurve discountCurve = new DiscountCurve(asOf, forwardRate);
  ///
  ///    // Create survival calibrator
  ///    SurvivalFitCalibrator calibrator =
  ///    new SurvivalFitCalibrator(
  ///      asOf,            // Pricing date
  ///      settle,          // Settlement date
  ///      recoveryRate,    // Flat single recovery rate
  ///      discountCurve    // Discount curve for calibration
  ///    );
  ///
  ///    // Create survival curve
  ///    SurvivalCurve survivalCurve = new SurvivalCurve( calibrator );
  ///
  ///    // Add cds to calibration
  ///    for( int i=0; i &lt; cdsMaturity.Count; i++ )
  ///    {
  ///      Dt maturity = Dt.cdsMaturity( asOf, cdsTenor[i]);
  ///      survivalCurve.AddCDS(
  ///        maturity,                 // Maturity date of CDS
  ///        cdsQuotes[i]/10000.0,     // CDS quote in %
  ///        DayCount.Actual360,       // Premium daycount
  ///        Frequency.Quarterly,      // Premium payment frequency
  ///        BDConvention.Modified,    // Premium roll convention
  ///        Calendar.NYB );           // Calendar for premium payments
  ///      );
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
  ///  </code>
  /// </example>
  [Serializable]
  public class SurvivalFitCalibrator : SurvivalCalibrator
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof (SurvivalFitCalibrator));

    #region Config

    // hard coded, retire later
    private static readonly bool useCashflowMaturity_ = true;

    #endregion Config

    #region Constructors

    /// <summary>
    /// Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    /// <para>Settlement date defaults to as-of (pricing) date.</para>
    /// </remarks>
    /// <param name="asOf">As-of (pricing) date</param>
    public SurvivalFitCalibrator(Dt asOf) : this(asOf, asOf, null, null)
    {
    }

    /// <summary>
    /// Constructor given as-of (pricing) and settlement dates
    /// </summary>
    /// <remarks>
    /// <para>Settlement date defaults to as-of (pricing) date.</para>
    /// </remarks>
    /// <param name="asOf">As-of (pricing) date</param>
    /// <param name="settle">Settlement date</param>
    public SurvivalFitCalibrator(Dt asOf, Dt settle) : this(asOf, settle, null, null)
    {
    }

    /// <summary>
    /// Constructor given recovery and discount curves
    /// </summary>
    /// <param name="asOf">As-of (pricing) date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="discountCurve">Discount Curve</param>
    public SurvivalFitCalibrator(Dt asOf, 
      Dt settle, 
      RecoveryCurve recoveryCurve, 
      DiscountCurve discountCurve)
      : base(asOf, settle, recoveryCurve, discountCurve)
    {
      stepSize_ = 0; // (int)settings_.SurvivalCalibrator.StepSize;
      stepUnit_ = TimeUnit.None; // settings_.SurvivalCalibrator.StepUnit;
    }

    /// <summary>
    ///  Constructor given recovery and discount curves
    /// </summary>
    /// <param name="asOf">As-of (pricing) date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="discountCurve">Discount Curve</param>
    public SurvivalFitCalibrator(Dt asOf, 
      Dt settle, 
      double recoveryRate, 
      DiscountCurve discountCurve)
      : this(asOf, settle, new RecoveryCurve(asOf, recoveryRate), discountCurve)
    {
    }

    /// <summary>
    /// Clone object
    /// </summary>
    public override object Clone()
    {
      var obj = (SurvivalFitCalibrator) base.Clone();

      obj.rateResets_ = CloneUtil.Clone(rateResets_);

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Fit from a specified point assuming all initial test/etc. have been performed.
    /// </summary>
    /// <param name="curve">Survival curve to calibrate</param>
    /// <param name="fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      Exception exception = TryFitFrom(curve, fromIdx);
      if (exception == null) return;
      DetermineCause(curve, exception);
      throw exception;
    }

    /// <summary>
    /// Try to fit the curve from a given tenor.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="fromIdx">From idx.</param>
    /// <returns>Null if successfully fit; otherwise, an exception.</returns>
    private Exception TryFitFrom(CalibratedCurve curve, int fromIdx)
    {
      var survivalCurve = new OverlayWrapper(curve);

      // Some validation
      if (DiscountCurve == null || DiscountCurve.Count <= 0)
        throw new ToolkitException("Cannot fit survival curve without discount curve set");

      // Construct product cashflows in advance for speed as we know
      // these will not be modified by changes in the survival curve.
      //
      var cashflows = new Cashflow[survivalCurve.Tenors.Count];
      var inclMatProts = new bool[survivalCurve.Tenors.Count];
      for (int i = 0; i < survivalCurve.Tenors.Count; i++)
      {
        // Construct pricer to generate cashflows
        var pricer = (ICashflowPricer) survivalCurve.Tenors[i].GetPricer(survivalCurve, this);
        // Save generated cashflows
        cashflows[i] = pricer.Cashflow;
        inclMatProts[i] = pricer.IncludeMaturityProtection;
      }

      // Construct curve tenors if we have not done so.
      // Note: We set the survival probability dates to the last payment date of the CDS
      // as this may be after the maturity date of the CDS if the maturity date is
      // on a weekend. RTD Jun05.
      if (survivalCurve.Count != survivalCurve.Tenors.Count)
      {
        var flags = curve.Flags;
        survivalCurve.Clear();
        curve.Flags = flags;
        for (int i = 0; i < survivalCurve.Tenors.Count; i++)
        {
          Dt maturity = (UseCashflowMaturity && cashflows[i].Count > 0)
                          ? cashflows[i].GetDt(cashflows[i].Count - 1)
                          : survivalCurve.Tenors[i].Maturity;
          if (inclMatProts[i]) maturity = Dt.Add(maturity, 1);
          if (maturity > AsOf)
          {
            survivalCurve.Add(maturity, 1.0);
          }
        }
      }

      // If curve is defaulted skip the fit and simply fill with "zeros"
      if (survivalCurve.Defaulted != Defaulted.NotDefaulted)
      {
        for (int i = fromIdx; i < survivalCurve.Count; i++) survivalCurve.SetVal(i, 1e-8);
        return null;
      }
     
      // Note we are solving for the absolute survival probability so we can bracket effectively.
      var exception= FitFrom(survivalCurve, fromIdx, cashflows, inclMatProts);

      //Return to check the first curve point.
      for (int i = 0; i < 10 && exception == null; ++i) 
      {
        CurveTenor tenor = survivalCurve.Tenors[fromIdx];
        var eval = (survivalCurve.Flags & CurveFlags.Stressed) != 0
          ? EvaluateRate(survivalCurve.GetRate(fromIdx),
            survivalCurve, fromIdx, cashflows, inclMatProts)
          : Evaluate(survivalCurve.GetVal(fromIdx),
            survivalCurve, fromIdx, cashflows, inclMatProts);
        if (Math.Abs(tenor.MarketPv - eval) <= ToleranceF/10.0) 
          break;
        exception = FitFrom(survivalCurve, fromIdx, cashflows, inclMatProts);
      }
      return exception;
    }

    private Exception FitFrom(OverlayWrapper survivalCurve, int fromIdx, Cashflow[] cashflows, bool[] inclMatProts)
    {
      // Initialise tracking of -ve survival probabilities
      FitWasForced = false;
      NegSPFound = false;

      var rf = new Brent();
      rf.setUpperBounds(1.0);
      rf.setMaxIterations(1000);
      rf.setMaxEvaluations(5000);

      // Fit each security in sequence
      for (int currentIdx = fromIdx; currentIdx < survivalCurve.Count; currentIdx++)
      {
        CurveTenor tenor = survivalCurve.Tenors[currentIdx];

        // Remember financing spread
        double origSpread = DiscountCurve.Spread;

        // First do not try Neg SP or check for forced fitting
        bool tryNegSP = false;
        bool triedNegSP = false;
        bool tryForce = false;
        bool triedForce = false;

        double res = Double.NaN; // to please MS C# Compiler
        do
        {
          // Find survival probability
          //-logger.DebugFormat("Tenor {0}: solving for price {1}", currentIdx_, tenor.MarketPv);

          try
          {
            // Set financing spread
            DiscountCurve.Spread = origSpread + tenor.FinSpread;

            if (tryForce && currentIdx > 0)
            {
              triedForce = true;
              res = ImpliedSurvivalProbability(survivalCurve, currentIdx, cashflows, inclMatProts);
            }
            else if (tryForce)
            {
              // This is the case where currentIdx == 0,
              // i.e., the first tenor does not fit.
              // We set the curve to be stressed,
              // so the CashflowModel will automatically switches 
              // to the log-linear integral approximation.
              triedForce = true;
              survivalCurve.Flags |= CurveFlags.Stressed;

              // Set up root finder function to work in hazard rate space.
              rf.setLowerBounds(0);
              rf.setUpperBounds(Math.Log(Double.MaxValue));
              rf.setToleranceX(1E-12);
              rf.setToleranceF(1E-12);
              rf.restart();

              // Solve for hazard rate.
              int idx = currentIdx;
              rf.solve((x) => EvaluateRate(x, survivalCurve, idx, cashflows, inclMatProts), null, tenor.MarketPv, 5, 20);
              // Return solution in survival probability.
              res = survivalCurve.GetVal(idx);

              // We got solution, do not need to go around again
              tryNegSP = false;
              tryForce = false;
            }
            else
            {
              // Set up root finder function in probability space.
              double ub;
              if (tryNegSP)
              {
                rf.setLowerBounds(-1.0 + 1E-10);
                ub = (AllowNegativeCDSSpreads) ? 10.0 : 1.0;
              }
              else
              {
                rf.setLowerBounds(1E-10);
                ub = 1.0;
              }

              if (ForbidNegativeHazardRates)
              {
                ub = currentIdx == 0 ? 1.0 : survivalCurve.GetVal(currentIdx - 1);
              }
              rf.setUpperBounds(ub);


              rf.setToleranceX(ToleranceX);
              rf.setToleranceF(ToleranceF);
              rf.restart();

              // Guess next level.
              double sp = (currentIdx > 0) ? survivalCurve.GetVal(currentIdx - 1) : 1.0;
              double deltaT = Dt.Fraction((currentIdx > 0) ? survivalCurve.Tenors[currentIdx - 1].Maturity : AsOf,
                                          tenor.Maturity, DayCount.Actual360);

              // Solve for survival probability
              int idx = currentIdx;
              res = rf.solve((x) => Evaluate(x, survivalCurve, idx, cashflows, inclMatProts), null, tenor.MarketPv,
                             sp/(1.0 + .05*deltaT), sp);
            }

            // We got solution, do not need to go around again
            tryNegSP = false;
            tryForce = false;
          }
          catch (Exception e)
          {
            DiscountCurve.Spread = origSpread;
            if (NegSPTreatment == NegSPTreatment.Allow && !tryNegSP && !triedNegSP)
            {
              tryNegSP = true;
              triedNegSP = true;
            }
            else if (ForceFit && !triedForce)
            {
              tryForce = true;
              tryNegSP = false;
            }
            else
            {
              return
                new SurvivalFitException(
                  String.Format(
                    "Unable to fit {0} at tenor {1}. Best we could do is a survival prob {2} with a market price of {3}.  Caught exception: {4}",
                    survivalCurve.Name, tenor.Name, rf.getCurrentSolution(), rf.getCurrentF(), e.Message),
                  survivalCurve.Name, tenor);
            }
          }
          finally
          {
            // Restore financing spread
            DiscountCurve.Spread = origSpread;
          }
        } while (tryNegSP || tryForce);

        // Save result
        survivalCurve.SetVal(currentIdx, res);
        if (!triedForce) tenor.ModelPv = rf.getCurrentF();

        //logger.DebugFormat("Tenor {0}: res {1}, diff={2}", tenor.Name, res, tenor.ModelPv-tenor.MarketPv);

        // Adjust if necessary based on -ve forward survival treatment
        if ((currentIdx > 0) && (res > survivalCurve.GetVal(currentIdx - 1)))
        {
          NegSPFound = true;
          switch (NegSPTreatment)
          {
            case NegSPTreatment.Zero:
              logger.DebugFormat("Tenor {0}: adjusting forward hazard rate to 0.", tenor.Name);
              tenor.ModelPv = Evaluate(res, survivalCurve, currentIdx, cashflows, inclMatProts);
              break;
            case NegSPTreatment.Adjust:
              // TBD RTD. 21Oct03
              break;
            default:
              break;
          }
        }
      } // for

      return null;
    }

    private void DetermineCause(CalibratedCurve curve, Exception exception)
    {
      var tempCalibrator = (SurvivalFitCalibrator) Clone();

      //flat no-discounting curve
      var flatDC = new DiscountCurve(DiscountCurve.AsOf, 0);
      tempCalibrator.DiscountCurve = flatDC;

      try
      {
        // Try fitting survival curve using flat IR curve
        Exception ex = tempCalibrator.TryFitFrom(curve, 0);
        // If there's a new exception even using flat IR curve, it's still due to survival curve
        if (ex != null) return;
      }
      catch (Exception e)
      {
        //error is still due to survival curve
        e.ToString();
        return;
      }
      // We should throw a more detailed message.
      string message = exception.Message + ". Possibly due to invalid Discount Curve";
      throw new SurvivalFitException(message, DiscountCurve.Name, curve.TenorAfter(AsOf));
    }

    
    private double ImpliedSurvivalProbability(OverlayWrapper survivalCurve, int index, Cashflow[] cashflows,
                                              bool[] includeMaturityProtections)
    {
      if (index <= 0) throw new ArgumentException("Tenor index must be larger than 0.");

      // Construct a new curve for interpolation.
      // Note: The old curve has tenor INDEX but this tenor contains garbage,
      //  which make the curve not work with Interpolate at this point.
      var sc = new SurvivalCurve(this);
      sc.Ccy = survivalCurve.Ccy;
      sc.Interp = survivalCurve.CurveToFit.Interp;

      for (int i = 0; i < index; ++i) sc.Add(survivalCurve.GetDt(i), survivalCurve.GetVal(i));

      // Find the implied survival probability
      double sp = sc.Interpolate(survivalCurve.GetDt(index));

      // Find and set the implied model pv
      var modelPv = cashflows[index]
        .BackwardCompatiblePv(Settle, Settle, DiscountCurve, sc,
          IsCountertyCurveActive ? CounterpartyCurve : null,
          IsCountertyCurveActive ? CounterpartyCorrelation : 0.0,
          stepSize_, stepUnit_, AdapterUtil.CreateFlags(false,
            includeMaturityProtections[index],
            Settings.CashflowPricer.DiscountingAccrued));

      survivalCurve.Tenors[index].ModelPv = modelPv;
      // Set the force fit flag
      FitWasForced = true;
      logger.DebugFormat("Forced survival curve {0} to fit by copying previous quote to tenor {1}", survivalCurve.Name,
                        survivalCurve.Tenors[index].Name);

      return sp;
    }


    private double Evaluate(double x, OverlayWrapper ow, int currentIdx, Cashflow[] cashflows,
      bool[] includeMaturityProtections)
    {
      double pv = 0.0;

      // Update current parameter
      ow.SetVal(currentIdx, x);
      // Price using this parameter
      var sc = ow.Main;
      Dt asOf = valueDate_.IsEmpty() ? Settle : valueDate_;
      sc.ClearCache();
      pv = cashflows[currentIdx]
        .BackwardCompatiblePv(asOf, Settle, DiscountCurve, (SurvivalCurve) sc,
          IsCountertyCurveActive ? CounterpartyCurve : null,
          IsCountertyCurveActive ? CounterpartyCorrelation : 0.0,
          stepSize_, stepUnit_, AdapterUtil.CreateFlags(false,
            includeMaturityProtections[currentIdx],
            Settings.CashflowPricer.DiscountingAccrued));

      return pv;
    }


    private double EvaluateRate(double x, OverlayWrapper ow, int currentIdx, Cashflow[] cashflows,
                                bool[] includeMaturityProtections)
    {
      double pv = 0.0;

      // Update current parameter
      ow.SetRate(currentIdx, x);

      // Price using this parameter
      var sc = ow.Main;
      Dt asOf = valueDate_.IsEmpty() ? Settle : valueDate_;
      sc.ClearCache();
      pv = cashflows[currentIdx]
        .BackwardCompatiblePv(asOf, Settle, DiscountCurve, (SurvivalCurve) sc,
          IsCountertyCurveActive ? CounterpartyCurve : null,
          IsCountertyCurveActive ? CounterpartyCorrelation : 0.0,
          stepSize_, stepUnit_, AdapterUtil.CreateFlags(false,
            includeMaturityProtections[currentIdx],
            Settings.CashflowPricer.DiscountingAccrued));

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

      ICashflowPricer pricer = CashflowPricerFactory.PricerForProduct(product);
      pricer.AsOf = AsOf;
      pricer.Settle = Settle;
      pricer.DiscountCurve = DiscountCurve;
      pricer.ReferenceCurve = ReferenceCurve;
      pricer.SurvivalCurve = survivalCurve;
      if (IsCountertyCurveActive)
      {
        pricer.CounterpartyCurve = CounterpartyCurve;
        pricer.Correlation = CounterpartyCorrelation;
      }
      pricer.StepSize = stepSize_;
      pricer.StepUnit = stepUnit_;
      pricer.RecoveryCurve = RecoveryCurve;
      foreach (RateReset r in RateResets) pricer.RateResets.Add(r);

      return pricer;
    }

    
    /// <summary>
    /// Wraps the refinance curve into the calibrator.
    /// </summary>
    /// <param name="refinanceCurve">The refinance curve.</param>
    /// <param name="correlation">The correlation.</param>
    /// <exception cref="ToolkitException">The curve {0} already contains a refinance curve.</exception>
    /// <remarks>
    ///   Unlike in the case of the LCDS curve where the credit curve is calibrated from LCDS quotes,
    ///   here the credit curve is calibrated from CDS quotes an the wrapped refinance curve does not
    ///   participate the calibration.
    /// </remarks>
    public void WrapRefinanceCurve(
      SurvivalCurve refinanceCurve, double correlation)
    {
      if (CounterpartyCurve != null)
      {
        throw new ToolkitException(
          "The curve {0} already contains a refinance curve.",
          CounterpartyCurve.Name);
      }
      CounterpartyCurve = refinanceCurve;
      CounterpartyCorrelation = correlation;
      _pureCounterpartyCurveHolder = true;
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
    ///   Use cashflow maturity instead of product maturity
    /// </summary>
    /// <remarks>
    ///   This value defaults to false for backwards compatibility.  However, we recommend that
    ///   you set the value to true as this will give more correct results.
    /// </remarks>
    [Category("Base")]
    public static bool UseCashflowMaturity
    {
      get { return useCashflowMaturity_; }
    }

    /// <summary>
    ///  Historical rate fixings
    /// </summary>
    /// <remarks>
    ///  <para>Rate resets are stored as a SortedList.  The sort key is the date, and the
    ///    value is the rate, which means you can add the resets in any order but easily
    ///    retrieve them sorted by date.</para>
    /// </remarks>
    public IList<RateReset> RateResets
    {
      get
      {
        if (rateResets_ == null) rateResets_ = new List<RateReset>();
        return rateResets_;
      }
      set { rateResets_ = (List<RateReset>) value; }
    }

    /// <summary>
    ///   Allow Negative Spreads in the Survival Curve
    /// </summary>
    public bool AllowNegativeCDSSpreads
    {
      get { return allowNegativeSpreads_; }
      set { allowNegativeSpreads_ = value; }
    }

    /// <summary>
    ///   Allow Negative Spreads in the Survival Curve
    /// </summary>
    public bool ForbidNegativeHazardRates { get; set; } = false;

    internal Dt ValueDate
    {
      get { return valueDate_; }
      set { valueDate_ = value; }
    }

    /// <summary>
    /// Convert to string
    /// </summary>
    public override string ToString()
    {
      return base.ToString() + "; " + "StepSize = " + StepSize + "; " + "StepUnit = " + StepUnit + "; ";
    }

    private bool IsCountertyCurveActive
    {
      get { return CounterpartyCurve != null && !_pureCounterpartyCurveHolder; }
    }
    #endregion Properties

    #region Data

    //TODO: change these flags to bit fields
    private bool allowNegativeSpreads_;
    private bool _pureCounterpartyCurveHolder;
    private List<RateReset> rateResets_;
    private int stepSize_; // Pricing grid step size
    private TimeUnit stepUnit_; // Pricing grid step unit
    private Dt valueDate_;

    #endregion Data
  }

  #region Utility
  static class SurvivalFitUtility
  {
    internal static IPricer GetPricer(this CurveTenor tenor,
      OverlayWrapper ow, Calibrator calibrator)
    {
      return tenor.GetPricer(ow.Main, calibrator);
    }
  }
  #endregion

} //namespace calibrator