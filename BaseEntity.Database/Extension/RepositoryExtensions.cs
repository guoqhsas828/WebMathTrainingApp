// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RepositoryExtensions.cs" company="WebMathTraining, Inc">
//   (c) 2011 WebMathTraining, Inc
// </copyright>
// <summary>
//   Repository extensions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using BaseEntity.Metadata;

namespace BaseEntity.Database.Extension
{
  using System;
  using System.Linq.Expressions;

  /// <summary>
  /// Repository extensions
  /// </summary>
  public static class RepositoryExtensions
  {
    #region Public Methods

    /// <summary>
    /// Upserts an item in the specified repository.
    /// </summary>
    /// <typeparam name="T">
    /// The type of item to upsert
    /// </typeparam>
    /// <param name="repository">
    /// The repository.
    /// </param>
    /// <param name="criteria">
    /// The criteria.
    /// </param>
    /// <returns>
    /// An upserter for the appropriate type
    /// </returns>
    public static Upserter<T> Upsert<T>(this IRepository<T> repository, Expression<Func<T, bool>> criteria)
      where T : PersistentObject
    {
      return new Upserter<T>(criteria, repository);
    }

    #endregion
  }
}