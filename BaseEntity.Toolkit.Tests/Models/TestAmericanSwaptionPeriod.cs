

//
//    Test American swaption exercise periods
//

using System;
using System.Collections.Generic;
using System.Linq;
//
// Copyright (c)    2002-2018. All rights reserved.
//
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestAmericanSwaptionPeriod
  {

    [OneTimeSetUp]
    public void Initialize()
    {
      _projectCurve = new RateCurveBuilder().CreateRateCurves(_pricingDate) as DiscountCurve;
      _discountCurve = ((ProjectionCurveFitCalibrator)_projectCurve.Calibrator).DiscountCurve;
      _swapStartTimingFlags = new[] {SwapStartTiming.None, SwapStartTiming.Immediate, SwapStartTiming.NextPeriod};
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]//This case tests the default exercise schedule.
    public void TestAmericanSwaption(int testIndex)
    {
      var swapEffective = _data[testIndex].Effective;
      var swaptionExpiration = _data[testIndex].Expiry;
      var startDays = _data[testIndex].StartDays;
      var endDays = _data[testIndex].EndDays;

      if (startDays != null && endDays != null && startDays.Length != endDays.Length)
        throw new ArgumentException(string.Format(
          "The lengths of the start days and the end days are not equal"));

      var startDayArray = startDays == null ? null : startDays.Select(d => new Dt(d)).ToArray();
      var endDayArray = endDays == null ? null : endDays.Select(d => new Dt(d)).ToArray();

      var vol = new FlatVolatility
      {
        DistributionType = DistributionType.LogNormal,
        Volatility = _volatility
      };

      //Test different SwapStartTiming flags
      foreach (var flag in _swapStartTimingFlags)  
      {
        var swap = CreateSwap(swapEffective, _maturity, flag, OptionStyle.None);

        var swaptionA = CreateSwaption(_pricingDate, swaptionExpiration, swap, OptionStyle.American);  
        var swaptionB = CreateSwaption(_pricingDate, swaptionExpiration, swap, OptionStyle.Bermudan);  

        var exercisePeriods = (startDays == null || endDays == null)
          ? null : CreatePeriods(startDayArray, endDayArray, swaptionA)
            .OrderBy(p => p.StartDate).MergeOverlapPeriods().ToList();

        //Test there is no overlap
        if (exercisePeriods != null)
        {
          var modPeriods = exercisePeriods.ToArray();
          for (var i = 1; i < modPeriods.Length; ++i)
          {
            Assert.AreEqual(true, Dt.Cmp(modPeriods[i].StartDate, modPeriods[i - 1].EndDate) >= 0);
          }
        }

        var pricerA = new SwapBermudanBgmTreePricer(swaptionA, _pricingDate, _pricingDate,
          _discountCurve, _projectCurve, null, exercisePeriods, vol);

        var pricerB = new SwapBermudanBgmTreePricer(swaptionB, _pricingDate, _pricingDate,
          _discountCurve, _projectCurve, null, exercisePeriods, vol);  

        var pvA = pricerA.ProductPv();
        var pvB = pricerB.ProductPv();

        if (swap.OptionTiming == SwapStartTiming.NextPeriod)
          Assert.AreEqual(pvB, pvA);  //In this situation, the swaptionA is treated as Bermudan.
        else
          Assert.Less(pvB, pvA);
      }
    }



    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    public void TestSwapProduceSwaption(int testIndex)
    {
      var swapEffective = _data[testIndex].Effective;
      var startDays = _data[testIndex].StartDays;
      var endDays = _data[testIndex].EndDays;

      if (startDays != null && endDays != null && startDays.Length != endDays.Length)
        throw new ArgumentException(string.Format(
          "The lengths of the start days and the end days are not equal"));

      var startDayArray = startDays == null ? null : startDays.Select(d => new Dt(d)).ToArray();
      var endDayArray = endDays == null ? null : endDays.Select(d => new Dt(d)).ToArray();

      var vol = new FlatVolatility
      {
        DistributionType = DistributionType.LogNormal,
        Volatility = _volatility
      };

      //Test different SwapStartTiming flags
      foreach (var flag in _swapStartTimingFlags)
      {
        var swapA = CreateSwap(swapEffective, _maturity, flag, OptionStyle.American);
        swapA.ExerciseSchedule = CreateSwapPeriods(startDayArray, endDayArray, swapA)
          .OrderBy(p => p.StartDate).MergeOverlapPeriods().ToList();

        var swapB = CreateSwap(swapEffective, _maturity, flag, OptionStyle.Bermudan);
        swapB.ExerciseSchedule = swapA.ExerciseSchedule;

        var pricerA = new SwapBermudanBgmTreePricer(swapA, _pricingDate, _pricingDate,
          _discountCurve, _projectCurve, null, vol);  //create swaption pricer using swap

        var pricerB = new SwapBermudanBgmTreePricer(swapB, _pricingDate, _pricingDate,
          _discountCurve, _projectCurve, null, vol);  //create swaption pricer using swap

        var pvAswap = pricerA.ProductPv();
        var pvBswap = pricerB.ProductPv();

        if (swapA.OptionTiming == SwapStartTiming.NextPeriod)
          Assert.AreEqual(pvBswap, pvAswap); //In this situation, the swaption is Bermudan.
        else
          Assert.Less(pvBswap, pvAswap);
      }
    }


    #region Util Methods

    private static IEnumerable<IOptionPeriod> CreatePeriods(IList<Dt> startDays,
      IList<Dt> endDays, Swaption swpn)
    {
      if (startDays == null || endDays == null)
        return null;

      var exercisePeriods = new UniqueSequence<IOptionPeriod>();
      for (int i = 0; i < startDays.Count; i++)
      {
        Dt start = startDays[i], end = endDays[i];
        var exercisePeriod = new[]
        {
          swpn.OptionType == OptionType.Call
            ? (IOptionPeriod) new CallPeriod(start, end, 1.0,
              0, swpn.Style, 0)
            : new PutPeriod(start, end, 1.0, swpn.Style)
        };
        exercisePeriods.Add(exercisePeriod);
      }
      return exercisePeriods;
    }

    private static IEnumerable<IOptionPeriod> CreateSwapPeriods(IList<Dt> startDays,
      IList<Dt> endDays, Swap swap)
    {
      var exercisePeriods = new UniqueSequence<IOptionPeriod>();
      for (int i = 0; i < startDays.Count; i++)
      {
        Dt start = startDays[i], end = endDays[i];
        var exercisePeriod = new[]
        {
          new PutPeriod(start, end, 1.0, swap.OptionStyle) as IOptionPeriod
        };
        exercisePeriods.Add(exercisePeriod);
      }
      return exercisePeriods;
    }

    private Swap CreateSwap(Dt effective, Dt maturity, SwapStartTiming swapStartTiming, OptionStyle optionStyle)
    {
      const DayCount fixDc = DayCount.Actual360;
      const Frequency fixFr = Frequency.SemiAnnual, floatFr = Frequency.Quarterly;
      const double fixCpn = 0.014;
      const bool accrueOnCycle = false;
      var bdc = BDConvention.Modified;
      var calendar = Calendar.NYB;
      var ccy = Currency.USD;

      var fixLeg = new SwapLeg(effective, maturity, ccy,
        fixCpn, fixDc, fixFr, bdc, calendar, accrueOnCycle);
      var floatLeg = new SwapLeg(effective, maturity, 0.0, floatFr,
        GetIndex("USDLIBOR_3M"), ProjectionType.SimpleProjection,
        CompoundingConvention.None, Frequency.None, false);

      return new Swap(floatLeg, fixLeg){OptionTiming = swapStartTiming, OptionStyle = optionStyle};
    }


    private Swaption CreateSwaption(Dt effective, Dt expiry, Swap swap, OptionStyle optionStyle)
    {
      const double fixCpn = 0.014;
      var ccy = Currency.USD;

      var fixLeg = swap.PayerLeg;
      var floatLeg = swap.ReceiverLeg;

      return new Swaption(effective, expiry, ccy, fixLeg, floatLeg,
        2, PayerReceiver.Payer, optionStyle, fixCpn)
      {
        SwapStartTiming = swap.OptionTiming
      };
    }

    private static ReferenceIndex GetIndex(string indexName)
    {
      switch (indexName)
      {
        case "USDFEDFUNDS_1D":
          return new InterestRateIndex("USDFEDFUNDS_1D", Frequency.Daily,
            Currency.USD, DayCount.Actual360, Calendar.NYB, 2);
        case "USDLIBOR_3M":
          return new InterestRateIndex("USDLIBOR_3M", Frequency.Quarterly,
            Currency.USD, DayCount.Actual360, Calendar.NYB, 2);
        case "USDLIBOR_6M":
          return new InterestRateIndex("USDLIBOR_6M", Frequency.SemiAnnual,
            Currency.USD, DayCount.Actual360, Calendar.NYB, 2);
        default:
          throw new ArgumentException(
            string.Format("{0} index cannot be created here, try other ways", indexName));
      }
    }

    #endregion Util methods

    #region Data

    

    private static Data[] _data =
    {
      new Data  //0
      { 
        Effective =new Dt(20120613),
        Expiry = new Dt(20181214),
        StartDays = new []{20130614},
        EndDays = new []{20181214},
      },
      new Data //less than one period //1
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20141214),
        StartDays = new []{20130614},
        EndDays = new []{20130714},
      },
       new Data  //2
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20140611),
        StartDays = new []{20130614, 20131209},
        EndDays = new []{20131211, 20140611},
      },
       new Data  //two disjoint //3
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20140613),
        StartDays = new []{20130614, 20151214},
        EndDays = new []{20131211, 20170610},
      },
      new Data  //first two connect, last disjoint //4
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20171214),
        StartDays = new []{20130614, 20140711, 20151215},
        EndDays = new []{20140310, 20141222, 20160718},
      },
       new Data  //all three disjoint //5
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20171215),
        StartDays = new []{20130314, 20140711, 20151215},
        EndDays = new []{20131010, 20141222, 20160718},
      },
      new Data  //first disjoint, last two connect //6
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20171216),
        StartDays = new []{20130314, 20140711, 20141223},
        EndDays = new []{20131010, 20141222, 20150718},
      },
      new Data  //first disjoint, middle two connect, last disjoint //7
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20181216),
        StartDays = new []{20130314, 20140711, 20141223, 20160719},
        EndDays = new []{20131010, 20141222, 20150718, 20170312},
      },
      new Data  //first two connect, middle disjoint, last disjoint //8
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20181217),
        StartDays = new []{20130314, 20131111, 20141220, 20160719},
        EndDays = new []{20131010, 20140522, 20150718, 20170312},
      },
      new Data  //first two connect, last two disjoint //9
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20181218),
        StartDays = new []{20120914, 20131111, 20141220, 20160719},
        EndDays = new []{20131010, 20140522, 20150718, 20170312}
      },
      new Data  //first two connect, last two conncet, and some single points //10
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20181218),
        StartDays = new []{20120914, 20131111, 20141220, 20150719, 20160820,20160920,20161220},
        EndDays = new []{20131010, 20140522, 20150718, 20160312, 20160820,20160920,20161220}
      },
      new Data  //Default exercise schedule  //11
      {
        Effective =new Dt(20120613),
        Expiry = new Dt(20181218),
        StartDays = null,
        EndDays = null,
      }
    };

    private class Data
    {
      public Dt Effective;
      public Dt Expiry;
      public int[] StartDays;
      public int[] EndDays;
    }

    private readonly Dt _maturity = Dt.FromStr("11-Jun-19");  //underlying swap maturity day
    private readonly Dt _pricingDate = Dt.FromStr("9-Jun-2012"); //swaption effective day

    private readonly double _volatility = 0.4;
    private DiscountCurve _discountCurve;
    private DiscountCurve _projectCurve;
    private SwapStartTiming[] _swapStartTimingFlags;

    #endregion  Data
  }
}
