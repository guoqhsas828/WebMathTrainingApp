using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  [Serializable]
  public class SequentialInterpolator2D
  {
    public SequentialInterpolator2D(Func<double, SmileInputKind, double>[] fs,
      double[] ts, Interp interp)
    {
      Functions = fs;
      Points = ts;
      Interp = interp;
    }

    public double Evaluate(double t, double x, SmileInputKind xkind)
    {
      var ts = Points;
      int last = ts.Length - 1;
      var index = GetIndex(t);
      if (index > last) index = last;
      if (ts[index].IsAlmostSameAs(t))
        return Functions[index](x, xkind);
      double[] ys = Functions.Select(f => f(x, xkind)).ToArray();
      var interpolator = new Interpolator(Interp, ts, ys);
      return interpolator.evaluate(t);
    }

    public int GetIndex(double t)
    {
      var index = Array.BinarySearch(Points, t);
      return index < 0 ? (~index) : index;
    }

    public Func<double, SmileInputKind, double>[] Functions { get; }

    public double[] Points { get; }

    public Interp Interp { get; }
  }
}
