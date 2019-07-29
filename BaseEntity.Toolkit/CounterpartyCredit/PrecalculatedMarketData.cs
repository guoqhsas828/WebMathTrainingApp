using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Container for discount factor and measure change data
  /// </summary>
  public class PrecalculatedMarketData
  {
    private readonly Func<int, IList<double>> _getDiscountFactors;
    private readonly Func<int, IList<double>> _getRadonNikDensities;
    private readonly double[] _recoveries;
    

    /// <summary>
    /// </summary>
    public PrecalculatedMarketData(Dt[] exposureDts, double[,] discountFactors, Func<int, IList<double>> rnFuncPtr, IList<Tuple<Dt[], double[]>> integrationKernels, double[] recoveryRates)
    {
      _getDiscountFactors = (p) =>
      {
        var discounts = new double[discountFactors.GetLength(1)];
        for (int i = 0; i < discounts.Length; i++)
        {
          discounts[i] = discountFactors[p, i];
        }
        return discounts;
      };
      ExposureDates = exposureDts; 
      _getRadonNikDensities = rnFuncPtr;
      _recoveries = recoveryRates;
      IntegrationKernels = integrationKernels;
    }

    /// <summary>
    /// </summary>
    public PrecalculatedMarketData(Dt[] exposureDts, Func<int, IList<double>> dfFuncPtr, Func<int, IList<double>> rnFuncPtr, IList<Tuple<Dt[], double[]>> integrationKernels, double[] recoveryRates)
    {
      _getDiscountFactors = dfFuncPtr;
      _getRadonNikDensities = rnFuncPtr;
      _recoveries = recoveryRates;
      IntegrationKernels = integrationKernels;
      ExposureDates = exposureDts;
    }

    /// <summary>
    /// Exposure dates 
    /// </summary>
    public Dt[] ExposureDates { get; private set; }

    /// <summary>
    /// </summary>
    public IList<Tuple<Dt[], double[]>> IntegrationKernels { get; private set; }

    /// <summary>
    /// Cpty recoveries
    /// </summary>
    public double[] Recoveries { get { return _recoveries; } }

    /// <summary>
    /// Modelled on path.Evolve, retrieve all the measure change and market data needed to calculate measures
    /// </summary>
    public void GetMarketDataForNode(int pathIdx, int exposureDtIdx, bool wwr, bool unilateral, out double numeraire, out double discountFactor, out double cptyRn, out double ownRn, out double survivalRn, out double cptySpread, out double ownSpread, out double lendSpread, out double borrowSpread)
    {
      var discountFactors = _getDiscountFactors(pathIdx);
      discountFactor = discountFactors[exposureDtIdx];
      numeraire = 1.0 / discountFactor;
      var dtCount = ExposureDates.Length; 

      var rnDensities = _getRadonNikDensities(pathIdx);
      cptyRn = wwr ? rnDensities[(unilateral ? 5 : 0) * dtCount + exposureDtIdx] : 1.0;
      ownRn = wwr ? rnDensities[(unilateral ? 6 : 1) * dtCount + exposureDtIdx] : 1.0;

      survivalRn = wwr ? rnDensities[(unilateral ? 7 : 2) * dtCount + exposureDtIdx] : 1.0;
      cptySpread = rnDensities[3 * dtCount + exposureDtIdx] * (1.0 - _recoveries[0]);

      ownSpread = _recoveries.Length >= 2 ? rnDensities[4 * dtCount + exposureDtIdx] * (1.0 - _recoveries[1]) : 0;

      // By default, lendspread = 0.0 and borrowSpread = ownSpread
      borrowSpread = _recoveries.Length >= 3 ? rnDensities[8 * dtCount + exposureDtIdx] * (1.0 - _recoveries[2]) : ownSpread;
      lendSpread = _recoveries.Length >= 4 ? rnDensities[9 * dtCount + exposureDtIdx] * (1.0 - _recoveries[3]) : 0.0;
    }

    /// <summary>
    /// Modelled on path.Evolve, retrieve all the measure change and market data needed to calculate measures
    /// </summary>
    public void GetMarketDataForPath(int pathIdx, bool wwr, bool unilateral, out IList<double> numeraires, out IList<double> discountFactors, out IList<double> cptyRns, out IList<double> ownRns, out IList<double> survivalRns, out IList<double> cptySpreads, out IList<double> ownSpreads, out IList<double> lendSpreads, out IList<double> borrowSpreads)
    {
      discountFactors = _getDiscountFactors(pathIdx);
      numeraires = discountFactors.Select(d => 1.0 / d).ToList();
      var rnDensities = _getRadonNikDensities(pathIdx);
      var dtCount = ExposureDates.Length;
      Debug.Assert(dtCount*10 <= rnDensities.Count);
      cptyRns = ExposureDates.Select((dt, dtIdx) => wwr ? rnDensities[(unilateral ? 5 : 0) * dtCount + dtIdx] : 1.0).ToList();
      ownRns = ExposureDates.Select((dt, dtIdx) => wwr ? rnDensities[(unilateral ? 6 : 1) * dtCount + dtIdx] : 1.0).ToList();

      survivalRns = ExposureDates.Select((dt, dtIdx) => wwr ? rnDensities[(unilateral ? 7 : 2) * dtCount + dtIdx] : 1.0).ToList();
      cptySpreads = ExposureDates.Select((dt, dtIdx) => rnDensities[3 * dtCount + dtIdx] * (1.0 - _recoveries[0])).ToList();

      ownSpreads = ExposureDates.Select((dt, dtIdx) => _recoveries.Length >= 2 ? rnDensities[4 * dtCount + dtIdx] * (1.0 - _recoveries[1]) : 0).ToList();

      // By default, lendspread = 0.0 and borrowSpread = ownSpread
      borrowSpreads = _recoveries.Length >= 3 ? ExposureDates.Select((dt, dtIdx) => rnDensities[8 * dtCount + dtIdx] * (1.0 - _recoveries[2])).ToList() : ownSpreads;
      lendSpreads = ExposureDates.Select((dt, dtIdx) => _recoveries.Length >= 4 ? rnDensities[9 * dtCount + dtIdx] * (1.0 - _recoveries[3]) : 0).ToList();
    }
  }
}