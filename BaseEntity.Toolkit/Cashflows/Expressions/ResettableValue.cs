/*
 *  -2015. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  A convenient base class of resettable values
  /// </summary>
  public abstract class ResettableValue : Evaluable, IResettable
  {
    private double _result;
    private int _state = AlwaysRecomputeState;
    private const int AlwaysRecomputeState = 0,
      ResetState = 1, ComputedState = 2;

    /// <summary>
    ///  Evaluate the expression, implemented with cache.
    /// </summary>
    /// <returns>The evaluation result</returns>
    public override double Evaluate()
    {
      switch (_state)
      {
      case AlwaysRecomputeState:
        return Compute();
      case ResetState:
        _result = Compute();
        _state = ComputedState;
        return _result;
      case ComputedState:
        return _result;
      }
      throw new ToolkitException(String.Format("Invalid state {0}", _state));
    }

    /// <summary>
    ///  Reset the result to uninitialized state.
    /// </summary>
    public void Reset()
    {
      _state = ResetState;
    }

    /// <summary>
    ///  Actually calculate the result
    /// </summary>
    /// <returns>The computed result</returns>
    protected abstract double Compute();
  }
}
