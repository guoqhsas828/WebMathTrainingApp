/*
 *  -2013. All rights reserved.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  #region IVolatilityTenorBuilder

  internal interface IVolatilityTenorBuilder
  {
    IEnumerable<IVolatilityTenor> BuildTenors(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      StrikeArray specs,
      double[,] quotes,
      VolatilityQuoteType quoteType,
      VolatilityFitSettings fitSettings);
  }

  #endregion

  #region IVolatilitySurfaceBuilder

  internal interface IVolatilitySurfaceBuilder
  {
    CalibratedVolatilitySurface BuildSurface(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      StrikeArray strikes,
      double[,] quotes,
      VolatilityQuoteType quoteType,
      SmileModel smileModel,
      Interp smileInterp,
      Interp timeInterp,
      VolatilityFitSettings fitSettings,
      string surfaceName);
  }

  #endregion

  #region IConventionalQuoteConverter
  /// <summary>
  ///  Interface to convert the conventional quotes into the raw numbers.
  /// </summary>
  /// <exclude />
  public interface IConventionalQuoteConverter
  {
    /// <summary>
    /// Converts the specified strikes/quotes into raw numbers based market conventions.
    /// </summary>
    /// <param name="quoteType">Type of the quote.</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="quotes">The quotes.</param>
    /// <remarks>
    ///   <para>The conversion is based on the market convention specific to the underlying asset.
    ///   For example, the strike in basis points like 100 is converted into 0.0100 in raw numbers.</para>
    ///   <para>This method may modify both strikes and quotes in place.</para>
    /// </remarks>
    void Convert(VolatilityQuoteType quoteType, object[] strikes, double[,] quotes);
  }
  #endregion

  #region IVolatilityUnderlying
  /// <summary>
  /// Interface IVolatilityTerm
  /// </summary>
  public interface IVolatilityUnderlying
  {
    /// <summary>
    /// Gets the factor curve 1 (the yield curve, the convenience curve or the foreign interest rate curve).
    /// </summary>
    /// <value>The curve1.</value>
    IFactorCurve Curve1 { get; }

    /// <summary>
    /// Gets the factor curve 2 (normally the dicount curve or domestic interest curve).
    /// </summary>
    /// <value>The curve2.</value>
    IFactorCurve Curve2 { get; }

    /// <summary>
    /// Gets the spot price curve for the underlying (normally a constant spot price of the underlying security).
    /// </summary>
    /// <value>The spot.</value>
    ISpotCurve Spot { get; }

    /// <summary>
    /// Gets the deflator curve.
    /// </summary>
    /// <value>The deflator.</value>
    ISpotCurve Deflator { get; }
  }
  #endregion

  #region Constant Spot Curve
  [Serializable]
  internal class ConstantSpotCurve : ISpotCurve
  {
    internal static readonly ConstantSpotCurve One
      = new ConstantSpotCurve(1.0);

    private readonly double _value;

    private ConstantSpotCurve(double value)
    {
      _value = value;
    }

    public override string ToString()
    {
      return _value.ToString();
    }

    public static implicit operator ConstantSpotCurve(double value)
    {
      return new ConstantSpotCurve(value);
    }

    public static explicit operator double(ConstantSpotCurve curve)
    {
      return curve._value;
    }

    #region ISpotCurve Members

    public double Interpolate(Dt date)
    {
      return _value;
    }

    #endregion
  }
  #endregion

  #region Delegate Spot Curve
  [Serializable]
  class DelegateSpotCurve : ISpotCurve
  {
    private readonly Func<Dt, double> _fn;
    public DelegateSpotCurve(Func<Dt, double> fn)
    {
      _fn = fn;
    }
    public double Interpolate(Dt date)
    {
      return _fn(date);
    }
  }
  #endregion

  #region VolatilityUnderlying

  /// <summary>
  /// Class representing the term of price volatility.
  /// </summary>
  /// <remarks>
  ///   <para>Let <m>P_0</m> be the spot price at time <m>0</m>.
  ///   The forward price, <m>P(t)</m>, is calculated as <math>
  ///     P(t) = P_0 \exp\left( \int_0^t \left(r(s) - q(s)\right) ds \right)
  ///   </math> where <m>r(s)</m> is the interest rate
  ///   and <m>q(s)</m> is the yield rate, as implied from the discount curve
  ///   and the yield curve, respectively.</para>
  ///   <para>When either curve is null, the corresponding rate is zero.</para>
  ///   <para>When both curves are null, both rates are zero, in which case
  ///    the spot price is teh same as the forward price.</para>
  /// </remarks>
  [Serializable]
  public class VolatilityUnderlying : IVolatilityUnderlying
  {
    #region Data

    private readonly ISpotCurve _spotPrice;
    private readonly IFactorCurve _discountCurve;
    private readonly IFactorCurve _yieldCurve;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityUnderlying" /> class.
    /// </summary>
    /// <param name="spotPriceCurve">The spot price.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="yieldCurve">The yield curve.</param>
    private VolatilityUnderlying(
      ISpotCurve spotPriceCurve,
      IFactorCurve discountCurve,
      IFactorCurve yieldCurve)
    {
      _spotPrice = spotPriceCurve;
      _discountCurve = discountCurve;
      _yieldCurve = yieldCurve;
    }

    /// <summary>
    /// Creates the AssetVolatilityTerm from the yield curve.
    /// </summary>
    /// <param name="spotPriceCurve">The spot price curve.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="yieldCurve">The yield curve.</param>
    /// <returns>AssetVolatilityTerm.</returns>
    public static VolatilityUnderlying Create(
      ISpotCurve spotPriceCurve,
      IFactorCurve discountCurve,
      IFactorCurve yieldCurve)
    {
      return new VolatilityUnderlying(spotPriceCurve,
        discountCurve, yieldCurve);
    }

    /// <summary>
    /// Creates VolatilityUnderlying from yield curve.
    /// </summary>
    /// <param name="spotPrice">The spot price.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="yieldCurve">The yield curve.</param>
    /// <returns>VolatilityUnderlying.</returns>
    public static VolatilityUnderlying CreateFromYield(
      double spotPrice,
      IFactorCurve discountCurve,
      IFactorCurve yieldCurve)
    {
      return new VolatilityUnderlying((ConstantSpotCurve)spotPrice,
        discountCurve, yieldCurve);
    }

    /// <summary>
    /// Creates VolatilityUnderlying from forward price curve.
    /// </summary>
    /// <param name="spotPrice">The spot price.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="forwardPriceCurve">The forward price curve.</param>
    /// <returns>VolatilityUnderlying.</returns>
    public static VolatilityUnderlying CreateFromForward(
      double spotPrice,
      IFactorCurve discountCurve,
      IFactorCurve forwardPriceCurve)
    {
      return CreateFromYield(spotPrice, discountCurve,
        new YieldFactorCurve(forwardPriceCurve, discountCurve));
    }

    #region Properties

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    public IFactorCurve DiscountCurve
    {
      get { return _discountCurve; }
    }

    /// <summary>
    /// Gets the yield curve.
    /// </summary>
    /// <value>The yield curve.</value>
    public IFactorCurve YieldCurve
    {
      get { return _yieldCurve; }
    }

    /// <summary>
    /// Gets the spot price.
    /// </summary>
    /// <value>The spot price.</value>
    public ISpotCurve SpotPrice
    {
      get { return _spotPrice; }
    }

    #endregion

    #region IVolatilityUnderlying Members

    IFactorCurve IVolatilityUnderlying.Curve1
    {
      get { return YieldCurve; }
    }

    IFactorCurve IVolatilityUnderlying.Curve2
    {
      get { return DiscountCurve; }
    }

    ISpotCurve IVolatilityUnderlying.Spot
    {
      get { return _spotPrice; }
    }

    ISpotCurve IVolatilityUnderlying.Deflator
    {
      get { return ConstantSpotCurve.One; }
    }

    #endregion

    #region Nested type: yield factor curve
    class YieldFactorCurve : IFactorCurve
    {
      private readonly IFactorCurve _forwardPriceCurve;
      private readonly IFactorCurve _discountCurve;

      public YieldFactorCurve(IFactorCurve fwdPriceCurve,
        IFactorCurve discountCurve)
      {
        _forwardPriceCurve = fwdPriceCurve;
        _discountCurve = discountCurve;
      }

      public override string  ToString()
      {
        return _forwardPriceCurve.ToString();
      }

      #region ICurve Members

      public double Interpolate(Dt begin, Dt end)
      {
        var factor = _forwardPriceCurve.Interpolate(begin, end);
        return _discountCurve == null
          ? factor
          : (factor / _discountCurve.Interpolate(begin, end));
      }

      #endregion
    }
    #endregion
  }
  #endregion

  #region StockVolatilityUnderlying

  /// <summary>
  /// Class StockVolatilityTerm
  /// </summary>
  public class StockVolatilityUnderlying : IVolatilityUnderlying, IFactorCurve
  {
    #region Data

    /// <summary>
    /// The spot date
    /// </summary>
    private readonly Dt _spotDate;

    /// <summary>
    /// The spot price
    /// </summary>
    private readonly double _spotPrice;

    /// <summary>
    /// The discount curve
    /// </summary>
    private readonly DiscountCurve _discountCurve;

    /// <summary>
    /// The dividend schedule
    /// </summary>
    private readonly DividendSchedule _dividendSchedule;

    /// <summary>
    /// The continuous dividend yield curve
    /// </summary>
    private readonly IFactorCurve _stockCurve;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new instance of the <see cref="StockVolatilityUnderlying"/> class.
    /// </summary>
    /// <param name="spotDate">The spot date.</param>
    /// <param name="spotPrice">The spot price.</param>
    /// <param name="dividendSchedule">The dividend schedule.</param>
    /// <param name="discountCurve">The discount curve.</param>
    public static StockVolatilityUnderlying Create(
      Dt spotDate, double spotPrice,
      DividendSchedule dividendSchedule,
      DiscountCurve discountCurve)
    {
      return new StockVolatilityUnderlying(spotDate, spotPrice,
        dividendSchedule, null, discountCurve);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="StockVolatilityUnderlying"/> class.
    /// </summary>
    /// <param name="spotDate">The spot date.</param>
    /// <param name="spotPrice">The spot price.</param>
    /// <param name="dividendYield">The dividend yield.</param>
    /// <param name="discountCurve">The discount curve.</param>
    public static StockVolatilityUnderlying Create(
      Dt spotDate, double spotPrice,
      double dividendYield,
      DiscountCurve discountCurve)
    {
      return new StockVolatilityUnderlying(spotDate, spotPrice, null,
        new DiscountCurve(spotDate).SetRelativeTimeRate(dividendYield),
        discountCurve);
    }

    private StockVolatilityUnderlying(
      Dt spotDate, double spotPrice,
      DividendSchedule dividendSchedule,
      IFactorCurve stockCurve,
      DiscountCurve discountCurve)
    {
      _spotDate = spotDate;
      _spotPrice = spotPrice;
      _discountCurve = discountCurve;
      _dividendSchedule = dividendSchedule;
      _stockCurve = stockCurve;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the spot date.
    /// </summary>
    /// <value>The spot date.</value>
    public Dt SpotDate
    {
      get { return _spotDate; }
    }

    /// <summary>
    /// Gets the spot price.
    /// </summary>
    /// <value>The spot price.</value>
    public double SpotPrice
    {
      get { return _spotPrice; }
    }

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    public DiscountCurve DiscountCurve
    {
      get { return _discountCurve; }
    }

    /// <summary>
    /// Gets the dividend schedule.
    /// </summary>
    /// <value>The dividend schedule.</value>
    public DividendSchedule DividendSchedule
    {
      get { return _dividendSchedule; }
    }

    #endregion

    #region IVolatilityUnderlying Members

    /// <summary>
    /// Gets the curve1.
    /// </summary>
    /// <value>The curve1.</value>
    IFactorCurve IVolatilityUnderlying.Curve1
    {
      get { return _stockCurve ?? this; }
    }

    /// <summary>
    /// Gets the curve2.
    /// </summary>
    /// <value>The curve2.</value>
    IFactorCurve IVolatilityUnderlying.Curve2
    {
      get { return _discountCurve; }
    }

    /// <summary>
    /// Gets the spot rate or price.
    /// </summary>
    /// <value>The spot.</value>
    ISpotCurve IVolatilityUnderlying.Spot
    {
      get { return (ConstantSpotCurve)_spotPrice; }
    }

    ISpotCurve IVolatilityUnderlying.Deflator
    {
      get { return ConstantSpotCurve.One; }
    }

    #endregion

    #region ICurve Members

    /// <summary>
    /// Calculate the ratio of the discounted forward price
    /// at the end date over that at the begin date.
    /// </summary>
    /// <param name="begin">The begin date.</param>
    /// <param name="end">The end date.</param>
    /// <returns>System.Double.</returns>
    double IFactorCurve.Interpolate(Dt begin, Dt end)
    {
      return GetForwardPrice(end) / GetForwardPrice(begin);
    }

    /// <summary>
    /// Gets the forward price.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="ToolkitException">Forward stock price is zero or negative</exception>
    private double GetForwardPrice(Dt date)
    {
      if (_stockCurve != null)
      {
        throw new ToolkitException("Invalid call");
      }
      if (date <= _spotDate)
        return _spotPrice;
      var dpv = _dividendSchedule.Pv(
        _spotDate, date, _spotPrice, _discountCurve);
      if (dpv >= _spotPrice)
      {
        throw new ToolkitException("Forward stock price is zero or negative");
      }
      return _spotPrice - dpv;
    }

    #endregion
  }

  #endregion

  #region SmileModel
  /// <summary>
  /// Enumeration of the available volatility smile models.
  /// </summary>
  public enum SmileModel
  {
    /// <summary>
    ///  Not specified.
    /// </summary>
    None,
    /// <summary>
    /// Simple spline interpolation.
    /// </summary>
    SplineInterpolation,
    /// <summary>
    /// Regression based the quadratic smile function.
    /// </summary>
    QuadraticRegression,
    /// <summary>
    /// Hedge-based extrapolation in the vanna-volga style.
    /// </summary>
    VannaVolga,
    /// <summary>
    /// The SABR model.
    /// </summary>
    Sabr,
    /// <summary>
    /// The Heston model.
    /// </summary>
    Heston,
  }
  #endregion

  #region StrikeArray
  /// <summary>
  /// Representing an array of strike values in the specified format
  /// (raw value, moneyness, ATM and deltas,
  /// as well as ATM, risk reversal and butterfly.)
  /// </summary>
  public struct StrikeArray
  {
    /// <summary>
    /// The specification data
    /// </summary>
    private readonly Array _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrikeArray" /> struct.
    /// </summary>
    /// <param name="data">The data.</param>
    private StrikeArray(Array data)
    {
      _data = data;
    }

    /// <summary>
    /// Performs an implicit conversion from double[] to <see cref="StrikeArray"/>.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator StrikeArray(double[] data)
    {
      return new StrikeArray(data);
    }

    /// <summary>
    /// Performs an implicit conversion from and array of <see cref="DeltaSpec"/> to <see cref="StrikeArray"/>.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator StrikeArray(DeltaSpec[] data)
    {
      return new StrikeArray(data);
    }

    /// <summary>
    /// Performs an implicit conversion from an array of <seealso cref="FxRrBfSpec"/> to <see cref="StrikeArray"/>.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator StrikeArray(FxRrBfSpec[] data)
    {
      return new StrikeArray(data);
    }

    /// <summary>
    /// Performs an implicit conversion from an array of <seealso cref="StrikeSpec"/> to <see cref="StrikeArray"/>.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator StrikeArray(StrikeSpec[] data)
    {
      return new StrikeArray(data);
    }

    /// <summary>
    /// Convert the inputs to the appropriate strike specifications based on the quote type.
    /// </summary>
    /// <param name="strikes">The strikes.</param>
    /// <param name="quoteType">Type of the quote.</param>
    /// <returns>StrikeSpec[][].</returns>
    public static StrikeArray Create(
      IEnumerable<object> strikes,
      VolatilityQuoteType quoteType)
    {
      if (strikes == null)
      {
        throw new ArgumentException("strikes cannot be null");
      }
      switch (quoteType)
      {
      case VolatilityQuoteType.StickyDelta:
        return strikes.Select(k => DeltaSpec.Parse(
          k != null ? k.ToString() : "")).ToArray();
      case VolatilityQuoteType.RiskReversalButterfly:
        return strikes.Select(k => FxRrBfSpec.Parse(
          k != null ? k.ToString() : "")).ToArray();
      case VolatilityQuoteType.StrikePrice:
        return strikes.Select(StrikeSpec.Parse).ToArray();
      }

      // For the default case, we first makes sure the list is not empty.
      var enumerator = strikes.GetEnumerator();
      if (!enumerator.MoveNext())
      {
        throw new ArgumentException("strikes cannot be empty");
      }
      // Then we check the first element to determine the strike type.
      if (enumerator.Current is StrikeSpec)
        return ParseStrikes(enumerator, StrikeSpec.Parse, StrikeSpec.Empty);
      return ParseStrikes(enumerator, Double.Parse, Double.NaN);
    }

    /// <summary>
    /// Parses the strikes with the specified type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerator">The enumerator.</param>
    /// <param name="parse">The parse function.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>``0[][].</returns>
    private static T[] ParseStrikes<T>(
      IEnumerator<object> enumerator,
      Func<string, T> parse,
      T defaultValue)
    {
      var list = new List<T>();
      //Note:
      //  Upon entry, the current has already pointed to the first element.
      //  So we must not call enumerator.MoveNext() here.
      do
      {
        var strike = enumerator.Current;
        if (strike is T)
        {
          list.Add((T)strike);
          continue;
        }
        var s = strike != null ? strike.ToString() : null;
        list.Add(String.IsNullOrEmpty(s) ? defaultValue : parse(s));
      } while (enumerator.MoveNext());
      return list.ToArray();
    }

    /// <summary>
    /// Cast as the strike value.
    /// </summary>
    /// <returns>System.Double.</returns>
    /// <value>The strike value.</value>
    public double[] AsNumbers()
    {
      return _data as double[];
    }

    /// <summary>
    /// Casts as the delta spec.
    /// </summary>
    /// <returns>DeltaSpec.</returns>
    public DeltaSpec[] AsDeltaSpec()
    {
      return _data as DeltaSpec[];
    }

    /// <summary>
    /// Casts as the fx rr bf spec.
    /// </summary>
    /// <returns>FxRrBfSpec.</returns>
    public FxRrBfSpec[] AsRrBfSpec()
    {
      return _data as FxRrBfSpec[];
    }

    /// <summary>
    /// Casts as the strike specification.
    /// </summary>
    /// <returns>FxRrBfSpec.</returns>
    public StrikeSpec[] AsStrikeSpec()
    {
      return _data as StrikeSpec[];
    }

    /// <summary>
    /// Gets a value indicating whether this instance is null.
    /// </summary>
    /// <value><c>true</c> if this instance is null; otherwise, <c>false</c>.</value>
    public bool IsNullOrEmpty
    {
      get { return _data == null || _data.Length == 0; }
    }

    /// <summary>
    /// Gets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length
    {
      get { return _data == null ? 0 : _data.Length; }
    }
  }
  #endregion

  /// <summary>
  /// VolatilitySurfaceFactory to build volatility surfaces.
  /// </summary>
  public static class VolatilitySurfaceFactory
  {
    #region Surface builders

    /// <summary>
    /// Creates the specified CalibratedVolatiltiySurface
    /// </summary>
    /// <param name="asOf">Pricing (as of) date.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="quotes">The quotes.</param>
    /// <param name="quoteType">Type of the quote.</param>
    /// <param name="term">The term.</param>
    /// <param name="smileModel">The smile method.</param>
    /// <param name="smileInterp">The smile interpolation method.</param>
    /// <param name="timeInterp">The time interpolation method.</param>
    /// <param name="surfaceName">Name of the surface.</param>
    /// <param name="fitSettings">Additional settings for volatility fitting.</param>
    /// <returns>Created CalibratedVolatilitySurface</returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public static CalibratedVolatilitySurface Create(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      StrikeArray strikes,
      double[,] quotes,
      VolatilityQuoteType quoteType,
      IVolatilityUnderlying term,
      SmileModel smileModel,
      Interp smileInterp,
      Interp timeInterp,
      VolatilityFitSettings fitSettings,
      string surfaceName)
    {
      if (strikes.IsNullOrEmpty)
      {
        throw new ToolkitException("strikes cannot be empty");
      }
      if (smileInterp == null)
      {
        smileInterp = InterpScheme.FromString(
          "TensionC1", ExtrapMethod.Const, ExtrapMethod.Const).ToInterp();
      }
      if (timeInterp == null)
      {
        timeInterp = new SquareLinearVolatilityInterp();
      }

      //
      // Delegate to a surface builder if possible
      //
      var surfaceBuilder = term as IVolatilitySurfaceBuilder;
      if (surfaceBuilder != null)
      {
        return surfaceBuilder.BuildSurface(asOf, tenorNames, expiries, strikes,
          quotes, quoteType, smileModel, smileInterp, timeInterp, fitSettings,
          surfaceName);
      }

      //
      // The built-in surface builders.
      // 
      return BuildSurface(asOf, tenorNames, expiries,
        strikes, quotes, quoteType,
        term as IVolatilityTenorBuilder, term,
        smileModel, smileInterp, timeInterp, fitSettings, surfaceName);
    }

    internal static CalibratedVolatilitySurface BuildSurface(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      StrikeArray strikes,
      double[,] quotes,
      VolatilityQuoteType quoteType,
      IVolatilityTenorBuilder tenorBuilder,
      IVolatilityUnderlying term,
      SmileModel smileModel,
      Interp smileInterp,
      Interp timeInterp,
      VolatilityFitSettings fitSettings,
      string surfaceName)
    {
      var tenors = tenorBuilder?.BuildTenors(asOf, tenorNames, expiries,
        strikes, quotes, quoteType, fitSettings) ?? BuildTenors(asOf,
          tenorNames, expiries, strikes, quotes, quoteType, term, fitSettings);
      var smileInput = quoteType == VolatilityQuoteType.Moneyness
        ? SmileInputKind.Moneyness
        : SmileInputKind.Strike;

      switch (smileModel)
      {
      case SmileModel.None:
      case SmileModel.SplineInterpolation:
        if (quoteType == VolatilityQuoteType.StickyStrike && term == null)
        {
          return new CalibratedVolatilitySurface(asOf, tenors.ToArray(),
            null, new VolatilityPlainInterpolator(smileInterp, timeInterp));
        }
        if (term == null)
          throw new ToolkitException("Volatility term is null");
        return new GenericBlackScholesCalibrator(asOf, term.Spot,
          term.Curve1, term.Curve2, smileInterp, timeInterp, smileModel)
          .BuildSurface(tenors, smileInput);

      case SmileModel.QuadraticRegression:
        var fxterm = term as FxVolatilityUnderlying;
        if (fxterm != null)
        {
          return new FxVolatilitySurfaceCalibrator(asOf,
            timeInterp, smileInterp, fxterm.DomesticRateCurve,
            fxterm.ForeignRateCurve, fxterm.FxCurve)
            .BuildSurface(tenors, smileInput);
        }
        if (term == null)
          throw new ToolkitException("Volatility underlying is null");
        return new GenericBlackScholesCalibrator(asOf, term.Spot,
          term.Curve1, term.Curve2, smileInterp, timeInterp, smileModel)
          .BuildSurface(tenors, smileInput);
      case SmileModel.Sabr:
      case SmileModel.Heston:
        return SabrSurfaceCalibrator.CreateSurface(asOf, tenors,
          smileInput, term, fitSettings, timeInterp);

      case SmileModel.VannaVolga:
        if (quoteType != VolatilityQuoteType.RiskReversalButterfly)
          throw new ToolkitException("Expect Risk Reversal plus Butterfly quotes");
        return CreateVannaVolgasurface(asOf, tenors, term as FxVolatilityUnderlying,
          timeInterp, surfaceName);
      }
      throw new ToolkitException(String.Format(
        "Unable to build volatility surface with model {0}",
        smileModel));
    }

    private static CalibratedVolatilitySurface CreateVannaVolgasurface(
      Dt asOf,
      IEnumerable<IVolatilityTenor> tenors,
      FxVolatilityUnderlying term,
      Interp timeInterp,
      string surfaceName)
    {
      if (term == null)
        throw new ToolkitException("Expect FxVolatilityTerm");
      var fxCurve = term.FxCurve;
      // Create VV calibrator and vol surface
      var calibrator = new FxOptionVannaVolgaCalibrator(asOf, asOf,
        term.DomesticRateCurve ?? fxCurve.Ccy2DiscountCurve,
        term.ForeignRateCurve ?? fxCurve.Ccy1DiscountCurve,
        fxCurve, new[] { timeInterp }, term);
      calibrator.SmileConsistent = true;
      calibrator.PremiumIncludedDelta = term.QuoteTerm
        .DeltaPremiumSetting.Contains("Include");
      //calibrator.AdjustMarketButterfly = options.Contains("AdjustMarketButterfly");
      //var years = options.Where((s => s.ToLower().EndsWith("yearstouseforwardasatmstrike")))
      //  .Select(s => s.Substring(0, s.Length - "yearstouseforwardasatmstrike".Length).Trim())
      //  .FirstOrDefault();
      //if (!String.IsNullOrEmpty(years))
      //{
      //  calibrator.YearsToUseForwardAsAtmStrike = Double.Parse(years) - 0.01;
      //}
      var volatilitySurface = new CalibratedVolatilitySurface(asOf,
        tenors.ToArray(), calibrator, calibrator);
      volatilitySurface.Name = surfaceName;
      volatilitySurface.Fit();
      return volatilitySurface;
    }

    /// <summary>
    /// Fits a unified volatility surface from market quotes. A wide variety of methods and underlying assets are supported.
    /// </summary>
    /// <param name="asOf">Pricing (as-of) date</param>
    /// <param name="tenorNames">List of expiry tenor names. If empty, the parameter 'dates' must be non-empty</param>
    /// <param name="dates">List of expiry dates. If empty, they are inferred from the tenor names</param>
    /// <param name="strikes">List of strikes in either plain numbers, numbers plus call/put marks, delta specifications,
    ///   or risk-reversal and butterfly specifications</param>
    /// <param name="quotes">The matrix of quotes</param>
    /// <param name="quoteLayout">Quote layout in the format for "row=ROWFORMAT, col=COLFORMAT"</param>
    /// <param name="quoteType">Quote type, Eg StrikePrice, StickyStrike, StickyDelta, RiskReversalButterfly, Moneyness</param>
    /// <param name="underlying">Terms of the underlying asset</param>
    /// <param name="smileModel">The smile model, one of SplineInterpolation, QuadraticRegression, VannaVolga, Sabr or Heston</param>
    /// <param name="timeInterp">The method to interpolate the volatilities over time dimension</param>
    /// <param name="smileInterp">The method to interpolate the volatilities over strike dimension</param>
    /// <param name="fitSettings">Additional settings for volatility fitting</param>
    /// <param name="surfaceName">Surface Name</param>
    /// <returns>Calibrated unified volatility surface</returns>
    public static CalibratedVolatilitySurface FitVolatilitySurfaceFromQuotesWithLayout(
      Dt asOf,
      string[] tenorNames,
      Dt[] dates,
      object[] strikes,
      double[,] quotes,
      string quoteLayout,
      VolatilityQuoteType quoteType,
      IVolatilityUnderlying underlying,
      SmileModel smileModel,
      Interp timeInterp,
      Interp smileInterp,
      VolatilityFitSettings fitSettings,
      string surfaceName)
    {
      // Parse tenors and convert the quote layout into the standard format.
      var errmsg = VolatilitySurfaceFactory.CheckTenors(asOf, ref tenorNames, ref dates)
        ?? VolatilitySurfaceFactory.TryParseQuotes(quoteLayout, quoteType,
          ref tenorNames, ref dates, ref strikes, ref quotes);
      if (!String.IsNullOrEmpty(errmsg))
        throw new ArgumentException(errmsg);
      if ((dates.IsNullOrEmpty() && quotes.GetLength(0) != tenorNames.Length)
        || ((!dates.IsNullOrEmpty()) && quotes.GetLength(0) != dates.Length))
        throw new ArgumentException("maturities and volatilities not match.");
      if (quotes.GetLength(1) != strikes.Length)
        throw new ArgumentException("strikes and volatilities not match.");

      // Build the surface.
      return VolatilitySurfaceFactory.Create(asOf, tenorNames,
        dates, StrikeArray.Create(strikes, quoteType), quotes,
        quoteType, underlying, smileModel, smileInterp,
        timeInterp, fitSettings, surfaceName);
    }

    #endregion

    #region Quote layout parser

    private const string CallPutPattern = @"(call-?put|put-?call|payer-?receiver|receiver-?payer|payer|receiver|call|put)";

    /// <summary>
    /// Try to parse quotes based on the layout.
    /// </summary>
    /// <param name="quoteLayout">The quote layout as defined below</param>
    /// <param name="quoteType">Type of the quote</param>
    /// <param name="tenorNames">The tenors of the expiration dates</param>
    /// <param name="dates">The expiry dates matching the <paramref name="quotes"/></param>
    /// <param name="strikes">The strikes matching the <paramref name="quotes"/></param>
    /// <param name="quotes">The quotes of type <paramref name="quoteType"/> in a format defind by <paramref name="quoteLayout"/></param>
    /// <remarks>
    /// <para><b>Quote Layout</b></para>
    /// <para><paramref name="quoteLayout"/> specifies how the matrix of quotes is interpreted.
    /// Valid formats are:</para>
    /// <para>row=strikes|dates|expiries, col=strikes|dates|expiries[/call-put|/put-call|/call|/put]</para>
    /// <para>or</para>
    /// <para>row=strikes|dates|expiries, col=call-put|put-call|call|put</para>
    /// <para>Notes:</para>
    /// <list type="Bullet">
    ///   <item><description>Items separated by | are valid alternatives, items within [] are optional.</description></item>
    ///   <item><description>Expiries or expiry may be used, col or column may be used, call/payer or put/receiver may be used, and the hyphens are optional.</description></item>
    /// </list>
    /// 
    /// <para></para>
    /// <para><b>Examples</b></para>
    /// <para>Some examples of use include:</para>
    /// <para><i>quoteLayout</i>="row=strikes,column=dates/call-put", <i>quoteType</i>="strikePrice"</para>
    /// <para>Here each row is for a strike and each column is an expiry date grouped in pairs for call and put options. The quotes are option prices.</para>
    /// <table border="1" cellpadding="5">
    ///   <colgroup><col align="center"/><col align="center"/><col align="center"/><col align="center"/><col align="center"/><col align="center"/><col align="center"/></colgroup>
    ///   <tr><th></th><th colspan="2">16-Jan-13</th><th colspan="2">14-Feb-13</th><th colspan="2">15-Mar-13</th></tr>
    ///   <tr><td></td><td>Call</td><td>Put</td><td>Call</td><td>Put</td><td>Call</td><td>Put</td></tr>
    ///   <tr><td>80.00</td><td>4.66</td><td>2.19</td><td>6.17</td><td>3.03</td><td>7.46</td><td>3.71</td></tr>
    ///   <tr><td>80.50</td><td>4.34</td><td>2.37</td><td>5.86</td><td>3.22</td><td>7.75</td><td>3.90</td></tr>
    ///   <tr><td>81.00</td><td>4.04</td><td>2.57</td><td>5.55</td><td>3.41</td><td>6.85</td><td>4.10</td></tr>
    /// </table>
    /// <para><i>quoteLayout</i>="row=dates,column=strikes". <i>quoteType</i>="RiskReversalButterfly"</para>
    /// <para>Here each row is for an expiration date and columns are skew (AMT, Risk Reversal, Butterfly). The quotes are option volatilities.</para>
    /// <table border="1" cellpadding="5">
    ///   <colgroup><col align="center"/><col align="center"/><col align="center"/><col align="center"/><col align="center"/><col align="center"/><col align="center"/></colgroup>
    ///   <tr><th></th><th>ATM</th><th>25RR</th><th>25BF</th></tr>
    ///   <tr><td>10-Aug-13</td><td>15.55%</td><td>-3.25%</td><td>0.42%</td></tr>
    ///   <tr><td>10-Sep-13</td><td>15.75%</td><td>-3.55%</td><td>0.47%</td></tr>
    ///   <tr><td>10-Dec-13</td><td>15.90%</td><td>-3.85%</td><td>0.59%</td></tr>
    /// </table>
    /// 
    /// <para></para>
    /// <para><b>Underlying Asset Terms</b></para>
    /// <para>A number of underlyings are supported, including</para>
    /// <list type="table">
    ///     <item><term>Stock</term><description>A Stock. See <see cref="StockVolatilityUnderlying"/></description></item>
    ///     <item><term>Cap/floor</term><description>A Cap/Floor. See <see cref="CapVolatilityUnderlying"/></description></item>
    ///     <item><term>FX</term><description>A spot for forward FX. See <see cref="FxVolatilityUnderlying"/></description></item>
    ///     <item><term>CDX</term><description>A CDX. See <see cref="CdxVolatilityUnderlying"/></description></item>
    ///     <item><term>Discount rate and yield</term><description>A general underlying asset specified by a discount rate and yield. See <see cref="VolatilityUnderlying"/></description></item>
    ///   </list>
    /// </remarks>
    /// <returns>null if parse ok or error message if not</returns>
    public static string TryParseQuotes(
      string quoteLayout,
      VolatilityQuoteType quoteType,
      ref string[] tenorNames, ref Dt[] dates,
      ref object[] strikes, ref double[,] quotes)
    {
      if (String.IsNullOrWhiteSpace(quoteLayout) || quotes == null)
        return null;

      var match = Regex.Match(quoteLayout,
        @"^\s*row=(strikes?|dates?|expir(?:ies|y))\s*,\s*" +
          @"col(?:umn)?=(strikes?|dates?|expir(?:ies|y))(?:/"
            + CallPutPattern + @")?\s*$",
        RegexOptions.IgnoreCase);
      if (!match.Success)
      {
        return TryParseStackedQuotes(quoteLayout, quoteType,
          ref tenorNames, ref dates, ref strikes, ref quotes);
      }

      bool rowByStrike = match.Groups[1].Value.StartsWith(
        "strike", StringComparison.OrdinalIgnoreCase);
      bool colByStrike = match.Groups[2].Value.StartsWith(
        "strike", StringComparison.OrdinalIgnoreCase);
      if (rowByStrike == colByStrike)
      {
        return String.Format("Invalid layout: {0}", quoteLayout);
      }

      if (String.IsNullOrEmpty(match.Groups[3].Value))
      {
        if (rowByStrike) quotes = quotes.Transpose();
        return null;
      }

      OptionType o1, o2;
      ParseOptionTypesSpec(match.Groups[3].Value, out o1, out o2);
      if (o2 == OptionType.None)
      {
        for(int i = 0; i < strikes.Length; ++i)
        {
          double k = ParseDouble(strikes[i]);
          strikes[i] = StrikeSpec.Create(k, o1);
        }
        if (rowByStrike) quotes = quotes.Transpose();
        return null;
      }

      int nrow = quotes.GetLength(0), ncol = quotes.GetLength(1);
      if (ncol % 2 != 0)
        return "# of columns must be even with call-put layout";
      int halfcols = ncol / 2;

      object[] specs;
      if (rowByStrike)
      {
        if (nrow != strikes.Length)
          return "# of rows and strikes not match";
        if (ncol != dates.Length)
          return "# of columns and (expiry) dates not match";
        specs = new object[2 * nrow];
        var data = new double[halfcols,2 * nrow];
        for (int i = 0; i < nrow; ++i)
        {
          double k = ParseDouble(strikes[i]);
          int c = 2 * i;
          specs[c] = StrikeSpec.Create(k, o1);
          specs[c + 1] = StrikeSpec.Create(k, o2);
          for (int r = 0; r < halfcols; ++r)
          {
            data[r, c] = quotes[i, 2 * r];
            data[r, c + 1] = quotes[i, 2 * r + 1];
          }
        }
        quotes = data;
        strikes = specs;
        dates = dates.Where((d, i) => i % 2 == 0).ToArray();
        tenorNames = tenorNames.Where((d, i) => i % 2 == 0).ToArray();
        return null;
      }

      // Handle the case of rows by dates.
      if (ncol != strikes.Length)
        return "# of columns and strikes not match";
      specs = new object[ncol];
      for (int i = 0; i < halfcols; ++i)
      {
        var k = ParseDouble(strikes[2 * i]);
        specs[2 * i] = StrikeSpec.Create(k, OptionType.Call);
        specs[2 * i + 1] = StrikeSpec.Create(k, OptionType.Put);
      }
      strikes = specs;
      return null;
    }

    private static string TryParseStackedQuotes(
      string quoteLayout,
      VolatilityQuoteType quoteType,
      ref string[] tenorNames, ref Dt[] dates,
      ref object[] strikes, ref double[,] quotes)
    {
      var match = Regex.Match(quoteLayout,
        @"^\s*row=((?:dates?|expir(?:ies|y))/strikes?)\s*,\s*" +
          @"col(?:umn)?=" + CallPutPattern + @"\s*$",
        RegexOptions.IgnoreCase);
      if (!match.Success)
      {
        return String.Format("Invalid layout: {0}", quoteLayout);
      }

      OptionType o1, o2;
      ParseOptionTypesSpec(match.Groups[2].Value, out o1, out o2);

      // Check quotes consistency
      int nrow = quotes.GetLength(0), ncol = quotes.GetLength(1);
      if (nrow > 0 && ncol == 2)
      {
        if (quoteType != VolatilityQuoteType.StrikePrice)
          quotes = AverageByRows(quotes, nrow);
      }
      else if (ncol != 1 || nrow == 0)
      {
        return "quotes and layout not match";
      }
      else if (o1 != OptionType.None || o2 != OptionType.None)
      {
        return String.Format("{0} layout requires 2 columns",
          match.Groups[2].Value);
      }
      else if (quoteType == VolatilityQuoteType.StrikePrice
        && o1 == OptionType.None)
      {
        return "price quotes must specify call-put or payer-receiver";
      }

      if (dates.Length != nrow)
      {
        return "expiries and quotes not match";
      }
      if (strikes.Length != nrow)
      {
        return "expiries and quotes not match";
      }

      // Reformat quotes into the desired layout.
      bool twoColumns = quotes.GetLength(1) == 2;
      var kset = strikes.Distinct().Select(ParseDouble)
        .OrderBy(k => k).Cast<object>().ToArray();
      var dset = dates.Distinct().OrderBy(d => d).ToArray();
      int dcount = dset.Length, kcount = kset.Length;
      var names = new string[dcount];
      var data = new double[dcount,twoColumns ? (kcount * 2) : kcount];

      Dt expiry = Dt.Empty;
      for (int i = 0; i < nrow; ++i)
      {
        int row;
        var date = dates[i];
        if (!date.IsEmpty())
        {
          expiry = date;
          row = Array.BinarySearch(dset, date);
          names[row] = tenorNames[i];
        }
        else
        {
          date = expiry;
          row = Array.BinarySearch(dset, date);
        }

        var quote = quotes[i, 0];
        if (expiry.IsEmpty() || !(quote > 0
          || (twoColumns && quotes[i, 1] > 0)))
        {
          continue;
        }

        int col = Array.BinarySearch(kset, strikes[i]);
        data[row, col] = quote;
        if (twoColumns) data[row, col + kcount] = quotes[i, 1];
      }
      quotes = data;

      if (twoColumns) kset = CombineStrikesAndTypes(kset, o1, o2);
      strikes = kset;

      dates = dset;
      tenorNames = names;
      return null;
    }

    private static object[] CombineStrikesAndTypes(object[] strikes,
      OptionType otype1, OptionType otype2)
    {
      int n = strikes.Length;
      var tmp = new object[2 * n];
      for (int i = 0; i < n; ++i)
      {
        var k = ParseDouble(strikes[i]);
        tmp[i] = StrikeSpec.Create(k, otype1);
        tmp[i + n] = StrikeSpec.Create(k, otype2);
      }
      return tmp;
    }

    private static double[,] AverageByRows(double[,] quotes, int nrow)
    {
      var tmp = new double[nrow,1];
      for (int i = 0; i < nrow; ++i)
      {
        if (!(quotes[i, 0] > 0)) tmp[i, 0] = quotes[i, 1];
        else if (!(quotes[i, 1] > 0)) tmp[i, 0] = quotes[i, 0];
        else tmp[i, 0] = (quotes[i, 0] + quotes[i, 1]) / 2;
      }
      return tmp;
    }

    private static void ParseOptionTypesSpec(string s,
      out OptionType o1, out OptionType o2)
    {
      // s is one of "call-put", "payer-receiver", "call", "put", "payer", "receiver".

      if (s.StartsWith("payer", StringComparison.OrdinalIgnoreCase))
      {
        o1 = OptionType.Call;
        o2 = s.EndsWith("receiver", StringComparison.OrdinalIgnoreCase)
          ? OptionType.Put
          : OptionType.None;
      }
      else if (s.StartsWith("receiver", StringComparison.OrdinalIgnoreCase))
      {
        o1 = OptionType.Put;
        o2 = s.EndsWith("payer", StringComparison.OrdinalIgnoreCase)
          ? OptionType.Call
          : OptionType.None;
      }
      else if (s.StartsWith("call", StringComparison.OrdinalIgnoreCase))
      {
        o1 = OptionType.Call;
        o2 = s.EndsWith("put", StringComparison.OrdinalIgnoreCase)
          ? OptionType.Put
          : OptionType.None;
      }
      else if (s.StartsWith("put", StringComparison.OrdinalIgnoreCase))
      {
        o1 = OptionType.Put;
        o2 = s.EndsWith("call", StringComparison.OrdinalIgnoreCase)
          ? OptionType.Call
          : OptionType.None;
      }
      else
      {
        o1 = OptionType.None;
        o2 = OptionType.None;
      }
    }

    private static double ParseDouble(object input)
    {
      if (input is double)
        return (double)input;
      if (input == null)
        return Double.NaN;
      var s = input.ToString();
      double d;
      if (!Double.TryParse(s, out d))
        return Double.NaN;
      return d;
    }

    /// <summary>
    /// Checks the tenors and fills in the missing dates based on the tenor names.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="dates">The dates.</param>
    /// <returns>null on seccess, or an error string on error.</returns>
    public static string CheckTenors(Dt asOf,
      ref string[] tenorNames, ref Dt[] dates)
    {
      bool namesEmpty = tenorNames.IsNullOrEmpty(),
        datesEmpty = dates.IsNullOrEmpty();
      if (namesEmpty && datesEmpty)
        return "tenor dates and names cannot be both empty";

      if (namesEmpty)
      {
        tenorNames = dates.Select(d => d.IsEmpty()
          ? null
          : Tenor.FromDateInterval(asOf, d).ToString()).ToArray();
        return null;
      }

      var count = tenorNames.Length;
      if (!datesEmpty)
      {
        if (dates.Length != count)
        {
          return String.Format("Inconsistent # of dates ({0}) and tenor names ({1})",
            dates.Length, count);
        }
        return null;
      }

      var tmp = new Dt[count];
      for (int i = 0; i < count; ++i)
      {
        var s = tenorNames[i];
        if (String.IsNullOrWhiteSpace(s)) continue;
        Tenor t;
        if (!Tenor.TryParse(s, out t))
          return String.Format("Invalid tenor {0}", s);
        tmp[i] = Dt.Add(asOf, t.N, t.Units);
      }
      dates = tmp;
      return null;
    }

    #endregion

    #region Tenors builders

    /// <summary>
    /// Builds the tenors.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="specs">The specs.</param>
    /// <param name="quotes">The quotes.</param>
    /// <param name="quoteType">Type of the quote.</param>
    /// <param name="term">The term.</param>
    /// <param name="fitSettings">The volatility fit settings.</param>
    /// <returns>IEnumerable{IVolatilityTenor}.</returns>
    internal static IEnumerable<IVolatilityTenor> BuildTenors(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      StrikeArray specs,
      double[,] quotes,
      VolatilityQuoteType quoteType,
      IVolatilityUnderlying term,
      VolatilityFitSettings fitSettings)
    {
      Func<string, Dt, double[], double[], IVolatilityTenor> creator;
      switch (quoteType)
      {
      case VolatilityQuoteType.StickyDelta:
        return BuildDeltaTenors(asOf, tenorNames, expiries,
          specs.AsDeltaSpec(), quotes, term);

      case VolatilityQuoteType.RiskReversalButterfly:
        return BuildRrBfTenors(asOf, tenorNames, expiries,
          specs.AsRrBfSpec(), quotes, term);

      case VolatilityQuoteType.StrikePrice:
        return BuildPriceTenors(asOf, tenorNames, expiries,
          specs.AsStrikeSpec(), quotes, term == null ? null : term.Deflator,
          fitSettings == null ? 1E-8 : fitSettings.ImpliedVolatilityAccuracy);

      case VolatilityQuoteType.Moneyness:
        creator = (s, d, m, v) => new MoneynessVolatilityTenor(s, d, m, v);
        break;

      case VolatilityQuoteType.StickyStrike:
        creator = (s, d, k, v) => new StickyStrikeVolatilityTenor(s, d, k, v);
        break;

      default:
        creator = (s, d, k, v) => new PlainVolatilityTenor(s, d)
        {
          Strikes = k,
          Volatilities = v
        };
        break;
      }
      var numbers = specs.AsNumbers();
      if (numbers != null)
      {
        return BuildTenors(asOf, tenorNames, expiries, numbers, quotes,
          (k, v) => Double.IsNaN(k) || !(v > 0), // skip invalid quotes
          creator);
      }
      return BuildTenors(asOf, tenorNames,
        expiries, specs.AsStrikeSpec(), quotes,
        (k, v) => k.Type == OptionType.None || Double.IsNaN(k.Strike)
          || !(v > 0), // skip invalid quotes
        (s, d, k, v) => BuildCallPutVolatilityTenor(s, d, k, v, creator));
    }

    /// <summary>
    ///  Generic routine to builds the tenors with the specified delegates.
    /// </summary>
    /// <typeparam name="TStrike">The type of the T strike.</typeparam>
    /// <param name="asOf">As of.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="quotes">The quotes.</param>
    /// <param name="needSkip">The need skip.</param>
    /// <param name="createTenor">The create tenor.</param>
    /// <returns>IEnumerable{IVolatilityTenor}.</returns>
    /// <exception cref="ToolkitException">Invalid strikes.  Must be strike numbers.</exception>
    /// <exception cref="System.ArgumentException">maturities are out of order.</exception>
    internal static IEnumerable<IVolatilityTenor> BuildTenors<TStrike>(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      TStrike[] strikes,
      double[,] quotes,
      Func<TStrike, double, bool> needSkip,
      Func<string, Dt, TStrike[], double[], IVolatilityTenor> createTenor)
    {
      if (strikes == null)
      {
        throw new ToolkitException("Invalid strikes.  Must be strike numbers.");
      }
      for (int t = 0; t < expiries.Length; ++t)
      {
        if (expiries[t].IsEmpty()) continue;
        if (t > 0 && expiries[t - 1] >= expiries[t])
        {
          throw new ArgumentException("maturities are out of order.");
        }
        var ordered = strikes
          .Select((k, i) => new KeyValuePair<TStrike, double>(k, quotes[t, i]))
          .Where(o => !needSkip(o.Key, o.Value))
          .OrderBy(o => o.Key).ToList();
        if (ordered.Count <= 0) continue;
        var name = tenorNames == null || tenorNames.Length == 0
          ? Tenor.FromDateInterval(asOf, expiries[t]).ToString()
          : tenorNames[t];
        yield return createTenor(name, expiries[t],
          ordered.Select(o => o.Key).ToArray(),
          ordered.Select(o => o.Value).ToArray());
      }
    }

    /// <summary>
    /// Builds a tenor by taking the average of the call and put volatilities.
    /// </summary>
    /// <param name="tenorName">Name of the tenor.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="quotes">The quotes.</param>
    /// <param name="createTenor">The create tenor.</param>
    /// <returns>IVolatilityTenor.</returns>
    /// <exception cref="System.ArgumentException">strikes and quotes not match</exception>
    internal static IVolatilityTenor BuildCallPutVolatilityTenor(
      string tenorName,
      Dt expiry,
      StrikeSpec[] strikes,
      double[] quotes,
      Func<string, Dt, double[], double[], IVolatilityTenor> createTenor)
    {
      if (strikes == null || strikes.Length == 0)
        return null;
      if (quotes == null || quotes.Length != strikes.Length)
        throw new ArgumentException("strikes and quotes not match");
      List<double> ks = new List<double>(), vs = new List<double>();
      for (int i = 0, last = strikes.Length - 1; i <= last; ++i)
      {
        var v = quotes[i];
        if (!(v > 0)) continue;
        var k = strikes[i];
        if (k.Type == OptionType.None) continue;
        ks.Add(k.Strike);
        if (i < last && k.Strike.AlmostEquals(strikes[i + 1].Strike) 
          && quotes[i + 1] > 0)
        {
          vs.Add(0.5 * (v + quotes[i + 1]));
          ++i;
        }
        else
          vs.Add(v);
      }
      return createTenor(tenorName, expiry, ks.ToArray(), vs.ToArray());
    }

    private static IEnumerable<IVolatilityTenor> BuildPriceTenors(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      StrikeSpec[] strikes,
      double[,] quotes,
      ISpotCurve deflator,
      double accuracy)
    {
      return BuildTenors(asOf, tenorNames, expiries, strikes, quotes,
        // Skip the empty data point.
        (s, v) => s.IsEmpty || !(v > 0),
        // Create a volatility tenor.
        (name, date, k, v) => new StrikePriceVolatilityTenor(name, date,
          k, v, deflator == null ? 1.0 : deflator.Interpolate(date),
          accuracy));
    }

    private static IEnumerable<IVolatilityTenor> BuildDeltaTenors(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      DeltaSpec[] deltas,
      double[,] quotes,
      IVolatilityUnderlying term)
    {
      var ft = term as FxVolatilityUnderlying;
      var qt = ft != null ? ft.QuoteTerm : null;
      return BuildTenors(asOf, tenorNames, expiries, deltas, quotes,
        // Skip the empty data point.
        (s, v) => s.IsEmpty || !(v > 0),
        // Create a volatility tenor.
        (name, date, k, v) =>
        {
          var tenor = Tenor.FromDateInterval(asOf, date);
          return new StickyDeltaVolatilityTenor(name, date, k, v,
            qt == null ? AtmKind.DeltaNeutral : qt.GetAtmKind(tenor),
            qt == null ? DeltaStyle.None : qt.GetDeltaStyle(tenor));
        });
    }

    private static IEnumerable<IVolatilityTenor> BuildRrBfTenors(
      Dt asOf,
      string[] tenorNames,
      Dt[] expiries,
      FxRrBfSpec[] specs,
      double[,] quotes,
      IVolatilityUnderlying term)
    {
      var ft = term as FxVolatilityUnderlying;
      var qt = ft != null ? ft.QuoteTerm : null;
      return BuildTenors(asOf, tenorNames, expiries, specs, quotes,
        // Skip the empty data point.
        (s, v) => s.IsEmpty || (s.IsAtm && !(v > 0)),
        // Create a volatility tenor.
        (name, date, ss, vv) =>
        {
          var tenor = Tenor.FromDateInterval(asOf, date);
          return new FxRrBfVolatilityTenor.Builder(
            ss.Zip(vv, (s, v) => new KeyValuePair<FxRrBfSpec, double>(s, v)))
            .ToFxRrBfVolatilityTenor(name, date,
              qt == null ? FxVolatilityQuoteFlags.None : qt.GetFlags(tenor));
        });
    }

    #endregion

    #region Extract ATM volatility

    /// <summary>
    ///   Return the ATM volatility curve.
    /// </summary>
    /// <returns>The ATM volatility curve.</returns>
    internal static VolatilityCurve GetAtmVolatilityCurve(this VolatilitySurface surface)
    {
      var csurface = surface as CalibratedVolatilitySurface;
      if (csurface == null)
        throw new ToolkitException("Require calibrated volatility surface");
      var curve = new VolatilityCurve(csurface.AsOf);
      var tenors = csurface.Tenors;
      if (tenors.Length == 0)
      {
        foreach (var date in GetTenorDates(surface))
        {
          curve.Add(date, GetAtmVolatility(csurface, date));
        }
      }
      else
      {
        for (int i = 0; i < tenors.Length; i++)
        {
          var date = tenors[i].Maturity;
          curve.Add(date, csurface.GetAtmVolatility(tenors[i]));
        }
      }
      if (curve.Count == 0)
        throw new ToolkitException("No date points found in volatility surface");
      return curve;
    }

    internal static double GetAtmVolatility(
      this VolatilitySurface surface, IVolatilityTenor tenor)
    {
      if (tenor == null)
        throw new ArgumentNullException("tenor");

      double v = 0;
      if (GetAtmVolatility(tenor as MoneynessVolatilityTenor, ref v)
        || GetAtmVolatility(tenor as StickyDeltaVolatilityTenor, ref v)
          || GetAtmVolatility(tenor as IVolatilityLevelHolder, ref v)
            || GetAtmVolatility(tenor as PlainVolatilityTenor, ref v)
              || GetAtmVolatility(surface as CalibratedVolatilitySurface,
                tenor, ref v))
      {
        return v;
      }
      throw new ArgumentException("Unable to find ATM volatility");
    }

    private static bool GetAtmVolatility(
      IVolatilityLevelHolder tenor, ref double volatility)
    {
      if (tenor == null) return false;
      volatility = tenor.Level;
      return true;
    }

    private static bool GetAtmVolatility(
      StickyDeltaVolatilityTenor tenor, ref double volatility)
    {
      if (tenor == null) return false;
      volatility = tenor.AtmQuote;
      return true;
    }

    private static bool GetAtmVolatility(
      MoneynessVolatilityTenor tenor, ref double volatility)
    {
      if (tenor == null || tenor.Moneyness == null)
        return false;
      var idx = Array.IndexOf(tenor.Moneyness, 1.0);
      if (idx < 0) return false;
      volatility = tenor.Volatilities[idx];
      return true;
    }

    private static bool GetAtmVolatility(
      PlainVolatilityTenor tenor, ref double volatility)
    {
      if (tenor != null && tenor.Volatilities != null
        && tenor.Volatilities.Count == 1)
      {
        volatility = tenor.Volatilities[0];
        return true;
      }
      return false;
    }

    private static bool GetAtmVolatility(
      CalibratedVolatilitySurface surface,
      IVolatilityTenor tenor, ref double volatility)
    {
      if (surface == null) return false;

      var calibrator = surface.Calibrator as IBlackScholesParameterDataProvider;
      if (calibrator == null) return false;

      var data = calibrator.GetParameters(tenor.Maturity);
      if (data == null || Double.IsNaN(data.Spot)) return false;

      volatility = surface.Interpolate(tenor.Maturity,
        data.GetForward(), SmileInputKind.Strike);
      return true;
    }

    private static double GetAtmVolatility(
      CalibratedVolatilitySurface surface,
      Dt date)
    {
      var extended = surface.Interpolator
        as IExtendedVolatilitySurfaceInterpolator;
      if (extended != null)
        return extended.Interpolate(surface, date, 1.0, SmileInputKind.Moneyness);
      throw new ToolkitException("Unable to find a method for ATM volatility");
    }

    #endregion

    #region Extract tenor dates

    internal static IEnumerable<Dt> GetTenorDates(this VolatilitySurface surface)
    {
      var csurface = surface as CalibratedVolatilitySurface;
      if (csurface != null)
      {
        if (csurface.Tenors.IsNullOrEmpty())
        {
          return GetComponenSurfaces(csurface)
            .SelectMany(GetTenorDatesFromSingleSurface)
            .OrderBy(dt => dt)
            .Distinct();
        }
        return GetTenorDatesFromSingleSurface(csurface);
      }
      return GetTenorDatesFromTimePoints(surface);
    }

    private static IEnumerable<Dt> GetTenorDatesFromSingleSurface(
      CalibratedVolatilitySurface csurface)
    {
      foreach (var tenor in csurface.Tenors)
      {
        yield return tenor.Maturity;
        var rate = tenor as RateVolatilityTenor;
        if (rate != null && !rate.ForwardTenor.IsEmpty)
        {
          yield return Dt.Add(tenor.Maturity, rate.ForwardTenor);
        }
      }
    }

    private static IEnumerable<Dt> GetTenorDatesFromTimePoints(
      VolatilitySurface surface)
    {
      var points = GetTimePoints(surface);
      if (points != null && points.Count != 0)
      {
        Dt asOf = surface.AsOf;
        foreach (var t in points)
        {
          yield return new Dt(asOf, t);
        }
        yield break;
      }
      throw new ToolkitException("No date points found in volatility surface");
    }

    private static IList<double> GetTimePoints(VolatilitySurface surface)
    {
      var surfaceInterpolator = surface.Interpolator
        as VolatilitySurfaceInterpolator;
      if (surfaceInterpolator == null) return null;
      var seqInterpolator = surfaceInterpolator.SurfaceFunction.Target
        as SequentialInterpolator2D;
      if (seqInterpolator == null) return null;
      return seqInterpolator.Points;
    }

    #endregion

    #region Extract component surfaces

    internal static IEnumerable<CalibratedVolatilitySurface> GetComponenSurfaces(
      CalibratedVolatilitySurface surface)
    {
      yield return surface;

      var cal = surface.Calibrator as IVolatilitySurfaceProvider;
      if (cal != null)
      {
        foreach (var vs in cal.GetVolatilitySurfaces())
        {
          var cvs = vs as CalibratedVolatilitySurface;
          if (cvs != null) yield return cvs;
        }
      }
    }

    #endregion
  }
}
