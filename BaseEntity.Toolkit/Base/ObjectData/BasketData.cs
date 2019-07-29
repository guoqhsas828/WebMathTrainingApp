/*
 * BasketData.cs
 *
 *  -2008. All rights reserved.
 *
 * A simple class hold basket and tranche data
 *
 * This is an internal class.  Used in basket test programs.
 *
 */
using System;
using System.Collections;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <exclude />
  [Serializable]
  public class BasketData
  {
    /// <exclude />
    public BasketData()
    {
      this.Name = null;
      this.Type = BasketType.Heterogeneous;
      this.BasketSize = 100;
      this.AsOf = Dt.Today().ToStr("%D");
      this.Settle = AsOf;
      this.Maturity = Dt.Add(Dt.Today(), 5, TimeUnit.Years).ToStr("%D");
      this.CreditNames = null;
      this.Principals = null;
      this.CopulaType = CopulaType.Gauss;
      this.DfCommon = 0;
      this.DfIdiosyncratic = 0;
      this.Correlation = null;
      this.StepSize = 1;
      this.StepUnit = TimeUnit.Months;
      this.LossLevels = null;
      this.QuadraturePoints = 0;
      this.GridSize = 0;
      this.SimulationRuns = 100000;
      this.Tranches = null;
      this.DiscountData = null;
      this.CreditData = null;
      this.correlationObject_ = null;
    }

    //
    // Get Quadrature points
    //
    /// <exclude />
    public int
    GetQuadraturePoints(int basketSize)
    {
      int quadraturePoints = this.QuadraturePoints;
      if (quadraturePoints <= 0)
      {
        if (basketSize < 40)
          quadraturePoints = 7 + (basketSize - 5) * 10 / 35;
        else
          quadraturePoints = 17 + (basketSize - 40) / 10;
      }
      return quadraturePoints;
    }

    //
    // Get Base Correlation Object
    //
    /// <exclude />
    public CorrelationObject GetCorrelationObject()
    {
      return correlationObject_;
    }

    //
    // Get Base Correlation Object
    //
    /// <exclude />
    public void SetCorrelationObject(CorrelationObject obj)
    {
      correlationObject_ = obj;
    }

    //
    // Create a basket pricer
    //
    /// <exclude />
    public BasketPricer
    GetBasketPricer()
    {
      return GetBasketPricer(null, null, null, null);
    }

    //
    // Create basket pricer.
    //    If the parameter corrData is not null, it is used to construct the pricer;
    //    Otherwise, the data member CorrelationData is used.
    //
    //    If the parameter principals is not null, it is used to construct the pricer;
    //    Otherwise, the data member Principals is used.
    //
    //    In this way, we allow tranche specific correlation and notionals
    //
    private BasketPricer GetBasketPricer(
      CorrelationData corrData,
      double[] principals,
      SurvivalCurve[] survivalCurves,
      double[] apdp)
    {
      if (DiscountData == null)
        throw new System.NullReferenceException("No discount data specified");
      if (CreditData == null)
        throw new System.NullReferenceException("No credit data specified");

      switch (Type)
      {
        case BasketType.MonteCarloCDO2:
          return GetCDO2BasketPricer(corrData, principals);
        case BasketType.SemiAnalyticCDO2:
          return GetCDO2BasketPricer(corrData, principals);
        case BasketType.AnalyticCDO2:
          return GetCDO2BasketPricer(corrData, principals);
        default:
          break;
      }

      // check arguments
      if (corrData == null)
        corrData = this.Correlation;
      if (principals == null)
        principals = this.Principals;

      // dates
      Dt asOfDate = Dt.FromStr(AsOf, "%D");
      Dt settleDate = Dt.FromStr(Settle, "%D");
      Dt maturityDate = Dt.FromStr(Maturity, "%D");

      // set up loss levels
      double[,] lossLevels = new double[LossLevels.Length, 1];
      for (int i = 0; i < LossLevels.Length; ++i)
        lossLevels[i, 0] = LossLevels[i];

      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      GetIncludedAssets(principals, survivalCurves, out sc, out prins, out picks);
      rc = GetRecoveryCurves(sc);

      // Create copula
      int quadraturePoints = GetQuadraturePoints(prins.Length);
      Copula copula = new Copula(this.CopulaType,
                                  this.DfCommon,
                                  this.DfIdiosyncratic);


      // we need a correlation
      Correlation corr = null;

      // base correlation pricer is special
      if (Type == BasketType.BaseCorrelation)
      {
        BaseCorrelationBasketPricer basket = null;

        if (null != apdp)
        {
          // new interface
          BaseCorrelationObject bco = GetBaseCorrelation(corrData);
          if (bco == null)
            throw new System.NullReferenceException("No BaseCorrelation data specified");
          DiscountCurve dc = GetDiscountCurve();
          basket = new BaseCorrelationBasketPricer(asOfDate, settleDate, maturityDate,
                                                   dc, sc, rc, prins, bco, apdp[0], apdp[1],
                                                   StepSize, StepUnit);
        }
        else
        {
          // the following is the old interface
          double apCorr, dpCorr;
          BaseCorrelationObject bco = GetBaseCorrelation(picks, corrData, out apCorr, out dpCorr);

          // Create a heterogenoeus basket pricer
          corr = GetCorrelation(picks, null);
          FactorCorrelation correlation = CorrelationFactory.CreateFactorCorrelation(corr);
          SemiAnalyticBasketPricer heteroBasket =
            new SemiAnalyticBasketPricer(asOfDate, settleDate, maturityDate, sc, rc, prins,
            copula, correlation, StepSize, StepUnit, lossLevels,
            this.CheckRefinance);
          if (GridSize > 0.0)
            heteroBasket.GridSize = GridSize;
          heteroBasket.IntegrationPointsFirst = quadraturePoints;

          // Create a BaseCorrelationBasketPricer.
          if (bco == null)
          {
            basket = new BaseCorrelationBasketPricer(
              heteroBasket, (DiscountCurve)null, (BaseCorrelationObject)null, false,
              0.0000000001, 0.9999999999);
            basket.APCorrelation = apCorr;
            basket.DPCorrelation = dpCorr;
          }
          else
          {
            DiscountCurve discountCurve = this.GetDiscountCurve();
            basket = new BaseCorrelationBasketPricer(heteroBasket, discountCurve, bco, false,
              0.0, 1.0);
          }
        }

        basket.ConsistentCreditSensitivity = this.ConsistentCreditSensitivity;
        return basket;
      }

      corr = GetCorrelation(picks, corrData);


      // now create pricer
      switch (Type)
      {
        case BasketType.Uniform:
          {
            corr = CorrelationFactory.CreateSingleFactorCorrelation(corr);
            UniformBasketPricer basket =
              new UniformBasketPricer(asOfDate, settleDate, maturityDate,
                                      sc, rc, prins, copula,
                                      (SingleFactorCorrelation)corr,
                                      StepSize, StepUnit, lossLevels);
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
        case BasketType.Homogeneous:
          {
            corr = CorrelationFactory.CreateFactorCorrelation(corr);
            HomogeneousBasketPricer basket =
              new HomogeneousBasketPricer(asOfDate, settleDate, maturityDate,
                                          sc, rc, prins, copula,
                                          (FactorCorrelation)corr,
                                          StepSize, StepUnit, lossLevels);
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
        case BasketType.Heterogeneous:
          {
            corr = CorrelationFactory.CreateFactorCorrelation(corr);
            HeterogeneousBasketPricer basket =
              new HeterogeneousBasketPricer(asOfDate, settleDate, maturityDate,
              sc, rc, prins, copula, (FactorCorrelation)corr,
              StepSize, StepUnit, lossLevels);
            if (GridSize > 0.0)
              basket.GridSize = GridSize;
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
        case BasketType.SemiAnalytic:
          {
            corr = CorrelationFactory.CreateFactorCorrelation(corr);
            SemiAnalyticBasketPricer basket =
              new SemiAnalyticBasketPricer(asOfDate, settleDate, maturityDate,
              sc, rc, prins, copula, (FactorCorrelation)corr,
              StepSize, StepUnit, lossLevels, this.CheckRefinance);
            if (GridSize > 0.0)
              basket.GridSize = GridSize;
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
#if HAS_HeteroHomo
      case BasketType.HeteroHomo:
      {
        corr = CorrelationFactory.CreateFactorCorrelation(corr);
        HeteroHomoBasketPricer basket =
          new HeteroHomoBasketPricer(asOfDate, settleDate, maturityDate,
                                     sc, rc, prins, copula,
                                     (FactorCorrelation) corr,
                                     StepSize, StepUnit, lossLevels);
        if (GridSize > 0.0)
          basket.GridSize = GridSize;
        basket.IntegrationPointsFirst = quadraturePoints ;
        return basket;
      }
#endif
        case BasketType.MonteCarlo:
          {
            corr = CorrelationFactory.CreateGeneralCorrelation(corr);
            MonteCarloBasketPricer basket =
              new MonteCarloBasketPricer(asOfDate, settleDate, maturityDate,
                                         sc, rc, prins, copula,
                                         (GeneralCorrelation)corr,
                                         StepSize, StepUnit, lossLevels, SimulationRuns);
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
        default:
          throw new ArgumentException("Invalid basket type");
      }
    }


    //
    // Create CDO squared basket pricer.
    //
    private CDOSquaredBasketPricer
    GetCDO2BasketPricer(
                        CorrelationData corrData,
                        double[] principals
                        )
    {
      // check arguments
      if (corrData == null)
        corrData = this.Correlation;
      if (principals == null)
        principals = this.Principals;
      double[] attachments = this.SubAps;
      double[] detachments = this.SubDps;
      bool crossSubordination = this.CrossSubordination;

      int numNames = this.BasketSize;
      int numCdos = principals.Length / numNames;
      if (principals.Length != numNames * numCdos)
        throw new System.NullReferenceException("Invalid principal array for CDO squared basket");
      if (this.CreditNames.Length != numNames)
        throw new System.NullReferenceException("Number of credit names and basket size not match");
      if (this.SubAps.Length != numCdos)
        throw new System.NullReferenceException("Number of sub-baskets and sub-attachments not match");
      if (this.SubDps.Length != numCdos)
        throw new System.NullReferenceException("Number of sub-baskets and sub-detachments not match");

      // dates
      Dt asOfDate = Dt.FromStr(AsOf, "%D");
      Dt settleDate = Dt.FromStr(Settle, "%D");
      Dt maturityDate = Dt.FromStr(Maturity, "%D");

      // set up loss levels
      double[,] lossLevels = new double[LossLevels.Length, 1];
      for (int i = 0; i < LossLevels.Length; ++i)
        lossLevels[i, 0] = LossLevels[i];

      // Set up basket arguments
      double[] picks = new double[numNames];
      for (int i = 0; i < numNames; ++i)
        picks[i] = 1.0;
      SurvivalCurve[] sc = this.CreditData.GetSurvivalCurves(this.DiscountData.GetDiscountCurve(),
                                                               picks);
      RecoveryCurve[] rc = GetRecoveryCurves(sc);

      // Create copula
      int quadraturePoints = GetQuadraturePoints(sc.Length);
      Copula copula = new Copula(this.CopulaType,
                                  this.DfCommon,
                                  this.DfIdiosyncratic);


      // we need a correlation
      Correlation corr = GetCorrelation(picks, corrData);

      // now create pricer
      switch (Type)
      {
        case BasketType.MonteCarloCDO2:
          {
            corr = CorrelationFactory.CreateGeneralCorrelation(corr);
            MonteCarloCDO2BasketPricer basket =
              new MonteCarloCDO2BasketPricer(asOfDate, settleDate, maturityDate,
                                              sc, rc, principals,
                                              attachments, detachments, crossSubordination,
                                              copula, (GeneralCorrelation)corr,
                                              StepSize, StepUnit, lossLevels, SimulationRuns);
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
        case BasketType.SemiAnalyticCDO2:
          {
            corr = CorrelationFactory.CreateFactorCorrelation(corr);
            SemiAnalyticCDO2BasketPricer basket =
              new SemiAnalyticCDO2BasketPricer(asOfDate, settleDate, maturityDate,
                                                sc, rc, principals,
                                                attachments, detachments, null, crossSubordination,
                                                copula, (FactorCorrelation)corr,
                                                StepSize, StepUnit, lossLevels, SimulationRuns);
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
        case BasketType.AnalyticCDO2:
          {
            corr = CorrelationFactory.CreateFactorCorrelation(corr);
            AnalyticCDO2BasketPricer basket =
              new AnalyticCDO2BasketPricer(asOfDate, settleDate, maturityDate,
                                            sc, rc, principals,
                                            attachments, detachments, crossSubordination,
                                            copula, (FactorCorrelation)corr,
                                            StepSize, StepUnit, lossLevels);
            if (GridSize > 0.0)
              basket.GridSize = GridSize;
            basket.IntegrationPointsFirst = quadraturePoints;
            return basket;
          }
        default:
          throw new ArgumentException("Invalid basket type");
      }

      // end of function
    }


    //
    // create CDO tranches
    //
    /// <exclude />
    public SyntheticCDO[]
    GetSyntheticCDOs()
    {
      if (Tranches == null || Tranches.Length <= 0)
        return null;

      SyntheticCDO[] cdos = new SyntheticCDO[Tranches.Length];
      for (int i = 0; i < Tranches.Length; ++i)
      {
        Tranche t = Tranches[i];
        Dt effectiveDate = Dt.FromStr(t.Effective, "%D");
        Dt maturityDate = Dt.FromStr(t.Maturity, "%D");
        SyntheticCDO cdo = new SyntheticCDO(effectiveDate, maturityDate,
                                             t.Currency,
                                             t.Premium > 1 ? t.Premium / 10000.0 : t.Premium,
                                             t.DayCount, t.Frequency,
                                             t.Roll, t.Calendar);

        if (t.FirstPremium == null || t.FirstPremium.Length <= 0)
          cdo.FirstPrem = Dt.FromStr(t.FirstPremium, "%D");

        cdo.Attachment = t.Attachment;
        cdo.Detachment = t.Detachment;
        cdo.Fee = t.Fee;
        cdo.FeeSettle = effectiveDate;
        cdo.Validate();
        cdo.Description = t.Name;
        cdos[i] = cdo;
      }

      return cdos;
    }

    //
    // Create CDO pricers
    //
    /// <exclude />
    public SyntheticCDOPricer[] GetSyntheticCDOPricers()
    {
      return GetSyntheticCDOPricers(null, null, null);
    }

    //
    // Create CDO pricers
    //
    /// <exclude />
    public SyntheticCDOPricer[] GetSyntheticCDOPricers(
      CorrelationObject correlationObject,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves
      )
    {
      SyntheticCDO[] cdos = GetSyntheticCDOs();
      SyntheticCDOPricer[] pricers = null;
      if (!NeedTrancheSpecificBasket(this.Tranches))
        pricers = CreateCDOPricers(cdos, discountCurve,
          survivalCurves, correlationObject);
      if (pricers == null)
        pricers = CreateCDOPricersOldWay(cdos, discountCurve);
      foreach (var pricer in pricers)
      {
        var bp = pricer.Basket as BaseCorrelationBasketPricer;
        if (bp != null) bp.ConsistentCreditSensitivity = ConsistentCreditSensitivity;
      }
      return pricers;
    }

    //
    // Create an array of CDO pricers, new interface
    //
    private SyntheticCDOPricer[] CreateCDOPricers(
      SyntheticCDO[] cdos,
      DiscountCurve dc,
      SurvivalCurve[] survivalCurves,
      CorrelationObject correlationObject)
    {
      if (dc == null)
        dc = GetDiscountCurve();

      // The CDO Squared pricer should still use the old function
      switch (Type)
      {
        case BasketType.MonteCarloCDO2:
          return null;
        case BasketType.SemiAnalyticCDO2:
          return null;
        case BasketType.AnalyticCDO2:
          return null;
        default:
          break;
      }

      // dates
      Dt asOfDate = Dt.FromStr(AsOf, "%D");
      Dt settleDate = Dt.FromStr(Settle, "%D");
      Dt portfolioStart = (PortfolioStart == null ?
        Dt.Empty : Dt.FromStr(PortfolioStart, "%D"));

      // set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      GetIncludedAssets(this.Principals, survivalCurves, out sc, out prins, out picks);
      rc = GetRecoveryCurves(sc);

      CorrelationObject correlation = null;
      if (correlationObject == null)
      {
        correlation =
          (this.Correlation.BaseCorrelations != null ?
            (CorrelationObject)GetBaseCorrelation(this.Correlation) :
            (CorrelationObject)GetCorrelation(picks, this.Correlation));
      }
      else
        correlation = (CorrelationObject)correlationObject;

      if (correlation == null)
        return null; // maybe old XML file, need to switch back to old method

      // quadrature points and copula
      int quadraturePoints = GetQuadraturePoints(prins.Length);
      Copula copula = new Copula(this.CopulaType,
                                  this.DfCommon,
                                  this.DfIdiosyncratic);

      // fix basket type
      BasketType bsktType = Type;
      if (bsktType == BasketType.BaseCorrelation)
        bsktType = BasketType.Heterogeneous;

      // create pricers
      switch (bsktType)
      {
        case BasketType.Uniform:
          return BasketPricerFactory.CDOPricerLargePool(
            cdos, portfolioStart, asOfDate, settleDate, dc, sc,
            prins, copula, correlation, this.StepSize, this.StepUnit,
            quadraturePoints, this.TrancheNotionals, RescaleStrikes
            );
        case BasketType.Homogeneous:
          return BasketPricerFactory.CDOPricerHomogeneous(
            cdos, portfolioStart, asOfDate, settleDate, dc, sc,
            prins, copula, correlation, this.StepSize, this.StepUnit,
            quadraturePoints, this.TrancheNotionals, RescaleStrikes
            );
        case BasketType.Heterogeneous:
          return BasketPricerFactory.CDOPricerHeterogeneous(
            cdos, portfolioStart, asOfDate, settleDate, dc, sc,
            prins, copula, correlation, this.StepSize, this.StepUnit,
            quadraturePoints, this.GridSize, this.TrancheNotionals, RescaleStrikes
            );
        case BasketType.SemiAnalytic:
          return BasketPricerFactory.CDOPricerSemiAnalytic(
            cdos, portfolioStart, asOfDate, settleDate, dc, sc,
            prins, copula, correlation, this.StepSize, this.StepUnit,
            quadraturePoints, this.GridSize, this.TrancheNotionals, RescaleStrikes,
            this.CheckRefinance
            );
        case BasketType.MonteCarlo:
          return BasketPricerFactory.CDOPricerMonteCarlo(
            cdos, portfolioStart, asOfDate, settleDate, dc, sc,
            prins, copula, correlation, this.StepSize, this.StepUnit,
            this.SimulationRuns, this.TrancheNotionals, RescaleStrikes,
            0 /*seed*/ );
        default:
          throw new ArgumentException("Invalid basket type");
      }
    } // end of the new interface CreateCDOPricers()


    //
    // The old interface, works with the old test files
    //
    private SyntheticCDOPricer[]
    CreateCDOPricersOldWay(SyntheticCDO[] cdos,
                            DiscountCurve dc)
    {
      if (dc == null)
        dc = GetDiscountCurve();

      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[cdos.Length];

      bool[] wantOwnBasket = new bool[cdos.Length];
      bool needGlobalBasket = false;
      for (int i = 0; i < cdos.Length; ++i)
      {
        wantOwnBasket[i] = false;
        // Need tranche specific basket when the tranche is priced by
        // BaseCorrelationPricer and has its own attach and detach
        // correlations, or when it has tranche specific notionals
        if ((Tranches[i].Correlations != null
              && this.Type == BasketType.BaseCorrelation)
            || Tranches[i].Notionals != null)
          wantOwnBasket[i] = true;
        else
          needGlobalBasket = true;
      }

      BasketPricer globalBasket = null;
      if (needGlobalBasket)
        globalBasket = GetBasketPricer();

      for (int i = 0; i < cdos.Length; ++i)
      {
        BasketPricer localBasket = globalBasket;
        if (wantOwnBasket[i])
        {
          double[] apdp = null;
          CorrelationData cd = this.Correlation;
          if (Tranches[i].Correlations != null
              && this.Type == BasketType.BaseCorrelation)
          {
            cd = new CorrelationData();
            cd.Type = CorrelationData.CorrelationType.BaseCorrelation;
            cd.Data = Tranches[i].Correlations;
          }
          else if (this.Type == BasketType.BaseCorrelation
                   && null != cd && null != cd.BaseCorrelations)
          {
            apdp = new double[2] { Tranches[i].Attachment, Tranches[i].Detachment };
          }
          double[] notionals = this.Principals;
          if (Tranches[i].Notionals != null)
          {
            notionals = Tranches[i].Notionals;
          }
          localBasket = GetBasketPricer(cd, notionals, null, apdp);
        }
        if (localBasket is BaseCorrelationBasketPricer)
        {
          BaseCorrelationBasketPricer bp = (BaseCorrelationBasketPricer)localBasket;
          bp.Attachment = cdos[i].Attachment;
          bp.Detachment = cdos[i].Detachment;
        }
        pricers[i] = new SyntheticCDOPricer(cdos[i], localBasket, dc, 1.0, null);
        pricers[i].Notional = (cdos[i].Detachment - cdos[i].Attachment) * localBasket.TotalPrincipal;
      }

      return pricers;
    }


    //
    // Create discount curve
    //
    /// <exclude />
    public DiscountCurve
    GetDiscountCurve()
    {
      if (DiscountData == null)
        throw new System.NullReferenceException("No discount data specified");
      return DiscountData.GetDiscountCurve();
    }

    //
    // Get recovery curves from survival curves
    //
    private static RecoveryCurve[]
    GetRecoveryCurves(SurvivalCurve[] sc)
    {
      RecoveryCurve[] rc = new RecoveryCurve[sc.Length];
      for (int i = 0; i < sc.Length; ++i)
      {
        SurvivalCalibrator cal = sc[i].SurvivalCalibrator;
        if (cal == null)
          throw new ArgumentException("null calibrator in survival curve");
        rc[i] = cal.RecoveryCurve;
        if (rc[i] == null)
          throw new ArgumentException("null recovery curve in survival curve");
      }
      return rc;
    }

    //
    // Determine which assets are included
    //
    private void GetIncludedAssets(
      double[] principals,
      SurvivalCurve[] survivalCurves,
      out SurvivalCurve[] sc,
      out double[] prins,
      out double[] picks
      )
    {
      string[] creditNames = CreditNames;
      if (creditNames == null)
      {
        if (survivalCurves != null)
          creditNames = Utils.GetCreditNames(survivalCurves);
        else
          creditNames = new string[0];
      }

      // Argument validation
      if (principals.Length != 1 && principals.Length != creditNames.Length)
        throw new ArgumentException("Number of principals must match number of credit names");

      // Set up number of curves we are interested in
      picks = new double[creditNames.Length];
      int nCurves = 0;
      for (int i = 0; i < creditNames.Length; i++)
      {
        if ((creditNames[i].Length > 0) && (principals.Length == 1 || principals[i] != 0.0))
        {
          nCurves++;
          picks[i] = 1;
        }
        else
          picks[i] = 0;
      }

      // Set up arguments for basket pricer, ignoring notionals that are zero.
      prins = new double[nCurves];

      for (int i = 0, idx = 0; i < creditNames.Length; i++)
      {
        if (picks[i] > 0.0)
        {
          prins[idx] = (principals.Length == 1) ? principals[0] : principals[i];
          idx++;
        }
      }

      // If supplied survival curves, we pick from them
      if (survivalCurves != null)
      {
        sc = new SurvivalCurve[nCurves];
        for (int i = 0, idx = 0; i < creditNames.Length; i++)
        {
          if (picks[i] > 0.0)
          {
            sc[idx] = FindCurve(survivalCurves, creditNames[i]);
            idx++;
          }
        }
        return;
      }

      // survival curves not supplied
      if (this.CreditNames.Length != this.CreditData.Credits.Length)
        sc = CreditData.GetSurvivalCurves(DiscountData.GetDiscountCurve(),
                                           picks, this.CreditNames);
      else
        sc = CreditData.GetSurvivalCurves(DiscountData.GetDiscountCurve(),
                                         picks);

      return;
    }

    private static SurvivalCurve FindCurve(
      SurvivalCurve[] curves, string name)
    {
      if (name == null) return null;
      foreach (SurvivalCurve sc in curves)
        if (String.Compare(sc.Name, name, true) == 0)
          return sc;
      throw new ToolkitException(String.Format("Credit curve '{0}' not found", name));
    }

    //
    // Construct a Correlation object from the CorrelationData
    //
    private Correlation
    GetCorrelation(double[] picks, CorrelationData cd)
    {
      if (cd == null || cd.Data.Length == 1)
      {
        // find size of picks
        int nPicks = 0;
        for (int i = 0; i < picks.Length; ++i)
          if (picks[i] > 0.0) ++nPicks;

        // setup names array
        string[] names = this.CreditNames;
        if (this.CreditNames.Length != nPicks)
        {
          names = new string[nPicks];
          for (int i = 0, idx = 0; i < picks.Length; ++i)
            if (picks[i] > 0.0)
              names[idx++] = this.CreditNames[i];
        }

        // return a single factor correlation
        double factor = 0;
        if (cd != null)
        {
          factor = cd.Data[0];
          if (cd.Type == CorrelationData.CorrelationType.GeneralCorrelation)
          {
            factor = Math.Sqrt(factor);
          }
        }
        return new SingleFactorCorrelation(names, factor);
      }

      if (cd.Type == CorrelationData.CorrelationType.BaseCorrelation)
      {
        if (this.Type == BasketType.BaseCorrelation) return null;
        throw new System.InvalidOperationException("Invalid conversion from a BaseCorrelation to a factor or general correlation");
      }

      if (cd.Type == CorrelationData.CorrelationType.GeneralCorrelation)
      {
        GeneralCorrelation corr = new GeneralCorrelation(this.CreditNames, cd.Data);
        return CorrelationFactory.CreateGeneralCorrelation(corr, picks);
      }
      else if (cd.Type == CorrelationData.CorrelationType.FactorCorrelation)
      {
        FactorCorrelation corr = new FactorCorrelation(this.CreditNames,
                                                        cd.Data.Length / this.CreditNames.Length,
                                                        cd.Data);
        return CorrelationFactory.CreateFactorCorrelation(corr, picks);
      }
      else
        throw new ArgumentException("Invalid correlation type");
    }

    //
    // Create correlation object, new interface
    //
    private CorrelationObject
    CreateCorrelationObject(double[] picks)
    {
      if (correlationObject_ != null)
        // use the existing one
        return correlationObject_;

      // return null if we need tranche specific correlation
      for (int i = 0; i < this.Tranches.Length; ++i)
        if (this.Tranches[i].Correlations != null)
          return null;

      // find the correlation data
      CorrelationData cd = this.Correlation;
      if (cd == null)
        return null;

      // not a base correlation object
      if (cd.Type != CorrelationData.CorrelationType.BaseCorrelation)
        return GetCorrelation(picks, cd);

      // we have to deal with base correlation
      return GetBaseCorrelation(cd);
    }

    /// <summary>
    ///  Create a base correlation object from data
    /// </summary>
    /// <param name="cd">Correlation data</param>
    /// <returns>BaseCorrelation object</returns>
    public static BaseCorrelationObject GetBaseCorrelation(CorrelationData cd)
    {
      BaseCorrelationObject bco = null;
      if (cd.BaseCorrelations != null)
      {
        CorrelationData.BaseCorrelationData[] bcData = cd.BaseCorrelations;
        int N = bcData.Length;
        Dt[] dates = new Dt[N];
        BaseCorrelation[] bcs = new BaseCorrelation[N];
        for (int j = 0; j < N; ++j)
        {
          CorrelationData.BaseCorrelationData bcd = bcData[j];
          BaseCorrelation b = new BaseCorrelation(
            bcd.Method, bcd.StrikeMethod, null, bcd.Strikes, bcd.Correlations);
          b.Interp = InterpFactory.FromMethod(bcd.InterpMethod,
                                               bcd.ExtrapMethod,
                                               0.0, 1.0);
          b.ScalingFactor = bcd.ScalingFactor;
          if (bcd.TenorName != null)
            b.Name = bcd.TenorName;
          bcs[j] = b;
          if (bcd.TenorDate != null)
            dates[j] = Dt.FromStr(bcd.TenorDate, "%D");
        }
        if (N > 1)
        {
          BaseCorrelationTermStruct bcts = new BaseCorrelationTermStruct(dates, bcs);
          bcts.CalibrationMethod = cd.BCCalibrationMethod;
          if (cd.Interp != null)
            bcts.Interp = InterpFactory.FromMethod(cd.Interp.InterpMethod,
                                                    cd.Interp.ExtrapMethod,
                                                    0.0, 1.0);
          bco = bcts;
        }
        else
          bco = bcs[0];

        if (cd.Name != null)
          bco.Name = cd.Name;
      }

      return bco;
    }

    //
    // Construct a BaseCorrelation object from the CorrelationData, old interface
    //
    private BaseCorrelationObject
    GetBaseCorrelation(double[] picks,
                        CorrelationData cd,
                        out double apCorr,
                        out double dpCorr)
    {
      if (cd != null && cd.Type != CorrelationData.CorrelationType.BaseCorrelation)
      {
        if (cd.Data.Length != 1)
          throw new ArgumentException("Only single factor correlation can be used with BaseCorrelation pricer");

        SingleFactorCorrelation corr = (SingleFactorCorrelation)GetCorrelation(picks, cd);
        if (corr == null)
          throw new NullReferenceException("No correlation data specified");
        apCorr = dpCorr = corr.GetFactor() * corr.GetFactor();
        return null;
      }

      // for base correlation type
      if (cd.BaseCorrelations == null)
      {
        // Old interfaces
        if (cd.Data != null)
        {
          if (cd.Data.Length == 1)
          {
            apCorr = dpCorr = cd.Data[0];
            return null;
          }
          else if (cd.Data.Length == 2)
          {
            apCorr = cd.Data[0];
            dpCorr = cd.Data[1];
            return null;
          }
        }
        throw new ArgumentException("Invalid base correlation data");
      }

      apCorr = dpCorr = Double.NaN;

      return GetBaseCorrelation(cd);
    }

    //
    // Check if we need tranche specific basket
    //
    private static bool
    NeedTrancheSpecificBasket(Tranche[] tranches)
    {
      foreach (Tranche t in tranches)
      {
        if (t.Correlations != null || t.Notionals != null)
          return true;
      }
      return false;
    }

    //
    // class for CDO tranche
    //
    /// <exclude />
    [Serializable]
    public class Tranche
    {
      /// <exclude />
      public string Name;
      /// <exclude />
      public string Effective;
      /// <exclude />
      public string FirstPremium;
      /// <exclude />
      public string Maturity;
      /// <exclude />
      public Currency Currency;
      /// <exclude />
      public DayCount DayCount;
      /// <exclude />
      public Frequency Frequency;
      /// <exclude />
      public Calendar Calendar;
      /// <exclude />
      public BDConvention Roll;
      /// <exclude />
      public double Attachment;
      /// <exclude />
      public double Detachment;
      /// <exclude />
      public double Premium;
      /// <exclude />
      public double Fee;
      /// <exclude />
      public double[] Correlations; // ApCorrelation and DpCorrelation, used only in BaseCorrelationPricer
      /// <excluded />
      public double[] Notionals; // tranche specific principals
    }; // class Trache


    //
    // class of Index quotes
    //
    /// <summary>
    ///   CDO tranche quotes
    /// </summary>
    /// <exclude />
    [Serializable]
    public class TrancheQuotes
    {
      /// <exclude />
      public string Name;
      /// <exclude />
      public string Effective;
      /// <exclude />
      public string FirstPremium;
      /// <exclude />
      public Currency Currency;
      /// <exclude />
      public DayCount DayCount;
      /// <exclude />
      public Frequency Frequency;
      /// <exclude />
      public Calendar Calendar;
      /// <exclude />
      public BDConvention Roll;
      /// <exclude />
      public double RunningPremium;
      /// <exclude />
      public bool Funded;

      /// <exclude />
      public string[] Maturities;
      /// <exclude />
      public string[] TenorNames;
      /// <exclude />
      public double[] Detachments;
      /// <exclude />
      public double[] Notionals;
      /// <exclude />
      public double[] Quotes;

      /// <exclude />
      public string[] ResetDates;
      /// <exclude />
      public double[] ResetRates;

      /// <exclude />
      public string[] CreditNames;

      /// <summary>
      ///   Convert Tranche Quotes data to cdos
      /// </summary>
      /// <returns>Jagged CDO array</returns>
      public SyntheticCDO[][] ToCDOs()
      {
        if (TenorNames == null || TenorNames.Length == 0)
          throw new ToolkitException("TenorNames cannot be null");

        // find the number of effective tenors
        int nCDOs = Detachments.Length;
        int nTenors = TenorNames.Length;
        int nRealTenors = nTenors;

        Dt effective = Dt.FromStr(Effective, "%D");

        // Set up CDO tranche arrays
        SyntheticCDO[][] cdosAry = new SyntheticCDO[nRealTenors][];
        for (int j = 0, idx = 0; j < nTenors; ++j)
        {
          Dt maturity = Maturities == null ?
            Dt.Add(effective, TenorNames[j]) : Dt.FromStr(Maturities[j], "%D");
          SyntheticCDO[] cdos = new SyntheticCDO[nCDOs];
          for (int i = 0; i < nCDOs; i++)
          {
            double quote = Quotes[j * nCDOs + i];
            if (quote <= 0.0)
              throw new ArgumentException(String.Format("Quote value must be greater than 0, not {0}", quote));
            bool isFee = (quote < 1.0);
            cdos[i] = new SyntheticCDO(
              effective, maturity, this.Currency,
              (isFee ? RunningPremium : quote) / 10000.0,
              this.DayCount, this.Frequency, this.Roll, this.Calendar);
            cdos[i].Attachment = (i == 0) ? 0.0 : Detachments[i - 1];
            cdos[i].Detachment = Detachments[i];
            if (isFee)
            {
              cdos[i].Fee = quote;
              cdos[i].FeeSettle = effective;
            }
            cdos[i].Validate();
            cdos[i].Description = ProductName(null, TenorNames[j], cdos[i]);
          }
          cdosAry[idx++] = cdos;
        }

        return cdosAry;
      }

      /// <summary>
      ///   Get CDO quotes as 2D array
      /// </summary>
      /// <exclude />
      public double[,] GetQuotes()
      {
        // find the number of effective tenors
        int nCDOs = Detachments.Length;
        int nTenors = TenorNames.Length;
        double[,] quotes = new double[nCDOs, nTenors];
        for (int j = 0; j < nTenors; ++j)
          for (int i = 0; i < nCDOs; i++)
            quotes[i,j] = Quotes[j * nCDOs + i];
        return quotes;
      }

      /// <exclude />
      public double[,] GetRunningPremiums()
      {
        int nCDOs = Detachments.Length;
        int nTenors = TenorNames.Length;
        double[,] running = new double[nCDOs, nTenors];
        for (int i = 0; i < nCDOs; ++i)
          for (int j = 0; j < nTenors; ++j)
            running[i, j] = RunningPremium;
        return running;
      }

      private static string ProductName(string indexName, string tenorName, SyntheticCDO cdo)
      {
        if (tenorName == null || tenorName == String.Empty)
        {
          int y = cdo.Maturity.Year - cdo.Effective.Year;
          int m = y * 12 + cdo.Maturity.Month - cdo.Effective.Month;
          if (m > 12)
          {
            tenorName += String.Format("{0}Y", m / 12);
            m %= 12;
          }
          if (m > 0)
            tenorName += String.Format("{0}M", m);
          if (tenorName == null || tenorName == String.Empty)
            tenorName = cdo.Maturity.ToStr("%D");
        }
        string tranchName = String.Format("{0:P}~{1:P}", cdo.Attachment, cdo.Detachment) + tenorName;
        if (indexName != null && indexName != String.Empty)
          tranchName += "/" + indexName;
        return tranchName;
      }

      /// <exclude />
      public SortedList RateResets()
      {
        if (ResetDates == null || ResetRates == null) return null;
        if (ResetRates.Length != ResetDates.Length)
          throw new System.ArgumentException("Numbers of reset rates and dates not match");
        SortedList result = new SortedList();
        for (int i = 0; i < ResetDates.Length; ++i)
          result[ResetDates[i]] = ResetRates[i];
        return result;
      }
    };

    //
    // class of Index quotes
    //
    /// <summary>
    ///   Index data
    /// </summary>
    /// <exclude />
    [Serializable]
    public class Index
    {
      /// <exclude />
      public string Name;
      /// <exclude />
      public string Effective;
      /// <exclude />
      public string FirstPremium;
      /// <exclude />
      public Currency Currency;
      /// <exclude />
      public DayCount DayCount;
      /// <exclude />
      public Frequency Frequency;
      /// <exclude />
      public Calendar Calendar;
      /// <exclude />
      public BDConvention Roll;
      /// <exclude />
      public bool Funded;

      /// <exclude />
      public string[] Maturities;
      /// <exclude />
      public string[] TenorNames;
      /// <exclude />
      public double[] DealPremia;
      /// <exclude />
      public double[] Notionals;
      /// <exclude />
      public double[] Quotes;
      /// <exclude />
      public bool QuotesArePrices;
      /// <exclude />
      public CDXScalingMethod[] ScalingMethods;

      /// <exclude />
      public string[] CreditNames;
      /// <exclude />
      public double[] CreditWeights;
      /// <exclude />
      public double[] ScalingWeights;
      /// <exclude />
      public bool AbsoluteScaling;

      /// <exclude />
      public double[] ScalingFactors(Dt asOf, Dt settle,
        DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
      {
        Dt effectiveDate = Dt.FromStr(this.Effective, "%D");
        Dt firstPremiumDate =
          this.FirstPremium == null ? Dt.Empty : Dt.FromStr(FirstPremium, "%D");

        double[] indexWeights = this.CreditWeights;
        double[] scalingWeights = this.ScalingWeights;
        string[] tenors = TenorNames;
        Dt[] maturities = null;
        if (this.Maturities != null)
        {
          maturities = new Dt[this.Maturities.Length];
          for (int i = 0; i < this.Maturities.Length; ++i)
            maturities[i] = Dt.FromStr(this.Maturities[i], "%D");
        }

        // Validate
        if (maturities != null && maturities.Length != 0 && tenors.Length != maturities.Length)
          throw new System.ArgumentException("Number of tenors and maturities must be the same");
        if (indexWeights != null && indexWeights.Length != 0 && indexWeights.Length != survivalCurves.Length)
          throw new System.ArgumentException("Number of index weights must match number of survival curves");
        if (scalingWeights != null && scalingWeights.Length != 0 && scalingWeights.Length != survivalCurves.Length)
          throw new System.ArgumentException("Number of scaling weights must match number of survival curves");

        // Check basket input
        SurvivalCurve[] survCurves = survivalCurves;

        // Create indices for scaling
        CDX[] cdx = new CDX[tenors.Length];
        for (int i = 0; i < tenors.Length; i++)
        {
          Dt maturity = (maturities == null || maturities.Length == 0)
            ? Dt.CDSMaturity(effectiveDate, tenors[i]) : maturities[i];
          cdx[i] = new CDX(effectiveDate, maturity, this.Currency,
            this.DealPremia[i] / 10000.0, this.DayCount,
            this.Frequency, this.Roll, this.Calendar, indexWeights);
          if (!firstPremiumDate.IsEmpty())
            cdx[i].FirstPrem = firstPremiumDate;
          cdx[i].Funded = false;
        }

        // Convert quoted spreads from basis points
        double[] quotes = Quotes;
        if (!QuotesArePrices)
        {
          for (int i = 0; i < quotes.Length; i++)
            quotes[i] /= 10000.0;
        }

        // Return resulting scaling
        return CDXPricer.Scaling(asOf, settle, cdx, tenors, quotes,
          QuotesArePrices, ScalingMethods, !AbsoluteScaling, null/*overrides*/,
          discountCurve, survCurves, scalingWeights);
      }

      /// <exclude />
      public SurvivalCurve[] ScaleCurves(
        SurvivalCurve[] survivalCurves, double[] scalingFactors)
      {
        SurvivalCurve[] scaledCurves = new SurvivalCurve[survivalCurves.Length];
        for (int i = 0; i < survivalCurves.Length; ++i)
          if (survivalCurves[i] != null)
          {
            scaledCurves[i] =
            SurvivalCurve.Scale(survivalCurves[i], TenorNames, scalingFactors, true);
            scaledCurves[i].Name = survivalCurves[i].Name + " scaled";
          }
        return scaledCurves;
      }

    };


    /// <summary>
    ///   Base correlation parameter object
    /// </summary>
    [Serializable]
    public class BaseCorrelationParam
    {
      /// <exclude />
      public CopulaType CopulaType = CopulaType.Gauss;
      /// <exclude />
      public int DfCommon = 2;
      /// <exclude />
      public int DfIdiosyncratic = 2;
      /// <exclude />
      public int StepSize = 3;
      /// <exclude />
      public TimeUnit StepUnit = TimeUnit.Months;
      /// <exclude />
      public int QuadraturePoints = 0;
      /// <exclude />
      public double GridSize = 0.005;
      /// <exclude />
      public double ToleranceF = 1E-7;
      /// <exclude />
      public double ToleranceX = 1E-6;
      /// <exclude />
      public InterpMethod StrikeInterp = InterpMethod.PCHIP;
      /// <exclude />
      public ExtrapMethod StrikeExtrap = ExtrapMethod.Smooth;
      /// <exclude />
      public InterpMethod TenorInterp = InterpMethod.Linear;
      /// <exclude />
      public ExtrapMethod TenorExtrap = ExtrapMethod.Const;
      /// <exclude />
      public double Min = 0.0;
      /// <exclude />
      public double Max = 1.0;
      /// <exclude />
      public BaseCorrelationMethod Method = BaseCorrelationMethod.ArbitrageFree;
      /// <exclude />
      public BaseCorrelationCalibrationMethod CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;
      /// <exclude />
      public BaseCorrelationStrikeMethod StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLoss;
      /// <exclude />
      public BasketType ModelType = BasketType.Default;
      /// <exclude />
      public int SampleSize = 10000;
      /// <exclude />
      public int Seed = 0;

      /// <exclude />
      public enum BasketType
      {
        /// <exclude />
        Default,
        /// <exclude />
        LargePool,
        /// <exclude />
        Uniform,
        /// <exclude />
        Homogeneous,
        /// <exclude />
        Heterogeneous,
        /// <exclude />
        SemiAnalytic,
        /// <exclude />
        MonteCarlo
      };
    };

    //
    // basket type
    //
    /// <exclude />
    [BaseEntity.Shared.AlphabeticalOrderEnum]
    public enum BasketType
    {
      /// <exclude />
      Uniform,
      /// <exclude />
      Homogeneous,
      /// <exclude />
      HeteroHomo,
      /// <exclude />
      Heterogeneous,
      /// <exclude />
      SemiAnalytic,
      /// <exclude />
      MonteCarlo,
      /// <exclude />
      BaseCorrelation,
      /// <exclude />
      MonteCarloCDO2,
      /// <exclude />
      SemiAnalyticCDO2,
      /// <exclude />
      AnalyticCDO2
    };

    //
    // class for correlation data
    //
    /// <exclude />
    [Serializable]
    public class CorrelationData
    {
      /// <exclude />
      public string Name;
      /// <exclude />
      public CorrelationType Type;
      /// <exclude />
      public double[] Data;
      /// <exclude />
      public BaseCorrelationData[] BaseCorrelations;
      /// <exclude />
      public TimeInterp Interp;
      /// <exclude />
      public BaseCorrelationCalibrationMethod BCCalibrationMethod;

      /// <exclude />
      public class BaseCorrelationData
      {
        /// <exclude />
        public BaseCorrelationMethod Method;
        /// <exclude />
        public BaseCorrelationStrikeMethod StrikeMethod;
        /// <exclude />
        public InterpMethod InterpMethod;
        /// <exclude />
        public ExtrapMethod ExtrapMethod;
        /// <exclude />
        public double ScalingFactor;
        /// <exclude />
        public double[] Strikes;
        /// <exclude />
        public double[] Correlations;
        /// <exclude />
        public string TenorDate;
        /// <exclude />
        public string TenorName;
      };
      /// <exclude />
      public class TimeInterp
      {
        /// <exclude />
        public InterpMethod InterpMethod;
        /// <exclude />
        public ExtrapMethod ExtrapMethod;
      };
      /// <exclude />
      public enum CorrelationType
      {
        /// <exclude />
        BaseCorrelation,
        /// <exclude />
        FactorCorrelation,
        /// <exclude />
        GeneralCorrelation
      };
    };

    //
    // Array of correlation data
    //
    /// <exclude />
    [Serializable]
    public class CorrelationArray
    {
      /// <exclude />
      public CorrelationData[] Data;
      /// <exclude />
      public double[] Weights;
      /// <exclude />
      public BaseCorrelationParam Params;
    };


    // data
    /// <exclude />
    public string Name;
    /// <exclude />
    public BasketType Type;
    /// <exclude />
    public bool RescaleStrikes;
    /// <exclude />
    public bool CheckRefinance;
    /// <exclude />
    public int BasketSize;
    /// <exclude />
    public string PortfolioStart;
    /// <exclude />
    public string AsOf;
    /// <exclude />
    public string Settle;
    /// <exclude />
    public string Maturity;
    /// <exclude />
    public string[] CreditNames;
    /// <exclude />
    public double[] Principals;
    /// <exclude />
    public CopulaType CopulaType;
    /// <exclude />
    public int DfCommon;
    /// <exclude />
    public int DfIdiosyncratic;
    /// <exclude />
    public CorrelationData Correlation;
    /// <exclude />
    public int StepSize;
    /// <exclude />
    public TimeUnit StepUnit;
    /// <exclude />
    public double[] LossLevels;
    /// <exclude />
    public int QuadraturePoints;
    /// <exclude />
    public double GridSize;
    /// <exclude />
    public int SimulationRuns;
    /// <exclude />
    public Tranche[] Tranches;
    /// <exclude />
    public double[] TrancheNotionals;
    /// <exclude />
    public DiscountData DiscountData;
    /// <exclude />
    public CreditData CreditData;
    /// <exclude />
    public double[] SubAps;
    /// <exclude />
    public double[] SubDps;
    /// <exclude />
    public bool CrossSubordination;
    /// <exclude />
    public Index IndexData;
    /// <exclude />
    public TrancheQuotes CDOTrancheQuotes;
    /// <exclude />
    public bool ConsistentCreditSensitivity;

    private CorrelationObject correlationObject_;
  }; // class BasketData

}
