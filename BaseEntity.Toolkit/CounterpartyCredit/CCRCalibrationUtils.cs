// 
//  -2012. All rights reserved.
// 

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Calibration utils
  /// </summary>
  public static partial class CCRCalibrationUtils
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(CCRCalibrationUtils));

    #region MarketVariableType

    /// <summary>
    /// Primary market variable types 
    /// </summary>
    public enum MarketVariableType
    {
      /// <summary>
      /// Forward rate
      /// </summary>
      ForwardRate,

      /// <summary>
      /// Swap rate
      /// </summary>
      SwapRate,

      /// <summary>
      /// Credit spread
      /// </summary>
      CreditSpread,

      /// <summary>
      /// Credit spread of counterparty
      /// </summary>
      CounterpartyCreditSpread,

      /// <summary>
      /// Spot FX rate 
      /// </summary>
      SpotFx,

      /// <summary>
      /// Spot rate
      /// </summary>
      SpotPrice,

      /// <summary>
      /// Forward price  
      /// </summary>
      ForwardPrice,

      /// <summary>
      /// Forward FX rate  
      /// </summary>
      ForwardFx

    }

    #endregion

    #region Credit

    /// <summary>
    /// Calibrate survival process volatilities to CDX option quotes
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="underlierTenor">As of date</param>
    /// <param name="blackVol">underlier</param>
    /// <param name="credit">underlier</param>
    /// <param name="discountCurve">Discount curves</param>
    /// <param name="creditVolatilities">Credit volatilities to be overwritten</param>
    /// <param name="quadPts">Quadrature points</param>
    /// <param name="logCollection">Overwritten by calibration performance log</param>
    /// <returns>Calibration outcome</returns>
    public static void CalibrateCreditVolatility(
      Dt asOf,
      VolatilityCurve blackVol,
      Tenor underlierTenor,
      SurvivalCurve credit,
      DiscountCurve discountCurve,
      VolatilityCollection creditVolatilities,
      CalibrationUtils.CalibrationLogCollection logCollection,
      int quadPts)
    {
      if (credit == null || credit.Tenors == null)
        throw new ArgumentException("credit does not contain valid tenors");
      var tenor = credit.Tenors.FirstOrDefault(t => t.Product is CDS);
      if (tenor == null)
        throw new ArgumentException(String.Format("Cannot infer underlying {0} CDS terms from SurvivalCurve tenors", underlierTenor));
      var cds = (CDS)tenor.Product;
      cds = new CDS(cds.Effective, Dt.CDSMaturity(cds.Effective, underlierTenor), cds.Ccy, 0.0, cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar);
      var vol = CalibrationUtils.CalibrateCreditVolatilities(asOf, cds, credit, -1.0, blackVol, discountCurve, logCollection, quadPts);
      creditVolatilities.Add(credit, vol);
    }

    #endregion

    #region Rates

    private struct AtmForwardVolInterpolator
    {
      public Dt AsOf;
      public int Count;
      public DistributionType DistributionType;
      public Func<int, Dt> ResetDate;
      public IList<Dt> RunningTime;
      public Func<Dt, int, double> Interpolate;
    }

    /// <summary>
    /// Gets the projection curve matching the specified discount curve
    ///  from the rate volatility calibrator.
    /// </summary>
    /// <param name="discountCurve">The discount curve to match</param>
    /// <param name="calibrator">The calibrator</param>
    /// <returns>The desired projection curve, or null</returns>
    private static DiscountCurve GetProjectionCurve(
      DiscountCurve discountCurve, RateVolatilityCalibrator calibrator)
    {
      // The assumption is that the property <c>calibrator.DiscountCurve</c>,
      // if exists, is the projection curve used to calibrate the volatility
      // surface/cube.  We check whether this curve is indeed a projection
      // curve built with the supplied discount curve.  If so, we return the
      // curve; otherwise, return null.
      if (BaseEntity.Toolkit.Util.Configuration.ToolkitConfigurator.Settings.Simulations.EnableDualCurveVolatility
        && calibrator?.DiscountCurve?.NativeCurve.Overlay != null
        && calibrator.DiscountCurve.Overlay.Id == discountCurve.Id)
      {
        return calibrator.DiscountCurve;
      }
      return null;
    }

    private static double[,] ToFwdFwdVols(
      this SwaptionVolatilitySpline swaptionVolatilityCube,
      DiscountCurve liborCurve,
      IRateVolatilityCalibrationModel model,
      Dt[] bespokeTenors,
      out VolatilityCurve[] bespokeCapletVols)
    {
      var calibrator = swaptionVolatilityCube.RateVolatilityCalibrator as RateVolatilitySwaptionMarketCalibrator;
      if (calibrator == null)
      {
        throw new ArgumentException(
          $"Calibrator is null in {liborCurve.Ccy} SwaptionVolatilityCube");
      }
      try
      {
        var atmQuotes = swaptionVolatilityCube.DataView;
        var atmVols = new double[calibrator.ExpiryTenors.Count, calibrator.ForwardTenors.Count];
        for (int i = 0; i < atmVols.GetLength(0); ++i)
          for (int j = 0; j < atmVols.GetLength(1); ++j)
            atmVols[i, j] = atmQuotes[i, j].QuoteValues.First();

        double[,] retVal = null;
        bespokeCapletVols = model.FromSwaptionVolatility(
          liborCurve, GetProjectionCurve(liborCurve, calibrator),
          atmVols, swaptionVolatilityCube.DistributionType,
          calibrator.ExpiryTenors, calibrator.ForwardTenors,
          bespokeTenors, ref retVal);
        return retVal;
      }
      catch (Exception e)
      {
        throw new ArgumentException("Cannot calibrate caplet " +
          $"vols/factor loadings from {liborCurve.Ccy} SwaptionVolatilityCube",
          e);
      }
    }

    private static IEnumerable<VolatilityCurve> ToFwdFwdVols(
      this SwaptionVolatilitySpline swaptionVolatilityCube,
      DiscountCurve liborCurve,
      IRateVolatilityCalibrationModel model,
      Dt[] bespokeTenors, double[,] bespokeFactors)
    {
      var calibrator = swaptionVolatilityCube.RateVolatilityCalibrator as RateVolatilitySwaptionMarketCalibrator;
      if (calibrator == null)
        throw new ArgumentException(String.Format("Cannot calibrate caplet vols/factor loadings from {0} SwaptionVolatilityCube", liborCurve.Ccy));
      try
      {
        var atmQuotes = swaptionVolatilityCube.DataView;
        var atmVols = new double[calibrator.ExpiryTenors.Count, calibrator.ForwardTenors.Count];
        for (int i = 0; i < atmVols.GetLength(0); ++i)
          for (int j = 0; j < atmVols.GetLength(1); ++j)
            atmVols[i, j] = atmQuotes[i, j].QuoteValues.First();
        return model.FromSwaptionVolatility(
          liborCurve, GetProjectionCurve(liborCurve, calibrator),
          atmVols, swaptionVolatilityCube.DistributionType,
          calibrator.ExpiryTenors.ToArray(),
          calibrator.ForwardTenors.ToArray(),
          bespokeTenors, ref bespokeFactors);
      }
      catch (Exception e)
      {
        throw new ArgumentException(
          $"Cannot calibrate caplet vols/factor loadings from {liborCurve.Ccy} SwaptionVolatilityCube",
          e);

      }
    }

    private static AtmForwardVolInterpolator InterpolatorFactory(DiscountCurve liborCurve, object calibratedVolatilities)
    {
      var swaptionVolCube = calibratedVolatilities as SwaptionVolatilityCube;
      if (swaptionVolCube != null)
      {
        var capFloorCalibrator = swaptionVolCube.RateVolatilityCalibrator as RateVolatilityCapFloorBasisAdjustCalibrator;
        if (capFloorCalibrator != null)
          return InterpolatorFactory(liborCurve, swaptionVolCube.VolatilitySurface); //return underlying cap floor vol
      }
      var volCube = calibratedVolatilities as RateVolatilityCube;//caplet vol
      if (volCube != null)
      {
        var rateIndex = volCube.RateVolatilityCalibrator.RateIndex;
        Dt effective = Cap.StandardEffective(volCube.AsOf, rateIndex);
        int tenor = rateIndex.IndexTenor.Days;
        return new AtmForwardVolInterpolator
               {
                 AsOf = volCube.AsOf,
                 DistributionType = volCube.DistributionType,
                 Count = int.MaxValue,
                 ResetDate = i => Dt.Add(effective, i * tenor),
                 RunningTime = volCube.Dates,
                 Interpolate =
                   (dt, i) =>
                   {
                     Dt reset = Dt.Add(effective, i * tenor);
                     Dt maturity = Dt.Roll(Dt.Add(reset, tenor), rateIndex.Roll, rateIndex.Calendar);
                     return volCube.ForwardVolatility(dt, reset, liborCurve.F(reset, maturity, rateIndex.DayCount, Frequency.None)); //atm
                   }
               };
      }
      var fwdVolSurface = calibratedVolatilities as BgmForwardVolatilitySurface;
      if (fwdVolSurface != null)
      {
        var resetDates = fwdVolSurface.CalibratedVolatilities.ResetDates;
        var fwdFwdVols = fwdVolSurface.CalibratedVolatilities.ForwardVolatilityCurves;
        return new AtmForwardVolInterpolator
               {
                 AsOf = fwdVolSurface.AsOf,
                 DistributionType = fwdVolSurface.DistributionType,
                 Count = fwdFwdVols.Length,
                 ResetDate = i => (i == 0) ? fwdVolSurface.AsOf : resetDates[i - 1],
                 RunningTime = fwdVolSurface.CalibratedVolatilities.ResetDates,
                 Interpolate = (date, i) => ((i == 0) || (date > resetDates[i - 1])) ? 0.0 : fwdFwdVols[i - 1].Interpolate(date) //atm
               };
      }
      var modelParam = calibratedVolatilities as RateModelParameters;
      if (modelParam != null)
      {
        Dt asOf = liborCurve.AsOf;
        int tenor = modelParam.Tenor(RateModelParameters.Process.Funding).Days;
        return new AtmForwardVolInterpolator
               {
                 AsOf = asOf,
                 DistributionType = modelParam.DistributionType,
                 Count = int.MaxValue,
                 ResetDate = i => Dt.Add(asOf, i * tenor),
                 RunningTime = null,
                 Interpolate = (date, index) =>
                               {
                                 Dt reset = Dt.Add(asOf, index * tenor);
                                 Dt maturity = Dt.Add(reset, tenor);
                                 double fwd = liborCurve.F(reset, maturity);
                                 return (modelParam.DistributionType == DistributionType.Normal)
                                          ? modelParam.ImpliedNormalVolatility(RateModelParameters.Process.Funding, asOf, fwd, fwd, reset, reset)
                                          : modelParam.ImpliedVolatility(RateModelParameters.Process.Funding, asOf, fwd, fwd, reset, reset); //atm
                               }
               };
      }
      var volCurve = calibratedVolatilities as VolatilityCurve;
      if (volCurve != null)
      {
        return new AtmForwardVolInterpolator
        {
          AsOf = volCurve.AsOf,
          DistributionType = volCurve.DistributionType,
          Count = volCurve.Count,
          ResetDate = i => volCurve.GetDt(i),
          RunningTime = null,
          Interpolate = (date, index) => volCurve.GetVal(index)
        };
      }
      var volSurface = calibratedVolatilities as CalibratedVolatilitySurface;
      if (volSurface != null)
      {
        var tenors = volSurface.Tenors;
        if (tenors.IsNullOrEmpty())
          throw new ArgumentException("Volatility tenors empty");
        return new AtmForwardVolInterpolator
        {
          AsOf = volSurface.AsOf,
          DistributionType = DistributionType.LogNormal,
          Count = tenors.Length,
          ResetDate = i => tenors[i].Maturity,
          RunningTime = null,
          Interpolate = (date, index) => volSurface.GetAtmVolatility(tenors[index])
        };
      }
      throw new ArgumentException(String.Format("Cannot handle calibratedVolatilities of type {0}", calibratedVolatilities.GetType()));
    }


    private static Curve GetFwdFwdVolatility(Dt asOf, Dt resetDate, int index, AtmForwardVolInterpolator interpolator)
    {
      var fwdVol = new Curve(asOf)
                   {
                     Interp = new Flat(0.0),
                     DayCount = DayCount.None,
                     Frequency = Frequency.None
                   };
      if (interpolator.RunningTime == null || interpolator.RunningTime.Count == 0)
        fwdVol.Add(resetDate, interpolator.Interpolate(resetDate, index));
      else
      {
        foreach (Dt dt in interpolator.RunningTime)
        {
          fwdVol.Add(dt, interpolator.Interpolate((dt >= resetDate) ? resetDate : dt, index)); //left continuous
          if (dt >= resetDate)
            break;
        }
      }
      return fwdVol;
    }

    private static void GenerateVolCurves(DiscountCurve discountCurve, AtmForwardVolInterpolator interpolator,
                                           FactorLoadingCollection factorLoadings, VolatilityCollection liborVolatilities)
    {
      var asOf = interpolator.AsOf;
      var bespokeResetDates = Array.ConvertAll(factorLoadings.Tenors, ten => Dt.Add(asOf, ten));
      var fl = factorLoadings.GetFactorsAt(discountCurve);
      var capletFwdVols = new List<Curve>();
      var capletResets = new List<Dt>();
      for (int i = 0; i < interpolator.Count; ++i)
      {
        Dt resetDate = interpolator.ResetDate(i);
        capletFwdVols.Add(GetFwdFwdVolatility(asOf, resetDate, i, interpolator));
        capletResets.Add(resetDate);
        if (resetDate > bespokeResetDates[bespokeResetDates.Length - 1])
          break;
      }
      var volCurves = CalibrationUtils.MapCapletVolatilities(asOf, discountCurve, capletResets.ToArray(), capletFwdVols.ToArray(), bespokeResetDates, fl, null,
                                                             interpolator.DistributionType);
      liborVolatilities.Add(discountCurve, volCurves.ToArray());
    }


    /// <summary>
    /// Calibrate factor loadings of forward rates from factor loadings of one or more representative swap rates. If calibratedVols object is of type 
    /// SwaptionVolatilityCube the forward rate volatilities are calibrated jointly to factor loadings, so as to match relevant swaption volatility quotes. 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="calibratedVols">Calibrated caplet volatility</param>
    /// <param name="swapRateEffective">Effective date of the primary swap rate</param>
    /// <param name="swapRateMaturities">Maturity of the primary swap rate</param>
    /// <param name="swapRateFactorLoadings">Factor loadings of the primary swap rate</param>
    /// <param name="curveDates">For time inhomogenous volatility, specify time points</param>
    /// <param name="bespokeResetDates">tenors to calibrate for</param>
    /// <param name="separableVol">True to calibrate instantaneous vol of the form <m>\phi(T)\psi(t)</m> where <m>\phi(T)</m> is a maturity dependent constant 
    /// and <m>\psi(t)</m> is a time dependent function common to all tenors. This functional specification in a one factor framework results in a rank one 
    /// covariance matrix. If false, calibrated vol is of the form \phi(T)\psi(T-t). This parametric form preserves the shape of the vol surface over running time.</param>
    /// <param name="bespokeCapletVols">Libor vols for bespoke tenors</param>
    /// <remarks>
    /// In the definition of the market environment, a set of forward tenors, <m>T_0, T_1, \cdots, T_n, </m> 
    /// are provided. Let <m>F_i(t)</m> denote the forward LIBOR rate <m>F(t; T_{i-1}, T_i)</m> from <m>T_{i-1}</m>
    /// to <m>T_i</m> as of time <m>t</m>. Assume the forward rates either satisfy Lognormal distribution:
    /// <math>
    /// d F_i(t) = \mu_i(t)F_i(t) dt + \sigma_i(t)F_i(t) \sum_{j=1}^d \varphi_{ij} dW_j(t), \ \ \ \ \ i = 1, \cdots, n 
    /// </math>
    /// or Normal distribution:
    /// <math>
    /// d F_i(t) = \mu_i(t) dt + \sigma_i(t) \sum_{j=1}^d \varphi_{ij} dW_j(t), \ \ \ \ \ i = 1, \cdots, n 
    /// </math>
    /// , where <m>d</m> is the number of factors, and <m>\{\varphi_{ij}\}</m> are factor loadings with 
    /// <m>\sum_{j=1}^d \varphi_{ij}^2 = 1.</m> 
    /// <para>Calibrate the factor loadings <m>\varphi_{ij}</m> and volatilities <m>\sigma_i</m> by two methods
    /// which have the assumption for bespoke factor loadings but different assumptions for bespoke volatilities. In the
    /// first method, the forward IR model is calibrated with input forward caplet volatility cube by assuming that each forward
    /// rate has constant instantaneous volatilities. For one-factor model, the bespoke factor loadings are equal to 1.
    /// For two-factor model, the bespoke factor loadings are first calibrated from swap rate factor
    /// loadings by Hierarchical correlation. Then based on the value of bespoke factor loadings, the bespoke volatilities are
    /// calibrated by Rebonato freezing argument.
    /// </para>
    /// <para> In the second method, one use the ATM swaption volatility cube as input and assumes that the instantaneous 
    /// volatilities are piecewise constant. For one-factor model, the bespoke factor loadings are equal to 1 and the bespoke
    /// volatilities are calibrated by Rebonato freezing argument. For two-factor model, The bespoke volatilities and factor loadings are calibrated simultaneously from the ATM 
    /// swaption volatility surface and swap rate factor loadings by Hierarchical correlation.
    /// </para>
    /// <para>For the full description of the modeling, please refer to technique paper.</para>
    /// </remarks>
    public static double[,] CalibrateForwardRateFactorLoadings(Dt asOf, Dt[] bespokeResetDates, DiscountCurve discountCurve, object calibratedVols, Dt[] swapRateEffective, Dt[] swapRateMaturities, double[][] swapRateFactorLoadings, Dt[] curveDates, bool separableVol, out VolatilityCurve[] bespokeCapletVols)
    {
      var model = CalibrationUtils.GetLiborMarketCalibrationModel(asOf, separableVol,
        swapRateEffective, swapRateMaturities, swapRateFactorLoadings);
      return CalibrateForwardRateFactorLoadings(asOf, bespokeResetDates,
        discountCurve, calibratedVols, model, curveDates, out bespokeCapletVols);
    }

    internal static double[,] CalibrateForwardRateFactorLoadings(
      Dt asOf, Dt[] bespokeResetDates, DiscountCurve discountCurve,
      object calibratedVols,
      IRateVolatilityCalibrationModel model, Dt[] curveDates,
      out VolatilityCurve[] bespokeCapletVols)
    {
      //var model = CalibrationUtils.GetLiborMarketCalibrationModel(discountCurve.AsOf, separableVol,
      //  swapRateEffective, swapRateMaturities, swapRateFactorLoadings);
      var swaptionVolSurface = calibratedVols as SwaptionVolatilityCube;
      // calibrate the IR model from the ATM swaption volatility cube. 
      if ((swaptionVolSurface != null) && (swaptionVolSurface.RateVolatilityCalibrator is RateVolatilitySwaptionMarketCalibrator))
      {
        var atmVols = swaptionVolSurface.AtmVolatilityObject as SwaptionVolatilitySpline;
        if (atmVols != null)
        {
          return atmVols.ToFwdFwdVols(discountCurve, model, bespokeResetDates, out bespokeCapletVols);
        }
      }
      double[,] factors;
      var interpolator = InterpolatorFactory(discountCurve, calibratedVols);

      // Real world (override) model calibration
      var rateModelvols = calibratedVols as RateModelParameters;
      if (rateModelvols != null)
      {
        bespokeCapletVols = model.FromSimpleVolatility(
        discountCurve, rateModelvols, interpolator.DistributionType,
        curveDates ?? EmptyArray<Dt>.Instance,
        out factors);
        return factors;
      }

      // calibrate the IR model from forward caplet volatility cube.
      var capletFwdVols = new List<Curve>();
      var capletResets = new List<Dt>();
      for (int i = 0; i < interpolator.Count; ++i)
      {
        Dt resetDate = interpolator.ResetDate(i);
        capletFwdVols.Add(GetFwdFwdVolatility(asOf, resetDate, i, interpolator));
        capletResets.Add(resetDate);
        if (resetDate > bespokeResetDates[bespokeResetDates.Length - 1])
          break;
      }

      bespokeCapletVols = model.FromCapletVolatility(
        discountCurve, capletResets.ToArray(),
        capletFwdVols.ToArray(), interpolator.DistributionType,
        bespokeResetDates, curveDates ?? EmptyArray<Dt>.Instance,
        out factors);
      return factors;
    }

    /// <summary>
    /// Produces (approximate) ATM volatility curves for libor family with reset dates targetTenors from a calibrated VolatilityObject by standard freezing arguments 
    /// </summary>
    /// <param name="discountCurve">Libor curve</param>
    /// <param name="calibratedVols">Calibrated ForwardVolatilityCube object (only at the money vols are used in the conversion)</param>
    /// <param name="separableVol">True to calibrate instantaneous vol of the form <m>\phi(T)\psi(t)</m> where <m>\phi(T)</m> is a maturity dependent constant 
    /// and <m>\psi(t)</m> is a time dependent function common to all tenors. This functional specification in a one factor framework results in a rank one 
    /// covariance matrix.  
    /// 
    /// If false, calibrated vol is of the form \phi(T)\psi(T-t). This parametric form preserves the shape of the vol surface over running time.</param>
    /// <param name="factorLoadings">Factor loadings for target libor family.</param> 
    /// <param name="volatilities">Libor vols are added to this object</param> 
    /// <remarks> The bespoke libor tenors are taken from the factorLoadings object.
    ///  The factor loadings for the standard libor family (used in calibration) are obtained by linear interpolation</remarks>
    public static void FromVolatilityObject(DiscountCurve discountCurve, object calibratedVols, bool separableVol,
                                            FactorLoadingCollection factorLoadings,
                                            VolatilityCollection volatilities)
    {
      var swaptionVolSurface = calibratedVols as SwaptionVolatilityCube;
      if ((swaptionVolSurface != null) && (swaptionVolSurface.RateVolatilityCalibrator is RateVolatilitySwaptionMarketCalibrator))
      {
        var atmVols = swaptionVolSurface.AtmVolatilityObject as SwaptionVolatilitySpline;
        if (atmVols != null)
        {
          var asOf = atmVols.AsOf;
          var bespokeResetDates = Array.ConvertAll(factorLoadings.Tenors, ten => Dt.Add(asOf, ten));
          var fl = factorLoadings.GetFactorsAt(discountCurve);
          var model = CalibrationUtils.GetLiborMarketCalibrationModel(separableVol);
          var bespokeCapletVols = atmVols.ToFwdFwdVols(discountCurve, model, bespokeResetDates, fl);
          volatilities.Add(discountCurve, bespokeCapletVols.ToArray());
          return;
        }
      }
      var interpolator = InterpolatorFactory(discountCurve, calibratedVols);
      GenerateVolCurves(discountCurve, interpolator, factorLoadings, volatilities);
    }

    #endregion

    #region SpotFx
    private static VolatilityCurve GetAtmBlackFxVol(IEnumerable<Tenor> tenors, FxCurve fxCurve, object calibratedSurface)
    {
      var volCurve = calibratedSurface as VolatilityCurve;
      if (volCurve != null)
        return volCurve;
      var volSurface = calibratedSurface as IVolatilitySurface;
      if (volSurface != null)
      {
        var retVal = new VolatilityCurve(fxCurve.AsOf)
                     {
                       Name = fxCurve.Name + "Vol",
                       DistributionType = DistributionType.LogNormal
                     };
        foreach (var dt in tenors.Select(t => Dt.Add(fxCurve.AsOf, t)))
          retVal.AddVolatility(dt, volSurface.Interpolate(dt, fxCurve.Interpolate(dt)));
        retVal.Fit();
        return retVal;
      }
      throw new ArgumentException("Invalid FX CalibratedVolatilitySurface object");
    }



    /// <summary>
    /// Calibrate spot FX volatility curve from FX option data
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fxCurve">Forward FX curve</param>
    /// <param name="calibratedSurface">FX volatility</param>
    /// <param name="volatilities">Volatility</param>
    /// <param name="factorLoadings">Factor loadings</param>
    /// <param name="logCollection">Calibration info</param>
    /// <returns>True if calibration succeded</returns>
    public static void CalibrateFxVolatility(Dt asOf, FxCurve fxCurve, object calibratedSurface, VolatilityCollection volatilities,
                                             FactorLoadingCollection factorLoadings, CalibrationUtils.CalibrationLogCollection logCollection)
    {
      var atmBlackVol = GetAtmBlackFxVol(volatilities.Tenors, fxCurve, calibratedSurface);
      var resetDates = Array.ConvertAll(volatilities.Tenors, ten => Dt.Add(asOf, ten));
      var fxVol = CalibrationUtils.CalibrateFxVolatilities(fxCurve.Name, asOf, resetDates, fxCurve.Ccy2DiscountCurve, fxCurve.Ccy1DiscountCurve,
                                                           fxCurve.SpotFxRate, atmBlackVol, volatilities.GetVolsAt(fxCurve.Ccy2DiscountCurve),
                                                           volatilities.GetVolsAt(fxCurve.Ccy1DiscountCurve),
                                                           factorLoadings.GetFactorsAt(fxCurve.Ccy2DiscountCurve),
                                                           factorLoadings.GetFactorsAt(fxCurve.Ccy1DiscountCurve),
                                                           factorLoadings.GetFactorsAt(fxCurve.SpotFxRate),
                                                           logCollection);
      volatilities.Add(fxCurve.SpotFxRate, fxVol);
    }

    /// <summary>
    /// Calibrate flat spot FX volatility and FX factor loadings by an optimization procedure in the projective 2-currency LMM framework
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fxCurve">Forward FX curve</param>
    /// <param name="calibratedSurface">At the money Black volatility</param>
    /// <param name="volatilities">Volatilities</param>
    /// <param name="factorLoadings">Factor loadings</param>
    /// <param name="logCollection">CalibrationInfo</param>
    /// <param name="initialGuess">initialGuess[0] = FX factor correlation <m>\rho^2_{fx}</m>, initialGuess[1] = spot FX volatility coefficient <m>\sigma_{fx}</m> </param>
    /// <returns>True if calibration succeded</returns>
    /// <remarks> 
    /// The projective model is based on markov projections of the libor rate processes, and requires a separable libor instantaneous volatility of the form <m>\sigma_i(t):=\psi_i \phi(t)</m>
    /// The projective two-currency LMM assumes that each libor family is driven by one factor, namely <m>M^i_t := \int_0^t \phi_{i}(s)dW^i_s, </m>  with <m>\langle W^i,W^j\rangle_t = \rho t</m> 
    /// The spot FX rate is driven by an affine combination of the driving martingales, so that <m>\sigma_{fx}(t) :=  \sigma_{fx} (\rho_{fx} \phi_1(t) + \sqrt{1 - \rho_{fx}^2}\phi_2(t)).</m> </remarks>
    public static void CalibrateSemiAnalyticFxVolatility(Dt asOf, FxCurve fxCurve, object calibratedSurface,
                                               VolatilityCollection volatilities, FactorLoadingCollection factorLoadings,
                                               CalibrationUtils.CalibrationLogCollection logCollection,
                                               double[] initialGuess)
    {
      //interpolate Fx quotes at libor reset dates
      var atmBlackVol = GetAtmBlackFxVol(volatilities.Tenors, fxCurve, calibratedSurface);
      var resetDates = Array.ConvertAll(volatilities.Tenors, ten => Dt.Add(asOf, ten));
      var fxRate = fxCurve.SpotFxRate;
      double[,] fxFactorLoadings = null;
      var fxVol = CalibrationUtils.CalibrateFxVolatilities(fxCurve.Name, asOf, resetDates, fxCurve.Ccy2DiscountCurve, fxCurve.Ccy1DiscountCurve, fxRate,
                                                           atmBlackVol, volatilities.GetVolsAt(fxCurve.Ccy2DiscountCurve),
                                                           volatilities.GetVolsAt(fxCurve.Ccy1DiscountCurve),
                                                           factorLoadings.GetFactorsAt(fxCurve.Ccy2DiscountCurve),
                                                           factorLoadings.GetFactorsAt(fxCurve.Ccy1DiscountCurve), ref fxFactorLoadings, logCollection,
                                                           initialGuess);
      volatilities.Add(fxCurve.SpotFxRate, fxVol);
      factorLoadings.AddFactors(fxCurve.SpotFxRate, fxFactorLoadings);
    }

    #endregion

    #region Forward
    
    /// <summary>
    /// Produces (approximate) ATM volatility curves for forward price family with reset dates targetTenors 
    /// </summary>
    /// <param name="fwdCurve">Fwd price curve</param>
    /// <param name="calibratedVols">Calibrated volatility object (only at the money vols are used in the conversion)</param>
    /// <param name="fwdVolatilities">Vols are added to this object</param>
    public static void FromVolatilityObject(CalibratedCurve fwdCurve, object calibratedVols, VolatilityCollection fwdVolatilities)
    {
      Dt asOf = fwdCurve.AsOf;
      var resetDates = Array.ConvertAll(fwdVolatilities.Tenors, ten => Dt.Add(asOf, ten));
      var modelParameters = calibratedVols as RateModelParameters;
      if (modelParameters != null)
      {
        var vols = resetDates.Select((dt, i) =>
                                     {
                                       double f = fwdCurve.Interpolate(dt);
                                       double sigma = modelParameters.ImpliedVolatility(RateModelParameters.Process.Projection, asOf, f, f,
                                                                                        dt,
                                                                                        dt); //atm
                                       return new VolatilityCurve(asOf, sigma)
                                              {DistributionType = DistributionType.LogNormal, Name = fwdCurve.Name + fwdVolatilities.Tenors[i] + "Vol"};
                                     });
        fwdVolatilities.Add(fwdCurve, vols.ToArray());
        return;
      }
      var volSurface = calibratedVols as IVolatilitySurface;
      if (volSurface != null)
      {
        var vols =
          resetDates.Select(
            (dt, i) =>
            new VolatilityCurve(asOf, volSurface.Interpolate(dt, fwdCurve.Interpolate(dt)))
            {DistributionType = DistributionType.LogNormal, Name = fwdCurve.Name + fwdVolatilities.Tenors[i] + "Vol"});
        fwdVolatilities.Add(fwdCurve, vols.ToArray());
      }
    }

    #endregion

    #region Spot
    private static VolatilityCurve GetAtmBlackSpotVol(IEnumerable<Tenor> tenors, IForwardPriceCurve forwardPriceCurve, object calibratedSurface)
    {
      if(forwardPriceCurve.Spot == null)
        throw new ArgumentException("ForwardPriceCurve with non null spot price expected.");
      var volCurve = calibratedSurface as VolatilityCurve;
      if (volCurve != null)
        return volCurve;
      var volSurface = calibratedSurface as IVolatilitySurface;
      if (volSurface != null)
      {
        var retVal = new VolatilityCurve(forwardPriceCurve.Spot.Spot)
                     {
                       Name = forwardPriceCurve.Spot.Name + "Vol",
                       DistributionType = DistributionType.LogNormal
                     };
        foreach (var dt in tenors.Select(t => Dt.Add(forwardPriceCurve.Spot.Spot, t)))
          retVal.AddVolatility(dt, volSurface.Interpolate(dt, forwardPriceCurve.Interpolate(dt)));
        retVal.Fit();
        return retVal;
      }
      throw new ArgumentException("Invalid FX CalibratedVolatilitySurface object");
    }


    /// <summary>
    /// Calibrate spot price volatility curve from option data
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardCurve">Forward price curve</param>
    /// <param name="calibratedSurface">Calibrated surface</param>
    /// <param name="volatilities">Volatility</param>
    /// <param name="factorLoadings">Factor loadings</param>
    ///<param name="logCollection">Calibration info</param>
    /// <returns>True if calibration succeded</returns>
    public static void CalibrateSpotVolatility(Dt asOf, CalibratedCurve forwardCurve,
                                             object calibratedSurface, VolatilityCollection volatilities,
                                             FactorLoadingCollection factorLoadings,
                                             CalibrationUtils.CalibrationLogCollection logCollection)
    {
      var spotBased = forwardCurve as IForwardPriceCurve;
      if (spotBased == null)
        throw new ArgumentException("forwardCurve implementing interface ISpotBasedForwardCurve expected");
      var atmBlackVol = GetAtmBlackSpotVol(volatilities.Tenors, spotBased, calibratedSurface);
      var liborFactors = factorLoadings.GetFactorsAt(spotBased.DiscountCurve);
      var spotFactors = factorLoadings.GetFactorsAt(spotBased.Spot);
      var liborVols = volatilities.GetVolsAt(spotBased.DiscountCurve);
      var resetDates = Array.ConvertAll(volatilities.Tenors, ten => Dt.Add(asOf, ten));
      var spotVol = CalibrationUtils.CalibrateSpotVolatilities(spotBased.Spot.Name, asOf, resetDates, spotBased.DiscountCurve, spotBased.Spot,
                                                               atmBlackVol, liborVols, liborFactors, spotFactors, logCollection);
      volatilities.Add(spotBased.Spot, spotVol);
    }

    /// <summary>
    /// Calibrate spot volatility the projective framework
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardCurve">Forward price curve</param>
    /// <param name="calibratedSurface">Calibrated surface</param>
    /// <param name="volatilities">Volatilities</param>
    /// <param name="factorLoadings">Factor loadings</param>
    /// <param name="logCollection">CalibrationInfo</param>
    /// <param name="initialGuess">Initial guess</param>
    /// <remarks> 
    /// The projective model is based on markov projections of the libor rate processes, and requires a separable libor instantaneous volatility of the form <m>\sigma_i(t):=\psi_i \phi(t)</m>
    /// The projective model assumes that libor rates and spot asset are each driven by one factor, namely <m>M_t := \int_0^t \phi(s)dW_s, </m>  and 
    /// <m>N_t := \int_0^t \sigma(s)dB_s, </m>  with <m>\langle W,B\rangle_t = \rho t</m>
    /// </remarks>
    public static void CalibrateSemiAnalyticSpotVolatility(Dt asOf, CalibratedCurve forwardCurve, object calibratedSurface, VolatilityCollection volatilities, FactorLoadingCollection factorLoadings,
                                               CalibrationUtils.CalibrationLogCollection logCollection, double[] initialGuess)
    {
      var spotBased = forwardCurve as IForwardPriceCurve;
      if (spotBased == null)
        throw new ArgumentException("forwardCurve implementing interface ISpotBasedForwardCurve expected");
      var atmBlackVol = GetAtmBlackSpotVol(volatilities.Tenors, spotBased, calibratedSurface);
      var resetDates = Array.ConvertAll(volatilities.Tenors, ten => Dt.Add(asOf, ten));
      var vol = volatilities.GetVolsAt(spotBased.DiscountCurve);
      var liborFactors = factorLoadings.GetFactorsAt(spotBased.DiscountCurve);
      double[,] spotFactorLoadings = null;
      var spotVol = CalibrationUtils.CalibrateSpotVolatilities(forwardCurve.Name, asOf, resetDates, spotBased.DiscountCurve, spotBased.Spot,
                                                               atmBlackVol, vol, liborFactors, ref spotFactorLoadings, logCollection, initialGuess);
      volatilities.Add(spotBased.Spot, spotVol);
      factorLoadings.AddFactors(spotBased.Spot, spotFactorLoadings);
    }

    #endregion

    #region Correlation

    /// <summary>
    /// Pairwise correlation between [obj1,tenor1] and [obj2,tenor2]
    /// </summary>
    /// <param name="obj1">Market variable</param>
    /// <param name="obj2">Market variable</param>
    /// <param name="tenor1">Tenor</param>
    /// <param name="tenor2">Tenor</param>
    /// <param name="factorLoadings">Factor loadings</param>
    /// <returns>Pairwise correlation</returns>
    public static double GetCorrelation(object obj1, object obj2, Tenor tenor1, Tenor tenor2,
                                        FactorLoadingCollection factorLoadings)
    {
      var fl1 = factorLoadings.GetFactorsAt(obj1);
      var fl2 = factorLoadings.GetFactorsAt(obj2);
      if (fl1 == null || fl2 == null)
        throw new ArgumentException("Factors for obj1 or obj2 not found");
      int t1 = (fl1.GetLength(0) == 1 || tenor1.IsEmpty) ? 0 : Array.FindIndex(factorLoadings.Tenors, t => t.Equals(tenor1));
      int t2 = (fl2.GetLength(0) == 1 || tenor2.IsEmpty) ? 0 : Array.FindIndex(factorLoadings.Tenors, t => t.Equals(tenor2));
      if (t1 < 0 || t2 < 0)
        throw new ArgumentException("Factors for tenor1 or tenor2 not found");
      int factorCount = factorLoadings.FactorCount;
      var fli = new double[factorCount];
      var flj = new double[factorCount];
      for (int i = 0; i < factorCount; ++i)
      {
        fli[i] = fl1[t1, i];
        flj[i] = fl2[t2, i];
      }
      return LinearAlgebra.Multiply(fli, flj);
    }

    /// <summary>
    /// Block of correlation matrix between obj1 and obj2
    /// </summary>
    /// <param name="obj1">Market variable</param>
    /// <param name="obj2">Market variable</param>
    /// <param name="factorLoadings">Factor loadings</param>
    /// <returns>Block of correlation matrix</returns>
    public static MatrixOfDoubles GetCorrelation(object obj1, object obj2, FactorLoadingCollection factorLoadings)
    {
      var fl1 = factorLoadings.GetFactorsAt(obj1);
      var fl2 = factorLoadings.GetFactorsAt(obj2);
      var m1 = new MatrixOfDoubles(fl1);
      var m2 = LinearAlgebra.Transpose(new MatrixOfDoubles(fl2));
      return LinearAlgebra.Multiply(m1, m2);
    }
    #endregion

    #region MonteCarloModelCalibration

    /// <summary>
    /// Container for factor calibration data
    /// </summary>
    public struct FactorData
    {
      /// <summary>
      /// Contruct factor data from calibration inputs
      /// </summary>
      /// <param name="type"></param>
      /// <param name="obj"></param>
      /// <param name="tenor"></param>
      /// <param name="volatility"></param>
      /// <param name="factors"></param>
      /// <param name="idiosyncraticFactor"></param>
      public FactorData(MarketVariableType type, object obj, Tenor tenor, object volatility, double[] factors,
                        Tuple<int, double> idiosyncraticFactor)
      {
        Type = type;
        Obj = obj;
        Tenor = tenor;
        Volatility = volatility;
        Factors = factors;
        IdiosyncraticFactor = idiosyncraticFactor;
      }

      /// <summary>
      /// To string function
      /// </summary>
      /// <returns></returns>
      public override string ToString()
      {
        if (Factors == null)
        {
          return "Factor Data contains a null array of factors";
        }
        else if (Factors.Length == 0)
        {
          return "Factor Data contains an empty array of factors";
        }
        else
        {
          var builder = new StringBuilder();
          var i = 0;
          builder.Append("Factors: [");
          for (; i < Factors.Length - 1; ++i)
          {
            builder.Append(Factors[i] + ", ");
          }
          builder.Append(Factors[i] + "]");
          return builder.ToString();
        }
      }

      /// <summary>
      /// 
      /// </summary>
      public readonly MarketVariableType Type;
      /// <summary>
      /// 
      /// </summary>
      public readonly object Obj;
      /// <summary>
      /// 
      /// </summary>
      public readonly Tenor Tenor;
      /// <summary>
      /// 
      /// </summary>
      public readonly object Volatility;
      /// <summary>
      /// 
      /// </summary>
      public readonly double[] Factors;
      /// <summary>
      /// 
      /// </summary>
      public readonly Tuple<int, double> IdiosyncraticFactor;
    }

    /// <summary>
    /// Calibrate factor loadings/vols by a hierarchical strategy. 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="systemicFactorCount">Number of systemic factors</param>
    /// <param name="forwardTenors">Forward tenors for underlying term structures</param>
    /// <param name="primaryVariableTypes">Primary variable types</param>
    /// <param name="primaryVariables">Primary variables</param>
    /// <param name="primaryTenors">Primary tenors</param>
    /// <param name="primaryVariableVolatility">Volatility of primary variable</param>
    /// <param name="primaryCorrelationMatrix">Pairwise correlation among primary variables</param>
    /// <param name="secondaryVariableTypes">Secondary variable types</param>
    /// <param name="secondaryVariables">Secondary market variables</param>
    /// <param name="secondaryTenors">Primary tenors</param>
    /// <param name="secondaryVariableVolatility">Volatility of secondary market variables</param>
    /// <param name="betas">Betas
    /// Since counterparties are not part of the simulated market, the factor loadings of cpty default time to driving factors does not need have unit norm.</param>
    /// <param name="volCurveDates">Vol curve dates</param>
    /// <param name="separableVol"> If true, we assume <math>\sigma_k(t) = \Phi_k\Psi_{\beta{t}}</math>. If false, we assume <math>\sigma_k(t) = \Phi_k\Psi_{k+1-\beta{t}}</math>. (Default value is true.)</param>
    /// <param name="factorLoadings">Output factor loadings</param>
    /// <param name="volatilities">Output volatilities</param>
    /// <returns></returns>
    /// <remarks>
    /// Given a number of chosen systemic factors (common driving Brownian motions),  
    /// first define primary market variables and provide a correlation matrix among the primary variables. 
    /// The primary variables are assumed entirely driven by the systemic factors, and their factor loadings 
    /// are calculated by Cholesky Decomposition and Nonlinear Least Square optimization.
    /// All the secondary assets are assumed driven by a combination of the primary assets via the <m>\beta</m> factor loadings, and, if the norm of <m>\beta</m>  
    /// is less than one, by one idiosyncratic Brownian motion.
    /// </remarks>
    public static CalibrationUtils.CalibrationLogCollection CalibrateMonteCarloModel(
      Dt asOf,
      int systemicFactorCount,
      Tenor[] forwardTenors,
      MarketVariableType[] primaryVariableTypes,
      object[] primaryVariables,
      Tenor[] primaryTenors,
      object[] primaryVariableVolatility,
      double[,] primaryCorrelationMatrix,
      MarketVariableType[] secondaryVariableTypes,
      object[] secondaryVariables,
      Tenor[] secondaryTenors,
      object[] secondaryVariableVolatility,
      double[,] betas,
      Dt[] volCurveDates,
      bool separableVol,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities
      )
    {
      var modelBuilder = GetLiborMarketVolatilityModelBuilder(separableVol);
      return CalibrateMonteCarloModel(asOf, systemicFactorCount, forwardTenors,
        primaryVariableTypes , primaryVariables,primaryTenors, primaryVariableVolatility, primaryCorrelationMatrix,
        secondaryVariableTypes, secondaryVariables, secondaryTenors, secondaryVariableVolatility,
        betas, volCurveDates, modelBuilder,
        out factorLoadings, out volatilities);
    }

    /// <summary>
    /// Calibrate factor loadings/vols by a hierarchical strategy. 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="systemicFactorCount">Number of systemic factors</param>
    /// <param name="forwardTenors">Forward tenors for underlying term structures</param>
    /// <param name="primaryVariableTypes">Primary variable types</param>
    /// <param name="primaryVariables">Primary variables</param>
    /// <param name="primaryTenors">Primary tenors</param>
    /// <param name="primaryVariableVolatility">Volatility of primary variable</param>
    /// <param name="primaryCorrelationMatrix">Pairwise correlation among primary variables</param>
    /// <param name="secondaryVariableTypes">Secondary variable types</param>
    /// <param name="secondaryVariables">Secondary market variables</param>
    /// <param name="secondaryTenors">Primary tenors</param>
    /// <param name="secondaryVariableVolatility">Volatility of secondary market variables</param>
    /// <param name="betas">Betas
    /// Since counterparties are not part of the simulated market, the factor loadings of cpty default time to driving factors does not need have unit norm.</param>
    /// <param name="volCurveDates">Vol curve dates</param>
    /// <param name="modelBuilder">Build calibration model from swap rate correlations</param>
    /// <param name="factorLoadings">Output factor loadings</param>
    /// <param name="volatilities">Output volatilities</param>
    /// <returns>CalibrationUtils.CalibrationLogCollection.</returns>
    /// <remarks>
    /// Given a number of chosen systemic factors (common driving Brownian motions),  
    /// first define primary market variables and provide a correlation matrix among the primary variables. 
    /// The primary variables are assumed entirely driven by the systemic factors, and their factor loadings 
    /// are calculated by Cholesky Decomposition and Nonlinear Least Square optimization.
    /// All the secondary assets are assumed driven by a combination of the primary assets via the <m>\beta</m> factor loadings, and, if the norm of <m>\beta</m>  
    /// is less than one, by one idiosyncratic Brownian motion.
    /// </remarks>
    public static CalibrationUtils.CalibrationLogCollection CalibrateMonteCarloModel(
      Dt asOf,
      int systemicFactorCount,
      Tenor[] forwardTenors,
      MarketVariableType[] primaryVariableTypes,
      object[] primaryVariables,
      Tenor[] primaryTenors,
      object[] primaryVariableVolatility,
      double[,] primaryCorrelationMatrix,
      MarketVariableType[] secondaryVariableTypes,
      object[] secondaryVariables,
      Tenor[] secondaryTenors,
      object[] secondaryVariableVolatility,
      double[,] betas,
      Dt[] volCurveDates,
      ModelBuilder modelBuilder,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities
      )
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Initialising calibration data for Monte Carlo simulation");
      }
      var factorNames = CCRCalibrationUtils.GetFactorNames(systemicFactorCount);

      var primaryFactorLoadings = (double[,])primaryCorrelationMatrix.Clone();
      CalibrationUtils.FactorizeCorrelationMatrix(primaryFactorLoadings, systemicFactorCount, false);
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(string.Format("Factorized Matrix {0}", CCRCalibrationUtils.FormatMatrixInCSVFormat(primaryCorrelationMatrix)));
      }
      var modules = CCRCalibrationUtils.CreateMonteCarloModules(forwardTenors, primaryVariableTypes, primaryVariables, primaryTenors, primaryVariableVolatility, 
        secondaryVariableTypes, secondaryVariables, secondaryTenors, secondaryVariableVolatility, betas, factorNames, primaryFactorLoadings);

      var retVal = new CalibrationUtils.CalibrationLogCollection(modules.Count);

      factorLoadings = new FactorLoadingCollection(factorNames.ToArray(), forwardTenors);
      volatilities = new VolatilityCollection(forwardTenors);

      var moduleGroups = modules.GroupBy(v => v.Obj).ToList();
      var forwardTenorDates = forwardTenors.Select(t => Dt.Add(asOf, t)).ToArray();

      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Calibration data for Monte Carlo simulation initialsed successfully");
      }

      CCRCalibrationUtils.ProcessDiscountCurves(asOf, volCurveDates, modelBuilder, factorLoadings, volatilities, moduleGroups, factorNames,
            forwardTenorDates);
      CCRCalibrationUtils.ProcessFxCurves(asOf, factorLoadings, volatilities, moduleGroups, factorNames,
            forwardTenorDates, retVal);
      CCRCalibrationUtils.ProcessCalibratedCurves(asOf, factorLoadings, volatilities, moduleGroups, factorNames,
            forwardTenorDates, retVal);
      CCRCalibrationUtils.ProcessSurvivalCurves(asOf, factorLoadings, volatilities, moduleGroups, factorNames,
            forwardTenorDates, retVal);

      return retVal;
    }

    /// <summary>
    /// Calibrate factor loadings/vols by a hierarchical strategy. 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="factorNames">Input list of systemic factor names. Idiosyncratic names will be added to list by function</param>
    /// <param name="forwardTenors">Forward tenors for underlying term structures</param>
    /// <param name="primaryVariableTypes">Primary variable types</param>
    /// <param name="primaryVariables">Primary variables</param>
    /// <param name="primaryTenors">Primary tenors</param>
    /// <param name="primaryVariableVolatility">Volatility of primary variable</param>
    /// <param name="primaryCorrelationMatrix">Pairwise correlation among primary variables</param>
    /// <param name="secondaryVariableTypes">Secondary variable types</param>
    /// <param name="secondaryVariables">Secondary market variables</param>
    /// <param name="secondaryTenors">Primary tenors</param>
    /// <param name="secondaryVariableVolatility">Volatility of secondary market variables</param>
    /// <param name="betas">Betas
    /// Since counterparties are not part of the simulated market, the factor loadings of cpty default time to driving factors does not need have unit norm.</param>
    /// <param name="factorLoadings">Output factor loadings</param>
    /// <param name="volatilities">Output volatilities</param>
    /// <returns></returns>
    /// <remarks>
    /// Given a number of chosen systemic factors (common driving Brownian motions),  
    /// first define primary market variables and provide a correlation matrix among the primary variables. 
    /// The primary variables are assumed entirely driven by the systemic factors, and their factor loadings 
    /// are calculated by Cholesky Decomposition and Nonlinear Least Square optimization.
    /// All the secondary assets are assumed driven by a combination of the primary assets via the <m>\beta</m> factor loadings, and, if the norm of <m>\beta</m>  
    /// is less than one, by one idiosyncratic Brownian motion.
    /// </remarks>
    public static IList<FactorData> CalibrateMonteCarloModelIncremental(
      Dt asOf,
      List<string> factorNames,
      Tenor[] forwardTenors,
      MarketVariableType[] primaryVariableTypes,
      object[] primaryVariables,
      Tenor[] primaryTenors,
      object[] primaryVariableVolatility,
      double[,] primaryCorrelationMatrix,
      MarketVariableType[] secondaryVariableTypes,
      object[] secondaryVariables,
      Tenor[] secondaryTenors,
      object[] secondaryVariableVolatility,
      double[,] betas,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities
      )
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Initialising calibration data for Monte Carlo simulation");
      }
      var systemicFactorCount = factorNames.Count;

      var primaryFactorLoadings = (double[,])primaryCorrelationMatrix.Clone();
      CalibrationUtils.FactorizeCorrelationMatrix(primaryFactorLoadings, systemicFactorCount, false);
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(string.Format("Factorized Matrix {0}", CCRCalibrationUtils.FormatMatrixInCSVFormat(primaryCorrelationMatrix)));
      }
      var modules = CCRCalibrationUtils.CreateMonteCarloModules(forwardTenors, primaryVariableTypes, primaryVariables, primaryTenors, primaryVariableVolatility,
        secondaryVariableTypes, secondaryVariables, secondaryTenors, secondaryVariableVolatility, betas, factorNames, primaryFactorLoadings);

      factorLoadings = new FactorLoadingCollection(factorNames.ToArray(), forwardTenors);
      volatilities = new VolatilityCollection(forwardTenors);
      return modules;
    }



    /// <summary>
    /// Generate names of systemic factors
    /// </summary>
    public static List<string> GetFactorNames(int systemicFactorCount)
    {
      var factorNames = new List<string>();
      for (var i = 0; i < systemicFactorCount; ++i)
        factorNames.Add(string.Format("SystemicFactor{0}", i));
      return factorNames;
    }

    private static string FormatMatrixInCSVFormat(double[,] array)
    {
      if (array == null)
      {
        return "is Null";
      }
      else if (array.GetLength(0) == 0 || array.GetLength(1) == 0)
      {
        return "is empty";        
      }
      else
      {
        var builder = new StringBuilder();
        builder.Append("[");
        for (var i = 0; i < array.GetLength(0); ++i)
        {
          builder.Append("[");
          var j = 0;
          for (; j < array.GetLength(1) - 1; ++j)
          {
            builder.Append(array[i, j] + ",");
          }
          builder.Append(array[i, j] + "]");
        }
        builder.Append("]");
        return builder.ToString();
      }
    }

    private static List<FactorData> CreateMonteCarloModules(
      Tenor[] forwardTenors,
      MarketVariableType[] primaryVariableTypes,
      object[] primaryVariables,
      Tenor[] primaryTenors,
      object[] primaryVariableVolatility,
      MarketVariableType[] secondaryVariableTypes,
      object[] secondaryVariables,
      Tenor[] secondaryTenors,
      object[] secondaryVariableVolatility,
      double[,] betas,
      List<string> factorNames,
      double[,] primaryFactorLoadings)
    {
      // create modules
      var modules = new List<FactorData>();
      modules.AddRange(primaryVariables.Select((o, i) =>
      {
        var factorData = new FactorData(primaryVariableTypes[i], o, primaryTenors[i].IsEmpty ? forwardTenors.Last() : primaryTenors[i], primaryVariableVolatility[i],
          primaryFactorLoadings.Row(i), null);

        if (Logger.IsDebugEnabled)
        {
          Logger.Debug(string.Format("Adding new Factor Data Element, ObjectId: {0}, {1}", CCRCalibrationUtils.ObjectId(factorData.Obj, factorData.Tenor),
            factorData.ToString()));
        }
        return factorData;
      }));

      if (secondaryVariables != null)
      {
        modules.AddRange(secondaryVariables.Select((o, i) =>
        {
          var fl = Multiply(betas.Row(i), primaryFactorLoadings);
          var norm = fl.Norm();
          Tuple<int, double> idiosyncratic = null;
          if (o is SurvivalCurve && secondaryVariableTypes[i] == MarketVariableType.CounterpartyCreditSpread)
          //not part of environment, i.e. counteparty                                                     
          {
            if (norm > 1.0)
              fl = fl.Scale(norm);
            var factorData = new FactorData(secondaryVariableTypes[i], o,
              secondaryTenors[i].IsEmpty ? forwardTenors.Last() : secondaryTenors[i],
              secondaryVariableVolatility[i], fl, null);

            if (Logger.IsDebugEnabled)
            {
              Logger.Debug(string.Format("Adding new Factor Data Element, Object Id: {0}, {1}", ObjectId(factorData.Obj, factorData.Tenor),
                factorData.ToString()));
            }

            return factorData;
          }
          if (norm < 0.99)
          {
            factorNames.Add(ObjectId(secondaryVariables[i], secondaryTenors[i]));
            idiosyncratic = new Tuple<int, double>(factorNames.Count - 1, Math.Sqrt(1.0 - norm * norm));
          }
          else
            fl = fl.Scale(norm);

          var factorDataWithIdiosyncraticValue = new FactorData(secondaryVariableTypes[i], o,
            secondaryTenors[i].IsEmpty ? forwardTenors.Last() : secondaryTenors[i],
            secondaryVariableVolatility[i], fl, idiosyncratic);

          if (Logger.IsDebugEnabled)
          {
            Logger.Debug(string.Format("Adding new Factor Data Element, Object Id: {0}, {1}",
              ObjectId(factorDataWithIdiosyncraticValue.Obj, factorDataWithIdiosyncraticValue.Tenor), factorDataWithIdiosyncraticValue.ToString()));
            if (null != idiosyncratic)
            {
              Logger.Debug(string.Format("Adding Idiosyncratic Factor of {0} for {1}", idiosyncratic.ToString(),
                ObjectId(factorDataWithIdiosyncraticValue.Obj, factorDataWithIdiosyncraticValue.Tenor)));
            }
          }

          return factorDataWithIdiosyncraticValue;
        }));
      }
      return modules;
    }

    /// <summary>
    /// Attempt calibration of factorized vols for a specific discount curve
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="volCurveDates"></param>
    /// <param name="separableVol"></param>
    /// <param name="factorLoadings"></param>
    /// <param name="volatilities"></param>
    /// <param name="modules"></param>
    /// <param name="factorNames"></param>
    /// <param name="discountCurve"></param>
    /// <returns></returns>
    public static bool TryCalibrateDiscountCurve(
      Dt asOf,
      Dt[] volCurveDates,
      bool separableVol,
      FactorLoadingCollection factorLoadings,
      VolatilityCollection volatilities,
      IList<FactorData> modules,
      string[] factorNames,
      DiscountCurve discountCurve
      )
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(string.Format("Attemptiong Calibration for Curve {0}", discountCurve.Name));
      }
      var vm = GetRateCalibrationModel(separableVol, asOf,
        modules.Where(v => v.Obj == discountCurve).OrderBy(d => d.Tenor).ToList(),
        factorNames);
      if (vm.IsEmpty)
      {
        Logger.Debug(string.Format("No Volatilities found for Curve {0}", discountCurve.Name));
        return false;
      }
      return TryCalibrateDiscountCurve(asOf, volCurveDates, vm.Model, vm.Volatility,
        factorLoadings, volatilities, factorNames, discountCurve);
    }

    private static bool TryCalibrateDiscountCurve(
      Dt asOf,
      Dt[] volCurveDates,
      IRateVolatilityCalibrationModel model,
      object vol,
      FactorLoadingCollection factorLoadings,
      VolatilityCollection volatilities,
      string[] factorNames,
      DiscountCurve discountCurve)
    {
      try
      {
        var forwardTenorDates = factorLoadings.Tenors.Select(t => Dt.Add(asOf, t)).ToArray();

        VolatilityCurve[] bespokeVols;
        var fl = CalibrateForwardRateFactorLoadings(asOf, forwardTenorDates,
          discountCurve, vol, model, volCurveDates, out bespokeVols);
        volatilities.Add(discountCurve, bespokeVols);
        factorLoadings.AddFactors(discountCurve, fl.Resize(fl.GetLength(0), factorNames.Length));
      }
      catch (Exception e)
      {
        Logger.ErrorFormat("Calibration failed for Curve {0}. Returned error {1}", discountCurve.Name, e.Message);
        return false;
      }
      return true;
    }

    private static void ProcessDiscountCurves(
      Dt asOf,
      Dt[] volCurveDates,
      ModelBuilder buildModel,
      FactorLoadingCollection factorLoadings,
      VolatilityCollection volatilities,
      List<IGrouping<object, FactorData>> moduleGroups,
      List<string> factorNames,
      Dt[] forwardTenorDates)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Loading Discount Curves");
      }
      foreach (var moduleGroup in moduleGroups.Where(m => m.Key is DiscountCurve))
      {
        var data = moduleGroup.OrderBy(d => d.Tenor).ToArray();
        var o = (DiscountCurve)moduleGroup.Key;
        if (Logger.IsDebugEnabled)
        {
          Logger.Debug(string.Format("Calibrating for Curve {0}", o.Name));
        }
        var vm = buildModel(asOf, data, factorNames);
        if (vm.IsEmpty)
        {
          throw new ArgumentException(string.Format("Must provide vol object for {0}", o.Name));
        }
        VolatilityCurve[] bespokeVols;
        var fl = CalibrateForwardRateFactorLoadings(asOf, forwardTenorDates,
          o, vm.Volatility, vm.Model, volCurveDates, out bespokeVols);
        volatilities.Add(o, bespokeVols);
        factorLoadings.AddFactors(o, fl.Resize(fl.GetLength(0), factorNames.Count));
      }
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Discount Curves Loaded Successfully");
      }
    }

    /// <summary>
    /// Attempt calibration of single FxCurve
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="factorLoadings"></param>
    /// <param name="volatilities"></param>
    /// <param name="modules"></param>
    /// <param name="factorNames"></param>
    /// <param name="fxCurve"></param>
    /// <param name="retVal"></param>
    /// <returns></returns>
    public static bool TryCalibrateFxCurve(
     Dt asOf,
     FactorLoadingCollection factorLoadings,
     VolatilityCollection volatilities,
     IList<FactorData> modules,
     string[] factorNames,
     FxCurve fxCurve,
     CalibrationUtils.CalibrationLogCollection retVal)
    {
      var data = modules.Where(v => v.Obj == fxCurve).OrderBy(d => d.Tenor);
      var forwardTenorDates = factorLoadings.Tenors.Select(t => Dt.Add(asOf, t)).ToArray();
     
        if (Logger.IsDebugEnabled)
        {
          Logger.Debug(string.Format("Calibrating for Curve {0}", fxCurve.Name));
        }
      if (data.Any(d => d.Volatility != null))
      {
        var fl = data.Select(d =>
        {
          var f = d.Factors.Resize(factorNames.Length);
          var idiosyncratic = d.IdiosyncraticFactor;
          if (idiosyncratic != null)
            f[idiosyncratic.Item1] = idiosyncratic.Item2;
          return f;
        }).ToArray().ToMatrix();
        if (data.First().Type == MarketVariableType.ForwardFx || fxCurve.SpotFxRate == null)
        {
          try
          {
            fl = CalibrationUtils.InterpolateFactorLoadings(asOf, fl, data.Select(d => Dt.Add(asOf, d.Tenor)).ToArray(), forwardTenorDates);
            factorLoadings.AddFactors(fxCurve.GetComponentCurves<FxForwardCurve>(null)[0], fl);
            FromVolatilityObject(fxCurve.GetComponentCurves<FxForwardCurve>(null)[0], data.First(d => d.Volatility != null).Volatility, volatilities);
          }
          catch (Exception e)
          {
            Logger.ErrorFormat("Calibration failed for FxCurve {0}. {1}", fxCurve.Name, e.Message);
            return false;
          }
          return true;
          
        }
        else if (data.First().Type == MarketVariableType.SpotFx)
        {
          try
          {
            factorLoadings.AddFactors(fxCurve.SpotFxRate, fl);
            var volCurve = data.First(d => d.Volatility != null).Volatility;
            CalibrateFxVolatility(asOf, fxCurve, volCurve, volatilities, factorLoadings, retVal);
          }
          catch (Exception e)
          {
            Logger.ErrorFormat("Calibration failed for FxCurve {0}. {1}", fxCurve.Name, e.Message);
            return false;
          }
          return true;
        }
        else
        {
          Logger.ErrorFormat("Object of type FxCurve is incompatible to {0} MarketVariableType", data.First().Type);
          return false;
        }
      }
      else
      {
        Logger.ErrorFormat("Must provide vol object for {0}", fxCurve.Name);
        return false;
      }
      
    }

    private static void ProcessFxCurves(
      Dt asOf,
      FactorLoadingCollection factorLoadings,
      VolatilityCollection volatilities,
      List<IGrouping<object, FactorData>> moduleGroups,
      List<string> factorNames,
      Dt[] forwardTenorDates,
      CalibrationUtils.CalibrationLogCollection retVal)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Loading Fx Curves");
      }
      foreach (var moduleGroup in moduleGroups.Where(m => m.Key is FxCurve))
      {
        var data = moduleGroup.OrderBy(d => d.Tenor).ToArray();
        var o = (FxCurve)moduleGroup.Key;
        if (Logger.IsDebugEnabled)
        {
          Logger.Debug(string.Format("Calibrating for Curve {0}", o.Name));
        }
        if (data.Any(d => d.Volatility != null))
        {
          var fl = data.Select(d =>
          {
            var f = d.Factors.Resize(factorNames.Count);
            var idiosyncratic = d.IdiosyncraticFactor;
            if (idiosyncratic != null)
              f[idiosyncratic.Item1] = idiosyncratic.Item2;
            return f;
          }).ToArray().ToMatrix();
          if (data.First().Type == MarketVariableType.ForwardFx || o.SpotFxRate == null)
          {
            fl = CalibrationUtils.InterpolateFactorLoadings(asOf, fl, data.Select(d => Dt.Add(asOf, d.Tenor)).ToArray(), forwardTenorDates);
            factorLoadings.AddFactors(o.GetComponentCurves<FxForwardCurve>(null)[0], fl);
            FromVolatilityObject(o.GetComponentCurves<FxForwardCurve>(null)[0], data.First(d => d.Volatility != null).Volatility, volatilities);
          }
          else if (data.First().Type == MarketVariableType.SpotFx)
          {
            factorLoadings.AddFactors(o.SpotFxRate, fl);
            var volCurve = data.First(d => d.Volatility != null).Volatility;
            CalibrateFxVolatility(asOf, o, volCurve, volatilities, factorLoadings, retVal);
          }
          else
            throw new ArgumentException(string.Format("Object of type FxCurve is incompatible to {0} MarketVariableType", data.First().Type));
        }
        else throw new ArgumentException(string.Format("Must provide vol object for {0}", o.Name));
      }
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Fx Curves Loaded Successfully");
      }
    }

    /// <summary>
    /// Attempt to calibrate factorized vols for a single fwd curve
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="factorLoadings"></param>
    /// <param name="volatilities"></param>
    /// <param name="modules"></param>
    /// <param name="factorNames"></param>
    /// <param name="fwdCurve"></param>
    /// <param name="retVal"></param>
    /// <returns></returns>
    public static bool TryCalibrateForwardCurve(
    Dt asOf,
    FactorLoadingCollection factorLoadings,
    VolatilityCollection volatilities,
    IList<FactorData> modules,
    string[] factorNames,
    CalibratedCurve fwdCurve,
    CalibrationUtils.CalibrationLogCollection retVal)
    {
      var data = modules.Where(v => v.Obj == fwdCurve).OrderBy(d => d.Tenor);
      var forwardTenorDates = factorLoadings.Tenors.Select(t => Dt.Add(asOf, t)).ToArray();
     
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(string.Format("Calibrating for Curve {0}", fwdCurve.Name));
      }
      if (data.Any(d => d.Volatility != null))
      {
        var fl = data.Select(d =>
        {
          var f = d.Factors.Resize(factorNames.Length);
          var idiosyncratic = d.IdiosyncraticFactor;
          if (idiosyncratic != null)
            f[idiosyncratic.Item1] = idiosyncratic.Item2;
          return f;
        }).ToArray().ToMatrix();
        if (data.First().Type == MarketVariableType.ForwardPrice)
        {
          try
          {
            fl = CalibrationUtils.InterpolateFactorLoadings(asOf, fl, data.Select(d => Dt.Add(asOf, d.Tenor)).ToArray(), forwardTenorDates);
            factorLoadings.AddFactors(fwdCurve, fl);
            FromVolatilityObject(fwdCurve, data.First(d => d.Volatility != null).Volatility, volatilities);
            return true;
          }
          catch (Exception e)
          {
            Logger.ErrorFormat("Calibration failed for fwd curve {0}. {1}", fwdCurve.Name, e.Message);
            return false;
          }
        }
        else if (data.First().Type == MarketVariableType.SpotPrice)
        {
          var spotBased = fwdCurve as IForwardPriceCurve;
          if (spotBased == null || spotBased.Spot == null)
          {
            Logger.ErrorFormat("{0} expected to implement ISpotBasedForwardCurve and have observable spot price", fwdCurve.Name);
            return false;
          }
          try
          {
            factorLoadings.AddFactors(spotBased.Spot, fl);
            var volCurve = data.First(d => d.Volatility != null).Volatility;
            CalibrateSpotVolatility(asOf, fwdCurve, volCurve, volatilities, factorLoadings, retVal);
            return true;
          }
          catch (Exception e)
          {
            Logger.ErrorFormat("Calibration failed for fwd curve {0}. {1}", fwdCurve.Name, e.Message);
            return false;
          }
          
        }
        else
        {
          Logger.ErrorFormat("Object of type {0} is incompatible to {1} MarketVariableType", fwdCurve.GetType(), data.First().Type);
          return false;
        }
      }
      else
      {
         Logger.ErrorFormat("Must provide vol object for {0}", fwdCurve.Name);
         return false;
      }
      
    }

    private static void ProcessCalibratedCurves(
      Dt asOf,
      FactorLoadingCollection factorLoadings,
      VolatilityCollection volatilities,
      List<IGrouping<object, FactorData>> moduleGroups,
      List<string> factorNames,
      Dt[] forwardTenorDates,
      CalibrationUtils.CalibrationLogCollection retVal)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Loading Calibrated Curves");
      }
      foreach (
        var moduleGroup in
          moduleGroups.Where(
            m => (m.Key is CalibratedCurve) && (m.First().Type == MarketVariableType.SpotPrice || m.First().Type == MarketVariableType.ForwardPrice)))
      {
        var data = moduleGroup.OrderBy(d => d.Tenor).ToArray();
        var o = (CalibratedCurve)moduleGroup.Key;
        if (Logger.IsDebugEnabled)
        {
          Logger.Debug(string.Format("Calibrating for Curve {0}", o.Name));
        }
        if (data.Any(d => d.Volatility != null))
        {
          var fl = data.Select(d =>
          {
            var f = d.Factors.Resize(factorNames.Count);
            var idiosyncratic = d.IdiosyncraticFactor;
            if (idiosyncratic != null)
              f[idiosyncratic.Item1] = idiosyncratic.Item2;
            return f;
          }).ToArray().ToMatrix();
          if (data.First().Type == MarketVariableType.ForwardPrice)
          {
            if (o != null)
            {
              fl = CalibrationUtils.InterpolateFactorLoadings(asOf, fl, data.Select(d => Dt.Add(asOf, d.Tenor)).ToArray(), forwardTenorDates);
              factorLoadings.AddFactors(o, fl);
              FromVolatilityObject(o, data.First(d => d.Volatility != null).Volatility, volatilities);
            }
          }
          else if (data.First().Type == MarketVariableType.SpotPrice)
          {
            var spotBased = o as IForwardPriceCurve;
            if (spotBased == null || spotBased.Spot == null)
              throw new ArgumentException(string.Format("{0} expected to implement ISpotBasedForwardCurve and have observable spot price", o.Name));
            factorLoadings.AddFactors(spotBased.Spot, fl);
            var volCurve = data.First(d => d.Volatility != null).Volatility;
            CalibrateSpotVolatility(asOf, o, volCurve, volatilities, factorLoadings, retVal);
          }
          else
            throw new ArgumentException(string.Format("Object of type {0} is incompatible to {1} MarketVariableType", o.GetType(), data.First().Type));
        }
        else throw new ArgumentException(string.Format("Must provide vol object for {0}", o.Name));
      }
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Calibrated Curves Loaded Successfully");
      }
    }

    /// <summary>
    /// Attempt to calibrate factorized vols for single survival curve
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="factorLoadings"></param>
    /// <param name="volatilities"></param>
    /// <param name="modules"></param>
    /// <param name="factorNames"></param>
    /// <param name="survivalCurve"></param>
    /// <param name="retVal"></param>
    /// <returns></returns>
    public static bool TryCalibrateSurvivalCurve(
     Dt asOf,
     FactorLoadingCollection factorLoadings,
     VolatilityCollection volatilities,
     IList<FactorData> modules,
     string[] factorNames,
     SurvivalCurve survivalCurve,
     CalibrationUtils.CalibrationLogCollection retVal)
    {
      var data = modules.Where(v => v.Obj == survivalCurve).OrderBy(d => d.Tenor);
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(string.Format("Calibrating for Curve {0}", survivalCurve.Name));
      }
      if (data.Any(d => d.Volatility != null))
      {
        var fl = data.Select(d =>
        {
          var f = d.Factors.Resize(factorNames.Length).Scale(-1.0);
          var idiosyncratic = d.IdiosyncraticFactor;
          if (idiosyncratic != null)
            f[idiosyncratic.Item1] = idiosyncratic.Item2;
          return f;
        }).ToArray().ToMatrix();
        factorLoadings.AddFactors(survivalCurve, fl);
        var volCurve = data.First(d => d.Volatility != null).Volatility as VolatilityCurve;
        var tenor = data.First().Tenor;
        if (volCurve == null)
        {
          Logger.ErrorFormat("Volatility of type VolatilityCurve expected for CreditSpread MarketVariableType");
          return false;
        }
        var discountCurve = (survivalCurve.SurvivalCalibrator != null && survivalCurve.SurvivalCalibrator.DiscountCurve != null)
          ? survivalCurve.SurvivalCalibrator.DiscountCurve
          : modules.First(m => m.Obj is DiscountCurve && ((DiscountCurve)m.Obj).Ccy == survivalCurve.Ccy).Obj as DiscountCurve;
        if (discountCurve == null)
        {
          Logger.ErrorFormat("DiscountCurve for Credit {0} not found", survivalCurve.Name);
          return false;
        }
        try
        {
          CalibrateCreditVolatility(asOf, volCurve, tenor, survivalCurve, discountCurve, volatilities, retVal, 40);
          return true;
        }
        catch (Exception e)
        {
          Logger.ErrorFormat("Calibration failed for for Credit {0}. {1}", survivalCurve.Name, e.Message);
          return false;
        }
        
      }
      else
      {
        Logger.ErrorFormat("Must provide vol object for {0}", survivalCurve.Name);
        return false;
      }
    }

    private static void ProcessSurvivalCurves(
      Dt asOf,
      FactorLoadingCollection factorLoadings,
      VolatilityCollection volatilities,
      List<IGrouping<object, FactorData>> moduleGroups,
      List<string> factorNames,
      Dt[] forwardTenorDates,
      CalibrationUtils.CalibrationLogCollection retVal)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Loading Survival Curves");
      }
      foreach (var moduleGroup in moduleGroups.Where(m => m.Key is SurvivalCurve))
      {
        var data = moduleGroup.OrderBy(d => d.Tenor).ToArray();
        var o = (SurvivalCurve)moduleGroup.Key;
        if (Logger.IsDebugEnabled)
        {
          Logger.Debug(string.Format("Calibrating for Curve {0}", o.Name));
        }
        if (data.Any(d => d.Volatility != null))
        {
          var fl = data.Select(d =>
          {
            var f = d.Factors.Resize(factorNames.Count).Scale(-1.0);
            var idiosyncratic = d.IdiosyncraticFactor;
            if (idiosyncratic != null)
              f[idiosyncratic.Item1] = idiosyncratic.Item2;
            return f;
          }).ToArray().ToMatrix();
          factorLoadings.AddFactors(o, fl);
          var volCurve = data.First(d => d.Volatility != null).Volatility as VolatilityCurve;
          var tenor = data.First().Tenor;
          if (volCurve == null)
            throw new ArgumentException("Volatility of type VolatilityCurve expected for CreditSpread MarketVariableType");
          var discountCurve = (o.SurvivalCalibrator != null && o.SurvivalCalibrator.DiscountCurve != null)
                                ? o.SurvivalCalibrator.DiscountCurve
                                : moduleGroups.First(m => m.Key is DiscountCurve && ((DiscountCurve)m.Key).Ccy == o.Ccy).Key as DiscountCurve;
          if (discountCurve == null)
            throw new ArgumentException(string.Format("DiscountCurve for Credit {0} not found", o.Name));
          CalibrateCreditVolatility(asOf, volCurve, tenor, o, discountCurve, volatilities, retVal, 40);
        }
        else throw new ArgumentException(string.Format("Must provide vol object for {0}", o.Name));
      }
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Survival Curves Loaded Successfully");
      }
    }

    #endregion

    #region SemiAnalyticIRCalibration
    /// <summary>
    /// Calibrate factor loadings and volatilities of one or two-factor IR model.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Forward tenors</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="swapTenors">Maturity tenors of proxy swap rates.</param>
    /// <param name="capletVolatility">Caplet volatilities</param>
    /// <param name="rho">Pairwise correlation between swap rates</param>
    /// <param name="counterpartyCurves">Counterparty reference names</param>
    /// <param name="counterpartyBetas">Beta of credit spread of counterparty to proxy swap rates</param>
    /// <param name="cptySpreadTenors">Tenors for underlying spread options</param>
    /// <param name="counterpartyBlackVols">Black volatility of cpty credit spread</param>
    /// <param name="separableVol"> If true, we assume <math>\sigma_k(t) = \Phi_k\Psi_{\beta{t}}</math>. If false, we assume <math>\sigma_k(t) = \Phi_k\Psi_{k+1-\beta{t}}</math>. (Default value is true.)</param>
    /// <param name="factorLoadings">Overwritten by factor loadings</param>
    /// <param name="volatilities">Overwritten by volatilities</param>
    /// <remarks>
    /// In the definition of the market environment, a set of forward tenors, <m>T_0, T_1, \cdots, T_n, </m> 
    /// are provided. Let <m>F_i(t)</m> denote the forward LIBOR rate <m>F(t; T_{i-1}, T_i)</m> from <m>T_{i-1}</m>
    /// to <m>T_i</m> as of time <m>t</m>. Assume the forward rates either satisfy Lognormal distribution:
    /// <math>
    /// d F_i(t) = \mu_i(t)F_i(t) dt + \sigma_i(t)F_i(t) \sum_{j=1}^d \varphi_{ij} dW_j(t), \ \ \ \ \ i = 1, \cdots, n 
    /// </math>
    /// or Normal distribution:
    /// <math>
    /// d F_i(t) = \mu_i(t) dt + \sigma_i(t) \sum_{j=1}^d \varphi_{ij} dW_j(t), \ \ \ \ \ i = 1, \cdots, n 
    /// </math>
    /// , where <m>d</m> is the number of factors, and <m>\{\varphi_{ij}\}</m> are factor loadings with 
    /// <m>\sum_{j=1}^d \varphi_{ij}^2 = 1.</m> 
    /// <para>calibrate the factor loadings <m>\varphi_{ij}</m> and volatilities <m>\sigma_i</m> by two methods
    /// which have the assumption for bespoke factor loadings but different assumptions for bespoke volatilities. In the
    /// first method, the forward IR model is calibrated with input forward caplet volatility cube by assuming that each forward
    /// rate has constant instantaneous volatilities. For one-factor model, the bespoke factor loadings are equal to 1.
    /// For two-factor model, the bespoke factor loadings are first calibrated from swap rate factor
    /// loadings by Hierarchical correlation. Then based on the value of bespoke factor loadings, the bespoke volatilities are
    /// calibrated by Rebonato freezing argument.
    /// </para>
    /// <para> In the second method, one use the ATM swaption volatility cube as input and assumes that the instantaneous 
    /// volatilities are piecewise constant. For one-factor model, the bespoke factor loadings are equal to 1 and the bespoke
    /// volatilities are calibrated by Rebonato freezing argument. For two-factor model, The bespoke volatilities and factor loadings are calibrated simultaneously from the ATM 
    /// swaption volatility surface and swap rate factor loadings by Hierarchical correlation.
    /// </para>
    /// <para>For the full description of the modeling, please refer to  technique paper.</para>
    /// </remarks>
    public static CalibrationUtils.CalibrationLogCollection CalibrateSemiAnalyticIrModel(
      Dt asOf,
      Tenor[] forwardTenors,
      DiscountCurve discountCurve,
      Tenor[] swapTenors,
      object capletVolatility,
      double rho,
      SurvivalCurve[] counterpartyCurves,
      double[,] counterpartyBetas,
      Tenor[] cptySpreadTenors,
      VolatilityCurve[] counterpartyBlackVols,
      bool separableVol,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities)
    {
      if (swapTenors.Length > 2)
        throw new ArgumentException("At most two swapTenors allowed in SemiAnalytic model");
      var factorNames = swapTenors.Select(t => String.Concat(discountCurve.Ccy, t)).ToArray();
      rho = Math.Max(Math.Min(rho, 1.0), -1.0);
      factorLoadings = new FactorLoadingCollection(factorNames, forwardTenors);
      volatilities = new VolatilityCollection(forwardTenors);
      var primaryFactorLoadings = (swapTenors.Length == 1) ? new[,] {{1.0}} : new[,] {{1.0, 0.0}, {rho, Math.Sqrt(1.0 - rho * rho)}};
      var forwardTenorDates = forwardTenors.Select(t => Dt.Add(asOf, t)).ToArray();
      var model = CalibrationUtils.GetLiborMarketCalibrationModel(asOf, separableVol,
        swapTenors.Select(d => asOf).ToArray(),
        swapTenors.Select(d => Dt.Add(asOf, d)).ToArray(),
        swapTenors.Select((t, i) => primaryFactorLoadings.Row(i)).ToArray());
      VolatilityCurve[] bespokeVols;
      var retVal = new CalibrationUtils.CalibrationLogCollection(counterpartyCurves.Length);
      var liborFl = CalibrateForwardRateFactorLoadings(asOf, forwardTenorDates, discountCurve, capletVolatility, model,
                                                       null, out bespokeVols);    
      volatilities.Add(discountCurve, bespokeVols);
      factorLoadings.AddFactors(discountCurve, liborFl);
      for (int i = 0; i < counterpartyCurves.Length; ++i)
      {
        var sc = counterpartyCurves[i];
        if (sc == null)
          continue;
        var dc = (sc.SurvivalCalibrator != null && sc.SurvivalCalibrator.DiscountCurve != null) ? sc.SurvivalCalibrator.DiscountCurve : discountCurve;
        var fl = Multiply(counterpartyBetas.Row(i), primaryFactorLoadings).Scale(-1.0);
        double norm = fl.Norm();
        if (norm > 1.0)
          fl = fl.Scale(norm);
        factorLoadings.AddFactors(sc, new[] {fl}.ToMatrix());
        CalibrateCreditVolatility(asOf, counterpartyBlackVols[i], cptySpreadTenors[i], sc, dc, volatilities, retVal, 20);
      }
      return retVal;
    }

    #endregion

    #region SemiAnalyticCreditCalibration
    /// <summary>
    /// Calibrate semi-analytic credit model to index option implied vols
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Term structure forward tenors</param>
    /// <param name="creditIndex">Driving credit indices</param>
    /// <param name="rho">Credit spread correlation between driving credit indexes</param>
    /// <param name="credits">Underlying credits  </param>
    /// <param name="spreadTenors">Tenors for underlying spread options</param>
    /// <param name="atmVolCurves">ATM volatilities</param>
    /// <param name="creditBetas">Betas of credit spreads to index spreads</param>
    /// <param name="cptyCredits">Counterparty and booking entity curve</param> 
    /// <param name="cptySpreadTenors">Tenors for underlying spread options</param>
    /// <param name="cptyAtmVolCurves">ATM volatilities</param>
    /// <param name="cptyBetas">Betas of counterparty credit spreads to index credit spreads</param>
    /// <param name="volatilities">Overwritten by volatilities of underlying index</param>
    /// <param name="factorLoadings">Overwritten by factor loadings for each reference credit</param>
    /// <returns>Calibration logs</returns>
    /// <remarks>  
    /// The dynamic copula model assumes that default time i is driven by the terminal value of 
    /// <m>M^i_t := \int_0^t \frac{\lambda(s)}{1 + \int_0^s\lambda^2(u)\,du}dW^i_s</m> with <m>\lambda_i(t)>0</m> 
    /// as follows <m>\tau_i := F_i^{-1}\left(\Phi(\sqrt{1 - \rho^2_i}Z_i + \rho_i M^i_\infty\right),</m> where <m>F_i(\cdot)</m> is the marginal distribution of <m>\tau_i</m>,  
    /// <m>\Phi(\cdot)</m> is the standard gaussian cdf, <m>Z_i</m> are standard gaussians random variables and <m>W^i_t</m> correlated brownian motions
    /// </remarks> 
    public static CalibrationUtils.CalibrationLogCollection CalibrateSemiAnalyticCreditModel(
      Dt asOf,
      Tenor[] forwardTenors,
      string[] creditIndex,
      double rho,
      SurvivalCurve[] credits,
      Tenor[] spreadTenors,
      VolatilityCurve[] atmVolCurves,
      double[,] creditBetas,
      SurvivalCurve[] cptyCredits,
      Tenor[] cptySpreadTenors,
      VolatilityCurve[] cptyAtmVolCurves,
      double[,] cptyBetas,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities)
    {
      rho = Math.Max(Math.Min(rho, 1.0), -1.0);
      var retVal = new CalibrationUtils.CalibrationLogCollection(credits.Length);
      factorLoadings = new FactorLoadingCollection(creditIndex, forwardTenors);
      volatilities = new VolatilityCollection(forwardTenors);
      var primaryFactorLoadings = (creditIndex.Length == 1) ? new[,] {{1.0}} : new[,] {{1.0, 0.0}, {rho, Math.Sqrt(1.0 - rho * rho)}};
      for (int i = 0; i < credits.Length; ++i)
      {
        if ((credits[i] == null) || cptyCredits.Contains(credits[i]))
          continue;
        var dc = (credits[i].SurvivalCalibrator != null) ? credits[i].SurvivalCalibrator.DiscountCurve : null;
        if (dc == null)
          throw new ArgumentException(string.Format("DiscountCurve for credit {0} not found.", credits[i].Name));
        CalibrateCreditVolatility(asOf, atmVolCurves[i], spreadTenors[i], credits[i], dc, volatilities, retVal, 20);
        var factors = Multiply(creditBetas.Row(i), primaryFactorLoadings);
        factorLoadings.AddFactors(credits[i], new[] { factors.Scale(-factors.Norm()) }.ToMatrix());
      }
      for (int i = 0; i < cptyCredits.Length; ++i)
      {
        var sc = cptyCredits[i];
        if (sc == null)
          continue;
        var dc = (sc.SurvivalCalibrator != null) ? sc.SurvivalCalibrator.DiscountCurve : null;
        if(dc == null)
          throw new ArgumentException(string.Format("DiscountCurve for credit {0} not found.", credits[i].Name));
        CalibrateCreditVolatility(asOf, cptyAtmVolCurves[i], cptySpreadTenors[i], cptyCredits[i], dc, volatilities, retVal, 20);
        var factors = Multiply(cptyBetas.Row(i), primaryFactorLoadings);
        double norm = factors.Norm();
        if (norm > 1.0)
          factors = factors.Scale(norm);
        factorLoadings.AddFactors(cptyCredits[i], new[] {factors.Scale(-1.0)}.ToMatrix());
      }
      return retVal;
    }

    #endregion

    #region SemiAnalyticFXCalibration
    /// <summary>
    /// Calibrate semi-analytic FX model
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Term structure forward tenors</param>
    /// <param name="fxCurve">Underlier</param>
    /// <param name="rho">Correlation between domestic and foreign forward rates</param>
    /// <param name="ccy1CapletVolatility">Ccy1 caplet vols</param>
    /// <param name="ccy2CapletVolatility">Ccy2 caplet vols</param>
    /// <param name="fxVolatility">At the money Black vol of forward FX</param>
    /// <param name="counterpartyCurves">Counterparty curves</param>
    /// <param name="counterpartyBetas">Betas of counterparty credit spread respectively to spot FX rate and Ccy2 forward rates</param>
    /// <param name="cptySpreadTenors">Tenors for underlying spread options</param>
    /// <param name="counterpartyBlackVols">Black volatility of cpty credit spread</param>
    /// <param name="factorLoadings">Overwritten by factor loadings</param>
    /// <param name="volatilities">Overwritten by volatilities</param>
    /// <returns>Calibration logs</returns>
    /// <remarks> 
    /// The projective model is based on markov projections of the libor rate processes, and requires a separable libor instantaneous volatility of the form <m>\sigma_i(t):=\psi_i \phi(t)</m>
    /// The projective two-currency LMM assumes that each libor family is driven by one factor, namely <m>M^i_t := \int_0^t \phi_{i}(s)dW^i_s, </m>  with <m>\langle W^i,W^j\rangle_t = \rho t</m> 
    /// The spot FX rate is driven by an affine combination of the driving martingales, so that <m>\sigma_{fx}(t) :=  \sigma_{fx} (\rho_{fx} \phi_1(t) + \sqrt{1 - \rho_{fx}^2}\phi_2(t)).</m> </remarks>
    public static CalibrationUtils.CalibrationLogCollection CalibrateSemiAnalyticFxModel(
      Dt asOf,
      Tenor[] forwardTenors,
      FxCurve fxCurve,
      double rho,
      object ccy1CapletVolatility,
      object ccy2CapletVolatility,
      object fxVolatility,
      SurvivalCurve[] counterpartyCurves,
      double[,] counterpartyBetas,
      Tenor[] cptySpreadTenors,
      VolatilityCurve[] counterpartyBlackVols,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities)
    {
      rho = Math.Max(Math.Min(rho, 1.0), -1.0);
      var factorNames = new[] {fxCurve.Ccy2.ToString(), fxCurve.Ccy1.ToString()};
      var retVal = new CalibrationUtils.CalibrationLogCollection(1 + counterpartyCurves.Length);
      factorLoadings = new FactorLoadingCollection(factorNames, forwardTenors);
      volatilities = new VolatilityCollection(forwardTenors);
      var primaryFactorLoadings = new[,] {{1, 0}, {rho, Math.Sqrt(1 - rho * rho)}};
      var ccy1Fl = new double[factorLoadings.TenorCount,factorLoadings.FactorCount];
      var ccy2Fl = new double[factorLoadings.TenorCount,factorLoadings.FactorCount];
      for (int i = 0; i < factorLoadings.TenorCount; ++i)
      {
        ccy2Fl[i, 0] = primaryFactorLoadings[0, 0];
        ccy2Fl[i, 1] = primaryFactorLoadings[0, 1];
        ccy1Fl[i, 0] = primaryFactorLoadings[1, 0];
        ccy1Fl[i, 1] = primaryFactorLoadings[1, 1];
      }
      factorLoadings.AddFactors(fxCurve.Ccy2DiscountCurve, ccy2Fl);
      factorLoadings.AddFactors(fxCurve.Ccy1DiscountCurve, ccy1Fl);
      FromVolatilityObject(fxCurve.Ccy1DiscountCurve, ccy1CapletVolatility, true, factorLoadings, volatilities);
      FromVolatilityObject(fxCurve.Ccy2DiscountCurve, ccy2CapletVolatility, true, factorLoadings, volatilities);
      CalibrateSemiAnalyticFxVolatility(asOf, fxCurve, fxVolatility, volatilities, factorLoadings, retVal, null);
      var fxFl = factorLoadings.GetFactorsAt(fxCurve.SpotFxRate);
      primaryFactorLoadings[0, 0] = fxFl[0, 0];
      primaryFactorLoadings[0, 1] = fxFl[0, 1];
      for (int i = 0; i < counterpartyCurves.Length; ++i)
      {
        var sc = counterpartyCurves[i];
        if (sc == null) continue;
        var dc = (sc.SurvivalCalibrator != null && sc.SurvivalCalibrator.DiscountCurve != null)
                   ? sc.SurvivalCalibrator.DiscountCurve
                   : (sc.Ccy == fxCurve.Ccy1) ? fxCurve.Ccy1DiscountCurve : fxCurve.Ccy2DiscountCurve;
        var fl = Multiply(counterpartyBetas.Row(i), primaryFactorLoadings).Scale(-1.0);
        double norm = fl.Norm();
        if (norm > 1.0)
          fl = fl.Scale(norm);
        factorLoadings.AddFactors(sc, new[] { fl }.ToMatrix());
        CalibrateCreditVolatility(asOf, counterpartyBlackVols[i], cptySpreadTenors[i], sc, dc, volatilities, retVal, 20);
      }
      return retVal;
    }

    #endregion

    #region SemiAnalyticForwardCalibration
    /// <summary>
    /// Calibrate semi-analytic forward price model
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Term structure forward tenors</param>
    /// <param name="discountCurve">Underlying discount curve</param>
    /// <param name="forwardCurve">Underlier</param>
    /// <param name="rho">Correlation between forward prices and forward rates</param>
    /// <param name="capletVolatility">Caplet vols</param>
    /// <param name="fwdVolatility">At the money Black vol of forward prices/rates</param>
    /// <param name="counterpartyCurves">Counterparty curves</param>
    /// <param name="counterpartyBetas">Betas of counterparty credit spread respectively to underlying funding forward rates and projection forward prices/rates</param>
    /// <param name="cptySpreadTenors">Tenors for underlying spread options</param>
    /// <param name="counterpartyBlackVols">Black volatility of cpty credit spread</param>
    /// <param name="factorLoadings">Overwritten by factor loadings</param>
    /// <param name="volatilities">Overwritten by volatilities</param>
    /// <returns></returns>
    /// <remarks> 
    /// The projective model is based on markov projections of the libor rate/ forward price processes, and requires a separable instantaneous volatility of the form <m>\sigma_i(t):=\psi_i \phi(t)</m>
    /// The projective model assumes that each term structure is driven by one factor, namely <m>M^i_t := \int_0^t \phi_{i}(s)dW^i_s, </m>  with <m>\langle W^i,W^j\rangle_t = \rho t</m> 
    /// </remarks>
    public static CalibrationUtils.CalibrationLogCollection CalibrateSemiAnalyticForwardModel(
      Dt asOf,
      Tenor[] forwardTenors,
      DiscountCurve discountCurve,
      CalibratedCurve forwardCurve,
      double rho,
      object capletVolatility,
      object fwdVolatility,
      SurvivalCurve[] counterpartyCurves,
      double[,] counterpartyBetas,
      Tenor[] cptySpreadTenors,
      VolatilityCurve[] counterpartyBlackVols,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities)
    {
      rho = Math.Max(Math.Min(rho, 1.0), -1.0);
      var factorNames = new[] {forwardCurve.Ccy.ToString(), forwardCurve.Name};
      var retVal = new CalibrationUtils.CalibrationLogCollection(1 + counterpartyCurves.Length);
      var primaryFactorLoadings = new[,] {{1, 0}, {rho, Math.Sqrt(1.0 - rho*rho)}};
      factorLoadings = new FactorLoadingCollection(factorNames, forwardTenors);
      volatilities = new VolatilityCollection(forwardTenors);
      var liborFl = new double[factorLoadings.TenorCount,factorLoadings.FactorCount];
      var fwdFl = new double[factorLoadings.TenorCount,factorLoadings.FactorCount];
      for (int i = 0; i < factorLoadings.TenorCount; ++i)
      {
        liborFl[i, 0] = primaryFactorLoadings[0,0];
        liborFl[i, 1] = primaryFactorLoadings[0,1];
        fwdFl[i, 0] = primaryFactorLoadings[1,0];
        fwdFl[i, 1] = primaryFactorLoadings[1,1];
      }
      factorLoadings.AddFactors(discountCurve, liborFl);
      factorLoadings.AddFactors(forwardCurve, fwdFl);
      FromVolatilityObject(discountCurve, capletVolatility, true, factorLoadings, volatilities);
      if (forwardCurve is DiscountCurve)
        FromVolatilityObject(forwardCurve as DiscountCurve, fwdVolatility, true, factorLoadings, volatilities);
      else
        FromVolatilityObject(forwardCurve, fwdVolatility, volatilities);
      for (int i = 0; i < counterpartyCurves.Length; ++i)
      {
        var sc = counterpartyCurves[i];
        if (sc == null)
          continue;
        var dc = (sc.SurvivalCalibrator != null && sc.SurvivalCalibrator.DiscountCurve != null) ? sc.SurvivalCalibrator.DiscountCurve : discountCurve;
        CalibrateCreditVolatility(asOf, counterpartyBlackVols[i], cptySpreadTenors[i], sc, dc, volatilities, retVal, 20);
        var fl = Multiply(counterpartyBetas.Row(i), primaryFactorLoadings).Scale(-1.0);
        double norm = fl.Norm();
        if (norm > 1.0)
          fl = fl.Scale(norm);
        factorLoadings.AddFactors(sc, new[] { fl }.ToMatrix());
      }
      return retVal;
    }


    #endregion

    #region SemiAnalyticSpotCalibration
    /// <summary>
    /// Calibrate semi-analytic spot price model
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="forwardTenors">Term structure forward tenors</param>
    /// <param name="forwardCurve">Underlier</param>
    /// <param name="capletVolatility">Caplet vols</param>
    /// <param name="volatility">Black vol of forward price</param>
    /// <param name="counterpartyCurves">Counterparty curves</param>
    /// <param name="counterpartyBetas">Betas of counterparty credit spread respectively to underlying forward rates and spot price</param>
    /// <param name="cptySpreadTenors">Tenors for underlying spread options</param>
    /// <param name="counterpartyBlackVols">Black volatility of cpty credit spread</param>
    /// <param name="factorLoadings">Overwritten by factor loadings</param>
    /// <param name="volatilities">Overwritten by volatilities</param>
    /// <remarks> 
    /// The projective model is based on markov projections of the libor rate and requires a separable instantaneous volatility of the form <m>\sigma_i(t):=\psi_i \phi(t)</m> for the underlying libor rate
    /// The projective model assumes that forward rates and spot price are driven respectively by gaussian martingales <m>M_t := \int_0^t \phi(s)dW^i_s, </m>  
    /// and  <m>N_t := \int_0^t \sigma(s)dB_s, </m> with <m>\langle W,B\rangle_t = \rho t</m> 
    /// </remarks>
    public static CalibrationUtils.CalibrationLogCollection CalibrateSemiAnalyticSpotModel(
      Dt asOf,
      Tenor[] forwardTenors,
      CalibratedCurve forwardCurve,
      object capletVolatility,
      object volatility,
      SurvivalCurve[] counterpartyCurves,
      double[,] counterpartyBetas,
      Tenor[] cptySpreadTenors,
      VolatilityCurve[] counterpartyBlackVols,
      out FactorLoadingCollection factorLoadings,
      out VolatilityCollection volatilities)
    {
      var spotBased = forwardCurve as IForwardPriceCurve;
      if(spotBased == null || spotBased.Spot == null)
        throw new ArgumentException("forwardCurve expected of type ISpotBasedForwardCurve and have observable spot price");
      var factorNames = new[] {forwardCurve.Ccy.ToString(), spotBased.Spot.Name};
      var retVal = new CalibrationUtils.CalibrationLogCollection(1 + counterpartyCurves.Length);
      factorLoadings = new FactorLoadingCollection(factorNames.ToArray(), forwardTenors);
      volatilities = new VolatilityCollection(forwardTenors);
      var liborFl = new double[factorLoadings.TenorCount,factorLoadings.FactorCount];
      for (int i = 0; i < liborFl.GetLength(0); ++i)
        liborFl[i, 0] = 1.0;
      factorLoadings.AddFactors(spotBased.DiscountCurve, liborFl);
      FromVolatilityObject(spotBased.DiscountCurve, capletVolatility, true, factorLoadings, volatilities);
      CalibrateSemiAnalyticSpotVolatility(asOf, forwardCurve, volatility, volatilities, factorLoadings, retVal, null);
      var spotFl = factorLoadings.GetFactorsAt(spotBased.Spot);
      var primaryFactorLoadings = new[,] {{1, 0}, {spotFl[0, 0], spotFl[0, 1]}};
      for (int i = 0; i < counterpartyCurves.Length; ++i)
      {
        var sc = counterpartyCurves[i];
        var dc = (sc.SurvivalCalibrator != null && sc.SurvivalCalibrator.DiscountCurve != null)
                   ? sc.SurvivalCalibrator.DiscountCurve
                   : spotBased.DiscountCurve;
        CalibrateCreditVolatility(asOf, counterpartyBlackVols[i], cptySpreadTenors[i], sc, dc, volatilities, retVal, 20);
        var fl = Multiply(counterpartyBetas.Row(i), primaryFactorLoadings).Scale(-1.0);
        double norm = fl.Norm();
        if (norm > 1.0)
          fl = fl.Scale(norm);
        factorLoadings.AddFactors(sc, new[] { fl }.ToMatrix());
      }
      return retVal;
    }

    #endregion

    #region Historical Correlation
    /// <summary>
    /// Historical returns type
    /// </summary>
    public enum ReturnType
    {
      /// <summary>Log return.</summary>
      Log = 0,
      /// <summary>Simple return</summary>
      Simple = 1,
    }

    /// <summary>
    /// Create a correlation from historical data
    /// </summary>
    /// <param name="historicalData">Historical data series</param>
    /// <param name="start">start date for correlation</param>
    /// <param name="end">end date for correlation</param>
    /// <param name="returnType">Return type (Log or Simple)</param>
    /// <param name="halfLife">half life of past observations weight</param>
    /// <returns>Pairwise correlations based on the historical data</returns>
    //  TBD: Options for supporting alternate methods of calculation and handling of missing data.
    //  See http://stat.ethz.ch/R-manual/R-patched/library/stats/html/cor.html for examples. RTD Mar'13
    public static GeneralCorrelation CorrelationFromHistoricalSeries(
      RateResetsHistorical[] historicalData,
      Dt start,
      Dt end,
      ReturnType[] returnType,
      double halfLife)
    {
      if (historicalData == null || historicalData.Length <= 1)
        return new GeneralCorrelation(new string[]{}, new double[]{1.0} );
      if (start >= end)
        throw new ArgumentException("End date must be later than the start date");
      if (returnType != null && returnType.Length != historicalData.Length)
        throw new ArgumentException("returnType expected of same size as historicalData");
      returnType = historicalData.Where(d => d != null).Select((d, i) => (returnType == null) ? ReturnType.Log : returnType[i]).ToArray();
      historicalData = historicalData.Where(d => d != null).ToArray();
      var data = historicalData.Select(d => d.Where(r => r.Date >= start && r.Date <= end).Select(r => r.Rate).ToArray()).ToArray();
      int size = data[0].Length;
      for (int i = 1; i < data.Length; ++i)
        if (data[i].Length != size)
          throw new ArgumentException(String.Format("Expected {0} historical observations between start and end for historicalData at index {1}", size, i));
      Func<double, double, double> simpleReturn = (x, y) => x - y;
      Func<double, double, double> logReturn = (x, y) => (y > 0 && x > 0) ? Math.Log(x / y) : 0.0;
      data = data.Select((d, i) => ToReturns(d, (returnType[i] == ReturnType.Log) ? logReturn : simpleReturn, halfLife, true)).ToArray();
      var corrs = new double[data.Length * data.Length];
      var names = historicalData.Select(d => d.Description).ToArray();
      for (int i = 0; i < data.Length; i++)
      {
        var ri = data[i];
        for (int j = 0; j < i; j++)
        {
          var rj = data[j];
          corrs[i * data.Length + j] = corrs[j * data.Length+i] = SampleCovariance(ri, rj);
        }
        corrs[i * data.Length + i] = 1.0;
      }
      return new GeneralCorrelation(names, corrs);
    }

    /// <summary>
    /// Create a correlation table from historical data
    /// </summary>
    /// <param name="historicalData">Historical data series</param>
    /// <param name="start">start date for correlation</param>
    /// <param name="end">end date for correlation</param>
    /// <param name="returnType">Return type (Log or Simple)</param>
    /// <param name="halfLife">half life of past observations weight</param>
    /// <returns>Pairwise correlation table based on the historical data</returns>
    public static double[,] CorrelationFromHistoricalData(
      RateResetsHistorical[] historicalData,
      Dt start,
      Dt end,
      ReturnType[] returnType,
      double halfLife)
    {
      if (historicalData == null)
        throw new ArgumentException("Historical data must be non-null"); 
      if (historicalData.Length <= 1)
        return new[,] { { 1.0 } };
      if (start >= end)
        throw new ArgumentException("End date must be later than the start date");
      if (returnType != null && returnType.Length != historicalData.Length)
        throw new ArgumentException("returnType expected of same size as historicalData");
      returnType = (returnType == null) ? historicalData.Where(d => d != null).Select(d => ReturnType.Log).ToArray() : returnType.Where((t,i) => historicalData[i] != null).ToArray();
      historicalData = historicalData.Where(d => d != null).ToArray();
      var data = historicalData.Select(d => d.Where(r => r.Date >= start && r.Date <= end).Select(r => r.Rate).ToArray()).ToArray();
      int size = data[0].Length;
      for (int i = 1; i < data.Length; ++i)
        if (data[i].Length != size)
          throw new ArgumentException(String.Format("Expected {0} historical observations between start and end for historicalData at index {1}", size, i));
      Func<double, double, double> simpleReturn = (x, y) => x - y;
      Func<double, double, double> logReturn = (x, y) => (y > 0 && x > 0) ? Math.Log(x / y) : 0.0;
      data = data.Select((d, i) => ToReturns(d, (returnType[i] == ReturnType.Log) ? logReturn : simpleReturn, halfLife, true)).ToArray();
      var retVal = new double[data.Length, data.Length];
      for (int i = 0; i < data.Length; i++)
      {
        var ri = data[i];
        for (int j = 0; j < i; j++)
        {
          var rj = data[j];
          retVal[i, j] = retVal[j, i] = SampleCovariance(ri, rj);
        }
        retVal[i, i] = 1;
      }
      return retVal;
    }

    /// <summary>
    /// Calculate Betas to primary assets from historical data
    /// </summary>
    /// <param name="primaryHistoricalData">Primary assets data series</param>
    /// <param name="secondaryHistoricalData">Secondary assets data series</param>
    /// <param name="start">Start date for correlation</param>
    /// <param name="end">End date for correlation</param>
    /// <param name="primaryReturnType">Proxy assets return types</param>
    /// <param name="secondaryReturnType">Secondary assets return types</param>
    /// <param name="halfLife">half life of past observations weights</param>
    /// <param name="standardizeBeta">Whether to standardize the coefficients</param>
    /// <param name="selectBetas">Select proxies to regress against</param>
    /// <returns>Betas' table based on historical data. 
    /// The last two columns are respectively the standard deviation of the error and the
    /// standard deviation of the dependent variable</returns>
    public static FactorCorrelation BetasFromHistoricalSeries(
      RateResetsHistorical[] primaryHistoricalData,
      RateResetsHistorical[] secondaryHistoricalData,
      Dt start,
      Dt end,
      ReturnType[] primaryReturnType,
      ReturnType[] secondaryReturnType,
      double halfLife,
      bool standardizeBeta,
      bool[,] selectBetas)
    {
      if (primaryHistoricalData == null || primaryHistoricalData.Length <= 1)
        throw new ArgumentException("Primary asset data can not be empty");
      if (secondaryHistoricalData == null || secondaryHistoricalData.Length == 0)
        throw new ArgumentException("Secondary asset data can not be empty");
      if (start >= end)
        throw new ArgumentException("End date must be later than the start date");
      if (primaryReturnType != null && primaryReturnType.Length != primaryHistoricalData.Length)
        throw new ArgumentException("primaryReturnType expected of same size as primaryHistoricalData");
      if (secondaryReturnType != null && secondaryReturnType.Length != secondaryHistoricalData.Length)
        throw new ArgumentException("secondaryReturnType expected of same size as secondaryHistoricalData");
      if (selectBetas == null)
        throw new ArgumentException("selectBetas cannot be null");
      if (selectBetas.GetLength(0) != secondaryHistoricalData.Length || selectBetas.GetLength(1) != primaryHistoricalData.Length)
        throw new ArgumentException(String.Format("selectBetas size is {0}x{1}, but expected size {2}x{3}",
          selectBetas.GetLength(0), selectBetas.GetLength(1), secondaryHistoricalData.Length, primaryHistoricalData.Length));
      if (primaryReturnType == null)
        primaryReturnType = primaryHistoricalData.Select((d, i) => ReturnType.Log).ToArray();
      // Filter out empty primary and secondary elements
      var primaryCount = primaryHistoricalData.Count(d => d != null);
      var secondaryCount = secondaryHistoricalData.Count(d => d != null);
      var cleanSelectBetas = new bool[secondaryCount, primaryCount];
      for (int i = 0, ii = 0; i < secondaryHistoricalData.Length; ++i)
      {
        if (secondaryHistoricalData[i] == null) continue;
        for (int j = 0, jj = 0; j < primaryHistoricalData.Length; ++j)
        {
          if (primaryHistoricalData[j] == null) continue;
          cleanSelectBetas[ii, jj] = selectBetas[i, j];
          ++jj;
        }
        ++ii;
      }
      selectBetas = cleanSelectBetas;
      primaryReturnType = primaryHistoricalData.Where(d => d != null).Select((d, i) => (primaryReturnType == null) ? CCRCalibrationUtils.ReturnType.Log : primaryReturnType[i]).ToArray();
      primaryHistoricalData = primaryHistoricalData.Where(d => d != null).ToArray();
      secondaryReturnType = secondaryHistoricalData.Where(d => d != null).Select((d, i) => (secondaryReturnType == null) ? CCRCalibrationUtils.ReturnType.Log : secondaryReturnType[i]).ToArray();
      secondaryHistoricalData = secondaryHistoricalData.Where(d => d != null).ToArray();

      var names = secondaryHistoricalData.Select(d => d.Description).ToArray();
      var proxyData = primaryHistoricalData.Select(d => (d != null) ? d.Where(r => r.Date >= start && r.Date <= end).Select(r => r.Rate).ToArray() : null).ToArray();
      var secondaryData = secondaryHistoricalData.Select(d => d.Where(r => r.Date >= start && r.Date <= end).Select(r => r.Rate).ToArray()).ToArray();
      var size = secondaryData[0].Length; // secondaryData is guaranteed to be filled in
      for (var i = 1; i < secondaryData.Length; ++i)
        if (secondaryData[i].Length != size)
          throw new ArgumentException(String.Format("Expected {0} secondary historical observations between start and end for historicalData at index {1}", size, i));
      for (var i = 0; i < proxyData.Length; ++i)
        if (proxyData[i] != null && proxyData[i].Length != size)
          throw new ArgumentException(String.Format("Expected {0} primary historical observations between start and end for historicalData at index {1}", size, i));
      Func<double, double, double> simpleReturn = (x, y) => x - y;
      Func<double, double, double> logReturn = (x, y) => (y > 0 && x > 0) ? Math.Log(x / y) : 0.0;
      proxyData = proxyData.Select((d, i) => (d != null) ? ToReturns(d, (primaryReturnType[i] == ReturnType.Log) ? logReturn : simpleReturn, halfLife, standardizeBeta) : null).ToArray();
      secondaryData = secondaryData.Select((d, i) => ToReturns(d, (secondaryReturnType[i] == ReturnType.Log) ? logReturn : simpleReturn, halfLife, standardizeBeta)).ToArray();
      var retVal = new double[proxyData.Length, secondaryData.Length];
      for (var i = 0; i < secondaryData.Length; ++i)
      {
        var xM = proxyData.Where((d, j) => (d != null) && selectBetas[i, j]).ToArray();
        if (xM.Length <= 0) continue;
        var xMatrix = xM.ToMatrixTranspose();
        var w = new double[xMatrix.GetLength(1)];
        var v = new double[xMatrix.GetLength(1), xMatrix.GetLength(1)];
        var xVec = new double[xMatrix.GetLength(1)];
        LinearSolvers.FactorizeSVD(xMatrix, w, v);
        var y = secondaryData[i];
        LinearSolvers.SolveSVD(xMatrix, w, v, y, xVec, 1e-5);
        for (int j = 0, k = 0; j < proxyData.Length; ++j)
        {
          if (proxyData[j] == null || !selectBetas[i, j])
            continue;
          retVal[j, i] = xVec[k];
          ++k;
        }
      }
      return new FactorCorrelation(names, primaryHistoricalData.Length, retVal);
    }

    /// <summary>
    /// Beta to proxy assets computed from historical data
    /// </summary>
    /// <param name="proxyHistoricalData">Proxy assets data series</param>
    /// <param name="secondaryHistoricalData">Secondary assets data series</param>
    /// <param name="start">Start date for correlation</param>
    /// <param name="end">End date for correlation</param>
    /// <param name="proxyReturnType">Proxy assets return types</param>
    /// <param name="secondaryReturnType">Secondary assets return types</param>
    /// <param name="halfLife">half life of past observations weights</param>
    /// <param name="standardizeBeta">Whether to standardize the coefficients</param>
    /// <param name="selectBetas">Select proxies to regress against</param>
    /// <returns>Betas' table based on historical data. 
    /// The last two columns are respectively the standard deviation of the error and the standard deviation of the dependent variable</returns>
    public static double[,] BetasFromHistoricalData(
      RateResetsHistorical[] proxyHistoricalData,
      RateResetsHistorical[] secondaryHistoricalData,
      Dt start,
      Dt end,
      ReturnType[] proxyReturnType,
      ReturnType[] secondaryReturnType,
      double halfLife,
      bool standardizeBeta,
      bool[,] selectBetas)
    {
      if (secondaryHistoricalData == null || secondaryHistoricalData.Length == 0)
        throw new ArgumentException("Secondary asset data can not be null");
      if (start >= end)
        throw new ArgumentException("End date must be later than the start date");
      if (proxyReturnType == null)
        proxyReturnType = proxyHistoricalData.Select(d => ReturnType.Log).ToArray();
      if (secondaryReturnType == null)
        secondaryReturnType = secondaryHistoricalData.Select(d => ReturnType.Log).ToArray();
      var proxyData = proxyHistoricalData.Select(d => d.Where(r => r.Date >= start && r.Date <= end).Select(r => r.Rate).ToArray()).ToArray();
      var secondaryData = secondaryHistoricalData.Select(d => d.Where(r => r.Date >= start && r.Date <= end).Select(r => r.Rate).ToArray()).ToArray();
      int size = proxyData[0].Length;
      for (int i = 1; i < proxyData.Length; ++i)
        if (proxyData[i].Length != size)
          throw new ArgumentException(String.Format("Expected {0} proxy historical observations between start and end for historicalData at index {1}", size, i));
      Func<double, double, double> simpleReturn = (x, y) => x - y;
      Func<double, double, double> logReturn = (x, y) => (y > 0 && x > 0) ? Math.Log(x / y) : 0.0;
      proxyData = proxyData.Select((d, i) => ToReturns(d, (proxyReturnType[i] == ReturnType.Log) ? logReturn : simpleReturn, halfLife, standardizeBeta)).ToArray();
      secondaryData = secondaryData.Select((d, i) => ToReturns(d, (secondaryReturnType[i] == ReturnType.Log) ? logReturn : simpleReturn, halfLife, standardizeBeta)).ToArray();
      var retVal = new double[secondaryData.Length, proxyData.Length];
      if (selectBetas == null)
      {
        var xMatrix = proxyData.ToMatrixTranspose();
        var w = new double[xMatrix.GetLength(1)];
        var v = new double[xMatrix.GetLength(1), xMatrix.GetLength(1)];
        var xVec = new double[xMatrix.GetLength(1)];
        LinearSolvers.FactorizeSVD(xMatrix, w, v);
        for (int i = 0; i < secondaryData.Length; ++i)
        {
          var y = secondaryData[i];
          LinearSolvers.SolveSVD(xMatrix, w, v, y, xVec, 1e-5);
          for (int j = 0; j < xVec.Length; ++j)
            retVal[i, j] = xVec[j];
        }
      }
      else
      {
        for (int i = 0; i < secondaryData.Length; ++i)
        {
          var betas = selectBetas.Row(i);
          var xM = proxyData.Where((d, j) => betas[j]).ToArray();
          if (xM.Length <= 0) continue;
          var xMatrix = xM.ToMatrixTranspose();
          var w = new double[xMatrix.GetLength(1)];
          var v = new double[xMatrix.GetLength(1),xMatrix.GetLength(1)];
          var xVec = new double[xMatrix.GetLength(1)];
          LinearSolvers.FactorizeSVD(xMatrix, w, v);
          var y = secondaryData[i];
          LinearSolvers.SolveSVD(xMatrix, w, v, y, xVec, 1e-5);
          for (int j = 0, k = 0; j < proxyData.Length; ++j)
          {
            if (!betas[j])
              continue;
            retVal[i, j] = xVec[k];
            ++k;
          }
        }
      }
      return retVal;
    }

    #endregion

    #region Utils

    private static string ObjectId(object obj, Tenor t)
    {
      var cc = obj as CalibratedCurve;
      if (cc != null)
        return t.IsEmpty ? cc.Name : String.Format("{0}.{1:S}", cc.Name, t);
      var spot = obj as ISpot;
      if (spot != null)
        return spot.Name;
      return string.Empty;
    }
    
    private static T[] Row<T>(this T[,] array, int idx)
    {
      var retVal = new T[array.GetLength(1)];
      for (int i = 0; i < retVal.Length; ++i)
        retVal[i] = array[idx, i];
      return retVal;
    }

    private static double[] Resize(this double[] array, int dim)
    {
      if (array.Length == dim)
        return array;
      var retVal = new double[dim];
      dim = Math.Min(array.GetLength(0), dim);
      for (int i = 0; i < dim; ++i)
        retVal[i] = array[i];
      return retVal;
    }

    private static double[,] Resize(this double[,] array, int dim0, int dim1)
    {
      var retVal = new double[dim0, dim1];
      dim0 = Math.Min(array.GetLength(0), dim0);
      dim1 = Math.Min(array.GetLength(1), dim1);
      for (int i = 0; i < dim0; ++i)
        for (int j = 0; j < dim1; ++j)
          retVal[i, j] = array[i, j];
      return retVal;
    }

    private static double[,] ToMatrix(this double[][] jaggedArray)
    {
      if (jaggedArray == null)
        return null;
      int rows = jaggedArray.Length;int cols = jaggedArray.Max(v => v.Length);
      var retVal = new double[rows,cols];
      for (int i = 0; i < rows; ++i)
        for (int j = 0; j < jaggedArray[i].Length; ++j)
          retVal[i, j] = jaggedArray[i][j];
      return retVal;
    }

    private static double[,] ToMatrixTranspose(this double[][] jaggedArray)
    {
      int rows = jaggedArray.Max(v => v.Length);
      int cols = jaggedArray.Length;
      var retVal = new double[rows, cols];
      for (int i = 0; i < rows; ++i)
        for (int j = 0; j < cols; ++j)
          retVal[i, j] = jaggedArray[j][i];
      return retVal;
    }

    private static double Norm(this double[] v)
    {
      double retVal = 0.0;
      for (int i = 0; i < v.Length; ++i)
        retVal += v[i] * v[i];
      return Math.Sqrt(retVal);
    }

    private static double[] Scale(this double[] v, double factor)
    {
      for (int i = 0; i < v.Length; ++i)
        v[i] /= factor;
      return v;
    }

    private static double[] Multiply(double[] v, double[,] m)
    {
      var retVal = new double[m.GetLength(1)];
      for (int i = 0; i < v.Length; ++i)
      {
        for (int j = 0; j < retVal.Length; ++j)
        {
          retVal[j] += v[i] * m[i, j];
        }
      }
      return retVal;
    }

    private static double[] ToReturns(double[] data, Func<double, double, double> map, double halfLife, bool standardize)
    {
      var returns = data.Skip(1).Select((r, i) => map(r, data[i])).ToArray();
      double w = 1.0;
      if (halfLife > 1)
      {
        double factor = Math.Pow(0.5, 1.0 / halfLife);
        for (int i = returns.Length; --i >= 0; )
        {
          returns[i] *= w;
          w *= factor;
        }
      }
      if (standardize)
        StandardizeData(returns);
      return returns;
    }

    private static double SampleCovariance(double[] x, double[] y)
    {
      double retVal = 0.0;
      for (int i = 0; i < x.Length; ++i)
        retVal += x[i] * y[i];
      return retVal / x.Length;
    }

    private static double DataStdev(double[] data, out double mean)
    {
      double fm = 0;
      double sm = 0;
      for (int j = 0, count = 1; j < data.Length; ++j, ++count)
      {
        double d = data[j];
        fm += (d - fm) / count;
        sm += (d * d - sm) / count;
      }
      mean = fm;
      if (sm - fm * fm <= 0.0)
      {
        if (fm.AlmostEquals(0.0))
          return 0.0; // All zeros are accepted
        throw new ArgumentException("Degenerate time series cannot be used for correlation estimation");
      }
      return Math.Sqrt(Math.Max(sm - fm * fm, 0.0));
    }

    private static void StandardizeData(double[] data)
    {
      double mu;
      var sigma = DataStdev(data, out mu);
      if (sigma.AlmostEquals(0.0)) return;
      for (int j = 0; j < data.Length; ++j)
        data[j] = (data[j] - mu) / sigma;
    }

    #endregion

    #region Rate Volatility Model Builder

    /// <summary>
    /// Delegate ModelBuilder
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="data">The data.</param>
    /// <param name="factorNames">The factor names.</param>
    /// <returns>RateVolatilityWithModel.</returns>
    public delegate RateVolatilityWithModel ModelBuilder(Dt asOf,
      IReadOnlyList<FactorData> data, IReadOnlyList<string> factorNames);

    /// <summary>
    /// Gets the libor market volatility model builder.
    /// </summary>
    /// <param name="separableVolatility">True to calibrate instantaneous vol of the form <m>\phi(T)\psi(t)</m> where <m>\phi(T)</m> is a maturity dependent constant 
    /// and <m>\psi(t)</m> is a time dependent function common to all tenors. This functional specification in a one factor framework results in a rank one 
    /// covariance matrix.  
    /// 
    /// If false, calibrated vol is of the form \phi(T)\psi(T-t). This parametric form preserves the shape of the vol surface over running time.</param>
    /// <returns>ModelBuilder.</returns>
    public static ModelBuilder GetLiborMarketVolatilityModelBuilder(
      bool separableVolatility)
    {
      return (asOf, data, factorNames) => GetRateCalibrationModel(
        separableVolatility, asOf, data, factorNames);
    }

    /// <summary>
    /// Gets the Hull-White short rate model builder.
    /// </summary>
    /// <returns>ModelBuilder.</returns>
    public static ModelBuilder GetHullWhiteShortRateModelBuilder()
    {
      return (asOf, data, factorNames) => data.Any(d => d.Volatility != null)
        ? new RateVolatilityWithModel(
          CalibrationUtils.GetHullWhiteShortRateCalibrationModel(),
          data.First(d => d.Volatility != null).Volatility)
        : RateVolatilityWithModel.Empty;
    }

    private static RateVolatilityWithModel GetRateCalibrationModel(
      bool separableVol,
      Dt asOf,
      IReadOnlyList<FactorData> data,
      IReadOnlyList<string> factorNames)
    {
      if (data.Any(d => d.Volatility != null))
      {
        var vol = data.First(d => d.Volatility != null).Volatility;
        var swapFactors = data.Select(d =>
        {
          var idiosyncratic = d.IdiosyncraticFactor;
          if (idiosyncratic != null)
          {
            var f = d.Factors.Resize(factorNames.Count);
            f[idiosyncratic.Item1] = idiosyncratic.Item2;
            return f;
          }
          return d.Factors;
        }).ToArray();

        var model = CalibrationUtils.GetLiborMarketCalibrationModel(asOf, separableVol,
          data.Select(d => d.Type == MarketVariableType.SwapRate ? asOf : Dt.Add(asOf, d.Tenor)).ToArray(),
          data.Select(d => Dt.Add(asOf, d.Tenor)).ToArray(), swapFactors);
        return new RateVolatilityWithModel(model, vol);
      }
      return RateVolatilityWithModel.Empty;
    }
    #endregion
  }
}