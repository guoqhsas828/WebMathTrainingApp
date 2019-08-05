//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Cashflows.Expressions;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  /// <summary>
  /// This class should not depends on anything in toolkit
  /// other than those in the Cashflows.Expressions namespace. 
  /// </summary>
  [TestFixture]
  public class ExpressionTests
  {
    #region Fixture set up and tear down

    private IDisposable _managerStack;
    private Variable _x, _y, _z;

    [OneTimeSetUp]
    public void Initialize()
    {
      _managerStack = E.PushVariants();
      _x = 0.5;
      _y = 2.0;
      _z = 4.0;
    }

    [OneTimeTearDown]
    public void Restore()
    {
      if (_managerStack == null) return;
      _managerStack.Dispose();
      _managerStack = null;
    }

    #endregion

    #region Tests
    
    [Test]
    public void UniqueInstance()
    {
      Variable x = _x, y = _y;
      var z = x + y;

      // The unique instance of z should be itself.
      E.Unique(z).ShouldBeSameAs(z);

      // The unique instance of (x+y) should be z.
      E.Unique(x+y).ShouldBeSameAs(z);

      // Do (x+y) again should return the same instance.
      (x + y).ShouldBeSameAs(z);

      // The unique instance of null should be null.
      // No exception happens.
      z = null;
      Assert.That(E.Unique(z),Is.Null);
    }

    [Test]
    public void SimpleNegate()
    {
      Variable x = _x, y = _y;
      double x0 = x.Value, y0 = y.Value;
      (-x).ShouldBeSameAs(-x);
      (-x).ShouldEvaluateTo(-x0);
      (-(x + y)).ShouldBeSameAs(-(x + y));
      (-(x + y)).ShouldEvaluateTo(-x0 - y0);
    }

    [Test]
    public void SimpleAdd()
    {
      Variable x = _x, y = _y;
      double x0 = x.Value, y0 = y.Value;
      (x + 0).ShouldBeSameAs(x);
      (0 + x).ShouldBeSameAs(x);
      (x + y).ShouldBeSameAs(x + y);

      var expect = x0 + y0;
      (x + y).ShouldEvaluateTo(expect);
      (x0 + y).ShouldEvaluateTo(expect);
      (x + y0).ShouldEvaluateTo(expect);
    }

    [Test]
    public void SimpleSubtract()
    {
      Variable x = _x, y = _y;
      double x0 = x.Value, y0 = y.Value;
      (x - 0).ShouldBeSameAs(x);
      (0 - x).ShouldBeSameAs(-x);
      (y - x).ShouldBeSameAs(y - x);
      (x - x).ShouldBeConstant(0.0);

      var expect = x0 - y0;
      (x - y).ShouldEvaluateTo(expect);
      (x0 - y).ShouldEvaluateTo(expect);
      (x - y0).ShouldEvaluateTo(expect);
    }

    [Test]
    public void SimpleMultiply()
    {
      Variable x = _x, y = _y;
      double x0 = x.Value, y0 = y.Value;

      Assert.That((x*1),Is.SameAs(x));
      Assert.That((1*x),Is.SameAs(x));
      Assert.That((x*-1),Is.SameAs(-x));
      Assert.That((-1*x),Is.SameAs(-x));
      (x*0).ShouldBeConstant(0.0);
      (0*x).ShouldBeConstant(0.0);

      var expect = x0*y0;
      (x*y).ShouldEvaluateTo(expect);
      (x0*y).ShouldEvaluateTo(expect);
      (x*y0).ShouldEvaluateTo(expect);
    }

    [Test]
    public void SimpleDivide()
    {
      Variable x = _x, y = _y;
      double x0 = x.Value, y0 = y.Value;

      Assert.That((x/1),Is.SameAs(x));
      Assert.That((x/-1),Is.SameAs(-x));
      (x/x).ShouldBeConstant(1.0);
      (x*(1/(2*x))).ShouldBeConstant(0.5);
      (x*(2/x)).ShouldBeConstant(2);

      var expect = x0/y0;
      (x/y).ShouldEvaluateTo(expect);
      (x0/y).ShouldEvaluateTo(expect);
      (x/y0).ShouldEvaluateTo(expect);
    }

    [TestCase(2)]
    [TestCase(0.5)]
    public void SimpleInverse(double a)
    {
      E x = _x, y = _y, z = a / x;
      double x0 = _x.Value, y0 = _y.Value, z0 = a/x0;

      (x*z).ShouldBeConstant(a);
      (z*x).ShouldBeConstant(a);
      (a/(z*x)).ShouldBeConstant(1.0);
      (-a + z*x).ShouldBeConstant(0.0);
      (z*x - a).ShouldBeConstant(0.0);

      (x*z*y).ShouldBeSameAs(a*y);
      (x*z + y).ShouldBeSameAs(a + y);
      (x*z - y).ShouldBeSameAs(a - y);
      (x*z/y).ShouldBeSameAs(a/y);
      (1/(x*z/y)).ShouldBeSameAs(y/a);
      (a*(a/y)).ShouldBeSameAs(a*a/y);
      (y*(x*z/y)).ShouldBeConstant(a);

      //(y*x*z).ShouldBeSameAs(a*y);
      //(y*z*x).ShouldBeSameAs(a*y);
      //(x*y*z).ShouldBeSameAs(a*y);
      //(z*y*x).ShouldBeSameAs(a*y);
      (y*(x*z)).ShouldBeSameAs(a*y);
      (y + x*z).ShouldBeSameAs(a + y);
      (-y + x*z).ShouldBeSameAs(a - y);
      (y/(x*z)).ShouldBeSameAs(y/a);
      (y/(y/(x*z))).ShouldBeConstant(a);

      (z/x).ShouldEvaluateTo(z0/x0, 1E-15);
      (x/z).ShouldEvaluateTo(x0/z0, 1E-15);
      (y/z).ShouldEvaluateTo(y0/z0);
      (y*z).ShouldEvaluateTo(y0*z0, 1E-15);
      (z/y).ShouldEvaluateTo(z0/y0);
      (z*y).ShouldEvaluateTo(y0*z0, 1E-15);
    }

    [TestCase(2)]
    [TestCase(0.5)]
    public void SimpleScale(double a)
    {
      E x = _x, y = _y, z = a*x;
      double x0 = _x.Value, y0 = _y.Value, z0 = a*x0;
      (x/z).ShouldBeConstant(1/a);
      (z/x).ShouldBeConstant(a);
      (z*x).ShouldEvaluateTo(z0*x0);
      (x*z).ShouldEvaluateTo(z0*x0);
      (y/z).ShouldEvaluateTo(y0/z0);
      (y*z).ShouldEvaluateTo(y0*z0, 1E-15);
      (z/y).ShouldEvaluateTo(z0/y0);
      (z*y).ShouldEvaluateTo(y0*z0, 1E-15);
    }

    [TestCase(2, 3)]
    [TestCase(-2, 1)]
    [TestCase(1, 3)]
    [TestCase(-1, 1)]
    [TestCase(2, 0)]
    [TestCase(-2, 0)]
    public void AffineAdd(double a, double b)
    {
      E x = _x, y = _y, z = a * x + b;
      double x0 = _x.Value, y0 = _y.Value, z0 = a*x0 + b;
      (z + y).ShouldEvaluateTo(z0 + y0);
      (z - y).ShouldEvaluateTo(z0 - y0, 1E-15);
      (y + z).ShouldEvaluateTo(y0 + z0);
      (y - z).ShouldEvaluateTo(y0 - z0, 1E-15);

      (z - b - a * x).ShouldBeConstant(0.0);
      (z + (-a * x - b)).ShouldBeConstant(0.0);
    }

    [TestCase(2, 3, 7, 1)]
    [TestCase(-2, 1, 9, 1)]
    [TestCase(1, 3, 2, 0)]
    [TestCase(-1, 1, 2, 0)]
    [TestCase(2, 0, 1, 0)]
    [TestCase(-2, 0, -1, 0)]
    public void AffineAdd(double a1, double b1, double a2, double b2)
    {
      E x = _x, y = _y, z = a1 * x + b1, w = a2*y+b2;
      double x0 = _x.Value, y0 = _y.Value, z0 = a1*x0 + b1, w0 = a2*y0+b2;
      (z + w).ShouldEvaluateTo(z0 + w0, 1E-15);
      (z - w).ShouldEvaluateTo(z0 - w0, 1E-15);
      (w + z).ShouldEvaluateTo(w0 + z0, 1E-15);
      (w - z).ShouldEvaluateTo(w0 - z0, 1E-15);
    }

    [TestCase("", 2, 3)]
    [TestCase("", -2, 1)]
    [TestCase("", 1, 3)]
    [TestCase("", -1, 1)]
    [TestCase("", 2, 0)]
    [TestCase("", -2, 0)]
    [TestCase("Inverse", 2, 3)]
    [TestCase("Inverse", -2, 1)]
    [TestCase("Inverse", 1, 3)]
    [TestCase("Inverse", -1, 1)]
    [TestCase("Inverse", 2, 0)]
    [TestCase("Inverse", -2, 0)]
    public void AffineMultiply(string op, double a, double b)
    {
      E x = _x, y = _y, z = MakeAffine(op, a, x, b);
      double x0 = _x.Value, y0 = _y.Value, z0 = EvalAffine(op, a, x0, b);

      (z*x).ShouldEvaluateTo(z0*x0, 1E-15);
      (z/x).ShouldEvaluateTo(z0/x0, 1E-15);
      (x*z).ShouldEvaluateTo(z0*x0, 1E-15);
      (x/z).ShouldEvaluateTo(x0/z0, 1E-15);

      (z*y).ShouldEvaluateTo(z0*y0);
      (z/y).ShouldEvaluateTo(z0/y0);
      (y*z).ShouldEvaluateTo(z0*y0);
      (y/z).ShouldEvaluateTo(y0/z0);

      (z*y0).ShouldEvaluateTo(z0*y0, 1E-15);
      (z/y0).ShouldEvaluateTo(z0/y0, 1E-15);
      (y*z).ShouldEvaluateTo(y0*z0, 1E-15);
      (y/z).ShouldEvaluateTo(y0/z0, 1E-15);
      (y0*z).ShouldEvaluateTo(y0*z0, 1E-15);
      (y0/z).ShouldEvaluateTo(y0/z0, 1E-15);
    }

    [TestCase(2, 3, 7, 1)]
    [TestCase(-2, 1, 9, 1)]
    [TestCase(1, 3, 2, 0)]
    [TestCase(-1, 1, 2, 0)]
    [TestCase(2, 0, -1, 0)]
    [TestCase(-2, 0, 1, 0)]
    public void AffineMultiply(double a1, double b1, double a2, double b2)
    {
      E x = _x, y = _y, z = a1*x + b1, w = a2*y + b2;
      double x0 = _x.Value, y0 = _y.Value, z0 = a1*x0 + b1, w0 = a2*y0 + b2;
      (z*w).ShouldEvaluateTo(z0*w0, 1E-15);
      (z/w).ShouldEvaluateTo(z0/w0, 1E-15);
      (w*z).ShouldEvaluateTo(w0*z0, 1E-15);
      (w/z).ShouldEvaluateTo(w0/z0, 1E-15);
    }

    private static E MakeAffine(string op, double a, Evaluable x, double b)
    {
      return (op == "Inverse" ? (a/x) : (a*x)) + b;
    }

    private static double EvalAffine(string op, double a, double x, double b)
    {
      return (op == "Inverse" ? (a/x) : (a*x)) + b;
    }

    [TestCase(2, 3)]
    [TestCase(-2, 1)]
    [TestCase(1, 3)]
    [TestCase(-1, 1)]
    [TestCase(2, 0)]
    [TestCase(-2, 0)]
    public void AffineDivide(double a, double b)
    {
      E x = _x, y = _y, z = a*x + b;
      double x0 = _x.Value, y0 = _y.Value, z0 = a*x0 + b;
      (z/y).ShouldEvaluateTo(z0/y0, 1E-15);
      (y/z).ShouldEvaluateTo(y0/z0);
      (z/y0).ShouldEvaluateTo(z0/y0, 1E-15);
      (y0/z).ShouldEvaluateTo(y0/z0);

      (z/(b + a*x)).ShouldBeConstant(1.0);
      //(z/(-a*x - b)).ShouldBeConstant(-1.0);
    }

    [Test]
    public void DistributiveLaws()
    {
      E x = _x, y = _y, z = 2 * y / 2;// E.Interpolate(_y.Curve, _y.Date);
      double x0 = _x.Value, y0 = _y.Value;

      // y and z should be the same instance.
      Assert.That(z,Is.SameAs(y));

      // Simple test of the correctness of (x-y)
      var e = (x - y) as BinaryEvaluable;
      Assert.That(e,Is.Not.Null);
      Assert.That(e.Op,Is.EqualTo(Operator.Subtract));
      Assert.That(e.Left,Is.SameAs(x));
      Assert.That(e.Right,Is.SameAs(y));
      e.ShouldEvaluateTo(x0 - y0);

      // (x/y - 1)*z should simplify to (x - y)
      var f = (x/y - 1)*z;
      Assert.That(f,Is.SameAs(x - y));
      f.ShouldEvaluateTo(x0 - y0);

      // Now let z be different than y 
      z = _z;
      double z0 = _z.Value;
      Assert.That(z,Is.Not.SameAs(y));

      // (x/y - 1)*z should not expand
      var g = ((x/y - 1)*z) as BinaryEvaluable;
      Assert.That(g,Is.Not.Null);
      Assert.That(g.Op,Is.EqualTo(Operator.Multiply));
      Assert.That(g.Left,Is.SameAs(x/y - 1));
      Assert.That(g.Right,Is.SameAs(z));
      g.ShouldEvaluateTo((x0/y0 - 1)*z0);
    }


    [Test]
    public static void MaxAndMin()
    {
      // Create a value expression
      Variable spot = 1.2;
      var one = spot/spot;

      one.ShouldBeConstant(1.0);

      E.Max(1.4, spot).ShouldEvaluateTo(1.4);
      E.Max(spot, 1.4).ShouldEvaluateTo(1.4);
      E.Max(1.4, one).ShouldEvaluateTo(1.4);
      E.Max(one, 1.4).ShouldEvaluateTo(1.4);
      E.Max(1.0, spot).ShouldEvaluateTo(1.2);
      E.Max(spot, 1.0).ShouldEvaluateTo(1.2);
      E.Min(1.4, spot).ShouldEvaluateTo(1.2);
      E.Min(spot, 1.4).ShouldEvaluateTo(1.2);
      E.Min(1.4, one).ShouldEvaluateTo(1.0);
      E.Min(one, 1.4).ShouldEvaluateTo(1.0);
      E.Min(1.0, spot).ShouldEvaluateTo(1.0);
      E.Min(spot, 1.0).ShouldEvaluateTo(1.0);
    }

    #endregion
  }

  #region Mock type

  /// <summary>
  ///  This type represents a simple variable.
  ///  Used in tests only.
  /// </summary>
  class Variable : Evaluable
  {
    /// <summary>
    ///  The current value of the variable
    /// </summary>
    internal double Value;

    /// <summary>
    ///  Override the implicit conversion to allow simple construction
    ///  by initialization.
    /// </summary>
    /// <param name="v">The initial value of the variable</param>
    /// <returns>A new variable</returns>
    public static implicit operator Variable(double v)
    {
      return new Variable { Value = v };
    }

    #region Overrides of Evaluable

    /// <summary>
    ///  Evaluate the expression should simply return the current value.
    /// </summary>
    /// <returns>The value of the expression</returns>
    public override double Evaluate()
    {
      return Value;
    }

    /// <summary>
    ///  Create a LINQ expression representation
    /// </summary>
    /// <returns>The LINQ expression</returns>
    protected override System.Linq.Expressions.Expression Reduce()
    {
      return System.Linq.Expressions.Expression.MakeMemberAccess(
        System.Linq.Expressions.Expression.Constant(this), ValueField);
    }

    #endregion

    private static readonly System.Reflection.MemberInfo ValueField
      = GetMember<Func<Variable, double>>(v => v.Value);
  }

  #endregion


}
