using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Models.ISDACDSModel
{
  /// <summary>
  ///   The standard CDS converter.
  /// </summary>
  public static class SNACCDSConverter
  {
    /// <summary>
    ///   Conversion result.
    /// </summary>
    public class Result
    {
      /// <summary>
      ///   Conventional Spread.
      /// </summary>
      public double ConventionalSpread;
      /// <summary>
      ///   Clean Price.
      /// </summary>
      public double CleanPrice;
      /// <summary>
      ///   Cash settlement amount.
      /// </summary>
      public double CashSettlementAmount;
      /// <summary>
      ///   Accrued.
      /// </summary>
      public double Accrued;
      /// <summary>
      ///   MTM value.
      /// </summary>
      public double MTM;
      /// <summary>
      ///   Cash settle date.
      /// </summary>
      public Dt CashSettleDate;
    };

    /// <summary>
    ///   CDS conversion type
    /// </summary>
    public enum InputType
    {
      /// <summary>
      ///  Convert from conventional spread
      /// </summary>
      ConvSpread,
      /// <summary>
      ///  Convert from upfront
      /// </summary>
      UpFront
    };

    /// <summary>
    ///   CDS conversion type
    /// </summary>
    public enum ResultType
    {
      /// <summary>
      ///  Convert from conventional spread
      /// </summary>
      CashSettlementAmount,
      /// <summary>
      ///  Convert from upfront
      /// </summary>
      ConvSpread
    };

    /// <summary>
    ///  SNAC CDS conventional spread / upfront conversion.
    /// </summary>
    /// <param name="tradeDate">The trade date.</param>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="input">The quoted spread.</param>
    /// <param name="inputType">Type of conversion input (Spread or Upfront)</param>
    /// <returns>Conversion result.</returns>
    public static Result Convert(
      Dt tradeDate,
      Dt maturity,
      DiscountCurve discountCurve,
      double coupon,
      double recoveryRate,
      double notional,
      double input,
      InputType inputType)
    {
      return inputType == InputType.ConvSpread
        ? SNACCDSConverter.FromSpread(tradeDate, maturity,
           discountCurve, coupon / 10000.0,
           input / 10000.0, recoveryRate, notional)
        : SNACCDSConverter.FromUpfront(tradeDate, maturity,
           discountCurve, coupon / 10000.0,
           input, recoveryRate, notional);
    }

    /// <summary>
    ///  SNAC CDS conventional spread / upfront conversion.
    /// </summary>
    /// <param name="tradeDate">The trade date.</param>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="input">The quoted spread.</param>
    /// <param name="inputType">Type of conversion input (Spread or Upfront)</param>
    /// <returns>Conversion result.</returns>
    /// <param name="result">The results calculated.</param>
    /// <returns>Null on success, or error message on failure,
    ///  in which case <paramref name="result"/> contains either the values
    ///  which can be calculated from the input, or NaNs.</returns>
    public static string TryConvert(
      Dt tradeDate,
      Dt maturity,
      DiscountCurve discountCurve,
      double coupon,
      double recoveryRate,
      double notional,
      double input,
      InputType inputType,
      out Result result)
    {
      string msg = null;
      try
      {
        result = inputType == InputType.ConvSpread
      ? SNACCDSConverter.FromSpread(tradeDate, maturity,
         discountCurve, coupon / 10000.0,
         input / 10000.0, recoveryRate, notional)
      : SNACCDSConverter.FromUpfront(tradeDate, maturity,
         discountCurve, coupon / 10000.0,
         input, recoveryRate, notional);
        return null;
      }
      catch (Exception e)
      {
        if (inputType == InputType.ConvSpread)
          msg = "Can not compute price from spread " + input/10000.0 + ": " + e.Message;
        else
          msg = "Can not compute price from upfront fee " + input + ": " + e.Message;
      }

      Dt valueDate = Dt.Empty;
      double df0 = Double.NaN;
      double acc = Double.NaN;
      try
      {
        // Calculate the settlement discount factor.
        valueDate = Dt.AddDays(tradeDate, 3, GetCalendar(discountCurve.Ccy));
        df0 = discountCurve.DiscountFactor(tradeDate, valueDate);
        // Calculate the accrued.
        Dt protBegin = Dt.Add(tradeDate, 1);
        Dt accrBegin = Dt.SNACFirstAccrualStart(tradeDate, GetCalendar(discountCurve.Ccy));
        acc = coupon / 10000.0 * Dt.Fraction(accrBegin, protBegin, DayCount.Actual360);
      }
      catch { } // ignore any exception

      double convSpread, upfront;
      if (inputType == InputType.ConvSpread)
      {
        convSpread = input / 10000.0;
        upfront = Double.NaN;
      }
      else
      {
        upfront = input;
        convSpread = Double.NaN;
      }
      result = MakeResult(valueDate, df0, coupon, convSpread, upfront, acc, notional);
      return msg;
    }

    /// <summary>
    ///  Compute the statndard CDS trade payment from the upfront fee.
    /// </summary>
    /// <param name="tradeDate">The trade date.</param>
    /// <param name="ccy">The currency.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="upfront">The quoted upfront fee.</param>
    /// <param name="cashSettleDate">The cash settlement date calculated.</param>
    /// <returns>Computed trade payment</returns>
    public static double ComputeTradePaymentFromUpfrontFee(
      Dt tradeDate,
      Currency ccy,
      double coupon,
      double notional,
      double upfront,
      out Dt cashSettleDate)
    {
      // Copy part of the logic from the function above; the logic is greatly simplified if all we need to compute is the trade payment from upfront.
      cashSettleDate = Dt.AddDays(tradeDate, 3, GetCalendar(ccy));
      Dt protBegin = Dt.Add(tradeDate, 1);
      Dt accrBegin = Dt.SNACFirstAccrualStart(tradeDate, GetCalendar(ccy));
      double acc = coupon / 10000.0 * Dt.Fraction(accrBegin, protBegin, DayCount.Actual360);
      double cashSettlementAmount = (upfront - acc) * notional;
      return cashSettlementAmount;
    }

    internal static Result FromSpread(
      Dt tradeDate,
      Dt maturity,
      DiscountCurve discountCurve,
      double coupon,
      double quotedSpread,
      double recoveryRate,
      double notional)
    {
      double fee = 0, prot = 0, hzrd = 0, acc = 0;
      SNACModel.PriceFromSpread(
        tradeDate, maturity, discountCurve,
        quotedSpread, recoveryRate,
        ref hzrd, ref prot, ref fee, ref acc);
      Dt valueDate = Dt.AddDays(tradeDate, 3, GetCalendar(discountCurve.Ccy));
      double df0 = discountCurve.DiscountFactor(tradeDate, valueDate);
      prot *= 1 - recoveryRate;
      fee *= coupon;
      acc *= coupon;
      double upfront = acc + (prot - fee) / df0;
      Result res = MakeResult(valueDate, df0, coupon,
        quotedSpread, upfront, acc, notional);
      return res;
    }

    internal static Result FromUpfront(
      Dt tradeDate,
      Dt maturity,
      DiscountCurve discountCurve,
      double coupon,
      double upfront,
      double recoveryRate,
      double notional)
    {
      double fee = 0, prot = 0, hzrd = 0, acc = 0;
      SNACModel.PriceFromUpfront(tradeDate, maturity,
        discountCurve, coupon, recoveryRate, upfront, true,
        ref hzrd, ref prot, ref fee, ref acc);
      Dt valueDate = Dt.AddDays(tradeDate, 3, GetCalendar(discountCurve.Ccy));
      double df0 = discountCurve.DiscountFactor(tradeDate, valueDate);
      prot *= 1 - recoveryRate;
      double convSpread = prot / (fee - acc * df0);
      Result res = MakeResult(valueDate, df0, coupon,
        convSpread, upfront, acc * coupon, notional);
      return res;
    }

    private static Result MakeResult(
      Dt valueDate,
      double df0,
      double coupon,
      double convSpread,
      double upfront,
      double acc,
      double notional)
    {
      Result result = new Result();
      result.ConventionalSpread = convSpread * 10000;
      result.Accrued = acc * notional;
      result.CashSettlementAmount = (upfront - acc) * notional;
      result.MTM = - result.CashSettlementAmount * df0;
      result.CleanPrice = (1 - upfront) * 100;
      result.CashSettleDate = valueDate;
      return result;
    }
    private static Calendar GetCalendar(Currency ccy)
    {
      return ccy != Currency.JPY ? Calendar.None : Calendar.TYO;
    }
  }
}
