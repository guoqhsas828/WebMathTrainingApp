//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test no error due to first premium date > last premium date
  /// in risky duration of CDS
  ///
  /// 8/9/2007    9/9/2007            3/23/2009  6/8/2009
  ///----|----------|---------------------|---------|
  /// Effective  FirstPrem AsOf      Maturity
  ///
  /// For risky duration we call GenerateCashflow01 to get a cashflow with new effective date set to settle date(3/24/2009)
  /// because the old effective(8/9/2007) is earlier than settle; The FirstPrem thus calculated might be later than the maturity,
  /// and if so, FirstPrem is set to be maturity.
  /// The problem in the sheet is the CDS.LastPrem.It is ealier than FirstPrem when we construct the cashflow for risky duration. 
  /// So similarly the solution is to compare FirstPrem and LastPrem and if LastPrem<FirstPrem, set it to FirstPrem.

  /// See case 15460
  /// </summary>
  [TestFixture, Smoke]
  public class TestCDSFirstLastPremiumError : ToolkitTestBase
  {
    #region Data
    private new readonly ToolkitConfigSettings settings_ = ToolkitConfigurator.Settings;

    private Dt asOf_ = new Dt(23, 3, 2009);
    private Dt settle_ = new Dt(24, 3, 2009);
    private Dt effective_ = new Dt(9, 8, 2007);
    private Dt maturity_ = new Dt(8, 6, 2009);
    
    private double fee_ = 0.0;
    private Frequency freq_ = Toolkit.Base.Frequency.Quarterly;
    private DayCount mmDaycount_ = Toolkit.Base.DayCount.Actual360;
    private string[] mmTenors_ = new string[] {"6 Month", "1 Year"};
    private Dt[] mmDates_ = new Dt[2];
    private double[] mmRates_ = new double[] {0.0558938, 0.0569313};
    private DayCount swapDaycount_ = Toolkit.Base.DayCount.Thirty360;
    private Frequency swapFreq_ = Toolkit.Base.Frequency.SemiAnnual;
    private string[] swapTenors_ = new string[]{"2 Year","3 Year","5 Year", "7 Year", "10 Year"};
    private double[] swapRates_ = new double[] {0.056245, 0.05616, 0.05648, 0.05683, 0.057295};
    private Dt[] swapDates_ = new Dt[5];
    private DiscountCurve discountCurve_ = null;

    private double recovery_ = 0.4;
    private BDConvention cdsRoll_ = BDConvention.Following;
    private DayCount cdsDaycount_ = Toolkit.Base.DayCount.Actual360;
    private Frequency cdsFreq_ = Toolkit.Base.Frequency.Quarterly;
    private Calendar cdsCal_ = Toolkit.Base.Calendar.NYB;
    private double[] cdsQuotes_ = new double[] {120, 140, 160, 180, 200, 220, 240};
    private SurvivalCurve survivalCurve_ = null;

    private Dt firstPrem = new Dt(9,9,2007);
    private double premium_ = 200.0;
    private CDS cds_;

    private double notional_ = 10000000;
    private CDSCashflowPricer cdsPricer_;


    #endregion Data

    #region Tests

    /// <summary>
    ///  Test that the risky duration will not fail by checking
    ///  first premium date > last premium date 
    /// </summary>
    //[ExpectedException(typeof(System.ApplicationException), "")]
    [Test, Smoke]
    public void TestRiskyDurationSuccess()
    {
      BuildLiborCurve();
      BuildSurvivalCurve();
      BuildCDS();
      BuildCDSPricer();

      double riskyDuration = cdsPricer_.RiskyDuration();

      return;
    }    

    #endregion Tests

    #region helpers
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
          ((Note) discountCurve_.Tenors[last].Product).BDConvention = BDConvention.None;
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

    private void BuildSurvivalCurve()
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
      survivalCurve_ = SurvivalCurve.FitCDSQuotes(asOf_, Toolkit.Base.Currency.USD, "None", cdsDaycount_, cdsFreq_,
                                                  cdsRoll_, cdsCal_, InterpMethod.Weighted, ExtrapMethod.Const,
                                                  NegSPTreatment.Allow, discountCurve_, tenorNames, tenorDates, 
                                                  null, cdsQuotes_, new double[] {recovery_}, 0, false, null);
      survivalCurve_.Name = "Survival Curve";
      return;
    }

    private void BuildCDS()
    {
      cds_ = new CDS(effective_, maturity_, Currency.USD, premium_ / 10000.0, cdsDaycount_, cdsFreq_, cdsRoll_, cdsCal_);
      cds_.AccruedOnDefault = true;
      cds_.CdsType = CdsType.Unfunded;      
      cds_.FirstPrem = firstPrem;
      cds_.LastPrem = Schedule.DefaultLastCouponDate(firstPrem, cdsFreq_, maturity_, false);                       
      cds_.Description = "CDS";
      cds_.Validate();
      return ;
    }

    private void BuildCDSPricer()
    {
      DiscountCurve reference = null;
      double correlation = 0.0;
      cdsPricer_ = new CDSCashflowPricer(cds_, asOf_, settle_, discountCurve_,
                                         reference, survivalCurve_, null, correlation, 0, TimeUnit.None);
      cdsPricer_.RecoveryCurve = new RecoveryCurve(asOf_, recovery_);
      cdsPricer_.RecoveryCurve.JumpDate= survivalCurve_.SurvivalCalibrator.RecoveryCurve.DefaultSettlementDate;
      cdsPricer_.Notional = notional_;
      cdsPricer_.Validate();
    }
    #endregion helpers
  }
}
