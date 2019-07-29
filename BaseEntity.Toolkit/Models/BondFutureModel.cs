//
// Bond Future model
//   2011-2014. All rights reserved.
//

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  ///   Bond Future Models
  /// </summary>
  [ReadOnly(true)]
  public class BondFutureModel
  {
    #region Methods

    /// <summary>
    ///   Calculate last trading date and delivery/settlement for futures contract
    /// </summary>
    /// <remarks>
    ///   <para>Both are calculated together as the natural definitions from exchanges are can be relative to either the
    ///   last trading date or relative to the last delivery date.</para>
    /// </remarks>
    /// <param name="contractMonth">Futures contract month</param>
    /// <param name="contractYear">Futures contract year</param>
    /// <param name="lastTradingDayRuleRule">Rule for last trading day</param>
    /// <param name="deliveryDayRuleRule">Rule for last trading day</param>
    /// <param name="cal">Calendar for last trading day calculation (exchange business calendar)</param>
    /// <param name="lastDeliveryDate">Returned last futures delivery date</param>
    /// <param name="lastTradingDate">Returned last futures trading date</param>
    public static void LastTradingAndDeliveryDates(int contractMonth, int contractYear, BondFutureLastTradingDayRule lastTradingDayRuleRule,
      BondFutureDeliveryDayRule deliveryDayRuleRule, Calendar cal, out Dt lastTradingDate, out Dt lastDeliveryDate)
    {
      // Clear results
      lastTradingDate = new Dt();
      lastDeliveryDate = new Dt();
      // Calculate any explicit last trading date
      switch (lastTradingDayRuleRule)
      {
        case BondFutureLastTradingDayRule.Fifteenth:
          lastTradingDate = Dt.Roll(new Dt(15, contractMonth, contractYear), BDConvention.Following, cal);
          break;
        case BondFutureLastTradingDayRule.Last:
          lastTradingDate = Dt.Roll(Dt.LastDay(contractMonth, contractYear), BDConvention.Preceding, cal);
          break;
      }
      // Calculate any explicit last delivery date
      switch (deliveryDayRuleRule)
      {
      case BondFutureDeliveryDayRule.Tenth:
          lastDeliveryDate = Dt.Roll(new Dt(10, contractMonth, contractYear), BDConvention.Following, cal);
          break;
      case BondFutureDeliveryDayRule.Twentieth:
          lastDeliveryDate = Dt.Roll(new Dt(20, contractMonth, contractYear), BDConvention.Following, cal);
          break;
      case BondFutureDeliveryDayRule.Last:
          lastDeliveryDate = Dt.Roll(Dt.LastDay(contractMonth, contractYear), BDConvention.Preceding, cal);
          break;
      }
      // Calculate dependent last trading date
      switch (lastTradingDayRuleRule)
      {
        case BondFutureLastTradingDayRule.TwoBeforeDelivery:
          if( lastDeliveryDate.IsEmpty() ) throw new ArgumentException("Last delivery day rule must be explicit as last trading day is dependent");
          lastTradingDate = Dt.AddDays(lastDeliveryDate, -2, cal);
          break;
        case BondFutureLastTradingDayRule.SevenBeforeDelivery:
          if( lastDeliveryDate.IsEmpty() ) throw new ArgumentException("Last delivery day rule must be explicit as last trading day is dependent");
          lastTradingDate = Dt.AddDays(lastDeliveryDate, -7, cal);
          break;
        case BondFutureLastTradingDayRule.EighthBeforeDelivery:
          if( lastDeliveryDate.IsEmpty() ) throw new ArgumentException("Last delivery day rule must be explicit as last trading day is dependent");
          lastTradingDate = Dt.AddDays(lastDeliveryDate, -8, cal);
          break;
        case BondFutureLastTradingDayRule.Fifteenth:
        case BondFutureLastTradingDayRule.Last:
          // Explicit and calculated above
          break;
        default:
          throw new ArgumentException("Invalid futures last trading day rule");
      }
      // Calculate dependent last delivery date
      switch (deliveryDayRuleRule)
      {
      case BondFutureDeliveryDayRule.Tenth:
      case BondFutureDeliveryDayRule.Twentieth:
      case BondFutureDeliveryDayRule.Last:
          // Explicit and calculated above
          break;
      case BondFutureDeliveryDayRule.ThirdFollowingLastTradingDay:
          if( lastTradingDate.IsEmpty() ) throw new ArgumentException("Last trading day rule must be explicit as delivery day is dependent");
          lastDeliveryDate = Dt.AddDays(lastTradingDate, 3, cal);
          break;
      case BondFutureDeliveryDayRule.FollowingLastTradingDay:
          if( lastTradingDate.IsEmpty() ) throw new ArgumentException("Last trading day rule must be explicit as delivery day is dependent");
          lastDeliveryDate = Dt.AddDays(lastTradingDate, 1, cal);
          break;
        default:
          throw new ArgumentException("Invalid futures last delivery day rule");
      }
      return;
    }

    /// <summary>
    /// Australian Stock Exchange Bond Futures Pricing Formula
    /// </summary>
    /// <remarks>
    ///   See <a href="http://www.sfe.com.au/content/sfe/products/pricing.pdf">A Guide to the Pricing Conventions of ASX Interest Rate Products</a>
    /// </remarks>
    /// <param name="price">Futures price (in percent) (100 - yield)</param>
    /// <param name="nominalCoupon">Nominal coupon for contract (6%)</param>
    /// <param name="years">Years to maturity (3 or 10)</param>
    /// <param name="size">Contract size (1,000)</param>
    /// <returns>Futures price as a percent of notional</returns>
    public static double AsxTBondFuturePrice(double price, double nominalCoupon, int years, double size)
    {
      double A = (100.0 - price*100.0);
      double B = A/200.0;
      double C = Math.Round(1.0/(1.0 + B), 8, MidpointRounding.AwayFromZero);
      double D = Math.Round(Math.Pow(C, years * 2), 8, MidpointRounding.AwayFromZero);
      double E = 1 - D;
      double F = nominalCoupon*100.0/2.0*E;
      double G = Math.Round(F / B, 8, MidpointRounding.AwayFromZero);
      double H = 100.0*D;
      double I = G + H;
      double J = I*size/100.0;
      double K = Math.Round(J, 2, MidpointRounding.AwayFromZero);
      return K;
    }

    /// <summary>
    ///   Standard Bond Future Pricing (for margin) formula
    /// </summary>
    /// <remarks>
    ///   See <a href="http://www.cmegroup.com/trading/interest-rates/files/TreasuryFuturesPriceRoundingConventions_Mar_24_Final.pdf">
    ///   Marks-to-Market in U.S. Treasury Futures and Options:  Conventions for Computing Variation Margin Amounts</a>
    /// </remarks>
    /// <param name="price">Decimal price (116-27 1/4 = 1.168515625)</param>
    /// <param name="pointValue">Value per point per contract (per bp price change or 0.0001). Eg 10 for 10Yr TNote future</param>
    /// <returns>Futures value per contract</returns>
    public static double FuturePrice(double price, double pointValue)
    {
      // Multiply value per point by price in points. 1.00 = futures price of 100.00, 1bp is then 0.0001
      double val = price*1e4*pointValue;
      // Round result to the nearest penny. If result ends in a half-penny, then round up. 
      return Math.Round(val, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///   Calculate CME TBond future conversation factor
    /// </summary>
    /// <remarks>
    ///   <para><b>For US Treasury Bond and TNote Futures:</b></para>
    ///   <para>A bond’s conversion factor is defined as:</para>
    ///   <formula>factor = a * [(coupon/2) + c + d] – b</formula>
    ///   <para>where factor is rounded to 4 decimal places and coupon is the bond’s annual coupon in decimals.</para>
    ///   <para>n is the number of whole years from the first day of the delivery month to the maturity (or call)
    ///   date of the bond or note.</para>
    ///   <para>z is the number of whole months between n and the maturity (or call) date rounded down to the
    ///   nearest quarter for the 10-Year TNote, Classic Bond, and Ultra Bond futures contracts and to the nearest
    ///   month for the 2-Year, 3-Year, and 5-Year TNote futures contracts.</para>
    ///   <para><formula inline="true">v = z</formula> if z&lt;7
    ///   OR <formula inline="true">3</formula> if z≥7 (for TY, US, and UB)*
    ///   OR <formula inline="true">(z–6)</formula> if z≥7 (for TU, 3YR, and FV)**</para>
    ///   <para><formula inline="true">a = (1/1.03)^{v/6}</formula></para>
    ///   <para><formula inline="true">b = (coupon/2)*(6–v)/6</formula></para>
    ///   <para><formula inline="true">c = (1/1.03)^{2n}</formula> if z&lt;7
    ///   OR <formula inline="true">(1/1.03)^{2n+1}</formula> if otherwise</para>
    ///   <para><formula inline="true">d = (coupon/0.06)*(1–c)</formula></para>
    ///   <para>*TY, US, and UB indicate, respectively, the 10-Year U.S. TNote futures contract, the classic U.S
    ///   Treasury Bond futures contract, and the Ultra U.S. Treasury Bond futures contract.</para>
    ///   <para>**TU, 3YR, and FV indicate, respectively, the 2-Year U.S. TNote futures contract, the 3-Year
    ///   U.S. TNote futures contract, and the 5-Year U.S. TNote futures contract.</para>
    ///   <para>See <a href="http://www.cmegroup.com"/>.</para>
    /// </remarks>
    /// <param name="expiration">Futures last expiration date (only month and year are used)</param>
    /// <param name="maturity">Maturity date or first call date of deliverable bond</param>
    /// <param name="coupon">Coupon of deliverable bond</param>
    /// <returns>CME TBond Future converstion factor</returns>
    public static double CmeTBondFutureConversionFactor(Dt expiration, Dt maturity, double coupon)
    {
      // Number of whole years from first day of delivery month to the maturity
      int n = maturity.Year - expiration.Year;
      if (maturity.Month < expiration.Month)
        n--;
      // Number of whole months from n to maturity
      int z = maturity.Month - expiration.Month;
      if (z < 0 || z == 0)
        z += 12;
      // For 10 and 30 year futures, round to nearest quarter
      bool tbond = (n >= 7);
      if (tbond)
        z = (z/3)*3;
      int v = (z < 7) ? z : ((tbond) ? 3 : (z - 6));
      double a = Math.Pow(1.0/1.03, v/6.0);
      double b = (coupon/2.0)*(6.0 - v)/6.0;
      double c = (z < 7) ? Math.Pow(1.0/1.03, 2.0*n) : Math.Pow(1.0/1.03, 2.0*n + 1);
      double d = (coupon/0.06)*(1.0 - c);
      // Conversion factor
      double cf = a*((coupon/2.0) + c + d) - b;
      // Rounded to 4 decimal places
      return Math.Round(cf, 4);
    }

#if NOTYET
    /// <summary>
    /// Calculate CME TBond future conversation factor
    /// </summary>
    /// <remarks>
    /// <para><b>For German Bund Futures:</b></para>
    /// <para>A bond’s conversion factor is defined as:</para>
    /// <formula>
    /// Conversion factor = \frac{1}{(1+not/100)^f} * [ \frac{c}{100} * \frac{\delta_i}{act2} + \frac{c}{not} * ((1 + not/100) - \frac{1}{(1 + not/100)^n}) + \frac{1}{(1 + not/100)^n})- \frac{c}{100} * (\frac{\delta_i}{act2} - \frac{\delta_e}{act1})
    /// </formula>
    /// <para>Where:</para>
    /// <para>DD = Delivery date</para>
    /// <para>NCD = Next coupon after delivery date</para>
    /// <para>NCD1y = 1 year before the NCD</para>
    /// <para>NCD2y = 2 years before the NCD</para>
    /// <para>LCD = Last coupon date before the delivery date. Start interest
    /// period if last coupon date not available</para>
    /// <para><formula inline="true">\delta_e</formula> = NCD1y-DD</para>
    /// <para><formula inline="true">act1</formula> = NCD-NCD1y, where <formula inline="true">\delta_e < 0</formula> OR
    /// NCD1y-NCD2y, where <formula inline="true">\delta_e >= 0</formula></para>
    /// <para><formula inline="true">\delta_i</formula> = NCD1y-LCD</para>
    /// <para><formula inline="true">act2</formula> = NCD-NCD1y, where <formula inline="true">\delta_i < 0</formula> OR
    /// NCD1y-NCD2y, where <formula inline="true">\delta_i >= 0</formula></para>
    /// <para><formula inline="true">f</formula>=<formula inline="true">1 + \delta_e/act1</formula></para>
    /// <para><formula inline="true">c</formula> = Coupon</para>
    /// <para><formula inline="true">n</formula> = Integer years from the NCD until the maturity date of the bond</para>
    /// <para><formula inline="true">not</formula> = Notional coupon of futures contract</para>
    /// <seealso cref="http://www.eurexchange.com" />
    /// </remarks>
    /// <param name="bondType">Underlying Bond Type</param>
    /// <param name="refDate">Reference date - usually the first day of the expiration month</param>
    /// <param name="maturity">Maturity date of deliverable bond</param>
    /// <param name="coupon">Nomincal coupon of futures contract</param>
    /// <returns></returns>
    public static double ConversionFactor(BondType bondType, Dt refDate, Dt maturity, double coupon)
    {
      switch (bondType)
      {
        case BondType.USGovt:
          {
            // Number of whole years from reference date to maturity
            int n = maturity.Year - refDate.Year;
            if (maturity.Month < refDate.Month ||
              (maturity.Month == refDate.Month && maturity.Day < refDate.Day))
              n--;
            // Number of whole months from n to maturity
            int z = maturity.Month - refDate.Month;
            if (z < 0 || z == 0 && maturity.Day < refDate.Day)
              z += 12;
            // For 10 year futures, round to nearest quarter
            bool tbond = (n >= 7);
            if (tbond)
              z = (z / 3) * 3;
            int v = (z < 7) ? z : ((tbond) ? 3 : (z - 6));
            double a = Math.Pow(1.0 / 1.03, v / 6.0);
            double b = (coupon / 2.0) * (6.0 - v) / 6.0;
            double c = (z < 7) ? Math.Pow(1.0 / 1.03, 2.0 * n) : Math.Pow(1.0 / 1.03, 2.0 * n + 1);
            double d = (coupon / 0.06) * (1.0 - c);
            // Conversion factor
            double cf = a * ((coupon / 2.0) + c + d) - b;
            // Rounded to 4 decimal places
            return Math.Round(cf, 4);
          }
        case BondType.DEMGovt:
        /*
          {
            double not, f, deltai, act1, act2, deltae, c;
            /// <para>DD = Delivery date</para>
            /// <para>NCD = Next coupon after delivery date</para>
            /// <para>NCD1y = 1 year before the NCD</para>
            /// <para>NCD2y = 2 years before the NCD</para>
            /// <para>LCD = Last coupon date before the delivery date. Start interest
            /// period if last coupon date not available</para>
            /// <para><formula inline="true">\delta_e</formula> = NCD1y-DD</para>
            /// <para><formula inline="true">act1</formula> = NCD-NCD1y, where <formula inline="true">\delta_e < 0</formula> OR
            /// NCD1y-NCD2y, where <formula inline="true">\delta_e >= 0</formula></para>
            /// <para><formula inline="true">\delta_i</formula> = NCD1y-LCD</para>
            /// <para><formula inline="true">act2</formula> = NCD-NCD1y, where <formula inline="true">\delta_i < 0</formula> OR
            /// NCD1y-NCD2y, where <formula inline="true">\delta_i >= 0</formula></para>
            /// <para><formula inline="true">f</formula>=<formula inline="true">1 + \delta_e/act1</formula></para>
            /// <para><formula inline="true">c</formula> = Coupon</para>
            /// <para><formula inline="true">n</formula> = Integer years from the NCD until the maturity date of the bond</para>
            /// <para><formula inline="true">not</formula> = Notional coupon of futures contract</para>

            double cvf = 1 / Math.Pow(1.0 + not / 100.0, f) *
              (c/100.0 * deltai/act2 + c/not * ((1.0 + not/100.0) - 1.0/Math.Pow(1.0 + not/100.0, not)) + 1.0/Math.Pow(1 + not/100.0, not)) -
                         c/100.0*(deltai/act2 - deltae/act1);
            return cvf; // to round? RTD Jul'11
          }
          */
        case BondType.JGB:
        /*
int		num_int;
py_info_type	py_info;
date_type	set_date;
date_type	temp_date;
date_type	last_int;
date_type	next_int;
long		set_rdate;
long		nxti_rdate;
double		v, c, a, i;		// some temp vars
double		f, acc, pvn, pvf;	// some more temp vars
double		pow();
double		floor();

//	check the issue
if (acttype(issue) != BOND) {
  FMERR(FE_TYPE);
  return FALSE;
}	// switch
//	check the contract
if (acttype(contract) != BONDFUT) {
  FMERR(FE_TYPE);
  return FALSE;
}	// switch
// find the next and last interest dates
temp_date = issue->bd_calldate;
set_date = contract->bdf_deldate;
if (!_idates(issue, &temp_date, &set_date, &issue->bd_intdate,
    issue->bd_intfreq, &last_int, &next_int))
  return FALSE;
// and the number of interest periods
if (!_gnointp(&temp_date, &next_int, issue->bd_intfreq, &num_int))
  return FALSE;
// calc. some relative dates
if (!_rdate(&next_int, &nxti_rdate) ||
    !_rdate(&set_date, &set_rdate)) {
  FMERR(FE_DATE);
  return FALSE;
}	// if
c = issue->bd_coupon / issue->bd_intfreq;
i = contract->bdf_coupon / (contract->bdf_intfreq * 100.0);
v = 1.0 / (1.0 + i);
pvn = pow( v, (double) num_int);
a = 1.0 + (1.0 - pvn) / i;
//	get fractional period (in months)
f = (double) (next_int.month - set_date.month);
if (f <= 0.0)
  f += 12.0;
f *= (contract->bdf_intfreq / 12.0);
pvf = pow(v, f);
acc = (issue->bd_coupon / issue->bd_intfreq) * (1.0 - f);
*cvf = (pvf * (c * a + 100.0 * pvn) - acc) / 100.0;
//	now truncate it to 6 decimal places
*cvf = floor(*cvf * 1000000.0) / 1000000.0;
*/
        default:
          throw new ApplicationException("Bond futures type not supported");
      }
    }
#endif

    #endregion Methods

  } // BondFutureModel
}
