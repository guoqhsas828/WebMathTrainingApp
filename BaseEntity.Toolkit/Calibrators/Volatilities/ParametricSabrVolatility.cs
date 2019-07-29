using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Class ParametricSabrVolatility.
  /// </summary>
  public static class ParametricSabrVolatility
  {
    #region Static constructor

    /// <summary>
    /// Creates the rate volatility surface from the SABR parameters.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="beta">The beta.</param>
    /// <param name="nu">The nu.</param>
    /// <param name="rho">The rho.</param>
    /// <param name="projectionCurve">The projection curve.</param>
    /// <param name="referenceIndex">Index of the reference.</param>
    /// <returns>CalibratedVolatilitySurface.</returns>
    public static CalibratedVolatilitySurface CreateSurface(
      Dt asOf, Curve alpha, Curve beta, Curve nu, Curve rho,
      DiscountCurve projectionCurve,
      ReferenceIndex referenceIndex)
    {
      if (projectionCurve == null)
        throw new ArgumentException($"{nameof(projectionCurve)} is null");
      if (referenceIndex == null)
        throw new ArgumentException($"{nameof(referenceIndex)} is null");
      var forwardCalculator = new RateForwardCalculator(
        projectionCurve, referenceIndex);
      var tenors = GetTenors(alpha, beta, nu, rho).ToArray();
      var calibrator = new SabrCalibrator(
        forwardCalculator.GetForward, alpha, beta, nu, rho);
      return new Surface(asOf, tenors, calibrator);
    }

    /// <summary>
    /// Creates the price volatility surface from SABR parameters
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="alpha">The alpha curve</param>
    /// <param name="beta">The beta curve</param>
    /// <param name="nu">The nu curve</param>
    /// <param name="rho">The rho curve</param>
    /// <param name="forwardPriceCurve">The forward price curve.</param>
    /// <returns>CalibratedVolatilitySurface.</returns>
    public static CalibratedVolatilitySurface CreateSurface(
      Dt asOf,
      Curve alpha, Curve beta, Curve nu, Curve rho,
      Curve forwardPriceCurve)
    {
      var tenors = GetTenors(alpha, beta, nu, rho).ToArray();
      var calibrator = new SabrCalibrator(
        forwardPriceCurve.Interpolate, alpha, beta, nu, rho);
      return new CalibratedVolatilitySurface(asOf,
        tenors, calibrator, SabrInterpolator.Instance);
    }

    /// <summary>
    /// Gets the specialized tenors which allow 
    ///  the sensitivity routines to bump SABR parameters.
    /// </summary>
    /// <param name="alpha">The alpha curve</param>
    /// <param name="beta">The beta curve</param>
    /// <param name="nu">The nu curve</param>
    /// <param name="rho">The rho curve</param>
    /// <returns>IEnumerable&lt;IVolatilityTenor&gt;.</returns>
    internal static IEnumerable<IVolatilityTenor> GetTenors(
      Curve alpha, Curve beta, Curve nu, Curve rho)
    {
      return GetTenors("Alpha", alpha)
        .Concat(GetTenors("Beta", beta))
        .Concat(GetTenors("Nu", nu))
        .Concat(GetTenors("Rho", rho));
    }

    /// <summary>
    /// Gets the specialized tenors such that 
    ///   bumping the tenors directly bumps the linked point.
    /// </summary>
    /// <param name="kind">The parameter kind</param>
    /// <param name="curve">The curve</param>
    /// <returns>IEnumerable&lt;IVolatilityTenor&gt;.</returns>
    private static IEnumerable<IVolatilityTenor> GetTenors(
      string kind, Curve curve)
    {
      for (int i = 0, n = curve.Count; i < n; ++i)
        yield return new CurvePoint(kind, curve, i);
    }

    #endregion

    #region Nested type: RateForwardCalculator

    [Serializable]
    class RateForwardCalculator : IIndexedForwardCalculator
    {
      internal RateForwardCalculator(
        DiscountCurve projectionCurve,
        ReferenceIndex referenceIndex)
      {
        ProjectionCurve = projectionCurve;
        ReferenceIndex = referenceIndex;
      }

      private DiscountCurve ProjectionCurve { get; }

      private ReferenceIndex ReferenceIndex { get; }

      internal double GetForward(Dt expiry)
      {
        return GetForward(expiry, ReferenceIndex);
      }

      public double GetForward(Dt expiry, ReferenceIndex referenceIndex)
      {
        if (referenceIndex == null) referenceIndex = ReferenceIndex;

        var swapRateIndex = referenceIndex as SwapRateIndex;
        if (swapRateIndex != null)
        {
          Dt maturity = Dt.Add(expiry, swapRateIndex.IndexTenor);
          var curve = ProjectionCurve;
          var projector = new SwapRateCalculator(
            curve.AsOf, swapRateIndex, curve);
          var fixingSchdule = projector.GetFixingSchedule(Dt.Empty,
            expiry, maturity, maturity);
          var fixing = projector.Fixing(fixingSchdule);
          return fixing.Forward;
        }
        var irIndex = referenceIndex as InterestRateIndex;
        if (irIndex != null)
        {
          var maturity = Dt.Add(expiry, referenceIndex.IndexTenor);
          var curve = ProjectionCurve;
          return curve.F(expiry, maturity, irIndex.DayCount, Frequency.None);
        }
        throw new ToolkitException($"Index {referenceIndex} not supported yet");
      }
    }

    interface IIndexedForwardCalculator
    {
      double GetForward(Dt maturity, ReferenceIndex referenceIndex);
    }

    #endregion

    #region Nested type: Surface with forward models

    [Serializable]
    class Surface : CalibratedVolatilitySurface
      , IVolatilityCalculator, IModelParameter
    {
      public Surface(Dt asOf,
        IVolatilityTenor[] tenors,
        SabrCalibrator calibrator,
        DistributionType distribution = DistributionType.LogNormal,
        SmileInputKind kind = SmileInputKind.Strike)
        : base(asOf, tenors, calibrator, SabrInterpolator.Instance, kind)
      {
        Debug.Assert(calibrator.ForwardCalculator.Target is IIndexedForwardCalculator);
        DistributionType = distribution;
      }

      public double Interpolate(Dt expiry, double strike, ReferenceIndex referenceIndex)
      {
        var time = (expiry - AsOf)/365.25;
        var forward = GetForward(expiry, referenceIndex);
        var vol = ((SabrCalibrator) Calibrator).Evaluate(time, expiry, forward, strike);
        return DistributionType == DistributionType.LogNormal ? vol :
          LogNormalToNormalConverter.ConvertCapletVolatility(forward, strike,
            time, vol, VolatilityType.LogNormal, VolatilityType.Normal);
      }

      private double GetForward(Dt expiry, ReferenceIndex referenceIndex)
      {
        var calc = (Calibrator as SabrCalibrator)?.ForwardCalculator.Target
          as IIndexedForwardCalculator;
        if (calc == null)
        {
          throw new ToolkitException("null forward calculator");
        }
        return calc.GetForward(expiry, referenceIndex);
      }

      public DistributionType DistributionType { get; }

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
          var expiry = Dt.AddDays(dd.Date,
            swpn.NotificationDays, swpn.NotificationCalendar);
          return ((SabrCalibrator)Calibrator).Evaluate(
            AsOf, expiry, rate, strike);
        }
        throw new NotImplementedException();
      }

      #endregion
    }

    #endregion

    #region Nested type: Calibrator

    /// <summary>
    /// Class Calibrator.
    /// </summary>
    /// <seealso cref="IVolatilitySurfaceCalibrator" />
    [Serializable]
    class SabrCalibrator : IVolatilitySurfaceCalibrator
    {
      /// <summary>
      /// Fit a surface from the specified tenor point
      /// </summary>
      /// <param name="surface">The volatility surface to fit.</param>
      /// <param name="fromTenorIdx">The tenor index to start fit.</param>
      /// <remarks><para>Derived calibrators implement this to do the work of the fitting.</para>
      /// <para>Called by Fit() and Refit(), it can be assumed that the tenors have been validated.</para>
      /// <para>When the start index <paramref name="fromTenorIdx" /> is 0,
      /// a full fit is requested.  Otherwise a partial fit start from
      /// the given tenor is requested.  The derived class can do either
      /// a full fit a a partial fit as it sees appropriate.</para></remarks>
      public void FitFrom(CalibratedVolatilitySurface surface, int fromTenorIdx)
      {
        /* Do nothing */
      }

      internal double Evaluate(Dt asOf, Dt expiry, double strike)
      {
        var forward = ForwardCalculator(expiry);
        return Evaluate(asOf, expiry, forward, strike);
      }

      internal double Evaluate(Dt asOf, Dt expiry,
        double smileInputValue, SmileInputKind smileInputKind)
      {
        var forward = ForwardCalculator(expiry);
        switch (smileInputKind)
        {
        case SmileInputKind.Strike:
          return Evaluate(asOf, expiry, forward, smileInputValue);
        case SmileInputKind.Moneyness:
          return Evaluate(asOf, expiry, forward, forward*smileInputValue);
        case SmileInputKind.LogMoneyness:
          return Evaluate(asOf, expiry, forward, forward*Math.Exp(smileInputValue));
        }
        throw new ArgumentException($"Unknown smile kind {smileInputKind}");
      }

      internal double Evaluate(Dt asOf, Dt expiry, double forward, double strike)
      {
        return Evaluate((expiry - asOf) / 365.25, expiry, forward, strike);
      }

      internal double Evaluate(double time, Dt expiry, double forward, double strike)
      {
        var alpha = Alpha.Interpolate(expiry);
        var beta = Beta.Interpolate(expiry);
        var nu = Nu.Interpolate(expiry);
        var rho = Rho.Interpolate(expiry);
        return SabrVolatilityEvaluator.CalculateVolatility(
          alpha, beta, rho, nu, forward, strike, time);
      }

      /// <summary>Gets the alpha</summary>
      private Curve Alpha { get; }

      /// <summary>Gets the Beta</summary>
      private Curve Beta { get; }

      /// <summary>Gets the Nu</summary>
      private Curve Nu { get; }

      /// <summary>Gets the Rho</summary>
      private Curve Rho { get; }

      /// <summary>Gets the forward calculator</summary>
      internal Func<Dt, double> ForwardCalculator { get; }

      internal SabrCalibrator(Func<Dt, double> fwdCalculator,
        Curve alpha, Curve beta, Curve nu, Curve rho)
      {
        Alpha = alpha;
        Beta = beta;
        Nu = nu;
        Rho = rho;
        ForwardCalculator = fwdCalculator;
      }

    }

    #endregion

    #region Nested type: Interpolator

    /// <summary>
    /// Class Interpolator.
    /// </summary>
    /// <seealso cref="BaseEntity.Toolkit.Curves.Volatilities.SabrVolatilityEvaluator" />
    /// <seealso cref="BaseEntity.Toolkit.Curves.Volatilities.IExtendedVolatilitySurfaceInterpolator" />
    class SabrInterpolator : IExtendedVolatilitySurfaceInterpolator
    {
      internal static readonly SabrInterpolator Instance = new SabrInterpolator();

      private SabrCalibrator GetCalibrator(VolatilitySurface surface)
      {
        var cal = (surface as CalibratedVolatilitySurface)?.Calibrator as SabrCalibrator;
        if (cal == null)
        {
          throw new ToolkitException("Inconsistent calibrator");
        }
        return cal;
      }

      /// <summary>
      /// Interpolates a volatility at the specified date and strike.
      /// </summary>
      /// <param name="surface">The volatility surface.</param>
      /// <param name="expiry">The expiry date.</param>
      /// <param name="strike">The strike.</param>
      /// <returns>The volatility at the given date and strike.</returns>
      public double Interpolate(VolatilitySurface surface, Dt expiry, double strike)
      {
        return GetCalibrator(surface).Evaluate(surface.AsOf, expiry, strike);
      }

      /// <summary>
      /// Interpolates the specified surface.
      /// </summary>
      /// <param name="surface">The surface.</param>
      /// <param name="expiry">The expiry date</param>
      /// <param name="smileInputValue">The smile input value.</param>
      /// <param name="smileInputKind">Kind of the smile input.</param>
      /// <returns>System.Double.</returns>
      /// <exception cref="System.ArgumentException"></exception>
      public double Interpolate(VolatilitySurface surface, Dt expiry,
        double smileInputValue, SmileInputKind smileInputKind)
      {
        return GetCalibrator(surface).Evaluate(
          surface.AsOf, expiry, smileInputValue, smileInputKind);
      }
    }

    #endregion

    #region Nested type: CurvePoint

    /// <summary>
    /// Class CurvePoint.
    /// </summary>
    /// <seealso cref="BaseEntity.Shared.BaseEntityObject" />
    /// <seealso cref="IVolatilityTenor" />
    [Serializable]
    class CurvePoint : BaseEntityObject, IVolatilityTenor
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="CurvePoint"/> class.
      /// </summary>
      /// <param name="kind">The kind.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="index">The index.</param>
      public CurvePoint(string kind, Curve curve, int index)
      {
        Curve = curve;
        Index = index;
        Name = kind + '.' + curve.GetDt(index).ToString("yyyy-MM-dd");
      }

      /// <summary>
      /// Gets the value.
      /// </summary>
      /// <param name="shouldBeZer0">The should be zer0.</param>
      /// <returns>System.Double.</returns>
      private double GetValue(int shouldBeZer0)
      {
        return Curve.GetVal(Index);
      }

      /// <summary>
      /// Sets the value.
      /// </summary>
      /// <param name="shouldBeZer0">The should be zer0.</param>
      /// <param name="value">The value.</param>
      private void SetValue(int shouldBeZer0, double value)
      {
        Curve.SetVal(Index, value);
      }

      /// <summary>
      /// Gets the curve.
      /// </summary>
      /// <value>The curve.</value>
      private Curve Curve { get; }

      /// <summary>
      /// Gets the index.
      /// </summary>
      /// <value>The index.</value>
      private int Index { get; }

      /// <summary>
      /// Gets the maturity (expiry) date associated with the tenor.
      /// </summary>
      /// <value>The maturity.</value>
      public Dt Maturity => Curve.GetDt(Index);

      /// <summary>
      /// Gets the name associated with the tenor.
      /// </summary>
      /// <value>The name.</value>
      public string Name { get; }

      /// <summary>
      /// Gets the volatility quote values.
      /// </summary>
      /// <value>The volatility quote values.</value>
      /// <remarks>The quote values may not be the same as the implied volatilities.
      /// For example, when the underlying options are quoted with prices,
      /// the quote values are prices instead of volatilities.
      /// When the underlying options are quoted as ATM call, Risk Reversals and Butterflies,
      /// the quote values are ATM volatilities and RR/BF deviations.</remarks>
      public IList<double> QuoteValues => _list ??
        (_list = ListUtil.CreateList(1, GetValue, SetValue));

      /// <summary>
      /// The volatility value list, created once on demand
      /// </summary>
      [NonSerialized] [NoClone] private IList<double> _list;
    }

    #endregion
  }
}
