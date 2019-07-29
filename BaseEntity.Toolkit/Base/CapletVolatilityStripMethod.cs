using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	/// Indicates the different methods for stripping caplet volatilities 
	/// </summary>
  public enum CapletVolatilityStripMethod
	{
    /// <summary>Piecewise Constant</summary>
    PiecewiseConstant,
    /// <summary>Piecewise Linear</summary>
    PiecewiseLinear,
    /// <summary>Piecewise Quadratic  </summary>
    PiecewiseQuadratic
	}
}
