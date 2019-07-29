//
//   2015. All rights reserved.
//

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  /// <summary>
  ///   Definitions of build-in <see cref="IReferenceRate">Rate Indices</see>.
  /// </summary>
  /// <seealso cref="IReferenceRate"/>
  public class ReferenceRateDefaults
  {
    /// <summary>
    /// Create built-in rate indices
    /// </summary>
    /// <param name="cache">Reference rate cache</param>
    public static void Initialise(StandardTermsCache<IReferenceRate> cache)
    {
      // FX rates
      cache.Add(new FxReferenceRate("EURUSD", "EURUSD Exchange rate", Currency.EUR, Currency.USD, Calendar.TGT, Calendar.NYB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("GBPUSD", "GBPUSD Exchange rate", Currency.GBP, Currency.USD, Calendar.LNB, Calendar.NYB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("USDJPY", "USDJPY Exchange rate", Currency.USD, Currency.JPY, Calendar.NYB, Calendar.TKB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("USDCHF", "USDCHF Exchange rate", Currency.USD, Currency.CHF, Calendar.NYB, Calendar.ZUB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("EURGBP", "EURGBP Exchange rate", Currency.EUR, Currency.GBP, Calendar.TGT, Calendar.LNB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("GBPCHF", "GBPCHF Exchange rate", Currency.GBP, Currency.CHF,  Calendar.LNB, Calendar.ZUB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("AUDUSD", "AUDUSD Exchange rate", Currency.AUD, Currency.USD, Calendar.SYB, Calendar.NYB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("GBPAUD", "GBPAUD Exchange rate", Currency.GBP, Currency.AUD, Calendar.SYB, Calendar.LNB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("EURAUD", "EURAUD Exchange rate", Currency.EUR, Currency.AUD, Calendar.SYB, Calendar.LNB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("EURCHF", "EURCHF Exchange rate", Currency.EUR, Currency.CHF, Calendar.TGT, Calendar.ZUB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("USDCAD", "USDCAD Exchange rate", Currency.USD, Currency.CAD, Calendar.NYB, Calendar.TRB, 1, BDConvention.Following));
      cache.Add(new FxReferenceRate("USDMXN", "USDMXN Exchange rate", Currency.USD, Currency.MXN, Calendar.NYB, Calendar.MXB, 2, BDConvention.Following));
      cache.Add(new FxReferenceRate("USDZAR", "USDZAR Exchange rate", Currency.USD, Currency.ZAR, Calendar.NYB, Calendar.JOB, 2, BDConvention.Following));

      // RateIndices (OIS-like)
      // Ref: A Guide to the Front-End and Basis Swap Markets. CS 2010
      // Ref: https://www.dnb.no/en/marketrate
      // Ref: https://developers.opengamma.com/quantitative-research/Interest-Rate-Instruments-and-Market-Conventions.pdf
      // Ref: https://www.clarusft.com/ois-swap-nuances/
      cache.Add(new InterestReferenceRate("SARON", "Swiss Average Rate Overnight", Currency.CHF, DayCount.Actual360, Calendar.ZUB, 0));
      // Eonia (Euro OverNight Index Average) is an effective overnight rate computed as a weighted average of all overnight unsecured lending transactions in the interbank market, initiated within the euro area by the contributing banks in the EURIBOR panel. This is an ACT/360 rate and is calculated by the ECB.
      cache.Add(new InterestReferenceRate("EONIA", "Euro Overnight Index Average", Currency.EUR, DayCount.Actual360, Calendar.TGT, 0));
      cache.Add(new InterestReferenceRate("SONIA", "Sterling Overnight Index Average", Currency.GBP, DayCount.Actual365Fixed, Calendar.LNB, 0));
      cache.Add(new InterestReferenceRate("TONAR", "Tokyo Overnight Average Rate", Currency.JPY, DayCount.Actual365Fixed, Calendar.TKB, 1));
      // The daily effective federal funds rate is an overnight volume-weighted average of rates on trades arranged by major brokers. This is an ACT/360 rate and is calculated by the Federal Reserve Bank of New York.
      cache.Add(new InterestReferenceRate("FEDFUNDS", "FED Funds", Currency.USD, DayCount.Actual360, Calendar.NYB, 1));
      // RBA OCR (Reserve Bank of Australia Official Cash Rate). The Reserve Bank sets the target cash rate, which is the market interest rate on overnight funds. It uses this as the instrument for monetary policy and influences the cash rate through its financial market operations.
      cache.Add(new InterestReferenceRate("RBAON", "RBA Overnight", Currency.AUD, DayCount.Actual365Fixed, Calendar.SYB, 0));
      // The Canadian overnight repo rate (CORRA) is the weighted average rate of overnight general (non-specific) collateral repo trades that occurred through designated inter-dealer brokers and the Canadian Derivatives Clearing Corporation's central counterparty system between 6:00 a.m. and 4:00 p.m. on the specified date as reported to the Bank of Canada.
      cache.Add(new InterestReferenceRate("CORRA", "Canadian Overnight Repo Rate Average", Currency.CAD, DayCount.Actual365Fixed, Calendar.TRB, 1));
      cache.Add(new InterestReferenceRate("DNBTN", "Danish National Bank Tomorrow/Next", Currency.DKK, DayCount.Actual360, Calendar.COB, -1));
      cache.Add(new InterestReferenceRate("CZEONIA", "Czech Overnight Index Average", Currency.CZK, DayCount.Actual360, Calendar.PRB, 0));
      cache.Add(new InterestReferenceRate("HONIX", "HONIX", Currency.HKD, DayCount.Actual365Fixed, Calendar.HKB, 0));
      cache.Add(new InterestReferenceRate("HUFONIA", "Hungarian Forint Overnight Index Average", Currency.HUF, DayCount.Actual360, Calendar.DBD, 0));
      cache.Add(new InterestReferenceRate("ONMIBOR", "ONMIBOR", Currency.INR, DayCount.Actual365Fixed, Calendar.BMB, 0));
      cache.Add(new InterestReferenceRate("MITOR", "MITOR", Currency.INR, DayCount.Actual365Fixed, Calendar.BMB, 0));
      // RBNZ OCR (Reserve Bank of New Zealand Official Cash Rate) is the interest rate set by the Reserve Bank to meet the inflation target specified in the Policy Targets Agreement. The current PTA, signed in December 2008, defines price stability as annual increases in the Consumers Price Index (CPI) of between 1 and 3 per cent on average over the medium term.
      cache.Add(new InterestReferenceRate("NZIONA", "NZ Reserve Bank Cash Rate", Currency.NZD, DayCount.Actual365Fixed, Calendar.AUB, 0));
      cache.Add(new InterestReferenceRate("POLONIA", "POLONIA", Currency.PLN, DayCount.Actual365Fixed, Calendar.WAB, 0));
      // Tomorrow / Next rate (T / N) is an unsecured day-to-day reference rate for money market lending (deposit lending) in Danish kroner with denominations starting 1 banking day after the signing date and expiry 2 banking days after signing date.
      cache.Add(new InterestReferenceRate("SIOR", "SIOR", Currency.SEK, DayCount.Actual360, Calendar.STB, -1));
      cache.Add(new InterestReferenceRate("SONAR", "Singapore Overnight Average Rate", Currency.SGD, DayCount.Actual365Fixed, Calendar.SIB, 0));
      cache.Add(new InterestReferenceRate("SAFEX", "SAFEX Overnight Deposit Average Rate", Currency.ZAR, DayCount.Actual365Fixed, Calendar.JOB, 0));
      cache.Add(new InterestReferenceRate("SAONIA", "South African Benchmark Overnight Rate", Currency.ZAR, DayCount.Actual365Fixed, Calendar.JOB, 0));
      // NOWA - the Norwegian Overnight Weighted Average - is defined as a weighted average of interest rates set in agreements concluded by banks, either directly or via a broker, for unsecured loans in NOK, where the loan is paid out on the same day and repayment occurs on the following banking day. NOWA shall be calculated as nominal annual rates for the actual number of days in the year ahead (365 or 366). The percentage return over the term is thus calculated by dividing the interest rate by the actual number of days in the year ahead and multiplying it by the actual number of days to maturity.
      cache.Add(new InterestReferenceRate("NOWA", "Norwegian Overnight Weighted Average", Currency.NOK, DayCount.Actual365Fixed, Calendar.OSB, 0));

      // ISDA standard curves
      cache.Add(new ISDAInterestReferenceRate("USD_ISDA", "USD ISDA", Currency.USD, 2, Calendar.None, BDConvention.Modified, Tenor.ThreeMonths, DayCount.Actual360, DayCount.Thirty360, Frequency.SemiAnnual));
      cache.Add(new ISDAInterestReferenceRate("EUR_ISDA", "EUR ISDA", Currency.EUR, 2, Calendar.None, BDConvention.Modified, Tenor.SixMonths, DayCount.Actual360, DayCount.Thirty360, Frequency.Annual));
      cache.Add(new ISDAInterestReferenceRate("GBP_ISDA", "GBP ISDA", Currency.GBP, 0, Calendar.None, BDConvention.Modified, Tenor.SixMonths, DayCount.ActualActual, DayCount.ActualActual, Frequency.SemiAnnual));
      cache.Add(new ISDAInterestReferenceRate("JPY_ISDA", "JPY ISDA", Currency.JPY, 2, Calendar.TKB, BDConvention.Modified, Tenor.SixMonths, DayCount.Actual360, DayCount.ActualActual, Frequency.SemiAnnual));
      cache.Add(new ISDAInterestReferenceRate("CHF_ISDA", "CHF ISDA", Currency.CHF, 2, Calendar.None, BDConvention.Modified, Tenor.SixMonths, DayCount.Actual360, DayCount.Thirty360, Frequency.Annual));
      cache.Add(new ISDAInterestReferenceRate("CAD_ISDA", "CAD ISDA", Currency.CAD, 0, Calendar.None, BDConvention.Modified, Tenor.ThreeMonths, DayCount.ActualActual, DayCount.ActualActual, Frequency.SemiAnnual));
      cache.Add(new ISDAInterestReferenceRate("HKD_ISDA", "HKD ISDA", Currency.HKD, 0, Calendar.None, BDConvention.Modified, Tenor.ThreeMonths, DayCount.ActualActual, DayCount.ActualActual, Frequency.Quarterly));
      cache.Add(new ISDAInterestReferenceRate("SGD_ISDA", "SGD ISDA", Currency.SGD, 2, Calendar.None, BDConvention.Modified, Tenor.SixMonths, DayCount.ActualActual, DayCount.ActualActual, Frequency.SemiAnnual));
      cache.Add(new ISDAInterestReferenceRate("AUD_ISDA", "AUD ISDA", Currency.AUD, 0, Calendar.None, BDConvention.Modified, Tenor.SixMonths, DayCount.ActualActual, DayCount.ActualActual, Frequency.SemiAnnual));
      cache.Add(new ISDAInterestReferenceRate("NZD_ISDA", "NZD ISDA", Currency.NZD, 2, Calendar.None, BDConvention.Modified, Tenor.SixMonths, DayCount.ActualActual, DayCount.ActualActual, Frequency.SemiAnnual));

      // RateIndices (Libor)
      // Ref: https://www.theice.com/iba/libor#fixing-calendar
      // Ref: https://www.dnb.no/en/marketrate
      // EUR, USD O/N rates and all GBP rates are T+0, everything else is T+2
      // The value date is calculated on GBP holidays. If T+2 is a local holiday, value date will roll to next bussines day.
      // ICE LIBOR provides an indication of the average rate at which a LIBOR contributor bank can obtain unsecured funding in the London interbank market for a given period, in a given currency. Individual ICE LIBOR rates are the end-product of a calculation based upon submissions from LIBOR contributor banks. ICE Benchmark Administration maintains a reference panel of between 11 and 18 contributor banks for each currency calculated.
      cache.Add(new InterestReferenceRate("USDLIBOR", "ICE USD LIBOR", Currency.USD, DayCount.Actual360, 0, 2, new Calendar("NYB+LNB"), BDConvention.Modified, new Calendar("NYB+LNB"), CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneDay,Tenor.OneWeek,*/Tenor.OneMonth,/*Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.OneYear*/}, Tenor.OneMonth));
      cache.Add(new InterestReferenceRate("JPYLIBOR", "ICE JPY LIBOR", Currency.JPY, DayCount.Actual360, 2, 2, Calendar.TKB, BDConvention.Modified, Calendar.TKB, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneDay,Tenor.OneWeek,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.OneYear*/}, Tenor.ThreeMonths));
      cache.Add(new InterestReferenceRate("CHFLIBOR", "ICE CHF LIBOR", Currency.CHF, DayCount.Actual360, 2, 2, new Calendar("ZUB+LNB"), BDConvention.Modified, new Calendar("ZUB+LNB"), CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneDay,Tenor.OneWeek,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.OneYear*/}, Tenor.ThreeMonths));
      cache.Add(new InterestReferenceRate("GBPLIBOR", "ICE GBP LIBOR", Currency.GBP, DayCount.Actual365Fixed, 0, 0, Calendar.LNB, BDConvention.Modified, Calendar.LNB, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneDay,Tenor.OneWeek,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.OneYear*/}, Tenor.ThreeMonths));
      cache.Add(new InterestReferenceRate("EURLIBOR", "ICE EUR LIBOR", Currency.EUR, DayCount.Actual360, 2, 2, Calendar.LNB, BDConvention.Modified, Calendar.TGT, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneDay,Tenor.OneWeek,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.OneYear*/}, Tenor.ThreeMonths));
      // RateIndices (Libor-like)
      // http://www.euribor-rates.eu/
      cache.Add(new InterestReferenceRate("EURIBOR", "Euro Interbank Offer Rate", Currency.EUR, DayCount.Actual360, 2, 2, Calendar.TGT, BDConvention.Modified, Calendar.TGT, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneWeek,Tenor.TwoWeeks,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.NineMonths,Tenor.OneYear*/}, Tenor.ThreeMonths));
      // http://www.nasdaqomxnordic.com/bonds/denmark/cibor
      cache.Add(new InterestReferenceRate("CIBOR", "Copenhagen Interbank Offer Rate", Currency.DKK, DayCount.Actual360, 0, 0, new Calendar("COB+LNB"), BDConvention.Modified, new Calendar("COB+LNB"), CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneWeek,Tenor.TwoWeeks,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.NineMonths,Tenor.OneYear*/}, Tenor.ThreeMonths));
      // http://www.oslobors.no/ob_eng/markedsaktivitet/#/list/nibor/quotelist
      cache.Add(new InterestReferenceRate("NIBOR", "Norwegian Interbank Offer Rate", Currency.NOK, DayCount.Actual360, 2, 2, Calendar.OSB, BDConvention.Modified, Calendar.OSB, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneWeek,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths }, Tenor.ThreeMonths));
      // STIBOR stands for Stockholm Interbank Offered Rate, the interest rate banks pay when borrowing money from each other. STIBOR fixing is the average (with the exception of the highest and lowest quotes) of the interest rates.
      // http://www.nasdaqomx.com/transactions/trading/fixedincome/fixedincome/sweden/stiborswaptreasuryfixing
      cache.Add(new InterestReferenceRate("STIBOR", "Stockholm Interbank Offer Rate", Currency.SEK, DayCount.Actual360, 2, 2, new Calendar("STB+LNB"), BDConvention.Modified, new Calendar("STB+LNB"), CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneWeek,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths }, Tenor.ThreeMonths));
      // WIBOR rate is quoted by 14 banks – money market dealers selected in the competition by the National Bank of Poland. Selection criterion is the share in the Polish cash instruments and derivative instruments market.
      // http://www.acipolska.pl/wibor-en.html
      cache.Add(new InterestReferenceRate("WIBOR", "Warsaw Interbank Offer Rate", Currency.PLN, DayCount.Actual365Fixed, 2, 2, Calendar.WAB, BDConvention.Modified, Calendar.WAB, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneMonth,*/Tenor.ThreeMonths, Tenor.SixMonths }, Tenor.ThreeMonths));
      // http://www.jbatibor.or.jp/english/about/
      cache.Add(new InterestReferenceRate("TIBOR", "Tokyo Interbank Offer Rate", Currency.JPY, DayCount.Actual365Fixed, 2, 2, Calendar.TKB, BDConvention.Modified, Calendar.TKB, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneWeek, Tenor.OneMonth, Tenor.TwoMonths, */Tenor.SixMonths, Tenor.OneYear }, Tenor.SixMonths));
      // http://www.afma.com.au/
      cache.Add(new InterestReferenceRate("BBSW", "AUD Bank Bill Swap Rate", Currency.AUD, DayCount.Actual365Fixed, 0, 0, Calendar.SYB, BDConvention.Modified, Calendar.SYB, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths,/*Tenor.FourMonths,Tenor.FiveMonths,*/Tenor.SixMonths }, Tenor.ThreeMonths));
      // https://www.m-x.ca/marc_terme_bax_cdor_en.php
      cache.Add(new InterestReferenceRate("CDOR", "Canadian Dollar Offer Rate", Currency.CAD, DayCount.Actual365Fixed, 0, 0, Calendar.TRB, BDConvention.Modified, Calendar.TRB, CycleRule.None, Frequency.Daily, new[] { Tenor.OneMonth,/*Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.OneYear*/}, Tenor.OneMonth));
      // https://www.hkab.org.hk/DisplayArticleAction.do?sid=3&ss=0
      cache.Add(new InterestReferenceRate("HIBOR", "Hong Kong Interbank Offer Rate", Currency.HKD, DayCount.Actual365Fixed, 0, 0, Calendar.HKB, BDConvention.Modified, Calendar.HKB, CycleRule.None, Frequency.Daily, new[] {/*Tenor.OneWeek,Tenor.TwoWeeks,Tenor.OneMonth,Tenor.TwoMonths,*/Tenor.ThreeMonths, Tenor.SixMonths/*,Tenor.OneYear*/}, Tenor.ThreeMonths));
      // http://www.nzfma.org/
      cache.Add(new InterestReferenceRate("BKBM", "NZ Bank Bill Reference Rate", Currency.NZD, DayCount.Actual365Fixed, 0, 0, Calendar.AUB, BDConvention.Modified, Calendar.AUB, CycleRule.None, Frequency.Daily, new[] { Tenor.ThreeMonths, Tenor.SixMonths }, Tenor.ThreeMonths));
      // https://www.resbank.co.za/MonetaryPolicy/MonetaryPolicyOperations/MarketOperations/DomesticMarket/Pages/Johannesburg-Interbank-Agreed-Rate-%28Jibar%29.aspx
      cache.Add(new InterestReferenceRate("JIBAR", "Johannesburg Interbank Agreed Rate", Currency.ZAR, DayCount.Actual360, 0, 0, Calendar.JOB, BDConvention.Modified, Calendar.ZAB, CycleRule.None, Frequency.Daily, new[] { Tenor.ThreeMonths }, Tenor.ThreeMonths));
      // http://www.sifma.org/research/item.aspx?id=1690
      cache.Add(new InterestReferenceRate("MUNIPSA", "SIFMA Municipal Swap Index Yield", Currency.USD, DayCount.ActualActualBond, 2, 2, Calendar.NYB, BDConvention.Modified, Calendar.NYB, CycleRule.Wednesday, Frequency.Weekly, new[] { Tenor.OneDay }, Tenor.OneDay));
      // http://pluto.mscc.huji.ac.il/~mswiener/teaching/FRM02S/intro_swap_mkt.pdf
      cache.Add(new InterestReferenceRate("CMT", "US Constant Maturity Treasury (CMT)", Currency.USD, DayCount.ActualActualBond, 2, 2, Calendar.NYB, BDConvention.Modified, Calendar.NYB, CycleRule.None, Frequency.Weekly, new[] { Tenor.OneYear, Tenor.TwoYears, Tenor.ThreeYears, Tenor.FiveYears, Tenor.TenYears }, Tenor.OneYear));
      cache.Add(new InterestReferenceRate("PRIME", "US Prime rate", Currency.USD, DayCount.Actual360, 2, 2, Calendar.NYB, BDConvention.Modified, Calendar.NYB, CycleRule.None, Frequency.Weekly, new[] { Tenor.OneDay }, Tenor.OneDay));
      cache.Add(new InterestReferenceRate("USCP", "US Commercial Paper", Currency.USD, DayCount.Actual360, 2, 2, Calendar.NYB, BDConvention.Modified, Calendar.NYB, CycleRule.None, Frequency.Weekly, new[] { new Tenor(30, TimeUnit.Days) }, Tenor.Empty));
      cache.Add(new InterestReferenceRate("USTBILL", "US TBill", Currency.USD, DayCount.Actual360, 2, 2, Calendar.NYB, BDConvention.Modified, Calendar.NYB, CycleRule.None, Frequency.Weekly, new[] { new Tenor(90, TimeUnit.Days) }, Tenor.Empty));
      // http://www.banxico.org.mx/ayuda/temas-mas-consultados/tiie--interbank-equilibrium-i.html
      cache.Add(new InterestReferenceRate("MXIBTIIE", "Mexican Interbank IIE", Currency.MXN, DayCount.Actual360, 1, 1, Calendar.MXB, BDConvention.Modified, Calendar.MXB, CycleRule.None, Frequency.Daily, new Tenor[] { Tenor.TwentyEightDays, new Tenor(91, TimeUnit.Days), new Tenor(182, TimeUnit.Days) }, Tenor.Empty));

      // Swap Rate indices
      // CMS/CMT
      // TBD: 1Yr EUR swaps are Vs 3M EURIBOR. RD Apr'18. ** TERMS INCORRECT **
      // https://www.theice.com/iba/ice-swap-rate
      cache.Add(new SwapReferenceRate("EURCMS", "ICE EUR CMS", Currency.EUR, 2, DayCount.Thirty360, BDConvention.Following, Calendar.TGT, Frequency.Annual, "EURIBOR", Tenor.SixMonths, new[] { Tenor.OneYear, Tenor.TwoYears, Tenor.ThreeYears, Tenor.FourYears, Tenor.FiveYears, Tenor.SixYears, Tenor.SevenYears, Tenor.EightYears, Tenor.NineYears, Tenor.TenYears, Tenor.FifteenYears, Tenor.TwentyYears, Tenor.ThirtyYears }, Tenor.OneYear));
      cache.Add(new SwapReferenceRate("USDCMS", "ICE USD CMS", Currency.USD, 2, DayCount.Thirty360, BDConvention.Following, Calendar.NYB, Frequency.SemiAnnual, "USDLIBOR", Tenor.ThreeMonths, new[] { Tenor.OneYear, Tenor.TwoYears, Tenor.ThreeYears, Tenor.FourYears, Tenor.FiveYears, Tenor.SixYears, Tenor.SevenYears, Tenor.EightYears, Tenor.NineYears, Tenor.TenYears, Tenor.FifteenYears, Tenor.TwentyYears, Tenor.ThirtyYears }, Tenor.OneYear));
      cache.Add(new SwapReferenceRate("GBPCMS", "ICE GBP CMS", Currency.GBP, 0, DayCount.Thirty360, BDConvention.Following, Calendar.LNB, Frequency.SemiAnnual, "GBPLIBOR", Tenor.SixMonths, new[] { Tenor.OneYear, Tenor.TwoYears, Tenor.ThreeYears, Tenor.FourYears, Tenor.FiveYears, Tenor.SixYears, Tenor.SevenYears, Tenor.EightYears, Tenor.NineYears, Tenor.TenYears, Tenor.FifteenYears, Tenor.TwentyYears, Tenor.ThirtyYears }, Tenor.OneYear));
      //cache.Add(new SwapReferenceRate("USDCMT", "ICE USD CMT", Currency.USD, 2, DayCount.Thirty360, BDConvention.Following, Calendar.NYB, Frequency.SemiAnnual, "CMT", Tenor.SixMonths, new[] { Tenor.OneYear, Tenor.TwoYears, Tenor.ThreeYears, Tenor.FourYears, Tenor.FiveYears, Tenor.SixYears, Tenor.SevenYears, Tenor.EightYears, Tenor.NineYears, Tenor.TenYears, Tenor.FifteenYears, Tenor.TwentyYears, Tenor.ThirtyYears }, Tenor.OneYear));
      //cache.Add(new SwapReferenceRate("CHFCMS", "ICE CHF CMS", Currency.CHF, 2, DayCount.Thirty360, BDConvention.Following, Calendar.TGT, Frequency.Annual, "LIBOR", Tenor.SixMonths, new[] { Tenor.OneYear, Tenor.TwoYears, Tenor.ThreeYears, Tenor.FourYears, Tenor.FiveYears, Tenor.SixYears, Tenor.SevenYears, Tenor.EightYears, Tenor.NineYears, Tenor.TenYears, Tenor.FifteenYears, Tenor.TwentyYears, Tenor.ThirtyYears }, Tenor.OneYear));

      // Commodity price indices
      cache.Add(new CommodityReferenceRate("HENRYHUB", "Henry Hub Gas", Currency.USD, Calendar.NYB));
      cache.Add(new CommodityReferenceRate("WTI", "West Texas Intermediate Oil", Currency.USD, Calendar.NYB));
      cache.Add(new CommodityReferenceRate("BRENT", "Brent Crude Oil", Currency.USD, Calendar.NYB));
      cache.Add(new CommodityReferenceRate("RBOB", "Reformulated Regular Gasoline Blendstock", Currency.USD, Calendar.NYB));

      // Inflation indices
      cache.Add(new InflationReferenceRate("RPI_GBP", "GBP RPI Inflation Index", Currency.GBP, DayCount.Actual360, Calendar.LNB, BDConvention.Modified, Frequency.Monthly, Tenor.TwoMonths));
      cache.Add(new InflationReferenceRate("CPI_USD", "USD CPI Inflation Index", Currency.USD, DayCount.Actual360, Calendar.NYB, BDConvention.Modified, Frequency.Monthly, Tenor.ThreeMonths));
      cache.Add(new InflationReferenceRate("HICP_EUR", "HICP EUR Inflation Index", Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, Frequency.Monthly, Tenor.ThreeMonths));
      cache.Add(new InflationReferenceRate("CPI_FR", "French CPI Inflation Index", Currency.EUR, DayCount.Actual360, Calendar.TGT, BDConvention.Modified, Frequency.Monthly, Tenor.ThreeMonths));

      return;
    }
  }
}
