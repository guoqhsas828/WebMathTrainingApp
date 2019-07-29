/*
 *  -2015. All rights reserved.
 */

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  Evaluable context to keep track of variable market objects
  ///  and evaluable expressions.
  /// </summary>
  internal class EvaluableContext
  {
    #region Nested type: Equality comparer

    /// <summary>
    ///  Specialized comparer, which compares curve by Id (the unmanaged pointer)
    ///  instead of by managed reference, because the same unmanaged curve may be
    ///  represented by several managed instances.
    /// </summary>
    private class MyComparer : IEqualityComparer
    {
      bool IEqualityComparer.Equals(object x, object y)
      {
        Debug.Assert(!ReferenceEquals(x, y));

        var xcurve = x as Curve;
        var ycurve = y as Curve;
        return (xcurve != null && ycurve != null
          && xcurve.Id == ycurve.Id);
      }

      int IEqualityComparer.GetHashCode(object obj)
      {
        var curve = obj as Curve;
        return curve != null ? curve.Id.GetHashCode() : obj.GetHashCode();
      }
    }

    private static IEqualityComparer _myComparer = new MyComparer();

    #endregion

    #region Data and property queries

    // All instances of value expressions
    private readonly Hashtable _pool = new Hashtable(
      StructuralComparisons.StructuralEqualityComparer);

    // All the variable objects, either curves or FX rates,
    // which cannot be treated as constants.
    // If it is null, then all the curves and spot FX rate 
    // are treated as variable.
    private readonly Hashtable _variables = null;

    /// <summary>
    ///  Check if an object is variable, where a variable object is
    ///  an object under simulation or calibration, hence the value
    ///  on any given date cannot be considered as invariant.
    /// </summary>
    /// <param name="obj">The object to check</param>
    /// <returns>True if the object is variable</returns>
    public bool IsVariable(object obj)
    {
      Debug.Assert(obj != null);
      return _variables == null || _variables.ContainsKey(obj);
    }

    /// <summary>
    ///  Get a variable which is equal to the specified object,
    ///  returns null if the object is not a variable.
    /// </summary>
    /// <remarks>
    ///  We can use this to get a curve reference from the native curve presentation.
    /// </remarks>
    /// <param name="obj">The object to compare</param>
    /// <returns>A variable equal to the input object</returns>
    internal object GetVariable(object obj)
    {
      return obj == null || _variables == null ? obj : _variables[obj];
    }

    /// <summary>
    ///  Get the instance if a copy already exists in the pool which
    ///  is structurally equal to the specified expression;  otherwise,
    ///  put it in the pool.
    /// </summary>
    /// <remarks>
    ///  This guarantees that all the expressions structurally equal
    ///  to each others share the same instance if they are constructed
    ///  through the same manager.
    /// </remarks>
    /// <typeparam name="T">The type of the value expression</typeparam>
    /// <param name="expression">The expression to look up</param>
    /// <returns>The unique instance of the expression</returns>
    public T GetOrAdd<T>(T expression) where T : class
    {
      if (expression == null) return null;
      object found = _pool[expression];
      if (found != null) return (T) found;
      _pool.Add(expression, expression);
      return expression;
    }

    /// <summary>
    ///   Gets all the resettable expressions
    /// </summary>
    public IEnumerable<IEvaluable> AllEvaluables
    {
      get { return _pool.Keys.OfType<IEvaluable>(); }
    }

    #endregion

    #region Common expressions finder

    internal readonly CommonExpressionCollector Common
      = new CommonExpressionCollector();

    internal void RecordCommonExpressions<T>(IEnumerable<T> data)
      where T : Evaluable
    {
      Common.Process(data);
    }

    #endregion

    #region Constructor

    public EvaluableContext(IEnumerable variables = null)
    {
      if (variables == null) return;
      _variables = new Hashtable(_myComparer);
      foreach (var obj in variables)
      {
        if (obj != null && !_variables.ContainsKey(obj))
          _variables.Add(obj, obj);
      }
    }

    #endregion
  }
}
