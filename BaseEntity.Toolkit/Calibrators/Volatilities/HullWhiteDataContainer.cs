using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  #region HullWhiteDateContainer
  /// <summary>
  /// Hull White Date Container
  /// </summary>
  public class HullWhiteDataContainer
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="expiries">Expiriy dates</param>
    /// <param name="tenors">forward tenors</param>
    /// <param name="vols">Volatilities</param>
    /// <param name="volType">Volatility type</param>
    /// <param name="weights">Weights</param>
    /// <param name="rateCurve">Rate curve</param>
    public HullWhiteDataContainer(Dt[] expiries, string[] tenors,
      double[,] vols, DistributionType volType,
      double[,] weights, DiscountCurve rateCurve)
    {
      _expiries = expiries;
      _tenors = tenors;
      _vols = vols;
      _weights = weights;
      _rateCurve = rateCurve;
      _volType = volType;
      Initialize();
    }

    /// <summary>
    /// The public constructor
    /// </summary>
    public HullWhiteDataContainer()
    {
    }

    #endregion Constructor

    #region Calculations
    private void Initialize()
    {
      _timeGrids = GetTimeGrids();
      InitializeArrays(_timeGrids.Length);
    }

    private void InitializeArrays(int n)
    {
      _variables = new double[SigmaCurveDates.Length + MeanCurveDates.Length];
      _sigmas = new double[n];
      _meanReversions = new double[n];
      _nu = new double[n];
      _g = new double[n];
      _vr = new double[n];
      _b = new double[n, n];
    }

    private Dt[] GetTimeGrids()
    {
      var timeGrids = new UniqueSequence<Dt>();

      for (int i = 0; i < _expiries.Length; i++)
      {
        timeGrids.Add(_expiries[i]);
        for (int j = 0; j < _tenors.Length; j++)
        {
          timeGrids.Add(Dt.Add(_expiries[i], Tenor.Parse(_tenors[j])));
        }
      }
      return timeGrids.ToArray();
    }

    private Dt[] GetMeanCurveDates()
    {
      int cPoints = FitPoints.MeanFitPoints;
      var meanCurveDates = new UniqueSequence<Dt>();
      var dates = TimeGrids;
      var n = dates.Length;
      if (n <= cPoints) return dates;
      for (int i = 0; i < cPoints; i++)
      {
        var index = (int)(i * n / (cPoints - 1));
        if (index == n) index -= 1;
        meanCurveDates.Add(dates[index]);
      }
      return meanCurveDates.ToArray();
    }

    private Dt[] GetSigmaCurveDates()
    {
      int cPoints = FitPoints.SigmaFitPoints;
      var sigmaCurveDates = new UniqueSequence<Dt>();
      var expiries = Expiries;
      var n = expiries.Length;
      if (n <= cPoints) return expiries;
      for (int i = 0; i < cPoints; i++)
      {
        var index = (int)(i * n / (cPoints - 1));
        if (index == n) index -= 1;
        sigmaCurveDates.Add(expiries[index]);
      }
      return sigmaCurveDates.ToArray();
    }

    private void Reset()
    {
      _meanReversions = CalcMeanReversions();
      _sigmas = CalcSigmas();
      Update();
    }

    private void ResetSigma()
    {
      _sigmas = CalcSigmas();
      Update();
    }

    private void ResetMeanReversion()
    {
      _meanReversions = CalcMeanReversions();
      Update();
    }

    private void Update()
    {
      _g = CalcG();
      _b = CalcB();
      _nu = CalcNu();
      _vr = CalcVr();
    }

    private double[] CalcMeanReversions()
    {
      var timeGrids = TimeGrids;
      var n = timeGrids.Length;
      var mrs = _meanReversions ?? new double[n];
      VolatilityCurve curve = HullWhiteUtil.UpdateCurve(MeanCurve,
        AsOf, MeanCurveDates, MeanVariables);
      for (int i = 0; i < n; i++)
        mrs[i] = curve.Interpolate(timeGrids[i]);
      return mrs;
    }

    private double[] CalcSigmas()
    {
      var timeGrids = TimeGrids;
      var n = timeGrids.Length;
      var sigmas = _sigmas ?? new double[n];
      VolatilityCurve curve = HullWhiteUtil.UpdateCurve(SigmaCurve,
        AsOf, SigmaCurveDates, SigmaVariables);
      for (int i = 0; i < n; i++)
        sigmas[i] = curve.Interpolate(timeGrids[i]);
      return sigmas;
    }

    private double[] CalcG()
    {
      var n = Times.Length;
      var g = _g ?? new double[n];
      var mean = MeanReversions;
      var dt = Intervals;
      for (int i = 0; i < n; i++)
        g[i] = Math.Exp(mean[i]
         * dt[i]) * (i == 0 ? 1.0 : g[i - 1]);
      return g;
    }

    private double[,] CalcB()
    {
      var n = Times.Length;
      var b = _b ?? new double[n, n];
      var mean = MeanReversions;
      for (int i = n; --i >= 0;)
      {
        var di = Times[i] - (i == 0 ? 0.0 : Times[i - 1]);
        var dai = -di * mean[i];
        var ei = Math.Exp(dai);
        var bi = HullWhiteUtil.Expd1(dai) * di;
        for (int j = i; ++j < n;)
        {
          var bij = b[i + 1, j];
          b[i, j] = bi + ei * bij;
        }
        b[i, i] = bi;
      }
      return b;
    }

    private double[] CalcNu()
    {
      var n = Times.Length;
      var nu = _nu ?? new double[n];
      var dt = Intervals;
      var mean = MeanReversions;
      var sigma = Sigmas;
      for (int i = n; --i >= 0;)
      {
        var dti = dt[i];
        var dtai = -dti * mean[i];
        var vi2 = HullWhiteUtil.Expd1(2 * dtai) * dti * Math.Pow(sigma[i], 2);
        nu[i] = Math.Sqrt(vi2);
      }
      return nu;
    }

    private double[] CalcVr()
    {
      var n = Times.Length;
      var vr = _vr ?? new double[n];
      var g = G;
      var nu = Nu;
      var sum = 0.0;
      for (int i = 0; i < n; ++i)
      {
        var gi = g[i];
        var vi = gi * nu[i];
        sum += vi * vi;
        vr[i] = sum / (gi * gi);
      }
      return vr;
    }

    private double[] CalcTimeIntervals()
    {
      var asOf = AsOf;
      var dps = TimeGrids;
      var d = new double[dps.Length];
      for (int i = 0; i < dps.Length; i++)
      {
        d[i] = (dps[i] - (i == 0 ? asOf : dps[i - 1]))/365.0;
      }
      return d;
    }

    public double[] CalcForwards()
    {
      var n = TimeGrids.Length;
      var f = new double[n];
      var p = DiscountFactors;
      var dt = Intervals;
      for (int i = 0; i < n; i++)
      {
        f[i] = -(Math.Log(p[i]) - (i == 0 ? 0.0 : Math.Log(p[i - 1]))) / dt[i];
      }
      return f;
    }

    private double[] CalcFactors(DiscountCurve curve)
    {
      var tgs = TimeGrids;
      var factors = new double[tgs.Length];
      for (int i = 0; i < tgs.Length; i++)
        factors[i] = curve.Interpolate(tgs[i]);
      return factors;
    }

    private double[] CalcT()
    {
      var dps = TimeGrids;
      var t = new double[dps.Length];
      for (int i = 0; i < dps.Length; i++)
        t[i] = (dps[i] - AsOf) / 365.0;
      return t;
    }

    public double SwaptionMarketPv(double vol,
      double swapRate, double annuity, double T)
    {
      return _volType == DistributionType.LogNormal
        ? annuity * Black.P(OptionType.Call, T, swapRate, swapRate, vol)
        : annuity * BlackNormal.P(OptionType.Call, T, 0, swapRate, swapRate, vol);
    }

    private int[] GetExpiryPositions()
    {
      int n = _expiries.Length;
      var expiryPositions = new int[n];
      for (int i = 0; i < n; i++)
      {
        var idx = Array.BinarySearch(TimeGrids, _expiries[i]);
        if (idx < 0)
          throw new InvalidOperationException("public error");
        expiryPositions[i] = idx;
      }
      return expiryPositions;
    }

    private int[] GetMaturityPositions()
    {
      int n = _expiries.Length, m = _tenors.Length, s = m * n;
      var maturityPositions = new int[s];
      for (int i = 0, k = 0; i < n; i++)
      {
        for (int j = 0; j < m; j++)
        {
          var maturity = Dt.Add(_expiries[i], Tenor.Parse(_tenors[j]));
          var idx = Array.BinarySearch(TimeGrids, maturity);
          if (idx < 0)
            throw new InvalidOperationException("public error");
          maturityPositions[k] = idx;
          k++;
        }
      }
      return maturityPositions;
    }


    private Point[] GetPoints()
    {
      int n = _expiries.Length, m = _tenors.Length, s = n * m;
      int[] eIndex = ExpiryPositions, mIndex = MaturityPositions;
      double[] times = Times, dfs = DiscountFactors, pfs = ProjFactors;
      var points = new List<Point>();

      for (int k = 0; k < s; k++)
      {
        var vol = Volatilities[k / m, k % m];
        if (vol > 0)
        {
          var begin = eIndex[k / m];
          var end = mIndex[k];
          double annuity;
          var swapRate = HullWhiteUtil.CalcSwapRate(begin, end, pfs, dfs, times, out annuity);
          var marketPv = SwaptionMarketPv(vol, swapRate, annuity, times[begin]);
          var point = new Point
          {
            Begin = begin,
            End = end,
            Volatility = vol,
            Weight = Weights[k / m, k % m],
            SwapRate = swapRate,
            Annuity = annuity,
            MarketPv = marketPv
          };
          points.Add(point);
        }
      }
      return points.ToArray();
    }

    #endregion Calculations

    #region Properties

    #region Properties of Inputs

    /// <summary>
    /// Expiry Dates
    /// </summary>
    public Dt[] Expiries => _expiries;

    /// <summary>
    /// Forward tenors
    /// </summary>
    public string[] Tenors => _tenors;

    /// <summary>
    /// Rate curve
    /// </summary>
    public DiscountCurve RateCurve => _rateCurve;


    public DiscountCurve DiscountCurve
    {
      get
      {
        if (_discountCurve == null)
          _discountCurve = HullWhiteUtil.GetDiscountCurve(RateCurve);
        return _discountCurve;
      }
    }

    /// <summary>
    /// AsOf date
    /// </summary>
    public Dt AsOf => _rateCurve?.AsOf ?? Dt.Empty;

    /// <summary>
    /// The volatitilities
    /// </summary>
    public double[,] Volatilities => _vols;
    
    /// <summary>
    /// The volatility type
    /// </summary>
    public DistributionType VolType => _volType;
    
    /// <summary>
    /// The weights
    /// </summary>
    public double[,] Weights => _weights;
    
    #endregion Properties of Inputs

    #region Properties Calculated Once

    /// <summary>
    /// All dates
    /// </summary>
    public Dt[] TimeGrids
    {
      get
      {
        if (_timeGrids == null)
          _timeGrids = GetTimeGrids();
        return _timeGrids;
      }
    }

    /// <summary>
    ///Day fraction from AsOf to different date points.
    /// </summary>
    public double[] Times
    {
      get
      {
        if (_t == null)
          _t = CalcT();
        return _t;
      }
      set { _t = value; }
    }

    /// <summary>
    /// Time inverals between two adjacent date points
    /// </summary>
    public double[] Intervals
    {
      get
      {
        if (_dt == null)
          _dt = CalcTimeIntervals();
        return _dt;
      }
     set { _dt = value; }
    }

    /// <summary>
    /// Intantaneous forward rates
    /// </summary>
    public double[] Forwards
    {
      get
      {
        if (_f == null)
          _f = CalcForwards();
        return _f;
      }
      set { _f = value; }
    }

    /// <summary>
    /// Discount factors
    /// </summary>
    public double[] DiscountFactors
    {
      get
      {
        if (_discountFactors == null)
          _discountFactors = CalcFactors(DiscountCurve);
        return _discountFactors;
      }
     set { _discountFactors = value; }
    }

    /// <summary>
    /// Calculate the projected forward rate.
    /// </summary>
    public double[] ProjFactors
    {
      get
      {
        if (_projFactors == null)
          _projFactors = CalcFactors(RateCurve);
        return _projFactors;
      }
      set { _projFactors = value; }
    }

    /// <summary>
    /// Calculate the integral of the spreads between the discount and projection curve
    /// </summary>
    public double[] Spreads
    {
      get
      {
        if (_spreads == null)
          _spreads = HullWhiteUtil.CalcSpreads(
            CalcFactors(DiscountCurve), CalcFactors(RateCurve));
        return _spreads;
      }
      set { _spreads = value; }
    }


    /// <summary>
    /// The point array
    /// </summary>
    public Point[] Points
    {
      get
      {
        if (_points == null)
          _points = GetPoints();
        return _points;
      }
    }

    /// <summary>
    /// The positions of expiry dates in the time grid
    /// </summary>
    public int[] ExpiryPositions
    {
      get
      {
        if (_expiryPositions == null)
          _expiryPositions = GetExpiryPositions();
        return _expiryPositions;
      }
    }

    /// <summary>
    /// The positions of maturity dates in the time grid
    /// </summary>
    public int[] MaturityPositions
    {
      get
      {
        if (_maturityPositions == null)
          _maturityPositions = GetMaturityPositions();
        return _maturityPositions;
      }
    }

    #endregion Properties of Calculated Once

    #region Properties Updated Everytime

    public double[,] B
    {
      get
      {
        if (_b == null)
          _b = CalcB();
        return _b;
      }
    }

    public double[] Nu
    {
      get
      {
        if (_nu == null)
          _nu = CalcNu();
        return _nu;
      }
    }

    public double[] G
    {
      get
      {
        if (_g == null)
          _g = CalcG();
        return _g;
      }
    }

    public double[] Vr
    {
      get
      {
        if (_vr == null)
          _vr = CalcVr();
        return _vr;
      }
    }

    #endregion Properties Updated Everytime

    #region Properties of Curves

    /// <summary>
    /// Sigma curve
    /// </summary>
    public VolatilityCurve SigmaCurve
    {
      get
      {
        if (_sigmaCurve == null)
          _sigmaCurve = HullWhiteUtil.CreateVolatilityCurve(AsOf);
        return _sigmaCurve;
      }
    }

    /// <summary>
    /// Mean reversion curve
    /// </summary>
    public VolatilityCurve MeanCurve
    {
      get
      {
        if (_meanCurve == null)
          _meanCurve = HullWhiteUtil.CreateVolatilityCurve(AsOf);
        return _meanCurve;
      }
    }

    /// <summary>
    /// The curve dates of mean reversion
    /// </summary>
    public Dt[] MeanCurveDates
    {
      get
      {
        if (_meanCurveDates == null)
          _meanCurveDates = GetMeanCurveDates();
        return _meanCurveDates;
      }
    }

    /// <summary>
    /// The curve dates of sigma curve
    /// </summary>
    public Dt[] SigmaCurveDates
    {
      get
      {
        if (_sigmaCurveDates == null)
          _sigmaCurveDates = GetSigmaCurveDates();
        return _sigmaCurveDates;
      }
    }

    /// <summary>
    /// Variables including mean reversion and sigma variables.
    /// </summary>
    public double[] Variables
    {
      get { return _variables; }
      set
      {
        _variables = value;
        _meanVariables = _variables?.Take(ApartPoint).ToArray();
        _sigmaVariables = _variables?.Skip(ApartPoint).ToArray();
        Reset();
      }
    }

    /// <summary>
    /// Sigma variables
    /// </summary>
    public double[] SigmaVariables
    {
      get
      {
        if (_sigmaVariables == null)
          _sigmaVariables = _variables?.Skip(ApartPoint).ToArray();
        return _sigmaVariables;
      }
      set
      {
        _sigmaVariables = value;
        ResetSigma();
      }
    }

    /// <summary>
    /// Mean reversion variables
    /// </summary>
    public double[] MeanVariables
    {
      get
      {
        if (_meanVariables == null)
          _meanVariables = _variables?.Take(ApartPoint).ToArray();
        return _meanVariables;
      }
      set
      {
        _meanVariables = value;
        ResetMeanReversion();
      }
    }

    /// <summary>
    /// Mean reversions
    /// </summary>
    public double[] MeanReversions
    {
      get {
        if (_meanReversions == null)
          _meanReversions = CalcMeanReversions();
        return _meanReversions;
      }
      set { _meanReversions = value; }
    }

    /// <summary>
    /// Sigmas
    /// </summary>
    public double[] Sigmas
    {
      get
      {
        if (_sigmas == null)
          _sigmas = CalcSigmas();
        return _sigmas;
      }
      set { _sigmas = value; }
    }

    private int ApartPoint => MeanCurveDates.Length;
    
    #endregion Curve properties

    #endregion Properties

    #region Data

    private readonly Dt[] _expiries;
    private readonly string[] _tenors;
    private readonly double[,] _vols;
    private readonly double[,] _weights;
    private readonly DistributionType _volType;

    private double[] _variables;
    private double[] _meanVariables;
    private double[] _sigmaVariables;

    private readonly DiscountCurve _rateCurve;
    private DiscountCurve _discountCurve;
    private VolatilityCurve _sigmaCurve;
    private VolatilityCurve _meanCurve;

    private double[] _meanReversions;
    private double[] _sigmas;

    private Dt[] _timeGrids;
    private Point[] _points;
    private double[] _t;

    private Dt[] _meanCurveDates;
    private Dt[] _sigmaCurveDates;

    private double[] _g;
    private double[,] _b;
    private double[] _nu;
    private double[] _vr;

    private double[] _dt;
    private double[] _f;
    private double[] _discountFactors;
    private double[] _projFactors;
    private double[] _spreads;
    private int[] _expiryPositions;
    private int[] _maturityPositions;

    #endregion Data
  }

  #endregion HullWhiteDateContainer

  #region Point
  /// <summary>
  /// The point
  /// </summary>
  public struct Point
  {
    /// <summary>
    /// The index in the time grid for start date
    /// </summary>
    public int Begin;

    /// <summary>
    /// The index in the time grid for end date
    /// </summary>
    public int End;

    /// <summary>
    /// The volatility associated to the point
    /// </summary>
    public double Volatility;

    /// <summary>
    /// The weight associated to the point
    /// </summary>
    public double Weight;

    /// <summary>
    /// The swap rate associated to the point
    /// </summary>
    public double SwapRate;

    /// <summary>
    /// The annuity associated to the point
    /// </summary>
    public double Annuity;

    /// <summary>
    /// The swaption market pv associated to the point
    /// </summary>
    public double MarketPv;
  }
  #endregion Point
}
