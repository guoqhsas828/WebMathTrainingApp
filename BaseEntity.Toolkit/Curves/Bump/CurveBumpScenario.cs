using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.Bump
{
  /// <summary>
  ///   
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class CurveBumpScenario
  {
    #region Weak reference cache
    private static KeyGenerator IdGenerator = new KeyGenerator(1000);
    private static readonly string Prefix = typeof(CurveBumpScenario).TypeHandle.Value.ToString();
    private static readonly ConcurrentDictionary<string, WeakReference> cache_ = 
      new ConcurrentDictionary<string, WeakReference>();

    private static string NewKey()
    {
      return Prefix + '-' + IdGenerator.Generate();
    }
    private static CurveBumpScenario LookUp(ICurveTenorSelector selector, BumpFlags bumpFlags, double[] bumpSizes)
    {
      Debug.Assert(selector != null);
      Debug.Assert(bumpSizes != null && bumpSizes.Length > 0);
      var sb = new StringBuilder(1024);
      sb.Append(selector.Key).Append('\t').Append((int) bumpFlags).Append('\t');
      {
        sb.Append(XmlConvert.ToString(bumpSizes[0]));
        for (int i = 1, n = bumpSizes.Length; i < n; ++i)
          sb.Append(',').Append(XmlConvert.ToString(bumpSizes[i]));
      }
      var id = sb.ToString();
      var reference = cache_.AddOrUpdate(id,
        key => new WeakReference(new CurveBumpScenario(selector, bumpFlags, bumpSizes)),
        (key, wr) => wr.IsAlive
          ? wr
          : new WeakReference(new CurveBumpScenario(selector, bumpFlags, bumpSizes)));
      return reference.Target as CurveBumpScenario;
    }

    #endregion

    #region Nested Type: CurveBumpScenarioComparer
    [Serializable]
    class CurveBumpScenarioComparer : EqualityComparer<CurveBumpScenario>
    {
      public override bool Equals(CurveBumpScenario x, CurveBumpScenario y)
      {
        if (x == null || y == null) return ReferenceEquals(x, y);
        return x.Key == y.Key;
      }

      public override int GetHashCode(CurveBumpScenario obj)
      {
        return obj.Key.GetHashCode();
      }
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Prevents a default instance of the <see cref="CurveBumpScenario"/> class from being created.
    /// </summary>
    /// <param name="selector">The tenor selector.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <param name="bumpSizes">The bump sizes.</param>
    /// <remarks></remarks>
    private CurveBumpScenario(ICurveTenorSelector selector, BumpFlags bumpFlags, double[] bumpSizes)
    {
      key_ = NewKey();
      selector_ = selector;
      bumpFlags_ = bumpFlags;
      bumpSizes_ = bumpSizes;
    }
    #endregion

    #region Methods
    internal static CurveBumpScenario GetInstance(ICurveTenorSelector selector,
      BumpFlags bumpFlags, params double[] bumpSizes)
    {
      return LookUp(selector, bumpFlags, bumpSizes);
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    /// <remarks></remarks>
    public static void ClearCache()
    {
      cache_.Clear();
    }
    #endregion

    #region Properties

    /// <summary>
    /// Gets the equality comparer for curve bump scenarios.
    /// </summary>
    /// <remarks></remarks>
    public static IEqualityComparer<CurveBumpScenario> EqualityComparer
    {
      get { return eqComparer_; }
    }

    /// <summary>
    /// Gets the bump tenors.
    /// </summary>
    /// <remarks></remarks>
    internal ICurveTenorSelector Selector
    {
      get { return selector_; }
    }

    /// <summary>
    /// Gets the bump sizes.
    /// </summary>
    /// <remarks></remarks>
    public double[] BumpSizes
    {
      get { return bumpSizes_; }
    }

    /// <summary>
    /// Gets the bump flags.
    /// </summary>
    /// <remarks></remarks>
    public BumpFlags BumpFlags
    {
      get { return bumpFlags_; }
    }

    /// <summary>
    /// Gets the key which uniquely identifies this scenario.
    /// </summary>
    /// <remarks>The key is unique in the current application domain.</remarks>
    public string Key { get { return key_; } }
    #endregion

    #region Data

    internal static readonly CurveBumpScenario ZeroBumpScenario
      = new CurveBumpScenario(null, BumpFlags.None, null);

    private static readonly IEqualityComparer<CurveBumpScenario>
      eqComparer_ = new CurveBumpScenarioComparer();

    private readonly BumpFlags bumpFlags_;
    private readonly double[] bumpSizes_;
    private readonly ICurveTenorSelector selector_;
    private readonly string key_;

    #endregion
  }
}