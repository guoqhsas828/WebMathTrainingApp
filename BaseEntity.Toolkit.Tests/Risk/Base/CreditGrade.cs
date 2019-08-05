using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Credit Classification of a Legal Entity
  /// </summary>
  public enum CreditGrade
  {
    /// <summary></summary>
    None,
    /// <summary>Investment Grade</summary>
    IG,
    /// <summary>Crossover</summary>
    XO,
    /// <summary>High Yield</summary>
    HY,
    /// <summary></summary>
    Distressed

  } 
}