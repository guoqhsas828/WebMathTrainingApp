/*
 * ForwardRateQuoteHandler.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Curves
{
  public static partial class CurveTenorQuoteHandlers
  {
    [Serializable]
    internal class ForwardRateHandler
      : BaseEntityObject, ICurveTenorQuoteHandler
    {
      public ForwardRateHandler(
        double rate, Dt reset,
        Dt maturity, DayCount dayCount)
      {
        rate_ = rate;
        reset_ = reset;
        maturity_ = maturity;
        dayCount_ = dayCount;
      }

      #region ICurveTenorQuoteHandler Members

      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        return new CurveTenor.Quote(QuotingConvention.Yield, Rate);
      }

      public double GetQuote(
        CurveTenor tenor,
        QuotingConvention targetQuoteType,
        Curve curve, Calibrator calibrator,
        bool recalculate)
      {
        if (targetQuoteType != QuotingConvention.Yield)
        {
          throw new QuoteConversionNotSupportedException(
            targetQuoteType, QuotingConvention.Yield);
        }
        return Rate;
      }

      public void SetQuote(
        CurveTenor tenor,
        QuotingConvention quoteType, double quoteValue)
      {
        if (quoteType != QuotingConvention.Yield)
        {
          throw new QuoteConversionNotSupportedException(
            QuotingConvention.Yield, quoteType);
        }
        rate_ = quoteValue;
      }

      public double BumpQuote(
        CurveTenor tenor,
        double bumpSize, BumpFlags bumpFlags)
      {
        bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
        bool up = (bumpFlags & BumpFlags.BumpDown) == 0;
        double bumpAmt = (up) ? bumpSize : -bumpSize;
        if (bumpRelative)
        {
          if (bumpAmt < 0)
          {
            bumpAmt = bumpAmt / (1 - bumpAmt);
          }
          bumpAmt = bumpAmt * rate_;
        }
        else
          bumpAmt /= 10000.0;

        if (rate_ + bumpAmt < 0.0)
        {
          logger.DebugFormat(
            "Unable to bump tenor '{0}' by {1}, bump {2} instead",
            tenor.Name, bumpAmt, -rate_ / 2);
          bumpAmt = -rate_ / 2;
        }
        rate_ += bumpAmt;
        return bumpAmt;
      }

      public IPricer CreatePricer(CurveTenor tenor,
                                  Curve curve, Calibrator calibrator)
      {
        var ccurve = curve as CalibratedCurve;
        if (ccurve == null)
        {
          throw new NotSupportedException("Forward rate quote"
            + " handler works only with calibrated curves");
        }
        return calibrator.GetPricer(ccurve, tenor.Product);
      }

      #endregion

      #region Data and Properties

      private readonly DayCount dayCount_;
      private readonly Dt maturity_;
      private readonly Dt reset_;
      private double rate_;

      public Dt Reset
      {
        get { return reset_; }
      }

      public Dt Maturity
      {
        get { return maturity_; }
      }

      public double Rate
      {
        get { return rate_; }
      }

      public DayCount DayCount
      {
        get { return dayCount_; }
      }

      #endregion Data and Properties
    }
  }
}