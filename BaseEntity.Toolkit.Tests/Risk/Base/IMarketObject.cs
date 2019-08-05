using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Risk
{
  /// <summary>
  /// An interface for an object that can bind a market object to a market environment
  /// </summary>
  public interface IMarketObjectBinder
  {
    ///// <summary>
    ///// Bind a market object to the market environment
    ///// </summary>
    ///// <param name="marketEnv">The market environment</param>
    ///// <param name="marketKey">The key of the bound market object</param>
    ///// <returns>The bound market object</returns>
    //IMarketObject Bind(MarketEnvironment marketEnv, MarketKey marketKey);
  }
  /// <summary>
  /// An interface for a market object that can both bind, and be bound, to a market environment
  /// </summary>
  public interface IMarketObject : IMarketObjectBinder
  {
    ///// <summary>
    ///// Gets the market key of the market object within the market environment.
    ///// </summary>
    ///// <value>The market key.</value>
    //MarketKey MarketKey { get; }
  }
}
