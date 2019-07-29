//
// DiscountCurveFitCalibrator.cs
//   2012-2014. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;
using Parameter = BaseEntity.Toolkit.Models.RateModelParameters.Param;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Discount fit calibrator
  /// </summary>
  [Serializable]
  public class DiscountCurveFitCalibrator : DiscountCalibrator,
    IHasCashflowCalibrator, IRateCurveCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(DiscountCurveFitCalibrator));

    #region Static Constructors

    /// <inheritdoc cref="DiscountCurveFit(Dt,CurveTerms,string,string,double[],string[],string[],CalibratorSettings,Dt[],Dt[],double[],PaymentSettings[])"/>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="terms">Market conventions for products used in the calibration</param>
    /// <param name="name">Curve name</param>
    /// <param name="quotes">market quotes</param>
    /// <param name="instrumentNames">Instrument types</param>
    /// <param name="tenorNames">Tenors</param>
    /// <param name="fitSettings">Fit settings</param>
    public static DiscountCurve DiscountCurveFit(
      Dt asOf, CurveTerms terms, string name, double[] quotes,
      string[] instrumentNames, string[] tenorNames, CalibratorSettings fitSettings)
    {
      var paymentSettings = instrumentNames.Select(n => RateCurveTermsUtil.GetPaymentSettings(terms, n)).ToArray();
      return DiscountCurveFit(asOf, terms, name, "", quotes, instrumentNames, tenorNames,
                              fitSettings, null, null, null, paymentSettings);
    }

    /// <summary>
    /// Fits an interest rate discount curve from standard market quotes
    /// </summary>
    /// <remarks>
    /// <para>Fits a discount curve from standard market funding, money market, futures, FRAs and swap quotes.</para>
    /// <para>Supports single curve discounting and 'dual-curve' calibration.</para>
    /// <para>This function is paired with
    /// <see cref="ProjectionCurveFitCalibrator.ProjectionCurveFit(Dt, CurveTerms, DiscountCurve, string, double[], string[], string[], PaymentSettings[], CalibratorSettings)">ProjectionCurveFit</see>
    /// to provide dual-curve calibration.</para>
    /// 
    /// <para><b>Market Quotes</b></para>
    /// <para>The curve calibration is based on a set of <paramref name="quotes">market quotes</paramref>
    /// along with matching matching <paramref name="instrumentNames">instrument names</paramref>
    /// and <paramref name="tenorNames">tenors names</paramref>.</para>
    ///
    /// <para><b>Tenor Names</b></para>
    /// <para>The <paramref name="tenorNames">tenors names</paramref> identify the term of the standard
    /// instrument matching the quote. This may be a tenor such as 5 Year, a date, a Futures IMM code,
    /// or special funding instrument code.</para>
    /// 
    /// <para><b>Instrument Names</b></para>
    /// <para>The <paramref name="instrumentNames">instrument names</paramref> identify the standard
    /// product matching the quote. The name is used to look up the corresponding product terms in the
    /// <paramref name="terms">market terms</paramref>.</para>
    /// 
    /// <para><b>Curve Market Terms</b></para>
    /// <para>The <paramref name="terms">market terms</paramref> specify the <see cref="CurveTerms"/>
    /// that define the set of market standard instrument for calibration.</para>
    /// <para><see cref="CurveTerms"/> contain a set of named <see cref="AssetCurveTerm"/>, each of
    /// which defines the <see cref="DayCount">Day Count</see>, <see cref="BDConvention">Business Day Convention</see>,
    /// coupon frequencies, etc. of a particular quoted market financial instrument.</para>
    /// <para>The <paramref name="instrumentNames">instrument names</paramref> are used to look up the matching
    /// <see cref="AssetCurveTerm"/> in the <see cref="CurveTerms"/>.</para>
    /// <para>Definitions for most common markets are predefined for convenience.</para>
    ///
    /// <para><b>Fit Settings</b></para>
    /// <para>A wide range of sophisticated curve construction methods are supported. Defaults
    /// provide industry standard approaches. The <paramref name="fitSettings">fit settings</paramref>
    /// specify the <see cref="CalibratorSettings"/> that defines specialised numerical settings for
    /// the curve fitting.</para>
    /// <para>The <paramref name="fitSettings">fit settings</paramref> argument is optional with the
    /// following defaults:</para>
    /// <list type="bullet">
    ///   <item><description>WeightedTensionC1 interpolation with smooth extrapolation</description></item>
    ///   <item><description>Calibrate using all market quotes</description></item>
    ///   <item><description>Prioritize futures quotes over money market quotes</description></item>
    ///   <item><description>Prioritize swap quotes over futures quotes</description></item>
    ///   <item><description>Market weight is 1 (best fit to market quotes)</description></item>
    ///   <item><description>Futures weight is 1 (max weight to futures)</description></item>
    ///   <item><description>Futures weight is 1 (max weight to futures)</description></item>
    /// </list>
    /// 
    /// <para><b>Eurodollar Futures Convexity Adjustment</b></para>
    /// <para>The futures prices need to be converted to forward prices for the purposes of
    /// the curve construction. To do this an adjustment needs to be made based on some model
    /// assumptions. There are several alternatives supported for this
    /// <see cref="BaseEntity.Toolkit.Base.FuturesCAMethod">'convexity' adjustment</see>.</para>
    /// 
    /// <para><b>Notes:</b></para>
    /// <para>If basis swap spreads are provided, then the terms and projectionTerms respectively specify
    /// details of the pay leg resetting off the index/tenor of the curve being calibrated and a receive
    /// leg resetting off another index/tenor, which may or may not be the same as the discount curves
    /// index/tenor</para>
    /// <para>If the projection index is not the same as the funding index the calibrator will require basis
    /// swaps and vanilla swaps to calibrate the discount curve. Only basis and vanilla swaps with the
    /// same maturity will be used for calibration.</para>
    /// <para>If the funding Index is the same as the projection Index, the basis swaps will be dropped from
    /// calibration.</para>
    /// <para>InstrumentType.Swap pays fixed and receives (floating) projection Index.</para>
    /// <para>InstrumentType.BasisSwap pays (floating) projection Index and receives (floating) target
    /// projection Index, both discounted at fundingCurve.</para>
    /// <para>For BasisSwap instruments, discountTerms provides the conventions for the leg paying funding (target) Index,
    /// while projectionTerms provides the conventions for the leg paying projection index.</para>
    /// <para>If modelParameters are not null, convexityAdj and futureCAMethods are overridden by model
    /// choice and parameters specified in modelParameters</para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="terms">Market conventions of underlying products</param>
    /// <param name="name">Curve name</param>
    /// <param name="category">Curve category</param>
    /// <param name="quotes">Market quotes for the calibration</param>
    /// <param name="instrumentNames">Type of product matching market quote</param>
    /// <param name="tenorNames">Tenor of product matching quote. May be tenor, date, IMM code, or other identifier</param>
    /// <param name="fitSettings">Curve fitting tuning parameters</param>
    /// <param name="settles">The settle dates of products (null = imply from market terms)</param>
    /// <param name="maturities">The maturities of products (null = imply from market terms)</param>
    /// <param name="weights">The weights given to each product (between 0 and 1) (null = equal weights)</param>
    /// <param name="paymentSettings">An array of PaymentSettings is supported to assign non standard market
    ///   conventions to some or all calibration products</param>
    /// <returns>Calibrated DiscountCurve</returns>
    public static DiscountCurve DiscountCurveFit(
      Dt asOf, CurveTerms terms, string name, string category, double[] quotes,
      string[] instrumentNames, string[] tenorNames, CalibratorSettings fitSettings, Dt[] settles,
      Dt[] maturities, double[] weights, PaymentSettings[] paymentSettings)
    {
      if (tenorNames == null || quotes == null)
      {
        throw new ArgumentException("Quotes and tenor names cannot be empty.");
      }
      if (tenorNames.Length != quotes.Length)
      {
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and tenor names ({1}) not match.",
                                                  quotes.Length, tenorNames.Length));
      }
      if (settles == null || settles.Length != quotes.Length)
        settles = new Dt[quotes.Length];
      if (maturities == null || maturities.Length != quotes.Length)
        maturities = new Dt[quotes.Length];
      var instrumentTypes = new InstrumentType[quotes.Length];
      var freqs = new Frequency[quotes.Length,2];
      var dcs = new DayCount[quotes.Length];
      var rolls = new BDConvention[quotes.Length];
      var cals = new Calendar[quotes.Length];
      var receiverIndices = new ReferenceIndex[quotes.Length];
      var payerIndices = new ReferenceIndex[quotes.Length];
      var assets = new AssetCurveTerm[quotes.Length];
      for (int i = 0; i < quotes.Length; ++i)
      {
        if (Double.IsNaN(quotes[i]) || quotes[i].AlmostEquals(0.0))
          continue;
        var key = instrumentNames[i];
        var itype = instrumentTypes[i] = RateCurveTermsUtil.GetInstrumentType(key, terms);
        if (itype == InstrumentType.None)
        {
          // Have quote but not matching instrument type, throw error
          throw new ArgumentException( String.Format("Quote ({0}) for row {1} specified for invalid Instrument Type ({2})", quotes[i], i + 1, key));
        }
        else if (itype == InstrumentType.Swap)
        {
          SwapAssetCurveTerm swapTerms;
          if (terms.TryGetInstrumentTerm(InstrumentType.Swap, key, out swapTerms))
          {
            assets[i] = swapTerms;
            freqs[i, 0] = swapTerms.PayFreq;
            freqs[i, 1] = swapTerms.FloatPayFreq;
            cals[i] = swapTerms.Calendar;
            dcs[i] = swapTerms.DayCount;
            rolls[i] = swapTerms.BDConvention;
            receiverIndices[i] = swapTerms.ReferenceIndex;
            if (settles[i].IsEmpty())
              settles[i] = Dt.AddDays(asOf, swapTerms.SpotDays, swapTerms.Calendar);
          }
        }
        else if (itype == InstrumentType.BasisSwap)
        {
          BasisSwapAssetCurveTerm bsTerms;
          if (terms.TryGetInstrumentTerm(InstrumentType.BasisSwap, key, out bsTerms))
          {
            assets[i] = bsTerms;
            freqs[i, 0] = bsTerms.RecFreq;
            freqs[i, 1] = bsTerms.PayFreq;
            cals[i] = bsTerms.SpotCalendar;
            receiverIndices[i] = bsTerms.ReceiverIndex;
            payerIndices[i] = bsTerms.PayerIndex;
            if (settles[i].IsEmpty())
              settles[i] = Dt.AddDays(asOf, bsTerms.SpotDays, bsTerms.SpotCalendar);
          }
        }
        else
        {
          AssetCurveTerm term;
          if (terms.TryGetInstrumentTerm(itype, key, out term))
            assets[i] = term;

          receiverIndices[i] = RateCurveTermsUtil.GetAssetReferenceIndex(terms, itype, key).First();
          dcs[i] = RateCurveTermsUtil.GetAssetDayCount(terms, itype, key);
          rolls[i] = RateCurveTermsUtil.GetAssetBDConvention(terms, itype, key);
          cals[i] = RateCurveTermsUtil.GetAssetCalendar(terms, itype, key);
          freqs[i, 0] = RateCurveTermsUtil.GetAssetPaymentFrequency(terms, itype, key);
          if (settles[i].IsEmpty())
            settles[i] = RateCurveTermsUtil.GetTenorSettlement(terms, itype, key, asOf, tenorNames[i]);
        }
      }
      //If curve as of is not specified take shortest settlement date
      if (fitSettings.CurveAsOf.IsEmpty())
        fitSettings.CurveAsOf = settles.Where(dt => dt.IsValid()).Min();
      var savedResets = RateCurveTermsUtil.ClearHistoricalObservations(
        receiverIndices.Concat(payerIndices).Append(terms.ReferenceIndex));
      //do we really need to do this?
      try
      {
        return DiscountCurveFit(fitSettings, terms.ReferenceIndices,
          i => receiverIndices[i] ?? terms.ReferenceIndex, i => payerIndices[i],
          name, category, quotes, instrumentTypes, settles, maturities,
          tenorNames, weights, dcs, freqs, rolls, cals, paymentSettings, assets);
      }
      finally
      {
        foreach (var savedReset in savedResets)
          savedReset.Key.HistoricalObservations = savedReset.Value;
      }
    }

    /// <summary>
    /// Fits an interest rate discount curve from standard market quotes
    /// </summary>
    /// <param name="curveFitSettings">Curve fit settings</param>
    /// <param name="fundingIndex">Funding index</param>
    /// <param name="projectionIndex">Projection index</param>
    /// <param name="name">Name of the curve</param>
    /// <param name="ccy">Currency.</param>
    /// <param name="category">Category of the curve</param>
    /// <param name="quotes">Market quotes for each instrument</param>
    /// <param name="instruments">Instrument types</param>
    /// <param name="settles">Settle dates for each instrument</param>
    /// <param name="maturities">Maturity dates for each instrument</param>
    /// <param name="tenors">Tenors for each instrument</param>
    /// <param name="weights">Weights for each instrument (between 0 and 1, null = equal weighting)</param>
    /// <param name="dayCounts">Day counts by instruments.</param>
    /// <param name="freqs">Frequencies by instruments. If instrumentsType[i] is a fixed vs floating swap set 
    ///   freqs[i,0]= fixed leg frequency and freqs[i,1] = floating leg freqency. If instrumentTypes[i] is basis swap, 
    ///   set freqs[i,0]= tgtIndex leg frequency and freqs[i,1] = projIndex leg frequency </param>
    /// <param name="rolls">Roll conventions for each instrument</param>
    /// <param name="calendars">Calendars for each instrument</param>
    /// <param name="paymentSettings">Additional conventions for calibration instruments</param>
    public static DiscountCurve DiscountCurveFit(CalibratorSettings curveFitSettings, ReferenceIndex fundingIndex,
                                                 ReferenceIndex projectionIndex, string name, Currency ccy,
                                                 string category, double[] quotes, InstrumentType[] instruments,
                                                 Dt[] settles, Dt[] maturities, string[] tenors, double[] weights,
                                                 DayCount[] dayCounts, Frequency[,] freqs, BDConvention[] rolls,
                                                 Calendar[] calendars, PaymentSettings[] paymentSettings)
    {
      return DiscountCurveFit(curveFitSettings, new ReferenceIndex[]{ fundingIndex},
        i => (instruments[i] == InstrumentType.Swap) ? projectionIndex : fundingIndex,
        i => (instruments[i] == InstrumentType.BasisSwap) ? projectionIndex : null,
        name, category, quotes, instruments, settles, maturities, tenors, weights,
        dayCounts, freqs, rolls, calendars, paymentSettings, null);
    }

    /// <summary>
    /// Fits an interest rate discount curve from standard market quotes
    /// </summary>
    /// <remarks>
    /// <para>Fits a discount curve from standard market funding, money market, futures, FRAs and swap quotes.</para>
    /// <para>Supports single curve discounting and 'dual-curve' calibration.</para>
    /// <para>This function is paired with
    /// <see cref="ProjectionCurveFitCalibrator.ProjectionCurveFit(Dt, CurveTerms, DiscountCurve, string, double[], string[], string[], PaymentSettings[], CalibratorSettings)">ProjectionCurveFit</see>
    /// to provide dual-curve calibration.</para>0
    /// 
    /// <para><b>Fit Settings</b></para>
    /// <para>A wide range of sophisticated curve construction methods are supported. Defaults
    /// provide industry standard approaches. The <paramref name="fitSettings">fit settings</paramref>
    /// specify the <see cref="CalibratorSettings"/> that defines specialised numerical settings for
    /// the curve fitting.</para>
    /// <para>The <paramref name="fitSettings">fit settings</paramref> argument is optional with the
    /// following defaults:</para>
    /// <list type="bullet">
    ///   <item><description>WeightedTensionC1 interpolation with smooth extrapolation</description></item>
    ///   <item><description>Calibrate using all market quotes</description></item>
    ///   <item><description>Prioritize futures quotes over money market quotes</description></item>
    ///   <item><description>Prioritize swap quotes over futures quotes</description></item>
    ///   <item><description>Market weight is 1 (best fit to market quotes)</description></item>
    ///   <item><description>Futures weight is 1 (max weight to futures)</description></item>
    ///   <item><description>Futures weight is 1 (max weight to futures)</description></item>
    /// </list>
    /// 
    /// <para><b>Eurodollar Futures Convexity Adjustment</b></para>
    /// <para>The futures prices need to be converted to forward prices for the purposes of
    /// the curve construction. To do this an adjustment needs to be made based on some model
    /// assumptions. There are several alternatives supported for this
    /// <see cref="BaseEntity.Toolkit.Base.FuturesCAMethod">'convexity' adjustment</see>.</para>
    /// 
    /// <para><b>Notes:</b></para>
    /// <para>If basis swap spreads are provided, then the terms and projectionTerms respectively specify
    /// details of the pay leg resetting off the index/tenor of the curve being calibrated and a receive
    /// leg resetting off another index/tenor, which may or may not be the same as the discount curves
    /// index/tenor</para>
    /// <para>If the projection index is not the same as the funding index the calibrator will require basis
    /// swaps and vanilla swaps to calibrate the discount curve. Only basis and vanilla swaps with the
    /// same maturity will be used for calibration.</para>
    /// <para>If the funding Index is the same as the projection Index, the basis swaps will be dropped from
    /// calibration.</para>
    /// <para>InstrumentType.Swap pays fixed and receives (floating) projection Index.</para>
    /// <para>InstrumentType.BasisSwap pays (floating) projection Index and receives (floating) target
    /// projection Index, both discounted at fundingCurve.</para>
    /// <para>For BasisSwap instruments, discountTerms provides the conventions for the leg paying funding (target) Index,
    /// while projectionTerms provides the conventions for the leg paying projection index.</para>
    /// <para>If modelParameters are not null, convexityAdj and futureCAMethods are overridden by model
    /// choice and parameters specified in modelParameters</para>
    /// </remarks>
    /// <param name="fitSettings">Curve fit settings</param>
    /// <param name="referenceIndices">Reference index</param>
    /// <param name="getReceiverIndex">Receiver index</param>
    /// <param name="getPayerIndex">Payer index</param>
    /// <param name="name">Name of the curve</param>
    /// <param name="category">Category of the curve</param>
    /// <param name="quotes">Market quotes for each instrument</param>
    /// <param name="instruments">Instrument types</param>
    /// <param name="settles">Settle dates for each instrument</param>
    /// <param name="maturities">Maturity dates for each instrument</param>
    /// <param name="tenors">Tenors for each instrument</param>
    /// <param name="weights">Weights for each instrument (between 0 and 1, null = equal weighting)</param>
    /// <param name="dayCounts">Day counts by instruments</param>
    /// <param name="freqs">Frequencies by instruments. If instrumentsType[i] is a fixed vs floating swap set 
    ///   freqs[i,0]= fixed leg frequency and freqs[i,1] = floating leg freqency. If instrumentTypes[i] is basis swap, 
    ///   set freqs[i,0]= tgtIndex leg frequency and freqs[i,1] = projIndex leg frequency </param>
    /// <param name="rolls">Roll conventions for each instrument</param>
    /// <param name="calendars">Calendars for each instrument</param>
    /// <param name="paymentSettings">Additional conventions for calibration instruments</param>
    /// <param name="assets"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ToolkitException"></exception>
    public static DiscountCurve DiscountCurveFit(
      CalibratorSettings fitSettings,
      IList<ReferenceIndex> referenceIndices,
      Func<int, ReferenceIndex> getReceiverIndex,
      Func<int, ReferenceIndex> getPayerIndex,
      string name, string category,
      double[] quotes, InstrumentType[] instruments,
      Dt[] settles, Dt[] maturities, string[] tenors, double[] weights,
      DayCount[] dayCounts, Frequency[,] freqs, BDConvention[] rolls,
      Calendar[] calendars, PaymentSettings[] paymentSettings,
      AssetCurveTerm[] assets)
    {
      if (quotes.Length != instruments.Length)
      {
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and instrument types ({1}) not match.",
                                                  quotes.Length, instruments.Length));
      }
      if (maturities == null || maturities.Length == 0)
        maturities = new Dt[quotes.Length];
      else if (maturities.Length != quotes.Length)
      {
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and maturities ({1}) not match.",
                                                  quotes.Length, maturities.Length));
      }
      if (tenors.Length != quotes.Length)
      {
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and tenor names ({1}) not match.",
                                                  quotes.Length, tenors.Length));
      }
      settles = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, settles, Dt.Empty, "quotes", "settles");
      weights = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, weights, 1.0, "quotes", "weights");
      dayCounts = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, dayCounts, DayCount.None, "quotes",
                                                           "day counts");
      freqs = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, 2, freqs, Frequency.None, "quotes", "frequencies");
      rolls = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, rolls, BDConvention.None, "quotes",
                                                       "rolls");
      calendars = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, calendars, Calendar.None, "quotes",
                                                           "calendars");
      paymentSettings = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, paymentSettings, null, "quotes",
                                                                 "paymentSettings");
      var calibrator = new DiscountCurveFitCalibrator(fitSettings.CurveAsOf, fitSettings, referenceIndices);
      var curve = DiscountCurveCalibrationUtils.CreateTargetDiscountCurve(calibrator, fitSettings, calibrator.ReferenceIndex, category, name);
      curve.SpotDays = fitSettings.CurveSpotDays;
      curve.SpotCalendar = fitSettings.CurveSpotCalendar;
      var basisSwapBundles = new SortedMultiMap<Dt, int>();
      for (int i = 0; i < quotes.Length; ++i)
      {
        if (Double.IsNaN(quotes[i]) || quotes[i].AlmostEquals(0.0))
          continue;
        var itype = instruments[i];
        if (itype == InstrumentType.None) continue;
        if (settles[i].IsEmpty())
          settles[i] = DiscountCurveCalibrationUtils.GetSettlement(itype, fitSettings.CurveAsOf, 0, calendars[i]);
        if (maturities[i].IsEmpty() && assets != null && assets.Length >i && assets[i] is RateFuturesCurveTerm && ((RateFuturesCurveTerm)assets[i]).RateFutureType == RateFutureType.ASXBankBill)
        {
          maturities[i] = Dt.ImmDate(settles[i], tenors[i], CycleRule.IMMAUD);
        }
        else if (maturities[i].IsEmpty())
        {
          maturities[i] = DiscountCurveCalibrationUtils.GetMaturity(itype, settles[i], tenors[i], calendars[i], rolls[i]);
        }
        if (maturities[i] <= fitSettings.CurveAsOf) continue;
        string tenorName = getReceiverIndex(i).IndexName + "." +
                           Enum.GetName(typeof(InstrumentType), itype) + "_" +
                           tenors[i];
        switch (itype)
        {
          case InstrumentType.MM:
          case InstrumentType.FUNDMM:
            curve.AddMoneyMarket(tenorName, weights[i], settles[i], maturities[i], quotes[i], dayCounts[i], freqs[i, 0], rolls[i], calendars[i]);
            break;
          case InstrumentType.FUT:
            var futureTerm = assets == null ? null : assets[i] as RateFuturesCurveTerm;
            ReferenceIndex index = null;
            RateFutureType futureType;
            if (futureTerm != null)
            {
              index = new InterestRateIndex(String.Empty, futureTerm.Tenor, futureTerm.Currency, futureTerm.DayCount, futureTerm.Calendar,
                futureTerm.BDConvention, futureTerm.SpotDays);
              futureType = futureTerm.RateFutureType;
            }
            else
            {
              index = getReceiverIndex(i);
              futureType = RateFutureType.MoneyMarketCashRate;
            }
            curve.AddRateFuture(tenorName, weights[i] * fitSettings.FutureWeightFactor, quotes[i],
              maturities[i].Month, maturities[i].Year, index, futureType);
            break;
          case InstrumentType.FRA:
            curve.AddFRA(tenors[i], weights[i], settles[i], maturities[i], quotes[i], getReceiverIndex(i));
            break;
          case InstrumentType.Bond:
            curve.AddRiskFreeBond(tenors[i], weights[i], settles[i], maturities[i], paymentSettings[i].Coupon, dayCounts[i],
                                  rolls[i], calendars[i], freqs[i, 0], paymentSettings[i].BondType, getReceiverIndex(i), quotes[i],
                                  paymentSettings[i].QuoteConvention);
            break;
          case InstrumentType.Swap:
            if (!fitSettings.ChainedSwapApproach && getReceiverIndex(i).IsEqualToAnyOf(referenceIndices))
              curve.AddSwap(tenorName, weights[i], settles[i], maturities[i], quotes[i],
                            dayCounts[i], freqs[i, 0], freqs[i, 1], rolls[i],
                            calendars[i], getReceiverIndex(i), paymentSettings[i]);
            else
              basisSwapBundles.Add(maturities[i], i);
            break;
          case InstrumentType.BasisSwap:
            if (getPayerIndex(i) != null)
              basisSwapBundles.Add(maturities[i], i);
            break;
          default:
            throw new ToolkitException(String.Format("Unknown instrument tyep: {0}.", instruments[i]));
        }
      }
      if (basisSwapBundles.Count > 0) //set up dual curve calibration
      {
        int firstBasisSwap = -1;
        var quoteCurve = (fitSettings.CreateQuotes)
                           ? ConstructQuoteSpline(curve.AsOf, fitSettings.BasisQuotesInterpMethod, basisSwapBundles, quotes, instruments, maturities,
                                                  dayCounts, calendars, freqs, rolls, paymentSettings, out firstBasisSwap)
                           : null;
        foreach (var o in basisSwapBundles)
        {
          bool doInterpolate=false;
          if ((fitSettings.ChainedSwapApproach && o.Value.FindChain(getReceiverIndex, getPayerIndex, calibrator.ReferenceIndex) > 0)
            || CycleCondition(o.Value, getReceiverIndex, getPayerIndex, referenceIndices, (quoteCurve != null), out doInterpolate))
          {
            foreach (var index in o.Value)
            {
              if (instruments[index] == InstrumentType.Swap)
              {
                var id = getReceiverIndex(index).IndexName + "." +
                         Enum.GetName(typeof(InstrumentType), instruments[index]) +
                         "_" + tenors[index];
                curve.AddSwap(id, weights[index], settles[index], maturities[index], quotes[index], dayCounts[index], freqs[index, 0], freqs[index, 1],
                              rolls[index], calendars[index], getReceiverIndex(index), paymentSettings[index]);
              }
              else
              {
                var id = getReceiverIndex(index).IndexName + "."
                         + getPayerIndex(index).IndexName + "." +
                         Enum.GetName(typeof(InstrumentType), instruments[index]) +
                         "_" + tenors[index];
                curve.AddSwap(id, weights[index], settles[index], maturities[index], quotes[index] * 1e-4,
                              freqs[index, 0], freqs[index, 1], getReceiverIndex(index),
                              getPayerIndex(index), Calendar.None, paymentSettings[index]);
              }
            }
            if (doInterpolate) //add interpolated quote
            {
              var swapIndex = o.Value.First(i => instruments[i] == InstrumentType.Swap);
              var id = getReceiverIndex(firstBasisSwap).IndexName + "."
                       + getPayerIndex(firstBasisSwap).IndexName + "." +
                       Enum.GetName(typeof(InstrumentType), instruments[firstBasisSwap]) +
                       "_" + tenors[swapIndex] + "interpolated";
              curve.AddSwap(id, weights[firstBasisSwap], settles[firstBasisSwap], maturities[swapIndex], quoteCurve.Interpolate(maturities[swapIndex]) * 1e-4,
                            freqs[firstBasisSwap, 0], freqs[firstBasisSwap, 1], getReceiverIndex(firstBasisSwap), getPayerIndex(firstBasisSwap),
                            calendars[firstBasisSwap], paymentSettings[firstBasisSwap]);
            }
          }
        }
      }
      var overlapTreatment = new OverlapTreatment(fitSettings.OverlapTreatmentOrder);
      curve.ResolveOverlap(overlapTreatment);
      curve.Fit();
      ConvertBondTenorQuotes(curve);
      return curve;
    }

    private static Curve ConstructQuoteSpline(Dt asOf, InterpMethod interpMethod, SortedMultiMap<Dt, int> basisSwapBundles, double[] quotes, InstrumentType[] instruments, Dt[] maturities, DayCount[] dayCounts, Calendar[] calendars, Frequency[,] freqs, BDConvention[] rolls, PaymentSettings[] paymentSettings, out int firstBasisSwap)
    {
      firstBasisSwap = -1;
      if (basisSwapBundles.SelectMany(p => p.Value).All(i => instruments[i] != InstrumentType.BasisSwap))
        return null;
      var bs =
        basisSwapBundles.Where(p => p.Value.Any(i => instruments[i] == InstrumentType.BasisSwap)).Select(
          p => p.Value.First(i => instruments[i] == InstrumentType.BasisSwap)).ToList();
      int first = bs.First();
      if (
        !bs.Any(
          i =>
          dayCounts[i] != dayCounts[first] || freqs[i, 0] != freqs[first, 0] || freqs[i, 1] != freqs[first, 1] || rolls[i] != rolls[first] ||
          calendars[i] != calendars[first] || !paymentSettings[i].Equals(paymentSettings[first])))
      {
        firstBasisSwap = first;
        var quoteCurve = new Curve(asOf, InterpFactory.FromMethod(interpMethod, ExtrapMethod.Const),
                                   DayCount.Actual365Fixed, Frequency.None);
        foreach (var i in bs)
          quoteCurve.Add(maturities[i], quotes[i]);
        return quoteCurve;
      }
      return null;
    }

    private static bool CycleCondition(IEnumerable<int> set, 
      Func<int, ReferenceIndex> getReceiverIndex,
      Func<int, ReferenceIndex> getPayerIndex, 
      IEnumerable<ReferenceIndex> targetIndex,
      bool interpolateQuote, out bool doInterpolate)
    {
      doInterpolate = false;
      var offsetting = new List<ReferenceIndex>();
      foreach (var i in set)
      {
        var ri = getReceiverIndex(i);
        var oi = getPayerIndex(i);
        if (ri != null)
        {
          int rIdx = offsetting.FindIndex(ri.IsEqual);
          if (rIdx < 0)
            offsetting.Add(ri);
          else
            offsetting.RemoveAt(rIdx);
        }
        if (oi != null)
        {
          int oIdx = offsetting.FindIndex(oi.IsEqual);
          if (oIdx < 0)
            offsetting.Add(oi);
          else
            offsetting.RemoveAt(oIdx);
        }
      }
      if (offsetting.Count == 1)
      {
        if (offsetting[0].IsEqualToAnyOf(targetIndex))
          return true;
        if (interpolateQuote)
        {
          doInterpolate = true;
          return true;
        }
      }
      return false; //drop tenor
    }

    private static void ConvertBondTenorQuotes(DiscountCurve curve)
    {
      foreach (CurveTenor tenor in curve.Tenors)
      {
        if (tenor.Product is Bond && tenor.QuoteHandler != null)
        {
          tenor.UpdateQuote(QuotingConvention.Yield, curve, curve.Calibrator);
        }
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    ///   Discount calibrator by optimization
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fundingIndex">Funding index</param>
    /// <param name="curveFitSettings">Calibration settings</param>
    /// <remarks>
    ///   If the projection index is not the same as the funding index the calibrator will require some basis swaps and some vanilla swaps to calibrate 
    ///   the discount curve. Only basis and vanilla swaps with the same maturity will be used for calibration
    /// </remarks>
    public DiscountCurveFitCalibrator(Dt asOf, ReferenceIndex fundingIndex, CalibratorSettings curveFitSettings)
      : this(asOf, curveFitSettings, new[] { fundingIndex })
    {}

    public DiscountCurveFitCalibrator(Dt asOf)
      : this(asOf, new CalibratorSettings { CurveAsOf = asOf }, null)
    {}

    public DiscountCurveFitCalibrator(Dt asOf, CalibratorSettings curveFitSettings, IList<ReferenceIndex> targetIndices)
      : base(asOf, asOf)
    {
      _targetIndices = targetIndices;
      if (curveFitSettings == null)
        CurveFitSettings = new CalibratorSettings {CurveAsOf = AsOf};
      else
      {
        CurveFitSettings = curveFitSettings;
        if (CurveFitSettings.CurveAsOf.IsEmpty())
          CurveFitSettings.CurveAsOf = AsOf;
        if (curveFitSettings.OverlayAfterCalibration &&
          curveFitSettings.OverlayCurve != null)
        {
          CurveFitAction = new PostAttachOverlayAction(curveFitSettings.OverlayCurve);
        }
      }
      CashflowCalibratorSettings = new CashflowCalibrator.CashflowCalibratorSettings
      {
        SolverTolerance = CurveFitSettings.Tolerance
      };
    }
    #endregion

    #region Calibration

    /// <summary>
    ///   Fit a curve from the specified tenor point
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "fromIdx">Index to start fit from</param>
    /// <remarks>
    ///   <para>Derived calibrated curves implement this to do the work of the
    ///     fitting</para>
    ///   <para>Called by Fit() and Refit(). Child calibrators can assume
    ///     that the tenors have been validated and the data curve has
    ///     been cleared for a full refit (fromIdx = 0).</para>
    /// </remarks>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      curve.ReferenceIndex = ReferenceIndex;
      var calibrationTenors = curve.Tenors.ComposeSwapChain(ReferenceIndex, null, CurveFitSettings.ChainedSwapApproach);
      CashflowCalibrator calibrator = FillData(curve, calibrationTenors);
      calibrator.Lower = 1e-8;
      calibrator.Upper = 1.0;
      IModelParameter vol = null;
      if (CurveFitSettings.Method == CurveFitMethod.SmoothFutures && CurveFitSettings.FwdModelParameters != null)
        CurveFitSettings.FwdModelParameters.TryGetValue(RateModelParameters.Process.Projection, Parameter.Custom,
                                                        out vol);
      if (CurveFitSettings.MaximumIterations >= 0)
      {
        CashflowCalibratorSettings.MaximumOptimizerIterations =
          CashflowCalibratorSettings.MaximumSolverIterations =
          CurveFitSettings.MaximumIterations;
      }
      double[] priceErrors;
      FittingErrorCode =
        calibrator.Calibrate(CurveFitSettings.Method, curve, CurveFitSettings.SlopeWeightCurve,
                             CurveFitSettings.CurvatureWeightCurve, vol, out priceErrors, CashflowCalibratorSettings);
    }

    /// <summary>
    ///   Create a pricer equal to the one used for the discount curve calibration
    /// </summary>
    /// <param name = "curve">Calibrated curve</param>
    /// <param name = "product">Interest rate product</param>
    /// <returns>Instantiated pricer</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var note = product as Note;
      if (note != null)
      {
        var pricer = new NotePricer(note, curve.AsOf, note.Effective, 1.0, (DiscountCurve)curve);
        return pricer;
      }
      var future = product as StirFuture;
      if (future != null)
      {
        var pricer = new StirFuturePricer(future, AsOf, Settle, 1.0 / future.ContractSize, (DiscountCurve)curve, (DiscountCurve)curve)
          {RateModelParameters = CurveFitSettings.FwdModelParameters};
        return pricer;
      }
      var swapLeg = product as SwapLeg;
      if (swapLeg != null)
        return SwapPricerFromSwapLeg(AsOf, curve, swapLeg, ReferenceIndex); //vanilla swap
      var swap = product as Swap;
      if (swap != null)
      {
        var receiverPricer = new SwapLegPricer(swap.ReceiverLeg, curve.AsOf, swap.Effective, 1.0, (DiscountCurve)curve, swap.ReceiverLeg.ReferenceIndex,
                                               GetProjectionCurve(curve, swap.ReceiverLeg.ReferenceIndex), new RateResets(0.0, 0.0),
                                               CurveFitSettings.FwdModelParameters, null)
                             {ApproximateForFastCalculation = CurveFitSettings.ApproximateRateProjection};
        var payerPricer = new SwapLegPricer(swap.PayerLeg, curve.AsOf, swap.Effective, -1.0,
                                            (DiscountCurve)curve, swap.PayerLeg.ReferenceIndex,
                                            GetProjectionCurve(curve, swap.PayerLeg.ReferenceIndex),
                                            new RateResets(0.0, 0.0), CurveFitSettings.FwdModelParameters, null)
                          {ApproximateForFastCalculation = CurveFitSettings.ApproximateRateProjection};
        var pricer = new SwapPricer(receiverPricer, payerPricer);
        pricer.Validate();
        return pricer;
      }
      var fra = product as FRA;
      if (fra != null)
      {
        var pricer = new FRAPricer(fra, curve.AsOf, fra.Effective, (DiscountCurve)curve, (DiscountCurve)curve, 1);
        pricer.Validate();
        return pricer;
      }
      var bond = product as Bond;
      if (bond != null)
      {
        var pricer = new BondPricer(bond, curve.AsOf, curve.AsOf, (DiscountCurve)curve, null, 0, TimeUnit.None, 0.0);
        pricer.Validate();
        return pricer;
      }
      throw new ToolkitException("Product not supported");
    }
    
    private IPricer GetBondPricer(Curve curve, CurveTenor tenor, int curveSpotDays, Calendar curveCalendar)
    {
      var bond = tenor.Product as Bond;
      if (bond != null)
      {
        var quote = tenor.QuoteHandler.GetCurrentQuote(tenor);
        var pricer = new BondPricer(bond, curve.AsOf, Dt.AddDays(curve.AsOf, curveSpotDays, curveCalendar), (DiscountCurve)curve, null, 1, TimeUnit.None, 0.0)
                     {MarketQuote = quote.Value, QuotingConvention = quote.Type};
        if (bond.Floating)
        {
          pricer.ReferenceCurve = (DiscountCurve)curve;
        }
        pricer.Validate();
        return pricer;
      }
      throw new ToolkitException("Product is expected to be bond");
    }

    private static IPricer SwapPricerFromSwapLeg(Dt asOf, CalibratedCurve curve, SwapLeg payerLeg, ReferenceIndex referenceIndex)
    {
      Dt settle = payerLeg.Effective;
      var payerPricer = new SwapLegPricer(payerLeg, asOf, settle, -1.0, (DiscountCurve)curve, referenceIndex,
                                          curve, new RateResets(0.0, 0.0), null, null);
      var receiver = new SwapLeg(settle, payerLeg.Maturity, referenceIndex.IndexTenor.ToFrequency(), 0.0, referenceIndex);
      var receiverPricer = new SwapLegPricer(receiver, asOf, settle, 1.0, (DiscountCurve)curve, referenceIndex, curve,
                                             new RateResets(0.0, 0.0), null, null);
      return new SwapPricer(receiverPricer, payerPricer);
    }


    private static CalibratedCurve GetProjectionCurve(CalibratedCurve curve, ReferenceIndex projectionIndex)
    {
      if (projectionIndex == null)
        return null;
      var cal = curve.Calibrator as DiscountCurveFitCalibrator;
      if ((cal != null && projectionIndex.IsEqualToAnyOf(cal._targetIndices))
        || curve.ReferenceIndex.IsEqual(projectionIndex))
      {
        return curve;
      }
      return GetDependentCurve(curve, projectionIndex);
    }

    private static CalibratedCurve GetDependentCurve(CalibratedCurve parentCrv, ReferenceIndex projectionIndex)
    {
      if (parentCrv.DependentCurves == null || parentCrv.DependentCurves.Count == 0)
        throw new ArgumentException(String.Format("Cannot find projection curve corresponding to index {0}", projectionIndex.IndexName));
      var dcs = parentCrv.DependentCurves.Where(p => p.Value.ReferenceIndex.IsEqual(projectionIndex)).ToArray();
      if (dcs.Length == 0)
        throw new ArgumentException(String.Format("Cannot find projection curve corresponding to index {0}", projectionIndex.IndexName));
      if (dcs.Length > 1)
        throw new ArgumentException(String.Format("Two or more curves corresponding to index {0} were found among dependent curves of {1}.",
                                                  projectionIndex.IndexName, parentCrv.Name));
      return dcs[0].Value;
    }

    private CashflowCalibrator FillData(CalibratedCurve targetCurve, CurveTenorCollection tenors)
    {
      DiscountCurveCalibrationUtils.SetCurveDates(tenors);
      tenors.Sort();
      var calibrator = new CashflowCalibrator(targetCurve.AsOf);
      foreach (CurveTenor tenor in tenors)
      {
        //the new branch to add SwapChain product
        if (tenor.Product is SwapChain)
        {
          var swaps = (SwapChain) tenor.Product;
          var payments = swaps.Chain.GetSwapChainPayments(swaps.Count,
            targetCurve.ReferenceIndex, (DiscountCurve) targetCurve, null,
            CurveFitSettings);
          calibrator.Add(0.0, payments[0].ToArray(), payments[1].ToArray(),
            swaps.Effective, (DiscountCurve) targetCurve, tenor.CurveDate, 1.0,
            NeedsParallel(CurveFitSettings, swaps.ReceiverLeg.ProjectionType),
            NeedsParallel(CurveFitSettings, swaps.PayerLeg.ProjectionType));
        }
        else if (tenor.Product is Note)
        {
          var pricer = (NotePricer)GetPricer(targetCurve, tenor.Product);
          calibrator.Add(1.0, pricer.GetPaymentSchedule(null, targetCurve.AsOf),
            pricer.Settle, (DiscountCurve) targetCurve, tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is StirFuture)
        {
          var pricer = (StirFuturePricer)targetCurve.Calibrator.GetPricer(targetCurve, tenor.Product);
          PaymentSchedule ps = pricer.GetPaymentSchedule(null, targetCurve.AsOf);
          double frac = 0.0;
          foreach (FloatingInterestPayment ip in ps)
            frac += ip.AccrualFactor;
          calibrator.Add((1 - tenor.MarketPv) * frac, ps, pricer.StirFuture.DepositSettlement, null,
                         tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is SwapLeg && ReferenceIndex == null)
        {
          var swap = tenor.Product as SwapLeg;
          if (swap.Floating)
            throw new ToolkitException("Require fixed leg of fixed-floating par swap.");
          var pricer = new SwapLegPricer(swap, targetCurve.AsOf, targetCurve.AsOf,
            1.0, (DiscountCurve) targetCurve, null, null, null, null, null)
          {ApproximateForFastCalculation = CurveFitSettings.ApproximateRateProjection};
          PaymentSchedule ps = pricer.GetPaymentSchedule(null, targetCurve.AsOf);
          ps.AddPayment(new PrincipalExchange(swap.Maturity, 1.0, swap.Ccy));
          calibrator.Add(1.0, ps, pricer.Settle, (DiscountCurve)targetCurve, tenor.CurveDate, 1.0, true);
        }
        else if (tenor.Product is Swap || (tenor.Product is SwapLeg && ReferenceIndex != null))
        {
          var pricer = (SwapPricer)GetPricer(targetCurve, tenor.Product);
          calibrator.Add(0.0, pricer.ReceiverSwapPricer.GetPaymentSchedule(null, targetCurve.AsOf),
            pricer.PayerSwapPricer.GetPaymentSchedule(null, targetCurve.AsOf),
            pricer.Settle, (DiscountCurve) targetCurve, tenor.CurveDate, tenor.Weight, true,
            NeedsParallel(CurveFitSettings, pricer.ReceiverSwapPricer.SwapLeg.ProjectionType),
            NeedsParallel(CurveFitSettings, pricer.PayerSwapPricer.SwapLeg.ProjectionType));
        }
        else if (tenor.Product is FRA)
        {
          var pricer = (FRAPricer)GetPricer(targetCurve, tenor.Product);
          calibrator.Add(0.0, pricer.GetPaymentSchedule(null, targetCurve.AsOf), pricer.Settle, null, tenor.CurveDate,
                         tenor.Weight, true);
        }
        else if (tenor.Product is Bond)
        {
          if (tenor.CurveDate <= targetCurve.AsOf) continue;

          var pricer = (BondPricer)GetBondPricer(targetCurve, tenor, targetCurve.SpotDays, targetCurve.SpotCalendar);

          // if bond is ex div, only include cashflows from after next coupon date
          var cashflowsFrom = pricer.AsOf;
          if (!pricer.Bond.CumDiv(pricer.AsOf, pricer.Settle))
          {
            var nextCouponDate = pricer.NextCouponDate();
            if (!nextCouponDate.IsEmpty())
            {
              cashflowsFrom = Dt.Add(nextCouponDate, 1);
            }
          }
          calibrator.Add(pricer.FullPrice(), pricer.GetPaymentSchedule(null, cashflowsFrom),
            pricer.Settle, (DiscountCurve)targetCurve, tenor.CurveDate, tenor.Weight, true);
        }
        else
        {
          throw new ToolkitException(String.Format("Calibration to products of type {0} not handled",
                                                   tenor.Product.GetType()));
        }
      }
      return calibrator;
    }

    private static bool NeedsParallel(CurveFitSettings settings, ProjectionType type)
    {
      return !settings.ApproximateRateProjection &&
             ((type == ProjectionType.ArithmeticAverageRate) || (type == ProjectionType.GeometricAverageRate));
    }

    #endregion Calibration

    #region Properties

    /// <summary>
    ///   Gets the fitting error code.
    /// </summary>
    /// <value>The fitting error code.</value>
    [Mutable] public CashflowCalibrator.OptimizerStatus FittingErrorCode { get; private set; }

    /// <summary>
    ///   Reference index containing terms of swap floating legs
    /// </summary>
    public ReferenceIndex ReferenceIndex
    {
      get { return _targetIndices != null ? _targetIndices.FirstOrDefault() : null; }
    }

    DiscountCurve IRateCurveCalibrator.DiscountCurve
    {
      get { return null; }
    }

    /// <summary>
    /// settings for CashflowCalibrator
    /// </summary>
    public CashflowCalibrator.CashflowCalibratorSettings CashflowCalibratorSettings { get; private set; }

    private readonly IList<ReferenceIndex> _targetIndices;

    #endregion Properties
  }
}