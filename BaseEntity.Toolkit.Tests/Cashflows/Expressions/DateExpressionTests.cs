//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Linq.Expressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  [TestFixture]
  public class DateExpressionTests
  {
    #region Fixture set up and tear down

    private static readonly DateVariable
      X = new DateVariable { Name = "X" },
      Y = new DateVariable { Name = "Y" };

    private IDisposable _managerStack;
    private DateEvaluable _x, _y;

    [OneTimeSetUp]
    public void Initialize()
    {
      _managerStack = E.PushVariants();
      _x = DateEvaluable.Create(X);
      _y = DateEvaluable.Create(Y);
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
      var x = DateEvaluable.Create(X);
      var y = DateEvaluable.Create(Y);

      // The date evaluable with the same back objects
      // should have the same unique references.
      x.ShouldBeSameAs(_x);
      y.ShouldBeSameAs(_y);

      // Check the evaluable
      Dt date1 = new Dt(20160808);
      X.Date = date1;
      x.ShouldEvaluateTo(date1 - Dt.MinValue);

      Dt date2 = new Dt(20260808);
      X.Date = date2;
      x.ShouldEvaluateTo(date2 - Dt.MinValue);

      // Check date variables
      var u = (IVariableDate) x;
      Assert.That(u.Date,Is.EqualTo(X.Date));
      Assert.That(u.Name,Is.EqualTo(X.Name));
      u.GetExpression().ShouldEvaluateTo(X.Date);

      // Check the debug display
      Assert.That(x.DebugDisplay,Is.EqualTo(X.Name));
    }

    [Test]
    public void DateDifference()
    {
      var x = _x;
      var y = _y;

      // Check the dates
      Dt date1 = new Dt(20160808), date2 = new Dt(20260808);
      var expect = date2 - date1;

      X.Date = date1;
      Y.Date = date2;

      (date2 - x).ShouldEvaluateTo(expect);
      (y - date1).ShouldEvaluateTo(expect);
      (y - x).ShouldEvaluateTo(expect);

      (x - date2).ShouldEvaluateTo(-expect);
      (date1 - y).ShouldEvaluateTo(-expect);
      (x - y).ShouldEvaluateTo(-expect);
    }

    [Test]
    public void GlobalPricingDate()
    {
      var x = PricingDate.AsVariable;
      var u = (IVariableDate)x;
      var y = _y;

      // Check the name property
      Assert.That(u.Name,Is.EqualTo(PricingDate.Name));

      // Check the dates
      Dt date1 = new Dt(20160808), date2 = new Dt(20260808);

      // Set pricing date to a value and check consistency
      PricingDate.Value = date2;
      Assert.That(PricingDate.Value,Is.EqualTo(date2));
      x.ShouldEvaluateTo(date2 - Dt.MinValue);
      Assert.That(u.Date,Is.EqualTo(date2));
      u.GetExpression().ShouldEvaluateTo(date2);

      // Now set it to a different value
      PricingDate.Value = date1;
      Assert.That(PricingDate.Value,Is.EqualTo(date1));
      x.ShouldEvaluateTo(date1 - Dt.MinValue);
      Assert.That(u.Date,Is.EqualTo(date1));
      u.GetExpression().ShouldEvaluateTo(date1);

      // Some arithmetic
      var expect = date2 - date1;
      (date2 - x).ShouldEvaluateTo(expect);

      Y.Date = date2;
      (y - x).ShouldEvaluateTo(expect);
    }

    #endregion
  }

  #region Mock Type: DateVariable

  class DateVariable : IVariableDate
  {
    public Dt Date { get; set; }

    public string Name { get; set; }

    public Expression GetExpression()
    {
      return Expression.MakeMemberAccess(
        Expression.Constant(this),
        E.GetMember<Func<IVariableDate, Dt>>(v => v.Date));
    }
  }

  #endregion
}
