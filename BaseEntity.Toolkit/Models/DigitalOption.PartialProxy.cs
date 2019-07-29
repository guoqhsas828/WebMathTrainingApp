/*
 * DigitalOption.PartialProxy.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Models
{
  public abstract class DigitalOption : Native.DigitalOption
  {
    /// <summary>
    /// Calculates the digital option delta.
    /// </summary>
    /// <param name="style">The style</param>
    /// <param name="type">The type</param>
    /// <param name="digitalType">Type of the digital</param>
    /// <param name="S">The S</param>
    /// <param name="K">The K</param>
    /// <param name="r">Risk free rate</param>
    /// <param name="d">Dividend rate</param>
    /// <param name="vol">The vol</param>
    /// <param name="T">The T</param>
    /// <param name="C">The C</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public static double Delta(
      OptionStyle style,
      OptionType type,
      OptionDigitalType digitalType,
      double T,
      double S,
      double K,
      double r,
      double d,
      double vol,
      double C)
    {
      var pu = P(style, type, digitalType, T, S + 0.0001, K, r, d, vol, C);
      var pd = P(style, type, digitalType, T, S - 0.0001, K, r, d, vol, C);
      return (pu - pd) / (2 * 0.0001);
    }

    /// <summary>
    /// Calculates the digital option gamma.
    /// </summary>
    /// <param name="style">The style</param>
    /// <param name="type">The type</param>
    /// <param name="digitalType">Type of the digital</param>
    /// <param name="S">The S</param>
    /// <param name="K">The K</param>
    /// <param name="r">Risk free rate</param>
    /// <param name="d">Dividend rate</param>
    /// <param name="vol">The vol</param>
    /// <param name="T">The T</param>
    /// <param name="C">The C</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public static double Gamma(
      OptionStyle style,
      OptionType type,
      OptionDigitalType digitalType,
      double T,
      double S,
      double K,
      double r,
      double d,
      double vol,
      double C)
    {
      var p = P(style, type, digitalType, T, S, K, r, d, vol, C);
      var pu = P(style, type, digitalType, T, S + 0.0001, K, r, d, vol, C);
      var pd = P(style, type, digitalType, T, S - 0.0001, K, r, d, vol, C);
      return (pu - 2.0*p + pd)/(0.0001*0.0001);
    }

    /// <summary>
    /// Calculates the digital option vega.
    /// </summary>
    /// <param name="style">The style</param>
    /// <param name="type">The type</param>
    /// <param name="digitalType">Type of the digital.</param>
    /// <param name="S">The S</param>
    /// <param name="K">The K</param>
    /// <param name="r">Risk free rate</param>
    /// <param name="d">Dividend rate</param>
    /// <param name="vol">The vol</param>
    /// <param name="T">The T</param>
    /// <param name="C">The C</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public static double Vega(
      OptionStyle style,
      OptionType type,
      OptionDigitalType digitalType,
      double T,
      double S,
      double K,
      double r,
      double d,
      double vol,
      double C)
    {
      var pu = P(style, type, digitalType, T, S, K, r, d, vol + 0.01, C);
      var pd = P(style, type, digitalType, T, S, K, r, d, vol, C);
      return (pu - pd) / (0.01);

    }

    /// <summary>
    /// Calculates the digital option vanna.
    /// </summary>
    /// <param name="style">The style.</param>
    /// <param name="type">The type.</param>
    /// <param name="digitalType">Type of the digital.</param>
    /// <param name="S">The S.</param>
    /// <param name="K">The K.</param>
    /// <param name="rd">The rd.</param>
    /// <param name="rf">The rf.</param>
    /// <param name="vol">The vol.</param>
    /// <param name="T">The T.</param>
    /// <param name="C">The C.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public static double Vanna(
      OptionStyle style,
      OptionType type,
      OptionDigitalType digitalType,
      double T,
      double S,
      double K,
      double rd,
      double rf,
      double vol,
      double C)
    {
      var pu = Vega(style, type, digitalType, S + 0.0001, K, rd, rf, vol, T, C);
      var pd = Vega(style, type, digitalType, S - 0.0001, K, rd, rf, vol, T, C);
      return (pu - pd) / (2 * 0.0001);
    }

    /// <summary>
    /// Calculates the digital option volga.
    /// </summary>
    /// <param name="style">The style.</param>
    /// <param name="type">The type.</param>
    /// <param name="digitalType">Type of the digital.</param>
    /// <param name="S">The S.</param>
    /// <param name="K">The K.</param>
    /// <param name="r">The rd.</param>
    /// <param name="d">The rf.</param>
    /// <param name="vol">The vol.</param>
    /// <param name="T">The T.</param>
    /// <param name="C">The C.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public static double Volga(
      OptionStyle style,
      OptionType type,
      OptionDigitalType digitalType,
      double T,
      double S,
      double K,
      double r,
      double d,
      double vol,
      double C)
    {
      var pu = Vega(style, type, digitalType, S, K, r, d, vol + 0.01, T, C);
      var pd = Vega(style, type, digitalType, S, K, r, d, vol, T, C);
      return (pu - pd) / (0.01);
    }

    /// <summary>
    /// Implies the volatility from price.
    /// </summary>
    /// <param name="style">The style.</param>
    /// <param name="type">The type.</param>
    /// <param name="digitalType">Type of the digital.</param>
    /// <param name="T">The T.</param>
    /// <param name="S">The S.</param>
    /// <param name="K">The K.</param>
    /// <param name="r">The r</param>
    /// <param name="d">The d</param>
    /// <param name="price">The price (unit 1).</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public static double ImpliedVolatility(      
      OptionStyle style,
      OptionType type,
      OptionDigitalType digitalType,
      double T,
      double S,
      double K,
      double r,
      double d,
      double price)
    {
      double vol;
      // Target function
      Double_Double_Fn fn = delegate(double v, out string msg)
                              {
                                msg = null;
                                double val = double.NaN;
                                try
                                {
                                  val = P(style, type, digitalType, T, S, K, r, d, v, 1.0);
                                }
                                catch (Exception ex)
                                {
                                  msg = ex.ToString();
                                }
                                return val;
                              };

      // Setup solver
      SolverFn f = new DelegateSolverFn(fn, null);
      Brent2 solver = new Brent2();
      solver.setLowerBounds(0);
      solver.setLowerBracket(0.2);
      solver.setUpperBracket(0.8);

      try
      {
        // Solve
        vol = solver.solve(f, price);
      }
      catch (SolverException)
      {
        vol = double.NaN;
      }

      // Done
      return vol;
    }
  }
}
