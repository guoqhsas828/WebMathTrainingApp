using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Toolkit.Tests
{
  /// <summary>
  ///   Mark smoke tests
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
  public class SmokeAttribute : NUnit.Framework.CategoryAttribute
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    public SmokeAttribute() : base("Smoke")
    { }
  }
}
