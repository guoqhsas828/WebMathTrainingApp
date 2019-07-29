//
// IterativeInflationBootstrapCalibrator.cs
//
//  -2011. All rights reserved.
//
using System;
using System.Linq;
using log4net;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using System.Collections.Generic;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Bootstrapping of Inflation curve from inflation linked products
  /// </summary>
  [Serializable]
  public class InflationCurveFitCalibrator : ForwardPriceCalibrator
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof(InflationCurveFitCalibrator));
    
    #region Utils
    private static InflationCurve InitializeInflationCurve(CalibratorSettings fitSettings, DiscountCurve discountCurve, InflationIndex inflationIndex,
                                                           bool calibrateRealYields, string curveName, bool laggedSpotValue, IndexationMethod spotIndexationMethod, Tenor spotResetLag, 
                                                           RateResets historicFixings)
    {
      var fixings = historicFixings;
      if (fixings == null)
      {
        if (inflationIndex.HistoricalObservations == null)
          throw new Exception("No fixings available for inflation curve");
        fixings = inflationIndex.HistoricalObservations;
      }

      double asOfInfl;
      if (laggedSpotValue)
      {
        var laggedDate = InflationUtils.PublicationDate(InflationUtils
            .InflationPeriod(RateResetUtil.ResetDate(fitSettings.CurveAsOf, null, spotResetLag),
              inflationIndex.PublicationFrequency, inflationIndex.PublicationLag, spotIndexationMethod).Last(),
          inflationIndex.PublicationFrequency, inflationIndex.PublicationLag);
        asOfInfl = fixings.AllResets.Last(r => r.Key <= laggedDate).Value;
      }
      else
      {
        asOfInfl = fixings.HasCurrentReset
          ? fixings.CurrentReset
          : fixings.AllResets.Last(r => r.Key <= fitSettings.CurveAsOf).Value;
      }

      // add fixings to inflationIndex
      inflationIndex.HistoricalObservations = inflationIndex.HistoricalObservations ?? fixings;
      
      var ibs = new InflationCurveFitCalibrator(fitSettings.CurveAsOf, fitSettings.CurveAsOf, discountCurve,
        inflationIndex, fitSettings) {CurveFitAction = SynchronizeTenors};
      var drc = new DiscountRateCalibrator(ibs.AsOf, ibs.Settle) {Inverse = !calibrateRealYields};
      InflationCurve icurve;
      if (calibrateRealYields)
      {
        var realZeroCurve = new InflationRealCurve(drc, fitSettings.GetInterp(), discountCurve.DayCount, discountCurve.Frequency)
                            {
                              Name = !string.IsNullOrEmpty(curveName) ? curveName : String.Format("{0}.{1}", inflationIndex.Currency, "RealYieldCurve"),
                              Ccy = discountCurve.Ccy,
                              InflationIndex = inflationIndex,
                              IndexationMethod = spotIndexationMethod,
                              IndexationLag = spotResetLag
                            };
        icurve = new InflationCurve(fitSettings.CurveAsOf, asOfInfl, discountCurve, realZeroCurve,
                                    fitSettings.OverlayCurve)
                 {
                   Calibrator = ibs,
                   ReferenceIndex = inflationIndex,
                   Name = String.Format("{0}.{1}", inflationIndex.IndexName, "Curve"),
                   Ccy = inflationIndex.Currency
                 };
      }
      else
      {
        var inflationFactor = new InflationFactorCurve(fitSettings.CurveAsOf)
                              {
                                Calibrator = drc,
                                Interp = fitSettings.GetInterp(),
                                Name = !string.IsNullOrEmpty(curveName) ? curveName : String.Format("{0}.{1}", inflationIndex.IndexName, "FactorCurve"),
                                Ccy = inflationIndex.Currency,
                                InflationIndex = inflationIndex,
                                IndexationMethod = spotIndexationMethod,
                                IndexationLag = spotResetLag
                              };
        icurve = new InflationCurve(fitSettings.CurveAsOf, asOfInfl, inflationFactor, fitSettings.OverlayCurve)
                 {
                   Calibrator = ibs,
                   ReferenceIndex = inflationIndex,
                   Name = String.Format("{0}.{1}", inflationIndex.IndexName, "Curve"),
                   Ccy = inflationIndex.Currency
                 };
      }
      return icurve;
    }

    private static double[] InitializeFloorPrices(double[] floorPrices, double[] marketQuotes, bool useModelFloorPrices)
    {
      if (useModelFloorPrices)
        floorPrices = null;
      else if (floorPrices == null)
        floorPrices = new double[marketQuotes.Length];
      return floorPrices;
    }

    private static QuotingConvention[] InitializeQuotingConventions(QuotingConvention[] quotingConventions, double[] marketQuotes)
    {
      if (quotingConventions == null || quotingConventions.Length == 0)
        quotingConventions = Array.ConvertAll(marketQuotes, q => QuotingConvention.FlatPrice);
      return quotingConventions;
    }

    private static double NormalizeTipsQuote(double marketQuote, QuotingConvention quotingConvention)
    {
      switch (quotingConvention)
      {
        case QuotingConvention.ASW_Mkt:
        case QuotingConvention.ASW_Par:
        case QuotingConvention.ZSpread:
          marketQuote /= 10000;
          break;
        case QuotingConvention.FlatPrice:
        case QuotingConvention.FullPrice:
          marketQuote /= 100;
          break;
        case QuotingConvention.Yield:
          break;
        default:
          throw new ArgumentException("Quote type not supported");
      }
      return marketQuote;
    }
    #endregion

    #region Static Constructors
    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="tradeDt">Trade date</param>
    /// <param name="fitSettings">Calibrator settings</param>
    /// <param name="productTerms">List of terms for each calibration product</param>
    /// <param name="tenors">Calibration tenors</param>
    /// <param name="instrumentNames">Instrument types</param>
    /// <param name="maturity">Maturities</param>
    /// <param name="bondContractualInflations">Contractual inflation for TIPS bonds</param>
    /// <param name="bondCoupons">Bond coupon</param>
    /// <param name="marketQuotes">Market quotes</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="inflationIndex">Underlying inflation index</param>
    /// <param name="indexationLag">Indexation lag</param>
    /// <param name="floorPrices">User overridden floor prices</param>
    /// <param name="useModelFloorPrices">Use model to compute floor prices</param>
    /// <param name="refCurveForAssetSwap">If inflation bond quote is asset swap, provide reference curve for libor leg</param>
    /// <param name="curveName">Name of inflation curve object</param>
    /// <param name="calibrateRealYields">True to calibrate real yield curve</param>
    /// <param name="laggedSpotValue">Indicates whether the spot inflation value is lagged by the indexation lag or not</param>
    /// <param name="spotIndexationMethod">Indexation method for spot price</param>
    /// <returns>Calibrated inflation curve</returns>
    public static InflationCurve FitInflationCurve(
      Dt tradeDt,
      CalibratorSettings fitSettings,
      CurveTerms productTerms,
      string[] tenors,
      string[] instrumentNames,
      Dt[] maturity,
      double[] bondContractualInflations,
      double[] bondCoupons,
      double[] marketQuotes,
      DiscountCurve discountCurve,
      InflationIndex inflationIndex,
      Tenor indexationLag,
      double[] floorPrices,
      bool useModelFloorPrices,
      DiscountCurve refCurveForAssetSwap,
      bool calibrateRealYields,
      string curveName = "",
      bool laggedSpotValue = false,
      IndexationMethod spotIndexationMethod = IndexationMethod.None
      )
    {
      if (productTerms == null)
        throw new ArgumentException("Non null product terms expected");
      if (tenors.Length != marketQuotes.Length)
        throw new ArgumentException("Size of tenors must match number of quotes");
      if (instrumentNames.Length != marketQuotes.Length)
        throw new ArgumentException("Size of instrumentTypes must match number of quotes");
      var instrumentTypes = instrumentNames.GetInstrumentTypes(productTerms);
      if (instrumentTypes.Any(t => t == InstrumentType.Bond))
      {
        if (bondContractualInflations.Length != marketQuotes.Length)
          throw new ArgumentException("Size of bondContractualInflations must match number of quotes");
        if (bondCoupons.Length != marketQuotes.Length)
          throw new ArgumentException("Size of bondCoupons must match number of quotes");
        floorPrices = InitializeFloorPrices(floorPrices, marketQuotes, useModelFloorPrices);
      }
      if (fitSettings.CurveAsOf.IsEmpty())
        fitSettings.CurveAsOf = Dt.AddDays(tradeDt, inflationIndex.SettlementDays, inflationIndex.Calendar);
      var iCurve = InitializeInflationCurve(fitSettings, discountCurve, inflationIndex, calibrateRealYields, curveName, laggedSpotValue, spotIndexationMethod, indexationLag, 
        inflationIndex.HistoricalObservations);
      for (int i = 0; i < marketQuotes.Length; ++i)
      {
        if (Double.IsNaN(marketQuotes[i]) || marketQuotes[i].ApproximatelyEqualsTo(0.0))
          continue;
        var key = instrumentNames[i];
        var type = instrumentTypes[i];
        var pSettle = RateCurveTermsUtil.GetTenorSettlement(productTerms, instrumentTypes[i], instrumentNames[i], tradeDt, tenors[i]);
        var pMaturity = maturity[i].IsValid() ? maturity[i] : RateCurveTermsUtil.GetTenorMaturity(productTerms, type, key, tradeDt, tenors[i], false);
        if (pMaturity < tradeDt)
          continue;
        var pRoll = RateCurveTermsUtil.GetAssetBDConvention(productTerms, type, key);
        var pDayCount = RateCurveTermsUtil.GetAssetDayCount(productTerms, type, key);
        var pCalendar = RateCurveTermsUtil.GetAssetCalendar(productTerms, type, key);
        var pFreq = RateCurveTermsUtil.GetAssetPaymentFrequency(productTerms, type, key);
        switch (type)
        {
          case InstrumentType.Swap:
            SwapAssetCurveTerm swapTerm;
            if (productTerms.TryGetInstrumentTerm(type, key, out swapTerm))
            {
              var yoyTerm = swapTerm as YoYSwapAssetCurveTerm;
              if (yoyTerm != null)
              {
                iCurve.AddYoYSwap(pSettle, pMaturity, yoyTerm.PayFreq, pDayCount, pRoll, pCalendar, indexationLag, yoyTerm.FloatPayFreq,
                                  yoyTerm.InflationRateTenor, marketQuotes[i], yoyTerm.IndexationMethod, yoyTerm.AdjustPeriod, yoyTerm.AdjustLast);
                break;
              }
              var zcTerm = swapTerm as InflationSwapAssetCurveTerm;
              if (zcTerm != null && swapTerm.PayFreq == Frequency.None) //zero coupon
                iCurve.AddZeroCouponSwap(pSettle, pMaturity, swapTerm.CompoundingFreq, pDayCount, pRoll, pCalendar,
                                         indexationLag, marketQuotes[i], zcTerm.IndexationMethod, zcTerm.AdjustPeriod, zcTerm.AdjustLast);
            }
            break;
          case InstrumentType.Bond:
            InflationBondAssetCurveTerm bondTerm;
            if (productTerms.TryGetInstrumentTerm(type, key, out bondTerm))
            {
              iCurve.AddInflationBond(pSettle, pMaturity, bondCoupons[i], bondTerm.BondType, pDayCount, pFreq, pRoll, pCalendar, bondContractualInflations[i],
                                      indexationLag, bondTerm.IndexationMethod, bondTerm.FlooredNotional,
                                      marketQuotes[i], bondTerm.QuotingConvention,
                                      bondTerm.ProjectionType, bondTerm.SpreadType,
                                      (floorPrices != null && i < floorPrices.Length) ? floorPrices[i] : Double.NaN, refCurveForAssetSwap);
            }
            break;
          default:
            continue;
        }
      }
      iCurve.Fit();
      return iCurve;
    }

    /// <summary>
    ///   Static constructor
    /// </summary>
    /// <param name = "tradeDt">Trade date</param>
    /// <param name = "fitSettings">Curve fit settings</param>
    /// <param name = "calibrationProducts">Calibration products</param>
    /// <param name = "tenors">Tenores</param>
    /// <param name = "marketQuotes">Market quotes</param>
    /// <param name = "quotingConventions">Quoting conventions</param>
    /// <param name = "discountCurve">Discount curve</param>
    /// <param name = "inflationIndex">Inflation index</param>
    /// <param name = "indexationLag">Indexation lag</param>
    /// <param name = "floorPrices">Floor prices</param>
    /// <param name = "useModelFloorPrices">True to use floor prices</param>
    /// <param name = "refCurveForAssetSwap">Reference curve</param>
    /// <param name = "calibrateRealYields">True to calibrate real yield curve</param>
    /// <param name = "curveName">Name of inflation curve object</param>
    /// <param name = "laggedSpotValue">Indicates whether the spot inflation value is lagged by the indexation lag or not</param>
    /// <param name = "spotIndexationMethod">Indexation method for spot price</param>
    /// <returns></returns>
    public static InflationCurve FitInflationCurve(Dt tradeDt, CalibratorSettings fitSettings, Product[] calibrationProducts,
                                                   string[] tenors, double[] marketQuotes, QuotingConvention[] quotingConventions,
                                                   DiscountCurve discountCurve, InflationIndex inflationIndex, Tenor indexationLag, double[] floorPrices,
                                                   bool useModelFloorPrices, DiscountCurve refCurveForAssetSwap, bool calibrateRealYields,
                                                   string curveName="", bool laggedSpotValue = false, IndexationMethod spotIndexationMethod = IndexationMethod.None)
    {

      if (fitSettings.CurveAsOf.IsEmpty())
        fitSettings.CurveAsOf = Dt.AddDays(tradeDt, inflationIndex.SettlementDays, inflationIndex.Calendar);
      var iCurve = InitializeInflationCurve(fitSettings, discountCurve, inflationIndex, calibrateRealYields, curveName, 
        laggedSpotValue, spotIndexationMethod, indexationLag, inflationIndex.HistoricalObservations);
      if (calibrationProducts.Any(p => p is InflationBond))
      {
        floorPrices = InitializeFloorPrices(floorPrices, marketQuotes, useModelFloorPrices);
        quotingConventions = InitializeQuotingConventions(quotingConventions, marketQuotes);
      }
      for (int i = 0; i < calibrationProducts.Length; ++i)
      {
        var bond = calibrationProducts[i] as InflationBond;
        if (bond != null)
        {
          if (bond.ResetLag != indexationLag)
            logger.DebugFormat(
              "Calibration of products with differenct indexation lags in not supported. Indexation lag of product {0} will be changed to {1}",
              tenors[i], indexationLag);
          bond.ResetLag = indexationLag;
          iCurve.AddInflationBond(bond, NormalizeTipsQuote(marketQuotes[i], quotingConventions[i]), quotingConventions[i],
                                  (floorPrices != null && i < floorPrices.Length) ? floorPrices[i] : Double.NaN,
                                  refCurveForAssetSwap);
          continue;
        }
        var swap = calibrationProducts[i] as Swap;
        if (swap != null)
        {
          var floater = swap.IsPayerFixed ? swap.ReceiverLeg : swap.PayerLeg;
          if (floater.ResetLag != indexationLag)
            logger.DebugFormat(
              "Calibration of products with differenct indexation lags in not supported. Indexation lag of product {0} will be changed to {1}",
              tenors[i], indexationLag);
          floater.ResetLag = indexationLag;
          if (floater.IndexTenor.IsEmpty)
            floater.IndexTenor = inflationIndex.IndexTenor;
          iCurve.AddInflationSwap(swap, marketQuotes[i]);
          continue;
        }
        throw new ArgumentException(String.Format("Product {0} not supported for InflationCurve calibration", calibrationProducts[i]));
      }
      iCurve.Fit();
      return iCurve;
    }

    /// <summary>
    /// Fit Inflation curve with new list of quotes
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="calibratorSettings"></param>
    /// <param name="quotes"></param>
    /// <param name="discountCurve"></param>
    /// <param name="inflationIndex"></param>
    /// <param name="historicFixings">Historic fixings</param>
    /// <param name="curveName"></param>
    /// <param name="indexationLagParsed"></param>
    /// <param name="asRealYields"></param>
    /// <param name="spotLag"></param>
    /// <param name="spotIndexation"></param>
    /// <returns></returns>
    public static InflationCurve FitInflationCurve(Dt asOf, CalibratorSettings calibratorSettings, IReadOnlyList<CurveTenor> quotes, 
      DiscountCurve discountCurve, InflationIndex inflationIndex, RateResets historicFixings, bool asRealYields, string curveName,
      Tenor indexationLagParsed, bool spotLag, IndexationMethod spotIndexation)
    {
      if (calibratorSettings.CurveAsOf.IsEmpty())
        calibratorSettings.CurveAsOf = Dt.AddDays(asOf, inflationIndex.SettlementDays, inflationIndex.Calendar);
      var iCurve = InitializeInflationCurve(calibratorSettings, discountCurve, inflationIndex, asRealYields, curveName, spotLag, spotIndexation, indexationLagParsed, historicFixings);
      // Attached tenors to curve
      //iCurve.TargetCurve.Tenors = new CurveTenorCollection();
      foreach (var tenor in quotes)
      {
        if (tenor == null) continue;
        tenor.UpdateProduct(asOf);
        if (!MatchIndex(tenor, inflationIndex)) continue;
        iCurve.TargetCurve.Tenors.Add(tenor);
      }
      iCurve.Fit();
      return iCurve;
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
    ///   Calibrate an inflation curve by iterative bootstrapping
    /// </summary>
    /// <param name = "asOf">As of date</param>
    /// <param name = "settle">Settle of underlying contracts</param>
    /// <param name = "discountCurve">Curve used for discounting</param>
    /// <param name = "inflationIndex">Inflation index object</param>
    /// <param name = "fitSettings">Curve fit settings</param>
    public InflationCurveFitCalibrator(Dt asOf, Dt settle, DiscountCurve discountCurve,
                                       InflationIndex inflationIndex, CalibratorSettings fitSettings)
      : base(asOf, settle, discountCurve)
    {
      InflationIndex = inflationIndex;
      CashflowCalibratorSettings = new CashflowCalibrator.CashflowCalibratorSettings();
      CurveFitSettings = fitSettings ?? new CalibratorSettings {CurveAsOf = asOf};
      if (CurveFitSettings.CurveAsOf.IsEmpty())
        CurveFitSettings.CurveAsOf = asOf;
      SetParentCurves(ParentCurves, DiscountCurve);
    }

    #endregion

    #region Calibration
    /// <summary>
    /// Crate specialized pricer
    /// </summary>
    /// <param name="curve">Curve</param>
    /// <param name="product">Product</param>
    /// <returns></returns>
    protected override IPricer GetSpecializedPricer(ForwardPriceCurve curve, IProduct product)
    {
      var iCurve = (InflationCurve)curve;
      var bond = product as InflationBond;
      if (bond != null)
      {
        var pricer = new InflationBondPricer(bond, AsOf, Settle, 1.0, DiscountCurve, InflationIndex, iCurve, null,
                                             CurveFitSettings.FwdModelParameters);
        pricer.Validate();
        return pricer;
      }
      var swap = product as Swap;
      if (swap != null)
      {
        var floatLeg = swap.IsPayerFixed ? swap.ReceiverLeg : swap.PayerLeg;
        var fixedLeg = swap.IsPayerFixed ? swap.PayerLeg : swap.ReceiverLeg;
        var payerLegPricer = new SwapLegPricer(fixedLeg, AsOf, Settle, -1.0, DiscountCurve, null, null, null, null,
                                               null);
        var receiverLegPricer = new SwapLegPricer(floatLeg, AsOf, Settle, 1.0, DiscountCurve, InflationIndex,
                                                  iCurve, null, CurveFitSettings.FwdModelParameters, null);
        var pricer = new SwapPricer(receiverLegPricer, payerLegPricer);
        pricer.Validate();
        return pricer;
      }
      throw new ArgumentException(String.Format("Product {0} not supported", product.Description));
    }

    /// <summary>
    /// </summary>
    protected override void AddData(CurveTenor tenor, CashflowCalibrator calibrator, CalibratedCurve curve)
    {
      if (tenor.Product is InflationBond)
      {
        var bond = (InflationBond)tenor.Product;
        var pricer = (InflationBondPricer)GetPricer(curve, bond);
        
        // asOf date including ex-dividends
        Dt asOf = (!bond.CumDiv(pricer.AsOf, pricer.Settle)) 
          ? Dt.Add(bond.NextCouponDate(pricer.AsOf), 1) : pricer.AsOf;
        calibrator.Add(tenor.MarketPv, pricer.GetPaymentSchedule(
          null, asOf), pricer.Settle, DiscountCurve, tenor.CurveDate,
          tenor.Weight, pricer.DiscountingAccrued);
      }
      else if (tenor.Product is Swap)
      {
        var swap = (Swap)tenor.Product;
        var pricer = (SwapPricer)GetPricer(curve, swap);
        var payer = pricer.PayerSwapPricer;
        calibrator.Add(-payer.Pv() / payer.DiscountCurve.Interpolate(payer.AsOf, payer.Settle),
                       pricer.ReceiverSwapPricer.GetPaymentSchedule(null, curve.AsOf),
                       pricer.Settle, DiscountCurve, tenor.CurveDate, tenor.Weight,
                       pricer.ReceiverSwapPricer.DiscountingAccrued);
      }
    }

    /// <summary>
    /// </summary>
    protected override void SetCurveDates(CalibratedCurve curve)
    {
      foreach (CurveTenor ten in curve.Tenors)
      {
        var bond = ten.Product as InflationBond;
        if (bond != null)
        {
          ten.CurveDate = InflationUtils.PublicationDate(
            InflationUtils.InflationPeriod(RateResetUtil.ResetDate(bond.Maturity, null, bond.ResetLag), InflationIndex.PublicationFrequency,
                                           InflationIndex.PublicationLag, bond.IndexationMethod).Last(),
            InflationIndex.PublicationFrequency, InflationIndex.PublicationLag);
          continue;
        }
        var swap = ten.Product as Swap;
        if (swap != null)
        {
          var floater = swap.IsPayerFixed ? swap.ReceiverLeg : swap.PayerLeg;

            IndexationMethod indexationMethod = floater is InflationSwapLeg
                ? (floater as InflationSwapLeg).IndexationMethod 
                : IndexationMethod.CanadianMethod;
                     
          ten.CurveDate = InflationUtils.PublicationDate(
          InflationUtils.InflationPeriod(RateResetUtil.ResetDate(floater.Maturity, null, floater.ResetLag), InflationIndex.PublicationFrequency,
                                           InflationIndex.PublicationLag, indexationMethod).Last(),
          InflationIndex.PublicationFrequency, InflationIndex.PublicationLag);
        }
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Indexation lag
    /// </summary>
    private Tenor IndexationLag { get; set; }
    
    /// <summary>
    ///   Inflation index
    /// </summary>
    internal InflationIndex InflationIndex
    {
      get; private set;
    }

    #endregion

    #region Nested Type: SynchronizeTenorsAction

    private class SynchronizTenorsAction : ICurveFitAction
    {
      public object PreProcess(CalibratedCurve curve)
      {
        return null;
      }

      public void PostProcess(object state, CalibratedCurve curve)
      {
        var target = ((InflationCurve) curve).TargetCurve;
        if (target == null)
          return;

        bool inverse = ((DiscountRateCalibrator)target.Calibrator).Inverse;
        target.Tenors = new CurveTenorCollection();
        var asOf = target.AsOf;
        const DayCount dc = DayCount.Actual365Fixed;
        const Frequency freq = Frequency.Continuous;
        for (int i = 0, count = target.Count; i < count; ++i)
        {
          var curveDate = target.GetDt(i);
          var maturity = GetMaturity(i, curveDate, curve.Tenors);
          var df = target.GetVal(i);
          var yield = RateCalc.RateFromPrice(inverse ? (1/df) : df, asOf, maturity, dc, freq);
          var note = new Note(asOf, maturity,
            target.Ccy, yield, dc, freq, BDConvention.None, Calendar.None);
          note.Validate();
          var tenor = target.Add(note, 0.0, 1.0, 0.0, 0.0);
          var name = GetMatchedTenorName(i, curveDate, curve.Tenors);
          if (name != null) tenor.Name = name;
        }
        return;
      }

      private static string GetMatchedTenorName(int idx, Dt curveDate,
        CurveTenorCollection tenors)
      {
        int count = tenors.Count;
        for (int i = idx; i < count; ++i)
          if (tenors[i].CurveDate == curveDate) return tenors[i].Name;
        for (int i = 0; i < idx; ++i)
          if (tenors[i].CurveDate == curveDate) return tenors[i].Name;
        return null;
      }

      private static Dt GetMaturity(int idx, Dt curveDate,
        CurveTenorCollection tenors)
      {
        int count = tenors.Count;
        for (int i = idx; i < count; ++i)
          if (tenors[i].CurveDate == curveDate) return tenors[i].Maturity;
        for (int i = 0; i < idx; ++i)
          if (tenors[i].CurveDate == curveDate) return tenors[i].Maturity;
        return Dt.Empty;
      }
    }

    private static readonly ICurveFitAction SynchronizeTenors
      = new SynchronizTenorsAction();

    #endregion
  }

}