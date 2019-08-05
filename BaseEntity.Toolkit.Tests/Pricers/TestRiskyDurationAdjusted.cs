//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test the risky durations of CDX and CDO that should be  
  /// adjusted whenever there're already-occured defaults
  /// </summary>
  [TestFixture]
  public class TestRiskyDurationAdjusted : SensitivityTest
  {
    #region data
    private new readonly ToolkitConfigSettings settings_ = ToolkitConfigurator.Settings;

    private Dt asOf_ = new Dt(23, 3, 2009);
    private Dt settle_ = new Dt(24, 3, 2009);
    private Dt effective_ = new Dt(9, 8, 2007);
    private Dt maturity_ = new Dt(8, 6, 2012);

    private DayCount mmDaycount_ = Toolkit.Base.DayCount.Actual360;
    private string[] mmTenors_ = new string[] { "6 Month", "1 Year" };
    private Dt[] mmDates_ = new Dt[2];
    private double[] mmRates_ = new double[] { 0.0558938, 0.0569313 };
    private DayCount swapDaycount_ = Toolkit.Base.DayCount.Thirty360;
    private Frequency swapFreq_ = Toolkit.Base.Frequency.SemiAnnual;
    private string[] swapTenors_ = new string[] { "2 Year", "3 Year", "5 Year", "7 Year", "10 Year" };
    private double[] swapRates_ = new double[] { 0.056245, 0.05616, 0.05648, 0.05683, 0.057295 };
    private Dt[] swapDates_ = new Dt[5];
    private DiscountCurve discountCurve_ = null;

    private double recovery_ = 0.4;
    private BDConvention cdsRoll_ = BDConvention.Following;
    private DayCount cdsDaycount_ = Toolkit.Base.DayCount.Actual360;
    private Frequency cdsFreq_ = Toolkit.Base.Frequency.Quarterly;
    private Calendar cdsCal_ = Toolkit.Base.Calendar.NYB;
    private double[] cdsQuotes_ = new double[] { 120, 140, 160, 180, 200, 220, 240 };
    private SurvivalCurve[] survivalCurves_ = new SurvivalCurve[125];
    private double cdoPremium_ = 250;
    private DayCount cdoDaycount_ = Toolkit.Base.DayCount.Actual360;
    private Frequency cdoFreq_ = Frequency.Quarterly;
    private BDConvention cdoRoll_ = BDConvention.Following;
    private Calendar cdoCal_ = Calendar.NYB;
    private CdoType CdoType_ = CdoType.Unfunded;
    private double attach_ = 0;
    private double detach_ = 0.03;
    private SyntheticCDO cdo_;
    private double cdoNotional_ = 10000000;
    private double[] principals = new double[125];
    private double corr_ = 0.5;
    private SyntheticCDOPricer cdoPricer_;
    private CdxType cdxType_ = CdxType.Unfunded;
    private double cdxPremium_ = 250;
    private double marketQuote_ = 200;
    private double cdxNotional_ = 10000000; 
    private DayCount cdxDaycount_ = DayCount.Actual360;
    private Frequency cdxFreq_ = Frequency.Quarterly;
    private BDConvention cdxRoll_ = BDConvention.Following;
    private Calendar cdxCal_ = Calendar.NYB;
    private CDX cdx_, lcdx_;
    private CDXPricer cdxPricer_, lcdxPricer_;

    #endregion data

    #region tests
    [Test, Smoke]
    public void TestCDORiskyDurationWithoutDefaults()
    {
      BuildLiborCurve();
      BuildSurvivalCurves();
      BuildCDO();
      BuildCDOPricer();
      cdoPricer_.AdjustDurationForRemainingNotional = false;
      double durationFalse = cdoPricer_.RiskyDuration();
      cdoPricer_.AdjustDurationForRemainingNotional = true;
      double durationTrue = cdoPricer_.RiskyDuration();
      Assert.AreEqual(durationFalse, durationTrue, 1e-17);
    }

    [Test, Smoke]
    public void TestCDORiskyDurationWithDefaults()
    {
      BuildLiborCurve();
      BuildSurvivalCurves();
      MakeDefaults();
      BuildCDO();
      BuildCDOPricer();
      cdoPricer_.AdjustDurationForRemainingNotional = false;
      double durationFalse = cdoPricer_.RiskyDuration();
      cdoPricer_.AdjustDurationForRemainingNotional = true;
      double durationTrue = cdoPricer_.RiskyDuration();
      Assert.AreEqual(durationFalse, 
        durationTrue * cdoPricer_.Notional / cdoPricer_.EffectiveNotional, 1e-15);
    }

    [Test, Smoke]
    public void TestCDXRiskyDurationWithoutDefaults()
    {
      BuildLiborCurve();
      BuildSurvivalCurves();
      BuildCDX();
      BuildCDXPricer();
      cdxPricer_.AdjustDurationForRemainingNotional = false;
      double durationFalse = cdxPricer_.RiskyDuration();
      cdxPricer_.AdjustDurationForRemainingNotional = true;
      double durationTrue = cdxPricer_.RiskyDuration();
      Assert.AreEqual(durationFalse, durationTrue, 1e-17);
    }
    
    [Test, Smoke]
    public void TestCDXRiskyDurationWithDefaults()
    {
      BuildLiborCurve();
      BuildSurvivalCurves();
      MakeDefaults();
      BuildCDX();
      BuildCDXPricer();
      cdxPricer_.AdjustDurationForRemainingNotional = false;
      double durationFalse = cdxPricer_.RiskyDuration();
      cdxPricer_.AdjustDurationForRemainingNotional = true;
      double durationTrue = cdxPricer_.RiskyDuration();
      Assert.AreEqual(durationFalse,
        durationTrue * cdxPricer_.Notional / cdxPricer_.CurrentNotional, 1e-15);
    }
    
    [Test, Smoke]
    public void TestLCDXRiskyDurationWithoutDefaults()
    {
      BuildLiborCurve();
      BuildSurvivalCurves();
      BuildLCDX();
      BuildLCDXPricer();
      lcdxPricer_.AdjustDurationForRemainingNotional = false;
      double durationFalse = lcdxPricer_.RiskyDuration();
      lcdxPricer_.AdjustDurationForRemainingNotional = true;
      double durationTrue = lcdxPricer_.RiskyDuration();
      Assert.AreEqual(durationFalse, durationTrue, 1e-15);
    }
    
    [Test, Smoke]
    public void TestLCDXRiskyDurationWithDefaults()
    {
      BuildLiborCurve();
      BuildSurvivalCurves();
      MakeDefaults();
      BuildLCDX();
      BuildLCDXPricer();
      lcdxPricer_.AdjustDurationForRemainingNotional = false;
      double durationFalse = lcdxPricer_.RiskyDuration();
      lcdxPricer_.AdjustDurationForRemainingNotional = true;
      double durationTrue = lcdxPricer_.RiskyDuration();
      Assert.AreEqual(durationFalse,
        durationTrue * lcdxPricer_.Notional / lcdxPricer_.CurrentNotional, 1e-15);
    }
    #endregion tests

    #region helpers

    private void BuildCDX()
    {
      cdx_ = new CDX(effective_, maturity_, Currency.USD, 
        cdxPremium_/10000.0, cdxDaycount_, cdxFreq_, cdxRoll_, cdxCal_);
      cdx_.Funded = false;
      cdx_.Description = "cdx";
      return;
    }

    private void BuildCDXPricer()
    {
      cdxPricer_ = new CDXPricer(cdx_, asOf_, settle_, discountCurve_, null, survivalCurves_, marketQuote_/10000.0);
      cdxPricer_.QuotingConvention = QuotingConvention.CreditSpread;               
      cdxPricer_.Notional = cdxNotional_;
      return;
    }

    private void BuildLCDX()
    {
      lcdx_ = new LCDX(effective_, maturity_, Currency.USD,
        cdxPremium_ / 10000.0, cdxDaycount_, cdxFreq_, cdxRoll_, cdxCal_);
      lcdx_.Funded = false;
      lcdx_.Description = "lcdx";
      return;
    }

    private void BuildLCDXPricer()
    {
      lcdxPricer_ = new CDXPricer(lcdx_, asOf_, settle_, discountCurve_, null, survivalCurves_, marketQuote_ / 10000.0);
      lcdxPricer_.QuotingConvention = QuotingConvention.CreditSpread;
      lcdxPricer_.Notional = cdxNotional_;
      return;
    }

    private void BuildCDO()
    {
      cdo_ = new SyntheticCDO(effective_, maturity_, Toolkit.Base.Currency.USD, cdoPremium_/10000.0,
                              cdoDaycount_, cdoFreq_, cdoRoll_, cdoCal_);
      cdo_.Attachment = attach_;
      cdo_.Detachment = detach_;
      cdo_.CdoType = CdoType_;
      cdo_.FixedRecovery = false;
      cdo_.Description = "CDO";
      cdo_.Validate();
      return;
    }

    private void BuildCDOPricer()
    {
      for (int i = 0; i < principals.Length; i++)
        principals[i] = 1.0;

      Copula copula = new Copula(CopulaType.Gauss, 2, 2);
      Dt portfolioStart = new Dt();
      string[] names = new string[survivalCurves_.Length];
      for (int i = 0; i < survivalCurves_.Length; i++)
        if (null != survivalCurves_[i])
          names[i] = survivalCurves_[i].Name;
      CorrelationObject correlation = new SingleFactorCorrelation(names, Math.Sqrt(corr_));

      cdoPricer_ = BasketPricerFactory.CDOPricerHeterogeneous(
        new SyntheticCDO[] {cdo_}, portfolioStart, asOf_, settle_, discountCurve_, null, survivalCurves_, principals,
        copula, correlation, 3, TimeUnit.Months, 30, 0, new double[]{cdoNotional_}, false, null)[0];
      return;
    }

    private void BuildLiborCurve()
    {
      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf_, asOf_);
      calibrator.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);
      calibrator.SwapCalibrationMethod = SwapCalibrationMethod.Extrap;
      discountCurve_ = new DiscountCurve(calibrator);
      discountCurve_.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      discountCurve_.Ccy = Toolkit.Base.Currency.USD;
      discountCurve_.Category = "None";
      discountCurve_.Name = "Libor Curve";

      // Add MM rates
      for (int i = 0; i < mmTenors_.Length; i++)
        mmDates_[i] = String.IsNullOrEmpty(mmTenors_[i]) ? Dt.Empty : Dt.Add(asOf_, mmTenors_[i]);
      for (int i = 0; i < mmTenors_.Length; i++)
      {
        if (mmRates_[i] > 0.0)
        {
          int last = discountCurve_.Tenors.Count;
          discountCurve_.AddMoneyMarket(mmTenors_[i], mmDates_[i], mmRates_[i], mmDaycount_);
          ((Note)discountCurve_.Tenors[last].Product).BDConvention = BDConvention.None;
        }
      }
      // Add swap rates
      for (int i = 0; i < swapTenors_.Length; i++)
        swapDates_[i] = String.IsNullOrEmpty(swapTenors_[i]) ? Dt.Empty : Dt.Add(asOf_, swapTenors_[i]);
      for (int i = 0; i < swapTenors_.Length; i++)
        if (swapRates_[i] > 0.0)
          discountCurve_.AddSwap(swapTenors_[i], swapDates_[i], swapRates_[i], swapDaycount_,
                                 swapFreq_, BDConvention.None, Calendar.None);
      discountCurve_.Fit();
      return;
    }

    private void BuildSurvivalCurves()
    {
      string[] tenorNames = new string[mmTenors_.Length + swapTenors_.Length];
      Dt[] tenorDates = new Dt[mmTenors_.Length + swapTenors_.Length];
      for (int i = 0; i < mmTenors_.Length; i++)
      {
        tenorNames[i] = mmTenors_[i];
        tenorDates[i] = mmDates_[i];
      }
      for (int i = 0; i < swapTenors_.Length; i++)
      {
        tenorNames[mmTenors_.Length + i] = swapTenors_[i];
        tenorDates[mmTenors_.Length + i] = swapDates_[i];
      }
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        survivalCurves_[i] = SurvivalCurve.FitCDSQuotes(asOf_, Toolkit.Base.Currency.USD, "None", cdsDaycount_, cdsFreq_,
                                                        cdsRoll_, cdsCal_, InterpMethod.Weighted, ExtrapMethod.Const,
                                                        NegSPTreatment.Allow, discountCurve_, tenorNames, tenorDates,
                                                        null, cdsQuotes_, new double[] {recovery_}, 0, false, null);
        survivalCurves_[i].Name = String.Format("{0}{1}", "Surv Curve", i + 1);
      }
      return;
    }

    private void MakeDefaults()
    {
      int size = survivalCurves_.Length;
      for (int i = 1; i <= 2; ++i)
        survivalCurves_[size - i].SetDefaulted(Dt.Add(settle_, -100), true);
      return;
    }

    #endregion helpers

  }
}