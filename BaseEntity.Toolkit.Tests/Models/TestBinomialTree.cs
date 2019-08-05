//
// Test BinomialTree model
// Copyright (c)    2002-2018. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using NAssert = NUnit.Framework.Assert;

namespace BaseEntity.Toolkit.Tests.Models
{

  [TestFixture(Category = "Models")]
  public class TestBinomialTree : ToolkitTestBase
  {
    [Test]
    public void ExerciseInTheMoneyCallS()
    {
      double delta = 0, gamma = 0, theta = 0;
      var val = BinomialTree.P(OptionStyle.American, OptionType.Call, 0, 0, 0.05, 0.04, 0.0050, 0, 0.4, 0,
        ref delta, ref gamma, ref theta);
      Assert.AreEqual( 0.01, val, 1E-15, "In the money call option on exercise date did not return the correct payoff");
      Assert.AreEqual(1.0, delta, 1E-15, "In the money call option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, 1E-15, "In the money call option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, 1E-15, "In the money call option on exercise date did not return the correct theta");
    }

    [Test]
    public void ExerciseInTheMoneyPutS()
    {
      double delta = 0, gamma = 0, theta = 0;
      var val = BinomialTree.P(OptionStyle.American, OptionType.Put, 0, 0, 0.03, 0.04, 0.0050, 0, 0.4, 0,
        ref delta, ref gamma, ref theta);
      Assert.AreEqual( 0.01, val, 1E-15, "In the money put option on exercise date did not return the correct payoff");
      Assert.AreEqual(-1.0, delta, 1E-15, "In the money put option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, 1E-15, "In the money put option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, 1E-15, "In the money put option on exercise date did not return the correct theta");
    }

    [Test]
    public void ExerciseOutOfTheMoneyCallS()
    {
      double delta = 0, gamma = 0, theta = 0;
      var val = BinomialTree.P(OptionStyle.American, OptionType.Call, 0, 0, 0.03, 0.04, 0.0050, 0, 0.4, 0,
        ref delta, ref gamma, ref theta);
      Assert.AreEqual( 0, val, "Out of the money call option on exercise date did not return zero payoff");
      Assert.AreEqual(0.0, delta, "Out of the money call option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, "Out of the money call option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, "Out of the money call option on exercise date did not return the correct theta");
    }

    [Test]
    public void ExerciseOutOfTheMoneyPutS()
    {
      double delta = 0, gamma = 0, theta = 0;
      var val = BinomialTree.P(OptionStyle.American, OptionType.Put, 0, 0, 0.05, 0.04, 0.0050, 0, 0.4, 0,
        ref delta, ref gamma, ref theta);
      Assert.AreEqual( 0, val, "Out of the money put option on exercise date did not return zero payoff");
      Assert.AreEqual(0.0, delta, "Out of the money put option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, "Out of the money put option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, "Out of the money put option on exercise date did not return the correct theta");
    }
  }
}
