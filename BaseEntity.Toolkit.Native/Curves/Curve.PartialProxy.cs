/*
 * Curve.PartialProxy.cs
 *
 *
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.ComponentModel;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Curves.Native
{
  [Serializable]
  [ReadOnly(true)]
  partial class Curve : IFactorCurve, ISpotCurve, IEnumerable<DateAndValue<double>>
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Curve));

    #region Swig Interfaces
    [Serializable]
    private sealed class NativeRef : INativeSerializable, IDisposable
    {
      public HandleRef swigCPtr;
      public bool swigCMemOwn;

      public NativeRef(IntPtr cPtr, bool cMemoryOwn)
      {
        swigCPtr = new HandleRef(this, cPtr);
        swigCMemOwn = cMemoryOwn;
      }

      public NativeRef(IntPtr cPtr, bool cMemoryOwn,
        SerializationInfo info, StreamingContext context)
      {
        if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();

        swigCMemOwn = cMemoryOwn;
        swigCPtr = new HandleRef(this, cPtr);

        Dt asOf = (Dt)info.GetValue("asOf_", typeof(Dt));
        double spread = info.GetDouble("spread_");
        BaseEntity.Toolkit.Numerics.InterpScheme interpMeth = (BaseEntity.Toolkit.Numerics.InterpScheme)info.GetValue("interp_", typeof(BaseEntity.Toolkit.Numerics.InterpScheme));
        DayCount dc = (DayCount)info.GetValue("dayCount_", typeof(DayCount));
        Frequency freq = (Frequency)info.GetValue("freq_", typeof(Frequency));
        double[] x = (double[])info.GetValue("x_", typeof(double[]));
        double[] y = (double[])info.GetValue("y_", typeof(double[]));
        Dt[] dt = (Dt[])info.GetValue("dt_", typeof(Dt[]));
        string name = info.GetString("name_");
        string category = info.GetString("category_");
        Currency ccy = (Currency)info.GetValue("ccy_", typeof(Currency));
        int flags = (int)info.GetValue("flags_", typeof(int));
        Dt jumpDate = (Dt)info.GetValue("jumpDate_", typeof(Dt));
        BaseEntity.Toolkit.Numerics.Interp interp = interpMeth.ToInterp();
        BaseEntityPINVOKE.Curve_Set_publicState(swigCPtr, asOf, spread,
          BaseEntity.Toolkit.Numerics.Interp.getCPtr(interp), (int)dc, (int)freq,
          x, y, dt, name, category, (int)ccy, flags, jumpDate);

        return;
      }

      ///<exclude/>
      NativeRef(SerializationInfo info, StreamingContext context)
        : this(BaseEntityPINVOKE.new_Curve__SWIG_0(), true, info, context)
      {
      }

      ///<exclude/>
      [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
      public void GetObjectData(SerializationInfo info, StreamingContext context)
      {
        if (!swigCMemOwn)
          throw new ToolkitException("Object can not be serialized when swigCMemOwn is false.");

        Curve curve = new Curve(swigCPtr.Handle, false);
        info.AddValue("asOf_", curve.GetAsOf());
        info.AddValue("spread_", curve.GetSpread());
        info.AddValue("x_", curve.Get_publicState_x());
        info.AddValue("dt_", curve.Get_publicState_dt());
        info.AddValue("y_", curve.Get_publicState_y());
        info.AddValue("interp_", BaseEntity.Toolkit.Numerics.InterpScheme.FromInterp(curve.GetInterp()));
        info.AddValue("dayCount_", curve.GetDayCount());
        info.AddValue("freq_", curve.GetFrequency());
        info.AddValue("name_", curve.GetName());
        info.AddValue("category_", curve.GetCategory());
        info.AddValue("ccy_", curve.GetCcy());
        info.AddValue("flags_", curve.GetFlags());
        info.AddValue("jumpDate_", curve.Get_JumpDate());
      }

      /// <exclude />
      ~NativeRef()
      {
        Dispose();
      }

      /// <exclude />
      public void Dispose()
      {
        if (swigCPtr.Handle != IntPtr.Zero && swigCMemOwn)
        {
          swigCMemOwn = false;
          BaseEntityPINVOKE.delete_Curve(swigCPtr);
        }
        swigCPtr = new HandleRef(null, IntPtr.Zero);
        GC.SuppressFinalize(this);
      }

      public override string ToString()
      {
        return String.Format("{0},{1}", swigCPtr.Handle, swigCMemOwn);
      }
    } // class NativeCurve

    // Property of Curve to make SWIG happy.
    private HandleRef swigCPtr
    {
      get
      {
        ValidateIntegrity(this);
        return native_ == null
          ? new HandleRef(null, IntPtr.Zero) : native_.swigCPtr;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the object owns the allocated memory.
    /// <prelimnary>For  public use only.</prelimnary>
    /// </summary>
    /// <value><c>true</c> if the object owns the allocated memory; otherwise, <c>false</c>.</value>
    public bool swigCMemOwn
    {
      get
      {
        return native_ == null ? false : native_.swigCMemOwn;
      }
      set
      {
        if (native_ != null) native_.swigCMemOwn = value;
      }
    }

    public Curve(IntPtr cPtr, bool cMemoryOwn)
    {
      native_ = new NativeRef(cPtr, cMemoryOwn);
    }

    /// <exclude />
    public static HandleRef getCPtr(Curve obj)
    {
      return (obj == null) ? new HandleRef(null, IntPtr.Zero) : obj.swigCPtr;
    }

    /// <exclude />
    public virtual void Dispose()
    {
      if (native_ != null)
        native_.Dispose();
      GC.SuppressFinalize(this);
    }

    private bool OverlayMistmatch()
    {
      return overlay_ != null && overlay_.native_.swigCPtr.Handle
        != BaseEntityPINVOKE.Curve_getOverlayedCurve(native_.swigCPtr);
    }
    #endregion Swig Interfaces

    #region For Debuging Only
    [Conditional("DEBUG")]
    private static void ValidateIntegrity(Curve curve)
    {
      if (curve == null || curve.native_ == null
        || curve.native_.swigCPtr.Handle == IntPtr.Zero)
      {
        throw new ToolkitException("Access null curve handle!!!");
      }
      if (curve.OverlayMistmatch())
      {
        throw new ToolkitException("Curve integrity damaged!!!");
      }
      return;
    }

    [Conditional("DEBUG")]
    public static void ValidateIndex(Curve curve, int index)
    {
      if (index < 0)
        throw new ArgumentException("Curve index cannot be negative.");
      if (curve == null || curve.swigCPtr.Handle == IntPtr.Zero)
        throw new ArgumentException("Accessing null curve.");
      int count = curve.Size();
      if (count <= index)
      {
        throw count == 0
          ? new ArgumentException("Accessing data in empty curve.")
          : new ArgumentException(String.Format(
            "Curve index {0} out of the valid range [0,{1}].",
            index, count - 1));
      }
      return;
    }
    #endregion For Debuging Only

    #region Constructors

    private Curve(NativeRef native, Curve overlay, CustomInterp customInterp)
    {
      native_ = native;
      overlay_ = overlay;
      if (customInterp == null) return;
      var ci = customInterp_ = customInterp;
      SetInterp(ci.GetInterp());
    }

    /// <summary>
    /// Constructor of a standard overlaid curve
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="freq">Frequency</param>
    /// <param name="overlay">Overlay curve</param>
    public Curve(Dt asOf, Frequency freq, Curve overlay)
    {
      if (overlay == null)
      {
        var c = new Curve(asOf, freq);
        native_ = new NativeRef(c.native_.swigCPtr.Handle, true);
        c.swigCMemOwn = false;
        return;
      }
      var oc = createOverlayedCurve(asOf, freq, overlay);
      native_ = new NativeRef(oc.native_.swigCPtr.Handle, true);
      oc.swigCMemOwn = false;
      overlay_ = overlay;
    }

    /// <summary>
    ///   For  public use only.
    /// </summary>
    /// <param name="cPtr">cPtr</param>
    /// <param name="cMemoryOwn">cMemoryOwn</param>
    /// <returns>Curve</returns>
    /// <exclude/>
    public static Curve Create(IntPtr cPtr, bool cMemoryOwn)
    {
      return new Curve(cPtr, cMemoryOwn);
    }

    /// <summary>
    ///   Get a non-overlaid copy of the main curve.
    /// </summary>
    /// <returns>A copy of the main curve.</returns>
    public Curve CopyMainCurve()
    {
      return clone(this) ?? new Curve(IntPtr.Zero, false);
    }

    /// <summary>
    /// Clone method
    /// </summary>
    /// <returns>Deep copy of the curve</returns>
    public Curve clone()
    {
      Curve curve;
      if (Overlay != null)
      {
        if(customInterp_!=null)
        {
          throw new ToolkitException("Overlay and Custom Interpolator cannot both present.");
        }
        Curve overlay = Overlay.clone();
        curve = cloneOverlayedCurve(this, overlay) ??
          new Curve(IntPtr.Zero, false);
        return new Curve(curve.native_, overlay, null);
      }

      curve = clone(this) ?? new Curve(IntPtr.Zero, false);
      return customInterp_ != null
        ? new Curve(curve.native_, null, customInterp_) 
        : curve;
    }

    /// <summary>
    /// Creates the forward volatility curve from a Black volatility curve.
    /// </summary>
    /// <param name="blackVolatilityCurve">The black volatility curve.</param>
    /// <param name="interp">Interp method</param>
    /// <returns>The forward volatility curve</returns>
    public static Curve CreateForwardVolatilityCurve(
      Curve blackVolatilityCurve, Interp interp)
    {
      return createForwardVolatilityCurve(blackVolatilityCurve, interp);
    }

    [OnDeserialized, AfterFieldsCloned]
    private void SetUpInsideCurve(StreamingContext context)
    {
      // Handle the case with overlay inside overlay and
      // the call of this function is out of order.
      if (OverlayMistmatch())
      {
        overlay_.SetUpInsideCurve(context);
        Curve overlay = overlay_;
        overlay_ = null; // To make integrity check happy.
        Curve curve = cloneOverlayedCurve(this, overlay);
        native_ = curve.native_;
        overlay_ = overlay;
      }
      if (customInterp_ != null)
      {
        SetInterp(customInterp_.GetInterp());
      }
      return;
    }
    #endregion Constructors

    #region Custom Interps
    /// <summary>
    /// Create a new instance of the <see cref="Curve"/> class
    ///  with a custom curve interpolator.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="curveInterpolator">The curve interpolator.</param>
    public static Curve Create(Dt asOf, ICurveInterpolator curveInterpolator)
    {
      var customInterp = new CustomInterp(curveInterpolator);
      var interp = customInterp.GetInterp();
      var curve = new Curve(asOf, interp, DayCount.None, Frequency.None);
      return new Curve(curve.native_, null, customInterp);
    }

    /// <summary>
    /// Convert to a composite curve with custom interpolator, in place.
    /// </summary>
    /// <param name="curveInterpolator">The custom interpolator</param>
    /// <param name="addPoint">if set to <c>true</c>, add a point</param>
    /// <exception cref="ToolkitException">Cannot set custom interpolator on a composite curve</exception>
    /// <exception cref="System.ArgumentNullException"></exception>
    public void SetCustomInterp(ICurveInterpolator curveInterpolator,
      bool addPoint = true)
    {
      if (overlay_ != null)
      {
        throw new ToolkitException(
          "Cannot set custom interpolator on a composite curve");
      }
      if (curveInterpolator == null)
      {
        throw new ArgumentNullException("curveInterpolator");
      }
      Clear();
      SetDayCount(DayCount.None);
      SetFrequency(Frequency.None);
      var ci = customInterp_ = new CustomInterp(curveInterpolator);
      SetInterp(ci.GetInterp());
      if (addPoint) Add(GetAsOf(), 1.0);
    }

    [Serializable]
    private class CustomInterp
    {
      // These delegates are passed down to unmanaged world.
      // We need to hold the references here to prevent premature
      // garbage collection.
      [NonSerialized]
      private DelegateCurveInterp.EvaluateFn evalFn_;
      [NonSerialized]
      private DelegateCurveInterp.InitializeFn initFn_;

      public readonly ICurveInterpolator Interpolator;
      public CustomInterp(ICurveInterpolator interpolator)
      {
        Interpolator = interpolator;
      }
      public Interp GetInterp()
      {
        if (Interpolator == null) return null;
        if (evalFn_ == null)
        {
          evalFn_ = (c, t, i) => Interpolator.Evaluate(new Curve(c, false), t, i);
        }
        if (initFn_ == null)
        {
          initFn_ = (c) => Interpolator.Initialize(new Curve(c, false));
        }
        return new DelegateCurveInterp(initFn_, evalFn_);
      }
    }

    #endregion Custom Interps

    #region Properties
    /// <summary>
    ///   Unique id for this curve. Used for sorting
    /// </summary>
    public long Id
    {
      get { return native_ == null ? 0 : native_.swigCPtr.Handle.ToInt64(); }
    }

    /// <summary>
    /// Gets the custom interpolator.
    /// </summary>
    /// <value>The custom interpolator.</value>
    public ICurveInterpolator CustomInterpolator
    {
      get{ return customInterp_== null ? null : customInterp_.Interpolator;}
    }

    
    /// <summary>
    /// Overlay curve
    /// </summary>
    public Curve Overlay
    {
      get { return overlay_; }
    }

    #endregion Properties

    #region Data

    private NativeRef native_;
    private CustomInterp customInterp_;
    private Curve overlay_;

    [NonSerialized]
    private ConcurrentDictionary<Dt, double> cache_; 

    #endregion Data

    #region Methods
    public bool CacheEnabled
    {
      get { return cache_ != null; }
    }
    public void EnableCache()
    {
      if (cache_ == null)
        cache_ = new ConcurrentDictionary<Dt, double>(DtEqualityComparer.HashByMonth);
    }
    public void DisableCache()
    {
      cache_ = null;
    }
    public void ClearCache()
    {
      if (cache_ != null) cache_.Clear();
    }
    private double InvokeNativeCurveInterpolate(Dt date)
    {
      if (CustomInterpolator is IEvaluator<Dt, double> interp)
        return interp.Evaluate(date);
      if (Size() == 0 && CustomInterpolator!=null)
      {
        return CustomInterpolator.Evaluate(this, date - GetAsOf(), -1);
      }
      return interpolate(date);
    }
    /// <summary>
    /// Interpolates the value at the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>System.Double.</returns>
    public virtual double Interpolate(Dt date)
    {
      if (cache_ != null)
      {
        double y;
        if (cache_.TryGetValue(date, out y))
          return y;
        y = InvokeNativeCurveInterpolate(date);
        cache_.AddOrUpdate(date, y, (k,v)=>v);
        return y;
      }
      return InvokeNativeCurveInterpolate(date);
    }

    /// <summary>
    /// Interpolates the value at the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>System.Double.</returns>
    public virtual double Interpolate(double date)
    {
      if (CustomInterpolator is IEvaluator<double, double> interp)
        return interp.Evaluate(date);
      if (Size() == 0 && CustomInterpolator != null)
      {
        return CustomInterpolator.Evaluate(this, date, -1);
      }
      return interpolate(date);
    }

    /// <summary>
    /// Interpolates the value determined by the start and end dates.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns>System.Double.</returns>
    public virtual double Interpolate(Dt start, Dt end)
    {
      if (cache_ != null)
      {
        return Interpolate(end)/Interpolate(start);
      }
      if (CustomInterpolator is IEvaluator<Dt, double> interp)
        return (interp.Evaluate(end) / interp.Evaluate(start));
      if (Size() == 0 && CustomInterpolator!=null)
      {
        var asOf = GetAsOf();
        return CustomInterpolator.Evaluate(this, end - asOf, -1)
          / CustomInterpolator.Evaluate(this, start - asOf, -1);
      }
      return interpolate(start, end);
    }

    /// <summary>
    /// Integrate <m>f(t)g(t)</m> between start and end
    /// </summary>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <param name="f">Curve representation of a function of time</param>
    /// <param name="g">Curve representation of a function of time</param>
    public static double Integrate(Dt start, Dt end, Curve f, Curve g)
    {
      return Integral(start, end, f, g)/365.0;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.</returns>
    public IEnumerator<DateAndValue<double>> GetEnumerator()
    {
      for (int i = 0, n = Size(); i < n; ++i)
        yield return DateAndValue.Create(GetDt(i), GetVal(i));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
    #endregion
  } // partial class Curve
}
