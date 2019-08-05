//
// Test unit for BlackScholes model
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using DividendType = BaseEntity.Toolkit.Base.DividendSchedule.DividendType;

namespace BaseEntity.Toolkit.Tests.Models
{

  [TestFixture(Category="Models")]
  public class TestBlackScholesModel
  {
    #region Tests

    /// <summary>
    /// Tests BlackScholes.P()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 12.274 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 3.745 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 6.119 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 7.59 )]
    public void P(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.P(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.001,
        string.Format("P() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
    }

    /// <summary>
    /// Tests BlackScholes.Delta()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0.716 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, -0.2848 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0.506 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, -0.494 )]
    public void Delta(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Delta(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.001,
        string.Format("Delta() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(delta, res, 1E-10,
                      string.Format("P() and Delta() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, delta));
      // Test relative to manual calculation
      double nres = BlackScholes.P(style, type, T, S+0.5, K, r, d, v) - BlackScholes.P(style, type, T, S-0.5, K, r, d, v);
      Assert.AreEqual(nres, res, 1E-2,
        string.Format("Delta() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Gamma()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0.018 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0.018 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0.023 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0.023 )]
    public void Gamma(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Gamma(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.001,
        string.Format("Gamma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(gamma, res, 1E-10,
                      string.Format("P() and Gamma() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, gamma));
      // Test relative to manual calculation
      double nres = BlackScholes.Delta(style, type, T, S+0.5, K, r, d, v) - BlackScholes.Delta(style, type, T, S-0.5, K, r, d, v);
      Assert.AreEqual(nres, res, 0.001,
        string.Format("Gamma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Theta()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, -0.0149*365.25 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, -0.0055*365.25)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, -0.0133*365.25)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, -0.0039*365.25)]
    public void Theta(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Theta(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.05,
        string.Format("Theta() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(theta, res, 1E-10,
                      string.Format("P() and Theta() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, theta));
      // Test relative to manual calculation
      double nres = (BlackScholes.P(style, type, T-0.01/365.25, S, K, r, d, v) - BlackScholes.P(style, type, T, S, K, r, d, v))*36525;
      Assert.AreEqual(nres, res, 0.001,
        string.Format("Theta() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Vega()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 32.4 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 32.4 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 33.9 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 33.9 )]
    public void Vega(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Vega(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.2,
        string.Format("Vega() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(vega, res, 1E-8,
                      string.Format("P() and Vega() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, vega));
      // Test relative to manual calculation
      double nres = (BlackScholes.P(style, type, T, S, K, r, d, v+0.005) - BlackScholes.P(style, type, T, S, K, r, d, v-0.005))*100.0;
      Assert.AreEqual(nres, res, 0.2,
        string.Format("Vega() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Rho()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 55.7 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, -30.8 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 36.8 )]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, -49.7 )]
    public void Rho(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Rho(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.1,
        string.Format("Rho() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(rho, res, 1E-8,
                      string.Format("P() and Rho() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, rho));
      // Test relative to manual calculation
      double nres = (BlackScholes.P(style, type, T, S, K, r+0.0005, d, v) - BlackScholes.P(style, type, T, S, K, r-0.0005, d, v))*1000.0;
      Assert.AreEqual(nres, res, 1E-4,
        string.Format("Rho() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Lambda()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0.715 * 95 / 12.261)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, -0.2846 * 95 / 3.752)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0.506 * 85 / 6.119)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, -0.494 * 85 / 7.59)]
    public void Lambda(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Lambda(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.01,
        string.Format("Lambda() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(lambda, res, 1E-10,
                      string.Format("P() and Lambda() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, lambda));
      // Test relative to manual calculation
      double nres = BlackScholes.Delta(style, type, T, S, K, r, d, v) * S / BlackScholes.P(style, type, T, S, K, r, d, v);
      Assert.AreEqual(nres, res, 0.01,
        string.Format("Lambda() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Gearing()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 7.73993808)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 25.36715621)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 13.89115869)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 11.19894598)]
    public void Gearing(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Gearing(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.01,
        string.Format("Gearing() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(gearing, res, 1E-10,
                      string.Format("P() and Gearing() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, gearing));
      // Test relative to manual calculation
      double nres = S / BlackScholes.P(style, type, T, S, K, r, d, v);
      Assert.AreEqual(nres, res, 0.01,
        string.Format("Gearing() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.StrikeGearing()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 1.0/(12.274/90.0))]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 1.0/(3.745/90.0))]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 1.0/(6.119/90.0))]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 1.0/(7.59/90.0))]
    public void StrikeGearing(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.StrikeGearing(style, type, T, S, K, r, d, v);
      Assert.AreEqual(expected, res, 0.01,
        string.Format("StrikeGearing() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(strikeGearing, res, 1E-10,
                      string.Format("P() and StrikeGearing() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, strikeGearing));
      // Test relative to manual calculation
      double nres = K / BlackScholes.P(style, type, T, S, K, r, d, v);
      Assert.AreEqual(nres, res, 0.01,
        string.Format("StrikeGearing() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Vanna()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void Vanna(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Vanna(style, type, T, S, K, r, d, v);
      if (expected != 0)
        Assert.AreEqual(expected, res, 1E-4,
        string.Format("Vanna() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(vanna, res, 1E-10,
                      string.Format("P() and Vanna() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, vanna));
      // Test relative to manual calculation
      double nres = (BlackScholes.Delta(style, type, T, S, K, r, d, v + 0.0005) - BlackScholes.Delta(style, type, T, S, K, r, d, v - 0.0005))*1000;
      Assert.AreEqual(nres, res, 1E-4,
        string.Format("Vanna() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Charm()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void Charm(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Charm(style, type, T, S, K, r, d, v);
      if (expected != 0)
        Assert.AreEqual(expected, res, 1E-4,
        string.Format("Charm() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(charm, res, 1E-10,
                      string.Format("P() and Charm() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, charm));
      // Test relative to manual calculation
      double nres = (BlackScholes.Delta(style, type, T+0.1/365.25, S, K, r, d, v) - BlackScholes.Delta(style, type, T, S, K, r, d, v))*3652.5;
      
      Assert.AreEqual(nres, res, 1E-4,
        string.Format("Charm() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Speed()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void Speed(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Speed(style, type, T, S, K, r, d, v);
      if (expected != 0)
        Assert.AreEqual(expected, res, 1E-4,
        string.Format("Speed() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(speed, res, 1E-10,
                      string.Format("P() and Speed() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, speed));
      // Test relative to manual calculation
      double nres = BlackScholes.Gamma(style, type, T, S+0.5, K, r, d, v) - BlackScholes.Gamma(style, type, T, S-0.5, K, r, d, v);
      Assert.AreEqual(nres, res, 1E-2,
        string.Format("Speed() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Zomma()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void Zomma(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Zomma(style, type, T, S, K, r, d, v);
      if (expected != 0)
        Assert.AreEqual(expected, res, 1E-4,
        string.Format("Zomma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(zomma, res, 1E-10,
                      string.Format("P() and Zomma() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, zomma));
      // Test relative to manual calculation
      double nres = (BlackScholes.Gamma(style, type, T, S, K, r, d, v+0.005) - BlackScholes.Gamma(style, type, T, S, K, r, d, v-0.005))*100.0;
      Assert.AreEqual(nres, res, 1E-4,
        string.Format("Zomma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Color()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void Color(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Color(style, type, T, S, K, r, d, v);
      if (expected != 0)
        Assert.AreEqual(expected, res, 1E-4,
        string.Format("Color() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(color, res, 1E-10,
                      string.Format("P() and Color() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, color));
      // Test relative to manual calculation
      double nres = (BlackScholes.Gamma(style, type, T+1.0/365.25, S, K, r, d, v) - BlackScholes.Gamma(style, type, T, S, K, r, d, v))*365.25;
      Assert.AreEqual(nres, res, 1E-4,
        string.Format("Color() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.Vomma()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void Vomma(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.Vomma(style, type, T, S, K, r, d, v);
      if (expected != 0)
        Assert.AreEqual(expected, res, 1E-4,
        string.Format("Vomma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(vomma, res, 1E-10,
                      string.Format("P() and Vomma() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, vomma));
      // Test relative to manual calculation
      double nres = (BlackScholes.Vega(style, type, T, S, K, r, d, v+0.005) - BlackScholes.Vega(style, type, T, S, K, r, d, v-0.005))*100.0;
      Assert.AreEqual(nres, res, 0.1,
        string.Format("Vomma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.DualDelta()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void DualDelta(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.DualDelta(style, type, T, S, K, r, d, v);
      if (expected != 0)
        Assert.AreEqual(expected, res, 1E-4,
        string.Format("DualDelta() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(dualDelta, res, 1E-10,
                      string.Format("P() and DualDelta() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, dualDelta));
      // Test relative to manual calculation
      double nres = BlackScholes.P(style, type, T, S, K+0.5, r, d, v) - BlackScholes.P(style, type, T, S, K-0.5, r, d, v);
      Assert.AreEqual(nres, res, 1E-4,
        string.Format("DualDelta() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    /// <summary>
    /// Tests BlackScholes.DualGamma()
    /// </summary>
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 95, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Call, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    [NUnit.Framework.TestCase(OptionStyle.European, OptionType.Put, 1.0, 85, 90, 0.04, 0, 0.2, 0)]
    public void DualGamma(OptionStyle style, OptionType type, double T, double S, double K, double r, double d, double v, double expected)
    {
      double res = BlackScholes.DualGamma(style, type, T, S, K, r, d, v);
      if( expected != 0 )
      Assert.AreEqual(expected, res, 1E-4,
        string.Format("DualGamma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, expected));
      // Test consistency with P()
      double delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0,
        zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      BlackScholes.P(style, type, T, S, K, r, d, v, ref delta, ref gamma, ref theta, ref vega, ref rho,
        ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma, ref color, ref vomma, ref dualDelta, ref dualGamma);
      Assert.AreEqual(dualGamma, res, 1E-10,
                      string.Format("P() and DualGamma() not consistent for for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} was {8}, expected {9}",
                        style, type, T, S, K, r, d, v, res, dualGamma));
      // Test relative to manual calculation
      double nres = BlackScholes.DualDelta(style, type, T, S, K+0.5, r, d, v) - BlackScholes.DualDelta(style, type, T, S, K-0.5, r, d, v);
      Assert.AreEqual(nres, res, 1E-2,
        string.Format("DualGamma() for style={0}, type={1}, T={2}, S={3}, K={4}, r={5}, d={6}, v={7} not consistent with numerical calulation. Was {8}, expected {9}",
        style, type, T, S, K, r, d, v, res, nres));
    }

    #endregion Tests

    #region Behavior on the exercise date
    [Test]
    public void ExerciseInTheMoneyCall()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      double r = 0.0, la = 0.0, ge = 0.0, kge = 0.0, va = 0.0, ch = 0.0,
       sp = 0.0, zo = 0.0, co = 0.0, vo = 0.0, dd = 0.0, dg = 0.0;
      var val = BlackScholes.P(OptionStyle.European, OptionType.Call, 0.0, 0.05, 0.04, 0.05, 0.0, 0.4,
        ref delta, ref gamma, ref theta, ref vega, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo,
        ref co, ref vo, ref dd, ref dg);
      Assert.AreEqual( 0.01, val, 1E-15, "In the money call option on exercise date did not return the correct payoff");
      Assert.AreEqual(1.0, delta, 1E-15, "In the money call option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, 1E-15, "In the money call option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, 1E-15, "In the money call option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, 1E-15, "In the money call option on exercise date did not return the correct vega");
    }

    [Test]
    public void ExerciseInTheMoneyPut()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      double r = 0.0, la = 0.0, ge = 0.0, kge = 0.0, va = 0.0, ch = 0.0,
       sp = 0.0, zo = 0.0, co = 0.0, vo = 0.0, dd = 0.0, dg = 0.0;
      var val = BlackScholes.P(OptionStyle.European, OptionType.Put, 0, 0.03, 0.04, 0.05, 0, 0.4,
        ref delta, ref gamma, ref theta, ref vega, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo,
        ref co, ref vo, ref dd, ref dg);
      Assert.AreEqual( 0.01, val, 1E-15, "In the money put option on exercise date did not return the correct payoff");
      Assert.AreEqual(-1.0, delta, 1E-15, "In the money put option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, 1E-15, "In the money put option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, 1E-15, "In the money put option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, 1E-15, "In the money put option on exercise date did not return the correct vega");
    }

    [Test]
    public void ExerciseOutOfTheMoneyCall()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      double r = 0.0, la = 0.0, ge = 0.0, kge = 0.0, va = 0.0, ch = 0.0,
       sp = 0.0, zo = 0.0, co = 0.0, vo = 0.0, dd = 0.0, dg = 0.0;
      var val = BlackScholes.P(OptionStyle.European, OptionType.Call, 0, 0.03, 0.04, 0.05, 0, 0.4,
        ref delta, ref gamma, ref theta, ref vega, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo,
        ref co, ref vo, ref dd, ref dg);
      Assert.AreEqual( 0, val, "Out of the money call option on exercise date did not return zero payoff");
      Assert.AreEqual(0.0, delta, "Out of the money call option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, "Out of the money call option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, "Out of the money call option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, "Out of the money call option on exercise date did not return the correct vega");
    }

    [Test]
    public void ExerciseOutOfTheMoneyPut()
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0;
      double r = 0.0, la = 0.0, ge = 0.0, kge = 0.0, va = 0.0, ch = 0.0,
       sp = 0.0, zo = 0.0, co = 0.0, vo = 0.0, dd = 0.0, dg = 0.0;
      var val = BlackScholes.P(OptionStyle.European, OptionType.Put, 0, 0.05, 0.04, 0.05, 0, 0.4,
        ref delta, ref gamma, ref theta, ref vega, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo,
        ref co, ref vo, ref dd, ref dg);
      Assert.AreEqual( 0, val, "Out of the money put option on exercise date did not return zero payoff");
      Assert.AreEqual(0.0, delta, "Out of the money put option on exercise date did not return the correct delta");
      Assert.AreEqual(0.0, gamma, "Out of the money put option on exercise date did not return the correct gamma");
      Assert.AreEqual(0.0, theta, "Out of the money put option on exercise date did not return the correct theta");
      Assert.AreEqual(0.0, vega, "Out of the money put option on exercise date did not return the correct vega");
    }
    #endregion

    #region With divident schedule

    #region Data
    class BsData
    {
      public readonly Dt Today, Expiry;
      public readonly double Spot, Strike, Rate, Dividend, Volatility, Time;
      public readonly DividendSchedule DividendSchedule;
      public BsData(Dt asOf, Dt expiry, double S, double K,
        double r, double d, DividendSchedule ds, double v, double T)
      {
        Today = asOf;
        Expiry = expiry;
        Time = T;
        Spot = S;
        Strike = K;
        Rate = r;
        Dividend = d;
        DividendSchedule = ds;
        Volatility = v;
      }
    }

    private BsData _bsData;
    private BsData GetInputData()
    {
      if (_bsData == null)
      {
        // The input data
        Dt asOf = new Dt(21, 6, 2013);
        Dt date1 = asOf + (RelativeTime)0.25;
        Dt date2 = asOf + (RelativeTime)0.5;
        var ds = new DividendSchedule(asOf, new[]
        {
          Tuple.Create(date1, DividendType.Fixed, 2.0),
          Tuple.Create(date2, DividendType.Fixed, 2.0),
        });
        double S = 100, K = 100, r = 0.05, T = 7 / 12.0, v = 0.3;

        // Round trip of the rate with discount curve
        Dt expiry = asOf + (RelativeTime)T;

        // Calculate the divident yield.
        var pv = ds.PresentValue(S, r, T);
        var d = RateCalc.RateFromPrice(1 - pv / S, asOf, expiry);

        _bsData = new BsData(asOf, expiry, S, K, r, d, ds, v, T);
      }
      return _bsData;
    }
    #endregion

    [Test]
    public void TestDividentSchedule()
    {
      var bs = GetInputData();

      // The input data
      Dt asOf = bs.Today;
      Dt expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate, v = bs.Volatility;

      // Round trip of the rate with discount curve
      var discountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(r);
      var rate = RateCalc.Rate(discountCurve, asOf, expiry);
      Assert.AreEqual(r, rate, 2E-16, "Rate");

      // Divident pv with and without curve
      var T = bs.Time;
      var ds = bs.DividendSchedule;
      var pv1 = ds.PresentValue(S, rate, T);
      var pv2 = ds.Pv(asOf, expiry, S, discountCurve);
      Assert.AreEqual(pv1, pv2, 5E-16, "DividentPv");

      // Call price with divident schedule.
      var cp = BlackScholes.P(OptionStyle.European, OptionType.Call, T, S, K, r, 0, ds, v);
      Assert.AreEqual(8.29511, cp, 1E-5, "Price");

      // Call value with divident yield.
      var d = RateCalc.RateFromPrice(1 - pv1 / S, asOf, expiry);
      var cp0 = BlackScholes.P(OptionStyle.European, OptionType.Call, T, S, K, r, d, v);
      Assert.AreEqual(cp, cp0, 1E-17, "PriceFromYield");
      
      //var term = new StockVolatilityTerm(asOf, S, ds, discountCurve) as IVolatilityTerm;
      //var bs = BlackScholesSurfaceBuilder.GetParameters(asOf, expiry,
      //  OptionHelper.Time, term.Spot, term.Curve1, term.Curve2);
      //var call = BlackScholes.P(OptionStyle.European, OptionType.Call,
      //  bs.Time, bs.Spot, K, bs.Rate2, bs.Rate1, v);

      //var ap = BlackScholes.P(OptionStyle.American, OptionType.Call, T, S, K, r, 0, ds, v);
      //Assert.Greater(ap, cp);
    }

    [Test]
    public void TestBondFutureOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate;
      double v = bs.Volatility, T = bs.Time;

      var option =  new BondFutureOption(expiry, 1.0, 1.0, expiry,
        OptionType.Call, OptionStyle.European, K);
      var pricer = new BondFutureOptionBlackPricer(option, asOf, asOf, S, r, v, 1.0);
      var maturity = Dt.Add(expiry, 1, TimeUnit.Years);
      var coupon = (S / 100 * Math.Exp(r * Dt.RelativeTime(asOf, maturity)) - 1)
        / Dt.Fraction(expiry, maturity, DayCount.Actual365Fixed);
      pricer.CtdBond = new Bond(expiry, maturity,
        Currency.USD, BondType.None, coupon, DayCount.Actual365Fixed,
        CycleRule.None, Frequency.Annual, BDConvention.None, Calendar.None);
      pricer.CtdConversionFactor = 1.0;
      pricer.FuturesModelBasis = pricer.FuturesModelPrice() - S;
      var pv = pricer.ProductPv();
      var expect = BlackScholes.P(OptionStyle.European, OptionType.Call,
        T, S, K, r, r, v);
      Assert.AreEqual(expect, pv, 1E-17);
    }

    [Test]
    public void TestCommdityForwardOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate;
      double v = bs.Volatility, T = bs.Time;

      var option = new CommodityForwardOption(expiry, expiry, expiry,
        OptionType.Call, OptionStyle.European, K);
      var leaseRate = r;
      var pricer = new CommodityForwardOptionBlackPricer(option, asOf, asOf,
        r, leaseRate, CalibratedVolatilitySurface.FromFlatVolatility(asOf, v),
        S, 1.0);
      var pv = pricer.ProductPv();
      var expect = BlackScholes.P(OptionStyle.European, OptionType.Call,
        T, S, K, r, r, v);
      Assert.AreEqual(expect, pv, 1E-17);
    }

    [Test]
    public void TestCommdityFutureOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate;
      double v = bs.Volatility, T = bs.Time;

      var option = new CommodityFutureOption(expiry, 1.0, expiry, OptionType.Call, OptionStyle.European, K);
      var pricer = new CommodityFutureOptionBlackPricer(option, asOf, asOf, S, r, v, 1.0);
      var pv = pricer.ProductPv();
      var expect = BlackScholes.P(OptionStyle.European, OptionType.Call,
        T, S, K, r, r, v);
      Assert.AreEqual(expect, pv, 1E-17);
    }

    [Test]
    public void TestCommdityOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate;
      double v = bs.Volatility, T = bs.Time;

      var option = new CommodityOption(expiry,OptionType.Call, OptionStyle.European, K);
      var leaseRate = r;
      var pricer = new CommodityOptionPricer(option, asOf, asOf, S, leaseRate, r, v, 1.0);
      var pv = pricer.ProductPv();
      var expect = BlackScholes.P(OptionStyle.European, OptionType.Call,
        T, S, K, r, r, v);
      Assert.AreEqual(expect, pv, 1E-17);
    }

    [Test]
    public void TestFxForwardOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate;
      double v = bs.Volatility, T = bs.Time;

      var option = new FxForwardOption(expiry, Currency.USD, Currency.EUR,
        1.0, expiry, OptionType.Call, OptionStyle.European, K);
      var pricer = new FxForwardOptionBlackPricer(option, asOf, asOf,
        Currency.USD, new DiscountCurve(asOf).SetRelativeTimeRate(r),
        null, null, null,1.0);
      pricer.Volatility = v;
      pricer.UnderlyingPrice = S;
      var pv = pricer.ProductPv();
      var expect = BlackScholes.P(OptionStyle.European, OptionType.Call,
        T, S, K, r, r, v);
      Assert.AreEqual(expect, pv, 1E-17);
    }

    [Test]
    public void TestFxFutureOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate;
      double v = bs.Volatility, T = bs.Time;

      var option = new FxFutureOption(Currency.USD, Currency.JPY, 
        expiry, 1.0, expiry, OptionType.Call, OptionStyle.European, K)
      {
        Ccy = Currency.JPY
      };
      var pricer = new FxFutureOptionBlackPricer(option, asOf, asOf,
        new DiscountCurve(asOf).SetRelativeTimeRate(r),
        new FxCurve(new FxRate(asOf, asOf, Currency.USD, Currency.JPY, S),
          new DiscountCurve(asOf, 0.0)),
        CalibratedVolatilitySurface.FromFlatVolatility(asOf, v), 1.0);
      var pv = pricer.ProductPv();
      var expect = BlackScholes.P(OptionStyle.European, OptionType.Call,
        T, S, K, r, r, v);
      Assert.AreEqual(expect, pv, 1E-17);
    }

    [Test]
    public void TestStockFutureOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate;
      double v = bs.Volatility, T = bs.Time;

      var option = new StockFutureOption(expiry, 1.0, expiry,
        OptionType.Call, OptionStyle.European, K);
      var pricer = new StockFutureOptionBlackPricer(option, asOf, asOf, S, r, v);
      var pv = pricer.ProductPv();
      var expect = BlackScholes.P(OptionStyle.European, OptionType.Call,
        T, S, K, r, r, v);
      Assert.AreEqual(expect, pv, 1E-17);
    }

    [Test]
    public void TestStockOption()
    {
      var bs = GetInputData();
      Dt asOf = bs.Today, expiry = bs.Expiry;
      double S = bs.Spot, K = bs.Strike, r = bs.Rate, d = bs.Dividend;
      var ds = bs.DividendSchedule;
      double v = bs.Volatility, T = bs.Time;

      double expectPv = BlackScholes.P(OptionStyle.European,
        OptionType.Call, T, S, K, r, 0, ds, v);

      var stock = Stock.GetStockWithConvertedDividend(Currency.None, null, ds);
      var so = new StockOption(stock, expiry, OptionType.Call, OptionStyle.European, K);
      var sop = new StockOptionPricer(so, asOf, asOf, S, r, 0.0, v);
      var pv = sop.ProductPv();
      Assert.AreEqual(expectPv, pv, 1E-14, "Pv.ds");
      so = new StockOption(expiry, OptionType.Call, OptionStyle.European, K);
      sop = new StockOptionPricer(so, asOf, asOf, S, r, d, v);
      pv = sop.ProductPv();
      Assert.AreEqual(expectPv, pv, 1E-17, "Pv.d");
    }

    #endregion
  }
}
