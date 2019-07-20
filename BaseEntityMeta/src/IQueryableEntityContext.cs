// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// An <see cref="IEntityContext"/> that can be queried using LINQ.
  /// </summary>
  public interface IQueryableEntityContext : IEntityContext
  {
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    IOrderedQueryable<T> Query<T>();

    /// <summary>
    /// Find the entity (if any) with the specified business key
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="keyList"></param>
    /// <returns></returns>
    PersistentObject FindByKey(ClassMeta cm, IList<object> keyList);
  }
}