using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Payments
{
  struct PaymentNode
  {
    public readonly Payment Payment;
    public readonly double Scale;
    public readonly Curve DiscountCurve;

    public Dt PayDt { get { return Payment.PayDt; } }

    public PaymentNode(Payment payment, Curve discountCurve, double scale = 1)
    {
      Payment = payment;
      Scale= scale;
      DiscountCurve = discountCurve;
    }
  }
}
