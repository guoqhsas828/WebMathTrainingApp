//
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  /// Test Black model with normal volatility assumption.
  /// </summary>
  [TestFixture]
  public class TestBlackNormal : ToolkitTestBase
  {
    [Test]
    public void ExpiredTest()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        var pv = BlackNormal.P(OptionType.Call, -0.1, 0, 0.05, 0.06, 0.0050);
        Assert.Greater(pv, 0, "PV Calculation should have throw an exception!");
      });
    }

    [Test]
    public void ExerciseInTheMoneyCall()
    {
      var val = BlackNormal.P(OptionType.Call, 0, 0, 0.05, 0.04, 0.0050);
      Assert.AreEqual(0.01, val, 1E-15, "In the money call option on exercise date did not return the correct payoff");
    }

    [Test]
    public void ExerciseInTheMoneyPut()
    {
      var val = BlackNormal.P(OptionType.Put, 0, 0, 0.03, 0.04, 0.0050);
      Assert.AreEqual( 0.01, val, 1E-15, "In the money put option on exercise date did not return the correct payoff");
    }

    [Test]
    public void ExerciseOutOfTheMoneyCall()
    {
      var val = BlackNormal.P(OptionType.Call, 0, 0, 0.03, 0.04, 0.0050);
      Assert.AreEqual(0, val, "In the money call option on exercise date did not return zero payoff");
    }

    [Test]
    public void ExerciseOutOfTheMoneyPut()
    {
      var val = BlackNormal.P(OptionType.Put, 0, 0, 0.05, 0.04, 0.0050);
      Assert.AreEqual(0, val, "In the money put option on exercise date did not return zero payoff");
    }

    [Test]
    public void ExerciseInTheMoneyCallS()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      var val = BlackNormal.P(OptionType.Call, 0, 0, 0.05, 0.04, 0.0050,
        ref delta, ref gamma, ref theta, ref vega);
      Assert.AreEqual( 0.01, val, 1E-15, "In the money call option on exercise date did not return the correct payoff");
      Assert.AreEqual( 1.0, delta, 1E-15, "In the money call option on exercise date did not return the correct delta");
      Assert.AreEqual( 0.0, gamma, 1E-15, "In the money call option on exercise date did not return the correct gamma");
      Assert.AreEqual( 0.0, theta, 1E-15, "In the money call option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, 1E-15, "In the money call option on exercise date did not return the correct vega");
    }

    [Test]
    public void ExerciseInTheMoneyPutS()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      var val = BlackNormal.P(OptionType.Put, 0, 0, 0.03, 0.04, 0.0050,
        ref delta, ref gamma, ref theta, ref vega);
      Assert.AreEqual( 0.01, val, 1E-15, "In the money put option on exercise date did not return the correct payoff");
      Assert.AreEqual(-1.0, delta, 1E-15, "In the money put option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, 1E-15, "In the money put option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, 1E-15, "In the money put option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, 1E-15, "In the money put option on exercise date did not return the correct vega");
    }

    [Test]
    public void ExerciseOutOfTheMoneyCallS()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      var val = BlackNormal.P(OptionType.Call, 0, 0, 0.03, 0.04, 0.0050,
        ref delta, ref gamma, ref theta, ref vega);
      Assert.AreEqual( 0, val, "Out of the money call option on exercise date did not return zero payoff");
      Assert.AreEqual(0.0, delta, "Out of the money call option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, "Out of the money call option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, "Out of the money call option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, "Out of the money call option on exercise date did not return the correct vega");
    }

    [Test]
    public void ExerciseOutOfTheMoneyPutS()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      var val = BlackNormal.P(OptionType.Put, 0, 0, 0.05, 0.04, 0.0050,
        ref delta, ref gamma, ref theta, ref vega);
      Assert.AreEqual( 0, val, "Out of the money put option on exercise date did not return zero payoff");
      Assert.AreEqual(0.0, delta, "Out of the money put option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, "Out of the money put option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, "Out of the money put option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, "Out of the money put option on exercise date did not return the correct vega");
    }

    [Test]
    public void ImpliedVolatility()
    {
      var pv = BlackNormal.P(OptionType.Call, 3, 0, 0.05, 0.035, 0.0060);
      var vol = BlackNormal.ImpliedVolatility(OptionType.Call, 3, 0, 0.05, 0.035, pv);
      Assert.AreEqual(0.006, vol, 1e-8, "Implied volatility was incorrect");
    }
  }
}
