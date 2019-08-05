//
// Copyright (c)    2002-2018. All rights reserved.
//
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  ///   Test LCDS/LCDX/LCDO models
  /// </summary>
  [TestFixture("Test LCDS Calibration")]
  [TestFixture("TestLCDSModels")]
  [Smoke]
  public class TestLCDSModels : ToolkitTestBase
  {
    public TestLCDSModels(string name) : base(name) {}
    #region SetUp
    [OneTimeSetUp]
    public void SetUp()
    {
      // Get the user input dates
      if (this.PricingDate == 0)
      {
        asOf_ = Dt.Today();
        settle_ = Dt.Add(asOf_, 1);
      }
      else
      {
        asOf_ = new Dt(this.PricingDate);
        settle_ = this.SettleDate == 0 ? Dt.Add(asOf_, 1) : new Dt(this.SettleDate);
      }

      effective_ = this.EffectiveDate == 0 ? settle_ : new Dt(this.EffectiveDate);
      maturity_ = this.MaturityDate == 0 ?
        Dt.CDSMaturity(effective_, tenor_) : new Dt(this.MaturityDate);

      GetTimeGrid(ref stepSize_, ref stepUnit_);
      if (stepSize_ == 0 || stepUnit_ == TimeUnit.None)
      {
        stepSize_ = 0;
        stepUnit_ = TimeUnit.None;
      }

      if (Tenors == null)
        Tenors = new string[] { tenor_ };
      maturities_ = new Dt[Tenors.Length];
      for (int i = 0; i < Tenors.Length; ++i)
        maturities_[i] = Dt.CDSMaturity(effective_, Tenors[i]);

      if (Spreads == null)
      {
        // Calculate the LCDS spread from hazard rate
        double spread = AnalyticLcdsSpread() * 10000;
        Spreads = new double[Tenors.Length];
        for (int i = 0; i < Tenors.Length; ++i)
          Spreads[i] = spread;
      }

      discountCurve_ = GetDiscountCurve();
      return;
    }

    private ICDSPricer GetCDSPricer(Dt maturity, bool isLCDS)
    {
      return GetCDSPricer(maturity, isLCDS, stepSize_, stepUnit_);
    }

    private ICDSPricer GetCDSPricer(Dt maturity, bool isLCDS, int stepSize, TimeUnit stepUnit)
    {
      CDS cds = GetCDS(maturity, isLCDS);
      SurvivalCurve survivalCurve = GetCDSCurve(discountCurve_, isLCDS, PrepayRate);

      return isLCDS
        ? new LCDSCashflowPricer((LCDS)cds, asOf_, settle_,
          discountCurve_, survivalCurve, stepSize, stepUnit)
        : new CDSCashflowPricer(cds, asOf_, settle_,
          discountCurve_, survivalCurve, stepSize, stepUnit);
    }

    private CDS GetCDS(Dt maturity, bool isLCDS)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      return isLCDS ? new LCDS(effective_, maturity, ccy, 0, dayCount, freq, roll, calendar)
        : new CDS(effective_, maturity, ccy, 0, dayCount, freq, roll, calendar);
    }

    private SyntheticCDO GetLCDO(Dt maturity)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      SyntheticCDO cdo = new SyntheticCDO(effective_, maturity, ccy, 0.0, dayCount, freq, roll, calendar);
      return cdo;
    }

    private SurvivalCurve GetCDSCurve(DiscountCurve discountCurve, bool isLCDS, double prepayRate)
    {
      // The discount curve
      SurvivalCurve prepayCurve = GetPrepayCurve(prepayRate);

      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      SurvivalCurve curve = isLCDS
        ? SurvivalCurve.FitLCDSQuotes(
          asOf_, ccy, "None", dayCount, freq, roll, calendar,
          BaseEntity.Toolkit.Numerics.InterpMethod.Weighted,
          BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
          NegSPTreatment.Allow, discountCurve,
          Tenors, maturities_,
          new double[] { 0.0 }, Spreads,
          new double[] { RecoveryRate }, 0.0,
          false, null, prepayCurve, Correlation)
        : SurvivalCurve.FitCDSQuotes(
          asOf_, ccy, "None", dayCount, freq, roll, calendar,
          BaseEntity.Toolkit.Numerics.InterpMethod.Weighted,
          BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
          NegSPTreatment.Allow, discountCurve,
          Tenors, maturities_,
          new double[] { 0.0 }, Spreads,
          new double[] { RecoveryRate }, 0.0,
          false, null);
      if (stepSize_ != 0 || stepUnit_ != TimeUnit.None)
      {
        SurvivalFitCalibrator calibrator = (SurvivalFitCalibrator)curve.SurvivalCalibrator;
        calibrator.StepSize = stepSize_;
        calibrator.StepUnit = stepUnit_;
        curve.ReFit(0);
      }

      return curve;
    }

    private DiscountCurve GetDiscountCurve()
    {
      return new DiscountCurve(asOf_, DiscountRate);
    }

    private SurvivalCurve GetPrepayCurve(double prepayRate)
    {
      return new SurvivalCurve(asOf_, prepayRate);
    }

    private double AnalyticLcdsSpread()
    {
      DayCount dayCount = Get(DayCount.Actual360);
      Frequency freq = Get(Frequency.Quarterly);
      double halfPeriod = 0.5 / (int)freq;
      Dt start = effective_;
      Dt end = new Dt(effective_, halfPeriod);
      double fraction = Dt.Fraction(start, end, dayCount);
      return hazardRate_ * (1 - RecoveryRate)
        / (1 + fraction * (hazardRate_ + PrepayRate + DiscountRate / (int)freq) );
    }

    #endregion // SetUp

    #region Tests

    [Test, Smoke]
    public void SurvivalProbability()
    {
      SurvivalCurve lcdsCurve = GetCDSCurve(discountCurve_, true, 0.0);
      SurvivalCurve cdsCurve  = GetCDSCurve(discountCurve_, false, 0.0);
      for (int t = 0; t < maturities_.Length; ++t)
      {
        double expect = cdsCurve.GetVal(t);
        double actual = lcdsCurve.GetVal(t);
        AssertEqual(Tenors[t], expect, actual, 1E-6);
      }
      return;
    }

    [Test, Smoke]
    public void ConsistencyCDSvsLCDS()
    {
      for (int t = 0; t < maturities_.Length; ++t)
      {
        Dt maturity = maturities_[t];
        ICDSPricer lcdsPricer = GetCDSPricer(maturity, true);
        ICDSPricer cdsPricer = GetCDSPricer(maturity, false);

        // expected spread
        double expect = Spreads[t];

        // Should recover the spread of LCDS curve
        double actual = lcdsPricer.BreakEvenPremium() * 10000;
        AssertEqual(Tenors[t] + " LCDS Spread", expect, actual, 2E-6*expect);

        // Should recover the spread of LCDS curve
        actual = cdsPricer.BreakEvenPremium() * 10000;
        AssertEqual(Tenors[t] + " CDS Spread", expect, actual, 2E-6*expect);

        if (PrepayRate == 0)
        {
          SurvivalCurve cdsCurve = cdsPricer.SurvivalCurve;
          SurvivalCurve lcdsCurve = lcdsPricer.SurvivalCurve;

          lcdsPricer.SurvivalCurve = cdsCurve;
          actual = lcdsPricer.BreakEvenPremium() * 10000;
          AssertEqual(Tenors[t] + " LCDS1 Spread", expect, actual, 2E-6*expect);

          cdsPricer.SurvivalCurve = lcdsCurve;
          actual = cdsPricer.BreakEvenPremium() * 10000;
          AssertEqual(Tenors[t] + " CDS1 Spread", expect, actual, 2E-6*expect);
        }
      }
      return;
    }

    [Test, Smoke]
    public void ConsistencyLCDOvsLCDS()
    {
      Dt maturity = maturity_;

      int stepSize = stepSize_;
      TimeUnit stepUnit = stepUnit_;
      if (stepSize == 0 || stepUnit == TimeUnit.None)
      {
        stepSize = 3; stepUnit = TimeUnit.Months;
      }

      // Construct a single name basket
      ICDSPricer lcdsPricer = GetCDSPricer(maturity, true, stepSize, stepUnit);

      // Construct an equivalent LCDO pricer
      SyntheticCDOPricer lcdoPricer = BasketPricerFactory.CDOPricerSemiAnalytic(
        new SyntheticCDO[] { GetLCDO(maturity) }, Dt.Empty, asOf_, settle_,
        lcdsPricer.DiscountCurve,
        new SurvivalCurve[] { lcdsPricer.SurvivalCurve },
        null,
        new Copula(), new SingleFactorCorrelation(new string[1], 0.0),
        stepSize, stepUnit,
        0, 0.0, null, false, true)[0];
#if Not_Include
      // For exact results, we need to add the LCDS cashflow dates
      // to the CDO time grid.
      // Generate out payment dates from settlement.
      LCDS lcds = (LCDS)lcdsPricer.CDS;
      Schedule sched = new Schedule(lcds.Effective, lcds.Effective, lcds.FirstPrem,
        lcds.Maturity, lcds.Maturity, lcds.Freq, lcds.BDConvention, lcds.Calendar,
        false, false, false);
      for (int i = 0; i < sched.Count; i++)
      {
        Dt date = sched.GetPaymentDate(i);
        if (Dt.Cmp(date, settle_) > 0)
          lcdoPricer.Basket.AddGridDates(date);
      }
#endif

      // Loosely test the equality of break even spread from LCDS and LCDO pricers.
      // Small differences exist due different time grid used in the two pricers.
      double actual = lcdoPricer.BreakEvenPremium() * 10000;
      double expect = lcdsPricer.BreakEvenPremium() * 10000;
      AssertEqual("Spread", expect, actual, 2E-3 * expect);

      // Test the equality of cumulative expected losses on time grid.
      // Here we generate the time grid by hand.  Hence the equality should
      // hold exactly.
      Dt current = settle_;
      while (Dt.Cmp(current, maturity) < 0)
      {
        current = Dt.Add(current, stepSize, stepUnit);
        if (Dt.Cmp(current, maturity) > 0)
          current = maturity;
        expect = lcdsPricer.ExpectedLossRate(settle_, current); 
        actual = lcdoPricer.Basket.CalcLossDistribution(
          false, current, new double[] { 0.0, 1.0 })[1, 1];
        AssertEqual("EL(" + current + ")", expect, actual, 1E-15);
      }

      return;
    }

    #endregion // Tests

    #region Properties
    public double DiscountRate { get; set; } = 0.04;

    public double RecoveryRate { get; set; } = 0.70;

    public double PrepayRate { get; set; } = 0.0;

    public double Correlation { get; set; } = 0.0;

    public double[] Spreads { get; set; } = null;

    public string[] Tenors { get; set; } = null;

    #endregion Properties

    #region Data

    private Dt asOf_, settle_, effective_, maturity_;

    private double hazardRate_ = 0.05;
    private string tenor_ = "5Y";

    private int stepSize_ = 3;
    private TimeUnit stepUnit_ = TimeUnit.Months;
    private Dt[] maturities_ = null;
    private DiscountCurve discountCurve_;

    #endregion // Data
  }
}
