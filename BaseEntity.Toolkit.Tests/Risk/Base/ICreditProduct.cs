using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Implemented by all credit products
  /// </summary>
  public interface ICreditProduct
  {
    /// <summary>
    ///   List of objects that owns one or more reference credits
    /// </summary>
    IEnumerable<IReferenceCreditsOwner> ReferenceCreditsOwners { get; }
  }
}
