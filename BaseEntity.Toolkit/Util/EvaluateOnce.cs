using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  /// Utility class evaluating the specified
  /// function only once (per reseting).
  /// </summary>
  internal class EvaluateOnce<T>
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluateOnce"/> class.
    /// </summary>
    /// <param name="calculator">The calculator.</param>
    internal EvaluateOnce(Expression<Func<T>> calculator)
    {
      Debug.Assert(calculator != null);
      _expression = calculator;
      _calculator = calculator.Compile();
    }

    /// <summary>
    /// Evaluates the function.
    /// </summary>
    /// <returns>System.Double.</returns>
    internal T Evaluate()
    {
      if (!_hasValue)
      {
        _value = _calculator();
        _hasValue = true;
      }
      return _value;
    }

    /// <summary>
    /// Resets this instance.
    /// </summary>
    public void Reset()
    {
      _hasValue = false;
    }

    private T _value;
    private bool _hasValue;
    private readonly Expression<Func<T>> _expression;
    private readonly Func<T> _calculator;

    internal Expression<Func<T>> Expression { get { return _expression; } }
  }

  /// <summary>
  /// Utilities to transform the specified functions into a
  ///  function which is evaluated only once (per reseting).
  /// </summary>
  public static class EvaluateOnce
  {
    /// <summary>
    /// Transforms the specified function as evaluate once function.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    /// <param name="fn">The function</param>
    /// <returns>System.Func&lt;T&gt;</returns>
    public static Func<T> Get<T>(Expression<Func<T>> fn)
    {
      return new EvaluateOnce<T>(fn).Evaluate;
    }
  }
}
