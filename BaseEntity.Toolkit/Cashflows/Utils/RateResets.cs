using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{

  #region RateResets

  /// <summary>
  /// Container Class for representing Rate Resets
  /// 
  /// The user can either specify dates with rates, or just set the current and next reset
  /// </summary>
  [Serializable]
  public class RateResets : BaseEntityObject, IEnumerable<RateReset>
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RateResets"/> class.
    /// </summary>
    public RateResets()
    {
      AllResets = new SortedDictionary<Dt, double>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateResets"/> class.
    /// </summary>
    /// <param name="resets">The list of resets.</param>
    public RateResets(IEnumerable<RateReset> resets)
    {
      AllResets = (resets == null)
                    ? new SortedDictionary<Dt, double>()
                    : new SortedDictionary<Dt, double>(resets.ToDictionary(r => r.Date, r => r.Rate));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateResets"/> class.
    /// </summary>
    /// <param name="current">Current fixing</param>
    /// <param name="next">Fixing for next coupon period</param>
    public RateResets(double current, double next)
    {
      CurrentReset = current;
      NextReset = next;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateResets"/> class with a single rate reset.
    /// </summary>
    /// <param name="date">date of reset</param>
    /// <param name="rate">reset rate</param>
    public RateResets(Dt date, double rate)
    {
      AllResets = new SortedDictionary<Dt, double> {{date, rate}};
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateResets"/> class.
    /// </summary>
    /// <param name="resets">Dictionary of (Dt,reset) pairs</param>
    public RateResets(IDictionary<Dt, double> resets)
    {
      AllResets = (resets == null)
                    ? new SortedDictionary<Dt, double>()
                    : new SortedDictionary<Dt, double>(resets);
    }

    #endregion Constructors

    #region Properties

    private SortedDictionary<Dt, double> allResets_;

    /// <summary>
    /// Test if empty
    /// </summary>
    public bool HasAllResets
    {
      get { return allResets_ != null && allResets_.Count > 0; }
    }

    /// <summary>
    /// Fixing dictionary
    /// </summary>
    public SortedDictionary<Dt, double> AllResets
    {
      get
      {
        // make sure it is not null.
        allResets_ = allResets_ ?? new SortedDictionary<Dt, double>();
        return allResets_;
      }
      set
      {
        allResets_ = value;
      }
    }

    /// <summary>
    /// Reset for the current period (reset date &lt; asof)
    /// </summary>
    public double CurrentReset { get; set; }

    /// <summary>
    /// True if has current reset
    /// </summary>
    public bool HasCurrentReset
    {
      get { return !Double.IsNaN(CurrentReset) && !CurrentReset.ApproximatelyEqualsTo(0.0); }
    }

    /// <summary>
    /// True if has next reset
    /// </summary>
    public bool HasNextReset
    {
      get { return !Double.IsNaN(NextReset) && !NextReset.ApproximatelyEqualsTo(0.0); }
    }

    /// <summary>
    /// Allow the user to easily set just the next upcoming projected rate. (reset date >= asof)
    /// </summary>
    public double NextReset { get; set; }

    /// <summary>
    /// Gets the count of rate resets.
    /// </summary>
    /// <remarks></remarks>
    public virtual int Count { get { return AllResets.Count; } }

    #endregion

    #region Methods

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks></remarks>
    public override void Validate(ArrayList errors)
    {
      if(AllResets==null) return;
      foreach (var date in AllResets.Keys)
      {
        if (!date.IsValid())
          InvalidValue.AddError(errors, this, "Date",
            String.Format("Rate effective date {0} is invalid", date));
      }
    }

    /// <summary>
    /// Test wheter reset for a give reset date is present 
    /// </summary>
    /// <param name="reset">Reset date</param>
    /// <returns>True if fixing for the given reset date is present</returns>
    public bool HasRate(Dt reset)
    {
      return (AllResets != null && AllResets.ContainsKey(reset));
    }


    /// <summary>
    /// Get reset fixing
    /// </summary>
    /// <param name="reset">Reset date</param>
    /// <param name="found">Indicates whether historical reset is present</param>
    /// <returns>fixing</returns>
    public double GetRate(Dt reset, out bool found)
    {
      if (AllResets == null)
      {
        found = false;
        return 0.0;
      }
      double retVal;
      found = AllResets.TryGetValue(reset, out retVal);
      return retVal;
    }


    /// <summary>
    /// Convert to list
    /// </summary>
    /// <returns>RateReset list</returns>
    public virtual IList<RateReset> ToList()
    {
      return AllResets.Select(d => new RateReset(d.Key, d.Value)).ToList();
    }

    /// <summary>
    /// Adds the specified RateReset item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <remarks></remarks>
    public virtual void Add(RateReset item)
    {
      AllResets.Add(item.Date, item.Rate);
    }

    /// <summary>
    /// Adds the specified pair of date and value.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="value">The value.</param>
    public void Add(Dt date, double value)
    {
      AllResets.Add(date, value);
    }

    #endregion Methods

    #region Nested type: ResetInfo

    /// <summary>
    /// 
    /// </summary>
    public class ResetInfo
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="date">Reset date</param>
      /// <param name="rate">Fixing </param>
      /// <param name="state">RateResetState</param>
      public ResetInfo(Dt date, double rate, RateResetState state)
      {
        Date = date;
        Rate = rate;
        State = state;
        resetCompounds_ = null;
      }

      ///<summary>
      /// List of compounding reset infos
      ///</summary>
      public List<ResetInfo> ResetInfos
      {
        get
        {
          if (HasCompoundsInfo)
            return resetCompounds_;
          return new List<ResetInfo> {new ResetInfo(Date, Rate, State)};

        }
        set { resetCompounds_ = value; }
      }

      ///<summary>
      /// Flag to indicate whether the reset comes from compounding reset information
      ///</summary>
      public bool HasCompoundsInfo
      {
        get { return resetCompounds_ != null; }
      }
      /// <summary>
      /// Reset date
      /// </summary>
      public Dt Date { get; set; }

      /// <summary>
      /// Rate
      /// </summary>
      public double Rate { get; set; }

      /// <summary>
      /// Rate reset state
      /// </summary>
      public RateResetState State { get; set; }

      /// <summary>
      /// Period accrual start
      /// </summary>
      public Dt AccrualStart { get; set; }

      /// <summary>
      /// Period accrual end
      /// </summary>
      public Dt AccrualEnd { get; set; }

      /// <summary>
      /// Payment date
      /// </summary>
      public Dt PayDt { get; set; }

      /// <summary>
      /// Previous payment date
      /// </summary>
      public Dt PreviousPayDt { get; set; }

      /// <summary>
      /// Start date
      /// </summary>
      public Dt StartDt { get; set; }

      /// <summary>
      /// Index tenor
      /// </summary>
      public Tenor IndexTenor { get; set; }

      /// <summary>
      /// Frequency
      /// </summary>
      public Frequency Frequency { get; set; }

      /// <summary>
      /// True if the reset state is well defined
      /// </summary>
      /// <returns></returns>
      public bool IsValid()
      {
        return
          !(State == RateResetState.Missing || State == RateResetState.None);
      }

      private List<ResetInfo> resetCompounds_;
      ///<summary>
      /// Computed effective reset rate
      ///</summary>
      public double? EffectiveRate { get; set; }
    }

    #endregion

    #region IEnumerable<RateReset> Members

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.</returns>
    /// <remarks></remarks>
    public virtual IEnumerator<RateReset> GetEnumerator()
    {
      return AllResets.Select(d => new RateReset(d.Key,d.Value)).GetEnumerator();
    }

    #endregion

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator()
    {
      return AllResets.Select(d => new RateReset(d.Key, d.Value)).GetEnumerator();
    }

    #endregion
  }

  #endregion

  /// <summary>
  /// Rate reset states
  /// </summary>
  public enum RateResetState
  {
    /// <summary>
    /// None specified
    /// </summary>
    None = 0,
    /// <summary>
    /// Historical observation found for the raw index fixing
    /// </summary>
    ObservationFound = 1,
    /// <summary>
    /// Historical reset missing
    /// </summary>
    Missing = 2,
    /// <summary>
    /// Reset projected off forward term structure
    /// </summary>
    IsProjected = 3,
    /// <summary>
    /// All inclusive reset found for the all-inclusive coupon reset (fixing + spread) 
    /// </summary>
    ResetFound
  }

  #region RateResets adapter

  /// <summary>
  ///  Adapter adding some IList&lt;RateReset&gt; interfaces to RateResets object,
  ///  allowing it to be treated as a list.
  /// </summary> 
  /// <exclude>For internal use only.</exclude>

  /// <remarks>
  ///  Warning: with this class, all the modfications through <c>IList&lt;RateReset&gt;</c>
  ///  automatically goes to the underlying dictionary, RateResets.AllResets.
  ///  But the vice versa does not hold. The reverse synchronization happens only when
  ///  the method <c>RateResets.ToList()</c> is called.
  /// </remarks>
  [Serializable]
  internal class RateResetsList : RateResets, IList<RateReset>
  {
    #region Data
    private Dt effective_;
    private bool backwardCompatible_;
    private IList<RateReset> list_;
    #endregion Data

    #region construcrors and converters
    private RateResetsList(double currentRate, double nextRate)
      : base(currentRate, nextRate)
    {
    }

    private RateResetsList(IEnumerable<RateReset> resets)
      : base(resets)
    {
    }

    /// <summary>
    /// Creates a RateResetList object from the specified current rate.
    /// </summary>
    /// <param name="currentRate">The current rate.</param>
    /// <param name="nextRate">The next rate.</param>
    /// <param name="effective">The effective.</param>
    /// <param name="backwardCompatible">if set to <c>true</c> [backward compatible].</param>
    /// <returns></returns>
    /// <remarks></remarks>
    internal static RateResetsList Create(double currentRate, double nextRate,
      Dt effective, bool backwardCompatible)
    {
      var list = new RateResetsList(currentRate, nextRate)
        {
          effective_ = effective,
          backwardCompatible_ = backwardCompatible
        };
      return list;
    }

    /// <summary>
    /// Converts the specified to RateResetList.
    /// </summary>
    /// <param name="other">The object to convert.  It must be one of the following type:
    ///  1 RateResetsList: the object itself is returned;
    ///  2 RateRasets object: a new RateResetsList is constructed by copy the underlying data members;
    ///  3 IList&lt;RateReset&gt;: a new RateResetsList is created and initialized with the list.</param>
    /// <param name="effective">The effective.</param>
    /// <param name="backwardCompatible">if set to <c>true</c>, use the backward compatible mode,
    ///  and the current rate is included as the reset on effective data; otherwise, only those in
    ///  AllResets dictionary are included.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    internal static RateResetsList Convert(object other, Dt effective, bool backwardCompatible)
    {
      if (other is RateResetsList)
        return (RateResetsList) other;
      RateResetsList list;
      RateResets rr;
      IList<RateReset> lrr;
      if (other == null)
      {
        list = new RateResetsList(Double.NaN, Double.NaN);
      }
      else if ((rr = other as RateResets) != null)
      {
        list = new RateResetsList(Double.NaN, Double.NaN);
        list.AllResets = rr.AllResets;
        list.CurrentReset = rr.CurrentReset;
        list.NextReset = rr.NextReset;
      }
      else if ((lrr = other as IList<RateReset>) != null)
      {
        list = new RateResetsList(lrr);
      }
      else
      {
        throw new ToolkitException(String.Format(
          "Cannot convert {0} to {1}", other.GetType(),
          typeof (RateResetsList)));
      }
      list.effective_ = effective;
      list.backwardCompatible_ = backwardCompatible;
      return list;
    }
    #endregion

    #region Methods
    private void UpdateList()
    {
      if (list_ != null) return;
      list_ = backwardCompatible_ ? this.ToBackwardCompatibleList(effective_) : base.ToList();
    }

    /// <summary>
    /// Convert to list
    /// </summary>
    /// <returns>RateReset list</returns>
    /// <remarks></remarks>
    public override IList<RateReset> ToList()
    {
      list_ = null; // reset to reflect any modfication in the underlying dictionary.
      return this;
    }

    internal double ResetAt(Dt date)
    {
      double lastReset = 0;
      if (AllResets.Count == 0)
      {
        if(HasCurrentReset)
          lastReset= CurrentReset;
      }
      else
      {
        foreach (var p in AllResets)
        {
          if (p.Key <= date)
            lastReset = p.Value;
          else
            break;
        }
      }
      return lastReset;
    }
    #endregion

    #region IList<RateReset> Members

    int IList<RateReset>.IndexOf(RateReset item)
    {
      UpdateList();
      return list_ == null ? -1 : list_.IndexOf(item);
    }

    void IList<RateReset>.Insert(int index, RateReset item)
    {
      throw new NotImplementedException();
    }

    void IList<RateReset>.RemoveAt(int index)
    {
      UpdateList();
      if (list_ == null) return;
      var dt = list_[index].Date;
      AllResets.Remove(dt);
      list_ = null; // reset after changes.
    }

    RateReset IList<RateReset>.this[int index]
    {
      get
      {
        UpdateList();
        return list_[index];
      }
      set
      {
        throw new NotImplementedException();
      }
    }

    #endregion

    #region ICollection<RateReset> Members

    /// <summary>
    /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
    /// </summary>
    /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
    /// <exception cref="T:System.NotSupportedException">
    /// The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
    ///   </exception>
    /// <remarks></remarks>
    public override void Add(RateReset item)
    {
      if (item != null)
        AllResets.Add(item.Date, item.Rate);
      list_ = null;
    }

    void ICollection<RateReset>.Clear()
    {
      AllResets.Clear();
      list_ = null;
    }

    bool ICollection<RateReset>.Contains(RateReset item)
    {
      if (item == null)
        return false;
      if (backwardCompatible_ && item.Date == effective_)
      {
        return item.Rate == CurrentReset;
      }
      double rate;
      if (!AllResets.TryGetValue(item.Date, out rate))
      {
        return false;
      }
      return rate == item.Rate;
    }

    void ICollection<RateReset>.CopyTo(RateReset[] array, int arrayIndex)
    {
      UpdateList();
      if (list_ != null) list_.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
    /// </summary>
    /// <returns>
    /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
    ///   </returns>
    /// <remarks></remarks>
    public int Count
    {
      get
      {
        UpdateList();
        return list_ == null ? 0 : list_.Count;
      }
    }

    bool ICollection<RateReset>.IsReadOnly
    {
      get { return ((ICollection<KeyValuePair<Dt, double>>) AllResets).IsReadOnly; }
    }

    bool ICollection<RateReset>.Remove(RateReset item)
    {
      double rate;
      if (!AllResets.TryGetValue(item.Date, out rate))
      {
        return false;
      }
      if (rate == item.Rate)
        AllResets.Remove(item.Date);
      list_ = null;
      return true;
    }

    #endregion

    #region IEnumerable<RateReset> Members

    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
    public override IEnumerator<RateReset> GetEnumerator()
    {
      UpdateList();
      return list_.GetEnumerator();
    }

    #endregion

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator()
    {
      UpdateList();
      return list_.GetEnumerator();
    }

    #endregion
  }

  #endregion

  #region RateResetsHistorical

  /// <summary>
  ///  Store and retrieve a list of historical rates data.
  /// </summary>
  [Serializable]
  public class RateResetsHistorical : RateResets
  {
    #region Data
    private Currency ccy_;
    private Tenor tenor_;
    #endregion Data

    #region construcrors
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="resets">Historical resets</param>
    /// <param name="tenor">Index tenor</param>
    /// <param name="ccy">Denomination currency</param>
    public RateResetsHistorical(IList<RateReset> resets, Tenor tenor, Currency ccy)
      : base(resets)
    {
      tenor_ = tenor;
      ccy_ = ccy;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="resets">Historical resets</param>
    /// <param name="tenor">Index tenors</param>
    public RateResetsHistorical(IList<RateReset> resets, Tenor tenor)
      : base(resets)
    {
      tenor_ = tenor;
      ccy_ = Currency.None;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="resets">Historical resets</param>
    public RateResetsHistorical(IList<RateReset> resets)
      : base(resets)
    {
      tenor_ =  Tenor.Empty;
      ccy_ = Currency.None;
    }
    # endregion

    #region Properties
    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Tenor
    /// </summary>
    public Tenor Tenor
    {
      get { return tenor_; }
      set { tenor_ = value; }
    }
    /// <summary>
    /// Ccy - the curreny
    /// </summary>
    public Currency Ccy
    {
      get { return ccy_; }
      set { ccy_ = value; }
    }
    #endregion
  }

  #endregion
}