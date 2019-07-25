/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Compounding or payment frequency.
  /// </summary>
  [BaseEntity.Shared.AlphabeticalOrderEnum]
  public enum Frequency
  {
    /// <summary>Continuously compounded</summary>
    Continuous = -1,

    /// <summary>Simple interest</summary>
    None = 0,

    /// <summary>Annual</summary>
    Annual = 1,

    /// <summary>Semi-Annual</summary>
    SemiAnnual = 2,

    /// <summary>Tri-Annual (every four months)</summary>
    TriAnnual = 3,

    /// <summary>Quarterly</summary>
    Quarterly = 4,

    /// <summary>Monthly</summary>
    Monthly = 12,

    /// <summary>28 Days</summary>
    TwentyEightDays = 13,

    /// <summary>Bi-Weekly (every two weeks)</summary>
    BiWeekly = 26,

    /// <summary>Weekly</summary>
    Weekly = 52,

    /// <summary>Daily</summary>
    Daily = 365,
  }
}