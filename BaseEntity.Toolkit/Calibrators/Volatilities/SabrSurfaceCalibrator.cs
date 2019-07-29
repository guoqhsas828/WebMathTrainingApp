using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   SABR volatility surface calibrator.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class SabrSurfaceCalibrator : IVolatilitySurfaceCalibrator
    , IBlackScholesParameterDataProvider, IShiftValueProvider
  {
    private static readonly Func<Dt, Dt, double> DefaultTimeCalculator
      = (begin, end) => (end - begin) / 365.25;

    private readonly VolatilityFitSettings _fitSettings;
    private readonly Func<Dt, Dt, double> _timeCalculator;
    private readonly Interp _timeInterp;
    private readonly ISpotCurve _spot = null;
    private readonly IFactorCurve _curve1, _curve2;

    /// <summary>
    /// Builds a new volatility surface from the specified tenors.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="volatilityTenors">The tenors.</param>
    /// <param name="smileInputKind">The smile input kind.</param>
    /// <param name="term">The volatility quote term.</param>
    /// <param name="fitSettings">The volatility fit settings.</param>
    /// <param name="timeInterp">The time interpolation.</param>
    /// <returns>The volatility surface.</returns>
    /// <exception cref="BaseEntity.Toolkit.Util.ToolkitException">Empty tenors.</exception>
    /// <exception cref="ToolkitException">Empty tenors.</exception>
    public static CalibratedVolatilitySurface CreateSurface(
      Dt asOf,
      IEnumerable<IVolatilityTenor> volatilityTenors,
      SmileInputKind smileInputKind,
      IVolatilityUnderlying term,
      VolatilityFitSettings fitSettings,
      Interp timeInterp)
    {
      var timeCalculator = DefaultTimeCalculator;

      Debug.Assert(volatilityTenors != null);
      var tenors = volatilityTenors.ToArray();
      if (tenors.Length == 0)
      {
        throw new ToolkitException("Empty tenors.");
      }
      var calibrator = (term == null)
        ? new SabrSurfaceCalibrator(asOf, timeCalculator, timeInterp, fitSettings)
        : new SabrSurfaceCalibrator(asOf, term.Spot, term.Curve1, term.Curve2,
          timeCalculator, timeInterp, fitSettings);
      var surface = new CalibratedVolatilitySurface(asOf, tenors, calibrator,
        new VolatilitySurfaceInterpolator(timeCalculator,
          calibrator.Fit(tenors)), smileInputKind);
      return surface;
    }

    /// <summary>
    /// Creates the surface.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="expiryDates">The expiry dates</param>
    /// <param name="atmForwards">The ATM forwards</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="volatilities">The volatilities</param>
    /// <param name="lowerBounds">The lower bounds</param>
    /// <param name="upperBounds">The upper bounds</param>
    /// <param name="timeInterp">The time interpolator.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static CalibratedVolatilitySurface CreateSurfaceFromForwards(
      Dt asOf,
      Dt[] expiryDates,
      double[] atmForwards,
      double[] strikes,
      double[,] volatilities,
      double[] lowerBounds,
      double[] upperBounds,
      Interp timeInterp)
    {
      var timeCalculator = DefaultTimeCalculator;

      // Check consistenyc between expiry dates and times.
      if (expiryDates == null || expiryDates.Length == 0)
      {
        throw new ToolkitException("Expiry dates cannot be empty");
      }
      if (atmForwards == null || atmForwards.Length == 0)
      {
        throw new ToolkitException("ATM forwards cannot be empty");
      }
      if (expiryDates.Length != atmForwards.Length)
      {
        throw new ToolkitException("ATM forwards and expiry dates not match");
      }

      // Check arrays
      if (strikes == null || strikes.Length == 0)
        throw new ToolkitException("Array of moneyness cannot be empty");
      if (volatilities == null || volatilities.Length == 0)
        throw new ToolkitException("Volatilities cannot be empty");
      if (volatilities.GetLength(1) != expiryDates.Length)
        throw new ToolkitException("Expiries and volatilities nor match");
      if (volatilities.GetLength(0) != strikes.Length)
        throw new ToolkitException("Moneyness and volatilities nor match");

      // Interpolator
      if (timeInterp == null)
        timeInterp = new Linear();

      var tenorList = new List<MoneynessVolatilityTenor>();
      for(int j = 0; j < expiryDates.Length; ++j)
      {
        Dt d = expiryDates[j];
        if (!d.IsEmpty())
        {
          tenorList.Add(CreateTenor(
            Tenor.FromDateInterval(asOf, d).ToString(), d,
            strikes.Select((s, i) => Tuple.Create(s / atmForwards[j], volatilities[i, j])),
            atmForwards[j]));
        }
      }

      var tenors = tenorList.Where(t => t != null).ToArray();
      // Build surface
      var calibrator = new SabrSurfaceCalibrator(asOf, timeCalculator,
        timeInterp, CreateFitSettings(lowerBounds, upperBounds));
      var surface = new CalibratedVolatilitySurface(asOf, tenors, calibrator,
        new VolatilitySurfaceInterpolator(timeCalculator,
          calibrator.Fit(tenors)), SmileInputKind.Moneyness);
      return surface;
    }

    private static MoneynessVolatilityTenor CreateTenor(string name,
      Dt date, IEnumerable<KeyValuePair<double,double>> data)
    {
      if (date.IsEmpty()) return null;
      var m = new List<double>();
      var v = new List<double>();
      foreach (var p in data)
      {
        if(!(p.Key>0&&p.Value>0)) continue;
          m.Add(p.Key);
        v.Add(p.Value);
      }
      return m.Count == 0
        ? null
        : new MoneynessVolatilityTenor(name, date, m.ToArray(), v.ToArray());
    }

    private static MoneynessVolatilityTenor CreateTenor(string name,
      Dt date, IEnumerable<Tuple<double, double>> data, double forward)
    {
      if (date.IsEmpty()) return null;
      var m = new List<double>();
      var v = new List<double>();
      foreach (var p in data)
      {
        if (!(p.Item1 > 0 && p.Item2 > 0)) continue;
        m.Add(p.Item1);
        v.Add(p.Item2);
      }
      return m.Count == 0
        ? null
        : new MoneynessVolatilityTenor(name, date, forward, m.ToArray(), v.ToArray());
    }

    /// <summary>
    /// Creates the surface.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="expiryDates">The expiry dates.</param>
    /// <param name="timeToExpiries">The time to expiries.</param>
    /// <param name="moneyness">The moneyness.</param>
    /// <param name="volatilities">The volatilities.</param>
    /// <param name="lowerBounds">The lower bounds.</param>
    /// <param name="upperBounds">The upper bounds.</param>
    /// <param name="timeInterp">The time interpolator.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static CalibratedVolatilitySurface CreateSurfaceFromMoneyness(
      Dt asOf,
      Dt[] expiryDates,
      double[] timeToExpiries,
      double[] moneyness,
      double[,] volatilities,
      double[] lowerBounds,
      double[] upperBounds,
      Interp timeInterp)
    {
      var timeCalculator = DefaultTimeCalculator;

      // Check consistency between expiry dates and times.
      if (timeToExpiries == null || timeToExpiries.Length == 0)
      {
        if (expiryDates == null || expiryDates.Length == 0)
          throw new ToolkitException("Arrays of the expiries cannot be empty");
      }
      else if (expiryDates == null || expiryDates.Length == 0)
      {
        expiryDates = timeToExpiries.Select(t =>
          t <= 0.0 ? Dt.Empty : Dt.Add(asOf, Tenor.FromDays((int)(t * 365.25))))
          .ToArray();
      }
      else if (expiryDates.Length != timeToExpiries.Length)
      {
        throw new ToolkitException("Expiry dates and time to expiries not match");
      }
      else
      {
        // setup the time calculator to be the user defined linear mapping from dates to times.
        var curve = new Curve(asOf, new Linear(), DayCount.None, Frequency.Continuous);
        curve.Add(asOf, 0.0);
        for (int i = 0, n = expiryDates.Length; i < n; ++i)
        {
          if (expiryDates[i].IsEmpty() || !(expiryDates[i] > asOf)) continue;
          curve.Add(expiryDates[i], timeToExpiries[i]);
        }
        timeCalculator = (begin, end) => begin == asOf
          ? curve.Interpolate(end)
          : curve.Interpolate(end) - curve.Interpolate(begin);
      }

      // Check arrays
      if (moneyness == null || moneyness.Length == 0)
        throw new ToolkitException("Array of moneyness cannot be empty");
      if (volatilities == null || volatilities.Length == 0)
        throw new ToolkitException("Volatilities cannot be empty");
      if (volatilities.GetLength(0) != expiryDates.Length)
        throw new ToolkitException("Expiries and volatilities nor match");
      if (volatilities.GetLength(1) != moneyness.Length)
        throw new ToolkitException("Moneyness and volatilities nor match");

      // Interpolator
      if (timeInterp == null)
        timeInterp = new Linear();

      // Build tenors
      var tenors = expiryDates.Select((d, i) => d.IsEmpty()
        ? null
        : CreateTenor(Tenor.FromDateInterval(asOf, d).ToString(), d,
          moneyness.Select((m, j) => new KeyValuePair<double, double>(
            m, volatilities[i, j]))))
        .Where(t => t != null).ToArray();

      // Build surface
      var calibrator = new SabrSurfaceCalibrator(asOf,
        timeCalculator, timeInterp, CreateFitSettings(lowerBounds, upperBounds));
      var surface = new CalibratedVolatilitySurface(asOf, tenors, calibrator,
        new VolatilitySurfaceInterpolator(timeCalculator,
          calibrator.Fit(tenors)), SmileInputKind.Moneyness);
      return surface;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SabrSurfaceCalibrator" /> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="timeCalculator">The time calculator.</param>
    /// <param name="timeInterp">The time interpolator.</param>
    /// <param name="fitSettings">The volatility fit settings.</param>
    private SabrSurfaceCalibrator(Dt asOf,
      Func<Dt, Dt, double> timeCalculator,
      Interp timeInterp,
      VolatilityFitSettings fitSettings)
      : this(asOf, null, null, null, timeCalculator, timeInterp, fitSettings)
    {}

    private SabrSurfaceCalibrator(Dt asOf,
      ISpotCurve spot,
      IFactorCurve curve1,
      IFactorCurve curve2,
      Func<Dt, Dt, double> timeCalculator,
      Interp timeInterp,
      VolatilityFitSettings fitSettings)
    {
      AsOf = asOf;
      _spot = spot;
      _curve1 = curve1;
      _curve2 = curve2;
      _fitSettings = fitSettings;
      _timeCalculator = timeCalculator ?? DefaultTimeCalculator;
      _timeInterp = timeInterp;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the as-of date.
    /// </summary>
    /// <value>The as-of date.</value>
    public Dt AsOf { get; set; }

    private SabrModelFlags SabrModelFlags =>
      _fitSettings?.SabrModelFlags ?? SabrModelFlags.None;

    /// <summary>
    /// Gets the shift value for shifted SABR model
    /// </summary>
    /// <value>The shift value.</value>
    public double ShiftValue { get; internal set; }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Calculate at-the-money stock forward price at different expiration dates
    /// </summary>
    /// <param name="pricingDate">Pricing date</param>
    /// <param name="expirationDates">Expiration dates</param>
    /// <param name="stockPrice">Spot stock price</param>
    /// <param name="dividendYield">Stock dividend yield</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <returns>At-the-money stock forward price</returns>
    public static double[] CalculateAtmForwards(Dt pricingDate, Dt[] expirationDates, double stockPrice, double dividendYield, DiscountCurve discountCurve)
    {
      var stockCurve = new StockCurve(pricingDate, stockPrice, discountCurve, dividendYield, null);
      return expirationDates.Select(dt => stockCurve.Interpolate(dt)).ToArray();
    }

    #endregion

    #region IVolatilitySurfaceCalibrator Members

    /// <summary>
    /// Fit from specified tenor
    /// </summary>
    /// <param name="surface">Surface to fit</param>
    /// <param name="fromTenorIdx">Tenor index to fit from</param>
    public void FitFrom(CalibratedVolatilitySurface surface, int fromTenorIdx)
    {
      if (_spot == null && surface.SmileInputKind != SmileInputKind.Moneyness)
        throw new ToolkitException("Moneyness input required");
      var interpolator = surface.Interpolator as VolatilitySurfaceInterpolator;
      Debug.Assert(interpolator != null);
      // To fit the volatility surface, we only need to update the interpolation function.
      interpolator.SurfaceFunction = Fit(surface.Tenors);
    }

    /// <summary>
    /// Get parameter set
    /// </summary>
    /// <param name="date">date</param>
    /// <returns>Black Scholes parameter set</returns>
    protected virtual IBlackScholesParameterData GetParameters(Dt date)
    {
      var spot = _spot == null ? Double.NaN : _spot.Interpolate(date);
      return Double.IsNaN(spot)
        ? null
        : BlackScholesSurfaceBuilder.GetParameters(AsOf, date,
          _timeCalculator, spot, _curve1, _curve2, ShiftValue);
    }

    private Func<double, double, SmileInputKind, double> Fit(
      IEnumerable<IVolatilityTenor> tenors)
    {
      Debug.Assert(tenors != null);
      var interp = _timeInterp;
      return tenors.Select(FitTenor)
        .Select(smile => new KeyValuePair<double, Func<double,
          SmileInputKind, double>>(smile.Time, smile.Evaluate))
        .BuildBlackScholesSurface(interp);
    }

    private SabrVolatilitySmile FitTenor(IVolatilityTenor tenor)
    {
      double[] moneyness, volatilities;
      var data = tenor as ISmileDataProvider;
      if(data ==null)
        throw new ToolkitException("The tenor must implement ISmileDataProvider");
      var date = tenor.Maturity;
      var fwdmodel = GetParameters(date);
      var fwd = fwdmodel == null ? Double.NaN : fwdmodel.GetForward();
      GetMeneynessAndVolatilities(data, fwdmodel,
        out moneyness, out volatilities);
      var time = _timeCalculator(AsOf, date);
      var bounds = GetBounds(tenor.Maturity, _fitSettings);
      var pars = SabrVolatilitySmile.CalibrateParameters(
        time, moneyness, volatilities, bounds.Lower, bounds.Upper,
        SabrModelFlags);
      return new SabrVolatilitySmile(time, fwd,
        pars[0], pars[1], pars[2], pars[3], true);
    }

    private void GetMeneynessAndVolatilities(
      ISmileDataProvider tenor, IBlackScholesParameterData fwdModel,
      out double[] moneyness, out double[] volaitilities)
    {
      var mtenor = tenor as MoneynessVolatilityTenor;
      if (mtenor != null)
      {
        moneyness = mtenor.Moneyness;
        volaitilities = mtenor.QuoteValues.ToArray();
        return;
      }
      if (fwdModel == null)
        throw new ToolkitException("Need forward model for moneyness");
      var m = new List<double>();
      var v = new List<double>();
      var fwd = fwdModel.GetForward();
      foreach (var p in tenor.GetStrikeVolatilityPairs(fwdModel))
      {
        m.Add(p.Strike / fwd);
        v.Add(p.Volatility);
      }
      moneyness = m.ToArray();
      volaitilities = v.ToArray();
    }

    #endregion

    #region Helpers for fit settings

    private static readonly double[] DefaultLowerBounds = new[] { 0.0, 0.0, 0.0, -1.0 };
    private static readonly double[] DefaultUpperBounds = new[] { 5.0, 1.0, 5.0,  0.9999 };

    private static VolatilityFitSettings CreateFitSettings(
      double[] lower, double[] upper)
    {
      if (lower == null || lower.Length == 0)
      {
        if (upper == null || upper.Length == 0)
          return null;
        lower = DefaultLowerBounds;
      }
      else if (upper == null || upper.Length == 0)
      {
        upper = DefaultUpperBounds;
      }
      return new VolatilityFitSettings
      {
        SabrBounds = new Bounds<AlphaBetaRhoNu>(
          new AlphaBetaRhoNu(lower[0], lower[1], lower[3], lower[2]),
          new AlphaBetaRhoNu(upper[0], upper[1], upper[3], upper[2]))
      };
    }

    private static Bounds<double[]> GetBounds(
      Dt date, VolatilityFitSettings settings)
    {
      if (settings == null)
      {
        return new Bounds<double[]>(null, null);
      }
      var lower = (double[])DefaultLowerBounds.Clone();
      var upper = (double[])DefaultUpperBounds.Clone();
      if (settings.SabrBounds.Lower.HasValue)
      {
        SetZetaBetaNuRho(lower, settings.SabrBounds.Lower);
      }
      if (settings.SabrBounds.Upper.HasValue)
      {
        SetZetaBetaNuRho(upper, settings.SabrBounds.Upper);
      }
      if (settings.SabrBeta != null)
        lower[1] = upper[1] = settings.SabrBeta.Interpolate(date);
#if NewInterpretation
      if (settings.SabrAlpha != null)
        lower[0] = upper[0] = settings.SabrAlpha.Interpolate(date);
      if (settings.SabrNu != null)
        lower[2] = upper[2] = settings.SabrNu.Interpolate(date);
      if (settings.SabrRho != null)
        lower[3] = upper[3] = settings.SabrRho.Interpolate(date);
#endif
      return new Bounds<double[]>(lower, upper);
    }

    private static void SetZetaBetaNuRho(double[] d, AlphaBetaRhoNu p)
    {
      d[0] = p.Alpha;
      d[1] = p.Beta;
      d[2] = p.Nu;
      d[3] = p.Rho;
    }

    #endregion

    #region IBlackScholesParameterDataProvider Members

    IBlackScholesParameterData IBlackScholesParameterDataProvider
      .GetParameters(Dt expiry)
    {
      return GetParameters(expiry);
    }

    #endregion
  }
}
