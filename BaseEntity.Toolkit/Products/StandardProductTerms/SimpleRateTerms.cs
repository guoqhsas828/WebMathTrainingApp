//
// SimpleRateTerms.cs
//   2015. All rights reserved.
//

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  internal sealed class SimpleRateTerms<T> : StandardProductTermsBase where T : IProduct
  {
    #region Static cache and constructor

    private static readonly ConcurrentDictionary<IReferenceRate,
      SimpleRateTerms<T>> Cache = new ConcurrentDictionary<
        IReferenceRate, SimpleRateTerms<T>>();

    internal static SimpleRateTerms<T> Create(
      IReferenceRate referenceRate,
      Func<IReferenceRate, Dt, string, double, T> factory,
      string name = null)
    {
      return Cache.GetOrAdd(referenceRate,
        i => new SimpleRateTerms<T>((IReferenceRate) i, factory, name));
    }

    #endregion

    private SimpleRateTerms(IReferenceRate referenceRate,
      Func<IReferenceRate, Dt, string, double, T> factory,
      string name)
    {
      _referenceRate = referenceRate;
      _factory = factory;
      _name = string.IsNullOrEmpty(name) ? null : name;
    }

    [ProductBuilder]
    internal T GetProduct(Dt asOf, string tenorName, double quote)
    {
      return _factory(_referenceRate, asOf, tenorName, quote);
    }

    public override string Key
    {
      get
      {
        return string.Format("{0}.{1}",
          _referenceRate == null ? "<null>" : _referenceRate.Key,
          _name ?? typeof(T).Name);
      }
    }

    private readonly string _name;
    private readonly IReferenceRate _referenceRate;
    private readonly Func<IReferenceRate, Dt, string, double, T> _factory;
  }
}
