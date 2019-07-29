/*
 * LoanNextCouponTreatment.cs
 *
 *   2010. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   How to project the next coupon on a Loan.
  /// </summary>
  public enum LoanNextCouponTreatment
  {
    /// <summary>
    /// Use the Pricing Date's IR rate fixing.
    /// </summary>
    CurrentFixing, 

    /// <summary>
    /// Use the interest periods given on the Loan.
    /// </summary>
    InterestPeriods, 

    /// <summary>
    /// Use no IR component (credit spread only).
    /// </summary>
    None, 

    /// <summary>
    /// Use the Pricing Date's IR stub rate.
    /// </summary>
    StubRate
  } // enum LoanNextCouponTreatment
}
