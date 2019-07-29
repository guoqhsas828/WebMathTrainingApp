/*
 * BasketPricerFactory.cs
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  ///
	/// <summary>
	///   Helper factory methods for basket pricer objects
	/// </summary>
	///
	/// <remarks>
	///   This class provides static functions for creating various pricers of basket products, such as
  ///   CDOs, CDO Squareds and NTDs.
	/// </remarks>
	///
	public static class BasketPricerFactory
  {
    #region Config
    // Added 9.1
    private static readonly double defaultAccuracy_ = 1E-5;
    #endregion Config

    #region Basket Pricers

    /// <summary>
    ///   Analytical pricing model for synthetic CDO products associated
    ///   with homogeneous large pool baskets.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>UniformBasketPricer assumes that individual names have the same notionals, the same
    ///   deterministic recovery rates, the same survival curves, and the same correlation factors.
    ///   Under these assumptions, this model computes the exact loss distributions.
    ///   It achieves the same speed as the so-called "large pool model"
    ///   but it works with small basket also.</para>
    /// </remarks>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name (may contain nulls which are ignored)</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    ///
    /// <returns>Constructed uniform basket pricer</returns>
    ///
    static public LargePoolBasketPricer
    LargePoolBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      double[,] lossLevels,
      int quadraturePoints
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      LargePoolBasketPricer basket;
      if (correlation is BaseCorrelationObject)
        basket = new LargePoolBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins, 
          copula, DefaultSingleFactorCorrelation(sc), stepSize, stepUnit, lossLevels);
      else
      {
        SingleFactorCorrelation corr = CorrelationFactory.CreateSingleFactorCorrelation((Correlation)correlation, picks);
        basket = new LargePoolBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins,
          copula, corr, stepSize, stepUnit, lossLevels);
      }
      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }

    /// <summary>
    ///   Analytical pricing model for synthetic CDO products associated
    ///   with uniform baskets.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>UniformBasketPricer assumes that individual names have the same notionals, the same
    ///   deterministic recovery rates, the same survival curves, and the same correlation factors.
    ///   Under these assumptions, this model computes the exact loss distributions.
    ///   It achieves the same speed as the so-called "large pool model"
    ///   but it works with small basket also.</para>
    /// </remarks>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name (may contain nulls which are ignored)</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    ///
    /// <returns>Constructed uniform basket pricer</returns>
    ///
    static public UniformBasketPricer
    UniformBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      double[,] lossLevels,
      int quadraturePoints
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      UniformBasketPricer basket;
      if (correlation is BaseCorrelationObject)
        basket = new UniformBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins, copula,
                                          DefaultSingleFactorCorrelation(sc), stepSize, stepUnit, lossLevels);
      else
      {
        SingleFactorCorrelation corr = CorrelationFactory.CreateSingleFactorCorrelation((Correlation)correlation, picks);
        basket = new UniformBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins, copula,
                                          corr, stepSize, stepUnit, lossLevels);
      }
      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }

    /// <summary>
    ///   Analytical pricing model for synthetic CDO products associated
    ///   with heterogeneous baskets.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities (or 0 for default)</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    ///
    /// <returns>Constructed heterogeneous basket</returns>
    ///
    static public SemiAnalyticBasketPricer
    SemiAnalyticBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      object correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels,
      int quadraturePoints,
      double gridSize,
      bool checkRefinance
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      SemiAnalyticBasketPricer basket;
      if (correlation is BaseCorrelationObject)
        basket = new SemiAnalyticBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins,
          copula, DefaultFactorCorrelation(sc), stepSize, stepUnit, lossLevels,
          checkRefinance);

      else
      {
        if (correlation is double)
          correlation = new SingleFactorCorrelation(new string[sc.Length], (double)correlation);
        else if (correlation is double[])
          correlation = new FactorCorrelation(new string[sc.Length],
            ((double[])correlation).Length / sc.Length, (double[])correlation);

        if (correlation is CorrelationObject)
        {
          FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation((Correlation)correlation, picks);
          basket = new SemiAnalyticBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins,
            copula, corr, stepSize, stepUnit, lossLevels, checkRefinance);
        }
        else
          throw new ArgumentException("Invalid correlation parameter");
      }
      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (gridSize > 0.0)
        basket.GridSize = gridSize;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }

    /// <summary>
    ///   Analytical pricing model for synthetic CDO products associated
    ///   with heterogeneous baskets.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="maturities">If not null, contains the early maturity dates by names</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities (or 0 for default)</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    ///
    /// <returns>Constructed heterogeneous basket</returns>
    ///
    static public BasketPricer SemiAnalyticBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      Dt[] maturities,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      object correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels,
      int quadraturePoints,
      double gridSize,
      bool checkRefinance
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);
      Dt[] ms = ArrayUtil.PickElements(maturities, picks);

      SemiAnalyticBasketPricer basket;
      if (correlation is BaseCorrelationObject)
      {
        BaseCorrelationObject bco = correlation as BaseCorrelationObject;
        basket = new SemiAnalyticBasketPricer(asOfDate, settleDate, maturityDate,
          ms, sc, rc, prins, copula, DefaultFactorCorrelation(sc),
          stepSize, stepUnit, lossLevels, checkRefinance);
        basket.RecoveryCorrelationModel = bco.RecoveryCorrelationModel;
      }
      else
      {
        if (correlation is double)
          correlation = new SingleFactorCorrelation(new string[sc.Length], (double)correlation);
        else if (correlation is double[])
          correlation = new FactorCorrelation(new string[sc.Length],
            ((double[])correlation).Length / sc.Length, (double[])correlation);

        if (correlation is CorrelationTermStruct && copula.CopulaType == CopulaType.Poisson)
        {
          basket = new SemiAnalyticBasketPricer(asOfDate, settleDate, maturityDate, ms, sc, rc, prins,
            copula, (CorrelationTermStruct)correlation, stepSize, stepUnit, lossLevels, checkRefinance);
        }
        else if (correlation is CorrelationObject)
        {
          FactorCorrelation corr = correlation is SingleFactorCorrelation ?
            (SingleFactorCorrelation)correlation :
            CorrelationFactory.CreateFactorCorrelation((Correlation)correlation, picks);
          basket = new SemiAnalyticBasketPricer(asOfDate, settleDate, maturityDate, ms, sc, rc, prins,
            copula, corr, stepSize, stepUnit, lossLevels, checkRefinance);
        }
        else
          throw new ArgumentException("Invalid correlation parameter");
      }
      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (gridSize > 0.0)
        basket.GridSize = gridSize;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }
    
    /// <summary>
    ///   Create Homogeneous basket pricer
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    ///
    /// <returns>Constructed homogeneous basket pricer</returns>
    ///
    static public HomogeneousBasketPricer
    HomogeneousBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      double[,] lossLevels,
      int quadraturePoints
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      HomogeneousBasketPricer basket;
      if (correlation is BaseCorrelationObject)
        basket = new HomogeneousBasketPricer(asOfDate, settleDate, maturityDate,
                                             sc, rc, prins, copula, DefaultFactorCorrelation(sc),
                                             stepSize, stepUnit, lossLevels);
      else
      {
        FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation((Correlation)correlation, picks);
        basket = new HomogeneousBasketPricer(asOfDate, settleDate, maturityDate,
                                             sc, rc, prins, copula, corr,
                                             stepSize, stepUnit, lossLevels);
      }
      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }
    
    /// <summary>
    ///   Analytical pricing model for synthetic CDO products associated
    ///   with heterogeneous baskets.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities (or 0 for default)</param>
    ///
    /// <returns>Constructed heterogeneous basket</returns>
    ///
    static public HeterogeneousBasketPricer
    HeterogeneousBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      object correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels,
      int quadraturePoints,
      double gridSize
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      HeterogeneousBasketPricer basket;
      if (correlation is BaseCorrelationObject)
        basket = new HeterogeneousBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins,
                                                copula, DefaultFactorCorrelation(sc), stepSize, stepUnit, lossLevels);

      else
      {
        if (correlation is double)
          correlation = new SingleFactorCorrelation(new string[sc.Length], (double)correlation);
        else if (correlation is double[])
          correlation = new FactorCorrelation(new string[sc.Length],
            ((double[])correlation).Length / sc.Length, (double[])correlation);

        if (correlation is CorrelationObject)
        {
          FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation((Correlation)correlation, picks);
          basket = new HeterogeneousBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins,
                                                  copula, corr, stepSize, stepUnit, lossLevels);
        }
        else
          throw new ArgumentException("Invalid correlation parameter");
      }
      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (gridSize > 0.0)
        basket.GridSize = gridSize;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }
    
    /// <summary>
    ///   Create a Monte Carlo basket pricer.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
    ///
    /// <returns>Constructed Monte Carlo basket pricer.</returns>
    ///
    static public MonteCarloBasketPricer
    MonteCarloBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      double[,] lossLevels,
      int sampleSize,
      int seed
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      MonteCarloBasketPricer basket;
      if (correlation is BaseCorrelationObject)
        basket = new MonteCarloBasketPricer(asOfDate, settleDate, maturityDate,
                                             sc, rc, prins, copula, DefaultGeneralCorrelation(sc),
                                             stepSize, stepUnit, lossLevels, sampleSize);
      else
      {
        GeneralCorrelation corr = CorrelationFactory.CreateGeneralCorrelation((Correlation)correlation, picks);
        basket = new MonteCarloBasketPricer(asOfDate, settleDate, maturityDate,
                                             sc, rc, prins, copula, corr,
                                             stepSize, stepUnit, lossLevels, sampleSize);
      }
      basket.Seed = seed;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }
    
    /// <summary>
    ///   Create a Monte Carlo basket pricer for pricing NTDs.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
    ///
    /// <returns>Constructed Monte Carlo basket pricer.</returns>
    ///
    static public BasketForNtdPricer
    MonteCarloBasketForNtdPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      int seed
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      MonteCarloBasketForNtdPricer basket;
      if (correlation is BaseCorrelationObject)
      {
        basket = new MonteCarloBasketForNtdPricer(asOfDate, settleDate, maturityDate,
                                                  sc, rc, prins, copula, DefaultGeneralCorrelation(sc),
                                                  stepSize, stepUnit);
      }
      else
      {
        GeneralCorrelation corr = CorrelationFactory.CreateGeneralCorrelation((Correlation) correlation, picks);
        basket = new MonteCarloBasketForNtdPricer(asOfDate, settleDate, maturityDate,
                                                  sc, rc, prins, copula, corr,
                                                  stepSize, stepUnit);
      }

      if (sampleSize > 0)
        basket.SampleSize = sampleSize;
      basket.Seed = seed;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;
      return basket;
    }

    /// <summary>
    ///   Create a semi-analytic pricer for pricing NTDs.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    ///
    /// <returns>Constructed Monte Carlo basket pricer.</returns>
    ///
    static public BasketForNtdPricer
    SemiAnalyticBasketForNtdPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      SemiAnalyticBasketForNtdPricer basket;
      if (correlation is BaseCorrelationObject)
      {
        basket = new SemiAnalyticBasketForNtdPricer(asOfDate, settleDate, maturityDate,
                                                    sc, rc, prins, copula, DefaultFactorCorrelation(sc),
                                                    stepSize, stepUnit);
      }
      else
      {
        FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation((Correlation) correlation, picks);
        basket = new SemiAnalyticBasketForNtdPricer(asOfDate, settleDate, maturityDate,
                                                    sc, rc, prins, copula, corr,
                                                    stepSize, stepUnit);
      }
      if (quadraturePoints > 0)
        basket.IntegrationPointsFirst = quadraturePoints;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;
      return basket;
    }

    /// <summary>
    ///   Create a Forward Loss Model basket pricer.
    /// </summary>
    /// <remarks>
    ///   <para>This model employs HJM-type forward loss rates model as its basic approach to
    ///   pricing <see cref="SyntheticCDO">Synthetic CDOs</see>.</para>
    /// </remarks>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="indexNotes">Array of index notes by tenors</param>
    /// <param name="indexSpreads">Array of quoted index spreads</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="stateIndices">Array of segment indices</param>
    /// <param name="transitionCoefs">Probability distribution transition coefs</param>
    /// <param name="scalingFactors">Scaling factors for transition rates</param>
    /// <param name="baseLevels">Base levels for transition rates</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <returns>Constructed Forward Loss Model basket pricer.</returns>
    static public ForwardLossBasketPricer
    ForwardLossBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CDX[] indexNotes,
      double[] indexSpreads,
      DiscountCurve discountCurve,
      int[] stateIndices,
      double[,] transitionCoefs,
      double[] scalingFactors,
      double[] baseLevels,
      int stepSize,
      TimeUnit stepUnit
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      int basketSize = sc.Length;

      // default values
      double alpha = 0;
      double beta = 0;
      int flat = 0;
      bool needExtraParam = false;

      // Create state losses array
      double[] stateLosses = BasketPricers.ForwardLossBasketPricer.GetDefaultStateLosses(basketSize);

      // Check scaling factors and base levels
      if (null == scalingFactors || 0 == scalingFactors.Length)
        scalingFactors = BasketPricers.ForwardLossBasketPricer.GetDefaultScalingFactors(basketSize, 0.0);
      else if (scalingFactors.Length == 1)
        scalingFactors = BasketPricers.ForwardLossBasketPricer.GetDefaultScalingFactors(basketSize, scalingFactors[0]);
      else if (scalingFactors.Length != basketSize + 1)
      {
        if (scalingFactors.Length == 4)
        {
          needExtraParam = true;
          alpha = scalingFactors[1];
          beta = scalingFactors[2];
          flat = (int)scalingFactors[3];
          scalingFactors = BasketPricers.ForwardLossBasketPricer.GetDefaultScalingFactors(basketSize, scalingFactors[0]);
        }
        else if (scalingFactors.Length == basketSize + 4)
        {
          needExtraParam = true;
          alpha = scalingFactors[0];
          beta = scalingFactors[1];
          flat = (int)scalingFactors[2];
          double[] tmp = new double[basketSize + 1];
          for (int i = 0; i <= basketSize; ++i)
            tmp[i] = scalingFactors[i + 3];
          scalingFactors = tmp;
        }
      }

      if (null == baseLevels || 0 == baseLevels.Length)
        baseLevels = BasketPricers.ForwardLossBasketPricer.GetDefaultBaseLevels(basketSize);

      if (stateLosses.Length != basketSize + 1)
        throw new ArgumentException(String.Format("Number of state losses {0} must match number of states {1}",
                                                  scalingFactors.Length, basketSize + 1));

      if (scalingFactors.Length != basketSize + 1)
        throw new ArgumentException(String.Format("Number of scaling factors {0} must match number of states {1}",
                                                  scalingFactors.Length, basketSize + 1));
      if (baseLevels.Length != basketSize + 1)
        throw new ArgumentException(String.Format("Number of base levels {0} must match number of states {1}",
                                                  baseLevels.Length, basketSize + 1));

      // Create basket pricer
      ForwardLossBasketPricer basket = new ForwardLossBasketPricer(asOfDate, settleDate, maturityDate, sc,
                                                                   rc, prins, stepSize, stepUnit, indexNotes,
                                                                   indexSpreads,
                                                                   discountCurve, stateLosses, transitionCoefs,
                                                                   scalingFactors,
                                                                   baseLevels, stateIndices);
      if (needExtraParam)
      {
        BasketPricers.ForwardLossBasketPricer.Alpha = alpha;
        BasketPricers.ForwardLossBasketPricer.Beta = beta;
        BasketPricers.ForwardLossBasketPricer.Flat = flat;
      }
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }


    /// <summary>
    ///   Semi-Analytical pricing model for CDO Squared tranche.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="attachments">Attachment points of child CDOs</param>
    /// <param name="detachments">Detachment points of child CDOs</param>
    /// <param name="cdoMaturities">Same of different underlying CDO maturities</param>
    /// <param name="crossSub">If true, with cross subordination</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="discountCurve">Discount curve used to interpolate base correlation (can be null for non base correlation pricer</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (defaults to reasonable guess if zero)</param>
    /// <param name="gridSize">The grid used to update probabilities (or 0 for default)</param>
    ///
    /// <returns>Constructed Semi-Analytic CDO2 basket</returns>
    ///
    static public CDOSquaredBasketPricer
    SemiAnalyticCDO2BasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[,] principals,
      double[] attachments,
      double[] detachments,
      Dt[] cdoMaturities,
      bool crossSub,
      Copula copula,
      object correlation,
      DiscountCurve discountCurve,
      int stepSize,
      TimeUnit stepUnit,
      double[,] lossLevels,
      int sampleSize,
      int quadraturePoints,
      double gridSize
      )
    {
      // Argument validation
      int basketSize = survivalCurves.Length;
      if (basketSize != principals.GetLength(0))
        throw new ArgumentException("Number of principal rows must match number of survival curves");

      int nChild = attachments.Length;
      if (nChild != detachments.Length)
        throw new ArgumentException("Number of attachment points must mach the number detachment points");
      if (nChild != principals.GetLength(1))
        throw new ArgumentException("Number of principal columns must match number of attachment points");

      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, new double[1], out sc, out rc, out prins, out picks);

      prins = new double[sc.Length * nChild];
      for (int i = 0, idx = 0; i < nChild; ++i)
      {
        for (int j = 0; j < basketSize; ++j)
          if (picks[j] != 0.0)
            prins[idx++] = principals[j, i];
      }

      // Default sample size
      if (sampleSize <= 0)
        sampleSize = 5000;

      CDOSquaredBasketPricer basket;

      // Get correlation
      if (correlation is BaseCorrelationObject)
      {
        basket = new BaseCorrelationCDO2BasketPricer(asOfDate, settleDate, maturityDate, discountCurve,
                                                     sc, rc, prins, attachments, detachments, cdoMaturities,
                                                     crossSub, copula, (BaseCorrelationObject)correlation,
                                                     stepSize, stepUnit, lossLevels, sampleSize);
      }
      else if (correlation is double[])
      {
        double[] corr = (double[])correlation;
        if (basketSize != sc.Length)
        {
          double[] orig = (double[])correlation;
          corr = new double[sc.Length];
          for (int i = 0, idx = 0; i < basketSize; ++i)
            if (picks[i] != 0.0) corr[idx++] = orig[i];
        }
        basket = new TrancheCorrelationCDO2BasketPricer(asOfDate, settleDate, maturityDate,
                                                        sc, rc, prins, attachments, detachments, cdoMaturities,
                                                        crossSub, copula, corr,
                                                        stepSize, stepUnit, lossLevels, sampleSize);
      }
      else if (correlation is Correlation)
      {
        FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation((Correlation)correlation);

        basket = new SemiAnalyticCDO2BasketPricer(asOfDate, settleDate, maturityDate,
                                                  sc, rc, prins, attachments, detachments, cdoMaturities,
                                                  crossSub, copula, corr,
                                                  stepSize, stepUnit, lossLevels, sampleSize);
      }
      else
        throw new ArgumentException("Invalid correlation (neither a correlation object or an array");

      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (gridSize > 0.0)
        basket.GridSize = gridSize;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }
    
    /// <summary>
    ///   Analytical pricing model for synthetic CDO products associated
    ///   with heterogeneous baskets.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="cashflowStreams">ABS cashflows</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="dates">Time grid used to approximate the loss distribution as a function of time</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities (or 0 for default)</param>
    ///
    /// <returns>Constructed heterogeneous basket</returns>
    ///
    static public ABSBasketPricer
    ABSBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CashflowStream[] cashflowStreams,
      Copula copula,
      object correlation,
      Dt[] dates,
      Array lossLevels,
      int quadraturePoints,
      double gridSize
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      // set up cashflow generators
      if(sc.Length != survivalCurves.Length)
      {
        CashflowStream[] tmp = new CashflowStream[sc.Length];
        for (int i = 0, idx = 0; i < picks.Length; ++i)
          if (picks[i] > 0)
            tmp[idx++] = cashflowStreams[i];
        cashflowStreams = tmp;
      }

      ABSBasketPricer basket;
      if (correlation is BaseCorrelationObject)
      {
        basket = new ABSBasketPricer(portfolioStart, asOfDate, settleDate, maturityDate, sc, rc, prins,
          cashflowStreams, copula, DefaultFactorCorrelation(sc), dates, lossLevels);
      }
      else
      {
        if (correlation is double)
          correlation = new SingleFactorCorrelation(new string[sc.Length], (double)correlation);
        else if (correlation is double[])
          correlation = new FactorCorrelation(new string[sc.Length],
            ((double[])correlation).Length / sc.Length, (double[])correlation);

        if (correlation is CorrelationObject)
        {
          FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation((Correlation)correlation, picks);
          basket = new ABSBasketPricer(portfolioStart, asOfDate, settleDate, maturityDate, sc, rc, prins,
            cashflowStreams, copula, corr, dates, lossLevels);
        }
        else
          throw new ArgumentException("Invalid correlation parameter");
      }
      if (quadraturePoints <= 0)
        quadraturePoints = DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (gridSize > 0.0)
        basket.GridSize = gridSize;

      return basket;
    }
    
    #endregion Basket Pricers

    #region CDO Pricers
    /// <summary>
    ///   Validating inputs.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <param name="products">Products array (CDO, NTD)</param>
    /// <param name="notional">Notional array</param>
    /// <param name="rateResets">Array of rate resets list for each product</param>
    /// <exclude />
    public static void Validate(
      IProduct[] products,
      double[] notional,
      List<RateReset>[] rateResets
      )
    {
      //- Product array cannot be empty
      if (products.Length < 1)
        throw new ArgumentException("Must specify at least one CDO");

      //- Notional cannot be zero
      if (notional != null && notional.Length > 1)
      {
        if (notional.Length != products.Length)
          throw new ArgumentException(String.Format("Number of notionals {0} must be 1 or match number of CDOs/NTDs {1}", notional.Length, products.Length));
        for (int i = 0; i < notional.Length; ++i)
          if (products[i] != null && notional[i] == 0.0)
          {
            throw new ArgumentException(String.Format("Notional on CDO[{0}] or NTD[{1]] is Zero", i, i));
          }
      }

      //- Rate resets, if supplied, must be consisten with CDO array 
      if (rateResets != null && rateResets.Length > 1 && rateResets.Length != products.Length)
        throw new ArgumentException(String.Format("Number of rateResets {0} must be 1 or match number of CDOs/NTDs {1}", rateResets.Length, products.Length));
      return;
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the large-pool basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method provides a convenient wrapper for the
    ///   <see cref="SyntheticCDOHomogeneousPricer">CDO Homogeneous Pricer</see>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed large-pool Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerLargePool(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
     return CDOPricerLargePool(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        quadraturePoints,
        notional,
        rescaleStrikes,
        rateResets);
    }


    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the large-pool basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method provides a convenient wrapper for the
    ///   <see cref="SyntheticCDOHomogeneousPricer">CDO Homogeneous Pricer</see>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    ///<param name="referenceCurve">Reference Curve for floating payments forecasts</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed large-pool Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerLargePool(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo);

      // Create a basket pricer
      LargePoolBasketPricer basket = LargePoolBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels, quadraturePoints);
      if (quadraturePoints <= 0)
        basket.IntegrationPointsFirst +=
          DefaultQuadraturePointsAdjust(copula.CopulaType, cdo);

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
        if ((cdo[i] != null) &&
          ((notional == null) || (notional.Length == 0) ||
          (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
            basketPricer = new BaseCorrelationBasketPricer(basket, discountCurve, (BaseCorrelationObject)correlation,
              rescaleStrikes, cdo[i].Attachment, cdo[i].Detachment);
          else
            basketPricer = basket;

          // make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new SyntheticCDOPricer(cdo[i], basketPricer, discountCurve, referenceCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basketPricer.TotalPrincipal * cdo[i].TrancheWidth
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the large-pool basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method provides a convenient wrapper for the
    ///   <see cref="SyntheticCDOHomogeneousPricer">CDO Homogeneous Pricer</see>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed large-pool Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerUniform(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerUniform(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        quadraturePoints,
        notional,
        rescaleStrikes,
        rateResets);
    }



    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the large-pool basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method provides a convenient wrapper for the
    ///   <see cref="SyntheticCDOHomogeneousPricer">CDO Homogeneous Pricer</see>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed large-pool Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerUniform(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo);

      // Create a basket pricer
      UniformBasketPricer basket = UniformBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels, quadraturePoints);
      if (quadraturePoints <= 0)
        basket.IntegrationPointsFirst +=
          DefaultQuadraturePointsAdjust(copula.CopulaType, cdo);

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
        if ((cdo[i] != null) &&
            ((notional == null) || (notional.Length == 0) ||
            (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
            basketPricer = new BaseCorrelationBasketPricer(basket, discountCurve, (BaseCorrelationObject)correlation,
                                                            rescaleStrikes, cdo[i].Attachment, cdo[i].Detachment);
          else
            basketPricer = basket;

          //- make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new SyntheticCDOPricer(cdo[i], basketPricer, discountCurve, referenceCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basketPricer.TotalPrincipal * cdo[i].TrancheWidth
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer CDOPricerSemiAnalytic(
      SyntheticCDO cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double notional,
      bool rescaleStrikes,
      bool checkRefinance,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerSemiAnalytic(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        quadraturePoints,
        gridSize,
        notional,
        rescaleStrikes,
        checkRefinance,
        rateResets);
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer CDOPricerSemiAnalytic(
      SyntheticCDO cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double notional,
      bool rescaleStrikes,
      bool checkRefinance,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerSemiAnalytic(new SyntheticCDO[] { cdo }, portfolioStart, asOfDate, settleDate,
        discountCurve, referenceCurve, null, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, quadraturePoints, gridSize, (notional != 0) ? new double[] { notional } : null,
        rescaleStrikes, checkRefinance, rateResets)[0];
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerSemiAnalytic(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes,
      bool checkRefinance,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerSemiAnalytic(cdo, portfolioStart, asOfDate, settleDate,
        discountCurve, null, null, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, quadraturePoints, gridSize, notional,
        rescaleStrikes, checkRefinance, rateResets);
    }


    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="maturities">If not null, contains the early maturity dates by names</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerSemiAnalytic(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      Dt[] maturities,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes,
      bool checkRefinance,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerSemiAnalytic(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        maturities,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        quadraturePoints,
        gridSize,
        notional,
        rescaleStrikes,
        checkRefinance,
        rateResets);
    }


    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="maturities">If not null, contains the early maturity dates by names</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerSemiAnalytic(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      Dt[] maturities,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes,
      bool checkRefinance,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo);

      // Create a basket pricer
      BasketPricer basket = SemiAnalyticBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, maturities, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels, quadraturePoints, gridSize,
        checkRefinance);
      if (quadraturePoints <= 0)
        basket.IntegrationPointsFirst +=
          DefaultQuadraturePointsAdjust(copula.CopulaType, cdo);
      AddGridDates(basket, cdo);
      double maxBasketAmortLevel = basket.MaximumAmortizationLevel();
      double minCdoAmortLevel = MinimumAmortizationLevel(cdo);
      if (maxBasketAmortLevel <= minCdoAmortLevel)
      {
        basket.NoAmortization = true;
        basket.LossLevelAddComplement = false; //enabling cutting down distributions
      }

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
      {
        if ((cdo[i] != null) &&
            ((notional == null) || (notional.Length == 0) ||
             (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
          {
            basketPricer = new BaseCorrelationBasketPricer(
                basket, discountCurve, (BaseCorrelationObject)correlation,
                rescaleStrikes, cdo[i].Attachment, cdo[i].Detachment);
            basketPricer.Maturity = cdo[i].Maturity;
            if (maxBasketAmortLevel <= MinimumAmortizationLevel(cdo[i]))
              basketPricer.NoAmortization = true;
          }
          else
            basketPricer = basket;

          // make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new SyntheticCDOPricer(cdo[i], basketPricer, discountCurve, referenceCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                   ? basketPricer.TotalPrincipal * cdo[i].TrancheWidth
                                   : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }
      }
      // done
      return pricer;
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the semi-analytic homogeneous basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHomogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed homogeneous semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerHomogeneous(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerHomogeneous(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        quadraturePoints,
        notional,
        rescaleStrikes);
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the semi-analytic homogeneous basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHomogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed homogeneous semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerHomogeneous(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo);

      // Create a basket pricer
      HomogeneousBasketPricer basket = HomogeneousBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels, quadraturePoints);
      if (quadraturePoints <= 0)
        basket.IntegrationPointsFirst +=
          DefaultQuadraturePointsAdjust(copula.CopulaType, cdo);

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
        if ((cdo[i] != null) &&
            ((notional == null) || (notional.Length == 0) ||
            (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
            basketPricer = new BaseCorrelationBasketPricer(basket, discountCurve, (BaseCorrelationObject)correlation,
                                                            rescaleStrikes, cdo[i].Attachment, cdo[i].Detachment);
          else
            basketPricer = basket;

          //- make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new SyntheticCDOPricer(cdo[i], basketPricer, discountCurve, referenceCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basketPricer.TotalPrincipal * cdo[i].TrancheWidth
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }

    
    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerHeterogeneous(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerHeterogeneous(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        quadraturePoints,
        gridSize,
        notional,
        rescaleStrikes,
        rateResets);
    }


    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerHeterogeneous(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve, 
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo);

      // Create a basket pricer
      HeterogeneousBasketPricer basket = HeterogeneousBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels, quadraturePoints, gridSize);
      if (quadraturePoints <= 0)
        basket.IntegrationPointsFirst +=
          DefaultQuadraturePointsAdjust(copula.CopulaType, cdo);

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
        if ((cdo[i] != null) &&
            ((notional == null) || (notional.Length == 0) ||
            (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
          {
            basketPricer = new BaseCorrelationBasketPricer(
                basket, discountCurve, (BaseCorrelationObject)correlation,
                rescaleStrikes, cdo[i].Attachment, cdo[i].Detachment);
          }
          else
            basketPricer = basket;

          // make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new SyntheticCDOPricer(cdo[i], basketPricer, discountCurve, referenceCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basketPricer.TotalPrincipal * cdo[i].TrancheWidth
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the Monte Carlo models.
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOMonteCarloPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed Monte Carlo Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerMonteCarlo(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      double[] notional,
      bool rescaleStrikes,
      int seed,
      params List<RateReset>[] rateResets
      )
    {
      return CDOPricerMonteCarlo(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        sampleSize,
        notional,
        rescaleStrikes,
        seed,
        rateResets);
    }


    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the Monte Carlo models.
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOMonteCarloPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed Monte Carlo Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerMonteCarlo(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      double[] notional,
      bool rescaleStrikes,
      int seed,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo);

      // Create a basket pricer
      MonteCarloBasketPricer basket = MonteCarloBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels, sampleSize, seed);

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
        if ((cdo[i] != null) &&
            ((notional == null) || (notional.Length == 0) ||
            (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
            basketPricer = new BaseCorrelationBasketPricer(basket, discountCurve, (BaseCorrelationObject)correlation,
                                                            rescaleStrikes, cdo[i].Attachment, cdo[i].Detachment);
          else
            basketPricer = basket;

          //- make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new SyntheticCDOPricer(cdo[i], basketPricer, discountCurve, referenceCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basketPricer.TotalPrincipal * cdo[i].TrancheWidth
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }


     /// <summary>
    ///   Creates a pricer for Synthetic CDO using the Forward Loss Model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOForwardLossModelPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="indexNotes">Array of index notes by tenors</param>
    /// <param name="indexSpreads">Array of quoted index spreads</param>
    /// <param name="stateIndices">Array of segment indices</param>
    /// <param name="transitionCoefs">Probability distribution transition coefs</param>
    /// <param name="scalingFactors">Scaling factors for transition rates</param>
    /// <param name="baseLevels">Base levels for transition rates</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed Forward Loss Model pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerForwardLoss(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CDX[] indexNotes,
      double[] indexSpreads,
      int[] stateIndices,
      double[,] transitionCoefs,
      double[] scalingFactors,
      double[] baseLevels,
      int stepSize,
      TimeUnit stepUnit,
      double[] notional,
      params List<RateReset>[] rateResets
      )
     {
      return CDOPricerForwardLoss(
        cdo,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        indexNotes,
        indexSpreads,
        stateIndices,
        transitionCoefs,
        scalingFactors,
        baseLevels,
        stepSize,
        stepUnit,
        notional,
        rateResets);
     }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the Forward Loss Model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOForwardLossModelPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    ///<param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="indexNotes">Array of index notes by tenors</param>
    /// <param name="indexSpreads">Array of quoted index spreads</param>
    /// <param name="stateIndices">Array of segment indices</param>
    /// <param name="transitionCoefs">Probability distribution transition coefs</param>
    /// <param name="scalingFactors">Scaling factors for transition rates</param>
    /// <param name="baseLevels">Base levels for transition rates</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed Forward Loss Model pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerForwardLoss(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CDX[] indexNotes,
      double[] indexSpreads,
      int[] stateIndices,
      double[,] transitionCoefs,
      double[] scalingFactors,
      double[] baseLevels,
      int stepSize,
      TimeUnit stepUnit,
      double[] notional,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Create a basket pricer
      ForwardLossBasketPricer basket = ForwardLossBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        indexNotes, indexSpreads, discountCurve,
        stateIndices, transitionCoefs, scalingFactors, baseLevels,
        stepSize, stepUnit);

      // Create Synthetic CDO Pricers
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
        if ((cdo[i] != null) &&
            ((notional == null) || (notional.Length == 0) ||
            (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          pricer[i] = new SyntheticCDOPricer(cdo[i], basket, discountCurve, referenceCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basket.TotalPrincipal * cdo[i].TrancheWidth
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }

    
    /// <summary>
    ///   Creates a pricer for NTD using the generalised semi-analytic basket model.
    /// </summary>
    ///
    /// <param name="ntd">Nth to default product</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">List of reset rates</param>
    /// <returns>Constructed generalised semi-analytic NTD pricer</returns>
    ///
    static public FTDPricer
    NTDPricerSemiAnalytic(
      FTD ntd,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      params List<RateReset>[] rateResets
      )
    {
      return NTDPricerSemiAnalytic(new FTD[] { ntd }, portfolioStart, asOfDate,
        settleDate, discountCurve, survivalCurves, principals, copula, correlation, stepSize,
        stepUnit, quadraturePoints, notional, rateResets)[0];
    }

    /// <summary>
    /// Creates a pricer for Quanto NTD using the generalised semi-analytic basket model under the assumption of Gaussian copula. 
    /// The numeraire currency is the discount curve currency. 
    /// The survival curves, as well as default time correlations, are given under the measure associated to 
    /// a foreign numeraire.
    /// </summary>
    ///
    /// <param name="ntd">Nth to default product</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="fxCurve">FXCurve between numeraire currency and survival c</param>
    /// <param name="fxAtmVolatility">At the money forward FX volatility </param>
    /// <param name="fxCorrelation">Correlation between the forward FX (from numeraire currency to quote currency) and the gaussian transform of default times under quote currency forward measure.</param>
    /// <param name="fxDevaluation">Jump of forward FX at nth default time</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">List of reset rates</param>
    /// <returns>Constructed generalised semi-analytic NTD pricer</returns>
    ///
    static public FTDPricer
    NTDPricerSemiAnalytic(
      FTD ntd,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CorrelationObject correlation,
      FxCurve fxCurve,
      VolatilityCurve fxAtmVolatility,
      double fxCorrelation,
      double fxDevaluation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double notional,
      params List<RateReset>[] rateResets
      )
    {
      var ccy = discountCurve.Ccy;
      var quanto = false;
      foreach (var s in survivalCurves)
      {
        if (ccy != s.Ccy)
        {
          quanto = true;
          break;
        }
      }
      if (!quanto)
        return NTDPricerSemiAnalytic(new[] {ntd}, portfolioStart, asOfDate, settleDate, discountCurve, referenceCurve,
                                     survivalCurves, principals, new Copula(), correlation, stepSize, stepUnit,
                                     quadraturePoints, new[] {notional}, rateResets)[0];
      var survCcy = Currency.None;
      foreach (var s in survivalCurves)
      {
        if (survCcy == Currency.None)
          survCcy = s.Ccy;
        else
        {
          if (s.Ccy != survCcy)
            throw new ArgumentException(
              String.Format("All underlying survivalCurves should be denominated in the same currency {0}", survCcy));
        }
      }
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);
      SemiAnalyticBasketForNtdPricerQuanto basket;
      if (correlation is BaseCorrelationObject)
      {
        basket = new SemiAnalyticBasketForNtdPricerQuanto(
          asOfDate, settleDate, ntd.Maturity, discountCurve, sc, rc, prins,
          DefaultFactorCorrelation(sc), fxCurve, fxAtmVolatility,
          fxCorrelation, fxDevaluation, ntd.RecoveryCcy, ntd.FxAtInception,
          stepSize, stepUnit);
      }
      else
      {
        FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation((Correlation) correlation, picks);
        basket = new SemiAnalyticBasketForNtdPricerQuanto(
          asOfDate, settleDate, ntd.Maturity, discountCurve, sc, rc, prins,
          corr, fxCurve, fxAtmVolatility,
          fxCorrelation, fxDevaluation, ntd.RecoveryCcy, ntd.FxAtInception,
          stepSize, stepUnit);
      }
      if (quadraturePoints > 0)
        basket.IntegrationPointsFirst = quadraturePoints;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;
      return new FTDPricer(ntd, basket, discountCurve, referenceCurve, notional,
        (rateResets != null && rateResets.Length > 0) ? rateResets[0] : null);
    }


    /// <summary>
    ///   Creates a pricer for NTD using the generalised semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <param name="ntd">Nth to default product or array of NTDs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">List of reset rates</param>
    /// <returns>Constructed generalised semi-analytic NTD pricer</returns>
    ///
    static public FTDPricer[]
    NTDPricerSemiAnalytic(
      FTD[] ntd,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      params List<RateReset>[] rateResets
      )
    {
      return NTDPricerSemiAnalytic(
        ntd,
        portfolioStart,
        asOfDate,
        settleDate,
        discountCurve,
        null,
        survivalCurves,
        principals,
        copula,
        correlation,
        stepSize,
        stepUnit,
        quadraturePoints,
        notional,
        rateResets);

    }

    /// <summary>
    ///   Creates a pricer for NTD using the generalised semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <param name="ntd">Nth to default product or array of NTDs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">List of reset rates</param>
    /// <returns>Constructed generalised semi-analytic NTD pricer</returns>
    ///
    static public FTDPricer[]
    NTDPricerSemiAnalytic(
      FTD[] ntd,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double[] notional,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(ntd, notional, rateResets);

      if (ntd.Length < 1)
        throw new ArgumentException("Must specify at least one NTD");
      if (notional != null && notional.Length > 1 && notional.Length != ntd.Length)
        throw new ArgumentException(String.Format("Number of notionals {0} must be 1 or match number of NTDs {1}", notional.Length, ntd.Length));

      // Find the latest maturity from NTD array
      Dt maturityDate = ProductUtil.LastMaturity(ntd);

      // Create the basket
      BasketForNtdPricer basket = SemiAnalyticBasketForNtdPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, quadraturePoints);
      int basketSize = basket.Count;
      double principal = basket.TotalPrincipal / basketSize;

      // Create NTD Pricers
      FTDPricer[] pricer = new FTDPricer[ntd.Length];
      for (int i = 0; i < ntd.Length; i++)
        if ((ntd[i] != null) &&
          ((notional == null) || (notional.Length == 0) ||
          (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          pricer[i] = new FTDHeterogeneousPricer(ntd[i], basket, discountCurve, referenceCurve);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? (principal * ntd[i].NumberCovered)
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].RateResets = (rateResets == null || rateResets.Length == 0) ? null : rateResets[i]; 
          pricer[i].Validate();
        }

      // done
      return pricer;
    }

    
    /// <summary>
    ///   Creates a pricer for NTD using the Monte Carlo basket
    ///   model.
    /// </summary>
    ///
    /// <param name="ntd">Nth to default product or array of NTDs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
    /// <param name="rateResets">List of reset rates</param>
    /// <returns>Constructed generalised semi-analytic NTD pricer</returns>
    ///
    static public FTDPricer[]
    NTDPricerMonteCarlo(
      FTD[] ntd,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      double[] notional,
      int seed,
      params List<RateReset>[] rateResets
      )
    {
      return NTDPricerMonteCarlo(ntd, portfolioStart, asOfDate, settleDate, discountCurve, null, survivalCurves,
                                 principals, copula, correlation, stepSize, stepUnit, sampleSize, notional, seed,
                                 rateResets);

    }



    /// <summary>
    ///   Creates a pricer for NTD using the Monte Carlo basket
    ///   model.
    /// </summary>
    ///
    /// <param name="ntd">Nth to default product or array of NTDs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
    /// <param name="rateResets">List of reset rates</param>
    /// <returns>Constructed generalised semi-analytic NTD pricer</returns>
    ///
    static public FTDPricer[]
    NTDPricerMonteCarlo(
      FTD[] ntd,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      double[] notional,
      int seed,
      params List<RateReset>[] rateResets
      )
    {
      // Sanity check
      if (ntd.Length < 1)
        throw new ArgumentException("Must specify at least one NTD");
      if (notional != null && notional.Length > 1 && notional.Length != ntd.Length)
        throw new ArgumentException(String.Format("Number of notionals {0} must be 1 or match number of NTDs {1}", notional.Length, ntd.Length));

      // Find the latest maturity from NTD array
      Dt maturityDate = ProductUtil.LastMaturity(ntd);

      // Create basket pricer
      BasketForNtdPricer basket = MonteCarloBasketForNtdPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        copula, correlation, stepSize, stepUnit, sampleSize, seed);

      // Create Synthetic CDO Pricers
      int basketSize = basket.Count;
      double principal = basket.TotalPrincipal / basketSize;
      FTDPricer[] pricer = new FTDPricer[ntd.Length];
      for (int i = 0; i < ntd.Length; i++)
        if ((ntd[i] != null) &&
          ((notional == null) || (notional.Length == 0) ||
          (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          pricer[i] = new FTDMonteCarloPricer(ntd[i], basket, discountCurve, referenceCurve);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? principal
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].RateResets = (rateResets == null || rateResets.Length == 0) ? null : rateResets[i]; 
          pricer[i].Validate();
        }

      // done
      return pricer;
    }

    /// <summary>
    ///   Creates a pricer for NTD using the Monte Carlo basket
    ///   model.
    /// </summary>
    ///
    /// <param name="ntd">Nth to default product or array of NTDs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
    /// <returns>Constructed generalised semi-analytic NTD pricer</returns>
    ///
    static public FTDPricer[]
    NTDPricerMonteCarlo(
      FTD[] ntd,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      double[] notional,
      int seed)
    {
      return NTDPricerMonteCarlo(ntd, portfolioStart, asOfDate, settleDate, discountCurve, survivalCurves,
        principals, copula, correlation, stepSize, stepUnit, sampleSize, notional, seed, null);
    }
  
    /// <summary>
    ///   Creates a pricer for Synthetic CDO Squared using the generalised semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SemiAnalyticCDO2BasketPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo2">Synthetic CDO2 product or array of CDO2s sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="attachments">Attachment points of child CDOs</param>
    /// <param name="detachments">Detachment points of child CDOs</param>
    /// <param name="cdoMaturities">Same or different underlying CDO maturities</param>
    /// <param name="crossSub">If true, with cross subordination</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalised semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDO2PricerSemiAnalytic(
      SyntheticCDO[] cdo2,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[,] principals,
      double[] attachments,
      double[] detachments,
      Dt[] cdoMaturities,
      bool crossSub,
      Copula copula,
      object correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      int quadraturePoints,
      double[] notional,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo2, notional, rateResets);

      // Find the latest maturity from CDO2 array
      Dt maturityDate = ProductUtil.LastMaturity(cdo2);
      if(cdoMaturities == null || cdoMaturities.Length == 0)
      {
        cdoMaturities = new Dt[attachments.Length];
        for(int i = 0; i < cdoMaturities.Length; i++)
        {
          cdoMaturities[i] = maturityDate;
        }
      }

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo2);

      // Create a basket pricer
      CDOSquaredBasketPricer basketPricer = SemiAnalyticCDO2BasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, attachments, detachments, cdoMaturities, crossSub,
        copula, correlation, discountCurve, stepSize, stepUnit, lossLevels,
        sampleSize, quadraturePoints, 0);
      if (basketPricer is BaseCorrelationCDO2BasketPricer)
        ((BaseCorrelationCDO2BasketPricer)basketPricer).BaseCDOTerm = (SyntheticCDO)cdo2[0].Clone();


      // Create Synthetic CDO Pricers
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo2.Length];
      for (int i = 0; i < cdo2.Length; i++)
        if ((cdo2[i] != null) &&
          ((notional == null) || (notional.Length == 0) ||
          (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          pricer[i] = new SyntheticCDOPricer(cdo2[i], basketPricer, discountCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basketPricer.TotalPrincipal * cdo2[i].TrancheWidth
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }

    /// <summary>
    ///   Creates pricers for CDO of ABS using the generalized semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <param name="abscdo">CDO of ABS product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="cashflowStreams">ABS cashflows</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    static public ABSCDOPricer[] ABSCDOPricerSemiAnalytic(
      ABSCDO[] abscdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      CashflowStream[] cashflowStreams,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(abscdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(abscdo);

      // Create pricing grid
      ArrayList dates = new ArrayList();
      Dt date = settleDate;
      while (true)
      {
        dates.Add(date);
        date = Dt.Add(date, stepSize, stepUnit);
        if (Dt.Cmp(date, maturityDate) >= 0)
        {
          dates.Add(maturityDate);
          break;
        }
      }

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(abscdo);

      // Create a basket pricer
      ABSBasketPricer basket = ABSBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        cashflowStreams, copula, correlation, (Dt[])dates.ToArray(typeof(Dt)),
        lossLevels, quadraturePoints, gridSize);
      basket.StepSize = stepSize;
      basket.StepUnit = stepUnit;

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      ABSCDOPricer[] pricer = new ABSCDOPricer[abscdo.Length];
      for (int i = 0; i < abscdo.Length; i++)
        if ((abscdo[i] != null) &&
          ((notional == null) || (notional.Length == 0) ||
          (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
          {
            basketPricer = new BaseCorrelationBasketPricer(
                basket, discountCurve, (BaseCorrelationObject)correlation,
                rescaleStrikes, abscdo[i].Attachment, abscdo[i].Detachment);
          }
          else
            basketPricer = basket;

          // make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new ABSCDOPricer(abscdo[i], basketPricer, discountCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basketPricer.TotalPrincipal * (abscdo[i].TrancheWidth)
                                 : notional[i < notional.Length ? i : 0];
        }

      // done
      return pricer;
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the Hull-White dymamic basket model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method provides a convenient wrapper for the
    ///   <see cref="SyntheticCDOHomogeneousPricer">CDO Homogeneous Pricer</see>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="parameters">Array of parameters</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed large-pool Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[] CDOPricerHWDynamic(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      double[] parameters,
      int stepSize,
      TimeUnit stepUnit,
      double[] notional,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = LossLevelsFromTranches(cdo);

      // Create a basket pricer
      HWDynamicBasketPricer basket = HWDynamicBasketPricer(
        portfolioStart, asOfDate, settleDate, maturityDate, survivalCurves, principals,
        parameters, stepSize, stepUnit, lossLevels);

      // Create Synthetic CDO Pricers
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
        if ((cdo[i] != null) &&
          ((notional == null) || (notional.Length == 0) ||
          (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          pricer[i] = new SyntheticCDOPricer(cdo[i], basket, discountCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                 ? basket.TotalPrincipal * (cdo[i].TrancheWidth)
                                 : notional[i < notional.Length ? i : 0];
          pricer[i].UpdateStates();
        }

      // done
      return pricer;
    }

    /// <summary>
    ///   Creates a basket based on the Hull-White dymamic model of portfolio risk.
    /// </summary>
    ///
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="parameters">Array of parameters</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    ///
    /// <returns>Constructed homogeneous basket pricer</returns>
    static public HWDynamicBasketPricer
    HWDynamicBasketPricer(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      double[] parameters,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels
      )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);

      HWDynamicBasketPricer basket = new HWDynamicBasketPricer(asOfDate, settleDate, maturityDate,
        sc, rc, prins, parameters, stepSize, stepUnit, lossLevels);
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;
      return basket;
    }

    #endregion // New_Functions

    #region Old_Functions
    /// <summary>
		///   Analytical pricing model for synthetic CDO products associated
    ///   with uniform baskets.
		/// </summary>
		///
		/// <remarks>
		///   <para>UniformBasketPricer assumes that individual names have the same notionals, the same
		///   deterministic recovery rates, the same survival curves, and the same correlation factors.
		///   Under these assumptions, this model computes the exact loss distributions.
		///   It achieves the same speed as the so-called "large pool model"
		///   but it works with small basket also.</para>
    /// </remarks>
    ///
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="maturityDate">Maturity date for the basket</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name (may contain nulls which are ignored)</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula object</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
 		/// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
		///
		/// <returns>Constructed uniform basket pricer</returns>
		///
		static public UniformBasketPricer
		UniformBasketPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      double [,] lossLevels,
      int quadraturePoints
      )
		{
      return UniformBasketPricer( Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, lossLevels, quadraturePoints);
    }


    /// <summary>
    ///   Create Homogeneous basket pricer
		/// </summary>
		///
    /// <param name="asOfDate">As-of date for pricing</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="maturityDate">Maturity date for the basket</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula object</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
 		/// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
		///
		/// <returns>Constructed homogeneous basket pricer</returns>
		///
		static public HomogeneousBasketPricer
		HomogeneousBasketPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      double [,] lossLevels,
      int quadraturePoints
      )
		{
      return HomogeneousBasketPricer(Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, lossLevels, quadraturePoints);
    }


    /// <summary>
    ///   Analytical pricing model for synthetic CDO products associated
    ///   with heterogeneous baskets.
    /// </summary>
    ///
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities (or 0 for default)</param>
    ///
    /// <returns>Constructed heterogeneous basket</returns>
    ///
    static public HeterogeneousBasketPricer
    HeterogeneousBasketPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      object correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels,
      int quadraturePoints,
      double gridSize
      )
    {
      return HeterogeneousBasketPricer(Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, lossLevels, quadraturePoints, gridSize);
    }


    /// <summary>
		///   Create a Monte Carlo basket pricer.
		/// </summary>
		///
    /// <param name="asOfDate">As-of date for pricing</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="maturityDate">Maturity date for the basket</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <param name="lossLevels">Levels for constructing the loss distribution (or 0 for default)</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		/// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
		///
		/// <returns>Constructed Monte Carlo basket pricer.</returns>
		///
    static public MonteCarloBasketPricer
		MonteCarloBasketPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      double [,] lossLevels,
      int sampleSize,
      int seed
      )
		{
      return MonteCarloBasketPricer(Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, lossLevels, sampleSize, seed);
    }


    /// <summary>
		///   Create a Monte Carlo basket pricer for pricing NTDs.
		/// </summary>
		///
    /// <param name="asOfDate">As-of date for pricing</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="maturityDate">Maturity date for the basket</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		/// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
		///
		/// <returns>Constructed Monte Carlo basket pricer.</returns>
		///
    static public BasketForNtdPricer
		MonteCarloBasketForNtdPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      int seed
      )
		{
      return MonteCarloBasketForNtdPricer(Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, sampleSize, seed);
    }


    /// <summary>
		///   Create a Semi-Analytic basket pricer for pricing NTDs.
		/// </summary>
		///
    /// <param name="asOfDate">As-of date for pricing</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="maturityDate">Maturity date for the basket</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
 		/// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
		///
		/// <returns>Constructed Monte Carlo basket pricer.</returns>
		///
    static public BasketForNtdPricer
    SemiAnalyticBasketForNtdPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints
      )
		{
      return SemiAnalyticBasketForNtdPricer(Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, quadraturePoints);
    }


    /// <summary>
		///   Create a Forward Loss Model basket pricer.
		/// </summary>
		/// <remarks>
		///   <para>This model employs HJM-type forward loss rates model as its basic approach to
		///   pricing <see cref="SyntheticCDO">Synthetic CDO</see>.</para>
		/// </remarks>
    /// <param name="asOfDate">As-of date for pricing</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="maturityDate">Maturity date for the basket</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="indexNotes">Array of index notes by tenors</param>
		/// <param name="indexSpreads">Array of quoted index spreads</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="stateIndices">Array of segment indices</param>
		/// <param name="transitionCoefs">Probability distribution transition coefs</param>
		/// <param name="scalingFactors">Scaling factors for transition rates</param>
		/// <param name="baseLevels">Base levels for transition rates</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <returns>Constructed Forward Loss Model basket pricer.</returns>
    static public ForwardLossBasketPricer
		ForwardLossBasketPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [] principals,
			CDX [] indexNotes,
			double [] indexSpreads,
			DiscountCurve discountCurve,
      int [] stateIndices,
      double [,] transitionCoefs,
      double [] scalingFactors,
      double [] baseLevels,
      int stepSize,
      TimeUnit stepUnit
      )
		{
      return ForwardLossBasketPricer(Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, indexNotes, indexSpreads, discountCurve, stateIndices,
        transitionCoefs, scalingFactors, baseLevels,
        stepSize, stepUnit);
    }


    /// <summary>
    ///   Semi-Analytical pricing model for CDO Squared tranche.
    /// </summary>
    ///
    /// <param name="asOfDate">As-of date for pricing</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="maturityDate">Maturity date for the basket</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="attachments">Attachment points of child CDOs</param>
    /// <param name="detachments">Detachment points of child CDOs</param>
    /// <param name="crossSub">If true, with cross subordination</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="discountCurve">Discount curve used to interpolate base correlation (can be null for non base correlation pricer</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels for constructing the loss distribution</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (defaults to reasonable guess if zero)</param>
    /// <param name="gridSize">The grid used to update probabilities (or 0 for default)</param>
    ///
    /// <returns>Constructed Semi-Analytic CDO2 basket</returns>
    ///
    static public CDOSquaredBasketPricer
    SemiAnalyticCDO2BasketPricer(
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve [] survivalCurves,
      double [,] principals,
      double [] attachments,
      double [] detachments,
      bool crossSub,
      Copula copula,
      object correlation,
			DiscountCurve discountCurve,
      int stepSize,
      TimeUnit stepUnit,
      double [,] lossLevels,
      int sampleSize,
      int quadraturePoints,
      double gridSize
      )
    {
      return SemiAnalyticCDO2BasketPricer(Dt.Empty, asOfDate, settleDate, maturityDate,
        survivalCurves, principals, attachments, detachments, null, crossSub, copula, correlation,
        discountCurve, stepSize, stepUnit, lossLevels, sampleSize, quadraturePoints, gridSize);
    }


    /// <summary>
		///   Creates a pricer for Synthetic CDO using the large-pool basket
		///   model.
		/// </summary>
		///
		/// <remarks>
		///   <para>This method provides a convenient wrapper for the
		///   <see cref="SyntheticCDOHomogeneousPricer">CDO Homogeneous Pricer</see>.</para>
    /// </remarks>
		///
		/// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula object</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
 		/// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
		/// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
		/// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
		///
		/// <returns>Constructed large-pool Synthetic CDO pricer</returns>
		///
		static public SyntheticCDOPricer []
		CDOPricerLargePool(
      SyntheticCDO [] cdo,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double [] notional,
      bool rescaleStrikes
      )
		{
      return CDOPricerLargePool(cdo, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, quadraturePoints, notional, rescaleStrikes);
		}


    /// <summary>
		///   Creates a pricer for Synthetic CDO using the semi-analytic homogeneous basket
		///   model.
		/// </summary>
		///
		/// <remarks>
		///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHomogeneousPricer"/>.</para>
		/// </remarks>
		///
		/// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula object</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
 		/// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
		/// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
		/// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
		///
		/// <returns>Constructed homogeneous semi-analytic Synthetic CDO pricer</returns>
		///
		static public SyntheticCDOPricer []
		CDOPricerHomogeneous(
      SyntheticCDO [] cdo,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double [] notional,
      bool rescaleStrikes
      )
		{
      return CDOPricerHomogeneous(cdo, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, quadraturePoints, notional, rescaleStrikes);
    }


    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalised semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    ///
    /// <returns>Constructed generalised semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer[]
    CDOPricerHeterogeneous(
      SyntheticCDO[] cdo,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes
      )
    {
      return CDOPricerHeterogeneous(cdo, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, quadraturePoints, gridSize, notional, rescaleStrikes);
    }


    /// <summary>
		///   Creates a pricer for Synthetic CDO using the Monte Carlo models.
		///   model.
		/// </summary>
		///
		/// <remarks>
		///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOMonteCarloPricer"/>.</para>
    /// </remarks>
		///
		/// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula object</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		/// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
		/// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
		/// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
		///
		/// <returns>Constructed Monte Carlo Synthetic CDO pricer</returns>
		///
		static public SyntheticCDOPricer []
		CDOPricerMonteCarlo(
      SyntheticCDO [] cdo,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      double [] notional,
      bool rescaleStrikes,
      int seed
      )
		{
      return CDOPricerMonteCarlo(cdo, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, sampleSize, notional, rescaleStrikes, seed);
    }


    /// <summary>
		///   Creates a pricer for Synthetic CDO using the Forward Loss Model.
		/// </summary>
		///
		/// <remarks>
		///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOForwardLossModelPricer"/>.</para>
    /// </remarks>
		///
		/// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="indexNotes">Array of index notes by tenors</param>
		/// <param name="indexSpreads">Array of quoted index spreads</param>
		/// <param name="stateIndices">Array of segment indices</param>
		/// <param name="transitionCoefs">Probability distribution transition coefs</param>
		/// <param name="scalingFactors">Scaling factors for transition rates</param>
		/// <param name="baseLevels">Base levels for transition rates</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
		///
		/// <returns>Constructed Forward Loss Model pricer</returns>
		///
		static public SyntheticCDOPricer []
		CDOPricerForwardLoss(
      SyntheticCDO [] cdo,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double [] principals,
			CDX [] indexNotes,
			double [] indexSpreads,
      int [] stateIndices,
      double [,] transitionCoefs,
      double [] scalingFactors,
      double [] baseLevels,
      int stepSize,
      TimeUnit stepUnit,
      double [] notional
      )
		{
      return CDOPricerForwardLoss(cdo, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, indexNotes, indexSpreads, stateIndices,
        transitionCoefs, scalingFactors, baseLevels, stepSize, stepUnit, notional);
    }


    /// <summary>
		///   Creates a pricer for NTD using the generalised semi-analytic basket
		///   model.
		/// </summary>
		///
		/// <param name="ntd">Nth to default product or array of NTDs sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula object</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
 		/// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
		/// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
		/// <param name="rateResets">List of reset rates</param>
		/// <returns>Constructed generalised semi-analytic NTD pricer</returns>
		///
		static public FTDPricer []
		NTDPricerSemiAnalytic(
      FTD [] ntd,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double [] notional,
      params List<RateReset>[] rateResets
      )
		{
      return NTDPricerSemiAnalytic(ntd, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, quadraturePoints, notional, rateResets);
    }


    /// <summary>
		///   Creates a pricer for NTD using the Monte Carlo basket
		///   model.
		/// </summary>
		///
		/// <param name="ntd">Nth to default product or array of NTDs sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
		/// <param name="copula">Copula object</param>
		/// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		/// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
		/// <param name="seed">Seed for Monte Carlo (0 = default seed; -1 = random seed)</param>
		/// <param name="rateResets">List of reset rates</param>
		/// <returns>Constructed generalised semi-analytic NTD pricer</returns>
		///
		static public FTDPricer []
		NTDPricerMonteCarlo(
      FTD [] ntd,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double [] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      double [] notional,
      int seed,
      params List<RateReset>[] rateResets
      )
    {
      return NTDPricerMonteCarlo(ntd, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, copula, correlation,
        stepSize, stepUnit, sampleSize, notional, seed, rateResets);
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO Squared using the generalised semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SemiAnalyticCDO2BasketPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo2">Synthetic CDO2 product or array of CDO2s sharing same asset pool</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="attachments">Attachment points of child CDOs</param>
    /// <param name="detachments">Detachment points of child CDOs</param>
    /// <param name="crossSub">If true, with cross subordination</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="notional">Notional amount for product(s) (default is tranche size)</param>
    ///
    /// <returns>Constructed generalised semi-analytic Synthetic CDO pricer</returns>
    ///
    static public SyntheticCDOPricer []
    CDO2PricerSemiAnalytic(
      SyntheticCDO [] cdo2,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double [,] principals,
      double [] attachments,
      double [] detachments,
      bool crossSub,
      Copula copula,
      object correlation,
      int stepSize,
      TimeUnit stepUnit,
      int sampleSize,
      int quadraturePoints,
      double [] notional
      )
    {
      return CDO2PricerSemiAnalytic(cdo2, Dt.Empty, asOfDate, settleDate,
        discountCurve, survivalCurves, principals, attachments, detachments, null,
        crossSub, copula, correlation,
        stepSize, stepUnit, sampleSize, quadraturePoints, notional);
    }

    #endregion // Old_Functions

    #region Utilities
    //
		// Other utilities
		//

		/// <summary>Return loss levels from tranches</summary>
		/// <exclude />
		static public double [,]
		LossLevelsFromTranches( SyntheticCDO [] cdo )
		{
		  // Sanity check
		  if( null == cdo || cdo.Length < 1 )
				throw new ArgumentException( "cdo cannot be null or empty" );
			int count = cdo.Length;
			double [,] lossLevels = new double [count, 2];
			for( int i = 0, c = 0; (i < cdo.Length) && (c < count); i++ )
				if( cdo[i] != null )
				{
					lossLevels[c, 0] = cdo[i].Attachment;
					lossLevels[c, 1] = cdo[i].Detachment;
					c++;
				}

			return lossLevels;
		}


		/// <summary>Get the default number of quadrature points</summary>
		/// <exclude />
    static public int
    DefaultQuadraturePoints( Copula copula, int basketSize )
    {
      int points = 25;
      switch (copula.CopulaType)
      {
        case CopulaType.Clayton:
          points = 200;
          break;
        case CopulaType.Gumbel:
        case CopulaType.Frank:
          points = 100;
          break;
        case CopulaType.Gauss:
        case CopulaType.ExtendedGauss:
        case CopulaType.NormalInverseGaussian:
        case CopulaType.RandomFactorLoading:
          if (basketSize < 40)
            points = 25;
          else
            points = 25 + (basketSize - 40) / 10;
          break;
        case CopulaType.DoubleT:
          if (basketSize < 40)
            points = 15;
          else
            points = 15 + (basketSize - 40) / 10;
          break;
        case CopulaType.StudentT:
          if (basketSize < 40)
            points = 12;
          else
            points = 12 + (basketSize - 40) / 10;
          break;
        case CopulaType.Poisson:
          break;
        default:
          throw new ToolkitException("Unknown copula type");
      }

      return points;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsAdjust(
      double attachment, double detachment)
    {
      int adjust = (int)(30.0 - 500 * Math.Abs(attachment - 0.09)
        - 100 * (detachment - attachment));
      return adjust > 0 ? adjust : 0;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsAdjust(
      CopulaType copulaType, SyntheticCDO[] cdos)
    {
      int adjust = 0;
      switch (copulaType)
      {
        case CopulaType.Clayton:
        case CopulaType.Gumbel:
        case CopulaType.Frank:
          break;
        default:
          foreach (SyntheticCDO cdo in cdos)
            if (cdo != null)
            {
              int a = DefaultQuadraturePointsAdjust(
                cdo.Attachment, cdo.Detachment);
              if (a > adjust)
                adjust = a;
            }
          break;
      }
      return adjust;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsCorrAdjust(
      int origPoints, CopulaType copulaType, double corr)
    {
      int points = 0;
      switch (copulaType)
      {
        case CopulaType.Clayton:
        case CopulaType.Gumbel:
        case CopulaType.Frank:
        case CopulaType.DoubleT:
        case CopulaType.StudentT:
          break;
        default:
          points = (int)(550 * corr) - 295;
          break;
      }
      return points < origPoints ? origPoints : points;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    SafeQuadraturePointsForGreeks(int origPoints, CopulaType copulaType, bool forGamma)
    {
      int points = 0;
      switch (copulaType)
      {
        case CopulaType.Clayton:
        case CopulaType.Gumbel:
        case CopulaType.Frank:
        case CopulaType.DoubleT:
        case CopulaType.StudentT:
          break;
        default:
          points = forGamma ? 200 : 100;
          break;
      }
      return points < origPoints ? origPoints : points;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsAdjust(double[] dps)
    {
      int adjust = 0;
      double ap = 0;
      foreach (double dp in dps)
        if (dp > 0 && dp <= 1)
        {
          int a = DefaultQuadraturePointsAdjust(ap, dp);
          if (a > adjust)
            adjust = a;
          ap = dp;
        }
      return adjust;
    }

    /// <summary>Create a default correlation object</summary>
		/// <exclude />
		static public SingleFactorCorrelation
		DefaultSingleFactorCorrelation( SurvivalCurve [] survivalCurves )
		{
		  string [] names = new string[ survivalCurves.Length ];
			for( int i = 0; i < survivalCurves.Length; ++i )
				names[i] = survivalCurves[i].Name;
			return new SingleFactorCorrelation( names, 0.0 );
		}

		/// <summary>Create a default correlation object</summary>
		/// <exclude />
		static public FactorCorrelation
		DefaultFactorCorrelation( SurvivalCurve [] survivalCurves )
		{
		  SingleFactorCorrelation corr = DefaultSingleFactorCorrelation( survivalCurves );
			return CorrelationFactory.CreateFactorCorrelation( corr );
		}

		/// <summary>Create a default pairwise correlation object</summary>
		/// <exclude />
		static public GeneralCorrelation
		DefaultGeneralCorrelation( SurvivalCurve [] survivalCurves )
		{
		  string [] names = new string[ survivalCurves.Length ];
			for( int i = 0; i < survivalCurves.Length; ++i )
				names[i] = survivalCurves[i].Name;
			return new GeneralCorrelation( names, 0.0 );
		}


		/// <summary>Calculate average recovery rate</summary>
		/// <exclude />
		static public double
    AverageRecoveryRate( RecoveryCurve [] recoveryCurves, Dt maturity )
		{
      double sum = 0;
			for (int i = 0; i < recoveryCurves.Length; ++i)
				sum += recoveryCurves[i].RecoveryRate(maturity);
			return ( sum / recoveryCurves.Length );
		}


    /// <summary>
    ///  Set up list of survival curves/etc. ignoring empty or zero notionals.
    ///  <preliminary/>
    /// </summary>
    /// <param name="survivalCurves"></param>
    /// <param name="principals"></param>
    /// <param name="sc"></param>
    /// <param name="rc"></param>
    /// <param name="prins"></param>
    /// <param name="picks"></param>
    /// <exclude />
		static public void SetupArgs(
      SurvivalCurve [] survivalCurves,
      double [] principals,
      out SurvivalCurve [] sc,
      out RecoveryCurve [] rc,
      out double [] prins,
      out double [] picks
      )
		{
			// Argument validation
			if( principals != null && principals.Length > 1 && principals.Length != survivalCurves.Length )
				throw new ArgumentException("Number of principals must match number of survival curves");

			// Set up number of curves we are interested in
			picks = new double[survivalCurves.Length];
			int nCurves = 0;
			for( int i = 0; i < survivalCurves.Length; i++ )
			{
				if( (survivalCurves[i] != null) && (principals == null || principals.Length <= 1 || principals[i] != 0.0) )
				{
					nCurves++;
					picks[i] = 1;
				}
				else
					picks[i] = 0;
			}

			// Set up arguments for basket pricer, ignoring notionals that are zero.
			prins = new double[nCurves];
			sc = new SurvivalCurve[nCurves];
			rc = new RecoveryCurve[nCurves];

			for( int i = 0, idx = 0; i < survivalCurves.Length; i++ )
			{
			  if( (survivalCurves[i] != null) && ((principals == null) || (principals.Length <= 1) || (principals[i] != 0.0)) )
				{
					prins[idx] = (principals == null || principals.Length == 0) ? 1000000 : ((principals.Length == 1) ? principals[0] : principals[i]);
					sc[idx] = survivalCurves[i];
					if( sc[idx].Calibrator != null )
						rc[idx] = sc[idx].SurvivalCalibrator.RecoveryCurve;
					else
						throw new ArgumentException( String.Format("Must specify recoveries as curve {0} does not have recoveries from calibration", sc[idx].Name) );
					idx++;
				}
			}
      Utils.ScaleUp(prins, 10.0);
			return;
    }

    /// <summary>
    ///   Add additional grid dates based on cdo effective dates
    /// </summary>
    /// <param name="basket">Basket pricer</param>
    /// <param name="cdos">cdos</param>
    /// <exclude/>
    public static void AddGridDates(
      BasketPricer basket,
      SyntheticCDO[] cdos
      )
    {
      Dt start = basket.PortfolioStart.IsEmpty() ? basket.Settle : basket.PortfolioStart;
      foreach (SyntheticCDO cdo in cdos)
        if (cdo != null && Dt.Cmp(cdo.Effective, start) > 0)
            basket.AddGridDates(cdo.Effective);
      return;
    }

    /// <summary>
    ///   Find the minimum amortization level for an array of tranches
    ///   <preliminary/>
    /// </summary>
    /// <param name="cdos">An array of tranches</param>
    /// <returns>Minimum level</returns>
    /// <exclude/>
    public static double MinimumAmortizationLevel(SyntheticCDO[] cdos)
    {
      double minLevel = 1.0;
      for (int i = 0; i < cdos.Length; ++i)
      {
        double level = MinimumAmortizationLevel(cdos[i]);
        if (minLevel > level)
          minLevel = level;
      }
      return minLevel;
    }

    /// <summary>
    ///   Find the minimum amortization level for a tranche
    ///   <preliminary/>
    /// </summary>
    /// <param name="cdo">Tranche</param>
    /// <returns>Minimum level</returns>
    /// <exclude/>
    public static double MinimumAmortizationLevel(SyntheticCDO cdo)
    {
      if (cdo == null || cdo.AmortizePremium == false)
        return 1.0;
      return 1 - cdo.Detachment;
    }

    /// <summary>
    ///   Determine the quadrature points or accuracy level
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    public static int GetQuadraturePoints(ref double accuracy)
    {
      int quadPoints = 0;
      if (accuracy <= 0)
        accuracy = defaultAccuracy_;
      else if (accuracy > 1)
      {
        quadPoints = (int)Math.Floor(accuracy + 1E-8);
        accuracy = 0;
      }
      return quadPoints;
    }

    #endregion Utilities

  } // class BasketPricerFactory
}
