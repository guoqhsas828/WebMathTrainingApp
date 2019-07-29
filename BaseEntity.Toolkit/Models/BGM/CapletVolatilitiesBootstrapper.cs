using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.BGM
{

  /// <summary>
  /// Container class for the calibration outputs 
  /// </summary>
  [Serializable]
  public class CalibrationOutput
  {
    /// <summary>
    /// Constructor for the calibration output class 
    /// </summary>
    /// <param name="curve"></param>
    /// <param name="strike"></param>
    /// <param name="errors"></param>
    public CalibrationOutput(Curve curve, double strike, double[] errors)
    {
      Curve = curve;
      Strike = strike;
      Errors = errors;
    }

    /// <summary>The Volatility curve </summary>
    public Curve Curve { get; set; }
    /// <summary>The Strike </summary>
    public double Strike { get; set; }
    /// <summary>The Fit Errors  </summary>
    public double[] Errors { get; set; }
  }

  /// <summary>
  /// Caplet Volatilities Stripper class 
  /// </summary>
  public static class CapletVolatilitiesBootstrapper
  {

    #region Methods

    /// <summary>
    /// Bootstrap caplet volatility curve for given strike 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="capMaturities">Underlying cap maturities</param>
    /// <param name="capVols">Underlying cap volatility</param>
    /// <param name="strike">Strike</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="referenceCurve">Reference curve/index for each underlying cap</param>
    /// <param name="volType">Volatility type</param>
    /// <param name="lambda">Smoothing parameter</param>
    /// <param name="volCurve">Caplet volatility for strike (as function of maturity)</param>
    /// <param name="fitErrors">Fit errors</param>
    /// <remarks>If more than one reference index is provided (i.e. 3M Libor and 6M Libor) then the calibrated index is that corresponding to the shortest tenor.
    /// The volatility of caplets with longer tenor is computed by standard freezing arguments from the relationship <m>1 + \Delta L^{\Delta} = \Pi_i (1 +\delta_i L_i^{\delta_i}) + B</m>
    /// where B is a deterministic basis and <m>L_i</m> are assumed perfectly correlated.
    /// </remarks>
    public static void BootstrapVolatilityCurve(
      Dt asOf,
      Dt[] capMaturities,
      double[] capVols,
      double strike,
      DiscountCurve discountCurve,
      Func<Dt, Tuple<InterestRateIndex, DiscountCurve>> referenceCurve,
      VolatilityType volType,
      double lambda,
      out Curve volCurve,
      out double[] fitErrors)
    {

      var rateOptionParams = RateOptionParamCollection.Factory(asOf, capMaturities, discountCurve, referenceCurve);
      volCurve = new Curve(asOf) { Interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const) };
      fitErrors = new double[capVols.Length];
      RateVolatilityCurveBuilder.BootstrapEDFCapletCurve(capVols, capMaturities, rateOptionParams, new double[0], new double[0],
                                                         new int[0], new Dt[0], strike, 0.0, lambda, volType, volCurve, fitErrors);

    }

    /// <summary>
    /// Bootstrap caplet volatility curve for given strike 
    /// </summary>
    /// <param name="data">Cap volatilities</param>
    /// <param name="asOf">As of date</param>
    /// <param name="capMaturities">Underlying cap maturities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="referenceCurve">Reference curve/index for each underlying cap</param>
    /// <param name="volatilityType">Volatility type</param>
    /// <param name="method">Bootrstrap method</param>
    /// <remarks>If more than one reference index is provided (i.e. 3M Libor and 6M Libor) then the calibrated index is that corresponding to the shortest tenor.
    /// The volatility of caplets with longer tenor is computed by standard freezing arguments from the relationship <m>1 + \Delta L^{\Delta} = \Pi_i (1 +\delta_i L_i^{\delta_i}) + B</m>
    /// where B is a deterministic basis and <m>L_i</m> are assumed perfectly correlated.
    /// </remarks>
    public static Dictionary<double, CalibrationOutput> BootstrapVolatilitySurface(
      VolatilityCubeBootstrapInput[] data, Dt asOf,
      Dt[] capMaturities, DiscountCurve discountCurve, Func<Dt, Tuple<InterestRateIndex, DiscountCurve>> referenceCurve,
      VolatilityType volatilityType, VolatilityBootstrapMethod method)
    {
      var threadSafeData = data;
      var result = new CalibrationOutput[threadSafeData.Length];
      var rateOptionParams = RateOptionParamCollection.Factory(asOf, capMaturities, discountCurve, referenceCurve);
      Parallel.For(0, threadSafeData.Length, i =>
      {
        var volatilityCubeBootstrapInput = threadSafeData[i];
        try
        {
          result[i] = BootstrapVolatilityCurve(asOf, volatilityCubeBootstrapInput, volatilityType, rateOptionParams, method);
        }
        catch
        {
          throw new ToolkitException(
            String.Format(
              "Unable to bootstrap a volatility curve for strike {0} with smoothing parameter {1} for Euros and {2} for Caps . Try fitting with a larger smoothing parameter",
              volatilityCubeBootstrapInput.Strike,
              volatilityCubeBootstrapInput.LambdaEdf, volatilityCubeBootstrapInput.LambdaCap));

        }
      });
      Array.Sort(result, (o1, o2) => o1.Strike.CompareTo(o2.Strike));
      return result.ToDictionary(o => o.Strike);
    }

    /// <summary>
    /// Calculates the Atm Vol Curve by interpolating from surface (family of curves indexed by strike)
    /// </summary>
    /// <param name="asOf">As of date.</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="lastCapMaturity">Maturity of last calibrated cap</param>
    /// <param name="curves">Caplet vol curves for each strike.</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="method">The method.</param>
    /// <param name="strikeInterp">Strike interpolation.</param>
    /// <returns>Atm vol curve</returns>
    public static Curve BuildAtmVolCurve(Dt asOf, InterestRateIndex referenceIndex, DiscountCurve referenceCurve, Dt lastCapMaturity, Curve[] curves, double[] strikes, VolatilityBootstrapMethod method, Interp strikeInterp)
    {
      var volCurve = new Curve(asOf);
      if (method == VolatilityBootstrapMethod.PiecewiseQuadratic)
        volCurve.Interp = InterpFactory.FromMethod(InterpMethod.Quadratic, ExtrapMethod.Const);
      var capletSchedule = RateOptionParamCollection.GetPaymentSchedule(asOf, lastCapMaturity, referenceCurve, referenceIndex).ToArray();
      RateVolatilityCurveBuilder.BuildAtmVolCurve(curves, strikes, capletSchedule.Select(p => p.RateFixing).ToArray(), capletSchedule.Select(p => p.Rate).ToArray(), volCurve, strikeInterp);
      return volCurve;
    }

    #endregion

    #region private methods

    /// <summary>
    /// Bootstraps a volatility curve
    /// </summary>
    /// <param name="asOf">As of date.</param>
    /// <param name="input">The input.</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="rateOptionParams">The rate option params.</param>
    /// <param name="method">The method.</param>
    /// <returns></returns>
    private static CalibrationOutput BootstrapVolatilityCurve(
      Dt asOf, VolatilityCubeBootstrapInput input,
      VolatilityType volatilityType,
      RateOptionParamCollection rateOptionParams,
      VolatilityBootstrapMethod method)
    {
      var curve = new Curve(asOf)
      {
        Interp = (method == VolatilityBootstrapMethod.PiecewiseQuadratic)
          ? (InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const))
          : (InterpFactory.FromMethod(InterpMethod.Flat, ExtrapMethod.Const))
      };
      double[] fitErrors;
      switch (input.BootstrapInputType)
      {
      case BootstrapInputType.Euro:
        fitErrors = new double[input.FutureExpiries.Length];
        RateVolatilityCurveBuilder.BootstrapEDFCapletCurve(new double[0], new Dt[0], rateOptionParams, input.OptionPrices,
          input.FuturePrices, input.OptionTypes, input.FutureExpiries, input.Strike,
          input.LambdaEdf, input.LambdaCap, volatilityType, curve, fitErrors);
        break;
      case BootstrapInputType.Cap:
      {
        fitErrors = new double[input.CapMaturities.Length];
        if (method == VolatilityBootstrapMethod.PiecewiseQuadratic)
        {
          RateVolatilityCurveBuilder.BootstrapEDFCapletCurve(input.CapVols, input.CapMaturities, rateOptionParams, new double[0],
            new double[0], new int[0], new Dt[0], input.Strike, input.LambdaEdf, input.LambdaCap,
            volatilityType, curve, fitErrors);
        }
        else
        {
          RateVolatilityCurveBuilder.BootstrapFlatEDFCapletCurve(input.CapVols, input.CapMaturities, input.Strike, rateOptionParams, volatilityType, curve,
            fitErrors);
        }
      }
        break;
      case BootstrapInputType.CapAndEuro:
      {
        fitErrors = new double[input.CapMaturities.Length + input.FutureExpiries.Length];
        RateVolatilityCurveBuilder.BootstrapEDFCapletCurve(input.CapVols, input.CapMaturities, rateOptionParams, input.OptionPrices, input.FuturePrices,
          input.OptionTypes, input.FutureExpiries, input.Strike, input.LambdaEdf, input.LambdaCap,
          volatilityType, curve, fitErrors);
      }
        break;
      default:
        throw new ArgumentException(String.Format("Invalid Input bootstrap type {0}", input.BootstrapInputType));

      }

      return new CalibrationOutput(curve, input.Strike, fitErrors);
    }

    /// <summary>
    /// Bootstraps the flat caplet curve.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="capMaturities">Underlying cap maturities</param>
    /// <param name="capVols">Underlying cap volatility</param>
    /// <param name="strike">Strike</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="referenceCurve">Reference curve/index for each underlying cap</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="fitErrors">The fit errors.</param>
    /// <remarks>If more than one reference index is provided (i.e. 3M Libor and 6M Libor) then the calibrated index is that corresponding to the shortest tenor.
    /// The volatility of caplets with longer tenor is computed by standard freezing arguments from the relationship <m>1 + \Delta L^{\Delta} = \Pi_i (1 +\delta_i L_i^{\delta_i}) + B</m>
    /// where B is a deterministic basis and <m>L_i</m> are assumed perfectly correlated.
    /// </remarks>
    public static void BootstrapFlatCapletCurve(Dt asOf, Dt[] capMaturities, double[] capVols, double strike, DiscountCurve discountCurve, Func<Dt, Tuple<InterestRateIndex, DiscountCurve>> referenceCurve,
      VolatilityType volatilityType, out Curve curve, out double[] fitErrors)
    {
      var rateOptionParams = RateOptionParamCollection.Factory(asOf, capMaturities, discountCurve, referenceCurve);
      fitErrors = new double[capVols.Length];
      curve = new Curve(asOf);
      RateVolatilityCurveBuilder.BootstrapFlatEDFCapletCurve(capVols, capMaturities, strike, rateOptionParams, volatilityType, curve, fitErrors);
    }

    /// <summary>
    /// Bootstraps the atm caplet curve.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="capVols">The cap vols.</param>
    /// <param name="capMaturities">The cap maturities.</param>
    /// <param name="capStrikes">The cap strikes.</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="referenceCurve">Reference curve/index for each underlying cap</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="lambda">The lambda.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="fitErrors">The fit errors.</param>
    /// <remarks>If more than one reference index is provided (i.e. 3M Libor and 6M Libor) then the calibrated index is that corresponding to the shortest tenor.
    /// The volatility of caplets with longer tenor is computed by standard freezing arguments from the relationship <m>1 + \Delta L^{\Delta} = \Pi_i (1 +\delta_i L_i^{\delta_i}) + B</m>
    /// where B is a deterministic basis and <m>L_i</m> are assumed perfectly correlated.
    /// </remarks>
    public static void BootstrapAtmCapletCurve(Dt asOf, double[] capVols, Dt[] capMaturities, double[] capStrikes, DiscountCurve discountCurve, Func<Dt, Tuple<InterestRateIndex, DiscountCurve>> referenceCurve,
      VolatilityType volatilityType, double lambda, out Curve curve, out double[] fitErrors)
    {
      var rateOptionParams = RateOptionParamCollection.Factory(asOf, capMaturities, discountCurve, referenceCurve);
      curve = new Curve(asOf)
                {
                  Interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const)
                };
      fitErrors = new double[capVols.Length];
      RateVolatilityCurveBuilder.BootstrapAtmCapletCurve(capVols, capStrikes, capMaturities, rateOptionParams, lambda, volatilityType, curve, fitErrors);
    }
    #endregion
  }

  /// <summary>
  /// Test interface class for the RateVolatility Curve Builder
  /// </summary>
  public class TestRateVolatilityCurveBuilderUtil
  {
    /// <summary>
    /// Test method used for testing the cap pvs , should we move it to a seperate utility class ?
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="vols"></param>
    /// <param name="maturities"></param>
    /// <param name="dc"></param>
    /// <param name="cap"></param>
    /// <param name="volType"></param>
    /// <param name="pvs"></param>
    /// <param name="vegas"></param>
    public static void CalculateCapMarketPvs(Dt asOf, double[] vols, Dt[] maturities, DiscountCurve dc, Cap cap, VolatilityType volType, out double[] pvs, out double[] vegas)
    {
      pvs = new double[vols.Length];
      vegas = new double[vols.Length];
      var rateOptionParams = RateOptionParamCollection.Factory(asOf, maturities, dc, dt => new Tuple<InterestRateIndex, DiscountCurve>(cap.RateIndex, dc));
      RateVolatilityCurveBuilder.CalculateCapPvs(vols, cap.Strike, rateOptionParams, pvs, vegas, volType);
    }

    /// <summary>
    /// Test method that exposes the 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="vols"></param>
    /// <param name="maturities"></param>
    /// <param name="dc"></param>
    /// <param name="cap"></param>
    /// <param name="volType"></param>
    /// <param name="guesses"></param>
    public static void CalculateCapPcGuesses(Dt asOf, double[] vols, Dt[] maturities, DiscountCurve dc, Cap cap, VolatilityType volType, out double[] guesses)
    {
      guesses = new double[vols.Length];
      var curve = new Curve(asOf);
      var rateOptionParams = RateOptionParamCollection.Factory(asOf, maturities, dc, dt => new Tuple<InterestRateIndex, DiscountCurve>(cap.RateIndex, dc));
      RateVolatilityCurveBuilder.BootstrapFlatEDFCapletCurve(vols, maturities, cap.Strike, rateOptionParams, volType, curve, guesses);
      for (int i = 0; i < guesses.Length; ++i)
        guesses[i] = curve.GetVal(i);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="vols"></param>
    /// <param name="maturities"></param>
    /// <param name="dc"></param>
    /// <param name="cap"></param>
    /// <param name="volType"></param>
    /// <param name="lambda"></param>
    /// <param name="curve"></param>
    /// <param name="fitErrors"></param>
    public static void BootstrapCapletCurve(Dt asOf, double[] vols, Dt[] maturities, DiscountCurve dc, Cap cap, VolatilityType volType, double lambda, out Curve curve, out double[] fitErrors)
    {
      var rateOptionParams = RateOptionParamCollection.Factory(asOf, maturities, dc, dt => new Tuple<InterestRateIndex, DiscountCurve>(cap.RateIndex, dc));
      curve = new Curve(asOf)
                {
                  Interp = InterpFactory.FromMethod(InterpMethod.PCHIP, ExtrapMethod.Const)
                };
      fitErrors = new double[maturities.Length];
      RateVolatilityCurveBuilder.BootstrapEDFCapletCurve(vols, maturities, rateOptionParams, new double[0], new double[0], new int[0], new Dt[0], cap.Strike,
                                                         0.0, lambda, volType, curve, fitErrors);
    }


    /// <summary>
    /// Helper method that gets the rate option param table
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="maturity"></param>
    /// <param name="discountCurve"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static DataTable RateOptionParamTable(Dt asOf, Dt maturity, DiscountCurve discountCurve, Cap parameters)
    {
      var dt = new DataTable();
      dt.Columns.Add(new DataColumn("Date", typeof(Dt)));
      dt.Columns.Add(new DataColumn("Rate", typeof(double)));
      dt.Columns.Add(new DataColumn("Fraction", typeof(double)));
      dt.Columns.Add(new DataColumn("Level", typeof(double)));

      foreach (var el in RateOptionParamCollection.GetPaymentSchedule(asOf, maturity, discountCurve, parameters.RateIndex))
      {
        DataRow row = dt.NewRow();
        row[0] = el.RateFixing;
        row[1] = el.Rate;
        row[2] = Dt.Fraction(asOf, el.Expiry, parameters.DayCount);
        row[3] = discountCurve.DiscountFactor(el.PayDt) * el.PeriodFraction;
        dt.Rows.Add(row);
      }
      return dt;
    }
  }
}
