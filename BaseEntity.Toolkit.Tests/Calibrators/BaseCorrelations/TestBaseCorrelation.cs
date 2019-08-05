//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;


using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Calibrators.BaseCorrelations
{
  [TestFixture("TestBaseCorrelation CDX.NA.HY.7.V1", Category = "Smoke")]
  [TestFixture("TestBaseCorrelation CDX.NA.IG.7.V1")]
  public class TestBaseCorrelation : ToolkitTestBase
  {

    public TestBaseCorrelation(string name) : base(name) 
    {}

    #region SetUp
    [OneTimeSetUp]
    public void Initialize()
    {
      // Load discount curve
      discountCurve_ = LoadDiscountCurve(LiborDataFile);
      asOf_ = discountCurve_.AsOf;

      // Load tranche quotes
      trancheData_ = LoadTrancheQuotes(TrancheDataFile);
      cdos_ = trancheData_.ToCDOs();
      if (cdos_ == null)
        throw new System.Exception(TrancheDataFile + ": Invalid tranche quotes data");
      settle_ = cdos_[0][0].Effective;
      if (Dt.Cmp(settle_, asOf_) < 0)
        settle_ = Dt.Add(asOf_, 1);

      // Change fee settle to include fee
      foreach (SyntheticCDO[] cdos in cdos_)
        foreach (SyntheticCDO cdo in cdos)
          if (cdo.Fee != 0.0)
            cdo.FeeSettle = Dt.Add(settle_, 1);

      // Load index data
      BasketData.Index indexData = null;
      if (IndexDataFile != null)
      {
        indexData = (BasketData.Index)XmlLoadData(
          GetTestFilePath(IndexDataFile), typeof(BasketData.Index));
      }

      // Load credit Curves
      survivalCurves_ = LoadCreditCurves(CreditDataFile, discountCurve_,
        indexData == null ? null : indexData.CreditNames);

      // Check if we need to scale curves
      if (indexData != null && indexData.Quotes != null)
      {
        scalingFactors_ = indexData.ScalingFactors(asOf_, settle_, discountCurve_, survivalCurves_);
        survivalCurves_ = indexData.ScaleCurves(survivalCurves_, scalingFactors_);
      }

      // Recovery curves and principals
      recoveryCurves_ = new RecoveryCurve[survivalCurves_.Length];
      double[] prins = new double[survivalCurves_.Length];
      for (int i = 0; i < survivalCurves_.Length; ++i)
      {
        prins[i] = 1.0E9 / survivalCurves_.Length;
        if (survivalCurves_[i].Calibrator != null)
          recoveryCurves_[i] = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
      }
      int nTenors = cdos_.Length;
      principals_ = new double[nTenors][];
      for (int j = 0; j < cdos_.Length; ++j)
        principals_[j] = prins;

      // Load BaseCorrelation Params
      baseCorrParams_ = (BasketData.BaseCorrelationParam)XmlLoadData(
        GetTestFilePath(BaseCorrelationParamFile),
        typeof(BasketData.BaseCorrelationParam));
      if (StrikeMethod != BaseCorrelationStrikeMethod.Unscaled)
        baseCorrParams_.StrikeMethod = StrikeMethod;

      // Load copula
      copula_ = LoadCopula(CopulaData);
      if (copula_ == null)
        copula_ = new Copula(baseCorrParams_.CopulaType,
          baseCorrParams_.DfCommon, baseCorrParams_.DfIdiosyncratic);
      return;
    }

    #endregion // SetUp

    #region Tests
    [Test, Smoke]
    public void CalibrateTermStruct()
    {
      baseCorrParams_.CalibrationMethod = BaseCorrelationCalibrationMethod.TermStructure;
      baseCorrParams_.Method = BaseCorrelationMethod.ArbitrageFree;
      Calibrate();
    }

    [Test, Smoke]
    public void CalibrateArbitrageFree()
    {
      baseCorrParams_.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;
      baseCorrParams_.Method = BaseCorrelationMethod.ArbitrageFree;
      Calibrate();
    }

    [Test, Smoke]
    public void CalibrateProtectionMatching()
    {
      baseCorrParams_.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;
      baseCorrParams_.Method = BaseCorrelationMethod.ProtectionMatching;
      Calibrate();
    }

    [Test, Smoke]
    public void DirectFitMaturityMatch()
    {
      baseCorrParams_.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;
      baseCorrParams_.Method = BaseCorrelationMethod.ArbitrageFree;
      CalibrateMaturityMatch(true);
    }

    [Test, Smoke]
    public void DirectFitTermStructure()
    {
      baseCorrParams_.CalibrationMethod = BaseCorrelationCalibrationMethod.TermStructure;
      baseCorrParams_.Method = BaseCorrelationMethod.ArbitrageFree;
      CalibrateMaturityMatch(true);
    }

    [Test, Smoke]
    public void BaseCorrelationCombine()
    {
      Toolkit.Base.BasketData.CorrelationArray corrs
        = (Toolkit.Base.BasketData.CorrelationArray)XmlLoadData(
        GetTestFilePath(baseCorrDataFile_),
        typeof(Toolkit.Base.BasketData.CorrelationArray));
      int N = corrs.Data.Length;
      BaseCorrelationObject[] bcObjs = new BaseCorrelationObject[N];
      for (int i = 0; i < N; ++i)
        bcObjs[i] = BasketData.GetBaseCorrelation(corrs.Data[i]);

      Timer timer = new Timer();
      timer.Start();
      BaseCorrelationObject bco =
        CorrelationFactory.CreateCombinedBaseCorrelation(bcObjs, corrs.Weights,
        corrs.Params.StrikeInterp, corrs.Params.StrikeExtrap,
        corrs.Params.TenorInterp, corrs.Params.TenorExtrap,
        corrs.Params.Min, corrs.Params.Max);
      timer.Stop();
      ResultData rd = ToResultData(bco);
      rd.TimeUsed = timer.Elapsed;
      MatchExpects(rd);
    }
    #endregion // Tests

    #region Helpers
    public void Calibrate()
    {
      BaseCorrelationTermStruct bco = null;
      Timer timer = new Timer();
      timer.Start();
      int quadraturePoints = baseCorrParams_.QuadraturePoints > 0 ?
        baseCorrParams_.QuadraturePoints :
        BasketPricerFactory.DefaultQuadraturePoints(copula_, survivalCurves_.Length);

      // Create basket pricers
      Dt portfolioStart = new Dt();
      Dt maturity = cdos_[cdos_.Length - 1][0].Maturity;
      CorrelationObject corr = new SingleFactorCorrelation(new string[survivalCurves_.Length], 0.0);
      double[,] lossLevels = new double[1, 2];
      lossLevels[0, 0] = 0.0;
      lossLevels[0, 1] = 1.0;

      BasketPricer basket;
      switch (baseCorrParams_.ModelType)
      {
        case BasketData.BaseCorrelationParam.BasketType.LargePool:
          basket = BasketPricerFactory.LargePoolBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints);
          break;
        case BasketData.BaseCorrelationParam.BasketType.Uniform:
          basket = BasketPricerFactory.UniformBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints);
          break;
        case BasketData.BaseCorrelationParam.BasketType.MonteCarlo:
          basket = BasketPricerFactory.MonteCarloBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            baseCorrParams_.SampleSize, baseCorrParams_.Seed);
          break;
        case BasketData.BaseCorrelationParam.BasketType.SemiAnalytic:
          basket = BasketPricerFactory.SemiAnalyticBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints, baseCorrParams_.GridSize, true);
          break;
        default:
          basket = BasketPricerFactory.SemiAnalyticBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints, baseCorrParams_.GridSize, false);
          break;
      }
      bco = new BaseCorrelationTermStruct(
          baseCorrParams_.Method,
          baseCorrParams_.StrikeMethod, null,
          baseCorrParams_.CalibrationMethod,
          cdos_, basket, principals_, discountCurve_,
          baseCorrParams_.ToleranceF, baseCorrParams_.ToleranceX,
          baseCorrParams_.StrikeInterp, baseCorrParams_.StrikeExtrap,
          baseCorrParams_.TenorInterp, baseCorrParams_.TenorExtrap,
          baseCorrParams_.Min, baseCorrParams_.Max);
      timer.Stop();
      ResultData rd = ToResultData(bco);
      rd.TimeUsed = timer.Elapsed;

      if (bco != null && consistencyCheck_
        && baseCorrParams_.Method != BaseCorrelationMethod.ProtectionMatching)
      {
        RountTripCheck(bco, true);
        //- Protection matching is known to fail round robin
        if (StrikeMethodsToCheck != null)
        {
          foreach (BaseCorrelationStrikeMethod sm in StrikeMethodsToCheck)
          {
            CalculateStrikes(basket, bco, sm);
            RountTripCheck(bco, true);
          }
        }
        // end round trip
      }

      MatchExpects(rd);
    }

    public void CalibrateMaturityMatch(bool direct)
    {
      BaseCorrelationTermStruct bco = null;
      Timer timer = new Timer();
      timer.Start();
      int quadraturePoints = baseCorrParams_.QuadraturePoints > 0 ?
        baseCorrParams_.QuadraturePoints :
        BasketPricerFactory.DefaultQuadraturePoints(copula_, survivalCurves_.Length);

      // Create basket pricers
      Dt portfolioStart = new Dt();
      Dt maturity = cdos_[cdos_.Length - 1][0].Maturity;
      CorrelationObject corr = new SingleFactorCorrelation(new string[survivalCurves_.Length], 0.0);
      double[,] lossLevels = new double[1, 2];
      lossLevels[0, 0] = 0.0;
      lossLevels[0, 1] = 1.0;

      BasketPricer basket;
      switch (baseCorrParams_.ModelType)
      {
        case BasketData.BaseCorrelationParam.BasketType.LargePool:
          basket = BasketPricerFactory.LargePoolBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints);
          break;
        case BasketData.BaseCorrelationParam.BasketType.Uniform:
          basket = BasketPricerFactory.UniformBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints);
          break;
        case BasketData.BaseCorrelationParam.BasketType.MonteCarlo:
          basket = BasketPricerFactory.MonteCarloBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            baseCorrParams_.SampleSize, baseCorrParams_.Seed);
          break;
        case BasketData.BaseCorrelationParam.BasketType.SemiAnalytic:
          basket = BasketPricerFactory.SemiAnalyticBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints, baseCorrParams_.GridSize, true);
          break;
        default:
          basket = BasketPricerFactory.SemiAnalyticBasketPricer(portfolioStart,
            asOf_, settle_, maturity, survivalCurves_, principals_[0], copula_, corr,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit, lossLevels,
            quadraturePoints, baseCorrParams_.GridSize, false);
          break;
      }
      int nDates = cdos_.Length;
      Dt[] tenorDates = new Dt[nDates];
      for (int j = 0; j < nDates; ++j)
        tenorDates[j] = cdos_[j][0].Maturity;
      int nDps = cdos_[0].Length;
      double[] dps = new double[nDps];
      for (int i = 0; i < nDps; ++i)
        dps[i] = cdos_[0][i].Detachment;
      double[,] quotes = trancheData_.GetQuotes();
      double[,] running = trancheData_.GetRunningPremiums();
      //baseCorrParams_.ToleranceF = 0.0000008;
      //baseCorrParams_.ToleranceX = 0.00008;
      BaseCorrelationCalibrator cal;
      if (direct)
        cal = new DirectArbitrageFreeCalibrator(
          baseCorrParams_.CalibrationMethod,
          cdos_[0][0], basket, discountCurve_, tenorDates, dps, running, quotes,
          baseCorrParams_.ToleranceF, baseCorrParams_.ToleranceX, true);
      else
      {
        basket.AddGridDates(tenorDates);
        cal = new MaturityMatchCalibrator(
          cdos_[0][0], basket, discountCurve_, tenorDates, dps, running, quotes,
          baseCorrParams_.ToleranceF, baseCorrParams_.ToleranceX);
      }

      bco = cal.Fit(baseCorrParams_.CalibrationMethod,
        baseCorrParams_.Method, baseCorrParams_.StrikeMethod, null,
        baseCorrParams_.StrikeInterp, baseCorrParams_.StrikeExtrap,
        baseCorrParams_.TenorInterp, baseCorrParams_.TenorExtrap,
        0.0, 1.0);

      timer.Stop();
      ResultData rd = ToResultData(bco);
      rd.TimeUsed = timer.Elapsed;

      if (bco != null && consistencyCheck_)
      {
        RountTripCheck(bco, direct);
        //- Protection matching is known to fail round robin
        if (StrikeMethodsToCheck != null)
        {
          foreach (BaseCorrelationStrikeMethod sm in StrikeMethodsToCheck)
          {
            CalculateStrikes(basket, bco, sm);
            RountTripCheck(bco, direct);
          }
        }
        // end round trip
      }

      MatchExpects(rd);
    }

    private void CalculateStrikes(
      BasketPricer basket,
      BaseCorrelationTermStruct bco,
      BaseCorrelationStrikeMethod strikeMethod)
    {
      basket.Correlation =
        bco.CalibrationMethod == BaseCorrelationCalibrationMethod.TermStructure
        ? new CorrelationTermStruct(new string[basket.Count], new double[bco.Dates.Length], bco.Dates)
        : new CorrelationTermStruct(new string[basket.Count], new double[1], new Dt[1] { basket.Settle });
      int nCDO = cdos_[0].Length;
      for (int d = 0; d < nCDO; ++d)
      {
        for (int t = 0; t < bco.Dates.Length; ++t)
        {
          BaseCorrelation bc = bco.BaseCorrelations[t];
          SyntheticCDO cdo = CloneUtil.Clone(cdos_[t][d]);
          cdo.Attachment = 0.0;
          basket.Maturity = cdo.Maturity;
          basket.Reset();
          //basket.SetFactor(Math.Sqrt(bc.Correlations[d]));
          SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basket, discountCurve_,
            cdo.Detachment * basket.TotalPrincipal);
          bc.Strikes[d] = BaseCorrelation.Strike(pricer, strikeMethod, bc.StrikeEvaluator, bc.Correlations[d]);
          bc.StrikeMethod = strikeMethod;
          bc.ScalingFactor = BaseCorrelation.DetachmentScalingFactor(strikeMethod, basket, discountCurve_);
        }
      }
      return;
    }

    private void RountTripCheck(BaseCorrelationTermStruct bco, bool direct)
    {
      foreach (SyntheticCDO[] cdo in cdos_)
        RountTripCheck(bco, cdo, direct);
      return;
    }

    private void RountTripCheck(
      BaseCorrelationTermStruct bco,
      SyntheticCDO[] cdos,
      bool direct)
    {
      int quadraturePoints = baseCorrParams_.QuadraturePoints > 0 ?
        baseCorrParams_.QuadraturePoints :
        BasketPricerFactory.DefaultQuadraturePoints(copula_, survivalCurves_.Length);

      // Create basket pricers
      Dt portfolioStart = new Dt();
      double[] notionals = new double[] { 1.0 };

      SyntheticCDOPricer[] pricers;
      switch (baseCorrParams_.ModelType)
      {
        case BasketData.BaseCorrelationParam.BasketType.LargePool:
          pricers = BasketPricerFactory.CDOPricerLargePool(cdos, asOf_, settle_,
            discountCurve_, survivalCurves_, principals_[0], copula_, bco,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit,
            quadraturePoints, notionals, false);
          break;
        case BasketData.BaseCorrelationParam.BasketType.Uniform:
          pricers = BasketPricerFactory.CDOPricerUniform(cdos, portfolioStart,
            asOf_, settle_, discountCurve_, survivalCurves_, principals_[0], copula_, bco,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit,
            quadraturePoints, notionals, false);
          break;
        case BasketData.BaseCorrelationParam.BasketType.MonteCarlo:
          pricers = BasketPricerFactory.CDOPricerMonteCarlo(cdos, portfolioStart,
            asOf_, settle_, discountCurve_, survivalCurves_, principals_[0], copula_, bco,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit,
            baseCorrParams_.SampleSize, notionals, false, baseCorrParams_.Seed);
          break;
        case BasketData.BaseCorrelationParam.BasketType.SemiAnalytic:
          pricers = BasketPricerFactory.CDOPricerSemiAnalytic(cdos, portfolioStart,
            asOf_, settle_, discountCurve_, survivalCurves_, principals_[0], copula_, bco,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit,
            quadraturePoints, baseCorrParams_.GridSize,
            notionals, false, true);
          break;
        default:
          pricers = BasketPricerFactory.CDOPricerSemiAnalytic(cdos, portfolioStart,
            asOf_, settle_, discountCurve_, survivalCurves_, principals_[0], copula_, bco,
            baseCorrParams_.StepSize, baseCorrParams_.StepUnit,
            quadraturePoints, baseCorrParams_.GridSize,
            notionals, false, false);
          break;
      }

      if (!direct)
      {
        foreach (SyntheticCDOPricer p in pricers)
          p.Basket.AddGridDates(bco.Dates);
      }

      const double tol = 1E-7;
      string sm = bco.BaseCorrelations[0].StrikeMethod.ToString();
      for (int i = 0; i < pricers.Length; ++i)
        Assert.AreEqual(0.0, pricers[i].FlatPrice(), tol,
          sm + "-" + pricers[i].CDO.Description + " Pv");
    }

#if Not_Yet
    private void checkStrikes(BaseCorrelationTermStruct bco)
    {
      int quadraturePoints = baseCorrParams_.QuadraturePoints > 0 ?
        baseCorrParams_.QuadraturePoints :
        BasketPricerFactory.DefaultQuadraturePoints(copula_, survivalCurves_.Length);
      for (int i = 0; i < cdos_.Length; ++i)
      {
        SyntheticCDO[] cdos = new SyntheticCDO[cdos_[i].Length];
        for (int j = 0; j < cdos.Length; ++j)
        {
          SyntheticCDO cdo = (SyntheticCDO)cdos_[i][j].Clone();
          cdo.Attachment = 0.0;
          cdos[j] = cdo;
        }
        SyntheticCDOPricer[] pricer
          = BasketPricerFactory.CDOPricerSemiAnalytic(
            cdos, Dt.Empty, asOf_, settle_,
            discountCurve_, survivalCurves_, principals_[i], copula_,
            bco, baseCorrParams_.StepSize, baseCorrParams_.StepUnit,
            quadraturePoints, baseCorrParams_.GridSize, null, false, false);
        double[] strikes = BaseCorrelation.Strike(pricer, baseCorrParams_.StrikeMethod, null);
        double[] expects = bco.BaseCorrelations[i].Strikes;
        string label = "Tenor " + (i + 1) + " strike ";
        for (int j = 0; j < strikes.Length; ++j)
          Assert.AreEqual(label + (j + 1), expects[j], strikes[j], 1E-7);
      }
      return;
    }
#endif

    private ResultData ToResultData(BaseCorrelationObject bcObj)
    {
      ResultData rd = LoadExpects();
      if (bcObj is BaseCorrelationTermStruct)
      {
        BaseCorrelationTermStruct bct = (BaseCorrelationTermStruct)bcObj;
        int nTenors = bct.BaseCorrelations.Length;
        string[] labels = new string[4];
        double[] values = new double[4];
        labels[0] = bct.CalibrationMethod.ToString();
        values[0] = ToDouble(bct.CalibrationMethod);
        labels[1] = bct.InterpMethod.ToString();
        values[1] = ToDouble(bct.InterpMethod);
        labels[2] = bct.ExtrapMethod.ToString();
        values[2] = ToDouble(bct.ExtrapMethod);
        labels[3] = "tenors";
        values[3] = nTenors;
        if (rd.Results[0].Expects == null)
        {
          rd.Results = new ResultData.ResultSet[1 + nTenors];
          for (int i = 0; i <= nTenors; ++i)
            rd.Results[i] = new ResultData.ResultSet();
        }
        rd.Results[0].Name = "Overall";
        rd.Results[0].Labels = labels;
        rd.Results[0].Actuals = values;
        for (int j = 0; j < nTenors; ++j)
        {
          ResultData.ResultSet rs = ToResultSet(bct.BaseCorrelations[j]);
          rd.Results[1 + j].Actuals = rs.Actuals;
          rd.Results[1 + j].Labels = rs.Labels;
          rd.Results[1 + j].Name = bct.Dates[j].ToString();
        }
      }
      else
      {
        ResultData.ResultSet rs = ToResultSet((BaseCorrelation)bcObj);
        rd.Results[0].Actuals = rs.Actuals;
        rd.Results[0].Labels = rs.Labels;
        rd.Results[0].Name = bcObj.Name;
      }
      return rd;
    }

    private ResultData.ResultSet ToResultSet(BaseCorrelation bc)
    {
      int n = bc.Correlations.Length;
      int len = 5 + (bc.TrancheCorrelations == null ? 2 : 3) * n;
      string[] labels = new string[len];
      double[] values = new double[len];
      labels[0] = bc.Method.ToString();
      values[0] = ToDouble(bc.Method);
      labels[1] = bc.StrikeMethod.ToString();
      values[1] = ToDouble(bc.StrikeMethod);
      labels[2] = bc.InterpMethod.ToString();
      values[2] = ToDouble(bc.InterpMethod);
      labels[3] = bc.ExtrapMethod.ToString();
      values[3] = ToDouble(bc.ExtrapMethod);
      labels[4] = "ScalingFactor";
      values[4] = bc.ScalingFactor;
      for (int i = 0; i < n; ++i)
      {
        labels[5 + i] = "strike " + i;
        values[5 + i] = bc.Strikes[i];
        labels[5 + n + i] = "corr " + i;
        values[5 + n + i] = bc.Correlations[i];
        if (bc.TrancheCorrelations != null)
        {
          labels[5 + n + n + i] = "tcorr " + i;
          values[5 + n + n + i] = bc.TrancheCorrelations[i];
        }
      }
      ResultData.ResultSet rs = new ResultData.ResultSet();
      rs.Labels = labels;
      rs.Actuals = values;
      return rs;
    }

    private double ToDouble(object obj)
    {
      if (obj is double num)
        return num;
      if (obj is Enum)
      {
        var e = (int) Convert.ChangeType(obj, typeof(int));
        return e;
      }
      return obj.ToString().ParseDouble();
    }

    #endregion // Helpers

    #region Properties
    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string BasketDataFile { get; set; } = null;

    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = "data/USD.LIBOR_Data061212.xml";

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string CreditDataFile { get; set; } = "data/CDX.NA.IG.7-V1_CreditData.xml";

    /// <summary>
    ///   Data for index notes
    /// </summary>
    public string TrancheDataFile { get; set; } = "data/CDX.NA.IG.7-V1_TrancheData.xml";

    /// <summary>
    ///   BaseCorrelation parameters
    /// </summary>
    public string BaseCorrelationParamFile { get; set; } = "data/CDX.NA.IG.7-V1_BaseCorrelationPars.xml";

    /// <summary>
    ///   BaseCorrelation function input data
    /// </summary>
    public string IndexDataFile { get; set; } = null;

    /// <summary>
    ///   Copula data
    /// </summary>
    /// <remarks>
    /// Copula data is a comma delimted string in the format
    /// "CopulaType,DfCommon,DfIdiosyncratic". For example,
    /// "StudentT,7,7".  This will override the copula specified
    /// in parameter files
    /// </remarks>
    new public string CopulaData { get; set; } = null;

    /// <summary>
    ///   BaseCorrelationStrikeMethod
    /// </summary>
    public BaseCorrelationStrikeMethod StrikeMethod { get; set; } = BaseCorrelationStrikeMethod.Unscaled;

    /// <summary>
    ///   Perform consistency checks after calibration
    /// </summary>
    /// <remarks>
    /// </remarks>
    public string CheckConsistencyAfterCalibration
    {
      set { consistencyCheck_ = Boolean.Parse(value); }
    }

    /// <summary>
    ///   BaseCorrelationStrikeMethod
    /// </summary>
    public BaseCorrelationStrikeMethod[] StrikeMethodsToCheck { get; set; }

    /// <summary>
    ///   Type of basket model used to calibrating base correlation
    /// </summary>
    public BasketData.BaseCorrelationParam.BasketType Model { get; set; }

    #endregion //Properties

    #region Data
    const double epsilon = 1.0E-7;

    // Data which can be set by others: input files
    private string baseCorrDataFile_ = "data/Bespoke_Pricing_Basket_Correlations.xml";

    // Data which can be set by others: override params
    private bool consistencyCheck_ = true;

    // Data to be initialized by set up routined
    Dt asOf_, settle_;
    SurvivalCurve[] survivalCurves_;
    RecoveryCurve[] recoveryCurves_;
    double[][] principals_;
    DiscountCurve discountCurve_;
    BasketData.TrancheQuotes trancheData_;
    SyntheticCDO[][] cdos_;
    Copula copula_;
    BasketData.BaseCorrelationParam baseCorrParams_;

    private double[] scalingFactors_;
    #endregion // Data

  }
}
