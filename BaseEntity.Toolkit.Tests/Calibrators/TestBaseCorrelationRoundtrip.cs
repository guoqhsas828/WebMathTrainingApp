// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestBaseCorrelationRoundtrip
  {
    #region Static Data

    private static readonly Data[] Tests =
    {
      new Data
      {
        Tenor = "3M",
        AsOf = new Dt(20150512),
        IndexQuote = 50,
        IndexQuoteInPrice = false,
        BasketSize = 100,
        Recovery = 0.4,
        Premium = 100,
        CdsSpread = 100,
        TrancheQuotes = new[,]
        {
          {0.03, 990, 500},
          {0.07, 200, 500},
          {0.10, 70, 500},
          {0.15, 40, 100},
          {0.30, 10, 100}
        },
      },
      new Data
      {
        Tenor = "5Y",
        AsOf = new Dt(20150512),
        IndexQuote = 200,
        IndexQuoteInPrice = false,
        BasketSize = 100,
        Recovery = 0.4,
        Premium = 100,
        CdsSpread = 100,
        TrancheQuotes = new[,]
        {
          {0.03, 0.6344, 500},
          {0.07, 0.2044, 500},
          {0.10, -0.037, 500},
          {0.15, 0.0299, 100},
        },
      },
      new Data
      {
        Tenor = "3Y",
        AsOf = new Dt(20150428),
        IndexQuote = 22,
        IndexQuoteInPrice = false,
        BasketSize = 100,
        Recovery = 0.4,
        Premium = 100,
        CdsSpread = 100,
        TrancheQuotes = new[,]
        {
          {0.03, 0.01, 500},
          {0.07, -0.0106, 100},
          {0.10, -0.0163, 100},
          {0.15, -0.0034, 25}
        },
      },
      new Data
      {
        Tenor = "3Y",
        AsOf = new Dt(20150312),
        IndexQuote = 234,
        IndexQuoteInPrice = false,
        BasketSize = 100,
        Recovery = 0.4,
        Premium = 100,
        CdsSpread = 100,
        TrancheQuotes = new[,]
        {
          {0.03, 0.7344, 500},
          {0.07, 0.3044, 500},
          {0.10, -0.017, 500},
          {0.15, 0.0399, 100},
        },
      },
    };

    private const int DetachmentIndex = 0,
      QuoteIndex = 1, PremiumIndex = 2;

    private static readonly BaseCorrelationParam BcParam =
      new BaseCorrelationParam
      {
        CopulaType = CopulaType.ExtendedGauss,
        AccuracyLevel = 0,
        QuadraturePoints = 50,
        StepSize = 3,
        StepUnit = TimeUnit.Months,
      };

    #endregion

    #region Test cases

    [Test, TestCaseSource(nameof(TestCases))]
    public void RoundtripDpCorrelations(
      int caseNumber,
      BaseCorrelationStrikeMethod mappingMethod)
    {
      var data = Tests[caseNumber];

      // Get the base correlation with unscaled mapping.
      var unscaled = data.GetBaseCaseUnscaled();

      // Calculate the strikes with the specified strike mapping methods.
      var strikes = CalculateStrikes(mappingMethod, unscaled);

      // Directly calibrate a based correlation with the specified strike mapping.
      var bct = data.GetBaseCorrelation(data.IndexQuote,
        data.IndexQuoteInPrice, data.TrancheQuotes, mappingMethod);

      // Both methods should produce the same strikes
      Assert.That(strikes, Is.EqualTo(bct.BaseCorrelations[0].Strikes)
        .Within(1E-10), "Strikes");

      // Now round trip the detachment correlations from CDO pricers
      var dpCorrs = GetCdoPricers(bct)
        .Select(p => ((BaseCorrelationBasketPricer)p.Basket).DPCorrelation)
        .ToArray();
      Assert.That(dpCorrs, Is.EqualTo(unscaled.BaseCorrelations[0].Correlations)
        .Within(1E-7), "Detachment Correlations");

      return;
    }

    static double GetDpCorrelation(IPricer pricer)
    {
      var p = (SyntheticCDOPricer)pricer;
      var b = (BaseCorrelationTermStruct)
        ((BaseCorrelationBasketPricer)p.Basket).BaseCorrelation;
      var dp = p.CDO.Detachment;
      var dps = b.BaseCorrelations[0].Detachments;
      int i = 0;
      for (i = 0; i < dps.Length; ++i)
      {
        if (dps[i].AlmostEquals(dp)) break;
      }
      return b.BaseCorrelations[0].Correlations[i];
    }

    static double GetDpStrike(IPricer pricer)
    {
      var p = (SyntheticCDOPricer)pricer;
      var b = (BaseCorrelationTermStruct)
        ((BaseCorrelationBasketPricer)p.Basket).BaseCorrelation;
      var dp = p.CDO.Detachment;
      var dps = b.BaseCorrelations[0].Detachments;
      int i = 0;
      for (i = 0; i < dps.Length; ++i)
      {
        if (dps[i].AlmostEquals(dp)) break;
      }
      return b.BaseCorrelations[0].Strikes[i];
    }


    static double GetCorrelations(IPricer pricer)
    {
      var p = (SyntheticCDOPricer) pricer;
      return ((BaseCorrelationBasketPricer) p.Basket).DPCorrelation;
    }

    

    [Test, TestCaseSource(nameof(TestCases))]
    public void BaseCorrelationBumpRoundTrip(int caseNumber, 
      BaseCorrelationStrikeMethod mappingMethod)
    {
      var origData = Tests[caseNumber];
      var bumpTargets = new List<BumpTarget>
      {
        BumpTarget.TrancheAndIndexQuotes,
        BumpTarget.IndexQuotes,
        BumpTarget.TrancheQuotes
      };
      
      foreach (var bumpTarget in bumpTargets)
      {
        //Original BC object
        var origBc = origData.GetBaseCorrelation(origData.IndexQuote,
          origData.IndexQuoteInPrice, origData.TrancheQuotes, mappingMethod);

        //Bumped BC object
        var hasIndexQuoteBump = (bumpTarget & BumpTarget.IndexQuotes) != 0;
        var hasTrancheQuoteBump = (bumpTarget & BumpTarget.TrancheQuotes) != 0;

        var bumpedBc = origData.GetBaseCorrelation(
          hasIndexQuoteBump ? BumpQuotes(origData.IndexQuote, 1.0) : origData.IndexQuote,
          origData.IndexQuoteInPrice,
          hasTrancheQuoteBump ? GetTrancheQuotes(origData.TrancheQuotes, 1.0) : origData.TrancheQuotes,
          mappingMethod);

        //Evaluators
        var evaluatorBc = GetCdoPricers(origBc)
          .Select(p => new PricerEvaluator(p, GetDpCorrelation))
          .ToArray();

        var evaluatorStrike = GetCdoPricers(origBc)
          .Select(p => new PricerEvaluator(p, GetDpStrike))
          .ToArray();

        //Sensitivities
        var tableBc = Sensitivities.DoBaseCorrelationSensitivity(
          evaluatorBc, null, 1, 0, BumpUnit.Natural, origBc, null, null, null,
          bumpTarget, false, 1, BaseCorrelationBumpType.Uniform,
          false, false, null);

        var tableStrike = Sensitivities.DoBaseCorrelationSensitivity(
          evaluatorStrike, null, 1, 0, BumpUnit.Natural, origBc, null, null, null,
          bumpTarget, false, 1, BaseCorrelationBumpType.Uniform,
          false, false, null);

        //Compare
        var rowsBc = tableBc.Rows;
        var rowsStrike = tableStrike.Rows;
        for (int i = 0, m = rowsBc.Count; i < m; ++i)
        {
          var bcChange = rowsBc[i]["Delta"];
          var expectedBc = bumpedBc.BaseCorrelations[0].Correlations[i]
                           - origBc.BaseCorrelations[0].Correlations[i];
          var strikeChange = rowsStrike[i]["Delta"];
          var expectedStrike = bumpedBc.BaseCorrelations[0].Strikes[i]
                               - origBc.BaseCorrelations[0].Strikes[i];
          Assert.AreEqual(expectedBc, (double) bcChange, 5E-12);
          Assert.AreEqual(expectedStrike, (double)strikeChange, 5E-12);
        }
      }
    }

    [Test, TestCaseSource(nameof(TestCases))]
    public void BaseCorrelationRoundTrip(int caseNumber,
      BaseCorrelationStrikeMethod mappingMethod)
    {
      var origData = Tests[caseNumber];
      var bumpTargets = new List<BumpTarget>
      {
        BumpTarget.TrancheAndIndexQuotes,
        BumpTarget.IndexQuotes,
        BumpTarget.TrancheQuotes
      };

      foreach (var bumpTarget in bumpTargets)
      {
        //Original BCs
        var origBc = origData.GetBaseCorrelation(origData.IndexQuote,
          origData.IndexQuoteInPrice, origData.TrancheQuotes, mappingMethod);

        var originalDpCorrelations = GetCdoPricers(origBc)
          .Select(p => ((BaseCorrelationBasketPricer)p.Basket).DPCorrelation).ToArray();

        //Bumped BCs
        var hasIndexQuoteBump = (bumpTarget & BumpTarget.IndexQuotes) != 0;
        var hasTrancheQuoteBump = (bumpTarget & BumpTarget.TrancheQuotes) != 0;

        var bumpedBc = origData.GetBaseCorrelation(
          hasIndexQuoteBump ? BumpQuotes(origData.IndexQuote, 1.0) : origData.IndexQuote,
          origData.IndexQuoteInPrice,
          hasTrancheQuoteBump ? GetTrancheQuotes(origData.TrancheQuotes, 1.0) : origData.TrancheQuotes,
          mappingMethod);

        var evaluator = GetCdoPricers(origBc)
          .Select(p => new PricerEvaluator(p, GetCorrelations))
          .ToArray();

        var bumpDpCorrelations = evaluator
          .Select(eval => eval.Evaluate(((SyntheticCDOPricer)eval.Pricer)
            .Substitute(bumpedBc))).ToArray();

        //Sensitivity
        var table = Sensitivities.DoBaseCorrelationSensitivity(
          evaluator, null, 1, 0, BumpUnit.Natural, origBc, null, null, null,
          bumpTarget, false, 1, BaseCorrelationBumpType.Uniform,
          false, false, null);

        //Compare
        var rows = table.Rows;
        for (int i = 0, m = rows.Count; i < m; ++i)
        {
          var bcChange = rows[i]["Delta"];
          var expected = bumpDpCorrelations[i] - originalDpCorrelations[i];
          Assert.AreEqual(expected, (double)bcChange, 5E-12);
        }
      }
    }

    private static double BumpQuotes(double originalQuote, double bumpSize)
    {
      if (originalQuote < 1.0)
        return originalQuote + bumpSize/100.0;
      return originalQuote + bumpSize;
    }

    private static double[,] GetTrancheQuotes(double[,] trancheQuotes, double bumpSize)
    {
      var quotes = (double[,])trancheQuotes.Clone();
      for (int i = 0, m = quotes.GetLength(0); i < m; ++i)
        quotes[i, 1] = BumpQuotes(quotes[i, 1], bumpSize);
      return quotes;
    }


    private static IEnumerable<object[]> TestCases
    {
      get
      {
        return typeof(BaseCorrelationStrikeMethod).GetFields(
          System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public)
          .Select(f => (BaseCorrelationStrikeMethod)f.GetValue(null))
          .Where(f => f != BaseCorrelationStrikeMethod.UserDefined)
          .SelectMany(m => Enumerable.Range(0, Tests.Length)
            .Select(i => new object[] { i, m }));
      }
    }

    #endregion

    #region Calculate strikes with a different strike mapping method

    /// <summary>
    ///   Calculate strikes with a different strike mapping
    /// </summary>
    /// <param name="mapping">Requested strike mapping method</param>
    /// <param name="bct">The base correlation term struct object</param>
    /// <returns>Strikes based on the specified mapping method</returns>
    private static double[] CalculateStrikes(
      BaseCorrelationStrikeMethod mapping,
      BaseCorrelationTermStruct bct)
    {
      // For testing purpose, only calculate the strikes on the first tenor.
      const int tenorIndex = 0;
      var maturity = bct.Dates[tenorIndex];
      var corrs = bct.BaseCorrelations[tenorIndex].Correlations;

      // The following are general.
      var cal = bct.Calibrator;
      var discountCurve = cal.IndexTerm.DiscountCurve;
      var term = cal.TrancheTerm;
      var dps = cal.Detachments;
      var strikes = new double[dps.Length];
      for (int i = 0; i < strikes.Length; ++i)
      {
        if (Double.IsNaN(corrs[i]))
        {
          strikes[i] = Double.NaN;
          continue;
        }

        var dp = dps[i];
        var cdo = new SyntheticCDO(term.Effective, maturity, term.Ccy,
          0, term.DayCount, term.Freq, term.BDConvention, term.Calendar)
        {
          Detachment = dp,
        };

        var basket = cal.Basket.Substitute(cal.Basket.OriginalBasket,
          new SingleFactorCorrelation(cal.Basket.EntityNames, 0),
          new[] { 0, dp });

        var pricer = new SyntheticCDOPricer(cdo, basket,
          discountCurve, null, cdo.TrancheWidth * 1000000, null);
        strikes[i] = BaseCorrelation.Strike(pricer, mapping, null, corrs[i]);
      }

      return strikes;
    }

    #endregion

    #region Create CDO pricers for round trip check

    private static IEnumerable<SyntheticCDOPricer> GetCdoPricers(
      BaseCorrelationTermStruct bct)
    {
      var cal = bct.Calibrator;
      var term = cal.TrancheTerm;
      var cdo = cal.Detachments.Select((d, i) => new SyntheticCDO(
        term.Effective, term.Maturity, term.Ccy, cal.RunningPremiums[0][i],
        term.DayCount, term.Freq, term.BDConvention, term.Calendar)
      {
        //Attachment = i > 0 ? cal.Detachments[i - 1] : 0,
        Detachment = d,
      }).ToArray();
      var notionals = cdo.Select(c => c.TrancheWidth*1000000).ToArray();


      var isc = cal.GetIndexTerm();
      var pricers = BasketPricerFactory.CDOPricerSemiAnalytic(cdo, Dt.Empty,
        isc.AsOf, isc.AsOf + 1, isc.DiscountCurve, isc.GetScaleSurvivalCurves(),
        null, BcParam.Copula, bct, BcParam.StepSize, BcParam.StepUnit,
        BcParam.QuadraturePoints, BcParam.GridSize,
        notionals, false, false, null);
      return pricers;
    }

    #endregion

    #region Nested type: Data for base correlation calibration
    private class Data
    {
      // Input data
      public Dt AsOf;
      public string Tenor;
      public double IndexQuote, Recovery, Premium, CdsSpread;
      public bool IndexQuoteInPrice;
      public int BasketSize;
      public double[,] TrancheQuotes;

      // Intermediate results
      private DiscountCurve _discountCurve;
      private SurvivalCurve[] _survivalCurves;
      private BaseCorrelationTermStruct _baseCaseUnscaled;

      public BaseCorrelationTermStruct GetBaseCaseUnscaled()
      {
        return _baseCaseUnscaled ?? (_baseCaseUnscaled
          = GetBaseCorrelation(IndexQuote, IndexQuoteInPrice,
            TrancheQuotes, BaseCorrelationStrikeMethod.Unscaled));
      }

      public BaseCorrelationTermStruct GetBaseCorrelation(
        double indexQuote, bool indexQuoteIsPrice,
        double[,] trancheQuotes,
        BaseCorrelationStrikeMethod strikeMethod)
      {
        var quote = indexQuoteIsPrice ? (indexQuote / 100) : (indexQuote / 10000);
        var calc = GetScalingCalibrator(quote, indexQuoteIsPrice);
        return GetBaseCorrelations(calc, trancheQuotes, strikeMethod);
      }

      private static BaseCorrelationTermStruct GetBaseCorrelations(
        IndexScalingCalibrator calc, double[,] trancheQuotes,
        BaseCorrelationStrikeMethod strikeMethod)
      {
        int nTranche = trancheQuotes.GetLength(0);
        var dps = trancheQuotes.Column(DetachmentIndex).ToArray();
        var premiums = trancheQuotes.Column(PremiumIndex)
          .Select(d => Enumerable.Repeat(d, 1)).ToArray2D(nTranche, 1);
        var quotes = trancheQuotes.Column(QuoteIndex)
          .Select(d => Enumerable.Repeat(d, 1)).ToArray2D(nTranche, 1);
        var cdx = calc.Indexes[0];
        var bco = BaseCorrelationFactory.BaseCorrelationFromMarketQuotes(
          BaseCorrelationCalibrationMethod.MaturityMatch, strikeMethod,
          calc, premiums, dps, quotes,
          new[] { cdx }, new[] { cdx.Maturity }, null, null, BcParam);
        return bco;
      }

      private IndexScalingCalibrator GetScalingCalibrator(
        double quote, bool quoteIsPrice)
      {
        var discountCurve = _discountCurve ??
          (_discountCurve = new DiscountCurve(AsOf, 0.04));
        var survCurves = _survivalCurves ??
          (_survivalCurves = GetCreditCurves(discountCurve));

        var scalingMethods = new[] { CDXScalingMethod.Spread };
        var cdx = GetCdx();

        var calc = new IndexScalingCalibrator(AsOf, AsOf + 1, new[] { cdx },
          new[] { cdx.Description }, new[] { quote }, quoteIsPrice, scalingMethods,
          false, false, discountCurve, survCurves, null, Recovery);
        calc.Iterative = true;
        calc.ScaleHazardRates = true;

        return calc;
      }

      private SurvivalCurve[] GetCreditCurves(DiscountCurve dc)
      {
        int n = BasketSize;
        var curves = new SurvivalCurve[n];
        var asOf = AsOf;
        var cdx = GetCdx();
        var pars = new SurvivalCurveParameters(
          cdx.DayCount, cdx.Freq, cdx.BDConvention, cdx.Calendar,
          InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow);
        double premium = Premium,
          spread = CdsSpread, recovery = Recovery;
        for (int i = 0; i < n; ++i)
        {
          curves[i] = SurvivalCurve.FitCDSQuotes("sc_" + i,
            asOf, asOf + 1, Currency.None, "", CDSQuoteType.ParSpread, premium,
            pars, dc, new[] { cdx.Description }, null, new[] { spread },
            new[] { recovery }, 0,
            null, null, 0, false);
        }
        return curves;
      }

      private CDX GetCdx()
      {
        var calendar = Calendar.None;
        var effective = Dt.SNACFirstAccrualStart(AsOf, calendar);
        var maturity = Dt.CDXMaturity(AsOf, Toolkit.Base.Tenor.Parse(Tenor));
        return new CDX(effective, maturity, Currency.None, Premium / 10000,
          DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
          calendar) { Description = Tenor };
      }
    }

    #endregion
  }
}
