using System.Collections.Generic;
using System.Diagnostics;

namespace BaseEntity.Toolkit.Cashflows
{

  #region Fixing

  /// <summary>
  /// Class containing fixing information
  /// </summary>
  [DebuggerDisplay("{Value}, {RateResetState}")]
  public class Fixing
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    public Fixing()
    {
      Forward = 0.0;
      RateResetState = RateResetState.IsProjected;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Forward projection
    /// </summary>
    public double Forward { get; set; }

    /// <summary>
    /// Rate reset state of the projection
    /// </summary>
    public RateResetState RateResetState { get; set; }

    /// <summary>
    /// Gets the fixing value
    /// </summary>
    /// <value>The fixing value</value>
    public double Value { get { return Forward; } }

    /// <summary>
    /// Gets the fixing state
    /// </summary>
    /// <value>The fixing value</value>
    public RateResetState State { get { return RateResetState; } }

    #endregion
  }

  #endregion

  #region Derived Fixing

  /// <summary>
  /// Swap rate fixing
  /// </summary>
  public class SwapRateFixing : Fixing
  {
    /// <summary>
    /// Annuity
    /// </summary>
    public double Annuity { get; set; }
  }

  /// <summary>
  /// Fixing for yoy rate
  /// </summary>
  public class InflationRateFixing : Fixing
  {
    /// <summary>
    /// Reset state of numerator
    /// </summary>
    public RateResetState NumeratorResetState { get; set; }
    /// <summary>
    /// Reset state of denominator
    /// </summary>
    public RateResetState DenominatorResetState { get; set; }
    /// <summary>
    /// Inflation at payment date
    /// </summary>
    public double InflationAtPayDt { get; set; }

    /// <summary>
    /// Inflation at previous payment date
    /// </summary>
    public double InflationAtPreviousPayDt { get; set; }
  }

  /// <summary>
  /// KFactor Fixing built from a series of inflation rate fixings
  /// </summary>
  public class InflationKFactorFixing : Fixing
  {
    /// <summary>
    /// List of rate fixings
    /// </summary>
    public List<InflationRateFixing> RateFixings { get; set; } 
  }

  /// <summary>
  /// Fixing for an averaged rate
  /// </summary>
  public class AveragedRateFixing : Fixing
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <returns></returns>
    public AveragedRateFixing()
    {
      Components = new List<double>();
      ResetStates = new List<RateResetState>();
    }

    /// <summary>
    /// Components of the average
    /// </summary>
    public List<double> Components { get; private set; }

    /// <summary>
    /// Reset state for each component
    /// </summary>
    public List<RateResetState> ResetStates { get; private set; }
  }

  #endregion
}