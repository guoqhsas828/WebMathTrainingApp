//
// AssetSwapPricer.cs
//  -2012. All rights reserved.
// 
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;


namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price an Asset Swap
  /// </summary>
  public class AssetSwapPricer : PricerBase, IPricer
  {
    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="assetPricer">Pricer of the underlying asset</param>
    /// <param name="assetSwap">Asset swap specifications</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="rateModelParameters">Model parameters</param>
    /// <param name="rateResets">Historical resets</param>
    /// <param name="refCurve">Projection curve</param>
    /// <param name="refIndex">Projection index</param>
    public AssetSwapPricer(IAssetPricer assetPricer, AssetSwap assetSwap, DiscountCurve discountCurve,
      RateModelParameters rateModelParameters, RateResets rateResets, CalibratedCurve refCurve, InterestRateIndex refIndex)
      : base(assetSwap)
    {
      Notional = assetPricer.Notional;
      AssetPricer = assetPricer;
      DiscountCurve = discountCurve;
      RateModelParams = rateModelParameters;
      RateResets = rateResets;
      ReferenceCurve = refCurve;
      ReferenceIndex = refIndex;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Pricer for the asset portion
    /// </summary>
    public IAssetPricer AssetPricer { get; private set; }
    
    //resources for swap pricing:
    /// <summary>
    /// Projection index for floating leg
    /// </summary>
    public InterestRateIndex ReferenceIndex { get; private set; }
    /// <summary>
    /// Funding curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }
    /// <summary>
    /// Reference curve used for floating payment projections
    /// </summary>
    public CalibratedCurve ReferenceCurve { get; private set; }
    /// <summary>
    /// Historical rate resets
    /// </summary>
    public RateResets RateResets { get; set; }
    /// <summary>
    /// Model parameters
    /// </summary>
    public RateModelParameters RateModelParams { get; set; }

    /// <summary>
    /// Floating leg pricer. 
    /// </summary>
    /// <remarks>Pricer is re-created each time this property is called</remarks>
    public SwapLegPricer FloatingLegPricer
    {
      get
      {
        return AssetPricer.GetFloatingLegPricer(AssetSwap, DiscountCurve, ReferenceCurve, ReferenceIndex,
                     RateModelParams);
      }
    }

    /// <summary>
    /// As of date
    /// </summary>
    public override Dt AsOf
    {
      get
      {
        return AssetPricer.AsOf;
      }
      set
      {
        AssetPricer.AsOf = value;
      }
    }

    /// <summary>
    /// Settlement date
    /// </summary>
    public override Dt Settle
    {
      get
      {
        return AssetPricer.Settle;
      }
      set
      {
        AssetPricer.Settle = value;
      }
    }

    /// <summary>
    /// AssetSwap
    /// </summary>
    public AssetSwap AssetSwap
    {
      get { return (AssetSwap) Product; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Payment schedule for payments made to the AssetSwapBuyer
    /// </summary>
    /// <param name="paymentSchedule">PaymentSchedule</param>
    /// <param name="from">From date</param>
    /// <returns>PaymentSchedule</returns>
    public PaymentSchedule GetAssetSwapBuyerReceiverSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      var retVal = FloatingLegPricer.GetPaymentSchedule(null, from);
      if (AssetSwap.Effective >= from)
      {
        double floatingNotional = (AssetSwap.AssetSwapQuoteType == AssetSwapQuoteType.PAR) ? -1.0 : -AssetSwap.DealPrice;
        double upFront = floatingNotional + AssetSwap.DealPrice;
        if (upFront > 0)
          retVal.AddPayment(new PrincipalExchange(AssetSwap.Effective, AssetSwap.Notional*upFront, AssetSwap.Ccy));
      }
      return retVal;
    }

    /// <summary>
    /// Payment schedule for payments made by the AssetSwapBuyer
    /// </summary>
    /// <param name="paymentSchedule">PaymentSchedule</param>
    /// <param name="from">From date</param>
    /// <returns>PaymentSchedule</returns>
    public PaymentSchedule GetAssetSwapBuyerPayerSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      var retVal = AssetPricer.GetPaymentSchedule(null, from);
      if (AssetSwap.Effective >= from)
      {
        double floatingNotional = (AssetSwap.AssetSwapQuoteType == AssetSwapQuoteType.PAR) ? -1.0 : -AssetSwap.DealPrice;
        double upFront = floatingNotional + AssetSwap.DealPrice;
        if (upFront < 0)
          retVal.AddPayment(new PrincipalExchange(AssetSwap.Effective, -AssetSwap.Notional*upFront, AssetSwap.Ccy));
      }
      return retVal;
    }

   
    private double FloatingLegPv()
    {
      var floatingLegPricer = FloatingLegPricer;
      var swapPs = floatingLegPricer.GetPaymentSchedule(null, AsOf);
      var ap = AssetPricer;
      return swapPs.CalculatePv(AsOf, Settle, DiscountCurve, null, null, 0.0,
        0, TimeUnit.None, AdapterUtil.CreateFlags(ap.IncludeSettlePayments,
          ap.IncludeMaturityProtection, ap.DiscountingAccrued));
    }


    private double AssetLegPv()
    {
      var assetPs = AssetPricer.GetPaymentSchedule(null, AsOf);
      var ap = AssetPricer;
      return assetPs.CalculatePv(AsOf, Settle, DiscountCurve, null, null, 0.0,
        0, TimeUnit.None, AdapterUtil.CreateFlags(ap.IncludeSettlePayments,
          ap.IncludeMaturityProtection, ap.DiscountingAccrued));
    }


    private double FloatingNotional()
    {
      return (AssetSwap.AssetSwapQuoteType == AssetSwapQuoteType.PAR) ? 1.0 : AssetSwap.DealPrice;
    }


    private double UpfrontPv()
    {
     if (Settle <= Product.Effective)
        return (-FloatingNotional() + AssetSwap.DealPrice)*DiscountCurve.Interpolate(Settle, Product.Effective);
      return 0.0;
    }

    /// <summary>
    ///   Present value (including accrued) for product to pricing as-of date given pricing arguments
    /// </summary>
    ///
    /// <returns>Present value</returns>
    ///
    public override double ProductPv()
    {
      return AssetPricer.Notional*(UpfrontPv() - AssetLegPv() + FloatingNotional()*FloatingLegPv());
    }

    /// <summary>
    ///   Spread to enter a par asset swap
    /// </summary>
    ///
    /// <returns>Asset Swap Spread</returns>
    ///
    public double AssetSwapSpread()
    {
      return SolveAssetSwap();
    }

    /// <summary>
    /// Convert spread over reference index into price
    /// </summary>
    /// <returns></returns>
    public double PriceFromAssetSwapSpread()
    {
      double floatingLegPv = FloatingLegPv();
      double assetLegPv = AssetLegPv();
      return (AssetSwap.AssetSwapQuoteType == AssetSwapQuoteType.PAR)
               ? 1.0 + assetLegPv - floatingLegPv
               : assetLegPv/floatingLegPv;
    }

    private double SolveAssetSwap()
    {
      var solver = new Brent2();
      solver.setLowerBounds(-1);
      solver.setUpperBounds(1);
      solver.setInitialPoint(0.0050);
      solver.setToleranceF(1E-14);
      solver.setToleranceX(1E-14);
      return solver.solve(new AssetSwapSolverFn(this), 0.0);
    }
    
    #region Overrides of SolverFn
    private class AssetSwapSolverFn : SolverFn
    {
      public AssetSwapSolverFn(AssetSwapPricer p)
      {
        pricer_ = p;
        var floatingLegPricer = p.FloatingLegPricer;
        paymentSchedule_ = floatingLegPricer.GetPaymentSchedule(null, p.AsOf);
        pv_ = pricer_.AssetLegPv();
      }

      public override double evaluate(double x)
      {
        double dealPrice = pricer_.AssetSwap.DealPrice;
        double floatingNotional = pricer_.FloatingNotional();
        foreach (var coupon in paymentSchedule_)
        {
          var ip = coupon as InterestPayment;
          if (ip != null)
            ip.FixedCoupon = x;
        }
        double upfront = -floatingNotional + dealPrice; //upfront exchanged at Settle so no discounting necessary
        
        double swapLegPv = paymentSchedule_.CalculatePv(pricer_.AsOf, pricer_.Settle,
          pricer_.DiscountCurve, null, null, 0.0, 0, TimeUnit.None,
          AdapterUtil.CreateFlags(pricer_.AssetPricer.IncludeSettlePayments,
            pricer_.AssetPricer.IncludeMaturityProtection,
            pricer_.AssetPricer.DiscountingAccrued));

        return upfront - pv_ + floatingNotional*swapLegPv;
      }

      private readonly double pv_;
      private readonly AssetSwapPricer pricer_;
      private readonly PaymentSchedule paymentSchedule_;
    }
    #endregion

    /// <summary>
    /// Change product spread
    /// </summary>
    /// <param name="spread"></param>
    public void SetSpread(double spread)
    {
      ((AssetSwap)Product).Spread = spread;
      Reset();
    }
    #endregion
  }

  #region IAssetPricer
  /// <summary>
  /// Asset pricer interface
  /// </summary>
  public interface IAssetPricer
  {
    /// <summary>
    /// Underlying product
    /// </summary>
    IProduct Product { get; set; }
    /// <summary>
    /// Trade notional
    /// </summary>
    double Notional { get; set; }
    /// <summary>
    /// As of date
    /// </summary>
    Dt AsOf { get; set; }
    /// <summary>
    /// Settlement date
    /// </summary>
    Dt Settle { get; set; }
    /// <summary>
    /// True to include payments occurring at settlement date
    /// </summary>
    bool IncludeSettlePayments { get; }
    /// <summary>
    /// Include maturity date in protection
    /// </summary>
    bool IncludeMaturityProtection { get; }
    /// <summary>
    /// Discount accrued
    /// </summary>
    bool DiscountingAccrued { get; }
    /// <summary>
    /// Step size for pricing grid
    /// </summary>
    int StepSize { get; set; }
    /// <summary>
    /// Step unit for pricing grid
    /// </summary>
    TimeUnit StepUnit { get; set; }
    /// <summary>
    /// Maturity of the deal
    /// </summary>
    Dt Maturity { get; set; }
    /// <summary>
    /// Current notional (net of amortization/prepayment)
    /// </summary>
    double CurrentNotional { get; }
    /// <summary>
    /// Get the payment schedule
    /// </summary>
    /// <param name="ps">Payment schedule</param>
    /// <param name="from">From date</param>
    /// <returns>Payment schedule</returns>
    PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from);
    /// <summary>
    /// Create floating leg for the asset swap
    /// </summary>
    /// <param name="assetSwap">Asset swap spread</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="projectionCurve">Projection curve</param>
    /// <param name="projectionIndex">Projection index</param>
    /// <param name="modelParameters">Model parameters</param>
    /// <returns>Pricer for the floating leg of the AssetSwap</returns>
    SwapLegPricer GetFloatingLegPricer(AssetSwap assetSwap, DiscountCurve discountCurve, CalibratedCurve projectionCurve, InterestRateIndex projectionIndex, RateModelParameters modelParameters);
  }
  #endregion

  #region AssetSwapQuoteType
  /// <summary>
  /// Quote type
  /// </summary>
  public enum AssetSwapQuoteType
  {
    /// <summary>
    /// None specified
    /// </summary>
    None,
    /// <summary>
    /// Par spread over libor
    /// </summary>
    PAR,
    /// <summary>
    /// Price
    /// </summary>
    MARKET
  }
  #endregion

}
