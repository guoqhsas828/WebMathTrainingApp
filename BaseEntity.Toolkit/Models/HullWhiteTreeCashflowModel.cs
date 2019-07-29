//
// HullWhiteTreeCashflowModel.cs
//   2014. All rights reserved.
//
using System;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.HullWhiteShortRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using CashflowAdapter = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Callable cashflow pricing model based on a generalized Hull-White trinomial tree.
  /// </summary>
  /// <remarks>
  /// <para>Hull-White or Black-Karasinski model for Callable Bonds. Interest rates
  /// diffusion process is a normally or log-normally distributed mean-reverting process with
  /// constant mean reversion and volatility.
  /// The interest rate  process has the dynamics described by:</para>
  /// <math>
  ///   dx_t = -\kappa(t)[\theta(t) - x_t]dt + \sigma(t) dW_t
  /// </math>
  /// <para>where <m> x_t = \ln(r_t) </m> for a Black-Karasinski or
  /// <m> x_t = f(r_t) </m> for Hull-White and
  /// <m> r_t </m> is the short rate or hazard rate of interest
  /// where <m> \kappa(t) </m> and <m> \sigma(t) </m>
  /// are deterministic functions of time and <m> dW_t </m> is a Brownian motion.
  /// <m> \kappa(t) </m>, <m> \theta(t) </m>, are
  /// referred to as the mean reversion speed parameters. <m></m>,
  /// <m> >= 0 </m> are the volatility parameters.</para>
  /// <para>The implementation of the model uses a trinomial lattice as described in
  /// "The General Hull-White Model and Super Calibration," (Hull, White Aug 2000). The implemented generalized tree
  /// model allows arbitrary time steps in the lattice and time-dependent volatility and mean reversion
  /// speed.  current model implementation allows users to only specify constant mean reversion
  /// speed and volatility parameters.</para>
  /// <para>For corporate bond pricing a survival curve is overlaid on top of the interest rates tree and the
  /// bond values in the tree are weighted by the survival probabilities at each time slice in the tree. Only if
  /// there is no default the tree is continued. Default probability in this implementation is non-stochastic, which
  /// means it is the same for all nodes corresponding to the same time. Also, depending on user’s input, the model
  /// is able to place defaults at the begiining or end or any point of the tree time slice.</para>
  ///
  /// <para><b>Calibrating the Hull-White tree.</b></para>
  /// <para>The following steps are employed in calibrating the trinomial tree:</para>
  /// <para><b>1. Generate the bond cashflows</b></para>
  /// <math>
  ///   cf_i = c_i \delta_i for i &lt; n, (1+ c_i \delta_i) for i = n
  /// </math>
  /// <para>where <m> \delta_i </m> are the coupon period fractions at each payment date.</para>
  /// <para><b>2. Create a time grid</b></para>
  /// <para>Using cashflow dates. Add the settlement date if it does not coincide with the first cashflow date.
  /// Convert to year fraction.</para>
  /// <para><b>3. Create a trinomial tree</b></para>
  /// <para>At each time node determine the rate increment <m> \delta x_i = \sqrt{3(t_{i} - t_{i-1})} </m>.
  /// Since the trinomial tree is recombining we note that the number of rate nodes <m> j_i </m> at
  /// the <m> i^{th} </m> time node satisfies
  /// <m> j_i = j_{i-1} + 2</m>. For <m> j &lt; j_i </m> we match the
  /// conditional modified mean and variance by defining the transition probabilities</para>
  /// <math>
  ///   p_d = \frac{1}{6}[1 + \frac{e^2}{\sigma^2_i} - \frac{e\sqrt{3}}{\sigma}]
  /// </math>
  /// <math>
  ///   p_m = \frac{1}{3}[2 - \frac{e^2}{\sigma^2}]
  /// </math>
  /// <math>
  ///   p_u = \frac{1}{6}[1 + \frac{e^2}{\sigma^2_i} + \frac{e\sqrt{3}}{\sigma}]
  /// </math>
  /// <para>Where <m> \sigma^2_i </m> is the time <m> t_i </m>
  /// variance of the modified BK diffusion process over the interval <m> t_i - t_{i-1} </m>
  /// and <m> m_{ij} - (x_0 + k_{ij} \Delta x_{i+1}) </m> where <m> k_ij </m>
  /// is the distance between the time <m> i + 1 </m> conditional mean evaluated at the
  /// <m> j^{th} </m> node, divided by the <m> i + j^{th} </m> rate
  /// increment and rounded to the nearest integer. <m> x_0 </m> is typically 0.</para>
  /// <para>As explained in Hull (2000), given the mean reversion speed and volatility parameters
  /// <m> k(t) </m> and <m> \sigma(t), t >= 0 </m>,
  /// the remaining parameters <m> \theta(t), t >= 0 </m>, are determined to ensure that the
  /// zero coupon interest rates calculated in the model match those supplied to the model. The zero-coupon values supplied
  /// to the model are extracted from a bootstrapped discount curve. This is called calibrating to the term structure
  /// (of interest rates). The volatility parameters could be calibrated from a set of at-the-money interest rate
  /// swaption prices. Thsi is currently done in a BGM model setup, but HW and BK models only require a single
  /// mean-reversion and volatility parameter.</para>
  /// <para>Starting with the root node we solve for <m> \theta </m> so that the risk-neutral
  /// expected discounted payoff of a zero coupon bond paying 1 at node <m> t+1 </m>
  /// is equal <m> \frac{Z(t_1)}{Z(t_0)} </m>, where <m> Z(-) </m>
  /// is the appropriate discount factor curve. With the interest rates completely defined up until
  /// <m> t_1 </m> we then repeat for the unknown rates on the interval
  /// <m> (t_1;t_2] </m> to matching <m> \frac{Z(t_2)}{Z(t_0)} </m>.
  /// This process is repeated until <m> \theta </m> adjustments are derived for all tenors
  /// and the trinomial tree (and hence the corresponding interest rates) are completely defined.</para>
  /// <para>Note: The current model implementation does not allow for stochastic spreads. Once random spreads are
  /// added, another trinomial tree needs to be constructed for the spreads process. The interest rate and spread tree
  /// would then be combined into a recombining multi-state tree.</para>
  /// <para><b>4. Value the defaultable bond and the call option</b></para>
  /// <para>Using the corresponding CDS spread we derive the conditional survival probabilities
  /// <m> S(t_i|\tau > t_0) </m> for each time node on the tree. Here
  /// <m> \tau </m> denotes the default time.</para>
  /// <para>Starting at the terminal time with value equal to the terminal cashflow <m> cf_n </m>
  /// we use backward induction on the trinomial tree to derive a discounted value <m> v_{n-1},j</m>
  /// at each node at time n-1. The value of each <m> t_{n-1} </m>node is
  /// then adjusted to account for default in the following manner:</para>
  /// <math>
  ///   v_{n-1,j}^d = v_{n-1,j}S(t_n|\tau > t_{n-1})e^{-r(t_n - t_{n-1})} + df_{av}(1-S(t_n|\tau > t_{n-1}))R
  /// </math>
  /// <para>where <m> R </m> is the recovery rate, and <m> df_{av} </m>
  /// is the average discount factor at the mid-point between time nodes n and n-1. Values for the defaultable bond
  /// are calculated in this manner via backward induction and payoffs at callable dates are calculated in
  /// the usual manner.</para>
  /// </remarks>
  public static class HullWhiteTreeCashflowModel
  {
    // Logger
    private static readonly log4net.ILog logger =
      log4net.LogManager.GetLogger(typeof(HullWhiteTreeCashflowModel));

    internal static bool AllowCallOnMaturity = false;

    #region Private Types
    class Calculator
    {
      /// <summary>
      ///   Adjust bond values at current time slice
      /// </summary>
      /// 
      /// <returns>Adjusted Callable Bond Values</returns>
      ///
      public void AdjustValues(double[] values, int i)
      {
        Debug.Assert(values != null, "values != null");
        // We assume the settle date is never callable.
        double strike = callPrices_ != null ? callPrices_[i] : Double.NaN;
        double adjDf = 1.0;
        var hasSpread = !spread.AlmostEquals(0.0);
        if(hasSpread)
        {
          double deltaT = timeGrid[i + 1] - timeGrid[i];
          adjDf = Math.Exp(-spread * deltaT);
        }

        // Remark: Assumption used in pricer/tree constructor:
        //   TimeGrid Payments coincide with coupon payment dates 
        // Hence all accruals (except first one) will be zero
        for (int j = 0; j < values.Length; ++j)
        {
          if (hasSpread)
          {
            values[j] *= adjDf;
          }
          if (Double.IsNaN(strike)) // the date is not callable
          {
            values[j] += timeGridPayments_[i];
            // payment is received only if  no default (up to time i)
          }
          else // only if date is a callable date
          {
            values[j] = Math.Min(strike + timeGridAccrued_[i], values[j]);
            // if time t = 0, TimeGridPayments[0] = TimeGridAccruals[0] 
            // if time t = 1, TimeGridPayments[1] = partial coupon , TimeGridAccruals[1] = 0;
            // if time t > 1, TimeGridPayments[t] = coupon , TimeGridAccruals[1] = 0;
            values[j] += timeGridPayments_[i];
          }
        }
      }

      public double GetDefaultAmount(int iStep, int iState)
      {
        return timeGridDefaultAmts_[iStep];
      }
      internal double[] callPrices_;
      internal double[] timeGridPayments_;
      internal double[] timeGridAccrued_;
      internal double[] timeGridDefaultAmts_;
      internal double spread;
      internal double[] timeGrid;
    }
    #endregion 

    /// <summary>
    /// Calculate the present value of the specified cashflows
    /// with embeded call options.
    /// </summary>
    /// <param name="cf">The cashflow.</param>
    /// <param name="asOf">The date from which the volatility begins.</param>
    /// <param name="settle">The date from which the protection start.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="diffusionType">Type of the diffusion process.</param>
    /// <param name="meanReversion">The mean reversion coefficient.</param>
    /// <param name="sigma">The volatility coefficient.</param>
    /// <param name="callable">The callable with schedule (null for non-callable).</param>
    /// <param name="accrued">The accrued to add to strike if the settle date is callable.</param>
    /// <returns>A Hull-White tree model.</returns>
    public static double Pv(
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, double recoveryRate,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)
    {
      double spread = 0;
      try
      {
        if (!BackwardCompatibleZSpread)
        {
          spread= discountCurve.Spread;
          discountCurve.Spread = 0;
        }
        return DoPv(cf, asOf, settle, discountCurve, spread, survivalCurve,
          recoveryRate, diffusionType, meanReversion, sigma, callable, accrued);
      }
      finally
      {
        if (!BackwardCompatibleZSpread) discountCurve.Spread = spread;
      }
    }

    private static bool BackwardCompatibleZSpread
    {
      get
      {
        return ToolkitConfigurator.Settings
          .BondPricer.BackwardCompatibleCallableZSpread;
      }
    }

    private static double DoPv(
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve, double spread,
      SurvivalCurve survivalCurve, double recoveryRate,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)
    {
      // Check default flags and return recovery * notional properly
      if (survivalCurve != null)
      {
        Defaulted dftd = survivalCurve.Defaulted;
        if (dftd == Defaulted.HasDefaulted || dftd == Defaulted.WillDefault)
        {
          if (recoveryRate > 0)
            return recoveryRate;
          RecoveryCurve rc;
          if (survivalCurve.SurvivalCalibrator != null &&
            (rc = survivalCurve.SurvivalCalibrator.RecoveryCurve) != null)
          {
            Dt dftdDate = survivalCurve.DefaultDate;
            return rc.RecoveryRate(dftdDate);
          }
          return 0.4; // default recovery rate
        }
      }

      // Fill in cashflow schedules matching tree nodes
      var timeGrid = new TimeGrid();
      timeGrid.Build(cf, callable, settle, accrued);

      DiffusionProcess diffusionProcess;
      switch (diffusionType)
      {
        case DiffusionProcessKind.HullWhite:
          diffusionProcess = new HullWhiteProcess(
            meanReversion, sigma, discountCurve);
          break;
        case DiffusionProcessKind.BlackKarasinski:
          diffusionProcess = new BlackKarasinskiProcess(
            meanReversion, sigma, discountCurve);
          break;
        default:
          throw new ToolkitException(String.Format(
            "Process {0} not supported in Hull-White tree yet",
            diffusionType));
      }

      // Tree Constructor
      var tree = new GeneralizedHWTree(diffusionProcess, timeGrid.Time,
        discountCurve, survivalCurve, settle, recoveryRate)
      {
        TimeGridDates = timeGrid.Dates,
        DefaultTiming = cf.GetDefaultTiming()
      };
      tree.BuildGeneralizedTree();
      TrinomialTreeObserver.Initialize(tree, timeGrid);

      // Find Strike at bond maturity
      Dt maturity = cf.GetDt(cf.Count - 1);
      int iFrom = tree.FindIndex(tree.TimeGrid,
        Dt.TimeInYears(settle, maturity));

      Debug.Assert(maturity == tree.TimeGridDates[iFrom],
        "maturity == HWTree.TimeGridDates[iFrom]");
      double strike = callable != null && AllowCallOnMaturity
        ? callable.GetExercisePriceByDate(maturity) : Double.NaN;

      // Fill in the final payment values at the maturity
      int tmpSize = tree.Size(iFrom);
      double[] values = new double[tmpSize];
      for (int i = 0; i < tmpSize; ++i)
      {
        values[i] = timeGrid.Payments[timeGrid.Payments.Length - 1];
      }
      if (!Double.IsNaN(strike)) // callable on the date
      {
        for (int j = 0; j < values.Length; ++j)
        {
          values[j] = Math.Min(strike + timeGrid.Accrued[iFrom], values[j]);
        }
      }

      // backward induction for Callable Bond
      var calculator = new Calculator
      {
        callPrices_ = timeGrid.CallPrices,
        timeGridPayments_ = timeGrid.Payments,
        timeGridAccrued_ = timeGrid.Accrued,
        timeGridDefaultAmts_ = timeGrid.DefaultAmounts,
        timeGrid = tree.TimeGrid,
        spread = spread,
      };
      values = tree.RollBack(calculator.AdjustValues, iFrom, values, 0,
        calculator.GetDefaultAmount);
      double pv = values[0];
      if (asOf != settle)
      {
        pv *= discountCurve.DiscountFactor(asOf, settle);
      }
      return pv;
    }

    #region Sensitivity fucntions
    /// <summary>
    /// Calculate discount rate spread (zspread/OAS) implied by full price
    /// </summary>
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="diffusionType">Type of the diffusion.</param>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="callable">The callable.</param>
    /// <param name="accrued">The accrued.</param>
    /// <returns>
    /// spread over discount curve implied by price
    /// </returns>
    /// <remarks>
    /// 	<para>Calculates the constant spread (continuously compounded) over
    /// discount curve for cashflow to match a specified full price
    /// <paramref name="fullPrice"/>.</para>
    /// 	<para>This is also commonly called the Z-Spread for non-callable bonds
    /// and the OAS (option adjusted spread) for callable bonds.</para>
    /// 	<para>In other words the OAS is the Z-spread of the stripped(non-callable) bond
    /// when properly adjusting for the value of the embedded call option. Works for both callable and
    /// non-callable bonds. Callable bonds will require a HWTree pricer instead of a bond pricer.</para>
    /// 	<para>For non-defaultable callable bonds the zspread is the OAS i.e the shift that
    /// needs to applied to the short rate in order to make model price and market price match.</para>
    /// 	<para>For defaultable callable bonds we approximate the zspread as the hazard rate of
    /// a flat CDS Spread Curve with zero recovery which makes the model price match the bond
    /// market price.</para>
    /// </remarks>
    /// <remarks> The OAS is the Z-spread of the stripped(non-callable) bond when properly adjusting
    /// for the value of the embedded call option.
    /// </remarks>
    public static double ImpliedDiscountSpread(
      double fullPrice,
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, double recoveryRate,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)
    {
      logger.Debug(String.Format("Trying to solve oas for full price {0}", fullPrice));

      double origSpread = discountCurve.Spread;

      // Create a delegate
      Func<double, double> evaluatePrice = (x) =>
      {
        double savedSpread = discountCurve.Spread;

        // Update spread
        discountCurve.Spread = origSpread + x;

        // Re-price (and refit tree with shifted discount curve)
        double price = Pv(cf, asOf, settle,
          discountCurve, survivalCurve, recoveryRate,
          diffusionType, meanReversion, sigma,
          callable, accrued);
        if (logger.IsDebugEnabled)
        {
          logger.DebugFormat("Trying rate spread {0} --> price {1}", x, price);
        }
        // Restore spread
        discountCurve.Spread = savedSpread;

        return price;
      };

      try
      {
        return evaluatePrice.SolveDiscountSpread(fullPrice);
      }
      finally
      {
        discountCurve.Spread = origSpread;
      }
    }

    /// <summary>
    /// Calculate hazard rate spread over survival spread implied the current market price.
    /// </summary>
    /// <param name="fullPrice">The full price.</param>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="diffusionType">Type of the diffusion.</param>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="callable">The callable.</param>
    /// <param name="accrued">The accrued.</param>
    /// <returns>
    /// spread over survival curve implied by price
    /// </returns>
    /// <remarks>
    /// Calculates constant lambda (continuous) over survival curve for
    /// cashflow to match the current market price.
    /// </remarks>
    public static double ImpSurvivalSpread(double fullPrice,
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, double recoveryRate,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)
    {
      // clone survival curve (if it exists)
      if (survivalCurve == null)
      {
        survivalCurve = new SurvivalCurve(asOf, 0.0);
      }
      else
      {
        survivalCurve = (SurvivalCurve) survivalCurve.Clone();
      }

      // Create a delegate
      Func<double, double> evaluatePrice = (x) =>
      {
        // save orig spread
        double origHSpread = survivalCurve.Spread;

        // Update spread
        survivalCurve.Spread = origHSpread + x;

        // Re-price (and refit tree with shifted discount curve)
        double price = Pv(cf, asOf, settle,
          discountCurve, survivalCurve, recoveryRate,
          diffusionType, meanReversion, sigma,
          callable, accrued);
        if (logger.IsDebugEnabled)
          logger.DebugFormat("Trying h spread {0} --> price {1}", x, price);
        // Restore spread
        survivalCurve.Spread = origHSpread;

        return price;
      };
      return evaluatePrice.SolveSurvivalSpread(fullPrice);
    }

    /// <summary>
    /// Imply a flat CDS curve with a single spread set at 5Y
    /// if hazard rate less than 1, or 3M otherwise.
    /// With this CDS curve, the callable should
    /// calculate a model full price at the market full price as the target.
    /// </summary>
    /// <param name="fullPrice">Market full price for bond</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="diffusionType">Type of the diffusion.</param>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="callable">The callable.</param>
    /// <param name="accrued">The accrued.</param>
    /// <returns>Implied flat survival curve</returns>
    public static SurvivalCurve ImpliedFlatSpreadCurve(
      double fullPrice, double recoveryRate,
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)
    {
      var hazardRate = ImpSurvivalSpread(fullPrice, cf, asOf, settle,
        discountCurve, null, recoveryRate,
        diffusionType, meanReversion, sigma,
        callable, accrued);
      //if (hazardRate < 0)
      //{
      //  throw new ToolkitException("Implied spread is negative.");
      //}
      if (recoveryRate < 0 || Double.IsNaN(recoveryRate))
      {
        recoveryRate = 0.4;
      }
      var curve = SurvivalCurve.FromHazardRate(
        asOf, discountCurve,
        hazardRate < 1 ? "5Y" : "3M",
        hazardRate, recoveryRate, false);
      return curve;
    }

    /// <summary>
    /// Calculate the CDS spread/basis implied by full price.
    /// </summary>
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="diffusionType">Type of the diffusion.</param>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="callable">The callable.</param>
    /// <param name="accrued">The accrued.</param>
    /// <returns>
    /// Spreads shift (also known as basis) to the Survival Curve implied by price
    /// </returns>
    /// <remarks>
    /// Calculates constant spread over survival curve spreads for
    /// cashflow to match a specified full price.
    /// </remarks>
    public static double ImpliedCdsSpread(
      double fullPrice, Dt maturity,
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, double recoveryRate,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)
    {
      if (fullPrice < 0)
        throw new ArgumentOutOfRangeException("fullPrice", "Full price must be +Ve");

      // Get the implied flat CDS curve and implied CDS level
      var flatImpliedSurvivalCurve = ImpliedFlatSpreadCurve(
        fullPrice, recoveryRate > 0 ? recoveryRate : 0.4, cf, asOf, settle,
        discountCurve, diffusionType, meanReversion, sigma,
        callable, accrued);

      // Calculate bond duration and extract spread at the duration generated date.
      double impliedLevel = CurveUtil.ImpliedSpread(flatImpliedSurvivalCurve,
        maturity, DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.None);

      double curveLevel = CurveUtil.ImpliedSpread(survivalCurve, maturity,
        DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
        Calendar.None);

      // Find ImpliedCDSSpread = impliedCDSLevel - curveLevel
      double result = impliedLevel - curveLevel;
      if (logger.IsDebugEnabled)
        logger.Debug(String.Format("Found oas {0}", result));

      return result;
    }

    /// <summary>
    /// Calculate the bond-implied CDS Curve
    /// </summary>
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="diffusionType">Type of the diffusion.</param>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="callable">The callable.</param>
    /// <param name="accrued">The accrued.</param>
    /// <returns>Bond-Implied Survival Curve</returns>
    /// <remarks>
    /// Calculates the (constant)spread that needs to be added/subtracted from CDS curve to
    /// recover full bond price. Once the shift is calculated the shifted survival curve is returned
    /// </remarks>
    public static SurvivalCurve ImpliedCdsCurve(double fullPrice,
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, double recoveryRate,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)
    {
      // Calculate spread basis
      double spreadBasis = ImpliedCdsSpread(
        fullPrice, Dt.CDSMaturity(asOf, "5Y"), cf, asOf, settle,
        discountCurve, survivalCurve, recoveryRate,
        diffusionType, meanReversion, sigma,
        callable, accrued);
      // return shifted survival curve
      var shiftedSurvivalCurve = (SurvivalCurve)survivalCurve.Clone();
      shiftedSurvivalCurve.Calibrator = (Calibrator)survivalCurve.Calibrator.Clone();
      CurveUtil.CurveBump(shiftedSurvivalCurve, null, spreadBasis * 10000.0, true, false, true);
      return shiftedSurvivalCurve;
    }

    /// <summary>
    /// Calculate the CDS spread/basis implied by full price.
    /// </summary>
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="diffusionType">Type of the diffusion.</param>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="callable">The callable.</param>
    /// <param name="accrued">The accrued.</param>
    /// <returns>
    /// Spreads shift (also known as basis) to the Survival Curve implied by price
    /// </returns>
    /// <remarks>
    /// Calculates constant spread over survival curve spreads for
    /// cashflow to match a specified full price.
    /// </remarks>
    public static double CdsSpreadShift(double fullPrice,
      CashflowAdapter cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, double recoveryRate,
      DiffusionProcessKind diffusionType,
      double meanReversion, double sigma,
      ICallable callable, double accrued)

    {
      if (fullPrice < 0)
        throw new ArgumentOutOfRangeException("fullPrice", "Full price must be +Ve");

      if (survivalCurve == null)
        throw new ArgumentException("No Survival Curve passed to the pricer");

      // save original SC
      var origSc = survivalCurve;

      // find smallest quote
      double minQuote = survivalCurve.GetMinQuote();

      // Create a delegate
      Func<double, double> evaluatePrice = (x) =>
      {
        // Clone and shift original survival Curve
        SurvivalCurve shiftedSurvivalCurve = (SurvivalCurve) origSc.Clone();
        shiftedSurvivalCurve.Calibrator = (Calibrator) origSc.Calibrator.Clone();
        CurveUtil.CurveBump(shiftedSurvivalCurve, null, x*10000.0, true, false,
          true);

        // Re-price (and refit tree with shifted discount curve)
        double price = Pv(cf, asOf, settle,
          discountCurve, shiftedSurvivalCurve, recoveryRate,
          diffusionType, meanReversion, sigma,
          callable, accrued);
        if (logger.IsDebugEnabled)
          logger.DebugFormat("Trying rate spread {0} --> price {1}", x, price);

        return price;
      };
      return evaluatePrice.SolveCdsSpreadShift(fullPrice, minQuote);
    }
    #endregion
  }
}
