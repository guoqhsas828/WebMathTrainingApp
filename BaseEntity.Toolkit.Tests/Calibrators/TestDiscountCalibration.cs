// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
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
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestDiscountCalibration : ToolkitTestBase
  {
    #region Data

    public Dt asOf_;
    public Dt[] bdates_;
    public string[] bnames_;
    public double[] quotes_;
    public double[] bquotes_;
    public double[] nquotes_;
    public Dt[] dates_;
    public Dt[] fraDates_;
    public string[] fraNames_;
    public double[] fraQuotes_;
    public double[] nFraQuotes_;
    public string[] names_;
    public CalibratorSettings bootstrapSettings_;
    public CalibratorSettings smoothSettings_;
    public CalibratorSettings svenssonSettings_;
    public CalibratorSettings nelsonSiegelSettings_;

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public TestDiscountCalibration()
    {
      asOf_ = new Dt(28, 1, 2010);


      dates_ = new Dt[25];
      dates_[0] = new Dt(26, 2, 2010);
      dates_[1] = new Dt(26, 3, 2010);
      dates_[2] = new Dt(26, 4, 2010);
      dates_[3] = new Dt(26, 7, 2010);
      dates_[4] = new Dt(26, 10, 2010);
      dates_[5] = new Dt(26, 1, 2011);
      dates_[6] = new Dt(17, 3, 2010);
      dates_[7] = new Dt(16, 6, 2010);
      dates_[8] = new Dt(15, 9, 2010);
      dates_[9] = new Dt(15, 12, 2010);
      dates_[10] = new Dt(16, 3, 2011);
      dates_[11] = new Dt(26, 1, 2012);
      dates_[12] = new Dt(26, 1, 2013);
      dates_[13] = new Dt(26, 1, 2014);
      dates_[14] = new Dt(26, 1, 2015);
      dates_[15] = new Dt(26, 1, 2016);
      dates_[16] = new Dt(26, 1, 2017);
      dates_[17] = new Dt(26, 1, 2018);
      dates_[18] = new Dt(26, 1, 2019);
      dates_[19] = new Dt(26, 1, 2020);
      dates_[20] = new Dt(26, 1, 2022);
      dates_[21] = new Dt(26, 1, 2025);
      dates_[22] = new Dt(26, 1, 2030);
      dates_[23] = new Dt(26, 1, 2035);
      dates_[24] = new Dt(26, 1, 2040);


      quotes_ = new[]
      {
        0.00231, 0.00239, 0.00249, 0.00390, 0.0063, 0.00875,
        0.99715, 0.99610, 0.99330, 0.98960, 0.9858,
        0.0126, 0.01731, 0.0245, 0.02659, 0.02977,
        0.03229, 0.03421, 0.03573, 0.03707, 0.03921,
        0.04142, 0.04295, 0.04361, 0.04398
      };

      nquotes_ = new[]
      {
        -0.00875, -0.0063, -0.0039, -0.00249, -0.00239, -0.00231, //MM
        0.99715, 0.99610, 0.99330, 0.98960, 0.9858, //Future
        0.0126, 0.01731, 0.0245, 0.02659, 0.02977,
        0.03229, 0.03421, 0.03573, 0.03707, 0.03921,
        0.04142, 0.04295, 0.04361, 0.04398 // Swap
      };

      nFraQuotes_ = new[] {-0.0135, -0.0125, -0.012};

      names_ = new[]
                 {
                   "1M",
                   "2M",
                   "3M",
                   "6M",
                   "9M",
                   "1Y",
                   "H0",
                   "M0",
                   "U0",
                   "Z0",
                   "H1",
                   "2Yr",
                   "3Yr",
                   "4Yr",
                   "5Yr",
                   "6Yr",
                   "7Yr",
                   "8Yr",
                   "9Yr",
                   "10Yr",
                   "12Yr",
                   "15Yr",
                   "20Yr",
                   "25Yr",
                   "30Yr"
                 };

      bdates_ = new Dt[14];
      bdates_[0] = new Dt(26, 1, 2012);
      bdates_[1] = new Dt(26, 1, 2013);
      bdates_[2] = new Dt(26, 1, 2014);
      bdates_[3] = new Dt(26, 1, 2015);
      bdates_[4] = new Dt(26, 1, 2016);
      bdates_[5] = new Dt(26, 1, 2017);
      bdates_[6] = new Dt(26, 1, 2018);
      bdates_[7] = new Dt(26, 1, 2019);
      bdates_[8] = new Dt(26, 1, 2020);
      bdates_[9] = new Dt(26, 1, 2022);
      bdates_[10] = new Dt(26, 1, 2025);
      bdates_[11] = new Dt(26, 1, 2030);
      bdates_[12] = new Dt(26, 1, 2035);
      bdates_[13] = new Dt(26, 1, 2040);

      bnames_ = new[]
                  {
                    "2Yr",
                    "3Yr",
                    "4Yr",
                    "5Yr",
                    "6Yr",
                    "7Yr",
                    "8Yr",
                    "9Yr",
                    "10Yr",
                    "12Yr",
                    "15Yr",
                    "20Yr",
                    "25Yr",
                    "30Yr"
                  };

      bquotes_ = new double[] {4, 6, 8, 10, 12, 14, 16, 18, 20, 24, 30, 40, 50, 60};

      fraDates_ = new Dt[3];
      fraDates_[0] = new Dt(28, 5, 2010);
      fraDates_[1] = new Dt(28, 7, 2010);
      fraDates_[2] = new Dt(28, 10, 2010);

      fraNames_ = new[] {"4 X 7", "6 X 9", "9 X 12"};
      fraQuotes_ = new[] {0.012, 0.0125, 0.0135};
      var volatility = new Curve(asOf_);
      volatility.Interp = InterpFactory.FromMethod(InterpMethod.Flat, ExtrapMethod.Const);
      volatility.Add(asOf_, 0.2);
      var pars = new RateModelParameters(RateModelParameters.Model.BGM,
                                         new[] {RateModelParameters.Param.Sigma}, new[] {volatility},
                                         new Tenor(Frequency.Quarterly), Currency);
      bootstrapSettings_ = new CalibratorSettings();
      bootstrapSettings_.FwdModelParameters = pars;
      bootstrapSettings_.InterpScheme = InterpScheme.FromInterp(new Weighted());
      smoothSettings_ = new CalibratorSettings();
      smoothSettings_.FwdModelParameters = pars;
      smoothSettings_.InterpScheme = InterpScheme.FromInterp(new Weighted());
      smoothSettings_.Method = CurveFitMethod.SmoothForwards;
      smoothSettings_.MarketWeight = 1.0;
      smoothSettings_.SlopeWeight = 0.0;
      svenssonSettings_ = new CalibratorSettings();
      svenssonSettings_.FwdModelParameters = pars;
      svenssonSettings_.Method = CurveFitMethod.Svensson;
      nelsonSiegelSettings_ = new CalibratorSettings();
      nelsonSiegelSettings_.FwdModelParameters = pars;
      nelsonSiegelSettings_.Method = CurveFitMethod.Svensson;
    }

    #endregion

    private void GetError(CalibratedCurve disc, double tol)
    {
      foreach (CurveTenor ten in disc.Tenors)
      {
        if (ten.Product is Note)
        {
          var pr =
            (NotePricer)
            disc.Calibrator.GetPricer(disc, ten.Product);
          Assert.AreEqual(1.0, pr.Pv(), tol);
        }
        else if (ten.Product is Swap)
        {
          var swap = (Swap)ten.Product;
          var sp =
            (SwapPricer)disc.Calibrator.GetPricer(disc, swap);
          Assert.AreEqual(0.0, Math.Abs(sp.Pv()), tol);
        }
        else if (ten.Product is StirFuture)
        {
          var fut = (StirFuturePricer)disc.Calibrator.GetPricer(disc, ten.Product);
          double a = 1.0 - fut.ModelRate();
          Assert.AreEqual(0.0, Math.Abs(a - ten.MarketPv), tol);
        }
        else if (ten.Product is FRA)
        {
          var fraPricer = (FRAPricer)disc.Calibrator.GetPricer(disc, ten.Product);
          double rate = fraPricer.ImpliedFraRate;
          Assert.AreEqual(0.0, Math.Abs(rate - ten.MarketPv), tol);
        }
      }
    }

    /// <summary>
    /// Test for simplified discount calibration interface: only check for exceptions
    /// </summary>
    [Test]
    public void DiscountCalibrationSimplified()
    {
      var types = new[]
                    {
                      "MM", "MM", "MM","MM", "MM","MM",
                      "FUT", "FUT","FUT","FUT","FUT",
                      "SWAP", "SWAP", "SWAP", "SWAP", "SWAP",
                      "SWAP", "SWAP", "SWAP", "SWAP", "SWAP",
                      "SWAP", "SWAP", "SWAP", "SWAP",
                      "BASIS", "BASIS", "BASIS","BASIS", "BASIS",
                      "BASIS", "BASIS", "BASIS", "BASIS", "BASIS",
                      "BASIS", "BASIS", "BASIS", "BASIS"
                    };

      int m = quotes_.Length;
      int k = bquotes_.Length;
      int n = m + k;
      var quotes = new double[n];
      var names = new string[n];

      for (int i = 0; i < n; i++)
      {
        quotes[i] = (i < m) ? quotes_[i] : bquotes_[i - m];
        names[i] = (i < m) ? names_[i] : bnames_[i - m];
      }

      CurveTerms prTerms = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      CurveTerms dsTerms = RateCurveTermsUtil.CreateDefaultCurveTerms("USDFUNDING_6M");
      var terms = dsTerms.Merge(names, prTerms, true);
      var settings = new CalibratorSettings();
      DiscountCurve disc = DiscountCurveFitCalibrator.DiscountCurveFit(asOf_, terms, "curve", quotes, types,
                                                                       names, settings);
      //Test to check that everything is constructed correctly. Pass => no exceptions. Goodness of fit is checked in other tests
    }

    /// <summary>
    /// Test for simplified discount calibration interface: only check for exceptions
    /// </summary>
    [Test]
    public void ProjectionCalibrationSimplified()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var types = new[]
                    {
                      "MM", "MM", "MM", "MM", "MM", "MM",
                      "FUT", "FUT", "FUT", "FUT", "FUT",
                      "SWAP", "SWAP", "SWAP", "SWAP", "SWAP",
                      "SWAP", "SWAP", "SWAP", "SWAP", "SWAP",
                      "SWAP", "SWAP", "SWAP", "SWAP"
                    };


      CurveTerms prTerms = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
      var settings = new CalibratorSettings();
      DiscountCurve proj = ProjectionCurveFitCalibrator.ProjectionCurveFit(asOf_, prTerms, disc, "curve", quotes_, types,
                                                                           names_, new PaymentSettings[quotes_.Length],
                                                                           settings);
      //Test to check that everything is constructed correctly. Pass => no exceptions. Goodness of fit is checked in other tests
    }

    /// <summary>
    /// Test of standard (discount and projection are one and the same) discount calibration   
    /// </summary>
    [Test]
    public void DiscountCalibrationBootstrap()
    {

      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, ri, bootstrapSettings_);
      var disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(), BDConvention.Following,
                       Calendar.NYB, ri, null);
      }
      disc.ResolveOverlap(new OverlapTreatment(true, false, false));
      disc.Fit();
      GetError(disc, 5e-9);   
    }


    /// <summary>
    /// Test of standard (discount and projection are one and the same) discount calibration 
    /// </summary>
    [Test]
    public void DiscountCalibrationSmooth()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, ri, smoothSettings_);
      var disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(), BDConvention.Following,
                       Calendar.NYB, ri, null);
      }
      disc.ResolveOverlap(new OverlapTreatment(true, false, false));
      disc.Fit();
      GetError(disc, 7e-6);
    }

   
    /// <summary>
    /// Test of standard (discount and projection are one and the same) discount calibration 
    /// </summary>
    [Test]
    public void DiscountCalibrationSvensson()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, ri, svenssonSettings_);
      ParametricCurveFn fn = new SvenssonFn(null);
      var disc = new DiscountCurve(asOf_, fn);
      disc.Calibrator = ibs0;
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(), BDConvention.Following,
                       Calendar.NYB, ri, null);
      }
      disc.ResolveOverlap(new OverlapTreatment(true, false, false));
      disc.Fit();
      double[] vals = new double[100];
      for (int i = 0; i < 100; i++)
        vals[i] = disc.Interpolate(Dt.Add(asOf_, 120*i));
        GetError(disc, 1e-2);
      
    }

    
    /// <summary>
    /// Test of standard (discount and projection are one and the same) discount calibration 
    /// </summary>
    [Test]
    public void DiscountCalibrationNelsonSiegel()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, ri, nelsonSiegelSettings_);
      ParametricCurveFn fn = new NelsonSiegelFn(null);
      var disc = new DiscountCurve(asOf_, fn);
      disc.Calibrator = ibs0;
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(), BDConvention.Following,
                       Calendar.NYB, ri, null);
      }
      disc.ResolveOverlap(new OverlapTreatment(true, false, false));
      disc.Fit();
      double[] vals = new double[100];
      for (int i = 0; i < 100; i++)
        vals[i] = disc.Interpolate(Dt.Add(asOf_, 120 * i));
      GetError(disc, 1e-2);
      
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionCalibrationBootstrap()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, null, bootstrapSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
          proj.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(),
                       BDConvention.Following, Calendar.NYB, ri, null);
      }
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 5e-9);
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionCalibrationSmooth()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, null, smoothSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
          proj.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(),
                       BDConvention.Following, Calendar.NYB, ri, null);
      }
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 7e-6);
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsBootstrap()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, new List<CalibratedCurve>{projb}, bootstrapSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(),
                     rip.IndexTenor.ToFrequency(), ri, rip, Calendar.None,
                     new PaymentSettings {SpreadOnReceiver = false});
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 5e-9);
      
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsSmooth()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, new List<CalibratedCurve>{projb}, smoothSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(),
                     rip.IndexTenor.ToFrequency(), ri, rip, Calendar.None,
                     new PaymentSettings {SpreadOnReceiver = false});
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 7e-5);
    }


    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsFFBootstrap()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");

      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, new List<CalibratedCurve>{projb}, bootstrapSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(),
                     rip.IndexTenor.ToFrequency(), ri, rip, Calendar.None,
                     new PaymentSettings
                       {SpreadOnReceiver = false, RecProjectionType = ProjectionType.ArithmeticAverageRate});
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 5e-9);
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsOISBootstrap()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, new List<CalibratedCurve>{projb}, bootstrapSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(),
                     rip.IndexTenor.ToFrequency(), ri, rip, Calendar.None,
                     new PaymentSettings
                       {SpreadOnReceiver = false, RecProjectionType = ProjectionType.GeometricAverageRate});
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 5e-9);
    }

    /// <summary>
    /// Test of projection calibration given a discount curve
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsCmpndBootstrap()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, rip, new List<CalibratedCurve>{projb}, bootstrapSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(), Frequency.Annual,
                     ri, rip, Calendar.None,
                     new PaymentSettings
                       {
                         RecCompoundingConvention = CompoundingConvention.FlatISDA,
                         PayCompoundingFreq = rip.IndexTenor.ToFrequency()
                       }
          );
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 5e-9);
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsCmpndSmooth()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, rip, new List<CalibratedCurve>{projb}, smoothSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(), Frequency.Annual,
                     ri, rip, Calendar.None,
                     new PaymentSettings
                       {
                         RecCompoundingConvention = CompoundingConvention.FlatISDA,
                         PayCompoundingFreq = rip.IndexTenor.ToFrequency()
                       }
          );
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 1e-3);
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsNBootstrap()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, new List<CalibratedCurve> {projb}, bootstrapSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], -bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(),
                     rip.IndexTenor.ToFrequency(), ri, rip, Calendar.None, null);
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 5e-9);
    }

    /// <summary>
    /// Test of projection calibration given a discount curve 
    /// </summary>
    [Test]
    public void ProjectionWtBasisSwapsNSmooth()
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      var projb = new DiscountCurve(asOf_, 0.035);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex rip = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, new List<CalibratedCurve>{projb}, smoothSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < 11; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        proj.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], -bquotes_[i]*1e-4, ri.IndexTenor.ToFrequency(),
                     rip.IndexTenor.ToFrequency(), ri, rip, Calendar.None, null);
      proj.ResolveOverlap(new OverlapTreatment(false, false, false));
      proj.Fit();
      GetError(proj, 7e-5);
    }

    
    /// <summary>
    /// Test of discount calibration for projection different than discounting 
    /// </summary>
    [Test]
    public void DiscountCalibrationWtBasisSwapsBootstrap()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex prime = new InterestRateIndex("Prime", new Tenor(6, TimeUnit.Months), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, prime, bootstrapSettings_);
      var disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
          continue;
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
        {
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(), BDConvention.Following, Calendar.NYB, ri, null);
        }
      }

      for (int i = 0; i < bquotes_.Length; i++)
        disc.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, prime.IndexTenor.ToFrequency(),
                     ri.IndexTenor.ToFrequency(), prime, ri, Calendar.None, new PaymentSettings() { SpreadOnReceiver = true });
      disc.ResolveOverlap(new OverlapTreatment(true, false, false));
      disc.Fit();
      SwapLeg basis;
      SwapLeg basisFx;
      SwapLeg vanilla;
      SwapLegPricer basisPricer;
      SwapLegPricer basisFxPricer;
      SwapLegPricer vanillaPricer;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis = new SwapLeg(asOf_, disc.Tenors[bnames_[i]].Product.Maturity, prime.IndexTenor.ToFrequency(), 0.0, prime);
        basisFx = new SwapLeg(asOf_, disc.Tenors[bnames_[i]].Product.Maturity, prime.Currency, bquotes_[i] * 1e-4, prime.DayCount,
                              prime.IndexTenor.ToFrequency(), prime.Roll, prime.Calendar, false);
        vanilla = new SwapLeg(asOf_, disc.Tenors[bnames_[i]].Product.Maturity, ri.Currency, quotes_[11 + i], DayCount.Thirty360,
                              Frequency.Quarterly, BDConvention.Following, Calendar.NYB, false);
        basisPricer = new SwapLegPricer(basis, asOf_, asOf_, 1, disc, prime, disc, null, null, null);
        basisFxPricer = new SwapLegPricer(basisFx, asOf_, asOf_, 1, disc, null, null, null, null, null);
        vanillaPricer = new SwapLegPricer(vanilla, asOf_, asOf_, 1, disc, null, null, null, null, null);
        Assert.AreEqual(0.0, Math.Abs(basisPricer.Pv() + basisFxPricer.Pv() - vanillaPricer.Pv()), 2e-4);
      }
    }

    /// <summary>
    /// Test of discount calibration for projection different than discounting 
    /// </summary>
    [Test]
    public void DiscountCalibrationWtBasisSwapsSmooth()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      ReferenceIndex prime = new InterestRateIndex("Prime", new Tenor(6, TimeUnit.Months), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, prime, smoothSettings_);
      var disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes_.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes_[i], DayCount.Actual360);
          continue;
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes_[i]);
        }
        else
        {
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes_[i], DayCount.Thirty360, Frequency.Quarterly,
                       ri.IndexTenor.ToFrequency(),
                       BDConvention.Following, Calendar.NYB, ri, null);
        }
      }
      for (int i = 0; i < bquotes_.Length; i++)
        disc.AddSwap(bnames_[i], 1.0, asOf_, bdates_[i], bquotes_[i]*1e-4, prime.IndexTenor.ToFrequency(),
                     ri.IndexTenor.ToFrequency(), prime, ri, Calendar.None, new PaymentSettings { SpreadOnReceiver = true });
      disc.ResolveOverlap(new OverlapTreatment(true, false, false));
      disc.Fit();
      SwapLeg basis;
      SwapLeg basisFx;
      SwapLeg vanilla;
      SwapLegPricer basisPricer;
      SwapLegPricer basisFxPricer;
      SwapLegPricer vanillaPricer;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis = new SwapLeg(asOf_, Dt.Roll(bdates_[i], prime.Roll, prime.Calendar), prime.IndexTenor.ToFrequency(), 0.0, prime);
        basisFx = new SwapLeg(asOf_, Dt.Roll(bdates_[i], prime.Roll, prime.Calendar), prime.Currency, bquotes_[i] * 1e-4, prime.DayCount,
                              prime.IndexTenor.ToFrequency(), prime.Roll, prime.Calendar, false);
        vanilla = new SwapLeg(asOf_, Dt.Roll(bdates_[i], BDConvention.Following, Calendar.NYB), prime.Currency, quotes_[11 + i], DayCount.Thirty360,
                              Frequency.Quarterly, BDConvention.Following, Calendar.NYB, false);
        basisPricer = new SwapLegPricer(basis, asOf_, asOf_, 1, disc, prime, disc, new RateResets(0, 0), null, null);
        basisFxPricer = new SwapLegPricer(basisFx, asOf_, asOf_, 1, disc, null, null, null, null, null);
        vanillaPricer = new SwapLegPricer(vanilla, asOf_, asOf_, 1, disc, null, null, null, null, null);
        double pv = vanillaPricer.ProductPv();
        Assert.AreEqual(0.0, Math.Abs(basisPricer.Pv() + basisFxPricer.Pv() - vanillaPricer.Pv()), 2e-4);
      }
    }

    /// <summary>
    /// Test of discount curve calibration including FRA instrument
    /// </summary>
    [Test]
    public void TestFRACalibrationDiscountCurveBootstrap()
    {
      FraDiscountCurveBootstrap(quotes_, fraQuotes_, 5e-9);
    }

    [Test]
    public void TestFraDiscountCurveBootstrapNegativeRate()
    {
      FraDiscountCurveBootstrap(nquotes_, nFraQuotes_, 5e-9);
    }

    private void FraDiscountCurveBootstrap(double[] quotes, double[] fraQuotes, double tolerance)
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, ri, bootstrapSettings_);
      var disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes[i], DayCount.Actual360);
          continue;
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes[i]);
        }
        else
        {
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes[i], DayCount.Thirty360, Frequency.Quarterly,
            ri.IndexTenor.ToFrequency(),
            BDConvention.Following, Calendar.NYB, ri, null);
        }
      }

      for (int i = 0; i < fraQuotes.Length; i++)
        disc.AddFRA(fraNames_[i], 1, asOf_, fraDates_[i], fraQuotes[i], ri);

      var overlapOrders = new[] { InstrumentType.Swap, InstrumentType.FRA,
        InstrumentType.FUT, InstrumentType.MM };
      disc.ResolveOverlap(new OverlapTreatment(overlapOrders));
      disc.Fit();
      GetError(disc, tolerance);
    }

    /// <summary>
    /// Test of discount curve calibration including FRA instrument
    /// </summary>
    [Test]
    public void TestFRACalibrationDiscountCurveSmooth()
    {
      FRADiscountCurveSmooth(quotes_, fraQuotes_, 4E-5);
    }

    [Test]
    public void TestFraDiscountCurveSmoothNegativeRate()
    {
      FRADiscountCurveSmooth(nquotes_, nFraQuotes_, 5e-3);
    }


    private void FRADiscountCurveSmooth(double[] quotes, double[] fraQuotes, double tolerance)
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, ri, smoothSettings_);
      var disc = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes.Length; i++)
      {
        if (i < 6)
        {
          disc.AddMoneyMarket(names_[i], dates_[i], quotes[i], DayCount.Actual360);
          continue;
        }
        else if (i > 5 && i < 11)
        {
          disc.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes[i]);
        }
        else
        {
          disc.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes[i], DayCount.Thirty360, 
            Frequency.Quarterly,
            ri.IndexTenor.ToFrequency(),
            BDConvention.Following, Calendar.NYB, ri, null);
        }
      }

      for (int i = 0; i < fraQuotes.Length; i++)
        disc.AddFRA(fraNames_[i], 1, asOf_, fraDates_[i], fraQuotes[i], ri);

      var overlapOrders = new[] { InstrumentType.Swap, InstrumentType.FRA,
        InstrumentType.FUT, InstrumentType.MM };
      disc.ResolveOverlap(new OverlapTreatment(overlapOrders));
      disc.Fit();
      GetError(disc, tolerance);
    }



    /// <summary>
    /// Test of projection curve calibration including FRA instrument
    /// </summary>
    [Test]
    public void TestFRACalibrationProjectCurveBootstrap()
    {
      FraProjectionCurveBootstrap(quotes_, fraQuotes_, 5e-9);
    }

    [Test]
    public void TestFraProjectionCurveBootstrapNegativeRate()
    {
      FraProjectionCurveBootstrap(nquotes_, nFraQuotes_, 5e-9);
    }

    private void FraProjectionCurveBootstrap(double[] quotes, 
      double[] fraQuotes, double tolerance)
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, null, bootstrapSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes.Length; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes[i], DayCount.Actual360);
          continue;
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes[i]);
        }
        else
        {
          proj.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes[i], DayCount.Thirty360, Frequency.Quarterly,
            ri.IndexTenor.ToFrequency(),
            BDConvention.Following, Calendar.NYB, ri, null);
        }
      }

      for (int i = 0; i < fraQuotes.Length; i++)
        proj.AddFRA(fraNames_[i], 1.0, asOf_, fraDates_[i], fraQuotes[i], ri);

      var overlapOrders = new[] { InstrumentType.Swap, InstrumentType.FRA, InstrumentType.FUT, InstrumentType.MM };
      proj.ResolveOverlap(new OverlapTreatment(overlapOrders));
      proj.Fit();
      GetError(proj, tolerance);
    }


    /// <summary>
    /// Test of projection curve calibration including FRA instrument
    /// </summary>
    [Test]
    public void TestFRACalibrationProjectCurveSmooth()
    {
      FraProjCurveSmooth(quotes_, fraQuotes_, 4e-5);
    }

    [Test]
    public void TestFraProjCurveSmoothNegativeRate()
    {
      FraProjCurveSmooth(nquotes_, nFraQuotes_, 5e-3);
    }


    private void FraProjCurveSmooth(double[] quotes, double[] fraQuotes, double tolerance)
    {
      var disc = new DiscountCurve(asOf_, 0.04);
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new ProjectionCurveFitCalibrator(asOf_, disc, ri, null, smoothSettings_);
      var proj = new DiscountCurve(ibs0);
      for (int i = 0; i < quotes.Length; i++)
      {
        if (i < 6)
        {
          proj.AddMoneyMarket(names_[i], dates_[i], quotes[i], DayCount.Actual360);
          continue;
        }
        else if (i > 5 && i < 11)
        {
          proj.AddEDFuture(names_[i], dates_[i], DayCount.Thirty360, quotes[i]);
        }
        else
        {
          proj.AddSwap(names_[i], 1.0, asOf_, dates_[i], quotes[i], 
            DayCount.Thirty360, Frequency.Quarterly,
            ri.IndexTenor.ToFrequency(),
            BDConvention.Following, Calendar.NYB, ri, null);
        }
      }

      for (int i = 0; i < fraQuotes.Length; i++)
        proj.AddFRA(fraNames_[i], 1.0, asOf_, fraDates_[i], fraQuotes[i], ri);

      var overlapOrders = new[] { InstrumentType.Swap, InstrumentType.FRA,
        InstrumentType.FUT, InstrumentType.MM };
      proj.ResolveOverlap(new OverlapTreatment(overlapOrders));
      proj.Fit();
      GetError(proj, tolerance);
    }
  }
}