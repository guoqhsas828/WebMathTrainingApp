//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// This will test that the Fee leg and protection leg should sum up to full pv for a
  /// CDS labeled with PayRecoveryAtMaturity = true;
  /// </summary>
  [TestFixture, Smoke]
  public class TestCDSPayRecoveryAtMaturity : ToolkitTestBase
  {
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {

      // Set up the IR curve 
      BuildDiscountCurve();
      
      // Set up the credit curve
      BuildCreditCurve();

      // Build the CDS with PayRecoveryAtMaturity = true
      BuildCDS();

      // Build the CDS pricer
      BuildCDSPricer();

      return;
    }

    #endregion setup

    #region tests

    [Test, Smoke]
    public void Test_PayRecoveryAtMaturityTRUE()
    {
      cds_.PayRecoveryAtMaturity = true;
      double feePv = cdsPricer_.FeePv();
      double protectionPv = cdsPricer_.ProtectionPv();
      double pv = cdsPricer_.Pv();
      Assert.AreEqual(pv, feePv + protectionPv, "Pv != FeePv + ProtectionPv");
    }

    [Test, Smoke]
    public void Test_PayRecoveryAtMaturityFALSE()
    {
      cds_.PayRecoveryAtMaturity = false;
      double feePv = cdsPricer_.FeePv();
      double protectionPv = cdsPricer_.ProtectionPv();
      double pv = cdsPricer_.Pv();
      Assert.AreEqual(pv, feePv + protectionPv,
        (1+Math.Abs(pv))*1E-14, "Pv != FeePv + ProtectionPv");
    }

    #endregion tests

    #region helpers
    // Build discount curve
    void BuildDiscountCurve()
    {
      // Get the tenor dates from tenors      
      mmDates_ = Array.ConvertAll(mmTenors_, s => Dt.Add(asOf_, s));
      swapDates_ = Array.ConvertAll(swapTenors_, s => Dt.Add(asOf_, s));

      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf_, asOf_);
      calibrator.SwapInterp = InterpFactory.FromMethod(swapInterp_, swapExtrap_);
      calibrator.SwapCalibrationMethod = SwapCalibrationMethod.Extrap;

      discountCurve_ = new DiscountCurve(calibrator);
      discountCurve_.Interp = InterpFactory.FromMethod(mmInterp, mmExtrap);
      discountCurve_.Ccy = currency_;
      discountCurve_.Category = "None";      

      // Add MM rates   
      for (int i = 0; i < mmTenors_.Length; i++)
      {
        int last = discountCurve_.Tenors.Count;
        discountCurve_.AddMoneyMarket(mmTenors_[i], mmDates_[i], mmrates_[i], mmDaycount_);
        ((Note)discountCurve_.Tenors[last].Product).BDConvention = BDConvention.None;
      }

      // Add swap rates
      for (int i = 0; i < swapTenors_.Length; i++)
      {
        discountCurve_.AddSwap(swapTenors_[i], swapDates_[i], swapRates_[i], swapDayCount_,
                      swapFreq_, BDConvention.None, Calendar.None);
      }
      discountCurve_.Fit();
      return;
    }

    // Build credit curve
    void BuildCreditCurve()
    {
      SurvivalCurveParams par = new SurvivalCurveParams(
        cdsContractType_, cdsQuoteType_, cdsCoupon_, cdsDaycount_, cdsFreq_, roll_, 
        cal_, InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow, true);

      SurvivalCurve refinanceCurve = null;
      List<string> tenorNames = new List<string>();
      tenorNames.AddRange(mmTenors_);
      tenorNames.AddRange(swapTenors_);
      survivalCurve_ = SurvivalCurve.FitCDSQuotes(
        "CreditCurve", asOf_, settle_, currency_, "none", true, cdsQuoteType_, cdsCoupon_,
        par.ToSurvivalCurveParameters(), discountCurve_, tenorNames.ToArray(), null, cdsQuotes_,
        new[] {recoveryRate_}, 0, null, refinanceCurve, 0, 0.4, null, true);

      return;
    }

    // Build CDS with PayRecoveryAtMaturity = true
    void BuildCDS()
    {
      cds_ = new CDS(
        effectiveDate_, cdsMaturity_, currency_, cdsPremium_ / 10000.0, cdsDaycount_, cdsFreq_, roll_, cal_);      
      cds_.AccruedOnDefault = true;
      cds_.CdsType = cdsType_;
      cds_.FirstPrem = firstCouponDate_;
      cds_.LastPrem = Dt.Empty;
      cds_.Fee = fee_;
      cds_.FeeSettle = feeSettle_;
      cds_.Validate();
      return;
    }

    // Build CDS pricer
    void BuildCDSPricer()
    {
      DiscountCurve reference = null;      
      SurvivalCurve counterpartySurvivalCurve = survivalCurve_.SurvivalCalibrator.CounterpartyCurve;

      cdsPricer_ = new CDSCashflowPricer(cds_, asOf_, settle_, discountCurve_, reference, survivalCurve_,
        counterpartySurvivalCurve, 0, 0, TimeUnit.None);
      cdsPricer_.Notional = notional_;
      cdsPricer_.Validate();
      return;
    }

    #endregion helpers

    #region data

    private DiscountCurve discountCurve_ = null;
    private SurvivalCurve survivalCurve_ = null;
    private CDS cds_ = null;
    private CDSCashflowPricer cdsPricer_ = null;

    private Dt asOf_ = new Dt(11, 3, 2009);
    private Dt settle_ = new Dt(12, 3, 2009);

    #region discount curve data
    private Currency currency_ = Currency.USD;
    private string[] mmTenors_ = new string[] {"6 Month", "1 Year"};
    private double[] mmrates_ = new double[]{0.0177, 0.0208};
    private Dt[] mmDates_ = null;
    private DayCount mmDaycount_ = DayCount.Actual360;
    private InterpMethod mmInterp = InterpMethod.Weighted;
    private ExtrapMethod mmExtrap = ExtrapMethod.Const;
    private string[] swapTenors_ = new string[]{"2 Year", "3 Year", "5 Year", "7 Year", "10 Year"};
    private double[] swapRates_ = new double[]{0.0165, 0.0201,0.0252, 0.0282, 0.0309};
    private Dt[] swapDates_ = null;
    private Frequency swapFreq_ = Frequency.SemiAnnual;
    private DayCount swapDayCount_ = DayCount.Thirty360;
    private InterpMethod swapInterp_ = InterpMethod.Cubic;
    private ExtrapMethod swapExtrap_ = ExtrapMethod.Const;
    #endregion discoutn data

    #region survival curve data
    private double recoveryRate_ = 0.4;
    private double[] cdsQuotes_ = new double[]{400, 420, 480, 550, 625, 700, 790};
    private Calendar cal_ = Calendar.NYB;
    private BDConvention roll_ = BDConvention.Following;
    private Frequency cdsFreq_ = Frequency.Quarterly;
    private DayCount cdsDaycount_ = DayCount.Actual360;
    private string cdsContractType_ = "SNAC";
    private double cdsCoupon_ = 500;
    private CDSQuoteType cdsQuoteType_ = CDSQuoteType.ParSpread;  
    #endregion survival curve data

    #region CDS data
    private Dt effectiveDate_ = new Dt(22, 12, 2008);
    private Dt cdsMaturity_ = new Dt(20, 3, 2014);
    private double cdsPremium_ = 500;
    private double fee_ = 0.0482;
    private Dt feeSettle_ = new Dt(16, 3, 2009);
    private Dt firstCouponDate_ = new Dt(20, 3, 2009);
    private CdsType cdsType_ = CdsType.Unfunded;
    private double notional_ = 10000000;

    #endregion CDS data
    
    #endregion data
  }
}