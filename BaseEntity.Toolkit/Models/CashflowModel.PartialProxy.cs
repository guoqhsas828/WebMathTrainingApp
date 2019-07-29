//
// Partial proxy for Cashflow pricing model
//  -2008. All rights reserved.
//

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Models
{


  [Flags]
  public enum CashflowModelFlags
  {
    LogLinearApproximation = 1,
    IncludeFees = 0x10,
    IncludeProtection = 0x20,
    IncludeSettlePayments = 0x40,
    IncludeMaturityProtection = 0x80,
    FullFirstCoupon = 0x100,
    CreditRiskToPaymentDate = 0x200,
    AllowNegativeSpread = 0x400,
    IncludeAccruedOnSettlementDefault = 0x10000,
    IgnoreAccruedInProtection = 0x20000,
  }

}