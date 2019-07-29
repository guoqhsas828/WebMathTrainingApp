using System;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{
  #region ICollateralMap

  /// <summary>
  /// Collateral map. Implementations of this interface calculate the collateral posting as a function of 
  /// </summary>
  public interface ICollateralMap
  {
    /// <summary>
    /// Margin period of risk
    /// </summary>
    Tenor MarginPeriodOfRisk { get; }

    /// <summary>
    /// Netting Group Name
    /// </summary>
    string NettingGroup { get; }

    /// <summary>
    /// Last collateral posting. 
    /// Sign should be positive if counterparty posted collateral, 
    /// negative if booking entity posted collateral
    /// </summary>
    double? LastPosting { get; set; }

    /// <summary>
    /// Can collateral be rehypothecated or otherwise reused. If IA is not segregated, it will also be treated as reusable 
    /// </summary>
    bool ReusePermitted { get; }


    /// <summary>
    /// Can IA collateral be rehypothecated or otherwise reused
    /// </summary>
    bool IndependentAmountSegregated { get; }

    /// <summary>
    /// Compute collateral posting
    /// </summary>
    /// <param name="pv">Mtm of the netting group. Sign is from booking entity's perspective (as usual). 
    /// Sign should be positive if booking entity has exposure (booking entity is in the money), 
    /// negative if counterparty has exposure (booking entity is out of the money)</param>
    /// <param name="spread">Risky party spread. Which risky party is determined by sign of pv. 
    /// If pv is positive, pass counterparty spread. If negative, pass booking entity spread</param>
    /// <param name="dt">the margin call date</param>
    /// <returns>Collateral posting (in units of domestic currency). 
    /// Signed should be positive if booking entity receives collateral, 
    /// negative if booking entity posts collateral</returns>
    double VariationMargin(double pv, double spread, Dt dt);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pv"></param>
    /// <param name="vm"></param>
    /// <returns></returns>
    double IndependentAmount(double pv, double vm);
  }

  #endregion


  #region INativeCollateralMap

  /// <summary>
  /// Interface to allow conversion of ICollateralMap to a native C++ equivalent
  /// </summary>
  public interface INativeCollateralMap
  {
    /// <summary>
    /// Convert this ICollateralMap to a native C++ version and add to nativeAggregator
    /// </summary>
    /// <param name="nativeAggregator"></param>
    void AddNativeCollateralMap(CollateralizedExposureAggregatorNative nativeAggregator);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="nativeAggregator"></param>
    void AddIncrementalNativeCollateralMap(IncrementalCCRExposureAggregatorNative nativeAggregator);
  }
  #endregion

  #region Netting

  /// <summary>
  /// Netting
  /// </summary>
  public struct Netting
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Netting));

    /// <summary>
    /// IDs of the netting groups to be aggregated (could be a proper subset of all the netting sets)
    /// </summary>
    public readonly string[] NettingGroups;

    /// <summary>
    /// Netting super group IDs. Only netting groups belonging to the same super group are netted with each other   
    /// </summary>
    public readonly string[] NettingSuperGroups;

    /// <summary>
    /// Collateral thresholds for each netting group 
    /// </summary>
    public ICollateralMap[] CollateralMaps;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="nettingGroups">Id of the netting groups to be aggregated (could be a proper subset of all the netting sets)</param>
    /// <param name="nettingSuperGroups">Netting super group IDs. Only netting groups belonging to the same super group are netted with each other.</param>
    /// <param name="collateralMaps">Collateral thresholds for each netting group </param>
    /// <remarks>
    /// NettingGroups[] contains the names of all netting groups to aggregate, 
    /// NettingSuperGroups[] contains the netting rule among the netting groups.
    /// For instance, if groups := {"a","b","c","d"}, a netting rule among the sets can be written as {"g0","g1","g1","g2"}, 
    /// meaning that netting set b and c belong to the same netting superset and should therefore be netted.
    /// </remarks>
    public Netting(string[] nettingGroups, string[] nettingSuperGroups, ICollateralMap[] collateralMaps)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Creating Netting...");
      }
      CollateralMaps = collateralMaps;
      if (nettingSuperGroups != null && nettingSuperGroups.Length != nettingGroups.Length)
        throw new ArgumentException("A netting id is required for each master agreement");
      var g = new List<string>();
      var sg = new List<string>();
      for (int i = 0; i < nettingGroups.Length; ++i)
      {
        if (g.Contains(nettingGroups[i]))
          continue;
        g.Add(nettingGroups[i]);
        sg.Add((nettingSuperGroups == null) ? "all" : nettingSuperGroups[i]);
      }
      NettingGroups = g.ToArray();
      NettingSuperGroups = sg.ToArray();
      if (Logger.IsDebugEnabled)
      {
        LogArrays(nettingGroups, nettingSuperGroups);
      }
    }

    private void LogArrays(string[] nettingGroups, string[] nettingSuperGroups)
    {
      LogArray(nettingGroups, "Netting Groups: ", "NetingGroups element is null", "NettingGroups Array is null", "NettingGroups Array is empty");
      LogArray(nettingSuperGroups, "Netting Super Groups: ", "NetingSuperGroups element is null", "NettingSuperGroups Array is null", "NettingSuperGroups Array is empty");
    }

    private void LogArray(string[] array, string heading, string nullElementMessage, string nullArrayMessage, string emptyArrayMessage)
    {
      var builder = new StringBuilder();
      if (array != null && array.Length > 0)
      {
        builder.Append(heading);
        var i = 0;
        for (; i < array.Length - 1; ++i)
        {
          if (array[i] == null)
          {
            builder.Append(nullElementMessage + ", ");
          }
          else
          {
            builder.Append(array[i] + ", ");
          }
        }
        if (array[i] == null)
        {
          builder.Append(nullElementMessage);
        }
        else
        {
          builder.Append(array[i]);
        }
      }
      else
      {
        if (array == null)
        {
          builder.Append(nullArrayMessage);
        }
        else
        {
          builder.Append(emptyArrayMessage);
        }
      }
    }
  }

  #endregion
}