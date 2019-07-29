using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Swaption model type
  /// </summary>
  public enum SwaptionModelType
  {
    /// <summary>
    /// Flag of the standard market model for physical-settled swaption
    /// </summary>
    PhysicalStandard,

    /// <summary>
    /// Flag of the standard market model for cash-settled swaption
    /// </summary>
    CashStandard,

    /// <summary>
    /// Flag of linear terminal swap rate model for cash-settled swaption
    /// </summary>
    LinearTerminalSwapRate,
  }
}
