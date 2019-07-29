/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Models
{
  [Serializable]
  public class BlackScholesParameterData : IBlackScholesParameterData
  {
    private readonly double _r1, _r2;
    private readonly double _spot;
    private readonly double _time;

    public BlackScholesParameterData(double time, double spot, double r1, double r2, double shift = 0)
    {
      _time = time;
      _spot = spot;
      _r1 = r1;
      _r2 = r2;
      Shift = shift;
    }

    #region IBlackScholesParameterData Members

    public double Time
    {
      get { return _time; }
    }

    public double Spot
    {
      get { return _spot; }
    }

    public double Rate1
    {
      get { return _r1; }
    }

    public double Rate2
    {
      get { return _r2; }
    }

    public double Shift { get; }

    #endregion
  }
}
