using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  interface ICurveFitAction
  {
    object PreProcess(CalibratedCurve curve);
    void PostProcess(object state, CalibratedCurve curve);
  }

  internal class PostAttachOverlayAction : ICurveFitAction
  {
    private readonly Curve _overlayCurve;

    public PostAttachOverlayAction(Curve overlayCurve)
    {
      _overlayCurve = overlayCurve;
    }

    public object PreProcess(CalibratedCurve curve)
    {
      var overlayCurve = _overlayCurve;
      if (overlayCurve == null || overlayCurve.NativeCurve != curve.NativeCurve.Overlay)
      {
        return null;
      }
      // save a copy of the overlay curve
      var saved = overlayCurve.clone();
      // flatten the original overlay curve to be 1.0 always
      overlayCurve.Clear();
      overlayCurve.Interp = new Flat();
      overlayCurve.Add(overlayCurve.AsOf + 365, 1.0);
      // return the clone
      return saved;
    }

    public void PostProcess(object state, CalibratedCurve curve)
    {
      var saved = state as Curve;
      if (saved == null || _overlayCurve == null)
        return;
      // restore the original overlay curve
      _overlayCurve.Set(saved);
    }
  }

  /// <summary>
  ///   Simple collection contains a sequence of actions.
  /// </summary>
  internal class CurveFitActionCollection : ICurveFitAction, ICollection<ICurveFitAction>
  {
    private readonly List<ICurveFitAction> _list = new List<ICurveFitAction>();

    #region ICurveFitAction implementation
    public object PreProcess(CalibratedCurve curve)
    {
      return _list.Select(a => a.PreProcess(curve)).ToList();
    }

    public void PostProcess(object state, CalibratedCurve curve)
    {
      var stateObjs = state as IList<object>;
      if (stateObjs == null || stateObjs.Count != _list.Count)
        throw new ToolkitException("Inconsistent PreProcess and PostProcess calls");
      // call post precess in reverse order
      for (int i = _list.Count; --i >= 0;)
        _list[i].PostProcess(stateObjs[i], curve);
    }
    #endregion

    #region ICollection<ICurveFitAction> implementation

    public void Add(ICurveFitAction item)
    {
      _list.Add(item);
    }

    public void Clear()
    {
      _list.Clear();
    }

    public bool Contains(ICurveFitAction item)
    {
      return _list.Contains(item);
    }

    public void CopyTo(ICurveFitAction[] array, int arrayIndex)
    {
      _list.CopyTo(array, arrayIndex);
    }

    public int Count
    {
      get { return _list.Count; }
    }

    public bool IsReadOnly
    {
      get { return false; }
    }

    public bool Remove(ICurveFitAction item)
    {
      return _list.Remove(item);
    }

    public IEnumerator<ICurveFitAction> GetEnumerator()
    {
      return _list.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    #endregion
  }
}
