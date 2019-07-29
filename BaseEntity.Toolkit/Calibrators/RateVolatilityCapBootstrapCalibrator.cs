/*
 * RateVolatilityCapBootstrapCalibrator.cs
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
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Enum indicating the type of input that is
  /// </summary>
  public enum BootstrapInputType
  {
    /// <summary>
    ///   Only Caps
    /// </summary>
    Cap,
    /// <summary>
    ///   Only Euro Dollar Options
    /// </summary>
    Euro,
    /// <summary>
    ///   Both Caps and Euro Dollar Options
    /// </summary>
    CapAndEuro
  }


  /// <summary>
  ///   Class that holds the input data to be passed into the rate volatility calibrator 
  /// </summary>
  [Serializable]
  public class VolatilityCubeBootstrapInput
  {
    /// <summary>
    ///   Constructor for the Volatility cube bootstrap input method
    /// </summary>
    /// <param name = "strike"></param>
    /// <param name = "optionPrices"></param>
    /// <param name = "optionTypes"></param>
    /// <param name = "futurePrices"></param>
    /// <param name = "futureExpiries"></param>
    /// <param name = "capVols"></param>
    /// <param name = "capMaturities"></param>
    /// <param name = "lambdaEdf"></param>
    /// <param name = "lambdaCap"></param>
    public VolatilityCubeBootstrapInput(double strike, double[] optionPrices, int[] optionTypes, double[] futurePrices,
                                        Dt[] futureExpiries, double[] capVols, Dt[] capMaturities, double lambdaEdf,
                                        double lambdaCap)
    {
      Strike = strike;
      OptionPrices = optionPrices;
      OptionTypes = optionTypes;
      FuturePrices = futurePrices;
      FutureExpiries = futureExpiries;
      CapVols = capVols;
      CapMaturities = capMaturities;
      LambdaEdf = lambdaEdf;
      LambdaCap = lambdaCap;
    }

    #region properties

    /// <summary>
    ///   Strike
    /// </summary>
    public double Strike { get; set; }

    /// <summary>
    ///   Option Prices
    /// </summary>
    public double[] OptionPrices { get; set; }

    /// <summary>
    ///   Option Types
    /// </summary>
    public int[] OptionTypes { get; set; }

    /// <summary>
    ///   Future Prices
    /// </summary>
    public double[] FuturePrices { get; set; }

    /// <summary>
    ///   Future Expiries
    /// </summary>
    public Dt[] FutureExpiries { get; set; }

    /// <summary>
    ///   Cap Volatilities
    /// </summary>
    public double[] CapVols { get; set; }

    /// <summary>
    ///   Cap Maturities
    /// </summary>
    public Dt[] CapMaturities { get; set; }

    /// <summary>
    ///   Smoothing parameters for the Euro Dollar Option
    /// </summary>
    public double LambdaEdf { get; set; }

    /// <summary>
    ///   Smoothing for the Caplet
    /// </summary>
    public double LambdaCap { get; set; }

    /// <summary>
    ///   Bootstrap Input Type
    /// </summary>
    public BootstrapInputType BootstrapInputType { get; set; }

    #endregion
  }

  /// <summary>
  ///   Calibrates a forward volatility cube from a set of cap volatility quotes.
  /// </summary>
  [Serializable]
  public class RateVolatilityCapBootstrapCalibrator : RateVolatilityCalibrator, IVolatilityTenorsProvider
  {
    #region Constructors

    /// <summary>
    ///   Constructor for the RateVolatilityCapBootstrap Calibrator
    /// </summary>
    /// <param name = "asOf">As of date </param>
    /// <param name = "settle">Settle date </param>
    /// <param name = "dc">Discount Curve </param>
    /// <param name = "projectionCurveSelector">projection curve (possibly one for each cap maturity)</param>
    /// <param name = "projectionIndexSelector">projection index (possibly one for each cap maturity)</param>
    /// <param name = "volatilityType">Volatility Type(Normal/LogNormal)</param>
    /// <param name = "edfExpiries">Euro Dollar Future Expiry Dates</param>
    /// <param name = "edfStrikes">EDF Option Strikes</param>
    /// <param name = "edfPrices">ED Future Prices</param>
    /// <param name = "edCallOptionPrices">ED Call Option Prices</param>
    /// <param name = "edPutOptionPrices">ED Put Option Prices</param>
    /// <param name = "capMaturities">Cap Expiry Dates </param>
    /// <param name = "capStrikes"></param>
    /// <param name = "capVols">Cap Strikes </param>
    /// <param name = "lambdaEdfs">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Euro Dollar Fit </param>
    /// <param name = "lambdaCaps">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Cap Fit </param>
    /// <param name = "method">Volatility Bootstrap method</param>
    public RateVolatilityCapBootstrapCalibrator(
      Dt asOf,
      Dt settle,
      DiscountCurve dc,
      Func<Dt, InterestRateIndex> projectionIndexSelector,
      Func<Dt, DiscountCurve> projectionCurveSelector,
      VolatilityType volatilityType,
      Dt[] edfExpiries,
      double[] edfStrikes,
      double[] edfPrices,
      double[,] edCallOptionPrices,
      double[,] edPutOptionPrices,
      Dt[] capMaturities,
      double[] capStrikes,
      double[,] capVols,
      double[] lambdaEdfs,
      double[] lambdaCaps,
      VolatilityBootstrapMethod method)
      : this(asOf, settle, dc, projectionIndexSelector, projectionCurveSelector, volatilityType,
        edfExpiries, edfStrikes, edfPrices, edCallOptionPrices, edPutOptionPrices,
        null, capMaturities, capStrikes, capVols, lambdaEdfs, lambdaCaps, method)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="RateVolatilityCapBootstrapCalibrator" /> class.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="dc">Discount Curve</param>
    /// <param name="projectionIndexSelector">projection index (possibly one for each cap maturity)</param>
    /// <param name="projectionCurveSelector">projection curve (possibly one for each cap maturity)</param>
    /// <param name="volatilityType">Volatility Type(Normal/LogNormal)</param>
    /// <param name="edfExpiries">Euro Dollar Future Expiry Dates</param>
    /// <param name="edfStrikes">EDF Option Strikes</param>
    /// <param name="edfPrices">ED Future Prices</param>
    /// <param name="edCallOptionPrices">ED Call Option Prices</param>
    /// <param name="edPutOptionPrices">ED Put Option Prices</param>
    /// <param name="capTenors">The tenor names by cap maturities.</param>
    /// <param name="capMaturities">Cap Expiry Dates</param>
    /// <param name="capStrikes">The cap strikes.</param>
    /// <param name="capVols">Cap Strikes</param>
    /// <param name="lambdaEdfs">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Euro Dollar Fit</param>
    /// <param name="lambdaCaps">Penalty on caplet vol curvature <m>\frac{\partial^2 \sigma}{\partial T^2}</m> for Cap Fit</param>
    /// <param name="method">Volatility Bootstrap method</param>
    /// <exception cref="ToolkitException">cap tenors and expiries not match</exception>
    public RateVolatilityCapBootstrapCalibrator(
      Dt asOf,
      Dt settle,
      DiscountCurve dc,
      Func<Dt, InterestRateIndex> projectionIndexSelector,
      Func<Dt, DiscountCurve> projectionCurveSelector,
      VolatilityType volatilityType,
      Dt[] edfExpiries,
      double[] edfStrikes,
      double[] edfPrices,
      double[,] edCallOptionPrices,
      double[,] edPutOptionPrices,
      string[] capTenors,
      Dt[] capMaturities,
      double[] capStrikes,
      double[,] capVols,
      double[] lambdaEdfs,
      double[] lambdaCaps,
      VolatilityBootstrapMethod method)
      : base(asOf, dc, TargetCurve(capMaturities, projectionIndexSelector, projectionCurveSelector), TargetIndex(capMaturities, projectionIndexSelector), volatilityType, null, null, null)
    {
      if (capTenors == null || capTenors.Length == 0)
        capTenors_ = null;
      else if (capTenors.Length != capMaturities.Length)
        throw new ToolkitException("cap tenors and expiries not match");
      else
        capTenors_ = capTenors;

      //now initialize the Expiries and strikes int he base class 
      capExpiries_ = capMaturities;
      capStrikes_ = capStrikes;
      method_ = method;
      settle_ = settle;
      //Initialize all the bootstrap inputs 
      double[] strikes;
      InitializeBootstrapInputs(edCallOptionPrices, edPutOptionPrices, edfStrikes, edfPrices, edfExpiries, capStrikes,
                                capVols, capMaturities, lambdaEdfs, lambdaCaps, out volCubeInputDict_, out strikes);

      //Initialize the Expiry Dates and Strikes 
      Dt[] edfExpDates = (edfExpiries.Length > 0) ? GenerateIntermediateMaturities(edfExpiries) : new Dt[0];
      Dt[] capExpDates = GenerateIntermediateMaturities(capMaturities);
      Expiries = SortedMerge(edfExpDates, capExpDates);
      Dates = Expiries;
      Strikes = strikes;
      //Finally Initialize the First Cap Index
      firstCapIdx_ = new int[Strikes.Length];
      int idx = 0;
      int firstCapExpiry = Array.BinarySearch(Expiries, capExpDates[0]);
      foreach (var input in volCubeInputDict_)
      {
        if (input.BootstrapInputType == BootstrapInputType.CapAndEuro ||
            input.BootstrapInputType == BootstrapInputType.Euro)
        {
          firstCapIdx_[idx++] = 0;
        }
        else
        {
          firstCapIdx_[idx++] = firstCapExpiry;
        }
      }
      if ((capMaturities != null) && (capMaturities.Select(projectionIndexSelector).Distinct().Count() > 1))
      {
        projectionCurves_ = new Dictionary<Dt, Tuple<InterestRateIndex, DiscountCurve>>();
        foreach (var dt in capMaturities)
          projectionCurves_[dt] = new Tuple<InterestRateIndex, DiscountCurve>(projectionIndexSelector(dt), projectionCurveSelector(dt));
      }
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name = "errors">ArrayList for errors</param>
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
    /// Fit the cube
    /// </summary>
    /// <param name="surface">The cube.</param>
    /// <param name="fromIdx">From idx.</param>
    public override void FitFrom(CalibratedVolatilitySurface surface, int fromIdx)
    {
      //set the is flat cube field 
      var fwdVols = ((RateVolatilityCube)surface).FwdVols;
      if (method_ == VolatilityBootstrapMethod.PiecewiseConstant)
        fwdVols.Native.SetIsFlatCube(true);
      var output = CapletVolatilitiesBootstrapper.BootstrapVolatilitySurface(
        volCubeInputDict_, AsOf, capExpiries_, DiscountCurve,
        dt => (projectionCurves_ == null)
          ? new Tuple<InterestRateIndex, DiscountCurve>(
            RateIndex, RateProjectionCurve)
          : projectionCurves_[dt],
        VolatilityType, method_);

      //Get the Bootstrapped Curves for each strike
      var bsCurves = output.Values.Select(kvp => kvp.Curve).ToArray();
      fwdVols.Native.SetFirstCapIdx(firstCapIdx_);
      // Read bootstrapped curves into fwd-fwd vol cube
      Dt[] dates = ArrayUtil.Generate(Expiries.Length, i => i == 0 ? AsOf : Expiries[i - 1]);
      for (int i = 0; i < Strikes.Length; i++)
      {
        for (int j = 0; j < Expiries.Length; j++)
        {
          double vol = bsCurves[i].Interpolate(Expiries[j]);
          for (int k = 0; k < dates.Length; k++)
          {
            fwdVols.AddVolatility(k, j, i, vol);
          }
        }
      }
    }

    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified cube.
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiryDate">The expiry date.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    public override double Interpolate(CalibratedVolatilitySurface surface,
      Dt expiryDate, double forwardRate, double strike)
    {
      return CalcCapletVolatilityFromCube(surface, expiryDate, strike);
    }

    IEnumerable<IVolatilityTenor> IVolatilityTenorsProvider.EnumerateTenors()
    {
      return EnumerateCapTenors(AsOf, volCubeInputDict_, capExpiries_, capTenors_);
    }
    #endregion Methods

    #region Private Methods
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

    private static void InitializeBootstrapInputs(double[,] callOptionPrices, double[,] putOptionPrices,
                                                  double[] edFutureStrikes, double[] edFuturePrices, Dt[] edfMaturities,
                                                  double[] capStrikes, double[,] capVolatilities, Dt[] capMaturities,
                                                  double[] lambdaEdf, double[] lambdaCaps,
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
                                                       lambdaEdf[i], 0.0) { BootstrapInputType = BootstrapInputType.Euro };
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

    /// <summary>
    ///   Static Helper method that gets the EDF Option Prices and EDF Option Strikes
    /// </summary>
    /// <param name = "callOptionPrices">Euro Dollar Call Option Prices</param>
    /// <param name = "putOptionPrices">Euro Dollar Put Option Prices</param>
    /// <param name = "edfutureStrikes">Euro Dollar Future Strikes</param>
    /// <param name = "edfuturePrices">Euro Dollar Future Prices </param>
    /// <param name = "strikeIdx">Strike Index</param>
    /// <param name = "optionPrices">The Option Prices</param>
    /// <param name = "optionTypes">Option Types </param>
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

    #endregion Private Methods

    #region Properties

    /// <summary>
    ///   The method used to bootstrap the cube
    /// </summary>
    public VolatilityBootstrapMethod BootstrapMethod
    {
      get { return method_; }
    }

    /// <summary>
    ///   Settle Date of the calibrator
    /// </summary>
    public Dt Settle
    {
      get { return settle_; }
    }

    /// <summary>
    ///   Cap/Floor strikes.
    /// </summary>
    public override double[] CapStrikes
    {
      get { return capStrikes_; }
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public override int[] GetFirstCapIdx()
    {
      return firstCapIdx_;
    }

    #endregion Properties

    #region Data

    private readonly string[] capTenors_;
    private readonly Dt[] capExpiries_;
    private readonly double[] capStrikes_;
    private readonly int[] firstCapIdx_;
    private readonly VolatilityBootstrapMethod method_;
    private readonly VolatilityCubeBootstrapInput[] volCubeInputDict_;
    private readonly Dictionary<Dt, Tuple<InterestRateIndex, DiscountCurve>> projectionCurves_;
    private readonly Dt settle_;

    #endregion Data
  }
}