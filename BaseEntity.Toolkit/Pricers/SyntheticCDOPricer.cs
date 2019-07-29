/*
 * SyntheticCDOPricer.cs
 *
 *  -2008. All rights reserved. 
 *
 * TBD: Change Pv method to full price. Don't forget to adjust calls from Excel and NTD to reflect this. RTD Jul05.
 * TBD: Resolve/complete USE_CASHFLOW_PRICER option RTD Jul05
 * TBD: Make this an abstract base class. RTD Sep05
 *
 */
//#define Use_Events

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;
using CDSBasket = BaseEntity.Toolkit.Pricers.Baskets.CreditPool;
using PriceCalc = BaseEntity.Toolkit.Pricers.Baskets.PriceCalc;

namespace BaseEntity.Toolkit.Pricers
{
  #region Config
  /// <exclude />
  [Serializable]
  public class SyntheticCDOPricerConfig
  {
    /// <exclude />
    [Util.Configuration.ToolkitConfig("Use the original instead of current notional for fee calculations")]
    public readonly bool UseOriginalNotionalForFee = false;

    /// <exclude />
    [Util.Configuration.ToolkitConfig("Whether to ignore accrued settings")]
    public readonly bool IgnoreAccruedSetting = false;
    
    /// <exclude />
    [Util.Configuration.ToolkitConfig("AdjustDurationForRemainingNotional when basket has alraedy-occured defaults")] 
    public readonly bool AdjustDurationForRemainingNotional = true;

    /// <exclude />
    [ToolkitConfig("Whether to make full accrual payments after default and get reimbursed with recovery payment.  If false, protection buyer only pays accrual between previous IMM date and default date with recovery settlement.")]
    public readonly bool SupportAccrualRebateAfterDefault = true;
  }
  #endregion Config

  ///
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO</see> using the
  ///   <see cref="BasketPricer">Basket Pricer Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.SyntheticCDO" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BasketPricer" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche Product</seealso>
  /// <seealso cref="BasketPricer">Basket Pricer</seealso>
  [Serializable]
  public partial class SyntheticCDOPricer : PricerBase, IPricer, IAnalyticDerivativesProvider, IRatesLockable
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SyntheticCDOPricer));

    #region Config

    // Default settings: hard coded, retire later
    private const bool adjustNext_ = false;
    private const bool useHeteroModelAsDefault_ = false;

    /// <summary>
    ///   Using original notional to calculate all fee values (Wrong!)
    /// </summary>
    /// <exclude />
    public bool UseOriginalNotionalForFee
    {
      get { return settings_.SyntheticCDOPricer.UseOriginalNotionalForFee; }
    }

    /// <summary>
    ///   Using original notional to calculate all fee values (Wrong!)
    /// </summary>
    /// <exclude />
    public bool IgnoreAccruedSetting
    {
      get { return settings_.SyntheticCDOPricer.IgnoreAccruedSetting; }
    }

    /// <summary>
    /// Adjust risky duration for remaining notional when basket has defaulted names
    /// </summary>
    /// <exclude />
    public bool AdjustDurationForRemainingNotional
    {
      get { return adjustDurationForRemainingNotional_; }//settings_.SyntheticCDOPricer.AdjustDurationForRemainingNotional; }
      set { adjustDurationForRemainingNotional_ = value; }
    }

    /// <summary>
    ///   Using original notional to calculate all fee values (Wrong!)
    /// </summary>
    /// <exclude />
    public static bool UseHeterogeneousModelAsDefault
    {
      get { return useHeteroModelAsDefault_; }
    }

    #endregion // Config

    #region Utility Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
      if (basket_ == null)
        InvalidValue.AddError(errors, this, "Basket", String.Format("Invalid basket. Cannot be null."));

      if (accruedFractionOnDefault_ < 0.0 || accruedFractionOnDefault_ > 1.0)
        InvalidValue.AddError(errors, this, "AccruedFractionOnDefault", String.Format("Invalid accrued on default {0}. Must be >= 0 and <= 1", accruedFractionOnDefault_));

      if (defaultTiming_ < 0.0 || defaultTiming_ > 1.0)
        InvalidValue.AddError(errors, this, "DefaultTiming", String.Format("Invalid default timing {0}. Must be >= 0 and <= 1", defaultTiming_));

      if (CDO.CdoType == CdoType.FundedFloating || CDO.CdoType == CdoType.IoFundedFloating)
        if (rateResets_ == null || rateResets_.Count == 0)
          InvalidValue.AddError(errors, this, "RateResets", String.Format("RateResets can neither be empty nor null for floating coupon pricing."));

      RateResetUtil.Validate(rateResets_, errors);

      return;
    }


    #endregion

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///
    public
    SyntheticCDOPricer(SyntheticCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve)
      : base(product, basket.AsOf, basket.Settle)
    {
      this.discountCurve_ = discountCurve;
      this.basket_ = basket;
      base.Notional = basket.TotalPrincipal * product.TrancheWidth;
      changed_ = SyntheticCDOPricer.ResetFlag.All;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    ///
    public
    SyntheticCDOPricer(SyntheticCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve, DiscountCurve referenceCurve)
      : base(product, basket.AsOf, basket.Settle)
    {
      this.discountCurve_ = discountCurve;
      this.ReferenceCurve = referenceCurve;
      this.basket_ = basket;
      base.Notional = basket.TotalPrincipal * product.TrancheWidth;
      changed_ = SyntheticCDOPricer.ResetFlag.All;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="notional">tranche notional</param>
    ///
    public
    SyntheticCDOPricer(SyntheticCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double notional)
      : base(product, basket.AsOf, basket.Settle)
    {
      this.discountCurve_ = discountCurve;
      this.basket_ = basket;
      base.Notional = notional;
      changed_ = SyntheticCDOPricer.ResetFlag.All;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="notional">tranche notional</param>
    ///
    public
    SyntheticCDOPricer(SyntheticCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      double notional)
      : base(product, basket.AsOf, basket.Settle)
    {
      this.discountCurve_ = discountCurve;
      this.referenceCurve_ = referenceCurve;
      this.basket_ = basket;
      base.Notional = notional;
      changed_ = SyntheticCDOPricer.ResetFlag.All;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="notional">tranche notional</param>
    /// <param name="rateResets">rate Resets list for FRNs</param>
    ///
    public
    SyntheticCDOPricer(
      SyntheticCDO product, BasketPricer basket, DiscountCurve discountCurve,
      double notional, List<RateReset> rateResets
      )
      : base(product, basket.AsOf, basket.Settle)
    {

      this.discountCurve_ = discountCurve;
      this.basket_ = basket;
      this.rateResets_ = rateResets;
      base.Notional = notional;
      changed_ = SyntheticCDOPricer.ResetFlag.All;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDO tranche to price</param>
    /// <param name="basket">The basket model used to price the cdo</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="notional">tranche notional</param>
    /// <param name="rateResets">rate Resets list for FRNs</param>
    ///
    public
    SyntheticCDOPricer(
      SyntheticCDO product, BasketPricer basket, DiscountCurve discountCurve, DiscountCurve referenceCurve,
      double notional, List<RateReset> rateResets
      )
      : base(product, basket.AsOf, basket.Settle)
    {

      this.discountCurve_ = discountCurve;
      this.referenceCurve_ = referenceCurve;
      this.basket_ = basket;
      this.rateResets_ = rateResets;
      base.Notional = notional;
      changed_ = SyntheticCDOPricer.ResetFlag.All;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      SyntheticCDOPricer obj = (SyntheticCDOPricer)base.Clone();
      obj.basket_ = (BasketPricer)Basket.Clone(); // force update states
      obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();
      obj.referenceCurve_ = (referenceCurve_ == null) ? null : (DiscountCurve)referenceCurve_.Clone();
      obj.rateResets_ = CloneUtil.Clone(rateResets_);

      //- If the original basket is base correlation basket and
      //- it shares the same discount curve with the CDO pricer,
      //- so be it in the cloned pricer.
      if (basket_ is BaseCorrelationBasketPricer &&
        ((BaseCorrelationBasketPricer)basket_).DiscountCurve == discountCurve_)
      {
        ((BaseCorrelationBasketPricer)obj.basket_).DiscountCurve = obj.discountCurve_;
      }

      return obj;
    }

    public SyntheticCDOPricer Substitute(
      SyntheticCDO cdo, BasketPricer basket,
      double notional, bool synchronize)
    {
      var pricer = (SyntheticCDOPricer)MemberwiseClone();
      if (basket != null)
        pricer.basket_ = basket;
      if (cdo != null)
        pricer.Product = cdo;
      if (notional != 0)
        pricer.Notional = notional;
      if (synchronize)
      {
        pricer.changed_ = ResetFlag.All;
      }
      else
      {
        pricer.changed_ = ResetFlag.None;
        pricer.UpdateEffectiveNotional();
      }
      return pricer;
    }

    /// <summary>
    ///   Create a new cdo pricer with a different correlation object.
    ///   <preliminary />
    /// </summary>
    /// 
    /// <remarks>
    ///   This is a light-weight function.  The new pricer is created
    ///   sharing the same underlying curves and principals as the
    ///   original pricer, but with a different correlation object.
    /// </remarks>
    ///
    /// <param name="correlation">
    ///   The correlation object to be used in the new pricer.
    ///   If it is null, the correlation in original pricer is used.
    /// </param>
    /// 
    /// <returns>A new copy of cdo pricer</returns>
    /// 
    /// <exception cref="ArgumentException">
    ///   <para>Correlation is not compatible with the pricer.</para>
    /// </exception>
    /// 
    /// <example>
    /// <para>
    ///   The following example calculates the Value change caused by
    ///   a parallel shift of 1% in the detachment/attachment points.
    /// </para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo and pricer
    ///   // ...
    /// 
    ///   SyntheticCDOPricer bumpedPricer = pricer.Substitute(
    ///     bumpedCorrelation);
    ///   double delta = bumpedPricer.Pv() - pricer.Pv();
    /// </code>
    /// </example>
    public SyntheticCDOPricer Substitute(
      CorrelationObject correlation)
    {
      if (correlation == null)
        correlation = basket_.Correlation;

      // Clone the cdo
      SyntheticCDO cdo = (SyntheticCDO)CDO.Clone();

      // Create a basket pricer with appropriate loss levels
      BasketPricer basket = (BasketPricer)Basket.Duplicate();

      if (Basket.Correlation is BaseCorrelationObject)
      {
        // Sanity check 
        if (!(correlation is BaseCorrelationObject))
          throw new System.ArgumentException(String.Format(
            "Correlation {0} must be a base correlation object, not {1}",
            correlation.Name, correlation.GetType().FullName));
        // Set base correlation
        ((BaseCorrelationBasketPricer)basket).Set(
          null, (BaseCorrelationObject)correlation);
      }
      else
      {
        // Sanity check
        if (correlation is BaseCorrelationObject)
          throw new System.ArgumentException(String.Format(
            "Correlation {0} cannot be a base correlation object",
            correlation.Name));
        Correlation corr1 = (Correlation)correlation;
        Correlation corr0 = (Correlation)Basket.Correlation;
        if (corr0.NameCount != corr1.NameCount)
          throw new System.ArgumentException(String.Format(
            "correlation must have the name count {1}, not {0}",
            corr1.NameCount, corr0.NameCount));
        // Set correlation
        basket.Correlation = corr1;
      }

      // Mark the basket to activate recalculating
      basket.Reset();

      //- Return a subsitituted CDO pricer
      return Substitute(cdo, basket, 0, true);
    }

    /// <summary>
    ///   Create a new CDO pricer with different underlying curves,
    ///   principals, correlation, attachment and detachment points.
    ///   <preliminary />
    /// </summary>
    /// 
    /// <remarks>
    ///   This is a heavy-weight function by which the basket size
    ///   can be modified.  It allows the array of input survival
    ///   curves to have a different length than the basket size
    ///   and checks if all the inputs are consistent with the new size.
    /// </remarks>
    /// 
    /// <param name="survivalCurves">
    ///   Survival Curve calibrations of individual names
    /// </param>
    /// <param name="recoveryCurves">
    ///   Recovery curves of individual names (or null to
    ///   use survivalCurve recoveries)
    /// </param>
    /// <param name="principals">
    ///   Participations of individual names
    /// </param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="attachment">Attachment</param>
    /// <param name="detachment">Detachment</param>
    /// 
    /// <returns>A new copy of cdo pricer</returns>
    public SyntheticCDOPricer Substitute(
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      CorrelationObject correlation,
      double attachment, double detachment)
    {
      return Substitute(new CreditPool(principals,
        survivalCurves, recoveryCurves, null, null,
        Basket.RefinanceCurves != null, null),
        correlation, attachment, detachment);
    }

    /// <summary>
    ///   Create a new cdo pricer with different underlying curve,
    ///   principals, correlation, attachment and detachment points.
    ///   <preliminary />
    /// </summary>
    /// 
    /// <remarks>
    ///   This is a heavy-weight function by which the basket size
    ///   can be modified.  It allows the array of input survival
    ///   curves to have a different length than the basket size
    ///   and checks if all the inputs are consistent with the new size.
    /// </remarks>
    /// 
    /// <param name="underlyings">The basket of underlyings.</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="attachment">Attachment</param>
    /// <param name="detachment">Detachment</param>
    /// 
    /// <returns>A new copy of cdo pricer</returns>
    public SyntheticCDOPricer Substitute(
      CreditPool underlyings,
      CorrelationObject correlation,
      double attachment, double detachment)
    {
      // sanity check
      if (attachment < 0.0)
        throw new System.ArgumentException(String.Format(
          "Attachment must be non-negative, not {0}",
          attachment));
      else if (attachment >= detachment)
        throw new System.ArgumentException(String.Format(
          "Attachment {0} must be less than detachment {1}",
          attachment, detachment));
      else if (detachment > 1.0)
        throw new System.ArgumentException(String.Format(
          "Detachment must be less than 1, not {0}",
          detachment));

      // Create a CDO with new attachment/detachment
      SyntheticCDO cdo = (SyntheticCDO)CDO.Clone();
      cdo.Detachment = detachment;
      cdo.Attachment = attachment;

      // Create a basket pricer with appropriate loss levels
      BasketPricer basket = Basket.Substitute(
        underlyings, correlation,
        new double[] { attachment, detachment });
      if (basket is BaseCorrelationBasketPricer)
      {
        ((BaseCorrelationBasketPricer)basket).Attachment = attachment;
        ((BaseCorrelationBasketPricer)basket).Detachment = detachment;
      }
      basket.Reset();

      //- Return a substititute CDO pricer
      return Substitute(cdo, basket, 0, true);
    }

    /// <summary>
    ///   Construct a pricer with optional synchronization.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If <paramref name="synchronize"/> is false, this function
    ///   assumes the basket and the product are perfectly synchronized and
    ///   it does not check the consistency between them.  This is a fast
    ///   way to reuse the distributions already computed in one basket to
    ///   price other compatible CDOs.</para>
    /// 
    ///   <para>For public use only.  Currently used in base correlation
    ///   calibrations and interpolations.</para>
    /// </remarks>
    /// 
    /// <param name="product">CDO product</param>
    /// <param name="basket">The basket model used to price the CDO</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="notional">tranche notional</param>
    /// <param name="synchronize">Synchronize the cdo and basket</param>
    ///
    public SyntheticCDOPricer(
      SyntheticCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double notional,
      bool synchronize)
      : base(product, basket.AsOf, basket.Settle)
    {
      if (basket == null)
        throw new System.NullReferenceException("Basket cannot be null");
      if (discountCurve == null)
        throw new System.NullReferenceException("Discount curve cannot be null");

      this.discountCurve_ = discountCurve;
      this.basket_ = basket;
      base.Notional = notional;
      if (synchronize)
      {
        changed_ = SyntheticCDOPricer.ResetFlag.All;
      }
      else
      {
        UpdateEffectiveNotional();
        changed_ = SyntheticCDOPricer.ResetFlag.None;
      }
      return;
    }

    /// <summary>
    ///   Contruct a pricer with optional synchronization.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If <paramref name="synchronize"/> is false, this function
    ///   assumes the basket and the product are perfectly synchronized and
    ///   it does not check the consistency between them.  This is a fast
    ///   way to reuse the distributions already computed in one basket to
    ///   price other compatible CDOs.</para>
    /// 
    ///   <para>For public use only.  Currently used in base correlation
    ///   calibrations and interpolations.</para>
    /// </remarks>
    /// 
    /// <param name="product">CDO product</param>
    /// <param name="basket">The basket model used to price the CDO</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="notional">tranche notional</param>
    /// <param name="synchronize">Synchronize the cdo and basket</param>
    ///
    public SyntheticCDOPricer(
      SyntheticCDO product,
      BasketPricer basket,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      double notional,
      bool synchronize)
      : base(product, basket.AsOf, basket.Settle)
    {
      if (basket == null)
        throw new System.NullReferenceException("Basket cannot be null");
      if (discountCurve == null)
        throw new System.NullReferenceException("Discount curve cannot be null");

      this.discountCurve_ = discountCurve;
      this.referenceCurve_ = referenceCurve;
      this.basket_ = basket;
      base.Notional = notional;
      if (synchronize)
      {
        changed_ = SyntheticCDOPricer.ResetFlag.All;
      }
      else
      {
        UpdateEffectiveNotional();
        changed_ = SyntheticCDOPricer.ResetFlag.None;
      }
      return;
    }

    /// <summary>
    ///   Contruct a pricer with optional synchronization.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If <paramref name="synchronize"/> is false, this function
    ///   assumes the basket and the product are perfectly synchronized and
    ///   it does not check the consistency between them.  This is a fast
    ///   way to reuse the distributions already computed in one basket to
    ///   price other compatible CDOs.</para>
    /// 
    ///   <para>For public use only.  Currently used in base correlation
    ///   calibrations and interpolations.</para>
    /// </remarks>
    /// 
    /// <param name="product">CDO product</param>
    /// <param name="basket">The basket model used to price the CDO</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="notional">tranche notional</param>
    /// <param name="synchronize">Synchronize the cdo and basket</param>
    /// <param name="rateResets">Rate Resets (for FRNs)</param>
    ///
    public SyntheticCDOPricer(
      SyntheticCDO product, BasketPricer basket, DiscountCurve discountCurve,
      double notional, bool synchronize, List<RateReset> rateResets
      )
      : base(product, basket.AsOf, basket.Settle)
    {
      if (basket == null)
        throw new System.NullReferenceException("Basket cannot be null");
      if (discountCurve == null)
        throw new System.NullReferenceException("Discount curve cannot be null");
#if TEST_FOR_FLOATING
      if (product.CdoType == CdoType.FundedFloating || product.CdoType == CdoType.IoFundedFloating)
        if (rateResets == null || rateResets.Count == 0)
          throw new System.ArgumentException("Rate Resets cannot be empty for FRNs");
#endif
      this.discountCurve_ = discountCurve;
      this.basket_ = basket;
      this.rateResets_ = rateResets;
      base.Notional = notional;
      if (synchronize)
      {
        changed_ = ResetFlag.All;
      }
      else
      {
        UpdateEffectiveNotional();
        changed_ = ResetFlag.None;
      }
      return;
    }


    /// <summary>
    ///   Construct a pricer with optional synchronization.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If <paramref name="synchronize"/> is false, this function
    ///   assumes the basket and the product are perfectly synchronized and
    ///   it does not check the consistency between them.  This is a fast
    ///   way to reuse the distributions already computed in one basket to
    ///   price other compatible CDOs.</para>
    /// 
    ///   <para>For public use only.  Currently used in base correlation
    ///   calibrations and interpolations.</para>
    /// </remarks>
    /// 
    /// <param name="product">CDO product</param>
    /// <param name="basket">The basket model used to price the CDO</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="notional">tranche notional</param>
    /// <param name="synchronize">Synchronize the cdo and basket</param>
    /// <param name="rateResets">Rate Resets (for FRNs)</param>
    ///
    public SyntheticCDOPricer(
      SyntheticCDO product, BasketPricer basket, DiscountCurve discountCurve, DiscountCurve referenceCurve,
      double notional, bool synchronize, List<RateReset> rateResets
      )
      : base(product, basket.AsOf, basket.Settle)
    {
      if (basket == null)
        throw new System.NullReferenceException("Basket cannot be null");
      if (discountCurve == null)
        throw new System.NullReferenceException("Discount curve cannot be null");
#if TEST_FOR_FLOATING
      if (product.CdoType == CdoType.FundedFloating || product.CdoType == CdoType.IoFundedFloating)
        if (rateResets == null || rateResets.Count == 0)
          throw new System.ArgumentException("Rate Resets cannot be empty for FRNs");
#endif
      this.discountCurve_ = discountCurve;
      this.referenceCurve_ = referenceCurve;
      this.basket_ = basket;
      this.rateResets_ = rateResets;
      base.Notional = notional;
      if (synchronize)
      {
        changed_ = ResetFlag.All;
      }
      else
      {
        UpdateEffectiveNotional();
        changed_ = ResetFlag.None;
      }
      return;
    }
    #endregion Constructors

    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, discountCurve_);
        }
        return paymentPricer_;
      }
    }

    #region ForwardValues

    /// <summary>
    ///   Up-front fee on forward date
    /// </summary>
    ///
    /// <param name="forwardSettle">forward settle date</param>
    /// 
    /// <returns>Up-front fee</returns>
    ///
    public double UpFrontFeePv(Dt forwardSettle)
    {
      SyntheticCDO cdo = CDO;
      if (cdo.Fee != 0.0 && Dt.Cmp(cdo.FeeSettle, forwardSettle) > 0)
      {
        if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating ||
            cdo.CdoType == CdoType.IoFundedFloating || cdo.CdoType == CdoType.IoFundedFixed ||
            cdo.CdoType == CdoType.Po)
        {
          return -cdo.Fee * EffectiveNotional;
        }
        else
        {
          return cdo.Fee * EffectiveNotional;
        }
      }
      else
        return 0.0;
    }

    /// <summary>
    ///   Fee pv (full) of CDO fee leg on forward date
    /// </summary>
    ///
    /// <remarks>This function is the same as FullFeePv(), which includes up-front fee.</remarks>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// 
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FeePv(Dt forwardDate)
    {
      SyntheticCDO cdo = CDO;
      return FeePv(forwardDate, cdo.Premium) + UpFrontFeePv(forwardDate);
    }

    /// <summary>
    ///   Calculate fee pv (full) of CDO fee leg.
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee.</remarks>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// 
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FullFeePv(Dt forwardDate)
    {
      SyntheticCDO cdo = CDO;
      return FeePv(forwardDate, cdo.Premium) + UpFrontFeePv(forwardDate);
    }


    /// <summary>
    ///   Calculate pv (full) of the CDO.
    /// </summary>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// 
    /// <returns>the PV of the CDO</returns>
    ///
    public double Pv(Dt forwardDate)
    {
      return ProtectionPv(forwardDate) + FeePv(forwardDate);
    }


    /// <summary>
    ///   Calculate flat (clean) price of the CDO.
    /// </summary>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// 
    /// <returns>the PV of the CDO</returns>
    ///
    public double FlatPrice(Dt forwardDate)
    {
      return ProtectionPv(forwardDate) + FlatFeePv(forwardDate);
    }


    /// <summary>
    ///   Calculate full (dirty) price of the CDO.
    /// </summary>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// 
    /// <returns>the PV of the CDO</returns>
    ///
    public double FullPrice(Dt forwardDate)
    {
      return ProtectionPv(forwardDate) + FeePv(forwardDate);
    }

    /// <summary>
    ///   Accrued on forward settle date
    /// </summary>
    /// 
    /// <remarks>
    ///   The relevant accrued to the names defaulted before the settle date
    ///   is included.
    /// </remarks>
    ///
    /// <param name="forwardDate">Forward settlement date</param>
    ///
    /// <returns>Accrued to settlement for CDO</returns>
    ///
    public double
    Accrued(Dt forwardDate)
    {
      return (Accrued(forwardDate, CDO.Premium) * Notional);
    }

    /// <summary>
    ///   Calculate the break even premium of the CDO.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">BreakEvenPremium = ProtectionLegPv 
    /// / { Duration * Notional }</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// <param name="convertCDOType">Should cdo type be changed to funded or not 
    /// (bc level/skew delta will not change type)</param>
    /// <returns>the break even premium</returns>
    ///
    public double BreakEvenPremium(Dt forwardDate, bool convertCDOType)
    {
      return BreakEvenPremiumSolver.Solve(this, forwardDate, forwardDate, convertCDOType);
    }
    /// <summary>
    ///   Calculate the break even premium of the CDO.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">BreakEvenPremium = ProtectionLegPv / { Duration * Notional }</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// <returns>the break even premium</returns>
    ///
    public double BreakEvenPremium(Dt forwardDate)
    {
      return BreakEvenPremiumSolver.Solve(this, forwardDate, forwardDate, true);
    }

    /// <summary>
    ///   Calculate the break even fee of the CDO.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even fee is the fee which would imply a
    ///   zero MTM value.</para>
    /// </remarks>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// <param name="convertCDOType">Should cdo type be changed to funded or not (bc level/skew delta will not change type)</param>
    /// <returns>the break even fee</returns>
    ///
    public double BreakEvenFee(Dt forwardDate, bool convertCDOType)
    {
      return BreakEvenFee(forwardDate, forwardDate, convertCDOType);
    }
    /// <summary>
    ///   Calculate the break even fee of the CDO.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even fee is the fee which would imply a
    ///   zero MTM value.</para>
    /// </remarks>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// <returns>the break even fee</returns>
    ///
    public double BreakEvenFee(Dt forwardDate)
    {
      return BreakEvenFee(forwardDate, forwardDate, true);
    }
    /// <summary>
    ///   Calculate the Premium 01 of the Synthetic CDO on a specified forward date
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the Premium01 of the tranche on the forward date,
    ///   covering the period from the <paramref name="forwardDate"/> to tranche
    ///   maturity and taking account of the expected default losses in the period 
    ///   from the pricer settle date to the forward date.</para>
    ///   <para>The Premium01 is the change in PV (MTM) for a Synthetic CDO
    ///   tranche if the premium is increased by one basis point.</para>
    ///   <para>The Premium 01 is calculated by calculating the PV (MTM) of the
    ///   Synthetic CDO tranche then bumping up the premium by one basis point
    ///   and re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    /// <param name="forwardDate">forward settle date</param>
    /// <returns>Premium 01 of the Synthetic CDO tranche</returns>
    public double Premium01(Dt forwardDate)
    {
      SyntheticCDO cdo = CDO;
      return (FeePv(forwardDate, cdo.Premium + 0.0001) 
        - FeePv(forwardDate, cdo.Premium));
    }

    /// <summary>
    ///   Calculate the forward pv of CDO protection leg.
    /// </summary>
    /// <remarks>The pv is discounted back to the forward date only.</remarks>
    /// <param name="forwardDate">The date at which the pv is calculated</param>
    /// <returns>the PV of the protection leg in this setting</returns>
    public double ProtectionPv(Dt forwardDate)
    {
      return _usePaymentSchedule
        ? PsProtectionPv(forwardDate)
        : CfProtectionPv(forwardDate);
    }

    private double PsProtectionPv(Dt forwardDate)
    {
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      BasketPricer basket = Basket; // this also update the public state

      Dt maturity = cdo.Maturity;

      // Funded notes(including Po's) don't have a protection leg 
      // (the bullet payment to the protection
      // seller at maturity is added to the fee leg). 
      // Io's also have no protectionleg but there's no bullet
      // payment at maturity 
      if (cdo.CdoType == CdoType.FundedFixed 
        || cdo.CdoType == CdoType.FundedFloating
        || cdo.CdoType == CdoType.Po 
        || cdo.CdoType == CdoType.IoFundedFloating 
        || cdo.CdoType == CdoType.IoFundedFixed)
      {
        return 0.0;
      }

      // include unsettled default payment
      double pv = Basket.DefaultSettlementPv(t, maturity,
        discountCurve_, cdo.Attachment, cdo.Detachment, true, false);

      if (Dt.Cmp(GetProtectionStart(), maturity) <= 0)
      {
        // Note: this is a bit INCONSISTENCY with the CDS pricer
        // because the later rolls if the maturity date is on sunday.
        // This may cause several hundreds bucks differences in FeePv
       var cashflow = new CashflowAdapter(GeneratePsForProtection(t));
        pv += price(cashflow, t, discountCurve_,
          basket, cdo.Attachment, cdo.Detachment,
          NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
          false, true, false,
          this.DefaultTiming, this.AccruedFractionOnDefault,
          basket.StepSize, basket.StepUnit, cashflow.Count);
      }
      return pv*this.Notional;
    }

    /// <summary>
    ///   Calculate the forward pv of CDO fee leg.
    /// </summary>
    ///
    /// <param name="forwardDate">The date at which the pv is calculated</param>
    /// <param name="premium">Premium</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    public double FeePv(Dt forwardDate, double premium)
    {
      // Using Notional rather than CurrentNotional or 
      // EffectiveNotional becuase Accrued(...) handles the 
      // effects of default directly based on a unit 
      // notional for the original trade.
      return FlatFeePv(forwardDate, forwardDate, premium, true, true)
        + UnsettledDefaultAccrualAdjustment(forwardDate, premium)
        + Accrued(forwardDate, premium) * Notional;
    }

    /// <summary>
    ///   Calculate the forward pv of CDO fee leg base on effective notional only.
    ///   Excluding all the accrued to the names defaulted before the settle.
    /// </summary>
    ///
    /// <param name="forwardDate">The date at which the pv is calculated</param>
    /// <param name="premium">Premium</param>
    /// <returns>the PV of the fee leg in this setting</returns>
    private double FeePvNoAccruedAdjustment(Dt forwardDate, double premium)
    {
      return _usePaymentSchedule
        ? PsFeePvNoAccruedAdjustment(forwardDate, premium)
        : CfFeePvNoAccruedAdjustment(forwardDate, premium);
    }

    private double PsFeePvNoAccruedAdjustment(Dt forwardDate, double premium)
    {
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      if (Dt.Cmp(t, cdo.Maturity) > 0)
        return 0;

      double pv = 0.0;
      double bulletFundedPayment = 0.0;
      BasketPricer basket = Basket; // this alsoe update the public state

      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating
        || cdo.CdoType == CdoType.Po)
      {
        // Only the maturity distributions count
        double survivalPrincipal = cdo.Detachment - cdo.Attachment;
        survivalPrincipal -= basket.AccumulatedLoss(cdo.Maturity, cdo.Attachment, cdo.Detachment);
#       if EXCLUDE_AMORTIZE
        if (cdo.AmortizePremium)
          survivalPrincipal -= basket.AmortizedAmount(cdo.Maturity, cdo.Attachment, cdo.Detachment);
#       endif
        survivalPrincipal *= DiscountCurve.DiscountFactor(t, cdo.Maturity);
        bulletFundedPayment = survivalPrincipal * TotalPrincipal;

        // include the unsettled recoveries
        bulletFundedPayment += Basket.DefaultSettlementPv(t, cdo.Maturity,
          DiscountCurve, cdo.Attachment, cdo.Detachment, false, true) * Notional;
      }

      if (cdo.CdoType == CdoType.Po)
      {
        // no running fee for zero coupon (PO tranche). 
        // Only bullet payment (remaining notional) at maturity
        return bulletFundedPayment;
      }

      double trancheWidth = cdo.Detachment - cdo.Attachment;
      if (cdo.FeeGuaranteed)
      {
        var cycleRule = cdo.CycleRule;

        CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;
        if (cdo.AccruedOnDefault)
          flags |= CashflowFlag.AccruedPaidOnDefault;

        var schedParams = new ScheduleParams(cdo.Effective, cdo.FirstPrem,
          cdo.LastPrem, cdo.Maturity, cdo.Freq,
          cdo.BDConvention, cdo.Calendar, cycleRule, flags);

        var schedule = Schedule.CreateScheduleForCashflowFactory(schedParams);
        const double fee = 0.0;
        Dt feeSettle = Dt.Empty;
        const double principal = 0.0;

        Currency defaultCcy = cdo.Ccy;
        Dt defaultDate = Dt.Empty;

        // Use hazard rate constructor with 0 hazard to get riskfree survival curve.
        SurvivalCurve riskfree = new SurvivalCurve(basket.AsOf, 0.0);
        var ps = new PaymentSchedule();
        flags |= CashflowFlag.AccrueOnCycle;
        ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(basket.AsOf,
          Dt.Empty, schedule, cdo.Ccy, cdo.DayCount,
          new PaymentGenerationFlag(flags, false, false), premium, 
          EmptyArray<CouponPeriod>.Instance,
          principal, null, riskfree, DiscountCurve, null, true));

        //the above payments don't include the recoverypayments, so we can 
        //directly apply the payment schedule to calculate feepv
        pv = ps.CalculatePv(t, t, DiscountCurve, riskfree, null, 0.0, basket.StepSize,
          basket.StepUnit, AdapterUtil.CreateFlags(false, false, false));
        pv *= trancheWidth;
        //add bullet payment at maturity if funded note
        return pv * this.TotalPrincipal + bulletFundedPayment;
      }
      else
      {
        // check if Libor flag is turned on
        var cashflow = new CashflowAdapter(GeneratePsForFee(t, premium));

        int stepSize = 0;
        TimeUnit stepUnit = TimeUnit.None;
        pv = price(cashflow, t, discountCurve_,
          basket, cdo.Attachment, cdo.Detachment,
          NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
          true, false, false,
          this.DefaultTiming, this.AccruedFractionOnDefault,
          stepSize, stepUnit, cashflow.Count);
        pv *= this.Notional;
        // add bullet payment at maturity if funded note
        return pv + bulletFundedPayment;
      }
    }

    private double UnsettledDefaultAccrualAdjustment(Dt forwardDate, double premium)
    {
      return _usePaymentSchedule
        ? PsUnsettledDefaultAccrualAdjustment(forwardDate, premium)
        : CfUnsettledDefaultAccrualAdjustment(forwardDate, premium);
    }

    private double PsUnsettledDefaultAccrualAdjustment(Dt forwardDate, double premium)
    {
      if (!paysAccrualAfterDefault_) { return 0; }

      Dt settle = forwardDate;
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0.0;
      }

      // If the user supplied a cash flow, we simply use it.
      // Otherwise, we generate a cahs flow, which has taken care of using the
      // correct floating coupons when necessary.
      var cf = new CashflowAdapter(GeneratePsForFee(settle, premium));

      // Find first cash flow on or after settlement (depending on includeSettle flag)
      int N = cf.Count;
      int firstIdx;
      for (firstIdx = 0; firstIdx < N; firstIdx++)
      {
        if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
          break;
      }
      if (firstIdx >= N)
        return 0.0; // This may happen when the forward date is after maturity, for example.

      //TODO: revisit and consider using start and end dates.
      Dt accrualStart = (firstIdx > 0) ? cf.GetStartDt(firstIdx - 1) : cf.Effective;
      if (Dt.Cmp(accrualStart, settle) > 0)
        return 0.0; // this may occur for forward starting CDO

      Dt accrualEnd = cf.GetEndDt(firstIdx), payDate = cf.GetDt(firstIdx);
      double paymentPeriod = Dt.Fraction(accrualStart, accrualEnd, cdo.DayCount);
      if (paymentPeriod < 1E-10)
        return 0.0; // this may happen if maturity on settle, for example

      return Basket.UnsettledDefaultAccrualAdjustment(
        payDate, accrualStart, accrualEnd, cdo.DayCount,
        CDO.Attachment, CDO.Detachment, DiscountCurve)
        *premium*Notional/DiscountCurve.DiscountFactor(forwardDate);
    }

    /// <summary>
    ///   Calculate pv (clean) of CDO fee leg.
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee and any bonus payment.</remarks>
    ///
    /// <param name="forwardDate">forward settle date</param>
    /// 
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FlatFeePv(Dt forwardDate)
    {
      return CDO != null ? FlatFeePv(forwardDate, CDO.Premium) : 0.0;
    }

    /// <summary>
    ///   Fee pv (clean) of CDO fee leg on forward date.
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee and bonus payment</remarks>
    ///
    /// <param name="forwardDate">Forward settle date</param>
    /// <param name="premium">Premium</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    public double FlatFeePv(Dt forwardDate, double premium)
    {
      return FlatFeePv(forwardDate, forwardDate, premium, true, true);
    }

    /// <summary>
    ///   Fee pv (clean) of CDO fee leg on forward date, given a premium
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee and bonus payment if requested</remarks>
    ///
    /// <param name="forwardAsOf">Forward as-of date</param>
    /// <param name="forwardSettle">Forward settle date</param>
    /// <param name="premium">Premium</param>
    /// <param name="includeUpFront">True to include UpFront Fee in the FlatFeePV calculation</param>
    /// <param name="includeBonus">True to include the bonus payment in the FlatFeePV calculation</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    public double FlatFeePv(Dt forwardAsOf, Dt forwardSettle, double premium,
      bool includeUpFront, bool includeBonus)
    {
      // Get the raw fee pv (no upfront or bonus, includes accrued)
      double pv = FeePvNoAccruedAdjustment(forwardSettle, premium);
      // Adjust the value by discounting back to the forward as-of date
      pv *= DiscountCurve.DiscountFactor(forwardAsOf, forwardSettle);

      // Add-on any requested "extras"
      if (includeUpFront)
        pv += UpFrontFeePv(forwardSettle);
      if (includeBonus)
      {
        if (this.CDO.Bullet != null && this.CDO.Bullet.CouponRate > 0)
          pv += BulletCouponUtil.GetDiscountedBonus(this);
      }
      // Clean off the accrued
      pv -= AccruedPerUnitNotional(forwardSettle, premium) * CurrentNotional;
      return pv;
    }

    /// <summary>
    ///   Calculate accrual days for CDO tranche through the settle date on the pricer
    /// </summary>
    ///
    /// <returns>Accrual days to settlement for CDO</returns>
    ///
    public int AccrualDays()
    {
      return AccrualDays(Settle);
    }


    /// <summary>
    ///   Calculate accrual days for CDO tranche through an arbitrary settle date
    /// </summary>
    ///
    /// <returns>Accrual days to settlement for CDO</returns>
    ///
    public int AccrualDays(Dt settle)
    {
      return _usePaymentSchedule ? PsAccrualDays(settle) : CfAccrualDays(settle);
    }

    private int PsAccrualDays(Dt settle)
    {
      // Leverages code from Accrued(date, premium) method, 
      // simply calls FractionDays(...) instead of Fraction(...)
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0;
      }
      else
      {
        // If the user suppiled a cashflow, we simply use it.
        // Otherwise, we generate a cahsflow, which has taken care of using the
        // correct floating coupons when neccessary.
        var cf = new CashflowAdapter(GeneratePsForFee(settle, 1.0));

        // Find first cashflow on or after settlement
        // (depending on includeSettle flag)
        int N = cf.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
            break;
        }
        if (firstIdx >= N)
          return 0;  // This may happen when the forward date is after maturity, for example.

        //TODO: revisit and consider using start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0; // this may occur for forward starting CDO

        // Dt.Diff(...) is the numerator for *most* daycounts.  OneOne and Monthly might be off here; it's too complex to worry about now.
        return Dt.Diff(accrualStart, settle);
      }
      // end of the AccrualDays
    }



    /// <summary>
    ///   Calculate accrued for CDO tranche as percentage of original notional
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>For a funded floating cdo, the <paramref name="premium"/> is the spread,
    ///   otherwise, it is a full coupon.</para>
    /// 
    ///   <para>This function counts in all the relevant accrued to the names defaulted
    ///    before the settle date</para>
    /// </remarks>
    ///
    /// <param name="forwardDate">Forward settlement date</param>
    /// <param name="premium">Premium or spread to accrue</param>
    ///
    /// <returns>Accrued to settlement for CDO, normalized by tranche notional</returns>
    ///
    public double Accrued(Dt forwardDate, double premium)
    {
      return _usePaymentSchedule
        ? PsAccrued(forwardDate, premium)
        : CfAccrued(forwardDate, premium);
    }

    private double PsAccrued(Dt forwardDate, double premium)
    {
      Dt settle = forwardDate;
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0.0;
      }
      else
      {
        // If the user suppiled a cashflow, we simply use it.
        // Otherwise, we generate a cahsflow, which has taken care of using the
        // correct floating coupons when neccessary.
        var cf = new CashflowAdapter(GeneratePsForFee(settle, premium));

        // Find first cashflow on or after settlement (depending on includeSettle flag)
        int N = cf.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
            break;
        }
        if (firstIdx >= N)
          return 0.0;  // This may happen when the forward date is after maturity, for example.

        //TODO: revisit and consider using start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0.0; // this may occur for forward starting CDO

        Dt nextDate = cf.GetDt(firstIdx);
        double paymentPeriod = Dt.Fraction(accrualStart, nextDate, cdo.DayCount);
        if (paymentPeriod < 1E-10)
          return 0.0; // this may happen if maturity on settle, for example

        double accrued = (UseOriginalNotionalForFee ? Dt.Fraction(accrualStart,
          settle, cdo.DayCount) : Basket.AccrualFraction(accrualStart,
          settle, cdo.DayCount, CDO.Attachment, CDO.Detachment))
          / paymentPeriod * cf.GetAccrued(firstIdx);
        return accrued;
      }
      // end of the Accrued
    }



    /// <summary>
    ///   Calculate accrued for CDO tranche per unit of effective notional
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>For a funded floating cdo, the <paramref name="premium"/> is the spread,
    ///   otherwise, it is a full coupon.</para>
    /// 
    ///   <para>The accrued is calculated based on the ssumption that no name default
    ///   before settle.</para>
    /// </remarks>
    ///
    /// <param name="forwardDate">Forward settlement date</param>
    /// <param name="premium">Premium or spread to accrue</param>
    ///
    /// <returns>Accrued to settlement for CDO, normalized by tranche notional</returns>
    ///
    private double AccruedPerUnitNotional(Dt forwardDate, double premium)
    {
      return _usePaymentSchedule
        ? PsAccruedPerUnitNotional(forwardDate, premium)
        : CfAccruedPerUnitNotional(forwardDate, premium);
    }


    private double PsAccruedPerUnitNotional(Dt forwardDate, double premium)
    {
      Dt settle = forwardDate;
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0.0;
      }
      else
      {
        if (IgnoreAccruedSetting)
        {
          // Oldway and wrong way
          Schedule sched = new Schedule(settle, cdo.Effective, 
            cdo.FirstPrem, cdo.Maturity, cdo.Maturity,
            cdo.Freq, cdo.BDConvention, cdo.Calendar, false, true);

          // Calculate accrued to settlement.
          Dt start = sched.GetPeriodStart(0);
          Dt end = sched.GetPeriodEnd(0);
          // Note schedule currently includes last date in schedule period. This may get changed in
          // the future so to handle this we test if we are on a coupon date. RTD. Jan05
          if (Dt.Cmp(settle, start) == 0 || Dt.Cmp(settle, end) == 0)
            return 0.0;
          else
            return (Dt.Fraction(start, settle, cdo.DayCount) * premium);
        }
        else
        {
          // If the user suppiled a cashflow, we simply use it.
          // Otherwise, we generate a cahsflow, which has taken care of using the
          // correct floating coupons when neccessary.
          var cf = new CashflowAdapter(GeneratePsForFee(settle, premium));

          // Find first cashflow on or after settlement (depending on includeSettle flag)
          int N = cf.Count;
          int firstIdx;
          for (firstIdx = 0; firstIdx < N; firstIdx++)
          {
            if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
              break;
          }
          if (firstIdx >= N)
            return 0.0;  // This may happen when the forward date is after maturity, for example.

          //TODO: revisit and consider using start and end dates.
          Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
          if (Dt.Cmp(accrualStart, settle) > 0)
            return 0.0; // this may occur for forward starting CDO

          Dt nextDate = cf.GetDt(firstIdx);
          double paymentPeriod = Dt.Fraction(accrualStart, nextDate, cdo.DayCount);
          if (paymentPeriod < 1E-10)
            return 0.0; // this may happen if maturity on settle, for example

          double accrued = Dt.Fraction(accrualStart, settle, cdo.DayCount)
            / paymentPeriod * cf.GetAccrued(firstIdx);
          return accrued;
        }
      }
      // end of the Accrued
    }

    /// <summary>
    ///   Risky duration on forward date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The risky duration is defined as the the fee pv with the running premium
    ///   to be one and the tranche notional normalized to one and without upfront fee.</para>
    /// </remarks>
    /// 
    /// <param name="forwardDate">Forward date</param>
    ///
    /// <returns>risky duration</returns>
    ///
    public double RiskyDuration(Dt forwardDate)
    {
      return RiskyDuration(forwardDate, forwardDate);
    }

    /// <summary>
    ///   Risky duration on forward date, with discounting
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The risky duration is defined as the the fee pv with the running premium
    ///   to be one and the tranche notional normalized to one and without upfront fee.</para>
    /// </remarks>
    /// 
    /// <param name="forwardAsOf">Forward AsOf date</param>
    /// <param name="forwardSettle">Forward Settle date</param>
    ///
    /// <returns>risky duration</returns>
    ///
    public double RiskyDuration(Dt forwardAsOf, Dt forwardSettle)
    {
      if (Math.Abs(EffectiveNotional) <= 1E-7)
        return 0.0;

      double pv = 0, timeToMaturity;
      switch (this.CDO.CdoType)
      {
        case CdoType.Po:
          timeToMaturity = Dt.TimeInYears(forwardSettle, this.CDO.Maturity);
          pv = FlatFeePv(forwardAsOf, forwardSettle, CDO.Premium, false, false) * timeToMaturity;
          break;
        case CdoType.IoFundedFloating:
        case CdoType.FundedFloating:
        {
          var pricer = (SyntheticCDOPricer) MemberwiseClone();
          pricer.CDO = (SyntheticCDO) CDO.ShallowCopy();
          pricer.CDO.CdoType = CDO.CdoType == CdoType.IoFundedFloating
            ? CdoType.IoFundedFixed : CdoType.FundedFixed;
          pricer.cashflow_ = null;
          return pricer.RiskyDuration(forwardAsOf, forwardSettle);
        }
        case CdoType.FundedFixed:
          {
            // For funded notes the duration is defined to be the average of the duration for the fee (RD1)
            // the duration of the bullet payment (RD2).  I.e we want 0.5*(RD1+RD2), discounted to the asof.
            // calculate RD1
            double poFee = FlatFeePv(forwardSettle, forwardSettle, 0, false, false);
            double rDurationIoLeg = FlatFeePv(forwardSettle, forwardSettle, 1, false, false) - poFee;

            // calculate RD2 
            timeToMaturity = Dt.TimeInYears(forwardSettle, this.CDO.Maturity);
            double rDurationPoLeg = poFee * timeToMaturity;

            pv = 0.5 * (rDurationIoLeg + rDurationPoLeg) * DiscountCurve.DiscountFactor(forwardAsOf, forwardSettle);
          }
          break;
        default:
          // We only calculate the FlatFeePv settle to settle -- otherwise we discount twice to asOf
          pv = FlatFeePv(forwardSettle, forwardSettle, 1, false, false) 
            * DiscountCurve.DiscountFactor(forwardAsOf, forwardSettle); // based on clean price
          break;
      }

      return AdjustDurationForRemainingNotional ? pv / Notional : pv / EffectiveNotional;
    }

    #endregion // ForwardValues

    #region Methods

    /// <summary>
    ///   Calculate pv of CDO protection leg.
    /// </summary>
    ///
    /// <returns>the PV of the protection leg in this setting</returns>
    ///
    public double ProtectionPv()
    {
      BasketPricer basket = Basket;
      Dt settle = GetProtectionStart();
      double pv = ProtectionPv(settle) * discountCurve_.DiscountFactor(settle);

      // discount back to the as-of date
      return pv / DiscountCurve.DiscountFactor(basket.AsOf);
    }


    /// <summary>
    ///   Calculate pv (full) of CDO fee leg.
    /// </summary>
    ///
    /// <remarks>This function does not include up-front fee</remarks>
    ///
    /// <param name="premium">Premium</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    public double FeePv(double premium)
    {
      BasketPricer basket = Basket;
      Dt settle = GetProtectionStart();
      if (Dt.Cmp(settle, Basket.Maturity) > 0)
        return 0.0;
      return FlatFeePv(basket.AsOf, settle, premium, true, true) 
        + Accrued(settle, premium) * Notional;
    }



    /// <summary>
    ///   Calculate the pv of up-front fee
    /// </summary>
    ///
    /// <returns>pv fee</returns>
    ///
    public double UpFrontFeePv()
    {
      SyntheticCDO cdo = CDO;
      Dt settle = GetProtectionStart();
      if (Dt.Cmp(settle, cdo.Maturity) > 0)
        return 0.0;
      if (cdo.Fee != 0.0 && Dt.Cmp(cdo.FeeSettle, settle) > 0)
      {
        if (cdo.CdoType == CdoType.FundedFixed 
          || cdo.CdoType == CdoType.FundedFloating 
          || cdo.CdoType == CdoType.IoFundedFloating 
          || cdo.CdoType == CdoType.IoFundedFixed 
          || cdo.CdoType == CdoType.Po)
        {
          return -cdo.Fee * EffectiveNotional;
        }
        else
        {
          return cdo.Fee * EffectiveNotional;
        }
      }
      else
        return 0.0;
    }

    /// <summary>
    ///   Calculate fee pv (full) of CDO fee leg.
    /// </summary>
    ///
    /// <remarks>This function is the same as FullFeePv(), which includes up-front fee.</remarks>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FeePv()
    {
      SyntheticCDO cdo = CDO;
      return FeePv(cdo.Premium) + UpFrontFeePv();
    }

    /// <summary>
    ///   Calculate fee pv (full) of CDO fee leg.
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee.</remarks>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FullFeePv()
    {
      SyntheticCDO cdo = CDO;
      return FeePv(cdo.Premium) + UpFrontFeePv();
    }

    /// <summary>
    ///   Calculate pv (clean) of CDO fee leg.
    /// </summary>
    ///
    /// <remarks>This function includes up-front fee and any bonus payment.</remarks>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    ///
    public double FlatFeePv()
    {
      return CDO != null ? FlatFeePv(this.AsOf, this.Settle, CDO.Premium, true, true) : 0.0;
    }

    /// <summary>
    ///   Calculate pv (clean) of CDO fee leg.
    /// </summary>
    ///
    /// <remarks>This function does not include up-front fee, but does include any bonus payment</remarks>
    ///
    /// <param name="premium">Premium</param>
    ///
    /// <returns>the PV of the fee leg in this setting</returns>
    public double FlatFeePv(double premium)
    {
      return FlatFeePv(this.AsOf, this.Settle, premium, false, true);
    }

    /// <summary>
    ///   Calculate pv (full) of the CDO.
    /// </summary>
    ///
    /// <returns>the PV of the CDO</returns>
    ///
    public override double ProductPv()
    {
      return ProtectionPv() + FeePv();
    }


    /// <summary>
    ///   Calculate flat (clean) price of the CDO.
    /// </summary>
    ///
    /// <returns>the PV of the CDO</returns>
    ///
    public double FlatPrice()
    {
      return ProtectionPv() + FlatFeePv();
    }

    /// <summary>
    ///   Calculate full (dirty) price of the CDO.
    /// </summary>
    ///
    /// <returns>the PV of the CDO</returns>
    ///
    public double FullPrice()
    {
      return ProtectionPv() + FeePv();
    }

    /// <summary>
    ///   Calculate accrued for CDO tranche
    /// </summary>
    /// 
    /// <remarks>
    ///   The arelevant accrued to the names defaulted before the settle
    ///   are included.
    /// </remarks>
    ///
    /// <returns>Accrued to settlement for CDO Tranche</returns>
    ///
    public override double Accrued()
    {
      Dt settle = GetProtectionStart();
      return (Accrued(settle, CDO.Premium) * Notional);
    }

    /// <summary>
    ///   Calculate the break even premium of the CDO.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">BreakEvenPremium = ProtectionLegPv / { Duration * Notional }</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    ///
    /// <returns>the break even premium</returns>
    ///
    public double BreakEvenPremium(bool convertCDOType)
    {
      return BreakEvenPremiumSolver.Solve(this, AsOf, GetProtectionStart(), convertCDOType);
    }

    /// <summary>
    ///   Calculate the break even premium of the CDO.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">BreakEvenPremium = ProtectionLegPv / { Duration * Notional }</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    ///
    /// <returns>the break even premium</returns>
    ///
    public double BreakEvenPremium()
    {
      return BreakEvenPremiumSolver.Solve(this, AsOf, GetProtectionStart(), true);
    }

    /// <summary>
    ///   Calculate the break even fee of the CDO.
    /// </summary>
    ///
    /// <returns>the break even fee</returns>
    ///
    public double BreakEvenFee(bool convertCDOType)
    {
      return BreakEvenFee(AsOf, GetProtectionStart(), convertCDOType);
    }

    /// <summary>
    ///   Calculate the break even fee of the CDO.
    /// </summary>
    ///
    /// <returns>the break even fee</returns>
    ///
    public double BreakEvenFee()
    {
      return BreakEvenFee(AsOf, GetProtectionStart(), true);
    }

    /// <summary>
    ///   Calculate the Premium 01 of the CDO
    /// </summary>
    /// <remarks>
    ///   <para>The Premium01 is the change in PV (MTM) for a Synthetic CDO
    ///   tranche if the premium is increased by one basis point.</para>
    ///   <para>The Premium 01 is calculated by calculating the PV (MTM) of the
    ///   Synthetic CDO tranche then bumping up the premium by one basis point
    ///   and re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    /// <returns>Premium 01 of the Synthetic CDO tranche</returns>
    public double Premium01()
    {
      SyntheticCDO cdo = CDO;
      return (FeePv(cdo.Premium + 0.0001) - FeePv(cdo.Premium));
    }

    /// <summary>
    ///   Calculate the change in pv cause by change in tranche subordination
    /// </summary>
    /// <param name="ap">New attachment level</param>
    /// <param name="dp">New detachment level</param>
    /// <returns>Change in Value value</returns>
    public double Subordination01(double ap, double dp)
    {
      SyntheticCDO cdo = CDO;

      // Create a CDO with new attachment/detachment
      cdo = (SyntheticCDO)cdo.Clone();
      cdo.Detachment = dp;
      cdo.Attachment = ap;
      cdo.Validate();

      // Create a new pricer
      SyntheticCDOPricer bumpedPricer = new SyntheticCDOPricer(
        cdo, basket_.Duplicate(), discountCurve_, referenceCurve_, this.Notional, this.RateResets);

      // return the delta
      return bumpedPricer.ProductPv() - this.ProductPv();
    }

    /// <summary>
    ///   Calculate the expected cumulative loss up to a given date
    /// </summary>
    ///
    /// <remark>
    ///   This is simple cash flow and no discounting applied.
    /// </remark>
    public double LossToDate(Dt date)
    {
      SyntheticCDO cdo = CDO;
      BasketPricer basket = Basket;
      double loss = TotalPrincipal * basket.AccumulatedLoss(date, cdo.Attachment, cdo.Detachment);
      return loss;
    }


    /// <summary>
    ///   Return the expected prepayment to date between specified attachment and detachment points.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates the expected prepayment on a specified tranche slice of an underlying
    ///   basket of credit assets.It applies only to LCDOs (were prepayment risk is present)</para>
    /// </remarks>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="attachment">The attachment point of the tranche</param>
    /// <param name="detachment">The detachment point of the tranche</param>
    ///
    /// <returns>Expected prepayment amount on specifed tranche to specified date</returns>
    ///
    public double ExpectedPrepayment(
      Dt date,
      double attachment,
      double detachment)
    {
      double expectedPrepayment;
      double trancheWidth = detachment - attachment;
      // clone pricer without prepayment
      BasketPricer basketNoPrepayment = Basket.Duplicate();
      basketNoPrepayment.RefinanceCurves = null;
      basketNoPrepayment.Reset();

      double expectedLossAndPrepayment = Basket.AmortizedAmount(date, attachment, detachment);
      double expectedLoss = basketNoPrepayment.AmortizedAmount(date, attachment, detachment);
      expectedPrepayment = expectedLossAndPrepayment - expectedLoss;
      expectedPrepayment *= Notional / trancheWidth;

      return expectedPrepayment;
    }

    /// <summary>
    ///   Calculate the expected outstanding notional at a given date
    /// </summary>
    ///
    /// <remark>
    ///   This is simple cash flow and no discounting applied.
    /// </remark>
    public double NotionalToDate(Dt date)
    {
      SyntheticCDO cdo = CDO;
      return ExpectedBalance(date, Basket, cdo.Attachment, cdo.Detachment) * Notional;
    }

    /// <summary>
    ///   Calculate the expected cumulative fee payment up to a given date
    /// </summary>
    /// <remark>
    ///   This is simple cash flow, without discounting.  The value is
    ///   premium payments up to the latest payment date before the given date.
    ///   calculated based on the original notional minus the cumulative losses
    ///   and recovery amortization.
    ///   The accrual between the previous payment date and the given date is
    ///   included.
    /// </remark>
    public double FeeToDate(Dt date)
    {
      Dt settle = GetProtectionStart();
      SyntheticCDO cdo = CDO;

      // The PO tranche is the only cdo(type) that does not have fee payments            
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
        return 0;
      else
      {
        // Need the tranche width      
        double trancheWidth = cdo.Detachment - cdo.Attachment;
        // These are the payment dates
        BasketPricer basket = Basket;
        bool eomRule = (cdo.CycleRule == CycleRule.EOM);
        Schedule sched = new Schedule(settle, cdo.Effective, cdo.FirstPrem, cdo.Maturity, cdo.Maturity,
                                      cdo.Freq, cdo.BDConvention, cdo.Calendar, adjustNext_, eomRule);

        Dt lastPaymentDate = sched.GetPaymentDate(sched.Count - 1);
        Dt lastDateCounted = Dt.Add(lastPaymentDate, 1);
        // Add up the pv of all payments
        double fee = 0.0, prevPrincipal = trancheWidth;
        Dt previous = basket.AsOf; //sched.getPeriodStart(0);                
        int j = 0;

        do
        {
          Dt current = sched.GetPaymentDate(j);
          if (Dt.Cmp(current, date) > 0)
          {
            current = date;
          }

          if (cdo.FeeGuaranteed)
          {
            fee += trancheWidth * Dt.Fraction(previous /*sched.getPeriodStart(j)*/, current, cdo.DayCount);
          }
          else
          {
            double accumulatedLoss =
              basket.AccumulatedLoss(current, cdo.Attachment, cdo.Detachment);

            double amortizedAmount =
              basket.AmortizedAmount(current, cdo.Attachment, cdo.Detachment);

            double remainPrincipal = trancheWidth - accumulatedLoss - amortizedAmount;

            if (j == sched.Count - 1)
              fee += (remainPrincipal + prevPrincipal) / 2 * Dt.Fraction(previous, lastDateCounted, cdo.DayCount);
            else
              fee += (remainPrincipal + prevPrincipal) / 2 * Dt.Fraction(previous, current, cdo.DayCount);

            remainPrincipal = remainPrincipal + 0;
            prevPrincipal = prevPrincipal + 0;

            prevPrincipal = remainPrincipal;
          }
          j++;
          previous = current;
        }
        while (j < sched.Count && (Dt.Cmp(date, sched.GetPaymentDate(j)) >= 0));

        return fee * cdo.Premium * Notional / trancheWidth;
      }
    }

    /// <summary>
    ///   Calculate premium payments up to a given date
    /// </summary>
    /// <remark>
    ///   This is simple cash flow, without discounting.  The value is
    ///   premium payments up to the latest payment date before the given date.
    ///   calculated based on the original notional, assuming no default.
    ///   The accrual between the previous payment date and the given date is not
    ///   included.
    /// </remark>
    public double AccrualToDate(Dt date)
    {
      Dt settle = GetProtectionStart();
      SyntheticCDO cdo = CDO;
      BasketPricer basket = Basket;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0.0;
      }
      else
      {
        // These are the payment dates
        bool eomRule = (cdo.CycleRule == CycleRule.EOM);
        Schedule sched = new Schedule(settle, cdo.Effective, cdo.FirstPrem, cdo.Maturity, cdo.Maturity,
                                      cdo.Freq, cdo.BDConvention, cdo.Calendar, adjustNext_, eomRule);

        double accrual = 0.0;
        for (int i = 0; i < sched.Count && Dt.Cmp(sched.GetPaymentDate(i), date) < 0; i++)
        {
          accrual += sched.Fraction(i, cdo.DayCount) * cdo.Premium;
        }

        return accrual * Notional;
      }
    }

    /// <summary>
    ///   Calculate the risky duration of the synthetic CDO tranche
    /// </summary>
    /// <remarks>
    ///   <para>The risky duration is defined as the the fee pv with the running premium set
    ///   to be one and the tranche notional normalized to one and without upfront fee.</para>
    /// </remarks>
    /// <returns>risky duration</returns>
    public double RiskyDuration()
    {
      return Math.Abs(EffectiveNotional) < 1E-1 ? 0.0 : RiskyDuration(AsOf, GetProtectionStart());
    }

    /// <summary>
    ///   Calculate the carry of the synthetic CDO tranche
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The carry is daily income from premium and is simply
    ///   the premium divided by 360 times the notional.</para>
    /// </remarks>
    ///
    /// <returns>Carry of the CSO</returns>
    ///
    public double Carry()
    {
      return _usePaymentSchedule ? PsCarry() : CfCarry();
    }

    private double PsCarry()
    {
      SyntheticCDO cdo = CDO;

      if (cdo.CdoType == CdoType.FundedFloating || cdo.CdoType == CdoType.IoFundedFloating)
      {
        Dt settle = GetProtectionStart();
        if (settle >= cdo.Maturity)
          return 0.0;

        //- If the user suppiled a cashflow, we simply use it.
        //- Otherwise, we generate a cahsflow, which has taken care of using the
        //- correct floating coupons when neccessary.
        var cf = new CashflowAdapter(GeneratePsForFee(settle, cdo.Premium));

        //- Find the latest cashflow on or before settlement
        int N = cf.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
            break;
        }
        //TODO: revisit and consider using start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0.0; //- can this occur?

        Dt nextDate = cf.GetDt(firstIdx);
        double paymentPeriod = Dt.Fraction(accrualStart, nextDate, cdo.DayCount);
        if (paymentPeriod < 1E-10)
          return 0.0; // can this happen?

        //- find the effective coupon on the settle date
        double coupon = cf.GetAccrued(firstIdx) / paymentPeriod;
        return coupon / 360 * CurrentNotional;
      }

      return cdo.Premium / 360 * CurrentNotional;
    }

    /// <summary>
    ///   Calculate the MTM Carry of the Credit Default Swap
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The MTM carry is the daily income of the MTM level
    ///   of the credit default swap. It is the Break Even Premium
    ///   divided by 360 times the notional.</para>
    /// </remarks>
    /// 
    /// <returns>MTM Carry of CDO</returns>
    /// 
    public double MTMCarry()
    {
      return BreakEvenPremium() / 360 * CurrentNotional;
    }

    /// <summary>
    ///   Calculate expected loss
    /// </summary>
    ///
    /// <remarks>
    ///   The expected loss is expressed as dollar amount.
    /// </remarks>
    ///
    /// <returns>Expected loss at the maturity date</returns>
    ///
    public double ExpectedLoss()
    {
      if (Dt.Cmp(Settle, Maturity) >= 0)
        return 0.0;
      SyntheticCDO cdo = CDO;
      BasketPricer basket = Basket;
      double trancheWidth = cdo.Detachment - cdo.Attachment;
      if (trancheWidth <= 1E-7)
        return 0;
      double loss = basket.AccumulatedLoss(cdo.Maturity, cdo.Attachment, cdo.Detachment);
      loss *= Notional / trancheWidth;
      return loss;
    }

    /// <summary>
    ///   Calculate expected survival
    /// </summary>
    ///
    /// <remarks>
    ///   The expected survival is expressed as percentage of tranche notional (0.01 = 1%)
    /// </remarks>
    ///
    /// <returns>Expected survival at the maturity date</returns>
    ///
    public double ExpectedSurvival()
    {
      if (Dt.Cmp(Settle, Maturity) >= 0)
        return 1.0;
      SyntheticCDO cdo = CDO;
      return ExpectedSurvival(cdo.Maturity, this.Basket, cdo.Attachment, cdo.Detachment);
    }


    
    /// <summary>
    ///   Calculate the up-front value the synthetic CDO tranche
    /// </summary>
    /// <remarks>
    ///   <para>The up-front value is defined as the expected pv of the cashflow generated
    ///   by a fixed interest rate applying to the underlying (risky) tranche principal
    ///   as well as receiving the remaining principal on maturity date.</para>
    /// </remarks>
    /// <param name="r">The interest rate</param>
    /// <returns>Up-front value</returns>
    public double UpFrontValue(double r)
    {
      SyntheticCDO cdo = CDO;
      double trancheWidth = cdo.Detachment - cdo.Attachment;
      BasketPricer basket = Basket;
      double expectedLoss = basket.AccumulatedLoss(cdo.Maturity, cdo.Attachment, cdo.Detachment);
      double remainingPrincipal = (1 - expectedLoss / trancheWidth) * Notional;

      double dv = FlatFeePv(1);
      double pv = dv * r
        + (DiscountCurve.DiscountFactor(cdo.Maturity) * remainingPrincipal
        / DiscountCurve.DiscountFactor(basket.AsOf));

      return pv;
    }

    /// <summary>
    /// Calculate the implied tranche volatility
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="SyntheticCDOPricer.ImpliedVolatility(double, double, bool)" />
    /// <para>In this case a normal distribution is assumed and default accuracy tollerances are used.</para>
    /// </remarks>
    /// <returns>Implied volatility for tranche</returns>
    public double ImpliedVolatility()
    {
      return ImpliedVolatility(0, 0, true);
    }

    /// <summary>
    ///   Calculate the implied tranche volatility
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="SyntheticCDOPricer.ImpliedVolatility(double, double, bool)" />
    /// <para>In this case a normal distribution is assumed.</para>
    /// </remarks>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied volatility (or 0 for default)</param>
    /// <param name="toleranceX">The accuracy of implied volatility (or 0 for default)</param>	
    /// <returns>Implied volatility for Synthetic CDO tranche</returns>
    public double ImpliedVolatility(double toleranceF, double toleranceX)
    {
      return ImpliedVolatility(toleranceF, toleranceX, true);
    }

    /// <summary>
    /// Calculate the implied tranche volatility
    /// </summary>
    /// <remarks>
    ///   <para>Assume that the portfolio loss at the maturity date follows normal or log-normal
    ///   distribution, calculate the standard deviation which matches the expected loss on the tranche.
    ///   The relative accuracy is controlled by two parameters <c>toleranceF</c> and <c>toleranceX</c>.
    ///   The type of distribution is controlled by the parameter <c>useNormalDistribution</c>.
    ///   If the paremter is <c>true</c>, normal distribution is assumed; otherwise, log-normal 
    ///   distribution is assumed.</para>
    ///   <para>Formally, let <formula inline="true">X</formula> be a random variable representing
    ///   the loss on the whole portfolio.  Let <formula inline="true">A</formula> and 
    ///   <formula inline="true">D</formula> be tranche attachment and detachment, respectively.
    ///   Then the tranche loss is given by <formula inline="true">\min(\max(0,X-A),D-A)</formula>.
    ///   Since the mean of <formula inline="true">X</formula> is known (and equals to the
    ///   expected loss on the whole portfolio), this function tries to find a standard deviation
    ///   of <formula inline="true">X</formula> such that the following equality holds:</para>
    ///   <para><formula>E[\min(\max(0,X-A),D-A)] = \mathrm{Expected\;Tranche\;Loss}</formula></para>
    ///   <para>where the <c>Expected Tranche Loss</c> is calculated using our CDO pricer.</para>
    ///   <para>The implied volatility calculated in this way provides an alternative measure of the risk
    ///   of a tranche.  Under some circumstances, however, the actual distribution of portfolio
    ///   loss may be too skewed to be represented by normal or log-normal distributions and the
    ///   solution for the above equation may not exists for some tranches.  In these cases, NaN
    ///   is returned.</para>
    ///   <para>The relative accuracy is controlled by two parameters <c>toleranceF</c> and <c>toleranceX</c>.</para>
    /// </remarks>
    /// <param name="toleranceF">Relative accuracy of PV in calculation of implied volatility (or 0 for default)</param>
    /// <param name="toleranceX">The accuracy of implied volatility (or 0 for default)</param>	
    /// <param name="useNormalDistribution">Use normal distribution or log-normal distribution</param>	
    /// <returns>Implied volatility for Synthetic CDO tranche</returns>
    public double
    ImpliedVolatility(double toleranceF, double toleranceX, bool useNormalDistribution)
    {
      var calc = new TrancheVolatilityCalc(this) {UseNormalDistribution = useNormalDistribution};
      return calc.ImpliedVolatility(toleranceF, toleranceX);
    }

    /// <summary>
    ///   Calculate the value at risk of the synthetic CDO tranche
    /// </summary>
    /// <remarks>
    ///   <para>The VaR is calculated for the period from settlement date to the
    ///   date specified in parameters.  Given a confidence level <formula inline="true">c</formula>,
    ///   VaR is defined as the minimum value of <formula inline="true">x</formula> such that
    ///   <formula inline="true">\mathrm{Probability}(\mathrm{Loss} \leq x) \geq c</formula>.</para>
    /// </remarks>
    /// <param name="date">The end date of the period</param>
    /// <param name="confidence">The confidence level</param>
    /// <param name="gridSize">The grid size use to discretize loss distribution</param>
    /// <returns>Calculated VaR</returns>
    public double VaR(Dt date, double confidence, double gridSize)
    {
      SyntheticCDO cdo = CDO;
      double trancheWidth = cdo.Detachment - cdo.Attachment;
      BasketPricer basket = Basket;

      if (gridSize <= 1E-9)
        gridSize = trancheWidth / 100;

      int nBuckets = (int)(trancheWidth / gridSize) + 1;
      double[] lossLevels = new double[nBuckets + 1];
      for (int i = 0; i < nBuckets; ++i)
        lossLevels[i] = cdo.Attachment + i * gridSize;
      lossLevels[nBuckets] = cdo.Detachment;

      double[,] dist =
        basket.CalcLossDistribution(true, date, lossLevels);
      double pVaR = 1.0;
      for (int i = dist.GetLength(0) - 1; i >= 0; --i)
      {
        if (dist[i, 1] >= confidence)
          pVaR = dist[i, 0];
        else
          break;
      }

      if (pVaR <= cdo.Attachment)
        pVaR = 0.0;
      else
      {
        pVaR -= cdo.Attachment;
        if (pVaR >= trancheWidth)
          pVaR = 1.0;
        else
          pVaR /= trancheWidth;
      }

      return pVaR * Notional;
    }

    /// <summary>
    ///   Calculate <see cref="VaR(Dt, double, double)"/>
    ///   at maturity and 95% confidence level
    /// </summary>
    /// <returns>Value at risk</returns>
    public double VaR()
    {
      return VaR(this.Maturity, 0.95, 0.0);
    }

#if DEBUG
    /// <exclude />
    public string DumpCurvePoints()
    {
      SurvivalCurve[] sc = Basket.SurvivalCurves;
      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < sc.Length; i++)
      {
        sb.Append(String.Format("{0}\n", sc[i].Name));
        for (int j = 0; j < sc[i].Tenors.Count; j++)
        {
          sb.Append(String.Format("{0}: {1} : {2}\n", sc[i].Tenors[j].Name, ((BaseEntity.Toolkit.Products.CDS)sc[i].Tenors[j].Product).Premium, sc[i].Points[j].Value));
        }
      }
      return sb.ToString();
    }
#endif

    /// <summary>
    ///   Calculate the spread delta sensitivity
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="Sensitivities.Spread01(IPricer, string, double, double, bool[])" />
    ///   <para>Computes numerical spread sensitivities.</para>
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
    ///   <para>This funcion wraps <see cref="Sensitivities.Spread01(IPricer, string, double, double, bool[])">Sensitivities.Spread01</see>
    ///   and it is designed to give relatively accurate results in the worst scenario 
    ///   (for example, with correlation higher than 90%).  It is much slower than a direct
    ///   call to <see cref="Sensitivities.Spread01(IPricer, string, double, double, bool[])">Sensitivities.Spread01</see>.</para>
    /// </remarks>
    ///
    public double Spread01(string measure, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      int savedPoints = basket_.IntegrationPointsFirst;
      try
      {
        basket_.IntegrationPointsFirst =
          BasketPricerFactory.SafeQuadraturePointsForGreeks(
          savedPoints, basket_.Copula.CopulaType, false);

        return Sensitivities.Spread01(this, measure, upBump, downBump, rescaleStrikes);
      }
      finally
      {
        basket_.IntegrationPointsFirst = savedPoints;
      }
    }


    
    /// <summary>
    /// Calculate the spread delta sensitivity. New Version(adding BumpFlags).
    /// </summary>
    /// <param name="measure">Target measure to evaluate (FeePv, ProtectionPv, BreakEvenPremium, etc.)</param>
    /// <param name="upBump">size of up bump</param>
    /// <param name="downBump">size of down bump</param>
    /// <param name="bumpFlags">Bump flags (BumpInPlace, RemapCorrelations, BumpRelative, etc)</param>
    /// <returns></returns>
    public double Spread01(string measure, double upBump, double downBump, BumpFlags bumpFlags)
    {
      int savedPoints = basket_.IntegrationPointsFirst;
      try
      {
        basket_.IntegrationPointsFirst =
          BasketPricerFactory.SafeQuadraturePointsForGreeks(
          savedPoints, basket_.Copula.CopulaType, false);

        return Sensitivities2.Spread01(this, measure, upBump, downBump, bumpFlags);
      }
      finally
      {
        basket_.IntegrationPointsFirst = savedPoints;
      }
    }
    



    /// <summary>
    ///   Calculate the spread gamma sensitivity
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
    ///
    ///   <para>This funcion wraps <see cref="Sensitivities.SpreadGamma(IPricer, string, double, double, bool[])">Sensitivities.SpreadGamma</see>
    ///   and it is designed to give relatively accurate results in the worst scenario 
    ///   (for example, with correlation higher than 90%). It is much slower than a direct
    ///   call to <see cref="Sensitivities.SpreadGamma(IPricer, string, double, double, bool[])">Sensitivities.SpreadGamma</see>.</para>
    /// </remarks>
    ///
    public double SpreadGamma(string measure, double upBump,
      double downBump, BumpFlags bumpFlags)
    {
      return Sensitivities2.SpreadGamma(this, measure, upBump, downBump, bumpFlags);
    }
     

    /// <summary>
    /// Calculate the hedge notional
    /// </summary>
    /// <param name="measure">Target measure to evaluate (FeePv, ProtectionPv, BreakEvenPremium, etc.)</param>
    /// <param name="hedgeTenor">Hedge Tenor, such as "3Y", "5Y", etc.</param>
    /// <param name="upBump">size of up bump</param>
    /// <param name="downBump">size of down bump</param>
    /// <param name="bumFlags">Bump flags (BumpRelative, BumpInPlace, RemapCorrelations, etc.)</param>
    /// <returns></returns>
    public double SpreadHedge(string measure, string hedgeTenor, double upBump,
      double downBump, BumpFlags bumFlags)
    {
      return Sensitivities2.SpreadHedge(this, measure, hedgeTenor, upBump, downBump, bumFlags);
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   CDO to price
    /// </summary>
    public SyntheticCDO CDO
    {
      get { return (SyntheticCDO)base.Product; }
      set { base.Product = value; }
    }

    /// <summary>
    ///   Underlying portfolio basket
    /// </summary>
    public BasketPricer Basket
    {
      get
      {
        if (changed_ != SyntheticCDOPricer.ResetFlag.None)
          UpdateStates();
        return basket_;
      }
    }


    /// <summary>
    ///   As-of date
    /// </summary>
    override public Dt AsOf
    {
      get { return Basket.AsOf; }
      set
      {
        base.AsOf = basket_.AsOf = value;
        this.Reset(SyntheticCDOPricer.ResetFlag.AsOf);
      }
    }


    /// <summary>
    ///   Settlement date
    /// </summary>
    override public Dt Settle
    {
      get { return Basket.Settle; }
      set
      {
        base.Settle = basket_.Settle = value;
        this.Reset(SyntheticCDOPricer.ResetFlag.Settle);
      }
    }


    /// <summary>
    ///   Maturity date
    /// </summary>
    public Dt Maturity
    {
      get { return Basket.Maturity; }
      set
      {
        basket_.Maturity = Product.Maturity = value;
        this.Reset(SyntheticCDOPricer.ResetFlag.Maturity);
      }
    }


    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    public int StepSize
    {
      get { return Basket.StepSize; }
      set { basket_.StepSize = value; }
    }


    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    public TimeUnit StepUnit
    {
      get { return Basket.StepUnit; }
      set { basket_.StepUnit = value; }
    }


    /// <summary>
    ///   Correlation structure of the basket
    /// </summary>
    public CorrelationObject Correlation
    {
      get { return Basket.Correlation; }
      set { basket_.Correlation = value; }
    }


    /// <summary>
    ///   Copula structure
    /// </summary>
    public Copula Copula
    {
      get { return Basket.Copula; }
      set { basket_.Copula = value; }
    }


    /// <summary>
    ///  Survival curves from curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return Basket.SurvivalCurves; }
      set { basket_.SurvivalCurves = value; }
    }


    /// <summary>
    ///  Recovery curves from curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get { return Basket.RecoveryCurves; }
      set { basket_.RecoveryCurves = value; }
    }


    /// <summary>
    ///   Recovery rates from curves
    /// </summary>
    public double[] RecoveryRates
    {
      get { return basket_.RecoveryRates; }
    }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set
      {
        discountCurve_ = value;
      }
    }

    /// <summary>
    /// Reference Curve used for floating payments forecast
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set { referenceCurve_ = value; }
    }

    /// <summary>
    ///   All discount Curves used for pricing, include those used in basket
    /// </summary>
    public DiscountCurve[] DiscountCurves
    {
      
      get
      {
        if(allDiscountCurves_ == null || changed_ != ResetFlag.None)
        {
          Dictionary<string, DiscountCurve> dc = new Dictionary<string, DiscountCurve>();
          dc.Add(discountCurve_.Name, discountCurve_);
          BasketPricer basket = Basket;
          if (basket is BaseCorrelationBasketPricer)
          {
            DiscountCurve d = ((BaseCorrelationBasketPricer) basket).DiscountCurve;
            if((d != null) && !dc.ContainsValue(d) && !dc.ContainsKey(d.Name))
              dc.Add(d.Name, d);
          }
          if (SurvivalCurves != null)
          {
            foreach (SurvivalCurve sc in SurvivalCurves)
            {
              DiscountCurve d = sc.SurvivalCalibrator.DiscountCurve;
              if ((d != null) && !dc.ContainsValue(d) && !dc.ContainsKey(d.Name))
                dc.Add(d.Name, d);
            }
          }
          allDiscountCurves_ = new DiscountCurve[dc.Values.Count]; 
          dc.Values.CopyTo(allDiscountCurves_, 0);
        }
        return allDiscountCurves_;
      }
    }

    /// <summary>
    ///   Principal or face values for each name in the basket
    /// </summary>
    public double[] Principals
    {
      get { return Basket.Principals; }
      set { basket_.Principals = value; }
    }

    /// <summary>
    ///   Indicator for how accrued is handled under default.
    /// </summary>
    /// <remarks>
    ///   A value of zero indicates no accrued will be used and a value of one means the entire period's 
    ///   (in the time grid sense) accrued will be counted.  This is further controlled by the CDO property
    ///   <c>AccruedOnDefault</c>, which, if FALSE, means this value is ignored.  Default value is 0.5 and 
    ///   any value must be between zero and one.
    /// </remarks>
    public double AccruedFractionOnDefault
    {
      get { return accruedFractionOnDefault_; }
      set
      {
        accruedFractionOnDefault_ = value;
      }
    }

    /// <summary>
    ///   Indicator for when default is considered to occur within a time period (in the time grid sense)
    /// </summary>
    /// <remarks>
    ///   A value of zero indicates the beginning of the period is assumed and a value of one means 
    ///   the end of the period.  Default value is 0.5 and any value must be between zero and one.
    /// </remarks>
    public double DefaultTiming
    {
      get { return defaultTiming_; }
      set
      {
        defaultTiming_ = value;
      }
    }

    /// <summary>
    ///   Portfolio notional of the underlying basket
    /// </summary>
    public double TotalPrincipal
    {
      get { return Notional / CDO.TrancheWidth; }
    }

    /// <summary>
    ///   Counterparty survival curve
    /// </summary>
    /// <exclude />
    public SurvivalCurve CounterpartySurvivalCurve
    {
      get { return counterpartySurvivalCurve_; }
      set { counterpartySurvivalCurve_ = value; }
    }

    /// <summary>
    ///   Historical rate fixings
    /// </summary>
    public List<RateReset> RateResets
    {
      get
      {
        if (rateResets_ == null)
          rateResets_ = new List<RateReset>();
        return rateResets_;
      }
    }

    /// <summary>
    ///   Current floating rate
    /// </summary>
    public double CurrentRate
    {
      get { return RateResetUtil.ResetAt(rateResets_, AsOf); }
    }

    /// <summary>
    ///   Effective notional on the settlement date, including both
    ///   the names not defaulted and the names defaulted
    ///   but not settled.
    /// </summary>
    public override double EffectiveNotional
    {
      get
      {
        if (changed_ != SyntheticCDOPricer.ResetFlag.None)
          UpdateStates();
        return (effectiveSurvival_ + unsettleDefaults_) * Notional;
      }
    }

    /// <summary>
    ///   Current notional on the settlement date,
    ///   including only the names not defaulted.
    /// </summary>
    public override double CurrentNotional
    {
      get
      {
        if (changed_ != SyntheticCDOPricer.ResetFlag.None)
          UpdateStates();
        return effectiveSurvival_ * Notional;
      }
    }

    /// <summary>
    ///   The effective attachment after defaulted names are removed
    /// </summary>
    public double CurrentAttachment
    {
      get
      {
        if (changed_ != SyntheticCDOPricer.ResetFlag.None)
          UpdateStates();
        return effectiveAttachment_;
      }
    }

    /// <summary>
    ///   The effective detachment after defaulted names are removed
    /// </summary>
    public double CurrentDetachment
    {
      get
      {
        if (changed_ != SyntheticCDOPricer.ResetFlag.None)
          UpdateStates();
        return effectiveDetachment_;
      }
    }

    #endregion // Properties

    #region IAnalyticDerivativesProvider Members
    /// <summary>
    /// Derivatives of the full price
    /// </summary>
    /// <param name="retVal">retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param>
    public void PvDerivatives(double[] retVal)
    {
      double[] protPv = new double[retVal.Length];
      ProtectionPvDerivatives(protPv);
      FeePvDerivatives(retVal);
      for (int k = 0; k < retVal.Length; k++)
        retVal[k] += protPv[k];
      if (this.Basket is BaseCorrelationBasketPricer)
      {
        BaseCorrelationBasketPricer bp = (BaseCorrelationBasketPricer)this.Basket;
        if (bp.RescaleStrike)
        {
          double[] strikeDers = new double[retVal.Length];
          BaseCorrelationObject bc = bp.BaseCorrelation;
          bc.RescaleStrikeDerivatives(this, strikeDers);
          for (int i = 0; i < retVal.Length; i++)
            retVal[i] += strikeDers[i];
        }
      }
    }



    /// <summary>
    /// Derivatives of the expected value of the protection leg 
    /// </summary>
    /// <param name="retVal">
    /// retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param>
    public void ProtectionPvDerivatives(double[] retVal)
    {
      Dt settle = GetProtectionStart();
      if(_usePaymentSchedule)
        PsProtectionPvDerivatives(settle, retVal);
      else
        CfProtectionPvDerivatives(settle, retVal);
      double df = discountCurve_.DiscountFactor(this.Settle) / DiscountCurve.DiscountFactor(Basket.AsOf);
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] *= df;
    }


    /// <summary>
    /// Derivatives of the expected value of the protection leg 
    /// </summary>
    ///<param name="forwardDate">forward start date</param>
    /// <param name="retVal">
    /// retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param>
    public void PsProtectionPvDerivatives(Dt forwardDate, double[] retVal)
    {
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      BasketPricer basket = Basket; // this also update the public state
      Dt maturity = cdo.Maturity;
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = 0.0;

      // Funded notes(including Po's) don't have a protection leg (the bullet payment to the protection
      // seller at maturity is added to the fee leg). Io's also have no protectionleg but theere's no bullet
      // payment at maturity 
      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating
        || cdo.CdoType == CdoType.Po || cdo.CdoType == CdoType.IoFundedFloating || cdo.CdoType == CdoType.IoFundedFixed
        || Dt.Cmp(GetProtectionStart(), maturity) > 0)
      {
        return;
      }
      var cashflow = new CashflowAdapter(PriceCalc.GeneratePsForProtection(t, 
        CDO.Maturity, CDO.Ccy, CounterpartySurvivalCurve));
      greeks(cashflow, t, discountCurve_, basket, cdo.Attachment, cdo.Detachment, NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
      false, true, false, this.DefaultTiming, this.AccruedFractionOnDefault, basket.StepSize, basket.StepUnit, cashflow.Count, retVal);

    }

    /// <summary>
    /// Derivatives of the fee leg
    /// </summary>
    /// <param name="retVal">retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param>
    public void FeePvDerivatives(double[] retVal)
    {
      Dt settle = GetProtectionStart();
      if(_usePaymentSchedule)
        FeePvDerivatives(settle, CDO.Premium, retVal);
      else
      {
        CfFeePvDerivatives(settle, CDO.Premium, retVal);
      }
      double df = discountCurve_.DiscountFactor(this.Settle) / DiscountCurve.DiscountFactor(Basket.AsOf);
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] *= df;
    }

    /// <summary>
    /// Derivatives of the fee leg
    /// </summary>
    ///<param name="forwardDate">forward start date</param>
    ///<param name="premium">running premium</param>
    /// <param name="retVal">retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param>
    public void FeePvDerivatives(Dt forwardDate, double premium, double[] retVal)
    {
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = 0.0;
      //double[] survPrincipalDers = new double[retVal.Length];
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      if (Dt.Cmp(t, cdo.Maturity) > 0)
        return;
      BasketPricer basket = Basket; // this also updates the public state
      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating
        || cdo.CdoType == CdoType.Po)
      {
        basket.AccumulatedLossDerivatives(cdo.Maturity, cdo.Attachment, cdo.Detachment, retVal);
        for (int k = 0; k < retVal.Length; k++)
          retVal[k] = -retVal[k];
#      if EXCLUDE_AMORTIZE
                double[] amort = new double[retVal.Length];
                if (cdo.AmortizePremium)
                basket.AmortizedAmountDerivatives(cdo.Maturity, cdo.Attachment, cdo.Detachment, amort);
                for (int k = 0; k < retVal.Length; k++)
                    retVal[k] -= amort[k];
#       endif
        double mult = DiscountCurve.DiscountFactor(t, cdo.Maturity) / (this.CDO.Detachment - this.CDO.Attachment);
        for (int k = 0; k < retVal.Length; k++)
          retVal[k] *= mult;
      }
      if (cdo.CdoType == CdoType.Po || cdo.FeeGuaranteed)
        return;
      double trancheWidth = cdo.Detachment - cdo.Attachment;
      double[] temp = new double[retVal.Length];
      var cashflow =new CashflowAdapter(GeneratePsForFee(t, premium));
      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
      greeks(cashflow, t, discountCurve_, basket, cdo.Attachment, cdo.Detachment,
            NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
            true, false, false, this.DefaultTiming, this.AccruedFractionOnDefault,
            stepSize, stepUnit, cashflow.Count, temp);
      for (int k = 0; k < retVal.Length; k++)
        retVal[k] += temp[k];//*this.Notional;
    }



    /// <summary>
    /// True if pricer supports semi-analytic derivatives computation
    /// </summary>
    bool IAnalyticDerivativesProvider.HasAnalyticDerivatives
    {
      get
      {
        var p = Basket as IAnalyticDerivativesProvider;
        return p != null && p.HasAnalyticDerivatives;
      }
    }


    /// <summary>
    /// Calculates semi-analytic derivatives of the PV wrt ordinates of each underlying curve,
    /// value of default of each name, and recovery derivatives of each name
    /// </summary>
    /// <returns>IDerivativeCollection object</returns>
    IDerivativeCollection IAnalyticDerivativesProvider.GetDerivativesWrtOrdinates()
    {
      var p = Basket as IAnalyticDerivativesProvider;
      if (p != null)
      {
        int size = 0;
        foreach (SurvivalCurve curve in SurvivalCurves)
        {
          int k = curve.Count;
          size += k + k * (k + 1) / 2 + 2;
        }
        DerivativeCollection retVal = new DerivativeCollection(SurvivalCurves.Length);
        double[] ders = new double[size];
        this.PvDerivatives(ders);
        int index = 0;
        foreach (SurvivalCurve curve in SurvivalCurves)
        {
          int k = curve.Count;
          DerivativesWrtCurve der = new DerivativesWrtCurve(curve);
          der.Gradient = new double[k];
          der.Hessian = new double[k * (k + 1) / 2];
          for (int i = 0; i < k; i++)
          {
            der.Gradient[i] = ders[index];
            index++;
          }
          int kk = 0;
          for (int i = 0; i < k; i++)
          {
            for (int j = 0; j <= i; j++)
            {
              der.Hessian[kk] = ders[index];
              kk++;
              index++;
            }
          }
          der.Vod = ders[index];
          index++;
          der.RecoveryDelta = ders[index];
          index++;
          retVal.Add(der);
        }

        return retVal;
      }
      throw new ToolkitException("BasketPricer does not support semi-analytic derivatives computation");
    }

    #endregion

    #region Data

    private BasketPricer basket_;
    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    [Mutable]
    private DiscountCurve[] allDiscountCurves_;
    private double accruedFractionOnDefault_ = 0.5;
    private double defaultTiming_ = 0.5;
    private SurvivalCurve counterpartySurvivalCurve_ = null;
    private List<RateReset> rateResets_ = null;

    // transitional public state
    private ResetFlag changed_;

    // automatically computed intermediate values
    private double effectiveSurvival_;
    private double effectiveAttachment_;
    private double effectiveDetachment_;
    private double unsettleDefaults_;
    private bool adjustDurationForRemainingNotional_ =
      ToolkitConfigurator.Settings.SyntheticCDOPricer.AdjustDurationForRemainingNotional;
    private bool paysAccrualAfterDefault_ =
      ToolkitConfigurator.Settings.SyntheticCDOPricer.SupportAccrualRebateAfterDefault;

    private const bool _useCache = true;
    private bool _usePaymentSchedule = true;
    #endregion // Data

    #region Handle_Changes

    #region RsetFlags
    /// <summary>
    ///   Indicate the status of pricer attributes
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    ///   This is used by the methof SyntheticCDO.Reset().
    /// </remarks>
    /// <exclude />
    public enum ResetFlag : uint
    {
      /// <summary>
      ///   Nothing changed
      /// </summary>
      None = 0x0000,

      /// <summary>
      ///   Product effective date changed
      /// </summary>
      Effective = 0x0001,

      /// <summary>
      ///   Product maturity date changed
      /// </summary>
      Maturity = 0x0002,

      /// <summary>
      ///   Subordination (detachment and/or attachment) changed
      /// </summary>
      Subordination = 0x0004,

      /// <summary>
      ///   Recovery data changed
      /// </summary>
      Recovery = 0x0008,

      /// <summary>
      ///   All the product attributes changed
      /// </summary>
      Product = 0x00FF,

      /// <summary>
      ///   AsOf date changed
      /// </summary>
      AsOf = 0x0100,

      /// <summary>
      ///   Settle date changed
      /// </summary>
      Settle = 0x0200,

      /// <summary>
      ///   Survival curves need to update
      /// </summary>
      Survival = 0x0400,

      /// <summary>
      ///   Correlations need to update
      /// </summary>
      Correlation = 0x0800,

      /// <summary>
      ///   Loss distributions need to recompute
      /// </summary>
      Distribution = 0x8000,

      /// <summary>
      ///   All pricer attributes need update
      /// </summary>
      All = 0xFFFFFFFF,
    }
    #endregion // Flags

    /// <summary>
    ///   Clear the public state
    /// </summary>
    /// <remarks>
    ///   The CDO pricer remembers some public states in order to skip
    ///   redundant calculation steps.
    ///   This function tell the pricer to update all the public states
    ///   and therefore force it to recalculate all the steps.
    /// </remarks>
    public override void Reset()
    {
      // This resets everything
      changed_ = SyntheticCDOPricer.ResetFlag.All;
    }

    /// <summary>
    ///   Tell pricer that what attributes have changed.
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    ///   <para>The CDO pricer remembers some public states in order to skip
    ///   redundant calculation steps.
    ///   This function tell the pricer that what pricer attributes have
    ///   changed and therefore, give the pricer an opportunity to selectively
    ///   clear/update its public states.  When used with caution, this method
    ///   can be much more efficient than the method Reset() without argument,
    ///   since the later resets everything.</para>
    /// </remarks>
    /// <exclude />
    public void Reset(ResetFlag what)
    {
      changed_ |= what;
    }

    /// <summary>
    ///   Tell pricer that what attributes have changed.
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    ///   <para>The CDO pricer remembers some public states in order to skip
    ///   redundant calculation steps.
    ///   This function tell the pricer that what pricer attributes have
    ///   changed and therefore, give the pricer an opportunity to selectively
    ///   clear/update its public states.  When used with caution, this method
    ///   can be much more efficient than the method Reset() without argument,
    ///   since the later resets everything.</para>
    /// </remarks>
    /// <exclude />
    public override void Reset(ResetAction what)
    {
      if (what == PricerBase.ResetProduct)
        changed_ |= ResetFlag.Product;
      else if (what == PricerBase.ResetAsOf)
        changed_ |= ResetFlag.AsOf;
      else if (what == PricerBase.ResetSettle)
        changed_ |= ResetFlag.Settle;
      // ignore others
      return;
    }

    /// <summary>
    ///   Reset basket distribution.
    /// </summary>
    public void ResetBasket()
    {
      // This also trigger UpdateStates() if needed.
      Basket.Reset();
    }

    /// <summary>
    ///   Update the public state such that the CDO product and
    ///   the basket pricer are compatible.
    /// </summary>
    public void UpdateStates()
    {
      // In this function, must NOT access basket through property!!!
      BasketPricer basket = basket_;

      // check the fixed recovery
      SyntheticCDO cdo = CDO;
      if ((changed_ & SyntheticCDOPricer.ResetFlag.Recovery) == SyntheticCDOPricer.ResetFlag.Recovery)
      {
        // First we have to tell basket that the rcovery changed
        basket.Reset(SyntheticCDOPricer.ResetFlag.Recovery);

        // Second to check if we need to set up fixed recovery
        if (cdo.FixedRecovery)
        {
          double[] rates = cdo.FixedRecoveryRates;
          // Is rates set ?
          if (rates == null || rates.Length == 0)
          {
            throw new ToolkitException(String.Format("Please set the fixed recovery numbers"));
          }

          // we need a clone of the underlying basket
          if (basket == basket_ && !basket.IsUnique)
          {
            basket = basket_.Duplicate();
            basket.IsUnique = true;
          }
          // set new recovery
          var ob = basket.OriginalBasket;
          var rc = new RecoveryCurve[ob.SurvivalCurves.Length];
          for (int i = 0; i < rc.Length; ++i)
          {
            if (i < rates.Length && rates[i] >= 0)
              rc[i] = new RecoveryCurve(basket.AsOf, rates[i]);
            else if (rates.Length == 1 && rates[0] >= 0)
              rc[i] = new RecoveryCurve(basket.AsOf, rates[0]);
            else
            {
              //keep the original recovery curve
              if (ob.RecoveryCurves == null)
                rc[i] = ob.SurvivalCurves[i].SurvivalCalibrator.RecoveryCurve;
              else
                rc[i] = ob.RecoveryCurves[i];
            }
          }

          var builder = new CreditPool.Builder(basket.OriginalBasket);
          builder.RecoveryCurves = rc;
          // Reset basket
          basket.Reset(builder.CreditPool);
          //basket.RecoveryCurves = rc;
          basket.HasFixedRecovery = true;
        }
        else
          basket.HasFixedRecovery = false;

        // clear this bit
        changed_ &= ~SyntheticCDOPricer.ResetFlag.Recovery;
      }

      // check the maturity date
      if ((changed_ & SyntheticCDOPricer.ResetFlag.Maturity) == SyntheticCDOPricer.ResetFlag.Maturity)
      {
        if (Dt.Cmp(cdo.Maturity, basket.Maturity) > 0)
        {
          // Base correlation basket adjustment (maturity match)
          if (basket is BaseCorrelationBasketPricer)
          {
            if (basket == basket_ && !basket.IsUnique)
            {
              basket = basket.Duplicate(); // in case maturity match
              basket.IsUnique = true;
            }
            basket.Reset(SyntheticCDOPricer.ResetFlag.Correlation);
          }

          // TODO: Should we add to time grid?
          basket.Maturity = cdo.Maturity;

          // Tell basket to re-calculate distribution
          basket.Reset(SyntheticCDOPricer.ResetFlag.Distribution);
        }

        // clear this bit
        changed_ &= ~SyntheticCDOPricer.ResetFlag.Maturity;
      }

      // special treatment for base correlation pricer
      if ((changed_ & SyntheticCDOPricer.ResetFlag.Subordination) == SyntheticCDOPricer.ResetFlag.Subordination)
      {
        if (basket is BaseCorrelationBasketPricer)
        {
          BaseCorrelationBasketPricer bp =
            (BaseCorrelationBasketPricer)basket;

          // is subordination changed?
          if (Math.Abs(bp.Attachment - cdo.Attachment) > Double.Epsilon ||
              Math.Abs(bp.Detachment - cdo.Detachment) > Double.Epsilon)
          {
            if (basket_ == bp && !bp.IsUnique)
            {
              bp = (BaseCorrelationBasketPricer)bp.Duplicate();
              bp.IsUnique = true;
            }
            bp.Attachment = cdo.Attachment;
            bp.Detachment = cdo.Detachment;
            bp.Reset(SyntheticCDOPricer.ResetFlag.Correlation);
            basket = bp;
          }
        }
        else if (basket.IsUnique)
        {
          basket.RawLossLevels = new UniqueSequence<double>(cdo.Attachment, cdo.Detachment);
          basket.Reset(SyntheticCDOPricer.ResetFlag.Distribution);
        }
        else
        {
          // Add the loss level to basket
          if (basket.AddLossLevels(cdo.Attachment, cdo.Detachment))
            basket.Reset(SyntheticCDOPricer.ResetFlag.Distribution);
        }

        // clear this bit
        changed_ &= ~SyntheticCDOPricer.ResetFlag.Subordination;
      }

      // For anything else, we just pass them to basket
      if (changed_ != SyntheticCDOPricer.ResetFlag.None)
        basket.Reset(changed_);

      // Replace with new basket pricer
      basket_ = basket;

      // update the effective survival value
      if (UseOriginalNotionalForFee)
      {
        effectiveSurvival_ = 1.0;
        unsettleDefaults_ = 0.0;
      }
      else
      {
        effectiveSurvival_ = CalcEffectiveInitialBalance(cdo, basket);
        unsettleDefaults_ = CalcUnsettleDefaults(cdo, basket);
      }
      {
        effectiveAttachment_ = cdo.Attachment;
        effectiveDetachment_ = cdo.Detachment;
        double loss = 0;
        basket.AdjustTrancheLevels(false,
          ref effectiveAttachment_, ref effectiveDetachment_, ref loss);
      }

      // Mark the state to be updated
      changed_ = SyntheticCDOPricer.ResetFlag.None;
      return;
    }

    /// <summary>
    ///   For manually update the effective notional
    /// </summary>
    public void UpdateEffectiveNotional()
    {
      if (UseOriginalNotionalForFee)
      {
        effectiveSurvival_ = 1.0;
        unsettleDefaults_ = 0.0;
      }
      else
      {
        effectiveSurvival_ = CalcEffectiveInitialBalance(CDO, Basket);
        unsettleDefaults_ = CalcUnsettleDefaults(CDO, Basket);
      }
      {
        effectiveAttachment_ = CDO.Attachment;
        effectiveDetachment_ = CDO.Detachment;
        double loss = 0;
        Basket.AdjustTrancheLevels(false,
          ref effectiveAttachment_, ref effectiveDetachment_, ref loss);
      }
      return;
    }

    #endregion // Handle_Changes

    #region Helpers

    /// <summary>
    ///   Compute the effective initial balance
    /// </summary>
    /// <param name="cdo">CDO</param>
    /// <param name="basket">Basket</param>
    /// <returns>Effective initial balance</returns>
    private static double CalcEffectiveInitialBalance(
      SyntheticCDO cdo, BasketPricer basket)
    {
      double attachment = cdo.Attachment;
      double detachment = cdo.Detachment;
      if (detachment <= attachment)
        return 0;

      double initialBalance;
      if (Dt.Cmp(cdo.Effective, basket.Settle) > 0)
      {
        // This is a Forward CDO. calculate the expected balance at forward settle date
        initialBalance = ExpectedBalance(cdo.Effective, basket, attachment, detachment);
      }
      else if (Dt.Cmp(basket.Settle, cdo.Maturity) > 0)
      {
        // If settle is after maturity, effective notional is zero
        initialBalance = 0;
      }
      else
      {
        double tranchLoss = 0;
        basket.AdjustTrancheLevels(false, ref attachment, ref detachment, ref tranchLoss);
        initialBalance = (detachment - attachment) * basket.InitialBalance / cdo.TrancheWidth;
      }

      return initialBalance;
    }

    private static double CalcUnsettleDefaults(
      SyntheticCDO cdo, BasketPricer basket)
    {
      if (Dt.Cmp(basket.Settle, cdo.Maturity) > 0)
      {
        // If settle is after maturity, effective notional is zero
        return 0;
      }
      Dt settle = (Dt.Cmp(cdo.Effective, basket.Settle) > 0
        ? cdo.Effective : basket.Settle);
      double d = basket.DefaultSettlementPv(settle, basket.Maturity,
          null, cdo.Attachment, cdo.Detachment, false, true);
      d -= basket.DefaultSettlementPv(settle, basket.Maturity,
          null, cdo.Attachment, cdo.Detachment, true, false);
      return d;
    }

    /// <summary>
    ///   Expected survival
    /// </summary>
    /// <remarks>
    ///   The expected survival is defined as one minus the ratio of
    ///   the expected cumulative loss to the tranche notional.
    /// </remarks>
    private static double ExpectedSurvival(
      Dt current, BasketPricer basket, double attachment, double detachment)
    {
      double trancheWidth = Math.Max(0.0, detachment - attachment);
      if (trancheWidth < 1E-9)
        return 0;
      double loss = Math.Min(1.0, basket.AccumulatedLoss(current, attachment, detachment) / trancheWidth);
      return Math.Max(1 - loss, 0.0);
    }

    /// <summary>
    ///   Expected balance
    /// </summary>
    /// <remarks>
    ///   The expected survival is defined as expected survival minus the ratio of
    ///   the expected cumulative amortization to the tranche notional, where the
    ///   amortization is due to default recovery.
    /// </remarks>
    private static double ExpectedBalance(
      Dt current, BasketPricer basket, double attachment, double detachment)
    {
      double trancheWidth = Math.Max(0.0, detachment - attachment);
      if (trancheWidth < 1E-9)
        return 0;
      double loss = Math.Min(1.0,
        (basket.AccumulatedLoss(current, attachment, detachment)
         + basket.AmortizedAmount(current, attachment, detachment)) / trancheWidth);
      return Math.Max(1 - loss, 0.0);
    }

    /// <summary>
    ///   Calculate price based on a cashflow stream
    /// </summary>
    private double price(
      CashflowAdapter cashflow, Dt settle, DiscountCurve discountCurve,
      BasketPricer basket, double attachment, double detachment, bool withAmortize,
      SurvivalCurve counterpartyCurve,
      bool includeFees, bool includeProtection, bool includeSettle,
      double defaultTiming, double accruedFractionOnDefault,
      int step, TimeUnit stepUnit, int stopIdx)
    {
      // If settle is after maturity, simply return 0
      if (Dt.Cmp(settle, basket.Maturity) > 0)
        return 0;

      double trancheWidth = detachment - attachment;
      if (trancheWidth < 1E-9) return 0.0;

      return PriceCalc.Price(cashflow, settle, discountCurve,
        delegate(Dt date)
        {
          double loss = basket.AccumulatedLoss(date,
            attachment, detachment) / trancheWidth;
          return Math.Min(1.0, loss);
        },
        delegate(Dt date)
        {
          double loss = basket.AccumulatedLoss(date, attachment, detachment);
          if (withAmortize)
            loss += basket.AmortizedAmount(date, attachment, detachment);
          // For a CDOSquare pricer, we nned to adjust the maximum loss level
          double maxLoss = 1.0;
          if (basket is CDOSquaredBasketPricer)
          {
            double totalPrincipal = ((CDOSquaredBasketPricer)basket).TotalPrincipal;
            maxLoss = ((CDOSquaredBasketPricer)basket).CurrentTotalPrincipal(date) / totalPrincipal;
          }
          return Math.Max(0.0, maxLoss - loss / trancheWidth);
        },
        counterpartyCurve, includeFees, includeProtection, includeSettle,
        defaultTiming, accruedFractionOnDefault, step, stepUnit, stopIdx);
    }

    /// <summary>
    ///   Calculate price based on a cashflow stream
    /// </summary>
    private void greeks(
      CashflowAdapter cashflow, Dt settle, DiscountCurve discountCurve,
      BasketPricer basket, double attachment, double detachment, bool withAmortize,
      SurvivalCurve counterpartyCurve,
      bool includeFees, bool includeProtection, bool includeSettle,
      double defaultTiming, double accruedFractionOnDefault,
      int step, TimeUnit stepUnit, int stopIdx, double[] retVal)
    {
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = 0.0;
      // If settle is after maturity, simply return 0
      if (Dt.Cmp(settle, basket.Maturity) > 0)
        return;
      double trancheWidth = detachment - attachment;
      if (trancheWidth < 1E-9) return;
      PriceCalc.Greeks(cashflow, settle, discountCurve,
      delegate(Dt date, double[] res)
      {
        basket.AccumulatedLossDerivatives(date, attachment, detachment, res);
        for (int k = 0; k < res.Length; k++)
          res[k] /= trancheWidth;
      },
      delegate(Dt date, double[] res)
      {
        double[] temp = new double[res.Length];
        basket.AccumulatedLossDerivatives(date, attachment, detachment, res);
        if (withAmortize)
        {
          basket.AmortizedAmountDerivatives(date, attachment, detachment, temp);
        }
        for (int k = 0; k < res.Length; k++)
          res[k] = -(res[k] + temp[k]) / trancheWidth;
      }, counterpartyCurve, includeFees, includeProtection, includeSettle,
        defaultTiming, accruedFractionOnDefault, step, stepUnit, stopIdx, retVal);
    }

    public PaymentSchedule GeneratePsForProtection(Dt settle)
    {
      SyntheticCDO cdo = CDO;
      var counterpartyCurve = CounterpartySurvivalCurve;
      var maturity = PriceCalc.FindTerminateDate(counterpartyCurve, settle, cdo.Maturity);
      return PriceCalc.GeneratePsForProtection(settle, 
        maturity, cdo.Ccy, counterpartyCurve);
    }

    public PaymentSchedule GeneratePsForFee(Dt t, double coupon)
    {
      SyntheticCDO cdo = CDO;
      DiscountCurve referenceCurve = ReferenceCurve ?? DiscountCurve;
      return PriceCalc.GeneratePsForFee(t, coupon,
        cdo.Effective, cdo.FirstPrem, cdo.Maturity,
        cdo.Ccy, cdo.DayCount, cdo.Freq, cdo.BDConvention, cdo.Calendar,
        this.CounterpartySurvivalCurve,
        cdo.CdoType == CdoType.FundedFloating || cdo.CdoType == CdoType.IoFundedFloating,
        false/*funded*/, referenceCurve, this.RateResets, DiscountCurve);
    }

    /// <summary>
    ///   Get protection starting date
    /// </summary>
    /// <returns>protection start date</returns>
    public Dt GetProtectionStart()
    {
      Dt settle = this.Settle;
      Dt effective = CDO.Effective;
      if (Dt.Cmp(effective, settle) > 0)
        return effective;
      return settle;
    }

    /// <summary>
    ///   Check if need to calculate amortization
    /// </summary>
    /// <param name="cdo">CDO product</param>
    /// <param name="basket">Basket</param>
    /// <returns>True if need to calculate amortization; otherwise, False.</returns>
    public static bool NeedAmortization(SyntheticCDO cdo, BasketPricer basket)
    {
      double minAmorLevel = BasketPricerFactory.MinimumAmortizationLevel(cdo);
      double maxBasketAmortLevel = basket.MaximumAmortizationLevel();
      return maxBasketAmortLevel > minAmorLevel;
    }
    #endregion // Helpers

    #region Break Even Calculations
    class BreakEvenPremiumSolver : SolverFn
    {
      public static double Solve(SyntheticCDOPricer pricer, Dt asOf, Dt settle, bool convertCDOType)
      {
        // If cdo type is unfunded, no need to convert type
        if (pricer.CDO.CdoType == CdoType.Unfunded)
        {
          // SolveBepUnfunded considers bullet bonus
          return SolveBep(pricer, asOf, settle);
        }
        else // If cdo type is not unfunded
        {
          // If we require a type change:
          if (convertCDOType)
          {
            // Set the pricer's type to be unfunded
            pricer = (SyntheticCDOPricer)pricer.MemberwiseClone();
            pricer.basket_ = pricer.basket_.Duplicate();
            var cdo = (SyntheticCDO)pricer.CDO.ShallowCopy();
            cdo.CdoType = CdoType.Unfunded;
            pricer.CDO = cdo;
            pricer.Basket.Reset();
            double bep = SolveBep(pricer, asOf, settle);
            return bep;
          }
          else // we do not require a type change for base correlation level delta and skew delta
          {
            if (pricer.CDO.CdoType == CdoType.Po || Math.Abs(pricer.EffectiveNotional) <= 1E-8)
              return 0.0;
            else if (pricer.CDO.CdoType == CdoType.IoFundedFixed || pricer.CDO.CdoType == CdoType.IoFundedFloating)
              return 0.0;
            else
              return SolveBep(pricer, asOf, settle);
          }
        }
      }

      public static double SolveBep(SyntheticCDOPricer pricer, Dt asOf, Dt settle)
      {
        // Discount factor from settle to as-of
        double df = pricer.DiscountCurve.DiscountFactor(asOf, settle);
        double netProtection = pricer.ProtectionPv(settle) * df + pricer.UpFrontFeePv(settle);
        double fee01 = df * pricer.FlatFeePv(settle, settle, 1.0, false, false);
        var bullet = pricer.CDO.Bullet;

        try
        {
          // ignore bonus for BE
          pricer.CDO.Bullet = null;
          // Direct calculation. 
          if (pricer.CDO.CdoType == CdoType.Unfunded)
          {
            if (fee01 / pricer.Notional < 1e-9)
              return 0.0;
            return -netProtection / fee01;
          }
          else
          {
            // ProtectionPv = 0 means defaulted names consumes whole basket
            if (pricer.CDO.CdoType == CdoType.Unfunded && Math.Abs(pricer.ProtectionPv() / pricer.Notional) < 1e-9)
              return 0.0;

            double targetPrice = 0;
            if (pricer.CDO.CdoType == CdoType.FundedFloating || pricer.CDO.CdoType == CdoType.FundedFixed)
            {
              targetPrice = df * SyntheticCDOPricer.ExpectedBalance(settle, pricer.Basket,
                pricer.CDO.Attachment, pricer.CDO.Detachment) * pricer.Notional;
            }
            // Use a solver for floating CDOs
            BreakEvenPremiumSolver fn = new BreakEvenPremiumSolver(pricer, netProtection, settle, df);
            Brent2 rf = new Brent2();
            rf.setToleranceX(1E-6);
            rf.setToleranceF(1E-8);
            return rf.solve(fn, targetPrice, pricer.CDO.Premium);
          }

        }
        finally
        {
          pricer.CDO.Bullet = bullet;
        }
      }
      public override double evaluate(double x)
      {
        double res = netProtection_ + pricer_.FlatFeePv(settle_, x) * df_;
        if (pricer_.CDO.Bullet != null && pricer_.CDO.Bullet.CouponRate > 0)
        {
          res += BulletCouponUtil.GetDiscountedBonus(pricer_);
        }
        return res;
      }
      private BreakEvenPremiumSolver(SyntheticCDOPricer pricer,
        double netProtection, Dt settle, double df)
      {
        pricer_ = pricer;
        settle_ = settle;
        df_ = df;
        netProtection_ = netProtection;
        currentNotional_ = pricer.EffectiveNotional;
      }
      private double netProtection_, currentNotional_;
      private Dt settle_;
      private double df_;
      private SyntheticCDOPricer pricer_;
    }

    private double BreakEvenFee(Dt asOf, Dt settle, bool CDOTypeConversion)
    {
      SyntheticCDOPricer pricer = this;
      SyntheticCDO cdo = pricer.CDO;
      if (cdo.CdoType != CdoType.Unfunded && CDOTypeConversion)
      {
        pricer = (SyntheticCDOPricer)MemberwiseClone();
        cdo = (SyntheticCDO)cdo.ShallowCopy();
        cdo.CdoType = CdoType.Unfunded;
        pricer.CDO = cdo;
      }
      double df = DiscountCurve.DiscountFactor(asOf, settle);
      double protection = pricer.ProtectionPv(settle) * df;
      double fee = pricer.FlatFeePv(settle, settle, cdo.Premium, false, false) * df;
      double bef = Math.Abs(pricer.EffectiveNotional) < 1E-12
          ? 0.0 : ((protection + fee) / pricer.EffectiveNotional);
      if (cdo.CdoType == CdoType.Unfunded)
        return -bef;
      else if (cdo.CdoType == CdoType.IoFundedFixed || cdo.CdoType == CdoType.IoFundedFloating || cdo.CdoType == CdoType.Po)
        return bef;
      else // FundedFixed or FundedFloating
      {
        double par = df * ExpectedBalance(settle, Basket, cdo.Attachment, cdo.Detachment);
        return bef - par;
      }
    }
    #endregion Break Even Calculations

    #region IRatesLockable Members

    RateResets IRatesLockable.LockedRates
    {
      get { return new RateResets(RateResets); }
      set { RateResets.Initialize(value); }
    }

    IEnumerable<RateReset> IRatesLockable.ProjectedRates
    {
      get
      {
        var cdo = CDO;
        if (cdo.CdoType == CdoType.FundedFloating
          || cdo.CdoType == CdoType.IoFundedFloating)
        {
          var ps = new PaymentSchedule();
          ps.AddPaymentSchedule(GeneratePsForFee(Settle, cdo.Premium));
          ps.AddPaymentSchedule(GeneratePsForProtection(Settle));
          return ps.EnumerateProjectedRates();
        }
        return null;
      }
    }

    #endregion

  } // class SyntheticCDOPricer

}
