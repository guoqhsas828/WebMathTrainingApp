/*
 *  -2015. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  Anything that can be evaluated to a double value.
  /// </summary>
  public interface IEvaluable
  {
    /// <summary>Evaluate the value</summary>
    /// <returns>Value</returns>
    double Evaluate();
  }

  /// <summary>
  ///  Anything that can be reset to uninitialized state.
  /// </summary>
  public interface IResettable
  {
    /// <summary>Reset to uninitialized state</summary>
    void Reset();
  }

  /// <summary>
  ///  Interface to provide debugger display string
  /// </summary>
  public interface IDebugDisplay
  {
    /// <summary>
    ///  Gets the debugger display string of this object
    /// </summary>
    string DebugDisplay { get; }
  }
}
