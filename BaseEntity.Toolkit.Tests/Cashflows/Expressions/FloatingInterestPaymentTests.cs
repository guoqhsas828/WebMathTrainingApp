//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Cashflows.Expressions.Payments;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  [TestFixture]
  public class FloatingInterestPaymentTests
  {
    private DiscountCurve _discountCurve, _projectCurve;

    [OneTimeSetUp]
    public void Initialize()
    {
      var asOf = new Dt(20140626);
      const string fedfunds = @"toolkit\test\data\FEDFUNDS-20140826.xml";
      _discountCurve = CurveLoader.GetDiscountCurve(
        fedfunds, asOf);

      const string usdlibor3M = @"toolkit\test\data\USDLIBOR3M-20140826.xml";
      _projectCurve = CurveLoader.GetProjectionCurve(
        usdlibor3M, _discountCurve, asOf);
    }


    [TestCase("7Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void SimpleRates(string tenor, double spread)
    {
      TestPricer(tenor, spread, false);
    }

    [TestCase("7Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void SimpleRatesOverlay(string tenor, double spread)
    {
      TestPricer(tenor, spread, true);
    }

    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    public void SimpleCap(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.None,
        0.025, null);
    }

    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    public void SimpleCapOverlay(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.None,
        0.025, null);
    }

    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    public void SimpleFloor(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.None,
        null, 0.01);
    }

    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    public void SimpleFloorOverlay(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.None,
        null, 0.01);
    }

    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    public void SimpleCapFloor(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.None,
        0.03, 0.01);
    }

    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    public void SimpleCapFloorOverlay(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.None,
        0.03, 0.01);
    }

    [TestCase("5Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void CompoundingIsda(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.ISDA);
    }

    [TestCase("7Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void CompoundingIsdaOverlay(string tenor, double spread)
    {
      TestPricer(tenor, spread, true, CompoundingConvention.ISDA);
    }

    [TestCase("5Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void CompoundingFlatIsda(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.FlatISDA);
    }

    [TestCase("7Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void CompoundingFlatIsdaOverlay(string tenor, double spread)
    {
      TestPricer(tenor, spread, true, CompoundingConvention.FlatISDA);
    }

    [TestCase("5Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void CompoundingSimple(string tenor, double spread)
    {
      TestPricer(tenor, spread, false, CompoundingConvention.Simple);
    }

    [TestCase("7Y", 0.0)]
    [TestCase("20Y", 0.0)]
    [TestCase("40Y", 0.0)]
    [TestCase("7Y", 0.002)]
    [TestCase("20Y", 0.002)]
    [TestCase("40Y", 0.002)]
    public void CompoundingSimpleOverlay(string tenor, double spread)
    {
      TestPricer(tenor, spread, true, CompoundingConvention.Simple);
    }


    private void TestPricer(string tenor, double spread, bool withOverlay,
      CompoundingConvention cmpd = CompoundingConvention.None,
      double? cap = null, double? floor = null)
    {
      var index = _projectCurve.ReferenceIndex;
      Dt effective = new Dt(20101015), maturity = Dt.Add(effective, tenor);
      var compounding = cmpd != CompoundingConvention.None;
      var floating = new SwapLeg(effective, maturity,
        compounding ? Frequency.Annual : Frequency.Quarterly,
        spread, index)
      {
        Cap = cap,
        Floor = floor,
        CompoundingConvention = cmpd,
        CompoundingFrequency = compounding
          ? index.IndexTenor.ToFrequency() : Frequency.None,
        ProjectionType = ProjectionType.SimpleProjection
      };


      double notional = 5000000;
      Dt asOf = _discountCurve.AsOf,
        settle = Dt.AddDays(asOf, 2, index.Calendar);
      double currentReset = Double.NaN, nextReset = Double.NaN;
      var modelParams = new RateModelParameters(
        RateModelParameters.Model.BGM,
        new[] { RateModelParameters.Param.Sigma },
        new[] { new VolatilityCurve(asOf, 0.0), },
        index.IndexTenor, index.Currency);
      var pricer = new SwapLegPricer(floating, asOf, settle,
        notional, _discountCurve, index, _projectCurve,
        new RateResets(currentReset, nextReset),
        cap.HasValue || floor.HasValue ? modelParams : null,
        null)
      {
        ApproximateForFastCalculation = true,
      };

      using (Evaluable.PushVariants(withOverlay
        ? new object[] { _discountCurve } : null))
      {
        var expect = pricer.Pv(); // -2907547.846
        var ps = pricer.GetPaymentSchedule(null, asOf);
        var ep = PaymentExpression.GetPayments(ps, _discountCurve)
          .ToArray();
        var actual = ep.FullPv(0, settle,
          _discountCurve.DiscountFactor(asOf, settle))
          * _discountCurve.DiscountFactor(asOf, settle)
          * pricer.Notional;
        var tol = 1E-15 * notional * (maturity - asOf) / 365;
        Assert.That(actual,Is.EqualTo(expect).Within(tol));
      }
      return;
    }

  }
}
