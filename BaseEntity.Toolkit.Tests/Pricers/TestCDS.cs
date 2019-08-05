//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test CDS Pricer functions based on external creadit data
  /// </summary>
  [TestFixture("CDX.NA.HY.7")]
  [TestFixture("CDX.NA.HY.7 Senstivities2")]
  [TestFixture("TestCDSCashflowPricer_FCDS_CDX.NA.HY.7")]
  [TestFixture("TestCDSPricer_FeeAfterSettle_CDX.NA.HY.7")]
  [TestFixture("TestCDSPricer_FeeOnSettle_CDX.NA.HY.7")]
  [Smoke]
  public class TestCDS : SensitivityTest
  {

   public TestCDS(string name) : base(name)
   {}

    #region PricingMethods
    [Test, Smoke, Category("PricingMethods")]
    public void Accrued()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).Accrued();
        });
    }

    // This tests the accrual amount changes with 
    // last reset rate, for funded floating CDS
    [Test, Smoke, Category("PricingMethods")]
    public void AccruedFundedFloating()
    {
      // Test one pricer is enough
      double lastRate = 0.02;
      ICDSPricer pricer = (ICDSPricer)cdsPricers_[0].Clone();
      pricer.CDS.CdsType = CdsType.FundedFloating;      
      pricer.RateResets.Add(new RateReset(Dt.Add(cdsPricers_[0].AsOf, -90), lastRate));      
      pricer.Reset();
      double accrual_1 = pricer.Accrued();

      pricer.RateResets.Clear();
      pricer.RateResets.Add(new RateReset(Dt.Add(cdsPricers_[0].AsOf, -90), 2*lastRate));
      double accrual_2 = pricer.Accrued();

      Assert.AreEqual(accrual_1 * 2, accrual_2, 1e-5, "Accrual failed for FundedFloating CDS");

      pricer.RateResets.Clear();
      pricer.RateResets.Add(new RateReset(Dt.Add(cdsPricers_[0].AsOf, -90), 3 * lastRate));
      double accrual_3 = pricer.Accrued();

      Assert.AreEqual(accrual_1 * 3, accrual_3, 1e-5, "Accrual failed for FundedFloating CDS");
    }

    // This tests the accrual amount throws out an exception 
    // when the accrual start date is ealier than the first rate reset date
    [Test, Smoke, Category("PricingMethods")]
    public void AccruedFundedFloatingException()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        // Test one pricer is enough
        double lastRate = 0.02;
        ICDSPricer pricer = (ICDSPricer) cdsPricers_[0].Clone();
        pricer.CDS.CdsType = CdsType.FundedFloating;
        pricer.RateResets.Add(new RateReset(cdsPricers_[0].AsOf, lastRate));
        pricer.Reset();
        double accrual = pricer.Accrued();
      });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void AccrualDays()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).AccrualDays();
        });
    }

   [Test, Smoke, Category("PricingMethods")]
   public void AccuedPaidOnDefaultFundedFixed()
   {
     AccruedPaidOnDefault(CdsType.FundedFixed);
   }

    [Test, Smoke, Category("PricingMethods")]
    public void AccuedPaidOnDefaultFundedFloating()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        AccruedPaidOnDefault(CdsType.FundedFloating);
      });
    }

    [Test, Smoke, Category("PricingMethods")]
   public void AccuedPaidOnDefaultUnfunded()
   {
     AccruedPaidOnDefault(CdsType.Unfunded);
   }

   [Test, Smoke, Category("PricingMethods")]
    public void BreakEvenPremium()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).BreakEvenPremium();
        });
    }

   [Test, Smoke, Category("PricingMethod")]
    public void BreakevenPremiumFunded()
    {
      double[] bep_Unfunded = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).BreakEvenPremium(); });

      CdsType savedCdsType = ((ICDSPricer)cdsPricers_[0]).CDS.CdsType;
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.CdsType = CdsType.FundedFixed;
        ((ICDSPricer)cdsPricers_[i]).Reset();
      }
      double[] bep_FundedFixed = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).BreakEvenPremium(); });
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.CdsType = savedCdsType;
        ((ICDSPricer)cdsPricers_[i]).Reset();
      }
      for(int i = 0; i < cdsPricers_.Length; i++)
        Assert.AreEqual(bep_Unfunded[i], bep_FundedFixed[i], 
          1e-6, "FundFixed BEP for CDSpricer " + i.ToString());

      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.CdsType = CdsType.FundedFloating;
        ((ICDSPricer)cdsPricers_[i]).Reset();
      }
      double[] bep_FundedFloating = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).BreakEvenPremium(); });
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.CdsType = savedCdsType;
        ((ICDSPricer)cdsPricers_[i]).Reset();
      }
      for(int i = 0; i < cdsPricers_.Length; i++)
        Assert.AreEqual(bep_Unfunded[i], bep_FundedFloating[i], 
          1e-6, "FundedFloating BEP for CDSPricer " + i.ToString());
    }

   [Test, Smoke, Category("PricingMethod")]
    public void BreakevenFeeFunded()
    {
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        if (((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle < cdsPricers_[i].AsOf)
        {
          ((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle = Dt.Add(cdsPricers_[i].AsOf, 3);
          cdsPricers_[i].Reset();
        }
      }
      double[] bef_Unfunded = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).BreakEvenFee(System.Double.NaN); });

      CdsType savedCdsType = ((ICDSPricer)cdsPricers_[0]).CDS.CdsType;
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.CdsType = CdsType.FundedFixed;
        ((ICDSPricer)cdsPricers_[i]).Reset();
      }
      double[] bef_FundedFixed = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).BreakEvenFee(System.Double.NaN); });
      
      for (int i = 0; i < cdsPricers_.Length; i++)
        Assert.AreEqual(bef_Unfunded[i], bef_FundedFixed[i],
          1e-6, "FundFixed BEP for CDSpricer " + i.ToString());

      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.CdsType = CdsType.FundedFloating;
        ((ICDSPricer)cdsPricers_[i]).Reset();
      }
      double[] bef_FundedFloating = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).BreakEvenFee(System.Double.NaN); });
      for (int i = 0; i < cdsPricers_.Length; i++)
        Assert.AreEqual(bef_Unfunded[i], bef_FundedFloating[i],
          1e-6, "FundedFloating BEP for CDSPricer " + i.ToString());
      Initialize();
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FwdPremium()
    {
      Dt forwardSettle =
        ForwarddSettleDate != 0 ? new Dt(ForwarddSettleDate) : Dt.Add(asOf_, "3 Months");
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).FwdPremium(forwardSettle);
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FwdPremium01()
    {
      Dt forwardSettle =
        ForwarddSettleDate != 0 ? new Dt(ForwarddSettleDate) : Dt.Add(asOf_, "3 Months");
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).FwdPremium01(forwardSettle);
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void Premium01()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).Premium01();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void RiskyDuration()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).RiskyDuration();
        });
    }

    // This test the equality of risky duration calculated 
    // from survival curve and CDS prcier (See case 17782)
    [Test, Smoke, Category("PricingMethods")]
    public void RiskyDuration_SurvCurve_CDSPricer()
    {
      // Build IR curve
      Dt pricingDate = new Dt(31, 7, 2008);
      DayCount mmDayCount = DayCount.Actual360;
      DayCount swapDayCount = DayCount.Thirty360;
      Frequency swapFreq = Frequency.SemiAnnual;
      double caVol = 0.012;
      string[] mmTenors = new string[] { "1 D", "1 W", "2 W", "1 M", "2 M", "3 M", "4 M", "5 M", "6 M", "9 M", "1 Y" };
      Dt[] mmTenorDates = new Dt[]{
        new Dt( 1, 8, 2008), new Dt( 7,  8, 2008), new Dt(14,  8, 2008), new Dt(31,  8, 2008), 
        new Dt(30, 9, 2008), new Dt(31, 10, 2008), new Dt(30, 11, 2008), new Dt(31, 12, 2008), 
        new Dt(31, 1, 2009), new Dt(30,  4, 2009), new Dt(31,  7, 2009)
      };
      double[] mmRates = new double[]{
        0.0536875, 0.053225, 0.05325, 0.0533438, 0.0542, 0.0548063,	
        0.0551688, 0.05555, 0.0558938, 0.0564938, 0.0569313
      };
      string[] swapTenors = new string[] { "2 Yr", "3 Yr", "4 Yr", "5 Yr", "6 Yr", "7 Yr", "8 Yr", "9 Yr", "10 Yr" };
      Dt[] swapTenorDates = new Dt[]{
        new Dt(31,7,2010),new Dt(31,7,2011),new Dt(31,7,2012),new Dt(31,7,2013),new Dt(31,7,2014),
        new Dt(31,7,2015),new Dt(31,7,2016),new Dt(31,7,2017),new Dt(31,7,2018)
      };
      double[] swapRates = new double[]{
        0.056245, 0.05616, 0.0563, 0.05648, 0.05665, 0.05683, 0.056995, 0.05713, 0.057295
      };
      BaseEntity.Toolkit.Numerics.InterpMethod interpMethod = BaseEntity.Toolkit.Numerics.InterpMethod.Weighted;
      BaseEntity.Toolkit.Numerics.ExtrapMethod extrapMethod = BaseEntity.Toolkit.Numerics.ExtrapMethod.Const;
      BaseEntity.Toolkit.Numerics.InterpMethod swapInterp = BaseEntity.Toolkit.Numerics.InterpMethod.Cubic;
      BaseEntity.Toolkit.Numerics.ExtrapMethod swapExtrap = BaseEntity.Toolkit.Numerics.ExtrapMethod.Const;
      DiscountBootstrapCalibrator calibrator =
        new DiscountBootstrapCalibrator(pricingDate, pricingDate);
      calibrator.SwapInterp = BaseEntity.Toolkit.Numerics.InterpFactory.FromMethod(swapInterp, swapExtrap);
      calibrator.FuturesCAMethod = FuturesCAMethod.Hull;
      DiscountCurve irCurve = new DiscountCurve(calibrator);
      irCurve.Interp = BaseEntity.Toolkit.Numerics.InterpFactory.FromMethod(interpMethod, extrapMethod);
      irCurve.Ccy = Currency.USD;
      irCurve.Category = "None";
      for (int i = 0; i < mmTenorDates.Length; i++)
        irCurve.AddMoneyMarket(mmTenors[i], mmTenorDates[i], mmRates[i], mmDayCount);
      for (int i = 0; i < swapTenors.Length; i++)
        irCurve.AddSwap(swapTenors[i], swapTenorDates[i], swapRates[i], swapDayCount,
                       swapFreq, BDConvention.None, Calendar.None);
      calibrator.VolatilityCurve = new VolatilityCurve(pricingDate, caVol);
      irCurve.Fit();

      // Build the survival curve                        
      string[] survCurveTenors = new string[] { "5Y", "7Y", "10Y" };
      Dt[] survCurveDates = new Dt[] { new Dt(20, 9, 2013), new Dt(20, 9, 2015), new Dt(20, 9, 2018) };
      double[] survCurveQuotes = new double[] { 186.22, 176.39, 166.17 };

      SurvivalCurveParameters par = new SurvivalCurveParameters(DayCount.Actual360, Frequency.Quarterly, BDConvention.Modified,
        Calendar.None, BaseEntity.Toolkit.Numerics.InterpMethod.Weighted, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const, NegSPTreatment.Allow);

      SurvivalCurve survCurve = SurvivalCurve.FitCDSQuotes("SurvCurve", pricingDate, Dt.Empty, Currency.USD, 
        "", true, CDSQuoteType.ParSpread, 500, par, irCurve, survCurveTenors, survCurveDates, survCurveQuotes, new []{0.4}, 0, null, null, 0, 0, null, true);    
      // Do tests
      for (int i = 0; i < survCurveDates.Length; i++)
      {
        double curveRiskyDuration = CurveUtil.ImpliedDuration(survCurve, survCurve.Calibrator.Settle, survCurveDates[i],
          DayCount.Actual360, Frequency.Quarterly, BDConvention.Modified, Calendar.None);
        CDS cds = new CDS(pricingDate, survCurveDates[i], Currency.USD, survCurveQuotes[i] / 10000.0, 
          DayCount.Actual360, Frequency.Quarterly, BDConvention.Modified, Calendar.None);                
        CDSCashflowPricer pricer = new CDSCashflowPricer(cds, pricingDate, 
          Dt.Add(pricingDate, 1), irCurve, survCurve, null, 0, 0, TimeUnit.None);
        double cdsRiskyDuration = pricer.RiskyDuration();
        Assert.AreEqual(curveRiskyDuration, cdsRiskyDuration, 1e-5,
          "Curve CDSPricer RiskyDuration");
      }
    }

    [Test, Smoke, Category("PricingMethods")]
    public void SurvivalProbability()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).SurvivalProbability();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void Carry()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).Carry();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void MTMCarry()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).MTMCarry();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void Pv()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).Pv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void PvTo()
    {
      Dt toDate = PvToDate != 0 ? new Dt(PvToDate) : Dt.Add(asOf_, "2 Y");
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).Pv(toDate);
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FullPrice()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).FullModelPrice();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FlatPrice()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).FlatPrice();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FeePv()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).FeePv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FlatFeePv()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).FlatFeePv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FullFeePv()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).FullFeePv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void ProtectionPv()
    {
      TestNumeric(cdsPricers_, cdsNames_,
        delegate(object p)
        {
          return ((ICDSPricer)p).ProtectionPv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void ProtectionPvNotDependOnUpfront()
    {
      double fee_1 = 0.02;
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.Fee = fee_1;
        if (((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle < cdsPricers_[i].AsOf)
        {
          ((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle = Dt.Add(cdsPricers_[i].AsOf, 3);
          cdsPricers_[i].Reset();
        }
      }
      double[] protectionPv_1 = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).ProtectionPv(); });

      double fee_2 = 0.03;
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.Fee = fee_2;
        if (((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle < cdsPricers_[i].AsOf)
        {
          ((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle = Dt.Add(cdsPricers_[i].AsOf, 3);
          cdsPricers_[i].Reset();
        }
      }
      double[] protectionPv_2 = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).ProtectionPv(); });

      Initialize();
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        Assert.AreEqual(protectionPv_1[i], protectionPv_2[i], 1e-6, "Protection " + i.ToString());
      }
    }

    [Test, Smoke, Category("PricingMethods")]
    public void LegsIdentity()
    {
      // This test identity: ProtectionPv + FeePv = Pv
      double[] feePv = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).FeePv(); });
      double[] protectionPv = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).ProtectionPv(); });
      double[] pv = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).Pv(); });
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        Assert.AreEqual(pv[i], protectionPv[i] + feePv[i], 1e-6, "pricer " + i.ToString());
      }
      
      // Setup upfront fee.
      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.Fee = 0.02;
        if (((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle < cdsPricers_[i].AsOf)
        {
          ((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle = Dt.Add(cdsPricers_[i].AsOf, 3);
          cdsPricers_[i].Reset();
        }
      }
      feePv = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).FeePv(); });
      protectionPv = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).ProtectionPv(); });
      pv = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).Pv(); });

      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        Assert.AreEqual(pv[i], protectionPv[i] + feePv[i], 1e-6, "pricer " + i.ToString());
      }

      Initialize();
    }

    [Test, Smoke, Category("PricingMethods")]
    public void UpfrontFee()
    {
      double[] feePv_0 = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).FeePv(); });
      double[] pv_0 = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).FeePv(); });

      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        ((ICDSPricer)cdsPricers_[i]).CDS.Fee = 0.02;
        if (((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle < cdsPricers_[i].AsOf)
        {
          ((ICDSPricer)cdsPricers_[i]).CDS.FeeSettle = Dt.Add(cdsPricers_[i].AsOf, 3);
          cdsPricers_[i].Reset();
        }
      }
      double[] feePv_002 = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).FeePv(); });
      double[] pv_002 = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p) { return ((ICDSPricer)p).FeePv(); });

      double[] discountFactor = System.Array.ConvertAll<IPricer, double>(cdsPricers_,
        delegate(IPricer p){
          return ((ICDSPricer)p).DiscountCurve.DiscountFactor(
            ((ICDSPricer)p).AsOf, ((ICDSPricer)cdsPricers_[0]).CDS.FeeSettle);
        });
      Initialize();

      for (int i = 0; i < cdsPricers_.Length; i++)
      {
        double upfrontFeeAmount = 0.02 * ((ICDSPricer)cdsPricers_[i]).Notional * discountFactor[i];
        Assert.AreEqual(feePv_0[i]+upfrontFeeAmount, feePv_002[i], 1e-5, "UpfrontFee FeePv" + i.ToString());
        Assert.AreEqual(pv_0[i] + upfrontFeeAmount, pv_002[i], 1e-5, "UpfrontFee Pv" + i.ToString());
      }
    }

    [Test, Category("PricingMethods")]
    public void Irr()
    {
      DayCount dayCount = DayCount.Actual365Fixed;
      Frequency freq = Frequency.SemiAnnual;
      double[] prices = CalcValues(cdsPricers_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).FullModelPrice();
        });
      TestNumeric<double>(cdsPricers_, prices, cdsNames_,
        delegate(object p, double price)
        {
            return ((CDSCashflowPricer)p).Irr(price, dayCount, freq);
        });
    }

    [Test, Category("PricingMethods")]
    public void ImpliedDiscountSpread()
    {
      double[] prices = CalcValues(cdsPricers_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).FullModelPrice();
        });
      TestNumeric<double>(cdsPricers_, prices, cdsNames_,
        delegate(object p, double price)
        {
            return ((CDSCashflowPricer)p).ImpliedDiscountSpread(price);
        });
    }

    [Test, Category("PricingMethods")]
    public void ImpliedSurvivalSpread()
    {
      double[] prices = CalcValues(cdsPricers_,
        delegate(object p)
        {
            return ((CDSCashflowPricer)p).FullModelPrice();
        });
      TestNumeric<double>(cdsPricers_, prices, cdsNames_,
        delegate(object p, double price)
        {
            return ((CDSCashflowPricer)p).ImpliedHazardRateSpread(price);
        });
    }


    [Test, Smoke, Category("PricingMethods")]
    public void Test_ReferenceDifferentFromDiscount()
    {
      CDSCashflowPricer pricer = (CDSCashflowPricer) cdsPricers_[0].Clone();
      CDS cds = (CDS) pricer.Product.Clone();
      cds.CdsType = CdsType.FundedFloating;
      cds.Premium = 0.005;
      pricer.Product = cds;
      double pv = pricer.Pv();
      pricer.ReferenceCurve = pricer.DiscountCurve;
      double pvR = pricer.Pv();
      Assert.AreEqual(pv, pvR, 1e-16, "pv");
    }


    #endregion // PricingMethods

    #region SummaryRiskMethods
    [Test, Category("SummaryRiskMethods")]
    public void Spread01()
    {
      Spread01(cdsPricers_, cdsNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadGamma()
    {
      SpreadGamma(cdsPricers_, cdsNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadHedge()
    {
      SpreadHedge(cdsPricers_, cdsNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void IR01()
    {
      IR01(cdsPricers_, cdsNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Recovery01()
    {
      Recovery01(cdsPricers_, cdsNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void VOD()
    {
      VOD(cdsPricers_, cdsNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Theta()
    {
      Theta(cdsPricers_, cdsNames_);
    }
    #endregion //SummaryRiskMethods

    #region RiskMethods
    [Test, Category("RiskMethods")]
    public void SpreadSensitivity()
    {
      Spread(cdsPricers_);
    }

    [Test, Category("RiskMethods")]
    public void SpreadSensitivityWithHedge()
    {
      double pv_1 = cdsPricers_[0].Pv();
      // Do a spread sensitivity with hedge calculation - "maturity"
      System.Data.DataTable dt = Toolkit.Sensitivity.Sensitivities.Spread(new IPricer[]{cdsPricers_[0]}, "Pv",
        QuotingConvention.CreditSpread, 0.0, 0.0, 0.35, true, true, Toolkit.Sensitivity.BumpType.Parallel, 
        null, false, true, "maturity", null, null);

      // Now use another sensitivity that refit the curve
      // If the "maturity" hedge does not restore tenor product maturity properly
      // The refit will change the survival curve state and Pv() will be different
      dt = Toolkit.Sensitivity.Sensitivities.Recovery(new IPricer[] { cdsPricers_[0] }, "Pv",
          true, 0, 0.1, Toolkit.Sensitivity.BumpType.Parallel, false, null, null);
      
      double pv_2 = cdsPricers_[0].Pv();
      Assert.AreEqual(pv_1, pv_2, 1e-7, "Curve restore after maturity hedge");
      return;
    }

    [Test]
    public void RateSensitivity()
    {
      Rate(cdsPricers_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(cdsPricers_);
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity()
    {
      Recovery(cdsPricers_);
    }

    [Test, Category("RiskMethods")]
    public void JTD_TestFD_SA_Equiv ()
    {
      Toolkit.Base.CdsType cdsType = CdsType;
      Toolkit.Base.CdsType[] cdsTypes = {Toolkit.Base.CdsType.FundedFixed, Toolkit.Base.CdsType.FundedFloating, Toolkit.Base.CdsType.Unfunded};

      for (int i = 0; i < 3; i++)
      {
        
        CdsType = cdsTypes[i];
        Initialize();
        string type = Enum.GetName(typeof (Toolkit.Base.CdsType), CdsType);
        System.Data.DataTable dt1 = Sensitivities.Default(
          cdsPricers_, "Pv", false, "", null,
          SensitivityMethod.FiniteDifference, null, null);
        System.Data.DataTable dt2 = Sensitivities.Default(
          cdsPricers_, "Pv", false, "", null, SensitivityMethod.SemiAnalytic,
          null, null);
        for (int j = 0; j < dt1.Rows.Count; j++)
        {
          Assert.AreEqual((double) dt1.Rows[j]["Delta"], (double) dt2.Rows[j]["Delta"], 1e-8, string.Concat(type, j.ToString()));
        }
      }
      CdsType = cdsType;
    }

    #endregion // RiskMethods

    #region SetUp
    /// <summary>
    ///    Initializer
    /// </summary>
    /// 
    /// <remarks>
    ///   This function is called once after a class object is constructed 
    ///   and public properties are set.
    /// </remarks>
    /// 
    [OneTimeSetUp]
    public void Initialize()
    {
      // Load discount curve
      string filename = GetTestFilePath(LiborDataFile);
      DiscountData dd = (DiscountData)XmlLoadData(filename, typeof(DiscountData));
      DiscountCurve discountCurve = dd.GetDiscountCurve();
      if (discountCurve == null)
        throw new System.Exception(filename + ": Invalid discount data");

      // Load credit Curves
      filename = GetTestFilePath(CreditDataFile);
      CreditData cd = (CreditData)XmlLoadData(filename, typeof(CreditData));
      SurvivalCurve[] survivalCurves = cd.GetSurvivalCurves(discountCurve);
      if (survivalCurves == null)
        throw new System.Exception(filename + ": Invalid credit data");

      CreateCDSPricers(cd, discountCurve, survivalCurves);
    }

    // Helper method to create CDS and CDS pricers
    private void CreateCDSPricers(CreditData cd, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
    {
          // Create CDS pricers
      cdsPricers_ = new IPricer[survivalCurves.Length];
      cdsNames_ = new string[survivalCurves.Length];
      Dt effective = this.EffectiveDate != 0 ?
        new Dt(this.EffectiveDate) : Dt.Add(survivalCurves[0].AsOf, -180);
      Dt maturity = this.MaturityDate != 0 ?
        new Dt(this.MaturityDate) : Dt.CDSMaturity(effective, "5Y");
      Dt asOf = asOf_ = this.PricingDate != 0 ?
        new Dt(this.PricingDate) : survivalCurves[0].AsOf;
      Dt settle = this.SettleDate != 0 ?
        new Dt(this.SettleDate) : Dt.Add(asOf, 1);
      double fee = this.Fee;
      Dt feeSettle = this.FeeSettleDate != 0 ?
        new Dt(this.FeeSettleDate) : Dt.Empty;
      for (int i = 0; i < survivalCurves.Length; ++i)
      {
        cdsNames_[i] = survivalCurves[i].Name;
        CurveTenor tenor = survivalCurves[i].TenorAfter(maturity);
        double premium = ((CDS)tenor.Product).Premium;
        CDS cds = new CDS(effective, maturity, cd.Currency, premium,
          cd.DayCount, cd.Frequency, cd.Roll, cd.Calendar);
        cds.CdsType = CdsType;
        {
          CDSCashflowPricer p = new CDSCashflowPricer(cds, asOf, settle,
           discountCurve, survivalCurves[i], null, 0.0, 0, TimeUnit.None);
          p.Notional = notional_;
          if (fee != 0 && !feeSettle.IsEmpty())
          {
            p.FeeSettle = feeSettle;
            p.MarketQuote = fee;
            p.QuotingConvention = QuotingConvention.CreditConventionalUpfront;
          }
          if (RateResetDates != null)
          {
            ArrayList rates = Parse(typeof(double), RateResetRates);
            ArrayList dates = Parse(typeof(int), RateResetDates);
            for (int j = 0; j < rates.Count; j++)
            {
              p.RateResets.Add(new RateReset(new Dt((int)dates[j]), (double)rates[j]));
            }
            asOf = Dt.Add(asOf_, i, TimeUnit.Months);
          }
          
          cdsPricers_[i] = p;
        }
      }
    }
    #endregion SetUp

    #region Helpers
    // This copy the logic from qCDSCashflowTable method and pop up the start dates, 
    // pay dates, accruals, discount factors and survival probabilities.
    private void GetCashflowTable(ICDSPricer pricer, 
      out Dt[] startDate, out Dt[] payDate, out double[] accrual, out double[] discountFactors, out double[] survivalProbs)
    {
      Cashflow cf = pricer.GenerateCashflow(null, pricer.Settle);
      DiscountCurve dc = pricer.DiscountCurve;
      SurvivalCurve sc = pricer.SurvivalCurve;

      Dt pStart, pEnd;
      pStart = pEnd = cf.Effective;
      int lastIdx = cf.Count - 1;
      startDate = new Dt[cf.Count];
      payDate = new Dt[cf.Count];
      accrual = new double[cf.Count];
      discountFactors = new double[cf.Count];
      survivalProbs = new double[cf.Count];

      for (int i = 0; i <= lastIdx; i++)
      {
        // At begining, pEnd is either cf.Effective or last payment date
        pStart = pEnd;
        startDate[i] = pStart;
        // Find the current payment date and set pEnd to it.
        Dt paymentDate = pEnd = cf.GetDt(i);
        // The current accrual
        double accr = cf.GetAccrued(i);

        payDate[i] = paymentDate;

        accrual[i] = accr;
        discountFactors[i] = dc.DiscountFactor(paymentDate);

        if (i == lastIdx && pricer.IncludeMaturityProtection)
          survivalProbs[i] = sc.SurvivalProb(Dt.Add(paymentDate, 1));
        else
          survivalProbs[i] = sc.SurvivalProb(paymentDate);
      }

      return;
    }


    private void AccruedPaidOnDefault(CdsType cdsType)
    {
      if (cdsType != CdsType.FundedFixed)
      {
        CheckAccruedPaidOnDefault(cdsType);
        return;
      }

      using (new ConfigItems
      {
        // The consistency test of the funded CDS requires
        // this flag set in order to calculate accrued on
        // default by substraction.
        {"CashflowPricer.IgnoreAccruedInProtection", true},
      }.Update())
      {
        CheckAccruedPaidOnDefault(cdsType);
      }
    }

    private void CheckAccruedPaidOnDefault(CdsType cdsType)
    {
      // Test one pricer is enough
      ICDSPricer pricer = (ICDSPricer)cdsPricers_[0].Clone();
      pricer.CDS.CdsType = cdsType;
      if(cdsType == CdsType.FundedFloating)
        pricer.RateResets.Add(new RateReset(cdsPricers_[0].AsOf, 0.05));
      pricer.CDS.AccruedOnDefault = true;
      pricer.Reset();
      double feePv_true = pricer.FeePv();

      pricer.CDS.AccruedOnDefault = false;
      pricer.Reset();
      double feePv_false = pricer.FeePv();
      double accruedOnDefault = feePv_true - feePv_false;

      // Get the cashflow table
      Dt[] startDate;
      Dt[] payDate;
      double[] accrual;
      double[] discountFactors;
      double[] survivalProbs;
      GetCashflowTable(pricer, out startDate, out payDate, out accrual, out discountFactors, out survivalProbs);

      // Calculate the accrual day fraction
      int[] accrualDays = new int[startDate.Length];
      int[] daysInPeriod = new int[startDate.Length];
      double[] fraction = new double[startDate.Length];
      double[] plainAccrualAmount = new double[accrual.Length];

      Dt accrualStart;
      double premium;
      ((CDSCashflowPricer)pricer).FindAccrualStart(pricer.Settle, pricer.CDS, out accrualStart, out premium);

      accrualDays[0] = (int)(System.Math.Abs(Dt.Diff(pricer.Settle < accrualStart ? accrualStart : pricer.Settle, payDate[0])) * 0.5 + 1);
      daysInPeriod[0] = System.Math.Abs(Dt.Diff(pricer.Settle < accrualStart ? accrualStart : pricer.Settle, payDate[0]));
      fraction[0] = daysInPeriod[0]==0 ? 0.0 : accrualDays[0] / (double)daysInPeriod[0];
      plainAccrualAmount[0] = fraction[0] * accrual[0];
      for (int i = 1; i < startDate.Length; i++)
      {
        accrualDays[i] = (int)(System.Math.Abs(Dt.Diff(startDate[i], payDate[i])) * 0.5 + 1);
        daysInPeriod[i] = System.Math.Abs(Dt.Diff(startDate[i], payDate[i]));
        if (i == startDate.Length - 1 && ((CDSCashflowPricer)pricer).IncludeMaturityAccrual)
          daysInPeriod[i] += 1;
        fraction[i] = (double)accrualDays[i] / (double)daysInPeriod[i];
        plainAccrualAmount[i] = fraction[i] * accrual[i];
      }

      double settleDiscoutnFactor = pricer.DiscountCurve.DiscountFactor(
        pricer.AsOf, pricer.Settle < accrualStart ? accrualStart : pricer.Settle);
      double settleSurvvialProb = pricer.SurvivalCurve.SurvivalProb(pricer.AsOf,
        pricer.Settle < accrualStart ? accrualStart : pricer.Settle);

      double sum = 0.0;
      double[] discountFactorsScaled = new double[discountFactors.Length];
      double[] survivalProbsScaled = new double[survivalProbs.Length];
      for (int i = 0; i < discountFactors.Length; i++)
      {
        if (i == 0)
        {
          discountFactorsScaled[i] = (1 + discountFactors[i] / settleDiscoutnFactor) / 2.0;
          survivalProbsScaled[i] = (1 - survivalProbs[i] / settleSurvvialProb);
        }
        else
        {
          discountFactorsScaled[i] = (discountFactors[i - 1] + discountFactors[i]) / settleDiscoutnFactor / 2.0;
          survivalProbsScaled[i] = (survivalProbs[i - 1] - survivalProbs[i]) / settleSurvvialProb;
        }

        sum += plainAccrualAmount[i] * discountFactorsScaled[i] * survivalProbsScaled[i];
      }
      sum *= pricer.Notional;
      sum = sum * settleDiscoutnFactor * settleSurvvialProb;

      Assert.AreEqual( 0,
        Math.Abs((sum - accruedOnDefault) / (sum + accruedOnDefault)), 
        1e-2, "Accrual paid on default " + cdsType.ToString());
    }
    #endregion Helpers

    #region Properties
    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = "data/USD.LIBOR_Data.xml";

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string CreditDataFile { get; set; } = "data/CDX.NA.HY.7_CreditData.xml";

    /// <summary>
    ///   CDS pricers based on CashflowStreamPricer instead of CashflowPricer
    /// </summary>
    public bool UseCashflowStreamPricer { get; set; } = false;

    /// <summary>
    ///   Forward settle date for FwdPremium() and FwdPremium01() tests
    /// </summary>
    public int ForwarddSettleDate { get; set; } = 0;

    /// <summary>
    ///   Forward date for PvTo() test
    /// </summary>
    public int PvToDate { get; set; } = 0;

    /// <summary>
    ///   Forward date for PvTo() function
    /// </summary>
    public int FwdValueFromDate { get; set; } = 0;

    public string RateResetRates { get; set; }

    public string RateResetDates { get; set; }

    public CdsType CdsType { get; set; } = CdsType.Unfunded;

    #endregion //Properties

    #region Data
    const double epsilon = 1.0E-7;

    // Calibrator Pricer configuration parameters
    //private bool configUseCashflowStream_ = true;
    //private bool includeMaturityAccrual_ = true;

    // CDS Pricer configuration parameters
    private IPricer[] cdsPricers_ = null;
    private string[] cdsNames_ = null;
    private Dt asOf_;
    private double notional_ = 1000000;
    // Parameters

    #endregion // Data

  } // TestCDS 

}  