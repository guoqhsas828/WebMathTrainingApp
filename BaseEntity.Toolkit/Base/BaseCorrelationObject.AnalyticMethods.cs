/*
 * BaseCorrelationObject.cs
 *
 * Base class for all the base correlation objects
 *
 *  . All rights reserved.
 *
 */
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;
namespace BaseEntity.Toolkit.Base
{

  public abstract partial class BaseCorrelationObject : CorrelationObject
  {
    #region SemiAnalytic derivatives methods
    /// <summary>
    /// Computes derivatives of the given pricing measure of the cdo w.r.t correlation(correlation) at correlation by finite difference
    /// </summary>
    ///<param name="correlation">factor level</param>
    /// <param name="pricer">SyntheticCDOPricer object (must have a SemiAnalyticBasketPricer inside)</param>
    /// <param name="fd">Overwritten by first derivative</param>
    /// <param name="sd">Overwritten by second derivative</param>
    private static void PvCorrelationDerivative(double correlation, SyntheticCDOPricer pricer, out double fd, out double sd)
    {
      double factor = Math.Sqrt(correlation);
      double h = 1e-4;
      bool shiftRight = factor - h < 0;
      bool shiftLeft = factor + h > 1;
      double xm = shiftRight? factor : shiftLeft? factor - 2*h : factor - h;
      double x = shiftRight? factor + h : shiftLeft ? factor - h : factor;
      double xp = shiftRight? factor + 2*h : shiftLeft? factor : factor + h;
      pricer.Basket.SetFactor(x);
      double pv = pricer.ProductPv() / pricer.Notional;
      pricer.Basket.SetFactor(xp);
      double pvp = pricer.ProductPv() / pricer.Notional;
      pricer.Basket.SetFactor(xm);
      double pvm = pricer.ProductPv()/pricer.Notional;
      double ffd = (pvp - pvm)/(2*h);
      double fsd = (pvp - 2*pv + pvm)/(h*h);
      fd = 0.5 / x * ffd;
      sd = 0.25 * (fsd / (x*x) - ffd / (x*x*x));
      pricer.Basket.SetFactor(factor);
      pricer.Reset();
    }


    /// <summary>
    /// Performs the compositon of the derivative of PV w.r.t correlation and derivatives of correlation w.r.t underlying curve tenors
    /// </summary>
    /// <param name="survivalCurves">Underlying survival curves</param>
    /// <param name="fd">First derivative of PV w.r.t correlation</param>
    /// <param name="sd">Second derivative of PV w.r.t correlation</param>
    /// <param name="corrDers">Derivatives of correlation w.r.t underlying curve tenors</param>
    /// <param name="retVal"> retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate</param>
    private static void ComposeWithPvDers(SurvivalCurve[] survivalCurves, double fd, double sd, double[] corrDers, double[] retVal)
    {
      int idx = 0;
      for (int i = 0; i < survivalCurves.Length; i++)
      {
        int len = survivalCurves[i].Count;
        double[] gradF = new double[len];
        for (int j = 0; j < len; j++)
        {
          gradF[j] = corrDers[idx];
          retVal[idx] = fd * gradF[j];
          idx++;
        }
        for (int j = 0; j < len; j++)
        {
          for (int k = 0; k <= j; k++)
          {
            retVal[idx] = sd * gradF[j] * gradF[k] + fd * corrDers[idx];
            idx++;
          }
        }
        retVal[idx] = fd * corrDers[idx] + 0.5 * sd * corrDers[idx] * corrDers[idx];
        idx++;
        retVal[idx] = fd * corrDers[idx];
        idx++;
      }
    }


    /// <summary>
    /// Compute derivatives of the equity tranche correlation arising from 
    /// a change in the underlying survival curve ordinates, default events and change in recovery via the strike maps
    /// </summary>
    ///<param name="basketPricer">BasketPricer object</param>
    ///<param name="discountCurve">Discount curve object</param>
    ///<param name="cdo">Cdo specifications</param>
    /// <param name="retVal"> retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate</param>
    ///<remarks>The derivatives of the loss distributions(inside bp and bpp) at the appropriate levels should be initialized prior to calling this function for efficiency</remarks>
    public virtual double CorrelationDerivatives(BasketPricer basketPricer, DiscountCurve discountCurve, SyntheticCDO cdo, double[] retVal)
    {
      throw new NotImplementedException(String.Format("SemiAnalytic derivatives of correlation are not implemented for type {0}", this.ToString()));
    }


    /// <summary>
    /// Compute portion of the derivatives of the PV of the underlying CDO w.r.t underlying curve data points attributable to 
    /// rescaling of the strike
    /// </summary>
    ///<param name="cdoPricer">Synthetic CDO pricer object</param>
    /// <param name="retVal"> retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate</param>
    internal void RescaleStrikeDerivatives(SyntheticCDOPricer cdoPricer, double[] retVal)
    {
      BaseCorrelationBasketPricer basketPricer = (BaseCorrelationBasketPricer)cdoPricer.Basket;
      double trancheWidth = cdoPricer.CDO.Detachment - cdoPricer.CDO.Attachment;
      if (cdoPricer.CDO.Attachment <= 1e-8)
      {
        double[] res = new double[retVal.Length];
        BasketPricer bp = basketPricer.CreateDetachmentBasketPricer(true);
        double fd, sd;
        double correlation = CorrelationDerivatives(bp, cdoPricer.DiscountCurve, cdoPricer.CDO, res);
        SyntheticCDOPricer pricer = cdoPricer.Substitute(cdoPricer.CDO, bp, cdoPricer.Notional, true);
        PvCorrelationDerivative(correlation,pricer, out fd, out sd);
        ComposeWithPvDers(bp.SurvivalCurves, fd, sd, res, retVal);
        return;
      }
      else
      {
        SyntheticCDOPricer pricer = null;
        double[][] resOld = new double[2][];
        double[][] resNew = new double[2][];
        SyntheticCDO[] cdos = new SyntheticCDO[2];
        BasketPricer bp = basketPricer.CreateDetachmentBasketPricer(true);
        for (int i = 0; i < 2; i++)
        {
          cdos[i] = (SyntheticCDO)cdoPricer.CDO.Clone();
          resOld[i] = new double[retVal.Length];
          resNew[i] = new double[retVal.Length];
        }
        cdos[0].Detachment = cdos[0].Attachment;
        cdos[0].Attachment = 0.0;
        cdos[1].Attachment = 0.0;
        for (int i = 0; i < 2; i++)
        {
          double fd, sd;
          double corr = CorrelationDerivatives(bp, cdoPricer.DiscountCurve, cdos[i], resOld[i]);
          pricer = cdoPricer.Substitute(cdos[i], bp, cdoPricer.Notional, true);
          PvCorrelationDerivative(corr, pricer, out fd, out sd);
          fd *= cdos[i].Detachment;
          sd *= cdos[i].Detachment;
          ComposeWithPvDers(bp.SurvivalCurves, fd, sd, resOld[i], resNew[i]);
        }
        for (int i = 0; i < retVal.Length; i++)
          retVal[i] = (resNew[1][i] - resNew[0][i]) / trancheWidth;
      }
    }
    #endregion
  }
}