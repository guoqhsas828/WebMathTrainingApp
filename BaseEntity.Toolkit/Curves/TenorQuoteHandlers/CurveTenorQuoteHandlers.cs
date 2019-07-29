/*
 * CurveTenorQuoteHandlers.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   An exception that is thrown when a quote type is not currently
  ///   supported in curve tenors.
  /// </summary>
  [Serializable]
  public class QuoteTypeNotSupportedException : ToolkitException
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="QuoteTypeNotSupportedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public QuoteTypeNotSupportedException(string message)
      : base(message) { }
  }

  /// <summary>
  ///   An exception that is thrown when a quote type is not currently
  ///   supported in curve tenors.
  /// </summary>
  [Serializable]
  public class QuoteConversionNotSupportedException : ToolkitException
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="QuoteTypeNotSupportedException"/> class.
    /// </summary>
    public QuoteConversionNotSupportedException(QuotingConvention from, QuotingConvention to)
      : base(String.Format("Converting quote type {0} to {1} not supported yet.",from, to))
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuoteTypeNotSupportedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public QuoteConversionNotSupportedException(string message)
      : base(message) { }
  }

  /// <summary>
  /// public class implementing several quote handlers.
  /// </summary>
  public static partial class CurveTenorQuoteHandlers
  {
    // Logger
    public static readonly log4net.ILog logger =
      log4net.LogManager.GetLogger(typeof(CurveTenorQuoteHandlers));

    /// <summary>
    ///   Default handler for tenor quotes.
    ///   It handles the quotes in the same way as we did before.
    /// </summary>
    public static readonly ICurveTenorQuoteHandler
      DefaultHandler = new GeneralQuoteHandler();

    /// <summary>
    /// Creates a pricer based on the tenor quote convention.
    /// This is a simple extension method.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <returns>Pricer.</returns>
    public static IPricer GetPricer(this CurveTenor tenor,
      Curve curve, Calibrator calibrator)
    {
      return tenor.QuoteHandler.CreatePricer(tenor, curve, calibrator);
    }

    public static double BumpCreditSpread(double spread,
      double bumpSize, BumpFlags flags, string tenorDescription)
    {
      bool bumpRelative = (flags & BumpFlags.BumpRelative) != 0;
      bool up = (flags & BumpFlags.BumpDown) == 0;

      double bumpAmt = (up) ? bumpSize : -bumpSize;

      if (bumpRelative)
      {
        if (bumpAmt > 0)
        {
          if (spread > 0)
            bumpAmt *= spread;
          else
            bumpAmt *= (-spread);
        }
        else
        {
          if (spread > 0)
          {
            bumpAmt /= (1 - bumpAmt);
            bumpAmt *= spread;
          }
          else
          {
            bumpAmt /= (1 + bumpAmt);
            bumpAmt *= (-spread);
          }
        }
      }
      else
      {
        bumpAmt /= 10000.0;

        if ((spread > 0.0 && spread + bumpAmt < 0.0) || (spread < 0.0 && spread + bumpAmt > 0.0))
        {
          logger.DebugFormat("Unable to bump tenor '{0}' with spread {1} by {2}, bump by {3} instead",
                            tenorDescription, spread, bumpAmt, -spread / 2);
          bumpAmt = -spread / 2;

        }
      }
      return bumpAmt;
    }

    /// <summary>
    ///   CDS quote handler.
    ///   It is able to handle the conversion among different quotes:
    ///   par spread, conventional spread, and upfront.
    /// </summary>
    [Serializable]
    public class CDSQuoteHandler : ICurveTenorQuoteHandler
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="CDSQuoteHandler"/> class.
      /// </summary>
      /// <param name="quoteValue">The quote value.</param>
      /// <param name="quoteType">Type of the quote.</param>
      /// <param name="convIRCurve">The conventional IR curve.</param>
      /// <param name="convRecovery">The conventional recovery.</param>
      public CDSQuoteHandler(
        double quoteValue, QuotingConvention quoteType,
        DiscountCurve convIRCurve, double convRecovery)
      {
        currentQuoteValue_ = quoteValue;
        currentQuoteType_ = quoteType;
        convDiscountCurve_ = convIRCurve;
        convRecoveryRate_ = convRecovery;
      }

      /// <summary>
      /// Creates a new object that is a copy of the current instance.
      /// </summary>
      /// <returns>
      /// A new object that is a copy of this instance.
      /// </returns>
      public object Clone()
      {
        return MemberwiseClone();
      }

      /// <summary>
      /// Gets the current quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <returns>Current quote.</returns>
      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        return new CurveTenor.Quote(currentQuoteType_, currentQuoteValue_);
      }

      /// <summary>
      /// Gets the current quote in the specified type.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="targetQuote">The target quote.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="calibrator">The calibrator.</param>
      /// <param name="recalculate">if set to <c>true</c>, recalculate all quotes.</param>
      /// <returns>The quote value.</returns>
      /// <exception cref="QuoteTypeNotSupportedException">
      ///  Conversion to the target quote type is not supported by this handler.
      /// </exception>
      public double GetQuote(CurveTenor tenor, QuotingConvention targetQuote,
        Curve curve, Calibrator calibrator, bool recalculate)
      {
        if (targetQuote == currentQuoteType_ && !recalculate)
          return currentQuoteValue_;

        var pricer = CreatePricer((CDS)tenor.Product.Clone(),
          curve, (SurvivalCalibrator)calibrator);
        double quoteValue;
        switch (targetQuote)
        {
          case QuotingConvention.CreditSpread:
            {
              pricer.CDS.Fee = 0;
              pricer.CDS.Premium = 1;
              double rd = pricer.FullFeePv() - pricer.Accrued();
              quoteValue = rd <= 1E-8 ? 0.0 :
                (-pricer.ProtectionPv() / rd);
              break;
            }
          case QuotingConvention.CreditConventionalSpread:
            quoteValue = ConventionalSpread(pricer);
            break;
          case QuotingConvention.CreditConventionalUpfront:
            quoteValue = ConventionalUpfront(pricer);
            break;
          case QuotingConvention.Fee:
            pricer.CDS.Fee = 0;
            quoteValue = pricer.Accrued() - pricer.Pv();
            if (!pricer.CDS.FeeSettle.IsEmpty())
            {
              quoteValue /= pricer.DiscountCurve.DiscountFactor(
                pricer.AsOf, pricer.CDS.FeeSettle);
            }
            break;
          default:
            throw new QuoteTypeNotSupportedException(String.Format(
              "Quote type {0} not supported.", targetQuote));
        }
        return quoteValue;
      }

      /// <summary>
      /// Sets the current quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="quoteType">Type of the quote.</param>
      /// <param name="quoteValue">The quote value.</param>
      public void SetQuote(CurveTenor tenor, QuotingConvention quoteType,
        double quoteValue)
      {
        currentQuoteValue_ = quoteValue;
        currentQuoteType_ = quoteType;
      }

      /// <summary>
      /// Bumps the current quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="bumpSize">Size of the bump.</param>
      /// <param name="flags">The flags.</param>
      /// <returns>The actual amount bumped.</returns>
      /// <remarks>
      /// Relative bump: expect bumpSize to be a number between 0 and 1, e.g. 0.1 for a 10% bump.
      /// Absolute bump: expect bumpSize to be a number, e.g. 10 for a 10 bps bump
      /// </remarks>
      public double BumpQuote(CurveTenor tenor,
        double bumpSize, BumpFlags flags)
      {
        double bumpAmt = BumpCreditSpread(currentQuoteValue_,
          bumpSize, flags, tenor.Product.Description);
        currentQuoteValue_ += bumpAmt;
        return ((flags & BumpFlags.BumpDown) == 0 ? bumpAmt : -bumpAmt) * 10000;
      }

      /// <summary>
      /// Creates a pricer and sets up target MTM for calibration.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="calibrator">The calibrator.</param>
      /// <returns>A pricer for calibration.</returns>
      public IPricer CreatePricer(CurveTenor tenor, Curve curve,
        Calibrator calibrator)
      {
        var scal = calibrator as SurvivalCalibrator;
        if (scal == null)
        {
          throw new InvalidOperationException("Not a survival calibrator");
        }
        var cds = (CDS)tenor.Product.Clone();
        switch (currentQuoteType_)
        {
          case QuotingConvention.Fee:
            // Old backward compatible handling of the clean fee.
            tenor.MarketPv = -currentQuoteValue_;
            if (!cds.FeeSettle.IsEmpty())
            {
              tenor.MarketPv *= scal.DiscountCurve.DiscountFactor(
                scal.AsOf, cds.FeeSettle);
            }
            cds.Fee = 0.0;
            if (cds.Effective < scal.Settle)
            {
              var pricer = CreatePricer(cds, curve, scal);
              tenor.MarketPv += pricer.Accrued();
            }
            break;
          case QuotingConvention.CreditSpread:
            {
              cds.Premium = currentQuoteValue_;
              cds.Fee = 0.0;
              var pricer = CreatePricer(cds, curve, scal);
              tenor.MarketPv = pricer.Accrued();
            }
            break;
          case QuotingConvention.CreditConventionalUpfront:
            {
              // Convert upfront to MTM at trade date.
              var pricer = CreatePricer(cds, curve, scal);
              var mtm = (pricer.Accrued() - currentQuoteValue_)
                * convDiscountCurve_.DiscountFactor(scal.AsOf,
                pricer.CDS.FeeSettle);
              tenor.MarketPv = mtm;
            }
            cds.Fee = 0.0;
            break;
          case QuotingConvention.CreditConventionalSpread:
            {
              // Convert conventional spread to MTM.
              var res = BaseEntity.Toolkit.Models.ISDACDSModel.SNACCDSConverter
                .FromSpread(calibrator.AsOf, cds.Maturity,
                convDiscountCurve_, cds.Premium, currentQuoteValue_,
                convRecoveryRate_, 1.0);
              tenor.MarketPv = res.MTM;
            }
            cds.Fee = 0;
            break;
          default:
            throw new QuoteTypeNotSupportedException(String.Format(
              "Quote type {0} not supported in CDS quote handler.",
              currentQuoteType_));
        }
        return CreatePricer(cds, curve, scal);
      }

      /// <summary>
      /// Creates a CDS pricer based on teh calibration settings.
      /// </summary>
      /// <param name="cds">The CDS.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="scal">The survival calibrator.</param>
      /// <returns>CDS pricer.</returns>
      private static CDSCashflowPricer CreatePricer(CDS cds,
        Curve curve, SurvivalCalibrator scal)
      {
        var sc = curve as SurvivalCurve;
        if (sc == null)
        {
          throw new InvalidOperationException("Not a survival curve");
        }
        var pricer = new CDSCashflowPricer(cds, scal.AsOf,
          scal.Settle, scal.DiscountCurve, sc, scal.CounterpartyCurve,
          scal.CounterpartyCorrelation, 0, TimeUnit.None);
        pricer.RecoveryCurve = scal.RecoveryCurve;
        pricer.ReferenceCurve = scal.ReferenceCurve;
        var sfcal = scal as SurvivalFitCalibrator;
        if (sfcal != null)
        {
          pricer.StepSize = sfcal.StepSize;
          pricer.StepUnit = sfcal.StepUnit;
          foreach (RateReset r in sfcal.RateResets)
            pricer.RateResets.Add(r);
        }
        return pricer;
      }

      /// <summary>
      /// Calculates the conventional upfront.
      /// </summary>
      /// <param name="pricer">The pricer.</param>
      /// <returns>Conventional upfront.</returns>
      private double ConventionalUpfront(CDSCashflowPricer pricer)
      {
        double df0 = convDiscountCurve_.DiscountFactor(
          pricer.AsOf, pricer.CDS.FeeSettle);
        pricer.CDS.Fee = 0;
        double cashSettleAmt = -pricer.Pv() / df0;
        return cashSettleAmt + pricer.Accrued();
      }

      /// <summary>
      /// Calculates the conventional spread.
      /// </summary>
      /// <param name="pricer">The pricer.</param>
      /// <returns>Conventional spread in decimal (1bp = 0.0001).</returns>
      private double ConventionalSpread(CDSCashflowPricer pricer)
      {
        double upfront = ConventionalUpfront(pricer);
        var res = BaseEntity.Toolkit.Models.ISDACDSModel.SNACCDSConverter.FromUpfront(
          pricer.AsOf, pricer.CDS.Maturity, convDiscountCurve_,
          pricer.CDS.Premium, upfront, convRecoveryRate_,
          pricer.Notional);
        return res.ConventionalSpread / 10000.0;
      }

      private double currentQuoteValue_;
      private QuotingConvention currentQuoteType_;
      private DiscountCurve convDiscountCurve_;
      private double convRecoveryRate_;

    } // class CDSQuoteHandler


    /// <summary>
    ///   A general handler of tenor quotes.
    ///   It handles the quotes in the same way as we did before.
    /// </summary>
    [Serializable]
    private class GeneralQuoteHandler : ICurveTenorQuoteHandler
    {
      /// <summary>
      /// Creates a new object that is a copy of the current instance.
      /// </summary>
      /// <returns>
      /// A new object that is a copy of this instance.
      /// </returns>
      public object Clone()
      {
        return this; // this is an instance with no data member.
      }

      /// <summary>
      /// Bumps the tenor quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="bumpSize">Size of the bump.</param>
      /// <param name="bumpFlags">The bump flags.</param>
      /// <returns>Actual bump size.</returns>
      public double BumpQuote(CurveTenor tenor,
        double bumpSize, BumpFlags bumpFlags)
      {
        bool bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;
        bool up = (bumpFlags & BumpFlags.BumpDown) == 0;
        bool allowNegativeCDSSpreads = (bumpFlags & BumpFlags.AllowNegativeCDSSpreads) != 0;
        double bumpAmt = (up) ? bumpSize : -bumpSize;
        if (bumpRelative && bumpAmt < 0)
        {
          bumpAmt = bumpAmt / (1 - bumpAmt);
        }

        if (tenor.Product is CDS)
        {
          var cds = (CDS)tenor.Product;
          bumpAmt = CalculateAbsoluteBump(bumpAmt, cds.Premium, bumpRelative);

          bumpAmt = allowNegativeCDSSpreads
            ? bumpAmt
            : bumpAmt.AdjustToHandleCrossingZero(cds.Premium, cds.Description, bumpFlags);
          cds.Premium += bumpAmt;
        }
        else if (tenor.Product is SwapLeg)
        {
          var swap = (SwapLeg)tenor.Product;
          bumpAmt = CalculateAbsoluteBump(bumpAmt, swap.Coupon, bumpRelative)
            .AdjustToHandleCrossingZero(swap.Coupon, swap.Description, bumpFlags);
          swap.Coupon += bumpAmt;
        }
        else if (tenor.Product is Swap)
        {
          var swp = (Swap) tenor.Product;
          bool perturbPayer = swp.IsPayerFixed || swp.IsSpreadOnPayer; // (info & (SwapInfo.PayerIsFixed | SwapInfo.SpreadOnPayer)) != 0;
          //Assume implicitely that either payer or receiver pay a coupon
          SwapLeg leg = perturbPayer ? swp.PayerLeg : swp.ReceiverLeg;
          bumpAmt = CalculateAbsoluteBump(bumpAmt, leg.Coupon, bumpRelative);

          bumpAmt = leg.Floating
            ? bumpAmt
            : bumpAmt.AdjustToHandleCrossingZero(leg.Coupon, leg.Description, bumpFlags);
          leg.Coupon += bumpAmt;
        }
        else if (tenor.Product is Note)
        {
          var note = (Note)tenor.Product;
          bumpAmt = CalculateAbsoluteBump(bumpAmt, note.Coupon, bumpRelative)
            .AdjustToHandleCrossingZero(note.Coupon, note.Description, bumpFlags);
          note.Coupon += bumpAmt;
        }
        else if (tenor.Product is StirFuture)
        {
          double rate = 1 - tenor.MarketPv;
          bumpAmt = CalculateAbsoluteBump(bumpAmt, rate, bumpRelative)
            .AdjustToHandleCrossingZero(rate, tenor.Name, bumpFlags);
          tenor.MarketPv -= bumpAmt;
        }
        else if (tenor.Product is FutureBase)
        {
          bumpAmt = CalculateAbsoluteBump(bumpAmt, tenor.MarketPv, bumpRelative)
            .AdjustToHandleCrossingZero(tenor.MarketPv, tenor.Name, bumpFlags);
          tenor.MarketPv -= bumpAmt;
        }
        else if (tenor.Product is Bond)
        {
          bumpAmt = CalculateAbsoluteBump(bumpAmt, tenor.MarketPv, bumpRelative)
            .AdjustToHandleCrossingZero(-tenor.MarketPv, tenor.Name, bumpFlags);
          tenor.MarketPv -= bumpAmt;
        }
        else if (tenor.Product is FRA)
        {
          var fra = (FRA)tenor.Product;
          bumpAmt = CalculateAbsoluteBump(bumpAmt, fra.Strike, bumpRelative)
            .AdjustToHandleCrossingZero(fra.Strike, fra.Description, bumpFlags);
          fra.Strike += bumpAmt;
        }
        else if (tenor.Product is CurvePointHolder)
        {
          var pt = (CurvePointHolder)tenor.Product;
          bumpAmt = (bumpRelative ? (bumpAmt * Math.Abs(pt.Value)) : bumpAmt)
            .AdjustToHandleCrossingZero(pt.Value, pt.Description, bumpFlags);
          pt.Value += bumpAmt;
          return ((up) ? bumpAmt : -bumpAmt);
        }
        else
        {
          throw new ArgumentException(String.Format("{0} not supported for curve bumping at the moment", tenor.Product.GetType()));
        }

        // This is the required post condition:
        //  The actual bump amount has the same sign as the input bump size.
        Debug.Assert((bumpSize.AlmostEquals(0.0) && bumpAmt.AlmostEquals(0.0))
          || (bumpSize > 0 && (up ? bumpAmt : -bumpAmt) >= 0.0)
          || (bumpSize < 0 && (up ? bumpAmt : -bumpAmt) <= 0.0));

        return ((up) ? bumpAmt : -bumpAmt) * 10000;
      }

      private static double CalculateAbsoluteBump(
        double bumpAmt, double baseQuote, bool bumpRelative)
      {
        return bumpRelative
          ? (bumpAmt * Math.Abs(baseQuote))
          : (bumpAmt / 10000.0);
      }

      /// <summary>
      /// Creates a pricer for calibration.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="calibrator">The calibrator.</param>
      /// <returns>Pricer.</returns>
      public IPricer CreatePricer(CurveTenor tenor, Curve curve, Calibrator calibrator)
      {
        var ccurve = curve as CalibratedCurve;
        if (ccurve == null)
        {
          throw new NotSupportedException(
            "General tenor quote handler works only with calibrated curves");
        }
        return calibrator.GetPricer(ccurve, tenor.Product);
      }

      /// <summary>
      /// Gets the current tenor quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <returns>The current quote.</returns>
      public IMarketQuote GetCurrentQuote(CurveTenor tenor)
      {
        // Standard product quotes
        if (tenor.ProductTerms != null)
          return tenor.MarketQuote;

        // Old style 
        var product = tenor.Product;
        var cds = product as CDS;
        if (cds != null)
          return new CurveTenor.Quote(QuotingConvention.CreditSpread, cds.Premium);
        var leg = product as SwapLeg;
        if (leg != null)
        {
          if (leg.Floating)
            return new CurveTenor.Quote(QuotingConvention.YieldSpread, leg.Coupon);
          return new CurveTenor.Quote(QuotingConvention.Yield, leg.Coupon);
        }
        var swp = product as Swap;
        if (swp != null)
        {
          if (swp.IsSpreadOnPayer)
            return new CurveTenor.Quote(QuotingConvention.YieldSpread, swp.PayerLeg.Coupon);
          if (swp.IsPayerFixed)
            return new CurveTenor.Quote(QuotingConvention.Yield, swp.PayerLeg.Coupon);
          if (swp.IsSpreadOnReceiver)
            return new CurveTenor.Quote(QuotingConvention.YieldSpread, swp.ReceiverLeg.Coupon);
          return new CurveTenor.Quote(QuotingConvention.Yield, swp.ReceiverLeg.Coupon);
        }
        var note = product as Note;
        if (note != null)
        {
          return new CurveTenor.Quote(QuotingConvention.Yield,
                                      note.Coupon);
        }
        var fra = product as FRA;
        if (fra != null)
        {
          return new CurveTenor.Quote(QuotingConvention.Yield, fra.Strike);
        }
        if (product is StirFuture || product is Bond)
        {
          return new CurveTenor.Quote(QuotingConvention.FlatPrice, tenor.MarketPv);
        }
        if (product is FutureBase)
        {
          return new CurveTenor.Quote(QuotingConvention.ForwardFlatPrice, tenor.MarketPv);
        }
        var curvePointHolder = product as CurvePointHolder;
        if (curvePointHolder != null)
        {
          return new CurveTenor.Quote(QuotingConvention.None,
                                      curvePointHolder.Value);
        }
        throw new ArgumentException(String.Format(
          "{0} not support getting curve quote at the moment",
          product.GetType()));
      }

      /// <summary>
      /// Gets the current tenor quote in the specified type.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="targetQuoteType">Type of the target quote.</param>
      /// <param name="curve">The curve.</param>
      /// <param name="calibrator">The calibrator.</param>
      /// <param name="recalculate">if set to <c>true</c>, recalculate the quote values.</param>
      /// <returns>The quote value.</returns>
      /// <exception cref="QuoteTypeNotSupportedException">
      ///  Conversion to the target quote type is not supported by this handler.
      /// </exception>
      public double GetQuote(CurveTenor tenor,
        QuotingConvention targetQuoteType,
        Curve curve, Calibrator calibrator, bool recalculate)
      {
        var product = tenor.Product;
        if (recalculate)
        {
          // In backward compatible mode, we only support recalculation
          // of traditional quotes for CDS.
          if (product is CDS)
          {
            var pricer = (CDSCashflowPricer)CreatePricer(tenor, curve, calibrator);
            switch (targetQuoteType)
            {
              case QuotingConvention.CreditSpread:
                return pricer.BreakEvenPremium();
              case QuotingConvention.Fee:
                return pricer.BreakEvenFee(pricer.CDS.Premium);
            }
          }
          else if (product is CurvePointHolder)
          {
            return curve.Interpolate(tenor.Maturity);
          }
          throw new NotSupportedException(String.Format(
            "Recalculating quote type {0} not supported in general handler for {1}",
            targetQuoteType, tenor.Product.GetType().Name));
        }

        // If we're, no need to recalculate the quotes.
        var cds = product as CDS;
        if (cds != null)
        {
          if (targetQuoteType != QuotingConvention.CreditSpread)
            throw QuoteTypeNotSupported(targetQuoteType, product);
          return cds.Premium;
        }
        var swapLeg = product as SwapLeg;
        if (swapLeg != null)
        {
          if (targetQuoteType != QuotingConvention.Yield && targetQuoteType != QuotingConvention.YieldSpread)
            throw QuoteTypeNotSupported(targetQuoteType, product);
          return swapLeg.Coupon;
        }
        var swp = product as Swap;
        if (swp != null)
        {
          if (targetQuoteType != QuotingConvention.Yield && targetQuoteType != QuotingConvention.YieldSpread)
            throw QuoteTypeNotSupported(targetQuoteType, product);
          return (swp.IsPayerFixed || swp.IsSpreadOnPayer) ? swp.PayerLeg.Coupon : swp.ReceiverLeg.Coupon;
        }
        var note = product as Note;
        if (note != null)
        {
          if (targetQuoteType != QuotingConvention.Yield)
            throw QuoteTypeNotSupported(targetQuoteType, product);
          return note.Coupon;
        }
        if (product is StirFuture || product is Bond)
        {
          if (targetQuoteType != QuotingConvention.FlatPrice)
            throw QuoteTypeNotSupported(targetQuoteType, product);
          return tenor.MarketPv;
        }
        if (product is FutureBase)
        {
          if (targetQuoteType != QuotingConvention.ForwardFlatPrice)
            throw QuoteTypeNotSupported(targetQuoteType, product);
          return tenor.MarketPv;
        }
        var fra = product as FRA;
        if (fra != null)
        {
          if (targetQuoteType != QuotingConvention.Yield)
            throw QuoteTypeNotSupported(targetQuoteType, product);
          return fra.Strike;
        }
        var curvePointHolder = product as CurvePointHolder;
        if (curvePointHolder != null)
        {
          if (targetQuoteType != QuotingConvention.None
              && targetQuoteType != QuotingConvention.Volatility)
          {
            throw QuoteTypeNotSupported(targetQuoteType, product);
          }
          return curvePointHolder.Value;
        }
        throw new ArgumentException(String.Format(
          "{0} not support getting curve quote at the moment",
          product.GetType().Name));
      }

      /// <summary>
      /// Sets the current tenor quote.
      /// </summary>
      /// <param name="tenor">The tenor.</param>
      /// <param name="quoteType">Type of the quote.</param>
      /// <param name="quoteValue">The quote value.</param>
      /// <exception cref="QuoteTypeNotSupportedException">
      ///  The target quote type is not supported by this handler.
      /// </exception>
      public void SetQuote(CurveTenor tenor,
        QuotingConvention quoteType, double quoteValue)
      {
        var product = tenor.Product;
        var cds = product as CDS;
        if (cds != null)
        {
          if (quoteType != QuotingConvention.CreditSpread)
            throw QuoteTypeNotSupported(quoteType, product);
          cds.Premium = quoteValue;
          return;
        }
        var swapLeg = product as SwapLeg;
        if (swapLeg != null)
        {
          if (quoteType != QuotingConvention.Yield && quoteType != QuotingConvention.YieldSpread)
            throw QuoteTypeNotSupported(quoteType, product);
          swapLeg.Coupon = quoteValue;
          return;
        }
        var swp = product as Swap;
        if (swp != null)
        {
          if (quoteType != QuotingConvention.Yield && quoteType != QuotingConvention.YieldSpread)
            throw QuoteTypeNotSupported(quoteType, product);
          if (swp.IsPayerFixed || swp.IsSpreadOnPayer)
            swp.PayerLeg.Coupon = quoteValue;
          else
            swp.ReceiverLeg.Coupon = quoteValue;
          return;
        }
        var note = product as Note;
        if (note != null)
        {
          if (quoteType != QuotingConvention.Yield)
            throw QuoteTypeNotSupported(quoteType, product);
          note.Coupon = quoteValue;
          return;
        }
        if (product is StirFuture || product is Bond)
        {
          if (quoteType != QuotingConvention.FlatPrice)
            throw QuoteTypeNotSupported(quoteType, product);
          tenor.MarketPv = quoteValue;
          return;
        }
        if (product is FutureBase)
        {
          if (quoteType != QuotingConvention.ForwardFlatPrice)
            throw QuoteTypeNotSupported(quoteType, product);
          tenor.MarketPv = quoteValue;
          return;
        }
        var fra = product as FRA;
        if (fra != null)
        {
          if (quoteType != QuotingConvention.Yield)
            throw QuoteTypeNotSupported(quoteType, product);
          fra.Strike = quoteValue;
          return;
        }
        var curvePointHolder = product as CurvePointHolder;
        if (curvePointHolder != null)
        {
          if (quoteType != QuotingConvention.None
            && quoteType != QuotingConvention.Volatility)
          {
            throw QuoteTypeNotSupported(quoteType, product);
          }
          ((CurvePointHolder)product).Value = quoteValue;
          return;
        }
        throw new ArgumentException(String.Format(
          "{0} not support getting curve quote at the moment",
          product.GetType()));
      }
    } // class GeneralQuoteHandler

    private static Exception QuoteTypeNotSupported(
      QuotingConvention quoteType, IProduct product)
    {
      return new QuoteTypeNotSupportedException(String.Format(
        "Quote type {0} not supported in general handler for {1}",
        quoteType, product.GetType().Name));
    }

    public static double AdjustToHandleCrossingZero(
      this double bumpAmt, double baseQuote, string tenorName,
      BumpFlags flags = BumpFlags.None)
    {
      if ((baseQuote > 0 && baseQuote + bumpAmt < 0 &&
           (flags & BumpFlags.AllowDownCrossingZero) == 0)
          || (baseQuote < 0 && baseQuote + bumpAmt > 0 &&
              (flags & BumpFlags.ForbidUpCrossingZero) != 0))
      {
        logger.DebugFormat("Unable to bump tenor '{0}' by {1}, bump {2} instead",
          tenorName, bumpAmt, -baseQuote / 2);
        return -baseQuote / 2;
      }
      return bumpAmt;
    }

  } // class CurveTenorQuoteHandlers
}