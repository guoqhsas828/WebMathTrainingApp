/*
 *  -2015. All rights reserved.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  The base class of all the financial value expressions.
  /// </summary>
  public abstract class Evaluable : IEvaluable
  {
    #region Evaluable instance members

    /// <summary>
    ///  Evaluate the expression
    /// </summary>
    /// <returns>The value of the expression</returns>
    public abstract double Evaluate();

    /// <summary>
    ///  Create a LINQ expression representation
    /// </summary>
    /// <returns>The LINQ expression</returns>
    protected abstract Expression Reduce();

    /// <summary>
    ///  Get the expression representation 
    /// </summary>
    /// <returns>The expression</returns>
    public Expression ToExpression()
    {
      return _expr ?? (_expr = Reduce());
    }

    private Expression _expr;
    #endregion

    #region Manager queries

    /// <summary>
    ///  Each thread has its own manager to work concurrently without lock.
    /// </summary>
    /// <remarks>
    ///   Null manager implies that all the curves and FX spot rates are variable.
    /// </remarks>
    [ThreadStatic]
    private static EvaluableContext _manager;

    /// <summary>
    ///  Get the unique instance of the specified expression.
    /// </summary>
    /// <remarks>
    ///  This guarantees that all the expressions structurally equal
    ///  to each others share the same instance if they are constructed
    ///  through the same manager.
    /// </remarks>
    /// <typeparam name="T">The type of the value expression</typeparam>
    /// <param name="expr">The expression to look up</param>
    /// <returns>The unique instance of the expression</returns>
    public static T Unique<T>(T expr) where T : class
    {
      var m = _manager;
      return m == null ? expr : m.GetOrAdd(expr);
    }

    /// <summary>
    ///  Check if an object is variable, i.e., an object under simulation
    ///  or calibration, hence its value on any given date cannot be taken
    ///  as invariant.
    /// </summary>
    /// <param name="obj">The object to check</param>
    /// <returns>True if the object is variable</returns>
    public static bool IsVariable(object obj)
    {
      // Null manager implies that all the curves are variable.
      var m = _manager;
      return m == null || m.IsVariable(obj);
    }

    /// <summary>
    ///  Set the manager to the specified input for the current scope
    /// </summary>
    /// <param name="variants">A list the objects taken as variant.
    ///   If null, all market objects (curves, spots, prices) are taken as variant</param>
    /// <returns>A object that must be disposed after the scope using the manager</returns>
    public static IDisposable PushVariants(IEnumerable variants = null)
    {
      return new ManagerStack(new EvaluableContext(variants));
    }

    public static void RecordCommonExpressions<T>(IEnumerable<T> data)
      where T : Evaluable
    {
      var m = _manager;
      if (m != null) m.RecordCommonExpressions(data);
    }

    public static IEnumerable<IEvaluable> GetCommonEvaluables()
    {
      var m = _manager;
      return m == null ? null : m.Common.GetCommonExpressions();
    }

    public static IEnumerable<IEvaluable> GetAllEvaluables()
    {
      return _manager == null ? null : _manager.AllEvaluables;
    }

    #region nested type

    private class ManagerStack : IDisposable
    {
      private readonly EvaluableContext _previous;
      public ManagerStack(EvaluableContext current)
      {
        _previous = _manager;
        _manager = current;
      }
      public void Dispose()
      {
        _manager = _previous;
      }
    }

    #endregion

    #endregion

    #region Static members and operator overloads

    #region Reflection helpers

    public static MemberInfo GetMember<T>(Expression<T> f)
    {
      var e = f.Body as MemberExpression;
      if (e == null)
        throw new ArgumentException("f is not a property access expression");
      return e.Member;
    }

    public static MethodInfo GetMethod<T>(Expression<T> f)
    {
      var m = f.Body as MethodCallExpression;
      if (m == null)
        throw new ArgumentException("f is not a method call expression");
      return m.Method;
    }

    public static string Display(object o)
    {
      var d = o as IDebugDisplay;
      return d != null ? d.DebugDisplay
        : (o == null ? "<null>" : o.ToString());
    }

    #endregion

    #region Primitive expressions

    public static Evaluable Constant(double value)
    {
      return Unique(new ConstantEvaluable(value));
    }

    public static Evaluable Constant(double value, Dt date)
    {
      return Unique(new ConstantEvaluable(value, date));
    }

    public static readonly Evaluable Zero = Constant(0.0);
    public static readonly Evaluable One = Constant(1.0);
    #endregion

    #region Curve operations

    public static Evaluable Interpolate(
      Curve curve, Dt begin, Dt end)
    {
      return Interpolate(curve, end)/Interpolate(curve, begin);
    }

    /// <summary>
    ///  Calculate the simple forward rate between two dates
    /// </summary>
    /// <param name="curve">The curve</param>
    /// <param name="begin">The begin date</param>
    /// <param name="end">The end date</param>
    /// <param name="dc">The day count convention</param>
    /// <returns>Evaluable</returns>
    public static Evaluable ForwardRate(
      Curve curve, Dt begin, Dt end, DayCount dc)
    {
      var fraction = Dt.Fraction(begin, end, dc);
      if (fraction.Equals(0.0)) return Zero;

      var df0 = Interpolate(curve, begin);
      var df1 = Interpolate(curve, end);
      return (df0/df1 - 1)/fraction;
    }

    /// <summary>
    ///  Create an expression representing the curve interpolation
    ///  on the specified date.
    /// </summary>
    /// <param name="curve">The curve</param>
    /// <param name="date">The date</param>
    /// <returns>The expression representation</returns>
    public static Evaluable Interpolate(Curve curve, Dt date)
    {
      if (IsVariable(curve))
      {
        if (HasNativeOverlay(curve))
        {
          //TODO: handle overlay here
          return Unique(new CurveInterpolateConstantDate(curve, date));
        }
        return MakeInterpolateExpression(curve, date);
      }

      var ov = GetVariableNativeOverlay(curve);
      if (ov == null)
      {
        // The whole curve is not simulated.
        return Constant(curve.Interpolate(date), date);
      }

      return Scale(curve.Interpolate(date)/ov.Interpolate(date),
        MakeInterpolateExpression(ov, date));
    }

    /// <summary>
    ///  Create an expression representing the curve interpolation
    ///  on the specified date.
    /// </summary>
    /// <param name="curve">The curve</param>
    /// <param name="date">The expression to get the date</param>
    /// <returns>The expression representation</returns>
    public static Evaluable Interpolate(Curve curve, Evaluable date)
    {
      var c = date as ConstantEvaluable;
      if (c != null)
      {
        if (c.Date.IsEmpty())
        {
          throw new ArgumentException("date is not a dated constant");
        }
        return Interpolate(curve, c.Date);
      }
      var v = date as IVariableDate;
      if (v != null)
      {
        return MakeInterpolateExpression(curve, v);
      }
      throw new ArgumentException("date is not a dated expression");
    }

    private static Evaluable MakeInterpolateExpression(
      Curve curve, IVariableDate date)
    {
      var correction = curve.CustomInterpolator as CorrectiveOverlay;
      if (correction != null)
      {
        var overlay = correction.OverlayCurve;
        var simulated = (Curve)correction.BaseCurve;
        return Unique(new CurveInterpolateVariableDate(overlay, date))
          *Unique(new CurveInterpolateVariableDate(simulated, date));
      }
      return Unique(new CurveInterpolateVariableDate(curve, date));
    }

    private static Evaluable MakeInterpolateExpression(Curve curve, Dt date)
    {
      var correction = curve.CustomInterpolator as CorrectiveOverlay;
      if (correction != null)
      {
        var simulated = (Curve)correction.BaseCurve;
        return Scale(curve.Interpolate(date) / simulated.Interpolate(date),
          CreateInterpolation(simulated, date));
      }
      return CreateInterpolation(curve, date);
    }

    private static Evaluable CreateInterpolation(Curve curve, Dt date)
    {
      var dc = curve.DayCount;
      if (dc == DayCount.Actual365Fixed || dc == DayCount.None)
        return Unique(new CurveInterpolateConstantTime(curve, date));
      return Unique(new CurveInterpolateConstantDate(curve, date));
    }

    private static Curve GetVariableNativeOverlay(Curve curve)
    {
      var ov = ((NativeCurve)curve).Overlay;
      if (ov == null) return null;
      Debug.Assert(_manager != null);
      return _manager.GetVariable((Curve)ov) as Curve;
    }

    private static bool HasNativeOverlay(Curve curve)
    {
      return ((NativeCurve)curve).Overlay != null;
    }
    #endregion

    #region Forward FX interpolations
    /// <summary>
    /// Get FxRate object
    /// </summary>
    /// <param name="fxCurve">fx curve</param>
    /// <param name="date">date</param>
    /// <param name="fromCcy">from ccy</param>
    /// <param name="toCcy">to ccy</param>
    /// <returns></returns>
    public static Evaluable FxRate(FxCurve fxCurve,
      Dt date, Currency fromCcy, Currency toCcy)
    {
      return FxRateEvaluation.FxRate(fxCurve, date, fromCcy, toCcy);
    }

    public static Evaluable MakeForwardFxRate(
      FxCurve fxCurve, Dt date, Currency from, Currency to)
    {
      return Unique(new FxForwardEvaluable(fxCurve, date, from, to));
    }


    #endregion

    #region Spot rate/price operations

    public static Evaluable SpotRate(ISpot spot)
    {
      return MakeSpot(spot);
    }

    public static Evaluable SpotPrice(ISpot spot)
    {
      return MakeSpot(spot);
    }

    private static Evaluable MakeSpot(ISpot spot)
    {
      Debug.Assert(spot != null);
      return IsVariable(spot)
        ? Unique(new SpotEvaluable(spot))
        : Constant(spot.Value, spot.Spot);
    }

    #endregion

    #region Arithmetic operations

    private static Evaluable Add(
      Evaluable left, Evaluable right)
    {
      var rc = right as ConstantEvaluable;
      if (rc != null)
        return Drift(rc.Value, left);

      var lc = left as ConstantEvaluable;
      if (lc != null)
        return Drift(lc.Value, right);

      var f = left as IAffine;
      if (f != null)
        return AffineAdd(f.A, f.X, f.B, right);

      f = right as IAffine;
      if (f != null)
        return AffineAdd(f.A, f.X, f.B, left);

      return MakeAdd(left, right);
    }

    // a*x + b + y
    private static Evaluable AffineAdd(
      double a, Evaluable x, double b, Evaluable y)
    {
      var f = y as IAffine;
      if (f != null)
        return AffineAdd(a, x, f.A, f.X, b + f.B);
      if (ReferenceEquals(x, y))
        return AffineCreate(a + 1.0, x, b);
      if (a.AlmostEquals(-1.0))
        return MakeDrift(b, MakeSubtract(y, x));
      return MakeDrift(b, MakeAdd(MakeScale(a, x), y));
    }

    // a1*x1 + a2*x2 + b
    private static Evaluable AffineAdd(
      double a1, Evaluable x1,
      double a2, Evaluable x2,
      double b)
    {
      if (ReferenceEquals(x1, x2))
        return AffineCreate(a1 + a2, x1, b);
      if (Math.Abs(a1) >= Math.Abs(a2))
        return AffineCreate(a1, MakeAdd(x1, MakeScale(a2/a1, x2)), b);
      return AffineCreate(a2, MakeAdd(MakeScale(a1/a2, x1), x2), b);
    }

    private static Evaluable AffineCreate(
      double a, Evaluable x, double b)
    {
      if (a.AlmostEquals(0.0)) return Constant(b);
      if (a.AlmostEquals(1.0)) return MakeDrift(b, x);
      if (b.AlmostEquals(0.0)) return MakeScale(a, x);
      return Unique(new Affine(a, x, b));
    }

    private static Evaluable Drift(double a, Evaluable ex)
    {
      var f = ex as IAffine;
      if (f != null)
        return AffineCreate(f.A, f.X, a + f.B);

      return MakeDrift(a, ex);
    }

    private static Evaluable Scale(double scale, Evaluable ex)
    {
      var si = ex as Inverse;
      if (si != null)
        return Divide(scale*si.Const, si.Node);

      var f = ex as IAffine;
      if (f != null)
        return AffineCreate(scale * f.A, f.X, scale * f.B);

      return MakeScale(scale, ex);
    }

    private static Evaluable MakeDrift(
      double b, Evaluable x)
    {
      var c = x as ConstantEvaluable;
      if (c != null)
        return Constant(c.Value + b);

      if (b.AlmostEquals(0.0))
        return x;

      return Unique(new Drift(b, x));
    }

    private static Evaluable MakeScale(
      double a, Evaluable x)
    {
      var c = x as ConstantEvaluable;
      if (c != null)
        return Constant(a * c.Value);

      if (a.AlmostEquals(1.0))
        return x;

      if (a.AlmostEquals(0.0))
        return Constant(0.0);

      return Unique(new Scaled(a, x));
    }

    private static Evaluable MakeAdd(
      Evaluable x, Evaluable y)
    {
      return Unique(new Add(x, y));
    }

    private static Evaluable MakeSubtract(
      Evaluable x, Evaluable y)
    {
      return Unique(new Subtract(x, y));
    }

    private static Evaluable Divide(double left, Evaluable right)
    {
      var rc = right as ConstantEvaluable;
      if (rc != null)
        return Constant(left/rc.Value);

      var ri = right as Inverse;
      if (ri != null)
        return Scale(left/ri.Const, ri.Node);

      var rs = right as Scaled;
      return rs != null
        ? Unique(new Inverse(left/rs.Const, rs.Node))
        : Unique(new Inverse(left, right));
    }

    private static Evaluable Multiply(Evaluable left, Evaluable right)
    {
      var rc = right as ConstantEvaluable;
      if (rc != null)
        return Scale(rc.Value, left);

      var ri = right as Inverse;
      if (ri != null)
        return Divide(ri.Const, left, ri.Node);

      var rs = right as Scaled;
      return rs != null
        ? Multiply(rs.Const, left, rs.Node)
        : Multiply(1.0, left, right);
    }

    private static Evaluable Multiply(double c, Evaluable left, Evaluable right)
    {
      var lc = left as ConstantEvaluable;
      if (lc != null)
      {
        if (lc == Zero)
        {
          return Zero;
        }
        return Scale(c*lc.Value, right);
      }

      var li = left as Inverse;
      if (li != null)
        return Divide(c*li.Const, right, li.Node);

      var f = AffineMultiply(c, left as IAffine, right)
        ?? AffineMultiply(c, right as IAffine, left);
      if (f != null) return f;

      var ls = left as Scaled;
      return ls != null
        ? MakeScale(c*ls.Const, MakeMultiply(ls.Node, right))
        : MakeScale(c, MakeMultiply(left, right));
    }

    private static Evaluable MakeMultiply(Evaluable left, Evaluable right)
    {
      var divide = left as Divide;
      if (divide != null && divide.Right == right)
        return divide.Left;

      divide = right as Divide;
      if (divide != null && divide.Right == left)
        return divide.Left;

      return Unique(new Multiply(left, right));
    }

    private static Evaluable AffineMultiply(
      double c, IAffine f, Evaluable y)
    {
      if (f == null) return null;
      var r = f.X as Divide;
      if (r != null && ReferenceEquals(r.Right, y))
      {
        return (c*f.A)*r.Left + (c*f.B)*y;
      }
      var i = f.X as Inverse;
      if (i != null && ReferenceEquals(i.Node, y))
      {
        return (c*f.A*i.Const) + (c*f.B)*y;
      }
      return null;
    }

    private static Evaluable Divide(double c, Evaluable left, Evaluable right)
    {
      if (right == left)
      {
        return Constant(c);
      }

      var lc = left as ConstantEvaluable;
      if (lc != null)
      {
        if (lc == Zero)
        {
          return Zero;
        }
        return Divide(c*lc.Value, right);
      }

      var li = left as Inverse;
      if (li != null)
        return Divide(c*li.Const, Multiply(li.Node, right));

      var ls = left as Scaled;
      return ls != null
        ? MakeScale(c*ls.Const, MakeDivide(ls.Node, right))
        : MakeScale(c, MakeDivide(left, right));
    }

    private static Evaluable Divide(Evaluable left, Evaluable right)
    {
      var rc = right as ConstantEvaluable;
      if (rc != null) 
        return Scale(1/rc.Value, left);

      var ri = right as Inverse;
      if (ri != null)
        return Multiply(1.0/ri.Const, left, ri.Node);

      var rs = right as Scaled;
      return rs != null
        ? Divide(1.0/rs.Const, left, rs.Node)
        : Divide(1.0, left, right);
    }

    private static Evaluable MakeDivide(Evaluable left, Evaluable right)
    {
      return ReferenceEquals(left, right) ? One
        : Unique(new Divide(left, right));
    }

    #endregion

    #region cast operators

    /// <summary>
    /// implicit convert a double value to constant expression
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Evaluable(double value)
    {
      return Constant(value);
    }

    #endregion

    #region Operator overloads: negate, add and subtract

    /// <summary>
    ///  Negate a value
    /// </summary>
    /// <param name="v">The value expression</param>
    /// <returns>Negated value</returns>
    public static Evaluable operator -(Evaluable v)
    {
      return Scale(-1.0, v);
    }

    /// <summary>
    ///  Add two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The sum of the left and right values</returns>
    public static Evaluable operator +(
      Evaluable left, Evaluable right)
    {
      return Add(left, right);
    }

    /// <summary>
    ///  Add two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The sum of the left and right values</returns>
    public static Evaluable operator +(double left, Evaluable right)
    {
      return Drift(left, right);
    }

    /// <summary>
    ///  Add two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The sum of the left and right values</returns>
    public static Evaluable operator +(Evaluable left, double right)
    {
      return Drift(right, left);
    }

    /// <summary>
    ///  Subtract a value from another
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The difference between the left and right values</returns>
    public static Evaluable operator -(
      Evaluable left, Evaluable right)
    {
      return left + (-right);
    }

    /// <summary>
    ///  Subtract a value from another
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The difference between the left and right values</returns>
    public static Evaluable operator -(double left, Evaluable right)
    {
      return left + (-right);
    }

    /// <summary>
    ///  Subtract a value from another
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The difference between the left and right values</returns>
    public static Evaluable operator -(Evaluable left, double right)
    {
      return -right + left;
    }

    #endregion

    #region Operator overloads: multiply and divide

    /// <summary>
    ///  Multiply two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The product of the left and right values</returns>
    public static Evaluable operator *(
      Evaluable left, Evaluable right)
    {
      return Multiply(left, right);
    }

    /// <summary>
    ///  Multiply two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The product of the left and right values</returns>
    public static Evaluable operator *(double left, Evaluable right)
    {
      return Scale(left, right);
    }

    /// <summary>
    ///  Multiply two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The product of the left and right values</returns>
    public static Evaluable operator *(Evaluable left, double right)
    {
      return Scale(right, left);
    }

    /// <summary>
    ///  Divide two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The ratio of the left and right values</returns>
    public static Evaluable operator /(
      Evaluable left, Evaluable right)
    {
      return Divide(left, right);
    }

    /// <summary>
    ///  Divide two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The ratio of the left and right values</returns>
    public static Evaluable operator /(double left, Evaluable right)
    {
      return Divide(left, right);
    }

    /// <summary>
    ///  Divide two values
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The ratio of the left and right values</returns>
    public static Evaluable operator /(Evaluable left, double right)
    {
      return Scale(1/right, left);
    }

    #endregion

    #region Mathematic functions

    /// <summary>
    ///  Returns the largest value
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The largest value</returns>
    public static Evaluable Max(double left, Evaluable right)
    {
      var rc = right as ConstantEvaluable;
      if (rc != null) return Constant(Math.Max(left, rc.Value));
      return Unique(new Floor(left, right));
    }

    /// <summary>
    ///  Returns the largest value
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The largest value</returns>
    public static Evaluable Max(Evaluable left, double right)
    {
      return Max(right, left);
    }

    /// <summary>
    ///  Returns the smallest value
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The smallest value</returns>
    public static Evaluable Min(double left, Evaluable right)
    {
      var rc = right as ConstantEvaluable;
      if (rc != null) return Constant(Math.Min(left, rc.Value));
      return Unique(new Cap(left, right));
    }

    /// <summary>
    ///  Returns the smallest value
    /// </summary>
    /// <param name="left">The left hand side</param>
    /// <param name="right">The right hand side</param>
    /// <returns>The smallest value</returns>
    public static Evaluable Min(Evaluable left, double right)
    {
      return Min(right, left);
    }

    #endregion

    #region Call external functions

    /// <summary>
    /// Calls the specified function
    /// </summary>
    /// <param name="f">The function to call</param>
    /// <returns>Evaluable</returns>
    public static Evaluable Call(Func<double> f)
    {
      return Unique(new FunctionCallEvaluable(f));
    }

    /// <summary>
    /// Calls the specified unary function
    /// </summary>
    /// <param name="invariantTarget">
    ///  <c>true</c> to indicate that function target
    ///   does not depend on any variable objects
    /// </param>
    /// <param name="f">The unary function</param>
    /// <param name="arg">The argument</param>
    /// <returns>Evaluable</returns>
    public static Evaluable Call(
      bool invariantTarget,
      Func<double, double> f, Evaluable arg)
    {
      var ce = arg as ConstantEvaluable;
      if (ce != null && (invariantTarget || f.Target == null))
        return f(ce.Value);
      return Unique(new UnaryFunctionCallEvaluable(f, arg));
    }

    /// <summary>
    /// Calls the specified binary function
    /// </summary>
    /// <param name="invariantTarget">
    ///  <c>true</c> to indicate that function target
    ///   does not depend on any variable objects
    /// </param>
    /// <param name="f">The binary function</param>
    /// <param name="arg1">The argument 1</param>
    /// <param name="arg2">The argument 2</param>
    /// <returns>Evaluable.</returns>
    public static Evaluable Call(
      bool invariantTarget,
      Func<double, double, double> f,
      Evaluable arg1, Evaluable arg2)
    {
      var c1 = arg1 as ConstantEvaluable;
      var c2 = arg2 as ConstantEvaluable;
      if (c1 != null && c2 != null
        && (invariantTarget || f.Target == null))
      {
        return f(c1.Value, c2.Value);
      }
      return Unique(new BinaryFunctionCallEvaluable(f, arg1, arg2));
    }

    /// <summary>
    /// Calls the specified ternary function
    /// </summary>
    /// <param name="invariantTarget">
    ///  <c>true</c> to indicate that function target
    ///   does not depend on any variable objects
    /// </param>
    /// <param name="f">The ternary function</param>
    /// <param name="arg1">The argument 1</param>
    /// <param name="arg2">The argument 2</param>
    /// <param name="arg3">The argument 3</param>
    /// <returns>Evaluable.</returns>
    public static Evaluable Call(
      bool invariantTarget,
      Func<double, double, double, double> f,
      Evaluable arg1, Evaluable arg2, Evaluable arg3)
    {
      var c1 = arg1 as ConstantEvaluable;
      var c2 = arg2 as ConstantEvaluable;
      var c3 = arg3 as ConstantEvaluable;
      if (c1 != null && c2 != null && c3 != null
        && (invariantTarget || f.Target == null))
      {
        return f(c1.Value, c2.Value, c3.Value);
      }
      return Unique(new TernaryFunctionCallEvaluable(
        f, arg1, arg2, arg3));
    }

    #endregion

    #endregion
  }

}
