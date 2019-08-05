//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  ///  Bond return leg tests
  /// </summary>
  [TestFixture(BondType.UKGilt)]
  [TestFixture(BondType.None)]
  public class TestBondReturnLeg
  {
    public TestBondReturnLeg(BondType bondType)
    {
      _bondType = bondType;
    }

    private readonly BondType _bondType;

    [Flags]
    private enum TestFlags
    {
      None = 0,
      WithAmortizing = 1,
      WithCredit = 2,
    }

    #region No arbitrage conditions

    /// <summary>
    ///  When initial bond price is arbitrage free, the PV of the bond return leg
    ///  should match the funding costs: (1 - DiscountFactor)*InitialPrice.
    /// </summary>
    [TestCase(20160209, 20160907, 0)]
    [TestCase(20160209, 20170907, 0)]
    [TestCase(20160209, 20180907, 0)]
    [TestCase(20160209, 20190907, 0)]
    [TestCase(20160209, 20200907, 0)]
    [TestCase(20160209, 20210907, 0)]
    [TestCase(20160209, 20220907, 0)]
    [TestCase(20160209, 20230907, 0)]
    [TestCase(20160209, 20240907, 0)]
    [TestCase(20160209, 20250907, 0)]
    [TestCase(20160209, 20250908, 0)]
    [TestCase(20160209, 20250908, 10)]
    public void SinglePeriod(
      int trsEffective, int trsMaturity, int minute)
    {
      NoArbitrage(TestFlags.None, trsEffective, trsMaturity, minute);
    }

    [TestCase(20160209, 20160301, 0)]
    [TestCase(20160209, 20160907, 0)]
    [TestCase(20160209, 20170907, 0)]
    [TestCase(20160209, 20180907, 0)]
    [TestCase(20160209, 20190907, 0)]
    [TestCase(20160209, 20200907, 0)]
    [TestCase(20160209, 20210907, 0)]
    [TestCase(20160209, 20220907, 0)]
    [TestCase(20160209, 20230907, 0)]
    [TestCase(20160209, 20240907, 0)]
    [TestCase(20160209, 20250907, 0)]
    [TestCase(20160209, 20250908, 0)]
    [TestCase(20160209, 20250908, 10)]
    [TestCase(20250609, 20250907, 0)]
    [TestCase(20250609, 20250908, 0)]
    [TestCase(20250609, 20250908, 10)]
    public void SinglePeriodWithCredits(
      int trsEffective, int trsMaturity, int minute)
    {
      NoArbitrage(TestFlags.WithCredit,
        trsEffective, trsMaturity, minute);
    }

    [TestCase(20160209, 20160301, 0)]
    [TestCase(20160209, 20160907, 0)]
    [TestCase(20160209, 20170907, 0)]
    [TestCase(20160209, 20180907, 0)]
    [TestCase(20160209, 20190907, 0)]
    [TestCase(20160209, 20200907, 0)]
    [TestCase(20160209, 20210907, 0)]
    [TestCase(20160209, 20220907, 0)]
    [TestCase(20160209, 20230907, 0)]
    [TestCase(20160209, 20240907, 0)]
    [TestCase(20160209, 20250907, 0)]
    [TestCase(20160209, 20250908, 0)]
    [TestCase(20160209, 20250908, 10)]
    [TestCase(20250609, 20250901, 0)]
    [TestCase(20250609, 20250908, 0)]
    [TestCase(20250609, 20250908, 10)]
    public void SinglePeriodWithRecovery(
      int trsEffective, int trsMaturity, int minute)
    {
      NoArbitrage(TestFlags.WithCredit,
        trsEffective, trsMaturity, minute, 0.4);
    }

    [TestCase(20160209, 20160907, 0)]
    [TestCase(20160209, 20170907, 0)]
    [TestCase(20160209, 20180907, 0)]
    [TestCase(20160209, 20190907, 0)]
    [TestCase(20160209, 20200907, 0)]
    [TestCase(20160209, 20210907, 0)]
    [TestCase(20160209, 20220907, 0)]
    [TestCase(20160209, 20230907, 0)]
    [TestCase(20160209, 20240907, 0)]
    [TestCase(20160209, 20250907, 0)]
    [TestCase(20160209, 20250908, 0)]
    [TestCase(20160209, 20250908, 10)]
    [TestCase(20190609, 20190907, 0)]
    public void SinglePeriodAmortizing(
      int trsEffective, int trsMaturity, int minute)
    {
      NoArbitrage(TestFlags.WithAmortizing,
        trsEffective, trsMaturity, minute);
    }

    [TestCase(20160209, 20160301, 0)]
    [TestCase(20160209, 20160907, 0)]
    [TestCase(20160209, 20170907, 0)]
    [TestCase(20160209, 20180907, 0)]
    [TestCase(20160209, 20190907, 0)]
    [TestCase(20160209, 20200907, 0)]
    [TestCase(20160209, 20210907, 0)]
    [TestCase(20160209, 20220907, 0)]
    [TestCase(20160209, 20230907, 0)]
    [TestCase(20160209, 20240907, 0)]
    [TestCase(20160209, 20250907, 0)]
    [TestCase(20160209, 20250908, 0)]
    [TestCase(20160209, 20250908, 10)]
    [TestCase(20240815, 20240915, 0)]
    [TestCase(20250609, 20250907, 0)]
    [TestCase(20250609, 20250908, 0)]
    [TestCase(20250609, 20250908, 10)]
    public void SinglePeriodAmortizingWithCredits(
      int trsEffective, int trsMaturity, int minute)
    {
      NoArbitrage(TestFlags.WithCredit | TestFlags.WithAmortizing,
        trsEffective, trsMaturity, minute);
    }

    [TestCase(20160209, 20160301, 0)]
    [TestCase(20160209, 20160907, 0)]
    [TestCase(20160209, 20170907, 0)]
    [TestCase(20160209, 20180907, 0)]
    [TestCase(20160209, 20190907, 0)]
    [TestCase(20160209, 20200907, 0)]
    [TestCase(20160209, 20210907, 0)]
    [TestCase(20160209, 20220907, 0)]
    [TestCase(20160209, 20230907, 0)]
    [TestCase(20160209, 20240907, 0)]
    [TestCase(20160209, 20250907, 0)]
    [TestCase(20160209, 20250908, 0)]
    [TestCase(20160209, 20250908, 10)]
    [TestCase(20250609, 20250901, 0)]
    [TestCase(20250609, 20250908, 0)]
    [TestCase(20250609, 20250908, 10)]
    public void SinglePeriodAmortizingWithRecovery(
      int trsEffective, int trsMaturity, int minute)
    {
      NoArbitrage(TestFlags.WithCredit | TestFlags.WithAmortizing,
        trsEffective, trsMaturity, minute, 0.4);
    }

    private void NoArbitrage(TestFlags flags,
      int trsEffective, int trsMaturity, int minute,
      double recoveryRate = 0)
    {
      var amortizing = (flags & TestFlags.WithAmortizing) != 0;
      var bond = GetUnderlyingBond(amortizing);

      Dt effective = new Dt(trsEffective),
        maturity = Dt.Roll(Date(trsMaturity, minute),
          bond.BDConvention, bond.Calendar);

      // Calculate the arbitrage free initial bond price
      Dt asOf = effective;
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var survivalCurve = (flags & TestFlags.WithCredit) != 0
        ? new SurvivalCurve(asOf, 0.5) : null;
      if (survivalCurve != null && recoveryRate > 0)
      {
        survivalCurve.Calibrator = new SurvivalFitCalibrator(asOf)
        {
          RecoveryCurve = new RecoveryCurve(asOf, recoveryRate)
        };
      }
      var initialPrice = CalculateBondPrice(asOf,
        bond, discountCurve, survivalCurve);
      var initialNotional = CalculateInitialNotional(bond, asOf);

      // This is the expected pv of the Bond Return Leg.
      var expect = initialPrice*CalculateExpectedReturn(bond,
        discountCurve, survivalCurve, asOf, maturity,
        bond.GetPaymentSchedule(survivalCurve)
          .OfType<CreditContingentPayment>().GetTimeGrids())
        /initialNotional;

      // The return leg
      var returnLeg = AssetReturnLeg.Create(bond, effective, maturity,
        bond.Ccy, double.NaN, bond.Calendar, bond.BDConvention);

      // Create a pricer for the Bond Return Leg and calculate Pv
      var pricer = new BondReturnLegPricer(returnLeg, asOf, asOf,
        discountCurve, null, survivalCurve, null)
      { Notional = initialPrice };

      // Check that initial bond notional is 1.0
      pricer.BondNotional.IsExpected(To.Match(1.0));

      // Calculate the product PV
      var pv = pricer.ProductPv();

      // Do we get the expected results?
      pv.IsExpected(To.Match(expect).Within(1E-15));
    }

    private static double CalculateExpectedReturn(
      Bond bond, DiscountCurve dc, SurvivalCurve sc,
      Dt begin, Dt end, IReadOnlyList<Dt> timeGrids)
    {
      double pv = 0.0, remainingNotional = bond.Notional;
      foreach (var payment in bond.GetPaymentSchedule(sc)
        .OfType<PrincipalExchange>())
      {
        Dt cutoff = payment.GetCutoffDate();
        if (cutoff < begin)
        {
          remainingNotional -= payment.Amount;
          continue;
        }
        if (cutoff >= end)
        {
          break;
        }
        var a = payment.Amount;
        pv += a*CalculateOnePeriodReturn(
          Dt.Min(payment.GetCreditRiskEndDate(), bond.Maturity),
          dc, sc, begin, payment.PayDt, timeGrids);
        remainingNotional -= a;
      }
      if (!remainingNotional.AlmostEquals(0.0))
      {
        pv += remainingNotional*CalculateOnePeriodReturn(
          bond.Maturity, dc, sc, begin, end, timeGrids);
      }
      return pv;
    }

    private static double CalculateOnePeriodReturn(
      Dt creditRiskEnd, DiscountCurve dc, SurvivalCurve sc,
      Dt begin, Dt end, IReadOnlyList<Dt> timeGrids)
    {
      var df = dc.DiscountFactor(begin, end);
      if (sc == null) return 1 - df;

      if (creditRiskEnd < end) end = creditRiskEnd;
      var sp = sc.Interpolate(begin, end);
      var loss = CreditContingentPayment.ProtectionPv(
        begin, end, timeGrids, dc, sc);
      return 1 - loss - sp*df;
    }

    private static double CalculateInitialNotional(
      Bond bond, Dt begin)
    {
      double pv = 0.0, remainingNotional = bond.Notional;
      foreach (var payment in bond.GetPaymentSchedule()
        .OfType<PrincipalExchange>())
      {
        Dt cutoff = payment.GetCutoffDate();
        if (cutoff >= begin) break;
        remainingNotional -= payment.Amount;
      }
      return remainingNotional;
    }

    [TestCase(20160209, 20160907, 0)]
    [TestCase(20160209, 20170907, 0)]
    [TestCase(20160209, 20180907, 0)]
    [TestCase(20160209, 20190907, 0)]
    [TestCase(20160209, 20200907, 0)]
    [TestCase(20160209, 20210907, 0)]
    [TestCase(20160209, 20220907, 0)]
    [TestCase(20160209, 20230907, 0)]
    [TestCase(20160209, 20240907, 0)]
    [TestCase(20160209, 20250907, 0)]
    [TestCase(20160209, 20250908, 0)]
    [TestCase(20160209, 20250908, 10)]
    public void WithHistoricalPrices(
      int trsEffective, int trsMaturity, int minute)
    {
      var bond = GetUnderlyingBond();

      Dt effective = new Dt(trsEffective),
        maturity = Dt.Roll(Date(trsMaturity, minute),
          bond.BDConvention, bond.Calendar);

      // Set pricing date 20 days after the effective
      Dt asOf = Dt.Roll(effective + 20, bond.BDConvention, bond.Calendar);

      // Find the initial pricer on the effective date
      RateResetState state;
      var initPrice = RateResetUtil.FindRate(effective, asOf, 
        BondFullPriceIndex.HistoricalObservations, false, out state);
      state.IsExpected(To.Match(RateResetState.ObservationFound));

      // Calculate the forward looking current prices
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var currPrice = CalculateBondPrice(asOf, bond, discountCurve);
      var endPrice = CalculateBondPrice(maturity, bond, discountCurve, null, true);
      var df = discountCurve.DiscountFactor(asOf, maturity);

      // This is the expected pv of the Bond Return Leg.
      var expect = (endPrice - initPrice)*df     // return amount
        + (currPrice - endPrice*df);   // distribution amount

      // Create the return leg pricer
      var returnLeg = AssetReturnLeg.Create(bond, effective, maturity,
        bond.Ccy, initPrice, bond.Calendar, bond.BDConvention);
      returnLeg.InitialPrice.IsExpected(To.Match(initPrice));

      // Create a pricer for the Bond Return Leg and calculate Pv
      var pricer = new BondReturnLegPricer(returnLeg, asOf, asOf,
        discountCurve, discountCurve, null, BondFullPriceIndex)
      { Notional = initPrice};

      // Check that bond notional is 1
      pricer.BondNotional.IsExpected(To.Match(1.0));

      // Calculate the product PV
      var pv = pricer.ProductPv();

      // Do we get the expected results?
      pv.IsExpected(To.Match(expect).Within(1E-15));
    }

    [Test]
    public void CleanToDirtyPrices()
    {
      // currently we only have data for UK Gilt
      if (_bondType != BondType.UKGilt) return;

      var bond = GetUnderlyingBond();

      Dt effective = new Dt(20160210),
        maturity = Dt.Roll(new Dt(20250907),
          bond.BDConvention, bond.Calendar);

      // Set pricing date 20 days after the effective
      Dt asOf = Dt.Roll(effective, bond.BDConvention, bond.Calendar);

      // Create the return leg pricer
      var returnLeg = AssetReturnLeg.Create(bond, effective, maturity,
        bond.Ccy, 1.06, bond.Calendar, bond.BDConvention);

      // Create a return leg pricer using the clean price observations
      var pricer = new BondReturnLegPricer(returnLeg, asOf, asOf,
        null, null, null, BondFlatPriceIndex);

      // Get the price calculator from it
      var calculator = pricer.GetPriceCalculator();

      // Now test if the price calculator returns the full prices
      var fullPrices = BondFullPriceIndex.HistoricalObservations;
      foreach (var price in fullPrices)
      {
        var expect = price.Value;
        var actual = calculator.GetPrice(price.Date).Value;
        actual.IsExpected(To.Match(expect).Within(1E-8));
      }

    }

    #endregion

    #region Unified Creation

    [Flags]
    public enum CreationFlag
    {
      NoAdditionalCurve = 0,
      WithProjectionDiscount = 1,
      WithSurvivalCurve = 2,
    }

    [TestCase(CreationFlag.NoAdditionalCurve)]
    [TestCase(CreationFlag.WithProjectionDiscount)]
    [TestCase(CreationFlag.WithSurvivalCurve)]
    [TestCase(CreationFlag.WithProjectionDiscount|CreationFlag.WithSurvivalCurve)]
    public void UnifiedCreation(CreationFlag flags)
    {
      var bond = GetUnderlyingBond();
      Dt effective = new Dt(20160209),
        maturity = Dt.Roll(new Dt(20160907),
          bond.BDConvention, bond.Calendar);
      var returnLeg = AssetReturnLeg.Make(bond, effective, maturity,
        bond.Ccy, double.NaN, bond.Calendar);
      returnLeg.IsExpected(To.Be.InstanceOf<AssetReturnLeg<Bond>>());
      returnLeg.UnderlyingAsset.IsExpected(To.Be.SameAs(bond));

      // Calculate the arbitrage free initial bond price
      Dt asOf = effective;
      var discountCurve = new DiscountCurve(asOf, 0.02);

      // create other curves
      var curves = null as IList<CalibratedCurve>;
      if ((flags & CreationFlag.WithProjectionDiscount) != 0)
      {
        curves = new List<CalibratedCurve>
        {
          new DiscountCurve(asOf, 0.01) {Name = "Project"}
        };
      }
      if ((flags & CreationFlag.WithSurvivalCurve) != 0)
      {
        curves = curves ?? new List<CalibratedCurve>();
        curves.Add(new SurvivalCurve(asOf, 0.01) { Name = "Survival" });
      }

      // Create pricer
      var pricer = returnLeg.CreatePricer(asOf, asOf, discountCurve,
        curves?.ToArray(), null);
      pricer.IsExpected(To.Be.InstanceOf<BondReturnLegPricer>());

      // Check the properties
      pricer.AsOf.IsExpected(To.Match(asOf));
      pricer.AssetReturnLeg.IsExpected(To.Be.SameAs(returnLeg));
      pricer.DiscountCurve.IsExpected(To.Be.SameAs(discountCurve));
      pricer.Notional.IsExpected(To.Match(1.0));

      if (curves == null)
        pricer.ReferenceCurves.IsExpected(To.Be.Empty);
      else
        pricer.ReferenceCurves.Length.IsExpected(To.Match(curves.Count));

      if ((flags & CreationFlag.WithProjectionDiscount) != 0)
      {
        ((BondReturnLegPricer)pricer).DiscountCurveForPriceProjection
          .Name.IsExpected(To.Match("Project"));
      }
      if ((flags & CreationFlag.WithSurvivalCurve) != 0)
      {
        var sc = ((BondReturnLegPricer)pricer).SurvivalCurve;
        sc.IsExpected(To.Be.Not.Null);
        sc.Name.IsExpected(To.Match("Survival"));
      }

      // Check price calculator
      var calculator = pricer.GetPriceCalculator() as CashflowPriceCalculator;
      calculator.IsExpected(To.Be.Not.Null);
      Debug.Assert(calculator != null); // to please ReSharper
      calculator.IndexName.IsExpected(To.Match(bond.Description));
      calculator.PricingDatePaymentsExcluded.IsExpected(To.Be.False);

      // Test another interface
      var apricer = pricer as IPricer<IAssetReturnLeg<Bond>>;
      apricer.IsExpected(To.Be.Not.Null);
      Debug.Assert(apricer != null); // to please ReSharper
      apricer.Product.IsExpected(To.Be.SameAs(returnLeg));
    }

    #endregion

    #region Pricer Reset

    [Test]
    public void PricerReset()
    {
      var bond = GetUnderlyingBond();
      Dt effective = new Dt(20160209),
        maturity = Dt.Roll(new Dt(20160907),
          bond.BDConvention, bond.Calendar);
      Dt asOf = effective;
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var discountForPrice = new DiscountCurve(asOf, 0.01);

      // Create the return leg pricer
      var returnLeg = AssetReturnLeg.Create(bond, effective, maturity,
        bond.Ccy, double.NaN, bond.Calendar, bond.BDConvention);
      var pricer = new BondReturnLegPricer(returnLeg, asOf, asOf,
        discountCurve, discountForPrice, null, BondFullPriceIndex);

      // Check discount curves
      pricer.DiscountCurve.IsExpected(To.Be.SameAs(discountCurve));
      pricer.ReferenceCurves.Length.IsExpected(To.Match(1));
      pricer.ReferenceCurves[0].IsExpected(To.Be.SameAs(discountForPrice));

      // Check bond price calculator have the correct discount curve
      var calculator1 = pricer.GetPriceCalculator() as CashflowPriceCalculator;
      calculator1.IsExpected(To.Be.Not.Null);
      Debug.Assert(calculator1 != null); // to please Resharper
      calculator1.DiscountCurve.IsExpected(To.Be.SameAs(discountForPrice));

      // Repeated getting price calculator returns the same instance
      var calculator2 = pricer.GetPriceCalculator();
      calculator2.IsExpected(To.Be.SameAs(calculator1));

      // After reset they are not the same instance
      pricer.Reset();
      calculator2 = pricer.GetPriceCalculator();
      calculator2.IsExpected(To.Be.Not.SameAs(calculator1));

      // But they should match structurally
      var diff = ObjectStatesChecker.Compare(calculator1, calculator2);
      diff.IsExpected(To.Be.Null);
    }

    #endregion

    #region Handle Missing Historical Prices

    [Test]
    public void MissingHistoricalPrice()
    {
      var bond = GetUnderlyingBond();

      Dt effective = new Dt(20160120),
        maturity = Dt.Roll(new Dt(20180907),
          bond.BDConvention, bond.Calendar);

      var returnLeg = AssetReturnLeg.Create(bond, effective, maturity,
        bond.Ccy, double.NaN, bond.Calendar, bond.BDConvention);

      // Calculate the arbitrage free initial bond price
      Dt asOf = effective + 20;
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var pricer = new BondReturnLegPricer(returnLeg, asOf, asOf,
        discountCurve, null, null, null);

      Assert.That(() => pricer.ProductPv(), 
        Throws.InstanceOf<MissingFixingException>());
    }

    #endregion

    #region Defaulted Bond

    [TestCase(-1, -1)]
    [TestCase(-1, 0)]
    [TestCase(-1, 1)]
    [TestCase(-1, 60)]
    [TestCase(0, 0)]
    [TestCase(1, 0)]
    public void DefaultedBond(int defaultDateOffset, int defaultSettleOffset)
    {
      const double recovery = 0.4, initPrice = 1.0;

      var bond = GetUnderlyingBond();

      Dt effective = new Dt(20160120),
        maturity = Dt.Roll(new Dt(20180907),
          bond.BDConvention, bond.Calendar);

      var returnLeg = AssetReturnLeg.Create(bond, effective, maturity,
        bond.Ccy, initPrice, bond.Calendar, bond.BDConvention);

      // Calculate the arbitrage free initial bond price
      Dt asOf = Dt.AddDays(effective, 10, bond.Calendar),
        settle = Dt.AddDays(asOf, 2, bond.Calendar),
        defaultDate = asOf + defaultDateOffset,
        defaultSettle = settle + defaultSettleOffset;
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var discountForPrice = new DiscountCurve(asOf, 0.01);
      var survivalCurve = new SurvivalCurve(new SurvivalFitCalibrator(asOf)
      {
        RecoveryCurve = new RecoveryCurve(asOf, recovery)
        {
          JumpDate = defaultSettle
        }
      })
      {
        DefaultDate = defaultDate,
      };
      var pricer = new BondReturnLegPricer(returnLeg, asOf, settle,
        discountCurve, discountForPrice, survivalCurve, null);

      var expect = defaultSettleOffset < 0 ||
        (defaultSettleOffset == 0 && defaultDateOffset <= 0)
        ? 0.0
        : ((recovery - initPrice) * discountCurve.Interpolate(asOf, defaultSettle));

      var actual = pricer.Pv();
      actual.IsExpected(To.Match(expect));
    }

    #endregion

    #region Utilities

    private static double CalculateBondPrice(
      Dt date, Bond ibond,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve = null, 
      bool includeSettlePayment = false)
    {
      var pricer = new BondPricer(ibond, date, date,
        discountCurve, survivalCurve, 0, TimeUnit.None,
        GetRecoveryRate(survivalCurve, ibond.Maturity))
      {
        DiscountingAccrued = true
      };
      var pv0 = pricer.ProductPv();
      var ps = pricer.GetPaymentSchedule().AddRecoveryPayments(
        pricer.Bond, survivalCurve?.SurvivalCalibrator?.RecoveryCurve);
      if (ps == null) return 0;
      var pv1 = ps.GroupByCutoff().CalculatePv(
        date, date, discountCurve, survivalCurve,
        includeSettlePayment || pricer.IncludeSettlePayments,
        pricer.DiscountingAccrued);
      Assert.That(pv1, Is.EqualTo(pv0).Within(1E-15));
      return pv1/pricer.EffectiveNotional;
    }

    private static double GetRecoveryRate(SurvivalCurve sc, Dt date)
    {
      var rc = sc?.SurvivalCalibrator?.RecoveryCurve;
      return rc?.Interpolate(date) ?? 0.0;
    }

    private static Dt Date(int date, int minutes)
    {
      if (minutes > 0)
      {
        return new Dt(date % 100, date % 10000 / 100,
          date / 10000, minutes / 100, minutes % 100, 0);
      }
      return new Dt(date);
    }

    private Bond GetUnderlyingBond(bool amortizing = false)
    {
      Dt effective = new Dt(20150320), maturity = new Dt(20250907);
      BondType type = _bondType; // BondType.UKGilt;
      Currency ccy = Currency.GBP;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar calendar = Calendar.LNB;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.Following;
      double coupon = 0.02;
      var bond = new Bond(effective, maturity, ccy, type,
        coupon, dayCount, CycleRule.None, freq, roll, calendar)
      {
        Description = "2% Treasury Gilt 2025",
      };
      if (amortizing)
      {
        for (int i = 1; i < 10; ++i)
        {
          bond.AmortizationSchedule.Add(new Amortization(
            new Dt(1, 9, 2015 + i),
            AmortizationType.PercentOfInitialNotional, 0.05));
        }
      }
      return bond;
    }

    #endregion

    #region Data

    private IAssetPriceIndex BondFullPriceIndex => _fullPriceIndex;

    private IAssetPriceIndex BondFlatPriceIndex => _flatPriceIndex;

    private static Dt _D(string input)
    {
      return input.ParseDt();
    }


    private static IAssetPriceIndex _fullPriceIndex = new AssetPriceIndex(
      "2% Treasury Gilt 2025", QuotingConvention.FullPrice,
      Currency.GBP, Calendar.LNB, 1, BDConvention.Following, new RateResets
      {
        {_D("20-Mar-15"), 1.03366304},
        {_D("23-Mar-15"), 1.03451739},
        {_D("24-Mar-15"), 1.03487174},
        {_D("25-Mar-15"), 1.03802609},
        {_D("26-Mar-15"), 1.02908043},
        {_D("27-Mar-15"), 1.03304348},
        {_D("30-Mar-15"), 1.03019783},
        {_D("31-Mar-15"), 1.03045217},
        {_D("1-Apr-15"), 1.03300652},
        {_D("2-Apr-15"), 1.02887826},
        {_D("7-Apr-15"), 1.02833261},
        {_D("8-Apr-15"), 1.02958696},
        {_D("9-Apr-15"), 1.0293413},
        {_D("10-Apr-15"), 1.02820435},
        {_D("13-Apr-15"), 1.0276587},
        {_D("14-Apr-15"), 1.03641304},
        {_D("15-Apr-15"), 1.03196739},
        {_D("16-Apr-15"), 1.02782174},
        {_D("17-Apr-15"), 1.02978478},
        {_D("20-Apr-15"), 1.03163913},
        {_D("21-Apr-15"), 1.03129348},
        {_D("22-Apr-15"), 1.01914783},
        {_D("23-Apr-15"), 1.01980217},
        {_D("24-Apr-15"), 1.02436522},
        {_D("27-Apr-15"), 1.02011957},
        {_D("28-Apr-15"), 1.01997391},
        {_D("29-Apr-15"), 1.00692826},
        {_D("30-Apr-15"), 1.00598261},
        {_D("1-May-15"), 1.0065},
        {_D("5-May-15"), 0.99365435},
        {_D("6-May-15"), 0.9938087},
        {_D("7-May-15"), 0.99826304},
        {_D("8-May-15"), 1.00422609},
        {_D("11-May-15"), 0.99898043},
        {_D("12-May-15"), 0.99313478},
        {_D("13-May-15"), 0.99408913},
        {_D("14-May-15"), 0.99414348},
        {_D("15-May-15"), 1.00270652},
        {_D("18-May-15"), 0.99806087},
        {_D("19-May-15"), 0.99731522},
        {_D("20-May-15"), 0.99586957},
        {_D("21-May-15"), 0.99632391},
        {_D("22-May-15"), 1.0010413},
        {_D("26-May-15"), 1.00559565},
        {_D("27-May-15"), 1.00445},
        {_D("28-May-15"), 1.00940435},
        {_D("29-May-15"), 1.01266739},
        {_D("1-Jun-15"), 1.00842174},
        {_D("2-Jun-15"), 0.99887609},
        {_D("3-Jun-15"), 0.98793043},
        {_D("4-Jun-15"), 0.99338478},
        {_D("5-Jun-15"), 0.98754783},
        {_D("8-Jun-15"), 0.99070217},
        {_D("9-Jun-15"), 0.98545652},
        {_D("10-Jun-15"), 0.98151087},
        {_D("11-Jun-15"), 0.98836522},
        {_D("12-Jun-15"), 0.99762826},
        {_D("15-Jun-15"), 0.99258261},
        {_D("16-Jun-15"), 0.99463696},
        {_D("17-Jun-15"), 0.9892913},
        {_D("18-Jun-15"), 0.99144565},
        {_D("19-Jun-15"), 0.9946087},
        {_D("22-Jun-15"), 0.98586304},
        {_D("23-Jun-15"), 0.98461739},
        {_D("24-Jun-15"), 0.98257174},
        {_D("25-Jun-15"), 0.98122609},
        {_D("26-Jun-15"), 0.97838913},
        {_D("29-Jun-15"), 0.98874348},
        {_D("30-Jun-15"), 0.99199783},
        {_D("1-Jul-15"), 0.98655217},
        {_D("2-Jul-15"), 0.98800652},
        {_D("3-Jul-15"), 0.99526957},
        {_D("6-Jul-15"), 0.99372391},
        {_D("7-Jul-15"), 1.01177826},
        {_D("8-Jul-15"), 1.00403261},
        {_D("9-Jul-15"), 0.99868696},
        {_D("10-Jul-15"), 0.98795},
        {_D("13-Jul-15"), 0.98470435},
        {_D("14-Jul-15"), 0.9835587},
        {_D("15-Jul-15"), 0.98311304},
        {_D("16-Jul-15"), 0.98836739},
        {_D("17-Jul-15"), 0.98843043},
        {_D("20-Jul-15"), 0.98938478},
        {_D("21-Jul-15"), 0.98853913},
        {_D("22-Jul-15"), 0.99349348},
        {_D("23-Jul-15"), 0.99444783},
        {_D("24-Jul-15"), 1.00211087},
        {_D("27-Jul-15"), 1.00336522},
        {_D("28-Jul-15"), 1.00141957},
        {_D("29-Jul-15"), 0.99887391},
        {_D("30-Jul-15"), 0.99892826},
        {_D("31-Jul-15"), 1.0070913},
        {_D("3-Aug-15"), 1.00744565},
        {_D("4-Aug-15"), 1.0083},
        {_D("5-Aug-15"), 0.99945435},
        {_D("6-Aug-15"), 1.0036087},
        {_D("7-Aug-15"), 1.00997174},
        {_D("10-Aug-15"), 1.00382609},
        {_D("11-Aug-15"), 1.01328043},
        {_D("12-Aug-15"), 1.01723478},
        {_D("13-Aug-15"), 1.01238913},
        {_D("14-Aug-15"), 1.00915217},
        {_D("17-Aug-15"), 1.01360652},
        {_D("18-Aug-15"), 1.00906087},
        {_D("19-Aug-15"), 1.01101522},
        {_D("20-Aug-15"), 1.01886957},
        {_D("21-Aug-15"), 1.02433261},
        {_D("24-Aug-15"), 1.02738696},
        {_D("25-Aug-15"), 1.0176413},
        {_D("26-Aug-15"), 1.00440217},
        {_D("27-Aug-15"), 1.00205652},
        {_D("28-Aug-15"), 1.00427391},
        {_D("1-Sep-15"), 1.00572826},
        {_D("2-Sep-15"), 1.00538261},
        {_D("3-Sep-15"), 1.00813696},
        {_D("4-Sep-15"), 1.0158},
        {_D("7-Sep-15"), 1.01775495},
        {_D("8-Sep-15"), 1.01450989},
        {_D("9-Sep-15"), 1.01186484},
        {_D("10-Sep-15"), 1.01291978},
        {_D("11-Sep-15"), 1.01678462},
        {_D("14-Sep-15"), 1.01413956},
        {_D("15-Sep-15"), 1.00869451},
        {_D("16-Sep-15"), 1.00554945},
        {_D("17-Sep-15"), 1.0048044},
        {_D("18-Sep-15"), 1.01526923},
        {_D("21-Sep-15"), 1.01102418},
        {_D("22-Sep-15"), 1.02127912},
        {_D("23-Sep-15"), 1.01873407},
        {_D("24-Sep-15"), 1.02478901},
        {_D("25-Sep-15"), 1.01515385},
        {_D("28-Sep-15"), 1.02100879},
        {_D("29-Sep-15"), 1.02336374},
        {_D("30-Sep-15"), 1.02221868},
        {_D("1-Oct-15"), 1.02397363},
        {_D("2-Oct-15"), 1.02873846},
        {_D("5-Oct-15"), 1.02089341},
        {_D("6-Oct-15"), 1.01974835},
        {_D("7-Oct-15"), 1.0171033},
        {_D("8-Oct-15"), 1.01855824},
        {_D("9-Oct-15"), 1.01462308},
        {_D("12-Oct-15"), 1.01787802},
        {_D("13-Oct-15"), 1.01663297},
        {_D("14-Oct-15"), 1.02368791},
        {_D("15-Oct-15"), 1.02234286},
        {_D("16-Oct-15"), 1.02080769},
        {_D("19-Oct-15"), 1.01806264},
        {_D("20-Oct-15"), 1.01511758},
        {_D("21-Oct-15"), 1.02067253},
        {_D("22-Oct-15"), 1.02062747},
        {_D("23-Oct-15"), 1.01519231},
        {_D("26-Oct-15"), 1.01824725},
        {_D("27-Oct-15"), 1.0246022},
        {_D("28-Oct-15"), 1.02105714},
        {_D("29-Oct-15"), 1.00971209},
        {_D("30-Oct-15"), 1.01037692},
        {_D("2-Nov-15"), 1.00773187},
        {_D("3-Nov-15"), 1.00618681},
        {_D("4-Nov-15"), 1.00364176},
        {_D("5-Nov-15"), 1.0061967},
        {_D("6-Nov-15"), 1.00006154},
        {_D("9-Nov-15"), 0.99821648},
        {_D("10-Nov-15"), 1.00107143},
        {_D("11-Nov-15"), 1.00002637},
        {_D("12-Nov-15"), 1.00258132},
        {_D("13-Nov-15"), 1.00574615},
        {_D("16-Nov-15"), 1.0091011},
        {_D("17-Nov-15"), 1.00565604},
        {_D("18-Nov-15"), 1.01001099},
        {_D("19-Nov-15"), 1.01396593},
        {_D("20-Nov-15"), 1.01593077},
        {_D("23-Nov-15"), 1.01488571},
        {_D("24-Nov-15"), 1.01704066},
        {_D("25-Nov-15"), 1.0148956},
        {_D("26-Nov-15"), 1.01955055},
        {_D("27-Nov-15"), 1.02141538},
        {_D("30-Nov-15"), 1.01987033},
        {_D("1-Dec-15"), 1.02572527},
        {_D("2-Dec-15"), 1.02738022},
        {_D("3-Dec-15"), 1.01603516},
        {_D("4-Dec-15"), 1.0109},
        {_D("7-Dec-15"), 1.02075495},
        {_D("8-Dec-15"), 1.02120989},
        {_D("9-Dec-15"), 1.01546484},
        {_D("10-Dec-15"), 1.01781978},
        {_D("11-Dec-15"), 1.02228462},
        {_D("14-Dec-15"), 1.02103956},
        {_D("15-Dec-15"), 1.00999451},
        {_D("16-Dec-15"), 1.01044945},
        {_D("17-Dec-15"), 1.0187044},
        {_D("18-Dec-15"), 1.01986923},
        {_D("21-Dec-15"), 1.02152418},
        {_D("22-Dec-15"), 1.01607912},
        {_D("23-Dec-15"), 1.01103407},
        {_D("24-Dec-15"), 1.01330879},
        {_D("29-Dec-15"), 1.01426374},
        {_D("30-Dec-15"), 1.00711868},
        {_D("31-Dec-15"), 1.01033846},
        {_D("4-Jan-16"), 1.01689341},
        {_D("5-Jan-16"), 1.01724835},
        {_D("6-Jan-16"), 1.0248033},
        {_D("7-Jan-16"), 1.02375824},
        {_D("8-Jan-16"), 1.02742308},
        {_D("11-Jan-16"), 1.02747802},
        {_D("12-Jan-16"), 1.02873297},
        {_D("13-Jan-16"), 1.02958791},
        {_D("14-Jan-16"), 1.03084286},
        {_D("15-Jan-16"), 1.03800769},
        {_D("18-Jan-16"), 1.03476264},
        {_D("19-Jan-16"), 1.03331758},
        {_D("20-Jan-16"), 1.04127253},
        {_D("21-Jan-16"), 1.03662747},
        {_D("22-Jan-16"), 1.03279231},
        {_D("25-Jan-16"), 1.03554725},
        {_D("26-Jan-16"), 1.0349022},
        {_D("27-Jan-16"), 1.03455714},
        {_D("28-Jan-16"), 1.03721209},
        {_D("29-Jan-16"), 1.04617692},
        {_D("1-Feb-16"), 1.04103187},
        {_D("2-Feb-16"), 1.04808681},
        {_D("3-Feb-16"), 1.05164176},
        {_D("4-Feb-16"), 1.0460967},
        {_D("5-Feb-16"), 1.04606154},
        {_D("8-Feb-16"), 1.06151648},
        {_D("9-Feb-16"), 1.06097143},
      });

    private static IAssetPriceIndex _flatPriceIndex = new AssetPriceIndex(
      "clean prices", QuotingConvention.FlatPrice, Currency.GBP,
      Calendar.LNB, 1, BDConvention.Following, new RateResets
      {
        {_D("20-Mar-15"), 103.35/100},
        {_D("23-Mar-15"), 103.43/100},
        {_D("24-Mar-15"), 103.46/100},
        {_D("25-Mar-15"), 103.77/100},
        {_D("26-Mar-15"), 102.87/100},
        {_D("27-Mar-15"), 103.25/100},
        {_D("30-Mar-15"), 102.96/100},
        {_D("31-Mar-15"), 102.98/100},
        {_D("1-Apr-15"), 103.23/100},
        {_D("2-Apr-15"), 102.79/100},
        {_D("7-Apr-15"), 102.73/100},
        {_D("8-Apr-15"), 102.85/100},
        {_D("9-Apr-15"), 102.82/100},
        {_D("10-Apr-15"), 102.69/100},
        {_D("13-Apr-15"), 102.63/100},
        {_D("14-Apr-15"), 103.5/100},
        {_D("15-Apr-15"), 103.05/100},
        {_D("16-Apr-15"), 102.63/100},
        {_D("17-Apr-15"), 102.81/100},
        {_D("20-Apr-15"), 102.99/100},
        {_D("21-Apr-15"), 102.95/100},
        {_D("22-Apr-15"), 101.73/100},
        {_D("23-Apr-15"), 101.79/100},
        {_D("24-Apr-15"), 102.23/100},
        {_D("27-Apr-15"), 101.8/100},
        {_D("28-Apr-15"), 101.78/100},
        {_D("29-Apr-15"), 100.47/100},
        {_D("30-Apr-15"), 100.37/100},
        {_D("1-May-15"), 100.4/100},
        {_D("5-May-15"), 99.11/100},
        {_D("6-May-15"), 99.12/100},
        {_D("7-May-15"), 99.56/100},
        {_D("8-May-15"), 100.14/100},
        {_D("11-May-15"), 99.61/100},
        {_D("12-May-15"), 99.02/100},
        {_D("13-May-15"), 99.11/100},
        {_D("14-May-15"), 99.11/100},
        {_D("15-May-15"), 99.95/100},
        {_D("18-May-15"), 99.48/100},
        {_D("19-May-15"), 99.4/100},
        {_D("20-May-15"), 99.25/100},
        {_D("21-May-15"), 99.29/100},
        {_D("22-May-15"), 99.74/100},
        {_D("26-May-15"), 100.19/100},
        {_D("27-May-15"), 100.07/100},
        {_D("28-May-15"), 100.56/100},
        {_D("29-May-15"), 100.87/100},
        {_D("1-Jun-15"), 100.44/100},
        {_D("2-Jun-15"), 99.48/100},
        {_D("3-Jun-15"), 98.38/100},
        {_D("4-Jun-15"), 98.92/100},
        {_D("5-Jun-15"), 98.32/100},
        {_D("8-Jun-15"), 98.63/100},
        {_D("9-Jun-15"), 98.1/100},
        {_D("10-Jun-15"), 97.7/100},
        {_D("11-Jun-15"), 98.38/100},
        {_D("12-Jun-15"), 99.29/100},
        {_D("15-Jun-15"), 98.78/100},
        {_D("16-Jun-15"), 98.98/100},
        {_D("17-Jun-15"), 98.44/100},
        {_D("18-Jun-15"), 98.65/100},
        {_D("19-Jun-15"), 98.95/100},
        {_D("22-Jun-15"), 98.07/100},
        {_D("23-Jun-15"), 97.94/100},
        {_D("24-Jun-15"), 97.73/100},
        {_D("25-Jun-15"), 97.59/100},
        {_D("26-Jun-15"), 97.29/100},
        {_D("29-Jun-15"), 98.32/100},
        {_D("30-Jun-15"), 98.64/100},
        {_D("1-Jul-15"), 98.09/100},
        {_D("2-Jul-15"), 98.23/100},
        {_D("3-Jul-15"), 98.94/100},
        {_D("6-Jul-15"), 98.78/100},
        {_D("7-Jul-15"), 100.58/100},
        {_D("8-Jul-15"), 99.8/100},
        {_D("9-Jul-15"), 99.26/100},
        {_D("10-Jul-15"), 98.17/100},
        {_D("13-Jul-15"), 97.84/100},
        {_D("14-Jul-15"), 97.72/100},
        {_D("15-Jul-15"), 97.67/100},
        {_D("16-Jul-15"), 98.19/100},
        {_D("17-Jul-15"), 98.18/100},
        {_D("20-Jul-15"), 98.27/100},
        {_D("21-Jul-15"), 98.18/100},
        {_D("22-Jul-15"), 98.67/100},
        {_D("23-Jul-15"), 98.76/100},
        {_D("24-Jul-15"), 99.51/100},
        {_D("27-Jul-15"), 99.63/100},
        {_D("28-Jul-15"), 99.43/100},
        {_D("29-Jul-15"), 99.17/100},
        {_D("30-Jul-15"), 99.17/100},
        {_D("31-Jul-15"), 99.97/100},
        {_D("3-Aug-15"), 100/100},
        {_D("4-Aug-15"), 100.08/100},
        {_D("5-Aug-15"), 99.19/100},
        {_D("6-Aug-15"), 99.6/100},
        {_D("7-Aug-15"), 100.22/100},
        {_D("10-Aug-15"), 99.6/100},
        {_D("11-Aug-15"), 100.54/100},
        {_D("12-Aug-15"), 100.93/100},
        {_D("13-Aug-15"), 100.44/100},
        {_D("14-Aug-15"), 100.1/100},
        {_D("17-Aug-15"), 100.54/100},
        {_D("18-Aug-15"), 100.08/100},
        {_D("19-Aug-15"), 100.27/100},
        {_D("20-Aug-15"), 101.05/100},
        {_D("21-Aug-15"), 101.58/100},
        {_D("24-Aug-15"), 101.88/100},
        {_D("25-Aug-15"), 100.9/100},
        {_D("26-Aug-15"), 100.5/100},
        {_D("27-Aug-15"), 100.26/100},
        {_D("28-Aug-15"), 100.46/100},
        {_D("1-Sep-15"), 100.6/100},
        {_D("2-Sep-15"), 100.56/100},
        {_D("3-Sep-15"), 100.83/100},
        {_D("4-Sep-15"), 101.58/100},
        {_D("7-Sep-15"), 101.77/100},
        {_D("8-Sep-15"), 101.44/100},
        {_D("9-Sep-15"), 101.17/100},
        {_D("10-Sep-15"), 101.27/100},
        {_D("11-Sep-15"), 101.64/100},
        {_D("14-Sep-15"), 101.37/100},
        {_D("15-Sep-15"), 100.82/100},
        {_D("16-Sep-15"), 100.5/100},
        {_D("17-Sep-15"), 100.42/100},
        {_D("18-Sep-15"), 101.45/100},
        {_D("21-Sep-15"), 101.02/100},
        {_D("22-Sep-15"), 102.04/100},
        {_D("23-Sep-15"), 101.78/100},
        {_D("24-Sep-15"), 102.38/100},
        {_D("25-Sep-15"), 101.4/100},
        {_D("28-Sep-15"), 101.98/100},
        {_D("29-Sep-15"), 102.21/100},
        {_D("30-Sep-15"), 102.09/100},
        {_D("1-Oct-15"), 102.26/100},
        {_D("2-Oct-15"), 102.72/100},
        {_D("5-Oct-15"), 101.93/100},
        {_D("6-Oct-15"), 101.81/100},
        {_D("7-Oct-15"), 101.54/100},
        {_D("8-Oct-15"), 101.68/100},
        {_D("9-Oct-15"), 101.27/100},
        {_D("12-Oct-15"), 101.59/100},
        {_D("13-Oct-15"), 101.46/100},
        {_D("14-Oct-15"), 102.16/100},
        {_D("15-Oct-15"), 102.02/100},
        {_D("16-Oct-15"), 101.85/100},
        {_D("19-Oct-15"), 101.57/100},
        {_D("20-Oct-15"), 101.27/100},
        {_D("21-Oct-15"), 101.82/100},
        {_D("22-Oct-15"), 101.81/100},
        {_D("23-Oct-15"), 101.25/100},
        {_D("26-Oct-15"), 101.55/100},
        {_D("27-Oct-15"), 102.18/100},
        {_D("28-Oct-15"), 101.82/100},
        {_D("29-Oct-15"), 100.68/100},
        {_D("30-Oct-15"), 100.73/100},
        {_D("2-Nov-15"), 100.46/100},
        {_D("3-Nov-15"), 100.3/100},
        {_D("4-Nov-15"), 100.04/100},
        {_D("5-Nov-15"), 100.29/100},
        {_D("6-Nov-15"), 99.66/100},
        {_D("9-Nov-15"), 99.47/100},
        {_D("10-Nov-15"), 99.75/100},
        {_D("11-Nov-15"), 99.64/100},
        {_D("12-Nov-15"), 99.89/100},
        {_D("13-Nov-15"), 100.19/100},
        {_D("16-Nov-15"), 100.52/100},
        {_D("17-Nov-15"), 100.17/100},
        {_D("18-Nov-15"), 100.6/100},
        {_D("19-Nov-15"), 100.99/100},
        {_D("20-Nov-15"), 101.17/100},
        {_D("23-Nov-15"), 101.06/100},
        {_D("24-Nov-15"), 101.27/100},
        {_D("25-Nov-15"), 101.05/100},
        {_D("26-Nov-15"), 101.51/100},
        {_D("27-Nov-15"), 101.68/100},
        {_D("30-Nov-15"), 101.52/100},
        {_D("1-Dec-15"), 102.1/100},
        {_D("2-Dec-15"), 102.26/100},
        {_D("3-Dec-15"), 101.12/100},
        {_D("4-Dec-15"), 100.59/100},
        {_D("7-Dec-15"), 101.57/100},
        {_D("8-Dec-15"), 101.61/100},
        {_D("9-Dec-15"), 101.03/100},
        {_D("10-Dec-15"), 101.26/100},
        {_D("11-Dec-15"), 101.69/100},
        {_D("14-Dec-15"), 101.56/100},
        {_D("15-Dec-15"), 100.45/100},
        {_D("16-Dec-15"), 100.49/100},
        {_D("17-Dec-15"), 101.31/100},
        {_D("18-Dec-15"), 101.41/100},
        {_D("21-Dec-15"), 101.57/100},
        {_D("22-Dec-15"), 101.02/100},
        {_D("23-Dec-15"), 100.51/100},
        {_D("24-Dec-15"), 100.71/100},
        {_D("29-Dec-15"), 100.8/100},
        {_D("30-Dec-15"), 100.08/100},
        {_D("31-Dec-15"), 100.38/100},
        {_D("4-Jan-16"), 101.03/100},
        {_D("5-Jan-16"), 101.06/100},
        {_D("6-Jan-16"), 101.81/100},
        {_D("7-Jan-16"), 101.7/100},
        {_D("8-Jan-16"), 102.05/100},
        {_D("11-Jan-16"), 102.05/100},
        {_D("12-Jan-16"), 102.17/100},
        {_D("13-Jan-16"), 102.25/100},
        {_D("14-Jan-16"), 102.37/100},
        {_D("15-Jan-16"), 103.07/100},
        {_D("18-Jan-16"), 102.74/100},
        {_D("19-Jan-16"), 102.59/100},
        {_D("20-Jan-16"), 103.38/100},
        {_D("21-Jan-16"), 102.91/100},
        {_D("22-Jan-16"), 102.51/100},
        {_D("25-Jan-16"), 102.78/100},
        {_D("26-Jan-16"), 102.71/100},
        {_D("27-Jan-16"), 102.67/100},
        {_D("28-Jan-16"), 102.93/100},
        {_D("29-Jan-16"), 103.81/100},
        {_D("1-Feb-16"), 103.29/100},
        {_D("2-Feb-16"), 103.99/100},
        {_D("3-Feb-16"), 104.34/100},
        {_D("4-Feb-16"), 103.78/100},
        {_D("5-Feb-16"), 103.76/100},
        {_D("8-Feb-16"), 105.3/100},
        {_D("9-Feb-16"), 105.24/100},
      });
    #endregion
  }
}
