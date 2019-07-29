using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Curves.Bump
{
  /// <summary>
  ///  Interface representing a collection of curve tenors to bump and
  ///  a collection of curves affecting the pricers in a sensitivity
  ///  calculation.
  /// </summary>
  /// <remarks></remarks>
  public interface ISensitivitySelection
  {
    /// <summary>
    /// Gets the name of this selection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the selected tenors in the selection.
    /// </summary>
    /// <remarks></remarks>
    IList<CurveTenor> Tenors { get; }

    /// <summary>
    /// Gets all curves affecting the pricers in this sensitivity calculation.
    /// </summary>
    /// <remarks></remarks>
    IEnumerable<CalibratedCurve> AllCurves { get; }
  }

  /// <summary>
  ///  Interface representing a collection of curve tenors which are bumping together
  ///  as well as the curves associated with them.
  /// </summary>
  /// <remarks>
  ///  Normally the tenor selection is resulted from applying a curve tenor selector
  ///   to a set of curves.  A single selector may yield different selections when it
  ///  applies to different sets of curves.
  /// </remarks>
  public interface ICurveTenorSelection : ISensitivitySelection
  {
    /// <summary>
    /// Gets the selected curves in the selection.
    /// </summary>
    /// <remarks>This property can return an empty or null list if the selector
    ///  is not associated with any set of curves, for example, in the case
    ///  of uniform selector.</remarks>
    IList<CalibratedCurve> Curves { get; }

    /// <summary>
    /// Gets the handler for bumping curves according to a particular
    /// set of bump sizes and bump flags.
    /// </summary>
    /// <param name="flags">The bump flags.</param>
    /// <param name="bumpSizes">The bump sizes.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    ICurveBumpHandler GetBumpHandler(BumpFlags flags, params double[] bumpSizes);
  }

  /// <summary>
  ///  Handler interface for curve bumping through cached shift values.
  /// </summary>
  /// <remarks></remarks>
  public interface ICurveBumpHandler
  {
    /// <summary>
    /// Determines whether the specified curve shift collection has been affected.
    /// </summary>
    /// <param name="shifts">The curve collection.</param>
    /// <returns><c>true</c> if the specified curve has affected; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    bool HasAffected(CurveShifts shifts);

    /// <summary>
    /// Gets the curve shifts corresponding to this selection.
    /// </summary>
    /// <param name="shifts">The curve shifts.</param>
    /// <returns>A array of shifted values by curve points</returns>
    /// <remarks></remarks>
    double[] GetShiftValues(CurveShifts shifts);

    /// <summary>
    /// Sets the curves shifts corresponding to this selection.
    /// </summary>
    /// <param name="shifts">The curve shifts.</param>
    /// <param name="values">The values to set.</param>
    /// <remarks></remarks>
    void SetShiftValues(CurveShifts shifts, double[] values);
  }
}
