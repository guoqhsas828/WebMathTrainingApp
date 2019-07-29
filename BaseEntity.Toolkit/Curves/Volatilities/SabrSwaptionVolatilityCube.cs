using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   Experimental volatility interface, to replace IVolatilityCalculatorProvider.
  /// </summary>
  /// <remarks></remarks>
  internal interface IVolatilityCalculator : IVolatilityObject
  {
    double CalculateVolatility(IProduct product, object additionalData);
  }

  /// <summary>
  ///  Create a swaption volatility cube.
  /// </summary>
  /// <remarks></remarks>
  public class SabrSwaptionVolatilityCube : SabrSwaptionVolatilityEvaluator, IVolatilityCalculator
  {
    private readonly Func<Dt, Dt, double> _timeCalculator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SabrSwaptionVolatilityCube"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="timeCalculator">The time calculator.</param>
    /// <param name="durations">The durations.</param>
    /// <param name="evaluators">The evaluators.</param>
    /// <param name="interp">The interp.</param>
    /// <remarks></remarks>
    public SabrSwaptionVolatilityCube(
      Dt asOf,
      Func<Dt, Dt, double> timeCalculator,
      double[] durations,
      SabrVolatilityEvaluator[] evaluators,
      Interp interp)
      : base(durations, evaluators, interp)
    {
      AsOf = asOf;
      if (timeCalculator == null)
      {
        timeCalculator = (begin, end) => (end - begin) / 365.25;
      }
      _timeCalculator = timeCalculator;
    }

    /// <summary>
    /// Gets the time to expiry calculator.
    /// </summary>
    public Func<Dt, Dt, double> TimeCalculator
    {
      get { return _timeCalculator; }
    }

    /// <summary>
    /// Gets or sets as of date.
    /// </summary>
    /// <value>As of date.</value>
    /// <remarks></remarks>
    public Dt AsOf { get; set; }

    #region IVolatilityObject Members

    /// <summary>
    /// Distribution type
    /// </summary>
    public DistributionType DistributionType
    {
      get { return DistributionType.LogNormal; }
    }

    #endregion

    #region IVolatilityCalculator Members

    /// <summary>
    /// Gets the volatility calculator for the specified product.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <param name="additionalData">Additional data</param>
    /// <returns>The calculator.</returns>
    public double CalculateVolatility(IProduct product, object additionalData)
    {
      var swpn = product as Swaption;
      if (swpn != null)
      {
        var strike = swpn.Strike;
        var pricer = (SwaptionBlackPricer)additionalData;
        var rate = pricer.ForwardSwapRate();
        var dd = RateVolatilityUtil.EffectiveSwaptionDuration(pricer);
        var duration = dd.Value/12;
        var expiry = Dt.AddDays(dd.Date,
          swpn.NotificationDays, swpn.NotificationCalendar);
        return Evaluate(expiry, duration, rate, strike);
      }
      throw new NotImplementedException();
    }

    #endregion
  }
}
