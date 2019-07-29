// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves.Volatilities;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   The volatility tenor to hold market data for calculating implied volatility from option price.
  /// </summary>
  /// <remarks>
  ///   The Tenor class is designed to hold all the market data
  ///   of implied volatilities calculation. If more data is needed, the user should
  ///   derive from this class and add other fields.
  /// </remarks>
  [Serializable]
  public class StrikePriceVolatilityTenor : VolatilityTenor, ISmileDataProvider
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PlainVolatilityTenor" /> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="strikeSpecs">The strike specs.</param>
    /// <param name="prices">The prices.</param>
    /// <param name="deflator">The price multiplier (the prices are the multiplier times the Black-Scholes values).</param>
    /// <param name="accuracy">The accuracy level for implied volatility.</param>
    /// <exception cref="BaseEntity.Toolkit.Util.ToolkitException">
    /// Strike specs cannot be null
    /// or
    /// Prices cannot be null
    /// or
    /// </exception>
    /// <exception cref="ToolkitException">Strike specs cannot be null
    /// or
    /// Prices cannot be null
    /// or</exception>
    internal StrikePriceVolatilityTenor(string name, Dt maturity,
      IList<StrikeSpec> strikeSpecs, IList<double> prices,
      double deflator, double accuracy)
      : base(name, maturity)
    {
      if (strikeSpecs == null)
        throw new ToolkitException("Strike specs cannot be null");
      if (prices == null)
        throw new ToolkitException("Prices cannot be null");
      if (strikeSpecs.Count != prices.Count)
      {
        throw new ToolkitException(String.Format(
          "Prices (count={0}) and strikes (count={1}) not match",
          prices.Count, strikeSpecs.Count));
      }
      StrikeSpecs = strikeSpecs;
      Prices = prices;
      Deflator = deflator;
      _accuracy = accuracy;
    }

    /// <summary>
    /// Return a new object that is a deep copy of this instance
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the life cycle of the referenced object).
    /// </remarks>
    public override object Clone()
    {
      var obj = (StrikePriceVolatilityTenor)base.Clone();
      obj.StrikeSpecs = new List<StrikeSpec>(StrikeSpecs);
      obj.Prices = Clone(Prices);
      return obj;
    }

    #endregion

    #region Data

    private readonly double _accuracy;
    #endregion

    #region Properties

    /// <summary>
    /// Option types
    /// </summary>
    public IList<StrikeSpec> StrikeSpecs { get; private set; }

    /// <summary>
    /// The option prices
    /// </summary>
    public IList<double> Prices { get; private set; }

    /// <summary>
    /// Gets the price deflator (the prices are the Black-Scholes values devided by the deflator).
    /// </summary>
    /// <value>The deflator.</value>
    public double Deflator { get; private set; }

    /// <summary>
    /// Gets the volatility quote values.
    /// </summary>
    /// <value>The volatility quote values.</value>
    /// <remarks>The quote values may not be the same as the implied volatilities.
    /// For example, when the underlying options are quoted with prices,
    /// the quote values are prices instead of volatilities.
    /// When the underlying options are quoted as ATM call, Risk Reversals and Butterflies,
    /// the quote values are ATM volatilities and RR/BF deviations.</remarks>
    public override IList<double> QuoteValues
    {
      get { return Prices; }
    }

    #endregion

    #region ISmileDataProvider Members

    /// <summary>
    /// Get strike volatility pairs
    /// </summary>
    /// <param name="data"></param>
    public IEnumerable<StrikeVolatilityPair> GetStrikeVolatilityPairs(
      IBlackScholesParameterData data)
    {
      double tol = _accuracy;
      for (int i = 0, n = QuoteValues.Count - 1; i <= n; ++i)
      {
        var spec = StrikeSpecs[i];
        var strike = spec.Strike;
        var sigma = ImplyVolatility(data, QuoteValues[i], Deflator, spec, tol);
        if (i < n && strike.AlmostEquals(StrikeSpecs[i + 1].Strike))
        {
          ++i;
          var sig1 = ImplyVolatility(data,
            QuoteValues[i], Deflator, StrikeSpecs[i], tol);
          if (!Double.IsNaN(sig1))
          {
            sigma = Double.IsNaN(sigma) ? sig1 : ((sig1 + sigma) / 2);
          }
        }
        if (!Double.IsNaN(sigma))
        {
          yield return new StrikeVolatilityPair(strike, sigma);
        }
        if (Logger.IsInfoEnabled)
          Logger.Info(String.Format(
            "Failed to imply volatility: price {0}, strike {1} at {2}",
            QuoteValues[i], spec, Maturity));
      }
    }

    private static double ImplyVolatility(IBlackScholesParameterData data,
      double price, double deflator, StrikeSpec spec, double accuracy)
    {
      return BlackScholes.TryImplyVolatility(spec.Style, spec.Type,
        data.Time, data.Spot, spec.Strike, data.Rate2, data.Rate1,
        price / deflator, accuracy);
    }

    private static readonly log4net.ILog Logger
      = log4net.LogManager.GetLogger(typeof(StrikePriceVolatilityTenor));

    #endregion
  }
}
