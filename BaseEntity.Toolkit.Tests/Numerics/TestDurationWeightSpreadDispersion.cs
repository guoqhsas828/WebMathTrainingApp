//
// Copyright (c)    2002-2018. All rights reserved.
//

using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Calibrators;

namespace BaseEntity.Toolkit.Tests.Numerics
{


  [TestFixture]
  public class TestDurationWeightSpreadDispersion
  {
    
    [Test]
    public static void TestDWeightedSpreadDispersion()
    {
      _survivalCurves = new RateCurveBuilder().CreateSurvivalCurves(_asOf, 0);
      var spreadSamples = _survivalCurves
        .Select(c => CurveUtil.ImpliedSpread(c, _cdxMaturity, _dc, _freq, _roll, _cal))
        .ToArray();

      var durationSamples = _survivalCurves
        .Select(c => CurveUtil.ImpliedDuration(c, c.AsOf, _cdxMaturity, _dc, _freq, _roll, _cal))
        .ToArray();

      var weights = Enumerable.Repeat(1.0/_survivalCurves.Length, _survivalCurves.Length).ToArray();

      var dWeights = Enumerable.Range(0, weights.Length)
        .Select(i => weights[i] * durationSamples[i]).ToArray();

      var stats = new StatisticsBuilder();
      for (int i = 0; i < spreadSamples.Length; ++i)
        stats.Add(dWeights[i], spreadSamples[i]);

      var sdExpect = stats.StdDev;

      var sdActual = BasketUtil.CalcDWeightedISpreadDispersion(
        _cdxMaturity, _survivalCurves, weights, _dc, _freq, _roll, _cal);

      Assert.AreEqual(sdExpect, sdActual, 1E-15);
    }

    
    #region Data

    private static readonly Dt _asOf = new Dt(20150220); // Trade asof Date
    private static readonly Dt _cdxMaturity = new Dt(20191220);
    private static readonly double indexQuote_ = 63.47;

    private static readonly DayCount _dc = DayCount.Actual365Fixed;
    private static readonly Frequency _freq = Frequency.Quarterly;
    private static readonly BDConvention _roll = BDConvention.Modified;
    private static readonly Calendar _cal = Calendar.NYB;

    private static SurvivalCurve[] _survivalCurves;

    #endregion Data
  }
}

