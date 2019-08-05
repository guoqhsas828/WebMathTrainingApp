// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  public static class CdxOptionTestUtility
  {
    internal static ICreditIndexOptionPricer CreatePricer(CdxVolatilityTestData data,
      CdxVolatilityTestData.Quote q, PayerReceiver payRec, CDXOptionModelType model, double vol)
    {
      return CreatePricer(data, q.TradeDate, q.ExpiryDate,
          q.IndexName, payRec,
          q.StrikePrice > 0,
          q.StrikePrice > 0 ? q.StrikePrice : q.StrikeSpread,
          q.ReferencePrice > 0,
          q.ReferencePrice > 0 ? q.ReferencePrice : q.ReferenceSpread,
          model, vol);
    }

    private static ICreditIndexOptionPricer CreatePricer(CdxVolatilityTestData inputData,
      Dt tradeDt, Dt expiryDt, string cdxname, PayerReceiver type,
      bool strikeIsPrice, double strike,
      bool quoteIsPrice, double quote,
      CDXOptionModelType model, double volatility)
    {
      var cdx = inputData.Lookup(cdxname);

      // Create option
      var cdxo = new CDXOption(tradeDt, Currency.USD, cdx.CDX,
        expiryDt, type, OptionStyle.European,
        strike, strikeIsPrice)
      {
        Description = String.Format("{0} {1}@{2}/{3}_{4}",
          cdxname, type, strike, expiryDt.ToInt(), tradeDt.ToInt()),
        SettlementType = SettlementType.Cash
      };
      cdxo.Validate();

      // Create pricer
      var settleDt = Dt.AddDays(tradeDt, 3, cdx.Calendar);
      var indexSettle = Dt.Add(tradeDt, 1);
      var dc = inputData.GetDicountCurve(Dt.AddDays(tradeDt, -2, cdx.Calendar));
      var marketQuote = new MarketQuote(quote, quoteIsPrice
        ? QuotingConvention.FlatPrice
        : QuotingConvention.CreditSpread);
      var data = new CDXOptionModelData();
      data.Choice |= CDXOptionModelParam.UseProtectionPvForFrontEnd
        | CDXOptionModelParam.MarketPayoffConsistent;
      return new CreditIndexOptionPricer(cdxo, tradeDt, settleDt, dc,
        marketQuote, indexSettle, cdx.RecoveryRate, 0, null, 1.0, 0.0, model, data,
        CalibratedVolatilitySurface.FromFlatVolatility(tradeDt, volatility), Double.NaN);
    }


  }
}
