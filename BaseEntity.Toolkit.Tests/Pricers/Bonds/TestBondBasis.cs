//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Reflection;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  [TestFixture, Smoke]
  public class TestBondBasis : ToolkitTestBase
  {
    #region SetUP

    [SetUp]
    public void Initialize()
    {
      // Get the discoutn curve
      discountCurve_ = new DiscountCurve(asOf_, 0.02);

      // Get the survival curve      
      Dt[] dates = new Dt[]
      {
        new Dt(20, 6, 2010), new Dt(20, 6, 2011), new Dt(20, 6, 2012), new Dt(20, 6, 2014), new Dt(20, 6, 2016)
      };
      double[] spreads = new double[] {800, 800, 800, 800, 800};

      survCurve_ = SurvivalCurve.FitCDSQuotes("NA", asOf_, settle_, ccy_, "", CDSQuoteType.ParSpread, 100.0,
                                              SurvivalCurveParameters.GetDefaultParameters(), discountCurve_, null,
                                              dates, spreads, new[] {0.4}, 0, new[] {Dt.Empty, Dt.Empty}, null, 0, true);
      
      // Get the bond
      BuildBond();

      // Get the bond pricer
      BuildBondPricer(marketPrice_);

      return;
    }

    #endregion SetUP


    [Test, Smoke]
    public void TestPositive_DiscountingAccruedFalse()
    {
      Type objectType = bondPricer_.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      property.SetValue(bondPricer_, false, null);

      using (new CheckStates(checkState_, new[] { bondPricer_ }))
      {
        double bondCDSBasis = bondPricer_.ImpliedCDSSpread();
        Assert.AreEqual(0.0397817848512895, bondCDSBasis, 1e-5, "Failed BondCDSBasis");
      }
    }

    [Test, Smoke]
    public void TestPositive_DiscountingAccruedTrue()
    {
      Type objectType = bondPricer_.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      property.SetValue(bondPricer_, true, null);

      using (new CheckStates(checkState_, new[] { bondPricer_ }))
      {
        double bondCDSBasis = bondPricer_.ImpliedCDSSpread();

        Assert.AreEqual(0.0398917273171291, bondCDSBasis, 1e-5, "Failed BondCDSBasis");
      }
    }

    [Test, Smoke]
    public void TestNegative_DiscountingAccruedFalse()
    {
      BuildBondPricer(90.0);

      Type objectType = bondPricer_.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      property.SetValue(bondPricer_, false, null);

      using (new CheckStates(checkState_, new[] { bondPricer_ }))
      {
        double bondCDSBasis = bondPricer_.ImpliedCDSSpread();
        Assert.AreEqual(-0.0278646014697511, bondCDSBasis, 1e-5, "Failed BondCDSBasis");
      }
    }

    [Test, Smoke]
    public void TestNegative_DiscountingAccruedTrue()
    {
      BuildBondPricer(90.0);

      Type objectType = bondPricer_.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      property.SetValue(bondPricer_, true, null);

      using (new CheckStates(checkState_, new[] { bondPricer_ }))
      {
        double bondCDSBasis = bondPricer_.ImpliedCDSSpread();
        Assert.AreEqual(-0.027499325330567, bondCDSBasis, 1e-5, "Failed BondCDSBasis");
      }
    } 
    
    #region helper
    /// <summary>
    ///  Build a bond product
    /// </summary>
    private void BuildBond()
    {
      bond_ = new Bond(bondIssueDate_, bondMaturity_, ccy_, bondType_,
                       coupon_, dayCount_, CycleRule.None, freq_, roll_, cal_);
      bond_.PeriodAdjustment = false;
      AmortizationUtil.ToSchedule(null, null, bond_.AmortizationSchedule);
      return;
    }

    /// <summary>
    ///  Build bond pricer
    /// </summary>
    private void BuildBondPricer(double marketPrice)
    {
      bondPricer_ = new BondPricer(bond_, asOf_, settle_, discountCurve_,
                                   survCurve_, 0, TimeUnit.None, -1, 0, 0, false);
      bondPricer_.Notional = notional_;
      bondPricer_.QuotingConvention = QuotingConvention.FlatPrice;
      bondPricer_.MarketQuote = marketPrice / 100;
      bondPricer_.SurvivalCurve = survCurve_;
      return;
    }
    #endregion helper

    #region data

    private Dt asOf_ = new Dt(29, 5, 2009);
    private Dt settle_ = new Dt(30, 5, 2009);
    private DiscountCurve discountCurve_;
    private SurvivalCurve survCurve_;
    private Bond bond_;
    private BondPricer bondPricer_;
    private double notional_ = 1000000;
    private double marketPrice_ = 110.0;
    private Dt bondIssueDate_ = new Dt(15, 7, 2008);
    private Dt bondMaturity_ = new Dt(15, 1, 2013);
    private BondType bondType_ = BondType.USCorp;
    private double coupon_ = 0.095;
    private Frequency freq_ = Frequency.SemiAnnual;
    private Calendar cal_ = Calendar.NYB;
    private BDConvention roll_ = BDConvention.Following;
    private Currency ccy_ = Toolkit.Base.Currency.USD;
    private DayCount dayCount_ = DayCount.ActualActualBond;
    private bool checkState_ = false;

    #endregion data
  }
}