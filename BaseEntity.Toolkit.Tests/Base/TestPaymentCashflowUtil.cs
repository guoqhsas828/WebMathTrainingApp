//
// Copyright (c)    2018. All rights reserved.
//

using System;
using NUnit.Framework;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Tests
{
  public static class TestPaymentCashflowUtil
  {
    internal static void AssertEqualCashflows(Cashflow expect, Cashflow actual)
    {
      int count = expect.Count;
      NUnit.Framework.Assert.AreEqual(count, actual.Count, "Count");
      AreEqual("StartDt", count, expect.GetStartDt, actual.GetStartDt);
      AreEqual("EndDt", count, expect.GetEndDt, actual.GetEndDt);
      AreEqual("PayDt", count, expect.GetDt, actual.GetDt);
      AreEqual("Fraction", count,
        expect.GetPeriodFraction, actual.GetPeriodFraction, 1E-16);
      AreEqual("Principal", count,
        expect.GetPrincipalAt, actual.GetPrincipalAt, 1E-16);
      AreEqual("Amount", count, expect.GetAmount, actual.GetAmount, 1E-16);
      AreEqual("Default Amount", count,
        expect.GetDefaultAmount, actual.GetDefaultAmount, 1E-16);
      AreEqual("Spread", count, expect.GetSpread, actual.GetSpread, 1E-16);
      AreEqual("Coupon", count, expect.GetCoupon, actual.GetCoupon, 1E-16);
      AreEqual("Accrued", count, expect.GetAccrued, actual.GetAccrued, 1E-16);

      if (expect.DefaultPayment == null)
      {
        NUnit.Framework.Assert.IsNull(actual.DefaultPayment, nameof(actual.DefaultPayment));
        return;
      }

      if (actual.DefaultPayment == null)
      {
        NUnit.Framework.Assert.Fail("Expects non-null default payment\nBut got null");
        return;
      }

      var ed = expect.DefaultPayment;
      var ad = actual.DefaultPayment;
      Assert.AreEqual(ed.Date, ad.Date, "DefaultPayment.Date");
      Assert.AreEqual(ed.Loss, ad.Loss, 1E-16, "DefaultPayment.Loss");
      Assert.AreEqual(ed.Amount, ad.Amount, 1E-16, "DefaultPayment.Amount");
      Assert.AreEqual(ed.Accrual, ad.Accrual, 1E-16, "DefaultPayment.Accrual");
    }

    private static void AreEqual<T>(
      string message, int n,
      Func<int, T> expect, Func<int, T> actual,
      double tolerance = Double.NaN)
    {
      T[] a = new T[n], e = new T[n];
      for (int i = 0; i < n; ++i)
      {
        e[i] = expect(i);
        a[i] = actual(i);
      }

      NUnit.Framework.Assert.That(a,
        double.IsNaN(tolerance)
          ? Is.EqualTo(e)
          : Is.EqualTo(e).Within(tolerance),
        message);
    }
  }
}
