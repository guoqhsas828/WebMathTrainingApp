//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test CDX Option Pricer
  /// </summary>
  //[TestFixture]
  [TestFixture("TestNTD_CDX.NA.IG.7_Hetrogeneous_HeteroBasket")]
  [TestFixture("TestNTD_CDX.NA.IG.7_Hetrogeneous_HomoBasket")]
  [TestFixture("TestNTD_CDX.NA.IG.7_MonteCarlo")]
  [TestFixture("TestNTD_CDX.NA.IG.7_SemiAnalytic")]
  [TestFixture("TestNTD_FundedFixed_CDX.NA.IG.7_Heterogeneous_HeteroBasket")]
  [TestFixture("TestNTD_FundedFixed_CDX.NA.IG.7_Heterogeneous_HomoBasket")]
  [TestFixture("TestNTD_FundedFixed_CDX.NA.IG.7_SA")]
  [Smoke]
  public class TestNTD : SensitivityTest
  {

    public TestNTD(string name) : base(name) { }

    #region PricingMethods
    [Test, Smoke, Category("PricingMethods")]
    public void ProtectionPv()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).ProtectionPv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FeePvWithPremium()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).FeePv(100.0);
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void UpFrontFeePv()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).UpFrontFeePv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FeePv()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).FeePv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void Pv()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).Pv();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FlatPrice()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).FlatPrice();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void FullPrice()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).FullPrice();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void Accrued()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).Accrued();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void AccruedWithOneNameDefault()
    {
      double[] beSaved = Array.ConvertAll<FTDPricer, double>(
        pricers_, (p) => (p as FTDPricer).ExpectedSurvival());

      using (new CheckStates(true, pricers_))
      {
        SurvivalCurve curve = pricers_[0].SurvivalCurves[0];
        SurvivalCurve savedCurve = CloneUtil.Clone(curve);

        // Set one name default, say the first curve
        curve.DefaultDate = pricers_[0].Settle;

        TestNumeric(pricers_, ntdNames_,
          delegate(object p)
          {
            FTDPricer fp = p as FTDPricer;
            fp.Reset();
            return fp.Accrued();
          });

        // Set back the original curves
        SurvivalCurveSet(curve, savedCurve);
        for (int i = 0; i < pricers_.Length; ++i)
          pricers_[i].Reset();

        double[] be = Array.ConvertAll<FTDPricer, double>(
          pricers_, (p) => (p as FTDPricer).ExpectedSurvival());

        for (int i = 0; i < be.Length; ++i)
          AssertEqual("BE " + (i + 1), beSaved[i], be[i], 1E-8);
      }
    }

    [Test, Smoke, Category("PricingMethods")]
    public void AccruedWithTwoNamesDefault()
    {
      double[] beSaved = Array.ConvertAll<FTDPricer, double>(
        pricers_, (p) => (p as FTDPricer).ExpectedSurvival());

      using (new CheckStates(true, pricers_))
      {
        SurvivalCurve curve0 = pricers_[0].SurvivalCurves[0];
        SurvivalCurve savedCurve0 = CloneUtil.Clone(curve0);
        SurvivalCurve curve1 = pricers_[0].SurvivalCurves[1];
        SurvivalCurve savedCurve1 = CloneUtil.Clone(curve1);

        // Set one name default, say the first curve      
        curve0.DefaultDate = pricers_[0].Settle;
        curve1.DefaultDate = pricers_[0].Settle;

        TestNumeric(pricers_, ntdNames_,
          delegate(object p)
          {
            FTDPricer fp = p as FTDPricer;
            fp.Reset();
            return fp.Accrued();
          });

        // Set back the original curves
        SurvivalCurveSet(curve0, savedCurve0);
        SurvivalCurveSet(curve1, savedCurve1);
        for (int i = 0; i < pricers_.Length; ++i)
          pricers_[i].Reset();

        double[] be = Array.ConvertAll<FTDPricer, double>(
          pricers_, (p) => (p as FTDPricer).ExpectedSurvival());

        for (int i = 0; i < be.Length; ++i)
          AssertEqual("BE " + (i + 1), beSaved[i], be[i], 1E-8);
      }
    }

    [Test, Smoke, Category("PricingMethods")]
    public void AccruedWithThreeNamesDefault()
    {
      double[] beSaved = Array.ConvertAll<FTDPricer, double>(
        pricers_, (p) => (p as FTDPricer).ExpectedSurvival());

      using (CheckStates check = new CheckStates(true, pricers_))
      {
        SurvivalCurve curve0 = pricers_[0].SurvivalCurves[0];
        SurvivalCurve savedCurve0 = CloneUtil.Clone(curve0);
        SurvivalCurve curve1 = pricers_[0].SurvivalCurves[1];
        SurvivalCurve savedCurve1 = CloneUtil.Clone(curve1);
        SurvivalCurve curve2 = pricers_[0].SurvivalCurves[2];
        SurvivalCurve savedCurve2 = CloneUtil.Clone(curve2);

        // Set one name default, say the first curve      
        curve0.DefaultDate = pricers_[0].Settle;
        curve1.DefaultDate = pricers_[0].Settle;
        curve2.DefaultDate = pricers_[0].Settle;

        TestNumeric(pricers_, ntdNames_,
          delegate(object p)
          {
            FTDPricer fp = p as FTDPricer;
            fp.Reset();
            return fp.Accrued();
          });

        // Set back the original curves
        SurvivalCurveSet(curve0, savedCurve0);
        SurvivalCurveSet(curve1, savedCurve1);
        SurvivalCurveSet(curve2, savedCurve2);
        for (int i = 0; i < pricers_.Length; ++i)
          pricers_[i].Reset();

        double[] be = Array.ConvertAll<FTDPricer, double>(
          pricers_, (p) => (p as FTDPricer).ExpectedSurvival());

        for (int i = 0; i < be.Length; ++i)
          AssertEqual("BE " + (i + 1), beSaved[i], be[i], 1E-8);
      }
    }

    private static void SurvivalCurveSet(SurvivalCurve dst, SurvivalCurve src)
    {
      dst.Defaulted = src.Defaulted;
      dst.Deterministic = src.Deterministic;
      dst.Interp = src.Interp;
      dst.Spread = src.Spread;
      dst.Ccy = src.Ccy;
      dst.Set(src);
      return;
    }

    [Test, Smoke, Category("PricingMethods")]
    public void BreakEvenPremium()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).BreakEvenPremium();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void RiskyDuration()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).RiskyDuration();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void ExpectedLoss()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).ExpectedLoss();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void ExpectedSurvival()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).ExpectedSurvival();
        });
    }

    [Test, Smoke, Category("PricingMethods")]
    public void ImpliedCorrelation()
    {
      TestNumeric(pricers_, ntdNames_,
        delegate(object p)
        {
          return ((FTDPricer)p).ImpliedCorrelation();
        });
    }


    [Test, Smoke, Category("PricingMethods")]
    public void BreakEvenWithNegativeNotional()
    {
      for(int i = 0;i<pricers_.Length;++i)
      {
        FTDPricer pricer = pricers_[i];
        double originalBE = pricer.BreakEvenPremium();
        //reverse direction of trade
        pricer.Notional *= -1;
        double oppositeBE = pricer.BreakEvenPremium();
        //revert it back
        pricer.Notional *= -1;
        AssertEqual(ntdNames_[i], originalBE, oppositeBE);
      }
    }

    [Test, Smoke, Category("PricingMethods")]
    public void Test_ReferenceDifferentFromDiscount()
    {
      FTDPricer pricer = (FTDPricer) pricers_[0].Clone();
      pricer.RateResets.Add(new RateReset(new Dt(1,1,2000),0.03));
      pricer.FTD.NtdType = Toolkit.Base.NTDType.FundedFloating;
      pricer.FTD.Premium = 0.005;
      double pv = pricer.Pv();
      pricer.ReferenceCurve = pricer.DiscountCurve;
      double pvR = pricer.Pv();
      AssertEqual("pv", pv, pvR, 1e-16);
    }

    #endregion // PricingMethods

    #region SummaryRiskMethods
    [Test, Category("SummaryRiskMethods")]
    public void Spread01()
    {
      Spread01(pricers_, ntdNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadGamma()
    {
      SpreadGamma(pricers_, ntdNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadHedge()
    {
      SpreadHedge(pricers_, ntdNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void IR01()
    {
      IR01(pricers_, ntdNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Recovery01()
    {
      Recovery01(pricers_, ntdNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void VOD()
    {
      VOD(pricers_, ntdNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Theta()
    {
      Theta(pricers_, ntdNames_);
    }
    #endregion //SummaryRiskMethods

    #region RiskMethods
    [Test, Category("RiskMethods")]
    public void SpreadSensitivity()
    {
      Spread(pricers_);
    }

    [Test, Category("RiskMethods")]
    public void RateSensitivity()
    {
      Rate(pricers_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(pricers_);
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity()
    {
      Recovery(pricers_);
    }

    [Test, Category("RiskMethods")]
    public void CorrelationSensitivity()
    {
      Correlation(pricers_);
    }
    #endregion // RiskMethods

    #region SetUp
    [OneTimeSetUp]
    public void Initialize()
    {
      DiscountCurve discountCurve = LoadDiscountCurve(LiborDataFile);
      SurvivalCurve[] survivalCurves = LoadCreditCurves(CreditDataFile, discountCurve);
      string[] names = ParseString(this.CurveNames);
      survivalCurves = Select(survivalCurves, names);

      // Check MakeHeteroGeneousBasket to modify the recovery rates
      if(MakeHeteroGeneousBasket)
      {
        double recov = survivalCurves[0].SurvivalCalibrator.RecoveryCurve.Points[0].Value;
        double deltaRecov = 0.05;
        int size = survivalCurves.Length;
        for(int i = 0; i < size; i++)
        {
          survivalCurves[i].SurvivalCalibrator.RecoveryCurve.Set(
            0, survivalCurves[i].SurvivalCalibrator.RecoveryCurve.Points[0].Date, recov + (i-size/2)*deltaRecov);
          survivalCurves[i].Fit();
        }
      }
      double[] principals = ParseDouble(this.Principals);
      CorrelationObject corr = new SingleFactorCorrelation(names, Math.Sqrt(correlation_));
      corr = CorrelationFactory.CreateFactorCorrelation((Correlation)corr);
      Toolkit.Base.Copula copula = GetCopula();

      double[] premia = ParseDouble(this.Premiums);
      int[] firsts = ParseInt(this.CoverStarts);
      int[] covers = ParseInt(this.CoverSpans);
      double[] notional = ParseDouble(Notionals);

      Dt effective = ToDt(this.EffectiveDate);
      Dt maturity = ToDt(this.MaturityDate);
      Dt firstPrem = ToDt(this.FirstPremDate);

      // Create product
      FTD[] ntd = CreateProducts(premia, effective, firstPrem, maturity, firsts, covers);
      
      Dt pricingDate = ToDt(PricingDate);
      Dt settleDate = ToDt(SettleDate);
      Dt portfolioStart = new Dt();

      // Create pricers
      CreatePricers(ntd, portfolioStart, pricingDate, settleDate, 
        discountCurve, survivalCurves, principals, copula, corr, notional);
      return;
    }

    // Create the NTD products
    public FTD[] CreateProducts(double[] premia, Dt effective, Dt firstPrem, Dt maturity, int[] firsts, int[] covers)
    {
      FTD[] ntd = new FTD[premia.Length];
      for (int i = 0; i < premia.Length; ++i)
      {
        FTD ftd = new FTD(effective, maturity,
          Get(Currency.USD), (double)premia[i] / 10000.0,
          Get(DayCount.Actual360), Get(Frequency.Quarterly),
          Get(BDConvention.Following), Get(Calendar.NYB));
        if (!firstPrem.IsEmpty())
          ftd.FirstPrem = firstPrem;
        ftd.First = (int)firsts[i];
        ftd.NumberCovered = (int)covers[i];
        ftd.NtdType = NTDType;
        ntd[i] = ftd;
      }
      return ntd;
    }

    // Create FTDPricers
    public void CreatePricers(FTD[] ntd, Dt portfolioStart, Dt pricingDate, Dt settleDate,
      DiscountCurve discountCurve, SurvivalCurve[] survivalCurves, double[] principals,
      Toolkit.Base.Copula copula, CorrelationObject corr, double[] notional)
    {
      int stepSize = 0;
      TimeUnit stepUnit = TimeUnit.None;
      GetTimeGrid(ref stepSize, ref stepUnit);
      int seed = 0;
      if (PricerType != null && PricerType.Contains("MonteCarlo"))
        pricers_ = BasketPricerFactory.NTDPricerMonteCarlo(
          ntd, portfolioStart, pricingDate, settleDate,
          discountCurve, survivalCurves, principals, copula, corr,
          stepSize, stepUnit, SampleSize, notional, seed);
      else
        pricers_ = BasketPricerFactory.NTDPricerSemiAnalytic(
          ntd, portfolioStart, pricingDate, settleDate,
          discountCurve, survivalCurves, principals, copula, corr,
          stepSize, stepUnit, QuadraturePoints, notional);

      if (pricers_ == null)
        throw new System.NullReferenceException("NTD Pricers not available");
      ntdNames_ = new string[pricers_.Length];
      for (int i = 0; i < ntdNames_.Length; ++i)
      {
        pricers_[i].Basket.HeteroGeneousThreshold = HeteroGeneousThreshold;
        ntdNames_[i] = pricers_[i].FTD.Description;
      }
    }

    #endregion // SetUp

    #region Properties
    /// <summary>
    ///   Data for dicount curve
    /// </summary>
    public string LiborDataFile { get; set; } = "data/USD.LIBOR_Data.xml";

    /// <summary>
    ///   Data for credit names
    /// </summary>
    public string CreditDataFile { get; set; } = "data/CDX.NA.IG.7-V1_CreditData.xml";

    /// <summary>
    ///   Premia
    /// </summary>
    public string Premiums { get; set; } = null;

    public string CoverStarts { get; set; } = null;

    public string CoverSpans { get; set; } = null;

    public string PricerType { get; set; } = null;

    public string CurveNames { get; set; } = null;

    public string Principals { get; set; } = null;

    public string Notionals { get; set; } = null;

    public NTDType NTDType { get; set; } = NTDType.Unfunded;

    /// <summary>
    ///  Get the threshold for heterogeneous NTD basket
    /// </summary>
    public double HeteroGeneousThreshold { get; set; } = 1;

    /// <summary>
    /// Test homogeneous basket or heterogeneous basket
    /// False = get a homogeneous basket
    /// True = get a heterogeneous basket 
    /// </summary>
    public bool MakeHeteroGeneousBasket { get; set; } = false;

    #endregion //Properties

    #region Data
    // Accuracy
    const double epsilon = 1.0E-7;

    // Interest rate and credit data

    // NTD data: comma separated

    // pricer data
    private double correlation_ = 0.3;

    // pricers
    private FTDPricer[] pricers_ = null;
    private string[] ntdNames_ = null;

    #endregion // Data
  }
}
