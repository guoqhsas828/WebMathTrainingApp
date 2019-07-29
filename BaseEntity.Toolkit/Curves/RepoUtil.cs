using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Curves
{
  ///<summary>
  /// Utility class with functions to imply repo rates, convert between spot quote and forward prices, calibrating repo curve
  ///</summary>
  public class RepoUtil
  {
    ///<summary>
    /// This method calibrates a repo curve based on rates, spreads and other related market components
    ///</summary>
    ///<param name="asOf">Repo curve date</param>
    ///<param name="ccy">Currency</param>
    ///<param name="maturities">Dates of fixed repo rates or the dates of forward prices quotes</param>
    ///<param name="repoRates">Repo rate quotes or implied repo rate from forward/spot bond prices</param>
    ///<param name="spreadDts">Tenor maturity of the long-end repo curve point</param>
    ///<param name="spreads">The spread between the repo rate and the reference rate </param>
    ///<param name="dc">The day-count convention to calculate re-investment proceeds</param>
    ///<param name="interpMethod">The method to interpolate repo rate</param>
    ///<param name="extrapMethod">The method to extrapolate repo rate</param>
    ///<param name="referenceCurve">The reference curve to project reference rates if needed </param>
    ///<param name="rateIndex">The reference index to project reference rates if needed</param>
    ///<returns></returns>
    public static RateQuoteCurve GenerateRepoCurve(
      Dt asOf,
      Currency ccy,
      Dt[] maturities,
      double[] repoRates,
      Dt[] spreadDts,
      double[] spreads,
      DayCount dc,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      DiscountCurve referenceCurve,
      BaseEntity.Toolkit.Base.ReferenceIndices.ReferenceIndex rateIndex
      )
    {
      if (maturities.Length != repoRates.Length)
        throw new ArgumentException("Repo rate array needs to have the same number of components as the date array");

      if (spreadDts.Length != spreads.Length)
        throw new ArgumentException("Spread array needs to have the same number of components as the spread maturity array");

      if (spreadDts.Length > 0)
      {
        if (referenceCurve == null)
          throw new ArgumentException("Reference curve is required to calculate reference rate");

        if (rateIndex == null)
          throw new ArgumentException("Reference index is required to calculate reference rate");
      }
      var toolkitRepoCurve = new RateQuoteCurve(asOf, interpMethod, extrapMethod, dc, ccy);
      var dates = new List<Dt>();
      var rates = new List<double>();
      var date2RateMapping = new Dictionary<Dt, double>();

      //to be safe, don't assume the components are entered as sorted
      for (int m = 0; m < maturities.Length; m++)
      {
        dates.Add(maturities[m]);
        date2RateMapping.Add(maturities[m], repoRates[m]);
      }

      for (int t = 0; t < spreadDts.Length; t++)
      {
        var swapFwdRate = CurveUtil.ImpliedForwardSwapRate(referenceCurve, asOf, spreadDts[t], true, rateIndex);
        dates.Add(spreadDts[t]);
        date2RateMapping.Add(spreadDts[t], spreads[t] + swapFwdRate);
      }



      dates.Sort();
      foreach (var date in dates)
        rates.Add(date2RateMapping[date]);

      toolkitRepoCurve.Add(dates.ToArray(), rates.ToArray());
      return toolkitRepoCurve;
    }

    ///<summary>
    /// This will generate a toolkit bond pricer for converting bond quotes into full price.
    ///</summary>
    ///<param name="initBond">Bond Object</param>
    ///<param name="asOf">Current Date</param>
    ///<param name="settle">Bond settle</param>
    ///<param name="curve">Discount curve to construct a bond pricer</param>
    ///<param name="marketQuote">The market quote for the bond</param>
    ///<param name="quotingConvention">Quoting convention of the market quote</param>
    ///<param name="currentCpn">The current coupon for floating bond (Bond object does not have this information)</param>
    ///<param name="projectionCurve">Projection curve</param>
    ///<returns></returns>
    public static BondPricer CreateBondPricer(Bond initBond, Dt asOf, Dt settle, DiscountCurve curve, double marketQuote, 
                                              QuotingConvention quotingConvention, double currentCpn, DiscountCurve projectionCurve)
    {
      Bond bond = initBond.Clone() as Bond;
      if (settle > bond.Maturity)
      {
        asOf = bond.Maturity;
        settle = bond.Maturity;
      }
      var bondPricer = new BondPricer(bond, asOf, settle);
      bondPricer.DiscountCurve = curve;
      if (bond.Floating)
      {
        if (curve == null || currentCpn <= 0.0)
          throw new ArgumentException("Must specify the reference curve and current coupon for a floating rate bond");
        if (currentCpn < bond.Coupon)
          throw new ArgumentException("The current coupon must be at least as large as the spread(over reference curve)");

        bondPricer.ReferenceCurve = projectionCurve?? bondPricer.DiscountCurve;
        bondPricer.CurrentRate = currentCpn;
      }

      bondPricer.QuotingConvention = quotingConvention;
      bondPricer.MarketQuote = marketQuote;
      return bondPricer;
    }

    ///<summary>
    /// This method calculates the flat implied repo rate based on spot price and forward price
    ///</summary>
    ///<param name="asOf">Pricing date</param>
    ///<param name="product">Bond product</param>
    ///<param name="currentCpn">Current coupon for floating rate bond</param>
    ///<param name="spotSettle">Bond spot settle date</param>
    ///<param name="fwdSettle">Forward settle date</param>
    ///<param name="spotQuote">Bond spot quote</param>
    ///<param name="spotQuoteConvention">Bond quote convention for spot quote</param>
    ///<param name="fwdPrice">Forward clean price</param>
    ///<param name="discountCurve">Discount curve</param>
    ///<param name="projectionCurve">Projection curve</param>
    ///<param name="repoDayCount">Day count to compute re-investment proceeds</param>
    ///<returns></returns>
    public static Double CalcImpliedRepoRate(Dt asOf, Bond product, double currentCpn, Dt spotSettle, Dt fwdSettle, double spotQuote, QuotingConvention spotQuoteConvention,
                                             double fwdPrice, DiscountCurve discountCurve, DiscountCurve projectionCurve, DayCount repoDayCount)
    {
      var spotBondPricer = CreateBondPricer(product, asOf, spotSettle, discountCurve, spotQuote, spotQuoteConvention,
                                            currentCpn, projectionCurve );
      var fwdBondPricer = CreateBondPricer(product, asOf, fwdSettle, discountCurve, fwdPrice,
                                           QuotingConvention.ForwardFlatPrice, currentCpn, projectionCurve);
      return CalcImpliedRepoRate(spotSettle, spotBondPricer.SpotSettleCashflowAdpater,
                                 spotBondPricer.FullPrice(), fwdSettle, fwdBondPricer.FullPrice(), repoDayCount);
    }

    /// <summary>
    /// This function calculates the flat implied repo rate based on spot price and forward price, which is conversion factor adjusted invoice price
    /// </summary>
    /// <param name="spotFullPrice">Spot full price</param>
    /// <param name="delivery">Forward date</param>
    /// <param name="settle">Spot settle</param>
    /// <param name="cf">Cash flow between spot date and forward date</param>
    /// <param name="futureFullPrice">Future invoice price</param>
    /// <param name="repoDayCount">The day count convention to calculate the investment proceed</param>
    /// <returns></returns>
    internal static Double CalcImpliedRepoRate(Dt settle, CashflowAdapter cf, double spotFullPrice, Dt delivery, double futureFullPrice, DayCount repoDayCount)
    {

      ///////////////////////////////////////////////////////////////////////////////////////////////////
      // Note that the pricer carries with it the actual Settle Date instead of Trade date and
      // the delivery date is the actual cash settlement date instead of futures expiration date 
      // The closed form formula for the Implied repo is as follows
      //  [FP12*CF +Ae + Sum(IC(i))-(cleanPrice+ Ab)]*360 / ((delivery-settle)*(price of bond+Ab)- Sum((IC(i)*(delivery-couponDate(i)))
      // FP12 = Futures Price
      // CF = Conversion Factor of the bond
      // Ab = accrued interest of bond at trade settle date
      // Ae = accrued interest of bond at delivery date(delivery to cover short)
      // IC(i) = Interim coupons between trade settle date and delivery date. There might be multiple Interim coupons
      // (delivery-coupon(i)  = Gives us the number of days for which the i-th Interim coupon was reinvested
      // (delivery-settle) = Gives us the number of days for which the initial cash outflow was invested at Implied Repo
      /////////////////////////////////////////////////////////////////////////////////////////////////////////

      // Get the cashflows and check the cashflow dates
      double ic = 0;  // Interim Coupons
      double icReinvestment = 0;

      for (int i = 0; i <= cf.Count - 1; i++)
      {
        Dt paymentDate = cf.GetDt(i);
        if (paymentDate > settle && paymentDate <= delivery)
        {
          // These coupon payments are paid between trade settle and delivery daye and hence need to be considered for our calculation
          double accrual = cf.GetAccrued(i);
          ic += accrual;
          icReinvestment += -accrual * Dt.Fraction(paymentDate, delivery, repoDayCount);
        }
      }
      //Calculate the Implied Rate of Return as a money market rate using an repoDaycount money market day-count convention
      double impliedRepo = ((futureFullPrice + ic - spotFullPrice) / (Dt.Fraction(settle, delivery, repoDayCount) * spotFullPrice - icReinvestment));

      return impliedRepo;
    }

    ///<summary>
    /// This function calculates the invoice forward price from the spot price
    ///</summary>
    ///<param name="spotFullPrice">Spot full price</param>
    ///<param name="delivery">Forward date</param>
    ///<param name="repoRate">The flat repo rate</param>
    ///<param name="settle">Spot settle</param>
    ///<param name="cf">Cash flow between spot date and forward date</param>
    ///<param name="repoDayCount">The day count convention to calculate the investment proceed</param>
    ///<returns></returns>
    internal static double ConvertSpotQuoteToForwardPrice(Dt settle, CashflowAdapter cf, double spotFullPrice, Dt delivery, double repoRate, DayCount repoDayCount)
    {
      double ic = 0;  // Interim Coupons
      double icReinvestment = 0;
      for (int i = 0; i <= cf.Count - 1; i++)
      {
        Dt paymentDate = cf.GetDt(i);
        if (paymentDate > settle && paymentDate <= delivery)
        {
          // These coupon payments are paid between trade settle and delivery daye and hence need to be considered for our calculation
          double accrual = cf.GetAccrued(i);
          ic += accrual;
          icReinvestment += -accrual * Dt.Fraction(paymentDate, delivery, repoDayCount);
        }
      }

      return repoRate * (Dt.Fraction(settle, delivery, repoDayCount) * spotFullPrice - icReinvestment) + spotFullPrice - ic;
    }

    ///<summary>
    /// This function converts the forward price of bond to spot full price
    ///</summary>
    ///<param name="cf">Cash flow between settle and delivery date</param>
    ///<param name="delivery">Delivery date</param>
    ///<param name="fwdFullPrice">The forward quote</param>
    ///<param name="repoRate">Repo rate between settle and future delivery date</param>
    ///<param name="settle">Spot settle</param>
    ///<param name="repoDayCount">The day count convention to calculate the investment proceed</param>
    ///<returns></returns>
    internal static double ConvertSpotPriceFromForwardPrice(Dt settle, CashflowAdapter cf, Dt delivery, double fwdFullPrice, double repoRate, DayCount repoDayCount)
    {
      double ic = 0;  // Interim Coupons
      double icReinvestment = 0;
      for (int i = 0; i <= cf.Count - 1; i++)
      {
        Dt paymentDate = cf.GetDt(i);
        if (paymentDate > settle && paymentDate <= delivery)
        {
          // These coupon payments are paid between trade settle and delivery daye and hence need to be considered for our calculation
          double accrual = cf.GetAccrued(i);
          ic += accrual;
          icReinvestment += -accrual * Dt.Fraction(paymentDate, delivery, repoDayCount);
        }
      }

      return (fwdFullPrice + ic + repoRate * icReinvestment) / (1 + repoRate * Dt.Fraction(settle, delivery, repoDayCount));

    }

  }
}
