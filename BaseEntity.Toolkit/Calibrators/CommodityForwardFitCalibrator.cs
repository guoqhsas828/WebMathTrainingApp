// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using log4net;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Calibrate implied commodity lease rate
  /// </summary>
  [Serializable]
  internal class CommodityForwardFitCalibrator : ForwardPriceCalibrator
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(CommodityForwardFitCalibrator));

    #region Constructors
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="projectionCurves">Projection curve/curves</param>
    /// <param name="index">Commodity index</param>
    /// <param name="fitSettings">Fit settings</param>
    public CommodityForwardFitCalibrator(Dt asOf, DiscountCurve discountCurve, IList<CalibratedCurve> projectionCurves, CommodityPriceIndex index, CalibratorSettings fitSettings)
      : base(asOf, asOf, discountCurve)
    {
      CommodityPriceIndex = index;
      CashflowCalibratorSettings = new CashflowCalibrator.CashflowCalibratorSettings();
      CurveFitSettings = fitSettings ?? new CalibratorSettings {CurveAsOf = asOf};
      ProjectionCurves = projectionCurves;
      SetParentCurves(ParentCurves, DiscountCurve);
      if (ProjectionCurves != null)
        foreach (var projectionCurve in projectionCurves)
          SetParentCurves(ParentCurves, projectionCurve);
    }
    #endregion

    #region Methods

    protected override void AddData(CurveTenor tenor, CashflowCalibrator calibrator, CalibratedCurve targetCurve)
    {
      var product = tenor.Product;
      var fut = product as CommodityFuture;
      if (fut != null)
      {
        var pricer = (CommodityFuturesPricer)GetPricer(targetCurve, fut);
        calibrator.Add(tenor.MarketPv, pricer.GetPaymentSchedule(null, AsOf), Settle, null, tenor.CurveDate, tenor.Weight, true);
        return;
      }

      var fwd = product as CommodityForward;
      if (fwd != null)
      {
        var pricer = (CommodityForwardPricer)GetPricer(targetCurve, fwd);
        calibrator.Add(0.0, pricer.GetPaymentSchedule(null, AsOf), Settle, pricer.DiscountCurve, tenor.CurveDate, tenor.Weight, false);
        return;
      }
      var swap = tenor.Product as CommoditySwap;
      if (swap != null)
      {
        var pricer = (CommoditySwapPricer)GetPricer(targetCurve, swap);
        var tgtPricer = swap.IsPayerFixed
                          ? pricer.PayerSwapPricer
                          : swap.IsReceiverFixed
                              ? pricer.ReceiverSwapPricer
                              : AreEqual(swap.PayerLeg.ReferenceIndex, CommodityPriceIndex) ? pricer.ReceiverSwapPricer : pricer.PayerSwapPricer;
        calibrator.Add(-tgtPricer.Pv() / tgtPricer.DiscountCurve.Interpolate(tgtPricer.AsOf, tgtPricer.Settle),
                       pricer.ReceiverSwapPricer.GetPaymentSchedule(null, targetCurve.AsOf),
                       pricer.Settle, DiscountCurve, tenor.CurveDate, tenor.Weight,
                       true);
      }
    }

    protected override void SetCurveDates(CalibratedCurve targetCurve)
    {
      foreach (CurveTenor ten in targetCurve.Tenors)
      {
        var maturity = ten.Product.Maturity;
        ten.CurveDate = maturity; // todo: for the time being
      }
    }

    protected override IPricer GetSpecializedPricer(ForwardPriceCurve curve, IProduct product)
    {
      var fut = product as CommodityFuture;
      if (fut != null)
      {
        var pricer = new CommodityFuturesPricer(fut, AsOf, Settle, (CommodityCurve)curve, 1.0 / fut.ContractSize)
          { FwdModelParameters = CurveFitSettings.FwdModelParameters };
        pricer.Validate();
        return pricer;
      }
      var fwd = product as CommodityForward;
      if (fwd != null)
      {
        var pricer = new CommodityForwardPricer(fwd, AsOf, Settle, DiscountCurve, (CommodityCurve)curve);
        pricer.Validate();
        return pricer;
      }
      var swap = product as CommoditySwap;
      if (swap != null)
      {
        var receiverLegPricer = new CommoditySwapLegPricer(swap.ReceiverLeg,
                                                           AsOf, Settle, 1.0, DiscountCurve, swap.ReceiverLeg.ReferenceIndex,
                                                           GetProjectionCurve(curve, ProjectionCurves, swap.ReceiverLeg.ReferenceIndex),
                                                           null);
        var payerLegPricer = new CommoditySwapLegPricer(swap.PayerLeg,
                                                        AsOf, Settle, -1.0, DiscountCurve, swap.PayerLeg.ReferenceIndex,
                                                        GetProjectionCurve(curve, ProjectionCurves, swap.PayerLeg.ReferenceIndex),
                                                        null);
        var pricer = new CommoditySwapPricer(receiverLegPricer, payerLegPricer);
        pricer.Validate();
        return pricer;
      }
      throw new ArgumentException(String.Format("Product {0} not supported", product.Description));
    }


    private static bool AreEqual(ReferenceIndex referenceIndex, ReferenceIndex otherIndex)
    {
      if (referenceIndex == null || otherIndex == null)
        return false;
      if (referenceIndex == otherIndex || referenceIndex.IndexName == otherIndex.IndexName)
        return true;
      return false;
    }

    private static CalibratedCurve GetProjectionCurve(CalibratedCurve curve, IList<CalibratedCurve> projectionCurves, ReferenceIndex projectionIndex)
    {
      if (projectionIndex == null)
        return null;
      if (AreEqual(curve.ReferenceIndex, projectionIndex))
        return curve;
      if (projectionCurves == null)
        throw new NullReferenceException("Cannot select projection curve for projection index from null ProjectionCurves");
      if (projectionCurves.Count == 1)
        return projectionCurves.First();//should we do this or let error occur?
      var retVal = projectionCurves.Where(c => AreEqual(c.ReferenceIndex, projectionIndex)).ToArray();
      if (retVal.Length == 0)
        throw new ArgumentException(String.Format("Cannot find projection curve corresponding to index {0}", projectionIndex.IndexName));
      if (retVal.Length > 1)
        throw new ArgumentException(String.Format("Two or more curves corresponding to index {0} were found among given projection curves.",
                                                  projectionIndex.IndexName));
      return retVal[0];
    }
    #endregion

    #region Properties
    internal IList<CalibratedCurve> ProjectionCurves { get; private set; } 

    /// <summary>
    /// Commodity price index
    /// </summary>
    internal CommodityPriceIndex CommodityPriceIndex
    {
      get;set;
    }
    #endregion
  }
}