//
// 
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  #region StockCurve

  /// <summary>
  ///  Stock forward curve
  /// </summary>
  /// <remarks>
  ///  <para>
  ///  Let <m>t_0</m> be the spot date and <m>S_0</m> the spot price,
  ///  the stock curve models the forward stock price <m>S_t</m>
  ///  at time <m>t &gt; t_0</m> as<math>
  ///   S_t = \frac{S_0\,Q(t_0, t) - A(t_0, t)}{D(t_0, t)}
  ///  </math>where
  ///    <m>D(t_0, t)</m> is the discount factor,
  ///    <m>Q(t_0, t)</m> is the dividend yield factor, and
  ///    <m>A(t_0, t)</m> is the adjustment from the declared discrete dividend payments.
  ///  </para>
  ///
  ///  <para>
  ///  Let <m>r_t</m> be the potentially time-varying short rate,
  ///   <m>q_t</m> be the potentially time-varying dividend yield rate,
  ///  then we have<math>
  ///    D(t_0, t) = \exp\!\left(-\int_{t_0}^{t} r_s\,d s\right)
  ///   \qquad
  ///    Q(t_0, t) = \exp\!\left(-\int_{t_0}^{t} q_s\,d s\right)
  ///  </math>
  /// </para>
  /// 
  ///  <para>
  ///  Let <m>(d_i, \tau_i)</m> be the dividend schedule, i.e., the pairs of
  ///  the declared discrete dividend payments per share and
  ///  the corresponding payment dates.
  ///  Then <m>A(t_0, t)</m> is given by<math>
  ///    A(t_0, t) = \sum_{t_0 \lt \tau_i \leq t} d_i\,D(t_0, \tau_i)
  ///  </math>
  ///  </para>
  ///
  ///  <para>
  ///   In calibration, the yield factor curve <m>Q(t_0, t)</m> is considered
  ///   to represent the hidden costs/benefits of holding the stock.  It is
  ///   calibrated from the stock futures prices and other market observables,
  ///   with the given discount curve and the declared dividend schedule.
  ///  </para>
  /// </remarks>
  [Serializable]
  public class StockCurve : ForwardPriceCurve
  {
    #region SpotStockPrice

    /// <summary>
    /// Spot inflation level
    /// </summary>
    [Serializable]
    private class SpotStockPrice : SpotPrice
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="asOf">AsOf date</param>
      /// <param name="ccy">Currency</param>
      /// <param name="price">Spot price</param>
      public SpotStockPrice(Dt asOf, Currency ccy, double price)
        : base(asOf, 0, Calendar.None, ccy, price)
      { }
    }

    #endregion

    #region StockDiscountCalibrator

    /// <summary>
    /// Dummy calibrator to hold zero curve
    /// </summary>
    [Serializable]
    private class StockSpotCalibrator : ForwardPriceCalibrator
    {
      public StockSpotCalibrator(Dt asOf, DiscountCurve discountCurve)
        : base(asOf, asOf, discountCurve)
      { }

      protected override void AddData(CurveTenor tenor, CashflowCalibrator calibrator,
                                      CalibratedCurve targetCurve)
      { }

      protected override void SetCurveDates(CalibratedCurve targetCurve)
      { }

      protected override IPricer GetSpecializedPricer(ForwardPriceCurve curve, IProduct product)
      {
        return null;
      }
    }

    #endregion

    #region ImpliedDividendYieldCurve

    [Serializable]
    private class ImpliedDividendYieldCurve : CalibratedCurve
    {
      public ImpliedDividendYieldCurve(Dt asOf)
        : base(asOf)
      { }

      public ImpliedDividendYieldCurve(Dt asOf, double dividendYield)
        : base(asOf, dividendYield)
      { }

      public double DividendYield(Dt spot, Dt deliveryDate, DayCount dayCount)
      {
        return -Math.Log(Interpolate(spot, deliveryDate)) / Dt.Years(spot, deliveryDate, dayCount);
      }
    }

    #endregion

    #region StockForwardInterpolator

    [Serializable]
    private class StockForwardInterpolator : BaseEntityObject, ICurveInterpolator
    {
      #region Properties

      /// <summary>
      /// Underlying curve 
      /// </summary>
      private StockCurve UnderlyingCurve { get; set; }

      #endregion

      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      public StockForwardInterpolator(StockCurve underlyingCurve)
      {
        UnderlyingCurve = underlyingCurve;
      }


      #endregion

      #region Methods

      public void Initialize(Native.Curve curve)
      { }

      public double Evaluate(Native.Curve curve, double t, int index)
      {
        Dt futureDt = new Dt(UnderlyingCurve.AsOf, t / 365.0);

        //parameter definitions
        var spotDate = UnderlyingCurve.Spot.Spot;
        double spotPrice = UnderlyingCurve.SpotPrice;
        var discountCurve = UnderlyingCurve.DiscountCurve;
        var yieldCurve = UnderlyingCurve.DividendYieldCurve;
        var dividends = UnderlyingCurve.Stock.DeclaredDividends;
        //------------------------------------------------
        double B;
        double Q;
        Dt prevDivPayDate = spotDate;

        foreach (var dividend in dividends)
        {
          if (spotDate < dividend.PayDate && dividend.Item1 <= futureDt)
          {
            B = discountCurve.DiscountFactor(prevDivPayDate, dividend.PayDate);
            Q = (yieldCurve == null) ? 1.0 : yieldCurve.Interpolate(prevDivPayDate, dividend.PayDate);
            prevDivPayDate = dividend.PayDate;
            //update for fixed dividend segment
            if (dividend.Type == DividendSchedule.DividendType.Fixed)
            {
              spotPrice = spotPrice * Q / B - dividend.Amount;
            }
            else
            {
              //Proportional dividend segment
              spotPrice = spotPrice * Q / (B * (1 + dividend.Amount));
            }
          }
          if (dividend.PayDate > futureDt)
          {
            break;
          }
        }
        //final discounting from the latest dividend date to the date of interest
        B = discountCurve.DiscountFactor(prevDivPayDate, futureDt);
        Q = (yieldCurve == null) ? 1.0 : yieldCurve.Interpolate(prevDivPayDate, futureDt);
        spotPrice = spotPrice * Q / B;

        return spotPrice;

      }
      #endregion
    }

    #endregion

    #region EquityForwardQuoteHandler
    [Serializable]
    private class EquityForwardQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
    {
      #region ICurveTenorQuoteHandler Members

      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        return new CurveTenor.Quote(QuotingConvention.ForwardFlatPrice, ((StockForward)tenor.Product).DeliveryPrice);
      }

      public double GetQuote(CurveTenor tenor, QuotingConvention targetQuoteType, Curve curve, Calibrator calibrator, bool recalculate)
      {
        if (targetQuoteType != QuotingConvention.ForwardFlatPrice)
        {
          throw new QuoteConversionNotSupportedException(
            targetQuoteType, QuotingConvention.ForwardFlatPrice);
        }
        return ((StockForward)tenor.Product).DeliveryPrice;
      }

      public void SetQuote(CurveTenor tenor, QuotingConvention quoteType, double quoteValue)
      {
        if (quoteType != QuotingConvention.ForwardFlatPrice)
          throw new QuoteConversionNotSupportedException(QuotingConvention.ForwardFlatPrice, quoteType);
        var stockForward = tenor.Product as StockForward;
        if (stockForward != null)
          stockForward.DeliveryPrice = quoteValue;
      }

      public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags bumpFlags)
      {
        return tenor.BumpRawQuote(bumpSize, bumpFlags,
          (t, bumpedQuote) => ((StockForward)t.Product).DeliveryPrice = bumpedQuote);
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

    #region EquityFutureQuoteHandler
    [Serializable]
    private class EquityFutureQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
    {
      #region ICurveTenorQuoteHandler Members

      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        return new CurveTenor.Quote(QuotingConvention.ForwardFlatPrice, tenor.MarketPv);
      }

      public double GetQuote(CurveTenor tenor, QuotingConvention targetQuoteType, Curve curve, Calibrator calibrator, bool recalculate)
      {
        if (targetQuoteType != QuotingConvention.ForwardFlatPrice)
        {
          throw new QuoteConversionNotSupportedException(
            targetQuoteType, QuotingConvention.ForwardFlatPrice);
        }
        return tenor.MarketPv;
      }

      public void SetQuote(CurveTenor tenor, QuotingConvention quoteType, double quoteValue)
      {
        if (quoteType != QuotingConvention.ForwardFlatPrice)
          throw new QuoteConversionNotSupportedException(QuotingConvention.ForwardFlatPrice, quoteType);
        tenor.MarketPv = quoteValue;
      }

      public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags bumpFlags)
      {
        return tenor.BumpFuturesPriceQuote(bumpSize, bumpFlags, (t, bumpedQuote) => t.MarketPv = bumpedQuote);
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

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="futurePrice">current stock futures price</param>
    public StockCurve(Dt asOf, double futurePrice)
      : this(new SpotStockPrice(asOf, Currency.None, futurePrice), new DiscountCurve(asOf, 0.0), null, null)
    { }


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="stockPrice">Spot price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="continuousDivYield">Dividend stream</param>
    /// <param name="stock">The stock</param>
    public StockCurve(Dt asOf, double stockPrice, DiscountCurve discountCurve,
      double continuousDivYield, Stock stock)
      : this(new SpotStockPrice(asOf, discountCurve.Ccy, stockPrice), discountCurve,
          (continuousDivYield > 0.0) ?
          new ImpliedDividendYieldCurve(asOf, continuousDivYield) : null, stock)
    { }


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="spot">Spot price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="continuousDivYield">Dividend stream</param>
    /// <param name="stock">The stock</param>
    public StockCurve(Dt asOf, ISpot spot, DiscountCurve discountCurve,
      double continuousDivYield, Stock stock)
      : this(spot, discountCurve,
          (continuousDivYield > 0.0) ?
          new ImpliedDividendYieldCurve(asOf, continuousDivYield) : null, stock)
    { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="stockSpot">Spot stock price data</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="dividendYieldCurve">Continuosly paid dividends</param>
    /// <param name="stock">The stock</param>
    /// <param name="fpCalibrator">Forward price calibrator</param>
    private StockCurve(ISpot stockSpot, DiscountCurve discountCurve,
      ImpliedDividendYieldCurve dividendYieldCurve, Stock stock,
      ForwardPriceCalibrator fpCalibrator = null)
      : base(stockSpot)
    {
      DiscountCurve = discountCurve;
      DividendYieldCurve = dividendYieldCurve;
      var dividends = stock?.DeclaredDividends;

      if (dividends != null)
      {
        Dividends = new DividendSchedule(stockSpot.Spot,
          dividends.Where(d => d.ExDivDate > AsOf).Select(d => d.Type == DividendSchedule.DividendType.Proportional
            ? new Tuple<Dt, DividendSchedule.DividendType, double>(d.PayDate, d.Type, d.Amount / (1.0 + d.Amount))
            : new Tuple<Dt, DividendSchedule.DividendType, double>(d.PayDate, d.Type, d.Amount)).ToList());
      }
      else
        Dividends = new DividendSchedule(stockSpot.Spot);

      Stock = stock ?? new Stock();
      Calibrator = fpCalibrator ?? new StockSpotCalibrator(stockSpot.Spot, discountCurve);
      Initialize(AsOf, new StockForwardInterpolator(this));
      AddSpot();
    }

    #endregion

    #region Static Constructors

    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="tradeDt">Trade date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="spotPrice">Spot stock price</param>
    /// <param name="ticker">Ticker</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="fitSettings">Calibrator settings</param>
    /// <param name="tenors">Calibration tenors</param>
    /// <param name="instrumentTypes">Instrument types</param>
    /// <param name="maturities">(unrolled)Maturities</param>
    /// <param name="marketQuotes">Market quotes</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="stock">The underlying stock </param>
    /// <returns>Calibrated stock forward curve</returns>
    public static StockCurve FitStockForwardCurve(
      Dt tradeDt,
      Dt settle,
      CalibratorSettings fitSettings,
      string ticker,
      double spotPrice,
      BDConvention roll,
      Calendar calendar,
      Dt[] maturities,
      string[] tenors,
      InstrumentType[] instrumentTypes,
      double[] marketQuotes,
      DiscountCurve discountCurve,
      Stock stock
      )
    {
      if (tenors.Length != marketQuotes.Length)
        throw new ArgumentException("Size of tenors must match number of quotes");
      if (instrumentTypes.Length != marketQuotes.Length)
        throw new ArgumentException("Size of instrumentTypes must match number of quotes");
      if (maturities.Length != marketQuotes.Length)
        throw new ArgumentException("Size of maturities must match number of quotes");
      if (fitSettings.CurveAsOf.IsEmpty())
        fitSettings.CurveAsOf = settle;
      var ccy = discountCurve.Ccy;
      var calibrator = new StockCurveFitCalibrator(tradeDt, settle, discountCurve, fitSettings);
      var dividendYieldCurve = new ImpliedDividendYieldCurve(settle)
      { Name = ticker + "DividendYieldCurve", Interp = fitSettings.GetInterp() };

      var retVal = new StockCurve(new SpotStockPrice(settle, ccy, spotPrice)
      { Name = String.Format("{0}.SpotStockPrice.{1}", ticker, ccy) }, discountCurve,
        dividendYieldCurve, stock, calibrator)
      { Name = ticker + "_Curve" };
      for (int i = 0; i < marketQuotes.Length; ++i)
      {
        if (marketQuotes[i].ApproximatelyEqualsTo(0.0))
          continue;
        var type = instrumentTypes[i];
        if (maturities[i] < tradeDt)
          continue;
        switch (type)
        {
          case InstrumentType.Forward:
            retVal.AddForward(ticker + ".Forward_" + tenors[i], maturities[i], roll, calendar, marketQuotes[i]);
            break;
          case InstrumentType.FUT:
            retVal.AddFuture(ticker + "Future_" + tenors[i], maturities[i], roll, calendar, marketQuotes[i]);
            break;
          default:
            throw new ArgumentException(String.Format("{0} is not a supported calibration product", type));
        }
      }
      retVal.Fit();
      return retVal;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Curve to be calibrated
    /// </summary>
    public override CalibratedCurve TargetCurve
    {
      get
      {
        return DividendYieldCurve;
      }
      protected set
      {
        var divCurve = value as ImpliedDividendYieldCurve;
        if (divCurve != null) DividendYieldCurve = divCurve;
      }
    }

    public DividendSchedule Dividends { get; }

    /// <summary>
    /// Stock product
    /// </summary>
    public Stock Stock { get; set; }

    /// <summary>
    /// Dividend induced carry basis
    /// </summary>
    private ImpliedDividendYieldCurve DividendYieldCurve { get; set; }

    /// <summary>
    /// Implied carry basis curve
    /// </summary>
    public CalibratedCurve ImpliedYieldCurve => DividendYieldCurve;

    /// <summary>
    /// Cashflows associated to holding the spot asset
    /// </summary>
    public override IEnumerable<Tuple<Dt, DividendSchedule.DividendType, double>> CarryCashflow
    {
      get { return Dividends; }
    }

    /// <summary>
    /// Spot stock level
    /// </summary>
    public double StockPrice => SpotPrice;

    #endregion

    #region Methods

    /// <summary>
    /// Add equity forward contract
    /// </summary>
    /// <param name="tenorName">Tenor description</param>
    /// <param name="deliveryDate">Maturity date</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="marketQuote">Delivery price</param>
    public void AddForward(string tenorName, Dt deliveryDate, BDConvention roll,
      Calendar calendar, double marketQuote)
    {
      var bs = Calibrator as StockCurveFitCalibrator;
      if (bs == null)
        throw new NullReferenceException("Calibrator cannot be null");
      var stockForward = new StockForward(Dt.Roll(deliveryDate, roll, calendar),
        marketQuote, Ccy)
      { Description = tenorName };
      stockForward.Validate();
      Tenors.Add(new CurveTenor(stockForward.Description, stockForward,
        0.0, 0.0, 0.0, 1.0, new EquityForwardQuoteHandler()));
    }

    /// <summary>
    /// Add equity future contract
    /// </summary>
    /// <param name="tenorName">Tenor description</param>
    /// <param name="deliveryDate">Maturity date</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="marketQuote">Delivery price</param>
    public void AddFuture(string tenorName, Dt deliveryDate, BDConvention roll,
      Calendar calendar, double marketQuote)
    {
      var bs = Calibrator as StockCurveFitCalibrator;
      if (bs == null)
        throw new NullReferenceException("Calibrator cannot be null");
      var stockFuture = new StockFuture(deliveryDate, 1.0, 0.1) { Ccy = Ccy, Description = tenorName };
      stockFuture.Validate();
      Tenors.Add(new CurveTenor(stockFuture.Description, stockFuture, marketQuote,
        0.0, 0.0, 1.0, new EquityFutureQuoteHandler()));
    }

    /// <summary>
    /// Total dividend yield between spot date and maturity 
    /// </summary>
    /// <param name="maturity">Maturity date</param>
    /// <returns>Dividend yield</returns>
    /// <remarks>It is the sum of continuous dividend yield and discrete dividend equivalent continuous yield</remarks>
    public double EquivalentDividendYield(Dt maturity)
    {
      if (Spot == null)
        return 0.0;
      if (Spot.Spot >= maturity)
        return 0.0;
      double f = Interpolate(maturity);
      double s = SpotPrice;
      double df = DiscountCurve.DiscountFactor(Spot.Spot, maturity);
      double t = Dt.FractDiff(Spot.Spot, maturity) / 365.0;
      return -Math.Log(f / s * df) / t;
    }


    /// <summary>
    /// Average continuously paid dividend yield between spot date and maturity
    /// </summary>
    /// <param name="spot">Spot tenor</param> 
    /// <param name="maturity">Forward tenor</param>
    /// <returns>Implied dividend yield</returns>
    public double ImpliedDividendYield(Dt spot, Dt maturity)
    {
      if (DividendYieldCurve != null && (maturity > spot))
        return DividendYieldCurve.DividendYield(spot, maturity, (DayCount == DayCount.None) ? DayCount.Actual365Fixed : DayCount);
      return 0.0;
    }

    /// <summary>
    /// Average carry rate on top of risk free rate between spot and delivery
    /// </summary>
    /// <param name="spot">Spot date</param>
    /// <param name="delivery">Delivery date</param>
    /// <returns>Carry rate</returns>
    public override double CarryRateAdjustment(Dt spot, Dt delivery)
    {
      return -ImpliedDividendYield(spot, delivery);
    }

    public static void GetFromToDates(Stock stock, ref Dt begin, ref Dt end)
    {
      if (end <= begin) return;

      foreach (var div in stock.DeclaredDividends ?? EmptyArray<Stock.Dividend>.Instance)
      {
        var payDt = div.PayDate;
        var exDiv = div.ExDivDate;
        if (exDiv <= begin)
        {
          if (begin < payDt) begin = payDt;
          continue;
        }
        if (exDiv <= end && end < payDt) end = payDt;
      }
    }




    /// <summary>
    /// Set the bool value using qxInvokeMethod from XL to choose interpolation scheme
    /// </summary>
    public bool DivInterpolate { get; set; }

    #endregion
  }

  #endregion
}

