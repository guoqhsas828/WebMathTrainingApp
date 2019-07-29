using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Curves.Commodities
{
  #region PaymentSettings

  /// <summary>
  /// Base class
  /// </summary>
  public abstract class CommodityPaymentSettings
  {}

  /// <summary>
  /// Payment settings
  /// </summary>
  public class CommodityFuturePaymentSettings : CommodityPaymentSettings
  {
    #region Constructors

    /// <summary>
    /// Setting for the payment
    /// </summary>
    public CommodityFuturePaymentSettings()
    {
      TickSize = 0.1;
      TickValue = 1.0;
      ContractSize = 10.0;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Contract size
    /// </summary>
    public double ContractSize { get; set; }

    /// <summary>
    /// Tick size
    /// </summary>
    public double TickSize { get; set; }

    /// <summary>
    /// Tick value
    /// </summary>
    public double TickValue { get; set; }

    #endregion
  }

  /// <summary>
  /// Payment settings
  /// </summary>
  public class CommoditySwapPaymentSettings : CommodityPaymentSettings
  {
    #region Constructors

    /// <summary>
    /// Setting for the payment
    /// </summary>
    public CommoditySwapPaymentSettings()
    {
      RecProjectionType = ProjectionType.CommodityPrice;
      PayProjectionType = ProjectionType.CommodityPrice;
      RecObservationRule = CommodityPriceObservationRule.Last;
      RecNumObs = 1;
      PayObservationRule = CommodityPriceObservationRule.Last;
      PayNumObs = 1;
      SpreadOnReceiver = false;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Projection type
    /// </summary>
    public ProjectionType RecProjectionType { get; set; }

    /// <summary>
    /// Projection type
    /// </summary>
    public ProjectionType PayProjectionType { get; set; }

    /// <summary>
    /// Observation rule receiver leg
    /// </summary>
    public CommodityPriceObservationRule RecObservationRule { get; set; }

    /// <summary>
    /// Observation rule payer leg
    /// </summary>
    public CommodityPriceObservationRule PayObservationRule { get; set; }

    /// <summary>
    /// Number of observations to determine projection in receiver leg 
    /// </summary>
    public int RecNumObs { get; set; }

    /// <summary>
    /// Spread on receiver leg
    /// </summary>
    public bool SpreadOnReceiver { get; set; }

    /// <summary>
    /// Number of observations to determine projection in payer leg 
    /// </summary>
    public int PayNumObs { get; set; }

    /// <summary>
    /// Payment lag
    /// </summary>
    public int PaymentLag { get; set; }

    #endregion
  }

  #endregion

  #region CommodityAssetCurveTerm

  /// <summary>
  ///   Asset curve term for commodity forward prices.
  /// </summary>
  [Serializable]
  public class CommodityAssetCurveTerm : AssetCurveTerm
  {
    private readonly InstrumentType _type;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommodityAssetCurveTerm" /> class.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="spotDays">The spot days.</param>
    /// <param name="bd">The roll convention.</param>
    /// <param name="dc">The day count.</param>
    /// <param name="cal">The calendar.</param>
    /// <param name="freq">The frequency.</param>
    /// <param name="projectionType">Type of the projection.</param>
    /// <param name="priceIndex">Index of the price.</param>
    public CommodityAssetCurveTerm(InstrumentType type, int spotDays,
                                   BDConvention bd, DayCount dc, Calendar cal, Frequency freq,
                                   ProjectionType projectionType, CommodityPriceIndex priceIndex)
    {
      _type = type;
      SpotDays = spotDays;
      Roll = bd;
      DayCount = dc;
      Calendar = cal;
      PayFreq = freq;
      CommodityPriceIndex = priceIndex;
      ProjectionType = (projectionType != ProjectionType.None)
                         ? projectionType
                         : ProjectionType.CommodityPrice;
    }

    #region Properties

    /// <summary>
    /// Type of asset
    /// </summary>
    /// <value>The type.</value>
    public override InstrumentType Type
    {
      get { return _type; }
    }

    ///<summary>
    /// Fixing calendar
    ///</summary>
    public Calendar Calendar { get; set; }

    ///<summary>
    /// Business-day convention
    ///</summary>
    public BDConvention Roll { get; set; }

    ///<summary>
    /// DayCount convention
    ///</summary>
    public DayCount DayCount { get; set; }

    ///<summary>
    /// Payment frequency
    ///</summary>
    public Frequency PayFreq { get; set; }

    ///<summary>
    /// Days to settle the asset
    ///</summary>
    public int SpotDays { get; set; }

    /// <summary>
    /// ProjectionType
    /// </summary>
    public ProjectionType ProjectionType { get; set; }

    /// <summary>
    /// Reference index
    /// </summary>
    public CommodityPriceIndex CommodityPriceIndex { get; set; }

    #endregion
  }

  #endregion

  #region CommodityFutureAssetCurveTerm

  /// <summary>
  /// Futures terms
  /// </summary>
  [Serializable]
  public class CommodityFutureAssetCurveTerm : AssetCurveTerm
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="bd">Roll</param>
    /// <param name="cal">Calendar</param>
    /// <param name="contractSize">Tick size</param>
    /// <param name="tickSize">Tick size</param>
    /// <param name="tickValue">Tick value</param>
    /// <param name="referenceIndex">Commodity index</param>
    public CommodityFutureAssetCurveTerm(BDConvention bd, Calendar cal, double contractSize, double tickSize, double tickValue, CommodityPriceIndex referenceIndex)
    {
      Roll = bd;
      Calendar = cal;
      TickSize = tickSize;
      TickValue = tickValue;
      CommodityPriceIndex = referenceIndex;
      ContractSize = contractSize;
    }

    /// <summary>
    /// Type of asset
    /// </summary>
    /// <value>The type.</value>
    public override InstrumentType Type
    {
      get { return InstrumentType.FUT; }
    }

    ///<summary>
    /// Fixing calendar
    ///</summary>
    public Calendar Calendar { get; set; }

    ///<summary>
    /// Business-day convention
    ///</summary>
    public BDConvention Roll { get; set; }

    /// <summary>
    /// Contract size
    /// </summary>
    public double ContractSize { get; set; }


    ///<summary>
    /// Tick size
    ///</summary>
    public double TickSize { get; set; }

    ///<summary>
    /// Tick value
    ///</summary>
    public double TickValue { get; set; }

    /// <summary>
    /// Reference index
    /// </summary>
    public CommodityPriceIndex CommodityPriceIndex { get; set; }

    /// <summary>
    /// Payment settings
    /// </summary>
    public CommodityFuturePaymentSettings PaymentSettings
    {
      get
      {
        var retVal = new CommodityFuturePaymentSettings();
        if (TickSize > 0)
          retVal.TickSize = TickSize;
        if (TickValue > 0)
          retVal.TickValue = TickValue;
        if (ContractSize > 0)
          retVal.ContractSize = ContractSize;
        return retVal;
      }
    }
  }

  #endregion

  #region CommoditySwapAssetCurveTerm

  /// <summary>
  /// Terms for fixed-floating swap
  /// </summary>
  [Serializable]
  public class CommoditySwapCurveTerm : AssetCurveTerm
  {
    #region Constructor

    /// <summary>
    /// Constructor 
    /// </summary>
    /// <param name="spotDays">Days to spot</param>
    /// <param name="bd">Business day convention</param>
    /// <param name="dc">Day count</param>
    /// <param name="cal">Calendar</param>
    /// <param name="freq">Frequency of fixed payments</param>
    /// <param name="fixedQuantityFreq">Quantity freq of fixed payments</param>
    /// <param name="floatFreq">Frequency of floating payments</param>
    /// <param name="floatProjectionType">Floating payment projection type</param>
    /// <param name="floatQuantityFreq">Quantity freq of floating payments</param>
    /// <param name="floatObservationRule">Observation rule of floating payments</param>
    /// <param name="floatNumObs">Number of commodity price observations required for each payment</param>
    /// <param name="referenceIndex">Commodity price index</param>
    public CommoditySwapCurveTerm(int spotDays, BDConvention bd, DayCount dc, Calendar cal, Frequency freq, QuantityFrequency fixedQuantityFreq,
                                  Frequency floatFreq, ProjectionType floatProjectionType,
                                  QuantityFrequency floatQuantityFreq, CommodityPriceObservationRule floatObservationRule, int floatNumObs,
                                  CommodityPriceIndex referenceIndex)
    {
      SpotDays = spotDays;
      Roll = bd;
      DayCount = dc;
      Calendar = cal;
      PayFreq = freq;
      QuantityFreq = fixedQuantityFreq;
      ProjectionType = (floatProjectionType != ProjectionType.None) ? floatProjectionType : ProjectionType.CommodityPrice;
      FloatPayFreq = floatFreq;
      FloatNumObs = floatNumObs;
      FloatObservationRule = floatObservationRule;
      FloatQuantityFreq = floatQuantityFreq;
      ReferenceIndex = referenceIndex;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Product type
    /// </summary>
    public override InstrumentType Type
    {
      get { return InstrumentType.Swap; }
    }

    ///<summary>
    /// Fixing calendar of fixed leg
    ///</summary>
    public Calendar Calendar { get; set; }

    ///<summary>
    /// Business-day convention of fixed leg
    ///</summary>
    public BDConvention Roll { get; set; }

    ///<summary>
    /// DayCount convention of fixed leg
    ///</summary>
    public DayCount DayCount { get; set; }

    ///<summary>
    /// Payment frequency of fixed leg
    ///</summary>
    public Frequency PayFreq { get; set; }

    ///<summary>
    /// Days to settle
    ///</summary>
    public int SpotDays { get; set; }

    /// <summary>
    /// Payment frequency of floating leg
    /// </summary>
    public Frequency FloatPayFreq { get; set; }

    /// <summary>
    /// ProjectionType of fixed leg
    /// </summary>
    public ProjectionType ProjectionType { get; set; }

    /// <summary>
    /// Gets or sets the reference index.
    /// </summary>
    public CommodityPriceIndex ReferenceIndex { get; set; }

    /// <summary>
    /// Quantity frequency fixed leg
    /// </summary>
    public QuantityFrequency QuantityFreq { get; set; }

    /// <summary>
    /// Quantity frequency floating leg
    /// </summary>
    public QuantityFrequency FloatQuantityFreq { get; set; }

    /// <summary>
    /// Observation rule floating leg
    /// </summary>
    public CommodityPriceObservationRule FloatObservationRule { get; set; }

    /// <summary>
    /// Num obs
    /// </summary>
    public int FloatNumObs { get; set; }

    /// <summary>
    /// Get payment settings
    /// </summary>
    public CommoditySwapPaymentSettings PaymentSettings
    {
      get
      {
        var retVal = new CommoditySwapPaymentSettings();
        if (FloatObservationRule != CommodityPriceObservationRule.None)
          retVal.RecObservationRule = FloatObservationRule;
        if (FloatNumObs != 0)
          retVal.RecNumObs = FloatNumObs;
        return retVal;
      }
    }

    #endregion
  }

  #endregion
}
