/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   RecoveryType represents the alternate recovery assumptions
  ///   supported by the Credit Toolkit models.
  /// </summary>
  public enum RecoveryType
  {
    /// <summary>Recovery is a percentage of face value</summary>
    Face = 0,

    /// <summary>Recovery is the pv of a percentage of face value</summary>
    FacePV,

    /// <summary>Recovery is drawn from a beta distribution around a percentage of face</summary>
    FaceBeta,

    /// <summary>Recovery is a percentage of market value</summary>
    Market,

    /// <summary>Recovery is the pv of a percentage of market value</summary>
    MarketPV,

    /// <summary>Recovery is drawn from a beta distribution around a percentage of market value</summary>
    MarketBeta,
  }
}
