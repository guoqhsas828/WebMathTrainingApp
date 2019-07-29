using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Cashflows.Expressions;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Struct SimulationModelChoice
  /// </summary>
  public struct SimulationModelChoice
  {
    /// <summary>
    /// Gets the type of the RNG.
    /// </summary>
    /// <value>The type of the RNG.</value>
    public MultiStreamRng.Type RngType { get; }
    /// <summary>
    /// Gets the sde model.
    /// </summary>
    /// <value>The sde model.</value>
    public ISimulationModel SdeModel => _sdeModel?? SimulationModels.LiborMarketModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationModelChoice"/> struct.
    /// </summary>
    /// <param name="rngType">Type of the RNG.</param>
    /// <param name="sdeModel">The sde model.</param>
    public SimulationModelChoice(
      MultiStreamRng.Type rngType,
      ISimulationModel sdeModel = null)
    {
      RngType = rngType;
      _sdeModel = sdeModel;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="MultiStreamRng.Type"/> to <see cref="SimulationModelChoice"/>.
    /// </summary>
    /// <param name="rngType">Type of the RNG.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator SimulationModelChoice(
      MultiStreamRng.Type rngType)
    {
      return new SimulationModelChoice(rngType);
    }

    /// <summary>
    /// The sde model
    /// </summary>
    private readonly ISimulationModel _sdeModel;
  }
}
