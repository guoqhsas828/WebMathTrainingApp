/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Treatment of negative survival probabilities in models.
  /// </summary>
  public enum NegSPTreatment
  {
    /// <summary>Allow positive rates only (error if negative)</summary>
    Positive = 0,

    /// <summary>Treat negative survival probabilities as zeros</summary>
    Zero,

    /// <summary>Allow negative survival probabilities</summary>
    Allow,

    /// <summary>Adjust the term structure of recovery to keep positive</summary>
    Adjust
  }
}
