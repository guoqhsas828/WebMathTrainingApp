//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture]
  public class TestCdxOptionWithDefaults
  {
    /// <summary>
    ///   Test consistency in default handling
    /// </summary>
    /// <param name="numDefaultsBeforeOptionStruck">
    ///   Number of names defaulted before option struck</param>
    /// <param name="numDefaultsAfterOptionStruck">
    ///   Number of names defaulted between option struck and pricing date</param>
    /// <param name="marketOnly">
    ///   If true, use no credit curves and supply index factors and loss explicitly;
    ///   otherwise, everything come from credit curves</param>
    [TestCase(0, 1, false)]
    [TestCase(0, 2, false)]
    [TestCase(1, 1, false)]
    [TestCase(1, 2, false)]
    [TestCase(2, 2, false)]
    [TestCase(0, 1, true)]
    [TestCase(0, 2, true)]
    [TestCase(1, 1, true)]
    [TestCase(1, 2, true)]
    [TestCase(2, 2, true)]
    public void TestConsistency(int numDefaultsBeforeOptionStruck,
      int numDefaultsAfterOptionStruck, bool marketOnly)
    {
      // Create a pricer with identical curves and with no default
      var pricer0 = GetPricerWithHomogenousBasket(marketOnly);

      // Record some values to be used later
      Dt asOf = pricer0.AsOf, optionEffective=pricer0.CDXOption.Effective,
        optionExpiry = pricer0.CDXOption.Expiration;
      double fep = pricer0.FrontEndProtection, 
        fv = pricer0.AtTheMoneyForwardValue,
        fk = pricer0.ForwardStrikeValue, 
        df = pricer0.DiscountCurve.DiscountFactor(asOf, optionExpiry);

      // Calculate the default data
      int basketSize = pricer0.BasketSize;
      double recovery = pricer0.MarketRecoveryRate;
      var factor0 = 1.0 - 1.0 * numDefaultsBeforeOptionStruck / basketSize;
      var dflts = 1.0 * numDefaultsAfterOptionStruck / basketSize;
      var factor1 = factor0 - dflts;
      var loss = dflts * (1 - recovery);// / df;

      // Make some curves defaulted
      var curves = pricer0.SurvivalCurves;
      if (curves != null)
      {
        for (int i = 0; i < numDefaultsBeforeOptionStruck; ++i)
          curves[i].SetDefaulted(optionEffective - 1, true);
        for (int i = 0; i < numDefaultsAfterOptionStruck; ++i)
          curves[numDefaultsBeforeOptionStruck + i].SetDefaulted(asOf - 1, true);
      }

      // Now create a new pricer with defaults
      var cdxo = pricer0.CDXOption;
      if (curves == null && factor0 < 1)
      {
        cdxo = new CDXOption(cdxo.Effective, cdxo.Ccy, cdxo.CDX,
          cdxo.Expiration, PayerReceiver.Payer, cdxo.Style,
          cdxo.Strike, cdxo.StrikeIsPrice, factor0);
      }
      var pricer = (CreditIndexOptionPricer)cdxo.CreatePricer(
        asOf, asOf, pricer0.DiscountCurve, GetMarketQuote(pricer0),
        pricer0.IndexSettleDate, recovery, basketSize, curves,
        CDXOptionModelType.Black, GetModelData(),
        pricer0.VolatilitySurface, 1, null);
      pricer.SetIndexFactorAndLosses(factor1, loss);

      // The effective notional should be consistent
      //   with index factor at option struck
      Assert.AreEqual(factor0, pricer.EffectiveNotional, 1E-15, "Initial Factor");

      // The current notional should be consistent with current index factor
      Assert.AreEqual(factor1, pricer.CurrentNotional, 1E-15, "Current Factor");

      // Consistency of the existing loss
      Assert.AreEqual(loss, pricer.ExistingLoss, 1E-15, "Existing Loss");

      // Consistency of the forward front end protection value
      Assert.AreEqual(fep, pricer.FrontEndProtection, 1E-15, "Forward FEP");

      // Consistency of the ATM forward value
      Assert.AreEqual(fv, pricer.AtTheMoneyForwardValue, 1E-15, "ATM Forward");

      // Consistency of the intrinsic value
      var netValue = loss + df*(factor1*fv - fk*factor0);
      Assert.AreEqual(netValue, pricer.Intrinsic(), 1E-15, "Intrinsic Value");

      // Call-put parity for all the models
      TestCallPutParity(pricer, CDXOptionModelType.Black, netValue);
      TestCallPutParity(pricer, CDXOptionModelType.BlackPrice, netValue);
      TestCallPutParity(pricer, CDXOptionModelType.ModifiedBlack, netValue);
      TestCallPutParity(pricer, CDXOptionModelType.FullSpread, netValue);
      return;
    }

    private void TestCallPutParity(CreditIndexOptionPricer pricer0,
      CDXOptionModelType model, double expect)
    {
      var asOf = pricer0.AsOf;
      var pricer = pricer0.CDXOption.CreatePricer(asOf, asOf,
        pricer0.DiscountCurve, GetMarketQuote(pricer0),
        pricer0.IndexSettleDate,
        pricer0.MarketRecoveryRate,
        pricer0.BasketSize, pricer0.SurvivalCurves,
        model, GetModelData(),
        pricer0.VolatilitySurface, 1, null);
      pricer.SetIndexFactorAndLosses(pricer0.CurrentFactor, pricer0.ExistingLoss);

      // Call-Put parity
      pricer.CDXOption.Type = OptionType.Call;
      var call = pricer.CalculateFairPrice(0.7);
      pricer.CDXOption.Type = OptionType.Put;
      var put = pricer.CalculateFairPrice(0.7);
      var parity = put - call;
      Assert.AreEqual(expect, parity, 1E-15, 
        String.Format("Call-Put Parity - {0}", model));
    }

    private static CreditIndexOptionPricer GetPricerWithHomogenousBasket(
      bool marketOnly)
    {
      Dt cdxEffective = new Dt(20130920), cdxMaturity = new Dt(20171220),
        optionEffective = new Dt(20150320), optionExpiry = new Dt(20150620),
        asOf = new Dt(20150415);
      int basketSize = 100;
      double premium = 500.0 / 10000, recovery = 0.30,
        strike = 108.0 / 100, volatility = 0.5;
      var marketQuote = new MarketQuote(108.0/100, QuotingConvention.FlatPrice);
      var cdxo = new CDXOption(optionEffective, optionExpiry,
        cdxEffective, cdxMaturity, Currency.USD, premium, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.NYB,
        PayerReceiver.Payer, OptionStyle.European, strike, true);
      var discountCurve = new DiscountCurve(asOf, 0.04);
      var cdxPricer = new CDXPricer(cdxo.CDX, asOf, asOf, discountCurve, 0)
      {
        MarketQuote = marketQuote.Value,
        QuotingConvention = marketQuote.Type,
        MarketRecoveryRate = recovery,
      };

      SurvivalCurve[] curves = null;
      if (!marketOnly)
      {
        var survivalCurve = cdxPricer.EquivalentCDSPricer.SurvivalCurve;
        curves = ArrayUtil.Generate(basketSize,
          i => survivalCurve.CloneObjectGraph());
      }
      return (CreditIndexOptionPricer)cdxo.CreatePricer(asOf, asOf,
        discountCurve, marketQuote, asOf + 1, recovery, basketSize, curves,
        CDXOptionModelType.Black, null,
        CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility),
        1, null);
    }

    private static CDXOptionModelData GetModelData()
    {
      var modelData = new CDXOptionModelData();
      modelData.Choice |= CDXOptionModelParam.HandleIndexFactors;
      return modelData;
    }

    private MarketQuote GetMarketQuote(ICreditIndexOptionPricer cdxoPricer)
    {
      var cdxPricer = cdxoPricer.GetPricerForUnderlying();
      return new MarketQuote(cdxPricer.MarketQuote,cdxPricer.QuotingConvention);
    }
  }
}
