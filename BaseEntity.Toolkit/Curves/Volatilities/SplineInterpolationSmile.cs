using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  [Serializable]
  public class SplineInterpolationSmile
  {
    private readonly Interpolator _interpolator;
    private readonly double _forward;
    private readonly SmileInputKind _interpSpace;

    private SplineInterpolationSmile(Interpolator interpolator,
      double forward, SmileInputKind kind)
    {
      _interpolator = interpolator;
      _forward = forward;
      _interpSpace = kind;
    }

    public static SplineInterpolationSmile Create(
      double atmFoward,
      SmileInputKind interpSpace,
      Interp interp,
      IEnumerable<StrikeVolatilityPair> data)
    {
      var x = new List<double>();
      var y = new List<double>();
      switch (interpSpace)
      {
      case SmileInputKind.Strike:
        foreach (var p in data)
        {
          x.Add(p.Strike);
          y.Add(p.Volatility);
        }
        break;
      case SmileInputKind.Moneyness:
        foreach (var p in data)
        {
          x.Add(p.Strike / atmFoward);
          y.Add(p.Volatility);
        }
        break;
      case SmileInputKind.LogMoneyness:
        foreach (var p in data)
        {
          x.Add(Math.Log(p.Strike / atmFoward));
          y.Add(p.Volatility);
        }
        break;
      default:
        throw new ToolkitException(String.Format(
          "Invalid interpolation space {0}", interpSpace));
      }
      if (x.Count == 0) return null;

      // Create ordered x-y pairs.
      var xy = x.Zip(y, (xi, yi) => new {X = xi, Y = yi})
        .OrderBy(o => o.X).ToList();
      return new SplineInterpolationSmile(
        new Interpolator(interp,
          xy.Select(o => o.X).ToArray(),
          xy.Select(o => o.Y).ToArray()),
        atmFoward, interpSpace);
    }

    public double Evaluate(double x)
    {
      return _interpolator.evaluate(x);
    }

    public double Evaluate(double x, SmileInputKind kind)
    {
      if (kind == _interpSpace)
        return _interpolator.evaluate(x);
      switch(_interpSpace)
      {
      case SmileInputKind.Strike:
        return _interpolator.evaluate(kind == SmileInputKind.Moneyness
          ? (x * _forward)
          : (Math.Exp(x) * _forward));
      case SmileInputKind.Moneyness:
        return _interpolator.evaluate(kind == SmileInputKind.Strike
          ? (x / _forward)
          : Math.Exp(x));
      case SmileInputKind.LogMoneyness:
        return _interpolator.evaluate(kind == SmileInputKind.Strike
          ? Math.Log(x / _forward)
          : Math.Log(x));
      }
      throw new ToolkitException(String.Format(
        "Invalid SmileInputKind {0}", kind));
    }
  }
}
