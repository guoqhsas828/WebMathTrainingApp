using System.Collections;
using System.Linq;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Utility methods for <see cref="Strategy">Strategy class</see>.
  /// </summary>
  public class StrategyUtil : ObjectFactory<Strategy>
  {
    #region Query

    /// <summary>
    ///   Get strategy by id
    /// </summary>
    ///
    /// <param name="id">Id for strategy to retrieve</param>
    ///
    /// <returns>Strategy matching specified id</returns>
    ///
    public static Strategy FindById(long id)
    {
      return (Strategy)EntityContext.Current.Get(id);
    }

    /// <summary>
    ///   Get strategy by name
    /// </summary>
    ///
    /// <param name="name">Name of strategy</param>
    ///
    /// <returns>Strategy matching specified name</returns>
    ///
    public static Strategy FindByName(string name)
    {
      return ((IQueryableEntityContext)EntityContext.Current).Query<Strategy>().SingleOrDefault(s => s.Name == name);
    }

    /// <summary>
    ///   Get all strategies
    /// </summary>
    ///
    /// <returns>List of all strategies</returns>
    ///
    public static IList FindAll()
    {
      return ((IQueryableEntityContext)EntityContext.Current).Query<Strategy>().ToList();
    }

    #endregion Query

    #region Factory

    /// <summary>
    ///   Create a new strategy
    /// </summary>
    ///
    /// <returns>Created Strategy</returns>
    ///
    public static Strategy CreateInstance()
    {
      return Create();
    }

    /// <summary>
    ///   Create a new strategy
    /// </summary>
    ///
    /// <param name="name">Name of strategy</param>
    /// <param name="description">Description of strategy</param>
    ///
    /// <returns>Created Strategy</returns>
    ///
    public static Strategy CreateInstance(string name, string description)
    {
      Strategy strategy = Create();

      strategy.Name = name;
      strategy.Description = description;

      return strategy;
    }

    #endregion
  }
}