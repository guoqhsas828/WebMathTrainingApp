//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Numerics;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  using NUnit.Framework;

  [TestFixture]
  public class FunctionCallTests
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
    public void ConstantFolding()
    {
      var x = E.Constant(0.4);
      var y = E.Constant(0.5);
      var f1 = Call(Math.Exp, x);
      var f2 = Call(Math.Max, x, y);
      Assert.That(f1, Is.TypeOf<ConstantEvaluable>());
      Assert.That(f2, Is.TypeOf<ConstantEvaluable>());
      f1.ShouldEvaluateTo(Math.Exp(x.Evaluate()));
      f2.ShouldEvaluateTo(Math.Max(x.Evaluate(), y.Evaluate()));
    }

    [Test]
    public void ConstantEquality()
    {
      const double amount = 19910808;
      var payment = new BasicPayment(
        new Dt(20170808), amount, Currency.USD);
      var f1 = (ConstantEvaluable)payment.GetEvaluableAmount();
      var f2 = (ConstantEvaluable)payment.GetEvaluableAmount();
      f1.ShouldBeSameAs(f2);
      f1.ShouldEvaluateTo(amount);
    }

    [Test]
    public void FunctionEquality()
    {
      var f1 = E.Call(NaturalLogarithmBase);
      var f2 = E.Call(NaturalLogarithmBase);
      f1.ShouldBeSameAs(f2);
      f1.ShouldEvaluateTo(Math.E);
      var d1 = (IDebugDisplay)f1;
      Assert.That(d1.DebugDisplay,
        Does.Contain(typeof(FunctionCallTests).Name));
    }

    [Test]
    public void FunctionInstanceEquality()
    {
      const double amount = 19910808;
      var payment = new PrincipalExchange(
        new Dt(20170808), amount, Currency.USD);
      var f1 = (FunctionCallEvaluable)payment.GetEvaluableAmount();
      var f2 = (FunctionCallEvaluable)payment.GetEvaluableAmount();
      f1.ShouldBeSameAs(f2);
      f1.ShouldEvaluateTo(amount);

      var dp = (IDebugDisplay)payment;
      var d1 = (IDebugDisplay)f1;
      Assert.That(d1.DebugDisplay, Does.Contain(dp.DebugDisplay));
    }

    [Test]
    public void UnaryFunctiony()
    {
      var x = _x;
      var y = _y;
      var f1 = Call(Math.Abs, x + y);
      var f2 = Call(Math.Abs, x + y);
      f1.ShouldBeSameAs(f2);

      x.Value = -10;
      y.Value = 5;
      f1.ShouldEvaluateTo(5.0);

      var d1 = (IDebugDisplay) f1;
      Assert.That(d1.DebugDisplay,
        Does.Contain(typeof (Math).Name));

      var e1 = (IReadOnlyCollection<Evaluable>)f1;
      Assert.That(e1.Count,Is.EqualTo(1));
      var a = e1.ToArray();
      a[0].ShouldBeSameAs(x + y);
    }

    [Test]
    public void UnaryFunctionInstance()
    {
      Func<double, double> func = x => Math.Max(x, 0.5);
      var y = _y;
      var f1 = Call(func, y);
      var f2 = Call(func, y);
      f1.ShouldBeSameAs(f2);

      y.Value = 0.8;
      f1.ShouldEvaluateTo(0.8);

      var d1 = (IDebugDisplay)f1;
      Assert.That(d1.DebugDisplay,
        Does.Contain("UnaryFunctionInstance"));

      var e1 = (IReadOnlyCollection<Evaluable>)f1;
      Assert.That(e1.Count,Is.EqualTo(1));
      var a = e1.ToArray();
      a[0].ShouldBeSameAs(y);
    }

    [Test]
    public void BinaryFunction()
    {
      var x = _x;
      var y = _y;
      var f1 = Call(Math.Max, x, y);
      var f2 = Call(Math.Max, x, y);
      f1.ShouldBeSameAs(f2);

      x.Value = -10;
      y.Value = 10;
      f1.ShouldEvaluateTo(10.0);

      var d1 = (IDebugDisplay)f1;
      Assert.That(d1.DebugDisplay,
        Does.Contain(typeof(Math).Name));

      var e1 = (IReadOnlyCollection<Evaluable>)f1;
      Assert.That(e1.Count,Is.EqualTo(2));
      var a = e1.ToArray();
      a[0].ShouldBeSameAs(x);
      a[1].ShouldBeSameAs(y);
    }

    [Test]
    public void TernaryFunction()
    {
      var x = _x;
      var y = _y;
      var z = _z;
      var f1 = Call(SpecialFunctions.BivariateNormalTail, x, y, z);
      var f2 = Call(SpecialFunctions.BivariateNormalTail, x, y, z);
      f1.ShouldBeSameAs(f2);

      x.Value = -0.5;
      y.Value = 0.5;
      z.Value = 0.3;
      var expect = SpecialFunctions.BivariateNormalTail(
        x.Value, y.Value, z.Value);
      f1.ShouldEvaluateTo(expect);

      var d1 = (IDebugDisplay)f1;
      Assert.That(d1.DebugDisplay,
        Does.Contain(typeof(SpecialFunctions).Name));

      var e1 = (IReadOnlyCollection<Evaluable>) f1;
      Assert.That(e1.Count,Is.EqualTo(3));
      var a = e1.ToArray();
      a[0].ShouldBeSameAs(x);
      a[1].ShouldBeSameAs(y);
      a[2].ShouldBeSameAs(z);
    }

    private static Evaluable Call(Func<double, double> f, Evaluable arg)
    {
      return E.Call(false, f, arg);
    }

    private static Evaluable Call(Func<double, double, double> f,
      Evaluable arg1, Evaluable arg2)
    {
      return E.Call(false, f, arg1, arg2);
    }

    private static Evaluable Call(Func<double, double, double, double> f,
      Evaluable arg1, Evaluable arg2, Evaluable arg3)
    {
      return E.Call(false, f, arg1, arg2, arg3);
    }

    private static double NaturalLogarithmBase()
    {
      return Math.E;
    }
    #endregion
  }
}
