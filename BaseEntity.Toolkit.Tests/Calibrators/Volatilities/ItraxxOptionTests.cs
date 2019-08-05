// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  [TestFixture]
  public class ItraxxOptionTests : ToolkitTestBase
  {
    [Test]
    public void Europe()
    {
      var curve = CreateDiscountCurve();
      var index = "Itraxx Europe.20.5Y".LookUpCreditIndexDefinition();
      foreach (var pd in GetPricers(index, _quotesMain, curve))
      {
        var pricer = pd.Pricer;
        var iv = pricer.ImplyVolatility(pd.FairValue);
        var fv = pricer.CalculateFairPrice(pd.ImpliedVolatility);
        var delta = Math.Abs(SetVolatility(pricer, iv).MarketDelta(0.0001));
        AssertEqual("IV", pd.ImpliedVolatility, iv, 0.02);
        AssertEqual("Delta", pd.Delta, delta, 0.0055);
        AssertEqual("Fv", pd.FairValue, fv, 0.000075);
      }
      return;
    }

    [Test]
    public void ProductScenario()
    {
      var curve = CreateDiscountCurve();
      var index = "Itraxx Europe.20.5Y".LookUpCreditIndexDefinition();
      var pricers = GetPricers(index, _quotesMain, curve)
        .Select(pd => pd.Pricer)
        .Where(p => Math.Abs(p.CDXOption.Strike - 0.008) < 0.0001)
        .ToArray();

      using (new CheckStates(true, pricers))
      {
        var productShift = new ScenarioShiftPricerTerms(
          new[] {"Strike", "Type"}, new object[] {0.0075, OptionType.Put});
        var results = Scenarios.CalcScenario(pricers, "Pv", new[] {productShift}, false, true);
        var expects = new[]
        {
          0.0040325023804057038,
          0.0045157576874115066,
          0.0050780041434140485,
          0.0064017999835391786,
        };
        MatchList(expects, results);
      }

      using (new CheckStates(true, pricers))
      {
        var productShift = new ScenarioShiftPricerTerms(
          new[] {"Strike", "Type"}, new object[] {0.0085, OptionType.Call});
        var results = Scenarios.CalcScenario(pricers, "Pv", new[] {productShift}, false, true);
        var expects = new[]
        {
          0.0010356785646321882,
          0.0010320371891638968,
          0.0010111620487575261,
          0.00095723991269007838,
        };
        MatchList(expects, results);
      }
      return;
    }

    [Test]
    public void CurveScenario()
    {
      var curve = CreateDiscountCurve();
      var index = "Itraxx Europe.20.5Y".LookUpCreditIndexDefinition();
      var pricers = GetPricers(index, _quotesMain, curve)
        .Select(pd => pd.Pricer)
        .Where(p => Math.Abs(p.CDXOption.Strike - 0.008) < 0.0001)
        .Select(p =>
        {
          var cdsPricer = p.GetPricerForUnderlying().EquivalentCDSPricer;
          cdsPricer.Settle = p.CDXOption.Expiration;
          cdsPricer.Notional = 1000000;
          return cdsPricer;
        }).ToArray();

      using (new CheckStates(true, pricers))
      {
        var curveShift = new ScenarioShiftCurves(
          new CalibratedCurve[] {curve},
          new[] {0.001}, ScenarioShiftType.Absolute, true);
        var results = Scenarios.CalcScenario(pricers, "Pv", new[] { curveShift }, false, true);
        var expects = new[]
        {
          -0.0018776629276544554,
          -0.0018833958783943672,
          -0.0018753143722278764,
          -0.0018737183618213749,
        };
        MatchList(expects, results);
      }
      return;
    }

    [Test]
    public void CrossOver()
    {
      var curve = CreateDiscountCurve();
      var index = "ITraxx Europe Crossover.20.5Y".LookUpCreditIndexDefinition();
      foreach (var pd in GetPricers(index, _quotesXO, curve))
      {
        var pricer = pd.Pricer;
        var iv = pricer.ImplyVolatility(pd.FairValue);
        var fv = pricer.CalculateFairPrice(pd.ImpliedVolatility);
        var delta = Math.Abs(SetVolatility(pricer, iv).MarketDelta(0.0001));
        AssertEqual("IV", pd.ImpliedVolatility, iv, 0.06);
        AssertEqual("Delta", pd.Delta, delta, 0.0075);
        AssertEqual("Fv", pd.FairValue, fv, 0.001);
      }
      return;
    }

    private static ICreditIndexOptionPricer SetVolatility(
      ICreditIndexOptionPricer pricer, double volatility)
    {
      var p = pricer;
      var u = p.GetPricerForUnderlying();
      return p.CDXOption.CreatePricer(p.AsOf, p.Settle, p.DiscountCurve,
        new MarketQuote(u.MarketQuote,u.QuotingConvention),
        u.Settle, u.MarketRecoveryRate, u.BasketSize, u.SurvivalCurves,
        CDXOptionModelType.Black, new CDXOptionModelData(),
        CalibratedVolatilitySurface.FromFlatVolatility(p.AsOf,volatility),
        1.0, null);
    }

    #region Option quotes

    struct PricerAndQuotes
    {
      public PricerAndQuotes(ICreditIndexOptionPricer pr,
        double iv, double delta, double fv)
      {
        Pricer = pr;
        ImpliedVolatility = iv;
        Delta = delta;
        FairValue = fv;
      }
      public readonly ICreditIndexOptionPricer Pricer;
      public readonly double ImpliedVolatility, Delta, FairValue;
    }

    private static IEnumerable<PricerAndQuotes> GetPricers(
      CreditIndexDefinition index, string[,] optionQuotes,
      DiscountCurve discountCurve)
    {
      Dt asOf = discountCurve.AsOf, expiry = Dt.Empty;
      var refLevel = new MarketQuote(Double.NaN, QuotingConvention.None);
      int rows = optionQuotes.GetLength(0), cols = optionQuotes.GetLength(1);
      for (int i = 0; i < rows; ++i)
      {
        var item = optionQuotes[i, 0];
        if (item == "Expiry")
        {
          expiry = optionQuotes[i, 1].ParseDt();
          refLevel = new MarketQuote(
            optionQuotes[i, 6].ParseDouble() / 10000,
            QuotingConvention.CreditSpread);
          continue;
        }
        if (String.IsNullOrEmpty(item) || !Char.IsDigit(item[0]))
          continue;

        var strike = item.ParseDouble()/10000;
        var optype = PayerReceiver.None;
        double iv, delta, fv;
        if (!String.IsNullOrEmpty(optionQuotes[i, 5]))
        {
          iv = optionQuotes[i, 1].ParseDouble();
          delta = optionQuotes[i, 3].ParseDouble();
          fv = optionQuotes[i, 5].ParseDouble() / 10000;
          optype = PayerReceiver.Receiver;
        }
        else if (!String.IsNullOrEmpty(optionQuotes[i, 6]))
        {
          iv = optionQuotes[i, 2].ParseDouble();
          delta = optionQuotes[i, 4].ParseDouble();
          fv = optionQuotes[i, 6].ParseDouble() / 10000;
          optype = PayerReceiver.Payer;
        }
        else
          continue;

        var cdxo = new CDXOption(asOf, discountCurve.Ccy, index.CDX,
          expiry, optype, OptionStyle.European, strike, false);
        var pricer = cdxo.CreatePricer(asOf, asOf, discountCurve, refLevel,
          Dt.Empty, index.RecoveryRate, index.EntityCount, null,
          CDXOptionModelType.Black, new CDXOptionModelData(),
          CalibratedVolatilitySurface.FromFlatVolatility(asOf, iv),
          1.0, null);
        yield return new PricerAndQuotes(pricer, iv, delta, fv);
      }
    }

    private string[,] _quotesMain =
      {
        {"Strike", "Rec Vol", "Pay Vol", "Rec Delta", "Pay Delta", "Rec Price", "Pay Price"},
        {"Expiry", "2/19/2014", "", "", "", "Ref", "84.00"},
        {"75", "58.2%", "", "15.1%", "", "4.88", ""},
        {"80", "60.5%", "", "29.2%", "", "11.90", ""},
        {"85", "", "61.7%", "", "54.9%", "", "23.29"},
        {"90", "", "65.2%", "", "40.2%", "", "15.24"},
        {"95", "", "69.1%", "", "28.8%", "", "10.15"},
        {"100", "", "72.5%", "", "20.4%", "", "6.76"},
        {"105", "", "76.6%", "", "14.8%", "", "4.77"},
        {"110", "", "80.2%", "", "10.8%", "", "3.39"},
        {"115", "", "86.1%", "", "8.7%", "", "2.80"},
        {"", "", "", "", "", "", ""},
        {"Expiry", "3/19/2014", "", "", "", "Ref", "84.00"},
        {"65", "54.4%", "", "5.8%", "", "2.25", ""},
        {"70", "53.4%", "", "11.2%", "", "4.82", ""},
        {"75", "54.5%", "", "19.8%", "", "10.05", ""},
        {"80", "56.5%", "55.3%", "30.5%", "69.8%", "18.41", "48.19"},
        {"85", "58.1%", "57.5%", "41.4%", "58.6%", "29.47", "36.66"},
        {"90", "", "60.2%", "", "48.3%", "", "28.07"},
        {"95", "", "62.8%", "", "39.4%", "", "21.64"},
        {"100", "", "65.4%", "", "32.1%", "", "16.91"},
        {"105", "", "67.5%", "", "26.0%", "", "13.17"},
        {"110", "", "69.5%", "", "21.1%", "", "10.35"},
        {"115", "", "71.1%", "", "17.1%", "", "8.11"},
        {"120", "", "72.7%", "", "13.9%", "", "6.40"},
        {"125", "", "", "", "", "", ""},
        {"130", "", "75.9%", "", "9.4%", "", "4.15"},
        {"135", "", "", "", "", "", ""},
        {"140", "", "81.4%", "", "7.2%", "", "3.23"},
        {"", "", "", "", "", "", ""},
        {"Expiry", "4/16/2014", "", "", "", "Ref", "84.00"},
        {"65", "50.0%", "", "7.2%", "", "3.36", ""},
        {"70", "51.4%", "", "13.3%", "", "7.24", ""},
        {"75", "52.8%", "", "21.1%", "", "13.38", ""},
        {"80", "54.8%", "", "29.9%", "", "22.33", ""},
        {"85", "", "56.0%", "", "61.2%", "", "47.24"},
        {"90", "", "58.1%", "", "52.7%", "", "38.34"},
        {"95", "", "60.0%", "", "45.1%", "", "31.16"},
        {"100", "", "61.9%", "", "38.5%", "", "25.54"},
        {"105", "", "63.6%", "", "32.9%", "", "21.04"},
        {"110", "", "65.1%", "", "28.0%", "", "17.31"},
        {"115", "", "66.8%", "", "24.0%", "", "14.49"},
        {"120", "", "67.1%", "", "20.0%", "", "11.56"},
        {"125", "", "", "", "", "", ""},
        {"130", "", "69.4%", "", "14.5%", "", "8.02"},
        {"135", "", "", "", "", "", ""},
        {"140", "", "71.4%", "", "10.6%", "", "5.65"},
        {"", "", "", "", "", "", ""},
        {"Expiry", "6/18/2014", "", "", "", "Ref", "84.00"},
        {"60", "49.0%", "", "6.0%", "", "3.62", ""},
        {"65", "49.1%", "", "9.8%", "", "6.55", ""},
        {"70", "50.6%", "", "15.2%", "", "11.68", ""},
        {"75", "51.8%", "", "21.3%", "", "18.56", ""},
        {"80", "53.3%", "52.3%", "27.9%", "72.3%", "27.51", "77.53"},
        {"85", "", "54.2%", "", "65.6%", "", "66.88"},
        {"90", "", "55.7%", "", "59.2%", "", "57.67"},
        {"95", "", "58.1%", "", "53.5%", "", "50.77"},
        {"100", "", "58.8%", "", "47.9%", "", "43.54"},
        {"105", "", "61.1%", "", "43.4%", "", "38.86"},
        {"110", "", "61.4%", "", "38.7%", "", "33.30"},
        {"115", "", "64.6%", "", "35.7%", "", "31.07"},
        {"120", "", "63.8%", "", "31.5%", "", "25.93"},
        {"125", "", "67.0%", "", "29.4%", "", "24.69"},
        {"130", "", "65.7%", "", "25.6%", "", "20.26"},
        {"135", "", "", "", "", "", ""},
        {"140", "", "66.5%", "", "20.5%", "", "15.44"},
        {"145", "", "", "", "", "", ""},
        {"150", "", "68.8%", "", "17.2%", "", "12.80"},
      };

    private string[,] _quotesXO =
      {
        {"Strike", "Rec Vol", "Pay Vol", "Rec Delta", "Pay Delta", "Rec Price", "Pay Price"},
        {"Expiry", "2/19/2014", "", "", "", "Ref", "315.00"},
        {"300", "51.5%", "", "28.2%", "", "33.55", ""},
        {"325", "", "53.4%", "", "47.6%", "", "63.53"},
        {"350", "", "57.6%", "", "28.0%", "", "32.25"},
        {"375", "", "62.3%", "", "16.1%", "", "17.12"},
        {"400", "", "67.3%", "", "9.5%", "", "9.75"},
        {"", "", "", "", "", "", ""},
        {"Expiry", "3/19/2014", "", "", "", "Ref", "315.00"},
        {"275", "46.4%", "", "14.1%", "", "19.14", ""},
        {"300", "47.8%", "", "29.0%", "", "50.16", ""},
        {"325", "", "49.3%", "", "54.0%", "", "106.68"},
        {"350", "", "51.8%", "", "38.7%", "", "68.00"},
        {"375", "", "54.7%", "", "27.1%", "", "44.02"},
        {"400", "", "57.7%", "", "19.1%", "", "29.50"},
        {"425", "", "60.8%", "", "13.7%", "", "20.48"},
        {"450", "", "63.8%", "", "10.0%", "", "14.73"},
        {"475", "", "67.1%", "", "7.6%", "", "11.18"},
        {"", "", "", "", "", "", ""},
        {"Expiry", "4/16/2014", "", "", "", "Ref", "315.00"},
        {"250", "45.8%", "", "7.4%", "", "10.99", ""},
        {"275", "45.8%", "", "16.1%", "", "28.15", ""},
        {"300", "47.0%", "", "28.5%", "", "61.10", ""},
        {"325", "", "48.2%", "", "57.8%", "", "143.80"},
        {"350", "", "50.3%", "", "45.1%", "", "101.56"},
        {"375", "", "52.5%", "", "34.5%", "", "72.25"},
        {"400", "", "54.7%", "", "26.4%", "", "52.44"},
        {"425", "", "57.2%", "", "20.4%", "", "39.19"},
        {"450", "", "59.3%", "", "15.8%", "", "29.40"},
        {"475", "", "61.7%", "", "12.5%", "", "23.10"},
        {"500", "", "64.6%", "", "10.3%", "", "19.11"},
        {"", "", "", "", "", "", ""},
        {"Expiry", "6/18/2014", "", "", "", "Ref", "318.00"},
        {"250", "43.6%", "", "8.5%", "", "16.92", ""},
        {"275", "44.0%", "", "15.6%", "", "36.01", ""},
        {"300", "44.9%", "", "24.8%", "", "67.16", ""},
        {"325", "", "46.0%", "", "65.0%", "", "220.41"},
        {"350", "", "47.5%", "", "55.1%", "", "172.20"},
        {"375", "", "49.1%", "", "46.1%", "", "135.44"},
        {"400", "", "51.0%", "", "38.5%", "", "108.15"},
        {"425", "", "52.6%", "", "32.2%", "", "86.91"},
        {"450", "", "54.2%", "", "26.9%", "", "70.59"},
        {"475", "", "56.0%", "", "22.8%", "", "58.48"},
        {"500", "", "57.4%", "", "19.3%", "", "48.47"},
      };
    #endregion

    #region IR quotes
    private DiscountCurve CreateDiscountCurve()
    {
      var asOf = _pricingDate;
      var data = _irQuotes;
      var tenors = data.Column(0).Skip(1).ToArray();
      var dates = data.Column(1).Skip(1).Select(s => s.ParseDt()).ToArray();
      var intruments = data.Column(2).Skip(1).ToArray();
      var indexName = data.Column(4).FirstOrDefault();
      var term = RateCurveTermsUtil.CreateDefaultCurveTerms(indexName);
      var fitSettings = new CalibratorSettings { CurveAsOf = asOf };
      var quotes = data.Column(4).Skip(1).Select(s => s.ParseDouble()).ToArray();
      var curve = DiscountCurveFitCalibrator.DiscountCurveFit(
        asOf, term, indexName, quotes, intruments, tenors, fitSettings);
      return curve;
    }
    private Dt _pricingDate = _D("1/30/2014");

    private string[,] _irQuotes =
      {
        {"Constructed Curve", "", "", "USDLIBOR_3M", "EURIBOR_3M"},
        {"1 D", "1/31/2014", "MM", "0.092%", ""},
        {"1 W", "2/6/2014", "MM", "0.124%", "0.173%"},
        {"2 W", "2/13/2014", "MM", "", "0.185%"},
        {"1 M", "2/28/2014", "MM", "0.160%", "0.205%"},
        {"2 M", "3/30/2014", "MM", "0.208%", "0.245%"},
        {"3 M", "4/30/2014", "MM", "0.242%", "0.282%"},
        {"4 M", "5/30/2014", "MM", "", ""},
        {"5 M", "6/30/2014", "MM", "", ""},
        {"6 M", "7/30/2014", "MM", "0.344%", "0.388%"},
        {"9 M", "10/30/2014", "MM", "", "0.481%"},
        {"1 Y", "1/30/2015", "MM", "0.585%", "0.557%"},
        {"H4", "3/19/2014", "FUT", "", ""},
        {"M4", "6/18/2014", "FUT", "", ""},
        {"U4", "9/17/2014", "FUT", "", ""},
        {"Z4", "12/17/2014", "FUT", "", ""},
        {"H5", "3/18/2015", "FUT", "", ""},
        {"18 M", "7/30/2015", "", "", ""},
        {"2 Yr", "1/30/2016", "Swap", "0.538%", "0.544%"},
        {"3 Yr", "1/30/2017", "Swap", "0.946%", "0.753%"},
        {"4 Yr", "1/30/2018", "Swap", "1.411%", "1.011%"},
        {"5 Yr", "1/30/2019", "Swap", "1.835%", "1.263%"},
        {"6 Yr", "1/30/2020", "Swap", "2.191%", "1.489%"},
        {"7 Yr", "1/30/2021", "Swap", "2.486%", "1.692%"},
        {"8 Yr", "1/30/2022", "Swap", "2.720%", "1.872%"},
        {"9 Yr", "1/30/2023", "Swap", "2.910%", "2.032%"},
        {"10 Yr", "1/30/2024", "Swap", "3.069%", "2.173%"},
        {"11 Yr", "1/30/2025", "Swap", "3.206%", "2.292%"},
        {"12 Yr", "1/30/2026", "Swap", "3.314%", "2.394%"},
        {"15 Yr", "1/30/2029", "Swap", "3.548%", "2.610%"},
        {"20 Yr", "1/30/2034", "Swap", "3.741%", "2.743%"},
        {"25 Yr", "1/30/2039", "Swap", "3.824%", "2.774%"},
        {"30 Yr", "1/30/2044", "Swap", "3.866%", "2.768%"},
        {"40 Yr", "1/30/2054", "Swap", "3.884%", "2.780%"},
        {"50 Yr", "1/30/2064", "Swap", "3.865%", "2.789%"},
      };
    private static Dt _D(string str)
    {
      return str.ParseDt();
    }
    #endregion

    static void MatchList(IList<double> expects, IList<double> actuals)
    {
      Assert.AreEqual(expects.Count, actuals.Count);
      for (int i = 0, n = expects.Count; i < n; ++i)
        Assert.AreEqual(expects[i], actuals[i], 1E-9);
    }
  }
}
