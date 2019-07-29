using System;

using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Experiment class for accrual convention
  /// </summary>
  /// <exclude />
  [Serializable]
  public class BulletConvention : BaseEntityObject
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="rate"></param>
    public BulletConvention(double rate)
    {
      CouponRate = rate;
    }

    /// <summary>
    ///   Coupon rate
    /// </summary>
    public double CouponRate { get; }
  }
}