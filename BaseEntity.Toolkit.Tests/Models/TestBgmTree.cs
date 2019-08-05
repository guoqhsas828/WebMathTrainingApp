//
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestBgmTree
  {
    // Get a delegate to call the method on an internal class.
    private static readonly Func<double, double, Dt, DiscountCurve,
      IList<Dt>, IList<VolatilityCurve>, IList<Dt>, IRateSystemDistributions>
      CalculateRateSystem = DelegateFactory.GetFunc<double, double, Dt,
        DiscountCurve, IList<Dt>, IList<VolatilityCurve>, IList<Dt>, IRateSystemDistributions>
        ("CalculateRateSystem",
          Type.GetType("BaseEntity.Toolkit.Models.BGM.BgmBinomialTree, BaseEntity.Toolkit"));

    private static readonly Func<IRateSystemDistributions, IList<double>> GetFractions =
      DelegateFactory.GetFunc<IRateSystemDistributions, IList<double>>
        ("GetFractions", typeof (RateCalculator));

    /// <summary>
    ///  Find a map from i to j such that <c>from[i] = to[j]</c>,
    ///  or <c>from[i] = to[map[i]]</c>.
    /// </summary>
    /// <typeparam name="T">element type</typeparam>
    /// <param name="from">From.</param>
    /// <param name="to">To.</param>
    /// <returns>The index </returns>
    public static int[] CreateIndexMap<T>(IList<T> from, IList<T> to)
    {
      int n = from.Count - 1;
      int[] map = new int[n];
      for (int i = 0; i < n; ++i)
      {
        int j = to.IndexOf(from[i]);
        if(j <0)
        {
          throw new ToolkitException(String.Format(
            "The destination list does not contain {0}",
            from[i]));
        }
        map[i] = j;
      }
      return map;
    }

    [Test]
    public void Test05()
    {
      int dim = 5;

      // Set up the basic data.
      var data = new BgmTestData();
      data.Generate = () => BgmTestData.Rng.Uniform(0.25, 0.55);
      data.Frequency = Frequency.Annual;
      const char tenorChar = 'Y';
      
      // Build a set of random volatility curves
      Dt asOf = Dt.Today();
      var v = new RandomForwardVolatilityCurves(data, asOf, dim, true);

      // Build a random discount curve.
      var dc = v.BuildRateCurve(()=>BgmTestData.Rng.Uniform(0.01,0.06));
      double[] rates, dfracs;
      v.CalculateRatesAndDiscountedFractions(dc, out rates, out dfracs);

      // Build a matrix of swaption volatilities
      var swpnVolatilities = v.BuildSwaptionVolatilities(
        DistributionType.LogNormal, dc, 1.0);

      // Build expiries/tenors
      var tenors = new string[dim];
      for (int i = 0; i < dim; ++i)
      {
        tenors[i] = (i + 1).ToString() + tenorChar;
      }

      // Now call the cailbrator to build a surface
      var surface = BgmForwardVolatilitySurface.Create(asOf,
        new BgmCalibrationParameters { CalibrationMethod = VolatilityBootstrapMethod.Cascading },
        dc, tenors, tenors,
        data.CycleRule, data.BDConvention, data.Calendar,
        v.MakeCorrelation(v.Count, 1.0),
        CloneUtil.Clone(swpnVolatilities),
        DistributionType.LogNormal);

      // The maturity dates
      var dates = dc.Select((p) => p.Date).ToList();

      // We add some node dates in the middle of each reset period.
      var nodeDates = new List<Dt>();
      for (int i = 0; i < dates.Count;++i)
      {
        Dt begin = i == 0 ? asOf : dates[i - 1];
        nodeDates.Add(Dt.Add(begin, (int) (dates[i] - begin)/2));
      }

      // Build the binomial tree
      var curves = ((BgmCalibratedVolatilities)surface.CalibratedVolatilities)
        .BuildBlackVolatilityCurves();
      var rds = CalculateRateSystem(0.01, 0, asOf, dc, dates, curves, nodeDates);

      // Let's caculate the caplet price and volatility
      var map = CreateIndexMap(rds.TenorDates, rds.NodeDates);
      var fractions = GetFractions(rds).ToArray();
      for (int i = 1; i <= map.Length; ++i )
      {
        double rate = rds.GetRate(i, 0, 0);
        double strike = rate;
        double frac = fractions[i];
        var actual = rds.CalculateCapletValue(
          OptionType.Call, map[i - 1], i, strike, frac);
        var expect = dc.DiscountFactor(asOf, dates[i])*frac
          *QMath.Black(rate, strike, curves[i - 1].GetVal(0)
            *Math.Sqrt((dates[i-1] - asOf)/365.0));
        Assert.AreEqual(expect, actual, 1E-4);
      }

      return;
    }
  }
}
