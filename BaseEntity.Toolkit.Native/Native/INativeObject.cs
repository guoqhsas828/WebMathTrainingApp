using System.Runtime.InteropServices;

namespace BaseEntity.Toolkit.Native
{
  /// <summary>
  ///   Native object interface.
  /// </summary>
  public interface INativeObject
  {
    /// <summary>
    /// Gets the unmanaged pointer.
    /// </summary>
    /// <value>The unmanaged pointer.</value>
    HandleRef HandleRef { get; }
  }
}
