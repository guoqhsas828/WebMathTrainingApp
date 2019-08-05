using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Risk.Trading
{
  /// <summary>
  /// 
  /// </summary>
  public static class TradeExtensions
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    public static bool ValidateWithRelatedTrades(this Trade trade, ArrayList errors)
    {
      Trade leadTrade; 
      IList<Trade> relatedTrades;

      if (trade.LeadTrade == null)
      {
        leadTrade = trade;
        relatedTrades = TradeUtil.GetUnwindAssignTrades(trade);

        if (relatedTrades.IsNullOrEmpty())
        return true;

        // Validate Sign
        if (relatedTrades.Any(t => Math.Sign(t.Amount) == Math.Sign(leadTrade.Amount)))
        {
          InvalidValue.AddError(errors, trade, "Amount", "Lead trade amount should be in opposite direction to unwind/assign trades.");
          return false;
        }

        // Validate Traded
        Trade unwindAssignTrade = relatedTrades.FirstOrDefault(t => t.Traded < leadTrade.Traded);
        if (unwindAssignTrade!=null)
        {
          InvalidValue.AddError(errors, trade, "Traded",
            String.Format("Lead trades' Traded cannot be after {0} trades' [{1}] traded [{2}].", unwindAssignTrade.TradeType, unwindAssignTrade.TradeId,
              unwindAssignTrade.Traded));
          return false;
        }

        // Validate Counterparty
        if (leadTrade.Counterparty != null)
        {
          Trade unwindTrade =
            relatedTrades.FirstOrDefault(t => t.TradeType == TradeType.Unwind && t.Counterparty != null && t.Counterparty.Name != leadTrade.Counterparty.Name);
          if (unwindTrade!=null)
          {
            InvalidValue.AddError(errors, trade, "Counterparty",
              "Lead Trade Counterparty and Unwind Trade [" + unwindTrade.Traded + "] Counterparty [" + unwindTrade.Counterparty.Name + "] must be same.");
            return false;
          }
          Trade assignTrade =
            relatedTrades.FirstOrDefault(t => t.TradeType == TradeType.Assign && t.Counterparty != null && t.Counterparty.Name == leadTrade.Counterparty.Name);
          if (assignTrade!=null)
          {
            InvalidValue.AddError(errors, trade, "Counterparty",
              "Lead Trade Counterparty and Assign Trade [" + assignTrade.TradeId + "] Counterparty [" + assignTrade.Counterparty.Name + "] cannot be same.");
            return false;
          }
        }

        // Validate Amount
        decimal relatedTradesAmt = relatedTrades.Sum(t => (decimal)t.Amount);
        if (Math.Abs((decimal)leadTrade.Amount) < Math.Abs(relatedTradesAmt))
        {
          InvalidValue.AddError(errors, trade, "Amount", String.Format("Lead Trade Amount lower than sum of unwinds/assigns [{0:#,###.##}].", relatedTradesAmt));
          return false;
        }
      }
      else
      {
        leadTrade = trade.LeadTrade;

        // Validate that the lead trade and trade are different
        if (leadTrade.TradeId == trade.TradeId)
        {
          InvalidValue.AddError(errors, trade, "LeadTrade", "Cannot " + trade.TradeType + " itself.");
          return false;
        }

        // Validate that the Lead Trade Type is New
        if (leadTrade.TradeType != TradeType.New)
        {
          InvalidValue.AddError(errors, trade, "LeadTrade", "Lead Trade cannot be " + leadTrade.TradeType);
          return false;
        }

        // Validate that the trade and lead trade are of same type
        if (trade.GetType() != leadTrade.GetType())
        {
          InvalidValue.AddError(errors, trade, "LeadTrade",
            String.Format("Trade [{0}] is [{1}] and Lead Trade [{2}] is [{3}]. Both trades should be of the same type.",
              trade.TradeId, trade.GetType().Name, leadTrade.TradeId, leadTrade.GetType().Name));
          return false;
        }

        // Validate that this trade does not have any unwind/asssign trades
        if (!TradeUtil.GetUnwindAssignTrades(trade).IsNullOrEmpty())
        {
          InvalidValue.AddError(errors, trade,
            "Cannot make this trade an " + trade.TradeType + " of " + leadTrade.TradeId + " because it has one or more active unwind/assign trades.");
        }
        
        // Validate Sign
        if (Math.Sign(trade.Amount) == Math.Sign(leadTrade.Amount))
        {
          InvalidValue.AddError(errors, trade, "Amount", trade.TradeType + " trade amount should be in opposite direction to Lead trade.");
          return false;
        }

        // Validate Traded
        if (trade.Traded < leadTrade.Traded)
        {
          InvalidValue.AddError(errors, trade, "Traded",
            String.Format("{0} trades' Traded cannot be before Lead trades' [{1}] traded [{2}].", trade.TradeType, leadTrade.TradeId, leadTrade.Traded));
          return false;
        }

        // Validate Counterparty
        string leadTradeCptyName = leadTrade.Counterparty == null ? null : leadTrade.Counterparty.Name;
        string thisTradeCptyName = trade.Counterparty == null ? null : trade.Counterparty.Name;
        // Check to see that the Counterparty is different for Assignment
        if (trade.TradeType == TradeType.Assign && leadTradeCptyName == thisTradeCptyName)
        {
          InvalidValue.AddError(errors, trade, "Counterparty", "Cannot assign to same counterparty. Either choose unwind or assign to a different counterparty");
          return false;
        }
        // Check to see that the Counterparty is same for Unwind
        if (trade.TradeType == TradeType.Unwind && leadTradeCptyName != thisTradeCptyName)
        {
          InvalidValue.AddError(errors, trade, "Counterparty", "Cannot unwind to different counterparty. Either choose assign or unwind to a same counterparty");
          return false;
        }

        // Validate Amount
        relatedTrades = TradeUtil.GetUnwindAssignTrades(leadTrade, trade.TradeId);
        double maxAllowedAmt = (double)((decimal)leadTrade.Amount + relatedTrades.Sum(t => (decimal)t.Amount)) * -1;

        // Validate that the Lead Trade is not fully unwound/assigned
        if (maxAllowedAmt.ApproximatelyEqualsTo(0.0))
        {
          InvalidValue.AddError(errors, trade, String.Format("Lead Trade {0} is already fully unwound/assigned", leadTrade.TradeId));
          return false;
        }

        // Validate that the Notional on the Assign/Unwind trade is not exceeding the maximum limit.
        if ((maxAllowedAmt < 0 && trade.Amount < maxAllowedAmt) || (maxAllowedAmt >= 0 && trade.Amount > maxAllowedAmt))
        {
          string errorMsg = trade.IsSwapLike()
                       ? String.Format("The maximum amount you can {0} this trade is {1:#.######%}.", trade.TradeType, maxAllowedAmt)
                       : String.Format("The maximum amount you can {0} this trade is {1:#,#.######}.", trade.TradeType, maxAllowedAmt);
          InvalidValue.AddError(errors, trade, "Amount", errorMsg);
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Checks if Trade is one of the Swap Trades
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public static bool IsSwapLike(this Trade t)
    {
      return false;
    }
  }
}
