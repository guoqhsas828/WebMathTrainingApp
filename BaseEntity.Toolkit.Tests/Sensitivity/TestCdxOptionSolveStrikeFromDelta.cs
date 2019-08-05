using System;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Tests.Calibrators.Volatilities;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{

  [TestFixture]
  public class TestCdxOptionSolveStrikeFromDelta
  {
    private CdxVolatilityTestData _data;

    [OneTimeSetUp]
    public void Init()
    {
      _data = CdxVolatilityTestData.Get(GetDataFile("IV_IR.txt"));
    }

    [TestCase("IV_HY40", CDXOptionModelType.Black, PayerReceiver.Payer)]
    [TestCase("IV_HY40", CDXOptionModelType.BlackPrice, PayerReceiver.Payer)]
    [TestCase("IV_HY40", CDXOptionModelType.FullSpread, PayerReceiver.Payer)]
    [TestCase("IV_HY40", CDXOptionModelType.ModifiedBlack, PayerReceiver.Payer)]
    [TestCase("IV_IG40", CDXOptionModelType.Black, PayerReceiver.Payer)]
    [TestCase("IV_IG40", CDXOptionModelType.BlackPrice, PayerReceiver.Payer)]
    [TestCase("IV_IG40", CDXOptionModelType.FullSpread, PayerReceiver.Payer)]
    [TestCase("IV_IG40", CDXOptionModelType.ModifiedBlack, PayerReceiver.Payer)]
    [TestCase("IV_HY40", CDXOptionModelType.Black, PayerReceiver.Receiver)]
    [TestCase("IV_HY40", CDXOptionModelType.BlackPrice, PayerReceiver.Receiver)]
    [TestCase("IV_HY40", CDXOptionModelType.FullSpread, PayerReceiver.Receiver)]
    [TestCase("IV_HY40", CDXOptionModelType.ModifiedBlack, PayerReceiver.Receiver)]
    [TestCase("IV_IG40", CDXOptionModelType.Black, PayerReceiver.Receiver)]
    [TestCase("IV_IG40", CDXOptionModelType.BlackPrice, PayerReceiver.Receiver)]
    [TestCase("IV_IG40", CDXOptionModelType.FullSpread, PayerReceiver.Receiver)]
    [TestCase("IV_IG40", CDXOptionModelType.ModifiedBlack, PayerReceiver.Receiver)]
    public void TestCdxOptionSolveStrikeGivenDelta(string cdxDataFile,
     CDXOptionModelType model, PayerReceiver type)
    {
      var input = GetDataFile(cdxDataFile + ".txt");
      var data = _data.EnumerateVolatilityQuotes(input).ToArray();
      for (int i = 0; i < 10; i++)
      {
        var quote = data[i];
        var pricer = CdxOptionTestUtility.CreatePricer(_data, quote, type, model, 0.3);
        TestSolveStrikeFromDeltaBoundary(pricer);
        TestSolveStrikeFromDeltaRoundTrip(pricer);
      }
    }

    private double PreviousQuote { get; set; }

    private void TestSolveStrikeFromDeltaBoundary(ICreditIndexOptionPricer pricer)
    {
      const double bumpSize = 0.0001;
      const double tinyPertube = 0.00001;
      var cdxPricer = pricer.GetPricerForUnderlying();
      var marketQuote = cdxPricer.MarketQuote;
      if (marketQuote.AlmostEquals(PreviousQuote)) return;

      var isStrikePrice = pricer.CDXOption.StrikeIsPrice;
      double delta1, delta2;
      if (isStrikePrice)
      {
        var indexSpread = cdxPricer.PriceToSpread(marketQuote);
        var bound1 = cdxPricer.SpreadToPrice(indexSpread * 0.5);
        delta1 = CalcDeltaGivenStrike(pricer, bound1);
        var bound2 = cdxPricer.SpreadToPrice(indexSpread * 3);
        delta2 = CalcDeltaGivenStrike(pricer, bound2);
      }
      else
      {
        delta1 = CalcDeltaGivenStrike(pricer, 0.5 * marketQuote);
        delta2 = CalcDeltaGivenStrike(pricer, 2 * marketQuote);
      }

      if (delta1 > delta2)
      {
        delta1 -= tinyPertube;
        delta2 += tinyPertube;
      }
      else
      {
        delta1 += tinyPertube;
        delta2 -= tinyPertube;
      }
      PreviousQuote = marketQuote;

      var strike1 = pricer.SolveStrikeFromDelta(delta1, bumpSize);
      var delta1Round = CalcDeltaGivenStrike(pricer, strike1);
      Assert.AreEqual(delta1, delta1Round, 1E-6);

      var strike2 = pricer.SolveStrikeFromDelta(delta2, bumpSize);
      var delta2Round = CalcDeltaGivenStrike(pricer, strike2);
      Assert.AreEqual(delta2, delta2Round, 1E-6);
    }

    private void TestSolveStrikeFromDeltaRoundTrip(ICreditIndexOptionPricer pricer)
    {
      const double bumpSize = 0.0001;
      var inputStrike = pricer.CDXOption.Strike;
      var delta = pricer.MarketDelta(bumpSize);
      pricer.CDXOption.Strike = Double.NaN;
      var strikeRound = pricer.SolveStrikeFromDelta(delta, bumpSize);
      pricer.CDXOption.Strike = inputStrike;
      Assert.AreEqual(inputStrike, strikeRound, 5e-5);
    }

    private static double CalcDeltaGivenStrike(ICreditIndexOptionPricer pricer,
      double strike)
    {
      var originalStrike = pricer.CDXOption.Strike;
      try
      {
        pricer.CDXOption.Strike = strike;
        return pricer.MarketDelta(0.0001);
      }
      finally
      {
        pricer.CDXOption.Strike = originalStrike;
      }
    }

    private static string GetDataFile(string filename)
    {
      var file = GetTestFilePath("data/" + filename);
      return file;
    }
  }
}
