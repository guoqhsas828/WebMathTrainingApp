//
// 
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.Bump
{
  /// <summary>
  /// Select specific set of curve tenors to bump
  /// </summary>
  /// <remarks>
  /// <para>Used by the Sensitivity2 functions to specify the
  /// set of tenors and surfaces to bump.</para>
  /// </remarks>
  public static class CurveTenorSelectors
  {
    #region Static data

    /// <summary>
    /// Whether to include spot price in forward price
    /// (stock/commodity/inflation) curve bump.
    /// The default value: not include.
    /// </summary>
    [ThreadStatic] internal static bool IncludeSpotPrice;

    #endregion

    #region Helpers
    /// <summary>
    /// Determines whether the specified tenor is a rate tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor is a rate tenor; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool IsRateTenor(this CurveTenor tenor)
    {
      if (tenor == null) return false;
      var quoteType = tenor.CurrentQuote.Type;
      return quoteType == QuotingConvention.Yield ||
        quoteType == QuotingConvention.FlatPrice ||
          quoteType == QuotingConvention.FullPrice;
    }

    /// <summary>
    /// Determines whether the specified tenor is a basis tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor is a rate basis tenor; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool IsRateBasisTenor(this CurveTenor tenor)
    {
      if (tenor == null) return false;
      var quoteType = tenor.CurrentQuote.Type;
      return quoteType == QuotingConvention.YieldSpread;
    }

    /// <summary>
    /// Determines whether the specified tenor is fx rate tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor is fx rate tenor; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool IsFxRateTenor(this CurveTenor tenor)
    {
      if (tenor == null) return false;
      var quoteType = tenor.CurrentQuote.Type;
      return quoteType == QuotingConvention.FxRate;
    }

    /// <summary>
    /// Determines whether the specified tenor is an FX basis tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor is an FX basis tenor; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool IsFxBasisTenor(this CurveTenor tenor)
    {
      if (tenor == null || tenor.CurrentQuote.Type != QuotingConvention.YieldSpread)
      {
        return false;
      }
      var swap = tenor.Product as Swap;
      if (swap == null) return false;
      var pi = swap.PayerLeg.ReferenceIndex;
      var ri = swap.ReceiverLeg.ReferenceIndex;
      return pi != null && ri != null && pi.Currency != ri.Currency;
    }

    /// <summary>
    /// Determines whether the specified tenor is an FX tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor is an FX tenor; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool IsFxTenor(this CurveTenor tenor)
    {
      return IsFxRateTenor(tenor) || IsFxBasisTenor(tenor);
    }

    /// <summary>
    /// Determines whether the specified tenor is a credit tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><c>true</c> if the specified tenor is a credit tenor; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool IsCreditTenor(this CurveTenor tenor)
    {
      if (tenor == null) return false;
      var quoteType = tenor.CurrentQuote.Type;
      return tenor.Product is CDS ||
             quoteType == QuotingConvention.CreditConventionalSpread ||
             quoteType == QuotingConvention.CreditConventionalUpfront ||
             quoteType == QuotingConvention.CreditSpread ||
             quoteType == QuotingConvention.FlatPrice;
    }

    /// <summary>
    /// Determines whether the specified tenor is an inflation rate tenor or not
    /// </summary>
    /// <param name="tenor">The tenor to determine</param>
    /// <returns>True or False</returns>
    public static bool IsInflationRateTenor(this CurveTenor tenor)
    {
      if (tenor == null) return false;
      var quoteType = tenor.CurrentQuote.Type;
      return quoteType == QuotingConvention.Yield ||
             quoteType == QuotingConvention.FlatPrice ||
             quoteType == QuotingConvention.FullPrice ||
             (IncludeSpotPrice && IsSpotTenor(tenor));
    }

    /// <summary>
    /// Determines whether the specified tenor is a stock price tenor or not
    /// </summary>
    /// <param name="tenor">The tenor to determine</param>
    /// <returns>True or False</returns>
    public static bool IsStockPriceTenor(this CurveTenor tenor)
    {
      if (tenor == null) return false;
      var quoteType = tenor.CurrentQuote.Type;
      return quoteType == QuotingConvention.ForwardPriceSpread
             || quoteType == QuotingConvention.ForwardFlatPrice
             || quoteType == QuotingConvention.ForwardFullPrice
             || (IncludeSpotPrice && IsSpotTenor(tenor));
    }

    /// <summary>
    /// Determines whether the specified tenor is a commodity tenor or not
    /// </summary>
    /// <param name="tenor">The tenor to determine</param>
    /// <returns>True or False</returns>
    public static bool IsCommodityPriceTenor(this CurveTenor tenor)
    {
      if (tenor == null) return false;
      var quoteType = tenor.CurrentQuote.Type;
      return quoteType == QuotingConvention.ForwardFlatPrice
             || quoteType == QuotingConvention.ForwardFullPrice
             || quoteType == QuotingConvention.ForwardPriceSpread
             || (IncludeSpotPrice && IsSpotTenor(tenor));
    }

    private static bool IsSpotTenor(CurveTenor tenor)
    {
      return tenor.Product is SpotAsset;
    }

    #endregion

    #region Nested Types: Selectors

    [Serializable]
    private abstract class CommonSelector
    {
    }

    [Serializable]
    private sealed class EmptySelector : CommonSelector, ICurveTenorSelector
    {
      private static readonly string key_ =
        typeof(EmptySelector).TypeHandle.Value.ToString();

      public bool HasSelected(CalibratedCurve curve, CurveTenor tenor)
      {
        return false;
      }

      public string Name
      {
        get { return ":Empty:"; }
      }

      public string Key
      {
        get { return key_; }
      }
    }

    [Serializable]
    private sealed class UniformRateSelector : CommonSelector, ICurveTenorSelector
    {
      private static readonly string key_ =
        typeof(UniformRateSelector).TypeHandle.Value.ToString();

      public bool HasSelected(CalibratedCurve curve, CurveTenor tenor)
      {
        return tenor.IsRateTenor();
      }

      public string Name
      {
        get { return "All Rate Tenors"; }
      }

      public string Key
      {
        get { return key_; }
      }
    }

    [Serializable]
    private sealed class UniformForwardPriceSelector : CommonSelector, ICurveTenorSelector
    {
      private static readonly string key_ =
        typeof(UniformRateSelector).TypeHandle.Value.ToString();

      public bool HasSelected(CalibratedCurve curve, CurveTenor tenor)
      {
        return tenor.IsCommodityPriceTenor() || tenor.IsInflationRateTenor() || tenor.IsStockPriceTenor();
      }

      public string Name
      {
        get { return "All Forward Price Tenors"; }
      }

      public string Key
      {
        get { return key_; }
      }
    }



    [Serializable]
    private sealed class UniformRateBasisSelector : CommonSelector, ICurveTenorSelector
    {
      private static readonly string key_ =
        typeof(UniformRateBasisSelector).TypeHandle.Value.ToString();

      public bool HasSelected(CalibratedCurve curve, CurveTenor tenor)
      {
        return tenor.IsRateBasisTenor();
      }

      public string Name
      {
        get { return "All Basis Spread Tenors"; }
      }

      public string Key
      {
        get { return key_; }
      }
    }

    [Serializable]
    private class UniformFxRateSelector : CommonSelector, ICurveTenorSelector
    {
      private static readonly string id_ =
        typeof(UniformFxRateSelector).TypeHandle.Value.ToString();

      public bool HasSelected(CalibratedCurve curve, CurveTenor tenor)
      {
        return tenor.IsFxTenor();
      }

      public string Name
      {
        get { return "All FX Rate Tenors"; }
      }

      public string Key
      {
        get { return id_; }
      }
    }

    [Serializable]
    private class UniformCreditSelector : CommonSelector, ICurveTenorSelector
    {
      private static readonly string id_ =
        typeof(UniformCreditSelector).TypeHandle.Value.ToString();

      public bool HasSelected(CalibratedCurve curve, CurveTenor tenor)
      {
        return curve is SurvivalCurve && tenor.IsCreditTenor();
      }

      public string Name
      {
        get { return "All Credit Tenors"; }
      }

      public string Key
      {
        get { return id_; }
      }
    }

    [Serializable]
    private class NameGroupSelector : CommonSelector, ICurveTenorSelector
    {
      private readonly string name_;
      private readonly KeyedSet<string> nameGroup_;

      public NameGroupSelector(IEnumerable<CurveTenor> tenors, string name)
      {
        nameGroup_ = KeyedSet<string>.GetInstance(tenors.Select(t => t.Name));
        name_ = name;
      }

      #region ICurveTenorSelector Members

      public bool HasSelected(CalibratedCurve curve, CurveTenor tenor)
      {
        return nameGroup_.Contains(tenor.Name);
      }

      public string Name
      {
        get { return name_; }
      }

      public string Key
      {
        get { return nameGroup_.Key; }
      }

      #endregion
    }
    #endregion

    #region Static Data and Properties
    private static readonly Func<CalibratedCurve, CurveTenor, bool> empty_ = new EmptySelector().HasSelected;
    private static readonly Func<CalibratedCurve, CurveTenor, bool> uniformRate_ = new UniformRateSelector().HasSelected;
    private static readonly Func<CalibratedCurve, CurveTenor, bool> uniformRateBasis_ = new UniformRateBasisSelector().HasSelected;
    private static readonly Func<CalibratedCurve, CurveTenor, bool> uniformFxRate_ = new UniformFxRateSelector().HasSelected;
    private static readonly Func<CalibratedCurve, CurveTenor, bool> uniformCredit_ = new UniformCreditSelector().HasSelected;
    private static readonly Func<CalibratedCurve, CurveTenor, bool> uniformForwardPrice_ = new UniformForwardPriceSelector().HasSelected;

    /// <summary>
    /// Gets an empty selector.
    /// </summary>
    /// <remarks></remarks>
    public static Func<CalibratedCurve,CurveTenor,bool> NullSelector
    {
      get { return empty_; }
    }

    /// <summary>
    /// Gets the uniform rate selector.
    /// </summary>
    /// <remarks></remarks>
    public static Func<CalibratedCurve, CurveTenor, bool> UniformRate
    {
      get { return uniformRate_; }
    }

    /// <summary>
    /// Gets the uniform rate basis selector.
    /// </summary>
    /// <remarks></remarks>
    public static Func<CalibratedCurve, CurveTenor, bool> UniformRateBasis
    {
      get { return uniformRateBasis_; }
    }

    /// <summary>
    /// Gets the uniform FX rate selector.
    /// </summary>
    /// <remarks></remarks>
    public static Func<CalibratedCurve, CurveTenor, bool> UniformFxRate
    {
      get { return uniformFxRate_; }
    }

    /// <summary>
    /// Gets the uniform credit selector.
    /// </summary>
    /// <remarks></remarks>
    public static Func<CalibratedCurve, CurveTenor, bool> UniformCredit
    {
      get { return uniformCredit_; }
    }

    /// <summary>
    /// Gets the uniform forward price selector.
    /// </summary>
    /// <remarks></remarks>
    public static Func<CalibratedCurve, CurveTenor, bool> UniformForwardPrice
    {
      get { return uniformForwardPrice_; }
    }

    #endregion

    #region Nested Types: Selection and Handler
    private class CurveBumpHanlder : ICurveBumpHandler
    {
      private readonly CurveTenorSelection selection_;
      private readonly CurveBumpScenario scenario_;

      public CurveBumpHanlder(CurveTenorSelection selection,
        BumpFlags flags, double[] bumpSizes)
      {
        selection_ = selection;
        scenario_ = CurveBumpScenario.GetInstance(
          selection.Selector, flags, bumpSizes);
      }

      #region ICurveBumpHandler Members

      public bool HasAffected(CurveShifts shifts)
      {
        return selection_.Tenors.Any(shifts.ContainsTenor);
      }

      public double[] GetShiftValues(CurveShifts shifts)
      {
        return shifts[scenario_];
      }

      public void SetShiftValues(CurveShifts shifts, double[] values)
      {
        shifts[scenario_] = values;
      }

      #endregion
    }

    private class CurveTenorSelection : ICurveTenorSelection
    {
      private readonly ICurveTenorSelector selector_;
      private readonly IList<CurveTenor> tenors_;
      private readonly IList<CalibratedCurve> curves_;
      private readonly DependencyGraph<CalibratedCurve> allCurves_;
      private string name_;

      public CurveTenorSelection(
        DependencyGraph<CalibratedCurve> allCurves,
        ICurveTenorSelector selector,
        IList<CurveTenor> tenors, IList<CalibratedCurve> curves,
        string name)
      {
        allCurves_ = allCurves;
        tenors_ = tenors;
        selector_ = selector;
        name_ = name ?? selector.Name;
        curves_ = curves ?? new List<CalibratedCurve>();
      }

      public ICurveTenorSelector Selector{get { return selector_; }}

      #region ICurveTenorSelection Members
      public string Name
      {
        get { return name_; }
        internal set { name_ = value; }
      }

      public IList<CurveTenor> Tenors
      {
        get { return tenors_; }
      }

      public ICurveBumpHandler GetBumpHandler(BumpFlags flags, params double[] bumpSizes)
      {
        return new CurveBumpHanlder(this, flags, bumpSizes);
      }

      public IList<CalibratedCurve> Curves
      {
        get { return curves_; }
      }

      public IEnumerable<CalibratedCurve> AllCurves
      {
        get { return allCurves_ == null ? curves_ : allCurves_.ReverseOrdered(); }
      }

      #endregion
    }
    #endregion

    #region Extension methods
    /// <summary>
    /// Selects the tenors.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="filter">The selector.</param>
    /// <param name="name">Name of the selection, usually appears in result table.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static ICurveTenorSelection SelectTenors(
      this DependencyGraph<CalibratedCurve> curves,
      Func<CalibratedCurve, CurveTenor, bool> filter,
      string name)
    {
      if (curves == null || filter == null) return null;
      var selectedCurves = new List<CalibratedCurve>();
      var tenors = curves.ReverseOrdered().SelectMany(
        c => c.Tenors.Select(t => new {Curve = c, Tenor = t}))
        .Where(o => filter(o.Curve, o.Tenor)).DistinctBy(o => o.Tenor)
        .Select(o =>
          {
            if (!selectedCurves.Contains(o.Curve)) selectedCurves.Add(o.Curve);
            return o.Tenor;
          }).ToList();
      if (tenors.Count == 0) return null;
      var selector = filter.Target is CommonSelector
        ? (ICurveTenorSelector) filter.Target
        : new NameGroupSelector(tenors, name);
      return new CurveTenorSelection(curves, selector, tenors, selectedCurves, name);
    }

    /// <summary>
    /// Selects the tenors.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="curves">The curves.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    internal static ICurveTenorSelection SelectTenors(
      this DependencyGraph<CalibratedCurve> curves, ICurveTenorSelector selector)
    {
      if (curves == null || selector == null) return null;
      var selectedCurves = new List<CalibratedCurve>();
      var tenors = curves.SelectMany(c => c.Tenors.Select(t => new { Curve = c, Tenor = t }))
          .Where(o => selector.HasSelected(o.Curve, o.Tenor)).DistinctBy(o => o.Tenor)
          .Select(o =>
          {
            if (!selectedCurves.Contains(o.Curve)) selectedCurves.Add(o.Curve);
            return o.Tenor;
          }).ToList();
      return new CurveTenorSelection(curves, selector, tenors, selectedCurves, null);
    }

    /// <summary>
    /// Gets the parallel selections.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    internal static IEnumerable<ICurveTenorSelection> GetParallelSelections(
      this DependencyGraph<CalibratedCurve> curves,
      Func<CalibratedCurve, CurveTenor, bool> predicate)
    {
      var groups = new Dictionary<IList<CurveTenor>, CurveTenorSelection>(
        TenorListComparer.Instance);
      return curves.SelectMany(c => c.GetTenors(predicate, curves, groups))
        .Where(s => s != null);
    }

    private static IEnumerable<ICurveTenorSelection> GetTenors(
      this CalibratedCurve curve,
      Func<CalibratedCurve, CurveTenor, bool> predicate,
      DependencyGraph<CalibratedCurve> curves,
      Dictionary<IList<CurveTenor>, CurveTenorSelection> groups)
    {
      var tenors = curve.Tenors.Where(t => predicate(curve, t)).ToList();
      var suffix = ".all";
      if (curve is FxForwardCurve && tenors.Any(t => t.Name == "SpotFx"))
      {
        yield return tenors.Where(t => t.Name == "SpotFx").ToList()
          .CreateTenorGroupSelection(curve, curves, groups, ".SpotFx");
        tenors = tenors.Where(t => t.Name != "SpotFx").ToList();
        suffix = ".FxForwards";
      }
      yield return tenors.CreateTenorGroupSelection(curve, curves, groups, suffix);
    }


    private static ICurveTenorSelection CreateTenorGroupSelection(
      this IList<CurveTenor> list, CalibratedCurve curve,
      DependencyGraph<CalibratedCurve> curves,
      Dictionary<IList<CurveTenor>, CurveTenorSelection> groups,
      string suffix)
    {
      if (list.Count == 0) return null;

      CurveTenorSelection s;
      if (groups.TryGetValue(list, out s))
      {
        // Curves with the same tenor collection should be
        // in the same tenor selection group.
        s.Curves.Add(curve);
        return null;
      }

      s = new CurveTenorSelection(curves, new NameGroupSelector(
        list, curve.Name), list, null, curve.Name + suffix);
      s.Curves.Add(curve);
      groups.Add(list, s);
      return s;
    }

    class TenorListComparer : IEqualityComparer<IList<CurveTenor>>
    {
      internal static readonly TenorListComparer Instance = new TenorListComparer();

      #region IEqualityComparer<IList<CurveTenor>> Members

      public bool Equals(IList<CurveTenor> x, IList<CurveTenor> y)
      {
        if (x == null) return y == null;
        if (y == null || y.Count != x.Count) return false;
        for (int i = 0, n = x.Count; i < n; ++i)
        {
          if (!x[i].Equals(y[i])) return false;
        }
        return true;
      }

      public int GetHashCode(IList<CurveTenor> obj)
      {
        if (obj == null || obj.Count == 0) return 0;
        var count = obj.Count;
        var hashcode = count.GetHashCode();
        // use at most 8 objects at the end.
        for (int i = count > 8 ? (count - 8) : 0; i < count; ++i)
          hashcode = CombineHashCodes(hashcode, obj[i].GetHashCode());
        return hashcode;
      }

      static int CombineHashCodes(int h1, int h2)
      {
        return (h1 << 5) + h1 ^ h2;
      }

      #endregion
    }
    #endregion
  }
}
