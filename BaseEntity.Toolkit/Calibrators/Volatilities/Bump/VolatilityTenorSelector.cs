//
// Copyright (c) 2014,  . All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.Bump
{
  /// <summary>
  /// A set of volatility surfaces and tenors to bump
  /// </summary>
  /// <remarks>Used by the Sensitivity2 functions to specify the
  /// set of tenors and surfaces to bump.</remarks>
  public interface IVolatilityTenorSelection
  {
    /// <summary>
    /// Gets the name of the selection
    /// </summary>
    /// <value>The name of the selection</value>
    string Name { get; }

    /// <summary>
    /// Gets the selected tenors
    /// </summary>
    /// <value>The tenors selected</value>
    IEnumerable<BumpedVolatilityTenor> Tenors { get; }

    /// <summary>
    /// Gets the selected volatility surfaces.
    /// </summary>
    /// <value>The surfaces</value>
    IEnumerable<CalibratedVolatilitySurface> Surfaces { get; }
  }

  /// <summary>
  /// A selection with the specification of the bump sizes by surfaces or tenors
  /// </summary>
  /// <seealso cref="IVolatilityTenorSelection" />
  public interface IVolatilityScenarioSelection : IVolatilityTenorSelection
  {
    /// <summary>
    /// Gets the bump sizes.
    /// </summary>
    /// <value>The bump sizes</value>
    IEnumerable<double> Bumps { get; }
  }
  
  /// <summary>
  /// 
  /// </summary>
  public static class VolatilityTenorSelector
  {
    #region Nested type: GroupSelection
    [Serializable]
    private class GroupSelection : IVolatilityTenorSelection
    {
      private readonly string _name;
      private readonly IList<BumpedVolatilityTenor> _tenors;
      private readonly IList<CalibratedVolatilitySurface> _surfaces;

      public GroupSelection(
        IList<IVolatilityTenor> tenors,
        CalibratedVolatilitySurface surface)
        : this(surface.Name, tenors, new[] { surface })
      {}

      public GroupSelection(
        string name,
        IList<IVolatilityTenor> tenors,
        IList<CalibratedVolatilitySurface> surfaces)
      {
        if (tenors == null || tenors.Count == 0)
          throw new ToolkitException("Tenors cannot be empty");
        if (surfaces == null || surfaces.Count == 0)
          throw new ToolkitException("Surfaces cannot be empty");
        if (String.IsNullOrEmpty(name))
        {
          if (surfaces.Count == 1)
            name = surfaces[0].Name;
          else if (tenors.Count == 1)
          {
            name = tenors[0].Name;
            if (String.IsNullOrEmpty(name))
              name = tenors[0].Maturity.ToString();
          }
          else
            name = "All";
        }
        _name = name;
        _tenors = tenors.Select(t => new BumpedVolatilityTenor(t)).ToList();
        _surfaces = surfaces;
      }

      #region IVolatilityTenorSelection Members

      public string Name
      {
        get { return _name; }
      }

      public IEnumerable<BumpedVolatilityTenor> Tenors
      {
        get { return _tenors; }
      }

      public IEnumerable<CalibratedVolatilitySurface> Surfaces
      {
        get { return _surfaces; }
      }

      #endregion
    }
    #endregion

    #region Nested type: ByTenorSelection
    class ByTenorSelection : IVolatilityTenorSelection
    {
      private readonly string _name;
      private readonly BumpedVolatilityTenor _tenor;
      private readonly CalibratedVolatilitySurface _surface;

      public ByTenorSelection(IVolatilityTenor tenor,
        CalibratedVolatilitySurface surface)
      {
        if (tenor == null)
          throw new ToolkitException("Tenor cannot be null");
        if (surface == null)
          throw new ToolkitException("Surface cannot be null");
        var name = tenor.Name;
        if (String.IsNullOrEmpty(name))
          name = tenor.Maturity.ToString();
        _name = name;
        _tenor = new BumpedVolatilityTenor(tenor);
        _surface = surface;
      }

      #region IVolatilityTenorSelection Members

      public string Name
      {
        get { return _name; }
      }

      public IEnumerable<BumpedVolatilityTenor> Tenors
      {
        get { yield return _tenor; }
      }

      public IEnumerable<CalibratedVolatilitySurface> Surfaces
      {
        get { yield return _surface; }
      }

      #endregion
    }
    #endregion

    #region Nested type: ScenarioSelection
    [Serializable]
    private class ScenarioSelection : IVolatilityScenarioSelection
    {
      private readonly string _name;
      private readonly IList<BumpedVolatilityTenor> _tenors;
      private readonly IList<double> _bumps;
      private readonly IList<CalibratedVolatilitySurface> _surfaces;
      

      public ScenarioSelection(
        IList<IVolatilityTenor> tenors,
        IList<double> bumps,
        CalibratedVolatilitySurface surface)
        : this(surface.Name, tenors,bumps, new[] { surface })
      { }

      public ScenarioSelection(
        string name,
        IList<IVolatilityTenor> tenors,
        IList<double> bumps,
        IList<CalibratedVolatilitySurface> surfaces)
      {
        if (tenors == null || tenors.Count == 0)
          throw new ToolkitException("Tenors cannot be empty");
        if (bumps == null || bumps.Count == 0)
          throw new ToolkitException("bumps cannot be empty");
        if (bumps.Count > 1 && bumps.Count != tenors.Count)
          throw new ToolkitException("Must supply same number of bumps as tenors");
        if (surfaces == null || surfaces.Count == 0)
          throw new ToolkitException("Surfaces cannot be empty");
        if (String.IsNullOrEmpty(name))
        {
          if (surfaces.Count == 1)
            name = surfaces[0].Name;
          else if (tenors.Count == 1)
          {
            name = tenors[0].Name;
            if (String.IsNullOrEmpty(name))
              name = tenors[0].Maturity.ToString();
          }
          else
            name = "All";
        }
        _name = name;
        _tenors = tenors.Select(t => new BumpedVolatilityTenor(t)).ToList();
        _bumps = bumps.Count == 1 ? tenors.Select(t=>bumps.First()).ToList() : bumps; 
        _surfaces = surfaces;
      }

      #region IVolatilityTenorSelection Members

      public string Name
      {
        get { return _name; }
      }

      public IEnumerable<BumpedVolatilityTenor> Tenors
      {
        get { return _tenors; }
      }

      public IEnumerable<double> Bumps
      {
        get { return _bumps; }
      }

      public IEnumerable<CalibratedVolatilitySurface> Surfaces
      {
        get { return _surfaces; }
      }

      #endregion
    }
    #endregion

    #region Volatility tenor selectors
    public static IVolatilityTenorSelection SelectTenors(
      this IList<CalibratedVolatilitySurface> surfaces,
      Func<CalibratedVolatilitySurface, IVolatilityTenor, bool> filter,
      string name)
    {
      if (surfaces == null || filter == null) return null;
      var selectedSurfaces = new List<CalibratedVolatilitySurface>();
      var tenors = surfaces.SelectMany(
        s => s.Tenors.Select(t => new {Surface = s, Tenor = t}))
        .Where(o => filter(o.Surface, o.Tenor)).DistinctBy(o => o.Tenor)
        .Select(o =>
        {
          if (!selectedSurfaces.Contains(o.Surface)) selectedSurfaces.Add(o.Surface);
          return o.Tenor;
        }).ToList();
      if (tenors.Count == 0) return null;
      return new GroupSelection(name, tenors, selectedSurfaces);
    }

    public static IEnumerable<IVolatilityTenorSelection> SelectByTenor(
      this IList<CalibratedVolatilitySurface> surfaces,
      Func<CalibratedVolatilitySurface, IVolatilityTenor, bool> filter)
    {
      if (surfaces == null) yield break;
      foreach (var surface in surfaces)
      {
        if (surface == null || surface.Tenors == null)
          continue;
        foreach (var tenor in surface.Tenors)
        {
          if (filter == null || filter(surface, tenor))
            yield return new ByTenorSelection(tenor, surface);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="surfaces"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    public static IEnumerable<IVolatilityTenorSelection> SelectParallel(
      this IList<CalibratedVolatilitySurface> surfaces,
      Func<CalibratedVolatilitySurface, IVolatilityTenor, bool> filter)
    {
      if (surfaces == null) yield break;
      foreach (var surface in surfaces)
      {
        var tenors = filter == null
          ? (IList<IVolatilityTenor>)surface.Tenors
          : surface.Tenors.Where(t => filter(surface, t)).ToList();
        yield return new GroupSelection(tenors,surface);
      }
    }

    public static IVolatilityScenarioSelection SelectScenario(
      this IList<CalibratedVolatilitySurface> surfaces,
      string scenarioName,
      IList<double> bumps)
    {
      return new ScenarioSelection(scenarioName, null, bumps, surfaces);
    }

    public static IVolatilityScenarioSelection SelectScenario(
      this IList<CalibratedVolatilitySurface> surfaces,
      string scenarioName, 
      Func<CalibratedVolatilitySurface, IVolatilityTenor, double> filter)
    {
      if (surfaces == null || filter == null)
        return null;

      var allTenors = new List<IVolatilityTenor>();
      var allBumps = new List<double>(); 
      foreach (var surface in surfaces)
      {
        var tenors = surface.Tenors.Where(t => !filter(surface, t).ApproximatelyEqualsTo(0.0)).ToList();
        var bumps = tenors.Select((t) => filter(surface, t)).ToList(); 
        allTenors.AddRange(tenors);
        allBumps.AddRange(bumps);
      }
      var scenario = new ScenarioSelection(scenarioName, allTenors, allBumps, surfaces);
      return scenario; 
    }


    #endregion

  }
}
