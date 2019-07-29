// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Calibrators.Volatilities;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Interface IVolatilitySurfaceProvider
  /// </summary>
  public interface IVolatilitySurfaceProvider
  {
    /// <summary>
    /// Gets the volatility surfaces.
    /// </summary>
    /// <returns>IEnumerable{CalibratedVolatilitySurface}.</returns>
    IEnumerable<IVolatilitySurface> GetVolatilitySurfaces();
  }
}
