using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CDX"/>(CDX or iTraxx) CDS Index or 
  ///     a <see cref="BaseEntity.Toolkit.Products.LCDX"/>(LCDX or LevX) Loan CDS Index, unfunded or funded note
  ///     using the standard market pricing conventions.
  /// </para>
  /// </summary>
  ///
  /// <seealso cref="BaseEntity.Toolkit.Products.CDX">CDX Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Products.LCDX">LCDX Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel">Cashflow pricing model</seealso>
  /// <exclude />
  public interface ICDXPricer : IPricer
  {
    /// <summary>
    ///   Survival curves
    /// </summary>
    SurvivalCurve[] SurvivalCurves { get; set; }
    
    /// <summary>
    ///   Current market quote 
    /// </summary>
    /// 
    /// <details>
    ///   <para>CreditSpread and FlatPrice  quoting types are supported
    ///   and are set by <see cref="QuotingConvention"/>. The default
    ///   quoting convention is CreditSpread.</para>
    /// </details>
    double MarketQuote { get; set; }

    /// <summary>
    ///   Recovery rate for market standard calculations
    /// </summary>
    double MarketRecoveryRate { get; set; }

    /// <summary>
    ///   Original notional amount for pricing
    /// </summary>
    double Notional { get; }

    /// <summary>
    ///   Effective outstanding notional on the settlement date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is the effective notional at the settlement
    ///   date. It includes adjustments based on amortizations
    ///   and any defaults prior to the settlement date.  Depending
    ///   on pricing methods, it may include the name defaulted before
    ///   the pricer settle date but the default loss/recovery has
    ///   to be included in the prices (for example, when the default
    ///   is yet settled on the pricing date).</para>
    /// </remarks>
    double EffectiveNotional { get; }

    /// <summary>
    ///   Current outstanding notional on the settlement date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is the current notional at the settlement
    ///   date, excluding al the names defaulted before the settle date.</para>
    /// </remarks>
    double CurrentNotional { get; }

    /// <summary>
    ///   Calculate the intrinsic value of the Note
    /// </summary>
    double IntrinsicValue(bool currentMarket);

    /// <summary>
    ///   Calculate the value of the CDX Note.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market value is the settlement value in dollars (or other currency) of the index.</para>
    /// </remarks>
    double MarketValue();

    /// <summary>
    ///   Calculate the implied market quoted spread given a market value for the note.
    /// </summary>
    double PriceToSpread(double cleanPrice);


    /// <summary>
    ///   Calculate the clean market price given a market quoted spread for the note.
    /// </summary>
    double SpreadToPrice(double marketSpread);

    /// <summary>
    /// 
    /// </summary>
    double MarkToMarketFactor { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ps"></param>
    /// <param name="from"></param>
    /// <returns></returns>
    PaymentSchedule GeneratePayments(PaymentSchedule ps, Dt from);
  }
  
}
