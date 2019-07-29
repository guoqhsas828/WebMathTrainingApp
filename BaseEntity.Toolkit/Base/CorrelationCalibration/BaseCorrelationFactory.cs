/*
 * BaseCorrelationFactory.cs
 *
 *  -2008. All rights reserved.
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Pricers.Baskets;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///   Factory methods to create base correlation object
  /// </summary>
  public static class BaseCorrelationFactory
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseCorrelationFactory));

    private static readonly bool useTopDownCalibrator_ = true;

    #region Base Correlation Calibration

    /// <summary>
    ///  Calculate the base correlations from provided market quotes using either TopDown or BottomUp
    /// </summary>
    /// <param name="calibrtnMethod">Base correlation calibration method (MaturityMatch/TermStructure)</param>
    /// <param name="strikeMethod">Base correlation strike method (ExpectedLossPv, probability, etc)</param>
    /// <param name="indexScalingCalibrator">Index scaling calibrator</param>
    /// <param name="runningPrem">Array of running premia</param>
    /// <param name="dp">Array of detachment points</param>
    /// <param name="quotes">Double array of CDO market quotes</param>
    /// <param name="cdx">Optional array of CDX products</param>
    /// <param name="maturities">Optional array of CDX maturities</param>
    /// <param name="useTenors">Optional array of boolean, use tenors or not</param>
    /// <param name="survivalCurves">Optional array of underlying survival curves</param>
    /// <param name="paramsObj">Optional base correlation parameter object</param>
    /// <returns>A base correlation surface</returns>
    public static BaseCorrelationTermStruct BaseCorrelationFromMarketQuotes(
      BaseCorrelationCalibrationMethod calibrtnMethod,
      BaseCorrelationStrikeMethod strikeMethod,
      IndexScalingCalibrator indexScalingCalibrator,
      double[,] runningPrem,
      double[] dp,
      double[,] quotes,
      CDX[] cdx,
      Dt[] maturities,
      bool[] useTenors,
      SurvivalCurve[] survivalCurves,
      BaseCorrelationParam paramsObj )
    {
      // Get all required arguments before branching the TopDown and BottomUp 
      if (indexScalingCalibrator == null)
      {
        if ((survivalCurves == null || survivalCurves.Length == 0))
          throw new ArgumentException("Must provide survival curves when no scaling calibrator provided");
      }
      else if (cdx == null || cdx.Length == 0)
      {
        // we get cdx from the calibrator
        cdx = indexScalingCalibrator.Indexes;
      }

      // Check nonempty of cdx array
      CDX cdxTerm = null;
      if (cdx != null)
      {
        for (int i = 0; i < cdx.Length; ++i)
          if (cdx[i] != null)
          {
            cdxTerm = cdx[i];
            break;
          }
      }
      if (cdxTerm == null)
        throw new ArgumentException("Must provide CDX products or nonempty scaling calibrator");

      // Retrieve portfolio information
      double[] principals; // the original principals
      Dt asOf, settle;
      DiscountCurve discountCurve;
      if (survivalCurves != null && survivalCurves.Length != 0)
      {
        // If both unscaled survival curves and index scaling calculator 
        // are given, simply use the unscaled survival curves      
        logger.DebugFormat("Use unscaled survival curves");

        principals = PrincipalsFromCDX(cdxTerm, survivalCurves.Length);
        GetDatesAndDiscountCurve(survivalCurves, out asOf, out settle, out discountCurve);
      }
      else
      {
        logger.DebugFormat("Use index scaling calibrator");
        discountCurve = indexScalingCalibrator.DiscountCurve;
        asOf = indexScalingCalibrator.AsOf;
        settle = indexScalingCalibrator.Settle;
        survivalCurves = indexScalingCalibrator.GetScaleSurvivalCurves();
        principals = PrincipalsFromCDX(cdxTerm, survivalCurves.Length);
      }


      // Get maturities from either cdx notes or index scaling calculator
      if (maturities == null || maturities.Length == 0)
        maturities = Array.ConvertAll<CDX, Dt>(cdx,
                                               delegate(CDX c) { return c == null ? Dt.Empty : c.Maturity; });
      string[] tenorNames = Array.ConvertAll<CDX, string>(cdx,
                                                          delegate(CDX c) { return c == null ? null : c.Description; });

      // Check real tenors
      int nTenors = maturities.Length;
      int nDps = dp.Length;

      // Validate tranches and tenors
      if (nTenors < 1)
        throw new System.ArgumentException("Must specify at least one maturity date");
      if (nTenors != quotes.GetLength(1))
        throw new System.ArgumentException(String.Format(
          "Number of maturities ({0}) and columns of quotes ({1}) not match",
          nTenors, quotes.GetLength(1)));
      if (quotes.GetLength(0) != nDps)
        throw new System.ArgumentException(
          "Rows of quotes must match the number of detachment points");

      // Check and set up the running premiums
      runningPrem = SetUpRunningPremiums(runningPrem, nDps, nTenors);

      // Find the effective detachments
      RemoveTrailingZeros(ref dp, quotes);

      // Find the number of effective tenors
      int nRealTenors = FindRealTenors(useTenors, quotes,
                                       maturities, tenorNames, runningPrem);
      if (nRealTenors != maturities.Length)
      {
        // Note: FindRealTenors() has already moved all the effective maturities
        //   to the begining of the maturities array, so we simply truncate it
        //   such that its length reflect the number of effective tenors.
        Array.Resize(ref maturities, nRealTenors);
        Array.Resize(ref tenorNames, nRealTenors);
      }

      // Parameter object
      if (paramsObj == null)
        paramsObj = new BaseCorrelationParam();
      BaseCorrelationMethod bcMethod = paramsObj.Method;
      InterpMethod strikeInterpMethod = paramsObj.StrikeInterp;
      ExtrapMethod strikeExtrapMethod = paramsObj.StrikeExtrap;
      InterpMethod tenorInterpMethod = paramsObj.TenorInterp;
      ExtrapMethod tenorExtrapMethod = paramsObj.TenorExtrap;
      double toleranceF = paramsObj.ToleranceF;
      double toleranceX = paramsObj.ToleranceX;
      double min = paramsObj.Min;
      double max = paramsObj.Max;

      // Create CDO term object
      SyntheticCDO cdoTerm = new SyntheticCDO(
        cdxTerm.Effective, maturities[nRealTenors - 1], Currency.None, 0.0001,
        cdxTerm.DayCount, cdxTerm.Freq, cdxTerm.BDConvention, cdxTerm.Calendar);

      // Create a Basket
      BasketPricer basket = CreateBasket(asOf, settle,
                                         cdoTerm.Maturity, survivalCurves, principals, paramsObj);

      // Adjust quadrature points based on detachments if the user does not specify it
      if (paramsObj.QuadraturePoints <= 0)
        basket.IntegrationPointsFirst += 
          BasketPricerFactory.DefaultQuadraturePointsAdjust(dp);

      // Check if we need to modify recovery curves
      if (paramsObj.DiscardRecoveryDispersion
          || !Double.IsNaN(paramsObj.OverrideRecoveryRate))
      {
        basket.RecoveryCurves = CloneUtil.Clone(basket.RecoveryCurves);
        // discard the recovery dispersions?
        if (paramsObj.DiscardRecoveryDispersion)
          SuppressRecoveryDispersions(basket.RecoveryCurves);
        // override the recovery rate?
        if (!Double.IsNaN(paramsObj.OverrideRecoveryRate))
          OverrideRecoveryRates(basket.RecoveryCurves,
                                paramsObj.OverrideRecoveryRate, basket.Maturity);
      }

      // Calibrate base correlation
      BaseCorrelationTermStruct bco = null;
      if (paramsObj.BottomUp || useTopDownCalibrator_)
      {
        bco = BaseCorrelationTermStruct.FromMarketQuotes(
          quotes, runningPrem, dp, maturities, tenorNames, indexScalingCalibrator,
          cdoTerm, basket, discountCurve, calibrtnMethod, bcMethod, strikeMethod,
          null, toleranceF, toleranceX, strikeInterpMethod, strikeExtrapMethod,
          tenorInterpMethod, tenorExtrapMethod, min, max, paramsObj.BottomUp);
      }
      else
      {
        int nCDOs = dp.Length;
        // Ignore missing or 100 detachment points
        int nZeros = 0;
        for (int i = nCDOs - 1; i >= 0; --i)
        {
          if (dp[i] <= 0.0 || dp[i] > 1.0)
            ++nZeros;
        }
        nCDOs -= nZeros;

        //This is important: the topdown model is only used when 1.0 is the last detachment
        if (dp[nCDOs - 1] != 1)
          throw new System.ArgumentException("The last detachment point must be 100%");

        SyntheticCDO[][] cdoArray = new SyntheticCDO[nRealTenors][];
        for (int j = 0, idx = 0; j < nRealTenors; ++j)
        {
          SyntheticCDO[] cdos = new SyntheticCDO[nCDOs];
          if (quotes[nCDOs - 1, j] == 0)
            throw new ArgumentException(
              "Super senior tranche (dps=100%) can not have 0-value premium");
          for (int i = 0; i < nCDOs; i++)
          {
            bool isFee = (Math.Abs(quotes[i, j]) < 1.0);
            if (!isFee && quotes[i, j] <= 0.0)
            {
              throw new System.ArgumentException(String.Format(
                "Quote value must be greater than 0, not {0}", quotes[i, j]));
            }
            cdos[i] = new SyntheticCDO(cdoTerm.Effective, maturities[j], cdoTerm.Ccy,
                                       (isFee ? runningPrem[i, j] : quotes[i, j]) / 10000.0,
                                       cdxTerm.DayCount, cdxTerm.Freq, cdxTerm.BDConvention, cdxTerm.Calendar);

            cdos[i].Attachment = (i == 0) ? 0.0 : dp[i - 1];
            cdos[i].Detachment = dp[i];

            if (isFee)
            {
              cdos[i].Fee = quotes[i, j];
              cdos[i].FeeSettle = Dt.Add(settle, 1);
            }
            cdos[i].Validate();
          }
          cdoArray[idx++] = cdos;
        }
        bco = new BaseCorrelationTermStruct(
          bcMethod, strikeMethod, null, calibrtnMethod, cdoArray, basket,
          null, discountCurve, toleranceF, toleranceX,
          strikeInterpMethod, strikeExtrapMethod, tenorInterpMethod,
          tenorExtrapMethod, min, max);
        BaseCorrelationCalibrator cal;
        if (bcMethod == BaseCorrelationMethod.ArbitrageFree)
          cal = new DirectArbitrageFreeCalibrator(calibrtnMethod, cdoTerm, basket,
                                                  discountCurve, maturities, dp, runningPrem, quotes,
                                                  toleranceF, toleranceX, paramsObj.BottomUp);
        else // protection match method
          cal = new DirectLossPvMatchCalibrator(
            cdoTerm, basket, discountCurve,
            maturities, dp, runningPrem, quotes, toleranceF, toleranceX);
        if (indexScalingCalibrator != null)
          cal.IndexTerm = indexScalingCalibrator;
        bco.InterpOnFactors = paramsObj.InterpOnFactors;
        bco.Calibrator = cal;
        bco.TenorNames = tenorNames;
      }
      bco.InterpOnFactors = paramsObj.InterpOnFactors;
      bco.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;
      return bco;
    }

    public static BaseCorrelationObject BaseCorrelationFromCdoQuotes(
      BaseCorrelationCalibrationMethod calibrtnMethod,
      BaseCorrelationStrikeMethod strikeMethod,
      Dt effective, Dt asOf, Dt settle, DayCount dayCount,
      Frequency frequency, BDConvention roll, Calendar calendar,
      double[,] runningPrem, double[] dp, Dt[] maturities, double[,] cdoQuotes,
      DiscountCurve discountCurve, SurvivalCurve[] sc, double[] prins,
      BaseCorrelationParam paramsObj)
    {
      Copula copula = paramsObj.Copula;
      double toleranceF = paramsObj.ToleranceF;
      double toleranceX = paramsObj.ToleranceX;
      double min = paramsObj.Min;
      double max = paramsObj.Max;

      int nCDOs = dp.Length, nRealTenors = maturities.Length;
      SyntheticCDO cdoTerm = new SyntheticCDO(
        effective, maturities[nRealTenors - 1],
        Currency.None, 0.0001, dayCount, frequency, roll, calendar);
      BasketPricer basket = BaseCorrelationFactory.CreateBasket(
        asOf, settle, cdoTerm.Maturity, sc, prins, paramsObj);

      // Check if we need to modify recovery curves
      if (paramsObj.DiscardRecoveryDispersion || !Double.IsNaN(paramsObj.OverrideRecoveryRate))
      {
        basket.RecoveryCurves = CloneUtil.Clone(basket.RecoveryCurves);
        // discard the recovery dispersions?
        if (paramsObj.DiscardRecoveryDispersion)
          BaseCorrelationFactory.SuppressRecoveryDispersions(basket.RecoveryCurves);
        // override the recovery rate?
        if (!Double.IsNaN(paramsObj.OverrideRecoveryRate))
          BaseCorrelationFactory.OverrideRecoveryRates(basket.RecoveryCurves, paramsObj.OverrideRecoveryRate, basket.Maturity);
      }

      // Adjust quadrature points based on detachments if the user does not specify it
      if (paramsObj.QuadraturePoints <= 0)
        basket.IntegrationPointsFirst += BasketPricerFactory.DefaultQuadraturePointsAdjust(dp);
      // Calibrate base correlation
      BaseCorrelationTermStruct bco = null;
      if (paramsObj.BottomUp)
      {
        bco = BaseCorrelationTermStruct.FromMarketQuotes(
          cdoQuotes, runningPrem, dp, maturities, null, null,
          cdoTerm, basket, discountCurve, calibrtnMethod, paramsObj.Method, strikeMethod,
          null, toleranceF, toleranceX, paramsObj.StrikeInterp, paramsObj.StrikeExtrap,
          paramsObj.TenorInterp, paramsObj.TenorExtrap, min, max, paramsObj.BottomUp);
      }
      else
      {
        //This is important: the topdown model is only used when 1.0 is the last detachment
        if (dp[nCDOs - 1] != 1)
          throw new System.ArgumentException("The last detachment point must be 100%");

        SyntheticCDO[][] cdoArray = new SyntheticCDO[nRealTenors][];
        for (int j = 0, idx = 0; j < nRealTenors; ++j)
        {
          SyntheticCDO[] cdos = new SyntheticCDO[nCDOs];
          if (cdoQuotes[nCDOs - 1, j] == 0)
            throw new ArgumentException(
              "Super senior tranche (dps=100%) can not have 0-value premium");
          for (int i = 0; i < nCDOs; i++)
          {
            bool isFee = (Math.Abs(cdoQuotes[i, j]) < 1.0);
            if (!isFee && cdoQuotes[i, j] <= 0.0)
            {
              throw new System.ArgumentException(String.Format(
                "Quote value must be greater than 0, not {0}", cdoQuotes[i, j]));
            }
            cdos[i] = new SyntheticCDO(settle, maturities[j], Currency.None,
              (isFee ? runningPrem[i, j] : cdoQuotes[i, j]) / 10000.0,
              dayCount, frequency, roll, calendar);

            cdos[i].Attachment = (i == 0) ? 0.0 : dp[i - 1];
            cdos[i].Detachment = dp[i];

            if (isFee)
            {
              cdos[i].Fee = cdoQuotes[i, j];
              cdos[i].FeeSettle = Dt.Add(settle, 1);
            }
            cdos[i].Validate();
          }
          cdoArray[idx++] = cdos;
        }
        bco = new BaseCorrelationTermStruct(
          paramsObj.Method, strikeMethod, null, calibrtnMethod, cdoArray, basket,
          null, discountCurve, toleranceF, toleranceX,
          paramsObj.StrikeInterp, paramsObj.StrikeExtrap, paramsObj.TenorInterp,
          paramsObj.TenorExtrap, min, max);
      }
      bco.InterpOnFactors = paramsObj.InterpOnFactors;
      bco.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;

      return bco;
    }

    #endregion Base Correlation Calibration

    #region Base Correlation Wrapping
    /// <summary>
    ///   Construct a base correlation surface directly from strikes and quoted base correlations
    /// </summary>
    ///
    /// <param name="calibrtnMethod">Base correlation calibration method (MaturityMatch/TermStructure)</param>
    /// <param name="strikeMethod">Base correlation strike method (ExpectedLossPv, probability, etc)</param>
    /// <param name="indexScalingCalibrator">Index scaling calibrator</param>
    /// <param name="runningPrem">Array of running premia</param>
    /// <param name="dp">Array of detachment points</param>
    /// <param name="quotes">Double array of CDO market quotes</param>
    /// <param name="strikes">Double array of scaled strikes</param>
    /// <param name="baseCorrelations">Double array of base correlation values matching the strikes</param>
    /// <param name="cdx">Optional array of CDX products</param>
    /// <param name="maturities">Optional array of CDX maturities</param>
    /// <param name="useTenors">Optional array of boolean, use tenors or not</param>
    /// <param name="survivalCurves">Optional array of underlying survival curves</param>
    /// <param name="paramsObj">Optional base correlation parameter object</param>
    ///
    /// <returns>Created base correlation surface</returns>
    ///
    public static BaseCorrelationTermStruct BaseCorrelationWrapCalibrator(
      BaseCorrelationCalibrationMethod calibrtnMethod,
      BaseCorrelationStrikeMethod strikeMethod,
      IndexScalingCalibrator indexScalingCalibrator,
      double[,] runningPrem,
      double[] dp,
      double[,] quotes,
      double[,] strikes,
      double[,] baseCorrelations,
      CDX[] cdx,
      Dt[] maturities,
      bool[] useTenors,
      SurvivalCurve[] survivalCurves,
      BaseCorrelationParam paramsObj )
    {
      // Get all required arguments before branching the TopDown and BottomUp 
      if (indexScalingCalibrator == null)
      {
        if ((survivalCurves == null || survivalCurves.Length == 0))
          throw new ArgumentException("Must provide survival curves when no scaling calibrator provided");
      }
      else if (cdx == null || cdx.Length == 0)
      {
        // we get cdx from the calibrator
        cdx = indexScalingCalibrator.Indexes;
      }

      // Check nonempty of cdx array
      CDX cdxTerm = null;
      if (cdx != null)
      {
        for (int i = 0; i < cdx.Length; ++i)
          if (cdx[i] != null)
          {
            cdxTerm = cdx[i];
            break;
          }
      }
      if (cdxTerm == null)
        throw new ArgumentException("Must provide CDX products or nonempty scaling calibrator");

      // Retrieve portfolio information
      double[] principals; // the original principals
      Dt asOf, settle;
      DiscountCurve discountCurve;
      if (survivalCurves != null && survivalCurves.Length != 0)
      {
        // If both unscaled survival curves and index scaling calculator 
        // are given, simply use the unscaled survival curves      
        logger.DebugFormat("Use unscaled survival curves");
        principals = PrincipalsFromCDX(cdxTerm, survivalCurves.Length);
        GetDatesAndDiscountCurve(survivalCurves, out asOf, out settle, out discountCurve);
      }
      else
      {
        logger.DebugFormat("Use index scaling calibrator");
        discountCurve = indexScalingCalibrator.DiscountCurve;
        asOf = indexScalingCalibrator.AsOf;
        settle = indexScalingCalibrator.Settle;
        survivalCurves = indexScalingCalibrator.GetScaleSurvivalCurves();
        principals = PrincipalsFromCDX(cdxTerm, survivalCurves.Length);
      }

      // Get maturities from either cdx notes or index scaling calculator
      if (maturities == null || maturities.Length == 0)
        maturities = Array.ConvertAll<CDX, Dt>(cdx,
                                               delegate(CDX c) { return c == null ? Dt.Empty : c.Maturity; });
      else if (maturities.Length != cdx.Length)
        throw new ArgumentException("Length of maturities and cdx array must match");
      string[] tenorNames = Array.ConvertAll<CDX, string>(cdx,
                                                          delegate(CDX c) { return c == null ? null : c.Description; });

      // Check the tenors and detachments
      int nTenors = maturities.Length;
      int nDps = dp.Length;

      // Sometimes the dp array may have extra row such as 100% detachment
      if ((strikes.GetLength(0) != nDps && strikes.GetLength(0) != nDps - 1)
          || (baseCorrelations.GetLength(0) != nDps && baseCorrelations.GetLength(0) != nDps - 1))
      {
        throw new System.ArgumentException(
          "Rows of strikes and correlations must match the number of detachment points");
      }

      // Validate tranches and tenors
      if (nTenors < 1)
        throw new System.ArgumentException("Must specify at least one maturity date");
      if (nTenors != quotes.GetLength(1) || nTenors != strikes.GetLength(1) || nTenors != baseCorrelations.GetLength(1))
        throw new System.ArgumentException(String.Format(
          "Number of maturities ({0}) and columns of quotes ({1}), strikes ({2}) or correlations ({3}) not match",
          nTenors, quotes.GetLength(1), strikes.GetLength(1), baseCorrelations.GetLength(1)));
      if (quotes.GetLength(0) != nDps)
        throw new System.ArgumentException(
          "Rows of quotes must match the number of detachment points");

      // Check and set up the running premiums
      runningPrem = SetUpRunningPremiums(runningPrem, nDps, nTenors);

      // Find the effective detachments
      RemoveTrailingZeros(ref dp, quotes);
      nDps = dp.Length;

      // Find the number of effective tenors
      int nRealTenors = FindRealTenors(useTenors, quotes,
                                       maturities, tenorNames, runningPrem, strikes, baseCorrelations);
      if (nRealTenors != maturities.Length)
      {
        // Note: FindRealTenors() has already moved all the effective maturities
        //   to the begining of the maturities array, so we simply truncate it
        //   such that its length reflect the number of effective tenors.
        Array.Resize(ref maturities, nRealTenors);
        Array.Resize(ref tenorNames, nRealTenors);
      }

      // Parameter object
      if (paramsObj == null)
        paramsObj = new BaseCorrelationParam();

      // Create a wrapped correlation
      string[] entityNames = Utils.GetCreditNames(survivalCurves);
      BaseEntity.Toolkit.Base.BaseCorrelation[] bcs = new BaseEntity.Toolkit.Base.BaseCorrelation[nRealTenors];
      for (int col = 0; col < nRealTenors; ++col)
      {
        double[] s = new double[nDps];
        double[] d = new double[nDps];
        double[] c = new double[nDps];
        for (int row = 0; row < nDps; ++row)
        {
          s[row] = row >= strikes.GetLength(0) ? Double.NaN : strikes[row, col];
          c[row] = row >= baseCorrelations.GetLength(0) ? Double.NaN : baseCorrelations[row, col];
          d[row] = dp[row];
        }
        BaseEntity.Toolkit.Base.BaseCorrelation b = new BaseEntity.Toolkit.Base.BaseCorrelation(paramsObj.Method, strikeMethod, s, c);
        b.Detachments = d;
        b.EntityNames = entityNames;
        b.Extended = (paramsObj.Max > 1);
        b.Interp = InterpFactory.FromMethod(paramsObj.StrikeInterp, paramsObj.StrikeExtrap, paramsObj.Min, paramsObj.Max);
        b.Name = maturities[col].ToString();
        bcs[col] = b;
      }

      BaseCorrelationTermStruct bco = new BaseCorrelationTermStruct(maturities, bcs);
      bco.TenorNames = tenorNames;
      bco.Interp = InterpFactory.FromMethod(paramsObj.TenorInterp, paramsObj.TenorExtrap, paramsObj.Min, paramsObj.Max);
      bco.CalibrationMethod = calibrtnMethod;
      bco.MinCorrelation = paramsObj.Min;
      bco.MaxCorrelation = paramsObj.Max;
      bco.EntityNames = entityNames;
      bco.Extended = (paramsObj.Max > 1);
      bco.InterpOnFactors = paramsObj.InterpOnFactors;
      bco.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;

      // Create CDO term object
      SyntheticCDO cdoTerm = new SyntheticCDO(
        cdxTerm.Effective, maturities[nRealTenors - 1], Currency.None, 0.0001,
        cdxTerm.DayCount, cdxTerm.Freq, cdxTerm.BDConvention, cdxTerm.Calendar);

      // Create a Basket
      BasketPricer basket = CreateBasket(asOf, settle,
                                         cdoTerm.Maturity, survivalCurves, principals, paramsObj);

      // Adjust quadrature points based on detachments if the user does not specify it
      if (paramsObj.QuadraturePoints <= 0)
        basket.IntegrationPointsFirst +=
          BasketPricerFactory.DefaultQuadraturePointsAdjust(dp);

      // Check if we need to modify recovery curves
      if (paramsObj.DiscardRecoveryDispersion
          || !Double.IsNaN(paramsObj.OverrideRecoveryRate))
      {
        basket.RecoveryCurves = CloneUtil.Clone(basket.RecoveryCurves);
        // discard the recovery dispersions?
        if (paramsObj.DiscardRecoveryDispersion)
          SuppressRecoveryDispersions(basket.RecoveryCurves);
        // override the recovery rate?
        if (!Double.IsNaN(paramsObj.OverrideRecoveryRate))
          OverrideRecoveryRates(basket.RecoveryCurves,
                                paramsObj.OverrideRecoveryRate, basket.Maturity);
      }

      // Create calibrator
      BaseCorrelationCalibrator cal;
      if (paramsObj.BCMethod == BaseCorrelationMethod.ArbitrageFree)
        cal = new DirectArbitrageFreeCalibrator(calibrtnMethod, cdoTerm, basket,
                                                discountCurve, maturities, dp, runningPrem, quotes,
                                                paramsObj.ToleranceF, paramsObj.ToleranceX, paramsObj.BottomUp);
      else // protection match method
        cal = new DirectLossPvMatchCalibrator(cdoTerm, basket, discountCurve,
                                              maturities, dp, runningPrem, quotes, paramsObj.ToleranceF, paramsObj.ToleranceX);
      if(indexScalingCalibrator != null)
        cal.IndexTerm = indexScalingCalibrator;
      bco.Calibrator=cal;

      return bco;
    }
    #endregion Base Correlation Wrapping

    #region Market Delta
    /// <summary>
    ///  Calculate the market delta of tranches.
    /// </summary>
    /// <param name="bct">Base correlation.</param>
    /// <param name="tenorDates">Array of N dates to get deltas for.</param>
    /// <param name="spreadBump">Credit spread bump size (in basis points).</param>
    /// <param name="rescaleStrikes">If true, recalculate strikes after bumping;
    ///   otherwise, keep detachment/attachment correlation unchanged.</param>
    /// <returns>Market deltas by tenors and detachements.</returns>
    public static double[,] MarketDeltas(
      this BaseCorrelationTermStruct bct,
      Dt[] tenorDates,
      double spreadBump, bool rescaleStrikes)
    {
      // Calculates the deltas by the correlation tenor dates.
      double[,] delta = bct.MarketDeltas(spreadBump, rescaleStrikes);
      if (tenorDates == null || tenorDates.Length == 0)
        return delta;
      // Maps the results to the input tenor dates and fills
      // zeros for those dates not in the correlation tenors.
      int[] map = MapTo(tenorDates, bct.Dates);
      return MapResults(delta, map);
    }

    /// <summary>
    ///  Calculate the market deltas of tranches.
    /// </summary>
    /// <param name="bct">Base correlation.</param>
    /// <param name="spreadBump">Credit spread bump size (in basis points).</param>
    /// <param name="rescaleStrikes">If true, recalculate strikes after bumping;
    ///   otherwise, keep detachment/attachment correlation unchanged.</param>
    /// <returns>Market deltas by tenors and detachements.</returns>
    public static double[,] MarketDeltas(
      this BaseCorrelationTermStruct bct,
      double spreadBump, bool rescaleStrikes)
    {
      // Sanity check.
      var calibrator = bct.Calibrator;
      if (calibrator == null)
        throw new ArgumentException("bct must contain a BaseCorrelationCalibrator.");
      if (calibrator.Basket == null)
        throw new ArgumentException("bct must contain a Basket.");
      if (calibrator.IndexTerm == null)
        throw new ArgumentException("bct must contain an IndexScalingCalibrator.");

      // Make a clone so the original is not affected.
      bct = CloneUtil.CloneObjectGraph(bct);
      calibrator = bct.Calibrator;
      var discountCurve = calibrator.DiscountCurve;
      var basket = calibrator.Basket;
      var totalPrincipal = basket.TotalPrincipal;
      int nTenors = calibrator.TenorDates.Length;
      int nDetachments = calibrator.Detachments.Length;

      // Create CDO pricers
      var cdoPricers = new SyntheticCDOPricer[nTenors][];
      for (int t = 0; t < nTenors; ++t)
        if (calibrator.TrancheQuotes[t] != null)
        {
          var pricers = cdoPricers[t]
                        = new SyntheticCDOPricer[nDetachments];
          for (int d = 0; d < nDetachments; ++d)
          {
            var dp = calibrator.Detachments[d];
            pricers[d] = CreateCDOPricer(
              calibrator.TrancheTerm, basket, rescaleStrikes, bct,
              discountCurve, totalPrincipal, 0.0, dp, calibrator.TenorDates[t]);
          }
        }

      // Create the index pricers
      var cdxPricers = CreateCDXPricers(calibrator.TenorDates,
                                        calibrator.IndexTerm.Indexes, discountCurve, basket);

      // Calculate the base values
      double[,] cdoValues = new double[nDetachments, nTenors];
      double[] cdxValues = new double[nTenors];
      for (int t = 0; t < nTenors; ++t)
        if (cdxPricers[t] != null && cdoPricers[t] != null)
        {
          cdxValues[t] = cdxPricers[t].IntrinsicValue(false);
          var cpt = cdoPricers[t];
          for (int d = 0; d < nDetachments; ++d)
          {
            double fee = 0, prem = 0;
            if (calibrator.GetTranchePremiumAndFee(t, d, ref prem, ref fee))
              cdoValues[d, t] = TrancheValueAt(d, prem, fee, cpt);
          }
        }

      // Bump the survival curves
      if (spreadBump == 0) spreadBump = 1;
      basket.OriginalBasket.SurvivalCurves.BumpQuotes(
        QuotingConvention.CreditSpread, spreadBump, BumpFlags.RefitCurve);

      // Reset pricers
      for (int t = 0; t < nTenors; ++t)
        if (cdxPricers[t] != null && cdoPricers[t] != null)
        {
          cdxPricers[t].Reset();
          for (int d = 0; d < nDetachments; ++d)
            if (cdoPricers[t][d] != null)
              cdoPricers[t][d].Basket.Reset();
        }

      // Calculate the deltas
      double[,] result = new double[nDetachments, nTenors];
      for (int t = 0; t < nTenors; ++t)
        if (cdxPricers[t] != null && calibrator.TrancheQuotes[t] != null)
        {
          double cdxDelta = cdxPricers[t].IntrinsicValue(false) - cdxValues[t];
          cdxDelta /= cdxPricers[t].Notional;
          var cpt = cdoPricers[t];
          double baseNotional = 0;
          for (int d = 0; d < nDetachments; ++d)
          {
            double fee = 0, prem = 0;
            if (calibrator.GetTranchePremiumAndFee(t, d, ref prem, ref fee))
            {
              double cdoDelta = TrancheValueAt(
                d, prem, fee, cpt) - cdoValues[d, t];
              double notional = cpt[d].Notional;
              double cdoNotional = notional - baseNotional;
              if (Math.Abs(cdoNotional) > 1E-8)
                result[d, t] = cdoDelta / cdoNotional / cdxDelta;
              baseNotional = notional;
            }
          }
        }

      return result;
    }

    /// <summary>
    ///  Calculates the equivalent spreads.
    /// </summary>
    /// <param name="bct">Base correlation term struct..</param>
    /// <param name="tenorDates">Array of N dates to calculate spreads for.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>Conventional spreads in basis points.</returns>
    public static double[,] EquivalentSpreads(
      this BaseCorrelationTermStruct bct,
      Dt[] tenorDates,
      DiscountCurve discountCurve)
    {
      double[,] spreads = bct.EquivalentSpreads(discountCurve);
      if (tenorDates == null || tenorDates.Length == 0)
        return spreads;
      // Maps the results to the input tenor dates and fills
      // zeros for those dates not in the correlation tenors.
      int[] map = MapTo(tenorDates, bct.Dates);
      return MapResults(spreads, map);
    }

    /// <summary>
    ///  Calculates the equivalent spreads.
    /// </summary>
    /// <param name="bct">Base correlation term struct..</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>Conventional spreads in basis points.</returns>
    public static double[,] EquivalentSpreads(
      this BaseCorrelationTermStruct bct,
      DiscountCurve discountCurve)
    {
      // Sanity check.
      var calibrator = bct.Calibrator;
      if (calibrator == null)
        throw new ArgumentException("bct must contain a BaseCorrelationCalibrator.");
      if (calibrator.Basket == null)
        throw new ArgumentException("bct must contain a Basket.");

      // Make a clone so the original is not affected.
      bct = CloneUtil.CloneObjectGraph(bct);
      calibrator = bct.Calibrator;
      if (discountCurve == null)
        discountCurve = calibrator.DiscountCurve;
      var basket = calibrator.Basket;
      int nTenors = calibrator.TenorDates.Length;
      int nDetachments = calibrator.Detachments.Length;

      // Calculate the break even spreads.
      double[,] result = new double[nDetachments, nTenors];
      for (int t = 0; t < nTenors; ++t)
        if (calibrator.TrancheQuotes[t] != null)
        {
          for (int d = 0; d < nDetachments; ++d)
          {
            double fee = 0, prem = 0;
            if (!calibrator.GetTranchePremiumAndFee(t, d, ref prem, ref fee))
              continue;
            // If fee is zero, just use the input premium.
            if (fee == 0.0)
            {
              result[d, t] = prem * 10000.0;
              continue;
            }
            // We have the upfront quote, convert it to conventional spread.
            double recovery = calibrator.Detachments[d] < 0.30000001 ? 0.0 : 0.40;
            var res = BaseEntity.Toolkit.Models.ISDACDSModel.SNACCDSConverter.FromUpfront(
              basket.AsOf, calibrator.TenorDates[t], discountCurve,
              prem, fee, recovery, 1.0);
            result[d, t] = res.ConventionalSpread;
          }
        }

      // return the results
      return result;
    }

    private static double[,] MapResults(double[,] delta, int[] map)
    {
      int rows = delta.GetLength(0);
      double[,] result = new double[rows, map.Length];
      for (int i = 0; i < map.Length; ++i)
        if (map[i] >= 0)
        {
          int c = map[i];
          for (int r = 0; r < rows; ++r)
            result[r, i] = delta[r, c];
        }
      return result;
    }
    private static int[] MapTo(Dt[] fromDates, Dt[] toDates)
    {
      if (fromDates == null) return null;
      if (fromDates.Length == 0) return new int[0];
      int[] map = ArrayUtil.NewArray(fromDates.Length, -1);
      if (toDates == null || toDates.Length == 0) return map;
      for (int i = 0, j = 0; i < fromDates.Length && j < toDates.Length; )
      {
        if (fromDates[i] < toDates[j]) { ++i; continue; }
        if (fromDates[i] > toDates[j]) { ++j; continue; }
        map[i] = j;
        ++j; ++i;
      }
      return map;
    }
    private static ICDXPricer[] CreateCDXPricers(
      Dt[] dates, CDX[] cdxs,
      DiscountCurve discountCurve, BasketPricer basket)
    {
      var cdxPricers = new ICDXPricer[dates.Length];
      for (int t = 0, i = 0; t < cdxs.Length && i < dates.Length; ++t)
      {
        var cdx = cdxs[t];
        if (cdx == null || cdx.Maturity < dates[i]) continue;
        if (cdx.Maturity != dates[i])
        {
          logger.DebugFormat("Index maturity {0} and tranche maturity {1} not match.",
                            cdx.Maturity, dates[i]);
          ++i;
          continue;
        }
        cdxPricers[i++] = CDXPricerUtil.CreateCdxPricer(cdx,
                                                        basket.AsOf, basket.Settle, discountCurve,
                                                        basket.OriginalBasket.SurvivalCurves);
      }
      return cdxPricers;
    }
    private static double TrancheValueAt(int d, 
                                         double prem, double fee, SyntheticCDOPricer[] pricers)
    {
      var pricer = pricers[d];
      if (Math.Abs(pricer.EffectiveNotional) < 1E-8)
        return 0;
      pricer.CDO.Premium = prem;
      pricer.CDO.Fee = fee;
      double pv = pricer.ProductPv();
      if (d > 0)
      {
        pricer = pricers[d - 1];
        pricer.CDO.Premium = prem;
        pricer.CDO.Fee = fee;
        pv -= pricer.ProductPv();
      }
      return pv;
    }
    private static SyntheticCDOPricer CreateCDOPricer(
      SyntheticCDO cdo, BasketPricer basket, bool rescaleStrikes,
      BaseCorrelationTermStruct bct,
      DiscountCurve discountCurve, double totalPrincipal,
      double ap, double dp, Dt maturity)
    {
      cdo = (SyntheticCDO)cdo.ShallowCopy();
      cdo.Maturity = maturity;
      cdo.Detachment = dp;
      cdo.Attachment = 0;
      cdo.Premium = 1.0;
      cdo.Fee = 0.0;
      cdo.FeeSettle = Dt.Add((cdo.Effective > basket.Settle)
                               ? cdo.Effective : basket.Settle, 1);
      var bcbskt = new BaseCorrelationBasketPricer(
        basket, discountCurve, bct, rescaleStrikes, ap, dp);
      bcbskt.Maturity = maturity;
      bcbskt.RawLossLevels = new UniqueSequence<double>(ap, dp);
      if (dp + bcbskt.MaximumAmortizationLevel() <= 1.0)
        bcbskt.NoAmortization = true;
      var pricer = new SyntheticCDOPricer(cdo,
                                          bcbskt, discountCurve, totalPrincipal * dp);
      return pricer;
    }
    #endregion Market Delta

    #region Helpers

    ///  <param name="survivalCurves">Survival curves</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeExtrap">Strike extrapolation method</param>
    /// <param name="principals">Original principals</param>
    /// <param name="paramsObj">Base correlation parameter object</param>
    /// <param name="baseCorrelation">Original base correlation</param>
    /// <param name="strikeInterp">Strike interpolation method</param>
    /// <returns>Created base correlation surface</returns>
    public static BaseCorrelationObject BaseCorrelationScale(BaseCorrelationObject baseCorrelation, SurvivalCurve[] survivalCurves,
      BaseCorrelationStrikeMethod strikeMethod, InterpMethod strikeInterp, ExtrapMethod strikeExtrap, double[] principals, BaseCorrelationParam paramsObj)
    {
      var corr = baseCorrelation as BaseCorrelationTermStruct;
      if (corr == null)
         throw new ArgumentException("Base correlation object is required to be BaseCorrelationTermStruct type for scaling");

      if (corr.BaseCorrelations.All(bc => bc.StrikeMethod == strikeMethod && bc.InterpMethod == strikeInterp && bc.ExtrapMethod == strikeExtrap))
        return baseCorrelation;

      var mappedStrikes = CalculateStrikes(corr, strikeMethod, survivalCurves, principals, paramsObj);
      int nTenors = corr.Dates.Length;
      bool extended = (paramsObj.Max > 1);
      var bcs = new BaseEntity.Toolkit.Base.BaseCorrelation[nTenors];
      for (int col = 0; col < nTenors; ++col)
      {
        var baseCorrObj = corr.BaseCorrelations[col];
        int nCdos = baseCorrObj.Detachments.Length;

        // Use this column
        var d = new double[nCdos];
        var c = new double[nCdos];
        bool failed = false;
        string errMsg = string.Empty;
        var recalculatedStrikes = new double[nCdos];

        for (int row = 0; row < nCdos; row++)
        {
          var sij = baseCorrObj.Correlations[row];
          if (baseCorrObj.CalibrationFailed)
          {
            failed = true;
            errMsg = baseCorrObj.ErrorMessage;
          }

          if (sij <= 0.0 && !Double.IsNaN(sij))
            break;
          c[row] = baseCorrObj.Correlations[row];
          d[row] = baseCorrObj.Detachments[row];
          recalculatedStrikes[row] = mappedStrikes[row, col];
        }

        var b = new BaseEntity.Toolkit.Base.BaseCorrelation(paramsObj.Method, strikeMethod, recalculatedStrikes, c)
        {
          Detachments = d,
          EntityNames = corr.EntityNames,
          MinCorrelation = paramsObj.Min,
          MaxCorrelation = paramsObj.Max,
          Extended = extended,
          Interp = InterpFactory.FromMethod(
            paramsObj.StrikeInterp, paramsObj.StrikeExtrap, paramsObj.Min, paramsObj.Max),
          Name = baseCorrObj.Name,
          CalibrationFailed = failed,
          ErrorMessage = errMsg
        };

        bcs[col] = b;
      }

      var bco = BaseCorrelationTermStruct.Create(paramsObj, corr.CalibrationMethod, corr.Dates, bcs, corr.TenorNames, corr.EntityNames);
      return bco;
    }

    /// <summary>
    /// Recalculate the strikes based on newly specified mapping method
    /// </summary>
    /// <param name="bct">Original base correlation</param>
    /// <param name="mapping">Customized mapping method</param>
    /// <param name="survivalCurves">Survival curves</param>
    /// <param name="paramsObj">Parameters</param>
    /// <param name="principals">Original principals</param>
    /// <returns>New strikes</returns>
    public static double[,] CalculateStrikes(BaseCorrelationTermStruct bct, BaseCorrelationStrikeMethod mapping, SurvivalCurve[] survivalCurves, double[] principals, BaseCorrelationParam paramsObj)
    {
      // Retrieve portfolio information
      Dt asOf, settle;
      DiscountCurve discountCurve;
      if (survivalCurves != null && survivalCurves.Length != 0)
      {
        if (principals == null || principals.Length ==0)
        {
          principals = ArrayUtil.NewArray(survivalCurves.Length, 1000000.0);
        }

        GetDatesAndDiscountCurve(survivalCurves, out asOf, out settle, out discountCurve);
      }
      else
      {
        throw new ArgumentException("Survival curves cannot be null or empty");
      }
      var maturities = bct.Dates;
      int strikeCount = bct.BaseCorrelations.Max(b => b.Correlations.Length);
      var strikes = new double[strikeCount, maturities.Length];
      var tenorCount = bct.Dates.Length;
      Dt maturity = bct.Dates.Max();
      var basketPricer = CreateBasket(asOf, settle, maturity, survivalCurves, principals, paramsObj);

      for (int t = 0; t < tenorCount; ++t)
      {
        maturity = bct.Dates[t];
        var j = Array.IndexOf(maturities, maturity);
        if (j < 0) continue;

        var corrs = bct.BaseCorrelations[t].Correlations;
        var dps = bct.BaseCorrelations[t].Detachments;

        for (int i = 0; i < dps.Length; ++i)
        {
          if (Double.IsNaN(corrs[i]))
          {
            strikes[i, j] = Double.NaN;
            continue;
          }

          var dp = dps[i];
          var cdo = new SyntheticCDO(asOf, maturity, Currency.None,
            0, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
          {
            Detachment = dp,
          };

          var basket = basketPricer.Substitute(basketPricer.OriginalBasket,
            new SingleFactorCorrelation(Array.ConvertAll(
              basketPricer.OriginalBasket.SurvivalCurves, c => c.Name), 0),
            new[] { 0, dp });
          basket.Maturity = maturity;

          var pricer = new SyntheticCDOPricer(cdo, basket,
            discountCurve, null, cdo.TrancheWidth * 1000000, null);
          strikes[i, j] = BaseEntity.Toolkit.Base.BaseCorrelation.Strike(
            pricer, mapping, null, corrs[i]);
        }
      }
      return strikes;
    }

    /// <summary>
    ///   Create an basket pricer for base correlation calibration
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="paramsObj">BaseCorrelationParams object</param>
    /// <returns>Bakset pricer</returns>
    /// <exclude/>
    public static BasketPricer CreateBasket(
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      BaseCorrelationParam paramsObj)
    {
      // Round notionals and principals
      principals = Utils.Round(principals, 9);

      // Setup arguments
      //SetupArgs(survivalCurves, principals, out sc, out rc, out prins);

      // Get correlation
      CorrelationObject corr = new SingleFactorCorrelation(
        Utils.GetCreditNames(survivalCurves), 0.0);

      // Create copula
      Copula copula = paramsObj.Copula;

      // Create basket pricers
      Dt portfolioStart = new Dt();
      double[,] lossLevels = new double[1, 2];
      lossLevels[0, 0] = 0.0;
      lossLevels[0, 1] = 1.0;

      bool asLCDXBasket = false;
      BasketPricer basket;
      switch (paramsObj.ModelType)
      {
        case BasketModelType.LargePool:
          basket = BasketPricerFactory.LargePoolBasketPricer(portfolioStart,
                                                             asOf, settle, maturity, survivalCurves, principals, copula, corr,
                                                             paramsObj.StepSize, paramsObj.StepUnit, lossLevels,
                                                             paramsObj.QuadraturePoints);
          break;
        case BasketModelType.Uniform:
          basket = BasketPricerFactory.UniformBasketPricer(portfolioStart,
                                                           asOf, settle, maturity, survivalCurves, principals, copula, corr,
                                                           paramsObj.StepSize, paramsObj.StepUnit, lossLevels,
                                                           paramsObj.QuadraturePoints);
          break;
        case BasketModelType.MonteCarlo:
          basket = BasketPricerFactory.MonteCarloBasketPricer(portfolioStart,
                                                              asOf, settle, maturity, survivalCurves, principals, copula, corr,
                                                              paramsObj.StepSize, paramsObj.StepUnit, lossLevels,
                                                              paramsObj.SampleSize, paramsObj.Seed);
          break;
        case BasketModelType.SemiAnalytic:
        case BasketModelType.LCDOCommonSignal:
        case BasketModelType.LCDOProportional:
          asLCDXBasket = true;
          basket = BasketPricerFactory.SemiAnalyticBasketPricer(portfolioStart,
                                                                asOf, settle, maturity, survivalCurves, principals, copula, corr,
                                                                paramsObj.StepSize, paramsObj.StepUnit, lossLevels,
                                                                paramsObj.QuadraturePoints, paramsObj.GridSize, true);
          basket.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;
          break;
        default:
          basket = BasketPricerFactory.SemiAnalyticBasketPricer(portfolioStart,
                                                                asOf, settle, maturity, survivalCurves, principals, copula, corr,
                                                                paramsObj.StepSize, paramsObj.StepUnit, lossLevels,
                                                                paramsObj.QuadraturePoints, paramsObj.GridSize, false);
          basket.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;
          if (paramsObj.BasketModel != null)
            basket = paramsObj.BasketModel.CreateBasket(basket, paramsObj);
          break;
      }
      basket.AccuracyLevel = paramsObj.AccuracyLevel;
      basket.OriginalBasket = new CreditPool(principals, survivalCurves, asLCDXBasket, null);
      return basket;
    }

    /// <summary>
    /// Setup arguments for baskets 
    /// </summary>
    /// <param name="survivalCurves">Array of survival curves</param>
    /// <param name="principals">Array of principals</param>
    /// <param name="sc">Out array of survival curves</param>
    /// <param name="rc">Out array of recovery curves</param>
    /// <param name="prins">Out array of principals</param>
    public static void SetupArgs(
      SurvivalCurve[] survivalCurves,
      double[] principals,
      out SurvivalCurve[] sc,
      out RecoveryCurve[] rc,
      out double[] prins
      )
    {
      // Argument validation
      if (survivalCurves.Length < 1)
        throw new System.ArgumentException("Must specify at least one survival curve");
      if (principals.Length > 1 && principals.Length != survivalCurves.Length)
        throw new System.ArgumentException("Number of principals must match number of survival curves");

      // Set up number of curves we are interested in
      int nCurves = 0;
      for (int i = 0; i < survivalCurves.Length; i++)
        if ((survivalCurves[i] != null) && (principals.Length <= 1 || principals[i] != 0.0))
          nCurves++;

      // Set up arguments for basket pricer, ignoring notionals that are zero.
      prins = new double[nCurves];
      sc = new SurvivalCurve[nCurves];
      rc = new RecoveryCurve[nCurves];

      for (int i = 0, idx = 0; i < survivalCurves.Length; i++)
      {
        if ((survivalCurves[i] != null) && (principals.Length <= 1 || principals[i] != 0.0))
        {
          prins[idx] = (principals.Length > 1) ? principals[i] : ((principals.Length > 0) ? principals[0] : 1000000.0);
          sc[idx] = survivalCurves[i];
          if (sc[idx].Calibrator != null)
            rc[idx] = sc[idx].SurvivalCalibrator.RecoveryCurve;
          else
            throw new System.ArgumentException(String.Format("Must specify recoveries as curve {0} does not have recoveries from calibration", sc[idx].Name));
          idx++;
        }
      }
      Utils.ScaleUp(prins, 1000000.0);

      return;
    }

    /// <summary>
    ///   Set up a 2D array with excact size:
    ///   rows = detachments, and columns = tenors.
    /// </summary>
    /// <param name="inputPremiums"></param>
    /// <param name="dps"></param>
    /// <param name="tenors"></param>
    /// <returns></returns>
    public static double[,] SetUpRunningPremiums(
      double[,] inputPremiums, int dps, int tenors)
    {
      // Empty array: 0 running premium
      if (inputPremiums == null || inputPremiums.Length == 0)
        return ArrayUtil.CreateMatrixFromSingleValue(0.0, dps, tenors);

      // A single number: one running premium applicable to all
      if (inputPremiums.Length == 1)
        return ArrayUtil.CreateMatrixFromSingleValue(inputPremiums[0, 0], dps, tenors);

      // OK if the dimensitions match the detachments and tenors
      int rows = inputPremiums.GetLength(0);
      int cols = inputPremiums.GetLength(1);
      if (rows == dps && cols == tenors)
        return inputPremiums;

      // A single row: premiums by tenors applicable to all detachments
      if (rows == 1 && cols == tenors)
      {
        double[,] result = new double[dps, tenors];
        for (int j = 0; j < tenors; ++j)
        {
          for (int i = 0; i < dps; ++i)
            result[i, j] = inputPremiums[0, j];
        }
        return result;
      }

      // A single column: premiums by detachments applicable to all tenors
      if (rows == dps && cols == 1)
      {
        double[,] result = new double[dps, tenors];
        for (int i = 0; i < dps; ++i)
        {
          for (int j = 0; j < tenors; ++j)
            result[i, j] = inputPremiums[i, 0];
        }
        return result;
      }

      throw new ArgumentException(String.Format(
        "runningPremiums ({0} x {1}) not match detachments (len={2}) and/or tenors (len={3})",
        rows, cols, dps, tenors));
    }

    /// <summary>
    ///    Find real tenors and move the quotes to the begining of the arrays
    /// </summary>
    /// <remarks>
    ///   On output, all the effective maturity dates, and correponding quotes
    ///   and running premiums are moved to the begining of the arrays.
    /// </remarks>
    /// <param name="useTenors"></param>
    /// <param name="quotes"></param>
    /// <param name="maturities"></param>
    /// <param name="tenorNames"></param>
    /// <param name="runningPrem"></param>
    /// <returns></returns>
    private static int FindRealTenors(
      bool[] useTenors, double[,] quotes,
      Dt[] maturities, string[] tenorNames,
      double[,] runningPrem)
    {
      int nTenors = maturities.Length;
      if (useTenors == null || useTenors.Length == 0)
      {
        useTenors = new bool[nTenors];
        for (int i = 0; i < nTenors; ++i)
        {
          // Check if at least one quote for current i'th tenor is nonzero   
          bool quoteNonZero = false;
          for (int j = 0; j < quotes.GetLength(0); j++)
            quoteNonZero = quoteNonZero || (!Double.IsNaN(quotes[j, i]) && quotes[j, i] != 0.0);
          useTenors[i] = quoteNonZero && !maturities[i].IsEmpty();
        }
      }
      else if (useTenors.Length != nTenors)
        throw new System.ArgumentException("Number of useTenor flags must match number of maturities");

      // find the number of effective tenors
      int nCDOs = quotes.GetLength(0);
      int nRealTenors = 0;
      for (int j = 0; j < nTenors; ++j)
        if (useTenors[j] && !maturities[j].IsEmpty())
        {
          if (j != nRealTenors)
          {
            maturities[nRealTenors] = maturities[j];
            tenorNames[nRealTenors] = tenorNames[j];
            for (int i = 0; i < nCDOs; ++i)
              quotes[i, nRealTenors] = quotes[i, j];
            for (int i = 0; i < nCDOs; ++i)
              runningPrem[i, nRealTenors] = runningPrem[i, j];
          }
          ++nRealTenors;
        }
      return nRealTenors;
    }

    /// <summary>
    ///    Find real tenors and move the quotes to the begining of the arrays
    /// </summary>
    /// <remarks>
    ///   On output, all the effective maturity dates, and correponding quotes
    ///   and running premiums are moved to the begining of the arrays.
    /// </remarks>
    /// <param name="useTenors">Array of boolean</param>
    /// <param name="quotes">2-D array of quotes</param>
    /// <param name="maturities">Array of maturities</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="runningPrem">Array of running premium</param>
    /// <param name="strikes">2-D array of strikes</param>
    /// <param name="corrs">2-D array of correlations</param>
    /// <returns>Number of real tenors</returns>
    private static int FindRealTenors(
      bool[] useTenors, double[,] quotes,
      Dt[] maturities, string[] tenorNames,
      double[,] runningPrem, double[,] strikes, double[,] corrs)
    {
      int nTenors = maturities.Length;
      if (useTenors == null || useTenors.Length == 0)
      {
        useTenors = new bool[nTenors];
        for (int i = 0; i < nTenors; ++i)
          useTenors[i] = quotes[0, i] > 0 && !maturities[i].IsEmpty();
      }
      else if (useTenors.Length != nTenors)
        throw new System.ArgumentException("Number of useTenor flags must match number of maturities");

      // find the number of effective tenors
      int nCDOs = quotes.GetLength(0);
      int nStrikes = strikes.GetLength(0);
      int nRealTenors = 0;
      for (int j = 0; j < nTenors; ++j)
        if (useTenors[j] && !maturities[j].IsEmpty())
        {
          if (j != nRealTenors)
          {
            maturities[nRealTenors] = maturities[j];
            tenorNames[nRealTenors] = tenorNames[j];
            for (int i = 0; i < nStrikes; ++i)
            {
              runningPrem[i, nRealTenors] = runningPrem[i, j];
              quotes[i, nRealTenors] = quotes[i, j];
              strikes[i, nRealTenors] = strikes[i, j];
              corrs[i, nRealTenors] = corrs[i, j];
            }
            if (nStrikes < nCDOs)
            {
              // Quotes may have extra row (i.g., 100% dp) than strikes
              for (int i = nStrikes; i < nCDOs; ++i)
              {
                quotes[i, nRealTenors] = quotes[i, j];
                runningPrem[i, nRealTenors] = runningPrem[i, j];
              }
            }
          } // end if(j != nRealTenors)
          ++nRealTenors;
        }
      return nRealTenors;
    }

    private static int RemoveTrailingZeros(ref double[] dp, double[,] quotes)
    {
      // Ignore missing detachment points
      int nZeros = 0;
      int nDps = dp.Length;
      for (int i = nDps - 1; i >= 0; --i)
      {
        if (dp[i] <= 0.0 || IsEmptyRow(quotes, i))
          ++nZeros;
        else
          break;
      }

      if (nDps <= nZeros)
        throw new System.ArgumentException("Must specify at least one detachment point");
      else if (nZeros > 0)
        Array.Resize(ref dp, nDps - nZeros);

      return nDps - nZeros;
    }

    private static bool IsEmptyRow(double[,] quotes, int i)
    {
      int cols = quotes.GetLength(1);
      for (int j = 0; j < cols; ++j)
      {
        if (!Double.IsNaN(quotes[i, j])) return false;
      }
      return true;
    }

    /// <summary>
    ///  Set recovery dispersion for all recovery curves to be 0
    /// </summary>
    /// <param name="recoveryCurves">Array of recovery curves</param>
    public static void SuppressRecoveryDispersions(RecoveryCurve[] recoveryCurves)
    {
      foreach (RecoveryCurve rc in recoveryCurves)
        rc.RecoveryDispersion = 0.0;
      return;
    }

    /// <summary>
    ///  Override recovery rates for all recovery curves
    /// </summary>
    /// <param name="recoveryCurves">Array of recovery curves</param>
    /// <param name="rate">Recovery rate</param>
    /// <param name="maturity">Maturity</param>
    public static void OverrideRecoveryRates(
      RecoveryCurve[] recoveryCurves, double rate, Dt maturity)
    {
      foreach (RecoveryCurve rc in recoveryCurves)
        rc.Spread += rate - rc.RecoveryRate(maturity);
      return;
    }

    private static double[] PrincipalsFromCDX(CDX cdx, int length)
    {
      double[] prins = cdx.Weights;
      if (prins == null || prins.Length == 0)
        prins = ArrayUtil.NewArray(length, 1000000.0);
      else if (prins.Length == 1)
      {
        prins = ArrayUtil.NewArray(length, prins[0]);
        Utils.ScaleUp(prins, 1000000.0);
      }
      else if (prins.Length == length)
      {
        prins = CloneUtil.Clone(prins);
        Utils.ScaleUp(prins, 1000000.0);
      }
      else
        throw new ArgumentException("Index weights and survival curves not match");
      return prins;
    }

    private static void GetDatesAndDiscountCurve(SurvivalCurve[] sc,
                                                 out Dt asOf, out Dt settle, out DiscountCurve discountCurve)
    {
      for(int i = 0; i < sc.Length;++i)
        if (sc[i] != null)
        {
          discountCurve = sc[i].SurvivalCalibrator.DiscountCurve;
          asOf = sc[i].AsOf;
          settle = Dt.Add(asOf, 1, TimeUnit.Days);
          return;
        }
      throw new ArgumentException("All survival curves are null.");
    }
    #endregion Helpers

  }
}