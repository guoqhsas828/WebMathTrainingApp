//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.CounterpartyCredit;

namespace BaseEntity.Toolkit.Tests.Pricers
{

  [TestFixture]
  public class TestHistoricVaR : TestCCRBase
  {
    /// <summary>
    /// Create input
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      input_ = null;
    }

    #region Tests

    [Test]
    public void HistoricVaR()
    {
      DiscountCurve[] dc;
      FxRate[] fx;
      SurvivalCurve[] sc;
      CalibratedCurve[] cc;
      var ccy = currencies_[0];
      CreateMarket(currencies_.Length, lambda_.Length, true, false, true, out dc, out fx, out sc, out cc);
      var portfolio = CreatePricers(dc, fx, sc, cc);
      var simulDates = GenerateSimulDates(asOf_, simulFreq_);
      var fwdTenors = tenors_.Select(Tenor.Parse).ToArray();
      var curves = new List<Curve>();
      curves.AddRange(dc);
      curves.AddRange(sc);
      curves.AddRange(cc);
      var curveArray = curves.ToArray();
      var tenors = Array.ConvertAll(curveArray, curve => fwdTenors);
      var curveShiftRelative = Array.ConvertAll(curveArray, curve => true);
      var fxShiftRelative = Array.ConvertAll(fx, o => true);
      var varEngine = new HistoricVarEngine(asOf_, ccy, portfolio, curveArray, tenors, curveShiftRelative,
                                            fx, fxShiftRelative, new StockCurve[0], new bool[0], simulDates, false, 0.0);
      var rand = new Random();
      foreach (var curve in curves)
      {
        foreach (var dt in simulDates)
        {
          var curveShift = Array.ConvertAll(fwdTenors, t => 0.10 * rand.NextDouble());
          varEngine.AddCurveShift(dt, curve.Name, curveShift);
        }
      }
      foreach (var fxRate in fx)
      {
        foreach (var dt in simulDates)
        {
          varEngine.AddFXShift(dt, fxRate.FromCcy, .01);
        }
      }
      varEngine.SimulateLosses();
      var result1 = varEngine.AllocateValueAtRisk(.95, LEstimatorMethod.HarrellDavis);
      var result2 = varEngine.ValueAtRisk(.95, LEstimatorMethod.HarrellDavis);
      Assert.AreEqual(result1.Sum(), result2, .01);
    }

    [Test]
    public void ExpectedShortfall()
    {
      DiscountCurve[] dc;
      FxRate[] fx;
      SurvivalCurve[] sc;
      CalibratedCurve[] cc;
      var ccy = currencies_[0];
      CreateMarket(currencies_.Length, lambda_.Length, true, true, true, out dc, out fx, out sc, out cc);
      var portfolio = CreatePricers(dc, fx, sc, cc);
      var simulDates = GenerateSimulDates(asOf_, simulFreq_);
      var fwdTenors = tenors_.Select(Tenor.Parse).ToArray();
      var curves = new List<Curve>();
      var stockCurves = new List<StockCurve>();
      curves.AddRange(dc);
      curves.AddRange(sc);
      foreach (CalibratedCurve calibratedCurve in cc)
      {
        if(calibratedCurve is StockCurve)
          stockCurves.Add(calibratedCurve as StockCurve);
        else
          curves.Add(calibratedCurve);
      }

      var curveArray = curves.ToArray();
      var stockArray = stockCurves.ToArray();
      var tenors = Array.ConvertAll(curveArray, curve => fwdTenors);
      var curveShiftRelative = Array.ConvertAll(curveArray, curve => true);
      var fxShiftRelative = Array.ConvertAll(fx, f => true);
      var stockShiftRelative = Array.ConvertAll(stockArray, s => true);
      var varEngine = new HistoricVarEngine(asOf_, ccy, portfolio, curveArray, tenors, curveShiftRelative,
                                            fx, fxShiftRelative, stockArray, stockShiftRelative, simulDates, false, 0.0);
      var rand = new Random();
      foreach (var curve in curves)
      {
        foreach (var dt in simulDates)
        {
          var curveShift = Array.ConvertAll(fwdTenors, t => 0.10 * rand.NextDouble());
          varEngine.AddCurveShift(dt, curve.Name, curveShift);
        }
      }

      foreach (var fxRate in fx)
      {
        foreach (var dt in simulDates)
        {
          varEngine.AddFXShift(dt, fxRate.FromCcy, .01);
        }
      }

      foreach (var stockCurve in stockCurves)
      {
        foreach (var dt in simulDates)
        {
          varEngine.AddStockPriceShift(dt, stockCurve.Name, .10 * rand.NextDouble());
        }
      }

      varEngine.SimulateLosses();
      var result1 = varEngine.AllocateExpectedShortfall(.95);
      var result2 = varEngine.ExpectedShortfall(.95);
      Assert.AreEqual(result1.Sum(), result2, .01);
    }

    protected override BaseEntity.Toolkit.Ccr.ICounterpartyCreditRiskCalculations CreateEngine(Input input)
    {
      throw new NotImplementedException();
    }

    protected override IPricer[] CreateBondPricers(out string[] id)
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
