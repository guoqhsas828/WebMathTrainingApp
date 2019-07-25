/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Types of copula distributions
  /// </summary>
  public enum CopulaType
  {
    /// <summary>Gaussian copula</summary>
    Gauss = 1,

    /// <summary>Student t copula</summary>
    StudentT = 2,

    /// <summary>Double t copula</summary>
    DoubleT = 3,

    /// <summary>Clayton copula</summary>
    Clayton = 4,

    /// <summary>Frank copula</summary>
    Frank = 5,

    /// <summary>Gumbel copula</summary>
    Gumbel = 6,

    /// <summary>Normal inverse Gaussian copula</summary>
    NormalInverseGaussian = 7,

    /// <summary>For internal use only<preliminary/></summary>
    ExternalGauss = 8,

    /// <summary>Random factor loading with Gaussian copula</summary>
    RandomFactorLoading = 9,

    /// <summary>Poisson copula as in the Hull-White dynamic model of credit portfolio risk</summary>
    Poisson = 10,

    /// <summary>Gauss copula extended to the range beyond 100&amp;. For internal use only<preliminary/></summary>
    ExtendedGauss = 11,

    /// <summary>Gauss copula conditional on survival event. For internal use only<preliminary/></summary>
    ConditionalGauss = 12

    /////<summary>Random factor loading of Andersen-Sidenius</summary>
    //RandomFactorLoadingAS = 12,
    /////<summary>Levy Gamma copula</summary>
    //ShiftedGamma  = 13
  }
}
