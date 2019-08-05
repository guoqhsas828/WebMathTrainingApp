/*
 * TestJTD_At_DifRecovRates.cs
 * 
 * This test will generate expected data for JTD at 
 * 0, 10%, 20%, 30%, and 40% ercovery rates 
 * 
 * Copyright (c) 2005-2008,   . All rights reserved.
 */
using System;
using System.Data;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestJTD_At_DifRecovRates : ToolkitTestBase
  {
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {
      string filename = GetTestFilePath(basketDataFile_);

      bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      bd.RescaleStrikes = this.rescaleStrikes_;

      if (bd.CreditData == null)
      {
        if (creditDataFile_ == null)
          throw new System.Exception("No credit data");
        filename = GetTestFilePath(creditDataFile_);
        bd.CreditData = (CreditData)XmlLoadData(filename, typeof(CreditData));
      }

      if (bd.DiscountData == null)
      {
        if (irDataFile_ == null)
          throw new System.Exception("No interest rates data");
        filename = GetTestFilePath(irDataFile_);
        bd.DiscountData = (DiscountData)XmlLoadData(filename, typeof(DiscountData));
      }

      if (basketType_ != null && basketType_.Length > 0)
        bd.Type = (BasketData.BasketType)Enum.Parse(typeof(BasketData.BasketType), basketType_);

      if (copulaData_ != null && copulaData_.Length > 0)
      {
        string[] elems = copulaData_.Replace(" ", "").Split(new char[] { ',' });
        if (elems.Length < 3)
          throw new ArgumentException("Invalid copula data");
        bd.CopulaType = (CopulaType)Enum.Parse(typeof(CopulaType), elems[0]);
        bd.DfCommon = Int32.Parse(elems[1]);
        bd.DfIdiosyncratic = Int32.Parse(elems[2]);
      }

      ExtractAllParameters();
      
      CreateCDOPricers();

      return;
    }

    #endregion

    #region Test

    [Test, Smoke]
    public void Test_0_RecovRate()
    {      
      Timer timer = new Timer();
      timer.Start();
      DataTable dt = null;
      double res = 0;

      bool calcHedge = false;
      string hedgeTenor = "";
      double[] defaultRecoveries = new double[] { 0 };
      dt = Sensitivities.Default(new SyntheticCDOPricer[] { cdoPricers_[1] }, "Pv", calcHedge, hedgeTenor, null, defaultRecoveries, null);
                
      string columnToSum = "Delta";
      string filterColumn1 = "Value Name";
      string valueToMatch1 = "3%-7%";
      string filterColumn2 = "";
      string valueToMatch2 = "";
      string filterColumn3 = "";
      string valueToMatch3 = "";
          
      filterColumn1 = MapColumnNames(filterColumn1);
      filterColumn2 = MapColumnNames(filterColumn2);
      filterColumn3 = MapColumnNames(filterColumn3);
      
      if (filterColumn2 == null || filterColumn2.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1);
      else if (filterColumn3 == null || filterColumn3.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1, filterColumn2, valueToMatch2);
      else
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1,
                                 filterColumn2, valueToMatch2, filterColumn3, valueToMatch3);
    
      double[] diff = new double[] { res };
      string[] labels = new string[] { "Average" };
      timer.Stop();
      MatchExpects(diff, labels, timer.Elapsed);
    }

    [Test, Smoke]
    public void Test_10_RecovRate()
    {
      Timer timer = new Timer();
      timer.Start();
      DataTable dt = null;
      double res = 0;

      bool calcHedge = false;
      string hedgeTenor = "";
      double[] defaultRecoveries = new double[] { 0.1 };
      dt = Sensitivities.Default(new SyntheticCDOPricer[] { cdoPricers_[1] }, "Pv", calcHedge, hedgeTenor, null, defaultRecoveries, null);

      string columnToSum = "Delta";
      string filterColumn1 = "Value Name";
      string valueToMatch1 = "3%-7%";
      string filterColumn2 = "";
      string valueToMatch2 = "";
      string filterColumn3 = "";
      string valueToMatch3 = "";

      filterColumn1 = MapColumnNames(filterColumn1);
      filterColumn2 = MapColumnNames(filterColumn2);
      filterColumn3 = MapColumnNames(filterColumn3);

      if (filterColumn2 == null || filterColumn2.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1);
      else if (filterColumn3 == null || filterColumn3.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1, filterColumn2, valueToMatch2);
      else
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1,
                                 filterColumn2, valueToMatch2, filterColumn3, valueToMatch3);

      double[] diff = new double[] { res };
      string[] labels = new string[] { "Average" };
      timer.Stop();
      MatchExpects(diff, labels, timer.Elapsed);
    }

    [Test, Smoke]
    public void Test_20_RecovRate()
    {
      Timer timer = new Timer();
      timer.Start();
      DataTable dt = null;
      double res = 0;

      bool calcHedge = false;
      string hedgeTenor = "";
      double[] defaultRecoveries = new double[] { 0.2 };
      dt = Sensitivities.Default(new SyntheticCDOPricer[] { cdoPricers_[1] }, "Pv", calcHedge, hedgeTenor, null, defaultRecoveries, null);

      string columnToSum = "Delta";
      string filterColumn1 = "Value Name";
      string valueToMatch1 = "3%-7%";
      string filterColumn2 = "";
      string valueToMatch2 = "";
      string filterColumn3 = "";
      string valueToMatch3 = "";

      filterColumn1 = MapColumnNames(filterColumn1);
      filterColumn2 = MapColumnNames(filterColumn2);
      filterColumn3 = MapColumnNames(filterColumn3);

      if (filterColumn2 == null || filterColumn2.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1);
      else if (filterColumn3 == null || filterColumn3.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1, filterColumn2, valueToMatch2);
      else
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1,
                                 filterColumn2, valueToMatch2, filterColumn3, valueToMatch3);

      double[] diff = new double[] { res };
      string[] labels = new string[] { "Average" };
      timer.Stop();
      MatchExpects(diff, labels, timer.Elapsed);
    }

    [Test, Smoke]
    public void Test_30_RecovRate()
    {
      Timer timer = new Timer();
      timer.Start();
      DataTable dt = null;
      double res = 0;

      bool calcHedge = false;
      string hedgeTenor = "";
      double[] defaultRecoveries = new double[] { 0.3 };
      dt = Sensitivities.Default(new SyntheticCDOPricer[] { cdoPricers_[1] }, "Pv", calcHedge, hedgeTenor, null, defaultRecoveries, null);

      string columnToSum = "Delta";
      string filterColumn1 = "Value Name";
      string valueToMatch1 = "3%-7%";
      string filterColumn2 = "";
      string valueToMatch2 = "";
      string filterColumn3 = "";
      string valueToMatch3 = "";

      filterColumn1 = MapColumnNames(filterColumn1);
      filterColumn2 = MapColumnNames(filterColumn2);
      filterColumn3 = MapColumnNames(filterColumn3);

      if (filterColumn2 == null || filterColumn2.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1);
      else if (filterColumn3 == null || filterColumn3.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1, filterColumn2, valueToMatch2);
      else
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1,
                                 filterColumn2, valueToMatch2, filterColumn3, valueToMatch3);

      double[] diff = new double[] { res };
      string[] labels = new string[] { "Average" };
      timer.Stop();
      MatchExpects(diff, labels, timer.Elapsed);
    }

    [Test, Smoke]
    public void Test_40_RecovRate()
    {
      Timer timer = new Timer();
      timer.Start();
      DataTable dt = null;
      double res = 0;

      bool calcHedge = false;
      string hedgeTenor = "";
      double[] defaultRecoveries = new double[] { 0.4 };
      dt = Sensitivities.Default(new SyntheticCDOPricer[] { cdoPricers_[1] }, "Pv", calcHedge, hedgeTenor, null, defaultRecoveries, null);

      string columnToSum = "Delta";
      string filterColumn1 = "Value Name";
      string valueToMatch1 = "3%-7%";
      string filterColumn2 = "";
      string valueToMatch2 = "";
      string filterColumn3 = "";
      string valueToMatch3 = "";

      filterColumn1 = MapColumnNames(filterColumn1);
      filterColumn2 = MapColumnNames(filterColumn2);
      filterColumn3 = MapColumnNames(filterColumn3);

      if (filterColumn2 == null || filterColumn2.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1);
      else if (filterColumn3 == null || filterColumn3.Length <= 0)
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1, filterColumn2, valueToMatch2);
      else
        res = Results.AverageIf(dt, columnToSum, filterColumn1, valueToMatch1,
                                 filterColumn2, valueToMatch2, filterColumn3, valueToMatch3);

      double[] diff = new double[] { res };
      string[] labels = new string[] { "Average" };
      timer.Stop();
      MatchExpects(diff, labels, timer.Elapsed);
    }

    #endregion Test

    #region Helpers

    private string MapColumnNames(string input)
    {
      string output = input;
      if (output != null)
      {
        if (output.Contains("Curve Name"))
          output = output.Replace("Curve Name", "Element");
        if (output.Contains("Value Name"))
          output = output.Replace("Value Name", "Pricer");
      }
      return output;
    }

    private void ExtractAllParameters()
    {
      asOf_ = Dt.FromStr(bd.DiscountData.AsOf, "%D");
      settle_ = Dt.FromStr(bd.Settle, "%D");
      effective_ = Dt.FromStr(bd.IndexData.Effective, "%D");
      ccy_ = bd.IndexData.Currency;
      dayCount_ = bd.IndexData.DayCount;
      freq_ = bd.IndexData.Frequency;
      calendar_ = bd.IndexData.Calendar;
      roll_ = bd.IndexData.Roll;

      if (bd.IndexData.TenorNames == null || bd.IndexData.TenorNames.Length == 0)
      {
        if (bd.IndexData.Maturities == null || bd.IndexData.TenorNames.Length == 0)
          throw new Exception("Index tenors and maturities can not both be null ");
        else
        {
          cdxMaturities_ = new Dt[bd.IndexData.Maturities.Length];
          for (int i = 0; i < bd.IndexData.Maturities.Length; ++i)
            cdxMaturities_[i] = Dt.FromStr(bd.IndexData.Maturities[i], "%D");
        }
      }
      else
      {
        if (bd.IndexData.Maturities != null &&
          (bd.IndexData.Maturities.Length != bd.IndexData.TenorNames.Length))
        {
          throw new Exception("Index tenors dimension must match index maturities dimension.");
        }
        cdxMaturities_ = new Dt[bd.IndexData.TenorNames.Length];
        for (int i = 0; i < bd.IndexData.TenorNames.Length; ++i)
        {
          Dt maturity = (bd.IndexData.Maturities == null || bd.IndexData.Maturities.Length == 0) ?
                        Dt.CDSMaturity(effective_, bd.IndexData.TenorNames[i]) : Dt.FromStr(bd.IndexData.Maturities[i], "%D");
          cdxMaturities_[i] = maturity;
        }
      }

      principals = bd.Principals;

      // Get the discount curve and survival curves
      discountCurve = bd.DiscountData.GetDiscountCurve();

      survCurves = bd.CreditData.GetSurvivalCurves(discountCurve);

      if (bd.CreditData.ScalingFactors != null)
      {
        scaledSurvCurves_ = new SurvivalCurve[survCurves.Length];
        for (int i = 0; i < survCurves.Length; ++i)
        {
          scaledSurvCurves_[i] = survCurves[i];
          scaledSurvCurves_[i].Name = scaledSurvCurves_[i] + "_Scaled";
        }
      }
      else
      {
        if (bd.CreditData.ScalingWeights == null || bd.CreditData.ScalingWeights.Length == 0)
        {
          scalingWeights_ = new double[survCurves.Length];
          for (int i = 0; i < survCurves.Length; ++i)
            scalingWeights_[i] = 1.0;
        }
        else
          scalingWeights_ = Array.ConvertAll<double, double>(bd.CreditData.ScalingWeights,
            delegate(double p) { return p != 1.0 ? 1.0 : p; });

        scalingMethods_ = bd.IndexData.ScalingMethods;
      }

      dealPremia_ = bd.IndexData.DealPremia;
      marketQuotes_ = bd.IndexData.Quotes;

      if (bd.GetCorrelationObject() == null)
      {
        corrObj_ = (CorrelationObject)(BasketData.GetBaseCorrelation(bd.Correlation));
      }
      else
        corrObj_ = (CorrelationObject)(bd.GetCorrelationObject());
      bd.QuadraturePoints = 128;
    }

    private void CreateCDOPricers()
    {
      cdoPricers_ = bd.GetSyntheticCDOPricers(corrObj_, discountCurve, survCurves);

      if (cdoPricers_ == null)
        throw new System.NullReferenceException("CDO Pricers not available");
      cdoNames_ = new string[cdoPricers_.Length];

      for (int i = 0; i < cdoNames_.Length; ++i)
      {
        cdoNames_[i] = cdoPricers_[i].CDO.Description;
        cdoPricers_[i].Notional = cdoPricers_[i].Notional / 10;// 1000000;

        double breakEvenFeeOrPremium =
          ((SyntheticCDO)cdoPricers_[i].Product).Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
      }
    }
    #endregion Helpers

    #region Data
    const double epsilon = 1.0E-9;

    //- Data files
    private string irDataFile_ = null;
    private string creditDataFile_ = null;
    private string basketDataFile_ = "JTD_At_Diff_RecoveryRates.xml";

    //- other params
    private string basketType_ = null;
    private string copulaData_ = null;
    private bool rescaleStrikes_ = true;

    Dt asOf_, settle_, effective_;
    Dt[] cdxMaturities_ = null;
    Currency ccy_;
    DayCount dayCount_;
    Frequency freq_;
    Calendar calendar_;
    BDConvention roll_;

    double[] dealPremia_;
    double[] marketQuotes_;
    double[] scalingWeights_;
    CDXScalingMethod[] scalingMethods_;

    double[] principals;
    CorrelationObject corrObj_ = null;

    protected SyntheticCDO[] cdos_ = null;
    protected SyntheticCDOPricer[] cdoPricers_ = null;
    protected CDX cdx_ = null;
    protected CDX[] cdx = null;
    protected CDXPricer[] cdxPricers_ = null;
    protected string[] cdoNames_ = null;
    protected DiscountCurve discountCurve = null;
    protected SurvivalCurve[] survCurves = null;
    protected SurvivalCurve[] scaledSurvCurves_ = null;

    private BasketData bd = null;
    #endregion // Data
  }
}