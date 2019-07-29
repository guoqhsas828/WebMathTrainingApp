using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Curves
{
  public static partial class CurveTenorQuoteHandlers
  {
    [Serializable]
    public class RiskFreeBondQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
    {
      public RiskFreeBondQuoteHandler(double qValue, QuotingConvention qConvention)
      {
        currentQuoteValue_ = qValue;
        currentQuoteType_ = qConvention;
      }

      #region ICurveTenorQuoteHandler

      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        return new CurveTenor.Quote(currentQuoteType_, currentQuoteValue_);
      }

      public double GetQuote(CurveTenor tenor, QuotingConvention targetQuote, Curve curve,
                             Calibrator calibrator, bool recalculate)
      {
        if (targetQuote == currentQuoteType_ && !recalculate)
          return currentQuoteValue_;

        var bondPricer = (BondPricer)tenor.GetPricer(curve, calibrator);
        double quoteVal;
        switch (targetQuote)
        {
          case QuotingConvention.FullPrice:
            quoteVal = bondPricer.FullPrice();
            break;
          case QuotingConvention.Yield :
            quoteVal = bondPricer.YieldToMaturity();
            break;
          case QuotingConvention.FlatPrice :
            quoteVal = bondPricer.FlatPrice();
            break;
          default:
            throw new QuoteTypeNotSupportedException(string.Format(
              "Quote type [{0}] not supported for bond calibration", targetQuote));
        }
        return quoteVal;
      }

      public void SetQuote(CurveTenor tenor, QuotingConvention quoteType, double quoteValue)
      {
        currentQuoteValue_ = quoteValue;
        currentQuoteType_ = quoteType;
      }

      public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags flags)
      {
        bool bumpRelative = (flags & BumpFlags.BumpRelative) != 0;
        bool up = (flags & BumpFlags.BumpDown) == 0;

        var bumpAmt = up ? bumpSize : -bumpSize;
        double yield = currentQuoteValue_;
        if (bumpRelative)
        {
          if (bumpAmt > 0)
          {
            bumpAmt *= Math.Abs(yield);
          }
          else
          {
            bumpAmt *= yield > 0 ? yield/(1.0 - bumpAmt) : -yield/(1 + bumpAmt);
          }
        }
        else
        {
          bumpAmt /= 10000.0;
          if ((yield >0.0 && yield + bumpAmt < 0.0) || (yield < 0.0 && yield + bumpAmt >0.0))
          {
            logger.DebugFormat("Unable to bump tenor [{0}] with spread [{1}] by [{2}], bump by [{3}] instead",
                              tenor.Product.Description, yield, bumpAmt, -yield/2.0);
            bumpAmt = -yield/2.0;
          }
        }

        currentQuoteValue_ = yield + bumpAmt;
        return (up ? bumpAmt : -bumpAmt)*10000.0;
      }

      /// <summary>
      /// Creates a pricer and set up target MTM for calibration.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="calibrator">The calibrator.</param>
      /// <returns>The pricer.</returns>
      public IPricer CreatePricer(CurveTenor tenor, Curve curve, Calibrator calibrator)
      {
        var bond = (Bond)tenor.Product.Clone();
        var discount = curve as DiscountCurve;
        if (discount == null)
        {
          throw new InvalidOperationException("Not a discount curve, cannot construct the bond pricer");
        }

        var bondPricer = new BondPricer(bond, calibrator.AsOf, calibrator.Settle, discount, null, 0, TimeUnit.None,
                                        0.0) {MarketQuote = currentQuoteValue_, QuotingConvention = currentQuoteType_ };

        tenor.MarketPv = bondPricer.FullPrice();
        return bondPricer;
      }

      #endregion

      #region Data

      private double currentQuoteValue_;
      private QuotingConvention currentQuoteType_;

      #endregion

    }
  }
}
