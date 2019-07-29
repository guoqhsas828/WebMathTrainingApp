/*
 * CapletPayment.cs
 *
 *   2005-2010. All rights reserved.
 * 
 */

using System;
using System.Data;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// A payment on a caplet.
  /// </summary>
  [Serializable]
  public class CapletPayment : Payment, IHasForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Payment Base Class
    /// </summary>
    public CapletPayment()
    {
    }

    /// <summary>
    /// Create Payment
    /// </summary>
    /// <param name="payDate">Date of Payment</param>
    public CapletPayment(Dt payDate)
    {
      PayDt = payDate;
    }

    /// <summary>
    /// Create Payment
    /// </summary>
    /// <param name="payDate">Date of Payment</param>
    /// <param name="strike">Strike rate</param>
    /// <param name="notional">Notional amount</param>
    /// <param name="ccy">Currency of Payment</param>
    /// <param name="type">Caplet or floorlet</param>
    /// <param name="perFrac">Period fraction</param>
    /// <param name="digitalType">Digital payout type (defaults to None)</param>
    /// <param name="digital">Digital payout rate (defaults to 0.0)</param>
    /// <remarks>
    /// Use this constructor only for fixed payments. Once set this way, 
    /// amount is immutable.
    /// </remarks>
    public CapletPayment(Dt payDate, double strike, double notional, Currency ccy, CapFloorType type, double perFrac,
      OptionDigitalType digitalType = OptionDigitalType.None,  double digital = 0.0)
    {
      PayDt = payDate;
      Strike = strike;
      Notional = notional;
      Ccy = ccy;
      Type = type;
      PeriodFraction = perFrac;
      OptionDigitalType = digitalType;
      DigitalFixedPayout = digital;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Digital fixed payout for digital caplet
    /// </summary>
    public double DigitalFixedPayout { get; set; }
    
    /// <summary>
    /// Option digital type
    /// </summary>
    public OptionDigitalType OptionDigitalType { get; set; } 
    
    /// <summary>
    /// The caplet's strike
    /// </summary>
    public double Strike { get; set; }
    
    /// <summary>
    /// The notional amount of the caplet
    /// </summary>
    public double Notional { get; set; }

    /// <summary>
    /// Caplet expiry date (date of rate fixing)
    /// </summary>
    public Dt Expiry { get; set; }

    /// <summary>
    /// Rate on expiry date (if known)
    /// </summary>
    public double Rate { get; set; }

    /// <summary>
    /// Index multiplier
    /// </summary>
    public double IndexMultiplier
    {
      get { return _indexMultiplier ?? 1.0; }
      set { _indexMultiplier = value; }
    }

    /// <summary>
    /// Fractional interest period.
    /// </summary>
    public double PeriodFraction { get; set; }

    /// <summary>
    /// The Date the forward rate gets reset 
    /// </summary>
    public Dt RateFixing { get; set; }

    /// <summary>The Tenor Date , the end date for the forward rate </summary>
    public Dt TenorDate { get; set; }

    /// <summary>
    /// Set/Get AsOf date of ForwardAdjustment 
    /// </summary>
    public override Dt VolatilityStartDt
    {
      get
      {
        if (ForwardAdjustment == null)
          return base.VolatilityStartDt;
        return ((ForwardAdjustment)ForwardAdjustment).AsOf;
      }
      set
      {
        if (ForwardAdjustment == null)
          base.VolatilityStartDt = value;
        else
          ((ForwardAdjustment)ForwardAdjustment).AsOf = value;
      }
    }

    /// <summary>
    /// Processor for projection of floating rate
    /// </summary>
    public IRateProjector RateProjector { get; internal set; }

    /// <summary>
    /// Processor for projection of floating rate
    /// </summary>
    internal IForwardAdjustment ForwardAdjustment { get; set; }

    internal IVolatilityObject VolatilityObject { get; set; }

    /// <summary>
    /// Whether its a caplet or floorlet
    /// </summary>
    public CapFloorType Type { get; set; }

    /// <summary>
    /// State of the interest rate fixing.
    /// </summary>
    public RateResetState RateResetState { get; set; }

    /// <summary>
    /// Whether the interest rate is projected or known.
    /// </summary>
    public override bool IsProjected
    {
      get { return RateResetState == RateResetState.IsProjected; }
    }

    /// <summary>
    /// Whether the interest rate fixing should be found but is missing.
    /// </summary>
    public bool IsMissingReset
    {
      get { return RateResetState == RateResetState.Missing; }
    }

    internal OptionType OptionType
    {
      get { return (Type == CapFloorType.Cap) ? OptionType.Call : OptionType.Put; }
    }

    IForwardAdjustment IHasForwardAdjustment.ForwardAdjustment
    {
      get { return ForwardAdjustment; }
      set { ForwardAdjustment = value; }
    }

    #endregion

    #region Data

    [Mutable] private double? _indexMultiplier;

    #endregion Data

    #region Column Names

    private static string StrikeLabel = "Strike";
    private static string ExpiryLabel = "Expiry";
    private static string NotionalLabel = "Notional";
    private static string RateFixingLabel = "RateFixing";
    private static string TenorDateLabel = "TenorDate";
    private static string RateLabel = "Rate";
    private static string IndexMultiplierLabel = "IndexMultiplier";
    private static string TypeLabel = "Type";
    private static string DigitalLabel = "Digital";

    #endregion

    #region Methods

    /// <summary>
    /// Get an evaluable expression for amount calculation
    /// </summary>
    /// <returns>Evaluable</returns>
    public override Evaluable GetEvaluableAmount()
    {
      return CapletEvaluation.Amount(this);
    }

    /// <summary>
    /// Compute the payment amount
    /// </summary>
    /// <returns></returns>
    protected override double ComputeAmount()
    {
      double rate;
      if (RateResetState == RateResetState.ObservationFound || RateProjector == null)
      {
        rate = Rate;
      }
      else
      {
        var projector = RateProjector;
        var fixingSchdule = projector.GetFixingSchedule(
          Dt.Empty, RateFixing, TenorDate, PayDt);
        var fixing = projector.Fixing(fixingSchdule);
        var ca = 0.0;
        var forwardAdjustment = ForwardAdjustment;
        if (forwardAdjustment != null)
        {
          ca = forwardAdjustment.ConvexityAdjustment(
            PayDt, fixingSchdule, fixing);
        }
        rate = fixing.Forward + ca;
      }

      var T = VolatilityStartDt.IsEmpty() ? 0.0 : (Expiry - VolatilityStartDt)/365.0;
      if (T < 0) T = 0;

      var vol = T <= 0.0 ? 0.0 : CapletVolatility(rate);
      var volatilityType = GetVolatilityType();

      rate *= IndexMultiplier;
      if (OptionDigitalType != OptionDigitalType.None)
      {
        switch (volatilityType)
        {
        case DistributionType.Normal:
          return Notional*PeriodFraction*DigitalOption.NormalBlackP(
            OptionStyle.European, OptionType, OptionDigitalType, T,
            rate, Strike, vol, DigitalFixedPayout);
        case DistributionType.LogNormal:
          return Notional*PeriodFraction*DigitalOption.BlackP(
            OptionStyle.European, OptionType, OptionDigitalType, T,
            rate, Strike, vol, DigitalFixedPayout);
        }
      }
      if (volatilityType == DistributionType.LogNormal)
      {
        if (rate <= 0.0 || Strike <= 0)
        {
          return Notional*PeriodFraction*Math.Max((
            OptionType == OptionType.Call ? 1.0 : -1.0)*(rate - Strike), 0.0);
        }
        return Notional*PeriodFraction*Black.P(
          OptionType, T, rate, Strike, vol);
      }
      if (volatilityType == DistributionType.Normal)
      {
        return Notional*PeriodFraction*BlackNormal.P(
          OptionType, T, 0, rate, Strike, vol);
      }
      throw new ApplicationException(string.Format(
        "Distribution type {0} not supported", volatilityType));
    }

    private ReferenceIndex GetReferenceIndex()
    {
      var calculator = RateProjector as CouponCalculator;
      return calculator != null ? calculator.ReferenceIndex : null;
    }

    internal DistributionType GetVolatilityType()
    {
      var vo = VolatilityObject;
      return vo == null ? DistributionType.LogNormal
        : vo.DistributionType;
    }

    internal double CapletVolatility(double rate)
    {
      var o = VolatilityObject;
      if (o == null) return 0;
      var v = o.CapletVolatility(Expiry, rate, Strike, GetReferenceIndex());
      if (o.DistributionType == DistributionType.Normal)
        v *= Math.Abs(IndexMultiplier);
      return v;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="collection"></param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      if (!collection.Contains(StrikeLabel))
        collection.Add(new DataColumn(StrikeLabel, typeof (double)));
      if (!collection.Contains(NotionalLabel))
        collection.Add(new DataColumn(NotionalLabel, typeof (double)));
      if (!collection.Contains(ExpiryLabel))
        collection.Add(new DataColumn(ExpiryLabel, typeof (string)));
      if (!collection.Contains(RateFixingLabel))
        collection.Add(new DataColumn(RateFixingLabel, typeof (string)));
      if (!collection.Contains(TenorDateLabel))
        collection.Add(new DataColumn(TenorDateLabel, typeof (string)));
      if (!collection.Contains(RateLabel))
        collection.Add(new DataColumn(RateLabel, typeof (double)));
      if (!IndexMultiplier.AlmostEquals(1.0) && !collection.Contains(IndexMultiplierLabel))
        collection.Add(new DataColumn(IndexMultiplierLabel, typeof (double)));
      if (!collection.Contains(TypeLabel))
        collection.Add(new DataColumn(TypeLabel, typeof (string)));
      if (!collection.Contains(DigitalLabel))
        collection.Add(new DataColumn(DigitalLabel, typeof(double)));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="row"></param>
    /// <param name="dtFormat"></param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row[StrikeLabel] = Strike;
      row[NotionalLabel] = Notional;
      row[ExpiryLabel] = Expiry.ToStr(dtFormat);
      row[RateLabel] = Rate;
      if (!IndexMultiplier.AlmostEquals(1.0))
        row[IndexMultiplierLabel] = IndexMultiplier;
      row[RateFixingLabel] = RateFixing.ToStr(dtFormat);
      row[TenorDateLabel] = TenorDate.ToStr(dtFormat);
      row[TypeLabel] = Type.ToString();
    }

    #endregion
  }
}