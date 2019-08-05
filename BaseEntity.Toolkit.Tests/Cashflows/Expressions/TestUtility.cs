//
// Copyright (c)    2002-2015. All rights reserved.
//
using System;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  internal static class TestUtility
  {
    #region Expression test utilities

    public static void ShouldBeConstant(this Evaluable ex, double expect)
    {
      Assert.That(ex,Is.InstanceOf<ConstantEvaluable>());
      var ce = (ConstantEvaluable)ex;
      Assert.That(ce.Value,Is.EqualTo(expect));
    }

    public static void ShouldBeSameAs(this Evaluable ex, Evaluable expect)
    {
      Assert.That(ex,Is.SameAs(expect));
    }

    public static void ShouldEvaluateTo<T>(this Evaluable ex, T expect, double tolerance)
    {
      Assert.That(ex.Evaluate(),Is.EqualTo(expect).Within(tolerance),"Evaluate");
      ex.ToExpression().ShouldEvaluateTo(expect, tolerance);
    }

    public static void ShouldEvaluateTo<T>(this Expression ex, T expect, double tolerance)
    {
      var lambda = Expression.Lambda<Func<T>>(ex, true);
      Assert.That(lambda.Compile()(),Is.EqualTo(expect).Within(tolerance),"Compiled");
    }

    public static void ShouldEvaluateTo<T>(this Evaluable ex, T expect)
    {
      Assert.That(ex.Evaluate(),Is.EqualTo(expect),"Evaluate");
      ex.ToExpression().ShouldEvaluateTo(expect);
    }

    public static void ShouldEvaluateTo<T>(this Expression ex, T expect)
    {
      var lambda = Expression.Lambda<Func<T>>(ex, true);
      Assert.That(lambda.Compile()(),Is.EqualTo(expect),"Compiled");
    }

    #endregion

    #region Match objects

    internal static void ShouldMatchObjectGraph(
      this object actual, object expect)
    {
      var mismatch = ObjectStatesChecker.Compare(actual, expect);
      if (mismatch == null) return;
      throw new AssertionException(mismatch.ToString());
    }

    internal static void ShouldMatchObjectGraph(
      this object actual, object expect, double tolerance)
    {
      var mismatch = ObjectStatesChecker.Compare(actual, expect, tolerance);
      if (mismatch == null) return;
      throw new AssertionException(mismatch.ToString());
    }


    #endregion

    #region ReferenceIndex utilities

    internal static ReferenceIndex GetIndex(string name)
    {
      return BaseEntity.Toolkit.Curves.StandardReferenceIndices.Create(name);
    }

    #endregion

    #region FX utility

    public static void Update(this FxRate fxRate,
      Dt asOf, Currency from, Currency to, double rate)
    {
      evolveFx(fxRate, asOf, from, to, rate);
    }

    private static Action<FxRate, Dt, Currency, Currency, double>
      GetEvolveFxMethod()
    {
      var method = typeof (FxRate).GetMethod("Update",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
        null,
        new[] {typeof (Dt), typeof (Currency), typeof (Currency), typeof (double)},
        null);
      return (Action<FxRate, Dt, Currency, Currency, double>)
        Delegate.CreateDelegate(
          typeof (Action<FxRate, Dt, Currency, Currency, double>),
          method);
    }

    private static Action<FxRate, Dt, Currency, Currency, double> evolveFx
      = GetEvolveFxMethod();

    #endregion
  }
}
