using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Ccr
{
  public static partial class Simulations
  {
    #region Methods

    /// <summary>
    ///   Calculate the forward price (could be inflation, commodity price, stock price, etc.) sensitivity for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="bumpType">Type of bump to apply</param>
    ///<param name="targetQuoteType">Target quote type</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    ///<param name="dataTable">Data table</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ForwardPriceSensitivities(ICounterpartyCreditRiskCalculations calculations, Netting netting,
                                                      double upBump, double downBump, bool bumpRelative,
                                                      BumpType bumpType, QuotingConvention targetQuoteType,
                                                      string[] bumpTenors,
                                                      bool calcGamma, DataTable dataTable, params CalibratedCurve[] subsetBounds)
    {
      var calcs = calculations as CCRCalculations;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRCalculations");
      CalibratedCurve[] fwd = calcs.Environment.GetForwardCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = TermStructurePerturbation.Generate(fwd, upBump,
                                                                                     downBump, bumpRelative,
                                                                                     bumpType, QuotingConvention.None,
                                                                                     null, calcGamma);
      dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, dataTable);
      return dataTable ?? new DataTable();
    }


    /// <summary>
    ///   Calculate the rates sensitivity for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    /// <param name="paths">precalculated baseline path values</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="bumpType">Type of bump to apply</param>
    ///<param name="targetQuoteType">Target quote type</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    public static DataTable ForwardPriceSensitivities(IRunSimulationPath calculations,
                                                      IEnumerable<ISimulatedPathValues> paths, Netting netting,
                                                      double upBump, double downBump, bool bumpRelative,
                                                      BumpType bumpType,
                                                      QuotingConvention targetQuoteType,
                                                      string[] bumpTenors,
                                                      bool calcGamma, params CalibratedCurve[] subsetBounds)
    {
      var calcs = calculations as CCRPathSimulator;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRPathSimulator");
      CalibratedCurve[] dc = calcs.OriginalMarketData.GetForwardCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = TermStructurePerturbation.Generate(dc, 
                                                                                     upBump,
                                                                                     downBump, bumpRelative,
                                                                                     bumpType, targetQuoteType,
                                                                                     bumpTenors, calcGamma);
      var dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, paths, null);
      return dataTable ?? new DataTable();
    }


    /// <summary>
    ///   Calculate the the forward price volatility (could be inflation, commodity price, stock price, etc.) sensitivities for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    ///<param name="dataTable">Data table</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ForwardPriceVolatilitiesSensitivities(ICounterpartyCreditRiskCalculations calculations,
                                                                  Netting netting, double upBump, double downBump,
                                                                  bool bumpRelative, BumpType bumpType,
                                                                  string[] bumpTenors,
                                                                  bool calcGamma, DataTable dataTable,
                                                                  params CalibratedCurve[] subsetBounds)
    {
      var calcs = calculations as CCRCalculations;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRCalculations");
      CalibratedCurve[] fwd = calcs.Environment.GetForwardCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = VolatilityPerturbation.Generate(fwd, 
                                                                                  calcs.Volatilities, upBump, downBump,
                                                                                  bumpRelative, bumpType, calcGamma);
      dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, dataTable);
      return dataTable ?? new DataTable();
    }

    /// <summary>
    ///   Calculate the the forward price volatility (could be inflation, commodity price, stock price, etc.) sensitivities for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    ///<param name="paths">precalculated baseline path values</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ForwardPriceVolatilitiesSensitivities(IRunSimulationPath calculations,
                                                                  IEnumerable<ISimulatedPathValues> paths,
                                                                  Netting netting, double upBump,
                                                                  double downBump,
                                                                  bool bumpRelative, BumpType bumpType,
                                                                  string[] bumpTenors,
                                                                  bool calcGamma,
                                                                  params CalibratedCurve[] subsetBounds)
    {
      var calcs = calculations as CCRPathSimulator;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRPathSimulator");
      CalibratedCurve[] dc = calcs.OriginalMarketData.GetForwardCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = VolatilityPerturbation.Generate(dc, 
                                                                                  calcs.Volatilities,
                                                                                  upBump, downBump,
                                                                                  bumpRelative, bumpType, calcGamma);
      var dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, paths, null);
      return dataTable ?? new DataTable();
    }


    /// <summary>
    ///   Calculate the the forward price (could be inflation, commodity price, stock price, etc.) factor loading sensitivities for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    ///<param name="bumpType">Bump type</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    ///<param name="dataTable">Data table</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ForwardPriceFactorsSensitivities(ICounterpartyCreditRiskCalculations calculations,
                                                             Netting netting, double[] upBump, double[] downBump,
                                                             bool bumpRelative, BumpType bumpType, bool calcGamma,
                                                             DataTable dataTable, params CalibratedCurve[] subsetBounds)
    {
      var calcs = calculations as CCRCalculations;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRCalculations");
      CalibratedCurve[] fwd = calcs.Environment.GetForwardCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = FactorPerturbation.Generate(fwd, calcs.FactorLoadings, upBump, downBump,
                                                                              bumpRelative, bumpType, calcGamma);
      dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, dataTable);
      return dataTable ?? new DataTable();
    }


    /// <summary>
    ///   Calculate the factor loading sensitivities for CVA, DVA, EEs and NEEs
    /// </summary>
    /// <param name="calculations">Base calculation environment</param>
    ///<param name="paths">precalculated baseline path values</param>
    /// <param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    ///<param name="bumpType">Bump type</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    public static DataTable ForwardPriceFactorsSensitivities(IRunSimulationPath calculations,
                                                             IEnumerable<ISimulatedPathValues> paths,
                                                             Netting netting,
                                                             double[] upBump, double[] downBump,
                                                             bool bumpRelative,
                                                             BumpType bumpType, bool calcGamma,
                                                             params CalibratedCurve[] subsetBounds)
    {
      var calcs = calculations as CCRPathSimulator;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRPathSimulator");
      CalibratedCurve[] fwd = calcs.OriginalMarketData.GetForwardCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = FactorPerturbation.Generate(fwd, calcs.FactorLoadings,
                                                                              upBump, downBump, bumpRelative, bumpType,
                                                                              calcGamma);
      var dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, paths, null);
      return dataTable ?? new DataTable();
    }

    #endregion
  }
}