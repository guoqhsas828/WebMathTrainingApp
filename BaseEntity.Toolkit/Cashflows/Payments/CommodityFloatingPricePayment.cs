// 
//  -2017. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  ///  Floating commodity price payment
  /// </summary>
  [Serializable]
  public class CommodityFloatingPricePayment : CommodityPricePayment, IFloatingPayment
  {

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CommodityFloatingPricePayment" /> class.
    /// </summary>
    /// <param name="payDate">The pay date.</param>
    /// <param name="ccy">The ccy.</param>
    /// <param name="periodStart">The period start.</param>
    /// <param name="periodEnd">The period end.</param>
    /// <param name="notional">The notionalQuantity.</param>
    /// <param name="spread">The spread.</param>
    /// <param name="projector">The projector.</param>
    /// <param name="forwardAdjustment">Calculator for convexity/cap/floor.</param>
    public CommodityFloatingPricePayment(Dt payDate,
                                         Currency ccy,
                                         Dt periodStart,
                                         Dt periodEnd,
                                         double notional,
                                         double spread,
                                         IRateProjector projector,
                                         IForwardAdjustment forwardAdjustment)
      : base(payDate, ccy, periodStart, periodEnd, notional)
    {
      Spread = spread;
      RateProjector = projector;
      ForwardAdjustment = forwardAdjustment;
      FixingSchedule = (CommodityAveragePriceFixingSchedule)RateProjector.GetFixingSchedule(Dt.Empty, periodStart, periodEnd, payDate);
    }

    #endregion

    #region Properties
    
    /// <summary>
    /// Fixing schedule
    /// </summary>
    public CommodityAveragePriceFixingSchedule FixingSchedule { get; set; }

    /// <summary>
    ///  For IFloatingInterestPayment interface
    /// </summary>
    public double EffectiveRate
    {
      get { return Price; }
      set { PriceOverride = value; }
    }

    /// <summary>
    /// Gets or sets the price.
    /// </summary>
    /// <value>
    /// The price.
    /// </value>
    public override double Price
    {
      get
      {
        if (PriceOverride.HasValue)
          return PriceOverride.Value; // all-in one price
        var fixing = RateProjector.Fixing(FixingSchedule);
        if (ForwardAdjustment == null)
          return fixing.Forward + Spread;
        var forward = fixing.Forward;
        var ca = ForwardAdjustment.ConvexityAdjustment(PayDt, FixingSchedule, fixing);
        return forward + ca;
      }
      set { PriceOverride = value; }
    }

    /// <summary>
    /// Override the price
    /// </summary>
    public double? PriceOverride { get; set; }

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
        if (PriceOverride.HasValue)
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
        new Tuple<string, Type>("Fixing", typeof(double)),
        new Tuple<string, Type>("Spread", typeof(double)),
        new Tuple<string, Type>("ResetDate", typeof(string)),
        new Tuple<string, Type>("RateResetState", typeof(string)),
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
      row["Fixing"] = Price - Spread;
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
      if (PriceOverride.HasValue)
      {
        resetInfos.Add(new RateResets.ResetInfo(ResetDate, PriceOverride.Value, RateResetState.ResetFound));
        return resetInfos;
      }
      return RateProjector?.GetResetInfo(RateProjector.GetFixingSchedule(Dt.Empty, PeriodStartDate, PeriodEndDate, PayDt));
    }

    #endregion

  }
}