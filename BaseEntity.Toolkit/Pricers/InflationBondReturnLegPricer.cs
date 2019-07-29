// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using static BaseEntity.Toolkit.Pricers.AssetReturnLegPricerFactory;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Inflation bond return leg pricer.
  /// </summary>
  [Serializable]
  public class InflationBondReturnLegPricer
    : AssetReturnLegPricer, IAssetReturnLegPricer<InflationBond>
  {
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="InflationBondReturnLegPricer" /> class.
    /// </summary>
    /// <param name="inflationBondReturnLeg">The inflation bond return leg.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="inflationCurve">The inflation curve.</param>
    /// <param name="discountCurveForPriceProjection">The discount curve for price projection</param>
    /// <param name="assetPriceIndex">The asset price index</param>
    public InflationBondReturnLegPricer(
      AssetReturnLeg<InflationBond> inflationBondReturnLeg,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      InflationCurve inflationCurve,
      DiscountCurve discountCurveForPriceProjection,
      IAssetPriceIndex assetPriceIndex)
      : base(inflationBondReturnLeg, asOf, settle, discountCurve,
        new CalibratedCurve[] {inflationCurve, discountCurveForPriceProjection},
        assetPriceIndex)
    {
      if (inflationCurve == null)
        throw new ArgumentException("inflationCurve cannot be null");
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the bond coupon payments.
    /// </summary>
    /// <param name="begin">The begin.</param>
    /// <param name="end">The end.</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    public override IEnumerable<Payment> GetUnderlyerPayments(
      Dt begin, Dt end)
    {
      // All bond payments from effective to maturity
      var bondPayments = GetInflationBondPricer()
        .GetPaymentSchedule(null, InflationBond.Effective);
      // Find bond coupon payments in the period [begin, end),
      // where the begin date is included in and the end date
      // is excluded from the period.
      return bondPayments.OfType<Payment>()
        .Where(p =>
        {
          var d = p.GetCutoffDate();
          return d >= begin && d < end;
        });
      //TODO: deal with redemption and bond notional adjustment
    }

    /// <summary>
    /// Creates the price calculator
    /// </summary>
    /// <returns>IPriceCalculator</returns>
    protected override IPriceCalculator CreatePriceCalculator()
    {
      // All bond payments from effective to maturity
      var pricer = GetInflationBondPricer();
      return new CashflowPriceCalculator(AsOf, 
        pricer.GetPaymentSchedule(), pricer.DiscountCurve, HistoricalPrices,
        WithDeflater ? pricer.IndexRatio : null as Func<Dt, double>,
        GetPriceAdjustment());
    }

    /// <summary>
    /// Gets the inflation bond pricer.
    /// </summary>
    /// <returns>InflationBondPricer.</returns>
    private InflationBondPricer GetInflationBondPricer()
    {
      return new InflationBondPricer(InflationBond, AsOf, Settle,
        1.0, DiscountCurveForPriceProjection, InflationIndex,
        InflationCurve, null, null)
      {
        DiscountingAccrued = true
      };
    }

    /// <summary>
    /// Gets the clean to dirty adjustment function for historical price quotes
    /// </summary>
    /// <returns>Func&lt;System.Double, Dt, System.Double&gt;.</returns>
    private Func<double, Dt, double> GetPriceAdjustment()
    {
      if (AssetPriceIndex == null ||
        AssetPriceIndex.PriceType != QuotingConvention.FlatPrice)
      {
        return null;
      }
      return (new AccruedInterestAdjustment
      {
        Bond = InflationBond,
        PriceIndex = AssetPriceIndex,
        InlationIndex = InflationIndex
      }).CleanToFullPrice;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the inflation bond.
    /// </summary>
    /// <value>The inflation bond.</value>
    public InflationBond InflationBond
      => InflationBondReturnLeg.UnderlyingAsset;

    /// <summary>
    /// Gets the inflation bond return leg.
    /// </summary>
    /// <value>The inflation bond return leg.</value>
    public AssetReturnLeg<InflationBond> InflationBondReturnLeg
      => (AssetReturnLeg<InflationBond>) Product;

    /// <summary>
    /// Inflation curve associated to the index
    /// </summary>
    /// <value>The reference curve.</value>
    public InflationCurve InflationCurve => Get<InflationCurve>(ReferenceCurves);

    /// <summary>
    /// Gets the index of the inflation.
    /// </summary>
    /// <value>The index of the inflation.</value>
    public InflationIndex InflationIndex => InflationCurve.InflationIndex;

    /// <summary>
    /// Gets or sets a value indicating whether to deflate the price by inflation index ratio.
    /// </summary>
    /// <value><c>true</c> if deflate the price by inflation index ratio; otherwise, <c>false</c>.</value>
    public bool WithDeflater { get; set; }

    /// <summary>
    /// Product to price
    /// </summary>
    /// <value>The product.</value>
    IAssetReturnLeg<InflationBond>
      IPricer<IAssetReturnLeg<InflationBond>>.Product => InflationBondReturnLeg;

    /// <summary>
    /// Gets the discount curve for inflation bond price projection.
    /// </summary>
    /// <value>The discount curve for price projection</value>
    public DiscountCurve DiscountCurveForPriceProjection
      => Get<DiscountCurve>(ReferenceCurves)??DiscountCurve;

    #endregion

    #region Data members

    [NonSerialized, NoClone]
    private IPriceCalculator _priceCalculator;

    #endregion

    #region Bond clean price to full price

    [Serializable]
    private class AccruedInterestAdjustment
    {
      public double CleanToFullPrice(double cleanPrice, Dt date)
      {
        var pi = PriceIndex;
        if (pi.SettlementDays != 0)
        {
          date = Dt.AddDays(date, pi.SettlementDays, pi.Calendar);
        }
        var pricer = new InflationBondPricer(Bond, date, date, 1.0,
          null, InlationIndex, null, InlationIndex.HistoricalObservations,
          null);
        return cleanPrice*pricer.IndexRatio(date) + pricer.Accrued();
      }

      public InflationBond Bond { private get; set; }
      public IAssetPriceIndex PriceIndex { private get; set; }
      public InflationIndex InlationIndex { get; set; }
    }

    #endregion
  }
}
