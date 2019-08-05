using System;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using BaseEntity.Toolkit.Curves;
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
  public class TestFxBasisCalibration : ToolkitTestBase
  {
    #region Data

    public Dt asOf_;
    public Dt[] bdates_;
    public string[] bnames_;
    public double[] bquotes_;
    public DiscountCurve domestic_;
    public DiscountCurve foreign_;
    public FxRate fxRate_;
    public CalibratorSettings bootstrapSettings_;
    public CalibratorSettings smoothSettings_;

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public TestFxBasisCalibration()
    {
      asOf_ = new Dt(28, 1, 2010);
      domestic_ = new DiscountCurve(asOf_, 0.04);
      domestic_.Ccy = Currency.USD;
      foreign_ = new DiscountCurve(asOf_, 0.035);
      foreign_.Ccy = Currency.JPY;
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

      bquotes_ = new double[] {12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25};


      asOf_ = new Dt(28, 1, 2010);
      fxRate_ = new FxRate(asOf_, 2, Currency.JPY, Currency.USD, 0.5, Calendar.NYB, Calendar.TKB);


      var dates_ = new Dt[25];
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


      var quotes_ = new[]
                      {
                        0.00231,
                        0.00239,
                        0.00249,
                        0.00390,
                        0.0063,
                        0.00875,
                        0.99715,
                        0.99610,
                        0.99330,
                        0.98960,
                        0.9858,
                        0.0126,
                        0.01731,
                        0.0245,
                        0.02659,
                        0.02977,
                        0.03229,
                        0.03421,
                        0.03573,
                        0.03707,
                        0.03921,
                        0.04142,
                        0.04295,
                        0.04361,
                        0.04398
                      };

      var names_ = new[]
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


      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new DiscountCurveFitCalibrator(asOf_, ri, null);
      var disc = new DiscountCurve(ibs0);
      disc.Ccy = ri.Currency;
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
                       ri.IndexTenor.ToFrequency(),
                       BDConvention.Following, Calendar.NYB, ri, null);
      }
      disc.ResolveOverlap(new OverlapTreatment(false, false, false));
      disc.Fit();
      domestic_ = (DiscountCurve) disc.Clone();
      bootstrapSettings_ = new CalibratorSettings();
      bootstrapSettings_.Method = CurveFitMethod.Bootstrap;
      bootstrapSettings_.InterpScheme = new InterpScheme { Method = InterpMethod.Weighted };
      smoothSettings_ = new CalibratorSettings();
      smoothSettings_.InterpScheme = InterpScheme.FromInterp(new Weighted());
      smoothSettings_.Method = CurveFitMethod.SmoothForwards;
      smoothSettings_.MarketWeight = 1.0;
      smoothSettings_.SlopeWeight = 0.0;
    }

    #endregion

    private double GetError(CalibratedCurve disc)
    {
      double diff = 0;
      foreach (CurveTenor ten in disc.Tenors)
      {
        if (ten.Product is Swap)
        {
          var sp = (SwapPricer) disc.Calibrator.GetPricer(disc, ten.Product);
          var swp = (Swap) sp.Product;
          double tgt = (swp.IsSpreadOnReceiver) ? swp.ReceiverLeg.Coupon : swp.PayerLeg.Coupon;
          diff += Math.Abs(sp.ParCoupon() - tgt);
        }
      }
      return diff;
    }

    /// <summary>
    /// Calibration of fx basis
    /// </summary>
    [Test]
    public void FxBasisCalibrationBootstrap()
    {
      var setting = new CalibratorSettings();
      setting.Method = CurveFitMethod.Bootstrap;
      setting.InterpScheme = new InterpScheme { Method = InterpMethod.Weighted };
      ReferenceIndex riForeign = new InterestRateIndex("JpyLibor", new Tenor(3, TimeUnit.Months), Currency.JPY,
                                                       DayCount.Actual360, Calendar.None, BDConvention.None, 0);
      ReferenceIndex riDomestic = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new FxBasisFitCalibrator(asOf_, foreign_, domestic_, riForeign, foreign_, riDomestic, domestic_,
                                          fxRate_, bootstrapSettings_);
      var basis = new DiscountCurve(ibs0);
      basis.Ccy = riDomestic.Currency;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis.AddSwap(bnames_[i], 1.0, basis.AsOf, bdates_[i], bquotes_[i] * 1e-4, riForeign.IndexTenor.ToFrequency(),
          riDomestic.IndexTenor.ToFrequency(), riForeign, riDomestic, Calendar.None, new PaymentSettings { PrincipalExchange = true });
      }
      basis.Fit();
      double diff;
      diff = GetError(basis);
      Assert.AreEqual(0, diff, basis.Tenors.Count * 1e-10);
    }
    
    /// <summary>
    /// Calibration of fx basis
    /// </summary>
    [Test]
    public void FxBasisCalibrationSmooth()
    {
      ReferenceIndex riForeign = new InterestRateIndex("JpyLibor", new Tenor(3, TimeUnit.Months), Currency.JPY,
                                                       DayCount.Actual360, Calendar.None, BDConvention.None, 0);
      ReferenceIndex riDomestic = SwapLegTestUtils.GetLiborIndex("6M");

      var ibs0 = new FxBasisFitCalibrator(asOf_, foreign_, domestic_, riForeign, foreign_, riDomestic, domestic_,
                                          fxRate_, smoothSettings_);
      var basis = new DiscountCurve(ibs0);
      basis.Ccy = riDomestic.Currency;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis.AddSwap(bnames_[i], 1.0, basis.AsOf, bdates_[i], bquotes_[i] * 1e-4, riForeign.IndexTenor.ToFrequency(),
          riDomestic.IndexTenor.ToFrequency(), riForeign, riDomestic, Calendar.None, new PaymentSettings { PrincipalExchange = true });
      }
      basis.Fit();
      double diff;
      diff = GetError(basis);
      Assert.AreEqual(0, diff, basis.Tenors.Count * 1e-6);
    }
    
    /// <summary>
    /// Calibration of fx basis with negative spreads
    /// </summary>
    [Test]
    public void FxBasisCalibrationWithNegativeSpreadsBootstrap()
    {
      var setting = new CalibratorSettings();
      setting.Method = CurveFitMethod.Bootstrap;
      setting.InterpScheme = new InterpScheme { Method = InterpMethod.Weighted };
      ReferenceIndex riForeign = new InterestRateIndex("JpyLibor", new Tenor(3, TimeUnit.Months), Currency.JPY,
                                                       DayCount.Actual360, Calendar.None, BDConvention.None, 0);
      ReferenceIndex riDomestic = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new FxBasisFitCalibrator(asOf_, foreign_, domestic_, riForeign, foreign_, riDomestic, domestic_,
                                          fxRate_, bootstrapSettings_);
      var basis = new DiscountCurve(ibs0);
      basis.Ccy = riDomestic.Currency;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis.AddSwap(bnames_[i], 1.0, basis.AsOf, bdates_[i], -bquotes_[i] * 1e-4, riForeign.IndexTenor.ToFrequency(),
          riDomestic.IndexTenor.ToFrequency(), riForeign, riDomestic, Calendar.None, new PaymentSettings { PrincipalExchange = true });
      }
      basis.Fit();
      double diff;
      diff = GetError(basis);
      Assert.AreEqual(0, diff, basis.Tenors.Count * 1e-10);
    }

    /// <summary>
    /// Calibration of fx basis with negative spreads
    /// </summary>
    [Test]
    public void FxBasisCalibrationWithNegativeSpreadsSmooth()
    {
      ReferenceIndex riForeign = new InterestRateIndex("JpyLibor", new Tenor(3, TimeUnit.Months), Currency.JPY,
                                                       DayCount.Actual360, Calendar.None, BDConvention.None, 0);
      ReferenceIndex riDomestic = SwapLegTestUtils.GetLiborIndex("3M");
      var ibs0 = new FxBasisFitCalibrator(asOf_, foreign_, domestic_, riForeign, foreign_, riDomestic, domestic_,
                                          fxRate_, smoothSettings_);
      var basis = new DiscountCurve(ibs0, InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const),
                                    foreign_.DayCount, Frequency.Continuous);
      basis.Ccy = riDomestic.Currency;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis.AddSwap(bnames_[i], 1.0, basis.AsOf, bdates_[i], -bquotes_[i] * 1e-4, riForeign.IndexTenor.ToFrequency(),
          riDomestic.IndexTenor.ToFrequency(), riForeign, riDomestic, Calendar.None, new PaymentSettings { PrincipalExchange = true });
      }
      basis.Fit();
      double diff;
      diff = GetError(basis);
      Assert.AreEqual(0, diff, basis.Tenors.Count * 1e-6);
    }
    

    /// <summary>
    /// Calibration of fx basis swaps with spread paid by the foreign leg
    /// </summary>
    [Test]
    public void FxBasisCalibrationISpreadOnForeignBootstrap()
    {
      var setting = new CalibratorSettings();
      setting.Method = CurveFitMethod.SmoothFutures;
      setting.MarketWeight = 1.0;
      setting.SlopeWeight = 0.0;
      setting.InterpScheme = new InterpScheme {Method = InterpMethod.Weighted};
      ReferenceIndex riForeign = new InterestRateIndex("JpyLibor", new Tenor(3, TimeUnit.Months), Currency.JPY,
                                                       DayCount.Actual360, Calendar.None, BDConvention.None, 0);
      ReferenceIndex riDomestic = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new FxBasisFitCalibrator(asOf_, foreign_, domestic_, riForeign, foreign_, riDomestic, domestic_,
                                          fxRate_, bootstrapSettings_);
      var basis = new DiscountCurve(ibs0);
      basis.Ccy = riDomestic.Currency;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis.AddSwap(bnames_[i], 1.0, basis.AsOf, bdates_[i], -bquotes_[i]*1e-4, riForeign.IndexTenor.ToFrequency(),
                      riDomestic.IndexTenor.ToFrequency(), riForeign, riDomestic, Calendar.None, new PaymentSettings
                                                                                    {
                                                                                      SpreadOnReceiver = true,
                                                                                      PrincipalExchange = true
                                                                                    });
      }
      basis.Fit();
      double diff;
      diff = GetError(basis);
      Assert.AreEqual(0, diff, basis.Tenors.Count * 1e-10);
    }

    /// <summary>
    /// Calibration of fx basis swaps with spread paid by the foreign leg
    /// </summary>
    [Test]
    public void FxBasisCalibrationISpreadOnForeignSmooth()
    {
      var setting = new CalibratorSettings();
      setting.Method = CurveFitMethod.SmoothFutures;
      setting.MarketWeight = 1.0;
      setting.SlopeWeight = 0.0;
      setting.InterpScheme = new InterpScheme { Method = InterpMethod.Weighted };
      ReferenceIndex riForeign = new InterestRateIndex("JpyLibor", new Tenor(3, TimeUnit.Months), Currency.JPY,
                                                       DayCount.Actual360, Calendar.None, BDConvention.None, 0);
      ReferenceIndex riDomestic = SwapLegTestUtils.GetLiborIndex("6M");
      var ibs0 = new FxBasisFitCalibrator(asOf_, foreign_, domestic_, riForeign, foreign_, riDomestic, domestic_,
                                          fxRate_, smoothSettings_);
      var basis = new DiscountCurve(ibs0);
      basis.Ccy = riDomestic.Currency;
      for (int i = 0; i < bquotes_.Length; i++)
      {
        basis.AddSwap(bnames_[i], 1.0, basis.AsOf, bdates_[i], -bquotes_[i] * 1e-4, riForeign.IndexTenor.ToFrequency(),
                      riDomestic.IndexTenor.ToFrequency(), riForeign, riDomestic, Calendar.None, new PaymentSettings
                      {
                        SpreadOnReceiver = true,
                        PrincipalExchange = true
                      });
      }
      basis.Fit();
      double diff;
      diff = GetError(basis);
      Assert.AreEqual(0, diff, basis.Tenors.Count * 1e-6);
    }
  }
}