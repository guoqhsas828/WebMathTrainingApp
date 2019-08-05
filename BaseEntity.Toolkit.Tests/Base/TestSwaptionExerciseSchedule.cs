//
// Tests for the bermudan swaption exercise schedule
// Copyright (c) 2004-2008,   . All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class TestSwaptionExerciseSchedule : ToolkitTestBase
  {

    [OneTimeSetUp]
    public void Initialize()
    {
      const int periods = 5;
      _startDates = new List<Dt>();
      _endDates = new List<Dt>();
      _ntfds = new List<Dt>();
      _swapStartDates = new List<Dt>();
      var begin = Dt.Add(PricingDt, 2, TimeUnit.Years);
      for (int i = 0; i < periods; i++)
      {
        var date = Dt.Add(begin, Tenor.Parse("3M"));
        _startDates.Add(date);
        _endDates.Add(date);
        _ntfds.Add(date);
        _swapStartDates.Add(date);
        begin = date;
      }
    }

    [Test]
    public void TestBermudanSwpnNtficationExerciseSchedule()
    {
      var optionEffective = PricingDt;
      var swapEffective = Dt.Add(PricingDt, 1, TimeUnit.Years);
      var swapMaturity = Dt.Add(swapEffective, 5, TimeUnit.Years);

      var swpn = GetSwaption(swapEffective, swapMaturity, optionEffective, OptionStyle.Bermudan);

      var amounts = Enumerable.Repeat(0.0, _startDates.Count).ToArray();
      var periodsA = GetExercisePeriods(swpn.OptionType, swpn.Style,
        _startDates.ToArray(), _endDates.ToArray(), amounts, !UseNtfd);
      var periodsB= GetExercisePeriods(swpn.OptionType, swpn.Style,
        _ntfds.ToArray(), _swapStartDates.ToArray(), amounts, UseNtfd);

      var disCurve = new DiscountCurve(PricingDt, 0.02);
      var refCurve = new DiscountCurve(PricingDt, 0.03);

      var pricerA = GetSwaptionPricer(PricingDt, swpn, disCurve, refCurve, periodsA);
      var pricerB = GetSwaptionPricer(PricingDt, swpn, disCurve, refCurve, periodsB);

      var pvA = pricerA.Pv();
      var pvB = pricerB.Pv();

      Assert.AreEqual(pvA, pvB, 1E-14);
    }


    private static IList<IOptionPeriod> GetExercisePeriods(
      OptionType oType, OptionStyle oStyle, Dt[] dates1,
      Dt[] dates2, double[] amounts, bool useNtfd)
    {
      if (dates1 == null || dates2 == null)
        return null;

      if(dates1.Length != dates2.Length)
        throw new ArgumentException("The lengths of start dates and end dates not match");

      var periods = new UniqueSequence<IOptionPeriod>();
      for (int i = 0; i < dates1.Length; i++)
      {
        Dt date1 = dates1[i], date2 = dates2[i];
        var exercisePeriod = (!useNtfd)
          ? new[]
          {
            oType == OptionType.Call
              ? (IOptionPeriod) new CallPeriod(date1, date2, amounts[i],
                0, oStyle, 0)
              : new PutPeriod(date1, date2, amounts[i], oStyle)
          }
          : new[]
          {
            oType == OptionType.Call
              ? (IOptionPeriod) new CallPeriod(date1, amounts[i],
                0, oStyle, 0, date2)
              : new PutPeriod(date1, amounts[i], oStyle, date2)
          };
        periods.Add(exercisePeriod);
      }
      return periods;
    }

    private static SwapBermudanBgmTreePricer GetSwaptionPricer(
      Dt asOf, Swaption swpn, DiscountCurve disCurve, DiscountCurve refCurve,
      IList<IOptionPeriod> exercisePeriods)
    {
      var vol = new FlatVolatility
      {
        DistributionType = DistributionType.LogNormal,
        Volatility = 0.4
      };

      return new SwapBermudanBgmTreePricer(swpn, asOf, asOf, disCurve, refCurve,
        null, exercisePeriods, vol);
    }

    private static Swaption GetSwaption(Dt swapEffective, Dt swapMaturity, 
      Dt optionEffective, OptionStyle optionStyle)
    {
      var index = StandardReferenceIndices.Create("USDLIBOR_3M");
      var floatFreq = index.IndexTenor.ToFrequency();
      var bdc = index.Roll;
      var calendar = index.Calendar;
      var ccy = index.Currency;
      const DayCount fixDc = DayCount.Actual360;
      const Frequency fixFreq = Frequency.SemiAnnual;
      const double fixCpn = 0.029;
      const bool accrueOnCycle = false;

      var fixLeg = new SwapLeg(swapEffective, swapMaturity, ccy,
        fixCpn, fixDc, fixFreq, bdc, calendar, accrueOnCycle);
      var floatLeg = new SwapLeg(swapEffective, swapMaturity, 0.0, floatFreq,
        index, ProjectionType.SimpleProjection,
        CompoundingConvention.None, Frequency.None, false);

      return new Swaption(optionEffective, swapEffective, ccy, fixLeg,
        floatLeg, 0, PayerReceiver.Payer, optionStyle, fixCpn);
    }

    private static readonly Dt PricingDt = new Dt(20110609);
    private static List<Dt> _startDates, _endDates, _ntfds, _swapStartDates;
    private static bool UseNtfd = true;
  }
}
