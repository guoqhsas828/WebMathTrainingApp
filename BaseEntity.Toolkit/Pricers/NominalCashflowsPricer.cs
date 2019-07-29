//
// 
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   Fixed cashflows pricer
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.NominalCashflows" />
  /// </remarks>
  ///   <seealso cref="NominalCashflows"/>
  ///   <seealso cref="InflationCashflows"/>
  ///   <seealso cref="InflationCashflowsPricer"/>
  [Serializable]
  public class NominalCashflowsPricer : SimpleCashflowPricer
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    public NominalCashflowsPricer(
      NominalCashflows product,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve
      )
      : base(product, asOf, settle, discountCurve, null)
    {
      // Generate business date adjusted payment dates
      for (int i = 0; i < product.PayDates.Length; i++)
      {
        // Dates already rolled
        Add(product.PayDates[i], product.Amounts[i], 0.0, 0.0);
      }
    }

    #endregion Constructors

    #region Methods
    
    /// <summary>
    ///   Calculate Pv of cashflows
    /// </summary>
    public override double ProductPv()
    {
      var feePayDt = Count > 0 ? this[Count-1].Date : Dt.Empty;
      return feePayDt > Settle ? base.ProductPv() : 0.0;
    }
    
    #endregion
  }
}