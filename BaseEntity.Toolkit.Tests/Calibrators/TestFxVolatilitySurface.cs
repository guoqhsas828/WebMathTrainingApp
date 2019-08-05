// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  /// <summary>
  ///  Test volatility surface building.
  /// </summary>
  /// <remarks></remarks>
  [TestFixture("EURUSD-vol-surface-average")]
  [TestFixture("EURUSD-vol-surface-strangle")]
  public class TestFxVolatilitySurface : ToolkitTestBase
  {
    public TestFxVolatilitySurface(string name) : base(name)
    {}

    /// <summary>
    /// Gets or sets the volatility quote term.
    /// </summary>
    /// <value>The volatility quote term.</value>
    /// <remarks></remarks>
    public FxVolatilityQuoteTerm VolatilityQuoteTerm { get; set; }

    /// <summary>
    /// Gets or sets the tenors.
    /// </summary>
    /// <value>The tenors.</value>
    public string[] Tenors { get; set; }

    /// <summary>
    /// Gets or sets the ccy1 rates.
    /// </summary>
    /// <value>The ccy1 rates.</value>
    public double[] Ccy1Rates { get; set; }

    /// <summary>
    /// Gets or sets the ccy2 rates.
    /// </summary>
    /// <value>The ccy2 rates.</value>
    public double[] Ccy2Rates { get; set; }

    /// <summary>
    /// Gets or sets the fx rates.
    /// </summary>
    /// <value>The fx rates.</value>
    public double[] FxRates { get; set; }

    /// <summary>
    /// Gets or sets the delta terms.
    /// </summary>
    /// <value>The delta terms.</value>
    public string[] DeltaSpecs { get; set; }

    /// <summary>
    /// Gets or sets the volatilities.
    /// </summary>
    /// <value>The volatilities.</value>
    public double[,] StickyDeltaVolatilities { get; set; }

    /// <summary>
    /// Gets or sets the delta terms.
    /// </summary>
    /// <value>The delta terms.</value>
    public string[] RrBfSpecs { get; set; }

    /// <summary>
    /// Gets or sets the volatilities.
    /// </summary>
    /// <value>The volatilities.</value>
    public double[,] RrBfVolatilities { get; set; }
   
    private static DiscountCurve BuildDiscountCurve(Dt asOf,
      IEnumerable<KeyValuePair<Dt,double>> dateRates)
    {
      var dc = new DiscountCurve(asOf);
      foreach (var p in dateRates)
      {
        var dt = p.Key;
        var df = 1.0 / (1 + Dt.Fraction(asOf, dt,
          DayCount.Actual365Fixed) * p.Value);
        dc.Add(dt, df);
      }
      return dc;
    }

    private static FxCurve BuildFxCurve(Dt asOf,
      Currency ccy1, Currency ccy2,
      string[] tenors, Dt[] dates, double[] fxRates)
    {
      return FxCurve.Create(asOf, asOf, ccy1, ccy2, fxRates[0], tenors, dates, fxRates,
        null, null, null, Calendar.None, BasisSwapSide.Default, null, null, null, "FxCurve");
    }

    private Dt _asOf;
    private DeltaSpec[] _deltaSpecs;
    private FxRrBfSpec[] _rrBfSpecs;
    private int _atmIndex;

    [OneTimeSetUp]
    public void SetUp()
    {
      _asOf = Dt.Today();
      _deltaSpecs = DeltaSpecs.Select(DeltaSpec.Parse).ToArray();
      _rrBfSpecs = RrBfSpecs.Select(FxRrBfSpec.Parse).ToArray();
      _atmIndex = Array.IndexOf(_deltaSpecs, DeltaSpec.Atm);
      if(_atmIndex <0)
      {
        throw new Exception("Atm volatility not found.");
      }
    }

    #region Smile from delta sticky quotes
    [Test]
    public void TestSmileBuilderFromDeltaQuotes()
    {
      int rows = StickyDeltaVolatilities.GetLength(0);
      if (rows > Tenors.Length) rows = Tenors.Length;
      for (int i = 0; i < rows; ++i)
      {
        Dt asOf = _asOf, date = Dt.Add(_asOf, Tenors[i]);
        double fwdFxRate = FxRates[i];
        double time = Dt.Fraction(asOf, date, DayCount.Actual365Fixed),
          sqrtTime = Math.Sqrt(time);
        double pd = 1 / (1 + Ccy2Rates[i] * time);
        double pf = 1 / (1 + Ccy1Rates[i] * time);
        double spot = fwdFxRate * pd / pf;
        var model = new BlackScholesParameterData(time,
          spot, -Math.Log(pf) / time, -Math.Log(pd) / time);

        var smileFn = Enumerable.Range(0, StickyDeltaVolatilities.GetLength(1))
          .Select(j =>
          {
            double sigma = StickyDeltaVolatilities[i, j] / 100;
            double strike;
            var ds = _deltaSpecs[j];
            if (ds.IsAtm)
            {
              double sigmaT = sigma * sqrtTime;
              strike = fwdFxRate * Math.Exp(0.5 * sigmaT * sigmaT);
            }
            else
            {
              int sign = ds.IsPut ? 1 : (-1);
              double sigmaT = sigma * sqrtTime;
              strike = fwdFxRate * Math.Exp(sigmaT * (
                sign * QMath.NormalInverseCdf(ds.Delta / pf) + 0.5 * sigmaT));
            }
            return new StrikeVolatilityPair(strike, sigma);
          }).BuildQuadraticRegressionSmile(model, new Linear());

        // Serialization should throw no exception.
        smileFn = smileFn.CloneObjectGraph(CloneMethod.Serialization);

        // Now do roundtrip tests
        CheckRoundTrips(smileFn.EvaluateStrike, i, fwdFxRate, pf, sqrtTime);
      }
      return;
    }

    private void CheckRoundTrips(Func<double, double> smileFn,
      int i, double fwdFxRate, double pf, double sqrtTime)
    {
      for (int j = 0, n = StickyDeltaVolatilities.GetLength(1); j < n; ++j)
      {
        double sigma = StickyDeltaVolatilities[i, j] / 100;
        double strike;
        var ds = _deltaSpecs[j];
        if (ds.IsAtm)
        {
          double sigmaT = sigma * sqrtTime;
          strike = fwdFxRate * Math.Exp(0.5 * sigmaT * sigmaT);
        }
        else
        {
          int sign = ds.IsPut ? 1 : (-1);
          double sigmaT = sigma * sqrtTime;
          strike = fwdFxRate * Math.Exp(sigmaT * (
            sign * QMath.NormalInverseCdf(ds.Delta / pf) + 0.5 * sigmaT));
        }
        double sigma2 = smileFn(strike);
        Assert.AreEqual(sigma, sigma2, 1E-12, ds.ToString() + '/' + Tenors[i]);
      }
    }
    #endregion

    #region Smile from RR BF quotes
    [Test]
    public void TestSmileBuilderFromRrBfQuotes()
    {
      var asOf = _asOf;
      var timeInterp = new Linear(new Const(), new Const());
      var smileInterp = new Linear();

      // Create the discount curves
      var ccy1DiscountCurve = BuildDiscountCurve(asOf,
        Tenors.Select((s, i) => new KeyValuePair<Dt, double>(
          Dt.Add(asOf, s), Ccy1Rates[i])));
      var ccy2DiscountCurve = BuildDiscountCurve(asOf,
        Tenors.Select((s, i) => new KeyValuePair<Dt, double>(
          Dt.Add(asOf, s), Ccy2Rates[i])));

      // Create the FX curve
      var fxCurve = BuildFxCurve(asOf,
        VolatilityQuoteTerm.Ccy1, VolatilityQuoteTerm.Ccy2,
        Tenors,
        Tenors.Select((s, i) => Dt.Add(asOf, s)).ToArray(),
        FxRates);

      // Create Tenors
      var tenors = Tenors
        .Select(BuildRrBfSingleTenor)
        .Where(t => t != null)
        .OrderBy(t => t.Maturity);

      // Create VV calibrator and vol surface
      var calibrator = new FxVolatilitySurfaceCalibrator(
        asOf, timeInterp,  smileInterp,
        ccy2DiscountCurve, ccy1DiscountCurve, fxCurve);
      var surface = calibrator.BuildSurface(tenors, SmileInputKind.Strike);

      var specs = _rrBfSpecs;

      Assert.IsTrue(surface.Tenors.Select(tenor =>
        AssertRrBfRoundTrip((FxRrBfVolatilityTenor)tenor, surface, specs))
        .All(b => b));
    }

    private FxRrBfVolatilityTenor BuildRrBfSingleTenor(
      string tenorName, int tenorIndex)
    {
      if (RrBfVolatilities[tenorIndex, 0] <= 0) return null;
      var term = VolatilityQuoteTerm;
      var asOf = _asOf;
      var tenor = Tenor.Parse(tenorName);
      var date = Dt.Add(asOf, tenor);
      var quotes = RrBfSpecs.Select((s, j) =>
        new KeyValuePair<string, double>(s, RrBfVolatilities[tenorIndex, j]));
      return new FxRrBfVolatilityTenor.Builder(quotes.Select(
        p => new KeyValuePair<FxRrBfSpec, double>(FxRrBfSpec.Parse(p.Key), p.Value)))
        .ToFxRrBfVolatilityTenor(tenorName, date, term.GetFlags(tenor));
    }

    private bool AssertRrBfRoundTrip(FxRrBfVolatilityTenor tenor,
      CalibratedVolatilitySurface surface, FxRrBfSpec[] specs)
    {
      if(!RoundTripStrikeVolatiltyPairs(tenor,surface))
        return false;

      int index = Array.IndexOf(Tenors, tenor.Name);
      if (index < 0)
      {
        throw new Exception("Extra tenor " + tenor.Name);
      }

      var calibrator = (FxVolatilitySurfaceCalibrator)surface.Calibrator;
      var data = calibrator.GetParameters(tenor.Maturity);
     
      foreach (var quote in tenor.Quotes)
      {
        var spec = FxRrBfSpec.Parse(quote.Key);
        int j = Array.IndexOf(specs, spec);
        if (j < 0)
        {
          throw new Exception("Extra spec " + quote.Key);
        }
        // First make sure the tenor quote matches the input.
        var expect = RrBfVolatilities[index, j];
        var actual = quote.Value;
        if (!actual.IsAlmostSameAs(expect, 1))
        {
          Assert.AreEqual(expect, actual, 0.0,
            "Original." + tenor.Name + "[" + j + ']');
        }
        // Then round trip the quote.
        actual = CalculateQuoteValue(spec, tenor, data, surface);
        if (Math.Abs(actual-expect) >= 1E-10)
        {
          Assert.AreEqual(expect, actual, 1E-12,
            tenor.Name + ' ' + spec);
        }

      }
      return true;
    }

    private static bool RoundTripStrikeVolatiltyPairs(
      FxRrBfVolatilityTenor tenor,
      CalibratedVolatilitySurface surface)
    {
      Dt date = tenor.Maturity;
      foreach (var pair in tenor.StrikeVolatilityPairs)
      {
        var expect = pair.Volatility;
        var actual = surface.Interpolate(date, pair.Strike);
        if (!actual.IsAlmostSameAs(expect))
        {
          Assert.AreEqual(expect, actual, 1E-16,
            "Volatility@" + pair.Strike);
          return false;
        }
      }
      return true;
    }

    private static double CalculateQuoteValue(FxRrBfSpec spec,
      FxRrBfVolatilityTenor tenor,
      IBlackScholesParameterData data,
      CalibratedVolatilitySurface surface)
    {
      var atmStrike = data.GetAtmStrike(tenor.AtmSetting,
        tenor.PremiumIncludedDelta, tenor.AtmQuote);
      var sigAtm = surface.Interpolate(tenor.Maturity, atmStrike);
      var delta = spec.Delta;
      if (!tenor.ForwardDelta)
      {
        delta = data.ConvertSpotToForwardDelta(delta);
      }
      var smile = GetSmile(data.Time, surface);
      if (spec.IsAtm)
      {
        double strike, sigma;
        data.GetAtmStrikeAndVolatility(smile.EvaluateLogMoneyness,
          tenor.AtmSetting,tenor.PremiumIncludedDelta,
          out strike, out sigma);
        return sigma;
      }
      var mPut1 = smile.EvaluateDelta(delta, -1);
      var mPut = BlackScholes.GetLogMoneynessFromDelta(
        smile.EvaluateLogMoneyness, delta, Math.Sqrt(data.Time), -1);
      var sigPut = smile.EvaluateLogMoneyness(mPut);
      var mCall1 = smile.EvaluateDelta(delta, 1);
      var mCall = BlackScholes.GetLogMoneynessFromDelta(
        smile.EvaluateLogMoneyness, delta, Math.Sqrt(data.Time),1);
      var sigCall = smile.EvaluateLogMoneyness(mCall);
      if (spec.IsRiskReversal)
      {
        return tenor.Ccy2RiskReversal
          ? sigPut - sigCall
          : sigCall - sigPut;
      }

      // spec.IsButterfly
      if (tenor.OneVolalityBufferfly)
      {
        return smile.ImplyStrangleVolatility(
          delta, tenor.PremiumIncludedDelta,
          tenor.Ccy2Strangle) - sigAtm;
      }
      return 0.5 * (sigCall + sigPut) - sigAtm;
    }

    private static QuadraticRegressionSmile GetSmile(
      double time,
      CalibratedVolatilitySurface surface)
    {
      var surfaceInterpolator = (VolatilitySurfaceInterpolator)surface.Interpolator;
      var seqInterpolator = (SequentialInterpolator2D)surfaceInterpolator.SurfaceFunction.Target;
      var fs = seqInterpolator.Functions;
      var index = seqInterpolator.GetIndex(time);
      if(index >= fs.Length)
        index = fs.Length - 1;
      var smile = (QuadraticRegressionSmile)fs[index].Target;
      return smile;
    }
    #endregion
  }
}
