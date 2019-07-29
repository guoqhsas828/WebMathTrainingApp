/*
 * Partial proxy for Counterparty Risk model
 *
 *  -2008. All rights reserved.
 *
 * $Id$
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.ComponentModel;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.Native.CounterpartyRisk"/>
  /// </summary>
  [ReadOnly(true)]
  public abstract class CounterpartyRisk: Native.CounterpartyRisk
  {
    private CounterpartyRisk() { } // no way to instantiate this

    #region Probability

    /// <summary>
    ///   The probability that the credit defaults first between two dates.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>
    ///   Let <formula inline="true">\tau_D</formula> be the (random)
    ///   default time of the credit, <formula inline="true">\tau_P</formula> be the
    ///   (random) default time of the counterpaty.
    ///   This function calculate the probability:
    ///   <formula>
    ///     P(t) \equiv \mathrm{Prob}\{\tau_D \lt t, \tau_D \lt \tau_P \}
    ///   </formula>
    ///   where <c>t</c> is the end date and the start date is 0.  The calculation
    ///   is conditional on no default on both the credit and counterparty at time 0.
    ///   </para>
    /// 
    ///   <para>
    ///    For LCDS when <formula inline="true">\tau_P</formula> is interpreted as the
    ///    (random) prepayment time, this function calculate the probaility that
    ///    the credit defaults before <c>t</c> and no prepayment happens before the
    ///    default time.
    ///   </para>
    /// 
    ///   <para>
    ///    Please do not confuse this with the marginal probability of default,
    ///    which is defined as
    ///    <formula inline="true">\mathrm{prob}(\tau_D \lt t)</formula>.
    ///   </para>
    /// 
    ///   <para>
    ///    If no counterparty nor prepayment curve presents, this function simple
    ///    returns the default probaility of the credit.
    ///   </para>
    /// </remarks>
    /// 
    /// <param name="start">
    ///   The start date.
    /// </param>
    /// <param name="end">
    ///   The end date.
    /// </param>
    /// <param name="creditCurve">
    ///   Survival curve of credit, or null if no credit risk on the entity.
    /// </param>
    /// <param name="counterpartyCurve">
    ///   Survival curve of counterparty, or null if nor default risk on the counterparty.
    /// </param>
    /// <param name="correlation">
    ///   Correlation between credit default and counterparty default.
    /// </param>
    /// <param name="stepSize">
    ///   Time grid size used to calculate the probability.
    /// </param>
    /// <param name="stepUnit">
    ///   Time unit of step size.
    /// </param>
    /// 
    /// <returns>
    ///   The probability that credit defaults first.
    /// </returns>
    public static double CreditDefaultProbability(
      Dt start, Dt end,
      SurvivalCurve creditCurve,
      SurvivalCurve counterpartyCurve,
      double correlation,
      int stepSize,
      TimeUnit stepUnit)
    {
      if (Dt.Cmp(start, end) > 0)
      {
        throw new System.ArgumentException(String.Format(
          "Start {0} must be later than end {1}", start, end));
      }

      if (creditCurve == null)
      {
        // No credit curve means no default risk
        return 0.0;
      }
      else if (creditCurve.DefaultDate.IsValid()
        && Dt.Cmp(creditCurve.DefaultDate, start) < 0)
      {
        // The credit already defaulted
        return 1.0;
      }

      if (counterpartyCurve == null)
      {
        // no counterparty risk
        return 1 - creditCurve.Interpolate(start, end);
      }
      else if (counterpartyCurve.DefaultDate.IsValid()
        && Dt.Cmp(counterpartyCurve.DefaultDate, start) < 0)
      {
        // Counterparty defaulted while the credit not
        return 0.0;
      }

      Curve sc = new Curve();
      Curve pc = new Curve();
      CounterpartyRisk.TransformSurvivalCurves(start, end,
        creditCurve, counterpartyCurve, correlation, sc, pc,
        stepSize, stepUnit);

      return 1 - sc.Interpolate(end);
    }

    /// <summary>
    ///   The probability that the counterparty defaults first between two dates.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>
    ///   Let <formula inline="true">\tau_D</formula> be the (random)
    ///   default time of the credit, <formula inline="true">\tau_P</formula> be the
    ///   (random) default time of the counterpaty.
    ///   This function calculate the probability:
    ///   <formula>
    ///     Q(t) \equiv \mathrm{Prob}\{\tau_P \lt t, \tau_P \lt \tau_D \}
    ///   </formula>
    ///   where <c>t</c> is the end date and the start date is 0.  The calculation
    ///   is conditional on no default on both the credit and counterparty at time 0.
    ///   </para>
    /// 
    ///   <para>
    ///    For LCDS when <formula inline="true">\tau_P</formula> is interpreted as the
    ///   (random) prepayment time, this function calculate the probaility that
    ///    the credit prepays before <c>t</c> and no default happens before the
    ///    prepayment time.
    ///   </para>
    /// 
    ///   <para>
    ///    Please do not confuse this with the marginal probability of counterparty default,
    ///    which is defined as
    ///    <formula inline="true">\mathrm{prob}(\tau_P \lt t)</formula>.
    ///   </para>
    /// 
    ///   <para>
    ///    If no counterparty nor prepayment curve presents, this function simple
    ///    returns 0.
    ///   </para>
    /// </remarks>
    /// 
    /// <param name="start">
    ///   The start date.
    /// </param>
    /// <param name="end">
    ///   The end date.
    /// </param>
    /// <param name="creditCurve">
    ///   Survival curve of credit, or null if no credit risk on the entity.
    /// </param>
    /// <param name="counterpartyCurve">
    ///   Survival curve of counterparty, or null if nor default risk on the counterparty.
    /// </param>
    /// <param name="correlation">
    ///   Correlation between credit default and counterparty default.
    /// </param>
    /// <param name="stepSize">
    ///   Time grid size used to calculate the probability.
    /// </param>
    /// <param name="stepUnit">
    ///   Time unit of step size.
    /// </param>
    /// 
    /// <returns>
    ///   The probability that the counterpaty defaults first.
    /// </returns>
    public static double CounterpartyDefaultProbability(
      Dt start, Dt end,
      SurvivalCurve creditCurve,
      SurvivalCurve counterpartyCurve,
      double correlation,
      int stepSize,
      TimeUnit stepUnit)
    {
      if (Dt.Cmp(start, end) > 0)
      {
        throw new System.ArgumentException(String.Format(
          "Start {0} must be later than end {1}", start, end));
      }

      if (counterpartyCurve == null)
      {
        // no counterparty risk
        return 0.0;
      }
      else if (counterpartyCurve.DefaultDate.IsValid()
        && Dt.Cmp(counterpartyCurve.DefaultDate, start) < 0)
      {
        // Counterparty defaulted while the credit not
        return 1.0;
      }

      if (creditCurve == null)
      {
        // No default risk on the credit
        return 1 - counterpartyCurve.Interpolate(start, end);
      }
      else if (creditCurve.DefaultDate.IsValid()
        && Dt.Cmp(creditCurve.DefaultDate, start) < 0)
      {
        // Credit already defaulted but the counterparty not
        return 0.0;
      }

      Curve sc = new Curve();
      Curve pc = new Curve();
      CounterpartyRisk.TransformSurvivalCurves(start, end,
        creditCurve, counterpartyCurve, correlation, sc, pc,
        stepSize, stepUnit);

      return 1 - pc.Interpolate(end);
    }

    /// <summary>
    ///  Build a joint survival curve for the event that
    ///  the counterparty defaults after the guarantor defaults.
    /// </summary>
    /// 
    /// <remarks>
    /// <math> \begin{align}
    /// \tilde{D}_{i} - \tilde{D}_{i-1} &amp; =  \mathbb{Q}(t_{i-1} \leq \tilde{\tau} \leq t_i)
    /// \\ &amp; = \mathbb{Q}(\tau_g \leq \tau_c, \ t_{i-1} \leq \tau_c \leq t_i)
    /// \\ &amp; = \int_{t_{i-1}}^{t_i} \mathbb{Q}(\tau_g \leq t \big | \tau_c = t)\  dD^c_t)
    /// \\ &amp; = \int_{t_{i-1}}^{t_i} \Phi\left(\frac{\Phi^{-1}(D^g_t) - \rho \cdot \Phi^{-1}(D^c_t)}{\sqrt{1 - \rho^2}} \right) dD^c_t
    /// \\ &amp; \approx \Phi\left(\frac{\Phi^{-1}(D^g_{\bar{t}_i}) - \rho \cdot \Phi^{-1}(D^c_{\bar{t}_i })}{\sqrt{1 - \rho^2}} \right)  \cdot (D^c_{i} - D^c_{i-1})
    /// \end{align}</math>
    /// 
    /// where <m>\bar{t}_i = \frac{t_i + t_{i-1}}{2}</m>, <m>\rho</m> is the correlation between 
    /// counterparty default and guarantor default, <m>\Phi</m> is the cdf of standard normal distribution, 
    /// and <m>\Phi^{-1}</m> is the inverse of the normal cdf function. 
    ///</remarks>
    /// 
    /// <param name="cpty">counterparty survival curve</param>
    /// <param name="guarantor">guarantor survival curve</param>
    /// <param name="rho">Correlation coefficient</param>
    /// <param name="dates">The curve dates for the joint curve</param>
    /// <returns>A joint survival curve</returns>
    public static SurvivalCurve JointDefaultCurveMidpointRule(
      SurvivalCurve cpty, SurvivalCurve guarantor, double rho,
      Dt[] dates = null)
    {
      double sqrt = Math.Sqrt(1 - Math.Min(1, rho*rho));
      Dt asOf = cpty.AsOf;
      if (dates == null || dates.Length == 0)
        dates = cpty.Select(p => p.Date).Where(d => d > asOf).ToArray();
      int count = dates.Length;
      var jointSurvivals = new double[count];
      Dt begin = asOf;
      bool cpDefaulted = (!cpty.DefaultDate.IsEmpty()) && cpty.DefaultDate <= asOf;
      double jointDefault = 0.0, cpBeginDefault = cpDefaulted ? 1.0 : 0.0;
      for (int i = 0; i < count; ++i)
      {
        Dt end = dates[i], middle = new Dt(begin, 0.5*(end - begin)/365.0);
        var cpEndDefaultProb = cpty.DefaultProb(asOf, end);
        var cpMiddleDefaultProb = cpty.DefaultProb(asOf, middle);
        var gtMiddleDefaultProb = guarantor.DefaultProb(asOf, middle);
        var cpX = Math.Max(-MaxInverseX, Math.Min(MaxInverseX,
          SpecialFunctions.NormalInverseCdf(cpMiddleDefaultProb)));
        var gtX = Math.Max(-MaxInverseX, Math.Min(MaxInverseX,
          SpecialFunctions.NormalInverseCdf(gtMiddleDefaultProb)));
        var integrand = SpecialFunctions.NormalCdf(Ratio(gtX - rho*cpX, sqrt));
        var delta = integrand*(cpEndDefaultProb - cpBeginDefault);
        jointSurvivals[i] = 1 - Math.Max(0, Math.Min(1, jointDefault += delta));

        // next iteration
        cpBeginDefault = cpEndDefaultProb;
        begin = end;
      }

      return SurvivalCurve.FromProbabilitiesWithCDS(
        asOf, cpty.Ccy, null, cpty.InterpMethod, cpty.ExtrapMethod, dates,
        jointSurvivals, null, null, null, null, null,
        GetRecoveryRates(cpty, dates), double.NaN, true, 1E-15);
    }

    private static double Ratio(double x, double y)
    {
      return x.AlmostEquals(0.0) ? 0.0 : (x/y);
    }

    /// <summary>
    /// Create a joint default curve
    /// </summary>
    /// <param name="cpty">The counterparty curve</param>
    /// <param name="guarantor">The guarantor curve</param>
    /// <param name="correlation">The correlation</param>
    /// <param name="dates">The curve dates for the joint curve</param>
    /// <returns>A joint survival curve</returns>
    public static SurvivalCurve JointDefaultCurve(
      SurvivalCurve cpty, SurvivalCurve guarantor, double correlation,
      Dt[] dates = null)
    {
      const double maxInverseX = 32;
      Dt asOf = cpty.AsOf;
      if (dates == null || dates.Length == 0)
        dates = cpty.Select(p => p.Date).Where(d => d > asOf).ToArray();
      int count = dates.Length;
      var survivalProbabilities = new double[count];

      double cProb = cpty.DefaultProb(asOf), gProb = guarantor.DefaultProb(asOf);
      double cPrev = Math.Max(-maxInverseX, Math.Min(maxInverseX,
        SpecialFunctions.NormalInverseCdf(cProb)));
      double gPrev = Math.Max(-maxInverseX, Math.Min(maxInverseX,
          SpecialFunctions.NormalInverseCdf(gProb)));
      double defaultProb = 0;
      for (int j = 0; j < dates.Length; ++j)
      {
        cProb = cpty.DefaultProb(dates[j]);
        gProb = guarantor.DefaultProb(dates[j]);
        double cNext = Math.Max(-maxInverseX, Math.Min(maxInverseX,
          SpecialFunctions.NormalInverseCdf(cProb)));
        double gNext = Math.Max(-maxInverseX, Math.Min(maxInverseX,
          SpecialFunctions.NormalInverseCdf(gProb)));
        double cPrev_gPrev = SpecialFunctions.BivariateNormalCdf(cPrev, gPrev, correlation);
        double cPrev_gNext = SpecialFunctions.BivariateNormalCdf(cPrev, gNext, correlation);
        double cNext_gPrev = SpecialFunctions.BivariateNormalCdf(cNext, gPrev, correlation);
        double cNext_gNext = SpecialFunctions.BivariateNormalCdf(cNext, gNext, correlation);
        defaultProb += 0.5 * (cNext_gPrev - cPrev_gPrev + cNext_gNext - cPrev_gNext);
        survivalProbabilities[j] = Math.Min(1.0, Math.Max(0.0, 1 - defaultProb));
        cPrev = cNext;
        gPrev = gNext;
      }
      var resultSurvivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        asOf, cpty.Ccy, null, cpty.InterpMethod, cpty.ExtrapMethod, dates,
        survivalProbabilities, null, null, null, null, null,
        GetRecoveryRates(cpty, dates), Double.NaN, false, Double.NaN);
      return resultSurvivalCurve;
    }


    private static double[] GetRecoveryRates(
      SurvivalCurve cpty, Dt[] dates)
    {
      var calibrator = cpty.SurvivalCalibrator;
      if (calibrator == null) return null;
      var rc = calibrator.RecoveryCurve;
      if (rc == null) return null;
      return Array.ConvertAll(dates, d => rc.Interpolate(d));
    }

    private const double MaxInverseX = 32;

    /// <summary>
    ///   The probability that neither the credit nor the counterpaty defaults
    ///   between the settle date and a given date.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>
    ///   Let <formula inline="true">\tau_D</formula> be the (random)
    ///   default time of the credit, <formula inline="true">\tau_P</formula> be the
    ///   (random) default time of the counterpaty.
    ///   This function calculate the probability:
    ///   <formula>
    ///     S(t) \equiv \mathrm{Prob}\{\tau_P \geq t, \tau_P \geq t \}
    ///   </formula>
    ///   where <c>t</c> is the end date and the start date is 0.  The calculation
    ///   is conditional on no default on both the credit and counterparty at time 0.
    ///   </para>
    /// 
    ///   <para>
    ///    For LCDS when <formula inline="true">\tau_P</formula> is interpreted as the
    ///   (random) prepayment time, this function calculate the probaility that
    ///    the credit does not default nor prepays before <c>t</c>.
    ///   </para>
    /// </remarks>
    /// 
    /// <param name="start">
    ///   The start date.
    /// </param>
    /// <param name="end">
    ///   The end date.
    /// </param>
    /// <param name="creditCurve">
    ///   Survival curve of credit, or null if no credit risk on the entity.
    /// </param>
    /// <param name="counterpartyCurve">
    ///   Survival curve of counterparty, or null if nor default risk on the counterparty.
    /// </param>
    /// <param name="correlation">
    ///   Correlation between credit default and counterparty default.
    /// </param>
    /// <param name="stepSize">
    ///   Time grid size used to calculate the probability.
    /// </param>
    /// <param name="stepUnit">
    ///   Time unit of step size.
    /// </param>
    /// 
    /// <returns>
    ///   The probability that neither the credit nor the counterpaty defaults.
    /// </returns>
    public static double OverallSurvivalProbability(
      Dt start, Dt end,
      SurvivalCurve creditCurve,
      SurvivalCurve counterpartyCurve,
      double correlation,
      int stepSize,
      TimeUnit stepUnit)
    {
      if (Dt.Cmp(start, end) > 0)
      {
        throw new System.ArgumentException(String.Format(
          "Start {0} must be later than end {1}", start, end));
      }

      if (creditCurve == null)
      {
        // No credit curve means no risk
        return counterpartyCurve == null ? 1.0 : counterpartyCurve.Interpolate(start, end);
      }
      else if (creditCurve.DefaultDate.IsValid()
        && Dt.Cmp(creditCurve.DefaultDate, start) < 0)
      {
        // Credit already defaulted
        return 0.0;
      }

      if (counterpartyCurve == null)
      {
        // no counterparty risk
        return creditCurve.Interpolate(start, end);
      }
      else if (counterpartyCurve.DefaultDate.IsValid()
        && Dt.Cmp(counterpartyCurve.DefaultDate, start) < 0)
      {
        // Counterparty already defaulted
        return 0.0;
      }

      Curve sc = new Curve();
      Curve pc = new Curve();
      CounterpartyRisk.TransformSurvivalCurves(start, end,
        creditCurve, counterpartyCurve, correlation, sc, pc,
        stepSize, stepUnit);

      return sc.Interpolate(end) + pc.Interpolate(end) - 1;
    }

    #endregion // Probability
  }
}
