using System;

namespace BaseEntity.Configuration
{
  /// <summary>
  ///   Attribute indicates that the class marked contains at least one object logger. This attribute is
  ///   used to speed up the loading of all object logger attribute as it minimises the number of fields which
  ///   need to be considered to only the classes with this attribute.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class ObjectLoggerEnabledAttribute : Attribute
  {
  }
}