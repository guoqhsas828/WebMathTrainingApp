using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Cashflows.Payments
{
  /// <summary>
  /// Annotations inserted in the payment stream for information purpose.
  /// </summary>
  public class PaymentAnnotation : Payment
  {
    /// <summary>
    /// Dummy implementation, always return 0
    /// </summary>
    /// <returns>System.Double.</returns>
    protected sealed override double ComputeAmount()
    {
      return 0;
    }
  }
}
