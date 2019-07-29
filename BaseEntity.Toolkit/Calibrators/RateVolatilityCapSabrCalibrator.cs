/*
 * RateVolatilityCapSabrCalibrator.cs
 *
 *   2005-2011. All rights reserved.
 * 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Calibrates a Forward Volatility Cube to the SABR model using market quoted cap and euro
  /// dollar volatilities
  /// </summary>
  [Serializable]
  public class RateVolatilityCapSabrCalibrator : RateVolatilitySabrCalibrator, IVolatilityTenorsProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor for the Forward Volatility Cube SABR Calibrator
    /// </summary>
    /// <param name = "asOf">As of Date of the Cube</param>
    /// <param name = "settle">Settle Date </param>
    /// <param name = "dc">discount Curve</param>
    /// <param name = "projectionCurveSelector">projection curve (possibly one for each cap maturity)</param>
    /// <param name = "projectionIndexSelector">projection index (possibly one for each cap maturity)</param>
    /// <param name = "volatilityType">volatility type ( Normal/LogNormal)</param>
    /// <param name = "edfExpiries">Euro Dolalr Future Expiory Dates </param>
    /// <param name = "edfStrikes">EDF Option Strikes </param>
    /// <param name = "edfPrices">EDF Prices</param>
    /// <param name = "edCallOptionPrices">ED Call option prices</param>
    /// <param name = "edPutOptionPrices">ED Put Option prices </param>
    /// <param name = "capExpiries">CapExpiry Dates </param>
    /// <param name = "capStrikes">Cap Strikes </param>
    /// <param name = "capVols">Cap Volatilities matrix </param>
    /// <param name = "lambdaEdfs">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Euro Dollar Fit </param>
    /// <param name = "lambdaCaps">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Cap Fit </param>
    /// <param name = "method">Volatility Cube Bootstrap method</param>
    /// <param name = "lowerBounds">Lower bounds </param>
    /// <param name = "upperBounds">Upper bounds </param>
    /// <param name = "inputBeta">Beta Curve(as input) </param>
    /// <param name = "guessAlpha">Guess Value of ALpha</param>
    /// <param name = "guessRho">Guess Value of Rho</param>
    /// <param name = "guessNu">Guess Value of Nu</param>
    /// <remarks>If more than one reference index is provided (i.e. 3M Libor and 6M Libor) then the calibrated index is that corresponding to the shortest tenor.
    /// The volatility of caplets with longer tenor is computed by standard freezing arguments from the relationship <m>1 + \Delta L^{\Delta} = \Pi_i (1 +\delta_i L_i^{\delta_i}) + B</m>
    /// where B is a deterministic basis and <m>L_i</m> are assumed perfectly correlated.
    /// </remarks>
    public RateVolatilityCapSabrCalibrator(
      Dt asOf, Dt settle, DiscountCurve dc, Func<Dt, InterestRateIndex> projectionIndexSelector, Func<Dt, DiscountCurve> projectionCurveSelector,
      VolatilityType volatilityType,
      Dt[] edfExpiries, double[] edfStrikes, double[] edfPrices,
      double[,] edCallOptionPrices, double[,] edPutOptionPrices, Dt[] capExpiries,
      double[] capStrikes, double[,] capVols, double[] lambdaEdfs, double[] lambdaCaps,
      VolatilityBootstrapMethod method, double[] lowerBounds, double[] upperBounds,
      Curve inputBeta, Curve guessAlpha, Curve guessRho, Curve guessNu)
      : this(asOf, settle, dc, projectionIndexSelector, projectionCurveSelector, volatilityType,
        edfExpiries, edfStrikes, edfPrices, edCallOptionPrices, edPutOptionPrices,
        null, capExpiries, capStrikes, capVols, lambdaEdfs, lambdaCaps, method,
        lowerBounds, upperBounds, inputBeta, guessAlpha, guessRho, guessNu)
    {}

    /// <summary>
    /// Constructor for the Forward Volatility Cube SABR Calibrator
    /// </summary>
    /// <param name = "asOf">As of Date of the Cube</param>
    /// <param name = "settle">Settle Date </param>
    /// <param name = "dc">discount Curve</param>
    /// <param name = "projectionCurveSelector">projection curve (possibly one for each cap maturity)</param>
    /// <param name = "projectionIndexSelector">projection index (possibly one for each cap maturity)</param>
    /// <param name = "volatilityType">volatility type ( Normal/LogNormal)</param>
    /// <param name = "edfExpiries">Euro Dollar Future Expiry Dates </param>
    /// <param name = "edfStrikes">EDF Option Strikes </param>
    /// <param name = "edfPrices">EDF Prices</param>
    /// <param name = "edCallOptionPrices">ED Call option prices</param>
    /// <param name = "edPutOptionPrices">ED Put Option prices </param>
    /// <param name = "capTenors">The tenor names by cap expiry dates </param>
    /// <param name = "capExpiries">The cap expiry dates</param>
    /// <param name = "capStrikes">Cap Strikes </param>
    /// <param name = "capVols">Cap Volatilities matrix </param>
    /// <param name = "lambdaEdfs">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Euro Dollar Fit </param>
    /// <param name = "lambdaCaps">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Cap Fit </param>
    /// <param name = "method">Volatility Cube Bootstrap method</param>
    /// <param name = "lowerBounds">Lower bounds </param>
    /// <param name = "upperBounds">Upper bounds </param>
    /// <param name = "inputBeta">Beta Curve(as input) </param>
    /// <param name = "guessAlpha">Guess Value of ALpha</param>
    /// <param name = "guessRho">Guess Value of Rho</param>
    /// <param name = "guessNu">Guess Value of Nu</param>
    /// <remarks>If more than one reference index is provided (i.e. 3M Libor and 6M Libor) then the calibrated index is that corresponding to the shortest tenor.
    /// The volatility of caplets with longer tenor is computed by standard freezing arguments from the relationship <m>1 + \Delta L^{\Delta} = \Pi_i (1 +\delta_i L_i^{\delta_i}) + B</m>
    /// where B is a deterministic basis and <m>L_i</m> are assumed perfectly correlated.
    /// </remarks>
    /// <exception cref="ToolkitException">cap tenors and expiries not match</exception>
    public RateVolatilityCapSabrCalibrator(
      Dt asOf, Dt settle, DiscountCurve dc, Func<Dt, InterestRateIndex> projectionIndexSelector, Func<Dt, DiscountCurve> projectionCurveSelector,
      VolatilityType volatilityType,
      Dt[] edfExpiries, double[] edfStrikes, double[] edfPrices,
      double[,] edCallOptionPrices, double[,] edPutOptionPrices, string[] capTenors, Dt[] capExpiries,
      double[] capStrikes, double[,] capVols, double[] lambdaEdfs, double[] lambdaCaps,
      VolatilityBootstrapMethod method, double[] lowerBounds, double[] upperBounds,
      Curve inputBeta, Curve guessAlpha, Curve guessRho, Curve guessNu)
      : base(
        asOf, dc, TargetCurve(capExpiries, projectionIndexSelector, projectionCurveSelector), TargetIndex(capExpiries, projectionIndexSelector), volatilityType,
        null, null, null)
    {
      if (capTenors == null || capTenors.Length == 0)
        capTenors_ = null;
      else if (capTenors.Length!=capExpiries.Length)
        throw new ToolkitException("cap tenors and expiries not match");
      else
        capTenors_ = capTenors;

      //Set the value of the maturities and strikes 
      edfMaturities_ = edfExpiries;
      edfutureStrikes_ = edfStrikes;
      edfuturePrices_ = edfPrices;
      capExpiries_ = capExpiries;
      capStrikes_ = capStrikes;
      method_ = method;
      lowerBounds_ = lowerBounds;
      upperBounds_ = upperBounds;
      settle_ = settle;
      inputBeta_ = inputBeta;
      guessAlpha_ = guessAlpha;
      guessRho_ = guessRho;
      guessNu_ = guessNu;

      //If we input Normal vols => we first convert them to LogNormal vols before doing the calibration
      var capVolatilities = (volatilityType == VolatilityType.Normal)
                              ? ConvertCapVols(capVols, capStrikes, capExpiries)
                              : capVols;

      double[] strikes;
      InitializeBootstrapInputs(edCallOptionPrices, edPutOptionPrices,
                                edfStrikes, edfPrices, edfExpiries, capStrikes, capVolatilities,
                                capExpiries, lambdaEdfs, lambdaCaps, out volCubeInputDict_, out strikes);
      //Validate the inputs

      //Initialize the Expiry Dates and Strikes 
      var edfExpDates = (edfExpiries.Length > 0)
                          ? GenerateIntermediateMaturities(edfExpiries)
                          : new Dt[] { };
      var capExpDates = GenerateIntermediateMaturities(capExpiries);
      Expiries = SortedMerge(edfExpDates, capExpDates);
      Dates = Expiries;
      Strikes = strikes;
      if ((capExpiries != null) && (capExpiries.Select(projectionIndexSelector).Distinct().Count() > 1))
      {
        projectionCurves_ = new Dictionary<Dt, Tuple<InterestRateIndex, DiscountCurve>>();
        foreach (var dt in capExpiries)
          projectionCurves_[dt] = new Tuple<InterestRateIndex, DiscountCurve>(projectionIndexSelector(dt), projectionCurveSelector(dt));
      }
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name = "errors">ArrayList of errors to fill</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      //Validation method for the errors 
      foreach (var input in volCubeInputDict_)
      {
        if (input.FuturePrices.Length != input.FutureExpiries.Length)
          InvalidValue.AddError(errors, this,
                                String.Format("Future Prices and expiries length should match for strike {0}",
                                              input.Strike));
        if ((input.FuturePrices.Length != input.OptionPrices.Length) ||
            (input.FuturePrices.Length != input.OptionTypes.Length))
          InvalidValue.AddError(errors, this,
                                String.Format("OPtion Prices  and Future Prices length should match for strike {0}",
                                              input.Strike));

        if (input.CapMaturities.Length != input.CapVols.Length)
          InvalidValue.AddError(errors, this,
                                String.Format(
                                  "Number of Cap Vols should match number of cap maturities for strike {0}",
                                  input.Strike));

        foreach (var vol in input.CapVols)
        {
          if (vol <= 0.0)
            InvalidValue.AddError(errors, this,
                                  String.Format("Cap Volatility Cannot be negative or zero  {0}", vol));
        }

        foreach (var quote in input.OptionPrices)
        {
          if (quote <= 0.0)
            InvalidValue.AddError(errors, this,
                                  String.Format("EDF Option Prices cannot be negative or zero {0}",
                                                quote));
        }
      }
    }

    /// <summary>
    ///   Fit the cube
    /// </summary>
    /// <param name = "surface">The cube.</param>
    /// <param name = "fromIdx">From idx.</param>
    public override void FitFrom(CalibratedVolatilitySurface surface, int fromIdx)
    {
      //Bootstrap the caplet volatility as a LogNormal vol 
      Dictionary<double, CalibrationOutput> output = CapletVolatilitiesBootstrapper.BootstrapVolatilitySurface(volCubeInputDict_, AsOf, capExpiries_,
                                                                                                               DiscountCurve,
                                                                                                               dt =>
                                                                                                               (projectionCurves_ == null)
                                                                                                                 ? new Tuple<InterestRateIndex, DiscountCurve>(
                                                                                                                     RateIndex, RateProjectionCurve)
                                                                                                                 : projectionCurves_[dt],
                                                                                                               VolatilityType.LogNormal,
                                                                                                               VolatilityBootstrapMethod.PiecewiseQuadratic);
      var capletCurves = new Curve[capStrikes_.Length];
      for (int i = 0; i < capletCurves.Length; i++) capletCurves[i] = output[capStrikes_[i]].Curve;
      //Fit SABR parameters to caplet vols
      Curve[] calibCurves = (IncludeEuros)
                              ? CapSabrCalibrator.SabrCalibrateCaplets(output, AsOf, RateProjectionCurve, RateIndex, capExpiries_, edfMaturities_,
                                                                       edfuturePrices_,
                                                                       edfutureStrikes_, capStrikes_, inputBeta_, 0.4,
                                                                       0.3, lowerBounds_, upperBounds_, guessAlpha_,
                                                                       guessRho_, guessNu_)
                              : CapSabrCalibrator.SabrCalibrateCaplets(output, AsOf, RateProjectionCurve, RateIndex, capExpiries_, capStrikes_, inputBeta_, 0.4,
                                                                       0.3,
                                                                       lowerBounds_, upperBounds_, guessAlpha_, guessRho_, guessNu_);

      betaCurve_ = calibCurves[0];
      alphaCurve_ = calibCurves[1];
      rhoCurve_ = calibCurves[2];
      nuCurve_ = calibCurves[3];

      // Read bootstrapped curves into fwd-fwd vol cube
      var fwdVols = ((RateVolatilityCube)surface).FwdVols;
      for (int i = 0; i < Strikes.Length; i++)
      {
        for (int j = 0; j < Expiries.Length; j++)
        {
          Dt tenorDate = Dt.Roll(Dt.Add(Expiries[j], RateIndex.IndexTenor), RateIndex.Roll, RateIndex.Calendar);
          double forwardRate = DiscountCurve.F(Expiries[j], tenorDate, RateIndex.DayCount, Frequency.None);

          double vol = CapletVolatility(Expiries[j], forwardRate, Strikes[i]);

          for (int k = 0; k < Dates.Length; k++)
          {
            fwdVols.AddVolatility(k, j, i, vol);
          }
        }
      }

      // Set this calibrator as the cube's interpolator
      ((RateVolatilityCube)surface).RateVolatilityInterpolator = this;
    }

    IEnumerable<IVolatilityTenor> IVolatilityTenorsProvider.EnumerateTenors()
    {
      return EnumerateCapTenors(
        AsOf, volCubeInputDict_, capExpiries_, capTenors_);
    }
    #endregion Methods

    #region Private Methods

    private static void InitializeBootstrapInputs(double[,] callOptionPrices, double[,] putOptionPrices,
                                                  double[] edFutureStrikes, double[] edFuturePrices, Dt[] edfMaturities,
                                                  double[] capStrikes, double[,] capVolatilities, Dt[] capMaturities,
                                                  double[] lambdaEdfs, double[] lambdaCaps,
                                                  out VolatilityCubeBootstrapInput[] inputs,
                                                  out double[] strikes)
    {
      var dict = new Dictionary<double, VolatilityCubeBootstrapInput>();

      //First input all the EDF products in to the dictionary 
      for (int i = 0; i < edFutureStrikes.Length; i++)
      {
        double[] optionPrices;
        int[] optionTypes;
        GetEdfOptionPrices(callOptionPrices, putOptionPrices, edFutureStrikes, edFuturePrices, i, out optionPrices,
                           out optionTypes);
        var cleanEdfMaturities = new List<Dt>();
        var cleanOptionPrices = new List<double>();
        var cleanOptionTypes = new List<int>();
        var cleanFuturePrices = new List<double>();

        //Ensure that we ignore the 0 quotes for the Euro Dollar Options 
        for (int idx = 0; idx < optionPrices.Length; idx++)
        {
          if (optionPrices[idx] > 0.0)
          {
            cleanEdfMaturities.Add(edfMaturities[idx]);
            cleanOptionPrices.Add(optionPrices[idx]);
            cleanOptionTypes.Add(optionTypes[idx]);
            cleanFuturePrices.Add(edFuturePrices[idx]);
          }
        }

        var bsInput = new VolatilityCubeBootstrapInput(edFutureStrikes[i], cleanOptionPrices.ToArray(),
                                                       cleanOptionTypes.ToArray(), cleanFuturePrices.ToArray(),
                                                       cleanEdfMaturities.ToArray(), new double[] { }, new Dt[] { },
                                                       lambdaEdfs[i], 0.0) { BootstrapInputType = BootstrapInputType.Euro };
        dict.Add(edFutureStrikes[i], bsInput);
      }

      //Now , input the caplets 
      for (int i = 0; i < capStrikes.Length; i++)
      {
        var capVols = new double[capMaturities.Length];
        for (int j = 0; j < capVols.Length; j++) capVols[j] = capVolatilities[j, i];

        //If there is an intersection 
        if (dict.ContainsKey(capStrikes[i]))
        {
          VolatilityCubeBootstrapInput bsInput = dict[capStrikes[i]];
          bsInput.CapMaturities = capMaturities;
          bsInput.CapVols = capVols;
          bsInput.LambdaCap = lambdaCaps[i];
          bsInput.BootstrapInputType = BootstrapInputType.CapAndEuro;
        }
        else
        {
          var bsInput = new VolatilityCubeBootstrapInput(capStrikes[i], new double[0], new int[0], new double[0], new Dt[0], capVols, capMaturities, 0.0,
                                                         lambdaCaps[i]) { BootstrapInputType = BootstrapInputType.Cap };
          dict.Add(capStrikes[i], bsInput);
        }
      }
      strikes = dict.Keys.ToArray();
      Array.Sort(strikes);
      inputs = strikes.Select(s => dict[s]).ToArray();
    }

    private static DiscountCurve TargetCurve(Dt[] capMaturities, Func<Dt, InterestRateIndex> indexSelector, Func<Dt, DiscountCurve> curveSelector)
    {
      if (capMaturities == null || capMaturities.Length == 0)
        return null;
      var index = indexSelector(capMaturities[0]);
      var curve = curveSelector(capMaturities[0]);
      for (int i = 0; ++i < capMaturities.Length; )
      {
        var pi = indexSelector(capMaturities[i]);
        var pc = curveSelector(capMaturities[i]);
        if (pi.IndexTenor.Days < index.IndexTenor.Days)
        {
          index = pi;
          curve = pc;
        }
      }
      return curve;
    }

    private static InterestRateIndex TargetIndex(Dt[] capMaturities, Func<Dt, InterestRateIndex> selector)
    {
      if (capMaturities == null || capMaturities.Length == 0)
        return selector(Dt.Empty);
      var retVal = selector(capMaturities[0]);
      for (int i = 0; ++i < capMaturities.Length; )
      {
        var pi = selector(capMaturities[i]);
        if (pi.IndexTenor.Days < retVal.IndexTenor.Days)
          retVal = pi;
      }
      return retVal;
    }


    private static void GetEdfOptionPrices(double[,] callOptionPrices, double[,] putOptionPrices,
                                           double[] edfutureStrikes, double[] edfuturePrices, int strikeIdx,
                                           out double[] optionPrices, out int[] optionTypes)
    {
      int rows = callOptionPrices.GetUpperBound(0) + 1;
      optionPrices = new double[rows];
      optionTypes = new int[rows];
      double strike = edfutureStrikes[strikeIdx];
      for (int i = 0; i < optionPrices.Length; i++)
      {
        bool callOrPut = (edfuturePrices[i] > strike);

        optionPrices[i] = (callOrPut) ? putOptionPrices[i, strikeIdx] : callOptionPrices[i, strikeIdx];
        optionTypes[i] = (callOrPut) ? 1 : 2;
      }
    }

    /// <summary>
    ///   static helper method that gets the intermediate maturities
    /// </summary>
    /// <param name = "input"></param>
    /// <returns></returns>
    private static Dt[] GenerateIntermediateMaturities(Dt[] input)
    {
      var result = new Dt[2 * input.Length - 1];
      for (int i = 0; i < input.Length; i++) result[2 * i] = input[i];
      for (int i = 2; i <= input.Length; i++)
      {
        int diff = Dt.Diff(result[2 * i - 4], result[2 * i - 2]);
        int daysToAdd = (diff / 2);
        result[2 * i - 3] = Dt.AddDays(result[2 * i - 4], daysToAdd, Calendar.None);
      }
      return result;
    }

    /// <summary>
    ///   static helper method that merges two pre-sorted arrays in to a combined array
    /// </summary>
    /// <param name = "array1"></param>
    /// <param name = "array2"></param>
    /// <returns></returns>
    private static Dt[] SortedMerge(Dt[] array1, Dt[] array2)
    {
      int i = 0, j = 0, k = 0;

      int m = array1.Length;
      int n = array2.Length;
      var result = new Dt[array1.Length + array2.Length];
      while ((i < m) && (j < n))
      {
        if (Dt.Cmp(array1[i], array2[j]) < 0)
        {
          result[k] = array1[i];
          k++;
          i++;
        }
        else
        {
          result[k] = array2[j];
          k++;
          j++;
        }
      }

      while (i < m)
      {
        result[k] = array1[i];
        k++;
        i++;
      }

      while (j < n)
      {
        result[k] = array2[j];
        k++;
        j++;
      }
      return result;
    }

    private double[,] ConvertCapVols(double[,] capInputVols, double[] capStrikes, Dt[] capMaturities)
    {
      var result = new double[capInputVols.GetLength(0), capStrikes.Length];
      // Get start date
      for (int i = 0; i < result.GetLength(0); i++)
      {
        var index = (projectionCurves_ != null) ? projectionCurves_[capMaturities[i]].Item1 : RateIndex;
        Dt effective = Cap.StandardEffective(settle_, index);
        for (int j = 0; j < capStrikes.Length; j++)
        {
          var newCap = new Cap(effective, capMaturities[i], index.Currency, CapFloorType.Cap, capStrikes[j],
                               index.DayCount, index.IndexTenor.ToFrequency(), index.Roll, index.Calendar) { AccrueOnCycle = true };
          result[i, j] = LogNormalToNormalConverter.ConvertCapVolatility(AsOf, effective, DiscountCurve, newCap, capInputVols[i, j],
                                                                         new List<RateReset>(), VolatilityType.Normal,
                                                                         VolatilityType.LogNormal);
        }
      }
      return result;
    }

    #endregion Private Methods

    #region Properties

    /// <summary>
    ///   Include EuroDollar Future options
    /// </summary>
    public bool IncludeEuros { get; set; }

    /// <summary>
    ///   Cap/Floor strikes.
    /// </summary>
    public override double[] CapStrikes
    {
      get { return capStrikes_; }
    }

    ///<summary>
    /// Strip method
    ///</summary>
    public VolatilityBootstrapMethod StripMethod
    {
      get { return method_; }
    }

    #endregion Properties

    #region Data

    private readonly string[] capTenors_;
    private readonly Dt[] capExpiries_;
    private readonly double[] capStrikes_;
    private readonly Dt[] edfMaturities_;
    private readonly double[] edfuturePrices_;
    private readonly double[] edfutureStrikes_;
    private readonly Curve guessAlpha_;
    private readonly Curve guessNu_;
    private readonly Curve guessRho_;
    private readonly Curve inputBeta_;
    private readonly double[] lowerBounds_;
    private readonly VolatilityBootstrapMethod method_;
    private readonly double[] upperBounds_;
    private readonly VolatilityCubeBootstrapInput[] volCubeInputDict_;
    private readonly Dt settle_;
    private readonly Dictionary<Dt, Tuple<InterestRateIndex, DiscountCurve>> projectionCurves_;

    #endregion Data
  }
}