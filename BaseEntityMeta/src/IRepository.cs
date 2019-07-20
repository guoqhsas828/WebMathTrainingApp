// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IRepository.cs" company="WebMathTraining, Inc">
//   (c) 2011 WebMathTraining, Inc
// </copyright>
// <summary>
//   An interface for a repository of items that can be created, read, updated and deleted
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// An interface for a repository of items that can be created, read, updated and deleted
  /// </summary>
  /// <typeparam name="T">
  /// The type of objects in the repository
  /// </typeparam>
  public interface IRepository<T>
  {
    #region Public Properties

    /// <summary>
    ///   Gets the items in the repository (query).
    /// </summary>
    /// <value>The items (query).</value>
    IQueryable<T> Items { get; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Begin updates to the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    void BeginUpdate(T item);

    /// <summary>
    /// Creates an item.
    /// </summary>
    /// <returns>
    /// The newly created item
    /// </returns>
    T Create();

    /// <summary>
    /// Deletes the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    void Delete(T item);

    /// <summary>
    /// Saves the specified item.
    /// </summary>
    /// <param name="item">
    /// The item.
    /// </param>
    void Save(T item);

    #endregion
  }
}