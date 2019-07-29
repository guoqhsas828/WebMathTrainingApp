//
// StandardProductTermsDefaults.cs
//   2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Definitions of build-in <see cref="IStandardProductTerms">Standard Product Terms</see>.
  /// </summary>
  /// <remarks>
  ///   <para>Used by <see cref="StandardProductTermsCache"/>.</para>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="StandardProductTermsCache"/>
  public class StandardProductTermsDefaults
  {
    /// <summary>
    /// Create built-in standard product terms
    /// </summary>
    /// <param name="cache">Standard Products cache</param>
    public static void Initialise(StandardTermsCache<IStandardProductTerms> cache)
    {
      // Standard CDS types
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardNorthAmericanCorporate, "SNAC", "Standard North American Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+NYB+TGT", String.Empty),
          new Tuple<Currency, string, string>(Currency.GBP, "LNB", String.Empty),
          new Tuple<Currency, string, string>(Currency.JPY, "LNB+TKB", "TKB"),
          new Tuple<Currency, string, string>(Currency.CHF, "LNB+ZUB", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+NYB+TRB", String.Empty)
        }
        ));
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardEuropeanCorporate, "STEC", "Standard European Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+TGT", String.Empty),
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.GBP, "LNB", String.Empty),
          new Tuple<Currency, string, string>(Currency.JPY, "LNB+TKB", "TKB"),
          new Tuple<Currency, string, string>(Currency.CHF, "LNB+ZUB", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+NYB+TRB", String.Empty)
        }
        ));
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardLatinAmericaCorporateBond, "SLAC", "Standard Latin American Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+NYB+TGT", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+NYB+TRB", String.Empty)
        }
        ));
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardEmergingEuropeanCorporate, "SEEC", "Standard Emerging European Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+TGT", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+TRB", String.Empty)
        }));
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardAsiaCorporate, "STAC", "Standard Asia Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+NYB+TGT", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+NYB+TRB", String.Empty),
          new Tuple<Currency, string, string>(Currency.JPY, "LNB+NYB+TKB", "TKB"),
          new Tuple<Currency, string, string>(Currency.HKD, "LNB+NYB+HKB", String.Empty),
          new Tuple<Currency, string, string>(Currency.SGD, "LNB+NYB+SIB", String.Empty)
        }));
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardAustraliaCorporate, "SAUC", "Standard Australian Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.AUD, "LNB+NYB+SYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB+SYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+NYB+TGT+SYB", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+NYB+TRB+SYB", String.Empty)
        }));
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardNewZealandCorporate, "SNZC", "Standard New Zealand Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.NZD, "LNB+NYB+AUB", String.Empty),
          new Tuple<Currency, string, string>(Currency.AUD, "LNB+NYB+SYB+AUB", String.Empty),
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB+AUB", String.Empty),
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+NYB+TGT+AUB", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+NYB+TRB+AUB", String.Empty)
        }));
      cache.Add(new CdsTerms(CreditDerivativeTransactionType.StandardJapanCorporate, "STJC", "Standard Japanese Corporate CDS",
        new List<Tuple<Currency, string, string>>()
        {
          new Tuple<Currency, string, string>(Currency.JPY, "LNB+NYB+TKB", "TKB"),
          new Tuple<Currency, string, string>(Currency.USD, "LNB+NYB+TKB", String.Empty),
          new Tuple<Currency, string, string>(Currency.EUR, "LNB+NYB+TGT+TKB", String.Empty),
          new Tuple<Currency, string, string>(Currency.CAD, "LNB+NYB+TRB+TKB", String.Empty)
        }));

      // Standard credit index
      cache.Add(new CreditIndexTerms("CDX.EM", Currency.USD, 14, 20040320, 500, Calendar.NYB, 0.25, true));
      cache.Add(new CreditIndexTerms("CDX.NA.HY", Currency.USD, 100, 20030920, 500, Calendar.NYB, 0.30, true));
      cache.Add(new CreditIndexTerms("CDX.NA.IG", Currency.USD, 125, 20030920, 100, Calendar.NYB, 0.40, false));
      cache.Add(new CreditIndexTerms("CDX.LATAM.CORP", Currency.USD, 20, 20040320, 500, Calendar.NYB, 0.25, true));
      cache.Add(new CreditIndexTerms("iTraxx Europe", Currency.EUR, 125, 20040320, 100, Calendar.TGT, 0.40, false));
      cache.Add(new CreditIndexTerms("iTraxx Europe Crossover", Currency.EUR, 50, 20040320, 500, Calendar.TGT, 0.40, false));
      cache.Add(new CreditIndexTerms("iTraxx Europe HiVol", Currency.EUR, 50, 20040320, 500, Calendar.TGT, 0.25, false));
      cache.Add(new CreditIndexTerms("iTraxx SovX CEEMEA", Currency.EUR, 15, 20040320, 500, Calendar.TGT, 0.25, false));
      cache.Add(new CreditIndexTerms("iTraxx Australia", Currency.USD, 25, 20040920, 100, Calendar.SYB, 0.40, false));
      cache.Add(new CreditIndexTerms("iTraxx Japan", Currency.JPY, 50, 20040920, 100, Calendar.TKB, 0.35, false));
      cache.Add(new CreditIndexTerms("iTraxx Asia ex-Japan", Currency.USD, 40, 20040920, 100, Calendar.None, 0.40, false));

      // STIR Futures
      //
      // Deposit futures
      cache.Add(new StirFutureTerms("ICE", "LEU", "ICE 3M Eurodollar Futures", RateFutureType.MoneyMarketCashRate, "EURIBOR", Tenor.ThreeMonths, Currency.EUR, 1e6, 0.5/1e4, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.TGT));
      cache.Add(new StirFutureTerms("ICE", "LSS", "ICE 3M Sterling Futures", RateFutureType.MoneyMarketCashRate, "GBPLIBOR", Tenor.ThreeMonths, Currency.GBP, 5e5, 1.0 / 1e4, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.LNB));
      cache.Add(new StirFutureTerms("SGX", "SEY", "SGX 3M Euroyen Futures", RateFutureType.MoneyMarketCashRate, "JPYLIBOR", Tenor.ThreeMonths, Currency.JPY, 1e8, 0.25/1e4, 625, DayOfMonth.ThirdWednesday, 0, -2, Calendar.TKB));
      cache.Add(new StirFutureTerms("ICE", "LES", "ICE 3M Euroswiss Futures", RateFutureType.MoneyMarketCashRate, "CHFLIBOR", Tenor.ThreeMonths, Currency.CHF, 1e6, 1.0 / 1e4, 25.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.ZUX));
      cache.Add(new StirFutureTerms("CME", "ED", "CME 3M Eurodollar Futures", RateFutureType.MoneyMarketCashRate, "USDLIBOR", Tenor.ThreeMonths, Currency.USD, 1e6, 0.25/1e4, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.NYB));
      cache.Add(new StirFutureTerms("CME", "EM", "CME 1M Eurodollar Futures", RateFutureType.MoneyMarketCashRate, "USDLIBOR", Tenor.OneMonth, Currency.USD, 3e6, 0.25/1e4, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.NYB));
      cache.Add(new StirFutureTerms("TMX", "BAX", "3M Candadian Bankers' Acceptance Futures", RateFutureType.MoneyMarketCashRate, "CDOR", Tenor.ThreeMonths, Currency.CAD, 1e6, 0.25 / 1e4, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.TRB));
      cache.Add(new StirFutureTerms("JSE", "JBAF", "JSE 3M JIBAR Futures", RateFutureType.MoneyMarketCashRate, "JIBAR", Tenor.ThreeMonths, Currency.ZAR, 1e5, 0.1 / 1e4, 2.5, DayOfMonth.ThirdWednesday, 0, 0, Calendar.JOS));

      // Bank Bill Futures
      cache.Add(new StirFutureTerms("ASX", "IR", "ASX 90D Bank Bill Futures", RateFutureType.ASXBankBill, null, Tenor.NinetyDays, Currency.AUD, 1e6, 0.01 / 1e4, 0.0, DayOfMonth.SecondFriday, 0, -1, Calendar.SYB));
      cache.Add(new StirFutureTerms("ASX", "BB", "ASX NZ 90D Bank Bill Futures", RateFutureType.ASXBankBill, null, Tenor.NinetyDays, Currency.NZD, 1e6, 0.01 / 1e4, 0.0, DayOfMonth.FirstWednesdayAfterNinth, 1, 0, Calendar.SYB));
      // OIS STIR Futures
      cache.Add(new StirFutureTerms("ASX", "OI", "ASX 3M OIS Futures", RateFutureType.GeometricAverageRate, "RBAON", Tenor.OneDay, Currency.AUD, 1e6, 0.5/1e4, 12.33, DayOfMonth.SecondThursday, 0, -1, Calendar.SYB));
      // Average rate futures
      cache.Add(new StirFutureTerms("ASX", "IB", "ASX 3M Interbank Cash Rate Futures", RateFutureType.ArithmeticAverageRate, "RBAON", Tenor.OneDay, Currency.AUD, 3e6, 0.5/1e4, 12.33, DayOfMonth.Last, 2, 0, Calendar.SYB));
      cache.Add(new StirFutureTerms("CME", "FF", "CME 30D Fed Funds Futures", RateFutureType.ArithmeticAverageRate, "FedFunds", Tenor.OneDay, Currency.USD, 5e6, 0.25/1e4, 10.4175, DayOfMonth.Last, 1, 0, Calendar.NYB));

      // FX Futures (CME Majors)
      cache.Add(new FxFutureTerms("CME", "AD", "Australian Dollar Futures", Currency.AUD, Currency.USD, 100000.0, 0.0001, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "C1", "Canadian Dollar Futures", Currency.CAD, Currency.USD, 100000.0, 0.0001, 10.0, DayOfMonth.ThirdWednesday, 0, -1, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "E1", "Swiss Franc Futures", Currency.CHF, Currency.USD, 125000.0, 0.0001, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "EC", "Euro FX Futures", Currency.EUR, Currency.USD, 125000.0, 0.00005, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "BP", "British Pound Futures", Currency.GBP, Currency.USD, 62500, 0.0001, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "J1", "Japanese Yen Futures", Currency.JPY, Currency.USD, 12500000.0, 0.0000005, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "UN", "Norwegian Krone Futures", Currency.NOK, Currency.USD, 2000000.0, 0.00001, 20.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "NE", "New Zealand Dollar Futures", Currency.NZD, Currency.USD, 100000.0, 0.0001, 5.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "SE", "Swedish Krona Futures", Currency.SEK, Currency.USD, 2000000.0, 0.00001, 20.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "J7", "E-mini Japenese Yen Futures", Currency.JPY, Currency.USD, 6250000.0, 0.000001, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "E7", "E-mini Euro FX Futures", Currency.EUR, Currency.USD, 62500, 0.0001, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));

      // FX Futures (CME Cross Rates)
      cache.Add(new FxFutureTerms("CME", "AC", "Australian Dollar/Canadian Dollar Futures", Currency.AUD, Currency.CAD, 200000.0, 0.0001, 20.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "AJ", "Australian Dollar/Japenese Yen Futures", Currency.AUD, Currency.JPY, 200000.0, 0.01, 2000.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "AN", "Australian Dollar/New Zealand Dollar Futures", Currency.AUD, Currency.NZD, 200000.0, 0.0001, 20.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "CY", "Canadian Dollar/Japenese Yen Futures", Currency.CAD, Currency.JPY, 200000.0, 0.0001, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "SJ", "Swiss Franc/Japenese Yen Futures", Currency.CHF, Currency.JPY, 250000.0, 0.005, 1250.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "CA", "Euro/Australian Dollar Futures", Currency.EUR, Currency.AUD, 125000.0, 0.0001, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "CC", "Euro/Canadian Dollar Futures", Currency.EUR, Currency.CAD, 125000.0, 0.0001, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "RF", "Euro/Swiss Franc Futures", Currency.EUR, Currency.CHF, 125000.0, 0.0001, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "RP", "Euro/British Pound Futures", Currency.EUR, Currency.GBP, 125000.0, 0.00005, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "RY", "Euro/Japenese Yen Futures", Currency.EUR, Currency.JPY, 125000.0, 0.01, 1250.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "CN", "Euro/Norwegian Krone Futures", Currency.EUR, Currency.NOK, 125000.0, 0.0005, 62.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "KE", "Euro/Swedish Krona Futures", Currency.EUR, Currency.SEK, 125000.0, 0.0005, 62.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "BF", "British Pound/Swiss Franc Futures", Currency.GBP, Currency.CHF, 125000.0, 0.0001, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "BY", "British Pound/Japenese Yen Futures", Currency.GBP, Currency.JPY, 125000.0, 0.01, 1250.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      // FX Futures (CME E-micro contracts)
      cache.Add(new FxFutureTerms("CME", "M6A", "E-micro Australian Dollar/American Dollar Futures", Currency.AUD, Currency.USD, 10000.0, 0.0001, 1.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "MCD", "E-micro Canadian Dollar/American Dollar Futures", Currency.CAD, Currency.USD, 10000.0, 0.0001, 1.0, DayOfMonth.ThirdWednesday, 0, -1, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "MSF", "E-micro Swiss Franc/American Dollar Futures", Currency.CHF, Currency.USD, 12500.0, 0.0001, 1.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "M6E", "E-micro Euro/American Dollar Futures", Currency.EUR, Currency.USD, 12500.0, 0.0001, 0.625, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "M6B", "E-micro British Pound/American Dollar Futures", Currency.GBP, Currency.USD, 6250, 0.0001, 6.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "MIR", "E-micro Indian Rupee/USD Futures", Currency.INR, Currency.USD, 1000000.0, 0.0001, 1.00, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "MJY", "E-micro Japanese Yen/American Dollar Futures", Currency.JPY, Currency.USD, 1250000.0, 0.000001, 1.25, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "M6S", "E-micro American Dollar/Swiss Franc Futures  Contract", Currency.USD, Currency.CHF, 10000.0, 0.0001, 1.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "MNH", "E-micro Size USD/Offshore RMB (CNH) Futures", Currency.USD, Currency.CNH, 10000.0, 0.0001, 1.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.HKB));
      cache.Add(new FxFutureTerms("CME", "M6J", "E-micro American Dollar/Japenese Yen Futures", Currency.USD, Currency.JPY, 10000.0, 0.01, 100.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      // FX Futures (CME E-micro contracts)
      cache.Add(new FxFutureTerms("CME", "BR", "Brazilian Real Futures", Currency.BRL, Currency.USD, 100000.0, 0.00005, 5.0, DayOfMonth.Last, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "K", "Czech Koruna/Euro (CZK/EUR) Cross Rate Futures", Currency.CZK, Currency.EUR, 4000000.0, 0.000002, 8.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "CZ", "Czech Koruna Futures", Currency.CZK, Currency.USD, 4000000.0, 0.000002, 8.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "TRE", "Turkish Lira Euro Futures", Currency.TRL, Currency.EUR, 125000.0, 0.0001, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "R", "Hungarian Forint/Euro (HUF/EUR) Cross Rate Futures", Currency.HUF, Currency.EUR, 30000000.0, 0.0000002, 6.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "FR", "Hungarian Forint Futures", Currency.HUF, Currency.USD, 30000000.0, 0.0000002, 6.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "IS", "Israeli Shekel Futures", Currency.ILS, Currency.USD, 1000000.0, 0.00001, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.HKB));
      cache.Add(new FxFutureTerms("CME", "SIR", "Indian Rupee/USD Futures", Currency.INR, Currency.USD, 5000000.0, 0.0001, 5.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "KRW", "Korean Won Futures", Currency.KRW, Currency.USD, 125000000.0, 0.0000001, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "MP", "Mexican Peso Futures", Currency.MXN, Currency.USD, 500000, 0.00001, 5.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "Z", "Polish Zloty/Euro (PLN/EUR) Cross Rate Futures", Currency.PLN, Currency.EUR, 500000, 0.00002, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "PZ", "Polish Zloty Futures", Currency.PLN, Currency.USD, 500000, 0.00002, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "RME", "Chinese Renminbi/Euro Futures", Currency.CNH, Currency.EUR, 1000000.0, 0.00001, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.BEB));
      cache.Add(new FxFutureTerms("CME", "RMB", "Chinese Renminbi/USD Futures", Currency.CNH, Currency.USD, 1000000.0, 0.00001, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.BEB));
      cache.Add(new FxFutureTerms("CME", "CHL", "US Dollar/Chilean Peso Futures", Currency.USD, Currency.CLP, 100000.0, 0.01, 1.0, DayOfMonth.Last, 0, -1, Calendar.SAB));
      cache.Add(new FxFutureTerms("CME", "CNH", "Standard-Size USD/Offshore RMB (CNH) Futures", Currency.USD, Currency.CNH, 100000.0, 0.0001, 10.0, DayOfMonth.ThirdWednesday, 0, -2, Calendar.BEB));
      cache.Add(new FxFutureTerms("CME", "TRY", "Turkish Lira Futures", Currency.USD, Currency.TRY, 200000.0, 0.0001, 20, DayOfMonth.ThirdWednesday, 0, -1, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "ZAR", "U.S. Dollar/South African Rand Futures", Currency.USD, Currency.ZAR, 100000.0, 0.0001, 10, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));
      cache.Add(new FxFutureTerms("CME", "RA", "South African Rand Futures", Currency.ZAR, Currency.USD, 500000.0, 0.000025, 12.5, DayOfMonth.ThirdWednesday, 0, -2, Calendar.CMG));

      // Bond Futures
      cache.Add(new BondFutureTerms("EUREX", "FGBL", "Euro-Buxl(R) Futures (FGBX)", Currency.EUR, 0.06, FuturesQuotingConvention.Price, 10, 100000.0, 0.01, 10, DayOfMonth.Tenth, 0, -2, Calendar.TGT));
      cache.Add(new BondFutureTerms("EUREX", "XT", "ASX 10 Year Treasury Bond Futures", Currency.AUD, 0.06, FuturesQuotingConvention.IndexYield, 10, 100000.0, 0.01, 10, DayOfMonth.Fifteenth, 0, -2, Calendar.SYB));

      // Commodity Futures
      // Brent 
      // For contract months up to and including the February 2016 contract, trading shall cease one business day prior to the termination of the Brent futures contract, i.e., two business days before the fifteenth calendar day prior to the first day of the delivery month, if the fifteenth calendar day is not a holiday or weekend in London. If the fifteenth calendar day is a holiday or weekend in London, trading shall end three business days prior to the last business day preceding the fifteenth calendar day. Effective with the March 2016 contract, the last trading day will be the business day preceding the last UK business day of the second month preceding the contract month, except for the business day preceding New Year's Day, where trading shall cease on the third UK business day preceding New Year's Day. 
      cache.Add(new CommodityFutureTerms("CME", "BB", "Brent Crude Oil Futures", Currency.USD, "BRENT", 1000.0, 0.01, 10, DayOfMonth.Last, 0, DayOfMonth.Last, 0, -2, new Calendar("NYB+LNB")));
      cache.Add(new CommodityFutureTerms("CME", "RB", "RBOB Gasoline Futures", Currency.USD, "RBOB", 42000.0, 0.0001, 4.2, DayOfMonth.Last, 0, DayOfMonth.Last, 0, -1, Calendar.NYB));
      cache.Add(new CommodityFutureTerms("ICE", "B", "ICE Brent Futures", Currency.USD, "BRENT", 1000.0, 0.01, 10, DayOfMonth.Last, 0, DayOfMonth.Last, -2, -2, Calendar.NYB));
      cache.Add(new CommodityFutureTerms("CME", "CL", "Crude Oil Futures", Currency.USD, "WTI", 1000.0, 0.01, 10, DayOfMonth.Last, 0, DayOfMonth.TwentyFifth, -3, -1, Calendar.NYB));

      // Equity Futures
      cache.Add(new StockFutureTerms("CME", "ES", "E-mini S&P 500 Futures", Currency.USD, "SAP", 50.0, 0.25, 12.50, DayOfMonth.ThirdFriday, 0, 0, Calendar.NYB));
      cache.Add(new StockFutureTerms("CME", "ENY", "E-mini Nikkei 225 Futures", Currency.JPY, "N225", 100.0, 10.0, 1000.0, DayOfMonth.SecondFriday, 0, 0, Calendar.TKB));
      cache.Add(new StockFutureTerms("CME", "FT1", "E-mini FTSE 100 Futures", Currency.GBP, "FTSE", 10.0, 0.5, 5.0, DayOfMonth.ThirdFriday, 0, 0, Calendar.LNB));

      // Swaps
      //
      // OIS Swaps
      cache.Add(new SwapTerms("USD OIS Swap", Currency.USD, 1, DayCount.Actual360, Calendar.NYB, "FEDFUNDS", ProjectionType.ArithmeticAverageRate, 1, CompoundingConvention.None));
      cache.Add(new SwapTerms("EUR OIS Swap", Currency.EUR, 0, DayCount.Actual360, Calendar.TGT, "EONIA", ProjectionType.GeometricAverageRate, 0, CompoundingConvention.None));
      cache.Add(new SwapTerms("GBP OIS Swap", Currency.GBP, 0, DayCount.Actual365Fixed, Calendar.LNB, "SONIA", ProjectionType.GeometricAverageRate, 0, CompoundingConvention.None));
      cache.Add(new SwapTerms("JPY OIS Swap", Currency.JPY, 2, DayCount.Actual365Fixed, Calendar.TKB, "TONAR", ProjectionType.GeometricAverageRate, 2, CompoundingConvention.None));
      cache.Add(new SwapTerms("AUD OIS Swap", Currency.AUD, 0, DayCount.Actual365Fixed, Calendar.SYB, "RBAON", ProjectionType.GeometricAverageRate, 1, CompoundingConvention.None));
      cache.Add(new SwapTerms("CAD OIS Swap", Currency.CAD, 1, DayCount.Actual365Fixed, Calendar.TRB, "CORRA", ProjectionType.GeometricAverageRate, 0, CompoundingConvention.None));
      cache.Add(new SwapTerms("SGD OIS Swap", Currency.SGD, 2, DayCount.Actual365Fixed, Calendar.SIB, "SONAR", ProjectionType.GeometricAverageRate, 2, CompoundingConvention.None));
      cache.Add(new SwapTerms("ZAR OIS Swap", Currency.ZAR, 0, DayCount.Actual365Fixed, Calendar.JOB, "SAFEX", ProjectionType.GeometricAverageRate, 0, CompoundingConvention.None));
      cache.Add(new SwapTerms("ZAR OIS Swap", Currency.ZAR, 0, DayCount.Actual365Fixed, Calendar.JOB, "SAONIA", ProjectionType.GeometricAverageRate, 0, CompoundingConvention.None));

      // Vanilla Swaps
      cache.Add(new SwapTerms("USD Swap", Currency.USD, Currency.USD, 2, DayCount.Thirty360, new Calendar("NYB+LNB"), Frequency.SemiAnnual, "USDLIBOR", Frequency.Quarterly));
      cache.Add(new SwapTerms("USD Swap (London)", Currency.GBP, Currency.USD, 2, DayCount.Actual360, new Calendar("NYB+LNB"), Frequency.Annual, "USDLIBOR", Frequency.Quarterly));
      cache.Add(new SwapTerms("EUR 3M Swap", Currency.EUR, Currency.EUR, "EURIBOR", Tenor.ThreeMonths, 
        DayCount.Thirty360, BDConvention.Modified, Calendar.TGT, Frequency.Annual, 
        Frequency.SemiAnnual, CompoundingConvention.FlatISDA, ProjectionType.SimpleProjection, 2, 0, false));
      cache.Add(new SwapTerms("EUR 6M Swap", Currency.EUR, Currency.EUR, "EURIBOR", Tenor.SixMonths,
        DayCount.Thirty360, BDConvention.Modified, Calendar.TGT, Frequency.Annual,
        Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection, 2, 0, false));

      cache.Add(new SwapTerms("GBP 3M Swap", Currency.GBP, Currency.GBP, "GBPLIBOR", Tenor.ThreeMonths,
        DayCount.Actual365Fixed, BDConvention.Modified, Calendar.LNB, Frequency.Annual,
        Frequency.Quarterly, CompoundingConvention.FlatISDA, ProjectionType.SimpleProjection, 0, 0, false));
      cache.Add(new SwapTerms("GBP 6M Swap", Currency.GBP, Currency.GBP, "GBPLIBOR", Tenor.SixMonths,
        DayCount.Actual365Fixed, BDConvention.Modified, Calendar.LNB, Frequency.SemiAnnual,
        Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, 0, false));

      cache.Add(new SwapTerms("JPY Swap", Currency.JPY, Currency.JPY, 2, DayCount.Actual365Fixed, Calendar.TKB, Frequency.Annual, "TIBOR", Frequency.Quarterly));
      cache.Add(new SwapTerms("JPY LIBOR Swap", Currency.JPY, Currency.JPY, 2, DayCount.Actual365Fixed, Calendar.TKB, Frequency.SemiAnnual, "JPYLIBOR", Frequency.SemiAnnual));
      //StandardProductTermsCache.Add(new SwapTerms("CHF Swap", Currency.CHF, Currency.CHF, 2, Tenor.OneYear, DayCount.Thirty360, BDConvention.Modified, Calendar.TGT, new []{Frequency.Annual, Frequency.Annual}, "CHFLIBOR", null, new[] {Frequency.Quarterly, Frequency.SemiAnnual}, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false));
      cache.Add(new SwapTerms("CHF Swap", Currency.CHF, Currency.CHF, 2, DayCount.Thirty360, Calendar.ZUB, Frequency.Annual, "CHFLIBOR", Frequency.SemiAnnual));
      cache.Add(new SwapTerms("DKK Swap", Currency.DKK, Currency.DKK, 2, DayCount.Thirty360, Calendar.DKB, Frequency.Annual, "CIBOR", Frequency.SemiAnnual));
      cache.Add(new SwapTerms("NOK Swap", Currency.NOK, Currency.NOK, 2, DayCount.Thirty360, Calendar.OSB, Frequency.Annual, "NIBOR", Frequency.SemiAnnual));
      cache.Add(new SwapTerms("PLN Swap", Currency.PLN, Currency.PLN, 0, DayCount.ActualActual, Calendar.PAB, Frequency.Annual, "WIBOR", Frequency.SemiAnnual));
      cache.Add(new SwapTerms("AUD 3M Swap", Currency.AUD, Currency.AUD, "BBSW", Tenor.ThreeMonths,
        DayCount.Actual365Fixed, BDConvention.Modified, Calendar.SYB, Frequency.Quarterly,
        Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 1, 0, false));
      cache.Add(new SwapTerms("AUD 6M Swap", Currency.AUD, Currency.AUD, "BBSW", Tenor.SixMonths,
        DayCount.Actual365Fixed, BDConvention.Modified, Calendar.SYB, Frequency.SemiAnnual,
        Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection, 1, 0, false));
      cache.Add(new SwapTerms("ZAR 3M Swap", Currency.ZAR, Currency.ZAR, "JIBAR", Tenor.ThreeMonths,
        DayCount.Actual365Fixed, BDConvention.Modified, Calendar.JOB, Frequency.Quarterly,
        Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, 0, false));

      cache.Add(new SwapTerms("HKD Swap", Currency.HKD, Currency.HKD, 0, DayCount.Actual365Fixed, Calendar.HKB, Frequency.Quarterly, "HIBOR", Frequency.None));
      cache.Add(new SwapTerms("NZD Swap", Currency.NZD, Currency.NZD, 0, DayCount.Actual365Fixed, Calendar.AUB, Frequency.SemiAnnual, "BKBM", Frequency.Quarterly));
      cache.Add(new SwapTerms("CAD Swap", Currency.CAD, Currency.CAD, 0, DayCount.Actual365Fixed, Calendar.TRB, Frequency.SemiAnnual, "CDOR", Frequency.SemiAnnual, Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None));
      cache.Add(new SwapTerms("MXN Swap", Currency.MXN, Currency.MXN, 1, DayCount.Actual360, Calendar.MXB, Frequency.TwentyEightDays, "MXIBTIIE", Frequency.TwentyEightDays));

      // Basis swaps
      cache.Add(new SwapTerms("FedFunds/3M LIBOR Basis Swap", Currency.USD, 2, true,
        new Calendar("NYB"), new Calendar("NYB+LNB"),
        BDConvention.Modified,
        "FEDFUNDS", Tenor.OneDay, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.ArithmeticAverageRate,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false));
      cache.Add(new SwapTerms("6M LIBOR/3M LIBOR Basis Swap", Currency.USD, 2, false, 
        new Calendar("NYB+LNB"), new Calendar("NYB+LNB"),
        BDConvention.Modified,
        "USDLIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.SemiAnnual, CompoundingConvention.ISDA, ProjectionType.SimpleProjection, 0, false));
      cache.Add(new SwapTerms("EONIA/3M LIBOR Basis Swap", Currency.EUR, 2, true, Calendar.TGT, Calendar.TGT, BDConvention.Modified,
        "EONIA", Tenor.OneDay, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.GeometricAverageRate,
        "EURIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false));
      cache.Add(new SwapTerms("6M LIBOR/3M LIBOR Basis Swap", Currency.EUR, 2, true, Calendar.TGT, Calendar.TGT, BDConvention.Modified,
       "EURIBOR", Tenor.ThreeMonths, Frequency.SemiAnnual, CompoundingConvention.ISDA, ProjectionType.SimpleProjection,
        "EURIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false));
      cache.Add(new SwapTerms("SONIA/3M GBPLIBOR Basis Swap", Currency.GBP, 0, true, Calendar.LNB, Calendar.LNB, BDConvention.Modified,
        "SONIA", Tenor.OneDay, Frequency.Quarterly, CompoundingConvention.ISDA, ProjectionType.GeometricAverageRate,
        "GBPLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false));
      cache.Add(new SwapTerms("6M GBPLIBOR/3M GBPLIBOR Basis Swap", Currency.GBP, 0, false, Calendar.LNB, Calendar.LNB, BDConvention.Modified,
        "GBPLIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "GBPLIBOR", Tenor.ThreeMonths, Frequency.SemiAnnual, CompoundingConvention.ISDA, ProjectionType.SimpleProjection, 0, false));
      cache.Add(new SwapTerms("TONAR/6M LIBOR Basis Swap", Currency.JPY, 2, true, Calendar.TKB, Calendar.TKB, BDConvention.Modified,
        "TONAR", Tenor.OneDay, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.GeometricAverageRate,
        "JPYLIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false));

      // Cross Currency Basis Swaps
      cache.Add(new SwapTerms("6M EURIBOR/3M USDLIBOR Basis Swap", Currency.EUR, Currency.USD, 2, true, 
        Calendar.Parse("TGT+NYB+LNB"), BDConvention.Modified,
        "EURIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));
      cache.Add(new SwapTerms("6M GBPLIBOR/3M USDLIBOR Basis Swap", Currency.GBP, Currency.USD, 2, true, 
        Calendar.Parse("LNB+NYB"), BDConvention.Modified,
        "GBPLIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));
      cache.Add(new SwapTerms("3M USDLIBOR/6M CHFLIBOR Basis Swap", Currency.USD, Currency.JPY, 2, false, 
        Calendar.Parse("NYB+TKB+LNB"), BDConvention.Modified,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "JPYLIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));
      cache.Add(new SwapTerms("3M USDLIBOR/6M CHFLIBOR Basis Swap", Currency.USD, Currency.CHF, 2, false, 
        Calendar.Parse("NYB+ZUB+LNB"), BDConvention.Modified,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "CHFLIBOR", Tenor.SixMonths, Frequency.SemiAnnual, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));
      cache.Add(new SwapTerms("3M BBSW/3M USDLIBOR Basis Swap", Currency.AUD, Currency.USD, 2, true, 
        Calendar.Parse("SYB+NYB+LNB"), BDConvention.Modified,
        "BBSW", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));
      cache.Add(new SwapTerms("3M CDOR/3M USDLIBOR Basis Swap", Currency.USD, Currency.CAD, 1, false, 
        Calendar.Parse("NYB+TRB+LNB"), BDConvention.Modified,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "CDOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));
      cache.Add(new SwapTerms("3M USDLIBOR/28D MXIBTIIE Basis Swap", Currency.USD, Currency.MXN, 2, false, 
        Calendar.Parse("NYB+MXB+LNB"), BDConvention.Modified,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "MXIBTIIE", Tenor.TwentyEightDays, Frequency.TwentyEightDays, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));
      cache.Add(new SwapTerms("3M USDLIBOR/3M JIBAR Basis Swap", Currency.USD, Currency.ZAR, 2, false,
        Calendar.Parse("NYB+JOB+LNB"), BDConvention.Modified,
        "USDLIBOR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection,
        "JIBAR", Tenor.ThreeMonths, Frequency.Quarterly, CompoundingConvention.None, ProjectionType.SimpleProjection, 0, false, true));

      // Inflation ZC Swaps
      cache.Add(new InflationSwapTerms("USD CPI Zero Coupon Swap", Currency.USD, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.NYB, Frequency.None, CompoundingConvention.None, "CPI_USD", ProjectionType.InflationRate, IndexationMethod.CanadianMethod, Tenor.ThreeMonths, 2, true, false));
      cache.Add(new InflationSwapTerms("EUR HICP Zero Coupon Swap", Currency.EUR, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.TGT, Frequency.None, CompoundingConvention.None, "HICP_EUR", ProjectionType.InflationRate, IndexationMethod.UKGilt_OldStyle, Tenor.ThreeMonths, 2, true, false));
      cache.Add(new InflationSwapTerms("EUR CPI Zero Coupon Swap", Currency.EUR, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.TGT, Frequency.None, CompoundingConvention.None, "CPI_FR", ProjectionType.InflationRate, IndexationMethod.CanadianMethod, Tenor.ThreeMonths, 2, true, false));
      cache.Add(new InflationSwapTerms("GBP RPI Zero Coupon Swap", Currency.GBP, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.LNB, Frequency.Annual, CompoundingConvention.None, "RPI_GBP", ProjectionType.InflationRate, IndexationMethod.UKGilt_OldStyle, Tenor.TwoMonths, 2, true, false));

      cache.Add(new InflationSwapTerms("USD CPI Year-on-Year Coupon Swap", Currency.USD, Currency.USD, Frequency.Annual, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.NYB, Frequency.None, CompoundingConvention.None, "CPI_USD", ProjectionType.InflationRate, IndexationMethod.CanadianMethod, Tenor.ThreeMonths, 0, true, false));
      cache.Add(new InflationSwapTerms("EUR HICP Year-on-Year Coupon Swap", Currency.EUR, Currency.EUR, Frequency.Annual, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.TGT, Frequency.None, CompoundingConvention.None, "HICP_EUR", ProjectionType.InflationRate, IndexationMethod.UKGilt_OldStyle, Tenor.ThreeMonths, 0, true, false));
      cache.Add(new InflationSwapTerms("EUR CPI Year-on-Year Coupon Swap", Currency.EUR, Currency.EUR, Frequency.Annual, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.TGT, Frequency.None, CompoundingConvention.None, "CPI_FR", ProjectionType.InflationRate, IndexationMethod.CanadianMethod, Tenor.ThreeMonths, 0, true, false));
      cache.Add(new InflationSwapTerms("GBP RPI Year-on-Year Coupon Swap", Currency.GBP, Currency.GBP, Frequency.Annual, 2, DayCount.Actual360,
        BDConvention.Modified, Calendar.LNB, Frequency.None, CompoundingConvention.None, "RPI_GBP", ProjectionType.InflationRate, IndexationMethod.UKGilt_OldStyle, Tenor.TwoMonths, 0, true, false));
    }
  }
}
