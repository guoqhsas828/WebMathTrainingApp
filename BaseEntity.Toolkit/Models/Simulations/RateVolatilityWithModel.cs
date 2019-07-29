// 
// 
// 
using System.Diagnostics;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  ///  The data structure combining the volatility data with the calibration model
  /// </summary>
  public struct RateVolatilityWithModel
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="RateVolatilityWithModel" /> struct.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="volatility">The volatility.</param>
    public RateVolatilityWithModel(
      IRateVolatilityCalibrationModel model,
      object volatility)
    {
      Debug.Assert(model != null);
      Debug.Assert(volatility != null);
      Volatility = volatility;
      Model = model;
    }

    /// <summary>
    /// Gets the volatility.
    /// </summary>
    /// <value>The volatility.</value>
    public object Volatility { get; }

    /// <summary>
    /// Gets the model.
    /// </summary>
    /// <value>The model.</value>
    public IRateVolatilityCalibrationModel Model { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    /// <value><c>true</c> if this instance is empty; otherwise, <c>false</c>.</value>
    public bool IsEmpty => Volatility == null || Model == null;

    /// <summary>
    /// The empty volatility
    /// </summary>
    public static readonly RateVolatilityWithModel Empty
      = new RateVolatilityWithModel();
  }
}
