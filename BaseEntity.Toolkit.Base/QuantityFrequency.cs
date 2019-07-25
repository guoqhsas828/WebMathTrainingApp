// 
// Copyright (c)    2002-2012. All rights reserved.
// 

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Method of specifying the notional quantity for commodities
  /// </summary>
  public enum QuantityFrequency
  {
    /// <summary>
    /// Not set
    /// </summary>
    None = 0,

    /// <summary>
    /// Quantity applies over the term
    /// </summary>
    Term,

    /// <summary>
    /// Quantity is per business day
    /// </summary>
    PerBusinessDay,

    /// <summary>
    /// Quantity applies to calculation period
    /// </summary>
    PerCalculationPeriod,

    /// <summary>
    /// Quantity applies to settlement period
    /// </summary>
    PerSettlementPeriod,

    /// <summary>
    /// Quantity is per calendar day
    /// </summary>
    PerCalendarDay,

    /// <summary>
    /// Quantity is per hour
    /// </summary>
    PerHour,

    /// <summary>
    /// Quantity is per months
    /// </summary>
    PerMonth
  }
}