// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Immutable object type used to represent reset actions
  /// </summary>
  public sealed class ResetAction
  {}

  /// <summary>
  /// Abstract base class for simple pricers on individual products.
  /// </summary>
  /// <remarks>
  ///   <para>Pricers need not inherit from this class but for
  ///   convenience this provides common properties.</para>
  ///   <para>This class should not be referenced directly but rather
  ///   the <see cref="IPricer"/> interface should be used.</para>
  /// </remarks>
  [Serializable]
  public abstract class PricerBase : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Product to price</param>
    protected PricerBase(IProduct product)
      : this(product, Dt.Empty, Dt.Empty)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Product to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    protected PricerBase(IProduct product, Dt asOf, Dt settle)
    {
      product_ = product;
      asOf_ = asOf;
      settle_ = settle;
      origNotional_ = 1.0;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (PricerBase)base.Clone();
      obj.product_ = (IProduct)product_.Clone();
      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some internal state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that all internate states should be cleared or updated.</para>
    ///   <para>Derived Pricers may implement this and should call base.Reset()</para>
    /// </remarks>
    public virtual void Reset()
    {}

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>Some pricers need to remember certain internal states in order
    ///   to skip redundant calculation steps.
    ///   This function tells the pricer that what attributes of the products
    ///   and other data have changed and therefore give the pricer an opportunity
    ///   to selectively clear/update its internal states.  When used with caution,
    ///   this method can be much more efficient than the method Reset() without argument,
    ///   since the later resets everything.</para>
    ///   <para>The default behaviour of this method is to ignore the parameter
    ///   <paramref name="what"/> and simply call Reset().  The derived pricers
    ///   may implement a more efficient version.</para>
    /// </remarks>
    /// <param name="what">The flags indicating what attributes to reset</param>
    public virtual void Reset(ResetAction what)
    {
      Reset();
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (!asOf_.IsValid())
        InvalidValue.AddError(errors, this, "AsOf", String.Format("Invalid as-of date ({0})", asOf_));
      if (!settle_.IsValid())
        InvalidValue.AddError(errors, this, "Settle", String.Format("Invalid settlement date ({0})", settle_));
      if (asOf_ > settle_)
        InvalidValue.AddError(errors, this, "Settle", String.Format("Settlement {0} must be on or after pricing AsOf date {1}", settle_, asOf_));
      if (product_ == null)
        InvalidValue.AddError(errors, this, "Product", "Product must not be null");
      return;
    }

    /// <summary>
    /// Get cashflows for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the specified date.</para>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    /// <returns>Cashflow from the specified date or null if not supported</returns>
    public virtual Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="paymentSchedule"></param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>PaymentSchedule from the specified date or null if not supported</returns>
    public virtual PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// True if product is active
    /// </summary>
    /// <remarks>
    ///   <para>A product is active if the pricing AsOf date is on or after the
    ///   product effective date or on or before the product maturity date.</para>
    /// </remarks>
    /// <returns>true if product is active</returns>
    protected bool IsActive()
    {
      return product_.IsActive(AsOf);
    }

    ///<summary>
    /// Net present value of the product, excluding the value
    /// of any additional payment.
    ///</summary>
    ///<returns></returns>
    ///<exception cref="NotImplementedException"></exception>
    public abstract double ProductPv();

    ///<summary>
    /// Convenience (static) method for building a fee pricer for
    /// an additional payment.
    ///</summary>
    ///<param name="payment"></param>
    ///<param name="discountCurve"></param>
    ///<returns></returns>
    public virtual IPricer BuildPaymentPricer(Payment payment, DiscountCurve discountCurve)
    {
      if (payment != null)
      {
        if (payment.PayDt > Settle) // strictly greater than
        {
          var oneTimeFee = new OneTimeFee(payment.Ccy, payment.Amount, payment.PayDt, "");
          var pricer = new SimpleCashflowPricer(oneTimeFee, AsOf, Settle, discountCurve, null);
          pricer.Add(payment.PayDt, payment.Amount, 0.0, 0.0, 0.0, 0.0, 0.0);
          return pricer;
        }
      }
      return null;
    }

    ///<summary>
    /// Pricer that will be used to price any additional (e.g. upfront) payment
    /// associated with the pricer.
    ///</summary>
    /// <remarks>
    /// It is the responsibility of derived classes to build this
    /// pricer with the appropriate discount curve for the payment's currency
    /// and to decide whether it can cache the pricer between uses.
    /// </remarks>
    ///<exception cref="NotImplementedException"></exception>
    public virtual IPricer PaymentPricer
    {
      get
      {
        if (Payment == null)
          return null;
        throw new NotImplementedException();
      }
    }

    ///<summary>
    /// Present value of any additional payment associated with the pricer.
    ///</summary>
    ///<returns></returns>
    public virtual double PaymentPv()
    {
      if (PaymentPricer != null)
      {
        if (Payment.PayDt > Settle) // strictly greater than
        {
          return PaymentPricer.Pv();
        }
      }
      return 0.0;
    }

    /// <summary>
    /// Present value (full price) including andy additional payment to the pricing as-of date
    /// </summary>
    /// <returns>Present value</returns>
    public virtual double Pv()
    {
      double pv = (IsTerminated) ? 0.0 : ProductPv();
      pv += PaymentPv();
      return pv;
    }

    /// <summary>
    /// Accrued interest
    /// </summary>
    public virtual double Accrued()
    {
      return 0.0;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Early Termination
    /// </summary>
    public bool IsTerminated
    {
      get { return isTerminated_; }
      set { isTerminated_ = value; }
    }

    /// <summary>
    ///   Product to price
    /// </summary>
    public IProduct Product
    {
      get { return product_; }
      set
      {
        product_ = value;
        Reset(ResetProduct);
      }
    }

    /// <summary>
    ///   Pricing as-of date for pricing
    /// </summary>
    public virtual Dt AsOf
    {
      get { return asOf_; }
      set
      {
        asOf_ = value;
        Reset(ResetAsOf);
      }
    }

    /// <summary>
    ///   PVFlags for low level pricing directives
    /// </summary>
    public PricerFlags PricerFlags
    {
      get { return pricerFlags_; }
      set
      {
        pricerFlags_ = value;
        Reset(ResetPvFlags);
      }
    }

    /// <summary>
    ///   Settlement date for pricing
    /// </summary>
    public virtual Dt Settle
    {
      get { return settle_; }
      set
      {
        settle_ = value;
        Reset(ResetSettle);
      }
    }

    /// <summary>
    ///   Original notional amount for pricing
    /// </summary>
    public double Notional
    {
      get { return origNotional_; }
      set { origNotional_ = value; }
    }

    /// <summary>
    ///   Effective outstanding notional on the settlement date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is the effective notional at the settlement
    ///   date. It includes adjustments based on amortizations
    ///   and any defaults prior to the settlement date.  Depending
    ///   on pricing methods, it may include the name defaulted before
    ///   the pricer settle date but the default loss/recovery has
    ///   to be included in the prices (for example, when the default
    ///   is yet settled on the pricing date).</para>
    /// </remarks>
    ///
    public virtual double EffectiveNotional
    {
      // This is introduced in release 8.7, 13June07.
      // Readonly at this time.
      get { return origNotional_; }
    }

    /// <summary>
    /// True to approximate the calculation of Pv (can be used for sensitivities when the calculation of the Pv is computationally intensive)
    /// </summary>
    public bool ApproximateForFastCalculation { get; set; }

    /// <summary>
    ///   Current outstanding notional on the settlement date
    /// </summary>
    /// <remarks>
    ///   <para>This is the current notional at the settlement
    ///   date, excluding all the names defaulted before the settle date.</para>
    /// </remarks>
    public virtual double CurrentNotional
    {
      // By default, we simply return the effective notional.
      // The derived class may override it to exclude the names
      // defaulted but not yet settled.
      get { return EffectiveNotional; }
    }

    /// <summary>
    /// Additonal payment to be added 
    /// </summary>
    public Payment Payment
    {
      get { return payment_; }
      set
      {
        payment_ = value;
        Reset(ResetPayment);
      }
    }

    /// <summary>
    /// Pv currency
    /// </summary>
    public virtual Currency ValuationCurrency
    {
      get { return Product.Ccy; }
    }

    #endregion Properties

    #region ResetAction

    /// <summary>AsOf changed</summary>
    public static readonly ResetAction ResetAsOf = new ResetAction();

    /// <summary>Settle changed</summary>
    public static readonly ResetAction ResetSettle = new ResetAction();

    /// <summary>Settle changed</summary>
    public static readonly ResetAction ResetPvFlags = new ResetAction();

    ///<summary> Payment changed</summary>
    public static readonly ResetAction ResetPayment = new ResetAction();

    /// <summary>Product changed</summary>
    public static readonly ResetAction ResetProduct = new ResetAction();

    /// <summary>Volatility changed</summary>
    public static readonly ResetAction ResetVolatility = new ResetAction();

    /// <summary>Reset everything</summary>
    public static readonly ResetAction ResetAll = new ResetAction();

    #endregion ResetAction

    #region Data

    private bool isTerminated_;
    private IProduct product_;
    private Dt asOf_;
    private Dt settle_;
    private Payment payment_;
    private double origNotional_;
    private PricerFlags pricerFlags_;
    /// <exclude />
    protected IPricer paymentPricer_;

    /// <summary>
    ///  configuration 
    /// </summary>
    protected ToolkitConfigSettings settings_ => ToolkitConfigurator.Settings;

    #endregion Data
  }

  // class PricerBase

  #region Flags

  /// <summary>
  /// Bit Field for Giving Pricers Low Level Directives
  /// </summary>
  [Flags]
  public enum PricerFlags
  {
    /// <summary>
    /// Exclude defaults from effecting calculation
    /// 
    /// <example>Calc Theta without defaults so large default payments don't get added to time sensitivity</example>
    /// </summary>
    NoDefaults = 1,

    /// <summary>
    /// When Rate Sensitivities are done, ignore maturity and calc sensitivity for all tenors
    /// </summary>
    SensitivityToAllRateTenors = 2
  }

  #endregion
}