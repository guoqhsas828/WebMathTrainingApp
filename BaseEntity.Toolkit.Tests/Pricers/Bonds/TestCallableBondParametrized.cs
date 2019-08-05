// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

//
// Parametrized test class for testing the bond analytics
// 

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  [TestFixture("TestCallableYTC1")]
  [TestFixture("TestCallableYTC2")]
  [TestFixture("TestCallableYTC3")]
  [TestFixture("TestCallableYTC4")]
  [TestFixture("TestPuttableYTP1")]
  public class TestCallableBondParametrized : ToolkitTestBase
  {
    public TestCallableBondParametrized(string name) : base(name)
    {}

    #region data

    private Dt issueDate_;
    private Dt firstCoupon_ = Dt.Empty;
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
    private object stubRate_ = null;
    private object currentLibor_ = null;
    private object cycleRule_ = null;
    private Currency ccy_ = Currency.USD;
    private object recoveryRate_ = null;

    // Additional properties to test callable bonds added on 11/14/2011 as part of FB 31144:
    private bool useBKForCallableBonds_; // If true, will use the BK tree model for callable bonds; otherwise, will use the BGM tree for callable bonds
    private double volatility_ = double.NaN; // Single value if used for BK model; for the BGM model, use this together with DistributionType
    private double meanRev_ = double.NaN; // The mean rev parameter - used only if BK model is selected
    private DistributionType disType_; // Used only if BGM model is specified, together with the volatility value, to form a FlatVolatility object.


    private Dt[] callPeriodStartDates_;
    // The size of the arrays callPeriodStartDates_, callPeriodEndDates_, callPrices_ must be the same - they form a call schedule

    private Dt[] callPeriodEndDates_;
    private double[] callPrices_;

    private Dt[] putPeriodStartDates_;
    private Dt[] putPeriodEndDates_;
    private double[] putPrices_;
    // ditto sizes of put arrays

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

    public struct SchedItem
    {
      public Dt Start;
      public Dt End;
      public double Price;
      public double Trigger;
      public OptionStyle Style;
      public int Grace;
    };

    #region Tests


    [Test]
    public void YieldToCall()
    {
      var bond = bondPricer_.Bond;
      CallPeriod nextCallPeriod = null;
      if (!bond.IsCustom && bond.CallSchedule.Count > 0)
      {
        foreach (var cp in bond.CallSchedule.Where(c => c.StartDate > bondPricer_.Settle))
        {
          nextCallPeriod = cp;
          break;
        }
      }
      if (nextCallPeriod != null)
      {
        var ytm = PredictYieldToRedemption(bond, nextCallPeriod.StartDate, nextCallPeriod.CallPrice);
        var ytc = bondPricer_.YieldToCall();
        Assert.AreEqual(ytm, ytc, 1e-6, String.Format("Expected YTC {0}, got {1}", ytm, ytc));
      }
      else
      {
        var ytm = bondPricer_.YieldToMaturity();
        var ytc = bondPricer_.YieldToCall();
        Assert.AreEqual(ytm, ytc, 1e-6, String.Format("Expected YTC {0}, got {1}", ytm, ytc));
      }

      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).YieldToCall());
    }

    [Test]
    public void YieldToPut()
    {
      var bond = bondPricer_.Bond;
      PutPeriod nextPutPeriod = null;
      if (!bond.IsCustom && bond.PutSchedule.Count > 0)
      {
        foreach (var cp in bond.PutSchedule.Where(c => c.StartDate > bondPricer_.Settle))
        {
          nextPutPeriod = cp;
          break;
        }
      }
      if (nextPutPeriod != null)
      {
        var ytm = PredictYieldToRedemption(bond, nextPutPeriod.StartDate, nextPutPeriod.PutPrice);
        var ytp = bondPricer_.YieldToPut();
        Assert.AreEqual(ytm, ytp, 1e-6, String.Format("Expected YTP {0}, got {1}", ytm, ytp));
      }
      else
      {
        var ytm = bondPricer_.YieldToMaturity();
        var ytp = bondPricer_.YieldToPut();
        Assert.AreEqual(ytm, ytp, 1e-6, String.Format("Expected YTP {0}, got {1}", ytm, ytp));
      }

      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).YieldToPut());
    }

    [Test]
    public void YieldToWorst()
    {
      var bond = bondPricer_.Bond;
      if (!bond.IsCustom && bond.CallSchedule.Count > 0)
      {
        var yields = new List<double> {bondPricer_.YieldToMaturity()};
        yields.AddRange(bond.CallSchedule.Where(c => c.StartDate > bondPricer_.Settle).Select(cp => PredictYieldToRedemption(bond, cp.StartDate, cp.CallPrice)));
        yields.AddRange(bond.CallSchedule.Where(c => c.EndDate > bondPricer_.Settle).Select(cp => PredictYieldToRedemption(bond, cp.EndDate, cp.CallPrice)));
        yields.Add(bondPricer_.YieldToMaturity());
        double worstYield = yields.Min();
        var ytw = bondPricer_.YieldToWorst();
        Assert.AreEqual(worstYield, ytw, 1e-6, String.Format("Expected YTC {0}, got {1}", worstYield, ytw));
      }
      else
      {
        var ytm = bondPricer_.YieldToMaturity();
        var ytc = bondPricer_.YieldToWorst();
        Assert.AreEqual(ytm, ytc, 1e-6, String.Format("Expected YTC {0}, got {1}", ytm, ytc));
      }

      TestNumeric(bondPricer_, names_, p => ((BondPricer)p).YieldToWorst());
    }

    private double PredictYieldToRedemption(Bond bond, Dt redempDate, double redempPrice)
    {
      // attempt to replicate the logic of yield-to-XXX using a cloned bond with foreshortened maturity
      // and call price * notional at redemption

      var bond2 = (Bond)bond.Clone();
      // can't adjust principal at maturity, so scale down coupon...
      bond2.Coupon /= redempPrice;
      bond2.CallSchedule.Clear();
      bond2.Maturity = redempDate;
      bond2.LastCoupon = bond.Schedule.GetPrevCouponDate(Dt.Add(bond2.Maturity, -1));
      // ... and scale down price (test cases should be configured with QuotingConvention = "FlatPrice")
      var adjustedPrice = bondPricer_.MarketQuote / redempPrice;
      var bp2 = GetBondPricer(bond2, bondPricer_.DiscountCurve, adjustedPrice, bondPricer_.RecoveryRate, bond2.Callable);

      var ytm = bp2.YieldToMaturity();
      return ytm;
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

      if (Dt.Cmp(discountCurve.AsOf, pricingDt) != 0 && !setDiscountCurveDateAsPricingDate_)
        //Some of the parameterized test cases have curve date before or after pricing date, making the rate reset requirement obscured!
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
        if (eomRule_)
          bond.CycleRule = Toolkit.Base.CycleRule.EOM;
      }

      if (firstCoupon_ != Dt.Empty)
        bond.FirstCoupon = firstCoupon_;
      if (lastCoupon_ != Dt.Empty)
        bond.LastCoupon = lastCoupon_;
      if (payLagDays_ > 0)
      {
        bond.PaymentLagRule = new PayLagRule(-payLagDays_, payLagByBusinessDays_);
      }
      if (amortDates_ != null && amortAmounts_ != null && amortDates_.Length > 0 && amortDates_.Length == amortAmounts_.Length)
        AmortizationUtil.ToSchedule(amortDates_, amortAmounts_, bond.AmortizationSchedule);

      if (couponScheduleDates_ != null && couponScheduleRates_ != null && couponScheduleDates_.Length > 0 &&
          couponScheduleDates_.Length == couponScheduleRates_.Length)
      {
        IList<CouponPeriod> cs = bond.CouponSchedule;
        for (i = 0; i < couponScheduleDates_.Length; i++)
          cs.Add(new CouponPeriod(couponScheduleDates_[i], couponScheduleRates_[i]));
      }
      double recoveryRate = (creditCurveSource_.Equals("ImplyFromPrice")) ? 0.4 : -1.0;

      // Add a put/call schedule to the bond, if specified. First validate.

      foreach (var x in RedemSchedule(callPeriodStartDates_, callPeriodEndDates_, callPrices_))
      {
        bond.CallSchedule.Add(new CallPeriod(x.Start, x.End, x.Price, x.Trigger, x.Style, x.Grace));
      }
      bool isCallable = bond.CallSchedule.Count > 0;

      foreach (var x in RedemSchedule(putPeriodStartDates_, putPeriodEndDates_, putPrices_))
      {
        bond.PutSchedule.Add(new PutPeriod(x.Start, x.End, x.Price, x.Style));
      }
      bool isPuttable = bond.PutSchedule.Count > 0;
    
      
      if (floating_)
      {
        bond.Index = "USDLIBOR";
        bond.ReferenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex(bond.Index, bond.Freq, bond.Ccy, bond.DayCount,
                                                                                  bond.Calendar, 0);
      }
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

      bondPricer_ = GetBondPricer(bond, discountCurve, marketQuote_, recoveryRate, isCallable || isPuttable);
    }

    private IEnumerable<SchedItem> RedemSchedule(Dt [] periodStartDates, Dt[] periodEndDates, double[] redemPrices)
    {
      bool validSchedule = false;

      if (periodStartDates != null && periodEndDates != null && redemPrices != null && periodStartDates.Length > 0
          && periodEndDates.Length == periodStartDates.Length && redemPrices.Length == periodStartDates.Length)
      {
        bool isValid = true;
        for (int i = 1; i < periodStartDates.Length; i++)
        {
          // Make sure period dates are in ascedning order
          if (periodStartDates[i] <= periodStartDates[i - 1])
          {
            isValid = false;
            break;
          }

          if (periodStartDates[i] != Dt.Add(periodEndDates[i - 1], 1))
          {
            isValid = false;
            break;
          }

          if (periodEndDates[i] <= periodEndDates[i - 1])
          {
            isValid = false;
            break;
          }
        }
        for (int i = 0; i < redemPrices.Length; i++)
        {
          if (redemPrices[i] <= 0.0)
          {
            isValid = false;
            break;
          }
        }

        if (isValid) validSchedule = true;
      }
      if (validSchedule)
      {
        for (int i = 0; i < periodStartDates.Length; i++)
        {
          yield return
            new SchedItem
            {
              Start = periodStartDates[i],
              End = periodEndDates[i],
              Price = redemPrices[i] / 100.0,
              Trigger = 1000.0,
              Style = OptionStyle.American,
              Grace = 0
            };
        }
      }
    }

    public void GetSurvivalCurve(DiscountCurve discountCurve, BondPricer pricer)
    {
      if (creditCurveSource_.Equals("UseSuppliedCurve"))
      {
        SurvivalCurve[] sc = LoadCreditCurves(CreditDataFile, discountCurve, new string[] {"AA"});
        if (defaultSettleDate_ != Dt.Empty)
        {
          sc[0].SurvivalCalibrator.RecoveryCurve.JumpDate = defaultSettleDate_;
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
      set { accrueOnCycle_ = value; }
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
      set { allowNegativeSpreads_ = value; }
    }

    public string AmortDates
    {
      set { amortDates_ = DateArrayFromString(value); }
    }

    public double[] AmortAmounts
    {
      set { amortAmounts_ = value; }
    }

    // CouponScheduleDates and CouponScheduleRates together represent a coupon schedule. The have to be the same length:

    public string CouponScheduleDates
    {
      set { couponScheduleDates_ = DateArrayFromString(value); }
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
      set { currentLibor_ = value; }
    }

    public double StubRate
    {
      set { stubRate_ = value; }
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
      set { recoveryRate_ = value; }
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
      set { callPeriodStartDates_ = DateArrayFromString(value); }
    }

    public string CallPeriodEndDates
    {
      set { callPeriodEndDates_ = DateArrayFromString(value); }
    }

    public double[] CallPrices
    {
      set { callPrices_ = value; }
    }

    public string PutPeriodStartDates
    {
      set { putPeriodStartDates_ = DateArrayFromString(value); }
    }

    public string PutPeriodEndDates
    {
      set { putPeriodEndDates_ = DateArrayFromString(value); }
    }

    public double[] PutPrices
    {
      set { putPrices_ = value; }
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
      set { resetDates_ = DateArrayFromString(value); }
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
      string[] tokens = s1.Split(new char[] {','});
      Dt[] ret = new Dt[tokens.Length];
      for (int i = 0; i < tokens.Length; i++)
        ret[i] = Dt.FromStr(tokens[i].Trim(), "%Y%m%d");
      return ret;
    }

    // Intead of creating duplicate redundant properties in the derived class, rely on the properties already provided in the base class.
    // However, unfortunately, they are defined as int in the base class, so, for convenience, we convert them to Dt in these functions:

    private Dt GetPricingDate()
    {
      return new Dt(PricingDate);
    }

    private Dt GetMaturityDate()
    {
      return new Dt(MaturityDate);
    }

    private Dt GetSettleDate()
    {
      return new Dt(SettleDate);
    }

    #endregion

    private BondPricer GetBondPricer(Bond bond, DiscountCurve discountCurve, double quote, double recoveryRate, bool isCallable)
    {
      var bp = new BondPricer(bond, GetPricingDate(), GetSettleDate(), discountCurve, null, 0, TimeUnit.None, recoveryRate);
      bp.AllowNegativeCDSSpreads = allowNegativeSpreads_;
      bp.EnableZSpreadAdjustment = zSpreadInSensitivities_;
      if (floating_)
      {
        bp.ReferenceCurve = discountCurve;
        // If we have reset rates and reset dates, use them. Otherwise, use CurrentRate and CurrentFloatingRate.
        if (resetDates_ != null && resetRates_ != null && resetDates_.Length > 0 && resetDates_.Length == resetRates_.Length)
        {
          for (int i = 0; i < resetDates_.Length; i++)
            bp.RateResets.Add(new RateReset(resetDates_[i], resetRates_[i]));
        }
        else
        {
          bp.CurrentRate = currentCoupon_;
          if ((currentLibor_ != null) && (stubRate_ != null))
          {
            bp.CurrentFloatingRate = (double)currentLibor_;
            bp.StubRate = (double)stubRate_;
          }
        }
      }
      bp.MarketQuote = quote;
      bp.QuotingConvention = quotingConvention_;
      GetSurvivalCurve(discountCurve, bp);

      if (isCallable)
      {
        if (useBKForCallableBonds_) // Use BK tree model for callable bonds
        {
          if (volatility_ > 0.0 && !double.IsNaN(volatility_))
            bp.Sigma = volatility_;
          if (!double.IsNaN(meanRev_))
            bp.MeanReversion = meanRev_;
        }
        else // Otherwise, use the BGM model
        {
          if (volatility_ > 0.0 && !double.IsNaN(volatility_))
          {
            FlatVolatility volObj = new FlatVolatility();
            volObj.Volatility = volatility_;
            volObj.DistributionType = disType_;
            bp.VolatilityObject = volObj;
          }
        }
      }
      return bp;
    }

  }
}