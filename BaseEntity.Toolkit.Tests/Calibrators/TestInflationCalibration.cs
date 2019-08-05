// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestInflationCalibration : ToolkitTestBase
  {
    private readonly Dt asOf_;
    private readonly double asOfInfl_;
    private readonly DiscountCurve disc_;
    private readonly InflationIndex fitinflIndex_;
    private readonly double[] mktQuotes_;
    private readonly IList<RateReset> resetList_;
    private readonly Dt settle_;
    private readonly CalibratorSettings bootstrapSettings_;
    private readonly CalibratorSettings smoothSettings_;

    public TestInflationCalibration()
    {
      asOf_ = new Dt(24, 09, 2009);
      asOfInfl_ = 0.5;
      settle_ = Dt.Add(asOf_, 1);
      resetList_ = new List<RateReset>();
      var oldDt = new Dt(1, 1, 2000);
      while (oldDt < settle_)
      {
        resetList_.Add(new RateReset(oldDt, asOfInfl_));
        oldDt = Dt.Add(oldDt, 1, TimeUnit.Months);
      }
      disc_ = new DiscountCurve(asOf_, 0.05);
      fitinflIndex_ = new InflationIndex("CPI", Currency.USD,
                                         DayCount.ActualActual, Calendar.None, BDConvention.None,
                                         Frequency.Monthly, Tenor.Empty);
      fitinflIndex_.HistoricalObservations = new RateResets(resetList_);

      //Calibration with inflation swaps and tips
      mktQuotes_ = new double[15];
      for (int i = 0; i < 5; i++)
        mktQuotes_[i] = 1.0 + i*0.05;
      for (int i = 5; i < 15; i++)
        mktQuotes_[i] = 0.04 + 0.005*i;
      var volCurve = new Curve(asOf_, 0.2);
      var alphaCurve = new Curve(asOf_, 0.2);
      var betCurve = new Curve(asOf_, 1.0);
      var rhoCurve = new Curve(asOf_, 0.3);
      var nuCurve = new Curve(asOf_, 0.2);
      IModelParameter[] ppar = new IModelParameter[] { alphaCurve, betCurve, nuCurve, rhoCurve };
      IModelParameter[] fpar = new IModelParameter[] { volCurve };
      RateModelParameters.Param[] ppN = new[]
                                          {
                                            RateModelParameters.Param.Alpha,
                                            RateModelParameters.Param.Beta,
                                            RateModelParameters.Param.Nu,
                                            RateModelParameters.Param.Rho
                                          };

      RateModelParameters.Param[] fpN = new[] { RateModelParameters.Param.Sigma };

      var fundingPar = new RateModelParameters(RateModelParameters.Model.BGM, fpN, fpar, new Tenor(Frequency.Quarterly), Currency.USD);
      var fwdparameters = new RateModelParameters(fundingPar, RateModelParameters.Model.SABR, ppN, ppar,
                                                  new Curve(asOf_, 0.75), Tenor.Empty);
      bootstrapSettings_ = new CalibratorSettings();
      bootstrapSettings_.InterpScheme = InterpScheme.FromInterp(new Linear(new Const(), new Const()));
      bootstrapSettings_.FwdModelParameters = null;//fwdparameters;
      bootstrapSettings_.Method = CashflowCalibrator.CurveFittingMethod.IterativeBootstrap;
      smoothSettings_ = new CalibratorSettings();
      smoothSettings_.InterpScheme = InterpScheme.FromInterp(new Linear(new Const(), new Const()));
      smoothSettings_.FwdModelParameters = fwdparameters;
      smoothSettings_.Method = CashflowCalibrator.CurveFittingMethod.SmoothForwards;
    }


    private void SetUpCurve(InflationCurve fitInflationCurve)
    {
      double baseInfl = asOfInfl_;

      var prods = new Product[15];
      var mat = new Dt[15];
      for (int i = 0; i < mat.Length; i++)
      {
        mat[i] = Dt.Add(settle_, 1 + i, TimeUnit.Years);
        if (i < 5)
        {
          var bond = new InflationBond(settle_, mat[i], Currency.USD, BondType.USGovt,
                                       0.05, DayCount.ActualActual, CycleRule.None, Frequency.Quarterly,
                                       BDConvention.Following,
                                       Calendar.NYB, fitinflIndex_, baseInfl, Tenor.Parse("2M"));
          bond.FlooredNotional = true;
          prods[i] = bond;
          fitInflationCurve.AddInflationBond(bond, mktQuotes_[i], QuotingConvention.FlatPrice, 0, null);
        }
        else if (i >= 5 && i < 10)
        {
          var fixedLeg = new SwapLeg(settle_, mat[i], Currency.USD, 0.05, DayCount.Thirty360,
                                     Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB, false);
          var floatLeg = new SwapLeg(settle_, mat[i], Currency.USD, 0, DayCount.Thirty360,
                                     Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB, false,
                                     new Tenor(Frequency.SemiAnnual), "CPI")
                           {ProjectionType = ProjectionType.InflationRate};
          floatLeg.ResetLag = Tenor.Parse("2m");
          floatLeg.InArrears = true;
          fixedLeg.Coupon = mktQuotes_[i];
          var swap = new Swap(floatLeg, fixedLeg);
          prods[i] = swap;
          fitInflationCurve.AddInflationSwap(swap, mktQuotes_[i]);
        }
        else
        {
          var fixedLeg = new SwapLeg(settle_, mat[i], Currency.USD, 0.05, DayCount.Thirty360,
                                     Frequency.None, BDConvention.Following, Calendar.NYB, false);
          var floatLeg = new SwapLeg(settle_, mat[i], Currency.USD, 0, DayCount.Thirty360,
                                     Frequency.None, BDConvention.Following, Calendar.NYB, false,
                                     new Tenor(Frequency.SemiAnnual), "CPI")
                           {ProjectionType = ProjectionType.InflationRate};
          fixedLeg.IsZeroCoupon = true;
          floatLeg.IsZeroCoupon = true;
          floatLeg.ResetLag = Tenor.Parse("2m");
          var swap = new Swap(floatLeg, fixedLeg);
          prods[i] = swap;
          fitInflationCurve.AddInflationSwap(swap, mktQuotes_[i]);
        }
      }
    }

    private void CheckRoundTrip(InflationCurve fitInflationCurve, double bondTolerance, double swapTolerance)
    {
      var iterBoostr = fitInflationCurve.Calibrator as InflationCurveFitCalibrator;
      var calibratedCurve = fitInflationCurve.TargetCurve;
      foreach (CurveTenor tenor in calibratedCurve.Tenors)
      {
        var pricer = iterBoostr.GetPricer(fitInflationCurve, tenor.Product);
        var ibond = pricer as InflationBondPricer;
        if (ibond != null)
        {
          double p = ibond.Pv()/ibond.DiscountCurve.Interpolate(ibond.AsOf, ibond.Settle);
          double e = tenor.OriginalQuote.Value*ibond.IndexRatio(ibond.Settle);
          Assert.AreEqual(0.0, p - e, bondTolerance);
        }
        var sp = pricer as SwapPricer;
        if (sp != null) 
        {
          if (sp.Swap.IsPayerZeroCoupon || sp.Swap.IsReceiverZeroCoupon)
          {
            Assert.AreEqual(0, sp.Pv(), swapTolerance);
          }
          else
          {
            Assert.AreEqual(0, sp.ParCoupon() - tenor.OriginalQuote.Value, swapTolerance);
          }
        }
      }
    }


    [Test]
    public void CalibrationTestBootstrap()
    {

      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_, bootstrapSettings_);
      var inflFactorCurve = new InflationFactorCurve(asOf_)
                            {
                              Calibrator = iterBoostr
                            };
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, inflFactorCurve, null);
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 5e-10, 2e-10);
    }

    [Test]
    public void CalibrationTestSmooth()
    {
      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_,
                                                       smoothSettings_);
      var inflFactorCurve = new InflationFactorCurve(asOf_)
                            {
                              Calibrator = iterBoostr
                            };
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, inflFactorCurve, null);
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 5e-4, 5e-4);
    }


    [Test]
    public void CalibrationTestWtSeasonalityBootstrap()
    {
      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_, bootstrapSettings_);
      var seasonalityIndex = new SeasonalityEffect(asOf_, asOf_, Dt.Add(asOf_, 30, TimeUnit.Years),
                                                  new[] {1.1, 1, 1, 1.2, 1, 1, 1, 1.1, 1.4, 1, 1, 1.1});
      var inflFactorCurve = new InflationFactorCurve(asOf_)
      {
        Calibrator = iterBoostr
      };
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, inflFactorCurve, seasonalityIndex.SeasonalityAdjustment());
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 2e-10, 2e-10);
    }

    [Test]
    public void CalibrationTestWtSeasonalitySmooth()
    {
      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_,
                                                       smoothSettings_);
      var seasonalityIndex = new SeasonalityEffect(asOf_, asOf_, Dt.Add(asOf_, 30, TimeUnit.Years),
                                                  new[] { 1.1, 1, 1, 1.2, 1, 1, 1, 1.1, 1.4, 1, 1, 1.1 });
      var inflFactorCurve = new InflationFactorCurve(asOf_)
      {
        Calibrator = iterBoostr
      };
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, inflFactorCurve, seasonalityIndex.SeasonalityAdjustment());
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 5e-4, 5e-4);
    }

    [Test]
    public void CalibrationRealYieldTestBootstrap()
    {

      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_, bootstrapSettings_);
      var realDisc = new DiscountCurve(iterBoostr);
      realDisc.Interp = disc_.Interp;
      realDisc.DayCount = disc_.DayCount;
      realDisc.Frequency = disc_.Frequency;
      realDisc.Calibrator = iterBoostr;
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, disc_, realDisc, null);
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 5e-10, 2e-10);

    }

    [Test]
    public void CalibrationRealYieldTestSmooth()
    {
      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_, smoothSettings_);
      var realDisc = new DiscountCurve(iterBoostr);
      realDisc.Interp = disc_.Interp;
      realDisc.DayCount = disc_.DayCount;
      realDisc.Frequency = disc_.Frequency;
      realDisc.Calibrator = iterBoostr;
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, disc_, realDisc, null);
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 5e-4, 5e-4);
    }


    [Test]
    public void CalibrationRealYieldTestWtSeasonalityBootstrap()
    {
      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_, bootstrapSettings_);
      var seasonalityIndex = new SeasonalityEffect(asOf_, asOf_, Dt.Add(asOf_, 30, TimeUnit.Years),
                                                  new[] { 1.1, 1, 1, 1.2, 1, 1, 1, 1.1, 1.4, 1, 1, 1.1 });
      var realDisc = new DiscountCurve(iterBoostr);
      realDisc.Interp = disc_.Interp;
      realDisc.DayCount = disc_.DayCount;
      realDisc.Frequency = disc_.Frequency;
      realDisc.Calibrator = iterBoostr;
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, disc_, realDisc, seasonalityIndex.SeasonalityAdjustment());
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 2e-10, 2e-10);
    }

    [Test]
    public void CalibrationRealYieldTestWtSeasonalitySmooth()
    {
      var iterBoostr = new InflationCurveFitCalibrator(asOf_, settle_, disc_, fitinflIndex_, smoothSettings_);
      var seasonalityIndex = new SeasonalityEffect(asOf_, asOf_, Dt.Add(asOf_, 30, TimeUnit.Years),
                                                  new[] { 1.1, 1, 1, 1.2, 1, 1, 1, 1.1, 1.4, 1, 1, 1.1 });
      var realDisc = new DiscountCurve(iterBoostr);
      realDisc.Interp = disc_.Interp;
      realDisc.DayCount = disc_.DayCount;
      realDisc.Frequency = disc_.Frequency;
      realDisc.Calibrator = iterBoostr;
      var fitInflationCurve = new InflationCurve(asOf_, asOfInfl_, disc_, realDisc, seasonalityIndex.SeasonalityAdjustment());
      SetUpCurve(fitInflationCurve);
      fitInflationCurve.Fit();
      CheckRoundTrip(fitInflationCurve, 5e-4, 5e-4);
    }
  }
}