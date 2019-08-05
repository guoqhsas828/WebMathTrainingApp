//
// Copyright (c)    2018. All rights reserved.
//
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  ///   <p>Under FogBugz case 21960, client LBBW tried to find CDS Level for a zero coupon bond
  ///   with long maturity and found for some market price there exist CDS levels but for others 
  ///   our solver fail. The reason is  current bond pricer assums a fixed recovery rate
  ///   and apply the two component pricing formula:
  ///     <math>
  ///       P = E\left[Z(T) \; Q(t > T)\right] + E\left[-\int z(t)\, R\, dQ(t)\right]
  ///     </math>
  ///   where<ul>
  ///       <li><m>Z(T)</m>   = discount factor for bond maturity <m>T</m></li>
  ///       <li><m>z(t)</m>   = discount factor for default time <m>t</m></li>
  ///      <li><m>R</m>      = assumed recovery rate</li>
  ///      <li><m>Q(t>T)</m> = probability of survival up to <m>T</m></li>
  ///       <li><m>dQ(t)</m>  = default probability in <m>(t, t+dt)</m></li>
  ///   </ul></p>
  ///   <p>This formula leads to a nonmonotonic pricing funtion, the bond price initially decreases
  ///   with credit spread, and it hits a minimum value, and then levels up with widening spread.
  ///   It is this formula's property that make some of the bond market prices fail the solver.
  ///   For example when market price is below the minimum point, there will be no solution, when
  ///   a market price corresponds two possible credit spreads, the solver (assums monotonicity)
  ///   will fail, and lastly when the market price is higher than the recovery rate, the solver 
  ///   will fail because the recovery rate is the asymptotic value for the price-spread profile.
  ///   But under this scenario, we should report a zero credit spread since the market price is
  ///   larger than the risk free bond model price.</p>
  ///  
  ///   <p>According to David Kelly, a simple solution without changing pricing model is to imply a 
  ///   CDS spread using:
  ///     <math>
  ///      P_{\mathrm{risk-free
  /// }} - P_{\mathrm{market}} = \mathrm{CDS Spread} \times \mathrm{Risky Duration}
  ///     </math>
  ///   whenever the solver fails for scenario where risk-free bond price is larger than market price.
  ///   The fact that risk-free bond price is larger than the market price must be compensated by a 
  ///   finite credit spread. The above equation is defined recursively and has a unique solutions. </p>
  ///     
  ///   <p>This test will test these scenarios and these are based on the spreadsheet example under the
  ///   case 21960</p>
  /// </summary>
  [TestFixture, Smoke]
  public class TestBondCDSLevelFailScenario : ToolkitTestBase
  {
    #region setup
    [OneTimeSetUp]
    public void Initialize()
    {
      BuildDiscountCurve();
      BuildSurvivalCurve();
      BuildZeroCouponBond();
    }
    #endregion setup

    #region tests
    [Test, Smoke]
    public void TestNonMonotonic()
    {
      // This test will compute ZC bond model price as a function 
      // of flat CDS curve spread and to show it's decreasing and
      // then the increasing trend

      testModelPrices_ = new double[testSpreads_.Length];
      SurvivalCurve flatSurvCurve = BuildFaltSurvivalCurve(0);

      double minimumPrice = 1000;
      pos_ = -1;
      for (int i = 0; i < testSpreads_.Length; i++)
      {
        flatSurvCurve.Spread = testSpreads_[i]*0.0001;
        BuildBondPricer(flatSurvCurve);
        testModelPrices_[i] = zcBondPricer_.FullModelPrice() * 100.0;
        if (minimumPrice > testModelPrices_[i])
        {
          minimumPrice = testModelPrices_[i];
          pos_ = i;
        }
      }

      // Test prices before the minimum to be monotoic decreasing
      bool decreasingBeforeMinimum = true;
      for(int i = 1; i <= pos_; i++)
      {
        decreasingBeforeMinimum = decreasingBeforeMinimum && (testModelPrices_[i - 1] > testModelPrices_[i]);        
      }
      TestNonMonotonicOK_ = decreasingBeforeMinimum;
      Assert.IsTrue(decreasingBeforeMinimum, "Decreasing trend failed");

      // Test prices after the minimum to be monotonic increasing
      bool increasingAfterMinimum = true;
      for (int i = pos_; i < testModelPrices_.Length - 1; i++)
      {
        increasingAfterMinimum = increasingAfterMinimum && (testModelPrices_[i + 1] > testModelPrices_[i]);
      }

      TestNonMonotonicOK_ = TestNonMonotonicOK_ && increasingAfterMinimum;
      Assert.IsTrue(increasingAfterMinimum, "Increasing trend failed");

      return;
    }

    [Test, Smoke]
    public void TestSolutionRegion()
    {      
      // Find the monotonic increasing bond prices set that do not fail original solver
      //                                * <---------- this won't fail solver
      //                   *  <---------- this won't fail solver
      //             *  <----------- this won't fail solver
      //*        *
      // *    *
      //   *
      // The first one that does not fail original solver is the one that's larger than the price at smallest spread
      if (testModelPrices_ == null)
        TestNonMonotonic();
      if (TestNonMonotonicOK_ && testModelPrices_ != null)
      {
        int k = pos_;
        for (; k < testModelPrices_.Length; k++)
        {
          if (testModelPrices_[k] > testModelPrices_[0])
            break;
        }
        // All market price = modelPrice[i >= k] should have CDS level and increasing
        BuildBondPricer(testModelPrices_[k]);
        double cdsLevel1 = zcBondPricer_.ImpliedCDSLevel();
        double cdsLevel2;
        for (int i = k + 1; i < testModelPrices_.Length; i++)
        {
          BuildBondPricer(testModelPrices_[i]);
          cdsLevel2 = zcBondPricer_.ImpliedCDSLevel();
          Assert.IsTrue(cdsLevel2 > cdsLevel1, "fail");
          cdsLevel1 = cdsLevel2;
        }
      }
      return;
    }

    [Test, Smoke]    
    public void TestAsymptotic()
    {
      //For ZC bond when market price > risk free bond, there will be no 
      //space for credit. So in this case the CDS level should be zero.
      double asymptotic = recoveries_[0];
      double[] mktPrices = new double[] {1.2*asymptotic, 1.4*asymptotic, 1.6*asymptotic, 1.8*asymptotic};
      for(int i = 0; i < mktPrices.Length; i++)
      {
        BuildBondPricer(mktPrices[i]*100.0);
        double cdsLevel = zcBondPricer_.ImpliedCDSLevel();
        Assert.AreEqual(0, cdsLevel, 1e-5, "Fail case market > risk-free");
      }
      return;
    }

    #endregion tests

    #region helper
    /// <summary>
    ///  Build a discount curve
    /// </summary>
    private void BuildDiscountCurve()
    {
      InterpMethod interpMethod = InterpMethod.Weighted;
      ExtrapMethod extrapMethod = ExtrapMethod.Const;
      InterpMethod swapInterp = InterpMethod.Cubic;
      ExtrapMethod swapExtrap = ExtrapMethod.Const;

      var calibrator =
        new DiscountBootstrapCalibrator(asOf_, asOf_);
      calibrator.SwapInterp = InterpFactory.FromMethod(swapInterp, swapExtrap);
      calibrator.FuturesCAMethod = futureCAMethod_;

      discountCurve_ = new DiscountCurve(calibrator);
      discountCurve_.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      discountCurve_.Ccy = ccy_;
      discountCurve_.Category = "None";
      discountCurve_.Name = "USD_LIBOR";

      for (int i = 0; i < mmTenors_.Length; i++)
        if (mmRates_[i] > 0.0)
          discountCurve_.AddMoneyMarket(mmTenors_[i], mmDates[i], mmRates_[i], mmDc_);
      
      for (int i = 0; i < swapTenors_.Length; i++)
        if (swaprates_[i] > 0.0)
          discountCurve_.AddSwap(swapTenors_[i], swapDates_[i], swaprates_[i], swapDc_, swapFreq_,
                                 BDConvention.None,
                                 Calendar.None);
      calibrator.VolatilityCurve = new VolatilityCurve(asOf_, vol_);
      discountCurve_.Fit();
      return;
    }

    /// <summary>
    ///  Build a survival curve
    /// </summary>
    private void BuildSurvivalCurve()
    {
      var param = new SurvivalCurveParameters(cdsCurveDc_, 
        cdsCurveFreq_, bd_, cdsCurveCal_, InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow);

      survivalCurve_ = SurvivalCurve.FitCDSQuotes("CreditCurve", asOf_, settle_, ccy_, "none", CDSQuoteType.ParSpread, 0,
                                                  param, discountCurve_, tenorNames_, null, cdsQuotes_, recoveries_, 0,
                                                  null, null, 0, true);
      return;
    }

    /// <summary>
    ///  Build a flat survival curve
    /// </summary>
    /// <param name="spread">CDS spread</param>
    /// <returns>Flat survival curve</returns>
    private SurvivalCurve BuildFaltSurvivalCurve(double spread)
    {
      var calibrator = new SurvivalFitCalibrator(asOf_, settle_, recoveries_[0], discountCurve_);
      var survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(bondMaturity_, spread/10000.0, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.None);
      survivalCurve.Fit();
      return survivalCurve;  
    }

    /// <summary>
    ///  Build a zero coupon bond
    /// </summary>
    private void BuildZeroCouponBond()
    {
      zcBond_ = new Bond(bondIssue_, bondMaturity_, ccy_, bondType_,
        bondCoupon_, bondDc_, CycleRule.None, bondFreq_, bondRoll_, bondCal_);
      return;
    }

    /// <summary>
    ///  Build pricer for zero coupon bond
    /// </summary>
    /// <param name="marketPrice">market price</param>
    private void BuildBondPricer(double marketPrice)
    {
      zcBondPricer_ = new BondPricer(zcBond_, asOf_, settle_, 
        discountCurve_, survivalCurve_, 0, TimeUnit.None, -1.0, 0, 0, true);
      zcBondPricer_.Notional = bondNotional_;
      zcBondPricer_.QuotingConvention = QuotingConvention.FlatPrice;
      zcBondPricer_.MarketQuote = marketPrice / 100;
      zcBondPricer_.EnableZSpreadAdjustment = false;
      zcBondPricer_.Validate();
      return;
    }

    /// <summary>
    ///  Build pricer for zero coupon bond
    /// </summary>
    /// <param name="survCurve">A flat survvial curve</param>
    private void BuildBondPricer(SurvivalCurve survCurve)
    {
      zcBondPricer_ = new BondPricer(zcBond_, asOf_, settle_,
        discountCurve_, survCurve, 0, TimeUnit.None, -1.0, 0, 0, true);
      zcBondPricer_.Notional = bondNotional_;
      zcBondPricer_.QuotingConvention = QuotingConvention.FlatPrice;
      zcBondPricer_.MarketQuote = 0.5;
      zcBondPricer_.EnableZSpreadAdjustment = false;
      zcBondPricer_.Validate();
      return;
    }

    #endregion helper

    #region data
    #region discount curve data
    private Dt asOf_ = new Dt(29, 5, 2009);
    private Currency ccy_ = Currency.USD;
    private DayCount mmDc_ = DayCount.Actual360;
    private string[] mmTenors_ = new string[]{"1 D", "1 W", "2 W", "1 M", "2 M","3 M","4 M","5 M","6 M","9 M","1 Y"};
    private Dt[] mmDates = new Dt[]{
      new Dt(30,  5, 2009), new Dt(5,  6, 2009), new Dt(12, 6, 2009), new Dt(29,  6, 2009),  
      new Dt(29,  7, 2009), new Dt(29, 8, 2009), new Dt(29, 9, 2009), new Dt(29, 10, 2009), 
      new Dt(29, 11, 2009), new Dt(28, 2, 2010), new Dt(29, 5, 2010)};
    private double[] mmRates_ = new double[] { 
      0.00261, 0.00289, 0.00299, 0.00318, 0.00475, 0.00629, 0.00875, 0.01049, 0.01180, 0.01368, 0.01548};
    private DayCount edDc_ = DayCount.Actual360;
    private string[] edNames_ = new string[] {"M9", "U9", "Z9", "H0", "M0"};
    private Dt[] edDates_ = new Dt[]{
      new Dt(17, 6, 2009), new Dt(16, 9, 2009), new Dt(16, 12, 2009), new Dt(17, 3, 2010), new Dt(16, 6, 2010) };  
    private double[] edPrices_ = new double[]{0,0,0,0,0};
    private FuturesCAMethod futureCAMethod_ = FuturesCAMethod.Hull;
    private double vol_ = 0.012;
    private DayCount swapDc_ = DayCount.Thirty360;
    private Frequency swapFreq_ = Frequency.SemiAnnual;
    private string[] swapTenors_ = new string[]{
      "2 Yr", "3 Yr", "4 Yr", "5 Yr", "6 Yr", "7 Yr", "8 Yr", "9 Yr", "10 Yr",
      "11 Yr", "12 Yr", "15 Yr", "20 Yr", "25 Yr", "30 Yr", "40 Yr", "50 Yr"};
    private Dt[] swapDates_ = new Dt[]{
      new Dt(29, 5, 2011), new Dt(29, 5, 2012), new Dt(29, 5, 2013), new Dt(29, 5, 2014), new Dt(29, 5, 2015), 
      new Dt(29, 5, 2016), new Dt(29, 5, 2017), new Dt(29, 5, 2018), new Dt(29, 5, 2019), new Dt(29, 5, 2020),
      new Dt(29, 5, 2021), new Dt(29, 5, 2024), new Dt(29, 5, 2029), new Dt(29, 5, 2034), new Dt(29, 5, 2039),
      new Dt(29, 5, 2049), new Dt(29, 5, 2059) }; 
    private double[] swaprates_ = new double[]{
      0.01384, 0.02016, 0.02569, 0.02985, 0.03290, 0.03524, 0.03697, 0.03833, 0.03950, 
      0.04047, 0.04119, 0.04257, 0.04298, 0.04313, 0.04332, 0.04352, 0.04339};
    private DiscountCurve discountCurve_ = null;
    #endregion discount curve region

    #region survival curve region
    private Dt settle_ = new Dt(29, 5, 2009);
    private string[] tenorNames_ = new string[]{"6 Month","1 Year","2 Year","3 Year","5 Year","7 Year","10 Year"};
    private double[] cdsQuotes_ = new double[] {228.0, 228.0, 301.0, 370.0, 418.0, 515.0, 537.0};
    private double[] recoveries_ = new double[] {0.4};    
    private BDConvention bd_ = BDConvention.Following;
    private Calendar cdsCurveCal_ = Calendar.NYB;
    private Frequency cdsCurveFreq_ = Frequency.Quarterly;
    private DayCount cdsCurveDc_ = DayCount.Actual360;
    private SurvivalCurve survivalCurve_ = null;
    #endregion survival curve region

    #region zero coupon bond data

    private BondType bondType_ = BondType.USCorp;
    private double bondNotional_ = 1000000;
    private Dt bondIssue_ = new Dt(29, 5, 2009);
    private Dt bondMaturity_ = new Dt(10, 10, 2036);
    private double bondCoupon_ = 0.0;
    private Frequency bondFreq_ = Frequency.SemiAnnual;
    private Calendar bondCal_ = Calendar.NYB;
    private BDConvention bondRoll_ = BDConvention.Following;
    private DayCount bondDc_ = DayCount.ActualActualBond;
    private Bond zcBond_ = null;
    private BondPricer zcBondPricer_ = null;
    #endregion zero coupon bond data

    #region test data
    private double[] testSpreads_ = new double[]{
        1, 10, 20, 30, 40, 50, 75, 100, 125, 150, 175, 200, 225, 250, 275, 300, 325, 350, 375, 400,
        425, 450, 475, 500, 525, 550, 575, 600, 650, 700, 800, 900, 1000, 1200, 1400, 1600, 1800, 
        2000, 2400, 3000, 3500, 4000};
    private int pos_ = -1;
    private double[] testModelPrices_ = null;
    private bool TestNonMonotonicOK_ = false;
    #endregion test data

    #endregion data
  }
}