//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Reflection;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests
{
  /// <summary>
  /// Parametrized test class for testing the bond analytics
  /// </summary>
  [TestFixture("BondFullPriceFromRSpread")]
  [TestFixture("BondRSFromFullPriceRiskFree")]
  [TestFixture("BondRSpreadFromFullPrice")]
  [TestFixture("TestAmortBondActualActualCase1")]
  [TestFixture("TestAmortBondActualActualCase2")]
  [TestFixture("TestAmortBondActualActualCase3")]
  [TestFixture("TestAmortBondBBGCase1")]
  [TestFixture("TestAmortFRNBBGActActBond1")]
  [TestFixture("TestAmortFRNBBGActActBond2")]
  [TestFixture("TestAmortFRNBBGActActBond3")]
  [TestFixture("TestAmortFRNBBGCase1a")]
  [TestFixture("TestAmortFRNBBGCase1b")]
  [TestFixture("TestAmortFRNBBGCase2")]
  [TestFixture("TestAmortFRNBBGCase3")]
  [TestFixture("TestBBGTieOut1")]
  [TestFixture("TestBBGTieOut10")]
  [TestFixture("TestBBGTieOut11")]
  [TestFixture("TestBBGTieOut12")]
  [TestFixture("TestBBGTieOut13")]
  [TestFixture("TestBBGTieOut14")]
  [TestFixture("TestBBGTieOut15")]
  [TestFixture("TestBBGTieOut16")]
  [TestFixture("TestBBGTieOut2")]
  [TestFixture("TestBBGTieOut3")]
  [TestFixture("TestBBGTieOut4")]
  [TestFixture("TestBBGTieOut5")]
  [TestFixture("TestBBGTieOut6")]
  [TestFixture("TestBBGTieOut7")]
  [TestFixture("TestBBGTieOut8")]
  [TestFixture("TestBBGTieOut9")]
  [TestFixture("TestBondLongFirstCouponPeriod")]
  [TestFixture("TestBondNegativeCDSSpread1")]
  [TestFixture("TestBondNegativeCDSSpread2")]
  [TestFixture("TestDefaultedBondCase1")]
  [TestFixture("TestDefaultedBondCase2")]
  [TestFixture("TestDefaultedBondCase3")]
  [TestFixture("TestDefaultedBondCase4")]
  [TestFixture("TestDefaultedBondCase5")]
  [TestFixture("TestDefaultedBondCase6")]
  [TestFixture("TestDefaultedBondCase7")]
  [TestFixture("TestDefaultedBondCase8")]
  [TestFixture("TestDefaultedBondCase9")]
  [TestFixture("TestFRNAccrualDays")]
  [TestFixture("TestFRNAccrualDays2")]
  [TestFixture("TestFRNAccrualDays3")]
  [TestFixture("TestFRNActActBond1")]
  [TestFixture("TestFRNActActBond2")]
  [TestFixture("TestFRNActActBond3")]
  [TestFixture("TestFRNBBGCase1")]
  [TestFixture("TestFRNBBGCase10")]
  [TestFixture("TestFRNBBGCase11")]
  [TestFixture("TestFRNBBGCase12")]
  [TestFixture("TestFRNBBGCase2")]
  [TestFixture("TestFRNBBGCase3")]
  [TestFixture("TestFRNBBGCase4")]
  [TestFixture("TestFRNBBGCase5")]
  [TestFixture("TestFRNBBGCase6")]
  [TestFixture("TestFRNBBGCase7")]
  [TestFixture("TestFRNBBGCase8")]
  [TestFixture("TestFRNBBGCase9")]
  [TestFixture("TestFullPriceFromRSRiskFree")]
  [TestFixture("TestPricefromRSAmortFixed1")]
  [TestFixture("TestPricefromRSAmortFixed2")]
  [TestFixture("TestPricefromRSAmortFRN1")]
  [TestFixture("TestPricefromRSAmortFRN2")]
  [TestFixture("TestPriceFromRSpreadFRNCase1")]
  [TestFixture("TestPriceFromRSpreadFRNCase2")]
  [TestFixture("TestRSfromPriceAmortFixed1")]
  [TestFixture("TestRSfromPriceAmortFixed2")]
  [TestFixture("TestRSfromPriceAmortFRN1")]
  [TestFixture("TestRSfromPriceAmortFRN2")]
  [TestFixture("TestRspreadFromPriceFRNCase1")]
  [TestFixture("TestRspreadFromPriceFRNCase2")]
  public class TestCcrBondPricer : ToolkitTestBase
  {

    public TestCcrBondPricer(string name) : base(name)
    {}

    #region data

    private Dt pricingDate_;
    private Dt issueDate_;
    private Dt maturity_;
    private Dt firstCoupon_=Dt.Empty;
    private Dt lastCoupon_ = Dt.Empty;
    private Dt defaultSettleDate_ = Dt.Empty;
    private Dt settleDate_;
    private BondType bondType_;
    private double coupon_;
    private Frequency freq_;
    private Calendar calendar_;
    private DayCount dc_;
    private BondPricer bondPricer_;
    private string names_;
    private double marketQuote_;
    private QuotingConvention quotingConvention_;
    private BDConvention bdConvention_ = Toolkit.Base.BDConvention.Following;
    private bool periodAdjust_ = false;
    private object accrueOnCycle_ = null;
    private bool zSpreadInSensitivities_ = true;
    private bool eomRule_ = false;
    private bool allowNegativeSpreads_ = false;
    private bool floating_;
    private String creditCurveSource_;
    private double currentCoupon_;
    private Dt[] amortDates_;
    private double[] amortAmounts_;
    private object stubRate_=null;
    private object currentLibor_=null;
    private object cycleRule_=null;
    private Currency ccy_ = Currency.USD;
    private object recoveryRate_ = null;
    
    #endregion

    static SurvivalCurve BackwardCompatibleFix(SurvivalCurve sc)
    {
      foreach(CurveTenor tenor in sc.Tenors)
      {
        CDS cds = (CDS) tenor.Product;
        Dt dt = Dt.CDSRoll(cds.Effective);
        Dt last = dt;
        while(dt < cds.Maturity)
        {
          last = dt;
          dt = Dt.CDSRoll(dt);
        }
        cds.LastPrem = last;
      }
      sc.Fit();
      return sc;
    }

    [Test]
    public void FastPv()
    {
      var lazyPricer = CcrPricer.Get(bondPricer_);

      double pvlazy = lazyPricer.FastPv(settleDate_);
      
      bondPricer_.AsOf = bondPricer_.Settle;
      double pv  = bondPricer_.Pv();
      Assert.AreEqual(pv, pvlazy, 1e-6, "fast pv");
    }

    [Test]
    public void FastPvForwardDts()
    {
      var lazyPricer = CcrPricer.Get(bondPricer_);

      var pvlazy = new List<double>();
      for (Dt dt = settleDate_; dt < maturity_; dt = Dt.Roll(Dt.AddMonth(dt, 1, false), bdConvention_, calendar_))
      {
        pvlazy.Add(lazyPricer.FastPv(dt));
      }
      var pv = new List<double>();
      for (Dt dt = settleDate_; dt < maturity_; dt = Dt.Roll(Dt.AddMonth(dt, 1, false), bdConvention_, calendar_))
      {
        double pvThisDt = 0;
        bondPricer_.AsOf = bondPricer_.Settle = dt;
        pvThisDt = bondPricer_.Pv();
        bondPricer_.AsOf = pricingDate_; 
        bondPricer_.Settle = settleDate_;
        pv.Add(pvThisDt);
      }
      for (int i = 0; i < pv.Count; ++i)
        Assert.AreEqual(pv[i], pvlazy[i], 1e-6, "forward pv " + i);
    }

    [Test]
    public void FastPvPerturbDiscountCurve()
    {
       var lazyPricer = CcrPricer.Get(bondPricer_);
       bondPricer_.AsOf = bondPricer_.Settle;
      
      var discountCurve = bondPricer_.DiscountCurve; 
      var rand = new Random();
      for (int i = 0; i < 100; i++)
      {
        discountCurve.BumpQuotes(null, Toolkit.Base.QuotingConvention.None, new[] {4 * rand.NextDouble() }, BumpFlags.RefitCurve);
        bondPricer_.Reset();
        double pv = bondPricer_.Pv();
        double pvlazy = lazyPricer.FastPv(settleDate_);
        Assert.AreEqual(pv, pvlazy, 1e-6, "perturbed Discount Curve " + i);
      }

    }

    [OneTimeSetUp]
    public void Initialize()
    {
      names_ = "bondPricerTest";
      DiscountCurve discountCurve = LoadDiscountCurve(LiborDataFile);
      

      Bond bond = new Bond(issueDate_, maturity_, ccy_, bondType_, coupon_, dc_, Toolkit.Base.CycleRule.None, freq_, bdConvention_, calendar_);
      Type mType = typeof(Bond);
      mType.GetField("overrideBackwardCompatibleCF_", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bond,
                                                                                                                 true);

      //set up the bond schedule parameters 
      bond.PeriodAdjustment = periodAdjust_;
      if (accrueOnCycle_ != null)
        bond.AccrueOnCycle = (bool)accrueOnCycle_;
      if (cycleRule_ != null)
        bond.CycleRule = (CycleRule)cycleRule_;
      else
      {
        if(eomRule_)
          bond.CycleRule = Toolkit.Base.CycleRule.EOM;
      }
      
      if (firstCoupon_ != Dt.Empty)
        bond.FirstCoupon = firstCoupon_;
      if(lastCoupon_ != Dt.Empty)
        bond.LastCoupon = lastCoupon_;
      if (amortDates_.Length > 0)
        AmortizationUtil.ToSchedule(amortDates_, amortAmounts_, bond.AmortizationSchedule);
      double recoveryRate = (creditCurveSource_.Equals("ImplyFromPrice")) ? 0.4 : -1.0;
      bondPricer_ = new BondPricer(bond, pricingDate_, settleDate_, discountCurve, null, 0, TimeUnit.None, recoveryRate);
      bondPricer_.AllowNegativeCDSSpreads = allowNegativeSpreads_;
      bondPricer_.EnableZSpreadAdjustment = zSpreadInSensitivities_;
      if (floating_)
      {
        bond.Index = "USDLIBOR";
        bond.ReferenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex(bond.Index, bond.Freq, bond.Ccy, bond.DayCount,
                                                                     bond.Calendar, 0);
        bondPricer_.ReferenceCurve = discountCurve;
        bondPricer_.CurrentRate = currentCoupon_;
        if ((currentLibor_ != null)&& (stubRate_!=null))
        {
          bondPricer_.CurrentFloatingRate = (double)currentLibor_;
          bondPricer_.StubRate = (double) stubRate_;
        }
      }
      bondPricer_.MarketQuote = marketQuote_;
      bondPricer_.QuotingConvention = quotingConvention_;
      GetSurvivalCurve(discountCurve, bondPricer_);
    }
    public void GetSurvivalCurve(DiscountCurve discountCurve, BondPricer pricer)
    {
      if (creditCurveSource_.Equals("UseSuppliedCurve"))
      {
        SurvivalCurve[] sc = LoadCreditCurves(CreditDataFile, discountCurve, new string[] { "AA" });
        if(defaultSettleDate_ != Dt.Empty)
        {
          sc[0].SurvivalCalibrator.RecoveryCurve.JumpDate= defaultSettleDate_;
        }
        pricer.SurvivalCurve = sc[0];
      }
      else
      {
        if (creditCurveSource_.Equals("ImplyFromPrice"))
        {
          SurvivalCurve flatHcurve = new SurvivalCurve(pricingDate_, 0.0);
          flatHcurve.Calibrator = new SurvivalFitCalibrator(pricingDate_, settleDate_, 0.4, discountCurve);
          //flatHcurve.Fit();
          pricer.SurvivalCurve = flatHcurve;
          // find flat curve to match market quote
          pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve(0.4);

          // Setup curve name
          if (pricer.SurvivalCurve != null)
            pricer.SurvivalCurve.Name = pricer.Product.Description + "_Curve";
        }
      }
    }

    #region properties

    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = null;

    public string CreditDataFile { get; set; } = null;

    public new string PricingDate
    {
      set { pricingDate_ = Dt.FromStr(value, "%Y%m%d"); }
    }

    public new string MaturityDate
    {
      set { maturity_ = Dt.FromStr(value, "%Y%m%d"); }
    }

    public new string SettleDate
    {
      set { settleDate_ = Dt.FromStr(value, "%Y%m%d"); }
    }

    public string IssueDate
    {
      set { issueDate_ = Dt.FromStr(value, "%Y%m%d"); }
    }

    public string FirstCouponDate
    {
      set { firstCoupon_ = Dt.FromStr(value, "%Y%m%d"); }
    }

    public string LastCouponDate
    {
      set { lastCoupon_ = Dt.FromStr(value, "%Y%m%d"); }
    }
    public string BondType
    {
      set { bondType_ = (BondType)Enum.Parse(typeof(BondType), value); }
    }

    public double Coupon
    {
      set { coupon_ = value; }
    }

    public double CurrentCoupon
    {
      set { currentCoupon_ = value; }
    }
    public new string Frequency
    {
      set { freq_ = (Frequency)Enum.Parse(typeof(Frequency), value); }
    }

    public new string Calendar
    {
      set { calendar_ = CalendarCalc.GetCalendar(value); }
    }

    public new string DayCount
    {
      set { dc_ = (DayCount)Enum.Parse(typeof(DayCount), value); }
    }

    public bool AccrueOnCycle
    {
      set{ accrueOnCycle_ =  value;}
    }

    public double MarketQuote
    {
      set { marketQuote_ = value; }
    }

    public string QuotingConvention
    {
      set { quotingConvention_ = (QuotingConvention)Enum.Parse(typeof(QuotingConvention), value); }
    }

    public string CreditCurveSource
    {
      set { creditCurveSource_ = value; }
    }

    public bool Floating
    {
      set { floating_ = value; }
    }

    public bool AllowNegativeCDSSpreads
    {
      set{ allowNegativeSpreads_ = value;}
    }


   
    public string AmortDates
    {
      set
      {
        var amortDatesArray = value.Split(new char[] { ',' });
        amortDates_ = new Dt[amortDatesArray.Length];
        for (int i = 0; i < amortDatesArray.Length; i++)
          amortDates_[i] = Dt.FromStr(amortDatesArray[i], "%Y%m%d");
      }
    }

    public bool ZSpreadInSensitivities
    {
      set{ zSpreadInSensitivities_ = value;}
    }

    public string AmortAmounts
    {
      set
      {
        var amortAmtsArray = value.Split(new char[] { ',' });
        amortAmounts_ = new double[amortAmtsArray.Length];
        for (int i = 0; i < amortAmtsArray.Length; i++)
          amortAmounts_[i] = Double.Parse(amortAmtsArray[i]);
      }
    }

    public bool EomRule
    {
      set { eomRule_ = value; }
    }

    public bool PeriodAdjustment
    {
      set { periodAdjust_ = value; }
    }

    public string BDConvention
    {
      set { bdConvention_ = (BDConvention)Enum.Parse(typeof(BDConvention), value); }
    }

    public double CurrentLibor
    {
      
      set{ currentLibor_ = value;}
    }

    public double StubRate
    {
      set{ stubRate_ = value;}
    }

    public string Ccy
    {
      set { ccy_ = (Currency) Enum.Parse(typeof (Currency), value); }
    }

    public string CycleRule
    {
      set { cycleRule_ = (CycleRule) Enum.Parse(typeof (CycleRule), value); }
    }

    public string DefaultSettlementDate
    {
      set { defaultSettleDate_ = Dt.FromStr(value, "%Y%m%d"); }
    }

    public double RecoveryRate
    {
      set{ recoveryRate_ = value;}
    }


    #endregion
  }
}
