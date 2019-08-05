//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Shared;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Tests.Helpers.Quotes;

namespace BaseEntity.Toolkit.Tests.Calibrators.BaseCorrelations
{
  [TestFixture("BC.MaturityMatch.DJ.LCDX.9-V1")]
  [TestFixture("BC.TermStructure.CDX.NA.IG.10.V1")]
  [TestFixture("BC.MaturityMatch.CDX.NA.IG.10.V1")]
  [TestFixture("BC.MaturityMatch.TopDown.CDX.NA.IG.10.V1")]
  public class TestBaseCorrelationFactory : ToolkitTestBase
  {
    public TestBaseCorrelationFactory(string fixtureName) : base(fixtureName)
    {}

    [Test]
    public void Calibrate()
    {
      // Calibrate
      DiscountCurve discountCurve = new DiscountCurve(
        new Dt(19700101), 0.05);
      BaseCorrelationParam bcParam = GetBCParams();
      BasketQuotes basketQuotes = QuoteUtil.LoadBasketQuotes(
        BasketQuotesFile);
      SurvivalCurve[] curves = QuoteUtil.CreateSurvivalCurves(
        basketQuotes.CdsQuotes, discountCurve, true, false);
      IndexScalingCalibrator index = QuoteUtil.CreateScalingCalibrator(
        basketQuotes.Index, basketQuotes.IndexQuotes, curves,
        discountCurve, CDXScalingMethod, true, true, null);
      BaseCorrelationTermStruct bct = QuoteUtil.CreateBaseCorrelation(
        index, basketQuotes.TrancheQuotes, bcParam);

      // Round trip check
      bool isLCDX = basketQuotes.Index.IndexName.Contains("LCDX");
      SyntheticCDO[][] cdos = QuoteUtil.CreateSyntheticCDOs(
        index, basketQuotes.TrancheQuotes);
      RountTripCheck(index.AsOf, index.Settle, bcParam, bct,
        cdos, discountCurve, index.GetScaleSurvivalCurves(),
        isLCDX, true);
      if (StrikeMethodsToCheck != null)
        foreach (BaseCorrelationStrikeMethod sm in StrikeMethodsToCheck)
        {
          CalculateStrikes(sm, index.AsOf, index.Settle, bcParam,
            index.GetScaleSurvivalCurves(), bct, cdos, discountCurve);
          RountTripCheck(index.AsOf, index.Settle, bcParam, bct,
            cdos, discountCurve, index.GetScaleSurvivalCurves(),
            isLCDX, true);
        }

      MatchExpects(bct);
    }

    #region Save Result Data
    private void MatchExpects(BaseCorrelationObject bcObj)
    {
      ResultData rd = LoadExpects();
      if (bcObj is BaseCorrelationTermStruct)
      {
        BaseCorrelationTermStruct bct = (BaseCorrelationTermStruct)bcObj;
        int nTenors = bct.BaseCorrelations.Length;

        List<string> labels = new List<string>();
        List<double> values = new List<double>();
        labels.Add(bct.CalibrationMethod.ToString());
        values.Add(ToDouble(bct.CalibrationMethod));
        labels.Add(bct.InterpMethod.ToString());
        values.Add(ToDouble(bct.InterpMethod));
        labels.Add(bct.ExtrapMethod.ToString());
        values.Add(ToDouble(bct.ExtrapMethod));
        labels.Add("tenors");
        values.Add(nTenors);
        if (bct.Calibrator != null && bct.Calibrator.IndexTerm != null)
        {
          double[] scalingFactors = bct.Calibrator.IndexTerm.GetScalingFactorsWithZeros();
          for (int i = 0; i < scalingFactors.Length; ++i)
          {
            labels.Add("Scaling " + (i + 1));
            values.Add(scalingFactors[i]);
          }
        }
        if (rd.Results[0].Expects == null)
        {
          rd.Results = new ResultData.ResultSet[1 + nTenors];
          for (int i = 0; i <= nTenors; ++i)
            rd.Results[i] = new ResultData.ResultSet();
        }
        rd.Results[0].Name = "Overall";
        rd.Results[0].Labels = labels.ToArray();
        rd.Results[0].Actuals = values.ToArray();

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
      rd.TimeUsed = bcObj.CalibrationTime;

      MatchExpects(rd);
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
        var e = (int)Convert.ChangeType(obj, typeof(int));
        return e;
      }
      return obj.ToString().ParseDouble();
    }
    #endregion Save Result Data

    #region Helpers

    private BaseCorrelationParam GetBCParams()
    {
      BaseCorrelationParam par = new BaseCorrelationParam();
      if (BCParams != null)
      {
        string[] parts = BCParams.Split(';');
        foreach (string part in parts)
          if (part.Contains(":"))
          {
            string[] pair = part.Split(':');
            if (pair[0] != null && pair[0].Length != 0)
              ConversionUtil.SetValue(par, pair[0], pair[1], true);
          }
      }
      return par;
    }

    #endregion Helpers

    #region Round Trip Checks

    private void CalculateStrikes(
      BaseCorrelationStrikeMethod strikeMethod,
      Dt asOf, Dt settle,
      BaseCorrelationParam bcParam,
      SurvivalCurve[] survivalCurves,
      BaseCorrelationTermStruct bco,
      SyntheticCDO[][] cdos,
      DiscountCurve discountCurve)
    {
      BasketPricer basket = BaseCorrelationFactory.CreateBasket(
        asOf, settle, cdos[cdos.Length - 1][0].Maturity, survivalCurves, new double[] { }, bcParam);
      basket.Correlation =
        bco.CalibrationMethod == BaseCorrelationCalibrationMethod.TermStructure
        ? new CorrelationTermStruct(new string[basket.Count], new double[bco.Dates.Length], bco.Dates)
        : new CorrelationTermStruct(new string[basket.Count], new double[1], new Dt[1] { basket.Settle });
      int nCDO = cdos[0].Length;
      for (int d = 0; d < nCDO; ++d)
      {
        for (int t = 0; t < bco.Dates.Length; ++t)
        {
          BaseCorrelation bc = bco.BaseCorrelations[t];
          SyntheticCDO cdo = CloneUtil.Clone(cdos[t][d]);
          cdo.Attachment = 0.0;
          basket.Maturity = cdo.Maturity;
          basket.Reset();
          //basket.SetFactor(Math.Sqrt(bc.Correlations[d]));
          SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basket, discountCurve,
            cdo.Detachment * basket.TotalPrincipal);
          bc.Strikes[d] = BaseCorrelation.Strike(pricer, strikeMethod, bc.StrikeEvaluator, bc.Correlations[d]);
          bc.StrikeMethod = strikeMethod;
          bc.ScalingFactor = BaseCorrelation.DetachmentScalingFactor(strikeMethod, basket, discountCurve);
        }
      }
      return;
    }

    private void RountTripCheck(
      Dt asOf, Dt settle,
      BaseCorrelationParam bcParam,
      BaseCorrelationTermStruct bco,
      SyntheticCDO[][] cdos,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      bool checkRefinance, bool direct)
    {
      foreach (SyntheticCDO[] cdo in cdos)
        RountTripCheck(asOf, settle, bcParam, bco, cdo,
          discountCurve, survivalCurves, checkRefinance, direct);
      return;
    }

    private void RountTripCheck(
      Dt asOf, Dt settle,
      BaseCorrelationParam baseCorrParams,
      BaseCorrelationTermStruct bco,
      SyntheticCDO[] cdos,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      bool checkRefinance, bool direct)
    {
      Copula copula = baseCorrParams.Copula;
      int quadraturePoints = baseCorrParams.QuadraturePoints > 0 ?
        baseCorrParams.QuadraturePoints :
        BasketPricerFactory.DefaultQuadraturePoints(copula, survivalCurves.Length);

      // Create basket pricers
      Dt portfolioStart = new Dt();
      double[] notionals = new double[] { 1.0 };
      double[] principals = new double[] { };

      SyntheticCDOPricer[] pricers;
      switch (baseCorrParams.ModelType)
      {
        case BasketModelType.LargePool:
          pricers = BasketPricerFactory.CDOPricerLargePool(cdos, asOf, settle,
            discountCurve, survivalCurves, principals, copula, bco,
            baseCorrParams.StepSize, baseCorrParams.StepUnit,
            quadraturePoints, notionals, false);
          break;
        case BasketModelType.Uniform:
          pricers = BasketPricerFactory.CDOPricerUniform(cdos, portfolioStart,
            asOf, settle, discountCurve, survivalCurves, principals, copula, bco,
            baseCorrParams.StepSize, baseCorrParams.StepUnit,
            quadraturePoints, notionals, false);
          break;
        case BasketModelType.MonteCarlo:
          pricers = BasketPricerFactory.CDOPricerMonteCarlo(cdos, portfolioStart,
            asOf, settle, discountCurve, survivalCurves, principals, copula, bco,
            baseCorrParams.StepSize, baseCorrParams.StepUnit,
            baseCorrParams.SampleSize, notionals, false, baseCorrParams.Seed);
          break;
        case BasketModelType.SemiAnalytic:
          pricers = BasketPricerFactory.CDOPricerSemiAnalytic(cdos, portfolioStart,
            asOf, settle, discountCurve, survivalCurves, principals, copula, bco,
            baseCorrParams.StepSize, baseCorrParams.StepUnit,
            quadraturePoints, baseCorrParams.GridSize,
            notionals, false, true);
          break;
        default:
          pricers = BasketPricerFactory.CDOPricerSemiAnalytic(cdos, portfolioStart,
            asOf, settle, discountCurve, survivalCurves, principals, copula, bco,
            baseCorrParams.StepSize, baseCorrParams.StepUnit,
            quadraturePoints, baseCorrParams.GridSize,
            notionals, false, checkRefinance);
          break;
      }

      if (!direct)
      {
        foreach (SyntheticCDOPricer p in pricers)
          p.Basket.AddGridDates(bco.Dates);
      }

      for (int i = 0; i < pricers.Length; ++i)
        if (pricers[i] != null)
        {
          pricers[i].Validate();
          pricers[i].Basket.AccuracyLevel = baseCorrParams.AccuracyLevel;
        }

      double tol = RoundTripTolerance;
      string sm = bco.BaseCorrelations[0].StrikeMethod.ToString();
      int first = 0, last = pricers.Length - 1;
      if (baseCorrParams.BottomUp)
      {
        if (pricers[last].CDO.Detachment > 0.9999)
          --last;
      }
      else if (pricers[0].CDO.Attachment < 0.0001)
        ++first;
      for (int i = first; i <= last; ++i)
      {
        double pv = pricers[i].FlatPrice() + pricers[i].CDO.Fee * pricers[i].Notional;
        Assert.AreEqual(0.0, pv, tol,
          sm + "-" + pricers[i].CDO.Description + " Pv");
      }
      return;
    }
    #endregion Round Trip Checks

    #region Properties

    /// <summary>
    ///   CDXScalingMethod
    /// </summary>
    public CDXScalingMethod CDXScalingMethod { get; set; } = CDXScalingMethod.Model;

    /// <summary>
    ///   BaseCorrelationParam
    /// </summary>
    public string BCParams { get; set; }

    /// <summary>
    ///   Basket Quotes file
    /// </summary>
    public string BasketQuotesFile { get; set; }

    /// <summary>
    ///   BaseCorrelationStrikeMethod
    /// </summary>
    public BaseCorrelationStrikeMethod[] StrikeMethodsToCheck { get; set; }

    /// <summary>
    ///   Round trip tolerance
    /// </summary>
    public double RoundTripTolerance { get; set; } = 1E-6;

    #endregion Properties

    #region Data

    #endregion Data
  }
}
