// 
//  -2015. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Base.ReferenceIndices
{
  /// <summary>
  /// Interface representing common convention for market prices of assets
  /// </summary>
  public interface IAssetPriceIndex
  {
    /// <summary>
    /// Gets the type of the price.
    /// </summary>
    /// <value>The type of the price.</value>
    QuotingConvention PriceType { get; }

    /// <summary>
    /// Currency of denomination of the price
    /// </summary>
    /// <value>The currency</value>
    Currency Currency { get; }

    /// <summary>
    /// Business day calendar
    /// </summary>
    /// <value>The calendar</value>
    Calendar Calendar { get; }

    /// <summary>
    /// Number of business days between the trade date and the settlement date
    /// </summary>
    /// <value>Number of business days to settlement</value>
    int SettlementDays { get; }

    /// <summary>
    /// Convention to roll an observation date to a business day
    /// </summary>
    /// <value>The roll convention</value>
    BDConvention Roll { get; }

    /// <summary>
    /// Historical observations of the asset prices
    /// </summary>
    /// <value>The historical observations</value>
    RateResets HistoricalObservations { get; }
  }


  /// <summary>
  /// A simple asset pricer index
  /// </summary>
  [Serializable]
  public class AssetPriceIndex : IAssetPriceIndex
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="priceType">Type of the price.</param>
    /// <param name="ccy">Currency of denomination of the index</param>
    /// <param name="calendar">Calendar</param>
    /// <param name="settleDays">Settlement days</param>
    /// <param name="roll">The roll convention</param>
    /// <param name="priceObservations">The price observations.</param>
    public AssetPriceIndex(string indexName,
      QuotingConvention priceType, 
      Currency ccy,  Calendar calendar, int settleDays,
      BDConvention roll, RateResets priceObservations)
    {
      IndexName = indexName;
      PriceType = priceType;
      Currency = ccy;
      Calendar = calendar;
      SettlementDays = settleDays;
      Roll = roll;
      HistoricalObservations = priceObservations;
    }

    #endregion

    /// <summary>
    /// Gets the name of the index.
    /// </summary>
    /// <value>The name of the index.</value>
    public string IndexName { get; private set; }

    /// <summary>
    /// Gets the type of the price.
    /// </summary>
    /// <value>The type of the price.</value>
    public QuotingConvention PriceType { get; private set; }

    /// <summary>
    /// Currency of denomination of the price
    /// </summary>
    /// <value>The currency</value>
    public Currency Currency { get; private set; }

    /// <summary>
    /// Business day calendar
    /// </summary>
    /// <value>The calendar</value>
    public Calendar Calendar { get; private set; }

    /// <summary>
    /// Number of business days between the trade date and the settlement date
    /// </summary>
    /// <value>Number of business days to settlement</value>
    public int SettlementDays { get; private set; }

    /// <summary>
    /// Convention to roll an observation date to a business day
    /// </summary>
    /// <value>The roll convention</value>
    public BDConvention Roll { get; private set; }

    /// <summary>
    /// Historical observations
    /// </summary>
    /// <value>The historical observations.</value>
    public RateResets HistoricalObservations { get; private set; }
  }

}
