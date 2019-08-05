//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Rng;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  /// <summary>
  ///   Test <c>TwoVariableLinearRegression</c> calculator with simulated data.
  /// </summary>
  [TestFixture]
  public class Test2VariablesLinearRegression
  {
    /// <summary>
    ///  The cases that both x1 and x2 vary without correlation.
    /// </summary>
    /// <param name="b0">The coefficient b0.</param>
    /// <param name="b1">The coefficient b1.</param>
    /// <param name="b2">The coefficient b2.</param>
    /// <param name="stderr">The standard error of regression.</param>
    /// <remarks></remarks>
    [TestCase(1.0, 0.0, 0.0, 0.01)]
    [TestCase(1.0, 2.5, 0.0, 0.01)]
    [TestCase(1.0, 0.0, 1.8, 0.01)]
    [TestCase(1.0, 2.5, 1.8, 0.01)]
    [TestCase(1.0, 1.8, 2.5, 0.01)]
    public void Both(double b0, double b1, double b2, double stderr)
    {
      int nobs = 2500;
      var rng = new RandomNumberGenerator();
      Func<double, double, double> f = (x1, x2) => b0 + b1 * x1 + b2 * x2 + rng.Normal(0, stderr);
      TwoVariableLinearRegression.Result result = Enumerable.Range(0, nobs)
        .Select(i => new {X1 = rng.Normal(0, 1), X2 = rng.Normal(0, 1)})
        .Select(d => new {d.X1, d.X2, Y = f(d.X1, d.X2)})
        .Aggregate(new TwoVariableLinearRegression(), (r, d) => r.Add(d.Y, d.X1, d.X2))
        .GetResult();
      double tolerance = 2 * stderr / Math.Sqrt(nobs);
      NUnit.Framework.Assert.AreEqual(/*"b0",*/ b0, result.Beta0, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b1",*/ b1, result.Beta1, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b2",*/ b2, result.Beta2, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"SE",*/ stderr, result.StandardError, tolerance);
    }

    /// <summary>
    ///  The cases that only X1 varies.
    /// </summary>
    /// <param name="b0">The coefficient b0.</param>
    /// <param name="b1">The coefficient b1.</param>
    /// <param name="stderr">The standard error of regression.</param>
    /// <remarks></remarks>
    [TestCase(1.0, 0.0, 0.01)]
    [TestCase(1.0, 2.5, 0.01)]
    [TestCase(1.0, 1.8, 0.01)]
    public void X1Only(double b0, double b1, double stderr)
    {
      int nobs = 2500;
      var rng = new RandomNumberGenerator();
      const double b2 = 0, cx2 = 1.0;
      Func<double, double, double> f = (x1, x2) => b0 + b1 * x1 + b2 * x2 + rng.Normal(0, stderr);
      TwoVariableLinearRegression.Result result = Enumerable.Range(0, nobs)
        .Select(i => new {X1 = rng.Normal(0, 1), X2 = cx2})
        .Select(d => new {d.X1, d.X2, Y = f(d.X1, d.X2)})
        .Aggregate(new TwoVariableLinearRegression(), (r, d) => r.Add(d.Y, d.X1, d.X2))
        .GetResult();
      double tolerance = 2 * stderr / Math.Sqrt(nobs);
      NUnit.Framework.Assert.AreEqual(/*"b0",*/ b0, result.Beta0, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b1",*/ b1, result.Beta1, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b2",*/ b2, result.Beta2, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"SE",*/ stderr, result.StandardError, tolerance);
    }

    /// <summary>
    ///  The case that only X2 varies.
    /// </summary>
    /// <param name="b0">The coefficient b0.</param>
    /// <param name="b2">The coefficient b2.</param>
    /// <param name="stderr">The standard error of regression.</param>
    /// <remarks></remarks>
    [TestCase(1.0, 0.0, 0.01)]
    [TestCase(1.0, 2.5, 0.01)]
    [TestCase(1.0, 1.8, 0.01)]
    public void X2Only(double b0, double b2, double stderr)
    {
      int nobs = 2500;
      var rng = new RandomNumberGenerator();
      const double b1 = 0, cx1 = 1.0;
      Func<double, double, double> f = (x1, x2) => b0 + b1 * x1 + b2 * x2 + rng.Normal(0, stderr);
      TwoVariableLinearRegression.Result result = Enumerable.Range(0, nobs)
        .Select(i => new {X1 = cx1, X2 = rng.Normal(0, 1)})
        .Select(d => new {d.X1, d.X2, Y = f(d.X1, d.X2)})
        .Aggregate(new TwoVariableLinearRegression(), (r, d) => r.Add(d.Y, d.X1, d.X2))
        .GetResult();
      double tolerance = 2 * stderr / Math.Sqrt(nobs);
      NUnit.Framework.Assert.AreEqual(/*"b0",*/ b0, result.Beta0, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b1",*/ b1, result.Beta1, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b2",*/ b2, result.Beta2, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"SE",*/ stderr, result.StandardError, tolerance);
    }

    /// <summary>
    ///  The cases that X1 and X2 are perfectly correlated.
    /// </summary>
    /// <param name="b0">The coefficient b0.</param>
    /// <param name="b1">The coefficient b1.</param>
    /// <param name="stderr">The standard error of regression.</param>
    /// <remarks></remarks>
    [TestCase(1.0, 0.0, 0.01)]
    [TestCase(1.0, 2.5, 0.01)]
    [TestCase(1.0, 1.8, 0.01)]
    public void PerfectCorrelation(double b0, double b1, double stderr)
    {
      int nobs = 2500;
      var rng = new RandomNumberGenerator();
      const double b2 = 0;
      Func<double, double, double> f = (x1, x2) => b0 + b1 * x1 + b2 * x2 + rng.Normal(0, stderr);
      TwoVariableLinearRegression.Result result = Enumerable.Range(0, nobs)
        .Select(i => new {X = rng.Normal(0, 1)})
        .Select(d => new {d.X, Y = f(d.X, d.X)})
        .Aggregate(new TwoVariableLinearRegression(), (r, d) => r.Add(d.Y, d.X, d.X))
        .GetResult();
      double tolerance = 2 * stderr / Math.Sqrt(nobs);
      NUnit.Framework.Assert.AreEqual(/*"b0",*/ b0, result.Beta0, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b1",*/ b1, result.Beta1, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"b2",*/ b2, result.Beta2, tolerance);
      NUnit.Framework.Assert.AreEqual(/*"SE",*/ stderr, result.StandardError, tolerance);
    }
  }
}