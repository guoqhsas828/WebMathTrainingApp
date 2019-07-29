/*
 *   2015. All rights reserved.
 */
using System;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Curves
{
  [Serializable]
  class SurvivalRateQuoteHandler : BaseEntityObject, ICurveTenorQuoteHandler
  {
    #region Helpers
    static double GetCreditSpread(Note note, DiscountCurve discountCurve)
    {
      Dt asOf = note.Effective, maturity = note.Maturity;
      var df = discountCurve == null ? 1.0
        : discountCurve.DiscountFactor(maturity);
      var rdf = RateCalc.PriceFromRate(note.Coupon,
        asOf, maturity, note.DayCount, note.Freq);
      var impliedSurvival = rdf / df;
      return RateCalc.RateFromPrice(impliedSurvival,
        asOf, maturity, note.DayCount, note.Freq);
    }

    static void SetCreditSpread(Note note, double spread,
      DiscountCurve discountCurve)
    {
      Dt asOf = note.Effective, maturity = note.Maturity;
      var df = discountCurve == null ? 1.0
        : discountCurve.DiscountFactor(maturity);
      var survival = RateCalc.PriceFromRate(spread,
        asOf, maturity, note.DayCount, note.Freq);
      var rdf = survival * df;
      note.Coupon = RateCalc.RateFromPrice(rdf,
        asOf, maturity, note.DayCount, note.Freq);
    }

    #endregion

    #region Data and constructor
    private DiscountCurve _discountCurve;

    public SurvivalRateQuoteHandler(DiscountCurve discountCurve)
    {
      _discountCurve = discountCurve;
    }

    #endregion

    #region Methods
    public IMarketQuote GetCurrentQuote(CurveTenor tenor)
    {
      return new MarketQuote(GetCreditSpread((Note)tenor.Product,
        _discountCurve), QuotingConvention.CreditSpread);
    }

    public double GetQuote(CurveTenor tenor, QuotingConvention targetQuoteType,
      Curve curve, Calibrator calibrator, bool recalculate)
    {
      if (targetQuoteType == QuotingConvention.CreditSpread)
        return GetCreditSpread((Note)tenor.Product, _discountCurve);
      else if (targetQuoteType == QuotingConvention.Yield)
        return ((Note)tenor.Product).Coupon;

      throw new ToolkitException(String.Format(
        "Unable to get quote with type {0}", targetQuoteType));
    }

    public void SetQuote(CurveTenor tenor,
      QuotingConvention targetQuoteType, double quoteValue)
    {
      if (targetQuoteType == QuotingConvention.CreditSpread)
        SetCreditSpread((Note)tenor.Product, quoteValue, _discountCurve);
      else if (targetQuoteType == QuotingConvention.Yield)
        ((Note)tenor.Product).Coupon = quoteValue;
      else
        throw new ToolkitException(String.Format(
          "Unable to get quote with type {0}", targetQuoteType));
    }

    public double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags flags)
    {
      var spread = GetCreditSpread((Note)tenor.Product, _discountCurve);
      double bumpAmt = CurveTenorQuoteHandlers.BumpCreditSpread(
        spread, bumpSize, flags, tenor.Product.Description);
      SetCreditSpread((Note)tenor.Product, spread + bumpAmt, _discountCurve);
      return ((flags & BumpFlags.BumpDown) == 0 ? bumpAmt : -bumpAmt) * 10000;
    }

    public IPricer CreatePricer(CurveTenor tenor,
      Curve curve, Calibrator calibrator)
    {
      return calibrator.GetPricer((CalibratedCurve)curve, tenor.Product);
    }

    internal static void SetCouponFromSurvivalProbability(
      CurveTenor tenor, double probability, 
      Func<Dt,double> recoveryFn, DiscountCurve discountCurve)
    {
      var note = (Note) tenor.Product;
      Dt asOf = note.Effective, maturity = note.Maturity;
      var df = discountCurve == null ? 1.0
        : discountCurve.DiscountFactor(maturity);
      var recovery = recoveryFn == null ? 0.0 : recoveryFn(maturity);
      var rdf = df * (probability + recovery * (1 - probability));
      note.Coupon = RateCalc.RateFromPrice(rdf,
        asOf, maturity, note.DayCount, note.Freq);
    }
    #endregion
  }
}
