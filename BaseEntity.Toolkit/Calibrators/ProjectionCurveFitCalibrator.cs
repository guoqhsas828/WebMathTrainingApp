//
// ProjectionCurveFitCalibrator.cs
//   2012-2014. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Models;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;
using Parameter = BaseEntity.Toolkit.Models.RateModelParameters.Param;
using Process = BaseEntity.Toolkit.Models.RateModelParameters.Process;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Projection curve fit calibrator
  /// </summary>
  [Serializable]
  public class ProjectionCurveFitCalibrator : DiscountCalibrator,
    IHasCashflowCalibrator, IRateCurveCalibrator
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof (ProjectionCurveFitCalibrator));

    #region Static Constructors

    /// <inheritdoc cref="ProjectionCurveFitCalibrator.ProjectionCurveFit(Dt,CurveTerms,DiscountCurve,IList{CalibratedCurve},string,string,double[],string[],string[],CalibratorSettings,Dt[],Dt[],double[],PaymentSettings[])"/>
    /// <param name = "tradeDt">Trade date</param>
    /// <param name = "terms">Market conventions of calibrated products</param>
    /// <param name = "fundingCurve">Curve used for discounting</param>
    /// <param name = "name">Curve name</param>
    /// <param name = "quotes">Quotes</param>
    /// <param name = "instrumentNames">Instrument types</param>
    /// <param name = "tenorNames">Tenors</param>
    /// <param name = "paymentSettings">Additional conventions for calibration instruments</param>
    /// <param name = "fitSettings">Calibrator settings</param>
    public static DiscountCurve ProjectionCurveFit(
      Dt tradeDt, CurveTerms terms, DiscountCurve fundingCurve,
      string name, double[] quotes, string[] instrumentNames, string[] tenorNames,
      PaymentSettings[] paymentSettings, CalibratorSettings fitSettings)
    {
      return ProjectionCurveFit(tradeDt, terms, fundingCurve, null, name, "", quotes, instrumentNames, tenorNames, fitSettings, null, null, null, paymentSettings);
    }

    /// <summary>
    /// Fits an interest rate projection curve from standard market quotes
    /// </summary>
    /// <remarks>
    /// <para>Fits a projection curve from standard market funding, money market, futures, FRAs and swap quotes.</para>
    /// <para>Supports single curve discounting and 'dual-curve' calibration.</para>
    /// <para>This function is paired with
    /// <see cref="DiscountCurveFitCalibrator.DiscountCurveFit(Dt, CurveTerms, string, double[], string[], string[], CalibratorSettings)"/>
    /// to provide dual-curve calibration. For single curve calibration only
    /// <see cref="DiscountCurveFitCalibrator.DiscountCurveFit(Dt, CurveTerms, string, double[], string[], string[], CalibratorSettings)"/>
    /// is required.</para>
    /// 
    /// <para><b>Market Quotes</b></para>
    /// <para>The curve calibration is based on a set of <paramref name="quotes">market quotes</paramref>
    /// along with matching matching <paramref name="instrumentNames">instrument names</paramref>
    /// and <paramref name="tenorNames">tenorNames names</paramref>.</para>
    ///
    /// <para><b>Tenor Names</b></para>
    /// <para>The <paramref name="tenorNames">tenorNames names</paramref> identify the term of the standard
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
    /// <param name="fundingCurve">Funding curve</param>
    /// <param name="projectionCurves">Projection curve</param>
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
    public static DiscountCurve ProjectionCurveFit(
      Dt asOf, CurveTerms terms, DiscountCurve fundingCurve, IList<CalibratedCurve> projectionCurves,
      string name, string category, double[] quotes, string[] instrumentNames, string[] tenorNames, CalibratorSettings fitSettings,
      Dt[] settles, Dt[] maturities, double[] weights, PaymentSettings[] paymentSettings)
    {
      if (tenorNames == null || quotes == null)
        throw new ArgumentException("Quotes and tenor names cannot be empty.");
      if (tenorNames.Length != quotes.Length)
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and tenor names ({1}) not match.",
                                                  quotes.Length, tenorNames.Length));
      if (settles == null || settles.Length != quotes.Length)
        settles = new Dt[quotes.Length];
      if (maturities == null || maturities.Length != quotes.Length)
        maturities = new Dt[quotes.Length];
      var instrumentTypes = new InstrumentType[quotes.Length]; 
      var freqs = new Frequency[quotes.Length,2];
      var dcs = new DayCount[quotes.Length];
      var rolls = new BDConvention[quotes.Length];
      var cals = new Calendar[quotes.Length];
      var referenceIndices = new ReferenceIndex[quotes.Length];
      var otherIndices = new ReferenceIndex[quotes.Length];
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
          throw new ArgumentException(String.Format("Quote ({0}) for row {1} specified for invalid Instrument Type ({2})", quotes[i], i + 1, key));
        }
        else if (itype == InstrumentType.Swap)
        {
          SwapAssetCurveTerm swapTerms;
          if (terms.TryGetInstrumentTerm(itype, key, out swapTerms))
          {
            assets[i] = swapTerms;
            freqs[i, 0] = swapTerms.PayFreq;
            freqs[i, 1] = swapTerms.FloatPayFreq;
            cals[i] = swapTerms.Calendar; //fixed leg calendar
            dcs[i] = swapTerms.DayCount; //fixed leg daycount
            rolls[i] = swapTerms.BDConvention; //fixed leg BDConvention
            referenceIndices[i] = swapTerms.ReferenceIndex;
            if (settles[i].IsEmpty())
              settles[i] = Dt.AddDays(asOf, swapTerms.SpotDays, swapTerms.Calendar);
          }
        }
        else if (itype == InstrumentType.BasisSwap)
        {
          BasisSwapAssetCurveTerm bsTerms;
          if (terms.TryGetInstrumentTerm(itype, key, out bsTerms))
          {
            assets[i] = bsTerms;
            freqs[i, 0] = bsTerms.RecFreq;
            freqs[i, 1] = bsTerms.PayFreq;
            cals[i] = bsTerms.SpotCalendar;
            referenceIndices[i] = bsTerms.ReceiverIndex;
            otherIndices[i] = bsTerms.PayerIndex;
            if (settles[i].IsEmpty())
              settles[i] = Dt.AddDays(asOf, bsTerms.SpotDays, bsTerms.SpotCalendar);
          }
        }
        else
        {
          AssetCurveTerm term;
          if (terms.TryGetInstrumentTerm(itype, key, out term))
            assets[i] = term;

          referenceIndices[i] = RateCurveTermsUtil.GetAssetReferenceIndex(terms, itype, key).First();
          dcs[i] = RateCurveTermsUtil.GetAssetDayCount(terms, itype, key);
          rolls[i] = RateCurveTermsUtil.GetAssetBDConvention(terms, itype, key);
          cals[i] = RateCurveTermsUtil.GetAssetCalendar(terms, itype, key);
          freqs[i, 0] = RateCurveTermsUtil.GetAssetPaymentFrequency(terms, itype, key);
          if (settles[i].IsEmpty())
            settles[i] = RateCurveTermsUtil.GetTenorSettlement(terms, itype, key, asOf, tenorNames[i]);
        }
      }
      //If curve as of is not specified take nearest settlement date
      if (fitSettings.CurveAsOf.IsEmpty())
        fitSettings.CurveAsOf = settles.Where(dt => dt.IsValid()).Min();
      var savedResets = RateCurveTermsUtil.ClearHistoricalObservations(
        referenceIndices.Concat(otherIndices).Append(terms.ReferenceIndex));
      try
      {

        return ProjectionCurveFit(name, fitSettings, fundingCurve, projectionCurves, terms.ReferenceIndex,
                                  i => referenceIndices[i] ?? terms.ReferenceIndex, i => otherIndices[i],
                                  category, quotes, instrumentTypes, settles, maturities, tenorNames,
                                  weights, dcs, freqs, rolls, cals, paymentSettings, assets);
      }
      finally
      {
        foreach (var savedReset in savedResets)
          savedReset.Key.HistoricalObservations = savedReset.Value;
      }
    }


    /// <summary>
    /// Fits an interest rate projection curve from standard market quotes
    /// </summary>
    /// <remarks>
    /// <para>Fits a projection curve from standard market funding, money market, futures, FRAs and swap quotes.</para>
    /// <para>Supports single curve discounting and 'dual-curve' calibration.</para>
    /// <para>This function is paired with
    /// <see cref="DiscountCurveFitCalibrator.DiscountCurveFit(Dt, CurveTerms, string, double[], string[], string[], CalibratorSettings)"/>
    /// to provide dual-curve calibration. For single curve calibration only
    /// <see cref="DiscountCurveFitCalibrator.DiscountCurveFit(Dt, CurveTerms, string, double[], string[], string[], CalibratorSettings)"/>
    /// is required.</para>
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
    /// <param name="name">The name of the curve</param>
    /// <param name="fitSettings">The curve fit settings</param>
    /// <param name="discountCurve">The discount curve</param>
    /// <param name="projectionCurve">Given projection curve/curves</param>
    /// <param name="referenceIndexes">Indexes of the target curve</param>
    /// <param name="projectionIndex">Index of the projectionCurve</param>
    /// <param name="ccy">The currency</param>
    /// <param name="category">The category of the curve</param>
    /// <param name="quotes">Market quotes for the calibration</param>
    /// <param name="instrumentTypes">Type of product matching market quote</param>
    /// <param name="settles">The settle dates of products (null = imply from market terms)</param>
    /// <param name="maturities">The maturities of products (null = imply from market terms)</param>
    /// <param name="tenorNames">The tenor names</param>
    /// <param name="weights">The weights given to each product (between 0 and 1) (null = equal weights)</param>
    /// <param name="dayCounts">The day counts by instruments</param>
    /// <param name="freqs">The frequencies by instruments</param>
    /// <param name="rolls">The roll conventions by instruments</param>
    /// <param name="calendars">The calendars</param>
    /// <param name="settings">Additional conventions for calibration instruments</param>
    /// <returns>Calibrated discount curve</returns>
    public static DiscountCurve ProjectionCurveFit(string name, CalibratorSettings fitSettings,
                                                   DiscountCurve discountCurve, DiscountCurve projectionCurve,
                                                   ReferenceIndex[] referenceIndexes, ReferenceIndex projectionIndex,
                                                   Currency ccy, string category, double[] quotes,
                                                   InstrumentType[] instrumentTypes, Dt[] settles,
                                                   Dt[] maturities, string[] tenorNames, double[] weights,
                                                   DayCount[] dayCounts, Frequency[,] freqs, BDConvention[] rolls,
                                                   Calendar[] calendars, PaymentSettings[] settings)
    {
      return ProjectionCurveFit(name, fitSettings, discountCurve, new List<CalibratedCurve>{projectionCurve},
        referenceIndexes[0], i => referenceIndexes.Length ==1 ? referenceIndexes[0] : referenceIndexes[i], i => projectionIndex, category, quotes, instrumentTypes, settles, 
        maturities, tenorNames, weights, dayCounts, freqs, rolls, calendars, settings, null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="curveFitSettings"></param>
    /// <param name="discountCurve"></param>
    /// <param name="projectionCurves"></param>
    /// <param name="referenceIndex"></param>
    /// <param name="getReferenceIndex"></param>
    /// <param name="getOtherIndex"></param>
    /// <param name="category"></param>
    /// <param name="quotes"></param>
    /// <param name="instrumentTypes"></param>
    /// <param name="settles"></param>
    /// <param name="maturities"></param>
    /// <param name="tenors"></param>
    /// <param name="weights"></param>
    /// <param name="dayCounts"></param>
    /// <param name="freqs"></param>
    /// <param name="rolls"></param>
    /// <param name="calendars"></param>
    /// <param name="settings"></param>
    /// <param name="assets"></param>
    /// <returns></returns>
    public static DiscountCurve ProjectionCurveFit(string name, CalibratorSettings curveFitSettings,
      DiscountCurve discountCurve, IList<CalibratedCurve> projectionCurves,
      ReferenceIndex referenceIndex,
      Func<int, ReferenceIndex> getReferenceIndex,
      Func<int, ReferenceIndex> getOtherIndex,
      string category, double[] quotes,
      InstrumentType[] instrumentTypes, Dt[] settles,
      Dt[] maturities, string[] tenors, double[] weights,
      DayCount[] dayCounts, Frequency[,] freqs, BDConvention[] rolls,
      Calendar[] calendars, PaymentSettings[] settings,
      AssetCurveTerm[] assets)
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
      dayCounts = DiscountCurveCalibrationUtils.CheckArray(quotes.Length,
                                                           dayCounts, DayCount.None, "quotes", "day counts");
      freqs = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, 2, freqs,
                                                       Frequency.None, "quotes", "frequencies");
      rolls = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, rolls,
                                                       BDConvention.None, "quotes", "frequencies");
      calendars = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, calendars,
                                                           Calendar.None, "quotes", "calendars");
      settings = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, settings, null, "quotes",
                                                          "paymentSettings");
      Dt asOf = curveFitSettings.CurveAsOf;
      var calibrator = new ProjectionCurveFitCalibrator(asOf, discountCurve, referenceIndex, projectionCurves, curveFitSettings);
      DiscountCurve curve;
      if (curveFitSettings.CreateAsBasis)
      {
        var basisSettings = new CalibratorSettings(curveFitSettings)
                            {
                              OverlayCurve = discountCurve,
                              FwdModelParameters = curveFitSettings.FwdModelParameters
                            };
        curve = DiscountCurveCalibrationUtils.CreateTargetDiscountCurve(calibrator, basisSettings, referenceIndex, category, name);
      }
      else
        curve = DiscountCurveCalibrationUtils.CreateTargetDiscountCurve(calibrator, curveFitSettings, referenceIndex, category, name);
      curve.SpotDays = curveFitSettings.CurveSpotDays;
      curve.SpotCalendar = curveFitSettings.CurveSpotCalendar;
      // Add tenorNames
      int count = quotes.Length;
      for (int i = 0; i < count; ++i)
      {
        if (Double.IsNaN(quotes[i]) || quotes[i].AlmostEquals(0.0))
          continue;
        InstrumentType itype = instrumentTypes[i];
        if (itype == InstrumentType.None)
          continue;
        if (settles[i].IsEmpty())
          settles[i] = DiscountCurveCalibrationUtils.GetSettlement(itype, curveFitSettings.CurveAsOf, 0, calendars[i]);
        if (maturities[i].IsEmpty() && assets != null && assets.Length > i && assets[i] is RateFuturesCurveTerm && ((RateFuturesCurveTerm)assets[i]).RateFutureType == RateFutureType.ASXBankBill)
        {
          maturities[i] = Dt.ImmDate(settles[i], tenors[i], CycleRule.IMMAUD);
        }
        else if (maturities[i].IsEmpty())
          maturities[i] = DiscountCurveCalibrationUtils.GetMaturity(itype, settles[i], tenors[i], calendars[i], rolls[i]);
        if (maturities[i] <= curveFitSettings.CurveAsOf)
          continue;
        string tenorName = getReferenceIndex(i).IndexName + "." +
                           Enum.GetName(typeof(InstrumentType), itype) + "_" +
                           tenors[i];
        switch (itype)
        {
          case InstrumentType.MM:
            curve.AddMoneyMarket(tenorName, weights[i], settles[i], maturities[i], quotes[i],
                                 dayCounts[i], freqs[i, 0], rolls[i], calendars[i]);
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
              index = getReferenceIndex(i);
              futureType = RateFutureType.MoneyMarketCashRate;
            }
            curve.AddRateFuture(tenorName, weights[i] * curveFitSettings.FutureWeightFactor, quotes[i],
              maturities[i].Month, maturities[i].Year, index, futureType);
            break;
          case (InstrumentType.Swap):
            string swpId = getReferenceIndex(i).IndexName + "." + Enum.GetName(typeof(InstrumentType), InstrumentType.Swap) + "_" + tenors[i];
            curve.AddSwap(swpId, weights[i], settles[i], maturities[i], quotes[i], dayCounts[i], freqs[i, 0], freqs[i, 1],
                          rolls[i], calendars[i], getReferenceIndex(i), settings[i]);
            break;
          case InstrumentType.BasisSwap:
            if (getOtherIndex(i) != null)
            {
              string bswpId = getReferenceIndex(i).IndexName + "."
                              + getOtherIndex(i).IndexName + "." +
                              Enum.GetName(typeof(InstrumentType), instrumentTypes[i]) + "_" + tenors[i];
              curve.AddSwap(bswpId, weights[i], settles[i], maturities[i], quotes[i] * 1e-4, freqs[i, 0], freqs[i, 1], getReferenceIndex(i),
                            getOtherIndex(i), Calendar.None, settings[i]);
            }
            break;
          case InstrumentType.FUNDMM:
            curve.AddMoneyMarket(tenorName, weights[i], settles[i], maturities[i], quotes[i],
                                 dayCounts[i], freqs[0, i], rolls[i], calendars[i]);
            break;
          case InstrumentType.FRA:
            curve.AddFRA(tenors[i], weights[i], settles[i], maturities[i],
                         quotes[i], getReferenceIndex(i));
            break;
          default:
            throw new ArgumentException(String.Format("Unknown instrument tyep: {0}.", instrumentTypes[i]));
        }
      }
      var overlap = new OverlapTreatment(curveFitSettings.OverlapTreatmentOrder);
      curve.ResolveOverlap(overlap);
      curve.Fit();
      return curve;
    }

    #endregion Static Constructors

    #region Constructors

    ///<summary>
    ///  Constructor given as-of (pricing) date
    ///</summary>
    ///<remarks>
    ///  <para>Settlement date defaults to as-of date.</para>
    ///  <para>Swap rate interpolation defaults to PCHIP/Const.</para>
    ///</remarks>
    ///<param name = "asOf">As-of (pricing) date</param>
    ///<param name = "discountCurve">Discount curve </param>
    ///<param name = "referenceIndex">Target reference index, i.e. reference index determining the terms of the projection curve to be adjusted by the basis.</param>
    ///<param name = "projectionCurves">Given projection curve/curves</param>
    ///<param name = "curveFitSettings">Calibration settings</param>
    public ProjectionCurveFitCalibrator(Dt asOf, DiscountCurve discountCurve, ReferenceIndex referenceIndex,
                                        IList<CalibratedCurve> projectionCurves, CalibratorSettings curveFitSettings)
      : base(asOf, asOf)
    {
      if (discountCurve == null) throw new ToolkitException("Discount curve cannot be null");
      if (referenceIndex == null) throw new ToolkitException("Target reference index cannot be null");
      DiscountCurve = discountCurve;
      ReferenceIndex = referenceIndex;
      ProjectionCurves = projectionCurves;
      if (curveFitSettings == null)
        CurveFitSettings = new CalibratorSettings {CurveAsOf = AsOf};
      else
      {
        CurveFitSettings = curveFitSettings;
        if (CurveFitSettings.CurveAsOf.IsEmpty())
          CurveFitSettings.CurveAsOf = AsOf;
      }
      CashflowCalibratorSettings = new CashflowCalibrator.CashflowCalibratorSettings();
      SetParentCurves(ParentCurves, DiscountCurve);
      if (ProjectionCurves != null)
        foreach (var projectionCurve in projectionCurves)
          SetParentCurves(ParentCurves, projectionCurve);
    }

    #endregion Constructors

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
    ///     that the tenorNames have been validated and the data curve has
    ///     been cleared for a full refit (fromIdx = 0).</para>
    /// </remarks>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      curve.ReferenceIndex = ReferenceIndex;
      var calibrationTenors = CurveFitSettings.ChainedSwapApproach
        ? curve.Tenors.ComposeSwapChain(ReferenceIndex, GetKnownIndinces(ReferenceIndex))
        : curve.Tenors;
      CashflowCalibrator calibrator = FillData(curve, calibrationTenors);
      calibrator.Lower = 1e-8;
      calibrator.Upper = 1.0;
      IModelParameter vol = null;
      if (CurveFitSettings.Method == CurveFitMethod.SmoothFutures && CurveFitSettings.FwdModelParameters != null)
        CurveFitSettings.FwdModelParameters.TryGetValue(Process.Projection, Parameter.Custom, out vol);
      if (CurveFitSettings.MaximumIterations >= 0)
      {
        CashflowCalibratorSettings.MaximumOptimizerIterations =
          CashflowCalibratorSettings.MaximumSolverIterations =
          CurveFitSettings.MaximumIterations;
      }
      double[] pricerErrors;
      FittingErrorCode =
        calibrator.Calibrate(CurveFitSettings.Method, curve, CurveFitSettings.SlopeWeightCurve,
                             CurveFitSettings.CurvatureWeightCurve, vol, out pricerErrors, CashflowCalibratorSettings);
      if (curve.Name == string.Empty)
        curve.Name = string.Concat(ReferenceIndex.IndexName, "_Curve");
      SetDependentCurves(curve, DiscountCurve);
      if (ProjectionCurves != null)
        foreach (var parent in ProjectionCurves)
          SetDependentCurves(curve, parent);
    }

    /// <summary>
    /// List of parent curves
    /// </summary>
    /// <returns>List of parent curves</returns>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      if (DiscountCurve != null)
        yield return DiscountCurve;
      if (ProjectionCurves != null)
        foreach (var curve in ProjectionCurves.Where(c => c != DiscountCurve && c != null).Distinct())
          yield return curve;
    }

    /// <summary>
    ///   Create a pricer equal to the one used for the basis curve calibration
    /// </summary>
    /// <param name = "curve">Calibrated curve</param>
    /// <param name = "product">Interest rate product</param>
    /// <returns>Instantiated pricer</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var note = product as Note;
      if(note != null)
      {
        Dt settle = note.Effective;
        var pricer = new NotePricer(note, AsOf, settle, 1.0, (DiscountCurve)curve);
        return pricer;
      }
      var future = product as StirFuture;
      if (future != null)
      {
        var pricer = new StirFuturePricer(future, AsOf, Settle, 1.0/future.ContractSize, DiscountCurve, (DiscountCurve)curve)
          { RateModelParameters = CurveFitSettings.FwdModelParameters };
        pricer.Validate();
        return pricer;
      }
      var swap = product as Swap;
      if (swap != null)
      {
        var receiverPricer = new SwapLegPricer(swap.ReceiverLeg, AsOf, swap.Effective, 1.0, DiscountCurve,
                                               swap.ReceiverLeg.ReferenceIndex, GetProjectionCurve(curve, ProjectionCurves, swap.ReceiverLeg.ReferenceIndex),
                                               new RateResets(0.0, 0.0), CurveFitSettings.FwdModelParameters, null)
                             {
                               ApproximateForFastCalculation = CurveFitSettings.ApproximateRateProjection
                             };
        var payerPricer = new SwapLegPricer(swap.PayerLeg, AsOf, swap.Effective, -1.0, DiscountCurve,
                                            swap.PayerLeg.ReferenceIndex, GetProjectionCurve(curve, ProjectionCurves, swap.PayerLeg.ReferenceIndex),
                                            new RateResets(0.0, 0.0), CurveFitSettings.FwdModelParameters, null)
                          {
                            ApproximateForFastCalculation = CurveFitSettings.ApproximateRateProjection
                          };
        var pricer = new SwapPricer(receiverPricer, payerPricer);
        pricer.Validate();
        return pricer;
      }
      var fra = product as FRA;
      if (fra != null)
      {
        var pricer = new FRAPricer(fra, AsOf, fra.Effective, DiscountCurve, (DiscountCurve)curve, 1);
        pricer.Validate();
        return pricer;
      }
      throw new ToolkitException("Product not supported");
    }

    private CashflowCalibrator FillData(CalibratedCurve curve, CurveTenorCollection tenors)
    {
      DiscountCurveCalibrationUtils.SetCurveDates(tenors);
      tenors.Sort();
      IList<CalibratedCurve> projectionCurves = null;
      var calibrator = new CashflowCalibrator(curve.AsOf);
      foreach (CurveTenor tenor in tenors)
      {
        //the new branch to add SwapChain product
        if (tenor.Product is SwapChain)
        {
          var swaps = (SwapChain)tenor.Product;
          var payments = swaps.Chain.GetSwapChainPayments(swaps.Count,
            curve.ReferenceIndex, DiscountCurve,
            projectionCurves ?? (projectionCurves = MergeProjectionCurves(curve)),
            CurveFitSettings);
          calibrator.Add(0.0, payments[0].ToArray(), payments[1].ToArray(),
            swaps.Effective, DiscountCurve, tenor.CurveDate, 1.0,
            NeedsParallel(CurveFitSettings, swaps.ReceiverLeg.ProjectionType),
            NeedsParallel(CurveFitSettings, swaps.PayerLeg.ProjectionType));
        }
        else if (tenor.Product is Note)
        {
          var pricer = (NotePricer) GetPricer(curve, tenor.Product);
          calibrator.Add(1.0, pricer.GetPaymentSchedule(null, curve.AsOf), pricer.Settle, (DiscountCurve)curve,
                         tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is StirFuture)
        {
          var pricer = (StirFuturePricer) curve.Calibrator.GetPricer(curve, tenor.Product);
          var ps = pricer.GetPaymentSchedule(null, curve.AsOf);
          double frac = 0.0;
          foreach (FloatingInterestPayment ip in ps)
            frac += ip.AccrualFactor;
          calibrator.Add((1 - tenor.MarketPv) * frac, ps, pricer.StirFuture.DepositSettlement, null,
                         tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is Swap)
        {
          var pricer = (SwapPricer)GetPricer(curve, tenor.Product);
          if (pricer.PayerSwapPricer.ReferenceCurve != curve && pricer.ReceiverSwapPricer.ReferenceCurve == curve)
          {
            calibrator.Add(-pricer.PayerSwapPricer.Pv() /
                           pricer.PayerSwapPricer.DiscountCurve.Interpolate(pricer.PayerSwapPricer.AsOf, pricer.PayerSwapPricer.Settle),
                           pricer.ReceiverSwapPricer.GetPaymentSchedule(null, curve.AsOf), pricer.ReceiverSwapPricer.Settle,
                           DiscountCurve, tenor.CurveDate, tenor.Weight, true,
                           NeedsParallel(CurveFitSettings, pricer.ReceiverSwapPricer.SwapLeg.ProjectionType));
          }
          else if (pricer.ReceiverSwapPricer.ReferenceCurve != curve && pricer.PayerSwapPricer.ReferenceCurve == curve)
          {
            calibrator.Add(pricer.ReceiverSwapPricer.Pv() /
                           pricer.ReceiverSwapPricer.DiscountCurve.Interpolate(pricer.ReceiverSwapPricer.AsOf, pricer.ReceiverSwapPricer.Settle),
                           pricer.PayerSwapPricer.GetPaymentSchedule(null, curve.AsOf), pricer.PayerSwapPricer.Settle,
                           DiscountCurve, tenor.CurveDate, tenor.Weight, true,
                           NeedsParallel(CurveFitSettings, pricer.PayerSwapPricer.SwapLeg.ProjectionType));
          }
          else if (pricer.ReceiverSwapPricer.ReferenceCurve == curve && pricer.PayerSwapPricer.ReferenceCurve == curve)
          {
            calibrator.Add(0.0, pricer.ReceiverSwapPricer.GetPaymentSchedule(null, curve.AsOf), pricer.PayerSwapPricer.GetPaymentSchedule(null, curve.AsOf),
                           pricer.Settle, DiscountCurve, tenor.CurveDate, tenor.Weight, true,
                           NeedsParallel(CurveFitSettings, pricer.ReceiverSwapPricer.SwapLeg.ProjectionType),
                           NeedsParallel(CurveFitSettings, pricer.PayerSwapPricer.SwapLeg.ProjectionType));
          }
        }
        else if (tenor.Product is FRA)
        {
          var pricer = (FRAPricer)GetPricer(curve, tenor.Product);
          calibrator.Add(0.0, pricer.GetPaymentSchedule(null, curve.AsOf), pricer.Settle, null, tenor.CurveDate,
                         tenor.Weight, true);
        }
        else
        {
          throw new ToolkitException(String.Format("Calibration to products of type {0} not handled",
                                                   tenor.Product.GetType()));
        }
      }
      return calibrator;
    }
    
    private static bool AreEqual(ReferenceIndex referenceIndex, ReferenceIndex otherIndex)
    {
      if (referenceIndex == null || otherIndex == null)
        return false;
      if (referenceIndex == otherIndex ||
        (referenceIndex.IndexTenor == otherIndex.IndexTenor &&
          referenceIndex.IndexName == otherIndex.IndexName))
      {
        return true;
      }
      return false;
    }

    private static bool NeedsParallel(CurveFitSettings settings, ProjectionType type)
    {
      return !settings.ApproximateRateProjection && ((type == ProjectionType.ArithmeticAverageRate) || (type == ProjectionType.GeometricAverageRate));
    }

    private static CalibratedCurve GetProjectionCurve(CalibratedCurve curve, IList<CalibratedCurve> projectionCurves, ReferenceIndex projectionIndex)
    {
      if (projectionIndex == null)
        return null;
      if (AreEqual(curve.ReferenceIndex, projectionIndex))
        return curve;
      if (projectionCurves == null)
        throw new NullReferenceException("Cannot select projection curve for projection index from null ProjectionCurves");
      if (projectionCurves.Count == 1)
        return projectionCurves.First();//should we do this or let error occur?
      var retVal = projectionCurves.Where(c => AreEqual(c.ReferenceIndex, projectionIndex)).ToArray();
      if (retVal.Length == 0)
        throw new ArgumentException(String.Format("Cannot find projection curve corresponding to index {0}", projectionIndex.IndexName));
      if (retVal.Length > 1)
        throw new ArgumentException(String.Format("Two or more curves corresponding to index {0} were found among given projection curves.",
                                                  projectionIndex.IndexName));
      return retVal[0];
    }

    private IList<ReferenceIndex> GetKnownIndinces(ReferenceIndex targetIndex)
    {
      var list = new List<ReferenceIndex>();
      var index = GetCurveIndex(DiscountCurve);
      if (index != null && !index.IsEqual(targetIndex))
      {
        list.Add(index);
      }
      if (ProjectionCurves == null)
        return list;
      foreach (var curve in ProjectionCurves)
      {
        index = GetCurveIndex(curve);
        if (index != null && !index.IsEqual(targetIndex)
          && !list.Any(i => i.IsEqual(index)))
        {
          list.Add(index);
        }
      }
      return list;
    }

    private static ReferenceIndex GetCurveIndex(CalibratedCurve curve)
    {
      return curve == null ? null : curve.ReferenceIndex;
    }

    private IList<CalibratedCurve> MergeProjectionCurves(CalibratedCurve target)
    {
      var list = new List<CalibratedCurve> { target };
      if (ProjectionCurves == null) return list;
      foreach (var projectionCurve in ProjectionCurves)
      {
        if (projectionCurve != null && !list.Contains(projectionCurve))
          list.Add(projectionCurve);
      }
      return list;
    }

    #endregion Calibration

    #region Properties

    /// <summary>
    ///   the Disocunt Curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    ///   The projection curve
    /// </summary>
    public IList<CalibratedCurve> ProjectionCurves { get; private set; }

    /// <summary>
    ///   The target index
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    ///   Gets the fitting error code.
    /// </summary>
    /// <value>The fitting error code.</value>
    [Mutable] public CashflowCalibrator.OptimizerStatus FittingErrorCode { get; private set; }

    /// <summary>
    /// Settings for cashflow calibrator
    /// </summary>
    public CashflowCalibrator.CashflowCalibratorSettings CashflowCalibratorSettings { get; private set; }
    
    #endregion Properties
  }
}