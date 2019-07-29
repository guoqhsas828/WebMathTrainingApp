/*
*  -2012. All rights reserved.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Curves
{
  #region SpotPrice

  /// <summary>
  /// Spot asset price
  /// </summary>
  [Serializable]
  internal abstract class SpotPrice : BaseEntityObject, ISpot
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settleDays">Settlement days to spot date</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="ccy">Denomination currency</param>
    /// <param name="price">Spot price</param>
    protected SpotPrice(Dt asOf, int settleDays, Calendar calendar, Currency ccy, double price)
    {
      AsOf = asOf;
      Spot = Dt.AddDays(asOf, SettleDays, calendar);
      SettleDays = settleDays;
      Calendar = calendar;
      Ccy = ccy;
      Price = price;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Accessor for currency
    /// </summary>
    public Currency Ccy { get; private set; }

    /// <summary>
    /// Spot fx rate
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// AsOf date of FxRate
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// Set Spot Date 
    /// </summary>
    public Dt Spot { get; set; }

    ///<summary>
    /// Business day calendar for the From Currency
    ///</summary>
    protected Calendar Calendar { get; private set; }

    ///<summary>
    /// Number of spot days in this market
    ///</summary>
    protected int SettleDays { get; private set; }

    /// <summary>
    /// Spot Asset ID
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Spot price
    /// </summary>
    public double Value
    {
      get { return Price; }
      set { Price = value; }
    }

    #endregion
  }

  #endregion

  #region SpotTenorQuoteHandler

  /// <summary>
  ///  Implementation of AssetSpot tenor quote handler
  /// </summary>
  [Serializable]
  internal sealed class SpotTenorQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
  {
    #region ICurveTenorQuoteHandler Members

    public IMarketQuote GetCurrentQuote(CurveTenor tenor)
    {
      return new CurveTenor.Quote(QuotingConvention.FlatPrice, tenor.MarketPv);
    }

    public double GetQuote(
      CurveTenor tenor,
      QuotingConvention targetQuoteType,
      Curve curve, Calibrator calibrator,
      bool recalculate)
    {
      if (targetQuoteType != QuotingConvention.FlatPrice)
      {
        throw new QuoteConversionNotSupportedException(
          targetQuoteType, QuotingConvention.FlatPrice);
      }
      return tenor.MarketPv;
    }

    public void SetQuote(
      CurveTenor tenor,
      QuotingConvention quoteType, double quoteValue)
    {
      if (quoteType != QuotingConvention.FlatPrice)
        throw new QuoteConversionNotSupportedException(QuotingConvention.FlatPrice, quoteType);
      tenor.MarketPv = quoteValue;
      //sync product for hedges
      var assetSpot = tenor.Product as SpotAsset;
      if (assetSpot != null)
        assetSpot.SpotPrice = quoteValue;
    }

    
    public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags bumpFlags)
    {
      return tenor.BumpRawQuote(bumpSize, bumpFlags, (t, bumpedQuote) =>
      {
        t.MarketPv = bumpedQuote;
        ((SpotAsset)t.Product).SpotPrice = t.MarketPv;
      });
    }

    public IPricer CreatePricer(CurveTenor tenor, Curve curve, Calibrator calibrator)
    {
      var ccurve = curve as CalibratedCurve;
      if (ccurve == null)
        throw new NotSupportedException("Asset price quote handler works only with calibrated curves");
      return calibrator.GetPricer(ccurve, tenor.Product);
    }

    #endregion
  }

  #endregion

  #region ForwardPriceCurve

  /// <summary>
  /// Term structure of forward asset prices
  /// </summary>
  [Serializable]
  public abstract class ForwardPriceCurve : CalibratedCurve, IForwardPriceCurve, ICalibratedCurveContainer
  {
    internal static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(ForwardPriceCurve));

    #region Constructors
    /// <summary>
    /// Term structure of forward prices where spot price is not readily observable in the market 
    /// </summary>
    /// <param name="calibrator"></param>
    /// <param name="interp"></param>
    protected internal ForwardPriceCurve(ForwardPriceCalibrator calibrator, Interp interp)
      : base(calibrator, interp, DayCount.None, Frequency.None)
    {
      SpotPriceCurve = null;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="spot">Spot price of the asset</param>
    protected internal ForwardPriceCurve(ISpot spot)
      : base(spot.Spot)
    {
      SpotPriceCurve = new SpotPriceCurve(spot);
      DayCount = DayCount.None;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Target curve to calibrate. Might be component curve rather than the curve itself.
    /// </summary>
    public abstract CalibratedCurve TargetCurve { get; protected set; }

    /// <summary>
    /// Calibrator
    /// </summary>
    public ForwardPriceCalibrator ForwardPriceCalibrator
    {
      get { return Calibrator as ForwardPriceCalibrator; }
    }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; protected set; }

    /// <summary>
    /// Spot asset price 
    /// </summary>
    public ISpot Spot => SpotPriceCurve?.Spot;

    internal SpotPriceCurve SpotPriceCurve { get; }

    /// <summary>
    /// Average carry rate net of funding rate, between spot date and delivery date. 
    /// The Spot-Forward parity then states that 
    /// <m>F_t(T) = S_t\exp(\int_t^T f_t(u) + \delta_t(u)) </m>
    /// The forward carry rate is given by <m>c_t(u) = f_t(u) + \delta_t(u)</m> where <m>f_t(u)</m> is the forward funding cost, 
    /// <m>s_t(u)</m> is the forward storage cost and <m>\delta_t(u)</m> arises from one or more of the following
    /// <list type="bullet">
    /// <item><description>Continuously paid dividends</description></item>
    /// <item><description>Storage costs</description></item>
    /// <item><description>Convenience yield</description></item>
    /// </list>  
    /// </summary>
    public abstract double CarryRateAdjustment(Dt spot, Dt delivery);
   
    /// <summary>
    /// Discretely cashflows associated to holding the spot asset 
    /// </summary>
    public abstract IEnumerable<Tuple<Dt,DividendSchedule.DividendType,double>> CarryCashflow { get; }

    /// <summary>
    /// Spot price
    ///</summary>
    ///<remarks>For some assets, spot prices are not observed. In that case, return NaN.</remarks>
    public double SpotPrice
    {
      get { return SpotPriceCurve?.GetSpotPrice(this is InflationCurve) ?? double.NaN; }
      set { SpotPriceCurve?.SetSpotPrice(value); }
    }

    /// <summary>
    /// Curve name
    /// </summary>
    public new string Name
    {
      get { return base.Name; }
      set
      {
        base.Name = value;
        SpotPriceCurve?.SetNameFromForward(value);
      }
    }

    #endregion

    #region Methods
    /// <summary>
    /// Clone
    /// </summary>
    /// <returns>Clone</returns>
    public override object Clone()
    {
      var retVal = (ForwardPriceCurve)base.Clone();
      if (TargetCurve == this)
        retVal.TargetCurve = retVal;
      else if (TargetCurve != null)
        retVal.TargetCurve = (CalibratedCurve)TargetCurve.Clone();
      return retVal;
    }

    /// <summary>
    /// Set curve points and tenors
    /// </summary>
    /// <param name="curve">Curve</param>
    public override void Copy(Curve curve)
    {
      base.Copy(curve);
      var fpc = curve as ForwardPriceCurve;
      if ((fpc != null) && (fpc.TargetCurve != fpc) && (fpc.TargetCurve != null))
        TargetCurve.Copy(fpc.TargetCurve);
    }

    /// <summary>
    /// Enumerates the component curves.
    /// </summary>
    /// <returns>IEnumerable&lt;CalibratedCurve&gt;.</returns>
    public override IEnumerable<CalibratedCurve> EnumerateComponentCurves()
    {
      if (DiscountCurve != null)
        yield return DiscountCurve;
      if (SpotPriceCurve != null && !(this is InflationCurve))
        yield return SpotPriceCurve;
    }

    /// <summary>
    /// Component curves
    /// </summary>
    /// <typeparam name="T">Curve type</typeparam>
    /// <returns></returns>
    public IEnumerable<CalibratedCurve> EnumerateComponentCurves<T>() where T : CalibratedCurve
    {
      return EnumerateComponentCurves().OfType<T>();
    }

    /// <summary>
    /// Add point at AsOf
    /// </summary>
    protected void AddSpot()
    {
      if (Spot == null)
        return;
      var assetSpot = new SpotAsset(Spot) {Description = Spot.Name};
      var tenor = new CurveTenor(Spot.Name, assetSpot, Spot.Value, 0.0, 0.0, 1.0, new SpotTenorQuoteHandler());
      Tenors.Add(tenor);
      Add(AsOf, Spot.Value);

      // Same tenor should also add to the spot curve
      SpotPriceCurve.SetSpotTenor(tenor);
    }

    [OnDeserialized]
    private void SynchronizeSpotTenor(StreamingContext ctx)
    {
      // Ensure reference equality of the spot tenors
      var tenor = Tenors.FirstOrDefault();
      if (tenor == null) return;
      SpotPriceCurve?.SetSpotTenor(tenor);
    }

    #endregion
  }

  #endregion

  #region ForwardPriceCurveUtil

  /// <summary>
  ///  A static utility class containing ForwardPriceCurve related extension methods.
  /// </summary>
  public static class ForwardPriceCurveUtil
  {
    #region BumpSize
    
    /// <summary>
    /// Calculate spot/forward price bump size
    /// </summary>
    /// <param name="tenor">Curve tenor</param>
    /// <param name="bumpSize">Bump size</param>
    /// <param name="bumpFlags">Bump flags</param>
    /// <param name="update">Update tenor</param>
    /// <returns>Bump size</returns>
    internal static double BumpPriceQuote(this CurveTenor tenor, double bumpSize, 
      BumpFlags bumpFlags, Action<CurveTenor, double> update)
    {
      bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
      bool up = (bumpFlags & BumpFlags.BumpDown) == 0;
      double bumpAmt = (up) ? bumpSize : -bumpSize;
      const int pipsSignificantDigits = 5;
      var originalQuote = tenor.OriginalQuote.Value;
      int pipsPlace = -pipsSignificantDigits + (int)Math.Round(Math.Log10(originalQuote) + 0.5);
      double onePip = Math.Pow(10, pipsPlace);
      if (bumpRelative)
        bumpAmt = bumpAmt * originalQuote;
      bumpAmt *= onePip;
      if (originalQuote + bumpAmt < 0.0)
      {
        ForwardPriceCurve.Logger.DebugFormat(
          "Unable to bump tenor '{0}' by {1}, bump {2} instead",
          tenor.Name, bumpAmt, -originalQuote / 2);
        bumpAmt = -originalQuote / 2;
      }
      update(tenor, originalQuote + bumpAmt);
      return ((up) ? bumpAmt : -bumpAmt) / onePip;
    }

    /// <summary>
    /// Calculate futures price bump size
    /// </summary>
    /// <param name="tenor">Curve tenor</param>
    /// <param name="bumpSize">Bump size</param>
    /// <param name="bumpFlags">Bump flags</param>
    /// <param name="update">Update tenor</param>
    /// <returns>Bump size</returns>
    internal static double BumpFuturesPriceQuote(this CurveTenor tenor, double bumpSize,
      BumpFlags bumpFlags, Action<CurveTenor, double> update)
    {
      var future = tenor.Product as FutureBase;
      if(future == null)
        return BumpPriceQuote(tenor, bumpSize, bumpFlags, update);
      bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
      var originalQuote = tenor.OriginalQuote.Value;
      if (bumpRelative)
        bumpSize = bumpSize * originalQuote;
      var bumpSign = Math.Sign(bumpSize);
      var bumpAmt = Math.Ceiling(Math.Abs(bumpSize) / future.TickSize) * future.TickSize;
      int sign = (bumpFlags & BumpFlags.BumpDown) == 0 ? bumpSign : -bumpSign;
      while (originalQuote + sign * bumpAmt < 0.0)
        bumpAmt -= future.TickSize;
      update(tenor, originalQuote + bumpSign * bumpAmt);
      return bumpSign * bumpAmt;
    }


    internal static double BumpRawQuote(this CurveTenor tenor, double bumpSize,
      BumpFlags bumpFlags, Action<CurveTenor, double> update)
    {
      var originalQuote = tenor.OriginalQuote.Value;
      bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
      bool upBump = (bumpFlags & BumpFlags.BumpDown) == 0;
      var bumpAmt = upBump ? bumpSize : -bumpSize;
      if (bumpRelative) bumpAmt *= originalQuote;
      if (originalQuote + bumpAmt < 0)
      {
        ForwardPriceCurve.Logger.DebugFormat(
          "Unable to bump tenor '{0}' by {1}, bump {2} instead",
          tenor.Name, bumpAmt, -originalQuote / 2);
        bumpAmt = -originalQuote / 2;
      }
      update(tenor, originalQuote + bumpAmt);
      return upBump ? bumpAmt : -bumpAmt;
    }

    #endregion

    #region Extension methods

    /// <summary>
    /// Recursively retrievs the component curves of a given type
    /// and adds them to a specified list.
    /// </summary>
    /// <typeparam name="T">The type of curve to retirieve</typeparam>
    /// <param name="forwardPriceCurve">The forward curve.</param>
    /// <param name="list">The list.  If null, a List of type T is created.</param>
    /// <returns>The list of curves.</returns>
    internal static IList<CalibratedCurve> GetComponentCurves<T>(
      this ForwardPriceCurve forwardPriceCurve,
      IList<CalibratedCurve> list) where T : CalibratedCurve
    {
      if (list == null) list = new List<CalibratedCurve>();
      foreach (var curve in forwardPriceCurve.EnumerateComponentCurves<T>())
      {
        if (!list.Contains(curve)) list.Add(curve);
        var fc = curve as ForwardPriceCurve;
        if (fc == null) continue; // not a fx curve
        fc.GetComponentCurves<T>(list); // recursion
      }
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
        var fac = curve as ForwardPriceCurve;
        if (fac != null) GetComponentCurves<T>(fac, list);
        else
        {
          var t = curve as T;
          if (t != null && !list.Contains(t)) list.Add(curve);
        }
      }
      return list;
    }

    /// <summary>
    /// Gets all asset forward curves from a sequence of curves.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <returns>A list of FX forward curves.</returns>
    /// <remarks></remarks>
    public static IList<CalibratedCurve> GetAllAssetForwardCurves(
      this IEnumerable<CalibratedCurve> curves)
    {
      return GetAllComponentCurves<ForwardPriceCurve>(curves, null);
    }

    #endregion
  }

  #endregion

  #region Spot price curve

  [Serializable]
  internal sealed class SpotPriceCurve : CalibratedCurve
  {
    internal ISpot Spot { get; }

    public SpotPriceCurve(ISpot spot, string name = null)
      : base(spot.Spot)
    {
      Spot = spot;
      Calibrator = SpotCalibraor.Instance;
      Name = name ?? GetNameFromSpot();
      Interp = new Flat();
      DayCount = DayCount.None;
      Fit();
    }

    internal void SetNameFromForward(string forwardCurveName)
    {
      Spot.Name = Regex.Replace(forwardCurveName.ToLower(),
        "[^a-zA-Z0-9]", "").Replace("curve", "").Trim();
      Name = GetNameFromSpot();
      var tenor = Tenors.FirstOrDefault();
      if (tenor != null && string.IsNullOrEmpty(tenor.Name))
        tenor.Name = Name;
    }

    internal void SetSpotPrice(double value)
    {
      Spot.Value = value;
    }

    internal double GetSpotPrice(bool isInflation)
    {
      if (isInflation)
      {
        // Used the old approach for inflation temporarily
        // before all the regression differences resolved.  
        return Spot.Value;
      }

      // If the Shift Overlay Curve is set, we must call Interpolate
      // function to find the current spot rate.
      if (ShiftOverlay != null)
      {
        return Interpolate(Spot.Spot);
      }

      // Otherwise, if Spot object is synchronized with the curve tenor,
      //   then the curve point is the fitted value.
      // Note: the curve point may be set by sensitivity routines without
      //       bumping the tenor, hence it may differ from both the tenor
      //       and the spot value.
      if (Tenors.Count > 0 && Tenors[0].MarketPv.AlmostEquals(Spot.Value))
      {
        return GetVal(0);
      }

      // Otherwise, Spot may change value independent of curve tenor/point,
      //   in most cases driven by simulation engine.
      return Spot.Value;
    }

    internal void SetSpotTenor(CurveTenor tenor)
    {
      var tenors = Tenors ?? (Tenors = new CurveTenorCollection());
      if (tenors.Count == 1 && ReferenceEquals(tenors[0], tenor))
      {
        return;
      }
      tenors.Clear();
      tenors.Add(tenor);
    }

    private string GetNameFromSpot()
    {
      if (!string.IsNullOrEmpty(Spot.Name)) return Spot.Name + "_Spot";
      return "spot";
    }

    [OnSerialized]
    private void OnSerialized(StreamingContext context)
    {
      // Ensure the single instance
      var sc = Calibrator as SpotCalibraor;
      if (sc != null && sc != SpotCalibraor.Instance)
        Calibrator = SpotCalibraor.Instance;
    }

    internal static void RegisterSerializer()
    {
      CustomSerializers.Register(new Serializer());
    }

    #region SpotCalibrator

    [Serializable]
    private class SpotCalibraor : Calibrator
    {
      public static readonly Calibrator Instance = new SpotCalibraor();

      private SpotCalibraor() : base(Dt.Empty)
      {
      }

      protected override void FitFrom(CalibratedCurve curve, int fromIdx)
      {
        var spot = (curve as SpotPriceCurve)?.Spot;
        if (spot == null) return;

        var date = curve.AsOf;
        var shift = curve.ShiftOverlay;
        if (shift != null)
        {
          shift.Clear();
          shift.Add(date, 1.0);
          var shiftValue = spot.Value/curve.Interpolate(date);
          shift.SetVal(0, shiftValue);
          return;
        }

        var value = spot.Value;
        if (curve.Count == 1)
        {
          if (!curve.GetVal(0).AlmostEquals(value))
          {
            curve.SetVal(0, value);
          }
          return;
        }
        curve.Clear();
        curve.Add(date, value);
      }
    }

    #endregion

    #region XML Serializer

    private class Serializer : CustomFieldMapSerializer
    {
      public Serializer() : base(new Dictionary<string, Type>
      {
        {nameof(Spot), typeof(ISpot)},
        {nameof(Name), typeof(string)},
      })
      {
      }

      public override bool CanHandle(Type type)
      {
        return type == typeof(SpotPriceCurve);
      }

      protected override object Construct(
        IReadOnlyDictionary<string, object> values)
      {
        if (values == null) return null;

        object value;
        if (!values.TryGetValue(nameof(Spot), out value))
        {
          return null;
        }
        var spot = (ISpot) value;

        string name = null;
        if (values.TryGetValue(nameof(Name), out value))
        {
          name = (string) value;
        }

        return new SpotPriceCurve(spot, name);
      }

      protected override IReadOnlyDictionary<string, object> GetFieldValues(
        object data)
      {
        var curve = data as SpotPriceCurve;
        if (curve?.Spot == null) return null;

        var values = new Dictionary<string, object> {{nameof(Spot), curve.Spot}};

        if (!string.IsNullOrEmpty(curve.Name))
        {
          values.Add(nameof(Name), curve.Name);
        }
        return values;
      }
    }

    #endregion
  }

  #endregion
}
