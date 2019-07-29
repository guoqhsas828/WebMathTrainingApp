using System;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;


namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Product to generally represent an asset swap based on cashflows
  /// </summary>
  [Serializable]
  [ReadOnly(true)]
  public class AssetSwap : Product
  {
    /// <summary>
    /// Construct Asset Swap
    /// </summary>
    /// <param name="assetProduct">Underlying asset</param>
    /// <param name="effective">Effective date</param>
    /// <param name="assetSwapQuoteType">Quote type</param>
    /// <param name="dayCount">Daycount convention</param>
    /// <param name="frequency">Payment frequency</param>
    /// <param name="calendar">Holidays calendar</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="spread">Spread over libor</param>
    /// <param name="dealPrice">Price of the deal</param>
    public AssetSwap(IProduct assetProduct, Dt effective, AssetSwapQuoteType assetSwapQuoteType, DayCount dayCount, Frequency frequency,
      Calendar calendar, BDConvention roll, double spread, double dealPrice)
    {
      Effective = effective;
      Maturity = assetProduct.Maturity;
      Ccy = assetProduct.Ccy;
      Notional = assetProduct.Notional;
      Description = "Asset Swap on " + assetProduct.Description;
      AssetSwapQuoteType = assetSwapQuoteType;
      DayCount = dayCount;
      Frequency = frequency;
      Calendar = calendar;
      Roll = roll;
      Spread = spread;
      DealPrice = dealPrice;
    }

    /// <summary>
    /// Market or Par quoted Asset Swap
    /// </summary>
    public AssetSwapQuoteType AssetSwapQuoteType { get; set; }
    /// <summary>
    /// DayCount of floating leg
    /// </summary>
    public DayCount DayCount { get; set; }
    /// <summary>
    /// Frequency of floating leg
    /// </summary>
    public Frequency Frequency { get; set; }
    /// <summary>
    /// Calendar of floating leg
    /// </summary>
    public Calendar Calendar { get; set; }
    /// <summary>
    /// Roll Convention of floating leg
    /// </summary>
    public BDConvention Roll {get; set;}
    /// <summary>
    /// Spread of floating leg
    /// </summary>
    public double Spread { get; set; }
    /// <summary>
    /// Deal Price of floating leg
    /// </summary>
    public double DealPrice { get; set; }
    /// <summary>
    /// Amortization Schedule of Floating Leg
    /// </summary>
    public List<Amortization> AmortizationSchedule { get; set; }
  }
}
