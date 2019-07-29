/*
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Class representing Credit index volatility underlying.
  /// </summary>
  /// <exclude />
  [Serializable]
  public sealed class CdxVolatilityUnderlying : IVolatilityUnderlying
    , IVolatilityTenorBuilder, IVolatilitySurfaceBuilder
    , IConventionalQuoteConverter
  {
    #region Data and properties

    private readonly Dt _pricingDate, _protectStart;

    private readonly int _flags;
    private const int StrikeIsPriceFlag = 1;
    private const int PriceVolatilityFlag = 2;

    private readonly DiscountCurve _discountCurve;
    private readonly CDXOptionModelData _data;
    private readonly double _initialFactor, _factor, _losses;

    internal bool StrikeIsPrice
    {
      get { return 0 != (_flags & StrikeIsPriceFlag); }
    }

    private bool IsPriceVolatility
    {
      get { return 0 != (_flags & PriceVolatilityFlag); }
    }

    internal CDXOptionModelType ModelType
    {
      get
      {
        return IsPriceVolatility
          ? CDXOptionModelType.BlackPrice : CDXOptionModelType.Black;
      }
    }

    /// <summary>
    /// Gets the underlying CDX.
    /// </summary>
    /// <value>The CDX.</value>
    public CDX CDX { get; }

    /// <summary>
    /// Gets the quote of the current index level.
    /// </summary>
    /// <value>The index quote.</value>
    public MarketQuote IndexQuote { get; }

    /// <summary>
    /// Gets the index recovery rate.
    /// </summary>
    /// <value>The index recovery rate.</value>
    public double IndexRecoveryRate { get; }
    #endregion

    #region Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="CdxVolatilityUnderlying" /> class.
    /// </summary>
    /// <param name="pricingDate">The pricing date.</param>
    /// <param name="protectStart">The protection start date.</param>
    /// <param name="cdxSpotQuote">The CDX spot quote.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="cdx">The underlying credit index.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="strikeIsPrice">if set to <c>true</c> [strike is price].</param>
    /// <param name="isPriceVolatility">if set to <c>true</c> [is price volatility].</param>
    /// <param name="modelData">Index option model specific data object</param>
    /// <param name="currentFactor">Current index factor</param>
    /// <param name="existingLosses">Existing losses included in option front end protection</param>
    /// <param name="initialFactor">The index factor at option struck</param>
    public CdxVolatilityUnderlying(
      Dt pricingDate,
      Dt protectStart,
      MarketQuote cdxSpotQuote,
      DiscountCurve discountCurve,

      CDX cdx,
      double recoveryRate,

      bool strikeIsPrice,
      bool isPriceVolatility,
      CDXOptionModelData modelData,
      double currentFactor = 1.0,
      double existingLosses = 0.0,
      double initialFactor = Double.NaN)
    {
      _flags = 0;

      _pricingDate = pricingDate;
      _protectStart = protectStart;
      IndexQuote = cdxSpotQuote;
      _discountCurve = discountCurve;

      CDX = cdx;
      if (strikeIsPrice)
        _flags |= StrikeIsPriceFlag;
      if (isPriceVolatility)
        _flags |= PriceVolatilityFlag;

      IndexRecoveryRate = recoveryRate;
      _data = modelData ?? new CDXOptionModelData();
      _initialFactor = initialFactor;
      _factor = currentFactor;
      _losses = existingLosses;
    }

    /// <summary>
    /// Converts the price volatility to/from spread volatility.
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="requestedModelType">The requested CDX option model type</param>
    /// <returns>System.Double.</returns>
    public double ConvertVolatility(
      Dt expiry,
      double strike,
      double volatility,
      CDXOptionModelType requestedModelType)
    {
      var fv = GetCdxOptionPricer(expiry, strike, ModelType)
        .CalculateFairPrice(volatility);
      return GetCdxOptionPricer(expiry, strike, requestedModelType)
        .ImplyVolatility(fv);
    }

    private double CalculateForward(Dt expiry)
    {
      // Create option
      var pricer = GetCdxOptionPricer(expiry, StrikeIsPrice ? 0.1 : 0.01);
      var spread = pricer.EffectiveForwardSpread();
      if (IsPriceVolatility)
      {
        var pv01 = pricer.ForwardPv01;
        return 1 + pv01 * (pricer.CDXOption.CDX.Premium - spread);
      }
      return spread;
    }

    private double CalculateDeflator(Dt expiry)
    {
      return GetCdxOptionPricer(expiry, StrikeIsPrice ? 0.1 : 0.01)
        .GetNumerairLevel(IsPriceVolatility);
    }

    internal double CalculateStrike(Dt expiry, double strike)
    {
      return GetCdxOptionPricer(expiry, strike)
        .GetForwardStrike(IsPriceVolatility);
    }

    private ICreditIndexOptionPricer GetCdxOptionPricer(
      Dt expiry, double strike)
    {
      return GetCdxOptionPricer(expiry, strike, ModelType);
    }

    private ICreditIndexOptionPricer GetCdxOptionPricer(
      Dt expiry, double strike, CDXOptionModelType model)
    {
      var asOf = _pricingDate;
      var pricer = GetCdxOption(expiry, strike).CreatePricer(
        asOf, _protectStart.IsEmpty() ? Dt.Add(asOf, 1) : _protectStart,
        _discountCurve, IndexQuote, _protectStart, IndexRecoveryRate, 0, null,
        model, _data, null, 1.0, null);
      pricer.SetIndexFactorAndLosses(_factor, _losses);
      return pricer;
    }

    private CDXOption GetCdxOption(Dt expiry, double strike)
    {
      var strikeIsPrice = StrikeIsPrice;
      var cdxo = new CDXOption(_pricingDate, CDX.Ccy, CDX,
        expiry, PayerReceiver.Receiver, OptionStyle.European,
        strike, strikeIsPrice, _initialFactor);
      cdxo.Description = "Index Option";
      cdxo.SettlementType = SettlementType.Cash;
      cdxo.Validate();
      return cdxo;
    }

    #endregion

    #region Specialized interpolation method

    internal static VolatilitySurface GetCdxModelSurface(
      VolatilitySurface surface,
      bool? strikeIsPrice,
      CDXOptionModelType? modelType)
    {
      var underlying = (surface as CdxVolatilitySurface)?.Underlying;
      if (underlying == null || (strikeIsPrice == null && modelType == null))
      {
        // No specific strike type and volatility model.
        return surface;
      }
      if (strikeIsPrice.HasValue && strikeIsPrice != underlying.StrikeIsPrice)
      {
        underlying = underlying.Update(strikeIsPrice.Value);
      }
      else if (modelType == null || modelType == underlying.ModelType)
      {
        // Both strike type and volatility model match exactly
        return surface;
      }
      // Create a new surface to match the specified strike type and volatility model.
      return CdxVolatilitySurface.Create(surface, underlying, modelType);
    }

    internal static double Interpolate(
      VolatilitySurface surface, Dt date,
      double strike, bool? strikeIsPrice,
      CDXOptionModelType? modelType)
    {
      var underlying = (surface as CdxVolatilitySurface)?.Underlying;
      if (underlying == null)
      {
        return surface.Interpolate(date, strike);
      }
      return InterpolateAndConvert(underlying,
        surface, date, strike,
        strikeIsPrice ?? underlying.StrikeIsPrice,
        modelType ?? underlying.ModelType);
    }

    internal static double InterpolateAndConvert(
      CdxVolatilityUnderlying underlying,
      VolatilitySurface surface,
      Dt date, double strike,
      bool strikeIsPrice, CDXOptionModelType modelType)
    {
      if (strikeIsPrice != underlying.StrikeIsPrice)
      {
        underlying = underlying.Update(strikeIsPrice);
      }

      var baseSurface = (surface as CdxVolatilitySurface)?.BaseSurface
        ?? surface;
      var volatility = baseSurface.Interpolate(date,
        underlying.CalculateStrike(date, strike));

      return modelType == underlying.ModelType
        ? volatility
        : underlying.ConvertVolatility(date, strike, volatility, modelType);
    }

    private CdxVolatilityUnderlying Update(bool strikeIsPrice)
    {
      return new CdxVolatilityUnderlying(_pricingDate,
        _protectStart, IndexQuote, _discountCurve, CDX, IndexRecoveryRate,
        strikeIsPrice, IsPriceVolatility, _data);
    }

    #endregion

    #region IVolatilityUnderlying Members

    IFactorCurve IVolatilityUnderlying.Curve1
    {
      get { return null; }
    }

    IFactorCurve IVolatilityUnderlying.Curve2
    {
      get { return null; }
    }

    ISpotCurve IVolatilityUnderlying.Spot
    {
      get { return new DelegateSpotCurve(CalculateForward); }
    }

    ISpotCurve IVolatilityUnderlying.Deflator
    {
      get
      {
        return IsPriceVolatility
          ? (ISpotCurve)ConstantSpotCurve.One
          : new DelegateSpotCurve(CalculateDeflator);
      }
    }

    #endregion

    #region IVolatilityTenorBuilder Members

    IEnumerable<IVolatilityTenor> IVolatilityTenorBuilder.BuildTenors(
      Dt asOf, string[] tenorNames, Dt[] expiries, StrikeArray specs,
      double[,] quotes, VolatilityQuoteType quoteType,
      VolatilityFitSettings fitSettings)
    {
      fitSettings = fitSettings ?? new VolatilityFitSettings
      {
        ImpliedVolatilityAccuracy = 0.0
      };
      switch (quoteType)
      {
      case VolatilityQuoteType.StickyStrike:
        if (specs.AsNumbers() != null)
        {
          return VolatilitySurfaceFactory.BuildTenors(
            asOf, tenorNames, expiries, specs.AsNumbers(), quotes,
            // Skip the empty data point.
            (k, v) => Double.IsNaN(k) || !(v > 0),
            // Create a volatility tenor.
            CreateStrikeTenor);
        }
        return VolatilitySurfaceFactory.BuildTenors(asOf,
          tenorNames, expiries, specs.AsStrikeSpec(), quotes,
          // Skip the empty data point.
          (s, v) => s.IsEmpty || !(v > 0),
          // Create a volatility tenor.
          CreateStrikeTenor);
      case VolatilityQuoteType.StrikePrice:
        return VolatilitySurfaceFactory.BuildTenors(asOf,
          tenorNames, expiries, specs.AsStrikeSpec(), quotes,
          // Skip the empty data point.
          (s, v) => s.IsEmpty || !(v > 0),
          // Create a volatility tenor.
          (s, t, k, v) => CreatePriceTenor(s, t, k, v, fitSettings));
      default:
        break;
      }
      return VolatilitySurfaceFactory.BuildTenors(asOf, tenorNames,
        expiries, specs, quotes, quoteType, this, fitSettings);
    }

    private IVolatilityTenor CreateStrikeTenor(
      string tenorName,
      Dt expiry,
      double[] strikes,
      double[] quotes)
    {
      strikes = strikes.Select(k => CalculateStrike(expiry, k)).ToArray();
      EnsureSorted(ref strikes, ref quotes);
      return new StickyStrikeVolatilityTenor(tenorName, expiry, strikes, quotes);
    }

    private IVolatilityTenor CreateStrikeTenor(
      string tenorName,
      Dt expiry,
      StrikeSpec[] strikes,
      double[] quotes)
    {
      strikes = ConvertStrikes(expiry, strikes);
      EnsureSorted(ref strikes, ref quotes);
      return VolatilitySurfaceFactory.BuildCallPutVolatilityTenor(
        tenorName, expiry, strikes, quotes,
        (s, d, k, v) => new StickyStrikeVolatilityTenor(s, d, k, v));
    }

    private StrikePriceVolatilityTenor CreatePriceTenor(
      string name, Dt expiry, StrikeSpec[] strikes, double[] volatilities,
      VolatilityFitSettings fitSettings)
    {
      strikes = ConvertStrikes(expiry, strikes);
      EnsureSorted(ref strikes, ref volatilities);
      return new StrikePriceVolatilityTenor(
        name, expiry, strikes, volatilities,
        CalculateDeflator(expiry), 
        fitSettings.ImpliedVolatilityAccuracy);
    }

    private StrikeSpec[] ConvertStrikes(Dt expiry, StrikeSpec[] strikes)
    {
      if (strikes.IsNullOrEmpty()) return null;

      for (int i = 0; i < strikes.Length; ++i)
      {
        var spec = strikes[i];
        var t = spec.Type;
        if (IsPriceVolatility)
        {
          t = (t == OptionType.Call)
            ? OptionType.Put
            : (t == OptionType.Put ? OptionType.Call : t);
        }
        var k = CalculateStrike(expiry, spec.Strike);
        strikes[i] = StrikeSpec.Create(k, t);
      }
      return strikes;
    }

    private static void EnsureSorted<T>(ref T[] strikes,
      ref double[] values) where T : IComparable<T>
    {
      if (IsSorted(strikes)) return;
      var pairs = strikes
        .Zip(values, (k, v) => new { K = k, V = v })
        .OrderBy(p => p.K).ToList();
      strikes = pairs.Select(p => p.K).ToArray();
      values = pairs.Select(p => p.V).ToArray();
    }

    private static bool IsSorted<T>(T[] strikes) where T : IComparable<T>
    {
      if (strikes.IsNullOrEmpty()) return true;
      for (int i = 1; i < strikes.Length; ++i)
        if (strikes[i].CompareTo(strikes[i - 1]) < 0) return false;
      return true;
    }

    #endregion

    #region IVolatilitySurfaceBuilder Members

    CalibratedVolatilitySurface IVolatilitySurfaceBuilder.BuildSurface(
      Dt asOf, string[] tenorNames, Dt[] expiries,
      StrikeArray strikes, double[,] quotes,
      VolatilityQuoteType quoteType,
      SmileModel smileModel,
      Interp smileInterp,
      Interp timeInterp,
      VolatilityFitSettings fitSettings,
      string surfaceName)
    {
      var baseSurface = VolatilitySurfaceFactory.BuildSurface(
        asOf, tenorNames, expiries, strikes, quotes, quoteType,
        this, this, smileModel, smileInterp, timeInterp, fitSettings,
        surfaceName);
      return CdxVolatilitySurface.Create(baseSurface, this);
    }

    #endregion

    #region IConventionalQuoteConverter Members

    void IConventionalQuoteConverter.Convert(
      VolatilityQuoteType quoteType, object[] strikes, double[,] quotes)
    {
      if (quoteType == VolatilityQuoteType.StrikePrice)
      {
        // Scale the option price quotes.
        for (var i = 0; i < quotes.GetLength(0); i++)
          for (var j = 0; j < quotes.GetLength(1); j++)
            quotes[i, j] *= 0.0001;
      }
      else if (quoteType != VolatilityQuoteType.StickyStrike)
      {
        // We don not scale deltas and moneyness.
        return;
      }
      // Scale strikes, which can either index spreads or index prices.
      var mult = (IndexQuote.Type == QuotingConvention.FlatPrice) ? 0.01 : 0.0001;
      for (var i = 0; i < strikes.Length; i++)
      {
        if (strikes[i] is double)
          strikes[i] = ((double)strikes[i]) * mult;
      }
    }

    #endregion
  }
}
