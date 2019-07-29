/*
 * TrancheValue.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///   Represent standardized base tranche values.
  /// </summary>
  /// <remarks>
  ///   The standardized tranche values recorded here are
  ///   protection pv, flat fee pv, and up-front fee, all
  ///   of which are calculated based on unit notional (1.0),
  ///   10000 bps premium, and 100% up-fron fee.
  /// </remarks>
  internal struct TrancheValue
  {
    public double ProtectionPv;
    public double FlatFeePv;
    public double UpfrontFee;

    public TrancheValue(
      double protectionPv,
      double flatFeePv,
      double upfrontFee)
    {
      ProtectionPv = protectionPv;
      FlatFeePv = flatFeePv;
      UpfrontFee = upfrontFee;
    }
  }
}
