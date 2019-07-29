/*
 * SwaptionVolatilityCube.cs
 *
 *   2010. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  ///<summary>
  /// Utility class for 2-D view of array data
  ///</summary>
  ///<typeparam name="T">Data element type</typeparam>
  [Serializable]
  public class View2D<T>
  {
    ///<summary>
    /// Constructor
    ///</summary>
    ///<param name="data">The data array</param>
    ///<param name="nrow">The total number of rows for 2-D view</param>
    ///<param name="ncol">The total number of columns for 2-D view</param>
    ///<exception cref="ToolkitException"></exception>
    public View2D(T[] data, int nrow, int ncol)
    {
      if(data.Length != nrow*ncol)
      {
        throw new ToolkitException("Data length is incompatible with the rows and columns");
      }
      data_ = data;
      nrow_ = nrow;
      ncol_ = ncol;
    }
    ///<summary>
    /// Get the data element from row i and column j
    ///</summary>
    ///<param name="i">Row index</param>
    ///<param name="j">Column index</param>
    public T this[int i, int j]
    {
      get { return data_[i * ncol_ + j]; }
      set { data_[i*ncol_ + j] = value; }
    }
    ///<summary>
    /// Get the data array
    ///</summary>
    public T[] Data {get{ return data_;}}

    private T[] data_;
    private readonly int nrow_, ncol_;
  }

  ///<summary>
  /// Swaption Volatility Cube data
  ///</summary>
  [Serializable]
  public class SwaptionVolatilityCube : RateVolatilitySurface, IModelParameter
  {
    ///<summary>
    /// Constructor for the case of basis adjustment on cap/floor cube
    ///</summary>
    ///<param name="atmVolatility"></param>
    ///<param name="skew"></param>
    private SwaptionVolatilityCube(
      IVolatilityObject atmVolatility,
      SwaptionVolatilitySpline skew)
      : base(skew.RateVolatilityCalibrator, skew.Tenors, null)
    {
      AtmVolatilityObject = atmVolatility;
      Skew = skew;
    }


    /// <summary>
    ///  Gets the volatility skew
    /// </summary>
    public SwaptionVolatilitySpline Skew { get; private set; }

    ///<summary>
    ///  Gets the ATM volatility surface
    ///</summary>
    public CalibratedVolatilitySurface VolatilitySurface
    {
      get { return AtmVolatilityObject as CalibratedVolatilitySurface; }
    }

    private RateVolatilitySurface RateVolatilitySurface
    {
      get { return AtmVolatilityObject as RateVolatilitySurface; }
    }

    public IVolatilityObject AtmVolatilityObject { get; set; }

    #region Static constructors

    ///<summary>
    /// Constructor for the case of basis adjustment on cap/floor cube
    ///</summary>
    ///<param name="atmVolatility"></param>
    ///<param name="skew"></param>
    public static SwaptionVolatilityCube Create(
      IVolatilityObject atmVolatility,
      SwaptionVolatilitySpline skew)
    {
      return new SwaptionVolatilityCube(atmVolatility, skew);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="dc"></param>
    /// <param name="ri"></param>
    /// <param name="volType"></param>
    /// <param name="strikeSkewExpiries"></param>
    /// <param name="cleanStrikeShifts"></param>
    /// <param name="strikeSkewTenors"></param>
    /// <param name="strikeSkew"></param>
    /// <param name="strikeInterp"></param>
    /// <param name="timeInterp"></param>
    /// <param name="swapDc"></param>
    /// <param name="swapRoll"></param>
    /// <param name="swapFreq"></param>
    /// <param name="notifyCal"></param>
    /// <param name="notifyDays"></param>
    /// <returns></returns>
    public static SwaptionVolatilitySpline CreateSwaptionVolatilitySkew(
      Dt asOf,
      DiscountCurve dc,
      InterestRateIndex ri,
      VolatilityType volType,
      IList<string> strikeSkewExpiries,
      IList<double> cleanStrikeShifts,
      IList<string> strikeSkewTenors,
      double[,] strikeSkew,
      Interp strikeInterp,
      Interp timeInterp,
      DayCount swapDc,
      BDConvention swapRoll,
      Frequency swapFreq,
      Calendar notifyCal,
      int notifyDays)
    {
      var interp = strikeInterp != null && timeInterp != null
        ? new VolCubeInterpolator(strikeInterp, timeInterp)
        : new VolCubeInterpolator();

      var swaptionExpiries = new List<Tenor>();
      var overlayDataView = CreateDataView(strikeSkewExpiries,
        t => RateVolatilityUtil.SwaptionStandardExpiry(asOf, ri, t),
        strikeSkewTenors, cleanStrikeShifts, strikeSkew, swaptionExpiries);

      var calibrator = new RateVolatilitySwaptionMarketCalibrator(
        asOf, notifyDays, dc, swaptionExpiries,
        strikeSkewTenors.ConvertAll(Tenor.Parse),
        ri, volType, swapDc, swapRoll, swapFreq, notifyCal)
      {Strikes = (cleanStrikeShifts as double[]) ?? cleanStrikeShifts.ToArray()};

      var skew = new SwaptionVolatilitySpline(overlayDataView, calibrator, interp);
      skew.Validate();
      skew.Fit();

      // Done
      return skew;
    }

    /// <summary>
    ///  Create a swaption volatility cube from raw data
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="expiries"></param>
    /// <param name="tenors"></param>
    /// <param name="atmVolatilities"></param>
    /// <param name="expiryTenorPairs"></param>
    /// <param name="strikeShifts"></param>
    /// <param name="strikeSkewTenors"></param>
    /// <param name="strikeSkews"></param>
    /// <returns></returns>
    public static SwaptionVolatilityCube CreateSwaptionVolatilityCube(
      Dt asOf,
      string[] expiries,
      string[] tenors,
      double[,] atmVolatilities,
      string[,] expiryTenorPairs,
      double[] strikeShifts,
      string[] strikeSkewTenors,
      double[,] strikeSkews)
    {
      var dataView = CreateDataView(expiries, t => Dt.Add(asOf, t),
        tenors, null, atmVolatilities, null);

      var strikeSkewExpiries = (expiryTenorPairs.GetUpperBound(0) + 1) / strikeSkewTenors.Length;
      var overlayData = new RateVolatilityTenor[expiryTenorPairs.GetUpperBound(0) + 1];
      var overlayDataView = new View2D<RateVolatilityTenor>(overlayData,
        strikeSkewExpiries,
        strikeSkewTenors.Length);
      for (int row = 0; row < strikeSkewExpiries; row++)
      {
        for (int row2 = 0; row2 < strikeSkewTenors.Length; row2++)
        {
          var vols = new double[strikeShifts.Length];
          for (int j = 0; j < strikeShifts.Length; ++j)
            vols[j] = strikeSkews[row * strikeSkewTenors.Length + row2, j];

          var expiry = expiryTenorPairs[row * strikeSkewTenors.Length + row2, 0];
          var ftenor = expiryTenorPairs[row * strikeSkewTenors.Length + row2, 1];
          overlayDataView[row, row2] = new RateVolatilityTenor(
            expiry + '-' + ftenor, Dt.Add(asOf, expiry))
          {
            ForwardTenor = Tenor.Parse(ftenor),
            Strikes = strikeShifts,
            Volatilities = vols
          };
        }
      }
      // TODO: add calibrator
      var interp = new VolCubeInterpolator();

      var surface = new SwaptionVolatilitySpline(dataView, null, interp);
      var skew = new SwaptionVolatilitySpline(overlayDataView, null, interp);
      var cube = new SwaptionVolatilityCube(surface, skew);
      return cube;
    }

    ///<summary>
    /// Static function to create swaption vol cube from market quotes
    ///</summary>
    ///<returns>SwaptionVolatilityCube</returns>
    public static SwaptionVolatilityCube CreateSwaptionMarketCube(
      Dt asOf,
      DiscountCurve dc,
      string[] cleanExpiries,
      string[] cleanTenors,
      double[,] cleanVols,
      InterestRateIndex ri,
      VolatilityType volType,
      string[] strikeSkewExpiries,
      double[] cleanStrikeShifts,
      string[] strikeSkewTenors,
      double[,] strikeSkew,
      Interp strikeInterp,
      Interp timeInterp,
      DayCount swapDc,
      BDConvention swapRoll,
      Frequency swapFreq,
      Calendar notifyCal,
      int notifyDays)
    {
      var interp = strikeInterp != null && timeInterp != null
                    ? new VolCubeInterpolator(strikeInterp, timeInterp)
                    : new VolCubeInterpolator();

      var fwdTenors = ArrayUtil.Convert(cleanTenors, Tenor.Parse).ToArray();


      // Build the source cube and the calibrator consistent with it.
      var swaptionExpiries = new List<Tenor>();
      var srcDataView = CreateDataView(
        cleanExpiries, t => RateVolatilityUtil.SwaptionStandardExpiry(asOf, ri, t),
        cleanTenors, null, cleanVols, swaptionExpiries);
      var srcCalibrator = new RateVolatilitySwaptionMarketCalibrator(asOf,
        notifyDays, dc, swaptionExpiries, fwdTenors, ri, volType, swapDc,
        swapRoll, swapFreq, notifyCal)
      {
        Strikes = GetStrikeArray(srcDataView)
      };
      var sourceCube = new SwaptionVolatilitySpline(srcDataView, srcCalibrator, interp);
 
      var calibrator = new RateVolatilitySwaptionMarketCalibrator(asOf, notifyDays,
                                                                  dc, swaptionExpiries,
                                                                  fwdTenors,
                                                                  ri, volType, swapDc, swapRoll,
                                                                  swapFreq, notifyCal) { Strikes = cleanStrikeShifts };

      var overlayDataView = CreateDataView(
        strikeSkewExpiries, t=>RateVolatilityUtil.SwaptionStandardExpiry(asOf, ri, t),
        strikeSkewTenors, cleanStrikeShifts, strikeSkew, null);
      var skew = new SwaptionVolatilitySpline(overlayDataView, calibrator, interp);
      var cube = new SwaptionVolatilityCube(sourceCube, skew);
      cube.Validate();
      cube.Fit();

      // Done
      return cube;

    }

    ///<summary>
    /// Utility function to create swaption volatility cube based on calibrated cap/floor vol cube and basis adjustment
    ///</summary>
    ///<param name="capfloorSurface">Cap/Floor volatility surface</param>
    ///<param name="dc">Discount curve</param>
    ///<param name="strikeSkewExpiries">Strike skew expiries</param>
    ///<param name="cleanStrikeShifts">Strike skew shift from ATM strike</param>
    ///<param name="strikeSkewTenors">Strike skew tenors</param>
    ///<param name="adjustments">Strike skew vol adjustments</param>
    ///<param name="strikeInterp">Interpolation schema on strike dimension</param>
    ///<param name="timeInterp">Interpolation schema on expiry/tenor dimension</param>
    ///<param name="swapDc">Swap fixed day-count</param>
    ///<param name="swapRoll">Swap BDConvention</param>
    ///<param name="swapFreq">Swap fixed leg frequency</param>
    ///<param name="notifyCal">Notification calendar</param>
    ///<param name="daysToNotify">Notification days</param>
    ///<param name="asOf">Pricing date</param>
    ///<returns>Basis-adjustment based swaption volatility cube</returns>
    public static SwaptionVolatilityCube CreateCapFloorBasisAdjustedCube(RateVolatilityCube capfloorSurface,
      Dt asOf,
      DiscountCurve dc,
      string[] strikeSkewExpiries,
      double[] cleanStrikeShifts,
      string[] strikeSkewTenors,
      double[,] adjustments,
      Interp strikeInterp,
      Interp timeInterp,
      DayCount swapDc,
      BDConvention swapRoll,
      Frequency swapFreq,
      Calendar notifyCal,
      int daysToNotify)
    {
      InterestRateIndex rateIndex = capfloorSurface.RateVolatilityCalibrator.RateIndex;
      VolatilityType volType = capfloorSurface.RateVolatilityCalibrator.VolatilityType;
      var swaptionExpiries = new List<Tenor>();
      var settle = BaseEntity.Toolkit.Products.Cap.StandardSettle(asOf, rateIndex);
      var overlayDataView = CreateDataView(strikeSkewExpiries, t=>Dt.Add(settle,t),
        strikeSkewTenors, cleanStrikeShifts, adjustments, swaptionExpiries);
      swaptionExpiries.Sort();
      var swaptionCalibrator = new RateVolatilityCapFloorBasisAdjustCalibrator(asOf, daysToNotify,
                                                                               dc, dc, swaptionExpiries,
                                                                               CollectionUtil.ConvertAll(
                                                                                 strikeSkewTenors,
                                                                                 Tenor.Parse),
                                                                               cleanStrikeShifts, rateIndex, volType,
                                                                               swapDc, swapRoll, swapFreq, notifyCal);

      var interp = strikeInterp != null && timeInterp != null
                     ? new VolCubeInterpolator(strikeInterp, timeInterp)
                     : new VolCubeInterpolator();
      var skew = new SwaptionVolatilitySpline(overlayDataView, swaptionCalibrator, interp);

      var cube = new SwaptionVolatilityCube(capfloorSurface, skew);
      cube.Validate();
      cube.Fit();

      // Done
      return cube;

    }

    private static View2D<RateVolatilityTenor> CreateDataView(
      IList<string> expiries,
      Func<Tenor, Dt> getExpiryDate,
      IList<string> tenors,
      IList<double> strikes,
      double[,] volatilities,
      IList<Tenor> swaptionExpiries)
    {
      if (expiries.IsNullOrEmpty())
      {
        if (tenors.IsNullOrEmpty())
        {
          return new View2D<RateVolatilityTenor>(
            EmptyArray<RateVolatilityTenor>.Instance, 0, 0);
        }
        throw new ArgumentException("expiries cannot be empty");
      }
      if (getExpiryDate == null)
        throw new ArgumentException("getExpiryDate cannot be null");
      if (tenors.IsNullOrEmpty())
        throw new ArgumentException("tenors cannot be empty");
      if (volatilities == null)
        throw new ArgumentException("volatilities cannot be null");

      int expiryCount = expiries.Count,
        tenorCount = tenors.Count,
        strikeCount = 0;
      if (strikes.IsNullOrEmpty())
      {
        if (volatilities.GetLength(0) < expiryCount
          || volatilities.GetLength(1) < tenorCount)
        {
          throw new ArgumentException("volatilities and expiries/tenors not match");
        }
        strikes = new[] {0.0};
      }
      else
      {
        if (volatilities.GetLength(0) < expiryCount*tenorCount
          || volatilities.GetLength(1) < strikeCount)
        {
          throw new ArgumentException("volatilities and expiries/tenors/strikes nor match");
        }
        strikeCount = strikes.Count;
      }

      var data = new RateVolatilityTenor[expiryCount*tenorCount];
      var view = new View2D<RateVolatilityTenor>(
        data, expiryCount, tenorCount);

      for (int i = 0; i < expiryCount; i++)
      {
        var expiry = Tenor.Parse(expiries[i]);
        if (swaptionExpiries != null && !swaptionExpiries.Contains(expiry))
          swaptionExpiries.Add(expiry);

        for (int j = 0; j < tenorCount; j++)
        {
          double[] vols = null;
          if (strikeCount > 0)
          {
            vols = new double[strikeCount];
            for (int k = 0; k < strikeCount; ++k)
              vols[k] = volatilities[i*tenorCount + j, k];
          }
          else
          {
            vols = new[] {volatilities[i, j]};
          }

          view[i, j] = new RateVolatilityTenor(
            expiries[i] + '-' + tenors[j],
            getExpiryDate(expiry))
          {
            ForwardTenor = Tenor.Parse(tenors[j]),
            Strikes = strikes,
            Volatilities = vols
          };
        }
      }

      return view;

    }

    private static double[] GetStrikeArray(View2D<RateVolatilityTenor> view)
    {
      var k = (view == null || view.Data == null || view.Data.Length == 0)
        ? null : view.Data[0].Strikes;
      return k == null ? new[] {0.0} : ((k as double[]) ?? k.ToArray());
    }

    #endregion

    #region Utility method
    ///<summary>
    /// Convert the forward tenor of swaption into year lengh
    ///</summary>
    ///<param name="fwdTenor">Forward tenor</param>
    ///<returns>#of years</returns>
    public static double ConvertForwardTenor(Tenor fwdTenor)
    {
      if (fwdTenor == Tenor.Empty)
        return 0.0;
      switch (fwdTenor.Units)
      {
        case TimeUnit.Years :
          return fwdTenor.N * 12;
        case TimeUnit.Months :
          return fwdTenor.N;
        default :
          throw new ArgumentException("Tenor with Units of Days not supported in calibration");
      }
    }

    ///<summary>
    /// Convert the forward maturity to the number representation
    ///</summary>
    ///<param name="expiry">Swaption expiry</param>
    ///<param name="maturity">Swaption maturity</param>
    ///<returns>The time span</returns>
    public static double ConvertForwardTenor(Dt expiry, Dt maturity)
    {
      return (int) (0.5 + ((double) Dt.Diff(expiry, maturity))/365.0*12.0);
    }

    ///<summary>
    /// Converting double-type expiry tenor into tenor format
    ///</summary>
    ///<param name="expiryTime">Double-type expiry tenor in years</param>
    ///<returns>Expiry tenor</returns>
    public static Tenor ConvertExpiry(double expiryTime)
    {
      var N = (int) (0.5 + expiryTime*12.0);
      if (N < 12)
        return new Tenor(N, TimeUnit.Months);
      else
        return new Tenor(N/12, TimeUnit.Years);
    }

    #endregion
    
    #region IModelParameter Members
    /// <summary>
    /// Interpolate 
    /// </summary>
    /// <param name="maturity">Effective of the forward starting swap</param>
    /// <param name="strike">Strike</param>
    /// <param name="referenceIndex">SwapRateIndex</param>
    /// <returns>Swaption volatility</returns>
    double IModelParameter.Interpolate(Dt maturity, double strike, ReferenceIndex referenceIndex)
    {
      if (referenceIndex is SwapRateIndex)
        return Evaluate(maturity, Dt.Add(maturity, referenceIndex.IndexTenor), strike);

      var irIndex = referenceIndex as InterestRateIndex;
      if (irIndex != null)
        return InterpolateForwardRateVolatility(maturity, strike, irIndex);

      throw new ToolkitException("SwapRateIndex or InterestRateIndex expected");
    }

    ///<summary>
    /// Interpolate swaption volatility based on expiry, swaption maturity and strike from the volatility cube
    ///</summary>
    ///<param name="expiry">Expiry</param>
    ///<param name="maturity">Swaption maturity</param>
    ///<param name="strike">Strike</param>
    ///<returns>Volatility</returns>
    private double Evaluate(Dt expiry, Dt maturity, double strike)
    {
      if (RateVolatilitySurface != null)
      {
        var rsc = RateVolatilitySurface.RateVolatilityCalibrator as RateVolatilitySwaptionMarketCalibrator;
        if (rsc != null)
          return rsc.Evaluate(RateVolatilitySurface, expiry, ConvertForwardTenor(expiry, maturity), strike);
        return RateVolatilitySurface.Interpolate(expiry, strike);
      }
      return 0.0;
    }
 
    private double InterpolateForwardRateVolatility(
      Dt expiry, double strike, InterestRateIndex index)
    {
      var surface = RateVolatilitySurface;
      if (surface == null) return 0;

      Dt maturity = Dt.Roll(Dt.Add(expiry, index.IndexTenor),
        index.Roll, index.Calendar);
      if (maturity <= expiry) maturity = expiry + 1;
      var duration = ConvertForwardTenor(expiry, maturity);
      var forward = CalculateForwardRate(
        Skew.RateVolatilityCalibrator.RateProjectionCurve,
        expiry, maturity, index);

      double volSkew = 0;
      if (Skew != null)
      {
        volSkew = Skew.Evaluate(expiry, duration, forward, strike);
      }

      var spline = surface as SwaptionVolatilitySpline;
      if (spline != null)
        return volSkew + spline.Evaluate(expiry, duration);

        var atmModel = surface as IModelParameter;
        if (atmModel == null)
        {
          // Ignored skew data
          return Skew == null
            ? surface.Interpolate(expiry, strike)
            : (volSkew + surface.Interpolate(expiry, forward));
        }
        
      return Skew == null
        ? atmModel.Interpolate(expiry, strike, index)
        : (volSkew + atmModel.Interpolate(expiry, forward, index));
    }

    private static double CalculateForwardRate(
      DiscountCurve curve, Dt begin, Dt end, InterestRateIndex index)
    {
      var fraction = Dt.Fraction(begin, end, begin, end,
        index.DayCount, index.IndexTenor.ToFrequency());
      return (1/curve.Interpolate(begin, end) - 1)/fraction;
    }

    #endregion
  }

  ///<summary>
  /// Representation of volatility term structure on strike dimension with the same expiry/forward tenor combination
  ///</summary>
  [Serializable]
  public class RateVolatilityTenor : PlainVolatilityTenor
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="RateVolatilityTenor" /> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="maturity">The maturity.</param>
    public RateVolatilityTenor(string name, Dt maturity)
      : base(name, maturity)
    {
    }

    ///<summary>
    /// Forward Tenor
    ///</summary>
    public Tenor ForwardTenor { get; set; }

    /// <summary>
    /// Validate object
    /// </summary>
    /// <param name="errors">Error list</param>
    public override void Validate(System.Collections.ArrayList errors)
    {

      if (Maturity.IsEmpty())
      {
        InvalidValue.AddError(errors, this, "Maturity",
          "Maturity cannot be null");
      }
    }
  }

  /// <summary>
  ///  Swaption volatility surface/cube based on spline interpolation
  /// </summary>
  [Serializable]
  public class SwaptionVolatilitySpline : RateVolatilitySurface
  {
    public SwaptionVolatilitySpline(
      View2D<RateVolatilityTenor> dataView,
      RateVolatilityCalibrator calibrator,
      ISwapVolatilitySurfaceInterpolator interpolator)
      : base(calibrator, dataView.Data, null)
    {
      RateVolatilityInterpolator = interpolator ?? new VolCubeInterpolator();
      DataView = dataView;
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>By default validation is meta data driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.</remarks>
    public override void Validate(ArrayList errors)
    {
      if (DataView == null)
        errors.Add("Spline data cannot be empty");
      base.Validate(errors);
    }

    ///<summary>
    /// Locate the volatility data on cube grid based on expiry, maturity and strike index
    ///</summary>
    ///<param name="expiryIdx">Expiry index</param>
    ///<param name="tenorIdx">Tenor index</param>
    ///<param name="strikeIdx">Strike index</param>
    ///<returns>Volatility</returns>
    public double SearchCubeData(int expiryIdx, int tenorIdx, int strikeIdx)
    {
      var viewData = DataView;
      var volTenor = viewData[expiryIdx, tenorIdx];
      return volTenor.QuoteValues[strikeIdx];
    }

    ///<summary>
    /// Evaluate swaption volatility at the specified expiry, effective duration and strike
    ///</summary>
    ///<param name="expiry">Expiry</param>
    ///<param name="duration">Swap duration</param>
    ///<param name="strike">Strike</param>
    ///<returns>Volatility</returns>
    public double Evaluate(Dt expiry, double duration, double strike)
    {
      var splineCalibrator = Calibrator as SwaptionVolatilityBaseCalibrator;
      if (splineCalibrator != null)
        return splineCalibrator.Evaluate(this, expiry, duration, strike);

      return 0;
    }

    ///<summary>
    /// Evaluated the volatility at specified expiry and duration, based on ATM strike.
    ///</summary>
    ///<param name="expiry">Swaption expiration</param>
    ///<param name="duration">Swaption effective duration</param>
    ///<returns>volatility</returns>
    public double Evaluate(Dt expiry, double duration)
    {
      return SplineInterpolator.Interpolate(this, expiry, duration);
    }

    public double Evaluate(Func<Dt, Dt> getExpiry,
      DateAndValue<double> fwdStartAndDuration)
    {
      return Evaluate(getExpiry(fwdStartAndDuration.Date),
        fwdStartAndDuration.Value);
    }

    ///<summary>
    /// Evaluated the volatility at specified expiry and duration, based on ATM strike.
    ///</summary>
    ///<param name="expiry">Swaption expiration</param>
    ///<param name="duration">Swaption effective duration</param>
    ///<param name="forwardRate">Strike</param>
    ///<param name="strike">Strike</param>
    ///<returns>volatility</returns>
    public double Evaluate(Dt expiry, double duration,
      double forwardRate, double strike)
    {
      return SplineInterpolator.Interpolate(this,
        expiry, strike - forwardRate, duration);
    }

    public double Evaluate(Func<Dt, Dt> getExpiry,
      DateAndValue<double> fwdStartAndDuration, double strike)
    {
      return Evaluate(getExpiry(fwdStartAndDuration.Date),
        fwdStartAndDuration.Value, strike);
    }

    private ISwapVolatilitySurfaceInterpolator SplineInterpolator
      => (ISwapVolatilitySurfaceInterpolator) RateVolatilityInterpolator;

    ///<summary>
    /// The 2-D data view 
    ///</summary>
    public View2D<RateVolatilityTenor> DataView { get; set; }

    ///<summary>
    /// The numeric-representation of forward tenor in number of months
    ///</summary>
    public List<double> TenorAxis { get; set; }

    ///<summary>
    /// The numeric-representation of expiry tenor in number of years
    ///</summary>
    public List<double> ExpiryAxis { get; set; }

    /// <summary>
    /// Gets or sets the expiry tenors.
    /// </summary>
    /// <value>The expiry tenors.</value>
    public double[] Strikes
    {
      get { return RateVolatilityCalibrator.Strikes; }
    }

    public bool Skewed
    {
      get
      {
        return ArrayUtil.IsNullOrEmpty(Strikes) ||
          (Strikes.Length == 1 && Strikes[0].AlmostEquals(0.0));
      }
    }
  }

}
