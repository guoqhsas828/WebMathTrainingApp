// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Equity product
  /// </summary>
  [Serializable]
  [ReadOnly(true)]
  public class Stock : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="ccy">Currency</param>
    /// <param name="ticker">Ticker</param>
    /// <param name="declareDividends">The declare dividends</param>
    public Stock(
      Currency ccy = Currency.None,
      string ticker = null,
      IReadOnlyList<Dividend> declareDividends = null)
      : base( Dt.Empty, Dt.MaxValue, ccy)
    {
      Ticker = ticker;
      DeclaredDividends = declareDividends ?? EmptyArray<Dividend>.Instance;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Ticker
    /// </summary>
    [Category("Base")]
    public string Ticker { get; }

    /// <summary>
    /// Dividend schedule
    /// </summary>
    public IReadOnlyList<Dividend> DeclaredDividends { get; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Create a stock with converted schedule
    /// </summary>
    /// <param name="currency">Currency </param>
    /// <param name="ticker">Ticker</param>
    /// <param name="schedule">Dividend Schedule</param>
    /// <returns></returns>
    public static Stock GetStockWithConvertedDividend(Currency currency, 
      string ticker, DividendSchedule schedule)
    {
      return new Stock(currency, ticker, schedule?
        .Select(d => new Dividend(d.Item1, d.Item1, d.Item2, d.Item3)).ToList());
    }
    

    #endregion Methods


    #region Nested type: Dividend

    /// <summary>
    /// Class DividentSpec.
    /// </summary>
    [Serializable]
    public class Dividend : Tuple<Dt, Dt, DividendSchedule.DividendType, double>
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="Dividend"/> class.
      /// </summary>
      /// <param name="exDivDate">The ex-div date</param>
      /// <param name="payDate">The payment date</param>
      /// <param name="type">The type of payment</param>
      /// <param name="amount">The payment amount</param>
      public Dividend(Dt exDivDate, Dt payDate,
        DividendSchedule.DividendType type, double amount)
        : base(exDivDate, payDate, type, amount)
      { }

      /// <summary>
      /// Gets the ex div date.
      /// </summary>
      /// <value>The ex div date.</value>
      public Dt ExDivDate => Item1;

      /// <summary>
      /// Gets the pay date.
      /// </summary>
      /// <value>The pay date.</value>
      public Dt PayDate => Item2;

      /// <summary>
      /// Gets the type.
      /// </summary>
      /// <value>The type.</value>
      public DividendSchedule.DividendType Type => Item3;

      /// <summary>
      /// Gets the amount.
      /// </summary>
      /// <value>The amount.</value>
      public double Amount => Item4;
    }
    #endregion
  }
}
