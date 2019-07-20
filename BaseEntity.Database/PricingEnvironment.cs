using System;
using System.Runtime.Serialization;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  [DataContract]
  public class PricingEnvironment
  {
    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public string CalculationEnvironment { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public DateTime PricingDate { get; set; }
  }
}
