//
// Copyright (c)    2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  [TestFixture("TestBondAccruedInterest1")]
  [TestFixture("TestBondAccruedInterestCase2")]
  [TestFixture("TestBondAccruedInterestCase3")]
  [TestFixture("TestBondAccruedInterestCase4")]
  [TestFixture("TestBondAccruedInterestCase5")]
  [TestFixture("TestBondAccruedInterestLongStub1")]
  [TestFixture("TestBondAccruedInterestLongStub2")]
  [TestFixture("TestBondAccruedInterestShortStub")]

  public class TestBondPreviousCycleDate : ToolkitTestBase
  {
    public TestBondPreviousCycleDate(string name) : base (name)
    {}


    #region data

    private Dt pricingDate_;
    private Dt issueDate_;
    private Dt maturity_;
    private Dt firstCoupon_ = Dt.Empty;
    private Dt lastCoupon_ = Dt.Empty;
    
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
    
    private bool eomRule_ = false;
    
    
    private object cycleRule_ = null;
    private Currency ccy_ = Currency.USD;
    
    #endregion

    [Test]
    public void PreviousCycleDate()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).PreviousCycleDate().ToInt());
    }
    
    
    [Test]
    public void AccruedInterest()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).AccruedInterest());
    }

    [Test]
    public void AccrualDays()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).AccrualDays());
    }

    [Test]
    public void Accrued()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).Accrued());
    }
    
    [OneTimeSetUp]
    public void Initialize()
    {
      names_ = "bondPricerTest";
      DiscountCurve discountCurve = LoadDiscountCurve(LiborDataFile);


      Bond bond = new Bond(issueDate_, maturity_, ccy_, bondType_, coupon_, dc_, Toolkit.Base.CycleRule.None, freq_, bdConvention_, calendar_);

      //set up the bond schedule parameters 
      bond.PeriodAdjustment = periodAdjust_;
      if (accrueOnCycle_ != null)
        bond.AccrueOnCycle = (bool)accrueOnCycle_;
      if (cycleRule_ != null)
        bond.CycleRule = (CycleRule)cycleRule_;
      else
      {
        if (eomRule_)
          bond.CycleRule = Toolkit.Base.CycleRule.EOM;
      }

      if (firstCoupon_ != Dt.Empty)
        bond.FirstCoupon = firstCoupon_;
      if (lastCoupon_ != Dt.Empty)
        bond.LastCoupon = lastCoupon_;
      
      bondPricer_ = new BondPricer(bond, pricingDate_, settleDate_, discountCurve, null, 0, TimeUnit.None, -1.0);
      
      bondPricer_.MarketQuote = marketQuote_;
      bondPricer_.QuotingConvention = quotingConvention_;
    }
    #region properties

    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = null;

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
      set { accrueOnCycle_ = value; }
    }

    public double MarketQuote
    {
      set { marketQuote_ = value; }
    }

    public string QuotingConvention
    {
      set { quotingConvention_ = (QuotingConvention)Enum.Parse(typeof(QuotingConvention), value); }
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

    public string Ccy
    {
      set { ccy_ = (Currency)Enum.Parse(typeof(Currency), value); }
    }

    public string CycleRule
    {
      set { cycleRule_ = (CycleRule)Enum.Parse(typeof(CycleRule), value); }
    }
    #endregion
  }
}
