using System;
//using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// Computes the derivatives of a given cashflow w.r.t the chosen reference curve
  /// </summary>
  [Serializable]
  public class CashflowModelDerivatives
  {

    /// <summary>
    /// A delegate object that computes gradient and hessian of the expected (under the appropriate probability measure) reset w.r.t to the ordinates of a given reference curve
    /// </summary>
    /// <param name="start">Accrual start date</param>
    /// <param name="end">Accrual end date</param>
    /// <param name="payDt">Coupon payment date</param>
    /// <param name="grad">Filled up with the gradient of the expected coupon rate w.r.t the underlying curves ordinates. If we let <m>P(Dt, Y(Dt, y_0,\dots,y_n))</m>
    /// be the payment at <m>Dt</m>, then grad[i]<m>= \partial_{y_i} P(Dt, Y(Dt,y_0,\dots, y_n))</m> where <m> Y</m> and <m>y_i</m> are respectively the curve
    /// and the curve ordinates </param>
    /// <param name="hess">Filled up with the hessian of the expected reset w.r.t the underlying reference curves ordinates. If we let <m>R(Dt, Y(Dt, y_0,\dots,y_n))</m>
    /// be the expected reset at the reset date<m>Dt</m>, then grad[i]<m>= \partial_{y_i} R(Dt, Y(Dt,y_0,\dots, y_n))</m> and 
    /// hess[i*(i+1)/2+j]<m>= \partial_{y_iy_j} R(Dt, Y(Dt,y_0,\dots, y_n))</m> where <m> Y</m> and <m>y_i</m> are respectively the reference curve
    /// and the reference curve ordinates </param>
    ///<remarks>This method is expected to compute internally the correct reset date</remarks>
    public delegate void ExpectedResetDerivatives(Dt start, Dt end, Dt payDt, double[] grad, double[] hess);

    #region Auxiliary methods

    /// <summary>
    /// Calculates the earlier of the default dates of the counterparty and the name corresponding to survival curve
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="counterpartySurvivalCurve">Survival curve of the counterparty</param>
    /// <param name="isCounterpartyDefaulted">True if the counterparty has defaulted</param>
    /// <returns>Default date of the counterparty</returns>
    private static Dt GetDefaultDate(Curve survivalCurve, Curve counterpartySurvivalCurve, ref bool isCounterpartyDefaulted)
    {
      isCounterpartyDefaulted = false;
      Dt defaultDate = (survivalCurve == null) ? new Dt() : survivalCurve.JumpDate;
      if (counterpartySurvivalCurve != null)
      {
        Dt cpDfltDate = counterpartySurvivalCurve.JumpDate;
        if (!cpDfltDate.IsEmpty() && Dt.Cmp(cpDfltDate, defaultDate) < 0 || defaultDate.IsEmpty())
        {
          defaultDate = cpDfltDate;
          isCounterpartyDefaulted = true;
        }
      }
      return defaultDate;
    }

    /// <summary>
    /// Overwrites gradient and hessian vectors with valg and valh, respectively
    /// </summary>
    /// <param name="valg">Real number</param>
    /// <param name="valh">Real number</param>
    /// <param name="grad">Gradient</param>
    /// <param name="hess">Hessian</param>
    internal static void Fill(double valg, double valh, double[] grad, double[] hess)
    {
      if (grad == null)
        return;
      int n = grad.Length;
      int k = 0;
      for (int ii = 0; ii < n; ii++)
      {
        grad[ii] = valg;
        for (int jj = 0; jj <= ii; jj++)
        {
          hess[k] = valh;
          k++;
        }
      }
    }

    /// <summary>
    /// Auxiliary function that multiplies gradient and hessian by the scalar b
    /// </summary>
    /// <param name="b">A scalar value </param>
    /// <param name="grad">Gradient</param>
    /// <param name="hess">Hessian</param>
    /// <param name="bGrad">filled up by b * Gradient</param>
    /// <param name="bHess">filled up by b * Hessian</param>
    internal static void MultiplyByScalar(double b, double[] grad, double[] hess, double[] bGrad, double[] bHess)
    {
      if (grad == null)
      {
        Fill(0, 0, bGrad, bHess);
        return;
      }
      int k = 0;
      int n = grad.Length;
      for (int i = 0; i < n; i++)
      {
        bGrad[i] = grad[i] * b;
        for (int j = 0; j <= i; j++)
        {
          bHess[k] = hess[k] * b;
          k++;
        }
      }
    }

    /// <summary>
    /// Auxiliary function that takes linear combinations of derivatives
    /// </summary>
    /// <param name="a">Array of real numbers </param>
    /// <param name="aGrad">Array of gradient vectors</param>
    /// <param name="aHess">Array of hessian matrices in vector form</param>
    /// <param name="lcGrad">Linear combination of the gradients</param>
    /// <param name="lcHess">Linear combination of the hessians</param>
    internal static void LinearCombination(double[] a, double[][] aGrad, double[][] aHess, double[] lcGrad, double[] lcHess)
    {
      if (a == null)
      {
        Fill(0, 0, lcGrad, lcHess);
      }
      int k;
      Fill(0, 0, lcGrad, lcHess);
      for (int l = 0; l < a.Length; l++)
      {
        if (aGrad[l] == null)
          continue;
        k = 0;
        for (int i = 0; i < aGrad[l].Length; i++)
        {
          lcGrad[i] += a[l] * aGrad[l][i];
          for (int j = 0; j <= i; j++)
          {
            lcHess[k] += a[l] * aHess[l][k];
            k++;
          }
        }
      }
    }


    /// <summary>
    /// Auxiliary function that computes the gradient and hessian of a ratio
    /// </summary>
    /// <param name="num">Numerator</param>
    /// <param name="den">Denominator</param>
    /// <param name="numGrad">Gradient of the numerator</param>
    /// <param name="numHess">Hessian of the numerator</param>
    /// <param name="denGrad">Gradient of the denominator</param>
    /// <param name="denHess">Hessian of the denominator</param>
    /// <param name="ratioGrad"> Filled up with the gradient of the ratio</param>
    /// <param name="ratioHess">Filled up with the hessian of the ratio</param>
    internal static void RatioDerivatives(double num, double den, double[] numGrad, double[] numHess, double[] denGrad, double[] denHess, double[] ratioGrad, double[] ratioHess)
    {
      if (denGrad == null && numGrad == null)
      {
        Fill(0, 0, ratioGrad, ratioHess);
      }
      if (denGrad == null)
      {
        ProductDerivatives(num, 1.0 / den, numGrad, numHess, null, null, ratioGrad, ratioHess);
        return;
      }
      double den2 = den * den;
      double den3 = den2 * den;
      if (numGrad == null)
      {
        int k = 0;
        for (int i = 0; i < denGrad.Length; i++)
        {
          ratioGrad[i] = -num / den2 * denGrad[i];
          for (int j = 0; j <= i; j++)
          {
            ratioHess[k] = 2 * num / den3 * denGrad[i] * denGrad[j] - num * denHess[k] / den2;
            k++;
          }
        }
        return;

      }
      else
      {
        int k = 0;
        for (int i = 0; i < numGrad.Length; i++)
        {
          ratioGrad[i] = numGrad[i] / den - num / den2 * denGrad[i];
          for (int j = 0; j <= i; j++)
          {
            ratioHess[k] = numHess[k] / den - (denGrad[i] * numGrad[j] + denGrad[j] * numGrad[i]) / den2 +
                           2 * num / den3 * denGrad[i] * denGrad[j]
                           - num * denHess[k] / den2;
            k++;
          }
        }
      }
    }


    /// <summary>
    /// Auxiliary function that computes the gradient and hessian of a product
    /// </summary>
    /// <param name="a">function a value</param>
    /// <param name="b">function b value</param>
    /// <param name="aGrad">Gradient of function a</param>
    /// <param name="aHess">Hessian of function a</param>
    /// <param name="bGrad">Gradient of function b</param>
    /// <param name="bHess">Hessian of function b</param>
    /// <param name="prodGrad">Filled up with the gradient of the product</param>
    /// <param name="prodHess">Filled up with the hessian of the product</param>
    internal static void ProductDerivatives(double a, double b, double[] aGrad, double[] aHess, double[] bGrad, double[] bHess, double[] prodGrad, double[] prodHess)
    {
      if (aGrad == null && bGrad == null)
      {
        Fill(0, 0, prodGrad, prodHess);
      }
      if (aGrad == null)
      {
        MultiplyByScalar(a, bGrad, bHess, prodGrad, prodHess);
        return;
      }
      if (bGrad == null)
      {
        MultiplyByScalar(b, aGrad, aHess, prodGrad, prodHess);
        return;
      }
      int k = 0;
      int n = aGrad.Length;
      for (int i = 0; i < n; i++)
      {
        prodGrad[i] = aGrad[i] * b + a * bGrad[i];
        for (int j = 0; j <= i; j++)
        {
          prodHess[k] = aHess[k] * b + aGrad[i] * bGrad[j] + aGrad[j] * bGrad[i] + a * bHess[k];
          k++;
        }
      }
    }



    /// <summary>
    /// Add-assign operations for gradient and hessian vectors 
    /// </summary>
    /// <param name="aGrad">Gradient array replace by aGrad+bGrad</param>
    /// <param name="aHess">Hessian array replaced by aHess + bHess</param>
    /// <param name="bGrad">Gradient array</param>
    /// <param name="bHess">Hessian array</param>
    internal static void AddAssign(double[] aGrad, double[] aHess, double[] bGrad, double[] bHess)
    {
      if (bGrad == null)
        return;
      int n = aGrad.Length;
      int k = 0;
      for (int ii = 0; ii < n; ii++)
      {
        aGrad[ii] += bGrad[ii];
        for (int jj = 0; jj <= ii; jj++)
        {
          aHess[k] += bHess[k];
          k++;
        }
      }
    }

    /// <summary>
    /// Copies aGrad into bGrad and aHess into bHess 
    /// </summary>
    /// <param name="grad">Gradient array</param>
    /// <param name="hess">Hessian array</param>
    /// <param name="gradCopy">Copy of grad</param>
    /// <param name="hessCopy">Copy of hess</param>
    internal static void Copy(double[] grad, double[] hess, double[] gradCopy, double[] hessCopy)
    {
      if (grad == null)
        return;
      int n = grad.Length;
      int k = 0;
      for (int ii = 0; ii < n; ii++)
      {
        gradCopy[ii] = grad[ii];
        for (int jj = 0; jj <= ii; jj++)
        {
          hessCopy[k] = hess[k];
          k++;
        }
      }
    }

    /// <summary>
    /// Finds the gradient and hessian of the composition <m>f(g(y_0, y_1, \dots y_n))</m> w.r.t <m>y_i, i = 0,\dots,n</m>
    /// </summary>
    /// <param name="fd">Function <m>\frac{d}{dx}f(g(y_0,y_1, \dots, y_n)</m></param>
    /// <param name="sd">Function <m>\frac{d^2}{dx^2}f(g(y_0, y_1, \dots, y_n)</m></param>
    /// <param name="grad">gradient of <m>g</m></param>
    /// <param name="hess">hessian of <m>g</m></param>
    /// <param name="gradC">gradient of the composition</param>
    /// <param name="hessC">hessian of the composition</param>
    internal static void Composition(double fd, double sd, double[] grad, double[] hess, double[] gradC, double[] hessC)
    {
      if (grad == null)
        return;
      int n = grad.Length;
      int k = 0;
      for (int i = 0; i < n; i++)
      {
        gradC[i] = fd * grad[i];
        for (int j = 0; j <= i; j++)
        {
          hessC[k] = sd * grad[i] * grad[j] + fd * hess[k];
          k++;
        }
      }
    }

    /// <summary>
    /// Computes the gradient and hessian of the F(forward rate) function
    /// </summary>
    /// <param name="curve">Curve object</param>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <param name="dc">Daycount</param>
    /// <param name="freq">Frequency</param>
    /// <param name="gradient">Gradient of F w.r.t curve ordinates</param>
    /// <param name="hessian">Hessian of F w.r.t curve ordinates </param>

    internal static void ForwardRateDerivatives(Curve curve, Dt start, Dt end, DayCount dc, Frequency freq, double[] gradient, double[] hessian)
    {
      double rp = 0;
      double rpp = 0;
      double num = curve.Interpolate(end);
      double den = curve.Interpolate(start);
      double fd = num / den;
      int n = gradient.Length;
      RateCalc.RateFromPriceDerivatives(fd, start, end, dc, freq, ref rp, ref rpp);
      double[] grad1 = new double[n];
      double[] grad2 = new double[n];
      double[] hess1 = new double[n * (n + 1) / 2];
      double[] hess2 = new double[n * (n + 1) / 2];
      double[] gradR = new double[n];
      double[] hessR = new double[n * (n + 1) / 2];
      curve.Derivatives(end, grad1, hess1);
      curve.Derivatives(start, grad2, hess2);
      CashflowModelDerivatives.RatioDerivatives(num, den, grad1, hess1, grad2, hess2, gradR, hessR);
      CashflowModelDerivatives.Composition(rp, rpp, gradR, hessR, gradient, hessian);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="gradVec"></param>
    /// <param name="hessVec"></param>
    /// <param name="grad"></param>
    /// <param name="hess"></param>
    /// <param name="gradO"></param>
    /// <param name="hessO"></param>
    /// <param name="idx"></param>
    internal static void Composition(double[][] gradVec, double[][] hessVec, double[] grad, double[] hess, double[] gradO, double[] hessO, int idx)
    {
      int kk, k = 0;
      int nt = gradVec[0].Length;
      for (int i = 0; i < nt; i++)
      {
        gradO[i] = 0.0;
        for (int ii = 0; ii < idx; ii++)
          gradO[i] += grad[ii] * gradVec[ii][i];
        for (int j = 0; j <= i; j++)
        {
          kk = 0;
          hessO[k] = 0.0;
          for (int ii = 0; ii < idx; ii++)
          {
            hessO[k] += grad[ii] * hessVec[ii][k];
            for (int jj = 0; jj <= ii; jj++)
            {
              double mult = (jj == ii)
                                ? gradVec[ii][i] *
                                 gradVec[jj][j]
                                : gradVec[ii][i] *
                                  gradVec[jj][j] +
                                  gradVec[ii][j] *
                                  gradVec[jj][i];
              hessO[k] += hess[kk] * mult;
              kk++;
            }
          }
          k++;
        }
      }
    }

    #endregion

    /// <summary>
    /// Compute the derivatives of the product's PV w.r.t the ordinates of the input discount curve. 
    /// This also cover the case of a the accrued being function of the discount curve, like the floating leg of swaps on Libor or Cms 
    /// </summary>
    /// <param name="cf">Cashflow object used to determine the cashflow PV</param>
    /// <param name="resetDerivatives">Derivatives of the expected reset wrt to the underlying reference curve ordinates</param>
    ///<param name="wrtOtherCurve">True if want to compute derivative w.r.t ordinates of the reset projection curve if this curve is different from that used to discount cashflows</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="discountCurve">Discount curve object</param>
    /// <param name="survivalCurve">Survival curve object</param>
    /// <param name="counterpartyCurve">Counterparty curve object</param>
    /// <param name="correlation">Correlation between survival curve and counterparty</param>
    /// <param name="includeFees">True if fees are included</param>
    /// <param name="includeProtection">True to compute protection</param>
    /// <param name="includeSettle">True if settle date is included in the PV calculation</param>
    /// <param name="includeMaturityProtection">True if protection at maturity is included in the PV calculation</param>
    /// <param name="discountingAccrued">True to discount accrued</param>
    /// <param name="step">Step size for approximation of the integral</param>
    /// <param name="stepUnit">Step unit of measure</param>
    /// <param name="idx">Index until which the PV is computed  </param>
    /// <param name="grad">Gradient of the PV w.r.t curve ordinates. If we let <m>PV(Y(Dt, y_0,\dots,y_n))</m>
    /// be the PV of the product, then gradPV[i]<m>= \partial_{y_i} PV(Y(Dt,y_0,\dots, y_n))</m> where <m> Y</m> and <m>y_i</m> are respectively the curve
    /// and the curve ordinates</param>
    /// <param name="hess">Filled up with the hessian of the expected payment w.r.t the underlying curves ordinates. If we let <m>PV(Y(Dt, y_0,\dots,y_n))</m>
    /// be the product's PV, then hessPV[i*(i+1)/2+j]<m>= \partial_{y_iy_j} PV(Y(Dt,y_0,\dots, y_n))</m> where <m> Y</m> and <m>y_i</m> are respectively the curve
    /// and the curve ordinates </param>
    public static void PriceDerivatives(Cashflow cf, ExpectedResetDerivatives resetDerivatives, bool wrtOtherCurve,
    Dt asOf, Dt settle, DiscountCurve discountCurve,
    SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve, double correlation, bool includeFees,
    bool includeProtection, bool includeSettle, bool includeMaturityProtection, bool discountingAccrued,
    int step, TimeUnit stepUnit, int idx, double[] grad, double[] hess)
    {
      if (wrtOtherCurve && resetDerivatives == null)
      {
        Fill(0, 0, grad, hess);
        return;
      }
      double df, prevDf, probCreditDflt, probCounterDflt, prevProbCreditDflt, prevProbCounterDflt, probNoDflt, prevProbNoDflt,
      settleDf, settleProbNoDflt;
      df = probNoDflt = 0;
      int i, firstIdx;
      int n = grad.Length;
      Dt prevDate;
      Curve creditSurvivalCurve, counterpartySurvivalCurve;
      double[] accrualGrad = (resetDerivatives == null) ? null : new double[n];
      double[] accrualHess = (resetDerivatives == null) ? null : new double[n * (n + 1) / 2];
      double[] accruedGrad = (resetDerivatives == null) ? null : new double[n];
      double[] accruedHess = (resetDerivatives == null) ? null : new double[n * (n + 1) / 2];
      double[] dfGradNew = wrtOtherCurve ? null : new double[n];
      double[] dfHessNew = wrtOtherCurve ? null : new double[n * (n + 1) / 2];
      double[] dfGradOld = wrtOtherCurve ? null : new double[n];
      double[] dfHessOld = wrtOtherCurve ? null : new double[n * (n + 1) / 2];
      double[] dfGradSettle = wrtOtherCurve ? null : new double[n];
      double[] dfHessSettle = wrtOtherCurve ? null : new double[n * (n + 1) / 2];
      double[] dfGradAvg = wrtOtherCurve ? null : new double[n];
      double[] dfHessAvg = wrtOtherCurve ? null : new double[n * (n + 1) / 2];
      double[] dfAccrgrad = wrtOtherCurve ? null : new double[n];
      double[] dfAccrhess = wrtOtherCurve ? null : new double[n * (n + 1) / 2];
      double[] dfAccrgradAD = (!cf.AccruedPaidOnDefault)? null : new double[n];
      double[] dfAccrhessAD = (!cf.AccruedPaidOnDefault) ? null : new double[n * (n + 1) / 2];
      double[] gradEPV = new double[n];
      double[] hessEPV = new double[n * (n + 1) / 2];

      Fill(0, 0, grad, hess);
      if (idx < 0 || idx > cf.Count)
        throw new ArgumentException("Invalid index (%d) for cf pv");
      DayCount dayCount = cf.DayCount;
      double pv = 0;
      double accrued = 0.0;
      if (idx == 0)
        return;
      int cmp = -1;
      for (firstIdx = 0; firstIdx < idx; firstIdx++)
      {
        cmp = Dt.Cmp(cf.GetDt(firstIdx), settle);
        if (cmp >= 0)
          break;
      }
      if (survivalCurve != null && counterpartyCurve != null)
      {
        creditSurvivalCurve = new Curve(asOf, survivalCurve.Interp, survivalCurve.DayCount, survivalCurve.Frequency);
        counterpartySurvivalCurve = new Curve(asOf, counterpartyCurve.Interp, counterpartyCurve.DayCount, counterpartyCurve.Frequency);
        Dt lastDate = (idx == 0 ? cf.GetDt(0) : cf.GetDt(idx - 1));
        if (includeMaturityProtection)
          lastDate = Dt.Add(lastDate, 1);
        else
          CounterpartyRisk.TransformSurvivalCurves(asOf, lastDate, survivalCurve, counterpartyCurve,
                                                       correlation, creditSurvivalCurve,
                                                       counterpartySurvivalCurve, step, stepUnit);
      }
      else
      {
        creditSurvivalCurve = survivalCurve;
        counterpartySurvivalCurve = null;
      }
      settleDf = discountCurve.Interpolate(settle);
      if (dfGradSettle != null)
        discountCurve.Derivatives(settle, dfGradSettle, dfHessSettle);
      probCreditDflt = (creditSurvivalCurve != null) ? (1.0 - creditSurvivalCurve.Interpolate(settle)) : 0.0;
      probCounterDflt = (counterpartySurvivalCurve != null) ? (1.0 - counterpartySurvivalCurve.Interpolate(settle)) : 0.0;
      settleProbNoDflt = 1 - probCreditDflt - probCounterDflt;
      bool isCounterpartyDefaulted = false;
      Dt defaultDate = GetDefaultDate(survivalCurve, counterpartyCurve, ref isCounterpartyDefaulted);
      bool defaultOnSettle = false;
      if (!defaultDate.IsEmpty())
      {
        int dfltCmp = Dt.Cmp(defaultDate, settle);
        if (dfltCmp < 0)
          return;
        defaultOnSettle = (dfltCmp == 0);
      }
      if (defaultOnSettle)
      {
        if (isCounterpartyDefaulted)
          return;
        if (cmp == 0)
        {
          if (includeProtection)
            pv += cf.GetDefaultAmount(firstIdx);
          ++firstIdx;
          cmp = 1;
        }
        if (settleProbNoDflt <= 1E-8)
          settleProbNoDflt = 1.0;//note this for division (if default on settle denom is constant)
      }
      if (cmp == 0 && !includeSettle)
        ++firstIdx;
      int last = idx - 1;


      for (i = firstIdx, prevDate = settle, prevProbNoDflt = 1.0, prevDf = 1.0; i < idx; i++)
      {
        Dt nextDate = cf.GetDt(i);
        Dt date = prevDate;
        Dt prevStepDate = date;
        Dt accrualStart = new Dt();
        int accrualPeriod = 0;
        if (includeFees && cf.AccruedPaidOnDefault)
        {
          accrualStart = (i > firstIdx) ? prevDate : ((firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective);
          accrualPeriod = Dt.Diff(accrualStart, nextDate, dayCount);
          if (includeMaturityProtection && i == last)
            accrualPeriod++;
        }
        bool includeLast = (i == last && includeMaturityProtection);
        while (Dt.Cmp(date, nextDate) < 0 || includeLast)
        {
          prevProbCreditDflt = probCreditDflt;
          prevProbCounterDflt = probCounterDflt;
          if (step > 0)
          {
            Dt protectionDate = date = Dt.Add(date, step, stepUnit);
            if (Dt.Cmp(date, nextDate) >= 0)
            {
              date = nextDate;
              protectionDate = includeLast ? Dt.Add(date, 1) : date;
              includeLast = false;
            }
            probCreditDflt = (creditSurvivalCurve != null)
                                 ? (1.0 - creditSurvivalCurve.Interpolate(protectionDate))
                                 : 0.0;
            probCounterDflt = (counterpartySurvivalCurve != null)
                                  ? (1.0 - counterpartySurvivalCurve.Interpolate(protectionDate))
                                  : 0.0;
          }
          else
          {
            date = nextDate;
            Dt protectionDate = includeLast ? Dt.Add(date, 1) : date;
            includeLast = false;
            probCreditDflt = (creditSurvivalCurve != null)
                                 ? (1.0 - creditSurvivalCurve.Interpolate(protectionDate))
                                 : 0.0;
            probCounterDflt = (counterpartySurvivalCurve != null)
                                  ? (1.0 - counterpartySurvivalCurve.Interpolate(protectionDate))
                                  : 0.0;
          }
          df = discountCurve.Interpolate(date) / settleDf;
          if (dfGradNew != null)
            discountCurve.Derivatives(date, dfGradNew, dfHessNew); //Division by settleDf will be handled in the end of the time loop
          probNoDflt = (1 - probCreditDflt - probCounterDflt) / settleProbNoDflt;
          double timeFraction = cf.GetDefaultTiming();
          double avgDf = ((step == 1) && (stepUnit == TimeUnit.Days))
                             ? df
                             : ((1.0 - timeFraction) * prevDf + timeFraction * df);

          if ((step == 1) && (stepUnit == TimeUnit.Days))
            Copy(dfGradNew, dfHessNew, dfGradAvg, dfHessAvg);
          else
            LinearCombination(new double[] { 1.0 - timeFraction, timeFraction },
                                         new double[][] { dfGradOld, dfGradNew },
                                         new double[][] { dfHessOld, dfHessNew }, dfGradAvg,
                                         dfHessAvg);

          if (includeFees && cf.AccruedPaidOnDefault)
          {
            int days;
            Dt dfltAccrualStart = accrualStart;
            if (i == firstIdx && Dt.Cmp(settle, dfltAccrualStart) > 0)
              dfltAccrualStart = settle;
            if (Dt.Cmp(dfltAccrualStart, prevStepDate) < 0)
              days = Dt.Diff(dfltAccrualStart, prevStepDate, dayCount)
                     + (int)(Dt.Diff(prevStepDate, date, dayCount) * cf.AccruedFractionOnDefault);
            else
              days = (int)(Dt.Diff(dfltAccrualStart, date, dayCount) * cf.AccruedFractionOnDefault);
            //- Include default date if we need to
            if (cf.AccruedIncludingDefaultDate)
              days++;
            //- Calculate accrued. 
            accrued = (double)days / (double)accrualPeriod * cf.GetAccrued(i);
            double accruedEPV = avgDf * (prevProbNoDflt - probNoDflt) * accrued;
            pv += accruedEPV;
            double coup = cf.GetCoupon(i);
            double accruedMultiplier = (coup != 0) ? ((double)days / (double)accrualPeriod) * accrued / coup : cf.GetPeriodFraction(i) * cf.GetPrincipalAt(i);
            if (resetDerivatives != null)
            {
              if (!cf.GetProjectedAt(i))
                Fill(0.0, 0.0, accrualGrad, accrualHess);
              else
                resetDerivatives(cf.GetStartDt(i), cf.GetEndDt(i), cf.GetDt(i), accrualGrad, accrualHess);
            }
            MultiplyByScalar(accruedMultiplier, accrualGrad, accrualHess, accruedGrad, accruedHess);
            ProductDerivatives(avgDf * settleDf, accrued, dfGradAvg, dfHessAvg, accruedGrad, accruedHess, dfAccrgradAD, dfAccrhessAD);
            MultiplyByScalar(prevProbNoDflt - probNoDflt, dfAccrgrad, dfAccrhess, gradEPV, hessEPV);
            AddAssign(grad, hess, gradEPV, hessEPV);
          }
          if (includeProtection)
          {
            double probDfltThisPeriod = (probCreditDflt - prevProbCreditDflt) / settleProbNoDflt;
            double protEPV = avgDf * probDfltThisPeriod * cf.GetDefaultAmount(i);
            pv += protEPV;
            MultiplyByScalar(probDfltThisPeriod * cf.GetDefaultAmount(i), dfGradAvg, dfHessAvg,
                                    gradEPV, hessEPV);
            AddAssign(grad, hess, gradEPV, hessEPV);
          }
          prevProbNoDflt = probNoDflt;
          prevDf = df;
          prevStepDate = date;
          Copy(dfGradNew, dfHessNew, dfGradOld, dfHessOld);
        }
        if (includeFees)
        {
          double accrual = cf.GetAccrued(i);
          if (resetDerivatives != null)
          {   //adjust start end accrual period for consistency 
            if (!cf.GetProjectedAt(i))
              Fill(0.0, 0.0, accrualGrad, accrualHess);
            else
              resetDerivatives(cf.GetStartDt(i), cf.GetEndDt(i), cf.GetDt(i), accrualGrad, accrualHess);
            double coup = cf.GetCoupon(i);
            double accruedMultiplier = (coup != 0) ? accrual / coup : cf.GetPeriodFraction(i) * cf.GetPrincipalAt(i);//To add amortization 
            MultiplyByScalar(accruedMultiplier, accrualGrad, accrualHess, accrualGrad, accrualHess);
          }
          if (i == firstIdx)
          {
            if (cmp > 0)
            {
              accrualStart = (firstIdx > 0 ? cf.GetDt(firstIdx - 1) : cf.Effective);
              if (Dt.Cmp(settle, accrualStart) > 0)
              {
                double fraction = ((double)Dt.Diff(accrualStart, settle, dayCount)) /
                          Dt.Diff(accrualStart, nextDate, dayCount);
                accrued = accrual * fraction;
                accrual -= accrued;
                MultiplyByScalar(fraction, accrualGrad, accrualHess, accruedGrad, accruedHess);
                MultiplyByScalar(1.0 - fraction, accrualGrad, accrualHess, accrualGrad, accrualHess);
               }
            }
            if (includeSettle && (Dt.Cmp(settle, nextDate) == 0))
            {
              df = discountCurve.Interpolate(settle) / settleDf;//=1?
              //Division by settledf will be handled at the end of the loop
              Fill(0.0, 0.0, dfGradNew, dfHessNew);//zero since df is constant function 1
              probCreditDflt = (creditSurvivalCurve != null)
                                   ? (1.0 - creditSurvivalCurve.Interpolate(date))
                                   : 0.0;
              probCounterDflt = (counterpartySurvivalCurve != null)
                                    ? (1.0 - counterpartySurvivalCurve.Interpolate(date))
                                    : 0.0;
              probNoDflt = (1 - probCreditDflt - probCounterDflt) / settleProbNoDflt;
            }
          }
          double feeEPV = df * probNoDflt * (cf.GetAmount(i) + accrual);
          pv += feeEPV;
          MultiplyByScalar(probNoDflt, dfGradNew, dfHessNew, dfAccrgrad, dfAccrhess);
          ProductDerivatives(df * settleDf * probNoDflt, cf.GetAmount(i) + accrual, dfAccrgrad, dfAccrhess, accrualGrad, accrualHess, gradEPV, hessEPV);
          AddAssign(grad, hess, gradEPV, hessEPV);
          if (i == last)
          {
            pv += df * (1 - probNoDflt) * cf.GetMaturityPaymentIfDefault();
            MultiplyByScalar((1 - probNoDflt) * cf.GetMaturityPaymentIfDefault(), dfGradNew, dfHessNew, gradEPV, hessEPV);
            AddAssign(grad, hess, gradEPV, hessEPV);
          }
        }
        prevDate = nextDate;
      }
      double dfAsOf = discountCurve.Interpolate(asOf);
      if (dfGradOld != null)
        discountCurve.Derivatives(asOf, dfGradOld, dfHessOld);
      RatioDerivatives(pv * settleDf, dfAsOf, grad, hess, dfGradOld, dfHessOld, gradEPV, hessEPV);
      LinearCombination(new double[] { 1, 1 }, new double[][] { accruedGrad, gradEPV }, new double[][] { accruedHess, hessEPV }, grad, hess);

    }



    /// <summary>
    /// Compute the derivatives of the product's PV w.r.t the ordinates of the input survival curve. 
    /// </summary>
    /// <param name="cf">Cashflow object used to determine the cashflow PV</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="discountCurve">Discount curve object</param>
    /// <param name="survivalCurve">Survival curve object</param>
    /// <param name="counterpartyCurve">Counterparty curve object</param>
    /// <param name="correlation">Correlation between survival curve and counterparty</param>
    /// <param name="includeFees">True if fees are included</param>
    /// <param name="includeProtection">True to compute protection</param>
    /// <param name="includeSettle">True if settle date is included in the PV calculation</param>
    /// <param name="includeMaturityProtection">True if protection at maturity is included in the PV calculation</param>
    /// <param name="discountingAccrued">True to discount accrued</param>
    /// <param name="step">Step size for approximation of the integral</param>
    /// <param name="stepUnit">Step unit of measure</param>
    /// <param name="idx"> Index until which the PV is computed </param>
    /// <param name="grad">Gradient of the PV w.r.t curve ordinates. If we let <m>PV(Y(Dt, y_0,\dots,y_n))</m>
    /// be the PV of the product, then gradPV[i]<m>= \partial_{y_i} PV(Y(Dt,y_0,\dots, y_n))</m> where <m> Y</m> and <m>y_i</m> are respectively the curve
    /// and the curve ordinates</param>
    /// <param name="hess">Filled up with the hessian of the expected payment w.r.t the underlying curves ordinates. If we let <m>PV(Y(Dt, y_0,\dots,y_n))</m>
    /// be the product's PV, then hessPV[i*(i+1)/2+j]<m>= \partial_{y_iy_j} PV(Y(Dt,y_0,\dots, y_n))</m> where <m> Y</m> and <m>y_i</m> are respectively the curve
    /// and the curve ordinates </param>
    public static void PriceDerivatives(Cashflow cf,
    Dt asOf, Dt settle, DiscountCurve discountCurve,
    SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve, double correlation, bool includeFees,
    bool includeProtection, bool includeSettle, bool includeMaturityProtection, bool discountingAccrued,
    int step, TimeUnit stepUnit, int idx, double[] grad, double[] hess)
    {
      if (survivalCurve == null)
      {
        Fill(0, 0, grad, hess);
        return;
      }
      int i, firstIdx;
      double df, prevDf, probCreditDflt, probCounterDflt, prevProbCreditDflt, prevProbCounterDflt, probNoDflt, prevProbNoDflt,
      settleDf, settleProbNoDflt;
      Curve creditSurvivalCurve, counterpartySurvivalCurve;
      bool haveCtpRisk = (counterpartyCurve != null);
      int n = grad.Length;
      double[] spGradNew = new double[n];
      double[] spHessNew = new double[n * (n + 1) / 2];
      double[] spGradOld = new double[n];
      double[] spHessOld = new double[n * (n + 1) / 2];
      double[] spGradSettle = new double[n];
      double[] spHessSettle = new double[n * (n + 1) / 2];
      double[] dfltGradNew = new double[n];
      double[] dfltHessNew = new double[n * (n + 1) / 2];
      double[] dfltGradOld = new double[n];
      double[] dfltHessOld = new double[n * (n + 1) / 2];
      double[] dfltCpGradNew = !haveCtpRisk ? null : new double[n];
      double[] dfltCpHessNew = !haveCtpRisk ? null : new double[n * (n + 1) / 2];
      double[] dfltCpGradOld = !haveCtpRisk ? null : new double[n];
      double[] dfltCpHessOld = !haveCtpRisk ? null : new double[n * (n + 1) / 2];
      double[] gradEPV = new double[n];
      double[] hessEPV = new double[n * (n + 1) / 2];
      Dt prevDate = new Dt();
      df = probNoDflt = 1.0;
      Fill(0, 0, grad, hess);
      if (idx < 0 || idx > cf.Count)
        throw new ArgumentException("Invalid index (%d) for cf pv");
      DayCount dayCount = cf.DayCount;
      double pv = 0;
      double accrued = 0.0;
      if (idx == 0)
        return;
      int cmp = -1;
      for (firstIdx = 0; firstIdx < idx; firstIdx++)
      {
        cmp = Dt.Cmp(cf.GetDt(firstIdx), settle);
        if (cmp >= 0)
          break;
      }
      if (survivalCurve != null && counterpartyCurve != null)
      {
        creditSurvivalCurve = new Curve(new AdjustedSurvivalCurve(asOf, survivalCurve.DayCount, survivalCurve.Frequency));
        creditSurvivalCurve.Interp = survivalCurve.Interp;
        counterpartySurvivalCurve = new Curve(new AdjustedSurvivalCurve(asOf, counterpartyCurve.DayCount, counterpartyCurve.Frequency));
        counterpartySurvivalCurve.Interp = counterpartyCurve.Interp;
        Dt lastDate = (idx == 0 ? cf.GetDt(0) : cf.GetDt(idx - 1));
        if (includeMaturityProtection)
          lastDate = Dt.Add(lastDate, 1);
        CounterpartyAdjusted.MakeCurves(asOf, lastDate, survivalCurve, counterpartyCurve,
                                                     correlation, (AdjustedSurvivalCurve)creditSurvivalCurve,
                                                         (AdjustedSurvivalCurve)counterpartySurvivalCurve, step, stepUnit);
      }
      else
      {
        creditSurvivalCurve = survivalCurve;
        counterpartySurvivalCurve = null;
      }
      settleDf = discountCurve.Interpolate(settle);
      probCreditDflt = (creditSurvivalCurve != null) ? (1.0 - creditSurvivalCurve.Interpolate(settle)) : 0.0;
      if (creditSurvivalCurve != null)
        creditSurvivalCurve.Derivatives(settle, dfltGradOld, dfltHessOld);
      probCounterDflt = (counterpartySurvivalCurve != null) ? (1.0 - counterpartySurvivalCurve.Interpolate(settle)) : 0.0;
      if (counterpartySurvivalCurve != null)
        counterpartySurvivalCurve.Derivatives(settle, dfltCpGradOld, dfltCpHessOld);
      settleProbNoDflt = 1 - probCreditDflt - probCounterDflt;
      LinearCombination(new double[] { 1.0, 1.0 }, new double[][] { dfltGradOld, dfltCpGradOld }, new double[][] { dfltHessOld, dfltCpHessOld }, spGradSettle, spHessSettle);
      bool isCounterpartyDefaulted = false;
      Dt defaultDate = GetDefaultDate(survivalCurve, counterpartyCurve, ref isCounterpartyDefaulted);
      bool defaultOnSettle = false;
      if (!defaultDate.IsEmpty())
      {
        int dfltCmp = Dt.Cmp(defaultDate, settle);
        if (dfltCmp < 0)
          return;
        defaultOnSettle = (dfltCmp == 0);
      }
      if (defaultOnSettle)
      {
        if (isCounterpartyDefaulted)
          return;
        if (cmp == 0)
        {
          if (includeProtection)
            pv += cf.GetDefaultAmount(firstIdx);
          ++firstIdx;
          cmp = 1;
        }
        if (settleProbNoDflt <= 1E-8)
          settleProbNoDflt = 1.0;
      }
      if (cmp == 0 && !includeSettle)
        ++firstIdx;
      int last = idx - 1;
      for (i = firstIdx, prevDate = settle, prevProbNoDflt = 1.0, prevDf = 1.0; i < idx; i++)
      {
        Dt nextDate = cf.GetDt(i);
        Dt date = prevDate;
        Dt prevStepDate = date;
        Dt accrualStart = new Dt();
        int accrualPeriod = 0;
        if (includeFees && cf.AccruedPaidOnDefault)
        {
          accrualStart = (i > firstIdx) ? prevDate : ((firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective);
          accrualPeriod = Dt.Diff(accrualStart, nextDate, dayCount);
          if (includeMaturityProtection && i == last)
            accrualPeriod++;
        }
        bool includeLast = (i == last && includeMaturityProtection);
        while (Dt.Cmp(date, nextDate) < 0 || includeLast)
        {
          prevProbCreditDflt = probCreditDflt;
          prevProbCounterDflt = probCounterDflt;
          Copy(dfltGradNew, dfltHessNew, dfltGradOld, dfltHessOld);
          Copy(dfltCpGradNew, dfltCpHessNew, dfltCpGradOld, dfltCpHessOld);
          if (step > 0)
          {
            Dt protectionDate = date = Dt.Add(date, step, stepUnit);
            if (Dt.Cmp(date, nextDate) >= 0)
            {
              date = nextDate;
              protectionDate = includeLast ? Dt.Add(date, 1) : date;
              includeLast = false;
            }
            probCreditDflt = (creditSurvivalCurve != null)
                                 ? (1.0 - creditSurvivalCurve.Interpolate(protectionDate))
                                 : 0.0;
            if (creditSurvivalCurve != null)
              creditSurvivalCurve.Derivatives(protectionDate, dfltGradNew, dfltHessNew);
            probCounterDflt = (counterpartySurvivalCurve != null)
                                  ? (1.0 - counterpartySurvivalCurve.Interpolate(protectionDate))
                                  : 0.0;
            if (counterpartySurvivalCurve != null)
              counterpartySurvivalCurve.Derivatives(protectionDate, dfltCpGradNew, dfltCpHessNew);
          }
          else
          {
            date = nextDate;
            Dt protectionDate = includeLast ? Dt.Add(date, 1) : date;
            includeLast = false;
            probCreditDflt = (creditSurvivalCurve != null)
                                 ? (1.0 - creditSurvivalCurve.Interpolate(protectionDate))
                                 : 0.0;
            if (creditSurvivalCurve != null)
              creditSurvivalCurve.Derivatives(protectionDate, dfltGradNew, dfltHessNew);
            probCounterDflt = (counterpartySurvivalCurve != null)
                                  ? (1.0 - counterpartySurvivalCurve.Interpolate(protectionDate))
                                  : 0.0;
            if (counterpartySurvivalCurve != null)
              counterpartySurvivalCurve.Derivatives(protectionDate, dfltCpGradNew, dfltCpHessNew);
          }
          df = discountCurve.Interpolate(date) / settleDf;
          probNoDflt = (1 - probCreditDflt - probCounterDflt) / settleProbNoDflt;//Division by settleDf will be handled in the end of the time loop
          LinearCombination(new double[] { 1, 1 }, new double[][] { dfltGradNew, dfltCpGradNew }, new double[][] { dfltHessNew, dfltCpHessNew }, spGradNew, spHessNew);
          double timeFraction = cf.GetDefaultTiming();
          double avgDf = ((step == 1) && (stepUnit == TimeUnit.Days))
                             ? df
                             : ((1.0 - timeFraction) * prevDf + timeFraction * df);
          if (includeFees && cf.AccruedPaidOnDefault)
          {
            int days;
            Dt dfltAccrualStart = accrualStart;
            if (i == firstIdx && Dt.Cmp(settle, dfltAccrualStart) > 0)
              dfltAccrualStart = settle;
            if (Dt.Cmp(dfltAccrualStart, prevStepDate) < 0)
              days = Dt.Diff(dfltAccrualStart, prevStepDate, dayCount)
                     + (int)(Dt.Diff(prevStepDate, date, dayCount) * cf.AccruedFractionOnDefault);
            else
              days = (int)(Dt.Diff(dfltAccrualStart, date, dayCount) * cf.AccruedFractionOnDefault);
            //- Include default date if we need to
            if (cf.AccruedIncludingDefaultDate)
              days++;
            //- Calculate accrued. 
            accrued = (double)days / (double)accrualPeriod * cf.GetAccrued(i);
            double accruedEPV = avgDf * (prevProbNoDflt - probNoDflt) * accrued;
            LinearCombination(new double[] { -avgDf * accrued, avgDf * accrued }, new double[][] { spGradNew, spGradOld }, new double[][] { spHessNew, spHessOld }, gradEPV, hessEPV);
            pv += accruedEPV;
            AddAssign(grad, hess, gradEPV, hessEPV);
          }
          if (includeProtection)
          {
            double probDfltThisPeriod = (probCreditDflt - prevProbCreditDflt) / settleProbNoDflt;
            double protEPV = avgDf * probDfltThisPeriod * cf.GetDefaultAmount(i);
            pv += protEPV;
            LinearCombination(new double[] { -avgDf * cf.GetDefaultAmount(i), avgDf * cf.GetDefaultAmount(i) }, new double[][] { dfltGradNew, dfltGradOld }, new double[][] { dfltHessNew, dfltHessOld }, gradEPV, hessEPV);
            AddAssign(grad, hess, gradEPV, hessEPV);
          }
          prevProbNoDflt = probNoDflt;
          Copy(spGradNew, spHessNew, spGradOld, spHessOld);
          prevDf = df;
          prevStepDate = date;

        }
        if (includeFees)
        {
          double accrual = cf.GetAccrued(i);
          if (i == firstIdx)
          {
            if (cmp > 0)
            {
              accrualStart = (firstIdx > 0 ? cf.GetDt(firstIdx - 1) : cf.Effective);
              if (Dt.Cmp(settle, accrualStart) > 0)
              {
                accrued = accrual * ((double)Dt.Diff(accrualStart, settle, dayCount)) /
                          Dt.Diff(accrualStart, nextDate, dayCount);
                accrual -= accrued;
              }
            }
            if (includeSettle && (Dt.Cmp(settle, nextDate) == 0))
            {
              df = discountCurve.Interpolate(settle) / settleDf;
              probCreditDflt = (creditSurvivalCurve != null)
                                   ? (1.0 - creditSurvivalCurve.Interpolate(date))
                                   : 0.0;
              if (creditSurvivalCurve != null)
                creditSurvivalCurve.Derivatives(date, dfltGradNew, dfltHessNew);
              probCounterDflt = (counterpartySurvivalCurve != null)
                                    ? (1.0 - counterpartySurvivalCurve.Interpolate(date))
                                    : 0.0;
              if (counterpartySurvivalCurve != null)
                counterpartySurvivalCurve.Derivatives(date, dfltCpGradNew, dfltCpHessNew);
              probNoDflt = (1 - probCreditDflt - probCounterDflt) / settleProbNoDflt;
              LinearCombination(new double[] { 1, 1 }, new double[][] { dfltGradNew, dfltCpGradNew }, new double[][] { dfltHessNew, dfltCpHessNew }, spGradNew, spHessNew);
            }
          }
          double feeEPV = df * probNoDflt * (cf.GetAmount(i) + accrual);
          pv += feeEPV;
          MultiplyByScalar(df * (cf.GetAmount(i) + accrual), spGradNew, spHessNew, gradEPV, hessEPV);
          AddAssign(grad, hess, gradEPV, hessEPV);
          if (i == last)
          {
            pv += df * (1 - probNoDflt) * cf.GetMaturityPaymentIfDefault();
            MultiplyByScalar(-df * cf.GetMaturityPaymentIfDefault(), spGradNew, spHessNew, gradEPV, hessEPV);
            AddAssign(grad, hess, gradEPV, hessEPV);
          }
        }
        prevDate = nextDate;
      }
      double disc = discountCurve.Interpolate(asOf, settle);
      RatioDerivatives(pv * settleProbNoDflt, settleProbNoDflt, grad, hess, spGradSettle, spHessSettle, gradEPV, hessEPV);
      MultiplyByScalar(disc, gradEPV, hessEPV, grad, hess);
    }
  }
}
