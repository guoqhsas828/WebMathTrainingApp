using System;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;


namespace BaseEntity.Toolkit.Products
{
  #region Credit Linked Note
  /// <summary>
  /// Credit Linked Note. 
  /// </summary>
  /// <remarks>The CLN pays out fixed/floating coupon until the underlying CreditDerivative or Collateral are terminated by a credit event.
  /// If the CreditDerivative defaults first, the underlying Collateral is liquidated at the prevaling market price and the investor receives 
  /// the proceeds left after the CreditDerivative counterparty is paid the contingent payment (floored at zero). 
  /// If the collateral piece defaults first, the deal is unwound at the prevailing market price and the investor receives the recovery on the collateral
  /// piece plus the prevailing market price of the CreditDerivatives, all floored at zero.</remarks>
  [Serializable]
  public class CreditLinkedNote : ProductWithSchedule
  {
    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="creditDerivative">Underlying credit derivative</param>
    /// <param name="collateralPiece">Collateral piece. 
    /// Call/Conversion features are not supported and are ignored.</param>
    /// <param name="effective">Effective date of the CLN</param>
    /// <param name="maturity">Maturity of the CLN</param>
    /// <param name="ccy">Denomination currency</param>
    /// <param name="coupon">Fixed coupon (in %) or spread (in bps)</param>
    /// <param name="dayCount">Daycount convention</param>
    /// <param name="cycleRule">Cycle rule</param>
    /// <param name="freq">Frequency</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="referenceIndex">Reference index for floating payments</param>
    public CreditLinkedNote(IProduct creditDerivative, Bond collateralPiece, Dt effective, Dt maturity, Currency ccy,
                            double coupon, DayCount dayCount, CycleRule cycleRule, Frequency freq, BDConvention roll,
                            Calendar calendar, ReferenceIndex referenceIndex) :
      base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, roll, calendar, cycleRule, CashflowFlag.AccrueOnCycle)
    {
      CreditDerivative = creditDerivative;
      Collateral = collateralPiece;
      Coupon = (referenceIndex == null) ? coupon : coupon*1e-4;//if reference index is null, assume fixed coupons
      DayCount = dayCount;
      ReferenceIndex = referenceIndex;
    }

    #endregion

    #region Methods
    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="errors">Errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      var pt = (ProjectionType == ProjectionType.None) ? ProjectionType.SimpleProjection : ProjectionType;
      if (ReferenceIndex != null && Array.IndexOf(ReferenceIndex.ProjectionTypes, pt) < 0)
        InvalidValue.AddError(errors, this, "Incompatible ReferenceIndex and ProjectionType.");
      if (!(CreditDerivative is SyntheticCDO || CreditDerivative is CDS || CreditDerivative is FTD))
        InvalidValue.AddError(errors, this,
                              String.Format("Product of type {0} not supported", CreditDerivative.GetType()));
      if (CreditDerivative is SyntheticCDO && ((SyntheticCDO) CreditDerivative).CdoType != CdoType.Unfunded)
        InvalidValue.AddError(errors, this, "The underlying CreditDerivative should be unfunded");
      if (CreditDerivative is CDS && ((CDS) CreditDerivative).CdsType != CdsType.Unfunded)
        InvalidValue.AddError(errors, this, "The underlying CreditDerivative should be unfunded");
      if (CreditDerivative is FTD && ((FTD) CreditDerivative).NtdType != NTDType.Unfunded)
        InvalidValue.AddError(errors, this, "The underlying CreditDerivative should be unfunded");
    }
    #endregion

    #region Properties
    /// <summary>
    /// Underlying credit derivative (CDS, CDX, CDO or NTD)
    /// </summary>
    public IProduct CreditDerivative { get; private set; }

    /// <summary>
    /// Collateral piece 
    /// </summary>
    public Bond Collateral { get; private set; }

    /// <summary>
    /// Fixed nominal coupon
    /// </summary>
    [Category("Base")]
    public double Coupon { get; set; }

    /// <summary>
    /// Daycount convention
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

    /// <summary>
    /// True to pay recovery payments at maturity of the CLN
    /// </summary>
    [Category("Base")]
    public bool PayRecoveryAtMaturity { get; set; }

    /// <summary>
    /// Cap over floating coupon. Null if coupon is not capped
    /// </summary>
    [Category("Base")]
    public double? Cap { get; set; }

    /// <summary>
    /// Floor on floating coupon. Null if coupon is not floored
    /// </summary>
    [Category("Base")]
    public double? Floor { get; set; }

    /// <summary>
    ///  Reference index.
    ///  </summary>
    [Category("Base")]
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    /// Projection type
    /// </summary>
    [Category("Base")]
    public ProjectionType ProjectionType
    {
      get;
      set;
    }

    /// <summary>
    ///  Amortization schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule
    {
      get { return amortSched_ ?? new List<Amortization>(); }
      set { amortSched_ = value; }
    }

    /// <summary>
    ///   True if bond amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes
    {
      get { return (amortSched_ != null && amortSched_.Count > 0); }
    }

    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<CouponPeriod> CouponSchedule
    {
      get { return couponSched_ ?? new List<CouponPeriod>(); }
      set { couponSched_ = value; }
    }

    /// <summary>
    ///   True if bond has coupon schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp
    {
      get { return (couponSched_ != null && couponSched_.Count > 0); }
    }
    #endregion

    #region Data
    private IList<Amortization> amortSched_;
    private IList<CouponPeriod> couponSched_;
    #endregion
  }
  #endregion
}
