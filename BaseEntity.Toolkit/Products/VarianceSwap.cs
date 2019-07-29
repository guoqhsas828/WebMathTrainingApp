/*
 *   2016. All rights reserved.
 */
using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using System.Collections;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Variance Swap Forward product
  /// </summary>
  /// <remarks>
  ///   <para>Variance swap forwards are OTC contracts commiting a buyer and a seller to exchange an 
  ///   realized variance over a specified period at an agreed variance rate at an agreed future date.</para>
  ///   <para>Terms to be expressed in percentaage annualized 
  ///   volatility terms.</para>
  ///   <para>A variance swap forward is an OTC contract between two counterparties to buy or sell realized variance at a specified future time and at
  ///   a specified agreed variance price. The party agreeing to buy the underlying asset is long the variance and the party
  ///   agreeing to sell the underlying asset is said to be short the contract. The price agreed to buy and sell the underlying
  ///   asset is termed the variance stike price and the date agreed is the value or maturity date.</para>
  ///   <para>At the trade inception no money is exchanged. The value of the contract at maturity <m>T</m> is a function of difference
  ///   between the value of the underlying variance <m>V_T</m> and the variance strike price <m>K</m> on the maturity date.</para>
  ///   <para>For a long position this is <m>F_T = S_T - K</m></para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.VarianceSwapPricer"/>
  /// <example>
  /// <para>The following example demonstrates constructing a Fx forward.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = new Dt(30, 7, 2016);   // First observation is July 30, 2016
  ///   Dt valueDate = new Dt(1, 12, 2018);       // Last observation is December 1, 2018
  ///   Dt maturityDate = new Dt(3, 12, 2018);    // Payment date is December 3, 2018
  /// 
  ///   var varSwap = new VarianceSwap(
  ///     effectiveDate,                          // first observation date
  ///     valueDate,                              // last observation date
  ///     maturityDate,                           // Date of payment
  ///     Currency.USD,                           // Currency USD
  ///     "SPX",                                  // Equity index 
  ///     18.0,                                   // Variance strike rate of 18 % annualized volatility
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class VarianceSwap : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Variance swap constructor when time fractions are calculated using actual / 365.
    /// </summary>
    /// <param name="effective">First observation date</param>
    /// <param name="maturity">Payment date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="valueDate">Valuation date</param>
    /// <param name="index">Equity index name</param>
    /// <param name="strike">Variance strike price quoted in annualised percentage terms, i.e. 20 for an annualized volatility of 20%</param>
    public VarianceSwap(Dt effective, Dt valueDate, Dt maturity, Currency ccy, string index, double strike)
      : this(effective, valueDate, maturity, ccy, index, strike, Calendar.None, BDConvention.None) { }

    /// <summary>
    /// Variance swap constructor when time fractions are calculated using a business calendar
    /// </summary>
    /// <param name="effective">First observation date</param>
    /// <param name="maturity">Payment date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="valueDate">Valuation date</param>
    /// <param name="index">Equity index name</param>
    /// <param name="strike">Variance strike price quoted in annualised percentage terms, i.e. 20 for an annualized volatility of 20%</param>
    /// <param name="cal">Business calendar to calculate time fractions</param>
    /// <param name="roll">Business day roll convention</param>
    public VarianceSwap(Dt effective, Dt valueDate, Dt maturity, Currency ccy, string index, double strike, Calendar cal, BDConvention roll)
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, Frequency.None, roll, cal, CycleRule.None, new CashflowFlag())
    {
      ValuationDate = valueDate.IsEmpty() ? maturity : valueDate;
      StrikePrice = strike;
      Calendar = cal;
      Index = index;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">if product not valid</exception>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      // Invalid NearValueDate date
      if (!ValuationDate.IsValid() || ValuationDate > Maturity)
        InvalidValue.AddError(errors, this, "ValueDate",
          $"Invalid value date. Must be empty or valid date, not {ValuationDate} and less than maturity {Maturity}");
      // Strike has to be >= 0
      if (StrikePrice <= 0.0)
        InvalidValue.AddError(errors, this, "StrikePrice ", $"Invalid Variance Strike. Must be greater than 0, not {StrikePrice}");
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Equity index
    /// </summary>
    [Category("Base")]
    public string Index { get; set; }

    /// <summary>
    /// Variance strike price quoted in annualised percentage terms, i.e. 20 for an annualized volatility of 20%.
    /// </summary>
    [Category("Base")]
    public double StrikePrice { get; set; }

    ///// <summary>
    ///// 
    ///// </summary>
    //public Calendar Calendar { get; set; }

    /// <summary>
    /// Last observation date
    /// </summary>
    [Category("Base")]
    public Dt ValuationDate
    {
      get;
      set;
    }

    #endregion Properties
  }
}
