//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// This test consider combination (3x2x2x2=24)of varying parameters: 
  /// (Spread, Duration, Model) X(OnHazard Rate, OnSpread) X(Relative, Absolute) X(Iterative, NonIterative)
  /// </summary>
  [TestFixture]
  public class TestNewCDXScaling : ToolkitTestBase
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(TestNewCDXScaling));
    #region SetUp

    [OneTimeSetUp]
    public void Initialize()
    {
      string filename = GetTestFilePath(BasketFile);
      BasketData bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      asOf_ = Dt.FromStr(bd.AsOf, "%D");
      settle_ = Dt.FromStr(bd.Settle, "%D");
      relativeScaling_ = !(bd.IndexData.AbsoluteScaling);
      discountCurve_ = bd.GetDiscountCurve();
      if (bd.CreditData != null)
        survivalCurves_ = bd.CreditData.GetSurvivalCurves(discountCurve_);

      // Get survival curves according to selected credit names
      GetSurvivalCurves(bd.IndexData);

      // Get CDX for scaling
      GetCDX(bd.IndexData);

      // Get market quotes
      GetMarketQuotes(bd.IndexData);
    }

    #endregion // SetUp

    #region Tests

    #region Test Not On Hazard Rate
    // Test scaling using "Model, Relative=FALSE, Iterative=TRUE"
    [Test, Smoke]
    public void NohzrdModelAbsltItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = false;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Model;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      scalingFactors = calibrator.GetScalingFactors();
      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      CDXPricer[] pricers = GetCDXPricers(cdx_, quotes_, scaledSurvivalCurves);
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double intrinsicValue = pricers[i].IntrinsicValue(true);
        double marketValue = pricers[i].MarketValue();
        diff[i] = Math.Abs(intrinsicValue - marketValue) * 2 / Math.Abs(intrinsicValue + marketValue);
        Assert.AreEqual(0, diff[i], 1e-6, "NohzrdModelAbsltItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;

      return;
    }

    // Test scaling using "Model, Relative=TRUE, Iterative=TRUE"
    /*[Test, Smoke]
    public void NohzrdModleReltvItrtv()
    {
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      //SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = true;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Model;
      //IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      bool suc = false;
      while (suc != true)
      {
        bool ok = false;
        IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
        try
        {
          scalingFactors = calibrator.ScalingFactors;          
          ok = true;
        }
        catch (Exception e)
        {
          ok = false;
        }
        finally
        {
          if (ok == false)
            suc = false;
          if (ok == true)
          {
            suc = true;
            double[] diff = new double[cdx_.Length];
            CDXPricer[] pricers = GetCDXPricers(cdx_, quotes_, calibrator.ScaledSurvivalCurves);
            for (int i = 0; i < cdx_.Length; ++i)
            {
              double intrinsicValue = pricers[i].IntrinsicValue();
              double marketValue = pricers[i].MarketValue();
              diff[i] = Math.Abs(intrinsicValue - marketValue) * 2 / Math.Abs(intrinsicValue + marketValue);
              Assert.AreEqual("NohzrdModleReltvItrtv" + (i + 1).ToString(), 0, diff[i], 1e-6);
            }
          }
        }
      }
      rd.TimeUsed = timer.Elapsed;
      return;
    }*/
    // Test scaling using "Spread, Relative=TRUE, Iterative=FALSE"    
    [Test, Smoke]
    public void NohzrdSprdReltvNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = true;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Spread;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      try
      {
        scalingFactors = calibrator.GetScalingFactors();

        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

          // Compare average spread and the cdx quote
          double averageSpread = 0;
          for (int j = 0; j < impliedSpreads.Length; ++j)
            averageSpread += impliedSpreads[j];
          averageSpread /= impliedSpreads.Length;

          diff[i] = Math.Abs(averageSpread - quotes_[i]);
          Assert.AreEqual(0, diff[i], 1e-3, "NohzrdSprdReltvNoItrtv" + (i + 1).ToString());
        }
      }
      catch (Exception e)
      {
        logger.DebugFormat("{0}", e.Message);
      }
      finally
      {
        rd.TimeUsed = timer.Elapsed;
      }
      return;
    }
    // Test scaling using "Spread, Relative=FALSE, Iterative=TRUE"
    /*[Test, Smoke]
    public void NohzrdSprdAbsltItrtv()
    {
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = false;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Spread;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      scalingFactors = calibrator.ScalingFactors;
      scaledSurvivalCurves = calibrator.ScaledSurvivalCurves;

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[0].Maturity,
          cdx_[0].DayCount, cdx_[0].Freq, cdx_[0].BDConvention, cdx_[0].Calendar);

        double averageSpread = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
          averageSpread += impliedSpreads[j];
        averageSpread /= impliedSpreads.Length;

        diff[i] = Math.Abs(averageSpread - quotes_[i]);
        Assert.AreEqual("NohzrdSprdAbsltItrtv" + (i + 1).ToString(), 0, diff[i], 1e-3);
      }
      rd.TimeUsed = timer.Elapsed;

      return;
    }*/


    // Test scaling using "Duration, Relative=TRUE, Iterative=FALSE"
    [Test, Smoke]
    public void NohzrdDurtnReltvNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = true;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Duration;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      try
      {
        scalingFactors = calibrator.GetScalingFactors();
        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
          double[] impliedDurations = GetImpliedRiskyDurations(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
          // Compare average spread and the cdx quote
          double durationWeightedSpread = 0;
          double durationSum = 0;
          for (int j = 0; j < impliedSpreads.Length; ++j)
          {
            durationWeightedSpread += impliedSpreads[j] * impliedDurations[j];
            durationSum += impliedDurations[j];
          }
          durationWeightedSpread /= durationSum;

          diff[i] = Math.Abs(durationWeightedSpread - quotes_[i]);
          Assert.AreEqual(0, diff[i], 1e-3, "NohzrdDurtnReltvNoItrtv" + (i + 1).ToString());
        }
      }
      catch (Exception e)
      {
        logger.DebugFormat("{0}", e.Message);
      }
      finally
      {
        rd.TimeUsed = timer.Elapsed;
      }
      return;
    }

    // Test scaling using "Model, Relative=TRUE, Iterative=FALSE"
    [Test, Smoke]
    public void NohzrdModleReltvNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = true;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Model;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      try
      {
        scalingFactors = calibrator.GetScalingFactors();
        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        CDXPricer[] pricers = GetCDXPricers(cdx_, quotes_, scaledSurvivalCurves);
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double intrinsicValue = pricers[i].IntrinsicValue(true);
          double marketValue = pricers[i].MarketValue();
          diff[i] = Math.Abs(intrinsicValue - marketValue) * 2 / Math.Abs(intrinsicValue + marketValue);
          Assert.AreEqual(0, diff[i], 1e-6, "NohzrdModleReltvNoItrtv" + (i + 1).ToString());
        }
      }
      catch (Exception e)
      {
        logger.DebugFormat("{0}", e.Message);
      }
      finally
      {
        rd.TimeUsed = timer.Elapsed;
      }
      return;
    }

    // Test scaling using "Spread, Relative=FALSE, Iterative=FALSE"
    [Test, Smoke]
    public void NohzrdSprdAbsltNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = false;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Spread;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      try
      {
        scalingFactors = calibrator.GetScalingFactors();
        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[0].Maturity,
            cdx_[0].DayCount, cdx_[0].Freq, cdx_[0].BDConvention, cdx_[0].Calendar);

          double averageSpread = 0;
          for (int j = 0; j < impliedSpreads.Length; ++j)
            averageSpread += impliedSpreads[j];
          averageSpread /= impliedSpreads.Length;

          diff[i] = Math.Abs(averageSpread - quotes_[i]);
          Assert.AreEqual(0, diff[i], 1e-3, "NohzrdSprdAbsltNoItrtv" + (i + 1).ToString());
        }
      }
      catch (Exception e)
      {
        logger.DebugFormat("{0}", e.Message);
      }
      finally
      {
        rd.TimeUsed = timer.Elapsed;
      }
      return;
    }

    // Test scaling using "Duration, Relative=FALSE, Iterative=FALSE"
    [Test, Smoke]
    //[ExpectedException(typeof(System.Exception))]
    public void NohzrdDurtnAbsltNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;      

      bool scaleHazardRate = false;
      bool relative = false;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Duration;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      scalingFactors = calibrator.GetScalingFactors();
    }

    // Test scaling using "Model, Relative=FALSE, Iterative=FALSE"
    [Test, Smoke]
    public void NohzrdModelAbsltNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = false;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Model;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      try
      {
        scalingFactors = calibrator.GetScalingFactors();
        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        CDXPricer[] pricers = GetCDXPricers(cdx_, quotes_, scaledSurvivalCurves);
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double intrinsicValue = pricers[i].IntrinsicValue(true);
          double marketValue = pricers[i].MarketValue();
          diff[i] = Math.Abs(intrinsicValue - marketValue) * 2 / Math.Abs(intrinsicValue + marketValue);
          Assert.AreEqual(0, diff[i], 1e-6, "NohzrdModelAbsltNoItrtv" + (i + 1).ToString());
        }
      }
      catch (Exception e)
      {
        logger.DebugFormat("{0}", e.Message);
      }
      finally
      {
        rd.TimeUsed = timer.Elapsed;
      }
      return;
    }

    // Test scaling using "Spread, Relative=TRUE, Iterative=TRUE"
    [Test, Smoke, Ignore("Not work yet")]
    public void NohzrdSprdReltvItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = true;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Spread;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      scalingFactors = calibrator.GetScalingFactors();
      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

        // Compare average spread and the cdx quote
        double averageSpread = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
          averageSpread += impliedSpreads[j];
        averageSpread /= impliedSpreads.Length;

        diff[i] = Math.Abs(averageSpread - quotes_[i]);
        Assert.AreEqual(0, diff[i], 1e-3, "NohzrdSprdReltvItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
      return;
    }

    // Test scaling using "Duration, Relative=TRUE, Iterative=TRUE"
    [Test, Smoke, Ignore("Not work yet")]
    public void NohzrdDurtnReltvItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = true;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Duration;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);
      scalingFactors = calibrator.GetScalingFactors();
      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
        double[] impliedDurations = GetImpliedRiskyDurations(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
        // Compare average spread and the cdx quote
        double durationWeightedSpread = 0;
        double durationSum = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
        {
          durationWeightedSpread += impliedSpreads[j] * impliedDurations[j];
          durationSum += impliedDurations[j];
        }
        durationWeightedSpread /= durationSum;

        diff[i] = Math.Abs(durationWeightedSpread - quotes_[i]);
        Assert.AreEqual(0, diff[i], 1e-3, "NohzrdDurtnReltvItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
      return;
    }



    // Test scaling using "Duration, Relative=FALSE, Iterative=TRUE"
    [Test, Smoke]
    //[ExpectedException(typeof(System.Exception))]
    public void NohzrdDurtnAbsltItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = false;
      bool relative = false;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Duration;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      scalingFactors = calibrator.GetScalingFactors();
      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
        double[] impliedDurations = GetImpliedRiskyDurations(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

        double durationWeightedSpread = 0;
        double durationSum = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
        {
          durationWeightedSpread += impliedSpreads[j] * impliedDurations[j];
          durationSum += impliedDurations[j];
        }
        durationWeightedSpread /= durationSum;

        diff[i] = Math.Abs(durationWeightedSpread - quotes_[i]);
        Assert.AreEqual(0, diff[i], 1e-3, "NohzrdDurtnAbsltItrtv" + (i + 1).ToString());
      }

      rd.TimeUsed = timer.Elapsed;
      return;
    }



    #endregion Test Not On Hazard Rate

    #region Test On Hazard Rate
    // Test scaling using "Spread, Relative=TRUE, Iterative=FALSE"    
    [Test, Smoke]
    public void HzrdSprdReltvNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = true;
      bool relative = true;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Spread;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      scalingFactors = calibrator.GetScalingFactors();

      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

        // Compare average spread and the cdx quote
        double averageSpread = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
          averageSpread += impliedSpreads[j];
        averageSpread /= impliedSpreads.Length;

        diff[i] = Math.Abs(averageSpread - quotes_[i]);
        Assert.AreEqual(0, diff[i], 1e-3, "HzrdSprdReltvNoItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
    }

    // Test scaling using "Duration, Relative=TRUE, Iterative=FALSE"
    [Test, Smoke]
    public void HzrdDurtnReltvNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = true;
      bool relative = true;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Duration;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      scalingFactors = calibrator.GetScalingFactors();

      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
        double[] impliedDurations = GetImpliedRiskyDurations(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
        // Compare average spread and the cdx quote
        double durationWeightedSpread = 0;
        double durationSum = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
        {
          durationWeightedSpread += impliedSpreads[j] * impliedDurations[j];
          durationSum += impliedDurations[j];
        }
        durationWeightedSpread /= durationSum;

        diff[i] = Math.Abs(durationWeightedSpread - quotes_[i]);
        Assert.AreEqual(0, diff[i], 1e-3, "HzrdDurtnReltvNoItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
    }

    // Test scaling using "Model, Relative=TRUE, Iterative=FALSE"
    [Test, Smoke]
    public void HzrdModleReltvNoItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = true;
      bool relative = true;
      bool addTenor = false;
      bool iterative = false;
      CDXScalingMethod method = CDXScalingMethod.Model;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      scalingFactors = calibrator.GetScalingFactors();

      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      CDXPricer[] pricers = GetCDXPricers(cdx_,  quotes_, scaledSurvivalCurves);
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double intrinsicValue = pricers[i].IntrinsicValue(true);
        double marketValue = pricers[i].MarketValue();

        diff[i] = Math.Abs(intrinsicValue - marketValue) * 2 / Math.Abs(intrinsicValue + marketValue);
        Assert.AreEqual(0, diff[i], 1e-6, "HzrdModleReltvNoItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
    }

    // Test scaling using "Spread, Relative=FALSE, Iterative=FALSE"
    [Test, Smoke]
    public void HzrdSprdAbsltNoItrtv()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        Initialize();
        ResultData rd = new ResultData();
        Timer timer = new Timer();
        double[] scalingFactors;
        SurvivalCurve[] scaledSurvivalCurves;

        bool scaleHazardRate = true;
        bool relative = false;
        bool addTenor = false;
        bool iterative = false;
        CDXScalingMethod method = CDXScalingMethod.Spread;
        IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

        scalingFactors = calibrator.GetScalingFactors();

        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

          // Compare average spread and the cdx quote
          double averageSpread = 0;
          for (int j = 0; j < impliedSpreads.Length; ++j)
            averageSpread += impliedSpreads[j];
          averageSpread /= impliedSpreads.Length;

          diff[i] = Math.Abs(averageSpread - quotes_[i]);
          Assert.AreEqual(0, diff[i], 1e-3, "HzrdSprdAbsltNoItrtv" + (i + 1).ToString());
        }

        rd.TimeUsed = timer.Elapsed;
      });
    }

    // Test scaling using "Duration, Relative=FALSE, Iterative=FALSE"
    [Test, Smoke]
    public void HzrdDurtnAbsltNoItrtv()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        Initialize();
        ResultData rd = new ResultData();
        Timer timer = new Timer();
        double[] scalingFactors;
        SurvivalCurve[] scaledSurvivalCurves;

        bool scaleHazardRate = true;
        bool relative = false;
        bool addTenor = false;
        bool iterative = false;
        CDXScalingMethod method = CDXScalingMethod.Duration;
        IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

        scalingFactors = calibrator.GetScalingFactors();

        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
          double[] impliedDurations = GetImpliedRiskyDurations(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

          double durationWeightedSpread = 0;
          double durationSum = 0;
          for (int j = 0; j < impliedSpreads.Length; ++j)
          {
            durationWeightedSpread += impliedSpreads[j]*impliedDurations[j];
            durationSum += impliedDurations[j];
          }

          durationWeightedSpread /= durationSum;

          diff[i] = Math.Abs(durationWeightedSpread - quotes_[i]);
          Assert.AreEqual(0, diff[i], 1e-3, "HzrdDurtnAbsltNoItrtv" + (i + 1).ToString());
        }

        rd.TimeUsed = timer.Elapsed;
      });
    }

    // Test scaling using "Model, Relative=FALSE, Iterative=FALSE"
    [Test, Smoke]
    public void HzrdModleAbsltNoItrtv()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        Initialize();
        ResultData rd = new ResultData();
        Timer timer = new Timer();
        double[] scalingFactors;
        SurvivalCurve[] scaledSurvivalCurves;

        bool scaleHazardRate = true;
        bool relative = false;
        bool addTenor = false;
        bool iterative = false;
        CDXScalingMethod method = CDXScalingMethod.Model;
        IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

        scalingFactors = calibrator.GetScalingFactors();

        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        CDXPricer[] pricers = GetCDXPricers(cdx_, quotes_, scaledSurvivalCurves);
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double intrinsicValue = pricers[i].IntrinsicValue(true);
          double marketValue = pricers[i].MarketValue();

          diff[i] = Math.Abs(intrinsicValue - marketValue)*2/Math.Abs(intrinsicValue + marketValue);
          Assert.AreEqual(0, diff[i], 1e-6, "HzrdModleAbsltNoItrtv" + (i + 1).ToString());
        }

        rd.TimeUsed = timer.Elapsed;
      });
    }

    // Test scaling using "Spread, Relative=TRUE, Iterative=TRUE"    
    [Test, Smoke]
    public void HzrdSprdReltvItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = true;
      bool relative = true;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Spread;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      scalingFactors = calibrator.GetScalingFactors();

      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

        // Compare average spread and the cdx quote
        double averageSpread = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
          averageSpread += impliedSpreads[j];
        averageSpread /= impliedSpreads.Length;

        diff[i] = Math.Abs(averageSpread - quotes_[i]);
        Assert.AreEqual(0, diff[i], 1e-3, "HzrdSprdReltvItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
    }

    // Test scaling using "Duration, Relative=TRUE, Iterative=TRUE"
    [Test, Smoke]
    public void HzrdDurtnReltvItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = true;
      bool relative = true;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Duration;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      scalingFactors = calibrator.GetScalingFactors();

      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
        double[] impliedDurations = GetImpliedRiskyDurations(scaledSurvivalCurves, cdx_[i].Maturity,
          cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
        // Compare average spread and the cdx quote
        double durationWeightedSpread = 0;
        double durationSum = 0;
        for (int j = 0; j < impliedSpreads.Length; ++j)
        {
          durationWeightedSpread += impliedSpreads[j] * impliedDurations[j];
          durationSum += impliedDurations[j];
        }
        durationWeightedSpread /= durationSum;

        diff[i] = Math.Abs(durationWeightedSpread - quotes_[i]);
        Assert.AreEqual(0, diff[i], 1e-3, "HzrdDurtnReltvItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
    }

    // Test scaling using "Model, Relative=TRUE, Iterative=TRUE"
    [Test, Smoke]
    public void HzrdModleReltvItrtv()
    {
      Initialize();
      ResultData rd = new ResultData();
      Timer timer = new Timer();
      double[] scalingFactors;
      SurvivalCurve[] scaledSurvivalCurves;

      bool scaleHazardRate = true;
      bool relative = true;
      bool addTenor = false;
      bool iterative = true;
      CDXScalingMethod method = CDXScalingMethod.Model;
      IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

      scalingFactors = calibrator.GetScalingFactors();

      scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

      double[] diff = new double[cdx_.Length];
      CDXPricer[] pricers = GetCDXPricers(cdx_, quotes_, scaledSurvivalCurves);
      for (int i = 0; i < cdx_.Length; ++i)
      {
        double intrinsicValue = pricers[i].IntrinsicValue(true);
        double marketValue = pricers[i].MarketValue();
        diff[i] = Math.Abs(intrinsicValue - marketValue) * 2 / Math.Abs(intrinsicValue + marketValue);
        Assert.AreEqual(0, diff[i], 1e-6, "HzrdModleReltvItrtv" + (i + 1).ToString());
      }
      rd.TimeUsed = timer.Elapsed;
    }

    // Test scaling using "Spread, Relative=FALSE, Iterative=TRUE"
    [Test, Smoke]
    public void HzrdSprdAbsltItrtv()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        Initialize();
        ResultData rd = new ResultData();
        Timer timer = new Timer();
        double[] scalingFactors;
        SurvivalCurve[] scaledSurvivalCurves;

        bool scaleHazardRate = true;
        bool relative = false;
        bool addTenor = false;
        bool iterative = true;
        CDXScalingMethod method = CDXScalingMethod.Spread;
        IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

        scalingFactors = calibrator.GetScalingFactors();

        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);

          // Compare average spread and the cdx quote
          double averageSpread = 0;
          for (int j = 0; j < impliedSpreads.Length; ++j)
            averageSpread += impliedSpreads[j];
          averageSpread /= impliedSpreads.Length;

          diff[i] = Math.Abs(averageSpread - quotes_[i]);
          Assert.AreEqual( 0, diff[i], 1e-3, "HzrdSprdAbsltItrtv" + (i + 1).ToString());
        }

        rd.TimeUsed = timer.Elapsed;
      });
    }

    // Test scaling using "Duration, Relative=FALSE, Iterative=TRUE"
    [Test, Smoke]
    public void HzrdDurtnAbsltItrtv()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        Initialize();
        ResultData rd = new ResultData();
        Timer timer = new Timer();
        double[] scalingFactors;
        SurvivalCurve[] scaledSurvivalCurves;

        bool scaleHazardRate = true;
        bool relative = false;
        bool addTenor = false;
        bool iterative = true;
        CDXScalingMethod method = CDXScalingMethod.Duration;
        IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

        scalingFactors = calibrator.GetScalingFactors();

        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double[] impliedSpreads = GetImpliedSpreads(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
          double[] impliedDurations = GetImpliedRiskyDurations(scaledSurvivalCurves, cdx_[i].Maturity,
            cdx_[i].DayCount, cdx_[i].Freq, cdx_[i].BDConvention, cdx_[i].Calendar);
          // Compare average spread and the cdx quote
          double durationWeightedSpread = 0;
          double durationSum = 0;
          for (int j = 0; j < impliedSpreads.Length; ++j)
          {
            durationWeightedSpread += impliedSpreads[j]*impliedDurations[j];
            durationSum += impliedDurations[j];
          }

          durationWeightedSpread /= durationSum;

          diff[i] = Math.Abs(durationWeightedSpread - quotes_[i]);
          Assert.AreEqual(0, diff[i], 1e-3, "HzrdDurtnAbsltItrtv" + (i + 1).ToString());
        }

        rd.TimeUsed = timer.Elapsed;
      });
    }

    // Test scaling using "Model, Relative=FALSE, Iterative=TRUE"
    [Test, Smoke]
    public void HzrdModleAbsltItrtv()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        Initialize();
        ResultData rd = new ResultData();
        Timer timer = new Timer();
        double[] scalingFactors;
        SurvivalCurve[] scaledSurvivalCurves;

        bool scaleHazardRate = true;
        bool relative = false;
        bool addTenor = false;
        bool iterative = true;
        CDXScalingMethod method = CDXScalingMethod.Model;
        IndexScalingCalibrator calibrator = GetCalibrator(method, 0.4, scaleHazardRate, relative, addTenor, iterative);

        scalingFactors = calibrator.GetScalingFactors();

        scaledSurvivalCurves = calibrator.GetScaleSurvivalCurves();

        double[] diff = new double[cdx_.Length];
        CDXPricer[] pricers = GetCDXPricers(cdx_, quotes_, scaledSurvivalCurves);
        for (int i = 0; i < cdx_.Length; ++i)
        {
          double intrinsicValue = pricers[i].IntrinsicValue(true);
          double marketValue = pricers[i].MarketValue();
          diff[i] = Math.Abs(intrinsicValue - marketValue)*2/Math.Abs(intrinsicValue + marketValue);
          Assert.AreEqual(0, diff[i], 1e-6, "HzrdModleAbsltItrtv" + (i + 1).ToString());
        }

        rd.TimeUsed = timer.Elapsed;
      });
    }

    #endregion // Test on hazard rate

    #endregion // Tests

    #region helpers
    /// <summary>
    ///   Calculate scaling factors
    /// </summary>
    /// <param name="method">Scaling method</param>
    /// <param name="rs">Result dataset</param>
    /// <param name="timer">Timer</param>
    /// <returns>Array of scaling factors</returns>
    private void DoScaling(
      CDXScalingMethod method,
      bool relativeScaling,
      ResultData.ResultSet rs,
      out double[] scalingFactors,
      out SurvivalCurve[] scaledSurvivalCurves,
      Timer timer)
    {
      // Setup scaling methods
      int nTenors = tenors_.Length;
      CDXScalingMethod[] scalingMethods = new CDXScalingMethod[nTenors];
      for (int i = 0; i < nTenors; ++i)
      {
        scalingMethods[i] = quotes_[i] <= 0.0 ? CDXScalingMethod.Next : method;
      }
      for (int i = nTenors - 1; i >= 0; --i)
      {
        if (scalingMethods[i] != CDXScalingMethod.Next)
          break;
        scalingMethods[i] = CDXScalingMethod.Previous;
      }
      // scalingMethod_ is the user inputs
      if (ScalingMethods != null)
      {
        // overriden by the use inputs
        string[] sms = ScalingMethods.Split(',');
        for (int i = 0; i < sms.Length && i < scalingMethods.Length; ++i)
          if (sms[i] != null)
          {
            string m = sms[i].Trim();
            if (m.Length <= 0)
              continue;
            scalingMethods[i] = (CDXScalingMethod)Enum.Parse(typeof(CDXScalingMethod), m);
          }
      }

      double[] overrideFactors = null;
      if (OverrideFactors != null)
      {
        overrideFactors = new double[nTenors];
        // overriden by the use inputs
        string[] sos = OverrideFactors.Split(',');
        for (int i = 0; i < sos.Length && i < nTenors; ++i)
          if (sos[i] != null)
          {
            string m = sos[i].Trim();
            if (m.Length <= 0)
              continue;
            overrideFactors[i] = Double.Parse(m);
          }
      }

      // Call scaling routine
      if (timer != null)
        timer.Resume();
      scalingFactors = CDXPricer.Scaling(asOf_, settle_, cdx_, tenors_, quotes_, quotesArePrices_,
        scalingMethods, relativeScaling, overrideFactors, discountCurve_, survivalCurves_, scalingWeights_);

      scaledSurvivalCurves = null;

      if (timer != null)
        timer.Stop();

      if (rs != null)
      {
        rs.Name = method.ToString();
        rs.Labels = tenors_;
        rs.Actuals = scalingFactors;
      }
    }

    /// <summary>
    ///   Scale an individual survival curve
    /// </summary>
    /// <param name="name">Name of the scaled curve</param>
    /// <param name="survivalCurve">Curve to scale (input)</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="factors">Scaling factors</param>
    /// <param name="weight">Scaling weight</param>
    /// <returns>Scaled curve</returns>
    private SurvivalCurve ScaleSurvivalCurve(
      string name,
      SurvivalCurve survivalCurve,
      string[] tenorNames,
      double[] factors,
      double weight)
    {
      // Apply scaling weight if neccessay
      double[] scalingFactors = factors;
      if (weight != 1.0)
      {
        scalingFactors = new double[factors.Length];
        for (int i = 0; i < scalingFactors.Length; i++)
          scalingFactors[i] = factors[i] * weight;
      }

      // Copy original survival curve
      Dt asOfDate = survivalCurve.AsOf;
      DiscountCurve discountCurve = survivalCurve.SurvivalCalibrator.DiscountCurve;
      RecoveryCurve recoveryCurve = survivalCurve.SurvivalCalibrator.RecoveryCurve;

      SurvivalFitCalibrator calibrator =
        new SurvivalFitCalibrator(asOfDate, asOfDate, recoveryCurve, discountCurve);
      calibrator.NegSPTreatment = survivalCurve.SurvivalCalibrator.NegSPTreatment;

      SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = survivalCurve.Interp.clone();
      curve.Ccy = survivalCurve.Ccy;
      curve.Category = survivalCurve.Category;
      curve.Name = name;

      foreach (CurveTenor t in survivalCurve.Tenors)
      {
        CurveTenor tenor = (CurveTenor)t.Clone();
        curve.Tenors.Add(tenor);
      }

      // Be sure to fit the curve first, otherwise the bump might do the wrong thing.
      curve.Fit();

      // Scale tenors
      CurveUtil.CurveBump(new SurvivalCurve[] { curve }, tenorNames, scalingFactors, true, true, true, null);

      return curve;
    }

    

    

    private void GetSurvivalCurves(BasketData.Index id)
    {
      if (id.CreditNames != null && id.CreditNames.Length != survivalCurves_.Length)
      {
        SurvivalCurve[] sc = survivalCurves_;
        survivalCurves_ = new SurvivalCurve[id.CreditNames.Length];
        int idx = 0;
        foreach (string name in id.CreditNames)
          survivalCurves_[idx++] = (SurvivalCurve)FindCurve(name, sc);
      }
      return;
    }

    // Create indices for scaling
    private void GetCDX(BasketData.Index id)
    {
      tenors_ = id.TenorNames;
      int nTenors = tenors_.Length;

      Dt effective = Dt.FromStr(id.Effective, "%D");
      Dt firstPremiumDate = id.FirstPremium == null ? new Dt() : Dt.FromStr(id.FirstPremium, "%D");
      string[] maturities = id.Maturities;
      double[] dealPremiums = id.DealPremia;
      double[] indexWeights = id.CreditWeights;

      cdx_ = new CDX[nTenors];
      for (int i = 0; i < nTenors; i++)
      {
        Dt maturity = (maturities == null || maturities.Length == 0) ?
          Dt.CDSMaturity(effective, tenors_[i]) : Dt.FromStr(maturities[i], "%D");
        cdx_[i] = new CDX(effective, maturity, id.Currency,
                          dealPremiums[i] / 10000.0, id.DayCount,
                          id.Frequency, id.Roll, id.Calendar, indexWeights);
        if (!firstPremiumDate.IsEmpty())
          cdx_[i].FirstPrem = firstPremiumDate;
        cdx_[i].Funded = false;
      }
      return;
    }

    // Create CDX pricers for CDX's  
    private CDXPricer[] GetCDXPricers(CDX[] cdx, double[] marketquotes, SurvivalCurve[] curves)
    {
      CDXPricer[] pricers = new CDXPricer[cdx.Length];
      for (int i = 0; i < cdx.Length; ++i)
      {
        pricers[i] = new CDXPricer(cdx[i], asOf_, settle_, discountCurve_, curves, marketquotes[i]);
        pricers[i].QuotingConvention = quotesArePrices_ ? QuotingConvention.FlatPrice : QuotingConvention.CreditSpread;
        pricers[i].Notional = 1000000;
      }
      return pricers;
    }

    // Setup quotes and methods
    private void GetMarketQuotes(BasketData.Index id)
    {
      quotesArePrices_ = id.QuotesArePrices;
      quotes_ = new double[id.TenorNames.Length];
      for (int i = 0; i < id.TenorNames.Length; ++i)
        quotes_[i] = id.Quotes[i];

      double[] scalingWeights = id.ScalingWeights;

      return;
    }

    // Get index scaling calibrator
    private IndexScalingCalibrator GetCalibrator(CDXScalingMethod scalingMethod, double marketRecoveryRate,
      bool scaleHazardRate, bool relative, bool addTenor, bool iterative)
    {
      CDXScalingMethod[] scalingMethods = new CDXScalingMethod[] { scalingMethod };

      // quotes should be in full bps
      IndexScalingCalibrator calc = BuildScalingCalculator(cdx_, tenors_,
        asOf_, settle_, quotes_, scalingMethods, relative,
        discountCurve_, survivalCurves_, null, null,
        quotesArePrices_, marketRecoveryRate, iterative);

      calc.ScaleHazardRates = scaleHazardRate;
      calc.ScalingType = relative ? BumpMethod.Relative : BumpMethod.Absolute;
      //calc.CdxScaleMethod = cdxScalingMethods[0];
      calc.ActionOnInadequateTenors = addTenor ? ActionOnInadequateTenors.AddCurveTenors :
        ActionOnInadequateTenors.DropFirstIndexTenor;
      // If user action is to drop first index tenor, need to check&update UseTenors
      if (calc.ActionOnInadequateTenors == ActionOnInadequateTenors.DropFirstIndexTenor)
        calc.CheckUseTenors();

      // Set iterative or not falg to scaling calibrator
      calc.Iterative = iterative;

      return calc;
    }

    // Build the index scaling calibator 
    private static IndexScalingCalibrator BuildScalingCalculator(CDX[] cdx, string[] tenors, Dt asOf, Dt settle,
      double[] quotes, CDXScalingMethod[] scalingMethods, bool relative, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves,
      double[] indexWeights, bool[] includes, bool quotesArePrices, double marketRecovery, bool useIterative)
    {
      // Check basket input
      //SurvivalCurve[] survCurves = BasketDataAddin.CheckBasketInput(survivalCurves, ref indexWeights);

      // Validate. maturities (for index), tenors (for cds curves that need to be bumped)
      if (indexWeights != null && indexWeights.Length != 0 && indexWeights.Length != survivalCurves.Length)
        throw new System.ArgumentException("Number of index weights must match number of survival curves");
      if (includes != null && includes.Length != 0 && includes.Length != survivalCurves.Length)
        throw new System.ArgumentException("Number of scaling weights must match number of survival curves");

      // Remove empty or zero weight survival curves
      if (includes != null && includes.Length > 0)
      {
        includes = BaseEntity.Shared.ArrayUtil.GenerateIf<bool>(survivalCurves.Length,
        delegate(int i) { return survivalCurves[i] != null; },
        delegate(int i) { return includes[i]; });
      }
      if (indexWeights != null && indexWeights.Length > 0)
      {
        indexWeights = BaseEntity.Shared.ArrayUtil.GenerateIf<double>(survivalCurves.Length,
          delegate(int i) { return survivalCurves[i] != null; },
          delegate(int i) { return indexWeights[i]; });
        Toolkit.Base.Utils.Normalize(indexWeights, 1.0);
      }

      // Here we use the compacted new length of survCurves
      if (indexWeights == null || indexWeights.Length == 0)
        indexWeights = BaseEntity.Shared.ArrayUtil.NewArray(survivalCurves.Length, 1.0 / survivalCurves.Length);
      if (includes == null || includes.Length == 0)
        includes = BaseEntity.Shared.ArrayUtil.NewArray(survivalCurves.Length, true);

      double recoveryRate = 0.4;
      if (Math.Abs(marketRecovery) <= 1.0)
        recoveryRate = marketRecovery;

      // Create indices for scaling
      for (int i = 0; i < cdx.Length; ++i)
        if (cdx[i] != null) cdx[i].Weights = indexWeights;

      // Convert quoted spreads from basis points
      if (!quotesArePrices)
      {
        for (int i = 0; i < quotes.Length; i++)
          quotes[i] /= 10000.0;
      }
      else
        for (int i = 0; i < quotes.Length; i++)
          quotes[i] /= 100.0;

      // Return resulting scaling      
      IndexScalingCalibrator calibrator =
        new IndexScalingCalibrator(asOf, settle, cdx, tenors, quotes, quotesArePrices, scalingMethods,
                                   relative, false, discountCurve, survivalCurves, includes, recoveryRate);
      calibrator.Iterative = useIterative;
      return calibrator;
    }

    private Curve FindCurve(string name, Curve[] curves)
    {
      foreach (Curve c in curves)
        if (String.Compare(name, c.Name) == 0)
          return c;
      throw new System.Exception(String.Format("Curve name '{0}' not found", name));
    }

    // Compute the implied spread for scaled survival curves at specified maturity
    private double[] GetImpliedSpreads(SurvivalCurve[] curves, Dt maturity,
      DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      return Array.ConvertAll<SurvivalCurve, double>(
        curves,
        delegate(SurvivalCurve c)
        {
          return CurveUtil.ImpliedSpread(c, maturity, dayCount, frequency, roll, calendar);
        }
        );
    }

    private double[] GetImpliedRiskyDurations(SurvivalCurve[] curves, Dt maturity,
      DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      return Array.ConvertAll<SurvivalCurve, double>(
        curves,
        delegate(SurvivalCurve c)
        {
          return CurveUtil.ImpliedDuration(c, maturity, dayCount, frequency, roll, calendar) * 10000.0;
        }
        );
    }
    #endregion helpers

    #region Properties
    /// <summary>
    ///   File containing input data
    /// </summary>
    public string BasketFile { get; set; } = "TestNewCDXScalingBasketData.xml";

    /// <summary>
    ///  Array of scaling methods
    /// </summary>
    public string ScalingMethods { get; set; } = null;

    /// <summary>
    ///  Array of scaling methods
    /// </summary>
    public string OverrideFactors { get; set; } = null;

    #endregion // Properties

    #region Data

    private Dt asOf_;
    private Dt settle_;
    private DiscountCurve discountCurve_ = null;
    private SurvivalCurve[] survivalCurves_ = null;

    private string[] tenors_ = null;
    private CDX[] cdx_ = null;
    private double[] quotes_ = null;
    private bool quotesArePrices_ = false;
    private double[] scalingWeights_ = null;
    private bool relativeScaling_ = false;

    #endregion // Data
  }
}
