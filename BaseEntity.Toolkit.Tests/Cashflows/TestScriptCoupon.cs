//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Models.BGM.Native;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using RateSystem = BaseEntity.Toolkit.Models.BGM.RateSystem;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  public class TestScriptCoupon
  {
    public static Func<IPeriod, IRateCalculator, double>
      GetCouponFunction(InterestRateIndex rateIndex, Calendar calendar)
    {
      return (period, calc) =>
      {
        // Calculate the LIBOR rate reset at the begin of the period.
        // i.e., the coupon of swap leg 2
        var begin = Dt.Roll(period.StartDate, BDConvention.Modified, calendar);
        var rate2 = calc.GetRateAt(begin, rateIndex);

        // Calculate the LIBOR rate reset at the end of the period.
        var end = Dt.Roll(period.EndDate, BDConvention.Preceding, calendar);
        var rate1 = calc.GetRateAt(end, rateIndex);

        // The coupon of swap leg 1 with the additional provisions
        var coupon1 = rate1 > 0.06 ? (rate1 - 0.007225) : 0.029675;

        // The net coupon received by the counter party
        return rate2 - coupon1;
      };
    }

    private static Tuple<double[], double[]> CalculateRatesAndFractions(
      Dt asOf, Dt maurity,
      IList<SwaptionInfo> ctswpns)
    {
    // In the following, the normal volatility is scaled by
    // the fraction because in the tree building, the rate
    // is scaled in the same way.
      int count = ctswpns.Count;
      var rates = new double[count+1];
      var fractions = new double[count+1];
      var resetTimes = new double[count+1];

      double B = 1 - ctswpns[0].Level*ctswpns[0].Rate;
      int last = count - 1;
      double A = ctswpns[last].Level, 
        r = rates[count] = ctswpns[last].Rate,
        sA = r*A;
      fractions[count] = A/B;
      resetTimes[count] = (ctswpns[last].Date - asOf)/365.0;
      double Bn = B;
      for(int i = count; --i > 0;)
      {
        var swpn = ctswpns[i-1];
        resetTimes[i] = (swpn.Date - asOf)/365.0;
        double A0 = swpn.Level;
        if (A0 - A < 1E-12)
        {
          throw new ToolkitException(
            "Non-decreasing swaption annuity at date "
            + ctswpns[i].Date);
        }
        double sA0 = A0*swpn.Rate;
        B = Bn + sA;
        fractions[i] = (A0 - A)/B;
        rates[i] = (sA0 - sA)/(A0 - A);
        sA = sA0; A = A0;
      }
      B = Bn + sA;
      fractions[0] = resetTimes[1];
      rates[0] = (1/B - 1)/fractions[0];
      return Tuple.Create(rates, fractions);
    }

    private static Tuple<double[], double[]> CalculateRatesAndWeights(
      IList<SwaptionInfo> swpns, Dt maturity)
    {
      int n = swpns.Count, last = n - 1;
      var rates = new double[n];
      var wbs = new double[n];
      double a = 1.0;
      for (int i = 0; i < last; ++i)
      {
        wbs[i] = swpns[i].Level - swpns[i + 1].Level;
        rates[i] = (swpns[i].Rate*swpns[i].Level - 
          swpns[i + 1].Rate*swpns[i + 1].Level)/wbs[i];
        a *= 1 + (swpns[i + 1].Date - swpns[i].Date) / 365.0 * rates[i];
        wbs[i] *= a;
      }
      rates[last] = swpns[last].Rate;
      wbs[last] = swpns[last].Level;
      a *= 1 + (maturity - swpns[last].Date)/365.0*rates[last];
      wbs[last] *= a;

      double df0 = wbs[0]/(((last == 0 ? maturity : swpns[1].Date) - swpns[0].Date)/365.0);
      return Tuple.Create(rates, wbs);
    }


    private static Func<IRateSystemDistributions, int, int, double>
      GetCouponCalculator(Func<IPeriod, IRateCalculator, double> fn)
    {
      return (rsd, date, state) =>
      {
        var period = new Period(rsd.NodeDates[date], rsd.TenorDates[date]);
        var calc = new TreeNodeRateCalculator
        {
          ProjectionCurve = rsd.GetDiscountCurve(date, state).Curve
        };
        var cpn = fn(period, calc) + 0.017225;
        return cpn;
      };
    }

    //[Test]
    public void Test()
    {
      var euribor3M = (InterestRateIndex)StandardReferenceIndices.Create("EURIBOR_3M");
      var fn = GetCouponFunction(euribor3M, Calendar.TGT);
      var pricer = (SwapBermudanBgmTreePricer)GetTestFilePath(
        "data/Swap2556737.bin").LoadBinary();
      pricer.Swap.PayerLeg.CouponFunction = fn;
      //pricer.Swap.OptionRight = OptionRight.RightToEnter;
      var pv0 = pricer.ProductPv();

      var swpns = pricer.BuildCoTerminalSwaptions(false).Item1;

      Dt asOf = pricer.AsOf, maturity = pricer.Swap.Maturity;
      var ratesAndWeights = CalculateRatesAndWeights(swpns, maturity);
      var rfs = CalculateRatesAndFractions(asOf, maturity, swpns);

      var tree = new RateSystem();
      BaseEntity.Toolkit.Models.BGM.Native.BgmBinomialTree.calibrateCoTerminalSwaptions(
        asOf, maturity, swpns, 1E-8, 0, tree);
      tree.AsOf = asOf;
      tree.TenorDates = swpns.Select(s => s.Date).Append(maturity).ToArray();
      tree.NodeDates = Enumerable.Repeat(asOf, 1)
        .Concat(swpns.Select(s => s.Date)).ToArray();


      
      var pv = tree.EvaluateBermudan(tree.NodeDates.Select(d => NodeDateKind.Exercisable).ToList(),
        GetCouponCalculator(fn), null);

      //var pv = pricer.ProductPv();
      return;



    }
  }



}
