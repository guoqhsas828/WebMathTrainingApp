using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Curves.Bump
{
  /// <summary>
  ///   Curve tenor selector interface.
  /// </summary>
  /// <remarks></remarks>
  internal interface ICurveTenorSelector
  {
    /// <summary>
    /// Determines whether the specified tenor has been selected.
    /// </summary>
    /// <param name="curve">The curve associated with the tenor.</param>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor has been selected; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    bool HasSelected(CalibratedCurve curve, CurveTenor tenor);

    /// <summary>
    /// Gets the name of this selector.
    /// </summary>
    /// <remarks></remarks>
    string Name { get; }

    /// <summary>
    /// Gets the unique key of this selector.
    /// </summary>
    /// <remarks>Implementation should make sure the key uniquely
    ///  identify the selector in the current application domain.
    ///  It is also required that the identical clones of the
    ///  selector have the same key as the original one.
    /// </remarks>
    string Key { get; }
  }
}
