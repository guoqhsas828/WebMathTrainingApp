// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{

  #region Constructors

  /// <summary>
  /// 
  /// </summary>
  public abstract class CommodityPricePayment : Payment
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CommodityPricePayment" /> class.
    /// </summary>
    /// <param name="payDate">The pay date.</param>
    /// <param name="ccy">The ccy.</param>
    /// <param name="periodStart">The period start.</param>
    /// <param name="periodEnd">The period end.</param>
    /// <param name="notional">The notional quantity.</param>
    protected CommodityPricePayment(Dt payDate,
                                    Currency ccy,
                                    Dt periodStart,
                                    Dt periodEnd,
                                    double notional)
      : base(payDate, ccy)
    {
      PeriodStartDate = periodStart;
      PeriodEndDate = periodEnd;
      Notional = notional;
    }

    #endregion
    /// <summary>
    /// Scale payment amount
    /// </summary>
    /// <param name="factor"></param>
    public override void Scale(double factor)
    {
      base.Scale(factor);
      Notional *= factor;
    }

    /// <summary>
    /// When Amount is derived from other fields implement this
    /// </summary>
    /// <returns></returns>
    protected override double ComputeAmount()
    {
      return Price * Notional;
    }

    /// <summary>
    /// Add data columns
    /// </summary>
    /// <param name="collection">Data column collection</param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      foreach (var x in new List<Tuple<string, Type>>
                        {
                          new Tuple<string, Type>("Notional", typeof(double)),
                          new Tuple<string, Type>("Period Start", typeof(string)),
                          new Tuple<string, Type>("Period End", typeof(string)),
                          new Tuple<string, Type>("Amount", typeof(double)),
                          new Tuple<string, Type>("Price", typeof(double)),
                        }.Where(x => !collection.Contains(x.Item1)))
      {
        collection.Add(new DataColumn(x.Item1, x.Item2));
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="row"></param>
    /// <param name="dtFormat"></param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row["Notional"] = Notional.ToString(CultureInfo.InvariantCulture);
      row["Period Start"] = PeriodStartDate.ToStr(dtFormat);
      row["Period End"] = PeriodEndDate.ToStr(dtFormat);
      row["Price"] = Price;
      row["Amount"] = Amount;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the notional quantity.
    /// </summary>
    /// <value>
    /// The notional quantity.
    /// </value>
    public double Notional { get; set; }

    /// <summary>
    /// Gets or sets the period start date.
    /// </summary>
    /// <value>
    /// The period start date.
    /// </value>
    public Dt PeriodStartDate { get; set; }

    /// <summary>
    /// Gets or sets the period end date.
    /// </summary>
    /// <value>
    /// The period end date.
    /// </value>
    public Dt PeriodEndDate { get; set; }

    /// <summary>
    /// Gets or sets the price.
    /// </summary>
    /// <value>
    /// The price.
    /// </value>
    public abstract double Price { get; set; }

    #endregion
  }
}