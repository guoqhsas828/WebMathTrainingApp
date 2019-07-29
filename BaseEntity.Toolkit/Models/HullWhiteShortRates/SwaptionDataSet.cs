/*
 * 
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;


namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  using static SwaptionsEvaluator;

  /// <summary>
  /// Organizing swaption data.
  /// </summary>
  [Serializable]
  public class SwaptionDataSet
  {
    #region Factory methods

    /// <summary>
    /// Creates the a <see cref="SwaptionDataSet" /> for volatility calibration
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="volatilities">The volatilities.</param>
    /// <param name="volatilityScaleFactor">The volatility scale factor.</param>
    /// <param name="strikeSkews">The strike skews.</param>
    /// <param name="volatilitySpec">The volatility spec.</param>
    /// <param name="index">The index.</param>
    /// <returns>SwaptionDataSet.</returns>
    /// <exception cref="System.ArgumentException">discountCurve
    /// or
    /// expiries
    /// or
    /// tenors</exception>
    internal static SwaptionDataSet Create(
      Dt asOf,
      DiscountCurve discountCurve,
      IReadOnlyList<Tenor> expiries,
      IReadOnlyList<Tenor> tenors,
      IReadOnlyList<IReadOnlyList<double>> volatilities,
      double volatilityScaleFactor = 1.0,
      IReadOnlyList<IReadOnlyList<double>> strikeSkews = null,
      VolatilitySpec volatilitySpec = new VolatilitySpec(),
      ReferenceIndex index = null)
    {
      // Sanity checks
      if (discountCurve == null || discountCurve.Count == 0)
        throw new ArgumentException($"{nameof(discountCurve)} cannot be empty");
      if (expiries == null || expiries.Count == 0)
        throw new ArgumentException($"{nameof(expiries)} cannot be empty");
      if (tenors == null || tenors.Count == 0)
        throw new ArgumentException($"{nameof(tenors)} cannot be empty");

      int expiryCount = expiries.Count, tenorCount = tenors.Count;
      Validate(nameof(volatilities), volatilities, expiryCount, tenorCount);
      if (!(volatilityScaleFactor > 0))
        throw new ArgumentNullException($"{nameof(volatilityScaleFactor)} must be positive");
      if (strikeSkews != null)
      {
        if (strikeSkews.Count == 0)
          strikeSkews = null;
        else
          Validate(nameof(strikeSkews), strikeSkews, expiryCount, tenorCount);
      }

      // Get the distinct dates
      var dates = new UniqueSequence<Dt>();
      {
        var bdc = index?.Roll ?? BDConvention.None;
        var calendar = index?.Calendar ?? Calendar.None;
        for (int i = 0; i < expiryCount; ++i)
        {
          Dt expiry = Dt.Roll(Dt.Add(asOf, expiries[i]), bdc, calendar);
          dates.Add(expiry);
          for (int j = 0; j < tenorCount; ++j)
            dates.Add(Dt.Roll(Dt.Add(expiry, tenors[j]), bdc, calendar));
        }
      }

      int dateCount = dates.Count;
      var times = new double[dateCount];
      var zeroPrices = new double[dateCount];
      var instForwards = new double[dateCount];
      var df0 = discountCurve.Interpolate(asOf);
      for (int i = 0; i < dateCount; ++i)
      {
        Dt date = dates[i];
        times[i] = (date - asOf)/365.0;
        var df = discountCurve.Interpolate(date);
        zeroPrices[i] = df/df0;
        instForwards[i] = Math.Log(df/discountCurve.Interpolate(date + 1))*365.0;
      }

      var swpns = new List<SwaptionDataItem>();
      for (int i = 0; i < expiryCount; ++i)
      {
        Dt expiry = Dt.Add(asOf, expiries[i]);
        var expiryIndex = dates.IndexOf(expiry);
        for (int j = 0; j < tenorCount; ++j)
        {
          var volatility = volatilities[i][j];

          // Ignore invalid data (we allow missing data points)
          if (!(volatility > 0))
          {
            continue;
          }

          // Calculate the swaption market PV
          Dt maturity = Dt.Add(expiry, tenors[j]);
          var maturityIndex = dates.IndexOf(maturity);

          double swapRate;
          var annuity = CalculateSwapAnnuity(
            expiryIndex, maturityIndex, times, zeroPrices,
            out swapRate);
          var strike = strikeSkews == null
            ? swapRate : (swapRate + strikeSkews[i][j]);

          var marketPv = volatilitySpec.CalculateOptionPrice(
            volatilities[i][j]*volatilityScaleFactor, times[expiryIndex],
            swapRate, strike)*annuity;

          swpns.Add(new SwaptionDataItem(
            expiryIndex, maturityIndex, strike, marketPv));
        }
      }

      // Now we have all the data members initialized
      return new SwaptionDataSet(swpns.ToArray(),
        times, GetFractions(times), zeroPrices, instForwards);
    }

    /// <summary>
    /// Validates the specified name.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name">The name.</param>
    /// <param name="data">The data.</param>
    /// <param name="rowCount">The row count.</param>
    /// <param name="columnCount">The column count.</param>
    /// <exception cref="System.ArgumentException">
    /// </exception>
    private static void Validate<T>(
      string name, IReadOnlyList<IReadOnlyList<T>> data,
      int rowCount, int columnCount)
    {
      if (data == null || data.Count == 0)
        throw new ArgumentException($"{name} cannot be empty");
      if (data.Count != rowCount)
        throw new ArgumentException($"{name} must have {rowCount} rows");
      for (int i = 0, m = data.Count; i < m; ++i)
      {
        var row = data[i];
        if (row == null || row.Count == 0)
          throw new ArgumentException($"Row {i} of {name} cannot be empty");
        if (row.Count != columnCount)
          throw new ArgumentException($"Row {i} of {name} must have {columnCount} columns");
      }
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="SwaptionDataSet"/> class.
    /// </summary>
    /// <param name="swpns">The SWPNS.</param>
    /// <param name="times">The times.</param>
    /// <param name="fractions">The fractions.</param>
    /// <param name="zeros">The zeros.</param>
    /// <param name="forwards">The forwards.</param>
    internal SwaptionDataSet(
      IReadOnlyList<SwaptionDataItem> swpns,
      IReadOnlyList<double> times,
      IReadOnlyList<double> fractions,
      IReadOnlyList<double> zeros,
      IReadOnlyList<double> forwards)
    {
      Swaptions = swpns;
      Times = times;
      Fractions = fractions;
      ZeroPrices = zeros;
      Forwards = forwards;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The swaptions
    /// </summary>
    internal readonly IReadOnlyList<SwaptionDataItem> Swaptions;

    /// <summary>
    /// The times
    /// </summary>
    internal readonly IReadOnlyList<double> Times;

    /// <summary>
    /// The fractions
    /// </summary>
    internal readonly IReadOnlyList<double> Fractions;

    /// <summary>
    /// The zero prices
    /// </summary>
    internal readonly IReadOnlyList<double> ZeroPrices;

    /// <summary>
    /// The forwards
    /// </summary>
    internal readonly IReadOnlyList<double> Forwards;

    #endregion
  }

  public class SwaptionDataItem
  {
#if UseSwapItem
      internal SwaptionDataItem(SwapDataItem swap,
        double strike, double marketVolatility, double marketPv)
      {
        Debug.Assert(swap != null);
        Swap = swap;
        Strike = strike;
        MarketVolatility = marketVolatility;
        MarketPv = marketPv;
      }

      public readonly SwapDataItem Swap;
#else
    internal SwaptionDataItem(int expiry, int maturity,
      double strike, double marketPv)
    {
      Expiry = expiry;
      Maturity = maturity;
      Strike = strike;
      MarketPv = marketPv;
    }

    public readonly int Expiry, Maturity;
#endif

    public readonly double Strike, MarketPv; //, MarketVolatility;
  }

}
