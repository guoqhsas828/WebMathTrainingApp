// 
//  -2014. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// An instrument representing a defaulted asset such as defaulted bond, loan, note, bill, etc.
  /// </summary>
  [Serializable]
  [ReadOnly(true)]
  public class DefaultedAsset : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="assumedRecoverySettlement">Assumed recovery settlement date for the original product - used as the Maturity data of the defaulted product</param>
    /// <param name="ccy">Currency</param>
    /// <returns>Constructed DefaultedAsset</returns>
    public DefaultedAsset(Dt effective, Dt assumedRecoverySettlement, Currency ccy)
      : base(effective, assumedRecoverySettlement, ccy)
    {
      Notional = 1.0;
    }

    #endregion Constructors
  }
}