// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BaseEntity.Database.Engine;

namespace BaseEntity.Database
{
  internal class QueryableProxy : IOrderedQueryable
  {
    private readonly IQueryable _impl;
    private readonly AuditInterceptor _interceptor;

    public QueryableProxy(IQueryable impl, AuditInterceptor interceptor)
    {
      _impl = impl;
      _interceptor = interceptor;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      var result = _impl.GetEnumerator();
      _interceptor.RollbackEvents();
      return result;
    }

    public Expression Expression
    {
      get { return _impl.Expression; }
    }

    public Type ElementType
    {
      get { return _impl.ElementType; }
    }

    public IQueryProvider Provider
    {
      get { return new QueryProviderProxy(_impl.Provider, _interceptor); }
    }
  }

  internal class QueryableProxy<T> : IOrderedQueryable<T>
  {
    private readonly IQueryable<T> _impl;
    private readonly AuditInterceptor _interceptor;

    public QueryableProxy(IQueryable<T> impl, AuditInterceptor interceptor)
    {
      _impl = impl;
      _interceptor = interceptor;
    }

    public IEnumerator<T> GetEnumerator()
    {
      var result = _impl.GetEnumerator();
      _interceptor.RollbackEvents();
      return result;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    public Expression Expression
    {
      get { return _impl.Expression; }
    }

    public Type ElementType
    {
      get { return _impl.ElementType; }
    }

    public IQueryProvider Provider
    {
      get { return new QueryProviderProxy(_impl.Provider, _interceptor); }
    }
  }

  internal class QueryProviderProxy : IQueryProvider
  {
    private readonly IQueryProvider _impl;
    private readonly AuditInterceptor _interceptor;

    public QueryProviderProxy(IQueryProvider impl, AuditInterceptor interceptor)
    {
      _impl = impl;
      _interceptor = interceptor;
    }

    public IQueryable CreateQuery(Expression expression)
    {
      return new QueryableProxy(_impl.CreateQuery(expression), _interceptor);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
      return new QueryableProxy<TElement>(_impl.CreateQuery<TElement>(expression), _interceptor);
    }

    public object Execute(Expression expression)
    {
      var result = _impl.Execute(expression);
      _interceptor.RollbackEvents();
      return result;
    }

    public TResult Execute<TResult>(Expression expression)
    {
      var result = _impl.Execute<TResult>(expression);
      _interceptor.RollbackEvents();
      return result;
    }
  }
}