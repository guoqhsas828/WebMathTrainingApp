//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Base.ReferenceIndices;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestLiabilityCashflows
  {
    [Test]
    public void NominalFlowsVsZeroCouponSwapCalc()
    {
      Currency ccy = Currency.GBP;
      Dt asOf = new Dt(1, 6, 2015);
      var discountCurve = new DiscountCurve(asOf, 0.005);
      discountCurve.Ccy = ccy;
      
      double[] amounts = new double[] { 91968.61, 110386.52, 132205.37, 157841.67, 187694.57, 222108.74, 261394.09, 291843.24, 323675.18, 356434.56 };
      Dt[] payDates = Dt.FromStr(new string[] { "01-Jan-2016",  "01-Jan-2017", "01-Jan-2018", "01-Jan-2019", "01-Jan-2020", "01-Jan-2021", 
                                                "01-Jan-2022", "01-Jan-2023", "01-Jan-2024", "01-Jan-2025" }, 
                                 "%d-%b-%Y");

      // create nominal flows
      var nominalFlows = new NominalCashflows(ccy, amounts, payDates, Toolkit.Base.BDConvention.Modified, Calendar.LNB, "Nominal Flows");
      var nominalFlowsPricer = new NominalCashflowsPricer(nominalFlows, asOf, asOf, discountCurve);

      // create equivalent swap legs
      double sum = 0.0;
      for (int i=0;i<payDates.Length; i++)
      {
        SwapLeg swapLeg = new SwapLeg(asOf, payDates[i], ccy, 0.0, DayCount.OneOne, Frequency.None, BDConvention.Modified, Calendar.LNB, false);
        swapLeg.Notional = 1.0;
        swapLeg.IsZeroCoupon = true;
        swapLeg.FinalExchange = true;

        SwapLegPricer swapPricer = new SwapLegPricer(swapLeg, asOf, asOf, amounts[i], discountCurve, null, null, null, null, null);
        sum += swapPricer.Pv();
      }

      AssertEqual("Nominal Flows Pv", sum, nominalFlowsPricer.Pv(), 1e-8);
    }

    [Test]
    public void NominalFlowsVsDiscountedFlowsCalc()
    {
      Currency ccy = Currency.GBP;
      Dt asOf = new Dt(1, 6, 2015);
      var discountCurve = new DiscountCurve(asOf, 0.005);
      discountCurve.Ccy = ccy;

      double[] amounts = new double[] { 91968.61, 110386.52, 132205.37, 157841.67, 187694.57, 222108.74, 261394.09, 291843.24, 323675.18, 356434.56 };
      Dt[] payDates = Dt.FromStr(new string[] { "01-Jan-2016",  "01-Jan-2017", "01-Jan-2018", "01-Jan-2019", "01-Jan-2020", "01-Jan-2021", 
                                                "01-Jan-2022", "01-Jan-2023", "01-Jan-2024", "01-Jan-2025" },
                                 "%d-%b-%Y");

      // create nominal flows
      var nominalFlows = new NominalCashflows(ccy, amounts, payDates, BDConvention.Modified, Calendar.LNB, "Nominal Flows");
      var nominalFlowsPricer = new NominalCashflowsPricer(nominalFlows, asOf, asOf, discountCurve);

      // create equivalent swap legs
      double sum = 0.0;
      for (int i = 0; i < payDates.Length; i++)
      {
        sum += amounts[i] * discountCurve.Interpolate(Dt.Roll(payDates[i], BDConvention.Modified, Calendar.LNB));
      }

      AssertEqual("Nominal Flows Pv", sum, nominalFlowsPricer.Pv(), 1e-8);
    }

    [Test]
    public void InflationFlowsVsZeroCouponSwapCalc()
    {
      Currency ccy = Currency.GBP;
      Dt asOf = new Dt(1, 6, 2015);
      Dt effectiveDate = new Dt(1, 1, 2015);
      var discountCurve = new DiscountCurve(asOf, 0.005);
      discountCurve.Ccy = ccy;

      double[] amounts = new double[] { 91968.61, 110386.52, 132205.37, 157841.67, 187694.57, 222108.74, 261394.09, 291843.24, 323675.18, 356434.56 };
      Dt[] payDates = Dt.FromStr(new string[] { "01-Jan-2016",  "01-Jan-2017", "01-Jan-2018", "01-Jan-2019", "01-Jan-2020", "01-Jan-2021", 
                                                "01-Jan-2022", "01-Jan-2023", "01-Jan-2024", "01-Jan-2025" },
                                 "%d-%b-%Y");

      InflationCurve inflationCurve = GetInflationCurve(asOf);
      InflationIndex inflationIndex = new InflationIndex("RPIGBP_INDEX", ccy, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, Frequency.Monthly, Tenor.Empty);
      inflationIndex.HistoricalObservations = new RateResets(new List<RateReset>() { new RateReset(new Dt(1, 11, 2014), 90), new RateReset(new Dt(1, 1, 2015), 95) });

      IndexationMethod indexationMethod = IndexationMethod.UKGilt_OldStyle;
      Tenor resetLag = Tenor.TwoMonths;

      // create nominal flows
      var nominalFlows = new NominalCashflows(ccy, amounts, payDates, Toolkit.Base.BDConvention.Modified, Calendar.LNB, "Nominal Flows");
      var nominalFlowsPricer = new NominalCashflowsPricer(nominalFlows, asOf, asOf, discountCurve);

      // create equivalent swap legs
      double sum = 0.0;
      for (int i = 0; i < payDates.Length; i++)
      {
        InflationSwapLeg swapLeg = new InflationSwapLeg(effectiveDate, payDates[i], ccy, 0.0, DayCount.OneOne, Frequency.None, BDConvention.Modified, Calendar.LNB, false);
        swapLeg.Notional = 1.0;
        swapLeg.IsZeroCoupon = true;
        swapLeg.FinalExchange = true;
        swapLeg.IndexationMethod = indexationMethod;
        swapLeg.ResetLag = resetLag;

        SwapLegPricer swapPricer = new SwapLegPricer(swapLeg, asOf, asOf, amounts[i], discountCurve, (ReferenceIndex)inflationIndex, (CalibratedCurve)inflationCurve, null, null, null);
        sum += swapPricer.Pv();
      }

      AssertEqual("Nominal Flows Pv", sum, nominalFlowsPricer.Pv(), 1e-8);
    }

    [Test]
    public void InflationFlowsVsDiscountedFlowsCalc()
    {
      Currency ccy = Currency.GBP;
      Dt asOf = new Dt(1, 6, 2015);
      Dt effectiveDate = new Dt(1, 1, 2015);
      var discountCurve = new DiscountCurve(asOf, 0.005);
      discountCurve.Ccy = ccy;

      InflationCurve inflationCurve = GetInflationCurve(asOf);
      InflationIndex inflationIndex = new InflationIndex("RPIGBP_INDEX", ccy, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, Frequency.Monthly, Tenor.Empty);
      inflationIndex.HistoricalObservations =  new RateResets(new List<RateReset>() { new RateReset(new Dt(1, 11, 2014), 90), new RateReset(new Dt(1, 1, 2015), 95) } );

      IndexationMethod indexationMethod = IndexationMethod.UKGilt_OldStyle;
      Tenor resetLag = Tenor.TwoMonths;

      double[] amounts = new double[] { 91968.61, 110386.52, 132205.37, 157841.67, 187694.57, 222108.74, 261394.09, 291843.24, 323675.18, 356434.56 };
      Dt[] payDates = Dt.FromStr(new string[] { "01-Jan-2016",  "01-Jan-2017", "01-Jan-2018", "01-Jan-2019", "01-Jan-2020", "01-Jan-2021", 
                                                "01-Jan-2022", "01-Jan-2023", "01-Jan-2024", "01-Jan-2025" },
                                 "%d-%b-%Y");

      // create nominal flows
      var realFlows = new InflationCashflows(ccy, effectiveDate, amounts, payDates, BDConvention.Modified, Calendar.LNB, "Inflation Flows");
      var realFlowsPricer = new InflationCashflowsPricer(realFlows, asOf, asOf, discountCurve, inflationIndex, inflationCurve, indexationMethod, resetLag);

      InflationForwardCalculator rateProjector = new InflationForwardCalculator(asOf, inflationIndex, inflationCurve, indexationMethod)
      {
        DiscountCurve = discountCurve,
        ResetLag = resetLag,
      };

      var fixSched0 = rateProjector.GetFixingSchedule(Dt.Empty, effectiveDate, effectiveDate, effectiveDate);
      double i0 = rateProjector.Fixing(fixSched0).Forward;

      // create equivalent swap legs
      double sum = 0.0;
      for (int i = 0; i < payDates.Length; i++)
      {
        Dt payDate = Dt.Roll(payDates[i], BDConvention.Modified, Calendar.LNB);

        // Inflation growth
        var fixSched = rateProjector.GetFixingSchedule(Dt.Empty, payDate, payDate, payDate);
        double iT = rateProjector.Fixing(fixSched).Forward;
        double inflationGrowth = iT / i0;

        sum += amounts[i] * inflationGrowth * discountCurve.Interpolate(payDate);
      }

      AssertEqual("Nominal Flows Pv", sum, realFlowsPricer.Pv(), 1e-8);
    }

    [Test]
    public void InflationFlowsWithNoResetLagVsDiscountedFlowsCalc()
    {
      Currency ccy = Currency.GBP;
      Dt asOf = new Dt(1, 6, 2015);
      Dt effectiveDate = new Dt(1, 1, 2015);
      var discountCurve = new DiscountCurve(asOf, 0.005);
      discountCurve.Ccy = ccy;

      InflationCurve inflationCurve = GetInflationCurve(asOf);
      InflationIndex inflationIndex = new InflationIndex("RPIGBP_INDEX", ccy, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, Frequency.Monthly, Tenor.Empty);
      inflationIndex.HistoricalObservations = new RateResets(new List<RateReset>() { new RateReset(new Dt(1, 11, 2014), 90), new RateReset(new Dt(1, 1, 2015), 95) });

      IndexationMethod indexationMethod = IndexationMethod.UKGilt_OldStyle;
      Tenor resetLag = Tenor.Empty;

      double[] amounts = new double[] { 91968.61, 110386.52, 132205.37, 157841.67, 187694.57, 222108.74, 261394.09, 291843.24, 323675.18, 356434.56 };
      Dt[] payDates = Dt.FromStr(new string[] { "01-Jan-2016",  "01-Jan-2017", "01-Jan-2018", "01-Jan-2019", "01-Jan-2020", "01-Jan-2021", 
                                                "01-Jan-2022", "01-Jan-2023", "01-Jan-2024", "01-Jan-2025" },
                                 "%d-%b-%Y");

      // create nominal flows
      var realFlows = new InflationCashflows(ccy, effectiveDate, amounts, payDates, BDConvention.Modified, Calendar.LNB, "Inflation Flows");
      var realFlowsPricer = new InflationCashflowsPricer(realFlows, asOf, asOf, discountCurve, inflationIndex, inflationCurve, indexationMethod, resetLag);

      InflationForwardCalculator rateProjector = new InflationForwardCalculator(asOf, inflationIndex, inflationCurve, indexationMethod)
      {
        DiscountCurve = discountCurve,
        ResetLag = resetLag,
      };

      var fixSched0 = rateProjector.GetFixingSchedule(Dt.Empty, effectiveDate, effectiveDate, effectiveDate);
      double i0 = rateProjector.Fixing(fixSched0).Forward;

      // create equivalent swap legs
      double sum = 0.0;
      for (int i = 0; i < payDates.Length; i++)
      {
        Dt payDate = Dt.Roll(payDates[i], BDConvention.Modified, Calendar.LNB);

        // Inflation growth
        var fixSched = rateProjector.GetFixingSchedule(Dt.Empty, payDate, payDate, payDate);
        double iT = rateProjector.Fixing(fixSched).Forward;
        double inflationGrowth = iT / i0;

        sum += amounts[i] * inflationGrowth * discountCurve.Interpolate(payDate);
      }

      AssertEqual("Nominal Flows Pv", sum, realFlowsPricer.Pv(), 1e-8);
    }

    [Test]
    public void InflationFlowsWithFutureResetDateVsDiscountedFlowsCalc()
    {
      Currency ccy = Currency.GBP;
      Dt asOf = new Dt(1, 6, 2015);
      Dt effectiveDate = new Dt(1, 1, 2017);
      var discountCurve = new DiscountCurve(asOf, 0.005);
      discountCurve.Ccy = ccy;

      InflationCurve inflationCurve = GetInflationCurve(asOf);
      InflationIndex inflationIndex = new InflationIndex("RPIGBP_INDEX", ccy, DayCount.Actual365Fixed, Calendar.LNB, BDConvention.Modified, Frequency.Monthly, Tenor.Empty);

      IndexationMethod indexationMethod = IndexationMethod.UKGilt_OldStyle;
      Tenor resetLag = Tenor.TwoMonths;

      double[] amounts = new double[] { 110386.52, 132205.37, 157841.67, 187694.57, 222108.74, 261394.09, 291843.24, 323675.18, 356434.56 };
      Dt[] payDates = Dt.FromStr(new string[] {  "01-Jan-2017", "01-Jan-2018", "01-Jan-2019", "01-Jan-2020", "01-Jan-2021", 
                                                "01-Jan-2022", "01-Jan-2023", "01-Jan-2024", "01-Jan-2025" },
                                 "%d-%b-%Y");

      // create nominal flows
      var realFlows = new InflationCashflows(ccy, effectiveDate, amounts, payDates, BDConvention.Modified, Calendar.LNB, "Inflation Flows");
      var realFlowsPricer = new InflationCashflowsPricer(realFlows, asOf, asOf, discountCurve, inflationIndex, inflationCurve, indexationMethod, resetLag);

      InflationForwardCalculator rateProjector = new InflationForwardCalculator(asOf, inflationIndex, inflationCurve, indexationMethod)
      {
        DiscountCurve = discountCurve,
        ResetLag = resetLag,
      };

      var fixSched0 = rateProjector.GetFixingSchedule(Dt.Empty, effectiveDate, effectiveDate, effectiveDate);
      double i0 = rateProjector.Fixing(fixSched0).Forward;

      // create equivalent swap legs
      double sum = 0.0;
      for (int i = 0; i < payDates.Length; i++)
      {
        Dt payDate = Dt.Roll(payDates[i], BDConvention.Modified, Calendar.LNB);

        // Inflation growth
        var fixSched = rateProjector.GetFixingSchedule(Dt.Empty, payDate, payDate, payDate);
        double iT = rateProjector.Fixing(fixSched).Forward;
        double inflationGrowth = iT / i0;

        sum += amounts[i] * inflationGrowth * discountCurve.Interpolate(payDate);
      }

      AssertEqual("Nominal Flows Pv", sum, realFlowsPricer.Pv(), 1e-8);
    }

    public static InflationCurve GetInflationCurve(Dt asOf)
    {
      Curve seasonality = new Curve(asOf);
      Dt dt = asOf;
      seasonality.Add(asOf, 1.0);
      for (int i = 0; i < 100; ++i)
      {
        dt = Dt.Add(dt, Frequency.Quarterly, false);
        seasonality.Add(dt, 10 * Math.Sin(Dt.Years(asOf, dt, DayCount.Actual365Fixed)));
      }
      var tenors = new[] { "1y", "2y", "3y", "4y", "5y", "6y", "7y", "8y", "9y", "10y"};
      var infl = new[] { 110.0, 120.0, 125.0, 120.0, 130.0, 135.0, 120.0, 145.0, 148.0, 150.0};
      var inflationFactor = new InflationFactorCurve(asOf);
      for (int i = 0; i < infl.Length; ++i)
        inflationFactor.Add(Dt.Add(asOf, tenors[i]), infl[i] / 100.0);
      var retVal = new InflationCurve(asOf, 100, inflationFactor, seasonality);
      return retVal;
    }
  }
}
