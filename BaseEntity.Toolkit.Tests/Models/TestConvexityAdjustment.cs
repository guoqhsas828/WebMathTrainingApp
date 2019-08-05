//
// Copyright (c)    2002-2018. All rights reserved.
//
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestConvexityAdjustment : ToolkitTestBase
  {
    [Test]
    public void EDFuturesHullWhite()
    {
      Dt asOf = new Dt(27, 6, 2013);
      Dt begin = Dt.AddMonths(asOf, 18, CycleRule.Twentieth);
      Dt end = Dt.AddMonths(asOf, 24, CycleRule.Twentieth);
      double years = Dt.Years(asOf, begin, DayCount.Actual365Fixed);
      double term = Dt.Years(begin, end, DayCount.Actual365Fixed);

      double sigma = 0.02;
      double rate = 0.5;
      var speeds = new[]
      {
        1.2, 0.7, 0.3, 0.1, 0.01, 0.001, 1E-4, 1E-5, 1E-6, 1E-7,
      };
      var expects = new[]
      {
        -9.5060182981818934E-05, -0.00018802232838154438,
        -0.0003504076947383278, -0.00049195855071288168,
        -0.00057676406449286646, -0.00058613910485242249,
        -0.00058708624624675585, -0.00058718105749254121,
        -0.00058719053959224287, -0.0005871914878116314,
      };

      TestHullWhiteModel(rate, years, term,
        sigma, speeds, expects);
      TestHullWhiteForwardAdjustment(rate, asOf,
        begin, end, sigma, speeds, expects);
    }

    private static void TestHullWhiteModel(
      double rate, double years, double term, double sigma,
      double[] speeds, double[] expects)
    {
      //var sb = new StringBuilder();
      for (int i = 0; i < speeds.Length; ++i)
      {
        var ca = ConvexityAdjustments.EDFutures(
          rate, years, term, sigma, speeds[i]);
        //sb.Append(ca.ToString("R")).Append(',');
        Assert.AreEqual(expects[i], ca, 2E-16);
      }
      //var x = sb.ToString();
      var ca0 = ConvexityAdjustments.EDFutures(rate,
        years, term, sigma, FuturesCAMethod.Hull);
      Assert.AreEqual(ca0, expects[speeds.Length - 1], 2E-10);
    }

    private static void TestHullWhiteForwardAdjustment(
      double rate, Dt asOf, Dt begin, Dt end, double sigma,
      double[] speeds, double[] expects)
    {
      var discountCurve = new DiscountCurve(asOf, rate);
      var parNames = new[]
      {
        RateModelParameters.Param.Sigma,
        RateModelParameters.Param.MeanReversion,
      };
      var fixSched = new ForwardRateFixingSchedule
      {
        ResetDate = begin,
        EndDate = end,
        StartDate = begin,
      };
      var fixing = new Fixing
      {
        Forward = rate,
        RateResetState = RateResetState.IsProjected,
      };

      for (int i = 0; i < speeds.Length; ++i)
      {
        var parData = new IModelParameter[]
        {
          new VolatilityCurve(asOf, sigma),
          new VolatilityCurve(asOf, speeds[i]),
        };
        var rmp = new RateModelParameters(RateModelParameters.Model.Hull,
          parNames, parData, null);
        var fadj = new FuturesAdjustment(asOf, discountCurve, rmp);
        var ca = fadj.ConvexityAdjustment(end, fixSched, fixing);
        Assert.AreEqual(-expects[i], ca, 2E-16);
      }
    }

  }
}
