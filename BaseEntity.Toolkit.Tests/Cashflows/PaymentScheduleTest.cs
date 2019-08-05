//
// Copyright (c)    2002-2015. All rights reserved.
//

using System.Collections;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  [TestFixture]
  public class PaymentScheduleTest : ToolkitTestBase
  {
    [Test]
    public void TestCreateAndAdd()
    {
      Currency ccy = Currency.None;
      PaymentSchedule ps = new PaymentSchedule();
      Payment p1 = new FixedInterestPayment(Dt.Empty, new Dt(17, 8, 2009), ccy, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, 0, 0.065, DayCount.None, Frequency.None);
      Payment p2 = new FixedInterestPayment(Dt.Empty, new Dt(18, 8, 2009), ccy, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, 0, 0.05, DayCount.None, Frequency.None);
      Payment p3 = new FixedInterestPayment(Dt.Empty, new Dt(18, 8, 2009), ccy, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, 0, 0.06, DayCount.None, Frequency.None);
      Payment p4 = new FixedInterestPayment(Dt.Empty, new Dt(20, 8, 2009), ccy, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, Dt.Empty, 0, 0.067, DayCount.None, Frequency.None);

      ps.AddPayment(p1);
      ps.AddPayment(p2);
      ps.AddPayment(p3);
      ps.AddPayment(p4);

      IEnumerator e = ps.GetEnumerator();
      Assert.IsNull(e.Current);
      Assert.IsTrue(e.MoveNext());
      Assert.AreEqual(p1, e.Current);
      Assert.IsTrue(e.MoveNext());
      Assert.AreEqual(p2, e.Current);
      Assert.IsTrue(e.MoveNext());
      Assert.AreEqual(p3, e.Current);
      Assert.IsTrue(e.MoveNext());
      Assert.AreEqual(p4, e.Current);
      Assert.IsFalse(e.MoveNext());

      int count = 0;
      foreach (Payment p in ps)
      {
        count++;
      }
      Assert.IsTrue(count == 4);

      Assert.IsTrue(4 == ps.GetPaymentsByType<InterestPayment>().Count());
      Assert.IsTrue(2 == ps.GetPaymentsOnDate(p2.PayDt).Count());
      Assert.IsTrue(2 == ps.GetPaymentsByType<InterestPayment>(p2.PayDt).Count());
      Assert.IsTrue(3 == ps.GetPaymentDates().Count());
    }
  }
}