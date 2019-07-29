/*
 * RateCurveTerms.cs
 * 
 *  -2013. All rights reserved.
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Curves
{
  #region RateCurveTerms

  ///<summary>
  /// Grouping of common market terms for all assets used in curve calibration
  ///</summary>
  [Serializable]
  public class CurveTerms
  {
    #region Constructors

    ///<summary>
    /// Constructor based on array of asset terms
    ///</summary>
    ///<param name="name">Name</param>
    ///<param name="ccy">Currency</param>
    ///<param name="rateIndex">Reference Index</param>
    ///<param name="assetTerms">Array of asset-specific curve term</param>
    public CurveTerms(string name, Currency ccy, ReferenceIndex rateIndex, IEnumerable<AssetCurveTerm> assetTerms)
      : this(name, ccy, new[] { rateIndex }, assetTerms)
    {
    }

    ///<summary>
    /// Constructor based on array of asset terms
    ///</summary>
    ///<param name="name">Name</param>
    ///<param name="ccy">Currency</param>
    ///<param name="assetTerms">Array of asset-specific curve term</param>
    public CurveTerms(string name, Currency ccy, IEnumerable<AssetCurveTerm> assetTerms)
      : this(name, ccy, null as IList<ReferenceIndex>, assetTerms)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurveTerms"/> class.
    /// </summary>
    /// <param name="name">Name</param>
    /// <param name="ccy">Currency</param>
    /// <param name="targetRateIndices">The target rate indices to fit with.</param>
    /// <param name="assetTerms">The asset terms.</param>
    public CurveTerms(string name, Currency ccy, 
      IList<ReferenceIndex> targetRateIndices,
      IEnumerable<AssetCurveTerm> assetTerms)
    {
      Name = name;
      Ccy = ccy;
      _targetIndices = targetRateIndices;
      _assetCurveTerms = new AssetTermList();
      foreach (var assetTerm in assetTerms)
      {
        if (assetTerm != null && !_assetCurveTerms.ContainsKey(assetTerm.AssetKey))
          _assetCurveTerms.Add(assetTerm);
      }
    }

    ///<summary>
    /// This constructor generates a new instance of RateCurveTerms based on shared value inputs
    ///</summary>
    ///<param name="name">Name</param>
    ///<param name="ccy">Currency</param>
    ///<param name="cal">Calendar, all assets share the same calendar</param>
    ///<param name="rateIndexKey">Get pre-defined index</param>
    ///<param name="bd">BD Convention, all assets share the same convention</param>
    ///<param name="nonSwapdc">Daycount convention for non-swap assets</param>
    ///<param name="spotDays">Days to settle, all assets share the same value</param>
    ///<param name="swapDc">Daycount convention for swap/basis swap asset</param>
    ///<param name="fixedFreq">Fixed leg payment frequency</param>
    ///<param name="floatFreq">Floating leg payment frequency</param>
    ///<param name="bsFreq">Basis swap frequency,
    ///if None, then no basis swap</param>
    /// <remarks>bsFreq is the payment frequency of the swap leg paying the NON-TARGET ReferenceIndex. 
    /// For instance, if we are calibrating a 3M libor curve from 3M-6M basis swaps, bsFreq is the 
    /// payment frequency of the leg paying the 6M libor rate.</remarks>
    public CurveTerms(string name, Currency ccy, Calendar cal, object rateIndexKey, BDConvention bd, DayCount nonSwapdc,
                          int spotDays, DayCount swapDc, Frequency fixedFreq, Frequency floatFreq, Frequency bsFreq)
    {
      Name = name;
      Ccy = ccy;
      if (rateIndexKey is string)
        _targetIndices = new[] {StandardReferenceIndices.Create((string)rateIndexKey)};
      else if (rateIndexKey is ReferenceIndex)
        _targetIndices = new[] { (ReferenceIndex)rateIndexKey };
      else
        throw new ArgumentException("rateIndexKey Type is not supported.");
      _assetCurveTerms = new AssetTermList();
      _assetCurveTerms.Add(new AssetRateCurveTerm(InstrumentType.FUNDMM, spotDays, bd, nonSwapdc, cal,
                                                  Frequency.None, ProjectionType.None, null));
      _assetCurveTerms.Add(new AssetRateCurveTerm(InstrumentType.MM, spotDays, bd, nonSwapdc, cal,
                                                  Frequency.None, ProjectionType.None, null));
      _assetCurveTerms.Add(new AssetRateCurveTerm(InstrumentType.FUT, spotDays, bd, nonSwapdc, cal,
                                                  Frequency.None, ProjectionType.SimpleProjection, ReferenceIndex));
      _assetCurveTerms.Add(new SwapAssetCurveTerm(spotDays, bd, swapDc, cal, fixedFreq, Frequency.None,
                                                  ProjectionType.SimpleProjection, floatFreq,
                                                  ReferenceIndex.IndexTenor.ToFrequency(), CompoundingConvention.None, ReferenceIndex));
      if (bsFreq != Frequency.None)
      {
        _assetCurveTerms.Add(new BasisSwapAssetCurveTerm(spotDays, cal, ProjectionType.SimpleProjection, floatFreq, ReferenceIndex.IndexTenor.ToFrequency(), CompoundingConvention.None,
                                                         ReferenceIndex, ProjectionType.SimpleProjection, bsFreq, fixedFreq, CompoundingConvention.None, null, true));
        //details of other leg are taken from terms of Swap in projection curve AssetCurveTerms.
      }
    }

    #endregion

    #region Properties

    ///<summary>
    /// Name of the curve terms
    ///</summary>
    public string Name { get; set; }

    ///<summary>
    /// Currency
    ///</summary>
    public Currency Ccy { get; set; }

    ///<summary>
    /// Reference rate index
    ///</summary>
    public ReferenceIndex ReferenceIndex
    {
      get
      {
        return _targetIndices != null
          ? _targetIndices.FirstOrDefault()
          : null;
      }
    }

    ///<summary>
    ///  List of the reference rate indices for mixed fitting.
    ///</summary>
    public IList<ReferenceIndex> ReferenceIndices
    {
      get { return _targetIndices; }
    }

    ///<summary>
    /// Collection of market information for all assets involved in curve calibration
    ///</summary>
    public AssetTermList AssetTerms
    {
      get { return _assetCurveTerms; }
    }

    /// <summary>
    /// Target index ProjectionType
    /// </summary>
    public ProjectionType SwapProjectionType
    {
      get
      {
        SwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.Swap, out t) && t.ProjectionType != ProjectionType.None)
        {
          return t.ProjectionType;
        }
        return ProjectionType.SimpleProjection;
      }
      set
      {
        SwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.Swap, out t))
        {
          t.ProjectionType = value;
        }
      }
    }

    /// <summary>
    /// Target index basis swap ProjectionType
    /// </summary>
    public ProjectionType BasisSwapProjectionType
    {
      get
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t))
        {
          if (t.RecProjectionType != ProjectionType.None)
            return t.RecProjectionType;
        }
        return ProjectionType.SimpleProjection;
      }
      set
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t))
        {
          t.RecProjectionType = value;
        }
      }
    }

    /// <summary>
    /// Projeciton Index basis swap ProjectionType
    /// </summary>
    public ProjectionType BasisSwapOtherProjectionType
    {
      get
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t) && t.PayProjectionType != ProjectionType.None)
        {
          return t.PayProjectionType;
        }
        return ProjectionType.SimpleProjection;
      }
      set
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t))
        {
          t.PayProjectionType = value;
        }
      }
    }

    /// <summary>
    /// Basis swap CompoundingConvention
    /// </summary>
    public CompoundingConvention BasisSwapCompoundingConvention
    {
      get
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t) && t.RecCompoundingConvention != CompoundingConvention.None)
        {
          return t.RecCompoundingConvention;
        }
        return CompoundingConvention.None;
      }
      set
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t))
        {
          t.RecCompoundingConvention = value;
        }
      }
    }

    /// <summary>
    /// Spread on target index
    /// </summary>
    public bool SpreadOnTargetIndex
    {
      get
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t))
        {
          return t.SpreadOnReceiver;
        }
        return true;
      }
      set
      {
        BasisSwapAssetCurveTerm t;
        if (_assetCurveTerms.TryGetValue(InstrumentType.BasisSwap, out t))
        {
          t.SpreadOnReceiver = value;
        }
      }
    }

    #endregion Properties

    #region Data

    private readonly AssetTermList _assetCurveTerms;
    private readonly IList<ReferenceIndex> _targetIndices;

    #endregion Data
  }

  #endregion

  #region Asset Term List

  /// <summary>
  ///   Collection of asset term definitions.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class AssetTermList : Dictionary<string, AssetCurveTerm>
  {
    /// <summary>
    ///  Constructor
    /// </summary>
    public AssetTermList()
    {}

    /// <summary>
    /// Deserialisation constructor
    /// </summary>
    protected AssetTermList(SerializationInfo info, StreamingContext ctx) : base(info, ctx) { }

    /// <summary>
    /// Adds the specified type.
    /// </summary>
    /// <param name="term">The term.</param>
    /// <remarks></remarks>
    internal void Add(AssetCurveTerm term)
    {
      Add(term.AssetKey, term);
    }

    /// <summary>
    /// Tries the get value.
    /// </summary>
    /// <param name="type">The asset type.</param>
    /// <param name="term">The asset term.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public bool TryGetValue<T>(InstrumentType type, out T term) where T : AssetCurveTerm
    {
      AssetCurveTerm t;
      if (TryGetValue(type.ToString(), out t))
      {
        term = t as T;
        if (term != null)
          return true;
      }
      term = null;
      return false;
    }

    /// <summary>
    /// Get value and make conversion
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="term"></param>
    /// <returns></returns>
    public bool TryGetValue<T>(string key, out T term) where T : AssetCurveTerm
    {
      AssetCurveTerm t;
      if (base.TryGetValue(key, out t))
      {
        term = t as T;
        if (term != null)
          return true;
      }
      term = null;
      return false;
    }



    /// <summary>
    /// Determines whether the specified type contains key.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> if the specified type contains key; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    internal bool ContainsKey(InstrumentType type)
    {
      return ContainsKey(type.ToString());
    }

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <returns>The value associated with the specified key. If the specified key is not found, a get
    /// operation throws a <see cref="T:System.Collections.Generic.KeyNotFoundException"/>, and a set
    /// operation creates a new element with the specified key.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="type"/> is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">
    ///   The property is retrieved and <paramref name="type"/> does not exist in the collection.</exception>
    internal AssetCurveTerm this[InstrumentType type]
    {
      get { return base[type.ToString()]; }
    }
  }

  #endregion

  #region AssetCurveTerm

  /// <summary>
  /// Common market term for a type of asset used in the curve calibration
  /// </summary>
  [Serializable]
  public abstract class AssetCurveTerm : BaseEntityObject
  {
    ///<summary>
    /// Type of asset
    ///</summary>
    public abstract InstrumentType Type { get; }

    /// <summary>
    /// Gets or sets the asset key.
    /// </summary>
    /// <value>The asset key.</value>
    /// <remarks></remarks>
    public string AssetKey
    {
      get { return _key ?? Type.ToString(); }
      set { _key = value; }
    }

    private string _key;
  }


  #endregion

  #region AssetRateCurveTerm

  /// <summary>
  /// Asset rate curve term for simple rate products
  /// </summary>
  [Serializable]
  public class AssetRateCurveTerm : AssetCurveTerm
  {
    #region Constructor

    ///<summary>
    /// Constructor to create the term of an asset used in curve calibration
    ///</summary>
    ///<param name="type">Instrument type</param>
    ///<param name="spotDays">Days to settle</param>
    ///<param name="bd">Business day convention</param>
    ///<param name="dc">Day count convention</param>
    ///<param name="cal">Calendar</param>
    ///<param name="freq">Payment frequency</param>
    ///<param name="projectionType">projection type</param>
    ///<param name="referenceIndex">reference index</param>
    public AssetRateCurveTerm(InstrumentType type, int spotDays, BDConvention bd, DayCount dc, Calendar cal, Frequency freq, ProjectionType projectionType,
                              ReferenceIndex referenceIndex)
    {
      _type = type;
      SpotDays = spotDays;
      BDConvention = bd;
      DayCount = dc;
      Calendar = cal;
      PayFreq = freq;
      ReferenceIndex = referenceIndex;
      ProjectionType = (projectionType != ProjectionType.None) ? projectionType : ProjectionType.SimpleProjection; 
    }

    #endregion

    #region Properties

    /// <summary>
    /// Product type
    /// </summary>
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
    public BDConvention BDConvention { get; set; }

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
    public ReferenceIndex ReferenceIndex { get; set; }

    #endregion

    #region Data

    private InstrumentType _type;

    #endregion
  }

  #endregion

  #region Rate Futures Market Term

  /// <summary>
  /// Terms for various rate futures
  /// </summary>
  [Serializable]
  public class RateFuturesCurveTerm : AssetRateCurveTerm
  {
    #region Constructor

    /// <summary>
    /// Constructor to create the term of a futures used in curve calibration
    /// </summary>
    /// <param name="bd">Business day convention</param>
    /// <param name="dc">Day count convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="futureType">Type of the future.</param>
    /// <param name="projectionType">Type of the projection.</param>
    public RateFuturesCurveTerm(BDConvention bd, DayCount dc, Calendar cal,
      Tenor tenor, Currency currency,
      RateFutureType futureType, ProjectionType projectionType)
      : base(InstrumentType.FUT, 0, bd, dc, cal, Frequency.None, projectionType, null)
    {
      Tenor = tenor.Units == TimeUnit.None ? new Tenor(3, TimeUnit.Months) : tenor;
      Currency = currency;
      RateFutureType = futureType;
    }

    #endregion

    #region Properties
    /// <summary>
    /// Yield convention
    /// </summary>
    public Tenor Tenor { get; private set; }

    /// <summary>
    /// Yield convention
    /// </summary>
    public Currency Currency { get; private set; }

    /// <summary>
    /// Yield convention
    /// </summary>
    public RateFutureType RateFutureType { get; private set; }
    #endregion
  }

  #endregion

  #region SwapAssetCurveTerm

  /// <summary>
  /// Terms for fixed-floating swap
  /// </summary>
  [Serializable]
  public class SwapAssetCurveTerm : AssetCurveTerm
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="bd">Business day convention of fixed leg</param>
    /// <param name="dc">Daycount of fixed leg</param>
    /// <param name="cal">Calendar of fixed leg</param>
    /// <param name="freq">Frequency of fixed leg</param>
    /// <param name="compoundingFreq">Compounding frequency of fixed leg(for zero coupon swaps)</param>
    /// <param name="projectionType">Projection type for floating payments</param>
    /// <param name="floatFreq">Floating frequency</param>
    /// <param name="floatCompoundingFreq">Floating leg compounding frequency</param>
    /// <param name="floatCompoundingConv">Floating leg compounding convention</param>
    /// <param name="referenceIndex">Reference index</param>
    public SwapAssetCurveTerm(int spotDays, BDConvention bd, DayCount dc, Calendar cal, Frequency freq, Frequency compoundingFreq,
                              ProjectionType projectionType, Frequency floatFreq, Frequency floatCompoundingFreq, CompoundingConvention floatCompoundingConv,
                              ReferenceIndex referenceIndex)
    {
      SpotDays = spotDays;
      BDConvention = bd;
      DayCount = dc;
      Calendar = cal;
      PayFreq = freq;
      CompoundingFreq = compoundingFreq;
      ProjectionType = (projectionType != ProjectionType.None) ? projectionType : ProjectionType.SimpleProjection; 
      FloatPayFreq = floatFreq;
      FloatCompoundingConvention = floatCompoundingConv;
      FloatCompoundingFreq = floatCompoundingFreq;
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
    public BDConvention BDConvention { get; set; }

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
    public ReferenceIndex ReferenceIndex { get; set; }

    /// <summary>
    /// Compounding Convention of floaing leg
    /// </summary>
    public CompoundingConvention FloatCompoundingConvention { get; set; }

    /// <summary>
    /// Compounding frequency of leg paying target index
    /// </summary>
    public Frequency FloatCompoundingFreq { get; set; }


    /// <summary>
    /// Compounding frequency of fixed leg(for zero coupon instruments)
    /// </summary>
    public Frequency CompoundingFreq { get; set; }

    #endregion
  }

  #endregion

  #region BasisSwapAssetCurveTerm

  /// <summary>
  /// Terms for floating-floating swap
  /// </summary>
  [Serializable]
  public class BasisSwapAssetCurveTerm : AssetCurveTerm
  {
    #region Constructor

    /// <summary>
    /// Constructor of a basis swap that receives target index and pays projection index 
    /// </summary>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="spotCalendar">Calendar for spot date calculation</param>
    /// <param name="recProjectionType">Projection type of leg paying reference index</param>
    /// <param name="recFreq">Payment frequency of leg paying reference index</param>
    /// <param name="recCompoundingFreq">Compounding frequency of leg paying reference index</param>
    /// <param name="recCompoundingConvention">Compounding convention of leg paying reference index</param>
    /// <param name="receiverIndex">Reference index</param>
    /// <param name="payProjectionType">Projection type of leg paying other index</param>
    /// <param name="payFreq">Payment frequency of leg paying other index</param>
    /// <param name="payCompoundingFreq">Compounding frequency of leg paying other index</param>
    /// <param name="otherCompoundingConv">Compounding convention of leg paying other index</param>
    /// <param name="payerIndex">Other index</param>
    /// <param name="spreadOnReceiver">If true basis spread is paid on leg paying reference index, otherwise spread is paid on leg paying other index</param>
    public BasisSwapAssetCurveTerm(int spotDays, Calendar spotCalendar, ProjectionType recProjectionType, Frequency recFreq, Frequency recCompoundingFreq,
                                   CompoundingConvention recCompoundingConvention, ReferenceIndex receiverIndex,
                                   ProjectionType payProjectionType, Frequency payFreq, Frequency payCompoundingFreq,
                                   CompoundingConvention otherCompoundingConv,
                                   ReferenceIndex payerIndex, bool spreadOnReceiver)
    {
      SpotDays = spotDays;
      SpotCalendar = spotCalendar;
      RecFreq = recFreq;
      PayFreq = payFreq;
      RecProjectionType = (recProjectionType != ProjectionType.None) ? recProjectionType : ProjectionType.SimpleProjection;
      PayProjectionType = (payProjectionType != ProjectionType.None) ? payProjectionType : ProjectionType.SimpleProjection;
      RecCompoundingFreq = recCompoundingFreq;
      PayCompoundingFreq = payCompoundingFreq;
      RecCompoundingConvention = recCompoundingConvention;
      PayCompoundingConvention = otherCompoundingConv;
      ReceiverIndex = receiverIndex;
      PayerIndex = payerIndex;
      SpreadOnReceiver = spreadOnReceiver;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Product type
    /// </summary>
    public override InstrumentType Type
    {
      get { return InstrumentType.BasisSwap; }
    }

    /// <summary>
    /// Calendar
    /// </summary>
    public Calendar SpotCalendar { get; set; }

    ///<summary>
    /// Days to settle the asset
    ///</summary>
    public int SpotDays { get; set; }

    ///<summary>
    /// Payment frequency of leg paying ReferenceIndex
    ///</summary>
    public Frequency RecFreq { get; set; }

    /// <summary>
    /// Payment frequency of leg paying OtherIndex
    /// </summary>
    public Frequency PayFreq { get; set; }

    /// <summary>
    /// ReferenceIndex ProjectionType
    /// </summary>
    public ProjectionType RecProjectionType { get; set; }

    /// <summary>
    /// OtherIndex ProjectionType
    /// </summary>
    public ProjectionType PayProjectionType { get; set; }

    /// <summary>
    /// Reference index.
    /// </summary>
    public ReferenceIndex ReceiverIndex { get; set; }

    /// <summary>
    /// Other index
    /// </summary>
    public ReferenceIndex PayerIndex { get; set; }

    /// <summary>
    /// Compounding Convention of leg paying reference index
    /// </summary>
    public CompoundingConvention RecCompoundingConvention { get; set; }

    /// <summary>
    /// Compounding Convention of leg paying other index
    /// </summary>
    public CompoundingConvention PayCompoundingConvention { get; set; }

    /// <summary>
    /// Compounding frequency of leg paying reference index
    /// </summary>
    public Frequency RecCompoundingFreq { get; set; }

    /// <summary>
    /// Compounding frequency of leg paying other index
    /// </summary>
    public Frequency PayCompoundingFreq { get; set; }

    /// <summary>
    /// Spread on target index
    /// </summary>
    public bool SpreadOnReceiver { get; set; }

    #endregion
  }

  #endregion
  
  #region InflationSwapAssetCurveTerm
  
  /// <summary>
  /// Terms for Inflation swaps
  /// </summary>
  [Serializable]
  public class InflationSwapAssetCurveTerm : SwapAssetCurveTerm
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="bd">Business day convention of fixed leg</param>
    /// <param name="dc">Daycount of fixed leg</param>
    /// <param name="cal">Calendar of fixed leg</param>
    /// <param name="freq">Frequency of fixed leg</param>
    /// <param name="compoundingFreq">Compounding frequency of fixed leg(for zero coupon swaps)</param>
    /// <param name="projectionType">Projection type for floating payments</param>
    /// <param name="floatFreq">Floating frequency</param>
    /// <param name="floatCompoundingFreq">Floating leg compounding frequency</param>
    /// <param name="floatCompoundingConv">Floating leg compounding convention</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="indexationMethod">Indexation method</param>
    /// <param name="adjustPeriod">Adjust period (the opposite to accrue on cycle)</param>
    /// <param name="adjustLast">Adjust last accrual date to payment date</param>
    public InflationSwapAssetCurveTerm(int spotDays, BDConvention bd, DayCount dc, Calendar cal, Frequency freq, Frequency compoundingFreq,
                              ProjectionType projectionType, Frequency floatFreq, Frequency floatCompoundingFreq, CompoundingConvention floatCompoundingConv,
                              ReferenceIndex referenceIndex, IndexationMethod indexationMethod, bool adjustPeriod, bool adjustLast)
          : base(spotDays, bd, dc, cal, freq, compoundingFreq, projectionType, floatFreq, floatCompoundingFreq, floatCompoundingConv, referenceIndex)
    {
      IndexationMethod = indexationMethod;
      AdjustPeriod = adjustPeriod;
      AdjustLast = adjustLast;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Indexation method
    /// </summary>
    public IndexationMethod IndexationMethod { get; set; }

    /// <summary>
    /// Adjust period (accrue on cycle)
    /// </summary>
    public bool AdjustPeriod { get; set; }

    /// <summary>
    /// Adjust last payment date
    /// </summary>
    public bool AdjustLast { get; set; }
    #endregion
  }
  
  #endregion
  
  #region InflationBondAssetCurveTerm

  /// <summary>
  /// Common market terms for Inflation indexed treasury bond
  /// </summary>
  [Serializable]
  public class InflationBondAssetCurveTerm : AssetRateCurveTerm
  {
    /// <summary>
    /// 
    /// </summary>
    ///<summary>
    /// Constructor to create the term of an asset used in curve calibration
    /// </summary>
    /// <param name="quotingConvention">Quoting conventions</param>
    /// <param name="bondType">Bond type</param>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="bd">Business day convention</param>
    /// <param name="dc">Day count convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="projectionType">projection type</param>
    /// <param name="spreadType">Spread type</param>
    /// <param name="referenceIndex">reference index</param>
    /// <param name="compoundingFrequency">compounding frequency</param>
    /// <param name="compoundingConvention">compounding convention</param>
    /// <param name="indexationMethod">indexation method</param>
    /// <param name="flooredNotional">floor final notional repayment</param>
    public InflationBondAssetCurveTerm(
      QuotingConvention quotingConvention,
      BondType bondType,
      int spotDays,
      BDConvention bd,
      DayCount dc,
      Calendar cal,
      Frequency freq,
      SpreadType spreadType,
      ProjectionType projectionType,
      Frequency compoundingFrequency,
      CompoundingConvention compoundingConvention,
      IndexationMethod indexationMethod,
      bool flooredNotional,
      InflationIndex referenceIndex)
      : base(InstrumentType.Bond, spotDays, bd, dc, cal, freq, projectionType, referenceIndex)
    {
      QuotingConvention = quotingConvention;
      BondType = bondType;
      SpreadType = spreadType;
      CompoundingFreq = compoundingFrequency;
      CompoundingConvention = compoundingConvention;
      IndexationMethod = indexationMethod;
      FlooredNotional = flooredNotional;
    }

    #region Properties

    /// <summary>
    /// Spread type for floating bonds 
    /// </summary>
    public SpreadType SpreadType { get; set; }

    /// <summary>
    /// Compounding frequency of fixed leg
    /// </summary>
    public CompoundingConvention CompoundingConvention { get; set; }

    /// <summary>
    /// Compounding frequency of fixed leg
    /// </summary>
    public Frequency CompoundingFreq { get; set; }

    /// <summary>
    /// Indexation method
    /// </summary>
    public IndexationMethod IndexationMethod { get; set; }

    /// <summary>
    /// Floored principal repayment
    /// </summary>
    public bool FlooredNotional { get; set; }

    /// <summary>
    /// Quoting convention
    /// </summary>
    public QuotingConvention QuotingConvention { get; set; }

    /// <summary>
    /// Bond type
    /// </summary>
    public BondType BondType { get; set; }

    #endregion

  }

  #endregion
  
  #region YoYSwapAssetCurveTerm
  /// <summary>
  /// Terms of YoY inflation swap
  /// </summary>
  [Serializable]
  public class YoYSwapAssetCurveTerm : InflationSwapAssetCurveTerm
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="spotDays">Days to settle</param>
    /// <param name="bd">Business day convention of fixed leg</param>
    /// <param name="dc">Daycount of fixed leg</param>
    /// <param name="cal">Calendar of fixed leg</param>
    /// <param name="freq">Frequency of fixed leg</param>
    /// <param name="compoundingFreq">Compounding frequency of fixed leg(for zero coupon swaps)</param>
    /// <param name="projectionType">Projection type for floating payments</param>
    /// <param name="floatFreq">Floating frequency</param>
    /// <param name="floatCompoundingFreq">Floating leg compounding frequency</param>
    /// <param name="floatCompoundingConv">Floating leg compounding convention</param>
    /// <param name="inflationIndex">Inflation index</param>
    /// <param name="inflationRateTenor">YoY tenor</param>
    /// <param name="indexationMethod">Indexation method</param>
    /// <param name="adjustPeriod">Adjust period</param>
    /// <param name="adjustLast">Adjust last accrual date to payment date</param>
    public YoYSwapAssetCurveTerm(int spotDays, BDConvention bd, DayCount dc, Calendar cal, Frequency freq, Frequency compoundingFreq,
                                 ProjectionType projectionType, Frequency floatFreq, Frequency floatCompoundingFreq, CompoundingConvention floatCompoundingConv,
                                 InflationIndex inflationIndex, Tenor inflationRateTenor, IndexationMethod indexationMethod, bool adjustPeriod, bool adjustLast)
      : base(spotDays, bd, dc, cal, freq, compoundingFreq, projectionType, floatFreq, floatCompoundingFreq, floatCompoundingConv, 
          inflationIndex, indexationMethod, adjustPeriod, adjustLast)
    {
      InflationRateTenor = inflationRateTenor;
    }

    #endregion

    #region Properties
    /// <summary>
    /// YoY tenor
    /// </summary>
    public Tenor InflationRateTenor { get; private set; }

  #endregion
  }

  #endregion
}

