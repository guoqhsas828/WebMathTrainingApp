/*
 * RecoveryCorrelationType.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id$
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///  Enum type for recovery correlation
  /// </summary>
  public enum RecoveryCorrelationType
  {
    /// <summary>No recovery correlation</summary>
    None = 0,
    /// <summary>0-1 two point extreme distribution</summary>
    ZeroOne = 1,
    /// <summary>Conditional copula on recovery level</summary>
    Level = 2,
  }
}
