// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestSurvivalFitStressed : ToolkitTestBase
  {
    private delegate double PriceFn(Cashflow cf,
      Dt asOf, Dt settle, Curve discountCurve,
      Curve survivalCurve, Curve counterpartyCurve, double correlation,
      int flags, int step, TimeUnit stepUnit, int maturityIndex);

    private static Func<Cashflow, Dt, Dt, Curve, Curve,
      Curve, double, int, int, TimeUnit, int, double> pricefn_
      = DelegateFactory.GetFunc<Cashflow, Dt, Dt, Curve, Curve, Curve,
      double, int, int, TimeUnit, int, double>("Price", typeof(CashflowModel));

    private static readonly SurvivalCurveParameters scparameters 
      = new SurvivalCurveParameters(DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Modified, Calendar.None, 
        InterpMethod.Weighted, ExtrapMethod.Smooth, NegSPTreatment.Allow);

    /// <summary>
    ///   Create a CDS pricer
    /// </summary>
    /// <returns>CDS pricer</returns>
    private static CDSCashflowPricer CreateCDSPricer(Dt asOf, Dt settle, CDS cds)
    {
      const double discountRate = 0.0;
      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate);
      SurvivalCurve survivalCurve = CreateSurvivalCurve(asOf, settle);

      return new CDSCashflowPricer(cds, asOf, settle,
        discountCurve, survivalCurve, null, 0.0, 0, TimeUnit.None);
    }

    /// <summary>
    ///   Create a CDS product
    /// </summary>
    private static CDS CreateCDS(Dt effective, Dt maturity)
    {
      // Get product terms
      Currency ccy = Currency.None;
      DayCount dayCount = scparameters.DayCount;
      BDConvention roll = scparameters.Roll;
      Frequency freq = scparameters.Frequency;
      Calendar calendar = scparameters.Calendar;

      CDS cds = new CDS(effective, maturity, ccy, Dt.Empty,
        0.0, dayCount, freq, roll, calendar, 0.0, Dt.Empty);

      return cds;
    }

    /// <summary>
    ///   Create a survival curve
    /// </summary>
    private static SurvivalCurve CreateSurvivalCurve(Dt asOf, Dt settle)
    {
      const double recoveryRate = 0.4;
      const double hazardRate = 0.05;
      const string tenor = "3M";

      // Get product terms
      Currency ccy = Currency.None;
      DayCount dayCount = scparameters.DayCount;
      BDConvention roll = scparameters.Roll;
      Frequency freq = scparameters.Frequency;
      Calendar calendar = scparameters.Calendar;

      Dt maturity = Dt.CDSMaturity(settle, tenor);
      SurvivalCurve survivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        asOf, ccy, "None",
        BaseEntity.Toolkit.Numerics.InterpMethod.Linear, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
        new Dt[] { maturity },
        new double[] { Math.Exp(-hazardRate * Dt.FractDiff(settle, maturity) / 365) },
        new string[] { tenor },
        new DayCount[] { dayCount }, new Frequency[] { freq },
        new BDConvention[] { roll }, new Calendar[] { calendar },
        new double[] { recoveryRate }, 0.0);
      return survivalCurve;
    }

    static void SetLogLinearApproximation(CDSCashflowPricer pricer, bool on)
    {
      var survivalCurve = pricer.SurvivalCurve;
      var flags = survivalCurve.Flags;
      if (on)
        flags |= CurveFlags.Stressed;
      else
        flags &= ~CurveFlags.Stressed;
      survivalCurve.Flags = flags;
      return;
    }

    private static SurvivalCurve Calibrate(
      Dt asOf, Dt settle,double spread)
    {
      var dcCurve = new DiscountCurve(asOf, 0.04);
      return SurvivalCurve.FitCDSQuotes("Curve" + spread, asOf, settle, Currency.USD, null, CDSQuoteType.ParSpread, 0.0,
                                        scparameters, dcCurve, new[] {"1Y"}, null, new[] {spread}, new[] {0.4}, 0.0,
                                        null, null, 0.0, true);
      
    }

    private static void CalibrateAndRoundTrip(Dt begin, Dt end)
    {
      for (Dt asOf = begin; asOf < end; asOf = Dt.Add(asOf, 1))
      {
        Dt settle = Dt.AddDays(asOf, 1, scparameters.Calendar);
        Dt maturity = Dt.CDSMaturity(settle, "5Y");
        CDS cds = CreateCDS(settle, maturity);
        double[] spreads = {100000, 250000, 500000, 750000, 1000000};
        foreach (var spread in spreads)
        {
          var sc = Calibrate(asOf, settle, spread);
          cds.Premium = spread;
          var pricer = new CDSCashflowPricer(cds, asOf, settle,
            sc.SurvivalCalibrator.DiscountCurve, sc, 0, TimeUnit.None);
          double be = pricer.BreakEvenPremium()*10000;
          Assert.AreEqual(spread, be, 0.01);
        }
      }
      return;
    }

    [Test, Smoke]
    public void CalibrateAndRoundTrip()
    {
      CalibrateAndRoundTrip(new Dt(20091006), new Dt(20101006));
      CalibrateAndRoundTrip(new Dt(20120101), new Dt(20130101));
    }

    [Test, Smoke]
    public void BreakEvenPremium()
    {
      Dt asOf = new Dt(20090929);
      Dt settle = Dt.Add(asOf, 1);
      Dt maturity = Dt.CDSMaturity(settle, "5Y");
      var cds = CreateCDS(settle, maturity);
      var pricer = CreateCDSPricer(asOf, settle, cds);

      double hazardRate = 0.00005, prem1, prem2;
      for (int i = 0; i < 4; ++i)
      {
        hazardRate *= 10;
        pricer.SurvivalCurve.SetVal(0, Math.Exp(-hazardRate/4));

        SetLogLinearApproximation(pricer, false);
        prem1 = pricer.BreakEvenPremium()*10000;

        SetLogLinearApproximation(pricer, true);
        prem2 = pricer.BreakEvenPremium()*10000;

        Assert.AreEqual(prem1, prem2, 3E-4*(1 + prem2),
          "BE@" + (hazardRate * 10000));
      }

      pricer.SurvivalCurve.SetVal(0, 1E-14);

      SetLogLinearApproximation(pricer, false);
      prem1 = pricer.BreakEvenPremium() * 10000;

      SetLogLinearApproximation(pricer, true);
      prem2 = pricer.BreakEvenPremium() * 10000;

      return;
    }

    [Test, Smoke]
    public void RiskyDuration()
    {
      Dt asOf = new Dt(20090929);
      Dt settle = Dt.Add(asOf, 1);
      Dt maturity = Dt.CDSMaturity(settle, "5Y");
      var cds = CreateCDS(settle, maturity);
      var pricer = CreateCDSPricer(asOf, settle, cds);

      double hazardRate = 0.00005, prem1, prem2;
      for (int i = 0; i < 4; ++i)
      {
        hazardRate *= 10;
        pricer.SurvivalCurve.SetVal(0, Math.Exp(-hazardRate/4));

        SetLogLinearApproximation(pricer, false);
        prem1 = pricer.RiskyDuration();

        SetLogLinearApproximation(pricer, true);
        prem2 = pricer.RiskyDuration();

        Assert.AreEqual(prem1, prem2, 2E-4*(1 + prem2),
          "RiskDuration@" + (hazardRate * 10000));
      }
    }

    [Test, Smoke]
    public void Protection()
    {
      Dt asOf = new Dt(20090929);
      Dt settle = Dt.Add(asOf, 1);
      Dt maturity = Dt.CDSMaturity(settle, "5Y");
      var cds = CreateCDS(settle, maturity);
      var pricer = CreateCDSPricer(asOf, settle, cds);

      double hazardRate = 0.00005, prem1, prem2;
      for (int i = 0; i < 4; ++i)
      {
        hazardRate *= 10;
        pricer.SurvivalCurve.SetVal(0, Math.Exp(-hazardRate / 4));

        SetLogLinearApproximation(pricer, false);
        prem1 = -pricer.ProtectionPv();

        SetLogLinearApproximation(pricer, true);
        prem2 = -pricer.ProtectionPv();

        Assert.AreEqual(prem1, prem2, 2E-4 * (1 + prem2),
          "Protection@" + (hazardRate * 10000));
      }
    }

    [Test]
    public void RoundTrip()
    {
      var asOf = Dt.Parse("22-Jan-2015");
      var data = new object[,]
      {
        {"23-Jun-2015", 0.152582698938511},
        {"22-Sep-2015", 0.456320346999284},
        {"22-Mar-2016", 0.308199858819051},
        {"21-Mar-2017", 0.0254437664851538},
        {"21-Mar-2018", 0.0153409882705748},
        {"21-Mar-2019", 0.0170542338288971},
        {"21-Mar-2020", 0.0132262299957455},
        {"23-Mar-2021", 0.0121751361923969},
        {"22-Mar-2022", 0.0107034692945245},
        {"21-Mar-2023", 0.0104858984686911},
        {"21-Mar-2024", 0.00969220744780745},
        {"21-Mar-2025", 0.00922518685798588},
        {"21-Mar-2030", 0.00682794438141413},
        {"21-Mar-2035", 0.00511768275121965},
        {"21-Mar-2045", 0.00282049423894487},
      };
      var pars = new SurvivalCurveParameters(DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Modified, Calendar.NYB,
        InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow,
        false, true);
      var recovery = 0.131;


      var curve = new SurvivalCurve(new SurvivalFitCalibrator(
        asOf, asOf + 1, recovery, new DiscountCurve(asOf, 0.02)))
      {
        Name = "RSH.USD.SeniorUnsecured.Excluded_C1 (CDX.NA.HY.19-V2_CalibratedScaling)",
        Stressed = pars.Stressed,
      };
      var count = data.GetLength(0);
      for (int i = 0; i < count; ++i)
        curve.Add(Dt.Parse((string) data[i, 0]), (double) data[i, 1]);

      var maturites = new Dt[count];
      var spreads = new double[count];
      for (int i = 0; i < count; ++i)
      {
        Dt dt = curve.GetDt(i),
          maturity = maturites[i] = new Dt(20, dt.Month, dt.Year);
        spreads[i] = curve.ImpliedSpread(maturity, pars.DayCount,
          pars.Frequency, pars.Roll, pars.Calendar)*10000.0;
      }

      var refit = SurvivalCurve.FitCDSQuotes("RSH_Refit",
        asOf, asOf + 1, Currency.USD, "", false, CDSQuoteType.ParSpread,
        0.0, pars, curve.SurvivalCalibrator.DiscountCurve, null,
        maturites, spreads, new[] {recovery}, 0, null, null, 0, 0,
        null, true);

      for (int i = 0; i < count; ++i)
        Assert.AreEqual(curve.GetVal(i), refit.GetVal(i), 1E-7, "Point " + i, curve.GetVal(i));
      return;
    }
  }
}
