//
// VolatilityCurve.cs
//  -2014. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Volatility curve
  /// </summary>
  /// <remarks>
  ///   <para>Contains a term structure of volatilities. The interface is in terms
  ///   of volatilities.</para>
  /// </remarks>
  [Serializable]
  public class VolatilityCurve : CalibratedCurve, IVolatilityCalculatorProvider, IVolatilitySurface
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>The default interpolation is linear between volatilities.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    public VolatilityCurve(Dt asOf)
      : base(new SimpleCalibrator(asOf))
    {
      Frequency = Frequency.None;
      DayCount = DayCount.None;
      var extrap = new Const();
      //this.Interp = new SquareLinearVolatilityInterp(extrap, extrap);
      Interp = new Tension(extrap, extrap);
    }

    /// <summary>
    ///   Constructor for a flat volatility curve
    /// </summary>
    /// <remarks>
    ///   <para>Constructs a simple volatility curve based on single volatility.</para>
    ///   <para>Settlement defaults to asOf date</para>
    ///   <para>Interpolation defaults to linear/constant.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="volatility">Single volatility</param>
    /// <example>
    /// <code language="C#">
    ///   Dt today = Dt.today();       // Pricing is as of today.
    ///   double volatility = 0.20;    // Constant volatility is 20 percent.
    ///
    ///   // Construct the volatility curve using a single volatility
    ///   VolatilityCurve volCurve = new VolatilityCurve( today, volatility );
    /// </code>
    /// </example>
    public VolatilityCurve(Dt asOf, double volatility)
      : this(asOf)
    {
      // Add vol
      AddVolatility(asOf, volatility);
      // Fit curve
      Fit();
    }

    private VolatilityCurve(Native.Curve nativeCurve)
      : base(nativeCurve, new SimpleCalibrator(nativeCurve.GetAsOf()))
    {
    }

    /// <summary>
    ///  Creates a forward volatility curve from the Black volatility curve.
    /// </summary>
    /// <param name="blackVolatilityCurve">The Black volatility curve</param>
    /// <param name="interp">The interpolator.  If null, piecewise flat interpolation is assumed.</param>
    /// <returns>The forward volatility curve</returns>
    /// <remarks>
    ///   This function builds a new forward volatility curve from the current instance, 
    ///   assuming the latter is a Black volatility curve.  The current instance is not
    ///   modified.
    /// </remarks>
    public static VolatilityCurve ToForwardVolatilityCurve(
      Curve blackVolatilityCurve,
      Interp interp = null)
    {
      var vc = new VolatilityCurve(Native.Curve.CreateForwardVolatilityCurve(
        blackVolatilityCurve.NativeCurve, interp ?? new Flat(1E-15)));
      for (int i = 0, n = vc.Count; i < n; ++i)
        vc.AddVolatility(vc.GetDt(i), vc.GetVal(i));
      return vc;
    }

    /// <summary>
    ///  Creates a Black volatility curve from the forward volatility curve.
    /// </summary>
    /// <param name="forwardVolatilityCurve">The forward volatility curve</param>
    /// <param name="interp">The interpolator.  If null, squared interpolation is assumed.</param>
    /// <returns>The Black volatility curve</returns>
    public static VolatilityCurve FromForwardVolatilityCurve(
      Curve forwardVolatilityCurve,
      Interp interp = null)
    {
      var fwd = forwardVolatilityCurve;
      Dt asOf = fwd.AsOf;
      var black = new VolatilityCurve(asOf)
      {
        Interp = interp ?? new SquareLinearVolatilityInterp()
      };
      Dt date0 = asOf;
      double variance = 0;
      for (int i = 0, n = fwd.Count; i < n; ++i)
      {
        Dt date = fwd.GetDt(i);
        variance += Integral(date0, date, fwd, fwd);
        black.AddVolatility(date, Math.Sqrt(variance/(date - asOf)));
        date0 = date;
      }
      black.Fit();
      return black;
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Get rate give date
    /// </summary>
    /// <param name="date">Date to interpolate for</param>
    /// <returns>Vol matching date</returns>
    public double Volatility(Dt date)
    {
      return Interpolate(date);
    }

    /// <summary>
    /// Adds a value to the curve.
    /// </summary>
    /// <param name="date">The date to add.</param>
    /// <param name="vol">The volatility to add.</param>
    public void AddVolatility(Dt date, double vol)
    {
      var pt = new CurvePointHolder(AsOf, date, vol);
      Tenors.Add(new CurveTenor(date.ToStr("%m-%y"), pt, 0));
    }

    #endregion Methods

    #region Properties
    
    /// <summary>
    /// Gets or sets the type of the underlying distribution.
    /// </summary>
    /// <value>The type of the distribution.</value>
    public DistributionType DistributionType { get; set; }

    /// <summary>
    ///  Gets or sets the indicator that this is an instantaneous
    ///  volatility curve, instead of a term volatility (Black volatility) curve.
    /// </summary>
    /// <value>The type of the distribution.</value>
    internal bool IsInstantaneousVolatility
    {
      get { return (Flags & CurveFlags.Integrand) != 0; }
      set
      {
        if (value) Flags |= CurveFlags.Integrand;
        else Flags &= ~CurveFlags.Integrand;
      }
    }

    #endregion

    #region IVolatilityCalculatorProvider Members

    /// <summary>
    /// Gets the volatility calculator for the specified product.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <returns>The calculation function which takes the start (as-of) date
    ///   and returns the volatility from the start date to the expiry date.</returns>
    public Func<Dt, double> GetCalculator(IProduct product)
    {
      if (product == null) return Interpolate;

      var option = product as IOptionProduct;
      if (option != null)
        return begin => Interpolate(begin, option.Expiration);

      throw new ToolkitException("Unknown product type: " + product.GetType());
    }

    /// <summary>
    ///  Calculate the forward volatility between two days,
    ///   assuming this is a Black volatility curve.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns>The forward volatility between start and end dates</returns>
    /// <remarks></remarks>
    public override double Interpolate(Dt start, Dt end)
    {
      if (end == start)
      {
        end = start + 1;
      }
      var asOf = AsOf;
      if (start <= asOf)
      {
        return Interpolate(end);
      }
      var v1 = Interpolate(start);
      var t1 = Dt.RelativeTime(asOf, start);
      var v2 = Interpolate(end);
      var t2 = Dt.RelativeTime(asOf, end);
      return Math.Sqrt((v2 * v2 * t2 - v1 * v1 * t1) / (t2 - t1));
    }

    #endregion

    #region IVolatilitySurface Members

    /// <summary>
    /// ATM (no strike skew) vol surface
    /// </summary>
    /// <param name="maturity">Maturity</param>
    /// <param name="strike">Strike</param>
    /// <returns>Vol</returns>
    public double Interpolate(Dt maturity, double strike)
    {
      return Interpolate(maturity);
    }

    #endregion
  }
}
