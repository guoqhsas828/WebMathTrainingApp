// 
//  -2017. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  ///  Floating commodity price payment
  /// </summary>
  [Serializable]
  public class VariancePayment : Payment
  {
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="VariancePayment" /> class.
    /// </summary>
    /// <param name="payDate">The pay date.</param>
    /// <param name="ccy">The ccy.</param>
    /// <param name="periodStart">The period start.</param>
    /// <param name="periodEnd">The period end.</param>
    /// <param name="notional">The notionalQuantity.</param>
    /// <param name="spread">The spread.</param>
    /// <param name="projector">The projector.</param>
    /// <param name="forwardAdjustment">Calculator for convexity/cap/floor.</param>
    public VariancePayment(Dt payDate,
                                         Currency ccy,
                                         Dt periodStart,
                                         Dt periodEnd,
                                         double notional,
                                         double spread,
                                         IRateProjector projector,
                                         IForwardAdjustment forwardAdjustment)
      : base(payDate, ccy)
    {
      PeriodStartDate = periodStart;
      PeriodEndDate = periodEnd;
      Notional = notional;
      Spread = spread;
      RateProjector = projector;
      ForwardAdjustment = forwardAdjustment;
      FixingSchedule = (VarianceFixingSchedule)RateProjector.GetFixingSchedule(Dt.Empty, periodStart, periodEnd, payDate);
    }
    
    #endregion
    /// <summary>
    /// Scale payment amount
    /// </summary>
    /// <param name="factor"></param>
    public override void Scale(double factor)
    {
      base.Scale(factor);
      Notional *= factor;
    }

    /// <summary>
    /// When Amount is derived from other fields implement this
    /// </summary>
    /// <returns></returns>
    protected override double ComputeAmount()
    {
      return Variance * Notional;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the notional quantity.
    /// </summary>
    /// <value>
    /// The notional quantity.
    /// </value>
    public double Notional { get; set; }

    /// <summary>
    /// Gets or sets the period start date.
    /// </summary>
    /// <value>
    /// The period start date.
    /// </value>
    public Dt PeriodStartDate { get; set; }

    /// <summary>
    /// Gets or sets the period end date.
    /// </summary>
    /// <value>
    /// The period end date.
    /// </value>
    public Dt PeriodEndDate { get; set; }

    /// <summary>
    /// Fixing schedule
    /// </summary>
    public VarianceFixingSchedule FixingSchedule { get; set; }

    /// <summary>
    ///  For IFloatingInterestPayment interface
    /// </summary>
    public double EffectiveRate
    {
      get { return Variance; }
      set { VarianceOverride = value; }
    }

    /// <summary>
    /// Gets or sets the price.
    /// </summary>
    /// <value>
    /// The price.
    /// </value>
    public double Variance
    {
      get
      {
        if (VarianceOverride.HasValue)
          return VarianceOverride.Value; // all-in one price
        var fixing = RateProjector.Fixing(FixingSchedule);
        if (ForwardAdjustment == null)
          return fixing.Forward + Spread;
        var forward = fixing.Forward;
        var ca = ForwardAdjustment.ConvexityAdjustment(PayDt, FixingSchedule, fixing);
        return forward + ca;
      }
      set { VarianceOverride = value; }
    }

    /// <summary>
    /// Override the price
    /// </summary>
    public double? VarianceOverride { get; set; }

    /// <summary>
    ///  Price spread
    /// </summary>
    public double Spread { get; set; }

    /// <summary>
    /// Reset date
    /// </summary>
    public Dt ResetDate
    {
      get { return FixingSchedule.ResetDate; }
      set { FixingSchedule.ResetDate = value; }
    }

    /// <summary>
    /// True if the rate is a projected future rate
    /// </summary>
    public override bool IsProjected
    {
      get
      {
        var state = RateResetState;
        return (state == RateResetState.IsProjected);
      }
    }

    /// <summary>
    ///  Price reset state
    /// </summary>
    public RateResetState RateResetState
    {
      get
      {
        if (VarianceOverride.HasValue)
          return RateResetState.ResetFound;
        var fixingSchedule = RateProjector.GetFixingSchedule(Dt.Empty, PeriodStartDate, PeriodEndDate, PayDt);
        var fixing = RateProjector.Fixing(fixingSchedule);
        return fixing.RateResetState;
      }
    }

    /// <summary>
    /// Gets the rate (price) projector.
    /// </summary>
    /// <value>
    /// The projector.
    /// </value>
    public IRateProjector RateProjector { get; set; }

    /// <summary>
    /// Convexity/cap/floor calculator
    /// </summary>
    public IForwardAdjustment ForwardAdjustment { get; private set; }
    #endregion

    #region Methods

    /// <summary>
    /// Add data columns
    /// </summary>
    /// <param name="collection">Data column collection</param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      foreach (var x in new List<Tuple<string, Type>>
                        {
                          new Tuple<string, Type>("Notional", typeof(double)),
                          new Tuple<string, Type>("Period Start", typeof(string)),
                          new Tuple<string, Type>("Period End", typeof(string)),
                          new Tuple<string, Type>("Fixing", typeof(double)),
                          new Tuple<string, Type>("Spread", typeof(double)),
                          new Tuple<string, Type>("ResetDate", typeof(string)),
                          new Tuple<string, Type>("RateResetState", typeof(string)),
                          new Tuple<string, Type>("Amount", typeof(double)),
                          new Tuple<string, Type>("Price", typeof(double)),
                        }.Where(x => !collection.Contains(x.Item1)))
      {
        collection.Add(new DataColumn(x.Item1, x.Item2));
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="row"></param>
    /// <param name="dtFormat"></param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row["Notional"] = Notional.ToString(CultureInfo.InvariantCulture);
      row["Period Start"] = PeriodStartDate.ToStr(dtFormat);
      row["Period End"] = PeriodEndDate.ToStr(dtFormat);
      row["Price"] = Variance;
      row["Amount"] = Amount;
      row["Fixing"] = Variance - Spread;
      row["ResetDate"] = ResetDate;
      row["RateResetState"] = RateResetState.ToString();
      row["Spread"] = Spread;
    }

    /// <summary>
    /// Gets reset info for the period
    /// </summary>
    ///<returns>Return a list of reset information (reset date, fixing value, reset state). 
    /// If the state is projected or missing the fixing value is zero by default 
    /// (only found past or overridden fixings are displayed)</returns>
    public List<RateResets.ResetInfo> GetRateResetComponents()
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      if (VarianceOverride.HasValue)
      {
        resetInfos.Add(new RateResets.ResetInfo(ResetDate, VarianceOverride.Value, RateResetState.ResetFound));
        return resetInfos;
      }
      return RateProjector?.GetResetInfo(RateProjector.GetFixingSchedule(Dt.Empty, PeriodStartDate, PeriodEndDate, PayDt));
    }

    #endregion

  }
}