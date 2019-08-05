//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

//
// Parametrized test class for testing the bond analytics
// 

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  [TestFixture("Amort_Fixed_1")]
  [TestFixture("Amort_Fixed_10")]
  [TestFixture("Amort_Fixed_11")]
  [TestFixture("Amort_Fixed_2")]
  [TestFixture("Amort_Fixed_3")]
  [TestFixture("Amort_Fixed_4")]
  [TestFixture("Amort_Fixed_5")]
  [TestFixture("Amort_Fixed_6")]
  [TestFixture("Amort_Fixed_7")]
  [TestFixture("Amort_Fixed_8")]
  [TestFixture("Amort_Fixed_9")]
  [TestFixture("Amort_Fixed_Customized_1")]
  [TestFixture("Amort_Fixed_Customized_10")]
  [TestFixture("Amort_Fixed_Customized_11")]
  [TestFixture("Amort_Fixed_Customized_2")]
  [TestFixture("Amort_Fixed_Customized_3")]
  [TestFixture("Amort_Fixed_Customized_4")]
  [TestFixture("Amort_Fixed_Customized_5")]
  [TestFixture("Amort_Fixed_Customized_6")]
  [TestFixture("Amort_Fixed_Customized_7")]
  [TestFixture("Amort_Fixed_Customized_8")]
  [TestFixture("Amort_Fixed_Customized_9")]
  [TestFixture("Amort_Floating_1")]
  [TestFixture("Amort_Floating_2")]
  [TestFixture("Amort_Floating_3")]
  [TestFixture("Amort_Floating_Customized_1")]
  [TestFixture("Amort_Floating_Customized_2")]
  [TestFixture("Amort_Floating_Customized_3")]
  [TestFixture("TestAmort1")]
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
  [TestFixture("TestCallable1")]
  [TestFixture("TestCallable10")]
  [TestFixture("TestCallable11")]
  [TestFixture("TestCallable12")]
  [TestFixture("TestCallable13")]
  [TestFixture("TestCallable14")]
  [TestFixture("TestCallable15")]
  [TestFixture("TestCallable16")]
  [TestFixture("TestCallable17")]
  [TestFixture("TestCallable18")]
  [TestFixture("TestCallable19")]
  [TestFixture("TestCallable2")]
  [TestFixture("TestCallable20")]
  [TestFixture("TestCallable21")]
  [TestFixture("TestCallable22")]
  [TestFixture("TestCallable23")]
  [TestFixture("TestCallable24")]
  [TestFixture("TestCallable25")]
  [TestFixture("TestCallable26")]
  [TestFixture("TestCallable27")]
  [TestFixture("TestCallable28")]
  [TestFixture("TestCallable29")]
  [TestFixture("TestCallable3")]
  [TestFixture("TestCallable30")]
  [TestFixture("TestCallable31")]
  [TestFixture("TestCallable32")]
  [TestFixture("TestCallable33")]
  [TestFixture("TestCallable34")]
  [TestFixture("TestCallable35")]
  [TestFixture("TestCallable36")]
  [TestFixture("TestCallable4")]
  [TestFixture("TestCallable5")]
  [TestFixture("TestCallable6")]
  [TestFixture("TestCallable7")]
  [TestFixture("TestCallable8")]
  [TestFixture("TestCallable9")]
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
  [TestFixture("TestFullPriceFromRSpread")]
  [TestFixture("TestFullPriceFromRSRiskFree")]
  [TestFixture("TestPricefromRSAmortFixed1")]
  [TestFixture("TestPricefromRSAmortFixed2")]
  [TestFixture("TestPricefromRSAmortFRN1")]
  [TestFixture("TestPricefromRSAmortFRN2")]
  [TestFixture("TestPriceFromRSpreadFRNCase1")]
  [TestFixture("TestPriceFromRSpreadFRNCase2")]
  [TestFixture("TestRSFromFullPriceRiskFree")]
  [TestFixture("TestRSfromPriceAmortFixed1")]
  [TestFixture("TestRSfromPriceAmortFixed2")]
  [TestFixture("TestRSfromPriceAmortFRN1")]
  [TestFixture("TestRSfromPriceAmortFRN2")]
  [TestFixture("TestRSpreadFromFullPrice")]
  [TestFixture("TestRspreadFromPriceFRNCase1")]
  [TestFixture("TestRspreadFromPriceFRNCase2")]
  [TestFixture("TestFwdPv01FromDiscountMargin")]
  public class TestBondParametrized : ToolkitTestBase
  {
    public TestBondParametrized(string name) : base(name)
    {}
    
    #region data

    private Dt issueDate_;
    private Dt firstCoupon_=Dt.Empty;
    private Dt lastCoupon_ = Dt.Empty;
    private Dt defaultSettleDate_ = Dt.Empty;
    private BondType bondType_;
    private double coupon_;
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
    private String creditCurveSource_ = string.Empty;
    private double currentCoupon_;
    private Dt[] amortDates_;
    private double[] amortAmounts_;
    private Dt[] couponScheduleDates_;
    private double[] couponScheduleRates_;
    private object stubRate_=null;
    private object currentLibor_=null;
    private object cycleRule_=null;
    private Currency ccy_ = Currency.USD;
    private object recoveryRate_ = null;

    // Additional properties to test callable bonds added on 11/14/2011 as part of FB 31144:
    private bool useBKForCallableBonds_;  // If true, will use the BK tree model for callable bonds; otherwise, will use the BGM tree for callable bonds
    private double volatility_ = double.NaN;  // Single value if used for BK model; for the BGM model, use this together with DistributionType
    private double meanRev_ = double.NaN;     // The mean rev parameter - used only if BK model is selected
    private DistributionType disType_; // Used only if BGM model is specified, together with the volatility value, to form a FlatVolatility object.
    private Dt[] callPeriodStartDates_; // The size of the arrays callPeriodStartDates_, callPeriodEndDates_, callPrices_ must be the same - they form a call schedule
    private Dt[] callPeriodEndDates_;
    private double[] callPrices_;

    // When the following flag is set to true, we will construct a "regular" bond, as before, retrieve its payment schedule,
    // and then pass it back as customized schedule. This will be used to match the results of computations on a bond with customized
    // schedule: we will have one set of N test results for "regular" bonds (possibly, with amortizations schedule and/or coupon schedule).
    // and a parralel set of othe N results for the same bonds with "customized" schedule obtained from the regular bond.
    // We expect the numbers in these two parralel sets of results to match in general (except, perhaps the Asset Swap Spread
    // in some cases)
    private bool testCustomizedBondSchedule_;

    private int payLagDays_ = 0;
    private bool payLagByBusinessDays_;
    private bool setDiscountCurveDateAsPricingDate_;

    // For floating rate bonds, use the reset dates and rates as an alternative to CurrentCoupon and CurrentLibor.
    // The arrays ResetDates and ResetRates must have the same size, if present
    private Dt[] resetDates_;
    private double[] resetRates_;
    
    #endregion

    #region Tests

    [Test]
    public void NotionalConsistency()
    {
      var prevNotional = double.NaN;
      var amortization = 0.0;
      var ps = bondPricer_.PaymentSchedule;
      foreach (var date in ps.GetPaymentDates().OrderBy(d=>d))
      {
        var payments = ps.GetPaymentsOnDate(date);
        var ip = payments.OfType<InterestPayment>().FirstOrDefault();
        if (ip != null)
        {
          var thisNotional = ip.Notional;
          if (!double.IsNaN(prevNotional))
          {
            NUnit.Framework.Assert.AreEqual(
              prevNotional - thisNotional, amortization, 1E-15,
              "Inconsistent amortization and notional changes");
          }

          prevNotional = thisNotional;
          amortization = 0;
        }

        foreach (var pe in payments.OfType<PrincipalExchange>())
        {
          amortization += pe.Amount;
        }
      }

      if (double.IsNaN(prevNotional)) return;

      NUnit.Framework.Assert.AreEqual(
        prevNotional, amortization, 1E-15,
        "Inconsistent amortization and notional changes");
    }

    [Test]
    public void FullPrice()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).FullPrice());
    }

    [Test]
    public void YieldToMaturity()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).YieldToMaturity());
    }

    [Test]
    public void TrueYield()
    {
      
      TestNumeric(bondPricer_, names_, p => (bondPricer_.Bond.Callable || bondPricer_.Bond.Convertible)?0.0:((BondPricer) p).TrueYield());
    }


    [Test]
    public void AccruedInterest()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).AccruedInterest());
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


    [Test]
    public void Pv01()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).PV01());
    }

    [Test]
    public void Convexity()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).Convexity());
    }

    [Test]
    public void ModDuration()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).ModDuration());
    }
    [Test]
    public void Irr()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).Irr());
    }

    [Test]
    public void AssetSwapSpread()
    {
      DayCount dc;
      Frequency freq;
      BondPricer.DefaultAssetSwapParams(bondPricer_.Bond.Ccy, out freq, out dc);
      TestNumeric(bondPricer_, names_,
        p =>((floating_ ? 0.0 : ((BondPricer) p).AssetSwapSpread(dc,freq))));
    }

    [Test]
    public void ModelFullPrice()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).FullModelPrice());
    }

    [Test]
    public void ZSpread()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).ImpliedZSpread());
    }

    [Test]
    public void CDSSpread()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).ImpliedCDSSpread());
    }

    [Test]
    public void CDSLevel()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).ImpliedCDSLevel());
    }

    [Test]
    public void RSpread()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).CalcRSpread());
    }
    [Test]
    public void CDSSpread01()
    {
      double spread01 = (bondPricer_.SurvivalCurve != null)
                          ? bondPricer_.Spread01() / bondPricer_.NotionalFactor
                          : 0.0;
      if (Double.IsInfinity(spread01))
        spread01 = Double.NaN;
      TestNumeric(bondPricer_, names_,
                         p => (spread01));
    }

    [Test]
    public void CDSSpreadDuration()
    {
      double spreadDuration = (bondPricer_.SurvivalCurve != null) ? bondPricer_.SpreadDuration() : 0.0;
      if (Double.IsInfinity(spreadDuration))
        spreadDuration = Double.NaN;
      TestNumeric(bondPricer_, names_,
                         p => (spreadDuration));
    }

    [Test]
    public void CDSSpreadConvexity()
    {
      double spreadConvexity = (bondPricer_.SurvivalCurve != null) ? bondPricer_.SpreadConvexity() : 0.0;
      if (Double.IsInfinity(spreadConvexity))
        spreadConvexity = Double.NaN;
      TestNumeric(bondPricer_, names_,
                         p => (spreadConvexity));
    }

    [Test]
    public void ZSpread01()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).ZSpread01()/bondPricer_.NotionalFactor);
    }

    [Test]
    public void ZSpreadRoundTrip()
    {
      double zSpread;
      var zBondPricer = ZSpreadPricer(bondPricer_, out zSpread);
      Assert.AreEqual(bondPricer_.ProductPv(), zBondPricer.ProductPv(), 1e-7);
    }

    [Test]
    public void ZSpread01TieOut()
    {
      if (bondPricer_.Bond.Floating)
        return;

      // tie out for fixed rate bonds

      var pricerWithZSpreadAdjEnabled = (BondPricer)bondPricer_.Clone();
      pricerWithZSpreadAdjEnabled.EnableZSpreadAdjustment = true;

      double zSpread;
      var pricerQuotedOnZSpread = ZSpreadPricer(pricerWithZSpreadAdjEnabled, out zSpread);

      pricerQuotedOnZSpread.MarketQuote = zSpread + (25.0 / 10000.0);
      double priceUp = pricerQuotedOnZSpread.FullPrice();
      pricerQuotedOnZSpread.MarketQuote = zSpread - (25.0 / 10000.0);
      double priceDown = pricerQuotedOnZSpread.FullPrice();
      double zSpread01InCurrentNotional = (pricerWithZSpreadAdjEnabled.ZSpread01() / bondPricer_.NotionalFactor) * 10000.0;
      double priceChange = ((priceDown - priceUp) / 50.0) * 10000.0;
      Assert.AreEqual(priceChange, zSpread01InCurrentNotional, 1e-2, // tolerance equiv to 1/100 cent on $100
                      String.Format("Tie out {0} : ZSpread01 {1}", priceChange, zSpread01InCurrentNotional));
    }

    [Test]
    public void ZSpread10PCTieOut()
    {
      if (bondPricer_.Bond.Floating)
        return;

      // tie out for fixed rate bonds

      double zSpread;
      var pricerQuotedOnZSpread = ZSpreadPricer(bondPricer_, out zSpread);

      pricerQuotedOnZSpread.MarketQuote = zSpread * 1.1;
      double priceUp = pricerQuotedOnZSpread.FullPrice();
      pricerQuotedOnZSpread.MarketQuote = zSpread * 0.9;
      double priceDown = pricerQuotedOnZSpread.FullPrice();
      double zSpread10PC = bondPricer_.ZSpreadScenario(0.1);
      double priceChange = (priceDown - priceUp) * bondPricer_.NotionalFactor / 2.0;
      Assert.AreEqual(priceChange, zSpread10PC, 1e-6, // tolerance equiv to 1/100 cent on $100
                      String.Format("Tie out {0} : ZSpread10PC {1}", priceChange, zSpread10PC));
    }

    private static BondPricer ZSpreadPricer(BondPricer pricer, out double zSpread)
    {
      zSpread = pricer.ImpliedZSpread();
      var zBondPricer = (BondPricer)pricer.Clone();
      zBondPricer.QuotingConvention = QuotingConvention.ZSpread;
      zBondPricer.MarketQuote = zSpread;
      return zBondPricer;
    }

    [Test]
    public void ZSpreadDuration()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).ZSpreadDuration());
    }

    [Test]
    public void Pv()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).Pv());
    }

    [Test]
    public void IR01()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).Rate01() / bondPricer_.NotionalFactor);
    }

    [Test]
    public void RateDuration()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).RateDuration());
    }

    [Test]
    public void RateConvexity()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer) p).RateConvexity());
    }

    [Test]
    public void DiscountMargin()
    {
      TestNumeric(bondPricer_, names_, p => ((floating_ ? ((BondPricer) p).DiscountMargin() : 0.0)));
    }

    [Test]
    public void MarketSpreadDuration()
    {
      TestNumeric(bondPricer_, names_, p => ((floating_ ? ((BondPricer) p).MarketSpreadDuration() : 0.0)));
    }

    [Test]
    public void VoD()
    {
      TestNumeric(bondPricer_, names_,
        p => ((BondPricer)p).SurvivalCurve == null
          ? 0.0 : Sensitivities.VOD((BondPricer)p));
    }

    // Additional computations to test callable bonds added on 11/14/2011 as part of FB 31144:

    [Test]
    public void OptionPv()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).OptionPv());
    }

    [Test]
    public void YieldToCall()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).YieldToCall());
    }

    [Test]
    public void YieldToWorst()
    {
      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).YieldToWorst());
    }

    #endregion Tests

    #region SetUp

    [OneTimeSetUp]
    public void Initialize()
    {
      names_ = "bondPricerTest";
      DiscountCurve discountCurve;
      Dt pricingDt = GetPricingDate();
      if (setDiscountCurveDateAsPricingDate_)
        discountCurve = LoadDiscountCurve(LiborDataFile, pricingDt);
      else
        discountCurve = LoadDiscountCurve(LiborDataFile);
      int i;

      Bond bond = new Bond(issueDate_, GetMaturityDate(), ccy_, bondType_, coupon_, DayCount, Toolkit.Base.CycleRule.None, Frequency, bdConvention_, Calendar);

      if (Dt.Cmp(discountCurve.AsOf, pricingDt) != 0 && !setDiscountCurveDateAsPricingDate_)  //Some of the parameterized test cases have curve date before or after pricing date, making the rate reset requirement obscured!
      {
          Type mType = typeof(Bond);
          mType.GetField("overrideBackwardCompatibleCF_", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bond, true);
      }
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
      if (payLagDays_ > 0)
      {
        bond.PaymentLagRule = new PayLagRule(-payLagDays_, payLagByBusinessDays_);
      }
      if (amortDates_ != null && amortAmounts_ != null && amortDates_.Length > 0 && amortDates_.Length == amortAmounts_.Length)
        AmortizationUtil.ToSchedule(amortDates_, amortAmounts_, bond.AmortizationSchedule);

      if (couponScheduleDates_ != null && couponScheduleRates_ != null && couponScheduleDates_.Length > 0 && couponScheduleDates_.Length == couponScheduleRates_.Length)
      {
        IList<CouponPeriod> cs = bond.CouponSchedule;
        for (i = 0; i < couponScheduleDates_.Length; i++)
          cs.Add(new CouponPeriod(couponScheduleDates_[i], couponScheduleRates_[i]));
      }
      double recoveryRate = (creditCurveSource_.Equals("ImplyFromPrice")) ? 0.4 : -1.0;

      // Add a call schedule to the bond, if specified. First validate.

      bool isCallable = false;
      if (callPeriodStartDates_ != null && callPeriodEndDates_ != null && callPrices_ != null && callPeriodStartDates_.Length > 0
        && callPeriodEndDates_.Length == callPeriodStartDates_.Length && callPrices_.Length == callPeriodStartDates_.Length)
      {
        bool isValid = true;
        for (i = 1; i < callPeriodStartDates_.Length; i++)
        {
          // Make sure call period dates are in ascedning order
          if (callPeriodStartDates_[i] <= callPeriodStartDates_[i-1])
          {
            isValid = false;
            break;
          }

          if (callPeriodStartDates_[i] != Dt.Add(callPeriodEndDates_[i-1], 1))
          {
            isValid = false;
            break;
          }

          if (callPeriodEndDates_[i] <= callPeriodEndDates_[i - 1])
          {
            isValid = false;
            break;
          }
        }
        for (i = 0; i < callPrices_.Length; i++)
        {
          if (callPrices_[i] <= 0.0)
          {
            isValid = false;
            break;
          }
        }

        if (isValid) isCallable = true;
      }
      if (isCallable)
      {
        for (i = 0; i < callPeriodStartDates_.Length; i++)
          bond.CallSchedule.Add(
            new CallPeriod(callPeriodStartDates_[i], callPeriodEndDates_[i], callPrices_[i] / 100.0, 1000.0, OptionStyle.American, 0));
        
      }

      bondPricer_ = new BondPricer(bond, GetPricingDate(), GetSettleDate(), discountCurve, null, 0, TimeUnit.None, recoveryRate);
      bondPricer_.AllowNegativeCDSSpreads = allowNegativeSpreads_;
      bondPricer_.EnableZSpreadAdjustment = zSpreadInSensitivities_;
      if (floating_)
      {
        bond.Index = "USDLIBOR";
        bond.ReferenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex(bond.Index, bond.Freq, bond.Ccy, bond.DayCount,
                                                                     bond.Calendar, 0);
        bondPricer_.ReferenceCurve = discountCurve;
        // If we have reset rates and reset dates, use them. Otherwise, use CurrentRate and CurrentFloatingRate.
        if (resetDates_ != null && resetRates_ != null && resetDates_.Length > 0 && resetDates_.Length == resetRates_.Length)
        {
          for (i  = 0; i < resetDates_.Length; i++)
            bondPricer_.RateResets.Add(new RateReset(resetDates_[i], resetRates_[i]));
        }
        else
        {
          bondPricer_.CurrentRate = currentCoupon_;
          if ((currentLibor_ != null) && (stubRate_ != null))
          {
            bondPricer_.CurrentFloatingRate = (double)currentLibor_;
            bondPricer_.StubRate = (double)stubRate_;
          }
        }
      }
      bondPricer_.MarketQuote = marketQuote_;
      bondPricer_.QuotingConvention = quotingConvention_;
      GetSurvivalCurve(discountCurve, bondPricer_);

      // If the testCustomizedBondSchedule_ flag is set to true, we create a bond with customized schedule as follows.
      // We take the original bond, retrieve the payment schedule from it, and pass it back to the bond object
      // to simulate "customized" schedule. We then run the same computations on it as on the "original" bond.
      // We expect, in general, (perhaps with some exceptions), the results to match those of the "original" bond.
      // See also comments next to testCustomizedBondSchedule_ flag.
      if (testCustomizedBondSchedule_)
      {
        PaymentSchedule ps = bond.GetPaymentSchedule();
        bond.CustomPaymentSchedule = PaymentScheduleUtils.CreateCopy(ps);
      }

      if (isCallable)
      {
        if (useBKForCallableBonds_)  // Use BK tree model for callable bonds
        {
          if (volatility_ > 0.0 && !double.IsNaN(volatility_))
            bondPricer_.Sigma = volatility_;
          if (!double.IsNaN(meanRev_))
            bondPricer_.MeanReversion = meanRev_;
        }
        else // Otherwise, use the BGM model
        {
          if (volatility_ > 0.0 && !double.IsNaN(volatility_))
          {
            FlatVolatility volObj = new FlatVolatility();
            volObj.Volatility = volatility_;
            volObj.DistributionType = disType_;
            bondPricer_.VolatilityObject = volObj;
          }
        }
      }
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
          SurvivalCurve flatHcurve = new SurvivalCurve(GetPricingDate(), 0.0);
          flatHcurve.Calibrator = new SurvivalFitCalibrator(GetPricingDate(), GetSettleDate(), 0.4, discountCurve);
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
    #endregion Set Up

    #region properties

    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = null;

    public string CreditDataFile { get; set; } = null;

    public Dt IssueDate
    {
      set { issueDate_ = value; }
    }

    public Dt FirstCouponDate
    {
      set { firstCoupon_ = value; }
    }

    public Dt LastCouponDate
    {
      set { lastCoupon_ = value; }
    }
    public BondType BondType
    {
      set { bondType_ = value; }
    }

    public double Coupon
    {
      set { coupon_ = value; }
    }

    public double CurrentCoupon
    {
      set { currentCoupon_ = value; }
    }

    public bool AccrueOnCycle
    {
      set{ accrueOnCycle_ =  value;}
    }

    public double MarketQuote
    {
      set { marketQuote_ = value; }
    }

    public QuotingConvention QuotingConvention
    {
      set { quotingConvention_ = value; }
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
        amortDates_ = DateArrayFromString(value);
      }
    }

    public double[] AmortAmounts
    {
      set { amortAmounts_ = value; }
    }

    // CouponScheduleDates and CouponScheduleRates together represent a coupon schedule. The have to be the same length:

    public string CouponScheduleDates
    {
      set
      {
        couponScheduleDates_ = DateArrayFromString(value);
      }
    }

    public double[] CouponScheduleRates
    {
      set { couponScheduleRates_ = value; }
    }

    public bool ZSpreadInSensitivities
    {
      set { zSpreadInSensitivities_ = value; }
    }

    public bool EomRule
    {
      set { eomRule_ = value; }
    }

    public bool PeriodAdjustment
    {
      set { periodAdjust_ = value; }
    }

    public BDConvention BDConvention
    {
      set { bdConvention_ = value; }
    }

    public double CurrentLibor
    {
      
      set{ currentLibor_ = value;}
    }

    public double StubRate
    {
      set{ stubRate_ = value;}
    }

    public Currency Ccy
    {
      set { ccy_ = value; }
    }

    public CycleRule CycleRule
    {
      set { cycleRule_ = value; }
    }

    public Dt DefaultSettlementDate
    {
      set { defaultSettleDate_ = value; }
    }

    public double RecoveryRate
    {
      set{ recoveryRate_ = value;}
    }

    // Additional properties to test callable bonds added on 11/14/2011 as part of FB 31144:

    public bool UseBKForCallableBonds
    {
      set { useBKForCallableBonds_ = value; }
    }

    public double Volatility
    {
      set { volatility_ = value; }
    }

    public double MeanRev
    {
      set { meanRev_ = value; }
    }

    public DistributionType DisType
    {
      set { disType_ = value; }
    }

    public string CallPeriodStartDates
    {
      set
      {
        callPeriodStartDates_ = DateArrayFromString(value);
      }
    }

    public string CallPeriodEndDates
    {
      set
      {
        callPeriodEndDates_ = DateArrayFromString(value);
      }
    }

    public double[] CallPrices
    {
      set { callPrices_ = value; }
    }

    public bool TestCustomizedBondSchedule
    {
      set { testCustomizedBondSchedule_ = value; }
    }

    public int PayLagDays
    {
      set { payLagDays_ = value; }
    }

    public bool PayLagByBusinessDays
    {
      set { payLagByBusinessDays_ = value; }
    }

    // In this is set to true, we will change the Asof date of the curve to be the same as pricing date.
    public bool SetDiscountCurveDateAsPricingDate
    {
      set { setDiscountCurveDateAsPricingDate_ = value; }
    }

    // For floating rate bonds, use the reset dates and rates as an alternative to CurrentCoupon and CurrentLibor.
    // The arrays ResetDates and ResetRates must have the same size, if present
    public string ResetDates
    {
      set
      {
        resetDates_ = DateArrayFromString(value);
      }
    }

    public double[] ResetRates
    {
      set { resetRates_ = value; }
    }

    #endregion

    #region helping_functions

    private static Dt[] DateArrayFromString(string sArr)
    {
      // Pass as input a comma-separated array of dates in the format YYYYMMDD
      if (sArr == null) return null;
      string s1 = sArr.Trim();
      if (s1 == string.Empty) return null;
      string[] tokens = s1.Split(new char[] { ',' });
      Dt[] ret = new Dt[tokens.Length];
      for (int i = 0; i < tokens.Length; i++)
        ret[i] = Dt.FromStr(tokens[i].Trim(), "%Y%m%d");
      return ret;
    }

    // Intead of creating duplicate redundant properties in the derived class, rely on the properties already provided in the base class.
    // However, unfortunately, they are defined as int in the base class, so, for convenience, we convert them to Dt in these functions:

    private Dt GetPricingDate() { return new Dt(PricingDate); }
    private Dt GetMaturityDate() { return new Dt(MaturityDate); }
    private Dt GetSettleDate() { return new Dt(SettleDate); }

    #endregion
  }
}