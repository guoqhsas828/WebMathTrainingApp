// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICachingRepository.cs" company="WebMathTraining, Inc">
//   (c) 2011 WebMathTraining, Inc
// </copyright>
// <summary>
//   An interface for a repository of items that can be created, read, updated and deleted, and tracks changes
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// An interface for a repository of items that can be created, read, updated and deleted, and tracks changes to the collection
  /// </summary>
  /// <typeparam name="T">
  /// The type of objects in the repository
  /// </typeparam>
  public interface ICachingRepository<T> : IRepository<T>
  {
    #region Public Properties

    /// <summary>
    ///   Gets the removed items.
    /// </summary>
    /// <value>The removed items.</value>
    IEnumerable<T> DeletedItems { get; }

    /// <summary>
    ///   Gets the inserted items.
    /// </summary>
    /// <value>The inserted items.</value>
    IEnumerable<T> InsertedItems { get; }

    /// <summary>
    ///   Gets the updated items.
    /// </summary>
    /// <value>The updated items.</value>
    IEnumerable<T> UpdatedItems { get; }

    #endregion
  }
}