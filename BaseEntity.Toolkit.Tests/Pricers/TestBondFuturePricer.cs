//
// Copyright (c)    2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestBondFuturePricer
  {
    #region Tests

    /// <summary>
    /// Test CTD accrued
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, 
      Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =0.00879755 )]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, 
      Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =0.03067255)]
    public double CtdAccrued(int settle, int deliveryDate, double nominalCoupon, 
      double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, 
      DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, 
      double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, 
        futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, 
       repoDayCount, conversionFactor).CtdAccrued(), 8);
    }

    /// <summary>
    /// Test CTD full price
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, 
      Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =1.298798)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, 
      Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =1.290673)]
    public double CtdFullPrice(int settle, int deliveryDate, double nominalCoupon, 
      double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, 
      DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, 
      double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, 
        futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, 
       repoDayCount, conversionFactor).CtdFullPrice(), 6);
    }

    /// <summary>
    /// Test CTD forward accrued
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =0.03233696)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =0.03263122)]
    public double CtdForwardAccrued(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).CtdForwardAccrued(), 8);
    }

    /// <summary>
    /// Test CTD forward full price
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =1.321657)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =1.280165)]
    public double CtdForwardFullPrice(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).CtdForwardFullPrice(), 6);
    }

    /// <summary>
    /// Test Coupon Income from cash-and-carry
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =23.53940)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =45.70866)]
    public double CouponIncome(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).CouponIncome(), 5);
    }

    /// <summary>
    /// Test cost of funding for cash-and-carry
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =22.85884)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =34.05943)]
    public double CostOfFunding(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).CostOfFunding(repoRate, repoDayCount), 5);
    }

    /// <summary>
    /// Test implied repo from cash-and-carry
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =0.0584)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =0.0608)]
    public double ImpliedRepoRate(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).ImpliedRepoRate(repoDayCount), 4);
    }

    /// <summary>
    /// Test gross basis
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =0.00267200)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =0.00530600)]
    public double GrossBasis(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).GrossBasis(), 8);
    }

    /// <summary>
    /// Test carry basis
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =0.00068056)]
    public double CarryBasis(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).CarryBasis(repoRate, repoDayCount), 8);
    }

    /// <summary>
    /// Test net basis
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136,
      ExpectedResult =0.00199144)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803,
      ExpectedResult =-0.00634323)]
    public double NetBasis(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdPrice, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      return Math.Round(Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdPrice, repoRate, repoDayCount, conversionFactor).NetBasis(repoRate, repoDayCount), 8);
    }

    /// <summary>
    /// Test PV01 consistency
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803)]
    [NUnit.Framework.TestCase(20140730, 20140917, 0.06, 0.98,
      BondType.AUSGovt, 20240917, 20140917, 0.06, DayCount.Actual365Fixed, Frequency.SemiAnnual,
      0, 0.02, 0.05, DayCount.Actual360, 1.0)]
    public void Pv01Consistency(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdQuote, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      var pricer = Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdQuote, repoRate, repoDayCount, conversionFactor);
      var futuresPv01 = pricer.Pv01();
      var ctdPrice = pricer.CtdFullPrice();
      var bump = pricer.CtdPv01();
      var testPv01 = pricer.PercentageMarginValue(pricer.ModelPrice(ctdPrice + bump/2.0)) -
        pricer.PercentageMarginValue(pricer.ModelPrice(ctdPrice - bump/2.0));
      Assert.AreEqual(futuresPv01, testPv01, 1e-8, string.Format("Futures Pv01 ({0}) does not manual calc ({1})", futuresPv01, testPv01));
    }

    /// <summary>
    /// Test PV01 consistency with underlying bond
    /// </summary>
    /// <remarks>
    /// <note>Pv01 of CTD bond will be close but will not exactly match futures pv01</note>
    /// </remarks>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803)]
    [NUnit.Framework.TestCase(20140730, 20140917, 0.06, 0.98,
      BondType.AUSGovt, 20240917, 20140917, 0.06, DayCount.Actual365Fixed, Frequency.SemiAnnual,
      0, 0.02, 0.05, DayCount.Actual360, 1.0)]
    public void Pv01UlConsistency(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdQuote, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      var pricer = Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdQuote, repoRate, repoDayCount, conversionFactor);
      var bondPricer = new BondPricer(pricer.CtdBond, new Dt(settle), new Dt(settle), pricer.DiscountCurve,
        null, 0, TimeUnit.None, 0.0)
      {
        MarketQuote = ctdQuote,
        QuotingConvention = pricer.CtdQuotingConvention
      };
      var bondFv01 = bondPricer.Fv01(new Dt(deliveryDate)) / pricer.CtdConversionFactor;
      var futuresPv01 = pricer.Pv01();
      Assert.AreEqual(bondFv01, futuresPv01, 1e-5, string.Format("Bond Fwd Pv01 ({0}) does not match Futures Pv01 ({1})", bondFv01, futuresPv01));
    }

    /// <summary>
    /// Test Price01 consistency
    /// </summary>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803)]
    [NUnit.Framework.TestCase(20140730, 20140917, 0.06, 0.98,
      BondType.AUSGovt, 20240917, 20140917, 0.06, DayCount.Actual365Fixed, Frequency.SemiAnnual,
      0, 0.02, 0.05, DayCount.Actual360, 1.0)]
    public void Price01Consistency(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdQuote, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      var pricer = Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdQuote, repoRate, repoDayCount, conversionFactor);
      var futuresPrice01 = pricer.Price01();
      var ctdPrice = pricer.CtdFullPrice();
      const double bump = 0.01;
      var testPv01 = pricer.PercentageMarginValue(pricer.ModelPrice(ctdPrice + bump/2.0)) -
        pricer.PercentageMarginValue(pricer.ModelPrice(ctdPrice - bump/2.0));
      Assert.AreEqual(futuresPrice01, testPv01, 1e-8, string.Format("Futures Price01 ({0}) does not manual calc ({1})", futuresPrice01, testPv01));
    }

    /// <summary>
    /// Test Price01 consistency with underlying bond
    /// </summary>
    /// <remarks>
    /// <note>Price01 of CTD bond will be close but will not exactly match futures Price01</note>
    /// </remarks>
    [NUnit.Framework.TestCase(20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.29, 0.064, DayCount.Actual360, 1.3136)]
    [NUnit.Framework.TestCase(20000921, 20010330, 0.06, 0.98,
      BondType.USGovt, 20170515, 20000515, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual,
      425, 1.26, 0.05, DayCount.Actual360, 1.2803)]
    [NUnit.Framework.TestCase(20140730, 20140917, 0.06, 0.98,
      BondType.AUSGovt, 20240917, 20140917, 0.06, DayCount.Actual365Fixed, Frequency.SemiAnnual,
      0, 0.02, 0.05, DayCount.Actual360, 1.0)]
    public void Price01UlConsistency(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon, DayCount ctdDayCount, Frequency ctdFrequency,
      int ctdCalendar, double ctdQuote, double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      var pricer = Pricer(settle, deliveryDate, nominalCoupon, futuresPrice, ctdType, ctdMaturity, ctdIssue, ctdCoupon,
       ctdDayCount, ctdFrequency, ctdCalendar, ctdQuote, repoRate, repoDayCount, conversionFactor);
      var bondPricer = new BondPricer(pricer.CtdBond, new Dt(settle), new Dt(settle), pricer.DiscountCurve,
        null, 0, TimeUnit.None, 0.0)
      {
        MarketQuote = ctdQuote,
        QuotingConvention = pricer.CtdQuotingConvention
      };
      var bondFwdPrice01 = (bondPricer.FwdFullPrice(new Dt(deliveryDate), bondPricer.FullPrice() + 0.005) -
                            bondPricer.FwdFullPrice(new Dt(deliveryDate), bondPricer.FullPrice() - 0.005)) / pricer.CtdConversionFactor;
      var futuresPrice01 = pricer.Price01();
      Assert.AreEqual(bondFwdPrice01, futuresPrice01, 1e-5, string.Format("Bond Fwd Price01 ({0}) does not match Futures Price01 ({1})", bondFwdPrice01, futuresPrice01));
    }

#if NOTNOW
    /// <summary>
    /// Test BondFuturePricer
    /// Ref: Handbook of global fixed income calculations. p152
    /// </summary>
    [NUnit.Framework.TestCase("CME UST Dec'00", 20000921, 20001229, 0.06, 0.98,
      BondType.USGovt, 20200815, 20000815, 0.0875, DayCount.ActualActualBond, Frequency.SemiAnnual, 425, 1.29,
      0.064, DayCount.Actual360, 1.3136,
      0.00879755, 1.298798, 0.03233696, 1.353994,
      0.02353940, 0.02285884, 0.0584244,
      0.002672, 0.000680557, 0.001991443
      )]
    public void BondFuturePricer(string testName, int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon,
      DayCount ctdDayCount, Frequency ctdFrequency, int ctdCalendar, double ctdPrice,
      double repoRate, DayCount repoDayCount, double conversionFactor,
      double expectedAccrued, double expectedFullPrice, double expectedFwdAccrued, double expectedFwdFullPrice,
      double expectedCouponIncome, double expectedCostOfFunding, double expectedImpliedRepoRate,
      double expectedGrossBasis, double expectedCostOfCarry, double expectedNetBasis
      )
    {
      ToolkitConfigurator.Init();
      var future = new BondFuture(new Dt(deliveryDate), nominalCoupon);
      var bond = new Bond(new Dt(ctdIssue), new Dt(ctdMaturity), Currency.None, ctdType, ctdCoupon, ctdDayCount,
                          CycleRule.None, ctdFrequency, BDConvention.Following, new Calendar(ctdCalendar));
      var repoCurve = new DiscountCurve(new Dt(settle));
      double df = RateCalc.PriceFromRate(repoRate, new Dt(settle), new Dt(deliveryDate), repoDayCount, Frequency.None);
      repoCurve.Add(new Dt(deliveryDate), df);
      var pricer = new BondFuturePricer(future, new Dt(settle), new Dt(settle), 1.0, futuresPrice, repoCurve, bond, ctdPrice, Toolkit.Base.QuotingConvention.FlatPrice, conversionFactor);
      AssertAndContinue.AreEqual(expectedAccrued, pricer.CtdAccrued(), 1E-8, testName + ":Accrued");
      AssertAndContinue.AreEqual(expectedFullPrice, pricer.CtdFullPrice(), 1E-8, testName + ":Full Price");
      AssertAndContinue.AreEqual(expectedFwdAccrued, pricer.CtdForwardAccrued(), 1E-8, testName + ":Forward Accrued");
      AssertAndContinue.AreEqual(expectedFullPrice, pricer.CtdForwardFullPrice(), 1E-8, testName + ":Forward Full Price");
      AssertAndContinue.AreEqual(expectedCouponIncome, pricer.CouponIncome(), 1E-8, testName + ":Coupon Income");
      AssertAndContinue.AreEqual(expectedCostOfFunding, pricer.CostOfFunding(), 1E-8, testName + ":Cost of Funding");
      AssertAndContinue.AreEqual(expectedImpliedRepoRate, pricer.ImpliedRepoRate(repoDayCount), 1E-8, testName + ":Implied Repo Rate");
      AssertAndContinue.AreEqual(expectedGrossBasis, pricer.GrossBasis(), 1E-8, testName + ":Gross Basis");
      AssertAndContinue.AreEqual(expectedCostOfCarry, pricer.CarryBasis(), 1E-8, testName + ":Cost of Carry");
      AssertAndContinue.AreEqual(expectedNetBasis, pricer.NetBasis(), 1E-8, testName + ":Net Basis");
      AssertAndContinue.AssertNoErrors();
    }

    private class AssertAndContinue
    {
      public static void AreEqual(double expected, double actual, double delta, string msg)
      {
        try
        {
          NUnit.Framework.Assert.AreEqual(expected, actual, delta, msg);
        }
        catch (Exception ex)
        {
          Errors.AppendLine(ex.Message);
        }
      }
      public static void AssertNoErrors()
      {
        if( Errors.Length > 0 )
        {
          string msg = Errors.ToString();
          Errors = new StringBuilder();
          throw new Exception(msg);
        }
      }
      private static StringBuilder Errors = new StringBuilder();
    }
#endif

    private BondFuturePricer Pricer(int settle, int deliveryDate, double nominalCoupon, double futuresPrice,
      BondType ctdType, int ctdMaturity, int ctdIssue, double ctdCoupon,
      DayCount ctdDayCount, Frequency ctdFrequency, int ctdCalendar, double ctdQuote,
      double repoRate, DayCount repoDayCount, double conversionFactor
      )
    {
      ToolkitConfigurator.Init();
      var future = new BondFuture(new Dt(deliveryDate), nominalCoupon, 1000.0, 0.01);
      var bond = new Bond(new Dt(ctdIssue), new Dt(ctdMaturity), Currency.None, ctdType, ctdCoupon, ctdDayCount,
                          CycleRule.None, ctdFrequency, BDConvention.Following, new Calendar(ctdCalendar));
      var repoCurve = new DiscountCurve(new Dt(settle));
      var df = RateCalc.PriceFromRate(repoRate, new Dt(settle), new Dt(deliveryDate), repoDayCount, Frequency.None);
      repoCurve.Add(new Dt(deliveryDate), df);
      var quotingConvention = (ctdType == BondType.AUSGovt) ? QuotingConvention.Yield : QuotingConvention.FlatPrice;
      var pricer = new BondFuturePricer(future, new Dt(settle), new Dt(settle), 1.0, futuresPrice, repoCurve, bond, ctdQuote,
                                        quotingConvention, conversionFactor);
      return pricer;
    }

    #endregion Tests
  }
}
