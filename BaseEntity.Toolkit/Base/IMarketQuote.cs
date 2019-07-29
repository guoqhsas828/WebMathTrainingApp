/*
 * IMarketQuote.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Quote interface
  /// </summary>
  public interface IMarketQuote
  {
    /// <summary>
    ///   Quoted value
    /// </summary>
    double Value { get; }

    /// <summary>
    ///   Quote type
    /// </summary>
    QuotingConvention Type { get; }
  };
}
