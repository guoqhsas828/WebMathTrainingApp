//
// Copyright (c)    2002-2015. All rights reserved.
//

using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  /// <summary>
  /// Test for generation of payment schedule
  /// </summary>
  [TestFixture]
  public class PaymentScheduleUtilsTest : ToolkitTestBase
  {
    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void FloatingPaymentScheduleWithDefaultDateNoAccruedPaid()
    {

      Dt asof = new Dt(10, 1, 2011);
      InterestRateIndex ri = new InterestRateIndex("Libor", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
      var sl1 = new SwapLeg(asof, Dt.Add(asof, "5 Y"), Frequency.SemiAnnual, 10e-4, ri)
                  {
                    CompoundingFrequency = Frequency.Quarterly,
                    CompoundingConvention = CompoundingConvention.FlatISDA
                  };
      var dc = new DiscountCurve(asof, 0.04);
      ForwardRateCalculator calculator = new ForwardRateCalculator(asof, ri, dc);
      ProjectionParams parameters = new ProjectionParams()
                                      {
                                        ProjectionType = sl1.ProjectionType,
                                        CompoundingFrequency = sl1.CompoundingFrequency,
                                        CompoundingConvention = sl1.CompoundingConvention
                                      };
      Dt defaultDt = Dt.Add(asof, 3, TimeUnit.Months);
      Dt defaultSettleDt = Dt.Add(defaultDt, 10, TimeUnit.Days);
      PaymentSchedule ps = PaymentScheduleUtils.FloatingRatePaymentSchedule(asof, Dt.Empty, sl1.Ccy, calculator, null,
                                                                            null, sl1.Schedule,
                                                                            sl1.CashflowFlag, sl1.Coupon, null,
                                                                            sl1.Notional, null, false, sl1.DayCount,
                                                                            null, parameters, null, null, false,
                                                                            defaultDt, defaultSettleDt, null, null);
      Assert.AreEqual(0, ps.Count, "No accrued paid");

    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void FloatingPaymentScheduleWithDefaultDateAccruedPaid()
    {

      Dt asof = new Dt(10, 1, 2011);
      InterestRateIndex ri = new InterestRateIndex("Libor", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
      var sl1 = new SwapLeg(asof, Dt.Add(asof, "5 Y"), Frequency.SemiAnnual, 10e-4, ri)
                  {
                    CompoundingFrequency = Frequency.Quarterly,
                    CompoundingConvention = CompoundingConvention.FlatISDA
                  };
      sl1.CashflowFlag |= CashflowFlag.AccruedPaidOnDefault;
        var dc = new DiscountCurve(asof, 0.04);
      ForwardRateCalculator calculator = new ForwardRateCalculator(asof, ri, dc);
      ProjectionParams parameters = new ProjectionParams()
      {
        ProjectionType = sl1.ProjectionType,
        CompoundingFrequency = sl1.CompoundingFrequency,
        CompoundingConvention = sl1.CompoundingConvention
      };
      Dt defaultDt = Dt.Add(asof, 3, TimeUnit.Months);
      Dt defaultSettleDt = Dt.Add(defaultDt, 10, TimeUnit.Days);
      PaymentSchedule ps = PaymentScheduleUtils.FloatingRatePaymentSchedule(asof, Dt.Empty, sl1.Ccy, calculator, null,
                                                                                    null, sl1.Schedule,
                                                                                    sl1.CashflowFlag, sl1.Coupon, null,
                                                                                    sl1.Notional, null, false, sl1.DayCount,
                                                                                    null, parameters, null, null, false,
                                                                                    defaultDt, defaultSettleDt, null, null);
      List<InterestPayment> ips = (List<InterestPayment>) ps.GetPaymentsByType<InterestPayment>(defaultSettleDt);
      Assert.AreEqual(1, ips.Count, "Accrued paid at default settle date");
      Assert.AreEqual(ips[0].AccrualFactor,
                      Dt.Fraction(ips[0].CycleStartDate, defaultDt, ips[0].PeriodStartDate, defaultDt,
                                  ips[0].DayCount, Frequency.None ), 1e-10);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void FixedPaymentScheduleWithDefaultDateNoAccruedPaid()
    {

      Dt asof = new Dt(10, 1, 2011);
      var sl1 = new SwapLeg(asof, Dt.Add(asof, "5 Y"), Currency.USD, 0.04, DayCount.Thirty360, Frequency.SemiAnnual,
                            BDConvention.Modified,
                            Calendar.NYB, false);
      Dt defaultDt = Dt.Add(asof, 3, TimeUnit.Months);
      Dt defaultSettleDt = Dt.Add(defaultDt, 10, TimeUnit.Days);
      PaymentSchedule ps = PaymentScheduleUtils.FixedRatePaymentSchedule(asof, Dt.Empty, sl1.Ccy, sl1.Schedule,
                                                                            sl1.CashflowFlag, sl1.Coupon, null,
                                                                            sl1.Notional, null, false, sl1.DayCount,
                                                                            sl1.CompoundingFrequency, null, false,
                                                                            defaultDt, defaultSettleDt, null, null);
      Assert.AreEqual(0, ps.Count, "No accrued paid");

    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void FixedPaymentScheduleWithDefaultDateAccruedPaid()
    {

      Dt asof = new Dt(10, 1, 2011);
      var sl1 = new SwapLeg(asof, Dt.Add(asof, "5 Y"), Currency.USD, 0.04, DayCount.Thirty360, Frequency.SemiAnnual,
                            BDConvention.Modified,
                            Calendar.NYB, false);
      sl1.CashflowFlag |= CashflowFlag.AccruedPaidOnDefault;
      Dt defaultDt = Dt.Add(asof, 3, TimeUnit.Months);
      Dt defaultSettleDt = Dt.Add(defaultDt, 10, TimeUnit.Days);
      PaymentSchedule ps = PaymentScheduleUtils.FixedRatePaymentSchedule(asof, Dt.Empty, sl1.Ccy, sl1.Schedule,
                                                                            sl1.CashflowFlag, sl1.Coupon, null,
                                                                            sl1.Notional, null, false, sl1.DayCount,
                                                                            Frequency.None, null, false,
                                                                            defaultDt, defaultSettleDt, null, null);
      List<InterestPayment> ips = (List<InterestPayment>)ps.GetPaymentsByType<InterestPayment>(defaultSettleDt);
      Assert.AreEqual(1, ips.Count, "Accrued paid at default settle date");
      Assert.AreEqual(ips[0].AccrualFactor,
                      Dt.Fraction(ips[0].CycleStartDate, defaultDt, ips[0].PeriodStartDate, defaultDt,
                                  ips[0].DayCount, Frequency.None), 1e-10);
    }
  }
}
