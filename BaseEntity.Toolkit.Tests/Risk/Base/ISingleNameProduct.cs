using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   This should be implemented by all single name credit products
  /// </summary>
  public interface ISingleNameProduct : ICreditProduct
  {
    /// <summary>
    /// 
    /// </summary>
    IReferenceCredit ReferenceCredit { get; }
  }
}
