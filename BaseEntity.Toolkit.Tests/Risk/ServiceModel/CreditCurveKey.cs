using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk.Services.ServiceModel
{
  /// <summary>
  /// 
  /// </summary>
  public class CreditCurveKey
  {
    /// <summary>
    /// 
    /// </summary>
    public long ReferenceEntityId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public Currency Currency { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public Seniority Seniority { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public RestructuringType Restructuring { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public Cancellability Cancellability { get; set; }
  }
}