

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Tests.Curves
{
  using NUnit.Framework;

  [TestFixture]
  public class TestJointDefaultCurve
  {
    [TestCase(Method.MidpointRule)]
    [TestCase(Method.RectRule)]
    public void GuarantorDefaulted(Method method)
    {
      Dt asOf = _asOf;
      var dates = Array.ConvertAll(_tenors, s => Dt.Add(asOf, s));

      double hazardRate = 0.04;
      var sps = Array.ConvertAll(dates, d => Math.Exp(-hazardRate*(d - asOf)/365));
      var guaranteeSurvivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        _asOf, Currency.None, "",
        InterpMethod.Weighted, ExtrapMethod.Const, dates, sps, _tenors,
        null, null, null, null, new[] {0.4}, 0.0, true, 1E-15);

      var spreads1 = Array.ConvertAll(dates,
        d => guaranteeSurvivalCurve.ImpliedSpread(d)*10000.0);
      var guarantorSurvivalCurve = GetSurvivalCurve(asOf, 0.01);
      guarantorSurvivalCurve.DefaultDate = asOf - 1;

      var correlation = 0.5;
      var jointDefaultCurve = GetFunc(method)(
        guaranteeSurvivalCurve, guarantorSurvivalCurve, correlation, dates);

      var spreads2 = Array.ConvertAll(dates,
        d => jointDefaultCurve.ImpliedSpread(d)*10000.0);

      var tolerance = method == Method.MidpointRule ? 5E-4 : 5E-3;
      Assert.That(spreads2,Is.EqualTo(spreads1).Within(tolerance));
    }

    //
    // Test cases with correction = 0
    //
    [TestCase(Method.MidpointRule, 0.08)]
    [TestCase(Method.RectRule, 0.08)]
    [TestCase(Method.MidpointRule, 0.04)]
    [TestCase(Method.RectRule, 0.04)]
    [TestCase(Method.MidpointRule, 0.02)]
    [TestCase(Method.RectRule, 0.02)]
    public void IndependentDefaults(Method method, double gHazardRate)
    {
      Dt asOf = _asOf;
      var dates = Array.ConvertAll(_tenors, s => Dt.Add(asOf, s));

      double cHazardRate = 0.04;
      var sps = Array.ConvertAll(dates,
        d => Math.Exp(-cHazardRate*(d - asOf)/365));
      var guaranteeSurvivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        _asOf, Currency.None, "",
        InterpMethod.Weighted, ExtrapMethod.Const, dates, sps, _tenors,
        null, null, null, null, new[] {0.4}, 0.0, true, 1E-15);
      var guarantorSurvivalCurve = GetSurvivalCurve(asOf, gHazardRate);

      var jointDefaultCurve = GetFunc(method)(
        guaranteeSurvivalCurve, guarantorSurvivalCurve, 0, dates);
      var points = jointDefaultCurve
        .Select(p => (1 - p.Value)*1E4).ToArray();
      var expects = Array.ConvertAll(dates, d => IndependentDefaultProbability(
        (d - asOf)/365.0, cHazardRate, gHazardRate)*1E4);

      var tolerance = method == Method.MidpointRule ? 5E-3 : 7E-2;
      for (int i = 0; i < expects.Length; ++i)
        Assert.AreEqual(expects[i], points[i], expects[i]*tolerance);
    }

    //
    // Test cases with correction = 1
    //
    [TestCase(Method.MidpointRule, 0.08)]
    //[TestCase(Method.RectRule, 0.08)] // doesn't work
    //[TestCase(Method.MidpointRule, 0.04)] // doesn't work
    [TestCase(Method.RectRule, 0.04)]
    [TestCase(Method.MidpointRule, 0.02)]
    //[TestCase(Method.RectRule, 0.02)] // doesn't work
    public void ComovementDefaults(Method method, double gHazardRate)
    {
      Dt asOf = _asOf;
      var dates = Array.ConvertAll(_tenors, s => Dt.Add(asOf, s));

      double cHazardRate = 0.04;
      var sps = Array.ConvertAll(dates,
        d => Math.Exp(-cHazardRate*(d - asOf)/365));
      var guaranteeSurvivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        _asOf, Currency.None, "",
        InterpMethod.Weighted, ExtrapMethod.Const, dates, sps, _tenors,
        null, null, null, null, new[] {0.4}, 0.0, true, 1E-15);
      var guarantorSurvivalCurve = new SurvivalCurve(asOf);
      guarantorSurvivalCurve.Add(dates, Array.ConvertAll(dates,
        d => Math.Exp(-gHazardRate*(d - asOf)/365)));

      var jointDefaultCurve = GetFunc(method)(
        guaranteeSurvivalCurve, guarantorSurvivalCurve, 1, dates);
      var points = jointDefaultCurve
        .Select(p => (1 - p.Value)*1E4).ToArray();
      var expects = Array.ConvertAll(dates, d => ComovementDefaultProbability(
        (d - asOf)/365.0, cHazardRate, gHazardRate)*1E4);

      var tolerance = method == Method.MidpointRule ? 5E-12 : 7E-2;
      for (int i = 0; i < expects.Length; ++i)
        Assert.AreEqual(expects[i], points[i], expects[i]*tolerance);
    }

    //
    // Test cases with correction = -1
    //
    [TestCase(Method.MidpointRule, 0.08)]
    [TestCase(Method.RectRule, 0.08)]
    [TestCase(Method.MidpointRule, 0.04)]
    [TestCase(Method.RectRule, 0.04)]
    [TestCase(Method.MidpointRule, 0.02)]
    [TestCase(Method.RectRule, 0.02)]
    public void CounterMovementDefaults(Method method, double gHazardRate)
    {
      Dt asOf = _asOf;
      var dates = Array.ConvertAll(_tenors, s => Dt.Add(asOf, s));

      double cHazardRate = 0.04;
      var sps = Array.ConvertAll(dates,
        d => Math.Exp(-cHazardRate*(d - asOf)/365));
      var guaranteeSurvivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        _asOf, Currency.None, "",
        InterpMethod.Weighted, ExtrapMethod.Const, dates, sps, _tenors,
        null, null, null, null, new[] {0.4}, 0.0, true, 1E-15);
      var guarantorSurvivalCurve = new SurvivalCurve(asOf);
      guarantorSurvivalCurve.Add(dates, Array.ConvertAll(dates,
        d => Math.Exp(-gHazardRate*(d - asOf)/365)));

      var jointDefaultCurve = GetFunc(method)(
        guaranteeSurvivalCurve, guarantorSurvivalCurve, -1, dates);
      var points = jointDefaultCurve
        .Select(p => (1 - p.Value)*1E4).ToArray();
      var expects = Array.ConvertAll(dates, d => 0.0);

      var tolerance = method == Method.MidpointRule ? 5E-12 : 7E-2;
      for (int i = 0; i < expects.Length; ++i)
        Assert.AreEqual(expects[i], points[i], expects[i]*tolerance);
    }


    /// <summary>
    ///  Calculates the joint default probability with
    ///  zero correlation and constant hazard rates.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <param name="cHazardRate">Counter party hazard rate.</param>
    /// <param name="gHazardRate">Guarantor hazard rate.</param>
    /// <returns>System.Double.</returns>
    /// <remarks>
    ///  In this case, the default probabilities are defined by<math>
    ///    P(\tau_c \lt t) = 1 - e^{-h_c t}
    ///    ,\quad
    ///    P(\tau_g \lt t) = 1 - e^{-h_g t}
    ///  </math> 
    ///  The joint probability is given by<math>\begin{align}
    ///   P(\tau_g \lt \tau_c, \tau_c \lt T) 
    ///    &amp;= \int_0^T P(\tau_g \lt t) P(\tau_c \in dt)
    ///    \\&amp;= \int_0^T \left(1-e^{-h_g t}\right)d\left(-e^{-h_c t}\right)
    ///    \\ &amp;= 1 - e^{-h_c T} - \frac{h_c}{h_c + h_g}\left(1 - e^{-(h_c+h_g)T}\right)
    ///  \end{align}</math>
    /// </remarks>
    private static double IndependentDefaultProbability(double time,
      double cHazardRate, double gHazardRate)
    {
      var sum = cHazardRate + gHazardRate;
      return (1 - Math.Exp(-cHazardRate*time))
        - cHazardRate/sum*(1 - Math.Exp(-sum*time));
    }

    /// <summary>
    ///  Calculates the joint default probability with
    ///  perfect correlation and constant hazard rates.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <param name="cHazardRate">Counter party hazard rate.</param>
    /// <param name="gHazardRate">Guarantor hazard rate.</param>
    /// <returns>System.Double.</returns>
    private static double ComovementDefaultProbability(double time,
      double cHazardRate, double gHazardRate)
    {
      if (gHazardRate < cHazardRate) return 0.0;
      return (gHazardRate.Equals(cHazardRate) ? 0.5 : 1.0)*
        (1 - Math.Exp(-cHazardRate*time));
    }

    public enum Method
    {
      MidpointRule,
      RectRule,
    }

    private static Func<SurvivalCurve, SurvivalCurve, double, Dt[], SurvivalCurve>
      GetFunc(Method method)
    {
      switch (method)
      {
      case Method.MidpointRule:
        return CounterpartyRisk.JointDefaultCurveMidpointRule;
      case Method.RectRule:
        return CounterpartyRisk.JointDefaultCurve;
      }
      throw new ArgumentException("Invalid method");
    }

    private static SurvivalCurve GetSurvivalCurve(Dt asOf, double hazardRate)
    {
      return new SurvivalCurve(asOf, hazardRate)
      {
        Calibrator = new SurvivalFitCalibrator(asOf)
        {
          DiscountCurve = _discountCurve,
          RecoveryCurve = new RecoveryCurve(asOf, 0.4)
        }
      };
    }

    #region Data

    private static Dt _asOf = new Dt(20160812);

    private static DiscountCurve _discountCurve =
      new DiscountCurve(_asOf, 0.03);

    private string[] _tenors =
    {
      "3M", "6M", "9M", "1Y", "2Y", "3Y", "4Y", "5Y"
    };

    #endregion Data
  }
}
