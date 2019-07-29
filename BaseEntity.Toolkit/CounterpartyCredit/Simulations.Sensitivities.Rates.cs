using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Ccr
{
  public static partial class Simulations
  {
    #region Methods

    /// <summary>
    ///   Calculate the rates sensitivity for CVA, DVA, EEs and NEEs
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
    public static DataTable RateSensitivities(ICounterpartyCreditRiskCalculations calculations, Netting netting,
                                              double upBump, double downBump, bool bumpRelative, BumpType bumpType,
                                              QuotingConvention targetQuoteType, string[] bumpTenors,
                                              bool calcGamma, DataTable dataTable, params DiscountCurve[] subsetBounds)
    {
      var calcs = calculations as CCRCalculations;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRCalculations");
      DiscountCurve[] dc = calcs.Environment.GetDiscountCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = TermStructurePerturbation.Generate(dc, upBump,
                                                                                     downBump, bumpRelative,
                                                                                     bumpType, targetQuoteType,
                                                                                     bumpTenors, calcGamma);
      dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, dataTable);
      return dataTable ?? new DataTable();
    }

    /// <summary>
    ///   Calculate the rates sensitivity for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    ///<param name="paths">precalculated base path values</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="bumpType">Type of bump to apply</param>
    ///<param name="targetQuoteType">Target quote type</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    public static DataTable RateSensitivities(IRunSimulationPath calculations,
                                              IEnumerable<ISimulatedPathValues> paths,
                                              Netting netting,
                                              double upBump, double downBump, bool bumpRelative,
                                              BumpType bumpType,
                                              QuotingConvention targetQuoteType, string[] bumpTenors,
                                              bool calcGamma, params DiscountCurve[] subsetBounds)
    {
      var calcs = calculations as CCRPathSimulator;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRPathSimulator");
      DiscountCurve[] dc = calcs.OriginalMarketData.GetDiscountCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = TermStructurePerturbation.Generate(dc, 
                                                                                     upBump,
                                                                                     downBump, bumpRelative,
                                                                                     bumpType, targetQuoteType,
                                                                                     bumpTenors, calcGamma);
      var dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, paths, null);
      return dataTable ?? new DataTable();
    }

    ///  <summary>
    ///    Generate perturbed simulators for rates sensitivity 
    ///  </summary>
    /// <param name="marketEnvironment">original market data</param>
    /// <param name="simulator">baseline simulator</param>
    /// <param name="upBump">Up bump size</param>
    ///  <param name="downBump">Down bump size</param>
    ///  <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    ///  <param name="bumpType">Type of bump to apply</param>
    /// <param name="targetQuoteType">Target quote type</param>
    ///  <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    ///  <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="subset">subset of discount curves to bump</param>
    public static IEnumerable<Perturbation> RateSensitivityPerturbations(MarketEnvironment marketEnvironment, Simulator simulator,
                                              double upBump, double downBump, bool bumpRelative,
                                              BumpType bumpType,
                                              QuotingConvention targetQuoteType, string[] bumpTenors,
                                              bool calcGamma, params DiscountCurve[] subset )
    {
      object[] dc;
      if (subset == null || subset.Length == 0)
        dc = marketEnvironment.DiscountCurves.Cast<object>().ToArray();
      else
        dc = subset.Where(marketEnvironment.DiscountCurves.Contains).Cast<object>().ToArray();
      Tuple<Perturbation[], bool> perturbations = TermStructurePerturbation.Generate(dc,
                                                                                     upBump,
                                                                                     downBump, bumpRelative,
                                                                                     bumpType, targetQuoteType,
                                                                                     bumpTenors, calcGamma);
      return perturbations.Item1;
    }

    /// <summary>
    ///   Calculate the volatility sensitivities for CVA, DVA, EEs and NEEs
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
    public static DataTable RateVolatilitiesSensitivities(ICounterpartyCreditRiskCalculations calculations,
                                                          Netting netting, double upBump, double downBump,
                                                          bool bumpRelative, BumpType bumpType, string[] bumpTenors,
                                                          bool calcGamma, DataTable dataTable, params DiscountCurve[] subsetBounds)
    {
      var calcs = calculations as CCRCalculations;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRCalculations");
      DiscountCurve[] dc = calcs.Environment.GetDiscountCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = VolatilityPerturbation.Generate(dc, calcs.Volatilities,
                                                                                  upBump, downBump,
                                                                                  bumpRelative, bumpType, calcGamma);
      dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, dataTable);
      return dataTable ?? new DataTable();
    }

    /// <summary>
    ///   Calculate the volatility sensitivities for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    ///<param name="paths">precalculated base path values</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    public static DataTable RateVolatilitiesSensitivities(IRunSimulationPath calculations,
                                                          IEnumerable<ISimulatedPathValues> paths,
                                                          Netting netting, double upBump, double downBump,
                                                          bool bumpRelative, BumpType bumpType,
                                                          string[] bumpTenors,
                                                          bool calcGamma, params DiscountCurve[] subsetBounds)
    {
      var calcs = calculations as CCRPathSimulator;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRPathSimulator");
      DiscountCurve[] dc = calcs.OriginalMarketData.GetDiscountCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = VolatilityPerturbation.Generate(dc, 
                                                                                  calcs.Volatilities,
                                                                                  upBump, downBump,
                                                                                  bumpRelative, bumpType, calcGamma);
      var dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, paths, null);
      return dataTable ?? new DataTable();
    }


    /// <summary>
    ///   Calculate the factor loading sensitivities for CVA, DVA, EEs and NEEs
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
    public static DataTable RateFactorsSensitivities(ICounterpartyCreditRiskCalculations calculations, Netting netting,
                                                     double[] upBump, double[] downBump, bool bumpRelative,
                                                     BumpType bumpType, bool calcGamma, DataTable dataTable,
                                                     params DiscountCurve[] subsetBounds)
    {
      var calcs = calculations as CCRCalculations;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRCalculations");
      DiscountCurve[] dc = calcs.Environment.GetDiscountCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = FactorPerturbation.Generate(dc, calcs.FactorLoadings,
                                                                              upBump, downBump,
                                                                              bumpRelative, bumpType, calcGamma);
      dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, dataTable);
      return dataTable ?? new DataTable();
    }


    /// <summary>
    ///   Calculate the factor loading sensitivities for CVA, DVA, EEs and NEEs
    /// </summary>
    ///<param name="calculations">Base calculation environment</param>
    ///<param name="paths">precalculated base path values</param>
    ///<param name="netting">Netting information</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    ///<param name="bumpType">Bump type</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="subsetBounds">Start and end index of the subset of curves we want to perturb</param>
    public static DataTable RateFactorsSensitivities(IRunSimulationPath calculations,
                                                     IEnumerable<ISimulatedPathValues> paths,
                                                     Netting netting,
                                                     double[] upBump, double[] downBump, bool bumpRelative,
                                                     BumpType bumpType, bool calcGamma,
                                                     params DiscountCurve[] subsetBounds)
    {
      var calcs = calculations as CCRPathSimulator;
      if (calcs == null)
        throw new ArgumentException("Calculations is expected to be of type CCRPathSimulator");
      DiscountCurve[] dc = calcs.OriginalMarketData.GetDiscountCurves(subsetBounds);
      Tuple<Perturbation[], bool> perturbations = FactorPerturbation.Generate(dc, calcs.FactorLoadings,
                                                                              upBump, downBump,
                                                                              bumpRelative, bumpType, calcGamma);
      var dataTable = GenerateSensitivitiesTable(calcs, netting, perturbations, paths, null);
      return dataTable ?? new DataTable();
    }

    #endregion
  }
}