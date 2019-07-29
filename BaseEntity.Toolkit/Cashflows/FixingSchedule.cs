using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using CompoundingPeriod =
  System.Tuple<BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Cashflows.FixingSchedule>;


namespace BaseEntity.Toolkit.Cashflows
{

  #region FixingSchedule

  /// <summary>
  /// Fixing schedule
  /// </summary>
  [Serializable]
  public abstract class FixingSchedule
  {
    /// <summary>
    /// Reset date (first date at which the rate/price fixing is fully determined) 
    /// </summary>
    public Dt ResetDate { get; set; }

    /// <summary>
    /// Last forward date used for the determination of the rate/price fixing
    /// </summary>
    public abstract Dt FixingEndDate { get; }
  }

  #endregion

  #region ForwardPriceFixingSchedule

  /// <summary>
  /// Fixing schedule for a discretely compounded forward rate
  /// </summary>
  [Serializable]
  public class ForwardPriceFixingSchedule : FixingSchedule
  {
    /// <summary>
    /// Deposit maturity date
    /// </summary>
    public override Dt FixingEndDate
    {
      get { return ResetDate; }
    }

    /// <summary>
    /// Gets or sets the reference date.
    /// </summary>
    /// <value>The reference date.</value>
    public Dt ReferenceDate { get; set; }
  }

  #endregion

  #region ForwardRateFixingSchedule

  /// <summary>
  /// Fixing schedule for a discretely compounded forward rate
  /// </summary>
  [Serializable]
  public class ForwardRateFixingSchedule : FixingSchedule
  {
    /// <summary>
    /// Fixing start date;
    /// </summary>
    public Dt StartDate { get; set; }

    /// <summary>
    /// Fixing end date;
    /// </summary>
    public Dt EndDate { get; set; }

    /// <summary>
    /// Deposit maturity date
    /// </summary>
    public override Dt FixingEndDate
    {
      get { return EndDate; }
    }
  }

  #endregion

  #region ForwardInflationFixingSchedule

  /// <summary>
  /// FFixingSchedule for fixings tied to Inflation index
  /// </summary>
  [Serializable]
  public class ForwardInflationFixingSchedule : FixingSchedule
  {

    /// <summary>
    /// Reset date/Release date 
    /// </summary>
    public Dt[] FixingDates { get; set; }

    /// <summary>
    /// Fixing end date;
    /// </summary>
    public Dt EndDate { get; set; }

    /// <summary>
    /// Last observation of the inflation index used to determine Inflation price fixing
    /// </summary>
    public override Dt FixingEndDate
    {
      get
      {
        if (FixingDates == null || FixingDates.Length == 0)
          return Dt.Empty;
        return FixingDates.Last();
      }
    }
  }

  #endregion

  #region InflationRateFixingSchedule

  /// <summary>
  /// YoY Inflation rate FixingSchedule
  /// </summary>
  [Serializable]
  public class InflationRateFixingSchedule : FixingSchedule
  {
    /// <summary>
    /// Reset/Release dates for numerator 
    /// </summary>
    public Dt[] FixingDates { get; set; }

    /// <summary>
    /// Reset dates for denominator
    /// </summary>
    public Dt[] PreviousFixingDates { get; set; }

    /// <summary>
    /// Fixing start date
    /// </summary>
    public Dt StartDate { get; set; }

    /// <summary>
    /// Fixing end date;
    /// </summary>
    public Dt EndDate { get; set; }

    /// <summary>
    /// Fraction
    /// </summary>
    public double Frac { get; set; }

    /// <summary>
    /// Last observation of the inflation index used to determine Inflation rate fixing
    /// </summary>
    public override Dt FixingEndDate
    {
      get
      {
        if (FixingDates == null || FixingDates.Length == 0)
          return Dt.Empty;
        return FixingDates.Last();
      }
    }
  }

  #endregion

  #region ForwardParYieldFixingSchedule

  /// <summary>
  /// Fixing schedule to calculate par yield from a treasury curve
  /// </summary>
  [Serializable]
  public class ForwardParYieldFixingSchedule : FixingSchedule
  {
    /// <summary>
    /// Fixing period start date
    /// </summary>
    public Dt StartDate { get; set; }

    /// <summary>
    /// The schedule of a forward bond paying the par coupon
    /// </summary>
    public Schedule BondSchedule { get; set; }

    /// <summary>
    /// Last observation of the interest rate index used to determine the swap rate
    /// </summary>
    public override Dt FixingEndDate
    {
      get
      {
        if (BondSchedule == null)
          return Dt.Empty;
        Dt fixedLast = BondSchedule.GetPaymentDate(BondSchedule.Count - 1);
        return fixedLast;
      }
    }
  }

  #endregion

  #region SwapRateFixingSchedule

  /// <summary>
  ///Swap rate fixing schedule 
  /// </summary>
  [Serializable]
  public class SwapRateFixingSchedule : FixingSchedule
  {
    /// <summary>
    /// Settle date of the swap rate
    /// </summary>
    public Dt StartDate { get; set; }

    /// <summary>
    /// Schedule of floating leg
    /// </summary>
    public Schedule FloatingLegSchedule { get; set; }

    ///<summary>
    /// Fixed leg schedule
    /// </summary>
    public Schedule FixedLegSchedule { get; set; }


    /// <summary>
    /// Last observation of the interest rate index used to determine the swap rate
    /// </summary>
    public override Dt FixingEndDate
    {
      get
      {
        if (FloatingLegSchedule == null || FixedLegSchedule == null)
          return Dt.Empty;
        Dt floatLast = FloatingLegSchedule.GetPeriodEnd(FloatingLegSchedule.Count - 1);
        Dt fixedLast = FixedLegSchedule.GetPaymentDate(FixedLegSchedule.Count - 1);
        return floatLast > fixedLast ? floatLast : fixedLast;
      }
    }
  }

  #endregion

  #region AverageRateFixingSchedule

  /// <summary>
  /// Fixing schedule for average rate
  /// </summary>
  [Serializable]
  public class AverageRateFixingSchedule : FixingSchedule
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public AverageRateFixingSchedule()
    {
      ResetDates = new List<Dt>();
      StartDates = new List<Dt>();
      EndDates = new List<Dt>();
      Weights = new List<double>();
    }

    /// <summary>
    /// Reset dates of underlying rates
    /// </summary>
    public List<Dt> ResetDates { get; set; }

    /// <summary>
    /// Start dates of underlying rates
    /// </summary>
    public List<Dt> StartDates { get; set; }

    /// <summary>
    /// End dates of underlying rates
    /// </summary>
    public List<Dt> EndDates { get; set; }

    /// <summary>
    /// Averaging weights
    /// </summary>
    public List<double> Weights { get; set; }

    /// <summary>
    /// Last observation of the interest rate index used to determine the average rate
    /// </summary>
    public override Dt FixingEndDate
    {
      get
      { 
        if(EndDates == null || EndDates.Count == 0)
          return Dt.Empty;
        return  EndDates[EndDates.Count - 1];   
      }   
    }
  }

  #endregion

  #region AveragePriceFixingSchedule

  /// <summary>
  ///  Average price fixing schedule
  /// </summary>
  public abstract class AveragePriceFixingSchedule : FixingSchedule
  {
    /// <summary>
    ///  Average price fixing schedule constructor
    /// </summary>
    protected AveragePriceFixingSchedule()
    {
      ObservationDates = new List<Dt>();
      CurveDates = new List<Dt>();
      Weights = new List<double>();
    }

    /// <summary>
    ///  Observation Dates
    /// </summary>
    public List<Dt> ObservationDates { get; set; }

    /// <summary>
    ///  Weights
    /// </summary>
    public List<double> Weights { get; set; }

    /// <summary>
    ///  Curve Dates
    /// </summary>
    public List<Dt> CurveDates { get; set; }

    /// <summary>
    ///  Fixing End Date
    /// </summary>
    public override Dt FixingEndDate => ObservationDates?[ObservationDates.Count - 1] ?? ResetDate;
  }

  #endregion

  #region CommodityAveragePriceFixingSchedule

  /// <summary>
  ///  Commodity Average price fixing schedule
  /// </summary>
  public class CommodityAveragePriceFixingSchedule : AveragePriceFixingSchedule
  {
  }

  #endregion

  #region VarianceFixingSchedule

  /// <summary>
  ///  Commodity Average price fixing schedule
  /// </summary>
  public class VarianceFixingSchedule : AveragePriceFixingSchedule
  {
  }

  #endregion
}