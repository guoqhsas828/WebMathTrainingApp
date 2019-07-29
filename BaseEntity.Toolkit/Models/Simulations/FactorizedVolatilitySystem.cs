using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// The system of factor loadings and corresponding volatilities for simulation.
  /// </summary>
  [Serializable]
  public class FactorizedVolatilitySystem
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="FactorizedVolatilitySystem"/> class.
    /// </summary>
    /// <param name="factors">The factors.</param>
    /// <param name="volatilities">The volatilities.</param>
    public FactorizedVolatilitySystem(FactorLoadingCollection factors,
      VolatilityCollection volatilities)
    {
      FactorLoadings = factors;
      Volatilities = volatilities;
    }

    /// <summary>
    /// Gets the factor loadings.
    /// </summary>
    /// <value>The factor loadings.</value>
    public FactorLoadingCollection FactorLoadings { get; private set; }

    /// <summary>
    /// Gets the volatilities.
    /// </summary>
    /// <value>The volatilities.</value>
    public VolatilityCollection Volatilities { get; private set; }
  }

  ///<summary>
  /// Defines a grid of factor loadings relating a set of reference curves to market factors
  ///</summary>
  [Serializable]
  public class FactorLoadingCollection
  {
    #region Data

    private readonly Dictionary<string, Tuple<object, double[,]>> _data;
    private readonly string[] _marketFactorNames;
    private readonly Tenor[] _tenors;
    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="marketFactorNames">Id of the market factors</param>
    /// <param name="tenors">Term structure tenors</param>
    public FactorLoadingCollection(string[] marketFactorNames, Tenor[] tenors)
    {
      _marketFactorNames = marketFactorNames;
      _tenors = tenors ?? new[] { Tenor.Empty };
      _data = new Dictionary<string, Tuple<object, double[,]>>();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Number of underlying references
    /// </summary>
    public int Count
    {
      get { return _data.Count; }
    }

    /// <summary>
    /// Tenor count
    /// </summary>
    public int TenorCount
    {
      get { return _tenors.Length; }
    }

    /// <summary>
    /// Number of factors
    /// </summary>
    public int FactorCount
    {
      get { return _marketFactorNames.Length; }
    }

    /// <summary>
    /// Merge two vol sets
    /// </summary>
    /// <param name="other">Other vol object</param>
    public void Merge(FactorLoadingCollection other)
    {
      if (other == null)
        return;
      foreach (var tuple in other._data)
      {
        AddFactors(tuple.Value.Item1, tuple.Value.Item2);
      }
    }

    private double[,] Validate(double[,] factors)
    {
      int rows = factors.GetLength(0);
      int cols = factors.GetLength(1);
      if ((FactorCount == cols) && ((TenorCount == rows) || (1 == rows)))
        return factors;
      throw new ArgumentException(String.Format("factors expected of size {0}x{1} or 1x{1} but instead are {2}x{3}", TenorCount, FactorCount, rows, cols));
    }

    /// <summary>
    /// Add market factors
    /// </summary>
    /// <param name="reference">Reference process</param>
    /// <param name="factors">Factor loadings</param>
    public void AddFactors(object reference, double[,] factors)
    {
      factors = Validate(factors);
      var key = GetId(reference);
      if (_data.ContainsKey(key))
      {
        _data[key] = new Tuple<object, double[,]>(reference, factors);
        return;
      }
      _data.Add(key, new Tuple<object, double[,]>(reference, factors));
    }

    /// <summary>
    /// Add signed squared factor loadings
    /// </summary>
    /// <param name="reference">Reference </param>
    /// <param name="correlations">Signed squared factor loadings</param>
    public void AddCorrelations(object reference, double[,] correlations)
    {
      correlations = Validate(correlations);
      var factors = new double[correlations.GetLength(0), correlations.GetLength(1)];
      for (int i = 0; i < factors.GetLength(0); ++i)
      {
        for (int j = 0; j < factors.GetLength(1); ++j)
        {
          double rho = correlations[i, j];
          factors[i, j] = Math.Sign(rho) * Math.Sqrt(Math.Abs(rho));
        }
      }
      AddFactors(reference, factors);
    }

    /// <summary>
    /// Clear factors
    /// </summary>
    public void Clear()
    {
      _data.Clear();
    }
    /// <summary>
    /// Clear factors linked to a reference
    /// </summary>
    public void Remove(object reference)
    {
      var key = GetId(reference);
      _data.Remove(key);
    }
    /// <summary>
    /// Market variable Id
    /// </summary>
    /// <param name="reference">Reference</param>
    /// <returns>Id</returns>
    public static string GetId(object reference)
    {
      var curve = reference as CalibratedCurve;
      if (curve != null)
      {
        return curve.Name;
      }
      var spot = reference as ISpot;
      if (spot != null)
      {
        return spot.Name;
      }
      return string.Empty;
    }

    /// <summary>
    /// Get Factors pertaining to a given object reference
    /// </summary>
    /// <param name="obj">Market variable reference</param>
    /// <returns>Factors</returns>
    public double[,] GetFactorsAt(params object[] obj)
    {
      if (obj == null || obj.Length == 0)
      {
        return null;
      }
      if (obj.Length == 1)
      {
        Tuple<object, double[,]> retVal;
        var key = GetId(obj[0]);
        if (_data.TryGetValue(key, out retVal))
        {
          return retVal.Item2;
        }
        throw new ArgumentException(String.Format("FactorLoadings for object {0} not found", Ccr.Simulations.ReferenceName(obj[0])));
      }
      else
      {
        int count = 0;
        var factors = obj.Select(o =>
        {
          var fl =
            GetFactorsAt(o);
          count += fl.GetLength(0);
          return fl;
        }).ToArray();
        var retVal = new double[count, MarketFactorNames.Length];
        for (int i = 0, index = 0; i < factors.Length; ++i)
        {
          var fl = factors[i];
          for (int j = 0; j < fl.GetLength(0); ++j, ++index)
          {
            for (int k = 0; k < fl.GetLength(1); ++k)
            {
              retVal[index, k] = fl[j, k];
            }
          }
        }
        return retVal;
      }
    }

    /// <summary>
    /// Convert to DataTable
    /// </summary>
    /// <param name="squared">True if factors are squared (and signed)</param>
    /// <param name="references">references"></param>
    /// <returns>DataTable object</returns>
    public DataTable ToDataTable<T>(bool squared, params object[] references) where T : class
    {
      bool all = (references == null || references.Length == 0);
      var dt = new DataTable("Correlations");
      dt.Columns.Add("References", typeof(string));
      foreach (string factorName in MarketFactorNames)
        dt.Columns.Add(factorName, typeof(double));
      foreach (var data in _data.Values.Where(o => o is T && (all || references.Contains(o.Item1))))
      {
        var fl = data.Item2;
        for (int k = 0; k < fl.GetLength(0); ++k)
        {
          var row = dt.NewRow();
          row["References"] = ((Tenors[k] == Tenor.Empty) || (fl.GetLength(1) == 1)) ? GetId(data.Item1) : String.Concat(GetId(data.Item1), Tenors[k].ToString());
          for (int j = 0; j < MarketFactorNames.Length; ++j)
          {
            string factorName = MarketFactorNames[j];
            double rho = fl[k, j];
            row[factorName] = squared ? Math.Sign(rho) * rho * rho : rho;
          }
          dt.Rows.Add(row);
        }
      }
      return dt;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reference"></param>
    /// <returns></returns>
    public bool ContainsReference(object reference)
    {
      var key = GetId(reference);
      return _data.ContainsKey(key);
    }

    #endregion

    #region Properties
    /// <summary>
    /// Reference market variables
    /// </summary>
    public object[] References
    {
      get { return _data.Values.Select(e => e.Item1).ToArray(); }
    }

    ///<summary>
    /// IDs of the market factors
    ///</summary>
    public string[] MarketFactorNames
    {
      get { return _marketFactorNames; }
    }

    /// <summary>
    /// Tenor names
    /// </summary>
    public Tenor[] Tenors
    {
      get { return _tenors; }
    }

    #endregion
  }

  ///<summary>
  /// Defines market vols
  ///</summary>
  [Serializable]
  public class VolatilityCollection
  {
    #region Data
    private readonly Tenor[] _tenors;
    private readonly Dictionary<string, Tuple<object, IVolatilityProcessParameter>> _data;
    //if model has a separable volatility structure store both components separately

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="tenors">Term structure tenors</param>
    public VolatilityCollection(Tenor[] tenors)
    {
      _tenors = tenors ?? new[] { Tenor.Empty };
      _data = new Dictionary<string, Tuple<object, IVolatilityProcessParameter>>();
    }

    #endregion

    #region Methods


    /// <summary>
    /// Convert to DataTable with row format [Reference|Tenor1|...|TenorN|Dt]
    /// </summary>
    /// <param name="references">references"></param>
    /// <returns>DataTable object</returns>
    /// <remarks>All the volatility curves relative to the same reference are assumed to have the same abscissas (dates)</remarks>
    public DataTable ToDataTable<T>(params object[] references) where T : class
    {
      if (Count == 0)
        return new DataTable();
      var asOf = _data.Values.Select(o => o.Item2)
        .OfType<StaticVolatilityCurves>()
        .Select(v => v.Curves.First().AsOf).FirstOrDefault();
      if (asOf.IsEmpty())
        return new DataTable();

      bool all = (references == null || references.Length == 0);
      var dt = new DataTable("Volatilities");    
      dt.Columns.Add("References", typeof(string));

      if (all)
        references = References;

      var tenorDates = Array.ConvertAll(Tenors, t => Dt.Add(asOf, t).ToString());
      dt.Columns.Add("0", typeof(double));
      foreach (string tenorDate in tenorDates)
        dt.Columns.Add(tenorDate, typeof(double));
      dt.Columns.Add("Dt", typeof(string));

      foreach (var data in _data.Values.Where(o => o.Item1 is T && references.Contains(o.Item1)))
      {
        var id = data.Item1;
        var vol = (data.Item2 as StaticVolatilityCurves)?.Curves;
        if (vol == null) continue;

        for (int k = 0; k < vol.Length; ++k)
        {
          var curvePoints = vol[k].Points;
          bool curvePointsMatchTenorDates = curvePoints.Cast<CurvePoint>().All(p => p.Date == asOf || Array.FindIndex(tenorDates, t => t.Equals(p.Date.ToString())) >= 0);
          if (curvePointsMatchTenorDates)
          {
            var row = dt.NewRow();
            row["References"] = GetId(id);
            foreach (CurvePoint p in curvePoints)
              row[p.Date == asOf ? "0" : p.Date.ToString()] = vol[k].Volatility(p.Date);
            dt.Rows.Add(row);
          }
          else
          {
            foreach (CurvePoint p in curvePoints)
            {
              var row = dt.NewRow();
              row["References"] = GetId(id);
              row["Dt"] = p.Date.ToString();
              row["0"] = vol[k].Volatility(p.Date);
              dt.Rows.Add(row);
            }
          }
        }
      }
      return dt;
    }


    /// <summary>
    /// Determines whether the specified reference is in the volatility set.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns><c>true</c> if the specified reference is in the volatility set; otherwise, <c>false</c>.</returns>
    public bool ContainsReference(object reference)
    {
      var key = GetId(reference);
      return _data.ContainsKey(key);
    }

    /// <summary>
    /// Add market vols
    /// </summary>
    /// <param name="reference">Reference process (term structure)</param>
    /// <param name="volatilities">Process vols</param>
    public void Add(object reference, params VolatilityCurve[] volatilities)
    {
      if (volatilities.Any(v => v == null))
      {
        throw new ArgumentNullException(nameof(volatilities));
      }
      Add(reference, new StaticVolatilityCurves(volatilities));
    }

    /// <summary>
    /// Adds a volatility item.
    /// </summary>
    /// <param name="reference">The reference object</param>
    /// <param name="volatility">The volatility object</param>
    /// <remarks>
    ///  This method is for internal use only.  It does not check
    ///  the validity of the reference and volatility objects at all.
    ///  Use it with extreme cautions.
    /// </remarks>
    public void Add(object reference, IVolatilityProcessParameter volatility)
    {
      var key = GetId(reference);
      if (_data.ContainsKey(key))
      {
        _data[key] = Tuple.Create(reference, volatility);
        return;
      }
      _data.Add(key, Tuple.Create(reference, volatility));
    }

    /// <summary>
    /// Merge two vol sets
    /// </summary>
    /// <param name="other">Other vol object</param>
    public void Merge(VolatilityCollection other)
    {
      if (other == null)
        return;
      foreach (var tuple in other._data)
      {
        Add(tuple.Value.Item1, tuple.Value.Item2);
      }
    }

    /// <summary>
    /// Clear vols related to a reference
    /// </summary>
    public void Remove(object reference)
    {
      var key = GetId(reference);
      _data.Remove(key);
    }
    /// <summary>
    /// Market variable Id
    /// </summary>
    /// <param name="reference">Reference</param>
    /// <returns>Id</returns>
    public static string GetId(object reference)
    {
      var curve = reference as CalibratedCurve;
      if (curve != null)
      {
        return curve.Name;
      }
      var spot = reference as ISpot;
      if (spot != null)
      {
        return spot.Name;
      }
      return string.Empty;
    }

    /// <summary>
    /// Get vols pertaining to a given object reference
    /// </summary>
    /// <param name="obj">Market variable reference</param>
    /// <returns>Volas</returns>
    public VolatilityCurve[] GetVolsAt(params object[] obj)
    {
      if (obj == null || obj.Length == 0)
        return null;
      if (obj.Length == 1)
      {
        Tuple<object, IVolatilityProcessParameter> retVal;
        var key = GetId(obj[0]);
        if (_data.TryGetValue(key, out retVal))
        {
          return (retVal.Item2 as StaticVolatilityCurves)?.Curves;
        }
        throw new ArgumentException(String.Format("Volatilities for object {0} not found", Ccr.Simulations.ReferenceName(obj[0])));
      }
      return obj.SelectMany(o => GetVolsAt(o)).ToArray();
    }

    /// <summary>
    /// Gets the volatility data pertaining to the specified object reference
    /// </summary>
    /// <param name="reference">The reference object</param>
    /// <returns>The volatility data</returns>
    /// <exception cref="System.ArgumentException">No volatility data found</exception>
    internal IVolatilityProcessParameter GetVolatilityData(object reference)
    {
      Tuple<object, IVolatilityProcessParameter> retVal;
      var key = GetId(reference);
      if (_data.TryGetValue(key, out retVal))
      {
        return retVal.Item2;
      }
      var name = Ccr.Simulations.ReferenceName(reference);
      throw new ArgumentException($"Volatilities for object {name} not found");
    }
    #endregion

    #region Properties

    /// <summary>
    /// Reference market variables
    /// </summary>
    public object[] References => _data.Values.Select(e => e.Item1).ToArray();

    /// <summary>
    /// Number of underlying references
    /// </summary>
    public int Count => _data.Count;

    /// <summary>
    /// Tenor count
    /// </summary>
    public int TenorCount => _tenors.Length;


    /// <summary>
    /// Tenor names
    /// </summary>
    public Tenor[] Tenors => _tenors;

    #endregion
  }

  public static class UtilityMethods
  {
    #region Utils

    public static VolatilityCurve[] GetVols(this VolatilityCollection volatilities,
      object reference)
    {
      if (volatilities == null)
        throw new ArgumentNullException("volatilities");
      return volatilities.GetVolsAt(reference);
    }

    internal static VolatilityCurve[] TryGetVols(this VolatilityCollection volatilities,
      object reference, VolatilityCurve[] defaultValue)
    {
      try
      {
        return volatilities.GetVolsAt(reference) ?? defaultValue;
      }
      catch (Exception)
      {
        return defaultValue;
      }
    }

    public static double[,] GetFactors(this FactorLoadingCollection factorLoadings,
      object reference)
    {
      if (factorLoadings == null)
        throw new ArgumentNullException("factorLoadings");
      return factorLoadings.GetFactorsAt(reference);
    }

    internal static double[,] TryGetFactors(this FactorLoadingCollection factorLoadings,
      object reference, double[,] defaultValue)
    {
      try
      {
        return factorLoadings.GetFactorsAt(reference) ?? defaultValue;
      }
      catch (Exception)
      {
        return defaultValue;
      }
    }

    internal static CalibratedCurve[] GetSpotBased(this IEnumerable<CalibratedCurve> forwardTermStructures, VolatilityCollection volatilities, FactorLoadingCollection factorLoadings)
    {
      if (forwardTermStructures == null || volatilities == null || factorLoadings == null)
      {
        return null;
      }
      return
        forwardTermStructures.Where(c =>
        {
          var sb = c as IForwardPriceCurve;
          return ((sb != null) && (sb.Spot != null)) && volatilities.References.Contains(sb.Spot) && factorLoadings.References.Contains(sb.Spot);
        }).ToArray();
    }

    internal static CalibratedCurve[] GetForwardBased(this IEnumerable<CalibratedCurve> forwardTermStructures, VolatilityCollection volatilities, FactorLoadingCollection factorLoadings)
    {
      if (forwardTermStructures == null || volatilities == null || factorLoadings == null)
      {
        return null;
      }
      return forwardTermStructures.Where(c => volatilities.References.Contains(c) && factorLoadings.References.Contains(c)).ToArray();
    }
    #endregion
  }
}
