// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves.Commodities;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Commodity forward curve
  /// </summary>
  /// <remarks>
  ///  <para>The curve represents commodity forward price as a function of time.
  ///  <math>
  ///    P(T) = \left(S_0
  ///     - \sum_{T_i &lt; T}R_{i}\, e^{-\int_0^{T_i} r_t dt } \right)
  ///     e^{\int_0^T (r_t - \delta_t) dt}
  ///  </math> where 
  ///  <m>S_0</m> is the spot price, <m>r_t</m> is the risk free interest rate,
  ///  <m>\delta_t</m> is the continuous lease rate, 
  ///  <m>R_i</m> is the discrete lease payments at time <m>T_i</m>.</para>
  /// 
  /// <para>The lease rate is equal to the convenience yield minus the storage costs.
  ///   Both lease rate and payments can be either positive or negative
  ///   depending on the relative sizes of the benefits and costs of holding the commodity.</para>
  /// </remarks>
  [Serializable]
  public class CommodityCurve : ForwardPriceCurve
  {
    #region SpotCommodityPrice

    /// <summary>
    /// Commodity spot
    /// </summary>
    [Serializable]
    private class SpotCommodityPrice : SpotPrice
    {
      public SpotCommodityPrice(Dt asOf, Currency ccy, double price)
        : base(asOf, 0, Calendar.None, ccy, price)
      {}

      /// <summary>
      /// Returns a <see cref="System.String" /> that represents this instance.
      /// </summary>
      /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
      public override string ToString()
      {
        return string.IsNullOrEmpty(Name) ? base.ToString(): Name;
      }
    }

    #endregion

    #region CommoditySpotCalibrator

    /// <summary>
    /// Dummy calibrator to hold zero curve
    /// </summary>
    [Serializable]
    private class CommoditySpotCalibrator : ForwardPriceCalibrator
    {
      public CommoditySpotCalibrator(Dt asOf, DiscountCurve discountCurve)
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

    #region ImpliedLeaseRateCurve

    /// <summary>
    /// Temporary stub implementation
    /// </summary>
    [Serializable]
    private class ImpliedLeaseRateCurve : CalibratedCurve
    {
      #region Constructors

      public ImpliedLeaseRateCurve(Dt asOf)
        : base(asOf)
      {
        Flags |= CurveFlags.SmoothTime;
      }

      public static ImpliedLeaseRateCurve Create(Dt asOf, double leaseRate)
      {
        return new ImpliedLeaseRateCurve(asOf).SetRelativeTimeRate(leaseRate);
      }

      #endregion

      #region Methods

      public double LeaseRate(Dt spot, Dt deliveryDate, DayCount dayCount)
      {
        return -Math.Log(Interpolate(spot, deliveryDate)) / Dt.Years(spot, deliveryDate, dayCount);
      }

      #endregion
    }

    #endregion

    #region CommodityForwardInterpolator

    [Serializable]
    private sealed class CommodityForwardInterpolator : BaseEntityObject, ICurveInterpolator
    {
      public CommodityForwardInterpolator(CommodityCurve curve)
      {
        UnderlyingCurve = curve;
      }

      private CommodityCurve UnderlyingCurve { get; set; }

      #region Implementation of ICurveInterpolator

      public void Initialize(NativeCurve curve)
      { }

      /// <summary>
      /// Evaluates the curve value at point t.
      /// </summary>
      /// <param name="curve">The curve.</param>
      /// <param name="t">The value of the variable.</param>
      /// <param name="index">The index of the predefined intervals where t locates.</param>
      /// <returns>
      /// The curve value at t.
      /// </returns>
      /// <remarks>
      /// Forward price <m>F^{T}(t) = S(t)e^{-y(T-t)}/B(t,T)</m>
      /// </remarks>
      public double Evaluate(NativeCurve curve, double t, int index)
      {
        var maturity = new Dt(UnderlyingCurve.AsOf, t / 365.0);
        double spotPrice = UnderlyingCurve.SpotPrice;
        if ((UnderlyingCurve.SeasonalityCurve != null) && (UnderlyingCurve.SeasonalityCurve.Count > 0))
          spotPrice *= UnderlyingCurve.SeasonalityCurve.Interpolate(t);
        return CalculateForward(spotPrice, maturity);
      }

      /// <summary>
      /// Seasonality adjusted forward
      /// </summary>
      /// <param name="maturity">Forward tenor</param>
      /// <returns></returns>
      public double SeasonalityAdjustedForward(Dt maturity)
      {
        double spotPrice = UnderlyingCurve.SpotPrice;
        return CalculateForward(spotPrice, maturity);
      }

      private double CalculateForward(double spotPrice, Dt maturity)
      {
        var spot = UnderlyingCurve.Spot.Spot;
        double df = UnderlyingCurve.DiscountCurve.Interpolate(spot, maturity);
        double fwdPrice = (UnderlyingCurve.LeaseRateCurve == null)
          ? spotPrice / df
          : spotPrice * UnderlyingCurve.LeaseRateCurve.Interpolate(spot, maturity) / df;
        if (UnderlyingCurve.LeasePayments != null && UnderlyingCurve.LeasePayments.Any())
          fwdPrice -= UnderlyingCurve.LeasePayments.Pv(spot, maturity, spotPrice, UnderlyingCurve.DiscountCurve) / df;
        return fwdPrice;
      }
      #endregion
    }
    #endregion

    #region CommodityFutureQuoteHandler
    [Serializable]
    private class CommodityFutureQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
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

    #region CommodityForwardQuoteHandler
    [Serializable]
    private class CommodityForwardQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
    {
      #region ICurveTenorQuoteHandler Members

      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        var fwd = (CommodityForward)tenor.Product;
        if (fwd == null)
          throw new ArgumentException("Product not supported");
        return new CurveTenor.Quote(QuotingConvention.ForwardFlatPrice, fwd.DeliveryPrice);
      }

      public double GetQuote(CurveTenor tenor, QuotingConvention targetQuoteType, 
        Curve curve, Calibrator calibrator, bool recalculate)
      {
        var fwd = (CommodityForward)tenor.Product;
        return fwd.DeliveryPrice;
      }

      public void SetQuote(CurveTenor tenor, QuotingConvention quoteType, double quoteValue)
      {
        var fwd = (CommodityForward)tenor.Product;
        fwd.DeliveryPrice = quoteValue;
      }

      public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags bumpFlags)
      {
        var fwd = (CommodityForward)tenor.Product;
        Action<CurveTenor, double> updater = (t, bumpedQuote) => fwd.DeliveryPrice = bumpedQuote;
        return tenor.BumpRawQuote(bumpSize, bumpFlags, updater);
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

    #region CommoditySwapQuoteHandler
    [Serializable]
    private class CommoditySwapQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
    {
      #region ICurveTenorQuoteHandler Members

      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        var swap = (CommoditySwap)tenor.Product;
        if(swap.IsPayerFixed)
          return new CurveTenor.Quote(QuotingConvention.ForwardFlatPrice, swap.PayerLeg.Price);
        if(swap.IsReceiverFixed)
          return new CurveTenor.Quote(QuotingConvention.ForwardFlatPrice, swap.ReceiverLeg.Price);
        if(swap.IsSpreadOnPayer)
          return new CurveTenor.Quote(QuotingConvention.ForwardPriceSpread, swap.PayerLeg.Price);
        if (swap.IsSpreadOnReceiver)
          return new CurveTenor.Quote(QuotingConvention.ForwardPriceSpread, swap.ReceiverLeg.Price);
        throw new ArgumentException("Unsupported swap type");
      }

      public double GetQuote(CurveTenor tenor, QuotingConvention targetQuoteType, 
        Curve curve, Calibrator calibrator, bool recalculate)
      {
        var swap = (CommoditySwap)tenor.Product;
        if (swap.IsPayerFixed)
          return swap.PayerLeg.Price;
        if (swap.IsReceiverFixed)
          return swap.ReceiverLeg.Price;
        if (swap.IsSpreadOnPayer)
          return swap.PayerLeg.Price;
        if (swap.IsSpreadOnReceiver)
          return swap.ReceiverLeg.Price;
        throw new ArgumentException("Unsupported swap type");
      }

      public void SetQuote(CurveTenor tenor, QuotingConvention quoteType, double quoteValue)
      {
        var swap = (CommoditySwap)tenor.Product;
        if (swap.IsPayerFixed)
          swap.PayerLeg.Price = quoteValue;
        else if (swap.IsReceiverFixed)
          swap.ReceiverLeg.Price = quoteValue;
        else if (swap.IsSpreadOnPayer)
          swap.PayerLeg.Price = quoteValue;
        else if (swap.IsSpreadOnReceiver)
          swap.ReceiverLeg.Price = quoteValue;
        throw new ArgumentException("Unsupported swap type");
      }

      public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags bumpFlags)
      {
        var swap = (CommoditySwap)tenor.Product;
        Action<CurveTenor, double> updater = (t, bumpedQuote) =>
                                             {
                                               if (swap.IsPayerFixed)
                                                 swap.PayerLeg.Price = bumpedQuote;
                                               else if (swap.IsReceiverFixed)
                                                 swap.ReceiverLeg.Price = bumpedQuote;
                                               else if (swap.IsSpreadOnPayer)
                                                 swap.PayerLeg.Price = bumpedQuote;
                                               else if (swap.IsSpreadOnReceiver)
                                                 swap.ReceiverLeg.Price = bumpedQuote;
                                             };
        return tenor.BumpPriceQuote(bumpSize, bumpFlags, updater);
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

    #region StaticConstructor
    /// <summary>
    /// Creates the specified curve name.
    /// </summary>
    /// <param name="curveName">Name of the curve.</param>
    /// <param name="tradeDt">The trade dt.</param>
    /// <param name="terms">The terms.</param>
    /// <param name="instrumentNames">The instrument names.</param>
    /// <param name="tenorNames">The tenor names.</param>
    /// <param name="quotes">The quotes.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="spotPrice">The spot price (or null if spot price is not a market observable).</param>
    /// <param name="curveFitSettings">The curve fit settings.</param>
    /// <param name="projectionCurves">The projection curves.</param>
    /// <param name="leasePayments">The lease payments.</param>
    /// <param name="settles">The settles.</param>
    /// <param name="maturities">The maturities.</param>
    /// <param name="weights">The weights.</param>
    /// <returns>CommodityCurve.</returns>
    /// <exception cref="System.ArgumentException">Quotes, instrument and tenor names cannot be empty.</exception>
    public static CommodityCurve Create(
      string curveName,
      Dt tradeDt,
      CurveTerms terms,
      string[] instrumentNames,
      string[] tenorNames,
      double[] quotes,
      DiscountCurve discountCurve,
      double? spotPrice,
      CalibratorSettings curveFitSettings,
      CalibratedCurve[] projectionCurves,
      DividendSchedule leasePayments,
      Dt[] settles,
      Dt[] maturities,
      double[] weights)
    {
      if (tenorNames == null || quotes == null || instrumentNames == null)
      {
        throw new ArgumentException(
          "Quotes, instrument and tenor names cannot be empty.");
      }
      if (tenorNames.Length != quotes.Length)
      {
        throw new ArgumentException(String.Format(
          "The numbers of quotes ({0}) and tenor names ({1}) not match.",
          quotes.Length, tenorNames.Length));
      }
      if (instrumentNames.Length != quotes.Length)
      {
        throw new ArgumentException(String.Format(
          "The numbers of quotes ({0}) and instrument names ({1}) not match.",
          quotes.Length, instrumentNames.Length));
      }
      if(terms==null)
      {
        throw new ArgumentException("Asset term set cannot be null");
      }
      var priceIndex = terms.ReferenceIndex as CommodityPriceIndex;
      if (priceIndex == null && terms.ReferenceIndex != null)
      {
        throw new ArgumentException("Expect reference index to be CommodityPriceIndex");
      }
      if (settles == null || settles.Length != quotes.Length)
        settles = new Dt[quotes.Length];
      if (maturities == null || maturities.Length != quotes.Length)
        maturities = new Dt[quotes.Length];
      var instrumentTypes = new InstrumentType[quotes.Length];
      var freqs = new Frequency[quotes.Length, 2];
      var dcs = new DayCount[quotes.Length];
      var rolls = new BDConvention[quotes.Length];
      var cals = new Calendar[quotes.Length];
      var settings = new CommodityPaymentSettings[quotes.Length];
      var assetTerms = terms.AssetTerms;
      for (int i = 0; i < quotes.Length; ++i)
      {
        if (quotes[i].AlmostEquals(0.0)) continue;
        var key = instrumentNames[i];
        if (String.IsNullOrEmpty(key) || String.Compare(key, "None",
                                                        StringComparison.OrdinalIgnoreCase) == 0)
        {
          continue;
        }
        var at = (assetTerms != null) ? assetTerms[key] : null;
        if (at == null)
        {
          instrumentTypes[i] = (InstrumentType)Enum.Parse(typeof(InstrumentType), key);
          if (priceIndex != null)
          {
            freqs[i, 0] = priceIndex.IndexTenor.ToFrequency();
            dcs[i] = priceIndex.DayCount;
            rolls[i] = priceIndex.Roll;
            cals[i] = priceIndex.Calendar;
            settings[i] = null;
          }
          continue;
        }
        instrumentTypes[i] = at.Type;
        var fut = at as CommodityFutureAssetCurveTerm;
        if(fut != null)
        {
          rolls[i] = fut.Roll;
          cals[i] = fut.Calendar;
          settings[i] = fut.PaymentSettings;
          continue;
        }
        var swp = at as CommoditySwapCurveTerm;
        if (swp != null)
        {
          freqs[i, 0] = swp.PayFreq;
          freqs[i, 1] = swp.FloatPayFreq;
          dcs[i] = swp.DayCount;
          rolls[i] = swp.Roll;
          cals[i] = swp.Calendar;
          settings[i] = swp.PaymentSettings;
          continue;
        }
        var cct = at as CommodityAssetCurveTerm;
        if (cct != null)
        {
          freqs[i, 0] = cct.PayFreq;
          dcs[i] = cct.DayCount;
          rolls[i] = cct.Roll;
          cals[i] = cct.Calendar;
          settings[i] = null;
          continue;
        }
        
        throw new ArgumentException(String.Format(
          "{0}: Unknown asset term.", at.GetType().Name));
      }
      return Create(curveName, spotPrice, curveFitSettings, discountCurve,
        projectionCurves, priceIndex, leasePayments, i => priceIndex, i => null,
        quotes,instrumentTypes, settles, maturities, tenorNames,
        weights, freqs, rolls, cals, settings);
    }

    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="name">Curve name</param>
    /// <param name="spotPrice">Spot price</param>
    /// <param name="curveFitSettings">Fit settings</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="projectionCurves">Curves used for coupon projections</param>
    /// <param name="referenceIndex">Underlying commodity index</param>
    /// <param name="leasePayments">Discrete lease payments</param>
    /// <param name="quotes">Market quotes</param>
    /// <param name="instrumentTypes">Product types</param>
    /// <param name="settles">Settle dates</param>
    /// <param name="maturities">Maturity dates</param>
    /// <param name="tenors">Tenor names</param>
    /// <param name="weights">Calibration weights</param>
    /// <param name="freqs">Product frequencies</param>
    /// <param name="rolls">Roll conventions</param>
    /// <param name="calendars">Calendars</param>
    /// <param name="settings">Pa</param>
    /// <returns></returns>
    public static CommodityCurve Create(
      string name,
      double? spotPrice,
      CalibratorSettings curveFitSettings,
      DiscountCurve discountCurve, IList<CalibratedCurve> projectionCurves,
      CommodityPriceIndex referenceIndex,
      DividendSchedule leasePayments,
      double[] quotes, InstrumentType[] instrumentTypes, Dt[] settles,
      Dt[] maturities, string[] tenors, double[] weights,
      Frequency[,] freqs, BDConvention[] rolls,
      Calendar[] calendars, CommodityPaymentSettings[] settings)
    {
      return Create(name, spotPrice, curveFitSettings, discountCurve, projectionCurves,
        referenceIndex, leasePayments, i => referenceIndex, i => null, quotes,
        instrumentTypes, settles,
        maturities, tenors, weights, freqs, rolls, calendars, settings);
    }

    private static CommodityCurve Create(
     string name,
     double? spotPrice,
     CalibratorSettings curveFitSettings,
     DiscountCurve discountCurve, IList<CalibratedCurve> projectionCurves,
     CommodityPriceIndex referenceIndex,
     DividendSchedule leasePayments,
     Func<int, ReferenceIndex> getReceiverIndex,
     Func<int, ReferenceIndex> getPayerIndex,
     double[] quotes, InstrumentType[] instrumentTypes, Dt[] settles,
     Dt[] maturities, string[] tenors, double[] weights,
     Frequency[,] freqs, BDConvention[] rolls,
     Calendar[] calendars, CommodityPaymentSettings[] settings)
    {
      //Sanity check
      if (tenors.Length != quotes.Length)
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and tenor names ({1}) not match.",
                                                  quotes.Length, tenors.Length));
      if (maturities == null || maturities.Length != quotes.Length)
        maturities = new Dt[quotes.Length];

      settles = DiscountCurveCalibrationUtils.CheckArray(quotes.Length,
                                                         settles, Dt.Empty, "quotes", "settles");
      weights = DiscountCurveCalibrationUtils.CheckArray(quotes.Length,
                                                         weights, 1.0, "quotes", "weights");
      freqs = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, 2, freqs,
                                                       Frequency.None, "quotes", "frequencies");
      rolls = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, rolls,
                                                       BDConvention.None, "quotes", "frequencies");
      calendars = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, calendars,
                                                           Calendar.None, "quotes", "calendars");
      settings = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, settings, null, "quotes", "paymentSettings");
      var asOf = curveFitSettings.CurveAsOf;
      var ccy = discountCurve.Ccy;
      var calibrator = new CommodityForwardFitCalibrator(asOf, discountCurve, 
        projectionCurves, referenceIndex, curveFitSettings);
      var seasonalityCurve = curveFitSettings.OverlayCurve;
      var impliedLeaseRateCurve = new ImpliedLeaseRateCurve(asOf)
      {
        Name = name + "ImpliedLeaseRate",
        Interp = curveFitSettings.GetInterp(),
        SpotDays = curveFitSettings.CurveSpotDays,
        SpotCalendar = curveFitSettings.CurveSpotCalendar
      };
      
      var retVal = (spotPrice.HasValue)
        ? new CommodityCurve(new SpotCommodityPrice(asOf, ccy, spotPrice.Value)
        {Name = $"{name}.SpotCommodityPrice.{ccy}"}, discountCurve,
          impliedLeaseRateCurve, leasePayments, seasonalityCurve, calibrator)
        { Name = name + "_Curve"}
        : new CommodityCurve(calibrator, curveFitSettings.GetInterp(), 
        seasonalityCurve) {Name = name + "_Curve"};
      retVal.ReferenceIndex = referenceIndex;
      // Add tenors
      int count = quotes.Length;
      for (int i = 0; i < count; ++i)
      {
        if (quotes[i].AlmostEquals(0.0))
          continue;
        InstrumentType itype = instrumentTypes[i];
        if (itype == InstrumentType.None)
          continue;
        if (settles[i].IsEmpty())
          settles[i] = DiscountCurveCalibrationUtils.GetSettlement(itype, 
            curveFitSettings.CurveAsOf, referenceIndex.SettlementDays, calendars[i]);
        if (maturities[i].IsEmpty())
          maturities[i] = DiscountCurveCalibrationUtils.GetMaturity(itype, settles[i], 
            tenors[i], calendars[i], rolls[i]);
        if (maturities[i] <= curveFitSettings.CurveAsOf)
          continue;
        string tenorName = getReceiverIndex(i).IndexName + "." +
                           Enum.GetName(typeof(InstrumentType), itype) + "_" +
                           tenors[i];
        switch (itype)
        {
          case InstrumentType.FUT:
            retVal.AddFuture(tenorName, weights[i], maturities[i], 
              (CommodityPriceIndex)getReceiverIndex(i), quotes[i], settings[i]);
            break;
          case (InstrumentType.Swap):

            string swpId = getReceiverIndex(i).IndexName 
              + "." 
              + Enum.GetName(typeof(InstrumentType), InstrumentType.Swap) 
              + "_" 
              + tenors[i];
            retVal.AddSwap(swpId, weights[i], settles[i], maturities[i], 
              quotes[i], freqs[i, 0], freqs[i, 1], rolls[i], calendars[i],
                           (CommodityPriceIndex)getReceiverIndex(i),
                           settings[i]);
            break;
          case InstrumentType.Forward:
            retVal.AddForward(tenorName, weights[i], maturities[i], maturities[i], 
              quotes[i], (CommodityPriceIndex)getReceiverIndex(i));
            break;
          case InstrumentType.BasisSwap: //TODO
            break;
          default:
            throw new ArgumentException(String.Format("Unknown instrument type: {0}.", instrumentTypes[i]));
        }
      }
      retVal.Fit();
      return retVal;
    }

    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="name">Curve name</param>
    /// <param name="asOf">Curve as of date</param>
    /// <param name="spotPrice">Spot price</param>
    /// <param name="curveFitSettings">Fit settings</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="projectionCurves">Curves used for coupon projections</param>
    /// <param name="referenceIndex">Underlying commodity index</param>
    /// <param name="leasePayments">Discrete lease payments</param>
    /// <param name="curveTenors">Curve tenor collection</param>
    /// <returns></returns>
    public static CommodityCurve Create(
      string name,
      Dt asOf,
      double? spotPrice,
      CalibratorSettings curveFitSettings,
      DiscountCurve discountCurve, IList<CalibratedCurve> projectionCurves,
      CommodityPriceIndex referenceIndex,
      DividendSchedule leasePayments, IEnumerable<CurveTenor> curveTenors)
    {
      return Create(name, asOf, spotPrice, curveFitSettings, discountCurve, projectionCurves, referenceIndex, leasePayments, i => referenceIndex, i => null, curveTenors);
    }

    private static CommodityCurve Create(
    string name,
    Dt asOf,
    double? spotPrice,
    CalibratorSettings curveFitSettings,
    DiscountCurve discountCurve, IList<CalibratedCurve> projectionCurves,
    CommodityPriceIndex referenceIndex,
    DividendSchedule leasePayments,
    Func<int, ReferenceIndex> getReceiverIndex,
    Func<int, ReferenceIndex> getPayerIndex,
    IEnumerable<CurveTenor> curveTenors)
    {
      var ccy = discountCurve.Ccy;
      var calibrator = new CommodityForwardFitCalibrator(asOf, discountCurve, projectionCurves, referenceIndex, curveFitSettings);
      var seasonalityCurve = curveFitSettings.OverlayCurve;
      var impliedLeaseRateCurve = new ImpliedLeaseRateCurve(asOf)
      {
        Name = name + "ImpliedLeaseRate",
        Interp = curveFitSettings.GetInterp(),
        SpotDays = curveFitSettings.CurveSpotDays,
        SpotCalendar = curveFitSettings.CurveSpotCalendar
      };
      var retVal = (spotPrice.HasValue)
        ? new CommodityCurve(new SpotCommodityPrice(asOf, ccy, spotPrice.Value) {Name = $"{name}.SpotCommodityPrice.{ccy}"},
          discountCurve, impliedLeaseRateCurve, leasePayments, seasonalityCurve, calibrator){Name = name}
        : new CommodityCurve(calibrator, curveFitSettings.GetInterp(), seasonalityCurve) {Name = name};

      retVal.ReferenceIndex = referenceIndex;

      // Attached tenors to curve
      foreach (var tenor in curveTenors)
      {
        if (tenor == null) continue;
        tenor.UpdateProduct(asOf);
        if (!MatchIndex(tenor, getReceiverIndex(0) ?? getPayerIndex(0))) continue;
        retVal.Tenors.Add(tenor);
      }
      retVal.Fit();
      return retVal;
    }

    private static bool MatchIndex(CurveTenor tenor,
      ReferenceIndex targetIndex)
    {
      if (tenor.Product.Ccy != targetIndex.Currency)
        return false;
      var index = tenor.ReferenceIndex;
      if (index == null)
        return true; // Empty means match any
      return (tenor.Product is Swap) || targetIndex.IsEqual(index);
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="spotPrice">Spot price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="continuousLeaseRate">Continuous lease rate</param>
    /// <param name="discreteLeasePayments">Discrete lease payments</param>
    public CommodityCurve(Dt asOf, double spotPrice,
      DiscountCurve discountCurve, double continuousLeaseRate,
      DividendSchedule discreteLeasePayments)
      : this(new SpotCommodityPrice(asOf, discountCurve.Ccy, spotPrice), discountCurve,
        // We allow negative lease rate (storage costs, etc.)
        (continuousLeaseRate > 0.0 || continuousLeaseRate <0)
          ? ImpliedLeaseRateCurve.Create(asOf, continuousLeaseRate)
          : null,
        discreteLeasePayments, null)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="spotPrice">Spot price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="continuousLeaseRate">Continuous lease rate</param>
    /// <param name="discreteLeasePayments">Discrete lease payments</param>
    /// <param name="seasonalityCurve">Seasonality adjustment</param>
    public CommodityCurve(Dt asOf, double spotPrice,
      DiscountCurve discountCurve, double continuousLeaseRate,
      DividendSchedule discreteLeasePayments, Curve seasonalityCurve)
      : this(new SpotCommodityPrice(asOf, discountCurve.Ccy, spotPrice), discountCurve,
        // We allow negative lease rate (storage costs, etc.)
        (continuousLeaseRate > 0.0 || continuousLeaseRate < 0.0)
          ? ImpliedLeaseRateCurve.Create(asOf, continuousLeaseRate)
          : null,
        discreteLeasePayments, seasonalityCurve)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="futurePrice">Current futures price</param>
    public CommodityCurve(Dt asOf, double futurePrice)
      : this(new SpotCommodityPrice(asOf, Currency.None, futurePrice), 
          new DiscountCurve(asOf, 0.0), null, null, null)
    {}

   /// <summary>
   /// Constructor for commodity curve with no observable spot price
   /// </summary>
   /// <param name="calibrator">Calibrator</param>
   /// <param name="interp">Interpolation/Extrapolation</param>
    /// <param name="seasonalityCurve">Seasonality adjustment</param>
    private CommodityCurve(CommodityForwardFitCalibrator calibrator, 
      Interp interp, Curve seasonalityCurve)
      : base(calibrator, interp)
   {
     SeasonalityCurve = seasonalityCurve;
   }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="spot">Spot spot</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="leaseRateCurve">Continuously paid leasePayments</param>
    /// <param name="leasePayments">Discrete leasePayments</param>
    /// <param name="seasonalityCurve">Seasonality adjustment</param>
    /// <param name="fpCalibrator">Forward price calibrator</param>
    private CommodityCurve(ISpot spot, DiscountCurve discountCurve, 
      ImpliedLeaseRateCurve leaseRateCurve, DividendSchedule leasePayments, 
      Curve seasonalityCurve, ForwardPriceCalibrator fpCalibrator = null )
      : base(spot)
    {
      DiscountCurve = discountCurve;
      LeaseRateCurve = leaseRateCurve;
      SeasonalityCurve = seasonalityCurve;
      LeasePayments = leasePayments ?? new DividendSchedule(spot.Spot);

      Calibrator = fpCalibrator ?? new CommoditySpotCalibrator(spot.Spot, discountCurve);
      Initialize(AsOf, new CommodityForwardInterpolator(this));
      AddSpot();
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
        if (Spot == null)
          return this;
        return LeaseRateCurve;
      }
      protected set
      {
        var leaseCurve = value as ImpliedLeaseRateCurve;
        if (leaseCurve != null) LeaseRateCurve = leaseCurve;
      }
    }

    /// <summary>
    /// Discrete lease payments
    /// </summary>
    public DividendSchedule LeasePayments { get; private set; }

    /// <summary>
    /// Lease cost induced carry basis
    /// </summary>
    private ImpliedLeaseRateCurve LeaseRateCurve { get; set; }

    /// <summary>
    /// Cashflows associated to holding the spot asset
    /// </summary>
    public override IEnumerable<Tuple<Dt, DividendSchedule.DividendType, double>> CarryCashflow
    {
      get { return LeasePayments?.Select(tuple => new Tuple<Dt, DividendSchedule.DividendType, double>(tuple.Item1, tuple.Item2, -tuple.Item3)); }
    }

    /// <summary>
    /// Spot price
    /// </summary>
    public double CommoditySpotPrice
    {
      get { return SpotPrice; }
    }

    /// <summary>
    /// Seasonality
    /// </summary>
    public Curve SeasonalityCurve { get; private set; }


    #endregion

    #region Methods

    /// <summary>
    /// Add commodity swap
    /// </summary>
    /// <param name="name">Product name</param>
    /// <param name="weight">Weight</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="price">Quoted price</param>
    /// <param name="fixedLegFreq">Frequency of fixed payments</param>
    /// <param name="floatLegFreq">Frequency of floating payments</param>
    /// <param name="roll">Roll convention of fixed leg</param>
    /// <param name="calendar">Calendar of fixed leg</param>
    /// <param name="index">Floating commodity price index</param>
    /// <param name="settings">Payment settings</param>
    public void AddSwap(string name, double weight, Dt effective, Dt maturity,
      double price, Frequency fixedLegFreq, Frequency floatLegFreq,
      BDConvention roll, Calendar calendar,
      CommodityPriceIndex index, CommodityPaymentSettings settings)
    {
      var swapSettings = settings as CommoditySwapPaymentSettings;
      swapSettings = swapSettings ?? new CommoditySwapPaymentSettings();
      var payer = new CommoditySwapLeg(effective, maturity, index.Currency, price, 
        fixedLegFreq, roll, calendar, CycleRule.None, 
        ProjectionType.AverageCommodityPrice, index, CommodityPriceObservationRule.First, 0, false, 0, false);
      var psp = (IScheduleParams)payer.Schedule;
      payer.CycleRule = psp.CycleRule;
      payer.Maturity = psp.Maturity;
      var receiver = new CommoditySwapLeg(effective, maturity, floatLegFreq, 0.0, 
        index, swapSettings.RecObservationRule, swapSettings.RecNumObs, false, 0, false);
      //for the time being
      var rsp = (IScheduleParams)receiver.Schedule;
      receiver.CycleRule = rsp.CycleRule;
      receiver.Maturity = rsp.Maturity;
      var swap = new CommoditySwap(receiver, payer);
      Tenors.Add(new CurveTenor(name, swap, 0.0, price, 0.0, weight, new CommoditySwapQuoteHandler()));
    }

    /// <summary>
    /// Add commodity future
    /// </summary>
    /// <param name="name">Product name</param>
    /// <param name="weight">Weight</param>
    /// <param name="lastDeliveryDate">Future delivery date</param>
    /// <param name="referenceIndex">Floating commodity price index</param>
    /// <param name="price">Futures price</param>
    /// <param name="settings">Futures specifications</param>
    public void AddFuture(string name, double weight, Dt lastDeliveryDate, 
      CommodityPriceIndex referenceIndex, double price, CommodityPaymentSettings settings)
    {
      var futSettings = settings as CommodityFuturePaymentSettings;
      futSettings = futSettings ?? new CommodityFuturePaymentSettings();
      var fut = new CommodityFuture(lastDeliveryDate, futSettings.ContractSize, futSettings.TickSize)
        {ReferenceIndex = referenceIndex, Description = name, TickValue = futSettings.TickValue};
      fut.Validate();
      Tenors.Add(new CurveTenor(name, fut, price, 0.0, 0.0, weight, new CommodityFutureQuoteHandler()));
    }

    /// <summary>
    /// Add commodity future
    /// </summary>
    /// <param name="name">Product name</param>
    /// <param name="weight">Weight</param>
    /// <param name="fixingDate">Fixing date for underlying spot asset</param>
    /// <param name="deliveryDate">Delivery date</param>
    /// <param name="deliveryPrice">Delivery price</param>
    /// <param name="referenceIndex">Floating commodity price index</param>
    public void AddForward(string name, double weight, Dt fixingDate, Dt deliveryDate, 
      double deliveryPrice, CommodityPriceIndex referenceIndex)
    {
      var fwd = new CommodityForward(fixingDate, deliveryDate, deliveryPrice, 
        referenceIndex.Roll, referenceIndex.Calendar) {Description = name};
      fwd.Validate();
      Tenors.Add(new CurveTenor(name, fwd, 0.0, 0.0, 0.0, weight, new CommodityForwardQuoteHandler()));
    }


    /// <summary>
    /// Total cost of carry between spot date and maturity 
    /// </summary>
    /// <param name="maturity">Maturity date</param>
    /// <returns>Lease rate</returns>
    /// <remarks>It is the sum of continuous lease rate and discrete lease payments equivalent rate</remarks>
    public double EquivalentLeaseRate(Dt maturity)
    {
      if (Spot == null)
        return 0.0;
      if (Spot.Spot >= maturity)
        return 0.0;
      double f = Interpolate(maturity);
      double s = Spot.Value;
      double df = DiscountCurve.DiscountFactor(Spot.Spot, maturity);
      return RateCalc.RateFromPrice(f * df / s, Spot.Spot, maturity);
    }

    /// <summary>
    /// Average continuously paid lease cost between spot date and maturity
    /// </summary>
    /// <param name="spot">Spot tenor</param> 
    /// <param name="maturity">Forward tenor</param>
    /// <returns>Implied dividend yield</returns>
    public double ImpliedLeaseRate(Dt spot, Dt maturity)
    {
      if (LeaseRateCurve != null && (maturity > spot))
        return LeaseRateCurve.LeaseRate(spot, maturity, 
          (DayCount == DayCount.None) ? DayCount.Actual365Fixed : DayCount);
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
      return ImpliedLeaseRate(spot, delivery);
    }

    /// <summary>
    /// Seasonality adjusted forward commodity price
    /// </summary>
    /// <param name="forwardTenor"></param>
    /// <returns></returns>
    public double SeasonalityAdjustedForward(Dt forwardTenor)
    {
      if (SeasonalityCurve == null || SeasonalityCurve.Count == 0)
        return Interpolate(forwardTenor);
      var interpolator = CustomInterpolator as CommodityForwardInterpolator;
      if (interpolator != null)
        return interpolator.SeasonalityAdjustedForward(forwardTenor);
      return Interpolate(forwardTenor) / SeasonalityCurve.Interpolate(forwardTenor);
    }
    
    #endregion
  }
}
