using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  ///  Represent numerical options to control BGM tree calculations.
  /// </summary>
  [Serializable]
  public class BgmTreeOptions
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BgmTreeOptions"/> class.
    /// </summary>
    public BgmTreeOptions()
    {
      Adaptive = true;
    }

    /// <summary>
    ///  Gets/sets the number of steps to the first tree node
    /// </summary>
    public int InitialSteps { get; set; }

    /// <summary>
    ///  Gets/sets the number of steps between intermediate tree nodes
    /// </summary>
    public int MiddleSteps { get; set; }

    /// <summary>
    ///  Gets/sets the tolerance of swaption PV differences in tree calibration
    /// </summary>
    public double CalibrationTolerance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether numerical option should
    /// be adaptive if not set.
    /// </summary>
    /// <value><c>true</c> if adaptive; otherwise, <c>false</c>.</value>
    public bool Adaptive { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether to use the arbitrage free tree model.
    /// </summary>
    /// <value><c>true</c> if [arbitrage free tree]; otherwise, <c>false</c>.</value>
    public bool? ArbitrageFreeTree { get; set; }

    /// <summary>
    /// Gets or sets the calibration periods.
    /// </summary>
    /// <value>The calibration periods</value>
    public IList<IOptionPeriod> CalibrationPeriods { get; set; }

    internal bool HasOptionsForInitialStep
    {
      get { return InitialSteps > 0 && CalibrationTolerance > 0; }
    }

    internal bool HasOptionsForMiddleSteps
    {
      get { return MiddleSteps > 0 && CalibrationTolerance > 0; }
    }

  }

}
