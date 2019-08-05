//
// Compare various basket loss distribution results
// Copyright (c)    2002-2018. All rights reserved.
//

// Enable this test efficiency
//#define TEST_TIMING

using System;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture, Smoke]
  public class TestBasket
  {
    const double epsilon = 1.0e-4;
    const int nBasket = 100;
    const double principal = 10000000;
    const double recoveryRate = 0.4;
    const double corr = 0.3;
    const double hazardRate = 0.005;
    const int stepSize = 1;
    const TimeUnit stepUnit = TimeUnit.Months;
    const int simulationRuns = 100000;

    const int IntegrationPointsFirst = 25;
    const int IntegrationPointsSecond = 5;

    private static SurvivalCurve
    CreateSurvivalCurve( Dt asOfDate,
                         double hazardRate )
    {
      SurvivalCurve SurvCurve = new SurvivalCurve(asOfDate);
      SurvCurve.Add(asOfDate, 1.0);
      for (int i = 1; i <= 6; ++i)
        SurvCurve.Add(Dt.Add(asOfDate, i, TimeUnit.Years), 
                      Math.Exp(-i*hazardRate));
      SurvCurve.Add(Dt.Add(asOfDate, 10, TimeUnit.Years),
                    Math.Exp(-10*hazardRate));
      return SurvCurve;
    }

    private static DiscountCurve
    CreateDiscountCurve(Dt asOfDate, double discountRate)
    {
      return new DiscountCurve(asOfDate, discountRate);
    }

    // directt computation of uniform basket loss grids
    private static double[,]
    UniformBasketResult(Dt start, Dt end,
                        int nBasket,
                        Copula copula,
                        double corr,
                        double recoveryRate,
                        Curve sc,
                        double[] tranches )
    {
      int nTranches = tranches.Length;
      double[] points = new double[nTranches];
      for (int i = 0; i < nTranches; ++i) {
        double x = tranches[i] * nBasket / (1-recoveryRate);
        if (x > nBasket) x = nBasket;
        points[i] = x;
      }
  
      // find size of the output array
      int nrow = 0;
      Dt current = start;
      while (Dt.Cmp(current,end) < 0) {
        current = Dt.Add(current, 1, TimeUnit.Months);
        ++nrow;
      }

      // allocate array
      double[,] result = new double [nrow, nTranches];

      // compute cumulative loss
      int rowIdx = 0;
      current = start;
      while (Dt.Cmp(current,end) < 0) {
        current = Dt.Add(current, stepSize, stepUnit);
        if (Dt.Cmp(current,end) > 0)
          current = end;
        double loss = UniformBasketModel.Cumulative( false, start, current,
                                                     nBasket,
                                                     copula.CopulaType,
                                                     copula.DfCommon,
                                                     copula.DfIdiosyncratic,
                                                     Math.Sqrt(corr),
                                                     IntegrationPointsFirst,
                                                     IntegrationPointsSecond,
                                                     sc,
                                                     0, nBasket);
        loss *= 1 - recoveryRate;
        loss *= principal;
        result[rowIdx,0] = loss;

        for (int i = 0; i < nTranches - 1; ++i) {
          loss = UniformBasketModel.Cumulative(false, start, current,
                                               nBasket,
                                               copula.CopulaType,
                                               copula.DfCommon,
                                               copula.DfIdiosyncratic,
                                               Math.Sqrt(corr),
                                               IntegrationPointsFirst,
                                               IntegrationPointsSecond,
                                               sc,points[i], points[i+1]);
          loss *= 1 - recoveryRate;
          loss *= principal;
          result[rowIdx, 1+i] = loss;
        }

        ++ rowIdx;
      }
    
      return result;
    }

    private static BasketPricer
    CreateBasketPricer( string kind, Dt start, Dt end,
                        SurvivalCurve sc, Copula copula,
                        double [] tranches )
    {
      string [] names = new string[nBasket];
      for( int i = 0; i < nBasket; i++ )
        names[i] = String.Format("Name {0}", i+1);

      int nTranches = tranches.Length;
      double[,] lossLevels = new double[nTranches,1];
      for (int i = 0; i < nTranches; ++i)
        lossLevels[i,0] = tranches[i] ;

      SurvivalCurve[] survCurves = new SurvivalCurve[nBasket];
      for (int i = 0; i < nBasket; ++i)
        survCurves[i] = sc;

      RecoveryCurve[] recoveryCurves = new RecoveryCurve[nBasket];
      RecoveryCurve recoveryCurve = new RecoveryCurve(start, recoveryRate);
      for (int i = 0; i < nBasket; ++i)
        recoveryCurves[i] = recoveryCurve;

      double[] principals = new double[nBasket];
      for (int i = 0; i < nBasket; ++i)
        principals[i] = principal;

      if (string.Compare(kind, "UniformBasket") == 0)
      {
        SingleFactorCorrelation
          correlation = new SingleFactorCorrelation(names,0);
        correlation.SetFactor(Math.Sqrt(corr));

        UniformBasketPricer pricer
          = new UniformBasketPricer( start, start, end,
                                     survCurves, recoveryCurves, principals,
                                     copula, correlation,
                                     stepSize, stepUnit, lossLevels );
        return pricer;
      }
      else if (string.Compare(kind, "HomogeneousBasket") == 0)
      {
        FactorCorrelation correlation
          = new FactorCorrelation(names,1, new double[nBasket]);
        correlation.SetFactor(Math.Sqrt(corr));

        HomogeneousBasketPricer pricer
          = new HomogeneousBasketPricer( start, start, end,
          survCurves, recoveryCurves, principals, copula, correlation,
          stepSize, stepUnit, lossLevels );
        return pricer;
      }
      else if (string.Compare(kind, "HeterogeneousBasket") == 0)
      {
        FactorCorrelation correlation
          = new FactorCorrelation(names, 1, new double[nBasket]);
        correlation.SetFactor(Math.Sqrt(corr));

        HeterogeneousBasketPricer pricer
          = new HeterogeneousBasketPricer(start, start, end,
          survCurves, recoveryCurves, principals, copula, correlation,
          stepSize, stepUnit, lossLevels);
        return pricer;
      }
      else if (string.Compare(kind, "SemiAnalyticBasket") == 0)
      {
        FactorCorrelation correlation
          = new FactorCorrelation(names, 1, new double[nBasket]);
        correlation.SetFactor(Math.Sqrt(corr));

        SemiAnalyticBasketPricer pricer
          = new SemiAnalyticBasketPricer(start, start, end,
          survCurves, recoveryCurves, principals, copula, correlation,
          stepSize, stepUnit, lossLevels, false /*checkRefinance*/);
        return pricer;
      }
      else if (string.Compare(kind, "MonteCarloBasket") == 0)
      {
        GeneralCorrelation correlation
          = new GeneralCorrelation(names,new double[nBasket*nBasket]);
        correlation.SetCorrelation( corr );

        MonteCarloBasketPricer pricer
          = new MonteCarloBasketPricer( start, start, end,
          survCurves, recoveryCurves, principals, copula, correlation,
          stepSize, stepUnit, lossLevels, simulationRuns );
        return pricer;
      }

    // Should be error here!!!
    return null;
    }

    //
    // Test the values of cumulative losses at each time grid poins
    //
    private static void
    LossGrid( string kind, double tolerance )
    {
      double[] tranches = new double [10]{0, 0.03, 0.06, 0.09, 0.12, 0.15, 0.20, 0.30, 0.80, 1.0};
      int nTranches = tranches.Length;
      Dt start = Dt.Today();
      Dt end = Dt.Add(start, 5, TimeUnit.Years);

      SurvivalCurve sc = CreateSurvivalCurve(start, hazardRate);
      Copula copula = new Copula(CopulaType.Gauss,0,0);

      double[,] expects = UniformBasketResult(start, end, nBasket, copula, corr,
                                              recoveryRate, sc, tranches);

      BasketPricer pricer = CreateBasketPricer( kind, start, end, sc, copula, tranches );
      pricer.IntegrationPointsFirst = IntegrationPointsFirst;
      pricer.IntegrationPointsSecond = IntegrationPointsSecond;

      int rowIdx = 0;
      Dt current = start;
      if (string.Compare(kind, "MonteCarloBasket") != 0)
      {
        while (Dt.Cmp(current,end) < 0) {
          current = Dt.Add(current, stepSize, stepUnit);
          if (Dt.Cmp(current,end) > 0)
            current = end;

          double loss =
            pricer.AccumulatedLoss(current, 0.0, 1.0) * pricer.TotalPrincipal;
          Assert.AreEqual( expects[rowIdx,0],
                           loss,
                           tolerance,
                           String.Format("Loss at tranche 0~1, date {0}", rowIdx)
                           );
      
          for (int i = 0; i < nTranches - 1; ++i) {
            double tBegin = tranches[i] ;
            double tEnd = tranches[i+1] ;
            loss = pricer.AccumulatedLoss(current,tBegin,tEnd) * pricer.TotalPrincipal;
            Assert.AreEqual( expects[rowIdx,i+1],
                             loss,
                             tolerance,
                             String.Format("Loss at tranche {0}~{1}, date {2}",
                                           tranches[i], tranches[i+1], rowIdx)
                             );
          }

          ++ rowIdx;
        }
      }
      else
      {
        // compute standard errors
        double[] stderr = new double [nTranches]; 
        for (int i = 0; i < nTranches; ++i)
          stderr[i] = 0.0;

        while (Dt.Cmp(current,end) < 0) {
          current = Dt.Add(current, 1, TimeUnit.Months);
          if (Dt.Cmp(current,end) > 0)
            current = end;
 
          double loss = 
            pricer.AccumulatedLoss(current, 0.0, 1.0) * pricer.TotalPrincipal;
          Assert.AreEqual( expects[rowIdx,0],
                         loss,
                         0.05 * (expects[rowIdx,0]+1),
                         String.Format("Loss at tranche 0~1, date {0}", rowIdx)
                         );
          stderr[0] += Math.Abs(loss - expects[rowIdx,0])/(1+expects[rowIdx,0]);

    
          for (int i = 0; i < nTranches - 1; ++i) {
            loss =
              pricer.AccumulatedLoss( current, 
                                      tranches[i],
                                      tranches[i+1]) * pricer.TotalPrincipal;
            Assert.AreEqual( expects[rowIdx,i+1],
                             loss,
                             0.05 * (expects[rowIdx,0]+1),
                             String.Format("Loss at tranche {0}~{1}, date {2}",
                                           tranches[i], tranches[i+1], rowIdx)
                             );

            stderr[i+1] += Math.Abs(loss - expects[rowIdx,i+1])/(1+expects[rowIdx,0]);
          }

          ++ rowIdx;
        }

        // check standard errors
        stderr[0] /= rowIdx;
        Assert.AreEqual( 0.0,
                         stderr[0],
                         0.01,
                         String.Format("Std. Err. of losses at tranche 0~1")
                         );

        for (int i = 1; i < nTranches; ++i) {
          stderr[i] /= rowIdx;
          Assert.AreEqual( 0.0,
                           stderr[i],
                           0.01,
                           String.Format("Std. Err. of losses at tranche {0}~{1}",
                                         tranches[i-1], tranches[i])
                           );
        }
      }
      return;
    }

    /// <summary>
    ///   Test order independence
    /// </summary>
    private void OrderIndepedence(string basketType)
    {
      const double tolerance = epsilon;
      double[] tranches = new double[10] { 0, 0.03, 0.06, 0.09, 0.12, 0.15, 0.20, 0.30, 0.80, 1.0 };
      int nTranches = tranches.Length - 1;
      Dt start = Dt.Today();
      Dt end = Dt.Add(start, 5, TimeUnit.Years);
      SurvivalCurve sc = CreateSurvivalCurve(start, hazardRate);
      Copula copula = new Copula(CopulaType.Gauss, 0, 0);

      // create a basket pricer
      BasketPricer basket = CreateBasketPricer(basketType,
          start, end, sc, copula, tranches);
      basket.IntegrationPointsFirst = IntegrationPointsFirst;
      basket.IntegrationPointsSecond = IntegrationPointsSecond;
      basket.GridSize = 0.002;

      // create CDOs
      SyntheticCDO[] cdos = new SyntheticCDO[nTranches];
      for (int i = 0; i < nTranches; ++i)
      {
        SyntheticCDO cdo = new SyntheticCDO(start, end, Currency.USD, 0.0,
                                            DayCount.Actual360, Frequency.Quarterly,
                                            BDConvention.Following, Calendar.NYB);
        cdo.Attachment = tranches[i];
        cdo.Detachment = tranches[i + 1];
        cdos[i] = cdo;
      }

      // Create discount curve
      DiscountCurve discountCurve = CreateDiscountCurve(start, 0.04);

      // factor of heterogeneity
      double alpha = 0.5;

      // calculate protection pv in forward order
      double[] principals = basket.Principals;
      for (int i = 1; i <= nBasket; i++)
        principals[i - 1] = principal * (1 - alpha + (alpha * i) / nBasket);
      basket.Principals = principals; // force recalculation of total principals
      basket.Reset();

      double[] basePvs = new double[nTranches];
      for (int j = 0; j < nTranches; ++j)
      {
        // create a pricer
        SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdos[j], basket, discountCurve);
        basePvs[j] = pricer.ProtectionPv();
      }

      // calculate protection pv in backward order
      for (int i = 1; i <= nBasket; i++)
        principals[nBasket - i] = principal * (1 - alpha + (alpha * i) / nBasket);
      basket.Principals = principals; // force recalculation of total principals
      basket.Reset();

      for (int j = 0; j < nTranches; ++j)
      {
        // create a pricer
        SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdos[j], basket, discountCurve);
        double Pv = pricer.ProtectionPv();

        // calculate the relative error
        double error = (Pv - basePvs[j]) / (1 + basePvs[j]);

        Assert.AreEqual(0.0,
                         error,
                         tolerance,
                         String.Format("Reverse order difference {0} and {1} for tranche {2}~{3}",
                                       Pv, basePvs[j], cdos[j].Attachment, cdos[j].Detachment)
                         );
      }
    } // OrderIndependence

    //
    // Consistency between Uniform Model and Uniforn pricer
    //
    [Test, Smoke]
    public void UniformBasket()
    {
      LossGrid( "UniformBasket", 1.0e-7);
    } // UniformBasket()


    //
    // Consistency between Uniform and Homogeneous Basket models
    //
    [Test, Smoke]
    public void HomogeneousBasket()
    {
      LossGrid( "HomogeneousBasket", epsilon);
    } // HomogeneousBasket()


    //
    // Consistency between Uniform and Heterogeneous Basket models
    //
    [Test, Smoke]
    public void HeterogeneousBasket()
    {
      LossGrid( "HeterogeneousBasket", epsilon);
    } //HeterogeneousBasket()

    //
    // Consistency between Uniform and Heterogeneous Basket models
    //
    [Test, Smoke]
    public void SemiAnalyticBasket()
    {
      LossGrid("SemiAnalyticBasket", epsilon);
    } //HeterogeneousBasket()

    //
    // Consistency between Uniform and Monte Carlo Basket models
    //
    [Test, Smoke]
    public void MonteCarloBasket()
    {
      LossGrid( "MonteCarloBasket", epsilon);
    } //MonteCarloBasket()

    //
    // Order independence of heterogeneous basket pricer
    //
    [Test]
    public void OrderIndepedenceHeterogeneous()
    {
      OrderIndepedence("HeterogeneousBasket");
    }

    //
    // Order independence of semi-analytic basket pricer
    //
    [Test]
    public void OrderIndepedenceSemiAnalytic()
    {
      OrderIndepedence("SemiAnalyticBasket");
    }

    //
    // Consistency between a single FTD basket and FTD losses.
    // Condition:
    //   {Losses from FTDBasketModel} == {Losses from UniformBasketModel}
    //
    [Test, Smoke]
    public void
    FTDBasketTest1()
    {
      const double tolerance = epsilon;
      const int defaultFirst = 2;
      const int numCovered = 4;
      double[] tranches = new double [2];
      tranches[0] = (1 - recoveryRate) * (defaultFirst - 1) / nBasket ;
      tranches[1] = (1 - recoveryRate) * (defaultFirst - 1 + numCovered) / nBasket ;

      Dt start = Dt.Today();
      Dt end = Dt.Add(start, 5, TimeUnit.Years);

      SurvivalCurve sc = CreateSurvivalCurve(start, hazardRate);
      Copula copula = new Copula(CopulaType.Gauss,0,0);

      double[,] expects = UniformBasketResult(start, end, nBasket, copula, corr,
                                              recoveryRate, sc, tranches);

      tranches[0] = 0.0;
      tranches[1] = 1.0;
      int [] ftdIndices = new int[1];
      ftdIndices[0] = nBasket;
      FTD [] ftds = new FTD [1];
      ftds[0] = new FTD(start, end, Currency.USD, 0.0, DayCount.Actual360,
                        Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      ftds[0].First = defaultFirst;
      ftds[0].NumberCovered = numCovered;

      int nTranches = tranches.Length;
      double[,] lossLevels = new double[nTranches,1];
      for (int i = 0; i < nTranches; ++i)
        lossLevels[i,0] = tranches[i] ;

      RecoveryCurve recoveryCurve = new RecoveryCurve(start, recoveryRate);
      SurvivalCurve[] survCurves = new SurvivalCurve[nBasket];
      RecoveryCurve[] recoveryCurves = new RecoveryCurve[nBasket];
      double[] principals = new double[nBasket];
      for (int i = 0; i < nBasket; ++i)
      {
        survCurves[i] = sc;
        principals[i] = principal;
        recoveryCurves[i] = recoveryCurve;
      }

      string [] names = new string[nBasket];
      for( int i = 0; i < nBasket; i++ )
        names[i] = String.Format("Name {0}", i+1);


      FactorCorrelation correlation
        = new FactorCorrelation(names,1, new double[nBasket]);
      correlation.SetFactor(Math.Sqrt(corr));

      BasketPricer pricer
        = new FTDBasketPricer( start, start, end,
                               survCurves, recoveryCurves,
                               ftds, ftdIndices, principals,
                               copula, correlation,
                               stepSize, stepUnit, lossLevels );
      pricer.IntegrationPointsFirst = IntegrationPointsFirst;
      pricer.IntegrationPointsSecond = IntegrationPointsSecond;

      double totalPrincipal = pricer.TotalPrincipal;

      int rowIdx = 0;
      Dt current = start;
      while (Dt.Cmp(current,end) < 0) {
        current = Dt.Add(current, stepSize, stepUnit);
        if (Dt.Cmp(current,end) > 0)
          current = end;

        double loss = pricer.AccumulatedLoss(current, 0.0, 1.0) * totalPrincipal;
        Assert.AreEqual( expects[rowIdx,1],
                         loss,
                         tolerance,
                         String.Format("Loss at date {0}", rowIdx)
                         );
      
        ++ rowIdx;
      }
      return;
    }


    //
    // Consistency between a multiple FTD basket and FTD losses
    // Condition:
    //   { Losses of the whole FTD }    { Sum of the losses of all   }
    //   { basket as computed from } == { the child FTDs as computed }
    //   { the FTDBasketModel      }    { from the UniformBasketModel}
    //
    [Test, Smoke]
    public void
    FTDBasketTest2()
    {
      const double tolerance = epsilon;
      const int nFTD = 4;
      const int defaultFirst = 2;
      const int numCovered = 4;
      double[] tranches = new double [2];
      tranches[0] = (1 - recoveryRate) * (defaultFirst - 1) / nBasket * nFTD;
      tranches[1] = (1 - recoveryRate) * (defaultFirst - 1 + numCovered) / nBasket * nFTD;

      Dt start = Dt.Today();
      Dt end = Dt.Add(start, 5, TimeUnit.Years);

      SurvivalCurve sc = CreateSurvivalCurve(start, hazardRate);
      Copula copula = new Copula(CopulaType.Gauss,0,0);

      double[,] expects = UniformBasketResult(start, end, nBasket/nFTD, copula, corr,
                                              recoveryRate, sc, tranches);
      for (int i = 1; i < nFTD; ++i) {
        double[,] tmp = UniformBasketResult(start, end, nBasket/nFTD, copula, corr,
                                            recoveryRate, sc, tranches);
        int nrow = tmp.GetLength(0);
        for (int row = 0; row < nrow; ++row)
          expects[row,1] += tmp[row,1];
      }

      int [] ftdIndices = new int[nFTD];
      FTD [] ftds = new FTD [nFTD];
      for (int i = 0; i < nFTD; ++i) {
        ftdIndices[i] = (i+1)*nBasket/nFTD;
        ftds[i] = new FTD(start, end, Currency.USD, 0.0, DayCount.Actual360,
                          Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
        ftds[i].First = defaultFirst;
        ftds[i].NumberCovered = numCovered;
      }

      tranches[0] = 0.0;
      tranches[1] = 1.0;
      int nTranches = tranches.Length;
      double[,] lossLevels = new double[nTranches,1];
      for (int i = 0; i < nTranches; ++i)
        lossLevels[i,0] = tranches[i] ;

      RecoveryCurve recoveryCurve = new RecoveryCurve(start, recoveryRate);
      SurvivalCurve[] survCurves = new SurvivalCurve[nBasket];
      RecoveryCurve[] recoveryCurves = new RecoveryCurve[nBasket];
      double[] principals = new double[nBasket];
      for (int i = 0; i < nBasket; ++i)
      {
        survCurves[i] = sc;
        principals[i] = principal;
        recoveryCurves[i] = recoveryCurve;
      }

      string [] names = new string[nBasket];
      for( int i = 0; i < nBasket; i++ )
        names[i] = String.Format("Name {0}", i+1);


      FactorCorrelation correlation
        = new FactorCorrelation(names,1, new double[nBasket]);
      correlation.SetFactor(Math.Sqrt(corr));

      BasketPricer pricer
        = new FTDBasketPricer( start, start, end,
                               survCurves, recoveryCurves,
                               ftds, ftdIndices, principals,
                               copula, correlation,
                               stepSize, stepUnit, lossLevels );
      pricer.IntegrationPointsFirst = IntegrationPointsFirst;
      pricer.IntegrationPointsSecond = IntegrationPointsSecond;

      double totalPrincipal = pricer.TotalPrincipal;

      int rowIdx = 0;
      Dt current = start;
      while (Dt.Cmp(current,end) < 0) {
        current = Dt.Add(current, stepSize, stepUnit);
        if (Dt.Cmp(current,end) > 0)
          current = end;

        double loss = pricer.AccumulatedLoss(current, 0.0, 1.0) * totalPrincipal;
        Assert.AreEqual( expects[rowIdx,1],
                         loss,
                         tolerance,
                         String.Format("Loss date {0}", rowIdx)
                         );
      
        ++ rowIdx;
      }
      return;
    }


    //
    // Consistency between a multiple CDO^2 basket and child CDO losses
    // Condition:
    //   { Losses of the whole CDO^2}    { Sum of the losses of all   }
    //   { basket as computed from  } == { the child CDOs as computed }
    //   { the CDO^2BasketModel     }    { from the UniformBasketModel}
    //
    private enum CDO2BasketType { Analytic, SemiAnalytic, MonteCarlo };

    private void
    CDOSquredBasketTest( CDO2BasketType type, int simulationRuns, double eps )
    {
      double tolerance = principal * eps ;
      const int numCDOs = 2;
      const double trancheBegin = 0.0;
      const double trancheEnd = 1.0;

      int subBasketSize = nBasket / numCDOs ;
      if( numCDOs * subBasketSize != nBasket )
        throw new ArgumentException("number of CDOs not compatible with basket size");

      double[] tranches = new double [2];
      tranches[0] = trancheBegin;
      tranches[1] = trancheEnd;

      Dt start = Dt.Today();
      Dt end = Dt.Add(start, 5, TimeUnit.Years);

      SurvivalCurve sc = CreateSurvivalCurve(start, hazardRate);
      Copula copula = new Copula(CopulaType.Gauss,0,0);

      double[,] expects = UniformBasketResult(start, end, subBasketSize, copula, corr,
                                              recoveryRate, sc, tranches);
      for (int i = 1; i < numCDOs; ++i) {
        double[,] tmp = UniformBasketResult(start, end, subBasketSize, copula, corr,
                                            recoveryRate, sc, tranches);
        int nrow = tmp.GetLength(0);
        for (int row = 0; row < nrow; ++row)
          expects[row,1] += tmp[row,1];
      }

      double[] principals = new double[nBasket*numCDOs];
      double[] attachments = new double[numCDOs];
      double[] detachments = new double[numCDOs];
      for (int i = 0, idx = 0; i < numCDOs; ++i) {
        for( int j = 0; j < nBasket; ++j ) {
          if( j >= i*subBasketSize && j < (i+1)*subBasketSize )
            principals[idx++] = principal;
          else
            principals[idx++] = 0;
        }
        attachments[i] = trancheBegin;
        detachments[i] = trancheEnd;
      }

      tranches[0] = 0.0;
      tranches[1] = 1.0;
      int nTranches = tranches.Length;
      double[,] lossLevels = new double[nTranches,1];
      for (int i = 0; i < nTranches; ++i)
        lossLevels[i,0] = tranches[i] ;

      SurvivalCurve[] survCurves = new SurvivalCurve[nBasket];
      for (int i = 0; i < nBasket; ++i)
        survCurves[i] = sc;

      RecoveryCurve[] recoveryCurves = new RecoveryCurve[nBasket];
      RecoveryCurve recoveryCurve = new RecoveryCurve(start, recoveryRate);
      for (int i = 0; i < recoveryCurves.Length; ++i)
        recoveryCurves[i] = recoveryCurve;

      string [] names = new string[nBasket];
      for( int i = 0; i < nBasket; i++ )
        names[i] = String.Format("Name {0}", i+1);


      Correlation correlation
        = new GeneralCorrelation(names,new double[nBasket*nBasket]);
      ((GeneralCorrelation)correlation).SetCorrelation( corr );

      BasketPricer pricer;

      switch( type )
      {
      case CDO2BasketType.Analytic:
        correlation = CorrelationFactory.CreateFactorCorrelation( correlation );
        pricer = new AnalyticCDO2BasketPricer( start, start, end,
                                               survCurves, recoveryCurves, principals,
                                               attachments, detachments, true,
                                               copula, (FactorCorrelation)correlation,
                                               stepSize, stepUnit, lossLevels );
        ((AnalyticCDO2BasketPricer)pricer).GridSize = (1.0 - recoveryRate) / nBasket ; 
        break;
      case CDO2BasketType.SemiAnalytic:
        correlation = CorrelationFactory.CreateFactorCorrelation( correlation );
        pricer = new SemiAnalyticCDO2BasketPricer( start, start, end,
                                                   survCurves, recoveryCurves, principals,
                                                   attachments, detachments, null, false,
                                                   copula, (FactorCorrelation)correlation,
                                                   stepSize, stepUnit, lossLevels,
                                                   simulationRuns );
        break;
      case CDO2BasketType.MonteCarlo:
        pricer = new MonteCarloCDO2BasketPricer( start, start, end,
                                                 survCurves, recoveryCurves, principals,
                                                 attachments, detachments, false,
                                                 copula, (GeneralCorrelation)correlation,
                                                 stepSize, stepUnit, lossLevels, simulationRuns );
        break;
      default:
        throw new ArgumentException("Unknow CDO Squared basket type");
      }
      pricer.IntegrationPointsFirst = IntegrationPointsFirst;
      pricer.IntegrationPointsSecond = IntegrationPointsSecond;

      double totalPrincipal = pricer.TotalPrincipal;

      int rowIdx = 0;
      Dt current = start;
      while (Dt.Cmp(current,end) < 0) {
        current = Dt.Add(current, stepSize, stepUnit);
        if (Dt.Cmp(current,end) > 0)
          current = end;

        double loss = pricer.AccumulatedLoss(current, 0.0, 1.0) * totalPrincipal;
        Assert.AreEqual( expects[rowIdx,1],
                         loss,
                         tolerance,
                         String.Format("Loss date {0}", rowIdx)
                         );
      
        ++ rowIdx;
      }
      return;
    }

    [Test, Smoke]
    public void
    CDOSquredAnalyticBasketTest()
    {
      CDOSquredBasketTest( CDO2BasketType.Analytic, 0, 1e-6 );
    }

#if INCLUDE_SEMIANALYTIC
    // By its nature GaussApproximation does not work for very small probabilities
    [Test, Smoke]
    public void
    CDOSquredSemiAnalyticBasketTest()
    {
      CDOSquredBasketTest( CDO2BasketType.SemiAnalytic, 100000, 1e-2 );
    }
#endif

    [Test]
    public void
    CDOSquredMonteCarloBasketTest()
    {
      CDOSquredBasketTest( CDO2BasketType.MonteCarlo, 100000, 1e-2 );
    }

    bool adaptive_ = true;

    [OneTimeSetUp]
    public void Init()
    {
      adaptive_ = UniformBasketPricer.AdaptiveApproach;
      UniformBasketPricer.AdaptiveApproach = false;
    }
    
    [OneTimeTearDown]
    public void Done()
    {
      UniformBasketPricer.AdaptiveApproach = adaptive_;
    }

  } // TestBasket
} 
