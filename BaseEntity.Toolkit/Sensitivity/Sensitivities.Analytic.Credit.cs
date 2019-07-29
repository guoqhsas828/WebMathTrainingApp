
using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;
namespace BaseEntity.Toolkit.Sensitivity
{

  /// <summary>
  /// Analytic sensitivities    
  /// </summary>
  public static class CreditAnalyticSensitivities
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CreditAnalyticSensitivities));

    /// <summary>
    /// Make computations thread safe in case pricers share the same BasketPricer object by initializing shared states (CurveArrays containing derivatives info)
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    private static void MakeThreadSafe(SyntheticCDOPricer[] pricers)
    {
      if (pricers.Length <= 1) return;
      var basketPricers = new List<BasketPricer>();
      var detach = new List<UniqueSequence<double>>();
      basketPricers.Add(pricers[0].Basket);
      detach.Add(new UniqueSequence<double>(pricers[0].CDO.Attachment, pricers[0].CDO.Detachment));
      for (int i = 0; i < pricers.Length; i++)
      {
        bool alreadyThere = false;
        for (int j = 0; j < basketPricers.Count; j++)
        {
          if (pricers[i].Basket == basketPricers[j])
          {
            detach[j].Add(pricers[i].CDO.Attachment, pricers[i].CDO.Detachment);
            alreadyThere = true;
            break;
          }
        }
        if (!alreadyThere)
        {
          basketPricers.Add(pricers[i].Basket);
          detach.Add(new UniqueSequence<double>(pricers[i].CDO.Attachment, pricers[i].CDO.Detachment));
        }
      }
      if (basketPricers.Count != pricers.Length)
      {
        Parallel.For(0, basketPricers.Count, i => basketPricers[i].ComputeAndSaveSemiAnalyticSensitivities(detach[i]));
      }
    }


    /// <summary>
    /// Returns a data table containing tenor-wise Pv deltas and gammas, vods and recovery deltas 
    /// (survival probability is NOT recalibrated)
    /// </summary>
    /// <param name="pricers">Array of pricers that implement the IAnalyticSensitivitiesProvider interface</param>
    ///<param name="rescaleStrikes">True to rescale strikes</param>
    /// <returns>Array of DerivativeCollection objects containing analytic derivative results</returns>
    public static DerivativeCollection[] BasketSemiAnalyticSensitivities(SyntheticCDOPricer[] pricers, bool[] rescaleStrikes)//For basket pricers
    {
      //Pre-initialize semi-analytic sensitivities for each underlying basket, so as to make parallel computation safe
      bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(pricers, rescaleStrikes);
      DerivativeCollection[] retVal = null;
      try
      {
        var retValY = new DerivativeCollection[pricers.Length];
        retVal = new DerivativeCollection[pricers.Length];
        //Compute raw derivatives wrt curve ordinates 
        if (pricers.Length > 1)
          MakeThreadSafe(pricers); //make thread safe in case pricers share the same basket
        Parallel.For(0, pricers.Length, delegate(int i)
                                          {
                                            var p = pricers[i] as IAnalyticDerivativesProvider;
                                            if (p == null)
                                            {
                                              logger.ErrorFormat(
                                                "Pricer {0} does not support semi-analytic sensitivities. Calculations are not performed for pricer {0}",
                                                pricers[i].Product.Description);
                                              retValY[i] = new DerivativeCollection();
                                              return;
                                            }
                                            retValY[i] = (DerivativeCollection) p.GetDerivativesWrtOrdinates();
                                          });
        //Convert them to derivatives w.r.t quotes
        for (int i = 0; i < pricers.Length; i++)
        {
          retVal[i] = new DerivativeCollection(retValY[i].CurveCount);
          DerivativeCollection derivativesWrtOrdinates = retValY[i];
          for (int j = 0; j < derivativesWrtOrdinates.CurveCount; j++)
          {
            var der = derivativesWrtOrdinates.GetDerivatives(j);
            var derRes = new DerivativesWrtCurve(der.ReferenceCurve);
            AnalyticSensitivities.CalcDerivativesWrtTenorQuotes(der, derRes);
            retVal[i].Add(derRes);
          }
          retVal[i].Name = pricers[i].Product.Description;
        }
        
      }
      finally
      {
        Sensitivities.ResetRescaleStrikes(pricers, rescaleStrikesSaved);
      }
      return retVal;
    }



    /// <summary>
    /// Compute semi-analytic derivatives with respect to quote tenors for an array of arbitrary cashflow pricers
    /// </summary>
    /// <param name="pricers">Cashflow pricers</param>
    ///<param name="rescaleStrikes">True if want to rescale tranche strikes (applicable to basket pricers</param>
    /// <returns>Collection of cached derivatives of the Pv of the underlying products with respect to each underlying curve</returns>
    public static DerivativeCollection[] SemiAnalyticSensitivities(IPricer[] pricers, bool[] rescaleStrikes)
    {
      //separate basket pricers from regular cash flow pricers
      var basketPricers = new List<SyntheticCDOPricer>();
      var cashflowPricers = new List<IPricer>();
      var basketPos = new List<int>();
      var cashflowPos = new List<int>();
      for (int i = 0; i < pricers.Length; i++)
      {
        if (pricers[i] is SyntheticCDOPricer)
        {
          basketPricers.Add((SyntheticCDOPricer)pricers[i]);
          basketPos.Add(i);
        }
        else
        {
          cashflowPricers.Add(pricers[i]);
          cashflowPos.Add(i);
        }
      }
      var bp = basketPricers.ToArray();
      var cp = cashflowPricers.ToArray();
      var retValBasket = (bp.Length > 0)? BasketSemiAnalyticSensitivities(bp, rescaleStrikes) : new DerivativeCollection[0];
      var retVal = new DerivativeCollection[cp.Length];
      for (int i = 0; i < cp.Length; i++)
      {
        var p = cp[i] as IAnalyticDerivativesProvider;
        if (p == null)
        {
          logger.ErrorFormat("Pricer {0} does not support semi-analytic sensitivities. Calculations are not performed for pricer {0}", pricers[i].Product.Description);
          retVal[i] = new DerivativeCollection();
          continue;
        }
        var derivativesWrtOrdinates = (DerivativeCollection)p.GetDerivativesWrtOrdinates();
        retVal[i] = new DerivativeCollection(derivativesWrtOrdinates.CurveCount);
        for (int j = 0; j < derivativesWrtOrdinates.CurveCount; j++)
        {
          var der = derivativesWrtOrdinates.GetDerivatives(j);
          var derRes = new DerivativesWrtCurve(der.ReferenceCurve);
          AnalyticSensitivities.CalcDerivativesWrtTenorQuotes(der, derRes);
          retVal[i].Add(derRes);
        }
        retVal[i].Name = cp[i].Product.Description;
      }
      var res = new DerivativeCollection[pricers.Length];
      for (int i = 0; i < basketPricers.Count; i++)
      {
        res[basketPos[i]] = retValBasket[i];
      }
      for (int i = 0; i < cashflowPricers.Count; i++)
      {
        res[cashflowPos[i]] = retVal[i];
      }
      return res;
    }



    /// <summary>
    /// Fills up a data table with by tenor sensitivities with respect to individual name spreads
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    ///<param name="bumpType">Bump type</param>
    /// <param name="bump">Size of the bump for spread perturbation (as real number)</param>
    /// <param name="bumpRelative">True if the bump size is relative (percentage)</param>
    /// <param name="calcHedges">True to compute hedge ratios with respect to curve tenors</param>
    ///<param name="hedgeTenor">If bump is parallel, compute hedge delta wrt given hedge tenor, otherwise compute them all</param>
    /// <param name="rescaleStrikes">True if want to rescale strikes</param>
    /// <returns>
    ///The output table consists the following columns:
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve tenor (or all for parallel shift)</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma </description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedges"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedges"/> is true)</description></item>
    ///       </list>
    /// </returns>
    public static DataTable SemiAnalyticSpreadSensitivities(IPricer[] pricers, double bump, BumpType bumpType, bool bumpRelative, bool calcHedges, string hedgeTenor, bool[] rescaleStrikes)
    {
      if (!(bumpType == BumpType.Parallel || bumpType == BumpType.ByTenor))
        throw new ArgumentException(String.Format("SemiAnalytic sensitivities are not supported for bump type {0}", Enum.GetName(typeof(BumpType), bumpType)));
      Timer timer = new Timer();
      timer.start();
      DerivativeCollection[] derivativeCollection = SemiAnalyticSensitivities(pricers, rescaleStrikes);
      timer.stop();
      var dataTable = new DataTable("Spread Sensitivities Report");
      dataTable.Columns.Add(new DataColumn("Category", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));
      if (bumpType != BumpType.Parallel)
        dataTable.Columns.Add(new DataColumn("Curve Tenor", typeof(string)));
      if (calcHedges)
      {
        dataTable.Columns.Add(new DataColumn("Hedge Tenor", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Hedge Delta", typeof(double)));
        dataTable.Columns.Add(new DataColumn("Hedge Notional", typeof(double)));
      }
      if (pricers == null || pricers.Length == 0)
      {
        logger.InfoFormat("Completed spread sensitivity in {0}s", timer.getElapsed());
        dataTable.ExtendedProperties.Add("Elapsed", timer.getElapsed());
        return dataTable;
      }
      for (int i = 0; i < derivativeCollection.Length; i++)
      {
        var pricer = pricers[i] as PricerBase;
        if(pricer == null)
          continue;
        double notional = pricer.Notional;
        for (int j = 0; j < derivativeCollection[i].CurveCount; j++)
        {
          var derivativesWrtCurve = derivativeCollection[i].GetDerivatives(j);
          var curve = derivativesWrtCurve.ReferenceCurve;
          int k = curve.Tenors.Count;
          DerivativeCollection[] hedges = null;
          var bp = new double[k];
          for (int n = 0; n < k; n++)
            bp[n] = bumpRelative ? bump * curve.Tenors[n].OriginalQuote.Value : bump * 1e-4;
          if (bumpType == BumpType.Parallel)
          {
            double dv01 = derivativesWrtCurve.ComputeSensitivity(bp);
            double gamma = derivativesWrtCurve.SecondOrderOnly(bp);
            DataRow dataRow = dataTable.NewRow();
            dataRow["Category"] = derivativesWrtCurve.ReferenceCurve.Category;
            dataRow["Element"] = derivativesWrtCurve.ReferenceCurve.Name;
            dataRow["Pricer"] = derivativeCollection[i].Name;
            dataRow["Delta"] = dv01 * notional;
            dataRow["Gamma"] = gamma * notional;
            if (calcHedges)
            {
              string[] hedgeNames;
              int[] hedgeP;
              var underlyings = AnalyticSensitivities.HedgingInstruments(curve, pricers[i], hedgeTenor, out hedgeNames, out hedgeP);
              if (underlyings != null)
              {
                hedges = SemiAnalyticSensitivities(underlyings, null);
                double hedgesdv01 = hedges[0].GetDerivatives(0).ComputeSensitivity(bp);
                dataRow["Hedge Tenor"] = hedgeNames[0];
                dataRow["Hedge Delta"] = (Math.Abs(hedgesdv01) < 1e-8) ? 0 : hedgesdv01 * 1e6;
                dataRow["Hedge Notional"] = (Math.Abs(hedgesdv01) < 1e-8) ? 0 : dv01 / hedgesdv01 * notional;
              }
              else
              {
                dataRow["Hedge Tenor"] = hedgeTenor;
                dataRow["Hedge Delta"] = 0.0;
                dataRow["Hedge Notional"] = 0.0;
              }
            }
            dataTable.Rows.Add(dataRow);
          }
          else
          {
            string[] hedgeNames = null;
            int[] hedgeP = null;
            if (calcHedges)
            {

              var underlyings = AnalyticSensitivities.HedgingInstruments(curve, pricers[i], hedgeTenor, out hedgeNames, out hedgeP);
              if (underlyings != null)
                hedges = SemiAnalyticSensitivities(underlyings, null);
            }
            for (int l = 0; l < k; l++)
            {
              DataRow dataRow = dataTable.NewRow();
              double delta = derivativesWrtCurve.Gradient[l];
              double gamma = derivativesWrtCurve.Hessian[l * (l + 1) / 2 + l];
              dataRow["Category"] = derivativesWrtCurve.ReferenceCurve.Category;
              dataRow["Element"] = derivativesWrtCurve.ReferenceCurve.Name;
              dataRow["Curve Tenor"] = derivativesWrtCurve.ReferenceCurve.Tenors[l].Name;
              dataRow["Pricer"] = derivativeCollection[i].Name;
              dataRow["Delta"] = delta * bp[l] * notional;
              dataRow["Gamma"] = gamma * bp[l] * bp[l] * notional;
              if (calcHedges)
              {
                if (hedges != null)
                {
                  int hedgePos = (hedges.Length == 1) ? 0 : l;
                  int pos = (hedges.Length == 1) ? hedgeP[0] : l;
                  double deltaHedge;
                  if (hedges.Length == 1)
                    deltaHedge = (pos == l) ? hedges[hedgePos].GetDerivatives(0).Gradient[pos] : 0;
                  else
                    deltaHedge = hedges[hedgePos].GetDerivatives(0).Gradient[pos];
                  dataRow["Hedge Tenor"] = derivativesWrtCurve.ReferenceCurve.Tenors[l].Name;
                  dataRow["Hedge Delta"] = (Math.Abs(deltaHedge) < 1e-8) ? 0 : deltaHedge * 1e2;
                  dataRow["Hedge Notional"] = (Math.Abs(deltaHedge) < 1e-8) ? 0 : delta / deltaHedge * notional;
                }
              }
              dataTable.Rows.Add(dataRow);
            }
          }
        }
      }
      logger.InfoFormat("Completed spread sensitivity in {0}s", timer.getElapsed());
      dataTable.ExtendedProperties.Add("Elapsed", timer.getElapsed());
      return dataTable;
    }


    /// <summary>
    /// Fills up a data table with default sensitivities 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    ///<param name="calcHedges">True to calculate hedging ratios</param>
    /// <param name="hedgeTenor">Name of the hedge product</param>
    /// <param name="rescaleStrikes">True if want to rescale strikes</param>
    /// <returns>
    ///The output table consists the following columns:
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>VOD</term><description>Value of default</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedges"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedges"/> is true)</description></item>
    ///       </list>
    /// </returns>
    public static DataTable SemiAnalyticVOD(IPricer[] pricers, bool calcHedges, string hedgeTenor, bool[] rescaleStrikes)
    {
      Timer timer = new Timer();
      timer.start();
      var derivativeCollection = SemiAnalyticSensitivities(pricers, rescaleStrikes);
      timer.stop();
      DerivativeCollection[] hedges = null;
      var dataTable = new DataTable("Default Sensitivities Report");
      dataTable.Columns.Add(new DataColumn("Category", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
      if (calcHedges)
      {
        dataTable.Columns.Add(new DataColumn("Hedge Tenor", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Hedge Delta", typeof(double)));
        dataTable.Columns.Add(new DataColumn("Hedge Notional", typeof(double)));
      }
      if (pricers == null || pricers.Length == 0)
      {
        logger.InfoFormat("Completed VOD sensitivity in {0}s", timer.getElapsed());
        dataTable.ExtendedProperties.Add("Elapsed", timer.getElapsed());
        return dataTable;
      }
      for (int i = 0; i < derivativeCollection.Length; i++)
      {
        var pricer = pricers[i] as PricerBase;
        if (pricer == null)
          continue;
        double notional = pricer.Notional;
        for (int j = 0; j < derivativeCollection[i].CurveCount; j++)
        {
          var derivativesWrtCurve = derivativeCollection[i].GetDerivatives(j);
          var curve = derivativesWrtCurve.ReferenceCurve;
          double vod = derivativesWrtCurve.Vod;
          DataRow dataRow = dataTable.NewRow();
          dataRow["Category"] = derivativesWrtCurve.ReferenceCurve.Category;
          dataRow["Element"] = derivativesWrtCurve.ReferenceCurve.Name;
          dataRow["Pricer"] = derivativeCollection[i].Name;
          dataRow["Delta"] = vod * notional;
          if (calcHedges)
          {
            string[] hedgeNames;
            int[] hedgeP;
            var underlyings = AnalyticSensitivities.HedgingInstruments(curve, pricers[i], hedgeTenor, out hedgeNames, out hedgeP);
            if (underlyings != null)
            {
              hedges = SemiAnalyticSensitivities(underlyings, null);
              double hedgesVod = hedges[0].GetDerivatives(0).Vod;
              dataRow["Hedge Tenor"] = underlyings[0].Product.Description;
              dataRow["Hedge Notional"] = (Math.Abs(hedgesVod) < 1e-8) ? 0 : vod / hedgesVod * notional;
              dataRow["Hedge Delta"] = hedgesVod * 1e6;
            }
          }
          dataTable.Rows.Add(dataRow);
        }

      }
      logger.InfoFormat("Completed VOD sensitivity in {0}s", timer.getElapsed());
      dataTable.ExtendedProperties.Add("Elapsed", timer.getElapsed());
      return dataTable;
    }

    /// <summary>
    /// Fills up a data table with default sensitivities 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    ///<param name="bump">Absolute bump size(in percentage)</param>
    /// <param name="rescaleStrikes">True if want to rescale strikes</param>
    /// <returns>
    ///The output table consists the following columns:
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>RecoveryDelta</term><description>Delta wrt mean quoted recovery</description></item>
    ///     </list>
    /// </returns>
    public static DataTable SemiAnalyticRecoveryDelta(IPricer[] pricers, double bump, bool[] rescaleStrikes)
    {
      Timer timer = new Timer();
      timer.start();
      var derivativeCollection = SemiAnalyticSensitivities(pricers, rescaleStrikes);
      timer.stop();
      var dataTable = new DataTable("Default Sensitivities Report");
      dataTable.Columns.Add(new DataColumn("Category", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));
      if (pricers == null || pricers.Length == 0)
      {
        logger.InfoFormat("Completed recovery sensitivity in {0}s", timer.getElapsed());
        dataTable.ExtendedProperties.Add("Elapsed", timer.getElapsed());
        return dataTable;
      }
      for (int i = 0; i < derivativeCollection.Length; i++)
      {
        var pricer = pricers[i] as PricerBase;
        if (pricer == null)
          continue;
        double notional = pricer.Notional;
        for (int j = 0; j < derivativeCollection[i].CurveCount; j++)
        {
          DerivativesWrtCurve derivativesWrtCurve = derivativeCollection[i].GetDerivatives(j);
          double rd = derivativesWrtCurve.RecoveryDelta;
          DataRow dataRow = dataTable.NewRow();
          dataRow["Category"] = derivativesWrtCurve.ReferenceCurve.Category;
          dataRow["Element"] = derivativesWrtCurve.ReferenceCurve.Name;
          dataRow["Pricer"] = derivativeCollection[i].Name;
          dataRow["Delta"] = rd * notional * bump;
          dataRow["Gamma"] = 0.0;
          dataTable.Rows.Add(dataRow);
        }
      }
      logger.InfoFormat("Completed recovery sensitivity in {0}s", timer.getElapsed());
      dataTable.ExtendedProperties.Add("Elapsed", timer.getElapsed());
      return dataTable;
    }
  }
}
