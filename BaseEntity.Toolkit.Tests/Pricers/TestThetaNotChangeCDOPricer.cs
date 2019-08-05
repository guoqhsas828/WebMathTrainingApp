//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// This test will check if theta changes state of CDO pricer and affect
  /// berakeven fee/premium computation, with default but not settled curves.
  /// It computes breakeven first, then calculate theta, and calculate the
  /// breakeven again to see if it changes.
  /// </summary>
  [TestFixture]
  public class TestThetaNotChangeCDOPricer : ToolkitTestBase
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

      return;
    }
    #endregion

    #region Test
    [Test, Smoke]
    public void Test_Theta_NoDefault()
    {
      CreateCDOPricers();
      for (int i = 0; i < cdoPricers_.Length; i++)
      {
        double BE0 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        double Theta = Sensitivities.Theta(cdoPricers_[i], null,
          Dt.Add(cdoPricers_[i].AsOf, 1), Dt.Add(cdoPricers_[i].Settle, 1), ThetaFlags.None, SensitivityRescaleStrikes.No);
        double BE1 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        Assert.AreEqual(BE0, BE1, 1e-5, "BE before and after Theta");
      }
    }

    [Test, Smoke]
    public void Test_Theta_WithOneDefault()
    {
      MakeDefaultCurve(1, -2);
      CreateCDOPricers();
      for (int i = 0; i < cdoPricers_.Length; i++)
      {
        double BE0 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        double Theta = Sensitivities.Theta(cdoPricers_[i], null,
          Dt.Add(cdoPricers_[i].AsOf, 1), Dt.Add(cdoPricers_[i].Settle, 1), ThetaFlags.None, SensitivityRescaleStrikes.No);
        double BE1 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        Assert.AreEqual(BE0, BE1, 1e-5, "BE before and after Theta, AsOf-2");
      }
      RestoreSurvivalCurves();

      MakeDefaultCurve(1, -1);
      CreateCDOPricers();
      for (int i = 0; i < cdoPricers_.Length; i++)
      {
        double BE0 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        double Theta = Sensitivities.Theta(cdoPricers_[i], null,
          Dt.Add(cdoPricers_[i].AsOf, 1), Dt.Add(cdoPricers_[i].Settle, 1), ThetaFlags.None, SensitivityRescaleStrikes.No);
        double BE1 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        Assert.AreEqual(BE0, BE1, 1e-5, "BE before and after Theta, AsOf-1");
      }
      RestoreSurvivalCurves();

      MakeDefaultCurve(1, 0);
      CreateCDOPricers();
      for (int i = 0; i < cdoPricers_.Length; i++)
      {
        double BE0 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        double Theta = Sensitivities.Theta(cdoPricers_[i], null,
          Dt.Add(cdoPricers_[i].AsOf, 1), Dt.Add(cdoPricers_[i].Settle, 1), ThetaFlags.None, SensitivityRescaleStrikes.No);
        double BE1 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        Assert.AreEqual(BE0, BE1, 1e-5, "BE before and after Theta, AsOf");
      }
      RestoreSurvivalCurves();
    }

    [Test, Smoke]
    public void Test_Theta_WithMoreDefaults()
    {
      MakeDefaultCurve(3, -2);
      CreateCDOPricers();
      for (int i = 0; i < cdoPricers_.Length; i++)
      {
        double BE0 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        double Theta = Sensitivities.Theta(cdoPricers_[i], null,
          Dt.Add(cdoPricers_[i].AsOf, 1), Dt.Add(cdoPricers_[i].Settle, 1), ThetaFlags.None, SensitivityRescaleStrikes.No);
        double BE1 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        Assert.AreEqual(BE0, BE1, 1e-5, "BE before and after Theta, AsOf-2");
      }
      RestoreSurvivalCurves();

      MakeDefaultCurve(3, -1);
      CreateCDOPricers();
      for (int i = 0; i < cdoPricers_.Length; i++)
      {
        double BE0 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        double Theta = Sensitivities.Theta(cdoPricers_[i], null,
          Dt.Add(cdoPricers_[i].AsOf, 1), Dt.Add(cdoPricers_[i].Settle, 1), ThetaFlags.None, SensitivityRescaleStrikes.No);
        double BE1 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        Assert.AreEqual(BE0, BE1, 1e-5, "BE before and after Theta, AsOf-1");
      }
      RestoreSurvivalCurves();

      MakeDefaultCurve(3, 0);
      CreateCDOPricers();
      for (int i = 0; i < cdoPricers_.Length; i++)
      {
        double BE0 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        double Theta = Sensitivities.Theta(cdoPricers_[i], null,
          Dt.Add(cdoPricers_[i].AsOf, 1), Dt.Add(cdoPricers_[i].Settle, 1), ThetaFlags.None, SensitivityRescaleStrikes.No);
        double BE1 = cdoPricers_[i].CDO.Attachment == 0 ?
          cdoPricers_[i].BreakEvenFee() : cdoPricers_[i].BreakEvenPremium();
        Assert.AreEqual(BE0, BE1, 1e-5, "BE before and after Theta, AsOf");
      }
      RestoreSurvivalCurves();
    }
    #endregion Test

    #region Helpers
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

    private void MakeDefaultCurve(int n, int daysFromAsOf)
    {
      savedSurvCurveList.Clear();
      for (int i = 0; i < n; i++)
      {
        savedSurvCurveList.Add((SurvivalCurve)survCurves[i].CloneWithCalibrator());
        survCurves[i].SetDefaulted(Dt.Add(asOf_, daysFromAsOf), true);
        // No need to set the default settle date since we need default but unsettled
        // survCurves[i].SurvivalCalibrator.RecoveryCurve.Set_JumpDate(dfltSettle);
      }
      return;
    }

    private void RestoreSurvivalCurves()
    {
      if (savedSurvCurveList.Count > 0)
        for (int i = 0; i < savedSurvCurveList.Count; i++)
          survCurves[i] = savedSurvCurveList[i];
      return;
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
    protected List<SurvivalCurve> savedSurvCurveList = new List<SurvivalCurve>();

    private BasketData bd = null;
    #endregion // Data
  }
}