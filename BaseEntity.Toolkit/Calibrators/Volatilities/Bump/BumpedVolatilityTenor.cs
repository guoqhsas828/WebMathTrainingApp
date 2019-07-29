using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.Bump
{
  [Serializable]
  public sealed class BumpedVolatilityTenor
  {
    private readonly double[] _originalValues;
    public readonly IVolatilityTenor Tenor;
    public BumpedVolatilityTenor(IVolatilityTenor tenor)
    {
      Tenor = tenor;
      var vols = tenor.QuoteValues;
      _originalValues = new double[vols.Count];
      for (int i = vols.Count; --i >= 0; )
        _originalValues[i] = vols[i];
    }
    public double Bump(double bumpSize, BumpFlags flags)
    {
      return Tenor.Bump(_originalValues, bumpSize,
        (flags & BumpFlags.BumpRelative) != 0,
        (flags & BumpFlags.BumpDown) == 0);
    }
    public void Restore()
    {
      var targets = Tenor.QuoteValues;
      int n = targets.Count;
      if (n != _originalValues.Length)
      {
        throw new ToolkitException("Unexpected change of volatility count");
      }
      for (int i = n; --i >= 0;)
        targets[i] = _originalValues[i];
    }
  }
}
