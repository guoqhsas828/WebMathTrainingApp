//
//  -2014. All rights reserved.
//

using System;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.TenorQuoteHandlers
{
  /// <summary>
  ///   The market quote handler, following the Singleton pattern.
  /// </summary>
  [Serializable]
  internal class MarketQuoteHandler : ICurveTenorQuoteHandler
  {
    /// <summary>
    ///   The instance
    /// </summary>
    public static MarketQuoteHandler Instance = new MarketQuoteHandler();

    #region private constructor

    /// <summary>
    ///   Constructor
    /// </summary>
    private MarketQuoteHandler()
    {
    }

    #endregion

    #region ICurveTenorQuoteHandler Members

    public IMarketQuote GetCurrentQuote(CurveTenor tenor)
    {
      CheckTenorConsistency(tenor);
      return tenor.MarketQuote;
    }

    public double GetQuote(CurveTenor tenor, QuotingConvention targetQuoteType,
      Curve curve, Calibrator calibrator, bool recalculate)
    {
      CheckTenorConsistency(tenor);
      var quote = tenor.MarketQuote;
      if (quote.Type != targetQuoteType)
      {
        throw new ToolkitException(String.Format(
          "Unable to convert from {0} to {1}",
          quote.Type, targetQuoteType));
      }
      return quote.Value;
    }

    public void SetQuote(CurveTenor tenor, QuotingConvention quoteType, double quoteValue)
    {
      CheckTenorConsistency(tenor);
      SetQuote(tenor, new MarketQuote(quoteValue, quoteType));
    }

    public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags bumpFlags)
    {
      CheckTenorConsistency(tenor);

      var quote = tenor.MarketQuote;

      bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
      bool up = (bumpFlags & BumpFlags.BumpDown) == 0;

      double bumpAmt = (up) ? bumpSize : -bumpSize;
      if (bumpRelative)
      {
        if (bumpAmt < 0)
        {
          bumpAmt = bumpAmt/(1 - bumpAmt);
        }
        var baseQuote = IsRateFutureTenor(tenor)
          ? 1 - quote.Value
          : quote.Value;
        bumpAmt *= Math.Abs(baseQuote);
      }
      else
      {
        bumpAmt /= 10000.0;
      }

      if (!AllowNegative(quote.Type, bumpFlags))
      {
        var baseQuote = IsRateFutureTenor(tenor)
          ? 1 - quote.Value
          : quote.Value;
        bumpAmt = bumpAmt.AdjustToHandleCrossingZero(baseQuote, tenor.Name, bumpFlags);
      }

      if (quote.Type == QuotingConvention.FlatPrice)
        quote.Value -= bumpAmt;
      else
        quote.Value += bumpAmt;
      SetQuote(tenor, quote);

      // This is the required post condition:
      //  The actual bump amount has the same sign as the input bump size.
      Debug.Assert((bumpSize.AlmostEquals(0.0) && bumpAmt.AlmostEquals(0.0))
        || (bumpSize > 0 && (up ? bumpAmt : -bumpAmt) >= 0.0)
        || (bumpSize < 0 && (up ? bumpAmt : -bumpAmt) <= 0.0));

      return ((up) ? bumpAmt : -bumpAmt)*10000;
    }

    public BaseEntity.Toolkit.Pricers.IPricer CreatePricer(CurveTenor tenor, Curve curve,
      Calibrator calibrator)
    {
      CheckTenorConsistency(tenor);
      var ccurve = curve as CalibratedCurve;
      if (ccurve == null)
      {
        throw new NotSupportedException(
          "General tenor quote handler works only with calibrated curves");
      }
      return calibrator.GetPricer(ccurve, tenor);
    }

    #endregion

    #region Helpers

    private static void SetQuote(CurveTenor tenor, MarketQuote quote)
    {
      tenor.MarketQuote = quote;
    }

    private static bool AllowNegative(QuotingConvention type, BumpFlags bumpFlags)
    {
      switch (type)
      {
      case QuotingConvention.CreditConventionalSpread:
      case QuotingConvention.CreditSpread:
        return (bumpFlags & BumpFlags.AllowNegativeCDSSpreads) != 0;
      case QuotingConvention.YieldSpread:
        return true;
      }
      return false;
    }

    private static bool IsRateFutureTenor(CurveTenor tenor)
    {
      var provider = tenor.ProductTerms;
      if (provider == null)
      {
        throw new NullReferenceException("Product provider cannot be null");
      }
      return (provider is StirFutureTerms);
    }

    #endregion

    #region ICloneable Members

    public object Clone()
    {
      return this;
    }

    #endregion

    #region Validate

    [Conditional("DEBUG")]
    private void CheckTenorConsistency(CurveTenor tenor)
    {
      if (tenor == null)
      {
        throw new NullReferenceException("tenor cannot be null");
      }
      if (tenor.ProductTerms == null)
      {
        throw new ToolkitException(String.Format(
          "Inconsistent quote handler for tenor {0}",
          tenor.Name ?? tenor.QuoteKey));
      }
    }

    #endregion
  }
}
