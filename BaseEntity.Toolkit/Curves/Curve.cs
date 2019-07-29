using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Native;
using BaseEntity.Toolkit.Numerics;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   A data curve
  /// </summary>
  /// <remarks>
  ///   <para>A generalized term structure of values.</para>
  ///   <para>Examples of curves include:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="Toolkit.Curves.DiscountCurve">Interest rate term structure</see></description></item>
  ///     <item><description><see cref="Toolkit.Curves.SurvivalCurve">Credit spread term structure</see></description></item>
  ///     <item><description><see cref="Toolkit.Curves.VolatilityCurve">Volatility term structure</see></description></item>
  ///   </list>
  /// </remarks>
  [Serializable]
  public class Curve : BaseEntityObject, INativeCurve, IFactorCurve, ISpotCurve,
    IComparable<Curve>, IEquatable<Curve>, IEnumerable<CurvePoint>, IModelParameter
  {
    #region Native curve interface

    private NativeCurve native_;

    /// <summary>
    /// Gets the native curve.
    /// </summary>
    /// <remarks></remarks>
    public  NativeCurve NativeCurve {get { return native_; }}

    /// <summary>
    ///  Implicit convert to native curve.
    /// </summary>
    /// <param name="curve"></param>
    /// <returns></returns>
    public static implicit operator NativeCurve(Curve curve)
    {
      return curve?.NativeCurve;
    }

    /// <summary>
    ///  Explicit convert from native curve.
    /// </summary>
    /// <param name="curve">The native curve</param>
    /// <returns>Curve</returns>
    public static explicit operator Curve(NativeCurve curve)
    {
      return new Curve(curve);
    }

    /// <exclude>For  public use only.</exclude>
    public static HandleRef getCPtr(Curve curve)
    {
      return curve == null
        ? new HandleRef(null, IntPtr.Zero)
        : NativeCurve.getCPtr(curve.NativeCurve);
    }

    HandleRef INativeObject.HandleRef => getCPtr(this);

    /// <summary>Clone</summary>
    public override object Clone()
    {
      Curve obj = (Curve)base.Clone();

      // Call small clone() to clone native curve.
      if (native_ != null) obj.native_ = native_.clone();
      return obj;
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks></remarks>
    public override string ToString()
    {
      return native_ == null ? "" : native_.ToString();
    }
    #endregion

    #region Cast operators

    /// <summary>
    /// Performs an implicit conversion from <see cref="Curve"/> to Func&lt;Dt, double&gt;"/>.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Func<Dt,double>(Curve curve)
    {
      var sc = curve as SurvivalCurve;
      if (sc != null && !sc.DefaultDate.IsEmpty() && sc.Deterministic)
      {
        Dt defaultDate = sc.DefaultDate;
        return dt => dt < defaultDate ? 1.0 : 0.0;
      }
      return curve == null ? null : new Func<Dt, double>(curve.Interpolate);
    }

    #endregion Cast operators

    #region Constructors
    ///<exclude>For internal use only.</exclude>
    /// <summary>
    /// Creates a curve from the specified native pointer. 
    /// </summary>
    /// <param name="cPtr">The pointer.</param>
    /// <param name="cMemoryOwn">If set to <c>true</c>, this object is responsible to
    ///   delete the memory upon disposal;  Otherwise, it is will never delete the pointer..</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static Curve Create(IntPtr cPtr, bool cMemoryOwn)
    {
      return new Curve(NativeCurve.Create(cPtr, cMemoryOwn));
    }

    public Curve(NativeCurve native)
    {
      native_ = native;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <remarks></remarks>
    public Curve()
    {
      native_ = new NativeCurve();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <remarks></remarks>
    public Curve(Dt asOf)
    {
      native_ = new NativeCurve(asOf);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="rate">The rate.</param>
    /// <remarks></remarks>
    public Curve(Dt asOf, double rate)
    {
      native_ = new NativeCurve(asOf, rate);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="freq">The freq.</param>
    /// <remarks></remarks>
    public Curve(Dt asOf, Frequency freq)
    {
      native_ = new NativeCurve(asOf, freq);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="freq">The freq.</param>
    /// <param name="rate">The rate.</param>
    /// <remarks></remarks>
    public Curve(Dt asOf, Frequency freq, double rate)
    {
      native_ = new NativeCurve(asOf, freq, rate);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="dc">The dc.</param>
    /// <param name="freq">The freq.</param>
    /// <remarks></remarks>
    public Curve(Dt asOf, DayCount dc, Frequency freq)
    {
      native_ = new NativeCurve(asOf, dc, freq);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="interp">The interp.</param>
    /// <param name="dc">The dc.</param>
    /// <param name="freq">The freq.</param>
    /// <remarks></remarks>
    public Curve(Dt asOf, Interp interp, DayCount dc, Frequency freq)
    {
      native_ = new NativeCurve(asOf, interp, dc, freq);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="freq">The freq.</param>
    /// <param name="overlay">The overlay.</param>
    /// <remarks></remarks>
    public Curve(Dt asOf, Frequency freq, Curve overlay)
    {
      native_ = new NativeCurve(asOf, freq, overlay != null ? overlay.NativeCurve : null);
    }

    public Curve clone()
    {
      return new Curve(native_.clone());
    }
    #endregion

    #region Mutation Methods
    /// <summary>
    /// Clears all curve points.
    /// </summary>
    /// <remarks></remarks>
    public void Clear()
    {
      native_.ClearCache();
      var savedFlags = Flags & (CurveFlags.Internal
        | CurveFlags.SmoothTime | CurveFlags.Stressed);
      native_.Clear();
      Flags = savedFlags;
    }

    /// <summary>
    /// Shrinks the array of curve points to the specified size.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <remarks></remarks>
    public void Shrink(int size)
    {
      native_.ClearCache();
      native_.Shrink(size);
    }

    /// <summary>
    /// Adds a curve point with the specified date and value.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="val">The val.</param>
    /// <remarks></remarks>
    public void Add(Dt date, double val)
    {
      native_.ClearCache();
      native_.Add(date, val);
    }

    /// <summary>
    /// Adds an array of curve points with the specified dates and values.
    /// </summary>
    /// <param name="dates">The dates.</param>
    /// <param name="vals">The vals.</param>
    /// <remarks></remarks>
    public void Add(Dt[] dates, double[] vals)
    {
      native_.ClearCache();
      native_.Add(dates, vals);
    }

    /// <summary>
    /// Sets the curve points with the specified dates and values.
    /// </summary>
    /// <param name="dates">The dates.</param>
    /// <param name="vals">The vals.</param>
    /// <remarks></remarks>
    public void Set(Dt[] dates, double[] vals)
    {
      native_.ClearCache();
      native_.Set(dates, vals);
    }

    /// <summary>
    /// Sets the curve points from the specified curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <remarks></remarks>
    public void Set(Curve curve)
    {
      native_.ClearCache();
      native_.Set(curve.NativeCurve);
    }

    /// <summary>
    /// Sets the tension factors.
    /// </summary>
    /// <param name="factors">The factors.</param>
    /// <remarks></remarks>
    public void SetTensionFactors(double[] factors)
    {
      native_.ClearCache();
      native_.SetTensionFactors(factors);
    }

    /// <summary>
    /// Makes the tension factors fixed at their current values.
    /// </summary>
    /// <remarks></remarks>
    public void FixTensionFactors()
    {
      native_.ClearCache();
      native_.FixTensionFactors();
    }

    /// <summary>
    /// Sets the value of at the specified curve point.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="value">The value.</param>
    /// <remarks></remarks>
    public void SetVal(int index, double value)
    {
      native_.ClearCache();
      native_.SetVal(index, value);
    }

    /// <summary>
    /// Sets the rate value at the specified curve point.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="rate">The rate.</param>
    /// <remarks></remarks>
    public void SetRate(int index, double rate)
    {
      native_.ClearCache();
      native_.SetRate(index, rate);
    }

    /// <summary>
    /// Sets the date at the specified curve point.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="date">The date.</param>
    /// <remarks></remarks>
    public void SetDt(int index, Dt date)
    {
      native_.ClearCache();
      native_.SetDt(index, date);
    }

    /// <summary>
    /// Sets the date and value at the specified curve point.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="date">The date.</param>
    /// <param name="val">The val.</param>
    /// <remarks></remarks>
    public void Set(int index, Dt date, double val)
    {
      native_.ClearCache();
      native_.Set(index, date, val);
    }

    /// <summary>
    /// Fills the specified value at all the curve points.
    /// </summary>
    /// <param name="val">The val.</param>
    /// <remarks></remarks>
    public void Fill(double val)
    {
      native_.ClearCache();
      native_.Fill(val);
    }

    public bool CacheEnabled
    {
      get { return native_.CacheEnabled; }
    }
    public void EnableCache()
    {
      native_.EnableCache();
    }
    public void DisableCache()
    {
      native_.DisableCache();
    }
    public void ClearCache()
    {
      native_.ClearCache();
    }
    #endregion

    #region Nonmutation Methods
    /// <summary>
    /// Gets the value of the specified curve point.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public  double GetVal(int index)
    {
      return native_.GetVal(index);
    }

    /// <summary>
    /// Gets the date of the specified curve point.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public  Dt GetDt(int index)
    {
      return native_.GetDt(index);
    }

    /// <summary>
    /// Interpolates a value for the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public double Interpolate(Dt date)
    {
      return native_.Interpolate(date);
    }

    /// <summary>
    /// Interpolates a value for the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public double Interpolate(double date)
    {
      return native_.Interpolate(date);
    }

    /// <summary>
    /// Interpolates the value for the specified period.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public virtual double Interpolate(Dt start, Dt end)
    {
      return native_.Interpolate(start, end);
    }

    /// <summary>
    /// Integrates the curve values for the specified period, with time unit in years.
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <param name="f">The first curve.</param>
    /// <param name="g">The second curve.</param>
    /// <returns>The integral</returns>
    /// <remarks></remarks>
    public static double Integrate(Dt start, Dt end, Curve f, Curve g)
    {
      return NativeCurve.Integrate(start, end, f, g);
    }

    /// <summary>
    /// Integrates the curve values for the specified period, with time unit in days.
    /// </summary>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="f">The first curve.</param>
    /// <param name="g">The second curve.</param>
    /// <returns>The integral</returns>
    /// <remarks></remarks>
    public static double Integral(double start, double end, Curve f, Curve g)
    {
      return NativeCurve.Integral(start, end, f, g);
    }

    /// <summary>
    /// Integrates the curve values for the specified period, with time unit in days.
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <param name="f">The first curve.</param>
    /// <param name="g">The second curve.</param>
    /// <returns>The integral</returns>
    /// <remarks></remarks>
    public static double Integral(Dt start, Dt end, Curve f, Curve g)
    {
      return NativeCurve.Integral(start, end, f, g);
    }

    /// <summary>
    ///  Calculates the forward rate for the specified period.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public double F(Dt start, Dt end)
    {
      return native_.F(start, end);
    }

    /// <summary>
    ///  Calculates the forward rate for the specified period.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <param name="dc">The dc.</param>
    /// <param name="freq">The freq.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public double F(Dt start, Dt end, DayCount dc, Frequency freq)
    {
      return native_.F(start, end, dc, freq);
    }

    /// <summary>
    ///  Calculates the spot rate for the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public double R(Dt date)
    {
      return native_.R(date);
    }

    /// <summary>
    ///  Calculates the first and second order derivatives of the curve points.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="grad">The grad.</param>
    /// <param name="hess">The hess.</param>
    /// <remarks></remarks>
    public void Derivatives(Dt date, double[] grad, double[] hess)
    {
      native_.Derivatives(date, grad, hess);
    }

    /// <summary>
    ///  Find the first curve point after the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public int After(Dt date)
    {
      return native_.After(date);
    }

    /// <summary>
    /// Solves the specified value.
    /// </summary>
    /// <param name="val">The val.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public Dt Solve(double val)
    {
      return native_.Solve(val);
    }

    /// <summary>
    /// Solves the specified value.
    /// </summary>
    /// <param name="val">The val.</param>
    /// <param name="accuracy">The accuracy.</param>
    /// <param name="iterations">The iterations.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public Dt Solve(double val, double accuracy, int iterations)
    {
      return native_.Solve(val, accuracy, iterations);
    }
    #endregion

    #region Composite and overlay curves
    /// <summary>
    /// Creates the forward volatility curve.
    /// </summary>
    /// <param name="blackVolatilityCurve">The black volatility curve.</param>
    /// <param name="interp">The interpolator.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static Curve CreateForwardVolatilityCurve(
      Curve blackVolatilityCurve, Interp interp)
    {
      return new Curve(NativeCurve.CreateForwardVolatilityCurve(
        blackVolatilityCurve.NativeCurve, interp));
    }

    /// <summary>
    ///  Make this the reference of a C++ composite curve.
    /// </summary>
    /// <remarks>
    ///  This function is unsafe to call anywhere other than within
    ///  the constructor of the <c>CompositeDiscountCurve</c> class,
    ///  for it requires that the two curves <paramref name="preSpotCurve"/> 
    ///  and <paramref name="postSpotCurve"/> are alive during 
    ///  the life span of the this object.
    /// </remarks>
    /// <param name="preSpotCurve">The pre spot curve.</param>
    /// <param name="postSpotCurve">The post spot curve.</param>
    public void Composite(DiscountCurve preSpotCurve, DiscountCurve postSpotCurve)
    {
      native_ = NativeCurve.createCompositeCurve(preSpotCurve, postSpotCurve);
    }

    /// <summary>
    /// Copies the main curve.
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
    public Curve CopyMainCurve()
    {
      return new Curve(native_.CopyMainCurve());
    }

    public void AddOverlay(Curve overlay, bool addPoint = true)
    {
      // The original native curve becomes the base curve.
      var baseCurve = native_;

      // Create a new native curve combining base and overlay.
      var curve = NativeCurve.Create(baseCurve.GetAsOf(),
        new MultiplicativeOverlay(baseCurve, overlay));
      curve.SetCcy(baseCurve.GetCcy());
      curve.SetName(baseCurve.GetName());
      curve.SetCategory(baseCurve.GetCategory());
      curve.SetFlags(baseCurve.GetFlags());
      if (addPoint) curve.Add(baseCurve.GetAsOf(), 1.0);
      native_ = curve;
    }

    public void RemoveOverlay()
    {
      if (native_ == null) return;
      var customInterp = native_.CustomInterpolator as MultiplicativeOverlay;
      if (customInterp == null) return;
      // Restore the original base curve.
      native_ = customInterp.BaseCurve;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class
    ///  with a custom curve interpolator.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="curveInterpolator">The curve interpolator.</param>
    public void Initialize(Dt asOf, ICurveInterpolator curveInterpolator)
    {
      native_ = NativeCurve.Create(asOf, curveInterpolator);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Curve"/> class
    ///  with a custom curve interpolator.
    /// </summary>
    /// <param name="curveInterpolator">The curve interpolator.</param>
    /// <remarks>
    ///   This mehod must be called immediately after the curve is constructed.
    ///   Otherwise, the behavior is underfined.
    /// </remarks>
    public void Initialize(ICurveInterpolator curveInterpolator)
    {
      native_ = NativeCurve.Create(native_ != null
        ? native_.GetAsOf()
        : Dt.Empty, curveInterpolator);
      // Let the custom interpolator to determine the rate conversion.
      native_.SetDayCount(DayCount.None);
    }
    #endregion

    #region Properties

    /// <summary>Name</summary>
    [Category("Base")]
    public string Name
    {
      get { return native_.GetName(); }
      set { native_.SetName(value); }
    }

    /// <summary>Category</summary>
    [Category("Base")]
    public string Category
    {
      get { return native_.GetCategory(); }
      set { native_.SetCategory(value); }
    }

    /// <summary>AsOf date for curve</summary>
    [Category("Base")]
    public Dt AsOf
    {
      get { return native_.GetAsOf(); }
      set { native_.SetAsOf(value); }
    }

    /// <summary>Currency of curve</summary>
    [Category("Base")]
    public Currency Ccy
    {
      get { return native_.GetCcy(); }
      set { native_.SetCcy(value); }
    }

    /// <summary>Interpolation method</summary>
    [Category("Base")]
    public Numerics.Interp Interp
    {
      get { return native_.GetInterp(); }
      set { native_.SetInterp(value); }
    }

    /// <summary>Interpolation method</summary>
    [Category("Base")]
    public Numerics.InterpMethod InterpMethod
    {
      get { return Numerics.InterpFactory.ToInterpMethod(native_.GetInterp()); }
    }

    /// <summary>Extrapolation method</summary>
    [Category("Base")]
    public Numerics.ExtrapMethod ExtrapMethod
    {
      get { return Numerics.InterpFactory.ToExtrapMethod(native_.GetInterp()); }
    }

    /// <summary>
    ///   Constant spread to be added to the curve.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is a convenience for fast parallel shifts in curves.</para>
    /// 
    ///   <para>The spread is by default the same terms as the daycount and
    ///   compounding frequency used for interpolation. This can be changed
    ///   by specifying the daycount and compounding frequency of the spread
    ///   directly.</para>
    /// </remarks>
    ///
    /// <example>
    ///   <code>
    ///   // Construct a discount curve with flat forward interpolation
    ///   Curve c = new Curve(new Dt(1,1,1999), Frequency.Continuous);
    ///
    ///   // Add a discount factor
    ///   c.Add(new Dt(1,2,1999), 0.99);
    ///
    ///   // Add a 5% continuously compounded spread
    ///   c.Spread = 0.05;
    ///
    ///   // Clear spread
    ///   c.Spread = 0.0;
    ///   </code>
    /// </example>
    [Category("Base")]
    public double Spread
    {
      get { return native_.GetSpread(); }
      set { native_.SetSpread(value); }
    }

    /// <summary>DayCount for interpolation rate compounding</summary>
    [Category("Base")]
    public DayCount DayCount
    {
      get { return native_.GetDayCount(); }
      set { native_.SetDayCount(value); }
    }

    /// <summary>Compounding interpolation rate frequency</summary>
    [Category("Base")]
    public Frequency Frequency
    {
      get { return native_.GetFrequency(); }
      set { native_.SetFrequency(value); }
    }

    /// <summary>Number of points in curve</summary>
    [Category("Base")]
    public int Count
    {
      get { return native_.Size(); }
    }

    /// <summary>Points property read/write interface</summary>
    [Category("Base")]
    public CurvePointArray Points
    {
      get { return new CurvePointArray(this); }
    }

    /// <summary>Points property read/write interface</summary>
    /// <exclude />
    [Category("Base")]
    public Dt JumpDate
    {
      get { return native_.Get_JumpDate(); }
      set { native_.Set_JumpDate(value); }
    }

    /// <summary>
    ///   Unique id for this curve. Used for sorting
    /// </summary>
    public long Id
    {
      get { return getCPtr(this).Handle.ToInt64(); }
    }

    /// <summary>
    /// Gets the custom interpolator.
    /// </summary>
    /// <value>The custom interpolator.</value>
    public ICurveInterpolator CustomInterpolator
    {
      get { return native_.CustomInterpolator; }
    }

    /// <summary>
    /// Curve flags
    /// </summary>
    public CurveFlags Flags
    {
      get { return (CurveFlags)native_.GetFlags(); }
      set { native_.SetFlags((int)value); }
    }

    /// <summary>
    /// Overlay curve
    /// </summary>
    public Curve Overlay
    {
      get { return native_.Overlay == null ? null : new Curve(native_.Overlay); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="Curve"/> is stressed.
    /// </summary>
    /// <value><c>true</c> if stressed; otherwise, <c>false</c>.</value>
    public bool Stressed
    {
      get
      {
        return (Flags & CurveFlags.Stressed) != 0;
      }
      set
      {
        if (value)
        {
          Flags |= CurveFlags.Stressed;
        }
        else
        {
          Flags &= ~CurveFlags.Stressed;
        }
      }
    }
    #endregion Properties

    #region IEquatable

    /// <summary>
    ///   IEquitable.Equals implementation
    /// </summary>
    public bool Equals(Curve other)
    {
      return (Id == other.Id);
    }

    #endregion IEquatable

    #region IComparable

    /// <summary>
    ///   IComparable.CompareTo implementation.
    /// </summary>
    public int CompareTo(Curve other)
    {
      long diff = Id - other.Id;
      return diff < 0 ? -1 : (diff > 0 ? 1 : 0);
    }

    #endregion IComparable

    #region IEnumerable<Pair<Dt,double>> Members

    /// <summary>
    /// Get curve point enumerator
    /// </summary>
    public IEnumerator<CurvePoint> GetEnumerator()
    {
      int count = Count;
      for (int i = -1; ++i < count; )
        yield return new CurvePoint(GetDt(i), GetVal(i));
    }

    #endregion

    #region IEnumerable Members

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      int count = Count;
      for (int i = -1; ++i < count; )
        yield return new CurvePoint(GetDt(i), GetVal(i));
    }

    #endregion

    #region IModelParameter Members
    /// <summary>
    /// Parameter value for rate/price with the given maturity tenor
    /// </summary>
    /// <param name="maturity">Tenor</param>
    /// <param name="strike">Strike</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <returns>Parameter</returns>
    /// <remarks>
    /// This corresponds to a flat(in strike) surface. 
    /// </remarks>
    double IModelParameter.Interpolate(Dt maturity, double strike, BaseEntity.Toolkit.Base.ReferenceIndices.ReferenceIndex referenceIndex)
    {
      return Interpolate(maturity);
    }

    #endregion
  }

  [Flags]
  public enum CurveFlags : uint
  {
    None = 0,
    Integrand = 1,
    Stressed = 8,
    SmoothTime = 16,
    Internal = 32, // indicate an public curve
  }
}
