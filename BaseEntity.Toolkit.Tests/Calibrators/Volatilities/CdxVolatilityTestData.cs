// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  internal class CdxVolatilityTestData
  {
    #region Methods

    public static CdxVolatilityTestData Get(string irDataFile)
    {
      var data = new CdxVolatilityTestData();
      data.BuildIrCurves(irDataFile);
      return data;
    }

    public IEnumerable<Quote> EnumerateVolatilityQuotes(
      string volatilityQuoteDataFile)
    {
      return File.ReadLines(volatilityQuoteDataFile).Skip(1)
        .Select(r => Quote.Get(r.Split('\t')));
    }

    public ICreditIndexOptionPricer GetPricer(Quote q,
      PayerReceiver type, CDXOptionModelType model,
      CDXOptionModelData md = null)
    {
      return CreatePricer(q, type, model, md,
        model == CDXOptionModelType.BlackPrice
          ? q.PriceVolatility
          : q.SpreadVolatility);
    }

    private ICreditIndexOptionPricer CreatePricer(Quote q,
      PayerReceiver type, CDXOptionModelType model, CDXOptionModelData md,
      double volatility)
    {
      return CreatePricer(q.TradeDate, q.ExpiryDate, q.IndexName, type,
        q.StrikePrice > 0,
        q.StrikePrice > 0 ? q.StrikePrice : q.StrikeSpread,
        q.ReferencePrice > 0,
        q.ReferencePrice > 0 ? q.ReferencePrice : q.ReferenceSpread,
        model, md, volatility);
    }

    private ICreditIndexOptionPricer CreatePricer(
      Dt tradeDt, Dt expiryDt, string cdxname, PayerReceiver type,
      bool strikeIsPrice, double strike,
      bool quoteIsPrice, double quote,
      CDXOptionModelType model, CDXOptionModelData md,
      double volatility)
    {
      if (md == null)
      {
        md = new CDXOptionModelData();
        md.Choice |= CDXOptionModelParam.UseProtectionPvForFrontEnd
          | CDXOptionModelParam.MarketPayoffConsistent;
      }

      // Create option
      var cdx = Lookup(cdxname);
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
      var settle = 0 != (md.Choice & CDXOptionModelParam.MarketPayoffConsistent)
        ? Dt.AddDays(tradeDt, 3, cdx.Calendar)
        : Dt.Add(tradeDt, 1);
      var indexSettle = Dt.Empty;
      var dc = GetDicountCurve(Dt.AddDays(tradeDt, -2, cdx.Calendar));
      var marketQuote = new MarketQuote(quote, quoteIsPrice
        ? QuotingConvention.FlatPrice
        : QuotingConvention.CreditSpread);
      return cdxo.CreatePricer(tradeDt, settle, dc, marketQuote, indexSettle,
        cdx.RecoveryRate, cdx.EntityCount, null, model, md,
        CalibratedVolatilitySurface.FromFlatVolatility(tradeDt, volatility),
        1.0, null);
    }


    public DiscountCurve GetDicountCurve(Dt asOf)
    {
      OnDemandDiscountCurve curve;
      if (_irdata.TryGetValue(asOf, out curve))
        return curve;
      //Trace.WriteLine(String.Format(
      //  "Discount curve nor found for date {0}", asOf));
      var minDt = _irdata.Keys.Min();
      for (var dt = asOf; dt > minDt;)
      {
        dt = Dt.Add(dt, -1);
        if (_irdata.TryGetValue(dt, out curve))
        {
          //Trace.WriteLine(String.Format(
          //  "    Use the curve for date {0}", dt));
          return curve;
        }
      }
      throw new ApplicationException(String.Format(
        "Discount curve nor found for date {0}", asOf));
    }

    public CreditIndexDefinition Lookup(string name)
    {
      return name.Replace(' ', '.').LookUpCreditIndexDefinition();
    }

    #endregion

    #region Build Ir Curves

    // Intermediate objects
    private Dictionary<Dt, OnDemandDiscountCurve> _irdata
      = new Dictionary<Dt, OnDemandDiscountCurve>();

    private class OnDemandDiscountCurve
    {
      private readonly string[] _tenors;
      private readonly string[] _types;
      private readonly double[] _quotes;
      private DiscountCurve _curve;

      private DiscountCurve GetDiscountCurve()
      {
        if (_curve == null)
        {
          var term = RateCurveTermsUtil.CreateDefaultCurveTerms("USDLIBOR_3M");
          _curve = DiscountCurveFitCalibrator.DiscountCurveFit(AsOf,
            term, String.Format("USD_LIBOR_{0}", AsOf.ToInt()),
            _quotes, _types, _tenors,
            new CalibratorSettings {CurveAsOf = AsOf});
        }
        return _curve;
      }

      public OnDemandDiscountCurve(string[][] headers, string[] quotes)
      {
        _tenors = headers[0];
        _types = headers[1];
        AsOf = quotes[0].ParseDt();
        _quotes = quotes.Skip(1).Select(Double.Parse).ToArray();
      }

      public static implicit operator DiscountCurve(OnDemandDiscountCurve curve)
      {
        return curve.GetDiscountCurve();
      }

      public Dt AsOf { get; }
    }

    private void BuildIrCurves(string rateDataFile)
    {
      var enumerator = File.ReadLines(rateDataFile).GetEnumerator();
      if (!enumerator.MoveNext())
      {
        throw new ApplicationException(String.Format(
          "File is empty: {0}", rateDataFile));
      }
      var instruments = ParseInstruments(enumerator.Current.Split('\t'));
      while (enumerator.MoveNext())
      {
        var curve = new OnDemandDiscountCurve(instruments,
          enumerator.Current.Split('\t'));
        _irdata.Add(curve.AsOf, curve);
      }
    }

    private static string[][] ParseInstruments(string[] names)
    {
      const string pattern = @"^US0+(?:(O/N)|([1-9]\d?[DMWY]))|^USSWAP(\d+)";
      var count = names.Length - 1;
      var tenors = new string[count];
      var types = new string[count];
      for (int i = 0; i < count; ++i)
      {
        var m = Regex.Match(names[i + 1], pattern);
        if (!m.Success)
        {
          throw new ApplicationException(String.Format(
            "Unknown instrument: {0}", names[i + 1]));
        }
        var isSwap = !String.IsNullOrEmpty(m.Groups[3].Value);
        tenors[i] = String.IsNullOrEmpty(m.Groups[1].Value)
          ? (isSwap ? (m.Groups[3].Value + 'Y') : m.Groups[2].Value)
          : "1D";
        types[i] = isSwap ? "Swap" : "MM";
      }
      return new[] {tenors, types};
    }

    #endregion

    #region Volatility quote

    internal class Quote
    {
      public readonly string IndexName;
      public readonly Dt TradeDate, ExpiryDate;

      public readonly double ReferencePrice,
        StrikePrice,
        ReferenceSpread,
        StrikeSpread,
        PriceVolatility,
        SpreadVolatility;

      public readonly Price Payer, Receiver;

      public static Quote Get(string[] line)
      {
        return new Quote(line);
      }

      private Quote(string[] row)
      {
        const int Date = 0,
          ExMonth = 1,
          ExYear = 2,
          Index = 3,
          Series = 4,
          Ref_Price = 5,
          Ref_Spread = 6,
          Strike_Price = 7,
          Strike_Spread = 8,
          P_Bid = 9,
          P_Ask = 10,
          R_Bid = 11,
          R_Ask = 12,
          Vol_Price = 13,
          Vol_Spread = 14;

        TradeDate = row[Date].ParseDt();
        ExpiryDate = GetExpiry(row[ExMonth], Int32.Parse(row[ExYear]));
        IndexName = row[Index].Replace("CDX", "CDX NA")
          + ' ' + row[Series] + " 5Y";
        ReferencePrice = row[Ref_Price].ParseDouble() / 100;
        StrikePrice = row[Strike_Price].ParseDouble() / 100;
        ReferenceSpread = row[Ref_Spread].ParseDouble() / 10000;
        StrikeSpread = row[Strike_Spread].ParseDouble() / 10000;
        Payer = new Price(row[P_Bid].ParseDouble() / 10000,
          row[P_Ask].ParseDouble() / 10000);
        Receiver = new Price(row[R_Bid].ParseDouble() / 10000,
          row[R_Ask].ParseDouble() / 10000);
        PriceVolatility = row[Vol_Price].ParseDouble();
        SpreadVolatility = row[Vol_Spread].ParseDouble();
      }

      private static Dt GetExpiry(string month, int year)
      {
        var m = (int)typeof(Month).GetFields()
          .First(f => f.Name.StartsWith(month, StringComparison.OrdinalIgnoreCase))
          .GetValue(null);
        return Dt.ImmDate(m, year);
      }


      public struct Price
      {
        public readonly double Bid, Ask;

        public Price(double bid, double ask)
        {
          Bid = bid;
          Ask = ask;
        }

        public double Mid => (Bid + Ask) / 2;
      }
    }

    #endregion
  }
}
