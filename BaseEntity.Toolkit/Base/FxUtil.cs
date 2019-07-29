/*
 *  -2012. All rights reserved.
 */
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Exchange rate utility functions
  /// </summary>
  public class FxUtil : BaseEntityObject
  {
    #region Static Utilities

    /// <summary>
    /// Calculate the default number of days to spot for a currency pair (ccy1/ccy2)
    /// </summary>
    /// <remarks>
    ///   <para>The standard settlement for fx spot transactions is T+2 days. The exceptions to this are
    ///   USD/CAD, USD/TRY, USD/PHP, USD/RUB, USD/KZT or USD/PKR which are typically quoted T+1.</para>
    /// </remarks>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to/receive) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from/pay) currency</param>
    /// <returns>Number of days to spot (settlement)</returns>
    public static int FxDaysToSpot(Currency ccy1, Currency ccy2)
    {
      if ((ccy1 == Currency.USD && ccy2 == Currency.CAD) || (ccy1 == Currency.CAD && ccy2 == Currency.USD))
        return 1;
      else if ((ccy1 == Currency.USD && ccy2 == Currency.TRY) || (ccy1 == Currency.TRY && ccy2 == Currency.USD))
        return 1;
      else if ((ccy1 == Currency.USD && ccy2 == Currency.RUB) || (ccy1 == Currency.RUB && ccy2 == Currency.USD))
        return 1;
      else
        return 2;
    }

    /// <summary>
    /// Calculate spot date for an fx transaction
    /// </summary>
    /// <remarks>
    ///   <para>The spot date is calculated relative to the asOf date given the number of days to settlement
    ///   and the calendars for each currency.</para>
    ///   <para>For T+1 settlement, the spot date is the next US business day.</para>
    ///   <para>For T+2 settlement, the spot date is the second day counting valid business days in both
    ///   currencies. For example if T+1 is a holiday in either currency then spot becomes T+3. For T+2
    ///   spot must also be a valid US business day. For example if T+1 is a US holiday, spot is unaffected.
    ///   If T+2 is a US holiday, spot becomes T+3.</para>
    /// </remarks>
    /// <param name="asOf">asOf (pricing/horizon) date</param>
    /// <param name="settleDays">Days to settlement</param>
    /// <param name="ccy1Calendar">Base (domestic/base/unit/transaction/source/to/receive) currency calendar</param>
    /// <param name="ccy2Calendar">Quoting (foreign/quote/price/payment/destination/from/pay) currency calendar</param>
    /// <returns>Calculated spot date</returns>
    public static Dt FxSpotDate(Dt asOf, int settleDays, Calendar ccy1Calendar, Calendar ccy2Calendar)
    {
      Dt spotDate = asOf;
      int clearWorkingDaysToCcy = settleDays >= 1
                                    ? (ccy1Calendar == Calendar.NYB)
                                        ? Math.Max(settleDays - 1, 1)
                                        : settleDays
                                    : 0;
      int clearWorkingDaysFromCcy = settleDays >= 1
                                      ? (ccy2Calendar == Calendar.NYB)
                                          ? Math.Max(settleDays - 1, 1)
                                          : settleDays
                                      : 0;

      while (true)
      {
        bool minimumClearWorkingDaysToCcy = Dt.BusinessDays(asOf, spotDate, ccy1Calendar) >= clearWorkingDaysToCcy;
        bool minimumClearWorkingDaysFromCcy = Dt.BusinessDays(asOf, spotDate, ccy2Calendar) >= clearWorkingDaysFromCcy;
        if (minimumClearWorkingDaysToCcy &&
            minimumClearWorkingDaysFromCcy &&
            CalendarCalc.IsValidSettlement(ccy2Calendar, spotDate.Day, spotDate.Month, spotDate.Year) &&
            CalendarCalc.IsValidSettlement(ccy1Calendar, spotDate.Day, spotDate.Month, spotDate.Year) &&
            CalendarCalc.IsValidSettlement(Calendar.NYB, spotDate.Day, spotDate.Month, spotDate.Year))
          break;
        spotDate = Dt.AddDays(spotDate, 1, Calendar.None);
      }
      return spotDate;
    }

    /// <summary>
    /// Calculate the Non-deliverable Forward fixing date from its maturity
    /// </summary>
    /// <param name="mat">Maturity</param>
    /// <param name="spotDays">Spot days</param>
    /// <param name="fromCal">From currency calendar</param>
    /// <param name="toCal">To currency calendar</param>
    /// <returns>Fixing date</returns>
    public static Dt GetNdfFixingDate(Dt mat, int spotDays, Calendar fromCal, Calendar toCal)
    {
      if (spotDays <= 0)
        return mat;

      Dt spotDate = mat;
      int clearWorkingDaysToCcy = spotDays >= 1
                                    ? (fromCal == Calendar.NYB)
                                        ? Math.Max(spotDays - 1, 1)
                                        : spotDays
                                    : 0;
      int clearWorkingDaysFromCcy = spotDays >= 1
                                      ? (toCal == Calendar.NYB)
                                          ? Math.Max(spotDays - 1, 1)
                                          : spotDays
                                      : 0;

      while (true)
      {
        bool minimumClearWorkingDaysToCcy = Dt.BusinessDays(spotDate, mat, fromCal) >= clearWorkingDaysToCcy;
        bool minimumClearWorkingDaysFromCcy = Dt.BusinessDays(spotDate, mat, toCal) >= clearWorkingDaysFromCcy;
        if (minimumClearWorkingDaysToCcy &&
            minimumClearWorkingDaysFromCcy &&
            CalendarCalc.IsValidSettlement(fromCal, spotDate.Day, spotDate.Month, spotDate.Year) &&
            CalendarCalc.IsValidSettlement(toCal, spotDate.Day, spotDate.Month, spotDate.Year) &&
            CalendarCalc.IsValidSettlement(Calendar.NYB, spotDate.Day, spotDate.Month, spotDate.Year))
          break;
        spotDate = Dt.AddDays(spotDate, -1, Calendar.None);
      }
      return spotDate;
    }

    /// <summary>
    /// Gets the currency priority ranking for standard quoting
    /// </summary>
    /// <remarks>
    ///   <para>Currencies are quoted in terms of terms currency pairs
    ///   Ccy1/Ccy2 which is the value of a unit of Ccy1 in terms of Ccy2.</para>
    ///   <para>Ccy1 is termed the base  /foreign/source/from currency and Ccy2 is termed the quoting  /domestic/destination/to currency.</para>
    ///   <para>Ccy1 is the higher priority currency</para>
    ///   <para>The standard priority order of which currency is the base currency is
    ///   EUR, GBP, AUD, NZD, USD, CAD, CHF, JPY, Others.</para>
    ///   <para>For example EUR/USD is the standard quoting convention for EUR and USD. A quote of 1.4 means 1 EUR = 1.4 USD,
    ///   or 1 EUR costs $1.4 USD.</para>
    /// </remarks>
    /// <param name="ccy">Currency</param>
    /// <returns>Priority ranking for base currency quoting</returns>
    public static int FxRank(Currency ccy)
    {
      switch (ccy)
      {
        case Currency.EUR:
          return 1;
        case Currency.GBP:
          return 2;
        case Currency.AUD:
          return 3;
        case Currency.NZD:
          return 4;
        case Currency.USD:
          return 5;
        case Currency.CAD:
          return 6;
        case Currency.CHF:
          return 7;
        case Currency.JPY:
          return 8;
        default:
          return 9;
      }
    }

    /// <summary>
    /// Gets the standard quoting key for two currencies
    /// </summary>
    /// <remarks>
    ///   <para>Currencies are quoted in terms of terms currency pairs
    ///   Ccy1/Ccy2 or Ccy1Ccy2 which is the value of a unit of Ccy1 in terms of Ccy2.</para>
    ///   <para>Ccy1 is termed the base currency and Cc2 is termed the quoting currency.</para>
    ///   <para>The standard priority order of which currency is the base currency is
    ///   EUR, GBP, AUD, NZD, USD, CAD, CHF, JPY, Others. For example EURUSD is the
    ///   standard quoting convention for EUR and USD.</para>
    /// </remarks>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to/receive) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from/pay) currency</param>
    /// <returns>Standard quoting for two currencies. Eg EURUSD</returns>
    public static string FxStandardQuote(Currency ccy1, Currency ccy2)
    {
      if (FxRank(ccy1) < FxRank(ccy2))
        return String.Concat(ccy1.ToString(), ccy2.ToString());
      else
        return String.Concat(ccy2.ToString(), ccy1.ToString());
    }

    /// <summary>
    /// Gets the cross currency swap leg (1 or 2) the basis swap spread is paid on
    /// </summary>
    /// <remarks>
    ///   <para>The market convention is for the basis swap spread to be paid on the
    ///   less liquid currency swap leg.</para>
    ///   <para>If one of legs is USD, then the basis swap spread is paid on the
    ///   other leg.</para>
    ///   <para>If neither leg is USD and one of the legs is EUR, then the basis swap
    ///   spread is paid on the other leg.</para>
    /// </remarks>
    /// <returns>Cross currency leg the basis swap is paid on (1 or 2)</returns>
    public static int BasisSwapCcyLeg(Currency ccy1, Currency ccy2)
    {
      return BasisSwapCcyLeg(ccy1, ccy2, true);
    }

    internal static int BasisSwapCcyLeg(Currency ccy1, Currency ccy2, bool exceptionOnFailure)
    {
      if (ccy1 == Currency.USD || ccy2 == Currency.USD)
        return (ccy1 == Currency.USD) ? 2 : 1;
      else if (ccy1 == Currency.EUR || ccy2 == Currency.EUR)
        return (ccy1 == Currency.EUR) ? 2 : 1;
      else if (exceptionOnFailure)
        throw new ArgumentException("Cannot determine default cross currency swap leg for basis");
      return 0;
    }

    /// <summary>
    /// Return true if fx rate can be obtained from two fx curves either directly or via trangulation
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency</param>
    /// <param name="fxCurve1">First fx curve</param>
    /// <param name="fxCurve2">Second fx curve</param>
    /// <returns>True if fx rate can be obtained</returns>
    public static bool CanGetFxRate(Currency ccy1, Currency ccy2, FxCurve fxCurve1, FxCurve fxCurve2)
    {
      if ((fxCurve1 != null) && (fxCurve1.From(ccy1, ccy2)))
        // Fx curve 1 direct fx
        return true;
      else if ((fxCurve2 != null) && (fxCurve2.From(ccy1, ccy2)))
        // Fx curve 2 direct fx
        return true;
      if (fxCurve1 != null && fxCurve2 != null)
      {
        if (fxCurve1.Contains(ccy1) && fxCurve2.Contains(ccy2) && (CurveOtherCcy(fxCurve1, ccy1) == CurveOtherCcy(fxCurve2, ccy2)))
          // ccy1 in FxCurve 1, ccy2 in FxCurve2, same cross rate
          return true;
        if (fxCurve1.Contains(ccy2) && fxCurve2.Contains(ccy1) && (CurveOtherCcy(fxCurve1, ccy2) == CurveOtherCcy(fxCurve2, ccy1)))
          // ccy2 in FxCurve 1, ccy1 in FxCurve2, same cross rate
          return true;
      }
      return false;
    }

    /// <summary>
    /// Return fx spot rate from curve triangulation (one or two fx curves containing direct or cross fx rate via valueCcy)
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency</param>
    /// <param name="fxCurve1">First fx curve</param>
    /// <param name="fxCurve2">Second fx curve</param>
    /// <returns>Spot fx rate</returns>
    public static double SpotFxRate(Currency ccy1, Currency ccy2, FxCurve fxCurve1, FxCurve fxCurve2)
    {
      if ((fxCurve1 != null) && (fxCurve1.From(ccy1, ccy2)))
        // Fx curve 1 direct fx
        return fxCurve1.FxRate(ccy1, ccy2);
      else if ((fxCurve2 != null) && (fxCurve2.From(ccy1, ccy2)))
        // Fx curve 2 direct fx
        return fxCurve2.FxRate(ccy1, ccy2);
      if (fxCurve1 != null && fxCurve2 != null)
      {
        if (fxCurve1.Contains(ccy1) && fxCurve2.Contains(ccy2) && (CurveOtherCcy(fxCurve1, ccy1) == CurveOtherCcy(fxCurve2, ccy2)))
          // ccy1 in FxCurve 1, ccy2 in FxCurve2, same cross rate
          return fxCurve1.FxRate(ccy1, CurveOtherCcy(fxCurve1, ccy1)) / fxCurve2.FxRate(ccy2, CurveOtherCcy(fxCurve2, ccy2));
        if (fxCurve1.Contains(ccy2) && fxCurve2.Contains(ccy1) && (CurveOtherCcy(fxCurve1, ccy2) == CurveOtherCcy(fxCurve2, ccy1)))
          // ccy2 in FxCurve 1, ccy1 in FxCurve2, same cross rate
          return fxCurve2.FxRate(ccy1, CurveOtherCcy(fxCurve2, ccy1)) / fxCurve1.FxRate(ccy2, CurveOtherCcy(fxCurve1, ccy2));
      }
      throw new ToolkitException("{0}/{1} not valid for this fx forward", ccy1, ccy2);
    }

    /// <summary>
    /// Return fx rate from curve triangulation (one or two fx curves containing direct or cross fx rate via valueCcy)
    /// </summary>
    /// <param name="date">Date for fx rate</param>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency</param>
    /// <param name="fxCurve1">First fx curve</param>
    /// <param name="fxCurve2">Second fx curve</param>
    /// <returns>Spot fx rate</returns>
    public static double ForwardFxRate(Dt date, Currency ccy1, Currency ccy2, FxCurve fxCurve1, FxCurve fxCurve2)
    {
      if ((fxCurve1 != null) && (fxCurve1.From(ccy1, ccy2)))
        // Fx curve 1 direct fx
        return fxCurve1.FxRate(date, ccy1, ccy2);
      else if ((fxCurve2 != null) && (fxCurve2.From(ccy1, ccy2)))
        // Fx curve 2 direct fx
        return fxCurve2.FxRate(date, ccy1, ccy2);
      if (fxCurve1 != null && fxCurve2 != null)
      {
        if (fxCurve1.Contains(ccy1) && fxCurve2.Contains(ccy2) && (CurveOtherCcy(fxCurve1, ccy1) == CurveOtherCcy(fxCurve2, ccy2)))
          // ccy1 in FxCurve 1, ccy2 in FxCurve2, same cross rate
          return fxCurve1.FxRate(date, ccy1, CurveOtherCcy(fxCurve1, ccy1)) / fxCurve2.FxRate(date, ccy2, CurveOtherCcy(fxCurve2, ccy2));
        if (fxCurve1.Contains(ccy2) && fxCurve2.Contains(ccy1) && (CurveOtherCcy(fxCurve1, ccy2) == CurveOtherCcy(fxCurve2, ccy1)))
          // ccy2 in FxCurve 1, ccy1 in FxCurve2, same cross rate
          return fxCurve2.FxRate(date, ccy1, CurveOtherCcy(fxCurve2, ccy1)) / fxCurve1.FxRate(date, ccy2, CurveOtherCcy(fxCurve1, ccy2));
      }
      throw new ToolkitException("{0}/{1} not valid for this fx forward", ccy1, ccy2);
    }

    /// <summary>
    /// Return discount curve given the valuation currency from fx curve triangulation (one or two fx curves
    /// containing direct or cross fx rate via valueCcy)
    /// </summary>
    /// <param name="valuationCcy">Valuation currency</param>
    /// <param name="fxCurve1">First fx curve</param>
    /// <param name="fxCurve2">Second fx curve</param>
    /// <returns>Spot fx rate</returns>
    public static DiscountCurve DiscountCurve(Currency valuationCcy, FxCurve fxCurve1, FxCurve fxCurve2)
    {
      if (fxCurve1 != null)
      {
        if (valuationCcy == fxCurve1.Ccy1)
          return fxCurve1.Ccy1DiscountCurve;
        else if (valuationCcy == fxCurve1.Ccy2)
          return fxCurve1.Ccy2DiscountCurve;
      }
      if( fxCurve2 != null)
      {
        if (valuationCcy == fxCurve2.Ccy1)
          return fxCurve2.Ccy1DiscountCurve;
        else if (valuationCcy == fxCurve2.Ccy2)
          return fxCurve2.Ccy2DiscountCurve;
      }
      return null;
    }

    /// <summary>
    /// Other curve currency
    /// </summary>
    private static Currency CurveOtherCcy(FxCurve fxCurve, Currency ccy)
    {
      if( fxCurve == null )
        return Currency.None;
      else if( fxCurve.Ccy1 == ccy )
        return fxCurve.Ccy2;
      else if( fxCurve.Ccy2 == ccy )
        return fxCurve.Ccy1;
      else
        return Currency.None;
    }

    /// <summary>
    /// Construct the direct name from the currency pair
    /// </summary>
    /// <param name="fromCcy">From ccy</param>
    /// <param name="toCcy">To ccy</param>
    /// <returns>Direct name</returns>
    public static string DirectName(Currency fromCcy, Currency toCcy)
    {
      return fromCcy.ToString() + toCcy.ToString();
    }

    #endregion Static Utilities
	}
}
