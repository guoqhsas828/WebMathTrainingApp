// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Collections.Generic;
using System.Linq;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Calibrators.Volatilities.Bump;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{

  [TestFixture]
  public class TestCdxOptionVolRoundTrip
  {

    [SetUp]
    public static void SetUp()
    {
      _initialFactor = 0.99;
      _currentFactor = 0.98;
      _existingLoss = 0.00885;
      _cdxRunningPermium = 500.0;
      _recoveryRate = 0.3;
      _flatVol = 0.52;
      _cdxAccrualDay = new Dt(20140922);
      _cdxMaturity = new Dt(20191220);
      _optionExpiry = new Dt(20150617);
      _pricingDt = new Dt(20150318);
      _settleDt=new Dt(20150319);
      _discountCurve = new RateCurveBuilder().CreateRateCurves(_pricingDt) as DiscountCurve;
      _data = GetCdxOptionModelData();
    }

    #region Bump interpolated volatilities

    [TestCase(CDXOptionModelType.Black, true, true)]
    [TestCase(CDXOptionModelType.BlackPrice, true, true)]
    [TestCase(CDXOptionModelType.ModifiedBlack, true, true)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, true, true)]
    [TestCase(CDXOptionModelType.FullSpread, true, true)]
    [TestCase(CDXOptionModelType.Black, false, true)]
    [TestCase(CDXOptionModelType.BlackPrice, false, true)]
    [TestCase(CDXOptionModelType.ModifiedBlack, false, true)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, false, true)]
    [TestCase(CDXOptionModelType.FullSpread, false, true)]
    [TestCase(CDXOptionModelType.Black, true, false)]
    [TestCase(CDXOptionModelType.BlackPrice, true, false)]
    [TestCase(CDXOptionModelType.ModifiedBlack, true, false)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, true, false)]
    [TestCase(CDXOptionModelType.FullSpread, true, false)]
    [TestCase(CDXOptionModelType.Black, false, false)]
    [TestCase(CDXOptionModelType.BlackPrice, false, false)]
    [TestCase(CDXOptionModelType.ModifiedBlack, false, false)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, false, false)]
    [TestCase(CDXOptionModelType.FullSpread, false, false)]
    public static void BumpInterpolated(
      CDXOptionModelType modelType, bool strikeIsPrice, bool isPriceVolatility)
    {
      var flatVol = modelType == CDXOptionModelType.BlackPrice ? (_flatVol / 50) : _flatVol;
      double[] strikes;
      Dt[] maturities;
      var cvs = GetSurface(modelType, strikeIsPrice, isPriceVolatility,
        flatVol, out strikes, out maturities);
      int idx = -1;
      foreach (var pricer in CreatePricers(
        strikeIsPrice ? StrikesByPrice : StrikesBySpread,
        strikeIsPrice, modelType, cvs, PayerReceiver.Payer))
      {
        TestVolatilityBumpInterpolated(idx, 1E-13, pricer, cvs);
      }
    }

    internal static void TestVolatilityBumpInterpolated(
      int idx, double eps,
      ICreditIndexOptionPricer cdxoPricer,
      CalibratedVolatilitySurface originalSurface = null)
    {
      const BumpFlags flags = BumpFlags.BumpInterpolated;

      var pricer = (CreditIndexOptionPricer) cdxoPricer;
      var volatility = pricer.Volatility;
      var pv = pricer.ProductPv();
      pricer.Reset();

      // First perform direct bump and check results
      BumpResult bump;
      double bumpedVolatility, bumpedPv;
      var selection = new[]
      {
        pricer.VolatilitySurface as CalibratedVolatilitySurface,
      }.SelectParallel(null).First();
      try
      {
        bump = selection.Bump(1.0, flags);
        bumpedVolatility = pricer.Volatility;
        bumpedPv = pricer.ProductPv();
      }
      finally
      {
        selection.Restore(flags);
      }

      Assert.AreEqual(bump.Amount, bumpedVolatility - volatility,
        eps, "Bump at " + idx);
      Assert.AreEqual(1.0, bump.Amount*10000,
        eps, "Amount at " + idx);

      // Second check the delta from the sensitivity function
      pricer.Reset();
      var table = Sensitivities2.Calculate(new IPricer[] {pricer},
        "Pv", null, BumpTarget.Volatilities, 1.0, 0.0, BumpType.Parallel,
        BumpFlags.BumpInterpolated, null, true, false, null, false,
        true, null);
      var delta = (double) table.Rows[0]["Delta"];
      Assert.AreEqual((bumpedPv - pv)/bump.Amount, delta,
        eps, "Delta at " + idx);

      if (originalSurface == null || originalSurface == pricer.VolatilitySurface)
        return;

      // Further tests of bumping the original surface
      try
      {
        originalSurface.BumpInterpolated(1.0, flags);
        pricer.Reset();
        var pv1 = pricer.ProductPv();
        Assert.AreEqual(pv, pv1, 1E-16, "Bump original surface at " + idx);

        var pricer1 = (CreditIndexOptionPricer) CreatePricer(
          pricer.CDXOption.Strike, pricer.StrikeIsPrice,
          GetModelType(pricer), originalSurface, PayerReceiver.Payer);
        var bumpedPv1 = pricer1.ProductPv();
        Assert.AreEqual(delta, (bumpedPv1 - pv1)/bump.Amount,
          eps, "Delta bumping original at " + idx);
      }
      finally
      {
        originalSurface.RestoreInterpolated();
      }
    }

    private static CDXOptionModelType GetModelType(
      CreditIndexOptionPricer pricer)
    {
      var surface = (CdxVolatilitySurface) pricer.VolatilitySurface;
      return surface.ModelType;
    }

    #endregion

    #region Bump volatility quotes

    [TestCase(CDXOptionModelType.Black, true, true)]
    [TestCase(CDXOptionModelType.BlackPrice, true, true)]
    [TestCase(CDXOptionModelType.ModifiedBlack, true, true)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, true, true)]
    [TestCase(CDXOptionModelType.FullSpread, true, true)]
    [TestCase(CDXOptionModelType.Black, false, true)]
    [TestCase(CDXOptionModelType.BlackPrice, false, true)]
    [TestCase(CDXOptionModelType.ModifiedBlack, false, true)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, false, true)]
    [TestCase(CDXOptionModelType.FullSpread, false, true)]
    [TestCase(CDXOptionModelType.Black, true, false)]
    [TestCase(CDXOptionModelType.BlackPrice, true, false)]
    [TestCase(CDXOptionModelType.ModifiedBlack, true, false)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, true, false)]
    [TestCase(CDXOptionModelType.FullSpread, true, false)]
    [TestCase(CDXOptionModelType.Black, false, false)]
    [TestCase(CDXOptionModelType.BlackPrice, false, false)]
    [TestCase(CDXOptionModelType.ModifiedBlack, false, false)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, false, false)]
    [TestCase(CDXOptionModelType.FullSpread, false, false)]
    public static void BumpQuotes(
      CDXOptionModelType modelType, bool strikeIsPrice, bool isPriceVolatility)
    {
      var flatVol = modelType == CDXOptionModelType.BlackPrice ? (_flatVol / 50) : _flatVol;
      double[] strikes;
      Dt[] maturities;
      var cvs = GetSurface(modelType, strikeIsPrice, isPriceVolatility,
        flatVol, out strikes, out maturities);
      var pricers = CreatePricers(
        strikeIsPrice ? StrikesByPrice : StrikesBySpread,
        strikeIsPrice, modelType, cvs, PayerReceiver.Payer)
        .OfType<IPricer>().ToArray();
  
      // Quotes are fair values and we bump them up 1bp.
      // Expect the fair value deltas be 1bp.
      var table = Sensitivities2.Calculate(pricers, "FairValue",
        null, BumpTarget.Volatilities, 1.0, 0.0, BumpType.Parallel,
        BumpFlags.None, null, true, false, null, false,
        true, null);
      var eps = (modelType == CDXOptionModelType.ModifiedBlack
        || modelType == CDXOptionModelType.FullSpread) ? 2E-4 : 1E-8;
      for (int i = 0; i < pricers.Length; ++i)
      {
        var delta = (double)table.Rows[i]["Delta"]*10000;
        Assert.AreEqual(1.0, delta, eps, "Delta at " + i);
      }
    }

    #endregion

    #region Round trip with different models

    [TestCase(CDXOptionModelType.Black, true, true)]
    [TestCase(CDXOptionModelType.BlackPrice, true, true)]
    [TestCase(CDXOptionModelType.ModifiedBlack, true, true)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, true, true)]
    [TestCase(CDXOptionModelType.FullSpread, true, true)]
    [TestCase(CDXOptionModelType.Black, false, true)]
    [TestCase(CDXOptionModelType.BlackPrice, false, true)]
    [TestCase(CDXOptionModelType.ModifiedBlack, false, true)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, false, true)]
    [TestCase(CDXOptionModelType.FullSpread, false, true)]
    [TestCase(CDXOptionModelType.Black, true, false)]
    [TestCase(CDXOptionModelType.BlackPrice, true, false)]
    [TestCase(CDXOptionModelType.ModifiedBlack, true, false)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, true, false)]
    [TestCase(CDXOptionModelType.FullSpread, true, false)]
    [TestCase(CDXOptionModelType.Black, false, false)]
    [TestCase(CDXOptionModelType.BlackPrice, false, false)]
    [TestCase(CDXOptionModelType.ModifiedBlack, false, false)]
    [TestCase(CDXOptionModelType.BlackArbitrageFree, false, false)]
    [TestCase(CDXOptionModelType.FullSpread, false, false)]
    public static void InterpolateWithDifferentModels(
      CDXOptionModelType modelType, bool strikeIsPrice, bool isPriceVolatility)
    {
      var flatVol = modelType == CDXOptionModelType.BlackPrice ? (_flatVol / 50) : _flatVol;
      double[] strikes;
      Dt[] maturities;
      var cvs = GetSurface(modelType, strikeIsPrice, isPriceVolatility,
        flatVol, out strikes, out maturities);
      //interpolate and compare
      int rows = strikes.Length, cols = maturities.Length;
      var result = new double[rows, cols];
      for (int i = 0; i < rows; ++i)
      {
        if (strikes[i] <= 0) continue;
        for (int j = 0; j < cols; ++j)
        {
          if (maturities[j].IsEmpty()) continue;
          result[i, j] = cvs.InterpolateCdxVolatility(maturities[j], strikes[i], strikeIsPrice, modelType);
          if (modelType == CDXOptionModelType.ModifiedBlack || modelType == CDXOptionModelType.FullSpread)
            Assert.AreEqual(flatVol, result[i, j], 9E-6);
          else
            Assert.AreEqual(flatVol, result[i, j], 1E-8);
        }
      }
    }

    #endregion

    #region Utilities

    private static CDXOptionModelData GetCdxOptionModelData()
    {
      CDXOptionModelData data = new CDXOptionModelData();
      data.Choice |=CDXOptionModelParam.HandleIndexFactors;
      return data;
    }

    private static CalibratedVolatilitySurface GetSurface(
      CDXOptionModelType modelType, bool strikeIsPrice,
      bool isPriceVolatility, double flatVol,
      out double[] resultStrikes, out Dt[] resultMaturities)
    {
      Initialize(strikeIsPrice);

      //fair values from option pricers
      //for the blackprice model, too high flatvol may cause the numerial issue when fit the BS vol.
      double[] payerValues = CalculateFairValues(strikeIsPrice ?
        StrikesByPrice : StrikesBySpread, strikeIsPrice,
        modelType, flatVol, PayerReceiver.Payer);
      double[] receiverValues = CalculateFairValues(strikeIsPrice ?
        StrikesByPrice : StrikesBySpread, strikeIsPrice,
        modelType, flatVol, PayerReceiver.Receiver);

      //index
      var creditIndex = "CDX.NA.HY.23.5Y".LookUpCreditIndexDefinition().CDX;

      //vol underlying
      var volUnderlying = new CdxVolatilityUnderlying(_pricingDt, _settleDt,
        GetQuote(strikeIsPrice), _discountCurve, creditIndex, _recoveryRate,
        strikeIsPrice, isPriceVolatility,
        _data, _currentFactor, _existingLoss, _initialFactor);

      //parse quotes and strikes
      var maturities = resultMaturities = new[] {_optionExpiry, _optionExpiry};
      double[,] quotes = new double[StrikesByPrice.Length, 2];
      double[] strikes = resultStrikes = new double[StrikesByPrice.Length];
      for (int i = 0; i < StrikesByPrice.Length; ++i)
      {
        strikes[i] = strikeIsPrice ? (StrikesByPrice[i]/100.0) : (StrikesBySpread[i]/10000.0);
        quotes[i, 0] = payerValues[i]/10000.0;
        quotes[i, 1] = receiverValues[i]/10000.0;
      }

      //vol surface
      return VolatilitySurfaceFactory
        .FitVolatilitySurfaceFromQuotesWithLayout(_pricingDt, null,
          maturities, strikes.Cast<object>().ToArray(), quotes,
          "row=strikes,column=dates/payer-receiver",
          VolatilityQuoteType.StrikePrice, volUnderlying,
          SmileModel.SplineInterpolation,
          InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
          InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
          null, "Vol Surface");
    }

    private static double[] CalculateFairValues(double[] strikes, bool strikeByPrice,
      CDXOptionModelType modelType,
      double volatility, PayerReceiver pr)
    {
      //change type and strike to create pricer
      return CreatePricers(strikes, strikeByPrice, modelType,
        CalibratedVolatilitySurface.FromFlatVolatility(_pricingDt, volatility),
        pr).Select(p => p.FairValue()*10000.0).ToArray();
    }

    private static ICreditIndexOptionPricer CreatePricer(
      double strike, bool strikeByPrice,
      CDXOptionModelType modelType,
      CalibratedVolatilitySurface volatility,
      PayerReceiver pr)
    {
      var k = strikeByPrice ? (strike*100) : (strike*10000);
      return CreatePricers(new[] {k}, strikeByPrice, modelType,
        volatility, pr).First();
    }

    private static IEnumerable<ICreditIndexOptionPricer> CreatePricers(
      double[] strikes, bool strikeByPrice,
      CDXOptionModelType modelType,
      CalibratedVolatilitySurface volatility,
      PayerReceiver pr)
    {
      //create cdx option
      var cdxo = new CDXOption(_cdxAccrualDay, _optionExpiry,
        _cdxAccrualDay, _cdxMaturity, Currency.USD,
        _cdxRunningPermium/10000, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following,
        Calendar.NYB, PayerReceiver.Payer, OptionStyle.European,
        strikeByPrice ? _optionStrike/100.0 : _optionStrike/10000.0,
        strikeByPrice, _initialFactor);
      cdxo.Description = "CDX.NA.HY.23.5Y Option";
      cdxo.SettlementType = SettlementType.Cash;

      //change type and strike to create pricer
      for (int i = 0; i < strikes.Length; ++i)
      {
        cdxo.Strike = strikeByPrice ? strikes[i]/100.0 : strikes[i]/10000.0;
        cdxo.Type = (OptionType) pr;
        cdxo.Validate();
        yield return CreateIndexOptionPricer(cdxo,
          strikeByPrice, volatility, modelType);
      }
    }

    private static ICreditIndexOptionPricer CreateIndexOptionPricer(
      CDXOption option, bool strikeByPrice,
      CalibratedVolatilitySurface volatility,
      CDXOptionModelType modelType)
    {
      var optionPricer = option.CreatePricer(_pricingDt, _settleDt, _discountCurve, GetQuote(strikeByPrice),
        Dt.Empty, _recoveryRate, 0 /*basketSize*/, null, modelType, _data,
        volatility, 1.0, null);

      optionPricer.SetIndexFactorAndLosses(_currentFactor, _existingLoss);

      return optionPricer;
    }

    private static MarketQuote GetQuote(bool strikeByPrice)
    {
      return new MarketQuote(strikeByPrice ? _cdxQuote / 100.0 : _cdxQuote / 10000.0,
        strikeByPrice ? QuotingConvention.FlatPrice : QuotingConvention.CreditSpread);
    }

    private static void Initialize(bool cdxMarketQuoteIsPrice)
    {
      if (cdxMarketQuoteIsPrice)
      {
        _optionStrike = 106.0;
        _cdxQuote = 107.9188;
      }
      else
      {
        _optionStrike = 460.0;
        _cdxQuote = 465.0;
      }
    }

    #endregion

    #region Data

    private static DiscountCurve _discountCurve;
    private static CDXOptionModelData _data;

    private static Dt _cdxAccrualDay,
      _cdxMaturity,
      _optionExpiry,
      _pricingDt,
      _settleDt;

    private static double _initialFactor,
      _currentFactor,
      _existingLoss,
      _optionStrike,
      _cdxQuote,
      _cdxRunningPermium,
      _recoveryRate,
      _flatVol;

    private static readonly double[] StrikesByPrice = { 104, 104.5, 105, 105.5, 106, 106.5, 107, 107.5 };

    private static readonly double[] StrikesBySpread = {445, 450, 460, 462, 464, 466, 468, 470};

    #endregion Data
  }
}
