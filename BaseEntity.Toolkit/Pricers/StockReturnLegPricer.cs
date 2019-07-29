// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Collections;
using static BaseEntity.Toolkit.Pricers.AssetReturnLegPricerFactory;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Class StockReturnLegPricer.
  /// </summary>
  [Serializable]
  public class StockReturnLegPricer : AssetReturnLegPricer, IAssetReturnLegPricer<Stock>
  {
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="StockReturnLegPricer" /> class.
    /// </summary>
    /// <param name="stockReturnLeg">The stock return leg.</param>
    /// <param name="asOf">As-of date.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="stockCurve">The stock curve.</param>
    /// <param name="assetPriceIndex">The asset price index</param>
    public StockReturnLegPricer(
      AssetReturnLeg<Stock> stockReturnLeg, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      StockCurve stockCurve,
      IAssetPriceIndex assetPriceIndex = null)
      : base(stockReturnLeg, asOf, settle, discountCurve,
        new CalibratedCurve[] {stockCurve},
        assetPriceIndex ?? CurveBasedPriceCalculator
          .GetHistoricalPrices(stockCurve).ToAssetPriceIndex(
            QuotingConvention.FullPrice,
            stockReturnLeg.UnderlyingAsset, stockCurve.SpotCalendar,
            stockCurve.SpotDays))
    {
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates the price calculator.
    /// </summary>
    /// <returns>IPriceCalculator.</returns>
    protected override IPriceCalculator CreatePriceCalculator()
    {
      return new CurveBasedPriceCalculator(StockCurve, HistoricalPrices);
    }

    /// <summary>
    /// Gets the stock dividend payments.
    /// </summary>
    /// <param name="begin">The begin.</param>
    /// <param name="end">The end.</param>
    /// <exception cref="NotImplementedException"></exception>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    public override IEnumerable<Payment> GetUnderlyerPayments(
      Dt begin, Dt end)
    {
      var stock = Stock;
      if (stock.DeclaredDividends == null)
      {
        yield break;
      }
      for (int i = 0, n = stock.DeclaredDividends.Count; i < n; ++i)
      {
        var dividend = stock.DeclaredDividends[i];
        if (begin < dividend.ExDivDate && end >= dividend.ExDivDate) 
        {
          if (dividend.Type == DividendSchedule.DividendType.Fixed)
          {
            yield return new BasicPayment(dividend.PayDate,
              dividend.Amount, stock.Ccy);
          }
          else
          {
            throw new NotImplementedException(
              $"Dividend type {dividend.Type} not supported"
                + " in stock return leg pricer yet");
          }
        }
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the stock curve.
    /// </summary>
    /// <value>The stock curve.</value>
    public StockCurve StockCurve => Get<StockCurve>(ReferenceCurves);

    /// <summary>
    /// Gets the stock return leg.
    /// </summary>
    /// <value>The stock return leg.</value>
    public AssetReturnLeg<Stock> StockReturnLeg
      => (AssetReturnLeg<Stock>) Product;

    /// <summary>
    /// Gets the stock.
    /// </summary>
    /// <value>The stock.</value>
    private Stock Stock => StockReturnLeg.UnderlyingAsset;

    /// <summary>
    /// Product to price
    /// </summary>
    /// <value>The product.</value>
    IAssetReturnLeg<Stock> IPricer<IAssetReturnLeg<Stock>>.Product
      => StockReturnLeg;

    #endregion
  }
}
