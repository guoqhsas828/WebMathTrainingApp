// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.SqlCommand;
using NHibernate.Transform;
using BaseEntity.Database.Engine;
using System.Threading;
using System.Threading.Tasks;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class CriteriaProxy : ICriteria
  {
    private readonly ICriteria _impl;
    private readonly AuditInterceptor _interceptor;

    public CriteriaProxy(ICriteria impl, AuditInterceptor interceptor)
    {
      _impl = impl;
      _interceptor = interceptor;
    }

    public object Clone()
    {
      return new CriteriaProxy((ICriteria)_impl.Clone(), _interceptor);
    }

    public ICriteria SetProjection(params IProjection[] projection)
    {
      _impl.SetProjection(projection);
      return this;
    }

    public ICriteria Add(ICriterion expression)
    {
      _impl.Add(expression);
      return this;
    }

    public ICriteria AddOrder(Order order)
    {
      _impl.AddOrder(order);
      return this;
    }

    public ICriteria SetFetchMode(string associationPath, FetchMode mode)
    {
      _impl.SetFetchMode(associationPath, mode);
      return this;
    }

    public ICriteria SetLockMode(LockMode lockMode)
    {
      _impl.SetLockMode(lockMode);
      return this;
    }

    public ICriteria SetLockMode(string alias, LockMode lockMode)
    {
      _impl.SetLockMode(alias, lockMode);
      return this;
    }

    public ICriteria CreateAlias(string associationPath, string alias)
    {
      _impl.CreateAlias(associationPath, alias);
      return this;
    }

    public ICriteria CreateAlias(string associationPath, string alias, JoinType joinType)
    {
      _impl.CreateAlias(associationPath, alias, joinType);
      return this;
    }

    public ICriteria CreateAlias(string associationPath, string alias, JoinType joinType, ICriterion withClause)
    {
      _impl.CreateAlias(associationPath, alias, joinType, withClause);
      return this;
    }

    public ICriteria CreateCriteria(string associationPath)
    {
      return new CriteriaProxy(_impl.CreateCriteria(associationPath), _interceptor);
    }

    public ICriteria CreateCriteria(string associationPath, JoinType joinType)
    {
      return new CriteriaProxy(_impl.CreateCriteria(associationPath, joinType), _interceptor);
    }

    public ICriteria CreateCriteria(string associationPath, string alias)
    {
      return new CriteriaProxy(_impl.CreateCriteria(associationPath, alias), _interceptor);
    }

    public ICriteria CreateCriteria(string associationPath, string alias, JoinType joinType)
    {
      return new CriteriaProxy(_impl.CreateCriteria(associationPath, alias, joinType), _interceptor);
    }

    public ICriteria CreateCriteria(string associationPath, string alias, JoinType joinType, ICriterion withClause)
    {
      return new CriteriaProxy(_impl.CreateCriteria(associationPath, alias, joinType, withClause), _interceptor);
    }

    public ICriteria SetResultTransformer(IResultTransformer resultTransformer)
    {
      _impl.SetResultTransformer(resultTransformer);
      return this;
    }

    public ICriteria SetMaxResults(int maxResults)
    {
      _impl.SetMaxResults(maxResults);
      return this;
    }

    public ICriteria SetFirstResult(int firstResult)
    {
      _impl.SetFirstResult(firstResult);
      return this;
    }

    public ICriteria SetFetchSize(int fetchSize)
    {
      _impl.SetFetchSize(fetchSize);
      return this;
    }

    public ICriteria SetTimeout(int timeout)
    {
      _impl.SetTimeout(timeout);
      return this;
    }

    public ICriteria SetCacheable(bool cacheable)
    {
      _impl.SetCacheable(cacheable);
      return this;
    }

    public ICriteria SetCacheRegion(string cacheRegion)
    {
      _impl.SetCacheRegion(cacheRegion);
      return this;
    }

    public ICriteria SetComment(string comment)
    {
      _impl.SetComment(comment);
      return this;
    }

    public ICriteria SetFlushMode(FlushMode flushMode)
    {
      _impl.SetFlushMode(flushMode);
      return this;
    }

    public ICriteria SetCacheMode(CacheMode cacheMode)
    {
      _impl.SetCacheMode(cacheMode);
      return this;
    }

    public IList List()
    {
      var result = _impl.List();
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task<IList> ListAsync(CancellationToken token)
    {
      var result =  await _impl.ListAsync(token);
      _interceptor.RollbackEvents();

      return result;
    }

    public object UniqueResult()
    {
      var result = _impl.UniqueResult();
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task<object> UniqueResultAsync(CancellationToken token)
    {
      var result = await _impl.UniqueResultAsync(token);
      _interceptor.RollbackEvents();
      return result;
    }

    public IFutureEnumerable<T> Future<T>()
    {
      return _impl.Future<T>();
    }

    public IFutureValue<T> FutureValue<T>()
    {
      return _impl.FutureValue<T>();
    }

    public ICriteria SetReadOnly(bool readOnly)
    {
      _impl.SetReadOnly(readOnly);
      return this;
    }

    public void List(IList results)
    {
      _impl.List(results);
      _interceptor.RollbackEvents();
    }

    public async Task ListAsync(IList results, CancellationToken token)
    {
      await _impl.ListAsync(results, token);
      _interceptor.RollbackEvents();
     
    }

    public IList<T> List<T>()
    {
      var result = _impl.List<T>();
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task<IList<T>> ListAsync<T>(CancellationToken token)
    {
      var result = await _impl.ListAsync<T>(token);
      _interceptor.RollbackEvents();
      return result;
    }

    public T UniqueResult<T>()
    {
      var result = _impl.UniqueResult<T>();
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task<T> UniqueResultAsync<T>(CancellationToken token)
    {
      var result = await _impl.UniqueResultAsync<T>();

      _interceptor.RollbackEvents();

      return result;
    }

    public void ClearOrders()
    {
      _impl.ClearOrders();
    }

    public ICriteria GetCriteriaByPath(string path)
    {
      return _impl.GetCriteriaByPath(path);
    }

    public ICriteria GetCriteriaByAlias(string alias)
    {
      return _impl.GetCriteriaByAlias(alias);
    }

    public Type GetRootEntityTypeIfAvailable()
    {
      return _impl.GetRootEntityTypeIfAvailable();
    }

    public string Alias
    {
      get { return _impl.Alias; }
    }

    public bool IsReadOnlyInitialized
    {
      get { return _impl.IsReadOnlyInitialized; }
    }

    public bool IsReadOnly
    {
      get { return _impl.IsReadOnly; }
    }
  }
}