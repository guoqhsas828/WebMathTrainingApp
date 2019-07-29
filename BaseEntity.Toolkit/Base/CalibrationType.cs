using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Methods for calibrating the model to the market.
  /// </summary>
	public enum CalibrationType
	{
    /// <summary>
    /// Perform no calibration
    /// </summary>
    None, 

    /// <summary>
    /// Shifts the DiscountCurve
    /// </summary>
    DiscountCurve, 

    /// <summary>
    /// Shifts the SurvivalCurve
    /// </summary>
    SurvivalCurve
	}
}
