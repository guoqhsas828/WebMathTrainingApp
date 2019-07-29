using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;

using BaseEntity.Toolkit.Calibrators.BaseCorrelation;


namespace BaseEntity.Toolkit.Base
{
  public partial class BaseCorrelation : BaseCorrelationObject, ICorrelationBump
  {
    #region Semianalytic sensitivities methods
    //ratio of derivative 


    private static void Fill(double val, double[] retVal)
    {
      if (retVal == null)
        return;
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = val;
    }


    private static void MultiplyByScalar(SurvivalCurve[] curves, double a, double[] fDers, double[] retVal)
    {
      if (fDers == null)
      {
        Fill(0.0, retVal);
        return;
      }
      for (int i = 0; i < fDers.Length; i++)
        retVal[i] = a * fDers[i];
    }


    private static void ProductDerivatives(SurvivalCurve[] curves, double f, double g, double[] fDers, double[] gDers, double[] retVal)
    {
      if (fDers == null && gDers == null)
      {
        Fill(0, retVal);
      }
      if (fDers == null)
      {
        MultiplyByScalar(curves, f, gDers, retVal);
        return;
      }
      if (gDers == null)
      {
        MultiplyByScalar(curves, g, fDers, retVal);
        return;
      }
      int idx = 0;
      for (int i = 0; i < curves.Length; i++)
      {
        int len = curves[i].Count;
        double[] gGrad = new double[len];
        double[] fGrad = new double[len];
        for (int k = 0; k < len; k++)
        {
          gGrad[k] = gDers[idx];
          fGrad[k] = fDers[idx];
          retVal[idx] = fGrad[k] * g + f * gGrad[k]; //deltas
          idx++;
        }
        for (int k = 0; k < len; k++)
        {
          for (int j = 0; j <= k; j++)
          {
            retVal[idx] = fDers[idx] * g + (gGrad[k] * fGrad[j] + gGrad[j] * fGrad[k]) +
                          +f * gDers[idx];
            idx++; //gammas
          }
        }
        retVal[idx] = (f + fDers[idx]) * (g + gDers[idx]) - f * g; //vod
        idx++;
        retVal[idx] = fDers[idx] * g + f * gDers[idx];//recovery
        idx++;
      }
    }


    private static void RatioDerivatives(SurvivalCurve[] curves, double num, double den, double[] numDers, double[] denDers, double[] retVal)
    {
      if (numDers == null && denDers == null)
        Fill(0, retVal);
      if (denDers == null)
      {
        ProductDerivatives(curves, num, 1.0 / den, numDers, null, retVal);
        return;
      }
      double d = den;
      double d2 = d * den;
      double d3 = d2 * den;
      int idx = 0;
      if (numDers == null)
      {
        for (int i = 0; i < curves.Length; i++)
        {
          int len = curves[i].Count;
          double[] gradDen = new double[len];
          for (int k = 0; k < len; k++)
          {
            gradDen[k] = denDers[idx];
            retVal[idx] = -num / d2 * gradDen[k]; //deltas
            idx++;
          }
          for (int k = 0; k < len; k++)
          {
            for (int j = 0; j <= k; j++)
            {
              retVal[idx] = 2 * num / d3 * gradDen[j] * gradDen[k] - num / d2 * denDers[idx];
              idx++; //gammas
            }
          }
          retVal[idx] = num / (den + denDers[idx]) - num / den; //vod
          idx++;
          retVal[idx] = -num / d2 * denDers[idx];//recovery
          idx++;
        }
        return;
      }
      for (int i = 0; i < curves.Length; i++)
      {
        int len = curves[i].Count;
        double[] gradDen = new double[len];
        double[] gradNum = new double[len];
        for (int k = 0; k < len; k++)
        {
          gradDen[k] = denDers[idx];
          gradNum[k] = numDers[idx];
          retVal[idx] = gradNum[k] / d - num / d2 * gradDen[k]; //deltas
          idx++;
        }
        for (int k = 0; k < len; k++)
        {
          for (int j = 0; j <= k; j++)
          {
            retVal[idx] = numDers[idx] / d - (gradDen[k] * gradNum[j] + gradDen[j] * gradNum[k]) / d2 +
                          2 * num / d3 * gradDen[j] * gradDen[k] - num / d2 * denDers[idx];
            idx++; //gammas
          }
        }
        retVal[idx] = (num + numDers[idx]) / (den + denDers[idx]) - num / den; //vod
        idx++;
        retVal[idx] = numDers[idx] / d - num / d2 * denDers[idx];//recovery
        idx++;
      }
    }

    private SyntheticCDO CreateCDO(BasketPricer basketPricer)
    {
      // Create cdo
      Dt start = basketPricer.PortfolioStart.IsEmpty() ? basketPricer.Settle : basketPricer.PortfolioStart;
      SyntheticCDO cdo = new SyntheticCDO(
        start, basketPricer.Maturity,
        Currency.None, 0.0, // premium, not used
        DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB);
      return cdo;
    }


    //Computes derivatives of the protection Pv of a [cdo.Detach-1] tranche on 1 dollar on a tranche
    private static void BasketLossPvDerivatives(
    SyntheticCDO cdo, BasketPricer basketPricer,
    DiscountCurve discountCurve, double notional, double[] retVal)
    {
      double dp = cdo.Detachment;
      cdo = (SyntheticCDO)cdo.Clone();
      cdo.Detachment = 1.0;
      SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basketPricer, discountCurve, notional, false, null);
      pricer.ProtectionPvDerivatives(retVal);
      MultiplyByScalar(basketPricer.SurvivalCurves, notional, retVal, retVal);
    }


    //ExpectedLoss
    private double ExpectedLossDerivatives(double detach, BasketPricer basketPricer, DiscountCurve discount, double[] retVal)
    {
      double[] numDers = null;
      double[] denDers = null;
      double num = 0;
      double den = 0;
      double eLossT = 0;
      double eLoss = 0;
      double dp = detach;
      double notAdj = 1.0;
      if (strikeMethod_ == BaseCorrelationStrikeMethod.ProtectionForward ||
        strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossRatioForward ||
        strikeMethod_ == BaseCorrelationStrikeMethod.EquityProtectionForward ||
        strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossForward)
      {
        double el = 0;
        double a = 0;
        basketPricer.AdjustTrancheLevels(false, ref a, ref dp, ref el);
        notAdj = (basketPricer.TotalPrincipal - basketPricer.DefaultedPrincipal) / basketPricer.TotalPrincipal;
      }
      if (strikeMethod_ == BaseCorrelationStrikeMethod.Protection ||
        strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossRatio ||
        strikeMethod_ == BaseCorrelationStrikeMethod.EquityProtection)
      {
        eLossT = basketPricer.AccumulatedLoss(basketPricer.Maturity, 0.0, detach);
        numDers = new double[retVal.Length];
        basketPricer.AccumulatedLossDerivatives(basketPricer.Maturity, 0, detach, numDers);

      }
      if (strikeMethod_ == BaseCorrelationStrikeMethod.ProtectionForward ||
        strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossRatioForward ||
        strikeMethod_ == BaseCorrelationStrikeMethod.EquityProtectionForward)
      {
        eLossT = basketPricer.AccumulatedLoss(basketPricer.Maturity, 0.0, detach) - basketPricer.PreviousLoss;
        numDers = new double[retVal.Length];
        basketPricer.AccumulatedLossDerivatives(basketPricer.Maturity, 0, detach, numDers);
      }
      if (strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossRatio)
      {
        eLoss = basketPricer.BasketLoss(basketPricer.Settle, basketPricer.Maturity) + basketPricer.PreviousLoss;
        denDers = new double[retVal.Length];
        basketPricer.AccumulatedLossDerivatives(basketPricer.Maturity, 0, 1, denDers);
      }
      if (strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLoss || 
        strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossRatioForward)
      {
        eLoss = basketPricer.BasketLoss(basketPricer.Settle, basketPricer.Maturity);
        denDers = new double[retVal.Length];
        basketPricer.AccumulatedLossDerivatives(basketPricer.Maturity, 0, 1, denDers);
      }
      switch (strikeMethod_)
      {
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossForward:
          num = dp;
          den = eLoss;
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
          num = eLossT;
          den = eLoss;
          break;
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
          num = eLossT;
          den = notAdj;
          break;
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
          num = eLossT;
          den = dp * notAdj;
          break;
      }
      RatioDerivatives(basketPricer.SurvivalCurves, num, den, numDers, denDers, retVal);
      return num / den;
    }


    private double ProtectionPvDerivatives(double detach, BasketPricer basketPricer, DiscountCurve discount, double[] retVal)
    {
      SyntheticCDO cdo = CreateCDO(basketPricer);
      cdo.Attachment = 0.0;
      cdo.Detachment = detach;
      AdjustForForwardStrikes(ref basketPricer, ref cdo, ref strikeMethod_);

      double dp = cdo.Detachment;
      double notional = basketPricer.TotalPrincipal;
      double notAdj = 1.0;
      
      double[] dersNum = new double[retVal.Length];
      double[] dersDen = null;
      double num, den = notional;
      if (strikeMethod_ == BaseCorrelationStrikeMethod.EquityProtectionPvForward ||
        strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossPVForward ||
        strikeMethod_ == BaseCorrelationStrikeMethod.ProtectionPvForward)
      {
        double el = 0;
        double a = 0;
        basketPricer.AdjustTrancheLevels(false, ref a, ref dp, ref el);
        notAdj = (basketPricer.TotalPrincipal - basketPricer.DefaultedPrincipal) / basketPricer.TotalPrincipal;
      }
      switch (strikeMethod_)
      {
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
        case BaseCorrelationStrikeMethod.ExpectedLossPVForward:
          dersDen = new double[retVal.Length];
          den = -ProtectionPvFn.BasketLossPv(cdo, basketPricer, discount, 1.0);
          BasketLossPvDerivatives(cdo, basketPricer, discount, 1.0, dersDen);
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
          dersDen = new double[retVal.Length];
          den = ProtectionPvFn.BasketLossPv(cdo, basketPricer, discount, notional);
          BasketLossPvDerivatives(cdo, basketPricer, discount, notional, dersDen);
          break;
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
          den = notional * notAdj * dp;
          break;
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
          den = notional * notAdj;
          break;
      }
      if (strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossPV ||
          strikeMethod_ == BaseCorrelationStrikeMethod.ExpectedLossPVForward)
      {
        RatioDerivatives(basketPricer.SurvivalCurves, -dp*notAdj, den, null, dersDen, retVal);
        return Math.Abs(dp*notAdj / den);
      }
      SyntheticCDOPricer pricer;
      pricer = new SyntheticCDOPricer(cdo, basketPricer, discount, dp * notional, false, null);
      num = pricer.ProtectionPv();
      pricer.ProtectionPvDerivatives(dersNum);
      MultiplyByScalar(basketPricer.SurvivalCurves, dp * notional, dersNum, dersNum);
      RatioDerivatives(basketPricer.SurvivalCurves, num, -den, dersNum, dersDen, retVal);
      return Math.Abs(num / den);
    }


    private double SpreadDerivatives(double detach, BasketPricer basketPricer, DiscountCurve discount, double[] retVal)
    {
      bool senior = (strikeMethod_ == BaseCorrelationStrikeMethod.SeniorSpread);
      SyntheticCDO cdo = CreateCDO(basketPricer);
      if (senior)
      {
        cdo.Attachment = detach;
        cdo.Detachment = 1.0;
        if (cdo.AmortizePremium && basketPricer.NoAmortization)
        {
          basketPricer = basketPricer.Duplicate();
          basketPricer.NoAmortization = false;
        }
      }
      else
      {
        cdo.Attachment = 0.0;
        cdo.Detachment = detach;
      }
      cdo.Fee = 0;
      SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basketPricer, discount, 1.0, null);
      double num = -pricer.ProtectionPv();
      double den = pricer.FlatFeePv(pricer.Settle, 1.0);
      double[] numDer = new double[retVal.Length];
      double[] denDer = new double[retVal.Length];
      pricer.ProtectionPvDerivatives(numDer);
      for (int i = 0; i < numDer.Length; i++)
      {
        numDer[i] = -numDer[i];
      }
      pricer.FeePvDerivatives(pricer.Settle, 1.0, denDer);
      RatioDerivatives(basketPricer.SurvivalCurves, num, den, numDer, denDer, retVal);
      return num / den;
    }


    /// <summary>
    /// Compute semi-analytical derivatives of the strike maps w.r.t underlying curve ordinates, vods and recovery deltas
    /// </summary>
    /// <param name="basketPricer">Underlying basket pricer</param>
    /// <param name="discount">Discount curve</param>
    /// <param name="cdo">Cdo product</param>
    /// <param name="retVal"> retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the (raw) survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the (raw) survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate</param>
    /// <returns>Strike value</returns>
    private double StrikeDerivatives(BasketPricer basketPricer, DiscountCurve discount, SyntheticCDO cdo, double[] retVal)
    {
      double strike = 0;
      switch (strikeMethod_)
      {
        case BaseCorrelationStrikeMethod.Unscaled:
          strike = cdo.Detachment;
          Fill(0, retVal);
          break;
        case BaseCorrelationStrikeMethod.UnscaledForward:
          double el = 0;
          double a = 0;
          double dp = cdo.Detachment;
          basketPricer.AdjustTrancheLevels(false, ref a, ref dp, ref el);
          strike = dp;
          Fill(0, retVal);
          break;
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPV:
        case BaseCorrelationStrikeMethod.ExpectedLossPVForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
          strike = ProtectionPvDerivatives(cdo.Detachment, basketPricer, discount, retVal);
          break;
        case BaseCorrelationStrikeMethod.ExpectedLoss:
        case BaseCorrelationStrikeMethod.ExpectedLossForward:
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
          strike = ExpectedLossDerivatives(cdo.Detachment, basketPricer, discount, retVal);
          break;
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
          strike = SpreadDerivatives(cdo.Detachment, basketPricer, discount, retVal);
          break;
        default:
          throw new NotSupportedException(String.Format("Derivatives of map {0} not supported in release 9.3", 
            Enum.GetName(typeof(BaseCorrelationStrikeMethod), strikeMethod_)));

      }
      return strike;
    }

    /// <summary>
    /// Compute numerically derivatives of base correlation interpolating function w.r.t strike
    /// </summary>
    /// <param name="strike">Strike</param>
    /// <param name="fd">First derivative of the correlation function w.r.t strike at strike</param>
    /// <param name="sd">Second derivative of the correlation function w.r.t strike at strike</param>
    private void CorrelationStrikeDerivative(double strike, out double fd, out double sd)
    {
      if(this.InterpMethod == InterpMethod.Linear || this.InterpMethod == InterpMethod.Weighted )
        logger.Debug(String.Format("{0} interpolation is non smooth. Precision loss might occur at BaseCorrelation curve abscissas", Enum.GetName(typeof(InterpMethod), InterpMethod)));
      if(this.ExtrapMethod != ExtrapMethod.Smooth)
        logger.Debug(String.Format("{0} extrapolation is non smooth. Precision loss might occur at BaseCorrelation curve nodes 0 and {1}", Enum.GetName(typeof(ExtrapMethod), ExtrapMethod), this.Strikes.Length-1));
      double h = 1e-4;
      double rhom = GetCorrelation(strike - h);
      double rho = GetCorrelation(strike);
      double rhop = GetCorrelation(strike + h);
      fd = (rhop - rhom) / (2 * h);
      sd = (rhop - 2 * rho + rhom) / (h * h);
    }


    /// <summary>
    /// Compute numerically derivatives of strike map w.r.t correlation at the detachment point of the given CDO
    /// </summary>
    /// <param name="correlation">Factor level</param>
    /// <param name="basketPricer">Underlying basket pricer</param>
    /// <param name="cdo">SyntheticCDO</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="s">Overwritten by the strike at correlation</param>
    /// <param name="fd">Overwritten by first derivative of the strike at correlation</param>
    /// <param name="sd">Overwritten by second derivative of the strike at correlation</param>

    private void StrikeCorrelationDerivative(double correlation, BasketPricer basketPricer, SyntheticCDO cdo, DiscountCurve discountCurve, out double s, out double fd, out double sd)
    {
      double att = cdo.Attachment;
      double det = cdo.Detachment;
      cdo = CreateCDO(basketPricer);
      cdo.Attachment = att;
      cdo.Detachment = det;
      if (StrikeMethod == BaseCorrelationStrikeMethod.Unscaled)
      {
        s = cdo.Detachment;
        fd = sd = 0;
        return;
      }
      if (StrikeMethod == BaseCorrelationStrikeMethod.UnscaledForward)
      {
        double el = 0.0;
        double a = cdo.Attachment;
        double d = cdo.Detachment;
        basketPricer.AdjustTrancheLevels(false, ref a, ref d, ref el);
        s = d;
        fd = sd = 0;
        return;
      }
      if (StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLoss || StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLossForward)
      {
        s = basketPricer.BasketLoss(basketPricer.Settle, cdo.Maturity);
        if(StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLossForward)
        {
          double a = cdo.Attachment;
          double d = cdo.Detachment;
          double el = 0;
          double mult = (basketPricer.TotalPrincipal - basketPricer.DefaultedPrincipal)/basketPricer.TotalPrincipal;
          basketPricer.AdjustTrancheLevels(false, ref a, ref d, ref el);
          s = d*mult/s;
          fd = sd = 0;
          return;
        }
        s = cdo.Detachment / s;
        fd = sd = 0;
        return;
      }
      if (StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLossPV || StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLossPVForward)
      {
        s = basketPricer.BasketLossPv(basketPricer.Settle, cdo.Maturity, discountCurve);
        if (StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLossPVForward)
        {
          double a = cdo.Attachment;
          double d = cdo.Detachment;
          double el = 0;
          double mult = (basketPricer.TotalPrincipal - basketPricer.DefaultedPrincipal) / basketPricer.TotalPrincipal;
          basketPricer.AdjustTrancheLevels(false, ref a, ref d, ref el);
          s = d * mult / s;
          fd = sd = 0;
          return;
        }
        
        s = cdo.Detachment / s;
        fd = sd = 0;
        return;
      }
      StrikeFn strikeFunc = null;
      switch (strikeMethod_)
      {
        case BaseCorrelationStrikeMethod.EquityProtectionPv:
        case BaseCorrelationStrikeMethod.EquityProtectionPvForward:
        case BaseCorrelationStrikeMethod.ProtectionPv:
        case BaseCorrelationStrikeMethod.ProtectionPvForward:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossPvRatioForward:
          strikeFunc = new ProtectionPvFn(this.Interp, true, this.Strikes, this.Correlations, this.StrikeMethod, cdo,
                                    basketPricer, discountCurve);
          break;
        case BaseCorrelationStrikeMethod.ExpectedLossRatio:
        case BaseCorrelationStrikeMethod.ExpectedLossRatioForward:
        case BaseCorrelationStrikeMethod.Protection:
        case BaseCorrelationStrikeMethod.ProtectionForward:
        case BaseCorrelationStrikeMethod.EquityProtection:
        case BaseCorrelationStrikeMethod.EquityProtectionForward:
          strikeFunc = new ProtectionFn(this.Interp, true, this.Strikes, this.Correlations, this.StrikeMethod, cdo.Detachment,
                                    basketPricer);
          break;
        case BaseCorrelationStrikeMethod.EquitySpread:
        case BaseCorrelationStrikeMethod.SeniorSpread:
          strikeFunc = new SpreadFn(this.Interp, true, this.Strikes, this.Correlations, cdo, basketPricer, discountCurve, StrikeMethod == BaseCorrelationStrikeMethod.SeniorSpread);
          break;
        default:
          throw new NotSupportedException(String.Format("Correlation derivatives of map {0} not supported in release 9.3", Enum.GetName(typeof(BaseCorrelationStrikeMethod), strikeMethod_)));

      }
      double h = 1e-4;
      double factor = Math.Sqrt(correlation);
      bool shiftRight = factor - h < 0;
      bool shiftLeft = factor + h > 1;
      double xm = shiftRight ? factor : shiftLeft ? factor - 2 * h : factor - h;
      double x = shiftRight ? factor + h : shiftLeft ? factor - h : factor;
      double xp = shiftRight ? factor + 2 * h : shiftLeft ? factor : factor + h;
      s = strikeFunc.strike(x);
      double sp = strikeFunc.strike(xp);
      double sm = strikeFunc.strike(xm);
      double ffd = (sp - sm) / (2 * h);
      double fsd = (sp - 2 * s + sm) / (h * h);
      fd = 0.5 / x * ffd;
      sd = 0.25 * (fsd / (x*x) - ffd / (x * x * x));
      if (strikeMethod_ == BaseCorrelationStrikeMethod.EquitySpread || strikeMethod_ == BaseCorrelationStrikeMethod.SeniorSpread)
      {
        s = 1 - s;
        fd = -fd;
        sd = -sd;
      }
      basketPricer.SetFactor(factor);//repristinate original correlation state
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
    public override double CorrelationDerivatives(BasketPricer basketPricer, DiscountCurve discountCurve, SyntheticCDO cdo, double[] retVal)
    {
      if (StrikeMethod == BaseCorrelationStrikeMethod.Unscaled)
      {
        for (int i = 0; i < retVal.Length; i++)
          retVal[i] = 0.0;
      }
      if (basketPricer.HasFixedRecovery)
      {
        basketPricer = (BasketPricer) basketPricer.ShallowCopy();
        basketPricer.RecoveryCurves = BasketPricer.GetRecoveryCurves(basketPricer.SurvivalCurves);
        basketPricer.HasFixedRecovery = false;
      }
      double s, dsdf, dsdf2, dbcds, dbcds2;
      double[] ders = new double[retVal.Length];
      double[] dersP = new double[retVal.Length];
      double h = 1e-4;
      UniqueSequence<double> rawLossLevels;
      if (StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLoss || StrikeMethod == BaseCorrelationStrikeMethod.ExpectedLossPV)
        rawLossLevels = new UniqueSequence<double>(0.0, 1.0);
      else
        rawLossLevels = new UniqueSequence<double>(0.0, cdo.Detachment, 1.0);
      basketPricer.RawLossLevels = new UniqueSequence<double>(0.0, cdo.Detachment);
      basketPricer.Reset();
      double correlation = this.GetCorrelation(cdo, basketPricer, discountCurve, 0, 0);
      double factor = Math.Sqrt(correlation);
      StrikeCorrelationDerivative(correlation, basketPricer, cdo, discountCurve, out s, out dsdf, out dsdf2);
      CorrelationStrikeDerivative(s, out dbcds, out dbcds2);
      basketPricer.ComputeAndSaveSemiAnalyticSensitivities(rawLossLevels);
      StrikeDerivatives(basketPricer, discountCurve, cdo, ders);
      basketPricer.SetFactor(factor + h);
      //basket is reset, recompute distributions
      basketPricer.ComputeAndSaveSemiAnalyticSensitivities(rawLossLevels);
      StrikeDerivatives(basketPricer, discountCurve, cdo, dersP);
      basketPricer.SetFactor(factor);
      //compute mixed derivatives d2SdYdRho by finite difference of dsdY(factor+h) - dsdY(factor)
      double multfd = dbcds / (1.0 - dbcds * dsdf);
      double multsd = 1.0 / (1.0 - dbcds * dsdf);
      double mixedK, mixedJ;
      double x = (factor > 0) ? factor : factor + h;
      int idx = 0;
      for (int i = 0; i < basketPricer.SurvivalCurves.Length; i++)
      {
        int len = basketPricer.SurvivalCurves[i].Count;
        double[] gradF = new double[len];
        double[] gradS = new double[len];
        double[] gradSp = new double[len];
        for (int j = 0; j < len; j++)
        {
          gradS[j] = ders[idx];
          gradSp[j] = dersP[idx];
          gradF[j] = retVal[idx] = ders[idx] * multfd;
          idx++;
        }
        for (int j = 0; j < len; j++)
        {
          for (int k = 0; k <= j; k++)
          {
            mixedK = 0.5 / x * (gradSp[k] - gradS[k]) / h;
            mixedJ = 0.5 / x * (gradSp[j] - gradS[j]) / h;
            retVal[idx] = multsd * (dbcds2 * (dsdf * gradF[k] + gradS[k]) * (dsdf * gradF[j] + gradS[j]) +
                           dbcds * (dsdf2 * gradF[k] * gradF[j] + mixedK * gradF[j]
                                  + mixedJ * gradF[k] + ders[idx]));
            idx++;
          }
        }
        retVal[idx] = ders[idx] * multfd;
        idx++;
        retVal[idx] = ders[idx] * multfd;
        idx++;
      }
      return correlation;
    }

    #endregion
  }
}
