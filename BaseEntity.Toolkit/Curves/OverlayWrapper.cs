using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  class OverlayWrapper
  {
    private OverlayWrapper(CalibratedCurve main,
      Native.Curve toFit, Native.Curve baseCurve)
    {
      _main = main;
      _toFit = toFit;
      _base = baseCurve;
    }

    public OverlayWrapper(CalibratedCurve curve)
    {
      _main = curve;
      var overlay = curve.ShiftOverlay;
      var interp = curve.CustomInterpolator as MultiplicativeOverlay;
      if (overlay == null || interp == null)
      {
        // _toFit is never null, while _base can be null for non-overlapped.
        _toFit = _main.NativeCurve;
        return;
      }
      _base = interp.BaseCurve;
      _toFit = interp.OverlayCurve;
    }

    public OverlayWrapper Clone()
    {
      return new OverlayWrapper(_main, _toFit.clone(), _base);
    }

    public void Clear()
    {
      new Curve(_toFit).Clear();
    }

    public void Shrink(int size)
    {
      _toFit.Shrink(size);
    }

    public void Set(Curve curve)
    {
      _toFit.Clear();
      for (int i = 0, n = curve.Count; i < n; ++i)
      {
        var dt = curve.GetDt(i);
        _toFit.Add(dt, curve.GetVal(i) / BaseCurveValue(dt));
      }
    }

    public void Set(int index, Dt date, double value)
    {
      _toFit.Set(index, date, value / BaseCurveValue(date));
    }

    public void SetVal(int index, double value)
    {
      var date = _toFit.GetDt(index);
      Set(index, date, value);
    }

    public void Set(Dt[] dates, double[] values)
    {
      if(dates==null || values==null)
      {
        throw new ToolkitException("Null dates or values.");
      }
      double[] vals = new double[values.Length];
      for(int i = 0; i < values.Length;++i)
      {
        vals[i] = values[i] / BaseCurveValue(dates[i]);
      }
      _toFit.Set(dates, vals);
    }

    public void Add(Dt date, double val)
    {
      _toFit.Add(date, val / BaseCurveValue(date));
    }

    public Dt GetDt(int index)
    {
      return _toFit.GetDt(index);
    }

    public double GetVal(int index)
    {
      return _base != null
        ? _base.GetVal(index) * _toFit.GetVal(index)
        : _toFit.GetVal(index);
    }

    public double Interpolate(Dt date)
    {
      return _base != null
        ? _base.Interpolate(date)*_toFit.Interpolate(date)
        : _toFit.Interpolate(date);
    }

    public int After(Dt date)
    {
      return _toFit.After(date);
    }

    public double DiscountFactor(Dt date)
    {
      return _toFit.Interpolate(date) * BaseCurveValue(date);
    }

    public void SetRate(int index, double rate)
    {
      _toFit.ClearCache();
      _toFit.SetRate(index, rate - BaseCurveRate(GetDt(index)));
    }

    public double GetRate(int index)
    {
      return _toFit.GetY(index) + BaseCurveRate(GetDt(index));
    }

    private double BaseCurveValue(Dt dt)
    {
      return _base == null ? 1.0 : _base.Interpolate(dt);
    }

    private double BaseCurveRate(Dt dt)
    {
      return _base == null ? 0.0
        : Math.Log(_base.Interpolate(dt)) / (dt - AsOf);
    }

    #region Properties

    public Dt AsOf { get { return _main.AsOf; } }
    public int Count { get { return _toFit.Size(); } }
    public Curve CurveToFit { get { return new Curve(_toFit); } }
    public CalibratedCurve Main {get { return _main; }}

    public CurveTenorCollection Tenors
    {
      get { return _main.Tenors; } 
    }

    public Defaulted Defaulted
    {
      get
      {
        var sc = _main as SurvivalCurve;
        return sc == null ? Defaulted.NotDefaulted : sc.Defaulted;
      }
    }

    public Currency Ccy
    {
      get { return _main.Ccy; }
      set { _main.Ccy = value; }
    }

    public Numerics.Interp Interp
    {
      get { return _main.Interp; }
      set { _main.Interp = value; }
    }

    public string Name
    {
      get { return _main.Name; }
      set { _main.Name = value; }
    }

    //in the curve, this is internal//
    internal CurveFlags Flags
    {
      get { return _main.Flags; }
      set { _main.Flags = value; }
    }

    #endregion

    #region Data

    private CalibratedCurve _main;
    private Native.Curve _toFit; 
    private Native.Curve _base;

    #endregion
  }
}
