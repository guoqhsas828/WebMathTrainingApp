/*
 * FxCurve.cs
 *  -2012. All rights reserved.
 * TBD: Should spot date be used for fx curve asof date? RTD Jan'12
 * TBD: Should WithDiscountCurve test foreign discount curve also? RTD Jan'12
 */
using System;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Util.Configuration;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;
using BaseEntity.Toolkit.Base.ReferenceRates;

namespace BaseEntity.Toolkit.Curves
{
  [Serializable]
  public sealed class FxRateWithBasis : FxRate
  {
    public readonly Dt OriginalSpot;
    public readonly DiscountCurve BasisCurve;

    public FxRateWithBasis(FxRate fx, DiscountCurve basisCurve)
      : base(fx.AsOf, fx.SettleDays, fx.FromCcy, fx.ToCcy, fx.Rate, fx.FromCcyCalendar, fx.ToCcyCalendar)
    {
      Spot = OriginalSpot = fx.Spot;
      BasisCurve = basisCurve;
    }

    internal override void Update(Currency fromCcy, Currency toCcy, double rate)
    {
      if (toCcy == FromCcy && fromCcy == ToCcy)
      {
        toCcy = ToCcy;
        fromCcy = FromCcy;
        rate = 1.0 / rate;
      }
      if (BasisCurve != null)
      {
        Dt spot = Spot;
        if (spot != OriginalSpot)
          rate *= BasisCurve.Interpolate(OriginalSpot, spot);
      }
      base.Update(fromCcy, toCcy, rate);
    }
  }

  /// <exclude/>
  [Serializable]
  public class FxCalibratorConfig
  {
    /// <exclude/>
    /// <remarks><c>True</c> 10.3.0 backward compatibility, <c>False</c> for better results.
    ///   It should be removed in 11.0.</remarks>
    [ToolkitConfig("In the pure forward curve calibration, ignores curve fit settings and always use the linear interpolation scheme.")]
    public readonly bool ForwardCurveAlwaysLinear = false;
  }

  /// Selection of basis swap leg paying spread
  public enum BasisSwapSide
  {
    /// <summary>Basis swap spread paid by non-USD leg for swaps with a USD leg, or non-EUR leg for swaps with no USD leg and EUR leg</summary>
    Default,
    /// <summary>Basis swap spread paid by Ccy1 leg</summary>
    Ccy1,
    /// <summary>Basis swap spread paid by Ccy2 leg</summary>
    Ccy2
  }

  /// <summary>
  ///  Implementation of FX tenor quote handler
  /// </summary>
  [Serializable]
  internal sealed class FxTenorQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FxTenorQuoteHandler"/> class.
    /// </summary>
    /// <param name="fxRate">The fx rate.</param>
    public FxTenorQuoteHandler(FxRate fxRate)
    {
      FxRate = fxRate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxTenorQuoteHandler"/> class.
    /// </summary>
    public FxTenorQuoteHandler(Dt date,
      Currency fromCcy, Currency toCcy,double rate)
    {
      FxRate = new FxRate(date, 0, fromCcy, toCcy, rate, Calendar.None, Calendar.None);
    }

    #endregion Constructors

    #region ICurveTenorQuoteHandler Members

    public IMarketQuote GetCurrentQuote(CurveTenor tenor)
    {
      return new CurveTenor.Quote(QuotingConvention.FxRate, Rate);
    }

    public double GetQuote(
      CurveTenor tenor,
      QuotingConvention targetQuoteType,
      Curve curve, Calibrator calibrator,
      bool recalculate)
    {
      if (targetQuoteType != QuotingConvention.FxRate)
      {
        throw new QuoteConversionNotSupportedException(
          targetQuoteType, QuotingConvention.FxRate);
      }
      return Rate;
    }

    public void SetQuote(
      CurveTenor tenor,
      QuotingConvention quoteType, double quoteValue)
    {
      if (quoteType != QuotingConvention.FxRate)
        throw new QuoteConversionNotSupportedException(QuotingConvention.FxRate, quoteType);
      Rate = quoteValue;

      // Synchronize the product for hedging calculation.
      var fw = tenor.Product as FxForward;
      if (fw != null) fw.FxRate = Rate;
    }

    public double BumpQuote(CurveTenor tenor,
      double bumpSize, BumpFlags bumpFlags)
    {
      bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
      bool up = (bumpFlags & BumpFlags.BumpDown) == 0;
      double bumpAmt = (up) ? bumpSize : -bumpSize;

      var onePip = FxCurveUtil.OnePip(Rate);

      if (bumpRelative)
      {
        bumpAmt = bumpAmt*Rate;
      }
      else
      {
        bumpAmt *= onePip;
      }
      if (Rate + bumpAmt < 0.0)
      {
        FxCurve.logger.DebugFormat(
          "Unable to bump tenor '{0}' by {1}, bump {2} instead",
          tenor.Name, bumpAmt, -Rate/2);
        bumpAmt = -Rate/2;
      }
      Rate += bumpAmt;

      // Synchronize the product for hedging calculation.
      var fw = tenor.Product as FxForward;
      if (fw != null) fw.FxRate = Rate;
      return ((up) ? bumpAmt : -bumpAmt)/onePip;
    }

    public IPricer CreatePricer(CurveTenor tenor,
      Curve curve, Calibrator calibrator)
    {
      var ccurve = curve as CalibratedCurve;
      if (ccurve == null)
      {
        throw new NotSupportedException("FX rate quote"
          + " handler works only with calibrated curves");
      }
      return calibrator.GetPricer(ccurve, tenor.Product);
    }

    #endregion

    #region Properties

    public FxRate FxRate { get; private set; }

    public double Rate
    {
      get { return FxRate.Rate; }
      set { FxRate.Rate = value; }
    }

    #endregion Properties
  }

  /// <summary>
  /// Dummy class for holding an FX Calibrator object which at the moment
  /// would do Nothing other than copying curve points from tenors.
  /// </summary>
  [Serializable]
  public sealed class FxCalibrator : Calibrator
  {
    #region Members

    /// <summary>
    /// Constructor for the FX Calibrator
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="fi">The FxInterpolator.</param>
    public FxCalibrator(Dt asOf, FxInterpolator fi): base(asOf)
    {
      fi_ = fi;
    }

    /// <summary>
    /// Dummy FitFrom method
    /// </summary>
    /// <remarks>
    /// <para>Derived calibrated curves implement this to do the work of the
    /// fitting</para>
    /// <para>Called by Fit() and Refit(). Child calibrators can assume
    /// that the tenors have been validated and the data curve has
    /// been cleared for a full refit (fromIdx = 0).</para>
    /// </remarks>
    /// <param name="curve">Curve to calibrate</param>
    /// <param name="fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      if (!(curve is FxCurve))
      {
        throw new ToolkitException("Call FxCalibraton on non-FxCurve.");
      }
      FxCurve.logger.Debug("Refit composite FX curve ignored.");
    }


    /// <summary>
    ///  Dummy implementation, always returns null.
    /// </summary>
    /// <param name="curve">Curve to calibrate</param>
    /// <param name="product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      return null;
    }

    private readonly FxInterpolator fi_;

    /// <summary>
    /// Parent curves
    /// </summary>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      if (fi_ == null) return EmptyEnumerable;
      return fi_.EnumerateComponentCurves().Cast<CalibratedCurve>();
    }

    private static IEnumerable<CalibratedCurve> CreateEmptyEnumerable()
    {
      yield break;
    }
    private static readonly IEnumerable<CalibratedCurve> EmptyEnumerable =
      CreateEmptyEnumerable();
    #endregion Members
  }

  /// <summary>
  /// Dummy class for holding an FX Calibrator object which at the moment
  /// would do Nothing other than copying curve points from tenors.
  /// </summary>
  [Serializable]
  internal sealed class FxForwardCalibrator : Calibrator
  {
    #region Members

    /// <summary>
    /// Constructor for the FX Calibrator
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="baseRate">The base rate.</param>
    /// <remarks></remarks>
    public FxForwardCalibrator(Dt asOf, double baseRate)
      : base(asOf)
    {
      baseRate_ = baseRate;
    }

    /// <summary>
    /// Dummy FitFrom method
    /// </summary>
    /// <remarks>
    /// <para>Derived calibrated curves implement this to do the work of the
    /// fitting</para>
    /// <para>Called by Fit() and Refit(). Child calibrators can assume
    /// that the tenors have been validated and the data curve has
    /// been cleared for a full refit (fromIdx = 0).</para>
    /// </remarks>
    /// <param name="ffcurve">The FX fortward curve to calibrate</param>
    /// <param name="fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve ffcurve, int fromIdx)
    {
      var curve = new OverlayWrapper(ffcurve);
      var dates = new Dt[curve.Tenors.Count];
      var vals = new double[curve.Tenors.Count];
      CurveTenorCollection tenors = curve.Tenors;
      int count = tenors.Count;
      for (int i = 0; i < count; i++)
      {
        var tenor = tenors[i];
        dates[i] = tenor.Product.Maturity;
        vals[i] = tenor.QuoteHandler.GetQuote(tenor,
          QuotingConvention.FxRate, ffcurve, this, false);
      }
      if (baseRate_ != 0.0)
      {
        double scale = vals[0] / baseRate_;
        for(int i = 1; i < count; ++i)
          vals[i] *= scale;
      }
      curve.Clear();
      curve.Set(dates, vals);
    }

    /// <summary>
    /// Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name="curve">Curve to calibrate</param>
    /// <param name="product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    /// <remarks></remarks>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      return GetFxForwardPricer((FxForwardCurve)curve,
        _domesticDiscountCurve, (FxForward)product);
    }

    internal static FxForwardPricer GetFxForwardPricer(
      FxForwardCurve curve, DiscountCurve discountCurve, FxForward fwd)
    {
      return new FxForwardPricer(fwd, curve.AsOf, curve.AsOf, 1.0, fwd.PayCcy,
        discountCurve ?? _zeroRateDiscountCurve, new FxCurve(curve), null);
    }

    internal static void SetDiscountCurve(FxForwardCurve curve,
      DiscountCurve domesticDiscountCurve)
    {
      var cal = curve.Calibrator as FxForwardCalibrator;
      if (cal != null) cal._domesticDiscountCurve = domesticDiscountCurve;
    }

    /// <summary>
    /// Parent curves
    /// </summary>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      if (_domesticDiscountCurve != null)
        yield return _domesticDiscountCurve;
    }

    private DiscountCurve _domesticDiscountCurve;
    private readonly double baseRate_;
    private static readonly DiscountCurve _zeroRateDiscountCurve =
      new DiscountCurve(new Dt(1.0), 0.0);
    #endregion Members
  }

  /// <summary>
  ///  Curve build on simple forward FX rate data.
  /// </summary>
  [Serializable]
  public sealed class FxForwardCurve : CalibratedCurve
  {
    #region Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="FxForwardCurve"/> class.
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <remarks>
    /// Interpolation defaults to flat continuously compounded forward rates.
    /// Ie. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    /// Compounding frequency is Continuous.
    /// </remarks>
    public FxForwardCurve(Dt asOf) : base(asOf)
    {
      DayCount = DayCount.None;
      Name = String.Format("FxForwardCurve#{0}", Id);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxForwardCurve"/> class.
    /// </summary>
    /// <param name="curveName">Name of the curve.</param>
    /// <param name="asOf">Curve as-of date</param>
    /// <param name="settings">Curve fit settings.</param>
    /// <param name="fxRate">The fx rate.</param>
    /// <param name="interpOnFxFactor">if set to <c>true</c> [interp on fx factor].</param>
    public FxForwardCurve(string curveName, Dt asOf, CurveFitSettings settings,
      FxRate fxRate, bool interpOnFxFactor) : base(asOf)
    {
      Name = String.IsNullOrEmpty(curveName)
        ? String.Format("FxForwardCurve#{0}", Id)
        : curveName;

      if (ToolkitConfigurator.Settings.FxCalibrator.ForwardCurveAlwaysLinear)
      {
        // Ingore the user settings and use the hard coded one.
        settings = new CurveFitSettings(fxRate.Spot)
        {
          //Interpolation method defaults to linear
          InterpScheme = InterpScheme.FromString("Linear; Const", ExtrapMethod.None, ExtrapMethod.None)
        };
      }
      else if (settings == null)
      {
        settings = CreateDefaultCurveFitSettings(fxRate.Spot);
      }
      DayCount = settings.CurveDayCount.HasValue ? settings.CurveDayCount.Value : DayCount.None;
      Interp = settings.GetInterp();
      Calibrator = new FxForwardCalibrator(fxRate.Spot, interpOnFxFactor ? fxRate.Rate : 0);

      // The curve always has spot rate data and
      // we pass in FX spot rate object by reference.
      AddFxSpot("SpotFx", fxRate);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxForwardCurve"/> class.
    /// </summary>
    /// <param name="curveName">Name of the curve.</param>
    /// <param name="settings">Curve fit settings.</param>
    /// <param name="fxRate">The fx rate.</param>
    /// <param name="dates">The dates.</param>
    /// <param name="vals">The vals.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="interpOnFxFactor">if set to <c>true</c> [interp on fx factor].</param>
    /// <remarks></remarks>
    public FxForwardCurve(string curveName, CurveFitSettings settings,
      FxRate fxRate, Dt[] dates, double[] vals, string[] tenorNames, bool interpOnFxFactor)
      : base(fxRate.Spot)
    {
      Name = String.IsNullOrEmpty(curveName)
        ? String.Format("FxForwardCurve#{0}", Id)
        : curveName;

      if (ToolkitConfigurator.Settings.FxCalibrator.ForwardCurveAlwaysLinear)
      {
        // Ingore the user settings and use the hard coded one.
          settings = new CurveFitSettings(fxRate.Spot)
          {
            //Interpolation method defaults to linear
            InterpScheme = InterpScheme.FromString("Linear; Const", ExtrapMethod.None, ExtrapMethod.None)
          };
      }
      else if (settings == null)
      {
        settings = CreateDefaultCurveFitSettings(fxRate.Spot);
      }
      DayCount = settings.CurveDayCount.HasValue ? settings.CurveDayCount.Value : DayCount.None;
      Interp = settings.GetInterp();
      Calibrator = new FxForwardCalibrator(fxRate.Spot, interpOnFxFactor ? fxRate.Rate : 0);

      // The curve always has spot rate data and
      // we pass in FX spot rate object by reference.
      AddFxSpot("SpotFx", fxRate);

      // If no other data, we're done.
      if (dates == null || dates.Length == 0) return;

      // We have additional data. Check the consistency.
      if (tenorNames != null && tenorNames.Length > 0 && tenorNames.Length != dates.Length)
        throw new ToolkitException(String.Format("Number of tenor names ({0}) and dates ({1}) not match.",
                                                 tenorNames.Length, dates.Length));
      if (dates.Length != vals.Length)
        throw new ToolkitException(String.Format("Number of values ({0}) and dates ({1}) not match.", vals.Length,
                                                 dates.Length));
      int first = 0;
      if (dates[0] == fxRate.Spot)
      {
        // If the first date is the spot date....
        if (Math.Abs(vals[0] - fxRate.Rate) > 1E-8)
          throw new ToolkitException(String.Format("Two FX rates ({0} vs {1}) on the spot date {2}.", fxRate.Rate,
                                                   vals[0], fxRate.Spot));
        first = 1; // skip the spot date, for it's already set.
      }

      var lengthMatched = (tenorNames != null && tenorNames.Length == dates.Length);
      for (int i = first; i < dates.Length; i++)
      {
        AddFxForward(lengthMatched ? tenorNames[i] : null,
          fxRate.Spot, dates[i], fxRate.FromCcy, fxRate.ToCcy, vals[i]);
      }
      Calibrator.Fit(this);
    }


    /// <summary>
    /// Adds the FX spot tenor.
    /// </summary>
    /// <param name="name">Tenor name.</param>
    /// <param name="fx">FX spot rate object.</param>
    /// <remarks>
    ///   The FX spot rate object is passed and saved by reference.
    ///   The CCR simulation engine relies on this to work properly.
    /// </remarks>
    public void AddFxSpot(string name, FxRate fx)
    {
      if (String.IsNullOrEmpty(name)) name = "SpotFx";
      Dt date = fx.Spot;
      double rate = fx.Rate;
      Add(date, fx.Rate);
      var fxFwd = new FxForward(date, fx.FromCcy, fx.ToCcy, rate)
      {
        Description = name
      };
      Tenors.Add(new CurveTenor(name, fxFwd, 0.0, 0.0, 0.0, 1.0,
        new FxTenorQuoteHandler(fx)));// save FxRate object by reference.
    }

    /// <summary>
    /// Adds an FX forward tenor.
    /// </summary>
    /// <param name="name">Tenor name.</param>
    /// <param name="effective">The effective date.</param>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="fromCcy">The currency to convert from.</param>
    /// <param name="toCcy">The currency to onvert to.</param>
    /// <param name="rate">The FX rate.</param>
    public void AddFxForward(string name, Dt effective, Dt maturity,
      Currency fromCcy, Currency toCcy,double rate)
    {
      if (String.IsNullOrEmpty(name)) name = maturity.ToString();
      Add(maturity, rate);
      var fxFwd = new FxForward(maturity, fromCcy, toCcy, rate)
      {
        Description = name
      };
      Tenors.Add(new CurveTenor(name, fxFwd, 0.0, 0.0, 0.0, 1.0,
        new FxTenorQuoteHandler(maturity, fromCcy, toCcy, rate)));
    }

    #endregion methods

    #region Properties
    internal FxRate SpotFxRate
    {
      get
      {
        return ((FxTenorQuoteHandler)Tenors[0].QuoteHandler).FxRate;
      }
    }
    #endregion

    #region private methods

    /// <summary>
    /// Creates the default settings for Fx-forward(s) based FxCurve fitting.
    /// </summary>
    /// <param name="asOf">The curve as-of date.</param>
    /// <returns>The curve fit settings.</returns>
    private static CurveFitSettings CreateDefaultCurveFitSettings(Dt asOf)
    {
      return new CurveFitSettings(asOf)
      {
        InterpScheme = InterpScheme.FromString(
          "LogTensionC1; Const/Smooth",
          ExtrapMethod.None, ExtrapMethod.None),
        CurveDayCount = DayCount.None
      };
    }

    #endregion
  }

  /// <summary>
  ///  The base class of all the FX interpolators.
  /// </summary>
  [Serializable]
  public abstract class FxInterpolator : BaseEntityObject, ICurveInterpolator
  {
    #region Members

    #region FxSpotRateCalibrator
    /// <summary>
    ///  The calibrator to handle the bumping of the spot rate.
    /// </summary>
    [Serializable]
    private class FxSpotRateCalibrator : Calibrator
    {
      internal FxSpotRateCalibrator(FxRate fxRate) : base(fxRate.Spot) { }
      // FitFrom has nothing to do. The quote handler synchronize everything.
      protected override void FitFrom(CalibratedCurve sfcurve, int fromIdx)
      {
        var fx = ((FxTenorQuoteHandler)sfcurve.Tenors[0].QuoteHandler).FxRate;
        var curve = new OverlayWrapper(sfcurve);
        curve.Clear();
        curve.Add(fx.Spot, fx.Rate);
      }
      public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
      {
        return FxForwardCalibrator.GetFxForwardPricer(
          (FxForwardCurve)curve, null, (FxForward)product);
      }
    }
    #endregion
    
    // The underlying curve controlled by this interpolator.
    internal Curve UnderlyingCurve { get; set; }
    internal FxForwardCurve SpotFxCurve { get; private set; }

    /// <summary>
    /// Fx rate object. 
    /// </summary>
    public FxRate SpotFxRate
    {
      get { return SpotFxCurve.SpotFxRate; }
    }

    internal double CurrentSpotRate
    {
      get
      {
        var fx = SpotFxRate;
        if (SpotFxCurve.ShiftOverlay != null)
        {
          // If the Shift Overlay Curve is set, we must call Interpolate
          // function to find the current spot rate.
          return SpotFxCurve.Interpolate(fx.Spot);
        }
        // Otherwise, spot rate set in the tenor.
        return SpotFxRate.Rate;
      }
    }

    /// <summary>
    /// Curve as-of date. 
    /// </summary>
    public Dt AsOf
    {
      get { return UnderlyingCurve == null ? SpotFxRate.Spot : UnderlyingCurve.AsOf; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxInterpolator"/> class.
    /// </summary>
    /// <param name="fxRate">The FX rate.</param>
    protected FxInterpolator(FxRate fxRate)
    {
      var curve = SpotFxCurve = new FxForwardCurve(fxRate.Spot);
      curve.Calibrator = new FxSpotRateCalibrator(fxRate);
      //Note: fxRate is passed and saved by reference here.
      curve.AddFxSpot("SpotFx", fxRate);
    }

    internal FxInterpolator(FxForwardCurve fxfwdCurve)
    {
      var fxRate = fxfwdCurve.SpotFxRate;
      var curve = SpotFxCurve = new FxForwardCurve(fxRate.Spot);
      curve.Calibrator = new FxSpotRateCalibrator(fxRate);
      //Note: fxRate is passed and saved by reference here.
      curve.Tenors.Add(fxfwdCurve.Tenors[0]);
      curve.Add(fxRate.Spot, fxRate.Rate);
    }

    /// <summary>
    /// Interpolates the curve value at point t.
    /// </summary>
    /// <param name="t">The point.</param>
    /// <returns>Curve value</returns>
    public abstract double Interpolate(double t);

    /// <summary>
    /// Interpolates the FX rate at specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="inverse">if set to <c>true</c>, interpolate the inverse FX rate.</param>
    /// <returns>The FX rate.</returns>
    internal virtual double Interpolate(Dt date, bool inverse)
    {
      var s = Interpolate(date - AsOf);
      return inverse ? (1/s) : s;
    }

    /// <summary>
    /// Initializes this interpolator based on the specified curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    public void Initialize(NativeCurve curve) {}

    /// <summary>
    /// Evaluates the curve value at point t.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="t">The value of the variable.</param>
    /// <param name="index">The index of the predefined intervals where t locates.</param>
    /// <returns>The curve value at t.</returns>
    public double Evaluate(NativeCurve curve, double t, int index)
    {
      return Interpolate(t);
    }

    /// <summary>
    /// Enumerates the component curves.
    /// </summary>
    /// <returns>An IEnumerable of curves.</returns>
    public virtual IEnumerable<Curve> EnumerateComponentCurves()
    {
      yield break;
    }

    /// <summary>
    /// Forward basis over the forward FX implied by the interest rate parity
    /// </summary>
    /// <param name="dt">Date</param>
    /// <param name="inverse"></param>
    /// <returns>Forward basis</returns>
    public virtual double ForwardBasis(Dt dt, bool inverse)
    {
      return 1.0;
    }

    // Note: No need to override the Clone method since we do
    // NOT want to clone the underlying curves.  The sensitivity
    // codes rely on this property.

    #endregion Members
  }

  /// <summary>
  ///   Interpolator based on FX forward rate curve.
  /// </summary>
  [Serializable]
  public sealed class FxForwardInterpolator : FxInterpolator
  {
    #region Properties

    /// <summary>
    /// Gets the curve.
    /// </summary>
    /// <value>The curve.</value>
    public CalibratedCurve Curve { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="FxForwardInterpolator"/> class.
    /// </summary>
    /// <param name="fxRate">The fx rate</param>
    /// <param name="settings">Curve fit settings</param>
    /// <param name="dates">The dates</param>
    /// <param name="vals">The vals</param>
    /// <param name="tenorNames">The tenor names</param>
    /// <param name="interpOnFxFactor">if set to <c>true</c>, interpolates on fx factors</param>
    /// <remarks></remarks>
    public FxForwardInterpolator(FxRate fxRate, CurveFitSettings settings,
      Dt[] dates, double[] vals, string[] tenorNames, bool interpOnFxFactor)
      : this(new FxForwardCurve(null, settings, fxRate, dates, vals,
        tenorNames, interpOnFxFactor))
    {}

    internal FxForwardInterpolator(FxForwardCurve fxFwdCurve)
      : base(fxFwdCurve)
    {
      Curve = fxFwdCurve;
    }

    /// <summary>
    /// Interpolates the curve value at point t.
    /// </summary>
    /// <param name="t">The point.</param>
    /// <returns>Curve value</returns>
    public override double Interpolate(double t)
    {
      return Curve.Interpolate(t);
    }

    /// <summary>
    /// Interpolates the FX rate at specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="inverse">if set to <c>true</c>, interpolate the inverse FX rate.</param>
    /// <returns>The FX rate.</returns>
    internal override double Interpolate(Dt date, bool inverse)
    {
      var s = Curve.Interpolate(date);
      return inverse ? (1/s) : s;
    }

    /// <summary>
    /// Enumerates the component curves.
    /// </summary>
    /// <returns>An IEnumerable of curves.</returns>
    public override IEnumerable<Curve> EnumerateComponentCurves()
    {
      yield return Curve;
    }

    // Note: No need to override the Clone method since we do
    // NOT want to clone the underlying curves.  The sensitivity
    // codes rely on this property.

    #endregion Methods
  }

  /// <summary>
  ///  FX interpolator based on domestic/foreign discount curves
  ///  and/or basis curve.
  /// </summary>
  [Serializable]
  public sealed class FxDiscountInterpolator : FxInterpolator
  {
    #region Properties

    /// <summary>
    /// Basis curve 
    /// </summary>
    public DiscountCurve BasisCurve
    {
      get
      {
        var fx = SpotFxRate as FxRateWithBasis;
        return fx == null ? null : fx.BasisCurve;
      }
    }

    /// <summary>
    /// Domestic discount curve 
    /// </summary>
    public DiscountCurve DomesticDiscount { get; private set;}

    /// <summary>
    /// Foreign discount curve 
    /// </summary>
    public DiscountCurve ForeignDiscount { get; private set; }

    /// <summary>
    /// Boolean flag that determines whether the curves has embedded
    ///  a discount curve.
    /// </summary>
    internal bool WithDiscountCurve
    {
      get { return DomesticDiscount != null; }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurve"/> class.
    /// </summary>
    /// <param name="fxRate">The fx rate</param>
    /// <param name="basisCurve">The basis curve</param>
    /// <param name="domesticCurve">The domestic discount curve</param>
    /// <param name="foreignCurve">The foreign discount curve</param>
    internal FxDiscountInterpolator(FxRate fxRate, DiscountCurve basisCurve,
      DiscountCurve domesticCurve, DiscountCurve foreignCurve)
      : base(basisCurve == null || fxRate is FxRateWithBasis
        ? fxRate
        : new FxRateWithBasis(fxRate, basisCurve))
    {
      // Sanity checks
      // Notes: since the domestic and foreign discount curves are read-only,
      //   it is better to check the validity in the constructor, instead of
      //   in the Validate() function.
      if ((domesticCurve == null || foreignCurve == null) && domesticCurve != foreignCurve)
        throw new ToolkitException("The domestic and foreign discount curves must both present.");
      if (domesticCurve == null && basisCurve == null)
        throw new ToolkitException("The discount curve and basis curve cannot be both null.");
      DomesticDiscount = domesticCurve;
      ForeignDiscount = foreignCurve;
    }

    /// <summary>
    /// Forward basis
    /// </summary>
    /// <param name="date"></param>
    /// <param name="inverse"></param>
    /// <returns></returns>
    public override double ForwardBasis(Dt date, bool inverse)
    {
      Dt spot = SpotFxRate.Spot;
      if (inverse && BasisCurve != null)
      {
        var cal = BasisCurve.Calibrator as IInverseCurveProvider;
        if (cal != null && cal.InverseCurve != null)
          return Df(cal.InverseCurve, spot, date);
      }
      var s = Df(BasisCurve, spot, date);
      return inverse ? (1 / s) : s;
    }

    /// <summary>
    /// Interpolates the curve value at point t.
    /// </summary>
    /// <param name="t">The point.</param>
    /// <returns>Curve value</returns>
    public override double Interpolate(double t)
    {
      return Interpolate(new Dt(AsOf, t/365), false);
    }

    /// <summary>
    /// Interpolates the FX rate at specified date
    /// </summary>
    /// <param name="date">The date</param>
    /// <param name="inverse">if set to <c>true</c>, interpolate the inverse FX rate</param>
    /// <returns>The FX rate</returns>
    internal override double Interpolate(Dt date, bool inverse)
    {
      Dt spot = SpotFxRate.Spot;
      if (inverse && BasisCurve != null)
      {
        var cal = BasisCurve.Calibrator as IInverseCurveProvider;
        if (cal != null && cal.InverseCurve != null)
        {
          var s = Df(cal.InverseCurve, spot, date) / CurrentSpotRate
            * Df(DomesticDiscount, spot, date) / Df(ForeignDiscount, spot, date);
          return s;
        }
      }
      {
        var s = CurrentSpotRate * Df(BasisCurve, spot, date)
          * Df(ForeignDiscount, spot, date) / Df(DomesticDiscount, spot, date);
        return inverse ? (1 / s) : s;
      }
    }

    /// <summary>
    /// Enumerates the component curves.
    /// </summary>
    /// <returns>An IEnumerable of curves.</returns>
    public override IEnumerable<Curve> EnumerateComponentCurves()
    {
      yield return SpotFxCurve;
      if (DomesticDiscount != null)
        yield return DomesticDiscount;
      if (ForeignDiscount!=null)
        yield return ForeignDiscount;
      if (BasisCurve != null)
        yield return BasisCurve;
    }

    private static double Df(Curve curve, Dt spot, Dt date)
    {
      return curve == null ? 1.0 : curve.Interpolate(spot, date);
    }

    #endregion Methods
  }

  /// <summary>
  ///   FX interpolator based on triangulation.
  /// </summary>
  [Serializable]
  public sealed class FxTriangulationInterpolator : FxInterpolator
  {
    #region Properties

    /// <summary>
    /// The first fx curve.
    /// </summary>
    public FxCurve Curve1 { get; private set; }

    /// <summary>
    /// The second FX curve.
    /// </summary>
    public FxCurve Curve2 { get; private set; }

    /// <summary>
    /// Intermediate currency
    /// </summary>
    private Currency InterCcy { get; set; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurve"/> class.
    /// </summary>
    public FxTriangulationInterpolator(FxRate fxRate, FxCurve curve1, FxCurve curve2)
      : base(fxRate)
    {
      if (curve1 == null || curve2 == null)
        throw new ToolkitException("Both FX curves must present for triangulation");
      var first = curve1.SpotFxRate;
      var second = curve2.SpotFxRate;
      if (fxRate.FromCcy == first.FromCcy || fxRate.FromCcy == first.ToCcy)
      {
        InterCcy = (fxRate.FromCcy == first.FromCcy) ? first.ToCcy : first.FromCcy;
        if ((second.FromCcy == InterCcy && second.ToCcy == fxRate.ToCcy)
          || (second.ToCcy == InterCcy && second.FromCcy == fxRate.ToCcy))
        {
          Curve1 = curve1;
          Curve2 = curve2;
          return;
        }
      }
      throw new ToolkitException(String.Format(
        "FX {0}/{1} and {2}/{3} not good for triangulation {4}/{5}",
        first.FromCcy, first.ToCcy, second.FromCcy, second.ToCcy,
        fxRate.FromCcy, fxRate.ToCcy));
    }

    /// <summary>
    ///  Calculates the FX rate by triangulation.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="fromCcy">The currency of the from</param>
    /// <param name="toCcy">The currency of the to</param>
    /// <param name="curve1">The first FX curve.</param>
    /// <param name="curve2">The second FX curve.</param>
    /// <returns>The FX rate</returns>
    public static double FxRate(
      Dt date, Currency fromCcy, Currency toCcy,
      FxCurve curve1, FxCurve curve2)
    {
      if (curve1 == null || curve2 == null)
      {
        throw new ToolkitException(
          "Both FX curves must present for triangulation");
      }
      var first = curve1.SpotFxRate;
      var second = curve2.SpotFxRate;
      if (fromCcy == first.FromCcy || fromCcy == first.ToCcy)
      {
        var interCcy = fromCcy == first.FromCcy ? first.ToCcy : first.FromCcy;
        if ((second.FromCcy == interCcy && second.ToCcy == toCcy)
          || (second.ToCcy == interCcy && second.FromCcy == toCcy))
        {
          return curve1.FxRate(date, fromCcy, interCcy)
            *curve2.FxRate(date, interCcy, toCcy);
        }
      }
      throw new ToolkitException(String.Format(
        "FX {0}/{1} and {2}/{3} not good for triangulation {4}/{5}",
        first.FromCcy, first.ToCcy, second.FromCcy, second.ToCcy,
        fromCcy, toCcy));
    }

    /// <summary>
    /// Interpolates the curve value at point t.
    /// </summary>
    /// <param name="t">The point.</param>
    /// <returns>Curve value</returns>
    public override double Interpolate(double t)
    {
      Dt date = new Dt(AsOf, t / 365);
      return Curve1.FxRate(date, SpotFxRate.FromCcy, InterCcy)
        * Curve2.FxRate(date, InterCcy, SpotFxRate.ToCcy);
    }

    /// <summary>
    /// Interpolates the FX rate at specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="inverse">if set to <c>true</c>, interpolate the inverse FX rate.</param>
    /// <returns>The FX rate.</returns>
    internal override double Interpolate(Dt date, bool inverse)
    {
      return inverse
        ? (Curve2.FxRate(date, SpotFxRate.ToCcy, InterCcy)
          * Curve1.FxRate(date, InterCcy, SpotFxRate.FromCcy))
        : (Curve1.FxRate(date, SpotFxRate.FromCcy, InterCcy)
          * Curve2.FxRate(date, InterCcy, SpotFxRate.ToCcy));
    }

    /// <summary>
    /// Enumerates the component curves.
    /// </summary>
    /// <returns>An IEnumerable of curves.</returns>
    public override IEnumerable<Curve> EnumerateComponentCurves()
    {
      yield return Curve1;
      yield return Curve2;
    }

    #endregion Methods
  }

  /// <summary>
  ///   Forward Fx curve
  /// </summary>
  /// <remarks>
  ///   <para>A term structure of fx rates.</para>
  ///   <para>The FX forward curve can be generated from a combination of fx forward quotes and fx forwards implied by
  ///   interest rates and cross currency basis swaps.</para>
  ///   <para>Typically the short end of the fx forward curve is specified using fx forward quotes and the long end of the
  ///   fx forward curve is implied by interest rates and cross currency basis swaps.</para>
  ///   <para>Dual rate curves are supported by passing in the projection curve rather than the discount curve.</para>
  ///   <para><b>FX forward quotes</b></para>
  ///   <para>FX forward quotes may be directly specified as a date, tenor, and fx forward.</para>
  ///   <para>If dates are not specified, they are calculated from the spot date based on the tenors
  ///   and US Bank holidays.</para>
  ///   <para><b>FX forwards implied from rates</b></para>
  ///   <para>With no market friction, forward exchange rates are implied
  ///   by the relative deposit rates in each currency.</para>
  ///   <para>For example, investing in a domestic deposit is equivalent to
  ///   exchanging money at the spot rate, investing in a foreign deposit and
  ///   exchanging back the foreign deposit proceeds at the forward exchange rate.</para>
  ///   <formula>
  ///     \left( 1 + r_d * \frac{t}{360} \right) = \frac{fx_fwd}{fx_spot} * \left( 1 + r_f * \frac{t}{360} \right)
  ///   </formula>
  ///   <para>or</para>
  ///   <formula>
  ///     fx_fwd = fx_spot * \frac{\left( 1 + r_d * \frac{t}{360} \right)}{\left( 1 + r_f * \frac{t}{360} \right)}
  ///   </formula>
  ///   <para>where</para>
  ///   <list type="bullet">
  ///     <item><description><m>r_d</m> is the domestic deposit rate</description></item>
  ///     <item><description><m>r_f</m> is the foreign deposit rate</description></item>
  ///     <item><description><m>t</m> is the number of days</description></item>
  ///	    <item><description><m>fx_spot</m> is the spot exchange rate (domestic/foreign)</description></item>
  ///  		<item><description><m>fx_fwd</m> is the forward exchange rate (domestic/foreign)</description></item>
  ///   </list>
  ///   <para>Taking into account intermediate cashflows, credit and market friction requires are
  ///   more complex parity relationship that includes cross currency basis swaps.</para>
  /// </remarks>
  [Serializable]
  public sealed class FxCurve : CalibratedCurve
  {
    internal static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof (FxCurve));

    #region Static Constructor
    /// <summary>
    ///   Create an FX forward curve
    /// </summary>
    /// <remarks>
    ///   <para>The FX forward curve can be generated from a combination of fx forward quotes and fx forwards implied by
    ///   interest rates and cross currency basis swaps.</para>
    ///   <para>Typically the short end of the fx forward curve is specified using fx forward quotes and the long end of the
    ///   fx forward curve is implied by interest rates and cross currency basis swaps.</para>
    ///   <para>Dual rate curves are supported by passing in the projection curve rather than the discount curve.</para>
    ///   <para><b>FX forward quotes</b></para>
    ///   <para>FX forward quotes may be directly specified as a date, tenor, and fx forward.</para>
    ///   <para>If dates are not specified, they are calculated from the spot date based on the tenors
    ///   and US Bank holidays.</para>
    ///   <para><b>FX forwards implied from rates</b></para>
    ///   <para>With no market friction, forward exchange rates are implied
    ///   by the relative deposit rates in each currency.</para>
    ///   <para>For example, investing in a domestic deposit is equivalent to
    ///   exchanging money at the spot rate, investing in a foreign deposit and
    ///   exchanging back the foreign deposit proceeds at the forward exchange rate.</para>
    ///   <formula>
    ///     \left( 1 + r_d * \frac{t}{360} \right) = \frac{fx_fwd}{fx_spot} * \left( 1 + r_f * \frac{t}{360} \right)
    ///   </formula>
    ///   <para>or</para>
    ///   <formula>
    ///     fx_fwd = fx_spot * \frac{\left( 1 + r_d * \frac{t}{360} \right)}{\left( 1 + r_f * \frac{t}{360} \right)}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m>r_d</m> is the domestic deposit rate</description></item>
    ///     <item><description><m>r_f</m> is the foreign deposit rate</description></item>
    ///     <item><description><m>t</m> is the number of days</description></item>
    ///	    <item><description><m>fx_spot</m> is the spot exchange rate (domestic/foreign)</description></item>
    ///  		<item><description><m>fx_fwd</m> is the forward exchange rate (domestic/foreign)</description></item>
    ///   </list>
    ///   <para>Taking into account intermediate cashflows, credit and market friction requires are
    ///   more complex parity relationship that includes cross currency basis swaps.</para>
    /// </remarks>
    /// <param name="asOf">AsOf (trade/pricing/horizon) date</param>
    /// <param name="spot">Spot fx date</param>
    /// <param name="spotFxRate">Spot fx rate for one unit of ccy1 in terms of ccy2 (ccy1/ccy2)</param>
    /// <param name="curveTenors">curve tenor quotes</param>
    /// <param name="fxRate">Fx spot rate</param>
    /// <param name="ccy1DiscountCurve">The ccy1 (base/foreign/source/from) discount or projection curve</param>
    /// <param name="ccy2DiscountCurve">The ccy2 (quoting/domestic/destination/to) discount or projection curve</param>
    /// <param name="fitSettings">Curve fitting tuning parameters or null for defaults</param>
    /// <param name="curveName">Name of the curve.</param>
    /// <returns>Fx forward curve created</returns>
    public static FxCurve Create(
      Dt asOf, Dt spot, FxReferenceRate fxRate, double spotFxRate,
      IEnumerable<CurveTenor> curveTenors,
      DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve,
      CurveFitSettings fitSettings, string curveName)
    {
      var ccy1 = fxRate.ForeignCurrency;
      var ccy2 = fxRate.Currency;
      var fxRefIndex = new BaseEntity.Toolkit.Base.ReferenceIndices.FxRateIndex(fxRate);
      var fx = new FxRate(asOf, spot, fxRate.ForeignCurrency, fxRate.Currency, spotFxRate);

      var tenors = new CurveTenorCollection();
      foreach (var tenor in curveTenors)
      {
        if (tenor == null) continue;
        tenor.UpdateProduct(asOf);
        if (!MatchIndex(tenor, fxRefIndex, ccy1DiscountCurve.ReferenceIndex, ccy2DiscountCurve.ReferenceIndex)) continue;
        tenors.Add(CloneUtil.Clone(tenor));
      }

      // Is this a pure forward curve?
      if (ccy1DiscountCurve == null || ccy2DiscountCurve == null)
      {
        // Is it pure FX forward curve?
        if (tenors.Any() && !tenors.Any(o => o.Product is Swap))
        {
          var fwdCurve = new FxForwardCurve(curveName, asOf, fitSettings, fx, true);
          foreach (var t in tenors)
          {
            fwdCurve.Tenors.Add(t);
          }
          FxForwardCalibrator.SetDiscountCurve(fwdCurve, ccy2DiscountCurve);
          var xccy = String.Concat(ccy1, ccy2, '.');
          foreach (CurveTenor tenor in fwdCurve.Tenors)
            tenor.QuoteKey = xccy + tenor.Name;
          return new FxCurve(fwdCurve);
        }
        throw new ArgumentException("Must specify discount curves for fx curve fit based on rates");
      }

      FxCurve fxCurve;
      if (!tenors.Any())
      {
        fxCurve = new FxCurve(fx, null, ccy2DiscountCurve, ccy1DiscountCurve, curveName);
      }
      else
      {
        // Default fit settings if we don't have any
        if (fitSettings == null)
          fitSettings = new CurveFitSettings(asOf);

        // Give unique name
        string name = curveName == null ? String.Format("{0}{1}_BasisCurve", ccy1, ccy2) : curveName;
        var basis = FxBasisFitCalibrator.FxCurveFit(name, fitSettings, fx, ccy1DiscountCurve, ccy2DiscountCurve, tenors);
        var cal = (FxBasisFitCalibrator)basis.Calibrator;
        fxCurve = new FxCurve(fx, basis, cal.DomesticDiscount, cal.ForeignDiscount, curveName);
      }
      fxCurve.FxInterpolator.SpotFxCurve.Tenors[0].QuoteKey = string.Concat(ccy1, ccy2, ".SpotFx");
      return fxCurve;      
    }

    private static bool MatchIndex(CurveTenor tenor,
      ReferenceIndex targetIndex, ReferenceIndex ccy1Index, ReferenceIndex ccy2Index)
    {
      // Cross Currency basis swaps
      if (tenor.Product is Swap)
      {
        var swap = tenor.Product as Swap;

        return swap.ReferenceIndices.Any(o => ccy1Index.IndexName.Contains(o.IndexName) // contains rather than equals for backwards compatibility
        && o.IndexTenor == ccy1Index.IndexTenor)
        && swap.ReferenceIndices.Any(o => ccy2Index.IndexName.Contains(o.IndexName) // contains rather than equals for backwards compatibility
        && o.IndexTenor == ccy2Index.IndexTenor);
      }

      // Fx Forwards
      if (tenor.Product is FxForward)
      {
        var index = tenor.ReferenceIndex;
        if (index == null)
          return true; // Empty means match any
        return targetIndex.IsEqual(index);
      }

      // Fx Futures
      var target = targetIndex as FxRateIndex;
      if (tenor.Product is FxFuture)
      {
        var index = tenor.ReferenceIndex as FxRateIndex;
        return (index.Currency == target.Currency && index.ForeignCurrency == target.ForeignCurrency)
          || (index.Currency == target.ForeignCurrency && index.ForeignCurrency == target.Currency);
      }

      return false;
    }

    /// <summary>
    ///   Create an FX forward curve
    /// </summary>
    /// <remarks>
    ///   <para>The FX forward curve can be generated from a combination of fx forward quotes and fx forwards implied by
    ///   interest rates and cross currency basis swaps.</para>
    ///   <para>Typically the short end of the fx forward curve is specified using fx forward quotes and the long end of the
    ///   fx forward curve is implied by interest rates and cross currency basis swaps.</para>
    ///   <para>Dual rate curves are supported by passing in the projection curve rather than the discount curve.</para>
    ///   <para><b>FX forward quotes</b></para>
    ///   <para>FX forward quotes may be directly specified as a date, tenor, and fx forward.</para>
    ///   <para>If dates are not specified, they are calculated from the spot date based on the tenors
    ///   and US Bank holidays.</para>
    ///   <para><b>FX forwards implied from rates</b></para>
    ///   <para>With no market friction, forward exchange rates are implied
    ///   by the relative deposit rates in each currency.</para>
    ///   <para>For example, investing in a domestic deposit is equivalent to
    ///   exchanging money at the spot rate, investing in a foreign deposit and
    ///   exchanging back the foreign deposit proceeds at the forward exchange rate.</para>
    ///   <formula>
    ///     \left( 1 + r_d * \frac{t}{360} \right) = \frac{fx_fwd}{fx_spot} * \left( 1 + r_f * \frac{t}{360} \right)
    ///   </formula>
    ///   <para>or</para>
    ///   <formula>
    ///     fx_fwd = fx_spot * \frac{\left( 1 + r_d * \frac{t}{360} \right)}{\left( 1 + r_f * \frac{t}{360} \right)}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m>r_d</m> is the domestic deposit rate</description></item>
    ///     <item><description><m>r_f</m> is the foreign deposit rate</description></item>
    ///     <item><description><m>t</m> is the number of days</description></item>
    ///	    <item><description><m>fx_spot</m> is the spot exchange rate (domestic/foreign)</description></item>
    ///  		<item><description><m>fx_fwd</m> is the forward exchange rate (domestic/foreign)</description></item>
    ///   </list>
    ///   <para>Taking into account intermediate cashflows, credit and market friction requires are
    ///   more complex parity relationship that includes cross currency basis swaps.</para>
    /// </remarks>
    /// <param name="asOf">AsOf (trade/pricing/horizon) date</param>
    /// <param name="spot">Spot fx date</param>
    /// <param name="ccy1">Ccy1 (base/foreign/source/from) currency</param>
    /// <param name="ccy2">Ccy2 (quoting/domestic/destination/to) currency</param>
    /// <param name="spotFxRate">Spot fx rate for one unit of ccy1 in terms of ccy2 (ccy1/ccy2)</param>
    /// <param name="fwdTenors">Tenor of fx fwd quotes</param>
    /// <param name="fwdDates">Dates of fx fwd quotes</param>
    /// <param name="fwdQuotes">Fx fwd quotes</param>
    /// <param name="swapTenors">Tenor of basis swap quotes</param>
    /// <param name="swapDates">Dates of basis swap quotes</param>
    /// <param name="swapQuotes">Basis swap quotes</param>
    /// <param name="swapCal">Calendar for basis swap settlement (default is calendar for each currency and LNB)</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <param name="ccy1DiscountCurve">The ccy1 (base/foreign/source/from) discount or projection curve</param>
    /// <param name="ccy2DiscountCurve">The ccy2 (quoting/domestic/destination/to) discount or projection curve</param>
    /// <param name="fitSettings">Curve fitting tuning parameters or null for defaults</param>
    /// <param name="curveName">Name of the curve.</param>
    /// <returns>Fx forward curve created</returns>
    public static FxCurve Create(
      Dt asOf, Dt spot, Currency ccy1, Currency ccy2, double spotFxRate,
      string[] fwdTenors, Dt[] fwdDates, double[] fwdQuotes,
      string[] swapTenors, Dt[] swapDates, double[] swapQuotes,
      Calendar swapCal, BasisSwapSide swapSpreadLeg,
      DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve,
      CurveFitSettings fitSettings, string curveName)
    {
      return Create(new FxRate(asOf, spot, ccy1, ccy2, spotFxRate),
        fwdTenors, fwdDates, fwdQuotes,
        swapTenors, swapDates, swapQuotes, swapCal, swapSpreadLeg,
        ccy1DiscountCurve, ccy2DiscountCurve, fitSettings, curveName);
    }

    /// <summary>
    ///   Create an FX forward curve
    /// </summary>
    /// <param name="fx">Fx rate object</param>
    /// <param name="fwdTenors">Tenor of fx fwd quotes</param>
    /// <param name="fwdDates">Dates of fx fwd quotes</param>
    /// <param name="fwdQuotes">Fx fwd quotes</param>
    /// <param name="swapTenors">Tenor of basis swap quotes</param>
    /// <param name="swapDates">Dates of basis swap quotes</param>
    /// <param name="swapQuotes">Basis swap quotes</param>
    /// <param name="swapCal">Calendar for basis swap settlement 
    /// (default is calendar for each currency and LNB)</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <param name="ccy1DiscountCurve">The ccy1 (base/foreign/source/from) 
    /// discount or projection curve</param>
    /// <param name="ccy2DiscountCurve">The ccy2 (quoting/domestic/destination/to) 
    /// discount or projection curve</param>
    /// <param name="fitSettings">Curve fitting tuning parameters or null for defaults</param>
    /// <param name="curveName">Name of the curve.</param>
    /// <returns>Fx forward curve created</returns>
    public static FxCurve Create(FxRate fx,
      string[] fwdTenors, Dt[] fwdDates, double[] fwdQuotes,
      string[] swapTenors, Dt[] swapDates, double[] swapQuotes,
      Calendar swapCal, BasisSwapSide swapSpreadLeg,
      DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve,
      CurveFitSettings fitSettings, string curveName)
    {
      // Normalize
      if (fwdTenors == null) fwdTenors = EmptyArray<string>.Instance;
      if (fwdDates == null) fwdDates = EmptyArray<Dt>.Instance;
      if (fwdQuotes == null) fwdQuotes = EmptyArray<double>.Instance;
      if (swapTenors == null) swapTenors = EmptyArray<string>.Instance;
      if (swapDates == null) swapDates = EmptyArray<Dt>.Instance;
      if (swapQuotes == null) swapQuotes = EmptyArray<double>.Instance;

      // Verify we have right number of tenors and dates
      if (fwdDates.Length != fwdQuotes.Length)
        throw new ArgumentException("Number of fx fwd dates must match number of quotes");
      if (fwdTenors.Length != fwdQuotes.Length)
        throw new ArgumentException("Number of fx fwd tenors must match number of quotes");
      if (swapDates.Length != swapQuotes.Length)
        throw new ArgumentException("Number of basis swap dates must match number of quotes");
      if (swapTenors.Length != swapQuotes.Length)
        throw new ArgumentException("Number of basis swap tenors must match number of quotes");

      Currency ccy1 = fx.FromCcy, ccy2 = fx.ToCcy;

      // Is this a pure forward curve?
      if (ccy1DiscountCurve == null || ccy2DiscountCurve == null)
      {
        // Is it pure FX forward curve?
        if (swapQuotes.Length == 0)
        {
          var fwdCurve = new FxForwardCurve(curveName, fitSettings, fx, fwdDates,
            fwdQuotes, fwdTenors.Select(s => "FxForward." + s).ToArray(), true);
          FxForwardCalibrator.SetDiscountCurve(fwdCurve, ccy2DiscountCurve);
          var xccy = String.Concat(ccy1, ccy2, '.');
          foreach (CurveTenor tenor in fwdCurve.Tenors)
            tenor.QuoteKey = xccy + tenor.Name;

          return new FxCurve(fwdCurve)
          {
            SpotDays = fx.SettleDays
          };
        }
        throw new ArgumentException("Must specify discount curves for fx curve fit based on rates");
      }

      FxCurve fxCurve;
      if (fwdQuotes.Length + swapQuotes.Length == 0)
      {
        fxCurve = new FxCurve(fx, null, ccy2DiscountCurve, ccy1DiscountCurve, curveName);
      }
      else
      {
        // Default fit settings if we don't have any
        if (fitSettings == null)
          fitSettings = new CurveFitSettings(fx.AsOf);

        // Construct basis curve if we have one
        int fwdCount = fwdQuotes.Length;
        var types = new InstrumentType[fwdCount + swapQuotes.Length];
        var dates = new Dt[fwdCount + swapQuotes.Length];
        var quotes = new double[fwdCount + swapQuotes.Length];
        var tenors = new string[fwdCount + swapQuotes.Length];
        for (int i = 0; i < fwdCount; i++)
        {
          types[i] = InstrumentType.FxForward;
          dates[i] = fwdDates[i];
          quotes[i] = fwdQuotes[i];
          tenors[i] = fwdTenors[i];
        }
        for (int i = fwdCount; i < types.Length; i++)
        {
          types[i] = InstrumentType.BasisSwap;
          dates[i] = swapDates[i - fwdCount];
          quotes[i] = swapQuotes[i - fwdCount];
          tenors[i] = swapTenors[i - fwdCount];
        }

        // Give unique name
        string name = curveName == null
          ? String.Format("{0}{1}_BasisCurve", ccy1, ccy2) : curveName;
        var basis = FxBasisFitCalibrator.FxCurveFit(name, fitSettings,
          fx, ccy1DiscountCurve, ccy2DiscountCurve, fx.Spot, swapCal,
          types, dates, tenors, quotes, null, swapSpreadLeg);
        var cal = (FxBasisFitCalibrator) basis.Calibrator;
        fxCurve = new FxCurve(fx, basis, cal.DomesticDiscount, cal.ForeignDiscount,
          curveName);
      }
      fxCurve.FxInterpolator.SpotFxCurve.Tenors[0].QuoteKey = String.Concat(ccy1, ccy2, ".SpotFx");
      fxCurve.SpotDays = fx.SettleDays;
      return fxCurve;
    }

    #endregion Static Constructor

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="ccy1">The ccy1.</param>
    /// <param name="ccy2">The ccy2.</param>
    /// <param name="spotFxRate">The spot fx rate.</param>
    /// <param name="types">The types.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="dates">The dates.</param>
    /// <param name="quotes">The quotes.</param>
    /// <param name="swapCal">The swap cal.</param>
    /// <param name="swapSpreadLeg">The swap spread leg.</param>
    /// <param name="ccy1DiscountCurve">The ccy1 discount curve.</param>
    /// <param name="ccy2DiscountCurve">The ccy2 discount curve.</param>
    /// <param name="fitSettings">The fit settings.</param>
    /// <param name="curveName">Name of the curve.</param>
    /// <remarks></remarks>
    public FxCurve(Dt asOf, Dt spot, Currency ccy1, Currency ccy2, double spotFxRate,
                   InstrumentType[] types, string[] tenors, Dt[] dates, double[] quotes,
                   Calendar swapCal, BasisSwapSide swapSpreadLeg,
                   DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve,
                   CurveFitSettings fitSettings, string curveName)
      : base(spot)
    {
      // Create fx rate
      var fx = new FxRate(asOf, spot, ccy1, ccy2, spotFxRate);

      // Default fit settings if we don't have any
      if (fitSettings == null)
        fitSettings = new CurveFitSettings(asOf);
      if (ccy1DiscountCurve == null || ccy2DiscountCurve == null)
        throw new ArgumentException("Must specify discount curves for fx curve fit based on rates");
      // Construct basis curve if we have one
      string name = String.Format("{0}{1}_BasisCurve", ccy1, ccy2);
      var basis = FxBasisFitCalibrator.FxCurveFit(name, fitSettings, fx,
                                                            ccy1DiscountCurve, ccy2DiscountCurve, spot, swapCal,
                                                            types, dates, tenors, quotes, null, swapSpreadLeg);
      var fxInterp = _fxInterpolator =
        new FxDiscountInterpolator(fx, basis, ccy2DiscountCurve, ccy1DiscountCurve);
      Initialize(AsOf, fxInterp);
      Add(fx.Spot, fx.Rate);
      Tenors = fxInterp.SpotFxCurve.Tenors;
      Calibrator = new FxCalibrator(fx.Spot, fxInterp);
      Ccy = fx.ToCcy;
      if (String.IsNullOrEmpty(curveName)) return;
      fxInterp.SpotFxCurve.Name = curveName;
      fxInterp.UnderlyingCurve = this;
      Name = curveName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurve"/> class
    /// implied by interest rate parity.
    /// </summary>
    /// <remarks>
    /// <para>With no market friction, forward exchange rates are implied
    /// by the relative deposit rates in each currency.</para>
    /// <para>For example, investing in a domestic deposit is equivalent to
    /// exchanging money at the spot rate, investing in a foreign deposit and
    /// exchanging back the foreign deposit proceeds at the forward exchange rate.</para>
    /// <formula>
    ///   \left( 1 + r_d * \frac{t}{360} \right) = \frac{fx_fwd}{fx_spot} * \left( 1 + r_f * \frac{t}{360} \right)
    /// </formula>
    /// <para>or</para>
    /// <formula>
    ///   fx_fwd = fx_spot * \frac{\left( 1 + r_d * \frac{t}{360} \right)}{\left( 1 + r_f * \frac{t}{360} \right)}
    /// </formula>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><m>r_d</m> is the domestic deposit rate</description></item>
    ///   <item><description><m>r_f</m> is the foreign deposit rate</description></item>
    ///   <item><description><m>t</m> is the number of days</description></item>
    ///	  <item><description><m>fx_spot</m> is the spot exchange rate (domestic/foreign)</description></item>
    ///		<item><description><m>fx_fwd</m> is the forward exchange rate (domestic/foreign)</description></item>
    /// </list>
    /// <para>Taking into account intermediate cashflows, credit and market friction requires are
    /// more complex parity relationship that includes cross currency basis swaps.</para>
    /// </remarks>
    /// <param name="fxRate">The fx rate.</param>
    /// <param name="basisCurve">The basis curve.</param>
    /// <param name="domesticCurve">The domestic discount curve.</param>
    /// <param name="foreignCurve">The foreign discount curve.</param>
    /// <param name="curveName">Name of the curve.</param>
    public FxCurve(FxRate fxRate, DiscountCurve basisCurve,
                   DiscountCurve domesticCurve, DiscountCurve foreignCurve,
                   string curveName)
      : base(fxRate.Spot)
    {
      var fxInterp = _fxInterpolator =
        new FxDiscountInterpolator(fxRate, basisCurve, domesticCurve, foreignCurve);
      Initialize(AsOf, fxInterp);
      Add(fxRate.Spot, fxRate.Rate);
      Tenors = fxInterp.SpotFxCurve.Tenors;
      Calibrator = new FxCalibrator(fxRate.Spot, fxInterp);
      Ccy = fxRate.ToCcy;
      if (String.IsNullOrEmpty(curveName)) return;
      fxInterp.SpotFxCurve.Name = curveName;
      fxInterp.UnderlyingCurve = this;
      Name = curveName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurve"/> class implied by triangulation
    /// </summary>
    /// <remarks>
    /// <para>Triangulation uses the fx forwards implied by two fx forward curves with a common
    /// currency. For example GBP/JPY can by implied from the GBP/USD and USD/JPY forwards.</para>
    /// </remarks>
    /// <param name="asOf">AsOf (trade/pricing/horizon) date</param>
    /// <param name="spot">Spot date</param>
    /// <param name="ccy1">Ccy1 (base/foreign/source/from) currency</param>
    /// <param name="ccy2">Ccy2 (quoting/domestic/destination/to) currency</param>
    /// <param name="fxCurve1">The FX curve ccy1/ccy3</param>
    /// <param name="fxCurve2">The FX curve ccy2/ccy3</param>
    /// <returns>Fx forward curve from fx triangulation</returns>
    public FxCurve(Dt asOf, Dt spot, Currency ccy1, Currency ccy2, FxCurve fxCurve1, FxCurve fxCurve2)
      : base(spot)
    {
      double rate = FxTriangulationInterpolator.FxRate(spot, ccy1, ccy2, fxCurve1, fxCurve2);
      var fxRate = new FxRate(asOf, spot, ccy1, ccy2, rate);
      var fxInterp = _fxInterpolator =
        new FxTriangulationInterpolator(fxRate, fxCurve1, fxCurve2);
      Initialize(AsOf, fxInterp);
      Add(fxRate.Spot, fxRate.Rate);
      Tenors = fxInterp.SpotFxCurve.Tenors;
      Calibrator = new FxCalibrator(fxRate.Spot, fxInterp);
      Ccy = fxRate.ToCcy;
      fxInterp.UnderlyingCurve = this;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurve"/> class.
    /// </summary>
    /// <remarks>
    /// <para>Should be replaced by alternate FxCurve usage. Separately setting the fx rate
    /// allows for inconsistent results. RTD Dec'11</para>
    /// </remarks>
    /// <param name="fxRate">The fx rate.</param>
    /// <param name="fxCurve1">The first FX curve.</param>
    /// <param name="fxCurve2">The second FX curve.</param>
    public FxCurve(FxRate fxRate, FxCurve fxCurve1, FxCurve fxCurve2)
      : base(fxRate.Spot)
    {
      var fxInterp = _fxInterpolator = 
        new FxTriangulationInterpolator(fxRate, fxCurve1, fxCurve2);
      Initialize(AsOf, fxInterp);
      Add(fxRate.Spot, fxRate.Rate);
      Tenors = fxInterp.SpotFxCurve.Tenors;
      Calibrator = new FxCalibrator(fxRate.Spot, fxInterp);
      Ccy = fxRate.ToCcy;
      fxInterp.UnderlyingCurve = this;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurve"/> class.
    /// </summary>
    /// <param name="fxRate">The fx rate.</param>
    /// <param name="basisCurve">The basis curve.</param>
    public FxCurve(FxRate fxRate, DiscountCurve basisCurve)
      : this(fxRate, basisCurve, null, null,
             basisCurve != null ? basisCurve.Name : null)
    {
    }

    /// <summary>
    /// Constructor that takes arbitrary dates and values
    /// </summary>
    /// <param name="fxRate">Fx Rate</param>
    /// <param name="dates">Dates</param>
    /// <param name="vals">Values</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="curveName">Name of the curve.</param>
    public FxCurve(FxRate fxRate, Dt[] dates, double[] vals,
                   string[] tenorNames, string curveName)
      : this(new FxForwardCurve(curveName, null, fxRate,
        dates, vals, tenorNames, false))
    {
    }

    internal FxCurve(FxForwardCurve curve)
      : base(curve.SpotFxRate.Spot)
    {
      var ffi = new FxForwardInterpolator(curve);
      _fxInterpolator = ffi;
      var fxRate = curve.SpotFxRate;
      Initialize(fxRate.Spot, ffi);
      Add(fxRate.Spot, fxRate.Rate);
      Tenors = ffi.SpotFxCurve.Tenors;
      Calibrator = new FxCalibrator(fxRate.Spot, ffi);
      Ccy = fxRate.ToCcy;
      if (!String.IsNullOrEmpty(curve.Name))
      {
        Name = curve.Name;
        ffi.Curve.Name = curve.Name;
      }
      ffi.UnderlyingCurve = this;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Calculate the forward fx rate using the native quoting order of the curve
    /// </summary>
    /// <param name="date">The date</param>
    /// <returns>Forward fx rate quoted native for curve</returns>
    public double FxRate(Dt date)
    {
      return Interpolate(date);
    }

    /// <summary>
    /// Forward basis
    /// </summary>
    /// <param name="date">Date</param>
    /// <param name="fromCcy">From ccy</param>
    /// <param name="toCcy">To ccy</param>
    /// <returns>forward basis</returns>
    public double ForwardBasis(Dt date, Currency fromCcy, Currency toCcy)
    {
      return FxInterpolator.ForwardBasis(date, SpotFxRate.IsInverse(fromCcy, toCcy));
    }

    /// <summary>
    /// Calculate the spot fx rate using the specified quoting order
    /// </summary>
    /// <param name="fromCcy">The currency to convert from</param>
    /// <param name="toCcy">The currency to convert to</param>
    /// <returns>The forward fx rate at the given date using the specified quoting order</returns>
    public double FxRate(Currency fromCcy, Currency toCcy)
    {
      return FxInterpolator.Interpolate(SpotFxRate.Spot, SpotFxRate.IsInverse(fromCcy, toCcy));
    }

    /// <summary>
    /// Calculate the forward fx rate using the specified quoting order
    /// </summary>
    /// <param name="date">The date</param>
    /// <param name="fromCcy">The currency to convert from</param>
    /// <param name="toCcy">The currency to convert to</param>
    /// <returns>The forward fx rate at the given date using the specified quoting order</returns>
    public double FxRate(Dt date, Currency fromCcy, Currency toCcy)
    {
      return FxInterpolator.Interpolate(date, SpotFxRate.IsInverse(fromCcy, toCcy));
    }

    /// <summary>
    /// Calculate the forward fx points using the native quoting order of the curve
    /// </summary>
    /// <param name="date">The date</param>
    /// <returns>Forward fx points quoted native for curve</returns>
    public double FxPoints(Dt date)
    {
      return (Interpolate(date) - Interpolate(SpotFxRate.Spot))*10000.0;
    }

    /// <summary>
    /// Calculate the forward fx points using the specified quoting order
    /// </summary>
    /// <param name="date">The date</param>
    /// <param name="fromCcy">The currency to convert from</param>
    /// <param name="toCcy">The currency to convert to</param>
    /// <returns>The forward fx points at the given date using the specified quoting order</returns>
    public double FxPoints(Dt date, Currency fromCcy, Currency toCcy)
    {
      var inverse = SpotFxRate.IsInverse(fromCcy, toCcy);
      return (FxInterpolator.Interpolate(date, inverse) - FxInterpolator.Interpolate(SpotFxRate.Spot, inverse))*10000.0;
    }

    /// <summary>
    /// True if fx curve based from specified currencies (independent of quoting order)
    /// </summary>
    /// <param name="ccy1">First currency</param>
    /// <param name="ccy2">Second currency</param>
    /// <returns>True if fx curve from ccy1/ccy2 or ccy2/ccy1</returns>
    public bool From(Currency ccy1, Currency ccy2)
    {
      return (Contains(ccy1) && Contains(ccy2));
    }

    /// <summary>
    /// True if Fx curve based on specified currency
    /// </summary>
    /// <param name="ccy">Currency to test</param>
    /// <returns>True if fx curve from ccy</returns>
    public bool Contains(Currency ccy)
    {
      return (Ccy1 == ccy || Ccy2 == ccy);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Ccy1 (base/foreign/source/from) currency
    /// </summary>
    public Currency Ccy1
    {
      get { return FxInterpolator.SpotFxRate.FromCcy; }
    }

    /// <summary>
    /// Ccy2 (quoting/domestic/destination/to) currency
    /// </summary>
    public Currency Ccy2
    {
      get { return FxInterpolator.SpotFxRate.ToCcy; }
    }

    /// <summary>
    /// Fx rate object. 
    /// </summary>
    public FxRate SpotFxRate
    {
      get { return FxInterpolator.SpotFxRate; }
    }

    /// <summary>
    /// FX spot rate. 
    /// </summary>
    public double SpotRate
    {
      get { return FxInterpolator.SpotFxRate.Rate; }
    }

    /// <summary>
    /// Fx spot date
    /// </summary>
    public Dt SpotDate
    {
      get { return FxInterpolator.SpotFxRate.Spot; }
    }

    /// <summary>
    /// Fx Basis curve 
    /// </summary>
    public DiscountCurve BasisCurve
    {
      get
      {
        var di = FxInterpolator as FxDiscountInterpolator;
        return di == null ? null : di.BasisCurve;
      }
    }

    /// <summary>
    /// Ccy1 (base/foreign/source/from) discount curve
    /// </summary>
    public DiscountCurve Ccy1DiscountCurve
    {
      get
      {
        var di = FxInterpolator as FxDiscountInterpolator;
        if (di != null)
          return di.ForeignDiscount;
        var ti = FxInterpolator as FxTriangulationInterpolator;
        if (ti != null)
          return ti.Curve1.Ccy2 == Ccy1 ? ti.Curve1.Ccy2DiscountCurve : ti.Curve1.Ccy1DiscountCurve; 
        return null; 
      }
    }

    /// <summary>
    /// Ccy2 (quoting/domestic/destination/to) currency
    /// </summary>
    public DiscountCurve Ccy2DiscountCurve
    {
      get
      {
        var di = FxInterpolator as FxDiscountInterpolator;
        if(di != null)
          return di.DomesticDiscount;
        var ti = FxInterpolator as FxTriangulationInterpolator;
        if (ti != null)
          return ti.Curve2.Ccy2 == Ccy2 ? ti.Curve2.Ccy2DiscountCurve : ti.Curve2.Ccy1DiscountCurve; 
        return null; 
      }
    }

    /// <summary>
    /// Boolean flag that determines whether the curves has embedded
    ///  a discount curve.
    /// </summary>
    internal bool WithDiscountCurve
    {
      get
      {
        var di = FxInterpolator as FxDiscountInterpolator;
        return di != null && di.DomesticDiscount != null;
      }
    }

    /// <summary>
    /// Boolean flag that determines whether the curves is creates from user supplied points or otherwise
    /// </summary>
    internal bool IsSupplied
    {
      get { return !(FxInterpolator is FxDiscountInterpolator); }
    }

    /// <summary>
    /// Fx interpolator 
    /// </summary>
    public FxInterpolator FxInterpolator
    {
      get { return _fxInterpolator; }
    }

    #endregion Properties

    #region Data

    private readonly FxInterpolator _fxInterpolator;

    #endregion
  }

  /// <summary>
  ///   A static utility class containing FX curve related extension methods.
  /// </summary>
  public static class FxCurveUtil
  {
    #region Extension methods

    /// <summary>
    ///  Gets the FX spot days corresponding the specified date
    /// </summary>
    /// <param name="fx">The FX curve</param>
    /// <param name="expiry">The expiry date</param>
    /// <returns>The expiry spot date</returns>
    internal static Dt GetExpirySpot(this FxCurve fx, Dt expiry)
    {
      if (fx == null || fx.SpotDays <= 0)
        return expiry;
      FxRate fxSpot = fx.SpotFxRate;
      var ccy1Cal = fxSpot?.ToCcyCalendar ?? Calendar.None;
      var ccy2Cal = fxSpot?.FromCcyCalendar ?? Calendar.None;
      return FxUtil.FxSpotDate(expiry, fx.SpotDays, ccy1Cal, ccy2Cal);
    }

    /// <summary>
    /// Recursively retrievs the component curves of a given type
    /// and adds them to a specified list.
    /// </summary>
    /// <param name="fxCurve">The fx curve.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="includeInverseCurve">if set to <c>true</c>, include the inverse curve in the selected set.</param>
    /// <param name="list">The list.  If null, a List of type T is created.</param>
    /// <returns>The list of curves.</returns>
    /// <remarks></remarks>
    internal static IList<CalibratedCurve> GetComponentCurves(
      this FxCurve fxCurve, Func<CalibratedCurve, bool> selector,
      bool includeInverseCurve, IList<CalibratedCurve> list)
    {
      if (list == null) list = new List<CalibratedCurve>();
      fxCurve.RecursiveGetComponentCurves(selector,
        includeInverseCurve, null, list);
      return list;
    }

    /// <summary>
    /// Recursively retrievs the component curves of a given type
    /// and adds them to a specified list.
    /// </summary>
    /// <typeparam name="T">The type of curve to retirieve</typeparam>
    /// <param name="fxCurve">The fx curve.</param>
    /// <param name="list">The list.  If null, a List of type T is created.</param>
    /// <returns>The list of curves.</returns>
    public static IList<CalibratedCurve> GetComponentCurves<T>(
      this FxCurve fxCurve,
      IList<CalibratedCurve> list) where T : CalibratedCurve
    {
      if (list == null) list = new List<CalibratedCurve>();
      fxCurve.RecursiveGetComponentCurves(curve => curve is T,
        false, null, list);
      return list;
    }

    internal static IList<CalibratedCurve> GetComponentCurves<T>(
      this FxCurve fxCurve, bool includeInverseCurve,
      IList<CalibratedCurve> list) where T : CalibratedCurve
    {
      if (list == null) list = new List<CalibratedCurve>();
      fxCurve.RecursiveGetComponentCurves(curve => curve is T,
        includeInverseCurve, null, list);
      return list;
    }
    /// <summary>
    /// Retrieves the component curves of type T from a sequence of curves.
    /// </summary>
    /// <typeparam name="T">A type derived from <c>CalibratedCurve</c></typeparam>
    /// <param name="curves">The curves.</param>
    /// <param name="list">The list.</param>
    /// <returns>A list of curves.</returns>
    internal static IList<CalibratedCurve> GetAllComponentCurves<T>(
      this IEnumerable<CalibratedCurve> curves,
      IList<CalibratedCurve> list) where T : CalibratedCurve
    {
      if (curves == null) return null;
      if (list == null) list = new List<CalibratedCurve>();
      foreach (var curve in curves)
      {
        var fxCurve = curve as FxCurve;
        if (fxCurve != null) GetComponentCurves<T>(fxCurve, list);
        else
        {
          var t = curve as T;
          if (t!= null && !list.Contains(t)) list.Add(curve);
        }
      }
      return list;
    }

    /// <summary>
    /// Gets all FX forward curves from a sequence of curves.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <returns>A list of FX forward curves.</returns>
    /// <remarks></remarks>
    public static IList<CalibratedCurve> GetAllFxForwardCurves(
      this IEnumerable<CalibratedCurve> curves)
    {
      return GetAllComponentCurves<FxForwardCurve>(curves, null);
    }

    private static void RecursiveGetComponentCurves(
      this FxCurve fxCurve,
      Func<CalibratedCurve, bool> select, bool includeInverseCurve,
      IList<FxCurve> checkedFxCurves,
      ICollection<CalibratedCurve> list)
    {
      foreach (var curve in fxCurve.FxInterpolator.EnumerateComponentCurves())
      {
        var cc = curve as CalibratedCurve;
        if (cc == null) continue;
        var fx = cc as FxCurve;
        if (fx != null)
        {
          if (checkedFxCurves != null && checkedFxCurves.Contains(fx)) continue;
          if (checkedFxCurves == null) checkedFxCurves = new List<FxCurve>();
          checkedFxCurves.Add(fx);
          fx.RecursiveGetComponentCurves(select, includeInverseCurve,
            checkedFxCurves, list);
        }
        else if (select(cc) && !list.Contains(cc))
        {
          list.Add(cc);
          if (!includeInverseCurve) continue;
          var cal = cc.Calibrator as IInverseCurveProvider;
          if (cal != null && (cc = cal.InverseCurve) != null && !list.Contains(cc))
            list.Add(cc);
        }
      }
    }

    /// <summary>
    /// Bumps all the FX rates inside an FxCurve.
    /// </summary>
    /// <param name="curve">The FX curve.</param>
    /// <param name="tenor">The tenor to bump.</param>
    /// <param name="bumpUnit">The bump unit.</param>
    /// <param name="up">if set to <c>true</c> [up].</param>
    /// <param name="bumpRelative">if set to <c>true</c> [bump relative].</param>
    /// <param name="refit">if set to <c>true</c> [refit].</param>
    /// <returns></returns>
    /// <exclude/>
    public static double[] BumpFxRates(this FxCurve curve, string tenor,
      double bumpUnit, bool up, bool bumpRelative, bool refit)
    {
      // set spot rate too as this is sometimes used directly (e.g. by CCR)
      //fxCurve.SpotFxRate.Rate = fxCurve.GetVal(0);
      return CurveUtil.CurveBump(
        curve.GetComponentCurves<FxForwardCurve>(null).ToArray(),
        (tenor != null) ? new[] { tenor } : null, new[] { bumpUnit },
        up, bumpRelative, refit, null);
    }

    internal static bool HasFxForwardQuote(this CalibratedCurve curve)
    {
      return curve is FxForwardCurve || (curve.Calibrator is FxBasisFitCalibrator
        && curve.Tenors.Any(t => t.Product is FxForward));
    }
    #endregion

    /// <summary>
    /// Calculates the conventional value of one pip given an Fx rate
    /// </summary>
    /// <param name="fxRate"></param>
    /// <returns></returns>
    public static double OnePip(double fxRate)
    {
      const int pipsSignificantDigits = 5;
      return OnePip(fxRate, pipsSignificantDigits);
    }

    /// <summary>
    /// Calculates the conventional value of one pip given an Fx rate
    /// </summary>
    /// <param name="fxRate"></param>
    /// <param name="significantDigits"></param>
    /// <returns></returns>
    public static double OnePip(double fxRate, int significantDigits)
    {
      int logRate = (int)Math.Round(Math.Log10(fxRate) + 0.5);
      int pipsPlace = -significantDigits + logRate;
      return Math.Pow(10, pipsPlace);
    }

    /// <summary>
    /// Create an FX forward curve
    /// </summary>
    /// <param name="curveName">Curve name</param>
    /// <param name="asOf">AsOf (trade/pricing/horizon) date</param>
    /// <param name="spot">Spot fx date</param>
    /// <param name="ccy1">Currency 1 (the base/foreign/source/from currency)</param>
    /// <param name="ccy2">Currency 2 (the counter/quoting/domestic/destination/to currency)</param>
    /// <param name="fxRate">Spot fx rate for one unit of ccy1 in terms of ccy2 (ccy1/ccy2)</param>
    /// <param name="instruments">Type of financial instrument matching market quote</param>
    /// <param name="tenors">Tenor of product matching market quotes</param>
    /// <param name="dates">Dates matching market quotes</param>
    /// <param name="quotes">Market quotes for the calibration</param>
    /// <param name="fxCal">Calendar for fx settlement. This is usually 
    /// the calendar for each currency + NYB</param>
    /// <param name="swapCal">Calendar for basis swap leg settlement. 
    /// This is usually the calendar for each currency + LNB</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <param name="ccy1IrCurve">The currency 1 interest rate curve. To support dual rate curves,
    ///  put the projection curve; otherwise, the discount curve</param>
    /// <param name="ccy2IrCurve">The currency 2 interest rate curve. To support dual rate curves, 
    /// put the projection curve; otherwise, the discount curve</param>
    /// <param name="fitSettings">Curve fitting tuning parameters</param>
    ///<param name="ccy1RateIndex">Ccy1 rate index, only needed if the ccy1 
    /// rate curve is backward-compatible style </param>
    ///<param name="ccy2RateIndex">Ccy2 rate index, only needed if the ccy2 
    /// rate curve is backward-compatible style </param>
    ///<returns>Fx forward curve created</returns>
    public static FxCurve FxCurveFit(
      string curveName,
      Dt asOf,
      Dt spot,
      Currency ccy1,
      Currency ccy2,
      double fxRate,
      string[] instruments,
      string[] tenors,
      Dt[] dates,
      double[] quotes,
      Calendar fxCal,
      Calendar swapCal,
      BasisSwapSide swapSpreadLeg,
      DiscountCurve ccy1IrCurve,
      DiscountCurve ccy2IrCurve,
      CurveFitSettings fitSettings,
      ReferenceIndex ccy1RateIndex,
      ReferenceIndex ccy2RateIndex)
    {
      if (!spot.IsEmpty() && !spot.IsValid())
        throw new ArgumentException("The provided spot date is not valid");
      if (spot.IsEmpty()) spot = asOf;

      var spotFx = new FxRate(asOf, spot, ccy1, ccy2, fxRate);
      return FxCurveFit(curveName, spotFx, instruments, tenors, dates,
        quotes,fxCal, swapCal, swapSpreadLeg,
        ccy1IrCurve, ccy2IrCurve, fitSettings, ccy1RateIndex,ccy2RateIndex);
    }

    /// <summary>
    /// Create an FX forward curve
    /// </summary>
    /// <param name="curveName">Curve name</param>
    /// <param name="spotFx">Fx rate object</param>
    /// <param name="instruments">Type of financial instrument matching market quote</param>
    /// <param name="tenors">Tenor of product matching market quotes</param>
    /// <param name="dates">Dates matching market quotes</param>
    /// <param name="quotes">Market quotes for the calibration</param>
    /// <param name="fxCal">Calendar for fx settlement. This is usually 
    /// the calendar for each currency + NYB</param>
    /// <param name="swapCal">Calendar for basis swap leg settlement. 
    /// This is usually the calendar for each currency + LNB</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <param name="ccy1IrCurve">The currency 1 interest rate curve. To support dual rate curves,
    ///  put the projection curve; otherwise, the discount curve</param>
    /// <param name="ccy2IrCurve">The currency 2 interest rate curve. To support dual rate curves, 
    /// put the projection curve; otherwise, the discount curve</param>
    /// <param name="fitSettings">Curve fitting tuning parameters</param>
    ///<param name="ccy1RateIndex">Ccy1 rate index, only needed if the ccy1 
    /// rate curve is backward-compatible style </param>
    ///<param name="ccy2RateIndex">Ccy2 rate index, only needed if the ccy2 
    /// rate curve is backward-compatible style </param>
    public static FxCurve FxCurveFit(
      string curveName,
      FxRate spotFx,
      string[] instruments,
      string[] tenors,
      Dt[] dates,
      double[] quotes,
      Calendar fxCal,
      Calendar swapCal,
      BasisSwapSide swapSpreadLeg,
      DiscountCurve ccy1IrCurve,
      DiscountCurve ccy2IrCurve,
      CurveFitSettings fitSettings,
      ReferenceIndex ccy1RateIndex,
      ReferenceIndex ccy2RateIndex)
    {
      // Verify we have right number of tenors and dates
      if (dates.Length == 0 && tenors.Length == 0)
      {
        if (quotes.Length != 0)
          throw new ArgumentException("Number of quotes must match tenors or dates");
        if (ccy1IrCurve == null || ccy2IrCurve == null)
          throw new ArgumentException("Tenors or dates must be specified");
      }
      if (dates.Length != 0 && dates.Length != quotes.Length)
        throw new ArgumentException("Number of dates must match number of quotes if specified");
      if (tenors.Length != 0 && tenors.Length != quotes.Length)
        throw new ArgumentException("Number of tenors must match number of quotes if specified");
      if (instruments.Length != 0 && instruments.Length != quotes.Length && instruments.Length != 1)
        throw new ArgumentException("Number of instruments must match number of quotes or one if specified");

      if ((ccy1RateIndex == null && ccy2RateIndex != null) || (ccy2RateIndex == null && ccy1RateIndex != null))
      {
        throw new ArgumentException("Both or none rate indexes must be provided");
      }

      // Fill out dates and tenors where we have quotes (ignore lines where we don't have quotes)
      var fwdDates = new List<Dt>();
      var fwdTenors = new List<string>();
      var fwdQuotes = new List<double>();
      var swapDates = new List<Dt>();
      var swapTenors = new List<string>();
      var swapQuotes = new List<double>();
      for (int i = 0; i < quotes.Length; i++)
      {
        if (Math.Abs(quotes[i]) > Double.Epsilon)
        {
          string tenor = (tenors.Length != 0) ? tenors[i] : String.Format("{0}", dates[i]);
          string instrument = (instruments.Length > 1)
                                ? instruments[i]
                                : (instruments.Length == 1) ? instruments[0] : "FxFwd";
          if (String.Compare(instrument, "FxFwd", StringComparison.OrdinalIgnoreCase) == 0 ||
              String.Compare(instrument, "Fwd", StringComparison.OrdinalIgnoreCase) == 0)
          {
            Dt date = (dates.Length != 0) ? dates[i] : Dt.Roll(Dt.Add(spotFx.Spot, 
              Tenor.Parse(tenors[i])), BDConvention.Following, fxCal);
            fwdQuotes.Add(quotes[i]);
            fwdDates.Add(date);
            fwdTenors.Add(tenor);
          }
          else if (String.Compare(instrument, "BasisSwap", StringComparison.OrdinalIgnoreCase) == 0 ||
                   String.Compare(instrument, "Basis", StringComparison.OrdinalIgnoreCase) == 0)
          {
            // Don't roll swap maturities. Need to validate this is the right 
            // behaviour but this is what we have been doing. RTD Jan'12
            // Dt date = (dates.Length != 0) ? dates[i] : Dt.Roll(Dt.Add(spot, 
            // Tenor.Parse(tenors[i])), BDConvention.Following, swapCal);
            Dt date = (dates.Length != 0) ? dates[i] : Dt.Add(spotFx.Spot, Tenor.Parse(tenors[i]));
            swapQuotes.Add(quotes[i]);
            swapDates.Add(date);
            swapTenors.Add(tenor);
          }
          else
            throw new ArgumentException(String.Format("Invalid instrument [{0}]", instrument));
        }
      }

      FxCurve fxCurve = null;
      if (ccy1RateIndex != null)
      {
        fxCurve = FxBasisCalibrationHelper.FxCurveCreate(spotFx,
          fwdTenors.ToArray(), fwdDates.ToArray(), fwdQuotes.ToArray(),
          swapTenors.ToArray(), swapDates.ToArray(), swapQuotes.ToArray(),
          swapCal, swapSpreadLeg, ccy1IrCurve, ccy2IrCurve, fitSettings,
          curveName, ccy1RateIndex, ccy2RateIndex);
      }
      else
      {
        // Fit curve
        fxCurve = FxCurve.Create(spotFx, fwdTenors.ToArray(), 
          fwdDates.ToArray(),fwdQuotes.ToArray(),swapTenors.ToArray(), 
          swapDates.ToArray(), swapQuotes.ToArray(), swapCal, swapSpreadLeg, 
          ccy1IrCurve, ccy2IrCurve, fitSettings,curveName);
      }
      fxCurve.SpotDays = spotFx.SettleDays;
      fxCurve.SpotCalendar = spotFx.FromCcyCalendar==spotFx.ToCcyCalendar
        ? spotFx.FromCcyCalendar
        : new Calendar(spotFx.FromCcyCalendar, spotFx.ToCcyCalendar);
      return fxCurve;
    }
  }
}
