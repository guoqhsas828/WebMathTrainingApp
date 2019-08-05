using System;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  internal class LeadTradeOwnershipResolver : IOwnershipResolver
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentType"></param>
    /// <returns></returns>
    public string GetCascade(Type parentType)
    {
      return "none";
    }

    /// <summary>
    /// Gets the concrete type of the owned entity.
    /// </summary>
    /// <param name="parentType">Type of the parent.</param>
    /// <returns></returns>
    public Type GetOwnedConcreteType(Type parentType)
    {
      return parentType;
    }
  }
}