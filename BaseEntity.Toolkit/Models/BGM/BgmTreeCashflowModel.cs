
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM.Native;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  ///   Cashflow models based on BGM tree.
  /// </summary>
  public static partial class BgmTreeCashflowModel
  {
    // Logger
    private static readonly log4net.ILog logger =
      log4net.LogManager.GetLogger(typeof(BgmTreeCashflowModel));

    private const double Tolerance = 2E-16;

    #region private Methods
    // Create a cashflow for annuity calculation
    

    private static double GetVolatility(
      this IVolatilityObject volatilityObject,
      Dt asOf, Dt expiry, CashflowAdapter cashflow, double strike)
    {
      // If the object is a flat volatility
      {
        var flat = volatilityObject as FlatVolatility;
        if (flat != null) return flat.Volatility;
      }

      Dt maturity = cashflow.IsNullOrEmpty()
        ? Dt.Empty
        : cashflow.GetEndDt(cashflow.Count - 1);
      // If the object is calibrated by BGM models
      {
        var p = volatilityObject as BgmForwardVolatilitySurface;
        if (p != null)
          return ((BgmSwaptionVolatilityInterpolator)
            p.GetSwaptionVolatilityInterpolator(expiry, maturity))
            .Interpolate(asOf, expiry, maturity, cashflow);
      }

      // For volatility curves
      {
        var v = volatilityObject as VolatilityCurve;
        if (v != null)
        {
          //TODO: return (asOf) => v.Interpolate(asOf, swaption.Expiration);
          return v.Interpolate(asOf, maturity);
        }
      }

      // For swaption volatility spline
      {
        var s = volatilityObject as SwaptionVolatilitySpline;
        if (s != null)
        {
          double duration = SwaptionVolatilityCube.ConvertForwardTenor(expiry, maturity);
          if (s.RateVolatilityCalibrator is RateVolatilitySwaptionMarketCalibrator)
          //For swaption volatility cube adjusted from cap/floor calibration
          {
            return s.Evaluate(expiry, duration);
          }
          else
          {
            var calibrator = (RateVolatilityCapFloorBasisAdjustCalibrator)s.RateVolatilityCalibrator;
            var cap = CreateEquivalentCap(expiry, maturity, strike, calibrator);
            return s.CapVolatility(s.RateVolatilityCalibrator.DiscountCurve,cap);
          }
          
        }
      }

      // For swaption volatility cube
      {
        var c = volatilityObject as SwaptionVolatilityCube;
        if (c != null)
        {
          var atm = GetVolatility(c.AtmVolatilityObject, asOf, expiry, cashflow, strike);
          if (c.Skew == null) return atm;
          double duration = SwaptionVolatilityCube.ConvertForwardTenor(expiry, maturity);
          return Math.Max(0.0, atm + c.Skew.Evaluate(expiry, duration, strike));
        }
      }
      throw new ToolkitException(String.Format(
        "Volatility object {0} not supported yet.",
        volatilityObject.GetType().FullName));
    }

    private static Cap CreateEquivalentCap(
      Dt expiry, Dt maturity, double strike,
      RateVolatilityCalibrator calibrator)
    {
      var ri = calibrator.RateIndex;
      return new Cap(expiry, maturity, calibrator.DiscountCurve.Ccy,
        CapFloorType.Floor, strike, ri.DayCount, ri.IndexTenor.ToFrequency(),
        ri.Roll, ri.Calendar);
    }

    private static double SwaptionValue(OptionType type,
      double t, double level, double rate, double strike,
      double volatility, DistributionType volType)
    {
      if(volType==DistributionType.LogNormal)
      {
        return level * Black.P(type, t, rate, strike, volatility);
      }
      if (volType == DistributionType.Normal)
      {
        return level * BlackNormal.P(type, t, 0, rate, strike, volatility);
      }
      throw new ToolkitException("DistributionType {0} not supported yet",
        volType);
    }
    #endregion 

    #region Pv Calculation
    /// <summary>
    /// To calculate the pv that includes the callable option value.
    /// </summary>
    public static double CalculatePvWithOptions(
      this CashflowAdapter cfa,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve,
      double correlation,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      IVolatilityObject volatilityObject,
      CashflowModelFlags flags,
      int stepSize, TimeUnit stepUnit,
      BgmTreeOptions treeOptions)
    {
      double pv = cfa.Pv(asOf, settle, discountCurve, survivalCurve,
        counterpartyCurve, correlation, stepSize, stepUnit, flags);

      if (volatilityObject != null)
      {
        pv -= cfa.OptionValue(asOf, settle,
          optionType, exerciseSchedule, notificationDays, discountCurve,
          survivalCurve, volatilityObject, stepSize, stepUnit,
          treeOptions, null, flags);
      }
      return pv;
    }

    internal static double OptionValue(
      this CashflowAdapter cfa,
      Dt asOf, Dt settle,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      IVolatilityObject volatilityObject,
      int stepSize, TimeUnit stepUnit,
      BgmTreeOptions treeOptions,
      List<KeyValuePair<Dt,double>> callProbabilities,
      CashflowModelFlags flags)
    {
      if (exerciseSchedule == null || exerciseSchedule.Count == 0)
      {
        logger.Debug("Exercis e schedule is empty");
        return 0.0;
      }
      if (cfa.IsNullOrEmpty())
      {
        if (survivalCurve.Defaulted == Defaulted.NotDefaulted)
        {
          throw new ToolkitException("Cashflow is empty");
        }
        else  // VOD/JTD calcualtion will generate the empty cashflow
        {
          return 0.0;
        }
      }
      Dt maturity = cfa.GetEndDt(cfa.Count - 1);
      var cotermSwpns = BuildEquivalentSwaptions(cfa,
        asOf, settle, optionType, exerciseSchedule, notificationDays,
        discountCurve, survivalCurve, volatilityObject,
        stepSize, stepUnit, treeOptions, flags);
      if (cotermSwpns == null || cotermSwpns.Count == 0)
        return 0.0;
      if (BondPricer.ZSpreadConsistentBgmModel
        && !discountCurve.Spread.AlmostEquals(0.0))
      {
        // Find the swap rates WITHOUT the discount spread
        var savedSpread = discountCurve.Spread;
        try
        {
          discountCurve.Spread = 0;
          var swpnsNoSpread = BuildEquivalentSwaptions(cfa,
            asOf, settle, optionType, exerciseSchedule, notificationDays,
            discountCurve, survivalCurve, volatilityObject,
            stepSize, stepUnit, treeOptions, flags);
          for (int i = 0, n = swpnsNoSpread.Count; i < n; ++i)
          {
            // move the spread from the rate to the strike,
            // while keep (Rate - Strike) unchanged.
            var swp = cotermSwpns[i];
            var rateDiff = swpnsNoSpread[i].Rate - swp.Rate;
            swp.Rate += rateDiff;
            swp.Coupon += rateDiff;
            swp.Volatility = swpnsNoSpread[i].Volatility;
            swp.Value = SwaptionValue(optionType,
              (swp.Date - settle)/365.0, swp.Level,
              swp.Rate, swp.Coupon, swp.Volatility,
              volatilityObject.DistributionType);
            cotermSwpns[i] = swp;
          }
        }
        finally
        {
          discountCurve.Spread = savedSpread;
        }
      }
      int idx = FindAnyOverrideNode(cotermSwpns);
      if (idx >= 0)
      {
        return cotermSwpns[idx].Value;
      }
      double pv;
      if (cotermSwpns[0].Date == settle)
      {
        // The first call date is the settlement date.
        // We first calculate the option pv excluding the settle,
        // then compare the value with the value of calling on settle.
        pv = cotermSwpns.Count == 1 ? 0
          : BgmTreeSwaptionEvaluation.CalculateBermudanPv(
            cotermSwpns.Skip(1).ToArray(), settle, maturity,
            volatilityObject.DistributionType);
        return Math.Max(pv, cotermSwpns[0].Value);
      }
      var callInfo = (callProbabilities == null
        ? null : new CallInfo[cotermSwpns.Count]);
      pv = BgmTreeSwaptionEvaluation.CalculateBermudanPv(
        cotermSwpns.ToArray(), settle, maturity,
        volatilityObject.DistributionType, callInfo);
      if (callInfo != null)
      {
        callProbabilities.Clear();
        for (int i = 0; i < callInfo.Length; ++i)
        {
          callProbabilities.Add(new KeyValuePair<Dt, double>(
            cotermSwpns[i].Date, callInfo[i].Probability));
        }
      }
      return pv;
    }

    public static IList<SwaptionInfo> BuildEquivalentSwaptions(
      this CashflowAdapter cfa,
      Dt asOf, Dt settle,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      IVolatilityObject volatilityObject,
      int stepSize, TimeUnit stepUnit,
      BgmTreeOptions treeOpt,
      CashflowModelFlags flags)
    {
      if (cfa.IsNullOrEmpty())
        return null;
      int count = cfa.Count;
      Dt maturity = cfa.GetEndDt(count - 1);
      var dateNtnls = Enumerable.Range(0, count).Select(idx =>
      {
        double notional = -1;
        var dt = cfa.GetDt(idx);
        if (dt < maturity && exerciseSchedule.IndexOf(dt) >= 0)
        {
          notional = cfa.GetPrincipalAt(idx);
          // In case the coupon date moves before the period end date.
          var end = cfa.GetEndDt(idx);
          if (end > dt) dt = end;
        }
        return new {Date = dt, Notional = notional};
      }).Where((dn) => dn.Notional >= 0 && dn.Date > settle).ToArray();
      if(dateNtnls.Length==0)
      {
        logger.Debug("No date exercisable");
        return null;
      }

      var fixedCfa = cfa.ClearNotionalPayment();
      var unitCfa = fixedCfa.CreateUnitCashflow();
      var lossCfa = survivalCurve == null ? null : cfa.CreateLossCashflow();

      var accuracy = treeOpt != null && treeOpt.CalibrationTolerance > 0
        ? treeOpt.CalibrationTolerance : 1E-6; 
      var cotermSwpns = new List<SwaptionInfo>();
      for (int i = 0; i < dateNtnls.Length; ++i)
      {
        Dt expiry = dateNtnls[i].Date;
        Dt noticeDate = expiry - notificationDays;
        if (noticeDate <= asOf) continue;

        // Discount and Notional
        double df = discountCurve.DiscountFactor(asOf, expiry);
        if (df < 1E-12)
        {
          continue; // optionvalue is essentially zero
        }
        double notional = df*dateNtnls[i].Notional;

        // The exercise price must adjust by the protection value.
        double sp = survivalCurve == null ? 1.0
          : survivalCurve.SurvivalProb(settle, expiry);
        if (!(sp > 10E-12)) break;
        double protection = lossCfa == null ? 0.0 : lossCfa.BackwardCompatiblePv(asOf, expiry,
          discountCurve, survivalCurve, null, 0.0, stepSize, stepUnit, 
          lossCfa.GetBackwardCompatibleFlags(flags));
        double exercisePrice = exerciseSchedule.ExercisePriceByDate(expiry);
        double exerciseCost = (exercisePrice - 1)*notional + protection;

        // The exercise cost is treated as something added to strike.
        if (optionType == OptionType.Put) exerciseCost = -exerciseCost;
        double level = unitCfa.BackwardCompatiblePv(asOf, expiry, discountCurve, survivalCurve,
          null, 0.0, stepSize, stepUnit, unitCfa.GetBackwardCompatibleFlags(flags));
        if (!(level > 1E-12))
        {
          throw new ToolkitException(String.Format(
            "Invalid annuity {0} at date {1}", level, expiry));
        }
        double fixedPv = fixedCfa.BackwardCompatiblePv(asOf, expiry, discountCurve, survivalCurve,
          null, 0.0, stepSize, stepUnit, fixedCfa.GetBackwardCompatibleFlags(flags));
        double strike = (fixedPv + exerciseCost)/level;

        // The equivalent swap rate.
        double pv = cfa.BackwardCompatiblePv(asOf, expiry, discountCurve, survivalCurve,
          null, 0.0, stepSize, stepUnit, cfa.GetBackwardCompatibleFlags(flags));
        double rate = (notional + fixedPv - protection - pv) / level;
        CheckIntrinsicValue(notional, pv, exercisePrice, rate, strike, level);

        // The volatility and time to expiry.
        double time = (expiry - settle)/365.0;
        double vol = volatilityObject.GetVolatility(asOf, expiry, cfa, strike);
        if (noticeDate != expiry)
        {
          vol = noticeDate <= settle ? 0.0
            : (vol * Math.Sqrt((noticeDate - settle) / 365.0 / time));
        }

        // Is this option possibly be called?
        if (IsPutOptionEffective(strike, rate, time,
          volatilityObject.DistributionType, vol))
        {
          double optpv = SwaptionValue(optionType, time, level,
            rate, strike, vol, volatilityObject.DistributionType);
          int steps = treeOpt == null ? 0: (cotermSwpns.Count == 0
            ? treeOpt.InitialSteps : treeOpt.MiddleSteps);
          cotermSwpns.Add(new SwaptionInfo
          {
            Date = expiry,
            Level = level*sp,
            Rate = rate,
            Coupon = strike,
            Value = optpv,
            Volatility = vol,
            OptionType = optionType,
            Accuracy = accuracy,
            Steps = steps > 0 ? steps : 0
          });
        }
      }
      return cotermSwpns;
    }

    private static CashflowModelFlags GetBackwardCompatibleFlags(
      this CashflowAdapter cfa, CashflowModelFlags flags)
    {
      return !cfa.IsCashflow ? flags : AdapterUtil.CreateFlags(false, false, true);
    }

    private static int FindAnyOverrideNode(IList<SwaptionInfo> swpns)
    {
      for (int i = 0, n = swpns.Count; i < n; ++i)
      {
        // This is known happen in the case where the survival curve has very large
        // negative spread, making it preferable to always call at the date.
        if (swpns[i].Rate * swpns[i].Level > 1) return i;
      }
      return -1;
    }

    [Conditional("DEBUG")]
    private static void CheckIntrinsicValue(
      double df, double pv, double price,
      double rate, double strike, double level)
    {
      if(!logger.IsDebugEnabled) return;
      double expect = pv - price*df;
      double actual = (strike - rate)*level;
      if(Math.Abs(actual - expect)>1E-12)
      {
        throw new ToolkitException(String.Format(
          "Intrinsic value: expect {0}, actual {1}",
          expect, actual));
        //logger.DebugFormat("Intrinsic value: expect {0}, actual {1}",
        //  expect, actual);
      }
    }

    private static bool IsPutOptionEffective(double strike, double rate,
      double time, DistributionType distribution, double sigma)
    {
      if (distribution == DistributionType.LogNormal)
      {
        if (strike <= 1E-16 || rate <= 1E-16)
        {
          return false;
        }
        strike = Math.Log(strike);
        rate = Math.Log(rate);
      }
      if(strike - rate < -6*sigma*Math.Sqrt(time))
        return false;
      return true;
    }

    internal static double CalculateExpectation(
      this IRateSystemDistributions tree,
      int dateIndex, int stateIndex,
      int nextDateIndex, double[] nextStateValues)
    {
      double sump = 0, sumv = 0;
      int stateCount = nextStateValues.Length;
      for(int i = 0; i < stateCount;++i)
      {
        double prob = tree.GetConditionalProbability(
          nextDateIndex, i, dateIndex, stateIndex);
        sump += prob;
        sumv += prob*nextStateValues[i];
      }
      return sump > Tolerance ? (sumv/sump) : 0.0;
    }
    #endregion Pv Calculation

    #region Implied Discount Spread
    internal static double ImplyDiscountSpread(
      this CashflowAdapter cfa, 
      double fullPrice,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      IVolatilityObject volatilityObject,
      CashflowModelFlags flags,
      int stepSize, TimeUnit stepUnit,
      BgmTreeOptions treeOpt)
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
        double price = cfa.CalculatePvWithOptions(asOf, settle,
          discountCurve, survivalCurve, null, 0.0,
          optionType, exerciseSchedule, notificationDays, volatilityObject,
          flags, stepSize,stepUnit, treeOpt);
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

    internal static double SolveDiscountSpread(this Func<double, double> evaluatePrice, double fullPrice)
    {
      // In general the price function is not linear relative to discount curve quote bump 
      // size, particularly for possible larger bump that target the bond market price.
      // Get the smallest quote from discount curve. Since the discount curve used in 
      // HWTreeCallableBondModel excluded the tenor info, set the smallest quote = -0.005

      List<double> xList = new List<double>();
      List<double> yList = new List<double>();
      {
        double x0 = 0.1;
        while (x0 >= -0.005)
        {
          double evaluation = evaluatePrice(x0);
          // Interpolator requires exclusion of same abscissa values
          if (!(yList.Contains(evaluation)))
          {
            yList.Add(evaluation);
            xList.Add(x0);
          }
          x0 -= 0.005;
        }
      }
      double[] X = xList.ToArray();
      double[] Y = yList.ToArray();
      for (int i = 0; i < X.Length - 1; i++)
      {
        for (int j = i + 1; j < X.Length; j++)
        {
          if (Y[i] > Y[j])
          {
            double temp = Y[i]; Y[i] = Y[j]; Y[j] = temp;
            temp = X[i]; X[i] = X[j]; X[j] = temp;
          }
        }
      }
      Interpolator interp = new Interpolator(InterpFactory.FromMethod(
        InterpMethod.PCHIP, ExtrapMethod.Smooth), Y, X);
      double guess = interp.evaluate(fullPrice);
      if (guess < -1) // -100% rate causes numerical instability
      {
        guess = -0.5;
      }

      logger.Debug(String.Format("Initial guess of oas is {0}", guess));

      // Solve for implied rate spread
      Brent rf = new Brent();
      rf.setToleranceX(1e-8);
      rf.setToleranceF(1e-6);
      rf.setLowerBracket(guess - 0.002);
      rf.setUpperBracket(guess + 0.002);

      double result = rf.solve(evaluatePrice, null, fullPrice, guess);

      if (logger.IsDebugEnabled)
        logger.Debug(String.Format("Found zspread/ oas {0}", result));

      return result;
    }
    #endregion

    #region Imply CDS Spread
    /// <summary>
    /// Calculate hazard rate spread over survival spread implied the current market price.
    /// </summary>
    /// <param name="cf">The cf.</param>
    /// <param name="fullPrice">The full price.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="optionType">Type of the option.</param>
    /// <param name="exerciseSchedule">The exercise schedule.</param>
    /// <param name="notificationDays">The notification days</param>
    /// <param name="volatilityObject">The volatility object.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="stepUnit">The step unit.</param>
    /// <param name="treeOpt">The bgm tree option.</param>
    /// <returns>
    /// spread over survival curve implied by price
    /// </returns>
    /// <remarks>
    /// Calculates constant lambda (continuous) over survival curve for
    /// cashflow to match the current market price.
    /// </remarks>
    internal static double ImplySurvivalSpread(
      this CashflowAdapter cf, 
      double fullPrice,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      IVolatilityObject volatilityObject,
      CashflowModelFlags flags,
      int stepSize, TimeUnit stepUnit, BgmTreeOptions treeOpt)
    {
      // clone survival curve (if it exists)
      if (survivalCurve == null)
      {
        survivalCurve = new SurvivalCurve(asOf, 0.0);
      }
      else
      {
        survivalCurve = (SurvivalCurve)survivalCurve.Clone();
      }

      // Create a delegate
      Func<double, double> evaluatePrice = (x) =>
      {
        // save orig spread
        double origHSpread = survivalCurve.Spread;

        // Update spread
        survivalCurve.Spread = origHSpread + x;

        // Re-price (and refit tree with shifted discount curve)
        double price = cf.CalculatePvWithOptions(asOf, settle,
          discountCurve, survivalCurve, null, 0.0, optionType,
          exerciseSchedule, notificationDays, volatilityObject, flags,
          stepSize, stepUnit, treeOpt);
        if (logger.IsDebugEnabled)
          logger.DebugFormat("Trying h spread {0} --> price {1}", x, price);
        // Restore spread
        survivalCurve.Spread = origHSpread;

        return price;
      };
      return evaluatePrice.SolveSurvivalSpread(fullPrice);
    }

    internal static double SolveSurvivalSpread(
      this Func<double, double> evaluatePrice, double fullPrice)
    {
      // Guess
      double b = evaluatePrice(0.0);
      double bShift = evaluatePrice(0.001);
      double guess = 0;
      if (!b.AlmostEquals(bShift))
        guess = (fullPrice - b) / ((bShift - b) / 0.001);
      if (logger.IsDebugEnabled)
        logger.Debug(String.Format("Initial guess of oas is {0}", guess));

      // Solve for implied rate spread
      var rf = new Brent();
      rf.setToleranceF(1e-6);
      rf.setToleranceX(1e-8);
      rf.setLowerBounds(-1.0);

      double hazardRateShift = rf.solve(evaluatePrice, null,
        fullPrice, guess - 0.002, guess + 0.002);
      if (logger.IsDebugEnabled)
        logger.Debug(String.Format("Found hShift {0}", hazardRateShift));

      return hazardRateShift;
    }

    /// <summary>
    /// Imply a flat CDS curve with a single spread set at 5Y
    /// if hazard rate less than 1, or 3M otherwise.
    /// With this CDS curve, the callable should
    /// calculate a model full price at the market full price as the target.
    /// </summary>
    /// <param name="cfa">The cash flow adapter</param>
    /// <param name="fullPrice">Market full price for bond</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="optionType">Type of the option.</param>
    /// <param name="exerciseSchedule">The exercise schedule.</param>
    /// <param name="notificationDays">The notification days.</param>
    /// <param name="volatilityObject">The volatility object.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="stepUnit">The step unit.</param>
    /// <param name="treeOpt">The bgm tree option.</param>
    /// <returns>Implied flat survival curve</returns>
    internal static SurvivalCurve ImplyFlatSpreadCurve(
      this CashflowAdapter cfa, 
      double fullPrice, double recoveryRate,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      IVolatilityObject volatilityObject,
      CashflowModelFlags flags,
      int stepSize, TimeUnit stepUnit, BgmTreeOptions treeOpt)
    {
      var hazardRate = cfa.ImplySurvivalSpread(fullPrice, asOf, settle,
        discountCurve, null, optionType, exerciseSchedule, notificationDays,
        volatilityObject, flags, stepSize, stepUnit, treeOpt);
      bool allowNegativeSpread = (CashflowModelFlags.AllowNegativeSpread
        & flags) != 0;
      if (hazardRate < 0 && !allowNegativeSpread)
      {
        throw new ToolkitException("Implied spread is negative.");
      }
      if (recoveryRate < 0 || Double.IsNaN(recoveryRate))
      {
        recoveryRate = 0.4;
      }

      var curve = SurvivalCurve.FromHazardRate(
        asOf, discountCurve,
        hazardRate < 1 ? "5Y" : "3M",
        hazardRate, recoveryRate, false);
      ((SurvivalFitCalibrator) curve.SurvivalCalibrator).AllowNegativeCDSSpreads
        = allowNegativeSpread;
      return curve;
    }

    /// <summary>
    /// Calculate the CDS spread/basis implied by full price.
    /// </summary>
    /// <param name="cfa">The cash flow adpater</param>
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="optionType">Type of the option.</param>
    /// <param name="exerciseSchedule">The exercise schedule.</param>
    /// <param name="notificationDays">The notification days.</param>
    /// <param name="volatilityObject">The volatility object.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="stepUnit">The step unit.</param>
    /// <param name="treeOpt">The bgm tree option.</param>
    /// <returns>
    /// Spreads shift (also known as basis) to the Survival Curve implied by price
    /// </returns>
    /// <remarks>
    /// Calculates constant spread over survival curve spreads for
    /// cashflow to match a specified full price.
    /// </remarks>
    internal static double ImplyCdsSpread(
      this CashflowAdapter cfa,
      double fullPrice, Dt maturity, double recoveryRate,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      IVolatilityObject volatilityObject,
      CashflowModelFlags flags,
      int stepSize, TimeUnit stepUnit, BgmTreeOptions treeOpt)
    {
      if (fullPrice < 0)
        throw new ArgumentOutOfRangeException("fullPrice", "Full price must be +Ve");

      // Get the implied flat CDS curve and implied CDS level
      var flatImpliedSurvivalCurve = cfa.ImplyFlatSpreadCurve(
        fullPrice, recoveryRate > 0 ? recoveryRate : 0.4, asOf, settle,
        discountCurve, optionType, exerciseSchedule, notificationDays,
        volatilityObject, flags, stepSize, stepUnit, treeOpt);

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
    /// <param name="cf">The cf.</param>
    /// <param name="fullPrice">Target full price (percentage of notional)</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurve">The survival curve.</param>
    /// <param name="optionType">Type of the option.</param>
    /// <param name="exerciseSchedule">The exercise schedule.</param>
    /// <param name="notificationDays">The notification days.</param>
    /// <param name="volatilityObject">The volatility object.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="stepUnit">The step unit.</param>
    /// <param name="treeOpt">The bgm  tree option</param>
    /// <returns>Bond-Implied Survival Curve</returns>
    /// <remarks>
    /// Calculates the (constant)spread that needs to be added/subtracted from CDS curve to
    /// recover full bond price. Once the shift is calculated the shifted survival curve is returned
    /// </remarks>
    internal static SurvivalCurve ImplyCdsCurve(
      this CashflowAdapter cf,
      double fullPrice, double recoveryRate,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      IVolatilityObject volatilityObject,
      CashflowModelFlags flags,
      int stepSize, TimeUnit stepUnit, BgmTreeOptions treeOpt)
    {
      // Calculate spread basis
      double spreadBasis = cf.ImplyCdsSpread(
        fullPrice, Dt.CDSMaturity(asOf, "5Y"), recoveryRate,
        asOf, settle, discountCurve, survivalCurve,
        optionType, exerciseSchedule, notificationDays,
        volatilityObject, flags, stepSize, stepUnit, treeOpt);

      // return shifted survival curve
      var shiftedSurvivalCurve = (SurvivalCurve)survivalCurve.Clone();
      shiftedSurvivalCurve.Calibrator = (Calibrator)survivalCurve.Calibrator.Clone();
      CurveUtil.CurveBump(shiftedSurvivalCurve, null, spreadBasis * 10000.0, true, false, true);
      return shiftedSurvivalCurve;
    }
    #endregion

    internal static double ImplyCdsSpreadShift(
      this CashflowAdapter cfa,
      double fullPrice, double recoveryRate,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
      OptionType optionType,
      IList<IOptionPeriod> exerciseSchedule,
      BusinessDays notificationDays,
      IVolatilityObject volatilityObject,
      CashflowModelFlags flags,
      int stepSize, TimeUnit stepUnit, BgmTreeOptions treeOpt)
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
        SurvivalCurve shiftedSurvivalCurve = (SurvivalCurve)origSc.Clone();
        shiftedSurvivalCurve.Calibrator = (Calibrator)origSc.Calibrator.Clone();
        CurveUtil.CurveBump(shiftedSurvivalCurve, null, x * 10000.0, true, false,
          true);

        // Re-price (and refit tree with shifted discount curve)
        double price = cfa.CalculatePvWithOptions(asOf, settle,
          discountCurve, shiftedSurvivalCurve, null, 0.0, optionType, exerciseSchedule,
          notificationDays, volatilityObject, flags, stepSize, stepUnit, treeOpt);
        if (logger.IsDebugEnabled)
          logger.DebugFormat("Trying rate spread {0} --> price {1}", x, price);

        return price;
      };

      // Set up root finder
      return SolveCdsSpreadShift(evaluatePrice, fullPrice, minQuote);
    }

    internal static double SolveCdsSpreadShift(
      this Func<double, double> evaluatePrice, double fullPrice, double minQuote)
    {
      Brent rf = new Brent();
      rf.setToleranceX(10e-6);
      rf.setToleranceF(10e-6);
      // Solve for price over reasonable range of spread shifts
      rf.setLowerBounds(-minQuote + 10e-8);
      rf.setUpperBounds(0.1); // 1000 bps

      double result = rf.solve(evaluatePrice, null,
        fullPrice, -minQuote + 10e-8, 0.1);
      if (logger.IsDebugEnabled)
        logger.Debug(String.Format("Found oas {0}", result));

      return result;
    }


    /// <summary>
    ///  Find the smallest possible minimum quote that can pass the curve bumping
    ///  Idea: 
    ///  [1] Using bisection method to find the first quote q that pass curve bump
    ///  [2] For [minQuote, q], find the first quote that fails the curve bump
    ///      where the quote is 1e-3 to the left of a successful quote on the right
    ///  [3] The successful quote is set to be minQuote and returned
    /// </summary>
    internal static double GetMinQuote(this SurvivalCurve survivalCurve)
    {
      // find smallest quote
      CurveTenorCollection tenors = survivalCurve.Tenors;
      int count = survivalCurve.Tenors.Count;
      double minQuote = CurveUtil.MarketQuote(tenors[0]);
      for (int i = 1; i < count; ++i)
      {
        double quote = CurveUtil.MarketQuote(tenors[i]);
        if (quote < minQuote)
          minQuote = quote;
      }

      if (((SurvivalFitCalibrator)survivalCurve.SurvivalCalibrator).ForceFit == true)
        return minQuote;

      minQuote -= 1e-8;

      bool foundMinQuote = true;
      // save original SC
      var origSC = survivalCurve;
      // Clone and shift original survival Curve
      var shiftedSurvivalCurve = (SurvivalCurve)origSC.Clone();
      shiftedSurvivalCurve.Calibrator = (Calibrator)origSC.Calibrator.Clone();
      try
      {
        CurveUtil.CurveBump(shiftedSurvivalCurve, null, -minQuote * 10000.0, true, false, true);
      }
      catch (Exception)
      {
        foundMinQuote = false;
      }
      if (foundMinQuote)
        return minQuote;

      // Now minQuote fails the curve bump, need to find the first successful quote
      double xl = -minQuote, xr = 0, xmid = (xl + xr) / 2.0;
      while (!foundMinQuote)
      {
        shiftedSurvivalCurve = (SurvivalCurve)origSC.Clone();
        shiftedSurvivalCurve.Calibrator = (Calibrator)origSC.Calibrator.Clone();
        try
        {
          CurveUtil.CurveBump(shiftedSurvivalCurve, null, xmid * 10000.0, true, false, true);
          foundMinQuote = true;
        }
        catch (Exception)
        {
          foundMinQuote = false;
          xl = xmid; xmid = (xl + xr) / 2.0;
        }
      }

      // Now xmid pass the curve bump, push left to find quote that fails the curve bump
      foundMinQuote = true;
      xr = xmid; xmid = (xl + xr) / 2.0;
      while (foundMinQuote || Math.Abs(xr - xmid) > 1e-3)
      {
        shiftedSurvivalCurve = (SurvivalCurve)origSC.Clone();
        shiftedSurvivalCurve.Calibrator = (Calibrator)origSC.Calibrator.Clone();
        try
        {
          CurveUtil.CurveBump(shiftedSurvivalCurve, null, xmid * 10000.0, true, false, true);
          foundMinQuote = true;
          xr = xmid;
          xmid = (xl + xr) / 2.0;
        }
        catch (Exception)
        {
          foundMinQuote = false;
          xl = xmid;
          xmid = (xl + xr) / 2.0;
        }
      }

      minQuote = -xr;
      return minQuote;
    }

    #region Tree Building and Evaluating
    public delegate double Evaluator(
      int cashflowIndex, double continuationValue, double rate);

    public struct CalibratedTree
    {
      public IRateSystemDistributions Tree { get; set; }
      public RateAnnuity[][] RateAnnuities { get; set; }
    }

 
    /// <summary>
    /// Calculates the present value based on a tree.
    /// </summary>
    /// <param name="calibratedTree">The tree.</param>
    /// <param name="cashflowDates">The cashflow dates.</param>
    /// <param name="nodeValueFn">The node value fn.</param>
    /// <returns></returns>
    public static double CalculatePv(
      this CalibratedTree calibratedTree,
      IList<Dt> cashflowDates,
      Evaluator nodeValueFn)
    {
      var tree = calibratedTree.Tree;
      var rateData = calibratedTree.RateAnnuities;
      int last = cashflowDates.Count - 1;
      var map = MapDates(cashflowDates, tree.NodeDates);
      int dateIndex = map[last];
      var ras = rateData[dateIndex];
      int stateCount = ras.Length;
      var values = new double[stateCount];
      for (int si = 0; si < stateCount; ++si)
      {
        values[si] = nodeValueFn(last, 0.0, ras[si].Rate) * ras[si].Annuity;
      }
      for (int ci = last; --ci >= 0; )
      {
        double[] conValues;
        int prevDateIndex = map[ci];
        if (prevDateIndex == dateIndex)
        {
          conValues = values;
        }
        else
        {
          int prevStateCount = rateData[prevDateIndex].Length;
          conValues = new double[prevStateCount];
          for (int si = 0; si < prevStateCount; ++si)
          {
            conValues[si] = tree.CalculateExpectation(
              prevDateIndex, si, dateIndex, values);
          }
          dateIndex = prevDateIndex;
          stateCount = prevStateCount;
        }
        ras = rateData[dateIndex];
        values = new double[stateCount];
        for (int si = 0; si < stateCount; ++si)
        {
          double annuity = ras[si].Annuity;
          values[si] = Math.Abs(annuity) < Tolerance ? 0.0
            : (nodeValueFn(ci, conValues[si] / annuity, ras[si].Rate) * annuity);
        }
      }
      if (dateIndex > 0)
      {
        double contValue = tree.CalculateExpectation(
          0, 0, dateIndex, values);
        return contValue;
      }
      return values[0];
    }

    /// <summary>
    /// Calculates the present value based on a tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="cashflowDates">The cashflow dates.</param>
    /// <param name="nodeValueFn">The node value fn.</param>
    /// <returns></returns>
    public static double CalculatePv(
      this IRateSystemDistributions tree,
      IList<Dt> cashflowDates,
      Evaluator nodeValueFn)
    {
      int last = cashflowDates.Count - 1;
      var map = MapDates(cashflowDates, tree.NodeDates);
      int dateIndex = map[last];
      int rateIndex = tree.GetLastResetIndex(dateIndex);
      int stateCount = tree.GetStateCount(dateIndex);
      var values = new double[stateCount];
      for (int si = 0; si < stateCount; ++si)
      {
        values[si] = nodeValueFn(last, 0.0,
          tree.GetRate(rateIndex, dateIndex, si))
            * tree.GetAnnuity(rateIndex, dateIndex, si);
      }
      for (int ci = last; --ci >= 0; )
      {
        double[] conValues;
        int prevDateIndex = map[ci];
        if (prevDateIndex == dateIndex)
        {
          conValues = values;
        }
        else
        {
          int prevStateCount = tree.GetStateCount(prevDateIndex);
          conValues = new double[prevStateCount];
          for (int si = 0; si < prevStateCount; ++si)
          {
            conValues[si] = tree.CalculateExpectation(
              prevDateIndex, si, dateIndex, values);
          }
          dateIndex = prevDateIndex;
          rateIndex = tree.GetLastResetIndex(dateIndex);
          stateCount = prevStateCount;
        }
        values = new double[stateCount];
        for (int si = 0; si < stateCount; ++si)
        {
          double annuity = tree.GetAnnuity(rateIndex, dateIndex, si);
          values[si] = Math.Abs(annuity) < Tolerance ? 0.0
            : (nodeValueFn(ci, conValues[si] / annuity,
              tree.GetRate(rateIndex, dateIndex, si)) * annuity);
        }
      }
      if (dateIndex > 0)
      {
        double contValue = tree.CalculateExpectation(
          0, 0, dateIndex, values);
        return contValue;
      }
      return values[0];
    }

    private static int[] MapDates(IList<Dt> maturities, Dt[] dates)
    {
      int n = maturities.Count, m = dates.Length - 1;
      var map = new int[n];
      if (m <= 0) return map;
      for (int i = 0, j = 0; i < n; ++i)
      {
        Dt date = maturities[i];
        for (; j <= m; ++j)
        {
          int cmp = Dt.Cmp(date, dates[j]);
          if (cmp <= 0)
          {
            j = j > 0 ? (j - 1) : j;
            break;
          }
        }
        map[i] = j > m ? m : j;
      }
      return map;
    }
    #endregion
  }
}
