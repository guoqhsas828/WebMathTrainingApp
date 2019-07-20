// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using NHibernate;
using NHibernate.Transform;
using NHibernate.Type;
using BaseEntity.Database.Engine;
using System.Threading.Tasks;
using System.Threading;

namespace BaseEntity.Database
{
  internal class QueryProxy : IQuery
  {
    private readonly IQuery _impl;
    private readonly AuditInterceptor _interceptor;

    public QueryProxy(IQuery impl, AuditInterceptor interceptor)
    {
      _impl = impl;
      _interceptor = interceptor;
    }

    public async Task<IEnumerable> EnumerableAsync(CancellationToken token)
    {
      var result = await _impl.EnumerableAsync(token);
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task<IEnumerable<T>> EnumerableAsync<T>(CancellationToken token)
    {
      var result = await _impl.EnumerableAsync<T>(token);
      _interceptor.RollbackEvents();
      return result;
    }

    public IEnumerable Enumerable()
    {
      var result = _impl.Enumerable();
      _interceptor.RollbackEvents();
      return result;
    }


    public IEnumerable<T> Enumerable<T>()
    {
      var result = _impl.Enumerable<T>();
      _interceptor.RollbackEvents();
      return result;
    }

    public IList List()
    {
      var result = _impl.List();
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task<IList> ListAsync(CancellationToken token)
    {
      var result = await _impl.ListAsync(token);
      _interceptor.RollbackEvents();
      return result;
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
      var result = await _impl.ListAsync<T>();
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

    public T UniqueResult<T>()
    {
      var result = _impl.UniqueResult<T>();
      _interceptor.RollbackEvents();
      return result;
    }


    public async Task<T> UniqueResultAsync<T>(CancellationToken token)
    {
      var result = await _impl.UniqueResultAsync<T>(token);
      _interceptor.RollbackEvents();
      return result;
    }

    public int ExecuteUpdate()
    {
      return _impl.ExecuteUpdate();
    }

    public async Task<int> ExecuteUpdateAsync(CancellationToken token)
    {
      return await _impl.ExecuteUpdateAsync();
    }

    public IQuery SetMaxResults(int maxResults)
    {
      _impl.SetMaxResults(maxResults);
      return this;
    }

    public IQuery SetFirstResult(int firstResult)
    {
      _impl.SetFirstResult(firstResult);
      return this;
    }

    public IQuery SetReadOnly(bool readOnly)
    {
      _impl.SetReadOnly(readOnly);
      return this;
    }

    public IQuery SetCacheable(bool cacheable)
    {
      _impl.SetCacheable(cacheable);
      return this;
    }

    public IQuery SetCacheRegion(string cacheRegion)
    {
      _impl.SetCacheRegion(cacheRegion);
      return this;
    }

    public IQuery SetTimeout(int timeout)
    {
      _impl.SetTimeout(timeout);
      return this;
    }

    public IQuery SetFetchSize(int fetchSize)
    {
      _impl.SetFetchSize(fetchSize);
      return this;
    }

    public IQuery SetLockMode(string alias, LockMode lockMode)
    {
      _impl.SetLockMode(alias, lockMode);
      return this;
    }

    public IQuery SetComment(string comment)
    {
      _impl.SetComment(comment);
      return this;
    }

    public IQuery SetFlushMode(FlushMode flushMode)
    {
      _impl.SetFlushMode(flushMode);
      return this;
    }

    public IQuery SetCacheMode(CacheMode cacheMode)
    {
      _impl.SetCacheMode(cacheMode);
      return this;
    }

    public IQuery SetParameter(int position, object val, IType type)
    {
      _impl.SetParameter(position, val, type);
      return this;
    }

    public IQuery SetParameter(string name, object val, IType type)
    {
      _impl.SetParameter(name, val, type);
      return this;
    }

    public IQuery SetParameter<T>(int position, T val)
    {
      _impl.SetParameter(position, val);
      return this;
    }

    public IQuery SetParameter<T>(string name, T val)
    {
      _impl.SetParameter(name, val);
      return this;
    }

    public IQuery SetParameter(int position, object val)
    {
      _impl.SetParameter(position, val);
      return this;
    }

    public IQuery SetParameter(string name, object val)
    {
      _impl.SetParameter(name, val);
      return this;
    }

    public IQuery SetParameterList(string name, IEnumerable vals, IType type)
    {
      _impl.SetParameterList(name, vals, type);
      return this;
    }

    public IQuery SetParameterList(string name, IEnumerable vals)
    {
      _impl.SetParameterList(name, vals);
      return this;
    }

    public IQuery SetProperties(object obj)
    {
      _impl.SetProperties(obj);
      return this;
    }

    public IQuery SetAnsiString(int position, string val)
    {
      _impl.SetAnsiString(position, val);
      return this;
    }

    public IQuery SetAnsiString(string name, string val)
    {
      _impl.SetAnsiString(name, val);
      return this;
    }

    public IQuery SetBinary(int position, byte[] val)
    {
      _impl.SetBinary(position, val);
      return this;
    }

    public IQuery SetBinary(string name, byte[] val)
    {
      _impl.SetBinary(name, val);
      return this;
    }

    public IQuery SetBoolean(int position, bool val)
    {
      _impl.SetBoolean(position, val);
      return this;
    }

    public IQuery SetBoolean(string name, bool val)
    {
      _impl.SetBoolean(name, val);
      return this;
    }

    public IQuery SetByte(int position, byte val)
    {
      _impl.SetByte(position, val);
      return this;
    }

    public IQuery SetByte(string name, byte val)
    {
      _impl.SetByte(name, val);
      return this;
    }

    public IQuery SetCharacter(int position, char val)
    {
      _impl.SetCharacter(position, val);
      return this;
    }

    public IQuery SetCharacter(string name, char val)
    {
      _impl.SetCharacter(name, val);
      return this;
    }

    public IQuery SetDateTime(int position, DateTime val)
    {
      _impl.SetDateTime(position, val);
      return this;
    }

    public IQuery SetDateTime(string name, DateTime val)
    {
      _impl.SetDateTime(name, val);
      return this;
    }

    public IQuery SetDateTimeNoMs(int position, DateTime val)
    {
      _impl.SetDateTimeNoMs(position, val);
      return this;
    }

    public IQuery SetDateTimeNoMs(string name, DateTime val)
    {
      _impl.SetDateTimeNoMs(name, val);
      return this;
    }

    public IQuery SetDateTime2(int position, DateTime val)
    {
      _impl.SetDateTime2(position, val);
      return this;
    }

    public IQuery SetDateTime2(string name, DateTime val)
    {
      _impl.SetDateTime2(name, val);
      return this;
    }

    public IQuery SetTimeSpan(int position, TimeSpan val)
    {
      _impl.SetTimeSpan(position, val);
      return this;
    }

    public IQuery SetTimeSpan(string name, TimeSpan val)
    {
      _impl.SetTimeSpan(name, val);
      return this;
    }

    public IQuery SetTimeAsTimeSpan(int position, TimeSpan val)
    {
      _impl.SetTimeAsTimeSpan(position, val);
      return this;
    }

    public IQuery SetTimeAsTimeSpan(string name, TimeSpan val)
    {
      _impl.SetTimeAsTimeSpan(name, val);
      return this;
    }

    public IQuery SetDateTimeOffset(int position, DateTimeOffset val)
    {
      _impl.SetDateTimeOffset(position, val);
      return this;
    }

    public IQuery SetDateTimeOffset(string name, DateTimeOffset val)
    {
      _impl.SetDateTimeOffset(name, val);
      return this;
    }

    public IQuery SetDecimal(int position, decimal val)
    {
      _impl.SetDecimal(position, val);
      return this;
    }

    public IQuery SetDecimal(string name, decimal val)
    {
      _impl.SetDecimal(name, val);
      return this;
    }

    public IQuery SetDouble(int position, double val)
    {
      _impl.SetDouble(position, val);
      return this;
    }

    public IQuery SetDouble(string name, double val)
    {
      _impl.SetDouble(name, val);
      return this;
    }

    public IQuery SetEnum(int position, Enum val)
    {
      _impl.SetEnum(position, val);
      return this;
    }

    public IQuery SetEnum(string name, Enum val)
    {
      _impl.SetEnum(name, val);
      return this;
    }

    public IQuery SetInt16(int position, short val)
    {
      _impl.SetInt16(position, val);
      return this;
    }

    public IQuery SetInt16(string name, short val)
    {
      _impl.SetInt16(name, val);
      return this;
    }

    public IQuery SetInt32(int position, int val)
    {
      _impl.SetInt32(position, val);
      return this;
    }

    public IQuery SetInt32(string name, int val)
    {
      _impl.SetInt32(name, val);
      return this;
    }

    public IQuery SetInt64(int position, long val)
    {
      _impl.SetInt64(position, val);
      return this;
    }

    public IQuery SetInt64(string name, long val)
    {
      _impl.SetInt64(name, val);
      return this;
    }

    public IQuery SetSingle(int position, float val)
    {
      _impl.SetSingle(position, val);
      return this;
    }

    public IQuery SetSingle(string name, float val)
    {
      _impl.SetSingle(name, val);
      return this;
    }

    public IQuery SetString(int position, string val)
    {
      _impl.SetString(position, val);
      return this;
    }

    public IQuery SetString(string name, string val)
    {
      _impl.SetString(name, val);
      return this;
    }

    public IQuery SetTime(int position, DateTime val)
    {
      _impl.SetTime(position, val);
      return this;
    }

    public IQuery SetTime(string name, DateTime val)
    {
      _impl.SetTime(name, val);
      return this;
    }

    public IQuery SetTimestamp(int position, DateTime val)
    {
      _impl.SetTimestamp(position, val);
      return this;
    }

    public IQuery SetTimestamp(string name, DateTime val)
    {
      _impl.SetTimestamp(name, val);
      return this;
    }

    public IQuery SetGuid(int position, Guid val)
    {
      _impl.SetGuid(position, val);
      return this;
    }

    public IQuery SetGuid(string name, Guid val)
    {
      _impl.SetGuid(name, val);
      return this;
    }

    public IQuery SetEntity(int position, object val)
    {
      _impl.SetEntity(position, val);
      return this;
    }

    public IQuery SetEntity(string name, object val)
    {
      _impl.SetEntity(name, val);
      return this;
    }

    public IQuery SetResultTransformer(IResultTransformer resultTransformer)
    {
      _impl.SetResultTransformer(resultTransformer);
      return this;
    }

    public IFutureEnumerable<T> Future<T>()
    {
      return _impl.Future<T>();
    }

    public IFutureValue<T> FutureValue<T>()
    {
      return _impl.FutureValue<T>();
    }

    public string QueryString
    {
      get { return _impl.QueryString; }
    }

    public IType[] ReturnTypes
    {
      get { return _impl.ReturnTypes; }
    }

    public string[] ReturnAliases
    {
      get { return _impl.ReturnAliases; }
    }

    public string[] NamedParameters
    {
      get { return _impl.NamedParameters; }
    }

    public bool IsReadOnly
    {
      get { return _impl.IsReadOnly; }
    }
  }
}