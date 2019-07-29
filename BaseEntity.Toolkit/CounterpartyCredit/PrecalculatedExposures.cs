using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Container for a group of precalculated exposures. 
  /// </summary>
  public class PrecalculatedExposures
  {
    /// <summary>
    /// 
    /// </summary>
    public PrecalculatedExposures()
    {
      _exposures = new List<double[,]>();
      _exposureDates = new List<Dt[]>();
      _exposureDateFracts = new List<double[]>();
      _nettingGroups = new List<string>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="exposures"></param>
    /// <param name="exposureDts"></param>
    /// <param name="nettingGroup"></param>
    public void AddExposures(double[,] exposures, Dt[] exposureDts, string nettingGroup)
    {
      _exposures.Add(exposures);
      _exposureDates.Add(exposureDts);
      _nettingGroups.Add(nettingGroup);
      if (_pathCount == 0)
        _pathCount = exposures.GetLength(0);
      else if (_pathCount != exposures.GetLength(0))
      {
        throw new ArgumentException("Attempt to add exposures with different path count.");
      }
      var dtFracts = new List<double>();
      for (int i = 1; i < exposureDts.Length; i++)
      {
        dtFracts.Add(Dt.FractDiff(exposureDts[i-1], exposureDts[i]));
      }
      _exposureDateFracts.Add(dtFracts.ToArray());
    }

    /// <summary>
    /// Calculate exposures along a given path and date for a specific trade. 
    /// </summary>
    /// <param name="exposureIdx">index of the precalculated trade exposures</param>
    /// <param name="pathIdx">simulated path id</param>
    /// <param name="exposureDt">date to calc exposure. If exposure not recorded on this date, linear interp from surrounding dates</param>
    public double GetExposure(int exposureIdx, int pathIdx, Dt exposureDt)
    {
      var exposures = _exposures[exposureIdx];
      var exposureDts = _exposureDates[exposureIdx];
      if (exposureDt <= exposureDts[0])
        return exposures[pathIdx, 0];
      if (exposureDt >= exposureDts[exposureDts.Length-1])
        return exposures[pathIdx, exposureDts.Length - 1];

      var dtIdx2 = Array.BinarySearch(exposureDts, exposureDt);
      if (dtIdx2 < 0) dtIdx2 = ~dtIdx2;

      var dtIdx1 = dtIdx2 - 1;
      if (dtIdx2 == 0)
      {
        return exposures[pathIdx, 0];
      }
      else if (dtIdx2 >= exposureDts.Length)
      {
        return 0.0;
      }
      else if (exposureDt == exposureDts[dtIdx1])
      {
        return exposures[pathIdx, dtIdx1];
      }
      else if (exposureDt == exposureDts[dtIdx2])
      {
        return exposures[pathIdx, dtIdx2];
      }
      else
      {
        // linear interp exposure for this date
        var fracts = _exposureDateFracts[exposureIdx];
        var d = fracts[dtIdx1]; //Dt.FractDiff(exposureDts[dtIdx1], exposureDts[dtIdx2]);
        var n = Dt.FractDiff(exposureDts[dtIdx1], exposureDt);
        var alpha = n / d;
        return exposures[pathIdx, dtIdx1] * (1.0 - alpha) + exposures[pathIdx, dtIdx2] * alpha;
      }
    }

    /// <summary>
    /// Get interpolated exposures for a single trade by [path,date]
    /// </summary>
    public double[,] GetExposures(int exposureIdx, Dt[] source, Tenor? mpor = null)
    {
      var target = _exposureDates[exposureIdx];
      var alphas = new double[source.Length];
      var betas = new double[source.Length];
      var idxA = new int[source.Length];
      var idxB = new int[source.Length];

      int sourceIdx = 0, targetIdx2 = 0, targetIdx1 = 0;
      var dtDoubles = source.Select(d => d.ToDouble()).ToList();
      var targetDtDoubles = target.Select(d => d.ToDouble()).ToList();
      while (sourceIdx < source.Length && targetIdx2 < target.Length)
      {
        var exposureDt = source[sourceIdx];
        if (mpor.HasValue && !mpor.Value.IsEmpty)
          exposureDt = Dt.Add(exposureDt, -mpor.Value.N, mpor.Value.Units);

        if (exposureDt <= target[0])
        {
          alphas[sourceIdx] = 1.0;
          betas[sourceIdx] = 0.0;
          idxA[sourceIdx] = 0;
          idxB[sourceIdx] = 0;
          sourceIdx++; 
        }
        else if (exposureDt >= target[target.Length - 1])
        {
          alphas[sourceIdx] = 1.0;
          betas[sourceIdx] = 0.0;
          idxA[sourceIdx] = target.Length - 1;
          idxB[sourceIdx] = 0;
          sourceIdx++; 
        }
        else if (exposureDt > target[targetIdx2])
        {
          targetIdx1 = targetIdx2++;
        }
        else
        {
          if (exposureDt == target[targetIdx2])
          {
            alphas[sourceIdx] = 1.0;
            betas[sourceIdx] = 0.0;
            idxA[sourceIdx] = targetIdx2;
            idxB[sourceIdx] = targetIdx1;
            sourceIdx++;
          }
          else
          {
            // linear interp exposure for this date
            var fracts = _exposureDateFracts[exposureIdx];
            var period = fracts[targetIdx1]; //Dt.FractDiff(exposureDts[dtIdx1], exposureDts[dtIdx2]);
            var subPeriod = Dt.FractDiff(target[targetIdx1], exposureDt);
            var alpha = 1.0 - subPeriod / period;
            alphas[sourceIdx] = alpha;
            betas[sourceIdx] = 1.0 - alpha;
            idxA[sourceIdx] = targetIdx1;
            idxB[sourceIdx] = targetIdx2;
            sourceIdx++;
          }
        }
      }

      var exposures = _exposures[exposureIdx];
      var interpolatedExposures = new double[PathCount, source.Length];
      Parallel.For(0, PathCount, p =>
      {
        for (int d = 0; d < source.Length; d++)
        {
          interpolatedExposures[p, d] = exposures[p, idxA[d]] * alphas[d] + exposures[p, idxB[d]] * betas[d];
        }
      }
      );
      return interpolatedExposures;
    }


    /// <summary>
    /// Return netted exposures for each netting group along each path on supplied dates
    /// </summary>
    /// <returns>net exposures by [paths,exposureDts,nettingGroups] </returns>
    public double[][][] GetNetExposures(Dictionary<string, int> nettingMap, Dt[] exposureDts, Netting? netting = null)
    {
      var netExposures = new double[PathCount][][];
      for (int p = 0; p < PathCount; p++)
      {
        netExposures[p] = new double[exposureDts.Length][];
        for (int d = 0; d < exposureDts.Length; d++)
        {
          netExposures[p][d] = new double[nettingMap.Count];
        }
      }
      var allTradeIdxs = NettingGroups.Select((name, i) => new {Name = name, Idx = i});
      var names = nettingMap.Keys.ToList();
      Parallel.For(0, nettingMap.Count, (n) =>
      {
        var nettingIdx = nettingMap[names[n]];
        var tradeIdxs = allTradeIdxs.Where(t => t.Name == names[n]).ToList();
        for (int i = 0; i < tradeIdxs.Count; i++)
        {
          var tradeIdx = tradeIdxs[i].Idx;
          var mpor = Tenor.Empty;
          if (netting.HasValue && netting.Value.CollateralMaps != null)
            mpor = netting.Value.CollateralMaps[nettingIdx].MarginPeriodOfRisk; 
          var tradeExposures = GetExposures(tradeIdx, exposureDts, mpor);
          for (int pathIdx = 0; pathIdx < PathCount; pathIdx++)
          {
            for (int dtIdx = 0; dtIdx < exposureDts.Length; dtIdx++)
            {
              var pv = tradeExposures[pathIdx, dtIdx];
              netExposures[pathIdx][dtIdx][nettingIdx] += pv;
            }
          }
        }
      });
      return netExposures;
    }


    /// <summary>
    /// Returns the last exposure date for a specified trade ID.
    /// </summary>
    /// <param name="tradeIdx"></param>
    /// <returns></returns>
    public Dt MaxExposureDate(int tradeIdx)
    {
      return _exposureDates[tradeIdx].Max();
    }

    /// <summary>
    /// number of sets of exposures
    /// </summary>
    public int Count { get { return _exposures.Count; } }

    /// <summary>
    /// number of paths in each exposure set
    /// </summary>
    public int PathCount { get { return _pathCount;  } }

    /// <summary>
    /// netting group name associated with each exposure set
    /// </summary>
    public IList<string> NettingGroups { get { return _nettingGroups; } }  

    private IList<Dt[]> _exposureDates;
    private IList<double[,]> _exposures;
    private IList<string> _nettingGroups;
    private int _pathCount;
    private IList<double[]> _exposureDateFracts;
  }
}