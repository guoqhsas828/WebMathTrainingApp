/*
 *  -2012. All rights reserved.
 */
using System;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// 
  /// </summary>
  public class BondQuotingConventionUtil
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="quoteType"></param>
    /// <param name="marketQuote"></param>
    /// <param name="floating"></param>
    /// <returns></returns>
    public static double ParseQuote(QuotingConvention quoteType, double marketQuote, bool floating)
    {
      switch (quoteType)
      {
        case QuotingConvention.FlatPrice:
        case QuotingConvention.FullPrice:
        case QuotingConvention.Yield:
        case QuotingConvention.YieldToNext:
        case QuotingConvention.YieldToWorst:
        case QuotingConvention.ForwardFullPrice:
        case QuotingConvention.ForwardFlatPrice:
        case QuotingConvention.DiscountRate:
          return marketQuote / 100;
        case QuotingConvention.ZSpread:
          return marketQuote / 10000;
        case QuotingConvention.RSpread:
          return marketQuote / 10000;
        case QuotingConvention.DiscountMargin:
          if (!floating)
            throw new ArgumentOutOfRangeException("quoteType", "Quote type supported only for FRNs");
          return marketQuote / 10000;
        case QuotingConvention.ASW_Par:
        case QuotingConvention.ASW_Mkt:
          if (floating)
            throw new ArgumentOutOfRangeException("quoteType", "Quote type supported only for Fixed Rate Bonds");
          return marketQuote / 10000;
        case QuotingConvention.UseModelPrice:
          return marketQuote;
        default:
          throw new ArgumentOutOfRangeException("quoteType", "Quote type not supported");
      }
    }

    #region BondQuoteUtils

    /// <summary>
    ///  Converts a bond quote from the database raw value to its quoted convention
    /// </summary>
    /// <param name="quotingConvention"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static double ConvertBondQuoteToToolkit(QuotingConvention quotingConvention, double value)
    {
      switch (quotingConvention)
      {
        // Converting to Basis Points
        case QuotingConvention.ASW_Par:
        case QuotingConvention.DiscountMargin:
        case QuotingConvention.ZSpread:
        case QuotingConvention.RSpread:
        case QuotingConvention.YieldSpread:
          return value * 10000;
        case QuotingConvention.Yield:
        case QuotingConvention.FlatPrice:
        case QuotingConvention.FullPrice:
        case QuotingConvention.ForwardFlatPrice:
        case QuotingConvention.ForwardFullPrice:
        case QuotingConvention.DiscountRate:
          return value * 100;
        default:
          return value;
      }
    }


    /// <summary>
    ///  Converts a quote from its quoted convention to raw db format
    /// </summary>
    /// <param name="quotingConvention"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static double ConvertBondQuoteFromExcel(QuotingConvention quotingConvention, double value)
    {
      switch (quotingConvention)
      {
        // Converting to Basis Points
        case QuotingConvention.ASW_Par:
        case QuotingConvention.DiscountMargin:
        case QuotingConvention.ZSpread:
        case QuotingConvention.RSpread:
        case QuotingConvention.YieldSpread:
          return value / 10000;
        case QuotingConvention.Yield:
        case QuotingConvention.FlatPrice:
        case QuotingConvention.FullPrice:
        case QuotingConvention.ForwardFlatPrice:
        case QuotingConvention.ForwardFullPrice:
        case QuotingConvention.DiscountRate:
          return value / 100;
        default:
          return value;
      }
    }
    #endregion

  }
}
