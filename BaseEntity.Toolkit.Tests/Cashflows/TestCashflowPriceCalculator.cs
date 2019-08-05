//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows
{

  [TestFixture]
  public class TestCashflowPriceCalculator
  {
    #region Constructor

    static CashflowPriceCalculator GetCalculator(
      Func<Dt, double> deflator = null)
    {
      var asOf = Dt.Today();
      var discountCurve = new DiscountCurve(asOf, 0.0);

      var ps = new PaymentSchedule();
      ps.AddPayment(new ScaledPayment(
        new BasicPayment(asOf + 180, 0.2, Currency.USD),
        0.5));
      ps.AddPayment(new ScaledPayment(
        new PrincipalExchange(asOf + 365, 2.0, Currency.USD),
        0.5));

      var pastPrices = new RateResets
      {
        {asOf - 100, 1.05}
      };

      return new CashflowPriceCalculator(
        asOf, ps, discountCurve, pastPrices, deflator);
    }

    #endregion

    #region Tests

    [Test]
    public void HistoricalPrice()
    {
      var calculator = PriceCalculator;
      var record = calculator.HistoricalObservations.First();
      var price = calculator.GetPrice(record.Date);
      Assert.That(price.State,Is.EqualTo(RateResetState.ObservationFound));
      Assert.That(price.Value,Is.EqualTo(record.Rate));

      // Test IRateProject interface methods
      var fs = calculator.GetFixingSchedule(Dt.Empty, Dt.Empty,
        record.Date, record.Date + 1) as ForwardPriceFixingSchedule;
      Assert.That(fs,Is.Not.Null);
      Assert.That(fs.ResetDate,Is.EqualTo(record.Date));
      Assert.That(fs.ReferenceDate,Is.EqualTo(record.Date + 1));

      price = calculator.Fixing(fs);
      Assert.That(price.State,Is.EqualTo(RateResetState.ObservationFound));
      Assert.That(price.Value,Is.EqualTo(record.Rate));
    }

    [Test]
    public void MissingPrice()
    {
      var calculator = PriceCalculator;
      var record = calculator.HistoricalObservations.First();
      Assert.That(() => calculator.GetPrice(record.Date - 10),
        Throws.InstanceOf<MissingFixingException>());
    }

    [Test]
    public void PriceAtTwoDaysAfterLastDate()
    {
      var calculator = PriceCalculator;
      var ps = calculator.UnderlyingPayments;
      var payment = ps.Last();
      var price = calculator.GetPrice(payment.PayDt + 2);
      Assert.That(price.State,Is.EqualTo(RateResetState.IsProjected));
      Assert.That(price.Value,Is.EqualTo(0.0));
    }

    [Test]
    public void PriceWithinOneDaysAfterLastDate()
    {
      var calculator = PriceCalculator;
      var ps = calculator.UnderlyingPayments;
      var payment = ps.Last();
      var price = calculator.GetPrice(payment.PayDt + 1);
      Assert.That(price.State,Is.EqualTo(RateResetState.IsProjected));
      Assert.That(price.Value,Is.EqualTo(0.0));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PriceReturnPayment(bool isAbsolute)
    {
      var calculator = PriceCalculator;
      var begin = calculator.HistoricalObservations.First();
      var ps = calculator.UnderlyingPayments;
      var end = ps.Last();
      var payment = new PriceReturnPayment(Dt.Empty, end.PayDt,
        Currency.USD, begin.Date, end.PayDt, calculator, double.NaN, isAbsolute);
      Assert.That(payment.BeginFixing.Value,Is.EqualTo(begin.Rate));
      Assert.That(payment.EndFixing.Value,Is.EqualTo(end.DomesticAmount));
      Assert.That(payment.IsProjected,Is.EqualTo(true));
      Assert.That(payment.Amount,Is.EqualTo(isAbsolute
        ? (payment.EndFixing.Value - payment.BeginFixing.Value)
        : (payment.EndFixing.Value/payment.BeginFixing.Value - 1)));
    }

    [Test]
    public void InitialPrice()
    {
      var calculator = PriceCalculator;
      var begin = calculator.HistoricalObservations.First().Date;
      var initialPrice = 120.0;
      var payment = new PriceReturnPayment(Dt.Empty, begin+100,
        Currency.USD, begin, begin+100, calculator, initialPrice);
      Assert.That(payment.BeginFixing.Value,Is.EqualTo(initialPrice));
      Assert.That(payment.BeginFixing.State,Is.EqualTo(RateResetState.ResetFound));
    }

    [Test]
    public void PriceOnLastDate()
    {
      var calculator = PriceCalculator;
      var ps = calculator.UnderlyingPayments;
      var payment = ps.Last();
      var price = calculator.GetPrice(payment.PayDt);
      Assert.That(price.State,Is.EqualTo(RateResetState.IsProjected));
      Assert.That(price.Value,Is.EqualTo(payment.DomesticAmount));
    }

    [Test]
    public void PriceOnLastDateWithDeflator()
    {
      var calculator = GetCalculator(d => 2.0);
      var ps = calculator.UnderlyingPayments;
      var payment = ps.Last();
      var price = calculator.GetPrice(payment.PayDt);
      Assert.That(price.State,Is.EqualTo(RateResetState.IsProjected));
      Assert.That(price.Value,Is.EqualTo(payment.DomesticAmount/2));
    }

    [Test]
    public void PriceBeforeFirstPayment()
    {
      var calculator = PriceCalculator;
      var dc = calculator.DiscountCurve;
      var ps = calculator.UnderlyingPayments;
      var payment = ps.First();
      var date = payment.PayDt - 1;
      var expect = ps.Aggregate(0.0, (v, p) => v +
        p.DomesticAmount*dc.DiscountFactor(date, p.PayDt));
      var price = calculator.GetPrice(date);
      Assert.That(price.State,Is.EqualTo(RateResetState.IsProjected));
      Assert.That(price.Value,Is.EqualTo(expect).Within(1E-15));
    }

    [Test]
    public void PriceOnFirstPaymentDate()
    {
      var calculator = PriceCalculator;
      var dc = calculator.DiscountCurve;
      var ps = calculator.UnderlyingPayments;
      var payment = ps.First();
      var date = payment.PayDt;
      var expect = ps.Aggregate(0.0, (v, p) => v +
        p.DomesticAmount*dc.DiscountFactor(date, p.PayDt));
      var price = calculator.GetPrice(date);
      Assert.That(price.State,Is.EqualTo(RateResetState.IsProjected));
      Assert.That(price.Value,Is.EqualTo(expect).Within(1E-15));
    }

    [Test]
    public void PriceOnAsOf()
    {
      var calculator = PriceCalculator;
      var asOf = calculator.AsOf;
      var dc = calculator.DiscountCurve;
      var ps = calculator.UnderlyingPayments;
      var expect = ps.Where(p => p.PayDt >= asOf).Aggregate(0.0,
        (v, p) => v + p.DomesticAmount*dc.DiscountFactor(asOf, p.PayDt));
      var price = calculator.GetPrice(asOf, asOf);
      Assert.That(price.State,Is.EqualTo(RateResetState.IsProjected));
      Assert.That(price.Value,Is.EqualTo(expect).Within(1E-15));
    }

    [Test]
    public void GetResetInfo()
    {
      // this is not implemented yet.
      Assert.That(() => PriceCalculator.GetResetInfo(null),
      Throws.InstanceOf<NotImplementedException>());
    }

    #endregion

    public TestCashflowPriceCalculator()
    {
      PriceCalculator = GetCalculator();
    }

    CashflowPriceCalculator PriceCalculator { get; set; }

  }
}
