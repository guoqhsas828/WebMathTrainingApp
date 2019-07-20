// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public interface IHasPricingEnvironment
  {
    /// <summary>
    /// Get the pricing date defined for this application
    /// </summary>
    DateTime PricingDate { get; }

    /// <summary>
    /// Get the calculation environment defined for this application
    /// </summary>
    String CalcEnv { get; }
  }
}