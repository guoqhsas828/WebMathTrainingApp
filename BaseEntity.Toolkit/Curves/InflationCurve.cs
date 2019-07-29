using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;
using BaseEntity.Toolkit.Numerics;


namespace BaseEntity.Toolkit.Curves
{
  #region InflationRealDiscountCurve

  /// <summary>
  /// Seasonally adjusted forward inflation factor (Forward Inflation/Spot Inflation)
  /// </summary>
  [Serializable]
  public sealed class InflationRealCurve : DiscountCurve
  {
    #region Constructors
    /// <summary>
    ///   Constructor given calibrator and interpolation
    /// </summary>
    /// <param name="calibrator">Calibrator</param>
    /// <param name="interp">Interpolation method</param>
    /// <param name="dc">Daycount for interpolation</param>
    /// <param name="freq">Compounding frequency for interpolation</param>
    public InflationRealCurve(Calibrator calibrator, Interp interp, DayCount dc, Frequency freq)
      : base(calibrator, interp, dc, freq)
    { }
    #endregion

    #region Methods
    /// <summary>
    ///  The effective date (included for inflation zero curves).
    /// </summary>
    /// <value>The spot calendar.</value>
    public override Dt GetCurveDate(Dt date)
    {
      return InflationUtils.PublicationDate(
          InflationUtils.InflationPeriod(RateResetUtil.ResetDate(date, null, IndexationLag), InflationIndex.PublicationFrequency,
                                           InflationIndex.PublicationLag, IndexationMethod).Last(),
          InflationIndex.PublicationFrequency, InflationIndex.PublicationLag);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Inflation index
    /// </summary>
    public InflationIndex InflationIndex { get; set; }

    /// <summary>
    /// Indexation Method
    /// </summary>
    public IndexationMethod IndexationMethod { get; set; }

    /// <summary>
    /// Indexation Lag
    /// </summary>
    public Tenor IndexationLag { get; set; }
    #endregion
  }

  #endregion

  #region InflationFactorCurve

  /// <summary>
  /// Seasonally adjusted forward inflation factor (Forward Inflation/Spot Inflation)
  /// </summary>
  [Serializable]
  public sealed class InflationFactorCurve : CalibratedCurve
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Interpolation defaults to flat continuously compounded forward rates.
    ///   i.e. Interpolation is Weighted/Const, Daycount is Actual365Fixed,
    ///   Compounding frequency is Continuous. Settlement defaults to </para>
    /// </remarks>
    ///
    /// <param name="asOf">As-of date</param>
    ///
    public InflationFactorCurve(Dt asOf)
      : base(asOf)
    {}

    /// <summary>
    ///  The effective date (included for inflation zero curves).
    /// </summary>
    /// <value>The spot calendar.</value>
    public override Dt GetCurveDate(Dt date)
    {
      return InflationUtils.PublicationDate(
          InflationUtils.InflationPeriod(RateResetUtil.ResetDate(date, null, IndexationLag), InflationIndex.PublicationFrequency,
                                           InflationIndex.PublicationLag, IndexationMethod).Last(),
          InflationIndex.PublicationFrequency, InflationIndex.PublicationLag);
    }

    /// <summary>
    /// Inflation index
    /// </summary>
    public InflationIndex InflationIndex { get; set; }

    /// <summary>
    /// Indexation Method
    /// </summary>
    public IndexationMethod IndexationMethod { get; set; }

    /// <summary>
    /// Indexation Lag
    /// </summary>
    public Tenor IndexationLag { get; set; }
  }

  #endregion

  #region Inflation Curve

  /// <summary>
  /// Inflation curve
  /// </summary>
  [Serializable]
  public class InflationCurve : ForwardPriceCurve
  {
    #region SpotInflation

    /// <summary>
    /// Spot inflation level
    /// </summary>
    [Serializable]
    private class SpotInflationPrice : SpotPrice
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="asOf">AsOf date</param>
      /// <param name="settleDays">Days to spot</param>
      /// <param name="calendar">Calendar</param>
      /// <param name="ccy">Currency</param>
      /// <param name="price">Spot price</param>
      public SpotInflationPrice(Dt asOf, int settleDays, Calendar calendar, Currency ccy, double price)
        : base(asOf, settleDays, calendar, ccy, price)
      {

        if (price <= 0)
          throw new ArgumentException("Base inflation must be strictly positive");
        Name = String.Format("SpotInflation.{0}", Ccy);
      }
    }

    #endregion

    #region InflationInterpolator

    [Serializable]
    private abstract class InflationInterpolator : BaseEntityObject, ICurveInterpolator
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="underlyingCurve"></param>
      protected InflationInterpolator(InflationCurve underlyingCurve)
      {
        UnderlyingCurve = underlyingCurve;
      }

      #endregion

      #region Methods

      /// <summary>
      /// Get component curves
      /// </summary>
      /// <returns></returns>
      public abstract IEnumerable<Curve> EnumerateComponentCurves();

      #endregion

      #region Properties

      /// <summary>
      /// Underlying curve
      /// </summary>
      protected InflationCurve UnderlyingCurve { get; private set; }

      /// <summary>
      /// Spot inflation
      /// </summary>

      protected SpotInflationPrice SpotInflationPrice
      {
        get { return (SpotInflationPrice)UnderlyingCurve.Spot; }
      }

      /// <summary>
      /// Seasonality
      /// </summary>

      protected Curve SeasonalityCurve
      {
        get { return UnderlyingCurve.SeasonalityCurve; }
      }

      #endregion

      #region ICurveInterpolator Members

      /// <summary>
      /// Initialize native curve
      /// </summary>
      /// <param name="curve"></param>
      public void Initialize(NativeCurve curve)
      {}

      /// <summary>
      /// Interpolate
      /// </summary>
      /// <param name="effectiveDt">Inflation effective date</param>
      /// <param name="publicationDt">Inflation publication date</param>
      /// <returns>Forward inflation</returns>
      public abstract double Interpolate(Dt effectiveDt, Dt publicationDt);

      /// <summary>
      ///Evaluate 
      /// </summary>
      /// <param name="curve">Underlying curve</param>
      /// <param name="t">time</param>
      /// <param name="index">Index</param>
      /// <returns>Forward inflation</returns>
      public double Evaluate(NativeCurve curve, double t, int index)
      {
        var dt = new Dt(UnderlyingCurve.AsOf, t/365.0);
        var effectiveDt = new Dt(1, dt.Month, dt.Year);
        var inflIndex = UnderlyingCurve.ReferenceIndex as InflationIndex;
        var publicationDt = (inflIndex == null)
                              ? effectiveDt
                              : InflationUtils.PublicationDate(effectiveDt, inflIndex.PublicationFrequency, inflIndex.PublicationLag);
        return Interpolate(effectiveDt, publicationDt);
      }

      #endregion
    }

    #endregion

    #region InflationForwardInterpolator

    /// <summary>
    /// Inflation forward as spot times ratio of real and nominal zero  
    /// </summary>
    [Serializable]
    private sealed class InflationForwardInterpolator : InflationInterpolator
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="inflationCurve">Inflation curve</param>
      public InflationForwardInterpolator(InflationCurve inflationCurve)
        : base(inflationCurve)
      {}

      #endregion

      #region Methods

      /// <summary>
      ///Evaluate 
      /// </summary>
      /// <param name="effectiveDt">Inflation effective date</param>
      /// <param name="publicationDt">Inflation publication date</param>
      /// <returns>Forward inflation</returns>
      public override double Interpolate(Dt effectiveDt, Dt publicationDt)
      {
        Dt asOf = SpotInflationPrice.Spot;
        if (publicationDt < asOf)
          return SpotInflationPrice.Price;
        double retVal = SpotInflationPrice.Value;
        var inflationFactor = UnderlyingCurve.TargetCurve;
        if (inflationFactor != null && inflationFactor.Count > 0)
          retVal *= inflationFactor.Interpolate(publicationDt) / inflationFactor.Interpolate(asOf);
        if (SeasonalityCurve != null && SeasonalityCurve.Count > 0)
          retVal *= SeasonalityCurve.Interpolate(effectiveDt);
        return retVal;
      }

      /// <summary>
      /// Enumerates the component curves.
      /// </summary>
      /// <returns>An IEnumerable of curves.</returns>
      public override IEnumerable<Curve> EnumerateComponentCurves()
      {
        if (UnderlyingCurve.TargetCurve != null)
          yield return UnderlyingCurve.TargetCurve;
        if (SeasonalityCurve != null)
          yield return SeasonalityCurve;
      }

      #endregion
    }

    #endregion

    #region InflationRealYieldInterpolator

    /// <summary>
    /// Inflation forward as spot times ratio of real and nominal zero  
    /// </summary>
    [Serializable]
    private sealed class InflationRealYieldInterpolator : InflationInterpolator
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="inflationCurve">InflationCurve</param>
      public InflationRealYieldInterpolator(InflationCurve inflationCurve)
        : base(inflationCurve)
      {}

      #endregion

      #region ICurveInterpolator Members

      /// <summary>
      ///Evaluate 
      /// </summary>
      /// <param name="effectiveDt">Inflation effective date</param>
      /// <param name="publicationDt">Inflation publication date</param>
      /// <returns>Forward inflation</returns>
      public override double Interpolate(Dt effectiveDt, Dt publicationDt)
      {
        Dt asOf = SpotInflationPrice.Spot;
        if (publicationDt <= asOf)
          return SpotInflationPrice.Price;
        double nominalZero = UnderlyingCurve.NominalYieldCurve.Interpolate(publicationDt) / UnderlyingCurve.NominalYieldCurve.Interpolate(asOf);
        double realZero = UnderlyingCurve.RealYieldCurve.Interpolate(publicationDt) / UnderlyingCurve.RealYieldCurve.Interpolate(asOf);
        double inflationForward = SpotInflationPrice.Price * realZero / nominalZero;
        if (SeasonalityCurve != null && SeasonalityCurve.Count > 0)
          inflationForward *= SeasonalityCurve.Interpolate(effectiveDt);
        return inflationForward;
      }

      /// <summary>
      /// Enumerates the component curves.
      /// </summary>
      /// <returns>An IEnumerable of curves.</returns>
      public override IEnumerable<Curve> EnumerateComponentCurves()
      {
        if (UnderlyingCurve.NominalYieldCurve != null)
          yield return UnderlyingCurve.NominalYieldCurve;
        if (UnderlyingCurve.RealYieldCurve != null)
          yield return UnderlyingCurve.RealYieldCurve;
        if (UnderlyingCurve.InflationFactorCurve != null)
          yield return UnderlyingCurve.InflationFactorCurve;
        if (SeasonalityCurve != null)
          yield return SeasonalityCurve;
      }

      #endregion
    }

    #endregion

    #region InflationBondQuoteHandler

    /// <summary>
    ///   A general handler of tenor quotes.
    ///   It handles the quotes in the same way as we did before.
    /// </summary>
    [Serializable]
    private sealed class InflationBondQuoteHandler : ICurveTenorQuoteHandler
    {
      /// <summary>
      /// Creates a new object that is a copy of the current instance.
      /// </summary>
      /// <returns>
      /// A new object that is a copy of this instance.
      /// </returns>
      public object Clone()
      {
        return this; // this is an instance with no data member.
      }

      /// <summary>
      /// Bumps the tenor quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="bumpSize">Size of the bump.</param>
      /// <param name="bumpFlags">The bump flags.</param>
      /// <returns>Actual bump size.</returns>
      public double BumpQuote(CurveTenor tenor,
                              double bumpSize, BumpFlags bumpFlags)
      {
        bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
        bool up = (bumpFlags & BumpFlags.BumpDown) == 0;
        double bumpAmt = (up) ? bumpSize : -bumpSize;
        if (bumpRelative && bumpAmt < 0)
          bumpAmt = bumpAmt / (1 - bumpAmt);
        if (bumpRelative)
          bumpAmt = bumpAmt * tenor.MarketPv;
        else
          bumpAmt /= 10000.0;
        if (tenor.MarketPv - bumpAmt <= 0)
          bumpAmt = tenor.MarketPv / 2;
        tenor.MarketPv -= bumpAmt;
        return ((up) ? bumpAmt : -bumpAmt) * 10000;
      }

      /// <summary>
      /// Creates a pricer for calibration.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="calibrator">The calibrator.</param>
      /// <returns>Pricer.</returns>
      public IPricer CreatePricer(CurveTenor tenor, Curve curve, Calibrator calibrator)
      {
        var ccurve = curve as InflationCurve;
        if (ccurve == null)
        {
          throw new ArgumentException("InflationBond tenor quote handler works only with InflationCurves");
        }
        return calibrator.GetPricer(ccurve, tenor.Product);
      }

      /// <summary>
      /// Gets the current tenor quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <returns>The current quote.</returns>
      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        return new CurveTenor.Quote(QuotingConvention.FlatPrice, tenor.MarketPv);
      }

      /// <summary>
      /// Gets the current tenor quote in the specified type.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="targetQuoteType">Type of the target quote.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="calibrator">The calibrator.</param>
      /// <param name="recalculate">if set to <c>true</c>, recalculate the quote values.</param>
      /// <returns>The quote value.</returns>
      /// <exception cref="QuoteTypeNotSupportedException">
      ///  Conversion to the target quote type is not supported by this handler.
      /// </exception>
      public double GetQuote(CurveTenor tenor,
                             QuotingConvention targetQuoteType,
                             Curve curve, Calibrator calibrator, bool recalculate)
      {
        var product = tenor.Product;

        if (targetQuoteType != QuotingConvention.FlatPrice)
          throw QuoteTypeNotSupported(targetQuoteType, product);
        return tenor.MarketPv;
      }

      /// <summary>
      /// Sets the current tenor quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="quoteType">Type of the quote.</param>
      /// <param name="quoteValue">The quote value.</param>
      /// <exception cref="QuoteTypeNotSupportedException">
      ///  The target quote type is not supported by this handler.
      /// </exception>
      public void SetQuote(CurveTenor tenor,
                           QuotingConvention quoteType, double quoteValue)
      {
        if (quoteType != QuotingConvention.FlatPrice)
          throw QuoteTypeNotSupported(quoteType, tenor.Product);
        tenor.MarketPv = quoteValue;
      }
    }

    #endregion

    #region Constructors


    /// <summary>
    /// Constructor of inflation 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="spotInflation">Spot inflation</param>
    /// <param name="nominalYieldCurve">Nominal zero curve</param>
    /// <param name="realYieldCurve">Real zero curve</param>
    /// <param name="seasonalityCurve">Inflation seasonality factor</param>
    public InflationCurve(Dt asOf, double spotInflation, DiscountCurve nominalYieldCurve, DiscountCurve realYieldCurve,
                          Curve seasonalityCurve)
      : base(
        new SpotInflationPrice(asOf, 0, nominalYieldCurve.SpotCalendar, nominalYieldCurve.Ccy, spotInflation))
    {
      RealYieldCurve = realYieldCurve;
      SeasonalityCurve = seasonalityCurve;
      DiscountCurve = nominalYieldCurve;
      var interpolator = new InflationRealYieldInterpolator(this);
      Initialize(asOf, interpolator);
      Calibrator = realYieldCurve.Calibrator;
      Tenors = realYieldCurve.Tenors;
      AddSpot();
    }

    /// <summary>
    /// Constructor of inflation 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="spotInflation">Spot inflation</param>
    /// <param name="inflationFactor">Forward inflation/Spot inflation</param>
    /// <param name="seasonalityCurve">Inflation seasonality factor</param>
    public InflationCurve(Dt asOf, double spotInflation, InflationFactorCurve inflationFactor, Curve seasonalityCurve)
      : base(new SpotInflationPrice(asOf, 0, Calendar.None, Currency.None, spotInflation))
    {
      InflationFactorCurve = inflationFactor;
      SeasonalityCurve = seasonalityCurve;
      var interpolator = new InflationForwardInterpolator(this);
      Initialize(asOf, interpolator);
      Calibrator = inflationFactor.Calibrator;
      Tenors = inflationFactor.Tenors;
      AddSpot();
    }


    #endregion Constructors

    #region Methods

    /// <summary>
    /// Perturb seasonality adjustment for the given month
    /// </summary>
    /// <param name="month">Month</param>
    /// <param name="bump">Bump size</param>
    /// <remarks>Bump is additive/multiplicative depending on whether the adjustment is additive/multiplicative</remarks>
    public void PerturbSeasonalityAdjustment(Month month, double bump)
    {
      var seasonality = SeasonalityCurve;
      if (seasonality == null)
        return;
      for (int i = 0; i < seasonality.Count; ++i)
      {
        if (seasonality.GetDt(i).Month != (int)month)
          continue;
        double v = seasonality.GetVal(i);
        v *= 1 + bump / (Math.Pow(1 + bump, 1.0 / 12.0));
        seasonality.SetVal(i, v);
      }
    }


    /// <summary>
    /// Add a TipsBond to the array of products used for calibration
    /// </summary>
    /// <param name="bond">bond object</param>
    /// <param name="marketQuote"></param>
    /// <param name="quotingConvention">Bond quoting convention</param>
    /// <param name="floorP"></param>
    /// <param name="refCurveForAsw"></param>
    public void AddInflationBond(InflationBond bond, double marketQuote, QuotingConvention quotingConvention,
                                 double floorP, DiscountCurve refCurveForAsw)
    {
      var bs = Calibrator as InflationCurveFitCalibrator;
      if (bs == null)
        throw new NullReferenceException("Calibrator cannot be null");
      var inflationFactor = new InflationFactorCurve(AsOf);
      inflationFactor.Add(AsOf, 1.0);
      var pricer = bs.GetPricer(new InflationCurve(AsOf, SpotInflation, inflationFactor, null), bond);
      var pxer = (InflationBondPricer)pricer;
      double floorPriceToUse = 0;
      if (bond.Floor.HasValue &&
          (quotingConvention == QuotingConvention.ASW_Mkt || quotingConvention == QuotingConvention.ASW_Par))
        floorPriceToUse = Double.IsNaN(floorP) ? pxer.ImpliedFloorPrice() : floorP / 100;
      pxer.SetMarketQuote(marketQuote, quotingConvention, refCurveForAsw, floorPriceToUse);
      double pv = pxer.MarketPv(); //market calcs
      pv -= floorPriceToUse;
      if (String.IsNullOrEmpty(bond.Description))
      {
        string indexName = bs.InflationIndex.IndexName;
        bond.Description = indexName + "." + "InflationIndexedBond" + "_" + bond.Maturity;
      }
      TargetCurve.Tenors.Add(new CurveTenor(bond.Description, bond, pv, bond.Coupon, 0.0, 1.0, new InflationBondQuoteHandler()));
    }


    /// <summary>
    /// Add Inflation bond to curve tenors
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="coupon">Coupon</param>
    /// <param name="bondType">Bond type</param>
    /// <param name="dayCount">Day count</param>
    /// <param name="freq">Frequency</param>
    /// <param name="roll">BDConvention</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="baseInflation">Base inflation</param>
    /// <param name="resetLag">Indexation lag</param>
    /// <param name="indexationMethod">Indexation method</param>
    /// <param name="flooredNotional">Is notional floored</param>
    /// <param name="marketQuote"></param>
    /// <param name="quotingConvention">Bond quoting convention</param>
    /// <param name="floorPrice">Override for floor price</param>
    /// <param name="projectionType">Bond quoting convention</param>
    /// <param name="spreadType">Override for floor price</param>
    /// <param name="referenceCurveForAssetSwap"></param>
    public void AddInflationBond(Dt effective, Dt maturity, double coupon, BondType bondType, DayCount dayCount, Frequency freq, BDConvention roll,
                                 Calendar calendar,
                                 double baseInflation, Tenor resetLag, IndexationMethod indexationMethod, bool flooredNotional, double marketQuote,
                                 QuotingConvention quotingConvention,
                                 ProjectionType projectionType, SpreadType spreadType, double floorPrice, DiscountCurve referenceCurveForAssetSwap)
    {
      var bs = Calibrator as InflationCurveFitCalibrator;
      if (bs == null)
        throw new NullReferenceException("Calibrator cannot be null");
      var inflationIndex = bs.InflationIndex;
      var issue = maturity;
      while (issue > effective)
        issue = Dt.Add(issue, freq, -1, false);
      var bond = new InflationBond(issue, maturity, inflationIndex.Currency, bondType, coupon, dayCount, CycleRule.None, freq, roll, calendar,
                                   inflationIndex, baseInflation, resetLag)
                 {
                   ResetLag = resetLag,
                   IndexationMethod = indexationMethod,
                   FlooredNotional = flooredNotional,
                   ProjectionType = projectionType,
                   SpreadType = spreadType,
                   Description = inflationIndex.IndexName + "." + "InflationIndexedBond" + "_" + maturity
                 };
      bond.Validate();
      var psp = (IScheduleParams)bond.Schedule;
      bond.CycleRule = psp.CycleRule;
      AddInflationBond(bond, marketQuote, quotingConvention, floorPrice, referenceCurveForAssetSwap);
    }

    /// <summary>
    /// Add zero coupon swap
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="compoundingFrequency">Compounding frequency for fixed coupon</param>
    /// <param name="dayCount">Fixed leg daycount</param>
    /// <param name="roll">Fixed leg roll convention</param>
    /// <param name="calendar">Fixed leg calendar</param>
    /// <param name="resetLag">Indexation lag</param>
    /// <param name="marketQuote">Fixed coupon</param>
    /// <param name="indexationMethod">Inflation indexation method</param>
    /// <param name="adjustPeriod">Adjust period</param>
    /// <param name="adjustLast">Adjust last accrual date</param>
    public void AddZeroCouponSwap(Dt effective, Dt maturity, Frequency compoundingFrequency, DayCount dayCount, BDConvention roll, Calendar calendar,
                                  Tenor resetLag, double marketQuote, IndexationMethod indexationMethod, bool adjustPeriod, bool adjustLast)
    {
      var bs = Calibrator as InflationCurveFitCalibrator;
      if (bs == null)
        throw new NullReferenceException("Calibrator cannot be null");
      var inflationIndex = bs.InflationIndex;
      var payerLeg = new InflationSwapLeg(effective, maturity, inflationIndex.Currency, marketQuote, dayCount, Frequency.None, roll, calendar, !adjustPeriod)
                     {
                       IsZeroCoupon = true,
                       CompoundingFrequency = compoundingFrequency,
                       IndexationMethod = indexationMethod,
                       AdjustLast = adjustLast
                     };
      payerLeg.Validate();
      var psp = (IScheduleParams)payerLeg.Schedule;
      payerLeg.CycleRule = psp.CycleRule;
      var receiverLeg = new InflationSwapLeg(effective, maturity, Frequency.None, 0.0, inflationIndex)
                        {
                          InArrears = true,
                          IsZeroCoupon = true,
                          CycleRule = psp.CycleRule,
                          ResetLag = resetLag,
                          ProjectionType = ProjectionType.InflationRate,
                          IndexationMethod = indexationMethod,
                          AccrueOnCycle = !adjustPeriod,
                          AdjustLast = adjustLast
                        };
      receiverLeg.Validate();
      AddInflationSwap(new Swap(receiverLeg, payerLeg), marketQuote);
    }

    /// <summary>
    /// Add YoY swap
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="frequency">Fixed leg frequency</param>
    /// <param name="dayCount">Fixed leg daycount</param>
    /// <param name="roll">Fixed leg roll convention</param>
    /// <param name="calendar">Fixed leg calendar</param>
    /// <param name="resetLag">Indexation lag</param>
    /// <param name="floatFrequency">Fixed leg frequency</param>
    /// <param name="indexTenor">Fixed leg frequency</param>
    /// <param name="marketQuote">Fixed coupon</param>
    /// <param name="indexationMethod">Inflation indexation method</param>
    /// <param name="adjustPeriod">Adjust period</param>
    /// <param name="adjustLast">Adjust last accrual date</param>
    public void AddYoYSwap(Dt effective, Dt maturity, Frequency frequency, DayCount dayCount, BDConvention roll, Calendar calendar, Tenor resetLag,
                           Frequency floatFrequency, Tenor indexTenor, double marketQuote, IndexationMethod indexationMethod, bool adjustPeriod, bool adjustLast)
    {
      var bs = Calibrator as InflationCurveFitCalibrator;
      if (bs == null)
        throw new NullReferenceException("Calibrator cannot be null");
      var inflationIndex = bs.InflationIndex;
      var payerLeg = new SwapLeg(effective, maturity, inflationIndex.Currency, marketQuote, dayCount, frequency, roll, calendar, !adjustPeriod)
      {
        AdjustLast = adjustLast
      };
      payerLeg.Validate();
      var psp = (IScheduleParams)payerLeg.Schedule;
      payerLeg.CycleRule = psp.CycleRule;
      var receiverLeg = new InflationSwapLeg(effective, maturity, (floatFrequency == Frequency.None) ? Frequency.Annual : floatFrequency, 0.0, inflationIndex)
                        {
                          InArrears = true,
                          CycleRule = psp.CycleRule,
                          ResetLag = resetLag,
                          ProjectionType = ProjectionType.InflationRate,
                          IndexTenor = indexTenor.IsEmpty ? new Tenor(1, TimeUnit.Years) : indexTenor,
                          IndexationMethod = indexationMethod,
                          AccrueOnCycle = !adjustPeriod,
                          AdjustLast = adjustLast
                        };
      receiverLeg.Validate();
      AddInflationSwap(new Swap(receiverLeg, payerLeg), marketQuote);
    }

    /// <summary>
    /// Add an inflation swap to the array of products used for calibration
    /// </summary>
    /// <param name="swap">Fixed leg of a swap product, made up of two legs. For calibration purposes one should use vanilla swaps with floating vs fixed leg </param>
    ///<param name="marketQuote">Quoted spread</param>
    public void AddInflationSwap(Swap swap, double marketQuote)
    {
      var bs = Calibrator as InflationCurveFitCalibrator;
      if (bs == null)
        throw new NullReferenceException("Calibrator cannot be null");
      if (swap.ReceiverLeg.Floating && swap.PayerLeg.Floating)
        throw new ArgumentException("Calibration does not support floating vs floating legs");
      var fixL = (swap.IsPayerFixed) ? swap.PayerLeg : swap.ReceiverLeg;
      var flL = (swap.IsPayerFixed) ? swap.ReceiverLeg : swap.PayerLeg;
      fixL.Coupon = marketQuote;
      if (String.IsNullOrEmpty(swap.Description))
      {
        string indexName = bs.InflationIndex.IndexName;
        swap.Description = indexName + "." + "InflationSwap" + "_" + swap.Maturity;
      }
      TargetCurve.Add(new Swap(flL, fixL) {Description = swap.Description}, 0);
    }

    /// <summary>
    /// Enumerate component curves
    /// </summary>
    /// <returns></returns>
    public override IEnumerable<CalibratedCurve> EnumerateComponentCurves()
    {
      var curves = base.EnumerateComponentCurves();
      var iryInterpolator = CustomInterpolator as InflationRealYieldInterpolator;
      if (iryInterpolator != null)
        return curves.Concat(iryInterpolator.EnumerateComponentCurves().OfType<CalibratedCurve>());
      var ifInterpolator = CustomInterpolator as InflationForwardInterpolator;
      if (ifInterpolator != null)
        return curves.Concat(ifInterpolator.EnumerateComponentCurves().OfType<CalibratedCurve>());
      return curves;
    }

    private static Exception QuoteTypeNotSupported(
      QuotingConvention quoteType, IProduct product)
    {
      return new QuoteTypeNotSupportedException(String.Format(
        "Quote type {0} not supported in general handler for {1}",
        quoteType, product.GetType().Name));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Real yield curve
    /// </summary>
    public DiscountCurve RealYieldCurve { get; private set; }

    /// <summary>
    /// Carry rate
    /// </summary>
    /// <param name="spot"></param>
    /// <param name="delivery"></param>
    /// <returns></returns>
    public override double CarryRateAdjustment(Dt spot, Dt delivery)
    {
      return 0.0;
    }

    /// <summary>
    /// Cashflows associated to holding the spot asset
    /// </summary>
    public override IEnumerable<Tuple<Dt, DividendSchedule.DividendType, double>> CarryCashflow
    {
      get { return null; }
    }

    /// <summary>
    /// Nominal yield curve
    /// </summary>
    public DiscountCurve NominalYieldCurve
    {
      get { return DiscountCurve; }
    }

    /// <summary>
    /// Forward inflation normalized by spot 
    /// </summary>
    private InflationFactorCurve InflationFactorCurve { get; set; }

    /// <summary>
    /// Seasonality
    /// </summary>
    public Curve SeasonalityCurve { get; private set; }


    /// <summary>
    /// If Interp is InflationRealYieldInterpolator, calibrate RealZeroCurve
    /// </summary>
    public override CalibratedCurve TargetCurve
    {
      get
      {
        if (RealYieldCurve != null)
          return RealYieldCurve;
        if (InflationFactorCurve != null)
          return InflationFactorCurve;
        return null;
      }
      protected set
      {
        var factorCurve = value as InflationFactorCurve;
        if (factorCurve != null) InflationFactorCurve = factorCurve;
        var realYieldCurve = value as DiscountCurve;
        if (realYieldCurve != null) RealYieldCurve = realYieldCurve;
      }
    }

    /// <summary>
    /// Initial (as of As-Of date) curve level in case this is different from one
    /// </summary>
    public double SpotInflation
    {
      get { return SpotPrice; }
    }

    /// <summary>
    /// Underlying index
    /// </summary>
    public InflationIndex InflationIndex
    {
      get
      {
        var bs = Calibrator as InflationCurveFitCalibrator;
        if (bs == null)
          throw new NullReferenceException("Calibrator cannot be null");
        return bs.InflationIndex;
      }
    }

    #endregion
  }

  #endregion
}