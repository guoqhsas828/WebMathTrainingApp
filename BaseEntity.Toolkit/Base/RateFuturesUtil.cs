//
// RateFuturesUtil.cs
//   2010. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Base
{
  ///<summary>
  /// Utility function related with eurodollar future trading
  ///</summary>
  public class RateFuturesUtil
  {
    ///<summary>
    /// This function generates the implied spreads on each order to satisfy pack/bundle trade requirement
    ///</summary>
    ///<param name="bundleSize">The size of the bundle trade</param>
    ///<param name="targetTradeSpread">Trade spread</param>
    ///<returns>Individual contract spread to generate bundle orders </returns>
    public static double[] ImpliedEDFutureContractSpreads(int bundleSize, double targetTradeSpread)
    {
      if (bundleSize <= 0)
        throw new ArgumentException("Pack size must be positive to compute implied pack trade spread");

      var impliedSpreads = new double[bundleSize];
      var orderPackDifference = Math.Abs(targetTradeSpread - (int)targetTradeSpread);
      for (int i = 0; i < bundleSize; i++)
        impliedSpreads[i] = (int)targetTradeSpread;

      var adjustPackIndex = bundleSize - 1;
      while (orderPackDifference > 0 && adjustPackIndex >= 0)
      {
        impliedSpreads[adjustPackIndex] = targetTradeSpread > 0 ? Math.Ceiling(targetTradeSpread) : Math.Floor(targetTradeSpread);
        orderPackDifference -= 1.0 / bundleSize;
        adjustPackIndex--;
      }
      return impliedSpreads;
    }

    ///<summary>
    /// Utility function to calculate Eurodollar futures contract trading start date based on maturity and terms
    ///</summary>
    ///<param name="maturity">Futures contract maturity</param>
    ///<param name="calendar">Calendar</param>
    ///<param name="tenor">Tenor</param>
    ///<returns>Trading start date</returns>
    public static Dt EDFutureTradingStart(Dt maturity, Calendar calendar, Tenor tenor)
    {
      var tenorN = 1; //
      Dt effective;
      if (tenor != Tenor.Empty && tenor.Units == TimeUnit.Months)
      {
        tenorN = tenor.N;
      }
      if (tenorN == 1) // 1-month eurodollar futures
      {
        effective = Dt.Add(maturity, -1, TimeUnit.Years);
      }
      else if ((maturity.Month % tenorN) == 0)  //standard contracts
      {
        effective = Dt.Add(maturity, -10, TimeUnit.Years);
      }
      else  //front-month contracts
      {
        effective = Dt.Add(maturity, -6, TimeUnit.Months);
      }
      return Dt.AddDays(Dt.ImmDate(effective.Month, effective.Year), -2, calendar);
    }

    ///<summary>
    ///</summary>
    ///<param name="year">Contract expiration year</param>
    ///<param name="firstDeliveryRule">Bond future first delivery day rule</param>
    ///<param name="tradingDayRule">Bond future last trading day rule</param>
    ///<param name="lastDeliveryRule">Bond future last delivery day rule</param>
    ///<param name="cal">Bond future calendar</param>
    ///<param name="roll">Bond future BD Convention</param>
    ///<param name="month">Contract expiration month</param>
    ///<returns>Bond future first available delivery day</returns>
    public static Dt CalcFirstDeliveryDate(int month, int year, BondFutureFirstDeliveryDayRule firstDeliveryRule,
      BondFutureLastTradingDayRule tradingDayRule, BondFutureDeliveryDayRule lastDeliveryRule,
       Calendar cal, BDConvention roll)
    {
      if (firstDeliveryRule == BondFutureFirstDeliveryDayRule.First)
      {
        var dt = Dt.Roll(new Dt(1, month, year), roll, cal);
        return (dt.Month != month) ? Dt.AddDays(dt, 1, cal) : dt;
      }
      else
      {
        Dt lastDelivery, lastTrade;
        BondFutureModel.LastTradingAndDeliveryDates(month, year, tradingDayRule, lastDeliveryRule, cal, out lastTrade, out lastDelivery);
        return lastDelivery;
      }
    }
  }
}
