/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Represents a tenor with pairs of moneyness and volatilities as quotes.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class MoneynessVolatilityTenor : VolatilityTenor, ISmileDataProvider
  {
    #region Data
    private readonly double[] _moneyness;
    private readonly double _atmForward;
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="MoneynessVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">The name of this tenor.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="moneyness">The moneyness data (ratios of the strike to the ATM forwards).</param>
    /// <param name="volatilities">The volatilities.</param>
    /// <remarks></remarks>
    public MoneynessVolatilityTenor(
      string name, Dt expiry,
      double[] moneyness, double[] volatilities)
      : this(name, expiry, double.NaN, moneyness,volatilities)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MoneynessVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="atmForward">The atm forward.</param>
    /// <param name="moneyness">The moneyness.</param>
    /// <param name="volatilities">The volatilities.</param>
    /// <exception cref="ToolkitException">
    /// volatilities cannot be empty.
    /// or
    /// deltas cannot be empty.
    /// or
    /// Volatilities and strikes not match.
    /// </exception>
    public MoneynessVolatilityTenor(
      string name, Dt expiry, double atmForward,
      double[] moneyness, double[] volatilities)
      : base(name, expiry)
    {
      if (volatilities == null || volatilities.Length == 0)
      {
        throw new ToolkitException("volatilities cannot be empty.");
      }
      if (moneyness == null || moneyness.Length == 0)
      {
        throw new ToolkitException("deltas cannot be empty.");
      }
      if (volatilities.Length != moneyness.Length)
      {
        throw new ToolkitException("Volatilities and strikes not match.");
      }
      Volatilities = volatilities;
      _moneyness = moneyness;
      _atmForward = atmForward;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the FX volatility quote convention.
    /// </summary>
    /// <remarks></remarks>
    public VolatilityQuoteType QuoteType
    {
      get { return VolatilityQuoteType.Moneyness; }
    }

    /// <summary>
    /// Gets the quotes.
    /// </summary>
    /// <remarks></remarks>
    public VolatilityQuote[] Quotes
    {
      get
      {
        var vols = QuoteValues;
        return _moneyness.Select((m, i) => new VolatilityQuote(
          (m - 1).ToString(CultureInfo.InvariantCulture),
          vols[i])).ToArray();
      }
    }

    /// <summary>
    /// Gets the array of moneyness data at this tenor.
    /// </summary>
    /// <remarks></remarks>
    public double[] Moneyness
    {
      get { return _moneyness; }
    }

    /// <summary>
    /// Gets the volatilities.
    /// </summary>
    /// <value>The volatilities.</value>
    public IList<double> Volatilities { get; private set; }

    /// <summary>
    /// Gets the volatility quote values.
    /// </summary>
    /// <value>The volatility quote values.</value>
    /// <exception cref="System.NotImplementedException"></exception>
    /// <remarks>The quote values may not be the same as the implied volatilities.
    /// For example, when the underlying options are quoted with prices,
    /// the quote values are prices instead of volatilities.
    /// When the underlying options are quoted as ATM call, Risk Reversals and Butterflies,
    /// the quote values are ATM volatilities and RR/BF deviations.</remarks>
    public override IList<double> QuoteValues
    {
      get { return Volatilities; }
    }

    /// <summary>
    /// Gets the strikes.
    /// </summary>
    /// <value>The strikes.</value>
    public IList<double> Strikes
    {
      get { return ListUtil.CreateList(Moneyness.Length, i => Moneyness[i] * _atmForward); }
    }

    /// <summary>
    /// Gets a value indicating whether this instance has atm forward.
    /// </summary>
    /// <value><c>true</c> if this instance has atm forward; otherwise, <c>false</c>.</value>
    public bool HasAtmForward
    {
      get { return !Double.IsNaN(_atmForward); }
    }

    /// <summary>
    /// Gets the atm forward.
    /// </summary>
    /// <value>The atm forward.</value>
    public double AtmForward
    {
      get { return _atmForward; }
    }
    #endregion

    #region ISmileDataProvider Members

    /// <summary>
    /// Gets the strike/volatility pairs associated with the specified Black-Scholes parameter data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>A sequence of strike/volatility pairs.</returns>
    /// <remarks></remarks>
    public IEnumerable<StrikeVolatilityPair> GetStrikeVolatilityPairs(
      IBlackScholesParameterData data)
    {
      var fwd = data.GetForward();
      var vols = QuoteValues;
      return _moneyness.Select((m, i) => new StrikeVolatilityPair(m * fwd, vols[i]));
    }

    #endregion
  }
}
