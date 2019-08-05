// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestSurvivalCurveSmoothExtrapFit
  {

    [TestCase(true)]
    [TestCase(false)]
    public void TestSensitivity(bool stressed)
    {
      var maturity = Dt.CDSMaturity(AsOf, Tenor.Parse("6M"));
      var survivalCurve1 = FitCdsQuotes(AsOf, ExtrapMethod.Const, stressed);
      var survivalCurve2 = FitCdsQuotes(AsOf, ExtrapMethod.Smooth, stressed);
      var cds = CreateCds(AsOf, maturity);
      var pricer1 = GetCdsPricer(AsOf, survivalCurve1, cds);
      var pricer2 = GetCdsPricer(AsOf, survivalCurve2, cds);
      var gamma1 = Sensitivities.SpreadGamma(pricer1, "Pv",
        QuotingConvention.CreditSpread, 1, 1, null);
      var gamma2 = Sensitivities.SpreadGamma(pricer2, "Pv",
        QuotingConvention.CreditSpread, 1, 1, null);
      Assert.AreEqual(gamma1, gamma2, 1e-8);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TestBumpInPlaceAndOverlay(bool stressed)
    {
      var maturity = Dt.CDSMaturity(AsOf, Tenor.Parse("6M"));
      var survivalCurve = FitCdsQuotes(AsOf, ExtrapMethod.Smooth, stressed);
      var cds = CreateCds(AsOf, maturity);
      var pricer = GetCdsPricer(AsOf, survivalCurve, cds);
      
      var delta1 = CalcDelta(pricer, BumpFlags.BumpInPlace);
      var delta2 = CalcDelta(pricer, BumpFlags.None);
      Assert.AreEqual(delta1, delta2, 5e-8);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TestSmoothRoundTrip(bool stressed)
    {
      for (int i = 0; i < Tenors.Length; i++)
      {
        var maturity = Dt.CDSMaturity(AsOf, Tenor.Parse(Tenors[i]));
        var survivalCurve = FitCdsQuotes(AsOf, ExtrapMethod.Smooth, stressed);
        var cds = CreateCds(AsOf, maturity);
        var pricer = GetCdsPricer(AsOf, survivalCurve, cds);
        var premium = pricer.BreakEvenPremium() * 10000;
        Assert.AreEqual(premium, Spreads[i], 6E-4);
      }
    }

    private static double CalcDelta(IPricer pricer, BumpFlags flag)
    {
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, "Pv", null,
        BumpTarget.CreditQuotes, 1.0, 1.0, BumpType.Uniform,
        flag, null, true, true, null, false, false, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    private static SurvivalCurve FitCdsQuotes(Dt asOf, ExtrapMethod eMethod, bool stressed)
    {
      Dt settle = Dt.Add(asOf, 1);
      var paras = GetParas(eMethod, stressed);
      var curve = SurvivalCurve.FitCDSQuotes("SurvivalCurve", asOf, settle,
        Currency.USD, "None", true, CDSQuoteType.ParSpread, 500.0/*running premium*/,
        paras, DisCurve, Tenors, null, Spreads, new[] {0.4}, 0.0, null, 
        null, 0.0, 0.4, null, false);
      return curve;
    }

    private static CDSCashflowPricer GetCdsPricer(Dt asOf, 
      SurvivalCurve survivalCurve, CDS cds)
    {
      Dt settle = Dt.Add(asOf, 1);
      return new CDSCashflowPricer(cds, asOf, settle,
        DisCurve, survivalCurve, null, 0.0, 0, TimeUnit.None);
    }

    /// <summary>
    ///   Create a CDS product
    /// </summary>
    private static CDS CreateCds(Dt effective, Dt maturity)
    {
      CDS cds = new CDS(effective, maturity, Currency.None, Dt.Empty,
        0.0, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, 
        Calendar.None, 0.0, Dt.Empty);

      return cds;
    }

    private static SurvivalCurveParameters GetParas(ExtrapMethod eMethod, bool stressed)
    {
      return new SurvivalCurveParameters(DayCount.Actual360, Frequency.Quarterly, 
        BDConvention.Following, Calendar.None, InterpMethod.Weighted, eMethod, 
        NegSPTreatment.Allow, false, stressed);
    }

    private static readonly Dt AsOf = new Dt(20160201);
    private static readonly string[] Tenors = { "6M", "1Y", "3Y", "7Y", "10Y" };
    private static readonly double[] Spreads = { 1574.0, 1694.0, 2708.0, 2700.0, 2617.0 };
    private static readonly DiscountCurve DisCurve = new DiscountCurve(AsOf, 0.01);

  }
}
