// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestChainedSwapEquation
  {
    #region Types and data

    private readonly Dt _asOf, _settle, _maturity;
    private readonly DiscountCurve[] _curves;

    private SwapPricer p3MFixed, p6MFixed, p1YFixed, pOisFixed,
      pOis3M, pOis6M, pOis1Y, p3M6M, p3M1Y, p6M1Y;

    private struct ChainData
    {
      internal Swap[] Input, Output;
    }

    private readonly ChainData[] _data;

    #endregion

    #region Constructor

    public TestChainedSwapEquation()
    {
      var ois1D = GetIndex("USDFEDFUNDS_1D");

      var cal = ois1D.Calendar;
      var asOf = _asOf = new Dt(20150406);
      var settle = _settle = Dt.AddDays(asOf, ois1D.SettlementDays, cal);
      _maturity = Dt.Add(settle, "5Y");

      _curves = new[]
      {
        new DiscountCurve(asOf, 0.02)
        {
          Name = "OIS Curve",
          ReferenceIndex = ois1D
        },
        new DiscountCurve(asOf, 0.03)
        {
          Name = "LIBOR 3M Curve",
          ReferenceIndex = GetIndex("USDLIBOR_3M")
        },
        new DiscountCurve(asOf, 0.04)
        {
          Name = "LIBOR 6M Curve",
          ReferenceIndex = GetIndex("USDLIBOR_6M")
        },
        new DiscountCurve(asOf, 0.05)
        {
          Name = "LIBOR 1Y Curve",
          ReferenceIndex = new InterestRateIndex("USDLIBOR", Frequency.Annual,
            Currency.USD, DayCount.Actual360, Calendar.NYB, 2)
        },
      };

      p3MFixed = GetSwapPricer("3M", null, true);
      p6MFixed = GetSwapPricer("6M", null, true);
      p1YFixed = GetSwapPricer("1Y", null, true);
      pOisFixed = GetSwapPricer("1D", null, true);
      p3M6M = GetSwapPricer("3M", "6M");
      p3M1Y = GetSwapPricer("3M", "1Y");
      p6M1Y = GetSwapPricer("6M", "1Y");
      pOis3M = GetSwapPricer("1D", "3M");
      pOis6M = GetSwapPricer("1D", "6M");
      pOis1Y = GetSwapPricer("1D", "1Y");

      _data = new[]
      {
        // Chain of 1 swap
        new ChainData
        {
          Input = new[] {pOisFixed.Swap, p6MFixed.Swap, pOis3M.Swap},
          Output = new[] {pOisFixed.Swap}
        },
        new ChainData
        {
          Input = new[] {pOis3M.Swap, pOis6M.Swap, pOisFixed.Swap},
          Output = new[] {pOisFixed.Swap}
        },
        // Chain of 2 swaps
        new ChainData
        {
          Input = new[] {pOis6M.Swap, p6MFixed.Swap, pOis3M.Swap},
          Output = new[] {pOis6M.Swap, p6MFixed.Swap}
        },
        new ChainData
        {
          Input = new[] {pOis3M.Swap, pOis6M.Swap, p6MFixed.Swap},
          Output = new[] {pOis6M.Swap, p6MFixed.Swap}
        },
        // Chain of 3 swaps
        new ChainData
        {
          Input = new[] {p6MFixed.Swap, p3M6M.Swap, pOis3M.Swap},
          Output = new[] {pOis3M.Swap, p3M6M.Swap, p6MFixed.Swap}
        },
        new ChainData
        {
          Input = new[] {p3M6M.Swap, pOis3M.Swap, p6MFixed.Swap},
          Output = new[] {pOis3M.Swap, p3M6M.Swap, p6MFixed.Swap}
        },
        // Chain of 4 swaps
        new ChainData
        {
          Input = new[] {p3MFixed.Swap, p3M6M.Swap, p6M1Y.Swap, pOis1Y.Swap},
          Output = new[] {pOis1Y.Swap, p6M1Y.Swap, p3M6M.Swap, p3MFixed.Swap}
        },
        // This should fail, having no chain
        new ChainData
        {
          Input = new[] {pOis3M.Swap, pOis6M.Swap, p3M6M.Swap},
          Output = null
        },
      };
    }

    private static ReferenceIndex GetIndex(string indexName)
    {
      return StandardReferenceIndices.Create(indexName);
    }

    #endregion

    // Projection curve
    [Test]
    public void TestProjectionFit()
    {
      var asOf = new Dt(20110916);
      var dicountCurve = new DiscountCurve(asOf, 0.02)
      {
        Name = "USDFEDFUNDS_1D",
        ReferenceIndex = StandardReferenceIndices.Create("USDFEDFUNDS_1D")
      };
      var projectionCurve = new DiscountCurve(asOf, 0.01)
      {
        Name = "USDLIBOR_3M",
        ReferenceIndex = StandardReferenceIndices.Create("USDLIBOR_3M")
      };
      var targetIndex = StandardReferenceIndices.Create("USDLIBOR_6M");
      var calibrator = new ProjectionCurveFitCalibrator(
        asOf, dicountCurve,targetIndex,new []{projectionCurve},
        new CalibratorSettings());
      var curve = new DiscountCurve(calibrator)
      {
        Name = "USDLIBOR_6M",
      };
      curve.AddSwap("USDLIBOR_6M.USDLIBOR_3M.BasisSwap_1Yr", 1.0, 
        new Dt(20110920), new Dt(20120920), 0.00159,
        Frequency.SemiAnnual, Frequency.Quarterly,
        targetIndex, projectionCurve.ReferenceIndex,
        Calendar.None, null);

      calibrator.CurveFitSettings.ChainedSwapApproach = false;
      curve.Fit();
      var df0 = curve.GetVal(0);
      var pricer0 = calibrator.GetPricer(curve, curve.Tenors[0].Product);
      var pv0 = pricer0.Pv();

      calibrator.CurveFitSettings.ChainedSwapApproach = true;
      curve.Fit();
      var df1 = curve.GetVal(0);
      var pricer1 = calibrator.GetPricer(curve, curve.Tenors[0].Product);
      var pv1 = pricer0.Pv();

      Assert.AreEqual(df0, df1, 1E-14);
      return;
    }


    [Test]
    public void ParCoupons()
    {
      Assert.AreEqual(0.02, GetParCoupon(pOisFixed), 1E-3);
      Assert.AreEqual(0.03, GetParCoupon(p3MFixed), 1E-3);
      Assert.AreEqual(0.04, GetParCoupon(p6MFixed), 1E-3);
      Assert.AreEqual(0.05, GetParCoupon(p1YFixed), 1E-3);
      Assert.AreEqual(0.01, GetParCoupon(p3M6M), 1E-3);
      Assert.AreEqual(0.02, GetParCoupon(p3M1Y), 1E-3);
      Assert.AreEqual(0.01, GetParCoupon(p6M1Y), 1E-3);
      Assert.AreEqual(0.01, GetParCoupon(pOis3M), 1E-3);
      Assert.AreEqual(0.02, GetParCoupon(pOis6M), 1E-3);
      Assert.AreEqual(0.03, GetParCoupon(pOis1Y), 1E-3);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void SwapChain(int index)
    {
      var expects = _data[index].Output;
      var list = _data[index].Input;
      var count = list.FindChain(_curves[0].ReferenceIndex);

      if (expects == null || expects.Length == 0)
      {
        // Check no chain found
        Assert.AreEqual(0, count);
        return;
      }

      // Check that the chain are expected
      Assert.AreEqual(expects.Length, count);
      for (int i = 0; i < count; ++i)
        Assert.AreEqual(expects[i], list[i]);

      // Check that the synthesized equation holds
      var diff = CalculateEquation(list, count, _settle, _curves[0]);
      Assert.AreEqual(0.0, diff, 1E-15);
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(6)]
    public void TestCalibrator(int index)
    {
      var calibrator = new DiscountCurveFitCalibrator(
        _asOf, _curves[0].ReferenceIndex,
        new CalibratorSettings {ChainedSwapApproach = true});
      var dicountcurve = new DiscountCurve(calibrator);
      foreach (var swap in _data[index].Input)
      {
        dicountcurve.Add(swap, 0.0);
      }
      dicountcurve.Fit();

      var df0 = _curves[0].DiscountFactor(_asOf, _maturity);
      var df1 = dicountcurve.DiscountFactor(_asOf, _maturity);
      Assert.AreEqual(df0, df1, 1E-11);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void TestOldRoutineNewRoutineConsistent(int index)
    {
      var disCalibrator = new DiscountCurveFitCalibrator(
        _asOf, _curves[0].ReferenceIndex,
        new CalibratorSettings{ChainedSwapApproach = false});
      var dicountCurve1 = new DiscountCurve(disCalibrator);
      foreach (var swap in _data[index].Output)
      {
        dicountCurve1.Add(swap, 0.0);
      }
      dicountCurve1.Fit();

      var df0 = _curves[0].DiscountFactor(_asOf, _maturity);
      var df1 = dicountCurve1.DiscountFactor(_asOf, _maturity);
      Assert.AreEqual(df0, df1, 1E-11);

      var multiCalibrator = new DiscountCurveFitCalibrator(_asOf,
        _curves[0].ReferenceIndex,
        new CalibratorSettings{ChainedSwapApproach = true});
      var discountCurve2 = new DiscountCurve(multiCalibrator);

      var list = _data[index].Input;
      var count = list.FindChain(_curves[0].ReferenceIndex);

      foreach (var swap in list)
      {
        discountCurve2.Add(swap, 0.0);
      }
      discountCurve2.Fit();

      var df2 = discountCurve2.DiscountFactor(_asOf, _maturity);
      Assert.AreEqual(df0, df2, 1e-11);
      Assert.AreEqual(df1, df2, 1e-11);
    }

    #region Helpers

    private double CalculateEquation(IList<Swap> list, int count,
      Dt settle, DiscountCurve discountCurve)
    {
      var cal = new DiscountCurveFitCalibrator(_asOf);
      var payments = list.GetSwapChainPayments(count,
        discountCurve.ReferenceIndex, discountCurve,
        null, cal.CurveFitSettings);
      var lhsValue = Pv(payments[0], settle, discountCurve);
      var rhsValue = Pv(payments[1], settle, discountCurve);
      return lhsValue - rhsValue;
    }

    private static double Pv(List<Payment> list,
      Dt settle, DiscountCurve discountCurve)
    {
      return CashflowCalibrator.Pv(settle, null, list.ToArray(), discountCurve);
    }

    private static double GetParCoupon(SwapPricer pricer)
    {
      var coupon = pricer.ReceiverSwapPricer.SwapLeg.Coupon;
      if (coupon.AlmostEquals(0.0))
        return pricer.PayerSwapPricer.SwapLeg.Coupon;
      return coupon;
    }

    private SwapPricer GetSwapPricer(string recLegTenor,
      string payLegTenor, bool spreadOnPayleg = false)
    {
      var receiver = GetSwapLegPricer(recLegTenor, payLegTenor);
      var payer = GetSwapLegPricer(payLegTenor, recLegTenor);
      if (spreadOnPayleg)
        payer.SwapLeg.Coupon = 1E-15;
      else
        receiver.SwapLeg.Coupon = 1E-15;
      var pricer = CreateSwapPricer(receiver, payer);
      var parCoupon = pricer.ParCoupon();
      if (spreadOnPayleg)
        payer.SwapLeg.Coupon = parCoupon;
      else
        receiver.SwapLeg.Coupon = parCoupon;
      pricer.Reset();
      Assert.AreEqual(0.0, pricer.Pv(), 1E-15);
      return pricer;
    }

    private SwapLegPricer GetSwapLegPricer(
      string thisLegTenor, string otherLegTenor)
    {
      if (String.IsNullOrEmpty(thisLegTenor))
        return CreateSwapLegPricer(otherLegTenor, isFixedLeg: true);
      if (String.Compare(thisLegTenor, "1D",
        StringComparison.OrdinalIgnoreCase) == 0)
      {
        return CreateSwapLegPricer("1D", otherLegTenor);
      }
      return CreateSwapLegPricer(thisLegTenor);
    }

    private SwapLegPricer CreateSwapLegPricer(
      string indexTenor,
      string frequency = null,
      double coupon = 0,
      bool isFixedLeg = false)
    {
      var discountCurve = _curves[0];
      var tenor = Tenor.Parse(indexTenor);
      var projectCurve = _curves.First(c => c.ReferenceIndex.IndexTenor.Equals(tenor));
      var index = projectCurve.ReferenceIndex;
      if (frequency == null) frequency = indexTenor;
      var freq = Tenor.Parse(frequency).ToFrequency();
      var swpLeg = isFixedLeg
        ? new SwapLeg(_settle, _maturity, index.Currency, coupon,
          index.DayCount, freq, index.Roll, index.Calendar, false)
        {
          Description = "Fixed"
        }
        : new SwapLeg(_settle, _maturity, freq, coupon, index)
        {
          Description = indexTenor
        };
      return new SwapLegPricer(swpLeg, _asOf, _settle, 1.0, discountCurve,
        index, isFixedLeg ? null : projectCurve, new RateResets(), null, null);
    }

    private static SwapPricer CreateSwapPricer(
      SwapLegPricer receiver, SwapLegPricer payer)
    {
      receiver.Notional = 1;
      payer.Notional = -1;
      var p = new SwapPricer(receiver, payer);
      p.Swap.Description = receiver.SwapLeg.Description
        + " vs " + payer.SwapLeg.Description;
      return p;
    }

    #endregion
  }
}
