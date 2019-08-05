//
// Copyright (c)    2018. All rights reserved.
//
using NUnit.Framework;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  /// Test calculations on bonds with an ex-dividend period
  /// </summary>
  [TestFixture, Ignore("Unknown.  To investigate")]
  public class TestCustomBondPayments : ToolkitTestBase
  {
    public Bond RegularBond { get; set; }
    private readonly Dt issueDate_ = new Dt(15, 9, 2010);
    private readonly Dt maturityDate_ = new Dt(15, 9, 2015);
    private readonly Dt pricingDate_ = new Dt(15, 10, 2010);

    [OneTimeSetUp]
    public void Setup()
    {
      RegularBond = new Bond(issueDate_,
                      maturityDate_,
                      Currency.USD,
                      BondType.USCorp,
                      3.6 / 100.0,
                      DayCount.Thirty360,
                      CycleRule.Fifteenth,
                      Frequency.SemiAnnual,
                      BDConvention.Modified,
                      Calendar.NYB);

    }

    [Test]
    public void PreliminaryTesting()
    {
      DiscountCurve discountCurve = CreateDiscountCurve(issueDate_);

      // create a pricer for a trade settling on spot date
      var bondPricer1 = new BondPricer(RegularBond, pricingDate_, pricingDate_, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0,
                                      true)
                         {
                           QuotingConvention = QuotingConvention.FlatPrice,
                           MarketQuote = 1.0,
                           Notional = 10000.0
                         };
      Cashflow cashflow = bondPricer1.Cashflow;

      var ps = bondPricer1.GetPaymentSchedule(null, issueDate_);

      var customBond2 = (Bond)RegularBond.Clone();
      customBond2.CustomPaymentSchedule = ps;

      var bondPricer2 = new BondPricer(customBond2, pricingDate_, pricingDate_, discountCurve, null, 0, TimeUnit.None,
                                       0.0, 0.0, 0.0,
                                       true)
                          {
                            QuotingConvention = QuotingConvention.FlatPrice,
                            MarketQuote = 1.0,
                            Notional = 10000.0
                          };

      Cashflow cashflow2 = bondPricer2.Cashflow;

      var customBond3 = (Bond) RegularBond.Clone();
      customBond3.CustomPaymentSchedule = new PaymentSchedule();
      // include every other payment period
      bool b = false;
      foreach (Dt d in customBond2.CustomPaymentSchedule.GetPaymentDates())
      {
        if (b)
        {
          customBond3.CustomPaymentSchedule.AddPayments(
            CloneUtil.CloneToGenericList(customBond2.CustomPaymentSchedule.GetPaymentsOnDate(d).ToList()));
        }
        b = !b;
      }

      var bondPricer3 = new BondPricer(customBond3, pricingDate_, pricingDate_, discountCurve, null, 0, TimeUnit.None,
                                       0.0, 0.0, 0.0,
                                       true)
                          {
                            QuotingConvention = QuotingConvention.FlatPrice,
                            MarketQuote = 1.0,
                            Notional = 10000.0
                          };
      Cashflow cashflow3 = bondPricer3.Cashflow;

      var y1 = bondPricer1.YieldToMaturity();
      var y2 = bondPricer2.YieldToMaturity();
      var y3 = bondPricer3.YieldToMaturity();

      var fp1 = bondPricer1.FullPrice();
      var fp2 = bondPricer2.FullPrice();
      var fp3 = bondPricer3.FullPrice();

      var md1 = bondPricer1.ModDuration();
      var md2 = bondPricer2.ModDuration();
      var md3 = bondPricer3.ModDuration();

      var c1 = bondPricer1.Convexity();
      var c2 = bondPricer2.Convexity();
      var c3 = bondPricer3.Convexity();

      var ty1 = bondPricer1.TrueYield();
      var ty2 = bondPricer2.TrueYield();
      var ty3 = bondPricer3.TrueYield();

      int breakHere = 0;

    }


    private DiscountCurve CreateDiscountCurve(Dt asOf)
    {
      var calibrator = new DiscountBootstrapCalibrator(asOf, asOf)
                         {
                           SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const),
                           SwapCalibrationMethod = SwapCalibrationMethod.Extrap
                         };
      var curve = new DiscountCurve(calibrator)
                    {
                      DayCount = DayCount.Actual365Fixed,
                      Frequency = Frequency.Continuous,
                      Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
                      Ccy = Currency.USD
                    };
      curve.AddSwap("10Y", Dt.Add(asOf, "10Y"), 2.0 / 100.0, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None,
                    Calendar.None);
      curve.Fit();
      return curve;
    }
  }
}
