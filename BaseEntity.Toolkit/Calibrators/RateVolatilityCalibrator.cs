/*
 * RateVolatilityCalibrator.cs
 *
 *   2005-2011. All rights reserved.
 * 
 */

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators
{
  internal interface IVolatilityTenorsProvider
  {
    IEnumerable<IVolatilityTenor> EnumerateTenors();
  }

  /// <summary>
  ///   Base calibrator class for forward volatility calibration.
  /// </summary>
  [Serializable]
  public abstract class RateVolatilityCalibrator : BaseEntityObject, IVolatilitySurfaceCalibrator, IVolatilitySurfaceInterpolator
  {
    #region Constructors

    /// <summary>
    ///   Constructor.
    /// </summary>
    protected RateVolatilityCalibrator(Dt asOf, DiscountCurve dc, DiscountCurve referenceCurve, InterestRateIndex rateIndex,
                                       VolatilityType volatilityType, Dt[] dates, Dt[] expiries, double[] strikes)
    {
      asOf_ = asOf;
      discountCurve_ = dc;
      rateIndex_ = rateIndex;
      volatilityType_ = volatilityType;
      Dates = dates;
      Expiries = expiries;
      Strikes = strikes;
      rateProjectionCurve_ = referenceCurve;
    }
    #endregion

    #region Properties

    /// <summary>
    ///   Cube date
    /// </summary>
    public Dt AsOf { get { return asOf_; } set { asOf_ = value; } }

    /// <summary>
    ///   Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get { return discountCurve_; } }

    /// <summary>
    ///   The interest rate index whose dynamics are described by the volatility
    /// </summary>
    public InterestRateIndex RateIndex { get { return rateIndex_; } }

    /// <summary>
    ///   The type of volatility.
    /// </summary>
    public VolatilityType VolatilityType { get { return volatilityType_; } }

    /// <summary>
    ///   Expiries quoted in the cube.
    /// </summary>
    public Dt[] Expiries { get; set; }

    /// <summary>
    ///   Strikes quoted in the cube.
    /// </summary>
    public double[] Strikes { get; set; }

    /// <summary>
    ///   Cap/Floor strikes quoted in the cube.
    /// </summary>
    public virtual double[] CapStrikes { get { return Strikes; } }

    /// <summary>
    ///   Dates used to discretize volatilities
    /// </summary>
    public Dt[] Dates { get; set; }

    ///<summary>
    /// The projection curve, if different from discount curve
    ///</summary>
    public DiscountCurve RateProjectionCurve
    {
      get { return rateProjectionCurve_ ?? discountCurve_; }
    }
   
    #endregion

    #region Methods

    /// <summary>
    /// Fit the cube
    /// </summary>
    /// <param name = "cube">The cube.</param>
    public void Fit(RateVolatilityCube cube)
    {
      FitFrom(cube, 0);
    }

    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified cube.
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    /// The volatility at the given date and strike.
    /// </returns>
    double IVolatilitySurfaceInterpolator.Interpolate(
      VolatilitySurface surface, Dt expiry, double strike)
    {
      return CalcCapletVolatilityFromCube(surface, expiry, strike);
    }

    internal static double CalcCapletVolatilityFromCube(
      VolatilitySurface surface, Dt expiryDate, double strike)
    {
      var volCube = surface as RateVolatilityCube;
      if (volCube == null)
        throw new ArgumentException("RateVolatilityCube expected");
      var fwdVols = ((RateVolatilityCube)surface).FwdVols;
      return fwdVols.Native.CalcCapletVolatility(expiryDate, strike);
    }

    /// <summary>
    /// Fit the cube
    /// </summary>
    /// <param name = "surface">The cube.</param>
    /// <param name = "fromIdx">From idx.</param>
    public abstract void FitFrom(CalibratedVolatilitySurface surface, int fromIdx);

    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified cube.
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiryDate">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <returns>The volatility.</returns>
    public abstract double Interpolate(CalibratedVolatilitySurface surface,
      Dt expiryDate, double forwardRate, double strike);

    /// <summary>
    ///   Gets the first cap idx.
    /// </summary>
    /// <returns>int[]</returns>
    public virtual int[] GetFirstCapIdx()
    {
      var firstCapIdx = new int[Strikes.Length];
      for (int i = 0; i < Strikes.Length; i++) firstCapIdx[i] = 0;
      return firstCapIdx;
    }

    /// <summary>
    ///   Calculate the swap rate from a DiscountCurve.
    /// </summary>
    /// <param name = "dc"></param>
    /// <param name = "asOf"></param>
    /// <param name = "maturity"></param>
    /// <param name = "rateIndex"></param>
    /// <returns></returns>
    public static double CalculateSwapRate(DiscountCurve dc, Dt asOf, Dt maturity, InterestRateIndex rateIndex)
    {
      // Calc effective by going forward 1 caplet period
      Dt effective = Cap.StandardEffective(asOf, rateIndex);
      // Calculate swap rate
      return CurveUtil.DiscountForwardSwapRate(dc, effective, maturity, rateIndex.DayCount,
                                               rateIndex.IndexTenor.ToFrequency(),
                                               rateIndex.Roll, rateIndex.Calendar);
    }

    #endregion

    #region Data

    // Readonly 
    private Dt asOf_;
    private readonly DiscountCurve rateProjectionCurve_;
    private readonly InterestRateIndex rateIndex_;
    private readonly DiscountCurve discountCurve_;
    private readonly VolatilityType volatilityType_;
    #endregion

    #region Tenor View

    internal static IEnumerable<PlainVolatilityTenor> EnumerateTenors(
      Dt asOf, double[] volatilities, Dt[] capExpiries, string[] capTenorNames)
    {
      if (volatilities == null || volatilities.Length == 0 ||
        capExpiries == null || capExpiries.Length == 0)
      {
        yield break;
      }
      if (capTenorNames != null && capTenorNames.Length != capExpiries.Length)
      {
        throw new ToolkitException("cap tenors and expiry dates not match");
      }
      for (int t = 0, n = capExpiries.Length; t < n; ++t)
      {
        int i = t;
        var maturity = capExpiries[i];
        var name = capTenorNames != null
          ? capTenorNames[i]
          : Tenor.FromDateInterval(asOf, maturity).ToString();
        yield return new PlainVolatilityTenor(name, maturity)
        {
          Volatilities = ListUtil.CreateList(1,
            k => volatilities[i], (k, v) => volatilities[i] = v)
        };
      }
    }

    internal static IEnumerable<PlainVolatilityTenor> EnumerateCapTenors(
      Dt asOf, VolatilityCubeBootstrapInput[] inputs,
      Dt[] capExpiries, string[] capTenorNames)
    {
      if (inputs == null || inputs.Length == 0 ||
        capExpiries == null || capExpiries.Length == 0)
      {
        yield break;
      }
      if (capTenorNames != null && capTenorNames.Length != capExpiries.Length)
      {
        throw new ToolkitException("cap tenors and expiry dates not match");
      }
      for (int i = 0, n = capExpiries.Length; i < n; ++i)
      {
        var maturity = capExpiries[i];
        var data = GetCapInputsByMaturity(inputs, maturity);
        if (data.Length == 0) continue;
        var name = capTenorNames != null
          ? capTenorNames[i]
          : Tenor.FromDateInterval(asOf, maturity).ToString();
        yield return new PlainVolatilityTenor(name, maturity)
        {
          Strikes = new CapStrikeList(data),
          Volatilities = new CapVolatilityList(data)
        };
      }
    }

    private static KeyValuePair<VolatilityCubeBootstrapInput, int>[]
      GetCapInputsByMaturity(VolatilityCubeBootstrapInput[] inputs, Dt maturity)
    {
      var data = new List<KeyValuePair<VolatilityCubeBootstrapInput, int>>();
      for (int i = 0, n = inputs.Length; i < n; ++i)
      {
        var dates = inputs[i].CapMaturities;
        if (dates == null) continue;
        var idx = Array.IndexOf(dates, maturity);
        if (idx < 0 || idx >= inputs[i].CapVols.Length)
          continue;
        data.Add(new KeyValuePair<VolatilityCubeBootstrapInput, int>(inputs[i], idx));
      }
      return data.ToArray();
    }

    #region Nested type: CapVolatilityList

    [Serializable]
    private class CapVolatilityList : FixedSizeList<double>
    {
      private readonly KeyValuePair<VolatilityCubeBootstrapInput, int>[] _data;
      public CapVolatilityList(KeyValuePair<VolatilityCubeBootstrapInput, int>[] data)
      {
        _data = data;
      }
      public override double this[int index]
      {
        get
        {
          var d = _data[index];
          return d.Key.CapVols[d.Value];
        }
        set
        {
          var d = _data[index];
          d.Key.CapVols[d.Value] = value;
        }
      }

      public override int Count
      {
        get { return _data.Length; }
      }
    }

    [Serializable]
    private class CapStrikeList : FixedSizeList<double>
    {
      private readonly KeyValuePair<VolatilityCubeBootstrapInput, int>[] _data;
      public CapStrikeList(KeyValuePair<VolatilityCubeBootstrapInput, int>[] data)
      {
        _data = data;
      }
      public override double this[int index]
      {
        get
        {
          var d = _data[index];
          return d.Key.Strike;
        }
        set
        {
          var d = _data[index];
          d.Key.Strike = value;
        }
      }

      public override int Count
      {
        get { return _data.Length; }
      }
    }

    #endregion
    #endregion
  }
}
