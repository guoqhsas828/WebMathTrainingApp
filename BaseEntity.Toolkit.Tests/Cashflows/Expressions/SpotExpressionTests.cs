//
// Copyright (c)    2002-2015. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows.Expressions;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  [TestFixture]
  public class SpotExpressionTests
  {
    #region Nested type: fake objects used in tests

    /// <summary>Mock object for spot</summary>
    class MySpot : ISpot
    {
      public Currency Ccy { get; set; }
      public string Name { get; set; }
      public Dt Spot { get; set; }
      public double Value { get; set; }
    }

    #endregion

    #region Tests

    [Test]
    public static void FxSpot()
    {
      // Initialize an FX rate object
      Dt asOf = Dt.Today();
      const Currency from = Currency.EUR, to = Currency.USD;
      var fxRate = new FxRate(asOf, asOf, from, to, 1.2);

      // Create a SpotExpression
      var spot = (SpotEvaluable)E.SpotRate(fxRate);

      // Now let's evolve the FX to a new date and new value
      var newDt = Dt.Add(asOf, 91);
      var newRate = 1.4;
      fxRate.Update(newDt, from, to, newRate);

      // The expression should evaluate to the new value
      Assert.That(spot.Date,Is.EqualTo(newDt));
      spot.ShouldEvaluateTo(newRate);

      // The spot as a variable date should evaluate to the new date
      var variableDate = (IVariableDate)spot;
      variableDate.GetExpression().ShouldEvaluateTo(newDt);
    }

    [Test]
    public static void InvariantSpot()
    {
      Dt date = Dt.Today() + 2;

      // This spot object is invariant, because it is not
      // in the non-empty set of variable objects below.
      var spot = new MySpot { Name = "Spot", Spot = date, Value = 0.5 };
      using (E.PushVariants(new[] { new MySpot() }))
      {
        var spotPrice = E.SpotPrice(spot);
        var dated = spotPrice as ConstantEvaluable;
        Assert.That(dated,Is.Not.Null);
        Assert.That(dated.Value,Is.EqualTo(spot.Value));
        Assert.That(dated.Date,Is.EqualTo(spot.Spot));

        var spotRate = E.SpotRate(spot);
        dated = spotRate as ConstantEvaluable;
        Assert.That(dated,Is.Not.Null);
        Assert.That(dated.Value,Is.EqualTo(spot.Value));
        Assert.That(dated.Date,Is.EqualTo(spot.Spot));
      }
    }

    [Test]
    public static void RepeatedVariantIsOk()
    {
      var spot = new MySpot { Name = "Spot", Spot = Dt.Today(), Value = 0.5 };
      // Expect no exception if we push the same objects repeatedly.
      using (E.PushVariants(new[] { spot, spot }))
      {
        // For the same spot object, SpotRate and SpotPrice
        // return the same thing.
        var spotPrice = E.SpotPrice(spot);
        spotPrice.ShouldBeSameAs(E.SpotRate(spot));
      }
    }

    #endregion
  }
}
