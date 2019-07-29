/*
 *  -2012. All rights reserved.
 */

using System;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Two variables linear regression calculator.
  /// </summary>
  /// <remarks>
  ///   This class calculates the coefficients of the two variables linear regression<math>
  ///     Y_i = \beta_0 + \beta_1 X_{1i} + \beta_2 X_{2i} + \epsilon_i
  ///   </math>
  /// </remarks>
  /// <example>
  ///   Suppose that a list of observation, <c>observations</c>, hold the observed values
  ///   of <m>Y</m>, <m>X_1</m> and <m>X_2</m>.
  ///   <code>
  ///     var result = observations
  ///       .Aggregate(new TwoVariableLinearRegression(),
  ///          (regression, obs) =&gt; regression.Add(obs.Y, obs.X1, obs.X2))
  ///       .GetResult();
  ///     Console.WriteLine("The coefficents are [{0}, {1}, {2}]",
  ///       result.Beta0, result.Beta1, result.Beta2);
  ///   </code>
  /// </example>
  public class TwoVariableLinearRegression
  {
    //Note: this class is intentionally not marked serialization.

    private int _n;
    private double _x1, _x11, _x12, _x2, _x22, _y, _yx1, _yx2, _yy;

    /// <summary>
    ///   Adds an observation <m>(Y_i, X_{1i}, X_{2i})</m>.
    /// </summary>
    /// <param name="y">The observed value of y.</param>
    /// <param name="x1">The observed value of x1.</param>
    /// <param name="x2">The observed value of x2.</param>
    public TwoVariableLinearRegression Add(double y, double x1, double x2)
    {
      _x1 += x1;
      _x11 += x1 * x1;
      _x2 += x2;
      _x22 += x2 * x2;
      _x12 += x1 * x2;
      _y += y;
      _yy += y * y;
      _yx1 += y * x1;
      _yx2 += y * x2;
      ++_n;
      return this;
    }

    /// <summary>
    ///   Calculates the regression coefficients.
    /// </summary>
    /// <remarks>
    ///   Here we solve equation <math>
    ///     \left[\begin{array}{cc} x_{11} &amp; x_{12} \\ x_{12} &amp; x_{22}\end{array}\right]
    ///     \left[\begin{array}{c} \beta_1 \\ \beta_2 \end{array}\right]
    ///     = \left[\begin{array}{c} y_{x1} \\ y_{x2} \end{array}\right]
    ///   </math> where the matrix on the left hand side is the covariance matrix of the two
    ///   independent variables <m>X_1</m> and <m>X_2</m>, while the vector on the right hand side
    ///   is the covariance of <m>Y</m> with <m>X_1</m> and <m>X_2</m>.
    /// </remarks>
    public Result GetResult()
    {
      double b0, b1, b2; // coefficients
      double ess; // explained sum of squares
      int n = _n;
      double x1 = _x1 / n, x2 = _x2 / n, y = _y / n;
      double x11 = _x11 / n - x1 * x1,
        x12 = _x12 / n - x1 * x2,
        x22 = _x22 / n - x2 * x2,
        yx1 = _yx1 / n - y * x1,
        yx2 = _yx2 / n - y * x2,
        yy = _yy / n - y * y;
      const double varianceTolerance = 1E-15;

      // Does x1 have enough variance?
      if (x11 < varianceTolerance)
      {
        if (x22 < varianceTolerance)
        {
          // Variance in strikes too small.
          // A contant function approximate is enough.
          return new Result(y, 0, 0, n, 0, yy);
        }
        b1 = 0;
        b2 = yx2 / x22;
        ess = b2 * yx2;
        b0 = y - b2 * x2;
        return new Result(b0, b1, b2, n, ess, yy);
      }

      // Does x2 have enough variance?
      if (x22 < varianceTolerance)
      {
        // Near perfect correlation in x1 and x2 (over 99.99%).
        // A linear function approximate is enough and more stable.
        b2 = 0;
        b1 = yx1 / x11;
        ess = b1 * yx1;
        b0 = y - b1 * x1;
        return new Result(b0, b1, b2, n, ess, yy);
      }

      // Consider both variables.
      double z1 = x12 / x11, z2 = x12 / x22;
      double det = 1 - z1 * z2;
      if (det < 1E-4)
      {
        // Near perfect correlation in x1 and x2 (over 99.99%).
        // A linear function approximate is enough and more stable.
        b2 = 0;
        b1 = yx1 / x11;
        ess = b1 * yx1;
        b0 = y - b1 * x1;
      }
      else
      {
        //! Easier to solve the transformed equation <math>
        //!   \left[\begin{array}{cc} 1 &amp; z_1 \\ z_2 &amp; 1\end{array}\right]
        //!   \left[\begin{array}{c} b \\ a\end{array}\right]
        //!   = \left[\begin{array}{c} y_{z1} \\ y_{z2} \end{array}\right]
        //! </math>
        double yz1 = yx1 / x11, yz2 = yx2 / x22;
        b2 = (yz2 - yz1 * z2) / det;
        b1 = (yz1 - yz2 * z1) / det;
        ess = b1 * yx1 + b2 * yx2;
        b0 = y - b1 * x1 - b2 * x2;
      }
      return new Result(b0, b1, b2, n, ess, yy);
    }

    #region Nested type: Result

    /// <summary>
    ///   Represent the regression results of the two variables linear regression <math>
    ///     Y_i = \beta_0 + \beta_1 X_{1i} + \beta_2 X_{2i} + \epsilon_i
    ///   </math>
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class Result
    {
      private readonly double _b0, _b1, _b2;
      private readonly int _n;

      internal Result(double b0, double b1, double b2,
        int n, double ess, double yy)
      {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _n = n;
        R2 = ess / yy;
        var rss = yy - ess;
        StandardError = rss < 0 ? 0 : Math.Sqrt(rss);
      }

      /// <summary>
      ///   Gets the coefficient <m>\beta_0</m>.
      /// </summary>
      public double Beta0
      {
        get { return _b0; }
      }

      /// <summary>
      ///   Gets the coefficient <m>\beta_1</m>.
      /// </summary>
      public double Beta1
      {
        get { return _b1; }
      }

      /// <summary>
      ///   Gets the coefficient <m>\beta_2</m>.
      /// </summary>
      public double Beta2
      {
        get { return _b2; }
      }

      /// <summary>
      ///   Gets all the coefficients <m>[\beta_0, \beta_1, \beta_2]</m>.
      /// </summary>
      public double[] Coefficients
      {
        get { return new[] {_b0, _b1, _b2}; }
      }

      /// <summary>
      ///   Gets the number of observations.
      /// </summary>
      public int ObservationCount
      {
        get { return _n; }
      }

      /// <summary>
      ///   Gets the <m>R^2</m>.
      /// </summary>
      public double R2 { get; private set; }

      /// <summary>
      ///   Gets the standard error of regression.
      /// </summary>
      public double StandardError { get; private set; }
    }

    #endregion
  }
}
