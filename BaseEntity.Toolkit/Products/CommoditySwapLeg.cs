// 
//  -2017. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Fixed or floating price commodity swap leg
  /// </summary>
  [Serializable]
  [ReadOnly(true)]
  public class CommoditySwapLeg : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Constructor of commodity floating leg 
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="ccy">Currency</param>
    /// <param name="price">Fixed spread over floating payment</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="bdc">Business day convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="cycleRule">Schedule cycle rule </param>
    /// <param name="projType"></param>
    /// <param name="index">Commodity price index</param>
    /// <param name="observationRule">Fixing observation rule </param>
    /// <param name="numObs">Number of price observation to determine fixing</param>
    /// <param name="weighted">Weight each observation by the number of days between fixings</param>
    /// <param name="payLag">Payment lag in days</param>
    /// <param name="rollExpiry">Roll expiry</param>
    public CommoditySwapLeg(Dt effective, Dt maturity, Currency ccy, double price, 
                            Frequency freq, BDConvention bdc, Calendar cal, CycleRule cycleRule, 
                            ProjectionType projType, ReferenceIndex index,
                            CommodityPriceObservationRule observationRule, int numObs, bool weighted,
                            int payLag, bool rollExpiry)
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, bdc, cal, cycleRule, CashflowFlag.None)
    {
      Price = price;
      ReferenceIndex = index;
      ProjectionType = projType;
      ObservationRule = observationRule;
      Observations = numObs;
      RollExpiry = rollExpiry;
      Weighted = weighted;
      PayLagRule = payLag > 0 ? new PayLagRule(payLag, true) : null;
    }

    /// <summary>
    /// Constructor of commodity fixed leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="ccy">Currency</param>
    /// <param name="price">Fixed price payment</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="bdc">Business day convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="cycleRule">Schedule cycle rule </param>
    /// <param name="payLag">Payment lag in days</param>
    /// <param name="rollExpiry">Roll expiry</param>
    public CommoditySwapLeg(Dt effective, Dt maturity, Currency ccy, double price,
      Frequency freq, BDConvention bdc, Calendar cal, CycleRule cycleRule, int payLag, bool rollExpiry)
      : this(effective, maturity, ccy, price, freq, bdc, cal, cycleRule, ProjectionType.None, null,
        CommodityPriceObservationRule.All, 0, false, payLag, rollExpiry)
    {
    }

    /// <summary>
    /// Constructor of commodity floating leg, terms matching index
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="coupon">Spread over floating price</param>
    /// <param name="index">Commodity index</param>
    /// <param name="observationRule">Observation rule for floating payment fixing</param>
    /// <param name="numObs">Number of observation for floating payment fixing</param>
    /// <param name="payLag">Payment lag in days</param>
    /// <param name="rollExpiry">Roll expiry</param>
    /// <param name="weighted">Weight each observation by the number of days between fixings</param>
    public CommoditySwapLeg(Dt effective, Dt maturity, Frequency freq, double coupon, CommodityPriceIndex index, CommodityPriceObservationRule observationRule,
      int numObs, bool weighted, int payLag, bool rollExpiry)
      : this(
        effective, maturity, index.Currency, coupon, freq, index.Roll, index.Calendar, CycleRule.None,
        index.ProjectionTypes[0], index, observationRule, numObs, weighted, payLag, rollExpiry)
    {
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (SwapLeg)base.Clone();
      obj.ProjectionType = ProjectionType;

      return obj;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Fixed Price or Price Spread if floating
    /// </summary>
    [Category("Base")]
    public double Price { get; set; }

    /// <summary>
    ///   Quantity of commodity
    /// </summary>
    [Category("Base")]
    public double Quantity { get; set; }

    /// <summary>
    /// Reference index
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; set; }

    /// <summary>
    ///  Projection type
    /// </summary>
    public ProjectionType ProjectionType
    {
      get { return _ptype; }
      set { _ptype = value; }
    }

    /// <summary>
    ///  True if floating rate
    /// </summary>
    [Category("Base")]
    public bool Floating => ReferenceIndex != null;

    /// <summary>
    ///  Gets or sets the payment lag rule.
    /// </summary>
    /// <value>
    /// The pay lag rule.
    /// </value>
    public PayLagRule PayLagRule { get; set; }

    /// <summary>
    ///  Gets or sets the observation rule
    /// </summary>
    /// <value>
    /// The rule for how often/when to observe prices on a floating leg
    /// </value>
    public CommodityPriceObservationRule ObservationRule { get; set; }

    /// <summary>
    ///  Gets or sets the number of observations.
    /// </summary>
    /// <value>
    /// The number of observations.
    /// </value>
    public int Observations { get; set; }

    /// <summary>
    ///  True if floating coupon is fixed at the end of the coupon period. Default value is false
    /// </summary>
    [Category("Base")]
    public bool RollExpiry { get; private set; }

    /// <summary>
    ///  Amortization schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule => _amortSched ?? (_amortSched = new List<Amortization>());

    /// <summary>
    ///  True if swap amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes => false;

    /// <summary>
    ///  Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<CouponPeriod> PriceSchedule => _priceSched ?? (_priceSched = new List<CouponPeriod>());

    /// <summary>
    ///  True if swap has strike schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp => !(_priceSched == null || _priceSched.Count == 0);

    /// <summary>
    ///  Final date the product keeps to be active
    /// </summary>
    public override Dt EffectiveMaturity
    {
      get
      {
        if (_effectiveMaturity != null)
          return _effectiveMaturity.Value;
        if (PayLagRule == null || PayLagRule.PaymentLagDays == 0)
          _effectiveMaturity = Maturity;
        else if (!Maturity.IsEmpty())
        {
          _effectiveMaturity = PayLagRule.CalcPaymentDate(Maturity, PayLagRule.PaymentLagDays,
            PayLagRule.PaymentLagBusinessFlag, BDConvention,
            Calendar);
        }
        else
        {
          _effectiveMaturity = Dt.Roll(Dt.Add(Maturity, PayLagRule.PaymentLagDays), BDConvention, Calendar);
        }
        return _effectiveMaturity.Value;
      }
    }

    /// <summary>
    ///   Weighted resets
    /// </summary>
    [Category("Base")]
    public bool Weighted { get; set; }

    #endregion

    #region Methods

    /// <summary>
    ///  Validate product
    /// </summary>
    /// <remarks>
    ///  This tests only relationships between fields of the product that
    ///  cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Validate schedules
      AmortizationUtil.Validate(_amortSched, errors);
      CouponPeriodUtil.Validate(_priceSched, errors, false);

      if (Floating)
      {
        if (ObservationRule == CommodityPriceObservationRule.All && Observations != 0)
          InvalidValue.AddError(errors, this, "NumObservations", "Number of observations must be 0 for a floating price leg averaging from all observations");
        else if (ObservationRule != CommodityPriceObservationRule.All && Observations < 0)
          InvalidValue.AddError(errors, this, "NumObservations", $"Number of observations must be >= 0 for a floating price leg with {ObservationRule} rule");
      }
      else
      {
        if (ObservationRule != CommodityPriceObservationRule.All)
          InvalidValue.AddError(errors, this, "ObservationRule", "ObservationRule must be None for fixed price leg");
      }
    }

    /// <summary>
    /// Projection params for the swap leg
    /// </summary>
    /// <returns></returns>
    public ProjectionParams GetProjectionParams()
    {
      return new ProjectionParams
      {
        ProjectionType = ProjectionType,
        CompoundingFrequency = Frequency.None,
        CompoundingConvention = CompoundingConvention.None,
        ResetLag = Tenor.Empty,
        ProjectionFlags = ProjectionFlag.None
      };
    }

    #endregion

    #region Data

    private List<Amortization> _amortSched;
    private List<CouponPeriod> _priceSched;
    private ProjectionType _ptype;
    [Mutable]
    private Dt? _effectiveMaturity;

    #endregion Data
  }
}