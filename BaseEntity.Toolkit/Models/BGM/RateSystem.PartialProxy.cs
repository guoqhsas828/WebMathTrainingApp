using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using ArrayBody = BaseEntity.Shared.ArrayMarshaler.Array;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  ///  Struct represent a pair of rate and annuity.
  /// </summary>
  [Serializable]
  public struct RateAnnuity
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="RateAnnuity"/> struct.
    /// </summary>
    /// <param name="rate">The rate.</param>
    /// <param name="annuity">The annuity.</param>
    public RateAnnuity(double rate, double annuity)
    {
      Rate = rate;
      Annuity = annuity;
    }
    /// <summary>
    ///   The rate.
    /// </summary>
    public double Rate;
    /// <summary>
    ///   The annuity.
    /// </summary>
    public double Annuity;

    /// <summary>
    /// Conversion to string
    /// </summary>
    /// <returns>String representation of rate annuity</returns>
    public override string ToString()
    {
      return String.Format("Rate:{0},Annuity:{1}",
        Rate, Annuity);
    }
  }

  /// <summary>
  ///  Struct represent a triple of rate, annuity and probability.
  /// </summary>
  [Serializable]
  public struct RateAnnuityProbability
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="RateAnnuityProbability"/> struct.
    /// </summary>
    /// <param name="rate">The rate.</param>
    /// <param name="annuity">The annuity.</param>
    /// <param name="probability">The probability.</param>
    public RateAnnuityProbability(
      double rate, double annuity, double probability)
    {
      Rate = rate;
      Annuity = annuity;
      Probability = probability;
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="RateAnnuityProbability"/> struct.
    /// </summary>
    /// <param name="rateAnnuity">The rate annuity.</param>
    /// <param name="probability">The probability.</param>
    public RateAnnuityProbability(
      RateAnnuity rateAnnuity, double probability)
    {
      Rate = rateAnnuity.Rate;
      Annuity = rateAnnuity.Annuity;
      Probability = probability;
    }
    /// <summary>
    ///   The rate.
    /// </summary>
    public double Rate;
    /// <summary>
    ///   The annuity.
    /// </summary>
    public double Annuity;
    /// <summary>
    ///   The probability.
    /// </summary>
    public double Probability;
  }

  /// <summary>
  ///   The abstract rate tree system.
  /// </summary>
  [Serializable]
  public sealed class RateSystem : Native.RateSystem, IRateSystemDistributions, INativeSerializable
  {
    public RateSystem() { }

    #region Native interfaces

#pragma warning disable 649
    [Serializable]
    private class RateStep
    {
      internal int istep;  // step index;
      internal int start;  // index of the first nontrivial tree node at this step;
      internal int count;  // the number of nontrivial tree nodes;
      internal int first;  // the index of the first active rate;
      internal RateAnnuity[] states; // states as an array of value types by rates and tree nodes;
    }
    private struct RateStepBody
    {
      internal int istep;  // step index;
      internal int start;  // index of the first nontrivial tree node at this step;
      internal int count;  // the number of nontrivial tree nodes;
      internal int first;  // the index of the first active rate;
      internal ArrayBody states_; // states as an array of value types by rates and tree nodes;
    }
    private struct RateSystemBody
    {
      internal int rateCount_;
      internal ArrayBody data_;
      internal ArrayBody prob_;
      internal ArrayBody frac_;
    }
#pragma warning restore 649

    unsafe private struct RateAnnuityPtr
    {
      internal RateAnnuityPtr(RateAnnuity* ptr)
      {
        Debug.Assert(ptr != null);
        ptr_ = ptr;
      }
      internal double Rate
      {
        get { return ptr_->Rate; }
      }
      internal double Annuity
      {
        get { return ptr_->Annuity; }
      }
      private readonly RateAnnuity* ptr_;
    }
    unsafe private struct RateStepPtr
    {
      internal RateStepPtr(RateStepBody* ptr)
      {
        Debug.Assert(ptr != null);
        ptr_ = ptr;
      }
      internal int StepIndex
      {
        get { return ptr_->istep; }
      }
      internal int StartLelevl
      {
        get { return ptr_->start; }
      }
      internal int NodeCount
      {
        get { return ptr_->count; }
      }
      internal int FirstActiveRate
      {
        get { return ptr_->first; }
      }
      internal int DataPointCount
      {
        get { return ptr_->states_.n_; }
      }
      internal RateAnnuityPtr this[int index]
      {
        get
        {
          var data = (RateAnnuity*)ptr_->states_.data_;
          return new RateAnnuityPtr(data + index);
        }
      }
      internal RateStep ToRateStep()
      {
        var p = ptr_;
        var d = new RateStep();
        d.istep = p->istep;
        d.start = p->start;
        d.count = p->count;
        d.first = p->first;
        int n = DataPointCount;
        var a = d.states = new RateAnnuity[n];
        if (n <= 0) return d;
        var data = (RateAnnuity*)p->states_.data_;
        for (int i = 0; i < n; ++i)
          a[i] = data[i];
        return d;
      }
      internal void CopyRateStep(RateStep d)
      {
        Debug.Assert(d != null);
        var p = ptr_;
        p->istep = d.istep;
        p->start = d.start;
        p->count = d.count;
        p->first = d.first;
        int n = DataPointCount;
        var a = d.states;
        Debug.Assert(a.Length == n);
        if (n <= 0) return;
        var data = (RateAnnuity*)p->states_.data_;
        for (int i = 0; i < n; ++i)
          data[i] = a[i];
      }
      private RateStepBody* ptr_;
    }
    unsafe private struct DoubleArrayPtr
    {
      internal DoubleArrayPtr(ArrayBody* ptr)
      {
        Debug.Assert(ptr != null);
        ptr_ = ptr;
      }
      internal int Count
      {
        get
        {
          return ptr_->n_;
        }
      }
      internal double this[int index]
      {
        get
        {
          return ((double*)(ptr_->data_))[index];
        }
        set
        {
          ((double*)(ptr_->data_))[index] = value;
        }
      }
      internal double[] ToArray()
      {
        int n = Count;
        var dst = new double[n];
        if (n <= 0) return dst;
        var src = (double*)(ptr_->data_);
        for (int i = 0; i < n; ++i)
          dst[i] = src[i];
        return dst;
      }
      internal void CopyArray(double[] src)
      {
        int n = Count;
        Debug.Assert(n == src.Length);
        if (n <= 0) return;
        var dst = (double*)(ptr_->data_);
        for (int i = 0; i < n; ++i)
          dst[i] = src[i];
      }
      private ArrayBody* ptr_;
    };
    private unsafe struct RateSystemPtr
    {
      internal RateSystemPtr(RateSystem rs)
      {
        IntPtr handle = getCPtr(rs).Handle;
        if (handle== IntPtr.Zero)
          ptr_ = null;
        ptr_ = (RateSystemBody*) handle.ToPointer();
      }
      internal bool IsNull
      {
        get{ return ptr_ == null;}
      }
      internal int RateCount
      {
        get
        {
          Debug.Assert(ptr_ != null);
          return ptr_->rateCount_;
        }
      }
      internal int DateCount
      {
        get
        {
          Debug.Assert(ptr_ != null);
          return ptr_->data_.n_;
        }
      }
      internal DoubleArrayPtr ProbabilitiesAt(int index)
      {
        Debug.Assert(ptr_ != null);
        Debug.Assert(index >= 0 && index < ptr_->prob_.n_);
        var data = (ArrayBody*)ptr_->prob_.data_;
        return new DoubleArrayPtr(data + index);
      }
      internal double[][] GetProbabilityArray()
      {
        Debug.Assert(ptr_ != null);
        int n = DateCount;
        var a = new double[n][];
        if (n <= 0) return a;
        var data = (ArrayBody*)ptr_->prob_.data_;
        for (int i = 0; i < n; ++i)
          a[i] = (new DoubleArrayPtr(data + i)).ToArray();
        return a;
      }
      internal RateStep[] GetRateStepArray()
      {
        Debug.Assert(ptr_ != null);
        int n = DateCount;
        var a = new RateStep[n];
        if (n <= 0) return a;
        var data = (RateStepBody*)ptr_->data_.data_;
        for (int i = 0; i < n; ++i)
          a[i] = (new RateStepPtr(data + i)).ToRateStep();
        return a;
      }
      internal RateStepPtr this[int index]
      {
        get
        {
          Debug.Assert(ptr_ != null);
          Debug.Assert(index >= 0 && index < ptr_->data_.n_);
          var data = (RateStepBody*)ptr_->data_.data_;
          return new RateStepPtr(data + index);
        }
      }
      internal double GetFraction(int idx)
      {
        return ptr_->frac_.Get<double>(idx);
      }
      private RateSystemBody* ptr_;
    }
    private RateSystemPtr GetPointer()
    {
      return new RateSystemPtr(this);
    }
    private void InitializeFrom(int rateCount, RateStep[] data, double[][] prob)
    {
      Debug.Assert(data.Length == prob.Length);
      int dateCount = data.Length;
      initialize(rateCount, dateCount);
      var p = GetPointer();
      Debug.Assert(!p.IsNull);
      for (int i = 0; i < dateCount; ++i)
      {
        set_up(i, prob[i].Length);
        p.ProbabilitiesAt(i).CopyArray(prob[i]);
        p[i].CopyRateStep(data[i]);
      }
      return;
    }
    #endregion Native interfaces

    #region IRateSystemDistributions Members

    /// <summary>
    /// Gets the number of rates at a given date.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <returns>The number of rates.</returns>
    public int GetRateCount(int dateIndex)
    {
      return RateCount;
    }

    /// <summary>
    /// Gets the number of state nodes at a given date.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <returns>The number of nodes.</returns>
    public int GetStateCount(int dateIndex)
    {
      var ptr = GetPointer();
      if (ptr.IsNull || ptr.DateCount <= dateIndex || dateIndex < 0)
        return 0;
      return ptr[dateIndex].NodeCount;
    }

    /// <summary>
    /// Gets the probability of a given state at a given date.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns>The probability.</returns>
    public double GetProbability(int dateIndex, int stateIndex)
    {
      var ptr = GetPointer();
      if (ptr.IsNull || ptr.DateCount <= dateIndex || dateIndex < 0 || stateIndex < 0)
        return 0;
      var dist = ptr.ProbabilitiesAt(dateIndex);
      if (stateIndex >= dist.Count)
        return 0;
      return dist[stateIndex];
    }

    /// <summary>
    /// Gets the conditional probability of a date/state pair
    /// given that the system is in the <c>baseState</c>
    /// at the <c>baseDate</c>.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <param name="baseDateIndex">Index of the base date.</param>
    /// <param name="baseStateIndex">Index of the base state.</param>
    /// <returns>The conditional probability.</returns>
    public double GetConditionalProbability(
      int dateIndex, int stateIndex,
      int baseDateIndex, int baseStateIndex)
    {
      var ptr = GetPointer();
      if (ptr.IsNull || ptr.DateCount <= dateIndex || dateIndex < 0
        || ptr.DateCount <= baseDateIndex || baseDateIndex < 0)
      {
        return 0;
      }
      var dist = ptr[dateIndex];
      var baseDist = ptr[baseDateIndex];
      return FixedProbabilityBinaryTree.ConditionalProbability(
        (uint)dist.StepIndex, (uint)(dist.StartLelevl + stateIndex),
        (uint)baseDist.StepIndex, (uint)(baseDist.StartLelevl + baseStateIndex)
        );
    }

    /// <summary>
    /// Gets the annuity, which normally should be the zero bond
    /// price corresponding to a rate.
    /// </summary>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns>The annuity.</returns>
    public double GetAnnuity(int rateIndex, int dateIndex, int stateIndex)
    {
      var ptr = GetPointer();
      if (ptr.IsNull || ptr.DateCount <= dateIndex || dateIndex < 0)
        return 0;
      var dist = ptr[dateIndex];
      if (dist.NodeCount <= stateIndex || stateIndex < 0)
        return 0;
      return dist[stateIndex*ptr.RateCount + rateIndex].Annuity;
    }

    /// <summary>
    /// Gets the rate.
    /// </summary>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns>The rate.</returns>
    public double GetRate(int rateIndex, int dateIndex, int stateIndex)
    {
      var ptr = GetPointer();
      if (ptr.IsNull || ptr.DateCount <= dateIndex || dateIndex < 0)
        return 0;
      var dist = ptr[dateIndex];
      if (dist.NodeCount <= stateIndex || stateIndex < 0)
        return 0;
      return dist[stateIndex * ptr.RateCount + rateIndex].Rate;
    }

    /// <summary>
    /// Gets the index of the rate reset most recently.
    /// </summary>
    /// <param name="dateIndex">Index of the date.</param>
    /// <returns>The rate index, or -1 for invalid date index.</returns>
    public int GetLastResetIndex(int dateIndex)
    {
      var ptr = GetPointer();
      if (ptr.IsNull || ptr.DateCount <= dateIndex || dateIndex < 0)
        return -1;
      return ptr[dateIndex].FirstActiveRate;
    }

    /// <summary>
    /// Gets the fraction.
    /// </summary>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <returns>The fraction</returns>
    public double GetFraction(int rateIndex)
    {
      var ptr = GetPointer();
      if (ptr.IsNull)
        return Double.NaN;
      return ptr.GetFraction(rateIndex);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the time grid node dates.
    /// </summary>
    /// <value>The time grid node dates.</value>
    public Dt[] NodeDates
    {
      get { return nodeDates_; }
      set{ nodeDates_ = value;}
    }

    /// <summary>
    /// Gets the number of time grid dates.
    /// </summary>
    /// <value>The number of time grid dates.</value>
    public int DateCount
    {
      get { return GetPointer().DateCount; }
    }

    /// <summary>
    /// Gets the number of forward rates.
    /// </summary>
    /// <value>The number of forward rates.</value>
    public int RateCount
    {
      get { return GetPointer().RateCount; }
    }

    /// <summary>
    /// Gets as-of date.
    /// </summary>
    /// <value>The as-of date.</value>
    public Dt AsOf
    {
      get { return asOf_; }
      set { asOf_ = value; }
    }

    /// <summary>
    /// Gets the tenor dates of the forward rates.
    /// </summary>
    /// <value>The tenor dates.</value>
    public Dt[] TenorDates
    {
      get { return tenorDates_; }
      set { tenorDates_ = value; }
    }

#if DEBUG
    private RateStep[] RateSteps
    {
      get
      {
        var p = GetPointer();
        return p.IsNull ? null : p.GetRateStepArray();
      }
    }

    public double[][] Probabilities
    {
      get
      {
        var p = GetPointer();
        return p.IsNull ? null : p.GetProbabilityArray();
      }
    }
#endif
    #endregion

    #region ISerializable Members

    private RateSystem(SerializationInfo info, StreamingContext context)
    {
      var rateCount = (int)info.GetValue("rateCount_", typeof(int));
      var data = (RateStep[])info.GetValue("data_", typeof(RateStep[]));
      var prob = (double[][])info.GetValue("prob_", typeof(double[][]));
      InitializeFrom(rateCount, data, prob);
      return;
    }

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (!swigCMemOwn)
        throw new Exception("Object can not be serialized when swigCMemOwn is false.");
      var p = GetPointer();
      info.AddValue("rateCount_", p.RateCount);
      info.AddValue("data_", p.GetRateStepArray());
      info.AddValue("prob_", p.GetProbabilityArray());
    }

    #endregion

    #region data members

    private Dt asOf_;
    private Dt[] tenorDates_;
    private Dt[] nodeDates_;
    #endregion
  }
}
