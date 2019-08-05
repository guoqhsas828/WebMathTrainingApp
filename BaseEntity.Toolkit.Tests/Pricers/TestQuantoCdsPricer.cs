//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Diagnostics;
using System.Reflection;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Curves;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture(false)]
  [TestFixture(true)]
  public class TestQuantoCdsPricer : ToolkitTestBase
  {
    public TestQuantoCdsPricer(bool withOverlay)
    {
      _withOverlay = withOverlay;
    }

    [Test]
    public void CurveConsistencyNearCorrelationZero()
    {
      const int N = 100;
      for (int i = 0; i <= N; ++i)
        TestCurveConsistency(i * 1.0 / N);
    }

    [Test]
    public void ProtectionParities()
    {
      ProtectionParities(false);
    }

    [Test]
    public void ProtectionParitiesOnDefault()
    {
      ProtectionParities(true);
    }

    [Test]
    public void ForeignPutValue()
    {
      const int N = 100;
      for (int i = 0; i <= N; ++i)
      {
        var sigma = i * 1.0 / N;
        Console.Write(sigma);
        for (int j = -8; j <= 8; ++j)
        {
          var rho = j / 10.0;
          var pricer = GetQuantoCDSPricer(sigma, rho, false);
          var put = ForeignPutValue(pricer);
          Console.Write(',');
          Console.Write(put);
        }
        Console.WriteLine();
      }
    }

    [TestCase(Double.NaN)]
    [TestCase(0.5)]
    [TestCase(1.0)]
    [TestCase(1.5)]
    public void SpotFxEffects(double capLevel)
    {
      const double sigma = 0.2, rho = 0.8;
      var spotRates = new[] { 1.4, 1.6, 1.8, 2, 2.2, 2.4, 2.6, 2.8, 3, 3.2, 3.4, 3.6, 3.8, 4, 4.2, 4.4, 4.6, 4.8, 5, 5.2, 5.4, 5.6, 5.8, 6, 6.2 };
      var inceptFx = 3.2;
      var protections = new double[spotRates.Length];
      for (int i = 0; i < spotRates.Length;++i)
      {
        var spotFx = spotRates[i];
        var pricer = GetQuantoCDSPricer(sigma, rho, false, spotFx, inceptFx);
        if (!Double.IsNaN(capLevel))
          pricer.CDS.QuantoNotionalCap = capLevel;
        var protection = protections[i] = -pricer.ProtectionPv();
        Assert.AreEqual(pricer.FullFeePv() - protection, pricer.ProductPv(), 2E-16);
        var expected = ProtectionPv(pricer, spotFx, inceptFx);
        Assert.AreEqual(expected, protection, 2E-16);
        // Protection is increasing with spot
        if (i > 0 && protections[i - 1] >= protection)
          Assert.Less(protections[i - 1], protection + 1E-16);
        // Foreign value is less than proportional due to cap
        if (i > 0 && protections[i - 1]/spotRates[i-1] <= protection/spotFx)
          Assert.Greater(protections[i - 1] / spotRates[i - 1] + 1E-16, protection / spotFx);
      }
      return;
    }

    private static double ProtectionPv(CDSCashflowPricer pricer, double spotFx, double inceptFx)
    {
      var cds = pricer.CDS;
      var cap = cds.QuantoNotionalCap.HasValue ? cds.QuantoNotionalCap.Value : 1.0;
      var recovery = cds.FixedRecovery ? cds.FixedRecoveryRate : pricer.RecoveryRate;
      recovery = 1 - (1 - recovery) / cap * spotFx / inceptFx;
      cds.FixedRecovery = true;
      cds.FixedRecoveryRate = recovery;
      cds.FxAtInception = null;
      cds.QuantoNotionalCap = null;
      pricer.Reset();
      return -cap * pricer.ProtectionPv();
    }

    private void ProtectionParities(bool defaulted)
    {
      const int N = 100;
      for (int i = 0; i <= N; ++i)
      {
        var sigma = i * 1.0 / N;
        for (int j = -8; j <= 8; ++j)
        {
          var rho = j / 10.0;
          TestProtectionParities(sigma, rho, defaulted);
        }
      }
    }


    private void TestCurveConsistency(double sigma)
    {
      const double rho = QuantoSurvivalCurveUtilities.TinyCorrelation * 10;
      var pricer = GetQuantoCDSPricer(sigma, rho);
      var cds = pricer.CDS;
      var fsc = pricer.SurvivalCurve;
      var dsc = GetDomesticSurvivalCurve(pricer);
      for (Dt dt = fsc.AsOf, lastDate = cds.Maturity; dt <= lastDate; )
      {
        dt = Dt.Add(dt, 5);
        var fsp = fsc.Interpolate(dt);
        var dsp = dsc.Interpolate(dt);
        AssertEqual(String.Format("Survival@{0}/{1}", sigma, dt),
          dsp, fsp, 1E-9);
      }

      var qad0 = GetQuantoCapAdjustment(pricer);
      // calculate using the original curve.
      var grid = pricer.GetPricingTimeGrid();
      var qad1 = fsc.QuantoCapValue(fsc, pricer.AsOf,
        pricer.ProtectionStart, grid, pricer.IncludeMaturityProtection,
        pricer.FxCurve,
        cds.FixedRecovery ? cds.FixedRecoveryRate : pricer.RecoveryRate,
        cds.QuantoNotionalCap, cds.FxAtInception, pricer.FxVolatility,
        pricer.FxCorrelation, pricer.FxDevaluation); //protection seller
      AssertEqual(String.Format("QuantoCapValue@{0}", sigma),
        qad0, qad1, 5E-10);
      return;
    }

    private double ForeignPutValue(CDSCashflowPricer pricer)
    {
      var cds = pricer.CDS;
      var fsc = pricer.SurvivalCurve;
      var dsc = GetDomesticSurvivalCurve(pricer);
      var grid = pricer.GetPricingTimeGrid();
      var lgd = 1 - (cds.FixedRecovery
        ? cds.FixedRecoveryRate : pricer.RecoveryRate);
      return fsc.QuantoValue(dsc, OptionType.Put, pricer.AsOf,
        pricer.ProtectionStart, grid, pricer.IncludeMaturityProtection,
        pricer.FxCurve, dt=>lgd,
        cds.QuantoNotionalCap, cds.FxAtInception, pricer.FxVolatility,
        pricer.FxCorrelation, pricer.FxDevaluation); //protection seller
    }

    private void TestProtectionParities(double sigma, double rho, bool defaulted)
    {
      var pricer = GetQuantoCDSPricer(sigma, rho, false);
      if (defaulted)
      {
        pricer.SurvivalCurve.DefaultDate = pricer.Settle;
        pricer.SurvivalCurve.Defaulted = Defaulted.WillDefault;
      }
      Debug.Assert(pricer.FxCurve.BasisCurve == null);
      var protection0 = -pricer.ProtectionPv();
      var foreignPutValue = ForeignPutValue(pricer);

      var foreignPricer = (CDSCashflowPricer)pricer.ShallowCopy();
      foreignPricer.Product = (Product)pricer.CDS.ShallowCopy();
      foreignPricer.CDS.RecoveryCcy = Currency.None;
      foreignPricer.CDS.QuantoNotionalCap = null;
      foreignPricer.DiscountCurve = pricer.FxCurve.Ccy1DiscountCurve;
      var foreignCdsProtection = -foreignPricer.ProtectionPv();
      var protection1 = foreignCdsProtection - foreignPutValue;
      // We want the parity hold exactly: larger error means some inconsistencies
      // in the algorithms, making it unable to guarantee the parity.
      AssertEqual(String.Format("Protection@v{0}/c{1}", sigma, rho),
        protection0, protection1, 5E-14);
    }

    const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;
    private static SurvivalCurve GetDomesticSurvivalCurve(CDSCashflowPricer pricer)
    {
      return (SurvivalCurve)pricer.GetType().GetProperty(
        "DomesticSurvivalCurve", Bf).GetValue(pricer, new object[0]);
    }
    private static double GetQuantoCapAdjustment(CDSCashflowPricer pricer)
    {
      return (double)pricer.GetType().GetProperty(
        "QuantoCapAdjustment", Bf).GetValue(pricer, new object[0]);
    }

    private CDSCashflowPricer GetQuantoCDSPricer(
      double fxVolatility, double fxCorr,
      bool hasBasis = true, double spotFx = Double.NaN,
      double recoveryFx = 3.2)
    {
      double recovery = 0.4, premium = 0.01;
      Dt effectiveDate = _D("20-Sep-2013"), maturityDate = _D("20-Sep-2018");
      Currency ccy = Currency.MYR, recoveryCcy = Currency.USD;

      var cds = new CDS(effectiveDate, maturityDate, ccy, Dt.Empty,
        premium, DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.KLB, 0, Dt.Empty)
      {
        RecoveryCcy = recoveryCcy,
        Description = "QuantoCDS",
      };
      if (!Double.IsNaN(recoveryFx))
        cds.FxAtInception = recoveryFx;
      cds.Validate();

      Dt pricingDate = _asOf, settleDate = _protectStart;
      if (Double.IsNaN(spotFx)) spotFx = _spotFx;
      var discountCurve = GetDomesticDiscountCurve();
      var fxCurve = GetFxCurve(spotFx, hasBasis,
        discountCurve, GetForeignDiscountCurve());
      var survivalCurve = GetSurvivalCurve();

      var pricer = new CDSCashflowPricer(cds, pricingDate, settleDate,
        discountCurve, null, survivalCurve,
        fxCurve, new VolatilityCurve(_asOf, fxVolatility), fxCorr,
        0.0, 0, TimeUnit.None);
      if (recovery >= 0.0)
      {
        pricer.RecoveryCurve = new RecoveryCurve(pricingDate, recovery);
      }
      pricer.Validate();
      return pricer;
    }

    private FxCurve GetFxCurve(double fxSpot, bool hasBisis,
      DiscountCurve domestic, DiscountCurve foreign)
    {
      return new FxCurve(new FxRate(_asOf, _asOf,
        Currency.USD, Currency.MYR, fxSpot),
        hasBisis ? new DiscountCurve(_asOf, _basis) : null,
        domestic, foreign,
        "USDMYR");
    }
    private DiscountCurve GetDomesticDiscountCurve()
    {
      return new DiscountCurve(_asOf, _rd)
      {
        Ccy = Currency.MYR,
      };
    }
    private DiscountCurve GetForeignDiscountCurve()
    {
      return new DiscountCurve(_asOf, _rf)
      {
        Ccy = Currency.USD,
      };
    }
    private SurvivalCurve GetSurvivalCurve()
    {
      var sc = new SurvivalCurve(new SurvivalFitCalibrator(_asOf))
      {
        Interp = new Weighted(new Const(), new Const()),
        Ccy = Currency.USD,
      };
      var pts = _points;
      for (int i = 0; i < pts.Length; ++i)
        sc.Add(pts[i].Date, pts[i].Value);
      if (_withOverlay) CurveBumpUtilityAccessor.SetUpShiftOverlay(sc);
      return sc;
    }
    private Dt _asOf = _D("25-Nov-2013"), _protectStart = _D("26-Nov-2013");
    private double _rd = 0.05, _rf = 0.03, _basis = 0.001, _spotFx = 3.2;
    private CurvePoint[] _points =
    {
      _P(_D("21-Jun-2014"), 0.998652954953863),
      _P(_D("23-Dec-2014"), 0.996541888599531),
      _P(_D("22-Dec-2015"), 0.988446377196941),
      _P(_D("21-Dec-2016"), 0.975156691838811),
      _P(_D("21-Dec-2017"), 0.953500082757112),
      _P(_D("21-Dec-2018"), 0.928018440551324),
      _P(_D("22-Dec-2020"), 0.869727381885908),
      _P(_D("21-Dec-2023"), 0.803442306733649),
    };
    private static Dt _D(string s)
    {
      return Dt.FromStr(s);
    }

    private static CurvePoint _P(Dt dt, double val)
    {
      return new CurvePoint(dt, val);
    }

    private readonly bool _withOverlay;
  }
}
