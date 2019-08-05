using System;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public class TradeSizeCalculatorAttribute : Attribute
  {
    /// <summary>
    /// 
    /// </summary>
    public Type Calculator { get; set; }
  }
}