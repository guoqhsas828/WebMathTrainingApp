// 
//  -2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for a defaulted asset instrument
  /// </summary>
  [Serializable]
  public partial class DefaultedAssetPricer : PricerBase, IPricer, IRecoverySensitivityCurvesGetter
  {
    #region Constructors

    /// <summary>
    /// Constructor based on the product and market information
    /// </summary>
    /// <param name="product">The underlying product</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricer settlement date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="notional">Notional (Face or Redemption) amount</param>
    public DefaultedAssetPricer(DefaultedAsset product, Dt asOf, Dt settle,
      DiscountCurve discountCurve, RecoveryCurve recoveryCurve, double notional, double price)
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      RecoveryCurve = recoveryCurve;
      Notional = notional;
      PriceQuote = price;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="product">The underlying product</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricer settlement date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="notional">Notional (Face or Redemption) amount</param>
    /// <param name="price">Market price</param>
    public DefaultedAssetPricer(DefaultedAsset product, Dt asOf, Dt settle, DiscountCurve discountCurve, double notional, double price) 
      : base(product, asOf, settle)
    {
      Notional = notional;
      PriceQuote = price;
      DiscountCurve = discountCurve;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Accrued interest
    /// </summary>
    public override double Accrued()
    {
      return AccruedInt * Notional;
    }

    /// <summary>
    /// Effective clean price
    /// </summary>
    /// <returns></returns>
    public double EffectivePrice()
    {
      return PriceQuote;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="quote"></param>
    /// <returns></returns>
    public double PvFromQuote(double quote)
    {
      double df = DiscountCurve.DiscountFactor(AsOf, Product.Maturity);
      double pv = df * quote * Notional + Accrued() + PaymentPv();
      return pv;
    }

    /// <summary>
    /// Present value (including accrued) of trade to pricing as-of date given the natural quote for this trade.
    /// </summary>
    /// <returns></returns>
    public double ProductPvFromRecoveryQuote(double recoveryQuote)
    {
      return Notional * recoveryQuote + Accrued();
    }

    /// <summary>
    ///   Calculate the present value (full price times Notional) of the cash flow stream
    /// </summary>
    public override double ProductPv()
    {
      if (RecoveryCurve == null)
        return ProductPvFromRecoveryQuote(PriceQuote) + Notional * ModelBasis;
      if (Settle >= Product.Maturity)
        return 0.0;
      if (DiscountCurve == null)
        throw new ToolkitException("DiscountCurve must be specified before model pv can be calculated");
      if (RecoveryCurve == null)
        throw new ToolkitException("RecoveryCurve must be specified before model pv can be calculated");
      double recoveryRate = RecoveryCurve.RecoveryRate(Product.Maturity);
      double df = DiscountCurve.DiscountFactor(AsOf, Product.Maturity);
      double pv = (df * recoveryRate + ModelBasis) * Notional + Accrued();
      return pv;
    }

    
    /// <summary>
    /// Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <param name="ps"></param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>
    /// PaymentSchedule from the specified date or null if not supported
    /// </returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (from > Product.Maturity || RecoveryCurve == null)
        return ps ?? new PaymentSchedule();
      if (ps == null)
        ps = new PaymentSchedule();
      double recoveryRate = RecoveryCurve.RecoveryRate(Product.Maturity);
      var defSettlement = new DefaultSettlement(Dt.Empty, Product.Maturity, Product.Ccy, Notional, recoveryRate);
      defSettlement.IsFunded = true;
      ps.AddPayment(defSettlement);
      return ps;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// DefaultedAsset product
    /// </summary>
    public DefaultedAsset DefaultedAsset
    {
      get { return (DefaultedAsset)Product; }
    }

    ///<summary>
    /// Discount curve
    ///</summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Recovery curve
    /// </summary>
    public RecoveryCurve RecoveryCurve { get; private set; }

    /// <summary>
    /// Survival curve
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; set; }

    /// <summary>
    /// The Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public double ModelBasis { get; set; }

    /// <summary>
    /// Clean price
    /// </summary>
    public double PriceQuote { get; set; }

    /// <summary>
    /// Unit accrued accumulated 
    /// </summary>
    public double AccruedInt { get; set; }

    #endregion Properties

    #region Explicit Implementation of Recovery Sensitivity
    /// <summary>
    /// Gets the Recovery Curve that the defaulted loan is sensitive to.
    /// </summary>
    /// 
    /// <returns>List of Curves</returns>
    /// 
    IList<Curve> IRecoverySensitivityCurvesGetter.GetCurves()
    {
      var curves = new List<Curve>();
      if (RecoveryCurve != null)
      {
        curves.Add(RecoveryCurve);
      }

      return curves;
    }
    #endregion
  }
}