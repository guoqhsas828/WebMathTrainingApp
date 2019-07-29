//
//  -2015. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using System.Linq;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Swap pricer for contracts composed of two swap legs. Could be both floating, fixed or any combination thereof. 
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.MultiLeggedSwap" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.CashflowModel" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.MultiLeggedSwap"/>
  /// <seealso cref="SwapLegPricer"/>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel"/>
  [Serializable]
  public class MultiLeggedSwapPricer : PricerBase, IPricer, ICashflowNodesGenerator, IReadOnlyList<SwapLegPricer>
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="legPricers">List of swap leg pricers</param>
    public MultiLeggedSwapPricer(IList<SwapLegPricer> legPricers)
      : base(new MultiLeggedSwap(legPricers.Select(l => l.Product).Cast<SwapLeg>().ToList()))
    {
        _swapLegPricers = legPricers;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Get payment schedule
    /// </summary>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      var retVal = new PaymentSchedule();
      foreach (SwapLegPricer swapLegPricer in _swapLegPricers)
      {
        var absNotional = Math.Sign(swapLegPricer.Notional) *
                          Math.Abs(swapLegPricer.SwapLeg.Notional * swapLegPricer.Notional /
                                   swapLegPricer.ValuationCcyNotional);
        var ps = swapLegPricer.GetPaymentSchedule(null, from);
        if (ps != null)
          retVal.AddPayments(ps.ConvertAll<Payment>(p => p.Scale(absNotional)));
      }
      return retVal;
    }

    /// <summary>
    ///   Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    ///
    /// <returns>Total accrued interest</returns>
    ///
    public override double Accrued()
    {
      var retVal = 0.0;
      foreach (SwapLegPricer swapLegPricer in _swapLegPricers)
      {
        retVal += swapLegPricer.Accrued();
      }
      return retVal;
    }

    /// <summary>
    /// Carry
    /// </summary>
    /// <returns></returns>
    public double Carry()
    {
      double carry = 0.0;
      foreach (SwapLegPricer swapLegPricer in _swapLegPricers)
      {
        var settle = swapLegPricer.Settle;
        // accrual starts on later of leg.Effective and Trade.Settle
        Dt startAccruing = settle > swapLegPricer.Product.Effective ? settle : swapLegPricer.Product.Effective;
        if (startAccruing > Settle || Settle >= swapLegPricer.Product.Maturity)
          continue;

        double? coupon = swapLegPricer.CurrentCoupon(true);
        if (!coupon.HasValue)
          throw new ToolkitException(String.Format("Could not determine current coupon for swap leg {0}", Product.Description));
        var fxCurve = swapLegPricer.FxCurve;
        var fxRate = fxCurve == null ? 1.0 : fxCurve.FxRate(Settle, swapLegPricer.Product.Ccy, ValuationCurrency);
        carry += coupon.Value / 360 * swapLegPricer.NotionalFactorAt(settle) * swapLegPricer.Notional * fxRate;
      }
      return carry;
    }

    /// <summary>
    /// Deep copy 
    /// </summary>
    /// <returns>A new swap pricer object.</returns>
    public override object Clone()
    {
      var swapLegPricers = _swapLegPricers.Select(slp => (SwapLegPricer)slp.Clone()).ToList();
      return new MultiLeggedSwapPricer(swapLegPricers);
    }

    /// <summary>
    ///   Validate product inputs
    /// </summary>
    /// <param name="errors">Error list </param>
    /// <remarks>
    ///   This tests only relationships between fields of the pricer that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      foreach (var swapLegPricer in _swapLegPricers)
      {
        swapLegPricer.Validate();
      }

      if (_swapLegPricers.Select(slp => slp.ValuationCurrency).Distinct().Count() > 1)
      {
        InvalidValue.AddError(errors, this, "ValuationCurrency","All swap leg pricers must have same valuation currency");
      }
    }

    /// <summary>
    /// Present value of a swap composed of two legs, i.e. the difference of the the present value of the two legs.
    /// </summary>
    /// <returns>Present value of the swap leg</returns>
    public override double ProductPv()
    {
      var retVal = 0.0;
      foreach (SwapLegPricer swapLegPricer in _swapLegPricers)
      {
        retVal += swapLegPricer.ProductPv();
      }
      return retVal;
    }

    ///<summary>
    /// Present value including pv of any additional payment
    ///</summary>
    ///<returns></returns>
    public override double Pv()
    {
      var retVal = 0.0;
      foreach (SwapLegPricer swapLegPricer in _swapLegPricers)
      {
        retVal += swapLegPricer.Pv();
      }
      return retVal;
    }

    #endregion Method

    #region Properties

    /// <summary>
    /// As of date
    /// </summary>
    public override Dt AsOf
    {
      get { return _swapLegPricers.Min(p => p.AsOf); }
      set
      {
        foreach (var p in _swapLegPricers)
          p.AsOf = value;
      }
    }

    /// <summary>
    /// Settle of the trade
    /// </summary>
    public override Dt Settle
    {
      get { return _swapLegPricers.Min(p => p.Settle); }
      set
      {
        foreach (var p in _swapLegPricers)
          p.Settle = value;
      }
    }

    /// <summary>
    /// Product
    /// </summary>
    public Swap Swap
    {
      get { return Product as Swap; }
    }

    /// <summary>
    /// Discount curve 
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get
      {
        if (_swapLegPricers.Select( p => p.DiscountCurve).Distinct().Count() > 1)
          throw new ToolkitException("Payer and receiver legs are discounted with different curves, call is ambiguous");
        return _swapLegPricers.First().DiscountCurve;
      }
    }

    /// <summary>
    /// Swap discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves
    {
      get
      {
        var list = new HashSet<DiscountCurve>();
        foreach (var p in _swapLegPricers)
        {
          var curve = p.ReferenceCurve as DiscountCurve;
          if (curve != null) list.Add(curve);
          curve = p.DiscountCurve;
          if (curve != null) list.Add(curve);
        }
        return list.ToArray();
      }
    }

    /// <summary>
    /// Swap FxCurves
    /// </summary>
    public FxCurve[] FxCurves
    {
      get
      {
        var list = new HashSet<FxCurve>();
        foreach (FxCurve rFx in _swapLegPricers.Select(p => p.FxCurve).Where(rFx => rFx != null))
        {
          list.Add(rFx);
        }
        return list.ToArray();
      }
    }

    /// <summary>
    /// Reference curves
    /// </summary>
    public CalibratedCurve[] ReferenceCurves
    {
      get
      {
        var list = new HashSet<CalibratedCurve>();
        foreach (var referenceCurve in _swapLegPricers.Select(p => p.ReferenceCurve).Where(referenceCurve => referenceCurve != null))
          list.Add(referenceCurve);

        return list.ToArray();
      }
    }

    /// <summary>
    /// Gets the valuation currency.
    /// </summary>
    /// <value>The valuation currency.</value>
    public override Currency ValuationCurrency
    {
      get
      {
        if (_swapLegPricers.Select(p => p.ValuationCurrency).Distinct().Count() > 1)
          throw new ArgumentException("SwapLegPricers should be denominated in the same currency");
        return _swapLegPricers.First().ValuationCurrency;
      }
    }

    /// <summary>
    /// The pricers for each individual swap leg
    /// </summary>
    public IEnumerable<SwapLegPricer> SwapLegPricers
    {
      get { return _swapLegPricers; }
    }

      #endregion

    #region ICashflowNodesGenerator Members

    /// <summary>
    /// Generate cashflow for simulation
    /// </summary>
    IList<ICashflowNode> ICashflowNodesGenerator.Cashflow
    {
      get
      {
        double notional = Notional;
        IList<ICashflowNode> retVal = null;
        foreach (var p in _swapLegPricers)
        {
          retVal = p.GetPaymentSchedule(null, AsOf).ToCashflowNodeList(Math.Abs(p.SwapLeg.Notional),
            p.Notional / notional, p.DiscountCurve, null,
            retVal);
        }

        return retVal;
      }
    }

    #endregion

    #region Data

    private readonly IList<SwapLegPricer> _swapLegPricers = new List<SwapLegPricer>();

    #endregion

    #region IReadOnlyCollection<SwapLegPricer> members

    /// <summary>
    /// how many legs in a swap
    /// </summary>
    public int Count
    {
      get { return _swapLegPricers.Count; }
    }

    /// <summary>
    /// Get enumerator
    /// </summary>
    /// <returns></returns>
    public IEnumerator<SwapLegPricer> GetEnumerator()
    {
      return _swapLegPricers.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return _swapLegPricers.GetEnumerator();
    }

    /// <summary>
    /// Swapleg pricer indexing.
    /// </summary>
    /// <param name="index">index</param>
    /// <returns></returns>
    public SwapLegPricer this[int index]
    {
      get { return _swapLegPricers[index]; }
    }

    #endregion
  }
}