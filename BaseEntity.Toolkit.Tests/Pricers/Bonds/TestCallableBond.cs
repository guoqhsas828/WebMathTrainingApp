//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Models.HullWhiteShortRates;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  /// Test Bond Calculations.
  /// </summary>
  /// <remarks>
  /// <para>This test will follow a series of cases reporting callable bond problems
  /// Related cases are: 15754, 15755, 15756, 15758, 15551, 15818, ... </para>
  /// <para>Data source: BlueCrest callable spreadsheet sent to Anuj under case 15755</para>
  /// </remarks>
  [TestFixture, Smoke]
  public class TestCallableBond : ToolkitTestBase
  {
    #region Test zero state prices
    private static readonly string TestDataDir = System.IO.Path.Combine(
      SystemContext.InstallDir, "toolkit","test","data");

    private static readonly string StepBackArgsFile
      = "TrinomialTree_StepBack_ZeroStatePrices.xml";

    [Test]
    public void ZeroStatePrices()
    {
      var args = BaseEntity.Toolkit.Util.XmlSerialization.ReadXmlFile<object[]>(
        System.IO.Path.Combine(TestDataDir, StepBackArgsFile));
      var values = TrinomialTreeObserver.StepBack((TrinomialJump[]) args[0],
        (double[]) args[1], (int) args[2], (double[]) args[3]);
      NUnit.Framework.Assert.That(values, Is.All.Not.NaN);
    }
    #endregion

    #region Call in the middle of coupon periods

    [Test]
    public static void CallOnNonCouponDate()
    {
      // Create a 10Y amortizing bond with annual payments
      Dt effective = new Dt(20150318), maturity = new Dt(20250318);

      // Large coupon with small discount rate, to make sure
      // the bond will be called at par on the first callable date.
      const double coupon = 0.1;
      var dc = new DiscountCurve(effective, 0.002);
      var callable = GetAmortizingBond(coupon, -1, effective, maturity);

      // We test the call inside each coupon period
      var payments = callable.GetPaymentSchedule()
        .OfType<InterestPayment>().OrderBy(p => p.PayDt).ToArray();
      for (int i = 0, count = payments.Length; i < count; ++i)
      {
        // Call in the middle of the period i
        var ip = payments[i];
        var days = Dt.Diff(ip.AccrualStart, ip.AccrualEnd)/2;
        if (days <= 0) days = 1;
        var date = Dt.AddDays(ip.AccrualStart, days, callable.Calendar);
        callable.CallSchedule.Clear();
        callable.CallSchedule.Add(Call(date, 1.0));

        // Calculate the callable bond Pv. 
        var callablePricer = new BondPricer(callable,
          effective, effective, dc, null, 0, TimeUnit.None, 0);
        var actual = callablePricer.ProductPv();

        // Create a comparable regular bond maturing on the call date.
        // Make sure it has the partial coupon period at the end.
        var regular = GetAmortizingBond(coupon, -1, effective, date,
          CashflowFlag.StubAtEnd | CashflowFlag.RespectAllUserDates);
        if (i > 0) regular.FirstCoupon = callable.FirstCoupon;
        var regularPricer = new BondPricer(regular,
          effective, effective, dc, null, 0, TimeUnit.None, 0);
        var expect = regularPricer.ProductPv();

        // We should have the same PVs in both cases.
        NUnit.Framework.Assert.AreEqual(expect, actual, 1E-9, "Period {0}", i);
      }
    }

    private static CallPeriod Call(Dt date, double price)
    {
      return new CallPeriod(date, date, price, 1000, OptionStyle.European, 0);
    }

    #endregion

    #region Amortizing bonds

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void AmortizingBond(int callStartPeriod)
    {
      Dt effective = new Dt(20150307), maturity = new Dt(20250307);
      const double coupon = 0.1;
      var dc = new DiscountCurve(effective, 0.002);

      // Create a callable amortizing bond which will
      // be called for certain on the first call date.
      var callable = GetAmortizingBond(
        coupon, callStartPeriod, effective, maturity);
      var cPricer = new BondPricer(callable,
        effective, effective, dc, null, 0, TimeUnit.None, 0);
      var actual = cPricer.ProductPv();

      // Create a comparable, regular amortizing bond
      // maturing on the first call date.
      var schedule = callable.Schedule;
      var regular = GetAmortizingBond(coupon, -1,
        effective, schedule.GetPeriodEnd(callStartPeriod));
      var rPricer = new BondPricer(regular,
        effective, effective, dc, null, 0, TimeUnit.None, 0);
      var expect = rPricer.ProductPv();

      // We should have the same PVs in both cases.
      NUnit.Framework.Assert.AreEqual(expect, actual, 1E-9);
      return;
    }

    private static Bond GetAmortizingBond(
      double coupon, int callStart,
      Dt effective, Dt maturity,
      CashflowFlag additionalFlags = CashflowFlag.None)
    {
      BondType type = BondType.EURCorp;
      Currency ccy = Currency.GBP;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar calendar = Calendar.LNB;
      Frequency freq = Frequency.Annual;
      BDConvention roll = BDConvention.Following;
      var bond = new Bond(effective, maturity, ccy, type,
        coupon, dayCount, CycleRule.None, freq, roll, calendar);
      bond.CashflowFlag |= additionalFlags;
      var sched = bond.Schedule;
      for (int i = 1, n = sched.Count; i < n; ++i)
      {
        var date = sched.GetPaymentDate(i-1) + 1;
        bond.AmortizationSchedule.Add(new Amortization(date,
          AmortizationType.PercentOfInitialNotional, 0.05));
        if (i == callStart)
        {
          bond.CallSchedule.Add(new CallPeriod(
            date, maturity, 1.0, 1000.0, OptionStyle.Bermudan, 0));
        }
      }

      return bond;
    }

    #endregion

    #region SetUP and Clean
    [OneTimeSetUp]
    public void Initialize()
    {
      ExtractInfo();
    }
    #endregion SetUp and Clean

    #region Tests
    [Test, Smoke]
    public void TestZSpreadsNotDependentOnSurvivalCurves()
    {
      // Calculate the zspreads with survival curves
      BuildAllBonds();
      includeSurvivalCurve = true;
      BuildAllBondPricers();
      double[] zSpreads_1 = Array.ConvertAll<BondPricer, double>(
        bondPricers,
        delegate(BondPricer bp)
        {
          double x = Double.NaN;
          try { x = bp.ImpliedZSpread() * 10000; }
          catch (Exception) { }
          finally { }
          return x;
        }
        );

      // Calculate the zspreads without survival curves
      includeSurvivalCurve = false;
      BuildAllBondPricers();
      double[] zSpreads_2 = Array.ConvertAll<BondPricer, double>(
        bondPricers,
        delegate(BondPricer bp)
        {
          double x = Double.NaN;
          try { x = bp.ImpliedZSpread() * 10000; }
          catch (Exception) { }
          finally { }
          return x;
        }
        );

      for (int i = 0; i < bondPricers.Length; i++)
      {
        Assert.AreEqual(zSpreads_1[i], zSpreads_2[i], 1e-8,
          bondPricers[i].Bond.Description + ": zspread depends on survival curve, wrong");
      }
      return;
    }
    
    /// <summary>
    ///  Test Zspread roundtrip. 
    ///  If useSurvivalCurve = false, the bond pricer always set up implied CDS curve to target market price
    ///  we cannot test zspread roundtrip by bumping discount curves and compare market and model price because
    ///  they'll be always equal.
    ///  To test, we use a flat survival curve with 1 as survival probability (same as setting useSurvivalCurve=false
    ///  but do not imnply a survival curve). Compare the market price (before bumping discount curves) and model
    ///  price (after bumping discount curves)
    /// </summary>
    [Test, Smoke]
    public void TestZSpreadRoundTrip()
    {
      includeSurvivalCurve = true;
      ignoreCall = false;
      DoZspreadRoundTrip();
      return;
    }

    [Test, Smoke]
    public void TestZSpreadRoundTripIgnoreCall()
    {
      includeSurvivalCurve = true;
      ignoreCall = true;
      DoZspreadRoundTrip();
      return;
    }

    [Test, Smoke]
    public void TestRSpreadRoundTrip()
    {
      BuildAllBonds();
      ignoreCall = false;
      // Rspread takes into acoount the survival curves
      // Note most of the bonds in CR spreadhseet cannot find rSpread
      // So build a bond pricer using mild conditions
      Bond bond = new Bond(new Dt(22, 9, 2004), new Dt(1, 10, 2014), Currency.USD, BondType.USCorp, 0.08625,
        DayCount.Thirty360, CycleRule.None, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      bond.FirstCoupon = new Dt(1, 4, 2005);
      Dt[] start = new Dt[] { new Dt(1, 10, 2009), new Dt(1, 10, 2010), new Dt(1, 10, 2011), new Dt(1, 10, 2012) };
      Dt[] end = new Dt[] { new Dt(30, 9, 2010), new Dt(30, 9, 2011), new Dt(30, 9, 2012), new Dt(1, 10, 2014) };
      double[] call = new double[] { 104.312, 102.875, 101.437, 100.000 };
      for (int i = 0; i < start.Length; i++)
      {
        if (call[i] > 0)
          bond.CallSchedule.Add(
            new CallPeriod(start[i], end[i], call[i] / 100.0, 1000.0, OptionStyle.American, 0));
      }

      bond.Description = "MildBond";
      bond.Validate();

      Dt pricingDate = new Dt(26, 3, 2009);
      DiscountCurve singleDiscountCurve = BuildDiscountCurve(pricingDate, Currency.USD, "", DayCount.Actual360, new string[] { "6 Month", "1 Year" },
        null, new double[] { 0.015, 0.015 }, DayCount.Actual360, null, null, null, FuturesCAMethod.None, 0.0, DayCount.Actual360,
        new Frequency[] { Frequency.SemiAnnual }, new string[] { "2 Year", "3 Year", "5 Year", "7 Year", "10 Year" }, 
        null, new double[] { 0.015, 0.015, 0.015, 0.015, 0.015 }, "single");
      SurvivalCurve singleSurvivalCurve = BuildSurvivalCurve(pricingDate, Currency.USD, "", DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
        Calendar.NYB, InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow, singleDiscountCurve, new string[]{"6 Month", "1 Year", "2 Year",
          "3 Year", "5 Year", "7 Year", "10 Year"}, null, new double[] { 500, 500, 500, 500, 500, 500, 500 }, new double[] { 0.3 }, 0, false, null, "singleSurvCurve");
      
      BondPricer pricer = new BondPricer(bond, pricingDate, new Dt(31, 3, 2009), singleDiscountCurve, singleSurvivalCurve, 0, 
        TimeUnit.None, 0.3, 0.3, 0.1, ignoreCall);
      pricer.Notional = 1000000.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 1.0;
      pricer.EnableZSpreadAdjustment = false;
      
      pricer.Validate();

      double rspread = pricer.CalcRSpread();
      double fullPrice = pricer.FullPrice();
      pricer.DiscountCurve = BuildDiscountCurve(pricingDate, Currency.USD, "", DayCount.Actual360, new string[] { "6 Month", "1 Year" },
        null, new double[] { 0.015 + rspread, 0.015 + rspread }, DayCount.Actual360, null, null, null, FuturesCAMethod.None, 0.0, DayCount.Actual360,
        new Frequency[] { Frequency.SemiAnnual }, new string[] { "2 Year", "3 Year", "5 Year", "7 Year", "10 Year" },
        null, new double[] { 0.015 + rspread, 0.015 + rspread, 0.015 + rspread, 0.015 + rspread, 0.015 + rspread }, "single");
      double rspreadRemaining = pricer.CalcRSpread() * 10000;
      Assert.AreEqual(0, rspreadRemaining / (rspread*10000), 0.02, "rspread roundtrip wrong");

      double fullModelPrice = pricer.FullModelPrice();
      Assert.AreEqual(1, fullPrice / fullModelPrice, 0.02, "rspread roundtrip wrong");
    }

    [Test, Smoke]
    public void TestRSpreadRoundTripIgnoreCall()
    {
      BuildAllBonds();
      ignoreCall = true;
      // Rspread takes into acoount the survival curves
      // Note most of the bonds in CR spreadhseet cannot find rSpread
      // So build a bond pricer using mild conditions
      Bond bond = new Bond(new Dt(22, 9, 2004), new Dt(1, 10, 2014), Currency.USD, BondType.USCorp, 0.08625,
        DayCount.Thirty360, CycleRule.None, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      bond.FirstCoupon = new Dt(1, 4, 2005);
      Dt[] start = new Dt[] { new Dt(1, 10, 2009), new Dt(1, 10, 2010), new Dt(1, 10, 2011), new Dt(1, 10, 2012) };
      Dt[] end = new Dt[] { new Dt(30, 9, 2010), new Dt(30, 9, 2011), new Dt(30, 9, 2012), new Dt(1, 10, 2014) };
      double[] call = new double[] { 104.312, 102.875, 101.437, 100.000 };
      for (int i = 0; i < start.Length; i++)
      {
        if (call[i] > 0)
          bond.CallSchedule.Add(
            new CallPeriod(start[i], end[i], call[i] / 100.0, 1000.0, OptionStyle.American, 0));
      }

      bond.Description = "MildBond";
      bond.Validate();

      Dt pricingDate = new Dt(26, 3, 2009);
      DiscountCurve singleDiscountCurve = BuildDiscountCurve(pricingDate, Currency.USD, "", DayCount.Actual360, new string[] { "6 Month", "1 Year" },
        null, new double[] { 0.015, 0.015 }, DayCount.Actual360, null, null, null, FuturesCAMethod.None, 0.0, DayCount.Actual360,
        new Frequency[] { Frequency.SemiAnnual }, new string[] { "2 Year", "3 Year", "5 Year", "7 Year", "10 Year" },
        null, new double[] { 0.015, 0.015, 0.015, 0.015, 0.015 }, "single");
      SurvivalCurve singleSurvivalCurve = BuildSurvivalCurve(pricingDate, Currency.USD, "", DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
        Calendar.NYB, InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow, singleDiscountCurve, new string[]{"6 Month", "1 Year", "2 Year",
          "3 Year", "5 Year", "7 Year", "10 Year"}, null, new double[] { 500, 500, 500, 500, 500, 500, 500 }, new double[] { 0.3 }, 0, false, null, "singleSurvCurve");

      BondPricer pricer = new BondPricer(bond, pricingDate, new Dt(31, 3, 2009), singleDiscountCurve, singleSurvivalCurve, 0,
        TimeUnit.None, 0.3, 0.3, 0.1, ignoreCall);
      pricer.Notional = 1000000.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 1.0;
      pricer.EnableZSpreadAdjustment = false;

      pricer.Validate();

      double rspread = pricer.CalcRSpread();
      double fullPrice = pricer.FullPrice();
      pricer.DiscountCurve = BuildDiscountCurve(pricingDate, Currency.USD, "", DayCount.Actual360, new string[] { "6 Month", "1 Year" },
        null, new double[] { 0.015 + rspread, 0.015 + rspread }, DayCount.Actual360, null, null, null, FuturesCAMethod.None, 0.0, DayCount.Actual360,
        new Frequency[] { Frequency.SemiAnnual }, new string[] { "2 Year", "3 Year", "5 Year", "7 Year", "10 Year" },
        null, new double[] { 0.015 + rspread, 0.015 + rspread, 0.015 + rspread, 0.015 + rspread, 0.015 + rspread }, "single");
      double rspreadRemaining = pricer.CalcRSpread() * 10000;
      Assert.AreEqual(0, rspreadRemaining / (rspread * 10000), 0.02, "rspread roundtrip wrong");

      double fullModelPrice = pricer.FullModelPrice();
      Assert.AreEqual(1, fullPrice / fullModelPrice, 0.02, "rspread roundtrip wrong");
    }

    [Test, Smoke]
    public void TestZspreadRspreadEquivalency()
    {
      // Zspread ~ Rspread when survival curve is 0-flat.
      BuildAllBonds();
      BuildAllBondPricers();
      for (int i = 0; i < bondPricers.Length; i++)
      {
        DiscountCurve disctCurve = null;
        if (bondCcy[i].ToString().Contains("USD"))
          disctCurve = discountCurve_USD_LIBOR_;
        else if (bondCcy[i].ToString().Contains("EUR"))
          disctCurve = discountCurve_EUR_LIBOR_;
        else if (bondCcy[i].ToString().Contains("GBP"))
          disctCurve = discountCurve_GBP_LIBOR_;
        double[] rates = new double[survivalCurveDates.Length];
        for (int j = 0; j < survivalCurveDates.Length; j++) rates[j] = 1e-5;
        bondPricers[i].SurvivalCurve = BuildSurvivalCurve(asOf_, bondCcy[i], "", bondDaycount[i],
          bondFreq[i], bondBDConvention[i], bondCalendar[i],
          InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow, disctCurve,
          survivalCurveTenors, survivalCurveDates, rates,
          new double[] { 0.3 }, 0, false, null, "singleSurvCurve" + i.ToString());
      }
      double[] zSpreads = Array.ConvertAll<BondPricer, double>(
        bondPricers,
        delegate(BondPricer bp)
        {
          double x = 0;
          try { x = bp.ImpliedZSpread() * 10000; }
          catch (Exception) { }
          finally { }
          return x;
        }
        );
      double[] rSpreads = Array.ConvertAll<BondPricer, double>(
              bondPricers,
              delegate(BondPricer bp)
              {
                double x = 0;
                try { x = bp.CalcRSpread() * 10000; }
                catch (Exception) { }
                finally { }
                return x;
              }
              );
      for (int i = 0; i < zSpreads.Length; i++)
        Assert.AreEqual(zSpreads[i], rSpreads[i], 0.1, "equivalency wrong");
    }
    #endregion Tests

    #region helpers

    private void ExtractInfo()
    {
      // Build discount curve USD_LIBOR
      discountCurve_USD_LIBOR_ = BuildDiscountCurve(asOf_, Currency.USD, "",
        mmDayCount_USD, mmTenors_USD, mmDates_USD, mmRates_USD,
        edfDayCount_USD, edfNames_USD, edfDates, edfPrices_USD, caMethod_USD, vol_USD,
        swapDayCount_USD, freq_USD, swapTenors_USD, swapDates, swapRates_USD, curveName_USD);
      // Build discount curve EUR_LIBOR
      discountCurve_EUR_LIBOR_ = BuildDiscountCurve(asOf_, Currency.EUR, "",
        mmDayCount_EUR, mmTenors_EUR, mmDates_EUR, mmRates_EUR,
        edfDayCount_EUR, edfNames_EUR, edfDates, edfPrices_EUR, caMethod_EUR, vol_EUR,
        swapDayCount_EUR, freq_EUR, swapTenors_EUR, swapDates, swapRates_EUR, curveName_EUR);
      // Build discount curve GBP_LIBOR
      discountCurve_GBP_LIBOR_ = BuildDiscountCurve(asOf_, Currency.GBP, "",
        mmDayCount_GBP, mmTenors_GBP, mmDates_GBP, mmRates_GBP,
        edfDayCount_GBP, edfNames_GBP, edfDates, edfPrices_GBP, caMethod_GBP, vol_GBP,
        swapDayCount_GBP, freq_GBP, swapTenors_GBP, swapDates, swapRates_GBP, curveName_GBP);

      int number = company.Length;
      number = recoveries.Length;

      // Get the survival curves list
      survivalCurves_ = SurvivalCurves();

      bondEffectiveDates = Array.ConvertAll<string, Dt>
        (effective, delegate(string s) { return Dt.FromStr(s, "%D"); });
      bondMaturities = Array.ConvertAll<string, Dt>
        (maturities, delegate(string s) { return Dt.FromStr(s, "%D"); });
      bondFirstCoupon = Array.ConvertAll<string, Dt>
        (firstCoupon, delegate(string s) { return Dt.FromStr(s, "%D"); });
      bondDaycount = Array.ConvertAll<string, DayCount>
        (dayCounts, delegate(string s) { return (DayCount)Enum.Parse(typeof(DayCount), s, true); });
      bondFreq = Array.ConvertAll<string, Frequency>
        (freqs, delegate(string s) { return (Frequency)Enum.Parse(typeof(Frequency), s, true); });
      bondBDConvention = Array.ConvertAll<string, BDConvention>
        (bdconvetions, delegate(string s) { return (BDConvention)Enum.Parse(typeof(BDConvention), s, true); });
      bondCalendar = Array.ConvertAll<string, Calendar>(calendars, delegate(string s)
      {
        object obj = null; Calendar cal = Calendar.None;
        try
        {
          obj = CalendarCalc.GetCalendar(s);
        }
        catch (Exception) { }
        finally
        {
          cal = (obj == null ? Calendar.None : (Calendar)obj);
        }
        return cal;
      }
      );

      bondCallStartDates = GetBondCallDates(callStarts);
      bondCallEndDates = GetBondCallDates(callEnds);
      GetBondSurvivalCurves();
      return;
    }

    // Build discount curves
    private DiscountCurve BuildDiscountCurve(Dt asOf, Currency ccy, string category, 
      DayCount mmDC, string[] mmTenors, Dt[] mmMat, double[] mmRates,
      DayCount edDC, string[] edNames, Dt[] edMat, double[] edPrices, FuturesCAMethod convexityAdj, double vol,
      DayCount swapDC, Frequency[] swapFreq, string[] swapTenors, Dt[] swapMat, double[] swapRates, string curveName)
    {
      InterpMethod interpMethod = InterpMethod.Weighted;
      ExtrapMethod extrapMethod = ExtrapMethod.Const;
      InterpMethod swapInterp = InterpMethod.Cubic;
      ExtrapMethod swapExtrap = ExtrapMethod.Const;

      DiscountBootstrapCalibrator calibrator =
        new DiscountBootstrapCalibrator(asOf, asOf);
      calibrator.SwapInterp = InterpFactory.FromMethod(swapInterp, swapExtrap);
      calibrator.FuturesCAMethod = convexityAdj;

      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Ccy = ccy;
      curve.Category = category ?? "None";
      curve.Name = curveName;

      if (mmMat == null || mmMat.Length == 0)
      {
        mmMat = new Dt[mmTenors.Length];
        for (int i = 0; i < mmTenors.Length; i++)
          mmMat[i] = String.IsNullOrEmpty(mmTenors[i]) ? Dt.Empty : Dt.Add(asOf, mmTenors[i]);
      }
      // Add MM rates
        for (int i = 0; i < mmTenors.Length; i++)
          if (mmRates[i] > 0.0)
            curve.AddMoneyMarket(mmTenors[i], mmMat[i], mmRates[i], mmDC);

      // Add ED Futures
      /*

        for (int i = 0; i < edNames.Length; i++)
          if (edPrices[i] > 0.0)
            curve.AddEDFuture(edNames[i], edMat[i], edDC, edPrices[i]);
       */

        if (swapMat == null || swapMat.Length == 0)
        {
          swapMat = new Dt[swapTenors.Length];
          for (int i = 0; i < swapTenors.Length; i++)
            swapMat[i] = String.IsNullOrEmpty(swapTenors[i]) ? Dt.Empty : Dt.Add(asOf, swapTenors[i]);
        }
      // Add swap rates
        for (int i = 0; i < swapTenors.Length; i++)
          if (swapRates[i] > 0.0)
            curve.AddSwap(swapTenors[i], swapMat[i], swapRates[i], swapDC,
              swapFreq.Length==1?swapFreq[0]:swapFreq[i], BDConvention.None, Calendar.None);
      
      // Any volatility curve
      calibrator.VolatilityCurve = new VolatilityCurve(asOf, vol);
      curve.Fit();
      return curve;
    }

    DiscountCurve[] GetModifiedDiscountCurves(double[] zSpreads)
    {
      // Save the origional discount curves
      discountCurve_USD_LIBOR_Saved = (DiscountCurve)discountCurve_USD_LIBOR_.Clone();
      discountCurve_EUR_LIBOR_Saved = (DiscountCurve)discountCurve_EUR_LIBOR_.Clone();
      discountCurve_GBP_LIBOR_Saved = (DiscountCurve)discountCurve_EUR_LIBOR_.Clone();

      DiscountCurve[] modifiedDiscountCurves = new DiscountCurve[zSpreads.Length];
      for (int i = 0; i < zSpreads.Length; i++)
      {
        string curveName = bondPricers[i].DiscountCurve.Name;
        double[] mmRatesMod;
        double[] edfPricesMod;
        double[] swapRatesMod;
        if (zSpreads[i] == 0)
          continue;
        if (curveName.Contains("USD"))
        {
          mmRatesMod = (double[])mmRates_USD.Clone();
          edfPricesMod = (double[])edfPrices_USD.Clone();
          swapRatesMod = (double[])swapRates_USD.Clone();
          for (int j = 0; j < mmRatesMod.Length; j++)
            mmRatesMod[j] += zSpreads[i] / 10000.0;
          for (int j = 0; j < edfPricesMod.Length; j++)
            edfPricesMod[j] += zSpreads[i] / 10000.0;
          for (int j = 0; j < swapRatesMod.Length; j++)
            swapRatesMod[j] += zSpreads[i] / 10000.0;
          modifiedDiscountCurves[i] = BuildDiscountCurve(asOf_, Currency.USD, "",
              mmDayCount_USD, mmTenors_USD, mmDates_USD, mmRatesMod,
              edfDayCount_USD, edfNames_USD, edfDates, edfPricesMod, caMethod_USD, vol_USD,
              swapDayCount_USD, freq_USD, swapTenors_USD, swapDates, swapRatesMod, bonds[i].Description + "_discountCurve");
        }
        else if (curveName.Contains("EUR"))
        {
          mmRatesMod = (double[])mmRates_EUR.Clone();
          edfPricesMod = (double[])edfPrices_EUR.Clone();
          swapRatesMod = (double[])swapRates_EUR.Clone();
          for (int j = 0; j < mmRatesMod.Length; j++)
            mmRatesMod[j] += zSpreads[i] / 10000.0;
          for (int j = 0; j < edfPricesMod.Length; j++)
            edfPricesMod[j] += zSpreads[i] / 10000.0;
          for (int j = 0; j < swapRatesMod.Length; j++)
            swapRatesMod[j] += zSpreads[i] / 10000.0;
          modifiedDiscountCurves[i] = BuildDiscountCurve(asOf_, Currency.EUR, "",
              mmDayCount_EUR, mmTenors_EUR, mmDates_EUR, mmRatesMod,
              edfDayCount_EUR, edfNames_EUR, edfDates, edfPricesMod, caMethod_EUR, vol_EUR,
              swapDayCount_EUR, freq_EUR, swapTenors_EUR, swapDates, swapRatesMod, bonds[i].Description + "_discountCurve");
        }
        else
        {
          mmRatesMod = (double[])mmRates_GBP.Clone();
          edfPricesMod = (double[])edfPrices_GBP.Clone();
          swapRatesMod = (double[])swapRates_GBP.Clone();
          for (int j = 0; j < mmRatesMod.Length; j++)
            mmRatesMod[j] += zSpreads[i] / 10000.0;
          for (int j = 0; j < edfPricesMod.Length; j++)
            edfPricesMod[j] += zSpreads[i] / 10000.0;
          for (int j = 0; j < swapRatesMod.Length; j++)
            swapRatesMod[j] += zSpreads[i] / 10000.0;
          modifiedDiscountCurves[i] = BuildDiscountCurve(asOf_, Currency.GBP, "",
              mmDayCount_GBP, mmTenors_GBP, mmDates_GBP, mmRatesMod,
              edfDayCount_GBP, edfNames_GBP, edfDates, edfPricesMod, caMethod_GBP, vol_GBP,
              swapDayCount_GBP, freq_GBP, swapTenors_GBP, swapDates, swapRatesMod, bonds[i].Description + "_discountCurve");
        }
      }
      return modifiedDiscountCurves;     
    }

    // Build a single survival curve
    private SurvivalCurve BuildSurvivalCurve(Dt asOf, Currency ccy, string category, DayCount cdsDayCount, Frequency cdsFreq,
      BDConvention cdsRoll, Calendar cdsCalendar, InterpMethod interpMethod, ExtrapMethod extrapMethod, NegSPTreatment nspTreatment,
      DiscountCurve discountCurve, string[] tenorNames, Dt[] tenorDates, double[] premiums, double[] recoveries, double recoveryDisp,
      bool forceFit, Dt[] eventDates, string cdsCurveName
      )
    {
      SurvivalCurve curve = SurvivalCurve.FitCDSQuotes(asOf, ccy, category,
        cdsDayCount, cdsFreq, cdsRoll, cdsCalendar,
        interpMethod, extrapMethod, nspTreatment, discountCurve,
        tenorNames, tenorDates, null, premiums,
        recoveries, recoveryDisp, forceFit, eventDates);
      curve.Name = cdsCurveName;
      return curve;
    }

    // Build survival curves pool
    private SurvivalCurve[] SurvivalCurves()
    {
      SurvivalCurve[] curves = new SurvivalCurve[category.Length];
      for (int i = 0; i < category.Length; i++)
      {
        double[] quotes = new double[survivalCurveTenors.Length];
        for (int j = 0; j < survivalCurveTenors.Length; j++)
          quotes[j] = cdsQuotes[i, j];
        DiscountCurve disctCurve = null;
        if (ccy[i].ToString().Contains("USD"))
          disctCurve = discountCurve_USD_LIBOR_;
        else if (ccy[i].ToString().Contains("EUR"))
          disctCurve = discountCurve_EUR_LIBOR_;
        else if (ccy[i].ToString().Contains("GBP"))
          disctCurve = discountCurve_GBP_LIBOR_;
        curves[i] = BuildSurvivalCurve(asOf_, ccy[i], category[i], cdsDayCount, cdsFreq, cdsRoll, calendar[i],
          cdsInterp, cdsExtrap, negAllow, disctCurve, survivalCurveTenors, survivalCurveDates, quotes, new double[] { recoveries[i] }, 
          0, false, null, survivalCurveNames[i]);
      }
      return curves;
    }

    // Get the bond call start and end dates from [,] of strings
    private List<Dt[]> GetBondCallDates(string[,] callDatesString)
    {
      int len = callDatesString.GetLength(0);
      List<Dt[]> dates = new List<Dt[]>();
      for(int i = 0; i < len; i++)
      {
        if (callDatesString[i, 0] == "1/0/00")
          dates.Add(null);
        else
        {
          List<Dt> list = new List<Dt>();
          for (int j = 0; j < callDatesString.GetLength(1); j++)
          {
            if (callDatesString[i, j] != "1/0/00")
              list.Add(Dt.FromStr(callDatesString[i,j], "%D"));
          }
          dates.Add(list.ToArray());
        }
      }
      return dates;
    }

    // Build all bonds
    private void BuildAllBonds()
    {
      int numBonds = bondNames.Length;
      bonds = new Bond[numBonds];

      for (int i = 0; i < numBonds; i++)
      {
        bonds[i] = new Bond(bondEffectiveDates[i], bondMaturities[i], bondCcy[i], BondType.USCorp,
          bondFloating[i] ? bondCoupon[i] / 10000.0 : bondCoupon[i], bondDaycount[i], CycleRule.None, bondFreq[i], 
          bondBDConvention[i], bondCalendar[i]);
        bonds[i].PeriodAdjustment = false;
        if (!bondFirstCoupon[i].IsEmpty())
          bonds[i].FirstCoupon = bondFirstCoupon[i];
        if (bondFloating[i])
        {
          bonds[i].Index = "LIBOR";
          bonds[i].ReferenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex(bonds[i].Index, bonds[i].Freq, bonds[i].Ccy,
                                                                           bonds[i].DayCount, bonds[i].Calendar, 0);

          double[] floatingCoupons = Toolkit.Base.Utils.Scale(new double[] { }, 1 / 10000.0);
          CouponPeriodUtil.ToSchedule(new Dt[] { }, floatingCoupons, bonds[i].CouponSchedule);

        }
        if (bondCallStartDates[i] != null)
        {
          for (int j = 0; j < bondCallStartDates[i].Length; j++)
          {
            if (bondCallPrices[i, j] > 0)
              bonds[i].CallSchedule.Add(
                new CallPeriod(bondCallStartDates[i][j], bondCallEndDates[i][j], bondCallPrices[i, j] / 100.0, 1000.0, OptionStyle.American, 0));
          }
        }
        bonds[i].Description = bondNames[i];
        bonds[i].Validate();
      }
      return;
    }

    // Build all bond pricers
    private void BuildAllBondPricers()
    {
      bondPricers = new BondPricer[bonds.Length];
      for (int i = 0; i < bonds.Length; i++)
      {
        DiscountCurve dCurve = bondDiscountCurvesIndex[i] == 0 ? discountCurve_USD_LIBOR_ :
          (bondDiscountCurvesIndex[i] == 1 ? discountCurve_EUR_LIBOR_ : discountCurve_GBP_LIBOR_);
        BondPricer pricer = new BondPricer(bonds[i], asOf_, bondSettleDate, dCurve, includeSurvivalCurve ? bondSurvivalCurves[i] : null,
          0, TimeUnit.None, bondRecoveries[i], bondMeanReversion, bondSigma, ignoreCall);

        pricer.Notional = bondNotionals[i];
        if (bonds[i].Floating)
        {
          pricer.ReferenceCurve = bondDiscountCurvesIndex[i] == 0 ? discountCurve_USD_LIBOR_ : (bondDiscountCurvesIndex[i] == 1 ? discountCurve_EUR_LIBOR_ : discountCurve_GBP_LIBOR_);
          if (bondCurrentCoupon[i] < bonds[i].Coupon)
            throw new ArgumentException("The current coupon must be at least as large as the spread(over reference curve)");
          pricer.CurrentRate = bondCurrentCoupon[i];
        }
        pricer.QuotingConvention = QuotingConvention.FlatPrice;
        if (bondMarketQuotes[i] > 0.0)
        {
          pricer.MarketQuote = bondMarketQuotes[i] / 100;
        }

        if (!includeSurvivalCurve && bondRecoveries[i] >= 0.0 && bondMarketQuotes[i] > 0.0)
        {
          // Initialize flat hazard rate curve at h = 0.0 @ R = recovery rate
          double h = 0.0;
          SurvivalCurve flatHcurve = new SurvivalCurve(asOf_, h);
          flatHcurve.Calibrator = new SurvivalFitCalibrator(asOf_, bondSettleDate, bondRecoveries[i], dCurve);
          pricer.SurvivalCurve = flatHcurve;
          // find flat curve to match market quote
          pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve(bondRecoveries[i]);

          // Force survival curve to be calibrated
          if (forceSurvivalCalibration && pricer.SurvivalCurve == null)
            throw new Exception("A survival curve could not be calibrated for this bond against the market price!");

          // Setup curve name
          if (pricer.SurvivalCurve != null)
            pricer.SurvivalCurve.Name = pricer.Product.Description + "_Curve";
        }

        pricer.EnableZSpreadAdjustment = useZspreadInSensitivities;
        pricer.Validate();
        bondPricers[i] = pricer;
      }
      return;
    }

    // Get bond survival curves from their names
    private void GetBondSurvivalCurves()
    {
      bondSurvivalCurves = new SurvivalCurve[bondSurvivalCurvesNames.Length];
      for(int i = 0; i < bondSurvivalCurvesNames.Length; i++)
        for (int j = 0; j < survivalCurves_.Length; j++)
        {
          if (survivalCurves_[j].Name.Contains(bondSurvivalCurvesNames[i]))
            bondSurvivalCurves[i] = survivalCurves_[j];
        }
      return;
    }

    private void DoZspreadRoundTrip()
    {
      BuildAllBonds();
      // Build flat surviavl curve with 1 survival probability
      BuildAllBondPricers();
      for (int i = 0; i < bondPricers.Length; i++)
      {
        DiscountCurve disctCurve = null;
        if (bondCcy[i].ToString().Contains("USD"))
          disctCurve = discountCurve_USD_LIBOR_;
        else if (bondCcy[i].ToString().Contains("EUR"))
          disctCurve = discountCurve_EUR_LIBOR_;
        else if (bondCcy[i].ToString().Contains("GBP"))
          disctCurve = discountCurve_GBP_LIBOR_;
        double[] rates = new double[survivalCurveDates.Length];
        for (int j = 0; j < survivalCurveDates.Length; j++) rates[j] = 1e-5;
        bondPricers[i].SurvivalCurve = BuildSurvivalCurve(asOf_, bondCcy[i], "", bondDaycount[i],
          bondFreq[i], bondBDConvention[i], bondCalendar[i],
          InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow, disctCurve,
          survivalCurveTenors, survivalCurveDates, rates,
          new double[] { 0.3 }, 0, false, null, "singleSurvCurve" + i.ToString());
      }
      double[] zSpreads = Array.ConvertAll<BondPricer, double>(
        bondPricers,
        delegate(BondPricer bp)
        {
          double x = 0;
          try { x = bp.ImpliedZSpread() * 10000; }
          catch (Exception) { }
          finally { }
          return x;
        }
        );

      double[] fullPrices = Array.ConvertAll<BondPricer, double>(
        bondPricers,
        delegate(BondPricer b) { double x = Double.NaN; try
        {
          x = b.IsProductActive()
            ? b.FullPrice()*100
            : 0.0;
        } catch (Exception) { } finally { } return x; }
        );

      DiscountCurve[] origionalCurves = Array.ConvertAll<BondPricer, DiscountCurve>(
        bondPricers, delegate(BondPricer bp) { return (DiscountCurve)bp.DiscountCurve.Clone(); });

      DiscountCurve[] modifiedDiscountCurves = GetModifiedDiscountCurves(zSpreads);
      for (int i = 0; i < bondPricers.Length; i++)
        bondPricers[i].DiscountCurve = modifiedDiscountCurves[i];

      double[] modelFullPrices = Array.ConvertAll<BondPricer, double>(
        bondPricers,
        delegate(BondPricer b) { double x = 100; try { x = b.FullModelPrice() * 100; } catch (Exception) { } finally { } return x; }
      );

      for (int i = 0; i < bondPricers.Length; i++)
      {
        Assert.AreEqual(fullPrices[i], modelFullPrices[i], 2.5, "zSpread roundtrip is wrong");
      }

      double[] zSpreadsRemaining = Array.ConvertAll<BondPricer, double>(
        bondPricers,
        delegate(BondPricer bp)
        {
          double x = 0;
          try { x = bp.ImpliedZSpread() * 10000; }
          catch (Exception) { }
          finally { }
          return x;
        }
        );

      for (int i = 0; i < bondPricers.Length; i++)
      {
        Assert.AreEqual(0,
          zSpreads[i] != 0 ? Math.Abs(zSpreadsRemaining[i] / zSpreads[i]) : 0, 1e-1, "zSpread roundtrip is wrong");
      }
    }
    #endregion helpers

    #region Data

    #region discount curve data
    // Since the qDiscountDataBootstrap method in 9.1.5 only takes into account the
    // money market and swap rates (excluding eurodollar futures prices), I hard-coded
    // the disocunt curve data copied from BlueCrest spreadsheet BC2_copy.xls.

    //Money market rates information
    private Dt asOf_ = new Dt(6 , 4 , 2009);  //April 6th, 2009
    private double[] mmRates_USD = new double[] { 0.0029063, 0.0045938, 0.005225, 0.0123188 };
    private Dt[] mmDates_USD = new Dt[] {new Dt(7, 4, 2009), new Dt(13, 4, 2009), new Dt(6, 5, 2009), new Dt(6, 7, 2009) };
    private string[] mmTenors_USD = new string[] {"1 D", "1 Wk",	"1 M",	"3 M"};
    private double[] mmRates_EUR = new double[] { 0.01019, 0.01155, 0.01538};
    private Dt[] mmDates_EUR = new Dt[] {new Dt(13, 4, 2009), new Dt(6, 5, 2009), new Dt(6, 7, 2009) };
    private string[] mmTenors_EUR = new string[] { "1 Wk", "1 M", "3 M" };
    private double[] mmRates_GBP = new double[] { 0.0066375, 0.0107188, 0.016975 };
    private Dt[] mmDates_GBP = new Dt[] { new Dt(13, 4, 2009), new Dt(6, 5, 2009), new Dt(6, 7, 2009) };
    private string[] mmTenors_GBP = new string[] { "1 Wk", "1 M", "3 M" };
    DayCount mmDayCount_USD = DayCount.Actual360;
    DayCount mmDayCount_EUR = DayCount.Actual360;
    DayCount mmDayCount_GBP = DayCount.Actual365Fixed;

    // EuroDollar futures information
    private Dt[] edfDates = new Dt[] {new Dt(17, 6, 2009), new Dt(16, 9, 2009), 
      new Dt(16, 12, 2009), new Dt(17, 3, 2010), new Dt(16, 6, 2010), new Dt(15, 9, 2010) };
    private double[] edfPrices_USD = new double[] {0.9875, 0.98745, 0.98575, 0.98510, 0.98340, 0.98165 };
    private double[] edfPrices_EUR = new double[] { 0.98635, 0.98575, 0.98375, 0.98250, 0.98025, 0.97800 };
    private double[] edfPrices_GBP = new double[] { 0.98470, 0.98440, 0.98210, 0.98050, 0.97810, 0.97550 };
    private string[] edfNames_USD = new string[] { "M9",	"U9",	"Z9",	"H0",	"M0",	"U0"};
    private string[] edfNames_EUR = new string[] { "M9", "U9", "Z9", "H0", "M0", "U0" };
    private string[] edfNames_GBP = new string[] { "M9", "U9", "Z9", "H0", "M0", "U0" };
    DayCount edfDayCount_USD = DayCount.Actual360;
    DayCount edfDayCount_EUR = DayCount.Actual360;
    DayCount edfDayCount_GBP = DayCount.Actual365Fixed;
    
    // Swap rates information
    private Dt[] swapDates = new Dt[] {
      new Dt(6, 4, 2011), new Dt(6, 4, 2012), new Dt(6, 4, 2013), new Dt(6, 4, 2014), new Dt(6, 4, 2015), new Dt(6, 4, 2016), 
      new Dt(6, 4, 2017), new Dt(6, 4, 2018), new Dt(6, 4, 2019), new Dt(6, 4, 2021), new Dt(6, 4, 2024), new Dt(6, 4, 2029), 
      new Dt(6, 4, 2034), new Dt(6, 4, 2039)};
    private double[] swapRates_USD = new double[] {0.015485,	0.018547,	0.02156,	0.0238875,	0.02578,	0.027295,	0.028495,	
                                                   0.029485,	0.030325,	0.0318,	0.033315,	0.03399,	0.03422,	0.0345};
    private double[] swapRates_EUR = new double[] {0.01991,	0.0234,	0.02619,	0.02846,	0.03034,	0.03191,	0.03316,	0.03425,	
                                                   0.0352,	0.036815,	0.03868,	0.039565,	0.038645,	0.037525};
    private double[] swapRates_GBP = new double[] {0.0220535,	0.02588,	0.028865,	0.03109,	0.032665,	0.03398,	0.035055,	0.03595,	
                                                   0.0367,	0.03832,	0.039845,	0.03976,	0.03877,	0.03823};
    private string[] swapTenors_USD = new string[] { "3y", "3y", "4y", "5y", "6y", "7y", "8y", "9y", "10y", "12y", "15y", "20y", "25y", "30y"};
    private string[] swapTenors_EUR = new string[] { "3y", "3y", "4y", "5y", "6y", "7y", "8y", "9y", "10y", "12y", "15y", "20y", "25y", "30y" };
    private string[] swapTenors_GBP = new string[] { "3y", "3y", "4y", "5y", "6y", "7y", "8y", "9y", "10y", "12y", "15y", "20y", "25y", "30y" };
    DayCount swapDayCount_USD = DayCount.Thirty360;
    DayCount swapDayCount_EUR = DayCount.Thirty360;
    DayCount swapDayCount_GBP = DayCount.Actual365Fixed;

    private string curveName_USD = "USD_LIBOR";
    private string curveName_EUR = "EUR_LIBOR";
    private string curveName_GBP = "GBP_LIBOR";

    Frequency[] freq_USD = new Frequency[] { Frequency.SemiAnnual };
    Frequency[] freq_EUR = new Frequency[] { Frequency.Annual};
    Frequency[] freq_GBP = new Frequency[] { Frequency.SemiAnnual };

    FuturesCAMethod caMethod_USD = FuturesCAMethod.Hull; double vol_USD = 0.012;
    FuturesCAMethod caMethod_EUR = FuturesCAMethod.Hull; double vol_EUR = 0.012;
    FuturesCAMethod caMethod_GBP = FuturesCAMethod.None; double vol_GBP = 0.000;

    private DiscountCurve discountCurve_USD_LIBOR_ = null;
    private DiscountCurve discountCurve_EUR_LIBOR_ = null;
    private DiscountCurve discountCurve_GBP_LIBOR_ = null;
    private DiscountCurve discountCurve_USD_LIBOR_Saved = null;
    private DiscountCurve discountCurve_EUR_LIBOR_Saved = null;
    private DiscountCurve discountCurve_GBP_LIBOR_Saved = null;

    #endregion discount curve data

    #region CDS curve data
    // Exactly copied from BlueCrest spreadsheet
    #region company
    string[] company = new string[]{
      "HET-HOC",	"JNY",	"NRBS",	"LEN",	"GROHE",	"NXP",	"MESSA",	"NT",	"HCA",	"AIND-LYON",	"NSINO",  "COGN",
      "F-MotCrLLC",	"AMD",	"BSC",	"F",	"AXP",	"TRWAuto",	"CIT",	"THC",	"STENA",	"CIT",	"BSC",	"BSC",	"MER",	
      "COF",	"PMI",	"DHI",	"TOL",	"PHM",	"CCR-HomeLoans",	"MGIC",	"IKB",	"IKB",	"CCR",	"CCR-HomeLoans",	"WM",	
      "WM-Bank",	"C",	"GLTNR",	"ARGENT",	"FHLMC",	"FNMA",	"RYL",	"SFI",	"GCI",	"SPGIM",	"LUV",	"SLMA",	"AXL-Inc",	
      "CTX",	"TXU-USHldg",	"INTEL",	"LEH",	"UIS",	"MRO",	"WM-Bank",	"RCL",	"GLTNR",	"GLTNR",	"WB",	"CHTR-Holdings",	
      "CCR",	"WFC",	"MAT",	"MOS",	"KINGFI",	"LEH",	"TRWAuto",	"GS",	"MER",	"BPLN"
    };
    #endregion company
    #region category
    string[] category = new string[]{
      "Consumer Services",	"Consumer Goods",	"Financials",	"Consumer Goods",	"Industrials",	"Technology",	"Basic Materials",	
      "Technology",	"Health Care",	"Basic Materials",	"Basic Materials",	"Basic Materials",	"Financials",	"Technology",	
      "Financials",	"Consumer Goods",	"Financials",	"Consumer Goods",	"Financials",	"Health Care",	"Industrials",	"Financials",	
      "Financials",	"Financials",	"Financials",	"Financials",	"Financials",	"Consumer Goods",	"Consumer Goods",	"Consumer Goods",	
      "Financials",	"Financials",	"Financials",	"Financials",	"Financials",	"Financials",	"Financials",	"Financials",	"Financials",	
      "Financials",	"Government",	"Government",	"Government",	"Consumer Goods",	"Financials",	"Consumer Services",	"Consumer Services",	
      "Consumer Services",	"Financials",	"Consumer Goods",	"Consumer Goods",	"Utilities",	"Telecommunications",	"Financials",	
      "Technology",	"Oil & Gas",	"Financials",	"Consumer Services",	"Financials",	"Financials",	"Financials",	"Consumer Services",	
      "Financials",	"Financials",	"Consumer Goods",	"Basic Materials",	"Consumer Services",	"Financials",	"Consumer Goods",	"Financials",	
      "Financials",	"Oil & Gas"
    };
    #endregion category
    static Currency USD_ = Currency.USD;
    static Currency EUR_ = Currency.EUR;
    static Currency GBP_ = Currency.GBP;
    #region currencies
    Currency[] ccy = new Currency[]{
      USD_,	USD_,	EUR_,	USD_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	
      EUR_,	USD_,	EUR_,	USD_,	EUR_,	GBP_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	GBP_,	EUR_,	EUR_,	
      USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	
      USD_,	USD_,	GBP_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	EUR_
    };
    #endregion currencies
    static Calendar NYB__ = Calendar.NYB;
    static Calendar EUR__ = Calendar.None;
    static Calendar GBP__ = Calendar.None;
    #region calendar
    Calendar[] calendar = new Calendar[]{
      NYB__,	NYB__,	EUR__,	NYB__,	EUR__,	EUR__,	EUR__,	NYB__,	NYB__,	EUR__,	EUR__,	EUR__,	
      NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	EUR__,	EUR__,	NYB__,	EUR__,	NYB__,	EUR__,	GBP__,	
      EUR__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	EUR__,	GBP__,	EUR__,	EUR__,	
      NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	EUR__,	NYB__,	
      NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	GBP__,	EUR__,	EUR__,	EUR__,	
      NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	NYB__,	EUR__,	EUR__,	NYB__,	NYB__,	NYB__,	EUR__
    };
    #endregion calendar
    #region survival curve names pool
    string[] survivalCurveNames = new string[]{
      "HET-HOC USD SNRFOR MR",	"JNY USD SNRFOR MR",	"NRBS EUR SNRFOR MM",	"LEN USD SNRFOR MR",	"GROHE EUR SNRFOR MM",	"NXP EUR SNRFOR MM",	
      "MESSA EUR SNRFOR MM",	"NT USD SNRFOR MR",	"HCA USD SNRFOR MR",	"AIND-LYON EUR SNRFOR MM",	"NSINO EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	
      "F-MotCrLLC USD SNRFOR MR",	"AMD USD SNRFOR MR",	"BSC USD SNRFOR MR",	"F USD SNRFOR MR",	"AXP USD SNRFOR MR",	"TRWAuto EUR SNRFOR XR",	
      "CIT EUR SNRFOR MM",	"THC USD SNRFOR MR",	"STENA EUR SNRFOR MM",	"CIT USD SNRFOR MR",	"BSC EUR SNRFOR MM",	"BSC GBP SNRFOR MR",	
      "MER EUR SNRFOR MM",	"COF USD SNRFOR MR",	"PMI USD SNRFOR MR",	"DHI USD SNRFOR MR",	"TOL USD SNRFOR MR",	"PHM USD SNRFOR MR",	
      "CCR-HomeLoans USD SNRFOR MR",	"MGIC USD SNRFOR MR",	"IKB EUR SNRFOR MM",	"IKB GBP SNRFOR MM",	"CCR EUR SNRFOR MM",	"CCR-HomeLoans EUR SNRFOR MR",	
      "WM USD SNRFOR MR",	"WM-Bank USD SNRFOR MR",	"C USD SNRFOR MR",	"GLTNR USD SNRFOR MR",	"ARGENT USD SNRFOR CR",	"FHLMC USD SNRFOR MR",	
      "FNMA USD SNRFOR MR",	"RYL USD SNRFOR MR",	"SFI USD SNRFOR MR",	"GCI USD SNRFOR MR",	"SPGIM EUR SNRFOR MM",	"LUV USD SNRFOR MR",	
      "SLMA USD SNRFOR MR",	"AXL-Inc USD SNRFOR MR",	"CTX USD SNRFOR MR",	"TXU-USHldg USD SNRFOR MR",	"INTEL USD SNRFOR MR",	"LEH USD SNRFOR MR",	
      "UIS USD SNRFOR MR",	"MRO USD SNRFOR MR",	"WM-Bank GBP SUBLT2 MR",	"RCL EUR SNRFOR MM",	"GLTNR EUR SNRFOR MM",	"GLTNR EUR SUBLT2 MM",	
      "WB USD SNRFOR MR",	"CHTR-Holdings USD SNRFOR XR",	"CCR USD SNRFOR MR",	"WFC USD SNRFOR MR",	"MAT USD SNRFOR MR",	"MOS USD SNRFOR MR",	
      "KINGFI EUR SNRFOR MM",	"LEH EUR SNRFOR MM",	"TRWAuto USD SNRFOR MR",	"GS USD SNRFOR MR",	"MER USD SNRFOR MR",	"BPLN EUR SNRFOR MM"
    };
    #endregion survival curve names pool
    #region recoveries
    double[] recoveries = new double[]{
      0.0557,	0.35,	0.4,	0.403333333333333,	0.3,	0.1,	0.25,	0.11,	0.32,	0.0511,	0.35,	0.3,	0.2593571,	0.146666666666667,	0.4,	0.1,	
      0.4,	0.2,	0.368533333333332,	0.3375,	0.309583333333333,	0.37625,	0.4,	0.4,	0.400000000662274,	0.4,	0.316249999999999,	0.4,	
      0.4,	0.4,	0.425,	0.331,	0.4,	0.4,	0.409,	0.425,	0.57,	0.31,	0.4,	0.03,	0.189999999602636,	0.96,	0.96,	0.4,	0.234,	0.2675,	
      0.2614,	0.341862251490116,	0.4,	0.128571428571429,	0.41834411111111,	0.275,	0.3,	0.0895,	0.15,	0.4,	0.01,	0.3675,	0.03,	0.03,	
      0.4,	0.03429,	0.4,	0.4,	0.4,	0.4,	0.4,	0.0895,	0.24,	0.400000000851495,	0.400000000745058,	0.4
    };
    #endregion recoveries
    string[] survivalCurveTenors = new string[] {"6 Month",	"1 Year",	"2 Year",	"3 Year",	"4 Year",	"5 Year",	"7 Year",	"10 Year",	"15 Year"};    
    Dt[] survivalCurveDates = new Dt[] {new Dt(20,12,2009),  new Dt(20, 6, 2010), new Dt(20,6,2011), new Dt(20,6,2012), new Dt(20,6,2013),
                                        new Dt(20, 6, 2014), new Dt(20, 6, 2016), new Dt(20,6,2019), new Dt(20,6,2024)};
    #region cds quotes
    double[,] cdsQuotes = new double[,] { 
      {6953.55318922029,	7770.60843319291,	7408.13099565798,	6694.34318478895,	6234.6202341431,	5949.58953782413,	5410.90741141494,	4892.38719050177,	4707.91063147341},
      {569.99514945,	799.224685516655,	778.581606878642,	722.887179671101,	700.508723125559,	689.198002286562,	612.254700023349,	586.090819820545,	608.024985533333},
      {295.6542969,	359.124612066667,	300.407949225,	300.0235132,	297.89246215,	300.6029555,	291.3079635,	285.94557555,	271.744461733333},
      {670.24166402,	673.827459916667,	625.79180086,	586.72787844,	558.101833014286,	519.343613877778,	462.666340549999,	401.98894535,	400.167934624999},
      {2916.60786132813,	2839.41435833332,	2948.68356585938,	2859.15905794999,	2758.539560225,	2683.80187499999,	2601.4816125,	2519.9787375,	0},
      {9078.59273110347,	9297.39516412019,	8078.98888203142,	7974.09409465886,	7832.07856077663,	7713.99812166109,	7588.94714142043,	7421.07878913562,	0},
      {7017.11275621259,	7731.37922891869,	6348.03592822089,	5815.12167606623,	5330.49305402247,	4993.31363471257,	4636.73073932616,	4362.48397996717,	0},
      {12753.0517650272,	11636.5939535518,	8556.6513442623,	7713.40637470726,	6914.64839708561,	6651.22765339578,	5855.41742941712,	5329.88787626854,	0},
      {683.632838982411,	752.955591883239,	966.204053328571,	1017.83296752049,	1087.04085390228,	1132.204916896,	1047.57649364423,	974.969926651646,	927.011650165512},
      {51985.3116689062,	49403.5911904486,	45067.3820397715,	41681.5274200356,	39000.3906873114,	36834.0328812668,	35537.7952422589,	34854.9499276906,	0},
      {1486.8660816013,	1464.6117292092,	1414.9257054953,	1395.92711634801,	1308.19979672031,	1238.25259259109,	1135.09509291453,	1049.82890830412,	1019.14362291667},
      {1370.6670375,	1751.81735,	1775.68487142857,	1774.235734375,	1742.881809375,	1715.91924166667,	1624.95101875,	1535.10414583333,	0},
      {1667.76666666667,	1618.09287535714,	1548.07322867778,	1427.965129875,	1294.23934635713,	1237.13965861,	1106.60022637777,	998.7201428,	920.997693859517},
      {3565.41664698178,	3200.95653063388,	3007.24297131079,	2838.66542067394,	2668.29484045424,	2588.52784415738,	2309.57869071147,	2055.57508397485,	2110.8675839235},
      {120.025,	244.929974999999,	205.7217,	192.7577,	173.54044,	172.083333333333,	164.73725,	148.125,	146.8207},
      {7444.6475,	7720.389,	7317.19975,	6490.16706666667,	6044.1803,	5648.7868,	5031.48868333333,	4423.18706428571,	0},
      {801.538599,	778.409193669999,	741.7578660375,	709.049766408333,	665.18203935,	635.683018908333,	584.05305545,	538.20245183,	543.162659757142},
      {3803.92765,	3282.31061871853,	2979.44914764285,	2733.90156818571,	2550.04820382857,	2424.19500284286,	1899.91245971209,	1688.46745791881,	0},
      {2265.22133966523,	2239.60503565895,	1574.91460585398,	1511.86178374737,	1334.38620383452,	1277.96686235808,	1107.02199354715,	1038.6853946614,	1037.59229811222},
      {787.709928651407,	801.973725,	1104.054475,	1260.7407611225,	1310.78310614365,	1336.68275,	1235.51811666667,	1153.23474166007,	1132.57900705374},
      {883.561666666666,	1267.72383217589,	1238.72829020937,	1211.92304199219,	1181.38171322578,	1161.7774543,	1092.80847442857,	1027.34330313571,	981.992222433333},
      {2275.22,	2244.33310461429,	1565.34799166666,	1495.01912975,	1319.90116164286,	1264.851669875,	1096.15221071429,	1027.46133011,	1054.457015275},
      {120.025,	244.929974999999,	194.842606798965,	194.006114972678,	175.485628410699,	174.107103825137,	164.73725,	149.684210526315,	146.8207},
      {120.025,	244.929974999999,	205.7217,	192.7577,	173.54044,	172.083333333333,	164.73725,	148.125,	146.8207},
      {645.995789473683,	690.27119576711,	563.191575292329,	512.257228816811,	476.076146319626,	455.336371968172,	431.956572091366,	410.286952522719,	401.058136118447},
      {571.326166666667,	544.292857142857,	479.264375,	426.061875,	408.455711111111,	390.374999998479,	339.213749999999,	304.281428571429,	299.924666666667},
      {2424.69779823,	2467.99864028575,	2064.00643551398,	1817.10834631121,	1645.71986673153,	1525.63655152165,	1319.00764285714,	1151.9943827875,	1098.62148743333},
      {465.649468640574,	470.808963433333,	465.570848466667,	439.194417266667,	408.447387,	391.30326224,	351.433184625,	311.59586186,	307.002338816029},
      {209.524088933332,	194.689745814286,	193.851919322221,	185.6274188,	179.162410255556,	171.018004218181,	148.8522005875,	130.3246418,	136.790277966666},
      {270.641624999999,	279.87971245,	286.81386035,	281.7570283625,	267.7238348625,	255.625,	224.857142857143,	194.432225788889,	200.89932445},
      {460.7,	467.027103825137,	398.333333333333,	347.666666666667,	324.625,	319.381830601093,	293.333333333333,	252.614298724954,	272.088825136612},
      {2862.28291247012,	2631.82768148696,	2001.2563748393,	1833.49601335496,	1666.15721034336,	1554.40135338592,	1353.13135,	1154.55895,	1049.10060108051},
      {1525.8,	1205.38999999999,	1009.59321104167,	997.3134123,	949.45890495,	886.044226283333,	838.6341242625,	785.4970814,	0},
      {1525.8,	1205.38999999999,	1009.59321104167,	997.3134123,	949.45890495,	886.044226283333,	838.6341242625,	785.4970814,	0},
      {320.666666666667,	318.383474833333,	241.71019395,	216.726209466667,	216.625826025,	219.973437899999,	204.251144566667,	199.168836633333,	220.897802338388},
      {460.7,	467.027103825137,	398.333333333333,	347.666666666667,	324.625,	319.381830601093,	293.333333333333,	252.614298724954,	272.088825136612},
      {22449.7837377513,	20673.2231418736,	17944.0656971674,	15907.8413292614,	14336.7617219099,	13099.4758685066,	11276.7883600797,	9498.16835800695,	7763.69192985595},
      {37810.6889003956,	35238.3545185302,	31172.024873301,	28035.2368366821,	25552.1601936667,	23556.7573314697,	20551.0614607705,	17539.2631740606,	14524.2586732595},
      {894.1590683,	834.354833333333,	688.6137,	613.142524999999,	582.644866666666,	562.5,	515.833333333333,	476.875,	485},
      {40650.3038452253,	35164.6589528532,	27881.3350562238,	23218.4883590831,	19990.0416945119,	17643.7607099899,	14464.1914488958,	11640.9366914752,	9119.58535896218},
      {4569.2277502132,	5149.60494742143,	4904.69664597929,	4550.91981327423,	4126.25090913504,	3926.51418807378,	3718.6592284017,	3564.53102175384,	3628.27746844421},
      {500,	450,	400,	350,	300,	250,	200,	200,	0},
      {500,	450,	400,	350,	300,	250,	200,	200,	0},
      {336.23366666,	359.413666666667,	342.127476185713,	326.587523814286,	316.669476185714,	300,	247.254,	213.325833333333,	202.336666666666},
      {4106.7114375,	3527.23589999999,	4065.73386666666,	3815.866725,	3729.36955,	3775.37175892532,	3389.6949,	3146.962975,	2979.1502375},
      {1254.45976666666,	1874.42888625889,	1722.32478807965,	1617.85638453014,	1448.28035310554,	1384.29958405023,	1204.21612286667,	1082.17759225,	1040.45251219999},
      {1984.69059122793,	2423.24152146482,	2597.72526035803,	2480.2995973796,	2364.95680477073,	2300.65667765241,	2165.6982533064,	1985.75946722828,	0},
      {283.116666666667,	278.335940733333,	280.011660016666,	283.0601313375,	284.888433233333,	282.442382466667,	253.55064085,	226.73191018,	226.511093586833},
      {3067.13214116667,	3042.37398333333,	2607.41747857143,	2411.22582916667,	2196.48825416667,	2041.57383214286,	1822.122725,	1657.61809285714,	1585.3425625},
      {11262.952006025,	8325.73075037703,	7305.72452903129,	5561.10811254324,	5032.6324712977,	4671.61503141065,	4262.76936770048,	3750.99401568598,	0},
      {459.817121533333,	456.381585255555,	451.76332335,	433.15646395,	415.9719057,	397.58519796,	351.901681877778,	302.141146811111,	319.1716624},
      {1986.528,	1927.00175,	2155,	1970.59709999999,	2016,	1847.3584,	1595.90109094999,	1532.0448819,	0},
      {413.031693116667,	477.61754,	609.10445,	721.38526934,	782.544938642857,	789.078647942857,	723.3624873875,	683.3862325125,	669.728465025},
      {39241.4949212738,	34235.6030369749,	27459.4577092647,	23038.1878273773,	19938.0556066535,	17664.7000895972,	14556.2619754014,	11769.0895585057,	9258.13047809346},
      {7184.37032556203,	7185.58929899805,	6426.0669415519,	6147.50773700186,	5760.79314736928,	5553.87587885186,	5464.25686455149,	5428.84692675083,	0},
      {239.152499999999,	250.561058044444,	260.717145777778,	267.48667098,	274.075883144444,	277.482469455556,	276.225334139999,	273.95031738,	285.75596905},
      {49649.6453864281,	45240.6444594698,	38635.5085039439,	33844.4215246544,	30224.8741275828,	27420.2904008542,	23360.5777451035,	19479.2487264123,	15768.305768204},
      {1372.35,	1806.6485112172,	1651.9632375,	1737.17099,	1632.48908,	1509.75060833333,	1362.666575,	1153.36904,	1102.08969885009},
      {40650.3038452253,	35164.6589528532,	27881.3350562238,	23218.4883590831,	19990.0416945119,	17643.7607099899,	14464.1914488958,	11640.9366914752,	9119.58535896218},
      {40650.3038452253,	35164.6589528532,	27881.3350562238,	23218.4883590831,	19990.0416945119,	17643.7607099899,	14464.1914488958,	11640.9366914752,	9119.58535896218},
      {298.795,	325.6075,	273.089192188889,	243.092680208333,	238.191143708333,	233.132928357143,	212.86664746,	192.76294169,	214.1898432},
      {41554.4049845686,	41591.5664546786,	41464.2456297915,	41410.79899702,	41361.8566668196,	41317.0082514531,	41238.6073598484,	41145.1168085625,	0},
      {320.666666666667,	318.383474833333,	241.71019395,	216.726209466667,	216.625826025,	219.973437899999,	204.251144566667,	199.168836633333,	220.897802338388},
      {305.8148658,	317.6568447,	272.885332018182,	242.721819,	240.517370916667,	233.947279633332,	210.23507124,	185.473977888889,	184.818633499999},
      {66.1064391,	131.6675,	144.7125,	150.370416666667,	154.627419042857,	164,	151.4367,	145.597183333333,	182.464010349999},
      {127.998214533333,	138.700962052632,	151.098566213115,	173.10799767193,	172.939470152932,	192.128157125729,	196.56077413479,	202.046703096539,	212.41175},
      {401.621267770076,	404.017543632344,	394.463027316069,	375.712273789293,	349.822741519999,	328.296877637158,	289.877403580816,	252.601291196696,	225.757170189258},
      {39241.4949212738,	34235.6030369749,	27459.4577092647,	23038.1878273773,	19938.0556066535,	17664.7000895972,	14556.2619754014,	11769.0895585057,	9258.13047809346},
      {3803.92765,	3406.26430979363,	3044.54148333333,	2837.02743647502,	2646.23588758158,	2419.84955,	1972.5867068049,	1757.46457379548,	0},
      {335.76475,	335.3823125,	304.51023,	287.224700372727,	273.17263314,	269.7432531,	242.316212909091,	214.95326221,	225},
      {639.266666666667,	682.927673816667,	557.1077,	507.556934922222,	471.054714289999,	450.277777777778,	426.8433928625,	405.319682544445,	396.7736494},
      {61.68295,	79.1316625,	71.5591566666667,	66.919816,	63.6402,	60.33224,	56.1479349999999,	50.3631285714286,	58.08327685}
    };
    #endregion cds quotes
    #endregion CDS curve data
    private DayCount cdsDayCount = DayCount.Actual360;
    Frequency cdsFreq = Frequency.Quarterly;
    BDConvention cdsRoll = BDConvention.Modified;
    InterpMethod cdsInterp = InterpMethod.Weighted;
    ExtrapMethod cdsExtrap = ExtrapMethod.Const;
    NegSPTreatment negAllow = NegSPTreatment.Allow;
    SurvivalCurve[] survivalCurves_ = null;

    #region bond data
    #region bond effective
    private string[] effective = new string[]{
      "3/1/2005",	"4/28/2005",	"5/15/2005",	"12/11/2003",	"3/13/2007",	"4/28/2005",	"4/28/2005",	"3/1/2005",	"9/22/2004",	"9/22/2004",	"9/22/2004",	
      "10/12/2006",	"3/29/2006",	"5/27/2005",	"5/27/2005",	"3/8/2004",	"9/22/2004",	"3/8/2004",	"8/10/2005",	"6/26/2007",	"4/28/2005",	"6/26/2007",	
      "6/9/2006",	"5/13/2004",	"5/13/2004",	"3/8/2004",	"3/8/2004",	"7/5/2006",	"7/5/2006",	"4/10/2006",	"5/1/2007",	"5/1/2007",	"3/8/2004",	"7/5/2006",	
      "3/26/2007",	"3/26/2007",	"12/11/2003",	"5/27/2005",	"6/26/2007",	"9/22/2004",	"9/22/2004",	"12/11/2003",	"5/27/2005",	"5/13/2004",	"1/28/2003",	
      "2/8/2007",	"5/13/2004",	"7/5/2006",	"5/13/2004",	"5/1/2007",	"3/29/2006",	"2/8/2007",	"5/13/2004",	"5/13/2004",	"5/1/2007",	"5/1/2007",	"5/1/2007",	
      "12/7/1999",	"1/25/2007",	"7/5/2006",	"7/5/2006",	"9/26/2006",	"9/15/2005",	"1/25/2007",	"5/15/2005",	"8/17/2006",	"2/1/2008",	"12/7/1999",	"7/5/2006",	
      "1/30/2004",	"3/25/2004",	"5/13/2004",	"7/5/2006",	"6/23/2005",	"10/23/2006",	"5/1/2007",	"6/23/2005",	"2/1/2008",	"11/30/2006",	"7/5/2006",	"11/30/2006",	
      "11/30/2006",	"8/17/2006",	"12/7/1999",	"1/31/2007",	"6/26/2007",	"6/26/2007",	"9/28/2005",	"4/10/2006",	"1/25/2007",	"5/13/2004",	"1/24/2007",	
      "11/23/2005",	"1/24/2007",	"7/5/2006",	"5/27/2005",	"7/5/2006",	"1/24/2007",	"1/24/2007",	"2/6/2006","5/16/2007",	"5/13/2004",	"5/16/2007",	"5/13/2004",	
      "5/16/2007",	"4/28/2008",	"4/28/2008",	"1/28/2003",	"1/18/2007",	"4/28/2008",	"2/6/2006",	"8/24/2006",	"3/22/2005",	"4/28/2008",	"8/24/2006",	"2/6/2006",	
      "11/23/2005",	"11/23/2005",	"9/22/2004",	"1/18/2007",	"1/18/2007",	"4/26/2006",	"10/23/2006",	"8/17/2006",	"1/25/2007",	"2/1/2008",	"1/25/2007",	"8/10/2005",	
      "8/10/2005",	"10/12/2006",	"11/30/2006",	"6/23/2005",	"8/17/2006",	"8/17/2006",	"4/28/2008",	"4/22/2004",	"3/25/2004",	"12/7/1999",	"1/30/2004",	"1/25/2007",	
      "2/5/2003",	"8/10/2005",	"4/22/2004",	"4/22/2004",	"2/6/2006",	"3/17/2006",	"5/15/2005",	"2/27/2007",	"2/27/2007",	"2/27/2007",	"10/6/2003",	"12/15/2004",	
      "10/12/2006",	"5/9/2005",	"3/22/2005",	"10/12/2006",	"3/29/2006",	"9/22/2004",	"5/16/2007",	"5/16/2007",	"5/13/2004",	"5/13/2004",	"12/15/2004",	"9/15/2005",	
      "9/26/2006",	"5/16/2007",	"5/13/2004",	"11/23/2005",	"11/23/2005",	"11/23/2005",	"8/10/2005",	"11/23/2005",	"10/12/2006",	"8/18/2005",	"2/8/2007",	"11/30/2006",	
      "11/7/2003",	"7/22/2004",	"5/1/2007",	"4/28/2008",	"4/28/2008",	"9/14/2005",	"12/5/2006",	"1/25/2007",	"1/18/2007",	"5/13/2004",	"5/13/2004",	"5/13/2004",	
      "5/16/2007",	"5/16/2007",	"5/16/2007",	"1/18/2007",	"10/23/2006",	"4/10/2006",	"9/22/2004",	"9/22/2004",	"9/22/2004",	"9/22/2004",	"9/22/2004",	"6/7/2007",
      "4/10/2006",	"1/24/2007",	"9/22/2004",	"8/10/2005",	"1/24/2007",	"5/27/2005",	"9/28/2005",	"9/22/2004",	"9/28/2005",	"5/16/2007",	"5/13/2004",	"10/12/2006",	
      "5/16/2007",	"5/13/2004",	"8/18/2005",	"5/19/2008",	"5/27/2005",	"4/10/2006",	"5/13/2004",	"5/13/2004",	"11/7/2003",	"5/13/2004",	"5/13/2004",	"2/6/2006",	
      "3/9/2005",	"2/6/2006",	"4/1/2008",	"2/6/2006",	"10/12/2006",	"5/16/2007",	"5/16/2007",	"5/13/2004",	"3/9/2005",	"3/9/2005",	"9/26/2005",	"9/26/2005",	"12/20/2004",	
      "11/3/2003",	"8/24/2006",	"9/26/2005",	"8/24/2006",	"3/22/2005",	"11/3/2003",	"12/20/2004",	"9/26/2005",	"9/26/2005",	"3/22/2005",	"5/19/2008",	"5/19/2008",	
      "2/6/2006",	"2/6/2006",	"1/18/2007",	"1/18/2007",	"4/1/2008",	"4/1/2008",	"6/15/2006",	"6/15/2006",	"10/23/2006",	"4/28/2008",	"4/28/2008",	"4/28/2008",	"2/6/2006",	
      "1/18/2007",	"6/7/2007",	"1/18/2007",	"5/27/2005",	"5/27/2005",	"9/28/2005",	"9/28/2005",	"7/5/2006",	"7/5/2006",	"2/6/2006",	"1/24/2007",	"1/24/2007",	"6/15/2006",	
      "8/10/2005",	"8/10/2005",	"8/10/2005"
    };
    private Dt[] bondEffectiveDates = null;
    #endregion bond effective
    #region bond maturity
    private string[] maturities = new string[]{
      "9/1/2014",	"5/31/2015",	"11/15/2014",	"12/15/2013",	"3/13/2012",	"5/31/2015",	"5/31/2015",	"9/1/2014",	"10/1/2014",	"10/1/2014",	"10/1/2014",	"10/15/2015",	
      "4/1/2013",	"6/1/2015",	"6/1/2015",	"3/15/2014",	"10/1/2014",	"3/15/2014",	"8/15/2015",	"6/26/2017",	"5/31/2015",	"6/26/2017",	"6/1/2016",	"5/15/2014",	
      "5/15/2014",	"3/15/2014",	"3/15/2014",	"7/15/2011",	"7/15/2011",	"4/15/2012",	"5/1/2015",	"5/1/2015",	"3/15/2014",	"7/15/2011",	"3/15/2014",	"3/15/2014",	
      "12/15/2013",	"6/1/2015",	"6/26/2017",	"10/1/2014",	"10/1/2014",	"12/15/2013",	"6/1/2015",	"5/13/2014",	"2/1/2013",	"2/1/2017",	"5/13/2014",	"7/15/2011",	
      "5/13/2014",	"5/1/2015",	"4/1/2013",	"2/1/2017",	"5/15/2014",	"5/15/2014",	"5/1/2015",	"5/1/2015",	"5/1/2015",	"12/7/2009",	"2/1/2012",	"7/15/2011",	"7/15/2011",	
      "9/26/2013",	"12/7/2012",	"2/1/2012",	"11/15/2014",	"8/15/2011",	"2/1/2018",	"12/7/2009",	"7/15/2011",	"1/30/2009",	"3/25/2009",	"5/15/2014",	"7/15/2011",	
      "6/23/2010",	"10/22/2010",	"5/1/2015",	"6/23/2010",	"2/1/2018",	"11/30/2011",	"7/15/2011",	"11/30/2011",	"11/30/2011",	"8/15/2011",	"12/7/2009",	"1/31/2014",	
      "6/26/2017",	"6/26/2017",	"10/1/2017",	"4/15/2012",	"2/1/2012",	"5/15/2014",	"1/24/2012",	"11/23/2010",	"1/24/2012",	"7/15/2011",	"6/1/2015",	"7/15/2011",	
      "1/24/2012",	"1/24/2012",	"2/4/2011",  "9/15/2013",	"5/15/2014",	"9/15/2013",	"5/15/2014",	"9/15/2013",	"4/30/2049",	"4/30/2049",	"2/1/2013",	"1/18/2012",	
      "4/30/2049",	"2/4/2011",	"8/24/2011",	"3/22/2012",	"4/30/2049",	"8/24/2011",	"2/4/2011",	"11/23/2010",	"11/23/2010",	"10/1/2014",	"1/18/2012",	"1/18/2012",	
      "4/15/2016",	"10/22/2010",	"8/15/2011",	"2/1/2012",	"2/1/2018",	"2/1/2012",	"8/15/2015",	"8/15/2015",	"10/15/2015",	"11/30/2011",	"6/23/2010",	"8/15/2011",	
      "8/15/2011",	"4/30/2049",	"4/30/2014",	"3/25/2009",	"12/7/2009",	"1/30/2009",	"2/1/2012",	"3/1/2013",	"8/15/2015",	"4/30/2014",	"4/30/2014",	"2/4/2011",	
      "3/15/2011",	"11/15/2014",	"3/1/2017",	"3/1/2017",	"3/1/2017",	"10/1/2013",	"1/15/2016",	"10/15/2015",	"5/15/2012",	"3/22/2012",	"10/15/2015",	"4/1/2013",	
      "10/1/2014",	"9/15/2013",	"9/15/2013",	"5/15/2014",	"5/15/2014",	"1/15/2016",	"12/7/2012",	"9/26/2013",	"9/15/2013",	"5/15/2014",	"11/23/2010",	"11/23/2010",	
      "11/23/2010",	"8/15/2015",	"11/23/2010",	"10/15/2015",	"8/15/2012",	"2/1/2017",	"11/30/2011",	"11/1/2013",	"7/22/2014",	"5/1/2015",	"4/30/2049",	"4/30/2049",	
      "10/15/2012",	"6/10/2019",	"1/27/2014",	"1/18/2012",	"5/15/2014",	"5/15/2014",	"5/15/2014",	"9/15/2013",	"9/15/2013",	"9/15/2013",	"1/18/2012",	"10/15/2011",	
      "4/15/2012",	"10/1/2014",	"10/1/2014",	"10/1/2014",	"10/1/2014",	"10/1/2014",	"5/7/2012", "4/15/2012",	"1/24/2012",	"10/1/2014",	"8/15/2015",	"1/24/2012",	
      "6/1/2015",	"10/1/2017",	"10/1/2014",	"10/1/2017",	"9/15/2013",	"5/15/2014",	"10/15/2015",	"9/15/2013",	"5/15/2014",	"8/15/2012",	"12/29/2049",	"6/1/2015",	
      "4/15/2012",	"5/15/2014",	"5/15/2014",	"11/1/2013",	"5/15/2014",	"5/15/2014",	"2/4/2011",	"3/9/2015",	"2/4/2011",	"4/1/2018",	"2/4/2011",	"10/15/2015",	"9/15/2013",	
      "9/15/2013",	"5/15/2014",	"3/9/2015",	"3/9/2015",	"9/17/2012",	"9/17/2012",	"1/15/2010",	"1/15/2009",	"8/24/2011",	"9/15/2017",	"8/24/2011",	"3/22/2012",	
      "1/15/2009",	"1/15/2010",	"9/15/2017",	"9/17/2012",	"3/22/2012",	"12/29/2049",	"12/29/2049",	"2/4/2011",	"2/4/2011",	"1/18/2012",	"1/18/2012",	"4/1/2018",	"4/1/2018",	
      "6/15/2016",	"6/15/2016",	"10/15/2011",	"4/30/2049",	"4/30/2049",	"4/30/2049",	"2/4/2011",	"1/18/2012",	"5/7/2012",	"1/18/2012",	"6/1/2015",	"6/1/2015",	"10/1/2017",	
      "10/1/2017",	"7/15/2011",	"7/15/2011",	"2/4/2011",	"1/24/2012",	"1/24/2012",	"6/15/2016",	"8/15/2015",	"8/15/2015",	"8/15/2015"
    };
    private Dt[] bondMaturities = null;
    #endregion bond maturity
    #region bond first coupon
    private string[] firstCoupon = new string[]{
      "9/1/2005",	"12/1/2005",	"11/15/2005",	"6/15/2004",	"6/13/2007",	"12/1/2005",	"12/1/2005",	"9/1/2005",	"4/1/2005",	"4/1/2005",	"4/1/2005",	
      "4/15/2007",	"10/1/2006",	"12/1/2005",	"12/1/2005",	"9/15/2004",	"4/1/2005",	"9/15/2004",	"2/15/2006",	"6/26/2008",	"12/1/2005",	
      "6/26/2008",	"12/1/2006",	"11/15/2004",	"11/15/2004",	"9/15/2004",	"9/15/2004",	"10/15/2006",	"10/15/2006",	"7/15/2006",	"11/1/2007",	
      "11/1/2007",	"9/15/2004",	"10/15/2006",	"9/15/2007",	"9/15/2007",	"6/15/2004",	"12/1/2005",	"6/26/2008",	"4/1/2005",	"4/1/2005",	"6/15/2004",	
      "12/1/2005",	"5/13/2005",	"8/1/2003",	"8/1/2007",	"5/13/2005",	"10/15/2006",	"5/13/2005",	"11/1/2007",	"10/1/2006",	"8/1/2007",	"11/15/2004",	
      "11/15/2004",	"11/1/2007",	"11/1/2007",	"11/1/2007",	"6/7/2000",	"5/1/2007",	"10/15/2006",	"10/15/2006",	"12/26/2006",	"12/7/2005",	"5/1/2007",	
      "11/15/2005",	"11/15/2006",	"8/1/2008",	"6/7/2000",	"10/15/2006",	"4/30/2004",	"9/25/2004",	"11/15/2004",	"10/15/2006",	"12/23/2005",	"1/22/2007",	
      "11/1/2007",	"12/23/2005",	"8/1/2008",	"2/28/2007",	"10/15/2006",	"2/28/2007",	"2/28/2007",	"11/15/2006",	"6/7/2000",	"4/30/2007",	"6/26/2008",	
      "6/26/2008",	"4/1/2006",	"7/15/2006",	"5/1/2007",	"11/15/2004",	"4/24/2007",	"2/23/2006",	"4/24/2007",	"10/15/2006",	"12/1/2005",	"10/15/2006",	
      "4/24/2007",	"4/24/2007",	"5/4/2006", "9/15/2007",	"11/15/2004",	"9/15/2007",	"11/15/2004",	"9/15/2007",	"10/30/2008",	"10/30/2008",	"8/1/2003",	
      "4/18/2007",	"10/30/2008",	"5/4/2006",	"2/24/2007",	"6/22/2005",	"10/30/2008",	"2/24/2007",	"5/4/2006",	"2/23/2006",	"2/23/2006",	"4/1/2005",	
      "4/18/2007",	"4/18/2007",	"12/15/2006",	"1/22/2007",	"11/15/2006",	"5/1/2007",	"8/1/2008",	"5/1/2007",	"2/15/2006",	"2/15/2006",	"4/15/2007",	
      "2/28/2007",	"12/23/2005",	"11/15/2006",	"11/15/2006",	"10/30/2008",	"10/31/2004",	"9/25/2004",	"6/7/2000",	"4/30/2004",	"5/1/2007",	"9/1/2003",	
      "2/15/2006",	"10/31/2004",	"10/31/2004",	"5/4/2006",	"6/15/2006",	"11/15/2005",	"9/1/2007",	"9/1/2007",	"9/1/2007",	"4/1/2004",	"7/15/2005",	
      "4/15/2007",	"11/15/2005",	"6/22/2005",	"4/15/2007",	"10/1/2006",	"4/1/2005",	"9/15/2007",	"9/15/2007",	"11/15/2004",	"11/15/2004",	"7/15/2005",
      "12/7/2005",	"12/26/2006",	"9/15/2007",	"11/15/2004",	"2/23/2006",	"2/23/2006",	"2/23/2006",	"2/15/2006",	"2/23/2006",	"4/15/2007",	"2/15/2006",	
      "8/1/2007",	"2/28/2007",	"5/1/2004",	"10/22/2004",	"11/1/2007",	"10/30/2008",	"10/30/2008",	"4/15/2006",	"6/10/2007",	"1/27/2008",	"4/18/2007",	
      "11/15/2004",	"11/15/2004",	"11/15/2004",	"9/15/2007",	"9/15/2007",	"9/15/2007",	"4/18/2007",	"4/15/2007",	"7/15/2006",	"4/1/2005",	"4/1/2005",	
      "4/1/2005",	"4/1/2005",	"4/1/2005",	"8/7/2007", "7/15/2006",	"4/24/2007",	"4/1/2005",	"2/15/2006",	"4/24/2007",	"12/1/2005",	"4/1/2006",	"4/1/2005",	
      "4/1/2006",	"9/15/2007",	"11/15/2004",	"4/15/2007",	"9/15/2007",	"11/15/2004",	"2/15/2006",	"9/26/2008",	"12/1/2005",	"7/15/2006",	"11/15/2004",	
      "11/15/2004",	"5/1/2004",	"11/15/2004",	"11/15/2004",	"5/4/2006",	"3/9/2006",	"5/4/2006",	"10/1/2008",	"5/4/2006",	"4/15/2007",	"9/15/2007",	"9/15/2007",	
      "11/15/2004",	"3/9/2006",	"3/9/2006",	"12/19/2005",	"12/19/2005",	"7/15/2005",	"7/15/2004",	"2/24/2007",	"3/15/2006",	"2/24/2007",	"6/22/2005",	
      "7/15/2004",	"7/15/2005",	"3/15/2006",	"12/19/2005",	"6/22/2005",	"9/26/2008",	"9/26/2008",	"5/4/2006",	"5/4/2006",	"4/18/2007",	"4/18/2007",	
      "10/1/2008",	"10/1/2008",	"12/15/2006",	"12/15/2006",	"4/15/2007",	"10/30/2008",	"10/30/2008",	"10/30/2008",	"5/4/2006",	"4/18/2007",	"8/7/2007",	
      "4/18/2007",	"12/1/2005",	"12/1/2005",	"4/1/2006",	"4/1/2006",	"10/15/2006",	"10/15/2006",	"5/4/2006",	"4/24/2007",	"4/24/2007",	"12/15/2006",	
      "2/15/2006",	"2/15/2006",	"2/15/2006"
    };
    private Dt[] bondFirstCoupon = null;
    #endregion bond first coupon
    #region bond currency
    private Currency[] bondCcy = new Currency[]{
      USD_,	USD_,	USD_,	USD_,	EUR_,	USD_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	EUR_,	USD_,	EUR_,	EUR_,	USD_,	EUR_,	USD_,	EUR_,	EUR_,	
      USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	EUR_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	EUR_,	USD_,	EUR_,	EUR_,	USD_,	EUR_,	USD_,	
      EUR_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	GBP_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	USD_,	USD_,	USD_,	
      USD_,	USD_,	USD_,	EUR_,	USD_,	EUR_,	EUR_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	EUR_,	GBP_,	EUR_,	GBP_,	USD_,	USD_,	USD_,	GBP_,	GBP_,	USD_,
      EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	
      USD_,	USD_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	
      USD_,	USD_,	EUR_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	GBP_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	EUR_,	
      EUR_,	USD_,	EUR_,	USD_,	USD_,	USD_,	USD_,	GBP_,	EUR_,	USD_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,
      USD_,	GBP_,	EUR_,	EUR_,	GBP_,	USD_,	USD_,	EUR_,	USD_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	EUR_,	EUR_,	USD_,	EUR_,	EUR_,	USD_,	EUR_,	
      USD_,	USD_,	USD_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	EUR_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	
      USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	USD_,	GBP_,	GBP_,	USD_,	
      EUR_,	EUR_,	EUR_
    };
    #endregion bond currency
    #region bond day counts
    private string[] dayCounts = new string[]{
      "Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	
      "Thirty360",	"Thirty360",	"ActualActual",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"ActualActual",	
      "Thirty360",	"ActualActual",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Actual360",	"Actual360",	
      "Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"ActualActual",	"Thirty360",	
      "Thirty360",	"Thirty360",	"Thirty360",	"ActualActual",	"Thirty360",	"Thirty360",	"ActualActual",	"Actual360",	"ActualActual",	"Thirty360",	
      "ActualActual",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Actual360",	
      "Actual360",	"Actual360",	"ActualActual",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Actual360",	"Actual360",	
      "Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Actual360",	
      "Actual360",	"Actual360",	"Actual360",	"Thirty360",	"Actual360",	"ActualActual",	"ActualActual",	"Thirty360",	"Actual360",	"Actual360",	
      "Thirty360",	"Actual365Fixed",	"Actual360",	"Actual365Fixed",	"Actual360",	"Thirty360",	"Actual360",	"Actual365Fixed",	"Actual365Fixed",	
      "Actual360",  "Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	
      "Thirty360",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Actual360",	"Actual360",	"Actual360",	"Thirty360",	
      "Actual360",	"Actual360",	"Thirty360",	"Actual360",	"Actual360",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	
      "Thirty360",	"Actual360",	"Thirty360",	"Actual360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	
      "Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	
      "Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	"ActualActual",	"Thirty360",	"Actual360",	
      "Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"ActualActual",	"Actual360",	"Actual360",	"Thirty360",	"Actual360",	"Actual360",	
      "Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	
      "Thirty360",	"Thirty360",	"Thirty360",	"ActualActual",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	
      "Actual360",	"Actual360",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	
      "Actual360",  "Actual360",	"Actual365Fixed",	"Thirty360",	"Thirty360",	"Actual365Fixed",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	
      "Actual360",	"Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	
      "Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"ActualActual",	"Actual360",	"Thirty360",	"Actual360",	"Thirty360",	
      "Actual360",	"Actual360",	"Thirty360",	"ActualActual",	"ActualActual",	"Actual360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	
      "Thirty360",	"Thirty360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Actual360",	"Actual360",	"Thirty360",	"Thirty360",	
      "Actual360",	"Actual360",	"Actual360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	
      "Thirty360",	"Thirty360",	"Actual360",	"Actual360",	"Actual360",	"Actual360",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",	
      "Actual360",	"Actual360",	"Actual360",	"Actual365Fixed",	"Actual365Fixed",	"Thirty360",	"Thirty360",	"Thirty360",	"Thirty360",
    };
    private DayCount[] bondDaycount = null;
    #endregion bond day counts
    #region bond frequency
    private string[] freqs = new string[]{
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Annual",	
      "SemiAnnual",	"Annual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Annual",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Annual",	"SemiAnnual",	"SemiAnnual",	"Annual",	"Quarterly",	"Annual",	"SemiAnnual",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	
      "Quarterly",	"Annual",	"Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	
      "Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"Quarterly",	
      "Quarterly",	"SemiAnnual",	"Quarterly",	"Annual",	"Annual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"SemiAnnual",	"Quarterly",	"Quarterly",	
      "Quarterly",	"Quarterly",	"SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"Quarterly", "Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	
      "Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	
      "SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"SemiAnnual",	"Quarterly",	"Quarterly",	"SemiAnnual",	"Quarterly",	"Quarterly",	
      "Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"Quarterly",	"Quarterly",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	
      "Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	
      "Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Annual",	"Quarterly",	
      "Quarterly",	"SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	
      "Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Annual",	"Annual",	"Quarterly",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"Quarterly",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",  "Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	
      "SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Annual",	"Quarterly",	"SemiAnnual",	
      "Quarterly",	"SemiAnnual",	"Quarterly",	"Quarterly",	"SemiAnnual",	"Annual",	"Annual",	"Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"SemiAnnual",	
      "SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	
      "SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	
      "SemiAnnual",	"Quarterly",	"Quarterly",	"Quarterly",	"Quarterly",	"Quarterly",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",	"SemiAnnual",
    };
    private Frequency[] bondFreq = null;
    #endregion bond frequency
    #region bond calendar
    private string[] calendars = new string[]{
      "NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"TGT",	
      "NYB",	"TGT",	"TGT",	"NYB",	"TGT",	"NYB",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	
      "TGT",	"TGT",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"TGT",	"NYB",	"TGT",	"TGT",	"NYB",	"TGT",	"NYB",	"TGT",	
      "TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	
      "NYB",	"NYB",	"NYB",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"NYB",	"TGT",	"TGT",	"NYB",	"NYB",	"TGT",	
      "TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"NYB",  "TGT",	"TGT",	
      "TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	
      "NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	
      "NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	
      "NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	
      "TGT",	"TGT",	"TGT",	"NYB",	"TGT",	"TGT",	"NYB",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"NYB",	"TGT",	"TGT",	
      "TGT",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"NYB",  "NYB",	"TGT",	"TGT",	"TGT",	
      "TGT",	"NYB",	"NYB",	"TGT",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"NYB",	
      "TGT",	"TGT",	"NYB",	"TGT",	"NYB",	"NYB",	"NYB",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"TGT",	"NYB",	"NYB",	"NYB",	"NYB",	
      "NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	
      "NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	"NYB",	
      "TGT",	"TGT",	"NYB",	"TGT",	"TGT",	"TGT"
    };
    private Calendar[] bondCalendar = null;
    #endregion bond calendar
    #region bond bdconvention
    private string[] bdconvetions = new string[]{
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Modified",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",
      "Following",	"Following",	"Following",	"Following",	"Following",	"Modified",	"Modified",	"Following",	"Following",	"Modified",	"Following",	
      "Following",	"Following",	"Modified",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Modified",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Modified",	"Modified",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",  "Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Modified",	"Modified",	"Modified",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	"Following",	
      "Following",	"Following",	"Following",	"Following",	"Following"
    };
    private BDConvention[] bondBDConvention = null;
    #endregion bond bdconvention
    #region bond coupon
    private double[] bondCoupon = new double[]{
      0.055,	0.056,	0.05125,	0.05375,	10,	0.056,	0.056,	0.055,	0.08625,	0.08625,	0.08625,	0.08625,	0.0875,	0.05625,	0.05625,	0.0575,	
      0.08625,	0.0575,	0.08375,	0.07,	0.056,	0.07,	0.065,	0.095,	0.095,	0.0575,	0.0575,	425,	425,	445,	0.06,	0.06,	0.0575,	425,	0.06375,	
      0.06375,	0.05375,	0.05625,	0.07,	0.08625,	0.08625,	0.05375,	0.05625,	0.05,	0.07375,	0.06125,	0.05,	425,	0.05,	0.06,	0.0875,	0.06125,	
      0.095,	0.095,	0.06,	0.06,	0.06,	0.07625,	19,	425,	425,	25,	0.0475,	19,	0.05125,	21,	0.0725,	0.07625,	425,	30,	0.0325,	0.095,	425,	0.0455,	
      14,	0.06,	0.0455,	0.0725,	34,	425,	34,	34,	21,	0.07625,	26,	0.07,	0.07,	0.0575,	445,	19,	0.095,	7.5,	40,	7.5,	425,	0.05625,	425,	7.5,	7.5,	
      20, 200,	0.095,	200,	0.095,	200,	0.084,	0.084,	0.07375,	47,	0.084,	20,	0.055,	30,	0.084,	0.055,	20,	40,	40,	0.08625,	47,	47,	0.065,	
      14,	21,	19,	0.0725,	19,	0.08375,	0.08375,	0.08625,	34,	0.0455,	21,	21,	0.084,	0.08,	0.0325,	0.07625,	30,	19,	0.0595,	0.08375,	0.08,	0.08,	20,	
      20,	0.05125,	0.07875,	0.07875,	0.07875,	0.05125,	0.05625,	0.08625,	0.05375,	30,	0.08625,	0.0875,	0.08625,	200,	200,	0.095,	0.095,	
      0.05625,	0.0475,	25,	200,	0.095,	40,	40,	40,	0.08375,	40,	0.08625,	0.0545,	0.06125,	34,	0.065,	45,	0.06,	0.084,	0.084,	0.08,	0.055,	
      0.05625,	47,	0.095,	0.095,	0.095,	200,	200,	200,	47,	0.053,	445,	0.08625,	0.08625,	0.08625,	0.08625,	0.08625,	44, 445,	7.5,	0.08625,	
      0.08375,	7.5,	0.05625,	0.0575,	0.08625,	0.0575,	200,	0.095,	0.08625,	200,	0.095,	0.0545,	0.077,	0.05625,	445,	0.095,	0.095,	0.065,	
      0.095,	0.095,	20,	0.04,	20,	0.0615,	20,	0.08625,	200,	200,	0.095,	0.04,	0.04,	40,	40,	0.042,	0.04,	0.055,	0.0525,	0.055,	30,	0.04,	0.042,	
      0.0525,	40,	30,	0.077,	0.077,	20,	20,	47,	47,	0.0615,	0.0615,	0.06693,	0.06693,	0.053,	0.084,	0.084,	0.084,	20,	47,	44,	47,	0.05625,	0.05625,
      0.0575,	0.0575,	425,	425,	20,	7.5,	7.5,	0.06693,	0.08375,	0.08375,	0.08375
    };
    #endregion bond coupon
    #region bond floating
    private bool[] bondFloating = new bool[]{
      false,	false,	false,	false,	true,	false,	false,	false,	false,	false,	false,	false,	false,	false,	false,	false,	false,	
      false,	false,	false,	false,	false,	false,	false,	false,	false,	false,	true,	true,	true,	false,	false,	false,	true,	false,	
      false,	false,	false,	false,	false,	false,	false,	false,	false,	false,	false,	false,	true,	false,	false,	false,	false,	
      false,	false,	false,	false,	false,	false,	true,	true,	true,	true,	false,	true,	false,	true,	false,	false,	true,	true,	false,	
      false,	true,	false,	true,	false,	false,	false,	true,	true,	true,	true,	true,	false,	true,	false,	false,	false,	true,	true,	false,	
      true,	true,	true,	true,	false,	true,	true,	true,	true, true,	false,	true,	false,	true,	false,	false,	false,	true,	false,	true,	false,	
      true,	false,	false,	true,	true,	true,	false,	true,	true,	false,	true,	true,	true,	false,	true,	false,	false,	false,	true,	false,	true,	
      true,	false,	false,	false,	false,	true,	true,	false,	false,	false,	false,	true,	true,	false,	false,	false,	false,	false,	false,	
      false,	false,	true,	false,	false,	false,	true,	true,	false,	false,	false,	false,	true,	true,	false,	true,	true,	true,	false,	true,	
      false,	false,	false,	true,	false,	true,	false,	false,	false,	false,	false,	false,	true,	false,	false,	false,	true,	true,	true,	true,	
      false,	true,	false,	false,	false,	false,	false,	true, true,	true,	false,	false,	true,	false,	false,	false,	false,	true,	false,	false,	
      true,	false,	false,	false,	false,	true,	false,	false,	false,	false,	false,	true,	false,	true,	false,	true,	false,	true,	true,	false,	
      false,	false,	true,	true,	false,	false,	false,	false,	false,	true,	false,	false,	false,	true,	true,	false,	false,	true,	true,	true,	
      true,	false,	false,	false,	false,	false,	false,	false,	false,	true,	true,	true,	true,	false,	false,	false,	false,	true,	true,	true,	
      true,	true,	false,	false,	false,	false
    };
    #endregion bond floating
    #region bond call start and end dates
    private string[,] callStarts = new string[,]{
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"}, {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"}, {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"}, {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"}, {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"}, {"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},
      {"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"}, {"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"10/15/2011",	"10/15/2012",	"10/15/2013",	"10/15/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},  {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"10/15/2011",	"10/15/2012",	"10/15/2013",	"10/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"4/30/2009",	"4/30/2010",	"4/30/2011",	"4/30/2012"}, {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2009",	"4/30/2010",	"4/30/2011",	"4/30/2012"},{"4/30/2009",	"4/30/2010",	"4/30/2011",	"4/30/2012"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"3/1/2012",	"3/1/2013",	"3/1/2014",	"3/1/2015"},{"3/1/2012",	"3/1/2013",	"3/1/2014",	"3/1/2015"},{"3/1/2012",	"3/1/2013",	"3/1/2014",	"3/1/2015"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/15/2011",	"10/15/2012",	"10/15/2013",	"10/15/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/15/2011",	"10/15/2012",	"10/15/2013",	"10/15/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/15/2011",	"10/15/2012",	"10/15/2013",	"10/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},  {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},
      {"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},
      {"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"10/1/2009",	"10/1/2010",	"10/1/2011",	"10/1/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"10/15/2011",	"10/15/2012",	"10/15/2013",	"10/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"10/15/2011",	"10/15/2012",	"10/15/2013",	"10/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/15/2009",	"5/15/2010",	"5/15/2011",	"5/15/2012"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"6/15/2011",	"1/0/00",	"1/0/00",	"1/0/00"},{"6/15/2011",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/30/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"6/15/2011",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"}};
    private List<Dt[]> bondCallStartDates = null;
    private string[,] callEnds = new string[,]{
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},
      {"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"10/14/2012",	"10/14/2013",	"10/14/2014",	"10/15/2015"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"10/14/2012",	"10/14/2013",	"10/14/2014",	"10/15/2015"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"4/29/2010",	"4/29/2011",	"4/29/2012",	"4/30/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"4/29/2010",	"4/29/2011",	"4/29/2012",	"4/30/2014"},{"4/29/2010",	"4/29/2011",	"4/29/2012",	"4/30/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"2/28/2013",	"2/28/2014",	"2/28/2015",	"3/1/2017"},{"2/28/2013",	"2/28/2014",	"2/28/2015",	"3/1/2017"},{"2/28/2013",	"2/28/2014",	"2/28/2015",	"3/1/2017"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/14/2012",	"10/14/2013",	"10/14/2014",	"10/15/2015"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/14/2012",	"10/14/2013",	"10/14/2014",	"10/15/2015"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"10/14/2012",	"10/14/2013",	"10/14/2014",	"10/15/2015"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},
      {"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},
      {"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"9/30/2010",	"9/30/2011",	"9/30/2012",	"10/1/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"10/14/2012",	"10/14/2013",	"10/14/2014",	"10/15/2015"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"10/14/2012",	"10/14/2013",	"10/14/2014",	"10/15/2015"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/14/2010",	"5/14/2011",	"5/14/2012",	"5/15/2014"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"6/16/2011",	"1/0/00",	"1/0/00",	"1/0/00"},{"6/16/2011",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},{"5/1/2018",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"6/16/2011",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},
      {"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"},{"1/0/00",	"1/0/00",	"1/0/00",	"1/0/00"}    };
    private List<Dt[]> bondCallEndDates = null;
    #endregion bond call start and end dates
    #region bond call prices
    private double[,] bondCallPrices = new double[,]{
      {0,	0,	0,	0}, {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {104.312,	102.875,	101.437,	0},{104.312,	102.875,	101.437,	0},{104.312,	102.875,	101.437,	0},{104.313,	102.875,	101.438,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.312,	102.875,	101.437,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},{104.75,	103.167,	101.583,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.312,	102.875,	101.437,	0},
      {104.312,	102.875,	101.437,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},{104.75,	103.167,	101.583,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},{0,	0,	0,	0},
      {104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{100,	0,	0,	0},{100,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{100,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{100,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.312,	102.875,	101.437,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{104.313,	102.875,	101.438,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{100,	0,	0,	0},
      {104,	102.667,	101.333,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {104,	102.667,	101.333,	0},{104,	102.667,	101.333,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{103.938,	102.625,	101.313,	0},
      {103.938,	102.625,	101.313,	0},{103.938,	102.625,	101.313,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.313,	102.875,	101.438,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{104.313,	102.875,	101.438,	0},{0,	0,	0,	0},{104.312,	102.875,	101.437,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {104.75,	103.167,	101.583,	0},{104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.313,	102.875,	101.438,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{100,	0,	0,	0},{100,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},{104.75,	103.167,	101.583,	0},{104.75,	103.167,	101.583,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.312,	102.875,	101.437,	0},
      {104.312,	102.875,	101.437,	0},{104.312,	102.875,	101.437,	0},{104.312,	102.875,	101.437,	0},{104.312,	102.875,	101.437,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.312,	102.875,	101.437,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {104.312,	102.875,	101.437,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},{104.313,	102.875,	101.438,	0},{0,	0,	0,	0},
      {104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},
      {104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},{104.75,	103.167,	101.583,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.313,	102.875,	101.438,	0},{0,	0,	0,	0},{0,	0,	0,	0},{104.75,	103.167,	101.583,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{100,	0,	0,	0},{100,	0,	0,	0},{0,	0,	0,	0},{100,	0,	0,	0},
      {100,	0,	0,	0},{100,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},{100,	0,	0,	0},{0,	0,	0,	0},{0,	0,	0,	0},
      {0,	0,	0,	0}
    };
    #endregion bond call prices
    #region bond names
    private string[] bondNames = new string[]{
      "LEN 5.5 09/01/14 Bond 1",	"LEN 5.6 05/31/15 Bond 2",	"JNY 5.125 11/15/14 Bond 3",	"HET 5.375 12/15/13 Bond 4",	"NRBS 0 03/13/12 Bond 5",	
      "LEN 5.6 05/31/15 Bond 6",	"LEN 5.6 05/31/15 Bond 7",	"LEN 5.5 09/01/14 Bond 8",	"GROHE 8.625 10/01/14 Bond 9",	"GROHE 8.625 10/01/14 Bond 10",	
      "GROHE 8.625 10/01/14 Bond 11",	"NXPBV 8.625 10/15/2015 Bond 12",	"MESSA 7.25 04/01/2013 Bond 13",	"HET 5.625 06/01/15 Bond 14",	
      "HET 5.625 06/01/15 Bond 15",	"HCA 5.75 03/15/14 Bond 16",	"GROHE 8.625 10/01/14 Bond 17",	"HCA 5.75 03/15/14 Bond 18",	"NELL 8.375 08/15/15 Bond 19",	
      "NSINO 7 06/26/17 Bond 20",	"LEN 5.6 05/31/15 Bond 21",	"NSINO 7 06/26/17 Bond 22",	"HET 6.5 06/01/16 Bond 23",	"COGNIS 9.5 05/15/14 Bond 24",	
      "COGNIS 9.5 05/15/14 Bond 25",	"HCA 5.75 03/15/14 Bond 26",	"HCA 5.75 03/15/14 Bond 27",	"NT 0 07/15/11 Bond 28",	"NT 0 07/15/11 Bond 29",	
      "F 0 04/15/2012 Bond 30",	"AMD 6 05/01/15 Bond 31",	"AMD 6 05/01/15 Bond 32",	"HCA 5.75 03/15/14 Bond 33",	"NT 0 07/15/11 Bond 34",	
      "TRW 6.375 03/15/14 Bond 35",	"TRW 6.375 03/15/14 Bond 36",	"HET 5.375 12/15/13 Bond 37",	"HET 5.625 06/01/15 Bond 38",	"NSINO 7 06/26/17 Bond 39",	
      "GROHE 8.625 10/01/14 Bond 40",	"GROHE 8.625 10/01/14 Bond 41",	"HET 5.375 12/15/13 Bond 42",	"HET 5.625 06/01/15 Bond 43",	"CIT 5 05/13/14 Bond 44",	
      "THC 7.375 02/01/13 Bond 45",	"STENA 6.125 02/01/17 Bond 46",	"CIT 5 05/13/14 Bond 47",	"NT 0 07/15/11 Bond 48",	"CIT 5 05/13/14 Bond 49",	
      "AMD 6 05/01/15 Bond 50",	"MESSA 7.25 04/01/2013 Bond 51",	"STENA 6.125 02/01/17 Bond 52",	"COGNIS 9.5 05/15/14 Bond 53",	
      "COGNIS 9.5 05/15/14 Bond 54",	"AMD 6 05/01/15 Bond 55",	"AMD 6 05/01/15 Bond 56",	"AMD 6 05/01/15 Bond 57",	"BSC 7.625 12/07/09 Bond 58",	
      "BSC 0 02/01/12 Bond 59",	"NT 0 07/15/11 Bond 60",	"NT 0 07/15/11 Bond 61",	"BSC 0 09/26/13 Bond 62",	"BSC 4.75 12/07/12 Bond 63",	
      "BSC 0 02/01/12 Bond 64",	"JNY 5.125 11/15/14 Bond 65",	"BSC 0 08/15/11 Bond 66",	"BSC 7.25 02/01/18 Bond 67",	"BSC 7.625 12/07/09 Bond 68",	
      "NT 0 07/15/11 Bond 69",	"BSC 0 01/30/09 Bond 70",	"BSC 3.25 03/25/09 Bond 71",	"COGNIS 9.5 05/15/14 Bond 72",	"NT 0 07/15/11 Bond 73",	
      "BSC 4.55 06/23/10 Bond 74",	"BSC 0 10/22/10 Bond 75",	"AMD 6 05/01/15 Bond 76",	"BSC 4.55 06/23/10 Bond 77",	"BSC 7.25 02/01/18 Bond 78",	
      "CIT 0 11/30/11 Bond 79",	"NT 0 07/15/11 Bond 80",	"CIT 0 11/30/11 Bond 81",	"CIT 0 11/30/11 Bond 82",	"BSC 0 08/15/11 Bond 83",	
      "BSC 7.625 12/07/09 Bond 84",	"MER 0 01/31/14 Bond 85",	"NSINO 7 06/26/17 Bond 86",	"NSINO 7 06/26/17 Bond 87",	"HET 5.75 10/01/17 Bond 88",	
      "F 0 04/15/2012 Bond 89",	"BSC 0 02/01/12 Bond 90",	"COGNIS 9.5 05/15/14 Bond 91",	"IKB 0 01/24/12 Bond 92",	"CFC 0 11/23/10 Bond 93",	
      "IKB 0 01/24/12 Bond 94",	"NT 0 07/15/11 Bond 95",	"HET 5.625 06/01/15 Bond 96",	"NT 0 07/15/11 Bond 97",	"IKB 0 01/24/12 Bond 98",	
      "IKB 0 01/24/12 Bond 99",	"WM 0 02/04/11 Bond 100", "COGNIS 0 09/15/13 Bond 101",	"COGNIS 9.5 05/15/14 Bond 102",	"COGNIS 0 09/15/13 Bond 103",	
      "COGNIS 9.5 05/15/14 Bond 104",	"COGNIS 0 09/15/13 Bond 105",	"C 8.4 04/29/49 Bond 106",	"C 8.4 04/29/49 Bond 107",	"THC 7.375 02/01/13 Bond 108",	
      "GLBIR 0 01/18/12 Bond 109",	"C 8.4 04/29/49 Bond 110",	"WM 0 02/04/11 Bond 111",	"WM 5.5 08/24/11 Bond 112",	"WM 0 03/22/12 Bond 113",	
      "C 8.4 04/29/49 Bond 114",	"WM 5.5 08/24/11 Bond 115",	"WM 0 02/04/11 Bond 116",	"CFC 0 11/23/10 Bond 117",	"CFC 0 11/23/10 Bond 118",	
      "GROHE 8.625 10/01/14 Bond 119",	"GLBIR 0 01/18/12 Bond 120",	"GLBIR 0 01/18/12 Bond 121",	"LEN 6.5 04/15/16 Bond 122",	"BSC 0 10/22/10 Bond 123",
      "BSC 0 08/15/11 Bond 124",	"BSC 0 02/01/12 Bond 125",	"BSC 7.25 02/01/18 Bond 126",	"BSC 0 02/01/12 Bond 127",	"NELL 8.375 08/15/15 Bond 128",	
      "NELL 8.375 08/15/15 Bond 129",	"NXPBV 8.625 10/15/2015 Bond 130",	"CIT 0 11/30/11 Bond 131",	"BSC 4.55 06/23/10 Bond 132",	"BSC 0 08/15/11 Bond 133",	
      "BSC 0 08/15/11 Bond 134",	"C 8.4 04/29/49 Bond 135",	"SEAT 8 04/30/14 Bond 136",	"BSC 3.25 03/25/09 Bond 137",	"BSC 7.625 12/07/09 Bond 138",	
      "BSC 0 01/30/09 Bond 139",	"BSC 0 02/01/12 Bond 140",	"LEN 5.95 03/01/13 Bond 141",	"NELL 8.375 08/15/15 Bond 142",	"SEAT 8 04/30/14 Bond 143",	
      "SEAT 8 04/30/14 Bond 144",	"WM 0 02/04/11 Bond 145",	"SLMA 0 03/15/11 Bond 146",	"JNY 5.125 11/15/14 Bond 147",	"AXL 7.875 03/01/17 Bond 148",	
      "AXL 7.875 03/01/17 Bond 149",	"AXL 7.875 03/01/17 Bond 150",	"CTX 5.125 10/01/13 Bond 151",	"DHI 5.625 01/15/16 Bond 152",	
      "NXPBV 8.625 10/15/2015 Bond 153",	"RYL 5.375 05/15/12 Bond 154",	"WM 0 03/22/12 Bond 155",	"NXPBV 8.625 10/15/2015 Bond 156",	
      "MESSA 7.25 04/01/2013 Bond 157",	"GROHE 8.625 10/01/14 Bond 158",	"COGNIS 0 09/15/13 Bond 159",	"COGNIS 0 09/15/13 Bond 160",	
      "COGNIS 9.5 05/15/14 Bond 161",	"COGNIS 9.5 05/15/14 Bond 162",	"DHI 5.625 01/15/16 Bond 163",	"BSC 4.75 12/07/12 Bond 164",	
      "BSC 0 09/26/13 Bond 165",	"COGNIS 0 09/15/13 Bond 166",	"COGNIS 9.5 05/15/14 Bond 167",	"CFC 0 11/23/10 Bond 168",	"CFC 0 11/23/10 Bond 169",	
      "CFC 0 11/23/10 Bond 170",	"NELL 8.375 08/15/15 Bond 171",	"CFC 0 11/23/10 Bond 172",	"NXPBV 8.625 10/15/2015 Bond 173",	
      "CTX 5.45 08/15/12 Bond 174",	"STENA 6.125 02/01/17 Bond 175",	"CIT 0 11/30/11 Bond 176",	"INTEL 6.5 11/01/13 Bond 177",	
      "MER 0 07/22/14 Bond 178",	"AMD 6 05/01/15 Bond 179",	"C 8.4 04/29/49 Bond 180",	"C 8.4 04/29/49 Bond 181",	"UIS 8 10/15/12 Bond 182",	
      "WM 5.5 06/10/19 Bond 183",	"RCL 5.625 01/27/14 Bond 184",	"GLBIR 0 01/18/12 Bond 185",	"COGNIS 9.5 05/15/14 Bond 186",	
      "COGNIS 9.5 05/15/14 Bond 187",	"COGNIS 9.5 05/15/14 Bond 188",	"COGNIS 0 09/15/13 Bond 189",	"COGNIS 0 09/15/13 Bond 190",	
      "COGNIS 0 09/15/13 Bond 191",	"GLBIR 0 01/18/12 Bond 192",	"WB 5.3 10/15/11 Bond 193",	"F 0 04/15/2012 Bond 194",	"GROHE 8.625 10/01/14 Bond 195",	
      "GROHE 8.625 10/01/14 Bond 196",	"GROHE 8.625 10/01/14 Bond 197",	"GROHE 8.625 10/01/14 Bond 198",	"GROHE 8.625 10/01/14 Bond 199",	
      "CFC 0 05/07/12 Bond 200", "F 0 04/15/2012 Bond 201",	"IKB 0 01/24/12 Bond 202",	"GROHE 8.625 10/01/14 Bond 203",	"NELL 8.375 08/15/15 Bond 204",	
      "IKB 0 01/24/12 Bond 205",	"HET 5.625 06/01/15 Bond 206",	"HET 5.75 10/01/17 Bond 207",	"GROHE 8.625 10/01/14 Bond 208",	"HET 5.75 10/01/17 Bond 209",	
      "COGNIS 0 09/15/13 Bond 210",	"COGNIS 9.5 05/15/14 Bond 211",	"NXPBV 8.625 10/15/2015 Bond 212",	"COGNIS 0 09/15/13 Bond 213",	"COGNIS 9.5 05/15/14 Bond 214",	
      "CTX 5.45 08/15/12 Bond 215",	"WFC 7.7 12/29/49 Bond 216",	"HET 5.625 06/01/15 Bond 217",	"F 0 04/15/2012 Bond 218",	"COGNIS 9.5 05/15/14 Bond 219",	
      "COGNIS 9.5 05/15/14 Bond 220",	"INTEL 6.5 11/01/13 Bond 221",	"COGNIS 9.5 05/15/14 Bond 222",	"COGNIS 9.5 05/15/14 Bond 223",	"WM 0 02/04/11 Bond 224",	
      "LEH 4 03/09/15 Bond 225",	"WM 0 02/04/11 Bond 226",	"GS 6.15 04/01/18 Bond 227",	"WM 0 02/04/11 Bond 228",	"NXPBV 8.625 10/15/2015 Bond 229",	
      "COGNIS 0 09/15/13 Bond 230",	"COGNIS 0 09/15/13 Bond 231",	"COGNIS 9.5 05/15/14 Bond 232",	"LEH 4 03/09/15 Bond 233",	"LEH 4 03/09/15 Bond 234",	
      "WM 0 09/17/12 Bond 235",	"WM 0 09/17/12 Bond 236",	"WM 4.2 01/15/10 Bond 237",	"WM 4 01/15/09 Bond 238",	"WM 5.5 08/24/11 Bond 239",	
      "WM 5.25 09/15/17 Bond 240",	"WM 5.5 08/24/11 Bond 241",	"WM 0 03/22/12 Bond 242",	"WM 4 01/15/09 Bond 243",	"WM 4.2 01/15/10 Bond 244",	
      "WM 5.25 09/15/17 Bond 245",	"WM 0 09/17/12 Bond 246",	"WM 0 03/22/12 Bond 247",	"WFC 7.7 12/29/49 Bond 248",	"WFC 7.7 12/29/49 Bond 249",	
      "WM 0 02/04/11 Bond 250",	"WM 0 02/04/11 Bond 251",	"GLBIR 0 01/18/12 Bond 252",	"GLBIR 0 01/18/12 Bond 253",	"GS 6.15 04/01/18 Bond 254",	
      "GS 6.15 04/01/18 Bond 255",	"GLBIR 6.693 06/15/16 Bond 256",	"GLBIR 6.693 06/15/16 Bond 257",	"WB 5.3 10/15/11 Bond 258",	"C 8.4 04/29/49 Bond 259",	
      "C 8.4 04/29/49 Bond 260",	"C 8.4 04/29/49 Bond 261",	"WM 0 02/04/11 Bond 262",	"GLBIR 0 01/18/12 Bond 263",	"CFC 0 05/07/12 Bond 264",	
      "GLBIR 0 01/18/12 Bond 265",	"HET 5.625 06/01/15 Bond 266",	"HET 5.625 06/01/15 Bond 267",	"HET 5.75 10/01/17 Bond 268",	
      "HET 5.75 10/01/17 Bond 269",	"NT 0 07/15/11 Bond 270",	"NT 0 07/15/11 Bond 271",	"WM 0 02/04/11 Bond 272",	"IKB 0 01/24/12 Bond 273",	
      "IKB 0 01/24/12 Bond 274",	"GLBIR 6.693 06/15/16 Bond 275",	"NELL 8.375 08/15/15 Bond 276",	"NELL 8.375 08/15/15 Bond 277",	"NELL 8.375 08/15/15 Bond 278"
    };
    #endregion bond names
    private Bond[] bonds = null;
    private Dt bondSettleDate = new Dt(9, 4, 2009);
    #region bond notionals
    private double[] bondNotionals = new double[]{
      5,	6,	5,	10,	15,	5,	-5,	-5,	5,	5,	5,	5,	3,	5,	5,	5,	5,	7.5,	5,	3,	-6,	3,	4,	3,	5,	-5,	-5,
      4.5,	2,	20,	5,	2,	-2.5,	3,	2,	2.2,	-5,	5,	3,	5,	5,	-5,	5,	10,	2,	2.5,	10,	10,	10,	2,	-3,	2.5,
      5,	3,	2,	1,	2,	-5,	5,	-2,	2,	10,	4.11,	10,	-2,	10,	10,	15,	-2,	10,	3.686,	3,	-2,	5,	5,	2,	4.175,	
      -2,	2,	-2,	2,	1.4,	10,	-6,	5,	6,	3,	5,	-5,	10,	-1,	2,	10,	1.5,	-2,	2,	-1.5,	1,	0.5,	10, 5,	-5,	3,
      -3,	2,	15,	5,	-2,	6.4,	-10,	5,	2.15,	5,	-10,	-2.15,	-3,	5,	5,	-1,	5,	5,	9,	-5,	-5,	-5,	-8,	-10,
      3,	3,	-4,	1,	-9.175,	-5,	-10,	5,	6.5,	-3.686,	-4,	-10,	-10,	5,	3,	5,	3,	3,	5,	5,	5,	7,	1.25,
      5,	5,	2,	10,	10,	6,	3,	4,	-2,	-1,	2,	1,	10,	-4.11,	-10,	-2,	2,	-2,	-6.5,	-1.5,	2,	-10,	5,	10.27,
      3,	1,	5,	4.677,	2.5,	5,	5,	5,	2.5,	3,	-5,	-2,	-1,	-2,	3,	2,	3,	-5,	5,	-5,	-3,	-3,	-2,	-1.5,	-3,
      5, -5,	-1,	-4,	-2,	-1.3,	6,	18,	-5,	12,	-3,	3,	-5,	-2,	2,	6.5,	5,	18,	-5,	-3,	-3,	5,	-2,	-2,	7.5,	8,
      3.5,	5,	1,	-2.5,	-2,	-2,	2,	-7.61,	0.170464,	5,	5,	5,	4.5,	2,	3,	-2,	-15,	-4.5,	-5,	-3,	-10,	0.01875,	
      -2,	-3,	-2,	-2,	-5.3,	0.0422270786666666,	-2,	-3,	4.5,	-4.0194,	-5,	-5,	-5,	-5,	-2,	3.873,	-0.6,	-0.428,	
      -2.88,	2.88,	-3.6,	3.6,	-7,	-3,	-2.22,	0.324,	-0.324,	-0.1,	-7.5,	-6,	0.687796875
    };
    #endregion bond notionals
    #region bond discount curves
    //0: USD_LIBOR, 1:EUR_LIBOR, 2: GBP_LIBOR
    private int[] bondDiscountCurvesIndex = new int[]{
      0,	0,	0,	0,	1,	0,	0,	0,	1,	1,	1,	1,	1,	0,	0,	0,	1,	0,	1,	1,	0,	1,	0,	1,	1,	0,	0,	0,	
      0,	0,	0,	0,	0,	0,	1,	1,	0,	0,	1,	1,	1,	0,	0,	1,	0,	1,	1,	0,	1,	0,	1,	1,	1,	1,	0,	0,	
      0,	0,	0,	0,	0,	1,	2,	0,	0,	0,	0,	0,	0,	0,	0,	1,	0,	0,	0,	0,	0,	0,	1,	0,	1,	1,	0,	0,	
      1,	1,	1,	0,	0,	0,	1,	2,	1,	2,	0,	0,	0,	2,	2,	0,  1,	1,	1,	1,	1,	0,	0,	0,	0,	0,	0,	0,	
      0,	0,	0,	0,	1,	1,	1,	0,	0,	0,	0,	0,	0,	0,	0,	1,	1,	1,	1,	0,	0,	0,	0,	1,	0,	0,	0,	0,	
      0,	1,	1,	1,	0,	0,	0,	0,	0,	0,	0,	0,	1,	0,	0,	1,	1,	1,	1,	1,	1,	1,	0,	2,	1,	1,	1,	1,	
      1,	1,	1,	1,	1,	0,	1,	1,	0,	1,	0,	0,	0,	0,	2,	1,	0,	1,	1,	1,	1,	1,	1,	0,	0,	0,	1,	1,	
      1,	1,	1,	0,  0,	2,	1,	1,	2,	0,	0,	1,	0,	1,	1,	1,	1,	1,	0,	0,	0,	0,	1,	1,	0,	1,	1,	0,	
      1,	0,	0,	0,	1,	1,	1,	1,	1,	1,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	
      0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	0,	2,	2,	0,	1,	1,	1
    };
    #endregion bond discount curves
    #region bond survival curves
    private SurvivalCurve[] bondSurvivalCurves = null; 
    private string[] bondSurvivalCurvesNames = new string[]{
      "LEN USD SNRFOR MR",	"LEN USD SNRFOR MR",	"JNY USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",	"NRBS EUR SNRFOR MM",	
      "LEN USD SNRFOR MR",	"LEN USD SNRFOR MR",	"LEN USD SNRFOR MR",	"GROHE EUR SNRFOR MM",	"GROHE EUR SNRFOR MM",	
      "GROHE EUR SNRFOR MM",	"NXP EUR SNRFOR MM",	"MESSA EUR SNRFOR MM",	"HET-HOC USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",	
      "HCA USD SNRFOR MR",	"GROHE EUR SNRFOR MM",	"HCA USD SNRFOR MR",	"AIND-LYON EUR SNRFOR MM",	"NSINO EUR SNRFOR MM",	
      "LEN USD SNRFOR MR",	"NSINO EUR SNRFOR MM",	"HET-HOC USD SNRFOR MR",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	
      "HCA USD SNRFOR MR",	"HCA USD SNRFOR MR",	"NT USD SNRFOR MR",	"NT USD SNRFOR MR",	"F-MotCrLLC USD SNRFOR MR",	
      "AMD USD SNRFOR MR",	"AMD USD SNRFOR MR",	"HCA USD SNRFOR MR",	"NT USD SNRFOR MR",	"TRWAuto EUR SNRFOR XR",	
      "TRWAuto EUR SNRFOR XR",	"HET-HOC USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",	"NSINO EUR SNRFOR MM",	"GROHE EUR SNRFOR MM",	
      "GROHE EUR SNRFOR MM",	"HET-HOC USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",	"CIT EUR SNRFOR MM",	"THC USD SNRFOR MR",	
      "STENA EUR SNRFOR MM",	"CIT EUR SNRFOR MM",	"NT USD SNRFOR MR",	"CIT EUR SNRFOR MM",	"AMD USD SNRFOR MR",
      "MESSA EUR SNRFOR MM",	"STENA EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"AMD USD SNRFOR MR",	
      "AMD USD SNRFOR MR",	"AMD USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"NT USD SNRFOR MR",	
      "NT USD SNRFOR MR",	"BSC EUR SNRFOR MM",	"BSC GBP SNRFOR MR",	"BSC USD SNRFOR MR",	"JNY USD SNRFOR MR",	
      "BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"NT USD SNRFOR MR",	"BSC USD SNRFOR MR",	
      "BSC USD SNRFOR MR",	"COGN EUR SNRFOR MM",	"NT USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	
      "AMD USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"CIT EUR SNRFOR MM",	"NT USD SNRFOR MR",	
      "CIT EUR SNRFOR MM",	"CIT EUR SNRFOR MM",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"MER EUR SNRFOR MM",
      "NSINO EUR SNRFOR MM",	"NSINO EUR SNRFOR MM",	"HET-HOC USD SNRFOR MR",	"F-MotCrLLC USD SNRFOR MR",	"BSC USD SNRFOR MR",	
      "COGN EUR SNRFOR MM",	"IKB GBP SNRFOR MM",	"CCR EUR SNRFOR MM",	"IKB GBP SNRFOR MM",	"NT USD SNRFOR MR",	
      "HET-HOC USD SNRFOR MR",	"NT USD SNRFOR MR",	"IKB GBP SNRFOR MM",	"IKB GBP SNRFOR MM",	"WM-Bank USD SNRFOR MR",
      "COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	
      "C USD SNRFOR MR",	"C USD SNRFOR MR",	"THC USD SNRFOR MR",	"GLTNR USD SNRFOR MR",	"C USD SNRFOR MR",	
      "WM-Bank USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"C USD SNRFOR MR",	"WM USD SNRFOR MR",	
      "WM-Bank USD SNRFOR MR",	"CCR EUR SNRFOR MM",	"CCR EUR SNRFOR MM",	"GROHE EUR SNRFOR MM",	"GLTNR USD SNRFOR MR",	
      "GLTNR USD SNRFOR MR",	"LEN USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",
      "BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"AIND-LYON EUR SNRFOR MM",	"AIND-LYON EUR SNRFOR MM",	"NXP EUR SNRFOR MM",	
      "CIT EUR SNRFOR MM",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"C USD SNRFOR MR",	
      "SPGIM EUR SNRFOR MM",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",	"BSC USD SNRFOR MR",
      "LEN USD SNRFOR MR",	"AIND-LYON EUR SNRFOR MM",	"SPGIM EUR SNRFOR MM",	"SPGIM EUR SNRFOR MM",	"WM-Bank USD SNRFOR MR",	
      "SLMA USD SNRFOR MR",	"JNY USD SNRFOR MR",	"AXL-Inc USD SNRFOR MR",	"AXL-Inc USD SNRFOR MR",	"AXL-Inc USD SNRFOR MR",	
      "CTX USD SNRFOR MR",	"DHI USD SNRFOR MR",	"NXP EUR SNRFOR MM",	"RYL USD SNRFOR MR",	"WM USD SNRFOR MR",	
      "NXP EUR SNRFOR MM",	"MESSA EUR SNRFOR MM",	"GROHE EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	
      "COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"DHI USD SNRFOR MR",	"BSC GBP SNRFOR MR",	"BSC EUR SNRFOR MM",
      "COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"CCR EUR SNRFOR MM",	"CCR EUR SNRFOR MM",	"CCR EUR SNRFOR MM",
      "AIND-LYON EUR SNRFOR MM",	"CCR EUR SNRFOR MM",	"NXP EUR SNRFOR MM",	"CTX USD SNRFOR MR",	"STENA EUR SNRFOR MM",
      "CIT EUR SNRFOR MM",	"INTEL USD SNRFOR MR",	"MER EUR SNRFOR MM",	"AMD USD SNRFOR MR",	"C USD SNRFOR MR",	"C USD SNRFOR MR",	
      "UIS USD SNRFOR MR",	"WM-Bank GBP SUBLT2 MR",	"RCL EUR SNRFOR MM",	"GLTNR USD SNRFOR MR",	"COGN EUR SNRFOR MM",
      "COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",
      "GLTNR USD SNRFOR MR",	"WB USD SNRFOR MR",	"F-MotCrLLC USD SNRFOR MR",	"GROHE EUR SNRFOR MM",	"GROHE EUR SNRFOR MM",
      "GROHE EUR SNRFOR MM",	"GROHE EUR SNRFOR MM",	"GROHE EUR SNRFOR MM",	"CCR USD SNRFOR MR","F-MotCrLLC USD SNRFOR MR",	
      "IKB GBP SNRFOR MM",	"GROHE EUR SNRFOR MM",	"AIND-LYON EUR SNRFOR MM",	"IKB GBP SNRFOR MM",	"HET-HOC USD SNRFOR MR",
      "HET-HOC USD SNRFOR MR",	"GROHE EUR SNRFOR MM",	"HET-HOC USD SNRFOR MR",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",
      "NXP EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"CTX USD SNRFOR MR",	"WFC USD SNRFOR MR",	
      "HET-HOC USD SNRFOR MR",	"F-MotCrLLC USD SNRFOR MR",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"INTEL USD SNRFOR MR",
      "COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"WM-Bank USD SNRFOR MR",	"LEH EUR SNRFOR MM",	"WM-Bank USD SNRFOR MR",
      "GS USD SNRFOR MR",	"WM-Bank USD SNRFOR MR",	"NXP EUR SNRFOR MM",	"COGN EUR SNRFOR MM",	"COGN EUR SNRFOR MM",
      "COGN EUR SNRFOR MM",	"LEH EUR SNRFOR MM",	"LEH EUR SNRFOR MM",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",
      "WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	
      "WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WM USD SNRFOR MR",	"WFC USD SNRFOR MR",	"WFC USD SNRFOR MR",	
      "WM-Bank USD SNRFOR MR",	"WM-Bank USD SNRFOR MR",	"GLTNR USD SNRFOR MR",	"GLTNR USD SNRFOR MR",	"GS USD SNRFOR MR",	
      "GS USD SNRFOR MR",	"GLTNR USD SNRFOR MR",	"GLTNR USD SNRFOR MR",	"WB USD SNRFOR MR",	"C USD SNRFOR MR",	
      "C USD SNRFOR MR",	"C USD SNRFOR MR",	"WM-Bank USD SNRFOR MR",	"GLTNR USD SNRFOR MR",	"CCR USD SNRFOR MR",
      "GLTNR USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",	"HET-HOC USD SNRFOR MR",
      "NT USD SNRFOR MR",	"NT USD SNRFOR MR",	"WM-Bank USD SNRFOR MR",	"IKB GBP SNRFOR MM",	"IKB GBP SNRFOR MM",
      "GLTNR USD SNRFOR MR",	"AIND-LYON EUR SNRFOR MM",	"AIND-LYON EUR SNRFOR MM",	"AIND-LYON EUR SNRFOR MM"
    };
    #endregion bond survival curves
    private double bondMeanReversion = 0.3;
    private double bondSigma = 0.1;
    #region bond market quotes
    private double[] bondMarketQuotes = new double[]{
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	65,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	65,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,  100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	65,	100,	100,	100,	100,	100,	100, 65,	100,	100,	100,	100,	100,	100,	100,	100,	
      100,	100,	100,	100,	100,	100,	91.5,	100,	65,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,
      91.5,	91.5,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	
      100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100,	100
    };
    #endregion bond market quotes
    #region bond current coupons
    private double[] bondCurrentCoupon = new double[]{
      0.055,	0.056,	0.05125,	0.05375,	0.01787,	0.056,	0.056,	0.055,	0.08625,	0.08625,	0.08625,	0.08625,	
      0.0875,	0.05625,	0.05625,	0.0575,	0.08625,	0.0575,	0.08375,	0.07,	0.056,	0.07,	0.065,	0.095,	0.095,	
      0.0575,	0.0575,	0.0541,	0.0541,	0.0561,	0.06,	0.06,	0.0575,	0.0541,	0.06375,	0.06375,	0.05375,	0.05625,	
      0.07,	0.08625,	0.08625,	0.05375,	0.05625,	0.05,	0.07375,	0.06125,	0.05,	0.0541,	0.05,	0.06,	0.0875,	
      0.06125,	0.095,	0.095,	0.06,	0.06,	0.06,	0.07625,	0.0136438,	0.0541,	0.0541,	0.0171625,	0.0475,	
      0.0136438,	0.05125,	0.0144125,	0.0725,	0.07625,	0.0541,	0.038075,	0.0325,	0.095,	0.0541,	0.0455,	
      0.012825,	0.06,	0.0455,	0.0725,	0.02198,	0.0541,	0.02198,	0.02198,	0.0144125,	0.07625,	0.0239,	0.07,	
      0.07,	0.0575,	0.0561,	0.0136438,	0.095,	0.022875,	0.02288,	0.022875,	0.0541,	0.05625,	0.0541,	0.022875,	
      0.022875,	0.0021, 0.03663,	0.095,	0.03663,	0.095,	0.03663,	0.084,	0.084,	0.07375,	0.049725,	0.084,	
      0.0021,	0.055,	0.018775,	0.084,	0.055,	0.0021,	0.02288,	0.02288,	0.08625,	0.049725,	0.049725,	0.065,	
      0.012825,	0.0144125,	0.0136438,	0.0725,	0.0136438,	0.08375,	0.08375,	0.08625,	0.02198,	0.0455,	0.0144125,	
      0.0144125,	0.084,	0.08,	0.0325,	0.07625,	0.038075,	0.0136438,	0.0595,	0.08375,	0.08,	0.08,	0.0021,	0.0075688,	
      0.05125,	0.07875,	0.07875,	0.07875,	0.05125,	0.05625,	0.08625,	0.05375,	0.018775,	0.08625,	0.0875,	
      0.08625,	0.03663,	0.03663,	0.095,	0.095,	0.05625,	0.0475,	0.0171625,	0.03663,	0.095,	0.02288,	0.02288,	
      0.02288,	0.08375,	0.02288,	0.08625,	0.0545,	0.06125,	0.02198,	0.065,	0.02903,	0.06,	0.084,	0.084,	
      0.08,	0.055,	0.05625,	0.049725,	0.095,	0.095,	0.095,	0.03663,	0.03663,	0.03663,	0.049725,	0.053,	0.0561,	
      0.08625,	0.08625,	0.08625,	0.08625,	0.08625,	0.0167563, 0.0561,	0.022875,	0.08625,	0.08375,	0.022875,
      0.05625,	0.0575,	0.08625,	0.0575,	0.03663,	0.095,	0.08625,	0.03663,	0.095,	0.0545,	0.077,	0.05625,	
      0.0561,	0.095,	0.095,	0.065,	0.095,	0.095,	0.0021,	0.04,	0.0021,	0.0615,	0.0021,	0.08625,	0.03663,	
      0.03663,	0.095,	0.04,	0.04,	0.0321625,	0.0321625,	0.042,	0.04,	0.055,	0.0525,	0.055,	0.018775,	0.04,	
      0.042,	0.0525,	0.0321625,	0.018775,	0.077,	0.077,	0.0021,	0.0021,	0.049725,	0.049725,	0.0615,	0.0615,	
      0.06693,	0.06693,	0.053,	0.084,	0.084,	0.084,	0.0021,	0.049725,	0.0167563,	0.049725,	0.05625,	0.05625,
      0.0575,	0.0575,	0.0541,	0.0541,	0.0021,	0.022875,	0.022875,	0.06693,	0.08375,	0.08375,	0.08375
    };
    #endregion bond current coupons
    #region bond recoveries
    private double[] bondRecoveries = new double[]{
      0.403333333333333,	0.403333333333333,	0.35,	0.0557,	0.4,	0.403333333333333,	0.403333333333333,	0.403333333333333,	
      0.3,	0.3,	0.3,	0.1,	0.25,	0.0557,	0.0557,	0.32,	0.3,	0.32,	0.0511,	0.35,	0.403333333333333,	0.35,	0.0557,	
      0.3,	0.3,	0.32,	0.32,	0.11,	0.11,	0.2593571,	0.146666666666667,	0.146666666666667,	0.32,	0.11,	0.2,	0.2,	
      0.0557,	0.0557,	0.35,	0.3,	0.3,	0.0557,	0.0557,	0.368533333333332,	0.3375,	0.309583333333333,	0.368533333333332,	
      0.11,	0.368533333333332,	0.146666666666667,	0.25,	0.309583333333333,	0.3,	0.3,	0.146666666666667,	0.146666666666667,
      0.146666666666667,	0.4,	0.4,	0.11,	0.11,	0.4,	0.4,	0.4,	0.35,	0.4,	0.4,	0.4,	0.11,	0.4,	0.4,	0.3,
      0.11,	0.4,	0.4,	0.146666666666667,	0.4,	0.4,	0.368533333333332,	0.11,	0.368533333333332,	0.368533333333332,
      0.4,	0.4,	0.400000000662274,	0.35,	0.35,	0.0557,	0.2593571,	0.4,	0.3,	0.4,	0.409,	0.4,	0.11,	0.0557,
      0.11,	0.4,	0.4,	0.31, 0.3,	0.3,	0.3,	0.3,	0.3,	0.4,	0.4,	0.3375,	0.03,	0.4,	0.31,	0.57,	0.57,	0.4,	0.57,	0.31,	
      0.409,	0.409,	0.3,	0.03,	0.03,	0.403333333333333,	0.4,	0.4,	0.4,	0.4,	0.4,	0.0511,	0.0511,	0.1,	0.368533333333332,	
      0.4,	0.4,	0.4,	0.4,	0.2614,	0.4,	0.4,	0.4,	0.4,	0.403333333333333,	0.0511,	0.2614,	0.2614,	0.31,	0.4,	0.35,
      0.128571428571429,	0.128571428571429,	0.128571428571429,	0.41834411111111,	0.4,	0.1,	0.4,	0.57,	0.1,	0.25,	0.3,	
      0.3,	0.3,	0.3,	0.3,	0.4,	0.4,	0.4,	0.3,	0.3,	0.409,	0.409,	0.409,	0.0511,	0.409,	0.1,	0.41834411111111,	
      0.309583333333333,	0.368533333333332,	0.3,	0.400000000662274,	0.146666666666667,	0.4,	0.4,	0.15,	0.31,	0.3675,	0.03,
      0.3,	0.3,	0.3,	0.3,	0.3,	0.3,	0.03,	0.4,	0.2593571,	0.3,	0.3,	0.3,	0.3,	0.3,	0.409, 0.2593571,	0.4,	0.3,	
      0.0511,	0.4,	0.0557,	0.0557,	0.3,	0.0557,	0.3,	0.3,	0.1,	0.3,	0.3,	0.41834411111111,	0.4,	0.0557,	0.2593571,	
      0.3,	0.3,	0.3,	0.3,	0.3,	0.31,	0.0895,	0.31,	0.400000000851495,	0.31,	0.1,	0.3,	0.3,	0.3,	0.0895,	0.0895,	0.57,	
      0.57,	0.57,	0.57,	0.57,	0.57,	0.57,	0.57,	0.57,	0.57,	0.57,	0.57,	0.57,	0.4,	0.4,	0.31,	0.31,	0.03,	0.03,	0.400000000851495,	
      0.400000000851495,	0.03,	0.03,	0.4,	0.4,	0.4,	0.4,	0.31,	0.03,	0.409,	0.03,	0.0557,	0.0557,	0.0557,	0.0557,	0.11,	0.11,	
      0.31,	0.4,	0.4,	0.03,	0.0511,	0.0511,	0.0511
    };
    #endregion bond recoveries
    private bool ignoreCall = false;
    private bool forceSurvivalCalibration = false;
    private bool includeSurvivalCurve = true;
    private bool useZspreadInSensitivities = false;
    private BondPricer[] bondPricers = null;
    #endregion bond data

    #endregion Data
  }
}