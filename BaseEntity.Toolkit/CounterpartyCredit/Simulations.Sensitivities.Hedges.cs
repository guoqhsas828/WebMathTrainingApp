using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Ccr
{
  public partial class Simulations
  {
    /// <summary>
    /// Calculate the CVA hedge notionals in domestic currency for the given hedging portfolio. 
    /// </summary>
    /// <param name="calculations">Base calculation environment</param>
    /// <param name="netting">Netting information</param>
    /// <param name="rebalancingDate">Rebalancing date</param>
    /// <param name="arbitraryHedgePricers">Arbitrary (not part of CalibratedCurve tenors) hedge instruments  </param> 
    /// <param name="underlyingCurves">Calibrated term structures whose calibration instruments are used as hedges </param>
    /// <param name="hedgeInstrumentNames">Names of tenors to be used as hedges (null or empty to use all curve tenors)</param>
    /// <param name="dataTable">Tabulated results</param>
    /// <returns>Hedge notionals and distribution of hedging error</returns>
    public static DataTable CvaHedgeNotionals(ICounterpartyCreditRiskCalculations calculations, Dt rebalancingDate, Netting netting,
                                              IList<IPricer> arbitraryHedgePricers, CalibratedCurve[] underlyingCurves, string[] hedgeInstrumentNames,
                                              DataTable dataTable)
    {
      var calcs = calculations as CCRCalculations;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRCalculations");
      return GenerateHedgesTable(calcs, netting, rebalancingDate, arbitraryHedgePricers, underlyingCurves, hedgeInstrumentNames, dataTable);
    }

    /// <summary>
    /// Calculate the CVA hedge notionals in domestic currency for the given hedging portfolio. 
    /// </summary>
    /// <param name="calculations">Base calculation environment</param>
    /// <param name="netting">Netting information</param>
    /// <param name="rebalancingDate">Rebalancing date</param>
    /// <param name="paths">Precalculated paths</param>  
    /// <param name="arbitraryHedgePricers">Arbitrary (not part of CalibratedCurve tenors) hedge instruments</param> 
    /// <param name="underlyingCurves">Calibrated term structures whose calibration instruments are used as hedges </param>
    /// <param name="hedgeInstrumentNames">Names of tenors to be used as hedges (null or empty to use all curve tenors)</param>
    /// <param name="dataTable">Tabulated results</param>
    /// <returns>Hedge notionals and distribution of hedging error</returns>
    public static DataTable CvaHedgeNotionals(IRunSimulationPath calculations,
                                              Dt rebalancingDate,
                                              IEnumerable<ISimulatedPathValues> paths,
                                              Netting netting,
                                              IList<IPricer> arbitraryHedgePricers,
                                              CalibratedCurve[] underlyingCurves,
                                              string[] hedgeInstrumentNames,
                                              DataTable dataTable)
    {
      var calcs = calculations as CCRPathSimulator;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRPathSimulator");
      return GenerateHedgesTable(calcs, netting, rebalancingDate, arbitraryHedgePricers, underlyingCurves, hedgeInstrumentNames, paths, dataTable);
    }
  }
}
