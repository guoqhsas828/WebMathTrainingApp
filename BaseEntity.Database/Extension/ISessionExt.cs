using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Metadata;

namespace BaseEntity.Database.Extension
{
  using System.Linq.Expressions;

  using NHibernate;

  /// <summary>
  /// 
  /// </summary>
  public static class ISessionExt
  {
    /// <summary>
    /// Creates the caching repository.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="session">The session.</param>
    /// <returns></returns>
    public static IRepository<T> CreateCachingRepository<T>(this NHibernateEntityContext session) where T : PersistentObject
    {
      return CreateCachingRepository<T>(session, t => true);
    }

    /// <summary>
    /// Creates the caching repository.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="session">The session.</param>
    /// <param name="criteria">The criteria.</param>
    /// <returns></returns>
    public static IRepository<T> CreateCachingRepository<T>(this NHibernateEntityContext session, Expression<Func<T, bool>> criteria) where T : PersistentObject
    {
      return new CachingRepository<T>(new DirectRepository<T>(session), criteria);
    } 
  }
}
