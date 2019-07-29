// 
// 
// 

using System;
using System.Collections;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Product to represent a set of nominal cashflows
  /// </summary>
  /// <remarks>
  ///   <para>Nominal cashflows are a set of cashflow date, amount pairs. Typically used
  ///   to represent future nominal cashflow liabilities in a certain currency.
  ///   </para>
  ///   <para><b>Pricing Methodology</b></para>
  ///   <para>The value of the set of cashflows, V, is given as</para>
  ///   <formula>
  ///     V = \sum_{i=1}^{n} D_{t_i} f_i
  ///   </formula>
  ///   <para>where</para>
  ///   <list type="bullet">
  ///     <item><description><formula inline="true"> n </formula> is the number of fixed cashflows</description></item>
  ///     <item><description><formula inline="true"> f_i </formula> is the fixed amount for the ith cashflow</description></item>
  ///     <item><description><formula inline="true"> D_{t_i} </formula> is the discount factor
  ///     for the ith cashflow amount</description></item>
  ///   </list>
  /// <seealso cref="InflationCashflows"/>
  /// <seealso cref="InflationCashflowsPricer"/>
  /// <seealso cref="NominalCashflowsPricer"/>
  /// </remarks>

  [Serializable]
  public class NominalCashflows : Product
  {
    #region Constructor

    /// <summary>
    /// Nominal cashflows constructor
    /// </summary>
    /// <param name="ccy">Currency</param>
    /// <param name="amounts">Nominal amounts</param>
    /// <param name="paymentDates">Payment dates</param>
    /// <param name="roll">roll convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="description"></param>
    public NominalCashflows(Currency ccy, double[] amounts, Dt[] paymentDates, BDConvention roll, Calendar cal, string description)
      : base(Dt.Empty, Dt.Empty, ccy)
    {
      Amounts = amounts;
      PayDates = paymentDates.Select(x => Dt.Roll(x, roll, cal)).ToArray<Dt>();
      Calendar = cal;
      Roll = roll;
      Maturity = PayDates.Last();
      Description = description;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Payment dates for the nominal flows
    /// </summary>
    public Dt[] PayDates { get; set; }

    /// <summary>
    /// Cashflow amounts
    /// </summary>
    public double[] Amounts { get; set; }

    /// <summary>
    /// Calendar
    /// </summary>
    public Calendar Calendar { get; set; }

    /// <summary>
    /// roll convention
    /// </summary>
    public BDConvention Roll { get; set; }

    #endregion

    #region Methods

    /// <summary>
    ///  Validate deal attributes
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (PayDates.Length != Amounts.Length)
      {
        InvalidValue.AddError(errors, this, "Number of pay amounts must equal the number of pay dates");
      }

      // Check payment dates are sorted and there are no duplicates
      if (PayDates.Length > 1)
      {
        for (int i = 1; i < PayDates.Length; i++)
        {
          if (PayDates[i - 1] > PayDates[i])
            InvalidValue.AddError(errors, this, "Pay dates must be in ascending order");
        }
      }
    }

    #endregion
  }
}