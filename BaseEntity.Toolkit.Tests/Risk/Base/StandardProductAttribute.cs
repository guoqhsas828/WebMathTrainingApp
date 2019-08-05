using System;

namespace BaseEntity.Risk
{
  /// <summary>
  /// An attribute to mark Product-derived classes as standard (rather than OTC or bespoke) products.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class StandardProductAttribute : Attribute {}
}