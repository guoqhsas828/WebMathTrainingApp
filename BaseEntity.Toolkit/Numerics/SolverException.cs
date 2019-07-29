/*
 * SolverException.cs
 *
 *
 */

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Solver Exception class
  /// </summary>
  /// 
  /// <remarks>
  ///   When a SolverException is thrown, the routines can put extra information in 
  ///   the Data property.
  ///   For example, when the base correlation solver could not find
  ///   the correlations for all tranches, it throws a SolverException
  ///   and put the correlations as a double array in Data.  The array
  ///   contains the correlations it has found, together with Double.NaN's
  ///   representing the correlations it could not find.
  /// </remarks>
  [Serializable]
  public class SolverException : ToolkitException
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="message">Exception message</param>
    public SolverException(string message)
      : base(message)
    { }
  }
}
