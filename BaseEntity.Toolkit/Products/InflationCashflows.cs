// 
// 
// 

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Product to represent a set of real (i.e. inflation linked) cashflows
  /// </summary>
  /// <remarks>
  ///   <para>Inflation cashflows are a set of cashflow date, amount pairs which are inflation linked
  ///   from the given effective date. Typically used to represent future inflation-linked cashflow 
  ///   liabilities in a certain currency.
  ///   </para>
  ///   <para><b>Pricing Methodology</b></para>
  ///   <para>The value of the set of cashflows, V, is given as</para>
  ///   <formula>
  ///     V = \sum_{i=1}^{n} D_{t_i} f_i ( \frac { I_{t_i} } { I_{t_0} } )
  ///   </formula>
  ///   <para>where</para>
  ///   <list type="bullet">
  ///     <item><description><formula inline="true"> n </formula> is the number of fixed cashflows</description></item>
  ///     <item><description><formula inline="true"> f_i </formula> is the fixed amount for the 
  ///     ith cashflow</description></item>
  ///     <item><description><formula inline="true"> D_{t_i} </formula> is the discount factor
  ///     for the ith cashflow amount</description></item>
  ///     <item><description><formula inline="true"> I_{t_i} </formula> represents the inflation index for
  ///     payment date <formula inline="true"> t_i </formula> </description></item>
  ///     <item><description><formula inline="true"> I_{t_0} </formula> is the inflation index reference rate at
  ///     date <formula inline="true"> t_i </formula> </description></item>
  ///   </list>
  ///   <seealso cref="InflationCashflowsPricer"/>
  ///   <seealso cref="NominalCashflows"/>
  ///   <seealso cref="NominalCashflowsPricer"/>
  /// </remarks>
  [Serializable]
  public class InflationCashflows : NominalCashflows
  {
    #region Constructor

    /// <summary>
    /// Inflation cashflows constructor
    /// </summary>
    /// <param name="ccy">Currency</param>
    /// <param name="amounts">Nominal amounts</param>
    /// <param name="effectiveDate">Effective date for inflation cashflows</param>
    /// <param name="paymentDates">Payment dates</param>
    /// <param name="roll">roll convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="description"></param>
    public InflationCashflows(Currency ccy, Dt effectiveDate, double[] amounts, Dt[] paymentDates, BDConvention roll, Calendar cal, string description)
      : base(ccy, amounts, paymentDates, roll, cal, description)
    {
      Effective = effectiveDate;
    }

    #endregion
  }
}
